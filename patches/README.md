# Submodule patches waiting to be applied

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

## Index

### `0001-html-canvas-backdrop-lever.patch` — `Broiler.HTML`

One line in `Source/Broiler.HTML.Orchestration/IR/PaintWalker.CanvasBackground.cs`:
the colour a translucent propagated canvas background (CSS 2.1 §14.2) is
flattened against becomes `Broiler.Layout.Engine.CanvasBackdrop.Current ??
BColor.White` instead of `BColor.White`.

`CanvasBackdrop` itself is **already in this repository**
(`Broiler.Layout/Broiler.Layout/Engine/CanvasBackdrop.cs`), thread-static and
null by default, so the main repo builds and every render that does not set it is
byte-identical to today. The patch is only the call.

Wanted by the WPT reftest runner's `@page` paint (CSS Paged Media 3 §7 — see
[docs/wpt-reftests.md](../docs/wpt-reftests.md#page-paint-is-not-behind-the-lever-because-it-is-not-about-pagination)):
a page background is painted under the flow, and a `body` background propagated
over it has to composite with it rather than against an assumed white.
`css/css-page/page-box-002-print` is the pair that states it — `body {
background: #f008 }` over `@page { background: #00f }` must come out violet, not
pink — and it stays at 0.0 % until this is applied. Nothing else in the `@page`
paint work depends on it.

**No main-repo fallback, deliberately.** The flatten happens while the display
list is built, so the composited-away colour is not recoverable from the rendered
pixels afterwards; a runner-side workaround would have to re-implement canvas
background propagation to guess what was flattened. One reftest is not worth
that, and the patch is one line.

### `0002-html-empty-inset-clip.patch` — `Broiler.HTML`

Four lines in `Source/Broiler.HTML.Orchestration/IR/PaintWalker.Geometry.cs`: a
`clip-path: inset()` whose rectangle comes out empty is emitted as a clip instead
of being dropped. An empty rectangle is a clip that admits nothing —
`inset(100% 0 0 0)` says the element is not to be seen — and the raster backend
clips to it correctly once it arrives; dropping it painted the element in full.

Wanted by CSS 2.1 §11.1.2 `clip`, which is implemented **in this repository**
(`Broiler.Layout/Broiler.Layout/IR/ClipRect.cs`, projecting onto the `clip-path`
the paint walker already applies — see
[docs/wpt-reftests.md](../docs/wpt-reftests.md)). Most of what `clip` states *is*
an empty rectangle: `rect(96px, 96px, 96px, 96px)` on a 96×96 box runs from the
96px mark to the 96px mark on both axes. So the main-repo half lands the
non-empty cases and this unlocks the rest — measured together, **+46 reftests,
none lost**: `css/CSS2/visufx` 6 → 50 of 51, and two in `css-masking/clip` that
were the same bug reached through `clip-path` directly.

Applying it does not need `0001`; the two touch different files.

### `0003-dom-frame-void-element.patch` — `Broiler.DOM`

Two lines across `Broiler.Dom.Html/HtmlDocumentParser.cs` and
`Broiler.Dom.Html/HtmlSerializer.cs`: `frame` joins the void-element set each
keeps. HTML §"the in frameset insertion mode" inserts a `frame` element and
*immediately pops it* off the stack of open elements, so a frame never takes
children; the fragment-serialisation algorithm names it alongside the void
elements as taking no end tag. The tree builder did not know either, so
`<frame src=a><frame src=b>` nested the second frame inside the first,
`DomParser.LayoutFramesetChildren` was handed one cell instead of two, and
**every frame after the first painted nothing** — a two-frame `cols="50%,50%"`
frameset rendered its left half and left the right half blank.

Verified before the tree was reverted: a two-frame frameset (`cols` and `rows`
alike) goes from 50 % painted to both cells painting their own document, and a
single-frame frameset and a two-iframe page are unchanged.

**Not needed by the WPT test that motivated the frameset work.**
`resource-timing/initiator-type/frameset` has exactly one frame, and it passes
on CI today (99.7 %) from the main-repo half of that work —
`Broiler.Layout.Engine.DocumentRoot` plus the root-relative branch in
`FragmentTreeBuilder.TryLoadEmbeddedDocument`. This patch is the multi-frame
case, which no test in the current subset covers; nothing regresses while it
waits.
