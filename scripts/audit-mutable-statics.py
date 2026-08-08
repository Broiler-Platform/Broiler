#!/usr/bin/env python3
"""Enumerate mutable static state on the render path.

Multithreading roadmap item P0-c asks for "a documented list" of the mutable statics
a parallel render would reach, each classified as immutable / thread-static /
needs-synchronization. This produces the list; the classification and the
per-site judgements live in docs/architecture/multithreading-static-state.md.

Why a script rather than a one-off grep pasted into the document: the list is a
moving target, and a document that cannot be re-derived stops being checkable the
first time somebody adds a cache. Run this to re-derive it.

What counts as mutable static state here:

  * a `static` field that is neither `const` nor `readonly` — assignable after
    initialisation, so two threads can race the assignment; and
  * a `static readonly` field of a *mutable collection type* — the reference cannot
    move but the contents can, which is the shape that actually corrupts
    (`static readonly Dictionary<..>` is the FontsHandler defect verbatim).

`[ThreadStatic]` fields are reported separately: they are not a race, they are an
ambient-state contract a worker thread has to establish before it may run layout or
paint. Missing one is silent and reads as another document's state.

Deliberately syntactic. A Roslyn pass would be more precise, but this has to run on
a bare checkout with no build, and precision is not what the audit needs — it needs
every candidate surfaced so a human can classify it. False positives are cheap here;
a missed cache is not.

Usage:
    python3 scripts/audit-mutable-statics.py [--json OUT] [ROOT ...]
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from collections import defaultdict

# The assemblies a headless render reaches, in pipeline order. Paths are relative to
# the repository root. Test projects are excluded by EXCLUDED_DIR_PARTS below: a
# static in a test fixture is not on the render path.
RENDER_PATH_ROOTS = [
    ("Broiler.DOM/Broiler.Dom", "DOM"),
    ("Broiler.DOM/Broiler.Dom.Html", "DOM"),
    ("Broiler.CSS/Broiler.CSS", "CSS"),
    ("Broiler.CSS/Broiler.CSS.Dom", "CSS"),
    ("Broiler.Layout/Broiler.Layout", "Layout"),
    ("Broiler.Graphics/Broiler.Graphics", "Graphics"),
    ("Broiler.Media/Broiler.Media.Image", "Media"),
    ("Broiler.Media/Broiler.Media.Image.Managed", "Media"),
    ("Broiler.HTML/Source", "HTML"),
]

EXCLUDED_DIR_PARTS = ("/bin/", "/obj/", ".Tests/", "/Tests/", "/benchmarks/", "/OtherTests/")

# A static field declaration, with its modifiers. Deliberately anchored to the start
# of a line (after indentation) so a `static` inside an expression is not matched.
FIELD_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    # The whole modifier run in one group. A repeated capture keeps only its LAST
    # repetition, so `(?P<mods>(?:static|readonly|...)\s+)+` would report
    # `private static readonly Foo` as just `readonly` and drop every multi-modifier
    # declaration on the floor — which is most of them.
    r"(?P<mods>(?:(?:public|private|protected|internal|static|readonly|volatile|unsafe|extern|new|const|required)\s+)+)"
    r"(?P<type>[A-Za-z_][A-Za-z0-9_.]*(?:\s*<[^;=()]*>)?(?:\[\s*[,]*\s*\])?\??)\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*"
    r"(?P<tail>[;=].*)$"
)

# Types whose contents are mutable even behind a `readonly` reference. Matched on the
# open generic / bare name, so `Dictionary<string, Foo>` and `Dictionary` both hit.
MUTABLE_CONTAINERS = (
    "Dictionary", "List", "HashSet", "SortedDictionary", "SortedSet", "Queue",
    "Stack", "LinkedList", "ArrayList", "Hashtable", "StringBuilder",
)

# Types that are already synchronised. Reported, because "already concurrent" is a
# classification the audit has to make explicitly rather than by omission.
CONCURRENT_CONTAINERS = (
    "ConcurrentDictionary", "ConcurrentBag", "ConcurrentQueue", "ConcurrentStack",
    "BlockingCollection", "ImmutableDictionary", "ImmutableArray", "ImmutableList",
    "ImmutableHashSet", "FrozenDictionary", "FrozenSet",
)


# Value and near-value types whose static holder is a scalar, not a shared object
# graph. A `static readonly int` is not a root; a `static readonly RAdapter` is.
SCALAR_TYPES = {
    "bool", "byte", "sbyte", "char", "short", "ushort", "int", "uint", "long",
    "ulong", "float", "double", "decimal", "string", "object", "Guid", "TimeSpan",
    "DateTime", "DateTimeOffset", "Type", "Encoding", "Regex", "CultureInfo",
}


def classify(mods: str, type_name: str, attributes: str) -> str | None:
    """Returns the audit bucket for one field, or None when it is not mutable state."""
    if "static" not in mods:
        return None
    if "const" in mods:
        return None

    if "ThreadStatic" in attributes:
        return "thread-static"

    bare = type_name.split("<")[0].split(".")[-1].rstrip("?")

    if bare in CONCURRENT_CONTAINERS:
        return "concurrent-container"
    if bare in MUTABLE_CONTAINERS:
        return "mutable-container"

    if "readonly" in mods:
        # A readonly reference to a non-scalar is a SHARED INSTANCE ROOT: the reference
        # cannot move, but every mutable field of the object it names is process-wide
        # state reachable from every thread. This bucket exists because the roadmap's
        # own headline example is one — `FontsHandler`'s four caches are *instance*
        # fields on the singleton `RAdapter` behind `CompatProvider.ImageAdapter`, so a
        # scan that only looked for `static` fields would report item #9 as absent.
        if bare in SCALAR_TYPES:
            return None
        return "shared-instance-root"

    return "assignable"


def attributes_above(lines: list[str], index: int) -> str:
    """Collects attribute lines immediately preceding a declaration."""
    collected = []
    i = index - 1
    while i >= 0:
        stripped = lines[i].strip()
        if stripped.startswith("[") and stripped.endswith("]"):
            collected.append(stripped)
            i -= 1
            continue
        if stripped.startswith("///") or stripped.startswith("//") or not stripped:
            i -= 1
            continue
        break
    return " ".join(collected)


def scan_file(path: str) -> list[dict]:
    try:
        with open(path, encoding="utf-8-sig", errors="replace") as handle:
            lines = handle.read().splitlines()
    except OSError:
        return []

    found = []
    for number, line in enumerate(lines):
        match = FIELD_RE.match(line)
        if not match:
            continue

        tail = match.group("tail").lstrip()
        # `static Foo Bar => ...` is an expression-bodied property, not a field.
        if tail.startswith("=>"):
            continue
        # `public static bool operator ==(..)` parses as type `bool`, name `operator`,
        # tail `==(..)`. Both halves of the guard are needed: `operator` alone misses
        # `operator !=`, and the tail alone misses nothing but is the cheaper check.
        if match.group("name") == "operator" or tail.startswith("=="):
            continue

        mods = match.group("mods")
        inline_attributes = ""
        attribute_match = re.match(r"^\s*((?:\[[^\]]*\]\s*)+)", line)
        if attribute_match:
            inline_attributes = attribute_match.group(1)

        attributes = inline_attributes + " " + attributes_above(lines, number)
        bucket = classify(mods, match.group("type"), attributes)
        if bucket is None:
            continue

        found.append({
            "file": path,
            "line": number + 1,
            "bucket": bucket,
            "name": match.group("name"),
            "type": match.group("type"),
            "declaration": line.strip(),
        })

    return found


def walk(root: str) -> list[str]:
    paths = []
    for directory, _, files in os.walk(root):
        normalised = directory.replace(os.sep, "/") + "/"
        if any(part in normalised for part in EXCLUDED_DIR_PARTS):
            continue
        for name in files:
            if name.endswith(".cs") and not name.endswith(".g.cs"):
                paths.append(os.path.join(directory, name))
    return sorted(paths)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", help="write the full finding list here")
    parser.add_argument("roots", nargs="*", help="override the render-path roots")
    args = parser.parse_args()

    roots = [(r, r) for r in args.roots] if args.roots else RENDER_PATH_ROOTS

    findings = []
    for root, component in roots:
        if not os.path.isdir(root):
            print(f"skipped (absent): {root}", file=sys.stderr)
            continue
        for path in walk(root):
            for finding in scan_file(path):
                finding["component"] = component
                findings.append(finding)

    by_component = defaultdict(lambda: defaultdict(int))
    files_by_component = defaultdict(set)
    for finding in findings:
        by_component[finding["component"]][finding["bucket"]] += 1
        files_by_component[finding["component"]].add(finding["file"])

    buckets = ["assignable", "mutable-container", "concurrent-container", "shared-instance-root", "thread-static"]
    print("| Component | files | assignable | mutable container | concurrent container | shared instance root | thread-static |")
    print("|---|---:|---:|---:|---:|---:|---:|")
    for component in sorted(by_component):
        counts = by_component[component]
        print(f"| {component} | {len(files_by_component[component])} | "
              + " | ".join(str(counts.get(b, 0)) for b in buckets) + " |")

    totals = defaultdict(int)
    for counts in by_component.values():
        for bucket, count in counts.items():
            totals[bucket] += count
    print(f"| **total** | {len({f['file'] for f in findings})} | "
          + " | ".join(str(totals.get(b, 0)) for b in buckets) + " |")

    if args.json:
        with open(args.json, "w", encoding="utf-8") as handle:
            json.dump(findings, handle, indent=2)
            handle.write("\n")
        print(f"\nWrote {args.json} ({len(findings)} findings).", file=sys.stderr)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
