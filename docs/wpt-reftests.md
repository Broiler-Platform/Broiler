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
paged media, not thirteen separate fixes.

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
