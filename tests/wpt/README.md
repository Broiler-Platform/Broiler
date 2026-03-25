# WPT Testsuite — Broiler CLI vs Playwright

Automated comparison of [wpt.live](https://wpt.live/) Web Platform Tests
rendered by **Broiler's Browser.CLI** and **Playwright (Chromium)**.

## Quick Start (local)

```bash
# From the repository root:
cd tests/wpt
npm install
npx playwright install chromium

# Run the full pipeline (Playwright → Broiler CLI → compare):
bash run-all.sh
```

The report is written to `tests/wpt/results/wpt-report.md`.

## What It Does

| Step | Script | Description |
|------|--------|-------------|
| 1 | `run-playwright.js` | Loads each WPT URL in headless Chromium via Playwright, captures the page text (`.txt`) and rendered HTML (`.html`). |
| 2 | `run-broiler-cli.sh` | Builds `Broiler.Cli` and runs it against the same URLs, saving `.txt` and `.html` outputs. |
| 3 | `compare.js` | Reads both result sets, normalises text, computes per-test similarity, and writes a Markdown + JSON report. |

## Test Manifest

`wpt-urls.txt` lists every test in the format:

```
category|test-id|https://wpt.live/...
```

Add or remove lines to adjust which tests run.  Categories currently include:

- **dom** — DOM nodes, traversal, events
- **html** — HTML elements, parsing, semantics
- **css** — CSS selectors, properties, layout
- **url** — URL constructor & searchparams
- **encoding** — TextEncoder / TextDecoder
- **fetch** — Fetch API basics
- **xhr** — XMLHttpRequest

## Report Classifications

| Label | Meaning |
|-------|---------|
| ✅ **MATCH** | Text outputs are ≥ 95% similar |
| ⚠️ **DIFF** | Both engines loaded the page but outputs differ significantly |
| ❌ **BROILER_FAIL** | Broiler CLI failed to capture the page |
| 🟡 **PLAYWRIGHT_FAIL** | Playwright failed (rare — indicates a wpt.live issue) |
| 🔴 **BOTH_FAIL** | Neither engine succeeded |

## CI Integration

The workflow **`.github/workflows/wpt-test.yml`** runs automatically on
pushes/PRs to `main` and can also be triggered manually.  It:

1. Sets up .NET 8 + Node.js 20
2. Installs Playwright Chromium
3. Builds Broiler CLI
4. Runs both capture scripts
5. Generates the comparison report
6. Uploads the report as a GitHub Actions artifact
7. Prints the summary to the Actions step summary

## Directory Layout

```
tests/wpt/
├── wpt-urls.txt          # Test URL manifest
├── run-playwright.js     # Playwright capture script
├── run-broiler-cli.sh    # Broiler CLI capture script
├── compare.js            # Output comparison & report generation
├── run-all.sh            # Local orchestration (all-in-one)
├── package.json          # Node.js dependencies
├── README.md             # This file
└── results/              # Generated at runtime (git-ignored)
    ├── playwright/       # Playwright outputs
    ├── broiler/          # Broiler CLI outputs
    ├── wpt-report.md     # Markdown comparison report
    └── wpt-report.json   # Machine-readable report
```

## Extending

1. **Add new tests** — append lines to `wpt-urls.txt`.
2. **Change timeout** — pass `--timeout <seconds>` to `run-all.sh`.
3. **Run only one engine** — use `--skip-broiler` or `--skip-playwright`.
4. **Custom output directories** — each script accepts `--output-dir`.

## Prerequisites

- **.NET 8 SDK** — <https://dotnet.microsoft.com/download>
- **Node.js ≥ 18** — <https://nodejs.org/>
- **Playwright Chromium** — installed automatically by `npx playwright install chromium`
