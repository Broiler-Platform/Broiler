# CSS, Layout, DOM/HTML, and HtmlBridge refactor gap register

**Status:** Open

**Audit date:** 2026-06-28

**Audited baseline:** `decbb55c` plus the RF-CSS-1/RF-CSS-2 working-tree changes
recorded below

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
| Renderer layout | The engine and namespace move to `Broiler.Layout` is complete and its dependency boundary is green. Cleanup and final visual/conformance closeout remain. | No |
| DOM/HTML | The canonical `Broiler.Dom`/`Broiler.Dom.Html` model is implemented and is the default typed hand-off. Compatibility tree/parser surfaces and deferred behavior remain. | No |
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

### RF-LAYOUT-1 — Finish the cleanup phase

`Broiler.Layout` correctly references only `Broiler.CSS`, `Broiler.CSS.Dom`, and
`Broiler.Dom`, and its architecture tests pass. The roadmap's Phase 5 cleanup is not
complete: the project now exposes internals to nine direct box-tree consumer/test
assemblies (down from 16); remaining dead renderer glue and the layout-specific
closeout still need their own audit.

Close when every friend assembly is justified by a documented compatibility need or
removed, dead glue is deleted, and architecture/API documents reflect the final seam.

### RF-LAYOUT-2 — Run the final layout visual/conformance gate

The layout roadmap itself still calls for a final WPT pixel run. The current audit
verified the build and layout unit/architecture suite, not the heavy pixel corpus.

Close by recording final Acid2/Acid3 box/pixel parity and the agreed WPT layout subset
after RF-CSS-1/RF-LAYOUT-1, including any baseline changes.

### RF-DOM-1 — Retire or explicitly version compatibility DOM/HTML surfaces

The canonical tree is real, but the roadmap's strict definition of done is not met:

- `src/Broiler.HtmlBridge.Core/Dom/DomElement.cs` remains as a public compatibility
  facade over the canonical element;
- `src/Broiler.HtmlBridge.Dom/HtmlTreeBuilder.cs` and several bridge call sites remain
  as compatibility materializers; and
- `RendererHandoffMode.SerializedHtml` remains as an explicit alternate path.

Close by either removing these surfaces after consumer migration or revising the
compatibility policy and definition of done to declare a versioned, tested adapter
boundary. Merely deriving the facade from the canonical node does not satisfy the
current wording that the legacy bridge-owned surface is removed.

### RF-DOM-2 — Close deferred behavior and conformance/performance evidence

The DOM roadmap defers bridge-owned range geometry, including the documented
`Range_GetBoundingClientRect_Includes_DisplayContents_Descendants` failure, and its
definition of done requires WPT, Acid, pixel, and performance gates.

Close when deferred range/geometry ownership is decided, the known failure is fixed
or explicitly moved to a successor roadmap, and current gate results are recorded.

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
- `Broiler.Layout.Tests`: 12/12 passed.
- `Broiler.Dom.Tests`: 19/19 passed.
- `Broiler.Dom.Html.Tests`: 4/4 passed.
- RF-CSS extraction + bridge mutation groups: 34/34 passed.
- visual and broad CSS results, accepted baselines, and performance evidence are
  recorded under RF-CSS-2 above.

The CSS architecture tests initially failed only because their project locator still
assumed the old `src/` checkout shape. This audit made both locators accept the current
submodule layout and retained the old layout as a supported fallback.

## Closeout order

1. Profile or clear the remaining raster-performance gate (RF-CSS-2).
2. Finish the renderer layout seam cleanup (RF-LAYOUT-1).
3. Resolve DOM/HTML compatibility surfaces and deferred geometry (RF-DOM-1/2).
4. Decide the bridge layout end state, then extract or unify it (RF-BRIDGE-1).
5. Record the final layout-specific visual/performance gate (RF-LAYOUT-2).

Do not mark the parent roadmaps complete until every gap above is closed or explicitly
superseded by an approved compatibility decision with equivalent acceptance criteria.
