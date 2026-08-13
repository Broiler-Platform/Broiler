# WPT rendering gaps — not fixed

> Part of the [WPT rendering gaps](wpt-rendering-gaps.md) set:
> **not fixed** · [fixed](wpt-rendering-gaps-fixed.md) · [won't fix](wpt-rendering-gaps-wont-fix.md).
> Every status here was re-measured on **2026-08-13**; see
> [How this was verified](wpt-rendering-gaps.md#how-the-2026-08-13-split-was-verified).

Real gaps. Each entry names an owner, the evidence behind it, and an objective
exit gate. `rel=match` is Broiler against the reference the test itself declares,
measured 2026-08-13; CI is the golden-image score from the
[#1624 run](https://github.com/Broiler-Platform/Broiler/issues/1624).

**Two things to check before starting on any of them.**

1. **Look at what the reference contains.** A 0.0% is as likely to be a
   [reference disagreement](wpt-rendering-gaps-wont-fix.md) as an engine gap, and
   chasing one means deleting working support.
2. **A test can pass its own reference and still be wrong** — see
   [the false negative](#the-flag-can-be-a-false-negative) — and it can fail its
   own reference while passing CI, which is the newer and more interesting case:
   four tests below are green on CI and demonstrably wrong.

## Contents

- [View transitions](#view-transitions)
- [Grid](#grid)
- [Sizing and layout](#sizing-and-layout)
- [Masking](#masking)
- [Dynamic stylesheets](#dynamic-stylesheets)
- [Quirks](#quirks)
- [Text and fonts](#text-and-fonts)
- [Tests the pixel suite cannot judge](#tests-the-pixel-suite-cannot-judge)
- [Runner and harness](#runner-and-harness)
- [Method notes](#method-notes)

---

## View transitions

The largest remaining cluster, and it reduces to three causes.

### The root capture is not rasterised

- **Tests and current numbers:**

  | Test | CI | `rel=match` |
  | --- | --- | --- |
  | `css-view-transitions/root-to-shared-animation-start` | 1.5% | 1.5% |
  | `css-view-transitions/new-content-captures-root` | passing | 98.5% |
  | `css-view-transitions/old-content-captures-root` | passing | 98.5% |
  | `css-view-transitions/root-captured-as-different-tag` | passing | 98.5% |

- **Owner:** `Broiler.HTML` — this is a renderer capability, not something the
  bridge can synthesise from the DOM.
- **Where it stands.** The
  [gated DOM clone](wpt-rendering-gaps-fixed.md#the-root-snapshot-now-clones-when-the-page-cannot-show-through)
  took the bottom three from a flat pink page to passing on CI. It is still a
  clone, and the residual 1.5% is what a clone cannot reach: **a root capture that
  is exact in both cases needs a rasterised snapshot from the renderer.** That is
  what the original "capture the old and new snapshots as images" action asked for,
  and it still stands.
- **Read the fixed entry before attempting it.** Cloning the DOM unconditionally
  was implemented, measured at +8/−7 across 458 tests, and reverted; the reason is
  structural, not a missing detail.
- **Exit gate:** a rasterised root snapshot composites at the group geometry so
  `root-to-shared-animation-start` matches, with the other three staying passing.

### The snapshot clone lays its children out horizontally

- **Tests:** the four horizontal members of the `massive-element-*` family —
  `massive-element-{left,right}-of-viewport-partially-onscreen-{new,old}`. CI
  1.8–2.6%, `rel=match` **2.0% / 2.7%**.
- **Scrolling is not the problem, and that is measured.** Rendering the tests' own
  `-ref.html`, which performs the identical scroll without a transition, gives
  white 87.1% / green 10.6% / blue 1.3% against Chromium's 87.2% / 11.5% / 0.7% —
  so scrolling a vertical writing mode is right, and the gap is in the transition.
  [The old capture's containing block](wpt-rendering-gaps-fixed.md#the-old-capture-was-not-in-the-snapshot-containing-block)
  was the other half and is fixed; it closed the two *vertical*-scroll siblings.
- **What remains.** Ours is green 85.1% — a green band ~651px tall where the
  element is 100px tall — so `.middle`'s `block-size: 39800px` is resolving as a
  height. It is **not** a lost `writing-mode` bake:
  `BuildViewTransitionSnapshotContent` carries `writing-mode: vertical-lr` onto the
  content box correctly, so the miss is further in, in how the clone's box is
  sized. These four are the horizontal-scroll members, where the block axis and the
  scrolled axis are the same one.
- **Exit gate:** the four match; the family is 20 tests locally and the other 16
  must not move.

### A captured element still paints in place

- **Test:** `css-view-transitions/names-are-tree-scoped`, CI 1.3% → **94.9%**
  against its own reference.
- Per spec a captured element is **replaced** by its snapshot, not drawn alongside
  it. Ours shows the three light-tree red boxes where the reference shows only the
  shadow-scoped green snapshot.
- The [page-selector leak](wpt-rendering-gaps-fixed.md#page-selectors-leaked-into-the-pseudo-tree)
  that took it from 0% to 96.19% is fixed, and the
  [shadow-tree scoping](wpt-rendering-gaps-fixed.md#shadow-trees-leaked-their-styles-into-the-whole-page)
  moved it 96.12% → 94.84% — *down*, correctly, because its shadow rules now stop
  reaching light-tree boxes.
- **Tree scoping proper is still untested here** — that a document-scoped
  `::view-transition-old(*)` rule must not reach a shadow-scoped name — because it
  sits behind this.
- **Exit gate:** a captured element generates no box of its own while its snapshot
  is live, and a focused test pins a document-scoped rule not reaching a
  shadow-scoped name.

### A nested browsing context runs no transition

- **Tests:** `css-view-transitions/iframe-and-main-frame-transition-old-main-{old,new}-iframe`,
  CI 0.0% → **74.5%** against their own references.
- The two tests drive `iframe.contentDocument.startViewTransition` — a transition
  in a nested browsing context, composited with the parent's. The script never gets
  going.
- This belongs with the nested-browsing-context work, not with compositing.
- **Also here:** `css-view-transitions/nested/nested-position-with-border` (98.3%)
  and `nested/nested-root-capture-with-clip` (98.9%), both of which now pass on CI
  and marginally miss their own reference.
- **Exit gate:** a nested browsing context runs its own transition and composites
  into the parent, with a focused test pinning an iframe's old root snapshot
  against the parent's.

### Scrollbars in a captured snapshot

- **Tests:** `css-view-transitions/{new,old}-content-has-scrollbars`, CI 11.1% →
  **11.1%** against their own references. Reproduces exactly; not triaged further.

### Two tests are green on CI and wrong

Found by this review, and the more useful half of it. Both **pass the golden-image
comparison** and **fail the reference the test itself declares**:

| Test | CI | `rel=match` |
| --- | --- | --- |
| `css-view-transitions/html-becomes-fixed` | passing | **0.5%** |
| `css-view-transitions/nothing-captured` | passing | **97.5%** |

Both were previously filed as "does not reproduce here — judge from a CI
artifact", on local scores of 99.99% and 99.54% against locally generated Chromium
references. That reading is now doubly stale: the
[resolver fix](wpt-rendering-gaps-fixed.md#a-root-relative-resolver-returned-a-working-directory-relative-path)
removed the cause of inflated local scores, and the reftest suite — which involves
no other engine at all — says `html-becomes-fixed` renders essentially nothing of
what its own reference asks for.

**A golden-image pass is not proof of correctness.** These two are the mirror image
of a reference disagreement: Broiler and Chromium agree with each other and both
disagree with the test. Nothing in the current reporting surfaces that, because the
golden suite never looks at a passing test's own reference.

- **Exit gate:** `html-becomes-fixed` renders its own reference; and the runner
  reports the class — a test that passes the golden while failing its `rel=match`
  is worth a heading of its own, the way reference disagreements now get one.

---

## Grid

### `grid-lanes` is an unshipped draft feature

- **Tests:** `css-grid/grid-lanes/subgrid/grid-subgridded-to-grid-lanes/…` —
  `column-subgrid-auto-fill-003` (CI 0.8%, `rel=match` **94.0%**),
  `column-subgrid-orthogonal-writing-mode-004` (0.9%, **94.8%**),
  `column-subgrid-auto-fill-008` (11.5%, **10.4%**).
- Built on `display: inline grid-lanes` — CSS Grid Level 3 — combined with
  `grid-template-columns: subgrid` and `repeat(auto-fill, [line-names])`. Broiler
  **deliberately** treats `grid-lanes` as an invalid display value so the
  declaration is dropped and the element keeps its default display, on the stated
  grounds that no stable browser ships it unflagged
  (`Broiler.Layout/Engine/CssUtils.cs`, and the pinned `Broiler.CSS` rejects it at
  validation). Chromium does the same.
- So both engines lack the feature, and what remains is a difference in how the
  *unfeatured fallback* lays out. The first two sit at 94–95% against their own
  reference and 0.8–0.9% against Chromium's golden — the signature of a reference
  disagreement, which they do not get flagged as only because they miss the 99%
  gate. `-008` is different: 10.4% against its own reference is a real track-sizing
  gap.
- **Worth a maintainer's call** on whether the first two belong in
  [won't fix](wpt-rendering-gaps-wont-fix.md#two-fall-through-the-99-gate). Chasing
  byte-compatibility on a dropped declaration is not the same as implementing
  subgrid.

### The flag can be a false negative

- **Test:** `css-grid/subgrid/orthogonal-writing-mode-006`, CI 5.6%, `rel=match`
  **passes at 100%** — and CI flags it as a reference disagreement.
- **It is not one, and this is the cautionary case for the whole split.** The
  `-ref.html` is byte-for-byte the test except that `.grid > .grid` drops
  `grid-template: subgrid / subgrid`. So the test asserts that a subgrid whose
  parent declares no explicit tracks lays out exactly like a plain nested grid — and
  since Broiler drops `subgrid` as a no-op, it renders the test and the reference to
  the **identical PNG**. Passing is automatic and means nothing.
- **The whole 5.6% is the grid layout the test and the reference *share*.** The body
  is a `display: grid` with `place-items: start`, so each of the eight cyan `.grid`
  children should shrink-wrap; ours stretch to the full viewport width, stack in a
  single column, and scatter the eight Ahem strings along the top. The vertical
  writing modes (`vertical-rl` on half the boxes) are the other half of it.
- **So the exit gate is not "implement subgrid".** It is `place-items: start`
  shrink-wrapping on a grid container plus orthogonal writing modes inside one — and
  the check that subgrid stays a no-op while that is fixed, since the reference
  depends on it being one.

### Not triaged

- `css-grid/abspos/grid-sizing-positioned-items-001`, CI 9.1%. Declares no
  `rel=match`, so it can only be judged from a CI artifact.

---

## Sizing and layout

### `<canvas>` cannot paint its bitmap

Three tests regressed when `<canvas>` was
[modelled as a replaced element](wpt-rendering-gaps-fixed.md#a-replaced-elements-two-axes-were-sized-independently),
and **two of them were passing by rendering nothing** — the trap, sprung in the
other direction. None is a sizing bug.

- `css-images/object-view-box-writing-mode-canvas` (94.3%). The test's canvas
  carries `background-color: black` and the reference's does not; Broiler cannot
  paint a canvas bitmap, so with the canvas sized at zero *both* sides were blank
  white and agreed. Giving it its real size paints the black box the test asks for
  against a reference whose canvas shows a painted bitmap Broiler has no 2D context
  to produce.
- `css-grid/alignment/grid-align-baseline-005` (92.3%) is the same in a grid, plus a
  placement gap it uncovers: the two items land in separate rows where the template
  asks for one.
- `css-sizing/intrinsic-percent-replaced-012` (98.7%, against a 99% threshold) is a
  genuine near-miss on a `display: block` canvas under `height: 100%`.
- **Exit gate:** a 2D canvas context that paints, at which point the first two
  become ordinary comparisons.

### An inline-block's height ignores `line-height`

- **The diagnosis stands; two fixes were measured and both reverted.** An
  auto-height inline-block takes its height from the glyphs, so `line-height: 10px`
  around a 32px font measures **39px** where every browser gives 10, and the
  ordinary 16px case measures 22px against Chromium's 18. A *block* with the same
  content already honours `line-height`, so the two paths disagree with each other
  as well as with the reference.
- **Attempt 1 — clamp a single-line auto-height inline-block to its line box.**
  Fixed every direct measurement — 10px, 16px, 24px and 40px line-heights all matched
  Chromium exactly — **and regressed
  `css-anchor-position/position-area-scrolling-002` to 90.6%**, content shifted left
  30px and up 19px. Bisected rather than assumed: reverting the `normal` change alone
  left the failure, reverting the clamp restored the test. So it is out.
- **Attempt 2 — floor `line-height: normal`.** The reference builds `normal` from
  integer ascent and descent, so the sum lands a whole pixel below the fractional
  height measured here. Flooring instead of rounding up matches Chromium on **12 of
  19** font sizes swept from 8px to 48px where rounding up matches **6**. Over the
  WPT suite it nonetheless *lost*: `css-values` `lh` unit and `css-overflow`
  clip-border-box-with-size regressed while `css-align` safe-justify-self-vrl
  recovered — a net −1, reproduced by running each test on both builds.
- **The lesson is the entry.** *Whole-page rendering is the authority, not a single
  metric compared in isolation.* A sweep against one number said 12 > 6 and was
  measuring the wrong thing.
- **Closing it properly needs real per-size ascent/descent from the font backend**
  rather than a rounding mode over this one value — the layout layer approximates the
  baseline with a hardcoded 0.8 ratio and has no ascent/descent to work from.
- **Exit gate:** a line box holding nested inline-blocks sizes to the reference (the
  rows are 76px against 54px), with `position-area-scrolling-002` and the `lh`-unit
  tests staying green.

### Not triaged

- `css-flexbox/percentage-heights-003`, CI 15.4%. A `check-layout-th.js` test with
  no `rel=match`.
- `css-flexbox/flexbox-min-width-auto-002b` (98.5% against a 99% threshold), which
  fell out of the
  [abspos replaced fix](wpt-rendering-gaps-fixed.md#an-absolutely-positioned-img-rendered-nothing-at-all).
  It measures `min-width: auto` on a flex item that has an intrinsic ratio and a
  `min-height`, which CSS Flexbox §4.5 resolves through the ratio from the cross
  axis. Broiler does not implement that transfer, and the item was only landing on
  the reference by having no natural size to transfer from. **A flexbox rule, not a
  replaced-sizing one.**

---

## Images

### SVG-as-an-image went through a second, weaker SVG renderer — **fixed**

**[Issue #1627](https://github.com/Broiler-Platform/Broiler/issues/1627).** The largest single cause
found in the 2026-08-13 triage — 11 of the 28 previously-unexamined reference-disagreement flags, and
it reached well beyond them. The renderer duplication is retired; the eleven tests are still open on
the two defects at the end of this entry.

- **Tests:** `css-transforms/transform-background-005`, `-006`, `-007`, `-008`;
  `css-transforms/transform-root-bg-001`, `-002`, `-004`;
  `compositing/root-element-background-image-transparency-001` … `-004`. CI 49.1–51.0%.
  Every one loads `support/transform-triangle-{left,down}.svg` as a CSS background, and
  that file's entire content is one `<polygon>`.
- **Broiler renders a blank white canvas for all eleven** — and for their references,
  which is why `--verify-reference` cleared them.
- **Owner:** `Broiler.HTML` (`Source/Broiler.HTML.Image/BSvgRasterizer.cs`).
- **There are two SVG renderers, and only one of them is missing shapes.** Inline
  `<svg>` markup in a document is rendered by `Broiler.Layout/IR/SvgRenderer.cs`, which
  handles rect, circle, ellipse, line, path, text, **polygon**, **polyline** and groups.
  An SVG loaded as an *image* — `background: url(x.svg)`, `<img src=x.svg>` — is routed
  by `StubImageAdapter` to `BSvgRasterizer` instead, a separate regex-based renderer
  whose whole element set is `RenderRectangles`, `RenderCircles`, `RenderEllipses`,
  `RenderLines`, `RenderPaths` and `RenderText`. **Anything else in the file is silently
  dropped and the bitmap comes back transparent.**
- **Narrowed by probe rather than inferred.** Six one-line documents against the same
  checkout:

  | Probe | Broiler renders |
  | --- | --- |
  | `html { background: blue }` | 100% blue ✓ |
  | `html { background: url(/images/green.png) }` | 100% green ✓ |
  | inline `<svg><polygon fill=blue …></svg>` | 5.7% blue triangle ✓ |
  | `<img src="probe-rect.svg">` (an external SVG holding a `<rect>`) | 5.1% blue ✓ |
  | `<img src="probe-poly.svg">` (the same square as a `<polygon>`) | **100% white ✗** |
  | `html { background: url(support/…​.svg) }` (the tests' file) | **100% white ✗** |

  Rows 4 and 5 are the pair that matters: the *same shape*, in the *same position*, as
  an external SVG image — one paints and one does not. So the file is fetched, sniffed
  as SVG (`BSvgRasterizer.IsSvgData`) and rasterised; the rasteriser just has no arm for
  `<polygon>`. An earlier reading of this — "an external SVG is never decoded" — was
  too strong and is superseded by these two probes.
- **It is much broader than these eleven.** Of the 3 952 currently-failing tests present
  in a local checkout, **70 reference an external `.svg` as an image** — concentrated in
  `css-masking` (20), `css-transforms` (9), `css-images` (8), `html/canvas` (8) and
  `css-ui` (5). Not all fail *because* of this, but it is the first thing to rule out
  for any of them, and any element outside the six above will hit it.
- **Fixed, in two repositories.** SVG images now render through the same `SvgRenderer` as inline
  markup — `Broiler.Layout.IR.SvgImageRaster` builds the display list and the image backend replays it
  through the raster backend it already implements. The two renderers were each a superset of the
  other, so the switch needed a second change to be safe:

  | | `SvgRenderer` (inline) | `BSvgRasterizer` (image) |
  | --- | --- | --- |
  | Elements | rect, circle, ellipse, line, path, text, **polygon**, **polyline**, groups, textPath | rect, circle, ellipse, line, path, text |
  | Percentage lengths | **was: none at all** — now resolved | resolved against the viewport |
  | Attribute quoting | **was: double quotes only** — now either | either |

  `SvgRenderer` had no percentage handling anywhere, so `<rect width="100%">` parsed as `0` and drew
  nothing. It now resolves percentages per SVG 1.1 §7.10 — horizontal lengths against the viewport
  width, vertical against its height, and `r`/`stroke-width`/`font-size` against the normalised
  diagonal — taking the viewport from the `viewBox` when there is one and from the destination box
  when there is not. **That fixes inline SVG too**, which ignored percentages for the same reason.
- **And a second parser gap, found by the tests for the first.** `ParseAttributes` matched
  **double-quoted attributes only**, so `<rect x='0' width='100'>` parsed as an element with *no
  attributes at all* and drew nothing — silently, since an element with no attributes is not an error.
  XML gives the two quote styles equal standing, and **101 documents** under the directories this sweep
  covers are written with single quotes. Both styles parse now, and a value may contain the other quote
  (`title='He said "hi"'`). It was found because the first draft of the new tests **passed while
  asserting nothing**: written with single quotes, every element parsed bare.
- **Measured over 3 974 reftests**, before and after, on the same build:

  | | passing | avg match |
  | --- | --- | --- |
  | baseline | 2 675 | 98.562% |
  | + the switch and percentages | 2 751 | 98.602% |
  | **+ attribute quoting** | **2 756** | 98.599% |

  **+95 / −14 against baseline.** The headline cluster is `css-images/object-fit-*-svg-*`,
  **52/120 → 120/120**. The quoting fix is **+5 / −0** on its own, and three of those five —
  `non-scaling-stroke-003/-009/-010` — are tests the switch itself had regressed, because they are
  written with single quotes and the renderer it switched *to* could not read them.
  - **Five of the 14 losses were passing by rendering nothing** — test and reference both blank, so
    they matched at 100%. They now render real content and expose the two separate bugs below.
  - **The other nine are sub-1.5% differences** just under the 99% threshold. One test moved
    materially without changing state — `css-images/cross-fade-natural-size`, 96.0% → 76.0% — and it
    is [diagnosed below](#cross-fade-is-unimplemented-and-chromium-does-not-implement-it-either): not
    a regression, the same blank-on-blank artefact disappearing.
- **The eleven tests this started from still do not pass**, and that is the honest result: the
  `<polygon>` now renders, but they are blocked on the two defects below. Four improved against the
  Chromium golden — `transform-root-bg-001`/`-004` and `transform-background-007` from 49.1% to 60.8%,
  `perspective-svg-001` from 27.3% to 47.1%.
- **Where it landed.** `SvgImageRaster` and the percentage support are main repo. The image backend's
  six-line call site is `patches/0001` — the push to `Broiler.HTML` is denied (403), so it ships as a
  patch registered in `scripts/apply-pending-wpt-patches.sh`. **The two halves must land together:**
  the patch alone regresses ~70 `background-size/vector` tests, which is exactly what percentages fix.
- **Two smaller things the attempt exposed**, both pre-existing and both invisible while SVG images
  rendered nothing:
  - **A supersampled SVG tiles at its raster size, not its intrinsic size.** `GetSvgRasterizationScale`
    rasterises an SVG whose longest side is under 128px at 2× for quality, and the background tiling
    then uses the 200×200 bitmap instead of the 100×100 intrinsic size. Measured: a 100px SVG tiles
    every 200px, a 150px one (above the threshold, so scale 1) tiles correctly every 150px.
  - **A transform on the root element moves the canvas background.** With the images rendering,
    `transform-root-bg-001` shows Broiler flipping the tiled background under `transform: scale(-1)`,
    which is exactly what the test asserts must not happen.
- **Exit gate:** the same SVG file renders identically whether it is inline markup or an
  external image, the eleven tests match, and a sweep over `css-masking`, `css-images`
  and `css-transforms` shows no regression.

### Other image formats and inline-SVG edge cases

Each renders blank, each was cleared by the same flag, and each is its own gap:

| Test | CI | What is missing |
| --- | --- | --- |
| `avif/animated-avif-timeout` | 68.0% | AVIF decode — the test is one `<img src=…​.avif>` |
| `css-paint-api/one-custom-property-animation-half-opaque.https` | 68.2% | the CSS Paint API — `paintWorklet.addModule` and `paint()` |
| `resize-observer/devicepixel2` | 50.0% | a canvas 2D context: the page builds its background from a canvas `toDataURL` under a `ResizeObserver` |
| `svg/extensibility/foreignObject/foreign-object-paints-before-rect` | 68.2% | `<foreignObject>` content inside an inline `<svg>` |
| `css-transforms/perspective-svg-001` | 27.3% | a percentage-sized inline `<svg>` under `perspective` with `backface-visibility: hidden` and 3D transforms |
| `filter-effects/backdrop-filter-plus-mask-large` | 43.7% | `backdrop-filter` combined with a mask — the only blank one that also **fails its own reference** (43.8%), so it is not hidden by the flag |

The canvas gap is the same one that keeps
[three `<canvas>` sizing tests](#canvas-cannot-paint-its-bitmap) from passing.

### Six that render content and are still wrong

Broiler draws something, Chromium is self-consistent at 100% against its own
reference, and the two disagree. Not triaged further:

| Test | CI | Broiler vs its own ref |
| --- | --- | --- |
| `filter-effects/backdrop-filter-clip-rect-zoom` | 41.4% | 100% |
| `css-backgrounds/animations/background-color-scroll-into-viewport` | 51.0% | 100% |
| `css-inline/text-box-trim/text-box-trim-accumulation-004` | 61.0% | 100% |
| `css-grid/layout-algorithm/auto-margins-ignored-during-track-sizing-001` | 66.1% | 97.7% |
| `css-ruby/block-ruby-003` | 70.0% | 98.7% |
| `css-view-transitions/massive-element-right-of-viewport-offscreen-new` | 78.8% | 98.6% |

The last belongs to the [`massive-element-*` family](#the-snapshot-clone-lays-its-children-out-horizontally);
the other five are unexamined.

### `cross-fade()` is unimplemented, and Chromium does not implement it either

- **Test:** `css-images/cross-fade-natural-size`. Failing on CI in **both** suites before this work and
  after it; only the local reftest percentage moved, 96.0% → 76.0%.
- **The move is not a regression.** The test's `::before` content is
  `cross-fade(75% url(…), 25% url(…))` over two `data:image/svg+xml` documents, and its reference is a
  single pre-composited SVG. Every SVG in both files is written with **single-quoted attributes**, so
  before [the quoting fix](#svg-as-an-image-went-through-a-second-weaker-svg-renderer--fixed) neither
  side drew anything and two blank canvases matched at 96%. The reference draws now; the test still
  does not, because **nothing in the engine parses `cross-fade()`** — there is no such symbol in
  `Broiler.CSS`, `Broiler.Layout` or `Broiler.HTML`.
- **Chromium fails this reftest too, and by more.** Rendering all four documents:

  | | match |
  | --- | --- |
  | Chromium test vs Chromium reference | **67.8%** |
  | Broiler test vs Broiler reference | 76.0% |
  | Broiler test vs Chromium test — *what the golden suite scores* | 95.7% |
  | Broiler reference vs Chromium reference | 67.4% |

  Chromium's render of the test is 99.19% white: it does not implement the CSS Images 4 percentage
  syntax either, so the declaration is dropped. The two engines agree closely on the *test* (95.7%)
  precisely because both render nothing — which is why the golden score is nowhere near as alarming as
  the reftest one. **Both engines fail this test**, so it belongs with the pair below rather than being
  read as something this work broke.
- **The reference render exposes three separate `SvgRenderer` gaps**, each small and each now visible
  because the document finally parses. The reference asks for tinted rects over a black background and
  Broiler paints solid colours on white:
  - **`fill-opacity` is unhandled** — the green rect paints `rgb(0,128,0)` where it should be 50% over
    black. Only `flood-opacity` on an `feFlood` is read today.
  - **`mix-blend-mode` on a shape is unhandled**, so the `screen` compositing the reference relies on
    does not happen.
  - **`style="background: …"` on the root `<svg>` is not painted**, leaving white where the reference
    wants black.

  Those three are worth more than this test: they apply to every SVG document Broiler renders, inline
  or as an image. They are the reason `Broiler reference vs Chromium reference` is only 67.4%.
- **Exit gate:** `fill-opacity`, `mix-blend-mode` and a root `<svg>` background all render, which takes
  the reference to Chromium's; `cross-fade()` is a separate feature and a separate decision, given
  Chromium does not implement this syntax.

### Two where both engines fail the test

Worth separating from the rest, because the test itself may be at fault:

- `css-conditional/container-queries/query-style-color` — CI 64.8%. Chromium scores
  **86.1%** against its own reference and Broiler **98.0%**. Both fail; Broiler is
  closer. Given the [style-query work](wpt-rendering-gaps-fixed.md#contrast-color-and-style-container-queries)
  deliberately supports only the custom-property form of `style()`, the residual is
  probably the standard-property form.
- `css-gaps/flex/fragmentation/flex-gap-decorations-fragmentation-024` — CI 78.9%.
  Chromium and Broiler each score **97.3%** against the reference. Two independent
  engines failing by the same margin points at the test or its reference rather than
  at either engine; check it upstream before spending time on it.

## Masking

### SVG `<clipPath>` referenced by `url()`

- **Test:** `css-masking/clip-path/clip-path-element-userSpaceOnUse-004`, CI 2.9% →
  **82.6%** against its own reference.
- The [path-clip work](wpt-rendering-gaps-fixed.md#clip-path-modelled-only-inset)
  landed `polygon()`, `circle()`, `ellipse()` and `url(#…)` resolution, and took this
  test from 2.9% to 82.6% — but not over the line. `userSpaceOnUse` units on the
  referenced `<clipPath>` are the remaining piece.
- **Exit gate:** the test matches, with the two `clip-path-document-element` tests
  staying passing.

---

## Dynamic stylesheets

### A script-injected `data:text/css` stylesheet never applies

- **Test:** `css-backgrounds/background-image-shared-stylesheet`, CI 5.7% →
  **5.7%** against its own reference. Reproduces exactly.
- **It does not need the WPT server, and was filed for three runs as though it
  did.** The `?pipe=trickle(d2)` query is simply stripped and `/images/green.png` is
  served from the checkout like any other root-relative resource. The earlier note
  that "the pair matched at 99.8% locally while CI reports 0.0%" was
  [the resolver bug](wpt-rendering-gaps-fixed.md#a-root-relative-resolver-returned-a-working-directory-relative-path):
  neither side loaded the image, so the two agreed on nothing.
- **What the 5.7% is, precisely.** The reference is 100% lime. Broiler paints 94%
  white with a green block of exactly 300×150 — **the default `<iframe>` size**. So
  the iframe is never removed and the parent's script-injected `data:text/css`
  stylesheet never applies, while the *iframe's own* copy of that stylesheet does.
- **Exit gate:** a `data:text/css` stylesheet injected from script applies to the
  injecting document, with a focused test covering the shared-sheet case.

---

## Quirks

### A table inherits `color` where it must not

- **Test:** `quirks/tables-inherit-color-from-body-quirk-007`, CI 5.1% → **94.9%**
  against its own reference.
- **The earlier diagnosis was wrong and is worth recording as such.** It concluded
  that appending an element to `document` after `documentElement.remove()` never
  installs a new document element, that "the render stays empty", and that this was a
  DOM/bridge gap rather than a quirks one. Measured: ours is 94.6% `rgb(18,18,18)` +
  5.1% square against a reference that is 94.6% `rgb(18,18,18)` + the same 5.1%
  square. **The page renders and the document element is installed.**
- **The entire difference is the square's colour** — ours `rgb(255,0,0)`, the
  reference's `rgb(0,0,0)`. So it is the quirk the test is named for after all: the
  table inherits `color: red` from the `<div>` instead of falling back to the initial
  colour, and the test says so outright — *"Test passes if there is a square filled
  with initial color and no red"*.
- **Exit gate:** a table does not inherit `color` from a non-body ancestor in quirks
  mode, and the square paints the initial colour.

---

## Text and fonts

### Bold and italic never reach the face

**This caps every text comparison whose reference contains bold or italic**, so it
is worth more than the one test that names it.

- **Measured rather than eyeballed:** rendering `HHHH` at 60px as normal, `bold` and
  `italic` gives **byte-identical ink (2384 px) and identical advance span (170 px)**
  for all three, while `monospace` differs (1920 / 134) — so family affects *layout
  advances* and nothing affects the *face*.
- **The chain is intact right up to the last step.** `font-weight` reaches
  `DrawTextItem.FontWeight` (`PaintWalker.Text.cs`), and
  `StubImageAdapter.CreateFontInt(family, size, style)` passes the style into
  `ResolveTypeface`. But `TrueTypeTypefaceResolver.ResolveTypeface(family, style)` —
  the resolver `StubCompatProvider` actually installs — **ignores its `style`
  parameter entirely** and looks the family up by name alone. The raster backend then
  draws from `item.FontHandle`, never from `item.FontWeight`.
- **Underneath that, the HTML image backend has no system-font enumeration at all.**
  `BroilerFontRegistry` records only fonts registered at runtime, so in the WPT path
  the "available families" set is empty, every generic-family mapping resolves to
  nothing, and *every* family falls through to one bundled resource,
  `Vazirmatn-Regular.ttf`. One regular face draws the entire suite.
- **`Broiler.Graphics` has the other half already:** `FallbackSystemFont` discovers a
  system regular+bold **pair** (`DejaVuSans.ttf` + `DejaVuSans-Bold.ttf`) and
  `BImageRenderer` selects between them on `run.Font.Weight >= BFontWeight.Bold`. Its
  `BoldPath` has **no consumer outside its own file** — that path serves the
  UI/Graphics backend, not `HtmlRender`.
- **Not attempted, deliberately.** Closing it means giving the image backend real
  system-font enumeration and `(family, weight, style)` matching — a feature that
  would change the face of every text render in the suite and so needs its own
  before/after sweep. Synthesising bold from the regular outlines would be cheaper but
  would not match a real bold face, so it would not carry a test over the 99%
  threshold anyway, and could regress tests that pass today. **Worth doing; not worth
  doing blind.**
- **One test it currently caps:** `uievents/…/UIEvent.load.stylesheet` renders `PASS`
  where it rendered `FAIL` — both of
  [its gaps are closed](wpt-rendering-gaps-fixed.md#a-stylesheet-link-dispatched-no-load-event)
  — and its score barely moves, 97.88% → 97.87%, because the rest of the difference is
  bold text.
- **Exit gate:** `(family, weight, style)` matching against enumerated system fonts in
  the HTML image backend, with a before/after sweep over the full suite.

---

## Tests the pixel suite cannot judge

These fail for reasons that are not rendering gaps in the usual sense. They are
listed so they are not mistaken for untriaged mismatches.

### Testharness tests, whose reference is a results table

Closing one means passing its subtests, not making one fix.

| Test | CI | Note |
| --- | --- | --- |
| `css-transforms/animation/transform-interpolation-002` | 0.0% | builds its whole DOM from `interpolation-testcommon.js`; declares no `rel=match`, so only a CI artifact says what Broiler drew |

Two more were on this list and have **dropped off the CI failure manifest** —
`css-align/animation/row-gap-interpolation` (2.6% at
[#1538](https://github.com/Broiler-Platform/Broiler/issues/1538)) and
`html/…/form-validation-validity-textarea-defaultValue` (3.8%, three of whose five
subtests drive `test_driver.send_keys`, which the runner only stubs). Both parent
directories are exercised by the run and carry other failures, so absence means
their last scored run passed them. **That is weaker evidence than a measurement:**
neither declares a `rel=match`, so the reftest suite cannot judge them, and the
manifest is a merged file where a test that stops being exercised keeps its old
entry. Confirm from a run artifact before treating either as closed.

### Large documents, not triaged

| Test | CI |
| --- | --- |
| `conformance-checkers/html-svg/*-isvalid` (5) | 11.1–19.3% |
| `conformance-checkers/html/elements/{track,video}/src-isvalid` (2) | 14.4% |
| `cssom-view/scrollIntoView-fixed` | 10.9% |
| `scroll-animations/css/scroll-timeline-nearest-with-absolute-positioned-element` | 11.3% |

None declares a `rel=match`. `scrollIntoView-fixed` needs scripted scrolling.

---

## Runner and harness

### The runner resolves scroll metrics against the wrong viewport

`new WptTestRunner(w, h)` renders at the given size, but the scroll metrics — `vh`
lengths and the maximum scroll offset — resolve against the default 1024×768
regardless. A page built to be "taller than the viewport" at 200×200 therefore
scrolls to somewhere that is not the bottom of the canvas, and a test asserting on
what is on screen fails for a reason that has nothing to do with what it is testing.
`ScrollClampingTests` and `ViewTransitionOldCaptureScrollTests` pin their renders to
the default size to work around it. **A real defect in the runner, not just a
test-authoring trap.**

### `?pipe=` is not emulated

The last piece of WPT server behaviour worth emulating. The handlers are per-pipe and
independent (`tools/wptserve/wptserve/pipes.py`), so `trickle`, `status` and `header`
can follow the same shape the
[`sub` pipe already did](wpt-rendering-gaps-fixed.md#the-runner-never-performed-wpts-sub-substitution)
— a runner-side transform, not a server. No test currently on any list is blocked on
it: `background-image-shared-stylesheet`, the one that was, turned out
[not to need it](#a-script-injected-datatextcss-stylesheet-never-applies).

### Paged media is partial

Off by default: the 409 print reftests score **252 unpaginated versus 212 paged**
(`docs/wpt-reftests.md`). Rendering `page-margin-002-print` as paged media
(`BROILER_WPT_PAGED_PRINT=1`) surfaces two distinct defects — the test paginates to
**4** pages and its reference to **7** where both should be **3** (each `.fullpager`
is exactly one page area), and on both sides only the first block paints:

1. **Viewport units do not resolve against the page area** — `100vh` stays the full
   page box, so each block overflows its page.
2. **`break-before: page` over-fragments.**

Neither can change what CI reports for that test, which is scored unpaginated against
[a blank Chromium capture](wpt-rendering-gaps-wont-fix.md#page-margin-002-print-is-a-screenshot-artifact).

### `--verify-reference` clears a test that renders nothing

**The most consequential defect on this page**, because it silently moves real gaps
off the severity list.

`WptTestRunner.VerifyAgainstReferenceHtml` clears a pixel-mismatch failure whenever
Broiler reproduces the test's own `rel=match` reference. It does not check that
anything was **drawn**. A test that renders a blank white canvas, whose reference
also renders a blank white canvas, matches at 100% and is reported as a reference
disagreement — the exact "passing by rendering nothing" trap this document set warns
about, now built into the mechanism that decides what counts as a real bug.

**Measured on the 2026-08-13 triage of the 28 unexamined flags: 17 of them are
blank-on-blank.** Broiler paints a uniform white canvas for the test *and* for the
reference, while Chromium paints substantial content in both. Every one is a real
gap that had been moved out of the ranking.

Two cheap checks would separate them, and either alone would have caught all 17:

1. **Reject a clear when the render is uniform.** A test and reference that are both
   a single colour across the whole canvas are not evidence of agreement. The runner
   already computes a colour histogram for its `subCategory` classification.
2. **Compare against the committed golden's content, not just its pixels.** A golden
   with substantial content and a render with none is the signature; today that pair
   scores 0.0% and is cleared anyway.

Until one of them lands, **a `suspectReference` flag is a candidate for triage, not a
verdict** — the tables in [won't fix](wpt-rendering-gaps-wont-fix.md#the-settled-set)
are the flags that survived being checked by hand.

### The report cannot distinguish "wrong everywhere" from "wrong only against Chromium"

`--verify-reference` sets `suspectReference` only when Broiler clears the same 99%
gate against the test's own reference, so a test at 94–95% against its own reference
and 0.8–8.0% against the golden is ranked as though nothing were known about it. Four
entries fall through: two `grid-lanes` tests above, and
[two in won't fix](wpt-rendering-gaps-wont-fix.md#two-fall-through-the-99-gate).

**Recording the reference score alongside the golden one, rather than only using it as
a pass/fail gate, would separate the two classes** without needing a second threshold
to be tuned. The same change would surface
[the inverse case](#two-tests-are-green-on-ci-and-wrong) — a test that passes the
golden while failing its own reference — which nothing reports today.

---

## Method notes

### One test is flaky

`css-view-transitions/new-content-transform-change-001` scores 99.6% in one run and
1.0% in the next **on an unmodified build**. It has appeared in a regression diff
twice and was very nearly attributed to a change that had nothing to do with it, both
times. **Re-run a suspicious test against the unmodified build before believing a
diff.**

### References in `css-view-transitions` are generation-sensitive

Locally generated Chromium references for this directory differ enough between
generation passes to move a test from 97.46% to 1.27%. **Compare runs only against
references generated in the same pass.**

### A higher local score than CI is a warning

Historically it meant the resolver had silently failed to load a resource, so *both*
engines rendered nothing and agreed. That
[bug is fixed](wpt-rendering-gaps-fixed.md#a-root-relative-resolver-returned-a-working-directory-relative-path)
and a local run now agrees with CI — but the signature is still worth treating as a
warning rather than as good news. Check whether the resource actually loaded.
