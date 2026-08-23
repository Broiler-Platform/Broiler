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
