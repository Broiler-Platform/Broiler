# CSS, Layout, DOM/HTML, and HtmlBridge refactor gap register

**Status:** Open — one gap remaining (RF-BRIDGE-1). CSS, Layout, and DOM/HTML are
closed; RF-CSS-2 closed 2026-06-29.

**Audit date:** 2026-06-28 (RF-CSS-2 raster confirmation added 2026-06-29)

**Audited baseline:** `ea7b7bc6` plus the RF-LAYOUT-1/RF-LAYOUT-2 and
RF-DOM-1/RF-DOM-2 working-tree changes recorded below

This register is the closeout authority for the refactors described by:

- [`broiler-css-component.md`](broiler-css-component.md) and
  [`broiler-css-next-steps.md`](broiler-css-next-steps.md);
- [`broiler-layout-component.md`](broiler-layout-component.md);
- [`broiler-dom-component.md`](broiler-dom-component.md); and
- Track C in [`public-preview-roadmap.md`](public-preview-roadmap.md), which is the
  separate HtmlBridge-layout purification proposal.

"Core extraction implemented" is not treated as "roadmap complete." A roadmap is
complete only when its own definition of done, cleanup work, and stated validation
gates are satisfied.

## Audit result

| Refactor | Current state | Complete? |
|---|---|---|
| CSS | Phases 0-7 are implemented. `HtmlStyleSet` is the supported origin-aware API; `CssData` is only an obsolete one-release wrapper. RF-CSS-2's raster confirmation is within budget (2026-06-29). | Yes |
| Renderer layout | The extraction, cleanup, API boundary, and final Acid/WPT regression gates are complete. | Yes |
| DOM/HTML | The canonical `Broiler.Dom`/`Broiler.Dom.Html` model is implemented, the typed hand-off is exclusive in the application pipeline, compatibility facades are a tested v1 adapter boundary, and the deferred behavior/validation gates are closed. | Yes |
| HtmlBridge layout | The separate Track C extraction/unification has not started. | No |

## Open gaps

### RF-CSS-1 — Complete Phase 7 compatibility cleanup — **Closed 2026-06-28**

Evidence on the current tree:

- no production project references the former `Broiler.HTML.CSS` project;
- `DomBridge` no longer contains `_cssRules`, `BuildComputedStyleMapLegacy`,
  `ParseAndApplyCssRules`, `EnumerateScopedStyleRules`, or its manual stylesheet
  parser/cascade path;
- the renderer no longer has the `UseSharedRendererCascade` fallback; and
- `Broiler.HTML.Core.CssData` remains only as an obsolete one-release wrapper over
  `HtmlStyleSet`; no renderer runtime state or production caller depends on its old indexes.

Close when no production project references `Broiler.HTML.CSS`, public callers have
an approved migration/compatibility story, the bridge and renderer legacy parsers
and selectors are retired, and broad compatibility-only `InternalsVisibleTo` grants
are trimmed.

#### Progress — legacy runtime ownership retired (2026-06-27)

Decision: public `CssData` keeps a one-release adapter while internal runtime
ownership moves to the shared model. Landed:

- **Renderer `UseSharedRendererCascade` fallback — retired.** The flag is gone and the
  legacy `else` branch in `DomParser.CascadeApplyStyles` (the `AssignCssBlocks`
  per-element selector cascade) is deleted; the shared `Broiler.CSS.Dom` engine
  projection is now the sole renderer cascade. Removed the dead helpers
  `AssignClassCssBlocks`, `AssignCssBlocks`, `IsBlockAssignableToBox`, and
  `MatchesAttributeConditions`. Confirmed every element box carries a `SourceElement`
  (`HtmlParser.AppendCanonicalNode`), so the deleted branch was unreachable.
  Pseudo-element selector matching and inline-style application are unaffected.
- **Bridge `UseSharedComputedStyleEngine` fallback — retired.** The gate const and
  `BuildComputedStyleMapLegacy` are deleted; `getComputedStyle()` resolves solely
  through `BuildComputedStyleMapViaEngine`. Removed ~565 lines of orphaned legacy
  computed-style helpers (the custom-property registration/resolution cluster,
  `ResolveCssWideKeywordProperties`/`IsInheritedCssProperty`, and the relative
  font-weight cluster).
- **Bridge tuple cascade — retired.** `_cssRules` and its parse/match/apply helpers
  are deleted. Computed-style, specified-style, mutation invalidation, anchors,
  visibility, and `::backdrop` now query `CssStyleEngine`. The public `CssRules`
  member is an obsolete on-demand projection only; architecture tests prevent it
  from becoming runtime state again.
- **Legacy project — removed.** `Broiler.HTML.CSS.csproj` is gone from both solution
  graphs and all project references. Its short-lived compatibility parser source is
  compiled into `Broiler.HTML.Orchestration`; the namespace is preserved for source
  compatibility, but there is no separate production assembly. Obsolete friend grants
  to the removed assembly were deleted.
- **`CssData` adapter — started.** It now carries the canonical immutable
  `Broiler.CSS.CssStyleSheet`, combines/clones that shared model, and is explicitly
  documented as a one-release adapter. Image, Graphics, and WPF expose
  `ParseStyleSheetModel` as the new shared-model entry point.

#### Closure — public and renderer adapter tail retired (2026-06-28)

- Added the origin-aware `HtmlStyleSet` public API and style-set overloads across
  Image, Graphics, WPF, containers, file rendering, stylesheet events, CLI, tools,
  WPT, apps, and benchmarks. Existing `CssData` signatures are obsolete wrappers for
  the documented one-release compatibility window.
- Pseudo-elements, `::selection`, animations/keyframes, serialization, colors,
  lengths, `@font-face`, and `@font-feature-values` now read the shared stylesheet and
  style engine. Inline declarations participate in the origin/importance/specificity
  cascade, including correct `!important` handling.
- Deleted the compatibility parser sources and the obsolete Core `CssBlock`,
  selector-item, font-face, and keyframe models. `CssData` now has only `StyleSet`,
  `StyleSheet`, `Combine`, and `Clone`.
- Trimmed `Broiler.Layout` from 16 friend grants to nine direct box-tree consumers
  (RF-LAYOUT-1 later reduced this further to the current seven — see that section);
  removed stale grants for Core, Image.Compat, Image.Tests, WPF, both bridge facades,
  and CLI. Architecture tests lock the reduced surface and the removed legacy models.

RF-CSS-1's close conditions are satisfied. The compatibility wrapper is an explicit
API-retirement policy, not a second parser, cascade, model, or runtime path.

### RF-CSS-2 — Record final CSS cutover validation — **Closed 2026-06-29**

The repeatable runner is `scripts/run-rf-css-validation.ps1`. It serially rebuilds
the solution and every standalone test assembly it executes, emits TRX files and a
Markdown summary, rejects failures outside the explicit baseline, and optionally
runs visual and performance gates.

Post-tail correctness evidence (`artifacts/rf-css-validation/rf-css-closeout-20260628`,
2026-06-28):

- CSS kernel 22/22, CSS DOM 55/55, extraction architecture 13/13, and bridge
  mutation/cascade 21/21 passed;
- broad CLI CSS: 151 passed and the five pre-existing selector/invalidation cases
  remained accepted (`Has_NthChild_Invalidation_Tracks_Removals`,
  `Root_Matches_DocumentElement_Only`,
  `Has_GeneralSibling_NestedNthChild_Invalidation_Tracks_Removals`,
  `Lang_Matches_XmlLang_Ancestor`, and
  `Has_IsAndWhereWrappedSelectors_Invalidation_Tracks_Removals`);
- Acid3 CSS/layout: 65 passed with the same two accepted cascade failures; WPT
  anchor/visibility/backdrop improved to 23 passed with three accepted pixel failures
  (the six top-layer anchor baselines now pass). No new visual or assembly-load failure
  appeared.

#### Closure — clean raster confirmation within budget (2026-06-29)

The `html.raster` gate now passes. Three consecutive `--no-build` runs of the
benchmark harness on a stable machine (no compile near sampling) measured raster
**below** the 190.515 ms baseline — 182.075 ms, 185.923 ms, and 182.757 ms (−4.4%,
−2.4%, −4.1%; ceiling 194.33 ms) — all "within budget", with `bridge.mutation` and
`js.startup` also within budget. Evidence is under
`artifacts/rf-css-validation/rf-css-2-confirmation-20260629`.

The 2026-06-28 `html.raster` reading of 216.644 ms (+13.71%) was a
measurement-environment artifact, not a CSS-refactor regression:

1. **Compile too close to sampling.** The 06-28 run sampled raster in a loaded
   container right after building — the failure mode the runner's performance step
   explicitly warns about. A clean `--no-build` run removes it.
2. **Stale cross-API baseline.** The baseline JSON was captured at `12d055b3`
   (2026-06-26) with the old `SetHtml`/`RenderToImage` API; commit `ea7b7bc6`
   swapped every benchmark body to the new `*WithStyleSet` APIs without
   regenerating it. The un-gated companion metrics prove the drift — baseline
   `html.parse` 6.703 ms and `bridge.typed-render-handoff` 7.779 ms are 15–25×
   faster than any current run, impossible as a real per-call regression because
   the `html.paint` benchmark runs the same parse and moved only +8%.

This satisfies the close condition ("a clean confirmation is within budget"). The
baseline was not widened or replaced.

**Follow-up (non-blocking hygiene, not an open gate):** the committed baseline's
un-gated metrics (`html.parse`, `html.layout`, `bridge.typed-render-handoff`) are
stale relative to the current `*WithStyleSet` harness. Regenerating the baseline JSON
on a stable machine with the current harness would make those companion numbers
reproducible. The gated metrics already pass, so do this as a deliberate re-baseline,
separate from RF-CSS-2.

### RF-LAYOUT-1 — Finish the cleanup phase — **Closed 2026-06-28**

- Reduced `Broiler.Layout` from nine friend assemblies to seven (down from 16 at
  extraction). `Broiler.HTML.Dom`, `Broiler.HTML`, and `Broiler.HTML.Orchestration`
  are the production box-tree consumers; `Broiler.DevConsole` projects diagnostic
  snapshots; the other three grants are focused test assemblies.
- Removed two facade/application leaks: canvas-background traversal now stays beside
  the box tree in orchestration, so `Broiler.HTML.Image` no longer needs internals;
  the WPF app consumes renderer-independent dev-console snapshots instead of retaining
  `CssBox` references.
- Documented every remaining compatibility need and the public host/input seam in
  `Broiler.Layout/README.md`. `LayoutArchitectureTests` locks the exact friend set as
  well as the CSS/DOM-only dependency and public-surface constraints.
- Audited the remaining layout engine and renderer adapters: every internal engine
  type has a live layout or direct integration consumer, and no legacy layout-engine
  source remains in `Broiler.HTML.Dom`.

The Phase 5 cleanup conditions are satisfied.

### RF-LAYOUT-2 — Run the final layout visual/conformance gate — **Closed 2026-06-28**

The repeatable runner is `scripts/run-rf-layout-validation.ps1`. It serially builds
the solution and standalone layout tests, emits TRX/JSON/Markdown evidence, accepts
only exact named baselines, and treats new failures, skips, or WPT discovery loss as
regressions. The agreed WPT subset is the committed in-tree `tests/wpt` corpus; its
exact-path baseline is `tests/wpt-baseline/rf-layout-curated.json`.

Final evidence is under
`artifacts/rf-layout-validation/rf-layout-closeout-20260628`:

- layout kernel/architecture 13/13 and diagnostic snapshot seam 5/5 passed;
- Acid2 image/box parity passed 25/25 with no exception baseline;
- Acid3 passed 65 with the same two accepted cascade failures,
  `Without_Important_Higher_Specificity_Red_Wins` and
  `Border_Shorthand_Expands_Color_To_Individual_Sides`; and
- 145 eligible WPT pixel tests produced 69 passes, 71 accepted failures, five
  accepted missing-reference skips, and zero unexpected outcomes. The accepted pixel
  backlog classifies as 63 missing-content, six layout-shift, and two color-shift
  cases; it is frozen as a regression baseline, not claimed as conformance.

No baseline was widened after the recorded run. Known failures may become passes;
any new failure or skip fails the runner. The final visual/conformance gate conditions
are satisfied.

### RF-DOM-1 — Retire or explicitly version compatibility DOM/HTML surfaces — **Closed 2026-06-28**

The remaining public materializers are now an explicit, tested adapter boundary:

- `Broiler.HtmlBridge.DomElement` and `HtmlTreeBuilder` publish
  `htmlbridge-dom-adapter/v1` and the
  `htmlbridge-public-surface/v2` removal boundary;
- the facade delegates mutable tree/attribute state to `Broiler.Dom`, and the builder
  delegates parsing to `Broiler.Dom.Html`; neither owns a second tree or parser;
- the unused application `SerializedHtml` hand-off switch, payload, and execution
  branch were removed, so the WPF pipeline uses the typed `DomDocument` path
  exclusively; and
- seven dead bridge helper copies were removed after confirming the live shared
  callback implementations.

The public-v1 serialized `IScriptEngine.Execute` and renderer `SetHtml` APIs remain
available to direct callers. Architecture guards lock the adapter version and require
a public-v2 boundary before removal. The compatibility policy and DOM definition of
done now record that supported end state.

### RF-DOM-2 — Close deferred behavior and conformance/performance evidence — **Closed 2026-06-28**

Range boundary and mutation semantics remain canonical in `Broiler.Dom.DomRange`.
Client-rectangle calculation is deliberately bridge-owned because it depends on
computed style and renderer geometry. Its deferred `display: contents` failure was
fixed by including descendants of boxless normal-flow siblings; the focused marker is
now `expected=60|actual=60|clientRects=2`.

`scripts/run-rf-dom-validation.ps1` is the repeatable closeout runner. The recorded
`rf-dom-closeout-20260628` run passed:

- DOM kernel 19/19, HTML 4/4, boundary 20/20, bridge DOM behavior 190/190,
  focused range 1/1, and focused WPT DOM 5/5;
- Acid DOM/range: 24 passed plus three exact accepted invalidation failures;
- the nested RF-LAYOUT visual/conformance gate: layout 13/13, diagnostics 5/5,
  Acid2 25/25, Acid3 65 passed plus two exact accepted failures, and curated WPT
  69 passed, 71 accepted failures, five accepted skips, and zero unexpected
  outcomes; and
- owned performance gates: bridge mutation 962,353.100 ns/op versus the
  1,099,027.367 ns/op baseline, serialization median 1.517 ms versus 1.942 ms,
  and typed hand-off 149.026 ms versus serialized hand-off at 153.977 ms.

The full benchmark harness exited successfully, and the fresh solution build passed
with zero warnings and zero errors. No accepted baseline was widened.

### RF-BRIDGE-1 — Decide and execute HtmlBridge Track C

The completed `Broiler.Layout` extraction moved the renderer's layout engine. It did
not complete the distinct HtmlBridge purification proposal:

- `src/Broiler.HtmlBridge.Layout` does not exist;
- `CssBoxModel.cs` and `CssTextProperties.cs` remain in
  `Broiler.HtmlBridge.Rendering`; and
- layout runtime state remains in
  `Broiler.HtmlBridge.Dom/DomBridge/ElementRuntimeState.cs`.

Close by making the roadmap's open end-state decision and then either (a) extracting
the proposed HtmlBridge layout boundary and repointing consumers, or (b) unifying the
bridge on `Broiler.Layout` and formally superseding Track C. The selected path must
retain computed-style, offset-geometry, and bridge-test parity.

#### Code-reality findings (2026-06-29) — the choice is not what Track C framed

A read of the actual bridge code reframes the decision. Track C assumes the
duplication to relocate is `CssBoxModel` + `CssTextProperties`. It is not:

- **The bridge's `CssBoxModel` (1064 LOC) + `RenderingStages` (440 LOC) paint
  pipeline is dead except for its own unit tests.** `BuildLayoutTree`,
  `PaintBox`, and `CreateStackingContext` have **no live (non-test) caller anywhere
  in the repo** (`RenderingPipelineTests.cs` is the only `BuildLayoutTree` caller).
  Runtime painting goes through the renderer's `CssLayoutEngine` (the Graphics app
  and WPF both paint from it). `CssTextProperties` (269 LOC) has no external
  consumer by file name. So Track C's C.2 would extract **dead code** into a new
  assembly — entrenching and dignifying it, the worst outcome.
- **The live duplication is `LayoutMetrics.cs` (2711 LOC)** — recursive estimators
  that re-derive box geometry from computed style to answer `clientWidth/Height`,
  `getBoundingClientRect`, `offset*`, hit-testing, and check-layout assertions. This
  is the exponential-recursion path behind the #1113/#1115 timeouts and the
  pixels-vs-`getComputedStyle` divergence risk. Track C's move list does not mention
  it.
- **The bridge is no longer a `Broiler.Layout` friend** (RF-LAYOUT-1 trimmed it), and
  the engine types are `internal`. Unification therefore needs a *new public*
  `DomElement`-keyed geometry read-model API on the renderer/`Broiler.Layout`, plus a
  headless metrics-only `ILayoutEnvironment` the bridge can supply (the
  `ILayoutEnvironment` text-metrics seam already exists — it was the original reason
  two engines diverged).

#### Recommendation — end-state (b) unify; supersede Track C's extraction

Split RF-BRIDGE-1 into two independently-valued tracks:

- **RF-BRIDGE-1a (retire the dead paint pipeline — small, but a public-surface
  change, not a free deletion).** The pipeline (`CssBoxModel.BuildLayoutTree`,
  `Painter`/`PaintCommand`/`PaintLayer`, `LayoutBox`, stacking contexts in
  `RenderingStages.cs`) is dead at runtime (test-only callers), but the per-type
  audit (2026-06-29) found two constraints:
  1. **The types are public API** — `CssBoxModel`, `LayoutBox`, `Painter`,
     `PaintCommand`, `RenderOutput`, the CSS/flex/grid enums, etc. are all
     `[assembly: TypeForwardedTo]` from the `Broiler.HtmlBridge` facade
     (`src/Broiler.HtmlBridge/TypeForwarding.cs`). Removal must go through the
     `htmlbridge-public-surface` versioning policy: mark `[Obsolete]` now, delete at
     the next public-surface major, mirroring the DOM v1→v2 boundary.
  2. **Shared primitives must be preserved** — `Rect` is live
     (`ImagePipeline.cs` `GetViewBox`/SVG); `BoxEdges`/`BoxDimensions` and the enum
     names collide with live `Broiler.HTML.Core.IR` twins. Keep/relocate the shared
     geometry primitives; only the box-build + paint implementation with no live
     consumer is retired.
  Net effect is still large dead-code removal with zero runtime behavior change, but
  it is a deliberate API-retirement PR, not a one-shot file delete.
- **RF-BRIDGE-1b (large, gated, later): unify the live geometry path.** Full design
  in [`rf-bridge-1b-layout-unification.md`](rf-bridge-1b-layout-unification.md).
  **Feasibility confirmed (2026-06-29):** the renderer already supports headless
  layout (`HtmlContainer.PerformLayout(RectangleF)` uses an internal bitmap for text
  metrics only — no paint surface), `CssBox.SourceElement` links boxes to the
  canonical `DomElement`, and the geometry surface (`Actual*`/client extents) is
  complete. Because `SourceElement` is `internal` to `Broiler.Layout` and the bridge
  is not a friend, the `DomElement`-keyed read-model is exposed by the renderer
  (`HtmlContainerInt`, a Layout friend) — an additive public API in the
  `Broiler.HTML` **submodule** (patch/push workflow). The bridge drives it through a
  versioned per-snapshot provider behind a `UseSharedLayoutGeometry` flag; cut
  `LayoutMetrics`' ~10 `*ForDomElement` entry points over; gate with the
  `EvaluateCheckLayoutAssertions` parity harness over the WPT check-layout corpus +
  the six bridge geometry tests (require shared ≥ estimator) plus WPT pixel/Acid;
  flip; delete the ~2700-line estimators and `WithLayoutGeometryCache`; move
  `LayoutRuntimeState` (`ElementRuntimeState.cs:102`) last.

#### Decision (2026-06-29) — ratified: end-state (b) unify; Track C superseded

The owner ratified end-state (b): unify the bridge on `Broiler.Layout` and formally
supersede Track C's extract-and-keep-two-engines proposal. RF-BRIDGE-1 is now tracked
as 1a (delete the dead paint pipeline — immediate) then 1b (unify the live
`LayoutMetrics` geometry path — large, gated). `public-preview-roadmap.md` Track C
"Layout Component Extraction" is superseded by this decision and should not be built
as a standalone `Broiler.HtmlBridge.Layout` assembly.

## Verification captured by this audit

- serial `dotnet build Broiler.slnx`: passed, 0 errors.
- `Broiler.CSS.Tests`: 22/22 passed.
- `Broiler.CSS.Dom.Tests`: 55/55 passed.
- `Broiler.Layout.Tests`: 13/13 passed.
- `Broiler.Dom.Tests`: 19/19 passed.
- `Broiler.Dom.Html.Tests`: 4/4 passed.
- RF-CSS extraction + bridge mutation groups: 34/34 passed.
- RF-LAYOUT diagnostic seam: 5/5; Acid2: 25/25; Acid3: 65 passed plus two
  accepted failures; curated WPT: 69 passed, 71 accepted failures, five accepted
  skips, and zero unexpected outcomes.
- RF-DOM closeout: kernel 19/19, HTML 4/4, boundary 20/20, bridge behavior
  190/190, focused range 1/1, Acid DOM/range 24 passed plus three accepted
  failures, focused WPT DOM 5/5, visual gate within baseline, and all owned
  performance budgets passed.
- visual and broad CSS results, accepted baselines, and performance evidence are
  recorded under RF-CSS-2 above.

The CSS architecture tests initially failed only because their project locator still
assumed the old `src/` checkout shape. This audit made both locators accept the current
submodule layout and retained the old layout as a supported fallback.

## Closeout order

1. ~~Profile or clear the remaining raster-performance gate (RF-CSS-2).~~ Done
   2026-06-29 — clean raster confirmation within budget.
2. Decide the bridge layout end state, then extract or unify it (RF-BRIDGE-1) — the
   sole remaining open gap.

Do not mark the parent roadmaps complete until every gap above is closed or explicitly
superseded by an approved compatibility decision with equivalent acceptance criteria.
