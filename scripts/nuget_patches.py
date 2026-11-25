"""Discover NuGet patches and validate proposed edits in isolated repository copies."""
import argparse
from collections import Counter, defaultdict
from concurrent.futures import ThreadPoolExecutor
import gzip
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
from tempfile import TemporaryDirectory
import time
from urllib.error import HTTPError
from urllib.request import urlopen
from xml.parsers import expat

from nuget_publish_order import ROOT, OUTPUT as INVENTORY

OUTPUT = ROOT / "artifacts/nuget-patches"
POLICY = ROOT / "eng/nuget-patch-policy.json"
FEED = "https://api.nuget.org/v3/index.json"
PACKAGE = re.compile(r"[A-Za-z0-9][A-Za-z0-9_.-]*")
VERSION = re.compile(r"(\d+)\.(\d+)(?:\.(\d+))?(?:\.0)?(?:-preview\.(\d+))?", re.I)
RESTORE_CONFLICT = re.compile(r"\bNU(?:1107|1605|1608)\b")
AUDIT = re.compile(r"\bwarning (NU190[0-5])\s*:\s*(.*?)(?:\s+\[[^\n]*\])?$", re.M)
DECLARATIONS = {".props", ".targets", ".csproj", ".fsproj", ".vbproj"}


def version(value):
    match = VERSION.fullmatch(value)
    if not match:
        return None
    major, minor, patch, preview = match.groups()
    return int(major), int(minor), int(patch or 0), int(preview) if preview is not None else None


def patch_allowed(current, candidate, previews):
    old, new = version(current), version(candidate)
    if not old or not new or old[:2] != new[:2]:
        return False
    if old[3] is None:
        return new[3] is None and new[2] > old[2]
    return previews and old[:3] == new[:3] and new[3] is not None and new[3] > old[3]


def get_json(url):
    if not url.startswith("https://api.nuget.org/"):
        raise ValueError("Unexpected NuGet.org resource URL")
    for attempt in range(3):
        try:
            with urlopen(url, timeout=25) as response:
                body = response.read()
                return json.loads(gzip.decompress(body) if body.startswith(b"\x1f\x8b") else body)
        except OSError as error:
            if attempt == 2 or (isinstance(error, HTTPError) and error.code not in {429, 500, 502, 503, 504}):
                raise
            time.sleep(2 ** attempt)


def registration(base, package):
    try:
        pages = get_json(f"{base.rstrip('/')}/{package}/index.json")["items"]
    except HTTPError as error:
        if error.code == 404:
            return []
        raise
    return [record["catalogEntry"] for page in pages
            for record in (page["items"] if "items" in page else get_json(page["@id"])["items"])]


def candidates(current, entries, previews):
    eligible, blocked, newer_lines = [], [], []
    old = version(current)
    if old is None:
        return eligible, blocked, newer_lines
    for item in entries:
        candidate = item["version"]
        parsed = version(candidate)
        if not parsed:
            continue
        if patch_allowed(current, candidate, previews):
            if not item.get("listed", True):
                blocked.append({"version": candidate, "reason": "unlisted"})
            elif item.get("vulnerabilities"):
                blocked.append({"version": candidate, "reason": "known vulnerability"})
            elif item.get("deprecation"):
                blocked.append({"version": candidate, "reason": "deprecated"})
            else:
                eligible.append(candidate)
        elif old[3] is None and parsed[3] is None and parsed[:2] > old[:2] and item.get("listed", True):
            newer_lines.append((parsed[0] != old[0], candidate))
    higher = {name: max((v for is_major, v in newer_lines if is_major == major),
                        key=version, default=None) for name, major in (("minor", False), ("major", True))}
    return sorted(set(eligible), key=version, reverse=True), blocked, higher


def observations(snapshot, selected):
    result = defaultdict(dict)
    for row in snapshot["projects"]:
        repo = row["repository"]
        if selected and repo not in selected:
            continue
        for profile in row["profiles"]:
            for dependency in profile["packages"]:
                package = dependency["id"].casefold()
                fingerprint = (dependency["version"], dependency["declaration"],
                               dependency["itemType"], dependency["versionMetadata"])
                entry = result[repo, package].setdefault(fingerprint, {
                    **dependency, "uses": set()})
                entry["uses"].add((row["project"], profile["configuration"],
                                   profile["framework"], profile["runtime"]))
    for pins in result.values():
        for pin in pins.values():
            pin["uses"] = sorted(pin["uses"])
    return result


def source_consumers(snapshot):
    reverse = defaultdict(set)
    for row in snapshot["projects"]:
        for profile in row["profiles"]:
            use = (row["project"], profile["configuration"], profile["framework"], profile["runtime"])
            for reference in profile.get("references", []):
                reverse[row["repository"], reference["project"].casefold()].add(use)
    return reverse


def affected_uses(repo, pins, reverse):
    affected = {tuple(use) for pin in pins for use in pin["uses"]}
    pending = list(affected)
    while pending:
        project, _, _, _ = pending.pop()
        downstream = reverse[repo, project.casefold()] - affected
        affected.update(downstream)
        pending.extend(downstream)
    return sorted(affected)


def declaration_spans(data, package, item_type, metadata, old):
    """Find a literal attribute or metadata element and keep the XML bytes intact."""
    parser = expat.ParserCreate()
    stack = []
    spans = []
    value = re.escape(old.encode())
    field = re.escape(metadata.encode())
    tag = re.compile(rb"""<(?:[^>"']|"[^"]*"|'[^']*')*>""")
    attribute = re.compile(rb"\s" + field + rb"\s*=\s*(['\"])(" + value + rb")\1")

    def start(name, attrs):
        offset = parser.CurrentByteIndex
        used = (attrs.get("Include") or attrs.get("Update") or "").casefold()
        matched = name == item_type and used == package
        if matched and attrs.get(metadata) == old:
            token = tag.match(data, offset)
            if token:
                hit = attribute.search(token.group())
                if hit:
                    spans.append((offset + hit.start(2), offset + hit.end(2)))
        if name == metadata and stack and stack[-1]:
            token = tag.match(data, offset)
            if token:
                literal = re.compile(rb"\s*(" + value + rb")\s*</" + field + rb"\s*>").match(data, token.end())
                if literal:
                    spans.append(literal.span(1))
        stack.append(matched)

    parser.StartElementHandler = start
    parser.EndElementHandler = lambda _: stack.pop()
    parser.Parse(data, True)
    return spans


def run(args, cwd, **kwargs):
    return subprocess.run(args, cwd=cwd, text=True, encoding="utf-8", errors="replace",
                          stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=180, **kwargs)


def checked(args, cwd, **kwargs):
    result = run(args, cwd, **kwargs)
    if result.returncode:
        raise ValueError(result.stdout[-4500:].strip())
    return result.stdout.strip()


def restore(worktree, repo, uses, packages, candidate=False):
    graphs, advisories = {}, set()
    for project, configuration in sorted({(p, c) for p, c, _, _ in uses}):
        path = worktree / Path(project).relative_to(repo)
        config = next((p for p in worktree.iterdir() if p.name.casefold() == "nuget.config"), None)
        args = ["dotnet", "restore", str(path), "--source", FEED, "--packages", str(packages),
                "--force", "--no-http-cache", "-p:NuGetAuditMode=all", "-p:Configuration=" + configuration,
                "-p:DisableImplicitLibraryPacksFolder=true",
                "-p:UseBroilerUiSource=false", "-p:UseBroilerDocumentsFormatCodesSource=false"]
        if config:
            args += ["--configfile", str(config)]
        if candidate:
            args += ["--force-evaluate", "-p:RestoreLockedMode=false"]
        output = run(args, worktree)
        warnings = list(AUDIT.findall(output.stdout))
        if any(code in {"NU1900", "NU1905"} for code, _ in warnings):
            raise ValueError(f"{project}: NuGet audit data unavailable\n{output.stdout[-2500:]}")
        advisories.update((project, configuration, code, message) for code, message in warnings)
        if output.returncode or RESTORE_CONFLICT.search(output.stdout):
            raise RestoreError(f"{project} ({configuration})\n{output.stdout[-4000:]}",
                               bool(RESTORE_CONFLICT.search(output.stdout)))
        asset_path = checked(["dotnet", "msbuild", str(path), "-nologo", "-getProperty:ProjectAssetsFile",
                              "-p:Configuration=" + configuration], worktree)
        assets = json.loads(Path(asset_path).read_text(encoding="utf-8-sig"))
        if set(assets["project"]["restore"]["sources"]) != {FEED}:
            raise ValueError(f"{project}: unexpected restore sources")
        folders = {Path(folder).resolve() for folder in assets["packageFolders"]}
        if folders != {packages.resolve()}:
            raise ValueError(f"{project}: unexpected package folders")
        libraries = assets["libraries"]
        for target, deps in assets["targets"].items():
            graph = {}
            for key, info in deps.items():
                if info["type"] == "package":
                    name, resolved = key.rsplit("/", 1)
                    graph[name.casefold()] = {"version": resolved, "hash": libraries[key]["sha512"]}
            graphs[project, configuration, target] = graph
        for _, _, framework, runtime in (u for u in uses if u[:2] == (project, configuration)):
            target = framework + ("/" + runtime if runtime else "")
            if target and (project, configuration, target) not in graphs:
                raise ValueError(f"{project}: required restore target {target} was not evaluated")
    return graphs, advisories


class RestoreError(ValueError):
    def __init__(self, message, conflict):
        super().__init__(message)
        self.conflict = conflict


def diff_graphs(before, after, package, pins, previews):
    changes, violations = [], []
    if before.keys() != after.keys():
        violations.append("Restore targets changed")
    expected = {}
    for pin, candidate in pins:
        for project, configuration, framework, runtime in pin["uses"]:
            target = framework + ("/" + runtime if runtime else "")
            scope = (project, configuration, target)
            if scope in expected and expected[scope] != candidate:
                violations.append(f"{' '.join(scope)}: inconsistent planned versions")
            expected[scope] = candidate
    for scope in sorted(before.keys() | after.keys()):
        old, new = before.get(scope, {}), after.get(scope, {})
        if scope in expected and version(new.get(package, {}).get("version", "")) != version(expected[scope]):
            violations.append(f"{' '.join(scope)}: {package} did not resolve to {expected[scope]}")
        for key in sorted(old.keys() | new.keys()):
            prior, resolved = old.get(key), new.get(key)
            if prior == resolved:
                continue
            changes.append({"scope": scope, "package": key, "before": prior, "after": resolved})
            if key == package and scope in expected:
                if not prior or not resolved or not patch_allowed(prior["version"], resolved["version"], key in previews):
                    violations.append(f"{' '.join(scope)}: {key} changed outside the resolved patch policy")
                continue
            if not prior or not resolved or not patch_allowed(prior["version"], resolved["version"], key in previews):
                violations.append(f"{' '.join(scope)}: out-of-policy resolved change for {key}")
    return changes, violations


def edits(worktree, repo, package, pin, replacement):
    source = Path(pin["declaration"])
    try:
        path = source.relative_to(repo)
    except ValueError as error:
        raise ValueError("Version declaration is outside its owning repository") from error
    if source.is_absolute() or ".." in path.parts or not path.parts or path.suffix not in DECLARATIONS:
        raise ValueError("Version does not have an editable project declaration")
    data = (worktree / path).read_bytes()
    spans = declaration_spans(data, package, pin["itemType"], pin["versionMetadata"], pin["version"])
    if not spans:
        raise ValueError(f"{path.as_posix()}: property-backed, imported override, or nonliteral version needs review")
    return path, [(start, end, replacement.encode()) for start, end in spans]


def verify_group(worktree, repo, package, group, previews, cache):
    options = [(pin, pin["eligible"] or [pin["version"]]) for pin in group["pins"]]
    tracked = set(checked(["git", "ls-files"], worktree).splitlines())
    locks = sorted(path for path in tracked if re.fullmatch(r"packages(?:\..+)?\.lock\.json", Path(path).name))
    uses = group["affectedUses"]
    # Guard declarations before spending a restore on this group.
    if any(pin["status"] not in {"eligible", "current", "outside-patch-policy"} for pin, _ in options):
        return "guarded", "At least one active pin requires separate review"
    try:
        for pin, values in options:
            if pin["eligible"]:
                edits(worktree, repo, package, pin, values[0])
    except (OSError, ValueError, expat.ExpatError) as error:
        return "guarded", str(error)
    try:
        before, audit_before = restore(worktree, repo, uses, cache)
    except (OSError, ValueError, subprocess.TimeoutExpired, KeyError) as error:
        return "baseline-failed", str(error)

    group["baselineAdvisories"] = sorted(audit_before)
    group["attempts"] = []
    for index in range(max(len(values) for _, values in options)):
        chosen = [(pin, values[min(index, len(values) - 1)]) for pin, values in options]
        changes = defaultdict(list)
        for pin, selected in chosen:
            if selected != pin["version"]:
                path, replacements = edits(worktree, repo, package, pin, selected)
                changes[path].extend(replacements)
        affected = {path.as_posix() for path in changes} | set(locks)
        attempt = {"versions": sorted({selected for _, selected in chosen})}
        group["attempts"].append(attempt)
        try:
            for path, replacements in changes.items():
                if path.as_posix() not in tracked:
                    raise ValueError(f"{path}: declaration is not tracked at the inventoried commit")
                file = worktree / path
                data = file.read_bytes()
                for start, end, new in sorted(set(replacements), reverse=True):
                    data = data[:start] + new + data[end:]
                file.write_bytes(data)
            after, audit_after = restore(worktree, repo, uses, cache, candidate=True)
            attempt["resolvedChanges"], violations = diff_graphs(before, after, package, chosen, previews)
            attempt["advisories"] = sorted(audit_after)
            if audit_after - audit_before:
                violations.append("Candidate introduces NuGet audit advisories")
            if violations:
                attempt["reason"] = violations
                continue
            changed = set(checked(["git", "diff", "--name-only"], worktree).splitlines())
            if not changed or not changed <= affected:
                raise ValueError(f"Unexpected tracked changes: {sorted(changed - affected)}")
            patch = subprocess.run(["git", "diff", "--binary", "--no-ext-diff"], cwd=worktree,
                                   capture_output=True, timeout=180)
            if patch.returncode:
                raise ValueError(patch.stderr.decode("utf-8", errors="replace"))
            if any(path in changed for path in locks):
                # The candidate lock must also pass its ordinary restore without unlocking.
                restore(worktree, repo, uses, cache)
            updates = [{key: pin[key] for key in ("declaration", "itemType", "versionMetadata", "version")}
                       | {"target": selected} for pin, selected in chosen if selected != pin["version"]]
            return "validated", {"paths": sorted(changed), "patch": patch.stdout,
                                 "versions": attempt["versions"], "updates": updates}
        except RestoreError as error:
            attempt["reason"] = str(error)
            if not error.conflict:
                return "candidate-failed", str(error)
        except (OSError, ValueError, subprocess.TimeoutExpired, KeyError) as error:
            return "candidate-failed", str(error)
        finally:
            if affected:
                checked(["git", "restore", "--worktree", "--", *sorted(affected & tracked)], worktree)
    return "blocked", "No eligible patch passed restore and dependency checks"


def audit(snapshot, policy, selected, validation, output):
    observed = observations(snapshot, selected)
    if not observed:
        raise ValueError("No matching package references in the inventory")
    errors, groups = [], []
    previews = set(policy["previewPackages"])
    guarded_dirs = {part.casefold() for part in policy["guardedDirectories"]}
    reverse = source_consumers(snapshot)
    entries = {}
    try:
        service = get_json(FEED)
        base = next(item["@id"] for item in service["resources"] if item["@type"] == "RegistrationsBaseUrl/3.6.0")
    except (OSError, ValueError, KeyError, StopIteration) as error:
        return [], [f"NuGet.org service index: {error}"]

    def lookup(package):
        try:
            return package, registration(base, package), None
        except (OSError, ValueError, KeyError) as error:
            return package, [], str(error)

    with ThreadPoolExecutor(max_workers=8) as executor:
        for package, items, error in executor.map(lookup, sorted({key[1] for key in observed if PACKAGE.fullmatch(key[1])})):
            entries[package] = items, error
            if error:
                errors.append(f"{package}: registration lookup failed: {error}")

    for (repo, package), pins in sorted(observed.items()):
        items, error = entries.get(package, ([], "Invalid package identity"))
        group = {"repository": repo, "package": package,
                 "commit": snapshot["revisions"][repo]["commit"], "pins": list(pins.values()),
                 "affectedUses": affected_uses(repo, pins.values(), reverse)}
        for pin in pins.values():
            current = pin["version"]
            versions, excluded, newer = candidates(current, items, package in previews)
            paths = [pin["declaration"], *(use[0] for use in pin["uses"])]
            guarded = any(part.casefold().split(".")[-1] in guarded_dirs
                          for path in paths for part in Path(path).parts[:-1])
            reason = None
            if error:
                status, reason = "feed-error", error
            elif not items:
                status, reason = "unavailable", "No package registration on the approved feed"
            elif guarded:
                status, reason = "guarded", "Pinned conformance or evidence input"
            elif not version(current):
                status, reason = "guarded", "Range, floating, nonstandard or unresolved version"
            elif version(current)[3] is not None and package not in previews:
                status, reason = "guarded", "Preview updates require an explicit opt-in"
            else:
                status = "eligible" if versions else ("outside-patch-policy" if any(newer.values()) else "current")
            current_metadata = [item for item in items if version(item["version"]) == version(current)] if version(current) else []
            pin.update(status=status, reason=reason, eligible=versions if status == "eligible" else [],
                       excluded=excluded, newerLines=newer,
                       currentAdvisories=[item for entry in current_metadata for item in (entry.get("vulnerabilities") or [])])
        group["status"] = "eligible" if any(pin["eligible"] for pin in pins.values()) else (
            "feed-error" if any(pin["status"] == "feed-error" for pin in pins.values()) else "no-edit")
        groups.append(group)

    for repo in sorted(validation):
        planned = [group for group in groups if group["repository"] == repo and group["status"] == "eligible"]
        if not planned:
            continue
        try:
            validate_repo(repo, planned, previews, output)
        except (OSError, subprocess.TimeoutExpired, ValueError) as error:
            errors.append(f"{repo}: {error}")
            for group in planned:
                if group["status"] == "eligible":
                    group.update(status="validation-error", reason=str(error))
    return groups, errors


def validate_repo(repo, planned, previews, output):
    with TemporaryDirectory(prefix="restore-", dir=output) as directory:
        scratch = Path(directory).resolve()
        worktree = scratch / "src"
        cache = scratch / "packages"
        commit = planned[0]["commit"]
        if not commit or not re.fullmatch("[0-9a-f]{40}", commit):
            raise ValueError("A Git commit is required for baseline restore")
        checked(["git", "-c", "core.longpaths=true", "clone", "--quiet", "--shared", "--no-checkout",
                 str(ROOT / repo), str(worktree)], ROOT)
        checked(["git", "config", "core.autocrlf", "false"], worktree)
        checked(["git", "config", "core.longpaths", "true"], worktree)
        checked(["git", "checkout", "--quiet", "--detach", commit], worktree)
        for group in planned:
            package = group["package"]
            print(f"Checking {repo}: {package}", flush=True)
            status, outcome = verify_group(worktree, repo, package, group, previews, cache)
            group["status"] = status
            if status == "validated":
                patch = outcome.pop("patch")
                name = hashlib.sha256(f"{repo}/{package}".encode()).hexdigest() + ".patch"
                (output / name).write_bytes(patch)
                group.update(outcome, patch=name)
            else:
                group["reason"] = outcome


def write_blocker_report(output, plan):
    group_statuses = {"guarded", "blocked", "baseline-failed", "candidate-failed", "validation-error"}
    pin_statuses = {"feed-error", "guarded", "unavailable", "outside-patch-policy"}
    owners = defaultdict(list)
    for group in plan["groups"]:
        issues = set()
        if group["status"] in group_statuses:
            issues.add((group["status"], group["reason"]))
        for pin in group["pins"]:
            if pin["status"] in pin_statuses:
                issues.add((pin["status"], pin["reason"] or "Only a newer minor or major line is listed"))
            if not pin["eligible"]:
                issues.update(("excluded-candidate", item["reason"]) for item in pin["excluded"])
        if issues:
            owners[group["repository"]].append({
                "package": group["package"], "commit": group["commit"],
                "currentVersions": sorted({pin["version"] for pin in group["pins"]}),
                "issues": [{"status": status, "reason": reason} for status, reason in sorted(issues)],
                "attempts": group.get("attempts", []),
            })

    report = {"source": plan["source"], "runUrl": plan["runUrl"], "errors": plan["errors"],
              "owners": dict(sorted(owners.items()))}
    (output / "patch-blockers.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    summary = ["## Dependency blockers and policy holds", ""]
    if plan["errors"]:
        summary += [f"The audit reported {len(plan['errors'])} feed or scan errors. See the artifact for details.", ""]
    if owners:
        summary += ["| Owning repository | Dependency groups | Reasons |", "| --- | ---: | --- |"]
        for owner, entries in report["owners"].items():
            counts = Counter(status for entry in entries for status in {issue["status"] for issue in entry["issues"]})
            reasons = ", ".join(f"{reason} ({count})" for reason, count in sorted(counts.items()))
            label = owner.replace("|", "&#124;")
            summary.append(f"| {label} | {len(entries)} | {reasons} |")
    elif not plan["errors"]:
        summary.append("No blocked dependency groups or policy holds.")
    (output / "patch-blockers.md").write_text("\n".join(summary) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", type=Path, default=INVENTORY / "evaluated-projects.json")
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--repository", action="append", help="Limit reporting to this repository")
    parser.add_argument("--validate", action="append", help="Restore and validate this pilot's proposals")
    args = parser.parse_args()
    snapshot = json.loads(args.inventory.read_text(encoding="utf-8"))
    policy = json.loads(POLICY.read_text(encoding="utf-8"))
    selected, validation = set(args.repository or []), set(args.validate or [])
    if not validation <= policy["pilots"].keys() or (selected and not validation <= selected):
        parser.error("Validation requires a configured pilot in the selected reporting scope")
    if any(row["errors"] for row in snapshot["projects"]):
        parser.error("Inventory contains evaluation errors. Run Phase 1 successfully first.")
    args.output.mkdir(parents=True, exist_ok=True)
    groups, errors = audit(snapshot, policy, selected, validation, args.output.resolve())
    run_url = None
    if all(os.environ.get(name) for name in ("GITHUB_SERVER_URL", "GITHUB_REPOSITORY", "GITHUB_RUN_ID")):
        run_url = f"{os.environ['GITHUB_SERVER_URL']}/{os.environ['GITHUB_REPOSITORY']}/actions/runs/{os.environ['GITHUB_RUN_ID']}"
    result = {"source": FEED, "groups": groups, "errors": errors, "runUrl": run_url,
              "revisions": {repo: revision for repo, revision in snapshot["revisions"].items()
                            if not selected or repo in selected}}
    (args.output / "patch-plan.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    write_blocker_report(args.output, result)
    summary = ["## Broiler NuGet patch report", "",
               f"- Dependency groups checked: {len(groups)}",
               f"- Groups with a patch candidate: {sum(any(p['eligible'] for p in g['pins']) for g in groups)}",
               f"- Restore-validated patch groups: {sum(g['status'] == 'validated' for g in groups)}",
               f"- Groups blocked during restore validation: {sum(g['status'] in {'blocked', 'baseline-failed', 'candidate-failed'} for g in groups)}",
               f"- Feed or scan errors: {len(errors)}", ""]
    summary.extend(f"- {g['repository']} / {g['package']}: {g['status']} ({', '.join(g.get('versions', []))})"
                   for g in groups if g["status"] in {"validated", "blocked", "baseline-failed", "candidate-failed"})
    (args.output / "patch-summary.md").write_text("\n".join(summary) + "\n", encoding="utf-8")
    if os.environ.get("GITHUB_OUTPUT"):
        with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
            output.write("validated=" + str(any(g["status"] == "validated" for g in groups)).lower() + "\n")
    print("\n".join(summary), flush=True)
    for error in errors:
        print(error, flush=True)
    return int(bool(errors))


if __name__ == "__main__":
    raise SystemExit(main())
