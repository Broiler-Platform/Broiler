"""Open or refresh draft PRs from the validated plans of the Windows scan job."""
import argparse
from collections import defaultdict
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
from tempfile import TemporaryDirectory

from nuget_patches import DECLARATIONS, PACKAGE, POLICY, ROOT, checked, declaration_spans, patch_allowed

MARKER = "Broiler-NuGet-Patch: "


def git(repo, *arguments):
    return checked(["git", *arguments], repo)


def github(repo, endpoint, *arguments):
    return json.loads(checked(["gh", "api", endpoint, *arguments], repo))


def verify_edits(worktree, group, paths, previews):
    edits = defaultdict(list)
    package = group["package"]
    for update in group["updates"]:
        path = Path(update["declaration"]).relative_to(group["repository"]).as_posix()
        kind, field = update["itemType"], update["versionMetadata"]
        if (path not in paths or Path(path).suffix not in DECLARATIONS or
                (kind, field) not in {("PackageVersion", "Version"), ("PackageReference", "Version"),
                                      ("PackageReference", "VersionOverride")} or
                not patch_allowed(update["version"], update["target"], package in previews)):
            raise ValueError("Plan contains an out-of-policy declaration edit")
        edits[path].append(update)
    declarations = {path for path in paths if Path(path).suffix in DECLARATIONS}
    if not edits or edits.keys() != declarations:
        raise ValueError("Patch and declared version edits differ")
    for path, updates in edits.items():
        baseline = subprocess.run(["git", "show", f"HEAD:{path}"], cwd=worktree, check=True,
                                  capture_output=True, timeout=30).stdout
        changes = set()
        for update in updates:
            spans = declaration_spans(baseline, package, update["itemType"],
                                      update["versionMetadata"], update["version"])
            if not spans:
                raise ValueError(f"{path}: expected declaration was absent at the base commit")
            changes.update((start, end, update["target"].encode()) for start, end in spans)
        if len(changes) != len({(start, end) for start, end, _ in changes}):
            raise ValueError(f"{path}: conflicting version edits")
        for start, end, literal in sorted(changes, reverse=True):
            baseline = baseline[:start] + literal + baseline[end:]
        staged = subprocess.run(["git", "show", f":{path}"], cwd=worktree, check=True,
                                capture_output=True, timeout=30).stdout
        if baseline != staged:
            raise ValueError(f"{path}: patch contains changes beyond the approved version edits")


def draft(worktree, remote, base, group, artifact, previews):
    package = group["package"]
    if not PACKAGE.fullmatch(package):
        raise ValueError("Invalid package identity")
    commit = group["commit"]
    if git(worktree, "rev-parse", f"refs/remotes/origin/{base}") != commit:
        raise ValueError("The default branch has moved. Re-run the report.")
    suffix = hashlib.sha256(f"{group['repository']}/{package}".encode()).hexdigest()
    if group["patch"] != suffix + ".patch":
        raise ValueError("Unexpected patch artifact name")
    paths = group["paths"]
    if not paths or any(Path(path).is_absolute() or ".." in Path(path).parts or path.startswith(".")
                        or not (Path(path).suffix in DECLARATIONS or
                                re.fullmatch(r"packages(?:\..+)?\.lock\.json", Path(path).name))
                        for path in paths):
        raise ValueError("Patch contains an unexpected path")
    git(worktree, "checkout", "--quiet", "--detach", commit)
    git(worktree, "apply", "--index", "--", str(artifact / group["patch"]))
    changed = git(worktree, "diff", "--cached", "--name-status", "--no-renames").splitlines()
    if set(changed) != {f"M\t{path}" for path in paths}:
        raise ValueError("Patch includes undeclared paths or file creations")
    raw = git(worktree, "diff", "--cached", "--raw", "--no-renames").splitlines()
    if any(not re.match(r"^:(100644|100755) \1 ", line) for line in raw):
        raise ValueError("Patch attempts to change a file type or mode")
    verify_edits(worktree, group, paths, previews)

    branch = f"codex/nuget-patch/{suffix[:16]}"
    marker = MARKER + package
    ref = f"refs/heads/{branch}"
    owner = remote.split("/")[0]
    endpoint = f"repos/{remote}/pulls"
    prs = github(worktree, endpoint, "--method", "GET", "-f", f"head={owner}:{branch}", "-f", "state=all")
    current = [pr for pr in prs if pr["state"] == "open"]
    if len(current) > 1 or any(pr["base"]["ref"] != base or marker not in (pr["body"] or "")
                               for pr in current):
        raise ValueError(f"Existing PR for {branch} does not belong to this patch group")
    remote_ref = git(worktree, "ls-remote", "origin", ref)
    previous = remote_ref.split()[0] if remote_ref else ""
    if not previous or git(worktree, "write-tree") != existing_tree(worktree, ref, previous, marker):
        git(worktree, "-c", "core.hooksPath=/dev/null", "-c", "user.name=Broiler NuGet automation",
            "-c", "user.email=41898282+github-actions[bot]@users.noreply.github.com", "commit",
            "--quiet", "-m", f"Update {package} within the NuGet patch policy",
            "-m", f"{marker}\nRun: {os.environ.get('GITHUB_SERVER_URL', '')}/"
            f"{os.environ.get('GITHUB_REPOSITORY', '')}/actions/runs/{os.environ.get('GITHUB_RUN_ID', '')}")
        remote_base = git(worktree, "ls-remote", "origin", f"refs/heads/{base}")
        if not remote_base or remote_base.split()[0] != commit:
            raise ValueError("The default branch moved before the patch could be pushed")
        git(worktree, "push", f"--force-with-lease={ref}:{previous}", "origin", f"HEAD:{ref}")
    else:
        git(worktree, "restore", "--staged", "--worktree", "--", *paths)
        if not current and prs:
            print(f"{remote}: {package} patch was already reviewed and closed")
            return

    if current:
        print(f"{remote}: reused {current[0]['html_url']}")
        return
    title = f"Update {package} within the NuGet patch policy"
    body = ("The Broiler patch scan validated this dependency group with clean baseline and "
            "candidate restores. Producer CI will run its normal checks against the proposed changes.\n\n"
            f"<!-- {marker} -->")
    pr = github(worktree, endpoint, "--method", "POST", "-f", f"title={title}", "-f", f"head={branch}",
                "-f", f"base={base}", "-f", f"body={body}", "-F", "draft=true")
    print(f"{remote}: opened {pr['html_url']}")


def existing_tree(worktree, ref, previous, marker):
    git(worktree, "fetch", "--quiet", "--depth=1", "origin", ref)
    if git(worktree, "rev-parse", "FETCH_HEAD") != previous:
        raise ValueError("The patch branch changed while being read")
    message = git(worktree, "show", "-s", "--format=%B", "FETCH_HEAD")
    if marker not in message.splitlines():
        raise ValueError("The existing patch branch contains a commit outside this automation")
    return git(worktree, "rev-parse", "FETCH_HEAD^{tree}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("repository")
    parser.add_argument("artifact", type=Path)
    args = parser.parse_args()
    policy = json.loads(POLICY.read_text(encoding="utf-8"))
    if args.repository not in policy["pilots"]:
        parser.error("Draft PRs are restricted to configured pilot repositories")
    artifact = args.artifact.resolve()
    plan = json.loads((artifact / "patch-plan.json").read_text(encoding="utf-8"))
    if plan["errors"]:
        parser.error("Cannot propose patches from a failed scan")
    groups = [group for group in plan["groups"]
              if group["repository"] == args.repository and group["status"] == "validated"]
    if not groups:
        print("No validated patch groups")
        return 0
    remote = policy["pilots"][args.repository]
    base = github(ROOT, f"repos/{remote}")["default_branch"]
    failures = []
    for group in groups:
        # Each group starts in a fresh checkout so failure cannot leak changes into the next branch.
        with TemporaryDirectory(prefix="broiler-patch-pr-") as directory:
            worktree = Path(directory).resolve()
            try:
                git(worktree, "clone", "--quiet", "--depth=1", "--no-checkout", "--branch", base,
                    f"https://github.com/{remote}.git", ".")
                draft(worktree, remote, base, group, artifact, set(policy["previewPackages"]))
            except (ValueError, OSError, KeyError, subprocess.SubprocessError) as error:
                failures.append(f"{group['package']}: {error}")
    for failure in failures:
        print(failure)
    return int(bool(failures))


if __name__ == "__main__":
    raise SystemExit(main())
