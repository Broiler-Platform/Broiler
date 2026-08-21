import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "run-privacy-test-pages.py"
SPEC = importlib.util.spec_from_file_location("privacy_test_pages", SCRIPT_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)
REPO_ROOT = SCRIPT_PATH.parents[1]
MANIFEST_PATH = REPO_ROOT / "tests" / "privacy-test-pages" / "pages.json"
BASELINE_PATH = REPO_ROOT / "tests" / "privacy-test-pages" / "baseline.json"


class ManifestTests(unittest.TestCase):
    def test_checked_in_manifest_describes_the_privacy_protections_corpus(self) -> None:
        manifest = MODULE.load_manifest(MANIFEST_PATH)
        pages = {page["id"]: page for page in manifest["pages"]}

        self.assertTrue({"fingerprinting", "request-blocking", "storage-blocking"}.issubset(pages))
        self.assertEqual(
            "https://github.com/duckduckgo/privacy-test-pages", manifest["source"]["repository"]
        )
        self.assertTrue(manifest["source"]["baseUrl"].startswith("https://"))
        self.assertTrue(
            all(page["path"].startswith("/privacy-protections/") for page in pages.values())
        )

    def test_every_page_resolves_to_an_https_url(self) -> None:
        manifest = MODULE.load_manifest(MANIFEST_PATH)

        for page in manifest["pages"]:
            self.assertTrue(MODULE.page_url(manifest, page).startswith("https://"))

    def test_duplicate_page_ids_are_rejected(self) -> None:
        manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        manifest["pages"].append(copy.deepcopy(manifest["pages"][0]))

        with self.assertRaisesRegex(MODULE.ManifestError, "duplicates"):
            MODULE.validate_manifest(manifest)

    def test_unknown_page_fields_are_rejected(self) -> None:
        manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        manifest["pages"][0]["surprise"] = True

        with self.assertRaisesRegex(MODULE.ManifestError, "unsupported keys"):
            MODULE.validate_manifest(manifest)

    def test_relative_paths_are_rejected(self) -> None:
        manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        manifest["pages"][0]["path"] = "privacy-protections/fingerprinting/?run"

        with self.assertRaisesRegex(MODULE.ManifestError, "must start with"):
            MODULE.validate_manifest(manifest)

    def test_plain_http_origins_are_rejected(self) -> None:
        manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        manifest["source"]["baseUrl"] = "http://privacy-test-pages.site"

        with self.assertRaisesRegex(MODULE.ManifestError, "https"):
            MODULE.validate_manifest(manifest)

    def test_per_page_overrides_share_the_defaults_contract(self) -> None:
        manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        manifest["pages"][0].update(timeoutSeconds=120, attempts=3)

        validated = MODULE.validate_manifest(manifest)
        effective = MODULE.resolve_effective_config(validated, validated["pages"][0])

        self.assertEqual(120, effective["timeoutSeconds"])
        self.assertEqual(3, effective["attempts"])
        self.assertEqual(
            validated["defaults"]["resultsExpression"], effective["resultsExpression"]
        )

    def test_an_override_outside_the_allowed_range_is_rejected(self) -> None:
        manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        manifest["pages"][0]["attempts"] = 99

        with self.assertRaisesRegex(MODULE.ManifestError, "attempts"):
            MODULE.validate_manifest(manifest)


class PageSelectionTests(unittest.TestCase):
    def test_comma_separated_and_repeated_values_are_merged_without_duplicates(self) -> None:
        selection = MODULE.parse_page_selection(["fingerprinting, gpc", "gpc", "surrogates"])

        self.assertEqual(["fingerprinting", "gpc", "surrogates"], selection)

    def test_no_selection_is_an_empty_list(self) -> None:
        self.assertEqual([], MODULE.parse_page_selection(None))


class OutcomeClassificationTests(unittest.TestCase):
    def test_a_produced_value_counts_as_carried_out(self) -> None:
        for value in ("Mozilla/5.0", 0, False, {"a": 1}, [1], 3.5):
            self.assertEqual(MODULE.OUTCOME_VALUE, MODULE.classify_test_value(value))

    def test_nothing_produced_counts_as_a_gap(self) -> None:
        for value in (None, {}, [], "", "   "):
            self.assertEqual(MODULE.OUTCOME_EMPTY, MODULE.classify_test_value(value))

    def test_results_payload_maps_to_one_outcome_per_test(self) -> None:
        payload = {
            "page": "fingerprinting",
            "results": [
                {"id": "navigator.userAgent", "value": "Broiler/1.0"},
                {"id": "headers - accept", "value": {}},
                {"id": "screen.width", "value": None},
            ],
        }

        self.assertEqual(
            {
                "navigator.userAgent": MODULE.OUTCOME_VALUE,
                "headers - accept": MODULE.OUTCOME_EMPTY,
                "screen.width": MODULE.OUTCOME_EMPTY,
            },
            MODULE.extract_test_outcomes(payload),
        )

    def test_repeated_ids_are_disambiguated_rather_than_collapsed(self) -> None:
        payload = {
            "results": [
                {"id": "cookie", "value": "first"},
                {"id": "cookie", "value": None},
            ]
        }

        outcomes = MODULE.extract_test_outcomes(payload)

        self.assertEqual(
            {"cookie": MODULE.OUTCOME_VALUE, "cookie (2)": MODULE.OUTCOME_EMPTY}, outcomes
        )

    def test_a_payload_without_a_results_array_yields_no_outcomes(self) -> None:
        self.assertEqual({}, MODULE.extract_test_outcomes({"page": "x"}))
        self.assertEqual({}, MODULE.extract_test_outcomes(None))

    def test_summary_counts_values_and_gaps(self) -> None:
        summary = MODULE.summarize_outcomes(
            {"a": MODULE.OUTCOME_VALUE, "b": MODULE.OUTCOME_EMPTY, "c": MODULE.OUTCOME_VALUE}
        )

        self.assertEqual({"tests": 3, "withValue": 2, "empty": 1}, summary)


class EvaluationReportTests(unittest.TestCase):
    def _report(self, **evaluation) -> dict:
        return {
            "url": "https://privacy-test-pages.site/privacy-protections/fingerprinting/?run",
            "evaluations": [{"index": 0, "expression": "start", "value": "started"}, evaluation],
        }

    def test_a_json_results_string_is_parsed(self) -> None:
        payload = {"page": "fingerprinting", "results": [{"id": "a", "value": 1}]}
        parsed = MODULE.parse_evaluation_report(self._report(value=json.dumps(payload)), 1)

        self.assertIsNone(parsed["reason"])
        self.assertEqual(payload, parsed["results"])

    def test_a_page_without_the_global_is_reported_rather_than_crashing(self) -> None:
        parsed = MODULE.parse_evaluation_report(self._report(value=None), 1)

        self.assertIsNone(parsed["results"])
        self.assertIn("did not define", parsed["reason"])

    def test_a_thrown_expression_is_reported_with_its_message(self) -> None:
        parsed = MODULE.parse_evaluation_report(self._report(value=None, error="boom"), 1)

        self.assertIsNone(parsed["results"])
        self.assertIn("boom", parsed["reason"])

    def test_a_non_json_value_is_reported(self) -> None:
        parsed = MODULE.parse_evaluation_report(self._report(value="[object Object]"), 1)

        self.assertIsNone(parsed["results"])
        self.assertIn("did not produce JSON", parsed["reason"])

    def test_a_truncated_report_is_reported(self) -> None:
        parsed = MODULE.parse_evaluation_report({"evaluations": []}, 1)

        self.assertIsNone(parsed["results"])
        self.assertIn("no results expression", parsed["reason"])


class BaselineComparisonTests(unittest.TestCase):
    BASELINE = {
        "status": "ok",
        "tests": {
            "kept": MODULE.OUTCOME_VALUE,
            "lost": MODULE.OUTCOME_VALUE,
            "gone": MODULE.OUTCOME_VALUE,
            "gap": MODULE.OUTCOME_EMPTY,
            "fixed": MODULE.OUTCOME_EMPTY,
        },
    }

    def test_a_test_that_stops_producing_a_value_is_a_regression(self) -> None:
        comparison = MODULE.compare_to_baseline(
            "ok",
            {
                "kept": MODULE.OUTCOME_VALUE,
                "lost": MODULE.OUTCOME_EMPTY,
                "gap": MODULE.OUTCOME_EMPTY,
                "fixed": MODULE.OUTCOME_VALUE,
                "added": MODULE.OUTCOME_VALUE,
            },
            self.BASELINE,
        )

        self.assertEqual(
            [
                {"test": "gone", "from": MODULE.OUTCOME_VALUE, "to": MODULE.OUTCOME_MISSING},
                {"test": "lost", "from": MODULE.OUTCOME_VALUE, "to": MODULE.OUTCOME_EMPTY},
            ],
            sorted(comparison["regressions"], key=lambda change: change["test"]),
        )
        self.assertEqual(
            [{"test": "fixed", "from": MODULE.OUTCOME_EMPTY, "to": MODULE.OUTCOME_VALUE}],
            comparison["improvements"],
        )
        self.assertEqual(["added"], comparison["newTests"])
        self.assertEqual(["gone"], comparison["removedTests"])

    def test_a_test_the_corpus_dropped_is_not_an_improvement(self) -> None:
        comparison = MODULE.compare_to_baseline("ok", {}, {"status": "ok", "tests": {"gap": MODULE.OUTCOME_EMPTY}})

        self.assertEqual([], comparison["regressions"])
        self.assertEqual([], comparison["improvements"])
        self.assertEqual(["gap"], comparison["removedTests"])

    def test_a_page_that_stops_running_is_a_regression(self) -> None:
        comparison = MODULE.compare_to_baseline("failed", {}, self.BASELINE)

        self.assertIn({"test": "(page)", "from": "ok", "to": "failed"}, comparison["regressions"])

    def test_a_page_that_starts_running_is_an_improvement(self) -> None:
        comparison = MODULE.compare_to_baseline(
            "ok", {"kept": MODULE.OUTCOME_VALUE}, {"status": "failed", "tests": {}}
        )

        self.assertIn({"test": "(page)", "from": "failed", "to": "ok"}, comparison["improvements"])

    def test_a_page_with_no_baseline_entry_reports_every_test_as_new(self) -> None:
        comparison = MODULE.compare_to_baseline("ok", {"a": MODULE.OUTCOME_VALUE}, None)

        self.assertFalse(comparison["compared"])
        self.assertEqual([], comparison["regressions"])
        self.assertEqual(["a"], comparison["newTests"])


class BaselineDocumentTests(unittest.TestCase):
    def test_checked_in_baseline_matches_the_manifest(self) -> None:
        manifest = MODULE.load_manifest(MANIFEST_PATH)
        baseline = MODULE.load_baseline(BASELINE_PATH)

        self.assertIsNotNone(baseline)
        self.assertEqual(
            {page["id"] for page in manifest["pages"]}, set(baseline["pages"]),
            "every manifest page needs a baseline entry, and the baseline must not name pages the manifest dropped",
        )

    def test_a_partial_run_keeps_the_pages_it_did_not_cover(self) -> None:
        previous = {
            "schemaVersion": MODULE.SCHEMA_VERSION,
            "pages": {
                "kept": {"status": "ok", "tests": {"a": MODULE.OUTCOME_VALUE}},
                "rerun": {"status": "failed", "tests": {}},
            },
        }

        rebuilt = MODULE.build_baseline(
            [{"id": "rerun", "status": "ok", "outcomes": {"b": MODULE.OUTCOME_VALUE}}],
            previous=previous,
        )

        self.assertEqual(previous["pages"]["kept"], rebuilt["pages"]["kept"])
        self.assertEqual(
            {"status": "ok", "tests": {"b": MODULE.OUTCOME_VALUE}}, rebuilt["pages"]["rerun"]
        )

    def test_an_unknown_outcome_is_rejected(self) -> None:
        with self.assertRaisesRegex(MODULE.BaselineError, "must be"):
            MODULE.validate_baseline(
                {
                    "schemaVersion": MODULE.SCHEMA_VERSION,
                    "pages": {"p": {"status": "ok", "tests": {"a": "maybe"}}},
                }
            )

    def test_a_baseline_from_another_schema_version_is_rejected(self) -> None:
        with self.assertRaisesRegex(MODULE.BaselineError, "schemaVersion"):
            MODULE.validate_baseline({"schemaVersion": 99, "pages": {}})


class OutputDirectoryTests(unittest.TestCase):
    def test_previous_run_artefacts_go_and_nothing_else_does(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "pages" / "gpc").mkdir(parents=True)
            (root / "pages" / "gpc" / "results.json").write_text("{}", encoding="utf-8")
            (root / "results.json").write_text("{}", encoding="utf-8")
            (root / "report.md").write_text("stale", encoding="utf-8")
            (root / "not-ours.txt").write_text("keep me", encoding="utf-8")

            MODULE.clear_previous_run(root)

            self.assertFalse((root / "pages").exists())
            self.assertFalse((root / "results.json").exists())
            self.assertFalse((root / "report.md").exists())
            self.assertEqual("keep me", (root / "not-ours.txt").read_text(encoding="utf-8"))

    def test_a_directory_that_does_not_exist_yet_is_created(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "fresh" / "nested"

            MODULE.clear_previous_run(root)

            self.assertTrue(root.is_dir())


class ReportRenderingTests(unittest.TestCase):
    AGGREGATE = {
        "schemaVersion": MODULE.SCHEMA_VERSION,
        "source": {
            "repository": "https://github.com/duckduckgo/privacy-test-pages",
            "baseUrl": "https://privacy-test-pages.site",
        },
        "environment": {
            "timestamp": "2026-01-01T00:00:00Z",
            "platform": "linux",
            "dotnetSdkVersion": "10.0.302",
            "gitCommit": "abc123",
        },
        "summary": {
            "pages": 2,
            "pagesOk": 1,
            "pagesFailed": 1,
            "tests": 4,
            "testsWithValue": 3,
            "testsEmpty": 1,
            "regressions": 1,
            "improvements": 0,
            "newTests": 1,
            "removedTests": 0,
        },
        "results": [
            {
                "id": "fingerprinting",
                "name": "Fingerprinting",
                "url": "https://privacy-test-pages.site/privacy-protections/fingerprinting/?run",
                "description": "Reads the fingerprinting surface.",
                "status": "ok",
                "reason": None,
                "summary": {"tests": 4, "withValue": 3, "empty": 1},
                "comparison": {
                    "compared": True,
                    "regressions": [
                        {"test": "screen.width", "from": "value", "to": "empty"}
                    ],
                    "improvements": [],
                    "newTests": ["navigator.gpu"],
                    "removedTests": [],
                },
            },
            {
                "id": "gpc",
                "name": "Global Privacy Control",
                "url": "https://privacy-test-pages.site/privacy-protections/gpc/?run",
                "description": "Reads the GPC signal.",
                "status": "failed",
                "reason": "the page did not define a results object",
                "summary": {"tests": 0, "withValue": 0, "empty": 0},
                "comparison": {
                    "compared": True,
                    "regressions": [],
                    "improvements": [],
                    "newTests": [],
                    "removedTests": [],
                },
            },
        ],
    }

    def test_markdown_report_names_the_regression_and_the_failing_page(self) -> None:
        report = MODULE.render_markdown_report(self.AGGREGATE)

        self.assertIn("`screen.width`: value → empty", report)
        self.assertIn("the page did not define a results object", report)
        self.assertIn("https://github.com/duckduckgo/privacy-test-pages", report)

    def test_job_summary_tables_the_regressions_and_the_failures(self) -> None:
        summary = MODULE.render_job_summary(self.AGGREGATE)

        self.assertIn("| Fingerprinting | `screen.width` | value | empty |", summary)
        self.assertIn("### Pages that did not run", summary)
        self.assertIn("3/4", summary)

    def test_html_report_escapes_page_content(self) -> None:
        aggregate = copy.deepcopy(self.AGGREGATE)
        aggregate["results"][0]["description"] = "<script>alert(1)</script>"

        report = MODULE.render_html_report(aggregate)

        self.assertNotIn("<script>alert(1)</script>", report)
        self.assertIn("&lt;script&gt;", report)

    def test_coverage_percent_is_zero_when_nothing_ran(self) -> None:
        self.assertEqual(0.0, MODULE.coverage_percent({"tests": 0, "testsWithValue": 0}))
        self.assertEqual(75.0, MODULE.coverage_percent({"tests": 4, "testsWithValue": 3}))


if __name__ == "__main__":
    unittest.main()
