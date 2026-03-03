# CSS2 Specification Verification Report

> **Scope:** High-level CSS 2.1 compliance summary — test-suite inventory,
> checklist coverage, observed issues, and recommendations. This is the
> **top-level entry point** for CSS2 verification. For detailed
> pixel-comparison data see
> [CSS2 Differential Verification](css2-differential-verification.md). For
> root-cause analysis and fix tracking see
> [CSS2 Differential Resolution](css2-differential-resolution.md).

## 1. Overview

This report documents the verification of the html-renderer engine against
the CSS 2.1 specification (W3C Recommendation, June 2011). It lists all
testsuites used, their provenance, Chromium/Playwright comparison status,
observed rendering issues, and recommendations for additional coverage.

**Date:** 2026-03-02
**Engine:** html-renderer (Broiler) — SkiaSharp raster backend
**Reference browser:** Chromium (headless, via Playwright)
**Viewport:** 800 × 600 pixels
**Total test count:** 1691 (1451 html-renderer + 240 CLI)

---

## 2. Testsuites Used and Provenance

### 2.1 CSS2 Chapter Tests (In-House)

| Suite | Location | Tests | Provenance |
|-------|----------|-------|------------|
| Css2Chapter1Tests | `HtmlRenderer.Image.Tests/Css2Chapter1Tests.cs` | 31 | Hand-written against CSS 2.1 §1 (About the Specification) |
| Css2Chapter2Tests | `HtmlRenderer.Image.Tests/Css2Chapter2Tests.cs` | 35 | Hand-written against CSS 2.1 §2 (Introduction) |
| Css2Chapter3Tests | `HtmlRenderer.Image.Tests/Css2Chapter3Tests.cs` | 30 | Hand-written against CSS 2.1 §3 (Conformance) |
| Css2Chapter4Tests | `HtmlRenderer.Image.Tests/Css2Chapter4Tests.cs` | 65 | Hand-written against CSS 2.1 §4 (Syntax and Data Types) |
| Css2Chapter5Tests | `HtmlRenderer.Image.Tests/Css2Chapter5Tests.cs` | 66 | Hand-written against CSS 2.1 §5 (Selectors) |
| Css2Chapter6Tests | `HtmlRenderer.Image.Tests/Css2Chapter6Tests.cs` | 74 | Hand-written against CSS 2.1 §6 (Cascading and Inheritance) |
| Css2Chapter7Tests | `HtmlRenderer.Image.Tests/Css2Chapter7Tests.cs` | 43 | Hand-written against CSS 2.1 §7 (Media Types) |
| Css2Chapter8Tests | `HtmlRenderer.Image.Tests/Css2Chapter8Tests.cs` | 81 | Hand-written against CSS 2.1 §8 (Box Model) |
| Css2Chapter9Tests | `HtmlRenderer.Image.Tests/Css2Chapter9Tests.cs` | 49 | Hand-written against CSS 2.1 §9 (Visual Formatting Model) |
| Css2Chapter10Tests | `HtmlRenderer.Image.Tests/Css2Chapter10Tests.cs` | 132 | Hand-written against CSS 2.1 §10 (VFM Details) |
| Css2Chapter11Tests | `HtmlRenderer.Image.Tests/Css2Chapter11Tests.cs` | 28 | Hand-written against CSS 2.1 §11 (Visual Effects) |
| Css2Chapter12Tests | `HtmlRenderer.Image.Tests/Css2Chapter12Tests.cs` | 78 | Hand-written against CSS 2.1 §12 (Generated Content / Lists) |
| Css2Chapter13Tests | `HtmlRenderer.Image.Tests/Css2Chapter13Tests.cs` | 60 | Hand-written against CSS 2.1 §13 (Paged Media) |
| Css2Chapter14Tests | `HtmlRenderer.Image.Tests/Css2Chapter14Tests.cs` | 94 | Hand-written against CSS 2.1 §14 (Colors and Backgrounds) |
| Css2Chapter15Tests | `HtmlRenderer.Image.Tests/Css2Chapter15Tests.cs` | 65 | Hand-written against CSS 2.1 §15 (Fonts) |
| Css2Chapter16Tests | `HtmlRenderer.Image.Tests/Css2Chapter16Tests.cs` | 72 | Hand-written against CSS 2.1 §16 (Text) |
| Css2Chapter17Tests | `HtmlRenderer.Image.Tests/Css2Chapter17Tests.cs` | 95 | Hand-written against CSS 2.1 §17 (Tables) |
| Css2Chapter18Tests | `HtmlRenderer.Image.Tests/Css2Chapter18Tests.cs` | 63 | Hand-written against CSS 2.1 §18 (User Interface) |

**Total CSS2 chapter tests: 1161**

### 2.2 CSS2 Differential Verification (Chromium Comparison)

| Suite | Location | Tests | Provenance |
|-------|----------|-------|------------|
| Css2DifferentialVerificationTests | `HtmlRenderer.Image.Tests/Css2DifferentialVerificationTests.cs` | 343 | Pixel-by-pixel comparison of html-renderer vs Chromium (Playwright) |
| Css2TestSnippets | `HtmlRenderer.Image.Tests/Css2TestSnippets.cs` | 393 snippets | Shared HTML snippets for differential testing |

The differential verification covers chapters **6, 9, 10, 12, 13, 15, 16,
and 17** using shared test snippets rendered in both engines.

### 2.3 Acid Tests (External Standard)

| Suite | Location | Tests | Provenance |
|-------|----------|-------|------------|
| Acid1CaptureTests | `Broiler.Cli.Tests/Acid1CaptureTests.cs` | 34 | W3C Acid1 (CSS1 conformance) — capture verification |
| Acid1ProgrammaticTests | `Broiler.Cli.Tests/Acid1ProgrammaticTests.cs` | 49 | W3C Acid1 — programmatic layout assertions |
| Acid1SplitTests | `Broiler.Cli.Tests/Acid1SplitTests.cs` | 25 | Acid1 split into 10 isolated CSS1 feature sections |
| Acid1DifferentialTests | `HtmlRenderer.Image.Tests/Acid1DifferentialTests.cs` | 24 | Acid1 pixel-diff comparison vs Chromium |
| Acid2NavigationTests | `Broiler.Cli.Tests/Acid2NavigationTests.cs` | 13 | W3C Acid2 — link navigation and basic rendering |

### 2.4 CSS Unit Tests (Parsing / Logic)

| Suite | Location | Tests | Scope |
|-------|----------|-------|-------|
| CssSelectorTests | `Broiler.App.Tests/` | 13 | Specificity, combinators, pseudo-classes |
| CssBoxModelTests | `Broiler.App.Tests/` | 16 | Display, position, layout tree |
| CssAnimationsTests | `Broiler.App.Tests/` | 12 | Transition parsing, timing functions |
| CssGridFlexTests | `Broiler.App.Tests/` | 10 | Flex/grid display resolution |
| CssTextPropertiesTests | `Broiler.App.Tests/` | varies | White-space, word-break, text-overflow |

---

## 3. Chromium/Playwright Comparison Status

### 3.1 Chapters With Pixel-Level Chromium Comparison

The following chapters have test snippets in `Css2TestSnippets.cs` that are
rendered by both html-renderer and headless Chromium, then compared
pixel-by-pixel via `DifferentialTestRunner`:

| Chapter | Snippet Count | Pass (≤ 5%) | Fail (> 5%) | Notes |
|---------|---------------|-------------|-------------|-------|
| 6 | 25 | — | — | Snippets defined; differential report pending |
| 9 | 50 | 14 | 36 | Visual Formatting Model — most failures from UA stylesheet |
| 10 | 135 | 53 | 82 | VFM Details — most failures from UA stylesheet |
| 12 | 20 | 20 | 0 | Generated Content — all pass |
| 13 | 25 | — | — | Paged Media — snippets defined; report pending |
| 15 | 20 | 20 | 0 | Fonts — all pass |
| 16 | 23 | 23 | 0 | Text — all pass |
| 17 | 95 | 87 | 8 | Tables — strong agreement |

### 3.2 Chapters Without Chromium Comparison

The following chapters have dedicated test suites that verify html-renderer
behaviour in isolation (layout assertions, property parsing, expected output)
but do **not** compare against Chromium pixel output:

| Chapter | Title | Tests | Reason |
|---------|-------|-------|--------|
| 1 | About the CSS 2.1 Specification | 31 | Informative — no rendering implications |
| 2 | Introduction to CSS 2.1 | 35 | Informative — processing model concepts |
| 3 | Conformance | 30 | Definitions and conformance requirements |
| 4 | Syntax and Basic Data Types | 65 | Parsing-level tests — no visual output |
| 5 | Selectors | 66 | Selector matching — verified by style application |
| 7 | Media Types | 43 | @media rule parsing — no visual diff needed |
| 8 | Box Model | 81 | Layout-level assertions — no snippets yet |
| 11 | Visual Effects | 28 | Overflow/clip/visibility — no snippets yet |
| 13 | Paged Media | 60 | Print-oriented — snippets defined but not yet diffed |
| 14 | Colors and Backgrounds | 94 | Layout-level — no snippets yet |
| 18 | User Interface | 63 | Cursor/outline/system colors — no snippets yet |

### 3.3 Acid Test Chromium Comparison

| Test | Chromium Comparison | Pixel Diff |
|------|---------------------|------------|
| Acid1 | ✅ Yes (Playwright) | < 12% |
| Acid2 | ❌ Navigation only | N/A — navigation and link-following tested |

---

## 4. Checklist Coverage Summary

Each CSS 2.1 chapter has a detailed checklist in `css2/chapter-N-checklist.md`.
The table below summarises verification status:

| Chapter | Title | Checked | Unchecked | Coverage |
|---------|-------|---------|-----------|----------|
| 1 | About the Specification | 21 | 0 | 100% |
| 2 | Introduction | 23 | 0 | 100% |
| 3 | Conformance | 30 | 0 | 100% |
| 4 | Syntax and Data Types | 83 | 0 | 100% |
| 5 | Selectors | 68 | 0 | 100% |
| 6 | Cascading and Inheritance | 50 | 0 | 100% |
| 7 | Media Types | 29 | 0 | 100% |
| 8 | Box Model | 81 | 0 | 100% |
| 9 | Visual Formatting Model | 101 | 7 | 93.5% |
| 10 | Visual Formatting Model Details | 101 | 0 | 100% |
| 11 | Visual Effects | 29 | 0 | 100% |
| 12 | Generated Content / Lists | 64 | 0 | 100% |
| 13 | Paged Media | 48 | 0 | 100% |
| 14 | Colors and Backgrounds | 44 | 0 | 100% |
| 15 | Fonts | 66 | 0 | 100% |
| 16 | Text | 65 | 0 | 100% |
| 17 | Tables | 93 | 0 | 100% |
| 18 | User Interface | 78 | 0 | 100% |
| A | Aural Style Sheets | 0 | 62 | 0% |
| D | Default HTML 4 Stylesheet | 0 | 54 | 0% |
| E | Stacking Contexts | 0 | 24 | 0% |

### 4.1 Chapter 9 — Unchecked Items (7)

1. **`display: run-in`** (4 items) — Removed from the CSS specification;
   intentionally omitted. This display type was dropped and is not supported
   by modern browsers.
2. **`unicode-bidi`** (3 items: `normal`, `embed`, `bidi-override`) — Not
   implemented. Only basic `direction: ltr | rtl` is supported.

### 4.2 Appendix Coverage

- **Appendix A (Aural):** 0/62 items. Aural/speech stylesheets are not
  applicable to a visual rendering engine.
- **Appendix D (Default Stylesheet):** 0/54 items. Informative appendix;
  the html-renderer implements its own UA stylesheet in `CssDefaults.cs`.
- **Appendix E (Stacking Contexts):** 0/24 items. Informative appendix
  elaborating on the z-index painting order described in Chapter 9.

---

## 5. Observed Rendering Issues

### 5.1 Differential Verification Results Summary

| Severity | Count | Description |
|----------|-------|-------------|
| Identical (0%) | 6 | Pixel-perfect match between engines |
| Low (< 5%) | 148 | Font rasterisation / anti-aliasing differences |
| Medium (5–10%) | 5 | Moderate rendering differences |
| High (10–20%) | 2 | Significant rendering differences |
| Critical (≥ 20%) | 119 | Major layout or rendering differences |

### 5.2 Root Cause #1 — User-Agent Stylesheet Differences (119 Critical)

**CSS2 spec sections:** §14.2 (Background propagation), §8 (Box Model —
body margin), Appendix D (Default stylesheet)

**Description:** The majority of "Critical" failures (119 tests) are caused
by differences in how the two engines handle HTML fragments without explicit
`<html>` and `<body>` wrapper elements:

- Chromium's HTML5 parser implicitly wraps fragments in `<html><body>` and
  applies `body { margin: 8px }` from its UA stylesheet.
- The html-renderer parses fragments as-is. The `body { margin: 8px }` rule
  in `CssDefaults.cs` only applies when `<body>` is explicitly present.

**Impact:** Block-only test snippets (coloured `<div>` elements) show
near-total pixel differences (~93–99%) because the elements are positioned
8px differently.

**Reproduction:** Any test snippet containing only block-level elements
without `<html>/<body>` wrappers, e.g.:
```html
<div style="width:100px; height:50px; background:red;"></div>
```

**Status:** Known limitation. CSS 2.1 §14.2 background propagation has been
fixed in `PaintWalker.cs` (`FindCanvasBackground()` / `EmitCanvasBackground()`).
The remaining difference is the implicit wrapping behaviour which is HTML5
parser-level, not CSS-level.

### 5.3 Root Cause #2 — Table Layer Background Rendering (3 Critical)

**CSS2 spec sections:** §17.5.1 (Table layers)

**Affected tests:**
- `S17_5_1_Layer1_TableBackground` — 98.42% diff
- `S17_5_1_Layer5_RowBackground` — 100.00% diff
- `S17_5_1_Layer6_CellBackground` — 99.03% diff

**Description:** CSS 2.1 §17.5.1 defines a six-layer painting model for
tables. Three tests that render table backgrounds in isolation (without text
content) show critical differences due to the UA stylesheet body margin
issue described in §5.2.

**Status:** The six-layer painting model has been implemented in
`PaintWalker.PaintTableChildren()`. Table background rendering is correct
when text content is present (verified by passing tests in the same chapter).

### 5.4 Root Cause #3 — Float/Block Overlap (6+ Tests)

**CSS2 spec sections:** §9.5.1 (Float placement rules 1–9), §9.5.2 (Clear)

**Affected tests (with overlap warnings):**
- `S9_5_1_ContentFlowsAroundFloat` — 1 overlap
- `S9_8_ComparisonExample_AllPositioningSchemes` — 2 overlaps
- `S10_3_5_Golden_FloatShrinkToFit` — 1 overlap
- `S10_6_6_InlineBlock_AutoHeightIncludesFloats` — 1 overlap
- `S10_6_6_Golden_OverflowHiddenWithFloat` — 1 overlap
- `S10_6_7_BFCRoot_*` — 3 tests with 1 overlap each

**Description:** The html-renderer's float placement algorithm occasionally
produces float/block overlap situations that Chromium avoids. The overlap
detection system flags these as potential CSS 2.1 §9.5.1 rule violations.

**Prior fix:** CSS 2.1 §9.5.1 rule 6 enforcement was added in `CssBox.cs`
(lines 339–345) using `CollectPrecedingFloatsInBfc()` + top constraint.

**Status:** Monitored. Remaining overlaps require per-test diagnosis of the
specific float/block interaction.

### 5.5 Root Cause #4 — Table Height Distribution (2 High)

**CSS2 spec sections:** §17.5.3 (Row height algorithm)

**Affected tests:**
- `S17_5_3_PercentageHeight` — 12.54% diff
- `S17_5_3_ExtraHeightDistributed` — 18.77% diff

**Description:** When a cell spans multiple rows or a table has percentage-
based row heights, the excess height distribution algorithm in html-renderer
differs from Chromium's implementation. This is a known area of ambiguity
in the CSS 2.1 specification.

**Status:** Requires investigation of `CssTable.cs` height distribution code.

### 5.6 Root Cause #5 — Medium-Severity Rendering Differences (5 Tests)

**CSS2 spec sections:** §9.7, §10.8.2, §17 (mixed)

| Test | Diff | Section |
|------|------|---------|
| `S9_7_FloatAdjustsDisplay` | 6.72% | §9.7 display/position/float relationship |
| `S10_8_2_VerticalAlign_TableCell` | 6.55% | §10.8.2 vertical-align on table cells |
| `S17_Integration_MixedHtmlCssTable` | 6.21% | §17 mixed HTML/CSS table layouts |
| `S17_Integration_Golden_ComplexTable` | 5.78% | §17 complex multi-feature table |
| `S17_5_3_MinimumRowHeight` | 5.01% | §17.5.3 minimum row height enforcement |

**Status:** Requires per-test investigation.

### 5.7 Root Cause #6 — Font Rasterisation Differences (148 Low)

**CSS2 spec sections:** §15 (Fonts), §16 (Text)

**Description:** Different font engines (SkiaSharp/FreeType vs Chromium's
HarfBuzz/Skia) produce sub-pixel differences in glyph rendering, anti-
aliasing, and hinting. All 148 Low-severity tests show < 5% pixel
difference.

**Impact:** Not a rendering bug. Expected cross-engine variation.

**Status:** Monitored for regression only.

---

## 6. Issue–Specification Mapping

### 6.1 Rendering Issues by CSS2 Section

| CSS2 Section | Title | Issue Count | Severity Range | Root Cause |
|--------------|-------|-------------|----------------|------------|
| §8 | Box Model | 0 (indirect) | — | UA stylesheet body margin |
| §9.2 | Block/Inline Boxes | 4 Critical | Critical | UA stylesheet |
| §9.3 | Positioning Schemes | 5 Critical | Critical | UA stylesheet |
| §9.4 | Normal Flow / BFC | 5 Critical | Critical | UA stylesheet + layout |
| §9.5 | Floats | 11 Critical, 1 Medium | Critical–Medium | UA stylesheet + float overlap |
| §9.7 | display/position/float | 1 Medium | Medium | Display adjustment |
| §9.9 | Stacking / z-index | 1 Critical | Critical | UA stylesheet |
| §10.1 | Containing Block | 8 Critical | Critical | UA stylesheet |
| §10.2 | Content Width | 7 Critical | Critical | UA stylesheet |
| §10.3 | Width Algorithms | 28 Critical | Critical | UA stylesheet |
| §10.4 | Min/Max Width | 8 Critical | Critical | UA stylesheet |
| §10.5 | Height | 7 Critical | Critical | UA stylesheet |
| §10.6 | Height Algorithms | 18 Critical | Critical | UA stylesheet + float overlap |
| §10.7 | Min/Max Height | 6 Critical | Critical | UA stylesheet |
| §10.8 | Line Height / Vertical Align | 1 Critical, 1 Medium | Critical–Medium | UA stylesheet + table cell |
| §14.2 | Background Propagation | — | — | Fixed (PaintWalker) |
| §17.5.1 | Table Layers | 3 Critical | Critical | UA stylesheet |
| §17.5.3 | Row Height | 2 High, 1 Medium | High–Medium | Height distribution algorithm |
| §17 integration | Mixed Table Layouts | 2 Medium | Medium | Various |

### 6.2 Acid Test Compliance

| Test | Standard | Result | Key Issues |
|------|----------|--------|------------|
| Acid1 | CSS1 | < 12% pixel diff vs Chromium | Border corner seam rendering |
| Acid2 | CSS2.1 | Navigation working | Full rendering comparison pending |

---

## 7. Gaps in Test Coverage

### 7.1 Chapters Not Yet in Differential (Chromium) Comparison

The following chapters have test suites with html-renderer-only assertions
but no test snippets in `Css2TestSnippets.cs` for cross-engine comparison:

| Priority | Chapter | Title | Reason |
|----------|---------|-------|--------|
| P1 | 8 | Box Model | High-value visual chapter — margins, padding, borders |
| P2 | 11 | Visual Effects | overflow, clip, visibility — directly visual |
| P3 | 14 | Colors and Backgrounds | Colors, background-image, background-position |
| P4 | 18 | User Interface | Outlines, cursor — partially visual |
| Low | 1–3 | Spec Introduction / Conformance | Informative — no visual output |
| Low | 4 | Syntax | Parsing — no visual output |
| Low | 5 | Selectors | Style matching — tested via application |
| Low | 7 | Media Types | @media parsing — no visual diff needed |

### 7.2 Specific Subsection Gaps

| Section | Gap | Impact |
|---------|-----|--------|
| §9.2.3 `display: run-in` | Not implemented (removed from CSS3) | None — deprecated |
| §9.10 `unicode-bidi` | Only basic `direction` supported | Low — BiDi edge cases |
| §13 Paged Media | Snippets defined but not yet diffed | Low — print-only |
| Appendix A (Aural) | Not applicable to visual rendering | None |
| Appendix D (Default Stylesheet) | Informative — not testable | None |
| Appendix E (Stacking Contexts) | Informative — covered by §9.9 tests | None |

### 7.3 Missing External Test Suites

| Test Suite | Status | Notes |
|------------|--------|-------|
| W3C CSS2.1 Test Suite (test.csswg.org) | Not integrated | The W3C maintains an official CSS2.1 conformance test suite with ~9000+ tests. Integration would provide the most comprehensive coverage. |
| Web Platform Tests (WPT) | Not integrated | WPT includes CSS2 tests that modern browsers use for conformance. |
| Acid3 | Not tested | Tests CSS2/3 and DOM features |

---

## 8. Recommendations

### 8.1 High Priority

1. **Add differential snippets for Chapter 8 (Box Model):** This is the
   most visually impactful chapter without Chromium comparison. Test
   snippets for margins, padding, borders, and margin collapsing should
   be added to `Css2TestSnippets.cs`.

2. **Add differential snippets for Chapter 11 (Visual Effects):** Overflow,
   clip, and visibility have direct visual impact and should be compared
   against Chromium.

3. **Add differential snippets for Chapter 14 (Colors/Backgrounds):**
   Background colours, images, and positioning are highly visible features.

4. **Investigate table height distribution (§17.5.3):** The 2 High-severity
   failures in table row height calculation should be root-caused and fixed.

### 8.2 Medium Priority

5. **Investigate the 5 Medium-severity failures:** Each requires individual
   diagnosis:
   - `S9_7_FloatAdjustsDisplay` — float/display interaction
   - `S10_8_2_VerticalAlign_TableCell` — vertical-align in table cells
   - `S17_Integration_MixedHtmlCssTable` — mixed table layout
   - `S17_Integration_Golden_ComplexTable` — complex table
   - `S17_5_3_MinimumRowHeight` — minimum row height

6. **Address float overlap warnings:** The 6+ tests with float/block
   overlaps should be investigated to determine if they represent genuine
   CSS 2.1 §9.5.1 rule violations or false positives from the overlap
   detection algorithm.

7. **Run differential verification for Chapter 6 and 13 snippets:** These
   chapters have snippets defined in `Css2TestSnippets.cs` but the
   differential report data is pending.

### 8.3 Low Priority

8. **Consider integrating the W3C CSS2.1 Test Suite:** The official W3C
   test suite would provide the broadest conformance coverage. However,
   the test format (ref tests with reference images) requires adaptation
   to the html-renderer's rendering pipeline.

9. **Implement `unicode-bidi` support:** The 3 unchecked items in Chapter 9
   relate to bidirectional text embedding. This is a niche feature but
   part of the CSS 2.1 specification.

10. **Complete Acid2 visual comparison:** Currently only navigation is
    tested. A pixel-level comparison against Chromium would validate
    broader CSS2.1 compliance.

---

## 9. Test Commands

```bash
# Build the solution
dotnet build Broiler.slnx

# Run all tests (excluding differential report generation)
dotnet test Broiler.slnx --filter "Category!=Differential&Category!=DifferentialReport"

# Run CSS2 chapter tests only
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/ \
  --filter "FullyQualifiedName~Css2Chapter"

# Run differential verification (requires Playwright/Chromium)
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/ \
  --filter "FullyQualifiedName~Css2DifferentialVerificationTests"

# Run Acid1 tests
dotnet test src/Broiler.Cli.Tests/ \
  --filter "FullyQualifiedName~Acid1"

# Run engine smoke test
dotnet run --project src/Broiler.Cli -- --test-engines
```

---

## 10. Related Documents

| Document | Path |
|----------|------|
| CSS2 Specification Checklist | `css2/css2-specification-checklist.md` |
| Per-Chapter Checklists | `css2/chapter-N-checklist.md` (N = 1–18) |
| Differential Verification Results | `docs/css2-differential-verification.md` |
| Differential Resolution Tracker | `docs/css2-differential-resolution.md` |
| Acid1 Testing Documentation | `docs/acid1-testing.md` |
| Testing Guide | `docs/testing-guide.md` |
| Testing Roadmap | `docs/testing-roadmap.md` |
| Testing Current State | `docs/testing-current-state.md` |
