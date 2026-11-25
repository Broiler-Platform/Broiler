"""Inventory Broiler package owners and evaluate release graphs without restoring."""
import argparse
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
import json
import os
from pathlib import Path
import platform
import subprocess
import sys
from urllib.error import HTTPError
from urllib.request import urlopen
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "artifacts/nuget-publish-order"
REGISTRY = ROOT / "eng/nuget-publishers.json"
BASELINE = ROOT / "docs/nuget-package-publish-order.json"
PROJECT_SUFFIXES = {".csproj", ".fsproj", ".vbproj"}
SKIP = {"bin", "obj", "artifacts", "node_modules", "samples", "demos", "benchmarks",
        "docs", "fixtures", "testdata", "probes", "referencearms", "adversarial"}
PROPERTIES = ("PackageId,PackageVersion,IsPackable,TargetFramework,TargetFrameworks,"
              "RuntimeIdentifier,RuntimeIdentifiers,IncludeBuildOutput,IncludeSymbols")
ITEMS = "PackageReference,PackageVersion,ProjectReference"


def run(command, cwd=ROOT):
    return subprocess.run(command, cwd=cwd, capture_output=True, text=True,
                          encoding="utf-8", errors="replace", timeout=90)


def relative(path):
    return path.relative_to(ROOT).as_posix()


def tracked(repo):
    result = run(["git", "ls-files", "-z"], cwd=repo)
    if result.returncode:
        raise ValueError(result.stderr.strip())
    return [repo / name for name in result.stdout.split("\0") if name]


def component_projects(repo, working_tree):
    try:
        paths = tracked(repo)
    except ValueError:
        if not working_tree:
            raise
        paths = []
        for directory, dirs, files in os.walk(repo):
            dirs[:] = [name for name in dirs if not name.startswith(".")
                       and name.casefold() not in SKIP and not (Path(directory) / name / ".git").exists()]
            paths.extend(Path(directory) / name for name in files)
    return {path for path in paths if path.suffix in PROJECT_SUFFIXES
            and SKIP.isdisjoint(part.casefold() for part in path.relative_to(repo).parts[:-1])}


def gitlinks():
    result = run(["git", "ls-files", "--stage", "-z"])
    result.check_returncode()
    pins = {}
    for line in filter(None, result.stdout.split("\0")):
        meta, name = line.split("\t", 1)
        mode, sha, _ = meta.split()
        if mode == "160000" and name.startswith("Broiler."):
            pins[name] = sha
    return pins


def targets(repo, selectors):
    projects = set()
    for selector in selectors:
        path = repo / selector
        if not path.is_file():
            raise ValueError(f"Missing pack target {relative(path)}")
        if path.suffix == ".slnx":
            projects.update((path.parent / entry.attrib["Path"].replace("\\", "/")).resolve()
                            for entry in ET.parse(path).iter("Project") if "Path" in entry.attrib)
        elif path.suffix == ".slnf":
            solution = json.loads(path.read_text(encoding="utf-8-sig"))["solution"]
            base = (path.parent / solution["path"].replace("\\", "/")).parent
            projects.update((base / entry.replace("\\", "/")).resolve() for entry in solution["projects"])
        elif path.suffix in PROJECT_SUFFIXES:
            projects.add(path)
        else:
            raise ValueError(f"Unsupported pack target {relative(path)}")
    return projects


def split(value):
    return [item for item in value.split(";") if item]


def evaluate(path, repo, configurations, properties=()):
    row = {"project": relative(path), "repository": relative(repo), "profiles": [], "errors": []}

    def query(configuration, framework="", runtime=""):
        args = ["dotnet", "msbuild", str(path), "-nologo", f"-p:Configuration={configuration}",
                "-p:UseBroilerUiSource=false", "-p:UseBroilerDocumentsFormatCodesSource=false",
                "-getProperty:" + PROPERTIES, "-getItem:" + ITEMS]
        args.extend(f"-p:{name}={value}" for name, value in properties)
        if framework:
            args.append("-p:TargetFramework=" + framework)
        if runtime:
            args.append("-p:RuntimeIdentifier=" + runtime)
        context = dict(configuration=configuration, framework=framework, runtime=runtime)
        try:
            result = run(args, cwd=repo)
            if result.returncode:
                raise ValueError((result.stdout + result.stderr).strip()[-3000:])
            data = json.loads(result.stdout[result.stdout.index("{"):])
            props = data["Properties"]
            items = data["Items"]
        except (ValueError, OSError, subprocess.TimeoutExpired) as error:
            row["errors"].append({**context, "message": str(error)})
            return None

        versions = {entry["Identity"].casefold(): entry
                    for entry in items["PackageVersion"]}
        references = []
        for entry in items["PackageReference"]:
            central = versions.get(entry["Identity"].casefold())
            metadata = "VersionOverride" if entry.get("VersionOverride") else "Version"
            declaration = entry if entry.get(metadata) or not central else central
            source = declaration.get("DefiningProjectFullPath", "")
            if source and Path(source).is_relative_to(ROOT):
                source = relative(Path(source))
            references.append(dict(id=entry["Identity"], version=declaration.get(metadata, ""),
                declaration=source, itemType="PackageReference" if declaration is entry else "PackageVersion",
                versionMetadata=metadata,
                privateAssets=entry.get("PrivateAssets", ""),
                includeAssets=entry.get("IncludeAssets", ""), excludeAssets=entry.get("ExcludeAssets", "")))
        profile = {**context, "framework": props["TargetFramework"], "runtime": props["RuntimeIdentifier"],
                   "package": props["PackageId"], "version": props["PackageVersion"],
                   "includesBuildOutput": props["IncludeBuildOutput"].casefold() != "false",
                   "includesSymbols": props["IncludeSymbols"].casefold() == "true",
                   "packable": props["IsPackable"].casefold() == "true", "packages": references,
                   "references": [{"project": relative(Path(ref["FullPath"]).resolve())
                                   if Path(ref["FullPath"]).is_relative_to(ROOT) else ref["FullPath"],
                                   "referenceOutputAssembly": ref.get("ReferenceOutputAssembly", ""),
                                   "privateAssets": ref.get("PrivateAssets", "")}
                                  for ref in items["ProjectReference"]]}
        return props, profile

    for configuration in configurations:
        outer = query(configuration)
        if outer is None:
            continue
        frameworks = split(outer[0]["TargetFrameworks"])
        variants = [query(configuration, framework) for framework in frameworks] if frameworks else [outer]
        for variant in filter(None, variants):
            props, profile = variant
            row["profiles"].append(profile)
            for runtime in split(props["RuntimeIdentifiers"]):
                if runtime != profile["runtime"]:
                    resolved = query(configuration, profile["framework"], runtime)
                    if resolved:
                        row["profiles"].append(resolved[1])
    return row


def layers(graph):
    remaining = {node: set(edges) & graph.keys() for node, edges in graph.items()}
    waves = []
    while remaining:
        ready = sorted(node for node, edges in remaining.items() if not edges)
        if not ready:
            break
        waves.append(ready)
        for node in ready:
            del remaining[node]
        for edges in remaining.values():
            edges.difference_update(ready)
    return waves, {node: sorted(edges) for node, edges in remaining.items()}


def feed_versions(base, package):
    try:
        with urlopen(f"{base.rstrip('/')}/{package}/index.json", timeout=15) as response:
            versions = json.load(response)["versions"]
        if not isinstance(versions, list) or not all(isinstance(item, str) for item in versions):
            raise ValueError("Unexpected version index")
        return versions
    except HTTPError as error:
        if error.code == 404:
            return []
        raise


def write_json(name, data):
    (OUTPUT / name).write_text(json.dumps(data, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--component-heads", action="store_true",
                        help="Inventory the selected component commits independently of the aggregate gitlinks")
    parser.add_argument("--working-tree", action="store_true",
                        help="Allow a local source snapshot without access to component Git metadata")
    parser.add_argument("--check-feed", action="store_true", help="Check package identities on NuGet.org")
    parser.add_argument("--reuse-evaluation", action="store_true",
                        help="Reuse the previous local MSBuild snapshot only on an unchanged tree")
    args = parser.parse_args()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    config = json.loads(REGISTRY.read_text(encoding="utf-8"))
    errors = []
    pins = gitlinks()
    repos = sorted({*pins, *config["publishers"]})
    root_head = run(["git", "rev-parse", "HEAD"]).stdout.strip()
    revisions = {".": {"commit": root_head}}
    project_owners = {path: ROOT for path in component_projects(ROOT, False) if path.is_file()}
    declared = {}
    legacy = set()
    configurations = {}

    for name in repos:
        repo = ROOT / name
        if not repo.is_dir() or (name != "." and not (repo / ".git").exists()):
            errors.append(f"{name}: missing component checkout")
            continue
        revision = run(["git", "rev-parse", "HEAD"], cwd=repo)
        commit = revision.stdout.strip() if revision.returncode == 0 else None
        revisions[name] = {"gitlink": pins.get(name), "commit": commit}
        if not args.working_tree and (not commit or
                (not args.component_heads and name in pins and commit != pins[name])):
            errors.append(f"{name}: expected checkout at {pins.get(name)}, found {commit or revision.stderr.strip()}")
        if name != ".":
            try:
                project_owners.update({path: repo for path in component_projects(repo, args.working_tree) if path.is_file()})
            except ValueError as error:
                errors.append(f"{name}: {error}")

        cohorts = config["publishers"].get(name, {})
        workflows = repo / ".github/workflows"
        for workflow in workflows.glob("*.yml"):
            if "dotnet nuget push" in workflow.read_text(encoding="utf-8") and workflow.name not in cohorts:
                errors.append(f"{name}: publisher {workflow.name} has no registry entry")
        for publisher, cohort in cohorts.items():
            try:
                publication = workflows / publisher
                test = workflows / cohort["test"]
                if not publication.is_file() or "dotnet nuget push" not in publication.read_text(encoding="utf-8"):
                    raise ValueError(f"Missing NuGet publication entry point {relative(publication)}")
                if not test.is_file():
                    raise ValueError(f"Missing test entry point {relative(test)}")
                selected = targets(repo, cohort["targets"]) - targets(repo, cohort.get("excludeTargets", []))
                if not selected:
                    raise ValueError("No projects in the pack targets")
                for project in selected:
                    component = relative(project).split("/")[0]
                    project_repo = ROOT / component if name == "." and component in pins else repo
                    project_owners.setdefault(project, project_repo)
                    if cohort.get("legacy"):
                        legacy.add(project)
                    else:
                        declared.setdefault(project, []).append({
                            "repository": name, "workflow": relative(publication),
                            "test": relative(test), "cohort": f"{name}/{publisher}"})
                    configurations.setdefault(project, {"Release"}).update(cohort.get("configurations", []))
            except (ValueError, OSError, ET.ParseError) as error:
                errors.append(f"{name}/{publisher}: {error}")

    rows_path = OUTPUT / "evaluated-projects.json"
    if args.reuse_evaluation:
        snapshot = json.loads(rows_path.read_text(encoding="utf-8"))
        rows = snapshot["projects"]
        if (snapshot["revisions"] != revisions or
                {row["project"] for row in rows} != {relative(path) for path in project_owners}):
            raise ValueError("Checkout or selection changed. Run a fresh evaluation.")
    else:
        print(f"Evaluating {len(project_owners)} projects", flush=True)
        rows = []
        with ThreadPoolExecutor(max_workers=6) as executor:
            evaluations = executor.map(lambda path: evaluate(
                path, project_owners[path], sorted(configurations.get(path, {"Release"}))),
                sorted(project_owners))
            for index, row in enumerate(evaluations, 1):
                rows.append(row)
                if index % 100 == 0:
                    print(f"Evaluated {index} projects", flush=True)
        write_json(rows_path.name, {"revisions": revisions, "projects": rows})

    by_project = {os.path.normcase(row["project"]): row for row in rows}
    packages = {}
    for row in rows:
        project = ROOT / row["project"]
        errors.extend(f"{row['project']} [{item['configuration']}/{item['framework']}/{item['runtime']}]: {item['message']}"
                      for item in row["errors"])
        for profile in row["profiles"]:
            package = profile["package"]
            if not profile["packable"] or not package.casefold().startswith("broiler."):
                continue
            key = package.casefold()
            record = packages.setdefault(key, {"id": package, "projects": {}})
            record["projects"][row["project"]] = {
                "sourceRepository": row["repository"], "publishers": declared.get(project, []),
                "legacyRootTarget": project in legacy}

    exceptions = {key.casefold(): value for key, value in config.get("exceptions", {}).items()}
    package_graph = {}
    package_projects = {}
    overlaps = []
    unknown = set()

    def dependencies(row, visited):
        if row["project"] in visited:
            return set()
        visited = visited | {row["project"]}
        required = set()
        for profile in row["profiles"]:
            required.update(item["id"].casefold() for item in profile["packages"]
                            if item["id"].casefold().startswith("broiler."))
            for ref in profile["references"]:
                target = by_project.get(os.path.normcase(ref["project"]))
                if target is None:
                    errors.append(f"{row['project']}: unevaluated project reference {ref['project']}")
                    continue
                owned = {p["package"].casefold() for p in target["profiles"]
                         if p["packable"] and p["package"].casefold() in packages}
                required.update(owned or dependencies(target, visited))
        return required

    for key, record in packages.items():
        producers = [p for path in record["projects"].values() for p in path["publishers"]]
        legacy_match = any(path["legacyRootTarget"] for path in record["projects"].values())
        if len(producers) > 1:
            errors.append(f"{record['id']}: ambiguous publishers {producers}")
        if len(producers) == 1:
            record["publisher"] = producers[0]
            record["classification"] = "offered"
            if legacy_match:
                overlaps.append(record["id"])
            project = next(path for path, item in record["projects"].items() if item["publishers"])
            package_projects[key] = project
            package_graph[key] = dependencies(by_project[os.path.normcase(project)], set())
        elif legacy_match:
            root = config["publishers"]["."]["nuget-packages.yml"]
            record["publisher"] = {"repository": ".", "workflow": ".github/workflows/nuget-packages.yml",
                                   "test": f".github/workflows/{root['test']}", "cohort": "./nuget-packages.yml"}
            record["classification"] = "offered"
            project = next(path for path, item in record["projects"].items() if item["legacyRootTarget"])
            package_projects[key] = project
            package_graph[key] = dependencies(by_project[os.path.normcase(project)], set())
        else:
            record["classification"] = exceptions.get(key, {}).get("classification", "internal")

    populated = {record["publisher"]["cohort"] for record in packages.values() if "publisher" in record}
    for name, cohorts in config["publishers"].items():
        for workflow, cohort in cohorts.items():
            if not cohort.get("legacy") and f"{name}/{workflow}" not in populated:
                errors.append(f"{name}/{workflow}: pack targets contain no designated Broiler package")

    for package, edges in package_graph.items():
        for dependency in edges - package_graph.keys():
            if dependency not in exceptions or exceptions[dependency]["classification"] != "published-prerequisite":
                unknown.add(dependency)
                errors.append(f"{packages[package]['id']}: no release owner for prerequisite {dependency}")

    if args.check_feed:
        try:
            with urlopen("https://api.nuget.org/v3/index.json", timeout=15) as response:
                service = json.load(response)
            base = next(item["@id"] for item in service["resources"] if item["@type"] == "PackageBaseAddress/3.0.0")
        except (OSError, ValueError, KeyError, StopIteration) as error:
            errors.append(f"NuGet.org service index: {error}")
        else:
            def check(key):
                try:
                    versions = feed_versions(base, key)
                    return key, versions, None
                except (OSError, ValueError, KeyError) as error:
                    return key, [], str(error)

            with ThreadPoolExecutor(max_workers=8) as executor:
                feed_keys = sorted(packages.keys() | {key for key, exception in exceptions.items()
                                   if exception["classification"] == "published-prerequisite"})
                for key, versions, error in executor.map(check, feed_keys):
                    if error:
                        errors.append(f"{key}: NuGet.org lookup failed: {error}")
                        continue
                    record = packages.get(key)
                    if record is not None:
                        record["publicVersions"] = versions
                        if record["classification"] == "offered" and not versions:
                            record["classification"] = "pending"
                        elif record["classification"] == "internal" and versions:
                            errors.append(f"{key}: published identity has no designated publisher or retirement declaration")
                    if key in exceptions and exceptions[key]["classification"] == "published-prerequisite":
                        exceptions[key]["publicVersions"] = versions
                        if not versions:
                            errors.append(f"{key}: declared public prerequisite has no NuGet.org versions")

    waves, blocked = layers(package_graph)
    cohort_graph = {}
    for key, edges in package_graph.items():
        cohort = packages[key]["publisher"]["cohort"]
        prerequisites = {packages[dep]["publisher"]["cohort"] for dep in edges & package_graph.keys()}
        cohort_graph.setdefault(cohort, set()).update(prerequisites - {cohort})
    cohort_waves, blocked_cohorts = layers(cohort_graph)
    if blocked:
        errors.append(f"Package publication order blocked: {blocked}")
    if blocked_cohorts:
        errors.append(f"Release cohort order blocked: {blocked_cohorts}")

    def names(keys):
        return sorted(packages[key]["id"] if key in packages else key for key in keys)

    previous = json.loads(BASELINE.read_text(encoding="utf-8"))["packageProjects"]
    previous = {package.casefold(): {"id": package, "project": project}
                for package, project in previous.items()}
    baseline_changes = {
        "added": names(package_projects.keys() - previous.keys()),
        "removed": sorted(previous[key]["id"] for key in previous.keys() - package_projects.keys()),
        "moved": [{"id": packages[key]["id"], "from": previous[key]["project"], "to": package_projects[key]}
                  for key in sorted(package_projects.keys() & previous.keys())
                  if previous[key]["project"] != package_projects[key]],
    }
    classifications = Counter(record["classification"] for record in packages.values())
    result = dict(packageCount=len(package_graph), discoveredPackageCount=len(packages), os=platform.system(),
                  revisions=revisions, workingTree=args.working_tree, componentHeads=args.component_heads,
                  checkedFeed=args.check_feed, classifications=dict(classifications), baselineChanges=baseline_changes,
                  packages=list(packages.values()), exceptions=exceptions,
                  packageProjects={packages[key]["id"]: path for key, path in package_projects.items()},
                  graph={packages[key]["id"]: names(edges) for key, edges in package_graph.items()},
                  packageLayers=[names(wave) for wave in waves], blockedPackages=blocked,
                  cohortDependencies={key: sorted(edges) for key, edges in cohort_graph.items()},
                  cohortLayers=cohort_waves, blockedCohorts=blocked_cohorts,
                  legacyRootOverlaps=sorted(overlaps), missingPackageOwners=sorted(unknown),
                  errors=sorted(set(errors)))
    write_json("publish-order.json", result)
    status = "FAILED" if errors else "Passed"
    summary = [f"## Broiler NuGet inventory — {status}", "",
               f"- Projects evaluated: {len(rows)}",
               f"- Broiler package IDs discovered: {len(packages)}",
               f"- Designated package IDs: {len(package_graph)}",
               f"- Package waves: {len(waves)}",
               f"- Release cohort waves: {len(cohort_waves)}",
               f"- Legacy root publisher overlaps: {len(overlaps)}",
               f"- NuGet.org queried: {'yes' if args.check_feed else 'no'}", "",
               "### Classifications", "",
               *[f"- {name}: {count}" for name, count in sorted(classifications.items())], "",
               "### Changes from the October inventory", "",
               *[f"- {name.capitalize()}: {len(items)}" for name, items in baseline_changes.items()], "",
               "### Legacy root overlaps", "",
               *[f"- {package}" for package in sorted(overlaps)], "",
               "### Diagnostics", "",
               *[f"- {error}" for error in result["errors"]]]
    (OUTPUT / "inventory-summary.md").write_text("\n".join(summary) + "\n", encoding="utf-8")
    print("\n".join(summary[:10]), flush=True)
    for error in result["errors"]:
        print(error, file=sys.stderr)
    return int(bool(errors))


if __name__ == "__main__":
    sys.exit(main())
