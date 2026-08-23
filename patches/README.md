# Pending submodule patches

A fix often belongs in a submodule rather than in this repository. When the push
to that submodule's remote is authorized, the fix lands there and the pointer is
bumped, and nothing appears here. When the push is denied — the submodule remote
is outside the session's GitHub scope, so the git proxy answers **403** — the
commit is captured here instead, as a `git format-patch` file, for a maintainer
to apply upstream.

**This directory is a backlog, not an archive.** A patch file is deleted once its
fix is upstream and the submodule pointer is bumped, and the numbering restarts
from `0001` against whatever is left. The same number therefore names different
changes at different times, so never identify a patch by number in prose that
outlives it — name the **commit subject** instead, and check whether a fix is
live with:

```sh
git -C <Submodule> log --oneline --grep '<commit subject>'
git -C <Submodule> merge-base --is-ancestor <sha> HEAD
```

`scripts/apply-pending-wpt-patches.sh` applies the listed patches to the
checked-out submodule working trees before a WPT run, so a fix that is not in the
pinned pointer is still exercised on CI. It is idempotent: once a maintainer
lands one upstream and bumps the pointer, the patch's reverse-apply check starts
succeeding and it is skipped rather than re-applied.

## Index

| Patch | Submodule | What it is |
| --- | --- | --- |
| `0001-html-svg-viewbox-intrinsic-ratio.patch` | `Broiler.HTML` | An SVG image's `viewBox` gives it an intrinsic aspect ratio whatever `preserveAspectRatio` says (SVG 2 §8.2), and an absolute `width`/`height` it states is reported even when the other one is missing. Listed in the apply script. |
| `0002-html-affine-transform-layer.patch` | `Broiler.HTML` | A rotated or skewed element is resampled through its matrix instead of being dropped on the floor: `transform: rotate(45deg)` painted a blank page and took its whole subtree with it. Pairs with `Broiler.Layout.IR.AffineLayerMap` in the main repo. Listed in the apply script. |
| `0003-html-column-flex-image-stretch.patch` | `Broiler.HTML` | A column flex item's cross-axis stretch reaches the image inside the anonymous wrapper that became the item, instead of leaving it at the 300×150 default object size. Pairs with `Broiler.Layout.Engine.FlexGridItemBlockification.IsStretchedColumnFlexItem` in the main repo. Listed in the apply script. |
| `0004-html-inline-replaced-box-model.patch` | `Broiler.HTML` | An inline replaced element's padding and content rects are deflated out of the line rectangle instead of all three box-model levels being the same rectangle, so `clientWidth`/`clientHeight` on a bordered or padded inline `<img>` stop reporting the border box. Pairs with `Broiler.Layout.BoxGeometry.ForInlineBox` in the main repo. Listed in the apply script. |
| `0005-css-image-set-negative-resolution.patch` | `Broiler.CSS` | A declaration whose `image-set()` carries a negative `<resolution>` is dropped in the cascade, so the declaration under it applies instead of the property falling back to its initial value. No main-repo half — the dependency direction forbids one. Listed in the apply script. |

### `0001-html-svg-viewbox-intrinsic-ratio.patch`

`StubImageAdapter.RasterizeSvg` read `preserveAspectRatio="none"` as removing the
image's intrinsic aspect ratio. SVG 2 §8.2 derives the intrinsic sizing
properties from `width`/`height`/`viewBox` alone; `preserveAspectRatio` governs
how the content is fitted *once the viewport size is known*, which is a later
question than what size the image asks to be. Reading it the other way made such
an image report no ratio, so CSS sized it to the whole background positioning
area.

Measured over the full 26 366-test WPT reftest suite: **18 776 → 18 844 passing,
+68 / −0**, average match 98.42% → 98.46%. Every one of the 68 is in
`css/css-backgrounds/background-size`, which goes **146 → 214 of 217** — the
directory's SVGs all carry `preserveAspectRatio="none"`, so the whole family was
failing by construction.

**It is listed in `PENDING_PATCHES`**, and it has to be: it decides the concrete
size of every SVG background image, which is a pixel difference on 68 tests that
no unit test in this repository can reach — the sizing arithmetic lives in the
submodule's `PaintWalker`, and what this patch changes is the intrinsic data fed
into it.

There is no main-repo half to pair it with, so nothing here compensates while it
is pending; without the apply script the 68 tests stay red on CI.

### `0002-html-affine-transform-layer.patch`

`BCanvas` maps a point per axis — `p → p·scale + translation` — so translation
and axis-aligned scale (mirrors included, and therefore `rotate(180deg)`) fold
into its own state, and anything carrying a `b` or `c` term does not.
`GraphicsAdapter` routed those to the compat backend, which on a headless host is
an inert stub, and that route also switches `CanUseRaster` off for **every draw
the group encloses**. So `transform: rotate(45deg)` on a green square painted a
**blank page**, and took its whole subtree with it. Opacity, filter and blend had
already been moved off the same fall-through by degrading gracefully; a transform
has somewhere better to go, because a finished layer can be resampled through a
matrix the primitives cannot express.

The patch is the call: an offscreen the contents draw into with the mapping
unchanged, and a resample of it on the way out. The arithmetic is in the main
repo — `Broiler.Layout.IR.AffineLayerMap`, with 20 tests — which is why this is
310 lines and not a rewrite of every primitive.

**The two halves must stay together, and neither does anything alone.** The map
is inert with nothing calling it; the call does not compile without the map. The
main-repo half is already in the pinned tree, so applying this patch is all that
is outstanding.

**Measured over the full 26 366-test WPT reftest suite: 18 776 → 18 771 passing,
+22 / −27**, average match 98.42% → 98.42%, and the run takes the same time
(22:04 → 21:5x). The score going *down* is the honest result and is worth reading
carefully:

- **25 of the 27 losses have a rotation or skew on both sides**, so both rendered
  blank and matched at 100%. That is the
  [fake pass](../docs/wpt-reftests.md#the-bug-is-as-likely-to-be-in-the-reference-and-that-inverts-the-scoreboard)
  this suite is built to produce, ending. The two that do not — `mix-blend-mode-rotated-clip`
  and `3dtransform-and-filter-no-perspective-001` — were checked by rendering
  them: the reference transforms too (through spellings a grep for `rotate`
  misses), so both sides were blank there as well.
- **13 of the 22 wins transform on only one side**, which could not have passed
  before at all.
- **The upside this suite structurally cannot show is in the golden one**, where
  the reference is Chromium's render rather than Broiler's: **192 of the 6 914
  golden-image failures declare a rotation or skew**, and a blank render can only
  lose against a reference that shows the rotated content.

### `0003-html-column-flex-image-stretch.patch`

A block-level image inside a flex or grid container is wrapped in an anonymous block
(`DomParser.CorrectImgBoxes`), and for a **column** container that wrapper becomes the
flex item — `FlexGridItemBlockification.IsRowFlexItem` exempts only *row* containers,
deliberately, because block flow cannot position a block-level replaced box on its own.
The cross-axis stretch therefore lands on the wrapper while the image inside keeps an
`auto` width, which for an inline replaced box with no intrinsic size means the 300×150
default object size. To the spec the image *is* the item, so it has to fill what was
stretched.

**The main-repo half carries the decision and the submodule half is one call**, which is
the shape `CLAUDE.md` recommends: the patch is 16 lines, and
`IsStretchedColumnFlexItem` — with the conditions, the reasoning and the counter-examples
— sits beside the `IsRowFlexItem` this fix-up already asks, so the two readings of "what
is the item" cannot drift apart. The predicate is asked *before* the reparent, while the
image is still the container's own child and still carries the width, margins and
alignment CSS Flexbox §9.4 step 11 turns on.

**Measured over `css/css-flexbox`, 644 reftests: 436 → 438 passing, +2 / −0**
(`aspect-ratio-intrinsic-size-007` and `flex-svg-no-intrinsic-column-001`).
`css/css-images` (271/439), `css/css-masking/clip-path` (157/227) and `quirks` (21/25) do
not move, and `Broiler.Wpt.Tests` holds at its 54 pre-existing failures.

**Two conditions in the predicate exist because the first attempt lost two tests**, and
they are worth keeping in mind for the general form of this gap. A percentage width sizes
the *content* box while a stretch sizes the *border* box, so `width: 100%` is an exact
stand-in only when the image has no inline-axis padding or border —
`flex-aspect-ratio-intrinsic-padding-001`, whose assertion names the content box outright,
overflowed by exactly its `padding: 20px`. And the container's cross size has to come from
outside rather than from its contents: an `inline-flex` column container shrink-wraps to
its items, so an item declared `100%` of it contributes nothing to the size it is a
percentage of and the container collapses (`inline-flex-column-image-load`). Both of those
passed before and pass now.

**Outside those conditions the stretch still belongs on the replaced element and is still
not applied.** That is the general form, and it wants the cross size pushed onto the box
during flex layout rather than a declaration rewritten before it.

### `0004-html-inline-replaced-box-model.patch`

A `display: inline` box lays out as one rectangle per line rather than as a single
border box, so `HtmlContainerInt.CollectLayoutGeometry` rebuilds its border box from the
union of those rectangles — and then set **all three** box-model levels to that same
rectangle, on the stated grounds that inline boxes contribute no box-model padding or
border to line geometry in this engine.

**That premise holds for a non-replaced inline and not for a replaced one.**
`CssLineBox.UpdateRectangle` adds an image's border and padding to its line rectangle
explicitly, and `MeasureImageSize` adds them in the block axis, so the union really *is*
the border box and the other two levels have to be deflated out of it. Measured:
`<img style="width:50px;height:30px;border:3px solid;padding:4px">` reported
`getBoundingClientRect` **64×44** — right — and `clientWidth`/`clientHeight` **64×44**
too, where the client box excludes the border and must be **58×38**. It now reports
58×38, and a `<div>` with the same declarations reports 58×38 as it always did, so the
two paths agree again. A non-replaced `<span>` is untouched.

The main-repo half is `BoxGeometry.ForInlineBox`, which owns the replaced/non-replaced
distinction and the arithmetic; the patch is the call.

**It moves no WPT test, and the entry it came from was wrong about why it would.**
`css-sizing/contain-intrinsic-size/contain-intrinsic-size-logical-003` was named as
carrying "the six `<img>` assertions still failing"; it holds at **42/96 before and
after**, and those six are identical in both — `client-width: expected 50, actual 0`
against `client-height: expected 0, actual 50` is a logical/physical axis transposition
under `contain-intrinsic-size`'s logical properties, not a border deflation. What this
fixes is the general API defect, which is every inline replaced element with a border or
padding and is not what that test measures.

`Broiler.Cli.Tests` 50 → 49 failures with no new ones, `Broiler.Wpt.Tests` holds at 54,
`Broiler.Layout.Tests` at 0.

### `0005-css-image-set-negative-resolution.patch`

CSS Images 4 §5.4 makes a `<resolution>` non-negative, so `image-set(url(a) -1x, …)` is a
parse error and CSS Syntax 3 §9 drops the **whole declaration**, letting the one cascaded
under it apply. `image-set-negative-resolution-rendering-2` puts a green `url()` behind it
and expects green.

**It has to be refused in the cascade, not at the renderer.** Only the winning value
reaches the renderer, so declining it there leaves the property at its *initial* value
rather than at the previous declaration — which is why the main-repo half already present
in `CssUtils` (leave the property alone when `CssImageSet.TryResolveLayers` reports a parse
error) could not close these two on its own.

**This one has no main-repo half, and the open entry was wrong to imply it could.** That
entry read *"`CssImageSet.TryResolveLayers` already reports the parse error separately from
a function that validly selects nothing, so the validator has something to call."*
`CssImageSet` lives in `Broiler.Layout`, which **references `Broiler.CSS`** and not the
other way round, so the validator cannot call it. The check is therefore self-contained in
the cascade — a scan that ignores anything inside `url()` or quotes, so a file genuinely
named `sprite-1x.png` does not invalidate the declaration that loads it.

A **zero** resolution is deliberately still accepted: it parses and simply selects nothing,
which is a different outcome from the declaration being dropped.

**Measured: `css/css-images/image-set` goes 26 → 28 of 31, +2 / −0**, and `css/css-images`
as a whole 271 → 273 of 439. `Broiler.Cli.Tests` is unchanged at 49 failures.

**A note for whoever measures next.** `Broiler.Cli.Tests.SpeculativePreloadScanTests` is
**flaky under the full run**: two identical full runs of the suite gave 56 and 49 failures,
differing by exactly its seven cases, and all ten pass in isolation. It cost a diagnosis
here; re-run before attributing it to a change.
