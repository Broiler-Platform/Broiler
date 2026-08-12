# Cleanup inventory

What each batch of
[the test-suite retirement item](../../docs/ROADMAP.md#retire-obsolete-test-suites-and-historical-test-artifacts)
touches, and what happened to it. Update this as batches land.

Legend: **done** — landed on this branch. **pending** — not started.
**patch** — submodule-resident, shipped under [`patches/`](../../patches/README.md).

## Batch 1 — baseline and discovery repair · done (partial)

| Item | Outcome |
| --- | --- |
| Focused pre-cleanup results | Recorded in [`focused-results.md`](focused-results.md) |
| Already-red suites | Recorded, so later gates cannot miscount them |
| `Broiler.UI.RichEdit.Rtf.Tests` registration | **pending** — see the note below |

The RTF discovery repair is deliberately not bundled with the deletion batches.
`Broiler.UI/Broiler.UI.slnx` is hand-maintained: it carries no generated-from
header, is absent from `eng/solutions.json`, and `scripts/update-solutions.ps1`
globs the repository root without `-Recurse`, so the generator neither adds nor
reverts an edit to it. Registering the suite will surface its own failures,
which is the point — but it changes a different solution's baseline and belongs
in its own change.

## Batch 2a — tests with no effective coverage, main repo · done

| Item | Outcome |
| --- | --- |
| `HttpClientMigrationTests` | Deleted — reflects over four assemblies that do not exist |
| `Acid3DebugTest` | Deleted — `createElement` invalid-name coverage superseded by `Acid3RegressionTests` (both `1invalid` and `a b`) and `NamespaceAndDomCoreTests` |
| `Acid3CascadeDebugTests` | Deleted — superseded by the dedicated `CssImportantCascadeTests` suite plus `Acid3BorderLayoutTests.Important_Border_Override_Eliminates_Blue` |
| `Acid3TitlePositionDiagTest.Root_Selector_Does_Not_Match_Non_Html` | Deleted — superseded by `SelectorsAndCssomTests`, which asserts `document.querySelector('body:root') === null` |
| `Acid3TitlePositionDiagTest.Root_Selector_Overrides_Html_Border_Top` | **Relocated**, not deleted, to `Acid3CssComplianceTests.Root_Selector_Overrides_Html_Border_Top_In_Rendered_Output`. The existing `Root_Selector_Overrides_Html_Border_Width` asserts the *computed* value only; the rendered title position was uncovered |
| `GoogleLikeDiagTest.FlexChild_DisplayBlock_NotFullWidth` | **Renamed** to `SubmitButtons_InFlexRow_DoNotStretchToContainerWidth`. Not a duplicate — it shared a name with a `FlexLayoutTests` method asserting something else, so a `FullyQualifiedName~` filter caught both |
| `GoogleLikeDiagTest.GridChild_UsesContentSizing` | **Renamed** to `GridChild_SubmitButton_UsesContentSizing`, same reason |
| `Phase1FixTests` | **Kept.** `ToFixed_NegativeZero_Returns_PositiveString` and `NullByte_In_Regex_Test` are the only coverage of those behaviors in the tree |
| `scripts/run-rf-layout-validation.ps1`, `scripts/run-rf-css-validation.ps1` | `Acid3CascadeDebugTests` dropped from the `acid3-layout` / `acid3-css-layout` filters, and only its `Without_Important_Higher_Specificity_Red_Wins` allowed-failure entry removed. `Border_Shorthand_Expands_Color_To_Individual_Sides` kept — it belongs to `Acid3CssComplianceTests`, which stays and is still red |

## Batch 2b — Broiler.JS · pending (patch)

`ReproTests`, `ReproT`, `BroilerJS.sln`, the standalone `JIntPerfTests`
executable, and the status-count reconciliation. `OtherTests/JIntPerfTests/Scripts`
must survive — the engine benchmark project globs it directly.

## Batch 3a — Skia-era transition, main repo · done (except `GraphicsAbstractionTests`)

| Item | Outcome |
| --- | --- |
| `GraphicsBackendStabilizationTests` | Deleted — 6 cases, ~50 full renders per run, comparing the raster pipeline against the internal stub under the name "Skia fallback" |
| `SkiaDecouplingGuardTests` | **Repaired, not deleted.** Its only defect was the deleted `Broiler.HTML.WPF` directory in `ProductionDirectories`; removing that entry takes it from 6/7 to 7/7. Deleting the file would have removed the only automated enforcement of this batch's own gate — no Skia package in the restore graph, no SkiaSharp token in production source — leaving it to a one-time manual grep, which batch 1 forbids |
| `GraphicsBackendCutoverTests` two pixel-parity facts | Deleted. Now 5/5, was 5/7 |
| `GraphicsBackendCutoverTests.CaptureArtifactMetadata_Uses_Explicit_Skia_Fallback_Label` | **Renamed** to `CaptureArtifactMetadata_Records_The_Active_Backend` and kept — the only `renderBackend` sidecar coverage in the repository, and it passes |
| `GraphicsAbstractionTests` 11 Skia fake/materialization facts | **Pending.** Left failing rather than deleted, deliberately — see below |
| `Directory.Build.props`, `FormControlClickTests`, `CssExtractionPhaseZeroTests` WPF references | **Pending** |

Measured: the Skia cluster went from 109 cases with 19 failures to 101 cases with
11 failures. The 8 that went away are exactly the ones retired or repaired.

The 11 `GraphicsAbstractionTests` facts are held back on purpose. They exercise a
compat seam that is still supported — the roadmap forbids removing the boundary
until Broiler.HTML's own exit gate is met — so "why do they fail" has to be
answered before deciding between replacing and removing them. Deleting eleven
failing tests without that diagnosis risks deleting evidence of a real defect,
which is the one thing this whole item is written to avoid.

## Batch 3b — Broiler.HTML · pending (patch)

Three `InternalsVisibleTo("Broiler.HTML.WPF")` entries in
`Source/Broiler.HTML.{Core,Dom,Orchestration}/Properties/AssemblyInfo.cs`, plus
stale adapter references in the submodule's README and architecture notes.

## Batch 4 — geometry cutover seams · done

| Item | Outcome |
| --- | --- |
| `SharedGeometryExclusiveCutoverTests.Exclusive_Boxed_Element_Reads_Real_Shared_Geometry` | **Relocated** to `ElementGeometryBindingModuleTests.Boxed_Element_Reads_Real_Shared_Geometry` |
| `SharedGeometryExclusiveCutoverTests.Exclusive_DisplayNone_Element_Reads_Zero_Not_Estimator` | **Relocated** to `ElementGeometryBindingModuleTests.DisplayNone_Element_Reads_Zero_Geometry`. This was the only coverage anywhere of the `display:none` zero-geometry read |
| `SharedGeometryExclusiveCutoverTests.DefaultsOn` | Deleted with the flag it asserted |
| `LayoutGeometryCacheEquivalenceTests` | Deleted. Its two fixtures were **re-expressed**, not dropped, as absolute check-layout assertions in `SharedLayoutGeometryParityTests` — a stronger statement than "cached equals uncached", which loses meaning once there is no uncached path |
| `DomBridge.UseSharedGeometryExclusively` | Deleted. It gated no production branch at all |
| `DomBridge.LayoutGeometryCacheEnabled` | Deleted, with the single two-line early return it gated in `WithLayoutGeometryCache` |
| `SharedGeometryTestCollection` | **Kept.** Comment rewritten to name the suites that really need serialization: the nine `NativeAnchor*`/`NativePositionTry*` pipeline suites and `ZoomBakeVsEngineEquivalenceTests` |
| `DomBridge.UseSharedLayoutGeometry` | **Kept.** It gates five live production branches and is toggled from a second assembly. Two stale comments claiming it defaults to disabled were corrected |

## Batch 5 — browser WebAssembly phases · pending

Blocked on a decision, not on effort: `Broiler.BrowserWasm.Phase0.csproj` pins 28
project references and is the only thing compiling the Broiler.UI closure against
the `browser-wasm` runtime identifier. Deleting it takes
`Broiler.WebAssembly.Tests.slnx` from 58 projects to about 7 and removes that
check with no replacement. "Loads and builds" does not detect the loss.

## Batch 6 — historical generated output · done

| Item | Outcome |
| --- | --- |
| `tests/html/wpt-results`, `tests/css/wpt-results` | Deleted — 10 files, 38,597,035 bytes, generated 2026-04-24. Two were `.log` files tracked against `.gitignore` |
| `tests/html/` | Removed entirely; it held no other tracked file |
| `CLAUDE.md` | Stale pointer fixed — it named both directories as the home of generated results |
| `docs/ROADMAP.md` Acid/Google/WPT item | No longer describes the snapshots as present |
| `tests/octane/jint-host` | **No change.** Unregistered by design, with the reason recorded in the project file: it references no repository project and is built directly by `scripts/run-octane-benchmarks.sh` |
| `tests/wpt-baseline` | Untouched |

## Batch 7 — consolidation · pending

Sized by the 58 `src/Broiler.Cli.Tests/*BindingModuleTests.cs` files: 250 cases,
of which 47 assert only that a private member moved off the bridge and 34 assert
only that a type is internal or co-located.
