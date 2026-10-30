"""Evaluate MSBuild package dependencies without restoring or building projects."""
import concurrent.futures
import argparse
import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "artifacts/nuget-publish-order"
SKIP = {".git", ".claude", "bin", "obj", "artifacts", "node_modules", "_site", "tests", "test", "samples", "demos", "benchmarks", "eng", "tools", "docs"}


def projects():
    found = []
    for owner in sorted(ROOT.glob("Broiler.*")):
        if not owner.is_dir():
            continue
        for directory, dirs, files in os.walk(owner):
            dirs[:] = [name for name in dirs if name.lower() not in SKIP and not (Path(directory) / name / ".git").exists()]
            for name in files:
                if name.endswith(".csproj"):
                    found.append(Path(directory) / name)
    return found


def evaluate(path, framework=None):
    args = ["dotnet", "msbuild", str(path), "-nologo", "-p:Configuration=Release", "-p:UseBroilerUiSource=false",
            "-getProperty:PackageId,IsPackable,TargetFrameworks", "-getItem:PackageReference,ProjectReference"]
    if framework:
        args.append("-p:TargetFramework=" + framework)
    result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=90)
    if result.returncode:
        return {"project": str(path.relative_to(ROOT)), "error": (result.stdout + result.stderr)[-2500:]}
    start = result.stdout.find('{\n')
    if start < 0:
        start = result.stdout.find('{\r\n')
    data = json.loads(result.stdout[start:])
    row = {"project": str(path.relative_to(ROOT)), "package": data['Properties']['PackageId'],
           "packable": data['Properties']['IsPackable'] == 'true',
           "packages": [{"id": r['Identity'], "private": r.get('PrivateAssets') == 'all'} for r in data['Items']['PackageReference'] if r['Identity'].startswith('Broiler.')],
           "references": [r['FullPath'] for r in data['Items']['ProjectReference']],
           "frameworks": data['Properties'].get('TargetFrameworks', '')}
    if not framework and row['frameworks']:
        for target in row['frameworks'].split(';'):
            variant = evaluate(path, target)
            if 'error' in variant:
                row.setdefault('variantErrors', []).append(variant)
            else:
                row['packages'].extend(p for p in variant['packages'] if p not in row['packages'])
                row['references'].extend(p for p in variant['references'] if p not in row['references'])
    return row


def layers(graph):
    remaining = {k: set(v) & graph.keys() for k, v in graph.items()}
    result = []
    while remaining:
        ready = sorted(k for k, dependencies in remaining.items() if not dependencies)
        if not ready:
            break
        result.append(ready)
        for name in ready:
            del remaining[name]
        for dependencies in remaining.values():
            dependencies.difference_update(ready)
    return result, {k: sorted(v) for k, v in remaining.items()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reuse-evaluation', action='store_true', help='Reuse the previous snapshot; omit after project/dependency changes')
    args = parser.parse_args()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    candidates = projects()
    print(f"Evaluating {len(candidates)} source projects", flush=True)
    rows = []
    if args.reuse_evaluation:
        selected = {str(p.relative_to(ROOT)) for p in candidates}
        rows = [r for r in json.loads((OUTPUT / 'evaluated-projects.json').read_text()) if r['project'] in selected]
        if {r['project'] for r in rows} != selected:
            raise ValueError('Project set changed; perform a fresh evaluation')
    else:
        with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
            for index, row in enumerate(pool.map(evaluate, candidates), 1):
                rows.append(row)
                if index % 50 == 0:
                    print(f"Evaluated {index}/{len(candidates)}", flush=True)
    (OUTPUT / 'evaluated-projects.json').write_text(json.dumps(rows, indent=2), encoding='utf-8')
    by_path = {str((ROOT / r['project']).resolve()).lower(): r for r in rows if 'error' not in r}
    packages = {r['package']: r for r in rows if r.get('packable') and r.get('package', '').startswith('Broiler.')}
    if len(packages) != sum(bool(r.get('packable') and r.get('package', '').startswith('Broiler.')) for r in rows):
        raise ValueError('Duplicate package identities; inspect the evaluation snapshot')
    def dependencies(row, visited):
        if row['project'] in visited:
            return set()
        visited = visited | {row['project']}
        result = {r['id'] for r in row['packages']}
        for reference in row['references']:
            target = by_path.get(str(Path(reference).resolve()).lower())
            if not target:
                raise ValueError('Referenced source project was not evaluated: ' + reference)
            if target.get('packable'):
                result.add(target['package'])
            else:
                # Build-time tools can consume published packages even when the
                # referenced tool itself does not produce a NuGet package.
                result.update(dependencies(target, visited))
        return result
    graph = {}
    for name, row in packages.items():
        graph[name] = sorted(dependencies(row, set()))
    order, blocked = layers(graph)
    owners = {name: Path(row['project']).parts[0] for name, row in packages.items()}
    component_graph = {owner: set() for owner in owners.values()}
    for name, deps in graph.items():
        for dependency in deps:
            if dependency in owners and owners[name] != owners[dependency]:
                component_graph[owners[name]].add(owners[dependency])
    component_order, component_blocked = layers(component_graph)
    if not blocked:
        position = {name: wave for wave, names in enumerate(order) for name in names}
        for name, deps in graph.items():
            for dependency in deps:
                if dependency in position and position[dependency] >= position[name]:
                    raise ValueError('Invalid publication order: ' + name + ' / ' + dependency)
    result = dict(packageCount=len(graph), graph=graph, packageLayers=order, blockedPackages=blocked,
                  packageProjects={name: row['project'].replace('\\', '/') for name, row in packages.items()},
                  componentDependencies={k: sorted(v) for k, v in component_graph.items()}, componentLayers=component_order,
                  blockedComponents=component_blocked, missingPackageOwners=sorted({d for deps in graph.values() for d in deps if d not in graph}),
                  evaluationErrors=[r for r in rows if 'error' in r or r.get('variantErrors')])
    (OUTPUT / 'publish-order.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k not in ('graph', 'packageLayers', 'evaluationErrors')}, indent=2), flush=True)


if __name__ == '__main__':
    main()
