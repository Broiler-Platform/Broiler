# ADR-018: Acid1 Visual Comparison – html-renderer vs. Chromium (Playwright)

## Status

Documented

## Trigger

- **Issue:** [#171](https://github.com/MaiRat/Broiler/issues/171) – Acid1.html
  Visual Comparison: html-renderer vs. Chromium (Playwright)

## Context

This document provides a comprehensive, element-by-element visual comparison of
the [Acid1 CSS1 conformance test](https://www.w3.org/Style/CSS/Test/CSS1/current/test5526c.htm)
rendered by the Broiler HTML-Renderer engine and headless Chromium (via
Playwright).  It catalogs all discrepancies, classifies each by type, identifies
root causes, and proposes a prioritised fix roadmap.

### Methodology

1. **Render:** `acid1.html` and 10 isolated split sections are rendered in both
   engines at 800×600 viewport, 96 DPI, 1× device scale.
2. **Capture:** PNG screenshots are saved for each engine.
3. **Compare:** Pixel-level diff with 30-per-channel colour tolerance and 3 px
   layout tolerance.  Per-pixel mismatch CSV logs record exact coordinates and
   RGBA values.
4. **Classify:** Each section is classified using a `FailureClassification`
   (LayoutDiff / PaintDiff / RasterDiff) and a `DifferenceCategory`
   (PositionError / StyleMismatch / FontRasterisation / RenderingEngineBug /
   MissingOrExtraElement).
5. **Overlap Detection:** The Broiler fragment tree is walked to detect invalid
   float/block bounding-box intersections.

### Test Infrastructure

- `Acid1DifferentialTests` (11 tests, `Category=Differential`)
- `Acid1FloatOverlapTests` (4 tests, per-commit)
- `Acid1RepeatedRenderTests` (9 tests, `Category=Differential`)
- `Acid1DifferentialReportGenerator` (auto-generates ADR on issue closure)
- Side-by-side HTML reports in `TestData/Acid1DifferentialReports/`

## Test Configuration

- **Viewport:** 800×600
- **Pixel Diff Threshold:** 20 %
- **Color Tolerance:** 30 per channel
- **Layout Tolerance:** 3 px

## Element-by-Element Comparison

### Full Page Composite

| Metric | Value |
|--------|-------|
| Pixel Diff | 11.26 % (54,030 / 480,000 pixels) |
| Float Overlaps | 0 |
| Severity | High |
| Category | RenderingEngineBug |

The full-page composite aggregates all section-level discrepancies.  No
single rendering bug dominates; the diff is the sum of per-section differences
documented below.

---

### Section 1 – Body Border (`html` bg blue, `body` bg white + border)

| Metric | Value |
|--------|-------|
| Pixel Diff | 1.64 % (7,857 / 480,000) |
| Category | StyleMismatch |
| CSS1 Reference | §5.5.23/24 – border-width, margin |

**Elements compared:**

| Element | Property | Broiler | Chromium | Delta |
|---------|----------|---------|----------|-------|
| `html` | background | blue (#0000FF) | blue (#0000FF) | Match |
| `body` | background | white (#FFFFFF) | white (#FFFFFF) | Match |
| `body` | border-width | 5 px (`.5em`) | 5 px | Match |
| `body` | margin | 15 px (`1.5em`) | 15 px | ≤ 1 px |
| `body` | border position | Integer-rounded | Sub-pixel | ≤ 1 px |

**Root cause:** Sub-pixel rounding of `.5em` border-width and `1.5em` margin.
Chromium uses sub-pixel layout; Broiler rounds to integer pixels at intermediate
layout stages, causing minor border position and thickness differences.

**Classification:** StyleMismatch – the visual style (border position/thickness)
differs due to rounding strategy, not missing functionality.

---

### Section 2 – `dt` float:left (percentage width 10.638 %)

| Metric | Value |
|--------|-------|
| Pixel Diff | ≤ 1.74 % |
| Category | FontRasterisation |
| CSS1 Reference | §5.5.3 – float:left |

**Elements compared:**

| Element | Property | Broiler | Chromium | Delta |
|---------|----------|---------|----------|-------|
| `dt` | float | left | left | Match |
| `dt` | width | 50 px (10.638 % of 470 px) | 50 px | ≤ 1 px |
| `dt` | position (X) | Correct | Correct | ≤ 1 px |
| `dt` | position (Y) | Correct | Correct | ≤ 1 px |
| `dt` | text | Verdana rendering | Verdana rendering | Font diff |

**Root cause:** Cross-engine font rasterisation differences (anti-aliasing,
hinting, Verdana glyph metrics).  Layout positions match within tolerance.

**Classification:** FontRasterisation – irreducible without identical text
rendering backends.

---

### Section 3 – `dd` float:right (border, width, side-by-side with `dt`)

| Metric | Value |
|--------|-------|
| Pixel Diff | ≤ 1.37 % |
| Category | FontRasterisation |
| CSS1 Reference | §5.5.3 – float:right, §5.3.4 – width |

**Elements compared:**

| Element | Property | Broiler | Chromium | Delta |
|---------|----------|---------|----------|-------|
| `dd` | float | right | right | Match |
| `dd` | width | 340 px (`34em`) | 340 px | Match |
| `dd` | border | 10 px (`1em`) solid black | 10 px solid black | Match |
| `dd` | padding | 10 px (`1em`) | 10 px | Match |
| `dd` | position | Correct | Correct | ≤ 1 px |
| Text content | rendering | Verdana | Verdana | Font diff |

**Root cause:** Font rasterisation differences in text content within the
floated `dd`.  Box model (border, padding, width) matches exactly.

**Classification:** FontRasterisation.

---

### Section 4 – `li` float:left (multiple float stacking, gold bg)

| Metric | Value |
|--------|-------|
| Pixel Diff | 1.05 % |
| Category | FontRasterisation |
| CSS1 Reference | §5.5.3 – float:left stacking |

**Elements compared:**

| Element | Property | Broiler | Chromium | Delta |
|---------|----------|---------|----------|-------|
| `li` items | float | left | left | Match |
| `li` items | stacking order | Correct L-to-R | Correct L-to-R | Match |
| `li` items | background | gold | gold | Match |
| `li` items | position | Correct | Correct | ≤ 1 px |
| List markers | rendering | Simplified | Native | Differs |

**Root cause:** Font rasterisation and list marker rendering differences.
Float stacking logic is correct.

**Classification:** FontRasterisation.

---

### Section 5 – `blockquote` (float:left, asymmetric borders)

| Metric | Value |
|--------|-------|
| Pixel Diff | ≤ 1.95 % |
| Category | FontRasterisation |
| CSS1 Reference | §5.5.3, §5.5.23 – asymmetric borders |

**Elements compared:**

| Element | Property | Broiler | Chromium | Delta |
|---------|----------|---------|----------|-------|
| `blockquote` | float | left | left | Match |
| `blockquote` | border-top | 0.5em solid blue | 0.5em solid blue | Match |
| `blockquote` | border-right | 1em solid navy | 1em solid navy | Match |
| `blockquote` | border-bottom | 0.5em solid blue | 0.5em solid blue | Match |
| `blockquote` | border-left | 1em solid navy | 1em solid navy | Match |
| Border corners | diagonal joins | Trapezoid rendering | Sub-pixel | ≤ 2 px |

**Root cause:** Minor differences in asymmetric border corner join rendering.
Broiler uses trapezoid/polygon rendering for solid borders with different
widths; Chromium uses sub-pixel compositing.

**Classification:** FontRasterisation (primarily text) + minor border rendering.

---

### Section 6 – `h1` float (float:left, black bg, normal font-weight)

| Metric | Value |
|--------|-------|
| Pixel Diff | ≤ 1.92 % |
| Category | FontRasterisation |
| CSS1 Reference | §5.5.3 – float:left, §5.2.5 – font-weight |

**Elements compared:**

| Element | Property | Broiler | Chromium | Delta |
|---------|----------|---------|----------|-------|
| `h1` | float | left | left | Match |
| `h1` | background | black (#000000) | black (#000000) | Match |
| `h1` | font-weight | normal (400) | normal (400) | Match |
| `h1` | colour | white (#FFFFFF) | white (#FFFFFF) | Match |
| `h1` | text rendering | Verdana 10px | Verdana 10px | Font diff |

**Root cause:** Font rasterisation differences in `h1` text rendered at normal
weight on a black background.  White-on-black anti-aliasing differs between
engines.

**Classification:** FontRasterisation.

---

### Section 7 – `form` line-height (line-height: 1.9 on form `<p>`)

| Metric | Value |
|--------|-------|
| Pixel Diff | 1.55 % |
| Category | StyleMismatch |
| CSS1 Reference | §5.4.8 – line-height |

**Elements compared:**

| Element | Property | Broiler | Chromium | Delta |
|---------|----------|---------|----------|-------|
| Form `<p>` | line-height | 19 px (1.9 × 10px) | 19 px | ≤ 1 px |
| Radio buttons | rendering | Simplified placeholder | Native widget | Visual diff |
| Text baselines | alignment | Correct | Correct | ≤ 1 px |
| Vertical spacing | between lines | Correct | Correct | ≤ 1 px |

**Root cause:** Two factors:
1. **Form widgets:** Radio buttons are rendered as simplified placeholders by
   Broiler vs. native OS widgets by Chromium.  This is an expected, irreducible
   difference.
2. **Sub-pixel line-height:** Minor rounding difference when `line-height` is
   a unitless multiplier (1.9 × 10px = 19px).

**Classification:** StyleMismatch – form widget rendering is a known limitation.

---

### Section 8 – `clear:both` (paragraph after floats)

| Metric | Value |
|--------|-------|
| Pixel Diff | ≤ 2.84 % |
| Category | PositionError |
| CSS1 Reference | §5.5.26 – clear property |

**Elements compared:**

| Element | Property | Broiler | Chromium | Delta |
|---------|----------|---------|----------|-------|
| Clear paragraph | Y position | Below floats | Below floats | ≤ 3 px |
| Clear paragraph | clearance distance | Correct | Correct | ≤ 3 px |
| Float bottom edge | margin-box bottom | Correct | Correct | ≤ 1 px |
| Margin collapsing | clear + margin-top | Absorbed | Absorbed | ≤ 2 px |

**Root cause:** The clearance computation correctly moves the element below
floats, but the exact vertical distance differs by a few pixels due to margin
collapsing precision.  Chromium may handle the interaction between
`clear:both` margin-top and the float's margin-bottom with sub-pixel
precision that Broiler's integer rounding does not match.

**Classification:** PositionError – the cleared element's Y position differs
from Chromium's by a small margin.

---

### Section 9 – Percentage Width (10.638 % and 41.17 %)

| Metric | Value |
|--------|-------|
| Pixel Diff | ≤ 10.67 % |
| Category | PositionError |
| CSS1 Reference | §5.3.4 – percentage widths |

**Elements compared:**

| Element | Property | Broiler | Chromium | Delta |
|---------|----------|---------|----------|-------|
| `dt` | width (10.638 %) | 50 px | 50 px | ≤ 1 px |
| `#bar` div | width (41.17 %) | ~140 px | ~140 px | ≤ 2 px |
| `dd` | content width | 340 px (34em) | 340 px | Match |
| `dt` + `dd` | side-by-side layout | Correct | Correct | ≤ 3 px |
| Nested `#bar` | horizontal position | Offset | Baseline | ≤ 5 px |

**Root cause:** The percentage width resolution itself is correct (41.17 % of
340 px = 139.978 px).  However, the interaction between multiple floated
elements with percentage widths inside a containing block with explicit `em`
width produces cascading sub-pixel rounding differences.  Each rounding step
accumulates, causing the horizontal positions of subsequent elements to diverge.

This is the largest remaining section-level discrepancy.  The root cause was
identified as CSS2.1 §9.5 non-floated block overlap with floats (fixed in
Priority 1).  The residual diff is from accumulated sub-pixel rounding in
percentage width resolution.

**Classification:** PositionError – element positions differ due to
accumulated rounding in percentage width calculations.

---

### Section 10 – `dd` Height/Clearance (content-box height, float clearance)

| Metric | Value |
|--------|-------|
| Pixel Diff | 2.36 % |
| Category | PositionError |
| CSS1 Reference | §5.3.5 – height, §5.5.26 – clear |

**Elements compared:**

| Element | Property | Broiler | Chromium | Delta |
|---------|----------|---------|----------|-------|
| `dd` | height | 270 px (27em) | 270 px | Match |
| `dt` (float) | height | 280 px (28em) | 280 px | Match |
| Float bottom | extends beyond `dd` | Correct | Correct | Match |
| Clear paragraph | Y (below tallest float) | Correct | Correct | ≤ 3 px |
| Content below clear | vertical spacing | Correct | Correct | ≤ 2 px |

**Root cause:** When a parent has explicit `height: 27em` and contains floats
taller than 27em, the subsequent `clear:both` element must clear to the
float's bottom, not the parent's explicit height.  Broiler handles this
correctly but the exact clearance distance differs by a few pixels from
Chromium due to margin and border rounding.

**Classification:** PositionError – minor Y-position differences in cleared
content below tall floats.

---

## Difference Summary Table

| # | Section | Pixel Diff | Category | Root Cause | Fixable? |
|---|---------|-----------|----------|------------|----------|
| — | Full page | 11.26 % | RenderingEngineBug | Aggregate of all sections | Via section fixes |
| 1 | Body border | 1.64 % | StyleMismatch | Sub-pixel border rounding | Partially (Priority 4) |
| 2 | dt float:left | ≤ 1.74 % | FontRasterisation | Font anti-aliasing | No (irreducible) |
| 3 | dd float:right | ≤ 1.37 % | FontRasterisation | Font anti-aliasing | No (irreducible) |
| 4 | li float:left | 1.05 % | FontRasterisation | Font + list markers | No (irreducible) |
| 5 | blockquote | ≤ 1.95 % | FontRasterisation | Font + border corners | No (mostly irreducible) |
| 6 | h1 float | ≤ 1.92 % | FontRasterisation | White-on-black AA | No (irreducible) |
| 7 | form line-height | 1.55 % | StyleMismatch | Radio widget + line-height | Partially (Priority 4) |
| 8 | clear:both | ≤ 2.84 % | PositionError | Clearance margin rounding | Yes (Priority 2 residual) |
| 9 | % width | ≤ 10.67 % | PositionError | Accumulated % rounding | Partially (Priority 4) |
| 10 | dd height | 2.36 % | PositionError | Clearance distance rounding | Partially (Priority 3 residual) |

### Category Distribution

| Category | Sections | Description |
|----------|----------|-------------|
| PositionError | 8, 9, 10 | Element positions differ due to rounding |
| StyleMismatch | 1, 7 | Visual style differs (border/widget rendering) |
| FontRasterisation | 2, 3, 4, 5, 6 | Cross-engine font rendering differences |

## Historical Progress

All four ADR-009 priorities have been completed, dramatically reducing diffs:

| Section | ADR-009 Baseline | Current | Improvement |
|---------|-----------------|---------|-------------|
| 1 – Body border | 89.97 % | 1.64 % | −88.33 pp |
| 2 – dt float:left | 86.30 % | ≤ 1.74 % | −84.56 pp |
| 3 – dd float:right | 84.16 % | ≤ 1.37 % | −82.79 pp |
| 4 – li float:left | 82.05 % | 1.05 % | −81.00 pp |
| 5 – blockquote | 92.00 % | ≤ 1.95 % | −90.05 pp |
| 6 – h1 float | 91.23 % | ≤ 1.92 % | −89.31 pp |
| 7 – form line-height | 85.22 % | 1.55 % | −83.67 pp |
| 8 – clear:both | 72.17 % | ≤ 2.84 % | −69.33 pp |
| 9 – % width | 84.64 % | ≤ 10.67 % | −73.97 pp |
| 10 – dd height | < 50 % | 2.36 % | > −47.64 pp |

## Fix Roadmap

### Completed Priorities (ADR-009)

- **Priority 1 – Float layout (CSS2.1 §9.5):** ✅ Fixed.  Non-floated blocks
  now correctly overlap with preceding floats.
- **Priority 2 – Box model / canvas background (CSS2.1 §14.2):** ✅ Fixed.
  Canvas background propagation from `body` to viewport.
- **Priority 3 – Border rendering (CSS2.1 §8.5):** ✅ Fixed.  Asymmetric
  border trapezoid rendering and parent padding preservation.
- **Priority 4 – Typography / line-height (CSS1 §5.4.8):** ✅ Fixed.
  Block-in-inline layout for form paragraphs.

### Remaining Priorities

#### Priority 5 – Sub-Pixel Rounding (Sections 1, 7, 8, 9, 10)

**Goal:** Reduce remaining PositionError and StyleMismatch sections.

**Quick fixes:**
1. Audit `em`-to-pixel conversion for fractional values (`.5em`, `1.5em`) to
   ensure consistent rounding direction (round half up).
2. Investigate percentage-width resolution in cascading contexts where each
   rounding step accumulates (Section 9).
3. Consider sub-pixel layout support in the paint pipeline.

**Estimated effort:** 2–3 days.  Expected improvement: Section 9 from 10.67 %
to < 5 %, Sections 1/7/8/10 from 1.5–2.8 % to < 1 %.

#### Priority 6 – Form Widget Fidelity (Section 7)

**Goal:** Improve radio button placeholder rendering.

**Tasks:**
1. Measure Chromium radio button dimensions and position.
2. Update Broiler placeholder to match size and center alignment.

**Estimated effort:** 1 day.  Expected improvement: Section 7 from 1.55 % to
< 1 %.

#### Priority 7 – Font Rasterisation Documentation (Sections 2–6)

**Goal:** Accept and document irreducible font rendering differences.

**Tasks:**
1. Verify Verdana availability on CI runners; document fallback font impact.
2. Add font metrics comparison tests (line-box height, advance width).
3. Document accepted residual diff levels per section.

**Estimated effort:** 1 day.  These differences are inherent to cross-engine
rendering and cannot be eliminated without matching Chromium's text backend.

### Threshold Tightening Plan

| Milestone | Threshold | Prerequisite |
|-----------|-----------|--------------|
| Current | 20 % | All sections pass |
| After Priority 5 | 8 % | Section 9 < 5 % |
| After Priority 5–6 | 5 % | All sections < 3 % |
| Final target | 3 % | Only font rasterisation diffs remain |

## Reproducibility

### Running the Comparison Locally

```bash
# 1. Build
dotnet build Broiler.slnx

# 2. Install Playwright Chromium
pwsh HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/bin/Debug/net8.0/playwright.ps1 install chromium

# 3. Run differential tests
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/ \
  --filter "FullyQualifiedName~Acid1DifferentialTests"

# 4. View reports
ls HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/TestData/Acid1DifferentialReports/
```

### Report Outputs

Each test generates in `TestData/Acid1DifferentialReports/`:

| File | Description |
|------|-------------|
| `*_broiler.png` | Broiler rendering |
| `*_chromium.png` | Chromium rendering |
| `*_diff.png` | Pixel diff overlay (red = mismatch) |
| `*_mismatches.csv` | Per-pixel position and RGBA values |
| `*_fragment.json` | Broiler fragment tree (layout diagnostics) |
| `*_displaylist.json` | Broiler display list (paint diagnostics) |
| `*_report.html` | Side-by-side HTML comparison report |

### CSV Mismatch Format

```csv
X,Y,ActualR,ActualG,ActualB,ActualA,BaselineR,BaselineG,BaselineB,BaselineA
12,34,255,0,0,255,0,0,255,255
```

## Related Documents

- **ADR-009** – Original differential testing baseline and fix roadmap
- **ADR-010 through ADR-017** – Auto-generated point-in-time snapshots
- **[Acid1 Error Resolution Roadmap](../roadmap/acid1-error-resolution.md)** –
  Detailed fix plan with CSS references
- **[Acid1 Testing Guide](../acid1-testing.md)** – Test suite documentation

## Related Tests

| Test File | Project | Purpose |
|-----------|---------|---------|
| `Acid1DifferentialTests.cs` | `HtmlRenderer.Image.Tests` | Pixel comparison reproducing the visual comparison above |
| `Acid1CaptureTests.cs` | `Broiler.Cli.Tests` | Similarity scoring against reference image |
| `Acid1SplitTests.cs` | `Broiler.Cli.Tests` | Per-section regression diagnostics |

## Consequences

- All acid1.html rendering differences between Broiler and Chromium are
  cataloged with element-level detail, root cause analysis, and categorisation.
- A clear three-priority roadmap (sub-pixel rounding, form widgets, font
  documentation) is proposed for the remaining discrepancies.
- The comparison is reproducible by running `Acid1DifferentialTests` with
  Playwright Chromium installed.
- Future improvements should re-run the comparison and update this document
  with the new diff ratios.
