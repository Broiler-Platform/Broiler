# Roadmap: WPT Failure Reduction and Runner Triage

> **Status**: Active — created 2026-04-18  
> **Scope**: Investigate the failures captured in `tests/wpt-results/` and define the smallest practical plan for reducing them over time.

## Implementation updates

### 2026-04-22

- The runner/Markdown/JSON triage output now surfaces timeout failures as a first-class section with the complete timeout path list plus focused `--subset` commands for each affected directory, and `Broiler.Wpt.Tests` now guards that richer timeout summary so future WPT workflow investigations do not require raw-log scraping.
- `Broiler.Wpt` now supports incremental reruns from a previous JSON report via `--rerun-json`, including a `--rerun-kind timeouts` mode for timeout-only debugging, and the generated JSON report now includes relative test paths so reruns remain tied to the current WPT checkout instead of the original absolute machine path.
- `scrollIntoView()` now treats fixed-position iframe descendants as fixed only within their own browsing context: it skips same-document root scrolling for subframe-fixed targets, still bubbles to outer browsing-context scrollers, resolves fixed `bottom` / `right` insets against the subframe viewport, and subtracts already-applied intermediate scroll offsets when continuing to outer ancestors. Focused `Broiler.Wpt.Tests` guard rails now cover the `scrollIntoView-fixed.html` Box B / Box D iframe cases.
- Subframe root/window scrolling now honors `scroll-behavior: smooth` plus `behavior: auto|instant|smooth` for `scrollIntoView()` and window/root scroll APIs by staging a deferred next-frame completion instead of jumping immediately, and focused `Broiler.Wpt.Tests` now guard the `scroll-behavior-subframe-root.html` / `scroll-behavior-subframe-window.html` follow-up slice without needing the whole harness file.
- Root `scrollIntoView()` bubbling now respects hidden/clip viewport overflow, so zoomed inner scrollers no longer spuriously scroll the document when `html`/`body` disable viewport scrolling; focused zoom guards cover the `scroll-padding`, `scroll-margin`, and abspos `scrollIntoView()` follow-up slice.
- Standalone universal selectors with structural pseudos/attributes now cascade after bare universal and tag rules, and closed `<details>` elements now hide non-`<summary>` children in the renderer path; focused `Broiler.Wpt.Tests` guard the remaining `selectors-4` `:lang(...)` / `details:open` near-pass slice locally.
- Bridge-side HTML attribute parsing now keeps the first duplicate attribute instead of the last, which fixes the reopened `css/selectors/invalidation` `nth-child(... of .c)` / `nth-last-child(... of .c)` sibling-toggle slice and adds focused CLI/WPT coverage for those cases.
- Oversized `grid-template-columns` / `grid-template-rows` track lists now bail out early in the lightweight grid parser instead of splitting multi-megabyte crash-style inputs, which starts hardening the `css/css-grid/parsing/grid-template-columns-crash.html` timeout track and adds a focused `Broiler.Wpt.Tests` guard for the parser-side repro.

### 2026-04-21

- The latest committed artifacts and the latest `WPT Tests` workflow run (`run_number: 79`, completed 2026-04-21) still fail only because WPT regressions remain; artifact generation itself is succeeding, and the runner leaves enough structured data in `tests/wpt-results/` to plan follow-up work from the JSON first and the Markdown/log views second.
- The current backlog has shifted again: the largest failing directories are now `css/css-writing-modes`, `css/filter-effects`, `css/css-variables`, `css/selectors`, and `css/css-view-transitions`, while the largest missing-reference buckets remain `css/css-flexbox`, `css/css-ui/compute-kind-widget-generated`, `css/css-break`, `css/css-ui`, and `css/css-transforms`.
- The timeout slice has narrowed but still needs its own workstream: the latest run recorded 8 deterministic 30-second timeouts concentrated in `css/css-grid/parsing`, `css/css-overflow/scroll-markers`, `css/css-shapes/shape-outside`, and `css/css-tables`.
- Because the earlier roadmap items for crash handling, near-pass harvesting, unsupported-feature triage, and missing-reference accounting are already reflected in the runner/reporting layer, the next planning update should focus on the remaining non-deferred failures, the near-pass slices inside deferred parents, and timeout ergonomics instead of repeating the now-closed `background-clip` / `background-size` work.
- Phase 6 has started: crash-test script execution now batches CSS invalidation for bulk DOM mutations, which lets the focused `css/css-variables/url-syntax-crash.html` repro complete locally again and adds a dedicated `Broiler.Wpt.Tests` timeout guard rail for that path.
- Phase 6 has widened again: same-block custom-property substitution now feeds parser-side and CSSOM-side shorthand expansion for representative `font`, `margin`, `border-left`, and `background` cases, with focused `Broiler.Cli.Tests` / `Broiler.Wpt.Tests` guard rails covering the `vars-font-shorthand`, `vars-background-shorthand`, and `variable-substitution-shorthands` follow-up slice locally.
- Phase 6 has widened again: `getComputedStyle(element, pseudoElement)` now distinguishes element vs pseudo-element rule matching, accepts unresolved `var(...)` values in closed-keyword bridge declarations until the later substitution pass, and locally guards representative `::first-line` / `::first-letter` custom-property cases from the next `css/css-variables` pseudo-element slice.
- Phase 6 has widened again: renderer-side inherited custom-property substitution now resolves representative text, background, and border paint values from ancestor-defined variables at used-value time, with a focused `Broiler.Wpt.Tests` guard rail advancing the remaining `variable-reference-*` / visited-context paint-color bucket.
- Phase 6 has widened again: parser-side and bridge-side custom-property recovery now tolerate the unclosed nested fallback tail from `missing-closing-nested-fallback.html`, with focused CLI computed-style and WPT rendering guard rails for that malformed-but-recoverable substitution path.
- Phase 6 has widened again: element scroll APIs now ignore non-scroll-container elements with `overflow:visible` (explicit or implicit) while preserving `overflow:hidden` scrolling-box behavior, with focused CLI and WPT guard rails for the `dom-element-scroll.html` slice.
- Phase 6 has widened again: iframe `srcdoc` subdocuments now expose `contentDocument.scrollingElement`, a cached same-origin `contentWindow`, and subframe window scroll proxies with `scrollX`/`scrollY`/`pageXOffset`/`pageYOffset`, with focused CLI and WPT guard rails for the `scroll-behavior-subframe-root.html` / `scroll-behavior-subframe-window.html` slice.
- Phase 6 has widened again: fetched iframe subdocuments now run their own scripts in document context and resolve nested relative iframe sources against the containing subdocument URL, with focused CLI and WPT guard rails for the `iframe-zoom-nested.html` browsing-context slice.
- Phase 6 has widened again: file-based WPT runs now normalize templated cross-origin iframe URLs and map `*.web-platform.test` resource paths back to the local `tests/wpt` tree for rendering while preserving cross-origin script fences, with focused CLI and WPT guard rails for the `iframe-zoom.sub.html` slice.

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

This roadmap is based on the latest committed WPT artifacts in `tests/wpt-results/` and the latest `WPT Tests` workflow run (`run_number: 79`, completed 2026-04-21).

For day-to-day triage, treat `tests/wpt-results/wpt-results.json` as the canonical source of totals, bucket rankings, skip reasons, and deferred-feature classification.

Use `tests/wpt-results/wpt-root-cause-analysis.txt`, the workflow job log, and the generated Markdown summary artifact as convenience views when you need the raw timeout paths or a shareable human-readable summary.

### 1.1 Result totals

From `tests/wpt-results/wpt-results.json`:

- **Total**: 24,920
- **Passed**: 2,267
- **Failed**: 1,984
- **Skipped**: 20,669

### 1.2 What failed in CI

The `WPT Tests` workflow is successfully producing the summary, root-cause analysis, and JSON artifacts. The workflow fails in the final **Check test result** step because the runner exits non-zero when WPT failures remain. In other words, the current blocker is **test coverage/compliance**, not artifact generation.

### 1.3 Failure category breakdown

From `tests/wpt-results/wpt-root-cause-analysis.txt`, the latest `WPT Tests` workflow summary/artifacts, and `tests/wpt-results/wpt-results.json`:

- **1,976 `PixelMismatch`** failures
  - 906 **`MissingContent`**
  - 839 **`MinorDiff`**
  - 165 **`LayoutShift`**
  - 55 **`ColorShift`**
  - 11 **`SubpixelAntiAliasing`**
- **8 `Timeout`** failures
  - `css/css-grid/parsing/grid-template-columns-crash.html`
  - `css/css-overflow/scroll-markers/column-scroll-marker-007.html`
  - `css/css-overflow/scroll-markers/targeted-scroll-marker-selection-with-transition.tentative.html`
  - `css/css-overflow/scroll-markers/targeted-scroll-marker-selection.tentative.html`
  - `css/css-shapes/shape-outside/supported-shapes/circle/shape-outside-circle-030.html`
  - `css/css-shapes/shape-outside/supported-shapes/circle/shape-outside-circle-031.html`
  - `css/css-tables/height-distribution/percentage-sizing-of-table-cell-children.html`
  - `css/css-tables/html5-table-formatting-3.html`

---

## 2. Highest-Value Failure Buckets

### 2.1 Top failing directories

The current failures are concentrated enough that the work should be done in buckets rather than test-by-test.

| Bucket | Failed tests | Main symptom |
|---|---:|---|
| `css/css-writing-modes` | 380 | Mostly `MissingContent`, with `forms` still the best near-pass slice to reopen selectively |
| `css/filter-effects` | 241 | Explicit deferred feature gap / unsupported rendering work |
| `css/css-variables` | 198 | Highest-count non-deferred near-pass bucket; most failures are already 95%+ matches |
| `css/selectors` | 197 | Mostly `MissingContent`, with localized invalidation and `selectors-4` leftovers still worth reopening |
| `css/css-view-transitions` | 126 | Explicit deferred feature gap / unsupported rendering work |
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
| `css/css-ui` | 470 | No generated reference images |
| `css/css-transforms` | 451 | No generated reference images |
| `css/css-grid` | 440 | No generated reference images outside the current timeout slice |
| `css/css-text` | 431 | No generated reference images |
| `css/css-overflow` | 241 | Mostly missing references plus the active timeout slice |

### 2.3 Highest-leverage near-pass slices still worth reopening

The broad directory view above is helpful for triage, but the current follow-up plan should optimize for **tests most likely to flip from fail to pass with localized fixes**. A direct scan of the latest `wpt-results.json` shows these slices still have unusually high match percentages:

| Slice | Failed tests | Why this is still high leverage | First focused command |
|---|---:|---|---|
| `css/css-variables` | 198 | 194 are already at **95%+** match and 182 are already at **99%+** match, so one shared custom-property/value-resolution fix could retire a large flat bucket quickly | `./scripts/run-wpt-tests.sh --subset "css/css-variables"` |
| `css/cssom-view` | 86 | 68 are already at **95%+** match; the remaining failures cluster around `scrollIntoView`, scroll APIs, and a few geometry outliers | `./scripts/run-wpt-tests.sh --subset "css/cssom-view"` |
| `css/css-viewport/zoom` | 64 | 48 are already at **95%+** match; the lowest-match cases still point to a narrow zoom scroll/iframe/SVG follow-up rather than a new broad subsystem | `./scripts/run-wpt-tests.sh --subset "css/css-viewport/zoom"` |
| `css/css-writing-modes/forms` | 81 | 73 are already at **95%+** match; this is still the best non-deferred slice inside the larger writing-modes bucket | `./scripts/run-wpt-tests.sh --subset "css/css-writing-modes/forms"` |
| `css/selectors/invalidation` | 83 | 80 are already at **95%+** match; the remaining failures are concentrated in a few `:has(...)` / `nth-*` invalidation paths | `./scripts/run-wpt-tests.sh --subset "css/selectors/invalidation"` |
| `css/selectors/selectors-4` | 29 | all 29 remaining failures are already above **99%** match, so they should stay in the short-term queue despite the broader selector bucket being mostly deferred | `./scripts/run-wpt-tests.sh --subset "css/selectors/selectors-4"` |

---

## 3. Likely Root Causes

### 3.1 `MissingContent` failures

These cluster heavily in:

- `css/css-writing-modes`
- `css/selectors`
- `css/css-variables`
- `css/css-viewport/zoom`
- `css/motion`
- `css/css-view-transitions`

This pattern strongly suggests missing or incomplete support for one or more of:

- zoom/viewport-aware layout calculations
- background clipping and paint order edge cases
- selector invalidation / dynamic style recomputation
- writing-mode-aware form control layout
- custom-property/value-resolution edge cases plus the remaining zoom-aware CSS consumers
- unsupported view-transition features

### 3.2 `MinorDiff` failures

These are still the best short-term opportunity for pass-rate improvement because they already render something close to the reference image. The biggest remaining clusters are:

- `css/css-writing-modes`
- `css/selectors`
- `css/css-variables`
- `css/cssom-view`

These should still be prioritized ahead of broad unsupported-feature suites because the fixes are more likely to be localized and testable.

### 3.3 `LayoutShift`, 0% matches, and timeout-heavy slices

The worst failures still include 0% matches in `css/css-values/*` (notably `vh-calc-support-pct.html`) and a handful of deterministic 30-second timeouts in `css-grid/parsing`, `css-overflow/scroll-markers`, `css-shapes/shape-outside`, and `css-tables`. Those deserve their own workstream because they point to execution-path or systemic value/layout bugs rather than paint-only differences.

### 3.4 Deferred unsupported-feature buckets

`css/filter-effects`, `css/css-view-transitions`, and the largest `MissingContent`-dominant portions of `css/css-writing-modes`, `css/selectors`, and `css/motion` should remain explicitly deferred unless a PR is clearly scoped to feature support rather than a near-pass cleanup.

### 3.5 Concrete follow-up themes inside the remaining non-deferred slices

The latest artifacts already narrow the next work down to a handful of repeatable themes:

- **`css/css-variables`**
  - flat custom-property substitution regressions (`variable-*`)
  - shorthand serialization / expansion edge cases (`vars-font-shorthand-001.html`, `vars-background-shorthand-001.html`)
  - pseudo-element and visited-state propagation (`variable-first-line.html`, `variable-first-letter.html`, `variable-reference-visited.html`)
  - lower-match keyword handling leftovers (`variable-css-wide-keywords.html`)
- **`css/cssom-view`**
  - fixed-position and visual viewport `scrollIntoView()` behavior (`scrollIntoView-fixed.html`, `visual-scrollIntoView-002.html`)
  - scroll API parity / alias behavior (`dom-element-scroll.html`, `elementScroll.html`)
  - shadow-tree / subframe scroll-parent and scroll-behavior edge cases (`scrollParent-shadow-tree.html`, `scroll-behavior-subframe-*`)
- **`css/css-viewport/zoom`**
  - remaining zoom-aware scroll spacing (`scroll-padding.html`, `scroll-margin.html`)
  - explicit zoom inheritance and nested browsing contexts (`css/css-viewport/zoom/explicit-inherit/column.html`, `iframe-zoom-nested.html`)
  - SVG and pseudo-image zoom consumers (`svg.html`, `svg-font-relative-units.html`, `zoom-pseudo-image.html`)
- **`css/css-writing-modes/forms`**
  - range control logical sizing (`input-range-zero-inline-size.html`)
  - select/listbox appearance and scrolling in vertical modes (`select-multiple-*`, `select-size-scrolling-and-sizing.optional.html`)
  - native button appearance computed-style leftovers (`button-appearance-native-computed-style.html`)
- **`css/selectors/invalidation` / `css/selectors/selectors-4`**
  - sibling-sensitive `:has(...)` invalidation (`is-pseudo-containing-sibling-relationship-in-has.html`, `has-with-nth-child.html`)
  - remaining `nth-*` tree refresh cases
  - the last `:lang(...)` / `details:open` rendering deltas in `selectors-4`

---

## 4. Prioritized Remediation Plan

### 4.1 Standard triage workflow for each follow-up issue or PR

Use this same loop for every bucket so follow-up work stays reproducible:

1. **Refresh the inputs**
   - Read `tests/wpt-results/wpt-results.json` first.
   - Confirm the latest `WPT Tests` run failed in the final **Check test result** step, not during artifact generation or setup.
2. **Classify the bucket before rerunning anything**
   - If the directory appears in `triage.deferredFeatureBuckets`, treat it as an explicit feature-gap/defer decision.
   - If it appears in `triage.referenceCoverage.priorityBuckets`, spend the next cycle on reference generation instead of comparing pass rates.
   - If the failure category is `Timeout`, keep it in the timeout-only workstream and rerun the smallest directory slice that reproduces it.
   - Otherwise, treat it as a non-deferred renderer/layout bug and pick the narrowest failing directory or subdirectory from `triage.topFailingDirectories`.
3. **Reproduce with a focused subset command**
   - Use `./scripts/run-wpt-tests.sh --subset "<bucket>"`.
   - Avoid broad `css` reruns unless you are refreshing the canonical artifact set on purpose.
4. **Make the smallest change that moves the bucket**
   - Prefer shared renderer/bridge fixes that retire a whole bucket.
   - If shared runner/reporting logic changes, add or update focused tests in `src/Broiler.Wpt.Tests/`.
5. **Validate against the same slice**
   - Re-run the exact same `--subset` command.
   - Re-check `wpt-results.json` to confirm the bucket count, timeout list, or skip reason distribution actually changed.
6. **Only then update roadmap status**
   - Mark a phase item complete only when the bucket is either fixed, intentionally deferred, or split into a narrower follow-up issue with a reproducible command.

## Phase 0 — Stabilize triage inputs

- [x] Keep `tests/wpt-results/wpt-results.json` as the canonical input for grouping and prioritization.
- [x] Standardize on a single results path name (`tests/wpt-results/`) across the committed artifacts, runner, and workflow.
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

## Phase 6 — Triage current non-deferred failures, near-pass leftovers, and timeout ergonomics

The roadmap now needs a fresh post-Phase-5 slice for the failures still surfacing in the latest artifact set.

- [x] Open a focused `css/css-variables` workstream, because it is now the largest non-deferred failure bucket and the current slice is still dominated by 95%+ near-pass regressions.
- [ ] Finish the most mature zoom/scroll follow-up buckets in `css/cssom-view` and `css/css-viewport/zoom` before opening broader new layout campaigns.
- [ ] Re-open only the near-pass slices inside the broader deferred `css/css-writing-modes` and `css/selectors` parents (`forms`, `invalidation`, `selectors-4`) and keep the rest explicitly deferred.
- [ ] Treat the 8 timeout cases as a single triage track with focused subset commands for `css/css-grid/parsing`, `css/css-overflow/scroll-markers`, `css/css-shapes/shape-outside`, and `css/css-tables`.
- [ ] Keep using the generated missing-reference priority list (`css/css-flexbox`, `css/css-ui/compute-kind-widget-generated`, `css/css-break`, `css/css-ui`, `css/css-transforms`) when reference generation time is available, rather than broad CSS reruns.

### 6.1 Priority order if the goal is maximum pass-rate gain per PR

1. **`css/css-variables`**
2. **`css/cssom-view`**
3. **`css/css-viewport/zoom`**
4. **`css/selectors/invalidation` + `css/selectors/selectors-4`**
5. **`css/css-writing-modes/forms`**
6. **Timeout-only slices that need non-visual debugging**
7. **Reference-generation backlog**

This order intentionally favors the buckets with the largest counts of already-near-passing tests instead of the broadest unsupported areas.

### 6.2 Detailed workstreams

#### Workstream A — `css/css-variables` near-pass sweep

**Why this comes first:**

- it is the highest-count non-deferred bucket
- 194 current failures are already at 95%+ match, so small shared fixes can still retire a wide slice quickly
- the failing filenames suggest a shared custom-property/value-resolution issue instead of hundreds of independent bugs

**Sub-slices to tackle in order:**

1. **Keyword / low-match cleanup**
   - `variable-css-wide-keywords.html`
   - goal: retire the small set of visibly larger outliers before sweeping the already-near-pass tail
2. **Shorthand/value application**
   - `vars-font-shorthand-001.html`
   - `vars-background-shorthand-001.html`
   - `variable-substitution-shorthands.html`
3. **Pseudo-element / inherited state propagation**
   - `variable-first-line.html`
   - `variable-first-letter.html`
   - `variable-reference-visited.html`
4. **General substitution edge cases**
   - `missing-closing-nested-fallback.html`
   - the remaining `variable-reference-*` cases

**Expected practice:** each PR should add focused tests only for the shared variable-resolution behavior it changes, not for the whole bucket.

#### Workstream B — finish the zoom-adjacent buckets already partly solved in Phase 3

**`css/cssom-view`**

- start with the lowest-match outliers:
  - `scrollIntoView-fixed.html`
  - `visual-scrollIntoView-002.html`
  - `dom-element-scroll.html`
  - `scrollParent-shadow-tree.html`
  - `scroll-behavior-subframe-root.html`
  - `scroll-behavior-subframe-window.html`
- then sweep the remaining `scrollIntoView*`, `scroll*`, `elementFromPoint*`, and `elementsFromPoint*` regressions while the geometry context is still fresh

**`css/css-viewport/zoom`**

- start with:
  - `scroll-padding.html`
  - `scroll-margin.html`
  - `explicit-inherit/column.html`
  - `iframe-zoom-nested.html`
  - `svg.html`
  - `svg-font-relative-units.html`
  - `zoom-pseudo-image.html`
- keep this work paired with focused WPT/bridge tests because the remaining failures are narrow but cross-cutting

#### Workstream C — selectively reopen the good slices inside deferred parent buckets

Do **not** reopen the whole `css/css-writing-modes` or `css/selectors` buckets. Only reopen the slices that still look like near-pass wins:

- `css/css-writing-modes/forms`
  - `input-range-zero-inline-size.html`
  - `button-appearance-native-computed-style.html`
  - `select-multiple-*`
  - `select-size-scrolling-and-sizing.optional.html`
- `css/selectors/invalidation`
  - `is-pseudo-containing-sibling-relationship-in-has.html`
  - `has-with-nth-child.html`
  - the remaining `nth-*` invalidation updates
- `css/selectors/selectors-4`
  - the remaining `details-open-pseudo-*`
  - the remaining `lang-*`

**Working rule:** if a change starts touching unrelated `MissingContent`-heavy selector or writing-mode suites, stop and defer that broader work instead of expanding the PR.

#### Workstream D — timeout-only debugging track

Treat these as execution-path or pathological-layout issues, not as ordinary rendering diffs:

| Timeout path | Likely subsystem | First repro command |
|---|---|---|
| `css/css-grid/parsing/grid-template-columns-crash.html` | grid parser / layout recursion / crash-to-timeout path | `./scripts/run-wpt-tests.sh --subset "css/css-grid/parsing"` |
| `css/css-overflow/scroll-markers/*` | scroll marker selection / transition interaction | `./scripts/run-wpt-tests.sh --subset "css/css-overflow/scroll-markers"` |
| `css/css-shapes/shape-outside/supported-shapes/circle/*` | float/shape layout convergence | `./scripts/run-wpt-tests.sh --subset "css/css-shapes/shape-outside"` |
| `css/css-tables/height-distribution/percentage-sizing-of-table-cell-children.html` | percentage table sizing / height distribution loops | `./scripts/run-wpt-tests.sh --subset "css/css-tables/height-distribution"` |
| `css/css-tables/html5-table-formatting-3.html` | table formatting / row-group layout interaction | `./scripts/run-wpt-tests.sh --subset "css/css-tables"` |

**Working rule:** do not mix timeout fixes with broad visual cleanups in the same PR unless the same localized bug clearly causes both.

#### Workstream E — reference-generation backlog

This does not directly reduce the current failure count, but it is still important because it determines which buckets can be measured honestly afterward.

- keep using:
  - `css/css-flexbox`
  - `css/css-ui/compute-kind-widget-generated`
  - `css/css-break`
  - `css/css-ui`
  - `css/css-transforms`
- treat these as separate throughput work, not as substitutes for failure reduction in the non-deferred buckets above

**Timeout subset commands to keep handy:**

- `./scripts/run-wpt-tests.sh --subset "css/css-grid/parsing"`
- `./scripts/run-wpt-tests.sh --subset "css/css-overflow/scroll-markers"`
- `./scripts/run-wpt-tests.sh --subset "css/css-shapes/shape-outside"`
- `./scripts/run-wpt-tests.sh --subset "css/css-tables"`

**Why this matters:** the roadmap already covers the earlier crash, near-pass, and reporting work; this phase keeps the planning document aligned with the failures still blocking the latest `WPT Tests` run.

---

## 5. Suggested Runner / CLI Improvements

The first four items below are already implemented in the runner/reporting stack. Keep the remaining items as follow-up tooling work if direct test fixes remain too expensive.

### 5.1 Add bucket summaries directly to the runner output

Add a top-N summary for:

- failing directories
- skipped directories
- mismatch subcategories
- lowest-match tests

This information already exists in `wpt-results.json`; surfacing it directly in the CLI would remove a lot of manual post-processing.

**Status:** done in `Broiler.Wpt`; the current runner already emits top failing/skipped buckets, mismatch subcategories, and lowest-match tests into the JSON-backed triage output.

### 5.2 Emit a roadmap-friendly summary file

Generate a small Markdown summary alongside the JSON report containing:

- current totals
- top failing buckets
- top skipped buckets
- non-pixel/rendering exceptions
- suggested next subset commands

**Status:** done; the workflow now publishes a Markdown triage summary alongside the JSON report.

### 5.3 Distinguish unsupported-feature skips from missing-reference skips

Right now the largest skip buckets mostly read as “No reference image.” A separate machine-readable reason field would make backlog accounting much easier.

**Status:** done; `wpt-results.json` now separates missing-reference skips from other skip reasons and ranks the highest-value missing-reference directories.

### 5.4 Standardize the output directory name

Choose either `tests/wpt-results/` or `tests/wpt/results/` and use it consistently across:

- the repository artifacts
- `scripts/run-wpt-tests.sh`
- `.github/workflows/wpt-tests.yml`

This is a small change, but it removes unnecessary ambiguity when people look for the latest reports.

**Status:** done; the repo, script, and workflow all use `tests/wpt-results/`.

### 5.5 Surface timeout summaries as first-class triage output

The latest run still required reading `wpt-results.json`, `wpt-root-cause-analysis.txt`, or the raw job log to recover the full set of 8 timeout paths. Extend the generated Markdown/console summary so it shows the complete timeout list plus suggested `--subset` commands for the affected directories.

**Status:** done; `Broiler.Wpt` now emits dedicated timeout sections in console, JSON, and Markdown output, including the full timeout path list and focused subset commands for each affected directory.

### 5.6 Support incremental reruns from the previous JSON report

If the remaining buckets are too expensive to attack via repeated broad subset runs, add a runner/CLI mode that reruns only the previous failure or timeout set from `tests/wpt-results/wpt-results.json`. That would make timeout and bucket triage faster without waiting for another full CSS pass.

**Status:** done; `Broiler.Wpt` now accepts `--rerun-json <PATH>` plus `--rerun-kind failures|timeouts`, filters the discovered test set against the previous JSON report, and writes relative test paths into new reports so follow-up reruns stay reproducible within the current checkout.

---

## 6. Recommended Execution Order for Follow-up PRs

1. **Tackle `css/css-variables` first, starting with the low-match keyword/pseudo-element leftovers and shorthand/value-application cases**
2. **Finish the `css/cssom-view` and `css/css-viewport/zoom` leftovers while the Phase 3 context is still fresh**
3. **Re-open only `css/selectors/invalidation`, `css/selectors/selectors-4`, and `css/css-writing-modes/forms` instead of the broader parent buckets**
4. **Keep the timeout-only slices as a dedicated debugging track running in parallel**
5. **Improve reference coverage for `css/css-flexbox`, `css/css-ui/compute-kind-widget-generated`, `css/css-break`, `css/css-ui`, and `css/css-transforms`**
6. **Keep explicit unsupported suites (`css/css-view-transitions`, larger `filter-effects`, `css/motion`, and the broader MissingContent-heavy remainders) as separate backlog items**
7. **If direct fixes are too slow, prioritize timeout summaries and incremental rerun support in the runner/CLI**

---

## 7. Validation Strategy

For each bucket-specific PR:

- run the smallest relevant `--subset` command through `scripts/run-wpt-tests.sh`
- add or update focused tests in `src/Broiler.Wpt.Tests/` when the fix is in shared runner logic
- avoid using the full CSS run as the development loop
- use the JSON report to confirm the bucket count actually moved
- when the roadmap claims a slice is a near-pass target, capture the before/after match bands or representative low-match tests from `wpt-results.json` so the PR proves it retired the intended slice rather than shifting failures around

---

## 8. Definition of Success

This roadmap should be considered successful when:

- the current timeout cases are either fixed, explicitly deferred, or trivially reproducible via focused subset commands
- the top non-deferred failure buckets are materially reduced
- value/layout bugs are split into explicit workstreams instead of mixed into paint-only failures
- skip counts are tracked separately from renderer failures and pass-rate comparisons stay tied to consistent reference coverage
- the runner/CLI surfaces enough bucket-level information that new WPT failures can be triaged without custom one-off scripts or raw-log scraping
