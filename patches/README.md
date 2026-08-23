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
