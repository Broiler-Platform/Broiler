# Submodule patches waiting to be applied

**Two patches are waiting on a maintainer.** See the index below.

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
