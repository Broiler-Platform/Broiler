#!/usr/bin/env bash
# google-compare.sh — Automated Google Search rendering, reference capture, and
# pixel comparison pipeline.
#
# Usage:
#     ./scripts/google-compare.sh [--skip-reference] [--output-dir <dir>]
#
# Prerequisites:
#     - .NET 8 SDK (for Broiler.CLI)
#     - Python 3 with Pillow and numpy (for pixel comparison)
#     - Optional: Node.js + Playwright (for Chromium reference rendering)
#
# Steps:
#     1. Render https://www.google.com using Broiler.CLI → google-broiler.png
#     2. (Optional) Render using Chromium/Playwright → google-reference.png
#     3. Compare pixel-by-pixel → google-diff.png + google-report.txt

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_DIR="$REPO_ROOT/docs/google-compliance"
SKIP_REFERENCE=false
URL="https://www.google.com"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        --skip-reference)
            SKIP_REFERENCE=true
            shift
            ;;
        --output-dir)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --url)
            URL="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [--skip-reference] [--output-dir <dir>] [--url <url>]"
            echo ""
            echo "Options:"
            echo "  --skip-reference  Skip Chromium reference rendering (use existing reference)"
            echo "  --output-dir      Output directory (default: docs/google-compliance/)"
            echo "  --url             URL to render (default: https://www.google.com)"
            echo "  -h, --help        Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1" >&2
            exit 1
            ;;
    esac
done

mkdir -p "$OUTPUT_DIR"

echo "=== Google Search Compliance Pixel Test Pipeline ==="
echo "Repository root: $REPO_ROOT"
echo "Output directory: $OUTPUT_DIR"
echo "URL: $URL"
echo ""

# --- Step 1: Render with Broiler CLI -----------------------------------------

echo "--- Step 1: Rendering Google Search with Broiler CLI ---"

BROILER_OUTPUT="$OUTPUT_DIR/google-broiler.png"
JS_LOG="$OUTPUT_DIR/google-js-errors.log"

# Capture both the image and the JS error log (stderr)
dotnet run --project "$REPO_ROOT/src/Broiler.Cli" -- \
    --capture-image "$URL" \
    --output "$BROILER_OUTPUT" \
    --width 1024 --height 768 \
    2> "$JS_LOG" || true

if [[ -f "$BROILER_OUTPUT" ]]; then
    echo "  ✓ Broiler render saved to: $BROILER_OUTPUT"
    echo "  Image info: $(file "$BROILER_OUTPUT")"
else
    echo "  ✗ Broiler render FAILED — no output file" >&2
    echo "  See JS error log: $JS_LOG"
    exit 1
fi

if [[ -s "$JS_LOG" ]]; then
    JS_ERROR_COUNT=$(grep -c "\[JSUndefined\]\|JSException\|Script execution error" "$JS_LOG" 2>/dev/null || echo "0")
    echo "  ⚠ JavaScript errors detected: $JS_ERROR_COUNT"
    echo "  Error log: $JS_LOG"
else
    echo "  ✓ No JavaScript errors detected"
fi
echo ""

# --- Step 2: Render with Chromium (optional) ---------------------------------

REFERENCE_OUTPUT="$OUTPUT_DIR/google-reference.png"

if [[ "$SKIP_REFERENCE" == "true" ]]; then
    echo "--- Step 2: Skipping Chromium reference rendering ---"
    if [[ -f "$REFERENCE_OUTPUT" ]]; then
        echo "  Using existing reference: $REFERENCE_OUTPUT"
    else
        echo "  ✗ No existing reference found at $REFERENCE_OUTPUT" >&2
        echo "  Run without --skip-reference to generate one, or provide a reference image." >&2
        exit 1
    fi
else
    echo "--- Step 2: Rendering Google Search with Chromium (Playwright) ---"

    if command -v npx &>/dev/null; then
        PLAYWRIGHT_SCRIPT=$(mktemp "${TMPDIR:-/tmp}/google-playwright-XXXXXX.js")
        cat > "$PLAYWRIGHT_SCRIPT" << 'JSEOF'
const { chromium } = require('playwright');

(async () => {
    const browser = await chromium.launch({ headless: true });
    const page = await browser.newPage({ viewport: { width: 1024, height: 768 } });
    const outputPath = process.argv[2];
    const url = process.argv[3] || 'https://www.google.com';

    await page.goto(url, { waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);
    await page.screenshot({ path: outputPath, fullPage: false });
    await browser.close();
    console.log('Reference render saved to: ' + outputPath);
})();
JSEOF
        npx playwright install chromium 2>&1 || echo "  ⚠ Playwright Chromium installation had warnings (non-fatal)"
        node "$PLAYWRIGHT_SCRIPT" "$REFERENCE_OUTPUT" "$URL" 2>/dev/null && {
            echo "  ✓ Chromium reference saved to: $REFERENCE_OUTPUT"
            rm -f "$PLAYWRIGHT_SCRIPT"
        } || {
            echo "  ⚠ Playwright rendering failed — using existing reference if available"
            rm -f "$PLAYWRIGHT_SCRIPT"
            if [[ ! -f "$REFERENCE_OUTPUT" ]]; then
                echo "  ✗ No reference image available" >&2
                exit 1
            fi
        }
    else
        echo "  ⚠ Node.js/npx not found — skipping Chromium rendering"
        if [[ -f "$REFERENCE_OUTPUT" ]]; then
            echo "  Using existing reference: $REFERENCE_OUTPUT"
        else
            echo "  ✗ No reference image available" >&2
            exit 1
        fi
    fi
fi
echo ""

# --- Step 3: Pixel comparison ------------------------------------------------

echo "--- Step 3: Pixel-by-pixel comparison ---"

if ! python3 -c "import PIL, numpy" 2>/dev/null; then
    echo "  ⚠ Installing Python dependencies (Pillow, numpy)..."
    pip3 install Pillow numpy --quiet
fi

python3 "$SCRIPT_DIR/google-compare.py" \
    "$BROILER_OUTPUT" "$REFERENCE_OUTPUT" \
    --output-dir "$OUTPUT_DIR"

echo ""
echo "=== Pipeline complete ==="
echo "Outputs:"
echo "  Broiler render:  $BROILER_OUTPUT"
echo "  Reference:       $REFERENCE_OUTPUT"
echo "  Diff image:      $OUTPUT_DIR/google-diff.png"
echo "  Report:          $OUTPUT_DIR/google-report.txt"
echo "  JS error log:    $JS_LOG"
