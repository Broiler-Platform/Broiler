# `mediawiki.org` — rendering the Vector 2022 skin

`https://www.mediawiki.org/` loads and paints, but it did not look like the same page in the
reference browser. Measured against a Chromium capture of the identical bytes at a 1024×768
viewport, **38.9 %** of pixels matched; the fixes below take that to **76.5 %**. The 80 % the work
was aimed at is not reached, and [what is left](#what-is-left) says why and what it would take.

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
fixed. The honest summary is the endpoints: 38.9 % → 76.5 %.

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

## What is left

At 76.5 % the remaining error is concentrated, and most of it is two things.

**The article's photograph, ~8 points.** It is 2px too high and the rest is resampling: even
perfectly aligned, Broiler's bilinear downscale of a 330px JPEG to 320px agrees with Chromium's on
about 80 % of pixels, and a busy photograph has no flat regions to forgive the other 20 %. The 2px
is worth chasing (see below); the resampling difference is a legitimate difference between two
correct implementations and is not worth chasing at all.

**A 2px vertical offset over the whole article body.** Shifting Broiler's render of everything below
`#bodyContent` down by 2px scores **81.2 %** — so this single offset is the difference between the
result here and the target. It was not found: the boxes above it (`#siteNotice`, `.vector-page-titlebar`,
`.vector-page-toolbar`) are individually mispositioned by 9–17px in ways that partly cancel, and
`getBoundingClientRect` in Broiler disagrees with where those boxes actually paint, so the usual
probe is unreliable exactly where the answer is. Two smaller findings sit inside that knot and are
worth writing down:

* **`#siteNotice` paints its content outside its own border box** — the box is at y=89..147 and the
  notice text at y=76..147. Something moves the box after its subtree has been positioned. It is
  not the margin-collapse propagation (that now moves the subtree with it) and it does not
  reproduce statically, which points at the relayout pass that runs after the skin's JavaScript
  mutates the DOM.
* **The first baseline sits about half a leading too high.** CSS2.1 §10.8 splits a strut's leading
  evenly above and below its content area; Broiler counts only the ascent from the top of the line
  box. At `line-height: 1.6` on a 16px font that is ~3px, and it grows with the line-height — a
  `line-height: 2.5` block draws its text flush with the top of the line box where the reference
  centres it in it. Adding the half-leading to the strut baseline alone does *not* fix it (tried:
  the page moved 0.07 points the wrong way), so the ascent Broiler uses — a flat 0.8 × font height
  rather than the face's own `hhea`/`OS/2` metrics — is part of the same answer.

Confirmed and not yet fixed, each smaller than the two above:

* SVG `fill="url(#id)"` paint servers (the sun logo is a gradient) and SVG `fill` inheritance (the
  wordmark) — the header's two logos do not paint.
* `mask-image` is unimplemented, so five header icons paint as solid squares.
* `[<a>dismiss</a>]` breaks between the bracket and the link. CSS Text §5.1 puts a break
  opportunity only where the text allows one, and an inline box boundary is not one, so the
  three-character run wraps to three lines and its float is 45px tall instead of 15px.
* Grid named areas, and `margin: 0 auto` with `max-width`.

Refuted by adversarial verification, and listed so nobody chases them again: `place-*` shorthands,
two-value `overflow`, flex auto widths, CSSOM `styleSheets`, `clamp()`, `srcset`, `line-height:
normal`, `calc()` in `font-size`, and `getComputedStyle().display`.

## Where the fixes live

The renderer, CSS engine and graphics core are git submodules, and this session's GitHub scope does
not include them, so six of the fixes are patch files under [`patches/`](../patches/README.md)
rather than pointer bumps. They are listed in `scripts/apply-pending-wpt-patches.sh`, so a WPT or
real-world run exercises them; a maintainer applies them and bumps the pointers. Everything else is
in `Broiler.Layout` and `src/`, with regression tests in
`Broiler.Layout/Broiler.Layout.Tests/VectorSkinLayoutTests.cs`.
