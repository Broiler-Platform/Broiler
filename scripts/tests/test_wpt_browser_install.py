"""Tests for scripts/lib/wpt-browser-install.sh.

The library is sourced into a real bash subprocess and driven from there. That is
deliberate: what these tests pin down is shell semantics — above all that a failed
command's exit status reaches the caller — and those cannot be checked by reading
the script. The original helper captured ``$?`` after an ``if cmd; then ... fi``,
which yields the *if compound's* status (0 when the condition failed and there is
no else branch), so it reported "(exit 0)" and returned success after every
attempt had failed. run-wpt-tests.sh then walked on into a reference-generation
pass with no browser (issue #1534).
"""

import json
from pathlib import Path
import subprocess
import tempfile
import unittest


LIB_PATH = Path(__file__).resolve().parents[1] / "lib" / "wpt-browser-install.sh"

# Trimmed from the real shard-0 output of workflow run 31008929475.
GEO_BLOCK_OUTPUT = (
    "Downloading Chrome for Testing 147.0.7727.15 (playwright chromium v1217) from "
    "https://cdn.playwright.dev/builds/cft/147.0.7727.15/linux64/chrome-linux64.zip\n"
    "Error: Download failed: server returned code 403 body "
    "'<?xml version=\\\"1.0\\\" encoding=\\\"UTF-8\\\"?><Error><Code>AccessDenied</Code>"
    "<Message>Access denied.</Message><Details>We are sorry, but this service is not "
    "available in your location</Details></Error>'.\n"
    "Failed to install browsers\n"
)


class WptBrowserInstallTests(unittest.TestCase):
    def _run(self, script: str, cwd: str | None = None) -> subprocess.CompletedProcess:
        """Run *script* with the library sourced, under run-wpt-tests.sh's shell flags."""
        return subprocess.run(
            ["bash", "-euo", "pipefail", "-c", f'source "{LIB_PATH}"\n{script}'],
            capture_output=True,
            text=True,
            cwd=cwd,
        )

    # --- exit-status propagation (the issue #1534 regression) ----------------

    def test_retry_helper_returns_the_failing_commands_exit_status(self) -> None:
        result = self._run(
            'STATUS=0\n'
            'wpt_run_with_retries_tail "install" 1 0 5 5 bash -c "exit 42" || STATUS=$?\n'
            'echo "status=$STATUS"'
        )
        self.assertIn("status=42", result.stdout)
        self.assertIn("(exit 42)", result.stderr)
        self.assertNotIn("(exit 0)", result.stderr)

    def test_exhausted_retries_abort_a_set_e_caller(self) -> None:
        """The failure must stop the script, not be swallowed into a success."""
        result = self._run(
            'wpt_run_with_retries_tail "install" 2 0 5 5 bash -c "exit 7"\n'
            'echo "REACHED-AFTER-FAILURE"'
        )
        self.assertEqual(result.returncode, 7)
        self.assertNotIn("REACHED-AFTER-FAILURE", result.stdout)
        self.assertIn("failed after 2 attempt(s)", result.stderr)

    def test_retry_helper_succeeds_after_a_transient_failure(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            counter = Path(tmp) / "attempts"
            counter.write_text("", encoding="utf-8")
            result = self._run(
                f'attempt() {{ echo x >> "{counter}"; '
                f'  [ "$(wc -l < "{counter}")" -ge 3 ] && {{ echo "install ok"; return 0; }}; '
                "  return 1; }\n"
                'wpt_run_with_retries_tail "install" 5 0 5 5 attempt\n'
                'echo "REACHED-AFTER-SUCCESS"'
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn("install ok", result.stdout)
            self.assertIn("REACHED-AFTER-SUCCESS", result.stdout)
            self.assertEqual(len(counter.read_text(encoding="utf-8").split()), 3)

    # --- non-retryable classification ---------------------------------------

    def test_geo_blocked_download_is_not_retried(self) -> None:
        """A 403 is deterministic for this runner's IP; five identical waits are waste."""
        with tempfile.TemporaryDirectory() as tmp:
            counter = Path(tmp) / "attempts"
            counter.write_text("", encoding="utf-8")
            result = self._run(
                f'attempt() {{ echo x >> "{counter}"; printf "%s" "{GEO_BLOCK_OUTPUT}"; return 1; }}\n'
                "STATUS=0\n"
                "WPT_RETRY_ABORT_CLASSIFIER=wpt_is_browser_download_blocked\n"
                'wpt_run_with_retries_tail "install" 5 30 5 40 attempt || STATUS=$?\n'
                'echo "status=$STATUS non_retryable=$WPT_RETRY_NON_RETRYABLE"'
            )
            self.assertIn("status=1", result.stdout)
            self.assertIn("non_retryable=true", result.stdout)
            self.assertEqual(len(counter.read_text(encoding="utf-8").split()), 1)
            self.assertIn("will not change on a retry", result.stderr)
            self.assertNotIn("Retrying", result.stderr)

    def test_unrelated_failure_still_uses_every_attempt(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            counter = Path(tmp) / "attempts"
            counter.write_text("", encoding="utf-8")
            result = self._run(
                f'attempt() {{ echo x >> "{counter}"; echo "ETIMEDOUT: connection reset"; return 1; }}\n'
                "STATUS=0\n"
                "WPT_RETRY_ABORT_CLASSIFIER=wpt_is_browser_download_blocked\n"
                'wpt_run_with_retries_tail "install" 3 0 5 5 attempt || STATUS=$?\n'
                'echo "status=$STATUS non_retryable=$WPT_RETRY_NON_RETRYABLE"'
            )
            self.assertIn("status=1", result.stdout)
            self.assertIn("non_retryable=false", result.stdout)
            self.assertEqual(len(counter.read_text(encoding="utf-8").split()), 3)

    def test_classifier_recognises_the_403_signatures(self) -> None:
        for label, line in (
            ("http status", "Error: Download failed: server returned code 403 body"),
            ("s3 error code", "<Error><Code>AccessDenied</Code></Error>"),
            ("geo message", "We're sorry, but this service is not available in your location"),
        ):
            with self.subTest(label), tempfile.TemporaryDirectory() as tmp:
                log = Path(tmp) / "install.log"
                log.write_text(line + "\n", encoding="utf-8")
                result = self._run(
                    f'if wpt_is_browser_download_blocked "{log}"; then echo BLOCKED; else echo OPEN; fi'
                )
                self.assertIn("BLOCKED", result.stdout)

    def test_classifier_ignores_unrelated_and_missing_logs(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            log = Path(tmp) / "install.log"
            log.write_text("npm ERR! network socket hang up\n", encoding="utf-8")
            result = self._run(
                f'if wpt_is_browser_download_blocked "{log}"; then echo BLOCKED; else echo OPEN; fi\n'
                f'if wpt_is_browser_download_blocked "{tmp}/absent.log"; then echo BLOCKED; else echo OPEN; fi'
            )
            self.assertEqual(result.stdout.split(), ["OPEN", "OPEN"])

    # --- failure marker ------------------------------------------------------

    def test_failure_marker_is_valid_json(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            marker = Path(tmp) / "nested" / "wpt-shard-failure.json"
            detail = 'Refused: "403" \\ AccessDenied.'
            result = self._run(
                f'wpt_write_failure_marker "{marker}" "$WPT_REASON_BROWSER_BLOCKED" {json.dumps(detail)}'
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            payload = json.loads(marker.read_text(encoding="utf-8"))
            self.assertEqual(payload["reason"], "BrowserDownloadBlocked")
            self.assertEqual(payload["detail"], detail)

    def test_browser_unavailable_exit_code_is_distinct_from_test_failure(self) -> None:
        """Exit 1 means "tests failed"; a shard that never ran a test must not use it."""
        result = self._run('echo "$WPT_EXIT_BROWSER_UNAVAILABLE"')
        self.assertEqual(result.stdout.strip(), "69")


if __name__ == "__main__":
    unittest.main()
