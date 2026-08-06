# WPT rendering gaps — the worst pixel mismatches

- **Scope:** the `< 50% match` tail of the WPT run reported in
  [issue #1491](https://github.com/Broiler-Platform/Broiler/issues/1491),
  problems 4–30. Each of those 27 tests renders at **0.0–2.5%** of its Chromium
  reference, so each is a whole-canvas difference rather than a tolerance
  problem.
- **Later runs get their own section rather than a rewrite**, since most of each
  new list is the same tests under new numbers:
  [#1497 (2026-07-30)](#the-next-run-issue-1497-2026-07-30) and
  [#1538 (2026-08-05)](#the-next-run-issue-1538-2026-08-05). Where a re-run
  contradicts something below, the section says so and the row here is struck
  through — read the newest section first.
- **Not in scope:** problem 1 (the `DomDocument.CreateElement` crash) is fixed —
  frames no longer parse a non-HTML resource as markup, and `patches/0035-…`
  carried the DOM-layer fix (since applied). Problems 2 and 3 are both per-test
  memory aborts but have **different causes**, each tracked in the root roadmap:
  problem 2 (`css/css-variables/url-syntax-crash.html`) is the per-element JS
  wrapper cost, in [HtmlBridge runtime](ROADMAP.md#htmlbridge-runtime); problem 3
  (`editing/crashtests/insertparagraph-in-listitem-in-svg-followed-by-collapsible-spaces.html`)
  creates no elements from script and is the render pipeline copying a 642 MiB
  text node, in
  [Bound what a large text node costs to render](ROADMAP.md#bound-what-a-large-text-node-costs-to-render).
- **Companion documents:** [root roadmap](ROADMAP.md) for cross-component work;
  the component roadmaps own the implementation once an item below names them.
- **Progress:** problems 6, 7–10, 24, 25, 27, 28 and the `vb` half of
  30 are fixed; each section says what landed, what was verified locally, and what
  is left for CI to confirm. Patches `0035`–`0039` — which carried the submodule
  half of 6, 27, 28 and 30 — **have since been applied and their pointers
  bumped**, so all of those are now live on CI rather than pending. So has
  problem 29's `0042` (confirmed two ways: it reverse-applies to the pinned
  `Broiler.HTML`, and `TemplateContentInertnessTests`' probe now sees template
  styles staying inert). The one still waiting on a maintainer is problems 7–10's
  [`patches/0041`](../patches/README.md), whose remote this session cannot push to
  (403, as documented in `CLAUDE.md`) — and which **no longer applies to the
  pinned pointer**, so it needs regenerating before it can be applied at all.
  Until then those four tests stay at their old numbers on CI.
- **Four of these tests should not be "fixed".** Problems 14, 15 and 24 pass only
  by rendering *less* than Broiler already does — their Chromium reference was
  produced by an engine that does not implement the feature under test, so the
  reference is the unfeatured render. Problem 18's reference is a blank white
  canvas for the same reason. Chasing them would mean deleting working support.
  The same trap governs problems 7–10, where the reference is the *unpaused*
  animation: the fix there is frame selection, deliberately not
  `image-animation: paused`. Check what the reference actually contains before
  treating a 0.0% as a gap.

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
#    this container already has a browser at $PLAYWRIGHT_BROWSERS_PATH).
#    `npm install` puts playwright under tests/wpt/node_modules, which is not on
#    the resolution path of a script run from scripts/ — hence NODE_PATH.
(cd tests/wpt && npm install)
NODE_PATH=tests/wpt/node_modules BROILER_CHROMIUM_PATH=/opt/pw-browsers/chromium \
  node scripts/generate-wpt-references.js <checkout>/<dir> <refs> --base-dir <checkout>

# 3. The comparison, its category, and side-by-side images
dotnet run --project src/Broiler.Wpt -- --wpt-dir <checkout> --reference-dir <refs> \
  --subset <dir> --failure-images <out>
```

Four caveats, all learned the hard way:

- **The reference generator's output path is relative to `--base-dir`, not to the
  test directory.** Pointing it at one subdirectory writes
  `<out>/<dir>/<dir>/…`, and the runner then reports every test as
  `MissingReferenceImage` rather than as a mismatch. Give `--reference-dir` the
  root that mirrors `--wpt-dir`.
- **Playwright's pinned browser build may not be the one installed.** The
  container ships a browser at `$PLAYWRIGHT_BROWSERS_PATH` whose build number
  need not match `tests/wpt/package.json`'s pin, and the launch then fails with
  "Executable doesn't exist". Point the generator at the installed one with
  `BROILER_CHROMIUM_PATH=/opt/pw-browsers/chromium` rather than downloading
  another.
- **A test that needs the WPT server cannot be reproduced from a bare checkout.**
  `.sub.html` files need substitution, `?pipe=trickle(…)` needs the server's pipe
  handlers, and cross-origin tests need a second host. Offline, both engines fail
  the same way and the match score is meaningless — a *higher* local score than
  CI is the signature of this.
- **Reference generation must honour `reftest-wait`.** A flat screenshot delay
  reads a view-transition or `takeScreenshotDelayed` test at the wrong moment, so
  the local reference disagrees with CI's.
- **A reference is evidence about Chromium, not about the spec.** Before treating
  a 0.0% as an engine gap, look at what the reference contains — a histogram of
  its colours is usually enough, and a mostly-white one is a warning. Where the
  answer turns on whether Chromium implements something, ask it directly rather
  than inferring from pixels: load the test under Playwright and read the CSSOM
  (`document.styleSheets[…].cssRules`) or the computed value. That is how the
  `view-transition-name: auto` tests were settled — Chromium keeps the rule but
  drops the declaration, so it captures nothing and its reference is the
  untransitioned page. Note that a bare Playwright script does **not** serve
  root-relative WPT paths, so `/common/reftest-wait.js` 404s and every such probe
  looks like "the test never ran"; register a `file://` route that serves them
  from the checkout, the way `generate-wpt-references.js` does.

## Animated images always painted their first frame — **fixed, pending patch**

- **Tests:** problems 7–10, all four
  `css/css-image-animation/image-animation-*-paused.html`.
- **Owner:** Broiler.Media (frame selection) with Broiler.HTML (a paint-time
  clock).
- **Original evidence:** measured locally, and unambiguous — for all four tests
  Broiler's canvas was 100% `rgb(0,255,0)` and Chromium's is 100% `rgb(255,0,0)`.
  The tests set an animated GIF (`images/anim-gr.gif`, two frames, green then
  red) as the root or body background and ask for `image-animation: paused`.
  Chromium does not implement `image-animation`, so its reference is simply the
  frame its own timeline had reached at screenshot time (300 ms, via
  `takeScreenshotDelayed`); Broiler painted frame 0 because nothing in the static
  render path carried elapsed time into image decoding. The propagation
  variations (root vs body, propagating or not) are incidental — the background
  is painted in every case, only its frame was wrong.
- **What landed, in two halves.** The machinery is main-repo and on CI now; the
  single call site that consumes it is the patch.
  1. **Main repo.** `ImageSequence.FrameAt` / `FrameIndexAt` in `Broiler.Media`
     answer "which frame is showing at time *t*", and `ImageAnimationClock`
     carries the presentation time a still render is taken at. The WPT runner
     pins that clock per test from the test's own `takeScreenshotDelayed(N)`,
     read from the source before the script pass and the post-processor strip the
     `<script>` that carries it.
  2. **`patches/0041` (Broiler.HTML).** `StubImageAdapter`'s decode — the single
     seam where a decoded sequence collapses to one bitmap — selects the frame at
     that clock instead of taking `FirstFrame`.
- **The clamp is what makes the numbers work.** `anim-gr.gif`'s green frame
  carries a **10 ms** delay and its red frame 100 s. Taken literally, a 300 ms
  screenshot would land deep in a fast loop; every engine instead treats a delay
  that short as "unspecified" and substitutes 100 ms (Blink's threshold is 11 ms,
  and the references are Chromium's). So green occupies 0–100 ms and red
  everything after — 300 ms is red, which is exactly what the reference shows.
- **`image-animation: paused` is still deliberately unimplemented.** Chromium
  does not implement it either, so each reference *is* the unpaused render;
  honouring the property would make these four tests diverge again. The property
  is what the tests are named for, not what they currently measure.
- **Verified:** the four tests, run against locally generated Chromium
  references, go from **0.0% to 100%** — the same 0.0% CI reports, reproduced
  before the change and gone after. Four focused tests cover the timeline
  (selection at successive times, the short-delay clamp, loop-count wrap versus
  hold, and the clock's nested pin/restore), and 14 cases cover the runner's
  delay extraction — including the negative half, that a test with no literal
  delay resolves to zero rather than guessing.
- **Remaining:** the patch. There is no main-repo fallback — the decode is
  entirely the submodule's — so CI paints frame 0 until it is applied and the
  pointer bumped. A frame-selection test at the `BBitmap` level has to wait for
  the same thing: `Broiler.HTML` has no test project of its own, and a main-repo
  test calling the new `DecodeFrameAt` would not compile against the pinned
  submodule.
- **A cost worth naming:** the clock is process-wide, not thread-local, because
  image loading is dispatched to the thread pool — a `[ThreadStatic]` value would
  be invisible to the code that reads it. Concurrent renders at *different*
  presentation times are therefore unsupported, which is the honest state of a
  stack that renders one document per process.

## View transitions do not capture the document — **still open; 2 will not be won here**

- **Tests:** problems 14–23, ten `css/css-view-transitions/*` tests.
- **Owner:** HtmlBridge (`DomBridge.ViewTransition.cs`).
- **The earlier reading of this bucket was wrong in a way worth recording.** It
  said "the pseudo tree is materialised, but nothing captured goes into it".
  Measuring each test individually shows the opposite: named elements' snapshots
  were already painting at the right size and colour — in `auto-name` ours
  carries the 1.3% green and 1.3% yellow item squares the reference has. The
  failures were four narrower gaps, and *three of the ten tests are not engine
  bugs at all* — 14 and 15 measure a feature the reference engine lacks, and 18's
  reference is blank. The ten break down as: 3 already passing locally (18, 20,
  22), 2 won't-fix (14, 15), 5 still open — 19, 21 and 23 on a rasterised root
  snapshot, 16 and 17 on nested browsing contexts.
- **What landed** — three narrow corrections, none of which closes a test on its
  own:
  1. **The root's captured name was hardcoded to `root`.** It is really whatever
     `view-transition-name` the document element carries; the UA sheet only
     supplies `root` as the default. `root-captured-as-different-tag` renames it
     to `another-root` and paints `::view-transition-group(root)` red *precisely
     to assert the `root` rules stop applying* — so our 100% red canvas was the
     test working as designed. `auto`/`match-element` on the document element
     resolve to `root` rather than a generated name.
  2. **`::view-transition-image-pair` was never materialised.** The spec puts it
     between a group and its old/new pair so one rule can address both;
     `old-content-captures-root` hides an entire group through it, and with no
     such box the rule had nowhere to land.
  3. **The pair box alone was not enough** — and this is the sort of thing only a
     real render catches. The snapshot content box bakes the captured element's
     computed style, so it re-asserted `visibility: visible` (the initial value
     nearly everything has) *over* the pair's inherited `hidden`. Only a
     non-initial `visibility` is carried now.
- **What did *not* work, and why it is worth knowing.** The root capture used to
  carry only a background colour, no content, so `::view-transition-old(root)` was
  transparent and the author backdrop showed through the page. Reproducing the
  page by **cloning the DOM** into the snapshot box was implemented, measured, and
  reverted. It did fix problems 19, 21 and 23 outright (0.0% → 100%), but across
  the 458 local `css-view-transitions` tests it was **+8 passing / −7 passing** —
  and it cost 79 pixel points on `root-to-shared-animation-end` (82.7% → 3.1%) and
  ~4 on `content-with-transform-old-/new-image`. Restricting the clone to the
  *old* snapshot did not rescue those. The reason is structural, not a missing
  detail: a DOM clone re-lays-out and is only *close*, while the transparent box
  let the **live page** show through — and the live page is pixel-exact. Anywhere
  the old root snapshot is genuinely visible, exact beats close.
- **What did work: gate the clone on whether the page can show through at all.**
  The two cases are distinguishable without a rasteriser. The live page can only
  stand in for the snapshot while nothing paints between them; once the author
  gives the bare `::view-transition` a background, that backdrop hides the page
  and a content-less snapshot has nothing left to fall back on — which is exactly
  when the viewport comes out a flat wash of the backdrop colour. So the root
  snapshot now clones **only when `::view-transition` paints a background**
  (`RootOverlayOccludesPage`), and every test that leaves the overlay transparent
  keeps the untouched, pixel-exact live-page path. That is why this does not
  reintroduce the −7: `root-to-shared-animation-end` and
  `content-with-transform-old-/new-image` set no `::view-transition` background,
  so the gate is false for them. It closes issue #1500 problems 13, 15 and 17
  (`new-content-captures-root`, `old-content-captures-root`,
  `root-captured-as-different-tag`), all three of which had rendered as a flat
  pink page. Two details the clone needs to be worth anything: it must skip
  `<head>`/`<style>`/`<script>`/`<link>` (re-inserting them duplicates author
  rules into the document and re-fetches resources) and it must **keep `id`
  attributes**, which the per-element snapshot path strips — a whole page of
  id-styled content otherwise reproduces as unstyled boxes. Keeping them is safe
  because the pseudo tree is materialised on a fresh render projection, so the
  duplicate ids never reach the tree page script observes.
- **Still open where the overlay is transparent.** The gated clone says nothing
  about the cases the live page already covers, and it is still a clone: **a root
  capture that is exact in both cases needs a rasterised snapshot from the
  renderer, which is a `Broiler.HTML` capability, not something the bridge can
  synthesise from the DOM.** That is what the original "capture the old and new
  snapshots as images" next action asked for, and it still stands.
- **A trap the attempt uncovered, worth keeping for whoever does the raster
  version.** The overlay serializes after `</body>` and the HTML parser
  foster-parents it back *inside* `<body>`, so a rule anchored on an ancestor
  outside the snapshot — `body.updated #box` — repaints the **old** snapshot with
  the **new** state the update callback just produced. Any DOM-shaped snapshot has
  to freeze its paint at capture time.
- **Problems 14 and 15 cannot be won by improving the engine.** Both
  `auto-name.html` and `auto-name-from-id-shadow.html` need
  `view-transition-name: auto`, and the reference Chromium **drops that
  declaration at parse time** — it computes to `none` (verified directly: the
  rule survives in the CSSOM with `view-transition-name` removed). Chromium
  therefore captures nothing, the transition finishes immediately, and its
  reference is the plain post-callback page: 97.5% white plus the two squares.
  Broiler implements `auto`, captures both items correctly, and paints the
  author's `rebeccapurple` backdrop over them. **Matching that reference would
  mean deleting working support** — the same shape as problem 24 and as
  `image-animation: paused`. Leave them failing.
- **Problems 16 and 17 are a different gap.** The two
  `iframe-and-main-frame-transition-*` tests drive
  `iframe.contentDocument.startViewTransition` — a transition in a nested
  browsing context, composited with the parent's. Ours renders 99.5% white
  against a 74.5% green + 25% blue reference, so the script never gets going.
  That belongs with problem 26 (framesets), not with compositing.
- **Problems 18, 20 and 22 already pass locally** at 100% (`compute-explicit-name-non-ancestor.tentative`,
  both `*-root-scrollbar-with-fixed-background`), where CI reported 0.0%. The two
  scrollbar tests are genuine — their reference is 99% `lightblue`, so both engines
  are drawing the same substantial content. The `.tentative` one is *not* worth
  trusting: its reference is 100% white, so the only way to "pass" is to render
  nothing, exactly like problem 24.
- **Verified:** 25 `ViewTransition*` tests pass — three new ones covering the
  renamed root and the image-pair hide *with its negative half* (the same group
  paints without the rule, so the test cannot be satisfied by a blank group). The
  three corrections were swept over all 458 local `css-view-transitions` tests to
  confirm they cost nothing.
- **Beware the flaky one.** `new-content-transform-change-001` scores 99.6% in one
  run and 1.0% in the next on an unmodified build. It appeared in a regression
  diff and was very nearly attributed to a change that had nothing to do with it;
  re-run a suspicious test against the unmodified build before believing a diff.
- **Exit gate for what remains:** a rasterised root snapshot composites at the
  group geometry so 19, 21 and 23 match without the clone's approximation; and a
  nested browsing context runs its own transition and composites into the parent
  (16, 17), with a focused test pinning an iframe's old root snapshot against the
  parent's.

## System colour keywords did not resolve at all — **fixed**

- **Test:** problem 28, `forced-colors-mode/forced-colors-mode-20.html`.
- **Owner:** Broiler.CSS.
- **Original evidence:** the test paints `body { background-color: Canvas }`.
  Broiler's canvas is 98% black; Chromium's is 98% white. Forced colors are not
  active in either engine (the reference is an ordinary Chromium render), so
  `Canvas` must resolve to the light palette's white.
- **Root cause:** not a dark palette, as first suspected — `CssSystemColors`
  carried only `Field` and `FieldText`. Every other system colour fell through to
  the named-colour lookup, which does not know them, and resolved to the
  unknown-colour fallback: black. That turned every system-colour test into a
  whole-canvas mismatch, and this one is only the worst-scoring member of the
  family.
- **What landed:** `patches/0036-…` (since applied, pointer bumped) fills in the
  CSS Color 4 §6 table from the
  light palette (matched to what Chromium reports, since the references are
  Chromium screenshots), maps the §6.2 deprecated keywords onto their aliases,
  and adds a `CssColorScheme` overload so a dark used colour scheme selects the
  dark palette — `Canvas` there is the same `rgb(18, 18, 18)` the canvas-background
  paint path already uses. `forced-colors` still computes to `none`; nothing
  emulates it. Both existing call sites (the renderer's colour hook in
  `Broiler.HTML`, and `CssAnimationResolver`) already went through `TryResolve`,
  so filling the table lit up both.
- **Verified:** rendering `background-color: Canvas` now paints 100% white and
  `ButtonFace` paints `rgb(239, 239, 239)` — a value only the new table produces,
  so the render path demonstrably goes through it. 45 focused tests cover the
  light palette, the `color-scheme: dark` switch, and the deprecated aliases.
- **Remaining:** the test itself is not in this container's WPT subset, so the
  pixel result is CI's to confirm. The patch has since been applied and the
  pointer bumped, so CI is now running this.

## `contrast-color()` and style container queries — **fixed**

- **Test:** problem 6, `css/css-color/contrast-color-style-query.html`.
- **Owner:** Broiler.CSS.
- **Original evidence:** Broiler renders 100% white where Chromium renders 100%
  green. The test needs three features to compose: an `@property`-registered
  `<color>` custom property, `contrast-color(#000)` resolving to an absolute
  colour, and `@container style(--contrast-color: white)` matching on it. Any one
  missing leaves the `background: green` rule unapplied.
- **Which of the three were missing:** registration already worked. The other two
  did not — and the earlier guess that the test would "distinguish the two once
  either lands" was wrong: it needs *both*, so neither alone moves the test.
- **What landed** (`patches/0039-…`):
  1. `contrast-color(<color>)` resolves to whichever of black or white contrasts
     more with its argument, via the WCAG 2 contrast ratio over relative
     luminance. The comparison collapses to one threshold — white wins below
     luminance `√(1.05 × 0.05) − 0.05 ≈ 0.1791`, where the two ratios are equal.
     **That is not mid-grey:** `#767676` takes black while `#757575` takes white,
     and a test pins exactly that boundary. Wired into
     `CssValueParser.TryParseColor`, so it is a `<color>` everywhere; the system
     colours are routed there in the same pass, since they are `<color>` keywords
     too.
  2. `style()` container queries, which were explicitly unsupported and forced
     the whole query false. A *style* container needs no `container-type` —
     css-contain-3 makes every element one — so size and style containers are now
     resolved separately and a style-only query works where no size container
     exists. Comparison is colour-aware, because a registered `<color>` property
     computes to an absolute colour: `white` and `rgb(255, 255, 255)` are the
     same computed value.
- **Two parsing traps this uncovered,** both of which made *every* style query
  silently false rather than erroring:
  - `SplitContainerName` read the leading identifier of `style(...)` as a
    container **name**, so the lookup hunted for `container-name: style`, found
    nothing, and bailed. An identifier immediately followed by `(` is a function.
  - The condition tokenizer split `style` from its parenthesised argument,
    leaving the argument looking like a nested condition and the name like a bare
    size feature. Function tokens now keep their argument list.
- **Verified:** the composed scenario renders red with the patch reverted and
  green with it applied. The negative half — `contrast-color(#fff)` is *black*,
  so the `white` query must **not** match — is what makes that meaningful; an
  early version of this check passed only because the query never matched at all.
- **Known gaps, deliberate:** only the custom-property form of `style()` is
  supported (a standard-property query returns false rather than guessing), and
  an `@property` registration with `inherits: false` is not honoured by the
  ancestor walk. The walk reads cascaded declarations rather than
  `GetComputedStyle` because it runs *during* style computation and re-entering
  it for an ancestor would recurse.
- **No main-repo fallback was possible:** the cascade lives entirely in
  Broiler.CSS, so unlike problems 25 and 27 there was no main-repo layer to carry
  an equivalent fix — this one was inert on CI until its patch was applied.
  `patches/0039` has since been applied and the pointer bumped, so CI now runs it
  and the pixel result is CI's to confirm.

## `<base href>` was ignored for `<link rel=stylesheet>` in the render path — **fixed**

- **Test:** problem 25,
  `html/semantics/document-metadata/the-link-element/stylesheet-with-base.html`.
- **Owner:** main repo (the renderer's stylesheet resolution).
- **Original evidence:** the test sets `<base href="resources/">` and links
  `stylesheet.css`, so only `resources/stylesheet.css` (green) may load — the
  sibling `stylesheet.css` next to the test sets red as the trap. Broiler renders
  100% red. It resolved the href against the document URL and loaded the trap
  file, which is exactly what the `<base>` is there to prevent.
- **Root cause:** the second site the previous entry predicted. `e4cc5e9` taught
  the DomBridge serialization transform to honour `<base>`, but the WPT runner's
  `InlineLinkedStylesheets` reads linked sheets off disk *before* those transforms
  run — resolving against the test's own directory. It inlined the trap as a
  `<style>`, so by the time `ApplyBaseHrefToStyleUrls` ran there was no `<link>`
  left to rebase.
- **What landed:** rather than a third implementation, `HtmlBaseHref`
  (`src/Broiler.HtmlBridge.Dom/HtmlBaseHref.cs`) is now the one seam both sites
  resolve through — it finds the document base (from raw HTML or the DOM) and
  resolves a URL against it, keeping the base's shape so downstream mapping still
  works: absolute base → absolute URL, root-relative base → root-relative path
  (the `wptRoot` handler still matches), document-relative base → a
  document-relative path when no page URL is known, which is what a caller
  holding a directory needs. `DomBridge.ResolveUrlAgainstBaseHref` delegates to
  it; the runner's inliner calls it before touching disk.
  - `@import` was the same bug one layer down: `InlineStyleSheetImports` resolved
    a relative import against `_pageUrl`, never the base. It now folds the base in
    via `HtmlBaseHref.ResolveDocumentBaseUrl`. `<style>` `url()` was already
    covered by `RewriteStyleElementUrls`.
- **Verified:** the trap scenario reproduced locally at 100% red before the change
  and renders 100% green after. Focused tests assert the trap file is never
  loaded (not merely outranked in the cascade), that a document with no `<base>`
  still picks the sibling, and that non-stylesheet `<link>`s are untouched; the
  pre-existing bridge-level test grew the trap file it had been missing.
- **Note for the next reader:** on a Unix host `Uri.TryCreate("/css/",
  UriKind.Absolute)` succeeds as the *file path* `file:///css/`. Base resolution
  must check for a scheme before treating a base as absolute or it silently drops
  the page's origin — the helper does, and a test pins it.

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
    does not resolve, so the box has no size. **The unit gap is fixed** — see
    below.
  - `monolithic-overflow-011`: Broiler renders 99% white against 95% yellow + 5%
    hotpink — the `display: table` subtree with a `contain: size; height: 350vh`
    row-group paints nothing.
  - `page-margin-002`: Broiler is 100% yellow, Chromium 100% white, with
    `writing-mode: vertical-rl` on the root and three `100vw × 100vh` blocks.
    The two engines disagree about where a vertical-rl root's initial scroll
    position is. **Unconfirmed** — verify the scroll origin before treating this
    as a paint bug.
- **Next actions:**
  1. ~~Resolve the logical viewport units (`vb`, `vi`, and their `sv`/`lv`/`dv`
     variants) against the writing mode.~~ **Done** — `patches/0036-…` and
     `patches/0037-…`, both since applied and their pointers bumped, so this is
     live on CI. `vb`/`vi` did not parse at all. They now resolve against
     the *root element's* writing mode, which is what CSS Values 4 §6.1.4
     specifies (not the element the unit appears on), so a per-pass factor set
     from the root's mode is the right granularity; `Broiler.HTML`'s layout pass
     hands that mode to the parser alongside the viewport size. The
     small/large/dynamic variants coincide with the default viewport in a
     headless render with no retractable UA chrome, so they canonicalise onto it.
     Verified end-to-end: `100vi × 100vb` fills the viewport, a `vertical-rl`
     root swaps the axes, and `100dvw × 50svh` covers half the canvas.
  2. ~~Lay out and paint a `contain: size` box inside table internals.~~
     **Half done, and the diagnosis was wrong.** `contain: size` is not involved:
     a `contain: size` box paints fine on its own. Two separate faults sit behind
     `monolithic-overflow-011`, found by bisecting the test down to
     `<table style="background: yellow">`:
     - **A table never painted its own background or borders at all** — CSS2.1
       §17.5.1 layer 1. The six-layer model covers a table's *internals*, but the
       painter handed the whole table to that pass (which starts at layer 2) while
       the background phase skipped `display: table` children outright and the
       foreground phase suppresses block backgrounds. Nobody emitted layer 1.
       Diagnosed at the source, not from pixels: the fragment has correct bounds
       and a computed `background-color` of yellow and still emits no fill.
       **Fixed** — `patches/0045`, with `TableBackgroundPaintTests` as the landed
       check. This is ordinary markup, not a paged-media edge case.
     - **Still open:** a block child of a `display: table-row-group` gets no box.
       The row-group measures 0×0 and its child computes `display: inline`, so the
       hotpink rectangle has nowhere to paint and the table is only as tall as its
       stray text. That is table fixup — a block inside a row group needs wrapping
       in an anonymous table-cell — and it is what still holds the test at 2.26%.
  3. ~~Establish what a vertical-rl root's initial scroll position is and align the
     canvas extent with it.~~ **Done, and `page-margin-002` must not be "fixed":
     the reference is a screenshot artifact.** The two engines do *not* disagree
     about layout. Asked directly under Playwright, Chromium puts the yellow
     `.fullpager` at exactly `(0, 0, 1024, 768)` — filling the viewport — with
     cyan at `x: -1024`, pink at `x: -2048`, `scrollLeft: 0` and
     `scrollWidth: 3072`. That is what Broiler renders. But Chromium's own
     **viewport** screenshot of that same page is 100% white, while its
     **full-page** screenshot (3072×768) paints all three blocks, yellow at the
     right. So the blank reference is an artifact of screenshotting a `vertical-rl`
     root, not evidence about rendering, and matching it would mean drawing
     nothing. Same category as problems 14, 15, 18, 24 and 25.
- **Exit gate:** all three tests match on screen, with focused tests for logical
  viewport units and for `contain: size` in table internals — and no paged-media
  work is required to get there. The viewport-unit half of that gate is met (a
  focused suite pins both axes, both writing modes, and all four viewport sizes);
  `page-box-008` itself is CI's to confirm, since it is not in this container's
  WPT subset.
- **Trap this uncovered:** canonicalising `svmin` → `vmin` means the unit *as
  written* can be longer than the unit reported. Three call sites split
  number-from-unit by the canonical length, so `"100svmin"` parsed its number as
  `"100s"` and silently resolved to 0. `GetUnit` now also reports the written
  length; any new site that splits a length must use it.

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

## `Node.moveBefore` was missing — **fixed**

- **Test:** problem 27, `dom/nodes/moveBefore/preserve-render-blocking-style.html`.
- **Owner:** Broiler.DOM with HtmlBridge for the binding.
- **Original evidence:** Broiler renders white where Chromium renders 100% green.
  The test moves a render-blocking `<style>` with `moveBefore()` and asserts the
  styles survive the move; without the method the script throws and the document
  is never styled.
- **What landed, in two pieces:**
  1. `patches/0038-…` adds the canonical `DomNode.MoveBefore` — the genuinely
     atomic version. The state it preserves follows from one spec constraint: both
     parents must share a shadow-including root, so a moved node's *connectedness
     cannot change*. That is why the document's id index is deliberately not torn
     down and rebuilt, and why an `<iframe>` must not reload. Observers still see
     the move (records are queued for both parents); only the disconnection is
     skipped.
  2. The bridge binding in the main repo, which is what CI runs. It exposes
     `moveBefore` on the element surface. It originally reproduced the observable
     behaviour on the primitives available at the pinned submodule SHA — a
     reposition that skipped the sub-document onload firing, not fully atomic
     (the node was briefly detached, so the id index churned). **0038 has since
     been applied and the pointer bumped**, so that interim body is gone:
     `DomBridge.MoveNodeBefore` now delegates the move to `parent.MoveBefore` and
     keeps only what is genuinely the bridge's — marshalling the canonical
     `DomException` into a JavaScript `DOMException`, and invalidating the style
     scopes the reposition dirtied. The duplicated validity check is deleted.
- **Verified:** rendering the move scenario paints green where it painted white;
  moving an orphan throws as the spec requires; a within-parent forward reorder
  lands in the right slot. 16 DOM-level tests (in the patch) and 10 bridge-level
  tests (on CI) cover moves within and across parents, the render-blocking
  `<style>` case, the observer records, and every pre-move validity rejection.
- **Why validity is stricter than `insertBefore`:** `moveBefore` rejects a node
  that is not already in the tree, and one from a different root. Both would be
  silently accepted by an insert; a caller relying on the atomic guarantee needs
  the exception instead of insert-shaped behaviour.
- **Remaining:** the WPT test is not in this container's subset, so its pixel
  result is CI's to confirm. CI now runs the atomic version; the 10 bridge-level
  tests were re-run against it unchanged, which is the evidence that the
  delegation preserved the binding's observable behaviour.

## A `<template>`'s styles leaked into the page — **fixed, pending patch**

- **Test:** problem 29, `shadow-dom/focus-navigation/delegatesFocus-highlight-sibling.html`.
- **Owner:** Broiler.HTML (the stylesheet walk), then HtmlBridge for what is left.
- **It was never about focus.** The old next action here was "establish what
  Broiler is painting grey before touching focus delegation", and that was the
  right instinct: the answer had nothing to do with focus, delegation, or control
  chrome. A `<style>` inside a `<template>` was being collected into the
  **document** cascade. HTML §4.12.3 keeps a template's children in a separate
  fragment as *template contents* — inert until stamped out. The test keeps its
  component styles in a template, as components normally do, so
  `:host { background-color: #aaa }` and `:host(:focus) { background-color: #ccc }`
  leaked into the page, matched it, and painted 99% of the canvas `#ccc`.
- **How it was narrowed,** since the signature pointed the wrong way. Bisecting
  the three ways to get a style into a shadow root separates the causes cleanly:
  a plain host populated by `innerHTML` renders correctly (2.8% `#aaa`); the same
  rules delivered by `<template>` + `importNode` fill the viewport (99.7%); and a
  custom element populated by `innerHTML` renders nothing. Removing the
  `:host(:focus)` rule still filled the viewport, which ruled the focus rule out.
  The serialized DOM then settled it: the shadow root was **empty** while the page
  rendered grey, so the style could only be coming from the template. A
  three-line document-level case confirms it with no shadow DOM at all — a
  `.probe` rule inside a template repaints a `div` outside it.
- **What landed:** `patches/0042-…` stops `DomParser.CascadeParseStyles` at a
  `<template>` box. Narrow by design — template *contents* already produce no
  boxes and correctly do not render; only the stylesheet walk descended into
  them.
- **Verified:** the test goes from **0.0% to 98.2%**. Four regression tests cover
  the leak, its nested form, the negative half (a sheet *next to* a template must
  still apply), and that template contents do not render. They probe the pinned
  submodule and skip when the patch is unapplied, so they become live guards the
  moment it lands.
- **This is not specific to problem 29.** Any component that keeps its styles in
  a template — the ordinary way to write one — has been leaking them into the
  page, so the blast radius is wider than one test.
- **What is left is a different gap: custom elements.** The remaining 1.8% is the
  shadow content, and chasing it uncovered that the runner's `customElements`
  shim never worked at all. Four things were wrong, three now fixed in the main
  repo (so they are on CI immediately):
  1. **The DOM globals were unreachable by bare name.** The bridge registers
     `HTMLElement` and the shim registers `customElements` on `window`, but a bare
     identifier does not resolve through `window` the way it does in a browser, so
     `class extends HTMLElement` threw *"HTMLElement is not defined"* and
     `customElements.define(...)` threw before any component could build itself.
     Both are now aliased into the global scope when — and only when — the bare
     name is missing.
  2. **The upgrade threw on any element with attributes.** It read
     `element.attributes[i].name`, but the bridge's `attributes` reports a length
     without answering to numeric indexing, so the read hit `undefined` and took
     the page's whole script with it. It uses `getAttributeNames()` now.
  3. **`connectedCallback` was never called.** The shim constructed and replaced
     the element but ran no reaction, and the upgraded element did not carry its
     class's prototype either. The reactions are copied onto the element and the
     connected one is invoked for elements already in the document — which is
     where a component builds its shadow root.
  4. **`template.content` and `document.importNode` did not exist.** `t.content`
     was `undefined` and `importNode` was not a function, so the
     `importNode(template.content, true)` idiom every one of these components uses
     yielded nothing. Both are implemented in HtmlBridge now — a real DOM gap
     rather than a harness one. One deviation is deliberate and pinned by a test:
     the spec has the parser move a template's children into the content fragment,
     leaving the element childless, whereas Broiler's parser keeps them as
     children so a template round-trips through serialization, so `content` is a
     stable *snapshot copy* of them. Stamping, querying and
     populating-before-stamping all behave; reading one side after mutating the
     other does not. Nothing renders either way — template contents are inert.
- **And that made the test's score go down, which is the useful part.** With all
  four fixes it reads **90.5%**, against 98.2% for the template patch alone.
  Nothing regressed: the shadow content simply renders now, and it renders at the
  wrong size, where before it was absent. Ours is 7.8% `#aaa` against the
  reference's 0.1%. **Found and fixed, and it was not a shadow bug at all.** The
  min/max-content passes (`GetMinMaxSumWords`, `GetMinimumWidth_LongestWord` in
  Broiler.Layout) walked every child collecting words with no `display:none`
  guard — while the shrink-to-fit *height* paths beside them had one. So the
  UA-hidden elements that carry text (`<style>`, `<script>`, `<title>`) were
  measured, and their **source text set the width of any shrink-to-fit
  ancestor**. Every shadow host is such a box holding its component's
  `<style>`, which is why it surfaced here, but a plain
  `<div style="display:inline-block">` holding one `<li>` and a stylesheet
  measured 861px wide against 65px without it. A `display:none` box generates no
  boxes at all (CSS 2.1 §9.2.4), so both passes now skip it. Problem 29 goes
  **90.5% → 95.7%** and the hosts drop from 7.8% of the canvas to 1.7%.
- **Two wrong turns on the way, recorded so they are not repeated.** First, the
  cause was ascribed to the CSS *text length*, then "refuted" by a pure-comment
  stylesheet that left the host at 1008×19 — but that case had no `:host` rule,
  so the host was full-width and the comment fit on one line; the test did not
  discriminate, and the refutation was wrong. Holding `:host` fixed and varying
  only inert text settles it: a 600-character comment takes the box from 468px to
  6014px. Second, `getBoundingClientRect` is the bridge's own measurement taken
  while scripts run, *before* the shadow style is projected, so it reported some
  cases as unchanged when the render had in fact improved. Measure the render.
- **A note on where these live.** Items 1–3 are in the runner's browser-API shim,
  which exists only because the bridge implements no custom elements. That is the
  honest fix for a harness bug — the component's own code runs and builds a real
  shadow root — but the durable answer is `customElements` in HtmlBridge proper.
- **A third general bug, also found and fixed here: a collapsible space between
  inline-block siblings counted as zero.** A space between siblings is normally
  carried as a flag on an adjacent *word*, but between two inline-blocks the
  neighbours live in other boxes, so the space is a text box of its own whose
  words collapsing clears — and the intrinsic pass measured nothing. The
  shrink-to-fit container then came out exactly one space too narrow and its last
  child wrapped: two 10px inline-blocks measured 20px and **stacked**, the second
  at `y=34`, where 24px would have put them side by side. Give the same row
  `width: 200px` and they sat on one line all along, which is what showed the
  line-breaking was right and the width was wrong. `GetMinMaxSumWords` now counts
  a collapsed whitespace separator as one space advance; preserved whitespace
  (`pre`, `pre-wrap`, `pre-line`) keeps its words and takes the normal path.
  The row now measures 25px — matching `&nbsp;` and plain text, which always
  counted the space and are the cross-check for what the collapsible case should
  have been. Problem 29 goes **95.7% → 97.8%**. Reproducible with no shadow DOM,
  template or custom element in sight, which is why it is worth fixing on its own
  account:

  ```html
  <style>
    .row { display: inline-block; background-color: #aaa; }
    .row span { display: inline-block; background-color: #eee; }
  </style>
  <div class="row"><span>Item One</span> <span>Item Two</span> <span>Item Three</span></div>
  ```
- **A fourth bug found here was fixed and then reverted, which is the useful
  record.** An auto-height inline-block ignored `line-height`: its height came
  from the glyphs, so `line-height: 10px` around a 32px font measured **39px**
  where every browser gives 10, and the ordinary 16px case measured 22px against
  Chromium's 18. A *block* with the same content already honoured `line-height`,
  so the two paths disagreed with each other as well as with the reference.
  Clamping a single-line auto-height inline-block to its line box fixed every
  direct measurement — 10px, 16px, 24px and 40px line-heights all matched
  Chromium exactly — **and regressed WPT
  `css-anchor-position/position-area-scrolling-002` to 90.6%**, content shifted
  left 30px and up 19px. Bisected to the clamp rather than assumed: reverting the
  `normal` change alone left the failure, reverting the clamp restored the test.
  So it is out. The diagnosis stands and is worth keeping — an inline-block's
  height really should be bounded by its line box — but the naive clamp is not the
  shape of the fix, and whatever replaces it has to keep that anchor-positioning
  test green.
- **A fifth attempt, also measured and also reverted: flooring
  `line-height: normal`.** The reference builds `normal` from integer ascent and
  descent, so the sum lands a whole pixel below the fractional height measured
  here, and flooring instead of rounding up matches Chromium on **12 of 19** font
  sizes swept from 8px to 48px where rounding up matches **6** — it would fix 16px
  (18 not 19), 24px (27 not 28) and 32px (37 not 38). Over the WPT suite it
  nonetheless *lost*: `css-values` `lh` unit and `css-overflow`
  clip-border-box-with-size regressed while `css-align` safe-justify-self-vrl
  recovered — a net −1, reproduced by running each test on both builds. **Whole-page
  rendering is the authority, not a single metric compared in isolation**, which is
  the lesson worth carrying: a sweep against one number said 12 > 6 and was
  measuring the wrong thing. Closing the gap properly needs real per-size
  ascent/descent from the font backend rather than a rounding mode over this one
  value — the layout layer approximates the baseline with a hardcoded 0.8 ratio and
  has no ascent/descent to work from.
- **Exit gate:** a line box holding nested inline-blocks sizes to the reference
  (the rows are 76px against 54px), and only then the focus question — a focused
  test for `delegatesFocus` moving focus to the first focusable shadow descendant.

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

## Runner: a `manual/` test was being scored — **fixed**

- **Test:** problem 24,
  `html/canvas/element/manual/draw-element-image/dialog-paints-in-top-layer.tentative.html`.
- **Owner:** the WPT runner (`src/Broiler.Wpt`).
- **Original evidence:** the test sits under a `manual/` path segment and is
  `.tentative`, but it is discovered and scored as a Regular test. Its reference
  is a 100% white Chromium canvas, because Chromium does not implement the
  proposed `draw-element-image` API either — so the only way to "pass" is to
  render nothing. Broiler paints a dialog (98% `#e5e5e5` + 2% green), which is
  arguably the more useful behaviour.
- **What landed:** `WptTestRunner.IsManualTest` now treats a `manual/` directory
  segment as the manual signal, alongside the `-manual` filename suffix —
  mirroring how `IsCrashTest` already accepts `/crashtests/` and `IsTentativeTest`
  accepts `/tentative/`. `ClassifyTestKind` checks Manual before Tentative, so
  such a test lands in the Manual bucket and leaves the scored set.
- **Verified:** focused tests cover the segment on both separators and
  case-insensitively, pin that `manual` only counts as a *whole* segment
  (`manually/`, `semi-manual/` stay automated), and assert the `manual/` +
  `.tentative` test classifies as Manual.
- **Remaining:** the count change is CI's to report — the run's Regular count
  should drop by exactly the number reclassified, and that drop is not a
  regression.

## The next run (issue #1497, 2026-07-30)

Everything above is scoped to
[issue #1491](https://github.com/Broiler-Platform/Broiler/issues/1491). The next
run reported the same shape of tail, and **most of it is the same tests under new
numbers** — so this section records only what is new, and what the re-run
confirmed or contradicted. Cross-referencing the two lists:

- **#1497 problems 1 and 2 are one bug, and it was the run's largest.** Both crash
  signatures — `Worker closed stdout before returning a result` (68 tests) and
  `Worker exited with code 134` (3 tests) — were a single unbounded recursion in
  `@container` prelude evaluation. Every `(` was read as the start of a nested
  condition, so a prelude whose parentheses belong to a *value* function
  (`(width = calc(100px + 10rem))`) or to a *query* function other than `style()`
  (`anchored(fallback: --foo)`, `scroll-state(scrollable: block-end)`) handed the
  tokenizer the identical text at every level. A .NET stack overflow cannot be
  caught, so it killed the worker outright — which is why the reported signature
  named the runner rather than the CSS engine. Measured locally over the 302
  `container-queries` tests in both affected directories: **68 crashes → 0**, no
  test regressed. Fixed in `Broiler.CSS`; **[`patches/0043`](../patches/README.md)**, with no
  main-repo fallback. The WPT runner applies it for the run via
  `scripts/apply-pending-wpt-patches.sh`, so CI's numbers reflect the fix; the
  pinned pointer still carries the crash until a maintainer lands it.
- **#1497 problems 3 and 4 are #1491's problems 2 and 3** — the per-element JS
  wrapper cost, still tracked in [the root roadmap](ROADMAP.md#htmlbridge-runtime).
- **#1497 problem 24 was recorded here as fixed, and was not.**
  `dom/nodes/moveBefore/preserve-render-blocking-style` is listed in the table
  below as "**fixed** — patch 0038 applied", but the re-run still had it at 0.0%,
  and so did this container. `Node.moveBefore` was only ever half the test. The
  real gap: **`<link>` had no IDL reflectors at all**, so the ordinary way to
  inject a stylesheet —
  `createElement("link")`, set `.rel` and `.href`, append — wrote *nothing*, and
  the element serialized as a bare `<link>` that never reached the cascade.
  `setAttribute("rel", …)` worked, which is how it stayed hidden. `<link>`/`<base>`
  now reflect `href` as a URL like `<a>`/`<area>`, and `<link>` reflects `rel`,
  `as`, `media`, `hreflang`, `integrity` and `referrerPolicy`. **0.0% → 100%**, in
  the main repo, so it is on CI immediately. The `?pipe=trickle(d1)` query and the
  `moveBefore` call in that test were both red herrings — a static `<link>` with
  the same query already rendered correctly.
- **#1497 problem 30 is new and is two bugs in one spec rule.**
  `css/css-transforms/transform-scale-percent-001` uses
  `transform: scale(50%, 75%)`, and css-transforms-2 makes a scale factor
  `<number> | <percentage>` where the percentage is *the ratio* — `scale(50%)` is
  `scale(0.5)`. The paint parser resolved every percentage against the element's
  box (right for `translate`, wrong for `scale`), so a 100px square came out 50×
  and filled the canvas; the bridge's geometry parser had no percentage branch for
  scale at all, so `50%` fell back to `0` and collapsed the box. **0.5% → 99.99%**,
  and the 939-test `css-transforms` subset goes 376 → 377 passing. Geometry is on
  CI now; the pixels need **[`patches/0044`](../patches/README.md)**.
- **#1497 problem 28 was an asymmetry between two code paths that model the same rule.**
  `hidePopover()` has honoured `transition: overlay allow-discrete` since the
  popover work — a popover hidden mid-transition stays in the top layer, so a
  static render still paints it and its `::backdrop`. `close()` did not, and tore
  a modal dialog down unconditionally, so `overlay-transition-dialog` rendered a
  blank canvas against a reference showing the dialog over its backdrop. `close()`
  now applies the same rule, in the two halves the spec actually has: `overlay`
  keeps the top-layer flag (so the backdrop paints), and `display` keeps the
  `open` attribute (because the UA sheet's `dialog:not([open]) { display: none }`
  is what decides whether a box is generated at all). A dialog transitioning
  `overlay` alone stays in the top layer but generates no box — a third test pins
  that, so the two halves cannot be collapsed into one flag. **0.4% → 99.89%**,
  and the 382-test `css-position` subset goes 241 → 242 passing. Main repo.
- **#1497 problem 25 must not be "fixed" — the reference is the unfeatured render.**
  `color-scheme-iframe-background-mismatch-dynamic` asserts that a same-origin
  frame with `color-scheme: light` gets an *opaque* background when its parent
  switches to dark. Ours renders the frame light; CI and this container both have
  Chromium rendering it `#121212`. Settled by rendering the test's own
  `<link rel=match>` target, `support/light-frame-scrolling.html`, in the same
  Chromium: it comes out **white**, while Chromium's render of the *test* is dark
  — so **Chromium fails this reftest against its own reference**, and its
  screenshot is evidence that it does not implement the rule. Ours matches the
  `rel=match` reference. Same shape as problems 14, 15, 18 and 24 above: closing
  it would mean deleting working support. **Leave it failing.**
- **#1497 problem 16 (`css-view-transitions/names-are-tree-scoped`): the 100% red
  canvas was page selectors leaking into the pseudo tree — fixed, 0% → 96.19%.**
  The pseudo tree is materialised as real `<div>`s, so the test's page-level
  `div { background: red }` matched every box in it, including the viewport-sized
  overlay root that paints at z-index 2147483646. Each box now re-asserts a
  transparent background beneath its own base style and the author's
  `::view-transition*` declarations.
  - **The interesting part is why the obvious version of this is wrong.** The
    reset is written as longhands and an author writes the `background`
    shorthand, so the two land on different keys of the inline-style dict and the
    longhands win by coming later — silently cancelling
    `::view-transition { background: lightpink }`. Layering them cost **341 → 264**
    passing across the 490-test subset, with individual tests falling from 100% to
    1%; narrowing the reset to backgrounds alone changed nothing (263), which is
    what identified the shorthand collision rather than the extra properties. The
    reset has to stand aside entirely when the author paints the box.
  - **Net: 341 → 341 passing**, one genuine gain
    (`shadow-part-with-name-overridden-by-important`, 1.3% → 100%) against one
    apparent loss that is `new-content-transform-change-001` — the flaky test
    warned about above, which scores 1.03% on three consecutive runs of the
    *unmodified* build in isolation, identically to the patched one. Worth
    repeating as a method note: that test appeared in a regression diff for the
    second time and was again not the change's fault.
  - **What still keeps the test failing** is separate and unfixed: the captured
    elements go on painting in place, so ours shows the three light-tree red boxes
    where the reference shows only the shadow-scoped green snapshot. Per spec a
    captured element is replaced by its snapshot, not drawn alongside it. Tree
    scoping proper — that a document-scoped `::view-transition-old(*)` rule must
    not reach a shadow-scoped name — is still untested here, because it sits
    behind that.
- **`css-view-transitions/html-becomes-fixed` (problem 29) does not reproduce
  here** — 99.99% locally against a locally generated reference, where CI reports
  0.4%. Judge it from a CI artifact, not from this container.
- **Still unanalysed:** `css-view-transitions/new-content-flat-transform-ancestor`
  (0.3% locally, so it does reproduce).
- **`uievents/…/UIEvent.load.stylesheet` (problem 26) was two gaps, and closing
  both still does not close the test.** It sets `link.href` from `window.onload`
  and waits for a `load` event on the `<link>` whose `currentTarget` is the link.
  The reflection fix above got the href to the attribute; the second gap was that
  **nothing dispatched a stylesheet link's `load` event at all**. It does now —
  once per href, only for a link in the document, and `error` rather than `load`
  when the fetch fails, decided by the same CSP gate and fetch the cascade uses
  (which needs the href resolved against the page URL, since the loader only
  accepts absolute URLs — skipping that dispatched `error` for every relative href
  while the sheet applied fine). The test renders `PASS` where it rendered `FAIL`.
  **Its pixel score barely moves — 97.88% → 97.87% — and it stays under the 99%
  threshold either way**, because the rest of the difference is bold text.
- **Neither `font-weight` nor `font-style` changes the rendered face — this is an
  engine gap, not a container font problem, and it caps every text comparison
  whose reference contains bold or italic.** Measured rather than eyeballed:
  rendering `HHHH` at 60px as normal, `bold` and `italic` gives **byte-identical
  ink (2384 px) and identical advance span (170 px)** for all three, while
  `monospace` differs (1920 / 134) — so family affects *layout advances* and
  nothing affects the *face*.

  The chain is intact right up to the last step. `font-weight` reaches
  `DrawTextItem.FontWeight`
  ([`PaintWalker.Text.cs`](../Broiler.HTML/Source/Broiler.HTML.Orchestration/IR/PaintWalker.Text.cs)),
  and `StubImageAdapter.CreateFontInt(family, size, style)` passes the style into
  `ResolveTypeface`. But
  [`TrueTypeTypefaceResolver.ResolveTypeface(family, style)`](../Broiler.HTML/Source/Broiler.HTML.Image.Compat/Text/TrueTypeText.cs)
  — the resolver `StubCompatProvider` actually installs — **ignores its `style`
  parameter entirely** and looks the family up by name alone. The raster backend
  then draws from `item.FontHandle`, never from `item.FontWeight`, so the weight
  that survived the whole pipeline is dropped at the last hop.

  Underneath that, the HTML image backend has **no system-font enumeration at
  all**: `BroilerFontRegistry` records only fonts registered at runtime, so in the
  WPT path the "available families" set is empty, every generic-family mapping
  resolves to nothing, and *every* family falls through to one bundled resource,
  `Vazirmatn-Regular.ttf`. One regular face draws the entire suite.

  Note this is specific to the HTML render path. `Broiler.Graphics` has the other
  half already: `FallbackSystemFont` discovers a system regular+bold **pair**
  (`DejaVuSans.ttf` + `DejaVuSans-Bold.ttf`, both present here) and
  `BImageRenderer` selects between them on `run.Font.Weight >= BFontWeight.Bold`.
  Its `BoldPath` has **no consumer outside its own file** — that path serves the
  UI/Graphics backend, not `HtmlRender`.

  **Not attempted here, deliberately.** Closing it means giving the image backend
  real system-font enumeration and `(family, weight, style)` matching — a feature,
  in a submodule this session cannot push to, that would change the face of every
  text render in the suite and so needs its own before/after sweep. Synthesising
  bold from the regular outlines would be cheaper but would not match a real bold
  face, so it would not carry a test over the 99% threshold anyway, and could
  regress tests that pass today. Worth doing; not worth doing blind.

## The next run (issue #1538, 2026-08-05)

20 048 failures, no incomplete shards. Cross-referencing the list against the two
runs above: **nine of the thirty are re-reports** under new numbers — problems 1,
2, 3, 4, 9 and 10 are #1491's problems 4, 5, 12, 13, 26 and #1497's problem 25,
and problems 5, 6, 7 and 18 are #1491's 16/17, 18 and 14/15. **The other
twenty-one are new to this list.** As above, this section records only what is
new, what landed, and what the measurements said — including where they
contradict an earlier verdict.

Every local number below is this container measured against a locally generated
Chromium reference, over a sparse WPT checkout of the directories the list names.
**Twenty-two of the thirty reproduce locally to within 0.1 of CI** — every one
that was measured except two — which is what makes the diagnoses in this section
worth writing down; those two are called out individually. (Problem 17 joined that
count late: it appeared not to reproduce until its reference was regenerated.)

### Shadow trees leaked their styles into the whole page — problems 16 and 20, **fixed**

- **Tests:** `css/css-shadow/shadow-directionality-001.tentative` and `-002`, at
  1.3% and 1.1% on CI and reproduced here to the decimal.
- **Owner:** HtmlBridge (`DomBridge/ShadowTreeSelectors.cs`), with a `Broiler.CSS`
  half in [`patches/0102`](../patches/README.md).
- **Two independent bugs, and the pixels only move when both are understood.**
  A shadow root's `<style>` is serialized inline into the render document, so the
  renderer sees its rules as ordinary global rules with no provenance — a shadow
  tree's `div { background: red }` repainted **every** `div` in the page. On top
  of that, `:dir()` sat in `CssSelectorMatcher`'s `RecognizedPseudoClasses` with
  no arm in the pseudo-class switch, so it fell through to the deliberately
  lenient default for recognised-but-unmodelled names and matched *every* element
  — `:dir(ltr)` and `:dir(rtl)` at once. Together they turned each of these
  tests' four small shadow rules into one canvas-wide repaint.
- **What landed, in two halves.**
  1. **Main repo — `ScopeShadowTreeSelectors`.** It reuses the shape
     `ScopeShadowHostSelectors` already established for the mirror-image `:host`
     problem: stamp every element of the tree with
     `data-broiler-shadow-scope="N"` (the host is deliberately *not* stamped — it
     belongs to the outer tree and is reachable only through `:host`), then append
     `[data-broiler-shadow-scope="N"]` to each selector's subject compound. It
     runs *before* the `:host` pass, which rewrites the keyword this one keys off.
  2. **`patches/0102` (`Broiler.CSS`).** `:dir()` now resolves HTML's
     directionality: nearest ancestor-or-self with a valid `dir`, `ltr` at the
     root, `dir=auto` (and `<bdi>`, whose default it is) from the first strong
     directional character. Strictly a narrowing, so it can only remove matches
     the lenient default invented. **The push was refused (403), as
     `CLAUDE.md` describes**, so it is a patch — but it is listed in
     `scripts/apply-pending-wpt-patches.sh`, so the WPT run exercises it on CI.
- **The subject compound, not every compound — and that is a deliberate choice.**
  Scoping the subject is what stops the leak: a rule whose subject is outside the
  tree cannot apply. Scoping *every* compound is closer to the spec (it would also
  require each ancestor in a descendant selector to be in the same tree) but adds
  one attribute selector per compound, so it changes specificity **unevenly**
  between rules of the same sheet — `div span` (0,0,2) and `.foo` (0,1,0) swap
  cascade order once they become `div[s] span[s]` (0,2,2) and `.foo[s]` (0,2,0).
  Subject-only adds exactly (0,1,0) to every rule, so their order relative to each
  other is preserved exactly, and the sheet as a whole outranks page rules
  reaching into the tree — the correct direction, since per spec those should not
  match at all.
- **A serialization pass is not free, and the suite says so.** Both this pass and
  the `animate({pseudoElement})` one added later walk the whole document, and
  running them unconditionally pushed
  `RunTestWithTimeout_GridTemplateColumnsCrash_Completes_Without_Timing_Out` — a
  **6-second** budget on a `grid-template-columns` with five million tracks — over
  its limit. It only failed in a full-suite run, never in isolation, which is
  exactly the shape that reads as flakiness; the control that settled it was
  running the *same full suite* against unmodified `origin/main` sources, where the
  test passes. Both passes now early-out on a flag (`_hasShadowRoots`,
  `_hasAnimatedPseudoStyles`), so a document with no shadow DOM and no pseudo
  animation pays nothing. Suite back to its 56 pre-existing failures, and
  `css/css-shadow` 157/207 and `css/css-pseudo` 237/358 are unchanged by the guard.
- **Compounds left alone, each for a reason:** `:host`/`:host-context` (the host
  is not in the tree), and `::slotted`/`::part` (their subject is a light-DOM
  node). `@keyframes`, `@font-face` and friends are copied verbatim — their blocks
  hold keyframe selectors and descriptors, not selectors — while the conditional
  group rules (`@media`, `@supports`, `@container`, `@layer`, `@scope`,
  `@starting-style`) are recursed into, because those do hold style rules.
- **Measured.** The two tests go **1.1% / 1.3% → 99.55%**, and across the 207-test
  `css/css-shadow` subset **153 → 157 passing with nothing lost** — the other two
  gains being `css-scoping-shadow-with-rules-no-style-leak` (98.1% → 99.2%, the
  test named for exactly this bug) and `host-specificity-003` (88.0% → 99.6%).
  Ten more tests moved up without crossing the threshold. Checked for regressions
  over **1 669 further tests** in `css-view-transitions`, `css-masking`,
  `css-position` and `css-pseudo`: **345/490, 222/439, 238/382 and 236/358 both
  before and after, with no test changing state in either direction.**
- **The one test that moved down** is `css-view-transitions/names-are-tree-scoped`,
  96.12% → 94.84% — failing on both sides, and already recorded in
  [the #1497 section](#the-next-run-issue-1497-2026-07-30) as blocked on captured
  elements still painting in place. Its shadow rules now correctly stop reaching
  light-tree boxes, which is why the number moves; the test cannot pass until that
  separate gap closes.
- **The main-repo half carries both tests on its own.** Measured with the patch
  reverted, `css/css-shadow` is the same 157 — with `:dir()` still matching
  everything, all four shadow rules happen to apply anyway. The patch is what makes
  them pass *for the right reason*, and it is why the fix is not resting on that
  coincidence.

### An `overlay` entry transition that never finished — problem 21, **fixed**

- **Test:** `css/css-position/overlay/overlay-transition-finished`, 1.8% on CI and
  1.81% here.
- **Owner:** HtmlBridge (`DomBridge/AnchorResolver/Dialogs.cs`). Main repo, so it
  is on CI immediately.
- **The CSSOM answer and the painted answer are taken at different instants, and
  conflating them made the test unwinnable.** It reads
  `getComputedStyle(el).overlay` synchronously after `showPopover()` and paints
  itself pink unless it sees `none` — the transition must be observed *running* at
  script time — then screenshots from `transitionend`, by which point the popover
  must be in the top layer covering a fixed red div.
  `PopoverHeldOutByOverlayTransitionIn` answered "held out" for both, because it
  returns true whenever an element merely *declares* a discrete `overlay`
  transition. Our render was 98.2% red with an 8px green band: the popover painted
  beneath the fixed div, which is "held out" exactly.
- **What landed.** `ComputeOverlayValue` keeps answering for t≈0; only the two
  paint sites move, through `PopoverHeldOutOfTopLayerForPaint`. The renderer has no
  clock, so "which instant" is read from what the page says — the same thing the
  runner already does for `takeScreenshotDelayed(N)` via
  `WptTestRunner.ScreenshotPresentationTime`. A test that gates its screenshot on
  `transitionend` is making that statement without a number, and
  `ScreenshotWaitsForTransitionEnd` recognises the shape: the document is still
  `reftest-wait` *and* a `transitionend` listener is reachable from the element
  (itself, an ancestor, the document, or the window — it bubbles).
- **The `reftest-wait` half is what keeps it from being a one-way door.** Broiler
  dispatches no transition events, so a page waiting on one waits forever and the
  class survives to serialization. If transition events are implemented later, the
  natural shape — dispatch `transitionend`, the listener calls `takeScreenshot()`,
  the class goes — makes the predicate false while the transition is genuinely
  over, and the ordinary path elevates the popover. The rule degrades into the real
  one rather than inverting.
- **Nothing else in the directory matches the shape**, which is what says this is a
  rule and not a fit to one test: the three tests that must keep the popover held
  out — `-in-rendering` (60s), `-backdrop-entry` (2s delay + 2s), `-out-rendering`
  — screenshot immediately and register no such listener, and
  `overlay-transition-dialog` is `reftest-wait` but releases it from a
  `requestAnimationFrame`.
- **Measured: 1.81% → 100%**, and the 382-test `css-position` subset goes
  **238 → 239 passing with nothing lost**. Four tests in
  `OverlayTransitionScreenshotTimeTests` cover both directions, the ancestor
  listener, and the script-time half that must not move.

### A Web Animation on a pseudo-element was silently inert — problem 11, **fixed**

- **Test:** `css/css-pseudo/backdrop-animate-002`, 0.8% on CI and 0.77% here.
- **Owner:** HtmlBridge (`DomBridge/WebAnimations.cs`, `DomBridge.Serialization.cs`,
  `Dialogs.cs`). Main repo.
- **Two gaps, and the test needs both.** It animates `::backdrop` to a
  10%-opacity green with
  `{opacity: [0.1, 0.1], backgroundColor: ["green", "green"]}` and got the UA modal
  scrim. **Its own reference writes the same declarations as CSS and already
  rendered correctly** — which is what said the gap was the API rather than the
  pseudo-element.
  1. **The property-indexed keyframe form was not parsed at all.**
     `ParseAnimationKeyframes` required a `JSArray`, so
     `{ opacity: [0, 1] }` — the other half of the Web Animations keyframe
     argument — resolved to zero keyframes and the whole animation was inert. Each
     property is now turned into its own keyframes, which is exactly how
     `ResolveKeyframeProperties` reads them: it brackets each property against only
     the keyframes that define it, so properties with different list lengths need
     no common offset grid.
  2. **`pseudoElement` was ignored.** A pseudo-element has no node, so the
     element-inline bake `animate()` performs has nowhere to land. Those values are
     kept aside per element and pseudo, and emitted at serialization as
     `#id::pseudo { … !important }` author rules.
- **The rule alone did not close it, and the reason is worth recording.** With the
  rule emitted and verifiably present in the serialized HTML, the backdrop went
  green but stayed opaque. The WPT path renders a modal backdrop as a *synthesized*
  `<div>`, and a `#id::backdrop` selector cannot match a `<div>` — the div is
  filled from the bridge's own `::backdrop` cascade instead. So the animated values
  are merged into that cascade too, in `BackdropDeclarationsFor`, which both the
  synthesized div and the native marker read. The serialized rule is still what
  carries the native `::backdrop` box and any other pseudo.
- **Measured: 0.77% → 99.74%**, and the 358-test `css-pseudo` subset goes
  **236 → 237 passing with nothing lost**. Five tests in `AnimatePseudoElementTests`
  pin the animated backdrop against the equivalent style rule, the two keyframe
  forms, the single-value case, and that one element's pseudo bake does not reach
  another's.
- **Checked wider, because keyframe parsing touches every `animate()` call:**
  `css-view-transitions` 346/490, `css-masking` 222/439, `css-shadow` 157/207,
  `css-transforms/animation` 30/64 and `css-align/animation` 4/6 — all unchanged,
  no test changing state in either direction. Four `transform-interpolation-*`
  testharness tests move (two up, two down, none crossing the threshold) because
  more of their subtests now actually run.
- **A method note that cost real time.** The first `css-view-transitions` diff
  showed `auto-name-from-id` falling 97.46% → 1.27% and `auto-name-from-id-shadow`
  98.73% → 0.64%. Neither was this change: the reference set had been regenerated
  between the two runs, and **this directory's references are timing-sensitive
  enough to differ between generations**. Rendering the test three times on each
  build gives a deterministic 1.27% on *both* — and 1.27% is what CI reports.
  Re-baselining against the same reference set showed 346/490 either way. Compare
  runs only against references generated in the same pass.

### `font-size: math` collapsed the element it was on — problem 30, **fixed**

- **Test:** `css/css-fonts/math-script-level-and-math-style/font-size-math-001.tentative`,
  3.9% on CI and 3.93% here.
- **Owner:** `Broiler.Layout` (`Engine/CssBoxProperties.cs`). Main repo, so it is on
  CI immediately.
- **The keyword is `1em`, and the bug was not that it failed to scale.** MathML
  Core makes `font-size: math` the inherited size times the math scaling factor,
  and that factor is driven entirely by a *change* in `math-depth` — with no change
  it is 1. Broiler models no math depth, so the keyword is always `1em`, which is
  exactly what the test's reference asserts: it is the same document with `math`
  written as `1em`. `math` had no arm in the font-size keyword switch, so it fell
  through to the length parser, which reads an unrecognised token as **0** — and
  the zero clamp turned that into a **0.001pt** font. Every relative size beneath
  it then resolved against 0.001pt, so the whole subtree vanished. Ours rendered
  99.6% white against a reference that is 96% black.
- **Two call sites, because the computed and used sizes resolve keywords
  separately** (`ComputedFontSizePoints` and `ActualFont`, the latter on the
  non-zoom path).
- **Measured: 3.93% → 99.86%**, and the one arm carries **five more tests** with
  it — the 14-test `math-script-level-and-math-style` subset goes **7 → 13
  passing**, including two at 67.92%. Swept the whole 552-test `css/css-fonts`
  directory: **347 → 353 passing, nothing lost and nothing else moved.** Five tests
  in `MathFontSizeTests` cover the resolution, case-insensitivity, that the
  subtree no longer collapses, and that the arm does not swallow other keywords.

### Problem 28 is two things, and only the smaller one is fixed

- **Test:** `css/css-view-transitions/reset-state-after-scrolled-view-transition`,
  3.6% on CI and 3.61% here.
- **The transition machinery is not what fails.** Rendering the test and its own
  `-ref.html` — which performs the same scroll without a transition — gives
  **byte-identical output here**, 100% `lightblue` for both, against a Chromium
  reference that is 96.36% `lightgreen` / 3.61% `lightblue`. When a test and its
  reference agree with each other and both disagree with the other engine, the gap
  is in what they share.
- **Half of it was a scroll that never stopped at the end — fixed.** CSSOM View
  §"scroll an element" normalizes the requested position to the scrolling box's
  scrolling area, so a scroll past either end comes to rest at the end.
  `scrollTo`/`scrollBy` — window and element alike — passed `clamp: false`, so
  `scrollBy({top: scrollHeight})`, the standard "scroll to the bottom" idiom (since
  `scrollHeight` is always at least the maximum offset), landed *beyond* the content
  and painted the bare canvas. Reduced to a probe: a page with a `lightblue` canvas,
  a `lightgreen` body and a 200vh block renders 96.36/3.61 unscrolled — Chromium's
  numbers exactly — and 100% canvas after that `scrollBy`. With the clamp it is
  98.42/1.56.
- **Measured honestly: this closes no test.** `css/cssom-view` is 193/234 and
  `css/css-view-transitions` 345/490 **both before and after, with nothing changing
  state in either direction.** It is a conformance fix found while diagnosing, kept
  because it is right and covered (`ScrollClampingTests`, five cases, four of which
  fail without it), not because it moved a number.
- **The other half keeps the test failing, and it is already tracked.** With a 2s
  `::view-transition-group(*)` animation the transition is still running at
  screenshot time, so what paints is the root snapshot — and the root capture
  carries only a background colour, which is the `lightblue` we render. That is the
  rasterised-root-snapshot gap from
  [problems 19/21/23 of the #1491 list](#view-transitions-do-not-capture-the-document--still-open-2-will-not-be-won-here),
  where cloning the DOM into the snapshot was implemented, measured at +8/−7 and
  reverted. Problem 28 belongs to that item, not to a scroll bug.

### Problems 12 and 13 are an unshipped draft feature, not a layout bug

`css-grid/…/subgrid/grid-subgridded-to-grid-lanes/…` (0.80% and 0.89%, both
reproduced here) are built on `display: inline grid-lanes` — CSS Grid Level 3 —
combined with `grid-template-columns: subgrid` and `repeat(auto-fill, [line-names])`.
Broiler already **deliberately** treats `grid-lanes` as an invalid display value so
the declaration is dropped and the element keeps its default display, on the stated
grounds that no stable browser ships it unflagged
(`Broiler.Layout/Engine/CssUtils.cs`, and the pinned `Broiler.CSS` rejects it at
validation). So both engines lack the feature and what remains is a difference in
how the *unfeatured fallback* lays out — 93.98% between our render of the test and
our render of its own reference. Worth a maintainer's call on whether these belong
in the "reference is the unfeatured render" bucket before any engine work; chasing
byte-compatibility on a dropped declaration is not the same as implementing subgrid.

### An earlier verdict that no longer held — problem 7, re-triaged and then **fixed**

**It was recorded as an untrustworthy pass, and it was neither.** The #1491 table
below has it as "passes only by rendering nothing — the reference is a blank white
canvas". That stopped being true: Chromium's reference is now **100% green** and
Broiler rendered **100% red**, a 0.0% match that reproduces CI exactly. Re-checking
what a reference actually contains is cheap; carrying a stale verdict is not — and
once it was re-triaged as an ordinary failure it turned out to be a one-line rule.

- **Owner:** HtmlBridge (`DomBridge.ViewTransition.cs`). Main repo.
- **`view-transition-group: <custom-ident>` resolves against the *ancestor* chain**,
  not against the whole document (css-view-transitions-2) — the test's own title is
  "Explicit view-transition-group name can only match ancestors".
  `ResolveGroupParentName` accepted any captured element with that name, so a group
  nested under its **sibling**. The colour follows from there:
  `::view-transition-group(test) { background: inherit }` then inherited the
  sibling's red instead of the green `::view-transition` root, and since every group
  in that family is `position: absolute; inset: 0`, the last one painted takes the
  whole canvas.
- **The family is six tests against one reference**, which is what makes the rule
  checkable rather than guessable: `-direct` (parent) and `-nested` (grandparent)
  must keep nesting, while `-non-ancestor` (sibling), `-non-existent`, `-self` and
  `-nested-vt-names` must not. All six render 100% green after the change. `root`
  keeps qualifying explicitly, since the document element is an ancestor of every
  other captured element.
- **Measured: 0.0% → 100%**, and the 490-test `css-view-transitions` subset goes
  **344 → 345 passing with nothing lost and nothing else moved**. Four tests in
  `ViewTransitionGroupAncestorTests` read the nesting off one colour — green when
  the group nested, blue when it stayed top-level — and cover both directions, so
  the fix cannot degenerate into "never nest". Only the sibling case fails without
  it, which is the shape of a narrowing.

### Diagnosed, not fixed

Each of these reproduces locally, so the diagnosis is from a real render rather
than from reading code.

- **Problems 22, 23, 25 and 26 — the `massive-element-*-of-viewport-partially-onscreen`
  quartet (1.8% and 2.6%) — are two separate bugs in the capture, not scrolling.**
  The tests put a 40 000px element in a `writing-mode: vertical-lr` document, call
  `scrollIntoView()` on its far end, and screenshot the transition. Rendering the
  tests' **own `-ref.html`**, which performs the identical scroll without a
  transition, gives white 87.1% / green 10.6% / blue 1.3% against Chromium's white
  87.2% / green 11.5% / blue 0.7% — so scrolling a vertical writing mode is right,
  and the gap is in the transition. Instrumenting the capture says what it is:

      capture target: old=(8,8,40000x100)  new=(-38986,8,40000x100)

  1. **The old capture is taken against pre-scroll layout.** The test scrolls
     *before* calling `startViewTransition`, so both rectangles should carry the
     same −38 986 offset; the old one is still at the unscrolled `x: 8`. That alone
     explains the `-old` variants, which paint `::view-transition-old(target)` and
     so show the element's *left* edge (its lightblue `.top`) where the reference
     shows its right. It is a layout-flush ordering bug in
     `CaptureOldViewTransitionState`, not a coordinate-space one — the *new* capture
     computed the same way is correct.
  2. **The snapshot clone lays its children out horizontally.** Ours is green 84.8%
     / lightblue 12.5% — a green band ~651px tall, where the element is 100px tall
     — so `.middle`'s `block-size: 39800px` is resolving as a height. It is **not**
     a lost `writing-mode` bake: instrumenting `BuildViewTransitionSnapshotContent`
     shows `writing-mode=vertical-lr` correctly carried onto the content box, so the
     miss is further in, in how the clone's box is sized. That needs its own
     investigation.

  The family is 20 tests locally (2 passing), scoring from 1.8% to 98.8%, so it is
  worth more than the four tests the list names — but it is two fixes, each needing
  its own before/after, not the one this entry used to describe.
- **Problems 14, 15 and 27 (`clip-path`, ~1.0% and 2.9%) need a real path clip.**
  `TryCreateInsetClipPathItem` in `Broiler.HTML`'s `PaintWalker.Geometry` models
  `clip-path` as a **rectangle** — it parses `inset()` and nothing else. Problems
  14 and 15 use `polygon()` (an L-shape on the document element, which must also
  clip the propagated root background), and problem 27 references an SVG
  `<clipPath>` by `url(#…)`. Both need a path clip in the graphics backend rather
  than a `ClipItem` rect, in a submodule this session cannot push to. Our render
  is the unclipped page in every case.

### Two that do not reproduce here

Judge them from a CI artifact, not from this container. Per the caveats above, a
*better* local score than CI usually means the offline render is not the one CI
scored.

- **Problem 8 (`css-view-transitions/nothing-captured`)**: CI 0.0%, **99.54%
  locally against a genuine reference** (white/green/blue, not blank).
- **Problem 19 (`view-transition-waituntil-animation-manipulation`)**: CI 1.3%,
  **98.46% locally**.
- **Problem 17 (`css-view-transitions/auto-name-from-id`)**: CI 1.3%, **97.46%
  locally** — and it belongs to the `auto-name` family that problem 18 heads,
  where the reference is Chromium's *unfeatured* render (see problems 14/15 in the
  #1491 table). Worth settling as one group rather than test by test.

### #1538 problems, at a glance

CI percentages are the issue's. Local numbers are this container against locally
generated Chromium references; **"—" means not measured here** (no reference
generated for that directory in this session — all six are re-reports already
owned by a section above). Re-reported problems point at that section.

| # | Test | CI | Local | Status |
| --- | --- | --- | --- | --- |
| 1 | `css-backgrounds/background-image-shared-stylesheet` | 0.0% | — | re-report of #1491 problem 4 — needs the server's `trickle` pipe |
| 2 | `css-color-adjust/…/cross-origin-002.sub` | 0.0% | — | re-report of #1491 problem 5 — needs `.sub` substitution and a second host |
| 3 | `css-page/page-margin-002-print` | 0.0% | — | re-report of #1491 problem 12 — [screen-layout gaps](#screen-layout-gaps-behind-the-three-print-html-tests) |
| 4 | `css-transforms/animation/transform-interpolation-002` | 0.0% | — | re-report of #1491 problem 13 — both engines empty offline |
| 5, 6 | `css-view-transitions/iframe-and-main-frame-transition-old-main-*` (2) | 0.0% | 0.00% | re-report of #1491 problems 16/17 — needs a transition in a nested browsing context |
| 7 | `css-view-transitions/nested/compute-explicit-name-non-ancestor.tentative` | 0.0% | **0.00% → 100%** | **fixed** — an explicit `view-transition-group` name now only matches an ancestor, main repo. (Also a corrected verdict: it was not the blank-reference pass the #1491 table records) |
| 8 | `css-view-transitions/nothing-captured` | 0.0% | **99.54% (passes)** | does not reproduce — judge from CI |
| 9 | `resource-timing/initiator-type/frameset` | 0.0% | — | re-report of #1491 problem 26 — [frameset frames render nothing](#frameset-frames-render-nothing) |
| 10 | `css-color-adjust/…/mismatch-dynamic` | 0.0% | — | **won't fix** — #1497 problem 25: Chromium fails this reftest against its own reference |
| 11 | `css-pseudo/backdrop-animate-002` | 0.8% | **0.77% → 99.74%** | **fixed** — property-indexed keyframes + `animate({pseudoElement})`, main repo |
| 12, 13 | `css-grid/…/subgrid/…` (2) | 0.8%, 0.9% | 0.80%, 0.89% | open — built on `display: inline grid-lanes`, which Broiler deliberately drops as invalid because no stable browser ships it unflagged. See above. The 241-test `grid-subgridded-to-grid-lanes` subset is 122 passing |
| 14, 15 | `css-masking/clip-path/clip-path-document-element{,-will-change}` (2) | 1.0% | 0.95% | open — `polygon()` needs a real path clip; see above |
| 16 | `css-shadow/shadow-directionality-002.tentative` | 1.1% | **1.09% → 99.55%** | **fixed** — shadow-tree scoping (main repo) + `:dir()` ([`patches/0102`](../patches/README.md)) |
| 17 | `css-view-transitions/auto-name-from-id` | 1.3% | 1.27% | open — **it does reproduce**; the earlier 97.46% was a stale reference (see the method note above). `auto-name` family — see problem 18 |
| 18 | `css-view-transitions/auto-name` | 1.3% | 1.27% | **won't fix** — #1491 problems 14/15: reference is the unfeatured render |
| 19 | `css-view-transitions/view-transition-waituntil-animation-manipulation` | 1.3% | 98.46% | does not reproduce — judge from CI |
| 20 | `css-shadow/shadow-directionality-001.tentative` | 1.3% | **1.27% → 99.55%** | **fixed** — same change as problem 16 |
| 21 | `css-position/overlay/overlay-transition-finished` | 1.8% | **1.81% → 100%** | **fixed** — the paint-time half of the `overlay` entry transition, main repo |
| 22, 23 | `css-view-transitions/massive-element-left-of-viewport-partially-onscreen-{new,old}` | 1.8% | 1.81% | open — diagnosed above (snapshot geometry, not scrolling) |
| 24 | `css-align/animation/row-gap-interpolation` | 2.6% | 2.59% | open — a **testharness** test: its reference is Chromium's results table, so closing it means passing the `row-gap` interpolation subtests, not one fix |
| 25, 26 | `css-view-transitions/massive-element-right-of-viewport-partially-onscreen-{new,old}` | 2.6% | 2.65% | open — same root cause as 22/23 |
| 27 | `css-masking/clip-path/clip-path-element-userSpaceOnUse-004` | 2.9% | 2.86% | open — SVG `<clipPath>` reference needs a real path clip |
| 28 | `css-view-transitions/reset-state-after-scrolled-view-transition` | 3.6% | 3.61% | **part-fixed** — the scroll no longer overshoots the end (CSSOM View clamp, main repo); still failing on the rasterised root snapshot, which is #1491's problems 19/21/23 |
| 29 | `html/…/form-validation-validity-textarea-defaultValue` | 3.8% | 3.78% | open — a **testharness** test whose reference is Chromium's results table; three of its five subtests drive `test_driver.send_keys`, which the runner only stubs |
| 30 | `css-fonts/…/font-size-math-001.tentative` | 3.9% | **3.93% → 99.86%** | **fixed** — `font-size: math` is `1em`; the subset goes 7 → 13 of 14, main repo |

## Reported problems, at a glance

Local numbers come from this container against locally generated Chromium
references; CI's are authoritative where they disagree. **Status** records work
landed since the run — a fix marked *pending patch* is not yet on CI, because it
lives in a submodule whose remote this session cannot push to (see
`patches/README.md`). Patches 0035–0039 have since been applied and their
pointers bumped, so everything they carried is now on CI. **won't fix** and
**untrustworthy** mark tests whose Chromium reference was produced without the
feature they test — closing those means rendering less, not more.

| # | Test | CI | Local observation | Status |
| --- | --- | --- | --- | --- |
| 4 | `css-backgrounds/background-image-shared-stylesheet` | 0.0% | 99.8% — needs the server's `trickle` pipe | open |
| 5 | `css-color-adjust/…/cross-origin-002.sub` | 0.0% | ours `#121212`, Chromium white — needs `.sub` | open |
| 6 | `css-color/contrast-color-style-query` | 0.0% | ours white, Chromium green | **fixed** — patch 0039 applied |
| 7–10 | `css-image-animation/*-paused` (4) | 0.0% | ours green frame 0, Chromium red | **fixed** — 0.0% → 100% locally; **pending patch 0041** |
| 11 | `css-page/monolithic-overflow-011-print` | 0.0% | ours blank, Chromium yellow + hotpink | open |
| 12 | `css-page/page-margin-002-print` | 0.0% | ours yellow, Chromium white | open |
| 13 | `css-transforms/animation/transform-interpolation-002` | 0.0% | 100% — both empty offline | open |
| 14, 15 | `css-view-transitions/auto-name*` (2) | 0.0% | ours captures both items + backdrop; Chromium drops `view-transition-name: auto` | **won't fix** — reference is the unfeatured render |
| 16, 17 | `css-view-transitions/iframe-and-main-frame-*` (2) | 0.0% | ours 99.5% white, Chromium 74.5% green + 25% blue | open — needs a transition in a nested browsing context |
| 18 | `css-view-transitions/nested/compute-explicit-name-non-ancestor.tentative` | 0.0% | 100% — reference is a blank white canvas | ~~**untrustworthy** — passes only by rendering nothing~~ **stale, and since fixed**: the reference is now 100% green, and an explicit `view-transition-group` name matching a non-ancestor was the cause. See [#1538 problem 7](#an-earlier-verdict-that-no-longer-held--problem-7-re-triaged-and-then-fixed) |
| 19, 21 | `css-view-transitions/old-/new-content-captures-root` (2) | 0.0% | ours 98.7% pink (backdrop through the page) | open — needs a rasterised root snapshot |
| 20, 22 | `css-view-transitions/*-root-scrollbar-with-fixed-background` (2) | 0.0% | 100% — reference is 99% `lightblue`, genuine | passing locally |
| 23 | `css-view-transitions/root-captured-as-different-tag` | 0.0% | ours 100% red (the `(root)` trap rule) | part-fixed — the `(root)` rules no longer match; still needs the root snapshot |
| 24 | `canvas/…/manual/dialog-paints-in-top-layer.tentative` | 0.0% | ours dialog, Chromium blank (unsupported) | **fixed** — reclassified Manual |
| 25 | `the-link-element/stylesheet-with-base` | 0.0% | ours red (trap file), Chromium white | **fixed** — renders green locally |
| 26 | `resource-timing/initiator-type/frameset` | 0.0% | ours white, Chromium `#dddddd` | open |
| 27 | `dom/nodes/moveBefore/preserve-render-blocking-style` | 0.0% | ours white, Chromium green | **fixed** — but only at the *second* attempt; `moveBefore` (patch 0038) was half of it, and the test stayed at 0.0% until `<link>` got its IDL reflectors. See [the #1497 section](#the-next-run-issue-1497-2026-07-30) |
| 28 | `forced-colors-mode/forced-colors-mode-20` | 0.0% | ours black, Chromium white | **fixed** — patch 0036 applied |
| 29 | `shadow-dom/focus-navigation/delegatesFocus-highlight-sibling` | 0.0% | ours flat `#cccccc` — a template's styles leaking into the page | **0.0% → 97.8%** with patch 0042; residual is inline-block line height |
| 30 | `css-page/page-box-008-print` | 0.0% | ours hotpink, Chromium yellow | **`vb` fixed** — patches 0036/0037 applied |
