# WPT rendering gaps — won't fix

> Part of the [WPT rendering gaps](wpt-rendering-gaps.md) set:
> **won't fix** · [fixed](wpt-rendering-gaps-fixed.md) · [not fixed](wpt-rendering-gaps-open.md).
> Every status here was re-measured on **2026-08-13**; see
> [How this was verified](wpt-rendering-gaps.md#how-the-2026-08-13-split-was-verified).

Every test below is reported as a **failure** by the golden-image suite and is
nonetheless rendering **correctly**. Closing one would mean rendering *less* than
Broiler already does.

The cause is structural, not incidental. The golden-image suite scores Broiler
against a **Chromium screenshot**. Where Broiler implements something Chromium
does not, the test drops toward 0% and stays there permanently — *shipping a
feature moves the score down*. The `image-animation` pair below is the worked
example: both were reported fixed at 100% while frame selection matched
Chromium's unpaused render, then went back to 0.0% when `image-animation: paused`
was implemented for real.

The check that settles it needs no other engine: the **reftest suite** renders
both the test *and the `rel=match` reference the test itself declares* with
Broiler and compares them. That is WPT's own statement of what the test should
look like.

```sh
./scripts/run-wpt-reftests.sh --wpt-dir <absolute-checkout> --subset <test path>
```

Since [#1618](wpt-rendering-gaps-fixed.md#the-run-reported-correct-renders-as-its-worst-failures)
the runner does this itself on every pixel-mismatch failure (`--verify-reference`,
on in CI) and lists what it clears under *Not ranked — reference disagreements*
rather than in the severity list.

## The settled set

`rel=match` is Broiler against the reference the test itself declares, measured
2026-08-13. CI is the golden-image score from the
[#1624 run](https://github.com/Broiler-Platform/Broiler/issues/1624).

| Test | CI | `rel=match` | Why the reference is what it is |
| --- | --- | --- | --- |
| `css-image-animation/image-animation-body-background-root-propagation-paused` | 0.0% | **passes (100%)** | Chromium does not implement `image-animation` |
| `css-image-animation/image-animation-root-background-paused` | 0.0% | **passes (100%)** | same |
| `mediaqueries/at-custom-media-basic` | 0.0% | **passes (100%)** | Chromium does not implement `@custom-media` |
| `fullscreen/rendering/backdrop-iframe` | 0.0% | **passes (99.1%)** | the reference generator has no WebDriver, so Chromium never entered fullscreen |
| `fullscreen/rendering/backdrop-inherit` | 0.0% | **passes (100%)** | same |
| `fullscreen/rendering/backdrop-object` | 1.1% | **passes (100%)** | same |
| `css-color-adjust/…/color-scheme-iframe-background-mismatch-dynamic` | 0.0% | **passes (100%)** | Chromium fails this reftest against its own reference |
| `css-color-adjust/…/mismatch-dynamic-cross-origin.sub` | 0.0% | **passes (100%)** | same, the cross-origin twin |
| `css-view-transitions/auto-name` | 1.3% | **passes (100%)** | Chromium drops `view-transition-name: auto` at parse time |
| `css-view-transitions/auto-name-from-id` | 1.3% | **passes (100%)** | same |
| `css-view-transitions/auto-name-from-id-shadow` | 1.3% | **passes (100%)** | same |
| `filter-effects/svg-filter-primitive-units-user-space` | 8.0% | **passes (100%)** | Chromium fails this reftest against its own reference |
| `filter-effects/svg-filter-filter-units-user-space` | 8.0% | 95.3% | same — but see [the threshold note](#two-fall-through-the-99-gate) |
| `css-page/page-margin-002-print` | 0.0% | 89.2% | a `-print` test screenshotted on screen; see [below](#page-margin-002-print-is-a-screenshot-artifact) |

**Eleven of the fourteen appear in the #1624 run's own *Not ranked* list**, so they
are already off the severity ranking. The other three are absent for two different
reasons, both worth knowing:

- `svg-filter-filter-units-user-space` and `page-margin-002-print` are **not
  cleared** — they miss the 99% gate against their own reference. See
  [below](#two-fall-through-the-99-gate).
- `auto-name-from-id-shadow` **is** cleared (it reproduces its reference at 100%) but
  does not appear in the reported list, because that list is a **bounded per-shard
  sample** — each shard contributes its five lowest cleared mismatches alongside its
  five lowest rankable ones (`lowestMatchTests` in `merge-wpt-shards.py`). The
  published count is therefore a floor on how many reference disagreements a run
  found, not a census.

## Why each reference is what it is

### `image-animation: paused` — the worked example of the cost

The four `css/css-image-animation/*-paused` tests set an animated GIF
(`images/anim-gr.gif`, two frames, green then red) as the root or body background
and ask for `image-animation: paused`. Chromium does not implement the property,
so its reference is whatever frame its own timeline had reached at screenshot
time — 300 ms via `takeScreenshotDelayed`, which the short-delay clamp puts on the
red frame.

Broiler now implements the property
(`Broiler.Layout/Engine/CssBox.ImageAnimation.cs`: a `paused`/`stopped` box pins
its own image loads to time zero, and a `<body>` background that propagates to
the canvas takes the **root's** value), so it holds the green first frame —
exactly what each `…-ref.html` asks for.

**Only two of the four are here, and the asymmetry is modelled rather than
accidental:**

- `image-animation-background-paused` paints its two 20×10 boxes green against
  Chromium's red and still scores **99.9%**, because the disagreement is 0.1% of
  the canvas. It passes the threshold by being small, not by being right.
- `image-animation-body-background-no-propagation-paused` asks for the *unpaused*
  render (`red.png`) and gets it, because propagation hands the canvas the
  **root's** `image-animation`, not body's.

Read this together with
[the frame-selection work](wpt-rendering-gaps-fixed.md#animated-images-always-painted-their-first-frame):
these two were at 100% before `image-animation` landed. The engine got better and
the score got worse. That is the trap, not a regression.

### `view-transition-name: auto` — the reference is the unfeatured render

`auto-name`, `auto-name-from-id` and `auto-name-from-id-shadow` need
`view-transition-name: auto`, and Chromium **drops that declaration at parse
time** — it computes to `none`. Verified directly rather than inferred from
pixels: the rule survives in the CSSOM with `view-transition-name` removed.
Chromium therefore captures nothing, the transition finishes immediately, and its
reference is the plain post-callback page — 97.5% white plus two squares.

Broiler implements `auto`, captures both items correctly, and paints the author's
`rebeccapurple` backdrop over them. Matching that reference would mean deleting
working support.

### `@custom-media` — Chromium does not implement it

`at-custom-media-basic` is `@custom-media --foo (width > 0px);` plus
`@media (--foo) { :root { background: green } }`. Broiler implements Media Queries
5 §3 (`Broiler.CSS` `56eea09`, "media queries: range syntax and @custom-media",
upstream and pinned) and paints the green `/css/reference/green.html` asks for.
Chromium's `@media (--foo)` block never matches, so its screenshot is white.

### fullscreen `::backdrop` — the reference never entered fullscreen

`backdrop-iframe`, `backdrop-inherit` and `backdrop-object` all call
`requestFullscreen()` through `test_driver.bless`, which needs WebDriver. The
plain Playwright reference generator provides none, so Chromium never enters
fullscreen, no `::backdrop` is generated, and the screenshot is the un-activated
page. Broiler runs the blessed callback (the runner's `test_driver` shim, covered
by `FullscreenRenderTests`), promotes the element into the top layer and paints
its `::backdrop` green.

`backdrop-inherit` is the strongest evidence that this is real rather than a
backdrop painted indiscriminately: it sets `--bg: red` on `body` and `--bg: green`
on the `div`, and asserts `div::backdrop` inherits from the *fullscreen element*.
Broiler renders green. A backdrop painted from the wrong parent would be red.

> `backdrop-object` is new to this list. The
> [#1618 write-up](https://github.com/Broiler-Platform/Broiler/issues/1618) named
> it as one of two "tests worth a maintainer's time" once the six then-known
> disagreements were split out. It has since been re-measured: it passes its own
> reference at 100%, and CI now flags it as a disagreement too. It belongs here,
> not on the severity list.

### `color-scheme` mismatch — Chromium fails its own reftest

`color-scheme-iframe-background-mismatch-dynamic` asserts that a same-origin frame
with `color-scheme: light` gets an *opaque* background when its parent switches to
dark. Ours renders the frame light; Chromium renders it `#121212`.

Settled by rendering the test's own `<link rel=match>` target,
`support/light-frame-scrolling.html`, in the same Chromium: it comes out
**white**, while Chromium's render of the *test* is dark. So Chromium fails this
reftest against its own reference, and its screenshot is evidence that it does not
implement the rule. Broiler matches the reference.

`…-mismatch-dynamic-cross-origin.sub` is the cross-origin twin and fails for
exactly the same reason. It is worth keeping the shape of how it got here: it used
to **pass**, and only because *both* engines rendered an empty frame — the runner
performed no `.sub` substitution, so the frame URL was uninterpretable on both
sides. Once
[substitution landed](wpt-rendering-gaps-fixed.md#the-runner-never-performed-wpts-sub-substitution)
the frame loaded, the real disagreement was exposed, and the test started failing
truthfully. A green tick that depended on a frame never loading was worth less
than that.

### SVG filter units — Chromium floods the viewport

`svg-filter-filter-units-user-space` and `svg-filter-primitive-units-user-space`
have references that are **fully green 1024×768 canvases**. Rendering each test
*and* its own `-ref.html` under Chromium settles what that means: the `-ref.html`
is the six-container layout the test describes (green 75×75 in the 150px `<svg>`,
100×100 in the 200px one, 50×50 for the filtered `<div>`s), while the test itself
comes out uniformly green. Chromium fails both reftests against their own
references.

Isolated rather than assumed: a `<div>` carrying `filter: url(#f)` for a filter
with `filterUnits="userSpaceOnUse"` and percentage `width`/`height` floods the
**entire viewport** green under Chromium, while the same filter on an SVG `<rect>`
stays local. It is the CSS `filter: url()` path on an HTML element whose filter
region resolves unbounded.

Broiler is already closer to the spec render: the six-container layout with the
flood regions off. That is a real gap — the green boxes are the default
`objectBoundingBox` region rather than the resolved `filterUnits`/`primitiveUnits`
subregion — but a *smaller* one than a whole-canvas flood. Passing the comparison
would mean reproducing Chromium's flood, i.e. rendering strictly worse.

**The underlying gap is still worth naming, for whenever the reference is fixed
upstream.** Both regions are computable from what the tests contain: the filter
region (`filterUnits`, defaulting to `objectBoundingBox` at −10%/+120%)
intersected with the primitive subregion (`primitiveUnits`, defaulting to
`userSpaceOnUse`, with `x`/`y`/`width`/`height` defaulting to the filter region and
percentages resolving against the SVG viewport). Working the six containers
through that by hand reproduces the `-ref.html` exactly in both tests.
`SvgRenderer` currently hardcodes the default `objectBoundingBox` region and
ignores the primitive subregion entirely.

### `page-margin-002-print` is a screenshot artifact

Chromium's own **viewport** capture of a `vertical-rl` root is blank while its
**full-page** capture (3072×768) paints all three blocks. The two engines do not
disagree about layout: asked directly under Playwright, Chromium puts the yellow
`.fullpager` at exactly `(0, 0, 1024, 768)` — filling the viewport — with cyan at
`x: -1024`, pink at `x: -2048`, `scrollLeft: 0` and `scrollWidth: 3072`. That is
what Broiler renders. Matching the blank reference would mean drawing nothing.

It is also the one entry here that disagrees with its **own** reference (89.2%),
and that is not an engine bug either — it is a `-print` test being scored on
screen. `@page { margin: 10px 20px 30px 40px }` has no effect outside paged media,
so the test paints `100vw × 100vh` of yellow across the whole canvas while its
reference subtracts the margins explicitly (`calc(100vw - 60px)`,
`margin-right: 20px`). The two are *designed* to agree only once the page box
exists. 89.2% is the size of those margins, nothing more.

Rendering both sides as paged media (`BROILER_WPT_PAGED_PRINT=1`) does surface a
real gap, recorded under
[paged media](wpt-rendering-gaps-open.md#paged-media-is-partial): the test
paginates to 4 pages and the reference to 7 where both should be 3, and on both
sides only the first block paints. **It cannot change what CI reports for this
test in any case** — CI scores it unpaginated against Chromium's blank
`vertical-rl` viewport capture.

## Two fall through the 99% gate

`--verify-reference` only sets `suspectReference` when Broiler *reproduces* the
declared reference, and "reproduces" means clearing the same 99% pass threshold.
Two entries score well against their own reference and still get ranked as though
nothing were known about them:

| Test | CI | `rel=match` |
| --- | --- | --- |
| `filter-effects/svg-filter-filter-units-user-space` | 8.0% | 95.3% |
| `css-page/page-margin-002-print` | 0.0% | 89.2% |

A gap that large in that direction — 8.0% against a golden, 95.3% against the
test's own reference — is the signature of a reference disagreement. Recording the
reference score alongside the golden one, rather than only using it as a pass/fail
gate, would separate "wrong everywhere" from "wrong only against Chromium" without
needing a second threshold to be tuned. Left for a run that owns the report.

The same threshold is why the two `css-grid/grid-lanes` entries
([#1624 problems 2 and 3](wpt-rendering-gaps-open.md#grid-lanes-is-an-unshipped-draft-feature))
sit in *not fixed* rather than here at 94.0% and 94.8%.

## The other 28 flags, triaged 2026-08-13 — only three held

The [#1624 run](https://github.com/Broiler-Platform/Broiler/issues/1624) reported
**40** reference disagreements. Twelve were already accounted for: the eleven settled
entries above, plus `css-grid/subgrid/orthogonal-writing-mode-006`, which is flagged
and [is not one](wpt-rendering-gaps-open.md#the-flag-can-be-a-false-negative). The
remaining 28 had never been checked by hand. They have been now, and **the flag was
wrong on 25 of them.**

### How they were checked

The flag says only *"Broiler reproduces the test's own reference"*. That is
compatible with two opposite situations, and the discriminator is cheap: render the
test **and** its reference under Chromium as well.

- **Chromium fails its own reftest** → its golden is not what the test asks for →
  genuine reference disagreement.
- **Chromium passes its own reftest** while Broiler also reproduces the reference,
  and the two engines disagree → both of Broiler's renders are wrong in the same way
  → a real gap the flag is hiding.

All 28 tests and their 21 references were rendered under the pinned Chromium via
`generate-wpt-references.js`, compared with an 8/255 per-channel tolerance so
antialiasing is not mistaken for a structural difference. Broiler's own render of
each test was then compared against the fresh Chromium render: **it reproduced CI's
reported percentage on 27 of 28**, which is what confirms the committed goldens are
current and the gaps real.

### The three that held

| Test | CI | Broiler vs own ref | Chromium vs own ref |
| --- | --- | --- | --- |
| `css-grid/alignment/grid-item-mixed-baseline-001` | 53.0% | **100%** | 97.8% |
| `css-overflow/scrollbar-gutter-003` | 79.0% | **100%** | 95.8% |
| `css-grid/grid-lanes/baseline/column-grid-lanes-item-baseline-005` | 65.4% | **99.5%** | 68.4% |

Broiler reproduces what each test asks for and Chromium does not. The third is a
`grid-lanes` test — the [unshipped draft feature](wpt-rendering-gaps-open.md#grid-lanes-is-an-unshipped-draft-feature)
Chromium drops to `display: block`, which is the same shape that produces a permanent
low score against a golden.

### The 25 that did not

**Seventeen render a blank white canvas** — and so do their references, which is the
only reason they matched. Chromium paints substantial content in both. Every one is a
real gap, and they are now carried in *not fixed*:

- **Eleven share one cause:** [SVG-as-an-image goes through a second, weaker SVG
  renderer](wpt-rendering-gaps-open.md#svg-as-an-image-went-through-a-second-weaker-svg-renderer--fixed)
  with no `<polygon>` arm, and a `<polygon>` is the entire content of the file all
  eleven load. That was the suspicion about the nine entries sitting at exactly
  49.1%, and it was right — it is one cause, and it covers eleven.
- **Six more** are individually distinct: [AVIF decode, the CSS Paint API, a canvas
  2D context, `<foreignObject>`, a percentage-sized inline SVG under `perspective`,
  and `backdrop-filter` with a mask](wpt-rendering-gaps-open.md#other-image-formats-and-inline-svg-edge-cases).

**Six render content** and are still wrong against a self-consistent Chromium; they
are [listed in *not fixed*](wpt-rendering-gaps-open.md#six-that-render-content-and-are-still-wrong).
**Two are cases where both engines fail the test**, one of which is worth reporting
upstream rather than fixing — see
[there](wpt-rendering-gaps-open.md#two-where-both-engines-fail-the-test).

### What this says about the flag

25 wrong out of 28 is not a tuning problem, it is a missing check:
**`--verify-reference` never asks whether anything was drawn.** Blank-on-blank scores
100% and clears. The defect and the two cheap fixes for it are recorded under
[not fixed](wpt-rendering-gaps-open.md#--verify-reference-clears-a-test-that-renders-nothing).

Until that lands, treat the *Not ranked* heading in a run as a **triage queue, not a
verdict**. The [settled set](#the-settled-set) above is the part that has been checked
by hand; a flag on its own establishes nothing.
