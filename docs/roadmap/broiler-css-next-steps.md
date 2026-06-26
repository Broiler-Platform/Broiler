# Broiler.CSS extraction — status, findings & next steps

**Date:** 2026-06-26
**Scope of this note:** what landed in the Phase 4 / Phase 6 work, the findings that
shape the rest, and a concrete, prioritized plan for the remaining phases. Read
alongside the per-phase records in [`broiler-css-component.md`](broiler-css-component.md).

## 1. Where the extraction stands

| Phase | State |
|---|---|
| 0–3 (guards, kernel, parser, selectors) | Done (earlier). |
| 4 — cascade & computed style | **Done for `getComputedStyle()`**; anchor `CssRules`-tuple migration **partly done**. |
| 5 — renderer cutover | **Not started.** Largest phase; pixel/Acid/WPT-gated. |
| 6 — CSSOM on shared model | **Slice 6a done** (model-backed storage); store-unification remaining. |
| 7 — compatibility cleanup | **Not started.** Depends on 5 + 6. |

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

## 5. Findings that shape the rest

1. **The CSSOM/rendering split is the heart of Phase 6.** There are **three** rule
   stores today: the CSSOM `rulesStorage`, the renderer-visible `InsertedRules` runtime
   state + style-element text read by `GetStyleElementCssText`, and the `getComputedStyle`
   engine sheets. A script `insertRule()` updates only the CSSOM view — not rendering or
   `getComputedStyle`. Slice 6a model-backed one of these stores; the exit criterion
   ("script mutations and renderer observe the same stylesheet state") needs all three
   unified behind one per-style-element mutable `CssStyleSheet`.
2. **The engine's principled cascade differs from the legacy bridge in ways tests
   encode.** The value-validation gap was the dominant `getComputedStyle` regression and
   is now closed; a possible secondary diff is border-shorthand color→side expansion
   (`Border_Shorthand_Expands_Color_To_Individual_Sides`) — confirm under the pixel suite.
3. **Pattern B anchor scans resist a behavior-preserving swap.** `Visibility`,
   `AnchorRegistry` (anchor-name), `Dialogs` (`::backdrop`), and `AnchorFunctions` scan
   *all* rules globally; the bridge's `_cssRules` is **not** document-scoped while the
   engine is, so swapping them changes multi-document behaviour. They need a shared
   *rule-enumeration* API (not the per-element computed view) plus anchor/WPT verification.
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

## 6. Verification done — and the gap

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
2. **Finish Phase 6 — unify the rule stores.** Give each `<style>`/`<link>` one mutable
   `CssStyleSheet` that backs the CSSOM list, the rendering text (`GetStyleElementCssText`
   / `InsertedRules`), and the `getComputedStyle` engine sheet; route `insertRule`/
   `deleteRule`/`textContent` edits through it; invalidate the style engine on CSSOM
   mutation. Also model-drive the nested `@media`/`@keyframes`/`@supports`/`@layer`
   wrappers (still string-built). Verify with `SelectorsAndCssomTests` + an
   insertRule→getComputedStyle test.
3. **Finish the Phase 4 anchor tail.** Add a shared rule-enumeration API and migrate the
   four Pattern B scans; verify with the anchor-positioning WPT subset.
4. **Phase 5 — renderer cutover** (own effort): build a style context from the canonical
   `DomDocument` in `Broiler.HTML.Orchestration`, replace `DomParser`'s selector/cascade
   assignment with shared computed styles, add a `CssComputedStyle → CssBoxProperties`
   map, keep layout-owned used-value resolution. Dual-run + pixel-diff before switching
   the observable result (roadmap decision #10). Then retire `GetComputedProps`' internal
   `CssRules` use.
5. **Phase 7 — cleanup.** Migrate image/WPF/CLI public APIs off `CssData`, remove
   `Broiler.HTML.CSS` and the obsolete `Broiler.HTML.Core` CSS models, trim broad
   `InternalsVisibleTo`, update architecture/API docs.

## 8. Dual-run rollback switches

- `UseSharedComputedStyleEngine` (`DomBridge/Css.cs`) — flip to `false` to revert
  `getComputedStyle()` to the legacy cascade if a regression surfaces under the pixel
  suite. `BuildComputedStyleMapLegacy` is retained for exactly this.
