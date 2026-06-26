#!/usr/bin/env bash
# check-wpt-regression.sh — Compare a WPT run's pass count against a committed
# per-segment baseline and fail only on regression (a drop in passes or a rise
# in failures beyond tolerance). This implements the "baseline gate" of Phase 0:
# the nightly/weekly WPT matrix should alarm on regressions, NOT on the large
# absolute backlog of skipped/failing tests.
#
# Usage:
#   ./scripts/check-wpt-regression.sh \
#       --results tests/wpt-results/wpt-results.json \
#       --baseline tests/wpt-baseline/<segment>.json \
#       [--tolerance 0]
#
# Behaviour:
#   - Baseline missing  -> WARN and exit 0 (nothing to compare against yet).
#                          Commit the current results as the baseline to enable
#                          the gate for that segment.
#   - passed < baseline.passed - tolerance      -> FAIL (regression).
#   - failed > baseline.failed + tolerance      -> FAIL (new breakage).
#   - otherwise                                 -> PASS.
#
# Requires: python3 (preinstalled on GitHub-hosted ubuntu runners; the repo's
# other workflows also rely on it).

set -euo pipefail

# Pick a Python interpreter that actually runs (python3 on CI; python on some
# dev boxes — and skip the non-functional Windows Store 'python3' alias).
PY=""
for cand in python3 python; do
    if command -v "$cand" >/dev/null 2>&1 && "$cand" -c 'pass' >/dev/null 2>&1; then
        PY="$cand"
        break
    fi
done
if [[ -z "$PY" ]]; then
    echo "ERROR: python3 (or python) is required but was not found." >&2
    exit 2
fi

# Read a dotted numeric field (e.g. summary.passed) from a JSON file.
json_num() {
    "$PY" -c 'import json,sys
d=json.load(open(sys.argv[1]))
for k in sys.argv[2].split("."): d=d[k]
print(int(d))' "$1" "$2"
}

RESULTS=""
BASELINE=""
TOLERANCE=0

while [[ $# -gt 0 ]]; do
    case "$1" in
        --results)   RESULTS="$2";   shift 2 ;;
        --baseline)  BASELINE="$2";  shift 2 ;;
        --tolerance) TOLERANCE="$2"; shift 2 ;;
        -h|--help)
            grep '^#' "$0" | sed 's/^# \{0,1\}//'
            exit 0
            ;;
        *) echo "Unknown option: $1" >&2; exit 2 ;;
    esac
done

if [[ -z "$RESULTS" || -z "$BASELINE" ]]; then
    echo "ERROR: --results and --baseline are required." >&2
    exit 2
fi

if [[ ! -f "$RESULTS" ]]; then
    echo "ERROR: results file not found: $RESULTS" >&2
    exit 2
fi

cur_passed="$(json_num "$RESULTS" summary.passed)"
cur_failed="$(json_num "$RESULTS" summary.failed)"
cur_total="$(json_num "$RESULTS" summary.total)"

echo "Current : passed=$cur_passed failed=$cur_failed total=$cur_total"

if [[ ! -f "$BASELINE" ]]; then
    echo "::warning::No baseline at '$BASELINE' — skipping regression gate."
    echo "Commit the current results as the baseline to enable gating:"
    echo "  cp '$RESULTS' '$BASELINE'"
    exit 0
fi

base_passed="$(json_num "$BASELINE" summary.passed)"
base_failed="$(json_num "$BASELINE" summary.failed)"
echo "Baseline: passed=$base_passed failed=$base_failed"

status=0

if (( cur_passed < base_passed - TOLERANCE )); then
    echo "::error::Pass-count regression: $cur_passed < $base_passed (tolerance $TOLERANCE)."
    status=1
fi

if (( cur_failed > base_failed + TOLERANCE )); then
    echo "::error::Failure-count rose: $cur_failed > $base_failed (tolerance $TOLERANCE)."
    status=1
fi

if [[ "$status" == "0" ]]; then
    echo "OK: no regression versus baseline."
fi

exit "$status"
