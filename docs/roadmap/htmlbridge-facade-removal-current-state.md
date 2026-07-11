# HtmlBridge `DomElement` Facade Removal — Current State & Handoff

Status: **facade removed — F4 implemented and `Broiler.Cli.Tests`-verified (0 regressions).**
The `DomElement` facade and `HtmlTreeBuilder` are **deleted**; the bridge holds only canonical
`Broiler.Dom` nodes. F3c part 2 (2a–2d) and F4 are all committed on
`claude/htmlbridge-domelement-f3c-flip` but **NOT merged** — the whole F3c/F4 stack awaits the
WPT range/selection/serialization + Acid + pixel gate (WPT is dispatch-only here; see §4/§5 gates).
Last updated: 2026-07-11.

## Decomposition note (F3c part 2)

The original plan framed F3c part 2 as one indivisible big-bang. Ground-truthing the
cascade showed the bulk is **type-widening** (`ChildAt`→`DomNode`, the `ToJSObject`
split, `RangeState`→`DomNode`, `ChildElements`→`OfType`, `CloneDomElement`/tree-helper
widening) that is **safe and green on today's homogeneous facade tree** — a canonical
`DomText` is not yet in the tree, so every widened branch is behaviour-preserving (dead
code exercised only after the flip). Only the **construction flip** (text/comment →
`document.CreateTextNode`/`CreateComment`) + the `TextContent` aggregation split + facade-
member removal are genuinely atomic/irreversible. So part 2 lands as a sequence of green,
`Cli.Tests`-verified widening commits (2a, 2b, …) capped by the one atomic flip gated on
the WPT range/selection/serialization + Acid corpus. This shrinks the irreversible surface
and matches the effort's PR-per-cluster cadence.

This is the standalone "where things stand" snapshot for the RF-BRIDGE-1c effort
(Milestones 1.2/1.3 of the blocked-items roadmap). The *design* lives in
[`htmlbridge-domelement-facade-removal-plan.md`](htmlbridge-domelement-facade-removal-plan.md);
this file is the current-state summary a reviewer or the next implementer can read
on its own.

## 1. What this effort does

`Broiler.HtmlBridge.DomElement` (`src/Broiler.HtmlBridge.Core/Dom/DomElement.cs`) is a
`public sealed class DomElement : Broiler.Dom.DomElement` — a compatibility facade the
bridge builds its **entire node tree** from, including text/comment nodes (modeled as a
facade `DomElement` with a text/comment `NodeType`, not canonical `DomText`/`DomComment`).
`HtmlTreeBuilder.ConvertNode` re-materializes the canonically-parsed tree back into facade
instances. The goal is to delete both types so the bridge holds only canonical
`Broiler.Dom` nodes and bridge-runtime state, by giving every facade member a canonical or
`ElementRuntimeState` (ERS) home, then flipping construction to canonical factories.

## 2. Facade surface: removed vs remaining

| Facade member | Status | Relocated to |
| --- | --- | --- |
| `JsSetStyleProps`, `OwnerDocRoot` | ✅ removed (A) | `ElementRuntimeState` |
| `Style` | ✅ removed (B) | ERS via `InlineStyle(node)` |
| `Attributes` (+`LegacyAttributeDictionary`) | ✅ removed (C) | canonical attributes |
| `Parent` | ✅ removed (E1) | canonical `ParentNode` via `ParentEl`/`SetParent` |
| `IsTextNode` | ✅ removed (D1) | canonical `NodeType` via `IsText(node)` |
| `Children` (+`LegacyChildList`) | ✅ removed (E2) | canonical `ChildNodes` |
| `NsAttrMap` | ✅ removed (C2) | canonical namespaced attributes |
| `InnerHtml` | ✅ removed (F1) | ERS |
| `TextContent` | ✅ removed (F3c part 2d) | canonical `DomText`/`DomComment`.`Data` |
| `NamespaceURI` | ✅ removed (F4) | canonical `NamespaceUri` read + `CreateElementNS` at construction |

`DomElement` and `HtmlTreeBuilder` are **deleted (F4)** — the facade surface is empty and both
types are gone. The bridge holds only canonical `Broiler.Dom` nodes + bridge-runtime state.

## 3. Transitional machinery currently in place

Introduced by the in-progress phases; the next implementer builds on these:

- **Character data:** `BridgeText(DomNode)` / `SetBridgeText(DomNode, string)` read/write a
  text/comment node's data over canonical `DomCharacterData.Data`, with a facade
  `TextContent` fallback while the tree is still facade. `IsText(DomNode)` /
  `IsComment(DomNode)` discriminate by `NodeType`. **All ~80 character-data + comment sites
  already route through these** (F3a) — so the flip only has to change *construction*.
- **Attributes:** `TryGetAttribute`/`GetAttr`/`HasAttr`/`SetAttr`/`RemoveAttr`/`AttributeNames`/
  `AttributeSnapshot`/`RestoreAttributes` (Phase C) and `TryGetNsAttribute` (C2) express the
  old string-keyed + namespaced surface over the canonical namespace-keyed attribute set.
- **Inline style:** ERS-backed `InlineStyle(node)` (Phase B).
- **Node identity (F3b + F3c-part1):** `_knownNodes`, `_jsObjectCache`, `ElementRuntimeStates`
  (CWT) / `GetElementRuntimeState`, `_mutationObservers` targets, `ChildIndexOf`, `ParentEl`,
  `SetParent`, `GetTreeRoot`/`ToJSRootNode`, and the JS Node-navigation callbacks
  (`childNodes`/`firstChild`/`lastChild`/`nextSibling`/`previousSibling`/`isConnected`,
  `nodeType`/`nodeName`) are all typed on canonical **`DomNode`**. `FindDomNodeByJSObject`
  resolves any node from its JS wrapper (`FindDomElementByJSObject` narrows with `as DomElement`).
- **`ToJSObject` split (F3c part 2a — DONE, verified):** the `(DomElement)node` cast is replaced
  by a `node is not DomElement` branch that builds a minimal Node/CharacterData wrapper for
  canonical `DomText`/`DomComment` (`PopulateCharacterDataJSObject`). Dead on today's homogeneous
  tree — facade text/comment are `DomElement` and keep the full element wrapper, so behaviour is
  preserved; goes live at the construction flip. The node-level `*Core` helpers the wrapper shares
  are widened to `DomNode`: `nodeValue`/`data`/`length`/`splitText`/`substring`/`append`/`delete`/
  `insert`/`replaceData`, `localName`/`prefix`/`namespaceURI` (`is DomElement` guards),
  `ownerDocument`, `parentElement`, `contains`, `compareDocumentPosition`, `isSameNode`,
  `normalize` (no-op on char-data), `isEqualNode`, `getRootNode`, `cloneNode`, plus
  `GetNodeTextValue`. The tree/clone leaf helpers they cascade into — `IsDescendant`,
  `CompareTreeOrder`, `CloneDomElement` (now has a `DomCharacterData` factory branch + deep-clones
  over raw `ChildNodes`), `NodesAreEqual`, `FindContainingShadowRoot`, `SetCharacterData`/
  `UpdateCharacterData`/`NotifyCharacterDataMutationObservers` — are widened too; text-node
  construction now funnels through `CreateBridgeTextNode`. The node-argument lookups in
  `contains`/`compareDocumentPosition`/`isSameNode`/`isEqualNode` use `FindDomNodeByJSObject`
  (equivalent today, forward-correct after the flip). **Verified:** full `Broiler.Cli.Tests`
  before/after TRX name-diff — identical 81 env failures, **0 regressed, 0 newly-failing**.
  **Intentionally deferred to F3c part 2b:** the wrapper omits the ChildNode mixin
  (`remove`/`before`/`after`/`replaceWith`) and EventTarget, which entangle with the `RangeState`/
  tree-mutation surface — added with that widening, before the wrapper goes live.

Everything above is **behaviour-preserving on the current homogeneous facade tree** and
verified regression-free against the full `Broiler.Cli.Tests` (1699 tests).

## 4. What remains — F3c part 2 (green widening prefix → atomic flip)

Split into **green, `Cli.Tests`-verified widening** (2a–2c) and the **atomic construction flip**
(2d). Each widening step is behaviour-preserving on the homogeneous tree (the widened branches are
dead until a canonical `DomText` exists). Concrete work:

- ✅ **2a — `ToJSObject` split. DONE + verified** (0 regressions). Canonical char-data gets its own
  minimal wrapper via `PopulateCharacterDataJSObject`; ~25 node-level `*Core` helpers + the
  tree/clone leaf helpers widened to `DomNode` (see §3). The wrapper still **omits** the ChildNode
  mixin + EventTarget (folded into 2b, below).

- ✅ **2b — `RangeState` + tree-mutation cascade → `DomNode`. DONE + verified** (0 regressions).
  Widened `RangeState.StartContainer`/`EndContainer`/`Root` + `AdjustForRemoval`/`IsDescendantOf`
  and the range helpers they feed (`FindCommonAncestor`, `GetNodesInRange`→`List<DomNode>`,
  `CollectRangeText`, `GetDocumentOrderNodes`/`CollectDescendants`→`List<DomNode>` walking raw
  `ChildNodes`, `ExtractStartPath`/`EndPath` with `DomNode` ancestor chains, `IsPositionAfter`/
  `CompareBoundaryPosition`); the NodeIterator surface (`IteratorState`, `GetNextNodeAfter`/
  `GetPreviousNodeBefore`, `ApplyFilter`, `GetNodeType`); the child-mutation helper family's parent
  param (`ChildAt`/`InsertChildAt`/`RemoveChildFrom`/`RemoveNthChild`/`ClearChildren`/`SetParent`);
  and the tree-mutation + EventTarget helpers (`InsertNodeAt`, `NotifyChildAdded`/`NotifyChildRemoved`/
  `NotifyMutationObservers`, `NotifyNodeIteratorPreRemoval`, `AdoptSubtreeIntoDocument`,
  `BuildChildNodeArgumentNodes`→`List<DomNode>`, `GetEventListeners`/`GetInlineEventHandlers`,
  `DispatchEventOnElement`/`FireListeners`/`BuildComposedPathValue`, and the `remove`/`before`/`after`/
  `replaceWith`/`addEventListener`/`removeEventListener`/`dispatchEvent` JS callbacks). The char-data
  wrapper (`PopulateCharacterDataJSObject`) now carries the full ChildNode-mixin + EventTarget surface.
  The four range-clone `(DomElement)` casts from 2a are gone. **Verified:** full `Broiler.Cli.Tests`
  before/after name-diff — 0 regressions across two commits (one interim run showed a lone flaky
  `GoogleSearchPolyfillTests` scroll test that passes on rerun; the final run is an exact 81-failure match).

- ✅ **2c — `ChildAt` *return type* → `DomNode`. DONE + verified** (0 regressions). Fixed the
  heterogeneity sites — **only ~18**, far fewer than the doc's estimated 68 because 2a/2b already
  absorbed most of the cascade (TreeWalker/NodeIterator traverse helpers, `normalize` recursion,
  `HitTesting`, `Css` `SnapshotChildren`, table-section scan, `ScrollSimulation`/`StickyPositioning`)
  narrowing with `is`/`as DomElement`. Narrowed **`ChildElements` → `OfType<DomElement>()`** and
  routed the text-needing callers to raw `ChildNodes` (`CollectTextContent` — textContent aggregation
  now includes direct text children; serialization `GetChildren`; the range/clone walks landed in
  2a/2b). Fixed `ToTraversalJsValue` (returns any non-null node's wrapper) + the `currentNode` cast;
  dropped the obsolete extractContents range-clone casts. Widened the **serialization adapter to
  `HtmlSerializationAdapter<DomNode>`**: `GetKind` keys text/comment off `NodeType` and the special
  kinds off the facade `#document-fragment`/`#subdoc-root`/`#doctype` TagNames; `GetName`/
  `GetAttributes`/`GetStyles`/`GetRawInnerHtml` narrow to `DomElement` (invoked only for element/
  doctype nodes). **Verified:** full `Broiler.Cli.Tests` name-diff, 0 regressions (sole delta the
  known-flaky scroll test), 0 new serialization/range failures.

**The green widening prefix (2a/2b/2c) is COMPLETE.** The tree, JS wrappers, ranges, NodeIterator/
TreeWalker, cloning, adoption, event dispatch, and serialization all handle canonical `DomText`/
`DomComment` correctly. Everything behaviour-preserving is done; only the irreversible flip (2d) —
which actually puts a canonical `DomText` into the tree — remains.

- ✅ **2d — the atomic construction flip. DONE + Cli.Tests-verified; ⚠️ NOT MERGED (awaits WPT gate).**
  Canonical `DomText`/`DomComment` now enter the tree. `CreateBridgeTextNode`/`CreateBridgeCommentNode`
  mint `_document.CreateTextNode`/`CreateComment`; all ~17 text/comment construction sites +
  `HtmlTreeBuilder.ConvertNode` (return + `AllElements` → `List<DomNode>`) route through them. `TextContent`
  element-aggregation split: set → `SetElementTextContent` (clear + append child `DomText`); get →
  aggregate over descendant text. Every element-store read/write removed; facade `TextContent` + the
  `NodeValue` override deleted; frozen guard updated. Text-reading callers routed to raw `ChildNodes`
  (`<style>`/animation CSS gatherers, `AppendRenderedText`, `GetDirectTextContent`, innerHTML/`document.write`/
  adjacent-HTML fragment application, and the `AddElementsRecursive`/`RemoveElementsRecursive`/
  `CollectAllDescendantsFlat` node-registration walks). JS node-arg resolution widened to
  `FindDomNodeByJSObject` where a Node is valid (append/insert/remove/replaceChild, Range boundary nodes,
  TreeWalker `currentNode`, `MutationObserver.observe` — characterData observers fire on text now).
  Serialization escapes text nodes like the old element-store path did, except inside raw-text elements
  (`script`/`style`/…), which stay literal. **Verified:** full `Broiler.Cli.Tests` before/after name-diff —
  **0 regressions and 8 fixes** (canonical text nodes correctly re-evaluate `:last-child`/`:has()`/
  `:nth-child` invalidation on whitespace-text removal); 81 → 73 environmental failures. Commit `72634e02`
  on branch `claude/htmlbridge-domelement-f3c-flip`.

**⚠️ MERGE GATE:** 2d is regression-free on `Broiler.Cli.Tests` (which includes the Acid3 corpus), but the
roadmap requires the **WPT range/selection/serialization + Acid** corpus before this irreversible step
merges (silent `outerHTML`/selection corruption is the failure mode). WPT is dispatch-only in this
environment — run it and confirm green before merging `72634e02`.

**Gate (2d):** the WPT range/selection/serialization + Acid corpus in addition to `Broiler.Cli.Tests`
(silent `outerHTML`/selection corruption is the failure mode). The green steps 2a–2c gate on
`Broiler.Cli.Tests` before/after name-diff alone.

## 5. What remains — F4 (final cutover, irreversible) — grounded sketch

**Goal:** delete `DomElement.cs` + `HtmlTreeBuilder.cs` so the bridge holds only canonical
`Broiler.Dom` nodes + bridge-runtime state. After 2d the facade carries **one** real member
(`NamespaceURI`) plus redundant `Id`/`ClassName` re-exposures — canonical `Broiler.Dom.DomElement`
already has public settable `Id`/`ClassName`, so those `new` overrides just delete.

**Strategy — the same funnel pattern that made 2d safe.** Introduce one
`CreateBridgeElement(tagName, namespaceUri?, id?, className?, attributes?)` helper that does
`CreateElement`/`CreateElementNS` + sets id/className/attributes (the work the facade ctor did
inline), route all **58** `new DomElement(...)` sites through it, then flip the funnel body to
canonical factories. Keeps most commits green and isolates the irreversible flip to one place.

**Measured surface (post-2d, on `claude/htmlbridge-domelement-f3c-flip`):**

1. **Construction flip — 58 sites.** All tag literals are lowercase HTML
   (`html/body/head/div/tr/style/pre/…`) or `#`-sentinels — **no mixed-case/foreign literals**, so
   `CreateElement` (which `ToLowerInvariant()`s) is safe for the real elements. Funnel gotchas:
   - **`#`-sentinels** (`#document`, `#document-fragment`, `#subdoc-root`, `#shadow-root`,
     `#doctype` — ~18 sites): the facade gave these a **null** namespace and preserved the name.
     `CreateElement` would lowercase + apply the HTML namespace → wrong. Use
     `CreateElementNS(null, "#subdoc-root")` (preserves name, null ns). They stay canonical
     `DomElement`s with sentinel tag names — a bridge-internal model, now over canonical types.
   - **id/className/attributes**: funnel does `CreateElement` then `Id=`/`ClassName=`/`SetAttribute`.
2. **`NamespaceURI` → construction (removes the last facade member).** ~7 set-sites
   (`el.NamespaceURI = ns` in `createElementNS` handlers, sub-doc roots, clone) create then
   `SetName` (protected — unreachable from `DomBridge` once the facade is gone); fold the namespace
   into the funnel → `CreateElementNS(ns, name)` at construction. The ~12 read-sites become canonical
   `element.NamespaceUri` (casing change `NamespaceURI` → `NamespaceUri`). Then delete the alias.
3. **Reconcile `_documentNode` vs `_document`.** The bridge holds both a canonical `DomDocument`
   (`_document`) *and* a facade element tagged `"#document"` (`_documentNode`). Recommend F4 keep
   `_documentNode` as a canonical sentinel element (minimal churn) and defer document-node
   unification — it is orthogonal to deleting the facade type.
4. **Re-key ~14 caches** `Dictionary/HashSet/CWT<DomElement,…>` → `<Broiler.Dom.DomElement,…>`
   (`_computedPropsCache`, `_styleSheetCache`, `_subDocument*Cache`, `_computedStyleEngines`,
   `_docRootToDocJSObject`, `_smoothScrollTokens`, `_zoomSpecifiedStyleCache`, `PositionAreaResolutions`
   CWT, `_onloadFired`, …). Pure type-widen (facade IS-A canonical, reference identity unchanged);
   re-check the `SharedLayoutGeometryParityTests` gate after.
5. **Widen public seams to canonical** `Broiler.Dom.DomElement`: `DomBridge.Elements`
   (`IReadOnlyList<DomElement>`), `DomBridge.DocumentElement`, `IDomBridgeRuntime.Elements`. Only
   external consumer to touch: `Broiler.Wpt.Tests/WptTestRunnerTests.cs`.
6. **Retire `HtmlTreeBuilder` (10 callers).** Post-2d `ConvertNode` already mints canonical
   text/comment; once elements are canonical too it becomes an identity re-materialization — pure
   overhead. Callers parse via `Broiler.Dom.Html.HtmlDocumentParser` directly and adopt the tree
   into `_document`. **Spike first (the one non-mechanical piece):** confirm whether `ParseDocument`
   can target `_document` or whether the bridge adopts/imports the parsed subtree (today `Build`
   parses into the parser's own document, then re-parents into `_document`).
7. **Delete `DomElement.cs` + `HtmlTreeBuilder.cs`; update the frozen guards.**
   `DomExtractionPhaseZeroTests` flips from "surface is frozen" to "facade is removed";
   `HtmlBridgePromotionPhaseZeroTests.DomElement_And_HtmlTreeBuilder_Adapter_Seam_Is_Versioned_And_Frozen`
   is removed/repurposed (+ any `HtmlBridgeBoundaryGuardTests`).

**Verification & gate.** Same before/after `Broiler.Cli.Tests` name-diff harness. F4 is mostly
mechanical type/construction widening (lower risk than 2d's text-model change) but **is** the final
irreversible cutover — gate on the **full WPT + Acid + pixel** baselines before merge. Watch for
sentinel-namespace regressions (SVG/foreign `createElementNS`) and any reader of a facade element's
*stored* tag case (all literals lowercase → low risk).

**Sequencing / effort — ~2 PRs.** (a) `CreateBridgeElement` funnel + route all 58 sites +
NamespaceURI-into-construction + cache re-key + seam widen (green, incremental); (b) flip the funnel
to canonical, retire `HtmlTreeBuilder`, delete both files, update guards (the irreversible cutover).
Spike the parser-document-ownership question (item 6) before starting.

### F4 — DONE + `Broiler.Cli.Tests`-verified (0 regressions); ⚠️ NOT MERGED (awaits WPT/pixel gate)

Landed on `claude/htmlbridge-domelement-f3c-flip` as the two commits the sketch predicted:

- **Step (a) — `CreateBridgeElement` funnel (green, `aa00ecf5`).** Added `CreateBridgeElement` /
  `CreateBridgeElementNS` (beside the F3c text/comment funnels) and routed all 58 `new DomElement(...)`
  sites through them; folded the ~7 `el.NamespaceURI = ns` overrides into the NS funnel and converted
  every `NamespaceURI` *read* to canonical `NamespaceUri`. Facade-bodied, behaviour-preserving.
- **Step (b) — atomic cutover (`ddad4769`).** Flipped the funnel bodies to `_document.CreateElementNS`
  (`#`-sentinels → null namespace + preserved name; case preserved exactly). Added
  `global using DomElement = Broiler.Dom.DomElement;` so the ~900 unqualified element-handling sites
  resolve to the canonical type en masse (`is`/`as DomElement` discriminates element-vs-text
  identically; `<DomElement,…>` caches re-key for free); swapped the ~217 qualified
  `Broiler.HtmlBridge.DomElement` refs. **Retired `HtmlTreeBuilder`** — the item-6 spike confirmed the
  re-materialization was pure overhead: `HtmlDocumentParser` already yields canonical nodes and
  canonical `AppendChild` **auto-adopts** the reparented subtree into `_document`
  (`DomNode.InsertBefore`), so the new `DomBridge.BuildDocumentTree` / `BuildFragmentTree` just parse
  and hand the tree back (`AllElements` keeps the old non-structural registration contract). Widened the
  Core seam `IDomBridgeRuntime.Elements` to canonical. Deleted `DomElement.cs` + `HtmlTreeBuilder.cs`
  and rewrote the three frozen guards (`DomExtractionPhaseZeroTests`, `HtmlBridgePromotionPhaseZeroTests`,
  `HtmlBridgeBoundaryGuardTests`) from "surface is frozen" to "facade is removed" — `DocumentElement`/
  `Elements` are no longer an engine-internal leak.

**Notes for the next reader.** (1) The doctype sentinel node is now `NodeType.Element` (the old facade
gave it `NodeType.DocumentType`); this is invisible — the JS `nodeType` getter, serialization, and every
`#doctype` check key off `TagName`, and nothing tests `NodeType == DocumentType`/`is DomDocumentType`.
(2) `Broiler.Wpt.Tests` was **already non-compiling before F4** (it references the facade's `.Style`/
`.Parent` compat members that phases B/E1 removed) and is outside the `Broiler.Cli.Tests` verification
scope; the item-5 seam type-swap is applied there regardless, but resurrecting those stale tests is a
separate follow-up.

**Verified:** clean build of `Broiler.HtmlBridge.Core`/`.Dom`, `Broiler.Cli`, `Broiler.Wpt`,
`Broiler.Cli.Tests` (0 errors). Full `Broiler.Cli.Tests` before/after TRX name-diff (slot-scroll crasher
excluded): **0 new failures** — after-failures (73) are a strict subset of the pre-F4 baseline (74); the
lone dropped failure is a pre-existing flaky render test (`VerticalTextPositioningTests`). Same
environmental baseline as post-2d.

**⚠️ MERGE GATE (F4 + the whole F3c stack).** Regression-free on `Broiler.Cli.Tests` (incl. Acid3), but
the irreversible cutover still requires the **full WPT + Acid + pixel** corpus before merge (silent
`outerHTML`/selection/render corruption is the failure mode; watch SVG/foreign `createElementNS`). WPT is
dispatch-only in this environment — run it green before merging `aa00ecf5`/`ddad4769`.

## 6. Verification methodology (used for every landed commit)

Baseline the full `Broiler.Cli.Tests` first (per `CLAUDE.md`), then require **zero
baseline-passing tests to regress** via a before/after TRX name diff. The
`ScrollIntoView_Treats_Assigned_Slot_As_Scroll_Container` slot-scroll crasher is excluded (it
stack-overflows and aborts the host). The steady-state environmental baseline is **81 failures /
1618 passes / 1699 total** — all pre-existing (graphics/Skia, PDF, HTTP, WPT-render,
flex/grid, `GoogleSearchPolyfillTests` scroll/render), each confirmed failing on the pre-change
tree. One F3a run aborted mid-suite on a parallel-load OOM flake that cleared on rerun.
