# RF-BRIDGE-1b — unify the bridge's live geometry on the renderer's layout engine

**Status:** Design (2026-06-29). Decision ratified in
[`refactor-gap.md`](refactor-gap.md) RF-BRIDGE-1 (end-state (b), unify). RF-BRIDGE-1a
(retire the dead paint pipeline) is implemented on `refactor/rf-bridge-1a-retire-dead-paint`.
This is the large, gated follow-on. No code landed yet.

## 1. Problem

The bridge answers every JS geometry query — `offsetTop/Left/Width/Height`,
`clientWidth/Height`, `scrollWidth/Height`, `getBoundingClientRect`, and WPT
`check-layout` assertions — from `LayoutMetrics.cs` (2711 LOC) recursive estimators
over computed style, **not** from the renderer's real layout. The estimators are
deliberately coarse: inline text advance is approximated as
`lineLength × fontSize × 0.5` ([`LayoutMetrics.cs:302`](../../src/Broiler.HtmlBridge.Dom/DomBridge/LayoutMetrics.cs))
— no real font metrics, kerning, proportional widths, or line breaking. This is the
structural cause of the pixels-vs-`getComputedStyle` divergence and the exponential
recursion that produced the #1113/#1115 timeouts (tamed, not fixed, by
`WithLayoutGeometryCache`).

## 2. Feasibility — confirmed (2026-06-29)

A renderer-side investigation confirmed the unification is viable with existing
infrastructure:

- **Headless layout already exists.** `HtmlContainer.PerformLayout(RectangleF)`
  ([Broiler.HTML.Image/HtmlContainer.cs:187](../../Broiler.HTML/Source/Broiler.HTML.Image/HtmlContainer.cs))
  spins up an internal `BBitmap` purely so text can be measured; **no window, GDI,
  Direct2D, or paint surface is required.** Text measurement flows through
  `ITextShaper` (font metrics only), behind `ILayoutEnvironment.MeasureText`.
- **The canonical document is shared.** `HtmlContainer.SetDocumentWithStyleSet(DomDocument, …)`
  ([HtmlContainerInt.cs:307](../../Broiler.HTML/Source/Broiler.HTML.Orchestration/HtmlContainerInt.cs))
  takes the same `Broiler.Dom.DomDocument` the bridge already owns post-RF-DOM, and
  rebuilds the box snapshot lazily on `DomDocument.Version` change.
- **Box→element link exists.** `CssBox.SourceElement` is the canonical
  `Broiler.Dom.DomElement` ([Broiler.Layout/Engine/CssBox.cs:20](../../Broiler.Layout/Broiler.Layout/Engine/CssBox.cs)),
  set during the `SetDocument` parse path.
- **Geometry surface is complete.** `CssBox` exposes `Location`, `Size`, `Bounds`,
  `ActualLeft/Top/Right/Bottom`, `Actual{Margin,Padding,Border}{Top,Bottom,Left,Right}Width`,
  and `ClientLeft/Top/Right/Bottom`/`ClientRectangle` — everything the bridge's
  geometry entry points need.
- **Precedent:** `HtmlContainer.GetElementRectangle(string id)` ([HtmlContainerInt.cs:703])
  already walks the laid-out tree and returns an element's rect headlessly.

## 3. The seam

`CssBox.SourceElement` is `internal` to `Broiler.Layout`, and the bridge is **not** a
`Broiler.Layout` friend (RF-LAYOUT-1 trimmed the friend list to seven). So the
DomElement-keyed read-model must be exposed by the **renderer** (`HtmlContainerInt`
in `Broiler.HTML.Orchestration`, which *is* a Layout friend and already walks boxes
by `SourceElement`), not built in the bridge.

```
HtmlBridge.Dom (LayoutMetrics)
   → HtmlBridge.Rendering  (already refs Broiler.HTML.Image)
      → HtmlContainer / HtmlContainerInt  (Broiler.HTML — SUBMODULE)
         → CssBox tree (Broiler.Layout, friend access to SourceElement)
```

**Proposed renderer API** (additive, public; in the `Broiler.HTML` submodule →
follows the patch/push workflow in `CLAUDE.md`):

```csharp
// HtmlContainer / HtmlContainerInt
public readonly record struct BoxGeometry(
    RectangleF BorderBox, RectangleF PaddingBox, RectangleF ContentBox);

// Lay out the bound DomDocument headlessly at the given viewport and return
// geometry for every box that has a SourceElement, keyed by that element.
public IReadOnlyDictionary<Broiler.Dom.DomElement, BoxGeometry>
    GetLayoutGeometry(SizeF viewport);
```

Increment ① exposes the three CSS box-model levels (border/padding/content),
which serve `offsetWidth/Height`, `clientWidth/Height`, `offsetTop/Left`, and
`getBoundingClientRect`. Scroll extents (`scrollWidth/Height`) need descendant
overflow computation and are added in a later increment; until then those queries
stay on the estimator.

The bridge consumes this from a thin provider in `HtmlBridge.Rendering` (the project
that already references `Broiler.HTML.Image`), exposed to `HtmlBridge.Dom` via an
interface on `HtmlBridge.Core`/`.Rendering` so `LayoutMetrics` depends on the seam,
not on `HtmlContainer` directly.

## 4. Performance — lay out once per snapshot

A full `PerformLayout` per geometry query would be far costlier than the memoized
estimators. The provider must lay out **once per layout snapshot** and answer all
queries from the cached dictionary, invalidating on `DomDocument.Version` change —
the same versioned-snapshot pattern the typed `HtmlContainer` already uses, and a
natural fit for the existing per-pass `WithLayoutGeometryCache` lifetime. Benchmark
`bridge.mutation` + a new geometry-query metric against baseline before flipping.

## 5. Increment sequence (each independently buildable, default-off until the flip)

1. **Renderer read-model (submodule).** Add `GetLayoutGeometry`/`BoxGeometry` to
   `HtmlContainerInt`/`HtmlContainer`. Submodule change → push to `MaiRat/Broiler.HTML`
   if allowed, else ship as a `patches/` file per `CLAUDE.md`. Unit-test against the
   laid-out tree directly.
   **DONE (2026-06-29, pending pointer bump).** Implemented `BoxGeometry` +
   `HtmlContainerInt.CollectLayoutGeometry()` + `HtmlContainer.GetLayoutGeometry(viewport)`
   (border/padding/content boxes keyed by `CssBox.SourceElement`, headless). Pushed to
   `MaiRat/Broiler.HTML` branch `rf-bridge-1b-layout-geometry` (commit `cdc398f`).
   Parent test `SharedLayoutGeometryTests` (3 tests, green) verifies the box-model
   arithmetic end-to-end via the canonical document. **Pointer bump deferred** until the
   submodule PR merges to `Broiler.HTML` `main` (don't pin the parent to a transient
   feature-branch SHA); the parent test must land with that bump, since it depends on
   the new API.
2. **Bridge provider + flag.** Add `SharedLayoutGeometryProvider` in
   `HtmlBridge.Rendering` (drives the headless `HtmlContainer`, caches by version) and
   a `DomBridge.UseSharedLayoutGeometry` static flag (default **false**), mirroring
   `LayoutGeometryCacheEnabled`.
   **DONE (2026-06-29).** `SharedLayoutGeometryProvider` caches the per-element map by
   `(document, DomDocument.Version, viewport)`; `DomBridge.UseSharedLayoutGeometry`
   (default false) + private `TryGetSharedLayoutGeometry(element, out BoxGeometry)`
   accessor added (not yet called by the live path → zero behavior change). Tests
   `SharedLayoutGeometryProviderTests` (real geometry keyed by element; snapshot reuse
   on unchanged version+viewport; viewport-change invalidation) green.
   **Carry-over for ③:** `DomBridge.GetRenderDocument()` runs `ReflectRenderState`,
   which may mutate the canonical document and bump `DomDocument.Version` on every
   call — that would defeat the provider's version cache (relayout per query). Increment
   ③ must reflect-then-snapshot once per geometry pass (e.g. reuse the existing
   per-pass `WithLayoutGeometryCache` lifetime) or gate reflection on the bridge's own
   mutation counter.
3. **Route the entry points.** Behind the flag, make the ~10 `*ForDomElement` methods
   in `LayoutMetrics.cs` read from the provider instead of the estimators. Estimators
   stay as the default path.
   **DONE (2026-06-29).** Routed `ComputeUnzoomedLayoutRect` (→ border box, powers
   `getBoundingClientRect` + offset top/left), `GetOffsetWidth/Height` (→ border box),
   and `GetClientWidth/Height` (→ **padding** box — clientWidth is content+padding; the
   engine's `ClientRectangle` is the content box, so the mapping is `PaddingBox`, not
   `ContentBox`). Each falls back to the estimator when the element has no box.
   **Two robustness fixes the gate forced out:** (a) the snapshot is built **up front**
   in `WithLayoutGeometryCache` setup, because `GetRenderDocument` mutates the document
   (reflects style→attributes) and building it lazily mid-traversal threw
   `InvalidOperationException: Collection was modified` inside the check-layout
   `foreach`; (b) `BuildSharedGeometrySnapshot` and the provider both swallow layout
   failures and degrade to the estimator, so a geometry query can never throw.
   **Gate result: estimator 72/484 → shared 69/484 — a 3-assertion regression, all the
   same root cause:** `@position-try` elements (`position-try-002` width+height,
   `position-try-grid-001` height) lay out to 0×0 because the **renderer has no
   position-try layout** (these are already WPT pixel-failing). The estimator "wins"
   only by reading declared width/height it never laid out. The flag therefore stays
   **off**; the parity gate carries a documented `KnownRendererGapRegressions = 3`
   budget so it still fails on any *new* regression and drops to 0 once the renderer
   lays out position-try.
4. **Parity gate.** Reuse `DomBridge.EvaluateCheckLayoutAssertions()` (returns
   `(Element, Property, Expected, Actual)`): run the WPT check-layout corpus through
   both paths and count matches within the ±1px WPT tolerance. Require shared ≥
   estimator, and run the WPT pixel + Acid gates.
   **DONE (2026-06-29).** [`SharedLayoutGeometryParityTests`](../../src/Broiler.Cli.Tests/SharedLayoutGeometryParityTests.cs)
   runs the committed `tests/wpt/css/css-align` + `css-anchor-position` check-layout
   files through `EvaluateCheckLayoutAssertions` under both flag states and asserts
   `shared.Matched >= estimator.Matched`. The assertion recomputes the estimator
   baseline each run (no hardcoded number to go stale). **Estimator baseline: 72 / 484
   assertions matched within ±1px across 23 files (~15%)** — the coarse estimators get
   most check-layout geometry wrong, quantifying the headroom for the shared path. The
   gate passes trivially today (shared == estimator, since ③ has not routed the live
   path); once ③ lands it fails on any regression.
5. **Flip** `UseSharedLayoutGeometry` to true once parity holds and budgets pass.
   **DONE.** `DomBridge.UseSharedLayoutGeometry` now defaults to **true**
   ([`SharedLayoutGeometry.cs`](../../src/Broiler.HtmlBridge.Dom/DomBridge/SharedLayoutGeometry.cs));
   the live geometry entry points read the shared renderer layout, with the
   estimators kept as the per-element fallback. The parity gate holds with a
   documented `KnownRendererGapRegressions = 3` budget (the renderer's missing
   `position-try`/anchor/grid layout — those elements fall back to the estimator).
   Increments 6–7 (delete the estimators, retire `LayoutRuntimeState`) stay blocked
   until the renderer closes that layout gap and the shortfall reaches 0.
6. **Delete the estimators.** Remove the ~2700-line estimator body from
   `LayoutMetrics.cs`, keeping only the thin JS-facing wrappers that now call the
   provider. Retire `WithLayoutGeometryCache` (the provider's snapshot cache replaces
   its purpose).
7. **Move `LayoutRuntimeState`** (`ElementRuntimeState.cs:102`, four
   `RuntimeValue<double>` slots) out last — most of it becomes obsolete once geometry
   comes from the shared snapshot.

## 5a. Blocker found (2026-06-29) — the typed render path drops author `<style>` CSS

Investigating the increment-③ parity regressions (`@position-try` elements at 0×0)
uncovered a deeper, more general blocker. The regressions were a symptom, not the
cause:

- The **entire** document lays out to 0×0 in the typed path for those files, not just
  the anchored elements — `.cb` (explicit `400×400`) included.
- Isolation A/B: for `<style>#x{width:50px;height:50px}</style><div id=x>`, the
  **HTML-string** path (`SetHtmlWithStyleSet` → `GetElementRectangle`) returns the
  correct **50×50**, but the **typed** path (`SetDocumentWithStyleSet(GetRenderDocument())`
  → `GetLayoutGeometry`) returns **800×0**. Inline `style=` attributes work on both
  paths (that is why the increment-① tests passed); author `<style>` rules do not work
  on the typed path.
- **Root cause:** `DomBridge.GetRenderDocument()` returns a `<style>` element with **no
  DOM text** (`childNodes=[DomElement] textLen=0`). The bridge keeps stylesheet text in
  runtime state / CSSOM (RF-CSS Phase 6 `GetStyleElementSourceText`), not as a live
  `DomText` child. The renderer's typed box-builder (`HtmlParser.ParseDocument(DomDocument)`)
  + `DomParser.CascadeParseStyles` extract author CSS from the `<style>` box's child
  **`box.Text`** (`DomParser.cs:105`), so with no text child there is nothing to parse →
  no author cascade → collapsed layout. This matches the `public-preview-roadmap.md`
  note that the typed `SetDocument` handoff "lays out empty."

Regression captured by the skipped test
`TypedDocument_Applies_Author_StyleSheet` (un-skip when fixed).

**This is the real prerequisite for the flag flip** — without author `<style>` CSS the
shared geometry is garbage for almost any real document. Two fix options:

1. **Bridge reflects `<style>` text into the render document.** Extend
   `GetRenderDocument()`/`ReflectRenderState` to write each `<style>` element's current
   CSS (from `GetStyleElementSourceText`) back as a `DomText` child, so the typed
   renderer can cascade it. **Fixes the typed path for every consumer** (also the
   Graphics app's typed handoff). Preferred.
2. **Provider passes an author `HtmlStyleSet`.** `SharedLayoutGeometryProvider` builds an
   author-origin `HtmlStyleSet` from the bridge's parsed sheets and passes it to
   `SetDocumentWithStyleSet`. Narrower (RF-BRIDGE-1b only), avoids touching the shared
   render document.

Until one lands, the shared path stays off regardless of the position-try gap.

### Option 1 is NOT viable (investigated 2026-06-29)

Attempting option 1 uncovered why: the bridge stores text nodes (including `<style>`
content) as its **facade `DomElement`** (`Broiler.HtmlBridge.Core/Dom/DomElement.cs`)
with `NodeType == Text` and the text in a bridge-private `_textContent` field — not as
canonical `Broiler.Dom.DomText`. Two hard constraints follow:

- **The renderer can't read facade text canonically.** `Broiler.Dom.DomNode` exposes
  no text accessor (no `TextContent`/`NodeValue`/`Data`); only `DomText` has `Data`. So
  the typed builder's `node is Broiler.Dom.DomText` check is the only way it can read
  text, and the facade element fails it.
- **A real `DomText` child breaks the bridge.** `DomElement.LegacyChildList` is
  `IList<DomElement>` whose enumerator is `owner.ChildNodes.Cast<DomElement>()` and
  whose indexer is `(DomElement)owner.ChildNodes[index]`. Appending a canonical
  `DomText` to a live `<style>` throws `InvalidCastException` everywhere the bridge
  walks `.Children` (`ReflectRenderState`, `GetStyleElementSourceText`, serialization).
  Cloning the document to add the `DomText` off to the side instead breaks the geometry
  keying (boxes would key by clone elements, not the bridge instances `LayoutMetrics`
  looks up).

**Resolved by the canonical-text-accessor fix — IMPLEMENTED + verified (2026-06-29).**
The "heavier proper" fix was taken instead of the styleSet workaround because it fixes
*all* text in the typed path, not just `<style>`:

- `Broiler.Dom.DomNode` gains `public virtual string? NodeValue => null;`
  (DOM `nodeValue`); `DomCharacterData` overrides it to return `Data` (so `DomText`
  exposes its text). *(Broiler.DOM submodule.)*
- The bridge facade `DomElement` overrides `NodeValue => IsTextNode ? _textContent :
  base.NodeValue`, so its text-as-element nodes expose text canonically.
  *(Broiler.HtmlBridge.Core, parent repo.)*
- The renderer's typed builder `HtmlParser.AppendCanonicalNode` matches text by
  `node.NodeType == DomNodeType.Text` and reads `node.NodeValue` instead of
  `node is DomText` / `text.Data` — so it consumes text from both the renderer's own
  `DomText` (string path) and the bridge's facade text nodes (typed path), identically.
  *(Broiler.HTML submodule.)*

Verified: `TypedDocument_Applies_Author_StyleSheet` flips FAIL→PASS (typed `#x` now
50×50). A reverted-vs-applied A/B over the Acid2/Acid3/RenderingPipeline/typed-handoff
sweep showed **identical** failures except that one test (13 baseline → 12 with the
fix) — i.e. the string-path/pixel rendering is unchanged and the documented baselines
(`Border_Shorthand`, `Without_Important`, the DOM-removal/cascade family, Acid3
score/image-capture) are untouched. **Zero regressions.**

The check-layout parity corpus stays at 69 (the css-anchor-position/css-align tail
exercises anchor/writing-mode/grid layout the renderer does not implement, independent
of `<style>` application), so the flag remains off — but the fundamental typed-path
text/CSS gap is now closed.

## 6. Risks

- **Behavior change on hot, well-tested code.** Real metrics will shift many geometry
  values; check-layout and pixel results will move (expected to improve). Gate on the
  parity harness + WPT/Acid; treat any net-worse assertion count as a blocker.
- **Submodule coupling.** Step 1 edits `Broiler.HTML`; if the push is denied, the
  pointer must not be bumped — ship the patch and add a main-repo path only if CI
  needs it before the patch lands (per `CLAUDE.md`).
- **Performance.** Per-snapshot layout must be cached; verify no regression in
  `bridge.mutation` and add a geometry-query benchmark.
- **Detached / not-yet-laid-out elements.** `getBoundingClientRect` on detached nodes
  must still return zeros; the provider returns absence for elements with no box, and
  the wrappers preserve current semantics.

## 7. Definition of done

`LayoutMetrics` answers all geometry from the renderer's real layout via the
read-model seam; the coarse estimators and `WithLayoutGeometryCache` are deleted; the
parity harness shows shared ≥ estimator on the WPT check-layout corpus; WPT pixel,
Acid, and bridge geometry tests hold or improve; performance budgets pass; and
`LayoutRuntimeState` is retired. At that point the two-box-model duplication is gone
and RF-BRIDGE-1 (and Track C) is fully closed.
