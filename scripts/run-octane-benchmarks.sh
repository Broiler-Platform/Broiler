#!/usr/bin/env bash
# run-octane-benchmarks.sh — run Google's Octane 2.0 JavaScript benchmark suite
# under Chromium (V8 via Playwright), Broiler (the BroilerJS shell) and Jint (a
# managed ECMAScript interpreter), then write per-engine result files and a
# comparison report.
#
# Chromium says how far Broiler is from V8; Jint says how it compares to another
# managed engine on the same runtime, which is the closer reference point.
#
# Source suite: https://github.com/chromium/octane
#
# Usage:
#     ./scripts/run-octane-benchmarks.sh [OPTIONS]
#
# Options:
#     --platform <rid>     Runtime identifier the run is labelled with, e.g.
#                          win-x64, linux-x64, linux-arm64 (default: detected
#                          from uname). Results are written per platform —
#                          a score only means something next to the machine
#                          that produced it, and the release matrix in
#                          docs/performance/protocol.md is all three.
#     --octane-dir <dir>   Existing Octane checkout (default: clone into tests/octane/checkout)
#     --out-dir <dir>      Results directory (default: tests/octane/results/<platform>)
#     --log-dir <dir>      Per-suite diagnostic logs (default: tests/octane/logs)
#     --engines <list>     Comma-separated engines to run: chromium, broiler, jint
#                          (default: chromium,broiler,jint)
#     --timeout <sec>      Per-suite timeout floor in seconds (default: 180). A
#                          slow suite may raise its own budget in
#                          scripts/octane-suites.json.
#     --repetitions <n>    Run each suite n times, report the median score and
#                          the observed spread (default: 1). A single run cannot
#                          tell a real change from run-to-run noise.
#     --noise-band <pct>   Spread above which a benchmark is flagged as noisy
#                          (default: 7.5)
#     --octane-ref <ref>   Git ref of chromium/octane to check out (default: master)
#     --skip-build         Do not rebuild the engine shells (reuse existing Release builds)
#     --only <list>        Comma-separated suites to run, e.g. --only Crypto,zlib.
#                          Writes to <log-dir>/partial/ so a debugging run never
#                          overwrites the committed full-run results.
#     --verbose            Stream each engine process's output live
#     --keep-scripts       Keep the combined script for every suite, not just failures
#     --no-trace           Disable the runner's breadcrumbs (undisturbed timing run)
#     --broiler-env K=V    Extra env var for the Broiler process (repeatable),
#                          e.g. --broiler-env BROILER_GENERATE_IL_LOGS=1
#     -h, --help           Show this help message
#
# Diagnosing a failing suite:
#     ./scripts/run-octane-benchmarks.sh --engines broiler --skip-build \
#         --only Crypto --verbose
# writes tests/octane/logs/partial/broiler/Crypto.log (full output, exit code,
# repro command) and a diagnostics.md summarizing where it failed.
#
# Prerequisites:
#     - .NET 10 SDK (the BroilerJS and Jint shells)
#     - Node.js + npm (Playwright Chromium driver)
#     - git (to clone the Octane suite)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# The .NET runtime identifier this machine is. Used to label the run and to keep
# each platform's results in their own directory: the harness folds a previously
# committed engine result back into the comparison, which is only honest when
# every column came off the same machine.
detect_platform() {
    local os arch
    case "$(uname -s)" in
        Linux)                        os=linux ;;
        Darwin)                       os=osx ;;
        MINGW*|MSYS*|CYGWIN*|Windows_NT) os=win ;;
        *)                            os="$(uname -s | tr '[:upper:]' '[:lower:]')" ;;
    esac
    case "$(uname -m)" in
        x86_64|amd64)  arch=x64 ;;
        aarch64|arm64) arch=arm64 ;;
        *)             arch="$(uname -m)" ;;
    esac
    printf '%s-%s\n' "$os" "$arch"
}

# Node is a native Windows binary under Git Bash, where an absolute path is an
# MSYS one (/d/a/...) that Node would resolve against the current drive — every
# result file would land somewhere nobody looks. Hand it a real Windows path
# instead; `cygpath -m` keeps forward slashes, which Node and bash both accept.
# A no-op everywhere cygpath does not exist, which is everywhere but Windows.
to_native() {
    if command -v cygpath > /dev/null 2>&1; then
        cygpath -m "$1"
    else
        printf '%s\n' "$1"
    fi
}

PLATFORM=""
OCTANE_DIR=""
OUT_DIR=""
LOG_DIR=""
ENGINES="chromium,broiler,jint"
TIMEOUT=180
OCTANE_REF="master"
SKIP_BUILD=false
ONLY=""
EXTRA_ARGS=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --platform) PLATFORM="$2"; shift 2 ;;
        --octane-dir) OCTANE_DIR="$2"; shift 2 ;;
        --out-dir) OUT_DIR="$2"; shift 2 ;;
        --log-dir) LOG_DIR="$2"; shift 2 ;;
        --engines) ENGINES="$2"; shift 2 ;;
        --timeout) TIMEOUT="$2"; shift 2 ;;
        --repetitions) EXTRA_ARGS+=(--repetitions "$2"); shift 2 ;;
        --noise-band) EXTRA_ARGS+=(--noise-band "$2"); shift 2 ;;
        --octane-ref) OCTANE_REF="$2"; shift 2 ;;
        --skip-build) SKIP_BUILD=true; shift ;;
        --only) ONLY="$2"; EXTRA_ARGS+=(--only "$2"); shift 2 ;;
        --verbose) EXTRA_ARGS+=(--verbose); shift ;;
        --keep-scripts) EXTRA_ARGS+=(--keep-scripts); shift ;;
        --no-trace) EXTRA_ARGS+=(--no-trace); shift ;;
        --broiler-env) EXTRA_ARGS+=(--broiler-env "$2"); shift 2 ;;
        -h|--help)
            sed -n '2,45p' "$0" | sed 's/^# \{0,1\}//'
            exit 0 ;;
        *) echo "Unknown option: $1" >&2; exit 1 ;;
    esac
done

[[ -n "$PLATFORM" ]] || PLATFORM="$(detect_platform)"
[[ -n "$OUT_DIR" ]] || OUT_DIR="$REPO_ROOT/tests/octane/results/$PLATFORM"
[[ -n "$LOG_DIR" ]] || LOG_DIR="$REPO_ROOT/tests/octane/logs"

echo "=== Broiler Octane Benchmark Runner ==="
echo "Repository root : $REPO_ROOT"
echo "Platform        : $PLATFORM"
echo "Output directory: $OUT_DIR"
echo "Log directory   : $LOG_DIR"
echo "Engines         : $ENGINES"
echo "Per-suite timeout: ${TIMEOUT}s"
[[ -n "$ONLY" ]] && echo "Suites          : $ONLY (partial run — committed results left alone)"
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

NODE_ARGS=(--octane-dir "$(to_native "$OCTANE_DIR")" \
           --out-dir "$(to_native "$OUT_DIR")" \
           --log-dir "$(to_native "$LOG_DIR")" \
           --platform "$PLATFORM" \
           --engines "$ENGINES" --timeout "$TIMEOUT" "${EXTRA_ARGS[@]+"${EXTRA_ARGS[@]}"}")

# --- Step 2: Build the shell hosts for whichever engines are running ----------

if [[ ",$ENGINES," == *",broiler,"* ]]; then
    BROILER_PROJ="$REPO_ROOT/Broiler.JS/Broiler.JS/Broiler.JavaScript/Broiler.JavaScript.csproj"
    if [[ "$SKIP_BUILD" != "true" ]]; then
        echo "--- Step 2a: Building BroilerJS shell (Release) ---"
        dotnet build "$(to_native "$BROILER_PROJ")" --configuration Release --nologo --verbosity quiet 2>&1 | tail -3
        echo "  ✓ Build succeeded"
    else
        echo "--- Step 2a: Skipping BroilerJS build (--skip-build) ---"
    fi
    BROILER_DLL="$(find "$REPO_ROOT/Broiler.JS/Broiler.JS/Broiler.JavaScript/bin/Release" -name BroilerJS.dll | head -1)"
    [[ -n "$BROILER_DLL" ]] || { echo "  ✗ BroilerJS.dll not found; build first." >&2; exit 1; }
    echo "  BroilerJS.dll: $BROILER_DLL"
    NODE_ARGS+=(--broiler-dll "$(to_native "$BROILER_DLL")")
    echo ""
fi

# The Jint shell is a small console app over the Jint NuGet package; building it
# needs a package restore, which is the one part of this step that needs network.
if [[ ",$ENGINES," == *",jint,"* ]]; then
    JINT_PROJ="$REPO_ROOT/tests/octane/jint-host/Broiler.Octane.JintHost.csproj"
    if [[ "$SKIP_BUILD" != "true" ]]; then
        echo "--- Step 2b: Building the Jint shell (Release) ---"
        dotnet build "$(to_native "$JINT_PROJ")" --configuration Release --nologo --verbosity quiet 2>&1 | tail -3
        echo "  ✓ Build succeeded"
    else
        echo "--- Step 2b: Skipping Jint shell build (--skip-build) ---"
    fi
    JINT_DLL="$(find "$REPO_ROOT/tests/octane/jint-host/bin/Release" -name Broiler.Octane.JintHost.dll | head -1)"
    [[ -n "$JINT_DLL" ]] || { echo "  ✗ Broiler.Octane.JintHost.dll not found; build first." >&2; exit 1; }
    echo "  Jint shell: $JINT_DLL"
    NODE_ARGS+=(--jint-dll "$(to_native "$JINT_DLL")")
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
    # --with-deps installs the system libraries Chromium needs, which Playwright
    # only knows how to do on Debian/Ubuntu; on Windows it is an error, not a
    # no-op, so ask for the browser alone there.
    if [[ "$PLATFORM" == linux-* ]]; then
        npx playwright install --with-deps chromium 2>&1 | tail -5
    else
        npx playwright install chromium 2>&1 | tail -5
    fi
    popd > /dev/null
    echo "  ✓ Chromium ready"
    echo ""
fi

# --- Step 4: Check the harness's own diagnostic parsing -----------------------
# Cheap, engine-free, and worth doing before a long run: if the stack-trace and
# line-mapping parsers have drifted, every failing suite still reports "crash",
# just without saying where — a silent loss that is easy to miss.

echo "--- Step 4: Harness self-test ---"
node "$(to_native "$REPO_ROOT/tests/octane/harness-selftest.mjs")"
echo ""

# --- Step 5: Run the benchmarks ----------------------------------------------

echo "--- Step 5: Running Octane suites ---"
node "$(to_native "$SCRIPT_DIR/run-octane.mjs")" "${NODE_ARGS[@]}"
echo ""

# --- Step 6: Summary ----------------------------------------------------------

echo "=== Octane Run Complete ==="
if [[ -n "$ONLY" ]]; then
    echo "Results : $LOG_DIR/partial (partial run)"
else
    echo "Results : $OUT_DIR"
fi
echo "Logs    : $LOG_DIR (per-suite output, exit codes, repro commands)"
if [[ -f "$OUT_DIR/comparison.md" ]]; then
    echo ""
    cat "$OUT_DIR/comparison.md"
fi
