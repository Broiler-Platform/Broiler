# Cleanup inventory

What each batch of
[the test-suite retirement item](../../docs/ROADMAP.md#retire-obsolete-test-suites-and-historical-test-artifacts)
touches, and what happened to it. Update this as batches land.

Legend: **done** — landed on this branch. **pending** — not started.
**patch** — submodule-resident, shipped under [`patches/`](../../patches/README.md).

## Batch 1 — baseline and discovery repair · done

| Item | Outcome |
| --- | --- |
| Focused pre-cleanup results | Recorded in [`focused-results.md`](focused-results.md) |
| Already-red suites | Recorded, so later gates cannot miscount them |
| `Broiler.UI.RichEdit.Rtf.Tests` registration | Registered in `Broiler.UI.slnx`, along with the `Broiler.UI.RichEdit.Rtf` implementation project it depends on, under a new `/src/Integrations/RichEdit/` folder |

`Broiler.UI/Broiler.UI.slnx` is hand-maintained: it carries no generated-from
header, is absent from `eng/solutions.json`, and `scripts/update-solutions.ps1`
globs the repository root without `-Recurse`, so the generator neither adds nor
reverts an edit to it. The edit follows the file's own per-project
`Debug-Linux`/`Release-Linux`/`Debug-Windows`/`Release-Windows` build-type shape.

Registering the suite surfaced no failures: `dotnet test Broiler.UI/Broiler.UI.slnx
-c Release` runs 10 test projects and 300 tests, all passing, with the 6 RTF
clipboard cases among them. Before this change those 6 were invisible to the
solution — the project built and passed only if someone named its `.csproj`
directly.

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

## Batch 2b — Broiler.JS · done (patch `0007`)

`ReproTests` and `ReproT` deleted — no assertion between them. What `ReproTests`
was probing (super property lookup in a class field initializer under eval) is
now `ClassFieldInitializerEvalSuperTests`, six asserting tests, all passing;
`ReproT`'s regex probes were already covered. `BroilerJS.sln` and the standalone
`JIntPerfTests` executable deleted, the `Scripts` corpus kept.

`Broiler.JavaScript.Network` and `Broiler.JavaScript.NodePollyfill` were
deliberately **not** registered in `Broiler.JS.slnx`: neither compiles. Both still
open `Broiler.JavaScript.Core`, a namespace the engine refactor removed. They were
only reachable through a solution that could not restore.

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

## Batch 3b — Broiler.HTML · done (patch `0008`)

Three `InternalsVisibleTo("Broiler.HTML.WPF")` grants removed, plus the README,
architecture, graphics-backend, and roadmap text that still advertised the
deleted adapter as a shipped backend with four public types and its own gate
entries.

## Batch 4 — geometry cutover seams · done

| Item | Outcome |
| --- | --- |
| `SharedGeometryExclusiveCutoverTests.Exclusive_Boxed_Element_Reads_Real_Shared_Geometry` | **Relocated** to `ElementGeometryBindingModuleTests.Boxed_Element_Reads_Real_Shared_Geometry` |
| `SharedGeometryExclusiveCutoverTests.Exclusive_DisplayNone_Element_Reads_Zero_Not_Estimator` | **Relocated** to `ElementGeometryBindingModuleTests.DisplayNone_Element_Reads_Zero_Geometry`. This was the only coverage anywhere of the `display:none` zero-geometry read |
| `SharedGeometryExclusiveCutoverTests.DefaultsOn` | Deleted with the flag it asserted |
| `LayoutGeometryCacheEquivalenceTests` | Deleted. Its two fixtures were **re-expressed**, not dropped, in `SharedLayoutGeometryParityTests`: two independent read passes over the same document must agree exactly and produce no NaN. "Cached equals uncached" loses meaning once there is no uncached path. They deliberately do **not** assert the fixtures' declared WPT values — those do not hold (margin-collapsing offsets come out 10 where WPT declares 15, and 0 where it declares 3), and those gaps belong to the layout engine, not to this cutover |
| `DomBridge.UseSharedGeometryExclusively` | Deleted. It gated no production branch at all |
| `DomBridge.LayoutGeometryCacheEnabled` | Deleted, with the single two-line early return it gated in `WithLayoutGeometryCache` |
| `SharedGeometryTestCollection` | **Kept.** Comment rewritten to name the suites that really need serialization: the nine `NativeAnchor*`/`NativePositionTry*` pipeline suites and `ZoomBakeVsEngineEquivalenceTests` |
| `DomBridge.UseSharedLayoutGeometry` | **Kept.** It gates five live production branches and is toggled from a second assembly. Two stale comments claiming it defaults to disabled were corrected |

## Batch 5 — browser WebAssembly phases · done

18 tracked files deleted, the two phase-zero roots removed from the manifest, and
`Broiler.WebAssembly.Tests.slnx` regenerated from 58 projects to 7.

The blocking concern did not survive checking. Phase 0's 28 pinned project
references were thought to be the only thing compiling the Broiler.UI closure
against the `browser-wasm` runtime identifier; in fact its browser-RID build
compiles a single empty marker class, no workflow ever built it with that RID,
and no workflow builds that solution at all. The live closure check is
`src/Broiler.Writer.WebAssembly`, published for `browser-wasm` by the
preview-package workflow over a comparably wide Broiler.UI graph.

Genuinely lost: the normalized input-trace baseline over the `UiInputEvent`
projection. The render-list half is covered by `Broiler.Graphics.WebAssembly.Tests`.
Even the input trace was never enforced — its generator failed before comparison,
so the committed blobs were compared against nothing. Rebuilding it is now a next
action on the Browser WebAssembly item.

## Batch 6 — historical generated output · done

| Item | Outcome |
| --- | --- |
| `tests/html/wpt-results`, `tests/css/wpt-results` | Deleted — 10 files, 38,597,035 bytes, generated 2026-04-24. Two were `.log` files tracked against `.gitignore` |
| `tests/html/` | Removed entirely; it held no other tracked file |
| `CLAUDE.md` | Stale pointer fixed — it named both directories as the home of generated results |
| `docs/ROADMAP.md` Acid/Google/WPT item | No longer describes the snapshots as present |
| `tests/octane/jint-host` | **No change.** Unregistered by design, with the reason recorded in the project file: it references no repository project and is built directly by `scripts/run-octane-benchmarks.sh` |
| `tests/wpt-baseline` | Untouched |

## Batch 7 — consolidation · done

`*BindingModuleTests`: 250 cases to 207. The 45 removed asserted only that a
private generated-looking callback name is absent from `DomBridge`. Two similarly
named tests were kept and renamed — they assert state *ownership*, which is live.

The `*RemovalTests`/`*MigrationTests` group turned out **not** to be the same
shape: of 54 cases across nine suites, four were tombstones and the rest test real
DOM behavior. All nine were renamed for what they protect; only the four were
deleted.

Duplicated tombstones resolved toward the guard suites.
`HtmlBridgePromotionPhaseZeroTests` is a boundary guard in substance and is now
`HtmlBridgeOwnershipGuardTests`, added to the keep boundary.
`scripts/run-rf-dom-validation.ps1`'s `dom-boundary` group was already demanding
20 tests from a filter matching 18; it now matches 24 and demands 23.
