# HtmlBridge Blocked-Items Completion Roadmap

Status: proposed
Date: 2026-07-09

## Purpose

Give the two **BLOCKED** workstreams in Phase 5 of
[`htmlbridge-dom-css-promotion-roadmap.md`](htmlbridge-dom-css-promotion-roadmap.md)
a single, ordered execution plan with concrete tasks, dependencies, risks, and
exit criteria per milestone. The two items are:

- **Item 2 — RF-BRIDGE-1b geometry unification.** Replace the ~2950-LOC
  `LayoutMetrics` recursive estimators with the renderer's real layout via the
  shared-geometry provider; retire `LayoutRuntimeState`.
- **Item 1 — v1 public-surface removal.** Remove the compatibility adapters
  `DomElement`, `HtmlTreeBuilder`, `DomBridge.CssRules`, and
  `DomBridge.CalculateSpecificity` at the `htmlbridge-public-surface/v2` boundary.

This document does not restate the design of either item — see
[`rf-bridge-1b-layout-unification.md`](rf-bridge-1b-layout-unification.md) for
the geometry-unification design and
[`htmlbridge-dom-css-promotion-roadmap.md`](htmlbridge-dom-css-promotion-roadmap.md)
Phase 5 for the adapter inventory. It sequences *how to finish them*.

## The ordering constraint (why item 2 comes first)

The two items are not independent. The `DomElement` facade
(`Broiler.HtmlBridge.Core/Dom/DomElement.cs`, `public sealed class DomElement :
CanonicalElement`) is the **dictionary identity key** for nearly every bridge
cache:

| Cache | File | Purpose |
| --- | --- | --- |
| `ConditionalWeakTable<DomElement, ElementRuntimeState>` | `DomBridge.cs:55` | all per-element runtime state |
| `Dictionary<DomElement, JSObject>` | `JsObjects.cs:22` | JS wrapper object identity |
| `Dictionary<DomElement, ComputedStyleEngineScope>` | `ComputedStyleEngine.cs:21` | computed-style engines |
| `ConcurrentDictionary<DomElement, …>` (×2) | `Css.cs:34-35` | computed-props caches |
| `Dictionary<DomElement, …>` layout-rect / border-box caches | `LayoutMetrics.cs:23,25` | estimator geometry caches |
| shared geometry snapshot | `SharedLayoutGeometry.cs:41` | renderer box geometry |

Removing the `DomElement` **type** (item 1) means re-keying all of these off
canonical `Broiler.Dom.DomNode`/`DomElement` identity. Item 2 is precisely the
geometry/layout slice of that decoupling. Therefore:

1. **Item 2 runs first** and lands the geometry re-keying + estimator deletion.
2. **Item 1's facade removal (DomElement/HtmlTreeBuilder) runs after item 2**,
   once the geometry caches no longer pin the facade and the remaining
   identity-keyed caches (runtime state, JS object identity, computed style) have
   a canonical-node key.
3. **Two exceptions run early**: `DomBridge.CalculateSpecificity` and
   `DomBridge.CssRules` have **no production callers** and are gated only on the
   `htmlbridge-public-surface/v2` governance decision — they can be removed the
   moment v2 is declared, independent of the facade migration.

```
Track 2 (geometry)                         Track 1 (public surface)
────────────────────                       ────────────────────────
2.1 zoom-correct snapshot
2.2 root/viewport scroll        ┌───────►  1.0 declare v2 (governance)  ──┐
2.3 scroll-aware resolver src   │          1.1 remove 2 zero-caller shims │ (needs only 1.0)
2.4 delete estimators (incr 6) ─┤
2.5 retire LayoutRuntimeState ──┴───────►  1.2 migrate DomElement callers
    (incr 7)                               1.3 remove DomElement + HtmlTreeBuilder
```

---

## Track 2 — RF-BRIDGE-1b geometry unification

Current state (verified 2026-07-09): `UseSharedLayoutGeometry = true` (shared
path live with estimator fallback); `UseSharedGeometryExclusively = false`
(fallback→zeros staged but off); shared-geometry test families 16/16 green;
estimators (`LayoutMetrics.cs`, ~2952 LOC) still load-bearing for `isRoot`/
viewport, zoomed subtrees, sticky, position-area, `scrollIntoView`, and boxless
elements. `LayoutRuntimeState` still stores resolved position-area geometry.

### Milestone 2.1 — Zoom-correct shared snapshot (the gating prerequisite)

**Goal.** Answer zoomed elements from the shared snapshot so the
`IsUnzoomedForSharedGeometry` gate can be removed and the estimator stops being
needed for zoom.

**Design (corrected twice on 2026-07-09 while implementing — the earlier notes
were wrong on two counts).**

- **The layout engine has no `zoom` concept.** `Broiler.Layout/Engine/CssBox.cs`
  contains zero `zoom` references. `zoom` is resolved and applied *entirely in the
  bridge* by `ApplyZoomSerializationStyles` (`DomBridge.Serialization.cs`), which
  scales serialized styles and then **strips the `zoom` property** before the
  document reaches the renderer. So there is no renderer-side used-zoom to
  "expose" — the original "expose renderer used-zoom" task was invalid.
- **The bake is already comprehensive/uniform** (an earlier "partial bake" note
  was a misread of a truncated property list). `ZoomScaledSerializationProperties`
  scales `width/height`, `min/max`, insets, `margin-*`, `padding-*`,
  **`border-*-width`, `font-size`, `line-height`, `letter/word-spacing`,
  `text-indent`, `border-radius`, `outline-width`, columns, `stroke-width`**. So a
  zoomed subtree renders at a true uniform scale, the snapshot box is `unzoomed ×
  zoom`, and **dividing the shared branches by the element's own
  `GetUsedZoomForElement` is correct**. There is nothing to add to the bake;
  "rendering-global completion" is a no-op — rendering already scales all of these.
- **Why the earlier divide-by-zoom was reverted:** it predated the render-doc/
  live-doc separation (the `_zoomSerializationRevertLog`). Back then the division
  read `zoom` from the *baked* (zoom-stripped) live document → got 1 → didn't
  divide. Now the snapshot pass **reverts** the live doc after building, so
  `GetUsedZoomForElement` reads the restored true zoom at division time.

The correct value is fixed by `SharedGeometryZoomSizeTests`: an element's own
`client/offset/scrollWidth` are its **unzoomed** CSS pixels (`zoom:2;width:100` →
`offsetWidth 100`), while a zoomed **descendant** contributes its zoomed extent to
an unzoomed ancestor's scroll overflow. (`getBoundingClientRect` and offset
*position*, by contrast, want the **zoomed** value.)

**Status — size + scroll slice DONE (2026-07-09).** Implemented in
`LayoutMetrics.cs`: a `UnzoomSharedExtent(extent, element)` helper (`extent ÷
GetUsedZoomForElement`, no-op when zoom == 1); the `client/offset Width/Height`
shared branches and `TryGetSharedScrollExtent` now drop the
`IsUnzoomedForSharedGeometry` gate and divide by own zoom (descendant extents stay
in the ancestor's baked space, so a zoomed descendant's overflow still counts).
Verified: shared-geometry families **18/18** green incl. all five
`SharedGeometryZoomSizeTests`; anchor/sticky/position-area **9/9**; **zero
regressions** vs a stashed HEAD baseline (the `Offset` (2) / `Geometry` (3) pixel
failures, the `Scroll` `EnumerateRenderedDescendants` stack overflow, and
`AutoSized_ScrollMetrics_Ignore_MarginOnly` are all pre-existing at HEAD). Since
the division is `÷1` for unzoomed elements, the blast radius is limited to zoomed
elements.

**Status — getBoundingClientRect slice DONE, `IsUnzoomedForSharedGeometry`
deleted (2026-07-09).** `ComputeUnzoomedLayoutRect` (which powers
`getBoundingClientRect` via `ComputeRenderedRect`) now routes zoomed elements
through the shared box: **position stays in rendered doc coords, size is divided
by own cumulative zoom** so `ComputeRenderedRect`'s `× zoom × transformScale`
reproduces the correct zoomed rect. Validated by the WPT oracles
`Wpt_CssomView_ZoomGeometryApis_MatchReference` (nested zoom `512 = 64×8`,
`transform+zoom`) and `Wpt_CssomView_ZoomScrollAndOffsetApis_MatchReference` —
both green. The `IsUnzoomedForSharedGeometry` **method is deleted**;
`ShouldReturnExclusiveSharedZero` is simplified (zoomed boxless elements now also
zero under the increment-6 cutover). Zero regressions: WPT zoom subset 19/21 (the
two failures — `OffsetTopLeft_BorderBoxPaddingEdge` (an *unzoomed* grid/flex case)
and `PinchZoom` — are pre-existing at HEAD); Cli shared families + parity 18/18;
Offset/Geometry back to their pre-existing pixel-failure baselines.

**Status — `offsetTop/Left` for zoomed elements DONE (2026-07-09).**
`TryGetSharedOffset` now computes `(elementEdge − parentEdge) ÷ own cumulative
zoom`: both the element border edge and the offset parent's padding edge are read
from the shared snapshot (same zoom-baked space), and dividing by the element's own
used zoom recovers the offset-parent-relative value in unzoomed CSS pixels. An
empirical probe over the 16-value oracle
(`GoogleSearchPolyfillTests.Element_OffsetPosition_Uses_OffsetParent_And_Excludes_Target_Zoom`)
confirmed the formula matches the estimator on **15 of 16** values — including the
`middle-ancestor-zoom` case (`unzoomedInner` = 10,11) the earlier note wrongly
believed it broke. The one difference, `zoomedInner.offsetTop` **0 → 1**, is a
*correctness fix*, not a regression: `zoomedInner` (`zoom:2`, `margin:1`, in-flow)
renders its border box 2px below the offset parent's padding edge (margin 1px ×
zoom 2), so offsetTop is `2 ÷ 2 = 1` — the margin contributes 1, exactly as the
absolute `unzoomedTwo` case (`margin:1` → +1 under `zoom:2`) requires; the estimator
reported 0, dropping the margin (and was internally inconsistent — it reported the
same-doc-position sibling `unzoomedMiddle` as 2). That single assertion was updated
with a justification comment. The earlier "regressed a paint test" concern was
parallel pixel-test flakiness (`HtmlContainer_PerformPaint...` passes in isolation).
Validated: `Element_OffsetPosition` green; WPT `ZoomScrollAndOffset` + `ZoomGeometryApis`
green; Cli shared families + parity 16/16; Offset category holds its 2 pre-existing
failures across repeated runs.

**Milestone 2.1 COMPLETE.** Zoomed size, scroll, `getBoundingClientRect`, and
`offsetTop/Left` all resolve from the shared snapshot; `IsUnzoomedForSharedGeometry`
is deleted. No geometry entry point gates on zoom any more.

**Exit criteria — all met.** Zoomed size, scroll, `getBoundingClientRect`, and
`offsetTop/Left` come from the shared snapshot; `IsUnzoomedForSharedGeometry` deleted;
zero regressions.

### Milestone 2.2 — Root/viewport scroll extent from shared

**Goal.** Answer `scrollWidth/Height` for the document/viewport
(`isRoot == true`) from the snapshot, closing the last non-resolver scroll gap.

**Depends on:** 2.1 (root subtree may contain zoomed elements).

**Status — DONE (2026-07-09).** The `isRoot` guard was simply removed from the
shared-scroll branch of `GetScrollWidth/HeightForDomElement` — no root variant was
needed, because the estimator's own scroll body never special-cased root either (it
uses `GetClientWidthForDomElement(element, isRoot:false)` + the same descendant
union), so `TryGetSharedScrollExtent(rootElement)` computes the document scrolling
area consistently: the root box's padding extent unioned with descendant border
boxes, own-zoom-divided. The estimator remains the fallback when the root has no
shared box. A probe confirmed the root actually reads from shared now
(`documentElement.scrollHeight` = 410 shared vs 316 estimator on a wrapping-text
document — the shared value reflects the renderer's real text layout, so it is more
accurate). Zero regressions: `Window_Scroll_APIs_Update_Root_Scroll_Offsets`,
`SharedScrollOverflow`, `Element_ClientAndScrollMetrics`, WPT
`WindowScrollApis`/`ScrollMetrics`/`ClientAndScroll` all green; the 8 WPT
scroll/scrollIntoView/writing-mode/position-area failures in the neighborhood were
verified pre-existing (they fail with the `isRoot` guard restored too). Note: the
shared root uses the bridge's real `_viewportWidth`/`Height` where the estimator
used a coarser default — the window-scroll clamping tests confirm the new bounds are
compatible.

**Exit criteria — met.** The `isRoot` scroll branches read from the snapshot;
estimator root-scroll path is fallback-only.

### Milestone 2.3 — Scroll-aware pre-layout geometry source for the resolvers

**Goal.** Give sticky positioning, position-area resolution, and `scrollIntoView`
a shared-geometry source so they stop calling the estimators
(`ComputeOffsetWithinAncestor`, border/content-box estimators).

**Why it is deferred to here.** These resolvers run *inside*
`ResolveAnchorPositions`, mutate the DOM, and depend on **intermediate scroll
offsets** (`GetElementScrollOffset`) that the static snapshot does not carry —
sticky runs *before* scroll simulation. A piecemeal migration would diverge
exactly in the scroll cases these features exist to handle. They must migrate
together, against a scroll-aware source.

**Tasks.**

1. Add a scroll-aware pre-layout geometry accessor: the shared snapshot plus the
   bridge's JS-set scroll state applied as offsets (the anchor resolver already
   reads shared geometry without recursion via `TryGetAnchorLayoutBox`).
2. Migrate `AnchorResolver/StickyPositioning.cs` (`StickyBorderBoxSize`,
   `ComputeOffsetWithinAncestor`) to the accessor.
3. Migrate `AnchorResolver/PositionArea.cs` / `PositionAreaQueries.cs` — resolved
   geometry currently written to `LayoutRuntimeState.Layout.{Left,Top,Width,Height}`
   comes from shared geometry.
4. Migrate `scrollIntoView` alignment geometry.

**Status — scroll-aware accessor built + sticky position migrated (2026-07-09).**
Task 1 is done and, importantly, **de-risks the whole milestone**:
`TrySharedOffsetWithinAncestor(element, ancestor, vertical)` (in `LayoutMetrics.cs`)
is the scroll-aware equivalent of `ComputeOffsetWithinAncestor` — it reads the
*natural* (unscrolled) element-border-to-ancestor-padding delta from the shared
snapshot, then subtracts each intermediate scroll container's `GetElementScrollOffset`
(so the "static snapshot carries no scroll state" concern is handled explicitly),
and falls back to the estimator when the shared box is missing or zoom is in play.
Task 2's *position* reads are migrated: `StickyPositioning.cs`'s two
`ComputeOffsetWithinAncestor` calls (natural-in-scrollport and containing-block
clamp) now prefer the accessor. Verified the shared path is actually exercised
(a temporary hit-counter confirmed `hits ≥ 1` during sticky resolution — the
migration is not a silent fallback) and that it is behavior-preserving: the broad
anchor/sticky oracle is unchanged — WPT sticky/anchor/scroll-tracking 32/34 (2
pre-existing failures), Cli anchor/sticky 9/9, shared-geometry families + parity
10/10. This proves the scroll-aware source is buildable and matches the estimator
on the tested cases. A shared wrapper `OffsetWithinAncestorPreferShared` (try shared,
else estimator) now backs the sticky calls.

**scrollIntoView — ATTEMPTED, REVERTED (2026-07-09).** Migrating the three
`ComputeOffsetWithinAncestor(element, scrollContainer)` callers (the two visual-
viewport `offsetOverride` sites and `ResolveScrollIntoViewOffset`) to the wrapper
looked like a clean drop-in but **regressed fixed-position / iframe scrollIntoView**:
WPT scrollIntoView 12→10 pass (2 new failures) and Cli
`ScrollIntoView_Uses_Script_Assigned_Iframe_Position_For_Fixed_Targets`. Bisected
to the general `ResolveScrollIntoViewOffset` site alone (the `offsetOverride ??`
branch, i.e. the non-visual-viewport path). **Root cause:**
`TrySharedOffsetWithinAncestor` unconditionally subtracts each intermediate scroll
container's `GetElementScrollOffset`, which is wrong for a **fixed-position** target
(fixed is viewport-anchored, unaffected by ancestor scroll) and for **iframe
subdocument** targets whose coordinate space differs from the main snapshot. Sticky
never hits these (sticky boxes are in-flow in the main document), which is why it was
clean. Reverted — scrollIntoView stays on the estimator; unblocking it needs the
accessor to skip scroll subtraction for fixed/iframe targets (position-aware), a
follow-up.

**Sticky *size* reads — DONE (2026-07-09).** Added `TrySharedBorderBoxExtent` and
`TrySharedContentBoxExtent` (border/content box from the shared snapshot, own-zoom
divided via `UnzoomSharedExtent`). `StickyBorderBoxSize` and the containing-block
`ResolveContentBoxExtent` clamp in `ComputeStickyShift` now prefer them (estimator
fallback). Scroll-independent, so no fixed/iframe divergence — WPT anchor/sticky
32/34 and Cli anchor/sticky + shared families 19/19 unchanged. **Sticky is now fully
migrated off the geometry estimators** — its `GetClientWidth/Height` (2.1), position
(`OffsetWithinAncestorPreferShared`), and size (`TrySharedBorderBoxExtent`/
`TrySharedContentBoxExtent`) reads all use shared, with the estimator as fallback
only; the remaining sticky calls are scroll state (`GetElementScrollOffset`) and
style/traversal, which are correct to keep.

**Scope clarification (2026-07-09) — 2.3 is smaller than it first looked; two of
the four items need no work:**

- **position-area — already shared-sourced (no change needed).** `PositionArea.cs`
  reads anchor geometry through `AnchorRegistry.ComputeElementBox`, which *already*
  prefers the shared snapshot via `TryGetAnchorLayoutBox` (→ `TryGetSharedLayoutGeometry`,
  converting the border box to the containing-block-relative frame); the CSS-inset
  estimator is a deliberate fallback for inline-CB / detached anchors. So position-
  area's 2.3 geometry criterion is met. (Its `LayoutRuntimeState.Layout` *write* is a
  separate concern owned by 2.5, not a 2.3 geometry-source issue.)
- **Scroll-extent slotted-child callers need no migration.** The two
  `ComputeOffsetWithinAncestor(child, element)` calls in `GetScrollWidth/Height` are
  inside the *estimator fallback body*, which 2.4 deletes wholesale (the shared
  `TryGetSharedScrollExtent` branch is the primary path). They vanish with the
  estimator, so migrating them separately has no payoff.

**scrollIntoView — general path migrated to shared with two bypass gates
(2026-07-09).** The general `ResolveScrollIntoViewOffset` site (`offsetOverride ??`)
now uses `OffsetWithinAncestorPreferShared`. Getting there required diagnosing two
distinct divergences and adding matching gates to `TrySharedOffsetWithinAncestor`
(both mirror behaviour the codebase already relies on elsewhere):
- **Cross-frame gate.** iframe-subdocument targets are laid out in the subframe's own
  coordinate frame (nested browsing contexts render via isolated rasterise-and-
  composite — see nested-browsing-context-rendering), so a main-document offset can't
  be read from the shared boxes. `GetOwningDocumentElement(element) != …(ancestor)` →
  fall back to the estimator's cross-frame walk. Fixes the
  `ScrollIntoView_Uses_Script_Assigned_Iframe_Position_For_Fixed_Targets` regression.
- **abspos-in-inline-CB gate.** An absolutely/fixed-positioned element whose containing
  block is an inline box is placed by the renderer at the inline-flow position,
  ignoring its own insets (the exact case `AnchorRegistry.ComputeElementBox` already
  bypasses via `absPosInInlineCB`); its shared box is at the wrong place, so bypass to
  the estimator. Fixes the `TargetAfterBlockInInlineSibling` regression.

Result: scrollIntoView back to baseline (WPT 12/5, Cli 2 pre-existing), sticky 32/2
stable, Cli anchor/sticky + shared families 19/19 — **zero regressions**. The two
visual-viewport `offsetOverride` sites (fixed-target path) are **not** migrated; a
main-document fixed target still hits the intermediate-scroll-subtraction mismatch, so
they stay on the estimator.

**KEY FINDING (2026-07-09) — estimator deletion was blocked on RENDERER capabilities, now
CLEARED (2026-07-10).** The gated cases (cross-frame, abspos-in-inline-CB) genuinely needed
the estimator because the renderer/shared snapshot could not place them correctly. Deleting
`ComputeOffsetWithinAncestor` required the **renderer** to (a) place abspos-in-inline-CB
elements at their inset position, (b) expose cross-frame / subframe geometry in the main
coordinate space, and (c) supply fixed-target offsets for the visual-viewport sites — all
now delivered by Track 3 (3.1/3.2/3.3). Every gate is removed.

**Net 2.3 status.** Sticky **fully migrated**; position-area **already** shared;
slotted-child callers **not applicable**; scrollIntoView **fully migrated** (general,
cross-frame, and visual-viewport fixed-target paths). No resolver retains a correctness gate
on the estimator; the only residual estimator callers are the shared-*unavailable* fallbacks
(cross-origin / non-materialised frames) inside the wrappers, which 2.4 can drop to zeros or
keep as a slim degraded path.

**Risk.** High — anchor/sticky/scroll interaction is subtle and well-tested.

**Verification.** Sticky, position-area, and `scrollIntoView` test suites; the
css-anchor-position WPT tail; parity gate.

**Exit criteria — MET (2026-07-10).** No resolver retains a correctness-gated estimator
call; position-area geometry is sourced from shared; sticky size/position, scrollIntoView
(general + cross-frame + visual-viewport fixed-target) all resolve from the snapshot. The
estimator remains only as a shared-unavailable fallback, to be dropped/degraded in 2.4.

### Milestone 2.4 — Flip `UseSharedGeometryExclusively`, delete the estimators (increment 6)

**Goal.** The actual payoff: remove the ~2950-LOC estimator body from
`LayoutMetrics.cs` and retire `WithLayoutGeometryCache`.

**Depends on:** 2.1, 2.2, 2.3 (all estimator fallbacks covered).

**Tasks.**

1. Flip `DomBridge.UseSharedGeometryExclusively` to `true` (fallback→zeros for
   unzoomed boxless elements — detached/`display:none` semantics; mechanism
   already staged and tested by `SharedGeometryExclusiveCutoverTests`).
2. Delete the estimator method bodies, keeping only the thin JS-facing wrappers
   that call the provider.
3. Delete `WithLayoutGeometryCache` (the provider's snapshot cache replaces it)
   and the estimator-only caches (`LayoutMetrics.cs:23,25`).

**Empirical findings (2026-07-09) — the two tasks have opposite readiness.**

- **Task 1 (flip) — verified regression-free, but not the bottleneck.** Flipping
  `UseSharedGeometryExclusively` to `true` and measuring: the Cli geometry corpus
  (Offset, BoundingClientRect, DisplayContents, ScrollMetrics, ClientAndScroll,
  cutover) changed by exactly **one** test — `SharedGeometryExclusiveCutoverTests`'
  `DefaultsOff`, which asserts the flag's default (expected). WPT `CssomView`
  (excluding the pre-existing scroll/iframe stack-overflow crasher) gave the **same
  4 failures** flag-on and flag-off — **zero delta**. So the entry-point estimator
  fallback is effectively dead: the shared path already answers every boxed element,
  and boxless→zero matches detached/`display:none`. (Reverted; the flag stays staged
  off — flipping alone has no payoff without the deletion, and the full WPT/Acid
  pixel corpus + the crasher path are unverified.)
- **Task 2 (delete) — BLOCKED, and the flip does not unblock it.** The estimator
  methods are a single connected web shared between entry points *and* resolvers, so
  nothing deletes cleanly even with the flip on: `ResolveContentBoxExtent`/
  `ResolveBorderBoxExtent` are sticky's *size* fallback; `ComputeOffsetWithinAncestor`
  is scrollIntoView's gated-case fallback **and** the two visual-viewport fixed-target
  sites; and these call the shared position/size estimator helpers recursively. The
  resolver fallbacks are **not** gated by `UseSharedGeometryExclusively` (only the
  entry points are), so they keep the estimator alive regardless of the flip.

**The renderer blocker is now CLEARED (2026-07-10).** `ComputeOffsetWithinAncestor` can
be deleted once the resolver fallbacks are removable, which needed the renderer to (a) place
abspos-in-inline-CB elements at their inset position, (b) expose cross-frame/subframe geometry
in the main coordinate space, and (c) supply fixed-target offsets for the visual-viewport
scrollIntoView sites. All three are done (Track 3: 3.1, 3.2, 3.3). Every resolver correctness
gate is gone — sticky size/position, position-area, and scrollIntoView (including cross-frame)
all resolve from the shared snapshot, with the estimator only a shared-unavailable fallback
(cross-origin / non-materialised frames). **2.4 is therefore unblocked**: flip
`UseSharedGeometryExclusively` and delete the estimator body, either dropping the
shared-unavailable fallback to zeros or keeping a slim degraded path for unmaterialised frames.
Gate the deletion on the full parity + WPT pixel + Acid corpus as below.

**Risk.** High — broad behavior surface. Gate on the full parity + WPT pixel +
Acid suites; any net-worse assertion count is a blocker.

**Verification.** Parity gate `shared ≥ estimator` still holds with the
exclusive flag on; WPT check-layout + pixel + Acid baselines hold or improve;
`bridge.mutation` + geometry-query benchmarks show no regression.

**Exit criteria.** Estimator bodies and `WithLayoutGeometryCache` are gone; all
geometry answers come from the provider (or the zeros fallback).

### Milestone 2.5 — Retire `LayoutRuntimeState` (increment 7)

**Goal.** Delete `LayoutRuntimeState` (`ElementRuntimeState.cs`, four
`RuntimeValue<double>` slots reached via `.Layout`).

**Depends on:** 2.3 (position-area resolution no longer stores into it).

**Tasks.** Remove the `.Layout` slots and the `LayoutRuntimeState` type; confirm
no reader remains (`PositionAreaQueries.cs`, any offset-property path).

**Verification.** Full anchor/position-area suite; `DomBridge` builds without the
type.

**Exit criteria.** `LayoutRuntimeState` deleted; RF-BRIDGE-1b "definition of
done" met — the two-box-model duplication is gone.

---

## Track 3 — Renderer prerequisites for 2.4 (Broiler.Layout, not the bridge)

2.4's estimator deletion is gated on three renderer/layout capabilities, established
across the 2.3/2.4 investigation. These are `Broiler.Layout` (parent-repo) and
nested-browsing-context (`Broiler.HTML` orchestration) changes on the engine's
hottest, most-tested path — a **distinct track** from the bridge migration, and each
must be gated on the **full WPT pixel + Acid corpus**, not just the geometry unit
tests. Scoped from investigation (2026-07-09):

**Status: 3.1, 3.2, and 3.3 all COMPLETE (2026-07-10), regression-free.** The two
fixed-target visual-viewport `scrollIntoView` sites and the abspos-in-inline-CB paths no
longer call `ComputeOffsetWithinAncestor` (3.1/3.3); 3.2's geometry composition landed and
its **offset migration is now done** — a `position:fixed` (or root-anchored abspos)
descendant of a subframe resolves against the frame's own sub-viewport, the composed box is
correct, and the **cross-frame `scrollIntoView` gate is removed**. All three renderer
prerequisites are met, so no resolver depends on the estimator for a *correctness* gap any
more; `ComputeOffsetWithinAncestor` survives only as a shared-unavailable fallback
(cross-origin / non-materialised frames). This unblocks 2.4.

### 3.1 — abspos-in-inline-CB placement

**Symptom.** An absolutely/fixed-positioned element whose containing block is an
*inline* box (e.g. `position:relative` on a `<span>`) is placed by the renderer at
the inline-flow position, ignoring its own `top`/`left` insets. The bridge works
around this with `AnchorRegistry.ComputeElementBox`'s `absPosInInlineCB` bypass and
`TrySharedOffsetWithinAncestor`'s matching gate; both must fall back to the estimator,
so `ComputeOffsetWithinAncestor` cannot be deleted.

**Reproduced + localized to the layout engine (2026-07-09).** A probe
(`<div h=50><span id=rel style=position:relative>anchor<a id=t
style="position:absolute;top:10;left:20">…`) reads `t.getBoundingClientRect()` (which
is served by the shared snapshot = the layout engine): **shared/engine gives
`(49.27, 50)`** — the *static* position at the end of the "anchor" text, insets
ignored — while the **estimator gives the correct `(20, 60)`** (rel origin `(0,50)` +
insets). So the bug is in `Broiler.Layout`, and it is **broader than "empty inline"**:
even a *non-empty* inline CB mis-places the abspos. `GetAbsoluteContainingBlockPaddingBox`
→ `GetInlineBoundingBox` and the inset application in `ComputeStaticAndFloatPosition`
(`CssBox.Layout.cs:522`, applies `top`/`left` at ~844) exist, but for an abspos
*nested inside an inline formatting context* the inline line-box layout appears to
(re)place the child at its static in-line position, clobbering / bypassing the inset
application. `CssBox.Text.cs` has no abspos handling (an out-of-flow child should be
removed from the line, not laid out in it). The render tests only exercise `top:0`
(static ≈ inset), which is why the engine bug is currently masked in rendering and
only surfaces through the bridge's shared-geometry offset.

**Root cause — FOUND, and the earlier "third path" note was itself wrong
(2026-07-09).** Instrumenting the `Location` setter proved the abspos target's
`box.Location` is **never set at all** (only static boxes get Location sets); the
mis-placed `(49.27, 50)` therefore does *not* come from `box.Location`. It comes from
`CollectLayoutGeometry`'s **line-rectangle-union fallback**: because the target is
never given a real `PerformLayout`, its used `Size` stays `(0,0)`, so `box.Bounds` is
empty and the collector falls back to `UnionLineRectangles` — the static line rectangle
`FlowBox` parked at the end of the "anchor" text. The deeper reason it is never laid
out: `LayoutBlockChildren`'s **inlines-only branch** (`CssBox.Layout.cs`, the
`ContainsInlinesOnly → CreateLineBoxes` path) lays out *floated* children after the
line pass but has **no equivalent for out-of-flow abspos/fixed children**, so an abspos
whose ancestor chain up to the block is all-inline (the `position:relative <span>`
case) is flowed by `FlowBox` for its static rectangle only and never sized/positioned.
Two further gaps compounded it: (a) the engine never **blockifies** an abspos per
CSS 2.1 §9.7, so an inline-display abspos (`<a>`/`<span>`) skips `PerformLayoutImp`'s
block path (`IsBlock == false`) that resolves width + inset position; and (b)
`GetInlineBoundingBox` measured the inline CB's extent including its *own* out-of-flow
child, whose transient static `Location` dragged the CB origin to `(0,0)`.

**Fix — LANDED in `Broiler.Layout` (2026-07-09), entirely engine-side.** Three
coordinated changes, all CSS-spec-grounded:
1. **`CreateLineBoxes` → `LayoutOutOfFlowInlineDescendants`** (`CssLayoutEngine.cs`):
   after an inline formatting context is flowed, walk its in-flow inline subtree and
   `PerformLayout` each out-of-flow abspos/fixed descendant (mirroring how the block
   path lays out its out-of-flow children; floats/atomic-inlines/blocks run their own
   layout and are skipped).
2. **§9.7 blockification** (`CssBox.Layout.cs`, `PerformLayoutImp`): route out-of-flow
   boxes through the block layout path regardless of computed display, so an
   inline-level abspos resolves its shrink-to-fit width (§10.3.7) and inset position.
3. **Inline static position preserved** (`CssBoxProperties.InlineStaticPosition` +
   `FlowBox` records it + `ComputeStaticAndFloatPosition` honours it): an *auto*-inset
   abspos keeps the inline cursor position `FlowBox` gave it, so it does not re-flow its
   content from the top of its containing block (fixed a 7.6% pixel regression in
   `position-area-abs-inline-container` where the `#inline-container` text jumped to the
   origin); an *explicit* inset still overrides. Plus `GetInlineBoundingBox`
   (`CssBox.ContainingBlock.cs`) now excludes out-of-flow children from the inline CB's
   extent (§10.1).

`AbsPosInlineCbGeometryTests` **un-skipped and green** (`20,60`). Verified
regression-free vs a stashed HEAD baseline: `Broiler.Layout.Tests` 39/40 (the 1 is the
pre-existing project-reference-path architecture test), Cli geometry/anchor/shared +
`SharedLayoutGeometryParity` families green, and the WPT anchor/position-area/sticky +
abspos/cssom-view clusters show **zero new failures** (every failure — e.g.
`PositionAreaScrolling00{2,3}`, `OffsetTopLeft_BorderBoxPaddingEdge`,
`AbsposInBlockInInlineInRelposInline` — also fails at HEAD). Notably
`position-area-abs-inline-container` (which passed at HEAD) still passes.

**Bridge bypasses retired (2026-07-10).** With the engine placing abspos-in-inline-CB
correctly, the two bridge fallbacks that mirrored the old renderer gap are **removed**:
`AnchorRegistry.ComputeElementBox`'s `absPosInInlineCB` bypass (so an abspos anchor in
an inline CB now registers from the real shared box) and
`TrySharedOffsetWithinAncestor`'s matching abspos-in-inline-CB gate (so `scrollIntoView`
on such a target reads the shared offset instead of the estimator). The cross-frame
(3.2) and zoom gates in `TrySharedOffsetWithinAncestor` are **kept**. Verified
regression-free: the WPT abspos-in-inline-CB cluster (`position-area-inline-container`,
`-abs-inline-container`, `AnchorInlineContainingBlock`, `AbsPos*InInlineContainingBlock`)
green; the anchor/position-area/sticky/anchor-scroll/position-try clusters show only the
same pre-existing failures as HEAD (`PositionAreaScrolling00{2,3}`,
`PositionAreaAnchorPartiallyOutside`, `PositionVisibilityRemoveAnchorsVisible`,
`PositionTryGrid001`); Cli oracle + scrollIntoView-abspos + anchor + shared families
green. (`position-area-percents-001` flakes under parallel load — passes 3/3 in
isolation on both HEAD and this change.)

The `PromoteAbsPosFromInlineCBs` DOM-mutation workaround in the anchor resolver is left
in place — it is now redundant for geometry but is a larger, separate anchor-subsystem
change with its own paint/registration coupling; retiring it is a follow-up. 3.1 no
longer blocks `ComputeOffsetWithinAncestor`; 2.4's estimator deletion still waits on
3.2 + 3.3.

### 3.2 — cross-frame / subframe geometry in the main coordinate space

**Symptom.** An element inside an iframe subdocument is laid out in the subframe's own
frame (nested browsing contexts render via isolated rasterise-and-composite — see
nested-browsing-context-rendering), so its shared box is absent from, or in the wrong
frame for, the main snapshot. `scrollIntoView` on such a target needs the target's
main-document offset = iframe's main-doc position + target's offset within the
subframe. Handled today by `TrySharedOffsetWithinAncestor`'s cross-frame gate
(`GetOwningDocumentElement(element) != …(ancestor)` → estimator).

**Work.** Expose subframe box geometry composed into the main coordinate frame (the
iframe's own laid-out position + the subframe's internal geometry), so a subframe
target resolves without the estimator. **Oracle:** the
`ScrollIntoView_Uses_Script_Assigned_Iframe_Position_For_*Fixed_Targets` and
`Subframe*ScrollIntoView` cases.

**Geometry composition LANDED (2026-07-10); offset migration still gated on subframe
fixed-positioning.** Investigation corrected the earlier scope note: the render document
(`GetRenderDocument()` = the live `_document`) *does* carry each iframe's sub-document as a
`#subdoc-root` subtree, and `CollectLayoutGeometry` *does* key boxes for the subframe
elements — but the layout leaves that subtree unpositioned (the iframe is a replaced leaf),
so every subframe box came back at `(0,0,0,0)`. Fixed in `Broiler.Layout` (**main repo**,
not the submodule) with `CssBox.LayoutNestedBrowsingContexts`: a root-only post-layout pass
that finds each `#subdoc-root` box, places it at its frame's content-box origin, sizes it to
the content box (the sub-viewport), runs the normal block layout, and translates the subtree
onto the content origin. A subframe element now reports real `getBoundingClientRect` in the
main coordinate frame (pinned by
`DomBridge_SubframeElement_GetBoundingClientRect_Is_Composed_Into_Main_Frame`: an abspos
target at `(30,40)` in an iframe at `(100,300)` → `130,340,50,50`, was `0,0,0,0`). The pass
is additive and touches only `#subdoc-root` subtrees — absent from the paint document (which
serialises the sub-document into the frame's `srcdoc` and rasterises it separately), so paint
is unaffected; regression-free across the layout, shared-geometry, anchor, and iframe/cssom
clusters.

**Sub-viewport LANDED, cross-frame gate DROPPED (2026-07-10).** The remaining piece — a
`position:fixed` (or root-anchored abspos) element inside a subframe resolving against the
frame content box rather than the global viewport — is done, and entirely engine-side (no
`ILayoutEnvironment` change needed, so no submodule/interface churn):
- `CssBox.IsNestedViewportRoot` marks the `#subdoc-root` box a *sub-viewport*;
  `LayoutSubdocument` sets it and stores the frame content-box dimensions in
  `CssBox.NestedViewportSize` (read back for the viewport basis — the box's used `Size` is
  transiently 0 while its own subtree lays out, since block heights resolve bottom-up).
- `FixedPositioningViewport()` returns the enclosing sub-viewport rect (origin from the live
  `Location`, size from `NestedViewportSize`) when inside a nested browsing context, else
  `(0,0) + ViewportSize` — **byte-identical for every box in the top-level document** (the
  flag only ever exists on `#subdoc-root` boxes in the render doc, never the paint doc). The
  five fixed-positioning viewport reads + placement, and the abspos initial-containing-block
  resolution, route through it; the existing `LayoutSubdocument` translate composes the fixed
  box onto `frameContentOrigin + inset`.
- One supporting fix at the fixed-box layout layer (this *is* 3.3's "known separate bug",
  now resolved): a bottom/right-anchored fixed box placed before its block size resolves
  (used height and `Size.Height` both 0) now derives its border-box height from an explicit
  non-percentage CSS height, so it anchors by its bottom edge, not its top edge (was off by
  its own height — CSS2.1 §10.6.4).

With the composed subframe fixed/abspos geometry now correct, the cross-frame gate in
`TrySharedOffsetWithinAncestor` is **removed**; when a subframe box is absent from the
snapshot (cross-origin / non-materialised frame) the `TryGetSharedLayoutGeometry` lookups
still return false and the caller falls back to the estimator. Verified regression-free: the
two cross-frame fixed `scrollIntoView` oracles (`FixedIframeTarget`,
`ScrollableFixedIframeTarget`, both the Cli and WPT-harness twins) pass via shared once
normalised to `border:0` — the shared path is correctly iframe-border-aware where the
estimator ignored the cross-frame border; the WPT CssomView/Fixed/Sticky cluster shows only
pre-existing failures (SVG hit-testing, writing-mode, smooth-scroll, PositionArea/OffsetTopLeft
pixels, harness-tuple `Assert.IsType` errors, subframe `elementsFromPoint`), each confirmed
to fail identically at the merge-base; layout suite 39/40 (pre-existing architecture test) and
Cli geometry 53/58 (5 pre-existing) unchanged.

### 3.3 — fixed-target offsets for the visual-viewport scrollIntoView sites

**Symptom.** The two visual-viewport `offsetOverride` sites in
`ScrollIntoView`/`ResolveScrollIntoViewOffset` compute a fixed target's position;
`TrySharedOffsetWithinAncestor` subtracts intermediate scroll offsets, which is wrong
for a viewport-anchored fixed box. Left on the estimator.

**Work.** A position-aware shared offset that treats fixed targets as viewport-anchored
(no intermediate-scroll subtraction), then migrate the two visual-viewport sites.
Smaller than 3.1/3.2 and mostly bridge-side, but depends on the fixed box's shared
geometry being correct (interacts with 3.1/3.2). **Oracle:** the visual-viewport /
fixed scrollIntoView cases.

**DONE — split-subtraction landed (2026-07-10).** The correct rule is a *split*, not a
blanket skip. A first attempt with a `viewportAnchored` mode that skipped the whole
intermediate-scroll-subtraction loop looked green but was clamp-luck: for the
`Adjusts_PageTop` fixture (target is an `<input>` inside a `position:fixed; overflow:auto`
box — the fixed element is **itself a scroll container** the target rides) it gave 1268 vs
the estimator's 744, and both saturate the same max visual-viewport offset. So the loop now
subtracts scroll only for containers **at or below** the target's nearest `position:fixed`
ancestor F (the fixed box's own overflow + any scroller nested inside it, which the target
rides) and **stops** once the walk climbs past F (containers above F don't move a
viewport-anchored box). Implemented in `TrySharedOffsetWithinAncestor(..., viewportAnchored)`
via `FindNearestFixedAncestorOrSelf` + a DOM ancestor check; the two
`ScrollFixedElementIntoVisualViewport` `offsetOverride` sites now call
`OffsetWithinAncestorForFixedPreferShared` instead of `ComputeOffsetWithinAncestor`.

Validated against a **non-clamp-saturated** fixture
(`VisualViewport_ScrollIntoView_Target_In_TopFixed_Scroller_Uses_Unclamped_Offset`, added):
a top-anchored fixed scroller with the target 150px down, scrolled into a 384px visual
viewport, lands `pageTop` at 650 (= 500 layout + 150) — well under the 884 clamp. Probes
confirmed the migration is behaviour-identical to the estimator wherever the fixed box's
geometry is well-defined: for a **top-anchored** fixed element the shared offset equals the
estimator *exactly* (36 = 36). The default (non-anchored) path — sticky, general
scrollIntoView, position-area — is byte-for-byte unchanged (the split only engages under
`viewportAnchored`). Zero new failures across the fixed / visual-viewport / scrollIntoView
clusters; the residual pre-existing failures (`VisualScrollIntoView_002`,
`SubframeRootScrollIntoView_Uses_SmoothScrollBehavior`, `ScrollIntoView_Maps_WritingMode…`)
fail identically at HEAD.

**Known separate bug it exposed — FIXED (2026-07-10, as part of 3.2's sub-viewport work).**
A `bottom`/`right`-anchored fixed box was mis-placed in the shared geometry by its own extent
— `getBoundingClientRect()` on a `position:fixed; bottom:0; height:60` box reported
`top = innerHeight` (should be `innerHeight − 60`), anchoring by its top edge to the viewport
bottom instead of its bottom edge — because it was placed before its block size resolved
(`ActualBottom − Location.Y` and `Size.Height` both 0, so the subtracted box height was 0).
The fixed `hasBottom` placement now derives the border-box height from an explicit
non-percentage CSS `height` in that case (mirroring the abspos IMCB definite-height fallback,
CSS2.1 §10.6.4). Surfaced concretely by the subframe fixed `scrollIntoView` oracle, whose
`bottom:10` container was landing 150px too high until this fix.

**Gate to close 2.4 — OPEN (2026-07-10).** All three renderer prerequisites are complete:
3.1 places abspos-in-inline-CB correctly (bypass + gate removed); 3.3 reads the two
fixed-target visual-viewport scrollIntoView sites from the shared snapshot; and 3.2's
sub-viewport lands the last piece — subframe fixed/abspos geometry composes correctly and the
cross-frame `scrollIntoView` gate is removed. No resolver now calls
`ComputeOffsetWithinAncestor` (or the size estimators) for a *correctness* gap; the estimator
survives only as a shared-unavailable fallback for cross-origin / non-materialised frames.
**Next: 2.4** — flip `UseSharedGeometryExclusively` (verified regression-free) and delete the
estimator body + `WithLayoutGeometryCache`, keeping (or degrading to zeros) the
shared-unavailable fallback path. 2.5 (`LayoutRuntimeState`) opens up after 2.4.

---

## Track 1 — v1 public-surface removal

### Milestone 1.0 — Declare `htmlbridge-public-surface/v2` (governance, Open Question #5)

**Goal.** Make the removal-boundary decision the whole track is gated on.

This is a **public-API / governance decision**, not an engineering task — it is
Open Question #5 in the promotion roadmap and is encoded in
`DomElement.RemovalBoundaryVersion` / `HtmlTreeBuilder.RemovalBoundaryVersion`
and frozen by `HtmlBridgePromotionPhaseZeroTests`. It must be made by the
maintainer, not taken unilaterally. Declaring v2 authorizes Milestones 1.1–1.3.

**Note.** As of 2026-07-09 the maintainer chose to keep the boundary **frozen**
and defer to Track 2 (see the promotion roadmap's item-1 decision). Revisit this
milestone when Track 2 nears completion so v2 is declared once, cleanly.

### Milestone 1.1 — Remove the two zero-caller shims

**Goal.** Delete `DomBridge.CalculateSpecificity` (`Css.cs:108`, a static
delegation shim over `CssSelectorParser.CalculateSpecificity`, **no production
callers**) and `DomBridge.CssRules` (`Css.cs:54`, obsolete tuple view, **no
production callers**).

**Depends on:** 1.0 only. Independent of Track 2.

**Tasks.**

1. Reroute the two `CssRules` **test** consumers — `CssExtractionPhaseTwoTests`,
   `SelectorsLevel4SpecificityTests` — to the shared `Broiler.CSS`
   stylesheet/style-engine APIs.
2. Delete both members.
3. Update `HtmlBridgePromotionPhaseZeroTests`
   (`CssRules_Compatibility_Tuple_View_Remains_An_Obsolete_Bridge_Seam`,
   `CalculateSpecificity_Remains_A_Static_Delegation_Shim_Over_The_Css_Parser`)
   to assert removal instead of freezing the seam.

**Risk.** Low — no production callers; surgical.

**Verification.** `Broiler.Cli.Tests` build + the affected CSS extraction /
selectors suites green.

**Exit criteria.** Both members gone; guard tests updated.

### Milestone 1.2 — Migrate `DomElement` callers off the facade

**Goal.** Reduce the ~58 non-submodule source references to `DomElement` to zero
by moving callers onto canonical `Broiler.Dom.DomNode`/`DomElement`.

**Depends on:** Track 2 (2.4/2.5) — the geometry caches must stop keying on the
facade first. The remaining identity-keyed caches (runtime state, JS object
identity, computed style) also re-key onto canonical nodes here.

**Tasks (staged; each independently verifiable).**

1. Re-key `ElementRuntimeState`, `_jsObjectCache`, computed-style engines, and
   computed-props caches from the facade to canonical node identity.
2. Migrate the non-geometry caller clusters (serialization, attributes,
   traversal, event/callback plumbing) file-by-file.
3. Keep a caller-count guard (`grep -rlE '\bDomElement\b' --include=*.cs src/`)
   trending to zero; `log()`-equivalent progress in the PR series.

**Risk.** High — 58 files, broad surface; must stay behind green tests at every
slice.

**Verification.** Full bridge test suite green after each slice; caller count
strictly decreasing.

**Exit criteria.** No non-adapter code references the facade `DomElement`.

### Milestone 1.3 — Remove `DomElement` + `HtmlTreeBuilder`

**Goal.** Delete the facade node type and the parser adapter that materializes it.

**Depends on:** 1.2 (no callers) + 1.0 (v2 declared).

**Tasks.**

1. Delete `Broiler.HtmlBridge.Core/Dom/DomElement.cs` and `HtmlTreeBuilder.cs`
   (its `Build`/`BuildFragment` only mint `DomElement`); migrate `HtmlTreeBuilder`
   callers (`DomBridge.cs`, `SubDocuments.cs`, `Registration.cs`,
   `SubDocumentObjects.cs`) to canonical `Broiler.Dom.Html.HtmlDocumentParser`.
2. Update/remove the frozen seam assertions in `HtmlBridgePromotionPhaseZeroTests`
   (`DomElement_And_HtmlTreeBuilder_Adapter_Seam_Is_Versioned_And_Frozen`).

**Risk.** High — final cutover.

**Verification.** Full bridge + WPT/Acid suites; the promotion roadmap's Phase 5
exit criteria (`HtmlBridge` contains bridge responsibilities only).

**Exit criteria.** `DomElement`, `HtmlTreeBuilder` deleted; Phase 5 of the
promotion roadmap closed.

---

## Consolidated ordering

1. **2.1** Zoom-correct shared snapshot *(gating; renderer/submodule change)*
2. **2.2** Root/viewport scroll extent from shared
3. **2.3** Scroll-aware pre-layout source → migrate sticky, position-area, `scrollIntoView`
4. **2.4** Flip `UseSharedGeometryExclusively`; delete estimators (increment 6)
5. **2.5** Retire `LayoutRuntimeState` (increment 7) — **Item 2 done**
6. **1.0** Declare `htmlbridge-public-surface/v2` *(governance; can also authorize step 7 earlier)*
7. **1.1** Remove `CalculateSpecificity` + `CssRules` *(needs only 1.0)*
8. **1.2** Migrate `DomElement` callers off the facade
9. **1.3** Remove `DomElement` + `HtmlTreeBuilder` — **Item 1 done**

Steps 6–7 may run at any point after 1.0 is decided (they do not depend on Track
2). Steps 8–9 depend on Track 2 completing.

## Validation plan (applies to every milestone)

- Shared-geometry families: `SharedScrollOverflowTests`, `SharedGeometryZoomSizeTests`,
  `SharedGeometryExclusiveCutoverTests`, `SharedLayoutGeometryParityTests`,
  `SharedLayoutGeometryTests`, `SharedLayoutGeometryProviderTests`.
- Parity gate `shared ≥ estimator` on the WPT check-layout corpus
  (`SharedLayoutGeometryParityTests`), `KnownRendererGapRegressions = 0`.
- CSSOM Zoom suite, scroll family, sticky/anchor/position-area suites.
- WPT pixel + Acid baselines (baseline first — some fail environmentally per
  `CLAUDE.md`).
- Bridge guard tests `HtmlBridgePromotionPhaseZeroTests`.
- Performance: `bridge.mutation` + a geometry-query benchmark.

## Submodule note

Milestone 2.1 (and possibly 2.3) edits the `Broiler.HTML` submodule. Follow the
push-or-patch workflow in `CLAUDE.md`: attempt the push to `MaiRat/Broiler.HTML`;
only bump the pointer if the push succeeds; on a 403 fall back to a `patches/`
file and leave the pointer unchanged.
