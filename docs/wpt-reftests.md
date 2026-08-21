# WPT reftests — Broiler against WPT's own references

The [WPT Reftests](../.github/workflows/wpt-reftests.yml) workflow runs the half
of the Web Platform Tests corpus that carries its own answer, and decides each
test **without any other rendering engine**.

## What a reftest is, and why it needs no reference image

A WPT reftest states its correct rendering inside the checkout, as a link in the
test document:

```html
<link rel="match"    href="foo-ref.html">   <!-- must render identically to this -->
<link rel="mismatch" href="bar-notref.html"><!-- must NOT render identically to this -->
```

The reference is deliberately written to be simple — it produces the same picture
using only features the test does not exercise. So the pair can be checked by one
engine on its own: render the test, render the reference, compare the two
bitmaps. That is what this suite does, with Broiler on both sides.

The main suite ([WPT Tests](../.github/workflows/wpt-tests.yml)) works the other
way: every shard downloads Chromium through Playwright, screenshots each test,
and compares Broiler's render against that PNG. The two suites answer different
questions and their pass rates are **not comparable**.

|                         | `wpt-tests.yml` (golden images)             | `wpt-reftests.yml` (this suite)                    |
| ----------------------- | ------------------------------------------- | -------------------------------------------------- |
| Baseline                | Chromium screenshot of the test             | Broiler's render of the test's declared reference   |
| Tests run               | every discovered document                   | only those declaring a reference                    |
| Needs a browser         | yes (Playwright Chromium, cached)           | no                                                  |
| A failure can be        | a real bug, **or** a font/scrollbar/AA difference between engines, **or** a stale golden | a real disagreement with what WPT says the test should look like |
| Blind spot              | Chromium missing the feature under test     | a bug that hits the test and its reference alike    |

The trade is worth stating plainly: this suite cannot catch a defect that damages
the test and its reference identically, because both go through the same
renderer. In exchange nothing is ever attributed to Broiler that is really a
cross-engine rendering difference, and a run needs no browser download — which
also removes the browser-provisioning outage the golden-image suite has to defend
against ([issue #1534](https://github.com/Broiler-Platform/Broiler/issues/1534)).

## Running it

CI: **Actions → WPT Reftests → Run workflow.** Inputs mirror the golden-image
suite — `subset`, `shard_index`, `rerun_failed_only`, `test_timeout_seconds`,
`memory_limit_mb`, `pass_threshold`, and the two issue-size limits.

Locally:

```sh
# Every reftest in a checkout
./scripts/run-wpt-reftests.sh --wpt-dir tests/wpt/checkout

# One directory, with the failing renders saved for triage
./scripts/run-wpt-reftests.sh --wpt-dir tests/wpt/checkout \
    --subset "css/css-flexbox" --failure-images /tmp/reftest-failures
```

Or the runner directly:

```sh
dotnet run --project src/Broiler.Wpt -- \
    --wpt-dir tests/wpt/checkout --reftests-only [--subset <path>]
```

`--reftests-only` is the whole switch: it narrows discovery to tests with a
resolvable reference and decides each one by rendering both sides.
`--reference-dir` is ignored in this mode and nothing reads or writes the
reference-image tree.

## How a test is decided

Implemented in
[`WptTestRunner.RunReferenceTest`](../src/Broiler.Wpt/WptTestRunner.cs).

1. **Membership.** A test is in the suite when it declares at least one
   `rel="match"` / `rel="mismatch"` href that resolves to a file present on disk
   (root-relative hrefs map under the WPT root; query and fragment are stripped).
   Everything else — including a test whose href dangles — is dropped before the
   run rather than reported as skipped, so the totals describe only tests the
   suite could actually decide.
2. **Render.** The test is rendered by Broiler. A reference href naming a bitmap
   (`.png`, `.jpg`, …) is decoded as-is; any other reference is rendered by
   Broiler too.
3. **Compare** with the same comparer the golden-image suite uses. A
   `rel="match"` pair is judged at the run's pixel pass threshold (99% by
   default, i.e. at most 1% of pixels may differ); a `rel="mismatch"` pair is
   judged on inequality instead — see below.
4. **Verdict.** A `rel="match"` reference must be reproduced; with several of
   them the test passes on the first one it reproduces (WPT's own rule) and the
   closest candidate is what a failure reports. A test whose references are all
   `rel="mismatch"` passes only when it differs from every one of them.

### A `rel="mismatch"` is decided on inequality, not on the pass threshold

The two relations ask opposite questions, and only one of them wants a
tolerance. `rel="match"` asks *are these the same picture?* — a question a 99%
threshold answers usefully, because anti-aliasing and rounding should not fail a
pair that agrees. `rel="mismatch"` asks *are these **not** the same picture?*,
and running that through the same gate inverts it: any difference smaller than
1% of the viewport gets reported as "identical", so a test the engine rendered
**correctly** fails.

That is the normal size of what these tests assert, because a mismatch reference
is deliberately minimal — it differs from the test by one glyph, one rule, one
box. `css/css-text/white-space/control-chars-*` is the worked example: 63 tests,
each rendering a single 4em control-character glyph against a reference that has
none. That glyph moves **2 217** pixels of a 1024×768 page, against the **7 864**
that 1% of 786 432 absorbs — so 62 of the 63 were reported as matching the
reference they were asserting they differ from, and failed, with the glyph on
screen the whole time. Fixing the comparison — not the renderer, which was
already right — passed all 62.

Across the corpus it is not a niche correction: 559 of the reftests are
mismatch-only. Re-running the 3 331 failures
[issue #1716](https://github.com/Broiler-Platform/Broiler/issues/1716) recorded,
before and after the change, takes them from **11 passing to 253** — **242
recovered, 0 regressed**. The change can only move a test in that direction: a
mismatch test that passed before differed from its reference by *more* than the
threshold, so it still differs by more than zero. The largest groups are
`control-chars` (62), `html-ruby-extensions` (48, which declares up to ten
mismatch references per test), `css/css-writing-modes/forms` (27) and
`css/css-text/text-align` (16). The golden-image suite is untouched — this code
path only runs under `--reftests-only`.

Upstream wptrunner compares reftest screenshots for equality and applies a
tolerance only where the test opts in through `fuzzy` metadata, so inequality is
also what WPT means by the relation. Broiler still applies the per-channel
`ColorTolerance`, so "differs" means a visibly different pixel rather than a
rounding wobble; both sides come out of the same deterministic renderer, so two
renders of the same picture are byte-identical regardless.

**The lesson generalises past this one comparison.** A whole family of tests
failing together, at a suspiciously uniform match percentage, is as likely to be
the harness measuring the wrong thing as it is to be an engine gap — and the
family's *size* is what puts it high on a failure list, which then reads as
evidence that the gap is real. `control-chars` was the second-largest failure
family in [issue #1716](https://github.com/Broiler-Platform/Broiler/issues/1716)
(39 of its listed failures) and had never been a rendering gap at all. When a
family fails uniformly, render one member and its reference and *look at them*
before believing the score.

Manual tests, variant tests, and media-playback tests are skipped exactly as in
the golden-image path. Reference *chains* — a reference that itself declares a
reference — are not followed; the declared reference is rendered as written,
which is what the chain asserts it looks like anyway.

## CI shape

The workflow is a copy of `wpt-tests.yml` with the Chromium stage removed, so
everything downstream is shared rather than reimplemented:

- 8 deterministic shards, the same FNV-1a(relative-path) assignment.
- One composite action, [`run-wpt-reftest-shard`](../.github/actions/run-wpt-reftest-shard/action.yml),
  used by both the initial pass and the end-of-workflow retry pass, so a retried
  shard is measured exactly like the attempt it replaces.
- `scripts/merge-wpt-shards.py` merges the shards, detects shards that aborted
  abnormally, files the two failure issues, and folds failures into the manifest.
- Its own rerun manifest, `tests/wpt-baseline/failed-reftests.json` — a reftest
  failure and a golden-image failure are different measurements of the same test,
  so they must not overwrite each other.
- Issues from this suite are titled `WPT reftest run: …` so they are never
  confused with the golden-image suite's.

Two things the reftest shard deliberately does *not* inherit: the Playwright and
reference caches (there is nothing to cache), and `BROILER_WPT_DEFER_PROMISE_TESTS`
— that override exists to freeze the DOM at the point Chromium's reference
generator screenshots, and a reftest has no Chromium capture to line up with.

## Triaging a failure: the suite cannot tell you whether the test is winnable

The trade this suite makes has a second edge that only shows up in triage. A
golden-image failure is measured against an engine that *does* implement the
feature, so the reference tells you what the answer looks like. Here both sides
are Broiler, so a 0.0% match says the two renders disagree and nothing else —
not whether the disagreement is a bug, an unimplemented feature, a test that
needs a driver the runner does not have, or a test no engine passes.

**Use a browser as an oracle for the question the suite cannot answer**, and
only for that question: render the test *and its declared reference* in
Chromium and compare those two. That says whether the reftest is winnable at
all, without any Chromium pixel entering a Broiler verdict.

```sh
CHROME=/opt/pw-browsers/chromium-*/chrome-linux/chrome
WPT=tests/wpt/checkout
for side in css/CSS2/box-display/root-box-003.xht \
            css/CSS2/box-display/root-box-003-ref.xht; do
  "$CHROME" --headless --disable-gpu --no-sandbox --hide-scrollbars \
    --force-device-scale-factor=1 --window-size=1024,768 \
    --screenshot="/tmp/$(basename "$side").png" "file://$PWD/$WPT/$side"
done
```

Three things this reliably separates, from the triage of
[issue #1601](https://github.com/Broiler-Platform/Broiler/issues/1601) (top 30
problems, all at 0.0–1.4% match):

- **A real Broiler bug** — Chromium reproduces its own reference, Broiler does
  not. Four of the thirty were this, and three of them turned out to be
  something other than what the directory suggested:
  `css/css-page/monolithic-overflow-014-print` was not a paged-media gap at all
  but `display: flow-root` painting nothing (fixed; it fixed 32 reftests across
  `css-break`, `css-box/margin-trim`, `CSS2/floats` and `css-rhythm`, all of
  them tests whose *reference* used a `flow-root` wrapper);
  `quirks/tables-inherit-color-from-body-quirk-007` was **two** bugs stacked, and
  neither was the colour one its name points at — `document.append` missing from
  the bridge took it from 0.0% to 94.9%, and the doctype the serializer emitted
  unconditionally was destroying quirks mode for the whole corpus (see below);
  the quirk itself is implemented now, and `-001`…`-003` pass on it; and the two
  `css/css-image-animation/…-paused` tests
  were the one case that *was* what it looked like, an unimplemented
  `image-animation` (fixed, 0.0% → 100% each, and 15/22 → 20/22 for the
  directory).
- **A test that is stale upstream.** `css/CSS2/box-display/root-box-003.xht`
  asserts that `html { display: none; background: green }` still paints the
  canvas green. Chromium renders it white, and so does Broiler — because
  `css/css-backgrounds/background-color-root-propagation-001` asserts the
  opposite — its title is "don't propagate html background when display:none" —
  and is the modern rule. Making it pass means regressing that one, which
  Broiler passes. **A reftest failing is not by itself evidence of a defect**,
  and this suite will never tell you which of a contradictory pair to believe.
- **A feature nothing here implements**, where the render is honest and the
  match is 0% anyway: the three `jpegxl/` tests need a JPEG XL decoder (Chromium
  has none either), `forced-colors-mode-49` needs the run to be in
  forced-colors mode, and the two `*.sub.html` colour-scheme tests need the WPT
  server for their cross-origin substitution. The `fullscreen/` and
  customizable-`select` ones used to be read this way too — they are not: the
  runner drives them, and when they fail it is because that driving broke. See
  [the testdriver override](#testdriverjs-overwrote-the-shim-that-drives-it).

**Both of the first two answers came up again in
[issue #1714](https://github.com/Broiler-Platform/Broiler/issues/1714)**, whose
three biggest problems were all at 0.0%. Only the third, `backdrop-iframe`, was
a defect. `root-box-003` and `forced-colors-mode-49` were each re-checked in
Chromium on 2026-08-20 by the oracle recipe above, and Chromium splits test from
reference exactly as Broiler does — white against green for the first, green
against white for the second, on both engines. **When the oracle reproduces the
disagreement, stop: there is nothing here to fix, and the pair will head the
next biggest-problems list too.** That the list ranks by blast radius does not
make its top entry winnable, and two of three is a fair rate to expect.

**It held exactly to that prediction in
[issue #1716](https://github.com/Broiler-Platform/Broiler/issues/1716)**, where
`root-box-003` and `forced-colors-mode-49` head the list again and the third
entry is a new instance of the *unwinnable-by-construction* kind: `html/rendering/
replaced-elements/images/abs-pos-transferred-max-width-from-percentage-max-height-in-
auto-height-containing-block.html` declares `<link rel=match href="/css/support/60x60-green.png">`
— a bare 60×60 bitmap, compared against a 1024×768 full-page render that also
carries the test's `<p>` of instructions. No threshold can reconcile those, and
it is the corpus's **only** bitmap-reference reftest, so there is no category
here to fix either. Three for three now: **the biggest-problems list has not
produced a real defect in two consecutive runs**, which is the strongest
argument for reading it as a ranking of blast radius rather than a work queue.
Issue #1716's actual defect was found by ignoring that list and asking why a
63-test family was failing uniformly — see the mismatch-comparison section above.

- **The runner cannot represent what the test builds.** The rarest answer and
  the hardest to see, because the render is neither wrong nor honest — it is of
  a *different document*. Two distinct versions of this, and the fix for the
  first does not touch the second:
  - *The document was rebuilt on the way to the renderer.* The runner used to
    serialise the script-mutated DOM back to HTML and render a re-parse of that
    string, and HTML tree construction is not the inverse of a DOM — it
    unconditionally creates a `<body>`. **Fixed:** the runner renders the
    document itself (`BROILER_WPT_DOM_RENDER`, default on; set it to `0` for the
    round trip). That is what decided
    `quirks/tables-inherit-color-from-body-quirk-004` and `-005`.
  - *The document was built at a moment the runner does not have.* Scripts here
    run after the whole document is parsed, so a test whose script runs
    **during** parsing sees a tree the runner cannot reproduce.
    `…-quirk-006` and `-007` say so in a comment — "at this point, the `<body>`
    has not been created yet" — and they still fail, because by the time this
    runner executes anything, it has. **Open, and deliberately**: see
    [what parse-time script execution would cost](#what-parser-blocking-scripts-would-cost).

  Suspect both when a test's script removes or replaces `<html>`/`<body>`, and
  pin the behaviour in a unit test over the box tree rather than waiting for the
  reftest to be able to express it.

The largest single group in that issue — 13 of the 30 — are `*-print.html`
tests, which state their expected rendering in terms of page boxes, named pages,
margin boxes and fragmentation. The runner renders them in screen mode on both
sides, so they are not measuring what they were written to measure; they need
paged media, not thirteen separate fixes. There is now a paged render behind a
lever — see [paged print rendering](#paged-print-rendering-and-why-it-is-off)
for what it does and what it costs.

## The bug is as likely to be in the reference, and that inverts the scoreboard

The suite renders both sides, so a defect in the *reference* file is worth
exactly as much as one in the test — and it is much harder to see, because the
reference is the thing you are reading to decide what the test should look like.
It also does something the test-side kind never does: **fixing the test can make
the pass rate go down**, because a test whose two sides were wrong in the same
direction was passing, and correcting one side alone separates them.

That is not hypothetical. Implementing `line-clamp` (CSS Overflow 4 §5 —
`max-lines`, `block-ellipsis` and the legacy `-webkit-line-clamp`, none of which
had any implementation behind them) took `css/css-overflow/line-clamp` from
**107/263 to 174/263**, and nine tests moved the other way. Four of the nine were
`line-clamp-with-abspos-0{10,14}` and `line-clamp-with-fixed-pos-0{10,14}`, and
none of them had anything to do with abspos, or with the clamp. Their
*references* state the clamped result as `white-space: pre` text ending in a
newline, and Broiler laid out a trailing preserved newline as an extra empty line
box — so each reference rendered a line too tall, and the tests could only pass
while the clamp was equally wrong. Fixing that (CSS Text 3 §4.1: a forced break
at the end of a block generates no line box; the engine already applied it to a
trailing `<br>` and not to the `pre` spelling of the same rule) fixed all four,
took the directory to **181/263**, and moved another 14 tests elsewhere in the
corpus that had nothing to do with clamping.

**The tell is that the diff is in a part of the picture the test is not about.**
When the rendered and reference images differ somewhere the test's own feature
cannot reach — a margin, a trailing line, the height of a wrapper — read the
reference as a document in its own right and reduce *it* to a minimal case
against Chromium. The four above all showed the clamp working perfectly and the
whole box a line too tall.

## Issue #1604's top thirty: what a "biggest problem" list is made of

Re-run after the work above, all thirty still fail, and the shape is worth
recording because it is the shape these lists keep having — the ranking is by
*blast radius*, and a 0.0% match is the signature of a missing feature rather
than of a bug close to being fixed. In order of size: **13** `*-print` tests
needing paged media; **5** `css-view-transitions`; **4** `jpegxl/` needing a
JPEG XL decoder (Chromium has none either); **3** `fullscreen/` and **1**
customizable-`select` needing `testdriver.js`; **3** `*.sub.html` colour-scheme
tests needing the WPT server for their cross-origin substitution; **1**
`forced-colors-mode-49` needing the run to be in forced-colors mode; **1**
`root-box-003` that is stale upstream (see above); **1** `grid-lanes` test; and
**1** whose `rel=match` is a bare 60×60 PNG compared against a full-page render
that also carries a `<p>` of instructions, which cannot match at any threshold.

Two of those deserve a note because they look actionable and are not:

- **`css/css-grid/grid-lanes` is the corpus's single largest failure group —
  478 reftests, 12.8% of every failure — and it is deliberate.** The engine drops
  `display: grid-lanes` as an invalid value to match reference browsers, and
  Chromium fails `column-align-items-001` against its own reference by 83% of
  pixels, so the tests are unwinnable without shipping the experimental feature.
  Doing so would trade **+478 reftests against the golden-image suite**, where
  the drop is currently what makes ~1 400 of those tests pass (191 fail there
  today). The trade is a large net loss and the reason the current behaviour is
  what it is; see `CssUtils.NormalizeDisplayValue`.
- **The `*-print` group needs paged media, not thirteen fixes** — the runner
  renders both sides in screen mode, so those tests are not measuring what they
  were written to measure. This is the same finding as issue #1601's triage.
  A paged render now exists behind `BROILER_WPT_PAGED_PRINT=1`; the section
  below has the numbers and the reason it is not the default yet.

The list to work from is therefore not this one. The directory totals are:
`css/css-grid/grid-lanes` 478 (above), `css/css-writing-modes` ~724 at 55–95%
match (partial vertical layout — deep, and the largest winnable group),
`css/css-flexbox` ~206, `css/css-overflow/line-clamp` (was 156, now 82).

## Paged print rendering, and why it is off

`BROILER_WPT_PAGED_PRINT=1`, default **off**. With it on, a reftest whose file
stem ends in `-print` is rendered as CSS Paged Media says: the document's own
`@page` `size` and `margin` give the page box, the flow is laid out once on a
surface several page areas tall, and each page is the band of that surface from
`k·H` to `(k+1)·H`, blitted into the output at its page's margin origin. Both
sides of the comparison switch together — WPT's print references are *not*
themselves named `-print` (`block-page-break-inside-avoid-1-print.html` matches
`block-page-break-inside-avoid-print-ref.html`), so the mode is decided from the
test and carried into the reference render.

**It is off because it is not yet a better answer than not paginating at all.**
Over the 409 print reftests: **252 pass unpaginated, 212 paged.** That is not the
paging being wrong so much as it being partial. Where the flow is not paginated,
a test and its reference are wrong in the *same* way and agree — this is the
suite's blind spot working in the corpus's favour — and each unimplemented piece
of paged media separates the pairs that rest on it. What is missing, in order of
size in the current failures: fragmentation of flex and table content (34),
per-name `@page` sizes, and monolithic content in grid.

**The lever is still worth having, because without it every paged-media fix
scores exactly zero.** It is what turned "the print tests need paged media" into
a number that moves: implementing named pages (CSS Paged Media 3 §3.4 — the
`page` property, its start/end propagation, and the break a name change forces)
took the paged run **173 → 213**. Of the 118 print tests whose paged render came
out with the wrong *page count*, 31 were `page-name` tests.

**Page count is the failure mode to look at first.** A paged render that gets the
count wrong reports 0.0% — the images are different sizes — so the ranked failure
list fills with 0.0% entries that are all the same defect. Read the dimensions out
of the message (`actual 480×576 vs baseline 480×864` is two pages against three)
before reading anything into the percentage.

### `@page` margin boxes, and what actually gates them

The sixteen margin boxes of CSS Paged Media 3 §5 are implemented and reachable
through the same lever. A box's slot is computed here — the ring of four corners
and four edge strips, an edge's length shared out by §5.3.2 — and the box itself
is emitted as ordinary markup laid over the page, because that is all it is: a box
with a background, a border, a font and an alignment. Sharing an edge takes a
measure render first (each box on its own, read back by the colour it is painted),
since §5.3.2 divides the unused length *in proportion to max-content sizes* and
counts a sized box's border and padding besides.

**The cluster does not move, and the reason is not margin boxes.** It went 15/37
to 13/37: one genuine new pass (`content-001`, at 100%) against three passes that
were never real — `dimensions-003` and `-005` were passing at 99.5% and 99.2%
*with none of their margin boxes drawn*, because the boxes are under 1% of a
1024×768 page, and `dimensions-010`'s reference renders blank. What blocks the
rest is in the **references**, and it is two engine gaps that have nothing to do
with paged media:

- **Flex items were never stretched on the cross axis** — fixed, in both halves:
  the stretch itself (`css/css-flexbox` 470 → 568 of 994), and the re-layout a
  flex container needs once the grid track it sits in has sized it. These
  references render now, where 26 of the 36 were blank or nearly so. It is still
  not enough to *pass* them — see below — but it is what makes the difference
  readable at all.
- **Generated `content` takes a single quoted string and nothing else.**
  `content: "a" "b"` renders as `a" "b` and `content: counter(page)` renders as
  the text `counter(page)`. Both sides of a comparison share the gap, so it costs
  nothing today — and it is why the page counters are deliberately *not* evaluated
  on the test side: doing so alone would separate the eight margin-box tests that
  use one from references that still render the literal text.

### `@page` paint is not behind the lever, because it is not about pagination

CSS Paged Media 3 §7 lets the sheet itself carry paint — a background, a border
and a padding on the `@page` rule — and that is a different question from whether
the flow is cut into pages. So it is **not** behind `BROILER_WPT_PAGED_PRINT`: a
`-print` reftest gets its page's paint whether or not it is paginated, decided by
the same `-print` stem and carried into the reference render the same way (see
`WptTestRunner.PrintMedia`, which the paged lever now sits on top of rather than
beside).

It has to be on for the unpaginated run, because the pairs that state it are
written to be read on screen. `css-page/page-background-image-print` says so
outright — *"Should print on a green background but not display it on screen"* —
and its reference states the same colour on `html`, so the two agree only when
the page paints. Before this, `page-box-001-print`, `-002`, `-003` and `-011` each
matched their reference by **0.0 %**: the reference paints the colour through a
`body` background and the test through its `@page`, and only one of the two was
drawn. `css/css-page` goes **133 → 137** — `page-background-image-print`,
`page-box-000`, `-001`, `-003` and `-011`, nothing lost — and **138** once the
`Broiler.HTML` patch below is applied.

> Measure with an **absolute** `--wpt-dir`, the way `scripts/run-wpt-reftests.sh`
> and CI do (`WPT_DIR="$REPO_ROOT/tests/wpt/checkout"`). A relative one does not
> resolve a test's root-relative resources — `url(/images/green.png)` and
> `<img src="/images/…">` silently render nothing — so a run started by hand with
> `--wpt-dir tests/wpt/checkout` reports failures CI does not have, and a
> before/after comparison across the two spellings is not a comparison at all.

Three details are the whole of it, and each is a WPT pair stating it:

- **The background covers the whole page box, margins included; the border and
  padding sit on the box the margins leave.** `page-background-004-print` states
  a 500×300 page with a 50px margin and matches a reference that is solid yellow
  corner to corner, while `page-box-008-print` paints its margin ring in the page
  background and its padding ring in the propagated `body` one. So the backdrop
  is two boxes — a full-sheet one carrying the background declarations, an inset
  one carrying the border and padding — and both are ordinary divs handed to the
  same renderer that will draw the page, which is what makes a page background
  that is a gradient or an image work without the runner knowing they exist. The
  distance from that box to the page area is measured off a render of it for the
  same reason (`WptPageDecoration.MeasureInsets`), rather than by parsing
  `border-width` shorthands, logical spellings and percentages a second time.
- **A page that generates no content paints nothing at all.**
  `root-element-display-none-print` states a hotpink page with a red border and
  matches an *empty document*: a root element with no box generates no page, so
  the sheet's own paint goes with it.
- **`visibility` applies in the page context** (§5.1) and to the background and
  the border alike, keeping the space they take.
  `page-visibility-hidden-001-print` hides a red page border and matches a
  reference whose border is `solid transparent`.

**One pair needs a `Broiler.HTML` change and carries a patch**:
`page-box-002-print` puts a half-transparent `body` background over a blue page
and must come out violet. CSS 2.1 §14.2 propagates the `body` background to the
canvas, and the paint walker flattens a translucent one into a single opaque fill
against a backdrop it assumes is white — right for a render onto a blank surface,
wrong for one onto a page background. The colour to composite against now comes
from `Broiler.Layout.Engine.CanvasBackdrop` (main repo, thread-static, null by
default so every other render is byte-identical), which the runner sets when the
page background under the whole page area is one flat colour and leaves alone
when it is an image or a gradient. The one-line call site is upstream and pinned
(`Broiler.HTML` `1bf117a`), so `page-box-002-print` **passes at 100 %**; before it
landed that test stayed at 0.0 % and everything else here worked without it.

**What is still missing is named pages.** `WptPageDecoration` reads the
unconditional `@page` only, as `WptPageBox` does, so a page a name selects paints
the wrong rule's background — `page-name-table-001-print` puts its table on a
`@page square` whose `#eee` overrides the unconditional red, and fails either way.
Fixing it needs the `page` property in the fragment tree's computed style, which
is what the paged run needs for per-name page sizes too.

## Scripts in an XHTML test never ran, and it cost 45 reftests

Half of `css/CSS2` is `.xht`, and an XHTML test writes its inline scripts inside
an XML CDATA section:

```xml
<script type="text/javascript"><![CDATA[
  function startTest() { … }
]]></script>
```

An XML parser consumes the wrapper and hands the engine what is between the
markers. This runner does not parse the document to find its scripts — it scans
the source — so it handed the engine the markers too, `<![CDATA[` is a syntax
error, and **the whole script was lost with it**. Not just the statements: the
functions an `onload` attribute goes on to call went with them, so a test whose
result depends on `body onload="startTest()"` rendered its unscripted state and
was compared as if that were the answer.

Stripping the wrapper (`WptTestRunner.StripCdataSection`, with the commented
`//<![CDATA[` and `<!-- … -->` spellings) took **`css/CSS2` from 4863 to 4908 of
6216**, nothing lost. The gains are where scripted CSS2 tests live:
`box-display` +30 (its `insert-block-in-*` / `delete-inline-in-*` DOM-mutation
family), `tables` +14, `positioning` +1. 395 documents in the checkout carry a
CDATA-wrapped script, so the reach is wider than the tests it happens to move
today.

**The tell for this class of bug is a test that renders its "before" state.**
Nothing errors, nothing is skipped, and the rendered image is a plausible render
— of the document as it stood before a script it never ran.

## testdriver.js overwrote the shim that drives it

The same tell, from the other direction, and it is the reason
`fullscreen/rendering/backdrop-iframe` headed
[issue #1714](https://github.com/Broiler-Platform/Broiler/issues/1714)'s biggest
problems at 0.0%. The render was not blank. It showed the test's un-fullscreened
state **plus a grey box reading "This test requires user interaction. Please
click here to allow fullscreen."** — which is upstream `testdriver.js` announcing
that it is waiting for a WebDriver click nobody is going to send.

A test that needs a user gesture asks for one through testdriver:

```js
test_driver.bless('fullscreen', () => {
  document.querySelector('iframe').requestFullscreen();
});
```

The runner has always shimmed `bless` — there is no user here and nothing checks
activation, so it just runs the action. What it could not survive was **being
injected first**. `BrowserApiStubs` goes in at position 0, ahead of the page's
scripts; `/resources/testdriver.js` is inlined from the checkout like any other
external script and assigns `window.test_driver` wholesale. The shim's
`if (typeof test_driver.bless === 'undefined')` guard ran while the name was
still free, installed, and was overwritten seconds later. Upstream's `bless`
then appends its button and awaits `test_driver.click`, which routes through
`test_driver_internal` — and WPT's in-tree `testdriver-vendor.js`, the file a
vendor is expected to substitute its own implementations into, **is empty**. The
promise never settles, the action never runs, `reftest-wait` is never dropped.

The fix is to stop guarding and start winning: `TestDriverStubs` is its own
constant now, it assigns unconditionally, and the runner appends it to every
`testdriver*.js` it inlines as well as to `BrowserApiStubs`. Whatever the page
loads, and in whichever order, the runner's `bless` and `Actions` are what the
page's own scripts see.

- **The reftest suite: 880 → 910 of 1258, nothing regressed.** A paired A/B over
  every directory holding a reftest that loads testdriver.js, in a checkout
  sparse to `css/`, `html/semantics/`, `fullscreen/` and `forced-colors-mode/` —
  so the figure is a floor, not the corpus-wide total. `fullscreen/rendering`
  went 3/6 → 6/6, and the `::backdrop` trio landed back on exactly the figures
  [wont-fix](wpt-rendering-gaps-wont-fix.md#fullscreen-backdrop--the-reference-never-entered-fullscreen)
  recorded for it on 2026-08-13 — 99.1%, 100%, 100% — which is what says this
  was a regression rather than a gap. The other 27 are
  customizable-`select`, `interestfor` and `css/css-shadow/part`.
- **The golden-image suite: +5, −3**, over five of the affected directories (823
  tests). Both directions are the same trade, because the reference generator
  drives nothing either: five tests were nearer Chromium's screenshot once
  Broiler stopped painting a differently-sized interaction button, and three are
  now *further* from it because Broiler runs the test and Chromium does not.
  `css/css-pseudo/pseudo-element-removal` is the clean example — its
  `test_driver.Actions().send()` used to reject, abandoning the promise_test
  before it removed the pseudo-element, and both engines rendered the leftover.
  That is the cost [wont-fix](wpt-rendering-gaps-wont-fix.md) exists to record,
  and it is why this suite is the one that can judge the change.

**A capability guard is only right when nothing can arrive later and claim the
name.** The `typeof … === 'undefined'` idiom is correct for the rest of
`BrowserApiStubs`, which fills in APIs the bridge lacks and would be wrong to
override. It is exactly backwards for a name the *page* is about to define, and
the failure is silent: the shim installs, reports nothing, and is gone by the
time anything calls it.

## `clip` is implemented, and most of what it clips is nothing

CSS 2.1 §11.1.2's `clip` had no implementation at all: `ComputedStyle` had no such
property and the paint walker never looked for one, so `css/CSS2/visufx/clip-*`
rendered 44 unclipped elements over references that show none of them.

It is implemented as a projection rather than a second clip.
`clip: rect(<top>, <right>, <bottom>, <left>)` and `clip-path: inset()` name the
same operation — a rectangle the element and everything inside it is clipped to,
measured on the border box — so `Broiler.Layout/…/IR/ClipRect.cs` resolves the one
into the other in `ComputedStyleBuilder`, where the used border box is already
known. Everything downstream sees an ordinary `clip-path`. CSS Masking 1 §7 makes
that ordering right as well as convenient: a real `clip-path` supersedes `clip`,
so `clip` is only consulted when `clip-path` is `none`.

**The geometry is stated from two edges, not four**, and that is the whole reason
this family looked unwinnable. `top` and `bottom` are offsets down from the border
box's top edge, `right` and `left` offsets right from its left edge — so
`rect(96px, 96px, 96px, 96px)` on a 96×96 box is an *empty* clip, and about forty
of the tests state exactly that, one per unit spelling (`1in`, `72pt`, `6pc`,
`2.54cm`, `-0px`, `+0px`, …). Each means "nothing should be visible".

The paint walker dropped an empty `inset()` as if it were no clip, which painted
the element in full — four lines in `Broiler.HTML` emit it instead (upstream and
pinned, `be76c7f`). With both halves: **`css/CSS2/visufx` 6 → 50 of 51**, plus two
in `css-masking/clip` that were the same bug reached through `clip-path` directly,
and nothing lost. Before the submodule half landed, the main-repo half still
handled the non-empty cases; it was the empty ones that waited on it.

Every `clip-*` test in the directory passes then; the one failure left in it is
`visibility-005`, which is about `visibility` and not about clipping at all.

## A percentage width made a replaced element ignore its height

`css/CSS2/backgrounds` had 135 failures at a median 97.7 % match — the signature
of something small and systematic rather than a missing feature. It was in the
**references**, and in one line of the layout engine.

CSS2's background references draw their coloured band the same way:

```html
<div><img src="support/1x1-green.png" width="100%" height="50" alt="…" /></div>
```

CSS 2.1 §10.4 uses a replaced element's intrinsic ratio to fill in a dimension
left `auto`; it never overrules one the author stated. `MeasureImageSize` agreed
for a length width — `width="200" height="20"` came out 200×20 — but its
percentage-width branch set the "now derive the height from the ratio" flag
unconditionally. So a 1×1 green pixel at `width="100%" height="50"` came out as
tall as it was wide: a full-page green block where the reference wanted a 50px
band. The max-width clamp a few lines below had it right (`!hasImageTagHeight`);
this one said `true`.

The same flag also stood in for "the width is stated" in the aspect-ratio pass,
where a percentage width had to stop counting as `auto` — otherwise fixing the
height merely moved the bug to the width, deriving 50px back out of it.

**`css/CSS2/backgrounds` goes 204 → 247 of 339**, and the fix reaches further than
the directory that surfaced it: over a 16 059-test sweep of every directory that
sizes a replaced element, **+89 reftests and none lost** — `backgrounds` +43,
`CSS2/normal-flow` +22, `CSS2/borders` +13, `CSS2/positioning` +10. The tests were
rendering correctly the whole time.

**This is the reference-side failure mode the section above warns about, at
scale.** The diff was in the part of the picture the tests were not about — 43
`background-N` and 42 `background-position-N` tests, none of which have anything
to do with replaced-element sizing, all failing because the document they were
being compared against was drawn with an `<img>`.

## `position: relative` did nothing to an inline box, and it cost 68 writing-mode reftests

`css/css-writing-modes` is the corpus's largest winnable group, and its largest
family — `abs-pos-non-replaced-v{lr,rl}-*`, 224 tests at a median 97.4 % — turned
out not to be about vertical layout at all. Their **references** are ordinary
horizontal documents that place a swatch like this:

```html
<style>img#green-square { position: relative; left: 160px; top: 80px; }</style>
<div><img id="green-square" src="swatch-green.png" width="80" height="80"></div>
```

The swatch rendered at its static position, so the reference showed the green
square in the wrong place while the *test* had it right.

CSS 2.1 §9.4.3's offset is visual — the box keeps its place in the flow and its
content is painted somewhere else — so it has to reach the words, which is what
`OffsetLeft`/`OffsetTop` already do. `PerformLayout` applies it for every box it
lays out; an **inline-level** box is laid out by `CreateLineBoxes` and never goes
through `PerformLayout`, so nothing applied it at all. Neither an inline `<img>`
nor an inline `<span>` moved. The fix walks the inline descendants once the line
boxes exist (`CssBox.OffsetRelativeInlineDescendants`).

**The walk is `display: inline` only, and that distinction is the whole of it.**
An `inline-block` is inline-*level* but is laid out as a block and already applies
its own offset in `PerformLayout`, so including it moved such a box by double —
`CSS2/margin-padding-clear/margin-collapse-001`'s reference stacks two relatively
positioned inline-blocks and was the pair that caught it. Such a box still moves
with a relative *ancestor*, because `OffsetLeft` carries the ancestor's shift into
every descendant whatever its display.

**`css/css-writing-modes` goes 419 → 487 of 1139**, and over a 16 059-test sweep
the change is **+73 with none lost** — `CSS2/box-display` +3, `CSS2/positioning`
+1 and `CSS2/visuren` +1 besides, all of them references that place something with
`position: relative` on an inline box.

**Vertical containers are deliberately left out.** `left`/`top` are physical, but
a vertical container's words sit in the engine's rotated space, so the offset
arrives turned a quarter turn: measured on `vertical-rl`, `left: 60px; top: 30px`
came out as a visual `(-30, +60)`, and on `vertical-lr` as `(+30, +60)`. Applying
it there needs a per-writing-mode mapping — the two `sideways-*` modes included,
which has not been measured — and doing it wrongly is worse than not doing it:
`vrl-inline-paint-invalidation` was the single regression when the offset went in
unmapped, and it is the pair to fix against when someone works out the mapping.

That is also the reason the family only moved 60 of its 224 tests: the rest fail
on their *test* side, which is what these were written to exercise.

## An out-of-flow replaced element renders now — that lead is closed

This section used to be the standing "next lead": an `<img>` painted in flow and
painted out of flow **only when both dimensions were stated**, which cost
`css/CSS2/positioning/absolute-replaced-width-*` 40 failures at 96.5–98.8 %
match. All three of the defects it named are fixed, in two separate pieces of
work, and re-rendering its own probe on 2026-08-20 paints every image:

```html
<div style="position:relative; width:200px; height:60px">
  <img src="60x60-green.png" style="position:absolute">     <!-- 3 600 px -->
  <img src="60x60-green.png" style="float:left">            <!-- 3 600 px -->
  <img src="60x60-green.png" style="position:absolute; width:40px; height:40px">
                                                            <!-- 1 600 px, and the
                                                                 only one that used
                                                                 to paint at all -->
  text <img src="60x60-green.png" style="float:left"> text  <!-- 3 600 px -->
</div>                                                      <!-- 12 400 px of green, exactly -->
```

- The **absolutely positioned** halves — auto size, and `display: block` — were
  `ResolveBlockUsedWidth` running the §10.3.7 *non-replaced* branches over a
  replaced box, which measured its (nonexistent) children and produced zero. See
  [the entry in the gaps document](wpt-rendering-gaps-fixed.md#an-absolutely-positioned-img-rendered-nothing-at-all).
- The **floated** half was CSS2.1 §9.7's other blockification: a floated replaced
  box stayed inline-level and took `PerformLayoutImp`'s else-branch, which sizes a
  box from its words and a replaced box has none. `CssBox.IsBlockifiedFloatedReplaced`
  is the fix and `FloatedReplacedBlockificationTests` pins it, including the
  deliberate narrowing to *replaced* floats.

Left here rather than deleted because a stale "next lead" is worse than none, and
because the shape of the diagnosis — dump the fragment tree, separate one
symptom into three defects, sweep each on its own — is the part worth reusing.
The current lead is [a flex item that has a ratio and no
width](#next-lead-a-flex-items-main-size-ignores-its-aspect-ratio).

## A definite height and an `aspect-ratio` did not make a square

`aspect-ratio` was implemented in one direction only. `TryResolveAspectRatioBlockHeight`
takes an auto *height* from a width that has already filled the containing block,
which is what an ordinary in-flow box needs; the inverse — an auto *width* taken
from a definite height — existed as `TryResolveAspectRatioInlineWidth` and had
exactly one caller, a grid item whose `justify-self` is positional. Nothing
consulted it for an ordinary box, so:

```html
<div style="background: green; height: 100px; aspect-ratio: 1/1"></div>
```

painted a **viewport-wide 100px band** where every engine paints a 100px square.
That is `css-sizing/aspect-ratio/block-aspect-ratio-002`, and it is the shape the
whole directory is built from: the tests compare against
`css/reference/ref-filled-green-100px-square`, so the assertion *is* the width.

The fix (`CssBox.TryResolveAspectRatioAutoInlineWidth`, called from
`ResolveBlockUsedWidth`) supersedes every auto-width rule in that method — the
block-level stretch-fit, the §10.3.7 inset equation, and the float/abspos
shrink-to-fit. That breadth is not an over-reach: each of those answers "how wide
is a box with no width of its own?", and a ratio plus a definite height is such a
width. Chromium agrees on all of them, including the one that looks wrong — an
abspos box with `left: 0; right: 0`, `height: 100px` and `aspect-ratio: 1/1` is
100px wide, not stretched to its insets.

**Which constraint wins where is the whole of the rest of it**, and it was read
out of Chromium rather than out of the prose:

- The transfer reads the **used** block size, so `min-height`/`max-height` clamp
  *before* it: `height: 100px; min-height: 200px; aspect-ratio: 1/1` is a 200px
  square.
- `min-width`/`max-width` clamp *after* it, and do not feed back through the
  ratio: `max-width: 40px` on that box gives 40×100, not 40×40.
- Both clamps and the ratio itself apply to the box `box-sizing` names. A
  `content-box` box with `padding: 0 20px; max-width: 60px` and a 100px
  transferred content width is **100px** wide — 60 plus the padding — which is
  what makes the clamp order observable. `TryResolveAspectRatioInlineWidth`
  clamped the border-box width against a bound stated in the content box and so
  came out at 60; it now clamps first and converts second, which also corrects
  the grid-item caller it already had.
- Auto margins still centre the result. The free space a ratio-derived width
  leaves is ordinary free space, so `margin: 0 auto` splits it.

A **column** flex item is the one box that keeps stretching, and it is not an
exception the transfer has to encode: `ApplyFlexColumnInlineAxisAlignment` runs
afterwards and widens an `align-items: stretch` item to the container, which is
what Chromium does too (`400 × 60`, not `60 × 60`).

`AspectRatioInlineSizeTests` pins all of it, Chromium-derived expectations
included.

### The other half was a `/` with spaces around it

`aspect-ratio: 1 / 2` parsed as **no ratio at all**. `TryParseAspectRatio` splits
the value on whitespace, so the slash can arrive attached to the numerator, to the
denominator, in the middle of both, or entirely on its own — and the lone-slash
token carries no number, so the numerator parse failed and the whole declaration
was rejected. `1/2` worked; `1 / 2` did not; the two are the same value, and the
spaced spelling is the one most of these tests use. That was worth **8 tests on
its own** in `css-sizing/aspect-ratio` after the transfer above had landed, and it
is invisible in any test that happens to write the value without spaces.

**Together: `css/css-sizing/aspect-ratio` 147 → 168 of 266.** Over the whole
corpus (27 327 reftests, 2026-08-20) the pair is **18 644 → 18 668 passing, +24
won and none lost** — 22 of the 24 in `css-sizing/aspect-ratio` itself, plus
`css-sizing/border-box-and-max-content-002` and
`css-values/calc-size/calc-size-aspect-ratio-004`.

The narrowness is the interesting part of that number, and it is what says the
change is the rule and not a bulldozer: a box that has *both* a definite height
and a ratio is not a common shape outside the directory that tests it, so almost
nothing else could move. Compare it with the absolute-unit fix below, which is a
smaller change and worth ten times as much, for the opposite reason.

## `border: 72pt solid red` painted a 3px black line

The biggest single win in this pass is not a layout rule at all, and nothing in
the failure names points at it. `css/CSS2/positioning`'s `left-*` and `right-*`
families were failing in a tight cluster at 98.8–98.9 % — 8 928 differing pixels,
which is 96×93 and looks like a small offset. It is not: the rendered image had
**no black square and no red one**. Neither border was painted.

`CssLengthParser.ParseToPixels` — the entry point that answers "is this a length,
and how many pixels is it?" with no font and no viewport to consult — handled
`px`, the font-relative family and the viewport family, and simply left out CSS
Values 3 §5.2's **absolute** units: `pt`, `pc`, `in`, `cm`, `mm`, `Q`. Those are
the easiest of the lot (fixed multiples of the reference pixel, and `CssMetrics`
already states every factor), and they answered `NaN`.

Its callers read that `NaN` as *"not a length"* and act on it. The `border`
shorthand is the loud one: `IsLengthOrPercentage` asks whether `72pt` is a length,
is told no, and the expansion therefore files
`border-left: 72pt solid red`'s first component under **colour**. The width falls
back to `medium` and the declared colour is dropped — a declaration that should
paint a 96px red band paints a 3px black line. The *longhand*
(`border-left-width: 72pt`) was right the whole time, which is exactly what makes
this hard to see from the outside: it is not "units are broken", it is "units are
broken in one shorthand".

**It matters far out of proportion to six keywords, because the CSS2.1 suite
states its geometry in physical units by convention.** Over the whole corpus it is
**18 668 → 18 886 passing, +220 won against 2 lost** — nine times what the layout
rule above is worth, out of twenty lines. The wins are almost entirely
`css/CSS2`: `margin-padding-clear` +78, `normal-flow` +76, `positioning` +30
(364 → 394 of 520), `borders` +21, `fonts` +12.

Two quieter callers were answering the same `NaN`: a media query such as
`(min-width: 8in)`, and a container query with an absolute length, both evaluated
as *invalid* rather than as the length they name. `css/mediaqueries` is one of
the +220 for exactly that reason.

**Both losses are false passes ending, and both were checked rather than
assumed** — this is *do not "fix" a reftest by making both sides equally wrong*
(below, under [quirks mode](#quirks-mode-reaches-the-render-as-of-the-doctype-round-trip-fix))
caught in the act. `css-break/overflowing-block-002-print` states
`border: 0.5in solid purple` on an absolutely positioned box and on an in-flow
one, and neither side painted a single purple pixel before: the test rendered 984
black pixels against the reference's 5 106, a 0.5 % difference that fits inside
the 1 % gate. Both sides paint their border now (69 120 and 84 480 purple pixels),
and what is left is the real difference between a shrink-to-fit abspos box and a
stretched in-flow one — which is what the test is about and which needs the paged
render to judge. `css-break/flexbox/multi-line-row-flex-fragmentation-080-print`
is the same shape with `border: 0.25in solid black`. Neither was measuring
anything while the border was missing.

**This one ships as a patch, not as a commit.** `CssLengthParser` is in the
`Broiler.CSS` submodule and the push is a 403, so it is
`patches/0001-resolve-the-absolute-length-units-in-parsetopixels.patch` and is
listed in `scripts/apply-pending-wpt-patches.sh` — which the reftest shard action
runs, so a CI run exercises it on top of the pinned pointer. There is no
main-repo half to fall back on: the shorthand expansion is entirely inside
`Broiler.CSS`. Identify it by its commit subject, *Resolve the absolute length
units in ParseToPixels*, not by the number — see
[`patches/README.md`](../patches/README.md) on why the number means nothing.

## A grid built from implicit columns was as wide as its border

`inline-grid` with no `grid-template-columns` at all, one child, and a
`grid-auto-columns` to size the tracks it generates:

```html
<div style="display: inline-grid; grid-auto-columns: 15px; border: 1px solid">
  <div style="grid-column: 3 / span 4; background: grey"></div>
</div>
```

`grid-column: 3 / span 4` puts the child in columns 3–6, so the grid is six 15px
tracks — 90px of content inside a 1px border. Broiler made it **2px**: the border,
and nothing else. The child came out 0px wide at x 1 instead of 60px at x 31.

`TryComputeGridIntrinsicContentWidth` summed only the tracks named in
`grid-template-columns`. Implicit columns contributed nothing, `grid-auto-columns`
was never consulted on that path, and a grid with no template bailed out entirely
so the caller measured inline *content* instead — 0, for a grid of empty divs.
That is the shape every test in
`css-grid/grid-lanes/subgrid/grid-subgridded-to-grid-lanes/track-sizing` and
`css-grid/subgrid/repeat-auto-fill-*` is built from.

The definite-width pass had always been right, so the repair was to stop having
two answers: `TryApplyGridTrackLayout`'s item collection and auto-placement moved
into `TryCollectGridPlacements` and `TryPlaceGridItems`, and the intrinsic path
now runs the same two. The count it sizes from is the count the real pass will
resolve, by construction, rather than a second implementation that can drift.
`grid-auto-columns` bounds the change: it defaults to `auto`, whose size is its
items' and therefore the track pass' job, so a grid that does not declare a
definite one is answered exactly as before.

**Fixing the width exposed the next bug in the same document.** With the container
finally 92px, a layout assertion showed the child ignoring its `grid-column` and
spanning all six tracks. The implicit-only pass declines for a nested
grid/flex/table item, because sizing an *auto* row from a single measurement of a
nested container collapses it — but when every track is a declared fixed length,
no measurement reaches a track at all and the gate is protecting nothing.
`AllTrackSizesAreFixed` relaxes that half; the baseline half stays, since a shared
baseline shifts items within a row no matter how the tracks are sized.

### The scoreboard goes down, and it is worth reading rather than summing

**5 140 reftests, before and after on the same build: 2 494 → 2 490 passing, +0
and −4.** Ten tests in 5 140 move at all — two up, eight down — and every one of
the eight is the same `inline-grid` + fixed `grid-auto-columns` shape.

The four that flip were passing **because the test and its reference both
collapsed**. Rendered side by side on both builds, before is four thin black lines
on a white page, on the test side and the reference side alike — an identical
blank, scored 100.0%. After, both render 92px boxes, and the boxes disagree. The
disagreement was always there; nothing was drawing it.

So the honest summary of this change is that it makes the renders substantially
more correct and the *scoreboard* four worse, and the two facts are the same fact.
The residual is now filed as its own gap: a subgrid resolves neither
`repeat(auto-fill, <line-names>)` nor the name-plus-index lines (`grid-column:
y 5`) the tests place items with. Widening the item gate further was tried and
reverted — it fixes the isolated placement (a probe goes 10/12 → 12/12) while
leaving all four tests red and pushing two of them further down.

The gap entry that predicted this asked for one thing: that
`css/css-grid/grid-lanes` not regress, because so many of its passes are grids
agreeing with their reference on a shared collapse. **869 tests, 195 → 195, +0 /
−0.**

## Next lead: a flex item's main size ignores its aspect ratio

Diagnosed, not fixed, and measured against Chromium on 2026-08-20. In a **row**
flex container an item with a ratio and no width paints nothing at all:

```html
<div style="display: flex; width: 400px; height: 100px">
  <div style="background: green; height: 60px; width: 50px"></div>        <!-- 50×60 ✔ -->
  <div style="background: green; height: 60px"></div>                     <!-- 0×60, correct -->
  <div style="background: green; height: 60px; aspect-ratio: 1/1"></div>  <!-- nothing; Chromium: 60×60 -->
  <div style="background: green; aspect-ratio: 1/1"></div>                <!-- nothing; Chromium: 100×100 -->
</div>
```

The second box is a fair control and shows the rule is specific: an empty item
with a height and no width genuinely *is* zero wide, in Chromium too. The last two
are the gap — CSS Flexbox §9.2 step 3 takes the flex base size of an item with a
preferred aspect ratio and a definite cross size from the **transferred** size,
and the fourth box's cross size becomes definite by `align-items: stretch` before
the main size is resolved.

**It is not the transfer added above, and that fix cannot reach it.** A row item's
width comes from `ResolveFlexItemBaseOuterWidth`, whose three branches are
`flex-basis`, a stated `width`, and — for everything else —
`GetMinMaxWidth`'s preferred content width, which is zero for an empty box. The
aspect ratio is not consulted in any of them, so whatever
`ResolveBlockUsedWidth` resolved is replaced. The missing branch belongs there,
between the stated width and the content measurement.

Worth roughly the 20 remaining `css-sizing/aspect-ratio/flex-aspect-ratio-*`
failures plus whatever a flex container used as a strip contributes elsewhere.
Probe it against Chromium first: `flex-basis`, `flex-grow` and the item's
min-content contribution all interact with the transferred size, and the flex
path is on the order of a thousand reftests, so this is not a change to make on
one test's evidence.

## Flex items are stretched now, and what that says about the scoreboard

`align-items: stretch` is the initial value, so it is what happens to most flex
items on most pages — and the engine did none of it. `CssBox.Flex.cs` only
*shifted* items for `center`/`end`; nothing ever sized one. An item with a width
and no height came out zero-tall and painted nothing, which is why any flex
container used as a strip rendered blank. Two shorthands went unexpanded with it:
`flex-flow: column` was silently laid out as a row (the wrong axis for everything
in it), and `flex: 1` never delivered a grow factor.

**`css/css-flexbox`: 470 → 568 of 994**, 123 won against 25 lost. The 25 are the
pattern this document keeps coming back to, and worth checking before reading
them as a regression: they are tests whose *reference* states the expected layout
with absolutely positioned boxes that Broiler still sizes to their text.
`flexbox_align-items-stretch` is exactly that — the render is now right to the
pixel and the reference is not, where before the two were wrong together.

**A flex container that is a grid item needs a second layout, and it scores
nothing.** A grid item is measured before its track is sized and resized
afterwards — fine for a block, whose content sits at the block-start either way,
and wrong for a flex container, whose line's cross size is read from its height.
Resizing the box alone left a strip that was the right size holding items sized
for the wrong one. Measured over 10 000+ reftests (`css/CSS2`, `css-align`,
`css-sizing`, `css-position`, `html/rendering`, `css-grid`, `css-flexbox`):
**not one test changes**. It moves the print set −2, both of them references that
used to render blank and now render, ending false passes. What it buys is that
the references built this way — most of `margin-boxes` — draw their content at
all, which is the difference between a diff you can read and a blank page.

The second layout has one trap worth recording, because it looks like a
pagination bug rather than a layout one: the pass re-places the item before the
grid puts it back, and everything it touches goes into the document's running
extent — which is what a paged render counts pages with. Leaking that
intermediate position doubled the page count of every reference built this way,
and turned eleven pixel mismatches into eleven 0.0% dimension mismatches.
`GridItemFlexRelayoutTests` pins it.

## Quirks mode reaches the render, as of the doctype round-trip fix

Worth knowing because it silently invalidated a whole directory. `SerializeToHtml`
emitted `<!DOCTYPE html>` unconditionally, and the renderer re-derives the
document mode from the string it is handed — so every doctype-less document was
rendered as standards mode, and the `quirks/` directory, which is doctype-less by
construction, could not exercise a single quirk. The serializer now emits a
doctype only when the document has one whose name selects standards mode.

The failure had no symptom of its own: quirks tests failed for whatever *other*
reason they would have failed for, and a quirk implemented against them would
have looked simply broken. If a mode-, encoding- or metadata-dependent behaviour
appears not to work at all on this path, check what the round trip does to the
signal that carries it before looking at the behaviour.

**Do not "fix" a reftest by making both sides equally wrong.** The suite's own
blind spot makes that easy to do by accident and impossible to see afterwards —
the two `css/css-page/page-size-00{7,8}-print` tests passed for exactly that
reason until `flow-root` started rendering, and nine more across the corpus with
them. When a change flips a test from pass to fail, check the oracle before
treating it as a regression: a false pass ending is progress that the scoreboard
reports as a loss.

## The runner renders the document, not a re-parse of its markup

`BROILER_WPT_DOM_RENDER`, default on. The script pass hands the renderer the
bridge's render projection (`DomBridge.GetRenderDocument`) and
`WptDocumentRenderer` binds it with `SetDocumentWithStyleSet` — the same
container, layout pass, paint pass and embedded-document compositing the string
entry point uses. Set the variable to `0` to go back to serialising and
re-parsing.

Two things the string path gets for free and this one does explicitly, both
worth knowing if you touch either:

- **The document mode.** `SetHtmlWithStyleSet` publishes
  `DocumentModeContext.CurrentQuirksMode` from the markup it is handed;
  `SetDocumentWithStyleSet` publishes nothing, so the renderer would inherit
  whatever that thread last rendered. Right by accident, and only while one
  thread renders one document.
- **Post-processing.** `HtmlPostProcessor` is regex-over-markup, so it cannot
  run. `WptDocumentPostProcessor` reproduces the three passes that can reach a
  WPT document — already-executed `<script>`s, `<iframe>` fallback, and `<map>`
  (32 files in the corpus have one) — plus the `:root` rewrite, through the
  string helper both paths share. The rest of that method is Acid-shaped and
  matches nothing here; that was checked against the checkout, not assumed.

**Adding a pass to one path means adding it to the other.** They are not
generated from a common description, and a pass that exists on only one side is
a silent per-test rendering difference rather than a build error.

The switch was made on a paired A/B over ~3 400 reftests in which 3 tests
changed, all fixed, none regressed. That is not the whole corpus, which is why
the round trip is still one environment variable away.

## What parser-blocking scripts would cost

A browser runs a `<script>` the moment the parser reaches it, against the tree
built so far. This runner parses the whole document and then evaluates the
scripts it extracted. The difference is invisible to almost every test and
decisive for a couple, so it is worth writing down what closing it involves
rather than rediscovering the question.

**What it would buy: two reftests.** Of the eleven reftests whose scripts could
observe a mid-parse DOM (`document.write`, `readyState`, `currentScript`, or a
comment saying as much), **seven already pass** — post-parse `document.write`
reproduces their result — and two of the four failures need unrelated features
(`popover-hidden-display`, `video_initially_paused`). Only
`quirks/tables-inherit-color-from-body-quirk-006` and `-007` actually require
it: their script runs before any body-level markup, so `document.body` is null
when it replaces the document element, and the document legitimately ends up
with no body.

**What it would cost.** Three things at once:

- **A parser callback.** `HtmlDocumentParser` would have to invoke the host when
  it inserts a `<script>` and resume afterwards. That change is small, but the
  parser is in the `Broiler.DOM` submodule — so it ships as a patch, and the
  first call to the new overload stops the main repository compiling against the
  pinned pointer. Every other fix on this path was kept main-repo-only precisely
  to avoid that coupling.
- **The runner's script sequencing, re-derived.** Stub injection, external
  `src=` resolution, deferred and module scripts, microtask draining, the
  `window`→global promotion and the load event are all ordered around "parse
  first, then evaluate". Interleaving moves all of it.
- **A corpus-wide behaviour change.** Every script would see only the markup
  above it. That is *more* correct — WPT scripts are written for real browsers —
  but "more correct" over ~9 000 tests is a claim only a paired A/B settles, and
  this is the runner's core rather than a leaf.

**The shortcut is a trap, and worth naming.** The tempting version is to stop
attaching `<body>` until markup opens it, which makes both tests pass. It is
wrong: HTML tree construction *does* insert `<body>` at EOF, so every head-only
document would become body-less and non-conformant. In these tests the body is
absent because the script replaced the document element *before* EOF — not
because head-only documents lack one. Passing them that way would be the same
"both sides equally wrong" failure this document warns about elsewhere, bought
at the price of a real regression.

## A mirrored element painted nothing, and issue #1732's 0.0% list

Issue #1732 ranks five reftests at a 0.0% match. Three of them are the ones the
top-thirty triage above already names, and wpt.fyi settles them: **no shipping
engine passes any of the three** on the aligned stable runs — Chrome, Edge,
Firefox and Safari all fail `css/CSS2/box-display/root-box-003.xht`,
`forced-colors-mode/forced-colors-mode-49.html` and
`html/rendering/replaced-elements/images/abs-pos-transferred-max-width-from-percentage-max-height-in-auto-height-containing-block.html`.
That is worth having on the record rather than re-derived each time the list
regenerates: `root-box-003` asserts a `display: none` root propagating its
background to the canvas, which every engine and this one deliberately decline
(`PaintWalker.FindCanvasBackgroundAndImage`); `forced-colors-mode-49` is
meaningful only when the run is in forced-colors mode, which neither this suite
nor wpt.fyi's runs are; and the third names a bare 60×60 PNG as the `rel=match`
of a full page that also carries a `<p>` of instructions. A fix for any of them
would be a deviation, not a gain.

The remaining two do pass in Chrome and Edge, and pulling on one of them found a
bug much larger than the test:

```sh
dotnet run --project src/Broiler.Wpt -- --render /tmp/t.html   # <div style="transform:rotate(180deg);background:red">
```

**rendered a blank page.** So did `scaleX(-1)`, `scaleY(-1)` and `scale(-1)`:
every axis-mirroring transform painted no pixels at all — background, borders,
children and text alike — while `translate` and a positive `scale` were fine.

The reason is one line deep. `GraphicsAdapter.SaveTransformLayer` prefers the
raster canvas, which maps a point per axis as `p * scale + translation`, and
`BCanvas.TrySaveTransform` accepts any matrix whose rotation/skew terms are zero
— which a mirror is, and which `rotate(180deg)` becomes once its sine terms round
away. But `BCanvas.Translate(RectangleF)` mapped a rectangle by scaling its
*extent*, and a negative factor made that extent negative. Every primitive walks
the rows and columns between `Left`/`Right` and `Top`/`Bottom`, so the rectangle
spanned nothing. The transform was accepted, and then drew zero pixels; the
compat backend that would otherwise have caught it is an inert stub here.

That is why `css-break/transform-024-print` scored 0.0% rather than the partial
score a mis-paginated five-page test would get: nothing inside the rotated
container reached the canvas. It still fails — it is a `-print` test, so the
default screen-mode run cannot measure what it was written to measure, and under
`BROILER_WPT_PAGED_PRINT=1` it is at 10.0% — but it fails for its own reason now.

The fix (submodule patch, subject *"Paint an element its transform mirrors"*)
normalises the mapped rectangle and mirrors the primitives whose sampling reads
*across* it rather than merely inside it — a bitmap, a tile phase, a gradient's
endpoints, a radial or conic centre and sweep, a corner radius. Glyph outlines go
through the per-point mapping and mirror on their own.

**The scoreboard barely moves, and that is the point.** Across
`css/css-transforms`, `css/compositing`, `css/css-backgrounds` and
`css/css-images` — 2 020 reftests — it goes 1 242 → **1 243**. Three tests are
gained (`transform-background-003`, `-004`, `ttwf-reftest-rotate`) and two are
lost, both of which were passing for the wrong reason: a mirror on *each* side
rendered nothing on *both*, so blank matched blank. `transform3d-scale-007` now
shows the `rotateX(180deg)` its test side still does not support, and
`animation/transform-interpolation-matrix` shows that its reference builds no
boxes at all. This is the "both sides equally wrong" failure this document warns
about elsewhere, caught in the act — and a reminder that a suite this narrow
cannot score "a mirrored element renders at all", which is what actually changed.

### `rotateX(180deg)` is exactly `scaleY(-1)`, and it was still not taken

The obvious follow-on — reducing a half-turn about X or Y to the mirror it
provably is, the same bargain `translate3d` and a 2D `matrix3d` already get in
`PaintWalker.ParseCssTransformMatrix` — was written, measured and **reverted**.
It gains `transform3d-scale-007` and `transform3d-sorting-002` and loses
`backface-visibility-hidden-animated-001` and `-002`, because an element whose
own transform is a half-turn about X or Y has its *back* face toward the viewer:
with `backface-visibility: hidden` it must not be painted at all, and painting it
mirrored is a new wrong answer where there was previously no answer. Deciding
that needs the facing parity accumulated down each `preserve-3d` chain — the
ancestor half-turn in those tests' references is exactly what cancels the
element's own — which is the 3D pipeline this renderer does not have. The
reduction is correct and worth taking *after* that, not before.
