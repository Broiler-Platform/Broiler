# HtmlBridge `DomElement` Facade Removal — Current State & Handoff

Status: **in progress** — the facade now carries only `NamespaceURI`. The atomic text-node
flip (F3c part 2) is **fully implemented and `Broiler.Cli.Tests`-verified (0 regressions,
8 fixes)** across the green widening prefix (2a/2b/2c) + the irreversible construction flip
(2d). **2d is committed but NOT merged** — it awaits the WPT range/selection/serialization +
Acid gate (see the merge gate in §4). After 2d merges, only **F4** (element construction flip,
remove `NamespaceURI`, delete `DomElement.cs`/`HtmlTreeBuilder.cs`) remains.
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
| **`TextContent`** | ⏳ **remains** | → canonical `DomText`/`DomComment`.`Data` (F3c part 2) |
| **`NamespaceURI`** | ⏳ **remains** | → canonical read + `CreateElementNS` at construction (F4) |

`DomElement` and `HtmlTreeBuilder` themselves are deleted in **F4**.

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

## 5. What remains — F4 (final cutover, irreversible)

Flip the ~40 real-element `new DomElement(...)` sites to `document.CreateElement`/
`CreateElementNS` (this removes the `NamespaceURI` ctor-coupling — canonical `SetName` is
`protected`, so namespace must be set at construction); retire `HtmlTreeBuilder` (callers use
`Broiler.Dom.Html.HtmlDocumentParser.ParseDocument(html, _document)` directly); re-key the ~14
remaining per-node caches to canonical `DomElement`; widen the public seams `DomBridge.Elements`/
`DocumentElement` + `IDomBridgeRuntime.Elements` to canonical `Broiler.Dom.DomElement` (only
external consumer needing a touch: `Broiler.Wpt.Tests/WptTestRunnerTests.cs`); remove facade
`NamespaceURI`; delete `DomElement.cs` + `HtmlTreeBuilder.cs`; update/remove the frozen seam
guard tests (`DomExtractionPhaseZeroTests`, `HtmlBridgePromotionPhaseZeroTests`,
`HtmlBridgeBoundaryGuardTests`).

## 6. Verification methodology (used for every landed commit)

Baseline the full `Broiler.Cli.Tests` first (per `CLAUDE.md`), then require **zero
baseline-passing tests to regress** via a before/after TRX name diff. The
`ScrollIntoView_Treats_Assigned_Slot_As_Scroll_Container` slot-scroll crasher is excluded (it
stack-overflows and aborts the host). The steady-state environmental baseline is **81 failures /
1618 passes / 1699 total** — all pre-existing (graphics/Skia, PDF, HTTP, WPT-render,
flex/grid, `GoogleSearchPolyfillTests` scroll/render), each confirmed failing on the pre-change
tree. One F3a run aborted mid-suite on a parallel-load OOM flake that cleared on rerun.
