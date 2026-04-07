#!/usr/bin/env bash
# run-wpt-tests.sh — Clone the Web Platform Tests repository and run
# Broiler.Wpt against the checkout, producing a log file of results.
#
# Usage:
#     ./scripts/run-wpt-tests.sh [OPTIONS]
#
# Options:
#     --output-dir <dir>   Directory for results (default: tests/wpt/results)
#     --wpt-dir <dir>      Use an existing WPT checkout instead of cloning
#     --shallow             Shallow-clone only (depth 1, much faster)
#     --subset <path>       Run tests only under this sub-path of WPT
#                           (e.g. "css/css-flexbox" or "html/semantics")
#     -h, --help           Show this help message
#
# Prerequisites:
#     - .NET 8 SDK
#     - git (for cloning WPT)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

OUTPUT_DIR="$REPO_ROOT/tests/wpt/results"
WPT_DIR=""
SHALLOW=true
SUBSET=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        --output-dir)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --wpt-dir)
            WPT_DIR="$2"
            shift 2
            ;;
        --shallow)
            SHALLOW=true
            shift
            ;;
        --subset)
            SUBSET="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --output-dir <dir>   Directory for results (default: tests/wpt/results)"
            echo "  --wpt-dir <dir>      Use an existing WPT checkout instead of cloning"
            echo "  --shallow            Shallow-clone only (depth 1, faster; default)"
            echo "  --subset <path>      Run tests only under this sub-path of WPT"
            echo "                       (e.g. \"css/css-flexbox\" or \"html/semantics\")"
            echo "  -h, --help           Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1" >&2
            exit 1
            ;;
    esac
done

mkdir -p "$OUTPUT_DIR"

LOGFILE="$OUTPUT_DIR/wpt-results.log"

echo "=== Broiler WPT Test Runner ==="
echo "Repository root : $REPO_ROOT"
echo "Output directory: $OUTPUT_DIR"
echo "Log file        : $LOGFILE"
echo ""

# --- Step 1: Obtain a WPT checkout ------------------------------------------

if [[ -z "$WPT_DIR" ]]; then
    WPT_DIR="$REPO_ROOT/tests/wpt/checkout"
    if [[ -d "$WPT_DIR/.git" ]]; then
        echo "--- Step 1: Using existing WPT checkout at $WPT_DIR ---"
    else
        echo "--- Step 1: Cloning web-platform-tests ---"
        CLONE_ARGS=(clone --single-branch --branch master)
        if [[ "$SHALLOW" == "true" ]]; then
            CLONE_ARGS+=(--depth 1)
            echo "  (shallow clone, depth=1)"
        fi
        git "${CLONE_ARGS[@]}" \
            https://github.com/web-platform-tests/wpt.git \
            "$WPT_DIR" 2>&1 | tail -5
        echo "  ✓ WPT cloned to: $WPT_DIR"
    fi
else
    echo "--- Step 1: Using provided WPT directory: $WPT_DIR ---"
    if [[ ! -d "$WPT_DIR" ]]; then
        echo "  ✗ WPT directory not found: $WPT_DIR" >&2
        exit 1
    fi
fi

# If a subset is requested, adjust the test directory.
TEST_DIR="$WPT_DIR"
if [[ -n "$SUBSET" ]]; then
    TEST_DIR="$WPT_DIR/$SUBSET"
    if [[ ! -d "$TEST_DIR" ]]; then
        echo "  ✗ Subset directory not found: $TEST_DIR" >&2
        exit 1
    fi
    echo "  Running subset: $SUBSET"
fi
echo ""

# --- Step 2: Generate Chromium reference images (Playwright) -----------------

REFERENCE_DIR="$REPO_ROOT/tests/wpt/references"

echo "--- Step 2: Generating reference images with Chromium (Playwright) ---"

# Install Playwright dependencies if needed.
if command -v npx &>/dev/null; then
    pushd "$REPO_ROOT/tests/wpt" > /dev/null
    npm ci --ignore-scripts 2>&1 | tail -3
    npx playwright install --with-deps chromium 2>&1 | tail -5
    popd > /dev/null

    node "$SCRIPT_DIR/generate-wpt-references.js" \
        "$TEST_DIR" "$REFERENCE_DIR" --concurrency 8 2>&1
    echo "  ✓ Reference images generated"
else
    echo "  ⚠ Node.js/npx not found — skipping reference generation"
    echo "    Tests without references will be reported as skipped."
fi
echo ""

# --- Step 3: Build Broiler.Wpt ----------------------------------------------

echo "--- Step 3: Building Broiler.Wpt ---"
dotnet build "$REPO_ROOT/src/Broiler.Wpt/Broiler.Wpt.csproj" \
    --configuration Release --nologo --verbosity quiet 2>&1
echo "  ✓ Build succeeded"
echo ""

# --- Step 4: Run the WPT test suite -----------------------------------------

echo "--- Step 4: Running WPT tests ---"
echo "  Test directory: $TEST_DIR"
echo "  Writing results to: $LOGFILE"
echo ""

# Run Broiler.Wpt and tee output to both console and log file.
# The tool returns exit code 1 if any tests fail, which is expected.
# Build the reference-dir argument — only pass it if the directory exists.
REF_ARGS=()
if [[ -d "$REFERENCE_DIR" ]]; then
    REF_ARGS+=(--reference-dir "$REFERENCE_DIR")
fi

set +e
dotnet run --project "$REPO_ROOT/src/Broiler.Wpt" \
    --configuration Release --no-build -- \
    --wpt-dir "$TEST_DIR" "${REF_ARGS[@]}" 2>&1 | tee "$LOGFILE"
WPT_EXIT=$?
set -e

echo ""
echo "=== WPT Test Run Complete ==="
echo "Exit code : $WPT_EXIT"
echo "Log file  : $LOGFILE"

# --- Step 5: Generate summary -----------------------------------------------

echo ""
echo "--- Summary ---"
if [[ -f "$LOGFILE" ]]; then
    PASSED="$(grep -c '^\[PASS\]' "$LOGFILE" || true)"
    FAILED="$(grep -c '^\[FAIL\]' "$LOGFILE" || true)"
    # Extract skipped count from the Results line
    SKIPPED="$(grep -oP '(?<=, )\d+(?= skipped)' "$LOGFILE" || true)"
    PASSED="${PASSED:-0}"
    FAILED="${FAILED:-0}"
    SKIPPED="${SKIPPED:-0}"
    echo "  Passed : $PASSED"
    echo "  Failed : $FAILED"
    echo "  Skipped: $SKIPPED"

    # Write a machine-readable summary file
    SUMMARY="$OUTPUT_DIR/wpt-summary.txt"
    {
        echo "WPT Test Results Summary"
        echo "========================"
        echo "Date     : $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
        echo "Subset   : ${SUBSET:-"(all)"}"
        echo "Passed   : $PASSED"
        echo "Failed   : $FAILED"
        echo "Skipped  : $SKIPPED"
        echo ""
        if [[ "$FAILED" -gt 0 ]]; then
            echo "Failed tests:"
            grep '^\[FAIL\]' "$LOGFILE" | sed 's/^\[FAIL\] /  /' || true
        fi
    } > "$SUMMARY"
    echo "  Summary: $SUMMARY"
fi

echo ""
echo "=== Done ==="
exit $WPT_EXIT
