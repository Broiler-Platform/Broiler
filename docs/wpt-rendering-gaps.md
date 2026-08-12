# WPT rendering gaps — the worst pixel mismatches

- **Scope:** the `< 50% match` tail of the WPT run reported in
  [issue #1491](https://github.com/Broiler-Platform/Broiler/issues/1491),
  problems 4–30. Each of those 27 tests renders at **0.0–2.5%** of its Chromium
  reference, so each is a whole-canvas difference rather than a tolerance
  problem.
- **Later runs get their own section rather than a rewrite**, since most of each
  new list is the same tests under new numbers:
  [#1497 (2026-07-30)](#the-next-run-issue-1497-2026-07-30),
  [#1538 (2026-08-05)](#the-next-run-issue-1538-2026-08-05),
  [#1562 (2026-08-07)](#the-next-run-issue-1562-2026-08-07),
  [#1612 (2026-08-12)](#the-next-run-issue-1612-2026-08-12) and
  [#1615 (2026-08-12)](#the-next-run-issue-1615-2026-08-12). Where a re-run
  contradicts something below, the section says so and the row here is struck
  through — read the newest section first. #1612 contradicts two of them: the
  frameset test was not a frameset bug, and `image-animation: paused` is no
  longer unimplemented. #1615 contradicts a third: `.sub` tests are no longer
  unjudgeable offline, because the runner now performs the substitution itself.
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
  styles staying inert). ~~The one still waiting on a maintainer is problems
  7–10's `patches/0041`.~~ **That one has been applied too** — the pinned
  `Broiler.HTML` calls `BBitmap.DecodeFrameAt` — so nothing on this list is
  waiting on a patch any more. What problems 7–10 score is now decided by the
  `image-animation` implementation that landed after it; see
  [#1612 problems 2 and 3](#the-next-run-issue-1612-2026-08-12).
- **Several of these tests should not be "fixed".** Problems 14, 15 and 24 pass
  only by rendering *less* than Broiler already does — their Chromium reference
  was produced by an engine that does not implement the feature under test, so
  the reference is the unfeatured render. Problem 18's reference is a blank white
  canvas for the same reason. Chasing them would mean deleting working support.
  Check what the reference actually contains before treating a 0.0% as a gap —
  **the newest run makes this the majority case**: six of the nine tests reported
  at 0.0% in [#1612](#the-next-run-issue-1612-2026-08-12) render exactly what
  their own `rel=match` reference asks for, and only one was a real bug. Problems
  7–10 are the worked example of the cost: they were fixed to 100% by frame
  selection, then went back to 0.0% when `image-animation: paused` was
  implemented for real, because Chromium still does not implement it.

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
- **~~`image-animation: paused` is still deliberately unimplemented.~~ No longer
  true — and the consequence this bullet predicted is exactly what happened.**
  The property *has* since been implemented in the main repo
  (`Broiler.Layout/Engine/CssBox.ImageAnimation.cs`: a `paused`/`stopped` box
  pins its own image loads to time zero, and a `<body>` background that
  propagates to the canvas takes the *root's* value). Chromium still does not
  implement it, so each Chromium reference is still the unpaused render — and
  the two canvas-propagation tests of the four duly went back to 0.0% as
  [#1612 problems 2 and 3](#the-next-run-issue-1612-2026-08-12). Broiler now
  renders both the way their own `rel=match` references say, and matching CI
  again would mean deleting the property. Read the two bullets together before
  touching either: this is the trap, not a regression.
- **Verified:** the four tests, run against locally generated Chromium
  references, go from **0.0% to 100%** — the same 0.0% CI reports, reproduced
  before the change and gone after. Four focused tests cover the timeline
  (selection at successive times, the short-delay clamp, loop-count wrap versus
  hold, and the clock's nested pin/restore), and 14 cases cover the runner's
  delay extraction — including the negative half, that a test with no literal
  delay resolves to zero rather than guessing.
- **~~Remaining: the patch.~~ Applied.** The pinned `Broiler.HTML` calls
  `BBitmap.DecodeFrameAt(data, ImageAnimationClock.PresentationTime)` from
  `StubImageAdapter`, so frame selection is live on CI. What the four tests score
  is now decided by the `image-animation` implementation above, not by this.
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

## Frameset frames render nothing — **fixed** (see [#1612 problem 8](#a-root-relative-frame-src-resolved-against-the-wrong-directory--problem-8-fixed))

- **Test:** problem 26, `resource-timing/initiator-type/frameset.html`.
- **Owner:** HtmlBridge (nested browsing contexts) with Broiler.HTML.
- **Original evidence:** Broiler's canvas is 100% white; Chromium's is 100%
  `#dddddd` with frame borders. `0b3c596` and `a06b53c` moved `<frame>`
  sub-documents and the iframe default object size forward, so this looked like
  the remaining `<frameset>` case: the frameset grid paints neither its own
  canvas nor its frames' documents.
- **That diagnosis was wrong, and the test is now passing** (0.0% → **99.7%**).
  The frameset grid was never the problem — the *URL* was. This test's frame is
  `src="/resource-timing/resources/green.html"`, and a root-relative URL did not
  resolve; the same page with a directory-relative `src` already rendered its
  frame correctly. Nothing about `<frameset>` was involved, and an `<iframe>`
  with a root-relative `src` failed identically. Written up in full under
  [#1612 problem 8](#a-root-relative-frame-src-resolved-against-the-wrong-directory--problem-8-fixed).
- **Left over, and separate:** a frameset with *more than one* frame paints only
  its first cell. That is a parser bug, not a grid bug —
  [`patches/0003`](../patches/README.md) — and no test in the current subset
  covers it.

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

> **Partly resolved.** The `.sub` half of this needed no server at all — see
> [#1615](#the-runner-never-performed-wpts-sub-substitution--problem-1-fixed).
> The runner now expands `.sub` templates itself and serves WPT's own hosts from
> the checkout, so problem 5 is reproducible, **fixed and passing**, and so is
> every other `.sub` test's cross-origin frame. What remains below is the `?pipe=`
> half and the script-built-DOM case.

- **Tests:** ~~problem 5
  (`css/css-color-adjust/…/color-scheme-iframe-background-mismatch-opaque-cross-origin-002.sub.html`)~~ **fixed**,
  problem 4 (`css/css-backgrounds/background-image-shared-stylesheet.html`),
  problem 13 (`css/css-transforms/animation/transform-interpolation-002.html`).
- **Owner:** the WPT runner, then the component the confirmed failure names.
- **Current evidence:** both remaining tests are reported at 0.0% by CI but are
  not reproducible offline, and their local scores are misleading:
  - problem 4 needs `?pipe=trickle(d2)` for its image and a script-injected
    `data:text/css` stylesheet; offline neither engine loads the image, so the
    pair matched at 99.8% locally while CI reports 0.0%.
  - problem 13 builds its whole DOM from `interpolation-testcommon.js`; offline
    both renders are empty (100% local match, 0.0% on CI), so the CI artifact's
    `rendered.png` is the only evidence that says what Broiler actually drew.
  - ~~problem 5 needs `.sub` substitution and a cross-origin host.~~ It needed
    substitution, which is now done on both sides of the comparison; the
    "cross-origin host" was a red herring, because WPT serves all of its hosts
    from one checkout.
- **Next actions:**
  1. Pull the `wpt-merged` artifact's failure images for these two before
     opening any component work — the local pipeline cannot see their real
     failure.
  2. `?pipe=` is the remaining server behaviour worth emulating; the `sub` pipe is
     done, and the handlers are per-pipe and independent
     (`tools/wptserve/wptserve/pipes.py`), so `trickle`, `status` and `header` can
     follow the same shape — a runner-side transform, not a server.
- **Exit gate:** each of the two is either reproducible locally or reassigned to
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

### A runner note: scroll metrics ignore the configured viewport size

Hit twice while writing tests for the fixes above, so it is worth recording rather
than working around a third time. `new WptTestRunner(w, h)` renders at the given
size, but the scroll metrics — `vh` lengths and the maximum scroll offset — resolve
against the default 1024x768 regardless. A page built to be "taller than the
viewport" at 200x200 therefore scrolls to somewhere that is not the bottom of the
canvas, and a test asserting on what is on screen fails for a reason that has
nothing to do with what it is testing. Both `ScrollClampingTests` and
`ViewTransitionOldCaptureScrollTests` pin their renders to the default size for
this reason. Out of scope for issue #1538, but it is a real defect in the runner,
not just a test-authoring trap.

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

- **Problems 22, 23, 25 and 26 — the `massive-element-*` family — are two separate
  bugs in the capture, not scrolling. One is fixed.**
  The tests put a 40 000px element in a `writing-mode: vertical-lr` document, call
  `scrollIntoView()` on its far end, and screenshot the transition. Rendering the
  tests' **own `-ref.html`**, which performs the identical scroll without a
  transition, gives white 87.1% / green 10.6% / blue 1.3% against Chromium's white
  87.2% / green 11.5% / blue 0.7% — so scrolling a vertical writing mode is right,
  and the gap is in the transition. Instrumenting the capture said what it is:

      capture target: old=(8,8,40000x100)  new=(-38986,8,40000x100)

  1. **The old capture was not in the snapshot containing block — fixed.** Both
     captures call the same `GetBoundingClientRectForDomElement`, but at different
     moments against different layouts, and only one has the scroll folded in: the
     new capture runs on the render projection, where the scroll is already baked
     into box positions, while the old one runs during script, where it is not. The
     page scroll is now subtracted from the old capture, which reproduces the new
     capture's −38 986 exactly. **A `position: fixed` element — or anything inside
     one — is excluded**, since it does not move with the page and its document
     coordinates are already viewport coordinates; without that exception
     `new-content-transform-position-fixed` falls from 100% to 98.73%, which is how
     the exception was found rather than guessed.
     **Measured: `css-view-transitions` 346 → 349 of 490, nothing lost.** The gains
     are `massive-element-on-top-of-viewport-partially-onscreen-old`/`-new`
     (96.70% → 99.58% — the *vertical*-scroll members of the family) and
     `transformed-element-scroll-transform` (98.73% → 100%).
     `snapshot-containing-block-absolute` moves 55.85% → 54.89%, failing on both
     sides.
  2. **The snapshot clone still lays its children out horizontally — open.** Ours is
     green 85.1% — a green band ~651px tall where the element is 100px tall — so
     `.middle`'s `block-size: 39800px` is resolving as a height. It is **not** a
     lost `writing-mode` bake: `BuildViewTransitionSnapshotContent` carries
     `writing-mode: vertical-lr` onto the content box correctly, so the miss is
     further in, in how the clone's box is sized. **This is what still fails the
     four tests the list names** — they are the horizontal-scroll members, where the
     block axis and the scrolled axis are the same one.

  The family is 20 tests locally, and the fixed half moved two of them.
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
| 22, 23 | `css-view-transitions/massive-element-left-of-viewport-partially-onscreen-{new,old}` | 1.8% | 1.81% | open — **half fixed**: the old capture is now in the snapshot containing block (which closed the two `on-top-of` siblings); these still fail on the clone's box sizing. See above |
| 24 | `css-align/animation/row-gap-interpolation` | 2.6% | 2.59% | open — a **testharness** test: its reference is Chromium's results table, so closing it means passing the `row-gap` interpolation subtests, not one fix |
| 25, 26 | `css-view-transitions/massive-element-right-of-viewport-partially-onscreen-{new,old}` | 2.6% | 2.65% | open — same two causes as 22/23 |
| 27 | `css-masking/clip-path/clip-path-element-userSpaceOnUse-004` | 2.9% | 2.86% | open — SVG `<clipPath>` reference needs a real path clip |
| 28 | `css-view-transitions/reset-state-after-scrolled-view-transition` | 3.6% | 3.61% | **part-fixed** — the scroll no longer overshoots the end (CSSOM View clamp, main repo); still failing on the rasterised root snapshot, which is #1491's problems 19/21/23 |
| 29 | `html/…/form-validation-validity-textarea-defaultValue` | 3.8% | 3.78% | open — a **testharness** test whose reference is Chromium's results table; three of its five subtests drive `test_driver.send_keys`, which the runner only stubs |
| 30 | `css-fonts/…/font-size-math-001.tentative` | 3.9% | **3.93% → 99.86%** | **fixed** — `font-size: math` is `1em`; the subset goes 7 → 13 of 14, main repo |

## The next run (issue #1562, 2026-08-07)

19 927 failures, no incomplete shards. Cross-referencing the list against the
three runs above: **seventeen of the thirty are tests those sections already
name, or new members of families they own** (the nested view transitions, the
`grid-lanes` subgrid pair, `auto-name`, the `massive-element-*` four). **The
other thirteen are new to the list, and all thirteen were worked here** —
problems 17, 18 and 20 through 30.

What they came to: **eight fixed** — problems 21–26 (the `css-contain`
background cluster, the largest single-cause group on the list), 27 and 20;
**two won't fix** — problems 28 and 29, whose reference is a render Chromium
itself fails the reftest with; and **three diagnosed but not fixed** — problems
17, 18 and 30, each with the cause isolated and an exit gate named. Two of the
three turned out to be mis-attributed by their own test names: problem 17 is not
a quirks bug and problem 18 is not a subgrid bug. So the at-a-glance table below
carries a local number only where one was measured; everything else points at the
section that already owns it rather than being re-measured.

### Containment other than `paint` never stopped background propagation — problems 21–26, **fixed**

- **Tests:** `css/css-contain/contain-body-bg-001` (layout), `-003` (size),
  `-004` (style) and `contain-html-bg-001`/`-003`/`-004` (the same three, set on
  the root instead of body). All six are 7.5% on CI and **reproduce here at 7.5%
  exactly** — 727 836 of 786 432 pixels differ, which is the whole 1024×768
  canvas but for the test's 300×200 white `<p>`.
- **Owner:** `Broiler.HTML` (`IR/PaintWalker.CanvasBackground.cs` and
  `HtmlContainerInt.cs`), so it ships as
  [`patches/0120`](../patches/README.md) — the push is 403, as `CLAUDE.md`
  describes. It is listed in `scripts/apply-pending-wpt-patches.sh`, so the WPT
  run exercises it on CI ahead of a maintainer landing it.
- **One condition, one keyword too narrow.** Each test paints `<body>` red under
  a white `<p>` that covers it exactly, so the only red that can reach the screen
  is red the *canvas* took from body. `FindCanvasBackgroundAndImage` suppressed
  propagation for `contain: paint` only (plus `strict`/`content`, the shorthands
  that include it), so `layout`, `size` and `style` propagated as if no
  containment were set and flooded the canvas. That is why `-002` — the `paint`
  member of each family — was the one already passing, and why it is not on the
  issue's list.
- **The spec names all four, and it names both elements.** CSS Contain 2 §2:
  *"when any containments are active on either the html or body elements,
  propagation of properties from the body element to the initial containing
  block, the viewport, or the canvas background, is disabled"*. So the check is
  now "is **any** containment active", tokenised rather than substring-matched
  (`none` must not read as a keyword), and it also answers yes for
  `content-visibility: hidden`/`auto`, which apply containment by other means.
  Both halves of the cascade were fixed together: `PaintWalker` decides what
  paints the canvas, and `HtmlContainerInt.GetRootBackgroundColor` decides the
  colour the surface is erased with — they have to agree, or the erase colour
  wins in the margins.
- **The mirror-image half, which the tests do not cover and Chromium settles.**
  The old code applied the same suppression to the *root's own* background, so
  `html { contain: paint; background: green }` painted a white canvas. That is
  wrong in the other direction: the root element's background **is** the canvas
  background rather than something propagated to it, and the spec disables
  propagation *from body*. Asked directly — Chromium under Playwright, five
  documents differing only in the `contain` value — the whole canvas comes back
  `rgb(0,128,0)` for `layout`, `paint`, `size`, `style` and no containment alike.
  Only `display: none` still holds the root's background back.
- **Calibrated against Chromium rather than inferred.** Ten further probes fixed
  the edges of the rule. Suppressing, on body: `contain: layout`, `style`,
  `inline-size`, `content-visibility: hidden` and `content-visibility: auto`. On
  html: `contain: inline-size` and `content-visibility: auto`. **Not**
  suppressing: `contain: none`, and no `contain` at all — both still flood the
  canvas red, which is what keeps the fix from over-suppressing.
- **One divergence, recorded rather than papered over.** Containment does not
  apply to a non-atomic inline, and Chromium duly keeps propagating for
  `body { display: inline; contain: layout }`. Broiler cannot reproduce that
  distinction: instrumenting both code paths shows the box tree reporting
  `<body>` as `display: block` whatever `display` says, so a guard for it would
  be unreachable. The first draft carried one; it was removed once the
  instrumentation said so, and the comment now says why.
- **Measured.** The 584-test `css/css-contain` subset goes **413 → 419 passing**,
  the six tests **7.5% → 99.8%**, and the failing-test set differs by exactly
  those six in one direction and nothing in the other. Average match
  96.57% → 97.62% — and that +1.05 is precisely the six tests' own contribution
  (6 × (99.8 − 7.5) ÷ 524), so no other test in the subset moved even
  sub-threshold.
- **Regression-checked on the two subsets that own the canvas.** `css-backgrounds`
  (956 tests) and `css-color-adjust` (36) were run against the pinned engine and
  against the patched one: **all 991 result lines byte-identical** — same passes,
  same failures, same match percentages to the decimal. `RootBackgroundTests` and
  `ContainPaintClipTests` are unchanged, and 20 new cases in
  `CanvasBackgroundContainmentTests` cover every containment keyword on both
  elements, both `content-visibility` values, the `none`/absent controls, the
  root's own background, and `display: none` on body.

### A colour-only SVG filter chain now recolours the shape — problem 27, **fixed**

- **Test:** `css/filter-effects/fecolormatrix-negative`, 7.7% on CI and
  **reproducing here at the same 7.7%**. Its reference is a cyan rectangle, which is also what the test's own
  assertion says to expect; Broiler painted the unfiltered `#ffaa00` orange.
- **Owner:** `Broiler.Layout` (`IR/SvgColorFilter.cs`, `IR/SvgRenderer.cs`,
  `IR/SvgFilterTable.cs`) — **main repo, so it is on CI immediately**, no patch.
- **What the filter does, in closed form.** `feColorMatrix` with the negative
  entries inverts each channel — `#ffaa00` → (0, 0.333, 1) — and the arithmetic
  `feComposite` with `k2="255"` multiplies the premultiplied channels by 255, so
  every non-zero one saturates: (0, 1, 1), cyan. There is no raster pipeline
  needed to know that, because **a shape filled with one solid colour has a
  source graphic that is that colour inside it and transparent black outside**,
  and a chain of per-pixel colour operations therefore produces exactly two
  colours. That is the same kind of modelling the engine already applies to an
  `feFlood`-only filter (`SvgFilterTable.FloodFilter`), extended to a chain.
- **Why the region does not have to be modelled too** — the trap that makes this
  cheap rather than a filter engine. Every step modelled here maps zero alpha to
  zero alpha, so the *outside* colour stays transparent and the filter region
  never becomes visible. `AMatrixThatZeroesAlpha_MakesTheShapeTransparent` pins
  that property rather than leaving it as a comment.
- **Deliberately narrow, and every bail-out renders unfiltered.** Only
  `feColorMatrix` (`type="matrix"`, its default) and `feComposite`
  (`operator="arithmetic"`); only a straight chain, where each primitive consumes
  the previous one's `result`; only when the filter declares
  `color-interpolation-filters="sRGB"`, because the default is linearRGB and the
  conversion is not modelled — a filter that does not say sRGB is left alone
  rather than computed in the wrong space. Applied only to an **unstroked**
  `<rect>`: a stroked shape is not one colour, so recolouring its fill would not
  describe what the filter does to it.
- **A pre-existing over-match found while testing this, and left alone.**
  `CollectFloodFilters` takes the first `feFlood` in a filter body whatever else
  is in the chain, so a filter that is `feColorMatrix` + `feFlood` is treated as
  flood-only. This change neither introduces nor fixes it; the test file records
  it and declines to pin it.
- **Measured.** `css/filter-effects` goes **180 → 181 passing** over 388 tests,
  the test itself **7.7% → 99.6%**, and the per-test diff across the whole subset
  is two lines: that test, and `fecolormatrix-display-p3` improving 97.2% → 98.0%
  without crossing the threshold (its residual is the Display-P3 colour space, a
  separate gap). **Nothing else moved in either direction.** 14 cases in
  `SvgColorFilterTests` cover the WPT chain, an identity matrix, the alpha rule,
  and six bail-outs.

### Chromium fails two of these reftests against its own reference — problems 28 and 29, **won't fix**

- **Tests:** `css/filter-effects/svg-filter-filter-units-user-space` and
  `svg-filter-primitive-units-user-space`, both 8.0% on CI and **both reproducing
  here at the same 8.0%**.
- **The reference is a fully green 1024×768 canvas** — for both tests. Rendering
  each test *and* its own `-ref.html` under Chromium settles what that means: the
  `-ref.html` is the six-container layout the test describes (green 75×75 in the
  150px `<svg>`, 100×100 in the 200px one, 50×50 for the filtered `<div>`s),
  while the test itself comes out uniformly green. **Chromium fails these
  reftests against their own references**, and the runner scores Broiler against
  Chromium's failing render.
- **Which element does it, isolated rather than assumed.** Reducing the test to
  one element at a time: a `<div>` carrying `filter: url(#f)` for a filter with
  `filterUnits="userSpaceOnUse"` and percentage `width`/`height` floods the
  **entire viewport** green, while the same filter on an SVG `<rect>` stays local
  and paints nothing outside the `<svg>`. So it is the CSS `filter: url()` path on
  an HTML element whose filter region resolves unbounded.
- **Broiler is already closer to the spec render than Chromium is.** Ours is the
  six-container layout with the flood regions off — the green boxes are the
  default `objectBoundingBox` region rather than the resolved
  `filterUnits`/`primitiveUnits` subregion — which is a real gap, but a *smaller*
  one than a whole-canvas flood. Passing the comparison would mean reproducing
  Chromium's flood, i.e. rendering strictly worse. Same trap as #1491's problems
  14/15 and #1538's problem 18; see the warning at the top of this document.
- **The underlying gap is still worth naming, for whenever the reference is
  fixed upstream.** Both regions are computable from what the tests contain:
  the filter region (`filterUnits`, defaulting to `objectBoundingBox` at
  −10%/+120%) intersected with the primitive subregion (`primitiveUnits`,
  defaulting to `userSpaceOnUse`, with `x`/`y`/`width`/`height` defaulting to the
  filter region and percentages resolving against the SVG viewport). Working the
  six containers through that by hand reproduces the `-ref.html` exactly in both
  tests. `SvgRenderer` currently hardcodes the default `objectBoundingBox` region
  and ignores the primitive subregion entirely.

### `<canvas>` is not a replaced element — problem 30, diagnosed, not fixed

- **Test:** `css/css-sizing/replaced-max-size-saturation`, 8.3% on CI.
  **No local percentage here** — the `css-sizing` subset was not run this session;
  the diagnosis below is from rendering this test and eight constructed probes and
  reading the pixels, not from a scored run. A `<canvas width=8000 height=8000>` under
  `max-width: 120px; max-height: 100px` must render as a **100×100** green square
  (CSS2.1 §10.4: both constraints violated, and `max-width/w > max-height/h`, so
  the height wins and the width follows the 1:1 ratio). Broiler renders it
  8000×8000, clipped by the viewport into a full-page green block.
- **Not a max-size bug — a replaced-element bug.** Five constructed probes
  separate the two: the same CSS on a `<div>` clamps correctly to 120×100, and so
  does an `<img>` with the same attributes; a `<canvas>` with `display: block`
  forced on it also clamps, to 120×100. Left alone, a `<canvas>` behaves exactly
  like a `<span>` with the same CSS. So `<canvas>` is being laid out as a
  **non-replaced inline** box, which is the one box type `max-width`/`max-height`
  do not apply to. `DomParser` special-cases `<video>`, `<audio>`, `<iframe>` and
  `<svg>` as replaced elements; `<canvas>` is missing from that list.
- **Three things are needed, and only the first is small.** (1) Model `<canvas>`
  as a replaced element sized from its `width`/`height` content attributes with
  the 300×150 default, mirroring the `<video>` handling in `DomParser`
  (`Broiler.HTML`, so a patch). (2) `max-height` on an `inline-block` is not
  applied either — a `display: inline-block` canvas probe clamps the width to 120
  and leaves the height unclamped, which is a second, separate gap. (3) Getting
  100×100 rather than 120×100 needs the CSS2.1 §10.4 constrained-ratio algorithm,
  which considers both violated constraints together; the current code applies
  min-width, max-height and min-height one at a time and has **no max-width arm at
  all** (`CssLayoutEngine`, main repo). Left for a session that can do all three.

### A `visibility: hidden` box stopped clipping its visible descendants — problem 20, **fixed**

- **Test:** `css/css-overflow/overflow-scroll-resize-visibility-hidden`, 5.9% on
  CI and reproducing here. Two `visibility: hidden` 100×100 scrollers each hold a
  1000×1000 green child that re-declares `visibility: visible`; the reference is
  the two 100×100 green squares the scrollers clip it to. Ours was the whole
  viewport green.
- **Owner:** `Broiler.HTML` (`IR/PaintWalker.Stacking.cs`), so it ships as
  [`patches/0121`](../patches/README.md) — the push is 403, as `CLAUDE.md`
  describes — and it is listed in `scripts/apply-pending-wpt-patches.sh` so the
  WPT run exercises it on CI.
- **Neither `resize` nor `scroll` is the interesting part of the test name.**
  Five constructed probes separate the variables: the same scroller with
  `visibility: visible` clips correctly to 100×100 **with and without
  `resize: both`**, and swapping `overflow: scroll` for `overflow: hidden` makes
  no difference either. The single variable that changes the outcome is
  `visibility: hidden` on the scroller — which then paints 999×759 of green,
  i.e. unclipped to the edge of the viewport.
- **The cause is one early return.** `PaintWalker.PaintFragment` handles a
  non-visible fragment by calling `PaintChildren` and returning — correctly, since
  CSS2.1 §11.2 makes `visibility: hidden` suppress only the box's *own* rendering
  and a descendant may re-declare `visible`. But that early return jumps over
  everything the visible path sets up afterwards, including the overflow clip. The
  box is still generated and still clips; only its own painting is suppressed. The
  clip is now pushed around the child walk in both places that walk children of a
  hidden fragment (`PaintFragment` and `PaintFragmentBackgroundPhase`).
- **Not changed: the foreground phase.** `PaintFragmentForegroundPhase` returns on
  a non-visible fragment *without* descending at all, so a visible descendant of a
  hidden box inside a table never paints. That is a separate pre-existing gap with
  a different fix, and no test on this list needs it.
- **Measured.** The test goes **5.9% → 100%**, the 772-test `css/css-overflow`
  subset **441 → 442 passing**, and the per-test diff across that whole subset is
  **two lines** — that test leaving the failures and joining the passes, with
  nothing else moving in either direction.
- **Checked for over-reach on the subset that shares the clip path.** `contain:
  paint` reaches the same `ClipsOverflow` predicate, so `css/css-contain` was
  re-run: **583 result lines identical to the pristine pre-change baseline**, and
  the aggregate (413 passing, 96.57% average match) is unchanged to the decimal.
  10 cases in `VisibilityHiddenOverflowClipTests` cover every clipping `overflow`
  value plus `contain: paint`, `visibility: collapse`, the `resize: both` the test
  is named for, and three controls — that a hidden `overflow: visible` box must
  *not* start clipping, that a visible scroller clips as it always did, and that
  the child still paints *inside* the clip (a fix that simply suppressed the
  visible descendant would pass the clip assertion and be wrong).

### Replacing the document element renders nothing — problem 17, diagnosed, not fixed

- **Test:** `quirks/tables-inherit-color-from-body-quirk-007`, 5.1% on CI. The
  reference is the UA dark canvas (`html { color-scheme: dark }`) with light text
  and a white 200px Ahem square. **Ours is a blank white page** — not a colour
  mistake, nothing rendered at all.
- **The quirk the test is named for never gets a chance to matter.** The test
  builds its content in a `<div>`, appends it to the document element, then does
  `document.documentElement.remove()` and `document.append(root.cloneNode(true))`
  — the point being that `<body>` is never created, so the "tables inherit color
  from body" quirk has no body to inherit from. Six probes isolate which step
  loses the render:
  - static markup with the same content and `color-scheme: dark` → dark canvas,
    renders;
  - script building the `<div>` and appending it to the document element → dark
    canvas, renders (so scripting and the append are fine);
  - adding `documentElement.remove()` + `document.append(clone)` → **white, empty**.
  - `documentElement.remove()` alone → white, as it should be;
  - `remove()` then appending a **hand-built** `<html><body>` with a lime block →
    still white;
  - `remove()` then appending a clone carrying a lime block → still white.
- **So the finding is not about `cloneNode`, and not about quirks.** After
  `documentElement.remove()`, appending *any* element to `document` does not
  install it as the new document element: the render stays empty. That is the
  whole 5.1%, and it is a DOM/bridge-level gap (the render tree is built from a
  document root that was never re-established), not a paint or cascade one.
- **Exit gate for whoever takes it:** the fifth probe above — remove the document
  element, append a fresh `<html>` containing a 200×200 lime block, and get lime
  pixels. Everything the test actually asserts is downstream of that.

### Problem 18 is not a subgrid bug — diagnosed, not fixed

- **Test:** `css/css-grid/subgrid/orthogonal-writing-mode-006`, 5.6% on CI.
- **Its reference is the same markup with one declaration removed.** The
  `-ref.html` is byte-for-byte the test except that `.grid > .grid` drops
  `grid-template: subgrid / subgrid` — so the test asserts that a subgrid whose
  parent declares no explicit tracks lays out exactly like a plain nested grid.
- **Broiler renders the test and its own reference to the identical PNG.**
  Rendering both and comparing gives byte equality, which settles the attribution:
  `grid-template: subgrid / subgrid` changes nothing here (it is dropped), so
  **subgrid is a no-op in both directions and cannot be what fails**. The whole
  5.6% is the grid layout the test and the reference *share*.
- **What that shared layout gets wrong** is visible in one render: the body is a
  `display: grid` with `place-items: start`, so each of the eight cyan `.grid`
  children should shrink-wrap; ours stretch to the full viewport width, stack in a
  single column, and scatter the eight Ahem strings along the top. The vertical
  writing modes (`vertical-rl` on half the boxes) are the other half of it.
- **So the exit gate is not "implement subgrid".** It is `place-items: start`
  shrink-wrapping on a grid container plus orthogonal writing modes inside one —
  and the check that subgrid stays a no-op while that is fixed, since the
  reference depends on it being one.

### #1562 problems, at a glance

CI percentages are the issue's. **"—" means not measured here** — problems 17,
18 and 20–30 were investigated across this run's sessions; the rest point at the
section that already owns them, and their status is that section's, not a fresh
measurement.

| # | Test | CI | Local | Status |
| --- | --- | --- | --- | --- |
| 1 | `css-color-adjust/…/cross-origin-002.sub` | 0.0% | — | re-report of #1538 problem 2 — needs `.sub` substitution and a second host |
| 2 | `css-page/page-margin-002-print` | 0.0% | — | re-report of #1538 problem 3 — [screen-layout gaps](#screen-layout-gaps-behind-the-three-print-html-tests) |
| 3, 4 | `css-view-transitions/nested/nested-{position-with-border,root-capture-with-clip}` (2) | 0.0% | — | new to this list — the nested-transition family of [view transitions do not capture the document](#view-transitions-do-not-capture-the-document--still-open-2-will-not-be-won-here) |
| 5 | `resource-timing/initiator-type/frameset` | 0.0% | — | re-report of #1538 problem 9 — [frameset frames render nothing](#frameset-frames-render-nothing) |
| 6 | `css-color-adjust/…/mismatch-dynamic` | 0.0% | — | **won't fix** — #1538 problem 10: Chromium fails this reftest against its own reference |
| 7, 8 | `css-grid/…/grid-subgridded-to-grid-lanes/…` (2) | 0.8%, 0.9% | — | open — same `display: inline grid-lanes` family as #1538 problems 12/13, which Broiler drops as invalid because no stable browser ships it unflagged |
| 9, 10 | `css-view-transitions/auto-name{-from-id-shadow,}` (2) | 1.3% | — | **won't fix** — the `auto-name` family, #1538 problem 18: the reference is Chromium's unfeatured render |
| 11 | `css-view-transitions/view-transition-waituntil-animation-manipulation` | 1.3% | — | does not reproduce offline (98.46% at #1538 problem 19) — judge from CI |
| 12 | `css-view-transitions/root-to-shared-animation-start` | 1.5% | — | new to this list — needs the rasterised root snapshot, #1491 problems 19/21/23 |
| 13–16 | `css-view-transitions/massive-element-{left,right}-of-viewport-partially-onscreen-{new,old}` (4) | 2.0%, 2.6% | — | open — **half fixed** at #1538 problems 22/23/25/26; these still fail on the snapshot clone's box sizing |
| 17 | `quirks/tables-inherit-color-from-body-quirk-007` | 5.1% | — | open — **diagnosed**: after `documentElement.remove()`, appending *any* element to `document` does not install a new document element, so the page renders empty. Not a quirks bug. See above |
| 18 | `css-grid/subgrid/orthogonal-writing-mode-006` | 5.6% | — | open — **diagnosed, and mis-named**: Broiler renders the test and its own `-ref.html` to a byte-identical PNG, so subgrid is a no-op both ways. The gap is the grid layout they share. See above |
| 19 | `css-backgrounds/background-image-shared-stylesheet` | 5.7% | — | re-report of #1538 problem 1 — needs the server's `trickle` pipe |
| 20 | `css-overflow/overflow-scroll-resize-visibility-hidden` | 5.9% | **5.9% → 100%** | **fixed** — a `visibility: hidden` box still clips its visible descendants ([`patches/0121`](../patches/README.md)) |
| 21–26 | `css-contain/contain-{body,html}-bg-00{1,3,4}` (6) | 7.5% | **7.5% → 99.8%** | **fixed** — any containment on html or body disables propagation from body ([`patches/0120`](../patches/README.md)) |
| 27 | `css/filter-effects/fecolormatrix-negative` | 7.7% | **7.7% → 99.6%** | **fixed** — a colour-only filter chain over a solid fill recolours the shape; main repo, on CI immediately |
| 28, 29 | `css/filter-effects/svg-filter-{filter,primitive}-units-user-space` (2) | 8.0% | 8.0% | **won't fix** — the reference is an all-green canvas: Chromium fails both against their own `-ref.html`. Ours is already the closer render |
| 30 | `css-sizing/replaced-max-size-saturation` | 8.3% | — | open — **diagnosed**: `<canvas>` is not modelled as a replaced element, so it lays out as a non-atomic inline and `max-width`/`max-height` never apply. Three parts, see above |

## The next run (issue #1612, 2026-08-12)

7 603 failures, no incomplete shards. **This session worked the nine tests
reported at 0.0%** — problems 1 through 9 — and the result is lopsided enough to
be the headline:

- **One is a real engine bug.** Problem 8, `resource-timing/initiator-type/frameset`,
  is **fixed** — 0.0% → **99.7%, passing** — and it was not the bug its own
  section here had predicted.
- **Six render *correctly* and fail anyway**, because the Chromium golden image
  they are scored against was produced without the feature under test. Problems
  2, 3, 5, 6 and 7 are new to this list and all five are this shape; problem 9 is
  the already-settled member of it. In every one of the six, Broiler's render
  matches the test's **own `rel=match` reference** and Chromium's does not.
- **Two are re-reports that were already settled.** Problem 1 cannot be judged
  offline at all — it needs the WPT server. Problem 4 is judged and closed:
  its reference is a screenshot artifact, not a rendering gap.

So the honest count for this run's 0.0% tail is **one gap, fixed**, and eight
tests that say more about the reference-generation strategy than about the
engine. That ratio is itself the finding: the golden-image suite scores Broiler
against Chromium's pixels, and Broiler now implements several things Chromium
does not, so *shipping a feature moves these tests to 0.0%*. Problems 2 and 3
demonstrate it end to end — they were at 100% in the #1491 write-up and are back
at 0.0% *because* `image-animation` was implemented in the meantime.

### A root-relative frame `src` resolved against the wrong directory — problem 8, **fixed**

- **Test:** problem 8, `resource-timing/initiator-type/frameset.html`. CI 0.0%,
  local **0.0% → 99.7% (passing)**.
- **Owner:** Broiler.Layout (`FragmentTreeBuilder`), with the WPT runner.
- **The previous diagnosis was wrong.** [Frameset frames render
  nothing](#frameset-frames-render-nothing--fixed-see-1612-problem-8) had this as
  the frameset grid painting neither its canvas nor its frames' documents, and
  the next action was to render frames as nested browsing contexts on that grid.
  None of that was needed: the grid, the sub-viewport projection and the frame
  document load all already worked. Bisecting the test down found the whole
  difference in the URL — the same page with `src="../resources/green.html"`
  rendered its frame correctly, and only `src="/resource-timing/resources/green.html"`
  came out blank. `<frameset>` was a red herring, and so was `<frame>`: an
  `<iframe>` with a root-relative `src` failed identically.
- **The bug.** HTML §"resolve a URL" resolves a leading `/` against the
  document's origin. A `file://` render has no origin, and
  `FragmentTreeBuilder.TryLoadEmbeddedDocument` joined the URL onto the
  containing directory like any other relative reference —
  `Path.Combine(dir, "/resource-timing/…")`. `Path.Combine` **discards its left
  operand when the right one is rooted**, so the result was an absolute path at
  the filesystem root, `File.Exists` failed, and the frame painted empty. Silent,
  and it had nothing to do with framesets.
- **What landed, all main repo — on CI immediately, no patch.**
  `Broiler.Layout.Engine.DocumentRoot` is a thread-static, scope-restoring render
  lever (the shape of `CanvasBackdrop.Current` and `NativeZoom.Enabled`) carrying
  the directory a root-relative sub-document URL resolves against;
  `TryLoadEmbeddedDocument` takes a root-relative branch that reads from it,
  stripping the query and fragment and refusing to leave the root. The WPT runner
  pins it to the checkout around both render paths — the same root its
  stylesheet, image and script loaders already resolve `/`-paths against
  (`TryResolveWptRootRelativePath`). **This was the one sub-resource kind with no
  such hook.**
- **Null by default is the point.** A host that sets nothing renders exactly as
  before: an unresolvable root-relative frame stays the empty box it has always
  been. Nothing outside the runner changes behaviour.
- **Verified:** the test goes 0.0% → 99.7% and passes, against a locally
  generated Chromium reference — and the render is the *right* pixels, 99.8%
  `#00FF00` plus the reference's own `<h1>Placeholder</h1>` text, matching
  Chromium's 99.8%/0.1% split. The `resource-timing/initiator-type` subset goes
  8 → 9 passing with nothing lost. 13 focused cases pin the behaviour, including
  the negative halves that keep it honest: no root set → still empty; the root is
  *not* the page's own directory; `//host/path` is scheme-relative and must not
  be read off the local disk; `..` cannot escape the root; a bare `/` is not a
  document; and a directory-relative `src` is unaffected either way.
- **Left over, and genuinely a frameset bug this time:** a frameset with more
  than one frame paints only its first cell. `<frame>` is missing from
  `Broiler.DOM`'s void-element set, though `Broiler.HTML` has it, so
  `<frame src=a><frame src=b>` parses the second frame as a *child* of the first
  and `DomParser.LayoutFramesetChildren` is handed one cell instead of two.
  Confirmed by writing the same markup with explicit `</frame>` tags, which
  renders both cells. Fixed in [`patches/0003`](../patches/README.md) — the
  submodule remote 403s from here — and verified before the tree was reverted:
  both `cols` and `rows` framesets go from half-painted to both cells painting
  their own document, with a single-frame frameset and a two-iframe page
  unchanged. **No test in the current subset covers it**, and the test that
  motivated this work has exactly one frame, so nothing regresses while it waits.

### Six tests where Broiler is right and the reference is not — problems 2, 3, 5, 6, 7 and 9

All six were rendered here and compared against a Chromium reference generated in
this container, alongside the reference the test itself declares. The pattern is
identical in each: **Broiler matches the test's `rel=match` target; Chromium does
not.** Percentages are dominant-colour shares of the 1024×768 canvas.

| Problem | Test | Broiler renders | Chromium reference | The test's own `rel=match` |
| --- | --- | --- | --- | --- |
| 2 | `image-animation-body-background-root-propagation-paused` | 100% `#00FF00` | 100% `#FF0000` | `…-ref.html` → `green.png` |
| 3 | `image-animation-root-background-paused` | 100% `#00FF00` | 100% `#FF0000` | `…-ref.html` → `green.png` |
| 5 | `mediaqueries/at-custom-media-basic` | 100% `#008000` | 100% white | `/css/reference/green.html` → `background: green` |
| 6 | `fullscreen/rendering/backdrop-iframe` | 99.1% `#008000` | 98.7% white | `backdrop-green-ref.html` → `background: green` |
| 7 | `fullscreen/rendering/backdrop-inherit` | 100% `#008000` | 98.9% white | `backdrop-green-ref.html` → `background: green` |
| 9 | `color-scheme-iframe-background-mismatch-dynamic` | 99.8% white | 99.7% `#121212` | `support/light-frame-scrolling.html` → white |

Why each reference is what it is:

- **Problems 2 and 3 — `image-animation: paused`, and a worked example of the
  trap.** These two were reported *fixed at 100%* in the [#1491
  write-up](#animated-images-always-painted-their-first-frame--fixed-pending-patch):
  frame selection put the 300 ms screenshot on the red frame, which is what
  Chromium shows. `image-animation` has since been implemented
  (`CssBox.ImageAnimation.cs`), so Broiler now honours `paused` and holds the
  green first frame — which is precisely what `…-ref.html` asks for, and precisely
  what Chromium, which does not implement the property, does not do. **The two
  that flipped are exactly the two canvas-propagation cases**, and the other two
  of the family did not, for reasons worth knowing:
  `image-animation-background-paused` paints its two 20×10 boxes green against
  Chromium's red and still scores **99.9%**, because the disagreement is 0.1% of
  the canvas — it passes the 99% threshold by being small, not by being right;
  and `image-animation-body-background-no-propagation-paused` asks for the
  *unpaused* render (`red.png`) and gets it, because propagation hands the
  canvas the **root's** `image-animation`, not body's. That asymmetry is
  modelled, and it is why only two of four are on this list.
- **Problem 5 — `@custom-media`.** `@custom-media --foo (width > 0px);` plus
  `@media (--foo) { :root { background: green } }`. Broiler implements Media
  Queries 5 §3 (`CssStyleEngine.Values.cs`, with substitution and cycle detection
  covered by `CssStyleEngineTests`) and paints the green the reference asks for.
  Chromium does not implement it, so the `@media (--foo)` block never matches and
  its screenshot is white.
- **Problems 6 and 7 — fullscreen `::backdrop`.** Both call `requestFullscreen()`
  through `test_driver.bless`, which needs WebDriver; the plain Playwright
  reference generator provides none, so Chromium never enters fullscreen, no
  `::backdrop` is generated, and the screenshot is the un-activated page. Broiler
  runs the blessed callback (the runner's `test_driver` shim, covered by
  `FullscreenRenderTests`), promotes the element into the top layer and paints its
  `::backdrop` green. Problem 7 is the stronger evidence that this is real rather
  than a backdrop painted indiscriminately: it sets `--bg: red` on `body` and
  `--bg: green` on the `div`, and asserts `div::backdrop` inherits from the
  *fullscreen element*. Broiler renders green — inheriting from the div. A
  backdrop painted from the wrong parent would be red.
- **Problem 9 — settled previously, unchanged.** Chromium fails this reftest
  against its own `rel=match` reference; see [#1497 problem
  25](#the-next-run-issue-1497-2026-07-30). Ours matches the reference.

**None of the six should be "fixed".** Each would require deleting working
support, and problems 2 and 3 are the proof that the cost is real rather than
theoretical: the engine got better and the score got worse.

### The two that cannot be judged here — problems 1 and 4

- **Problem 1** (`color-scheme-…-opaque-cross-origin-002.sub`) needs `.sub`
  substitution and a real cross-origin host; unchanged from [items that need the
  WPT server](#items-that-need-the-wpt-server-before-they-can-be-judged).
- **Problem 4** (`css-page/page-margin-002-print`) is a screenshot artifact, not
  a rendering gap: Chromium's own *viewport* capture of a `vertical-rl` root is
  blank while its full-page capture paints all three blocks. Established under
  [screen-layout gaps](#screen-layout-gaps-behind-the-three-print-html-tests);
  matching it would mean drawing nothing.

### #1612 problems, at a glance

Local numbers are this container's, against Chromium references generated here.

| # | Test | CI | Local | Status |
| --- | --- | --- | --- | --- |
| 1 | `css-color-adjust/…/cross-origin-002.sub` | 0.0% | — | re-report — needs `.sub` substitution and a second host |
| 2 | `css-image-animation/image-animation-body-background-root-propagation-paused` | 0.0% | 0.0% | **won't fix** — ours matches the test's `rel=match` (green); Chromium has no `image-animation` |
| 3 | `css-image-animation/image-animation-root-background-paused` | 0.0% | 0.0% | **won't fix** — same |
| 4 | `css-page/page-margin-002-print` | 0.0% | — | **won't fix** — the reference is a `vertical-rl` viewport-screenshot artifact |
| 5 | `mediaqueries/at-custom-media-basic` | 0.0% | 0.0% | **won't fix** — ours matches `/css/reference/green.html`; Chromium has no `@custom-media` |
| 6 | `fullscreen/rendering/backdrop-iframe` | 0.0% | 0.0% | **won't fix** — ours matches `backdrop-green-ref.html`; the reference never entered fullscreen (no WebDriver) |
| 7 | `fullscreen/rendering/backdrop-inherit` | 0.0% | 0.0% | **won't fix** — same, and ours inherits `--bg` from the fullscreen element as the test asserts |
| 8 | `resource-timing/initiator-type/frameset` | 0.0% | **0.0% → 99.7%** | **fixed** — a root-relative `<frame src>` resolved against the page's directory; main repo, on CI immediately |
| 9 | `css-color-adjust/…/mismatch-dynamic` | 0.0% | 0.0% | **won't fix** — Chromium fails this reftest against its own reference |

## The next run (issue #1615, 2026-08-12)

7 593 failures, no incomplete shards. **This run's 0.0% list is the previous
run's, minus the one that was fixed** — the eight tests reported here are #1612's
problems 1–7 and 9, and `resource-timing/initiator-type/frameset` has dropped off
because it now passes. So the six *won't fix* verdicts and the one
screenshot-artifact verdict all stood; every one was re-rendered here and
reproduced its documented colours to the tenth of a percent (100 % `#00FF00` for
both `image-animation` tests, 100 % `#008000` for `at-custom-media-basic` and
`backdrop-inherit`, 99.1 % for `backdrop-iframe`, 99.8 % white for
`mismatch-dynamic`, 99.95 % yellow for `page-margin-002-print`).

That left exactly one test with any headroom: **problem 1, the one that could not
be judged offline at all.** It can now, and it passes.

### The runner never performed WPT's `.sub` substitution — problem 1, **fixed**

- **Test:** problem 1,
  `css/css-color-adjust/rendering/dark-color-scheme/color-scheme-iframe-background-mismatch-opaque-cross-origin-002.sub.html`.
  CI 0.0%, local **0.0% → passing**, and 100.00 % pixel-identical to the test's
  own `rel=match` reference.
- **Owner:** the WPT runner (`src/Broiler.Wpt`) and its reference generator, with
  a two-line hook in `Broiler.Layout`.
- **The diagnosis this replaces.** [Items that need the WPT
  server](#items-that-need-the-wpt-server-before-they-can-be-judged) had this
  test as unreproducible without a real server and a second host, and the next
  action was to decide whether to serve the suite over a local HTTP origin. No
  server is needed. A `.sub` file is a **template**: WPT's server expands
  `{{host}}`, `{{ports[http][0]}}` and friends before sending it
  (`tools/wptserve/wptserve/handlers.py` — the rule is a literal `".sub." in
  path`). Read straight off disk the placeholders survive into the markup, so
  this test's frame pointed at the uninterpretable URL
  `http://{{hosts[alt][]}}:{{ports[http][0]}}/css/…/support/light-frame.html`,
  loaded nothing, and the page rendered 100 % `#121212` — the parent's dark
  canvas showing through an empty frame. **Neither side of the comparison
  substituted**, so Chromium's golden was blank for the same reason and the score
  was meaningless in both directions. There are **1 419 `.sub.html` files** in
  the tree; 51 of them are reftests.
- **The second half: WPT's hosts are all one checkout.** Substituting alone only
  moves the problem — the URL becomes `http://not-web-platform.test:8000/css/…`,
  which a local render still cannot fetch. But WPT serves *every* one of its
  hosts from the same document root, so that URL and `/css/…` name one file.
  Recognising that is what makes a cross-origin frame reachable offline.
- **What landed, all main repo — on CI immediately, no patch.**
  `WptSubstitution` performs the substitution with WPT's own defaults
  (`tools/serve/serve.py`): primary host `web-platform.test`, alternate
  `not-web-platform.test`, the subdomain set closed under the two-deep products
  `_make_subdomains_product` builds, IDNA-encoded, and ports pinned to the
  documented numbers so a render is reproducible. `Broiler.Layout.Engine.DocumentRoot`
  — the lever [#1612 problem 8](#a-root-relative-frame-src-resolved-against-the-wrong-directory--problem-8-fixed)
  added — gains a list of hosts served from that root, and `FragmentTreeBuilder`
  strips such an origin before its existing root-relative branch. The runner's
  stylesheet and image loaders take the same view through
  `TryResolveWptRootRelativePath`. `scripts/generate-wpt-references.js` mirrors
  both halves, because the two sides must render the same bytes.
- **Only what a file on disk can answer is substituted.** `{{uuid()}}`,
  `{{headers[…]}}`, `{{GET[…]}}`, `{{file_hash(…)}}` and `{{$var}}` describe a
  live request and are left **verbatim**, so a test using them renders exactly as
  it did before. `{{not_domains[nonexistent]}}` is left alone for a stronger
  reason — it names a host that is *meant* not to resolve — and the served-host
  match is exact rather than by suffix for the same reason, so
  `nonexistent.web-platform.test` is never served content.
- **Empty by default.** A host that declares no served hosts renders byte-for-byte
  as before: an absolute URL stays the unfetchable empty box it has always been.
- **CACHE_EPOCH is bumped to 7.** Every `.sub` test's golden changes, so the
  cached reference slice must be regenerated or the two sides disagree by
  construction.
- **Verified:** 0.0% → passing, and the render is the *right* pixels —
  **100.00 % identical** to `support/light-frame.html`, the reference the test
  itself declares. 47 focused substitution cases and 16 frame-loading cases pin
  it, including the negative halves: an unknown or request-scoped placeholder
  left verbatim, `not_domains` left verbatim, an unlisted host and a subdomain of
  a listed one both refused, a non-http scheme left alone, `..` unable to escape
  the root, and a served URL naming nothing still painting empty. The reference
  generator's own 28 node tests assert the JS mirror agrees case for case.

### What else moved, and the one it exposed

Measured before/after across three directories, each with locally generated
Chromium references on **both** sides so the comparison is like for like:

| Subset | Tests | Before | After |
| --- | --- | --- | --- |
| `css/css-color-adjust/rendering/dark-color-scheme` | 29 | 22 passing | 22 passing |
| `css/css-values/urls` + `html/syntax/speculative-parsing/…/document-write` | 121 | 121 passing | 121 passing |

Outside the cross-origin-iframe family the change is inert — 121 tests,
identical both ways. Inside it, four tests moved, and the net is zero because two
of them were **passing for the wrong reason**. (The frame-canvas fix below then
took that directory to **24 passing**; the four moves are recorded as the
substitution change left them, because the second of them is what pointed at the
bug.)

- **Gained (2).** `…-opaque-cross-origin-002.sub` (the problem above) and
  `color-scheme-iframe-preferred-page-dark-cross-origin.sub` both load their
  frame now and match.
- **Lost, and correctly (1).** `…-mismatch-dynamic-cross-origin.sub` is the
  cross-origin twin of problem 9, and it fails for exactly the same reason its
  same-origin twin does: Broiler renders **100.00 % identical to the test's own
  `rel=match`** (`support/light-frame-scrolling.html`, white), while Chromium's
  reference is 99.8 % `#121212`. It used to pass only because *both* engines
  rendered an empty frame. Same **won't fix** class as problem 9.
- **Lost, and it is a real bug (1).** `…-opaque-cross-origin-003.sub` painted a
  200×200 white box that should not be there. **Since fixed** — see
  [the frame-canvas section below](#a-frames-canvas-was-never-transparent--003sub-and-iframe-background-fixed-pending-patch).
  Worth keeping the shape of it on record: the test's former pass is exactly what
  this document calls **untrustworthy** — passing by rendering nothing — and a
  truthful failure that named a real gap was worth more than a green tick that
  depended on a frame never loading.

### A frame's canvas was never transparent — 003.sub and `iframe-background`, **fixed, pending patch**

- **Tests:** `css/css-color-adjust/rendering/dark-color-scheme/color-scheme-iframe-background-mismatch-opaque-cross-origin-003.sub`
  (94.7 % → **99.8 %, passing**) and `…/color-scheme-iframe-background`
  (69.0 % → **98.9 %**), plus `…/color-scheme-iframe-background-mismatch-used-preferred`
  (94.6 % → **99.5 %, passing**) which fell out with them.
- **Owner:** `Broiler.HTML` (`HtmlRender`, `PaintWalker.CanvasBackground`) for the
  renderer half; `Broiler.Layout` for the rule and the cascade fix.
- **The rule.** CSS Color Adjust 1 §2.4: a nested browsing context's canvas is
  **transparent** — the embedder shows through it — *unless* the used colour
  scheme of the **embedding element** differs from the embedded root's, in which
  case the UA paints an opaque backdrop of the embedded root's scheme. The
  comparison is element-to-root, not document-to-document, and these two tests are
  built on precisely that: one puts a dark frame in a dark-scheme `<iframe>` on a
  light page, the other a light frame in a light-scheme `<iframe>` on a dark page,
  and both ask for the page to show through.
- **Two bugs, not one.**
  1. **The canvas was always opaque.** `RenderToImageCore` erased every embedded
     document to its resolved canvas colour, `PaintWalker.EmitCanvasBackground`
     painted the UA dark fill unconditionally, and `BlitOnto` copied the result
     pixel-for-pixel with no alpha. A frame could not be transparent at all, so
     the embedding element's `color-scheme` was never consulted.
  2. **`color-scheme` did not inherit.** §2.1 makes it an inherited property, but
     it was missing from `CssBoxProperties.InheritStyle` — unnoticed because it was
     only ever read off the root element, which inherits nothing. An `<iframe>`
     under `html { color-scheme: dark }` therefore reported `normal`. Fixing only
     the first bug regressed `…-002.sub` (the frame went transparent when the
     schemes genuinely *did* differ); the two have to land together.
- **What landed where.** `Broiler.Layout.Engine.EmbeddedCanvas` is the rule — a
  thread-static, scope-restoring lever like `CanvasBackdrop` and `DocumentRoot`,
  carrying the embedding element's computed `color-scheme` and answering
  `PaintsOpaqueBackdrop`. Unpinned means "not embedded", so it answers `true` and
  a top-level render is byte-identical. That, the inheritance fix, and the WPT
  runner's own frame compositor (`WptDocumentRenderer`, which pins the lever and
  composites source-over) are **main repo**. The renderer's side is
  [`patches/0004`](../patches/README.md) — the `Broiler.HTML` remote 403s from
  here — and until it is applied the two tests keep their current scores.
- **Verified:** the dark-color-scheme directory goes **22 → 24 of 29** with
  nothing lost, and `html/semantics/embedded-content/the-iframe-element` is
  **unchanged across all 161 tests** — the change is inert for a frame that fills
  its own canvas, which is nearly all of them. 22 focused cases cover the rule,
  the cascade and the render, four of them probing for the patch so they become
  real guards when the pointer is bumped.
- **A separate gap the fix uncovered — since fixed.** `color-scheme-iframe-background`
  stopped at 98.9 % rather than passing, on a residual that was not colour-scheme
  related at all: the default `<iframe>` border. See
  [the bevel section below](#a-3d-border-was-painted-flat--fixed-pending-patch).

### A 3D border was painted flat — **fixed, pending patch**

- **Tests:** `css/css-color-adjust/rendering/dark-color-scheme/color-scheme-iframe-background`
  (98.9 % → **99.4 %, passing**, on top of the frame-canvas fix), and 89 tests
  across `html/rendering` and `html/semantics/embedded-content/the-iframe-element`
  that carry an `<iframe>` or an `<hr>`.
- **Owner:** `Broiler.HTML` (`PaintWalker.Decorations`, `CssDefaults`) for the call
  and the UA base; `Broiler.Layout` for the rule.
- **The gap.** CSS 2.1 §8.5.3 paints `inset`, `outset`, `groove` and `ridge` as a
  bevel — two sides in a darkened shade of the border colour, two in the colour
  itself. The IR paint path used the colour flat on all four sides, so the border
  the HTML Standard puts on every `<iframe>` and `<hr>` (`border: 2px inset`) came
  out **solid black** where every browser paints `#9A9A9A` over `#EEEEEE`, and the
  `border: 2px groove` it puts on every `<fieldset>` came out flat too. On a
  600×400 frame that ring is 4 012 px — half of the test's residual, and exactly
  the half that kept it under the threshold.
- **Measured, not guessed.** The spec leaves the shades to the UA, so the rule
  came from screenshotting Chromium and sampling each side. The darkened side
  scales all three channels by the factor that takes the *largest* one down by
  0.33 of full intensity — which is what keeps the hue: `rgb(200,100,50)` darkens
  to `rgb(116,58,29)`, all ×0.58, where the per-channel subtraction the greys
  alone suggested would have given `rgb(116,16,0)` and turned brown into red. The
  lit side is the colour itself, except black, whose lit side is `#545454`.
- **The second half is the UA stylesheet.** CSS makes the initial `border-color`
  `currentColor`, which bevels black-on-black; browsers substitute a light grey at
  paint time. Broiler states that grey in the UA stylesheet instead — which is
  what `hr` already did, with the *result* of the bevel hard-coded per side
  (`#9A9A9A`/`#EEEEEE`). Those four declarations collapse to one
  `border-color: #EEEEEE` now the engine derives the pair, and `iframe` gets the
  same base. **The two halves must land together:** shading while `hr` still
  carried the pre-bevelled colours would darken `#9A9A9A` a second time and
  regress every `<hr>`, which is why the call sits in
  [`patches/0005`](../patches/README.md) rather than in `ComputedStyleBuilder`.
- **Verified:** across 665 tests of `html/rendering` and the iframe element,
  **89 changed and every one of them improved** — none worse — with one more
  passing; many went 99.7–99.8 % to 100.0 %. `hr` renders identically to before.
  30 focused cases pin the shading numbers against the Chromium measurements.
- **`groove` and `ridge` split each side lengthwise**, and are emitted as two
  nested rings rather than one. A groove reads as `inset` on its outer half and
  `outset` on its inner half; a ridge is the mirror. The split sits at
  `ceil(width / 2)` from the outer edge — a 3px groove is two dark rows then one
  light, a 5px one three then two — and below 2px there is no room for two halves,
  where Chromium paints a single stroke of the *lit* shade on all four sides. That
  1px case is the one place the two styles agree and the only one that is not a
  split; it was found by measuring all four sides rather than just the top, which
  is where a per-side rule would have looked right and been wrong.
  - **Verified:** five more tests moved, all improvements, the largest
    `fieldset-vertical` at +0.67 points — `<fieldset>` is rendered
    `border: 2px groove`, so it is the element this reaches most. Against Chromium
    directly, a page of groove and ridge boxes matches to **99.95 %**.
- **The corner miters — since fixed.** The 0.05 % residual above was the 45°
  diagonal between two differently-coloured sides: Broiler stepped it, Chromium
  feathers it. See [the miter section below](#a-border-corner-had-no-mitre-and-no-anti-aliasing--fixed-pending-patch).
- **Also remaining:** the other half of `color-scheme-iframe-background`'s original
  residual (≈ 4 356 px) is text antialiasing inside the frame, unrelated to
  borders and below the threshold now that the bevel is right.

### A border corner had no mitre, and no anti-aliasing — **fixed, pending patch**

- **Owner:** `Broiler.HTML` (`RGraphicsRasterBackend`, `BCanvas`) for the paint;
  `Broiler.Layout` for the coverage rule.
- **Two gaps, found one behind the other.** CSS 2.1 §8.5.4 divides a border at its
  corners by a straight line.
  1. **A stroke has no mitre.** Only `solid` was painted as a trapezoid; `inset`,
     `outset`, `groove` and `ridge` were stroked along their centre lines, and a
     stroke butts square into its neighbour — so whichever side was drawn last
     owned the whole corner. Invisible while two sides share a colour, glaring
     when they do not, which is exactly what a `groove` does.
  2. **The mitre was a staircase.** Filled by testing each pixel's centre, the
     diagonal steps one pixel per row where a browser lays one blended pixel
     along it.
- **Coverage, not a 45° special case.** The miter is only diagonal when the two
  sides are equally wide. A 12px top against a 4px left slopes one-in-three, and
  the pixel coverages come out 1/6, 1/2, 5/6 — against Chromium's measured 0.158,
  0.503, 0.842. The rule is the area of the pixel the trapezoid covers, and it
  reproduces both.
- **Only the mitres blend.** The first attempt anti-aliased *every* edge of the
  trapezoid and regressed 210 tests, five of them out of passing. A border's own
  edges are straight lines the layout puts where it puts them, and feathering them
  turns a 1px form-control border sitting on a half-pixel into two grey rows
  instead of one solid one. Axis-aligned edges keep the pixel-centre test; only
  the diagonals carry coverage. **That failure is the useful part of this entry** —
  the obvious version of the fix is the wrong one, and only a broad measurement
  said so.
- **Why it is opt-in.** Two trapezoids meeting along a mitre each cover about half
  of the pixels on it, so blended independently onto the page they leave the
  background showing through the seam. The corner rectangle already filled for
  same-coloured sides is now filled for every corner, with the colour of whichever
  side is drawn first, so the second blends over an opaque corner. A translucent
  side disables the whole thing, since that fill would composite its alpha twice.
- **Verified:** against Chromium directly, a 12px four-colour border's corners go
  from **48 differing pixels to 12** (the rest off by 1/255) with the corner pixel
  now exact, and a page of groove and ridge boxes from **425 to 21**. Across
  1 949 tests of `css/css-backgrounds`, `html/rendering`, `css/css-gaps` and this
  directory, **no test changes state in either direction** and the net is
  **+1.578 points** (+1.707 across 55 tests against −0.129 across 103, one of
  which loses more than a hundredth of a point). Ships as
  [`patches/0006`](../patches/README.md).

### #1615 problems, at a glance

Local numbers are this container's, against Chromium references generated here.

| # | Test | CI | Local | Status |
| --- | --- | --- | --- | --- |
| 1 | `css-color-adjust/…/opaque-cross-origin-002.sub` | 0.0% | **0.0% → passing** | **fixed** — the runner performs WPT's `.sub` substitution and serves WPT's hosts from the checkout; main repo, on CI immediately |
| 2 | `css-image-animation/image-animation-body-background-root-propagation-paused` | 0.0% | 100% `#00FF00` | **won't fix** — re-verified; ours matches the test's `rel=match`, Chromium has no `image-animation` |
| 3 | `css-image-animation/image-animation-root-background-paused` | 0.0% | 100% `#00FF00` | **won't fix** — same |
| 4 | `css-page/page-margin-002-print` | 0.0% | 99.95% `#FFFF00` | **won't fix** — re-verified; the reference is a `vertical-rl` viewport-screenshot artifact |
| 5 | `mediaqueries/at-custom-media-basic` | 0.0% | 100% `#008000` | **won't fix** — re-verified; ours matches `/css/reference/green.html`, Chromium has no `@custom-media` |
| 6 | `fullscreen/rendering/backdrop-iframe` | 0.0% | 99.1% `#008000` | **won't fix** — re-verified; the reference never entered fullscreen (no WebDriver) |
| 7 | `fullscreen/rendering/backdrop-inherit` | 0.0% | 100% `#008000` | **won't fix** — same |
| 8 | `css-color-adjust/…/mismatch-dynamic` | 0.0% | 99.8% white | **won't fix** — re-verified; ours matches the test's `rel=match`, Chromium fails it |

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
| 12 | `css-page/page-margin-002-print` | 0.0% | ours yellow, Chromium white | **won't fix** — the reference is a `vertical-rl` viewport-screenshot artifact ([screen-layout gaps](#screen-layout-gaps-behind-the-three-print-html-tests)) |
| 13 | `css-transforms/animation/transform-interpolation-002` | 0.0% | 100% — both empty offline | open |
| 14, 15 | `css-view-transitions/auto-name*` (2) | 0.0% | ours captures both items + backdrop; Chromium drops `view-transition-name: auto` | **won't fix** — reference is the unfeatured render |
| 16, 17 | `css-view-transitions/iframe-and-main-frame-*` (2) | 0.0% | ours 99.5% white, Chromium 74.5% green + 25% blue | open — needs a transition in a nested browsing context |
| 18 | `css-view-transitions/nested/compute-explicit-name-non-ancestor.tentative` | 0.0% | 100% — reference is a blank white canvas | ~~**untrustworthy** — passes only by rendering nothing~~ **stale, and since fixed**: the reference is now 100% green, and an explicit `view-transition-group` name matching a non-ancestor was the cause. See [#1538 problem 7](#an-earlier-verdict-that-no-longer-held--problem-7-re-triaged-and-then-fixed) |
| 19, 21 | `css-view-transitions/old-/new-content-captures-root` (2) | 0.0% | ours 98.7% pink (backdrop through the page) | open — needs a rasterised root snapshot |
| 20, 22 | `css-view-transitions/*-root-scrollbar-with-fixed-background` (2) | 0.0% | 100% — reference is 99% `lightblue`, genuine | passing locally |
| 23 | `css-view-transitions/root-captured-as-different-tag` | 0.0% | ours 100% red (the `(root)` trap rule) | part-fixed — the `(root)` rules no longer match; still needs the root snapshot |
| 24 | `canvas/…/manual/dialog-paints-in-top-layer.tentative` | 0.0% | ours dialog, Chromium blank (unsupported) | **fixed** — reclassified Manual |
| 25 | `the-link-element/stylesheet-with-base` | 0.0% | ours red (trap file), Chromium white | **fixed** — renders green locally |
| 26 | `resource-timing/initiator-type/frameset` | 0.0% | **0.0% → 99.7%** | **fixed** at [#1612 problem 8](#a-root-relative-frame-src-resolved-against-the-wrong-directory--problem-8-fixed) — a root-relative `<frame src>`, not the frameset grid; main repo |
| 27 | `dom/nodes/moveBefore/preserve-render-blocking-style` | 0.0% | ours white, Chromium green | **fixed** — but only at the *second* attempt; `moveBefore` (patch 0038) was half of it, and the test stayed at 0.0% until `<link>` got its IDL reflectors. See [the #1497 section](#the-next-run-issue-1497-2026-07-30) |
| 28 | `forced-colors-mode/forced-colors-mode-20` | 0.0% | ours black, Chromium white | **fixed** — patch 0036 applied |
| 29 | `shadow-dom/focus-navigation/delegatesFocus-highlight-sibling` | 0.0% | ours flat `#cccccc` — a template's styles leaking into the page | **0.0% → 97.8%** with patch 0042; residual is inline-block line height |
| 30 | `css-page/page-box-008-print` | 0.0% | ours hotpink, Chromium yellow | **`vb` fixed** — patches 0036/0037 applied |
