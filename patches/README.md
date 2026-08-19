# Submodule patches waiting to be applied

**Six patches are waiting on a maintainer.** See the index below.

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

The directory emptied again here, which is why the numbering restarts at `0001`
once more. Both patches it held are upstream and the pinned pointer contains
them, so each reached CI through the pointer and neither file was doing anything
but inviting a re-apply:

* "Collect a JavaScript stack once, render it as often as asked" — `Broiler.JS`
  `6fb71d2f`.
* "Say who is asking" — `Broiler.HTML` `dc197ed`.

Both were checked the way this file says to check, and both answered "live on
CI":

```sh
git -C Broiler.JS   merge-base --is-ancestor 6fb71d2f HEAD
git -C Broiler.HTML merge-base --is-ancestor dc197ed  HEAD
```

## The index

The six below are one item: making `https://www.mediawiki.org/` render as the
reference browser renders it. Each is a general engine fix that the Vector 2022
skin happened to expose; none of them is specific to that site. Every one is
listed in `scripts/apply-pending-wpt-patches.sh`, because each decides what
reaches the canvas rather than how fast it gets there.

| # | submodule | subject |
| --- | --- | --- |
| `0001` | `Broiler.CSS` | Evaluate a math function wherever a length is expected |
| `0002` | `Broiler.CSS` | Match :link on an actual link, and :visited on nothing |
| `0003` | `Broiler.HTML` | Draw the face the style asked for, not the first one loaded |
| `0004` | `Broiler.HTML` | Filter a bitmap when it is drawn at a size other than its own |
| `0005` | `Broiler.HTML` | Make a flex or grid container's children items before the box fix-ups run |
| `0006` | `Broiler.HTML` | Clip an outset box-shadow out of its own border box |

### `0001` — a breakpoint written as arithmetic

`calc()` had no product tier, so `calc(0.85 * 59.25rem)` was invalid outright.
Worse, `CssLengthParser.ParseToPixels` — the entry point a *media feature value*
goes through — unwrapped a single-value `calc()` and then parsed one length
token, so `calc(1120px - 1px)` came out `NaN` and the whole media query was
malformed. Media Queries 4 §2.4.1 accepts a math function there, and Vector 2022
writes every breakpoint that way: 25 of the 76 `@media` blocks on the page are
`(max-width: calc(1120px - 1px))` and relatives, including the entire
narrow-viewport branch that applies at a 1024px viewport. All of them were being
dropped, which is why the page rendered as though it were 1200px wide.

### `0002` — every link the visited colour

`:link` and `:visited` shared one predicate, so `:visited` matched every
`<a href>`. An engine with no history has visited nothing; `:visited` now matches
nothing, which is both the correct answer and the privacy-preserving one.

### `0003` — bold that is not bold

The installed-face cache was keyed by family alone, so the first face loaded for
a family answered every later request for it. In practice that was the regular
face, and `font-weight: bold` and `font-style: italic` drew as regular text on
any page that does not ship its own `@font-face`.

### `0004` — a photograph point-sampled

`BCanvas.DrawBitmap` point-sampled the source for every destination pixel, which
is exact at 1:1 and wrong at any other scale. The thumbnail beside the article's
lead paragraph is a 330px JPEG drawn at 320px: 33% of its pixels matched the
reference within tolerance before the patch, 81% after.

### `0005` — a flex item that floats

CSS Flexbox §3 says `float` has no effect on a flex item, and CSS Display 3 §2.7
blockifies one. The pass that does both ran *after* the box fix-ups, too late for
a replaced item — `CorrectImgBoxes` had already wrapped a block image in an
anonymous block, so the float was cleared on the wrapper while the image inside
kept floating. Vector's logo is exactly that shape (a `display: flex` link with a
floated icon and a floated wordmark) and laid out as an empty box.

### `0006` — a card that is all shadow

CSS Backgrounds 3 §7.1: an outer shadow is not painted inside the border box of
the element casting it. The fill covered the whole shadow rectangle, so every
main-page card and the tab bar — all of which carry
`box-shadow: 0 2px 2px rgba(0,0,0,0.2)` — painted as solid grey rectangles.
