#!/usr/bin/env python3
"""Run Broiler against the DuckDuckGo privacy test pages, beside Chromium.

The pages under https://privacy-test-pages.site/privacy-protections/ each run a
battery of browser probes and publish the outcome in a ``results`` global.  That
global is the whole interface this runner uses: ``Broiler.Cli --evaluate-page``
runs the page's own scripts and then evaluates the manifest's expressions
against the settled page, so what is reported is what the page computed rather
than a rendering of it.

Every page is run twice — once by Chromium through Playwright
(``scripts/capture-privacy-reference.js``) and once by Broiler — from the same
manifest and the same expressions, and the two are compared probe by probe.
The Chromium run is what turns "Broiler produced nothing here" into a finding:
a probe Chromium answers and Broiler does not is a platform gap with a known
expected shape, while a probe neither answers is a property of the page, the
corpus or the network rather than of Broiler.  Nothing is compared as pixels;
these pages render a summary of what their own JavaScript measured, so their
`results` payload is the meaningful output and a screenshot of it would only
restate it less precisely.

What is measured is *coverage*, not privacy.  These pages ask a browser to
exercise storage APIs, request paths, fingerprinting surfaces and referrer
handling; a test that yields a value is one Broiler carried out, and a test that
yields nothing is a platform gap.  Whether the resulting value would be a good
privacy outcome for a shipping browser is a separate question this suite does
not answer — the pages are written to describe a browser, not to grade one.
That holds for the comparison too: matching Chromium is evidence Broiler
carried the probe out, not evidence it made the right privacy choice.

The corpus is live and maintained upstream, so tests appear and disappear
without a Broiler code change.  Only a test that used to produce a value and
now does not, or a page that stops running altogether, is treated as a
regression; additions and upstream removals are reported and never fail a run.

Manifest validation, outcome classification, the baseline comparison and the
Chromium comparison are pure helpers, so the meaning of a reported regression
or gap stays independent of process execution and report formatting.
"""

from __future__ import annotations

import argparse
import datetime as dt
import html
import json
import os
from pathlib import Path
import platform
import re
import shlex
import shutil
import subprocess
import sys
from typing import Any, Mapping, Sequence


SCHEMA_VERSION = 1
DEFAULT_MANIFEST = "tests/privacy-test-pages/pages.json"
DEFAULT_BASELINE = "tests/privacy-test-pages/baseline.json"
DEFAULT_OUTPUT_DIR = "artifacts/privacy-test-pages"
DEFAULT_REFERENCE_SCRIPT = "scripts/capture-privacy-reference.js"
REFERENCE_ENGINE = "chromium"
LOG_EXCERPT_BYTES = 64 * 1024
COMMAND_TIMEOUT_MARGIN_SECONDS = 60
# Whole-corpus reference capture, not per page: the Node runner walks every
# selected page itself, and each page is already bounded by the manifest.
REFERENCE_TIMEOUT_MARGIN_SECONDS = 300
VALUE_EXCERPT_CHARS = 240

OBSERVATIONAL_NOTICE = (
    "The privacy test pages are a live, independently maintained corpus. Tests "
    "are added, renamed, and removed upstream without a Broiler code change, so "
    "only a test that stops producing a value counts as a regression."
)
COVERAGE_NOTICE = (
    "A test that produces a value is one Broiler was able to carry out. This "
    "suite measures platform coverage, not whether the value would be a good "
    "privacy outcome for a shipping browser."
)
COMPARISON_NOTICE = (
    "Chromium runs the same pages through the same manifest expressions, so a "
    "probe it answers and Broiler does not is a platform gap with a known "
    "expected shape. Agreement means Broiler carried the probe out, not that "
    "the value it produced is the right privacy answer."
)

# `value` and `empty` are outcomes this run observed; `missing` and `new` only
# ever arise from comparing a run against the baseline.
OUTCOME_VALUE = "value"
OUTCOME_EMPTY = "empty"
OUTCOME_MISSING = "missing"
OUTCOME_NEW = "new"

# How one probe's Broiler outcome relates to the same probe in Chromium.
PARITY_MATCH = "parity"
PARITY_GAP = "gap"
PARITY_AHEAD = "broiler-only"
PARITY_NEITHER = "neither"

DEFAULT_KEYS = {
    "timeoutSeconds",
    "attempts",
    "startExpression",
    "resultsExpression",
}
PAGE_KEYS = {
    "id",
    "name",
    "path",
    "description",
    "tags",
} | DEFAULT_KEYS
SOURCE_KEYS = {"repository", "baseUrl"}
IDENTIFIER_RE = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")


class ManifestError(ValueError):
    """Raised when the privacy test page manifest is not schema version 1."""


class BaselineError(ValueError):
    """Raised when the tracked baseline cannot be read as schema version 1."""


def utc_now() -> str:
    """Return a compact, JSON-friendly UTC timestamp."""

    return dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def _require_object(value: Any, location: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ManifestError(f"{location} must be an object")
    return value


def _require_string(value: Any, location: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ManifestError(f"{location} must be a non-empty string")
    return value


def _require_identifier(value: Any, location: str) -> str:
    text = _require_string(value, location)
    if not IDENTIFIER_RE.match(text):
        raise ManifestError(f"{location} must be a lowercase hyphenated identifier, got {text!r}")
    return text


def _require_integer(value: Any, location: str, minimum: int, maximum: int) -> int:
    if not isinstance(value, int) or isinstance(value, bool) or not minimum <= value <= maximum:
        raise ManifestError(f"{location} must be an integer between {minimum} and {maximum}")
    return value


def _validate_config_fields(config: Mapping[str, Any], location: str, *, partial: bool) -> dict[str, Any]:
    unknown = sorted(set(config) - DEFAULT_KEYS)
    if unknown:
        raise ManifestError(f"{location} has unsupported keys: {', '.join(unknown)}")

    resolved: dict[str, Any] = {}
    if "timeoutSeconds" in config:
        resolved["timeoutSeconds"] = _require_integer(
            config["timeoutSeconds"], f"{location}.timeoutSeconds", 1, 600
        )
    elif not partial:
        raise ManifestError(f"{location}.timeoutSeconds is required")

    if "attempts" in config:
        resolved["attempts"] = _require_integer(config["attempts"], f"{location}.attempts", 1, 5)
    elif not partial:
        raise ManifestError(f"{location}.attempts is required")

    for key in ("startExpression", "resultsExpression"):
        if key in config:
            resolved[key] = _require_string(config[key], f"{location}.{key}")
        elif not partial:
            raise ManifestError(f"{location}.{key} is required")

    return resolved


def validate_manifest(data: Any, source: str = "manifest") -> dict[str, Any]:
    """Validate the manifest and return it with page identifiers proven unique."""

    document = _require_object(data, source)
    unknown = sorted(set(document) - {"$schema", "schemaVersion", "source", "defaults", "pages"})
    if unknown:
        raise ManifestError(f"{source} has unsupported keys: {', '.join(unknown)}")

    if document.get("schemaVersion") != SCHEMA_VERSION:
        raise ManifestError(f"{source}.schemaVersion must be {SCHEMA_VERSION}")

    origin = _require_object(document.get("source"), f"{source}.source")
    missing = sorted(SOURCE_KEYS - set(origin))
    if missing:
        raise ManifestError(f"{source}.source is missing: {', '.join(missing)}")
    unknown = sorted(set(origin) - SOURCE_KEYS)
    if unknown:
        raise ManifestError(f"{source}.source has unsupported keys: {', '.join(unknown)}")
    base_url = _require_string(origin["baseUrl"], f"{source}.source.baseUrl")
    if not base_url.startswith("https://"):
        raise ManifestError(f"{source}.source.baseUrl must be an https:// origin")
    if base_url.endswith("/"):
        raise ManifestError(f"{source}.source.baseUrl must not end with '/'")
    _require_string(origin["repository"], f"{source}.source.repository")

    defaults = _validate_config_fields(
        _require_object(document.get("defaults"), f"{source}.defaults"),
        f"{source}.defaults",
        partial=False,
    )

    pages = document.get("pages")
    if not isinstance(pages, list) or not pages:
        raise ManifestError(f"{source}.pages must be a non-empty array")

    seen: set[str] = set()
    for index, page in enumerate(pages):
        location = f"{source}.pages[{index}]"
        entry = _require_object(page, location)
        unknown = sorted(set(entry) - PAGE_KEYS)
        if unknown:
            raise ManifestError(f"{location} has unsupported keys: {', '.join(unknown)}")

        identifier = _require_identifier(entry.get("id"), f"{location}.id")
        if identifier in seen:
            raise ManifestError(f"{location}.id duplicates {identifier!r}")
        seen.add(identifier)

        _require_string(entry.get("name"), f"{location}.name")
        _require_string(entry.get("description"), f"{location}.description")
        path = _require_string(entry.get("path"), f"{location}.path")
        if not path.startswith("/"):
            raise ManifestError(f"{location}.path must start with '/'")

        tags = entry.get("tags", [])
        if not isinstance(tags, list):
            raise ManifestError(f"{location}.tags must be an array")
        for tag_index, tag in enumerate(tags):
            _require_identifier(tag, f"{location}.tags[{tag_index}]")

        _validate_config_fields(
            {key: value for key, value in entry.items() if key in DEFAULT_KEYS},
            location,
            partial=True,
        )

    return {
        "schemaVersion": SCHEMA_VERSION,
        "source": {"repository": origin["repository"], "baseUrl": base_url},
        "defaults": defaults,
        "pages": pages,
    }


def load_manifest(path: Path) -> dict[str, Any]:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        raise ManifestError(f"{path} is not valid JSON: {error}") from error
    return validate_manifest(data, source=str(path))


def resolve_effective_config(manifest: Mapping[str, Any], page: Mapping[str, Any]) -> dict[str, Any]:
    """Merge the manifest defaults with one page's overrides."""

    effective = dict(manifest["defaults"])
    effective.update({key: value for key, value in page.items() if key in DEFAULT_KEYS})
    return effective


def page_url(manifest: Mapping[str, Any], page: Mapping[str, Any]) -> str:
    return f"{manifest['source']['baseUrl']}{page['path']}"


def parse_page_selection(values: Sequence[str] | None) -> list[str]:
    """Split repeated/comma-separated --pages values into identifiers."""

    selected: list[str] = []
    for value in values or ():
        for part in value.split(","):
            identifier = part.strip()
            if identifier and identifier not in selected:
                selected.append(identifier)
    return selected


def classify_test_value(value: Any) -> str:
    """Classify one entry of a page's ``results`` array.

    A test counts as carried out when it produced anything the page could
    serialize.  ``null`` is what the pages' own runner leaves behind when a test
    threw or never resolved, and an empty object or array is a probe that
    returned no data, so both are gaps rather than results.
    """

    if value is None:
        return OUTCOME_EMPTY
    if isinstance(value, (dict, list)) and not value:
        return OUTCOME_EMPTY
    if isinstance(value, str) and not value.strip():
        return OUTCOME_EMPTY
    return OUTCOME_VALUE


def extract_test_entries(results: Any) -> dict[str, dict[str, Any]]:
    """Map a page's ``results`` payload to ``{test id: {outcome, value, name}}``.

    Ids repeat on some pages (the same probe run against several origins), so a
    repeated id is disambiguated by occurrence rather than silently collapsed —
    a collapse would make a page's test count drift with its content.  The value
    is kept alongside the outcome because comparing two engines needs the thing
    each of them produced, not only whether it produced one.
    """

    document = results if isinstance(results, dict) else {}
    entries = document.get("results")
    if not isinstance(entries, list):
        return {}

    extracted: dict[str, dict[str, Any]] = {}
    counts: dict[str, int] = {}
    for index, entry in enumerate(entries):
        if not isinstance(entry, dict):
            continue
        raw_id = entry.get("id")
        identifier = raw_id if isinstance(raw_id, str) and raw_id.strip() else f"#{index}"
        counts[identifier] = counts.get(identifier, 0) + 1
        if counts[identifier] > 1:
            identifier = f"{identifier} ({counts[identifier]})"
        name = entry.get("name")
        extracted[identifier] = {
            "outcome": classify_test_value(entry.get("value")),
            "value": entry.get("value"),
            "name": name if isinstance(name, str) and name.strip() else identifier,
        }
    return extracted


def extract_test_outcomes(results: Any) -> dict[str, str]:
    """Map a page's ``results`` payload to ``{test id: outcome}``."""

    return {test: entry["outcome"] for test, entry in extract_test_entries(results).items()}


def summarize_outcomes(outcomes: Mapping[str, str]) -> dict[str, int]:
    return {
        "tests": len(outcomes),
        "withValue": sum(1 for outcome in outcomes.values() if outcome == OUTCOME_VALUE),
        "empty": sum(1 for outcome in outcomes.values() if outcome == OUTCOME_EMPTY),
    }


def validate_baseline(data: Any, source: str = "baseline") -> dict[str, Any]:
    document = data if isinstance(data, dict) else None
    if document is None:
        raise BaselineError(f"{source} must be an object")
    if document.get("schemaVersion") != SCHEMA_VERSION:
        raise BaselineError(f"{source}.schemaVersion must be {SCHEMA_VERSION}")
    pages = document.get("pages")
    if not isinstance(pages, dict):
        raise BaselineError(f"{source}.pages must be an object")
    for identifier, entry in pages.items():
        location = f"{source}.pages[{identifier!r}]"
        if not isinstance(entry, dict):
            raise BaselineError(f"{location} must be an object")
        if entry.get("status") not in {"ok", "failed"}:
            raise BaselineError(f"{location}.status must be 'ok' or 'failed'")
        tests = entry.get("tests")
        if not isinstance(tests, dict):
            raise BaselineError(f"{location}.tests must be an object")
        for test_id, outcome in tests.items():
            if outcome not in {OUTCOME_VALUE, OUTCOME_EMPTY}:
                raise BaselineError(
                    f"{location}.tests[{test_id!r}] must be {OUTCOME_VALUE!r} or {OUTCOME_EMPTY!r}"
                )
    return document


def load_baseline(path: Path) -> dict[str, Any] | None:
    """Read the tracked baseline, or return ``None`` when there is not one yet."""

    if not path.is_file():
        return None
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        raise BaselineError(f"{path} is not valid JSON: {error}") from error
    return validate_baseline(data, source=str(path))


def compare_to_baseline(
    status: str,
    outcomes: Mapping[str, str],
    baseline_entry: Mapping[str, Any] | None,
) -> dict[str, Any]:
    """Diff one page's outcomes against its baseline entry.

    Regressions are deliberately narrow: a page that stops running at all, and a
    test that used to produce a value and now produces nothing or is gone.  A
    test the upstream corpus removed reads as ``missing`` too, which is why a
    missing test is only a regression when its baseline outcome was a value —
    and why the report names removals separately so a reader can tell the two
    apart.
    """

    if baseline_entry is None:
        return {
            "compared": False,
            "regressions": [],
            "improvements": [],
            "newTests": sorted(outcomes),
            "removedTests": [],
        }

    baseline_status = baseline_entry.get("status", "failed")
    baseline_tests = baseline_entry.get("tests", {})

    regressions: list[dict[str, str]] = []
    improvements: list[dict[str, str]] = []

    if baseline_status == "ok" and status != "ok":
        regressions.append({"test": "(page)", "from": "ok", "to": status})
    elif baseline_status != "ok" and status == "ok":
        improvements.append({"test": "(page)", "from": baseline_status, "to": status})

    for test_id, baseline_outcome in sorted(baseline_tests.items()):
        current = outcomes.get(test_id, OUTCOME_MISSING)
        if current == baseline_outcome:
            continue
        change = {"test": test_id, "from": baseline_outcome, "to": current}
        if baseline_outcome == OUTCOME_VALUE:
            regressions.append(change)
        elif current == OUTCOME_VALUE:
            improvements.append(change)

    new_tests = sorted(set(outcomes) - set(baseline_tests))
    removed_tests = sorted(set(baseline_tests) - set(outcomes))

    return {
        "compared": True,
        "regressions": regressions,
        "improvements": improvements,
        "newTests": new_tests,
        "removedTests": removed_tests,
    }


def build_baseline(results: Sequence[Mapping[str, Any]], *, previous: Mapping[str, Any] | None = None) -> dict[str, Any]:
    """Build a baseline document from this run, keeping pages it did not cover.

    A partial run (``--pages``) must not silently delete the pages it skipped,
    so the previous baseline's entries for them are carried over unchanged.
    """

    pages: dict[str, Any] = {}
    if previous:
        pages.update({key: value for key, value in previous.get("pages", {}).items()})

    for result in results:
        pages[result["id"]] = {
            "status": result["status"],
            "tests": dict(sorted(result["outcomes"].items())),
        }

    return {
        "schemaVersion": SCHEMA_VERSION,
        "generatedAt": utc_now(),
        "notes": OBSERVATIONAL_NOTICE,
        "pages": dict(sorted(pages.items())),
    }


def json_type_name(value: Any) -> str:
    """Name a JSON value's type the way the pages' own payloads are shaped."""

    if value is None:
        return "null"
    if isinstance(value, bool):
        return "boolean"
    if isinstance(value, (int, float)):
        return "number"
    if isinstance(value, str):
        return "string"
    if isinstance(value, list):
        return "array"
    if isinstance(value, dict):
        return "object"
    return type(value).__name__


def describe_value(value: Any, limit: int = VALUE_EXCERPT_CHARS) -> str:
    """Render one probe's value as a single bounded line of evidence.

    A fingerprinting probe can return a whole object graph, and a report that
    pasted it whole would bury the finding it is evidence for, so the excerpt is
    truncated rather than summarized: a reader has to be able to recognize the
    real value in it.
    """

    try:
        text = json.dumps(value, sort_keys=True, ensure_ascii=False)
    except (TypeError, ValueError):
        text = str(value)
    text = " ".join(text.split())
    return text if len(text) <= limit else f"{text[: limit - 1]}…"


def load_reference_document(path: Path) -> dict[str, Any] | None:
    """Read one page's Chromium reference, or ``None`` when there is not one.

    A malformed or unreadable reference is treated as an absent one rather than
    an error: the reference is evidence for a comparison, and losing it must
    degrade the report to "not compared" instead of failing the Broiler run it
    is meant to describe.
    """

    if not path.is_file():
        return None
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    if not isinstance(data, dict) or data.get("schemaVersion") != SCHEMA_VERSION:
        return None
    return data


def classify_parity(broiler_outcome: str, reference_outcome: str) -> str:
    """Relate one probe's Broiler outcome to the same probe in Chromium."""

    broiler_has = broiler_outcome == OUTCOME_VALUE
    reference_has = reference_outcome == OUTCOME_VALUE
    if broiler_has and reference_has:
        return PARITY_MATCH
    if reference_has:
        return PARITY_GAP
    if broiler_has:
        return PARITY_AHEAD
    return PARITY_NEITHER


def compare_to_reference(
    broiler_entries: Mapping[str, Mapping[str, Any]],
    reference: Mapping[str, Any] | None,
) -> dict[str, Any]:
    """Diff one page's probes against the same page as Chromium ran it.

    Only ``gap`` is a Broiler finding.  ``neither`` covers the probes no engine
    answered here — a page that needs a return navigation, a corpus endpoint
    that is down, a probe that needs a permission prompt — and reporting those
    as Broiler gaps would drown the ones that are.  ``broiler-only`` is kept
    because it is nearly always a sign the two runs saw different pages, which
    is worth seeing rather than hiding.
    """

    absent = {
        "available": False,
        "referenceStatus": (reference or {}).get("status"),
        "reason": (reference or {}).get("reason") or "no Chromium reference was captured",
        "summary": {"tests": 0, PARITY_MATCH: 0, PARITY_GAP: 0, PARITY_AHEAD: 0, PARITY_NEITHER: 0},
        "gaps": [],
        "broilerOnly": [],
        "neither": [],
        "divergentTypes": [],
    }
    if reference is None or reference.get("status") != "ok":
        return absent

    reference_entries = extract_test_entries(reference.get("results"))
    if not reference_entries:
        absent["reason"] = "the Chromium reference has no results"
        return absent

    gaps: list[dict[str, Any]] = []
    broiler_only: list[dict[str, Any]] = []
    neither: list[str] = []
    divergent: list[dict[str, Any]] = []
    counts = {PARITY_MATCH: 0, PARITY_GAP: 0, PARITY_AHEAD: 0, PARITY_NEITHER: 0}

    for test in sorted(set(reference_entries) | set(broiler_entries)):
        broiler = broiler_entries.get(test, {})
        expected = reference_entries.get(test, {})
        parity = classify_parity(
            broiler.get("outcome", OUTCOME_MISSING),
            expected.get("outcome", OUTCOME_MISSING),
        )
        counts[parity] += 1
        if parity == PARITY_GAP:
            gaps.append({
                "test": test,
                "name": expected.get("name", test),
                "broiler": broiler.get("outcome", OUTCOME_MISSING),
                "referenceType": json_type_name(expected.get("value")),
                "referenceValue": describe_value(expected.get("value")),
            })
        elif parity == PARITY_AHEAD:
            broiler_only.append({
                "test": test,
                "name": broiler.get("name", test),
                "reference": expected.get("outcome", OUTCOME_MISSING),
                "broilerValue": describe_value(broiler.get("value")),
            })
        elif parity == PARITY_NEITHER:
            neither.append(test)
        elif json_type_name(broiler.get("value")) != json_type_name(expected.get("value")):
            # Both engines answered, in different shapes. Not a gap — the value
            # of a fingerprinting probe legitimately differs between engines —
            # but a type that differs usually means a stub rather than a result.
            divergent.append({
                "test": test,
                "broilerType": json_type_name(broiler.get("value")),
                "referenceType": json_type_name(expected.get("value")),
                "broilerValue": describe_value(broiler.get("value")),
                "referenceValue": describe_value(expected.get("value")),
            })

    return {
        "available": True,
        "referenceStatus": "ok",
        "reason": None,
        "summary": {"tests": sum(counts.values()), **counts},
        "gaps": gaps,
        "broilerOnly": broiler_only,
        "neither": neither,
        "divergentTypes": divergent,
    }


def reference_metadata(references: Sequence[Mapping[str, Any]]) -> dict[str, Any]:
    """Describe the browser the references came from, when any of them did."""

    for reference in references:
        browser = reference.get("browser")
        if isinstance(browser, dict) and browser.get("chromiumVersion"):
            return {
                "engine": REFERENCE_ENGINE,
                "chromiumVersion": browser.get("chromiumVersion"),
                "playwrightVersion": browser.get("playwrightVersion"),
            }
    return {"engine": REFERENCE_ENGINE, "chromiumVersion": None, "playwrightVersion": None}


def _read_log_excerpt(path: Path, limit: int = LOG_EXCERPT_BYTES) -> tuple[str, bool]:
    try:
        data = path.read_bytes()
    except OSError:
        return "", False
    truncated = len(data) > limit
    return data[-limit:].decode("utf-8", errors="replace"), truncated


def _command_result(
    command: Sequence[str],
    *,
    cwd: Path,
    timeout: float,
    stdout_path: Path,
    stderr_path: Path,
) -> dict[str, Any]:
    """Run one child process, capturing its streams to files rather than memory."""

    stdout_path.parent.mkdir(parents=True, exist_ok=True)
    record: dict[str, Any] = {"command": " ".join(shlex.quote(part) for part in command)}
    try:
        with stdout_path.open("wb") as stdout_file, stderr_path.open("wb") as stderr_file:
            completed = subprocess.run(  # noqa: S603 - fixed argv, no shell
                list(command),
                cwd=str(cwd),
                stdout=stdout_file,
                stderr=stderr_file,
                timeout=timeout,
                check=False,
            )
        record["exitCode"] = completed.returncode
        record["timedOut"] = False
    except subprocess.TimeoutExpired:
        record["exitCode"] = None
        record["timedOut"] = True
    except OSError as error:
        record["exitCode"] = None
        record["timedOut"] = False
        record["error"] = str(error)

    stderr_text, truncated = _read_log_excerpt(stderr_path)
    record["stderrExcerpt"] = stderr_text
    record["stderrTruncated"] = truncated
    return record


def _command_text(command: Sequence[str], cwd: Path) -> str | None:
    try:
        completed = subprocess.run(  # noqa: S603 - fixed argv, no shell
            list(command),
            cwd=str(cwd),
            capture_output=True,
            text=True,
            timeout=60,
            check=False,
        )
    except (OSError, subprocess.SubprocessError):
        return None
    return completed.stdout.strip() or None


def collect_run_environment(repo_root: Path, dotnet: str) -> dict[str, Any]:
    return {
        "timestamp": utc_now(),
        "platform": platform.platform(),
        "python": platform.python_version(),
        "dotnetSdkVersion": _command_text([dotnet, "--version"], repo_root),
        "gitCommit": _command_text(["git", "rev-parse", "HEAD"], repo_root),
    }


def locate_cli_dll(repo_root: Path, configuration: str) -> Path | None:
    expected = repo_root / "src" / "Broiler.Cli" / "bin" / configuration / "net10.0" / "Broiler.Cli.dll"
    if expected.is_file():
        return expected
    candidates = sorted(
        (repo_root / "src" / "Broiler.Cli" / "bin" / configuration).glob("**/Broiler.Cli.dll"),
        key=lambda path: path.stat().st_mtime,
        reverse=True,
    )
    return candidates[0] if candidates else None


def parse_evaluation_report(data: Any, results_index: int) -> dict[str, Any]:
    """Read one ``--evaluate-page`` report into a results payload or a reason it has none."""

    if not isinstance(data, dict):
        return {"results": None, "reason": "the evaluation report is not a JSON object"}

    evaluations = data.get("evaluations")
    if not isinstance(evaluations, list) or len(evaluations) <= results_index:
        return {"results": None, "reason": "the evaluation report has no results expression"}

    evaluation = evaluations[results_index]
    if not isinstance(evaluation, dict):
        return {"results": None, "reason": "the results evaluation is not an object"}

    if evaluation.get("error"):
        return {"results": None, "reason": f"the results expression threw: {evaluation['error']}"}

    value = evaluation.get("value")
    if value is None:
        return {"results": None, "reason": "the page did not define a results object"}

    try:
        parsed = json.loads(value)
    except (TypeError, json.JSONDecodeError):
        return {"results": None, "reason": "the results expression did not produce JSON"}

    if not isinstance(parsed, dict) or not isinstance(parsed.get("results"), list):
        return {"results": None, "reason": "the results object has no results array"}

    return {"results": parsed, "reason": None}


def run_page(
    dotnet: str,
    cli_dll: Path,
    manifest: Mapping[str, Any],
    page: Mapping[str, Any],
    repo_root: Path,
    output_root: Path,
) -> dict[str, Any]:
    """Evaluate one page in fresh child processes until one produces results."""

    config = resolve_effective_config(manifest, page)
    url = page_url(manifest, page)
    page_dir = output_root / "pages" / page["id"]
    page_dir.mkdir(parents=True, exist_ok=True)

    expressions = [config["startExpression"], config["resultsExpression"]]
    results_index = len(expressions) - 1
    hard_timeout = int(config["timeoutSeconds"]) + COMMAND_TIMEOUT_MARGIN_SECONDS

    attempts: list[dict[str, Any]] = []
    payload: Any = None
    reason: str | None = "the page was never evaluated"

    for attempt_number in range(1, int(config["attempts"]) + 1):
        report_path = page_dir / f"attempt-{attempt_number}.json"
        command = [
            dotnet,
            str(cli_dll),
            "--evaluate-page",
            url,
            "--output",
            str(report_path),
            "--timeout",
            str(config["timeoutSeconds"]),
        ]
        for expression in expressions:
            command += ["--evaluate", expression]

        record = _command_result(
            command,
            cwd=repo_root,
            timeout=hard_timeout,
            stdout_path=page_dir / f"attempt-{attempt_number}.stdout.log",
            stderr_path=page_dir / f"attempt-{attempt_number}.stderr.log",
        )
        record["attempt"] = attempt_number
        record["hardTimeoutSeconds"] = hard_timeout

        if record.get("exitCode") == 0 and report_path.is_file():
            try:
                report = json.loads(report_path.read_text(encoding="utf-8"))
            except (OSError, json.JSONDecodeError) as error:
                report = None
                parsed = {"results": None, "reason": f"the evaluation report is unreadable: {error}"}
            else:
                parsed = parse_evaluation_report(report, results_index)
            if isinstance(report, dict):
                record["durationMs"] = report.get("durationMs")
                record["reportPath"] = report_path.relative_to(output_root).as_posix()
        elif record.get("timedOut"):
            parsed = {"results": None, "reason": f"the run exceeded {hard_timeout}s"}
        else:
            parsed = {
                "results": None,
                "reason": f"Broiler.Cli exited with {record.get('exitCode')}",
            }

        record["accepted"] = parsed["results"] is not None
        attempts.append(record)
        payload, reason = parsed["results"], parsed["reason"]
        if record["accepted"]:
            break

    entries = extract_test_entries(payload) if payload is not None else {}
    outcomes = {test: entry["outcome"] for test, entry in entries.items()}
    status = "ok" if payload is not None and outcomes else "failed"
    if payload is not None and not outcomes:
        reason = "the page produced an empty results array"

    if payload is not None:
        (page_dir / "results.json").write_text(
            json.dumps(payload, indent=2, sort_keys=False) + "\n", encoding="utf-8"
        )

    return {
        "id": page["id"],
        "name": page["name"],
        "url": url,
        "description": page["description"],
        "tags": list(page.get("tags", [])),
        "status": status,
        "reason": None if status == "ok" else reason,
        "attemptCount": len(attempts),
        "attempts": attempts,
        "outcomes": outcomes,
        "entries": entries,
        "summary": summarize_outcomes(outcomes),
        "resultsPath": (page_dir / "results.json").relative_to(output_root).as_posix()
        if payload is not None
        else None,
    }


def summarize_run(results: Sequence[Mapping[str, Any]]) -> dict[str, Any]:
    comparisons = [result.get("comparison", {}) for result in results]
    parities = [result.get("parity", {}) for result in results]
    compared = [parity for parity in parities if parity.get("available")]
    return {
        "pages": len(results),
        "pagesOk": sum(1 for result in results if result["status"] == "ok"),
        "pagesFailed": sum(1 for result in results if result["status"] != "ok"),
        "tests": sum(result["summary"]["tests"] for result in results),
        "testsWithValue": sum(result["summary"]["withValue"] for result in results),
        "testsEmpty": sum(result["summary"]["empty"] for result in results),
        "regressions": sum(len(comparison.get("regressions", [])) for comparison in comparisons),
        "improvements": sum(len(comparison.get("improvements", [])) for comparison in comparisons),
        "newTests": sum(len(comparison.get("newTests", [])) for comparison in comparisons),
        "removedTests": sum(len(comparison.get("removedTests", [])) for comparison in comparisons),
        "pagesCompared": len(compared),
        "referenceTests": sum(parity["summary"]["tests"] for parity in compared),
        "parity": sum(parity["summary"][PARITY_MATCH] for parity in compared),
        "gaps": sum(parity["summary"][PARITY_GAP] for parity in compared),
        "broilerOnly": sum(parity["summary"][PARITY_AHEAD] for parity in compared),
        "neither": sum(parity["summary"][PARITY_NEITHER] for parity in compared),
        "divergentTypes": sum(len(parity.get("divergentTypes", [])) for parity in compared),
    }


def parity_percent(summary: Mapping[str, Any]) -> float:
    """Share of the probes Chromium answered that Broiler answered too."""

    answered = summary.get("parity", 0) + summary.get("gaps", 0)
    return round(100.0 * summary.get("parity", 0) / answered, 1) if answered else 0.0


def coverage_percent(summary: Mapping[str, Any]) -> float:
    total = summary.get("tests", 0)
    return round(100.0 * summary.get("testsWithValue", 0) / total, 1) if total else 0.0


def _single_line(value: Any, limit: int = 300) -> str:
    text = " ".join(str(value).split())
    return text if len(text) <= limit else f"{text[: limit - 1]}…"


def _escape_table_cell(value: Any) -> str:
    """Keep a probe's own value from breaking the table that reports it.

    The values come from third-party pages, so they contain pipes and backticks
    often enough that an unescaped cell would silently reshape the row.
    """

    return _single_line(value).replace("\\", "\\\\").replace("|", "\\|").replace("`", "'")


def build_gap_document(aggregate: Mapping[str, Any]) -> dict[str, Any]:
    """Collect the Chromium-versus-Broiler findings on their own.

    The gap document is the part of a run that outlives it: it is what a reader
    turns into an issue, so it carries the evidence (what Chromium returned and
    in what shape) rather than a count, and it names the pages that could not be
    compared so an empty gap list is never mistaken for parity.
    """

    pages: list[dict[str, Any]] = []
    uncompared: list[dict[str, Any]] = []
    for result in aggregate["results"]:
        parity = result.get("parity", {})
        if not parity.get("available"):
            uncompared.append({
                "id": result["id"],
                "name": result["name"],
                "url": result["url"],
                "reason": parity.get("reason") or "no Chromium reference was captured",
            })
            continue
        if not parity["gaps"] and not parity["broilerOnly"] and not parity["divergentTypes"]:
            continue
        pages.append({
            "id": result["id"],
            "name": result["name"],
            "url": result["url"],
            "description": result["description"],
            "tags": list(result.get("tags", [])),
            "broilerStatus": result["status"],
            "summary": parity["summary"],
            "gaps": parity["gaps"],
            "broilerOnly": parity["broilerOnly"],
            "divergentTypes": parity["divergentTypes"],
        })

    return {
        "schemaVersion": SCHEMA_VERSION,
        "generatedAt": utc_now(),
        "notes": COMPARISON_NOTICE,
        "reference": aggregate.get("reference", {}),
        "environment": aggregate["environment"],
        "summary": {
            key: aggregate["summary"][key]
            for key in ("pages", "pagesCompared", "parity", "gaps", "broilerOnly", "neither", "divergentTypes")
        },
        "pages": pages,
        "notCompared": uncompared,
    }


def render_gap_report(document: Mapping[str, Any]) -> str:
    """Render the gap document as the issue-ready Markdown a reader files from."""

    summary = document["summary"]
    reference = document.get("reference", {})
    lines = [
        "# Privacy test pages: Chromium versus Broiler",
        "",
        f"Generated {document['generatedAt']} · Chromium "
        f"`{reference.get('chromiumVersion') or 'n/a'}` via Playwright "
        f"`{reference.get('playwrightVersion') or 'n/a'}` · Broiler commit "
        f"`{document['environment'].get('gitCommit') or 'n/a'}`.",
        "",
        f"> {COMPARISON_NOTICE}",
        "",
        f"{summary['gaps']} probe(s) Chromium answered and Broiler did not, across "
        f"{summary['pagesCompared']}/{summary['pages']} compared page(s). "
        f"{summary['parity']} probe(s) agree; {summary['neither']} were answered by neither engine.",
        "",
    ]

    if document["notCompared"]:
        lines += ["## Pages with no Chromium reference", "", "| Page | Why |", "| --- | --- |"]
        lines += [
            f"| [{entry['name']}]({entry['url']}) | {_single_line(entry['reason'], 160)} |"
            for entry in document["notCompared"]
        ]
        lines.append("")

    if not document["pages"]:
        lines += ["No gaps were found on the compared pages.", ""]
        return "\n".join(lines)

    for page in document["pages"]:
        page_summary = page["summary"]
        lines += [
            f"## {page['name']} (`{page['id']}`)",
            "",
            page["description"],
            "",
            f"- Page: <{page['url']}>",
            f"- Probes: {page_summary[PARITY_MATCH]} in parity, **{page_summary[PARITY_GAP]} gap(s)**, "
            f"{page_summary[PARITY_NEITHER]} answered by neither engine.",
            "",
        ]
        if page["gaps"]:
            lines += [
                "### Probes Chromium answers and Broiler does not",
                "",
                "| Probe | Broiler | Chromium returns | Chromium value |",
                "| --- | --- | --- | --- |",
            ]
            lines += [
                f"| `{gap['test']}` | {gap['broiler']} | {gap['referenceType']} "
                f"| `{_escape_table_cell(gap['referenceValue'])}` |"
                for gap in page["gaps"]
            ]
            lines.append("")
        if page["divergentTypes"]:
            lines += [
                "### Probes both engines answer in different shapes",
                "",
                "| Probe | Broiler | Chromium |",
                "| --- | --- | --- |",
            ]
            lines += [
                f"| `{entry['test']}` | {entry['broilerType']}: "
                f"`{_escape_table_cell(entry['broilerValue'])}` | {entry['referenceType']}: "
                f"`{_escape_table_cell(entry['referenceValue'])}` |"
                for entry in page["divergentTypes"]
            ]
            lines.append("")
        if page["broilerOnly"]:
            lines += [
                "### Probes only Broiler answered",
                "",
                "These usually mean the two runs saw different pages rather than that "
                "Broiler is ahead; check the reference before reading them as a win.",
                "",
            ]
            lines += [f"- `{entry['test']}`: {entry['broilerValue']}" for entry in page["broilerOnly"]]
            lines.append("")

    return "\n".join(lines)


def render_markdown_report(aggregate: Mapping[str, Any]) -> str:
    summary = aggregate["summary"]
    environment = aggregate["environment"]
    reference = aggregate.get("reference", {})
    lines = [
        "# Privacy test pages",
        "",
        f"Source: [{aggregate['source']['repository']}]({aggregate['source']['repository']})",
        f"Run at {environment['timestamp']} on `{environment['platform']}`, "
        f".NET SDK `{environment.get('dotnetSdkVersion') or 'n/a'}`, "
        f"commit `{environment.get('gitCommit') or 'n/a'}`.",
        "",
        f"> {OBSERVATIONAL_NOTICE}",
        ">",
        f"> {COVERAGE_NOTICE}",
        ">",
        f"> {COMPARISON_NOTICE}",
        "",
        f"Reference engine: Chromium `{reference.get('chromiumVersion') or 'n/a'}` via "
        f"Playwright `{reference.get('playwrightVersion') or 'n/a'}`.",
        "",
        "## Totals",
        "",
        "| Metric | Value |",
        "| --- | --- |",
        f"| Pages run | {summary['pagesOk']}/{summary['pages']} |",
        f"| Tests carried out | {summary['testsWithValue']}/{summary['tests']} "
        f"({coverage_percent(summary)}%) |",
        f"| Pages compared with Chromium | {summary['pagesCompared']}/{summary['pages']} |",
        f"| Probes matching Chromium | {summary['parity']}/{summary['parity'] + summary['gaps']} "
        f"({parity_percent(summary)}%) |",
        f"| Gaps behind Chromium | {summary['gaps']} |",
        f"| Answered by neither engine | {summary['neither']} |",
        f"| Regressions | {summary['regressions']} |",
        f"| Improvements | {summary['improvements']} |",
        f"| New upstream tests | {summary['newTests']} |",
        f"| Removed upstream tests | {summary['removedTests']} |",
        "",
        "## Pages",
        "",
        "| Page | Status | Tests carried out | Matches Chromium | Gaps | Regressions |",
        "| --- | --- | --- | --- | --- | --- |",
    ]

    for result in aggregate["results"]:
        comparison = result.get("comparison", {})
        parity = result.get("parity", {})
        page_summary = result["summary"]
        coverage = (
            f"{page_summary['withValue']}/{page_summary['tests']}"
            if page_summary["tests"]
            else "n/a"
        )
        matches = (
            f"{parity['summary'][PARITY_MATCH]}/"
            f"{parity['summary'][PARITY_MATCH] + parity['summary'][PARITY_GAP]}"
            if parity.get("available")
            else "n/a"
        )
        gaps = str(parity["summary"][PARITY_GAP]) if parity.get("available") else "n/a"
        lines.append(
            f"| [{result['name']}]({result['url']}) | {result['status']} | {coverage} "
            f"| {matches} | {gaps} | {len(comparison.get('regressions', []))} |"
        )

    for result in aggregate["results"]:
        comparison = result.get("comparison", {})
        parity = result.get("parity", {})
        lines += ["", f"### {result['name']}", "", result["description"], ""]
        lines.append(f"- URL: <{result['url']}>")
        lines.append(f"- Status: `{result['status']}`")
        if result.get("reason"):
            lines.append(f"- Reason: {_single_line(result['reason'])}")
        if not comparison.get("compared", False):
            lines.append("- No baseline entry; this run establishes one.")
        if parity.get("available"):
            lines.append(
                f"- Chromium: {parity['summary'][PARITY_MATCH]} probe(s) in parity, "
                f"{parity['summary'][PARITY_GAP]} gap(s), "
                f"{parity['summary'][PARITY_NEITHER]} answered by neither engine."
            )
        else:
            lines.append(
                f"- Not compared with Chromium: {_single_line(parity.get('reason') or 'no reference', 160)}"
            )

        if parity.get("gaps"):
            lines += ["", "**Behind Chromium**", ""]
            lines += [
                f"- `{gap['test']}`: Chromium returns {gap['referenceType']} "
                f"`{_escape_table_cell(gap['referenceValue'])}`, Broiler {gap['broiler']}"
                for gap in parity["gaps"]
            ]

        for label, key in (
            ("Regressions", "regressions"),
            ("Improvements", "improvements"),
        ):
            changes = comparison.get(key, [])
            if changes:
                lines += ["", f"**{label}**", ""]
                lines += [
                    f"- `{change['test']}`: {change['from']} → {change['to']}" for change in changes
                ]

        for label, key in (("New upstream tests", "newTests"), ("Removed upstream tests", "removedTests")):
            names = comparison.get(key, [])
            if names:
                lines += ["", f"**{label}**", ""]
                lines += [f"- `{name}`" for name in names]

    return "\n".join(lines) + "\n"


def render_job_summary(aggregate: Mapping[str, Any]) -> str:
    summary = aggregate["summary"]
    reference = aggregate.get("reference", {})
    lines = [
        "## Privacy test pages",
        "",
        f"- Pages run: **{summary['pagesOk']}/{summary['pages']}**",
        f"- Tests carried out: **{summary['testsWithValue']}/{summary['tests']}** "
        f"({coverage_percent(summary)}%)",
        f"- Matching Chromium `{reference.get('chromiumVersion') or 'n/a'}`: "
        f"**{summary['parity']}/{summary['parity'] + summary['gaps']}** "
        f"({parity_percent(summary)}%) over {summary['pagesCompared']}/{summary['pages']} compared page(s)",
        f"- Gaps behind Chromium: **{summary['gaps']}**",
        f"- Regressions: **{summary['regressions']}** · improvements: {summary['improvements']}",
        f"- Upstream churn: {summary['newTests']} new, {summary['removedTests']} removed",
        "",
        f"_{OBSERVATIONAL_NOTICE}_",
        "",
    ]

    gapped = [
        (result, result["parity"])
        for result in aggregate["results"]
        if result.get("parity", {}).get("gaps")
    ]
    if gapped:
        lines += [
            "### Gaps behind Chromium",
            "",
            "| Page | Gaps | Probes |",
            "| --- | --- | --- |",
        ]
        for result, parity in gapped:
            probes = ", ".join(f"`{gap['test']}`" for gap in parity["gaps"][:8])
            if len(parity["gaps"]) > 8:
                probes += f", … {len(parity['gaps']) - 8} more"
            lines.append(f"| {result['name']} | {len(parity['gaps'])} | {probes} |")
        lines.append("")

    uncompared = [
        result for result in aggregate["results"] if not result.get("parity", {}).get("available")
    ]
    if uncompared:
        lines += ["### Pages with no Chromium reference", "", "| Page | Why |", "| --- | --- |"]
        for result in uncompared:
            reason = result.get("parity", {}).get("reason") or "no reference"
            lines.append(f"| {result['name']} | {_single_line(reason, 160)} |")
        lines.append("")

    regressed = [
        (result, change)
        for result in aggregate["results"]
        for change in result.get("comparison", {}).get("regressions", [])
    ]
    if regressed:
        lines += ["### Regressions", "", "| Page | Test | Was | Now |", "| --- | --- | --- | --- |"]
        for result, change in regressed:
            lines.append(
                f"| {result['name']} | `{change['test']}` | {change['from']} | {change['to']} |"
            )
        lines.append("")

    failed = [result for result in aggregate["results"] if result["status"] != "ok"]
    if failed:
        lines += ["### Pages that did not run", "", "| Page | Reason |", "| --- | --- |"]
        for result in failed:
            lines.append(f"| {result['name']} | {_single_line(result.get('reason'), 160)} |")
        lines.append("")

    return "\n".join(lines)


def render_html_report(aggregate: Mapping[str, Any]) -> str:
    summary = aggregate["summary"]
    environment = aggregate["environment"]
    reference = aggregate.get("reference", {})
    rows = []
    for result in aggregate["results"]:
        comparison = result.get("comparison", {})
        parity = result.get("parity", {})
        page_summary = result["summary"]
        regressions = comparison.get("regressions", [])
        detail = "".join(
            f"<li><code>{html.escape(change['test'])}</code>: "
            f"{html.escape(change['from'])} → {html.escape(change['to'])}</li>"
            for change in regressions
        )
        gaps = parity.get("gaps", [])
        gap_detail = "".join(
            f"<li><code>{html.escape(gap['test'])}</code>: Chromium returns "
            f"{html.escape(gap['referenceType'])} "
            f"<code>{html.escape(gap['referenceValue'])}</code></li>"
            for gap in gaps
        )
        if parity.get("available"):
            answered = parity["summary"][PARITY_MATCH] + parity["summary"][PARITY_GAP]
            matches = f"{parity['summary'][PARITY_MATCH]}/{answered}"
            gap_cell = f"{len(gaps)}{f'<ul>{gap_detail}</ul>' if gap_detail else ''}"
        else:
            matches = "n/a"
            gap_cell = (
                f"<span class=\"desc\">{html.escape(_single_line(parity.get('reason') or 'no reference', 160))}</span>"
            )
        rows.append(
            "<tr>"
            f"<td><a href=\"{html.escape(result['url'])}\">{html.escape(result['name'])}</a>"
            f"<div class=\"desc\">{html.escape(result['description'])}</div></td>"
            f"<td class=\"{'ok' if result['status'] == 'ok' else 'bad'}\">{html.escape(result['status'])}</td>"
            f"<td>{page_summary['withValue']}/{page_summary['tests']}</td>"
            f"<td>{matches}</td>"
            f"<td class=\"{'bad' if gaps else ''}\">{gap_cell}</td>"
            f"<td>{len(regressions)}{f'<ul>{detail}</ul>' if detail else ''}</td>"
            "</tr>"
        )

    return f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Broiler privacy test pages</title>
<style>
:root {{ color-scheme: light dark; }}
body {{ font: 16px/1.5 system-ui, sans-serif; margin: 2rem auto; max-width: 70rem; padding: 0 1rem; }}
table {{ border-collapse: collapse; width: 100%; }}
th, td {{ border: 1px solid #8884; padding: .5rem; text-align: left; vertical-align: top; }}
.desc {{ font-size: .85em; opacity: .75; }}
.ok {{ color: #197a29; }}
.bad {{ color: #b3261e; }}
ul {{ margin: .25rem 0 0; padding-left: 1.1rem; font-size: .85em; }}
.note {{ font-size: .9em; opacity: .8; }}
</style>
</head>
<body>
<h1>Broiler privacy test pages</h1>
<p class="note">{html.escape(OBSERVATIONAL_NOTICE)}</p>
<p class="note">{html.escape(COVERAGE_NOTICE)}</p>
<p class="note">{html.escape(COMPARISON_NOTICE)}</p>
<p>Source: <a href="{html.escape(aggregate['source']['repository'])}">
{html.escape(aggregate['source']['repository'])}</a><br>
{html.escape(environment['timestamp'])} · {html.escape(environment['platform'])} ·
.NET SDK {html.escape(str(environment.get('dotnetSdkVersion') or 'n/a'))} ·
commit {html.escape(str(environment.get('gitCommit') or 'n/a'))} ·
Chromium {html.escape(str(reference.get('chromiumVersion') or 'n/a'))} via Playwright
{html.escape(str(reference.get('playwrightVersion') or 'n/a'))}</p>
<p><strong>{summary['pagesOk']}/{summary['pages']}</strong> pages ran ·
<strong>{summary['testsWithValue']}/{summary['tests']}</strong> tests carried out
({coverage_percent(summary)}%) ·
<strong>{summary['parity']}/{summary['parity'] + summary['gaps']}</strong> probes match Chromium
({parity_percent(summary)}%) ·
<strong>{summary['gaps']}</strong> gaps ·
<strong>{summary['regressions']}</strong> regressions ·
{summary['improvements']} improvements</p>
<table>
<thead><tr><th>Page</th><th>Status</th><th>Carried out</th><th>Matches Chromium</th><th>Gaps</th><th>Regressions</th></tr></thead>
<tbody>
{''.join(rows)}
</tbody>
</table>
</body>
</html>
"""


REPORT_FILENAMES = (
    "results.json",
    "report.md",
    "report.html",
    "job-summary.md",
    "gaps.json",
    "gaps.md",
    "reference.log",
)


def capture_references(
    node: str,
    script: Path,
    manifest_path: Path,
    output_root: Path,
    page_ids: Sequence[str],
    repo_root: Path,
    timeout: float,
) -> dict[str, Any]:
    """Run the Chromium capture for the selected pages, into the same tree.

    The Node runner writes one ``reference.json`` per page and reports its own
    per-page failures there, so a non-zero exit is recorded rather than raised:
    a page Chromium could not run is a page that cannot be compared, not a
    reason to abandon the Broiler run this is meant to describe.
    """

    command = [
        node,
        str(script),
        "--manifest",
        str(manifest_path),
        "--output-dir",
        str(output_root),
    ]
    if page_ids:
        command += ["--pages", ",".join(page_ids)]

    environment = dict(os.environ)
    node_modules = repo_root / "tests" / "wpt" / "node_modules"
    if node_modules.is_dir():
        existing = environment.get("NODE_PATH")
        environment["NODE_PATH"] = f"{node_modules}{os.pathsep}{existing}" if existing else str(node_modules)

    log_path = output_root / "reference.log"
    log_path.parent.mkdir(parents=True, exist_ok=True)
    record: dict[str, Any] = {"command": " ".join(shlex.quote(part) for part in command)}
    try:
        with log_path.open("wb") as log_file:
            completed = subprocess.run(  # noqa: S603 - fixed argv, no shell
                command,
                cwd=str(repo_root),
                stdout=log_file,
                stderr=subprocess.STDOUT,
                env=environment,
                timeout=timeout,
                check=False,
            )
        record["exitCode"] = completed.returncode
        record["timedOut"] = False
    except subprocess.TimeoutExpired:
        record["exitCode"] = None
        record["timedOut"] = True
    except OSError as error:
        record["exitCode"] = None
        record["timedOut"] = False
        record["error"] = str(error)

    excerpt, truncated = _read_log_excerpt(log_path)
    record["logPath"] = log_path.relative_to(output_root).as_posix()
    record["logExcerpt"] = excerpt
    record["logTruncated"] = truncated
    return record


def clear_previous_run(output_root: Path, *, keep_references: bool = False) -> None:
    """Remove the artefacts of the previous run, and only those.

    A stale ``pages/<id>/`` would otherwise be read as evidence for a run that
    did not produce it.  Deleting the directory wholesale would do that job too,
    and would delete whatever else a caller had pointed ``--output-dir`` at, so
    this removes the files and sub-tree this runner writes and nothing else.

    ``keep_references`` is what makes ``--skip-reference`` mean anything: the
    Chromium captures are the one artefact a run can legitimately inherit, so
    they survive while everything the Broiler run produces is cleared.
    """

    for name in REPORT_FILENAMES:
        if keep_references and name == "reference.log":
            continue
        (output_root / name).unlink(missing_ok=True)

    pages_root = output_root / "pages"
    if pages_root.is_dir():
        if not keep_references:
            shutil.rmtree(pages_root)
        else:
            for entry in sorted(pages_root.iterdir()):
                if entry.is_dir():
                    for item in sorted(entry.iterdir()):
                        if item.name != "reference.json":
                            shutil.rmtree(item) if item.is_dir() else item.unlink()
                else:
                    entry.unlink()
    output_root.mkdir(parents=True, exist_ok=True)


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=False) + "\n", encoding="utf-8")


def create_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--manifest", default=DEFAULT_MANIFEST, help=f"page manifest (default: {DEFAULT_MANIFEST})")
    parser.add_argument("--baseline", default=DEFAULT_BASELINE, help=f"tracked baseline (default: {DEFAULT_BASELINE})")
    parser.add_argument("--output-dir", default=DEFAULT_OUTPUT_DIR, help=f"report directory (default: {DEFAULT_OUTPUT_DIR})")
    parser.add_argument(
        "--pages",
        action="append",
        help="comma-separated page ids to run; repeatable. Omit to run every page.",
    )
    parser.add_argument("--configuration", default="Release", help="dotnet build configuration (default: Release)")
    parser.add_argument("--skip-build", action="store_true", help="reuse the existing Broiler.Cli build")
    parser.add_argument(
        "--reference-script",
        default=DEFAULT_REFERENCE_SCRIPT,
        help=f"Chromium capture script (default: {DEFAULT_REFERENCE_SCRIPT})",
    )
    parser.add_argument("--node", default=None, help="node executable to run the Chromium capture with")
    parser.add_argument(
        "--skip-reference",
        action="store_true",
        help="reuse the Chromium references already in the output directory instead of capturing them",
    )
    parser.add_argument(
        "--no-reference",
        action="store_true",
        help="run Broiler only; report no Chromium comparison at all",
    )
    parser.add_argument(
        "--require-reference",
        action="store_true",
        help="exit non-zero when any selected page could not be compared with Chromium",
    )
    parser.add_argument(
        "--update-baseline",
        action="store_true",
        help="rewrite the baseline from this run instead of comparing against it",
    )
    parser.add_argument(
        "--fail-on-regression",
        action="store_true",
        help="exit non-zero when a test that used to produce a value no longer does",
    )
    parser.add_argument(
        "--fail-on-incomplete",
        action="store_true",
        help="exit non-zero when any selected page could not be evaluated",
    )
    return parser


def _resolve_input_path(value: str, repo_root: Path) -> Path:
    path = Path(value)
    return path if path.is_absolute() else repo_root / path


def main(argv: Sequence[str] | None = None) -> int:
    args = create_argument_parser().parse_args(argv)
    repo_root = Path(__file__).resolve().parent.parent

    manifest_path = _resolve_input_path(args.manifest, repo_root)
    baseline_path = _resolve_input_path(args.baseline, repo_root)
    output_root = _resolve_input_path(args.output_dir, repo_root)

    try:
        manifest = load_manifest(manifest_path)
    except ManifestError as error:
        print(f"Manifest error: {error}", file=sys.stderr)
        return 2

    try:
        baseline = load_baseline(baseline_path)
    except BaselineError as error:
        print(f"Baseline error: {error}", file=sys.stderr)
        return 2

    selected = parse_page_selection(args.pages)
    known = {page["id"]: page for page in manifest["pages"]}
    unknown = [identifier for identifier in selected if identifier not in known]
    if unknown:
        print(f"Unknown page id(s): {', '.join(unknown)}", file=sys.stderr)
        return 2
    pages = [known[identifier] for identifier in selected] if selected else list(manifest["pages"])

    clear_previous_run(output_root, keep_references=args.skip_reference)

    # Chromium first: its references land in the same per-page directories the
    # Broiler run then fills, and capturing them before the long .NET build
    # keeps a browser failure visible early rather than an hour in.
    reference_run: dict[str, Any] | None = None
    if not args.no_reference and not args.skip_reference:
        node = args.node or shutil.which("node") or "node"
        script_path = _resolve_input_path(args.reference_script, repo_root)
        reference_timeout = sum(
            int(resolve_effective_config(manifest, page)["timeoutSeconds"])
            * int(resolve_effective_config(manifest, page)["attempts"])
            for page in pages
        ) + REFERENCE_TIMEOUT_MARGIN_SECONDS
        print(f"--- Capturing Chromium references for {len(pages)} page(s) ---", flush=True)
        reference_run = capture_references(
            node,
            script_path,
            manifest_path,
            output_root,
            [page["id"] for page in pages],
            repo_root,
            reference_timeout,
        )
        print(
            f"    chromium capture exited with {reference_run.get('exitCode')}"
            f"{' (timed out)' if reference_run.get('timedOut') else ''}",
            flush=True,
        )

    dotnet = shutil.which("dotnet") or "dotnet"
    if not args.skip_build:
        print("--- Building Broiler.Cli once ---")
        build = subprocess.run(  # noqa: S603 - fixed argv, no shell
            [
                dotnet,
                "build",
                str(repo_root / "src" / "Broiler.Cli" / "Broiler.Cli.csproj"),
                "-c",
                args.configuration,
            ],
            cwd=str(repo_root),
            check=False,
        )
        if build.returncode != 0:
            print("Broiler.Cli build failed.", file=sys.stderr)

    cli_dll = locate_cli_dll(repo_root, args.configuration)
    environment = collect_run_environment(repo_root, dotnet)

    results: list[dict[str, Any]] = []
    references: list[dict[str, Any]] = []
    for page in pages:
        print(f"--- {page['id']}: {page_url(manifest, page)} ---", flush=True)
        if cli_dll is None:
            result = {
                "id": page["id"],
                "name": page["name"],
                "url": page_url(manifest, page),
                "description": page["description"],
                "tags": list(page.get("tags", [])),
                "status": "failed",
                "reason": "Broiler.Cli build is unavailable",
                "attemptCount": 0,
                "attempts": [],
                "outcomes": {},
                "entries": {},
                "summary": summarize_outcomes({}),
                "resultsPath": None,
            }
        else:
            result = run_page(dotnet, cli_dll, manifest, page, repo_root, output_root)

        baseline_entry = (baseline or {}).get("pages", {}).get(page["id"]) if baseline else None
        result["comparison"] = compare_to_baseline(
            result["status"], result["outcomes"], baseline_entry
        )

        reference = (
            None
            if args.no_reference
            else load_reference_document(output_root / "pages" / page["id"] / "reference.json")
        )
        if reference is not None:
            references.append(reference)
            result["referencePath"] = f"pages/{page['id']}/reference.json"
        result["parity"] = compare_to_reference(result.pop("entries"), reference)
        if args.no_reference:
            result["parity"]["reason"] = "the Chromium comparison was disabled for this run"

        page_summary = result["summary"]
        parity_summary = result["parity"]["summary"]
        print(
            f"    {result['status']}: {page_summary['withValue']}/{page_summary['tests']} carried out, "
            f"{parity_summary[PARITY_GAP]} behind Chromium, "
            f"{len(result['comparison']['regressions'])} regression(s)",
            flush=True,
        )
        results.append(result)

    aggregate = {
        "schemaVersion": SCHEMA_VERSION,
        "source": manifest["source"],
        "environment": environment,
        "reference": {
            **reference_metadata(references),
            "captured": reference_run is not None,
            "reused": args.skip_reference,
            "disabled": args.no_reference,
            "run": reference_run,
        },
        "baseline": {
            "path": baseline_path.relative_to(repo_root).as_posix()
            if baseline_path.is_relative_to(repo_root)
            else str(baseline_path),
            "present": baseline is not None,
            "generatedAt": (baseline or {}).get("generatedAt"),
        },
        "summary": summarize_run(results),
        "results": results,
    }

    gaps = build_gap_document(aggregate)

    write_json(output_root / "results.json", aggregate)
    write_json(output_root / "gaps.json", gaps)
    (output_root / "gaps.md").write_text(render_gap_report(gaps), encoding="utf-8")
    (output_root / "report.md").write_text(render_markdown_report(aggregate), encoding="utf-8")
    (output_root / "report.html").write_text(render_html_report(aggregate), encoding="utf-8")
    (output_root / "job-summary.md").write_text(render_job_summary(aggregate), encoding="utf-8")

    if args.update_baseline:
        write_json(baseline_path, build_baseline(results, previous=baseline))
        print(f"Baseline written to {baseline_path}")

    summary = aggregate["summary"]
    print(
        f"\nPages {summary['pagesOk']}/{summary['pages']} · "
        f"tests carried out {summary['testsWithValue']}/{summary['tests']} "
        f"({coverage_percent(summary)}%) · "
        f"matching Chromium {summary['parity']}/{summary['parity'] + summary['gaps']} "
        f"({parity_percent(summary)}%) over {summary['pagesCompared']}/{summary['pages']} page(s) · "
        f"gaps {summary['gaps']} · "
        f"regressions {summary['regressions']} · improvements {summary['improvements']}"
    )
    print(f"Reports written to {output_root}")

    if args.fail_on_regression and summary["regressions"] and not args.update_baseline:
        print(f"Failing: {summary['regressions']} regression(s) against the baseline.", file=sys.stderr)
        return 1
    if args.fail_on_incomplete and summary["pagesFailed"]:
        print(f"Failing: {summary['pagesFailed']} page(s) could not be evaluated.", file=sys.stderr)
        return 1
    if args.require_reference and summary["pagesCompared"] < summary["pages"]:
        uncompared = summary["pages"] - summary["pagesCompared"]
        print(f"Failing: {uncompared} page(s) had no Chromium reference to compare.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
