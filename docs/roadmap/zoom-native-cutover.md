# CSS `zoom` — increment 6 cutover runbook (flip `NativeZoom` on, delete the bake)

Status: **not yet executed** — this is the executable plan. Increments 1–5 are landed
behind the `NativeZoom` flag (see `docs/roadmap/htmlbridge-complexity-reduction-notes.md`,
the CSS `zoom` endgame section). Increment 6 is the irreversible cutover from the bridge
**serialization bake** to the engine **used-value** model. It must not be done until every
precondition below is met, because it is atomic (no half state is correct) and it removes
the only code path that carries `zoom` for the still-unapplied submodule patches.

## What "the flip" is

Two zoom models exist in the tree:

- **Bake (current, `NativeZoom` off).** `DomBridge.ApplyZoomSerializationStyles`
  pre-multiplies length properties into the DOM at serialization time and strips the
  `zoom` property, so the renderer lays out an already-zoomed DOM. Invoked from
  `SerializeToHtml` and `GetRenderDocument` (`DomBridge.Serialization.cs`).
- **Engine (target, `NativeZoom` on).** `CssBoxProperties.EffectiveZoom` scales *used
  values* during layout (increments 1–5); computed values stay unzoomed, per spec. The
  DOM keeps its `zoom` property; the engine reads it via the cascade (`CssBox.Zoom`).

They are **mutually exclusive**. Running both double-counts (the bake scales the DOM, the
engine scales again); running neither drops zoom entirely. So "enable `NativeZoom`" and
"delete the bake" are one atomic change.

> Not in scope: the **visual-viewport / pinch-zoom** bake (`ApplyVisualViewportSerializationState`,
> `NativeVisualViewport`, patches 0006–0011) is a *separate* track (a root canvas scale, not
> element `zoom`). Increment 6 removes only the *element*-`zoom` bake
> (`ApplyZoomSerializationStyles` and its helpers). Leave the visual-viewport path alone.

## Preconditions (all must hold before flipping)

### P1 — the three submodule paint patches are applied and their pointers bumped
`patches/0001` (calc), `0002` (text-shadow), `0003` (SVG) are pinned/unapplied. Until they
land, the engine path does **not** scale `calc()` lengths, `text-shadow` offsets, or
view-box-less SVG — the bake is currently the only thing that does. Flipping before they
land regresses exactly those cases. Also **re-add the calc parent wiring** that was reverted
for the pinned-pointer build (patch 0001's parent side): in
`CssBoxProperties.ParseLengthWithLineHeight` and `ParseInsetLength`, wrap the `ParseLength`
call for a `(`-containing length with
`CssLengthParser.SetElementZoom(EffectiveZoom, percentAgainstContainingBlock ? OwnZoom : 1.0)`
… `finally SetElementZoom(1.0, 1.0)` (see `patches/README.md` §0001). This is only safe once
`Broiler.CSS` is bumped to a SHA that has `SetElementZoom`.

### P2 — `NativeZoom.Enabled` is set on the layout thread
`NativeZoom.Enabled` is `[ThreadStatic]` (deliberately, so it can't leak across concurrent
layouts). The bake works by mutating the DOM, which is thread-independent; the engine flag is
not. **Verify** which thread runs `PerformLayout` relative to the render/serialize entry, and
set the flag there for the duration of layout (mirror how `NativeAnchorPlacement.Enabled` /
`NativeVisualViewport` are scoped). A flag set on the bridge thread but read on a different
layout thread is a silent no-op — this is the most likely source of a "flip did nothing" bug.

### P3 — CSSOM metric reconciliation still divides out zoom  *(favorable — verify, don't rebuild)*
`LayoutMetrics` reports `clientWidth`/`offsetWidth`/… by dividing the (zoom-baked) snapshot
extent by the element's used zoom (`UnzoomSharedExtent`, `GetUsedZoomForElement`). Crucially
`GetUsedZoomForElement` reads `zoom` from **computed props**, not the stripped inline style —
so it is **source-agnostic**: it works whether the snapshot geometry was zoomed by the bake
or by the engine, as long as the engine produces the same zoomed geometry. So this path should
survive the flip unchanged; the task is to **prove** it (the `SharedGeometryZoomSize` Cli
tests are the guard — they must stay green on the engine path), not to rewrite it. One thing
to check: those tests document that "the render pipeline bakes zoom into the serialized box
sizes"; confirm the wording/behaviour still holds when the *engine* supplies the zoomed sizes.

### P4 — geometry/pixel validation (no reftest corpus exists)
This is the step the roadmap has flagged from the start. There is no reftest corpus and no
display in CI, so validate by **equivalence** instead (see below).

## The flip (exact changes, once P1–P4 hold)

1. **Re-add calc parent wiring** (P1) — `CssBoxProperties.cs`.
2. **Enable the flag** on the layout thread (P2) — scope `NativeZoom.Enabled = true` around
   the render/geometry layout entry, restoring it in a `finally`.
3. **Stop baking** — `DomBridge.Serialization.cs`: remove the `ApplyZoomSerializationStyles`
   calls in `SerializeToHtml` (line ~37) and `GetRenderDocument` (line ~62). Then delete the
   now-dead bake surface:
   - `ApplyZoomSerializationStyles`, `TryGetZoomSerializableValue`, `GetZoomSpecifiedStyleMap`,
     `ZoomPreferSpecifiedProperties`, `ZoomScaledSerializationProperties`,
     `TryScaleSerializableCssValue`, `TryScaleLengthToken`;
   - the pseudo overrides: `ApplyZoomPseudoSerializationOverrides`,
     `CollectZoomPseudoSerializationOverrides`, `AppendZoomPseudoSerializationOverride`
     (engine materialises real pseudo boxes — increment 5 slice 4 — so these are redundant);
   - the SVG bake: all of `DomBridge.Serialization.SvgZoom.cs`
     (`ApplyZoomSerializationSvgAttributes` and helpers);
   - the revert plumbing: `_zoomSerializationRevertLog`, `RevertZoomSerialization`, and the
     record-keeping in `ApplyZoomSerializationStyles`.
   - **Keep** `ResolveSpecifiedZoom`, `GetUsedZoomForElement`, `RootUsedZoomBase` — the CSSOM
     reconciliation (P3) still needs them.
4. **Confirm `zoom` reaches `CssBox.Zoom`** on the render path (the cascade → `CssUtils`
   `zoom` dispatch, added in increment 1) and is **not** stripped anywhere now that the bake
   is gone.

## Validation strategy (equivalence, since no reftest corpus)

Land a temporary **A/B equivalence harness** (test-only, deleted after cutover) that renders a
set of representative zoomed pages through both paths and diffs the result:

- **Path A (baseline):** `NativeZoom` off, bake on → `GetRenderDocument` → layout → capture the
  `DisplayList` / box geometry.
- **Path B (target):** `NativeZoom` on, bake removed → same capture.
- Assert B ≈ A within tolerance for: box geometry (border/padding/content rects), font sizes,
  border/outline widths, border-radius, insets/margins, multi-column tracks, `text-shadow`
  offsets, and SVG shape geometry (both view-boxed and view-box-less).

Corpus to cover (each `× zoom ∈ {1, 1.5, 2}`, nested where noted): absolute-length box; `%`
width/inset; `em`/`rem` lengths; `calc()` mixed units; nested zoom (compounding); abspos
`inset:0; margin:auto` centring; `fit-content` modal; multi-column; outline; border-radius
(px and `%`); `text-shadow`; `::before`/`::after`; view-boxed and view-box-less SVG;
`word-spacing`. Then run the full `Broiler.Cli.Tests` CSSOM suite on Path B — the
`SharedGeometryZoomSize` / geometry / metric tests are the CSSOM guard (P3).

Only after A ≈ B across the corpus **and** the CSSOM suite is green on Path B should the flip
be committed and the bake deleted.

## Rollback

The flip is one commit. If Path-B validation regresses after cutover, `git revert` restores the
bake (its code lives in history). Because the flag is thread-scoped and the bake is pure DOM
mutation, there is no persistent state to unwind. Keep the equivalence harness until a real
reftest corpus exists.

## Readiness checklist (current status — 2026-07-19)

- [ ] **P1** patches 0001–0003 applied + `Broiler.CSS` / `Broiler.HTML` pointers bumped +
  calc parent wiring re-added. *(Blocked: the submodule remotes 403 from this session's scope;
  applying the patches and bumping the pointers is an environment/maintainer action.)*
- [ ] **P2** `NativeZoom.Enabled` thread-locality confirmed at the render/geometry layout entry.
- [ ] **P3** CSSOM `SharedGeometryZoomSize` suite green on the engine path *(expected to hold —
  the divide-out reads computed zoom, which is source-agnostic — but unverified).*
- [ ] **P4** A/B equivalence harness written and green across the corpus above.

When all four are checked, execute "The flip" above as a single commit.
