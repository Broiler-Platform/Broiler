#!/usr/bin/env python3
"""Merge per-shard Broiler.Wpt JSON reports into a single view.

The sharded WPT workflow runs each shard as an independent job that emits its
own ``wpt-results.json``. This script combines those shard reports to produce:

* ``--merged-json``  — aggregate summary plus the union of every shard's
  *failing* results, shaped so the C# runner's ``--rerun-json`` can consume it
  directly (top-level ``results`` array with ``relativeTestPath`` / ``passed`` /
  ``skipped`` / ``category``). This doubles as the persisted "failed tests"
  manifest that drives incremental reruns.
* ``--issue-md``     — a Markdown body summarising totals and a bounded list
  of the most common failure groups, suitable for posting as a GitHub issue.
* ``--biggest-issue-md`` — a Markdown body for a *second*, severity-focused
  issue that lists only the run's few biggest problems, ranked by blast radius:
  incomplete shards first (a whole slice went unmeasured), then crashes (one bug
  gating many tests), then the worst pixel mismatches. Each crash names an example
  gated test and the issue spells out the ``--render`` command to reproduce a
  listed test, so every entry points at its cause. This is the "what hurt most
  this run" companion to the frequency-ranked ``--issue-md`` view.

When ``--merge-into`` names an existing manifest, the run's failures are folded
into it *by scope* instead of replacing it: entries for tests this run actually
exercised (conclusive pass/fail, skips excluded) are refreshed, while entries
for tests the run never touched (outside its subset/shards) are preserved. This
lets a partial subset/shard/rerun run update its own slice of the manifest
without shrinking it, so persistence is safe on every run, not just a full-suite
one.

When ``--github-output`` is given (the ``$GITHUB_OUTPUT`` file), the script also
writes ``failed_count``, ``total_count``, ``incomplete_shard_count``,
``create_issue``, ``biggest_problem_count`` and ``create_biggest_issue`` step
outputs. The last two drive the second (biggest-problems) issue.
"""

from __future__ import annotations

import argparse
import json
import os
import re
from collections import Counter
from pathlib import Path

DEFAULT_PROBLEM_LIMIT = 10
PROBLEM_EXAMPLE_LIMIT = 3

# Where the CI workflow checks out WPT (see .github/workflows/wpt-tests.yml
# WPT_CHECKOUT_DIR and scripts/run-wpt-tests.sh). Test paths in the reports are
# relative to it, so the biggest-problems issue can spell out a ready-to-run
# ``--render`` reproduction: <checkout>/<relative-path>.
WPT_CHECKOUT_DIR = "tests/wpt/checkout"

# The second, severity-focused issue reports at most this many of the run's
# biggest problems.
DEFAULT_BIGGEST_PROBLEM_LIMIT = 3
# A pixel mismatch counts as a "low percent match" big problem only when the
# rendered output matches the reference by less than this percentage — i.e. the
# render is substantially wrong, not merely off by a hair (the pass threshold is
# 99%). Runs where every mismatch is a near-miss surface no low-match problem.
DEFAULT_LOW_MATCH_THRESHOLD = 50.0


def _bucket_directory(relative_path: str) -> str:
    """Group a test by its first two path segments (e.g. ``css/css-flexbox``)."""
    parts = [segment for segment in relative_path.split("/") if segment]
    if len(parts) <= 1:
        return parts[0] if parts else "."
    return "/".join(parts[:2])


_DIGIT_RUN = re.compile(r"\d+")


def _family_key(relative_path: str) -> str:
    """Collapse the numeric token(s) in a test's file name so a numbered family
    (e.g. ``…/static-position-1.html`` … ``-8.html``) maps to one key
    ``…/static-position-{N}.html``. Directory segments are left intact, so only
    same-directory siblings that differ purely by number cluster together."""
    directory, _, filename = relative_path.rpartition("/")
    collapsed = _DIGIT_RUN.sub("{N}", filename)
    return f"{directory}/{collapsed}" if directory else collapsed


def _iter_shard_reports(shard_dir: Path):
    # Recurse so it does not matter whether artifacts were downloaded flat or
    # into per-shard subdirectories.
    for path in sorted(shard_dir.rglob("*.json")):
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError):
            continue
        # Only consider documents that look like a Broiler.Wpt report.
        if isinstance(data, dict) and "summary" in data and "results" in data:
            yield path, data


def _iter_shard_statuses(shard_dir: Path):
    for path in sorted(shard_dir.rglob("*-status.json")):
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError):
            continue
        if isinstance(data, dict) and "shardIndex" in data and "exitCode" in data:
            yield path, data


def _problem_identity(result: dict) -> tuple[str, str, str | None]:
    """Return a stable key and label for a failure root-cause group."""
    category = str(result.get("category") or "Unknown")
    diagnostics = result.get("mismatchDiagnostics")
    sub_category = None
    if isinstance(diagnostics, dict):
        sub_category = diagnostics.get("subCategory")
        if sub_category is not None:
            sub_category = str(sub_category)

    if category == "PixelMismatch" and sub_category:
        return f"{category}:{sub_category}", f"{category} / {sub_category}", sub_category
    return category, category, None


def _rank_biggest_problems(
    incomplete_shards: list[dict],
    exception_signatures: Counter[str],
    exception_examples: dict[str, list[str]],
    low_match_tests: list[dict],
    limit: int,
    low_match_threshold: float,
) -> list[dict]:
    """Rank the run's few most severe problems, worst first.

    Unlike ``topProblems`` (which ranks by *frequency*), this ranks by *blast
    radius* across three severity tiers:

    * tier 0 — **incomplete shards** (collapsed into one entry). A shard that
      never finished leaves a whole slice of the suite unmeasured, so its
      pass/fail is unknown — the most important thing to flag, even if only one
      shard is affected.
    * tier 1 — **crashes**, one entry per exception signature, ordered by the
      number of tests that signature gated. A single engine throw commonly fails
      many tests at once (one signature → one fix).
    * tier 2 — **low percent matches**, one entry per test whose render matched
      the reference by less than ``low_match_threshold`` percent, worst match
      first.

    Selection is diversity-first: the single worst entry of each distinct kind is
    taken before any slot is spent on a second entry of a kind already shown, then
    remaining slots (if ``limit`` exceeds the number of kinds present) are filled
    by the next-worst overall. This keeps a short list representative — a lone but
    severe low match still surfaces even when crashes would otherwise fill every
    slot — while the returned list stays ordered by blast radius. A healthy-ish
    run with no incomplete shards naturally surfaces its worst crashes and
    mismatches instead.
    """
    problems: list[dict] = []

    if incomplete_shards:
        shards = sorted(incomplete_shards, key=lambda status: status["shardIndex"])
        listing = ", ".join(
            f"shard {status['shardIndex']} (exit {status['exitCode']})" for status in shards
        )
        problems.append(
            {
                "kind": "IncompleteShards",
                "tier": 0,
                "severity": len(shards),
                "impact": len(shards),
                "title": f"{len(shards)} shard(s) did not complete",
                "detail": (
                    "A whole shard's tests are unaccounted for — the pass/fail of that "
                    f"slice is unknown. {listing}."
                ),
            }
        )

    for signature, count in exception_signatures.most_common():
        problems.append(
            {
                "kind": "Crash",
                "tier": 1,
                "severity": count,
                "impact": count,
                "title": f"Crash gating {count} test(s)",
                "detail": signature,
                # Example tests that hit this crash — the reproduction the report
                # points at. May be empty for reports produced before examples
                # were emitted.
                "examples": list(exception_examples.get(signature, [])),
            }
        )

    # A test can appear once per shard at most, but dedupe defensively and keep
    # the lowest match seen for each path.
    lowest_by_path: dict[str, dict] = {}
    for test in low_match_tests:
        if test["matchPercent"] >= low_match_threshold:
            continue
        existing = lowest_by_path.get(test["relativeTestPath"])
        if existing is None or test["matchPercent"] < existing["matchPercent"]:
            lowest_by_path[test["relativeTestPath"]] = test
    for test in lowest_by_path.values():
        label = test["category"]
        if test["subCategory"]:
            label = f"{label} / {test['subCategory']}"
        problems.append(
            {
                "kind": "LowMatch",
                "tier": 2,
                "severity": 100.0 - test["matchPercent"],
                "impact": 1,
                "matchPercent": test["matchPercent"],
                "relativeTestPath": test["relativeTestPath"],
                "title": f"{test['matchPercent']:.1f}% match — {test['relativeTestPath']}",
                "detail": (
                    f"Rendered output matches the reference by only "
                    f"{test['matchPercent']:.1f}% ({label})."
                ),
            }
        )

    # Lower tier first; within a tier the larger blast radius first; then a stable
    # alphabetical tie-break on the title.
    ordered = sorted(
        problems, key=lambda problem: (problem["tier"], -problem["severity"], problem["title"])
    )

    # Diversity-first selection over the severity-ordered list: pass 1 takes the
    # worst entry of each not-yet-seen kind; pass 2 fills any leftover slots with
    # the next-worst overall. `ordered` is already globally sorted, so slicing the
    # chosen indices back out in index order keeps the result severity-ranked.
    selected_indices: list[int] = []
    seen_kinds: set[str] = set()
    for index, problem in enumerate(ordered):
        if len(selected_indices) >= limit:
            break
        if problem["kind"] not in seen_kinds:
            seen_kinds.add(problem["kind"])
            selected_indices.append(index)
    for index in range(len(ordered)):
        if len(selected_indices) >= limit:
            break
        if index not in selected_indices:
            selected_indices.append(index)

    return [ordered[index] for index in sorted(selected_indices)]


def merge(
    shard_dir: Path,
    problem_limit: int = DEFAULT_PROBLEM_LIMIT,
    biggest_problem_limit: int = DEFAULT_BIGGEST_PROBLEM_LIMIT,
    low_match_threshold: float = DEFAULT_LOW_MATCH_THRESHOLD,
) -> dict:
    passed = failed = skipped = total = 0
    shard_count = 0
    failures: list[dict] = []
    seen_failures: set[str] = set()
    directory_counter: Counter[str] = Counter()
    category_counter: Counter[str] = Counter()
    dropped_declaration_counter: Counter[str] = Counter()
    exception_signature_counter: Counter[str] = Counter()
    exception_examples: dict[str, list[str]] = {}
    low_match_tests: list[dict] = []
    problem_groups: dict[str, dict] = {}
    family_groups: dict[str, dict] = {}
    reported_shard_indexes: set[int] = set()

    for _path, report in _iter_shard_reports(shard_dir):
        shard_count += 1
        summary = report.get("summary", {})
        shard = report.get("shard")
        if isinstance(shard, dict):
            try:
                reported_shard_indexes.add(int(shard.get("index")))
            except (TypeError, ValueError):
                pass
        passed += int(summary.get("passed", 0) or 0)
        failed += int(summary.get("failed", 0) or 0)
        skipped += int(summary.get("skipped", 0) or 0)
        total += int(summary.get("total", 0) or 0)

        # CSS declarations the style engine dropped as invalid/unsupported. A
        # high cross-shard count usually points at a missing feature that
        # silently gates many tests (see issue #1100).
        triage = report.get("triage")
        if isinstance(triage, dict):
            for entry in triage.get("droppedDeclarations", []) or []:
                if not isinstance(entry, dict):
                    continue
                declaration = entry.get("declaration")
                if declaration:
                    dropped_declaration_counter[str(declaration)] += int(entry.get("count", 0) or 0)

            # Exception failures grouped by "top frame — message" signature. A high
            # cross-shard count means one crash gates many tests (issue #1100, cluster
            # 7): one signature → one fix.
            for entry in triage.get("exceptionSignatures", []) or []:
                if not isinstance(entry, dict):
                    continue
                signature = entry.get("signature")
                if signature:
                    signature = str(signature)
                    exception_signature_counter[signature] += int(entry.get("count", 0) or 0)
                    # Union the example test paths this signature gated across
                    # shards (bounded, deduped) so the biggest-problems issue can
                    # name a concrete test to --render for the crash.
                    examples = exception_examples.setdefault(signature, [])
                    for example in entry.get("examples", []) or []:
                        example = str(example)
                        if example not in examples and len(examples) < PROBLEM_EXAMPLE_LIMIT:
                            examples.append(example)

            # The worst-matching pixel comparisons this shard saw (already the N
            # lowest by matchPercent). Feeds the "low percent match" biggest-problem
            # entries — a near-0% match means the render is substantially wrong.
            for entry in triage.get("lowestMatchTests", []) or []:
                if not isinstance(entry, dict):
                    continue
                match_percent = entry.get("matchPercent")
                relative_path = entry.get("testPath") or entry.get("relativeTestPath")
                if relative_path is None or not isinstance(match_percent, (int, float)):
                    continue
                sub_category = entry.get("subCategory")
                low_match_tests.append(
                    {
                        "relativeTestPath": str(relative_path),
                        "matchPercent": float(match_percent),
                        "category": str(entry.get("category") or "Unknown"),
                        "subCategory": str(sub_category) if sub_category else None,
                    }
                )

        for result in report.get("results", []):
            if not isinstance(result, dict):
                continue
            if result.get("passed") or result.get("skipped"):
                continue

            relative_path = result.get("relativeTestPath") or result.get("testPath") or ""
            if not relative_path or relative_path in seen_failures:
                continue
            seen_failures.add(relative_path)

            category = str(result.get("category") or "Unknown")
            problem_key, problem_label, sub_category = _problem_identity(result)
            failure = {
                "relativeTestPath": relative_path,
                "passed": False,
                "skipped": False,
                "category": category,
            }
            # Preserve the pixel-mismatch sub-category so a whole cluster (e.g. all
            # LayoutShift tests) can be enumerated from the merged artifact, not just
            # the 3 example paths in topProblems (#10). Additive and only present when
            # there is one; --rerun-json ignores unknown keys.
            if sub_category:
                failure["subCategory"] = sub_category
            failures.append(failure)
            directory_counter[_bucket_directory(relative_path)] += 1
            category_counter[category] += 1

            group = problem_groups.setdefault(
                problem_key,
                {
                    "key": problem_key,
                    "label": problem_label,
                    "category": category,
                    "subCategory": sub_category,
                    "count": 0,
                    "examples": [],
                },
            )
            group["count"] += 1
            if len(group["examples"]) < PROBLEM_EXAMPLE_LIMIT:
                group["examples"].append(relative_path)

            # Cluster numbered families (…-1.html … -8.html) into one row,
            # cross-tabbed by category, so they collapse from N scattered lines.
            family = _family_key(relative_path)
            family_group = family_groups.setdefault(
                family,
                {
                    "family": family,
                    "count": 0,
                    "categories": Counter(),
                    "examples": [],
                },
            )
            family_group["count"] += 1
            family_group["categories"][category] += 1
            if len(family_group["examples"]) < PROBLEM_EXAMPLE_LIMIT:
                family_group["examples"].append(relative_path)

    incomplete_shards = []
    for _path, status in _iter_shard_statuses(shard_dir):
        try:
            shard_index = int(status["shardIndex"])
            exit_code = int(status["exitCode"])
        except (TypeError, ValueError):
            continue
        if exit_code == 0 or shard_index in reported_shard_indexes:
            continue
        incomplete_shards.append({"shardIndex": shard_index, "exitCode": exit_code})

    if incomplete_shards:
        incomplete_shards.sort(key=lambda status: status["shardIndex"])
        problem_groups["ShardProcessError"] = {
            "key": "ShardProcessError",
            "label": "Shard process failure",
            "category": "ShardProcessError",
            "subCategory": None,
            "count": len(incomplete_shards),
            "examples": [
                f"shard {status['shardIndex']} (exit {status['exitCode']})"
                for status in incomplete_shards[:PROBLEM_EXAMPLE_LIMIT]
            ],
        }

    failures.sort(key=lambda item: item["relativeTestPath"])

    top_problems = sorted(
        problem_groups.values(),
        key=lambda group: (-group["count"], group["label"]),
    )[:problem_limit]

    # Only families that actually clustered (≥2 members) are worth a row; a lone
    # numbered test is already covered by the per-test results list.
    top_families = sorted(
        (
            {
                "family": group["family"],
                "count": group["count"],
                "categories": dict(group["categories"].most_common()),
                "examples": group["examples"],
            }
            for group in family_groups.values()
            if group["count"] >= 2
        ),
        key=lambda group: (-group["count"], group["family"]),
    )[:problem_limit]

    biggest_problems = _rank_biggest_problems(
        incomplete_shards,
        exception_signature_counter,
        exception_examples,
        low_match_tests,
        biggest_problem_limit,
        low_match_threshold,
    )

    return {
        "summary": {
            "passed": passed,
            "failed": failed,
            "skipped": skipped,
            "total": total,
        },
        "shardCount": shard_count,
        "problemLimit": problem_limit,
        "incompleteShards": incomplete_shards,
        "topProblems": top_problems,
        "topFailingDirectories": directory_counter.most_common(problem_limit),
        "failuresByCategory": category_counter.most_common(),
        "droppedDeclarations": dropped_declaration_counter.most_common(problem_limit),
        "exceptionSignatures": exception_signature_counter.most_common(problem_limit),
        "failureFamilies": top_families,
        "biggestProblemLimit": biggest_problem_limit,
        "lowMatchThreshold": low_match_threshold,
        "biggestProblems": biggest_problems,
        "results": failures,
    }


def _collect_executed_paths(shard_dir: Path) -> set[str]:
    """Relative paths of tests that produced a conclusive pass/fail verdict in
    this run. Skips are inconclusive (e.g. a missing reference image) and are
    deliberately excluded, so a test that merely skipped this run does not evict
    its existing manifest entry. Used to scope ``merge_into_manifest``."""
    executed: set[str] = set()
    for _path, report in _iter_shard_reports(shard_dir):
        for result in report.get("results", []):
            if not isinstance(result, dict) or result.get("skipped"):
                continue
            relative_path = result.get("relativeTestPath") or result.get("testPath") or ""
            if relative_path:
                executed.add(relative_path)
    return executed


def _load_manifest_results(path: Path) -> list[dict]:
    """Return the ``results`` array of an existing manifest, or ``[]`` when the
    file is absent or unreadable (e.g. the first run, before one exists)."""
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, OSError):
        return []
    results = data.get("results") if isinstance(data, dict) else None
    if not isinstance(results, list):
        return []
    return [entry for entry in results if isinstance(entry, dict)]


def merge_into_manifest(merged: dict, executed: set[str], existing_path: Path) -> dict:
    """Fold this run's failures into an existing manifest by scope.

    Existing entries for tests this run exercised are dropped (superseded by the
    fresh verdict — re-added below only if they still fail); entries for tests
    the run never touched are kept; then this run's failures are added. The net
    effect: a partial run refreshes its own slice and leaves the rest intact.
    """
    kept = [
        entry
        for entry in _load_manifest_results(existing_path)
        if (entry.get("relativeTestPath") or entry.get("testPath") or "") not in executed
    ]
    # New failures come last so they win any (normally impossible) key collision
    # with a kept entry.
    by_path: dict[str, dict] = {}
    for entry in kept + merged["results"]:
        key = entry.get("relativeTestPath") or entry.get("testPath") or ""
        if key:
            by_path[key] = entry
    results = sorted(by_path.values(), key=lambda item: item.get("relativeTestPath") or "")

    merged = dict(merged)
    merged["results"] = results
    # The manifest is a list of known failures; its summary describes that list,
    # not the (unknown, corpus-wide) pass/total of any single run.
    merged["summary"] = {"passed": 0, "failed": len(results), "skipped": 0, "total": len(results)}
    return merged


def render_issue_markdown(merged: dict, run_url: str | None) -> str:
    summary = merged["summary"]
    lines = [
        "## WPT run — failing tests",
        "",
        f"- Shards merged: {merged['shardCount']}",
        f"- Total: {summary['total']}",
        f"- Passed: {summary['passed']}",
        f"- Failed: {summary['failed']}",
        f"- Skipped: {summary['skipped']}",
        f"- Incomplete shards: {len(merged['incompleteShards'])}",
        "",
        f"### Top {merged['problemLimit']} problems",
        "",
    ]
    if merged["topProblems"]:
        for index, problem in enumerate(merged["topProblems"], start=1):
            lines.append(f"{index}. `{problem['label']}` — {problem['count']} failure(s)")
            if problem["examples"]:
                examples = ", ".join(f"`{path}`" for path in problem["examples"])
                lines.append(f"   - Examples: {examples}")
    else:
        lines.append("- None")

    lines += ["", f"### Top {merged['problemLimit']} failing directories", ""]
    if merged["topFailingDirectories"]:
        lines += [f"- `{directory}` — {count} failure(s)" for directory, count in merged["topFailingDirectories"]]
    else:
        lines.append("- None")

    # Silently-dropped CSS declarations: a high count usually means a single
    # unsupported value is gating many tests (e.g. text-align:-webkit-right).
    dropped = merged.get("droppedDeclarations") or []
    if dropped:
        lines += [
            "",
            f"### Top {merged['problemLimit']} dropped CSS declarations",
            "",
            "_Values the style engine rejected as invalid/unsupported. A high count"
            " often points at a missing feature gating many tests._",
            "",
        ]
        lines += [f"- `{declaration}` — {count} occurrence(s)" for declaration, count in dropped]

    # Exception failures grouped by signature: one high-count signature usually
    # means a single crash (e.g. a DOM constructor throw) gates many tests.
    exceptions = merged.get("exceptionSignatures") or []
    if exceptions:
        lines += [
            "",
            f"### Top {merged['problemLimit']} exception signatures",
            "",
            "_Exception failures grouped by top non-framework frame and message. A high"
            " count usually means one crash gates many tests (one signature → one fix)._",
            "",
        ]
        lines += [f"- `{signature}` — {count} failure(s)" for signature, count in exceptions]

    # Numbered test families collapsed into one row, cross-tabbed by category, so a
    # ``*-static-position-{1..8}`` cluster reads as one line instead of eight.
    families = merged.get("failureFamilies") or []
    if families:
        lines += [
            "",
            f"### Top {merged['problemLimit']} failure families",
            "",
            "_Numbered test families (e.g. `…-{N}.html`) collapsed into one row, with a"
            " per-category breakdown._",
            "",
        ]
        for family in families:
            breakdown = ", ".join(
                f"{category} {count}" for category, count in family["categories"].items()
            )
            lines.append(f"- `{family['family']}` — {family['count']} failure(s) ({breakdown})")

    lines += [
        "",
        "### CI metadata",
        f"- Workflow run: {run_url}" if run_url else "- Workflow run: (unknown)",
        "- Artifact: `wpt-merged`",
        "",
        "_Auto-generated by `.github/workflows/wpt-tests.yml`. The full per-shard"
        " logs and the rerun manifest are attached to the run artifacts._",
    ]
    return "\n".join(lines) + "\n"


def render_biggest_problems_markdown(merged: dict, run_url: str | None) -> str:
    """Render the body of the second, severity-focused issue: the run's few
    biggest problems ranked by blast radius (see ``_rank_biggest_problems``)."""
    problems = merged.get("biggestProblems") or []
    threshold = merged.get("lowMatchThreshold", DEFAULT_LOW_MATCH_THRESHOLD)
    summary = merged["summary"]
    lines = [
        f"## WPT run — top {len(problems)} biggest problem(s)",
        "",
        "_The run's most severe issues, ranked by blast radius rather than"
        " frequency: incomplete shards first (a whole slice went unmeasured), then"
        " crashes (one bug gating many tests), then the worst pixel mismatches"
        f" (< {threshold:g}% match). Companion to the most-common-failures issue._",
        "",
        f"- Failed: {summary['failed']}",
        f"- Incomplete shards: {len(merged['incompleteShards'])}",
        "",
        "### Biggest problems",
        "",
    ]
    # Concrete test paths a maintainer can render to reproduce, in report order:
    # a crash's example test(s), a low match's own path. Feeds the reproduce hint.
    repro_paths: list[str] = []
    if problems:
        for index, problem in enumerate(problems, start=1):
            lines.append(f"{index}. **{problem['title']}**")
            if problem["kind"] == "Crash":
                lines.append(f"   - Signature: `{problem['detail']}`")
                examples = problem.get("examples") or []
                if examples:
                    rendered = ", ".join(f"`{path}`" for path in examples)
                    lines.append(f"   - Example test(s): {rendered}")
                    repro_paths.extend(examples)
            elif problem.get("detail"):
                lines.append(f"   - {problem['detail']}")
                if problem["kind"] == "LowMatch" and problem.get("relativeTestPath"):
                    repro_paths.append(problem["relativeTestPath"])
    else:
        lines.append(
            "- None — no incomplete shards, crashes, or sub-threshold pixel matches."
        )

    # Point at the fix: spell out the exact command to render a listed test against
    # the live engine. Only when a listed problem has a concrete test path — an
    # incomplete-shard-only run has nothing single-test to render.
    if repro_paths:
        first = repro_paths[0]
        lines += [
            "",
            "### Reproduce locally",
            "",
            "_Render a listed test against the live engine to watch the failure happen"
            " — e.g. the first one. Swap in any `Example test(s)` path above._",
            "",
            "```sh",
            "dotnet run --project src/Broiler.Wpt -- \\",
            f"  --wpt-dir {WPT_CHECKOUT_DIR} --render {WPT_CHECKOUT_DIR}/{first}",
            "```",
        ]

    lines += [
        "",
        "### CI metadata",
        f"- Workflow run: {run_url}" if run_url else "- Workflow run: (unknown)",
        "- Artifact: `wpt-merged`",
        "",
        "_Auto-generated by `.github/workflows/wpt-tests.yml`. See the companion"
        " most-common-failures issue for the full frequency breakdown._",
    ]
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--shard-dir", required=True, type=Path, help="Directory containing per-shard JSON reports")
    parser.add_argument("--merged-json", type=Path, help="Where to write the merged report / rerun manifest")
    parser.add_argument(
        "--merge-into",
        type=Path,
        help="Existing manifest to fold this run's failures into by scope (preserving "
        "entries for tests the run did not exercise) instead of replacing it",
    )
    parser.add_argument("--issue-md", type=Path, help="Where to write the Markdown issue body")
    parser.add_argument(
        "--biggest-issue-md",
        type=Path,
        help="Where to write the Markdown body for the second, biggest-problems issue",
    )
    parser.add_argument(
        "--problem-limit",
        type=int,
        default=DEFAULT_PROBLEM_LIMIT,
        help=f"Maximum common failure groups/directories to report (default: {DEFAULT_PROBLEM_LIMIT})",
    )
    parser.add_argument(
        "--biggest-problem-limit",
        type=int,
        default=DEFAULT_BIGGEST_PROBLEM_LIMIT,
        help="Maximum biggest problems to report in the second issue "
        f"(default: {DEFAULT_BIGGEST_PROBLEM_LIMIT})",
    )
    parser.add_argument(
        "--low-match-threshold",
        type=float,
        default=DEFAULT_LOW_MATCH_THRESHOLD,
        help="A pixel match below this percent counts as a 'low percent match' big "
        f"problem (default: {DEFAULT_LOW_MATCH_THRESHOLD:g})",
    )
    parser.add_argument("--run-url", default=os.environ.get("WPT_RUN_URL"), help="Workflow run URL for the issue footer")
    parser.add_argument("--github-output", type=Path, help="Path to $GITHUB_OUTPUT for step outputs")
    args = parser.parse_args()

    if args.problem_limit < 1:
        parser.error("--problem-limit must be a positive integer")
    if args.biggest_problem_limit < 1:
        parser.error("--biggest-problem-limit must be a positive integer")
    if not 0 <= args.low_match_threshold <= 100:
        parser.error("--low-match-threshold must be between 0 and 100")

    merged = merge(
        args.shard_dir,
        args.problem_limit,
        args.biggest_problem_limit,
        args.low_match_threshold,
    )

    if args.merge_into:
        # Read the existing manifest before any write below (the same path may be
        # both --merge-into and --merged-json).
        merged = merge_into_manifest(merged, _collect_executed_paths(args.shard_dir), args.merge_into)

    if args.merged_json:
        args.merged_json.parent.mkdir(parents=True, exist_ok=True)
        args.merged_json.write_text(json.dumps(merged, indent=2) + "\n", encoding="utf-8")

    if args.issue_md:
        args.issue_md.parent.mkdir(parents=True, exist_ok=True)
        args.issue_md.write_text(render_issue_markdown(merged, args.run_url), encoding="utf-8")

    if args.biggest_issue_md:
        args.biggest_issue_md.parent.mkdir(parents=True, exist_ok=True)
        args.biggest_issue_md.write_text(
            render_biggest_problems_markdown(merged, args.run_url), encoding="utf-8"
        )

    failed = merged["summary"]["failed"]
    total = merged["summary"]["total"]
    incomplete_shard_count = len(merged["incompleteShards"])
    biggest_problem_count = len(merged.get("biggestProblems") or [])
    print(f"Merged {merged['shardCount']} shard(s): {merged['summary']['passed']} passed, "
          f"{failed} failed, {merged['summary']['skipped']} skipped, {total} total.")

    if args.github_output:
        with args.github_output.open("a", encoding="utf-8") as handle:
            handle.write(f"failed_count={failed}\n")
            handle.write(f"total_count={total}\n")
            handle.write(f"incomplete_shard_count={incomplete_shard_count}\n")
            handle.write(f"create_issue={'true' if failed > 0 or incomplete_shard_count > 0 else 'false'}\n")
            handle.write(f"biggest_problem_count={biggest_problem_count}\n")
            handle.write(f"create_biggest_issue={'true' if biggest_problem_count > 0 else 'false'}\n")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
