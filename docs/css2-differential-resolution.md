# CSS2 Differential Resolution: html-renderer vs Chromium

## Overview

This document tracks the resolution of rendering differences identified in
the [CSS2 Differential Verification](css2-differential-verification.md). It
provides root-cause analysis, code pointers, and progress-tracking for each
category of discrepancy.

## Baseline (Pre-Fix)

| Metric              | Count |
|---------------------|-------|
| Total tests         | 280   |
| Failing (>5% diff)  | 126   |
| Critical (≥20%)     | 119   |
| High (10–20%)       | 2     |
| Medium (5–10%)      | 5     |
| Low (<5%)           | 148   |
| Identical (0%)      | 6     |

## Root-Cause Analysis

### 1. User-Agent Stylesheet Differences (119 Critical)

**Cause:** Chromium's HTML5 parser implicitly creates `<html>` and `<body>`
wrapper elements for HTML fragments, then applies its UA stylesheet
(`body { margin: 8px }` and background propagation per CSS2.1 §14.2).
The html-renderer parses fragments as-is without implicit wrappers, so
`body { margin: 8px }` only applies when `<body>` is explicitly present.

**Impact:** Block-only test snippets without `<html>`/`<body>` wrappers
show near-total pixel differences because element positions differ by 8px.

**Resolution:**
- `body { margin: 8px }` already present in `CssDefaults.cs` (line 28).
- **Fixed:** CSS2.1 §14.2 background propagation now correctly suppresses
  double-painting when the body/html background is propagated to the canvas
  (`PaintWalker.cs`).
- **Code:** `PaintWalker.FindCanvasBackground()` returns the source fragment;
  `PaintFragment()` skips `EmitBackground()` for that fragment.

### 2. Table Layer Background Propagation (3 Failures)

**Cause:** CSS2.1 §17.5.1 defines a six-layer painting model for tables:
table → column-groups → columns → row-groups → rows → cells. Column
backgrounds must be painted before row backgrounds so they show through
transparent cells.

**Resolution:**
- **Fixed:** `PaintWalker.PaintTableChildren()` implements the six-layer
  model, painting column/column-group backgrounds before row-group/row/cell
  backgrounds.

### 3. Float/Block Overlap (6+ Tests, Chapters 9/10)

**Cause:** CSS2.1 §9.5.1 float placement violations where floats overlap
with block containers or other floats.

**Prior fix:** CSS2.1 §9.5.1 rule 6 enforcement added in `CssBox.cs`
(line 339–345) — `CollectPrecedingFloatsInBfc()` + top constraint.

**Status:** Monitored. Remaining overlap issues require per-test diagnosis
of the specific float/block interaction.

### 4. Table Height Distribution (2 Failures)

**Cause:** CSS2.1 §17.5.3 row height algorithm differences between
html-renderer and Chromium. When a cell spans multiple rows, the excess
height distribution to rows may differ.

**Status:** Requires investigation of the table height distribution code
in `CssTable.cs` and comparison with Chromium behaviour.

### 5. Medium-Severity Rendering Differences (5 Tests)

**Cause:** Mixed issues in float, table, and vertical-align rendering
where pixel differences are 5–10%.

**Status:** Requires per-test investigation after P1–P4 are resolved.

### 6. Font Rasterisation Differences (148 Low)

**Cause:** Different font engines (Skia/FreeType vs Chromium's HarfBuzz)
produce sub-pixel differences in glyph rendering and anti-aliasing.

**Impact:** Always <5% pixel difference. Not a rendering bug.

**Status:** Monitored for regression only. These are expected differences
between rendering engines.

## Code Pointers

| Area | File | Key Method/Location |
|------|------|---------------------|
| UA Stylesheet | `HtmlRenderer.Core/Core/CssDefaults.cs:28` | `body { margin: 8px }` |
| Canvas BG propagation | `HtmlRenderer.Orchestration/Core/IR/PaintWalker.cs` | `FindCanvasBackground()`, `EmitCanvasBackground()` |
| BG suppression | `HtmlRenderer.Orchestration/Core/IR/PaintWalker.cs` | `PaintFragment()` — `propagatedFrom` parameter |
| Table layer paint | `HtmlRenderer.Orchestration/Core/IR/PaintWalker.cs` | `PaintTableChildren()` |
| Float placement | `HtmlRenderer.Dom/Core/Dom/CssBox.cs:325–437` | Float positioning with `CollectPrecedingFloatsInBfc()` |
| Float rule 6 | `HtmlRenderer.Dom/Core/Dom/CssBox.cs:340–345` | `top = Math.Max(top, pf.Location.Y)` |
| Table layout | `HtmlRenderer.Dom/Core/Dom/CssTable.cs` | Table width/height algorithms |
| Differential tests | `HtmlRenderer.Image.Tests/Css2DifferentialVerificationTests.cs` | `VerifyAllCss2Tests_GenerateReport()` |

## Milestones

| ID | Target | Status |
|----|--------|--------|
| M1 | Critical issues ≤ 6, pass rate ≥ 90% | 🔄 In Progress |
| M2 | Table backgrounds all correct (Ch 17: 0 Critical) | ✅ Code Complete |
| M3 | Float overlaps resolved (0 overlap warnings) | 🔄 In Progress |
| M4 | Table heights correct (0 High-severity) | ⬜ Pending |
| M5 | Medium diffs resolved (0 Medium) | ⬜ Pending |
| M6 | CI regression gate on Low tests | ⬜ Pending |
| M7 | Differential coverage expanded to chapters 4, 5, 8 | ⬜ Pending |

## Test Commands

```bash
# Build
dotnet build Broiler.slnx

# Run all unit tests (excluding differential report generation)
dotnet test Broiler.slnx --filter "Category!=Differential&Category!=DifferentialReport"

# Run background propagation and table layer tests
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/ \
  --filter "FullyQualifiedName~PaintWalker_CanvasBgPropagation|FullyQualifiedName~PaintWalker_TableLayers"

# Run differential verification (requires Playwright/Chromium)
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/ \
  --filter "FullyQualifiedName~Css2DifferentialVerificationTests"
```

## Layout Shift Implications

Adding `body { margin: 8px }` and fixing background propagation may shift
content by 8px in both directions when the rendered HTML lacks explicit
`<html>`/`<body>` elements (fragment mode). This is intentional alignment
with browser behaviour.

Applications using html-renderer for tooltips or rich text labels that
provide bare HTML fragments will not be affected, because the margin only
applies when a `<body>` element exists in the parsed document.

---

## Related Documents

- [CSS2 Differential Verification](css2-differential-verification.md) — pixel-by-pixel comparison results that feed into this resolution tracker
- [CSS2 Verification Report](css2-verification-report.md) — high-level CSS 2.1 spec compliance summary
- [Roadmap: CSS2 Differential Resolution](roadmap/css2-differential-resolution.md) — roadmap for resolving all differential failures
- [Roadmap: CSS2 Verification Report Resolution](roadmap/css2-verification-report-resolution.md) — roadmap for addressing CSS2 verification issues
