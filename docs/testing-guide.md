# HTML-Renderer Testsuite Guide

## Overview

This document describes the comprehensive testsuite for Broiler's
HTML-Renderer components. The suite is organized into four categories:

1. **Unit Tests** — Verify individual functions and primitive types.
2. **W3C Compliance Tests** — Validate HTML/CSS rendering against web standards.
3. **Rendering Analytics Tests** — Measure performance, output quality, and
   consistency.
4. **CLI Output Validation Tests** — Verify CLI capture pipeline output
   formats.

## Test Projects

| Project | Location | Focus |
|---------|----------|-------|
| `HtmlRenderer.Image.Tests` | `HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/` | Unit tests, rendering, analytics |
| `Broiler.Cli.Tests` | `src/Broiler.Cli.Tests/` | W3C compliance, CLI validation, Acid1, integration |

## Running Tests

### Full Suite

```bash
dotnet test Broiler.slnx
```

### By Project

```bash
# HtmlRenderer unit & rendering tests (cross-platform)
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/

# CLI and W3C compliance tests (cross-platform)
dotnet test src/Broiler.Cli.Tests/
```

### By Category

```bash
# Unit tests only (primitives, CSS parsing, utilities)
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/ \
  --filter "FullyQualifiedName~PrimitivesTests|FullyQualifiedName~CssLengthTests|FullyQualifiedName~SubStringTests"

# W3C compliance tests only
dotnet test src/Broiler.Cli.Tests/ \
  --filter "FullyQualifiedName~W3cPhase1ComplianceTests|FullyQualifiedName~W3cPhase2ComplianceTests"

# Rendering analytics
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/ \
  --filter "FullyQualifiedName~RenderingAnalyticsTests"

# CLI output validation
dotnet test src/Broiler.Cli.Tests/ \
  --filter "FullyQualifiedName~CliOutputValidationTests"

# Image comparison tests
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/ \
  --filter "FullyQualifiedName~ImageComparerTests"

# Acid1 CSS1 conformance tests (all 3 test classes)
dotnet test src/Broiler.Cli.Tests/ \
  --filter "FullyQualifiedName~Acid1"
```

> **Acid1 testing:** For comprehensive documentation on running and
> understanding the Acid1 test suite, see [acid1-testing.md](acid1-testing.md).

## Test Categories

### 1. Unit Tests

#### CssLengthTests (23 tests)
Tests the CSS length value parser (`CssLength`) covering:
- Pixel, em, rem, percentage values
- Absolute units: pt, cm, mm, in, pc, ex
- Em-to-points and em-to-pixels conversion
- Edge cases: zero, empty, null, bare numbers, invalid units
- ToString round-trip formatting

#### PrimitivesTests (30 tests)
Tests for primitive rendering types:
- **System.Drawing.Color**: ARGB construction, channel extraction, equality, predefined
  colors, boundary validation, ToString formatting
- **RRect**: Construction, derived properties (Left/Top/Right/Bottom),
  Contains, Intersect, Union, Inflate, Offset, FromLTRB, equality
- **RPoint**: Construction, Add/Subtract with RSize, IsEmpty, equality
- **RSize**: Construction, Add/Subtract, copy constructor, ToPointF

#### SubStringTests (17 tests)
Tests for the lightweight `SubString` wrapper:
- Full string and range construction
- Character indexing and boundary checking
- IsEmpty, IsWhitespace, IsEmptyOrWhitespace predicates
- CutSubstring and Substring extraction
- Error handling for invalid arguments

#### CommonUtilsTests (9 tests — pre-existing)
URI handling and utility function tests.

### 2. W3C Compliance Tests

#### W3cPhase1ComplianceTests (16 tests — pre-existing)
Covers Phase 1 HTML5/CSS features:
- HTML5 semantic elements default display values
- Void elements (embed, source, wbr)
- `rem` CSS unit resolution
- `position: relative` offsets
- `background-size` property acceptance
- `@media screen` rule application

#### W3cPhase2ComplianceTests (24 tests)
Covers Phase 2 CSS specifications:
- **Box Model** (3 tests): margin spacing, padding expansion, border
  rendering
- **CSS Colors** (3 tests): named colors, hex values, rgb() notation
- **Text Properties** (4 tests): font-weight, font-style, text-decoration,
  text-align
- **Display Values** (3 tests): display:none, inline, inline-block
- **Tables** (3 tests): basic rendering, border-collapse, th vs td
- **Specificity & Cascade** (3 tests): inline > class, ID > class, later
  rules > earlier
- **Visibility** (1 test): visibility:hidden preserves layout space
- **Font-size Keywords** (1 test): small vs large produce different sizes
- **Multiple Classes** (1 test): both classes applied to element
- **CSS Value Parsing** (1 test): rgb() in border-color shorthand
- **Media Queries** (1 test): @media print vs @media screen coexistence

### 3. Acid1 CSS1 Conformance Tests (77 tests)

Tests validating the rendering engine against the W3C CSS1 Acid1 test
(`acid/acid1/acid1.html`). Organized into three test classes:

- **Acid1CaptureTests** (30 tests): Visual regression, structural validation,
  image format checks, and similarity scoring against the reference image.
- **Acid1ProgrammaticTests** (25 tests): Layout, float positioning, margin
  collapsing, percentage widths, clear behaviour, and border-box computation.
- **Acid1SplitTests** (22 tests): Isolated section tests (10 sections)
  targeting individual CSS1 features for precise regression diagnostics.

Run all Acid1 tests:

```bash
dotnet test src/Broiler.Cli.Tests/ --filter "FullyQualifiedName~Acid1"
```

See [acid1-testing.md](acid1-testing.md) for full details.

### 4. Rendering Analytics Tests (11 tests)

- **Performance**: Simple and large document render timing
- **Dimensions**: Auto-sized rendering respects maxWidth, wider content
  produces wider output
- **Pixel Coverage**: Colored elements produce significant non-white
  coverage, empty HTML is mostly white
- **Format Quality**: PNG preserves exact pixel data, JPEG quality affects
  file size
- **Consistency**: Same HTML produces identical output, different HTML
  produces different output

### 5. CLI Output Validation Tests (6 tests)

- HTML capture preserves title and special characters
- PNG capture produces valid PNG with correct magic bytes
- JPEG capture produces valid JPEG with correct magic bytes
- Larger dimensions produce larger image files

## CI Pipeline

All tests run automatically on every push to `main` and on every pull
request. The CI workflow (`.github/workflows/build.yml`) includes:

1. Build the full solution
2. Run all tests (unit, W3C, analytics, CLI)
3. Image capture test (captures heise.de as PNG)
4. Engine smoke test
5. Website HTML capture
6. Upload capture artifacts

## Known Limitations

- Border colors rendered through `solid` style may be adjusted by
  HTML-Renderer's 3D border styling, so border tests use non-white pixel
  detection rather than exact color matching.

## Phase 3 — Property-Based / Generative Layout Testing

Phase 3 introduces fuzz testing for the layout engine. Random HTML/CSS
documents are generated, laid out, and checked against structural
invariants. Failures are automatically saved for investigation and
optionally minimized via delta reduction.

### Key Classes

| Class | Location | Purpose |
|-------|----------|---------|
| `HtmlCssGenerator` | `HtmlRenderer.Core/Core/IR/` | Generates random but well-formed HTML with inline CSS styles targeting layout stress parameters (floats, clears, widths, heights, padding, margin, border, display, nesting 1–4 levels, 1–6 children per parent) |
| `FragmentInvariantChecker` | `HtmlRenderer.Core/Core/IR/` | Pure-C# invariant checker (no test-framework dependency). Validates Fragment trees for NaN/Infinity, negative dimensions, vertical ordering, etc. |
| `DeltaMinimizer` | `HtmlRenderer.Core/Core/IR/` | Reduces failing HTML to a minimal reproduction by removing child elements one at a time |
| `LayoutFuzzRunner` | `HtmlRenderer.Image.Tests/` | xUnit test class that generates 100 random documents, checks invariants, saves failures |
| `LayoutFuzzService` | `Broiler.Cli/` | CLI service for manual fuzz runs with configurable count and output directory |

### Running Fuzz Tests

#### Via xUnit (100 cases, CI-safe)

```bash
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/ \
  --filter "FullyQualifiedName~LayoutFuzzRunner"
```

#### Via CLI (configurable count)

```bash
# Default 1000 cases
dotnet run --project src/Broiler.Cli -- --fuzz-layout

# Custom count with output directory
dotnet run --project src/Broiler.Cli -- --fuzz-layout --count 5000 --output ./fuzz-results
```

### Failure Output

When invariant violations are found, four files are saved per failure:

- `fuzz_seed_{N}.html` — the generated HTML document
- `fuzz_seed_{N}_minimized.html` — delta-reduced minimal reproduction
- `fuzz_seed_{N}.json` — Fragment tree JSON dump
- `fuzz_seed_{N}_violations.txt` — list of invariant violations

### Invariant Violations

The fuzz runner checks these invariants on every Fragment tree:

1. No NaN or Infinity in coordinates/dimensions
2. Non-negative width and height
3. Box edges (margin/border/padding) are finite
4. Lines are ordered vertically (Y non-decreasing)
5. Line baselines are within line height bounds
6. Block children stack vertically (Y non-decreasing for non-floated,
   static-positioned block elements)

Known layout engine bugs (e.g. negative widths from deeply nested
elements) are logged and saved but do not fail the test. See
`TestData/FuzzFailures/` for any saved violations.

## Adding New Tests

1. Place unit tests for HTML-Renderer internals in
   `HtmlRenderer.Image.Tests` (has `InternalsVisibleTo` access).
2. Place rendering/compliance tests in `Broiler.Cli.Tests`.
3. Use the pixel-based helper methods (`CountPixels`, `GetColorBounds`,
   `IsRed/IsGreen/IsBlue`) for visual rendering assertions.
4. Follow existing test conventions: xUnit `[Fact]` attributes, XML doc
   comments, descriptive assertion messages.
5. Run `dotnet test` to verify before submitting.

---

## Related Documents

- [Testing Architecture](testing-architecture.md) — testable IR boundaries and invariants
- [Testing Current State](testing-current-state.md) — point-in-time audit of test coverage and gaps
- [Testing Roadmap](testing-roadmap.md) — staged plan for multi-layer automated testing
- [Acid1 Testing](acid1-testing.md) — W3C CSS1 conformance test workflow
