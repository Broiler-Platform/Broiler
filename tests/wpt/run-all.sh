#!/usr/bin/env bash
# run-all.sh — Orchestrate the full WPT test pipeline locally.
#
# Usage:
#   ./tests/wpt/run-all.sh [--skip-broiler] [--skip-playwright] [--timeout <sec>]
#
# Steps:
#   1. Install Node.js dependencies (Playwright + browser)
#   2. Run Playwright reference captures
#   3. Build & run Broiler CLI captures
#   4. Compare outputs and generate report
#
# Prerequisites:
#   - .NET 8 SDK
#   - Node.js ≥ 18

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SKIP_BROILER=false
SKIP_PLAYWRIGHT=false
TIMEOUT=30

while [[ $# -gt 0 ]]; do
    case "$1" in
        --skip-broiler)    SKIP_BROILER=true;    shift ;;
        --skip-playwright) SKIP_PLAYWRIGHT=true;  shift ;;
        --timeout)         TIMEOUT="$2";          shift 2 ;;
        -h|--help)
            echo "Usage: $0 [--skip-broiler] [--skip-playwright] [--timeout <sec>]"
            exit 0 ;;
        *) echo "Unknown option: $1" >&2; exit 1 ;;
    esac
done

echo "========================================"
echo "  WPT Test Pipeline — Broiler vs Playwright"
echo "========================================"
echo ""

# ── Step 0: Clean previous results ──────────────────────────────────────
echo "Cleaning previous results …"
rm -rf "$SCRIPT_DIR/results"

# ── Step 1: Install Node.js dependencies ────────────────────────────────
echo ""
echo "── Step 1: Installing Node.js dependencies ──"
cd "$SCRIPT_DIR"
npm install
npx playwright install chromium

# ── Step 2: Playwright captures ─────────────────────────────────────────
if [[ "$SKIP_PLAYWRIGHT" == "false" ]]; then
    echo ""
    echo "── Step 2: Running Playwright reference captures ──"
    node run-playwright.js --timeout "$((TIMEOUT * 1000))"
else
    echo ""
    echo "── Step 2: Skipped (--skip-playwright) ──"
fi

# ── Step 3: Broiler CLI captures ────────────────────────────────────────
if [[ "$SKIP_BROILER" == "false" ]]; then
    echo ""
    echo "── Step 3: Running Broiler CLI captures ──"
    bash run-broiler-cli.sh --timeout "$TIMEOUT"
else
    echo ""
    echo "── Step 3: Skipped (--skip-broiler) ──"
fi

# ── Step 4: Compare & Report ────────────────────────────────────────────
echo ""
echo "── Step 4: Comparing outputs and generating report ──"
node compare.js

echo ""
echo "Pipeline complete.  Report at: $SCRIPT_DIR/results/wpt-report.md"
