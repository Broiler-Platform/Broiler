# WPT rendering gaps — the worst pixel mismatches

- **Scope:** the `< 50% match` tail of the WPT run reported in
  [issue #1491](https://github.com/Broiler-Platform/Broiler/issues/1491),
  problems 4–30. Each of those 27 tests renders at **0.0–2.5%** of its Chromium
  reference, so each is a whole-canvas difference rather than a tolerance
  problem.
- **Not in scope:** problem 1 (the `DomDocument.CreateElement` crash) is fixed —
  frames no longer parse a non-HTML resource as markup, and
  `patches/0035-…` carries the DOM-layer fix. Problems 2 and 3 (per-test memory
  aborts) are the per-element JS wrapper cost, tracked in
  [the root roadmap](ROADMAP.md#htmlbridge-runtime).
- **Companion documents:** [root roadmap](ROADMAP.md) for cross-component work;
  the component roadmaps own the implementation once an item below names them.

Every item names an owner, the evidence behind it, its next action, and an
objective exit gate. Where the evidence is a local measurement rather than the CI
artifact, it says so — several of these tests cannot be reproduced faithfully
without the WPT server, and a local number that looks better than CI's usually
means *both* engines rendered nothing.

## Reproducing one of these locally

The runner compares Broiler's render against a Chromium screenshot of the same
test, so a local investigation needs both halves:

```sh
# 1. Broiler's render
dotnet run --project src/Broiler.Wpt -- --wpt-dir <checkout> --render <checkout>/<test>

# 2. Chromium's reference (Playwright is pinned in tests/wpt/package.json;
#    this container already has a browser at $PLAYWRIGHT_BROWSERS_PATH)
node scripts/generate-wpt-references.js <checkout>/<dir> <refs>/<dir> --base-dir <checkout>

# 3. The comparison, its category, and side-by-side images
dotnet run --project src/Broiler.Wpt -- --wpt-dir <checkout> --reference-dir <refs> \
  --subset <dir> --failure-images <out>
```

Two caveats, both learned the hard way while writing this document:

- **A test that needs the WPT server cannot be reproduced from a bare checkout.**
  `.sub.html` files need substitution, `?pipe=trickle(…)` needs the server's pipe
  handlers, and cross-origin tests need a second host. Offline, both engines fail
  the same way and the match score is meaningless — a *higher* local score than
  CI is the signature of this.
- **Reference generation must honour `reftest-wait`.** A flat screenshot delay
  reads a view-transition or `takeScreenshotDelayed` test at the wrong moment, so
  the local reference disagrees with CI's.

## Animated images always paint their first frame

- **Tests:** problems 7–10, all four
  `css/css-image-animation/image-animation-*-paused.html`.
- **Owner:** Broiler.Media (frame selection) with Broiler.HTML (a paint-time
  clock).
- **Current evidence:** measured locally, and unambiguous — for all four tests
  Broiler's canvas is 100% `rgb(0,255,0)` and Chromium's is 100% `rgb(255,0,0)`.
  The tests set an animated GIF (`images/anim-gr.gif`, two frames, green then
  red) as the root or body background and ask for `image-animation: paused`.
  Chromium does not implement `image-animation`, so its reference is simply the
  frame its own timeline had reached at screenshot time (300 ms, via
  `takeScreenshotDelayed`); Broiler paints frame 0 because nothing in the static
  render path carries elapsed time into image decoding. The propagation
  variations (root vs body, propagating or not) are incidental — the background
  is painted in every case, only its frame is wrong.
- **Next actions:**
  1. Give the render pass a pinned presentation time and select an animated
     image's frame from its own frame delays against that time, instead of
     always decoding frame 0.
  2. Thread the runner's reftest-wait delay into that presentation time so a
     `takeScreenshotDelayed(N)` test is rendered as of N.
  3. Only then implement `image-animation: paused` itself; while Chromium's
     reference is the unpaused render, honouring the property makes these four
     tests *diverge* rather than converge.
- **Exit gate:** an animated image paints the frame its timeline selects at the
  pass's presentation time, the four tests match their references, and a focused
  test pins frame selection at two different presentation times.

## View transitions composite no captured content

- **Tests:** problems 14–23, ten `css/css-view-transitions/*` tests.
- **Owner:** HtmlBridge (`DomBridge.ViewTransition.cs`) with Broiler.HTML for the
  snapshot compositing.
- **Current evidence:** the pseudo tree is materialised, but nothing captured
  goes into it. In `auto-name.html` Broiler's canvas is 97%
  `rebeccapurple` — exactly the `html::view-transition { background:
  rebeccapurple }` root the test sets — while Chromium's is 97% white with the 1%
  green and 1% yellow squares that are the captured old/new items. So the
  `::view-transition` root paints as an opaque box over the viewport and the
  `::view-transition-old()` / `::view-transition-new()` images are absent.
  `root-captured-as-different-tag` (ours 100% red vs 98% white) and
  `old-/new-content-captures-root` (ours 99% pink) fail the same way. The two
  `iframe-and-main-frame-transition-*` tests render blank against a reference
  that is 75% green + 25% blue, so there the transition never starts at all.
- **Next actions:**
  1. Capture the old and new snapshots as images and paint them in the
     `::view-transition-old()` / `::view-transition-new()` boxes, rather than
     materialising the pseudo tree as empty positioned boxes.
  2. Keep the `::view-transition` root behind the captured groups in paint order.
  3. Make `document.startViewTransition`'s callback and its `ready` promise run
     the DOM update before the screenshot, which the blank-rendering iframe pair
     suggests is not happening for a nested browsing context.
- **Exit gate:** a paused view transition composites both captured snapshots at
  their group geometry, the ten tests report a non-trivial match, and a focused
  test asserts old/new snapshot paint order against the pseudo root.

## System colour keywords resolve to a dark palette

- **Test:** problem 28, `forced-colors-mode/forced-colors-mode-20.html`.
- **Owner:** Broiler.CSS.
- **Current evidence:** the test paints `body { background-color: Canvas }`.
  Broiler's canvas is 98% black; Chromium's is 98% white. Forced colors are not
  active in either engine (the reference is an ordinary Chromium render), so
  `Canvas` must resolve to the light palette's white. Broiler resolving it to
  black turns every system-colour test into a whole-canvas mismatch, and this one
  is only the worst-scoring member of that family.
- **Next actions:**
  1. Resolve the CSS system colours (`Canvas`, `CanvasText`, `LinkText`, `Field`,
     `ButtonFace`, …) from the light palette by default.
  2. Switch palettes only where a used `color-scheme` or an emulated forced-colors
     mode asks for it, and keep `forced-colors` computing to `none` while nothing
     emulates it.
- **Exit gate:** `getComputedStyle` reports the light-palette value for every
  system colour in a default document, the test matches, and a focused test
  covers the `color-scheme: dark` switch.

## `contrast-color()` and style container queries

- **Test:** problem 6, `css/css-color/contrast-color-style-query.html`.
- **Owner:** Broiler.CSS.
- **Current evidence:** Broiler renders 100% white where Chromium renders 100%
  green. The test needs three features to compose: an `@property`-registered
  `<color>` custom property, `contrast-color(#000)` resolving to an absolute
  colour, and `@container style(--contrast-color: white)` matching on it. Any one
  missing leaves the `background: green` rule unapplied, which is what the blank
  render shows.
- **Next action:** implement `contrast-color()` as an absolute-colour resolution
  at computed-value time, then confirm whether the style container query
  evaluates registered custom properties; the test distinguishes the two once
  either lands.
- **Exit gate:** `contrast-color()` computes to an absolute colour, a style
  container query matches on a registered custom property, and the test matches.

## `<base href>` is ignored for `<link rel=stylesheet>` in the render path

- **Test:** problem 25,
  `html/semantics/document-metadata/the-link-element/stylesheet-with-base.html`.
- **Owner:** main repo (the renderer's stylesheet resolution).
- **Current evidence:** the test sets `<base href="resources/">` and links
  `stylesheet.css`, so only `resources/stylesheet.css` (green) may load — the
  sibling `stylesheet.css` next to the test sets red as the trap. Broiler renders
  100% red. It resolved the href against the document URL and loaded the trap
  file, which is exactly what the `<base>` is there to prevent.
- **Next actions:**
  1. Resolve a `<link rel=stylesheet>` href against the document's base URL in
     the render path. `e4cc5e9` fixed one such site; this render path is a second
     one, so find the shared seam rather than patching a third.
  2. Cover `@import` and `url()` inside a `<base>`-scoped document at the same
     time.
- **Exit gate:** every stylesheet URL in the render path resolves against
  `<base href>`, the test matches, and a focused test asserts the trap file is
  never loaded.

## Screen-layout gaps behind the three `*-print.html` tests

- **Tests:** problems 11, 12 and 30 — `css/css-page/monolithic-overflow-011-print.html`,
  `page-margin-002-print.html`, `page-box-008-print.html`.
- **Owner:** Broiler.Layout.
- **Current evidence:** these are `@page` tests, but the runner and its reference
  generator both screenshot them **on screen**, where Chromium largely ignores
  `@page`. So none of the three is blocked on paged media — each is an ordinary
  screen-layout gap:
  - `page-box-008`: Broiler is 99% hotpink (the body background) where Chromium
    is 99% yellow (a `block-size: 100vb` box). The logical viewport unit `vb`
    does not resolve, so the box has no size.
  - `monolithic-overflow-011`: Broiler renders 99% white against 95% yellow + 5%
    hotpink — the `display: table` subtree with a `contain: size; height: 350vh`
    row-group paints nothing.
  - `page-margin-002`: Broiler is 100% yellow, Chromium 100% white, with
    `writing-mode: vertical-rl` on the root and three `100vw × 100vh` blocks.
    The two engines disagree about where a vertical-rl root's initial scroll
    position is. **Unconfirmed** — verify the scroll origin before treating this
    as a paint bug.
- **Next actions:**
  1. Resolve the logical viewport units (`vb`, `vi`, and their `sv`/`lv`/`dv`
     variants) against the writing mode.
  2. Lay out and paint a `contain: size` box inside table internals.
  3. Establish what a vertical-rl root's initial scroll position is and align the
     canvas extent with it.
- **Exit gate:** all three tests match on screen, with focused tests for logical
  viewport units and for `contain: size` in table internals — and no paged-media
  work is required to get there.

## Frameset frames render nothing

- **Test:** problem 26, `resource-timing/initiator-type/frameset.html`.
- **Owner:** HtmlBridge (nested browsing contexts) with Broiler.HTML.
- **Current evidence:** Broiler's canvas is 100% white; Chromium's is 100%
  `#dddddd` with frame borders. `0b3c596` and `a06b53c` moved `<frame>`
  sub-documents and the iframe default object size forward, so this is the
  remaining `<frameset>` case: the frameset grid paints neither its own canvas
  nor its frames' documents.
- **Next action:** render a `<frameset>`'s frames as nested browsing contexts
  positioned on the frameset grid, and paint the frameset's own canvas behind
  them.
- **Exit gate:** the test matches, and a focused test asserts a two-frame
  frameset paints both documents at their grid rects.

## `Node.moveBefore` is missing

- **Test:** problem 27, `dom/nodes/moveBefore/preserve-render-blocking-style.html`.
- **Owner:** Broiler.DOM with HtmlBridge for the binding.
- **Current evidence:** Broiler renders white where Chromium renders 100% green.
  The test moves a render-blocking `<style>` with `moveBefore()` and asserts the
  styles survive the move; without the method the script throws and the document
  is never styled.
- **Next actions:**
  1. Implement `moveBefore()` as a state-preserving move (no removal/insertion
     pair), and expose it on the bridge's node surface.
  2. Keep a moved render-blocking element's blocking state intact.
- **Exit gate:** the test matches, and focused tests cover a move within and
  across parents plus the render-blocking case.

## Shadow-DOM focus delegation paints the wrong surface

- **Test:** problem 29, `shadow-dom/focus-navigation/delegatesFocus-highlight-sibling.html`.
- **Owner:** HtmlBridge (shadow tree + focus) with Broiler.HTML for control
  chrome.
- **Current evidence:** Broiler's canvas is 99% `#cccccc` — one flat grey area —
  where Chromium's is 98% white with form-control chrome and a focus highlight.
  The rendered surface, not just the highlight, is wrong, so this is more than a
  missing focus ring.
- **Next action:** establish what Broiler is painting grey (a slotted host box, or
  a UA widget fallback) before touching focus delegation itself; the highlight is
  the test's subject but not its current failure.
- **Exit gate:** the test matches, with a focused test for `delegatesFocus`
  moving focus to the first focusable shadow descendant.

## Items that need the WPT server before they can be judged

- **Tests:** problem 5
  (`css/css-color-adjust/…/color-scheme-iframe-background-mismatch-opaque-cross-origin-002.sub.html`),
  problem 4 (`css/css-backgrounds/background-image-shared-stylesheet.html`),
  problem 13 (`css/css-transforms/animation/transform-interpolation-002.html`).
- **Owner:** the WPT runner, then the component the confirmed failure names.
- **Current evidence:** all three are reported at 0.0% by CI but are not
  reproducible offline, and their local scores are misleading:
  - problem 5 needs `.sub` substitution and a cross-origin host. Offline Broiler
    paints the `#121212` dark canvas backdrop and Chromium paints white, which
    hints at a `color-scheme` propagation difference but proves nothing without
    the real cross-origin frame.
  - problem 4 needs `?pipe=trickle(d2)` for its image and a script-injected
    `data:text/css` stylesheet; offline neither engine loads the image, so the
    pair matched at 99.8% locally while CI reports 0.0%.
  - problem 13 builds its whole DOM from `interpolation-testcommon.js`; offline
    both renders are empty (100% local match, 0.0% on CI), so the CI artifact's
    `rendered.png` is the only evidence that says what Broiler actually drew.
- **Next actions:**
  1. Pull the `wpt-merged` artifact's failure images for these three before
     opening any component work — the local pipeline cannot see their real
     failure.
  2. Decide whether the runner should serve tests over a local HTTP origin with
     substitution and pipe support, which is what would make this whole class
     reproducible.
- **Exit gate:** each of the three is either reproducible locally or reassigned to
  an owning component with a CI-artifact failure image as its evidence.

## Runner: a `manual/` test is being scored

- **Test:** problem 24,
  `html/canvas/element/manual/draw-element-image/dialog-paints-in-top-layer.tentative.html`.
- **Owner:** the WPT runner (`src/Broiler.Wpt`).
- **Current evidence:** the test sits under a `manual/` path segment and is
  `.tentative`, but it is discovered and scored as a Regular test. Its reference
  is a 100% white Chromium canvas, because Chromium does not implement the
  proposed `draw-element-image` API either — so the only way to "pass" is to
  render nothing. Broiler paints a dialog (98% `#e5e5e5` + 2% green), which is
  arguably the more useful behaviour.
- **Next action:** treat a `manual/` path segment as a manual test in discovery,
  the way a `-manual.html` suffix already is, and report the count change so the
  drop is not read as a regression.
- **Exit gate:** `manual/` tests land in the Manual bucket, the run's Regular
  count changes by exactly the number reclassified, and problem 24 leaves the
  scored set.

## Reported problems, at a glance

Local numbers come from this container against locally generated Chromium
references; CI's are authoritative where they disagree.

| # | Test | CI | Local observation |
| --- | --- | --- | --- |
| 4 | `css-backgrounds/background-image-shared-stylesheet` | 0.0% | 99.8% — needs the server's `trickle` pipe |
| 5 | `css-color-adjust/…/cross-origin-002.sub` | 0.0% | ours `#121212`, Chromium white — needs `.sub` |
| 6 | `css-color/contrast-color-style-query` | 0.0% | ours white, Chromium green |
| 7–10 | `css-image-animation/*-paused` (4) | 0.0% | ours green frame 0, Chromium red |
| 11 | `css-page/monolithic-overflow-011-print` | 0.0% | ours blank, Chromium yellow + hotpink |
| 12 | `css-page/page-margin-002-print` | 0.0% | ours yellow, Chromium white |
| 13 | `css-transforms/animation/transform-interpolation-002` | 0.0% | 100% — both empty offline |
| 14–23 | `css-view-transitions/*` (10) | 0.0–1.3% | pseudo root paints, captures absent |
| 24 | `canvas/…/manual/dialog-paints-in-top-layer.tentative` | 0.0% | ours dialog, Chromium blank (unsupported) |
| 25 | `the-link-element/stylesheet-with-base` | 0.0% | ours red (trap file), Chromium white |
| 26 | `resource-timing/initiator-type/frameset` | 0.0% | ours white, Chromium `#dddddd` |
| 27 | `dom/nodes/moveBefore/preserve-render-blocking-style` | 0.0% | ours white, Chromium green |
| 28 | `forced-colors-mode/forced-colors-mode-20` | 0.0% | ours black, Chromium white |
| 29 | `shadow-dom/focus-navigation/delegatesFocus-highlight-sibling` | 0.0% | ours flat `#cccccc`, Chromium white + chrome |
| 30 | `css-page/page-box-008-print` | 0.0% | ours hotpink, Chromium yellow |
