# CSS, Layout, DOM/HTML, and HtmlBridge refactor gap register

**Status:** Open

**Audit date:** 2026-06-28

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
| CSS | Phases 0-7 are implemented. `HtmlStyleSet` is the supported origin-aware API; `CssData` is only an obsolete one-release wrapper. The final performance confirmation in RF-CSS-2 remains open. | No |
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
- Trimmed `Broiler.Layout` from 16 friend grants to nine direct box-tree consumers;
  removed stale grants for Core, Image.Compat, Image.Tests, WPF, both bridge facades,
  and CLI. Architecture tests lock the reduced surface and the removed legacy models.

RF-CSS-1's close conditions are satisfied. The compatibility wrapper is an explicit
API-retirement policy, not a second parser, cascade, model, or runtime path.

### RF-CSS-2 — Record final CSS cutover validation

The repeatable runner is `scripts/run-rf-css-validation.ps1`. It serially rebuilds
the solution and every standalone test assembly it executes, emits TRX files and a
Markdown summary, rejects failures outside the explicit baseline, and optionally
runs visual and performance gates.

Current post-tail evidence (`artifacts/rf-css-validation/rf-css-closeout-20260628`,
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
  appeared; and
- performance remains the only open gate. The latest optimized no-build confirmation
  kept `js.startup` and `bridge.mutation` within budget, while `html.raster` measured
  216.644 ms against the 190.515 ms baseline (+13.71%, 2% budget). Evidence is under
  `performance-optimized` in the closeout directory.

RF-CSS-2 remains open only for the reproducible `html.raster` performance delta. Do
not widen or replace the baseline until the renderer timing is profiled or a clean
confirmation is within budget.

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

1. Profile or clear the remaining raster-performance gate (RF-CSS-2).
2. Decide the bridge layout end state, then extract or unify it (RF-BRIDGE-1).

Do not mark the parent roadmaps complete until every gap above is closed or explicitly
superseded by an approved compatibility decision with equivalent acceptance criteria.
