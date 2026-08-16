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
  gating many tests), then the worst pixel mismatches. A pixel mismatch counts as
  a "low percent match" problem only when it renders below the low-match
  threshold (50% by default); when too few mismatches clear that bar to fill the
  issue, the threshold is widened in 10-point steps until it does (or nothing
  more can be found). Severity is read from the test's score against the
  reference *it itself declares* whenever ``--verify-reference`` measured one,
  and from the committed golden only when it did not — see
  ``_engine_gap_percent``. A mismatch the runner flagged with
  ``suspectReference`` (Broiler reproduces the test's own reference outright, so
  the committed golden is the outlier) is never ranked, and one whose own
  reference says it is mostly right is ranked accordingly rather than by the
  golden; both are listed under their own heading instead, so a test that is
  correct by its own reference stops being reported as the run's worst render.
  Each crash names an
  example gated test and the issue spells
  out the ``--render`` command to reproduce a listed test, so every entry points
  at its cause. This is the "what hurt most this run" companion to the
  frequency-ranked ``--issue-md`` view.
* ``--timeout-issue-md`` — a Markdown body for a *third* issue listing only the
  tests the runner had to abort at the per-test timeout, ranked by the size of
  the test's own source, **smallest first**. A timeout is not a mismatch you can
  read a percentage off, so neither of the other two issues ranks it usefully:
  the frequency view collapses every timeout into one ``Timeout`` row, and the
  severity view ranks on pixel scores a timed-out test never produced. Source
  size is the signal that separates them, and it separates them the counter-
  intuitive way round: the *less* there is in a document, the less of it can be
  legitimately slow, so the likelier its timeout is an engine hang — a layout
  loop that never terminates, a parser that never advances — rather than a heavy
  page that merely wants more than the budget. A 30s timeout on a 40 MB stress
  test says the budget is tight; the same timeout on a 400-byte document says
  something does not terminate, and that bug usually gates far more than the one
  test. The issue also clusters the timeouts by directory, because a directory
  full of them is one hang gating a feature area rather than N problems.

When ``--merge-into`` names an existing manifest, the run's failures are folded
into it *by scope* instead of replacing it: entries for tests this run actually
exercised (conclusive pass/fail, skips excluded) are refreshed, while entries
for tests the run never touched (outside its subset/shards) are preserved. This
lets a partial subset/shard/rerun run update its own slice of the manifest
without shrinking it, so persistence is safe on every run, not just a full-suite
one.

A shard that aborts abnormally (runner eviction, a browser it could not
download, a hard crash) is rerun once at the end of the workflow, and uploads
its results with a ``-retry`` suffix alongside the aborted attempt's. Both land
in the same directory, so the two are reconciled here: for each shard only the
latest attempt is merged, meaning a retry *replaces* the attempt it repeats
rather than being summed with it. ``retriedShards`` records which shards that
happened to.

When ``--github-output`` is given (the ``$GITHUB_OUTPUT`` file), the script also
writes ``failed_count``, ``total_count``, ``incomplete_shard_count``,
``create_issue``, ``biggest_problem_count``, ``create_biggest_issue``,
``timeout_count``, ``create_timeout_issue``, ``incomplete_shard_indexes``,
``incomplete_shard_matrix``, ``has_incomplete_shards`` and
``retried_shard_count`` step outputs. ``create_(biggest_|timeout_)issue`` drive
the three issues; the ``incomplete_shard_*`` trio drives the retry pass, which
re-dispatches exactly the shards flagged incomplete.
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
# 99%). This is only the *starting* cut-off: when too few mismatches fall below
# it to fill the issue, it is widened (see below).
DEFAULT_LOW_MATCH_THRESHOLD = 50.0
# When the sub-threshold mismatches — together with the incomplete-shard and
# crash entries — don't fill the biggest-problems issue to its limit, the
# low-match threshold is widened by this many percentage points and the ranking
# retried, repeating up to the ceiling. So a run whose only mismatches are near
# misses still surfaces its worst renders instead of an empty severity issue.
LOW_MATCH_THRESHOLD_STEP = 10.0
LOW_MATCH_THRESHOLD_CEILING = 100.0

# The third, timeout-only issue lists at most this many timed-out tests, smallest
# source first. Ten is enough to show the whole small-file head of the ranking —
# past that the entries are large documents whose timeout says little.
DEFAULT_TIMEOUT_LIMIT = 10

#: Source-size bands used to label a timeout's severity, as
#: ``(upper bound in bytes, label)`` ordered by ascending bound; anything above
#: the last bound gets ``TIMEOUT_LARGE_LABEL``. The bounds are deliberately
#: coarse — the ranking is the file size itself, and these only exist so a reader
#: does not have to hold "is 3 KiB small for a WPT test?" in their head. For
#: calibration: a WPT test that is nothing but a fragment of markup plus a
#: stylesheet link lands around 0.5–2 KiB, and one carrying its own script
#: harness around 4–8 KiB.
TIMEOUT_SIZE_BANDS = (
    (2 * 1024, "critical"),
    (8 * 1024, "high"),
    (32 * 1024, "medium"),
)
TIMEOUT_LARGE_LABEL = "low"
#: Label for a timeout whose source size could not be read. It is not a severity
#: — nothing is known — so it sorts last rather than anywhere on the scale.
TIMEOUT_UNKNOWN_LABEL = "unranked"


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


# The WPT workflow's retry pass re-runs, at the end of the run, any shard that
# aborted abnormally, and uploads that attempt's files with this suffix
# (``wpt-shard-3-retry.json`` beside the original ``wpt-shard-3.json``). Both
# attempts therefore land in the same flattened download directory, and the
# suffix is the only thing distinguishing them — the runner's own report has no
# notion of attempts. Reading it back here is what lets a retry *supersede* the
# attempt it replaces instead of being summed alongside it, which would
# double-count a shard that uploaded a partial report before dying.
RETRY_SUFFIX = "-retry"
STATUS_SUFFIX = "-status"


def _attempt_number(path: Path) -> int:
    """Which pass a shard artifact came from: 1 for the end-of-workflow retry, 0
    for the original attempt."""
    stem = path.stem
    if stem.endswith(STATUS_SUFFIX):
        stem = stem[: -len(STATUS_SUFFIX)]
    return 1 if stem.endswith(RETRY_SUFFIX) else 0


def _report_shard_index(report: dict) -> int | None:
    shard = report.get("shard")
    if not isinstance(shard, dict):
        return None
    try:
        return int(shard.get("index"))
    except (TypeError, ValueError):
        return None


def _status_shard_index(status: dict) -> int | None:
    try:
        return int(status["shardIndex"])
    except (KeyError, TypeError, ValueError):
        return None


def _select_latest_attempts(entries, index_of):
    """Collapse per-shard artifacts down to the latest attempt of each shard.

    ``entries`` is an iterable of ``(path, payload)``. When the retry pass reran
    a shard, both attempts are present; only the retry describes what that slice
    actually did, so the earlier attempt is dropped rather than merged next to
    it. Payloads whose shard index cannot be read are all kept — there is nothing
    to supersede them with, and dropping them would lose results.

    Returns ``(entries, retried_shard_indexes)``, the second being the shards a
    retry attempt actually superseded.
    """
    latest: dict[int, tuple[int, Path, dict]] = {}
    kept: list[tuple[Path, dict]] = []
    retried: set[int] = set()
    for path, payload in entries:
        shard_index = index_of(payload)
        if shard_index is None:
            kept.append((path, payload))
            continue
        attempt = _attempt_number(path)
        if attempt > 0:
            retried.add(shard_index)
        current = latest.get(shard_index)
        if current is None or attempt >= current[0]:
            latest[shard_index] = (attempt, path, payload)
    kept.extend((path, payload) for _attempt, path, payload in latest.values())
    return kept, retried


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


def _shard_reports(shard_dir: Path):
    """Shard reports with each retried shard represented by its retry only."""
    entries, _retried = _select_latest_attempts(_iter_shard_reports(shard_dir), _report_shard_index)
    return entries


def _iter_shard_statuses(shard_dir: Path):
    for path in sorted(shard_dir.rglob("*-status.json")):
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError):
            continue
        if isinstance(data, dict) and "shardIndex" in data and "exitCode" in data:
            yield path, data


# Human-readable labels for the failure reasons a shard can record before it runs
# a single test (written by scripts/lib/wpt-browser-install.sh). A shard reporting
# one of these went unmeasured for an infrastructure reason, not an engine one.
FAILURE_REASON_LABELS = {
    "BrowserDownloadBlocked": "Chromium download refused",
    "BrowserInstallFailed": "Chromium install failed",
}


def _format_incomplete_shard(status: dict) -> str:
    """Human-readable label for an incomplete shard.

    A recorded ``failureReason`` is the most specific thing available — the shard
    said why it could not run — so it wins over the bare exit code. Failing that,
    a numeric exit code means the shard crashed after running; the ``"missing"``
    sentinel means it uploaded nothing at all — its runner was lost before the
    ``if: always()`` collect/upload steps could run (typically a spot-runner
    shutdown signal, i.e. the WPT step ends with ``exit code 143`` / "The runner
    has received a shutdown signal"), so the whole slice went unmeasured rather
    than crashing on a specific test."""
    reason = status.get("failureReason")
    # A shard the workflow's retry pass already reran is labelled as such: it is
    # the *rerun* that came back inconclusive, so "re-run the workflow" is no
    # longer the obvious next step.
    retried = " after rerun" if status.get("retried") else ""
    if reason:
        # An unrecognised reason is still more informative than the exit code, so
        # it is shown as-is rather than dropped.
        label = FAILURE_REASON_LABELS.get(reason, reason)
        return f"shard {status['shardIndex']} ({label}{retried})"
    exit_code = status["exitCode"]
    if exit_code == "missing":
        return f"shard {status['shardIndex']} (no report uploaded{retried})"
    return f"shard {status['shardIndex']} (exit {exit_code}{retried})"


def _incomplete_shard_cause_sentences(shards: list[dict]) -> list[str]:
    """Explain *why* the incomplete shards are incomplete.

    A shard that recorded a ``failureDetail`` said exactly why it never ran a
    test, so that explanation is quoted rather than guessed at. Only shards with
    nothing recorded fall back to the eviction guess — the common cause of a
    silent "no report uploaded", but flatly wrong for issue #1534, where shard 0
    exited 1 because the Playwright CDN geo-blocked its Chromium download."""
    sentences: list[str] = []

    explained = [status for status in shards if status.get("failureDetail")]
    for status in explained:
        sentences.append(f"Shard {status['shardIndex']}: {status['failureDetail']}")
    if explained:
        sentences.append(
            "A shard that could not provision its browser is a CI infrastructure "
            "outage rather than a test regression — no code change fixes it, and it "
            "clears on its own."
        )

    if any(not status.get("failureDetail") for status in shards):
        sentences.append(
            "A shard that recorded no reason is usually a transient CI-runner "
            "eviction (the runner received a shutdown signal before its upload step "
            "ran), which is likewise not a test regression."
        )

    retried = [str(status["shardIndex"]) for status in shards if status.get("retried")]
    if retried:
        # Worth saying plainly: the workflow's own retry pass already spent a
        # second full run on these and they still came back inconclusive, so this
        # is not the usual one-off eviction that clears by itself.
        subject = f"Shard {retried[0]} was" if len(retried) == 1 else f"Shards {', '.join(retried)} were"
        sentences.append(
            f"{subject} already rerun automatically at the end of this run and still "
            "did not complete, so this is unlikely to be a one-off runner eviction."
        )

    return sentences


def _incomplete_shard_recovery_hint(shards: list[dict]) -> str:
    """Actionable recovery guidance for the incomplete-shard report entry: which
    ``shard_index`` values to re-dispatch the workflow with. A single missing shard
    is a direct ``shard_index=N``; several are re-dispatched one per index."""
    indexes = [str(status["shardIndex"]) for status in shards]
    if len(indexes) == 1:
        return f"set shard_index={indexes[0]}"
    return "re-dispatch once per shard, setting shard_index to " + ", ".join(indexes)


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


def _reference_score_note(test: dict) -> str:
    """
    Describe how the test scored against the reference it declares itself, when the
    runner measured it.

    ``suspectReference`` is only set when Broiler *clears the pass threshold* against
    that reference, so a test at 94% against its own reference and 0.8% against the
    committed golden carried no signal at all and was ranked as though nothing were
    known about it — indistinguishable from one that is wrong against both. The two
    need opposite work: the first says the goldens disagree and "fixing" it would mean
    rendering less than the test asks for, the second is a real engine gap. Printing the
    second number says which one this is; ``_engine_gap_percent`` is what *ranks* on it.
    """
    reference_percent = test.get("referenceMatchPercent")
    if reference_percent is None:
        return ""

    match_percent = test["matchPercent"]
    note = (
        f" Against the reference the test itself declares via `rel=match` it scores"
        f" {reference_percent:.1f}%"
    )
    if reference_percent - match_percent >= REFERENCE_DISAGREEMENT_MARGIN:
        note += (
            " — far closer than to the committed golden, so most of this gap is the two"
            " references disagreeing rather than the feature under test"
        )
    elif match_percent - reference_percent >= REFERENCE_DISAGREEMENT_MARGIN:
        note += (
            " — *worse* than against the committed golden, so the golden is flattering"
            " this render and the feature under test is the gap"
        )

    return note + ". Severity above is ranked on that score, not the golden's."


#: How far a test's score against its own ``rel=match`` reference must exceed its score
#: against the committed golden before the gap is attributed to the references
#: disagreeing rather than to the engine. Mirrors the runner's own margin; it is a
#: comparative claim, so it needs no tuning against the run's pass threshold.
REFERENCE_DISAGREEMENT_MARGIN = 25.0


def _engine_gap_percent(test: dict) -> float:
    """How wrong the *engine* is about this test, as a match percentage.

    Two different numbers can be measured for one failing test, and they answer
    different questions. ``matchPercent`` is against the committed golden — a
    *Chromium* screenshot, so it also moves when the two engines merely disagree
    about the reference. ``referenceMatchPercent`` is against the reference the
    test itself declares via ``rel=match``, re-rendered by Broiler, so nothing but
    Broiler is in it: it is WPT's own statement of the right answer.

    When ``--verify-reference`` measured the second, it is the better estimate of
    the engine's gap and severity is read from it — **in both directions**, which
    is the point. Ranking on the golden alone got the 2026-08-15 run's severity
    issue wrong at both ends: ``css-page/page-margin-002-print`` (0.0% golden,
    89.2% against its own reference, and already settled as a reference
    disagreement in ``docs/wpt-rendering-gaps-wont-fix.md``) was ranked the run's
    single biggest problem, while ``column-subgrid-auto-fill-008`` (11.5% golden,
    **0.2%** against its own reference — the worst real render in the list) was
    ranked seventeenth.

    Falls back to the golden when no reference score was measured, so a run
    without ``--verify-reference`` ranks exactly as it always did.
    """
    reference_percent = test.get("referenceMatchPercent")
    if reference_percent is None:
        return float(test["matchPercent"])
    return float(reference_percent)


def _is_partial_reference_disagreement(test: dict) -> bool:
    """Whether the test's own reference says most of its golden gap is not the engine.

    The runner's ``suspectReference`` covers only the clean case, where Broiler
    *clears the pass gate* against the test's own reference. This is the rest of
    the same class: short of the gate, but far enough above the golden that the
    references are demonstrably disagreeing. Such a test is still ranked — it has
    a real residual gap — but on ``_engine_gap_percent``, and it is listed
    alongside the cleared ones so a near-0% golden never disappears silently.
    """
    reference_percent = test.get("referenceMatchPercent")
    if reference_percent is None:
        return False
    return float(reference_percent) - float(test["matchPercent"]) >= REFERENCE_DISAGREEMENT_MARGIN


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

    Tier 2 is measured with ``_engine_gap_percent``, not with the raw golden
    score: where the runner rendered the test's own ``rel=match`` reference, that
    is the number that says how wrong the engine is, and both the threshold and
    the ordering read it. A test the golden calls catastrophic and its own
    reference calls nearly right therefore stops crowding out the run's real
    worst renders, and a test the golden flatters stops hiding among them.

    A mismatch carrying ``suspectReference`` is excluded from tier 2 altogether.
    That flag means the runner re-rendered the test's own ``rel=match`` reference
    and Broiler *did* reproduce it — so the committed golden is the outlier and
    the render is not the run's worst problem, however low the percentage looks.
    Ranking these would (and repeatedly did) fill the severity issue with tests
    that are correct by the only authority the test itself names; they are
    reported separately by ``_reference_disagreements``, which also lists the
    partial cases this ranking demotes rather than clears.

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
        listing = ", ".join(_format_incomplete_shard(status) for status in shards)
        problems.append(
            {
                "kind": "IncompleteShards",
                "tier": 0,
                "severity": len(shards),
                "impact": len(shards),
                "title": f"{len(shards)} shard(s) did not complete",
                "detail": " ".join(
                    [
                        "A whole shard's tests are unaccounted for — the pass/fail of "
                        f"that slice is unknown. {listing}.",
                        *_incomplete_shard_cause_sentences(shards),
                        "The slice can be remeasured without a code change: re-run the "
                        f"WPT Tests workflow and {_incomplete_shard_recovery_hint(shards)} "
                        "(cached references make the rerun fast).",
                    ]
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
        if _engine_gap_percent(test) >= low_match_threshold:
            continue
        if test.get("suspectReference"):
            continue
        existing = lowest_by_path.get(test["relativeTestPath"])
        if existing is None or _engine_gap_percent(test) < _engine_gap_percent(existing):
            lowest_by_path[test["relativeTestPath"]] = test
    for test in lowest_by_path.values():
        label = test["category"]
        if test["subCategory"]:
            label = f"{label} / {test['subCategory']}"
        gap_percent = _engine_gap_percent(test)
        problems.append(
            {
                "kind": "LowMatch",
                "tier": 2,
                "severity": 100.0 - gap_percent,
                "impact": 1,
                "matchPercent": test["matchPercent"],
                # The number the ranking was read from: the same as matchPercent
                # unless --verify-reference measured the test's own reference.
                "engineGapPercent": gap_percent,
                "relativeTestPath": test["relativeTestPath"],
                "title": f"{gap_percent:.1f}% match — {test['relativeTestPath']}",
                # Name which reference the first number is against as soon as there
                # are two of them, so the headline percentage and the body cannot be
                # read as disagreeing about the same measurement.
                "detail": (
                    f"Rendered output matches the "
                    f"{'committed golden' if test.get('referenceMatchPercent') is not None else 'reference'}"
                    f" by only {test['matchPercent']:.1f}% ({label})."
                    + _reference_score_note(test)
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


def _reference_disagreements(low_match_tests: list[dict]) -> list[dict]:
    """The mismatches whose own reference contradicts the golden, worst match first.

    Two classes, distinguished by ``cleared``:

    * **cleared** — carrying ``suspectReference``: with ``--verify-reference`` the
      runner also rendered the test's own ``rel=match`` reference and found that
      Broiler reproduces it. The committed golden disagrees with the test's own
      statement of what it should look like, so the render is not a Broiler bug.
    * **partial** — short of that gate, but scoring at least
      ``REFERENCE_DISAGREEMENT_MARGIN`` points better against its own reference
      than against the golden. Most of the golden's gap is the two references
      disagreeing; the remainder is real, so these stay in the ranking, demoted to
      where ``_engine_gap_percent`` puts them. Listing them here as well is what
      keeps a 0.0% golden score visible after the demotion — the report must not
      make a catastrophic-looking number simply vanish.

    A cleared one is excluded from the biggest-problems ranking entirely; a partial
    one is only demoted within it. Either way it is reported here, because "these
    six are not bugs" is itself worth telling a maintainer — and silently dropping a
    0.0% match would be indistinguishable from losing it.
    """
    lowest_by_path: dict[str, dict] = {}
    for test in low_match_tests:
        if not test.get("suspectReference") and not _is_partial_reference_disagreement(test):
            continue
        existing = lowest_by_path.get(test["relativeTestPath"])
        if existing is None or test["matchPercent"] < existing["matchPercent"]:
            lowest_by_path[test["relativeTestPath"]] = test
    return sorted(
        (
            {
                "relativeTestPath": test["relativeTestPath"],
                "matchPercent": test["matchPercent"],
                "referenceMatchPercent": test.get("referenceMatchPercent"),
                "cleared": bool(test.get("suspectReference")),
                "detail": test.get("suspectReference"),
            }
            for test in lowest_by_path.values()
        ),
        key=lambda entry: (entry["matchPercent"], entry["relativeTestPath"]),
    )


def _rank_biggest_problems_escalating(
    incomplete_shards: list[dict],
    exception_signatures: Counter[str],
    exception_examples: dict[str, list[str]],
    low_match_tests: list[dict],
    limit: int,
    low_match_threshold: float,
) -> tuple[list[dict], float]:
    """Rank the biggest problems, widening the low-match threshold until the
    issue is full.

    ``_rank_biggest_problems`` only counts a pixel mismatch as a "low percent
    match" problem when its render matched the reference by *less than* the
    threshold. When those sub-threshold mismatches — together with the
    incomplete-shard and crash entries — don't reach ``limit`` biggest problems,
    the threshold is raised by ``LOW_MATCH_THRESHOLD_STEP`` points and the ranking
    retried, repeating until the list reaches ``limit``, the threshold hits
    ``LOW_MATCH_THRESHOLD_CEILING``, or widening it further would admit no
    additional mismatch (every candidate is already below the cut-off).

    Returns the ranked problems and the threshold actually used, so the issue
    text can report the real cut-off rather than the (possibly stricter) start.
    """
    threshold = float(low_match_threshold)
    # The closest-matching candidate mismatch. Once the threshold clears it, a
    # wider band would capture nothing new, so escalating past it is pointless.
    # Reference disagreements are never ranked, so they are not candidates and must
    # not keep the escalation going after every real mismatch is already admitted.
    highest_match = max(
        (
            _engine_gap_percent(test)
            for test in low_match_tests
            if not test.get("suspectReference")
        ),
        default=None,
    )
    while True:
        problems = _rank_biggest_problems(
            incomplete_shards,
            exception_signatures,
            exception_examples,
            low_match_tests,
            limit,
            threshold,
        )
        if (
            len(problems) >= limit
            or threshold >= LOW_MATCH_THRESHOLD_CEILING
            or highest_match is None
            or threshold > highest_match
        ):
            return problems, threshold
        threshold = min(threshold + LOW_MATCH_THRESHOLD_STEP, LOW_MATCH_THRESHOLD_CEILING)


def _timeout_directory(relative_path: str) -> str:
    """The directory a timed-out test lives in, mirroring the runner's own bucket
    (``Program.GetBucketDirectory``): the test's parent directory, or ``.`` at the
    root. Only used for reports predating ``triage.timeoutFailures``, which carry
    the directory the runner computed."""
    directory, _, _filename = relative_path.rpartition("/")
    return directory or "."


def _record_timeout(
    store: dict[str, dict],
    relative_path: str,
    directory: str,
    message: str | None,
    size_bytes: int | None,
) -> None:
    """Remember one timed-out test, preferring the record that knows its size.

    A test can be described twice — once by ``triage.timeoutFailures`` (with its
    source size) and once by the bare ``results`` entry the fallback scan reads
    (without one). Whichever arrives first, the sized record wins, so the ranking
    never loses a measurement it was given.
    """
    existing = store.get(relative_path)
    if existing is not None and (existing.get("fileSizeBytes") is not None or size_bytes is None):
        return
    store[relative_path] = {
        "relativeTestPath": relative_path,
        "directory": directory,
        "message": message,
        "fileSizeBytes": size_bytes,
    }


def _timeout_severity(size_bytes: int | None) -> str:
    """Coarse severity label for a timeout, read off the test's source size."""
    if size_bytes is None:
        return TIMEOUT_UNKNOWN_LABEL
    for upper_bound, label in TIMEOUT_SIZE_BANDS:
        if size_bytes < upper_bound:
            return label
    return TIMEOUT_LARGE_LABEL


def _format_file_size(size_bytes: int | None) -> str:
    """Human-readable byte count, matching the runner's own formatting."""
    if size_bytes is None:
        return "size unknown"
    if size_bytes >= 1024 * 1024:
        return f"{size_bytes / (1024 * 1024):.1f} MiB"
    # Bytes, not "0.0 KiB", below a kilobyte: the head of this ranking is made of
    # exactly those files, and rounding them all to the same 0.0 would hide the
    # one distinction the report exists to draw.
    if size_bytes >= 1024:
        return f"{size_bytes / 1024:.1f} KiB"
    return f"{size_bytes} B"


def _rank_timeouts(timeout_tests: dict[str, dict], limit: int) -> list[dict]:
    """The run's timed-out tests, smallest source first, bounded to ``limit``.

    Ascending size is the whole ranking, and it is the point of this report: the
    smaller the document, the less of it can be legitimately slow, so the likelier
    its timeout is an engine hang than a heavy page (see the module docstring). A
    test whose size could not be read sorts last — it carries no signal either way
    and must not displace a measured small file.
    """
    ranked = sorted(
        timeout_tests.values(),
        key=lambda test: (
            test["fileSizeBytes"] is None,
            test["fileSizeBytes"] if test["fileSizeBytes"] is not None else 0,
            test["relativeTestPath"],
        ),
    )
    return [
        {
            **test,
            "severity": _timeout_severity(test["fileSizeBytes"]),
        }
        for test in ranked[:limit]
    ]


def merge(
    shard_dir: Path,
    problem_limit: int = DEFAULT_PROBLEM_LIMIT,
    biggest_problem_limit: int = DEFAULT_BIGGEST_PROBLEM_LIMIT,
    low_match_threshold: float = DEFAULT_LOW_MATCH_THRESHOLD,
    expected_shard_indexes: set[int] | None = None,
    timeout_limit: int = DEFAULT_TIMEOUT_LIMIT,
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
    timeout_tests: dict[str, dict] = {}
    problem_groups: dict[str, dict] = {}
    family_groups: dict[str, dict] = {}
    reported_shard_indexes: set[int] = set()

    # Reports first: a shard the retry pass reran contributes its retry only, so
    # the totals below count each shard's slice exactly once.
    report_entries, retried_by_report = _select_latest_attempts(
        _iter_shard_reports(shard_dir), _report_shard_index
    )

    for _path, report in report_entries:
        shard_count += 1
        summary = report.get("summary", {})
        shard_index = _report_shard_index(report)
        if shard_index is not None:
            reported_shard_indexes.add(shard_index)
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
                suspect_reference = entry.get("suspectReference")
                low_match_tests.append(
                    {
                        "relativeTestPath": str(relative_path),
                        "matchPercent": float(match_percent),
                        "category": str(entry.get("category") or "Unknown"),
                        "subCategory": str(sub_category) if sub_category else None,
                        # Present only when the runner ran with --verify-reference and
                        # found that Broiler reproduces the test's own rel=match
                        # reference. Such a test is a reference disagreement, not a bad
                        # render, so it is ranked separately (see _rank_biggest_problems).
                        "suspectReference": (
                            str(suspect_reference) if suspect_reference else None
                        ),
                        # The score against that same reference whether or not it
                        # cleared the gate. A test far closer to its own reference than
                        # to the golden is a reference disagreement too, and without
                        # this number nothing in the report could say so.
                        "referenceMatchPercent": (
                            float(reference_percent)
                            if isinstance(
                                (reference_percent := entry.get("referenceMatchPercent")),
                                (int, float),
                            )
                            else None
                        ),
                    }
                )

            # Tests the runner aborted at the per-test timeout, each with the size
            # of its own source — the signal the timeouts issue ranks on. The
            # results loop below re-reads the same failures for reports that predate
            # this triage entry, so the issue is complete either way; only the sizes
            # (and therefore the ranking) need this.
            for entry in triage.get("timeoutFailures", []) or []:
                if not isinstance(entry, dict):
                    continue
                relative_path = entry.get("testPath") or entry.get("relativeTestPath")
                if not relative_path:
                    continue
                size_bytes = entry.get("fileSizeBytes")
                _record_timeout(
                    timeout_tests,
                    str(relative_path),
                    str(entry.get("directory") or _timeout_directory(str(relative_path))),
                    str(entry["message"]) if entry.get("message") else None,
                    int(size_bytes) if isinstance(size_bytes, (int, float)) else None,
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

            # Every timeout, counted here rather than from triage.timeoutFailures:
            # this loop sees the authoritative per-test verdicts, so the timeouts
            # issue reports the same number the category breakdown does even for a
            # report that carries no timeout triage at all (an older shard, or one
            # merged from a runner build predating it). _record_timeout keeps the
            # sized triage record when there is one, so nothing is downgraded.
            if category == "Timeout":
                _record_timeout(
                    timeout_tests,
                    relative_path,
                    _timeout_directory(relative_path),
                    str(result["message"]) if result.get("message") else None,
                    None,
                )

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

    # Exit code left behind by each shard that got far enough to run the
    # "Collect shard result" step (index -> exit code).
    shard_exit_codes: dict[int, int] = {}
    # Why a shard could not run, for the shards that managed to say so
    # (index -> the marker fields). run-wpt-tests.sh records this for failures
    # that stop a shard before any test executes — a browser it could not
    # download, say — so the report can name the cause instead of inferring one
    # from a bare non-zero exit.
    shard_failure_reasons: dict[int, dict[str, str]] = {}
    status_entries, retried_by_status = _select_latest_attempts(
        _iter_shard_statuses(shard_dir), _status_shard_index
    )
    # Shards the end-of-workflow retry pass reran. A shard is "retried" if either
    # kind of artifact carries the retry suffix: a rerun that died before writing
    # a report still leaves a status file, and a rerun that succeeded leaves both.
    retried_shard_indexes = sorted(retried_by_report | retried_by_status)
    for _path, status in status_entries:
        try:
            shard_index = int(status["shardIndex"])
            exit_code = int(status["exitCode"])
        except (TypeError, ValueError):
            continue
        shard_exit_codes[shard_index] = exit_code
        marker = {
            field: status[field].strip()
            for field in ("failureReason", "failureDetail")
            if isinstance(status.get(field), str) and status[field].strip()
        }
        if marker:
            shard_failure_reasons[shard_index] = marker

    # A shard is incomplete when it produced no conclusive report. There are two
    # ways that happens, and the second one used to be invisible:
    #   1. it left a status file with a non-zero exit — it crashed after the run
    #      step but the runner still ran the always() collect step; or
    #   2. it was dispatched (its index is in expected_shard_indexes) but produced
    #      neither a report nor a status file. Its job was cancelled mid-run — e.g.
    #      it hit the job timeout-minutes — so the always() collect/upload steps
    #      never ran and its whole slice silently vanished from the artifacts.
    #
    # Case 2 is the important one: the merged pass/fail/skip/total counts are a raw
    # SUM over the shards that uploaded, so a vanished shard drops ~1/N of every
    # count with no indication. That is why the "skipped" total is not comparable
    # between runs — a run that merges 6 of 8 shards reports ~6/8 of the skips a
    # full 8-shard merge would. Flagging the missing shard makes the shortfall
    # visible (incomplete_shard_count > 0 → the issue and summary say so) instead
    # of silently under-counting.
    candidate_indexes = set(shard_exit_codes)
    if expected_shard_indexes is not None:
        candidate_indexes |= set(expected_shard_indexes)

    incomplete_shards = []
    for shard_index in sorted(candidate_indexes):
        if shard_index in reported_shard_indexes:
            continue
        exit_code = shard_exit_codes.get(shard_index)
        # A clean exit with no report is odd but not an incomplete run; leave it.
        if exit_code == 0:
            continue
        incomplete_shards.append(
            # A dispatched shard that left no status file at all never uploaded
            # anything — record it as "missing" rather than a numeric exit code.
            {
                "shardIndex": shard_index,
                "exitCode": exit_code if exit_code is not None else "missing",
                # Still unmeasured *after* the workflow already reran it, which
                # rules out a one-off runner eviction and is worth saying out
                # loud — the report otherwise reads as if a rerun might fix it.
                **({"retried": True} if shard_index in retried_by_report | retried_by_status else {}),
                **shard_failure_reasons.get(shard_index, {}),
            }
        )

    if incomplete_shards:
        incomplete_shards.sort(key=lambda status: status["shardIndex"])
        problem_groups["ShardProcessError"] = {
            "key": "ShardProcessError",
            "label": "Shard process failure",
            "category": "ShardProcessError",
            "subCategory": None,
            "count": len(incomplete_shards),
            "examples": [
                _format_incomplete_shard(status)
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

    biggest_problems, effective_low_match_threshold = _rank_biggest_problems_escalating(
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
        "retriedShards": retried_shard_indexes,
        "topProblems": top_problems,
        "topFailingDirectories": directory_counter.most_common(problem_limit),
        "failuresByCategory": category_counter.most_common(),
        "droppedDeclarations": dropped_declaration_counter.most_common(problem_limit),
        "exceptionSignatures": exception_signature_counter.most_common(problem_limit),
        "failureFamilies": top_families,
        "biggestProblemLimit": biggest_problem_limit,
        "lowMatchThreshold": effective_low_match_threshold,
        "biggestProblems": biggest_problems,
        "timeoutLimit": timeout_limit,
        # Every timeout the run saw, even when only `timeout_limit` of them are
        # listed in the issue — the count is what says whether the listed head is
        # the whole story.
        "timeoutCount": len(timeout_tests),
        "timeouts": _rank_timeouts(timeout_tests, timeout_limit),
        # Counted from the same deduped set the ranking is drawn from, so the
        # clusters always add up to `timeoutCount` rather than to whichever of the
        # two sources happened to describe a given test.
        "timeoutDirectories": Counter(
            test["directory"] for test in timeout_tests.values()
        ).most_common(problem_limit),
        "referenceDisagreements": _reference_disagreements(low_match_tests),
        "results": failures,
    }


def _collect_executed_paths(shard_dir: Path) -> set[str]:
    """Relative paths of tests that produced a conclusive pass/fail verdict in
    this run. Skips are inconclusive (e.g. a missing reference image) and are
    deliberately excluded, so a test that merely skipped this run does not evict
    its existing manifest entry. Used to scope ``merge_into_manifest``."""
    executed: set[str] = set()
    for _path, report in _shard_reports(shard_dir):
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
    ]
    # Shards the workflow reran at the end because their first attempt aborted
    # abnormally. Their retry's results are what the totals above count — the
    # aborted attempt is superseded, not added to it.
    retried = merged.get("retriedShards") or []
    if retried:
        lines.append(
            "- Shards rerun after an abnormal abort: "
            + ", ".join(str(index) for index in retried)
        )
    lines += [
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

    # Mismatches the runner cleared as reference problems. Listed, but deliberately
    # outside the ranking above: Broiler reproduces the reference each of these
    # tests itself declares, so a near-0% score against the committed golden says
    # the golden is wrong, not the render.
    disagreements = merged.get("referenceDisagreements") or []
    if disagreements:
        cleared = [entry for entry in disagreements if entry.get("cleared", True)]
        partial = [entry for entry in disagreements if not entry.get("cleared", True)]
        lines += [
            "",
            "### Reference disagreements",
            "",
            f"_For each of these {len(disagreements)} mismatch(es) the runner re-rendered"
            " the reference the test itself declares via `rel=match`, and Broiler scored"
            " far better against it than against the committed golden — so most of the"
            " golden's gap is the two references disagreeing rather than the feature"
            f" under test. {len(cleared)} reproduce that reference outright and are"
            " excluded from the ranking above entirely; the other"
            f" {len(partial)} keep a real residual gap and are ranked above on *that*"
            " score instead of the golden's. Chasing the golden on any of them would"
            " mean rendering *less* than the test asks for._",
            "",
        ]
        for entry in disagreements:
            reference_percent = entry.get("referenceMatchPercent")
            if reference_percent is None:
                against = ""
            elif entry.get("cleared", True):
                against = f" — reproduces its own reference at {reference_percent:.1f}%"
            else:
                against = f" — {reference_percent:.1f}% against its own reference"
            lines.append(
                f"- **{entry['matchPercent']:.1f}% golden** — "
                f"`{entry['relativeTestPath']}`{against}"
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


def render_timeout_issue_markdown(merged: dict, run_url: str | None) -> str:
    """Render the body of the third issue: only the tests that timed out, ranked
    by their own source size, smallest first (see ``_rank_timeouts``)."""
    timeouts = merged.get("timeouts") or []
    total = merged.get("timeoutCount", len(timeouts))
    limit = merged.get("timeoutLimit", DEFAULT_TIMEOUT_LIMIT)
    lines = [
        f"## WPT run — {total} timed-out test(s)",
        "",
        "_Only the tests the runner had to abort at the per-test timeout. They are"
        " ranked by the size of the test's own source, **smallest first**: the less"
        " there is in a document, the less of it can be legitimately slow, so the"
        " likelier its timeout is an engine hang — a layout loop that never"
        " terminates, a parser that never advances — than a heavy page that merely"
        " wants more than the budget. A timeout on a multi-megabyte stress test says"
        " the budget is tight; the same timeout on a sub-kilobyte document says"
        " something does not terminate, and that bug usually gates far more than the"
        " one test._",
        "",
        f"- Timed out: {total}",
        f"- Listed below: {min(total, limit)}"
        + (f" (of {total}; raise `timeout_problems_limit` for more)" if total > limit else ""),
        "",
        "### Timed-out tests, smallest source first",
        "",
    ]
    if timeouts:
        for index, timeout in enumerate(timeouts, start=1):
            lines.append(
                f"{index}. **{_format_file_size(timeout['fileSizeBytes'])}** —"
                f" `{timeout['relativeTestPath']}` ({timeout['severity']})"
            )
            if timeout.get("message"):
                lines.append(f"   - {timeout['message']}")
    else:
        lines.append("- None — no test hit the per-test timeout.")

    # A directory with several timeouts is one hang gating a feature area rather
    # than N independent problems, and it is the cheapest thing to act on: the
    # subset command reruns exactly that slice.
    directories = merged.get("timeoutDirectories") or []
    if directories:
        lines += [
            "",
            "### Timeout clusters",
            "",
            "_Directories with more than one timeout are usually a single hang gating a"
            " whole feature area. Rerun one with the subset command beside it._",
            "",
        ]
        for directory, count in directories:
            lines.append(
                f"- `{directory}` — {count} timeout(s) —"
                f' `./scripts/run-wpt-tests.sh --subset "{directory}"`'
            )

    if timeouts:
        first = timeouts[0]["relativeTestPath"]
        lines += [
            "",
            "### Reproduce locally",
            "",
            "_Render the smallest timed-out test — the top of the ranking — against the"
            " live engine. It hangs where the suite gave up, so a debugger attached here"
            " lands directly in the non-terminating code._",
            "",
            "```sh",
            "dotnet run --project src/Broiler.Wpt -- \\",
            f"  --wpt-dir {WPT_CHECKOUT_DIR} --render {WPT_CHECKOUT_DIR}/{first}",
            "```",
            "",
            "_Raise `--timeout` (or `BROILER_WPT_TIMEOUT_SECONDS`) to tell a genuine hang"
            " apart from a test that is merely slower than the budget: a hang still does"
            " not finish with the limit lifted._",
        ]

    lines += [
        "",
        "### CI metadata",
        f"- Workflow run: {run_url}" if run_url else "- Workflow run: (unknown)",
        "- Artifact: `wpt-merged`",
        "",
        "_Auto-generated by `.github/workflows/wpt-tests.yml`. See the companion"
        " most-common-failures and biggest-problems issues for the rest of the run._",
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
        "--timeout-issue-md",
        type=Path,
        help="Where to write the Markdown body for the third, timeouts-only issue",
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
        "--timeout-limit",
        type=int,
        default=DEFAULT_TIMEOUT_LIMIT,
        help="Maximum timed-out tests to list in the third issue, smallest source "
        f"first (default: {DEFAULT_TIMEOUT_LIMIT})",
    )
    parser.add_argument(
        "--low-match-threshold",
        type=float,
        default=DEFAULT_LOW_MATCH_THRESHOLD,
        help="A pixel match below this percent counts as a 'low percent match' big "
        f"problem (default: {DEFAULT_LOW_MATCH_THRESHOLD:g})",
    )
    parser.add_argument(
        "--expected-shards",
        help="Comma-separated shard indexes this run dispatched (e.g. '0,1,2,3,4,5,6,7'). "
        "Any expected shard that uploaded neither a report nor a status file is "
        "flagged incomplete, so a shard whose job was cancelled mid-run (its slice "
        "silently missing from the merged totals) is surfaced instead of dropped.",
    )
    parser.add_argument("--run-url", default=os.environ.get("WPT_RUN_URL"), help="Workflow run URL for the issue footer")
    parser.add_argument("--github-output", type=Path, help="Path to $GITHUB_OUTPUT for step outputs")
    args = parser.parse_args()

    if args.problem_limit < 1:
        parser.error("--problem-limit must be a positive integer")
    if args.biggest_problem_limit < 1:
        parser.error("--biggest-problem-limit must be a positive integer")
    if args.timeout_limit < 1:
        parser.error("--timeout-limit must be a positive integer")
    if not 0 <= args.low_match_threshold <= 100:
        parser.error("--low-match-threshold must be between 0 and 100")

    expected_shard_indexes: set[int] | None = None
    if args.expected_shards is not None:
        try:
            expected_shard_indexes = {
                int(token) for token in args.expected_shards.split(",") if token.strip() != ""
            }
        except ValueError:
            parser.error("--expected-shards must be a comma-separated list of integers")

    merged = merge(
        args.shard_dir,
        args.problem_limit,
        args.biggest_problem_limit,
        args.low_match_threshold,
        expected_shard_indexes=expected_shard_indexes,
        timeout_limit=args.timeout_limit,
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

    if args.timeout_issue_md:
        args.timeout_issue_md.parent.mkdir(parents=True, exist_ok=True)
        args.timeout_issue_md.write_text(
            render_timeout_issue_markdown(merged, args.run_url), encoding="utf-8"
        )

    failed = merged["summary"]["failed"]
    total = merged["summary"]["total"]
    incomplete_shard_count = len(merged["incompleteShards"])
    biggest_problem_count = len(merged.get("biggestProblems") or [])
    timeout_count = merged.get("timeoutCount", 0)
    print(f"Merged {merged['shardCount']} shard(s): {merged['summary']['passed']} passed, "
          f"{failed} failed, {merged['summary']['skipped']} skipped, {total} total.")

    if args.github_output:
        # The indexes of the shards that went unmeasured, in the two shapes the
        # workflow's retry pass needs: a comma-separated list for the log/summary
        # and a ready-to-use `strategy.matrix.include` array. `retry` only
        # re-dispatches shards that produced no conclusive report — a shard that
        # ran to completion and merely reported failing tests is not incomplete
        # and is never rerun.
        incomplete_indexes = [status["shardIndex"] for status in merged["incompleteShards"]]
        matrix = json.dumps([{"shard-index": index} for index in incomplete_indexes])
        with args.github_output.open("a", encoding="utf-8") as handle:
            handle.write(f"failed_count={failed}\n")
            handle.write(f"total_count={total}\n")
            handle.write(f"incomplete_shard_count={incomplete_shard_count}\n")
            handle.write(f"create_issue={'true' if failed > 0 or incomplete_shard_count > 0 else 'false'}\n")
            handle.write(f"biggest_problem_count={biggest_problem_count}\n")
            handle.write(f"create_biggest_issue={'true' if biggest_problem_count > 0 else 'false'}\n")
            handle.write(f"timeout_count={timeout_count}\n")
            handle.write(f"create_timeout_issue={'true' if timeout_count > 0 else 'false'}\n")
            handle.write(
                "incomplete_shard_indexes="
                + ",".join(str(index) for index in incomplete_indexes)
                + "\n"
            )
            handle.write(f"incomplete_shard_matrix={matrix}\n")
            handle.write(f"has_incomplete_shards={'true' if incomplete_indexes else 'false'}\n")
            handle.write(f"retried_shard_count={len(merged['retriedShards'])}\n")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
