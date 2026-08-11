import copy
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import time
import unittest

import numpy as np


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "run-real-world-render-tests.py"
SPEC = importlib.util.spec_from_file_location("real_world_render_tests", SCRIPT_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)
REPO_ROOT = SCRIPT_PATH.parents[1]
MANIFEST_PATH = REPO_ROOT / "tests" / "real-world-sites" / "sites.json"


class RealWorldManifestTests(unittest.TestCase):
    def test_checked_in_manifest_has_requested_public_site_corpus(self) -> None:
        manifest = MODULE.load_manifest(MANIFEST_PATH)
        sites = {site["id"]: site for site in manifest["sites"]}

        self.assertTrue({"google-search", "seven-zip", "heise"}.issubset(sites))
        self.assertGreaterEqual(len(sites), 6)
        self.assertTrue(all(site["url"].startswith("https://") for site in sites.values()))
        self.assertEqual(5, manifest["defaults"]["pixelTolerance"])
        self.assertEqual({"stable", "dynamic"}, {site["category"] for site in sites.values()})

    def test_duplicate_site_ids_are_rejected(self) -> None:
        manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        manifest["sites"].append(copy.deepcopy(manifest["sites"][0]))

        with self.assertRaisesRegex(MODULE.ManifestError, "duplicate site id"):
            MODULE.validate_manifest(manifest)

    def test_unknown_manifest_fields_are_rejected(self) -> None:
        manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        manifest["sites"][0]["surprise"] = True

        with self.assertRaisesRegex(MODULE.ManifestError, "unknown field"):
            MODULE.validate_manifest(manifest)

    def test_manifest_rejects_plain_http_urls(self) -> None:
        manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        manifest["sites"][0]["url"] = "http://example.com/"

        with self.assertRaisesRegex(MODULE.ManifestError, "HTTPS"):
            MODULE.validate_manifest(manifest)

    def test_per_site_capture_and_metric_overrides_share_one_contract(self) -> None:
        manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        manifest["sites"][0].update(
            viewport={"width": 800, "height": 600},
            pixelTolerance=9,
            thresholds={"contentMatchPercent": 72},
        )

        normalized = MODULE.validate_manifest(manifest)
        effective = MODULE.resolve_effective_config(normalized["defaults"], normalized["sites"][0])

        self.assertEqual({"width": 800, "height": 600}, effective["viewport"])
        self.assertEqual(9, effective["pixelTolerance"])
        self.assertEqual(72, effective["thresholds"]["contentMatchPercent"])
        self.assertEqual(90, effective["thresholds"]["pixelMatchPercent"])

    def test_empty_per_site_threshold_override_is_rejected(self) -> None:
        manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        manifest["sites"][0]["thresholds"] = {}

        with self.assertRaisesRegex(MODULE.ManifestError, "at least one"):
            MODULE.validate_manifest(manifest)

    def test_site_selection_is_repeatable_comma_separated_and_ordered(self) -> None:
        self.assertEqual(
            ["heise", "seven-zip", "google-search"],
            MODULE.parse_site_selection(["heise,seven-zip", "heise", "google-search"]),
        )


class RealWorldImageComparisonTests(unittest.TestCase):
    THRESHOLDS = {
        "pixelMatchPercent": 95,
        "contentMatchPercent": 60,
        "structureMatchPercent": 60,
    }

    @staticmethod
    def _white_image(width: int = 100, height: int = 100) -> np.ndarray:
        image = np.full((height, width, 4), 255, dtype=np.uint8)
        return image

    def test_identical_images_score_one_hundred_in_every_primary_metric(self) -> None:
        reference = self._white_image()
        reference[35:65, 20:80, :3] = [25, 70, 130]

        comparison = MODULE.compare_image_arrays(
            reference.copy(),
            reference,
            tolerance=5,
            thresholds=self.THRESHOLDS,
        )

        self.assertEqual(100, comparison["pixelMatchPercent"])
        self.assertEqual(100, comparison["contentMatchPercent"])
        self.assertEqual(100, comparison["structureMatchPercent"])
        self.assertTrue(comparison["quality"]["passed"])
        self.assertIsNone(comparison["mismatchBoundingBox"])

    def test_white_background_cannot_hide_missing_or_shifted_content(self) -> None:
        reference = self._white_image()
        actual = self._white_image()
        # Only four percent of the canvas changes, so a whole-canvas score looks
        # excellent. The visible block has moved far enough that content and
        # edge metrics correctly call the result broken.
        reference[40:50, 10:20, :3] = 0
        actual[40:50, 70:80, :3] = 0

        comparison = MODULE.compare_image_arrays(
            actual,
            reference,
            tolerance=5,
            thresholds=self.THRESHOLDS,
        )

        self.assertGreaterEqual(comparison["pixelMatchPercent"], 95)
        self.assertLess(comparison["contentMatchPercent"], 10)
        self.assertLess(comparison["structureMatchPercent"], 60)
        self.assertFalse(comparison["quality"]["passed"])
        self.assertEqual("BackgroundDominated", MODULE.classify_result(comparison))

    def test_dimension_mismatch_is_a_quality_failure_not_a_resize(self) -> None:
        comparison = MODULE.compare_image_arrays(
            self._white_image(80, 120),
            self._white_image(100, 100),
            tolerance=5,
            thresholds=self.THRESHOLDS,
        )

        self.assertFalse(comparison["sizeMatches"])
        self.assertFalse(comparison["quality"]["passed"])
        self.assertEqual(0, comparison["pixelMatchPercent"])
        self.assertEqual(12000, comparison["totalPixels"])
        self.assertEqual(12000, comparison["mismatchedPixels"])

    def test_partial_reference_without_captured_metadata_is_not_comparable(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            site_dir = root / "sites" / "case"
            site_dir.mkdir(parents=True)
            image = self._white_image()
            MODULE.Image.fromarray(image, mode="RGBA").save(site_dir / "reference.png")
            MODULE.Image.fromarray(image, mode="RGBA").save(site_dir / "broiler-live.png")
            (site_dir / "snapshot.html").write_text("<!doctype html><title>partial</title>", encoding="utf-8")

            manifest = MODULE.load_manifest(MANIFEST_PATH)
            site = manifest["sites"][0]
            effective = MODULE.resolve_effective_config(manifest["defaults"], site)
            reference = MODULE._reference_capture(site_dir, root, site, effective)
            comparison = MODULE.compare_image_files(
                site_dir / "broiler-live.png",
                site_dir / "reference.png",
                tolerance=5,
                thresholds=self.THRESHOLDS,
                diff_path=site_dir / "live-diff.png",
                output_root=root,
                reference_usable=reference["usable"],
                reference_error=reference["error"],
            )

            self.assertFalse(reference["usable"])
            self.assertFalse(comparison["available"])
            self.assertIn("metadata", comparison["error"])
            self.assertFalse((site_dir / "live-diff.png").exists())

    def test_reused_reference_must_match_its_recorded_hashes(self) -> None:
        manifest = MODULE.load_manifest(MANIFEST_PATH)
        site = manifest["sites"][0]
        effective = MODULE.resolve_effective_config(manifest["defaults"], site)
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            site_dir = root / "sites" / "case"
            site_dir.mkdir(parents=True)
            MODULE.Image.fromarray(self._white_image(), mode="RGBA").save(site_dir / "reference.png")
            (site_dir / "snapshot.html").write_text("<!doctype html><title>changed</title>", encoding="utf-8")
            (site_dir / "reference.json").write_text(
                json.dumps(
                    {
                        "status": "captured",
                        "requestedUrl": site["url"],
                        "viewport": effective["viewport"],
                        "locale": effective["locale"],
                        "timezoneId": effective["timezoneId"],
                        "navigationTimeoutSeconds": effective["navigationTimeoutSeconds"],
                        "settleMilliseconds": effective["settleMilliseconds"],
                        "referenceImageSha256": "0" * 64,
                        "snapshotSha256": "1" * 64,
                    }
                ),
                encoding="utf-8",
            )

            reference = MODULE._reference_capture(site_dir, root, site, effective)

            self.assertFalse(reference["usable"])
            self.assertFalse(reference["snapshotUsable"])
            self.assertIn("does not match", reference["error"])
            self.assertIn("does not match", reference["snapshot"]["error"])

    def test_reused_reference_is_bound_to_current_url_and_capture_config(self) -> None:
        manifest = MODULE.load_manifest(MANIFEST_PATH)
        site = manifest["sites"][0]
        effective = MODULE.resolve_effective_config(manifest["defaults"], site)
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            site_dir = root / "sites" / "case"
            site_dir.mkdir(parents=True)
            MODULE.Image.fromarray(self._white_image(), mode="RGBA").save(site_dir / "reference.png")
            (site_dir / "snapshot.html").write_text("<!doctype html><title>recorded</title>", encoding="utf-8")
            metadata = {
                "status": "captured",
                "requestedUrl": "https://example.com/a-different-page",
                "viewport": effective["viewport"],
                "locale": effective["locale"],
                "timezoneId": effective["timezoneId"],
                "navigationTimeoutSeconds": effective["navigationTimeoutSeconds"],
                "settleMilliseconds": effective["settleMilliseconds"],
                "referenceImageSha256": MODULE.sha256_file(site_dir / "reference.png"),
                "snapshotSha256": MODULE.sha256_file(site_dir / "snapshot.html"),
            }
            (site_dir / "reference.json").write_text(json.dumps(metadata), encoding="utf-8")

            reference = MODULE._reference_capture(site_dir, root, site, effective)

            self.assertFalse(reference["usable"])
            self.assertEqual(["requestedUrl"], reference["captureConfigMismatches"])
            self.assertIn("configuration", reference["error"])

    def test_page_metadata_is_flattened_before_markdown_rendering(self) -> None:
        rendered = MODULE._markdown_code("ok`\n## injected")
        self.assertNotIn("`", rendered)
        self.assertNotIn("\n", rendered)

    def test_stable_and_dynamic_summaries_are_independent(self) -> None:
        def result(category: str, status: str, metric: float) -> dict:
            return {
                "site": {"category": category},
                "result": {"status": status, "classification": None},
                "comparisons": {
                    "live": {
                        "available": True,
                        "pixelMatchPercent": metric,
                        "contentMatchPercent": metric,
                        "structureMatchPercent": metric,
                    }
                },
            }

        results = [result("stable", "passed", 98), result("dynamic", "failed", 40)]

        stable = MODULE.summarize_results(results, category="stable")
        dynamic = MODULE.summarize_results(results, category="dynamic")
        self.assertEqual((1, 0), (stable["passed"], stable["failed"]))
        self.assertEqual((0, 1), (dynamic["passed"], dynamic["failed"]))
        self.assertEqual(98, stable["averageLiveMetrics"]["pixelMatchPercent"])
        self.assertEqual(40, dynamic["averageLiveMetrics"]["pixelMatchPercent"])

    def test_actions_summary_contains_no_artifact_relative_links(self) -> None:
        result = {
            "site": {"name": "Canary", "category": "stable"},
            "result": {"status": "passed", "classification": None},
            "captureTiming": {"referenceToLiveStartSeconds": 1.25},
            "comparisons": {
                "live": {
                    "available": True,
                    "pixelMatchPercent": 99,
                    "contentMatchPercent": 98,
                    "structureMatchPercent": 97,
                }
            },
        }
        aggregate = {
            "generatedAt": "2026-08-11T00:00:00Z",
            "environment": {"repositoryRevision": "abc123"},
            "summary": MODULE.summarize_results([result]),
            "stableSummary": MODULE.summarize_results([result], category="stable"),
            "dynamicSummary": MODULE.summarize_results([result], category="dynamic"),
            "results": [result],
        }

        summary = MODULE.render_job_summary(aggregate)

        self.assertNotIn("](", summary)
        self.assertNotIn("![", summary)
        self.assertIn("real-world-render-results", summary)

    def test_capture_delta_uses_the_attempt_that_produced_the_compared_image(self) -> None:
        timing = MODULE._capture_timing(
            {"metadata": {"screenshotAt": "2026-08-11T00:00:00Z"}},
            {
                "attempts": [
                    {"attempt": 1, "startedAt": "2026-08-11T00:00:05Z", "accepted": False},
                    {"attempt": 2, "startedAt": "2026-08-11T00:00:12Z", "accepted": True},
                ]
            },
        )

        self.assertEqual(2, timing["broilerLiveAttempt"])
        self.assertEqual(12, timing["referenceToLiveStartSeconds"])

    def test_failed_process_cannot_reuse_a_stale_capture(self) -> None:
        manifest = MODULE.load_manifest(MANIFEST_PATH)
        effective = MODULE.resolve_effective_config(manifest["defaults"], manifest["sites"][0])
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            output = root / "sites" / "case" / "broiler-live.png"
            output.parent.mkdir(parents=True)
            output.write_bytes(b"stale")

            capture = MODULE.run_broiler_capture(
                str(root / "missing-dotnet"),
                root / "missing-cli.dll",
                "https://example.com/",
                output,
                effective,
                1,
                root,
                root,
            )

            self.assertEqual("failed", capture["status"])
            self.assertFalse(output.exists())
            self.assertFalse(capture["artifact"]["available"])
            self.assertTrue((output.parent / "broiler-live-attempt-1.stdout.log").exists())
            self.assertTrue((output.parent / "broiler-live-attempt-1.stderr.log").exists())

    def test_memory_watchdog_terminates_a_growing_child_and_keeps_logs(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            stdout_path = root / "child.stdout.log"
            stderr_path = root / "child.stderr.log"
            result = MODULE._monitored_command_result(
                [
                    sys.executable,
                    "-c",
                    "import time; print('allocating', flush=True); data=bytearray(160*1024*1024); time.sleep(10)",
                ],
                cwd=root,
                stdout_path=stdout_path,
                stderr_path=stderr_path,
                timeout=15,
                memory_limit_mb=64,
            )

            self.assertTrue(result["memoryLimitExceeded"])
            self.assertFalse(result["timedOut"])
            self.assertNotEqual(0, result["exitCode"])
            self.assertGreater(result["peakRssMiB"], 64)
            self.assertIn("allocating", stdout_path.read_text(encoding="utf-8"))
            self.assertTrue(stderr_path.exists())

    def test_generic_timeout_terminates_descendants(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            parent_script = (
                "import subprocess,sys,time; "
                "child=subprocess.Popen([sys.executable,'-c','import time; time.sleep(30)']); "
                "print(child.pid,flush=True); time.sleep(30)"
            )
            result = MODULE._command_result(
                [sys.executable, "-c", parent_script],
                cwd=root,
                timeout=0.5,
            )

            self.assertTrue(result["timedOut"])
            child_pid = int(result["stdout"].strip().splitlines()[0])
            deadline = time.monotonic() + 3
            while time.monotonic() < deadline:
                try:
                    status = MODULE.psutil.Process(child_pid).status()
                except MODULE.psutil.NoSuchProcess:
                    break
                if status == MODULE.psutil.STATUS_ZOMBIE:
                    break
                time.sleep(0.05)
            else:
                self.fail(f"timed-out descendant {child_pid} is still running")


if __name__ == "__main__":
    unittest.main()
