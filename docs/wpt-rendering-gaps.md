# WPT rendering gaps — the worst pixel mismatches

- **Scope:** the `< 50% match` tail of the WPT run reported in
  [issue #1491](https://github.com/Broiler-Platform/Broiler/issues/1491),
  problems 4–30. Each of those 27 tests renders at **0.0–2.5%** of its Chromium
  reference, so each is a whole-canvas difference rather than a tolerance
  problem.
- **Not in scope:** problem 1 (the `DomDocument.CreateElement` crash) is fixed —
  frames no longer parse a non-HTML resource as markup, and `patches/0035-…`
  carried the DOM-layer fix (since applied). Problems 2 and 3 (per-test memory
  aborts) are the per-element JS wrapper cost, tracked in
  [the root roadmap](ROADMAP.md#htmlbridge-runtime).
- **Companion documents:** [root roadmap](ROADMAP.md) for cross-component work;
  the component roadmaps own the implementation once an item below names them.
- **Progress:** problems 6, 7–10, 24, 25, 27, 28 and the `vb` half of
  30 are fixed; each section says what landed, what was verified locally, and what
  is left for CI to confirm. Patches `0035`–`0039` — which carried the submodule
  half of 6, 27, 28 and 30 — **have since been applied and their pointers
  bumped**, so all of those are now live on CI rather than pending. The one fix
  still waiting on a maintainer are problems 7–10's
  [`patches/0040`](../patches/README.md) and problem 29's `patches/0041`, whose
  remote this session cannot push to (403, as documented in `CLAUDE.md`). Neither
  has a main-repo fallback, so those tests stay at their old numbers on CI until
  the patches land.
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
  2. **`patches/0040` (Broiler.HTML).** `StubImageAdapter`'s decode — the single
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
- **What did *not* work, and why it is worth knowing.** The remaining gap is that
  the root capture carries only a background colour, no content, so
  `::view-transition-old(root)` is transparent and the author backdrop shows
  through the page. Reproducing the page by **cloning the DOM** into the snapshot
  box was implemented, measured, and reverted. It did fix problems 19, 21 and 23
  outright (0.0% → 100%), but across the 458 local `css-view-transitions` tests it
  was **+8 passing / −7 passing** — and it cost 79 pixel points on
  `root-to-shared-animation-end` (82.7% → 3.1%) and ~4 on
  `content-with-transform-old-/new-image`. Restricting the clone to the *old*
  snapshot did not rescue those. The reason is structural, not a missing detail: a
  DOM clone re-lays-out and is only *close*, while the transparent box let the
  **live page** show through — and the live page is pixel-exact. Anywhere the old
  root snapshot is genuinely visible, exact beats close. **A root capture needs a
  rasterised snapshot from the renderer, which is a `Broiler.HTML` capability, not
  something the bridge can synthesise from the DOM.** That is what the original
  "capture the old and new snapshots as images" next action asked for, and it
  still stands.
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
  2. Lay out and paint a `contain: size` box inside table internals.
  3. Establish what a vertical-rl root's initial scroll position is and align the
     canvas extent with it.
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
- **What landed:** `patches/0041-…` stops `DomParser.CascadeParseStyles` at a
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
- **What is left is not wrapping any more.** The menu rows render 76px tall
  against Chromium's 54px, where they were 133px. The items no longer wrap; they
  are simply ~25px tall where Chromium's are ~18px, which is line-height and font
  metrics — a different class of problem from the three fixed here, and one that
  will move many tests at once when it is addressed.
- **Exit gate:** inline-block line height matches the reference (ours ~25px against
  ~18px), and only then the focus question — a focused test for `delegatesFocus`
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
| 7–10 | `css-image-animation/*-paused` (4) | 0.0% | ours green frame 0, Chromium red | **fixed** — 0.0% → 100% locally; **pending patch 0040** |
| 11 | `css-page/monolithic-overflow-011-print` | 0.0% | ours blank, Chromium yellow + hotpink | open |
| 12 | `css-page/page-margin-002-print` | 0.0% | ours yellow, Chromium white | open |
| 13 | `css-transforms/animation/transform-interpolation-002` | 0.0% | 100% — both empty offline | open |
| 14, 15 | `css-view-transitions/auto-name*` (2) | 0.0% | ours captures both items + backdrop; Chromium drops `view-transition-name: auto` | **won't fix** — reference is the unfeatured render |
| 16, 17 | `css-view-transitions/iframe-and-main-frame-*` (2) | 0.0% | ours 99.5% white, Chromium 74.5% green + 25% blue | open — needs a transition in a nested browsing context |
| 18 | `css-view-transitions/nested/compute-explicit-name-non-ancestor.tentative` | 0.0% | 100% — reference is a blank white canvas | **untrustworthy** — passes only by rendering nothing |
| 19, 21 | `css-view-transitions/old-/new-content-captures-root` (2) | 0.0% | ours 98.7% pink (backdrop through the page) | open — needs a rasterised root snapshot |
| 20, 22 | `css-view-transitions/*-root-scrollbar-with-fixed-background` (2) | 0.0% | 100% — reference is 99% `lightblue`, genuine | passing locally |
| 23 | `css-view-transitions/root-captured-as-different-tag` | 0.0% | ours 100% red (the `(root)` trap rule) | part-fixed — the `(root)` rules no longer match; still needs the root snapshot |
| 24 | `canvas/…/manual/dialog-paints-in-top-layer.tentative` | 0.0% | ours dialog, Chromium blank (unsupported) | **fixed** — reclassified Manual |
| 25 | `the-link-element/stylesheet-with-base` | 0.0% | ours red (trap file), Chromium white | **fixed** — renders green locally |
| 26 | `resource-timing/initiator-type/frameset` | 0.0% | ours white, Chromium `#dddddd` | open |
| 27 | `dom/nodes/moveBefore/preserve-render-blocking-style` | 0.0% | ours white, Chromium green | **fixed** — patch 0038 applied; bridge now delegates to it |
| 28 | `forced-colors-mode/forced-colors-mode-20` | 0.0% | ours black, Chromium white | **fixed** — patch 0036 applied |
| 29 | `shadow-dom/focus-navigation/delegatesFocus-highlight-sibling` | 0.0% | ours flat `#cccccc` — a template's styles leaking into the page | **0.0% → 97.8%** with patch 0041; residual is inline-block line height |
| 30 | `css-page/page-box-008-print` | 0.0% | ours hotpink, Chromium yellow | **`vb` fixed** — patches 0036/0037 applied |
