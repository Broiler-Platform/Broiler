# WPT rendering gaps

The worst-scoring WPT pixel mismatches, the capability each one is missing, and its
owning component. **This page is the index**; the substance lives in three
documents, split by verdict:

| Document | What is in it | Tests |
| --- | --- | --- |
| [**Not fixed**](wpt-rendering-gaps-open.md) | real gaps, each with an owner, evidence and an exit gate | 55 |
| [**Won't fix**](wpt-rendering-gaps-wont-fix.md) | tests Broiler renders correctly and the golden image does not | 17 |
| [**Fixed**](wpt-rendering-gaps-fixed.md) | closed gaps, with root cause, what landed, and the wrong turns | 43 |

Start with *not fixed*. Read *won't fix* before starting on any 0.0% — closing one of
those means deleting working support. But do not read a run's *Not ranked* heading as
that verdict: of the 28 such flags checked by hand on 2026-08-13, **25 were real gaps**
being hidden by [a missing check in the runner](wpt-rendering-gaps-open.md#--verify-reference-clears-a-test-that-renders-nothing).

## Read this first — four things that are true of the whole set

1. **A 0.0% is as likely to be a reference disagreement as an engine gap.** The
   golden-image suite scores Broiler against a **Chromium screenshot**, so a
   feature Chromium lacks scores zero permanently: *shipping a feature moves the
   test down*. Since [#1618](wpt-rendering-gaps-fixed.md#the-run-reported-correct-renders-as-its-worst-failures)
   the run detects this itself and reports those failures under *Not ranked —
   reference disagreements* instead of ranking them. The
   [#1624 run](https://github.com/Broiler-Platform/Broiler/issues/1624) flagged 40.
2. **A `suspectReference` flag is a triage queue, not a verdict.** Of the 28 flags
   nobody had checked by hand, **25 were wrong** — 17 of them because Broiler renders
   a *blank canvas* for the test and for its reference, and blank-on-blank matches at
   100%. `--verify-reference`
   [never asks whether anything was drawn](wpt-rendering-gaps-open.md#--verify-reference-clears-a-test-that-renders-nothing).
   Passing a test's own reference is necessary evidence, not sufficient — the same
   way `css-grid/subgrid/orthogonal-writing-mode-006` passes its `rel=match` at 100%
   *because the test and its reference share the broken layout*
   ([details](wpt-rendering-gaps-open.md#the-flag-can-be-a-false-negative)). And the
   inverse exists too: two `css-view-transitions` tests
   [pass on CI and are demonstrably wrong](wpt-rendering-gaps-open.md#two-tests-are-green-on-ci-and-wrong).
3. **Nothing is waiting on a patch, and a patch number identifies nothing.** The
   `patches/` directory was **deleted on 2026-08-13** (main `d710a02`) once its last
   file landed upstream, and `scripts/apply-pending-wpt-patches.sh` has an empty
   `PENDING_PATCHES`. Every submodule fix in these documents is an ancestor of the
   pinned pointer, so all of it is live on CI. `patches/` was a backlog rather than
   an archive — a file was deleted once its fix was upstream and numbering restarted
   from `0001` — so the same number named different changes at different times. Fixes
   are identified here by **commit subject**:

   ```sh
   git -C <Submodule> log --oneline --grep '<commit subject>'
   git -C <Submodule> merge-base --is-ancestor <sha> HEAD
   ```
4. **A higher local score than CI is a warning, not good news.** It used to mean the
   runner had silently failed to load a root-relative resource, so *both* engines
   rendered nothing and agreed. That
   [bug is fixed](wpt-rendering-gaps-fixed.md#a-root-relative-resolver-returned-a-working-directory-relative-path)
   and a local run now agrees with CI, but the signature still deserves suspicion:
   check whether the resource actually loaded.

## Scope and history

These documents cover the low-match tail reported by the scheduled WPT runs, from
[#1491](https://github.com/Broiler-Platform/Broiler/issues/1491) (2026-07-24)
through [#1624](https://github.com/Broiler-Platform/Broiler/issues/1624)
(2026-08-12) — eight severity issues in all, plus the
[#1618](https://github.com/Broiler-Platform/Broiler/issues/1618) reporting work and
a 2026-08-12 audit.

Until 2026-08-13 this was one 2 792-line document that appended a section per run.
That kept the history but meant a verdict written five runs ago sat in the same
prose register as one written today, and a test could be recorded as *open* in one
section, *fixed* in another and *won't fix* in a third. The split is by verdict
instead, with each test appearing exactly once. **The per-run narrative is preserved
in git history** (`git log --follow docs/wpt-rendering-gaps.md`); nothing was
discarded, and each entry still names the run that first reported it.

Two problems from #1491 are out of scope and tracked in the root roadmap: the
per-element JS wrapper cost, in
[HtmlBridge runtime](ROADMAP.md#htmlbridge-runtime), and the render pipeline copying
a 642 MiB text node, in
[Bound what a large text node costs to render](ROADMAP.md#bound-what-a-large-text-node-costs-to-render).

**Companion documents:** [root roadmap](ROADMAP.md) for cross-component work;
[WPT reftests](wpt-reftests.md) for the independent reftest suite; the component
roadmaps own the implementation once an entry names them.

## How the 2026-08-13 split was verified

Every status in the three documents was re-derived from measurement rather than
carried over from the prose. Three sources, none of them the document itself:

1. **Every test the document named, run against its own reference.** 94 items
   resolving to 86 test files were extracted, resolved against a fresh WPT checkout,
   and run with `--reftests-only` — which renders both the test *and the `rel=match`
   reference the test itself declares* with Broiler. No Chromium, no committed
   golden. **75 were reftests: 47 passed, 27 failed, 1 skipped as Manual.**
2. **The CI failure manifest**, `tests/wpt-baseline/failed-tests.json`, refreshed by
   the #1624 run itself. Note its two properties before drawing conclusions: it lists
   only failures, and it is **scope-aware merged** — a run refreshes only the tests it
   exercised, so a test that becomes *skipped* keeps its old entry. That is why 204
   `manual/`-segment entries are still in it after the manual reclassification landed.
3. **Submodule history and pinned pointers**, for every claim that a fix is or is not
   live on CI.

Where the two sources disagree the reason is usually vintage: the manifest predates
the five fixes that landed on 2026-08-13 (main `68fd10a`, `3a7b9e2`, `b1611a5`), so
those tests read *fails* on CI and pass their own reference. That is expected and is
marked in the table.

### What the verification changed

Seven verdicts moved, all in the direction of the document being **more pessimistic
than the engine**:

- **`css-page/monolithic-overflow-011-print`** was *open* on "a block child of a
  `display: table-row-group` gets no box … holds the test at 2.26%". It renders
  95.1% yellow + 4.9% hotpink — what Chromium renders — passes its own reference and
  is absent from the CI failure manifest. Anonymous table-row and table-cell
  generation is implemented (`CssLayoutEngineTable.cs`). **Fixed.**
- **`css-masking/clip-path/clip-path-document-element`** and **`-will-change`** were
  *open* on "`polygon()` needs a real path clip … in a submodule this session cannot
  push to". `Broiler.Graphics` `b8aefa2` and `Broiler.HTML` `cabb66c`/`e2fe977` are
  upstream and pinned; `PaintWalker.Geometry` dispatches `polygon()`, `circle()`,
  `ellipse()` and `url(#…)`. Both pass at 100%. **Fixed.**
- **`css-view-transitions/reset-state-after-scrolled-view-transition`** was
  *part-fixed*, blocked on the rasterised root snapshot. It passes its own reference
  and is absent from the manifest. **Fixed.**
- **`fullscreen/rendering/backdrop-object`** was named as one of two mismatches
  "worth a maintainer's time". It passes its own reference at 100% and CI flags it as
  a reference disagreement. **Won't fix.**
- **`css-view-transitions/html-becomes-fixed`** and **`nothing-captured`** were filed
  as "does not reproduce here — judge from a CI artifact" on local scores of 99.99%
  and 99.54%. Both now **pass the golden comparison and fail their own reference**
  (0.5% and 97.5%). **Still not fixed, and the reporting cannot see it.**

Two further entries changed status without changing meaning: the `<canvas>` replaced-
element fix, described as shipping "as a patch with the gitlink left unbumped", landed
upstream as `Broiler.HTML` `1071e48` and its pointer was bumped the same morning; and
the eight links to `../patches/README.md` were dead, because that file was deleted with
the rest of `patches/`.

### The 28 unexamined reference-disagreement flags, triaged

The #1624 run reported 40 reference disagreements; twelve had been checked, and the
other 28 had never been looked at. All 28 were triaged the same day, by rendering each
test **and its declared reference** under the pinned Chromium as well as Broiler:

- **Chromium fails its own reftest** → the golden is not what the test asks for →
  genuine reference disagreement.
- **Chromium passes** while Broiler also reproduces the reference and the two engines
  disagree → both of Broiler's renders are wrong the same way → a real gap.

Comparisons used an 8/255 per-channel tolerance so antialiasing was not mistaken for a
structural difference — that alone moved 12 tests out of the "Chromium fails" column.
Broiler's render of each test was then compared against the fresh Chromium render and
**reproduced CI's reported percentage on 27 of 28**, confirming the committed goldens
are current.

**Three flags held. 25 did not.** Seventeen of the 25 render a blank canvas on both
sides; eleven of those seventeen share one cause —
[SVG-as-an-image goes through a second, weaker SVG renderer](wpt-rendering-gaps-open.md#svg-as-an-image-goes-through-a-second-weaker-svg-renderer)
that has no `<polygon>` arm, which reaches at least 70 currently-failing tests. Full
breakdown:
[won't fix](wpt-rendering-gaps-wont-fix.md#the-other-28-flags-triaged-2026-08-13--only-three-held).

## Reproducing one of these locally

The golden-image suite compares Broiler's render against a Chromium screenshot, so a
local investigation of a *golden* score needs both halves:

```sh
# 1. Broiler's render
dotnet run --project src/Broiler.Wpt -- --wpt-dir <checkout> --render <checkout>/<test>

# 2. Chromium's reference (Playwright is pinned in tests/wpt/package.json;
#    this container already has a browser at $PLAYWRIGHT_BROWSERS_PATH)
(cd tests/wpt && npm install)
NODE_PATH=tests/wpt/node_modules BROILER_CHROMIUM_PATH=/opt/pw-browsers/chromium \
  node scripts/generate-wpt-references.js <checkout>/<dir> <refs> --base-dir <checkout>

# 3. The comparison, its category, and side-by-side images
dotnet run --project src/Broiler.Wpt -- --wpt-dir <checkout> --reference-dir <refs> \
  --subset <dir> --failure-images <out>
```

**Judging correctness needs neither half.** The reftest suite renders the test and the
reference the test itself declares, both with Broiler:

```sh
./scripts/run-wpt-reftests.sh --wpt-dir <checkout> --subset <path>
```

`--subset` takes semicolon-separated glob patterns, so a list of exact test paths
works and is how the 86 files above were run in one pass.

### Caveats, all learned the hard way

- **Pass an absolute `--wpt-dir`.** CI does; every command written down here used to
  pass a relative one, and
  [that silently broke root-relative resources](wpt-rendering-gaps-fixed.md#a-root-relative-resolver-returned-a-working-directory-relative-path).
  The bug is fixed, but absolute remains the tested configuration.
- **The reference generator's output path is relative to `--base-dir`, not to the test
  directory.** Pointing it at one subdirectory writes `<out>/<dir>/<dir>/…`, and the
  runner then reports every test as `MissingReferenceImage` rather than as a mismatch.
  Give `--reference-dir` the root that mirrors `--wpt-dir`.
- **Playwright's pinned browser build may not be the one installed.** Point the
  generator at the installed one with `BROILER_CHROMIUM_PATH=/opt/pw-browsers/chromium`
  rather than downloading another.
- **Reference generation must honour `reftest-wait`.** A flat screenshot delay reads a
  view-transition or `takeScreenshotDelayed` test at the wrong moment.
- **`css-view-transitions` references are generation-sensitive.** Two passes can differ
  enough to move a test from 97.46% to 1.27%. Compare runs only against references
  generated in the same pass.
- **A reference is evidence about Chromium, not about the spec.** Where the answer turns
  on whether Chromium implements something, ask it directly — load the test under
  Playwright and read the CSSOM (`document.styleSheets[…].cssRules`) or the computed
  value. That is how the `view-transition-name: auto` tests were settled. Note that a
  bare Playwright script does **not** serve root-relative WPT paths, so
  `/common/reftest-wait.js` 404s and every such probe looks like "the test never ran";
  register a `file://` route that serves them from the checkout, the way
  `generate-wpt-references.js` does.
- **A test needing the WPT server is rarer than it looks.** `.sub` substitution,
  cross-origin hosts and `?pipe=` queries all turned out to be reproducible offline —
  see [the `.sub` work](wpt-rendering-gaps-fixed.md#the-runner-never-performed-wpts-sub-substitution).
  Only `?pipe=` emulation remains, and
  [no test on any list is blocked on it](wpt-rendering-gaps-open.md#pipe-is-not-emulated).

## Every test, at a glance

CI is the golden-image result from the
[#1624 run](https://github.com/Broiler-Platform/Broiler/issues/1624); `rel=match` is
Broiler against the reference the test itself declares, measured 2026-08-13. *Flagged*
means the run reported it under *Not ranked — reference disagreements*. **(blank)**
marks a `rel=match` score earned by rendering a uniform empty canvas on both sides —
a match that means nothing. Rows marked **#1624 RD** came from that run's
reference-disagreement list rather than from its severity ranking.

| Test | Status | CI (#1624) | `rel=match` | First reported |
| --- | --- | --- | --- | --- |
| `css-backgrounds/background-image-shared-stylesheet` | **not fixed** | fails | 5.7% | #1491.4 |
| `css-flexbox/percentage-heights-003` | **not fixed** | fails | n/a | #1624.27 |
| `css-grid/abspos/grid-sizing-positioned-items-001` | **not fixed** | fails | n/a | #1624.13 |
| `css-grid/grid-lanes/…/column-subgrid-orthogonal-writing-mode-004` | **not fixed** | fails | 94.8% | #1624.3 |
| `css-grid/grid-lanes/…/track-sizing/column-subgrid-auto-fill-003` | **not fixed** | fails | 94.0% | #1538.12 |
| `css-grid/grid-lanes/…/track-sizing/column-subgrid-auto-fill-008` | **not fixed** | fails | 10.4% | #1624.22 |
| `css-grid/subgrid/orthogonal-writing-mode-006` | **not fixed** [^fn] | fails (flagged) | 100.0% | #1562.18 |
| `css-masking/clip-path/clip-path-element-userSpaceOnUse-004` | **not fixed** | fails | 82.6% | #1538.27 |
| `css-transforms/animation/transform-interpolation-002` | **not fixed** | fails | n/a | #1491.13 |
| `css-view-transitions/html-becomes-fixed` | **not fixed** [^inv] | passes | 0.5% | #1497.29 |
| `css-view-transitions/iframe-and-main-frame-transition-old-main-new-iframe` | **not fixed** | fails | 74.5% | #1491.17 |
| `css-view-transitions/iframe-and-main-frame-transition-old-main-old-iframe` | **not fixed** | fails | 74.5% | #1491.16 |
| `css-view-transitions/massive-element-left-of-viewport-partially-onscreen-new` | **not fixed** | fails | 2.0% | #1538.22 |
| `css-view-transitions/massive-element-left-of-viewport-partially-onscreen-old` | **not fixed** | fails | 2.0% | #1538.23 |
| `css-view-transitions/massive-element-right-of-viewport-partially-onscreen-new` | **not fixed** | fails | 2.7% | #1538.25 |
| `css-view-transitions/massive-element-right-of-viewport-partially-onscreen-old` | **not fixed** | fails | 2.7% | #1538.26 |
| `css-view-transitions/names-are-tree-scoped` | **not fixed** | fails | 94.9% | #1497.16 |
| `css-view-transitions/nested/nested-position-with-border` | **not fixed** | passes | 98.3% | #1562.3 |
| `css-view-transitions/nested/nested-root-capture-with-clip` | **not fixed** | passes | 98.9% | #1562.4 |
| `css-view-transitions/new-content-captures-root` | **not fixed** | passes | 98.5% | #1491.19 |
| `css-view-transitions/new-content-has-scrollbars` | **not fixed** | fails | 11.1% | #1624.18 |
| `css-view-transitions/nothing-captured` | **not fixed** [^inv] | passes | 97.5% | #1538.8 |
| `css-view-transitions/old-content-captures-root` | **not fixed** | passes | 98.5% | #1491.21 |
| `css-view-transitions/old-content-has-scrollbars` | **not fixed** | fails | 11.1% | #1624.19 |
| `css-view-transitions/root-captured-as-different-tag` | **not fixed** | passes | 98.5% | #1491.23 |
| `css-view-transitions/root-to-shared-animation-start` | **not fixed** | fails | 1.5% | #1562.12 |
| `css-view-transitions/view-transition-waituntil-animation-manipulation` | **not fixed** | fails | 1.3% | #1538.19 |
| `cssom-view/scrollIntoView-fixed` | **not fixed** | fails | n/a | #1624.16 |
| `quirks/tables-inherit-color-from-body-quirk-007` | **not fixed** | fails | 94.9% | #1562.17 |
| `scroll-animations/css/scroll-timeline-nearest-with-absolute-positioned-element` | **not fixed** | fails | n/a | #1624.21 |
| `avif/animated-avif-timeout` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `compositing/root-element-background-image-transparency-001` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `compositing/root-element-background-image-transparency-002` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `compositing/root-element-background-image-transparency-003` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `compositing/root-element-background-image-transparency-004` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `css-backgrounds/animations/background-color-scroll-into-viewport` | **not fixed** | fails (flagged) | 100.0% | #1624 RD |
| `css-conditional/container-queries/query-style-color` | **not fixed** | fails (flagged) | 98.0% | #1624 RD |
| `css-gaps/flex/fragmentation/flex-gap-decorations-fragmentation-024` | **not fixed** | fails (flagged) | 97.3% | #1624 RD |
| `css-grid/layout-algorithm/auto-margins-ignored-during-track-sizing-001` | **not fixed** | fails (flagged) | 97.7% | #1624 RD |
| `css-inline/text-box-trim/text-box-trim-accumulation-004` | **not fixed** | fails (flagged) | 100.0% | #1624 RD |
| `css-paint-api/one-custom-property-animation-half-opaque.https` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `css-ruby/block-ruby-003` | **not fixed** | fails (flagged) | 98.7% | #1624 RD |
| `css-transforms/perspective-svg-001` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `css-transforms/transform-background-005` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `css-transforms/transform-background-006` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `css-transforms/transform-background-007` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `css-transforms/transform-background-008` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `css-transforms/transform-root-bg-001` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `css-transforms/transform-root-bg-002` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `css-transforms/transform-root-bg-004` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `css-view-transitions/massive-element-right-of-viewport-offscreen-new` | **not fixed** | fails (flagged) | 98.6% | #1624 RD |
| `filter-effects/backdrop-filter-clip-rect-zoom` | **not fixed** | fails (flagged) | 100.0% | #1624 RD |
| `filter-effects/backdrop-filter-plus-mask-large` | **not fixed** | fails (flagged) | 43.8% (blank) | #1624 RD |
| `resize-observer/devicepixel2` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `svg/extensibility/foreignObject/foreign-object-paints-before-rect` | **not fixed** | fails (flagged) | 100.0% (blank) | #1624 RD |
| `css-grid/alignment/grid-item-mixed-baseline-001` | **won't fix** | fails (flagged) | 100.0% | #1624 RD |
| `css-grid/grid-lanes/baseline/column-grid-lanes-item-baseline-005` | **won't fix** | fails (flagged) | 99.5% | #1624 RD |
| `css-overflow/scrollbar-gutter-003` | **won't fix** | fails (flagged) | 100.0% | #1624 RD |
| `css-color-adjust/…/color-scheme-iframe-background-mismatch-dynamic` | **won't fix** | fails (flagged) | 100.0% | #1497.25 |
| `css-color-adjust/…/mismatch-dynamic-cross-origin.sub` | **won't fix** | fails (flagged) | 100.0% | #1615 |
| `css-image-animation/image-animation-body-background-root-propagation-paused` | **won't fix** | fails (flagged) | 100.0% | #1491.9 |
| `css-image-animation/image-animation-root-background-paused` | **won't fix** | fails (flagged) | 100.0% | #1491.10 |
| `css-page/page-margin-002-print` | **won't fix** | fails | 89.2% | #1491.12 |
| `css-view-transitions/auto-name` | **won't fix** | fails (flagged) | 100.0% | #1491.14 |
| `css-view-transitions/auto-name-from-id` | **won't fix** | fails (flagged) | 100.0% | #1538.17 |
| `css-view-transitions/auto-name-from-id-shadow` | **won't fix** | fails | 100.0% | #1491.15 |
| `filter-effects/svg-filter-filter-units-user-space` | **won't fix** | fails | 95.3% | #1562.28 |
| `filter-effects/svg-filter-primitive-units-user-space` | **won't fix** | fails (flagged) | 100.0% | #1562.29 |
| `fullscreen/rendering/backdrop-iframe` | **won't fix** | fails (flagged) | 99.1% | #1612.6 |
| `fullscreen/rendering/backdrop-inherit` | **won't fix** | fails (flagged) | 100.0% | #1612.7 |
| `fullscreen/rendering/backdrop-object` | **won't fix** | fails (flagged) | 100.0% | #1618 |
| `mediaqueries/at-custom-media-basic` | **won't fix** | fails (flagged) | 100.0% | #1612.5 |
| `CSS2/positioning/abspos-025` | **fixed** [^new] | fails | 99.6% | #1624.24 |
| `css-align/animation/row-gap-interpolation` | **fixed** [^ci] | passes | n/a | #1538.24 |
| `css-color-adjust/…/color-scheme-iframe-background` | **fixed** | passes | 100.0% | #1615 |
| `css-color-adjust/…/opaque-cross-origin-002.sub` | **fixed** | passes | 100.0% | #1491.5 |
| `css-color-adjust/…/opaque-cross-origin-003.sub` | **fixed** | passes | 100.0% | #1615 |
| `css-color-adjust/…/mismatch-used-preferred` | **fixed** | passes | 99.5% | #1615 |
| `css-color/contrast-color-style-query` | **fixed** | passes | 100.0% | #1491.6 |
| `css-contain/contain-body-bg-001` | **fixed** | passes | 100.0% | #1562.21 |
| `css-contain/contain-body-bg-003` | **fixed** | passes | 100.0% | #1562.22 |
| `css-contain/contain-body-bg-004` | **fixed** | passes | 100.0% | #1562.23 |
| `css-contain/contain-html-bg-001` | **fixed** | passes | 100.0% | #1562.24 |
| `css-contain/contain-html-bg-003` | **fixed** | passes | 100.0% | #1562.25 |
| `css-contain/contain-html-bg-004` | **fixed** | passes | 100.0% | #1562.26 |
| `css-fonts/…/font-size-math-001.tentative` | **fixed** | passes | 100.0% | #1538.30 |
| `css-image-animation/image-animation-background-paused` | **fixed** | passes | 100.0% | #1491.7 |
| `css-image-animation/image-animation-body-background-no-propagation-paused` | **fixed** | passes | 100.0% | #1491.8 |
| `css-masking/clip-path/clip-path-document-element` | **fixed** [^chg] | passes | 100.0% | #1538.14 |
| `css-masking/clip-path/clip-path-document-element-will-change` | **fixed** [^chg] | passes | 100.0% | #1538.15 |
| `css-overflow/overflow-body-propagation-009` | **fixed** [^new] | fails | 100.0% | #1624.29 |
| `css-overflow/overflow-scroll-resize-visibility-hidden` | **fixed** | passes | 100.0% | #1562.20 |
| `css-page/monolithic-overflow-011-print` | **fixed** [^chg] | passes | 100.0% | #1491.11 |
| `css-page/page-box-008-print` | **fixed** [^print] | passes | 4.0% | #1491.30 |
| `css-position/overlay/overlay-transition-dialog` | **fixed** | passes | 100.0% | #1497.28 |
| `css-position/overlay/overlay-transition-finished` | **fixed** | passes | 100.0% | #1538.21 |
| `css-pseudo/backdrop-animate-002` | **fixed** | passes | 100.0% | #1538.11 |
| `css-shadow/shadow-directionality-001.tentative` | **fixed** | passes | 100.0% | #1538.20 |
| `css-shadow/shadow-directionality-002.tentative` | **fixed** | passes | 100.0% | #1538.16 |
| `css-sizing/block-image-percentage-max-height-inside-inline` | **fixed** [^new] | fails | 100.0% | #1624.14 |
| `css-sizing/image-percentage-max-height-in-anonymous-block` | **fixed** [^new] | fails | 100.0% | #1624.15 |
| `css-sizing/replaced-max-size-saturation` | **fixed** [^new] | fails | 100.0% | #1562.30 |
| `css-transforms/transform-scale-percent-001` | **fixed** | passes | 100.0% | #1497.30 |
| `css-view-transitions/nested/compute-explicit-name-non-ancestor.tentative` | **fixed** | passes | 100.0% | #1491.18 |
| `css-view-transitions/new-content-root-scrollbar-with-fixed-background` | **fixed** | passes | 99.0% | #1491.20 |
| `css-view-transitions/old-content-root-scrollbar-with-fixed-background` | **fixed** | passes | 99.0% | #1491.22 |
| `css-view-transitions/reset-state-after-scrolled-view-transition` | **fixed** [^chg] | passes | 100.0% | #1538.28 |
| `dom/nodes/moveBefore/preserve-render-blocking-style` | **fixed** | passes | n/a | #1491.27 |
| `filter-effects/fecolormatrix-negative` | **fixed** | passes | 100.0% | #1562.27 |
| `forced-colors-mode/forced-colors-mode-20` | **fixed** | passes | n/a | #1491.28 |
| `html/canvas/…/manual/dialog-paints-in-top-layer.tentative` | **fixed** [^man] | fails | skipped | #1491.24 |
| `html/…/the-link-element/stylesheet-with-base` | **fixed** | passes | 100.0% | #1491.25 |
| `html/…/form-validation-validity-textarea-defaultValue` | **fixed** [^ci] | passes | n/a | #1538.29 |
| `resource-timing/initiator-type/frameset` | **fixed** | passes | n/a | #1491.26 |
| `shadow-dom/focus-navigation/delegatesFocus-highlight-sibling` | **fixed** | passes | n/a | #1491.29 |

[^fn]: Flagged as a reference disagreement and **is not one** — the test and its
    reference share the broken layout.
    [Details](wpt-rendering-gaps-open.md#the-flag-can-be-a-false-negative).
[^inv]: Passes the golden comparison and fails its own reference — the inverse of a
    reference disagreement, and nothing in the report surfaces it.
    [Details](wpt-rendering-gaps-open.md#two-tests-are-green-on-ci-and-wrong).
[^new]: Fixed on 2026-08-13, after the #1624 run produced the manifest, so CI has not
    yet re-scored it.
[^chg]: Recorded as open or part-fixed before this review; verified fixed on
    2026-08-13.
[^print]: The `vb` gap is fixed and the golden comparison passes. Its own reference
    disagrees for the same reason
    [`page-margin-002-print`'s does](wpt-rendering-gaps-wont-fix.md#page-margin-002-print-is-a-screenshot-artifact)
    — a `-print` test scored on screen.
[^man]: Reclassified as a Manual test and no longer scored. The CI manifest entry is
    stale: a skipped test is not exercised, so its old failure entry is never cleared.
[^ci]: Absent from the CI failure manifest, but it declares no `rel=match` so the
    reftest suite cannot confirm it. Weaker evidence than the rest of this table —
    [see the caveat](wpt-rendering-gaps-open.md#testharness-tests-whose-reference-is-a-results-table).
