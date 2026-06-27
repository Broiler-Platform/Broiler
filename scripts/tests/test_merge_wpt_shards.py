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
            self.assertNotIn("testPath", merged["results"][0])
            self.assertEqual(
                {"relativeTestPath", "passed", "skipped", "category"},
                set(merged["results"][0]),
            )

            markdown = MODULE.render_issue_markdown(merged, "https://example.test/run/1")
            self.assertIn("### Top 2 problems", markdown)
            self.assertIn("`PixelMismatch / MissingContent` — 3 failure(s)", markdown)
            self.assertIn("`Timeout` — 2 failure(s)", markdown)
            self.assertNotIn("`RenderingError` — 1 failure(s)", markdown)
            self.assertIn("Incomplete shards: 1", markdown)

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
