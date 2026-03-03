# Contributing Tests

> Guidelines for writing, organising, and maintaining tests in the Broiler
> repository. For practical instructions on running the test suite see
> [Testing Guide](testing-guide.md).

## Test Projects

| Project | Location | Focus |
|---------|----------|-------|
| `HtmlRenderer.Image.Tests` | `HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/` | Unit tests, CSS2 compliance, rendering, golden-file, analytics, differential |
| `Broiler.Cli.Tests` | `src/Broiler.Cli.Tests/` | W3C compliance, CLI validation, Acid1/Acid2, integration |
| `Broiler.App.Tests` | `src/Broiler.App.Tests/` | Unit tests, rendering output, cross-feature rendering (Windows only) |

Choose the project that best matches the layer under test:

- **HTML-Renderer internals** → `HtmlRenderer.Image.Tests` (has
  `InternalsVisibleTo` access).
- **CLI capture pipeline / W3C compliance** → `Broiler.Cli.Tests`.
- **Rendering output / cross-feature rendering** → `Broiler.App.Tests` or
  `HtmlRenderer.Image.Tests` depending on the rendering layer.

## Required Traits

Every new test class **must** carry at least a `Category` and an `Engine`
trait. Add a `Feature` trait where the test clearly exercises a single
CSS/HTML feature area.

```csharp
[Trait("Category", "Unit")]
[Trait("Engine", "HtmlRenderer")]
public class CssParser_ShorthandExpansionTests
{
    // ...
}
```

### Standard Trait Values

| Trait | Values | Description |
|-------|--------|-------------|
| `Category` | `Unit` | Isolated function / type tests |
| | `Rendering` | Tests that verify visual / rendered output |
| | `Integration` | Multi-component pipeline tests |
| | `Differential` | Cross-engine pixel comparison (requires Playwright) |
| | `DifferentialReport` | Report-generation runs (manual / nightly) |
| | `Compliance` | CSS2 / W3C specification conformance |
| | `Fuzz` | Property-based / generative layout tests |
| `Feature` | `BoxModel`, `Float`, `Position`, `Table`, `Text`, `Color`, `Font`, `Selector`, `Media` | CSS/HTML feature area |
| `Engine` | `HtmlRenderer` | Tests in `HtmlRenderer.Image.Tests` |
| | `Broiler` | Tests in `Broiler.App.Tests` |
| | `Cli` | Tests in `Broiler.Cli.Tests` |

## Naming Convention

Use the pattern **`[Feature]_[Scenario]_[ExpectedResult]`**:

```
BoxModel_MarginCollapsing_LargerMarginWins
Float_LeftFloatWithClear_RendersBelow
Position_RelativeOffset_ShiftsElement
```

Existing tests are not required to be renamed, but all new tests **must**
follow this convention.

## Rendering-Specific Test Requirement

Any change that affects rendering output (CSS parsing, layout, painting,
rasterisation) **must** include at least one rendering-specific test that
verifies observable output — pixels, dimensions, or element positions —
rather than internal state alone.

Use the pixel-based helper methods (`CountPixels`, `GetColorBounds`,
`IsRed/IsGreen/IsBlue`) for visual rendering assertions.

### CSS Property Notes

When writing HTML snippets for rendering tests, use `background-color:`
(the longhand property) rather than the `background:` shorthand. The
HtmlRenderer CSS parser handles the longhand property more reliably for
isolated test snippets.

## Pixel Regression Baselines

Pixel regression tests compare rendered output against committed baseline
PNG images stored in `TestData/PixelBaseline/`.

### Generating a New Baseline

1. Write a test using `AssertPixelBaseline(html)` in
   `PixelRegressionTests.cs`.
2. Run the test — it will **fail** on the first run and write a new
   baseline PNG to `TestData/PixelBaseline/{TestName}.png`.
3. Inspect the generated PNG visually to confirm it is correct.
4. Re-run the test — it should now **pass**, comparing future renders
   against the committed baseline.

### Updating an Existing Baseline

1. Delete the existing baseline PNG for the test you want to re-baseline.
2. Run the test — a new baseline is written and the test fails.
3. Inspect the new baseline, then re-run to confirm it passes.

### Failure Diagnosis

When a pixel regression test fails, the runner saves a **diff image**
(`{TestName}_diff.png`) highlighting mismatched pixels in magenta and
classifies the failure as `LayoutDiff`, `PaintDiff`, or `RasterDiff`.

## Checklist Before Submitting

- [ ] Tests are in the correct project for the layer under test.
- [ ] Test class has `[Trait("Category", "...")]` and
      `[Trait("Engine", "...")]` attributes.
- [ ] Test names follow `[Feature]_[Scenario]_[ExpectedResult]`.
- [ ] Rendering-affecting changes include at least one rendering-specific
      test.
- [ ] New baseline images have been visually inspected and committed.
- [ ] `dotnet test` passes locally.

## Related Documents

- [Testing Guide](testing-guide.md) — practical instructions for running the test suite
- [Testing Architecture](testing-architecture.md) — testable IR boundaries and invariants
- [Testing Current State](testing-current-state.md) — point-in-time audit of test coverage
- [Testing Roadmap](testing-roadmap.md) — staged plan for multi-layer testing improvements
- [Contributing Documentation](CONTRIBUTING-DOCS.md) — guidelines for project documentation
