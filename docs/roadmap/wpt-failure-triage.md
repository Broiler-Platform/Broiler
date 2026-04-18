# Roadmap: WPT Failure Reduction and Runner Triage

> **Status**: Active — created 2026-04-18  
> **Scope**: Investigate the failures captured in `tests/wpt-results/` and define the smallest practical plan for reducing them over time.

## Implementation update — 2026-04-18

- `Broiler.Wpt` now emits bucket summaries in console output, machine-readable skip reasons in `wpt-results.json`, and a triage-focused Markdown report via `--markdown-output`.
- `scripts/run-wpt-tests.sh` and `.github/workflows/wpt-tests.yml` now standardize on `tests/wpt-results/`.
- Phase 1 has started: the `background-clip-006.html` null-reference no longer reproduces in targeted local WPT runner coverage, and the regression is now guarded by a focused test in `src/Broiler.Wpt.Tests/`.
- Phase 2 has started: the `background-size` vector bucket now covers the previously failing tall `cover` + `viewBox` cases in focused tests, and the targeted subset repro passes locally again.
- Phase 2 has expanded again: the tall `contain` + `viewBox` vector repro now matches the in-repo WPT reference image in focused local coverage, so the near-pass `background-size` workstream now guards both the tall `cover` and tall `contain` cases.
- Phase 2 has widened again: the additional tall `viewBox` vector variants with omitted and percentage root dimensions now reproduce at 100% against the in-repo reference PNGs, so the focused guard rails cover the next smallest tall `contain`/`cover` subset instead of just the fully explicit SVG dimensions.
- Phase 2 has widened again: the next near-pass `background-size` subset (`wide--12px-auto--*`) now resolves percentage children against a fully injected raster viewport when an SVG root is missing either intrinsic dimension, and focused local coverage now guards both the non-`viewBox` and `viewBox` partial-dimension variants.
- Phase 2 has widened again: the adjacent `wide--auto-32px--*` vector subset now reproduces cleanly against the in-repo reference PNGs in focused local coverage as well, so the roadmap guard rails now cover both wide `auto`-height and wide fixed-height partial-dimension SVG cases.
- Phase 2 has widened again: the adjacent `wide--contain--nonpercent-width-nonpercent-height-viewbox.html` repro now matches the in-repo reference PNG in focused local coverage as well, so the next smallest wide `contain` + `viewBox` near-pass case is now guarded too.
- The `background-clip*` subset has now been rerun against the in-repo WPT corpus; the raw subset still fails broadly on full-page visual noise, so guard rails now focus on the reproducible box-model cases (`border-box`, `padding-box`, `content-box`, size/position/radius variants, and `border-area` corner-shape) instead of the instruction text around them.
- **Deviation from the original proposal:** the roadmap-friendly Markdown file is generated directly by `Broiler.Wpt` instead of a separate post-processing step so the same logic is shared by local runs and CI.
- **Current blocker:** the hard crash is fixed, but the wider `background-clip` bucket still contains visual mismatches that belong to the next near-pass remediation steps rather than this crash-only fix.
- **Current near-pass focus:** continue harvesting the remaining `background-clip` and `background-size` cases with the smallest reproducible `--subset` commands rather than broad CSS reruns.
- **Secondary blocker:** full-solution validation still hits an unrelated compile failure in `Broiler.HTML.WPF/Adapters/GraphicsAdapter.cs` (`DrawGradientString` override missing), so WPT triage work should continue using targeted `Broiler.Wpt` validation until that project is repaired.
- Related WPT bucket issues: #956 (`background-clip` failures), #958 (`css-background-clip` follow-up), #962 (`background-size` vector cases).

---

## 1. Current Snapshot

This roadmap is based on the latest committed WPT artifacts in `tests/wpt-results/` and the latest `WPT Tests` workflow run (`run_number: 75`, completed 2026-04-18).

### 1.1 Result totals

From `tests/wpt-results/wpt-summary.txt`:

- **Total**: 24,900
- **Passed**: 2,389
- **Failed**: 2,178
- **Skipped**: 20,333

### 1.2 What failed in CI

The `WPT Tests` workflow is successfully producing the summary, root-cause analysis, and JSON artifacts. The workflow fails in the final **Check test result** step because the runner exits non-zero when WPT failures remain. In other words, the current blocker is **test coverage/compliance**, not artifact generation.

### 1.3 Failure category breakdown

From `tests/wpt-results/wpt-root-cause-analysis.txt` and `tests/wpt-results/wpt-results.json`:

- **2,177 `PixelMismatch`** failures
  - 998 **`MissingContent`**
  - 905 **`MinorDiff`**
  - 195 **`LayoutShift`**
  - 68 **`ColorShift`**
  - 11 **`SubpixelAntiAliasing`**
- **1 `RenderingError`** failure
  - `css/css-backgrounds/background-clip-006.html`
  - message: `Rendering failed: Object reference not set to an instance of an object.`

---

## 2. Highest-Value Failure Buckets

### 2.1 Top failing directories

The current failures are concentrated enough that the work should be done in buckets rather than test-by-test.

| Bucket | Failed tests | Main symptom |
|---|---:|---|
| `css/selectors/invalidation` | 87 | Mostly `MinorDiff` / `LayoutShift` |
| `css/css-writing-modes/forms` | 78 | Mostly `MinorDiff` / some `MissingContent` |
| `css/css-viewport/zoom` | 74 | Mostly `MissingContent` |
| `css/css-backgrounds/background-clip` | 45 | `MissingContent`, plus 1 hard rendering error |
| `css/css-backgrounds/background-size` | 39 | Mostly `MinorDiff` / `ColorShift` |
| `css/selectors/selectors-4` | 29 | Mostly `MinorDiff` |
| `css/css-values/urls` | 22 | Mostly `MissingContent` |
| `css/css-values/calc-size` | 20 | Mostly `MissingContent` |

### 2.2 Top skipped directories

The skip volume is even larger than the failure volume, so part of the roadmap must focus on reference coverage and triage ergonomics.

| Bucket | Skipped tests | Likely cause |
|---|---:|---|
| `css/css-ui/compute-kind-widget-generated` | 799 | No generated reference images |
| `css/css-grid/grid-lanes` | 580 | No generated reference images |
| `css/css-text/white-space` | 444 | No generated reference images |
| `css/css-grid/alignment` | 414 | No generated reference images |
| `css/css-text/i18n` | 354 | No generated reference images |
| `css/css-break/flexbox` | 317 | No generated reference images |
| `css/css-shapes/shape-outside` | 288 | No generated reference images |
| `css/css-sizing/aspect-ratio` | 283 | No generated reference images |

---

## 3. Likely Root Causes

### 3.1 `MissingContent` failures

These cluster heavily in:

- `css/css-viewport/zoom`
- `css/css-backgrounds/background-clip`
- `css/selectors/invalidation`
- `css/css-writing-modes/forms`
- `css/css-values/urls`
- `css/css-values/calc-size`
- `css/css-view-transitions/nested`

This pattern strongly suggests missing or incomplete support for one or more of:

- zoom/viewport-aware layout calculations
- background clipping and paint order edge cases
- selector invalidation / dynamic style recomputation
- writing-mode-aware form control layout
- CSS value resolution for `url()`, `calc()`, and related computed values
- unsupported view-transition features

### 3.2 `MinorDiff` failures

These are the best short-term opportunity for pass-rate improvement because they already render something close to the reference image. The biggest clusters are:

- `css/css-writing-modes/forms`
- `css/selectors/invalidation`
- `css/css-backgrounds/background-size`
- `css/selectors/selectors-4`

These should be prioritized ahead of broad unsupported-feature suites because the fixes are more likely to be localized and testable.

### 3.3 `LayoutShift` and 0% matches

The worst failures include several 0% matches in `css/css-values/*` (`calc-in-calc.html`, `calc-in-max.html`, media-query/calc variants, `vh-*` tests). Those deserve their own workstream because they point to systemic bugs in value resolution rather than paint-only differences.

### 3.4 Single hard rendering error

`css/css-backgrounds/background-clip-006.html` currently throws a null-reference exception. This should be fixed first because it is deterministic, localized, and likely blocks nearby tests from being debugged cleanly.

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

- [ ] `css/css-writing-modes/forms`
- [ ] `css/selectors/invalidation`
- [ ] `css/css-backgrounds/background-size`
- [ ] `css/selectors/selectors-4`

**Working rule:** prefer small renderer/layout fixes that retire dozens of visually-close failures at once.

## Phase 3 — Address systemic value/layout bugs

Target the clusters with many `LayoutShift` or 0% mismatches:

- [ ] `css/css-values/*` (`calc-*`, `vh-*`, media-query/value resolution)
- [ ] `css/css-viewport/zoom`
- [ ] `css/cssom-view/*zoom*`

**Working rule:** land these fixes only with focused regression tests because these bugs are likely cross-cutting.

## Phase 4 — Triage unsupported feature clusters separately

Do not mix these into the near-pass work. Track them as explicit feature gaps or deferred suites:

- [ ] `css/css-view-transitions/*`
- [ ] larger `filter-effects` failures
- [ ] other suites dominated by `MissingContent` rather than near-pass diffs

**Working rule:** if the platform feature is incomplete, document the gap instead of repeatedly re-triaging the same failures.

## Phase 5 — Expand reference coverage for skipped suites

The current skip count is too large to ignore.

- [ ] Prioritize reference generation for the largest skipped buckets first.
- [ ] Treat missing-reference skips separately from renderer failures in future progress tracking.
- [ ] Only compare pass-rate changes after the skip backlog is reduced for the same subset.

**Why this matters:** without better reference coverage, the failure rate understates the real work remaining.

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

---

## 6. Recommended Execution Order for Follow-up PRs

1. **Fix `background-clip-006.html` rendering exception**
2. **Finish harvesting `background-clip` + `background-size` near-pass wins**
3. **Tackle `selectors/invalidation` and `css-writing-modes/forms`**
4. **Open a dedicated value-resolution track for `calc-*`, `vh-*`, and zoom**
5. **Split unsupported feature suites (view transitions, larger filter-effects work) into separate backlog items**
6. **Improve reference coverage for the largest skipped buckets**

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

- the hard rendering error is eliminated
- the top near-pass buckets are materially reduced
- value/layout bugs are split into explicit workstreams instead of mixed into paint-only failures
- skip counts are tracked separately from renderer failures
- the runner/CLI surfaces enough bucket-level information that new WPT failures can be triaged without custom one-off scripts
