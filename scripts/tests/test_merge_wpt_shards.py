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

    def _write_status(self, root: Path, shard_index: int, exit_code: int) -> None:
        (root / f"wpt-shard-{shard_index}-status.json").write_text(
            json.dumps({"shardIndex": shard_index, "exitCode": exit_code}),
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
