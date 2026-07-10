# HtmlBridge `DomElement` Facade Removal — Implementation Plan

Status: proposed
Date: 2026-07-10

## Purpose

Give **Milestones 1.2 and 1.3** of
[`htmlbridge-blocked-items-completion-roadmap.md`](htmlbridge-blocked-items-completion-roadmap.md)
Track 1 — the removal of the `Broiler.HtmlBridge.DomElement` compatibility facade
and its `HtmlTreeBuilder` materializer — a concrete, staged, build-green-at-every-
commit implementation plan. This is the "detailed design" the roadmap defers; it
does not restate *why* the facade is removed (see the promotion roadmap Phase 5 and
the blocked-items Track 1) — it defines *how*.

**Prerequisite status: MET.** RF-BRIDGE-1b geometry unification (Item 2, Milestones
2.1–2.5) is complete and verified (2026-07-10): estimators deleted (PR #1354),
`LayoutRuntimeState` retired (PR #1355), `UseSharedLayoutGeometry` /
`UseSharedGeometryExclusively` both on, §9.2.1.1 fix landed (`Broiler.HTML` PR #205),
Track 3 renderer prerequisites in `Broiler.Layout`. Governance Milestone 1.0
(`htmlbridge-public-surface/v2` declared) and Milestone 1.1 (zero-caller shim removal)
are done. The facade removal is unblocked.

## The facade in one paragraph

`Broiler.HtmlBridge.DomElement` (`src/Broiler.HtmlBridge.Core/Dom/DomElement.cs`) is
`public sealed class DomElement : Broiler.Dom.DomElement`. The bridge's **entire node
tree is built from facade instances**, including text and comment nodes, which the
facade models as `DomElement` with `IsTextNode` set (not canonical `DomText`/
`DomComment`). `HtmlTreeBuilder.ConvertNode` re-materializes the canonically-parsed
tree back into facade nodes. The facade adds a surface of members over canonical
`DomElement` that callers depend on pervasively; removing the type means giving every
one of those members a canonical or bridge-runtime-state home, then flipping
construction to canonical factories.

## Measured scope (2026-07-10)

Established by a full sweep of the non-submodule bridge source (three cluster analyses
plus direct measurement):

- **~800+ facade references across ~24+ non-test files** (52 files reference the name
  `DomElement`, of which the bulk are the facade).
- **76 `new DomElement(...)` construction sites across 14 files.**
- **Zero fully-canonical "type-only" files** in the hot clusters — every anchor-resolver
  and JS-callback file touches at least `.Style` or `IsTextNode`.
- Two couplings are **architectural**, not mechanical:
  1. **`.Style`** — a mutable `Dictionary<string,string>` attached *to the node*
     (~200 read/write sites). Canonical DOM has no node-attached style dictionary.
  2. **text/comment-as-element** — `IsTextNode` (119 sites) + `TextContent` (90 sites);
     canonical uses `DomText`/`DomComment : DomCharacterData` with `.Data`, which are
     **not** `DomElement`.

The facade surface is frozen by the characterization test
`Broiler.Cli.Tests.DomExtractionPhaseZeroTests.Legacy_DomElement_Compatibility_Surface_Is_Explicitly_Frozen`;
each member removal updates that frozen list.

## Facade member inventory → canonical target

| Facade member | Kind | Sites¹ | Canonical / relocation target |
| --- | --- | --- | --- |
| `Style` | `Dictionary<string,string>` on node | ~200 | **ElementRuntimeState-backed** inline-style dict via a bridge accessor `InlineStyle(node)`. |
| `IsTextNode` | `bool` | 119 | `node.NodeType == DomNodeType.Text` (via bridge helper during transition). |
| `TextContent` | `string?` get/set | 90 | `DomText`/`DomComment`.`Data` (via bridge helper during transition). |
| `Attributes` (`LegacyAttributeDictionary`) | string-keyed `IDictionary` | ~120 | canonical `GetAttribute`/`SetAttribute`/`HasAttribute`/`RemoveAttribute` + `Attributes.Values`. |
| `Parent` | typed `DomElement?` get/set | facade subset of 336 | `ParentNode` (get, `as DomElement` where needed); set → `parent.AppendChild`/`InsertBefore`. |
| `Children` | `LegacyChildList : IList<DomElement>` | facade subset of 424 | `ChildNodes` (read); `AppendChild`/`InsertBefore`/`RemoveChild` (mutate); helper for `IndexOf`/typed-children. |
| `NamespaceURI` | `string?` get/set | ~10 | construct via `CreateElementNS`; read `NamespaceUri`. |
| `OwnerDocRoot` | `DomElement?` get/set | 30 | **ElementRuntimeState** (facade-only, no canonical equivalent). |
| `NsAttrMap` | `Dictionary<(ns,local),string>` | 18 | canonical namespaced attributes (`SetAttributeNS`/`GetAttributeNS`); relocate to ERS only if a residue remains. |
| `JsSetStyleProps` | `HashSet<string>` | 11 | **ElementRuntimeState** (facade-only). |
| `InnerHtml` | `string` get/set | 14 | **ElementRuntimeState** (facade-only bridge state). |
| constructors | `new DomElement(...)` | 76 | `document.CreateElement`/`CreateElementNS`/`CreateTextNode`/`CreateComment`. |

¹ "Sites" mixes facade-only counts (exact) with member-name greps that overcount
shared names (`.Parent`/`.Children`/`.Style` also occur on `CssBox`, canonical nodes,
JS objects); treat the tree-shape numbers as upper bounds.

## Canonical API the plan relies on (verified present)

- `DomNode`: `NodeType`, `ParentNode`, `ChildNodes` (`IReadOnlyList<DomNode>`),
  `AppendChild`, `InsertBefore(node, ref)`, `RemoveChild`, `Remove()`, `NodeValue`.
- `DomElement`: `LocalName`, `TagName`, `NamespaceUri`, `Id`, `ClassName`,
  `GetAttribute`/`GetAttributeNS`/`SetAttribute`/`SetAttributeNS`/`HasAttribute`/
  `RemoveAttribute`/`RemoveAttributeNS`, `Attributes` (namespace-keyed).
- `DomText`/`DomComment : DomCharacterData` with settable `Data` (publishes a
  `CharacterData` mutation record); **`internal` constructors** — the bridge must mint
  them via `document.CreateTextNode`/`CreateComment`.
- `DomDocument`: `CreateElement`, `CreateElementNS`, `CreateTextNode`, `CreateComment`,
  `DocumentElement`/`Head`/`Body`, `GetElementById`, `GetElementsByTagName`.

**Note (`ChildNodes` is read-only `IReadOnlyList<DomNode>`):** every `Children.Add/
Insert/IndexOf/Remove/Clear` call migrates to parent-side mutation
(`AppendChild`/`InsertBefore`/`RemoveChild`) or a bridge `ChildIndexOf` helper — there
is no in-place list surface on canonical nodes.

## Core strategy — strangler via transitional bridge helpers

The build must stay green at **every commit**, and no single commit can flip the tree
type. The technique is a strangler:

1. **Introduce a bridge helper** that expresses a facade member's behaviour over the
   canonical *base* type (`DomNode`), working on facade instances today and on
   canonical instances after the flip. Examples:
   - `static bool IsBridgeText(DomNode n) => n.NodeType == DomNodeType.Text;`
   - `static string BridgeText(DomNode n)` / `SetBridgeText(DomNode n, string)` —
     reads/writes `DomText.Data` (falls back to facade `TextContent` while facade
     nodes still exist).
   - `Dictionary<string,string> InlineStyle(DomNode n)` — ERS-backed.
   - `static DomElement? TypedParent(DomNode n) => n.ParentNode as DomElement;`
   - `static int ChildIndexOf(DomNode parent, DomNode child)`.
2. **Migrate call sites** from `element.<facadeMember>` to the helper. Safe while nodes
   are still facade (helper accepts `DomNode`, facade IS-A `DomNode`).
3. **Remove the facade member** once no site references it; update the frozen guard.
4. **Flip construction** to canonical factories *last*, after every member is helper-
   routed — the helpers already work on canonical nodes, so the flip is behaviour-
   preserving.

The critical ordering constraint this produces: **the tree-shape surface
(`Parent`/`Children`/traversal) must be widened from facade `DomElement` to canonical
`DomNode` BEFORE text/comment construction is flipped to `DomText`/`DomComment`** —
because `DomText` is not a `DomElement`, so any code holding children as
`IList<DomElement>` or casting `(DomElement)child` breaks the moment a real `DomText`
enters the tree. Widening traversal to `DomNode` (with facade nodes still in place) is
safe and is the true prerequisite for canonical text.

## Staged plan

Each phase is one or more PRs, each independently building + green. Gate every phase on
the **full** validation set (see "Validation" below), baselined first per `CLAUDE.md`.

**Progress (2026-07-10/11):** Phases A + B + C **DONE** on branch
`claude/rf-bridge-1c-domelement-facade-migration`, each full-`Broiler.Cli.Tests` regression-free
(78-failure baseline; the only diff is the documented `GoogleSearchPolyfillTests`
scrollIntoView-% parallel flake, which passes in isolation).
- **Phase A** — `JsSetStyleProps` + `OwnerDocRoot` → `ElementRuntimeState` (42 sites).
- **Phase B** — inline `.Style` → ERS via `DomBridge.InlineStyle(element)` (lazy-seeds from the
  `style=` attribute on first access; ~200 sites; clone + dialog-backdrop seed explicitly; facade
  `Style` deleted, ignored `style` ctor param retained until Phase F). Perf note: `InlineStyle`
  adds a CWT lookup per style access on hot paths — candidate for a per-pass cache if benchmarks
  regress.
- **Phase C** — `Attributes` (`LegacyAttributeDictionary`) shim removed; ~195 sites moved to
  bridge helpers over the canonical namespace-keyed attribute set (`TryGetAttribute`/`GetAttr`/
  `HasAttr`/`SetAttr`/`RemoveAttr`/`AttributeNames`/`AttributeSnapshot`/`RestoreAttributes`, each
  mirroring the legacy dict's case-insensitive scan-by-qualified-name exactly). `element.Attributes`
  now resolves to the canonical read-only map. **`NsAttrMap` deferred** — its NS-handler rewrite has
  namespace-normalization subtleties; isolate as its own follow-up (Phase C2). Note: canonical
  `GetAttribute`/`SetAttribute` are no-namespace + lowercasing, so raw canonical methods are **not**
  a drop-in for the legacy scan — the helpers are the faithful translation.

Facade now retains: `InnerHtml` (deferred, ctor-coupled → Phase F), `NsAttrMap` (Phase C2),
`IsTextNode`/`TextContent` (Phase D), `Parent`/`Children`/`NamespaceURI` (Phase E).
Next: **Phase E** (widen tree traversal to `DomNode`) or **Phase C2** (`NsAttrMap`).

### Phase A — Relocate facade-only bridge state into `ElementRuntimeState`

Independent, low-risk, no canonical-model change. Proves the relocation pattern and
thins the facade immediately. `GetElementRuntimeState` is `private static`, so even the
nested `CssStyleDeclaration`/`CssStyleMap` JS-object classes can reach it.

- **A1 — `JsSetStyleProps`** (11 sites). Add `HashSet<string> JsSetStyleProps` to
  `ElementRuntimeState`; rewrite `x.JsSetStyleProps` → `GetElementRuntimeState(x).JsSetStyleProps`;
  delete the facade member; drop `"JsSetStyleProps"` from the frozen list.
- **A2 — `OwnerDocRoot`** (30 sites). `DomElement? OwnerDocRoot` → ERS (type stays
  facade-typed for now; re-typed to canonical in Phase F).
- **A3 — `InnerHtml`** (14 sites). Relocate to ERS (or recompute where it is only a
  style-source fallback, e.g. `Css.cs:575`).

**Risk.** Low. **Exit.** Facade loses 3 members; frozen guard updated; all suites green.

### Phase B — Canonicalize `.Style` (node-attached inline style)

- Add an **eager** ERS field `Dictionary<string,string> Style` (init empty,
  `OrdinalIgnoreCase`) and a bridge accessor `Dictionary<string,string> InlineStyle(DomNode)`
  → `GetElementRuntimeState(node).Style`.
- Migrate the ~207 element `.Style` sites (136 writes) to `InlineStyle(node)`.
- Seed at construction where the facade seeds today:
  - `CloneDomElement` (in `DomBridge` partial — copy `InlineStyle(source)` into
    `InlineStyle(clone)`; drop the `style` constructor arg).
  - `HtmlTreeBuilder` element case — parses `style` at construction, but the builder is a
    **separate class** and cannot reach the private static `GetElementRuntimeState`. Seed
    in `DomBridge` after `Build`/`ParseFragment` returns (walk the returned elements, parse
    each node's `style` attribute into `InlineStyle`), or expose an internal seeding hook.
- Delete facade `Style` (and its constructor param); drop `"Style"` from the frozen list.

**Findings that shape this phase (2026-07-10):**
- **The `Style` dict is the authoritative in-memory inline style, not the `style`
  attribute.** It is seeded from `style=` at parse/set time (`Attributes.cs` clears +
  reparses the dict on a `style=` write), mutated directly by JS `element.style`
  (`Utilities.cs` `CssStyleDeclaration`), the anchor resolver, and synthetic form-control
  styling (`DomBridge.Serialization.cs`), and only written **back** to the `style`
  attribute at serialization. Therefore `InlineStyle` must be an **eager persistent** ERS
  dict seeded once — a lazy "parse the `style` attribute on first access" design would
  silently drop unsynced JS mutations (notably on `cloneNode`, whose clone must copy the
  live dict, not re-parse the possibly-stale attribute).
- **`.Style` is NOT a facade-unique member name** — it also occurs on tuples
  (`_zoomSerializationRevertLog`'s `(Element, Style, Attributes)`), computed-props maps,
  etc. So **no blind `sed`** (unlike Phase A's facade-unique `OwnerDocRoot`/
  `JsSetStyleProps`): each `.Style` site needs confirmation it is on a `DomElement` value.
  Migrate file-by-file with review.

**Risk.** High (breadth ~207 sites + construction seeding + non-unique name → per-site
judgment). Split into sub-PRs by cluster (anchor-resolver, JS-callbacks,
serialization/computed-style). **Exit.** No `.Style` on the node; inline style lives in
ERS; computed-style, anchor, animation, serialization suites green.

### Phase C — Canonicalize attribute access (`Attributes` + `NsAttrMap`)

- Migrate string-keyed `element.Attributes[...]`/`.TryGetValue`/`.ContainsKey`/`.Keys`/
  `.Remove` to canonical `GetAttribute`/`SetAttribute`/`HasAttribute`/`RemoveAttribute`
  and `Attributes.Values` (namespace-keyed). Add thin bridge helpers
  (`GetAttr(node,name)`, `AttrNames(node)`) only where they reduce churn.
- Fold `NsAttrMap` bookkeeping into canonical namespaced attributes
  (`SetAttributeNS`/`GetAttributeNS`); relocate any irreducible residue to ERS.
- Remove the facade `Attributes` hide and `NsAttrMap`; drop `"NsAttrMap"` from the
  frozen list.

**Risk.** Medium-High. Heaviest in `ElementInterfaces.cs`, `JsObjects.cs`,
`Attributes.cs`, `Registration.cs`. **Exit.** Attribute access is canonical; XHTML/NS
attribute WPT + Acid cases green.

### Phase E — Widen tree-shape traversal to `DomNode` (prerequisite for D)

*(Runs before D despite the label; kept as "E" to match the member table.)*

- Introduce `TypedParent(DomNode)`, `ChildIndexOf(parent, child)`,
  `ChildElements(node)` (canonical `ChildNodes.OfType<DomElement>()`), and reparent
  helpers wrapping `AppendChild`/`InsertBefore`/`RemoveChild`.
- Migrate `el.Parent` (get) → `ParentNode`/`TypedParent`; `el.Parent = p` (set) →
  `p.AppendChild(el)`/`InsertBefore`; `el.Children.Add/Insert/Remove/Clear` → parent-
  side mutation; `el.Children[i]`/`.Count`/`.IndexOf` → `ChildNodes`/`ChildIndexOf`.
- Re-type internal tree fields and walkers (`_documentNode`, `Elements`,
  `LayoutMetrics` parent-chain walks, `HitTesting`, `Traversal`) from facade
  `DomElement` to canonical `DomNode`/`DomElement` as appropriate. **Do not** flip
  construction yet — facade instances still populate the tree.
- Remove facade `Parent`/`Children`/`NamespaceURI`; drop `"Parent"`/`"NamespaceURI"`
  from the frozen list.

**Risk.** High (largest single surface; `LayoutMetrics.cs` alone is ~119 facade-typed
lines dominated by `.Parent` walks). Split by file cluster. **Exit.** All tree traversal
is `DomNode`-based; the tree can safely contain non-`DomElement` nodes.

### Phase D — Canonicalize text/comment nodes

Now safe because traversal is `DomNode`-based (Phase E).

- Route all `IsTextNode`/`TextContent` sites through `IsBridgeText`/`BridgeText`/
  `SetBridgeText` (introduced in the strategy section, reading `DomText.Data`).
- Flip text/comment **construction**: the ~20 `new DomElement("#text"/"#comment", …,
  isTextNode:true)` sites → `document.CreateTextNode`/`CreateComment`; `HtmlTreeBuilder`
  and `Registration`/`SubDocumentObjects`/`Traversal` text splitting produce canonical
  character-data nodes.
- Rework text-dependent algorithms confirmed by the sweep: `LayoutMetrics`
  `GetDirectTextContent`/select-option text, `Css` style-source/rendered-text,
  `AnchorResolver` `CssCleanup`/`PositionTry` `<style>` text rewrite,
  `InlineContainingBlocks` inline-width estimation, `Traversal` range extraction,
  `JsObjects` `splitText`/CharacterData.
- Remove facade `IsTextNode`/`TextContent`; drop `"TextContent"` from the frozen list.

**Risk.** Highest — the text-as-element model is load-bearing in range/selection,
serialization, and inline layout. Gate hard on WPT text/range/selection + Acid.
**Exit.** Text/comment are canonical `DomText`/`DomComment`; no `IsTextNode` on the node.

### Phase F — Flip element construction, re-key caches, delete the facade (Milestone 1.3)

- Route the remaining element `new DomElement(...)` sites to
  `document.CreateElement`/`CreateElementNS`.
- Rewrite/retire `HtmlTreeBuilder`: its callers (`DomBridge.cs`, `SubDocuments.cs`,
  `Registration.cs`, `SubDocumentObjects.cs`) parse into canonical nodes via
  `Broiler.Dom.Html.HtmlDocumentParser` directly (the parser already produces canonical
  nodes; the adapter's only job was re-materializing facade nodes).
- Re-key the 14 per-node caches (`ElementRuntimeStates`, `_jsObjectCache`,
  `_computedStyleEngines`, `_computedPropsCache`/`_computedPropsInProgress`,
  `_docRootToDocJSObject`, `_smoothScrollTokens`, `_styleSheetCache`,
  `_subDocument*Cache`, `_zoomSpecifiedStyleCache`, `PositionAreaResolutions`) from
  `DomElement` to canonical `DomNode` — now cast-free because
  `FindDomElementByJSObject` and the Registration reverse-lookups return canonical
  nodes. Re-type `OwnerDocRoot`/shadow `RuntimeValue<DomElement>` to canonical.
- Widen the public seam `IDomBridgeRuntime.Elements` (`IReadOnlyList<DomElement>`) and
  `DomBridge.DocumentElement` to canonical `Broiler.Dom.DomElement` (text/comment drop
  out naturally via `OfType<DomElement>()`).
- Delete `DomElement.cs` and `HtmlTreeBuilder.cs`.
- Update/remove the frozen seam assertions in `HtmlBridgePromotionPhaseZeroTests`
  (`DomElement_And_HtmlTreeBuilder_Adapter_Seam_Is_Versioned_And_Frozen`) and
  `DomExtractionPhaseZeroTests` (now asserting removal, not the frozen surface).

**Risk.** High — final cutover. **Exit.** `DomElement`/`HtmlTreeBuilder` gone; promotion
roadmap Phase 5 exit criteria met ("HtmlBridge contains bridge responsibilities only").

## Dependency graph

```
A (facade-only state)  ─┐
B (.Style → ERS)        ─┼─► E (widen traversal to DomNode) ─► D (text→DomText) ─► F (construct+cache+delete)
C (attributes canonical)─┘
```

A, B, C are mutually independent and can land in any order (A first is the cheapest
proof). E gates D (canonical text needs `DomNode` traversal). D gates F (element
construction flips last, once no facade instances are required). F is the only
irreversible step.

## Validation (every phase)

Baseline first (some fail environmentally per `CLAUDE.md`), then require zero new
failures by name:

- Full `Broiler.Cli.Tests` (bridge DOM/CSSOM/anchor/scroll/serialization) — the primary
  gate; the `ScrollIntoView_Treats_Assigned_Slot` slot-scroll crasher stays excluded.
- Full `Broiler.Wpt.Tests` + WPT check-layout + pixel + Acid baselines.
- `Broiler.Dom.Tests`, `Broiler.CSS.Dom.Tests` (canonical algorithms unaffected).
- Guard tests `HtmlBridgePromotionPhaseZeroTests` + `DomExtractionPhaseZeroTests`
  (updated per phase as members are removed).
- `SharedLayoutGeometryParityTests` parity gate + shared-geometry families (the node
  identity underneath geometry caches must stay stable).
- `bridge.mutation` + a geometry-query benchmark for no perf regression (the ERS-backed
  `InlineStyle`/text helpers add an indirection on hot paths — measure).

## Risks & mitigations

- **`.Style` and text helpers on hot paths** (computed style, inline layout, range).
  Mitigate: keep helpers allocation-free; ERS lookup is a `ConditionalWeakTable` hit
  already paid elsewhere; benchmark Phase B and D.
- **Text-as-element is load-bearing in serialization/range** — a wrong `DomText`
  boundary silently corrupts `outerHTML`/selection. Mitigate: Phase D lands behind the
  full WPT range/selection + serialization corpus, cluster by cluster.
- **Giant unreviewable PRs.** Mitigate: every phase splits by file cluster; caller-count
  guard (`grep -rlE '\bDomElement\b' --include=*.cs src/`) trends strictly down and is
  reported per PR.
- **Cache identity drift.** Re-keying (Phase F) must preserve reference identity;
  canonical nodes use reference equality (no `Equals` override), so declared key type
  does not change runtime behaviour — but verify the parity gate after re-key.

## Effort estimate

A: ~1 small PR. B: 2–3 PRs. C: 2 PRs. E: 3–4 PRs (LayoutMetrics/HitTesting/Traversal are
large). D: 3–4 PRs (hardest). F: 2 PRs (construction flip; then delete + guard flip).
**Order of ~13–16 PRs total.** This matches the roadmap's "High risk, staged, 58 files"
framing; it is not a single-session cutover.

## Design decisions (resolved 2026-07-10)

1. **`.Style` home — ERS-backed `Dictionary<string,string>`.** The inline-style dict
   moves into `ElementRuntimeState` behind a bridge `InlineStyle(node)` accessor,
   matching "the node model deliberately does not own this state." (Not backed by
   canonical CSSOM — that would be a larger CSS.Dom change with its own drift surface.)
2. **Text — transition helpers.** Introduce `IsBridgeText`/`BridgeText`/`SetBridgeText`
   over `DomNode`; migrate sites, then flip construction. Keeps every commit green (no
   long red-build window from a single text-model flip).
3. **`HtmlTreeBuilder` — retire entirely in Phase F.** Callers use
   `Broiler.Dom.Html.HtmlDocumentParser` directly (the promotion roadmap's end state).
4. **Cadence — incremental, PR-per-phase-cluster (~13–16 PRs).** Caller-count guard
   trends strictly down and is reported per PR.
