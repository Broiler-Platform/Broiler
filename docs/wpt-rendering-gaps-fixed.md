# WPT rendering gaps — fixed

> Part of the [WPT rendering gaps](wpt-rendering-gaps.md) set:
> **fixed** · [not fixed](wpt-rendering-gaps-open.md) · [won't fix](wpt-rendering-gaps-wont-fix.md).
> Every status here was re-measured on **2026-08-13**; see
> [How this was verified](wpt-rendering-gaps.md#how-the-2026-08-13-split-was-verified).

Gaps that are closed. Each entry keeps the root cause, what landed, the evidence,
and — where there was one — the wrong turn worth not repeating. **Every submodule fix
named below is now an ancestor of its pinned pointer**, the media-element one included
(it landed upstream as `Broiler.HTML` `a9be60a`), so all of them reach CI through the
pointer and none is waiting on `scripts/apply-pending-wpt-patches.sh`.

Where a fix landed in a submodule it is identified by its **commit subject**, not
by a patch number. Patch numbers named nothing durable — `patches/` was a backlog,
not an archive, and numbering restarted from `0001` each time it drained. To check
one:

```sh
git -C <Submodule> log --oneline --grep '<commit subject>'
git -C <Submodule> merge-base --is-ancestor <sha> HEAD
```

## Contents

- [The runner and the report](#the-runner-and-the-report)
- [CSS engine](#css-engine)
- [Layout](#layout)
- [Paint and the renderer](#paint-and-the-renderer)
- [DOM and the bridge](#dom-and-the-bridge)
- [View transitions](#view-transitions)
- [Conformance fixes that closed no test](#conformance-fixes-that-closed-no-test)

---

## The runner and the report

### The run reported correct renders as its worst failures

- **Owner:** the WPT runner (`src/Broiler.Wpt`), the shard merger
  (`scripts/merge-wpt-shards.py`) and the workflow's shard action. Main repo.
- **The bug.** The golden-image suite scores Broiler against *Chromium's* pixels.
  When Broiler implements something Chromium does not, the test drops to 0.0% and
  stays there permanently, by construction. The severity issue ranked strictly by
  `100 − matchPercent`, so those tests took the top of the list every run and
  pushed real bugs off it. Three consecutive runs' severity lists were almost
  entirely tests that had already been triaged and judged correct.
- **The evidence was already available and never gathered.** The runner had had a
  `--verify-reference` switch for some time
  (`WptTestRunner.VerifyAgainstReferenceHtml`): on a pixel-mismatch failure it
  re-renders the test's own `rel=match` reference and records `suspectReference`
  when Broiler reproduces *it* but not the committed PNG. The flag was serialised
  into the per-test results and **nothing ever ran it** — CI never passed the
  switch, and the ranking never read the field.
- **What landed.**
  1. **The switch is on in CI.** `scripts/run-wpt-tests.sh` forwards
     `--verify-reference` when `BROILER_WPT_VERIFY_REFERENCE=1`, and
     `.github/actions/run-wpt-shard` sets it. It costs one extra render per
     *failing* test and **changes no test's pass/fail** — only how a failure is
     described. Measured on `css/css-page` (280 tests, 37 failures): 43 s without,
     36 s with, inside the run-to-run noise.
  2. **The flag reaches the ranker**, carried on each `lowestMatchTests` triage
     entry rather than only on the full result.
  3. **The ranking excludes them.** A flagged mismatch is never a "biggest
     problem". It is listed under its own heading — *Not ranked — reference
     disagreements* — so the information stays in the issue. Dropping it silently
     would be indistinguishable from losing a 0.0%.
  4. **A shard offers candidates of both kinds.** `lowestMatchTests` was the five
     lowest matches overall; five reference disagreements would have starved the
     ranking of every real mismatch in that shard — and the tests that would do it
     are exactly the ones stuck at 0.0%. It is now the five lowest *rankable*
     mismatches plus the five lowest cleared ones.
- **Verified end to end.** `--verify-reference` flagged exactly the six then-known
  disagreements and left `page-margin-002-print` alone — precise discrimination,
  not a blanket amnesty for 0.0%. Four focused cases pin the behaviour, including
  the negative halves: a run *without* the switch ranks exactly as before, a
  cleared test does not drive threshold escalation, and an all-cleared run yields
  no biggest problems rather than falling back to ranking them.
- **It worked.** The next run's severity list
  ([#1624](https://github.com/Broiler-Platform/Broiler/issues/1624)) put 40
  mismatches under *Not ranked* and 30 real ones above them — the first list in
  four runs that was about the engine rather than about the report. Five of those
  30 were fixed the same day.
- **What this does not do.** It does not change the pass rate, hide a failure, or
  mark any test as passing. It changes only which failures are called the run's
  *worst*, and only on evidence the runner produced itself. The reftest suite is
  unaffected — it renders both sides with Broiler, so it never sets the flag.

### `--verify-reference` cleared a test that rendered nothing

- **Owner:** the WPT runner (`src/Broiler.Wpt/WptTestRunner.cs`). Main repo.
- **Reported as the most consequential defect in [not fixed](wpt-rendering-gaps-open.md)**,
  because it moved real gaps off the severity ranking silently.
- **What was wrong.** `VerifyAgainstReferenceHtml` cleared a pixel-mismatch failure
  whenever Broiler reproduced the test's own `rel=match` reference, without ever asking
  whether anything had been **drawn**. A test that paints a blank canvas and a reference
  that paints a blank canvas match at 100%, so the exact "passing by rendering nothing"
  trap this document set warns about was built into the mechanism that decided what
  counted as a real bug. The 2026-08-13 triage measured **17 of 28** unexamined flags as
  blank-on-blank.
- **What landed.** A clear now additionally requires the render to have content, judged
  against the committed golden: a uniform render is agreement only when the golden is
  uniform too. That one condition is both checks the open entry proposed — it rejects a
  uniform render, and it rejects a contentless render against a golden that has content —
  and it costs one early-exiting scan of each bitmap on a path that already compares them
  pixel by pixel.
- **What it does not do.** A test that draws *something* wrong and reproduces a reference
  that draws the same something wrong still clears; that is the `grid-lanes` shape, and it
  needs the test's own reference to be judged rather than the render, which is a different
  check. A flag is still a triage queue rather than a verdict — just a much shorter one.

### The reference score was measured and then thrown away

- **Owner:** the WPT runner (`src/Broiler.Wpt`) and the shard merger
  (`scripts/merge-wpt-shards.py`). Main repo.
- **The bug.** `VerifyAgainstReferenceHtml` rendered the test's own `rel=match`
  reference, compared Broiler's render to it, computed the percentage — and then
  `return null`ed unless that percentage cleared the pass threshold. The one
  measurement that separates a reference disagreement from an engine gap was
  discarded in exactly the cases where it was needed, so a test at 94% against its own
  reference and 0.8% against the committed golden was reported as though nothing at
  all were known about it, indistinguishable from one that is wrong against both.
- **The two need opposite work**, which is why the conflation mattered: the first says
  the goldens disagree and "fixing" it would mean rendering *less* than the test asks
  for; the second is a real gap.
- **What landed.** The percentage is recorded on the result
  (`WptTestResult.ReferenceMatchPercent`), serialised into the JSON report, carried
  through the shard merge, printed beside the golden score in the run summary, and
  written into the severity issue's per-problem detail. A gap of 25 points or more
  between the two is called out in words. `SuspectReference` keeps its narrower
  meaning — Broiler *reproduced* the declared reference — because the ranking uses it
  to drop a test out of the candidate set, and whether these near-misses belong in
  [won't fix](wpt-rendering-gaps-wont-fix.md) is a maintainer's call rather than the
  report's.
- **A margin, not a second threshold.** The claim it supports is comparative — far
  closer to one reference than the other — so it needs no tuning against the run's own
  gate.
- **Measured** on the three `grid-lanes` tests the open entry named. The runner now
  prints `0.8% … (rel=match 94.0%)` and `0.9% … (rel=match 94.8%)` for the two
  reference disagreements, and `11.5% … (rel=match 10.4%)` for
  `column-subgrid-auto-fill-008` — which is wrong against both and stays ranked as the
  real gap it is. 36 existing merger tests unchanged, 3 added.
- **What it does not do.** The inverse case — a test that passes the golden while
  failing its own reference — is still unreported, because the check only runs on
  golden failures. Widening it to passes would re-render a reference for every passing
  reftest in the suite.

### The runner never performed WPT's `.sub` substitution

- **Test:** `css/css-color-adjust/…/color-scheme-iframe-background-mismatch-opaque-cross-origin-002.sub`.
  0.0% → **passing**, and 100.00% pixel-identical to the test's own `rel=match`.
- **Owner:** the WPT runner and its reference generator, with a two-line hook in
  `Broiler.Layout`. Main repo.
- **No server is needed, and the earlier diagnosis said there was.** A `.sub` file
  is a **template**: WPT's server expands `{{host}}`, `{{ports[http][0]}}` and
  friends before sending it (`tools/wptserve/wptserve/handlers.py` — the rule is a
  literal `".sub." in path`). Read straight off disk the placeholders survive into
  the markup, so this test's frame pointed at the uninterpretable URL
  `http://{{hosts[alt][]}}:{{ports[http][0]}}/css/…/support/light-frame.html`,
  loaded nothing, and the page rendered 100% `#121212` — the parent's dark canvas
  through an empty frame. **Neither side of the comparison substituted**, so
  Chromium's golden was blank for the same reason and the score was meaningless in
  both directions. There are **1 419 `.sub.html` files** in the tree; 51 are
  reftests.
- **The second half: WPT's hosts are all one checkout.** Substituting alone only
  moves the problem — the URL becomes `http://not-web-platform.test:8000/css/…`,
  which a local render still cannot fetch. But WPT serves *every* one of its hosts
  from the same document root, so that URL and `/css/…` name one file. Recognising
  that is what makes a cross-origin frame reachable offline.
- **What landed.** `WptSubstitution` performs the substitution with WPT's own
  defaults (`tools/serve/serve.py`): primary host `web-platform.test`, alternate
  `not-web-platform.test`, the subdomain set closed under the two-deep products
  `_make_subdomains_product` builds, IDNA-encoded, and ports pinned to the
  documented numbers so a render is reproducible.
  `Broiler.Layout.Engine.DocumentRoot` gains a list of hosts served from that root,
  and `FragmentTreeBuilder` strips such an origin before its existing
  root-relative branch. The runner's stylesheet and image loaders take the same
  view through `TryResolveWptRootRelativePath`.
  `scripts/generate-wpt-references.js` mirrors both halves, because the two sides
  must render the same bytes.
- **Only what a file on disk can answer is substituted.** `{{uuid()}}`,
  `{{headers[…]}}`, `{{GET[…]}}`, `{{file_hash(…)}}` and `{{$var}}` describe a live
  request and are left **verbatim**. `{{not_domains[nonexistent]}}` is left alone
  for a stronger reason — it names a host that is *meant* not to resolve — and the
  served-host match is exact rather than by suffix for the same reason, so
  `nonexistent.web-platform.test` is never served content.
- **Empty by default.** A host that declares no served hosts renders byte-for-byte
  as before.
- **`CACHE_EPOCH` is bumped to 7.** Every `.sub` test's golden changes, so the
  cached reference slice must be regenerated or the two sides disagree by
  construction.
- **Verified:** 47 focused substitution cases and 16 frame-loading cases, including
  the negative halves — an unknown or request-scoped placeholder left verbatim,
  `not_domains` left verbatim, an unlisted host and a subdomain of a listed one
  both refused, a non-http scheme left alone, `..` unable to escape the root, and a
  served URL naming nothing still painting empty. The reference generator's own 28
  node tests assert the JS mirror agrees case for case.
- **What else moved.** Outside the cross-origin-iframe family the change is inert
  (121 tests identical both ways). Inside it, four tests moved and the net was zero
  because two were **passing for the wrong reason** — see
  [the frame-canvas fix](#a-frames-canvas-was-never-transparent) and the
  [cross-origin twin](wpt-rendering-gaps-wont-fix.md#color-scheme-mismatch--chromium-fails-its-own-reftest).

### A root-relative resolver returned a working-directory-relative path

- **Owner:** the WPT runner (`WptTestRunner.TryResolveWptRootRelativePath`).
- **The bug.** The resolver mapped a root-relative URL onto the checkout with
  `Path.Combine(wptRoot, rel)` and returned it as-is. With a **relative**
  `--wpt-dir` that result is relative to the *process working directory* — and the
  engine resolves what it is handed against the **document's** base URL, so it went
  looking for `…/css/css-backgrounds/tests/wpt/checkout/images/green.png` and found
  nothing. The `File.Exists` guard inside the resolver still passed, because *it*
  resolves against the working directory, so nothing anywhere reported a failure:
  the render simply came out with no image.
- **Why it mattered more than it looks.** CI passes an absolute path, so CI was
  never affected. Every *local* command in this document set and in `CLAUDE.md`
  passes a relative one. So the bug was invisible in the one place that gates
  merges and active in the one place the local evidence came from — which is the
  signature already recorded as *"a higher local score than CI usually means both
  engines rendered nothing"*, without knowing the cause was ours.
- **The fix** is to return `Path.GetFullPath(local)`. Absolute always, because the
  path is consumed by something that resolves relative paths against a different
  base.
- **Verified, including the negative half.** Across 1 553 reftests
  (`css-backgrounds`, `css-images`, `fullscreen`, `css-masking`): a relative
  `--wpt-dir` and an absolute one now produce **identical** results, where before
  they differed on 22 tests. CI's own configuration is unmoved — pre-fix and
  post-fix runs with an absolute root differ on one already-failing test by
  0.05 pp, with no pass/fail change.
- **Net on pass counts: zero, and that is the honest number.** Three tests went
  from failing to passing (`fullscreen/rendering/backdrop-object` reached 100%) and
  three from passing to failing — those three had been *passing by rendering
  nothing*, and now that the image loads on both sides a real difference is
  exposed.

> **Still true, and worth keeping:** a *higher* local score than CI is a warning,
> not good news. Check whether the resource actually loaded before concluding a
> test cannot be judged locally.

### A `manual/` test was being scored

- **Test:** `html/canvas/element/manual/draw-element-image/dialog-paints-in-top-layer.tentative`.
- **Owner:** the WPT runner. Main repo.
- **The bug.** The test sits under a `manual/` path segment and is `.tentative`,
  but it was discovered and scored as a Regular test. Its reference is a 100% white
  Chromium canvas, because Chromium does not implement the proposed
  `draw-element-image` API either — so the only way to "pass" was to render
  nothing. Broiler paints a dialog (98% `#e5e5e5` + 2% green), which is arguably
  the more useful behaviour.
- **What landed:** `WptTestRunner.IsManualTest` treats a `manual/` directory
  segment as the manual signal alongside the `-manual` filename suffix, mirroring
  how `IsCrashTest` already accepts `/crashtests/` and `IsTentativeTest` accepts
  `/tentative/`. `ClassifyTestKind` checks Manual before Tentative, so such a test
  lands in the Manual bucket and leaves the scored set.
- **Verified:** focused tests cover the segment on both separators and
  case-insensitively, and pin that `manual` only counts as a *whole* segment
  (`manually/`, `semi-manual/` stay automated). Re-checked 2026-08-13: the test is
  reported `skipped / ManualTest` by the reftest runner.
- **One stale artifact to expect.** `tests/wpt-baseline/failed-tests.json` is a
  scope-aware **merged** manifest — a run refreshes only the tests it exercised.
  A test that became *skipped* is no longer exercised, so its old failure entry is
  preserved rather than cleared. 204 `manual/`-segment entries are still in that
  file for this reason. They are historical, not current.

### The page box dropped its flow-relative margins and padding

- **Tests:** `css-page/page-box-008-print` (`rel=match` 4.0% → **6.7%**) and
  `page-box-009-print` (67.0% → **79.8%**), measured 2026-08-20 against the
  [#1726 reftest run](https://github.com/Broiler-Platform/Broiler/issues/1726),
  where the first was the run's 16th-biggest problem.
- **Owner:** the WPT runner (`src/Broiler.Wpt/WptPageBox.cs`,
  `WptPageDecoration.cs`, and the new `WptPageAxes.cs`). Main repo.
- **The bug.** CSS Paged Media 3 §3.2 lets the page box carry the flow-relative
  margin and padding properties, and neither half of the runner's `@page` model
  understood them. `WptPageBox.Resolve` switched on the four *physical* margin
  longhands only, so `margin-inline-start` and its siblings fell through the switch
  and left the margin at zero. The padding reached the decoration probe, but that
  probe is a bare `<div>`: its writing mode is the initial one, so
  `padding-block-start` resolved to `padding-top` no matter what the page said, and
  its containing block is the probe surface, so a percentage resolved against that
  rather than against the page box.
- **Where the writing mode comes from, and why it needs saying.** The two tests
  disagree on purpose. `page-box-008-print` puts `writing-mode: vertical-rl` on the
  root element and leaves the `@page` silent; `page-box-009-print` puts it on the
  `@page` rule and leaves the root horizontal. Both expect the same 16/32/48/80
  ring, so the page's own declaration wins where it makes one and the root
  element's applies otherwise.
- **And the percentage basis is per-axis, not the inline size.** Both references
  spell the expected ring out as `border-width: 16px 32px 48px 80px` on a 400×800
  page declared as 2%/8%/6%/20% — which only comes out that way if each percentage
  is taken against the page-box dimension its *physical* side runs along. That is
  the convention the physical margin longhands in `WptPageBox` already used; the
  `margin` shorthand beside them still resolves all four against the width.
- **What landed:** `WptPageAxes` resolves the page's writing mode and direction and
  maps a flow-relative side to a physical one; `WptPageBox.Resolve` reads the four
  flow-relative margin longhands through it; and `WptPageDecoration.Resolve` now
  takes the declared page box and rewrites a flow-relative padding into the
  physical longhand it means, **in the place it was written**, so the cascade
  between the two spellings is unchanged.
- **The wrong turn, and it cost a measurable regression.** The padding was first
  resolved at probe-build time against the `WptPageBox` the renderer is handed —
  which, on the unpaginated path, has had its `BoxSize` replaced by the runner's
  viewport. Percentages then resolved against 1024×768 instead of the declared
  400×800 and `page-box-009-print` fell from 81.4% to 64.4%. The basis has to be
  the page box **as declared**, which is why `WptTestRunner` now resolves it once,
  before the sheet is overridden, and passes it to both readers.
- **Verified:** nine focused tests across `WptPageBoxTests` and
  `PagePaintRenderTests` pin the mapping in both writing modes, `direction: rtl`,
  the initial axes, the source-order cascade against the physical longhands, and
  the page-box percentage basis. Suite-wide, `css/css-page` moved 87.86% → 87.93%
  average with the passing count unchanged at 140/223, and `css/css-break` did not
  move at all — no test regressed. The golden-image score for all three affected
  tests is byte-identical before and after (98.4%, 98.4%, 98.9% against a locally
  generated Chromium reference), so the change is free on that side.

### The sheet ignored the named page the document put its content on

- **Test:** `css-page/page-name-table-001-print` — `rel=match` **0.0% → passes at
  100%**, measured 2026-08-20. It was the 7th-biggest problem in the
  [#1726 reftest run](https://github.com/Broiler-Platform/Broiler/issues/1726).
- **Owner:** the WPT runner (`src/Broiler.Wpt/WptPageBox.cs`). Main repo.
- **The bug.** `WptPageBox` read only the *unconditional* `@page`. The test puts a
  table on `page: square`, sizes `@page square` to 5in and paints it `#eee`, and
  leaves the unconditional `@page` painting red — so the sheet came out the default
  size under a red background where the reference is a 5in `#eee` square. Nothing
  of the test's own page reached either the box or the decoration.
- **What landed:** `EnumerateAppliedPageBlocks` yields the unconditional rules and
  then, layered over them as the cascade orders it, the rule for the one named page
  the document actually uses. The runner renders a single sheet and that sheet is
  page one, so it takes the box of the page the flow starts on. Both the geometry
  and the decoration go through that one enumerator, so both follow.
- **Exactly one name is the whole of the guard,** and it is what separates this from
  the earlier attempt that read every selectored rule and regressed this very test.
  A document that uses two names needs a per-page box, which one surface cannot
  carry, so none is taken — `page-margin-auto-print` names six pages and is
  untouched. A named rule the document never puts content on is still ignored, which
  is what keeps `A_Page_Rule_With_A_Selector_Is_Ignored` passing unchanged. A
  pseudo-class selector is never read at all.
- **Verified:** eight focused tests cover the applied case from a style attribute
  and from a style rule, two names, `page: auto`, an unused name, the layering order
  both ways round, and the pseudo-class exclusion. Suite-wide, `css/css-page` goes
  141 → **142** passing of 224 with the average 87.92% → **88.37%**, `css/css-break`
  does not move, and a fail-list diff shows exactly one test changed state and none
  regressed. The golden-image score is unchanged (99.0%, passing either way).

### An out-of-flow subtree never broke on a page name

- **Test:** `css-page/page-name-003-print` — **0.0% → passes** under
  `BROILER_WPT_PAGED_PRINT=1`.
- **Owner:** `Broiler.Layout` (`Engine/CssBox.Fragmentation.cs`). Main repo.
- **The bug.** `CarriesThePageFlow` walked the whole ancestor chain and answered
  *no* for anything inside a `position: absolute` or `fixed` subtree, so a page-name
  change between two children of an out-of-flow box never forced a break. Two rules
  were being conflated: an out-of-flow box does not carry its **own** name into its
  parent's flow — which is `ParticipatesInPageNamePropagation`'s job and is what
  `page-name-abspos-001` and `-003` are built on — but its children are stacked in
  its own block flow, and a name change between two of *them* is still a break.
- **Two WPT tests state this opposite ways, so the fix had to be adjudicated
  rather than argued.** `page-name-003` (citing the Chromium bug that established
  the behaviour) wants the break; `page-name-abspos-002`, on near-identical markup,
  wants none. Printing all four documents to PDF in the Chromium that generates
  this project's references settled it: Chromium breaks in **both**, giving
  `page-name-003` two pages against its two-page reference and
  `page-name-abspos-002` two against its one-page reference — it passes the first
  and fails the second. Broiler now lands the same way round, and
  `page-name-abspos-002` is filed with that evidence in
  [won't fix](wpt-rendering-gaps-wont-fix.md#page-name-abspos-002--its-reference-asserts-a-break-chromium-makes-anyway).
- **The swap is exactly one-for-one and the score does not move** —
  `css/css-page` stays at 135 of 224 and 77.53 % average under the paged lever,
  `css/css-break` at 90, and the default unpaginated run is unchanged. This is kept
  for the rule it removes, not for a number: "an out-of-flow subtree never breaks
  on a page name" is not something any engine implements, and leaving it in the
  fragmentation model would mislead the next change to it.

### A `display: block` image lost its page name

- **Tests:** `css-page/page-name-img-003` and `-004` — both **0.0% → pass at
  100%** under `BROILER_WPT_PAGED_PRINT=1`.
- **Owner:** `Broiler.HTML` (`DomParser.CorrectImgBoxes`) — **a pending submodule
  patch**, `patches/0003`, because the push to `Broiler-Platform/Broiler.HTML`
  answers 403. Nothing in this repository references anything new, so the build is
  unaffected while it waits, and the WPT shard actions apply it for a run.
- **It was diagnosed in the wrong component first.** The symptom is that
  `CssBox.Display` reads `inline` for an `<img>` whose author style says
  `display: block`, so `TakesAPageName` drops the image's `page` name. The cascade
  sets it correctly and layout honours it, which made it look like staleness in
  `Broiler.Layout`. Instrumenting the property setter named the writer outright:
  `DomParser.CorrectImgBoxes`.
- **And it is not a bug there either.** That method implements a block-level
  replaced element the way this engine lays one out — it wraps the image in an
  **anonymous block** and demotes the image to `display: inline`, so the image
  paints as an inline replaced word inside a block wrapper. The geometry is right.
  What was missing is that the wrapper is now the block-level box the element
  generates, and CSS Paged Media 3 §3.4 hangs a page name on a block-level box and
  nothing else — so the name has to travel with it.
- **`-001` and `-002` are the control, and they are why this could not be worked
  around downstream.** There the image really is inline and its name really must be
  ignored. The demoted `Display` said `inline` for *both* spellings, so nothing
  after `CorrectImgBoxes` could tell them apart; the fix has to be at the point the
  block-level-ness is moved. All four now pass at 100%.
- **Verified:** the paged run goes 132 → **134** of 224 with the average 76.45% →
  **77.36%**, `css/css-break` does not move, and the default unpaginated render is
  unchanged test-for-test across `css/css-page`, `css/css-break`,
  `css/css-backgrounds` and `css/css-values` — a page name is not read outside
  paged media. `Broiler.HTML.Orchestration` has no unit-test project, so the
  four-test discrimination is the coverage the patch carries.

### Every page of a paged render was printed on the same page box

- **Tests:** `css-page/page-rule-specificity-001-print`, `-002`, `-003` and
  `page-size-006-print` — all four **0.0% or near-miss → pass** under
  `BROILER_WPT_PAGED_PRINT=1`. The first three were `SizeMismatch` at 0.0%, the
  largest single group of that lever's remaining losses.
- **Owner:** the WPT runner (`WptDocumentRenderer.RenderPaged`, `WptPageBox`, and
  the new `WptReftestPages`). Main repo.
- **Two things were missing, and the first is not a page box at all.** A print
  reftest often needs a page it does not want to compare — one that exists only to
  push the interesting content onto the page after it —
  and says so with `<meta name="reftest-pages">`. `page-orientation-on-landscape-001-print`
  spells it out in the markup it renders: *"Page 1. Not compared. Just bumps testing
  to page 2."* Its reference is a one-page document, so emitting both of the test's
  pages compares two pages against one and fails on the size before a pixel is
  looked at. **No page box can fix that**, which is why the three specificity tests
  were unreachable however their geometry was resolved.
- **The second is the page box.** `@page :first` describes exactly one page, and
  every page of a paged render was printed on the same box, so a document whose
  first page differs could not be drawn — including
  `page-rule-specificity-print-portrait-ref`, which states its whole geometry as
  one page sized by `@page :first { size: portrait }` alone.
- **What landed:** `WptReftestPages` reads the declaration (single pages, lists and
  `2-4` ranges), and `RenderPaged` emits only the pages it names, each carrying the
  box it is actually printed on — page one taking `@page :first` layered over the
  unconditional rule and the used named one. The sheet is as wide as the widest
  emitted page and as tall as they add up to.
- **The limit, stated rather than hidden.** The *flow* is still laid out against one
  page area, so only page one's box may differ. (Neither half of that limit still
  stands: any page's box may differ since
  [the per-page named boxes](#a-paged-render-guessed-one-page-name-for-the-whole-document)
  landed, and the flow divides against each page's own area since
  [the per-area layout passes](#a-paged-render-laid-every-page-out-against-one-page-area)
  did.) That is sound for these four
  because each forces its own break, so where the content divides does not depend
  on the size of the page it lands on.
- **Verified:** 19 focused tests across `WptReftestPagesTests` and `WptPageBoxTests`
  cover the meta's spellings, ranges, sorting and the declarations that name nothing
  usable, plus `:first` layering over the unconditional rule and the other
  pseudo-classes staying unread. The paged run goes 128 → **132** of 224 with the
  average 73.69% → **76.45%**, `css/css-break` does not move, four tests change
  state and none is lost. The default unpaginated run is byte-identical
  test-for-test.

### A paged render stamped a page for a document that generates none

- **Test:** `css-page/root-element-display-none-print` — under
  `BROILER_WPT_PAGED_PRINT=1`, **0.0% → passes**. The unpaginated path, which is
  the default, was already right.
- **Owner:** the WPT runner (`src/Broiler.Wpt/WptDocumentRenderer.cs`). Main repo.
- **The bug.** A root element that generates no box generates no page either, so
  the sheet keeps none of its own paint. `RenderDecorated` has honoured that since
  the page paint landed — `GeneratesPageContent` gates it — but `RenderPaged` never
  asked, so a document whose `@page` states `border: solid red; background: hotpink`
  and whose `html` is `display: none` still had a decorated first page stamped
  against a deliberately blank reference.
- **What landed:** the same gate, before the output sheet is allocated, returning
  one blank page — an empty document is one empty page, not none.
- **Verified:** the paged run goes 127 → **128** of 224 with the average 73.24% →
  **73.69%**, exactly one test changing state and none lost. The default
  unpaginated run is untouched.

### A media query had no way to know it was being printed

- **Test:** `css-page/media-queries-001-print` — `rel=match` **0.0% → passes at
  100%**, measured 2026-08-20. It was the 5th-biggest problem in the
  [#1726 reftest run](https://github.com/Broiler-Platform/Broiler/issues/1726).
- **Owner:** `Broiler.CSS` (`CssPagedMedia`, `CssStyleEngine.Values`) — **a
  pending submodule patch**, `patches/0002`, because the push to
  `Broiler-Platform/Broiler.CSS` answers 403. The call sites are in the main repo
  (`Broiler.Layout/Engine/EmbeddedCanvas.cs`, `src/Broiler.Wpt/WptTestRunner.cs`)
  behind the `BROILER_CSS_PAGED_MEDIA` file-existence probe, so the repo builds
  and renders identically against the pinned pointer and switches on when the
  patch lands. The WPT shard actions run `scripts/apply-pending-wpt-patches.sh`,
  so it reaches a CI run without waiting for the pointer.
- **Two bugs met in one test, and only one of them was about paged media.**
- **The first was a length parser.** `CssLengthParser.ParseToPixels` handled
  `px`, the font-relative units and the viewport family, and none of the absolute
  ones — `in`, `cm`, `mm`, `pt`, `pc`, `q` all answered `NaN`, which every caller
  reads as "not a length". That is
  [`patches/0001`](../patches/README.md), already pending before this work and
  found independently; this entry does not duplicate it. `media-queries-001-print`
  writes its whole assertion in inches, so it needs that patch too — **neither
  patch fixes the test alone**.
- **The second is that a formatting context has media-query answers of its own.**
  `EvaluateMediaType` matched `screen` and `all` unconditionally, so `@media
  print` never applied to a document being printed, and Media Queries 4 evaluates
  `width`/`height` against the page area rather than the surface a paged renderer
  allocated. `CssPagedMedia` carries both, thread-static and inert unless pinned.
- **The page area is the *initial* one, not the declared one, and that is not a
  shortcut.** A `@page` rule may itself sit inside a media query, so resolving the
  query against the declared page would need the query already resolved. The test
  states this outright: it declares `@page { size: 10in; margin: 2in }` and then
  asserts a query matching only between 4in and 5in wide and 2in and 3in tall —
  WPT's initial 5in × 3in page, whether or not a default margin comes off it. Its
  own comment is the clearest statement of the rule in the suite.
- **The wrong turn, and it cost two tests before it was caught.** The context was
  first pinned around the render phase, but a document's style sheets are
  cascaded while it is being *built* and the engine memoizes what that cascade
  produced, so the query was answered on the screen surface and stayed answered
  that way; it belongs around the whole test, where `PrintMedia` is set. And it
  must **not** reach a nested browsing context: `media-queries-002-print` and
  `-003-print` each embed a 100 × 100 frame whose own sheet asserts `@media
  (width: 100px) and (height: 100px)`, and both went red until
  `EmbeddedCanvas.Pin` suspended the paged context around every embedded render —
  which is also how the call site inside `Broiler.HTML` is covered without
  touching that submodule.
- **Verified:** focused tests in `Broiler.CSS.Dom.Tests` cover both surfaces of
  the media type including `not print`, the page-area override against an
  environment still carrying the layout viewport, the restore on dispose, and the
  frame suspension. Measured on top of `0001`, `css/css-page` goes 142 → **143**
  of 224 reftests with the average 88.37% → **88.83%**, `css/css-break` does not
  move, and a fail-list diff shows exactly one test changing state with none lost.
  Against the pinned pointer the repo is unchanged test-for-test.

### A paged render guessed one page name for the whole document

- **Test:** `css-page/page-size-010-print` — under `BROILER_WPT_PAGED_PRINT=1`,
  **→ passes**. The test this was opened for, `page-name-unnamed-trailing-001`,
  goes from `SizeMismatch` at 0.0% to **96.4% `MissingContent`** and still fails;
  that is the honest result, and the corpus gain comes from `page-size-010-print`
  instead.
- **Owner:** the WPT runner (`WptDocumentRenderer.RenderPaged`, `WptPageBox`) plus
  one field on `Broiler.Layout`'s `ComputedStyle`. Main repo, no patch.
- **The bug was one this document's own earlier entry introduced.**
  [The named-page fix](#the-sheet-ignored-the-named-page-the-document-put-its-content-on)
  guesses the page a document is printed on by finding the one page name it uses,
  which is right for the unpaginated path — that renders a single sheet, so "the
  one name in the document" is a fair stand-in for the page that sheet is. A
  *paged* render knows its pages one at a time and should never have guessed.
  `page-name-unnamed-trailing-001` uses exactly one name, `landscape`, on its
  middle page, so the guess applied `@page landscape { margin: 20px }` to every
  page: area 260px, four pages, against a reference (which uses two names, so no
  guess applies) at area 300px and three pages. Traced side by side as
  `actual=813.8 area=260 pages=4` versus `actual=860 area=300 pages=3`.
- **What landed.** `ComputedStyle` now carries `page`, so the name survives into
  the fragment IR — it is not otherwise recoverable from a laid-out fragment.
  `RenderPaged` reads the name off the *first* fragment to start on each page
  (§5.3: a page's name is the used name of the first box on it; a later box on the
  same page cannot rename it, because a different name would have forced a break),
  and prints each page on the box that name resolves to. `WptPageBox.Resolve` takes
  the name as a parameter, with an empty string as the sentinel for "the caller
  knows its pages, take no named rule" — that is what stops the paged path
  guessing while leaving the unpaginated path exactly as it was.
- **One re-layout, not per-page layout.** When every page turns out to name the
  same rule, the flow was divided against the wrong area and is laid out again
  against that rule's area — once, and only when the box actually changes.
  `page-name-table-001` needs it: it is a single page on `@page square { size: 5in }`,
  and dividing it against the default page first puts its content in the wrong
  place. A document with *mixed* names still divides its flow against the
  unconditional area; only the boxes differ. That approximation is what
  `page-name-unnamed-trailing-001` is now missing content against.
  (Superseded: the whole-document re-layout became the one-run case of
  [the per-area layout passes](#a-paged-render-laid-every-page-out-against-one-page-area),
  which lay each run of pages out against its own area.)
- **`page-orientation` turned out not to be needed**, contrary to what the open
  entry assumed: both sides of that test declare `rotate-left` on the same page, so
  it cancels, and the test's own comment names the margin as the distinguishing
  factor.
- **Verified:** `WptPageBoxTests` is at 63 focused tests, two of them new for the
  explicit name replacing the guess and for an unknown name falling back to the
  unconditional rule. The paged run goes 135 → **136** of 224 with the average
  77.53% → **78.27%**, `css/css-break` does not move, and a fail-list diff shows
  one test gained and none lost. The default unpaginated run is unchanged at
  143/107. One speculative variant — "the innermost name at a page edge wins" —
  was tried, measured at no change, and reverted.

### A paged render laid every page out against one page area

- **Test:** `css-page/page-name-unnamed-trailing-001-print` — under
  `BROILER_WPT_PAGED_PRINT=1`, **96.4% → passes at 100%**. That is the *only*
  test this moves, and the honest headline is below: the capability landed and
  the corpus barely noticed, because every test it was built for turns out to be
  blocked behind something else.
- **Owner:** the WPT runner (`WptDocumentRenderer`) and one fragmentation fix in
  `Broiler.Layout`. Main repo, no patch.
- **The gap.** A named `@page` rule may size the page differently, and then the
  flow on those pages has to divide against *that* area — where the floats wrap,
  where the breaks fall. One continuous surface has one width and one band
  height, so it can express one page area and no more. Since
  [the per-page *boxes* landed](#a-paged-render-guessed-one-page-name-for-the-whole-document)
  each page could be *printed* on its own box, but the flow was still cut on one
  grid, and that was the documented limit.
- **What landed: one layout per page area, not one per document.** The document
  is laid out once per distinct area, and each run of consecutive pages sharing a
  name is taken from the pass that used its area. That is sound because a
  page-name change forces a break (§5.3), so a run starts at a page boundary in
  every pass and its content divides against its own area alone.
- **The order of the runs is a property of the document; only their lengths are a
  property of the page.** So the order is read from the flow in document order and
  each length from that run's own pass. Reading both off one band scan was tried
  first and is wrong twice over: content that overflows its page area lands on
  bands that are not pages, inventing runs no part of the document asked for
  (`page-size-007`'s 640px-wide container on a 200px page produced a phantom
  trailing page), and it derives the page *count* from fragment bounds when the
  count is settled by the content height — that cost nine tests in one measured
  run, `fixedpos-003` and `monolithic-overflow-012` among them, every one a page
  count disagreement. The walk now says only where each run starts.
- **A float follows a forced break.** `ApplyForcedPageBreakBefore` skipped floats
  outright. A float declares no break of its own — `break-before` does not apply
  to it and it carries no page name into the flow — but a break the sibling before
  it forces is a break in the flow the float sits in.
  `page-size-007-print`'s reference is built on that: `break-after: page` on a div
  with a float immediately after it, and the float stayed on the page the break
  had just ended while the text following it moved. **This closes no test**, and
  it is kept because it is right and has a test of its own.
- **What is actually blocking the tests this was built for**, each established by
  rendering it rather than assumed — this is the useful part of the entry:
  - `page-size-007`/`-008`: the **test** side now schedules six pages against the
    three page areas it declares, which is what the feature was for. The
    **reference** side still comes out short (four pages against six), inside its
    `flow-root` containers.
  - `fixedpos-010` is not per-page layout at all: it needs `position: fixed` to
    repeat on every page, and Broiler paints it once. (That
    [has since been fixed](#a-fixed-position-box-appeared-once-in-the-document-not-once-on-every-page);
    the test still fails, now on three pages against its reference's four.)
  - `page-orientation-on-landscape-001`/`-portrait-001` need `page-orientation`
    to actually rotate the page.
  - `page-size-009` needs `vw`/`vh` to resolve against the **first** page's area
    for the whole document, which is what its reference states by sizing page one
    with `@page :first`.
  - `page-box-004` needs the page box's percentage margins and padding resolved
    per named page.
  - `page-margin-auto-print` and `page-margin-auto-and-non-zero-print` need `auto`
    margins on six named pages.
- **Verified:** four render-level tests in `PagedPrintRenderTests` — a named page
  dividing its flow against its own area (1000px of sheet if it does not, 400px if
  it does), an unnamed page after a named one returning to the unconditional box,
  a wrapper not claiming the page its child names, and the float following the
  forced break. Measured with the submodules at their pinned pointers: paged
  `css/css-page` 133 → **134** of 224 with the average 77.19% → **77.21%**, one
  test gained and none lost. Everything else is byte-for-byte unchanged — paged
  `css/css-break` 91, `css/CSS2` 96, `css/css-backgrounds` 424, `css/css-values`
  104, and the default unpaginated `css/css-page` 142. `page-size-010` also goes
  99.0% → 100%; `page-size-009` slips 55.6% → 54.7% and passes neither way.

### A fixed-position box appeared once in the document, not once on every page

- **Tests:** `css-page/fixedpos-009-print` — **0.0% → passes at 100%** under
  `BROILER_WPT_PAGED_PRINT=1`. Nine more of the family go from *passing* at
  99.2%–99.9% to **exactly 100%**, and eight of those on the **default**
  unpaginated path as well — they were over the 99% gate while drawing the box in
  the wrong place, which is the kind of pass that stops being one the moment
  anything else moves.
- **Owner:** `Broiler.Layout` (`FragmentTreeBuilder`, `CssBox.Layout`). Main repo,
  no patch.
- **Three bugs, and only the first is the one the tests are named for.**
- **A fixed box appeared once in the document.** CSS Paged Media makes the page
  area the fixed-positioning containing block, so a fixed box is on every page —
  WPT's `fixedpos-*` say so in the text they render, *"This should repeat on every
  page"*, and their references state the same layout with one absolutely
  positioned copy per page. `FragmentTreeBuilder` now emits the extra appearances,
  one page further down each. They are fragments and never boxes: a fixed box is
  out of flow, contributes no height, and the page count is what it was.
- **A bottom-anchored fixed box sized by its content was off by its own height.**
  `PositionAbsoluteBox` — the post-layout pass that re-resolves `right`/`bottom`
  once the used size is known — ran for `absolute` and not for `fixed`. The
  earlier pass recovers a height from an explicit, non-percentage `height` and
  from nothing else, so a fixed box sized by its content and anchored with
  `bottom` was anchored by its **top** edge to the viewport's bottom: one whole
  box-height low, which in a paged render is the top of the *next* page. That is
  what `fixedpos-001` through `-003` were really failing on — not "the box is
  missing from pages 2 and 3" but "the box is on the wrong page, once" — and it is
  why they read as 99.8% rather than as something obviously broken.
- **And that first placement cost a whole page.** The same pass places
  `absolute` boxes, and with the height still unknown it put the box *at* the
  containing block's bottom edge — so the subtree laid out below that edge, and
  `LayoutEnvironment.ActualSize`, a running maximum, kept the overshoot after
  `PositionAbsoluteBox` corrected the box. `fixedpos-009`'s reference is two pages
  of `height: 100vh` and came out three, its content ending 36px — one masked
  pencil — past the end. The first pass now leaves such a box at its static
  position and lets the post-layout pass place it.
- **Two tests go slightly down, and both are the fix working on content that is
  wrong for another reason.** `fixedpos-011` 97.7% → 97.0%: its fixed box is a
  four-column multicol whose 400px child does not column-fill, so it overflows its
  100px box and the repeats now duplicate the overflow. (That
  [has since been fixed](#a-box-taller-than-a-column-set-stayed-in-the-first-column);
  the test recovered to 98.3% and still fails.) `page-margin-005`
  80.3% → 80.0%: percentage `@page` margins are not resolved against the
  corresponding dimension, so the two sides' page areas differ and their repeated
  boxes land in different places. Neither is a fixed-positioning question.
- **`fixedpos-010` still fails**, and it is not this either: it renders three
  pages against its reference's four, which is where a named run ends.
- **Verified:** three render tests in `PagedPrintRenderTests` — the repeat, the
  bottom-anchored content-sized placement, and the page that the overshoot added —
  each confirmed to fail with its fix reverted and pass with it. Measured with the
  submodules at their pinned pointers: paged `css/css-page` 134 → **135** of 224,
  average 77.21% → **77.67%**, one test gained and none lost; the default
  unpaginated run holds at 142 with its average 88.37% → 88.38%; paged
  `css/css-break` (91), `css/CSS2` (96), `css/css-backgrounds` (424) and
  `css/css-values` (104) do not move.

---

## CSS engine

### Every CSS Color 4 colour function painted opaque black

- **Tests:** `css-images/gradient/gradient-single-stop-none-interpolation`
  ([#1670](https://github.com/Broiler-Platform/Broiler/issues/1670).29) and the whole of
  `css/css-color`, **182 → 273 of 307 passing**.
- **Owner:** `Broiler.Layout` (`IR/CssColor4.cs`). Main repo, no patch.
- **Root cause.** Only `rgb()`, `hsl()`, hex and the named colours parsed. `hwb()`,
  `lab()`, `lch()`, `oklab()`, `oklch()`, `color()`, `color-mix()` and the relative colour
  syntax all fell through to `Adapter.GetColor`, which folds an unrecognised value to
  **opaque black** — the same failure mode as
  [the system-colour keywords](#system-colour-keywords-did-not-resolve-at-all), and the
  reason a page written in a modern colour space renders as black rectangles rather than
  as nothing. A colour *stop* was dropped instead of blackened, so a single-stop gradient
  lost its only stop and painted nothing at all.
- **The gradient test was a blank-on-blank pass.** Its reference is written in the same
  notation, so both sides rendered an empty page and agreed at 100% while painting none of
  the five ramps the test is about. It renders them now and matches on their pixels. This
  is the [false-negative trap](wpt-rendering-gaps-open.md#the-flag-can-be-a-false-negative)
  a third time; the reftest flag cannot see it, because the flag only asks whether the two
  sides agree.
- **Why the fix is a normaliser and not a parser.** The canonical parser is
  `Broiler.CSS.CssValueParser.TryParseColor`, and `Broiler.CSS` is deliberately a
  dependency-free kernel — nothing there may reference `Broiler.Layout`, so a fix inside it
  could only ship as a submodule patch and could reuse nothing. Every path that turns a CSS
  colour into pixels passes through main-repo code first (`CssBox.GetActualColor`, the
  `BackgroundImage`/`BoxShadow`/`TextShadow` getters, `SvgRenderer.ParseColorValue`), so
  rewriting the value to `rgba(…)` there hands the existing parser something it already
  reads. **No submodule change at all**, and the gradient stop list is covered by the same
  pass as the flat colours.
- **Four things inside it are worth naming.**
  - **The modern `/ <alpha>` spelling was broken too.** The base parser rewrites the slash
    to a comma and then splits on commas, so `rgb(0 128 128 / 50%)` became two tokens,
    failed its arity check and painted black — while the legacy comma form worked. The
    legacy form is deliberately still declined here, so the two parsers cannot disagree
    about one value.
  - **Out-of-gamut colours are gamut-mapped, not clipped** (§13.2). Clipping each channel
    independently shifts hue as well as saturation: `oklch(0.8 0.4 0)` clips to a flat
    `rgb(255, 0, 179)`. Reducing OKLCH chroma until the clip is within one just-noticeable
    difference keeps the hue. Only out-of-gamut colours take that path.
  - **A missing component is zero** (§4.4) — what the single-stop test asserts.
  - **`currentcolor` is resolved by the caller, not guessed**, guarded against the
    self-reference of `color: color-mix(in srgb, currentcolor, red)`.
- **Three fake passes were unmasked**, each a separate gap now visible because the
  *reference* side started rendering correctly. All three are in the paint walker's
  gradient code (`Broiler.HTML`) and none is reachable from the value-normalising layer:
  `gradient-to-transparent` (gradient interpolation is not premultiplied),
  `gradient-{in,de}creasing-hue-lch` (the `increasing hue` / `decreasing hue` arc is
  ignored), and `gradient-none-interpolation` (93.7% → 78.8%, still failing) which needs
  §4.4's *carry-forward* rule — a missing component interpolated against a colour that has
  it takes the neighbour's value rather than zero.
- **`color-layers-no-blend-mode` drops 95.9% → 75.2%** without changing state:
  `color-layers()` is CSS Color 6 and unimplemented, and its reference — a stack of
  ordinary layers — is what got more correct.
- **Exit gate — met.** `CssColor4Tests` pins the conversions against the numbers WPT's own
  reftests state rather than against the implementation, including a round trip through
  each predefined space, which is what keeps the inverse primary matrices in step with the
  forward ones.

### System colour keywords did not resolve at all

- **Test:** `forced-colors-mode/forced-colors-mode-20`. Ours 98% black; Chromium's
  98% white.
- **Owner:** `Broiler.CSS`.
- **Root cause: not a dark palette, as first suspected.** `CssSystemColors`
  carried only `Field` and `FieldText`. Every other system colour fell through to
  the named-colour lookup, which does not know them, and resolved to the
  unknown-colour fallback: **black**. That turned every system-colour test into a
  whole-canvas mismatch, and this one was only the worst-scoring member of the
  family.
- **What landed:** the CSS Color 4 §6 table filled in from the light palette
  (matched to what Chromium reports, since the references are Chromium
  screenshots), the §6.2 deprecated keywords mapped onto their aliases, and a
  `CssColorScheme` overload so a dark used colour scheme selects the dark palette —
  `Canvas` there is the same `rgb(18, 18, 18)` the canvas-background paint path
  already uses. `forced-colors` still computes to `none`; nothing emulates it.
  Both existing call sites (the renderer's colour hook and `CssAnimationResolver`)
  already went through `TryResolve`, so filling the table lit up both.
- **Verified:** `background-color: Canvas` paints 100% white and `ButtonFace`
  paints `rgb(239, 239, 239)` — a value only the new table produces, so the render
  path demonstrably goes through it. 45 focused tests cover the light palette, the
  `color-scheme: dark` switch, and the deprecated aliases.

### `contrast-color()` and style container queries

- **Test:** `css/css-color/contrast-color-style-query`. Ours 100% white; Chromium
  100% green.
- **Owner:** `Broiler.CSS`.
- **Which of the three features were missing.** The test needs an
  `@property`-registered `<color>` custom property, `contrast-color(#000)`
  resolving to an absolute colour, and `@container style(--contrast-color: white)`
  matching on it. Registration already worked; the other two did not — and the
  earlier guess that the test would "distinguish the two once either lands" was
  wrong. It needs *both*, so neither alone moved it.
- **What landed.**
  1. `contrast-color(<color>)` resolves to whichever of black or white contrasts
     more with its argument, via the WCAG 2 contrast ratio over relative luminance.
     The comparison collapses to one threshold — white wins below luminance
     `√(1.05 × 0.05) − 0.05 ≈ 0.1791`, where the two ratios are equal. **That is
     not mid-grey:** `#767676` takes black while `#757575` takes white, and a test
     pins exactly that boundary. Wired into `CssValueParser.TryParseColor`, so it
     is a `<color>` everywhere; the system colours are routed there in the same
     pass, since they are `<color>` keywords too.
  2. `style()` container queries, which were explicitly unsupported and forced the
     whole query false. A *style* container needs no `container-type` —
     css-contain-3 makes every element one — so size and style containers are now
     resolved separately and a style-only query works where no size container
     exists. Comparison is colour-aware, because a registered `<color>` property
     computes to an absolute colour: `white` and `rgb(255, 255, 255)` are the same
     computed value.
- **Two parsing traps this uncovered,** both of which made *every* style query
  silently false rather than erroring:
  - `SplitContainerName` read the leading identifier of `style(...)` as a container
    **name**, so the lookup hunted for `container-name: style`, found nothing, and
    bailed. An identifier immediately followed by `(` is a function.
  - The condition tokenizer split `style` from its parenthesised argument, leaving
    the argument looking like a nested condition and the name like a bare size
    feature. Function tokens now keep their argument list.
- **Verified:** the composed scenario renders red with the change reverted and
  green with it applied. The negative half — `contrast-color(#fff)` is *black*, so
  the `white` query must **not** match — is what makes that meaningful; an early
  version of the check passed only because the query never matched at all.
- **Known gaps, deliberate:** only the custom-property form of `style()` is
  supported (a standard-property query returns false rather than guessing), and an
  `@property` registration with `inherits: false` is not honoured by the ancestor
  walk. The walk reads cascaded declarations rather than `GetComputedStyle` because
  it runs *during* style computation and re-entering it for an ancestor would
  recurse.

### `@container` prelude evaluation recursed without bound — 71 worker crashes

- **Owner:** `Broiler.CSS`.
- **The run's largest single problem, and its signature named the wrong
  component.** Both crash signatures — `Worker closed stdout before returning a
  result` (68 tests) and `Worker exited with code 134` (3) — were one unbounded
  recursion. Every `(` was read as the start of a nested condition, so a prelude
  whose parentheses belong to a *value* function (`(width = calc(100px + 10rem))`)
  or to a *query* function other than `style()` (`anchored(fallback: --foo)`,
  `scroll-state(scrollable: block-end)`) handed the tokenizer the identical text at
  every level. A .NET stack overflow cannot be caught, so it killed the worker
  outright — which is why the reported signature named the runner rather than the
  CSS engine.
- **Measured** over the 302 `container-queries` tests in both affected
  directories: **68 crashes → 0**, no test regressed.

### Shadow trees leaked their styles into the whole page

- **Tests:** `css/css-shadow/shadow-directionality-001.tentative` and `-002`,
  1.1% / 1.3% → **99.55%**.
- **Owner:** HtmlBridge (`DomBridge/ShadowTreeSelectors.cs`) with a `Broiler.CSS`
  half.
- **Two independent bugs, and the pixels only move when both are understood.** A
  shadow root's `<style>` is serialized inline into the render document, so the
  renderer saw its rules as ordinary global rules with no provenance — a shadow
  tree's `div { background: red }` repainted **every** `div` in the page. On top of
  that, `:dir()` sat in `CssSelectorMatcher`'s `RecognizedPseudoClasses` with no arm
  in the pseudo-class switch, so it fell through to the deliberately lenient default
  for recognised-but-unmodelled names and matched *every* element — `:dir(ltr)` and
  `:dir(rtl)` at once. Together they turned each test's four small shadow rules
  into one canvas-wide repaint.
- **What landed.**
  1. **`ScopeShadowTreeSelectors`** reuses the shape `ScopeShadowHostSelectors`
     established for the mirror-image `:host` problem: stamp every element of the
     tree with `data-broiler-shadow-scope="N"` (the host is deliberately *not*
     stamped — it belongs to the outer tree and is reachable only through `:host`),
     then append `[data-broiler-shadow-scope="N"]` to each selector's subject
     compound. It runs *before* the `:host` pass, which rewrites the keyword this
     one keys off.
  2. **`:dir()`** now resolves HTML's directionality: nearest ancestor-or-self with
     a valid `dir`, `ltr` at the root, `dir=auto` (and `<bdi>`, whose default it is)
     from the first strong directional character. Strictly a narrowing, so it can
     only remove matches the lenient default invented.
- **The subject compound, not every compound — a deliberate choice.** Scoping the
  subject is what stops the leak: a rule whose subject is outside the tree cannot
  apply. Scoping *every* compound is closer to the spec but adds one attribute
  selector per compound, so it changes specificity **unevenly** between rules of the
  same sheet — `div span` (0,0,2) and `.foo` (0,1,0) swap cascade order once they
  become `div[s] span[s]` (0,2,2) and `.foo[s]` (0,2,0). Subject-only adds exactly
  (0,1,0) to every rule, so their order relative to each other is preserved exactly.
- **Compounds left alone, each for a reason:** `:host`/`:host-context` (the host is
  not in the tree), `::slotted`/`::part` (their subject is a light-DOM node).
  `@keyframes`, `@font-face` and friends are copied verbatim — their blocks hold
  keyframe selectors and descriptors, not selectors — while the conditional group
  rules (`@media`, `@supports`, `@container`, `@layer`, `@scope`,
  `@starting-style`) are recursed into.
- **A serialization pass is not free, and the suite said so.** Running this pass and
  the `animate({pseudoElement})` one unconditionally pushed
  `RunTestWithTimeout_GridTemplateColumnsCrash_Completes_Without_Timing_Out` — a
  6-second budget on a `grid-template-columns` with five million tracks — over its
  limit. It only failed in a full-suite run, never in isolation, which is exactly
  the shape that reads as flakiness; the control that settled it was running the
  *same full suite* against unmodified sources, where the test passes. Both passes
  now early-out on a flag (`_hasShadowRoots`, `_hasAnimatedPseudoStyles`).
- **Measured.** `css/css-shadow` **153 → 157 of 207 with nothing lost** — the other
  two gains being `css-scoping-shadow-with-rules-no-style-leak` (98.1% → 99.2%, the
  test named for exactly this bug) and `host-specificity-003` (88.0% → 99.6%).
  Checked for regressions over **1 669 further tests** in `css-view-transitions`,
  `css-masking`, `css-position` and `css-pseudo`: no test changed state in either
  direction.

### `font-size: math` collapsed the element it was on

- **Test:** `css/css-fonts/math-script-level-and-math-style/font-size-math-001.tentative`,
  3.9% → **99.86%**.
- **Owner:** `Broiler.Layout` (`Engine/CssBoxProperties.cs`). Main repo.
- **The keyword is `1em`, and the bug was not that it failed to scale.** MathML
  Core makes `font-size: math` the inherited size times the math scaling factor,
  and that factor is driven entirely by a *change* in `math-depth` — with no change
  it is 1. Broiler models no math depth, so the keyword is always `1em`, which is
  exactly what the test's reference asserts: the same document with `math` written
  as `1em`. `math` had no arm in the font-size keyword switch, so it fell through to
  the length parser, which reads an unrecognised token as **0** — and the zero clamp
  turned that into a **0.001pt** font. Every relative size beneath it then resolved
  against 0.001pt, so the whole subtree vanished.
- **Two call sites, because the computed and used sizes resolve keywords
  separately** (`ComputedFontSizePoints` and `ActualFont`, the latter on the
  non-zoom path).
- **Measured:** the one arm carries **five more tests** with it — the 14-test
  subset goes **7 → 13 passing** — and the whole 552-test `css/css-fonts` directory
  goes **347 → 353, nothing lost and nothing else moved.**

### Media query range syntax and `@custom-media`

- **Owner:** `Broiler.CSS` (`56eea09`, "media queries: range syntax and
  @custom-media"), upstream and pinned.
- Media Queries 5 §3, with substitution and cycle detection covered by
  `CssStyleEngineTests`. It decides whether an `@media` block's rules cascade at
  all, so getting it wrong is a whole stylesheet applying — or not applying — to a
  page.
- **It moves one test the wrong way, and that is correct.**
  `mediaqueries/at-custom-media-basic` now renders the green its own reference asks
  for, and Chromium — which does not implement `@custom-media` — renders white. See
  [won't fix](wpt-rendering-gaps-wont-fix.md#custom-media--chromium-does-not-implement-it).

---

## Layout

### A column flex container never read `flex-basis` and never shrank

- **Tests:** `css/css-flexbox` reftests **693 → 704 passing**, 11 won and none lost. The
  headline is `percentage-heights-002`, entry 19 of
  [#1774](https://github.com/Broiler-Platform/Broiler/issues/1774)'s top 30 at 40.9%,
  now **100%**. The other ten:
  `dynamic-isize-change-003`, `flex-basis-012`, `flex-flow-007`,
  `flex-minimum-height-flex-items-003`/`-026`/`-027`,
  `flexbox-justify-content-vert-003`/`-004`/`-006` and `percentage-widths-001`, most of
  them at 98.7% against a 99% threshold.
- **Owner:** `Broiler.Layout` (`Engine/CssBox.Flex.cs`, `Engine/CssBox.ContainingBlock.cs`).
  Main repo — no patch, so it reaches the WPT run directly.
- **Three gaps, and the third gated the first two.**
  - **§9.2 step 3.** A column container's items stack through ordinary block flow here,
    and the pass that resizes that stack afterwards measured each item's height instead
    of reading its flex base size. Those coincide for a content-sized item, which is why
    it went unnoticed; they part company the moment `flex-basis` names a block size,
    which block flow does not implement and so ignored outright.
  - **§9.7, the shrink half.** Deliberately omitted, on the stated grounds that §4.5's
    automatic minimum size did not exist in this axis to floor a shrink with. Since
    `flex-shrink` is `1` by default, that is every column flex item on the web asking to
    shrink and none of them shrinking. `ClampFlexColumnItemBorderBoxHeight` is the floor
    that made turning it on safe — the block-axis twin of `ClampFlexItemBorderBoxWidth`,
    reading the content size the same way (measuring the item with its own `height`
    suppressed) and adding the one rule the row clamp omits: §4.5 gives no automatic
    minimum to an item whose main-axis `overflow` is not `visible`, which is what lets
    the `flex: 1; overflow: auto` pane of a column layout scroll instead of pushing its
    container open.
  - **CSS2.1 §10.6.4, which gates both.** "Definite main size" was read from the
    container's `height` declaration alone. An out-of-flow box with `height: auto` and
    both `top` and `bottom` set has no such declaration and a perfectly definite used
    height — the constraint equation leaves one unknown — and `position: absolute;
    inset: 0` is how a full-viewport app shell is written, so the most common definite
    column container there is was treated as content-sized. The same blindness sent
    every percentage height inside such a box to `auto` (§10.5).
    `TryGetInsetDerivedContentHeight` answers it once for all four readers.
- **`min-height: 0` had to become distinguishable from an undeclared minimum.**
  `MinHeight` computes to `"0"` either way, and §4.5 turns on exactly that distinction:
  an undeclared minimum on a flex item is `auto` and floors it at its content, while
  `min-height: 0` is the idiom for deliberately letting a column item shrink past it.
  `IsMinHeightSpecified` is the block-axis twin of the `IsMinWidthSpecified` flag the row
  path already had.
- **A vertical writing mode is declined, not transposed.** `column` names the *block*
  axis, so under `vertical-lr` the main axis is horizontal and every number the pass
  reads belongs to the cross axis. Growing on the wrong axis was inert; shrinking on it
  is not, and `flexbox-column-row-gap-002` squashed all six of its items. The sibling
  multi-line pass already declined for the same reason.
- **Two tests moved from passing to failing, and both were passing by omission.**
  `css-position/sticky/position-sticky-flex-item-001` and `-003` are the `column` half
  of a family of four; their red item is sized by `flex-basis`, so it used to be
  zero-tall and there was no red to show. They now fail exactly as their `row` twins
  `-002`/`-004` always have, on
  [a sticky box's contribution to scrollable overflow](wpt-rendering-gaps-open.md#a-sticky-box-contributes-its-stuck-position-to-the-scroll-containers-overflow)
  — one bug, no longer masked by a second.
- **Checks:** `src/Broiler.Wpt.Tests/FlexColumnMainAxisSizingTests.cs` renders each rule
  and measures it off the canvas. `FlexColumnWrapAndReverseTests`'
  nowrap case was restated: it pins the line breaking, and had been written against the
  pre-shrink geometry of two 40px items overflowing a 50px container.

### A table box outside a table painted nothing at all

- **Tests:** the `css/CSS2/tables/table-anonymous-objects-*` family — **103 of its
  members** were in the failure manifest for
  [#1688](https://github.com/Broiler-Platform/Broiler/issues/1688), the single largest
  numbered family in `css/CSS2`. On the family's own `rel=match` references: 46 → **48
  passing**, 60 tests improved against 7 regressed.
- **Owner:** `Broiler.Layout` for the pass, `Broiler.HTML` for the one line that calls
  it (patch: *"parse: generate the anonymous table a misparented table box needs"*).
- **CSS 2.1 §17.2.1 has two halves, and only one was implemented.**
  `CssLayoutEngineTable` generates the missing *children* — an anonymous row for a
  stray cell in a table or a row group, an anonymous cell for stray content in a row.
  But it only ever runs on a box that already *is* a table, so nothing generated the
  missing *parents*: a `table-row`, row group or `table-cell` sitting directly inside a
  block or an inline. Such a box is neither `IsBlock` nor `IsInline`, so block layout
  walked straight past it — it never laid out, never painted, and contributed no
  height. `Broiler.Layout/Engine/AnonymousTableBoxes.cs` is the landed pass;
  `AnonymousTableBoxParentTests` is the landed check.
- **The classification split was one bug, not two.** The family's failures were
  divided between `MissingContent` and `ReferenceOverlayExposed`, which reads like two
  causes. It is one: every test in it stacks a green layer over a red one, and the
  green layer is spans carrying nothing but `display: table-row-group`. Whichever layer
  the test put on top decided which bucket the same absence landed in.
- **Four tests moved from passing to failing, and that is the fix working.** In those
  the *red* layer is the one built from bare table boxes, so they had been passing
  because the content under test rendered nothing at all — passing by omission. They
  now render, and show the residual geometry gap between a generated anonymous table
  and the real `<table>` it is compared against.
- **A flex or grid container is deliberately excluded.** CSS Display 3 §2.7 blockifies
  those children, so a `display: table-cell` flex item computes to `block` and §17.2.1
  has nothing to find a parent for. Wrapping one would insert an anonymous table
  *between* the container and its item, so the item would stop being a flex/grid item
  altogether — a worse answer than the blockification Broiler still owes, and one that
  would land squarely in `css-grid` and `css-flexbox`, the two largest failing areas.
  An out-of-flow or floated box is excluded for the same reason, and it splits a run
  rather than joining one. (The exclusion is not hypothetical bookkeeping: an earlier
  build without it changed how `display: table-cell` flex items sized.)
- **A bare container cannot judge this family by pixels.** Broiler resolves no
  `font-family` there — `monospace`, `serif` and a named family all render in one
  sans face — so every golden comparison against Chromium sits at a ~96 % floor from
  glyph shapes alone, well under the 99 % pass threshold, before any engine
  difference is counted. The `rel=match` reftest suite renders both sides with
  Broiler and is unaffected, which is why the numbers above are quoted from it.

### An out-of-flow first child propagated its top margin into its parent

- **Tests:** `css/CSS2/margin-padding-clear/margin-006` … `-009`, failing → **passing**.
- **Owner:** `Broiler.Layout` (`Engine/CssBox.Margins.cs`). Main repo.
- **Root cause.** CSS2.1 §8.3.1 is explicit that margins of absolutely positioned boxes do
  not collapse. `MarginTopCollapse`'s parent–child branch is reached by any box with no
  previous *in-flow* sibling — `GetPreviousSibling` already skips out-of-flow ones — and
  nothing there asked whether the box itself was in flow. Worse than a wrong margin: when
  the child's margin exceeded the parent's, the branch mutated the **parent's** `Location`
  to carry the excess up, so a page whose first child is a fixed backdrop or an absolutely
  positioned panel laid *all* of its in-flow content at the wrong offset.
- **Found while triaging `css-page/body-background-vrl-print`**
  ([#1670](https://github.com/Broiler-Platform/Broiler/issues/1670).27), which it does not
  close — that test needs
  [block-axis pagination](wpt-rendering-gaps-open.md#pagination-runs-along-the-physical-y-axis-only),
  and moves 34.8% → 37.2%. The margin bug is worth fixing on its own merit and was measured
  on its own: `css/css-page`, `css/CSS2/margin-padding-clear`, `css/css-position` and
  `css/css-anchor-position` together go **968 → 972 passing, +4 / −0**.

### A grid with only out-of-flow children resolved no grid areas

**[Issue #1667](https://github.com/Broiler-Platform/Broiler/issues/1667).12.**

- **Test:** `css-grid/abspos/grid-sizing-positioned-items-001`, CI 9.1%. It declares no
  `rel=match`, so the pixel score can only be read off a CI artifact — but it is a
  `check-layout-th.js` test, and those state their expected geometry in the markup.
  Its assertions went **39 / 128 → 128 / 128**.
- **The abspos pass was written and then never reached.** `PlaceAbsposGridItems` has
  resolved a grid area per §9.2 since [#1624](https://github.com/Broiler-Platform/Broiler/issues/1624),
  and the entry for this test read the symptom — every `offset-*` coming back as the
  container's 15px padding — as that pass being absent. It is not: `TryApplyGridTrackLayout`
  **declines the whole definite-track pass when no in-flow item is placed**
  (`placements.Count == 0`), which is exactly the shape of this test. All eight of its
  grids hold nothing but abspos children, so the pass returned before sizing a single
  track and every child fell back to the container's padding box. A grid with no in-flow
  items still has an explicit template to size, and §9 makes those tracks the containing
  blocks; the guard now also asks whether anything is waiting on them.
- **Two smaller defects were behind it**, invisible until the pass ran at all:
  1. **The block extent was read from a box that was not finished.**
     `PlaceAbsposGridItems` took the padding box's bottom from `ActualBottom`, which at
     that point holds the *track sum* — the used height only while the container's own
     height is indefinite. A definite height is re-applied after the pass returns, so an
     `auto` grid line resolved to 230px on a grid that is 1030px tall. It now resolves
     the container's used content height itself, including the percentage case, which
     `TryGetDefiniteContentHeight` deliberately declines: widening *that* would change
     which grids size their rows against a definite basis, and with them `align-content`
     and every percentage track. The abspos containing block needs none of that.
  2. **`rtl` mirrored the area and then placed the item at its left edge.** With both
     inline insets `auto` the used value is the static position (CSS2.1 §10.3.7), which
     is the area's *inline-start* corner — the right edge in `rtl`. Eight of the test's
     offsets are that one rule, and the four `sizedToGridArea` rows hid it by filling
     their area, where both edges coincide.
- **Exit gate:** the test's 128 assertions pass. They do.

### An out-of-flow child of a flex container was never laid out

**[Issue #1667](https://github.com/Broiler-Platform/Broiler/issues/1667).22.**

- **Test:** `css-transforms/dynamic-fixed-pos-cb-change`, CI 18.9%. It now renders
  **byte-identical to the reference it declares**, and — the part that matters — both
  sides render the content the test is about, where before both rendered a bare
  background.
- **This was a blank-on-blank pass in waiting**, the trap
  [the whole set warns about](wpt-rendering-gaps.md#read-this-first--four-things-that-are-true-of-the-whole-set):
  the test and its reference agreed with each other at ~100% while agreeing with the
  golden at 18.9%, because Broiler drew nothing in either.
- **Cause.** `PerformFlexRowLayout` replaces the ordinary block-flow child loop
  wholesale, and that loop is the only thing that calls `PerformLayout` on a child. It
  walks `Boxes` through `IsInFlowFlexItem`, which correctly excludes out-of-flow children
  from flex layout (§4.1) — and nothing else ever laid them out. So an abspos or fixed
  child of a **row** flex container was not merely mispositioned; it never reached the
  canvas at all. `body { display: flex }` holding a fixed backdrop and an abspos panel
  painted neither, and `body { display: flex }` is not an exotic shape.
- **Only the row path.** A column flex container falls through to ordinary block/inline
  flow over the same list, so its out-of-flow children were always laid out; the same
  probe on `flex-direction: column` was correct before and after.
- **The static position is the container's content-box start corner** (§4.1), seeded
  before `PerformLayout` so an all-`auto` child lands there rather than at whatever
  coordinates the last laid-out box left behind.
- **Exit gate:** an abspos and a fixed child of a row flex container render exactly as
  they do with the `display: flex` removed. They do, pixel for pixel.

### `flex-grow` did nothing at all in a column flex container

**[Issue #1667](https://github.com/Broiler-Platform/Broiler/issues/1667).20.**

- **Test:** `css-flexbox/percentage-heights-003`, CI 15.4% — **4 / 9 assertions → 7 / 9**.
- **A column flex container had no flex algorithm.** `PerformFlexRowLayout` is the only
  one there is; a column container's children stack through ordinary block flow, which
  never resolves flexible lengths. So `flex-grow` was inert along a column's main axis:
  a lone `flex-grow: 1` item in a 100px-tall container stayed at its content height, and
  a `height: 100%` child of it measured 0. Two constructed probes covering
  `flex-grow`/`flex`/`flex-basis`, one item and two, went **1 / 6 → 6 / 6** and
  **2 / 6 → 6 / 6**.
- **Definite main size only, and the test is what pins the distinction.** §9.2 makes the
  main size definite from a specified `height` — then clamped by `min-height`/`max-height`
  — and *not* from a `min-height` alone. So this test's `height: 0; min-height: 100%`
  containers flex their items to the clamped 100px, while its `min-height: 100%`-only
  containers, whose main size stays content-based, correctly leave them at zero. Reusing
  `TryGetDefiniteContentHeight` would have got this wrong in both directions: it drops a
  zero result, and `height: 0` under a `min-height` is precisely the definite-but-clamped
  case.
- **Each grown item is laid out again at its target height** rather than resized in
  place, because a percentage-height descendant resolves against the item's used height —
  the `span { height: 100% }` this test measures — and poking `Size` alone leaves it
  reading the pre-flex value. That is the same reason the row path re-lays-out at the
  target *width* instead of resizing.
- **Growth only, on purpose.** `flex-shrink` defaults to `1`, so implementing the negative
  half here would make *every* column container whose content overflows squash its items —
  and squash them further than §4.5 allows, because that clamp (an item's min-content size
  as the floor for an `auto` `min-height`) does not exist yet. Not shrinking is the better
  of the two wrong answers until it does; §4.5 is the prerequisite, not more of §9.7.
- **Not attempted:** `column wrap` with items that overflow the main axis, which needs
  real line breaking. Single-line is what the tests on this page exercise.
- **Exit gate:** `flex-grow` distributes a definite column container's free space, and
  the two residual assertions in the test are the orthogonal-writing-mode ones
  [tracked with the crossed-axes grid item](wpt-rendering-gaps-open.md#not-triaged-3).

### A replaced element's two axes were sized independently

- **Tests:** `css-sizing/replaced-max-size-saturation` (8.3% on CI),
  `css-sizing/block-image-percentage-max-height-inside-inline` (9.7%) and
  `css-sizing/image-percentage-max-height-in-anonymous-block` (9.7%) — **all three
  pass (100%)** against their own references. Each asks for a 100px green square;
  Broiler drew a 1000×1000 or 8000×8000 block, clipped by the viewport into a
  full-page slab.
- **None was a reference disagreement.** All three reproduced offline against the
  `rel=match` reference the test itself declares — 8.6%, 10.1% and 10.1%, within
  rounding of CI's numbers. The engine was wrong, not the golden.
- **Four defects, three of them diagnosed a run earlier.**
  1. **The min/max pass clamped four properties one at a time.** A replaced box's
     two axes are coupled by its ratio: an axis left `auto` was *derived* from the
     other one, so clamping the stated axis has to re-derive it — and when both axes
     are auto and both maximums are violated, neither clamp can simply win. CSS2.1
     §10.4 settles that with a constraint-violation table that compares how hard
     each bound bites (`max-width/w` against `max-height/h`) and keeps the shape.
     Broiler had **no `max-width` arm for a replaced element at all**, and applied
     the other three independently. The table now lives in
     `Broiler.Layout.Engine.ReplacedBoxSizing`, shared by the two paths that size a
     replaced box so they cannot drift.
  2. **`max-height` was never applied to an `inline-block`.** `FlowInlineBlock` had
     a `min-height` arm and nothing beside it, so `height: 1000px; max-height: 60px`
     stayed 1000px tall while the same declarations on a `display: block` box
     clamped correctly. Never about replaced elements — every atomic inline-level
     box had it.
  3. **`<canvas>` was not a replaced element.** HTML §4.12.5 makes its
     `width`/`height` content attributes the dimensions of its *bitmap* — its
     natural size — and the Rendering section maps no presentation `width`/`height`
     for it, unlike `<img>` or `<table>`. `DomParser.TranslateAttributes` projected
     them onto CSS `width`/`height` regardless, which made both axes independently
     *stated*, so even a correct §10.4 pass would give 120×100 where the test wants
     100×100. Left alone entirely, a `<canvas>` laid out as a non-replaced inline —
     the one box type `max-width`/`max-height` do not apply to. `CorrectCanvasBoxes`
     records the attributes as the box's natural size and makes it an atomic
     inline-level box; only the UA default `display` is replaced, an author
     `display` is theirs to keep.
  4. **A percentage `min-`/`max-height` resolved against an anonymous block** — and
     an anonymous block is not an element, has no author height, and so is *always*
     indefinite, which turned the percentage into its initial value (`0` / `none`)
     and made it clamp nothing. Browsers climb to the nearest real element. Both
     percentage-max-height tests are built around an image that lands in an
     anonymous block — one after a sibling `<div>`, one through a `<span>` that a
     block-inside-inline split has to break up. This one only became visible once
     defect 1 stopped masking it.
- **Every expectation was Chromium's, measured rather than read off the spec** —
  through `getBoundingClientRect` on nine constructed probes. That was worth doing:
  it overturned one reading that looked obvious and is wrong.

  | Probe | Chromium | Before | After |
  | --- | --- | --- | --- |
  | 1×1 img, `max-width:100px; height:1000px; max-height:100%` in a 100px block | 100×100 | 1000×1000 | **100×100** |
  | 1×1 img, `max-width:100px; height:1000px` (no max-height) | 100×**1000** | 1000×1000 | **100×1000** |
  | `<canvas width=8000 height=8000>`, `max-width:120px; max-height:100px` | 100×100 | 8000×8000 | **100×100** |
  | `<canvas width=400 height=200>`, `max-width:100px` | 100×50 | 400×200 | **100×50** |
  | `<canvas width=400 height=200>`, no CSS | 400×200 | 0-sized inline | **400×200** |
  | `inline-block`, `width:200px; height:1000px; max-height:60px` | 200×60 | 200×1000 | **200×60** |
  | `inline-block`, `height:1000px; max-height:100%` in a 100px block | 50×100 | 50×1000 | **50×100** |
  | block img in a `<span>`, `max-height:100%` in a 100px block | 100×100 | 1000×1000 | **100×100** |
  | img, `width:1000px; max-width:100px`, auto height | 100×100 | 1000×1000 | **100×100** |

  **Row 2 is the one to keep.** `height: 1000px; max-width: 100px` on a 1:1 image is
  **100×1000**, not the 100×100 the §10.4 table would give — because the table is
  written for "both `width` and `height` specified as `auto`", and a *stated* axis is
  never re-derived from a clamp on the other one. Applying the table unconditionally
  would have "fixed" three tests and quietly broken every image with a stated height
  and a `max-width`, which is a common shape. The gate on
  `widthIsAuto && heightIsAuto` is the whole difference and it is pinned by a test.
- **Where it landed.** Main repo: `ReplacedBoxSizing`, the replaced
  block-level/out-of-flow sizing path, the `max-height` arm in `FlowInlineBlock`,
  `CssBox.ResolveInlineSizeBounds`/`ResolveBlockSizeBounds` (which also stop a
  keyword `max-width: fit-content` from being parsed as `0px` and collapsing the
  box), `CssBox.TryGetPercentageBlockSizeBasis` and
  `CssBoxProperties.IntrinsicReplacedSize`. The `DomParser` half is `Broiler.HTML`
  **`1071e48`**, "parse: size a `<canvas>` as a replaced element, and mark a
  blockified inline split" — **upstream and pinned since 2026-08-13**, so it is live
  on CI. The type sits in the main repo and the call in the submodule deliberately,
  which is what kept the submodule change to the parse-time code and nothing else.
- **Verified.** `css/css-sizing` **253 → 260 of 562** reftests.
  `Broiler.Layout.Tests` 663 passing with 25 new cases. A **17 077-test reftest
  sweep across 18 directories**, before and after: **11 674 → 11 705 passing**, 34
  newly passing and 3 newly failing. The 34 are not only the three — the unified
  §10.7 clamp picks up ten `CSS2/normal-flow/max-height-*`, the percentage-basis
  walk picks up two `css-sizing/intrinsic-height-*-percentage-child` and two
  `css-tables/percent-height-replaced-in-percent-cell-*`, and modelling `<canvas>`
  picks up `css-flexbox/canvas-contain-size`,
  `css-grid/grid-items/percentage-size-indefinite-replaced` and four
  `css-sizing/intrinsic-percent-replaced-*`.
- **The three that regressed are all `<canvas>`, and two were passing by rendering
  nothing** — the trap, sprung in the other direction.
  `css-images/object-view-box-writing-mode-canvas` (94.3%): the test's canvas
  carries `background-color: black` and the reference's does not, so with the canvas
  sized at zero *both* sides were blank white and agreed.
  `css-grid/alignment/grid-align-baseline-005` (92.3%) is the same in a grid.
  `css-sizing/intrinsic-percent-replaced-012` (98.7% against a 99% threshold) is a
  genuine near-miss. All three are carried in
  [not fixed](wpt-rendering-gaps-open.md#canvas-cannot-paint-its-bitmap).
- **One rule was tried and removed on the evidence.** Declining the ratio transfer
  into a percentage block size with no definite basis looked right and improved
  `grid-align-baseline-005` from 92.3% to 96.8% — and cost three other tests, all of
  which assert that behaviour and all of which want the transfer. The sweep settled
  it; the reasoning on its own pointed the wrong way twice.

### An absolutely positioned `<img>` rendered nothing at all

- **Test:** `CSS2/positioning/abspos-025`, 13.6% on CI → **passes (99.6%)**. An
  `<img>` with `position: absolute; left: 4em; right: 0; top: 4em; bottom: 0` must
  keep its natural 15×15 size and let `right`/`bottom` give way (CSS2.1
  §10.3.8/§10.6.5); Broiler stretched it across the whole inset box.
- **Probing it turned up something worse than the test.** The stretch only happens
  when `right` is set. With `left`/`top` alone — the ordinary way anyone positions an
  image — an absolutely positioned `<img>` **rendered nothing at all**, and the same
  was true of a `<canvas>`. That is not in any reported list because no test in the
  top 30 exercises it; it came out of nine constructed probes measured against
  Chromium.
- **Both are the same missing clause, in two places that each say the right thing in
  a comment and then do not do it.** `ResolveBlockUsedWidth` solves the §10.3.7 inset
  equation "for absolutely positioned, **non-replaced** elements", and falls back to
  shrink-to-fit "for absolutely positioned **non-replaced** elements with auto
  width" — but neither branch excluded replaced boxes. With `right` set the first
  branch stretched the image to the insets; without it the second measured the box's
  *children*, of which an `<img>` has none, and produced zero. A zero-width box
  paints no image.
- **The fix** routes a block-level or out-of-flow replaced box through the same
  `CssBox.ResolveReplacedContentSize` the inline path already used, and skips both
  non-replaced branches for it. `TryGetNaturalReplacedSize` is what makes that
  possible for an `<img>` as well as a `<canvas>`.
- **Verified against Chromium on seven inset combinations** — all four insets, each
  pair, none at all, and `width: 40px` with four insets (40×40: the height follows
  the width through the ratio). Broiler matches on every one.
- **The sweep shows the size of it.** The same 17 077 reftests: **11 705 → 11 749
  passing**, and the 45 newly-passing tests are almost entirely the family this rule
  governs — 22 `CSS2/positioning/absolute-replaced-{width,height}-*`, `abspos-025`
  and `-026`, six `css-flexbox/flex-aspect-ratio-img-row-*`, four
  `css-flexbox/flex-minimum-width-flex-items-*`, and
  `css-position/position-absolute-replaced-no-intrinsic-size.tentative`. Against the
  run's own baseline that is **+79 newly passing and 4 newly failing**.

### `overflow` on `<body>` was never propagated to the viewport

- **Test:** `css-overflow/overflow-body-propagation-009`, 18.1% → **passes (100%)**.
  `body { overflow: clip }` on a 30×30 body holding a 10 000px child: CSS Overflow 3
  §3.3 applies that `overflow` to the **viewport** and leaves the body's own used
  value `visible`, so the child fills the canvas. Broiler clipped it to the body —
  0.3% of the canvas blue against a reference that is 82%.
- **Nothing was propagated, for any value.** Three probes (`hidden`, `clip`, `auto`)
  all clipped the body's own box, so this was not a missing keyword in a list — the
  rule was absent.
- **The fix is a used-value adjustment, and the interesting half is the
  disqualifications.** The value goes to the viewport, which Broiler already clips at
  the canvas edge, so propagating means *removing* the body's own clip rather than
  moving it onto the root element's box — those are different rectangles once the
  body has margins, and putting it on the root scored 25% where dropping it scores
  100%. It must also apply to the **first** `<body>` only, and only if that one
  generates a box: `overflow-body-propagation-016` is a document with two of them
  where the first is `display: none`, and there the second has to keep its own
  `overflow: hidden`. A first pass without that guard fixed 009 and broke 016.
- **Sweep: 11 749 → 11 750 passing, +4 and −3.** The gains are 009 and two more of
  its own family (`-014`, `-015`) plus `css-sizing/fit-content-block-size-abspos`.
  The three losses are pre-existing gaps the propagation exposes rather than causes.
- **What this is not.** The viewport is still not a real clip container — it is the
  canvas edge. That is indistinguishable for a top-level document, and it is why this
  fix is small; a nested browsing context with its own propagated overflow would need
  the real thing.

### Containment other than `paint` never stopped background propagation

- **Tests:** `css/css-contain/contain-body-bg-001` (layout), `-003` (size), `-004`
  (style) and `contain-html-bg-001`/`-003`/`-004` — all six 7.5% → **99.8%**,
  and all six pass their own reference at 100%.
- **Owner:** `Broiler.HTML` (`IR/PaintWalker.CanvasBackground.cs`,
  `HtmlContainerInt.cs`), upstream and pinned.
- **One condition, one keyword too narrow.** Each test paints `<body>` red under a
  white `<p>` that covers it exactly, so the only red that can reach the screen is
  red the *canvas* took from body. `FindCanvasBackgroundAndImage` suppressed
  propagation for `contain: paint` only (plus `strict`/`content`, the shorthands that
  include it), so `layout`, `size` and `style` propagated as if no containment were
  set and flooded the canvas. That is why `-002` — the `paint` member of each family
  — was the one already passing.
- **The spec names all four, and it names both elements.** CSS Contain 2 §2: *"when
  any containments are active on either the html or body elements, propagation of
  properties from the body element to the initial containing block, the viewport, or
  the canvas background, is disabled"*. So the check is now "is **any** containment
  active", tokenised rather than substring-matched (`none` must not read as a
  keyword), and it also answers yes for `content-visibility: hidden`/`auto`. Both
  halves of the cascade were fixed together: `PaintWalker` decides what paints the
  canvas and `HtmlContainerInt.GetRootBackgroundColor` decides the colour the surface
  is erased with — they have to agree, or the erase colour wins in the margins.
- **The mirror-image half, which the tests do not cover and Chromium settles.** The
  old code applied the same suppression to the *root's own* background, so
  `html { contain: paint; background: green }` painted a white canvas. That is wrong
  in the other direction: the root element's background **is** the canvas background
  rather than something propagated to it. Asked directly — Chromium under Playwright,
  five documents differing only in the `contain` value — the whole canvas comes back
  `rgb(0,128,0)` for `layout`, `paint`, `size`, `style` and no containment alike. Only
  `display: none` still holds the root's background back.
- **Calibrated against Chromium rather than inferred.** Ten further probes fixed the
  edges. Suppressing, on body: `contain: layout`, `style`, `inline-size`,
  `content-visibility: hidden` and `auto`. On html: `contain: inline-size` and
  `content-visibility: auto`. **Not** suppressing: `contain: none`, and no `contain`
  at all — both still flood the canvas red, which is what keeps the fix from
  over-suppressing.
- **One divergence, recorded rather than papered over.** Containment does not apply
  to a non-atomic inline, and Chromium duly keeps propagating for
  `body { display: inline; contain: layout }`. Broiler cannot reproduce that
  distinction: instrumenting both code paths shows the box tree reporting `<body>` as
  `display: block` whatever `display` says, so a guard for it would be unreachable.
  The first draft carried one; it was removed once the instrumentation said so.
- **Measured.** `css/css-contain` **413 → 419 of 584**, and the failing-test set
  differs by exactly those six in one direction and nothing in the other. Average
  match 96.57% → 97.62% — and that +1.05 is precisely the six tests' own contribution
  (6 × (99.8 − 7.5) ÷ 524), so no other test in the subset moved even sub-threshold.
  **Regression-checked on the two subsets that own the canvas:** `css-backgrounds`
  (956 tests) and `css-color-adjust` (36) — **all 991 result lines byte-identical**.

### Logical viewport units did not parse

- **Test:** `css/css-page/page-box-008-print` — ours 99% hotpink where Chromium is
  99% yellow, because the `block-size: 100vb` box had no size. **Now passing on CI.**
- `vb`/`vi` did not parse at all. They now resolve against the **root element's**
  writing mode, which is what CSS Values 4 §6.1.4 specifies (not the element the unit
  appears on), so a per-pass factor set from the root's mode is the right
  granularity; `Broiler.HTML`'s layout pass hands that mode to the parser alongside
  the viewport size. The small/large/dynamic variants coincide with the default
  viewport in a headless render with no retractable UA chrome, so they canonicalise
  onto it.
- **Verified end-to-end:** `100vi × 100vb` fills the viewport, a `vertical-rl` root
  swaps the axes, and `100dvw × 50svh` covers half the canvas. A focused suite pins
  both axes, both writing modes, and all four viewport sizes.
- **Trap this uncovered:** canonicalising `svmin` → `vmin` means the unit *as
  written* can be longer than the unit reported. Three call sites split
  number-from-unit by the canonical length, so `"100svmin"` parsed its number as
  `"100s"` and silently resolved to 0. `GetUnit` now also reports the written length;
  any new site that splits a length must use it.
- **Its own reference still disagrees (4.0%)** for the same reason
  [`page-margin-002-print` does](wpt-rendering-gaps-wont-fix.md#page-margin-002-print-is-a-screenshot-artifact)
  — a `-print` test scored on screen. The golden comparison, which is what CI reports,
  passes.

### A table painted no background, and a block in a row group got no box

- **Test:** `css/css-page/monolithic-overflow-011-print`, 0.0% on CI → **passing,
  and 100% against its own reference** (95.1% yellow + 4.9% hotpink, which is what
  Chromium renders).
- **Neither half was the paged-media problem the test's name suggests**, and neither
  was `contain: size` — a `contain: size` box paints fine on its own. Two separate
  faults sat behind it, found by bisecting the test down to
  `<table style="background: yellow">`:
  1. **A table never painted its own background or borders at all** — CSS2.1 §17.5.1
     layer 1. The six-layer model covers a table's *internals*, but the painter
     handed the whole table to that pass (which starts at layer 2) while the
     background phase skipped `display: table` children outright and the foreground
     phase suppresses block backgrounds. Nobody emitted layer 1. Diagnosed at the
     source rather than from pixels: the fragment had correct bounds and a computed
     `background-color` of yellow and still emitted no fill. `TableBackgroundPaintTests`
     is the landed check.
  2. **A block child of a `display: table-row-group` got no box.** The row group
     measured 0×0 and its child computed `display: inline`, so the hotpink rectangle
     had nowhere to paint and the table was only as tall as its stray text. That is
     table fixup — a block inside a row group needs wrapping in an anonymous
     table-cell — and it is now implemented in
     `Broiler.Layout/Engine/CssLayoutEngineTable.cs` (CSS2.1 §17.2.1 anonymous
     table-row and table-cell generation).
- **This was recorded as open — it is not.** The earlier write-up had the second
  half holding the test at 2.26%. Re-measured 2026-08-13: it renders the reference
  exactly and is absent from the CI failure manifest.

### An atomic inline-level box was not a containing block, and a percentage height included its border

Two CSS2.1 defects in one file, found together on one grid test and each far wider than
the test that exposed it.

- **Test:** `css-grid/alignment/grid-item-aspect-ratio-justify-self-001`, CI 3.9%
  ([#1661](https://github.com/Broiler-Platform/Broiler/issues/1661).11). Its
  `check-layout-th.js` assertions go **2 / 40 → 20 / 40**: 18 of the 20 height assertions
  pass, where none did. The 20 that remain are one unimplemented rule and stay open as
  [`justify-self` on a grid item is not honoured](wpt-rendering-gaps-open.md#justify-self-on-a-grid-item-is-not-honoured).
- **Owner:** `Broiler.Layout` (`Engine/CssBox.ContainingBlock.cs`). Main repo, no patch.
- **The entry this replaces had the right bisect and the wrong suspect, twice.** It ruled
  out `CanTransferAspectRatioToBlockHeight` correctly, then reasoned that "the item's
  containing block is the `height: 32px` grid container". It is not — it is `<body>`, and
  the entry's own instruction to confirm that under a debugger before changing anything
  is what turned a plausible story into the actual cause.

**1. An atomic inline-level box was transparent to the containing-block walk.**

- §10.1 puts a box's containing block at its nearest ancestor **block container**, and
  `inline-block`, `inline-table`, `inline-flex` and `inline-grid` are all block
  containers: inline-level on the outside, an independent formatting context on the
  inside. The walk named only `inline-block`, so the other three were climbed straight
  past and a descendant's containing block came back as whatever block ancestor lay
  beyond them.
- **Why it presented as an `aspect-ratio` bug.** The wrong containing block is `<body>`,
  whose height is `auto`, so `height: 100%` computes to `auto` (§10.5). That is
  ordinarily invisible in a grid — stretch alignment fills the area with the right size
  anyway, which is why the item measures a correct 24×32 *without* the ratio. An
  `aspect-ratio` is what turns that `auto` into a **transfer from the used width**, and
  the used width came from the wrong containing block too: 1008 × 2 = **2016**, the
  height this test rendered for a 32px item.
- Neither condition alone reproduces, in any container. Measured on a 24×32 container
  holding one `height: 100%` item, on a 1024px-wide page:

  | Container | Item | Before | After |
  | --- | --- | --- | --- |
  | `inline-grid` | `height: 100%` | 24×32 | 24×32 |
  | `inline-grid` | `height: 100%` + `aspect-ratio` | **24×2048** | **24×32** |
  | `grid` | `height: 100%` + `aspect-ratio` | 24×32 | 24×32 |
  | `block` | `height: 100%` + `aspect-ratio` | 24×32 | 24×32 |
  | `inline-block` | `height: 100%` + `aspect-ratio` | 24×32 | 24×32 |

**2. A percentage height resolved against the containing block's border box.**

- `PercentageHeightContainingBlockHeight` normalised the containing block's specified
  height **to a border box** before resolving the percentage against it. §10.5 resolves
  against the **content** box, and the same file already carries
  `ResolveSpecifiedHeightToContentBox`, documented for exactly this and used by the
  sibling `min-`/`max-height` path (`TryGetPercentageBlockSizeBasis`). The two disagreed
  with each other, and only one of them was right.
- **Measured on a `height: 100%` child of a `height: 32px` box**, which is 32 in every
  row a browser renders:

  | Containing block | Before | After |
  | --- | --- | --- |
  | `height: 32px` | 32 | 32 |
  | `+ border: 2px` | **36** | 32 |
  | `+ padding: 5px` | **42** | 32 |
  | `+ box-sizing: border-box`, with the 2px border | 28 | 28 |

- **It hid behind two coincidences.** The border box and the content box are the same
  height whenever the containing block has neither border nor padding — the common case —
  and under `box-sizing: border-box` the specified height already *is* the border box, so
  both conversions are no-ops and the answer came out right by not doing anything. The
  same slip sat in the fallback that reads a settled `Size.Height`; it is stripped now,
  the way the min/max path already stripped it.

- **Sweep: 5 112 reftests over `css-grid`, `css-flexbox`, `css-sizing`, `css-tables`,
  `css-display`, `css-inline`, `css-writing-modes` and `css-position`, before and after
  on the same build — 2 397 → 2 395 passing, +7 and −9.** A net −2, and worth reading
  rather than summing:
  - **The 7 gains are real features on both sides**: five `css-grid/subgrid` tests
    (`repeat-auto-fill-002/-003/-004` at 95.3% → 100.0%, 89.6% → 99.2%,
    `placement-implicit-001` 97.9% → 100.0%, `orthogonal-writing-mode-005` 96.9% →
    99.7%) and two `grid-lanes` gap tests at 96.5% → 100.0%.
  - **All 9 losses are `grid-lanes/subgrid/grid-subgridded-to-grid-lanes`, and every one
    of them is a fake pass unmasked on the *reference* side.** Those tests declare
    `display: inline-grid-lanes`, which Broiler
    [deliberately drops](wpt-rendering-gaps-open.md#grid-lanes-is-an-unshipped-draft-feature)
    so the element keeps its default `block`; their references declare a real
    `display: inline-grid`. Rendering both sides of `row-subgrid-grid-gap-013` on both
    builds settles it: **the test render is byte-identical** (same MD5 before and after),
    and only the reference moved — from an inner subgrid stretched across the full
    1024px page to one honouring its `min-width: 30px`. The pair had been agreeing at
    99.07% because the reference shared the bug; it is 74.02% now because it does not.
    The eight others are the same shape, all previously sitting at 99.07–99.78%, just
    over the gate.
  - **Attributed, not assumed.** All 16 moved tests were re-run on a third build carrying
    only the containing-block walk: every move reproduces there, so **the percentage-basis
    fix moved no test in the 5 112**. It is spec-correct and inert on this corpus, and its
    effect is on the layout assertions above, which the pixel suite never reaches — the
    test declares no `rel=match`.
- **Unit suites: `Broiler.Layout.Tests` 908 / 908, and `Broiler.Cli.Tests` holds its
  baseline exactly** — 53 pre-existing failures before, the same 53 after, the failing set
  identical name for name, plus the new tests passing. One intermediate run reported a 54th
  (`ScriptCompileAheadOverlapTests.Every_Source_Is_Compiled_By_A_Worker_When_The_Budget_Is_On`);
  it did not recur, it passes in isolation on both builds, and it is a worker-scheduling
  budget assertion that touches no layout code — the machine was under memory pressure from
  a concurrent sweep. Re-run before believing a diff, the way
  [the flaky view-transition test](wpt-rendering-gaps-open.md#one-test-is-flaky) had to be.
- **Tests:** `Broiler.Layout.Tests/AtomicInlineContainingBlockTests.cs` reads
  `CssBox.ContainingBlock` directly — the four atomic inline displays, their four
  block-level counterparts, and a plain `display: inline` control that must stay
  transparent to the walk (three of the four atomic cases fail without the fix).
  `Broiler.Cli.Tests/AtomicInlineContainingBlockTests.cs` drives the same claim through
  real pages, as a parity assertion rather than absolute numbers: an atomic inline-level
  container must size its descendants exactly as its block-level counterpart does. That
  keeps it honest about the pairs whose shared behaviour is still wrong for other reasons
  — a percentage height inside a `table` resolves no better than inside an
  `inline-table`, and pinning today's number there would fail the day that separate gap
  is closed.

### A non-stretching grid item could not take its inline size from its `aspect-ratio`

- **Test:** `css-grid/alignment/grid-item-aspect-ratio-justify-self-001`, whose assertions
  go **20 / 40 → 29 / 40** on top of the containing-block fixes above. All nine
  non-stretching rows of the first group now measure **16×32** exactly, where they measured
  24×32. What remains is
  [two other rules](wpt-rendering-gaps-open.md#a-definite-inline-size-does-not-drive-the-block-axis-through-an-aspect-ratio).
- **Owner:** `Broiler.Layout` (`Engine/CssBox.Sizing.cs`, `Engine/CssBoxGrid.cs`). Main
  repo, no patch.
- **`justify-self` was implemented; the ratio could not run in the direction it needed.**
  `PlaceItemInArea` already declines to stretch an item whose `justify-self` is positional
  and aligns it in its area instead — the item simply *arrived* 24 wide, having filled its
  containing block during ordinary layout, and nothing then re-derived it. Reading the
  alignment code as missing would have been the wrong repair.
- **Only one of the two transfer directions existed for a non-replaced box.**
  `CanTransferAspectRatioToBlockHeight` takes an auto *height* from the used width, which is
  the direction an ordinary in-flow box needs: its auto width fills the containing block, so
  the width is known first. A grid item that is **not** stretched is the case where that is
  untrue — its inline size is auto and its block size is the definite one the area gave it —
  and the transfer has to run block→inline. Until now only a replaced box could do that
  (`ResolveReplacedContentSize` fills in whichever axis is auto).
  `TryResolveAspectRatioInlineWidth` is the new mirror of the existing helper, applying the
  ratio in the box named by `box-sizing` and clamping the result to `min-`/`max-width`.
- **The guard is the part worth keeping.** A first draft fired whenever the item did not
  fill its area, which is also true of an item with a stated `width: 20px` — and it
  overwrote that 20 with the ratio's 16. Only an *auto* inline size is the ratio's to fill
  in. The test that caught it is in the landed set.
- **Checked against Chromium on five shapes, not derived on paper.** All five widths agree:
  16 for the plain border-box case, 20 for `box-sizing: content-box` with 2px of padding
  (the ratio halves the *content* height, and the padding brings the border box back to 20),
  20 under a `min-width` floor, 20 for a stated width, and 24 when stretched. The paper
  arithmetic for the content-box row said 18 and was wrong; the engine and Chromium both
  say 20 × 36. The three rows whose block axis Chromium drives back through the ratio are
  recorded as the remaining gap rather than pinned at today's value.
- **Sweep: the same 5 112 reftests, and it moves nothing at all** — 2 395 passing before and
  after, `+0 / −0`, average match identical to three decimals. That is the expected result
  rather than a disappointing one: the transfer fires only for a grid item that carries an
  `aspect-ratio`, has an auto inline size *and* is not stretched, and no reftest in this
  corpus combines the three. **The evidence for this change is the layout assertions and the
  Chromium comparison, not the pixel suite** — the test it was written for declares no
  `rel=match`, so the reftest runner never sees it. Worth stating plainly, because a `+0/−0`
  sweep is only reassuring once you know it was capable of showing something.
- **Tests:** `Broiler.Cli.Tests/GridItemAspectRatioInlineSizeTests.cs` — the nine
  non-stretching `justify-self` values, the `normal`/`stretch` control that must still fill
  its area (a fix that applied the ratio unconditionally would pass the first and fail the
  second), the stated-width and `min-width` guards, and the `box-sizing` case.

### A grid's intrinsic inline size counted only its explicit tracks

- **Tests:** `css-grid/grid-lanes/subgrid/…/track-sizing/column-subgrid-auto-fill-008`
  ([#1723](https://github.com/Broiler-Platform/Broiler/issues/1723).8) goes **0.2% → 16.8%**
  against its own reference, and `column-subgrid-orthogonal-writing-mode-004` 95.0% → 95.5%.
  Neither passes; see the cost below, which is the more important half of this entry.
- **Owner:** `Broiler.Layout` (`Engine/CssBoxGrid.cs`). Main repo, no patch.
- **Root cause.** The shrink-to-fit inline size of a grid container summed only the tracks
  listed in `grid-template-columns`. Implicit columns — from auto-placement past the
  template, or from a `grid-column` reaching past it — contributed nothing, and
  `grid-auto-columns` was never consulted on that path at all; with no template the method
  bailed outright and the caller fell back to measuring inline *content*, which for a grid
  of empty divs is 0. So a grid built entirely from implicit columns painted its border and
  nothing else.
- **The definite-width pass was already correct, and the intrinsic path duplicated none of
  it.** Rather than write the column count a second way, `TryApplyGridTrackLayout`'s item
  collection and auto-placement were extracted to `TryCollectGridPlacements` and
  `TryPlaceGridItems`, and the intrinsic path runs the same two — so the count it sizes from
  is the count the real pass will resolve, by construction. Tracks outside the template are
  sized from `grid-auto-columns`, and gaps are charged across the full column count.
- **`grid-auto-columns` bounds it.** It defaults to `auto`, whose size is its items' and so
  the real track pass' job, so a grid that does not declare a definite `grid-auto-columns`
  returns exactly what it returned before and never reaches the placement replay.
- **A second bug fell out of it, and is fixed here too.** With the container finally 92px
  wide, a layout assertion showed the child ignoring `grid-column: 3 / span 4` and stretching
  across all six tracks. The implicit-only pass declines for a nested grid/flex/table item
  because sizing an *auto* row from one measurement of a nested container is untrustworthy —
  but that premise fails when every track is a declared fixed length, since then no
  measurement reaches a track at all. `AllTrackSizesAreFixed` relaxes exactly that half of
  the gate; the baseline half stays unconditional.
- **The cost: 8 reftests moved down, 4 of them out of passing, and every one is a false pass
  ending.** `css-grid/subgrid/repeat-auto-fill-002` and `-004` (100.0% → 87.9%), `-003`
  (99.2% → 74.3%) and `orthogonal-writing-mode-005` (99.7% → 98.4%) were passing because the
  test **and its reference** both collapsed to a 2px border on a blank page and matched each
  other. Rendering both at their true 92px is what exposes the disagreement, which is now
  filed as
  [a subgrid does not resolve `repeat(auto-fill, <line-names>)`](wpt-rendering-gaps-open.md#a-subgrid-does-not-resolve-repeatauto-fill-line-names).
  This was checked by rendering test and reference side by side on both builds rather than
  inferred: before, four thin black lines; after, four correctly-sized boxes whose contents
  differ.
- **Sweep: 5 140 reftests over `css-grid`, `css-flexbox`, `css-sizing`, `css-tables`,
  `css-display`, `css-inline`, `css-writing-modes` and `css-position`, before and after on
  the same build — 2 494 → 2 490 passing, +0 and −4.** Exactly **10 tests in 5 140 move at
  all**, and all 8 of the losers are the one shape this fix widens: an `inline-grid` with a
  fixed `grid-auto-columns`. `Broiler.Layout.Tests`, `Broiler.Wpt.Tests` and the
  grid/sizing slice of `Broiler.Cli.Tests` are byte-identical before and after.
- **The gap's own exit gate is met.** It asked that the `css/css-grid/grid-lanes` subset not
  regress, on the stated grounds that many of its passes are grids agreeing with their
  reference *because both sides collapse*: 869 tests, **195 → 195 passing, +0 / −0**. The
  collapse-pairs that did break are all in the plain-subgrid twins the same entry named.
- **What was tried and rejected.** Widening the item gate so a nested grid is always "simple"
  does fix the placement in isolation (a four-way probe goes 10/12 → 12/12), but leaves all
  four tests failing and drives two of them *further* down (87.9% → 83.9%), because the
  residual is the unimplemented subgrid auto-fill rather than the gate. Reverted.
- **Tests:** `Broiler.Cli.Tests/GridImplicitColumnIntrinsicWidthTests.cs` — the definite
  `grid-column` past the template, the templateless auto-placed item, implicit columns
  extending rather than replacing a template, gaps charged between implicit columns, and the
  guard that an intrinsic `grid-auto-columns` leaves the old answer alone.
  `Broiler.Cli.Tests/GridFixedTrackItemPlacementTests.cs` — a plain block, a nested grid and
  a nested subgrid child all landing in the columns they asked for, plus the auto-row case
  that must still be gated away.

### A box taller than a column set stayed in the first column

- **Tests:** `css-break/borders-002` **95.3% → passes at 99.2%**,
  `out-of-flow-in-multicolumn-014` **94.3% → passes at 100%**, and
  `table/table-border-007` **98.0% → passes at 99.2%**. Six more move a long way
  without reaching the gate: `fieldset-002` 86.4% → 98.7%, `borders-003`
  88.9% → 96.7%, `-004` 89.1% → 96.8%, `-005` 89.6% → 96.9%, `-006`
  95.3% → 98.8%, and `rounded-clipped-border` 85.1% → 89.6%. Two are lost, and
  they are the honest part of this entry — see below.
- **Owner:** `Broiler.Layout` (`CssBox.MultiColumn`). Main repo, no patch.
- **The engine filled a column set by *moving* whole boxes into it**, which covers
  a column set made of several blocks and not the shape most of the fragmentation
  corpus is actually written in: **one** block, taller than the column set, whose
  decoration is the thing under test. `borders-003` is a 250px bordered box in
  three 100px columns; `css-page/fixedpos-011-print` is a 400px block in four;
  `background-image-000` is four of them at once. Each rendered as a single column
  running out of the bottom of the column set.
- **Two things stood in the way, and the first is the smaller and the more
  decisive.** `FindMultiColumnFragmentParent` answered *null* for a column set
  with a single child — fewer than two boxes, nothing to distribute — so a
  one-child multi-column box was not columnised **at all**, however tall that child
  was. That rule was exactly right while the only thing a column set could do was
  move boxes; it stops being right the moment a box can be cut.
- **The second is the cut.** `SliceTallFragments` divides a fragment taller than
  the column into a run of column-tall pieces, and the loop that distributes
  fragments then places them without knowing anything about fragmentation. The
  decoration is *sliced*, not repeated — `box-decoration-break`'s initial value
  (§5.2) — so the border and its rounded corners belong to the two outer ends of
  the run and the joins between are square and open. That is what `borders-003`
  states in its own words: "The border should be rounded at the start (first
  column) and at the end (last column)."
- **Only a box with nothing in it is cut**, because a slice here is a real box in
  the tree rather than a paint instruction: a box with content would need that
  content divided too, which is the general fragmentation this engine does not do.
  A box with no content of its own is entirely described by its own decoration,
  and is what these tests are made of. (Cutting one *with* content was built and
  measured afterwards, and reverted — it works and it does not pay. The numbers
  are in
  [the open entry](wpt-rendering-gaps-open.md#cutting-a-box-with-content-in-it-across-columns--attempted-and-it-does-not-pay).)
- **A background image, a gradient and a box shadow are deliberately not sliced.**
  They are positioned against the box they are on, so cutting the box paints them
  once per piece instead of once across the run. Measured: slicing them cost
  `background-image-000` through `-002` and `box-shadow-002` through `-005`, so a
  box carrying one keeps the whole box until the paint can be told what the
  unfragmented run looked like.
- **Two tests are lost, and both were passing while rendering the wrong thing.**
  `out-of-flow-in-multicolumn-019` 100% → 98.9% and `overflowing-block-003`
  99.7% → 98.9%. Rendering the second at the old code shows why: its green content
  already ran 84px out of the bottom of both black boxes and it scored 99.7%
  anyway, because on a 1024×768 canvas 0.3% is 2,400 pixels. The change adds the
  column the test asks for and moves the mismatch around rather than causing it.
  That is an argument about what these two scores were worth, not a defence of
  losing them.
- **`fieldset-001` (79.6% → 78.0%) and `break-inside-avoid-min-block-size-1`/`-2`
  (83.7% → 82.0%) also slip**, and are not diagnosed. (Both were diagnosed
  afterwards, in [the entry below](#legend-was-an-inline-box-and-two-things-behind-it):
  `break-inside-avoid-min-block-size-1` recovers to 96.3% once `min-block-size`
  reaches layout at all, and `fieldset-001` is waiting on the column set cutting a
  box that has content in it.)
- **Verified:** four render tests in `MultiColumnFragmentationTests` — the cut
  itself, a block that fits staying put, `break-inside: avoid` keeping a box
  whole, and the border being drawn only at the run's two ends; two of the four
  fail with the fix reverted. Measured with the submodules at their pinned
  pointers: paged `css/css-break` 91 → **92** of 204 with the average
  87.11% → **87.38%**, three gained and two lost. `css/css-page` holds at 135
  paged and 142 default, with `fixedpos-011-print` 97.0% → 98.3% paged and
  95.2% → 97.0% default; paged `css/CSS2` (96), `css/css-backgrounds` (424) and
  `css/css-values` (104) do not move. One change in the set — not counting the
  whitespace between two elements as a fragment — moved no test on its own, and is
  kept because it is what makes the single-child rule mean what it says rather
  than depend on how the markup is indented.

### `<legend>` was an inline box, and two things behind it

- **Tests:** `css-break/break-inside-avoid-min-block-size-1` **82.0% → 96.3%** and
  both `css-sizing/aspect-ratio/fieldset-element-001` and `-002` to **exactly
  100%** (from 99.0% and 99.9%) — all three from this repository alone. With
  `patches/0004` and `0005` applied, `css-break/fieldset-004` 84.4% → **88.5%**
  and `fieldset-003` 91.3% → **92.1%**. **No test changes state**, and
  `fieldset-001` — the test this started from — goes 78.0% → 77.7% and still
  fails; why is below.
- **Owner:** `Broiler.Layout` (`CssBox.Fieldset`, `CssBox.Layout`,
  `CssBoxProperties`, `CssUtils`) plus a one-line user-agent addition in each of
  `Broiler.CSS` and `Broiler.HTML` — **pending patches**, because the push to
  either submodule answers 403.
- **The core, and it is one line twice over.** HTML's rendering section makes a
  `<legend>` a block box. Neither user-agent source said so: `Broiler.CSS`'s
  `CssUserAgentDefaults.DisplayValues` lists `fieldset` and every other
  block-level element and not `legend`, and `Broiler.HTML`'s default style sheet
  has the same omission. Left at the CSS initial value a legend is **inline**, so
  its `width`, `height` and `padding` do nothing at all. A four-way render pins
  it — a `<legend>`, the same legend with `display: block`, a `<span>` and a
  `<div>`, each given `width: 100px; height: 19px; padding: 10px 7px 20px 3px`:
  the `<div>` and the block legend occupy 49px, the bare legend and the `<span>`
  occupy 19px.
- **The main-repo half is the rendered legend's placement.** A fieldset's first
  legend is not laid out in its content (HTML §15.5.13): it belongs to the
  block-start border, its margin box centred on that border, so a legend taller
  than the border stands proud of the border box and the content begins below it
  rather than below the border. The rule was read back out of
  `fieldset-001`'s own reference, which states the same layout with a `<p>`, a
  `margin-top` making room for the part standing above, and an absolutely
  positioned legend at a negative `top`: a 49px legend margin box on a 6px border
  sits at `6/2 − 49/2 = −21.5`, and the content starts at `6/2 + 49/2 = 27.5`.
  It acts on a block-level legend and nothing else, so against the pinned pointers
  it is inert.
- **Making the legend a block exposed two more gaps, and the same tests rest on
  both.** Neither is about fieldsets:
  - **A preferred aspect ratio sized a box below its own content.** CSS Sizing 4
    §5.1 makes `min-height: auto` resolve to the content-based minimum, so a ratio
    that would make the box shorter than its content gives way. The transferred
    ratio height was overwriting the content height outright, which is how
    `fieldset-element-001`'s 200px-wide `aspect-ratio: 20/1` fieldset came out
    10px tall against a reference 57px tall. Once the legend was a block, the
    black legend covered that whole 10px strip and the test went from wrong to
    visibly wrong.
  - **The flow-relative minimum and maximum sizes never reached layout.**
    `min-block-size`, `max-block-size` and their inline counterparts parse in
    `Broiler.CSS` and were dropped on the floor by `CssUtils` — there was nowhere
    to put them. They now have their own properties and the physical `min-`/`max-`
    longhands consult whichever names the same axis under the box's writing mode,
    the way `Height` already consults `BlockSize`. That is what caps the
    content-based minimum in `fieldset-element-002`, and it is also the whole of
    `break-inside-avoid-min-block-size-1`, which is written on `min-block-size`
    and had nothing to read — the 14-point jump above.
- **Why `fieldset-001` still fails, stated plainly.** Two reasons, and the legend
  is neither. Its column set has to cut a box with *content* in it, which is the
  general fragmentation
  [the entry above](#a-box-taller-than-a-column-set-stayed-in-the-first-column)
  deliberately does not do. And the column pass walks *into* the fieldset — the
  single-child descent that exists for `html` → `body` — and redistributes the
  fieldset's own children over the three columns, which throws the legend
  placement away for this test while the fieldset's border stays drawn once,
  around the first column. Stopping that descent at a box that paints its own
  decoration is the right rule and was measured: `css/css-break` 92 → **88**. It
  is not worth four tests to be right about, so the descent stays and this is
  filed here rather than pretended away.
- **Verified:** five tests in `Broiler.Layout.Tests/LogicalMinMaxSizeTests` for
  the flow-relative bounds including the vertical-writing-mode swap and the
  physical longhand still winning, and five render tests in
  `Broiler.Wpt.Tests/FieldsetLegendRenderTests` for the placement, the second
  legend staying content, an inline legend not moving, and the two aspect-ratio
  cases. Measured with the submodules at their pinned pointers: paged
  `css/css-break` holds at 92 of 204 with the average 87.38% → **87.45%**,
  `css/css-sizing` at 74 of 112, and paged `css/css-page` (135), default
  `css/css-page` (142), `css/CSS2` (96), `css/css-backgrounds` (424) and
  `css/css-values` (104) are byte-identical test-for-test. With the two patches
  applied the css-break average reaches 87.47% and the three `fieldset-*` moves
  above appear; nothing else changes.

---

## Paint and the renderer

### Six SVG shape elements were never drawn when an attribute held a slash

- **Tests:** `conformance-checkers/html-svg/types-dom-06-f-isvalid`,
  `struct-cond-01-t-isvalid`, `struct-cond-overview-04-f-isvalid`,
  `struct-image-02-b-isvalid`, `svg/…/gradient-external-reference`. Partial improvements —
  each has further gaps of its own.
- **Owner:** `Broiler.Layout` (`IR/SvgRenderer.cs`). Main repo.
- **Root cause.** `<rect>`, `<circle>`, `<line>`, `<ellipse>`, `<polygon>` and `<polyline>`
  matched their attribute run with `[^/>]*`, which excludes `/`. Any element with a slash
  anywhere in **any** attribute value therefore failed to match and was never drawn —
  `requiredFeatures="http://www.w3.org/TR/SVG11/feature#Shape"`,
  `fill="url(support/resources.svg#greenGradient)"`,
  `filter="url(support/hueRotate.svg#MyFilter)"`. **Nothing reported it**, because an
  element that does not match is not an error; it simply never reached the display list.
- **The same defect was already found and fixed for `<path>`** — `PathElementRegex` is the
  quote-aware form, added because an arc command can contain a slash — and the other six
  were left behind. They now use that pattern. Worth knowing for the next regex added to
  this file: the shape passes are one regex per element type swept over the whole markup,
  so a pattern that silently fails to match costs a whole element class.

### An SVG presentation attribute outranked the author stylesheet

- **Test:** `conformance-checkers/html-svg/styling-css-05-b-isvalid`
  ([#1670](https://github.com/Broiler-Platform/Broiler/issues/1670).18), whose frame goes
  from **1.1% green to 86.7%**.
- **Owner:** `src/Broiler.HtmlBridge.Dom/DomBridge.Serialization.SvgZoom.cs`. Main repo.
- **Root cause.** `ApplySvgPresentationAttribute` is how a cascaded value reaches
  `SvgRenderer` at all, and its first statement was `if (HasAttr(element, propertyName))
  return;` — so the projection was skipped exactly when the element declared `fill` or
  `stroke` itself. SVG 1.1 §6.4 ranks a presentation attribute as an author-origin rule of
  **specificity 0 inserted at the start of the author sheet**, so any author rule outranks
  it: the deferral had the priority backwards. The test is a document of
  `<rect fill="none">` under `:lang(en) { fill: green }`.
- **Scoped to `fill` and `stroke`, deliberately.** The font properties this helper also
  projects are *inherited*, so their cascaded value on an SVG element is whatever the
  enclosing document sets; overwriting there would clobber a
  `font-family="SVGFreeSansASCII"` attribute with the body font. Measured rather than
  assumed: a `<body style="font-family:Courier">` does reach such an element's computed
  value.
- **The conformance-checkers family declares no `rel=match` reference**, so the reftest
  suite cannot score it at all — the number above is a direct read of the render against
  what the test asks for. `svg`, `css/css-masking` and `conformance-checkers` together go
  372 → 373 passing, +1 / −0.

### `clip-path` modelled only `inset()`

- **Tests:** `css/css-masking/clip-path/clip-path-document-element` and
  `-will-change`, 1.0% on CI → **both pass their own reference at 100%**, and both
  are absent from the CI failure manifest.
- **Owner:** `Broiler.HTML` (`IR/PaintWalker.Geometry.cs`) and `Broiler.Graphics`.
- **The gap.** `TryCreateInsetClipPathItem` parsed `inset()` and nothing else, and
  modelled it as a **rectangle**. These two tests use `polygon()` — an L-shape on
  the document element, which must also clip the propagated root background — so
  the render was the unclipped page.
- **What landed, in both submodules and both upstream and pinned:**
  - `Broiler.Graphics` **`b8aefa2`**, "Add a polygon clip to the graphics clip
    stack" — a real path clip rather than a `ClipItem` rect.
  - `Broiler.HTML` **`cabb66c`** ("Clip to clip-path: circle() and ellipse()") and
    **`e2fe977`** ("Resolve clip-path: url(#id) against the document's `<clipPath>`
    definitions"). `PaintWalker.Geometry` now dispatches `polygon()`, `circle()`,
    `ellipse()` and `url(#…)` alongside `inset()`.
- **This was recorded as open**, on the grounds that a path clip needed a submodule
  the session could not push to. It has since landed. `clip-path-element-userSpaceOnUse-004`
  moved with it — 2.9% → **82.6%** — but is [still failing](wpt-rendering-gaps-open.md#svg-clippath-referenced-by-url).

### A `visibility: hidden` box stopped clipping its visible descendants

- **Test:** `css/css-overflow/overflow-scroll-resize-visibility-hidden`, 5.9% →
  **100%**. Two `visibility: hidden` 100×100 scrollers each hold a 1000×1000 green
  child that re-declares `visibility: visible`; the reference is the two 100×100
  green squares the scrollers clip it to. Ours was the whole viewport green.
- **Owner:** `Broiler.HTML` (`IR/PaintWalker.Stacking.cs`, `f056363` "Keep the
  overflow clip of a visibility:hidden box"), upstream and pinned.
- **Neither `resize` nor `scroll` is the interesting part of the test name.** Five
  constructed probes separate the variables: the same scroller with
  `visibility: visible` clips correctly **with and without `resize: both`**, and
  swapping `overflow: scroll` for `overflow: hidden` makes no difference either. The
  single variable that changes the outcome is `visibility: hidden` on the scroller.
- **The cause is one early return.** `PaintWalker.PaintFragment` handles a
  non-visible fragment by calling `PaintChildren` and returning — correctly, since
  CSS2.1 §11.2 makes `visibility: hidden` suppress only the box's *own* rendering and
  a descendant may re-declare `visible`. But that early return jumps over everything
  the visible path sets up afterwards, including the overflow clip. The box is still
  generated and still clips; only its own painting is suppressed.
- **Not changed: the foreground phase.** `PaintFragmentForegroundPhase` returns on a
  non-visible fragment *without* descending at all, so a visible descendant of a
  hidden box inside a table never paints. That is a separate pre-existing gap with a
  different fix.
- **Measured.** `css/css-overflow` **441 → 442 of 772**, and the per-test diff across
  that whole subset is **two lines**. Checked for over-reach on the subset that
  shares the clip path: `css/css-contain` re-run gives **583 result lines identical
  to the pristine baseline**. 10 cases cover every clipping `overflow` value plus
  `contain: paint`, `visibility: collapse`, the `resize: both` the test is named for,
  and three controls — including that the child still paints *inside* the clip, since
  a fix that simply suppressed the visible descendant would pass the clip assertion
  and be wrong.

### A frame's canvas was never transparent

- **Tests:** `…/color-scheme-iframe-background-mismatch-opaque-cross-origin-003.sub`
  (94.7% → **99.8%, passing**), `…/color-scheme-iframe-background` (69.0% → 98.9%,
  then **99.4% passing** with the bevel fix) and
  `…/color-scheme-iframe-background-mismatch-used-preferred` (94.6% → **99.5%,
  passing**), which fell out with them.
- **Owner:** `Broiler.HTML` (`HtmlRender`, `PaintWalker.CanvasBackground`) for the
  renderer half — `d1cdad4`, upstream and pinned; `Broiler.Layout` for the rule and
  the cascade fix.
- **The rule.** CSS Color Adjust 1 §2.4: a nested browsing context's canvas is
  **transparent** — the embedder shows through it — *unless* the used colour scheme
  of the **embedding element** differs from the embedded root's, in which case the UA
  paints an opaque backdrop of the embedded root's scheme. The comparison is
  element-to-root, not document-to-document.
- **Two bugs, not one.**
  1. **The canvas was always opaque.** `RenderToImageCore` erased every embedded
     document to its resolved canvas colour, `PaintWalker.EmitCanvasBackground`
     painted the UA dark fill unconditionally, and `BlitOnto` copied the result
     pixel-for-pixel with no alpha. A frame could not be transparent at all, so the
     embedding element's `color-scheme` was never consulted.
  2. **`color-scheme` did not inherit.** §2.1 makes it an inherited property, but it
     was missing from `CssBoxProperties.InheritStyle` — unnoticed because it was only
     ever read off the root element, which inherits nothing. Fixing only the first bug
     regressed `…-002.sub` (the frame went transparent when the schemes genuinely
     *did* differ); the two have to land together.
- **What landed where.** `Broiler.Layout.Engine.EmbeddedCanvas` is the rule — a
  thread-static, scope-restoring lever like `CanvasBackdrop` and `DocumentRoot`,
  carrying the embedding element's computed `color-scheme` and answering
  `PaintsOpaqueBackdrop`. Unpinned means "not embedded", so it answers `true` and a
  top-level render is byte-identical.
- **Verified:** the `dark-color-scheme` directory goes **22 → 24 of 29** with nothing
  lost, and `html/semantics/embedded-content/the-iframe-element` is **unchanged
  across all 161 tests** — the change is inert for a frame that fills its own canvas,
  which is nearly all of them. 22 focused cases cover the rule, the cascade and the
  render.

### A 3D border was painted flat

- **Tests:** `…/color-scheme-iframe-background` (98.9% → **99.4%, passing**) and 89
  tests across `html/rendering` and `the-iframe-element` that carry an `<iframe>` or
  an `<hr>`.
- **Owner:** `Broiler.HTML` (`PaintWalker.Decorations`, `CssDefaults`) — `f8db3c6`,
  upstream and pinned; `Broiler.Layout` for the rule.
- **The gap.** CSS 2.1 §8.5.3 paints `inset`, `outset`, `groove` and `ridge` as a
  bevel — two sides in a darkened shade of the border colour, two in the colour
  itself. The IR paint path used the colour flat on all four sides, so the border the
  HTML Standard puts on every `<iframe>` (`2px inset`) and `<hr>` (`1px inset`) came
  out **solid black** where every browser paints `#9A9A9A` over `#EEEEEE`, and the
  `border: 2px groove` on every `<fieldset>` came out flat too. On a 600×400 frame
  that ring is 4 012 px — half of the test's residual, and exactly the half that kept
  it under the threshold.
- **Measured, not guessed.** The spec leaves the shades to the UA, so the rule came
  from screenshotting Chromium and sampling each side. The darkened side scales all
  three channels by the factor that takes the *largest* one down by 0.33 of full
  intensity — which is what keeps the hue: `rgb(200,100,50)` darkens to
  `rgb(116,58,29)`, all ×0.58, where the per-channel subtraction the greys alone
  suggested would have given `rgb(116,16,0)` and turned brown into red. The lit side
  is the colour itself, except black, whose lit side is `#545454`.
- **The second half is the UA stylesheet.** CSS makes the initial `border-color`
  `currentColor`, which bevels black-on-black; browsers substitute a light grey at
  paint time. Broiler states that grey in the UA stylesheet instead. **The two halves
  must land together:** shading while `hr` still carried its pre-bevelled per-side
  colours would darken `#9A9A9A` a second time and regress every `<hr>`.
- **`groove` and `ridge` split each side lengthwise**, and are emitted as two nested
  rings rather than one. A groove reads as `inset` on its outer half and `outset` on
  its inner half; a ridge is the mirror. The split sits at `ceil(width / 2)` from the
  outer edge — a 3px groove is two dark rows then one light — and below 2px there is
  no room for two halves, where Chromium paints a single stroke of the *lit* shade on
  all four sides. That 1px case is the one place the two styles agree, and it was
  found by measuring all four sides rather than just the top, which is where a
  per-side rule would have looked right and been wrong.
- **Verified:** across 665 tests, **89 changed and every one improved** — none worse
  — with one more passing. 30 focused cases pin the shading numbers against the
  Chromium measurements. Against Chromium directly, a page of groove and ridge boxes
  matches to 99.95%.

### A border corner had no mitre, and no anti-aliasing

- **Owner:** `Broiler.HTML` (`RGraphicsRasterBackend`, `BCanvas`) — `f86b655`,
  upstream and pinned; `Broiler.Layout` for the coverage rule.
- **Two gaps, found one behind the other.** CSS 2.1 §8.5.4 divides a border at its
  corners by a straight line.
  1. **A stroke has no mitre.** Only `solid` was painted as a trapezoid; `inset`,
     `outset`, `groove` and `ridge` were stroked along their centre lines, and a
     stroke butts square into its neighbour — so whichever side was drawn last owned
     the whole corner. Invisible while two sides share a colour, glaring when they do
     not, which is exactly what a `groove` does.
  2. **The mitre was a staircase.** Filled by testing each pixel's centre, the
     diagonal steps one pixel per row where a browser lays one blended pixel along it.
- **Coverage, not a 45° special case.** The mitre is only diagonal when the two sides
  are equally wide. A 12px top against a 4px left slopes one-in-three, and the pixel
  coverages come out 1/6, 1/2, 5/6 — against Chromium's measured 0.158, 0.503, 0.842.
  The rule is the area of the pixel the trapezoid covers, and it reproduces both.
- **Only the mitres blend.** The first attempt anti-aliased *every* edge of the
  trapezoid and regressed 210 tests, five of them out of passing. A border's own
  edges are straight lines the layout puts where it puts them, and feathering them
  turns a 1px form-control border sitting on a half-pixel into two grey rows instead
  of one solid one. Axis-aligned edges keep the pixel-centre test; only the diagonals
  carry coverage. **That failure is the useful part of this entry** — the obvious
  version of the fix is the wrong one, and only a broad measurement said so.
- **Why the corner fill is opt-in.** Two trapezoids meeting along a mitre each cover
  about half of the pixels on it, so blended independently onto the page they leave
  the background showing through the seam. The corner rectangle already filled for
  same-coloured sides is now filled for every corner, with the colour of whichever
  side is drawn first, so the second blends over an opaque corner. A translucent side
  disables the whole thing, since that fill would composite its alpha twice.
- **Verified:** against Chromium directly, a 12px four-colour border's corners go from
  **48 differing pixels to 12** (the rest off by 1/255), and a page of groove and
  ridge boxes from **425 to 21**. Across 1 949 tests of `css/css-backgrounds`,
  `html/rendering`, `css/css-gaps` and this directory, **no test changes state in
  either direction** and the net is **+1.578 points**.

### A colour-only SVG filter chain now recolours the shape

- **Test:** `css/filter-effects/fecolormatrix-negative`, 7.7% → **99.6%**, and it
  passes its own reference at 100%. Its reference is a cyan rectangle, which is what
  the test's own assertion says to expect; Broiler painted the unfiltered `#ffaa00`.
- **Owner:** `Broiler.Layout` (`IR/SvgColorFilter.cs`, `IR/SvgRenderer.cs`,
  `IR/SvgFilterTable.cs`). Main repo.
- **What the filter does, in closed form.** `feColorMatrix` with the negative entries
  inverts each channel — `#ffaa00` → (0, 0.333, 1) — and the arithmetic `feComposite`
  with `k2="255"` multiplies the premultiplied channels by 255, so every non-zero one
  saturates: (0, 1, 1), cyan. **No raster pipeline is needed to know that, because a
  shape filled with one solid colour has a source graphic that is that colour inside
  it and transparent black outside**, so a chain of per-pixel colour operations
  produces exactly two colours. Same modelling the engine already applied to an
  `feFlood`-only filter, extended to a chain.
- **Why the region does not have to be modelled too** — the trap that makes this cheap
  rather than a filter engine. Every step modelled here maps zero alpha to zero alpha,
  so the *outside* colour stays transparent and the filter region never becomes
  visible. `AMatrixThatZeroesAlpha_MakesTheShapeTransparent` pins that property rather
  than leaving it as a comment.
- **Deliberately narrow, and every bail-out renders unfiltered.** Only `feColorMatrix`
  (`type="matrix"`) and `feComposite` (`operator="arithmetic"`); only a straight chain;
  only when the filter declares `color-interpolation-filters="sRGB"`, because the
  default is linearRGB and the conversion is not modelled. Applied only to an
  **unstroked** `<rect>`: a stroked shape is not one colour.
- **A pre-existing over-match found while testing this, and left alone.**
  `CollectFloodFilters` takes the first `feFlood` in a filter body whatever else is in
  the chain, so a filter that is `feColorMatrix` + `feFlood` is treated as flood-only.
  This change neither introduces nor fixes it; the test file records it and declines
  to pin it.
- **Measured.** `css/filter-effects` **180 → 181 of 388**, and the per-test diff across
  the whole subset is two lines — that test, and `fecolormatrix-display-p3` improving
  97.2% → 98.0% without crossing the threshold (its residual is the Display-P3 colour
  space, a separate gap). **Nothing else moved in either direction.**

### SVG text, pattern fills, symbols and transforms were all missing

- **Tests:** seven of the nine `conformance-checkers` entries in
  [#1658](https://github.com/Broiler-Platform/Broiler/issues/1658). Measured against
  locally generated Chromium references at the run's own 99% threshold:

  | Test | Was | Now |
  | --- | --- | --- |
  | `html-svg/pservers-pattern-03-f-isvalid` | 21.9% | 94.2% |
  | `html-svg/pservers-grad-03-b-isvalid` | 29.9% | 93.8% |
  | `html-svg/pservers-pattern-02-f-isvalid` | 19.3% | 92.3% |
  | `html-svg/struct-symbol-01-b-isvalid` | 30.6% | 81.4% |

- **Owner:** `Broiler.Layout` (`IR/SvgRenderer*.cs`, `IR/SvgStructure.cs`,
  `IR/SvgTransform.cs`, `IR/SvgItemTransformer.cs`, `IR/SvgTextEnvironment.cs`,
  `Engine/CssLayoutEngine.cs`, `Engine/CssBox.Sizing.cs`). Main repo, no patch.
- **Five separate gaps behind one family**, which is why the family had sat as *large
  documents, not triaged*:

  1. **Every SVG `<text>` painted nothing at all.**
     `RGraphicsRasterBackend.RenderSvgText` returns early unless the item carries a
     resolved font, and **nothing in the engine ever set one** — `DrawSvgTextItem`
     was constructed with a family name and a size and no handle. The renderer builds
     its items with no box to ask for a font, so the host's font services are now
     published for the render pass the same way the SVG filter and clip-path tables
     already are (`SvgTextEnvironment`, seeded by `FragmentTreeBuilder.Build`) — no
     submodule change, because the item is built in the main repo.
  2. **The per-shape regexes could not see the tree.** One regex per shape type over
     the whole markup draws every `<rect>` wherever it sits, including the ones inside
     `<defs>`, `<symbol>` and `<pattern>` that SVG renders only through a reference —
     which is why `struct-symbol-01-b` painted a symbol's contents across the whole
     canvas — and it cannot see that a `<g font-size="32">` sets the size for the text
     inside it. `SvgStructure` scans the tags once with a stack and records, per
     start-tag offset, whether the element paints, what it inherits, and the transform
     it is under; the existing loops consult it by `Match.Index` and are otherwise
     unchanged.
  3. **`fill="url(#id)"` reached the colour parser**, matched no named colour, and fell
     through to the caller's default — solid black, which is most of the canvas in every
     `pservers-*` test. Patterns are now tiled as ordinary display items clipped to the
     shape, with `patternUnits`, `patternContentUnits`, `viewBox`, `href` inheritance and
     `patternTransform`; a reference that resolves to nothing paintable takes its declared
     fallback paint (SVG 2 §13.2) instead of black.
  4. **`<use>` drew nothing**, so a `<symbol>` had no way to reach the canvas at all.
     It now renders its referent, establishing the `<symbol>` viewport, and
     `preserveAspectRatio` is read rather than assumed to be `xMidYMid meet`.
  5. **An atomic inline's percentage height resolved against the containing block's
     *width*.** The wrong axis outright — and the reason
     `<svg width="100%" height="100%">`, the shape every one of these pages has, came out
     as tall as the page is wide and then let "xMidYMid meet" centre its drawing a couple
     of hundred pixels down the box. It resolves against the block size now, or computes
     to `auto` when there is none (CSS 2.1 §10.5), which lets the viewBox ratio give the
     height the reference browser uses. The in-flow grid-item branch beside it had already
     sidestepped that basis for its own case and named it as wrong.

- **A transform is baked into the geometry, not pushed as a layer** — the wrong turn
  worth not repeating. `TransformItem` exists and the backend takes it, but
  `GraphicsAdapter.SaveTransformLayer` keeps only translations and uniform scales on the
  raster canvas; anything else falls through to a compat backend that the image renderer
  stubs out, and **the enclosed drawing disappears entirely**. A rotated
  `patternTransform` written that way rendered a blank rectangle. Mapping the tiles'
  own coordinates instead needs no layer, and a rotated rectangle is still exactly
  expressible — as the polygon primitive the backend already draws
  (`SvgItemTransformer`). The one shape it cannot carry under a rotation is an ellipse,
  whose primitive has axis-aligned radii; those are left in place rather than drawn
  somewhere wrong.
- **Verified:** the four tests above, the probe cases behind them (a `userSpaceOnUse`
  pattern, a rotated `patternTransform` and a `url(#missing) lime` fallback all match
  Chromium pixel-for-pixel), and 839 `Broiler.Layout.Tests` unchanged.

### `<path>` was never drawn, and three more gaps behind the same family

- **Tests:** the six `conformance-checkers/html-svg` entries of
  [#1661](https://github.com/Broiler-Platform/Broiler/issues/1661), and a great deal
  of the rest of that family with them. Measured against locally generated Chromium
  references over `conformance-checkers/html-svg`, `svg` and `css/filter-effects`
  (1682 tests): **78 improve by more than a point, 4 worsen, mean +0.41**.

  | Test | Was | Now |
  | --- | --- | --- |
  | `html-svg/color-prop-04-t-isvalid` | 32.1% | 79.3% |
  | `html-svg/struct-group-02-b-isvalid` | 68.6% | 97.1% |
  | `html-svg/render-elems-01-t-isvalid` | 68.2% | 96.4% |
  | `html-svg/linking-a-07-t-isvalid` | 74.2% | 95.7% |
  | `html-svg/filters-example-01-b-isvalid` | 56.1% | 86.8% |
  | `html-svg/struct-image-02-b-isvalid` | 30.7% | 51.0% |

- **Owner:** `Broiler.Layout` (`IR/SvgPathData.cs`, `IR/SvgPaintOrder.cs`,
  `IR/SvgRenderer.Viewport.cs`, `IR/SvgRenderer*.cs`, `IR/SvgStructure.cs`). Main
  repo, no patch.
- **Five gaps, one family** — the same shape as the earlier `pservers-*` triage:

  1. **`<path>` painted nothing at all**, which is the commonest shape in SVG. There
     is no path display item, and the only pass over `<path>` harvested each one's
     start point so a `<textPath>` could be positioned. Paths are now flattened to
     straight-line subpaths and emitted through the **polygon and polyline items the
     backend already fills and strokes** — so this needed no new backend primitive
     and no submodule change, which is the whole reason it could land here.
  2. **The shape passes painted out of document order.** One regex per shape type,
     each sweeping the whole markup, emitted every rect, then every circle, then all
     the text — so an element a later sibling had to cover was painted over it
     instead. `color-prop-04-t`'s dropdown panel is opaque and sits after the
     paragraph text in the markup; the text showed through it.
  3. **A nested `<svg>` established no viewport.** Its children were drawn in the
     *root's* coordinate system, so the six panels of `coords-viewattr-03-b` all
     landed on the first one at the root's scale.
  4. **A `clip-path` on a shape did nothing.** The existing collection publishes to
     the table a CSS `clip-path` on an *HTML* element reads; the SVG attribute had no
     path at all.
  5. **A CSS system colour was not a colour.** `fill="Window"` matched no named
     colour and fell through to the caller's default — black — so a page built out of
     system colours painted as one black rectangle. `Broiler.CSS` already carries the
     table; nothing consulted it from here.

- **A path's subpaths fill as one ring, not one polygon each.** The rasterizer's fill
  is an even-odd crossing test, so joining them with bridge edges that retrace — and
  therefore cancel — gives an enclosed subpath the hole it should have. Filling each
  separately painted over it, which is what turned the three overlapping rings of
  `coords-viewattr-03-b` into a solid blob. Chaining them end to end instead would
  leave a closing edge that retraces no bridge and toggles a spurious wedge.
- **Drawing paths at all exposed `vector-effect: non-scaling-stroke`**, which was
  unimplemented. `svg/painting/reftests/non-scaling-stroke-precision-loss` draws a
  hairline under a view box 0.375 units tall over 344 pixels; scaling its width by
  that 917× factor put a band across the whole canvas. A non-scaling stroke now takes
  the viewport's scale rather than the view box's — *not* no scale at all, which
  halved the stroke of a zoomed viewport.
- **Two tests drop below the gate, and both are honest.**
  `non-scaling-stroke-008` (99.7% → 99.0%) passed while its stroke was ten times too
  wide and covered the canvas; `svg/types/scripted/SVGGraphicsElement.getBBox-10`
  reports geometry the renderer does not compute. `masking-path-14-f` regressed on
  the way and was closed by gap 4 above. `svg/interact/scripted/rect-hittest-002`
  appears in the diff and is **not** caused by this: it scores 97.7% on an unmodified
  build too — the flakiness
  [the method notes warn about](wpt-rendering-gaps-open.md#one-test-is-flaky), caught
  by re-running it against the unmodified build exactly as they say to.

### An `feFlood` feeding another primitive flooded the shape

- **Tests:** the whole `css/filter-effects/tainting-*` family — **31 tests crossed the
  99% gate**, from 98.4% to 99.2–100% — plus
  `conformance-checkers/html-svg/filters-blend-01-b` 31.1% → **38.2%**.
- **Owner:** `Broiler.Layout` (`IR/SvgRenderer.cs`, `IR/SvgColorFilter.cs`). Main repo.
- **The bug.** `CollectFloodFilters` registered a filter as flood-only whenever its body
  contained an `<feFlood>` *anywhere*, and a shape referencing a flood filter is replaced
  by a solid rectangle of the flood colour over the filter region — 20% larger than the
  shape on each axis. But a flood that another primitive then consumes describes a
  **backdrop**, not the result. Every `tainting-*` test floods and then composites, and
  each rendered as one solid flood-coloured rectangle. A flood is now taken as the whole
  filter only when it is the only primitive in it.
- **`feFlood` + `feBlend` is closed-form over a solid fill**, so it joins the two
  primitives [`SvgColorFilter`](#svg-text-pattern-fills-symbols-and-transforms-were-all-missing)
  already models: the backdrop is a single colour by construction, and the five separable
  SVG 1.1 blend modes composite with it under the ordinary source-over rule. A flood is
  not a chain step — it ignores its own `in` — so it contributes a named constant rather
  than continuing the chain, and a blend whose backdrop is anything else declines and
  leaves the shape unfiltered.
- **What it does not do.** A blend that names the flood as `in` rather than `in2` is
  declined rather than rendered the wrong way round: the running colour would then be the
  backdrop, which a one-colour model cannot express. `filters-blend-01-b`'s residual is
  the element `opacity` on each band, which is group compositing rather than recolouring
  and is deliberately outside this model.
- **Measured:** 0 tests lost across the 1682 in `conformance-checkers/html-svg`, `svg`
  and `css/filter-effects`; 9 `SvgColorFilterTests` added.

### A media element with nothing to show painted a black box

- **Tests:** `conformance-checkers/html/elements/{track,video}/src-isvalid` 14.4% →
  **100%**, and `.../audio/src-isvalid` 19.8% → **100%**.
- **Owner:** `Broiler.HTML` (`Parse/DomParser.cs`). It was waiting on a patch while its
  remote sat outside a this-repository session's push scope; it is upstream now as
  `a9be60a`, *Paint a media element's box only when it shows controls*, and the pinned
  pointer contains it, so it reaches CI through the pointer. Identify it by that commit
  subject, never by the patch number it once had.
- **What was wrong.** A `<video>` with no decodable media filled its whole box black, and
  an `<audio>` without `controls` laid out as a 300×32 black bar instead of not laying out
  at all. Each of those two test pages is 250 media elements; Chromium renders both as
  blank white pages and Broiler rendered walls of black.
- **What the reference browser actually does**, checked directly rather than assumed: an
  empty `<video>`, a `<video src="missing.mp4">` and a `<video autoplay><source></video>`
  all paint **transparent** — a div behind each one shows through — and only
  `<video controls>` draws anything, the control scrim. HTML §4.8.9 says as much: with
  neither poster frame nor video data the element "represents … nothing". The rendering
  section's UA stylesheet makes `audio:not([controls])` `display: none`, and an
  `<audio controls>` is 300×**54**, not 32.
- **So the placeholder follows `controls`, not the element:** a dark scrim under a video's
  controls, a light bar under an audio's, and nothing at all without them.
- **The main-repo tests moved with it, but not to the patched behaviour.**
  `WptCompositingTests`'s three video cases asserted the black fill directly; they now
  assert the replaced box's *extent* against an author background they set themselves —
  true with the patch and without it — because a fill the element is not supposed to draw
  is not something a test should pin. Transparency is pinned by the WPT tests instead.

### Animated images always painted their first frame

- **Tests:** the four `css/css-image-animation/image-animation-*-paused`. For all four
  Broiler's canvas was 100% `rgb(0,255,0)` and Chromium's 100% `rgb(255,0,0)`.
- **Owner:** Broiler.Media (frame selection) with `Broiler.HTML` (a paint-time clock).
- **What landed.** `ImageSequence.FrameAt` / `FrameIndexAt` answer "which frame is
  showing at time *t*", and `ImageAnimationClock` carries the presentation time a
  still render is taken at. The WPT runner pins that clock per test from the test's own
  `takeScreenshotDelayed(N)`, read from the source before the script pass and the
  post-processor strip the `<script>` that carries it. `StubImageAdapter`'s decode — the
  single seam where a decoded sequence collapses to one bitmap — selects the frame at
  that clock instead of taking `FirstFrame` (`BBitmap.DecodeFrameAt`, upstream and
  pinned).
- **The clamp is what makes the numbers work.** `anim-gr.gif`'s green frame carries a
  **10 ms** delay and its red frame 100 s. Taken literally, a 300 ms screenshot would
  land deep in a fast loop; every engine instead treats a delay that short as
  "unspecified" and substitutes 100 ms (Blink's threshold is 11 ms, and the references
  are Chromium's). So green occupies 0–100 ms and red everything after.
- **Verified:** the four tests went **0.0% → 100%** against locally generated Chromium
  references. Four focused tests cover the timeline (selection at successive times, the
  short-delay clamp, loop-count wrap versus hold, and the clock's nested pin/restore),
  and 14 cases cover the runner's delay extraction — including the negative half, that
  a test with no literal delay resolves to zero rather than guessing.
- **A cost worth naming:** the clock is process-wide, not thread-local, because image
  loading is dispatched to the thread pool — a `[ThreadStatic]` value would be invisible
  to the code that reads it. Concurrent renders at *different* presentation times are
  therefore unsupported, which is the honest state of a stack that renders one document
  per process.
- **Two of the four have since gone back to 0.0%, and that is correct.**
  `image-animation` was implemented afterwards, so Broiler now honours `paused` and
  holds the green frame Chromium never shows. See
  [won't fix](wpt-rendering-gaps-wont-fix.md#image-animation-paused--the-worked-example-of-the-cost).

### `object-fit` and `object-position` were not read at all

- **Tests:** the 42 `css/css-images/object-fit-*i` (the `<img>` variants), **21 → 42 of
  42**. Every one of the five fit keywords, against a raster image and against three
  kinds of SVG, at seven `object-position` values each.
- **Owner:** `Broiler.Layout` (`IR/ObjectFitPlacement.cs`, `IR/CssPositionValue.cs`) with
  a 21-line call site in `Broiler.HTML` (`IR/PaintWalker.Decorations.cs`).
- **The gap was total.** `EmitReplacedImage` drew every replaced element into its content
  box, which is `fill` behaviour whatever the author wrote. `object-fit-contain-svg-001i`,
  `-fill-svg-001i` and `-none-svg-001i` differ only in that one declaration and rendered
  to **byte-identical PNGs**; the `object-position` classes in them (`top right`,
  `bottom 1px right 2px`, …) had no effect either. Nothing in the engine named either
  property outside `Broiler.CSS`'s list of ones it claims to support.
- **Half the fix is on the reference side, and that is the part worth recording.** Each of
  these tests states its reference with `background-size` and `background-position` rather
  than with `object-fit`, and **four of its seven positions are written in the
  three- or four-component edge-offset syntax** — `top 25% left 25%`,
  `bottom 1px right 2px`, `top 3px center`, `center right 25%`. Paint read a
  `<position>` positionally, first component horizontal and second vertical, which is only
  the one- and two-component forms; the longer ones resolved to the origin, silently. So
  the reference was wrong before the test had a chance to be, and implementing
  `object-fit` alone would have moved nothing.
- **One resolver, three callers.** `CssPositionValue` reads the whole grammar and is
  shared by `object-position`, `background-position` on an image layer, and
  `background-position` on a gradient layer — which were two copies of the positional
  reading, so the same declaration could mean two things depending on whether the layer
  behind it was a bitmap or a gradient. The count of components is the spec's own
  disambiguation and the reason a `<position>` cannot be read token by token:
  `right 25%` is two components and means *x: right, y: 25%*, while `center right 25%` is
  three and means *25% of the free width in from the right edge*.
- **The concrete object size is defined over the ratio, not the size, and an SVG can carry
  one without the other.** `colors-8x16-noSize.svg` has a `viewBox` and no
  `width`/`height`, and `GetImageIntrinsics` reports the 300×150 default object size for
  it — deliberately, since that is what replaced *sizing* uses. Taking the ratio from that
  size put a 1∶2 image in a 48×24 box and cost `object-fit-cover-svg-004i`, which had been
  passing. `ImageIntrinsics` now reports the aspect ratio and whether the size is real
  alongside it, so `contain`/`cover` use the ratio and `none` can tell "no intrinsic size"
  from "an intrinsic size of 300×150" — which is exactly the distinction §5.1's default
  sizing algorithm turns on.
- **Overflow is clipped, not cropped.** `cover` always leaves the content box on one axis
  and `none` does whenever the image is larger than its box, so the call site emits a clip
  to the content box and draws the whole image into a rectangle that may exceed it.
  Cropping the *source* rectangle instead would have been wrong for the same reason the
  ratio was: the decoded bitmap is not the intrinsic size when an SVG is
  [supersampled at 2×](wpt-rendering-gaps-open.md#svg-as-an-image-went-through-a-second-weaker-svg-renderer--fixed).
  An object that fits emits no clip and paints exactly as it did before.
- **Measured.** `css/css-images` **234 → 262 of 460**, +28 and **−0**. By element:
  `<img>` **21 → 42 of 42**, `<object>` 5 → 12 of 40. The `<object>` gains are all the
  `<position>` half rather than this one — `<object data="…svg">` paints through
  `EmitSvgContent`, which this does not touch, so those seven crossed the threshold on
  their references alone (98.81% → 99.11%) and the rest stay just under it. `<embed>`,
  `<canvas>` and `<video poster>` do not move, and none of them is blocked on
  `object-fit`: their content is not painted at all, which for `<canvas>` is
  [the bitmap gap](wpt-rendering-gaps-open.md#canvas-cannot-paint-its-bitmap)
  and for the other two is element support Broiler does not have.
- **Tests:** `Broiler.Layout.Tests/ObjectFitPlacementTests.cs` (the five keywords against
  a box wider and narrower than the content's ratio, `scale-down`'s choice between two of
  them, a ratio without an intrinsic size, and which placements need the clip) and
  `CssPositionValueTests.cs` (all four grammar forms, the two-versus-three component
  disambiguation, percentages against the free space, and a `calc()` staying one component
  however much whitespace it holds).

---

## DOM and the bridge

### A root-relative frame `src` resolved against the wrong directory

- **Test:** `resource-timing/initiator-type/frameset`, 0.0% → **99.7%, passing**.
- **Owner:** `Broiler.Layout` (`FragmentTreeBuilder`) with the WPT runner. Main repo.
- **The previous diagnosis was wrong, and it named the wrong feature entirely.** This
  was filed as the frameset grid painting neither its canvas nor its frames' documents,
  with "render frames as nested browsing contexts on that grid" as the next action. None
  of that was needed: the grid, the sub-viewport projection and the frame document load
  all already worked. Bisecting the test down found the whole difference in the URL —
  the same page with `src="../resources/green.html"` rendered its frame correctly, and
  only `src="/resource-timing/resources/green.html"` came out blank. `<frameset>` was a
  red herring, and so was `<frame>`: an `<iframe>` with a root-relative `src` failed
  identically.
- **The bug.** HTML §"resolve a URL" resolves a leading `/` against the document's
  origin. A `file://` render has no origin, and
  `FragmentTreeBuilder.TryLoadEmbeddedDocument` joined the URL onto the containing
  directory like any other relative reference. **`Path.Combine` discards its left
  operand when the right one is rooted**, so the result was an absolute path at the
  filesystem root, `File.Exists` failed, and the frame painted empty. Silent, and it had
  nothing to do with framesets.
- **What landed.** `Broiler.Layout.Engine.DocumentRoot` is a thread-static,
  scope-restoring render lever (the shape of `CanvasBackdrop.Current` and
  `NativeZoom.Enabled`) carrying the directory a root-relative sub-document URL resolves
  against; `TryLoadEmbeddedDocument` takes a root-relative branch that reads from it,
  stripping the query and fragment and refusing to leave the root. The WPT runner pins it
  to the checkout around both render paths — the same root its stylesheet, image and
  script loaders already resolve `/`-paths against. **This was the one sub-resource kind
  with no such hook.**
- **Null by default is the point.** A host that sets nothing renders exactly as before.
- **Verified:** the render is the *right* pixels — 99.8% `#00FF00` plus the reference's
  own `<h1>Placeholder</h1>` text, matching Chromium's 99.8%/0.1% split. The
  `resource-timing/initiator-type` subset goes 8 → 9 passing with nothing lost. 13 focused
  cases pin the behaviour, including the negative halves: no root set → still empty; the
  root is *not* the page's own directory; `//host/path` is scheme-relative and must not be
  read off the local disk; `..` cannot escape the root; a bare `/` is not a document; and a
  directory-relative `src` is unaffected either way.
- **A genuine frameset bug, found alongside and also fixed:** a frameset with more than
  one frame painted only its first cell. `<frame>` was missing from `Broiler.DOM`'s
  void-element set, though `Broiler.HTML` has it, so `<frame src=a><frame src=b>` parsed
  the second frame as a *child* of the first and `DomParser.LayoutFramesetChildren` was
  handed one cell instead of two. Confirmed by writing the same markup with explicit
  `</frame>` tags, which renders both cells. Fixed in `Broiler.DOM` **`55057b8`**, "Treat
  `<frame>` as a void element in the parser and serializer", which is the pinned pointer.
  Both `cols` and `rows` framesets go from half-painted to both cells painting their own
  document. **No test in the current subset covers it** either way.

### A frame `src` with a query or a fragment resolved to nothing

- **Tests:** the four `the-img-element/sizes/parse-a-sizes-attribute-*` (39.0% each — see
  [the responsive-image entry](#an-img-loaded-nothing-at-all-when-its-source-came-from-srcset)
  for where they got to), and 68 other files in the checkout whose frame `src` carries a query.
- **Owner:** `Broiler.Layout` (`FragmentTreeBuilder`). Main repo, no patch.
- **The bug, and it is the other half of one already fixed here.** The
  [root-relative branch](#a-root-relative-frame-src-resolved-against-the-wrong-directory)
  strips the query and fragment before resolving, because only the path names a file and WPT
  leans on that (`?pipe=`, `?doctype=`). The **document-relative** branch did not: it joined
  the whole URL onto the containing directory, so `src="support/x.sub.html?doctype=…"` looked
  for a file literally called `x.sub.html?doctype=…`, `File.Exists` failed, and the frame
  painted empty with no error — indistinguishable from a missing file. The two branches
  disagreed about the same URL depending only on whether it began with a slash.
- **What landed.** One helper, `ResolveRelativeDocumentPath`, that both branches share: it
  takes the URL's path component, percent-decodes it, and normalises the separators. A URL
  with *only* a query or fragment addresses the containing document rather than a new one and
  still resolves to nothing.
- **How it was found.** Not from the test name. Bisecting
  `parse-a-sizes-attribute-standards-mode` down to a two-line page —
  `<iframe src="inner.html">` renders, `<iframe src="inner.html?x=1">` renders empty — took
  the whole `.sub`/`sizes`/harness story out of it.
- **Verified:** 10 focused cases in `RootRelativeFrameSrcTests` alongside the root-relative
  ones they mirror, including the negative half (a bare `?a=1` or `#frag` is not a document).

### An `<img>` loaded nothing at all when its source came from `srcset`

- **Tests:** `the-img-element/sizes/implicit-sizes-ignores-width` 33.7% → **99.9%, passing**;
  `the-img-element/current-pixel-density/basic` 43.9% → **97.9%**, with every image now
  *exactly* the size the test states (256, 256, 160, 128, 0, 0, 256, 128, 512, 1, 0 px, read
  off the render) — what is left is 4px of inter-image whitespace per gap, a font-metric
  difference between this container and the reference browser and nothing to do with the
  selection; and the four `sizes/parse-a-sizes-attribute-*` 39.0% → **82.7%** (they also
  needed the frame-`src` fix above, which is what let their `<iframe>` load at all — see
  [what is left](wpt-rendering-gaps-open.md#sizes-parses-and-the-last-of-its-two-hundred-spellings-do-not)).
- **Owner:** `Broiler.Layout` (`Engine/ResponsiveImageSourceSet.cs`, `CssBoxImage`,
  `CssLayoutEngine.MeasureImageSize`). Main repo, no patch.
- **The bug.** The engine read `src` and nothing else. `srcset` appeared in exactly one file
  in the tree — the preload scanner, which documents that it deliberately does *not* scan it —
  so a responsive `<img srcset="a.png 2x">`, and every `<picture>`, had no source to load and
  painted the missing-image border. `current-pixel-density/basic` renders fifteen images of
  which fourteen carry only a `srcset`; it was a wall of 20×20 error boxes.
- **The density is as much of the algorithm's output as the URL.** A `w` descriptor is not a
  size — it says how many pixels the candidate has to spend on a slot whose CSS width comes
  from `sizes` — so `<img srcset="x.png 100w" sizes="400px">` lays a 100-pixel bitmap out
  400px wide. HTML §4.8.4.3 calls that divisor the image's *current pixel density*, and
  dropping it would put a different image on the page, not a rounding difference.
- **What landed.** HTML's "parse a srcset attribute", "parse a sizes attribute" and "select an
  image source", including the `<picture>`/`<source>` walk with its `media` and `type` gates.
  `CssRectImage.PixelDensity` carries the chosen density to `MeasureImageSize`, which divides
  the decoded bitmap by it, and to the fragment builder, so `object-fit: none` draws at the
  same natural size layout sized the box from. The density is 1 for every image that did not
  come from a candidate list, which leaves all of this an identity for them.
- **`sizes` needed a CSS component-value scanner, and there was no reusing one.** The spec
  asks for the *last component value* of each comma-separated entry, which means telling a
  dimension from an identifier, keeping a function with its arguments, closing an unterminated
  block at the end of the input rather than discarding it (`sizes="calc(1px"` is the length
  `1px`; `sizes="((),1px"` is one block and no length at all) and dropping comments **without**
  leaving whitespace behind (`1/* */px` is a number and an identifier, not `1px`).
  `CssSyntax.SplitTopLevel` splits on nesting but does not honour escapes, and nothing else in
  the tree tokenises component values.
- **A media condition is not a media query.** A media *type* — `all`, `print`,
  `unknown-media-type` — makes a `sizes` entry a parse error rather than a query that happens
  to match, so `sizes="all 100vw, 1px"` is 1px. Handing the text to
  `CssStyleEngine.MatchesMediaQuery` unchecked gets every one of those backwards; the check
  that what follows the leading `not`s opens with `(` is exactly that distinction.
- **Known deviation.** `clamp()` is a `<source-size-value>` per spec, but
  `CssLengthParser` evaluates only `calc()`, `min()` and `max()`, so a `sizes` entry written
  with one is a parse error here. Teaching the length parser `clamp()` is a change to the CSS
  engine that would fix it for every property at once; it is pinned as a deviation in
  `ResponsiveImageSourceSetTests` so it moves when that lands.
- **Verified:** 86 cases in `ResponsiveImageSourceSetTests`, with the expectations taken from
  the two WPT tests rather than from a reading of the spec — including the one that reads like
  a typo either way (`srcset="a.png,b.png 2x"` is *one* candidate whose URL contains a comma,
  because only a trailing comma ends one).
- **A crash found by writing it.** `sizes="0"` — a source size whose text ends at the number —
  ran the scanner one character past the end of the string, and the exception aborted the
  layout of the subtree around the image: a page with one such attribute rendered as a blank
  area rather than as the same page with one image missing. Fixed, and the selection is now
  wrapped so that no malformed attribute can do that again.

### An inline replaced element was aligned by a font it does not draw

- **Tests:** the half of `the-img-element/current-pixel-density/basic` that the source
  selection above did not close — 70.2% → 97.9%, the images now standing on a shared
  baseline as the reference has them rather than sharing a top edge.
- **Owner:** `Broiler.Layout` (`CssLayoutEngine.ApplyVerticalAlignment`, `CssLineBox.SetBaseLine`).
  Main repo, no patch.
- **The bug.** CSS2.1 §10.8 gives two rules for where a box's baseline sits: an ordinary inline
  box sits on its own text's baseline, one font ascent below its top, while an **atomic** inline —
  an `inline-block` with no in-flow line boxes, and every inline **replaced** element — has its
  baseline at its bottom margin edge. Only the `inline-block` half was implemented. An `<img>`
  was therefore aligned by the ascent of a font it does not draw, a constant ~13px for the
  default strut, so every image on a line was placed the same distance below the line's top
  whatever its height — which reads as top-aligned. `SetBaseLine` then skipped image words
  outright, so even that placement never moved them.
- **The two halves already disagreed with each other.** `CreateLineBoxes` extends a line below
  a tall image by the strut's descent *precisely because* the image's bottom is the baseline —
  a comment there says so. The line's height assumed one rule and its contents were positioned
  by the other.
- **What landed.** `BaselineAscentOf` answers the §10.8 question once for both call sites, and
  an image box is moved by `SetBaseLine` the way an `inline-block` is (paint reads an `<img>`'s
  geometry off the box, and only its *source* rect off the word, so moving the word alone moved
  nothing on screen). Re-running the alignment is idempotent: an atomic box that has been moved
  reports the same baseline it was aligned to.
- **Measured cost, on the image-heavy subsets, A/B on the same checkout and references** —
  `html/rendering`, `html/rendering/replaced-elements`, `css/css-images`, `css/CSS2/normal-flow`,
  `css/CSS2/floats`, `css/CSS2/visudet`, ~2 000 tests: **8 improved and 8 regressed**, all small,
  and the pass count moved by one (1 055 → 1 054). **Every regression is a test that was already
  failing, for a reason this does not touch.** The two largest:
  `html/rendering/.../img-aspect-ratio` (98.1% → 96.9%) renders no images at all here — its
  `img { width: 100%; max-width: 100px; height: auto }` leaves four error boxes running the
  height of the viewport, and what moved was where those boxes sit; and
  `CSS2/normal-flow/inlines-014`/`-015` (98.5% → 97.2%) put a 1×1 image in a
  `font-size: 64px` table cell whose green box is already three line boxes tall against the
  reference's one. The improvements are the `css-images/object-view-box-*` family.

### `Node.moveBefore` was missing — and it was only half the test

- **Test:** `dom/nodes/moveBefore/preserve-render-blocking-style`. Ours white, Chromium
  100% green. **Now passing.**
- **Owner:** `Broiler.DOM` with HtmlBridge for the binding.
- **The canonical method.** `Broiler.DOM` **`994e196`**, "dom: implement
  Node.moveBefore" — the genuinely atomic version, upstream and pinned. The state it
  preserves follows from one spec constraint: both parents must share a shadow-including
  root, so a moved node's *connectedness cannot change*. That is why the document's id
  index is deliberately not torn down and rebuilt, and why an `<iframe>` must not reload.
  Observers still see the move (records are queued for both parents); only the
  disconnection is skipped. `DomBridge.MoveNodeBefore` delegates to `parent.MoveBefore`
  and keeps only what is genuinely the bridge's — marshalling the canonical `DomException`
  into a JavaScript `DOMException`, and invalidating the style scopes the reposition
  dirtied.
- **Why validity is stricter than `insertBefore`:** `moveBefore` rejects a node that is
  not already in the tree, and one from a different root. Both would be silently accepted
  by an insert; a caller relying on the atomic guarantee needs the exception instead of
  insert-shaped behaviour.
- **The test stayed at 0.0% anyway, and this is the part worth keeping.** It was recorded
  as fixed and was not. The real gap: **`<link>` had no IDL reflectors at all**, so the
  ordinary way to inject a stylesheet — `createElement("link")`, set `.rel` and `.href`,
  append — wrote *nothing*, and the element serialized as a bare `<link>` that never
  reached the cascade. `setAttribute("rel", …)` worked, which is how it stayed hidden.
  `<link>`/`<base>` now reflect `href` as a URL like `<a>`/`<area>`, and `<link>` reflects
  `rel`, `as`, `media`, `hreflang`, `integrity` and `referrerPolicy`. **0.0% → 100%**, in
  the main repo. The `?pipe=trickle(d1)` query and the `moveBefore` call in that test were
  both red herrings — a static `<link>` with the same query already rendered correctly.
- **Verified:** 16 DOM-level tests and 10 bridge-level tests cover moves within and across
  parents, the render-blocking `<style>` case, the observer records, and every pre-move
  validity rejection.

### `<base href>` was ignored for `<link rel=stylesheet>` in the render path

- **Test:** `html/semantics/document-metadata/the-link-element/stylesheet-with-base`.
  Ours 100% red; **now 100% green and passing.**
- **The trap the test sets.** It sets `<base href="resources/">` and links
  `stylesheet.css`, so only `resources/stylesheet.css` (green) may load — the sibling
  `stylesheet.css` next to the test sets red. Broiler resolved the href against the
  document URL and loaded the trap file, which is exactly what the `<base>` is there to
  prevent.
- **Root cause: a second site.** An earlier commit taught the DomBridge serialization
  transform to honour `<base>`, but the WPT runner's `InlineLinkedStylesheets` reads
  linked sheets off disk *before* those transforms run — resolving against the test's own
  directory. It inlined the trap as a `<style>`, so by the time `ApplyBaseHrefToStyleUrls`
  ran there was no `<link>` left to rebase.
- **What landed:** rather than a third implementation, `HtmlBaseHref`
  (`src/Broiler.HtmlBridge.Dom/HtmlBaseHref.cs`) is the one seam both sites resolve
  through — it finds the document base (from raw HTML or the DOM) and resolves a URL
  against it, **keeping the base's shape** so downstream mapping still works: absolute
  base → absolute URL, root-relative base → root-relative path (the `wptRoot` handler
  still matches), document-relative base → a document-relative path when no page URL is
  known, which is what a caller holding a directory needs.
  - `@import` was the same bug one layer down: `InlineStyleSheetImports` resolved a
    relative import against `_pageUrl`, never the base. It now folds the base in via
    `HtmlBaseHref.ResolveDocumentBaseUrl`.
- **Verified:** focused tests assert the trap file is never loaded (not merely outranked in
  the cascade), that a document with no `<base>` still picks the sibling, and that
  non-stylesheet `<link>`s are untouched.
- **Note for the next reader:** on a Unix host `Uri.TryCreate("/css/", UriKind.Absolute)`
  succeeds as the *file path* `file:///css/`. Base resolution must check for a scheme
  before treating a base as absolute or it silently drops the page's origin — the helper
  does, and a test pins it.

### A `<template>`'s styles leaked into the page

- **Test:** `shadow-dom/focus-navigation/delegatesFocus-highlight-sibling`, 0.0% →
  **97.8%**, and **now passing on CI**.
- **It was never about focus.** The old next action was "establish what Broiler is
  painting grey before touching focus delegation", and that was the right instinct: the
  answer had nothing to do with focus, delegation, or control chrome. A `<style>` inside a
  `<template>` was being collected into the **document** cascade. HTML §4.12.3 keeps a
  template's children in a separate fragment as *template contents* — inert until stamped
  out. The test keeps its component styles in a template, as components normally do, so
  `:host { background-color: #aaa }` and `:host(:focus) { background-color: #ccc }` leaked
  into the page, matched it, and painted 99% of the canvas `#ccc`.
- **How it was narrowed,** since the signature pointed the wrong way. Bisecting the three
  ways to get a style into a shadow root separates the causes cleanly: a plain host
  populated by `innerHTML` renders correctly (2.8% `#aaa`); the same rules delivered by
  `<template>` + `importNode` fill the viewport (99.7%); and a custom element populated by
  `innerHTML` renders nothing. Removing the `:host(:focus)` rule still filled the viewport,
  which ruled the focus rule out. The serialized DOM then settled it: the shadow root was
  **empty** while the page rendered grey.
- **What landed:** `DomParser.CascadeParseStyles` stops at a `<template>` box. Narrow by
  design — template *contents* already produce no boxes and correctly do not render; only
  the stylesheet walk descended into them.
- **This is not specific to one test.** Any component that keeps its styles in a template
  — the ordinary way to write one — was leaking them into the page.
- **Four further bugs were found chasing the residual, and three are general.**
  1. **The `customElements` shim never worked at all.** The DOM globals were unreachable
     by bare name (a bare identifier does not resolve through `window` the way it does in
     a browser, so `class extends HTMLElement` threw); the upgrade threw on any element
     with attributes (it read `element.attributes[i].name`, but the bridge's `attributes`
     reports a length without answering to numeric indexing); `connectedCallback` was never
     called; and `template.content` / `document.importNode` did not exist, so the
     `importNode(template.content, true)` idiom every one of these components uses yielded
     nothing. All four fixed. One deviation is deliberate and pinned by a test: the spec has
     the parser move a template's children into the content fragment, leaving the element
     childless, whereas Broiler's parser keeps them as children so a template round-trips
     through serialization — so `content` is a stable *snapshot copy*.
  2. **The min/max-content passes measured `display: none` text.** `GetMinMaxSumWords` and
     `GetMinimumWidth_LongestWord` walked every child collecting words with **no
     `display:none` guard**, while the shrink-to-fit *height* paths beside them had one. So
     UA-hidden elements that carry text (`<style>`, `<script>`, `<title>`) were measured,
     and their **source text set the width of any shrink-to-fit ancestor**. Every shadow
     host is such a box holding its component's `<style>`, which is why it surfaced here,
     but a plain `<div style="display:inline-block">` holding one `<li>` and a stylesheet
     measured 861px wide against 65px without it. A `display:none` box generates no boxes at
     all (CSS 2.1 §9.2.4), so both passes now skip it. **90.5% → 95.7%.**
  3. **A collapsible space between inline-block siblings counted as zero.** A space between
     siblings is normally carried as a flag on an adjacent *word*, but between two
     inline-blocks the neighbours live in other boxes, so the space is a text box of its own
     whose words collapsing clears — and the intrinsic pass measured nothing. The
     shrink-to-fit container then came out exactly one space too narrow and its last child
     wrapped: two 10px inline-blocks measured 20px and **stacked**, the second at `y=34`,
     where 24px would have put them side by side. `GetMinMaxSumWords` now counts a collapsed
     whitespace separator as one space advance; preserved whitespace (`pre`, `pre-wrap`,
     `pre-line`) keeps its words and takes the normal path. **95.7% → 97.8%**, and
     reproducible with no shadow DOM, template or custom element in sight:

     ```html
     <style>
       .row { display: inline-block; background-color: #aaa; }
       .row span { display: inline-block; background-color: #eee; }
     </style>
     <div class="row"><span>Item One</span> <span>Item Two</span> <span>Item Three</span></div>
     ```
- **The score going *down* was the useful part.** With the custom-element fixes the test
  read 90.5% against 98.2% for the template fix alone. Nothing regressed: the shadow
  content simply rendered for the first time, at the wrong size, where before it was
  absent.
- **Two wrong turns, recorded so they are not repeated.** First, the width cause was
  ascribed to the CSS *text length*, then "refuted" by a pure-comment stylesheet that left
  the host at 1008×19 — but that case had no `:host` rule, so the host was full-width and
  the comment fit on one line; the test did not discriminate, and the refutation was wrong.
  Holding `:host` fixed and varying only inert text settles it: a 600-character comment takes
  the box from 468px to 6014px. Second, `getBoundingClientRect` is the bridge's own
  measurement taken while scripts run, *before* the shadow style is projected, so it reported
  some cases as unchanged when the render had in fact improved. **Measure the render.**
- **Two more attempts were measured and reverted**, and both are recorded under
  [inline-block line height](wpt-rendering-gaps-open.md#an-inline-blocks-height-ignores-line-height).
- **The durable answer is still `customElements` in HtmlBridge proper.** Items in (1) live
  in the runner's browser-API shim, which exists only because the bridge implements no
  custom elements.

### A Web Animation on a pseudo-element was silently inert

- **Test:** `css/css-pseudo/backdrop-animate-002`, 0.8% → **99.74%**.
- **Owner:** HtmlBridge (`DomBridge/WebAnimations.cs`, `DomBridge.Serialization.cs`,
  `Dialogs.cs`). Main repo.
- **Two gaps, and the test needs both.** It animates `::backdrop` to a 10%-opacity green
  and got the UA modal scrim. **Its own reference writes the same declarations as CSS and
  already rendered correctly** — which is what said the gap was the API rather than the
  pseudo-element.
  1. **The property-indexed keyframe form was not parsed at all.**
     `ParseAnimationKeyframes` required a `JSArray`, so `{ opacity: [0, 1] }` — the other
     half of the Web Animations keyframe argument — resolved to zero keyframes and the
     whole animation was inert. Each property is now turned into its own keyframes, which
     is exactly how `ResolveKeyframeProperties` reads them: it brackets each property
     against only the keyframes that define it, so properties with different list lengths
     need no common offset grid.
  2. **`pseudoElement` was ignored.** A pseudo-element has no node, so the element-inline
     bake `animate()` performs has nowhere to land. Those values are kept aside per element
     and pseudo, and emitted at serialization as `#id::pseudo { … !important }` author
     rules.
- **The rule alone did not close it, and the reason is worth recording.** With the rule
  emitted and verifiably present in the serialized HTML, the backdrop went green but stayed
  opaque. The WPT path renders a modal backdrop as a *synthesized* `<div>`, and a
  `#id::backdrop` selector cannot match a `<div>` — the div is filled from the bridge's own
  `::backdrop` cascade instead. So the animated values are merged into that cascade too, in
  `BackdropDeclarationsFor`, which both the synthesized div and the native marker read.
- **Measured:** `css-pseudo` **236 → 237 of 358** with nothing lost. Checked wider, because
  keyframe parsing touches every `animate()` call: `css-view-transitions` 346/490,
  `css-masking` 222/439, `css-shadow` 157/207, `css-transforms/animation` 30/64 and
  `css-align/animation` 4/6 — all unchanged.
- **A method note that cost real time.** The first `css-view-transitions` diff showed
  `auto-name-from-id` falling 97.46% → 1.27%. Neither was this change: the reference set had
  been regenerated between the two runs, and **this directory's references are
  timing-sensitive enough to differ between generations**. **Compare runs only against
  references generated in the same pass.**

### An `overlay` entry transition that never finished

- **Test:** `css/css-position/overlay/overlay-transition-finished`, 1.8% → **100%**.
- **Owner:** HtmlBridge (`DomBridge/AnchorResolver/Dialogs.cs`). Main repo.
- **The CSSOM answer and the painted answer are taken at different instants, and
  conflating them made the test unwinnable.** It reads `getComputedStyle(el).overlay`
  synchronously after `showPopover()` and paints itself pink unless it sees `none` — the
  transition must be observed *running* at script time — then screenshots from
  `transitionend`, by which point the popover must be in the top layer covering a fixed red
  div. `PopoverHeldOutByOverlayTransitionIn` answered "held out" for both, because it
  returns true whenever an element merely *declares* a discrete `overlay` transition.
- **What landed.** `ComputeOverlayValue` keeps answering for t≈0; only the two paint sites
  move, through `PopoverHeldOutOfTopLayerForPaint`. The renderer has no clock, so "which
  instant" is read from what the page says — the same thing the runner already does for
  `takeScreenshotDelayed(N)`. A test that gates its screenshot on `transitionend` is making
  that statement without a number, and `ScreenshotWaitsForTransitionEnd` recognises the
  shape: the document is still `reftest-wait` *and* a `transitionend` listener is reachable
  from the element (itself, an ancestor, the document, or the window — it bubbles).
- **The `reftest-wait` half is what keeps it from being a one-way door.** Broiler dispatches
  no transition events, so a page waiting on one waits forever and the class survives to
  serialization. If transition events are implemented later, the natural shape — dispatch
  `transitionend`, the listener calls `takeScreenshot()`, the class goes — makes the
  predicate false while the transition is genuinely over, and the ordinary path elevates the
  popover. **The rule degrades into the real one rather than inverting.**
- **Nothing else in the directory matches the shape**, which is what says this is a rule and
  not a fit to one test: the three tests that must keep the popover held out screenshot
  immediately and register no such listener, and `overlay-transition-dialog` is
  `reftest-wait` but releases it from a `requestAnimationFrame`.
- **Measured: `css-position` 238 → 239 of 382 with nothing lost.**

### `close()` did not honour `transition: overlay allow-discrete`

- **Test:** `css/css-position/overlay/overlay-transition-dialog`, 0.4% → **99.89%**.
- An asymmetry between two code paths that model the same rule. `hidePopover()` had
  honoured it since the popover work — a popover hidden mid-transition stays in the top
  layer, so a static render still paints it and its `::backdrop`. `close()` did not, and tore
  a modal dialog down unconditionally.
- `close()` now applies the same rule, in the two halves the spec actually has: `overlay`
  keeps the top-layer flag (so the backdrop paints), and `display` keeps the `open` attribute
  (because the UA sheet's `dialog:not([open]) { display: none }` is what decides whether a
  box is generated at all). A dialog transitioning `overlay` alone stays in the top layer but
  generates no box — a third test pins that, so the two halves cannot be collapsed into one
  flag.
- **`css-position` 241 → 242 of 382.** Main repo.

### A stylesheet `<link>` dispatched no `load` event

- Part of `uievents/…/UIEvent.load.stylesheet`. Nothing dispatched a stylesheet link's
  `load` event at all. It does now — once per href, only for a link in the document, and
  `error` rather than `load` when the fetch fails, decided by the same CSP gate and fetch the
  cascade uses. That last part matters: the loader only accepts absolute URLs, and skipping
  the resolution against the page URL dispatched `error` for every relative href while the
  sheet applied fine.
- **The test renders `PASS` where it rendered `FAIL`** and is still
  [below the threshold](wpt-rendering-gaps-open.md#bold-and-italic-never-reach-the-face) on
  bold text.

### `transform: scale()` with percentages

- **Test:** `css/css-transforms/transform-scale-percent-001`, 0.5% → **99.99%**.
- Two bugs in one spec rule. css-transforms-2 makes a scale factor
  `<number> | <percentage>` where the percentage is *the ratio* — `scale(50%)` is
  `scale(0.5)`. The paint parser resolved every percentage against the element's box (right
  for `translate`, wrong for `scale`), so a 100px square came out 50× and filled the canvas;
  the bridge's geometry parser had no percentage branch for scale at all, so `50%` fell back
  to `0` and collapsed the box.
- **The 939-test `css-transforms` subset goes 376 → 377 passing.**

---

## View transitions

### The live page cannot stand in for the old root snapshot when the new one is hidden

- **Tests:** `css-view-transitions/{new,old}-content-has-scrollbars`
  ([#1670](https://github.com/Broiler-Platform/Broiler/issues/1670).14 and .15) 11.1% →
  **pass**, and `root-to-shared-animation-start` (.5) 1.5% → **pass**.
- **Owner:** `src/Broiler.HtmlBridge.Dom/DomBridge.ViewTransition.cs`. Main repo, no patch.
- **Root cause.** A root snapshot is normally left content-less, on the reasoning that the
  live page underneath renders the same thing pixel-exactly where a DOM clone is only close
  — cloning unconditionally was measured at +8/−7 and reverted, and
  [that entry](wpt-rendering-gaps-open.md#the-root-capture-is-not-rasterised) still stands.
  But the live page shows the **new** state, so it can only stand in while the *new*
  snapshot is what is meant to be on screen. An author who writes
  `::view-transition-new(root) { opacity: 0 }` has said it is not, and the old snapshot is
  then the only thing that can supply those pixels. Leaving it content-less painted a flat
  viewport-sized rectangle of the captured root background instead.
- **This is not the `opacity` case the gate deliberately excludes**, and the distinction is
  what keeps the −79 regression away. That rule asks whether *this* snapshot's own
  compositing needs real pixels — it does not, since compositing against the backdrop is
  what the live page already does. This asks whether the page underneath is still a
  truthful stand-in at all.
- **A page holding a nested browsing context is excluded.** What a frame displays during a
  transition is resolved through the live element (`TryGetFrameMarkupHeldByRootSnapshot`
  replays `FrameMarkupAtCapture` onto it), and a clone carries a second copy that has had
  none of that applied, painted over the top — it shows whatever markup the frame's
  `srcdoc` last round-tripped rather than the state at capture time.
  `SubDocumentViewTransitionTests` pins that, and reproducing a sub-document faithfully in
  a clone is the "close, not exact" problem that got the unconditional clone reverted.
- **Neither test has anything to do with scrollbars.** Broiler paints none and does not
  inset the viewport for one, and the two references are byte-identical to each other
  despite one page having scrollbars and the other `overflow: hidden`. **The scrollbar
  semantics they were written to verify are still untested here.**

### A group whose animation lasted no time was left at the start of it

- **Tests:** `3d-transform-outgoing`, `css-tags-shared-element`,
  `new-content-{container,element}-writing-modes`, `new-content-is-empty-div` → **pass**;
  `far-away-capture` 63.8% → 97.5%, `content-visibility-auto-shared-element` 86.8% → 92.2%.
- **Owner:** same file. Main repo.
- **Root cause.** `FrozenGroupProgress` decides where a `::view-transition-group` sits at
  screenshot time and read only `animation-timing-function`. The WPT idiom
  `::view-transition-group(x) { animation-duration: 0s }` — an animation already *finished*
  two rAFs later, which is when these tests screenshot — came out as progress 0, so the
  group stayed on the old geometry and every snapshot inside it was placed and scaled
  against the wrong rect. A zero duration now reads as progress 1 whatever the easing.
- **`animation-delay` is deliberately not folded in the same way**: a positive delay means
  the animation has not started, which is the progress-0 behaviour
  `root-to-shared-animation-start` depends on.
- **The order of the two fixes matters.** The root-snapshot change alone regresses
  `root-to-shared-animation-end` by 79 points, because the group it uncovers is one of the
  zero-duration ones. With the duration fix in first that test *gains* 8, to 93.0%.
- **Measured** over `css/css-view-transitions`: **157 → 165 of 307 passing, +8 / −0.**

### An explicit `view-transition-group` name matched a non-ancestor

- **Test:** `css/css-view-transitions/nested/compute-explicit-name-non-ancestor.tentative`,
  0.0% → **100%**.
- **It was recorded as an untrustworthy pass, and it was neither.** The old verdict was
  "passes only by rendering nothing — the reference is a blank white canvas". That stopped
  being true: Chromium's reference became **100% green** and Broiler rendered **100% red**.
  **Re-checking what a reference actually contains is cheap; carrying a stale verdict is
  not** — and once re-triaged as an ordinary failure it turned out to be a one-line rule.
- **`view-transition-group: <custom-ident>` resolves against the *ancestor* chain**, not
  against the whole document (css-view-transitions-2) — the test's own title is "Explicit
  view-transition-group name can only match ancestors". `ResolveGroupParentName` accepted
  any captured element with that name, so a group nested under its **sibling**. The colour
  follows from there: `::view-transition-group(test) { background: inherit }` then inherited
  the sibling's red instead of the green `::view-transition` root, and since every group in
  that family is `position: absolute; inset: 0`, the last one painted takes the whole canvas.
- **The family is six tests against one reference**, which is what makes the rule checkable
  rather than guessable: `-direct` (parent) and `-nested` (grandparent) must keep nesting,
  while `-non-ancestor` (sibling), `-non-existent`, `-self` and `-nested-vt-names` must not.
  All six render 100% green after the change. `root` keeps qualifying explicitly, since the
  document element is an ancestor of every other captured element.
- **`css-view-transitions` 344 → 345 of 490 with nothing lost and nothing else moved.** Four
  focused tests read the nesting off one colour — green when the group nested, blue when it
  stayed top-level — and cover both directions, so the fix cannot degenerate into "never
  nest".

### Three narrower corrections to the pseudo tree

All three landed; none closes a test on its own, and the remaining root-capture work is in
[not fixed](wpt-rendering-gaps-open.md#the-root-capture-is-not-rasterised).

1. **The root's captured name was hardcoded to `root`.** It is really whatever
   `view-transition-name` the document element carries; the UA sheet only supplies `root` as
   the default. `root-captured-as-different-tag` renames it to `another-root` and paints
   `::view-transition-group(root)` red *precisely to assert the `root` rules stop applying* —
   so the 100% red canvas was the test working as designed. `auto`/`match-element` on the
   document element resolve to `root` rather than a generated name.
2. **`::view-transition-image-pair` was never materialised.** The spec puts it between a
   group and its old/new pair so one rule can address both; `old-content-captures-root` hides
   an entire group through it, and with no such box the rule had nowhere to land.
3. **The pair box alone was not enough** — and this is the sort of thing only a real render
   catches. The snapshot content box bakes the captured element's computed style, so it
   re-asserted `visibility: visible` (the initial value nearly everything has) *over* the
   pair's inherited `hidden`. Only a non-initial `visibility` is carried now.

**Verified:** 25 `ViewTransition*` tests pass — three new ones covering the renamed root and
the image-pair hide *with its negative half* (the same group paints without the rule, so the
test cannot be satisfied by a blank group). Swept over all 458 local
`css-view-transitions` tests to confirm they cost nothing.

### Page selectors leaked into the pseudo tree

- **Test:** `css/css-view-transitions/names-are-tree-scoped`, 0% → 96.19%. Still failing on
  a separate cause — see
  [not fixed](wpt-rendering-gaps-open.md#a-captured-element-still-paints-in-place).
- The pseudo tree is materialised as real `<div>`s, so the test's page-level
  `div { background: red }` matched every box in it, including the viewport-sized overlay root
  that paints at z-index 2147483646. Each box now re-asserts a transparent background beneath
  its own base style and the author's `::view-transition*` declarations.
- **The interesting part is why the obvious version of this is wrong.** The reset is written
  as longhands and an author writes the `background` shorthand, so the two land on different
  keys of the inline-style dict and the longhands win by coming later — silently cancelling
  `::view-transition { background: lightpink }`. Layering them cost **341 → 264** passing
  across the 490-test subset, with individual tests falling from 100% to 1%; narrowing the
  reset to backgrounds alone changed nothing (263), which is what identified the shorthand
  collision rather than the extra properties. **The reset has to stand aside entirely when the
  author paints the box.**
- **Net: 341 → 341 passing**, one genuine gain
  (`shadow-part-with-name-overridden-by-important`, 1.3% → 100%) against one apparent loss
  that is `new-content-transform-change-001` — [a flaky
  test](wpt-rendering-gaps-open.md#one-test-is-flaky) which scores 1.03% on three consecutive
  runs of the *unmodified* build, identically to the patched one.

### The old capture was not in the snapshot containing block

- **Tests:** the `massive-element-*` family. `css-view-transitions` **346 → 349 of 490**,
  nothing lost.
- Both captures call the same `GetBoundingClientRectForDomElement`, but at different moments
  against different layouts, and only one has the scroll folded in: the new capture runs on
  the render projection, where the scroll is already baked into box positions, while the old
  one runs during script, where it is not. The page scroll is now subtracted from the old
  capture, which reproduces the new capture's −38 986 exactly.
- **A `position: fixed` element — or anything inside one — is excluded**, since it does not
  move with the page and its document coordinates are already viewport coordinates; without
  that exception `new-content-transform-position-fixed` falls from 100% to 98.73%, **which is
  how the exception was found rather than guessed**.
- The gains are `massive-element-on-top-of-viewport-partially-onscreen-old`/`-new`
  (96.70% → 99.58% — the *vertical*-scroll members of the family) and
  `transformed-element-scroll-transform` (98.73% → 100%). The horizontal-scroll members are
  [still failing](wpt-rendering-gaps-open.md#the-snapshot-clone-lays-its-children-out-horizontally).

### The root snapshot now clones when the page cannot show through

- **Tests:** `new-content-captures-root`, `old-content-captures-root`,
  `root-captured-as-different-tag`, all three of which had rendered as a flat pink page.
  **All three now pass on CI**, at 98.5% against their own reference.
- **What did *not* work, and why it is worth knowing.** The root capture used to carry only a
  background colour, no content, so `::view-transition-old(root)` was transparent and the
  author backdrop showed through the page. Reproducing the page by **cloning the DOM** into the
  snapshot box was implemented, measured, and reverted. It fixed three tests outright
  (0.0% → 100%), but across the 458 local `css-view-transitions` tests it was **+8 / −7** — and
  it cost 79 pixel points on `root-to-shared-animation-end` (82.7% → 3.1%). Restricting the
  clone to the *old* snapshot did not rescue those. **The reason is structural, not a missing
  detail: a DOM clone re-lays-out and is only *close*, while the transparent box let the live
  page show through — and the live page is pixel-exact. Anywhere the old root snapshot is
  genuinely visible, exact beats close.**
- **What did work: gate the clone on whether the page can show through at all.** The two cases
  are distinguishable without a rasteriser. The live page can only stand in for the snapshot
  while nothing paints between them; once the author gives the bare `::view-transition` a
  background, that backdrop hides the page and a content-less snapshot has nothing left to fall
  back on — which is exactly when the viewport comes out a flat wash of the backdrop colour. So
  the root snapshot clones **only when `::view-transition` paints a background**
  (`RootOverlayOccludesPage`). That is why this does not reintroduce the −7:
  `root-to-shared-animation-end` and `content-with-transform-old-/new-image` set no
  `::view-transition` background.
- **Two details the clone needs to be worth anything:** it must skip
  `<head>`/`<style>`/`<script>`/`<link>` (re-inserting them duplicates author rules into the
  document and re-fetches resources) and it must **keep `id` attributes**, which the
  per-element snapshot path strips — a whole page of id-styled content otherwise reproduces as
  unstyled boxes. Keeping them is safe because the pseudo tree is materialised on a fresh render
  projection, so the duplicate ids never reach the tree page script observes.
- **A trap for whoever does the raster version.** The overlay serializes after `</body>` and the
  HTML parser foster-parents it back *inside* `<body>`, so a rule anchored on an ancestor outside
  the snapshot — `body.updated #box` — repaints the **old** snapshot with the **new** state the
  update callback just produced. Any DOM-shaped snapshot has to freeze its paint at capture time.

---

## Conformance fixes that closed no test

Kept because they are right and covered, not because they moved a number.

### `@page { margin: auto }` left every margin at zero

- **Owner:** the WPT runner (`src/Broiler.Wpt/WptPageBox.cs`). Main repo.
- **The bug.** `auto` is a value of the margin property, and `TryParseMarginShorthand` read it as
  a failure to parse: any `auto` component rejected the *whole* shorthand, so
  `@page { margin: auto }` left all four margins at zero and the page area was never centred. The
  longhands did the same. Alongside it, a page that declared both `size` and `width`/`height`
  always had its box recomputed as area-plus-margins, which is right when the margins are stated
  and wrong when they are `auto` — that is the case where the box is the thing being centred *in*.
- **What landed:** `SettleAxis` resolves each axis once every declaration has been seen. With no
  `auto` on the axis the area plus its margins is the box, and a declared `size` gives way — the
  over-constrained resolution. With an `auto` the box stands and the `auto` sides take the
  remainder, halved between two of them or taken whole by one. The remainder is signed, so an area
  larger than its box hangs off the edges instead of clamping.
- **Three tests state the three cases, and their references spell out the answers:**
  `page-size-013-print` (over-constrained: `size: 500px; margin: 50px; width: 200px; height: 300px`
  against a reference of `size: 300px 400px; margin: 50px`), `page-margin-auto-print` (a 20em × 7em
  page centring a 12em × 3em area under 64/32px margins), and
  `page-margin-auto-negative-print.tentative` (a 300px page with a 340px area, hanging 20px off
  every edge).
- **It closes none of them, and the reason is worth knowing.** The unpaginated render consults the
  page box only when the `@page` *paints* — `WptTestRunner` takes the decorated path on a
  background, border or padding and otherwise renders straight onto the viewport — and not one of
  the `page-margin-*` tests paints anything. They are all gated on
  [rendering at the declared page box](wpt-rendering-gaps-open.md#a--print-document-renders-on-the-viewport-not-on-the-page-it-declares)
  instead, and `page-margin-auto-print` and `-auto-and-non-zero-print` additionally need six page
  boxes at once, which one surface cannot carry.
- **The paged path did catch a wrong first attempt,** which is the argument for keeping this
  covered. Resolving the over-constrained case as "declared margins stand, `size` wins" dropped
  `page-size-013-print` under `BROILER_WPT_PAGED_PRINT=1` (127 → 126 passing); that test's own
  reference is what settled the rule. With the corrected rule the paged run goes 72.97% → **73.24%**
  average at an unchanged 127 of 224, and the default unpaginated run is byte-identical
  test-for-test.
- **Verified:** eight focused tests in `WptPageBoxTests` pin centring, one `auto` taking the whole
  remainder, negative remainders, `auto` with no stated area, the over-constrained resize, `auto`
  expanding through the shorthand, and the single thing that separates `size`-stands from
  `size`-gives-way.

### A scroll past the end did not stop at the end

- CSSOM View §"scroll an element" normalizes the requested position to the scrolling box's
  scrolling area, so a scroll past either end comes to rest at the end. `scrollTo`/`scrollBy`
  — window and element alike — passed `clamp: false`, so `scrollBy({top: scrollHeight})`, the
  standard "scroll to the bottom" idiom (since `scrollHeight` is always at least the maximum
  offset), landed *beyond* the content and painted the bare canvas.
- Reduced to a probe: a page with a `lightblue` canvas, a `lightgreen` body and a 200vh block
  renders 96.36/3.61 unscrolled — Chromium's numbers exactly — and 100% canvas after that
  `scrollBy`. With the clamp it is 98.42/1.56.
- **Measured honestly: this closed no test at the time.** `css/cssom-view` was 193/234 and
  `css/css-view-transitions` 345/490 **both before and after**. `ScrollClampingTests`, five
  cases, four of which fail without it.
- **Its test has since closed anyway.**
  `css-view-transitions/reset-state-after-scrolled-view-transition` was carried as *part-fixed*,
  blocked on the root snapshot. Re-measured 2026-08-13: it renders 98.4% `lightgreen` /
  1.6% `lightblue`, **passes its own reference at 100%**, and is absent from the CI failure
  manifest.

### A runner note: scroll metrics ignore the configured viewport size

Hit twice while writing tests for the fixes above, so it is recorded rather than worked around
a third time. `new WptTestRunner(w, h)` renders at the given size, but the scroll metrics —
`vh` lengths and the maximum scroll offset — resolve against the default 1024×768 regardless.
A page built to be "taller than the viewport" at 200×200 therefore scrolls to somewhere that is
not the bottom of the canvas, and a test asserting on what is on screen fails for a reason that
has nothing to do with what it is testing. Both `ScrollClampingTests` and
`ViewTransitionOldCaptureScrollTests` pin their renders to the default size for this reason.
**Still open as a runner defect** — see
[not fixed](wpt-rendering-gaps-open.md#the-runner-resolves-scroll-metrics-against-the-wrong-viewport).
