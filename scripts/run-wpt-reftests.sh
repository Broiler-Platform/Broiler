#!/usr/bin/env bash
# run-wpt-reftests.sh — Run the WPT *reftests* against Broiler, and only those.
#
# A WPT reftest carries its own answer: the test document declares
# `<link rel="match" href="…">` (its render must be identical to that reference)
# or `<link rel="mismatch" href="…">` (it must differ from it). Both sides are
# rendered by Broiler and compared to each other, so this run needs no Chromium,
# no Playwright, and no generated reference images — nothing outside the WPT
# checkout is consulted. Tests that declare no reference are not run at all.
#
# That is the whole difference from scripts/run-wpt-tests.sh, which this script
# is otherwise a copy of: its Step 2 (generate Chromium reference PNGs via
# Playwright) is gone, and the runner is invoked with --reftests-only instead of
# --reference-dir. Everything else — the checkout, sharding, the rerun manifest,
# the summary/analysis files, the exit code — behaves identically, so the two
# runs are comparable and the same merge tooling reads both.
#
# What a result means here is narrower than in the golden-image suite, and worth
# stating: a pass says Broiler renders the test the same way it renders WPT's own
# statement of the correct result. A shared bug that hits test and reference
# alike still passes. In exchange, nothing is attributed to Broiler that is
# really a Chromium-vs-Broiler font/scrollbar/AA difference, and a run costs no
# browser download.
#
# Usage:
#     ./scripts/run-wpt-reftests.sh [OPTIONS]
#
# Options:
#     --output-dir <dir>   Directory for results (default: tests/wpt-reftest-results)
#     --wpt-dir <dir>      Use an existing WPT checkout instead of cloning
#     --shallow            Shallow-clone only (depth 1, much faster; default)
#     --subset <patterns>  Semicolon-separated sub-path patterns with optional
#                          wildcards (e.g. "css/CSS2;css/css-*")
#     --shard-count <N>    Split the reftests into N deterministic shards
#     --shard-index <I>    Run only shard I (0-based), or -1 for all
#     --rerun-json <path>  Rerun only tests from a prior JSON report
#     --rerun-kind <kind>  Which prior results to rerun (failures|timeouts)
#     --non-js             Run only documents that do not depend on JavaScript
#     --failure-images <dir>  Save rendered/reference/diff PNGs for each failure
#     -h, --help           Show this help message
#
# Prerequisites:
#     - .NET 10 SDK
#     - git (for cloning WPT)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

OUTPUT_DIR="$REPO_ROOT/tests/wpt-reftest-results"
WPT_DIR=""
SHALLOW=true
SUBSET=""
SHARD_INDEX=-1
SHARD_COUNT=1
RERUN_JSON=""
RERUN_KIND=""
NON_JS=false
FAILURE_IMAGES_DIR=""

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
        --shard-index)
            SHARD_INDEX="$2"
            shift 2
            ;;
        --shard-count)
            SHARD_COUNT="$2"
            shift 2
            ;;
        --rerun-json)
            RERUN_JSON="$2"
            shift 2
            ;;
        --rerun-kind)
            RERUN_KIND="$2"
            shift 2
            ;;
        --non-js)
            NON_JS=true
            shift
            ;;
        --failure-images)
            FAILURE_IMAGES_DIR="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Runs only the WPT reftests (rel=match / rel=mismatch), rendering both"
            echo "the test and its reference with Broiler. No Chromium, no reference images."
            echo ""
            echo "Options:"
            echo "  --output-dir <dir>            Directory for results (default: tests/wpt-reftest-results)"
            echo "  --wpt-dir <dir>               Use an existing WPT checkout instead of cloning"
            echo "  --shallow                     Shallow-clone only (depth 1, faster; default)"
            echo "  --subset <patterns>           Semicolon-separated sub-path patterns with wildcards"
            echo "                                (e.g. \"css/CSS2;css/css-*\" or \"css/css-flexbox\")"
            echo "  --shard-count <N>             Split the reftests into N deterministic shards (default: 1)"
            echo "  --shard-index <I>             Run only shard I (0-based), or -1 for all (default: -1)"
            echo "  --rerun-json <path>           Rerun only tests from a prior JSON report (incremental)"
            echo "  --rerun-kind <failures|timeouts>  Which prior results to rerun (default: failures)"
            echo "  --non-js                      Run only non-JavaScript WPT documents"
            echo "  --failure-images <dir>        Save rendered/reference/diff PNGs for each failure"
            echo "  -h, --help                    Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1" >&2
            exit 1
            ;;
    esac
done

# Shard arguments. The reftest run has only one consumer of them — the C# runner
# — because there is no reference generator to keep in step, but the assignment
# function is the same FNV-1a(relative-path) % shard-count, so a shard index
# means the same slice of a given test set in both suites.
SHARD_ARGS=()
if [[ "$SHARD_INDEX" != "-1" || "$SHARD_COUNT" != "1" ]]; then
    SHARD_ARGS=(--shard-count "$SHARD_COUNT" --shard-index "$SHARD_INDEX")
fi

RERUN_ARGS=()
if [[ -n "$RERUN_JSON" ]]; then
    # Normalized to an absolute path before it is handed on, so it resolves
    # against the invocation directory rather than wherever the runner runs from.
    if [[ "$RERUN_JSON" != /* ]]; then
        RERUN_JSON="$PWD/$RERUN_JSON"
    fi
    if [[ ! -f "$RERUN_JSON" ]]; then
        echo "  ✗ Rerun manifest not found: $RERUN_JSON" >&2
        exit 1
    fi
    RERUN_ARGS=(--rerun-json "$RERUN_JSON")
    [[ -n "$RERUN_KIND" ]] && RERUN_ARGS+=(--rerun-kind "$RERUN_KIND")
fi

NON_JS_ARGS=()
if [[ "$NON_JS" == "true" ]]; then
    NON_JS_ARGS=(--non-js)
fi

FAILURE_IMAGE_ARGS=()
if [[ -n "$FAILURE_IMAGES_DIR" ]]; then
    FAILURE_IMAGE_ARGS=(--failure-images "$FAILURE_IMAGES_DIR")
fi

mkdir -p "$OUTPUT_DIR"

# Marker describing why this shard could not run at all, read by the workflow's
# "Collect shard result" step. Cleared up front so a stale marker from an earlier
# run in the same workspace cannot be attributed to this one. The reftest suite
# has no browser to fail to provision — the one thing that writes this marker in
# the golden-image suite — but the file is still cleared and still honoured, so
# the two suites' collect steps stay identical.
FAILURE_MARKER="$OUTPUT_DIR/wpt-shard-failure.json"
rm -f "$FAILURE_MARKER"

LOGFILE="$OUTPUT_DIR/wpt-results.log"

echo "=== Broiler WPT Reftest Runner (Broiler-only, no reference images) ==="
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

TEST_DIR="$WPT_DIR"
SUBSET_ARGS=()
if [[ -n "$SUBSET" ]]; then
    SUBSET_ARGS+=(--subset "$SUBSET")
    echo "  Subset patterns: $SUBSET"
fi
echo ""

# --- Step 2: Build Broiler.Wpt ----------------------------------------------
#
# There is deliberately no reference-generation step here. The golden-image
# suite's Step 2 downloads Chromium and screenshots every test; a reftest run
# gets its baseline from the checkout itself.

echo "--- Step 2: Building Broiler.Wpt ---"
dotnet build "$REPO_ROOT/src/Broiler.Wpt/Broiler.Wpt.csproj" \
    --configuration Release --nologo --verbosity quiet 2>&1
echo "  ✓ Build succeeded"
echo ""

# --- Step 3: Run the reftests -----------------------------------------------

echo "--- Step 3: Running WPT reftests ---"
echo "  Test directory: $TEST_DIR"
echo "  Writing results to: $LOGFILE"
echo ""

JSON_REPORT="$OUTPUT_DIR/wpt-results.json"
MARKDOWN_REPORT="$OUTPUT_DIR/wpt-triage-summary.md"

set +e
dotnet run --project "$REPO_ROOT/src/Broiler.Wpt" \
    --configuration Release --no-build -- \
    --wpt-dir "$TEST_DIR" --reftests-only \
    --json-output "$JSON_REPORT" --markdown-output "$MARKDOWN_REPORT" \
    "${FAILURE_IMAGE_ARGS[@]}" "${SUBSET_ARGS[@]}" \
    "${SHARD_ARGS[@]}" "${RERUN_ARGS[@]}" "${NON_JS_ARGS[@]}" 2>&1 | tee "$LOGFILE"
WPT_EXIT=$?
set -e

echo ""
echo "=== WPT Reftest Run Complete ==="
echo "Exit code : $WPT_EXIT"
echo "Log file  : $LOGFILE"
echo "JSON file : $JSON_REPORT"
echo "Markdown  : $MARKDOWN_REPORT"

# --- Step 4: Generate summary -----------------------------------------------

echo ""
echo "--- Summary ---"
if [[ -f "$LOGFILE" ]]; then
    PASSED="$(grep -c '^\[PASS\]' "$LOGFILE" || true)"
    FAILED="$(grep -c '^\[FAIL\]' "$LOGFILE" || true)"
    SKIPPED="$(grep -oP '(?<=, )\d+(?= skipped)' "$LOGFILE" || true)"
    PASSED="${PASSED:-0}"
    FAILED="${FAILED:-0}"
    SKIPPED="${SKIPPED:-0}"
    echo "  Passed : $PASSED"
    echo "  Failed : $FAILED"
    echo "  Skipped: $SKIPPED"

    SUMMARY="$OUTPUT_DIR/wpt-summary.txt"
    {
        echo "WPT Reftest Results Summary"
        echo "==========================="
        echo "Date     : $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
        echo "Mode     : reftests only (rel=match/rel=mismatch), both sides rendered by Broiler"
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
    if [[ -f "$MARKDOWN_REPORT" ]]; then
        echo "  Triage : $MARKDOWN_REPORT"
    fi

    if [[ "$FAILED" -gt 0 ]]; then
        ANALYSIS="$OUTPUT_DIR/wpt-root-cause-analysis.txt"
        {
            echo "WPT Reftest Root Cause Analysis"
            echo "==============================="
            echo "Date     : $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
            echo "Subset   : ${SUBSET:-"(all)"}"
            echo ""
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
