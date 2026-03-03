# Testing Current State – Audit

> Phase 0 deliverable for the [Automated Multi-Layer Test Suite Roadmap](testing-roadmap.md).
>
> **Date:** 2026-03-03 (updated)  
> **Previous audit:** 2026-02-15  
> **Engine version:** html-renderer (Broiler) — post-CSS2 differential resolution  
> **Scope:** Point-in-time audit of test coverage and gaps. For the
> practical "how to run tests" guide see [Testing Guide](testing-guide.md).
> For the target architecture see
> [Testing Architecture](testing-architecture.md). For the implementation
> plan see [Testing Roadmap](testing-roadmap.md).
>
> ⚠️ Test counts and coverage gaps in this document reflect the state at the
> date above and may be outdated. Consult the test projects directly for
> current counts.

---

## Overview

This document audits the current testing state of the Broiler rendering engine,
identifying what exists, what can be dumped/inspected at each IR layer, and where
the biggest blind spots are.

---

## 1. What Tests Currently Exist

### Test Projects

| Project | Framework | Test Count (approx.) | Focus |
|---------|-----------|---------------------|-------|
| `Broiler.App.Tests` | xUnit | ~298 | Unit / integration (CSS, DOM, layout, rendering pipeline, cross-feature rendering) |
| `Broiler.Cli.Tests` | xUnit | ~240 | CLI output, image capture, Acid1/Acid2, W3C compliance, Heise.de live capture |
| `HtmlRenderer.Image.Tests` | xUnit | ~1474 | CSS2 chapter compliance, pixel regression, golden layout/paint, analytics, differential |

### Test Categories

#### Unit Tests (`Category=Unit`)

**`Broiler.App.Tests`** (~248 tests):

- **CSS parsing & box model** (`CssBoxModelTests`, 16 tests) – padding/border/margin
  rect calculation, float left/right positioning, percentage widths, explicit
  heights, block stacking order.
- **CSS selectors** (`CssSelectorTests`, 13 tests) – specificity, combinators.
- **CSS text** (`CssTextPropertiesTests`, 16 tests) – font resolution, text
  properties.
- **CSS animations** (`CssAnimationsTests`, 12 tests) – keyframes, transitions.
- **CSS grid/flex** (`CssGridFlexTests`, 10 tests) – flex direction, grid areas.
- **HTML tokenizer** (`HtmlTokenizerTests`, 13 tests) – tag parsing, attributes.
- **HTML tree builder** (`HtmlTreeBuilderTests`, 9 tests) – DOM construction.
- **DOM bridge** (`DomBridgeTests` + `Milestone3DomBridgeTests` +
  `Milestone4RuntimeTests`, ~136 tests) – JavaScript ↔ DOM interop.
- **Script engine** (`ScriptEngineTests`, 6 tests) – YantraJS evaluation.
- **Script extractor** (`ScriptExtractorTests`, 6 tests) – `<script>` tag
  extraction.
- **DOM events** (`DomEventsTests`, 7 tests) – event propagation.
- **Form elements** (`FormElementTests`, 10 tests) – input, select, textarea.
- **Image pipeline** (`ImagePipelineTests`, 14 tests) – image loading, decoding.

**`HtmlRenderer.Image.Tests`** (~172 tests):

- **Primitives** (`PrimitivesTests`, 38 tests) – RRect, RPoint, RSize, Color.
- **CSS length** (`CssLengthTests`, 24 tests) – px, em, rem, %, unit conversion.
- **SubString** (`SubStringTests`, 15 tests) – lightweight string wrapper.
- **Common utilities** (`CommonUtilsTests`, 8 tests) – URI handling.
- **IR types** (`IRTypesTests`, 74 tests) – ComputedStyle, Fragment,
  DisplayList, PaintWalker, FragmentTreeBuilder.
- **Orchestration** (`OrchestrationModuleTests`, 6 tests) – module wiring.
- **HtmlContainer** (`HtmlContainerTests`, 2 tests) – container integration.
- **Css2 test snippets** (`Css2TestSnippetsTests`, 8 tests) – snippet body wrapping.

**`Broiler.Cli.Tests`** (~57 tests):

- **Program/CLI** (`ProgramTests`, 37 tests) – CLI argument parsing, options.
- **Engine tests** (`EngineTestServiceTests`, 3 tests) – engine smoke tests.
- **Window stub** (`WindowStubTests`, 8 tests) – stub window tests.
- **Render logger** (`RenderLoggerTests`, 9 tests) – logging tests.

#### Rendering Tests (`Category=Rendering`)

**`Broiler.App.Tests`** (~19 tests):

- **Rendering output** (`RenderingOutputTests`, 10 tests) – render HTML through
  full pipeline, verify output properties.
- **Cross-feature rendering** (`CrossFeatureRenderingTests`, 9 tests) – CSS
  feature interactions (floats + positioning, tables + overflow).

**`HtmlRenderer.Image.Tests`** (~81 tests):

- **Rendering analytics** (`RenderingAnalyticsTests`, 10 tests) – performance,
  dimensions, pixel coverage, format quality, consistency.
- **Pixel regression** (`PixelRegressionTests`, 15 tests) – baseline image
  comparison with diff-image generation and failure classification.
- **Font regression** (`FontRegressionBaselineTests`, 4 tests) – determinism
  gate and cross-engine font regression.
- **Golden layout** (`GoldenLayoutTests`, 10 tests) – Fragment tree JSON
  snapshot comparison.
- **Golden display list** (`GoldenDisplayListTests`, 10 tests) – DisplayList
  JSON snapshot comparison.
- **Image comparer** (`ImageComparerTests`, 17 tests) – image comparison
  utilities.
- **Cross-chapter CSS2** (`CrossChapterCss2InteractionTests`, 8 tests) –
  cross-feature CSS2 rendering (positioning + dimensions + overflow).
- **Real-world snippets** (`RealWorldSnippetRenderingTests`, 6 tests) –
  layout patterns from real websites.
- **Render helpers** (`RenderToImageTests` 9, `RenderToPngTests` 3,
  `RenderToJpegTests` 2, `RenderToFileTests` 2) – render output format tests.

#### IR / Rendering Stage Tests

- **Rendering stages** (`RenderingStagesTests`, 6 tests) – PaintWalker,
  DisplayList generation, paint invariant checks.
- **Rendering pipeline** (`RenderingPipelineTests`, 3 tests) – script execution
  through the pipeline.
- **Page content** (`PageContentTests`, 2 tests) – page content extraction.

#### Compliance Tests (`Category=Compliance`)

**`Broiler.Cli.Tests`** (~120 tests):

- **W3C Phase 1** (`W3cPhase1ComplianceTests`, 16 tests) – HTML5 semantic
  elements, void elements, rem units, position: relative, background-size,
  @media rules.
- **W3C Phase 2** (`W3cPhase2ComplianceTests`, 25 tests) – box model, colours,
  text properties, display, tables, specificity, visibility, font-size.
- **Acid1** (`Acid1ProgrammaticTests` 49, `Acid1CaptureTests` 34,
  `Acid1SplitTests` 25 — 108 tests) – CSS1 conformance test with pixel
  analysis, structural validation, and section-level regression.
- **Acid2** (`Acid2NavigationTests`, 13 tests) – Acid2 navigation and link
  extraction tests.

**`HtmlRenderer.Image.Tests`** (~1161 tests across 18 CSS2 chapters):

- **Css2Chapter1Tests** (31) – spec conventions and definitions.
- **Css2Chapter2Tests** (35) – CSS introduction and processing model.
- **Css2Chapter3Tests** (30) – conformance and definitions.
- **Css2Chapter4Tests** (65) – syntax and values.
- **Css2Chapter5Tests** (66) – selectors.
- **Css2Chapter6Tests** (74) – cascading and inheritance.
- **Css2Chapter7Tests** (43) – media types.
- **Css2Chapter8Tests** (81) – box model.
- **Css2Chapter9Tests** (49) – visual formatting model.
- **Css2Chapter10Tests** (132) – visual formatting model details.
- **Css2Chapter11Tests** (28) – visual effects.
- **Css2Chapter12Tests** (78) – generated content and lists.
- **Css2Chapter13Tests** (60) – paged media.
- **Css2Chapter14Tests** (94) – colours and backgrounds.
- **Css2Chapter15Tests** (65) – fonts.
- **Css2Chapter16Tests** (72) – text.
- **Css2Chapter17Tests** (95) – tables.
- **Css2Chapter18Tests** (63) – user interface.

#### Integration Tests (`Category=Integration`)

**`Broiler.Cli.Tests`** (~18 tests):

- **Capture integration** (`CaptureIntegrationTests`, 6 tests) – end-to-end
  CLI capture producing files on disk.
- **CLI output** (`CliOutputValidationTests`, 5 tests) – HTML/PNG/JPEG output
  validation.
- **Image capture** (`ImageCaptureTests`, 4 tests) – image capture format and
  dimension tests.
- **Heise.de** (`HeiseCaptureTests`, 3 tests) – live-site capture with retry
  (3 attempts, 2 s × attempt).

#### Differential Tests (`Category=Differential`)

**`HtmlRenderer.Image.Tests`** (~38 tests):

- **Acid1 differential** (`Acid1DifferentialTests`, 24 tests) – html-renderer
  vs. Chromium pixel comparison for each Acid1 section.
- **Acid2 differential** (`Acid2DifferentialTests`, 3 tests) – Acid2 pixel
  comparison.
- **Differential tests** (`DifferentialTests`, 10 tests) – cross-engine
  structural and layout comparison.
- **CSS2 differential** (`Css2DifferentialVerificationTests`, 1 test) –
  chapter-level differential verification.

#### Fuzz Tests (`Category=Fuzz`)

- **Layout fuzz** (`LayoutFuzzRunner`, 1 test) – generates 100 random
  HTML/CSS documents and checks layout invariants.

---

## 2. What Can Currently Be Dumped

| Artifact | Dump Available? | Format | Notes |
|----------|----------------|--------|-------|
| **ComputedStyle** | ✅ Yes (via JSON) | JSON | Sealed record with init-only props; serialisable via `System.Text.Json`. Used in `IRTypesTests`. |
| **Fragment tree** | ✅ Yes | JSON | `FragmentJsonDumper` serialises the tree to deterministic JSON. Used by `GoldenLayoutTests`. |
| **DisplayList** | ✅ Yes | JSON | `[JsonDerivedType]` annotations + `ToJson()` convenience method. Used by `GoldenDisplayListTests`. |
| **Pixel output** | ✅ Yes | PNG / JPEG | CLI `--capture-image` produces images; `SKBitmap` used in tests for pixel analysis. Pixel regression baselines in `TestData/PixelBaseline/`. |
| **Layout tree (CssBox)** | ❌ No dump | — | Mutable `CssBox` tree is the legacy representation; no serialisation support. |

### Key Observations

- **DisplayList** has full JSON serialisation support (via `System.Text.Json`
  derived-type discriminators and `ToJson()` method).
- **Fragment** tree has deterministic JSON dump via `FragmentJsonDumper`.
- **ComputedStyle** is structurally serialisable (sealed record, value-type
  semantics).
- Golden-file testing infrastructure exists for both layout (`GoldenLayoutTests`)
  and paint (`GoldenDisplayListTests`).
- Pixel regression baselines are stored in `TestData/PixelBaseline/`.

---

## 3. Biggest Blind Spots

### Layout Correctness (Medium Priority — improved since initial audit)

| Area | Coverage | Risk |
|------|----------|------|
| **Float collision / clearance** | ✅ Good — `CssBoxModelTests` (2), `GoldenLayoutTests` (3 float cases), `CrossChapterCss2InteractionTests`, `Css2Chapter9Tests` (49) | Remaining: deeply nested BFC edge cases |
| **Percentage widths** | ✅ Good — `GoldenLayoutTests`, `Css2Chapter10Tests` (132) | Remaining: min/max constraint interactions |
| **Margin collapse** | ⚠️ Basic — `GoldenLayoutTests` (1 case), `Css2Chapter8Tests` (81) | Complex nested margin collapse untested |
| **Inline layout / line breaking** | ✅ Good — `GoldenLayoutTests` (2 cases), `Css2Chapter16Tests` (72) | Remaining: complex bidi, word-break edge cases |
| **Table layout** | ✅ Good — `Css2Chapter17Tests` (95) | Remaining: complex column-width distribution |
| **Block formatting context** | ⚠️ Basic — `GoldenLayoutTests` (1 case), fuzz testing | Complex BFC interactions still limited |

### Paint Correctness (Low Priority — significantly improved)

| Area | Coverage | Risk |
|------|----------|------|
| **Paint order / stacking context** | ✅ Good — `GoldenDisplayListTests` (stacking context ordering), `IRTypesTests` (PaintWalker tests) | Remaining: complex z-index nesting |
| **Border rendering** | ✅ Good — `GoldenDisplayListTests` (border test), `Css2Chapter8Tests` | Remaining: complex border-radius combinations |
| **Background images** | ✅ Basic — `PaintWalker` handles background images, `IRTypesTests` | Remaining: complex background-position/repeat |
| **Text decoration** | ✅ Basic — `GoldenDisplayListTests` (underline test), `PaintWalker` | Remaining: overline/line-through positioning |
| **Clip regions** | ✅ Basic — `GoldenDisplayListTests` (overflow:hidden test), `PaintInvariantChecker` | Remaining: nested clip edge cases |

### Raster / Pixel Correctness (Medium Priority — improved)

| Area | Coverage | Risk |
|------|----------|------|
| **DPI / scaling** | ⚠️ Basic — deterministic render mode exists for pixel regression tests | Remaining: multi-DPI scenarios |
| **Font rendering** | ✅ Basic — `FontRegressionBaselineTests` (4 tests) | Remaining: cross-platform font metrics variance |
| **Anti-aliasing** | ⚠️ Limited — pixel regression uses tolerance thresholds | No deterministic AA normalisation |

### Cross-Cutting Progress (since initial audit)

- ✅ **Golden-file testing** — implemented for both Fragment tree
  (`GoldenLayoutTests`, 10 cases) and DisplayList (`GoldenDisplayListTests`,
  10 cases).
- ✅ **Property-based / generative testing** — `LayoutFuzzRunner` generates
  100 random documents per run; `HtmlCssGenerator` + `DeltaMinimizer`.
- ✅ **Differential testing** — `DifferentialTests` (10 tests) compare
  html-renderer vs. Chromium; `Acid1DifferentialTests` (24 tests);
  `Css2DifferentialVerificationTests` for CSS2 chapters.
- ✅ **Invariant checking** — `FragmentInvariantChecker` and
  `PaintInvariantChecker` detect NaN, Inf, negative geometry, unbalanced clips.
- ✅ **CI pixel regression** — `PixelRegressionTests` (15 tests) compare
  against baseline images with diff-image generation and failure classification.

---

## 4. Summary

| Layer | Test Coverage | Dump Support | Remaining Gaps |
|-------|--------------|-------------|----------------|
| **Style** | Good (CSS parsing, selectors, text, animations, grid/flex) | ✅ JSON | Shorthand expansion edge cases |
| **Layout** | Good (box model, floats, tables, CSS2 chapters 4–10) | ✅ JSON (`FragmentJsonDumper`) | Complex nested margin collapse, deeply nested BFC |
| **Paint** | Good (golden DisplayList, stacking contexts, borders, clip) | ✅ JSON (`ToJson()`) | Complex border-radius combinations |
| **Raster** | Good (pixel regression baselines, font regression, analytics) | ✅ PNG/JPEG + diff images | Cross-platform font variance, no AA normalisation |

---

## Related Documents

- [Testing Architecture](testing-architecture.md) — testable IR boundary definitions
- [Testing Roadmap](testing-roadmap.md) — staged implementation plan
- [Testing Guide](testing-guide.md) — how to run, write, and organise tests
- [Architecture Separation](architecture-separation.md) — current pipeline structure

## Related Tests

| Test File | Project | Validates |
|-----------|---------|-----------|
| `IRTypesTests.cs` | `HtmlRenderer.Image.Tests` | IR type correctness (ComputedStyle, Fragment, DisplayList) |
| `GoldenLayoutTests.cs` | `HtmlRenderer.Image.Tests` | Fragment tree golden-file comparison |
| `GoldenDisplayListTests.cs` | `HtmlRenderer.Image.Tests` | DisplayList golden-file comparison |
| `PixelRegressionTests.cs` | `HtmlRenderer.Image.Tests` | Pixel-level baseline regression |
| `LayoutFuzzRunner.cs` | `HtmlRenderer.Image.Tests` | Property-based layout invariant testing |
| `RenderingOutputTests.cs` | `Broiler.App.Tests` | Full-pipeline rendering output verification |
| `CrossFeatureRenderingTests.cs` | `Broiler.App.Tests` | Cross-feature CSS interaction tests |
