# HtmlBridge DOM/CSS Promotion Roadmap

Status: **active** — Phases 0–5 are done; Phase 5's adapter removal merged to `main` (PR #1359,
2026-07-12). Remaining promotion candidates are tracked in
[`htmlbridge-remaining-work-roadmap.md`](htmlbridge-remaining-work-roadmap.md).
Date: 2026-07-09 (last updated 2026-07-12)

## Purpose

Investigate what remains in `Broiler.HtmlBridge.*` that can move into `Broiler.DOM.*`, `Broiler.CSS.*`, or a nearby shared component, and define a staged roadmap that avoids re-packaging compatibility code as if it were canonical engine code.

## Executive verdict

Yes, there are still candidates to promote out of `Broiler.HtmlBridge.*`, but most of them are small neutral algorithms, public helper APIs, or state projections. The large canonical moves have already happened:

- `Broiler.DOM` owns the canonical DOM tree, mutations, ranges, traversal primitives, parsing hooks, and serialization direction.
- `Broiler.Dom.Html` owns the HTML parser facade used by bridge compatibility code.
- `Broiler.CSS` owns CSS syntax, selectors, declarations, values, diagnostics, parsing, serialization, and the stylesheet rule model.
- `Broiler.CSS.Dom` owns selector matching, cascade, and computed style over canonical DOM nodes.

The remaining `HtmlBridge` code should split into four buckets:

1. Move neutral CSS helpers and duplicated computed-style algorithms into `Broiler.CSS` or `Broiler.CSS.Dom`.
2. Move neutral DOM algorithms into `Broiler.DOM` when they do not require JavaScript object identity, callbacks, layout geometry, or bridge runtime state.
3. Keep JavaScript wrappers, live CSSOM object identity, resource loading, events, timers, host integration, and layout/paint bridge code in `HtmlBridge` or route them to `Broiler.Layout` and media/graphics roadmaps.
4. Delete compatibility adapters and dead rendering code at the next public-surface boundary instead of promoting them.

## Current boundary facts

The repository already documents the intended split in:

- `Broiler.DOM/README.md`
- `Broiler.CSS/README.md`
- `docs/architecture/htmlbridge-engine-boundaries.md`
- `docs/roadmap/broiler-css-component.md`
- `docs/roadmap/refactor-gap.md`
- `docs/roadmap/rf-bridge-1b-layout-unification.md`

The important consequence is that `Broiler.HtmlBridge.Dom.DomElement` and `HtmlTreeBuilder` are compatibility adapters, not desirable destination APIs. They should stay as bridge shims until `htmlbridge-public-surface/v2`, then be removed once callers have migrated to canonical DOM APIs.

## Promotion Candidates

| Priority | Current area | Candidate owner | Move | Why |
| --- | --- | --- | --- | --- |
| P0 | `DomBridge.ParseStyle`, `IsAcceptableCssValue`, CSS priority helpers, property name casing helpers | `Broiler.CSS` or `Broiler.CSS.Dom` | Expose a shared inline declaration/parser utility and declaration validator | Bridge duplicates CSS value validation and inline-style handling that should be one canonical CSS behavior. |
| P0 | `AnchorRegistry.GetComputedProps`, bridge CSS initial/inherited/default tables, shorthand expansion, attr/length helpers | `Broiler.CSS.Dom` | Replace bridge local computed-style projection with shared computed/cascaded style APIs | Anchor/layout consumers still rebuild a CSS engine subset in bridge, including tables and helper logic that already exist in `CssStyleEngine`. |
| P1 | CSSOM rule metadata in `StyleSheets.cs` | `Broiler.CSS` or new `Broiler.CSS.Cssom` namespace | Add engine-neutral rule projection APIs for rule kind, descriptors, nested rules, namespace URI, and property rule metadata | Bridge should build JavaScript objects, but it should not serialize and re-parse shared rule model details to discover neutral metadata. |
| P1 | `classList` token parsing and mutation behavior | `Broiler.DOM` | Add a DOMTokenList-style ordered-token helper over attributes | The ordered-set token rules are DOM semantics; the JavaScript wrapper and callback plumbing stay in bridge. |
| P2 | Mutation observer option matching and record filtering | `Broiler.DOM` | Add neutral filtering over `DomMutationRecord` | Canonical DOM already emits mutation records. Bridge should convert filtered records to JS callbacks, not own the filtering algorithm. |
| P2 | Range content operations and traversal state | `Broiler.DOM` | Expand canonical `DomRange` and route TreeWalker/NodeIterator wrappers through canonical traversal | `DomRange`, `DomTreeWalker`, and `DomNodeIterator` exist in DOM, but bridge still owns several content/traversal algorithms. Geometry APIs such as `getClientRects()` stay bridge/layout-owned. |
| P2 | Stylesheet scope assembly without fetching **(DONE 2026-07-12)** | `Broiler.CSS.Dom` | `CssStyleScopeBuilder` — media-gated, origin/order-preserving, change-detected engine sync (host supplies text). See [`htmlbridge-remaining-work-roadmap.md`](htmlbridge-remaining-work-roadmap.md) §2.4. | CSS.Dom can own style/link collection, ordering, media/supports evaluation, and engine synchronization if the host supplies resource text. Fetching remains bridge/host code. |
| P3 | HTML serialization policies | `Broiler.Dom.Html` | Move only standard serialization policy helpers | Render-specific or Acid-test compatibility transforms should stay bridge-owned unless they are standard DOM/HTML behavior. |
| Deferred | Image decoder, SVG parser/renderer, canvas helpers | `Broiler.Media`, `Broiler.Graphics`, or existing media roadmap | Do not move to DOM/CSS | These are shared engine capabilities, but not DOM or CSS component responsibilities. Align with the media/graphics roadmap. |

## Non-candidates

Do not move these into `Broiler.DOM` or `Broiler.CSS`:

- `Broiler.HtmlBridge.Dom.DomElement`: compatibility facade over canonical DOM; remove at the public v2 boundary instead of promoting.
- `HtmlTreeBuilder`: compatibility adapter over `Broiler.Dom.Html.HtmlDocumentParser`; remove when callers parse into canonical DOM directly.
- JavaScript object wrappers for DOM, CSSOM, events, ranges, iterators, style declarations, and collections.
- `ElementRuntimeState` as a whole. It holds JavaScript identity, listeners, form state, scroll state, layout cache, dialog/shadow state, stylesheet runtime state, animations, and document-type runtime state.
- Resource loading, including external stylesheet fetching.
- Layout metrics, hit-testing, scroll geometry, paint staging, and rendering logs. These should follow `Broiler.Layout` or rendering roadmaps, not DOM/CSS extraction.
- `CssBoxModel`, `CssTextProperties`, and `RenderingStages`. Existing roadmap notes mark these as obsolete/dead compatibility surface for RF-BRIDGE-1a. They should be retired or deleted at the public-surface boundary, not promoted.

## Roadmap

### Phase 0: Lock the boundary and baseline

Status: **done** (2026-07-09). The compatibility-seam inventory, caller catalog,
and doc reconciliation live in
[`docs/architecture/htmlbridge-engine-boundaries.md`](../architecture/htmlbridge-engine-boundaries.md)
("DOM/CSS promotion — Phase 0 baseline"); the guard/inventory tests are
`Broiler.Cli.Tests.HtmlBridgePromotionPhaseZeroTests`.

Goal: make the extraction safe and prevent new bridge duplication.

Tasks:

- Add an inventory test or architecture note listing the remaining allowed `HtmlBridge` compatibility seams: `DomElement`, `HtmlTreeBuilder`, `CssRules`, `CalculateSpecificity`, CSSOM JS wrappers, runtime state, and host loading.
- Reconcile `docs/architecture/htmlbridge-engine-boundaries.md` with current code. The doc says traversal and range state are canonical DOM algorithms, but bridge still owns range/traversal state and mutation observer conversion paths.
- Catalog public callers of `DomElement`, `HtmlTreeBuilder`, `CssRules`, and `CalculateSpecificity` before any public-surface changes.
- Add guard tests that `Broiler.DOM` does not reference JavaScript engine types and `Broiler.CSS.Dom` does not reference `Broiler.HtmlBridge.*`.

Exit criteria:

- The intended compatibility surface is documented with removal boundaries.
- New work has a visible rule for whether it belongs in DOM/CSS, bridge, layout, or media/graphics.

### Phase 1: Promote CSS declaration utilities

Status: **exit criteria met** (2026-07-09). `Broiler.CSS.Dom.CssDeclarationValidator`
now exposes the cascade engine's `IsAcceptableDeclarationValue` (single source of
truth); `DomBridge.ParseStyle` routes through it and the bridge's
`IsAcceptableCssValue` / `IsWhiteSpaceValue` / `IsTextTransformValue` duplicates
are deleted. Covered by `Broiler.CSS.Dom.Tests.CssDeclarationValidatorTests`
plus the existing bridge inline-style/Acid3 compatibility tests (verified
regression-free against a no-change baseline). Deferred to PR slice 2 (does not
gate the exit criteria): the illustrative `CssPropertyNames.To*PropertyName`
casing helpers, `CssPriority.Parse/Apply/Strip`, and routing the live
`CSSStyleDeclaration` setters / stylesheet-mutation paths through shared APIs.

Goal: remove bridge-owned CSS parsing and validation duplicates.

Tasks:

- Add a shared API in `Broiler.CSS` or `Broiler.CSS.Dom`, for example:
  - `CssInlineStyleParser.ParseDeclarations(...)`
  - `CssDeclarationValidator.IsAcceptableDeclarationValue(...)`
  - `CssPropertyNames.ToCssPropertyName(...)`
  - `CssPropertyNames.ToDomPropertyName(...)`
  - `CssPriority.Parse/Apply/Strip(...)`
- Move or expose the newer validation logic currently private in `CssStyleEngine.Values.cs` instead of preserving the narrower bridge version.
- Route `DomBridge.ParseStyle`, style attribute parsing, `CSSStyleDeclaration` setters, and stylesheet mutation paths through the shared API.
- Preserve bridge diagnostics by passing a callback or diagnostic sink into the shared parser/validator.
- Keep JavaScript wrapper behavior in bridge.

Exit criteria:

- Bridge no longer owns `IsAcceptableCssValue` or duplicate inline declaration validation.
- Inline style behavior is covered by CSS unit tests and bridge compatibility tests.

### Phase 2: Promote computed-style projections used by anchor/layout code

> **Update (2026-07-12):** two claims below are superseded — see
> [`htmlbridge-remaining-work-roadmap.md`](htmlbridge-remaining-work-roadmap.md) §2.1 for current state.
> (a) **Shorthand expansion is now shared** — the bridge's `ExpandCssShorthands` was deleted and delegates
> to `CssStyleEngine.ExpandShorthands` (the "deliberately still bridge-owned" note below is stale).
> (b) The **form-control-sizing "needs layout" blocker is stale** — `CssStyleEngine.Computed.cs` already
> has a layout-free `ApplyApproximateFormControlComputedSizes` inside the CSS.Dom boundary. The additive
> `CssStyleEngine.GetSparseComputedStyle` projection (the null-for-undeclared view the `GetComputedProps`
> consumers need) has landed; a differential parity test scoped the remaining cutover to a four-class
> delta (UA display defaults, full-vs-sparse inheritance, value resolution, custom properties).

Status: **partially delivered** (2026-07-09). The two computed-style *tables*
that the bridge duplicated verbatim from `CssStyleEngine` — CSS initial values
and the inherited-property set — are promoted into the shared public
`Broiler.CSS.Dom.CssComputedDefaults`; the engine and the bridge's
`GetComputedProps`/`ApplyInheritedProperties` now consume that single source, and
the bridge's private copies are deleted (they had already drifted — the bridge's
initial-value table carried `text-align-last: auto` the engine's lacked; the
shared table reconciles to the superset). Covered by
`Broiler.CSS.Dom.Tests.CssComputedDefaultsTests`; verified regression-free against
a no-change baseline on the computed-style-sensitive suite.

**Deliberately not done — the literal `GetComputedProps` → `GetComputedStyle`
cutover.** Investigation showed it is not safely achievable as written:

- `GetComputedProps` is the bridge's *central* computed-style accessor (~200 call
  sites across `LayoutMetrics`, `HitTesting`, serialization, traversal, and the
  anchor resolver), and consumers rely on its **sparse-map** contract (an
  undeclared property reads back `null`). `GetComputedStyle` returns a
  full-initials map, so a blind swap would silently shift every consumer (e.g.
  `content` reads `"normal"` instead of `null`).
- The bridge's `ApplyApproximateFormControlComputedSizes` uses rendered-text and
  select-listbox metrics that need layout/text access `Broiler.CSS.Dom` must not
  take on ("Keep layout used-value resolution and box geometry outside CSS.Dom").
  Moving it would either lose that behavior or violate the component boundary.

Two of the remaining neutral helpers are now also shared, output-preserving:

- **`attr()` length substitution** — the engine's `ResolveLengthAttrFunctions`
  is exposed as a public static (the bridge's identical copy, plus its regex and
  `IsRecognizedLengthValue`, are deleted and routed to it).
- **User-agent `display` defaults** — the tag→`display` table moves into the
  public `Broiler.CSS.Dom.CssUserAgentDefaults.DisplayValues`; the bridge's
  private table is deleted and its `ApplyUserAgentDisplayDefaults` reads the
  shared table.

Both verified regression-free (`CssComputedDefaultsTests`, the `attr()` and
form-control display paths, geometry-parity tests, computed-style subset diff).
**Shorthand expansion is deliberately still bridge-owned**: the engine's
`ExpandCssShorthands` has drifted to a superset (it expands `outline`, which the
bridge's does not and which the bridge *does* serialize), so sharing it is not
output-preserving without a reconciliation pass. Form-control sizing and the
sparse-map orchestration also stay bridge-owned per the layout boundary.

Goal: stop rebuilding computed style in bridge for anchor positioning and layout-adjacent consumers.

Tasks:

- Add any missing public projection needed by bridge consumers to `CssStyleEngine`, such as a stable computed-style map view, cascaded declaration view with inline style included, or an anchor/layout style projection.
- Replace `AnchorRegistry.GetComputedProps` with `GetSyncedScopedEngine(...).GetComputedStyle(...)` or a new shared projection.
- Remove bridge-local copies of CSS initial values, inherited property sets, user-agent display defaults, shorthand expansion, attr substitution, approximate form-control sizing, and CSS length parsing where shared CSS.Dom APIs cover the behavior.
- Keep layout used-value resolution and box geometry outside CSS.Dom.

Exit criteria:

- Anchor positioning and layout-adjacent callers consume shared CSS.Dom computed/cascaded style behavior.
- The bridge has no private computed-style table that can drift from `CssStyleEngine`.

### Phase 3: Split neutral CSSOM rule projection from JS wrappers

Status: **done** (2026-07-09). `Broiler.CSS.Cssom.CssomRuleMetadata` is the neutral
projection over `CssRule`: rule kind + CSSOM numeric type, style-rule selector
text, `@import` href/media, `@namespace` prefix/URI, `@keyframes` name, and
`@charset` encoding — computed from the parsed model, no serialize→reparse. The
`@media`/`@supports`/`@layer`/`@page` preludes and `@property`/`@counter-style`
descriptors are read from `CssAtRule.Prelude`/`Declarations` directly.
`StyleSheets.cs`'s model-path `BuildCssRuleObject`/`BuildCssKeyframeRuleObject`
now build the JS CSSOM objects from that projection instead of
`CssSerializer.Serialize(rule)` + `Substring` re-parsing (declaration blocks still
feed the JS `CSSStyleDeclaration` via `ParseStyle` on the serialized block, which
is unchanged). Live object identity, parent wiring, mutation, and the cssText/
callback surfaces stay bridge-owned. Verified by
`Broiler.CSS.Tests.Cssom.CssomRuleMetadataTests` (23) plus ~350 CSSOM CLI tests
with no new failures. The **string-input path** (nested `insertRule`, where JS
supplies raw text with no model rule yet) still parses that text and is the
one remaining substring path; unrecognized at-rules (`@container`,
`@-webkit-keyframes`) fall back to it to preserve current behavior. The
open-question answer: the projection lives under a dedicated `Broiler.CSS.Cssom`
namespace (the CSS architecture guard now permits the component's own
sub-namespaces).

Goal: keep CSS rule semantics in CSS while retaining bridge-owned live JavaScript wrappers.

Tasks:

- Add a neutral projection API around `Broiler.CSS.CssRule` for:
  - rule kind and CSSOM numeric type
  - selector text and media/supports/layer metadata
  - nested rule access
  - property rule descriptors
  - namespace prefix and URI
  - keyframe selector and declaration access
- Update `StyleSheets.cs` to build JavaScript CSSOM objects from the projection instead of string serialization and local metadata parsing.
- Keep live object identity, parent rule/sheet wiring, mutation events, and JS callback surfaces in bridge.

Exit criteria:

- CSSOM JS wrappers are thin adapters over shared rule metadata.
- No bridge code needs to parse serialized CSS text to discover model metadata that `Broiler.CSS` already knows.

### Phase 4: Promote DOM token, mutation, range, and traversal algorithms

Status: **token list + mutation filtering done; slice 8 partially done**
(2026-07-09; PR slices 6–7, part of 8).

Slice 8 findings/work:
- **`TreeWalker`/`NodeIterator` routing is already canonical** — the bridge's
  `BuildTreeWalker`/`BuildNodeIterator` already construct
  `Broiler.Dom.DomTreeWalker`/`DomNodeIterator` and only wrap them with JS
  filters/object identity (no bridge-owned traversal state to promote).
- **Canonical `DomRange` boundary foundation added.** The stub gains the standard
  boundary-point surface — `SetStart`/`SetEnd`, `StartContainer`/`StartOffset`/
  `EndContainer`/`EndOffset`, `Collapsed` — over the range's already-present live
  "removing steps" adjustment (`OnMutation`). Covered by `DomRangeTests` (5). This
  also resolves the Range portion of the pre-existing `DomKernelTests` break.
- **Canonical content operations added (2026-07-12).** `Broiler.Dom.DomRange` now
  implements the DOM Standard §4.5 content operations directly —
  `ExtractContents`/`CloneContents`/`DeleteContents`/`InsertNode`/`SurroundContents`,
  plus `SelectNode`/`SelectNodeContents`/`Collapse` and `CommonAncestorContainer` —
  over canonical `DomNode`/`DomCharacterData` (no JS-object or layout dependencies),
  following the spec algorithms rather than the bridge's ad-hoc document-order
  heuristics. Covered by 13 new `DomRangeTests` (project green 64/64). See
  [`htmlbridge-remaining-work-roadmap.md`](htmlbridge-remaining-work-roadmap.md) §2.2.
- **Bridge rewire done (2026-07-12).** `RangeState` is deleted; the JS `Range` is backed
  by a `BridgeDomRange : Broiler.Dom.DomRange` subclass (non-tracking) that overrides the
  node-creation seams to mint bridge nodes (`#document-fragment` fragments + `CloneDomElement`
  clones carrying host runtime state, registered in `_knownNodes`). All `Range` callbacks
  delegate to the canonical boundary/selection/content methods; the bridge's ad-hoc extract
  helpers and boundary math are removed; external-mutation adjustment still flows through the
  weak `_activeRanges` registry via `DomRange.NotifyNodeRemoved`. Geometry/client-rects stay
  bridge-owned. Regression-free vs the `Cli.Tests` range/mutation/Acid suites; still wants the
  WPT range/selection corpus at merge. See
  [`htmlbridge-remaining-work-roadmap.md`](htmlbridge-remaining-work-roadmap.md) §2.2.

`Broiler.Dom.DomTokenList` is the canonical ordered-set token algorithm
(ASCII-whitespace parse/serialize, unique-ordered, contains/add/remove/toggle/
replace, empty/whitespace validation, attribute synchronization), covered by
`Broiler.Dom.Tests.DomTokenListTests` (18). The bridge's `classList`
`add`/`remove`/`toggle`/`contains` callbacks now delegate to it (and gain
`classList.replace`), keeping only JS argument marshaling, the lenient empty-token
skip, and the style-scope invalidation callback. Behavior-preserving: `ClassName`
is exactly `get/setAttribute("class")`, which the token list uses, so mutation
records and style invalidation are unchanged. Verified regression-free across the
Acid3 `classList`, DOM, and mutation-observer CLI tests (232/233; the one failure
is the pre-existing `Border_Shorthand` baseline case).

**Mutation-observer filtering (slice 7):** `Broiler.Dom.DomMutationObserverFilter`
+ `DomMutationObserverOptions` own the option-matching gate (type flags,
target/subtree scope via `DomNode.IsDescendantOf`, `attributeFilter`, and
`CapturesOldValue`), covered by `DomMutationObserverFilterTests` (8). The bridge's
three `Notify*MutationObservers` paths now build a canonical `DomMutationRecord`
and delegate the delivery decision to `DomMutationObserverFilter.Matches`; the
bridge's private `MutationObserverOptions` struct and `ShouldNotifyMutationObserver`
are deleted (it uses the canonical options type). Behavior-preserving — the bridge
sets no `attributeFilter`, so matching is identical to before; the filter's
`attributeFilter` support is spec-complete and tested for a future wiring.
Verified by the 10 `MutationObserver` CLI tests (childList, attributes with/without
old value, characterData) plus the DOM/mutation subset (no new failures).

Note: the `Broiler.DOM` test project previously had a **pre-existing** compile
break in `DomKernelTests.cs` (stale references to `DomRange` boundary members,
`DomException.Name`, `DomElement.Prefix`, `DomDocument.GetElementsByTagName`). This
is now **fixed** — the missing members were legitimate standard DOM API and have
been added to the canonical model, so the whole DOM test project compiles and runs
(**51 tests pass**, including `DomKernelTests`, `DomTokenListTests`,
`DomMutationObserverFilterTests`, and `DomRangeTests`).

Goal: move DOM-standard behavior out of JavaScript bridge code while keeping host callbacks and layout geometry in bridge.

Tasks:

- Add a `DomTokenList` or ordered-token helper in `Broiler.DOM` for `classList`-style validation, add/remove/toggle/replace, and attribute synchronization.
- Add mutation observer option matching/filtering over `DomMutationRecord`. Bridge should only adapt filtered records into JavaScript callback objects.
- Expand canonical `DomRange` with neutral content operations currently implemented in bridge where they do not require JS wrappers or layout.
- Route bridge `TreeWalker` and `NodeIterator` objects through canonical DOM traversal implementations, with bridge handling JavaScript filters and object identity.
- Keep range geometry APIs, client rect generation, and visual selection behavior in bridge/layout.

Exit criteria:

- DOM-standard algorithms are covered in `Broiler.DOM` tests.
- Bridge traversal/range files mostly contain JS object construction, callback adaptation, and layout-specific APIs.

### Phase 5: Public-surface cleanup

Status: **adapter-removal complete and MERGED** (2026-07-12). All three
workstreams — RF-BRIDGE-1a dead-paint removal, RF-BRIDGE-1b geometry unification (Item 2), and the v1
compatibility-adapter removal (`DomElement`/`HtmlTreeBuilder`/`CssRules`/`CalculateSpecificity`) — are
done and `Broiler.Cli.Tests`-verified. Phase 5's exit criteria are met; the F4 facade-removal stack passed
the WPT + Acid + pixel merge gate and **merged to `main` as PR #1359 (`ecbdf406`, 2026-07-12)**. Remaining
promotion candidates beyond this phase are tracked in
[`htmlbridge-remaining-work-roadmap.md`](htmlbridge-remaining-work-roadmap.md).

- **DONE — delete the RF-BRIDGE-1a dead paint pipeline.** The bridge's parallel,
  runtime-unused box model + paint pipeline is removed:
  `Broiler.HtmlBridge.Rendering/CssBoxModel.cs`, `CssTextProperties.cs`, and
  `RenderingStages.cs` (~29 public types: `CssBoxModel`/`LayoutBox`/`BoxDimensions`/
  `Rect`/the CSS enum family, `CssFontFace`/`CssFontFaceCollection`, and
  `Painter`/`Compositor`/`RenderOutput`/`PaintCommand*`) are deleted, along with the
  last live caller — `ImagePipeline.GetViewBox` (dead: the JS `viewBox` property
  parses the attribute inline and never called it). Verified: a full-codebase sweep
  of all ~29 type names found no remaining consumers (`Broiler.DevConsole.BoxEdges`
  is an unrelated same-named type in its own namespace); `Broiler.HtmlBridge.Rendering`
  and `Broiler.Cli.Tests` build clean; `RenderingPipelineTests` (its comment-only
  reference is stale but harmless) and the phase-zero guards pass. The two
  `CssExtractionPhaseZeroTests.Phase7_*` failures are **pre-existing at HEAD**
  (legacy `Broiler.HTML.CSS`/`CssData` environmental state), baselined as unrelated.

- **DONE — remove the v1 compatibility adapters** (`htmlbridge-public-surface/v2`):
  `Broiler.HtmlBridge.Dom.DomElement`, `HtmlTreeBuilder`, the obsolete `CssRules`
  tuple view, and the bridge-only `DomBridge.CalculateSpecificity` — all deleted
  (`CssRules`/`CalculateSpecificity` at Milestone 1.1; the `DomElement` facade +
  `HtmlTreeBuilder` at Milestones 1.2/1.3 = facade-removal Phase F4, 2026-07-11).
  **Update (2026-07-10, session 95a4149e): the v2 boundary is DECLARED** (Open
  Question #5 answered by the maintainer; see
  `docs/architecture/htmlbridge-engine-boundaries.md`), and the two zero-caller shims
  — `DomBridge.CssRules` and `DomBridge.CalculateSpecificity` — are **REMOVED**
  (Milestone 1.1): the two `CssRules` test consumers were rerouted to the shared
  `Broiler.CSS` parser and the phase-zero guards flipped to assert removal. The
  `DomElement`/`HtmlTreeBuilder` facade removal (Milestones 1.2/1.3) still follows the
  RF-BRIDGE-1b geometry unification — now unblocked (Item 2 complete) and detailed in a
  dedicated staged plan:
  [`htmlbridge-domelement-facade-removal-plan.md`](htmlbridge-domelement-facade-removal-plan.md)
  (Phases A–F, strangler via transitional bridge helpers). `DomElement`
  alone was referenced by **58 non-submodule source files** and is entangled with
  RF-BRIDGE-1b geometry keying, so it cannot be ripped out behind a single verifiable
  change; it is a staged migration, not a deletion.
  `HtmlTreeBuilder`/`CssRules`/`CalculateSpecificity` removal follows once callers
  no longer need the facade node type.
  **Complete (2026-07-11):** all facade members were relocated (Phases A/B/C/E1/D1/E2/C2/F1 →
  `ElementRuntimeState` or canonical DOM), text/comment nodes flipped to canonical `DomText`/`DomComment`
  (F3c part 2), and element construction flipped to the canonical document factories with the facade +
  `HtmlTreeBuilder` **deleted** (F4). Each step is behaviour-preserving and regression-free vs the full
  `Broiler.Cli.Tests` baseline (0 new failures). See
  [`htmlbridge-facade-removal-current-state.md`](htmlbridge-facade-removal-current-state.md) for the
  authoritative record; the WPT/Acid/pixel merge gate passed and the stack merged as PR #1359 (2026-07-12).

  **Sharpened dependency analysis (2026-07-09).** The four adapters split into two
  independent gates, not one:
  - *Transitively gated on BLOCKED item 2 (RF-BRIDGE-1b).* `DomElement` instance
    identity is the dictionary key for every bridge cache — runtime state
    (`ConditionalWeakTable<DomElement, ElementRuntimeState>`, `DomBridge.cs`), JS
    object identity (`_jsObjectCache`, `JsObjects.cs`), computed-style engines/caches
    (`ComputedStyleEngine.cs`, `Css.cs`), the layout-rect/border-box caches
    (`LayoutMetrics.cs`), and the shared geometry snapshot itself
    (`SharedLayoutGeometry.cs`). Removing the facade *type* means re-keying all of
    these off canonical `Broiler.Dom.DomNode` identity — which **is** the geometry
    unification blocked in item 2. `HtmlTreeBuilder` only exists to *materialize*
    `DomElement` nodes (`Build`/`BuildFragment` return `DomElement`), so it is gated
    on the same migration. Neither can land ahead of item 2.
  - *Gated only on the v2 governance decision (Open Question #5), zero code
    migration.* `DomBridge.CalculateSpecificity` has **no production callers** (a
    static delegation shim over `CssSelectorParser.CalculateSpecificity`; the
    phase-zero guard test records this), and `DomBridge.CssRules` has **no
    production callers** either — only two test consumers
    (`CssExtractionPhaseTwoTests`, `SelectorsLevel4SpecificityTests`) plus the
    obsolete-marker guard test. These two are removal-ready the moment v2 is
    declared; only the two test callers of `CssRules` need routing to the shared
    `Broiler.CSS` stylesheet API.

- **DONE — RF-BRIDGE-1b geometry unification (Item 2) is COMPLETE.** All of increments 5–7 have landed:
  `UseSharedLayoutGeometry` on (5), `UseSharedGeometryExclusively` on and the ~2950-LOC `LayoutMetrics`
  recursive estimator body **deleted** (increment 6, PR #1354), and `LayoutRuntimeState` **retired**
  (increment 7). The increment-6 deletion was gated on a real `Broiler.Layout` bug — a
  `display:inline-block` containing a block-level child with any sibling (even a `display:none` `<script>`)
  had its principal box dropped by the CSS 2.1 §9.2.1.1 block-inside-inline correction splitting the
  inline-block instead of keeping it atomic — which was **fixed** in the `Broiler.HTML` submodule (PR #1353,
  the atomic-inline fold in `DomParser.cs`) and, with the box then present in the snapshot, let the
  estimator be removed. Increment 7 retired `LayoutRuntimeState` by relocating its resolved-position-area
  memo to the bridge-level `PositionAreaResolutions` `ConditionalWeakTable` (a pure, behaviour-identical
  storage relocation — the previous "remove the cache and recompute" attempt recursed and was reverted).
  Both changes verified regression-free against full-corpus HEAD baselines (zero new failures by name). The
  three renderer prerequisites (Track 3: abspos-in-inline-CB placement, cross-frame/sub-viewport geometry,
  fixed-target scrollIntoView) all landed. The two-box-model duplication is gone; the only residual
  estimator caller is the shared-*unavailable* fallback (cross-origin / non-materialised frames). See
  [`htmlbridge-blocked-items-completion-roadmap.md`](htmlbridge-blocked-items-completion-roadmap.md)
  Track 2 for the full history. **This unblocks the `DomElement`/`HtmlTreeBuilder` facade removal
  (Milestones 1.2/1.3), the remaining Item 1 work.**

  **Correction (2026-07-09): the previously-documented blocker was stale.** The
  renderer *does* now size `@position-try` elements correctly on the shared path —
  `position-try-002` width+height and `position-try-grid-001` height all match. The
  parity gate now reads **shared 345 / estimator 72 of 484** (the shared path beats
  the estimators by 273 assertions), so `KnownRendererGapRegressions` has been
  lowered from 3 to **0**. Position-try *fallback offsets* are still wrong on the
  shared path, but they are wrong on the estimator path too, so they are not a
  shared-vs-estimator regression and do not gate the deletion.

  **The actual blocker for increments 6–7 is a shared-provider capability gap, not
  position-try sizing.** `SharedLayoutGeometryProvider` only supplies static box
  geometry (border/padding/content boxes for laid-out elements). The estimators —
  and `LayoutRuntimeState` — remain load-bearing for four things the provider does
  **not** supply, so deleting them today regresses real, tested behavior:
  1. **Scroll overflow extents** (`scrollWidth/Height`) — *partially migrated
     (2026-07-09)*: `GetScrollWidth/HeightForDomElement` now answer **non-root,
     unzoomed** elements from the shared snapshot (`TryGetSharedScrollExtent`),
     regression-free. Zoomed subtrees and the root/viewport still use the estimator.
     **Zoom (2026-07-09):** all shared geometry branches gate on
     `IsUnzoomedForSharedGeometry`; zoomed elements use the estimator (path-independent).
     A snapshot-side divide-by-zoom was tried and reverted (double-counts in the render
     pipeline, which bakes zoom into serialized sizes). Correct zoom values locked in by
     `SharedGeometryZoomSizeTests`. Separately fixed an `Element.remove()` NRE (read
     computed `Parent` after detach) that was the real blocker for 4 zoom pixel tests —
     flipped `ScrollMetricsIncludeChildZoomOverflow`, `ClientAndScrollMetricsIncludePadding`,
     `ZoomGeometryApis`, `ZoomIcUnit` green. Then implemented the **render-doc/live-doc
     separation for zoom** (extract zoom baking from the guarded transforms + revert after
     the geometry snapshot so the live/CSSOM doc stays pristine) — flipped
     `ZoomScrollAndOffsetApis`, `ZoomScrollPadding`, `ZoomScrollIntoViewAbsolutePosition`
     green (Zoom suite 7→1; only unrelated `PinchZoom` remains). Zero regressions. See
     rf-bridge-1b §5 incr 6.
  2. **Sticky positioning** (`AnchorResolver/StickyPositioning.cs`) — uses
     `ComputeOffsetWithinAncestor` and the border/content-box estimators.
  3. **`scrollIntoView`** alignment geometry.
  4. **Position-area / anchor resolved geometry** — the anchor resolver
     (`PositionArea.cs`, `PositionAreaQueries.cs`) stores results in
     `LayoutRuntimeState`.

  Increments 6–7 unblock only once the provider/renderer covers scroll-overflow,
  sticky, `scrollIntoView`, and position-area geometry (the deferred "later
  increments" in rf-bridge-1b §5). Until then the estimators stay.

Goal: remove compatibility adapters once consumers are on canonical APIs.

Tasks:

- At `htmlbridge-public-surface/v2`, remove or hide:
  - `Broiler.HtmlBridge.Dom.DomElement`
  - `HtmlTreeBuilder`
  - obsolete `CssRules` tuple view
  - bridge-only specificity compatibility method if shared CSS parser APIs are the public replacement
- Delete RF-BRIDGE-1a obsolete rendering pipeline types instead of moving them. **(done)**
- Finish RF-BRIDGE-1b layout unification by replacing bridge recursive geometry estimators with `Broiler.Layout` read-model access.

Exit criteria — **MET** (WPT/Acid/pixel merge gate passed; merged as PR #1359, 2026-07-12):

- `HtmlBridge` contains bridge responsibilities only: JS integration, compatibility surface, host/resource integration, CSSOM/DOM wrapper identity, and handoff to layout/rendering/media. ✅ (the v1 `DomElement`/`HtmlTreeBuilder` adapters are deleted)
- DOM and CSS components own canonical algorithms and data models without bridge dependencies. ✅ (bridge tree is canonical `Broiler.Dom` nodes)

## Suggested PR Slices

| PR | Scope | Risk |
| --- | --- | --- |
| 1 | Add CSS declaration utility APIs plus tests, no bridge call-site changes | Low |
| 2 | Route inline style parsing and `CSSStyleDeclaration` setters through shared CSS APIs | Medium |
| 3 | Add CSS.Dom computed-style projection for anchor/layout callers | Medium |
| 4 | Replace `AnchorRegistry.GetComputedProps` and delete bridge CSS tables/helpers | High |
| 5 | Add CSS rule projection API and migrate CSSOM object builders | Medium |
| 6 | Add DOM token helper and migrate `classList` wrappers | Low |
| 7 | Add DOM mutation observer filtering and migrate bridge callback preparation | Medium |
| 8 | Expand canonical `DomRange`/traversal use and thin the bridge wrappers | High |
| 9 | Public v2 compatibility cleanup and RF-BRIDGE-1a dead rendering deletions | High |

## Validation Plan

Run focused tests after each phase:

- `Broiler.CSS.Tests`
- `Broiler.CSS.Dom.Tests`
- `Broiler.Dom.Tests`
- `Broiler.Dom.Html.Tests`
- bridge CSSOM and inline-style tests
- selector and specificity compatibility tests
- anchor positioning and layout-adjacent tests
- mutation observer, range, TreeWalker, and NodeIterator tests
- targeted WPT/Acid cases already tracked by the existing roadmaps

Add architecture checks where practical:

- `Broiler.DOM` must not reference JavaScript engine or bridge assemblies.
- `Broiler.CSS` and `Broiler.CSS.Dom` must not reference bridge assemblies.
- `Broiler.HtmlBridge.Dom` should not contain private CSS parser, validator, shorthand, initial-value, or inherited-property duplicates after Phase 2.

## Open Questions

- Should the CSSOM projection live directly in `Broiler.CSS`, or should it be isolated under a `Broiler.CSS.Cssom` namespace to make the browser-API mapping explicit?
- Should `DomTokenList` be a public DOM type or an internal helper consumed by bridge and future DOM APIs?
- Should stylesheet scope assembly include host-provided external stylesheet text in CSS.Dom, or should it remain entirely bridge-owned until more non-bridge consumers exist?
- How much of current bridge serialization behavior is standard HTML serialization versus compatibility transforms for rendering tests?
- When is the project ready to declare `htmlbridge-public-surface/v2` and remove the v1 compatibility adapters?
