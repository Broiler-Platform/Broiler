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

The directory was emptied by the previous submodule bump — all six of the
`mediawiki.org` patches landed upstream — so the numbering restarts at `0001`
again with the one below.

## The index

| # | submodule | subject |
| --- | --- | --- |
| `0001` | `Broiler.CSS` | Resolve the absolute length units in ParseToPixels |

### `0001` — `border: 72pt solid red` painted a thin black line

CSS Values 3 §5.2's absolute units — `pt`, `pc`, `in`, `cm`, `mm`, `Q` — are
fixed multiples of the reference pixel, so they resolve with nothing else to
consult: no font, no viewport, exactly like `px`. `CssLengthParser.ParseToPixels`
handled `px` and the whole font-relative and viewport-relative families and
simply left these six out, answering `NaN`.

Its callers read that `NaN` as *"this is not a length"*, and act on it. The
`border` shorthand is the loud one: `IsLengthOrPercentage` asks whether `72pt` is
a length, is told no, and the expansion therefore classifies
`border: 72pt solid red`'s first component as a **colour**. The width falls back
to `medium` and the declared colour is dropped, so the declaration paints a 3px
black line. The longhand spelling — `border-left-width: 72pt` — was right the
whole time, which is what makes the failure hard to see: it is not "units are
broken", it is "units are broken in one shorthand".

That matters far out of proportion to the six keywords, because the CSS2.1 test
suite states its geometry in physical units by convention. **`css/CSS2/positioning`
alone goes 364 → 394 of 520 reftests on this patch, with none lost.**

Two quieter callers were answering the same `NaN`: a media query such as
`(min-width: 8in)` and a container query with an absolute length both evaluated
as *invalid* rather than as the length they name.

The `in` spelling has to be tested after the viewport-unit scan, which claims
`vmin`.
