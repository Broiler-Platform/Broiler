# `mediawiki.org` — rendering the Vector 2022 skin

`https://www.mediawiki.org/` loads and paints, but it did not look like the same page in the
reference browser. Measured against a Chromium capture of the identical bytes at a 1024×768
viewport, **38.9 %** of pixels matched; the fixes below take that to **82.7 %**. The page is now
vertically aligned with the reference to the pixel — the best whole-page shift is zero — and
[what is left](#what-is-left) is what remains once that is true.

Everything here is a general engine defect. The Vector 2022 skin is a useful witness because it
stacks ordinary constructs — a flex header, `calc()` breakpoints, a floated `display: table`
thumbnail, a JavaScript bootstrap that rewrites the page — and the reference browser handles all of
them without comment, so every disagreement is Broiler's.

## How it was measured

The live page changes between captures (a rotating site notice, a Wikimedia photo of the month),
so a live-vs-live comparison mixes engine differences with content differences. The measurements
below are against an **offline mirror**: one Chromium visit recorded the document and every
subresource, and both engines were then pointed at a local replay of exactly those bytes. Broiler's
own render of the live page and of the mirror agree, so the mirror is not flattering the result —
it removes a source of noise.

Numbers come from the real-world suite's own comparator (`compare_image_files` in
`scripts/run-real-world-render-tests.py`, tolerance 5), which is what
[`docs/real-world-render-tests.md`](real-world-render-tests.md) describes. The site is now in that
corpus as `mediawiki`, so the figure is reproducible:

```sh
python scripts/run-real-world-render-tests.py --sites mediawiki
```

**The per-fix deltas are not additive, and are not quoted as such.** Several of the defects
compensated for each other — a header 16px too short sat above a site notice 17px too tall, so
fixing either one alone moved the whole page *further* from the reference before the other was
fixed. Three of the margin-collapse fixes below each scored *worse* than the state before them,
and only the third made the page align. The honest summary is the endpoints: 38.9 % → 82.7 %.

## What was wrong

### Style — the cascade never reached the page

* **`calc()` in a media feature.** `CssLengthParser.ParseToPixels` unwrapped a single-value
  `calc()` and then parsed one length token, so `calc(1120px - 1px)` was `NaN` and the query was
  malformed. Media Queries 4 §2.4.1 accepts a math function there, and Vector writes *every*
  breakpoint that way: 25 of the page's 76 `@media` blocks, including the whole narrow-viewport
  branch that applies at 1024px. `calc()` also had no product tier at all, so `calc(0.85 * 59.25rem)`
  was invalid outright. (`Broiler.CSS`, patch `0001`.)
* **`list-style` was never expanded.** The longhands worked; the shorthand was dropped, so every
  list on the page kept the UA marker instead of the one the skin asks for.
* **`:visited` matched every `<a href>`,** so all links drew in the visited colour
  (`Broiler.CSS`, patch `0002`).
* **A `display: none` child made its parent look like an inline container,** which changed how the
  parent laid out even though the child generates no box (`Broiler.HTML`, patch `0005`).

### Layout

* **Floated flex and grid items.** CSS Flexbox §3: `float` has no effect on a flex item. Vector's
  logo is a `display: flex` link whose two children — the sun icon and the wordmark — are both
  `float: left`; taken out of flow, the container sized as though it held nothing and the site
  header came out 16px short. The blockification pass also had to move ahead of the box fix-ups,
  because a replaced item is wrapped by `CorrectImgBoxes` before the pass could reach it
  (`Broiler.HTML`, patch `0005`, calling `Broiler.Layout.Engine.FlexGridItemBlockification`).
* **Line boxes ignored floats.** Text ran under a float on both sides instead of beside it
  (CSS2.1 §9.5, `LineFloatBands`).
* **A floated `display: table` box had its table width overwritten** by the block width algorithm,
  so a floated table, flex or grid box rendered as nothing.
* **A right float placed before its content was sized stayed where it was first put,** so it
  overhung its containing block's right edge once the content came out narrower.
* **`max-width: calc(100% - …)` against an indefinite basis collapsed to 0** instead of being
  ignored (CSS Sizing 3 §5.1).
* **Margin collapsing spent the same margin twice.** A first in-flow child whose top margin the
  parent absorbs did not record how much was spent, so a following sibling collapsing *through* an
  empty box applied it again. MediaWiki's article body opens with exactly that box — an empty
  `<p>` holding a `<style>` and two absolutely positioned spans — and the whole article was pushed
  down by it. The same record is what makes the collapse transitive, so a wrapper `<div>` between a
  margin and its grandparent no longer turns the margin into a gap.
* **A parent shifted by that collapse left its own content behind,** because only its origin moved.
* **A collapse-through could produce a *negative* margin.** The next sibling subtracts the margin
  already spent placing the empty box; subtracting more than the collapse would have added lifts
  the box above its own parent's content edge. The site notice was drawn 18px above the box that
  owns it.
* **A `display: none` child's margins were collected and handed on.** CSS2.1 §9.2.4: it generates
  no box, so it has none to give. Vector's empty `.vector-column-start` holds two `display: none`
  pinned containers with `margin-bottom: 32px`, and that 32px was separating the site notice from
  the article — the single largest remaining offset, and the last one fixed.
* **An inline box broken around a block lost its element.** The break replaces the inline box with
  copies of itself on either side of the block; when the block was its only content, neither copy
  is made and the element was left with no box at all. A `<body>` broken that way is the canvas
  background's only source, so a page whose background lives on the body rendered white
  (`Broiler.HTML`, patch `0005`).
* **An inline child's horizontal margins were left out of max-content width.** Vector wraps every
  thumbnail in a `display: table` figure whose image carries `margin: 3px`, so the figure measured
  6px under, and `max-width` then scaled the photo down to fit a box that should have fitted it
  exactly (CSS Sizing 3 §5).
* **An inline replaced element's *vertical* margins were dropped entirely** — the image was placed
  at the top of its line and the line closed at the image's own bottom (CSS2.1 §10.8.1).
* **A `vertical-align: middle` image drove the line's baseline.** Its baseline is its bottom edge,
  so in a short line it pushed the baseline a whole image-height down and then centred the image on
  the baseline it had just moved, leaving a ~116px band of white above the thumbnail.

### Text and images

* **Every family drew in one bundled face.** Generic families (`serif`, `sans-serif`, `monospace`
  and the rest) now resolve against the machine's installed fonts (`Broiler.Layout.Text.SystemFontIndex`).
* **Bold and italic did not exist.** The installed-face cache was keyed by family alone, so the
  first face loaded — the regular one — answered every later request. `font-weight: bold` and
  `font-style: italic` drew as regular text (`Broiler.HTML`, patch `0003`).
* **`font-size` in `rem`/`em` came out 4/3 too large,** because a pixel factor was read as points.
* **A scaled bitmap was point-sampled.** Exact at 1:1 and wrong at any other scale: the article's
  thumbnail is a 330px JPEG drawn at 320px, and 33 % of its pixels matched the reference before the
  change, 81 % after (`Broiler.HTML`, patch `0004`).
* **An outset `box-shadow` filled the box it was cast by** instead of being clipped out of its
  border box (CSS Backgrounds 3 §7.1). Every main-page card and the whole tab bar carry
  `box-shadow: 0 2px 2px rgba(0,0,0,0.2)` and painted as solid grey rectangles (`Broiler.HTML`,
  patch `0006`).

### Scripting — the skin's own JavaScript

MediaWiki's ResourceLoader bootstrap is one `<script src>` whose URL carries `&amp;`-escaped
ampersands. The capture path extracted that attribute with a regular expression and never decoded
the entities, so the URL 404'd and **nothing** in the skin's JavaScript ran. With it decoded, the
rest of what the skin needs turned up in order:

* `document.readyState` was `undefined`, so Vector's `main()` never ran: the appearance panel
  stayed in the page column (~130px of displacement) and the site notice never appeared.
* `history.pushState`/`replaceState`/`state`/`scrollRestoration`, `PerformanceObserver` and
  `requestIdleCallback` were missing, and each threw out of a module the skin loads.
* `MediaQueryList` had no `addEventListener`/`removeEventListener`/`onchange`, which is how the
  skin watches its own breakpoints.
* `sessionStorage` did not exist — only `localStorage` did. `window` **is** the global object here,
  so the bare `sessionStorage` the page writes was a `ReferenceError` rather than an undefined
  property, and that aborts the whole script. MediaWiki serves its modules as one `load.php`
  bundle, so a single identifier took ResourceLoader, the skin and every module queued behind them
  with it. Both areas are now registered, with the `length`/`key()` pair and the named-property
  access (`storage.foo`) that go with them, and a `Storage` interface global for the
  `typeof Storage !== 'undefined'` feature test.
* `HTMLImageElement` carried nothing but `width`/`height`, so **`img.src` was `undefined`** — not
  the empty string a missing content attribute reflects as. MultimediaViewer's bootstrap walks the
  page's thumbnails and hands each one's `src` to `mw.util.parseImageUrl`, which opens with
  `url.match( … )`: "Cannot get property match of undefined". One bundle again, so that throw took
  the rest of it. `src` and `currentSrc` are now resolved URLs, and `alt`, `srcset`, `sizes`,
  `useMap`, `isMap` and the fetch hints reflect their content attributes in both directions —
  writing one used to set a plain JS property on the wrapper that nothing else in the engine saw.

## What is left

17.3 % of pixels still differ, and the page is no longer misaligned — shifting Broiler's render up
or down only makes the match worse, so what is left is content rather than position.

**The photograph, 3.5 points.** It is aligned and correctly sized; the difference is resampling.
The thumbnail is a 330px JPEG drawn at 320px, and Broiler's bilinear downscale agrees with
Chromium's on about 80 % of pixels — a busy photograph has no flat regions to forgive the rest.
This is two correct implementations disagreeing and is not worth chasing.

**The header, about 3 points.** It is still nearly empty: the sun logo and the "MediaWiki" wordmark
are SVGs that need `fill="url(#id)"` paint servers and `fill` inheritance, neither implemented, and
five more header icons are drawn with `mask-image`, which is not implemented at all. The header is
mostly white in both engines, which is why an entirely missing logo costs so little.

**Text, spread through the rest.** Every line of text sits about 3px high inside its own block:
CSS2.1 §10.8 splits a strut's leading evenly above and below its content area, and Broiler counts
only the ascent from the top of the line box — and it uses a flat 0.8 × font height for that ascent
rather than the face's own `hhea`/`OS/2` metrics. Adding the half-leading to the strut baseline
alone does not fix it (tried: the page moved 0.07 points the wrong way), so the two have to be done
together. At `line-height: 1.6` on a 16px font the error is ~3px and it grows with the line-height:
a `line-height: 2.5` block draws its text flush with the top of the line box where the reference
centres it.

Smaller, confirmed, and not fixed:

* `[<a>dismiss</a>]` breaks between the bracket and the link. CSS Text §5.1 puts a break
  opportunity only where the text allows one, and an inline box boundary is not one, so a
  nine-character run wraps to three lines and its float is 45px tall instead of 15px.
* Grid named areas, and `margin: 0 auto` with `max-width`.
* `getComputedStyle().display` answers `inline` for every element. It does not affect the render —
  the box tree is right — but it made every DOM-side probe during this work untrustworthy, which
  cost more time than any single defect here.

Refuted by adversarial verification, and listed so nobody chases them again: `place-*` shorthands,
two-value `overflow`, flex auto widths, CSSOM `styleSheets`, `clamp()`, `srcset`, `line-height:
normal`, and `calc()` in `font-size`.

## Where the fixes live

The renderer, CSS engine and graphics core are git submodules, and this session's GitHub scope does
not include them, so six of the fixes are patch files under [`patches/`](../patches/README.md)
rather than pointer bumps. They are listed in `scripts/apply-pending-wpt-patches.sh`, so a WPT or
real-world run exercises them; a maintainer applies them and bumps the pointers. Everything else is
in `Broiler.Layout` and `src/`, with regression tests in
`Broiler.Layout/Broiler.Layout.Tests/VectorSkinLayoutTests.cs`.
