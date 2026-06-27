# CSS, Layout, DOM/HTML, and HtmlBridge refactor gap register

**Status:** Open

**Audit date:** 2026-06-27

**Audited baseline:** `114b31bd` (`main`) plus the test-path correction made by this audit

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
| CSS | Phases 0-6 are implemented and the shared paths default on. Phase 7 cleanup and legacy retirement remain. | No |
| Renderer layout | The engine and namespace move to `Broiler.Layout` is complete and its dependency boundary is green. Cleanup and final visual/conformance closeout remain. | No |
| DOM/HTML | The canonical `Broiler.Dom`/`Broiler.Dom.Html` model is implemented and is the default typed hand-off. Compatibility tree/parser surfaces and deferred behavior remain. | No |
| HtmlBridge layout | The separate Track C extraction/unification has not started. | No |

## Open gaps

### RF-CSS-1 — Complete Phase 7 compatibility cleanup

Evidence on the audited tree:

- four production projects still reference `Broiler.HTML.CSS` (`Broiler.HTML`,
  `Broiler.HTML.Dom`, `Broiler.HTML.Orchestration`, and `Broiler.HTML.WPF`);
- `Broiler.HTML.Core.CssData` remains in public Image, Graphics, WPF, adapter,
  container, stylesheet-event, and tooling APIs;
- `DomBridge` still contains `_cssRules`, `BuildComputedStyleMapLegacy`, and its
  parser/selector compatibility path; and
- the renderer still retains the `UseSharedRendererCascade` fallback and legacy
  `CssData`-based helpers.

Close when no production project references `Broiler.HTML.CSS`, public callers have
an approved migration/compatibility story, the bridge and renderer legacy parsers
and selectors are retired, and broad compatibility-only `InternalsVisibleTo` grants
are trimmed.

#### Progress — internal dual-run fallbacks retired (2026-06-27)

Decision: public `CssData` keeps a one-release adapter (deferred); internal fallbacks
first. Landed this pass (no behaviour change — both removed paths were already dead at
runtime; focused suites unchanged at the documented 6-failure baseline, full
`Broiler.slnx` build green):

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
  font-weight cluster). `ParseAndApplyCssRules` is retained (still used by
  `BuildSpecifiedStyleMap`).

Still open under RF-CSS-1 (unchanged):

- **`_cssRules` field is NOT retired.** It is not a fallback but the bridge's live
  primary cascade: `InvalidateElementStyles` (mutation-time re-cascade into
  `element.Style`) and the anchor scans (`AnchorRegistry.GetComputedProps`,
  `EnumerateScopedStyleRules`) still read it. Removing it requires migrating that
  mutation cascade and the anchor declared-value collection onto the shared engine —
  an observable, anchor-positioning-sensitive change that the roadmap has never
  pixel-validated. It should be a dedicated, pixel-gated pass (fold into RF-CSS-2's
  gates), not a blind internal edit. (`ApplyCascadedStyles` appears to have no caller
  and is a separate dead-code follow-up.)
- Public `CssData` facade migration (one-release adapter), removal of
  `Broiler.HTML.CSS` + obsolete `Broiler.HTML.Core` CSS models (and the
  `CssExtractionPhase*` friend-list guard updates that gates), and the
  `InternalsVisibleTo` trim — all still pending.

### RF-CSS-2 — Record final CSS cutover validation

The focused unit and architecture suites are green, but the roadmap definition of
done also requires current Acid, WPT, pixel, and performance evidence after legacy
retirement. Historical intermediate records disagree about which heavy suites were
run, so they are not sufficient closeout evidence.

Close by recording the exact baseline, commands, pass/fail deltas, artifacts, and
accepted deviations from a post-RF-CSS-1 run.

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

- `dotnet build Broiler.slnx --no-restore --nologo`: passed, 0 errors (34 warnings).
- `Broiler.CSS.Tests`: 22/22 passed.
- `Broiler.CSS.Dom.Tests`: 46/46 passed.
- `Broiler.Layout.Tests`: 12/12 passed.
- `Broiler.Dom.Tests`: 19/19 passed.
- `Broiler.Dom.Html.Tests`: 4/4 passed.
- focused CSS/DOM/HtmlBridge extraction and shared-cascade tests in
  `Broiler.Cli.Tests`: 35/35 passed.

The CSS architecture tests initially failed only because their project locator still
assumed the old `src/` checkout shape. This audit made both locators accept the current
submodule layout and retained the old layout as a supported fallback.

## Closeout order

1. Complete CSS Phase 7 and retire its dual-run fallbacks (RF-CSS-1).
2. Trim the renderer layout seam and compatibility grants (RF-LAYOUT-1).
3. Resolve DOM/HTML compatibility surfaces and deferred geometry (RF-DOM-1/2).
4. Decide the bridge layout end state, then extract or unify it (RF-BRIDGE-1).
5. Run and record the final CSS/layout visual, WPT, Acid, and performance gates
   (RF-CSS-2/RF-LAYOUT-2).

Do not mark the parent roadmaps complete until every gap above is closed or explicitly
superseded by an approved compatibility decision with equivalent acceptance criteria.
