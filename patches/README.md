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

The directory was emptied by the previous submodule bump — all six of the
`mediawiki.org` patches landed upstream — so the numbering restarts at `0001`
again with the one below.

## The index

| # | submodule | subject |
| --- | --- | --- |
| `0001` | `Broiler.CSS` | Resolve the absolute length units in ParseToPixels |
| `0002` | `Broiler.CSS` | Give a media query a paged formatting context |
| `0003` | `Broiler.HTML` | Carry a block image's page name onto the box that replaces it |
| `0004` | `Broiler.CSS` | Give `<legend>` the user-agent `display: block` it has in HTML |
| `0005` | `Broiler.HTML` | The same, in the default style sheet |
| `0006` | `Broiler.HTML` | Paint an element its transform mirrors |

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

### `0002` — a media query had no way to know it was being printed

**Apply `0001` first, or rather: apply both.** On its own this one does not make
`css-page/media-queries-001-print` pass, because that test writes its whole
assertion in inches and `ParseToPixels` cannot resolve them until `0001` lands.
They touch different files, so they apply cleanly in either order.

A formatting context has media-query answers of its own, and neither of the two
that matter for print was reachable from outside `Broiler.CSS`:

* `EvaluateMediaType` matched `screen` and `all` unconditionally, so
  `@media print` never applied to a document being printed; and
* Media Queries 4 evaluates `width`/`height` against the **page area**, which is
  not the surface a paged renderer happens to allocate.

`CssPagedMedia` carries both, thread-static and inert unless pinned — like the
layout engine's other render levers — so a continuous render evaluates exactly as
before.

The page area it carries is the **initial** one the formatter is handed, not the
one `@page` declares. That reads as a bug until the circularity shows up: a
`@page` rule may itself sit inside a media query, so resolving the query against
the declared page would need the query already resolved.
`media-queries-001-print` states it outright — it declares
`@page { size: 10in; margin: 2in }` and then asserts a query matching only
between 4in and 5in wide and 2in and 3in tall, which is WPT's initial 5in × 3in
page whether or not a default margin comes off it. The declared 10in page is
precisely what the query must not see.

`Suspend()` is the other half, and it is not an optimisation: a **nested**
browsing context is its own formatting context, so the page area of the document
embedding a frame is not that frame's viewport. Without it
`media-queries-002-print` and `-003-print` go red, each embedding a 100 × 100
frame whose own sheet asserts `@media (width: 100px) and (height: 100px)`.
`Broiler.Layout`'s `EmbeddedCanvas.Pin` suspends it around every embedded render,
which covers the call site inside `Broiler.HTML` that the main repo cannot reach.

**The main-repo half is already in and inert.** `EmbeddedCanvas` and
`WptTestRunner` carry the two call sites behind a `BROILER_CSS_PAGED_MEDIA`
compile constant that both `.csproj` files define only when
`Broiler.CSS/Broiler.CSS.Dom/CssPagedMedia.cs` exists — the same file-existence
probe `Broiler.Render.Stage.Benchmarks.csproj` uses. Against the pinned pointer
the repo builds and renders byte-identically to before (verified: `css/css-page`
and `css/css-break` are unchanged test-for-test). Once this patch lands and the
pointer is bumped, the probe finds the file, the constant is defined and the two
call sites compile in — no further main-repo change needed.

Measured on top of `0001`: `css/css-page` goes 142 → **143** of 224 reftests with
the average 88.37% → **88.83%**, `css/css-break` does not move, and a fail-list
diff shows exactly one test changing state — `media-queries-001-print`, 0.0% →
100% — with none lost.

### `0003` — a `display: block` image lost its page name

`CorrectImgBoxes` implements a block-level replaced element the way this engine
lays one out: it wraps the image in an **anonymous block** and demotes the image
itself to `display: inline`, so the image paints as an inline replaced word
inside a block wrapper. The geometry that comes out is correct — an
`<img style="display:block">` followed by text puts the text on the next line —
but the wrapper is now the block-level box the element generates, and one thing
was not travelling with it.

CSS Paged Media 3 §3.4 hangs a page name on a block-level box **and nothing
else**, which is exactly what lets an *inline* image's own `page` be ignored.
Leaving the name behind on the demoted inline therefore reads
`<img style="display:block; page:b">` as staying on its ancestor's page: the name
is dropped, and a following `div { page: b }` forces a break that should not be
there.

`css-page/page-name-img-003` and `-004` are that failure, each rendering two
pages where its reference renders one. **`-001` and `-002` are the control** —
there the image really is inline, its name really must be ignored, and they pass
both before and after. That pair is why the demoted `Display` could not simply be
read around downstream: it said `inline` for *both* spellings, so nothing after
`CorrectImgBoxes` could tell a block image from an inline one. All four now pass
at 100%, which is the coverage this patch carries — `Broiler.HTML.Orchestration`
has no unit-test project of its own.

**Only paged rendering sees it.** The page name is not read outside paged media,
so the default unpaginated render is unchanged — verified test-for-test across
`css/css-page`, `css/css-break`, `css/css-backgrounds` and `css/css-values`.
Under `BROILER_WPT_PAGED_PRINT=1`, `css/css-page` goes 132 → **134** of 224 with
the average 76.45% → **77.36%**, `css/css-break` unmoved, none lost. There is no
main-repo half and nothing in this repository references anything new, so the
build is unaffected while the patch waits.

### `0004` and `0005` — `<legend>` was an inline box

HTML's rendering section makes a `<legend>` a block box. Neither of the two
user-agent sources this engine reads said so: `Broiler.CSS`'s
`CssUserAgentDefaults.DisplayValues` table lists `fieldset` and every other
block-level element and not `legend`, and `Broiler.HTML`'s default style sheet
has the same omission in its `display: block` rule. Left at the CSS initial
value a legend is **inline**, so its `width`, `height` and `padding` do nothing
at all.

A four-way render pins it: a `<legend>`, the same legend with `display: block`,
a `<span>` and a `<div>`, each given `width: 100px; height: 19px; padding: 10px
7px 20px 3px`. The `<div>` and the explicit `display: block` legend occupy 49px;
the bare legend and the `<span>` occupy 19px — the legend was behaving as an
inline box exactly.

**Two patches for one rule**, because the two sources feed different paths into
layout and a document reaching layout through either one needs it.

`css-break/fieldset-001`, `-003` and `-004` are written on a sized legend. The
main-repo half of the change — the rendered legend's placement on the fieldset's
block-start border (`CssBox.Fieldset`) — is inert without these: it only acts on
a block-level legend, so against the pinned pointers the repository renders
exactly as it did.

**Measured** on top of the main-repo half, under `BROILER_WPT_PAGED_PRINT=1`:
`css/css-break` holds at 92 of 204 with the average 87.45% → **87.47%** —
`fieldset-004` 84.4% → **88.5%** and `fieldset-003` 91.3% → **92.1%**, against
`fieldset-001` 78.0% → 77.7%, which needs its column set to cut a box that has
content in it and does not get there on the legend alone. `css/css-sizing` holds
at 74 of 112 with both of its fieldset `aspect-ratio` tests at 100%, and
`css/css-page` (paged and default), `css/CSS2`, `css/css-backgrounds` and
`css/css-values` do not move.

### `0006` — a mirrored element painted nothing at all

The raster canvas maps a point per axis as `p * scale + translation`, so a
*negative* factor is expressible on it and `TrySaveTransform` accepts one:
`scaleX(-1)`, `scaleY(-1)`, `scale(-1)` and the half-turn `rotate(180deg)` —
whose sine terms round to zero — all fold into `_scaleX`/`_scaleY` rather than
falling back to the compat backend, which is an inert stub on a host with no OS
backend.

Expressible, but not drawn. `Translate(RectangleF)` mapped a rectangle by scaling
its **extent**, which a negative factor made negative, and every primitive walks
the rows and columns between `Left`/`Right` and `Top`/`Bottom`. A mirrored
element therefore spanned nothing: background, borders, children and text all
vanished. It is the same shape of failure as a stroke that misses the raster fast
path — not coarser output, *absent* output — and it is why
`css-break/transform-024-print` rendered a blank page where five coloured bands
belong.

The fix normalises the mapped rectangle and mirrors the primitives whose sampling
reads *across* it rather than merely inside it: a bitmap and a tile phase are read
from the far end, a linear gradient's endpoints are reflected in the rectangle, a
radial or conic centre is measured from the opposite edge and the conic sweep runs
the other way, and a corner radius travels to the corner the mirror moves it to.
Glyph outlines already go through the per-point mapping, so they mirror on their
own. Non-negative scale takes none of the new branches and its arithmetic is
untouched.

**Measured** across `css/css-transforms`, `css/compositing`, `css/css-backgrounds`
and `css/css-images` — 2020 reftests — the suite goes 1242 → **1243** passing.
The gains are `transform-background-003`, `-004` and `ttwf-reftest-rotate`; the
two losses were passing for the wrong reason and now show the gap they always
had, a mirror on each side having rendered nothing on both:
`transform3d-scale-007` (an unsupported `rotateX(180deg)` on the test side) and
`animation/transform-interpolation-matrix` (its reference builds no boxes).
That small a net move understates it — what the patch buys is that a mirrored
element renders *at all*, which no count of a suite this narrow shows.

The main-repo half is `src/Broiler.Cli.Tests/MirroredTransformPaintTests.cs`,
which feature-probes the pinned pointer and self-skips there. `scripts/apply-pending-wpt-patches.sh`
lists this patch, so a WPT run exercises it.
