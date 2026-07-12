# HtmlBridge Promotion Backlog Roadmap

Status: **active (low-priority backlog)** — the residual "what else could move from `Broiler.HtmlBridge.*`
into the canonical `Broiler.DOM` / `Broiler.CSS` components" list, after the large HtmlBridge efforts and
the in-scope promotion slices all landed. Nothing here is blocked or urgent; items are picked up
opportunistically. **B1 (P3 serialization) is effectively complete** — the classification audit is done and
the one remaining standard helper (the raw-text element set) was promoted (2026-07-12); B4 is answered by that
audit. **B2 (stylesheet-mutation paths) is audited and confirmed no-promotion** (2026-07-12). **B3's
duplication sweep is done** (2026-07-12) — it deleted three dead duplicated bridge helpers and assessed the one
byte-identical remainder as not cleanly promotable. In short: the backlog is fully triaged — no open promotion
work remains beyond the optional, explicitly-low-value items.
Date: 2026-07-12

## Why this doc exists

Three big HtmlBridge workstreams are **complete and merged to `main`**:

- **v1 public-surface removal** (`DomElement` facade + `HtmlTreeBuilder` deleted) — PR #1359. See
  [`htmlbridge-facade-removal-current-state.md`](htmlbridge-facade-removal-current-state.md).
- **RF-BRIDGE-1b geometry unification** (recursive estimators + `LayoutRuntimeState` deleted) — see
  [`htmlbridge-blocked-items-completion-roadmap.md`](htmlbridge-blocked-items-completion-roadmap.md).
- **The in-scope DOM/CSS promotion slices** (computed-style cutover, canonical `DomRange`, `CssStyleScopeBuilder`,
  CSS helpers) — PRs #1362–#1367. See the now-closed
  [`htmlbridge-remaining-work-roadmap.md`](htmlbridge-remaining-work-roadmap.md).

What is left is the *open-ended* tail of the promotion program: the promotion-candidate rows in
[`htmlbridge-dom-css-promotion-roadmap.md`](htmlbridge-dom-css-promotion-roadmap.md) that were never started
(only P3), a handful of neutral micro-promotions already assessed as low value, and the one design question
still genuinely open. This doc collects them in one place so the completed roadmaps read as "done" and the
residue does not get lost.

## Guiding rule (unchanged from the promotion roadmap)

Move a bridge algorithm into `Broiler.DOM` / `Broiler.CSS` only when it is **neutral** — no JavaScript object
identity, callbacks, layout geometry, resource loading, or bridge runtime state. Keep JS wrappers, live CSSOM
object identity, host/resource integration, and layout/paint code in the bridge. Every promotion must be
output-preserving and gated on the same before/after `Broiler.Cli.Tests` name-diff (plus the dispatch-only
WPT/Acid/pixel corpus when it changes observed behavior). Submodule changes (`Broiler.CSS`, `Broiler.DOM`)
follow the push-or-patch workflow in `CLAUDE.md`.

---

## Backlog items

### B1 — P3: HTML serialization policy helpers → `Broiler.Dom.Html`

**What.** Move only the *standard* HTML-serialization policy helpers into `Broiler.Dom.Html`, leaving
render-specific / Acid-test compatibility transforms bridge-owned.

**Status: B1 effectively complete — audit done, last standard helper promoted (2026-07-12).** After the
promotion below there is **no standard HTML-serialization policy left duplicated in the bridge**; everything
remaining in `DomBridge.Serialization.cs` is either a render-compat transform or a bridge-runtime-state adapter
projection, both correctly bridge-owned.

**Classification audit — DONE (2026-07-12).** The bridge's serialization path (`DomBridge.Serialization.cs`)
was audited rule-by-rule. **Key finding: the standard serialization core was already promoted** —
`Broiler.Dom.Html.HtmlSerializer` already owns the spec 13.3 machinery (the iterative depth-bounded traversal,
`VoidElements`, doctype emission, comment emission, attribute + text HTML-escaping via `Encode`, and
shorthand-first style ordering via `IsShorthandProperty`). The bridge only supplies a thin
`HtmlSerializationAdapter<DomNode>` plus a set of pre-serialization transforms. Every rule classifies cleanly:

| Serializer rule | Class | Disposition |
| --- | --- | --- |
| Iterative traversal, `VoidElements`, doctype/comment emission, attribute + text `Encode` escaping, shorthand ordering | **Standard 13.3** | Already in `HtmlSerializer` (owned by `Broiler.Dom.Html`). |
| **Raw-text element set** (`script`/`style`/`xmp`/`iframe`/`noembed`/`noframes`/`noscript`/`plaintext` serialize text literally) | **Standard 13.3** | **Promoted (this change)** — see below. Was a bridge-private list *and* an incomplete `"script" or "style"` duplicate inside the serializer. |
| `GetKind` text/comment discrimination by `NodeType` | Standard | In the adapter, keyed off canonical `NodeType` — trivial, stays. |
| `GetKind` `#document-fragment`/`#subdoc-root`/`#doctype` sentinel tag-names | Bridge model | Bridge sentinel node model — stays. |
| `GetStyles` from the ERS inline-style map; `GetAttributes` id/class-first ordering, `style` skip | Bridge runtime | Reads bridge `ElementRuntimeState`; deterministic ordering is a bridge policy — stays. |
| `#subdoc-root` skip; `srcdoc` re-serialization; `input` value reflection from the FormControl IDL | Bridge runtime | Nested-browsing-context + form-control runtime state — stays. |
| Zoom baking (`ApplyZoomSerializationStyles`, SVG attr scaling, pseudo overrides); `RemoveRenderCommentNodes`; `progress`/`meter` placeholders; `ReflectRenderState` | **Render-compat transform** | Pixel/reftest fidelity, needs computed style + zoom model — stays bridge-owned (answers **B4**). |

**Promotion landed (2026-07-12) — the raw-text element set.** The one standard-serialization helper still
duplicated in the bridge is now the single source of truth in `Broiler.Dom.Html`:
- Added `HtmlSerializer.RawTextElements` (the standard 13.3 raw-text / escapable-raw-text / legacy set) and
  `HtmlSerializer.IsRawTextElement(tagName)`, as a sibling of `VoidElements`.
- The bridge's private `IsRawTextSerializationParent` list is deleted; it now delegates to
  `HtmlSerializer.IsRawTextElement` (behaviour-identical — same set, same case-insensitive match).
- Reconciled the serializer's own leaf-text branch from the incomplete `tagName is "script" or "style"` to
  `IsRawTextElement(tagName)`, closing a latent gap where `<xmp>`/`<noembed>`/... leaf text would have been
  HTML-escaped against the spec (a no-op for the canonical adapter, whose element nodes return no leaf text).
- **Delivery:** `HtmlSerializer.cs` + `HtmlSerializerTests.cs` are in the `Broiler.DOM` **submodule** — push +
  pointer bump (or `patches/` fallback if the push 403s); the one-line bridge delegation is main-repo.

**Verified regression-free (2026-07-12).** New `HtmlSerializerTests` (13 cases: the raw-text set incl.
case-insensitivity + literal-vs-escape round-trip) green; full `Broiler.Dom.Html.Tests` 18/18; bridge
`innerHTML`/`outerHTML`/DOM-interfaces sweep 50/50; the serialization suite's 8 non-environmental tests green.
The 3 failing `ScriptEngineExecuteTests` zoom/SVG/iframe-srcdoc serialization tests are **pre-existing** —
confirmed failing identically at clean HEAD (stash A/B), environmental per `CLAUDE.md`. Still wants the
dispatch-only WPT `domparsing`/`html/syntax/serializing` + Acid gate at merge (it changes serializer output
only for the rare literal-leaf-text `<xmp>`/`<noembed>` case).

**Exit criteria — MET.** The standard serialization policy lives in `Broiler.Dom.Html` with its own unit tests;
the bridge serializer calls it and retains only render-compatibility transforms and runtime-state projections.

### B2 — Live stylesheet-mutation paths — **audit done; confirmed no promotion needed (2026-07-12)**

**What.** The live CSSOM stylesheet-**mutation** entry points (`insertRule` / `deleteRule` / `cssText`).

**Audit — DONE (2026-07-12).** Traced every stylesheet-mutation entry point in
`DomBridge/JsFunctionCallbacks/StyleSheets.cs` + `DomBridge/StyleSheets.cs`. **All CSS parsing already routes
through the canonical `Broiler.CSS`; no parser or validator is duplicated in the bridge** (a grep for ad-hoc
`;`/`:`/`{` splitting in these files finds nothing):

| Path | Where | Parsing |
| --- | --- | --- |
| `insertRule` (model-backed live `CSSRuleList`) | `JsStyleSheetsInsertRule005Core` | `new Broiler.CSS.CssParser().ParseStyleSheet(ruleText)` → inserts the canonical `CssRule` into the shared model. |
| `insertRule` (nested JS-object rule list) | `JsStyleSheetsInsertRule009Core` → `ruleFactory` → `BuildCssRuleObject(string)` → `ParseCssRuleStrings` | Same `CssParser().ParseStyleSheet`. |
| `deleteRule` (both live and nested) | `JsStyleSheets{DeleteRule006,DeleteRule010}Core` | **No parsing** — `RemoveAt(index)` + index re-sync + `markRulesMutated`. |
| Rule `cssText` **getters** (`GetCssText013…028`) | serialize the live JS CSSOM object graph | Read-only per spec (no rule-level `cssText` setter to duplicate); assembled from live JS wrapper properties, and `BuildCssRuleObject(CssRule)` keys off the canonical `CssomRuleMetadata.GetRuleType` projection (Phase 3) with a serialize→reparse fallback only for `Unknown` at-rule types. |

**Conclusion — nothing to promote.** The only bridge-owned code on these paths is (a) live JS object identity
+ index synchronization (`syncLiveCssRulesIndices`/`SyncIndices`), (b) parent rule/sheet wiring
(`parentStyleSheet`/`parentRule`), (c) mutation dispatch (`markRulesMutated`), and (d) `cssText` serialization
of the *live JS wrapper graph* — all correctly bridge responsibilities per the promotion roadmap Non-candidates
list and the Phase-3 "cssText/callback surfaces stay bridge-owned" decision. The `cssText` getters do
hand-assemble at-rule wrappers by string interpolation, but they read the *live* (independently-mutable) JS
object graph rather than a canonical `CssRule`, so they cannot call `CssSerializer.Serialize(rule)` — the same
"different input shape" reason B3 gives for not consolidating `ParseDeclarations`. **Assessed low value; do not
pursue.**

**Verified (2026-07-12):** the CSSOM `insertRule`/`deleteRule`/stylesheet suite is green (29/30; the lone
failure, `HttpClientMigrationTests.StylesheetLoadHandler_Uses_HttpClient`, is a pre-existing reflection/
assembly-load environmental failure per `CLAUDE.md`, confirmed failing at clean HEAD). No code change; this
entry records the "route stylesheet mutation through shared APIs" Phase-1 task as **verified resolved**.

### B3 — Neutral micro-promotions — **duplication sweep done (2026-07-12)**

**Duplication sweep — DONE (2026-07-12).** Swept the bridge's pure static string/CSS helpers
(`DomBridge/Css.cs`, `DomBridge.cs`, `DomBridge/Utilities.cs`, `LayoutMetrics.cs`) against `Broiler.CSS` for
`StripVendorPrefix`-style byte-identical duplication. Findings:

- **Dead duplicated helpers — DELETED (main-repo only, this change).** Three bridge private helpers had **zero
  callers** anywhere in the `DomBridge` partial class (each duplicated logic that already lives canonically):
  `FindMatchingClosingParen` and `FindTopLevelChar` (`DomBridge/Css.cs`, twins of the private helpers in
  `CssStyleEngine.Values.cs`) and `IsLengthOrPercentage` (`DomBridge.cs`, twin of the `CssStyleEngine.Values.cs`
  length check). Deleting them is behaviour-neutral (private + uncalled — the bridge builds clean, 0 errors; CSS
  computed-style/length suite 53/53). Not a promotion — pure dead-code removal surfaced by the sweep.
- **Byte-identical length-normalize trio — found, NOT promoted (assessed low value).** `NormalizeSingleValueLengthFunction`
  + `TryUnwrapSingleValueFunction` + `HasBalancedParens` in `DomBridge/Css.cs` are **byte-identical** to the
  private helpers of the same name in `Broiler.CSS/CssLengthParser.cs`. Promoting them the `StripVendorPrefix`
  way is *not* a one-line reroute: `CssLengthParser` keeps them **deliberately private**, and the bridge uses
  `HasBalancedParens` independently at three `LayoutMetrics.cs` sites — so promotion means exposing two of
  `CssLengthParser`'s internal paren-helpers as public API and rerouting four call sites across two bridge
  files, to dedup ~40 lines of trivial paren-balancing. Public-surface expansion outweighs the benefit.
  **Recommendation: do not pursue** unless `CssLengthParser` gains a public value-normalization surface for
  another reason. (`ParseCssLengthToPixels` — 26 bridge call sites, canonical twin in `CssStyleEngine.Computed.cs`
  — is likewise not a clean/safe consolidation.)

**Previously recorded (still stands):**

- **`CssInlineStyleParser.ParseDeclarations` consolidation — assessed marginal (2026-07-12).** Both
  `DomBridge.ParseStyle` and the engine's inline loop already use canonical `CssParser().ParseDeclarations()`
  + `CssDeclarationValidator`; the only unshared code is a ~4-line orchestration loop with *different output
  shapes* (bridge: dict with `!important` suffix + vendor-prefix mapping; engine: cascade winners). The named
  "third copy" `HtmlCss.ParseDeclarations` (`Broiler.Documents.Html`) is a different naive parser serving
  document conversion — forcing it onto the canonical parser would change Documents behavior (out of scope).
  **Recommendation: do not pursue** unless the output shapes converge.
- Any *further* `StripVendorPrefix`-style byte-identical string helpers that surface later are cheap, safe wins
  (promote to `Broiler.CSS.CssPropertyNames`, the pattern set by PR #1366). The 2026-07-12 sweep above found no
  such clean case still open — the only byte-identical duplicate (the length-normalize trio) is not cleanly
  promotable, and the dead duplicates were deleted.

### B4 — design question (carried from the promotion roadmap) — **ANSWERED (2026-07-12)**

> How much of current bridge serialization behavior is standard HTML serialization versus compatibility
> transforms for rendering tests?

**Answered by the B1 classification audit.** The standard §13.3 serialization (traversal, void elements,
raw-text elements, doctype/comment emission, attribute/text escaping, shorthand ordering) is all in
`Broiler.Dom.Html.HtmlSerializer`. The bridge-owned remainder is exactly two kinds: (1) **render-compat
transforms** — zoom baking + SVG-attr scaling + zoom pseudo overrides, `RemoveRenderCommentNodes`,
`progress`/`meter` shadow-DOM placeholders, and `ReflectRenderState` (all need computed style / the zoom model
and exist for pixel/reftest fidelity); and (2) **bridge-runtime adapter projections** — ERS inline style,
`#subdoc-root` handling, `srcdoc` re-serialization, form-control value reflection, and the sentinel-tag node
model. See the B1 classification table for the per-rule split.

### Out of scope (do not promote to DOM/CSS)

Per the promotion roadmap's Non-candidates list and the **Deferred** table row: image decoder, SVG
parser/renderer, and canvas helpers are shared engine capabilities that belong to `Broiler.Media` /
`Broiler.Graphics` and the media/graphics roadmap — **not** DOM or CSS component responsibilities. Likewise JS
object wrappers, `ElementRuntimeState`, resource loading, and layout/paint/hit-testing/scroll geometry stay in
the bridge (or route to `Broiler.Layout`).

The disposition of every out-of-scope surface — destination component, owning roadmap, and status — is
tracked in its own routing map:
[`htmlbridge-out-of-scope-routing-roadmap.md`](htmlbridge-out-of-scope-routing-roadmap.md). None of it is open
DOM/CSS-promotion work.

---

## Validation plan

Same as the promotion roadmap: `Broiler.CSS.Tests`, `Broiler.CSS.Dom.Tests`, `Broiler.Dom.Tests`,
`Broiler.Dom.Html.Tests`, the bridge CSSOM/inline-style/serialization suites, and the targeted WPT/Acid cases
already tracked — baseline first (some fail environmentally per `CLAUDE.md`), then require zero
baseline-passing regressions via a before/after TRX name-diff. Keep the architecture guards green:
`Broiler.DOM` references no JS-engine/bridge types; `Broiler.CSS`/`Broiler.CSS.Dom` reference no bridge types.
