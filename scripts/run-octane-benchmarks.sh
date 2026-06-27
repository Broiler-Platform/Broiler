#!/usr/bin/env bash
# run-octane-benchmarks.sh — run Google's Octane 2.0 JavaScript benchmark suite
# under Chromium (V8 via Playwright) and Broiler (the BroilerJS shell), then
# write per-engine result files and a Chromium-vs-Broiler comparison report.
#
# Source suite: https://github.com/chromium/octane
#
# Usage:
#     ./scripts/run-octane-benchmarks.sh [OPTIONS]
#
# Options:
#     --octane-dir <dir>   Existing Octane checkout (default: clone into tests/octane/checkout)
#     --out-dir <dir>      Results directory (default: tests/octane/results)
#     --engines <list>     Comma-separated engines to run (default: chromium,broiler)
#     --timeout <sec>      Per-suite timeout in seconds (default: 180)
#     --octane-ref <ref>   Git ref of chromium/octane to check out (default: master)
#     --skip-build         Do not rebuild BroilerJS (reuse an existing Release build)
#     -h, --help           Show this help message
#
# Prerequisites:
#     - .NET 10 SDK (BroilerJS shell)
#     - Node.js + npm (Playwright Chromium driver)
#     - git (to clone the Octane suite)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

OCTANE_DIR=""
OUT_DIR="$REPO_ROOT/tests/octane/results"
ENGINES="chromium,broiler"
TIMEOUT=180
OCTANE_REF="master"
SKIP_BUILD=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --octane-dir) OCTANE_DIR="$2"; shift 2 ;;
        --out-dir) OUT_DIR="$2"; shift 2 ;;
        --engines) ENGINES="$2"; shift 2 ;;
        --timeout) TIMEOUT="$2"; shift 2 ;;
        --octane-ref) OCTANE_REF="$2"; shift 2 ;;
        --skip-build) SKIP_BUILD=true; shift ;;
        -h|--help)
            sed -n '2,30p' "$0" | sed 's/^# \{0,1\}//'
            exit 0 ;;
        *) echo "Unknown option: $1" >&2; exit 1 ;;
    esac
done

echo "=== Broiler Octane Benchmark Runner ==="
echo "Repository root : $REPO_ROOT"
echo "Output directory: $OUT_DIR"
echo "Engines         : $ENGINES"
echo "Per-suite timeout: ${TIMEOUT}s"
echo ""

# --- Step 1: Obtain an Octane checkout ---------------------------------------

if [[ -z "$OCTANE_DIR" ]]; then
    OCTANE_DIR="$REPO_ROOT/tests/octane/checkout"
    if [[ -f "$OCTANE_DIR/base.js" ]]; then
        echo "--- Step 1: Using existing Octane checkout at $OCTANE_DIR ---"
    else
        echo "--- Step 1: Cloning chromium/octane ($OCTANE_REF) ---"
        rm -rf "$OCTANE_DIR"
        git clone --depth 1 --branch "$OCTANE_REF" \
            https://github.com/chromium/octane.git "$OCTANE_DIR" 2>&1 | tail -3
        echo "  ✓ Octane cloned to: $OCTANE_DIR"
    fi
else
    echo "--- Step 1: Using provided Octane directory: $OCTANE_DIR ---"
    [[ -f "$OCTANE_DIR/base.js" ]] || { echo "  ✗ base.js not found in $OCTANE_DIR" >&2; exit 1; }
fi
echo ""

NODE_ARGS=(--octane-dir "$OCTANE_DIR" --out-dir "$OUT_DIR" --engines "$ENGINES" --timeout "$TIMEOUT")

# --- Step 2: Build the BroilerJS shell (if running the broiler engine) --------

if [[ ",$ENGINES," == *",broiler,"* ]]; then
    BROILER_PROJ="$REPO_ROOT/Broiler.JS/Broiler.JS/Broiler.JavaScript/Broiler.JavaScript.csproj"
    if [[ "$SKIP_BUILD" != "true" ]]; then
        echo "--- Step 2: Building BroilerJS shell (Release) ---"
        dotnet build "$BROILER_PROJ" --configuration Release --nologo --verbosity quiet 2>&1 | tail -3
        echo "  ✓ Build succeeded"
    else
        echo "--- Step 2: Skipping BroilerJS build (--skip-build) ---"
    fi
    BROILER_DLL="$(find "$REPO_ROOT/Broiler.JS/Broiler.JS/Broiler.JavaScript/bin/Release" -name BroilerJS.dll | head -1)"
    [[ -n "$BROILER_DLL" ]] || { echo "  ✗ BroilerJS.dll not found; build first." >&2; exit 1; }
    echo "  BroilerJS.dll: $BROILER_DLL"
    NODE_ARGS+=(--broiler-dll "$BROILER_DLL")
    echo ""
fi

# --- Step 3: Install the Playwright Chromium driver (if running chromium) -----

export NODE_PATH="$REPO_ROOT/tests/octane/node_modules"
if [[ ",$ENGINES," == *",chromium,"* ]]; then
    echo "--- Step 3: Installing Playwright Chromium ---"
    pushd "$REPO_ROOT/tests/octane" > /dev/null
    if [[ -f package-lock.json ]]; then
        npm ci 2>&1 | tail -5
    else
        npm install 2>&1 | tail -5
    fi
    npx playwright install --with-deps chromium 2>&1 | tail -5
    popd > /dev/null
    echo "  ✓ Chromium ready"
    echo ""
fi

# --- Step 4: Run the benchmarks ----------------------------------------------

echo "--- Step 4: Running Octane suites ---"
node "$SCRIPT_DIR/run-octane.mjs" "${NODE_ARGS[@]}"
echo ""

# --- Step 5: Summary ----------------------------------------------------------

echo "=== Octane Run Complete ==="
echo "Results : $OUT_DIR"
if [[ -f "$OUT_DIR/comparison.md" ]]; then
    echo ""
    cat "$OUT_DIR/comparison.md"
fi
