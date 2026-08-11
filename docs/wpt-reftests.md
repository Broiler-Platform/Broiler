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
3. **Compare** at the run's pixel pass threshold (99% by default, i.e. at most 1%
   of pixels may differ) — the same threshold and comparer the golden-image suite
   uses.
4. **Verdict.** A `rel="match"` reference must be reproduced; with several of
   them the test passes on the first one it reproduces (WPT's own rule) and the
   closest candidate is what a failure reports. A test whose references are all
   `rel="mismatch"` passes only when it differs from every one of them.

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
  opposite and is the modern rule. Making it pass means regressing that one.
  **A reftest failing is not by itself evidence of a defect**, and this suite
  will never tell you which of a contradictory pair to believe.
- **A feature nothing here implements**, where the render is honest and the
  match is 0% anyway: the three `jpegxl/` tests need a JPEG XL decoder (Chromium
  has none either), the `fullscreen/` and customizable-`select` ones need
  `testdriver.js` to drive them, `forced-colors-mode-49` needs the run to be in
  forced-colors mode, and the two `*.sub.html` colour-scheme tests need the WPT
  server for their cross-origin substitution.

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
when it is an image or a gradient. The one-line call site is
`patches/0001-html-canvas-backdrop-lever.patch`; until it is applied
`page-box-002-print` stays at 0.0 % and everything else here works without it.

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
the element in full — `patches/0002-html-empty-inset-clip.patch` is the four lines
that emit it instead. With both halves: **`css/CSS2/visufx` 6 → 50 of 51**, plus
two in `css-masking/clip` that were the same bug reached through `clip-path`
directly, and nothing lost. Without the patch the main-repo half still lands the
non-empty cases; it is the empty ones that wait on it.

Every `clip-*` test in the directory passes then; the one failure left in it is
`visibility-005`, which is about `visibility` and not about clipping at all.

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
