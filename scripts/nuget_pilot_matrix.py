"""Build, test and verify the Phase 3 release candidates without publishing."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
from tempfile import TemporaryDirectory
import time
from urllib.parse import unquote, urlsplit
from urllib.request import url2pathname
import xml.etree.ElementTree as ET
from zipfile import BadZipFile, ZipFile

from nuget_patch_drafts import verify_edits
from nuget_patches import FEED, declaration_spans, version
from nuget_publish_order import ROOT, evaluate, targets

PILOTS = {
    "Broiler.DateTime": "Broiler.DateTime.slnx",
    "Broiler.DOM": "Broiler.Dom.slnx",
    "Broiler.CSS": "Broiler.CSS.slnx",
}
SMOKES = {
    "broiler.datetime": 'Broiler.DateTime.ExtendedIsoDateTime.Parse("2024-02-29T00:00:00Z").Year == 2024',
    "broiler.dom": 'new Broiler.Dom.DomDocument().CreateElement("div").LocalName == "div"',
    "broiler.dom.html": 'Broiler.Dom.Html.HtmlDocumentParser.ParseDocument("<title>Pilot</title>").Title == "Pilot"',
    "broiler.css": 'Broiler.CSS.CssColor4.TryParse("color(srgb 0 0.5 0.5)", out var color) && System.Math.Round(color.G) == 128',
    "broiler.css.dom": 'new Broiler.CSS.Dom.CssSelectorMatcher().Matches(new Broiler.Dom.DomDocument().CreateElement("div"), "div")',
}
CONFLICTS = re.compile(r"\bNU(?:1107|1605|1608)\b")
TEST_COUNTS = re.compile(r"\bPassed:\s*([\d,]+)", re.I)


class CommandFailed(ValueError):
    def __init__(self, label, output):
        self.label, self.output = label, output
        super().__init__(f"{label} failed\n{output[-4000:]}")


def command(args, cwd, label, log, env=None):
    result = subprocess.run(args, cwd=cwd, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                            text=True, encoding="utf-8", errors="replace", timeout=600)
    log.write_text(result.stdout, encoding="utf-8")
    if result.returncode or CONFLICTS.search(result.stdout):
        raise CommandFailed(label, result.stdout)
    return result.stdout.strip()


def git(repo, *args):
    result = subprocess.run(["git", *args], cwd=repo, capture_output=True, text=True, timeout=120)
    if result.returncode:
        raise ValueError(result.stderr.strip())
    return result.stdout.strip()


def checkout(repo, path):
    source = ROOT / repo
    commit = git(source, "rev-parse", "HEAD")
    git(source, "-c", "core.longpaths=true", "clone", "--quiet", "--shared", "--no-checkout",
        str(source), str(path))
    git(path, "config", "core.longpaths", "true")
    git(path, "config", "core.autocrlf", "false")
    git(path, "remote", "set-url", "origin", git(source, "remote", "get-url", "origin"))
    git(path, "checkout", "--quiet", "--detach", commit)
    return commit


def write_config(path, feed, ids, alternate=None):
    root = ET.Element("configuration")
    sources = ET.SubElement(root, "packageSources")
    ET.SubElement(sources, "clear")
    ET.SubElement(sources, "add", key="nuget.org", value=FEED)
    mapping = ET.SubElement(root, "packageSourceMapping")
    ET.SubElement(mapping, "clear")
    public = ET.SubElement(mapping, "packageSource", key="nuget.org")
    ET.SubElement(public, "package", pattern="*")
    ET.SubElement(ET.SubElement(root, "fallbackPackageFolders"), "clear")
    for name, folder, patterns in (
            ("candidate", feed, sorted(set(ids) - (set(alternate[1]) if alternate else set()))),
            ("unexpected", alternate[0] if alternate else None, alternate[1] if alternate else [])):
        if patterns:
            ET.SubElement(sources, "add", key=name, value=str(folder))
            source = ET.SubElement(mapping, "packageSource", key=name)
            for package in sorted(patterns):
                ET.SubElement(source, "package", pattern=package)
    ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)


def restore_env(config, cache):
    return {**os.environ, "RestoreConfigFile": str(config), "NUGET_PACKAGES": str(cache),
            "NUGET_HTTP_CACHE_PATH": str(cache.with_name(cache.name + "-http")),
            "DisableImplicitLibraryPacksFolder": "true", "DOTNET_NOLOGO": "true",
            "DOTNET_CLI_UI_LANGUAGE": "en-US", "MSBUILDDISABLENODEREUSE": "1",
            "DOTNET_CLI_USE_MSBUILD_SERVER": "0", "UseSharedCompilation": "false"}


def with_candidate_dom(repo, candidate):
    path = repo / "Directory.Packages.props"
    data = path.read_bytes()
    declared = [entry for entry in ET.fromstring(data).iter("PackageVersion")
                if entry.get("Include", "").casefold() == "broiler.dom"]
    if len(declared) != 1 or not version(declared[0].get("Version", "")):
        raise ValueError("CSS must declare one literal Broiler.Dom version for this pilot")
    current = declared[0].get("Version")
    spans = declaration_spans(data, "broiler.dom", "PackageVersion", "Version", current)
    if len(spans) != 1:
        raise ValueError("Could not locate the CSS DOM version declaration")
    start, end = spans[0]
    path.write_bytes(data[:start] + candidate.encode() + data[end:])
    return current


def apply_datetime_patch(worktree, commit, plan_path):
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    if plan["errors"]:
        raise ValueError("Phase 2 report has errors")
    selected = [group for group in plan["groups"] if group["repository"] == "Broiler.DateTime"
                and group["package"] == "microsoft.net.test.sdk" and group["status"] == "validated"]
    if not selected:
        raise ValueError("Phase 2 report has no validated DateTime test SDK patch")
    group, = selected
    if group["commit"] != commit:
        raise ValueError("Phase 2 DateTime commit differs from the selected source commit")
    if group["paths"] != ["Directory.Packages.props"]:
        raise ValueError("Expected only the DateTime central test SDK declaration to change")
    patch = (plan_path.parent / group["patch"]).resolve()
    if patch.parent != plan_path.parent:
        raise ValueError("Patch lies outside its plan directory")
    git(worktree, "apply", "--index", "--", str(patch))
    paths = git(worktree, "diff", "--cached", "--name-status", "--no-renames").splitlines()
    if set(paths) != {f"M\t{path}" for path in group["paths"]}:
        raise ValueError("Patch includes unexpected paths")
    raw = git(worktree, "diff", "--cached", "--raw", "--no-renames")
    if not raw.startswith(":100644 100644 "):
        raise ValueError("Phase 2 patch changed the declaration file mode")
    verify_edits(worktree, group, group["paths"], set())
    return {"package": group["package"], "updates": group["updates"],
            "sha256": hashlib.sha256(patch.read_bytes()).hexdigest()}


def package_sources(sln, worktree, number):
    expected = {}
    for path in sorted(targets(worktree, [sln])):
        row = evaluate(path, worktree, ["Release"])
        if row["errors"]:
            raise ValueError(f"MSBuild evaluation failed: {row}")
        if any(profile["packable"] for profile in row["profiles"]):
            expected[path] = row
    if not expected:
        raise ValueError(f"No packages selected by {sln}")
    bases = {version(profile["version"]) for row in expected.values() for profile in row["profiles"]}
    if not all(bases) or len({value[:3] for value in bases}) != 1:
        raise ValueError(f"Unexpected producer package version for {sln}")
    base = ".".join(map(str, next(iter(bases))[:3]))
    candidate = f"{base}-preview.{number}"
    properties = (("Version", candidate), ("PackageVersion", candidate))
    for path in expected:
        row = evaluate(path, worktree, ["Release"], properties)
        if row["errors"] or not any(profile["packable"] and profile["version"] == candidate
                                    for profile in row["profiles"]):
            raise ValueError(f"{row['project']}: candidate MSBuild evaluation failed: {row['errors']}")
        expected[path] = row
    return expected, candidate


def public_contract(sources, candidate):
    ids = {row["project"]: next(profile["package"] for profile in row["profiles"] if profile["packable"])
           for row in sources.values()}
    contracts = {}
    for row in sources.values():
        package = ids[row["project"]]
        groups = {}
        for profile in row["profiles"]:
            framework = profile["framework"]
            if not profile["packable"] or profile["runtime"]:
                continue
            dependencies = {entry["id"].casefold(): entry["version"] for entry in profile["packages"]
                            if entry["privateAssets"].casefold() != "all"}
            for reference in profile["references"]:
                if reference["privateAssets"].casefold() == "all" or reference["referenceOutputAssembly"].casefold() == "false":
                    continue
                if reference["project"] not in ids:
                    raise ValueError(f"{package}: public project reference is outside the cohort")
                dependencies[ids[reference["project"]].casefold()] = candidate
            groups[framework] = dependencies
        contracts[package.casefold()] = groups
    return contracts


def range_key(value):
    value = value.strip()
    if version(value):
        return True, version(value), None, False
    if value and value[0] in "[(" and value[-1] in "])":
        bounds = value[1:-1].split(",")
        if len(bounds) == 1 and value[0] == "[" and value[-1] == "]" and version(bounds[0].strip()):
            exact = version(bounds[0].strip())
            return True, exact, exact, True
        if len(bounds) == 2 and all(not bound.strip() or version(bound.strip()) for bound in bounds):
            return value[0] == "[", version(bounds[0].strip()), version(bounds[1].strip()), value[-1] == "]"
    raise ValueError(f"Unsupported pilot dependency range {value!r}")


def inspect_package(path, contracts, candidate):
    with ZipFile(path) as archive:
        nuspec, = [name for name in archive.namelist() if name.lower().endswith(".nuspec")]
        manifest = ET.fromstring(archive.read(nuspec)).find("{*}metadata")
        if manifest is None or not manifest.findtext("{*}id") or not manifest.findtext("{*}version"):
            raise ValueError(f"{path.name}: incomplete nuspec identity")
        identity = manifest.findtext("{*}id")
        package = identity.casefold()
        declared = manifest.findtext("{*}version")
        dependencies = manifest.find("{*}dependencies")
        groups = {}
        if dependencies is not None:
            if dependencies.findall("{*}dependency"):
                raise ValueError(f"{identity}: unexpected ungrouped dependencies")
            for group in dependencies.findall("{*}group"):
                framework = group.attrib["targetFramework"]
                if framework in groups:
                    raise ValueError(f"{identity}: duplicate dependency group for {framework}")
                entries = group.findall("{*}dependency")
                groups[framework] = {item.attrib["id"].casefold(): item.attrib["version"] for item in entries}
                if len(groups[framework]) != len(entries):
                    raise ValueError(f"{identity}: duplicate dependency ID")
        if package not in contracts or version(declared) != version(candidate):
            raise ValueError(f"Unexpected candidate identity {identity} {declared}")
        expected = contracts[package]
        if not groups and all(not items for items in expected.values()):
            groups = {framework: {} for framework in expected}
        normalize = lambda graph: {framework: {key: range_key(value) for key, value in items.items()}
                                   for framework, items in graph.items()}
        if normalize(groups) != normalize(expected):
            raise ValueError(f"{identity}: dependency range mismatch. Expected {expected}. Packed {groups}.")
        for framework in expected:
            if not any(name.startswith(f"lib/{framework}/") and name.endswith(".dll") for name in archive.namelist()):
                raise ValueError(f"{identity}: no compile/runtime assembly for {framework}")
    content = path.read_bytes()
    result = {"id": identity, "version": declared, "dependencyGroups": groups, "file": path.name,
            "sha256": hashlib.sha256(content).hexdigest(),
            "sha512": base64.b64encode(hashlib.sha512(content).digest()).decode("ascii")}
    symbols = path.with_suffix(".snupkg")
    if symbols.is_file():
        result["symbolsFile"] = symbols.name
        result["symbolsSha256"] = hashlib.sha256(symbols.read_bytes()).hexdigest()
    return result


def suite_command(name):
    if name == "Broiler.CSS":
        return ["pwsh", "-NoProfile", "-File", "eng/run-tests.ps1", "-Configuration", "Release"]
    bash = "bash"
    if os.name == "nt":
        git_exe = Path(shutil.which("git"))
        bundled = git_exe.parent.parent / "bin/bash.exe"
        if bundled.is_file():
            bash = str(bundled)
    return [bash, "eng/run-tests.sh", "Release"]


def build_and_test(name, repo, sln, candidate, env, logs, prefix):
    command(["node", "--test", "eng/resolve-preview-version.test.mjs"], repo,
            f"{name} version tests", logs / f"{prefix}-version.log", env)
    command(["dotnet", "build", sln, "-c", "Release", "--nologo",
             f"-p:Version={candidate}", f"-p:PackageVersion={candidate}"], repo,
            f"{name} build", logs / f"{prefix}-build.log", env)
    text = command(suite_command(name), repo, f"{name} unit tests", logs / f"{prefix}-tests.log", env)
    tests = sum(int(count.replace(",", "")) for count in TEST_COUNTS.findall(text))
    if not tests:
        raise ValueError(f"{name}: suite reported no executed tests")
    return tests


def produce(name, repo, number, work, feed, upstream, logs, prefix, verify_build=None):
    sln = PILOTS[name]
    sources, candidate = package_sources(sln, repo, number)
    contracts = public_contract(sources, candidate)
    config, cache = work / f"{prefix}.config", work / f"{prefix}-cache"
    write_config(config, feed, upstream)
    env = restore_env(config, cache)
    tests = build_and_test(name, repo, sln, candidate, env, logs, prefix)
    build_evidence = verify_build(cache) if verify_build else []
    output = work / f"{prefix}-packed"
    command(["pwsh", "-NoProfile", "-File", "eng/pack.ps1", "-Version", candidate, "-Output", str(output)],
            repo, f"{name} pack", logs / f"{prefix}-pack.log", env)
    archives = sorted(output.glob("*.nupkg"))
    observed = [inspect_package(path, contracts, candidate) for path in archives]
    if sorted(item["id"].casefold() for item in observed) != sorted(contracts):
        raise ValueError(f"{name}: staged identities differ from expected pack targets")
    for path in sorted(output.iterdir()):
        if path.suffix in {".nupkg", ".snupkg"}:
            shutil.copyfile(path, feed / path.name)
    return {"tests": tests, "version": candidate, "packages": observed, "expectedGroups": contracts,
            "buildRestore": build_evidence, "evaluatedProjects": [
                {"project": (ROOT / row["project"]).relative_to(repo).as_posix(),
                 "profiles": row["profiles"]} for row in sources.values()]}


def local_source(source):
    if source.startswith("file:"):
        return Path(url2pathname(unquote(urlsplit(source).path))).resolve()
    return Path(source).resolve()


def restore_evidence(assets_path, cache, feed, candidates, expected, check_assets=False):
    assets = json.loads(assets_path.read_text(encoding="utf-8-sig"))
    restore = assets["project"]["restore"]
    if check_assets and any(profile.get("projectReferences") for profile in restore["frameworks"].values()):
        raise ValueError("Package-only consumer has project references")
    seen, rows = set(), []
    for framework, libraries in assets["targets"].items():
        for identity, item in sorted(libraries.items()):
            key, resolved = identity.casefold().rsplit("/", 1)
            if key not in expected:
                if check_assets and key.startswith("broiler.") and item["type"] == "package":
                    raise ValueError(f"Unexpected Broiler package in {framework}: {identity}")
                continue
            wanted = candidates[key]
            if item["type"] != "package" or version(resolved) != version(wanted["version"]):
                raise ValueError(f"{framework}: {key} did not resolve to candidate {wanted['version']}")
            if check_assets and not all(any(asset.endswith(".dll") for asset in item.get(kind, {}))
                                        for kind in ("compile", "runtime")):
                raise ValueError(f"{framework}: {identity} has missing compile or runtime assets")
            known = assets["libraries"][identity]["sha512"]
            data = json.loads((cache / key / resolved / ".nupkg.metadata").read_text(encoding="utf-8"))
            source = data.get("source", "")
            if not source or source.startswith(("http:", "https:")) or local_source(source) != feed.resolve():
                raise ValueError(f"{framework}: {key} resolved from an unexpected source: {source}")
            if known != wanted["sha512"] or data["contentHash"] != wanted["sha512"]:
                raise ValueError(f"{framework}: {key} content hash differs from the staged package")
            seen.add(key)
            rows.append({"framework": framework, "package": wanted["id"], "version": wanted["version"],
                         "source": "candidate-feed", "sha512": known,
                         "compileAssets": sorted(item.get("compile", {})),
                         "runtimeAssets": sorted(item.get("runtime", {}))})
    if seen != expected:
        raise ValueError(f"Missing staged dependencies: {sorted(expected - seen)}")
    return rows


def closure(package, candidates):
    seen, todo = set(), [package]
    while todo:
        key = todo.pop()
        if key in seen:
            continue
        seen.add(key)
        todo.extend(dep for group in candidates[key]["dependencyGroups"].values()
                    for dep in group if dep in candidates)
    return seen


def probe(package, candidates, work, feed, logs, label, test_feed=None, alternate=None, restore_only=False):
    base = work / label
    base.mkdir()
    config, cache = base / "NuGet.config", base / "packages"
    write_config(config, test_feed or feed, candidates, alternate)
    project = base / "Consumer.csproj"
    project.write_text(f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImportDirectoryBuildProps>false</ImportDirectoryBuildProps>
    <ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="{candidates[package]['id']}" Version="[{candidates[package]['version']}]" />
  </ItemGroup>
</Project>
""", encoding="utf-8")
    (base / "Program.cs").write_text(
        f'if (!({SMOKES[package]})) throw new System.InvalidOperationException("{package} smoke check failed");\n',
        encoding="utf-8")
    env = restore_env(config, cache)
    command(["dotnet", "restore", str(project), "--configfile", str(config), "--no-http-cache", "--nologo"],
            base, f"{label} restore", logs / f"{label}-restore.log", env)
    rows = restore_evidence(base / "obj/project.assets.json", cache, feed, candidates,
                            closure(package, candidates), check_assets=True)
    if not restore_only:
        command(["dotnet", "run", "--no-restore", "--configuration", "Release", "--project", str(project)],
                base, f"{label} smoke", logs / f"{label}-smoke.log", env)
    return rows


def rejection(test, condition, action):
    try:
        action()
    except ValueError as error:
        if condition(error):
            return {"check": test, "rejected": True, "reason": str(error).splitlines()[0]}
        raise
    raise ValueError(f"Negative control was accepted: {test}")


def negative_controls(candidates, producers, work, feed, logs, number):
    css = candidates["broiler.css.dom"]
    broken = work / "wrong-range.nupkg"
    with ZipFile(feed / css["file"]) as original, ZipFile(broken, "w") as rewritten:
        for name in original.namelist():
            content = original.read(name)
            if name.endswith(".nuspec"):
                manifest = ET.fromstring(content)
                edges = [item for item in manifest.iter() if item.tag.split("}")[-1] == "dependency"
                         and item.get("id", "").casefold() == "broiler.dom"]
                edge, = edges
                edge.set("version", "[0.0.0]")
                content = ET.tostring(manifest)
            rewritten.writestr(name, content)
    checks = [rejection("wrong packed dependency range", lambda error: "dependency range mismatch" in str(error),
                        lambda: inspect_package(broken, producers["Broiler.CSS"]["expectedGroups"], css["version"]))]
    missing = work / "missing-feed"
    missing.mkdir()
    for key, item in candidates.items():
        if key != "broiler.dom":
            shutil.copyfile(feed / item["file"], missing / item["file"])
    checks.append(rejection("missing staged DOM", lambda error: isinstance(error, CommandFailed)
                            and "Broiler.Dom" in error.output and re.search(r"\bNU110[12]\b", error.output),
                            lambda: probe("broiler.css.dom", candidates, work, feed, logs, "missing-dom", missing)))
    mirror = work / "unexpected-feed"
    mirror.mkdir()
    dom = candidates["broiler.dom"]
    shutil.copyfile(feed / dom["file"], mirror / dom["file"])
    checks.append(rejection("wrong source for identical DOM archive", lambda error: "unexpected source" in str(error),
                            lambda: probe("broiler.css.dom", candidates, work, feed, logs, "wrong-source",
                                          alternate=(mirror, ["broiler.dom"]), restore_only=True)))
    failing = work / "failing-datetime"
    checkout("Broiler.DateTime", failing)
    test_project = failing / "tests/Broiler.DateTime.Tests/PhaseThreeFailure.cs"
    test_project.write_text("""public sealed class PhaseThreeFailure
{
    [Xunit.Fact]
    public void MustBlockStaging() => throw new System.InvalidOperationException("PhaseThreeFailure");
}
""", encoding="utf-8")
    failure_feed = work / "failure-feed"
    failure_feed.mkdir()
    checks.append(rejection("failing producer suite", lambda error: isinstance(error, CommandFailed)
                            and error.label == "Broiler.DateTime unit tests" and "PhaseThreeFailure" in error.output,
                            lambda: produce("Broiler.DateTime", failing, number, work, failure_feed, {}, logs, "failure")))
    if list(failure_feed.iterdir()) or (work / "failure-packed").exists():
        raise ValueError("Failing suite reached packaging or staging")
    return checks


def run_pilots(work, output, patch_plan, number, datetime_release_ordinal=None):
    feed, logs = output / "candidate-feed", output / "logs"
    feed.mkdir()
    logs.mkdir()
    producers, candidates, commits = {}, {}, {}
    sdk = command(["dotnet", "--version"], ROOT, "SDK", logs / "sdk.log")
    edits = {}
    for name in PILOTS:
        print(f"Running {name} Build, tests and pack", flush=True)
        repo = work / name
        commit = commits[name] = checkout(name, repo)
        if name == "Broiler.DateTime" and patch_plan:
            edits[name] = apply_datetime_patch(repo, commit, patch_plan)
        upstream, verify_build = {}, None
        if name == "Broiler.CSS":
            dom = candidates["broiler.dom"]
            old = with_candidate_dom(repo, dom["version"])
            edits[name] = {"package": dom["id"], "before": old, "after": dom["version"]}
            upstream = {key: item for key, item in candidates.items() if key.startswith("broiler.dom")}

            def verify_build(cache):
                evidence = []
                for project in ("Broiler.CSS.Dom", "Broiler.CSS.Dom.Tests"):
                    rows = restore_evidence(repo / project / "obj/project.assets.json", cache, feed, upstream, {"broiler.dom"})
                    evidence.extend({"project": project, **row} for row in rows)
                return evidence

        ordinal = datetime_release_ordinal if name == "Broiler.DateTime" and datetime_release_ordinal else number
        result = produce(name, repo, ordinal, work, feed, upstream, logs, name, verify_build)
        producers[name] = {"commit": commit, **result}
        candidates.update({item["id"].casefold(): item for item in result["packages"]})
        print(f"{name}: {result['tests']} tests, {len(result['packages'])} packages", flush=True)

    if candidates.keys() != SMOKES.keys():
        raise ValueError(f"Pilot producer coverage changed: {sorted(candidates)}")
    probes = []
    for package in sorted(candidates):
        print(f"Probing {package} from the staged feed", flush=True)
        probes.append({"consumer": candidates[package]["id"], "status": "passed",
                       "resolved": probe(package, candidates, work, feed, logs, f"probe-{package}")})

    print("Running negative controls", flush=True)
    controls = negative_controls(candidates, producers, work, feed, logs, number)
    return {"status": "passed", "profile": {"os": platform.system(), "sdk": sdk, "configuration": "Release"},
            "edits": edits, "producers": producers, "packageConsumers": probes, "negativeControls": controls,
            "dateTimeReleaseIntent": datetime_release_ordinal is not None}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/nuget-pilots" / str(time.time_ns()))
    parser.add_argument("--patch-plan", type=Path, help="Use the validated Phase 2 DateTime test SDK patch")
    parser.add_argument("--candidate-number", type=int, default=time.time_ns(),
                        help="Use the same preview ordinal on each runner in a hosted dry run")
    parser.add_argument("--datetime-release-ordinal", type=int,
                        help="Stage this exact DateTime preview from the selected source commit for approval")
    args = parser.parse_args()
    if args.candidate_number < 1:
        parser.error("Candidate number must be positive")
    if args.datetime_release_ordinal is not None and (args.datetime_release_ordinal < 1 or args.patch_plan):
        parser.error("A release ordinal must be positive and staged from a committed DateTime tree")
    output = args.output.resolve()
    if not output.is_relative_to(ROOT / "artifacts"):
        parser.error("Output must be under the aggregate artifacts directory")
    output.mkdir(parents=True, exist_ok=True)
    if any(output.iterdir()):
        parser.error("Output directory must be empty")
    try:
        with TemporaryDirectory(prefix="nuget-probe-", dir=output.parent, ignore_cleanup_errors=True) as scratch:
            matrix = run_pilots(Path(scratch).resolve(), output,
                                args.patch_plan.resolve() if args.patch_plan else None, args.candidate_number,
                                args.datetime_release_ordinal)
        summary = ["## Broiler NuGet dry run passed", ""]
        summary.extend(f"- {name}: {result['tests']} tests and {len(result['packages'])} staged packages"
                       for name, result in matrix["producers"].items())
        summary += [f"- {len(matrix['packageConsumers'])} package-only consumers passed",
                    f"- {len(matrix['negativeControls'])} negative controls rejected"]
    except (OSError, ValueError, KeyError, ET.ParseError, BadZipFile, subprocess.SubprocessError) as error:
        matrix = {"status": "failed", "reason": str(error)}
        summary = ["## Broiler NuGet dry run failed", "", str(error)]
    (output / "matrix.json").write_text(json.dumps(matrix, indent=2) + "\n", encoding="utf-8")
    (output / "summary.md").write_text("\n".join(summary) + "\n", encoding="utf-8")
    print("\n".join(summary), flush=True)
    return int(matrix["status"] != "passed")


if __name__ == "__main__":
    raise SystemExit(main())
