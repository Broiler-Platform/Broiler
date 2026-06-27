# CSS, Layout, DOM/HTML, and HtmlBridge refactor gap register

**Status:** Open

**Audit date:** 2026-06-27

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
| CSS | Phases 0-6 are implemented. Phase 7 has retired the bridge/renderer fallbacks, bridge rule tuple store, and `Broiler.HTML.CSS` assembly; the public `CssData` adapter tail remains. | No |
| Renderer layout | The engine and namespace move to `Broiler.Layout` is complete and its dependency boundary is green. Cleanup and final visual/conformance closeout remain. | No |
| DOM/HTML | The canonical `Broiler.Dom`/`Broiler.Dom.Html` model is implemented and is the default typed hand-off. Compatibility tree/parser surfaces and deferred behavior remain. | No |
| HtmlBridge layout | The separate Track C extraction/unification has not started. | No |

## Open gaps

### RF-CSS-1 — Complete Phase 7 compatibility cleanup

Evidence on the current tree:

- no production project references the former `Broiler.HTML.CSS` project;
- `DomBridge` no longer contains `_cssRules`, `BuildComputedStyleMapLegacy`,
  `ParseAndApplyCssRules`, `EnumerateScopedStyleRules`, or its manual stylesheet
  parser/cascade path;
- the renderer no longer has the `UseSharedRendererCascade` fallback; and
- `Broiler.HTML.Core.CssData` remains in public Image, Graphics, WPF, adapter,
  container, stylesheet-event, and tooling APIs.

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

Still open under RF-CSS-1:

- migrate the remaining public render/container/stylesheet-event signatures from
  `CssData` to `CssStyleSheet` or an approved style-set API, then mark the old
  signatures obsolete for the compatibility window;
- migrate renderer-only pseudo-element, animation, selection, serialization, and
  font-face consumers off the legacy `CssData` block indexes so those obsolete
  `Broiler.HTML.Core` models can be removed; and
- audit and trim the remaining broad renderer/layout `InternalsVisibleTo` grants.

### RF-CSS-2 — Record final CSS cutover validation

The repeatable runner is `scripts/run-rf-css-validation.ps1`. It serially rebuilds
the solution and every standalone test assembly it executes, emits TRX files and a
Markdown summary, rejects failures outside the explicit baseline, and optionally
runs visual and performance gates.

Current post-cutover evidence (`artifacts/rf-css-validation/verified`, 2026-06-27):

- CSS kernel 22/22, CSS DOM 49/49, extraction architecture 20/20, and bridge
  mutation/cascade 21/21 passed;
- broad CLI CSS: 151 passed and the five pre-existing selector/invalidation cases
  remained accepted (`Has_NthChild_Invalidation_Tracks_Removals`,
  `Root_Matches_DocumentElement_Only`,
  `Has_GeneralSibling_NestedNthChild_Invalidation_Tracks_Removals`,
  `Lang_Matches_XmlLang_Ancestor`, and
  `Has_IsAndWhereWrappedSelectors_Invalidation_Tracks_Removals`);
- Acid3 CSS/layout: 65 passed with two accepted cascade failures; WPT anchor/
  visibility/backdrop: 17 passed with the same nine accepted pixel failures; no new
  visual failure or former-assembly load failure appeared. Synchronizing the image
  compatibility provider's lazy registration also removed two order-dependent Acid
  harness failures; and
- the clean no-build benchmark confirmation was within the 2% budget on all gated
  metrics (`html.raster` 168.675 ms, `bridge.mutation` 916,890 ns/op,
  `js.startup` 1.459 ms). The immediately preceding build-loaded sample was noisy and
  failed only `html.raster`; the runner now builds before launching the timed process
  with `--no-build` to keep compilation outside sampling.

RF-CSS-2 remains open only as a final closeout gate after the public/renderer adapter
tail in RF-CSS-1 changes; rerun with `-IncludeVisual -IncludePerformance` then record
any approved baseline delta.

### RF-LAYOUT-1 — Finish the cleanup phase

`Broiler.Layout` correctly references only `Broiler.CSS`, `Broiler.CSS.Dom`, and
`Broiler.Dom`, and its architecture tests pass. The roadmap's Phase 5 cleanup is not
complete: the project currently exposes internals to 17 consumer/test assemblies,
including legacy CSS and broad facade consumers, while the roadmap calls for trimming
that surface and removing dead renderer glue.

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
- `Broiler.CSS.Dom.Tests`: 49/49 passed.
- `Broiler.Layout.Tests`: 12/12 passed.
- `Broiler.Dom.Tests`: 19/19 passed.
- `Broiler.Dom.Html.Tests`: 4/4 passed.
- RF-CSS extraction + bridge mutation groups: 41/41 passed.
- visual and broad CSS results, accepted baselines, and performance evidence are
  recorded under RF-CSS-2 above.

The CSS architecture tests initially failed only because their project locator still
assumed the old `src/` checkout shape. This audit made both locators accept the current
submodule layout and retained the old layout as a supported fallback.

## Closeout order

1. Complete the public/renderer `CssData` adapter tail (RF-CSS-1).
2. Trim the renderer layout seam and compatibility grants (RF-LAYOUT-1).
3. Resolve DOM/HTML compatibility surfaces and deferred geometry (RF-DOM-1/2).
4. Decide the bridge layout end state, then extract or unify it (RF-BRIDGE-1).
5. Run and record the final CSS/layout visual, WPT, Acid, and performance gates
   (RF-CSS-2/RF-LAYOUT-2).

Do not mark the parent roadmaps complete until every gap above is closed or explicitly
superseded by an approved compatibility decision with equivalent acceptance criteria.
