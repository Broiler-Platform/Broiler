#!/usr/bin/env python3
"""Regenerate eng/aggregate-workspace-references.targets.

Every component submodule carries nested checkouts ("mirrors") of the components it
depends on, so that it still builds standalone. Its project references are relative, so
in the aggregate workspace they resolve into those mirrors rather than into the canonical
top-level checkout. One root build then compiles the same component several times from
several source trees, emitting assemblies that share a name -- `Broiler.Media` was built
from nine different project paths, `Broiler.Graphics` from eight. Broiler.Media ADR 0001
forbids it: during aggregate development every local project reference must point at the
single root checkout.

The root Directory.Build.props already collapses two kernels this way, via
$(BroilerDomPath)/$(BroilerGraphicsPath): each submodule defaults the property to its own
nested copy for standalone builds, and the root value wins in root builds. That mechanism
is the better one, but it only works for references written as $(BroilerXPath), and the
component submodules never adopted it -- their references are literal relative paths. This
script covers those from the root instead, needing no submodule change.

SAFETY -- a mirror is redirected only when its pinned commit matches the canonical
checkout's, so the redirect cannot change which source compiles. Mirrors pinned to a
different commit are left alone and reported; collapsing one would silently swap the
sources that go into a shipping assembly, which is a decision for a human. Mirrors with no
canonical top-level checkout at all (Broiler.JS vendors Broiler.Unicode, Broiler.Regex and
Broiler.DateTime, which are not top-level submodules) cannot be redirected either.

References carrying metadata (ReferenceOutputAssembly, OutputItemType,
GlobalPropertiesToRemove) are skipped: the Remove/Include rewrite below would drop it.
Today those are all inside Broiler.JS, which is excluded anyway.

A component that ships its own Directory.Build.targets is skipped too, because MSBuild
imports only the nearest such file and the root one therefore never reaches its projects.

Whether a component's file chains upward is decided by looking for that call outside XML
comments; a file that only names it, such as one warning a reader not to chain, is correctly
read as breaking the chain.

DO NOT "fix" that by chaining the component's file upward with GetPathOfFileAbove. It was
tried on Broiler.Browser and it makes things strictly worse. Broiler.Browser's four
solutions enumerate 80 projects that live in its own mirrors, and they have to: those
relative paths are what let the component build standalone in its own CI. Redirecting only
its ProjectReferences leaves the solutions still building the mirror copies, so a build
compiles the canonical AND the mirror copy of Broiler.UI, Broiler.Graphics, Broiler.Input
and Broiler.Media -- inventing the very duplicate this script exists to remove, in a
component that had none. Collapsing such a component needs its solutions rewritten as well,
which cannot be done without breaking its standalone build; it is a real piece of design
work, not a one-line import.

None of this costs anything today: no root solution references Broiler.Browser, so its
projects are never built from the root, only from its own solutions.

Usage:  python3 scripts/generate-aggregate-reference-redirects.py [--check]
        --check exits non-zero if the generated file is stale.
"""

from __future__ import annotations

import argparse
import collections
import os
import re
import subprocess
import sys

ROOT = os.path.realpath(os.path.join(os.path.dirname(__file__), ".."))
OUTPUT = os.path.join(ROOT, "eng", "aggregate-workspace-references.targets")

REFERENCE_RE = re.compile(
    r"<ProjectReference\b([^>]*?)/>|<ProjectReference\b([^>]*?)>(.*?)</ProjectReference>", re.S
)
INCLUDE_RE = re.compile(r'Include="([^"]+)"')

# XML comments cannot nest and cannot contain '--', so a non-greedy match is exact here
# rather than merely close enough.
XML_COMMENT_RE = re.compile(r"<!--.*?-->", re.S)


def git_sha(path: str) -> str | None:
    result = subprocess.run(
        ["git", "-C", path, "rev-parse", "HEAD"], capture_output=True, text=True
    )
    return result.stdout.strip() if result.returncode == 0 else None


# Extensions a build actually consumes. A difference outside this set (a CI workflow, a
# README, a .gitignore) cannot change the assembly the compiler produces.
BUILD_INPUT_SUFFIXES = (
    ".cs", ".csproj", ".props", ".targets", ".sln", ".slnx", ".resx", ".json",
    ".xaml", ".vb", ".fs", ".fsproj", ".editorconfig", ".ruleset", ".snk",
)


def build_relevant_diff(component: str, mirror_sha: str, canonical_sha: str) -> list[str] | None:
    """Build inputs that differ between two commits of a component.

    Returns [] when the two commits are build-identical, the differing paths when they are
    not, and None when the comparison cannot be made (the canonical checkout does not have
    the mirror's commit), which callers must treat as unsafe rather than as "no difference".
    """
    path = os.path.join(ROOT, component)
    for sha in (mirror_sha, canonical_sha):
        probe = subprocess.run(
            ["git", "-C", path, "cat-file", "-e", f"{sha}^{{commit}}"],
            capture_output=True,
            text=True,
        )
        if probe.returncode != 0:
            return None
    result = subprocess.run(
        ["git", "-C", path, "diff", "--name-only", mirror_sha, canonical_sha],
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        return None
    return [
        line
        for line in result.stdout.splitlines()
        if line.strip().endswith(BUILD_INPUT_SUFFIXES)
    ]


def components() -> list[str]:
    return sorted(
        d
        for d in os.listdir(ROOT)
        if d.startswith("Broiler.") and os.path.isdir(os.path.join(ROOT, d))
    )


def discover_mirrors() -> dict[str, dict]:
    """Nested directories that are their own git checkout of another component."""
    canonical = {c: git_sha(os.path.join(ROOT, c)) for c in components()}
    mirrors: dict[str, dict] = {}
    for owner in components():
        owner_dir = os.path.join(ROOT, owner)
        for entry in sorted(os.listdir(owner_dir)):
            nested = os.path.join(owner_dir, entry)
            if not entry.startswith("Broiler."):
                continue
            if not os.path.isdir(nested) or not os.path.exists(os.path.join(nested, ".git")):
                continue
            mirror_sha = git_sha(nested)
            canonical_sha = canonical.get(entry)
            if canonical_sha is None:
                reason = "no canonical top-level checkout"
            elif mirror_sha == canonical_sha:
                reason = None
            else:
                # A differing pin does not by itself make a redirect unsafe: what matters is
                # whether the SOURCE the compiler sees differs. Pins routinely drift by a
                # release-workflow or docs commit that no build reads, and blocking on SHA
                # equality would leave those duplicates in place for no reason. Compare the
                # trees instead, restricted to files a build actually consumes.
                differing = build_relevant_diff(entry, mirror_sha, canonical_sha)
                if differing is None:
                    reason = (
                        f"pin differs (mirror {mirror_sha[:10]}, canonical "
                        f"{canonical_sha[:10]}) and the trees cannot be compared"
                    )
                elif differing:
                    shown = ", ".join(differing[:2])
                    more = f" (+{len(differing) - 2} more)" if len(differing) > 2 else ""
                    reason = (
                        f"pin differs and {len(differing)} build input(s) differ: {shown}{more}"
                    )
                else:
                    reason = None
            mirrors[f"{owner}/{entry}"] = {
                "owner": owner,
                "component": entry,
                "skip_reason": reason,
            }
    return mirrors


def owning_mirror(relative_path: str, mirrors: dict) -> str | None:
    parts = relative_path.split(os.sep)
    for i in range(len(parts) - 1):
        key = f"{parts[i]}/{parts[i + 1]}"
        if key in mirrors:
            return key
    return None


def nearest_build_targets(project: str) -> str | None:
    """Directory of a Directory.Build.targets that hides the root one from this project.

    MSBuild imports only the nearest such file above a project. Returns None when the root
    file is reached (or when every file between chains upward with GetPathOfFileAbove, since
    the root one is then still imported); otherwise the repo-relative directory of the file
    that breaks the chain.

    Comments are stripped before the test. A file that only MENTIONS the function -- in a
    comment warning against chaining, say -- does not chain, and reading it as though it did
    is the worst failure this script has: the component looks covered, so redirects are
    emitted for its projects, and they then compete with the nested checkouts its own
    solutions enumerate. Broiler.Code hit exactly that, and the symptom was 55 redirects
    that would have double-built four components.

    The test stays a substring one on the rest of the file rather than looking only at
    Import/@Project, because the chaining idiom is routinely split in two: a property holds
    the GetPathOfFileAbove call and the Import consumes the property (Broiler.UI's
    Directory.Build.props is written that way). Matching only the Import element would miss
    that form and wrongly report a chaining file as breaking the chain.
    """
    directory = os.path.dirname(project)
    while True:
        candidate = os.path.join(directory, "Directory.Build.targets")
        if os.path.exists(candidate):
            if directory == ROOT:
                return None
            try:
                with open(candidate, encoding="utf-8-sig") as handle:
                    text = handle.read()
                chains = "GetPathOfFileAbove" in XML_COMMENT_RE.sub("", text)
            except OSError:
                chains = False
            if not chains:
                return os.path.relpath(directory, ROOT)
            # It chains, so keep walking: an ancestor could still break the chain.
        if directory == ROOT or os.path.dirname(directory) == directory:
            return None
        directory = os.path.dirname(directory)


def project_files() -> list[str]:
    found = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", ".git")]
        found.extend(
            os.path.join(dirpath, f) for f in filenames if f.endswith(".csproj")
        )
    return sorted(found)


def collect(mirrors: dict):
    """Return (redirects, skipped) for references that resolve into a mirror."""
    redirects = collections.defaultdict(list)
    skipped = collections.Counter()
    for project in project_files():
        relative_project = os.path.relpath(project, ROOT)
        # A project that itself lives inside a mirror is a copy; the canonical one is
        # visited separately, so rewriting the copy would be pointless churn.
        if owning_mirror(relative_project, mirrors):
            continue
        # MSBuild imports only the NEAREST Directory.Build.targets above a project. A
        # component that ships its own (and does not chain upward) never sees the root one,
        # so a redirect emitted for its projects would silently do nothing -- worse than not
        # emitting it, because the file would claim coverage it does not have. Note this is
        # decided below, once we know the project actually has references worth redirecting.
        shadowed_by = nearest_build_targets(project)
        try:
            text = open(project, encoding="utf-8-sig").read()
        except OSError:
            continue
        for match in REFERENCE_RE.finditer(text):
            attributes = match.group(1) or match.group(2) or ""
            body = (match.group(3) or "").strip()
            include = INCLUDE_RE.search(attributes)
            if not include:
                continue
            spec = include.group(1)
            resolved = os.path.realpath(
                os.path.join(os.path.dirname(project), spec.replace("\\", "/"))
            )
            key = owning_mirror(os.path.relpath(resolved, ROOT), mirrors)
            if not key:
                continue
            mirror = mirrors[key]
            if mirror["skip_reason"]:
                skipped[f'{key}: {mirror["skip_reason"]}'] += 1
                continue
            extra = [
                a
                for a in re.findall(r"(\w+)=", attributes)
                if a not in ("Include", "Remove", "Update", "Condition")
            ]
            if body or extra:
                skipped[f"{key}: reference carries metadata, cannot rewrite safely"] += 1
                continue
            # Re-root at the canonical checkout: strip everything before the component
            # segment that starts the mirror, however deeply nested it is.
            if shadowed_by is not None:
                skipped[
                    f"{shadowed_by}: ships its own Directory.Build.targets, so the root one is "
                    f"never imported for its projects. Chaining it upward does NOT fix this; "
                    f"see the note at the top of the generator script"
                ] += 1
                continue
            tail = os.path.relpath(resolved, os.path.join(ROOT, mirror["owner"], mirror["component"]))
            canonical = os.path.join(mirror["component"], tail)
            redirects[relative_project].append((spec, canonical.replace("/", "\\")))
    return redirects, skipped


def render(redirects: dict, skipped: collections.Counter) -> str:
    total = sum(len(v) for v in redirects.values())
    lines = [
        "<Project>",
        "",
        "  <!--",
        "    GENERATED FILE \u2014 do not edit by hand.",
        "    Regenerate with: python3 scripts/generate-aggregate-reference-redirects.py",
        "",
        "    Paths are anchored on $(BroilerWorkspaceRoot), set by Directory.Build.targets before it",
        "    imports this file. It must be an already-normalised absolute path: MSBuild compares",
        "    condition strings literally, so a '..' segment in either operand never matches and every",
        "    redirect below would silently do nothing.",
        "",
        "    Each component submodule carries nested checkouts of the components it depends on so",
        "    that it still builds standalone, and its project references are relative, so in the",
        "    aggregate workspace they resolve into those mirrors instead of the canonical top-level",
        "    checkout. One root build then compiled the same component several times over: nine",
        "    copies of Broiler.Media, eight of Broiler.Graphics, and 53 duplicated assemblies in",
        "    total across the root solutions. Broiler.Media ADR 0001 forbids it: during aggregate",
        "    development every local project reference to a component must point at the single root",
        "    checkout.",
        "",
        "    A mirror is redirected only when its pinned commit matches the canonical checkout's, so",
        "    this can never change which source compiles. Mirrors on a different pin, and mirrors of",
        "    components that have no top-level checkout, are deliberately left alone; see the",
        "    generator for the list and the reasons.",
        "",
        f"    {total} reference(s) across {len(redirects)} project(s).",
        "  -->",
        "",
    ]
    if skipped:
        lines.append("  <!--")
        lines.append("    Deliberately NOT redirected:")
        for reason, count in sorted(skipped.items()):
            lines.append(f"      {count:4} reference(s)  {reason}")
        lines.append("  -->")
        lines.append("")
    for project in sorted(redirects):
        msbuild_project = project.replace("/", "\\")
        lines.append(
            f"  <ItemGroup Condition=\"'$(MSBuildProjectFullPath)' == '$(BroilerWorkspaceRoot){msbuild_project}'\">"
        )
        for spec, canonical in sorted(redirects[project]):
            # Both halves are guarded on the canonical project being present. A component's
            # own CI checks out only that submodule (recursively), deliberately relying on
            # its nested mirrors -- the top-level checkouts do not exist there. Unguarded,
            # the Remove would strip a reference that resolves fine and the Include would
            # add one that does not, breaking a build the redirect has no business touching.
            guard = f"Exists('$(BroilerWorkspaceRoot){canonical}')"
            lines.append(f'    <ProjectReference Remove="{spec}" Condition="{guard}" />')
            lines.append(
                f'    <ProjectReference Include="$(BroilerWorkspaceRoot){canonical}"'
                f' Condition="{guard}" />'
            )
        lines.append("  </ItemGroup>")
        lines.append("")
    lines.append("</Project>")
    rendered = "\n".join(lines) + "\n"

    # An XML comment may not contain '--'. MSBuild rejects the whole file if one slips in,
    # and because the import is Exists()-guarded the failure surfaces as every redirect
    # silently not applying rather than as an obvious error. Catch it here instead.
    for comment in re.findall(r"<!--(.*?)-->", rendered, re.S):
        if "--" in comment:
            raise SystemExit(
                "generated XML comment contains '--', which MSBuild rejects:\n"
                + comment.strip()[:200]
            )
    return rendered


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="fail if the generated file is stale")
    args = parser.parse_args()

    mirrors = discover_mirrors()
    redirects, skipped = collect(mirrors)
    rendered = render(redirects, skipped)

    if args.check:
        current = open(OUTPUT, encoding="utf-8").read() if os.path.exists(OUTPUT) else ""
        if current != rendered:
            print(f"STALE: {os.path.relpath(OUTPUT, ROOT)} does not match the workspace.")
            print("Regenerate with: python3 scripts/generate-aggregate-reference-redirects.py")
            return 1
        print(f"up to date: {sum(len(v) for v in redirects.values())} redirect(s)")
        return 0

    os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)
    with open(OUTPUT, "w", encoding="utf-8") as handle:
        handle.write(rendered)
    print(
        f"wrote {os.path.relpath(OUTPUT, ROOT)}: "
        f"{sum(len(v) for v in redirects.values())} redirect(s) across {len(redirects)} project(s)"
    )
    for reason, count in sorted(skipped.items()):
        print(f"  skipped {count:4}  {reason}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
