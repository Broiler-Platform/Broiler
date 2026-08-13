# Submodule patches waiting to be applied

**One patch is waiting on a maintainer.** See the index below.

`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`, `Broiler.JS` and `Broiler.Graphics`
are git submodules with their own remotes. A session whose GitHub scope is this
repository alone cannot push to them — the git proxy answers **403** — so a fix
that belongs in a submodule is committed there, exported with
`git format-patch`, and left here for a maintainer to apply. The submodule
working tree is then reverted to its pinned commit and **the gitlink is not
bumped**: CI clones a submodule by pointer, and a pointer to a commit that was
never pushed would break the build.

Applying one:

```sh
cd <Submodule>
git checkout -b <branch> && git am ../patches/NNNN-<slug>.patch
git push origin HEAD
cd .. && git add <Submodule>      # bump the pointer only once the push succeeds
```

## This directory is a backlog, not an archive

A patch is deleted from here the moment its fix is upstream and the submodule
pointer is bumped, because from then on it reaches CI through the pointer and a
file that can only ever be skipped is noise. `scripts/apply-pending-wpt-patches.sh`
holds the matching list — the subset whose fix can move rendered pixels, so a
WPT run exercises it rather than testing against the un-fixed pointer — and is
idempotent, so a patch already contained in the pinned pointer is skipped rather
than re-applied.

**Check the pointer, not this file, before concluding a fix is pending.** The
numbering is *recycled*: numbers are assigned from `0001` against whatever the
directory holds at the time, so a patch number in an older commit message, code
comment or document does **not** identify the same change as today's patch of
that number. Prose that names a patch by number alone is evidence about the past
only. To decide whether a submodule fix is live, look for its commit:

```sh
git -C <Submodule> log --oneline --grep '<the commit subject>'
git -C <Submodule> merge-base --is-ancestor <sha> HEAD && echo "live on CI"
```

## The index

| # | submodule | subject |
| --- | --- | --- |
| `0001` | `Broiler.HTML` | image: render an SVG image through Broiler.Layout's SvgRenderer |

### `0001` — one SVG renderer instead of two

Six lines at the seam, and it is the second half of a two-repo change; the first
half is already in the main repo and is where all the logic lives
(`Broiler.Layout.IR.SvgImageRaster`, plus percentage-length support in
`SvgRenderer`). Issue
[#1627](https://github.com/Broiler-Platform/Broiler/issues/1627) has the full
story.

**Why it is listed for the WPT run.** Without it, every SVG used as an image is
still drawn by the image backend's own regex renderer, which has no `<polygon>`
arm — so the document renders as a fully transparent bitmap and the image does
not appear. That is squarely a pixel-moving change, which is the bar for this
list.

**Measured before landing**, over 3 974 reftests in `css-masking`, `css-images`,
`css-transforms`, `css-backgrounds`, `compositing`, `filter-effects`, `css-ui`
and `svg`, with the main-repo half in place on both sides:

- **2 675 → 2 756 passing**, +95 and −14, and average match 98.562% → 98.599%.
- `css-images/object-fit-*-svg-*` goes **52/120 → 120/120**.
- Of the 14 losses, five were *passing by rendering nothing* — the test and its
  reference were both blank, so they matched at 100% — and now render real
  content that exposes two separate pre-existing bugs, both recorded in
  [the gaps document](../docs/wpt-rendering-gaps-open.md#svg-as-an-image-went-through-a-second-weaker-svg-renderer--fixed).
  The other nine are sub-1.5% differences that fall just under the 99% threshold.

**Do not apply this patch without the main-repo half.** On its own it regresses
~70 `css-backgrounds/background-size/vector` tests, whose SVGs are built on
percentage lengths that `SvgRenderer` did not resolve until the same change
taught it to. The two are one fix in two repositories.

**When it lands upstream:** bump the pointer, delete this patch and its entry in
`scripts/apply-pending-wpt-patches.sh`, and delete the now-unreachable private
renderers in `BSvgRasterizer` (`RenderRectangles`, `RenderCircles`,
`RenderEllipses`, `RenderLines`, `RenderPaths`, `RenderText` and the helpers only
they use — roughly 450 lines). They are deliberately left in place here to keep
the patch to 23 lines and so hand-applying it cannot conflict; removing them is a
trivial in-repo follow-up once the pointer moves.
