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

### `0004-html-embedded-canvas-color-scheme.patch` — `Broiler.HTML`

Three call sites plus one accessor, in
`Source/Broiler.HTML.Image/HtmlRender.cs`,
`Source/Broiler.HTML.Orchestration/IR/PaintWalker.CanvasBackground.cs`,
`Source/Broiler.HTML.Orchestration/HtmlContainerInt.cs` and
`Source/Broiler.HTML.Image/HtmlContainer.cs`:

- `CompositeEmbeddedDocuments` pins `Broiler.Layout.Engine.EmbeddedCanvas` to the
  embedding element's computed `color-scheme` around each frame's render;
- `RenderToImageCore` erases to transparent instead of the resolved canvas colour
  when the lever says that frame's canvas is transparent;
- `EmitCanvasBackground` skips its UA dark fill in the same case;
- `BlitOnto` becomes source-over rather than a copy, since a transparent canvas
  has to composite over the page instead of punching it out — an opaque source
  still takes the copy path, so an opaque frame is byte-identical;
- `GetRootColorScheme` exposes the embedded root's computed value, which the
  rasterising caller has no other way to reach.

`EmbeddedCanvas` itself is **already in this repository**
(`Broiler.Layout/Broiler.Layout/Engine/EmbeddedCanvas.cs`), thread-static and
unpinned by default, so the main repo builds and every render that pins nothing
is byte-identical to today. So is the `color-scheme` inheritance half
(`CssBoxProperties.InheritStyle`) and the runner's own frame compositor
(`WptDocumentRenderer`), which already pins the lever and composites source-over.
The patch is the renderer's side of the same rule.

CSS Color Adjust 1 §2.4: a nested browsing context's canvas is **transparent** —
the embedder shows through it — unless the used colour scheme of the *embedding
element* differs from the embedded root's, in which case the UA paints an opaque
backdrop of the embedded root's scheme. Only the second half was modelled: an
embedded document's canvas was always resolved opaque and the blit copied it
pixel-for-pixel, so a frame could never be transparent and the embedding
element's `color-scheme` was never consulted at all.

Wanted by two WPT tests that turn on exactly that distinction, both in
`css/css-color-adjust/rendering/dark-color-scheme`:
`color-scheme-iframe-background` puts a dark frame in a dark-scheme `<iframe>` on
a light page and asks for the light page to show through (69.0 % without this),
and `color-scheme-iframe-background-mismatch-opaque-cross-origin-003.sub` puts a
light frame in a light-scheme `<iframe>` on a dark page and asks the same
(94.7 % without this — a 200×200 white box that should not exist). With the patch
applied they measure 98.9 % and 99.8 %, the directory goes 22 → 24 of 29 with
nothing lost, and `html/semantics/embedded-content/the-iframe-element` is
unchanged across all 161 tests. `color-scheme-iframe-background`'s residual 1.1 %
is a *separate*, pre-existing gap — Broiler paints a black default `<iframe>`
border where Chromium paints a grey inset one — and it is 100.00 % identical to
its own `rel=match` reference either way.

**No main-repo fallback, deliberately.** The decision has to be made while the
frame's own canvas is erased and while its paint walker runs, both inside the
renderer; the only main-repo lever that reaches that point is
`RenderToImageWithStyleSet`'s `backgroundColor`, which cannot express
"transparent" (`BColor.Transparent` and `default(BColor)` are the same value, and
the renderer reads `default` as "resolve it yourself"). Re-routing the runner's
frame rendering around `HtmlRender` to work about it would duplicate canvas
resolution in the runner and still leave the UA dark fill unfixable, which is the
kind of contortion the repository rule warns against. Until this is applied the
two tests keep their current scores, and
`src/Broiler.Cli.Tests/EmbeddedCanvasColorSchemeTests.cs` probes for the fix and
disarms its four render assertions — they become real guards the moment the
pointer is bumped.

### `0005-html-border-bevel.patch` — `Broiler.HTML`

Two call-shaped changes, in
`Source/Broiler.HTML.Orchestration/IR/PaintWalker.Decorations.cs` and
`Source/Broiler.HTML.Core/CssDefaults.cs`:

- the border display item takes each side's colour from
  `Broiler.Layout.Engine.BorderBevel.SideColor` instead of using the border
  colour flat on all four sides;
- the UA stylesheet states the *base* of the bevel rather than its result —
  `iframe { border: 2px inset #EEEEEE }`, and `hr`'s four hard-coded per-side
  colours collapse to one `border-color: #EEEEEE`.

`BorderBevel` itself is **already in this repository**
(`Broiler.Layout/Broiler.Layout/Engine/BorderBevel.cs`), a pure function with 30
tests pinning its numbers, so the main repo builds and — because nothing calls it
until this patch lands — renders byte-identically to today.

CSS 2.1 §8.5.3 paints `inset` and `outset` as a bevel: two sides in a darkened
shade of the border colour, two in the colour itself. The IR paint path used the
colour flat on every side, so the border the HTML Standard puts on every
`<iframe>` and `<hr>` — `border: 2px inset`, which browsers paint `#9A9A9A` over
`#EEEEEE` — came out solid black.

The spec leaves the shades to the UA, so the rule was **measured off Chromium**
rather than guessed. The darkened side scales all three channels by the factor
that takes the largest one down by 0.33 of full intensity, which is what keeps
the hue: `rgb(200,100,50)` → `rgb(116,58,29)`, all ×0.58, where a per-channel
subtraction would give `rgb(116,16,0)` and turn brown into red. The lit side is
the colour itself, except black, whose lit side is `#545454` so a black bevel is
still a bevel.

**Why the UA stylesheet carries the colour.** CSS makes the initial
`border-color` `currentColor`, which bevels black-on-black; browsers substitute a
light grey at paint time so the bevel is visible. Broiler states that grey in the
UA stylesheet instead — which is what `hr` already did, with the *result* of the
bevel hard-coded per side. The rendering matches Chromium; the computed
`border-color` differs from it (`#EEEEEE` rather than `currentColor`), and an
author element with a bevelled border and no colour of its own still bevels from
black rather than from the grey. Both are noted in `BorderBevel`'s remarks.

**Why the call is here and not in `ComputedStyleBuilder`.** Putting it in the main
repo would have shaded borders while the pinned `CssDefaults` still hard-coded
`hr`'s bevel per side — darkening `#9A9A9A` a second time and regressing every
`<hr>` on CI. The two halves have to arrive together, so both are in this patch.

`groove` and `ridge` are untouched: they split each side lengthwise into two
shades (a 16px grey `groove` paints its outer half `#2C2C2C` and its inner half
`#808080`, where `inset` paints the whole width `#2C2C2C`), which needs two
rectangles per side rather than one colour per side.

Verified across 665 tests of `html/rendering` and
`html/semantics/embedded-content/the-iframe-element`: **89 changed, every one an
improvement, none worse**, and one more passing. Applied together with
`0004`, `css/css-color-adjust/rendering/dark-color-scheme` reaches 25 of 29 and
`color-scheme-iframe-background` reaches 99.4 % from 69.0 %.
