"""Tests for scripts/ci-commit-generated-results.sh.

The script exists because of a real Octane run that benchmarked for hours and
then threw the results away::

    [main 0ab8848] ci: update Octane benchmark results [skip ci]
     6 files changed, 1415 insertions(+), 375 deletions(-)
     ! [rejected]        HEAD -> main (fetch first)
    error: failed to push some refs to '.../Broiler'

Something merged to main while the job ran. What these tests pin down is that
the losing push is retried against the branch as it is *now* — and that the work
which won the race is still there afterwards, since the whole point is to let
people merge PRs during a benchmark rather than around it.

Everything runs against real local repositories: the behaviour under test is git
behaviour (what a non-fast-forward rejection does, what survives a rebuild),
which cannot be checked by reading the script.
"""

from pathlib import Path
import json
import subprocess
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / "ci-commit-generated-results.sh"

RESULTS_DIR = "tests/octane/results"
MESSAGE = "ci: update Octane benchmark results [skip ci]"


def git(*args: str, cwd: Path) -> str:
    return subprocess.run(
        ["git", *args],
        cwd=cwd,
        capture_output=True,
        text=True,
        check=True,
    ).stdout


class CiCommitGeneratedResultsTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        self.addCleanup(self._tmp.cleanup)

        # A bare "GitHub", one clone standing in for the benchmark job, and a
        # second clone standing in for everyone else merging while it runs.
        self.remote = root / "remote.git"
        subprocess.run(
            ["git", "init", "--bare", "-b", "main", str(self.remote)],
            check=True,
            capture_output=True,
        )

        self.job = self._clone(root / "job")
        self.job_results = self.job / RESULTS_DIR
        self.job_results.mkdir(parents=True)
        (self.job_results / "comparison.md").write_text("baseline\n")
        (self.job / "README.md").write_text("Broiler\n")
        git("add", "-A", cwd=self.job)
        git("commit", "-m", "initial", cwd=self.job)
        git("push", "-u", "origin", "main", cwd=self.job)

        self.other = self._clone(root / "other")

    def _clone(self, path: Path) -> Path:
        subprocess.run(
            ["git", "clone", str(self.remote), str(path)],
            check=True,
            capture_output=True,
        )
        git("config", "user.name", "Test", cwd=path)
        git("config", "user.email", "test@example.com", cwd=path)
        return path

    def _run(self, *extra: str, cwd: Path | None = None) -> subprocess.CompletedProcess:
        return subprocess.run(
            [
                "bash",
                str(SCRIPT),
                "--branch",
                "main",
                "--message",
                MESSAGE,
                "--delay",
                "0",
                *extra,
                f"{RESULTS_DIR}/",
            ],
            cwd=cwd or self.job,
            capture_output=True,
            text=True,
        )

    def _land_on_main(self, path: str, contents: str, message: str) -> str:
        """Push a competing commit from the other clone, as a merged PR would."""
        git("pull", "--ff-only", "origin", "main", cwd=self.other)
        target = self.other / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(contents)
        git("add", "-A", cwd=self.other)
        git("commit", "-m", message, cwd=self.other)
        git("push", "origin", "main", cwd=self.other)
        return git("rev-parse", "HEAD", cwd=self.other).strip()

    def _remote_file(self, path: str) -> str:
        return git("show", f"main:{path}", cwd=self.remote)

    # --- the reported failure ------------------------------------------------

    def test_results_land_even_though_the_branch_moved_during_the_run(self) -> None:
        (self.job_results / "comparison.md").write_text("measured\n")
        landed = self._land_on_main("src/Feature.cs", "// merged PR\n", "feat: land a PR")

        result = self._run()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self._remote_file(f"{RESULTS_DIR}/comparison.md"), "measured\n")
        # The PR that won the race is still on the branch, and is the parent of
        # the results commit rather than something it replaced.
        self.assertEqual(self._remote_file("src/Feature.cs"), "// merged PR\n")
        self.assertEqual(
            git("rev-parse", "main^", cwd=self.remote).strip(),
            landed,
        )
        self.assertEqual(
            git("log", "-1", "--format=%s", "main", cwd=self.remote).strip(),
            MESSAGE,
        )

    def test_it_keeps_retrying_while_the_branch_keeps_moving(self) -> None:
        """A busy afternoon: a PR lands just before each of the first two pushes."""
        (self.job_results / "comparison.md").write_text("measured\n")

        # A pre-push hook in the job clone lands a PR from the other clone right
        # before each of the first two attempts leaves, so the retry is racing
        # too rather than pushing into a branch that has settled down.
        counter = self.job / ".git" / "push-attempts"
        hook = self.job / ".git" / "hooks" / "pre-push"
        hook.write_text(
            "#!/usr/bin/env bash\n"
            # Hooks inherit GIT_DIR/GIT_WORK_TREE, which would point every git
            # command below back at the pushing repository.
            "unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE GIT_PREFIX\n"
            "cat >/dev/null\n"
            f'n=$(cat "{counter}" 2>/dev/null || echo 0); n=$((n + 1)); echo "$n" > "{counter}"\n'
            '[ "$n" -gt 2 ] && exit 0\n'
            f'cd "{self.other}"\n'
            "git pull -q --ff-only origin main\n"
            'printf "// PR %s\\n" "$n" > "src/PR$n.cs"\n'
            "git add -A && git commit -qm \"feat: land PR $n\" && git push -q origin main\n"
            "exit 0\n"
        )
        hook.chmod(0o755)
        (self.other / "src").mkdir(parents=True, exist_ok=True)

        result = self._run("--attempts", "5")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(counter.read_text().strip(), "3")
        self.assertEqual(self._remote_file(f"{RESULTS_DIR}/comparison.md"), "measured\n")
        self.assertEqual(self._remote_file("src/PR1.cs"), "// PR 1\n")
        self.assertEqual(self._remote_file("src/PR2.cs"), "// PR 2\n")

    # --- what the rebuild keeps and drops ------------------------------------

    def test_the_benchmarks_own_output_wins_over_a_competing_results_commit(self) -> None:
        """Result files are generated wholesale, so the last measurement wins."""
        (self.job_results / "comparison.md").write_text("measured by this job\n")
        self._land_on_main(
            f"{RESULTS_DIR}/comparison.md",
            "measured by an older job\n",
            "ci: update Octane benchmark results [skip ci]",
        )

        result = self._run()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(
            self._remote_file(f"{RESULTS_DIR}/comparison.md"),
            "measured by this job\n",
        )

    def test_a_result_file_this_run_no_longer_produces_is_removed(self) -> None:
        (self.job_results / "comparison.md").write_text("measured\n")
        self._land_on_main(
            f"{RESULTS_DIR}/stale-engine.json",
            json.dumps({"engine": "gone"}),
            "ci: update Octane benchmark results [skip ci]",
        )

        result = self._run()

        self.assertEqual(result.returncode, 0, result.stderr)
        tracked = git("ls-tree", "-r", "--name-only", "main", cwd=self.remote).split()
        self.assertNotIn(f"{RESULTS_DIR}/stale-engine.json", tracked)
        self.assertIn(f"{RESULTS_DIR}/comparison.md", tracked)

    def test_files_outside_the_result_paths_are_never_touched(self) -> None:
        (self.job_results / "comparison.md").write_text("measured\n")
        # Job-local churn in a tracked file outside the results: the rebuild
        # resets the working tree, so this must not ride along into the commit.
        (self.job / "README.md").write_text("scribbled on by the job\n")
        self._land_on_main("README.md", "Broiler, edited by a PR\n", "docs: edit readme")

        result = self._run()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self._remote_file("README.md"), "Broiler, edited by a PR\n")

    # --- the quiet and the loud endings --------------------------------------

    def test_nothing_measured_means_nothing_committed(self) -> None:
        before = git("rev-parse", "main", cwd=self.remote).strip()

        result = self._run()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("No result changes to commit.", result.stdout)
        self.assertEqual(git("rev-parse", "main", cwd=self.remote).strip(), before)

    def test_first_ever_result_file_is_committed(self) -> None:
        """An untracked new file still counts as a change worth pushing."""
        (self.job_results / "diagnostics.md").write_text("first run\n")

        result = self._run()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self._remote_file(f"{RESULTS_DIR}/diagnostics.md"), "first run\n")

    def test_identical_results_already_on_the_branch_end_the_retry(self) -> None:
        (self.job_results / "comparison.md").write_text("measured\n")
        self._land_on_main(
            f"{RESULTS_DIR}/comparison.md",
            "measured\n",
            "ci: update Octane benchmark results [skip ci]",
        )

        result = self._run()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("already carries these results", result.stdout)

    def test_a_push_that_never_succeeds_fails_the_job(self) -> None:
        """Exhausted attempts must be loud: a green job with no results is worse."""
        reject = self.remote / "hooks" / "pre-receive"
        reject.write_text("#!/usr/bin/env bash\necho 'rejected by test'\nexit 1\n")
        reject.chmod(0o755)
        (self.job_results / "comparison.md").write_text("measured\n")

        result = self._run("--attempts", "2")

        self.assertNotEqual(result.returncode, 0)
        self.assertIn("after 2 attempts", result.stderr)

    def test_missing_arguments_are_rejected(self) -> None:
        result = subprocess.run(
            ["bash", str(SCRIPT), "--branch", "main", "--message", MESSAGE],
            cwd=self.job,
            capture_output=True,
            text=True,
        )
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("at least one path is required", result.stderr)


if __name__ == "__main__":
    unittest.main()
