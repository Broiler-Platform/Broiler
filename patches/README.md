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
| `0001` | `Broiler.HTML` | paint: place replaced content by object-fit and object-position |

The number was last used for "image: render an SVG image through Broiler.Layout's
SvgRenderer", which landed upstream as `Broiler.HTML` `c77f0f0` and whose pointer
is bumped. It is a different change; see the warning above.

### `0001` — read `object-fit` and `object-position` at the paint site

The second half of a two-repo change, and the small half: all of the logic is in
the main repo (`Broiler.Layout.IR.ObjectFitPlacement` for the placement,
`IR.CssPositionValue` for the `<position>` grammar), and this is the call site
plus the two lines that report an image's aspect ratio and whether its reported
size is really intrinsic. Recorded in
[the gaps document](../docs/wpt-rendering-gaps-fixed.md#object-fit-and-object-position-were-not-read-at-all).

**Why it is listed for the WPT run.** Without it `EmitReplacedImage` draws every
replaced element into its content box — `fill` behaviour whatever the author
wrote — and `background-position` keeps reading a `<position>` positionally, which
drops the three- and four-component edge-offset forms (`top 25% left 25%`)
silently. Both are plainly pixel-moving.

**Measured** over the full reftest suite with the main-repo half in place on both
sides: `css/css-images` **234 → 262 of 460**, with the 42 `object-fit-*i` tests
going 21 → 42 and no losses anywhere in that directory. The whole-suite numbers
are in the gaps document.

**Do not apply this patch without the main-repo half.** It calls types that only
exist there, so it does not compile on its own. The main-repo half is inert
without it — the new types are simply unreferenced — which is why the pointer can
stay where it is.

**When it lands upstream:** bump the pointer, and delete this patch and its entry
in `scripts/apply-pending-wpt-patches.sh`.
