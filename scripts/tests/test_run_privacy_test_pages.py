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
        "reference": {
            "engine": "chromium",
            "chromiumVersion": "141.0.0.0",
            "playwrightVersion": "1.59.1",
            "captured": True,
            "reused": False,
            "disabled": False,
            "run": {"exitCode": 0, "timedOut": False},
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
            "pagesCompared": 1,
            "referenceTests": 4,
            "parity": 3,
            "gaps": 1,
            "gapsFromErrors": 0,
            "broilerOnly": 0,
            "neither": 0,
            "divergentTypes": 0,
        },
        "results": [
            {
                "id": "fingerprinting",
                "name": "Fingerprinting",
                "url": "https://privacy-test-pages.site/privacy-protections/fingerprinting/?run",
                "description": "Reads the fingerprinting surface.",
                "tags": ["fingerprinting"],
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
                "parity": {
                    "available": True,
                    "referenceStatus": "ok",
                    "reason": None,
                    "summary": {
                        "tests": 4,
                        MODULE.PARITY_MATCH: 3,
                        MODULE.PARITY_GAP: 1,
                        MODULE.PARITY_AHEAD: 0,
                        MODULE.PARITY_NEITHER: 0,
                        "broilerErrors": 0,
                    },
                    "gaps": [
                        {
                            "test": "screen.width",
                            "name": "screen.width",
                            "broiler": "empty",
                            "broilerValue": None,
                            "referenceType": "number",
                            "referenceValue": "1280",
                        }
                    ],
                    "broilerOnly": [],
                    "neither": [],
                    "divergentTypes": [],
                },
            },
            {
                "id": "gpc",
                "name": "Global Privacy Control",
                "url": "https://privacy-test-pages.site/privacy-protections/gpc/?run",
                "description": "Reads the GPC signal.",
                "tags": ["gpc"],
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
                "parity": {
                    "available": False,
                    "referenceStatus": "failed",
                    "reason": "the page did not publish a results object",
                    "summary": {
                        "tests": 0,
                        MODULE.PARITY_MATCH: 0,
                        MODULE.PARITY_GAP: 0,
                        MODULE.PARITY_AHEAD: 0,
                        MODULE.PARITY_NEITHER: 0,
                        "broilerErrors": 0,
                    },
                    "gaps": [],
                    "broilerOnly": [],
                    "neither": [],
                    "divergentTypes": [],
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

    def test_reports_name_the_reference_engine_and_the_gap(self) -> None:
        report = MODULE.render_markdown_report(self.AGGREGATE)
        summary = MODULE.render_job_summary(self.AGGREGATE)
        page = MODULE.render_html_report(self.AGGREGATE)

        for rendered in (report, summary, page):
            self.assertIn("141.0.0.0", rendered)
        self.assertIn("Chromium returns number", report)
        self.assertIn("### Gaps behind Chromium", summary)
        self.assertIn("### Pages with no Chromium reference", summary)

    def test_a_page_without_a_reference_is_reported_as_uncompared(self) -> None:
        report = MODULE.render_markdown_report(self.AGGREGATE)

        self.assertIn("Not compared with Chromium", report)

    def test_parity_percent_ignores_probes_neither_engine_answered(self) -> None:
        self.assertEqual(0.0, MODULE.parity_percent({"parity": 0, "gaps": 0}))
        self.assertEqual(75.0, MODULE.parity_percent({"parity": 3, "gaps": 1}))


class ReferenceComparisonTests(unittest.TestCase):
    @staticmethod
    def reference(results: list[dict[str, object]], status: str = "ok") -> dict[str, object]:
        return {
            "schemaVersion": MODULE.SCHEMA_VERSION,
            "engine": "chromium",
            "status": status,
            "reason": None,
            "browser": {"chromiumVersion": "141.0.0.0", "playwrightVersion": "1.59.1"},
            "results": {"results": results},
        }

    def test_a_probe_chromium_answers_and_broiler_does_not_is_a_gap(self) -> None:
        broiler = MODULE.extract_test_entries({"results": [{"id": "webgl", "value": None}]})
        reference = self.reference([{"id": "webgl", "value": {"vendor": "Google Inc."}}])

        parity = MODULE.compare_to_reference(broiler, reference)

        self.assertTrue(parity["available"])
        self.assertEqual(1, parity["summary"][MODULE.PARITY_GAP])
        self.assertEqual("webgl", parity["gaps"][0]["test"])
        self.assertEqual("object", parity["gaps"][0]["referenceType"])
        self.assertIn("Google Inc.", parity["gaps"][0]["referenceValue"])

    def test_a_probe_that_threw_in_broiler_is_a_gap_not_a_match(self) -> None:
        thrown = {
            "name": "ReferenceError",
            "message": "webkitOfflineAudioContext is not defined",
            "stack": "\n at inline:deferred-3",
        }
        broiler = MODULE.extract_test_entries({"results": [{"id": "audio", "value": thrown}]})
        reference = self.reference([{"id": "audio", "value": "124.043"}])

        parity = MODULE.compare_to_reference(broiler, reference)

        self.assertEqual(0, parity["summary"][MODULE.PARITY_MATCH])
        self.assertEqual(1, parity["summary"][MODULE.PARITY_GAP])
        self.assertEqual(1, parity["summary"]["broilerErrors"])
        self.assertEqual("error", parity["gaps"][0]["broiler"])
        self.assertIn("webkitOfflineAudioContext", parity["gaps"][0]["broilerValue"])

    def test_a_probe_that_threw_in_both_engines_is_answered_by_neither(self) -> None:
        thrown = {"name": "ReferenceError", "message": "Gyroscope is not defined", "stack": "…"}
        broiler = MODULE.extract_test_entries({"results": [{"id": "Gyroscope", "value": thrown}]})
        reference = self.reference([{"id": "Gyroscope", "value": thrown}])

        parity = MODULE.compare_to_reference(broiler, reference)

        self.assertEqual(["Gyroscope"], parity["neither"])
        self.assertEqual(0, parity["summary"][MODULE.PARITY_GAP])

    def test_only_an_error_shaped_object_is_read_as_a_thrown_probe(self) -> None:
        self.assertTrue(MODULE.is_error_value({"name": "TypeError", "message": "x is not a function"}))
        self.assertTrue(MODULE.is_error_value({"message": "boom", "stack": "at <anonymous>"}))
        self.assertFalse(MODULE.is_error_value({"message": "boom"}))
        self.assertFalse(MODULE.is_error_value({"name": "Chrome", "version": "141"}))
        self.assertFalse(MODULE.is_error_value("TypeError: x is not a function"))
        self.assertFalse(MODULE.is_error_value(None))

    def test_the_baseline_outcome_of_a_thrown_probe_is_left_alone(self) -> None:
        thrown = {"name": "TypeError", "message": "x is not a function"}

        # The baseline tracks what the page published; only the comparison
        # reinterprets it, so a reclassification here cannot fail a run.
        self.assertEqual(MODULE.OUTCOME_VALUE, MODULE.classify_test_value(thrown))
        self.assertEqual(
            {"x": MODULE.OUTCOME_VALUE},
            MODULE.extract_test_outcomes({"results": [{"id": "x", "value": thrown}]}),
        )

    def test_a_probe_neither_engine_answers_is_not_a_gap(self) -> None:
        broiler = MODULE.extract_test_entries({"results": [{"id": "websocket", "value": None}]})
        reference = self.reference([{"id": "websocket", "value": None}])

        parity = MODULE.compare_to_reference(broiler, reference)

        self.assertEqual(0, parity["summary"][MODULE.PARITY_GAP])
        self.assertEqual(["websocket"], parity["neither"])

    def test_both_engines_answering_counts_as_parity_however_the_values_differ(self) -> None:
        broiler = MODULE.extract_test_entries({"results": [{"id": "ua", "value": "Broiler/1.0"}]})
        reference = self.reference([{"id": "ua", "value": "Chrome/141"}])

        parity = MODULE.compare_to_reference(broiler, reference)

        self.assertEqual(1, parity["summary"][MODULE.PARITY_MATCH])
        self.assertEqual([], parity["divergentTypes"])

    def test_a_shared_probe_answered_in_a_different_shape_is_reported_separately(self) -> None:
        broiler = MODULE.extract_test_entries({"results": [{"id": "screen", "value": "1280x720"}]})
        reference = self.reference([{"id": "screen", "value": {"width": 1280}}])

        parity = MODULE.compare_to_reference(broiler, reference)

        self.assertEqual(1, parity["summary"][MODULE.PARITY_MATCH])
        self.assertEqual(1, len(parity["divergentTypes"]))
        self.assertEqual("string", parity["divergentTypes"][0]["broilerType"])
        self.assertEqual("object", parity["divergentTypes"][0]["referenceType"])

    def test_a_probe_only_broiler_answered_is_kept_apart_from_the_gaps(self) -> None:
        broiler = MODULE.extract_test_entries({"results": [{"id": "extra", "value": "yes"}]})
        reference = self.reference([{"id": "other", "value": "yes"}])

        parity = MODULE.compare_to_reference(broiler, reference)

        self.assertEqual(["extra"], [entry["test"] for entry in parity["broilerOnly"]])
        self.assertEqual(["other"], [gap["test"] for gap in parity["gaps"]])

    def test_a_failed_chromium_run_leaves_the_page_uncompared(self) -> None:
        broiler = MODULE.extract_test_entries({"results": [{"id": "webgl", "value": "yes"}]})
        reference = self.reference([], status="failed")
        reference["reason"] = "net::ERR_CONNECTION_RESET"

        parity = MODULE.compare_to_reference(broiler, reference)

        self.assertFalse(parity["available"])
        self.assertIn("ERR_CONNECTION_RESET", parity["reason"])
        self.assertEqual([], parity["gaps"])

    def test_no_reference_at_all_leaves_the_page_uncompared(self) -> None:
        parity = MODULE.compare_to_reference({}, None)

        self.assertFalse(parity["available"])
        self.assertEqual("no Chromium reference was captured", parity["reason"])

    def test_repeated_probe_ids_are_compared_occurrence_by_occurrence(self) -> None:
        payload = {"results": [{"id": "cookie", "value": "a"}, {"id": "cookie", "value": None}]}
        broiler = MODULE.extract_test_entries(payload)
        reference = self.reference([{"id": "cookie", "value": "a"}, {"id": "cookie", "value": "b"}])

        parity = MODULE.compare_to_reference(broiler, reference)

        self.assertEqual(1, parity["summary"][MODULE.PARITY_MATCH])
        self.assertEqual(["cookie (2)"], [gap["test"] for gap in parity["gaps"]])

    def test_a_reference_from_another_schema_version_is_ignored(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "reference.json"
            path.write_text(json.dumps({"schemaVersion": 99, "status": "ok"}), encoding="utf-8")

            self.assertIsNone(MODULE.load_reference_document(path))

    def test_an_unreadable_reference_is_absent_rather_than_fatal(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "reference.json"
            path.write_text("{not json", encoding="utf-8")

            self.assertIsNone(MODULE.load_reference_document(path))
            self.assertIsNone(MODULE.load_reference_document(Path(directory) / "absent.json"))

    def test_the_browser_version_is_taken_from_whichever_reference_has_one(self) -> None:
        metadata = MODULE.reference_metadata([
            {"browser": {"chromiumVersion": None}},
            {"browser": {"chromiumVersion": "141.0.0.0", "playwrightVersion": "1.59.1"}},
        ])

        self.assertEqual("141.0.0.0", metadata["chromiumVersion"])
        self.assertEqual("chromium", metadata["engine"])

    def test_a_value_excerpt_stays_one_bounded_line(self) -> None:
        excerpt = MODULE.describe_value({"text": "a\nb" + "c" * 500})

        self.assertNotIn("\n", excerpt)
        self.assertLessEqual(len(excerpt), MODULE.VALUE_EXCERPT_CHARS)

    def test_a_pipe_in_a_probe_value_cannot_break_the_report_table(self) -> None:
        self.assertNotIn("|", MODULE._escape_table_cell("a|b").replace("\\|", ""))


class GapDocumentTests(unittest.TestCase):
    def test_the_gap_document_carries_the_evidence_and_the_uncompared_pages(self) -> None:
        document = MODULE.build_gap_document(ReportRenderingTests.AGGREGATE)

        self.assertEqual(["fingerprinting"], [page["id"] for page in document["pages"]])
        self.assertEqual(["gpc"], [page["id"] for page in document["notCompared"]])
        self.assertEqual("screen.width", document["pages"][0]["gaps"][0]["test"])

        report = MODULE.render_gap_report(document)
        self.assertIn("## Fingerprinting (`fingerprinting`)", report)
        self.assertIn("Probes Chromium answers and Broiler does not", report)
        self.assertIn("## Pages with no Chromium reference", report)

    def test_a_run_with_no_gaps_says_so_rather_than_rendering_nothing(self) -> None:
        aggregate = copy.deepcopy(ReportRenderingTests.AGGREGATE)
        parity = aggregate["results"][0]["parity"]
        parity["gaps"] = []
        parity["summary"][MODULE.PARITY_GAP] = 0
        parity["summary"][MODULE.PARITY_MATCH] = 4

        report = MODULE.render_gap_report(MODULE.build_gap_document(aggregate))

        self.assertIn("No gaps were found on the compared pages.", report)


class ErrorSignatureTests(unittest.TestCase):
    def test_the_same_missing_api_groups_whatever_value_it_was_passed(self) -> None:
        first = MODULE.error_signature('"TypeError: navigator.storage.estimate is not a function"')
        second = MODULE.error_signature('"TypeError: navigator.storage.estimate is not a function"')

        self.assertEqual(first, second)
        self.assertEqual("TypeError: navigator.storage.estimate is not a function", first)

    def test_incidental_detail_does_not_split_one_finding_into_many(self) -> None:
        signatures = {
            MODULE.error_signature('"SecurityError: blocked https://a.example/1.png after 30 ms"'),
            MODULE.error_signature('"SecurityError: blocked https://b.example/2.png after 45 ms"'),
        }

        self.assertEqual(1, len(signatures))

    def test_a_quoted_fragment_inside_a_message_is_normalized_not_swallowed(self) -> None:
        signature = MODULE.error_signature('Failed to read \'localStorage\' from Window')

        self.assertEqual("Failed to read <value> from Window", signature)

    def test_a_probe_that_stored_nothing_still_has_a_signature(self) -> None:
        self.assertEqual("(no message)", MODULE.error_signature(None))

    def test_a_long_message_is_bounded(self) -> None:
        signature = MODULE.error_signature("x" * 500)

        self.assertLessEqual(len(signature), MODULE.ERROR_SIGNATURE_CHARS)


class IssueDocumentTests(unittest.TestCase):
    @staticmethod
    def aggregate_with_errors() -> dict:
        """A run whose gaps are mostly one thrown error across two pages."""

        aggregate = copy.deepcopy(ReportRenderingTests.AGGREGATE)
        fingerprinting = aggregate["results"][0]["parity"]
        fingerprinting["gaps"] = [
            {
                "test": "storage.estimate",
                "name": "storage.estimate",
                "broiler": "error",
                "broilerValue": '"TypeError: navigator.storage.estimate is not a function"',
                "referenceType": "object",
                "referenceValue": '{"quota": 1}',
            },
            {
                "test": "storage.persisted",
                "name": "storage.persisted",
                "broiler": "error",
                "broilerValue": '"TypeError: navigator.storage.persisted is not a function"',
                "referenceType": "boolean",
                "referenceValue": "false",
            },
            {
                "test": "screen.width",
                "name": "screen.width",
                "broiler": "empty",
                "broilerValue": None,
                "referenceType": "number",
                "referenceValue": "1280",
            },
        ]
        fingerprinting["summary"][MODULE.PARITY_GAP] = 3
        fingerprinting["summary"]["broilerErrors"] = 2

        # A second compared page carrying the same thrown error, so the grouping
        # has something to collapse across pages.
        gpc = aggregate["results"][1]
        gpc["status"] = "ok"
        gpc["reason"] = None
        gpc["parity"] = {
            "available": True,
            "referenceStatus": "ok",
            "reason": None,
            "summary": {
                "tests": 1,
                MODULE.PARITY_MATCH: 0,
                MODULE.PARITY_GAP: 1,
                MODULE.PARITY_AHEAD: 0,
                MODULE.PARITY_NEITHER: 0,
                "broilerErrors": 1,
            },
            "gaps": [
                {
                    "test": "storage.estimate",
                    "name": "storage.estimate",
                    "broiler": "error",
                    "broilerValue": '"TypeError: navigator.storage.estimate is not a function"',
                    "referenceType": "object",
                    "referenceValue": '{"quota": 2}',
                }
            ],
            "broilerOnly": [],
            "neither": [],
            "divergentTypes": [],
        }
        aggregate["summary"].update({
            "pagesOk": 2,
            "pagesFailed": 0,
            "pagesCompared": 2,
            "gaps": 4,
            "gapsFromErrors": 3,
            "parity": 3,
        })
        return aggregate

    def test_the_gap_issue_ranks_error_signatures_before_pages(self) -> None:
        aggregate = self.aggregate_with_errors()
        issue = MODULE.build_gap_issue(MODULE.build_gap_document(aggregate))

        signatures = issue["errorSignatures"]
        self.assertEqual(
            "TypeError: navigator.storage.estimate is not a function", signatures[0]["signature"]
        )
        self.assertEqual(2, signatures[0]["count"])
        self.assertEqual(["fingerprinting", "gpc"], signatures[0]["pages"])
        self.assertEqual(["fingerprinting", "gpc"], [page["id"] for page in issue["pages"]])
        self.assertEqual(3, issue["pages"][0]["gaps"])
        self.assertEqual(2, issue["pages"][0]["errors"])

    def test_the_gap_issue_body_carries_its_marker_and_the_evidence(self) -> None:
        aggregate = self.aggregate_with_errors()
        body = MODULE.render_gap_issue_markdown(
            MODULE.build_gap_issue(MODULE.build_gap_document(aggregate)),
            "https://example.invalid/run/1",
        )

        self.assertIn(MODULE.issue_marker(MODULE.ISSUE_KIND_GAPS), body)
        self.assertIn("navigator.storage.estimate is not a function", body)
        self.assertIn("`fingerprinting:storage.estimate`", body)
        self.assertIn("`gpc:storage.estimate`", body)
        self.assertIn("141.0.0.0", body)
        self.assertIn("https://example.invalid/run/1", body)

    def test_the_gap_issue_limit_bounds_both_rankings(self) -> None:
        aggregate = self.aggregate_with_errors()
        issue = MODULE.build_gap_issue(MODULE.build_gap_document(aggregate), limit=1)

        self.assertEqual(1, len(issue["errorSignatures"]))
        self.assertEqual(2, issue["errorSignatureCount"])
        self.assertEqual(1, len(issue["pages"]))
        self.assertEqual(2, issue["pageCount"])

    def test_the_regression_issue_names_the_page_that_stopped_running(self) -> None:
        aggregate = copy.deepcopy(ReportRenderingTests.AGGREGATE)
        aggregate["results"][1]["comparison"]["regressions"] = [
            {"test": "(page)", "from": "ok", "to": "failed"}
        ]

        issue = MODULE.build_regression_issue(aggregate)
        body = MODULE.render_regression_issue_markdown(issue)

        self.assertEqual(2, issue["count"])
        self.assertTrue(any(page["stoppedRunning"] for page in issue["pages"]))
        self.assertIn("| `screen.width` | value | empty |", body)
        self.assertIn("stopped running altogether", body)
        self.assertIn(MODULE.issue_marker(MODULE.ISSUE_KIND_REGRESSIONS), body)

    def test_the_regression_issue_bounds_the_probes_it_lists_per_page(self) -> None:
        aggregate = copy.deepcopy(ReportRenderingTests.AGGREGATE)
        aggregate["results"][0]["comparison"]["regressions"] = [
            {"test": f"probe-{index}", "from": "value", "to": "empty"} for index in range(5)
        ]

        issue = MODULE.build_regression_issue(aggregate, limit=2)
        body = MODULE.render_regression_issue_markdown(issue)

        self.assertEqual(5, issue["pages"][0]["probeCount"])
        self.assertEqual(2, len(issue["pages"][0]["probes"]))
        self.assertIn("… 3 more", body)

    def test_the_unmeasured_issue_reports_the_page_neither_engine_measured(self) -> None:
        aggregate = copy.deepcopy(ReportRenderingTests.AGGREGATE)

        issue = MODULE.build_unmeasured_issue(aggregate)
        body = MODULE.render_unmeasured_issue_markdown(issue)

        self.assertEqual(["gpc"], [page["id"] for page in issue["pages"]])
        self.assertFalse(issue["pages"][0]["comparedWithChromium"])
        self.assertIn("the page did not define a results object", body)
        self.assertIn(MODULE.issue_marker(MODULE.ISSUE_KIND_UNMEASURED), body)

    def test_a_deliberately_uncompared_run_reports_no_unmeasured_pages(self) -> None:
        aggregate = copy.deepcopy(ReportRenderingTests.AGGREGATE)
        aggregate["reference"]["disabled"] = True
        aggregate["results"][1]["status"] = "ok"
        aggregate["results"][0]["parity"]["available"] = False

        issue = MODULE.build_unmeasured_issue(aggregate)

        self.assertEqual(0, issue["count"])

    def test_the_documents_say_which_findings_are_worth_filing(self) -> None:
        documents = {
            document["kind"]: document
            for document in MODULE.build_issue_documents(
                ReportRenderingTests.AGGREGATE,
                MODULE.build_gap_document(ReportRenderingTests.AGGREGATE),
            )
        }

        self.assertTrue(documents[MODULE.ISSUE_KIND_REGRESSIONS]["create"])
        self.assertTrue(documents[MODULE.ISSUE_KIND_UNMEASURED]["create"])
        self.assertTrue(documents[MODULE.ISSUE_KIND_GAPS]["create"])
        self.assertIn("2026-01-01", documents[MODULE.ISSUE_KIND_GAPS]["title"])
        self.assertIn("1 probe(s) behind Chromium", documents[MODULE.ISSUE_KIND_GAPS]["title"])

    def test_a_clean_run_files_nothing(self) -> None:
        aggregate = copy.deepcopy(ReportRenderingTests.AGGREGATE)
        aggregate["results"][1]["status"] = "ok"
        aggregate["results"][1]["parity"]["available"] = True
        aggregate["results"][0]["parity"]["gaps"] = []
        aggregate["results"][0]["comparison"]["regressions"] = []
        aggregate["summary"].update({"gaps": 0, "regressions": 0, "pagesCompared": 2, "pagesOk": 2})

        documents = MODULE.build_issue_documents(aggregate, MODULE.build_gap_document(aggregate))

        self.assertEqual([], [document["kind"] for document in documents if document["create"]])

    def test_a_run_that_compared_nothing_does_not_claim_zero_gaps_as_parity(self) -> None:
        aggregate = copy.deepcopy(ReportRenderingTests.AGGREGATE)
        aggregate["results"][0]["parity"]["available"] = False
        aggregate["summary"].update({"gaps": 0, "pagesCompared": 0})

        documents = {
            document["kind"]: document
            for document in MODULE.build_issue_documents(
                aggregate, MODULE.build_gap_document(aggregate)
            )
        }

        self.assertFalse(documents[MODULE.ISSUE_KIND_GAPS]["create"])
        self.assertTrue(documents[MODULE.ISSUE_KIND_UNMEASURED]["create"])

    def test_only_the_findings_are_written_and_the_index_names_them(self) -> None:
        documents = MODULE.build_issue_documents(
            ReportRenderingTests.AGGREGATE,
            MODULE.build_gap_document(ReportRenderingTests.AGGREGATE),
        )
        for document in documents:
            if document["kind"] == MODULE.ISSUE_KIND_UNMEASURED:
                document["create"] = False

        with tempfile.TemporaryDirectory() as directory:
            output_root = Path(directory)
            index = MODULE.write_issue_documents(output_root, documents, run_url="https://run")

            issue_dir = output_root / MODULE.ISSUE_DIR_NAME
            self.assertTrue((issue_dir / "gaps.md").is_file())
            self.assertFalse((issue_dir / "unmeasured.md").exists())
            self.assertEqual("https://run", index["runUrl"])
            entries = {entry["kind"]: entry for entry in index["issues"]}
            self.assertEqual("issues/gaps.md", entries[MODULE.ISSUE_KIND_GAPS]["body"])
            self.assertIsNone(entries[MODULE.ISSUE_KIND_UNMEASURED]["body"])
            self.assertEqual(
                json.loads((issue_dir / MODULE.ISSUE_INDEX_NAME).read_text(encoding="utf-8")),
                index,
            )

    def test_the_step_outputs_carry_the_counts_and_the_flags(self) -> None:
        documents = MODULE.build_issue_documents(
            ReportRenderingTests.AGGREGATE,
            MODULE.build_gap_document(ReportRenderingTests.AGGREGATE),
        )

        with tempfile.TemporaryDirectory() as directory:
            output_root = Path(directory)
            index = MODULE.write_issue_documents(output_root, documents)
            outputs_path = output_root / "step-output"
            MODULE.write_github_outputs(outputs_path, index)

            outputs = dict(
                line.split("=", 1)
                for line in outputs_path.read_text(encoding="utf-8").splitlines()
                if line
            )

        self.assertEqual("3", outputs["issue_count"])
        self.assertEqual("true", outputs["create_gaps_issue"])
        self.assertEqual("true", outputs["create_regressions_issue"])
        self.assertEqual("true", outputs["create_unmeasured_issue"])
        self.assertEqual("1", outputs["gap_count"])
        self.assertEqual("1", outputs["regression_count"])
        self.assertEqual("1", outputs["unmeasured_page_count"])

    def test_the_step_outputs_are_appended_not_overwritten(self) -> None:
        documents = MODULE.build_issue_documents(
            ReportRenderingTests.AGGREGATE,
            MODULE.build_gap_document(ReportRenderingTests.AGGREGATE),
        )

        with tempfile.TemporaryDirectory() as directory:
            output_root = Path(directory)
            index = MODULE.write_issue_documents(output_root, documents)
            outputs_path = output_root / "step-output"
            outputs_path.write_text("already_there=1\n", encoding="utf-8")
            MODULE.write_github_outputs(outputs_path, index)

            text = outputs_path.read_text(encoding="utf-8")

        self.assertTrue(text.startswith("already_there=1\n"))
        self.assertIn("issue_count=3", text)


class OutputRetentionTests(unittest.TestCase):
    def test_reusing_references_keeps_them_and_clears_everything_else(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            output_root = Path(directory)
            page_dir = output_root / "pages" / "fingerprinting"
            page_dir.mkdir(parents=True)
            (page_dir / "reference.json").write_text("{}", encoding="utf-8")
            (page_dir / "attempt-1.json").write_text("{}", encoding="utf-8")
            (output_root / "report.md").write_text("stale", encoding="utf-8")

            MODULE.clear_previous_run(output_root, keep_references=True)

            self.assertTrue((page_dir / "reference.json").is_file())
            self.assertFalse((page_dir / "attempt-1.json").exists())
            self.assertFalse((output_root / "report.md").exists())

    def test_a_stale_issue_body_never_survives_into_the_next_run(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            output_root = Path(directory)
            issue_dir = output_root / MODULE.ISSUE_DIR_NAME
            issue_dir.mkdir(parents=True)
            (issue_dir / "gaps.md").write_text("last week's gaps", encoding="utf-8")

            MODULE.clear_previous_run(output_root, keep_references=True)

            self.assertFalse(issue_dir.exists())

    def test_a_fresh_run_clears_the_references_too(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            output_root = Path(directory)
            page_dir = output_root / "pages" / "fingerprinting"
            page_dir.mkdir(parents=True)
            (page_dir / "reference.json").write_text("{}", encoding="utf-8")

            MODULE.clear_previous_run(output_root)

            self.assertFalse((output_root / "pages").exists())


if __name__ == "__main__":
    unittest.main()
