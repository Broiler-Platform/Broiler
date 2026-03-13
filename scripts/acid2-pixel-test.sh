#!/usr/bin/env bash
# acid2-pixel-test.sh — Automated Acid2 rendering, reference capture, and
# pixel comparison pipeline.
#
# Usage:
#     ./scripts/acid2-pixel-test.sh [--skip-reference] [--output-dir <dir>]
#
# Prerequisites:
#     - .NET 8 SDK (for broiler.cli)
#     - Python 3 with Pillow and numpy (for pixel comparison)
#     - Optional: Node.js + Playwright (for Chromium reference rendering)
#
# Steps:
#     1. Render Acid2 at #top using broiler.cli → acid2.png
#     2. (Optional) Render using Chromium/Playwright → acid2-reference.png
#     3. Compare pixel-by-pixel → acid2-diff.png + acid2-report.txt

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ACID2_DIR="$REPO_ROOT/acid/acid2"
OUTPUT_DIR="$ACID2_DIR"
SKIP_REFERENCE=false

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
        -h|--help)
            echo "Usage: $0 [--skip-reference] [--output-dir <dir>]"
            echo ""
            echo "Options:"
            echo "  --skip-reference  Skip Chromium reference rendering (use existing reference)"
            echo "  --output-dir      Output directory (default: acid/acid2/)"
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

echo "=== Acid2 Pixel Test Pipeline ==="
echo "Repository root: $REPO_ROOT"
echo "Output directory: $OUTPUT_DIR"
echo ""

# --- Step 1: Render with Broiler CLI -----------------------------------------

echo "--- Step 1: Rendering Acid2 #top with Broiler CLI ---"

BROILER_OUTPUT="$OUTPUT_DIR/acid2.png"
ACID2_HTML="file://$ACID2_DIR/acid2.html#top"

dotnet run --project "$REPO_ROOT/src/Broiler.Cli" -- \
    --capture-image "$ACID2_HTML" \
    --output "$BROILER_OUTPUT" \
    --width 1024 --height 768

if [[ -f "$BROILER_OUTPUT" ]]; then
    echo "  ✓ Broiler render saved to: $BROILER_OUTPUT"
    echo "  Image info: $(file "$BROILER_OUTPUT")"
else
    echo "  ✗ Broiler render FAILED — no output file" >&2
    exit 1
fi
echo ""

# --- Step 2: Render with Chromium (optional) ---------------------------------

REFERENCE_OUTPUT="$OUTPUT_DIR/acid2-reference.png"

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
    echo "--- Step 2: Rendering Acid2 with Chromium (Playwright) ---"

    if command -v npx &>/dev/null; then
        PLAYWRIGHT_SCRIPT=$(mktemp /tmp/acid2-playwright-XXXXXX.js)
        cat > "$PLAYWRIGHT_SCRIPT" << 'JSEOF'
const { chromium } = require('playwright');
const path = require('path');

(async () => {
    const browser = await chromium.launch({ headless: true });
    const page = await browser.newPage({ viewport: { width: 1024, height: 768 } });
    const acid2Path = path.resolve(process.argv[2]);
    const outputPath = process.argv[3];

    await page.goto('file://' + acid2Path + '#top', { waitUntil: 'load' });
    await page.waitForTimeout(2000);
    await page.screenshot({ path: outputPath, fullPage: false });
    await browser.close();
    console.log('Reference render saved to: ' + outputPath);
})();
JSEOF
        npx playwright install chromium 2>&1 || echo "  ⚠ Playwright Chromium installation had warnings (non-fatal)"
        node "$PLAYWRIGHT_SCRIPT" "$ACID2_DIR/acid2.html" "$REFERENCE_OUTPUT" 2>/dev/null && {
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

python3 "$SCRIPT_DIR/acid2-compare.py" \
    "$BROILER_OUTPUT" "$REFERENCE_OUTPUT" \
    --output-dir "$OUTPUT_DIR"

echo ""
echo "=== Pipeline complete ==="
echo "Outputs:"
echo "  Broiler render:  $BROILER_OUTPUT"
echo "  Reference:       $REFERENCE_OUTPUT"
echo "  Diff image:      $OUTPUT_DIR/acid2-diff.png"
echo "  Report:          $OUTPUT_DIR/acid2-report.txt"
