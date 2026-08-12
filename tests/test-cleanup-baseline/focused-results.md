# Focused results before the cleanup

Measured on `0edd1e0` (the commit this branch was recreated from), .NET 10.0.110,
Linux container, `-c Release`. Command:

```sh
dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj -c Release --no-build \
  --filter "FullyQualifiedName~<class>"
```

## The suites this cleanup deletes or edits

| Suite | Total | Passed | Failed |
| --- | ---: | ---: | ---: |
| `SkiaDecouplingGuardTests` | 7 | 6 | 1 |
| `GraphicsBackendStabilizationTests` | 6 | 1 | 5 |
| `GraphicsBackendCutoverTests` | 7 | 5 | 2 |
| `GraphicsAbstractionTests` | 89 | 78 | 11 |
| `SharedGeometryExclusiveCutoverTests` | 3 | 3 | 0 |
| `LayoutGeometryCacheEquivalenceTests` | 2 | 2 | 0 |
| `HttpClientMigrationTests` | 9 | 1 | 8 |

The roadmap's "8 of 20" figure is the first three rows: 7 + 6 + 7 = 20 cases,
1 + 5 + 2 = 8 failing. `GraphicsAbstractionTests` is counted separately and its
11 failures are exactly the Skia-specific fake/materialization facts.

### What each failure is

- `SkiaDecouplingGuardTests.NonImage_Production_Source_Does_Not_Reference_SkiaSharp`
  asserts `Directory.Exists` on `Broiler.HTML/Source/Broiler.HTML.WPF`, which was
  deleted. One stale entry in a list, not a defect in what the guard checks.
- The five `GraphicsBackendStabilizationTests` failures and the two
  `GraphicsBackendCutoverTests` failures are pixel-parity comparisons of the
  Broiler raster pipeline against the internal stub, described in the source as
  "Skia fallback". They compare the current renderer against a stub that is no
  longer a second implementation, so the diff they measure is meaningless.
- The eleven `GraphicsAbstractionTests` failures assert that operations do or do
  not materialize Skia objects through locally declared `SK*` fakes.
- Eight of nine `HttpClientMigrationTests` throw `FileNotFoundException`
  reflecting over `HtmlRenderer.Rendering`, `HtmlRenderer.Orchestration`,
  `HtmlRenderer.Utils`, and `Broiler.HtmlBridge` — none of which exist. The
  ninth passes without testing an HTTP client: it evaluates a JavaScript string
  literal and asserts the result starts with `https://`.

## Already red for reasons unrelated to this cleanup

These fail at the pre-cleanup commit and must not be counted as cleanup damage.
They are also not this item's to fix.

- `CssExtractionPhaseZeroTests` — including
  `Phase7_CssData_Is_Only_An_Obsolete_StyleSet_Wrapper`, which is the test that
  the roadmap's `CssData` keep boundary protects. It is protected *and* failing.
- `CssExtractionPhaseThreeTests.Bridge_Selector_Surface_Is_A_Compatibility_Wrapper`
- `Acid3CssComplianceTests.Border_Shorthand_Expands_Color_To_Individual_Sides` —
  already carried as an allowed failure by both `run-rf-*-validation.ps1`
  scripts, and kept as one.
- A tail of layout, compositing, and WPT-derived failures across
  `Acid2ImageComparisonTests`, `Acid3Phase5Tests`, `FlexLayoutTests`,
  `GridTrackLayoutTests`, `GoogleRealStructureTest`, `WptCompositingTests`,
  `WptCssVariablesTests`, `WptFontAndSelectorTests`, `HttpSubResourceTests`,
  `DomTraversalAndRangeTests`, `DomWrapperFunctionTests`,
  `NetworkAndHttpTests`, `HtmlPostProcessorProfileTests`, and
  `HtmlBridgePublicApiSnapshotTests`.

`CLAUDE.md` also warns that some `Broiler.Cli.Tests` PDF-conversion cases and
some `Wpt_*_MatchesReference` cases fail in a bare container for environmental
reasons. Baseline before attributing any failure to a change.

## Aggregate run

A full `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj -c Release`
stalled in a long-running case near the end of the run and was stopped, so there
is **no total pass/fail line** for the pre-cleanup commit. It had already
reported 78 distinct failures by then, which is the aggregate picture the later
batches should be compared against:

| Class | Failures | Retired by this cleanup? |
| --- | ---: | --- |
| `GoogleSearchPolyfillTests` | 12 | no — pre-existing |
| `GraphicsAbstractionTests` | 11 | batch 3, pending |
| `HttpClientMigrationTests` | 8 | yes, batch 2a |
| `GraphicsBackendStabilizationTests` | 5 | yes, batch 3a |
| `ScriptEngineExecuteTests` | 4 | no — pre-existing |
| `Acid3Phase5Tests` | 4 | no — pre-existing |
| `WptCompositingTests`, `HttpSubResourceTests`, `CssExtractionPhaseZeroTests` | 3 each | no — pre-existing |
| `PdfToWordConverterTests` | 2 | no — environmental, see `CLAUDE.md` |
| `NetworkAndHttpTests`, `HtmlPostProcessorProfileTests`, `GoogleRealStructureTest`, `FlexLayoutTests` | 2 each | no — pre-existing |
| `GraphicsBackendCutoverTests` | 2 | yes, batch 3a |
| `SkiaDecouplingGuardTests` | 1 | repaired, not deleted, batch 3a |
| 14 further classes | 1 each | no — pre-existing |

So of the 78, exactly 16 belong to suites this cleanup retires or repairs
(8 + 5 + 2 + 1), and 11 more are the `GraphicsAbstractionTests` facts batch 3
still has to resolve. Everything else is pre-existing and must survive the
cleanup unchanged.
