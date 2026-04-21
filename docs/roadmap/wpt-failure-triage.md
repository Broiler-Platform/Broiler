# Roadmap: WPT Failure Reduction and Runner Triage

> **Status**: Active — created 2026-04-18  
> **Scope**: Investigate the failures captured in `tests/wpt-results/` and define the smallest practical plan for reducing them over time.

## Implementation updates

### 2026-04-21

- The latest `WPT Tests` workflow run (`run_number: 78`, completed 2026-04-21) still fails in the final check step because WPT regressions remain, but the runner now leaves behind enough structured data in `tests/wpt-results/` to plan follow-up work from the generated JSON and Markdown artifacts instead of the raw log alone.
- The current backlog has shifted again: the largest failing directories are now `css/css-writing-modes`, `css/filter-effects`, `css/selectors`, `css/css-variables`, and `css/css-view-transitions`, while the largest missing-reference buckets are now `css/css-flexbox`, `css/css-ui/compute-kind-widget-generated`, `css/css-break`, `css/css-ui`, and `css/css-transforms`.
- A new timeout-focused planning slice is now required in addition to the earlier rendering and near-pass buckets: the latest run recorded 9 deterministic 30-second timeouts across `css-grid`, `css-overflow/scroll-markers`, `css-shapes`, `css-tables`, and `css/css-variables/url-syntax-crash.html`.
- Because the earlier roadmap items for crash handling, near-pass harvesting, unsupported-feature triage, and missing-reference accounting are already reflected in the runner/reporting layer, the next planning update should focus on the remaining non-deferred failures plus timeout ergonomics instead of repeating the now-closed `background-clip` / `background-size` work.

### 2026-04-18

- `Broiler.Wpt` now emits bucket summaries in console output, machine-readable skip reasons in `wpt-results.json`, and a triage-focused Markdown report via `--markdown-output`.
- `scripts/run-wpt-tests.sh` and `.github/workflows/wpt-tests.yml` now standardize on `tests/wpt-results/`.
- Phase 1 has started: the `background-clip-006.html` null-reference no longer reproduces in targeted local WPT runner coverage, and the regression is now guarded by a focused test in `src/Broiler.Wpt.Tests/`.
- Phase 2 has started: the `background-size` vector bucket now covers the previously failing tall `cover` + `viewBox` cases in focused tests, and the targeted subset repro passes locally again.
- Phase 2 has expanded again: the tall `contain` + `viewBox` vector repro now matches the in-repo WPT reference image in focused local coverage, so the near-pass `background-size` workstream now guards both the tall `cover` and tall `contain` cases.
- Phase 2 has widened again: the additional tall `viewBox` vector variants with omitted and percentage root dimensions now reproduce at 100% against the in-repo reference PNGs, so the focused guard rails cover the next smallest tall `contain`/`cover` subset instead of just the fully explicit SVG dimensions.
- Phase 2 has widened again: the next near-pass `background-size` subset (`wide--12px-auto--*`) now resolves percentage children against a fully injected raster viewport when an SVG root is missing either intrinsic dimension, and focused local coverage now guards both the non-`viewBox` and `viewBox` partial-dimension variants.
- Phase 2 has widened again: the adjacent `wide--auto-32px--*` vector subset now reproduces cleanly against the in-repo reference PNGs in focused local coverage as well, so the roadmap guard rails now cover both wide `auto`-height and wide fixed-height partial-dimension SVG cases.
- Phase 2 has widened again: the adjacent wide `contain` partial-dimension vector subset now reproduces cleanly against the in-repo reference PNGs in focused local coverage too, so the roadmap guard rails now cover both non-`viewBox` and `viewBox` variants across the wide `contain` bucket.
- Phase 2 has widened again: the adjacent wide `cover` partial-dimension vector subset now reproduces cleanly against the in-repo reference PNGs in focused local coverage too, so the roadmap guard rails now cover both non-`viewBox` and `viewBox` variants across the wide `cover` bucket as well.
- Phase 2 has widened again: the next in-repo `background-size` vector near-pass (`background-size-vector-003.html`) now reproduces cleanly against the committed reference PNG too, so the focused guard rails cover the adjacent fixed-width tall vector case in addition to the reduced wide/tall matrix buckets.
- Phase 2 has widened again: the adjacent in-repo `background-size` vector near-passes (`background-size-vector-005.html` and `background-size-vector-007.html`) now reproduce cleanly against the committed reference PNGs too, so the focused guard rails cover the next tall fixed-width `viewBox` variants with percent and omitted root dimensions as well.
- Phase 2 has widened again: the next adjacent in-repo `background-size` vector near-passes (`background-size-vector-009.html` and `background-size-vector-011.html`) now reproduce cleanly against the committed reference PNGs too, so the focused guard rails cover the remaining tall fixed-width omitted-dimension `viewBox` variants in that in-repo sequence as well.
- Phase 2 has widened again: the next adjacent in-repo `background-size` vector near-passes (`background-size-vector-013.html`, `background-size-vector-015.html`, and `background-size-vector-017.html`) now reproduce cleanly against the committed reference PNGs too, so the focused guard rails cover the remaining tall fixed-width percent-width `viewBox` variants in that in-repo sequence as well.
- Phase 2 has widened again: the adjacent `selectors-4` near-pass workstream now handles quoted `:lang(...)` arguments and the `:open` pseudo-class for `details`, including JS-driven `open` state reflection, and focused selector regression coverage now guards those fixes locally as well.
- Phase 2 has widened again: the representative `selectors-4` near-pass regressions now also cover document-root `lang-*` matching alongside the earlier `details-open-pseudo-*` and descendant `:lang(...)` cases, so that bucket's known near-pass issues are now closed locally instead of relying on only a subset of the selector patterns.
- Phase 2 has widened again: the selector invalidation workstream now reapplies CSS-derived inline styles across the whole document scope after selector-affecting class, id, reflected-attribute, and DOM sibling mutations, and focused serialization regressions now guard representative `class-id-attr`, sibling-combinator, and `:disabled`-style updates locally.
- Phase 2 has widened again: the focused `background-size` guard rails covering the in-repo near-pass vector bucket now still pass as a single validation set, so that Phase 2 bucket is now considered closed locally instead of being tracked as an open near-pass tranche.
- Phase 2 has widened again: the remaining selectors invalidation coverage now includes representative `:nth-child(... of selector)` / `:nth-last-child(... of selector)` matching plus class-mutation invalidation guard rails, so that bucket is now closed locally alongside the earlier document-scope style invalidation fixes.
- Phase 2 has widened again: the remaining writing-modes forms coverage now includes representative writing-mode-aware logical computed sizes (`block-size` / `inline-size`) for form controls, so that bucket is now closed locally with focused button/select/date guard rails.
- Phase 3 has started: main-document media queries now evaluate against the real viewport again, single-value `calc()` / `max()` / `min()` lengths are normalized in both the DOM bridge and renderer parser, and focused local guard rails now cover representative `css-values`, `css-viewport/zoom`, and `cssom-view/*zoom*` regressions.
- Phase 3 has widened again: zoom-sensitive element metrics now expose `clientTop` / `clientLeft` and border-inclusive `offsetWidth` / `offsetHeight` without applying the target element's own zoom, and focused DOM-bridge regressions now cover representative `cssom-view/*zoom*` client/offset expectations locally too.
- Phase 3 has widened again: `offsetParent` / `offsetTop` / `offsetLeft` now resolve against the nearest positioned ancestor in raw CSS pixels instead of document-space zoomed coordinates, including the nested collapsed-margin cases from the zoom-sensitive CSSOM view bucket.
- Phase 3 has widened again: viewport-aware media-query lengths now resolve through `matchMedia()` too, including `vw` / `vh`, `vmin` / `vmax`, and single-value `calc()` wrappers in both JS and stylesheet `@media` paths.
- Phase 3 has widened again: `scrollIntoView()` now respects `scroll-padding-*` and `scroll-margin-*` offsets in raw CSS pixels, including inherited `scroll-padding-top` / `scroll-margin-top` under zoomed containers.
- Phase 3 has widened again: `scrollIntoView()` now uses positioned `top` / `left` offsets for absolutely positioned targets in raw CSS pixels, including zoomed scroll containers.
- Phase 3 has widened again: `scrollIntoView()` now resolves percentage `top` / `left` insets for absolutely positioned targets in raw CSS pixels, including fixed and zoomed scroll containers.
- Phase 3 has widened again: `ch` and `ex` font-relative lengths now resolve in both layout and CSSOM width/height helpers, including raw CSS pixel metrics under zoom.
- Phase 3 has widened again: CSSOM `clientWidth` / `clientHeight` now include padding, and the focused padded `scrollWidth` / `scrollHeight` negative-margin case stays aligned with the padded client box under zoom.
- Phase 3 has widened again: CSSOM scrolling now covers `element.scroll()` plus object-argument `scrollTo()` / `scrollBy()` behavior, including clamped `scrollLeft` / `scrollTop` signs for representative writing-mode and direction combinations.
- Phase 3 has widened again: `lh` and `rlh` font-relative lengths are now recognized in the shared parser and resolve through focused renderer/bridge width-height cases, including parent-line-height `lh` semantics and default-root-line-height `rlh` metrics under zoom.
- Phase 3 has widened again: CSSOM `scrollWidth` / `scrollHeight` now account for mixed parent/child zoom overflow in raw CSS pixels, so padded scroll containers keep the correct overflow extent when zoom is applied only to descendants or to both container and child.
- Phase 3 has widened again: `scrollIntoView()` now avoids incorrectly bubbling root scrolling for targets inside fixed-position containers while still scrolling fixed-position scrollers themselves, matching the focused raw-CSS-pixel CSSOM view cases locally.
- Phase 3 has widened again: css-values math handling now covers deeper `calc()` parenthesis stacks, multi-argument `max()` length lists, invalid unitless zero rejection inside `min()` / `max()`, and negative `calc()` media-query ranges clamped to zero in the focused renderer and bridge guard rails.
- Phase 3 has widened again: bridge-side CSSOM geometry now resolves viewport `calc()` lengths for mixed viewport-plus-pixel and mixed viewport-plus-percentage rect cases, and explicit `body { margin: 0 }` no longer falls back to the default 8px body margin in those focused viewport-length regressions.
- Phase 3 has widened again: `ic` font-relative lengths now resolve through the shared parser and bridge-side CSSOM length helpers, including focused static css-values coverage and zoom-stable raw-CSS-pixel guard rails.
- Phase 3 has widened again: focused `attr(... type(<length>))` value resolution now works in direct length and `max(...)` width cases, including fallback handling in both renderer-applied stylesheet declarations and bridge-side CSSOM/inlined-style length consumers.
- Phase 3 has widened again: zoom-sensitive CSSOM view geometry now excludes preceding absolute/fixed siblings from normal-flow stacking, which stabilizes focused `getBoundingClientRect()`, `getClientRects()`, `scrollTo()`/scroll metrics, and offset metric cases in raw CSS pixels.
- Phase 4 has started: `Broiler.Wpt` triage output now emits a dedicated deferred feature-gap section in console, JSON, and Markdown output, explicitly aggregates `css/css-view-transitions/*` and larger `filter-effects` failures, surfaces other `MissingContent`-dominant buckets separately, and stops suggesting those suites as the next near-pass `--subset` commands.
- [Phase 5](#phase-5--expand-reference-coverage-for-skipped-suites) has started: `Broiler.Wpt` now emits dedicated missing-reference priority buckets in console, JSON, and Markdown output, suggests the highest-value `--subset` commands to generate references for those skipped suites first, and flags pass-rate comparisons as non-comparable until the same subset is rerun with those references in place.
- The `background-clip*` subset has now been rerun against the in-repo WPT corpus; the raw subset still fails broadly on full-page visual noise, so guard rails now focus on the reproducible box-model cases (`border-box`, `padding-box`, `content-box`, size/position/radius variants, and `border-area` corner-shape) instead of the instruction text around them.
- **Deviation from the original proposal:** the roadmap-friendly Markdown file is generated directly by `Broiler.Wpt` instead of a separate post-processing step so the same logic is shared by local runs and CI.
- **Phase 5 deviation:** this phase now stops short of committing bulk generated reference artifacts into the repository; instead, the runner/reporting layer makes the missing-reference backlog explicit so the largest skipped buckets can be reduced incrementally with reproducible subset commands.
- **Current blocker:** the hard crash is fixed, but the wider `background-clip` bucket still contains visual mismatches that belong to the next near-pass remediation steps rather than this crash-only fix.
- **Phase 5 blocker:** the largest skipped buckets still require external Playwright reference-generation time against a WPT checkout, so this repo change focuses on prioritization and pass-rate comparability tracking rather than landing thousands of generated images at once.
- **Current near-pass focus:** continue harvesting the remaining `background-clip` and `background-size` cases with the smallest reproducible `--subset` commands rather than broad CSS reruns.
- **Secondary blocker:** full-solution validation still hits an unrelated compile failure in `Broiler.HTML.WPF/Adapters/GraphicsAdapter.cs` (`DrawGradientString` override missing), so WPT triage work should continue using targeted `Broiler.Wpt` validation until that project is repaired.
- Related WPT bucket issues: #956 (`background-clip` failures), #958 (`css-background-clip` follow-up), #962 (`background-size` vector cases).

---

## 1. Current Snapshot

This roadmap is based on the latest committed WPT artifacts in `tests/wpt-results/` and the latest `WPT Tests` workflow run (`run_number: 78`, completed 2026-04-21).

### 1.1 Result totals

From `tests/wpt-results/wpt-results.json` / `tests/wpt-results/wpt-summary.txt`:

- **Total**: 24,919
- **Passed**: 2,261
- **Failed**: 1,964
- **Skipped**: 20,694

### 1.2 What failed in CI

The `WPT Tests` workflow is successfully producing the summary, root-cause analysis, and JSON artifacts. The workflow fails in the final **Check test result** step because the runner exits non-zero when WPT failures remain. In other words, the current blocker is **test coverage/compliance**, not artifact generation.

### 1.3 Failure category breakdown

From `tests/wpt-results/wpt-root-cause-analysis.txt`, `tests/wpt-results/wpt-triage-summary.md`, and `tests/wpt-results/wpt-results.json`:

- **1,955 `PixelMismatch`** failures
  - 902 **`MissingContent`**
  - 822 **`MinorDiff`**
  - 162 **`LayoutShift`**
  - 58 **`ColorShift`**
  - 11 **`SubpixelAntiAliasing`**
- **9 `Timeout`** failures
  - `css/css-grid/parsing/grid-template-columns-crash.html`
  - `css/css-overflow/scroll-markers/column-scroll-marker-007.html`
  - `css/css-overflow/scroll-markers/targeted-scroll-marker-selection-with-transition.tentative.html`
  - `css/css-overflow/scroll-markers/targeted-scroll-marker-selection.tentative.html`
  - `css/css-shapes/shape-outside/supported-shapes/circle/shape-outside-circle-030.html`
  - `css/css-shapes/shape-outside/supported-shapes/circle/shape-outside-circle-031.html`
  - `css/css-tables/height-distribution/percentage-sizing-of-table-cell-children.html`
  - `css/css-tables/html5-table-formatting-3.html`
  - `css/css-variables/url-syntax-crash.html`

---

## 2. Highest-Value Failure Buckets

### 2.1 Top failing directories

The current failures are concentrated enough that the work should be done in buckets rather than test-by-test.

| Bucket | Failed tests | Main symptom |
|---|---:|---|
| `css/css-writing-modes` | 382 | Mostly `MissingContent`, with a still-meaningful `MinorDiff` tail |
| `css/filter-effects` | 241 | Explicit deferred feature gap / unsupported rendering work |
| `css/selectors` | 197 | Mostly `MissingContent`, with localized invalidation leftovers |
| `css/css-variables` | 180 | Mixed pixel mismatches plus a reproducible timeout (`url-syntax-crash.html`) |
| `css/css-view-transitions` | 130 | Explicit deferred feature gap / unsupported rendering work |
| `css/motion` | 93 | Mostly `MissingContent` and already a deferred feature-gap candidate |
| `css/cssom-view` | 86 | Smaller targeted follow-up bucket after the earlier zoom work |
| `css/css-viewport` | 65 | Remaining zoom/viewport leftovers, mostly `MissingContent` |

### 2.2 Top skipped directories

The skip volume is even larger than the failure volume, so part of the roadmap must focus on reference coverage and triage ergonomics.

| Bucket | Skipped tests | Likely cause |
|---|---:|---|
| `css/css-flexbox` | 1,038 | No generated reference images |
| `css/css-ui/compute-kind-widget-generated` | 802 | No generated reference images |
| `css/css-break` | 478 | No generated reference images |
| `css/css-ui` | 471 | No generated reference images |
| `css/css-transforms` | 451 | No generated reference images |
| `css/css-grid` | 440 | No generated reference images outside the current timeout slice |
| `css/css-text` | 431 | No generated reference images |
| `css/css-overflow` | 241 | Mostly missing references plus the active timeout slice |

---

## 3. Likely Root Causes

### 3.1 `MissingContent` failures

These cluster heavily in:

- `css/css-writing-modes`
- `css/selectors`
- `css/css-variables`
- `css/css-viewport`
- `css/motion`
- `css/css-view-transitions/nested`

This pattern strongly suggests missing or incomplete support for one or more of:

- zoom/viewport-aware layout calculations
- background clipping and paint order edge cases
- selector invalidation / dynamic style recomputation
- writing-mode-aware form control layout
- CSS value resolution for `url()`, `calc()`, and related computed values
- unsupported view-transition features

### 3.2 `MinorDiff` failures

These are still the best short-term opportunity for pass-rate improvement because they already render something close to the reference image. The biggest remaining clusters are:

- `css/css-writing-modes`
- `css/selectors`
- `css/css-variables`
- `css/cssom-view`

These should still be prioritized ahead of broad unsupported-feature suites because the fixes are more likely to be localized and testable.

### 3.3 `LayoutShift`, 0% matches, and timeout-heavy slices

The worst failures still include 0% matches in `css/css-values/*` (notably `vh-calc-support-pct.html`) and a handful of deterministic 30-second timeouts in `css-grid`, `css-overflow/scroll-markers`, `css-shapes`, `css-tables`, and `css/css-variables`. Those deserve their own workstream because they point to execution-path or systemic value/layout bugs rather than paint-only differences.

### 3.4 Deferred unsupported-feature buckets

`css/filter-effects`, `css/css-view-transitions`, and the largest `MissingContent`-dominant portions of `css/css-writing-modes`, `css/selectors`, and `css/motion` should remain explicitly deferred unless a PR is clearly scoped to feature support rather than a near-pass cleanup.

---

## 4. Prioritized Remediation Plan

## Phase 0 — Stabilize triage inputs

- [x] Keep `tests/wpt-results/wpt-results.json` as the canonical input for grouping and prioritization.
- [x] Standardize on a single results path name. Today the repository contains `tests/wpt-results/`, while the runner/workflow currently write to `tests/wpt/results`.
- [ ] When investigating a bucket, always rerun via `--subset` instead of the full CSS corpus.

**Exit criteria:** every follow-up issue/PR names a specific bucket and uses a reproducible subset command.

## Phase 1 — Fix crash / deterministic rendering errors

- [x] Fix the null-reference in `css/css-backgrounds/background-clip-006.html`.
- [x] Re-run the `css/css-backgrounds/background-clip*` subset and convert that suite into a stable guard rail.

**Why first:** crash-style failures are usually small in count but high in leverage.

## Phase 2 — Harvest the near-pass buckets

Target the buckets with the highest concentration of `MinorDiff` and mid/high match percentages:

- [x] `css/css-writing-modes/forms`
- [x] `css/selectors/invalidation`
- [x] `css/css-backgrounds/background-size`
- [x] `css/selectors/selectors-4`

**Working rule:** prefer small renderer/layout fixes that retire dozens of visually-close failures at once.

**Status:** Phase 2 finished.

## Phase 3 — Address systemic value/layout bugs

Target the clusters with many `LayoutShift` or 0% mismatches:

- [x] `css/css-values/*` (`calc-*`, `vh-*`, media-query/value resolution)
- [x] `css/css-viewport/zoom`
- [x] `css/cssom-view/*zoom*`

**Working rule:** land these fixes only with focused regression tests because these bugs are likely cross-cutting.

**Status:** Phase 3 finished. The systemic value/layout bugs originally targeted here now have focused local guard rails across `css-values`, `css-viewport/zoom`, and `cssom-view/*zoom*`: deeper `calc-*` parsing, viewport-aware media-query resolution, viewport `calc()` geometry, explicit viewport-length inheritance plus root-`rem`/`em` consumers and negative-delay viewport interpolation snapshots, `attr(... type(<length>))` direct/max/fallback handling, `ic` alongside `ch` / `ex` / `lh` / `rlh` font-relative lengths, zoom rendering, zoom-sensitive CSSOM view rect/scroll/offset geometry in raw CSS pixels, padded client/scroll metrics including mixed-zoom overflow, scroll API alias/options and writing-mode-aware clamp behavior, and zoomed/fixed-position `scrollIntoView()` spacing/absolute-position/percentage-inset cases. Remaining stale or broader `MissingContent` leftovers in those directories should now be treated as Phase 4 unsupported-feature triage instead of blocking Phase 3 completion.

## Phase 4 — Triage unsupported feature clusters separately

Do not mix these into the near-pass work. Track them as explicit feature gaps or deferred suites:

- [x] `css/css-view-transitions/*`
- [x] larger `filter-effects` failures
- [x] other suites dominated by `MissingContent` rather than near-pass diffs

**Working rule:** if the platform feature is incomplete, document the gap instead of repeatedly re-triaging the same failures.

**Status:** Phase 4 is now accounted for in the runner/reporting layer instead of via repeated manual triage. `Broiler.Wpt` now keeps the explicit unsupported suites (`css/css-view-transitions/*` and larger `filter-effects` buckets) out of the "Suggested next subset commands" section, and it surfaces additional `MissingContent`-dominant buckets as deferred feature gaps in console output plus the generated JSON/Markdown artifacts. This keeps the near-pass work queue focused on smaller reproducible wins while still preserving machine-readable tracking for broader unsupported areas such as the current `css/css-values/calc-size`, `css/css-writing-modes`, `css/selectors`, `css/motion`, and stale `css/css-viewport/zoom` leftovers seen in the committed artifact set.

## Phase 5 — Expand reference coverage for skipped suites

The current skip count is too large to ignore.

- [x] Prioritize reference generation for the largest skipped buckets first.
- [x] Treat missing-reference skips separately from renderer failures in future progress tracking.
- [x] Only compare pass-rate changes after the skip backlog is reduced for the same subset.

**Why this matters:** without better reference coverage, the failure rate understates the real work remaining.

**Status:** Phase 5 is now tracked directly in the runner/reporting layer. `Broiler.Wpt` emits a dedicated missing-reference backlog section in console output plus the generated JSON/Markdown artifacts, ranks the largest missing-reference skip buckets separately from other skips, and surfaces the corresponding `./scripts/run-wpt-tests.sh --subset "<bucket>"` commands so reference generation can be expanded incrementally with explicit traceability.

**Deviation from the original proposal:** instead of landing a bulk repository-wide reference refresh in one PR, this phase now focuses on the smallest practical change that makes the missing-reference backlog measurable and reproducible bucket-by-bucket before broader artifact refreshes are attempted.

**Current blocker:** the largest skipped buckets still need significant external Playwright reference-generation time against a WPT checkout, so the current repo-side implementation can only prioritize and track those suites until the generated images themselves are produced.

## Phase 6 — Triage current non-deferred failures and timeout ergonomics

The roadmap now needs a fresh post-Phase-5 slice for the failures still surfacing in the latest artifact set.

- [ ] Open a focused `css/css-variables` workstream, because it is now one of the largest non-deferred failure buckets and also contains a reproducible timeout (`url-syntax-crash.html`).
- [ ] Treat the 9 timeout cases as a single triage track with focused subset commands for `css/css-grid/parsing`, `css/css-overflow/scroll-markers`, `css/css-shapes/shape-outside`, `css/css-tables`, and `css/css-variables`.
- [ ] Keep using the generated missing-reference priority list (`css/css-flexbox`, `css/css-ui/compute-kind-widget-generated`, `css/css-break`, `css/css-ui`, `css/css-transforms`) when reference generation time is available, rather than broad CSS reruns.
- [ ] Re-classify remaining `css/css-writing-modes`, `css/selectors`, `css/cssom-view`, and `css/css-viewport` failures bucket-by-bucket into either small near-pass fixes or explicit deferred feature gaps before starting new full-subset campaigns.

**Why this matters:** the roadmap already covers the earlier crash, near-pass, and reporting work; this phase keeps the planning document aligned with the failures still blocking the latest `WPT Tests` run.

---

## 5. Suggested Runner / CLI Improvements

If direct test fixes remain too expensive, these are the highest-value tooling improvements.

### 5.1 Add bucket summaries directly to the runner output

Add a top-N summary for:

- failing directories
- skipped directories
- mismatch subcategories
- lowest-match tests

This information already exists in `wpt-results.json`; surfacing it directly in the CLI would remove a lot of manual post-processing.

### 5.2 Emit a roadmap-friendly summary file

Generate a small Markdown summary alongside the JSON report containing:

- current totals
- top failing buckets
- top skipped buckets
- non-pixel/rendering exceptions
- suggested next subset commands

### 5.3 Distinguish unsupported-feature skips from missing-reference skips

Right now the largest skip buckets mostly read as “No reference image.” A separate machine-readable reason field would make backlog accounting much easier.

### 5.4 Standardize the output directory name

Choose either `tests/wpt-results/` or `tests/wpt/results/` and use it consistently across:

- the repository artifacts
- `scripts/run-wpt-tests.sh`
- `.github/workflows/wpt-tests.yml`

This is a small change, but it removes unnecessary ambiguity when people look for the latest reports.

### 5.5 Surface timeout summaries as first-class triage output

The latest run still required reading the raw job log to see the exact 9 timeout paths. Add a dedicated timeout section in the generated Markdown/console summary, plus suggested `--subset` commands for the affected directories.

### 5.6 Support incremental reruns from the previous JSON report

If the remaining buckets are too expensive to attack via repeated broad subset runs, add a runner/CLI mode that reruns only the previous failure or timeout set from `tests/wpt-results/wpt-results.json`. That would make timeout and bucket triage faster without waiting for another full CSS pass.

---

## 6. Recommended Execution Order for Follow-up PRs

1. **Open a dedicated timeout triage track for the 9 deterministic 30-second timeouts**
2. **Tackle `css/css-variables` as the largest current non-deferred failure bucket**
3. **Re-triage the remaining `css/css-writing-modes`, `css/selectors`, `css/cssom-view`, and `css/css-viewport` leftovers into fix-vs-defer buckets**
4. **Improve reference coverage for `css/css-flexbox`, `css/css-ui/compute-kind-widget-generated`, `css/css-break`, `css/css-ui`, and `css/css-transforms`**
5. **Keep explicit unsupported suites (`css/css-view-transitions`, larger `filter-effects`, `css/motion`) as separate backlog items**
6. **If direct fixes are too slow, prioritize timeout summaries and incremental rerun support in the runner/CLI**

---

## 7. Validation Strategy

For each bucket-specific PR:

- run the smallest relevant `--subset` command through `scripts/run-wpt-tests.sh`
- add or update focused tests in `src/Broiler.Wpt.Tests/` when the fix is in shared runner logic
- avoid using the full CSS run as the development loop
- use the JSON report to confirm the bucket count actually moved

---

## 8. Definition of Success

This roadmap should be considered successful when:

- the current timeout cases are either fixed, explicitly deferred, or trivially reproducible via focused subset commands
- the top non-deferred failure buckets are materially reduced
- value/layout bugs are split into explicit workstreams instead of mixed into paint-only failures
- skip counts are tracked separately from renderer failures and pass-rate comparisons stay tied to consistent reference coverage
- the runner/CLI surfaces enough bucket-level information that new WPT failures can be triaged without custom one-off scripts or raw-log scraping
