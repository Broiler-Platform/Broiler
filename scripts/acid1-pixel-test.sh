#!/usr/bin/env bash
# acid1-pixel-test.sh — Automated Acid1 rendering, reference capture, and
# pixel comparison pipeline.
#
# Usage:
#     ./scripts/acid1-pixel-test.sh [--skip-reference] [--output-dir <dir>]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ACID1_DIR="$REPO_ROOT/acid/acid1"
OUTPUT_DIR="$ACID1_DIR"
SKIP_REFERENCE=false

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
            exit 0
            ;;
        *)
            echo "Unknown option: $1" >&2
            exit 1
            ;;
    esac
done

mkdir -p "$OUTPUT_DIR"

echo "=== Acid1 Pixel Test Pipeline ==="
echo "Repository root: $REPO_ROOT"
echo "Output directory: $OUTPUT_DIR"
echo ""

echo "--- Step 1: Rendering Acid1 with Broiler CLI ---"

BROILER_OUTPUT="$OUTPUT_DIR/acid1.png"
ACID1_HTML="file://$ACID1_DIR/acid1.html"

dotnet run --project "$REPO_ROOT/src/Broiler.Cli" -- \
    --capture-image "$ACID1_HTML" \
    --output "$BROILER_OUTPUT" \
    --width 1024 --height 768 --full-page

if [[ -f "$BROILER_OUTPUT" ]]; then
    echo "  ✓ Broiler render saved to: $BROILER_OUTPUT"
    echo "  Image info: $(file "$BROILER_OUTPUT")"
else
    echo "  ✗ Broiler render FAILED — no output file" >&2
    exit 1
fi
echo ""

REFERENCE_OUTPUT="$OUTPUT_DIR/acid1-reference.png"

if [[ "$SKIP_REFERENCE" == "true" ]]; then
    echo "--- Step 2: Skipping Chromium reference rendering ---"
    if [[ -f "$REFERENCE_OUTPUT" ]]; then
        echo "  Using existing reference: $REFERENCE_OUTPUT"
    else
        echo "  ✗ No existing reference found at $REFERENCE_OUTPUT" >&2
        exit 1
    fi
else
    echo "--- Step 2: Rendering Acid1 with Chromium (Playwright) ---"

    if command -v npx &>/dev/null && command -v npm &>/dev/null; then
        PLAYWRIGHT_DIR="${TMPDIR:-/tmp}/broiler-playwright"
        if [[ ! -d "$PLAYWRIGHT_DIR/node_modules/playwright" ]]; then
            npm install --prefix "$PLAYWRIGHT_DIR" --no-save playwright >/dev/null
        fi

        PLAYWRIGHT_SCRIPT=$(mktemp "${TMPDIR:-/tmp}/acid1-playwright-XXXXXX.js")
        cat > "$PLAYWRIGHT_SCRIPT" << 'JSEOF'
const { chromium } = require('playwright');
const path = require('path');

(async () => {
    const browser = await chromium.launch({ headless: true });
    const page = await browser.newPage({ viewport: { width: 1280, height: 1024 } });
    const acid1Path = path.resolve(process.argv[2]);
    const outputPath = process.argv[3];

    await page.goto('file://' + acid1Path, { waitUntil: 'load' });
    await page.waitForTimeout(500);

    const clip = await page.evaluate(() => {
        const body = document.body;
        const rect = body.getBoundingClientRect();
        const style = getComputedStyle(body);
        const marginLeft = parseFloat(style.marginLeft) || 0;
        const marginRight = parseFloat(style.marginRight) || 0;
        const marginTop = parseFloat(style.marginTop) || 0;
        const marginBottom = parseFloat(style.marginBottom) || 0;

        return {
            x: Math.max(0, Math.floor(rect.left - marginLeft)),
            y: Math.max(0, Math.floor(rect.top - marginTop)),
            width: Math.ceil(rect.width + marginLeft + marginRight),
            height: Math.ceil(rect.height + marginTop + marginBottom)
        };
    });

    await page.screenshot({ path: outputPath, clip });
    await browser.close();
    console.log('Reference render saved to: ' + outputPath);
})();
JSEOF
        "$PLAYWRIGHT_DIR/node_modules/.bin/playwright" install chromium 2>&1 || echo "  ⚠ Playwright Chromium installation had warnings (non-fatal)"
        NODE_PATH="$PLAYWRIGHT_DIR/node_modules" \
            node "$PLAYWRIGHT_SCRIPT" "$ACID1_DIR/acid1.html" "$REFERENCE_OUTPUT" && {
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
        echo "  ⚠ Node.js/npm/npx not found — skipping Chromium rendering"
        if [[ -f "$REFERENCE_OUTPUT" ]]; then
            echo "  Using existing reference: $REFERENCE_OUTPUT"
        else
            echo "  ✗ No reference image available" >&2
            exit 1
        fi
    fi
fi
echo ""

echo "--- Step 3: Pixel-by-pixel comparison ---"

if ! python3 -c "import PIL, numpy" 2>/dev/null; then
    echo "  ⚠ Installing Python dependencies (Pillow, numpy)..."
    pip3 install Pillow numpy --quiet
fi

python3 "$SCRIPT_DIR/acid1-compare.py" \
    "$BROILER_OUTPUT" "$REFERENCE_OUTPUT" \
    --output-dir "$OUTPUT_DIR"

echo ""
echo "=== Pipeline complete ==="
echo "Outputs:"
echo "  Broiler render:  $BROILER_OUTPUT"
echo "  Reference:       $REFERENCE_OUTPUT"
echo "  Diff image:      $OUTPUT_DIR/acid1-diff.png"
echo "  Report:          $OUTPUT_DIR/acid1-report.txt"
