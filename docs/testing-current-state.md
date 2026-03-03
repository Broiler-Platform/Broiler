# Testing Current State – Audit

> Phase 0 deliverable for the [Automated Multi-Layer Test Suite Roadmap](testing-roadmap.md).
>
> **Date:** 2026-02-15  
> **Engine version:** html-renderer (Broiler) — pre-CSS2 differential resolution  
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
| `Broiler.App.Tests` | xUnit | ~220 | Unit / integration (CSS, DOM, layout, rendering pipeline) |
| `Broiler.Cli.Tests` | xUnit | ~193 | CLI output, image capture, ACID1, W3C compliance, Heise.de live capture |

### Test Categories

#### Unit Tests (~150)

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
- **DOM bridge** (`DomBridgeTests` + `Milestone3DomBridgeTests`, ~104 tests) –
  JavaScript ↔ DOM interop.
- **Script engine** (`ScriptEngineTests`, 6 tests) – YantraJS evaluation.
- **Script extractor** (`ScriptExtractorTests`, 6 tests) – `<script>` tag
  extraction.
- **DOM events** (`DomEventsTests`, 7 tests) – event propagation.
- **Form elements** (`FormElementTests`, 10 tests) – input, select, textarea.
- **Image pipeline** (`ImagePipelineTests`, 14 tests) – image loading, decoding.

#### IR / Rendering Stage Tests (~9)

- **Rendering stages** (`RenderingStagesTests`, 6 tests) – Painter command
  generation, Compositor z-index layering, RenderOutput properties,
  PaintCommand defaults.
- **Rendering pipeline** (`RenderingPipelineTests`, 3 tests) – script execution
  through the pipeline.

#### Pixel / Image Analysis Tests (~60)

- **W3C Phase 1 compliance** (`W3cPhase1ComplianceTests`) – renders HTML snippets,
  analyses pixel colours in specific regions using `SKBitmap` predicates
  (`IsRed`, `IsGreen`, `IsBlue`, `CountPixels`, `GetColorBounds`).
- **W3C Phase 2 compliance** (`W3cPhase2ComplianceTests`) – same pattern, extended
  feature coverage.
- **ACID1** (`Acid1ProgrammaticTests`, `Acid1CaptureTests`, `Acid1SplitTests`) –
  full ACID1 test rendered and pixel-analysed for correctness. Similarity
  threshold ≈ 0.43.

#### Live-Site Capture Tests (~10)

- **Heise.de** (`HeiseCaptureTests`) – captures `https://www.heise.de/` as HTML,
  PNG, JPEG. Uses retry with exponential back-off (3 attempts, 2 s × attempt).
- **CLI capture** (`CliOutputValidationTests`, `CaptureIntegrationTests`,
  `ImageCaptureTests`) – end-to-end CLI invocation producing files on disk.

#### CLI / Infrastructure Tests (~15)

- `ProgramTests`, `CaptureOptionsTests`, `ImageCaptureOptionsTests` – CLI
  argument parsing.
- `EngineTestServiceTests`, `WindowStubTests`, `RenderLoggerTests` – engine
  smoke tests and logging.

---

## 2. What Can Currently Be Dumped

| Artifact | Dump Available? | Format | Notes |
|----------|----------------|--------|-------|
| **ComputedStyle** | ❌ No explicit dump | — | Sealed record with init-only props; serialisable via `System.Text.Json` but no `ToJson()` method. |
| **Fragment tree** | ❌ No explicit dump | — | Immutable record hierarchy (`Fragment` → `LineFragment` → `InlineFragment`). No serialiser. |
| **DisplayList** | ✅ Partial | JSON | `[JsonDerivedType]` annotations on all `DisplayItem` subclasses enable polymorphic JSON serialisation. No convenience `ToJson()` wrapper exists. |
| **Pixel output** | ✅ Yes | PNG / JPEG | CLI `--capture-image` produces images; `SKBitmap` used in tests for pixel analysis. |
| **Layout tree (CssBox)** | ❌ No dump | — | Mutable `CssBox` tree is the legacy representation; no serialisation support. |

### Key Observations

- **DisplayList** is the only IR layer with built-in JSON serialisation support
  (via `System.Text.Json` derived-type discriminators).
- **Fragment** and **ComputedStyle** are structurally serialisable (sealed records,
  value-type semantics) but lack convenience dump methods.
- No golden-file / snapshot testing infrastructure exists anywhere.

---

## 3. Biggest Blind Spots

### Layout Correctness (High Priority)

| Area | Coverage | Risk |
|------|----------|------|
| **Float collision / clearance** | 2 basic tests in `CssBoxModelTests` | Complex BFC interactions (nested floats, margin collapse near floats) untested |
| **Percentage widths** | 1 test | Deeply nested percentage resolution, min/max constraints untested |
| **Margin collapse** | 0 dedicated tests | `MarginBottomCollapse()` logic has no direct test coverage |
| **Inline layout / line breaking** | 0 tests | No tests for text wrapping, word-break, white-space handling |
| **Table layout** | 0 tests | Table-cell sizing, border-collapse, column width distribution untested |
| **Block formatting context** | Implicit only | BFC establishment conditions and float containment not explicitly tested |

### Paint Correctness (Medium Priority)

| Area | Coverage | Risk |
|------|----------|------|
| **Paint order / stacking context** | 1 test (Compositor z-index) | No test for CSS `z-index`, `opacity` creating stacking contexts |
| **Border rendering** | 0 paint-level tests | Per-side border styles, radii, collapse not tested at DisplayList level |
| **Background images** | 0 paint-level tests | `BackgroundImageHandle` exists in Fragment but no DisplayList test |
| **Text decoration** | 0 paint-level tests | Underline/overline positioning untested |
| **Clip regions** | 0 tests | `overflow: hidden` clip nesting untested |

### Raster / Pixel Correctness (Medium Priority)

| Area | Coverage | Risk |
|------|----------|------|
| **DPI / scaling** | 0 tests | No deterministic DPR mode; platform-dependent rendering |
| **Font rendering** | 0 regression tests | Font selection/metrics vary by platform |
| **Anti-aliasing** | 0 tests | No AA normalisation for deterministic pixel comparison |

### Cross-Cutting Gaps

- **No golden-file testing** – all assertions are inline numeric/boolean checks;
  no snapshot comparison for layout trees or display lists.
- **No property-based / generative testing** – all test cases are hand-written.
- **No differential testing** – no comparison against a reference browser.
- **No invariant checking** – no automated detection of NaN, Inf, or negative
  geometry values in layout output.
- **No CI pixel regression** – pixel tests analyse colour regions but do not diff
  against baseline images.

---

## 4. Summary

| Layer | Test Coverage | Dump Support | Blind Spots |
|-------|--------------|-------------|-------------|
| **Style** | Good (CSS parsing, selectors, text) | ❌ | Animation, shorthand expansion edge cases |
| **Layout** | Basic (box model, simple floats) | ❌ | Margin collapse, inline layout, tables, BFC |
| **Paint** | Minimal (z-index layering only) | ✅ Partial (JSON) | Border, background, clip, text decoration |
| **Raster** | Pixel analysis (colour predicates) | ✅ (PNG/JPEG) | DPI, fonts, AA, no baseline diffing |

---

## Related Documents

- [Testing Architecture](testing-architecture.md) — testable IR boundary definitions
- [Testing Roadmap](testing-roadmap.md) — staged implementation plan
- [Testing Guide](testing-guide.md) — how to run, write, and organise tests
- [Architecture Separation](architecture-separation.md) — current pipeline structure
