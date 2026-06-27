# Broiler.CSS extraction — status, findings & next steps

**Date:** 2026-06-27

**Current closeout status:** Phases 0-6 are implemented. Phase 7 has retired the
legacy bridge/renderer runtime paths and removed the `Broiler.HTML.CSS` assembly;
the public/renderer `CssData` adapter tail and final post-tail gate remain open in
[`refactor-gap.md`](refactor-gap.md).
**Scope of this note:** what landed in the Phase 4 / Phase 6 work, the findings that
shape the rest, and a concrete, prioritized plan for the remaining phases. Read
alongside the per-phase records in [`broiler-css-component.md`](broiler-css-component.md).
Sections 3-7 are chronological implementation records; references there to a retained
legacy path describe that slice at the time. Section 9 and `refactor-gap.md` are the
current-state authorities.

## 1. Where the extraction stands

| Phase | State |
|---|---|
| 0–3 (guards, kernel, parser, selectors) | Done (earlier). |
| 4 — cascade & computed style | **Done** (`getComputedStyle()` cutover + anchor Pattern B scans migrated to a document-scoped enumeration, §4b). |
| 5 — renderer cutover | **Done — flag flipped ON (2026-06-26, §4d).** Both render paths cascade through the shared engine; verified against Acid3 + WPT pixel gates (no pass/fail regressions; fixes several important/border cascade tests). |
| 6 — CSSOM on shared model | **Done** (slices 6a + 6b: model storage, store unification, model-driven nested wrappers; §4a). |
| 7 — compatibility cleanup | **In progress.** Runtime fallbacks, bridge tuple cascade, and the old project are gone; public `CssData` signatures and renderer-only compatibility indexes remain. |

## 2. Build prerequisites that were broken (now fixed)

The working tree did **not** build when this work started. Three independent breakages
from the "everything moved into submodules" reorg, all fixed:

1. **`Broiler.HTML` submodule** — its six csprojs still referenced the pre-relocation
   `..\..\..\src\Broiler.CSS` / `src\Broiler.Dom*` paths (CS0234). Repointed to the
   `Broiler.CSS\` and `Broiler.DOM\` submodules.
2. **`Broiler.JS` submodule** — the `Broiler.Regex` project reference carried the
   `BRegex` extern alias but **not** `<PrivateAssets>all</PrivateAssets>`, so the
   `Broiler.Regex` namespace leaked downstream and collided with
   `System.Text.RegularExpressions.Regex` (CS0118) in `Broiler.Cli`. Added the guard
   (matching the adjacent `Broiler.DateTime` reference). Builds clean at JS `6db891`.
3. **Arch guard tests** — `CssExtractionPhase{Zero,Two,Three}Tests` froze the old
   `src\` project paths; updated to the submodule layout.

> Reminder: building the bridge needs the **nested** submodules under `Broiler.JS`
> initialized (`cd Broiler.JS && git submodule update --init --recursive`).

## 3. Phase 4 — what landed

### 3.1 `getComputedStyle()` cutover (done, gate on)

- `DomBridge.ComputedStyleEngine.cs` resolves `getComputedStyle()` through the shared
  `Broiler.CSS.Dom.CssStyleEngine`: one engine per document root (`GetSyncedScopedEngine`),
  re-syncing the scoped `<style>`/`<link>`/inserted-rule text only when it changes, then
  projecting `CssComputedStyle.Properties` into the bridge's dict. The bridge's
  `DomElement` derives from canonical `Broiler.Dom.DomElement`, so the engine runs on
  bridge nodes directly — no adapter.
- `DomBridge/Css.cs` `BuildComputedStyleMap` dispatches via the
  `UseSharedComputedStyleEngine` gate (now `true`) to the engine path or the retained
  `BuildComputedStyleMapLegacy` (kept as a dual-run fallback per §8.6).
- **Parity fix that unblocked the flip:** the engine lacked per-declaration value
  validation, so it kept invalid values (`display:supergrid` over a prior
  `display:inline-block`). Ported the legacy `DomBridge.IsAcceptableCssValue`
  closed-keyword table into `CssStyleEngine.IsAcceptableDeclarationValue` (validates the
  importance-stripped value), wired into `ApplyStyleRule` (cascade) + the inline step of
  `ComputeStyle`. Eight error-recovery unit tests added to `Broiler.CSS.Dom.Tests`.

### 3.2 Anchor `CssRules`-tuple migration (partial)

- `CssStyleEngine.GetCascadedDeclaredValues` returns raw cascade-winning author values
  (no inline / inheritance / shorthand / initial-value backfill — so an undeclared
  property reads as **absent**, which the anchor code relies on).
- Bridge `CollectMatchedRuleProperties` routes the **six** per-element declared-value
  collectors (`PositionArea`, `PositionAreaQueries`, `FixedPosition`, `PositionTry` ×2,
  `ContainingBlocks`) through it; each still merges `element.Style` on top, so behaviour
  is preserved by construction.
- `AnimationResolver` uses no tuples — the "animation helpers" criterion was already met.

## 4. Phase 6 — what landed (slice 6a)

`DomBridge/StyleSheets.cs` now stores each sheet's rules as `List<Broiler.CSS.CssRule>`
(was `List<string>`). `insertRule`/`deleteRule` parse into and mutate the model; a new
`BuildCssRuleObject(CssRule, …)` overload drives the JS wrapper via `CssSerializer`
(byte-identical). This removes string-only top-level CSSOM storage.

## 4a. Phase 6 — what landed (slice 6b: store unification)

The three stores from §5.1 are now **one** per-style-element source of truth. The dead
`StyleSheetRuntimeState.InsertedRules` (read in three places, never written after slice 6a)
was replaced by a live `List<CssRule> Rules` + `RulesSourceText` + `RulesMutated`:

- **One shared model.** `EnsureStyleSheetRulesCurrent(styleEl)` (`DomBridge/Css.cs`) holds
  the mutable rule list in runtime state, reparsing from `GetStyleElementSourceText`
  whenever the element's source text changes (so replacing `textContent` discards prior
  `insertRule`/`deleteRule` mutations — CSSOM semantics, already covered by
  `StyleSheet_InsertRule_Does_Not_Reappear_After_Owner_TextContent_Is_Replaced`).
- **CSSOM is backed by it.** `BuildStyleSheetObject` no longer keeps a private
  `rulesStorage`; `cssRules`/`insertRule`/`deleteRule` resolve the shared list via
  `CurrentRules()` and flag `RulesMutated` on mutation. The four `JsStyleSheets*Core`
  callbacks take a `Func<List<CssRule>>` instead of a captured list.
- **Renderer + engine observe it.** `GetStyleElementCssText` returns the raw author source
  byte-for-byte while unmutated (zero pixel risk for unchanged sheets), and the serialized
  live model once mutated — so a script `insertRule()` flows to the legacy cascade
  (`BuildComputedStyleMapLegacy`/`BuildSpecifiedStyleMap`) and, via the hash-based re-sync
  in `GetSyncedScopedEngine`, to the shared `getComputedStyle` engine. The two inline
  copies of the text-collection logic (`ApplyPseudoElementRules`, the font-weight resolver)
  now route through `GetStyleElementCssText` too.
- **New regression test:** `StyleSheet_InsertRule_Is_Observed_By_GetComputedStyle`
  (insert → `getComputedStyle` sees it → delete → reverts).

**Verification (this slice):** `Broiler.CSS.Dom.Tests` 31 pass / 1 baseline; `SelectorsAndCssom`
+ `CssRendering` 128 pass / 5 baseline (the §5.5 selector failures); broader Cli.Tests sweep
(excl. Acid3/WPT) unchanged from baseline. Heavy Acid3/WPT/pixel suites still **not run**.

The nested `@media`/`@keyframes`/`@supports`/`@layer` wrappers are now **model-driven**
too: `BuildCssRuleObject(CssRule, …)` threads the nested `CssAtRule.Rules` into the string
builder (via `BuildNestedRuleObjects`/`BuildNestedKeyframeObjects`), so initial nested
construction no longer round-trips through serialize→`ParseCssRuleStrings`→reparse. It
gates on `Rules.Count > 0`, so declaration-bodied at-rules and empty blocks stay on the
string path. Nested `insertRule` still feeds the list factory a string (correct — JS
supplies text). Phase 6 store unification is complete.

## 4b. Phase 4 — anchor tail (Pattern B scans)

The four global anchor rule-scans no longer read the non-document-scoped `_cssRules`
field directly; they go through a new document-scoped seam,
`EnumerateScopedStyleRules(DomElement scope)` (`DomBridge/Css.cs`):

- `AnchorRegistry.BuildAnchorRegistry` → `EnumerateScopedStyleRules(DocumentElement)`
- `AnchorFunctions.ResolveAnchorFunctions` → `EnumerateScopedStyleRules(element)`
- `Dialogs.GetBackdropBackground` → `EnumerateScopedStyleRules(dialog)`
- `Visibility.FindElementByAnchorName` → `EnumerateScopedStyleRules(el)`

For the main document the seam returns `_cssRules` verbatim (via `ReferenceEquals` on the
document root), so behaviour is identical — `ResolveAnchorPositions` only ever runs on
`DocumentElement`. For any other root it builds the triples from that root's own
`<style>`/`<link>` (Phase 6 `GetStyleElementCssText`) with the same parse/@media-filter/
selector-flatten as `_cssRules` (`ImportParsedRules` now takes a target list). The
`GetComputedProps` use of `CssRules` (`AnchorRegistry.cs:214`) is deliberately **left** —
it is the renderer/layout cascade (78 consumers, finding §5.4), Phase 5 territory.

Verified: build clean; `SelectorsAndCssom` + `CssRendering` 128 / 5 baseline. Anchor pixel
behaviour (Acid3/WPT) not run, per project state.

## 4c. Phase 5 — renderer cutover, slice 1 (dual-run scaffold, default off)

The foundation for replacing `DomParser`'s selector/cascade with the shared engine is in,
behind a flag that is **off by default** — the legacy cascade is still the observable
rendering path (roadmap decision #10).

- **Project wiring.** `Broiler.HTML.Orchestration` now references `Broiler.CSS.Dom`. The
  nested-vs-top-level `Broiler.Dom` unify to one assembly (same as the bridge), confirmed
  by a clean build of `engine.GetComputedStyle(box.SourceElement)` — i.e. the renderer's
  canonical element type *is* the engine's expected type, no adapter.
- **`CssBox.SourceElement`.** `HtmlParser.AppendCanonicalNode` (the `DomDocument` parse
  path) now stores the canonical `Broiler.Dom.DomElement` on each `CssBox`. Null on the
  legacy HTML-string path. This re-establishes the box→element link that box construction
  previously severed (only tag name + attributes were copied).
- **The `CssComputedStyle → CssBoxProperties` map.** `SharedRendererCascade.ApplyComputedStyle`
  (`Orchestration/Parse/SharedRendererCascade.cs`) writes each engine-computed
  (property, value) pair onto the box through the renderer's own
  `CssUtils.SetPropertyValue` — no hand-mapped ~80-field table; unknown names are ignored.
  `Apply` builds a `CssStyleEngine` from the document's `<style>` text and walks the box
  tree applying to boxes with a `SourceElement`. Layout-owned used-value (`Actual*`)
  resolution is untouched.
- **Dual-run hook.** `DomParser.GenerateCssTree(DomDocument,…)` calls `Apply` after the
  legacy `PrepareCssTree` **only when `SharedRendererCascade.UseSharedRendererCascade` is
  true** (default false).
- **Tests:** `Phase5SharedCascadeTests` (2, passing) flip the path on directly and assert
  the engine's computed `display` (author `block`, and `inline` initial-backfill for an
  unmatched element) lands on the box. Full suite 179 / 5 baseline (flag off → no change).

**Slice 2+ (remaining, pixel-gated):** parity work before the flag can flip — UA default
sheet, inline `style=`, `!important`/origin tracking, external `<link>` + `@import`,
pseudo-elements + `::selection`, `@font-face`/`@keyframes`, real viewport threading; then
dual-run pixel-diff, flip the flag, and retire `GetComputedProps`' internal `CssRules`.

## 4d. Phase 5 — renderer cutover complete, flag flipped ON (2026-06-26)

Slice 1's scaffold was a no-op for the gate: it only hooked `GenerateCssTree(DomDocument)`
(the `SetDocument` path), but the test gate and app render through the HTML-string path
(`HtmlRender.RenderToImage` → `SetHtml` → `GenerateCssTree(string)`). The cutover moved the
engine into the shared `PrepareCssTree`/`CascadeApplyStyles`, so **both** paths now cascade
through the shared engine. Both `GenerateCssTree(string)` and `…(DomDocument)` already build a
canonical `DomDocument` (the string overload goes through `HtmlDocumentParser` →
`HtmlParser.ParseDocument(DomDocument)`), so `CssBox.SourceElement` is set on both — the
"null on the string path" note in §4c is stale.

- **Non-clobbering projection.** New `CssStyleEngine.GetCascadedStyle(element)` returns the
  cascade-resolved *declared* longhands (UA + author cascade by origin/importance/specificity/
  order, custom-property/`var()`, CSS-wide keywords, shorthand expansion, `attr()`, relative
  font-weight) **without** inheritance backfill, initial backfill, or form-control/logical-size
  synthesis, and folds `inherit` to the parent's computed value. So the renderer keeps its own
  `InheritStyle` + per-box defaults; only explicitly-cascaded values are projected. This avoids
  the full-`GetComputedStyle` clobbering (which backfills `display:inline`, `font-family:serif`,
  etc. over the renderer's defaults).
- **Border shorthand reset.** `GetCascadedStyle` resets a `border`/`border-<side>` shorthand's
  omitted longhands to their initial (e.g. an important `border:1px solid` now projects
  `border-color: rgb(0,0,0)`), so it overrides a prior `border:… red` — fixes
  `Acid3CascadeDebugTests.Important_Override_Reduces_Border_Width`.
- **Integration.** `DomParser.PrepareCssTree` builds one engine (UA sheet at `CssOrigin.UserAgent`
  + author `<style>` text + viewport) via `SharedRendererCascade.BuildEngine`/`FindCanonicalDocument`;
  `CascadeApplyStyles` projects `GetCascadedStyle` in place of the legacy `AssignCssBlocks` block
  when the flag is on, keeping `InheritStyle`, presentational attributes, inline style,
  pseudo-elements, animations, and `::selection` (the latter still driven by the legacy `cssData`,
  which is still built).
- **Flag flipped.** `SharedRendererCascade.UseSharedRendererCascade` defaults **`true`**.

**Verification (2026-06-26).** Dual-run, single-threaded to neutralise the suite's pre-existing
±2-3 parallel/order flakiness:
- Cli gate (`CssExtraction|SelectorsAndCssom|CssRendering|CssImportantCascade|WptCssVariables|
  CssSelectorsPolish|Acid3`): serial-OFF 24 fail → serial-ON 21-22 fail. **Net improvement**;
  every "new failure" candidate (`Important_Override`, `Important_Low_Specificity`,
  `Border_Shorthand_Resets`, `Root_Selector_Overrides_Html_Border_Top`) **passes in isolation**
  → pre-existing flakiness, not cascade regressions. Fixes confirmed: Important-cascade,
  FirstChild, VerticalAlign, border-shorthand.
- WPT pixel subset (`tests/wpt`, 145 cases): OFF **67/73/5** == ON **67/73/5** — **0 pass/fail
  regressions, 0 improvements**. Three already-failing tests render a lower match% with the
  engine: `css-backgrounds/background-attachment-margin-root-001/002` (35%→0% — background on
  root edge case) and `css-anchor-position/position-area-percents-001` (93.5%→91.7%).
- Engine units: `Broiler.CSS.Dom.Tests` 37 pass / 1 (the standalone path-resolution quirk
  baseline); `Phase5SharedCascadeTests` 4/4. Full `Broiler.slnx` build green.

**Follow-ups (not blockers):** (a) the `background-attachment` on-root match drop — investigate the
background-shorthand reset / root background propagation in the engine path; (b) the suite's
parallel/order-dependent flakiness in the Acid3 rendering tests (shared process state), which makes
the parallel gate noisy by ±2-3 independent of this work.

## 5. Findings that shape the rest

1. **The CSSOM/rendering split is the heart of Phase 6.** ~~There are **three** rule
   stores today~~ **(resolved in slice 6b, §4a.)** The CSSOM `rulesStorage`, the
   renderer-visible `InsertedRules` runtime state + style-element text read by
   `GetStyleElementCssText`, and the `getComputedStyle` engine sheets are now unified
   behind one per-style-element mutable rule list (`StyleSheetRuntimeState.Rules`, kept
   current by `EnsureStyleSheetRulesCurrent`). A script `insertRule()` is now observed by
   the CSSOM view, rendering, and `getComputedStyle`. Only the nested-at-rule wrapper
   tail remains string-built (§4a, §7.2).
2. **The engine's principled cascade differs from the legacy bridge in ways tests
   encode.** The value-validation gap was the dominant `getComputedStyle` regression and
   is now closed; a possible secondary diff is border-shorthand color→side expansion
   (`Border_Shorthand_Expands_Color_To_Individual_Sides`) — confirm under the pixel suite.
3. **Pattern B anchor scans — migrated to a document-scoped enumeration API (done, §4b).**
   `Visibility`, `AnchorRegistry` (anchor-name), `Dialogs` (`::backdrop`), and
   `AnchorFunctions` scanned *all* rules globally via the non-document-scoped `_cssRules`.
   They now go through `EnumerateScopedStyleRules(scope)`, which returns `_cssRules`
   verbatim for the main document (so those scans are byte-for-byte unchanged — they only
   ever run on `DocumentElement`) and a per-root-built list for any other document root,
   removing the latent cross-document leak. **Caveat:** anchor/WPT pixel verification was
   not run (per project state) — the main-document path is behavior-preserving by
   construction, but the sub-document path is exercised only when a future sub-document
   anchor pass runs.
4. **`GetComputedProps` is the renderer/layout cascade, not a Phase 4 target.** Its
   internal `CssRules` use feeds 78 layout/hit-test consumers and is pixel-affecting —
   it belongs to the Phase 5 renderer cutover, not the anchor-helper migration.
5. **Known pre-existing baseline failures (this submodule-pinned tree), unrelated to
   this work** — treat as the baseline, not regressions:
   - selector suite: `Root_Matches_DocumentElement_Only`, `Lang_Matches_XmlLang_Ancestor`,
     three `Has_*_Invalidation_Tracks_Removals`, and Acid3
     `GetComputedStyle_LastChild_Recomputes_After_RemoveChild` (6 total);
   - many Acid3/WPT/form-control rendering, pixel, image-capture, and documented
     cascade-ordering "known limitation" tests fail in this headless tree independent of
     these changes (e.g. `Acid3CascadeDebugTests.Without_Important_Higher_Specificity_Red_Wins`).

## 6. Phase 4/6 checkpoint verification (historical)

- `Broiler.CSS.Dom.Tests`: **31 pass** (1 pre-existing standalone path-resolution quirk).
- Focused CSS guard suite (`CssExtraction*`, `SelectorsAndCssom`, `CssRendering`,
  `CssImportantCascade`, `WptCssVariables`, …): **at the 5-failure baseline, unchanged.**
- CSSOM suite (`SelectorsAndCssomTests`): **53 pass / 5 baseline.**
- **Not run** (per request): the heavy **Acid3 / WPT / pixel** suites. These are the only
  meaningful verification for the renderer cutover and for full `getComputedStyle`/anchor
  parity. **Run them before merging** and reconcile any diffs (start with the
  border-shorthand case above and anchor-positioning tests).

## 7. Recommended next steps (in order)

1. **Run the full Acid3/WPT/pixel suite against this branch** and triage diffs against the
   baseline in §5.5. Fix or document each. This gates everything below.
2. **Finish Phase 6 — unify the rule stores.** ✅ **Done in slice 6b (§4a).** Each
   `<style>`/`<link>` now has one mutable rule list backing the CSSOM list, the rendering
   text (`GetStyleElementCssText`), and the `getComputedStyle` engine sheet;
   `insertRule`/`deleteRule`/`textContent` edits route through it and the engine re-syncs
   off the hash of the (now model-derived) text. Verified with `SelectorsAndCssomTests` +
   the new `StyleSheet_InsertRule_Is_Observed_By_GetComputedStyle` test. The nested
   `@media`/`@keyframes`/`@supports`/`@layer` wrappers are now model-driven from
   `CssAtRule.Rules` too (§4a), so Phase 6 is complete.
3. **Finish the Phase 4 anchor tail.** ✅ **Done (§4b).** Added the document-scoped
   `EnumerateScopedStyleRules` seam and migrated the four Pattern B scans; behaviour-
   preserving for the main document. **Still to verify** under the anchor-positioning WPT
   subset when those suites are re-enabled.
4. **Phase 5 — renderer cutover.** ✅ **Done (§4d).** The shared renderer cascade
   defaults on. Retiring the retained rollback path belongs to Phase 7.
5. **Phase 7 — cleanup.** **Ready but not implemented (see §9).** Migrate image/WPF/CLI public
   APIs off `CssData`, remove `Broiler.HTML.CSS` and the obsolete `Broiler.HTML.Core` CSS
   models, retire the legacy renderer/bridge cascade, trim broad `InternalsVisibleTo`,
   and update architecture/API docs.

## 8. Dual-run rollback switches — RETIRED (2026-06-27)

Both dual-run switches and their legacy branches were removed in the RF-CSS-1
internal-fallback retirement (see `refactor-gap.md` → RF-CSS-1 progress note). No
rollback path remains; the shared engine is the sole authority for both
`getComputedStyle()` and the renderer cascade.

- ~~`UseSharedComputedStyleEngine` (`DomBridge/Css.cs`)~~ — gate const and
  `BuildComputedStyleMapLegacy` deleted; `getComputedStyle()` resolves solely through
  `BuildComputedStyleMapViaEngine`.
- ~~`SharedRendererCascade.UseSharedRendererCascade`~~ — flag and the legacy
  `CascadeApplyStyles` `else` branch (`AssignCssBlocks` selector matching) deleted; the
  shared engine projection is the sole renderer cascade.

## 9. Phase 7 readiness — current audit

Phase 5 and Phase 6 are complete, and Phase 7 is materially underway. The current
tree has one bridge CSS authority and no `Broiler.HTML.CSS` production assembly:

- renderer and bridge dual-run flags and their legacy branches are deleted;
- bridge `_cssRules`, manual parse/apply/selector helpers, and scoped tuple scans are
  deleted; anchors, visibility, mutation reads, specified style, and `::backdrop`
  resolve through `CssStyleEngine`;
- the old CSS project is removed from both solution graphs and every project
  reference. Compatibility parser sources now compile into Orchestration while their
  namespace remains temporarily source-compatible;
- `CssData` carries the shared `CssStyleSheet`, and Image/Graphics/WPF expose
  `ParseStyleSheetModel`; and
- architecture guards prevent the retired bridge state and project from returning.

**Still load-bearing:** `CssData` remains in public facade/container/event APIs and
its block indexes still serve renderer-only pseudo-element, animation, selection,
serialization, and font-face paths. Those consumers need shared-model projections
before the old Core CSS types can be removed. Broad renderer/layout friend grants
also still need a consumer-by-consumer trim.

The authoritative remaining work, accepted visual baselines, and repeatable
validation command are in [`refactor-gap.md`](refactor-gap.md), RF-CSS-1 and
RF-CSS-2.

**Closeout order:** (1) add shared-model/style-set facade signatures and obsolete the
old `CssData` signatures for one release; (2) migrate renderer-only compatibility
indexes; (3) remove obsolete Core CSS models and compatibility parser sources; (4)
trim `InternalsVisibleTo`; (5) rerun the visual and performance closeout gate.
