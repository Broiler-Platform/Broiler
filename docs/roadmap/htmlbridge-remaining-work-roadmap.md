# HtmlBridge — Remaining Work Roadmap (post-facade-removal)

Status: **active** — the consolidated "what's left" list now that the `DomElement` facade removal has landed.
Date: 2026-07-11 (Section 1 gate closed 2026-07-12).

> **Update (2026-07-12): all of Section 1 has landed on `main`.** The F3c/F4 merge gate (1.1) passed
> and merged as PR #1359; the CSS-helper promotion (2.1 shorthand + 2.3 casing/`CssPriority`) merged as
> PR #1362; and the `@position-try` twin follow-up from 1.2 — animation/`@keyframes` collection from
> InnerHtml-backed `<style>` elements — merged as PR #1363. The remaining open work is entirely in
> Section 2 (DOM/CSS promotion backlog).

## Purpose

With the `Broiler.HtmlBridge.DomElement` facade and `HtmlTreeBuilder` **deleted** (RF-BRIDGE-1c Phase F4),
the two big HtmlBridge efforts — the v1 public-surface removal (blocked-items Track 1 / Item 1) and the
RF-BRIDGE-1b geometry unification (Track 2 / Item 2) — are **implementation-complete**. Their roadmaps
close out. This doc is the single place that collects everything that still remains, so the completed
roadmaps can be read as "done" without hunting for stray open items.

Two distinct buckets:

1. **Blockers to *landing* the facade removal** — small, in-scope, must happen before merge.
2. **The ongoing DOM/CSS promotion backlog** — larger, out-of-scope of the facade removal, not urgent.

Authoritative records referenced below:
- Facade removal: [`htmlbridge-facade-removal-current-state.md`](htmlbridge-facade-removal-current-state.md)
  (live record), [`htmlbridge-domelement-facade-removal-plan.md`](htmlbridge-domelement-facade-removal-plan.md)
  (design history).
- Milestones/tracks: [`htmlbridge-blocked-items-completion-roadmap.md`](htmlbridge-blocked-items-completion-roadmap.md).
- Promotion phases: [`htmlbridge-dom-css-promotion-roadmap.md`](htmlbridge-dom-css-promotion-roadmap.md).

---

## 1. Blockers to landing the facade removal (in-scope, do these to merge)

### 1.1 WPT + Acid + pixel merge gate — **DONE (merged)**

The F3c/F4 stack (`72634e02`, `aa00ecf5`, `ddad4769` on `claude/htmlbridge-domelement-f3c-flip`, PR
#1359) — both irreversible cutovers, the text→`DomText` flip (2d) and the element-construction flip +
facade delete (F4) — passed the **full WPT range/selection/serialization + Acid + pixel** gate and
**merged (`ecbdf406`, 2026-07-12)**. The failing WPT set was confirmed a subset of the committed baseline
(`tests/wpt-baseline/failed-tests.json`, updated `[skip ci]` in `2b9b1319`); no new SVG/foreign
`createElementNS` namespace or range/selection/serialization regressions. The `DomElement` facade and
`HtmlTreeBuilder` are gone from `main`; the bridge tree is canonical `Broiler.Dom` nodes.

### 1.2 Resurrect `Broiler.Wpt.Tests` (pre-existing, out of `Cli.Tests` scope) — **DONE**

`Broiler.Wpt.Tests` (`WptTestRunnerTests.cs`) had been **non-compiling since phases B/E1** — it referenced
the facade's `.Style` / `.Parent` compatibility members that those phases removed. F4 applied the item-5
seam type-swap (`Broiler.HtmlBridge.DomElement` → `Broiler.Dom.DomElement`) there, but the `.Style`/
`.Parent` references remained broken. This test project is outside the `Broiler.Cli.Tests` verification
harness (WPT runs via the dispatch workflow, not these unit tests), so it did not block the gate — but
the whole solution would not build until it was fixed.

- **Done (2026-07-11):** the 12 broken call sites (across 5 test methods) were rewritten against a
  supported surface. `.Parent`/`.Parent.Id` → canonical `DomNode.ParentNode` (cast to `Broiler.Dom.DomElement`);
  the `.Style` inline-style reads → a new **internal** read-only accessor `DomBridge.GetInlineStyleView(DomElement)`
  (wraps the private `InlineStyle` map, visible only via `InternalsVisibleTo` — **not** a public facade seam;
  `HtmlBridgeBoundaryGuardTests` still green, 12/12). The stale `..\Broiler.HtmlBridge\` project reference
  (the pre-split project, gone since the Core/Dom/Rendering/Scripting split) was repointed to
  `..\Broiler.HtmlBridge.Dom\`, clearing the MSB9008 warning. **The whole solution now builds (0 errors).**
- **Surfaced pre-existing failures (NOT caused by this fix, NOT part of the facade work):** with the project
  dark for months, resurrecting it exposed failing tests. Across the full `WptTestRunnerTests` class: 334/472
  pass, 138 fail — ~118 are `*_MatchesReference` pixel reftests that fail in a bare container for the
  documented environmental (font) reasons, the remaining ~20 are script/subframe/harness tests (deferred-promise,
  PDF app, viewport features). Independent of both the merge gate (1.1) and the facade removal.
- **Position-try regression fixed (2026-07-11):** the resurrection also exposed a genuine **`@position-try`
  fallback regression** (not test staleness): `Wpt_PositionTryCascade_FallbackApplies` and the pixel-accurate
  `PositionTryFallbackTests.PositionTry002_…` both failed (box never moved). Root cause: after `Attach`, a
  `<style>` element's CSS can live in its `InnerHtml` runtime state with no `DomText` child (childCount == 0),
  but `CollectPositionTryRulesFromTree` read the CSS by hand-walking child text nodes → it collected **zero**
  `@position-try` rules, so every fallback silently no-op'd (both resolve-only and full-render paths). Fix:
  read via the canonical `GetStyleElementSourceText(el)` accessor (the same source the cascade uses; covers the
  InnerHtml-fallback case). Both tests now pass; the 7 nearby `PositionArea*`/`PositionTryGrid*` pixel reftests
  fail **identically with and without the fix** (baseline-verified environmental, 0 regressions). The latent
  twin of the same bug in `AnimationResolver.CollectAnimPropsFromStyleElements` (static InnerHtml fallback)
  is now **fixed and merged** (PR #1363, `d383b371`): animation/`@keyframes` collection reads
  InnerHtml-backed `<style>` source via the same canonical accessor, with a reproducing test.
- **Priority:** low; independent of the merge gate.

### 1.3 Optional cleanup — retire the `DomElement` alias — **DONE**

F4 added `global using DomElement = Broiler.Dom.DomElement;` in `Broiler.HtmlBridge.Dom` so the
unqualified element-handling sites resolved canonical without per-site edits. This was behaviourally exact
but left an alias shadowing a deleted type name.

- **Done (2026-07-11):** the 659 bare `DomElement` tokens across 46 files were fully-qualified to
  `Broiler.Dom.DomElement` (matching this assembly's existing convention — it already fully-qualifies
  every other `Broiler.Dom.*` type, e.g. `Broiler.Dom.DomNode` 216×, and has no `using Broiler.Dom;`),
  and `GlobalUsings.cs` (which held only the alias) was deleted. The rename used a dot- and
  word-boundary-guarded substitution so it skipped already-qualified refs and method-name substrings
  (`GetClientWidthForDomElement`, `FindDomElementByJSObject`, `CloneDomElement`, …). Qualifying a type name
  is a compile-time identity, so runtime behaviour is provably unchanged: whole solution builds (0 errors),
  `HtmlBridgeBoundaryGuardTests` 12/12 green, bridge smoke tests pass. (The unrelated file-scoped
  `using DomElement = …` in `Broiler.Cli.Tests/SharedLayoutGeometryTests.cs` is a separate test-local
  convenience, not the F4 alias, and was left as-is.)

---

## 2. DOM/CSS promotion backlog (out-of-scope of the facade removal)

These belong to [`htmlbridge-dom-css-promotion-roadmap.md`](htmlbridge-dom-css-promotion-roadmap.md) and
were **deliberately not** part of deleting the facade type. They continue the broader goal of moving bridge
responsibilities into the canonical `Broiler.Dom`/`Broiler.CSS` components. None are blocked by anything;
they are prioritized independently.

### 2.1 Promotion Phase 2 — computed style (partially delivered)

- The literal `GetComputedProps → GetComputedStyle` cutover (route the bridge's computed-property reads
  through the canonical CSSOM computed-style path). **The cutover of the ~98 call sites is still deferred,
  but the canonical projection it needs now exists and the swap is scoped by a measured parity delta
  (2026-07-12).**
  - **Additive canonical projection landed.** `Broiler.CSS.Dom.CssStyleEngine.GetSparseComputedStyle`
    (Broiler.CSS submodule) runs the full computed-style pipeline (cascade + inline, custom-property/`var()`,
    CSS-wide keywords, shorthand + `attr()`, relative font-weight, inheritance backfill, form-control size
    synthesis, logical-size aliases) **without** the initial-value backfill, so an undeclared non-inherited
    property reads back absent — the null-for-undeclared contract the ~98 `GetComputedProps` consumers rely
    on. It is the `ComputeStyle` pass gated by a new `backfillInitials:false` flag; the full
    `GetComputedStyle`/`GetCascadedStyle` paths are unchanged. Covered by 5 `CssStyleEngineTests`. This is
    additive — **zero call-site changes**, so it cannot regress anything on its own.
  - **Stale blocker corrected.** The roadmap's second blocker — "layout-dependent form-control sizing" —
    is **not** a blocker: `ApplyApproximateFormControlComputedSizes` already has a layout-free copy inside
    `CssStyleEngine.Computed.cs` (it counts `<br>`-split text lines and reads `size`/`rows` attributes, no
    glyph/box measurement), living within the `CssDomArchitectureTests` boundary. The only real remaining
    contract issue is sparse-vs-full, addressed by the flag above.
  - **Parity measured, and it is bigger than "a short reconciliation list."** A differential characterization
    test (`SparseComputedStyleParityTests` in `Broiler.Cli.Tests`, via two internal `*ForParity` bridge
    accessors) compares the canonical projection against the bridge's `GetComputedProps` over a corpus and
    pins the delta to **four structural classes** — everything else matches exactly, and the test fails on any
    *uncategorised* drift:
    1. **UA display defaults** — the bridge injects a UA `display` for block elements
       (`ApplyUserAgentDisplayDefaults`); the canonical projection leaves `display` to the renderer, so it is
       present-in-bridge / absent-in-sparse. (Real gap — a swap must reconcile it.)
    2. **Inheritance model** — the canonical projection backfills inheritance from the parent's *full*
       computed style (every inherited property materialises from the root's initials down); the bridge
       propagates from the parent's *sparse* map (an inherited property never declared anywhere stays absent).
       The whole inherited-property set therefore diverges on elements that declare none of it. (Real gap.)
    3. **Value resolution (bidirectional)** — the engine resolves `var()`/`initial`/`unset`/`bold`→`700` that
       the bridge leaves raw; conversely the engine's `ComputeStyle` has no inherit-fold, so it emits a raw
       `inherit` the bridge resolves. (Mostly improvements; audit at swap.)
    4. **Custom properties** — the engine surfaces `--*`; the bridge omits them. (Benign.)
  - **The cutover — DONE (2026-07-12) via the engine inline-provider approach.** `GetComputedProps`
    (`AnchorRegistry.cs`) now routes through `GetSyncedScopedEngine(el).GetSparseComputedStyle(el,
    sparseInheritance: true)` + the two thin reconciliations (`ResolveExplicitInheritedValues` for the
    class-3c inherit-fold, `ApplyUserAgentDisplayDefaults` for class 1). The bridge's private
    `ApplyInheritedProperties` (class-2 inheritance duplication) is **deleted**; the inset/form-control/logical
    tail steps are gone from the path (the engine's `ComputeStyle` already does them). Two enabling changes
    made it work where the earlier blind swap failed:
    1. **Sparse-inheritance projection** (`Broiler.CSS` submodule): `GetSparseComputedStyle` gains a
       `sparseInheritance` flag that sources inheritance from the parent's *sparse* projection (via a cached
       `GetSparseComputedStyleInternal` recursion) instead of the parent's full computed style — reconciling
       class 2 (nowhere-declared inherited props stay absent).
    2. **Inline-style provider** (`CssStyleEngine.SetInlineStyleSource`, submodule): the earlier attempt
       failed because the engine reads inline from the DOM `style` **attribute**, but the bridge's
       authoritative inline is its live **ElementRuntimeState** map — JS `el.style.X=` and the anchor
       resolver write ERS and *never* the attribute (verified: `getAttribute("style")` stays null,
       `getComputedStyle` returned defaults). The bridge now feeds the engine the ERS map as the cascade's
       inline source (`SerializeInlineStyleForEngine`), so the engine sees JS-set and resolver-written inline
       for both the element's cascade and its inheritance recursion. Cache coordination: the per-document
       engines' caches are invalidated together with `_computedPropsCache` (new `ClearComputedPropsCache`
       routing the three clear sites through `InvalidateScopedEngineComputedCaches`), since an ERS mutation is
       not a DOM mutation.
    - **Side effect (a fix):** `getComputedStyle` now also reflects JS-set/resolver-written inline (it
      previously did not), since it shares the same provider-fed engine.
    - **Verified regression-free + pixel-neutral (2026-07-12):** the 4 previously-regressing tests pass; the
      full `Broiler.Cli.Tests` suite has **0 new deterministic failures** vs a same-state baseline (the two
      apparent deltas — `FixedChild_DoesNot_Inflate_Parent_AutoHeight` and a `NetworkAndHttpTests` fetch case —
      are pre-existing parallel-flaky / order-dependent, confirmed at clean HEAD); the full local WPT corpus
      (171 tests: CSS2, css-align, css-anchor-position, css-animations, css-backgrounds) is **pixel-identical**
      (same 38 failures, **zero** matchPercent drift across every test); engine unit tests + the parity test
      pass. Still wants the dispatch-only full WPT/Acid gate at merge. **Follow-up:** the now-dead
      form-control/logical bridge helpers (`ApplyApproximateFormControlComputedSizes`, `ApplyLogicalSizeAliases`
      + orphaned helpers) are left for a separate sweep (tracked). **Delivery:** the engine changes
      (`sparseInheritance`, `SetInlineStyleSource`, cache) are in the `Broiler.CSS` submodule — push + pointer
      bump; the bridge rewire + parity-accessor repoint are main-repo.
- **Shorthand expansion — DONE (2026-07-12).** The bridge's `ExpandCssShorthands` (+ its private
  `ExpandFontShorthand` / `ExpandBoxShorthand` / `ExpandBorderShorthand` / `ExpandBorderSideShorthand` /
  `ExpandBackgroundShorthand` / `SplitCssValues` helpers, ~500 lines in `DomBridge/Css.cs`) is deleted and
  now delegates to the single canonical `Broiler.CSS.Dom.CssStyleEngine.ExpandShorthands` (a new public
  wrapper over the engine's existing private pass). The bridge copy had **drifted to a narrower subset**
  than the roadmap's earlier "only expands `outline`" note suggested — beyond `outline` it also lacked the
  engine's `font` slash line-height handling (`NormalizeFontSlashTokens`) and its multi-layer
  `background` parser (size/origin/clip). Adopting the engine (a strict, additive, more-spec-correct
  superset — never removes a shorthand, never overwrites an existing longhand) was verified
  **regression-neutral**: exact baseline set-diff over the computed-style / getComputedStyle / font /
  background / border Cli.Tests subset shows **0 new failures** (the ~16 pre-existing failures are
  environmental Skia-font / zoom / `*LikeChromium` rendering + Acid cases; the two that wobbled between
  parallel runs — `Root_Selector_Overrides_Html_Border_Top`, `FixedChild_DoesNot_Inflate_Parent_AutoHeight`
  — both pass in isolation = known Cli.Tests parallel flakiness). This closes the Phase 2 exit criterion
  "the bridge has no private computed-style table that can drift from `CssStyleEngine`" for shorthands.
  **Delivery note:** the public `ExpandShorthands` wrapper is in the `Broiler.CSS` submodule — push +
  pointer bump (or patch fallback) at commit time.

### 2.2 Promotion Phase 4 / slice 8 — Range content operations (canonical API landed; bridge rewire remains)

The token-list + mutation-filtering work landed earlier; the content operations are now split into two
slices — **the canonical algorithms first (done), the bridge rewire second (remaining, higher-risk).**

- **Done (2026-07-12) — canonical `DomRange` content operations.** `Broiler.Dom.DomRange` gains the full
  DOM Standard §4.5 content-operation surface, implemented against canonical `DomNode`/`DomCharacterData`
  (no JS-object or layout dependencies): `ExtractContents` / `CloneContents` / `DeleteContents` /
  `InsertNode` / `SurroundContents`, plus the selection helpers `SelectNode` / `SelectNodeContents` /
  `Collapse` and the `CommonAncestorContainer` accessor. These follow the spec algorithms literally
  (partially-contained-child recursion via sub-ranges, `contained`/`partially contained` boundary-point
  tests over the existing `CompareBoundaryPoints`, text-node split for `InsertNode`), rather than porting
  the bridge's ad-hoc document-order-`IndexOf` heuristics. Two new `DomException` factories
  (`InvalidStateError` / `InvalidNodeTypeError`) back `surroundContents`/`selectNode`. Covered by 13 new
  `DomRangeTests` cases (extract/clone/delete within one text node and across nodes, insert at an element
  offset + inside-text split + comment-container rejection, surround wrap + partial-non-text throw,
  select/collapse); the whole `Broiler.Dom.Tests` project is green (64/64) and the bridge consumer builds
  clean. **Delivery note:** the new code is in the `Broiler.DOM` **submodule** — push + pointer bump (or
  `patches/` fallback if the push 403s) at commit time.
- **Done (2026-07-12) — the bridge `Range`/`RangeState` rewire.** The bridge's `RangeState` class is
  **deleted**; the live JS `Range` is now backed by a `BridgeDomRange : Broiler.Dom.DomRange` (a private
  bridge subclass constructed `trackMutations: false`) that overrides the four node-creation seams so the
  canonical content operations mint bridge nodes — `#document-fragment` result fragments and
  `CloneDomElement` clones that carry host runtime state (form-control value/checked, scroll, dialog/shadow,
  live inline style), all registered in `_knownNodes`. Every `Range` JS callback in
  `JsFunctionCallbacks/Traversal.cs` now delegates: the content ops (`extract`/`clone`/`delete`/`insert`/
  `surround`) to the canonical methods; the boundary/selection ops (`setStart`/`setEnd`/`setStartBefore`…/
  `collapse`/`selectNode`/`selectNodeContents`) to `SetStart`/`SetEnd`/`Collapse`/`SelectNode`. The bridge's
  hand-rolled algorithms are removed: `RangeState.AdjustForRemoval`/`UpdateCollapsed`, the ad-hoc
  `ExtractStartPath`/`ExtractEndPath`/`CreatePartialCloneForExtract`/`IsContainedInRange` extract helpers,
  and the manual boundary collapse math. Boundary adjustment for external tree mutations still runs through
  the bridge's weak `_activeRanges` registry (preserving the no-leak `WeakReference` design), which now calls
  the canonical `DomRange.NotifyNodeRemoved`. Geometry stays bridge-owned: `GetClientRectsForRange` /
  `getBoundingClientRect` / `getClientRects` and `toString`/`compareBoundaryPoints` read boundary points off
  the `DomRange` but keep their bridge geometry/text helpers.
  **Verification:** `Broiler.Cli.Tests` range/traversal suite 56/57 (the 1 failure —
  `Range_GetBoundingClientRect_Includes_DisplayContents_Descendants` — is **pre-existing/environmental**,
  baseline-confirmed: a `display:contents` layout that produces zero height in the bare container, fails
  identically without this change); mutation-observer / boundary-guard / DOM-interface suites 90/90; Acid3 /
  DOM-events / DOM-edge suites 120/120; canonical `Broiler.Dom.Tests` 69/69. The two `insertNode`
  boundary-after-split tests were updated to the **spec-correct** result (the start boundary stays in the
  truncated original text node rather than the pre-rewire bridge's non-spec normalization to the parent) —
  gate-safe because `dom/ranges/Range-insertNode.html` (and all five `Range-*` content-op WPT tests) are
  already in the committed failed baseline, so adopting spec behavior can only hold or improve, never add a
  new failure. Like the F3c/F4 cutovers this still wants the full WPT range/selection corpus at merge.

### 2.3 Promotion Phase 1 slice-2 — deferred helpers (casing + `CssPriority` **DONE**; live-setter routing remains)

Phase 1's exit criteria were already met; slice-2 left deferred items: casing helpers, `CssPriority`, and
live-setter routing.

- **Done (2026-07-12):** the two pure-string helpers are promoted to the canonical `Broiler.CSS` kernel:
  - `Broiler.CSS.CssPropertyNames.ToCssPropertyName` / `ToDomPropertyName` — the CSS-kebab ↔ DOM-camelCase
    property-name mapping (including the vendor-prefix leading-hyphen round-trip). The bridge's three
    private copies (`Css.cs` local `ToCamelCase`, `Utilities.cs` `ToCamelCaseStatic` / `ToKebabCase`) are
    deleted and all ~15 call sites route through it.
  - `Broiler.CSS.CssPriority.Strip` / `Parse` / `Apply` — the CSSOM string-level `!important` handling
    (lenient trailing-suffix contract preserved byte-for-byte). The bridge's `StripCssPriority` /
    `GetCssPriority` / `ApplyCssPriority` + `ImportantSuffixPattern` regex are deleted and all call sites
    route through it.
  - Both are neutral, dependency-free static utilities; the CSS architecture guard (`CssArchitectureTests`)
    stays green. Covered by new `CssPropertyNamesTests` + `CssPriorityTests` (36 cases, exact-parity).
    Verified regression-free: whole solution builds (0 errors); the bridge CSSOM / inline-style /
    style-declaration subset is unchanged (the only 2 failures — `SelectorsAndCssomTests.Root_Matches_…`
    and `…Lang_Matches_XmlLang_Ancestor` — are **pre-existing at clean HEAD**, baseline-confirmed, and
    exercise `:root`/`:lang` selector matching untouched by this change).
  - **Delivery note:** the new files live in the `Broiler.CSS` **submodule** — the submodule commit must be
    pushed and the parent pointer bumped (or a `patches/` fallback if the push 403s) as part of committing
    this work.
- **Done (2026-07-12) — live per-property setter validation.** The remaining "live-setter routing" gap:
  the per-property `CSSStyleDeclaration` setters (`el.style.X = …`, `setProperty(…)`, the rule-side twins,
  `cssFloat`) wrote their value **raw** into the inline-style map with **no validation**, while the
  attribute/`cssText` path (`el.style = "color: bogus"`) already dropped invalid declarations via
  `ParseStyle` → the shared `CssDeclarationValidator`. That asymmetry meant `el.style.color = "bogus"`
  *stored* the invalid value. All six live setter sites now gate through a shared
  `DomBridge.IsAcceptableInlineValue` helper (= `CssDeclarationValidator.IsAcceptableDeclarationValue` on the
  `!important`-stripped value) — the same closed-keyword error-recovery the attribute path uses. An invalid
  value is now ignored (prior value kept), matching CSSOM error handling; custom (`--*`) and unknown
  properties still pass (the validator default), and `!important` is validated on the stripped value.
  - **Nuance found (the "live CSSOM object identity" entanglement the roadmap warned about):** the IDL
    `SetValue` path stores the value in **two** places — the ERS inline-style map *and*, via the trailing
    `base.SetValue`, a plain JS property on the style object that the getter reads **first**. Gating only the
    map was silently bypassed; the fix returns early on an invalid value so `base.SetValue` never stores it
    either. Separately, `el.style.cssFloat = …` is a **pre-existing dead path** (`cssFloat` is in
    `NonCssNames`, so `SetValue` delegates to `base.SetValue` and never reaches the `Set009`/`Set020`
    accessor); the gate there is harmless defensive code, and `float` set via the reachable
    `setProperty('float', …)` path is validated.
  - **Verified regression-free (Cli-scope):** 10 `CssStyleDeclarationValidationTests`; the CSSOM /
    inline-style / computed-style / Acid3 clusters show **0 new failures** — the 9 residual fails all
    **pre-existing**, confirmed by stashing the whole change set and re-running at clean HEAD (`:root`/`:lang`,
    `HttpClientMigration`, `Acid3CascadeDebug`, `Border_Shorthand`, and the environmental Acid3 render/score/
    image-capture + `PhaseC_NodeIterator` tests fail identically without the change). Because it changes
    observed CSSOM behavior (invalid values now rejected), it wants the full **WPT css/cssom + Acid** gate at merge.
  - **Delivery:** entirely main-repo (bridge `DomBridge.cs` helper + the six setter sites in
    `DomBridge/Utilities.cs` and `DomBridge/JsFunctionCallbacks/Utilities.cs`); no submodule change.
- **Still deferred:** the live stylesheet-**mutation** paths are already largely shared (`insertRule`/
  `deleteRule`/`cssText` parse through `CssParser`, per Phases 3/6).
- **Assessed, low value (2026-07-12) — `CssInlineStyleParser.ParseDeclarations` consolidation.** The
  Phase-1 "add a shared inline-declaration parser" idea turns out **marginal**: both `DomBridge.ParseStyle`
  and the engine's inline loop (`CssStyleEngine.cs:482`, `Computed.cs:131`) *already* use the canonical
  `CssParser().ParseDeclarations()` + `CssDeclarationValidator.IsAcceptableDeclarationValue`, so the only
  shared code is a ~4-line orchestration loop with **different output shapes** (the bridge formats a dict
  with a `!important` string suffix + vendor-prefix mapping; the engine adds cascade winners). The named
  "third copy" `HtmlCss.ParseDeclarations` (`Broiler.Documents.Html`) is a **different, naive parser**
  (HtmlDecode + split on `;`/`:`, no CssParser, no validation, no `!important`) serving document conversion —
  forcing it onto the canonical parser would change Documents behavior, out of scope.
- **Done (2026-07-12) — `StripVendorPrefix` promoted.** The one genuinely-clean remaining duplicate — the
  **byte-identical `StripVendorPrefix`** that lived privately in both `DomBridge.cs` (bridge `ParseStyle`
  vendor→unprefixed inline-style aliasing) and `CssStyleEngine.cs` (cascade-slot vendor→unprefixed alias) —
  is promoted to the canonical `Broiler.CSS.CssPropertyNames.StripVendorPrefix` (its natural home alongside
  the kebab↔camel `To*PropertyName` mapping). Both private copies are deleted and route through the shared
  static; the move is a compile-time identity (a pure, dependency-free string function called identically),
  so behaviour is provably unchanged. Covered by 9 new `CssPropertyNamesTests` cases (known prefixes,
  case-insensitive prefix match with verbatim remainder, `--custom`/unprefixed pass-through, empty). Verified
  regression-free: `Broiler.CSS.Tests` `CssPropertyNames` 26/26; `Broiler.CSS.Dom.Tests` 213/214 (the 1 =
  the pre-existing `Public_Surface_Does_Not_Expose_Mutable_Collections` arch guard, baseline per §2.4); the
  bridge end-to-end `GoogleSearchPolyfillTests.Webkit_Prefixed_Property_Mapped_To_Unprefixed` + the
  style-declaration-validation / inline-style cluster 16/16. **Delivery note:** `CssPropertyNames.cs` + its
  tests are in the `Broiler.CSS` **submodule** — push + pointer bump (or `patches/` fallback if the push
  403s); the bridge one-line reroute is main-repo.

### 2.4 Promotion Phase 2 slice-2 — stylesheet scope assembly (P2 `CssStyleScopeBuilder`) — **DONE (2026-07-12)**

The P2 candidate "stylesheet scope assembly without fetching" — previously **never built** (a whole-repo
search found only the roadmap mention) — is delivered.

- **Canonical `Broiler.CSS.Dom.CssStyleScopeBuilder`** (Broiler.CSS submodule) owns the neutral scope
  assembly the bridge previously did ad-hoc in `DomBridge.GetSyncedScopedEngine`: given a host-supplied,
  document-ordered list of `StyleSource(cssText, origin, media?)`, it evaluates each source's `media`
  attribute against the environment, keeps only the matches (in cascade-origin/document order), parses each
  as its own sheet, and re-syncs the `CssStyleEngine` only when the effective (media-filtered) set changes.
  The host keeps the parts that need the DOM + resource loading — discovering `<style>`/`<link>` elements,
  fetching external sheets, extracting each sheet's CSS text. 9 `CssStyleScopeBuilderTests`.
- **Real correctness fix, not just a move.** The old bridge path concatenated every collected sheet into one
  blob and applied it **unconditionally**, ignoring per-element `media` — so `<style media="print">` (and
  any non-matching `<link media>`) wrongly took effect on screen. Routing `GetSyncedScopedEngine` through the
  builder (extracting `GetAttr(styleEl, "media")` per source) fixes this; parsing each source separately also
  isolates a malformed sheet from the next and scopes `@import`/`@namespace` per-sheet. Pinned by
  `StyleScopeMediaTests` (a non-matching-media sheet is excluded; matching/no-media applies).
- **Verified regression-free (Cli-scope):** full `Broiler.CSS.Dom.Tests` 213/214 (the 1 = the pre-existing
  `Public_Surface_Does_Not_Expose_Mutable_Collections` arch guard, baseline-confirmed via stash); bridge
  StyleSheet/Cssom/ComputedStyle/MediaQuery/Cascade clusters 140/144 — the 4 fails all **pre-existing**
  (`:root`/`:lang` selector, `HttpClientMigration` reflection/assembly-load, `Acid3CascadeDebug` render-pixel
  known-limitation; the latter two confirmed failing at HEAD with the bridge change reverted). Because it
  changes observed behavior for media-gated sheets, it still wants the full **WPT/Acid/pixel** gate at merge.
- **Delivery note:** `CssStyleScopeBuilder.cs` + its tests are in the `Broiler.CSS` **submodule** — push +
  pointer bump (or `patches/` fallback) at commit time; the bridge rewire + `StyleScopeMediaTests` are main-repo.
- **Not moved (correctly host-owned):** `<style>`/`<link>` discovery (`CollectStyleElementsInTree`), external
  fetching, and CSS-text extraction (`GetStyleElementCssText`, InnerHtml/CSSOM/runtime-state) — they need the
  DOM and resource loading, matching the roadmap's "fetching remains bridge/host code" boundary.

### 2.5 Promotion-candidate backlog (P0–P3) + Open Questions

- The remaining P0–P3 promotion-candidate rows in the promotion roadmap not covered above (the broader
  "what else could move to the canonical components" backlog). The two higher-risk behavior changes that were
  open here — the `GetComputedProps` **cutover** (§2.1) and the live `CSSStyleDeclaration` **setter routing**
  (§2.3) — are now **both done** (each verified regression-free locally and wanting the dispatch-only WPT/Acid
  gate at merge). The residual backlog is small: the optional `StripVendorPrefix`-style neutral promotions and
  a tracked sweep of the now-dead form-control/logical bridge helpers left by the §2.1 cutover.
- The promotion roadmap's **Open Questions** (Open Question #5 — "declare v2" — is now answered; the rest
  remain).

---

## 3. Documentation state (done)

As of 2026-07-11 the four previously-stale roadmap docs are updated to reflect F4 complete:
`htmlbridge-domelement-facade-removal-plan.md` (status → implemented), `htmlbridge-blocked-items-completion-roadmap.md`
(Milestones 1.2/1.3 → done, Track 1 complete), `htmlbridge-dom-css-promotion-roadmap.md` (Phase 5
adapter-removal → done), and `rf-bridge-1b-layout-unification.md` (header + increment 6/7 BLOCKED labels
cleared). This roadmap is the forward-looking companion.
