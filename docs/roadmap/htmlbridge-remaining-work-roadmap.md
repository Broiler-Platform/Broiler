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
  through the canonical CSSOM computed-style path). **Still deliberately deferred** — the sparse-map vs
  full-initials contract mismatch and the layout-dependent form-control sizing make it unsafe as a blind
  swap (see the promotion roadmap Phase 2 for the analysis).
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
- **Still bridge-owned (the remaining, higher-risk slice):** routing the bridge's JS `Range` object and
  its `RangeState` (in `DomBridge/Traversal.cs`) through the new canonical `DomRange` so the bridge stops
  owning its own content-operation machinery. This is entangled with the bridge's range geometry /
  client-rect APIs (`GetClientRectsForRange`, which stay bridge-owned) and JS object identity, and — like
  the F3c/F4 cutovers — its failure mode is silent selection/serialization corruption, so it needs the
  WPT range/selection corpus at merge, not just `Cli.Tests`. F3c already widened `RangeState` to canonical
  `DomNode`, which de-risks the boundary-point handoff.

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
- **Still deferred:** routing the live `CSSStyleDeclaration` setters / stylesheet-mutation paths through
  shared CSS APIs (higher-risk, entangled with live CSSOM object identity — a separate slice).

### 2.4 Promotion-candidate backlog (P0–P3) + Open Questions

- The P0–P3 promotion-candidate table in the promotion roadmap (the broader "what else could move to the
  canonical components" backlog).
- The promotion roadmap's **Open Questions** (Open Question #5 — "declare v2" — is now answered; the rest
  remain).

---

## 3. Documentation state (done)

As of 2026-07-11 the four previously-stale roadmap docs are updated to reflect F4 complete:
`htmlbridge-domelement-facade-removal-plan.md` (status → implemented), `htmlbridge-blocked-items-completion-roadmap.md`
(Milestones 1.2/1.3 → done, Track 1 complete), `htmlbridge-dom-css-promotion-roadmap.md` (Phase 5
adapter-removal → done), and `rf-bridge-1b-layout-unification.md` (header + increment 6/7 BLOCKED labels
cleared). This roadmap is the forward-looking companion.
