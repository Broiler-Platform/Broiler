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

When ``--github-output`` is given (the ``$GITHUB_OUTPUT`` file), the script also
writes ``failed_count``, ``total_count``, ``incomplete_shard_count`` and
``create_issue`` step outputs.
"""

from __future__ import annotations

import argparse
import json
import os
from collections import Counter
from pathlib import Path

DEFAULT_PROBLEM_LIMIT = 10
PROBLEM_EXAMPLE_LIMIT = 3


def _bucket_directory(relative_path: str) -> str:
    """Group a test by its first two path segments (e.g. ``css/css-flexbox``)."""
    parts = [segment for segment in relative_path.split("/") if segment]
    if len(parts) <= 1:
        return parts[0] if parts else "."
    return "/".join(parts[:2])


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


def merge(shard_dir: Path, problem_limit: int = DEFAULT_PROBLEM_LIMIT) -> dict:
    passed = failed = skipped = total = 0
    shard_count = 0
    failures: list[dict] = []
    seen_failures: set[str] = set()
    directory_counter: Counter[str] = Counter()
    category_counter: Counter[str] = Counter()
    dropped_declaration_counter: Counter[str] = Counter()
    problem_groups: dict[str, dict] = {}
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
            failure = {
                "relativeTestPath": relative_path,
                "passed": False,
                "skipped": False,
                "category": category,
            }
            failures.append(failure)
            directory_counter[_bucket_directory(relative_path)] += 1
            category_counter[category] += 1

            problem_key, problem_label, sub_category = _problem_identity(result)
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
        "results": failures,
    }


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


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--shard-dir", required=True, type=Path, help="Directory containing per-shard JSON reports")
    parser.add_argument("--merged-json", type=Path, help="Where to write the merged report / rerun manifest")
    parser.add_argument("--issue-md", type=Path, help="Where to write the Markdown issue body")
    parser.add_argument(
        "--problem-limit",
        type=int,
        default=DEFAULT_PROBLEM_LIMIT,
        help=f"Maximum common failure groups/directories to report (default: {DEFAULT_PROBLEM_LIMIT})",
    )
    parser.add_argument("--run-url", default=os.environ.get("WPT_RUN_URL"), help="Workflow run URL for the issue footer")
    parser.add_argument("--github-output", type=Path, help="Path to $GITHUB_OUTPUT for step outputs")
    args = parser.parse_args()

    if args.problem_limit < 1:
        parser.error("--problem-limit must be a positive integer")

    merged = merge(args.shard_dir, args.problem_limit)

    if args.merged_json:
        args.merged_json.parent.mkdir(parents=True, exist_ok=True)
        args.merged_json.write_text(json.dumps(merged, indent=2) + "\n", encoding="utf-8")

    if args.issue_md:
        args.issue_md.parent.mkdir(parents=True, exist_ok=True)
        args.issue_md.write_text(render_issue_markdown(merged, args.run_url), encoding="utf-8")

    failed = merged["summary"]["failed"]
    total = merged["summary"]["total"]
    incomplete_shard_count = len(merged["incompleteShards"])
    print(f"Merged {merged['shardCount']} shard(s): {merged['summary']['passed']} passed, "
          f"{failed} failed, {merged['summary']['skipped']} skipped, {total} total.")

    if args.github_output:
        with args.github_output.open("a", encoding="utf-8") as handle:
            handle.write(f"failed_count={failed}\n")
            handle.write(f"total_count={total}\n")
            handle.write(f"incomplete_shard_count={incomplete_shard_count}\n")
            handle.write(f"create_issue={'true' if failed > 0 or incomplete_shard_count > 0 else 'false'}\n")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
