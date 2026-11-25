"""Plan and validate cohort admission against owner CI and exact package artifacts."""
import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
from tempfile import TemporaryDirectory
import xml.etree.ElementTree as ET
from zipfile import BadZipFile, ZipFile

from nuget_patches import FEED
from nuget_pilot_matrix import local_source, range_key, restore_env, write_config
from nuget_publish_order import ROOT, OUTPUT as INVENTORY, REGISTRY

OUTPUT = ROOT / "artifacts/nuget-cohorts"
HOST = "Broiler-Platform"
PREVIEW = re.compile(r"\d+\.\d+\.\d+-preview\.[1-9]\d*")
TFM = re.compile(r"[A-Za-z0-9][A-Za-z0-9.-]*")
WASM_ASSETS = {"broiler.graphics.webassembly", "broiler.hosting.webassembly"}
LINUX_ONLY = {"Broiler.UI", "Broiler.UI.Extended", "Broiler.UI.Shell"}
WINDOWS_PACK = {"Broiler.DateTime", "Broiler.DOM", "Broiler.Graphics", "Broiler.Hosting",
                "Broiler.HtmlBridge", "Broiler.Input", "Broiler.JS", "Broiler.JSeal", "Broiler.Media",
                "Broiler.Native", "Broiler.RegEx", "Broiler.Unicode", "Broiler.VM"}
ERRORS = (OSError, ValueError, KeyError, ET.ParseError, BadZipFile, subprocess.SubprocessError)


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def invoke(args, cwd=ROOT, env=None):
    result = subprocess.run(args, cwd=cwd, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                            text=True, encoding="utf-8", errors="replace", timeout=180)
    if result.returncode or (args[0] == "dotnet" and re.search(r"\bNU(?:1107|1605|1608)\b", result.stdout)):
        raise ValueError(f"{' '.join(args[:2])} failed\n{result.stdout[-2200:]}")
    return result.stdout


def framework_key(value):
    # The SDK writes the default Windows platform version into the package TFM.
    return re.sub(r"-windows7\.0$", "-windows", value.casefold())


def contract_range(value):
    try:
        return range_key(value)
    except ValueError:
        return re.sub(r"\s+", "", value).casefold()


def matrix(inventory, evaluated, registry):
    if (inventory["errors"] or not inventory["checkedFeed"] or inventory["workingTree"] or
            inventory["revisions"] != evaluated["revisions"]):
        raise ValueError("A successful, feed-checked Phase 1 inventory with matching source revisions is required")
    rows = {row["project"]: row for row in evaluated["projects"]}
    owners = {row["id"].casefold(): row for row in inventory["packages"] if "publisher" in row}
    if any(row["classification"] in {"offered", "pending"} and "publisher" not in row
           for row in inventory["packages"]):
        raise ValueError("Offered package without a designated publisher")
    all_projects = {path.casefold(): identity.casefold() for identity, path in inventory["packageProjects"].items()}
    cohort_waves = inventory["cohortLayers"]
    waves = {cohort: number for number, wave in enumerate(cohort_waves, 1) for cohort in wave}
    result = {}
    for identity, owner in sorted(owners.items()):
        publication = owner["publisher"]
        cohort, repo = publication["cohort"], publication["repository"]
        if repo == ".":
            continue
        definition = registry["publishers"][repo][Path(publication["workflow"]).name]
        commit = inventory["revisions"][repo]["commit"]
        if not commit or not re.fullmatch(r"[0-9a-f]{40}", commit):
            raise ValueError(f"{repo}: missing exact source commit")
        shared = cohort.endswith("/publish-shared.yml")
        listing = result.setdefault(cohort, {
            "repository": repo, "commit": commit, "wave": waves[cohort],
            "ci": Path(publication["test"]).name, "publication": Path(publication["workflow"]).name,
            "promotion": definition.get("promotion"), "artifact": "shared-packages" if shared else "nuget-packages",
            "prerequisites": inventory["cohortDependencies"][cohort], "packages": {},
            "legacyOverlap": [], "checks": owner_checks(repo, shared)})
        path = inventory["packageProjects"][owner["id"]]
        profiles = []
        for profile in rows[path]["profiles"]:
            if not profile["packable"] or profile["package"].casefold() != identity:
                continue
            direct = {item["id"].casefold(): item["version"] for item in profile["packages"]
                      if item["privateAssets"].casefold() != "all"}
            references = {}
            for item in profile["references"]:
                if item["privateAssets"].casefold() == "all" or item["referenceOutputAssembly"].casefold() == "false":
                    continue
                dependency = all_projects.get(item["project"].casefold())
                if dependency:
                    references[dependency] = owners[dependency]["publisher"]["cohort"]
            tfm, rid = profile["framework"], profile["runtime"]
            roles = set(identity.split(".")) & {"android", "windows", "linux"}
            if "-android" in tfm or rid.startswith("android"):
                roles.add("android")
            if "-windows" in tfm or rid.startswith("win"):
                roles.add("windows")
            if rid.startswith("linux"):
                roles.add("linux")
            if identity in WASM_ASSETS or rid.startswith("browser"):
                roles.add("webassembly")
            profiles.append({"configuration": profile["configuration"], "framework": tfm, "runtime": rid,
                             "roles": sorted(roles), "includesBuildOutput": profile.get("includesBuildOutput", True),
                             "includesSymbols": profile.get("includesSymbols", False),
                             "packageReferences": direct, "projectReferences": references})
        if not profiles or any(not profile["framework"] for profile in profiles):
            raise ValueError(f"{owner['id']}: no usable package profile")
        listing["packages"][identity] = {"id": owner["id"], "project": path, "profiles": profiles,
                                          "offered": owner["classification"] == "offered",
                                          "publicVersions": owner.get("publicVersions", [])}
        if owner["id"] in inventory["legacyRootOverlaps"]:
            listing["legacyOverlap"].append(owner["id"])
    return result


def owner_checks(repo, shared):
    if shared:
        return {f"Build and SL rules ({os_name})": ["Build", "Test, the SL rules among them"]
                for os_name in ("ubuntu-24.04", "windows-latest")} | {
            "Pack, validate and restore the shared stream (dry run)": [
                "Pack the shared stream", "Restore the set in an isolated cache, nuget.org for everything else",
                "Restore and run a pristine consumer from a local feed alone", "Upload packages"]}
    systems = ("ubuntu-24.04",) if repo in LINUX_ONLY else ("ubuntu-24.04", "windows-latest")
    checks = {}
    for system in systems:
        steps = ["Build", "Test"]
        if repo == "Broiler.HTML":
            steps = ["Build", "Test (.NET)", "Test"]
        elif repo == "Broiler.JSeal":
            steps = ["Build (Release)", "Test (Release)", "Build (Release-VM)", "Test (Release-VM)",
                     "Test isolated package consumers"]
        elif repo == "Broiler.Documents.Codecs":
            steps.append("Run the corpus suite over the command line")
        if repo in {"Broiler.Graphics", "Broiler.Hosting"}:
            steps += ["Verify browser package asset registration"]
        if repo == "Broiler.Hosting":
            steps += ["Install Android workload"]
        # Assembly/native outputs or workload-dependent pack jobs run on Windows for these cohorts.
        if (system == "windows-latest") == (repo in WINDOWS_PACK):
            steps += ["Pack and verify the package" if repo == "Broiler.Net" else "Pack and verify every package",
                      "Verify consumer restore from nuget.org (dry run)", "Upload packages"]
        checks["Release (Linux)" if repo in LINUX_ONLY else f"{system} / Release"] = steps
    if repo == "Broiler.HtmlBridge":
        checks["Engine neutrality"] = ["Test the engine coupling guard"]
        checks["ubuntu-24.04 / Release-VM"] = ["Build", "Verify the build type means what it says", "Test"]
    return checks


def manifest(archive):
    specs = [name for name in archive.namelist() if name.endswith(".nuspec")]
    if len(specs) != 1:
        raise ValueError("Expected one nuspec per archive")
    data = ET.fromstring(archive.read(specs[0])).find("{*}metadata")
    if data is None:
        raise ValueError("Missing package metadata")
    return data


def archive_groups(path):
    with ZipFile(path) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)):
            raise ValueError(f"{path.name}: duplicate archive entries")
        data = manifest(archive)
        groups = {}
        for group in data.findall("{*}dependencies/{*}group"):
            tfm = framework_key(group.attrib["targetFramework"])
            entries = group.findall("{*}dependency")
            deps = {item.attrib["id"].casefold(): item.attrib["version"] for item in entries}
            if tfm in groups or len(entries) != len(deps):
                raise ValueError(f"{path.name}: duplicate dependency groups or IDs")
            groups[tfm] = deps
        if data.findall("{*}dependencies/{*}dependency"):
            raise ValueError(f"{path.name}: ungrouped dependency cannot be assigned to a profile")
        return data, groups, names


def verify_archives(packages, entry, cohort):
    result = {}
    for path in sorted(packages.glob("*.nupkg")):
        data, groups, names = archive_groups(path)
        identity = data.findtext("{*}id", "").casefold()
        version = data.findtext("{*}version", "")
        source = data.find("{*}repository")
        if (not PREVIEW.fullmatch(version) or identity not in entry["packages"] or identity in result or
                source is None or source.get("commit") != entry["commit"]):
            raise ValueError(f"{path.name}: undeclared, duplicate, or wrong source identity/version")
        declared = entry["packages"][identity]
        expected = {}
        for profile in declared["profiles"]:
            if profile["configuration"] != "Release" or profile["runtime"]:
                continue
            if any(owner != cohort for owner in profile["projectReferences"].values()):
                raise ValueError(f"{identity}: cross-cohort project reference needs a staged publisher contract")
            tfm = framework_key(profile["framework"])
            deps = profile["packageReferences"] | dict.fromkeys(profile["projectReferences"], version)
            expected[tfm] = deps
            if profile["includesBuildOutput"] and not any(
                    name.startswith("lib/") and name.endswith(".dll") and
                    framework_key(name.split("/")[1]) == tfm for name in names):
                raise ValueError(f"{identity}: missing library for {tfm}")
            if profile["includesSymbols"] and not path.with_suffix(".snupkg").is_file():
                raise ValueError(f"{identity}: missing symbol archive")
        if identity in WASM_ASSETS and not all(any(name.startswith(prefix) for name in names)
                                               for prefix in ("browser/", "buildTransitive/")):
            raise ValueError(f"{identity}: missing WebAssembly registration assets")
        if not expected:
            raise ValueError(f"{identity}: no Release packaging profile")
        normalized = lambda mapping: {tfm: {key: contract_range(value) for key, value in deps.items()}
                                      for tfm, deps in mapping.items() if deps}
        if not groups.keys() <= expected.keys() or normalized(groups) != normalized(expected):
            raise ValueError(f"{identity}: dependency groups differ from evaluated profiles. "
                             f"Expected {expected}; observed {groups}.")
        result[identity] = {"id": declared["id"], "version": version, "file": path.name,
                            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "dependencyGroups": groups}
    if result.keys() != entry["packages"].keys():
        raise ValueError(f"Cohort archive set differs. Missing {sorted(entry['packages'].keys() - result.keys())}.")
    if len({item["version"] for item in result.values()}) != 1:
        raise ValueError("Cohort packages do not share one preview version")
    return result


def verify_run(repo, workflow, run, commit=None, pr=False):
    event = run.get("event")
    path = run.get("path", "").split("@", 1)[0]
    expected_repo = f"{HOST}/{repo}"
    repository = (run.get("repository") or {}).get("full_name")
    head = (run.get("head_repository") or {}).get("full_name")
    if (path != f".github/workflows/{workflow}" or
            (repository and repository != expected_repo) or
            (pr and not repository and head != expected_repo) or (not pr and head != expected_repo) or
            run.get("conclusion") != "success" or (commit is not None and run.get("head_sha") != commit) or
            (pr and event != "pull_request") or
            (not pr and (event not in {"workflow_dispatch", "push"} or run.get("head_branch") != "main"))):
        raise ValueError(f"Run must be a successful {workflow} at the requested source revision")


def require_jobs(jobs, checks):
    by_name = {job["name"]: job for job in jobs}
    for name, requirements in checks.items():
        job = by_name.get(name)
        if not job or job["conclusion"] != "success":
            raise ValueError(f"Required job missing or failed: {name}")
        passed = {step["name"] for step in job["steps"] if step["conclusion"] == "success"}
        if missing := set(requirements) - passed:
            raise ValueError(f"{name}: required steps missing or skipped: {sorted(missing)}")


def api(repo, endpoint, paginated=False):
    result = invoke(["gh", "api", f"repos/{HOST}/{repo}/{endpoint}",
                     *(["--paginate", "--slurp"] if paginated else [])])
    return json.loads(result)


def download(repo, run, name, directory):
    invoke(["gh", "run", "download", str(run), "--repo", f"{HOST}/{repo}",
            "--name", name, "--dir", str(directory)])


def owner_artifacts(entry, source, candidate_dir):
    repo = entry["repository"]
    run = api(repo, f"actions/runs/{source}")
    verify_run(repo, entry["ci"], run, entry["commit"])
    jobs = api(repo, f"actions/runs/{source}/jobs?filter=latest&per_page=100", paginated=True)
    require_jobs([job for page in jobs for job in page["jobs"]], entry["checks"])
    download(repo, source, entry["artifact"], candidate_dir)


def verify_pr(entry, source):
    repo = entry["repository"]
    run = api(repo, f"actions/runs/{source}")
    verify_run(repo, entry["ci"], run, pr=True)
    if run["head_sha"] != entry["commit"]:
        matched = False
        for linked in run.get("pull_requests", []):
            if linked["head"]["sha"] != run["head_sha"]:
                continue
            pr = api(repo, f"pulls/{int(linked['number'])}")
            if pr["merged"] and pr["merge_commit_sha"] == entry["commit"] and pr["head"]["sha"] == run["head_sha"]:
                matched = True
                break
        if not matched:
            raise ValueError("PR validation did not lead to the selected producer commit")
    jobs = api(repo, f"actions/runs/{source}/jobs?filter=latest&per_page=100", paginated=True)
    tests = {step for requirements in entry["checks"].values() for step in requirements
             if re.match(r"^Test(?:$| \(|,)", step)}
    if not any(job["conclusion"] == "success" and any(step["name"] in tests and step["conclusion"] == "success"
               for step in job["steps"]) for page in jobs for job in page["jobs"]):
        raise ValueError("PR workflow did not execute a required owner test suite")


def restore_candidates(work, feed, cohort, candidates):
    conf, cache = work / "NuGet.config", work / "cache"
    write_config(conf, feed, candidates)
    env = restore_env(conf, cache)
    env.pop("GH_TOKEN", None)
    env.pop("GITHUB_TOKEN", None)
    records = []
    for identity, package in candidates.items():
        slices = {(p["framework"], p["runtime"]) for p in cohort["packages"][identity]["profiles"]
                  if p["configuration"] == "Release"}
        for tfm, rid in sorted(slices):
            if not TFM.fullmatch(tfm) or (rid and not TFM.fullmatch(rid)):
                raise ValueError(f"Unsupported probe target {tfm}/{rid}")
            probe = work / f"probe-{len(records)}"
            probe.mkdir()
            project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
            properties = ET.SubElement(project, "PropertyGroup")
            for key, value in {"TargetFramework": tfm, "RuntimeIdentifier": rid, "EnableWindowsTargeting": "true",
                               "ImportDirectoryBuildProps": "false", "ImportDirectoryBuildTargets": "false",
                               "ManagePackageVersionsCentrally": "false"}.items():
                if value:
                    ET.SubElement(properties, key).text = value
            ET.SubElement(ET.SubElement(project, "ItemGroup"), "PackageReference",
                          Include=package["id"], Version=f"[{package['version']}]")
            path = probe / "Consumer.csproj"
            ET.ElementTree(project).write(path, encoding="utf-8")
            invoke(["dotnet", "restore", str(path), "--configfile", str(conf), "--packages", str(cache),
                    "--no-http-cache", "--nologo"], probe, env)
            assets = read(probe / "obj/project.assets.json")
            target = next((name for name in assets["targets"]
                           if framework_key(name.split("/")[0]) == framework_key(tfm) and
                           (name.split("/", 1)[1] if "/" in name else "") == rid), None)
            if target is None:
                raise ValueError(f"{identity}: no restore graph for {tfm}/{rid}")
            graph = {}
            for key, data in assets["targets"][target].items():
                dependency, resolved = key.lower().rsplit("/", 1)
                location = cache / dependency / resolved
                if data["type"] != "package":
                    raise ValueError(f"{key}: package-only probe resolved a project")
                info = read(location / ".nupkg.metadata")
                if dependency in candidates:
                    staged = candidates[dependency]
                    source = info.get("source", "")
                    if (resolved != staged["version"] or not source or source.startswith("https:") or
                            local_source(source) != feed.resolve() or hashlib.sha256(
                                (location / f"{dependency}.{resolved}.nupkg").read_bytes()).hexdigest() != staged["sha256"]):
                        raise ValueError(f"{key}: unexpected staged package version, source or bytes")
                    origin = "candidate-feed"
                elif info.get("source") == FEED:
                    origin = FEED
                else:
                    raise ValueError(f"{key}: outside the approved public feed")
                graph[dependency] = {"version": resolved, "source": origin, "contentHash": info["contentHash"],
                                     "compileAssets": sorted(data.get("compile", {})),
                                     "runtimeAssets": sorted(data.get("runtime", {}))}
            if identity not in graph:
                raise ValueError(f"{identity}: candidate did not resolve")
            needs_library = any(p["framework"] == tfm and p["runtime"] == rid and p["includesBuildOutput"]
                                for p in cohort["packages"][identity]["profiles"])
            if needs_library and not all(any(name.endswith(".dll") for name in graph[identity][kind])
                                         for kind in ("compileAssets", "runtimeAssets")):
                raise ValueError(f"{identity}: missing consumer compile or runtime assembly for {tfm}/{rid}")
            records.append({"consumer": package["id"], "framework": tfm, "runtime": rid, "resolved": graph})
    return records


def verified_promotion(entry, source, candidates, work):
    repo = entry["repository"]
    run = api(repo, f"actions/runs/{source}")
    verify_run(repo, entry["promotion"], run)
    if run.get("event") != "workflow_dispatch":
        raise ValueError("Protected promotion must be an explicit producer dispatch")
    artifact = work / "promotion"
    download(repo, source, "pilot-promotion", artifact)
    plan = read(artifact / "promotion.json")
    if plan["producerCommit"] != entry["commit"]:
        raise ValueError("Approved promotion was for a different producer commit")
    state_dir = work / "state"
    download(repo, source, f"pilot-release-state-{run['run_attempt']}", state_dir)
    state = read(state_dir / "release-state.json")
    if state["manifestSha256"] != hashlib.sha256((artifact / "promotion.json").read_bytes()).hexdigest():
        raise ValueError("Release state does not match the prepared manifest")
    released = {package["id"].casefold(): package for package in plan["packages"]}
    if released.keys() != candidates.keys() or state["packages"].keys() != candidates.keys():
        raise ValueError("Publication receipt covers a different package set")
    for key, package in candidates.items():
        receipt = state["packages"][key]
        if (released[key]["version"] != package["version"] or released[key]["sha256"] != package["sha256"] or
                receipt["version"] != package["version"] or receipt["state"] != "verified" or
                receipt["evidence"]["source"] != FEED or
                package["version"] not in entry["packages"][key]["publicVersions"]):
            raise ValueError(f"{key}: no exact verified public promotion for the checked archive")
    return source


def admission(entries, patch_plan, sources, output, wave=None, local=None):
    groups = patch_plan.get("groups", [])
    local = local or {}
    pilot = "Broiler.DateTime/publish.yml"
    results = {}
    for cohort, entry in sorted(entries.items(), key=lambda item: (item[1]["wave"], item[0] != pilot, item[0])):
        checks = results[cohort] = {"wave": entry["wave"], "audit": False, "pr": False, "dryRun": False,
                                  "publication": False, "admitted": False, "blockers": []}
        dependencies = [key for key in entry["prerequisites"] if not results.get(key, {}).get("admitted")]
        if dependencies:
            checks["blockers"].append(f"Unadmitted prerequisite cohorts: {', '.join(dependencies)}")
        if cohort != pilot and not results.get(pilot, {}).get("admitted"):
            checks["blockers"].append("DateTime pilot has no verified admission")
        own = [g for g in groups if g["repository"] == entry["repository"] and g["commit"] == entry["commit"]]
        audited_revision = patch_plan.get("revisions", {}).get(entry["repository"], {}).get("commit")
        checks["audit"] = bool(not patch_plan.get("errors") and (own or audited_revision == entry["commit"]))
        if not checks["audit"]:
            checks["blockers"].append("Missing passing dependency audit for the exact commit")
        elif any(pin["status"] == "feed-error" for group in own for pin in group["pins"]):
            checks["audit"] = False
            checks["blockers"].append("Dependency source lookup failed")
        if not entry["promotion"]:
            checks["blockers"].append("Protected artifact promotion is not configured for this owner")
        source = sources.get(cohort, {})
        if (wave is not None and entry["wave"] > wave) or (not source and cohort not in local):
            checks["blockers"].append("No selected owner CI and archive evidence for this wave")
            continue
        print(f"Checking wave {entry['wave']} {cohort}", flush=True)
        with TemporaryDirectory(prefix="cohort-", dir=output, ignore_cleanup_errors=True) as directory:
            work = Path(directory).resolve()
            try:
                if cohort in local:
                    feed = local[cohort]
                else:
                    feed = work / "packages"
                    # DateTime's Phase 4 workflow carries the exact staged archives; ordinary CI may
                    # build an unrelated default preview version.
                    if source.get("promotion") and entry["promotion"]:
                        run = api(entry["repository"], f"actions/runs/{source['promotion']}")
                        verify_run(entry["repository"], entry["promotion"], run)
                        download(entry["repository"], source["promotion"], "pilot-promotion", feed)
                    else:
                        owner_artifacts(entry, source["ci"], feed)
                candidates = verify_archives(feed, entry, cohort)
                checks["archives"] = candidates
                checks["restores"] = restore_candidates(work, feed, entry, candidates)
                # Local rehearsal is intentionally unable to satisfy trusted owner or public admission.
                checks["dryRun"] = True
                if cohort in local:
                    checks["blockers"].append("Local rehearsal lacks hosted owner CI provenance")
                    continue
                if source.get("pr"):
                    verify_pr(entry, source["pr"])
                    checks["pr"] = True
                else:
                    checks["blockers"].append("Missing passing owner PR workflow for the exact source commit")
                if entry["promotion"] and source.get("promotion"):
                    checks["publication"] = bool(verified_promotion(entry, source["promotion"], candidates, work))
                elif entry["promotion"]:
                    checks["blockers"].append("Missing exact public promotion receipt")
                checks["admitted"] = not checks["blockers"] and all(
                    checks[key] for key in ("audit", "pr", "dryRun", "publication"))
            except ERRORS as error:
                checks["blockers"].append(str(error))
    return results


def route_packages(folder, receipt):
    redirected, owned = [], []
    routes, ready = {}, set()
    for cohort, entry in sorted(receipt["cohorts"].items(), key=lambda item: item[1]["wave"]):
        checks = receipt["admission"][cohort]
        if (checks["blockers"] or not all(checks[name] is True
                for name in ("audit", "pr", "dryRun", "publication", "admitted")) or
                not set(entry["prerequisites"]) <= ready):
            continue
        ready.add(cohort)
        for identity in entry["legacyOverlap"]:
            if identity.casefold() in routes:
                raise ValueError(f"Ambiguous legacy route for {identity}")
            routes[identity.casefold()] = cohort
    if "Broiler.DateTime/publish.yml" not in ready:
        routes.clear()
    seen = set()
    for path in sorted(folder.glob("*.nupkg")):
        with ZipFile(path) as archive:
            identity = manifest(archive).findtext("{*}id", "")
        if not identity or identity.casefold() in seen:
            raise ValueError(f"{path.name}: missing or duplicate package ID")
        seen.add(identity.casefold())
        cohort = routes.get(identity.casefold())
        if cohort:
            redirected.append({"id": identity, "publisher": cohort})
        else:
            owned.append(path.name)
    return {"redirected": redirected, "retain": owned}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_subparsers(dest="mode", required=True)
    plan = modes.add_parser("plan")
    plan.add_argument("--inventory", type=Path, default=INVENTORY)
    plan.add_argument("--patch-plan", type=Path)
    plan.add_argument("--owner-runs", default="{}", help="JSON map from cohort keys to numeric hosted run IDs")
    plan.add_argument("--through-wave", type=int)
    plan.add_argument("--require-admission", action="store_true")
    plan.add_argument("--local-cohort", action="append", default=[])
    plan.add_argument("--archives", type=Path, action="append", default=[])
    plan.add_argument("--output", type=Path, default=OUTPUT)
    route = modes.add_parser("route")
    route.add_argument("--archives", type=Path, required=True)
    route.add_argument("--receipt", type=Path, required=True)
    route.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.mode == "route":
        args.output.write_text(json.dumps(route_packages(args.archives, read(args.receipt)), indent=2) + "\n",
                               encoding="utf-8")
        return
    entries = matrix(read(args.inventory / "publish-order.json"), read(args.inventory / "evaluated-projects.json"),
                     read(REGISTRY))
    sources = json.loads(args.owner_runs)
    if (not isinstance(sources, dict) or not sources.keys() <= entries.keys() or any(
            not isinstance(ids, dict) or not ids or not ids.keys() <= {"ci", "pr", "promotion"} or
            any(type(id) is not int or id < 1 for id in ids.values()) or
            ("ci" not in ids and not ("promotion" in ids and entries[cohort]["promotion"]))
            for cohort, ids in sources.items())):
        parser.error("Owner runs must map selected cohorts to positive CI, PR and promotion run IDs")
    if (len(set(args.local_cohort)) != len(args.archives) or len(args.local_cohort) != len(args.archives) or
            not set(args.local_cohort) <= entries.keys() or set(args.local_cohort) & sources.keys()):
        parser.error("Each local cohort requires its own archive folder and cannot replace hosted evidence")
    local = {cohort: path.resolve() for cohort, path in zip(args.local_cohort, args.archives)}
    if args.through_wave is not None and args.through_wave < 1:
        parser.error("Wave must be positive")
    if args.require_admission and not any(args.through_wave is None or entries[key]["wave"] <= args.through_wave
                                          for key in sources):
        parser.error("Admission requires requested hosted owner evidence in the selected waves")
    args.output = args.output.resolve()
    args.output.mkdir(parents=True, exist_ok=True)
    results = admission(entries, read(args.patch_plan) if args.patch_plan else {}, sources, args.output,
                        args.through_wave, local)
    (args.output / "cohort-admission.json").write_text(
        json.dumps({"cohorts": entries, "admission": results}, indent=2) + "\n", encoding="utf-8")
    coverage = Counter(role for entry in entries.values() for package in entry["packages"].values()
                       for role in {role for profile in package["profiles"] for role in profile["roles"]})
    summary = ["## Broiler NuGet cohort admission", "",
               f"- Cohorts: {len(entries)}",
               f"- Package IDs: {sum(len(entry['packages']) for entry in entries.values())}",
               f"- Runtime-specific package coverage: {dict(coverage)}",
               f"- Dry-run archive sets verified: {sum(row['dryRun'] for row in results.values())}",
               f"- Cohorts admitted: {sum(row['admitted'] for row in results.values())}", "",
               "| Wave | Cohort | Admission blockers |", "| --- | --- | --- |"]
    for key, checks in results.items():
        blockers = "; ".join(checks["blockers"]).replace("|", "/").replace("\n", " ")
        summary.append(f"| {checks['wave']} | {key} | {blockers[:800] or 'Passed'} |")
    rendered = "\n".join(summary) + "\n"
    (args.output / "cohort-summary.md").write_text(rendered, encoding="utf-8")
    print(rendered, flush=True)
    required = "admitted" if args.require_admission else "dryRun"
    if any(not results[key][required] for key in sources
           if args.through_wave is None or results[key]["wave"] <= args.through_wave) or (
            any(not results[cohort]["dryRun"] for cohort in local
                if args.through_wave is None or results[cohort]["wave"] <= args.through_wave)):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
