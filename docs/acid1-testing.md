# Acid1 Test Suite

## Overview

The [Acid1 test](https://www.w3.org/Style/CSS/Test/CSS1/current/test5526c.htm)
is the W3C CSS1 conformance test. It exercises fundamental CSS1 features
including floats, clears, percentage widths, background colours, borders, and
the content-box model. A fully CSS1-conformant renderer should produce output
indistinguishable from the
[reference rendering](https://www.w3.org/Style/CSS/Test/CSS1/current/sec5526c.gif).

Broiler's Acid1 test suite validates the HTML-Renderer engine against this
standard using three complementary test classes and a reference image.

## Quick Start

Run **all** Acid1 tests with a single command:

```bash
dotnet test src/Broiler.Cli.Tests/ --filter "FullyQualifiedName~Acid1"
```

## Prerequisites

| Dependency | Version | Purpose |
|------------|---------|---------|
| .NET SDK | 8.0+ | Build and test runner |
| SkiaSharp | (bundled) | Image rendering backend |
| xUnit | 2.5+ | Test framework |

All dependencies are declared in the project files and restored automatically
by `dotnet restore`. No additional setup is required.

### Verify Prerequisites

```bash
dotnet --version          # Must be 8.0 or later
dotnet restore Broiler.slnx
dotnet build Broiler.slnx
```

## Test Classes

### Acid1CaptureTests (~30 tests)

Visual regression and structural tests for `acid/acid1/acid1.html`. Tests
include HTML structure validation, CSS rule verification, pixel-level
rendering checks, image format validation, and similarity scoring against the
reference image.

```bash
dotnet test src/Broiler.Cli.Tests/ --filter "FullyQualifiedName~Acid1CaptureTests"
```

### Acid1ProgrammaticTests (~25 tests)

Programmatic layout and box-model tests that render HTML snippets extracted
from the Acid1 test and verify float positioning, margin collapsing,
percentage width resolution, clear behaviour, and border-box computation.

```bash
dotnet test src/Broiler.Cli.Tests/ --filter "FullyQualifiedName~Acid1ProgrammaticTests"
```

### Acid1SplitTests (~22 tests)

Split tests that isolate each CSS1 feature from the full Acid1 page into
individual HTML files (sections 1–10). Each section targets a specific
rendering feature for precise diagnostics.

```bash
dotnet test src/Broiler.Cli.Tests/ --filter "FullyQualifiedName~Acid1SplitTests"
```

| Section | File | CSS1 Feature |
|---------|------|-------------|
| 1 | `section1-body-border.html` | Body border, html/body backgrounds |
| 2 | `section2-dt-float-left.html` | `dt` float:left, percentage width |
| 3 | `section3-dd-float-right.html` | `dd` float:right alongside `dt` |
| 4 | `section4-li-float-left.html` | `li` float:left stacking |
| 5 | `section5-blockquote-float.html` | `blockquote` float:left, asymmetric borders |
| 6 | `section6-h1-float.html` | `h1` float:left, black background |
| 7 | `section7-form-line-height.html` | Form `line-height: 1.9` |
| 8 | `section8-clear-both.html` | `clear: both` paragraph |
| 9 | `section9-percentage-width.html` | Percentage widths (10.638%, 41.17%) |
| 10 | `section10-dd-height-clearance.html` | `dd` content-box height and float clearance |

## Test Data

| File | Location | Description |
|------|----------|-------------|
| `acid1.html` | `acid/acid1/acid1.html` | The W3C CSS1 conformance test page |
| `acid1.png` | `acid/acid1/acid1.png` | Reference rendering for similarity scoring |
| `acid1-fail.png` | `acid/acid1/acid1-fail.png` | Known-bad rendering for comparison validation |
| Split sections | `acid/acid1/split/` | 10 isolated HTML files (see table above) |

Test data is copied to the build output directory via MSBuild `<Content>`
items in `Broiler.Cli.Tests.csproj` and is available at
`TestData/acid1.html`, `TestData/acid1.png`, and `TestData/split/` at
runtime.

## CI Integration

Acid1 tests run automatically on every push and pull request as part of the
main CI pipeline (`.github/workflows/build.yml`). They are included in the
`dotnet test` step and also run as a dedicated Acid1 verification step:

```yaml
- name: Acid1 Tests
  run: dotnet test src/Broiler.Cli.Tests/ --no-build --configuration Release --filter "FullyQualifiedName~Acid1"
```

## Running Locally

### Full Acid1 Suite

```bash
dotnet test src/Broiler.Cli.Tests/ --filter "FullyQualifiedName~Acid1"
```

### Single Test Class

```bash
# Capture tests only
dotnet test src/Broiler.Cli.Tests/ --filter "FullyQualifiedName~Acid1CaptureTests"

# Programmatic layout tests only
dotnet test src/Broiler.Cli.Tests/ --filter "FullyQualifiedName~Acid1ProgrammaticTests"

# Split section tests only
dotnet test src/Broiler.Cli.Tests/ --filter "FullyQualifiedName~Acid1SplitTests"
```

### Single Test

```bash
dotnet test src/Broiler.Cli.Tests/ --filter "FullyQualifiedName~Acid1SplitTests.Section1_BodyBorder_HtmlHasBlueBackground"
```

### Verbose Output

```bash
dotnet test src/Broiler.Cli.Tests/ --filter "FullyQualifiedName~Acid1" --verbosity detailed
```

## Current Status

All four fix priorities from ADR-009 have been completed.  The Acid1 test
suite now achieves **< 12 % pixel diff** against headless Chromium for the
full page, with most individual sections below 3 %.  Recent improvements:

- **Priority 1 (Float Layout):** ✅ Fixed. Sections 2–6 dropped from
  82–92 % to < 2 %.
- **Priority 2 (Box Model / Canvas Background):** ✅ Fixed. Section 1
  89.97 % → 1.64 %, Section 9 84.64 % → 10.67 %.
- **Priority 3 (Border Rendering):** ✅ Fixed. Section 5 92.00 % → 0.57 %.
- **Priority 4 (Typography / Line-Height):** ✅ Fixed. Section 7
  85.22 % → 1.55 %.

Remaining differences are primarily due to percentage-width containing-block
resolution (Section 9), clear distance computation, and cross-engine font
rasterisation.  See the
[Acid1 Error Resolution Roadmap](roadmap/acid1-error-resolution.md) for the
next-phase fix plan.

## Differential Testing (Chromium Comparison)

The `Acid1DifferentialTests` class in `HtmlRenderer.Image.Tests` renders
`acid1.html` and each split section in both the Broiler engine and headless
Chromium (via Playwright), producing pixel-diff reports.

### Prerequisites

In addition to the standard prerequisites above, differential tests require
Playwright Chromium to be installed:

```bash
pwsh HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/bin/Release/net8.0/playwright.ps1 install chromium
```

### Running Differential Tests

```bash
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/ \
  --filter "FullyQualifiedName~Acid1DifferentialTests"
```

These tests are tagged `Category=Differential` and are excluded from
per-commit CI builds. They run nightly via `nightly-differential.yml`.

### Reports

Side-by-side HTML comparison reports are generated in:

```
HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/TestData/Acid1DifferentialReports/
```

Each report includes the Broiler PNG, Chromium PNG, pixel-diff overlay, and
JSON diagnostics (fragment tree, display list).

#### Per-Pixel Mismatch Log

Each differential report also generates a CSV mismatch log
(`<testname>_mismatches.csv`) containing the position and RGBA colour values
for every differing pixel (capped at 10,000 entries).  The CSV format is:

```csv
X,Y,ActualR,ActualG,ActualB,ActualA,BaselineR,BaselineG,BaselineB,BaselineA
12,34,255,0,0,255,0,0,255,255
```

- **X, Y** – pixel coordinates (0-based).
- **Actual\*** – Broiler (html-renderer) colour channels.
- **Baseline\*** – Chromium colour channels.

The HTML report links to the CSV and includes a mismatch summary.  Use the log
to identify colour/position patterns and prioritise renderer fixes.

### Current Diff Ratios

| Section | CSS1 Feature | Pixel Diff |
|---------|-------------|-----------|
| Full page | All features combined | 11.26 % |
| 1 – Body border | html/body backgrounds + border | 1.64 % |
| 2 – dt float:left | Float + percentage width | 0.89 % |
| 3 – dd float:right | Float + border + side-by-side | 0.65 % |
| 4 – li float:left | Multiple float stacking | 1.05 % |
| 5 – blockquote | Float + asymmetric borders | 0.57 % |
| 6 – h1 float | Float + black background | 0.65 % |
| 7 – form line-height | line-height: 1.9 | 1.55 % |
| 8 – clear:both | Clear after floats | 2.84 % |
| 9 – % width | Percentage widths | 10.67 % |
| 10 – dd height | Content-box height | 2.36 % |

See [ADR-009](adr/009-acid1-differential-testing.md) for the original error
documentation and fix roadmap, [ADR-018](adr/018-acid1-visual-comparison.md)
for the element-by-element visual comparison, and the
[Acid1 Error Resolution Roadmap](roadmap/acid1-error-resolution.md) for the
next-phase plan.

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Build fails | Run `dotnet restore Broiler.slnx` first |
| Test data not found | Ensure the project was built: `dotnet build Broiler.slnx` |
| Flaky test in parallel run | Run the specific test in isolation (see Single Test above) |
| Similarity below threshold | Run split tests to identify which section regressed |
| Playwright not found | Install Chromium: `pwsh .../playwright.ps1 install chromium` |

---

## Related Documents

- [Testing Guide](testing-guide.md) — how to run and write tests across all suites
- [Testing Roadmap](testing-roadmap.md) — planned testing improvements
- [CSS2 Verification Report](css2-verification-report.md) — CSS 2.1 spec compliance status
- [Roadmap: Acid1 Error Resolution](roadmap/acid1-error-resolution.md) — fix tracker for Acid1 rendering discrepancies
- [ADR-009: Acid1 Differential Testing](adr/009-acid1-differential-testing.md) — decision record for Acid1 differential testing approach
- [ADR-018: Acid1 Visual Comparison](adr/018-acid1-visual-comparison.md) — visual comparison results
