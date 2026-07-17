# Submodule patches

Patches for the `MaiRat/Broiler.*` submodules whose remotes are outside this session's
GitHub scope (`git push` → 403). Each captures a submodule change validated by building the
parent in place; a maintainer applies the patch to the submodule, pushes it, and bumps the
gitlink pointer in the parent. Until then the submodule pointer is unchanged and any active
fallback lives in the main repo (noted per patch).

Apply a patch with:

```sh
cd <Submodule>
git am < ../patches/NNNN-<slug>.patch     # or: git apply
```

## Native dialog/backdrop track — box-chrome slice (2026-07-16)

Two patches make the native UA `<dialog>` **box chrome** (border/padding/background) work
through the real cascade, so the bridge's box-chrome pre-bake in
`src/Broiler.HtmlBridge.Dom/DomBridge/AnchorResolver/Dialogs.cs` (`InsertDialogBackdrops`) can
be deleted. **Apply in order — 0004 depends on 0003.**

The **display** slice that preceded this (patches 0001 + 0002 — the `:not([attr])`
selector-matcher fix and the UA `dialog { display: block }` rule) has **landed**: it is in the
pinned submodule commits (`Broiler.CSS` `ce521e3`, `Broiler.HTML` `000274e`), so those patch
files are removed and the bridge `display: block` pre-bake in `InsertDialogBackdrops` has been
deleted (it was the CI fallback that is no longer needed).

### 0003-css-shorthand-longhand-origin-precedence.patch → `Broiler.CSS`

General cascade correctness fix (not dialog-specific). A higher-precedence author *shorthand*
(e.g. `background: lime`) failed to override a lower-precedence user-agent *longhand* (e.g.
`background-color: white`): the post-cascade shorthand expansion is `!ContainsKey`-gated, so it
keeps any already-present longhand regardless of origin. Only `margin`/`padding` seeded their
longhands into the cascade, so the leak hit `background` (and, symmetrically, the border
families). The fix generalises the longhand seeding (`AddShorthandLonghandSlots`) to reuse the
canonical `ExpandCssShorthands` expander for every modelled shorthand, so each shorthand competes
for its longhands by origin/specificity/source order.

The `border` / `border-<side>` shorthands are **excluded** from seeding because their
omitted-component reset to initial is handled by the separate, origin-blind
`ApplyBorderShorthandResets` pass (which requires those longhands to be *absent* from the map);
seeding them would defeat that reset (an `!important border: 1px solid` could no longer reset an
earlier `border: … red`). This is why the pre-existing `table`/`td` grey-border-color workaround
in `DomParser` (`CssDefaults.cs` note) still stands — the border-longhand-vs-author-shorthand case
is deliberately out of scope here.

Includes a regression test (`ShorthandLonghandOriginTests`). Full available css WPT corpus
(CSS2, css-align, css-backgrounds, css-animations, css-anchor-position; 147 tests) is
**byte-identical** with and without the fix (36 fails, identical set); 218 `Broiler.CSS.Dom.Tests`
pass (the 2 `CssDomArchitectureTests` failures are pre-existing/environmental).

### 0004-html-native-dialog-ua-box-chrome.patch → `Broiler.HTML`

Extends the native UA dialog rule in `CssDefaults.DefaultStyleSheet` with the box chrome the
bridge pre-bakes for modal dialogs: `dialog { … border: 1px solid black; padding: 1em;
background-color: white }`. Applies to every rendered (open) dialog through the cascade — matching
the HTML UA stylesheet, which styles all dialogs, not only modal ones (an improvement over the
bridge's modal-only bake). **Requires 0003** — without the shorthand-vs-longhand fix, the UA
`background-color: white` longhand leaks past an author `background` reset and regresses WPT
`anchor-position-top-layer-003/004/006` (to ~97.5%). With both patches applied, the
css-anchor-position corpus stays 33/6 and all 7 top-layer tests pass at 99.9–100%.

### Follow-up once 0003 + 0004 are applied and the pointers bumped

Delete the box-chrome pre-bake in `InsertDialogBackdrops` (the `border-width`/`border-style`/
`border-color`, `padding`, and `background-color` writes). It is the **active main-repo CI
fallback** and must stay until the patches land (CI clones submodules by pointer, so it styles
modal dialogs until the UA rule is live). Removing it was validated end-to-end: with 0003 + 0004
applied and the box-chrome bake removed, the 7 `anchor-position-top-layer-*` tests and the
`NativeModalDialogAnchorWptTests` / bridge `Dialog`/`Backdrop`/`Popover` unit tests pass and the
css-anchor-position corpus stays 33/6.

Not covered by this slice (later dialog/backdrop track work): modal centering / `position: fixed`,
native `::backdrop` box generation, and native top-layer paint (all `Broiler.HTML`). See the
native dialog/backdrop track section in
`docs/roadmap/htmlbridge-complexity-reduction-roadmap.md`.

## Engine-native live-geometry track (2026-07-16)

Phase 5 LayoutSnapshot endgame. The script bridge answers element-geometry queries
(`offsetLeft`/`getBoundingClientRect` during script) from a shared layout snapshot built by
`HeadlessLayoutView` (the bridge's `ILayoutView`). Today that snapshot carries pre-bake static
placement for CSS-anchor-positioned boxes, and the bridge's *live* anchor resolvers
(`ResolvePositionAreaForElement` / `ResolveAnchorInsetForElement` / `ResolveAnchorSizeForElement`
/ `ResolvePositionTryForElement` in `src/Broiler.HtmlBridge.Dom/DomBridge/AnchorResolver/`) patch
the resolved geometry back in on read. The migration moves that resolution into the engine so the
snapshot is authoritative and the bridge resolvers can eventually be deleted (mirrors the WPT
native anchor-placement cutover P5.8d).

Increment 1 enables `Broiler.Layout.Engine.NativeAnchorPlacement` around the headless geometry
layout so the live snapshot carries engine-resolved position-area / `anchor()` / `anchor-size()`
boxes. Increment 2 additionally threads the document's `@position-try` at-rules into the engine's
out-of-band `NativeAnchorPlacement.PositionTryRules` channel, so a position-try box whose base
overflows carries its resolved *fallback* placement in the snapshot too, not merely its base. The
enabling `InternalsVisibleTo Include="Broiler.HTML.Headless"` (the flag is `internal` to
`Broiler.Layout`) is a **main-repo** change (`Broiler.Layout` is not a submodule) and has landed;
only the `HeadlessLayoutView` edit is a submodule change (patch 0005).

### 0005-html-native-live-geometry-headless.patch → `Broiler.HTML`

`HeadlessLayoutView.GetGeometry` sets `NativeAnchorPlacement.Enabled = true` **and**
`NativeAnchorPlacement.PositionTryRules = ParsePositionTryRules(document)` (both thread-static,
save/restore) around `_container.GetLayoutGeometry(viewport)`, so the geometry snapshot the bridge
reads is laid out with the engine's native anchor-positioning post-pass, including the
`@position-try` fallback pass. `ParsePositionTryRules` walks the document's `<style>` elements and
parses them with the canonical `Broiler.CSS.PositionTryRule` model — the same rule bodies the
bridge resolver and the WPT runner use.

Validated (patch applied):
- `PositionAreaLiveGeometryTests` pass **from the snapshot alone** (bridge
  `ResolvePositionAreaForElement` short-circuited to `return null`) — the snapshot is authoritative
  for position-area geometry.
- Both the fixed-size (`FixedSizeOverflowingBase_…`) **and** the `min-content`
  (`OverflowingBase_…`, the `position-try-002` shape) position-try live tests pass **from the snapshot
  alone** with *both* the `ResolvePositionTryForElement` and `ResolveAnchorInsetForElement` resolvers
  short-circuited — proving the snapshot carries the native *fallback* placement. Both resolvers must
  be silenced because the offset getter precedence is position-area → position-try → anchor-inset →
  anchor-size → snapshot, so the anchor-inset resolver would otherwise intercept with the box's *base*
  `anchor()` inset. (An engine probe confirms the `min-content` box is handed off, not baked: it
  reaches `CssBox.TryApplyPositionTryFallback` with `position-try-fallbacks` intact and `Bounds.Width`
  already the laid-out intrinsic `200`, so the native pass flips it using the engine's real intrinsic
  width — consistent with P5.8d.2b, which retired the render-side `min-content` bake.)
- The full anchor/live-geometry suite (29 tests) stays green with the resolvers restored.

So the live read model is snapshot-authoritative for position-try fallback across **fixed-size and
`min-content`** boxes. The only position-try residue that still bakes is `max-content` / `fit-content`
(deliberate, pending a validating corpus test — see P5.8d.2b), not a live-read gap.

The bridge live anchor resolvers remain the **active main-repo CI fallback** and must stay until
this patch lands and the pointer is bumped (CI clones the submodule by pointer, so without the
patch the snapshot carries static placement and the resolvers supply the resolved geometry). Once
0005 lands, the resolvers can be retired incrementally (position-area first, then anchor insets and
size, then position-try — fixed-size and `min-content` together; the `max-content`/`fit-content`
estimator-parity slice is the only piece that waits).

## Visual-viewport / CSS `zoom` endgame track (2026-07-16)

Phase 5 LayoutSnapshot endgame, step-6 blocker (b). A document-root visual-viewport pinch-zoom (or
`html { zoom }`) is a **uniform** scale of the whole document. Today the bridge fakes it by scaling
every length-valued property into inline styles at serialization (`ApplyZoomSerializationStyles`, fed
the pinch factor by `ApplyVisualViewportSerializationState`), because the layout engine has no `zoom`
model. The endgame moves that to a **viewport transform where geometry leaves the box tree** — a
main-repo box-tree scale is infeasible (a box's padding/border/font geometry is computed from CSS
length *strings*, not stored as scalable numbers), so the scale is applied to the extracted
`BoxGeometry` rects instead. See the design + feasibility finding in
`docs/roadmap/htmlbridge-complexity-reduction-roadmap.md` (endgame section, blocker (b)).

The enabling channel `Broiler.Layout.Engine.NativeAnchorPlacement.VisualViewportScale` (a thread-static
`double`; `0`/`1` mean no scale) is a **main-repo** change and has landed (dormant — nothing in the
committed tree sets it, so behaviour is unchanged). Only the extraction-scale consumer is a submodule
change (patch 0006).

### 0006-html-visual-viewport-extraction-scale.patch → `Broiler.HTML`

`HtmlContainerInt.CollectLayoutGeometry` multiplies every collected element's three `BoxGeometry` rects
(border / padding / content box) by `NativeAnchorPlacement.VisualViewportScale` about the document
origin — exact for a uniform zoom — instead of the box tree being laid out from bridge-baked scaled
lengths. Inert unless the channel is set.

Validated (patch applied, local): with the channel at `2.0`, an abspos box (`left:50 top:60 width:100
height:40 border:5 padding:10`) reports all three box-model rects scaled exactly ×2 (border box
`50,60,110,50 → 100,120,220,100`; padding and content boxes scale uniformly too); at the default `0`
the geometry is unchanged and the anchor / live-geometry Cli suites stay green. The validation test is
**not committed** — it requires this patch, so it cannot pass at the pinned submodule SHA on CI (same
constraint as patch 0005 / the sticky live-read gap).

This is the submodule half of the (b1) read-model cutover. The **main-repo half has landed dormant**
behind `DomBridge.NativeVisualViewport` (default off): `SharedLayoutGeometry` sets the
`VisualViewportScale` channel from `GetVisualViewportScale()` around the geometry snapshot, and
`LayoutMetrics.GetUsedZoomForElement` folds the same scale as the root used-zoom base so `offset*` divides
it back out while `getBoundingClientRect` keeps it (CSSOM-View: pinch-zoom is a root zoom in this model).
Validated end-to-end locally **with this patch applied and the flag on**: `visualViewport.scale = 2` leaves
`offsetLeft`/`offsetWidth` unaffected and scales `getBoundingClientRect` ×2.

**To activate:** apply this patch **and 0007** (the snapshot cache key, below), bump the `Broiler.HTML`
pointer, and flip `NativeVisualViewport` on in the live bridge construction. **Not covered:** the
render/paint half (magnifying a pinch-zoomed page's paint) is still the WPT-runner `zoom` bake's job, so
`ApplyVisualViewportSerializationState` stays until a native paint transform lands; and general mid-tree
`zoom: N` (reflow) is the separate (b2) engine-zoom feature.

### 0007-html-visual-viewport-snapshot-cache-key.patch → `Broiler.HTML` (depends on 0005)

`HeadlessLayoutView.GetGeometry` adds the `NativeAnchorPlacement.VisualViewportScale` channel value (which
`DomBridge` sets around the call) to its `(document, version, viewport, baseUrl)` snapshot cache key. The
pinch scale scales the extracted geometry but is not a DOM mutation, so it does not bump
`DomDocument.Version`; without this, a `visualViewport.scale` change on an otherwise-unchanged document
would serve the stale, differently-scaled cached snapshot. Applies on top of 0005 (same file).

Validated end-to-end locally (0005 + 0006 + 0007 applied, flag on): on a single bridge, reading
`getBoundingClientRect().width` = 100, then `visualViewport.scale = 2`, then re-reading returns 200 (the
snapshot re-lays-out) — without the key change it would return the stale 100. The test is not committed (it
needs the patches, so it cannot pass at the pinned SHA on CI).

## Visual-viewport RENDER/paint half (2026-07-16)

The 0005–0007 track is the read-model (CSSOM geometry) side of blocker (b). The **render** side —
magnifying a pinch-zoomed page's *paint* — is a separate, larger piece: the software rasterizer
`BCanvas` was **translate-only**, so a document-root viewport zoom had no way to scale the painted pixels
(the current WPT-runner path magnifies only because `ApplyZoomSerializationStyles` bakes scaled CSS
lengths into the serialized HTML that gets re-parsed and painted 1:1). The paint pipeline is
`CssBox` → `FragmentTreeBuilder` → `PaintWalker.Paint` → `DisplayList` → `RGraphicsRasterBackend` →
`RGraphics`/`BCanvas` → bitmap; the `TransformItem` IR + `RGraphics.SaveTransformLayer` plumbing already
exists, but the raster surface itself could not scale.

### 0008-graphics-bcanvas-uniform-scale.patch → `Broiler.Graphics`

Gives `Broiler.Graphics.BCanvas` a uniform `Scale(float)` composed with its existing `_translation`: every
draw maps `point → point*scale + translation` through the two central `Translate(rect)`/`Translate(point)`
helpers, with scalar device dimensions (line stroke width, rounded-clip corner radii) scaled too;
saved/restored by `Save`/`Restore`. Uniform-only (not a full affine — exact for a viewport zoom). At scale
`1` (default) every path is byte-identical to the prior translate-only behaviour. Also adds virtual
`RGraphics.PushViewportScale(float)`/`PopViewportScale()` (default no-op) so a backend can apply a
document-root viewport zoom after the device-space viewport clip. Includes a `Broiler.Graphics.Tests` case
(`BCanvas scales draws about the origin`). All existing `BCanvas` tests pass unchanged.

> Note: there are **two** `BCanvas` classes — `Broiler.Graphics.BCanvas` (this patch; used by the
> standalone/WebAssembly graphics paths and `Broiler.Graphics.Tests`) and `Broiler.HTML.Image.BCanvas`
> (the one the HTML render path actually uses, patched in **0009**). The `RGraphics` hook here is what
> 0009's `GraphicsAdapter` override targets.

### 0009-html-viewport-zoom-paint.patch → `Broiler.HTML` (depends on 0008's `RGraphics` hook)

Wires the viewport zoom through the HTML render path:
- `Broiler.HTML.Image.BCanvas` gets the same uniform `Scale` as 0008 (it is a separate copy from
  `Broiler.Graphics.BCanvas` and is the rasterizer `GraphicsAdapter._rasterCanvas` resolves to).
- `GraphicsAdapter` overrides `PushViewportScale`/`PopViewportScale` to compose the scale onto the raster
  `BCanvas` — **raster-only**: it deliberately does NOT bump `_activeCompatLayerDepth` (that flips
  `CanUseRaster` off and would route every draw to the compat backend, bypassing the scale).
- `HtmlContainerInt.ViewportZoom` (default 1) is applied in `PerformPaint` **after** the device-space
  viewport clip, so page content magnifies while the clip stays in device pixels; `HtmlContainer` forwards it.

Validated end-to-end locally (0008 + 0009 applied): a pixel test renders a green `left:10 top:10 20×20` box
at `ViewportZoom` 1 vs 2 — device pixel (45,45) is background at zoom 1 and inside the magnified green box at
zoom 2, and the box origin moves from 10 to 20 (scaled about the origin). **Zero regression**: the
graphics/render suites fail the identical 17 pre-existing Skia/environmental tests with and without the
patches (a Skia backend is absent in this container). The pixel test is not committed (it needs the
patches, so it cannot pass at the pinned SHA on CI).

**Remaining for full bake retirement — the cutover (not yet authored):** thread the bridge's
`_visualViewportScale` to the render entry (`HtmlRender` / `WptTestRunner.RenderHtmlFileBitmap`) as
`HtmlContainer.ViewportZoom`, and have that path **stop** relying on `DomBridge.SerializeToHtml`'s `zoom`
bake for pinch-zoom — painting the unscaled document with the viewport zoom instead — so
`ApplyVisualViewportSerializationState` can retire. Needs pixel validation (there is no pinch-zoom reftest
corpus today). General mid-tree `zoom: N` (reflow) remains the separate (b2) engine-zoom feature.

## Native dialog/backdrop track — top-layer paint slice (2026-07-17)

The next slice of feature (a) (native dialog/backdrop). It replaces the bridge's approximate
top-layer **emulation** — a very-large `z-index` (2,000,000,000 + show order) baked onto open
popovers, and modal dialogs left as plain `position:fixed` — with a real **top-layer paint pass**
so open modal `<dialog>`s, open popovers, and their `::backdrop`s paint above every ordinary
stacking context, ordered by top-layer order. This is the keystone that lets the bridge's
`ApplyPopoverUAPositioning` z-index write (and, later, `InsertDialogBackdrops`) retire.

The split is mostly **main-repo** (Broiler.Layout IR + the bridge marker), with only the paint
consumption in the submodule:

- **Main repo (committed, CI-safe, dormant):** `Fragment.TopLayerOrder` (nullable int) on the
  layout IR, projected by `FragmentTreeBuilder` from a benign `data-broiler-top-layer` order the
  bridge stamps (`Dialogs.cs`, gated on the new `DomBridge.NativeTopLayer` flag — off in
  production, enabled by the WPT runner alongside `NativeAnchorPlacement`). Covered by
  `Broiler.Layout.Tests/TopLayerFragmentProjectionTests`. On CI the pinned `PaintWalker` never
  reads `TopLayerOrder`, so the marker is **inert** and the retained z-index emulation still
  drives the top layer — the 7 `anchor-position-top-layer-*` reftests stay green unchanged.

### 0010-html-native-top-layer-paint.patch → `Broiler.HTML`

- `PaintWalker.PaintFragment` skips a fragment with a non-null `TopLayerOrder` in normal in-tree
  traversal (unless invoked with `paintingTopLayer: true`).
- `PaintWalker.PaintTopLayer` (called from `Paint` after the whole tree) collects every top-layer
  fragment and paints them last, ordered by `TopLayerOrder` (document order breaks ties, so a
  `::backdrop` inserted just before its dialog paints directly beneath it), applying the fixed
  viewport offset. A no-op when nothing is in the top layer, so ordinary documents are untouched.

Validated end-to-end locally (0010 applied + `NativeTopLayer` on): the css-anchor-position corpus
stays **33/6/1** and all 7 `anchor-position-top-layer-*` reftests pass at 99.9–100% (average match
98.76%, marginally above the 98.73% baseline) — pixel-parity with the z-index emulation, zero
regression. With the patch **not** applied (the committed/CI state) the same corpus is byte-identical,
since the marker is inert and the emulation is retained.

### Follow-up once 0010 is applied and the pointer bumped

Delete the `ApplyPopoverUAPositioning` `z-index = TopLayerZIndexBase + order` write (the emulation
the native pass supersedes), then continue the track: native `::backdrop` box generation and modal
centering / `position: fixed`, each of which deletes a further piece of `Dialogs.cs`.

## Native dialog/backdrop track — native ::backdrop slice (2026-07-17)

Builds on the top-layer paint (0010): the bridge stops synthesizing a backdrop `<div>` (a
box-tree mutation) and the renderer generates a native `::backdrop` box instead — the largest
remaining piece of `InsertDialogBackdrops`.

The split is mostly **main-repo** (committed): `CssBox.TopLayerOrder`/`CssBoxProperties`
(Broiler.Layout) so a renderer-*generated* box can carry a top-layer order (a native `::backdrop`
has no element to hold the `data-broiler-top-layer` attribute); `FragmentTreeBuilder.GetTopLayerOrder`
prefers that field, else the attribute; and `Dialogs.cs`, under the new `DomBridge.NativeBackdrop`
flag, stamps the resolved backdrop background (`data-broiler-backdrop` — the UA modal/popover scrim
default folded with any author `background`) on the top-layer element and skips the `<div>`.

`NativeBackdrop` is **off by default and — unlike `NativeTopLayer` — not auto-enabled by the WPT
runner**: the synthesized `<div>` is the CI fallback until 0011 lands (the pinned renderer would
otherwise drop backdrops on the WPT path). Enable locally with `BROILER_WPT_NATIVE_BACKDROP=1`.

### 0011-html-native-backdrop-box.patch → `Broiler.HTML` (depends on 0010)

`DomParser.GenerateNativeBackdrops` post-pass: for each element the bridge marked with
`data-broiler-backdrop`, generate a `::backdrop` box as a sibling **before** the element (so the
0010 top-layer paint's document-order tiebreak paints it beneath), `position:fixed; inset:0`
(viewport-filling, resolved natively) with the resolved background, overlaying author `::backdrop`
*geometry* from the cascade, and `TopLayerOrder` from the element's `data-broiler-top-layer` marker.
Author `::backdrop` `position-try-fallbacks` are not yet carried natively (no corpus); the baked
`<div>` path still handles them.

Validated end-to-end locally (0010 + 0011 applied, both levers on): a modal dialog with a visible
`::backdrop` renders **pixel-identical to the baked `<div>` path** (a 100×100 blue dialog on top of
a red backdrop filling the 300×300 viewport). The css-anchor-position corpus stays 33/6/1 with the
native path, and — on the committed/CI state (patches not applied, `NativeBackdrop` off) — the
`<div>` fallback keeps it byte-identical. The render-validation test is not committed (it needs the
patches, so it cannot pass at the pinned SHA on CI).

### Follow-up once 0010 + 0011 are applied and the pointer bumped

Flip `NativeBackdrop` on for the WPT/production path; the bridge's backdrop-`<div>` synthesis (and
its author-geometry / position-try-backdrop helpers) then delete from `Dialogs.cs`, leaving only the
modal box-chrome and the resolved-background computation.
