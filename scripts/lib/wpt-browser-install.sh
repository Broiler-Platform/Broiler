#!/usr/bin/env bash
# wpt-browser-install.sh — provisioning Chromium for WPT reference generation.
#
# Sourced by run-wpt-tests.sh. It lives in its own file so the retry and
# failure-classification logic can be exercised directly by
# scripts/tests/test_wpt_browser_install.py instead of only via a full WPT run —
# the bug this file was extracted to fix (a retry helper that reported success
# after every attempt failed) is exactly the kind a unit test catches.

# sysexits.h EX_UNAVAILABLE. Distinct from the runner's own exit 1 ("some tests
# failed") so a shard that never got to run a single test is not mistaken for a
# shard that ran and found failures.
WPT_EXIT_BROWSER_UNAVAILABLE=69

# Reasons recorded in the failure marker. They travel through the shard status
# JSON into merge-wpt-shards.py, which renders them in the generated issue.
WPT_REASON_BROWSER_BLOCKED="BrowserDownloadBlocked"
WPT_REASON_BROWSER_INSTALL_FAILED="BrowserInstallFailed"

# Set by wpt_run_with_retries_tail: "true" when the abort classifier judged the
# failure deterministic and the remaining attempts were skipped.
WPT_RETRY_NON_RETRYABLE=false

# True when a Playwright browser download failed for a reason that will repeat on
# every attempt from this runner: the CDN refused the request outright with HTTP
# 403 / AccessDenied, which is how its geo-restriction ("We're sorry, but this
# service is not available in your location") surfaces. A retry keeps the same
# runner IP, so the answer cannot change within a job — issue #1534 burned ~4.5
# minutes on five identical 403s before giving up.
wpt_is_browser_download_blocked() {
    local log_file="$1"
    [[ -f "$log_file" ]] || return 1
    grep -qiE \
        'server returned code 403|<Code>AccessDenied</Code>|not available in your location' \
        "$log_file"
}

# Run "$@" with retries, echoing a tail of its captured output.
#
# Args: <description> <max_attempts> <delay_seconds> <success_tail_lines>
#       <failure_tail_lines> <command...>
#
# The delay doubles after each failed attempt. When WPT_RETRY_ABORT_CLASSIFIER
# names a shell function, it is called with the captured log after a failed
# attempt; a zero return means retrying cannot help and the remaining attempts
# are skipped. Returns the failing command's own exit status.
wpt_run_with_retries_tail() {
    local description="$1"
    local max_attempts="$2"
    local delay_seconds="$3"
    local success_tail_lines="$4"
    local failure_tail_lines="$5"
    shift 5

    local attempt=1
    local status=0
    local log_file
    log_file="$(mktemp)"

    WPT_RETRY_NON_RETRYABLE=false

    while (( attempt <= max_attempts )); do
        # Capture the command's own status. Reading $? after an `if cmd; then
        # ... fi` instead yields the *if compound's* status, which is 0 when the
        # condition failed and there is no else branch — that read the failure as
        # a success and let the caller march on (issue #1534).
        status=0
        "$@" >"$log_file" 2>&1 || status=$?

        if (( status == 0 )); then
            tail -n "$success_tail_lines" "$log_file" || true
            rm -f "$log_file"
            return 0
        fi

        echo "  $description failed on attempt $attempt/$max_attempts (exit $status)." >&2
        tail -n "$failure_tail_lines" "$log_file" >&2 || true

        if [[ -n "${WPT_RETRY_ABORT_CLASSIFIER:-}" ]] \
            && "$WPT_RETRY_ABORT_CLASSIFIER" "$log_file"; then
            WPT_RETRY_NON_RETRYABLE=true
            echo "  $description failed for a reason that will not change on a retry;" \
                 "skipping the remaining attempt(s)." >&2
            rm -f "$log_file"
            return "$status"
        fi

        if (( attempt == max_attempts )); then
            echo "  $description failed after $max_attempts attempt(s)." >&2
            rm -f "$log_file"
            return "$status"
        fi

        echo "  Retrying $description in ${delay_seconds}s..." >&2
        sleep "$delay_seconds"
        attempt=$((attempt + 1))
        delay_seconds=$((delay_seconds * 2))
    done
}

# Record why a shard could not run, for the workflow's "Collect shard result"
# step to fold into the shard status JSON. Without it the merge step sees only a
# bare non-zero exit and has to guess at the cause — in issue #1534 it reported a
# geo-blocked Chromium download as a probable CI-runner eviction.
#
# Args: <marker_path> <reason> <detail>
wpt_write_failure_marker() {
    local marker_path="$1"
    local reason="$2"
    local detail="$3"

    local marker_dir
    marker_dir="$(dirname "$marker_path")"
    mkdir -p "$marker_dir"

    # Escape the two characters that can appear in a one-line JSON string value.
    local escaped_detail
    escaped_detail="$(printf '%s' "$detail" | sed 's/\\/\\\\/g; s/"/\\"/g')"

    printf '{"reason":"%s","detail":"%s"}\n' "$reason" "$escaped_detail" > "$marker_path"
}
