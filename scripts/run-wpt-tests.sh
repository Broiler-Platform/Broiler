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
#     --subset <patterns>   Semicolon-separated list of sub-path patterns with
#                           optional wildcards (e.g. "css/CSS2;css/css-*").
#                           A single pattern like "css/css-flexbox" also works.
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
            echo "  --subset <patterns>  Semicolon-separated sub-path patterns with wildcards"
            echo "                       (e.g. \"css/CSS2;css/css-*\" or \"css/css-flexbox\")"
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

# If a subset is requested, expand patterns for reference generation but
# always pass the raw patterns to the C# program via --subset.
TEST_DIR="$WPT_DIR"
SUBSET_ARGS=()
if [[ -n "$SUBSET" ]]; then
    SUBSET_ARGS+=(--subset "$SUBSET")
    echo "  Subset patterns: $SUBSET"
fi
echo ""

# --- Step 2: Generate Chromium reference images (Playwright) -----------------

REFERENCE_DIR="$REPO_ROOT/tests/wpt/references"

echo "--- Step 2: Generating reference images with Chromium (Playwright) ---"

# Install Playwright dependencies if needed.
if command -v npx &>/dev/null; then
    pushd "$REPO_ROOT/tests/wpt" > /dev/null
    if [[ -f package-lock.json ]]; then
        npm ci 2>&1 | tail -10
    else
        echo "No package-lock.json found; using npm install instead of npm ci"
        npm install 2>&1 | tail -10
    fi
    npx playwright install --with-deps chromium 2>&1 | tail -10

    # Set NODE_PATH so require('playwright') resolves from the local
    # node_modules installed above (Node.js resolves modules relative to
    # the requiring file's directory, not cwd).
    #
    # When a subset is specified, expand semicolon-separated patterns
    # (which may contain wildcards) into individual directories and
    # generate references for each.
    REF_GEN_OK=true
    if [[ -n "$SUBSET" ]]; then
        IFS=';' read -ra PATTERNS <<< "$SUBSET"
        for PATTERN in "${PATTERNS[@]}"; do
            PATTERN="${PATTERN#"${PATTERN%%[![:space:]]*}"}"   # trim leading
            PATTERN="${PATTERN%"${PATTERN##*[![:space:]]}"}"   # trim trailing
            [[ -z "$PATTERN" ]] && continue
            MATCHED=false
            # Use bash glob expansion; nullglob prevents literal fallback.
            shopt -s nullglob
            for MATCH_DIR in $WPT_DIR/$PATTERN; do
                if [[ -d "$MATCH_DIR" ]]; then
                    MATCHED=true
                    echo "  Generating refs for: ${MATCH_DIR#"$WPT_DIR/"}"
                    if ! NODE_PATH="$REPO_ROOT/tests/wpt/node_modules" \
                        node "$SCRIPT_DIR/generate-wpt-references.js" \
                        "$MATCH_DIR" "$REFERENCE_DIR" --concurrency 8 --base-dir "$WPT_DIR" 2>&1; then
                        echo "  ✗ Reference generation failed for $MATCH_DIR" >&2
                        REF_GEN_OK=false
                    fi
                fi
            done
            shopt -u nullglob
            if [[ "$MATCHED" == "false" ]]; then
                echo "  ⚠ No directories matched pattern: $PATTERN"
            fi
        done
    else
        if ! NODE_PATH="$REPO_ROOT/tests/wpt/node_modules" \
            node "$SCRIPT_DIR/generate-wpt-references.js" \
            "$TEST_DIR" "$REFERENCE_DIR" --concurrency 8 2>&1; then
            REF_GEN_OK=false
        fi
    fi

    if [[ "$REF_GEN_OK" == "true" ]]; then
        echo "  ✓ Reference images generated"
    else
        echo "  ✗ Reference generation failed" >&2
        exit 1
    fi
    popd > /dev/null
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

JSON_REPORT="$OUTPUT_DIR/wpt-results.json"
REF_ARGS+=(--json-output "$JSON_REPORT")

set +e
dotnet run --project "$REPO_ROOT/src/Broiler.Wpt" \
    --configuration Release --no-build -- \
    --wpt-dir "$TEST_DIR" "${REF_ARGS[@]}" "${SUBSET_ARGS[@]}" 2>&1 | tee "$LOGFILE"
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

    # Write a root-cause analysis report when there are failures.
    if [[ "$FAILED" -gt 0 ]]; then
        ANALYSIS="$OUTPUT_DIR/wpt-root-cause-analysis.txt"
        {
            echo "WPT Root Cause Analysis"
            echo "======================="
            echo "Date     : $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
            echo "Subset   : ${SUBSET:-"(all)"}"
            echo ""
            # Count failures per category from [FAIL] [Category] tags.
            # NOTE: This list must stay in sync with the FailureCategory
            # enum in src/Broiler.Wpt/WptTestRunner.cs.
            for CAT in PixelMismatch ScriptError RenderingError FileIO ReferenceDecodeError Unknown; do
                COUNT="$(grep -c "^\[FAIL\] \[$CAT\]" "$LOGFILE" 2>/dev/null || true)"
                COUNT="${COUNT:-0}"
                if [[ "$COUNT" -gt 0 ]]; then
                    echo "$CAT: $COUNT failure(s)"
                    grep "^\[FAIL\] \[$CAT\]" "$LOGFILE" | sed "s/^\[FAIL\] \[$CAT\] /  /" || true
                    echo ""
                fi
            done

            # Count PixelMismatch sub-categories from [FAIL] [PixelMismatch] [SubCat] tags.
            # NOTE: This list must stay in sync with the MismatchCategory
            # enum in Broiler.HTML/Source/Broiler.HTML.Image/MismatchClassifier.cs.
            echo "PixelMismatch Sub-Categories:"
            for SUBCAT in SizeMismatch SubpixelAntiAliasing ColorShift LayoutShift MissingContent MinorDiff; do
                SCOUNT="$(grep -c "^\[FAIL\] \[PixelMismatch\] \[$SUBCAT\]" "$LOGFILE" 2>/dev/null || true)"
                SCOUNT="${SCOUNT:-0}"
                if [[ "$SCOUNT" -gt 0 ]]; then
                    echo "  $SUBCAT: $SCOUNT failure(s)"
                fi
            done
            echo ""

            # Include the full Root Cause Analysis section if present.
            if grep -q "^=== Root Cause Analysis ===$" "$LOGFILE"; then
                echo "--- Detailed Root Cause Breakdown ---"
                sed -n '/^=== Root Cause Analysis ===/,$ p' "$LOGFILE"
            fi
        } > "$ANALYSIS"
        echo "  Analysis: $ANALYSIS"
    fi
fi

echo ""
echo "=== Done ==="
exit $WPT_EXIT
