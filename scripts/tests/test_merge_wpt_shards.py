import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "merge-wpt-shards.py"
SPEC = importlib.util.spec_from_file_location("merge_wpt_shards", SCRIPT_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class MergeWptShardsTests(unittest.TestCase):
    def _write_report(
        self,
        root: Path,
        name: str,
        shard_index: int,
        results: list[dict],
    ) -> None:
        summary = {
            "passed": 1,
            "failed": len(results),
            "skipped": 0,
            "total": len(results) + 1,
        }
        (root / name).write_text(
            json.dumps(
                {
                    "summary": summary,
                    "shard": {"index": shard_index, "count": 8},
                    "results": results,
                }
            ),
            encoding="utf-8",
        )

    def _write_status(
        self,
        root: Path,
        shard_index: int,
        exit_code: int,
        failure_reason: str | None = None,
        failure_detail: str | None = None,
        attempt_suffix: str = "",
    ) -> None:
        status = {"shardIndex": shard_index, "exitCode": exit_code}
        # The workflow's "Collect shard result" step folds these in from the marker
        # run-wpt-tests.sh leaves when a shard dies before running a single test.
        if failure_reason is not None:
            status["failureReason"] = failure_reason
        if failure_detail is not None:
            status["failureDetail"] = failure_detail
        (root / f"wpt-shard-{shard_index}{attempt_suffix}-status.json").write_text(
            json.dumps(status),
            encoding="utf-8",
        )

    def test_merge_reports_bounded_common_problem_groups(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._write_report(
                shard_dir,
                "shard-0.json",
                0,
                [
                    self._failure("css/a/one.html", "PixelMismatch", "MissingContent"),
                    self._failure("css/a/two.html", "PixelMismatch", "MissingContent"),
                    self._failure("css/b/timeout.html", "Timeout"),
                ],
            )
            self._write_report(
                shard_dir,
                "shard-1.json",
                1,
                [
                    self._failure("html/a/three.html", "PixelMismatch", "MissingContent"),
                    self._failure("html/b/timeout.html", "Timeout"),
                    self._failure("html/c/render.html", "RenderingError"),
                ],
            )
            self._write_status(shard_dir, shard_index=0, exit_code=1)
            self._write_status(shard_dir, shard_index=7, exit_code=134)

            merged = MODULE.merge(shard_dir, problem_limit=2)

            self.assertEqual(2, merged["problemLimit"])
            self.assertEqual(
                ["PixelMismatch / MissingContent", "Timeout"],
                [problem["label"] for problem in merged["topProblems"]],
            )
            self.assertEqual([3, 2], [problem["count"] for problem in merged["topProblems"]])
            self.assertEqual(6, len(merged["results"]))
            self.assertEqual([{"shardIndex": 7, "exitCode": 134}], merged["incompleteShards"])
            # results[0] is a PixelMismatch/MissingContent case → carries subCategory (#10).
            self.assertNotIn("testPath", merged["results"][0])
            self.assertEqual(
                {"relativeTestPath", "passed", "skipped", "category", "subCategory"},
                set(merged["results"][0]),
            )
            self.assertEqual("MissingContent", merged["results"][0]["subCategory"])

            markdown = MODULE.render_issue_markdown(merged, "https://example.test/run/1")
            self.assertIn("### Top 2 problems", markdown)
            self.assertIn("`PixelMismatch / MissingContent` — 3 failure(s)", markdown)
            self.assertIn("`Timeout` — 2 failure(s)", markdown)
            self.assertNotIn("`RenderingError` — 1 failure(s)", markdown)
            self.assertIn("Incomplete shards: 1", markdown)

    def test_merge_aggregates_dropped_declarations(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            for name, idx, drops in (
                (
                    "shard-0.json",
                    0,
                    [
                        {"declaration": "text-align: -webkit-right", "count": 5},
                        {"declaration": "position: wobble", "count": 1},
                    ],
                ),
                ("shard-1.json", 1, [{"declaration": "text-align: -webkit-right", "count": 3}]),
            ):
                (shard_dir / name).write_text(
                    json.dumps(
                        {
                            "summary": {"passed": 1, "failed": 0, "skipped": 0, "total": 1},
                            "shard": {"index": idx, "count": 8},
                            "triage": {"droppedDeclarations": drops},
                            "results": [],
                        }
                    ),
                    encoding="utf-8",
                )

            merged = MODULE.merge(shard_dir, problem_limit=10)

            # Counts summed across shards, most frequent first.
            self.assertEqual(
                [("text-align: -webkit-right", 8), ("position: wobble", 1)],
                merged["droppedDeclarations"],
            )

            markdown = MODULE.render_issue_markdown(merged, None)
            self.assertIn("dropped CSS declarations", markdown)
            self.assertIn("`text-align: -webkit-right` — 8 occurrence(s)", markdown)

    def test_merge_aggregates_exception_signatures(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            for name, idx, signatures in (
                (
                    "shard-0.json",
                    0,
                    [
                        {"signature": "DomName..ctor — A prefixed name requires a namespace URI", "count": 4},
                        {"signature": "CssBox.Measure — overflow", "count": 1},
                    ],
                ),
                (
                    "shard-1.json",
                    1,
                    [{"signature": "DomName..ctor — A prefixed name requires a namespace URI", "count": 2}],
                ),
            ):
                (shard_dir / name).write_text(
                    json.dumps(
                        {
                            "summary": {"passed": 1, "failed": 0, "skipped": 0, "total": 1},
                            "shard": {"index": idx, "count": 8},
                            "triage": {"exceptionSignatures": signatures},
                            "results": [],
                        }
                    ),
                    encoding="utf-8",
                )

            merged = MODULE.merge(shard_dir, problem_limit=10)

            # Counts summed across shards, most frequent first.
            self.assertEqual(
                [
                    ("DomName..ctor — A prefixed name requires a namespace URI", 6),
                    ("CssBox.Measure — overflow", 1),
                ],
                merged["exceptionSignatures"],
            )

            markdown = MODULE.render_issue_markdown(merged, None)
            self.assertIn("exception signatures", markdown)
            self.assertIn(
                "`DomName..ctor — A prefixed name requires a namespace URI` — 6 failure(s)",
                markdown,
            )

    def test_merge_preserves_subcategory_in_results(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._write_report(
                shard_dir,
                "shard-0.json",
                0,
                [
                    self._failure("css/a/layout.html", "PixelMismatch", "LayoutShift"),
                    self._failure("css/b/render.html", "RenderingError"),
                ],
            )

            merged = MODULE.merge(shard_dir, problem_limit=10)

            by_path = {r["relativeTestPath"]: r for r in merged["results"]}
            # Pixel-mismatch record is self-describing: its sub-category round-trips.
            self.assertEqual("LayoutShift", by_path["css/a/layout.html"]["subCategory"])
            # A failure without a sub-category does not gain a null field.
            self.assertNotIn("subCategory", by_path["css/b/render.html"])

    def test_merge_clusters_numbered_families(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._write_report(
                shard_dir,
                "shard-0.json",
                0,
                [
                    self._failure("css/css-align/abspos/static-position-1.html", "PixelMismatch", "LayoutShift"),
                    self._failure("css/css-align/abspos/static-position-2.html", "PixelMismatch", "LayoutShift"),
                    self._failure("css/css-align/abspos/static-position-3.html", "ScriptError"),
                ],
            )
            self._write_report(
                shard_dir,
                "shard-1.json",
                1,
                [
                    self._failure("css/css-align/abspos/static-position-4.html", "PixelMismatch", "LayoutShift"),
                    # Non-numbered sibling: a singleton family, must not be reported.
                    self._failure("css/css-align/abspos/align-self.html", "PixelMismatch", "LayoutShift"),
                ],
            )

            merged = MODULE.merge(shard_dir, problem_limit=10)

            families = merged["failureFamilies"]
            self.assertEqual(1, len(families))
            family = families[0]
            self.assertEqual("css/css-align/abspos/static-position-{N}.html", family["family"])
            self.assertEqual(4, family["count"])
            self.assertEqual({"PixelMismatch": 3, "ScriptError": 1}, family["categories"])

            markdown = MODULE.render_issue_markdown(merged, None)
            self.assertIn("failure families", markdown)
            self.assertIn(
                "`css/css-align/abspos/static-position-{N}.html` — 4 failure(s)",
                markdown,
            )
            self.assertIn("PixelMismatch 3", markdown)

    def test_cli_requests_issue_for_incomplete_shard_without_test_results(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            github_output = shard_dir / "github-output.txt"
            self._write_status(shard_dir, shard_index=3, exit_code=134)

            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_PATH),
                    "--shard-dir",
                    temp,
                    "--github-output",
                    str(github_output),
                ],
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(0, result.returncode, result.stderr)
            outputs = github_output.read_text(encoding="utf-8")
            self.assertIn("failed_count=0", outputs)
            self.assertIn("incomplete_shard_count=1", outputs)
            self.assertIn("create_issue=true", outputs)

    def test_expected_shard_that_uploaded_nothing_is_flagged_incomplete(self) -> None:
        # Two of the four dispatched shards reported; the other two uploaded
        # neither a report nor a status file (their jobs were cancelled mid-run).
        # Without --expected-shards those two vanish silently, dropping ~half of
        # every merged count; with it they are surfaced as incomplete shards.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._write_report(
                shard_dir,
                "shard-0.json",
                0,
                [self._failure("css/a/one.html", "PixelMismatch", "MissingContent")],
            )
            self._write_report(
                shard_dir,
                "shard-1.json",
                1,
                [self._failure("html/a/two.html", "PixelMismatch", "MissingContent")],
            )

            # Baseline: no expected set → the missing shards stay invisible.
            blind = MODULE.merge(shard_dir)
            self.assertEqual([], blind["incompleteShards"])

            merged = MODULE.merge(shard_dir, expected_shard_indexes={0, 1, 2, 3})
            self.assertEqual(
                [
                    {"shardIndex": 2, "exitCode": "missing"},
                    {"shardIndex": 3, "exitCode": "missing"},
                ],
                merged["incompleteShards"],
            )

            markdown = MODULE.render_issue_markdown(merged, "https://example.test/run/1")
            self.assertIn("Incomplete shards: 2", markdown)
            self.assertIn("shard 2 (no report uploaded)", markdown)

    def test_reported_expected_shard_is_not_flagged(self) -> None:
        # A shard that both reported and left a status file is complete, and an
        # expected set that matches the reports produces no incomplete shards.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._write_report(
                shard_dir,
                "shard-0.json",
                0,
                [self._failure("css/a/one.html", "PixelMismatch", "MissingContent")],
            )
            self._write_status(shard_dir, shard_index=0, exit_code=1)

            merged = MODULE.merge(shard_dir, expected_shard_indexes={0})
            self.assertEqual([], merged["incompleteShards"])

    def test_recorded_failure_reason_replaces_the_eviction_guess(self) -> None:
        # Issue #1534: shard 0 exited 1 because the Playwright CDN refused its
        # Chromium download, so no test ran. The report used to attribute every
        # incomplete shard to a runner eviction, which sent triage looking for a
        # cancelled job that never existed.
        detail = (
            "The Playwright CDN refused this runner's Chromium download "
            "(HTTP 403 / AccessDenied). No test ran."
        )
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._write_status(
                shard_dir,
                shard_index=0,
                exit_code=69,
                failure_reason="BrowserDownloadBlocked",
                failure_detail=detail,
            )

            merged = MODULE.merge(shard_dir, expected_shard_indexes={0})
            self.assertEqual(
                [
                    {
                        "shardIndex": 0,
                        "exitCode": 69,
                        "failureReason": "BrowserDownloadBlocked",
                        "failureDetail": detail,
                    }
                ],
                merged["incompleteShards"],
            )

            problem = merged["biggestProblems"][0]
            self.assertEqual("IncompleteShards", problem["kind"])
            self.assertIn("shard 0 (Chromium download refused)", problem["detail"])
            self.assertIn(f"Shard 0: {detail}", problem["detail"])
            self.assertIn("CI infrastructure outage", problem["detail"])
            self.assertNotIn("eviction", problem["detail"])
            # The recovery action is unchanged — the block clears on its own.
            self.assertIn("set shard_index=0", problem["detail"])

    def test_unexplained_shard_keeps_the_eviction_guess(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._write_status(shard_dir, shard_index=2, exit_code=134)

            problem = MODULE.merge(shard_dir)["biggestProblems"][0]
            self.assertIn("shard 2 (exit 134)", problem["detail"])
            self.assertIn("eviction", problem["detail"])
            self.assertNotIn("CI infrastructure outage", problem["detail"])

    def test_mixed_shards_explain_each_cause_separately(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._write_status(
                shard_dir,
                shard_index=0,
                exit_code=69,
                failure_reason="BrowserInstallFailed",
                failure_detail="Installing Playwright Chromium failed. No test ran.",
            )
            self._write_status(shard_dir, shard_index=5, exit_code=1)

            merged = MODULE.merge(shard_dir)
            problem = merged["biggestProblems"][0]
            self.assertIn("shard 0 (Chromium install failed)", problem["detail"])
            self.assertIn("shard 5 (exit 1)", problem["detail"])
            self.assertIn("Shard 0: Installing Playwright Chromium failed.", problem["detail"])
            self.assertIn("eviction", problem["detail"])
            self.assertIn(
                "re-dispatch once per shard, setting shard_index to 0, 5", problem["detail"]
            )

    def test_unknown_failure_reason_is_surfaced_verbatim(self) -> None:
        # A reason added to the runner but not yet to the label table must not be
        # silently swallowed back into a bare exit code.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._write_status(
                shard_dir, shard_index=1, exit_code=70, failure_reason="DiskFull"
            )

            merged = MODULE.merge(shard_dir)
            markdown = MODULE.render_issue_markdown(merged, "https://example.test/run/1")
            self.assertIn("shard 1 (DiskFull)", markdown)

    def test_merge_into_preserves_out_of_scope_entries(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            # Manifest lives outside the scanned shard dir, as in production
            # (tests/wpt-baseline vs the downloaded shard-results dir).
            shard_dir = Path(temp) / "shards"
            shard_dir.mkdir()
            manifest = Path(temp) / "failed-tests.json"
            # Existing manifest: one css/a failure (in this run's scope) and one
            # html/z failure (NOT exercised by this run).
            manifest.write_text(
                json.dumps(
                    {
                        "summary": {"passed": 0, "failed": 2, "skipped": 0, "total": 2},
                        "results": [
                            {"relativeTestPath": "css/a/old-fail.html", "passed": False,
                             "skipped": False, "category": "RenderingError"},
                            {"relativeTestPath": "html/z/untouched.html", "passed": False,
                             "skipped": False, "category": "Timeout"},
                        ],
                    }
                ),
                encoding="utf-8",
            )
            # This run exercises css/a: old-fail.html now passes, new-fail.html fails,
            # skipped.html is skipped (inconclusive). It never touches html/z.
            (shard_dir / "shard-0.json").write_text(
                json.dumps(
                    {
                        "summary": {"passed": 1, "failed": 1, "skipped": 1, "total": 3},
                        "shard": {"index": 0, "count": 8},
                        "results": [
                            {"relativeTestPath": "css/a/old-fail.html", "passed": True,
                             "skipped": False, "category": "None"},
                            self._failure("css/a/new-fail.html", "RenderingError"),
                            {"relativeTestPath": "css/a/skipped.html", "passed": False,
                             "skipped": True, "category": "None"},
                        ],
                    }
                ),
                encoding="utf-8",
            )

            merged = MODULE.merge(shard_dir, problem_limit=10)
            executed = MODULE._collect_executed_paths(shard_dir)
            result = MODULE.merge_into_manifest(merged, executed, manifest)

            paths = {entry["relativeTestPath"] for entry in result["results"]}
            # Out-of-scope failure preserved; now-passing failure dropped; new
            # failure recorded; skipped test does not evict anything.
            self.assertEqual({"html/z/untouched.html", "css/a/new-fail.html"}, paths)
            self.assertEqual(2, result["summary"]["failed"])
            self.assertEqual(2, result["summary"]["total"])
            # A test that only skipped this run is inconclusive — not "executed".
            self.assertNotIn("css/a/skipped.html", executed)

    def test_merge_into_creates_manifest_when_absent(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._write_report(
                shard_dir, "shard-0.json", 0, [self._failure("css/a/one.html", "RenderingError")]
            )
            merged = MODULE.merge(shard_dir, problem_limit=10)
            executed = MODULE._collect_executed_paths(shard_dir)
            # No manifest on disk yet (first run).
            result = MODULE.merge_into_manifest(merged, executed, shard_dir / "does-not-exist.json")

            self.assertEqual(
                ["css/a/one.html"], [entry["relativeTestPath"] for entry in result["results"]]
            )

    def test_ranks_biggest_problems_by_blast_radius(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            (shard_dir / "shard-0.json").write_text(
                json.dumps(
                    {
                        "summary": {"passed": 5, "failed": 3, "skipped": 0, "total": 8},
                        "shard": {"index": 0, "count": 8},
                        "triage": {
                            "exceptionSignatures": [
                                {
                                    "signature": "Foo.Bar — boom",
                                    "count": 12,
                                    "examples": ["css/a/crash.html"],
                                }
                            ],
                            "lowestMatchTests": [
                                {
                                    "testPath": "css/a/broken.html",
                                    "matchPercent": 3.2,
                                    "category": "PixelMismatch",
                                    "subCategory": "MissingContent",
                                },
                                # A near-miss above the threshold: NOT a big problem.
                                {
                                    "testPath": "css/a/near.html",
                                    "matchPercent": 88.0,
                                    "category": "PixelMismatch",
                                    "subCategory": "LayoutShift",
                                },
                            ],
                        },
                        "results": [
                            self._failure("css/a/broken.html", "PixelMismatch", "MissingContent"),
                        ],
                    }
                ),
                encoding="utf-8",
            )
            # Shard 0 finished (with failures); shard 7 aborted before any report.
            self._write_status(shard_dir, shard_index=0, exit_code=1)
            self._write_status(shard_dir, shard_index=7, exit_code=134)

            merged = MODULE.merge(
                shard_dir, problem_limit=10, biggest_problem_limit=3, low_match_threshold=50.0
            )

            biggest = merged["biggestProblems"]
            # Ranked by blast radius: incomplete shard, then crash, then worst match.
            self.assertEqual(
                ["IncompleteShards", "Crash", "LowMatch"], [p["kind"] for p in biggest]
            )
            self.assertEqual(12, biggest[1]["impact"])
            # The crash carries the example test that hit it.
            self.assertEqual(["css/a/crash.html"], biggest[1]["examples"])
            low = [p for p in biggest if p["kind"] == "LowMatch"]
            self.assertEqual(1, len(low))
            self.assertEqual(3.2, low[0]["matchPercent"])
            self.assertIn("css/a/broken.html", low[0]["title"])

            markdown = MODULE.render_biggest_problems_markdown(merged, "https://example.test/run/1")
            self.assertIn("top 3 biggest problem(s)", markdown)
            self.assertIn("1 shard(s) did not complete", markdown)
            self.assertIn("shard 7 (exit 134)", markdown)
            # The incomplete-shard entry is actionable: it names the exact rerun
            # (shard_index) and explains a "no report uploaded" shard is usually a
            # transient runner eviction rather than a code regression.
            self.assertIn("set shard_index=7", markdown)
            self.assertIn("transient CI-runner eviction", markdown)
            self.assertIn("Crash gating 12 test(s)", markdown)
            self.assertIn("Foo.Bar — boom", markdown)
            self.assertIn("3.2% match — css/a/broken.html", markdown)
            # The crash names an example test and the report spells out a --render
            # reproduction pointed at the first reproducible test (the crash's).
            self.assertIn("Example test(s): `css/a/crash.html`", markdown)
            self.assertIn("### Reproduce locally", markdown)
            self.assertIn(
                "--wpt-dir tests/wpt/checkout --render tests/wpt/checkout/css/a/crash.html",
                markdown,
            )
            # The 88% near-miss is above threshold and never surfaces.
            self.assertNotIn("near.html", markdown)

    def test_crash_examples_union_across_shards_deduped_and_capped(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            # The same signature crashes in two shards, each reporting different
            # example paths; one path overlaps.
            (shard_dir / "shard-0.json").write_text(
                json.dumps(
                    {
                        "summary": {"passed": 0, "failed": 3, "skipped": 0, "total": 3},
                        "shard": {"index": 0, "count": 8},
                        "triage": {
                            "exceptionSignatures": [
                                {
                                    "signature": "Same.Sig — boom",
                                    "count": 2,
                                    "examples": ["css/a/one.html", "css/a/two.html"],
                                }
                            ]
                        },
                        "results": [],
                    }
                ),
                encoding="utf-8",
            )
            (shard_dir / "shard-1.json").write_text(
                json.dumps(
                    {
                        "summary": {"passed": 0, "failed": 3, "skipped": 0, "total": 3},
                        "shard": {"index": 1, "count": 8},
                        "triage": {
                            "exceptionSignatures": [
                                {
                                    "signature": "Same.Sig — boom",
                                    "count": 3,
                                    # two.html repeats (dedup); three/four push past cap.
                                    "examples": ["css/a/two.html", "css/a/three.html", "css/a/four.html"],
                                }
                            ]
                        },
                        "results": [],
                    }
                ),
                encoding="utf-8",
            )

            merged = MODULE.merge(shard_dir, biggest_problem_limit=3)

            crash = next(p for p in merged["biggestProblems"] if p["kind"] == "Crash")
            self.assertEqual(5, crash["impact"])
            # Union across shards, deduped, in first-seen order, capped at 3.
            self.assertEqual(
                ["css/a/one.html", "css/a/two.html", "css/a/three.html"], crash["examples"]
            )

    def test_biggest_problems_are_diversity_first(self) -> None:
        # Three crashes plus one low match, limit 3: strict severity tiers would
        # show three crashes and hide the low match. Diversity-first keeps the low
        # match by spending only one slot on the (worst) crash before covering the
        # other kind, then fills the last slot with the next crash.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            (shard_dir / "shard-0.json").write_text(
                json.dumps(
                    {
                        "summary": {"passed": 0, "failed": 4, "skipped": 0, "total": 4},
                        "shard": {"index": 0, "count": 8},
                        "triage": {
                            "exceptionSignatures": [
                                {"signature": "A.a — big", "count": 30},
                                {"signature": "B.b — mid", "count": 20},
                                {"signature": "C.c — small", "count": 6},
                            ],
                            "lowestMatchTests": [
                                {
                                    "testPath": "css/a/blank.html",
                                    "matchPercent": 2.0,
                                    "category": "PixelMismatch",
                                    "subCategory": "MissingContent",
                                }
                            ],
                        },
                        "results": [],
                    }
                ),
                encoding="utf-8",
            )

            merged = MODULE.merge(shard_dir, biggest_problem_limit=3)

            biggest = merged["biggestProblems"]
            self.assertEqual(3, len(biggest))
            kinds = [p["kind"] for p in biggest]
            # The low match survives; the smallest (count-6) crash is what drops.
            self.assertIn("LowMatch", kinds)
            self.assertEqual([30, 20], [p["impact"] for p in biggest if p["kind"] == "Crash"])
            # Result stays ordered by blast radius: crashes (tier 1) before the
            # low match (tier 2).
            self.assertEqual(["Crash", "Crash", "LowMatch"], kinds)

    def test_biggest_problem_limit_bounds_the_list(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            (shard_dir / "shard-0.json").write_text(
                json.dumps(
                    {
                        "summary": {"passed": 0, "failed": 3, "skipped": 0, "total": 3},
                        "shard": {"index": 0, "count": 8},
                        "triage": {
                            "exceptionSignatures": [
                                {"signature": "A.a — one", "count": 9},
                                {"signature": "B.b — two", "count": 4},
                                {"signature": "C.c — three", "count": 1},
                            ],
                        },
                        "results": [],
                    }
                ),
                encoding="utf-8",
            )

            merged = MODULE.merge(shard_dir, biggest_problem_limit=2)

            # Only the two biggest crashes survive the limit; the smallest drops.
            self.assertEqual(2, len(merged["biggestProblems"]))
            self.assertEqual([9, 4], [p["impact"] for p in merged["biggestProblems"]])

    def _low_match_report(self, root: Path, name: str, shard_index: int, matches: list[float]) -> None:
        (root / name).write_text(
            json.dumps(
                {
                    "summary": {"passed": 0, "failed": len(matches), "skipped": 0, "total": len(matches)},
                    "shard": {"index": shard_index, "count": 8},
                    "triage": {
                        "lowestMatchTests": [
                            {
                                "testPath": f"css/a/m{i}.html",
                                "matchPercent": match,
                                "category": "PixelMismatch",
                                "subCategory": "MissingContent",
                            }
                            for i, match in enumerate(matches)
                        ]
                    },
                    "results": [self._failure(f"css/a/m{i}.html", "PixelMismatch", "MissingContent") for i in range(len(matches))],
                }
            ),
            encoding="utf-8",
        )

    def test_low_match_threshold_widens_in_steps_until_issue_is_full(self) -> None:
        # Mismatches at 45/55/65%, no crashes or incomplete shards, limit 2. At the
        # 50% start only 45% qualifies (1 < 2), so the threshold steps to 60% where
        # 45% and 55% both qualify and the issue fills. It stops there — it does not
        # keep climbing to grab 65%.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._low_match_report(shard_dir, "shard-0.json", 0, [45.0, 55.0, 65.0])

            merged = MODULE.merge(shard_dir, biggest_problem_limit=2, low_match_threshold=50.0)

            self.assertEqual(60.0, merged["lowMatchThreshold"])
            biggest = merged["biggestProblems"]
            self.assertEqual(2, len(biggest))
            self.assertEqual(["LowMatch", "LowMatch"], [p["kind"] for p in biggest])
            self.assertEqual([45.0, 55.0], sorted(p["matchPercent"] for p in biggest))

            markdown = MODULE.render_biggest_problems_markdown(merged, None)
            self.assertIn("< 60% match", markdown)

    def test_low_match_threshold_not_widened_when_start_already_fills_issue(self) -> None:
        # Two sub-50% mismatches with limit 2: the issue is already full at the
        # start, so the threshold is left at 50% (a 70% mismatch stays hidden).
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._low_match_report(shard_dir, "shard-0.json", 0, [20.0, 30.0, 70.0])

            merged = MODULE.merge(shard_dir, biggest_problem_limit=2, low_match_threshold=50.0)

            self.assertEqual(50.0, merged["lowMatchThreshold"])
            self.assertEqual(2, len(merged["biggestProblems"]))
            self.assertEqual([20.0, 30.0], sorted(p["matchPercent"] for p in merged["biggestProblems"]))

    def test_low_match_threshold_stops_widening_when_nothing_left_to_find(self) -> None:
        # A single 30% mismatch with limit 3: the issue can never fill, but every
        # candidate is already below the 50% start, so widening would add nothing
        # and the threshold is left at 50% rather than climbing to the ceiling.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._low_match_report(shard_dir, "shard-0.json", 0, [30.0])

            merged = MODULE.merge(shard_dir, biggest_problem_limit=3, low_match_threshold=50.0)

            self.assertEqual(50.0, merged["lowMatchThreshold"])
            self.assertEqual(1, len(merged["biggestProblems"]))

    def test_low_match_threshold_widens_to_ceiling_for_near_miss(self) -> None:
        # Only a 96% near-miss with limit 3: the threshold climbs 50→60→…→100,
        # capping at the ceiling, and surfaces the mismatch there.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._low_match_report(shard_dir, "shard-0.json", 0, [96.0])

            merged = MODULE.merge(shard_dir, biggest_problem_limit=3, low_match_threshold=50.0)

            self.assertEqual(100.0, merged["lowMatchThreshold"])
            self.assertEqual(1, len(merged["biggestProblems"]))
            self.assertEqual("LowMatch", merged["biggestProblems"][0]["kind"])

    SUSPECT = (
        "⚠ suspect reference: Broiler matches its rel=match reference HTML (100.0%) "
        "but not the committed reference PNG — the committed reference is likely "
        "stale/incorrect, not a Broiler bug"
    )

    def _mixed_match_report(
        self, root: Path, name: str, shard_index: int, entries: list[tuple[str, float, bool]]
    ) -> None:
        """Write a shard report whose lowestMatchTests mix ordinary mismatches with
        reference disagreements (``suspect`` = the runner cleared it)."""
        (root / name).write_text(
            json.dumps(
                {
                    "summary": {
                        "passed": 0,
                        "failed": len(entries),
                        "skipped": 0,
                        "total": len(entries),
                    },
                    "shard": {"index": shard_index, "count": 8},
                    "triage": {
                        "lowestMatchTests": [
                            {
                                "testPath": path,
                                "matchPercent": match,
                                "category": "PixelMismatch",
                                "subCategory": "MissingContent",
                                **({"suspectReference": self.SUSPECT} if suspect else {}),
                            }
                            for path, match, suspect in entries
                        ]
                    },
                    "results": [
                        self._failure(path, "PixelMismatch", "MissingContent")
                        for path, _, _ in entries
                    ],
                }
            ),
            encoding="utf-8",
        )

    def test_reference_disagreements_are_not_ranked_as_biggest_problems(self) -> None:
        # The shape this run actually reports: several 0.0% matches that Broiler
        # renders exactly as the test's own rel=match reference asks, plus one real
        # mismatch. Only the real one is a "biggest problem" — the cleared ones are
        # reported separately rather than occupying the severity list.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._mixed_match_report(
                shard_dir,
                "shard-0.json",
                0,
                [
                    ("css/css-image-animation/paused.html", 0.0, True),
                    ("css/mediaqueries/at-custom-media-basic.html", 0.0, True),
                    ("fullscreen/rendering/backdrop-object.html", 1.1, False),
                ],
            )

            merged = MODULE.merge(
                shard_dir, biggest_problem_limit=3, low_match_threshold=50.0
            )

            biggest = merged["biggestProblems"]
            self.assertEqual(
                ["fullscreen/rendering/backdrop-object.html"],
                [p["relativeTestPath"] for p in biggest],
            )

            disagreements = merged["referenceDisagreements"]
            self.assertEqual(
                [
                    "css/css-image-animation/paused.html",
                    "css/mediaqueries/at-custom-media-basic.html",
                ],
                [entry["relativeTestPath"] for entry in disagreements],
            )
            self.assertEqual([0.0, 0.0], [entry["matchPercent"] for entry in disagreements])

            markdown = MODULE.render_biggest_problems_markdown(merged, None)
            self.assertIn("Not ranked — reference disagreements", markdown)
            self.assertIn("css/mediaqueries/at-custom-media-basic.html", markdown)
            # The reproduce hint points at a real problem, not a cleared one.
            self.assertIn(
                "--render tests/wpt/checkout/fullscreen/rendering/backdrop-object.html",
                markdown,
            )

    def test_reference_disagreements_do_not_drive_threshold_escalation(self) -> None:
        # A cleared 0.0% is not a ranking candidate, so it must not count towards
        # filling the issue — the threshold widens past the real 96% near-miss and
        # surfaces it, exactly as it would if the cleared test were absent.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._mixed_match_report(
                shard_dir,
                "shard-0.json",
                0,
                [("css/a/cleared.html", 0.0, True), ("css/a/real.html", 96.0, False)],
            )

            merged = MODULE.merge(
                shard_dir, biggest_problem_limit=3, low_match_threshold=50.0
            )

            self.assertEqual(100.0, merged["lowMatchThreshold"])
            self.assertEqual(
                ["css/a/real.html"],
                [p["relativeTestPath"] for p in merged["biggestProblems"]],
            )

    def test_all_disagreements_yields_no_biggest_problems(self) -> None:
        # When every mismatch is cleared there is nothing to rank. The severity
        # issue must not fall back to ranking them anyway, and escalation must
        # terminate rather than spin to the ceiling looking for candidates.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._mixed_match_report(
                shard_dir,
                "shard-0.json",
                0,
                [("css/a/one.html", 0.0, True), ("css/a/two.html", 2.5, True)],
            )

            merged = MODULE.merge(
                shard_dir, biggest_problem_limit=3, low_match_threshold=50.0
            )

            self.assertEqual([], merged["biggestProblems"])
            self.assertEqual(2, len(merged["referenceDisagreements"]))

    def _scored_report(
        self, root: Path, name: str, entries: list[tuple[str, float, float | None]]
    ) -> None:
        """Write a shard report whose lowestMatchTests carry a ``referenceMatchPercent``
        — the score against the test's own rel=match reference, short of the gate."""
        (root / name).write_text(
            json.dumps(
                {
                    "summary": {
                        "passed": 0,
                        "failed": len(entries),
                        "skipped": 0,
                        "total": len(entries),
                    },
                    "shard": {"index": 0, "count": 1},
                    "triage": {
                        "lowestMatchTests": [
                            {
                                "testPath": path,
                                "matchPercent": match,
                                "category": "PixelMismatch",
                                "subCategory": "LayoutShift",
                                **(
                                    {"referenceMatchPercent": reference}
                                    if reference is not None
                                    else {}
                                ),
                            }
                            for path, match, reference in entries
                        ]
                    },
                    "results": [
                        self._failure(path, "PixelMismatch", "LayoutShift")
                        for path, _, _ in entries
                    ],
                }
            ),
            encoding="utf-8",
        )

    def test_a_sub_threshold_reference_score_is_reported_beside_the_golden(self) -> None:
        # The case the runner used to say nothing about: `suspectReference` is only set
        # when Broiler *clears the gate* against the test's own reference, so a test at
        # 94% against it and 0.8% against the golden was ranked as though nothing were
        # known — indistinguishable from one that is wrong against both.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._scored_report(
                shard_dir,
                "shard-0.json",
                [
                    ("css/css-grid/column-subgrid-auto-fill-003.html", 0.8, 94.0),
                    ("css/css-grid/column-subgrid-auto-fill-008.html", 11.5, 10.4),
                ],
            )

            merged = MODULE.merge(
                shard_dir, biggest_problem_limit=5, low_match_threshold=50.0
            )
            details = {
                problem["relativeTestPath"]: problem["detail"]
                for problem in merged["biggestProblems"]
            }

            # Far closer to its own reference than to the golden: the references
            # disagree, and chasing it would mean rendering less than the test asks for.
            disagreement = details["css/css-grid/column-subgrid-auto-fill-003.html"]
            self.assertIn("94.0%", disagreement)
            self.assertIn("references disagreeing", disagreement)

            # Wrong against both, so the second number must not excuse it.
            real_gap = details["css/css-grid/column-subgrid-auto-fill-008.html"]
            self.assertIn("10.4%", real_gap)
            self.assertNotIn("references disagreeing", real_gap)

    def test_both_classes_stay_ranked_as_biggest_problems(self) -> None:
        # Recording the second score annotates the ranking; it does not silently drop a
        # test out of it. Only a reference Broiler actually reproduced does that, and
        # whether these belong in won't-fix is a maintainer's call, not the report's.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._scored_report(
                shard_dir, "shard-0.json", [("css/a/near.html", 0.8, 94.0)]
            )

            merged = MODULE.merge(
                shard_dir, biggest_problem_limit=5, low_match_threshold=50.0
            )

            self.assertEqual(
                ["css/a/near.html"],
                [p["relativeTestPath"] for p in merged["biggestProblems"]],
            )
            self.assertEqual([], merged["referenceDisagreements"])

    def test_a_report_without_reference_scores_reads_as_before(self) -> None:
        # A run without --verify-reference carries no referenceMatchPercent key at all.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._scored_report(shard_dir, "shard-0.json", [("css/a/plain.html", 4.0, None)])

            merged = MODULE.merge(
                shard_dir, biggest_problem_limit=5, low_match_threshold=50.0
            )
            detail = merged["biggestProblems"][0]["detail"]

            self.assertNotIn("rel=match", detail)
            self.assertTrue(detail.endswith("(PixelMismatch / LayoutShift)."))

    def test_low_match_tests_without_verification_are_ranked_as_before(self) -> None:
        # Reports from a run without --verify-reference carry no suspectReference
        # key at all; ranking must be unchanged for them.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._low_match_report(shard_dir, "shard-0.json", 0, [3.2, 40.0])

            merged = MODULE.merge(
                shard_dir, biggest_problem_limit=3, low_match_threshold=50.0
            )

            self.assertEqual([], merged["referenceDisagreements"])
            self.assertEqual(
                [3.2, 40.0], sorted(p["matchPercent"] for p in merged["biggestProblems"])
            )
            self.assertNotIn(
                "reference disagreements",
                MODULE.render_biggest_problems_markdown(merged, None),
            )

    def test_cli_emits_biggest_issue_outputs(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            github_output = shard_dir / "github-output.txt"
            biggest_md = shard_dir / "biggest.md"
            (shard_dir / "shard-0.json").write_text(
                json.dumps(
                    {
                        "summary": {"passed": 1, "failed": 1, "skipped": 0, "total": 2},
                        "shard": {"index": 0, "count": 8},
                        "triage": {
                            "exceptionSignatures": [{"signature": "Crash.Here — kaput", "count": 7}]
                        },
                        "results": [self._failure("css/a/x.html", "ScriptError")],
                    }
                ),
                encoding="utf-8",
            )

            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_PATH),
                    "--shard-dir",
                    temp,
                    "--biggest-issue-md",
                    str(biggest_md),
                    "--github-output",
                    str(github_output),
                ],
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(0, result.returncode, result.stderr)
            outputs = github_output.read_text(encoding="utf-8")
            self.assertIn("create_biggest_issue=true", outputs)
            self.assertIn("biggest_problem_count=1", outputs)
            self.assertIn("Crash gating 7 test(s)", biggest_md.read_text(encoding="utf-8"))

    def test_biggest_issue_widens_threshold_to_surface_near_miss(self) -> None:
        # Only a single near-miss mismatch (97.5%), no crash, no incomplete shard.
        # At the 50% start it clears no bar, but the threshold is widened in
        # 10-point steps until the mismatch surfaces, so the second issue is still
        # filed instead of silently swallowing the run's only severe signal.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            github_output = shard_dir / "github-output.txt"
            biggest_md = shard_dir / "biggest.md"
            (shard_dir / "shard-0.json").write_text(
                json.dumps(
                    {
                        "summary": {"passed": 1, "failed": 1, "skipped": 0, "total": 2},
                        "shard": {"index": 0, "count": 8},
                        "triage": {
                            "lowestMatchTests": [
                                {
                                    "testPath": "css/a/x.html",
                                    "matchPercent": 97.5,
                                    "category": "PixelMismatch",
                                    "subCategory": "LayoutShift",
                                }
                            ]
                        },
                        "results": [self._failure("css/a/x.html", "PixelMismatch", "LayoutShift")],
                    }
                ),
                encoding="utf-8",
            )
            self._write_status(shard_dir, shard_index=0, exit_code=1)

            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_PATH),
                    "--shard-dir",
                    temp,
                    "--biggest-issue-md",
                    str(biggest_md),
                    "--github-output",
                    str(github_output),
                ],
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(0, result.returncode, result.stderr)
            outputs = github_output.read_text(encoding="utf-8")
            # The widened threshold captures the near-miss → second issue is filed.
            self.assertIn("create_biggest_issue=true", outputs)
            self.assertIn("biggest_problem_count=1", outputs)
            self.assertIn("create_issue=true", outputs)
            # The issue text reports the widened cut-off (100%), not the 50% start.
            markdown = biggest_md.read_text(encoding="utf-8")
            self.assertIn("< 100% match", markdown)
            self.assertIn("97.5% match — css/a/x.html", markdown)

    def test_no_biggest_issue_when_no_severity_signals(self) -> None:
        # A plain failure with no crash, no incomplete shard, and no pixel
        # mismatch at all: widening the threshold has nothing to find, so no
        # second issue is filed.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            github_output = shard_dir / "github-output.txt"
            (shard_dir / "shard-0.json").write_text(
                json.dumps(
                    {
                        "summary": {"passed": 1, "failed": 1, "skipped": 0, "total": 2},
                        "shard": {"index": 0, "count": 8},
                        "results": [self._failure("css/a/x.html", "RenderingError")],
                    }
                ),
                encoding="utf-8",
            )
            self._write_status(shard_dir, shard_index=0, exit_code=1)

            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_PATH),
                    "--shard-dir",
                    temp,
                    "--github-output",
                    str(github_output),
                ],
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(0, result.returncode, result.stderr)
            outputs = github_output.read_text(encoding="utf-8")
            # Nothing to widen into → no second issue.
            self.assertIn("create_biggest_issue=false", outputs)
            self.assertIn("biggest_problem_count=0", outputs)
            # The run still failed, so the primary (most-common) issue is still filed.
            self.assertIn("create_issue=true", outputs)

    def test_cli_rejects_out_of_range_low_match_threshold(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_PATH),
                    "--shard-dir",
                    temp,
                    "--low-match-threshold",
                    "150",
                ],
                capture_output=True,
                text=True,
                check=False,
            )

        self.assertNotEqual(0, result.returncode)
        self.assertIn("--low-match-threshold must be between 0 and 100", result.stderr)

    def test_cli_rejects_non_positive_problem_limit(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_PATH),
                    "--shard-dir",
                    temp,
                    "--problem-limit",
                    "0",
                ],
                capture_output=True,
                text=True,
                check=False,
            )

        self.assertNotEqual(0, result.returncode)
        self.assertIn("--problem-limit must be a positive integer", result.stderr)

    # ── The end-of-workflow retry pass ───────────────────────────────────────
    #
    # A shard that aborts abnormally is rerun once at the end of the workflow and
    # uploads its files with a `-retry` suffix, next to whatever the aborted
    # attempt managed to leave behind. These cover the reconciliation: the retry
    # replaces the attempt it repeats, and never adds to it.

    def test_retry_report_supersedes_the_aborted_attempt(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            # Shard 0 died partway through: one failure recorded, then the crash.
            self._write_report(
                shard_dir,
                "wpt-shard-0.json",
                0,
                [self._failure("css/a/one.html", "RenderingError")],
            )
            self._write_status(shard_dir, shard_index=0, exit_code=134)
            # The rerun measured the whole slice.
            self._write_report(
                shard_dir,
                "wpt-shard-0-retry.json",
                0,
                [
                    self._failure("css/a/one.html", "RenderingError"),
                    self._failure("css/a/two.html", "Timeout"),
                ],
            )
            self._write_status(shard_dir, shard_index=0, exit_code=1, attempt_suffix="-retry")
            self._write_report(
                shard_dir,
                "wpt-shard-1.json",
                1,
                [self._failure("html/a/three.html", "Timeout")],
            )
            self._write_status(shard_dir, shard_index=1, exit_code=1)

            merged = MODULE.merge(shard_dir, expected_shard_indexes={0, 1})

            # Two shards' worth of totals, not three: the aborted attempt's
            # summary is dropped, not summed with its rerun's.
            self.assertEqual(2, merged["shardCount"])
            self.assertEqual(3, merged["summary"]["failed"])
            self.assertEqual(2, merged["summary"]["passed"])
            self.assertEqual(5, merged["summary"]["total"])
            # The rerun produced a report, so the slice is measured after all.
            self.assertEqual([], merged["incompleteShards"])
            self.assertEqual([0], merged["retriedShards"])

            markdown = MODULE.render_issue_markdown(merged, "https://example.test/run/1")
            self.assertIn("Shards rerun after an abnormal abort: 0", markdown)
            self.assertIn("Incomplete shards: 0", markdown)

    def test_shard_that_aborts_again_is_reported_as_retried(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            self._write_report(
                shard_dir,
                "wpt-shard-1.json",
                1,
                [self._failure("html/a/three.html", "Timeout")],
            )
            self._write_status(shard_dir, shard_index=1, exit_code=1)
            # Shard 0 aborted, was rerun, and aborted again — no report either time.
            self._write_status(
                shard_dir,
                shard_index=0,
                exit_code=1,
                failure_reason="BrowserDownloadBlocked",
                failure_detail="Chromium download returned HTTP 403.",
            )
            self._write_status(
                shard_dir,
                shard_index=0,
                exit_code=1,
                failure_reason="BrowserDownloadBlocked",
                failure_detail="Chromium download returned HTTP 403.",
                attempt_suffix="-retry",
            )

            merged = MODULE.merge(shard_dir, expected_shard_indexes={0, 1})

            self.assertEqual(
                [
                    {
                        "shardIndex": 0,
                        "exitCode": 1,
                        "retried": True,
                        "failureReason": "BrowserDownloadBlocked",
                        "failureDetail": "Chromium download returned HTTP 403.",
                    }
                ],
                merged["incompleteShards"],
            )
            self.assertEqual([0], merged["retriedShards"])

            biggest = MODULE.render_biggest_problems_markdown(merged, None)
            self.assertIn("shard 0 (Chromium download refused after rerun)", biggest)
            self.assertIn("already rerun automatically at the end of this run", biggest)

    def test_incomplete_shards_drive_the_retry_matrix(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            github_output = shard_dir / "github-output.txt"
            # Shard 0 reported; shard 2 crashed after running; shard 1 uploaded
            # nothing at all (its runner was lost mid-run).
            self._write_report(
                shard_dir,
                "wpt-shard-0.json",
                0,
                [self._failure("css/a/one.html", "Timeout")],
            )
            self._write_status(shard_dir, shard_index=0, exit_code=1)
            self._write_status(shard_dir, shard_index=2, exit_code=134)

            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_PATH),
                    "--shard-dir",
                    temp,
                    "--expected-shards",
                    "0,1,2",
                    "--github-output",
                    str(github_output),
                ],
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(0, result.returncode, result.stderr)
            outputs = github_output.read_text(encoding="utf-8")
            self.assertIn("has_incomplete_shards=true\n", outputs)
            self.assertIn("incomplete_shard_indexes=1,2\n", outputs)
            self.assertIn(
                'incomplete_shard_matrix=[{"shard-index": 1}, {"shard-index": 2}]\n',
                outputs,
            )
            self.assertIn("retried_shard_count=0\n", outputs)

    def test_failing_but_complete_shard_is_not_retried(self) -> None:
        # The retry pass exists for shards that went unmeasured, not for shards
        # that measured their slice and found failures — rerunning those would
        # burn a full run to produce the same answer.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp)
            github_output = shard_dir / "github-output.txt"
            self._write_report(
                shard_dir,
                "wpt-shard-0.json",
                0,
                [self._failure("css/a/one.html", "PixelMismatch", "LayoutShift")],
            )
            self._write_status(shard_dir, shard_index=0, exit_code=1)

            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_PATH),
                    "--shard-dir",
                    temp,
                    "--expected-shards",
                    "0",
                    "--github-output",
                    str(github_output),
                ],
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(0, result.returncode, result.stderr)
            outputs = github_output.read_text(encoding="utf-8")
            self.assertIn("has_incomplete_shards=false\n", outputs)
            self.assertIn("incomplete_shard_indexes=\n", outputs)
            self.assertIn("incomplete_shard_matrix=[]\n", outputs)
            # The failure itself is still reported, just not rerun.
            self.assertIn("failed_count=1\n", outputs)

    def test_retry_supersedes_the_aborted_attempt_in_the_manifest(self) -> None:
        # --merge-into scopes persistence to the tests a run exercised. A rerun
        # shard's *rerun* results are that scope; the aborted attempt's partial
        # verdicts must not resurrect entries the rerun has since passed.
        with tempfile.TemporaryDirectory() as temp:
            shard_dir = Path(temp) / "shards"
            shard_dir.mkdir()
            manifest = Path(temp) / "failed-tests.json"
            manifest.write_text(
                json.dumps(
                    {
                        "summary": {"passed": 0, "failed": 1, "skipped": 0, "total": 1},
                        "results": [
                            {
                                "relativeTestPath": "css/a/one.html",
                                "passed": False,
                                "skipped": False,
                                "category": "RenderingError",
                            }
                        ],
                    }
                ),
                encoding="utf-8",
            )
            # The aborted attempt saw css/a/one.html fail; the rerun saw it pass
            # (it is absent from the rerun's failures) and css/a/two.html fail.
            self._write_report(
                shard_dir,
                "wpt-shard-0.json",
                0,
                [self._failure("css/a/one.html", "RenderingError")],
            )
            self._write_status(shard_dir, shard_index=0, exit_code=134)
            (shard_dir / "wpt-shard-0-retry.json").write_text(
                json.dumps(
                    {
                        "summary": {"passed": 1, "failed": 1, "skipped": 0, "total": 2},
                        "shard": {"index": 0, "count": 8},
                        "results": [
                            {
                                "relativeTestPath": "css/a/one.html",
                                "passed": True,
                                "skipped": False,
                                "category": "Pass",
                            },
                            self._failure("css/a/two.html", "Timeout"),
                        ],
                    }
                ),
                encoding="utf-8",
            )
            self._write_status(shard_dir, shard_index=0, exit_code=1, attempt_suffix="-retry")

            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_PATH),
                    "--shard-dir",
                    str(shard_dir),
                    "--merge-into",
                    str(manifest),
                    "--merged-json",
                    str(manifest),
                ],
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(0, result.returncode, result.stderr)
            written = json.loads(manifest.read_text(encoding="utf-8"))
            self.assertEqual(
                ["css/a/two.html"],
                [entry["relativeTestPath"] for entry in written["results"]],
            )

    @staticmethod
    def _failure(path: str, category: str, sub_category: str | None = None) -> dict:
        result = {
            "testPath": f"/tmp/wpt/{path}",
            "relativeTestPath": path,
            "passed": False,
            "skipped": False,
            "category": category,
            "message": f"{category} at {path}",
        }
        if sub_category:
            result["mismatchDiagnostics"] = {
                "subCategory": sub_category,
                "summary": "Representative diagnostic",
            }
        return result


if __name__ == "__main__":
    unittest.main()
