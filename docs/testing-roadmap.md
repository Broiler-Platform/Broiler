# Testing Roadmap – Automated Multi-Layer Test Suite

> Style → Layout → Paint → Raster

This roadmap describes how to evolve from the current test suite to a robust,
multi-layer, partially auto-generated testing system. It is designed as a staged
adoption plan with clear incremental milestones.

Related documents:
- [Testing Current State (Phase 0 Audit)](testing-current-state.md)
- [Testing Architecture (IR Boundaries)](testing-architecture.md)
- [Testing Guide](testing-guide.md)
- [Architecture Separation](architecture-separation.md)
- [Architecture Roadmap](architecture-roadmap.md)

---

## Phase 0 – Audit Current Testing State ✅

**Status:** Complete

**Goal:** Document what tests exist, what can be dumped, and where the biggest
blind spots are.

**Deliverable:** [`docs/testing-current-state.md`](testing-current-state.md)

### Summary of Findings

- [x] Catalogued ~413 tests across two projects (`Broiler.App.Tests`,
      `Broiler.Cli.Tests`)
- [x] Identified four test categories: unit, IR/pipeline, pixel analysis,
      live-site capture
- [x] Documented dump capabilities: DisplayList has JSON support; Fragment and
      ComputedStyle do not yet
- [x] Identified blind spots: margin collapse, inline layout, table layout,
      paint order, clip nesting, DPI determinism

### Quick Wins Identified

| Quick Win | Effort | Impact |
|-----------|--------|--------|
| Add `Fragment.ToJson()` convenience method | 1–2 hours | Unblocks Phase 2 golden tests |
| Add `ComputedStyle.ToJson()` convenience method | 1 hour | Enables style-level snapshot testing |
| Add NaN/Inf invariant checks to existing layout tests | 30 min | Catches silent geometry corruption |
| Add clip balance assertion to DisplayList | 30 min | Catches unmatched Clip/Restore pairs |

### Risks

- **Low:** Audit is documentation-only; no code changes required.

---

## Phase 1 – Define Testable IR Boundaries ✅

**Status:** Complete

**Goal:** Define explicit testable interfaces for ComputedStyle, Fragment tree,
DisplayList, and the raster layer.

**Deliverable:** [`docs/testing-architecture.md`](testing-architecture.md)

### Summary

- [x] Defined inputs, outputs, and testable structure for all four IR layers
- [x] Specified 24 invariants across all layers
- [x] Proposed JSON dump format for Fragment tree
- [x] Documented existing JSON serialisation support for DisplayList
- [x] Identified missing convenience dump methods

### Architecture-Dependent Steps

- IR types (`ComputedStyle`, `Fragment`, `DisplayList`) are sealed records in
  `HtmlRenderer.Core/Core/IR/` – these are stable and suitable for snapshot
  testing.
- Builders (`ComputedStyleBuilder`, `FragmentTreeBuilder`, `PaintWalker`) are
  internal static classes in `HtmlRenderer.Orchestration/Core/IR/` – these are
  the functions under test.

### Risks

- **Low:** Boundary definitions are documentation; struct/interface additions
  are additive and non-breaking.

---

## Phase 2 – Layout-Level Deterministic Testing

**Status:** Complete

**Goal:** Move away from pure pixel tests for layout correctness by introducing
golden-file comparison of the Fragment tree.

### Prerequisites

- Phase 1 boundary definitions (✅ done)
- `FragmentTreeBuilder.Build()` producing stable output (✅ available)

### Tasks

- [x] **Implement Fragment JSON dump** – Add a `ToJson()` extension or static
      method that serialises a `Fragment` tree to deterministic JSON, including:
  - `x`, `y`, `width`, `height` (rounded to 2 decimal places for stability)
  - `margin`, `border`, `padding` (as `BoxEdges` sub-objects)
  - `stackLevel`, `createsStackingContext`
  - `lines[]` with `baseline`, `inlines[]` (text + geometry)
  - Recursive `children[]`
  - Exclude object references (`Style` back-ref, `FontHandle`, `ImageHandle`)

- [x] **Add golden layout tests** (5–10 cases):
  1. Single block element with explicit width/height
  2. Nested blocks with padding and border
  3. Two side-by-side left floats
  4. Left float with text wrap-around
  5. Float with `clear: both`
  6. Percentage width on nested element
  7. Inline elements on a single line
  8. Multiple lines with word wrap
  9. Margin collapse between siblings
  10. Block formatting context containing floats

- [x] **Implement layout invariant checker** – A reusable assertion helper that
      walks any `Fragment` tree and checks:
  - No NaN or Infinity in any coordinate/dimension
  - All `Width` and `Height` values are non-negative
  - Lines are ordered vertically (`Lines[i].Y ≤ Lines[i+1].Y`)
  - Block children stack vertically
  - Baseline is within line height

- [x] **Integrate invariant checker into existing tests** – Call the invariant
      checker after every layout operation in `IRTypesTests`.

### Deliverables

- `FragmentJsonDumper` utility (`HtmlRenderer.Core/Core/IR/FragmentJsonDumper.cs`)
- 10 golden `.json` files in `HtmlRenderer.Image.Tests/TestData/GoldenLayout/`
- `LayoutInvariantChecker` assertion helper (`HtmlRenderer.Image.Tests/LayoutInvariantChecker.cs`)
- Updated `IRTypesTests` with invariant assertions
- `GoldenLayoutTests` test class with 10 golden-file layout tests

### Effort Estimate

| Task | Effort |
|------|--------|
| Fragment JSON dump | 2–4 hours |
| Golden layout tests | 4–8 hours |
| Invariant checker | 2–3 hours |
| Integration | 1–2 hours |
| **Total** | **9–17 hours** |

### Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Fragment tree output is non-deterministic (floating-point rounding) | Medium | Round coordinates to 2 decimal places; use tolerance in comparison |
| Golden files break frequently during development | Medium | Store golden files per feature branch; update tooling for re-baselining |
| `CssBox` layout still has known bugs (margin collapse) | High | Document known failures; mark golden tests as `[Trait("Category", "KnownFailure")]` |

---

## Phase 3 – Property-Based / Generative Layout Testing

**Status:** Complete

**Goal:** Automatically generate layout stress cases to find edge-case bugs.

### Prerequisites

- Phase 2 invariant checker (required) ✅
- Phase 2 Fragment JSON dump (required for failure reporting) ✅

### Tasks

- [x] **Implement minimal HTML/CSS generator** targeting:
  - `float: left | right | none`
  - `clear: left | right | both | none`
  - `width` / `height` (px, %, auto)
  - `padding` / `border` / `margin` (px values)
  - `display: block | inline | inline-block`
  - Nesting depth: 1–4 levels
  - Child count: 1–6 per parent

- [x] **Build fuzz runner** that:
  1. Generates N random HTML/CSS documents (default: 1000)
  2. Parses and lays out each document
  3. Builds Fragment tree via `FragmentTreeBuilder.Build()`
  4. Runs invariant checker on the Fragment tree
  5. On failure: saves HTML input + Fragment JSON + violation description

- [x] **Implement failure minimizer** (basic delta reduction):
  1. Remove one child element at a time
  2. Re-run layout + invariant check
  3. If violation persists, keep removal
  4. Repeat until minimal repro

- [x] **Add CLI integration** for manual fuzz runs:
  ```bash
  dotnet run --project src/Broiler.Cli -- --fuzz-layout --count 1000
  ```

### Deliverables

- `HtmlCssGenerator` class
- `LayoutFuzzRunner` test class
- `DeltaMinimizer` utility
- Documentation in `docs/testing-guide.md`

### Effort Estimate

| Task | Effort |
|------|--------|
| HTML/CSS generator | 4–6 hours |
| Fuzz runner | 3–4 hours |
| Delta minimizer | 4–6 hours |
| CLI integration | 2–3 hours |
| **Total** | **13–19 hours** |

### Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| High false-positive rate from invariant checker | Medium | Tune invariants iteratively; allow spec-permitted violations (e.g., negative margins) |
| Generator produces invalid HTML | Low | Use well-formed template approach; validate structure before layout |
| Fuzz runs are slow (> 1 min) | Medium | Limit count in CI (100); full runs nightly |

---

## Phase 4 – Paint / DisplayList Testing

**Status:** Complete

**Goal:** Separate paint bugs from layout bugs by testing the DisplayList
output independently.

### Prerequisites

- PaintWalker producing DisplayList from Fragment tree (✅ available)
- DisplayList JSON serialisation (✅ available via `[JsonDerivedType]`)

### Tasks

- [x] **Add `DisplayList.ToJson()` convenience method** – Wrapper around
      `System.Text.Json.JsonSerializer.Serialize()` with deterministic settings
      (ordered properties, indented, no trailing commas).

- [x] **Add golden DisplayList tests** (5–10 cases):
  1. Single coloured `<div>` → `FillRectItem` only
  2. `<div>` with border → `FillRectItem` + `DrawBorderItem`
  3. Text paragraph → `DrawTextItem` entries
  4. Nested `<div>` with `overflow: hidden` → `ClipItem` / `RestoreItem` pair
  5. Image element → `DrawImageItem`
  6. Element with `opacity` → `OpacityItem`
  7. Underlined text → `DrawLineItem` for text-decoration
  8. Stacking context ordering → items ordered by `StackLevel`
  9. Float with background → correct paint-order position
  10. Border with radius → `DrawBorderItem` with corner radii

- [x] **Implement paint invariant checker**:
  - Every `ClipItem` has a matching `RestoreItem` (balanced nesting)
  - No negative `Width` or `Height` on rect-based items
  - All coordinates are finite (no NaN/Inf)
  - `DrawTextItem` has non-empty `Text` and valid `FontSize`
  - Deterministic ordering (same Fragment input → same DisplayList)

- [x] **Integrate with existing `RenderingStagesTests`** – Add invariant
      assertions to the existing Painter tests.

### Deliverables

- `DisplayList.ToJson()` convenience method
- 5–10 golden `.json` files for paint-level tests
- `PaintInvariantChecker` assertion helper
- Updated `RenderingStagesTests`

### Effort Estimate

| Task | Effort |
|------|--------|
| DisplayList.ToJson() | 1–2 hours |
| Golden paint tests | 4–8 hours |
| Paint invariant checker | 2–3 hours |
| Integration | 1–2 hours |
| **Total** | **8–15 hours** |

### Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| DisplayList item properties change during development | Medium | Version golden files; re-baseline tooling |
| Font handles are platform-specific (not serialisable) | High | Exclude `FontHandle` from JSON; test font metadata (family, size, weight) only |
| Floating-point coordinates in paint items | Medium | Round coordinates in JSON dump for stable comparison |

---

## Phase 5 – Pixel Regression (End-to-End)

**Status:** Complete

**Goal:** Controlled pixel testing with deterministic rendering and baseline
image diffing.

### Prerequisites

- Deterministic raster mode (fixed DPR, fonts, no AA randomness)
- CLI image capture (✅ available)

### Tasks

- [x] **Implement deterministic render mode**:
  - Fixed DPI (96 or 72)
  - Fixed font fallback chain (bundled test fonts)
  - Disabled anti-aliasing or deterministic AA
  - Fixed viewport size (e.g., 800×600)

- [x] **Build screenshot + diff mechanism**:
  - Render HTML → PNG at fixed settings
  - Compare against baseline PNG using per-pixel diff
  - Configurable threshold (default: 0.1% pixel difference)
  - Generate diff image highlighting changed pixels

- [x] **Integrate WPT reftest subset**:
  - Select 20–50 WPT reftests covering supported CSS features
  - Render both test and reference page
  - Compare pixel output
  - Track pass/fail rates over time

- [x] **Failure classification** – When a pixel test fails:
  1. Check if Fragment tree changed → **layout diff**
  2. Check if DisplayList changed → **paint diff**
  3. If neither changed → **pure raster diff**

### Deliverables

- `DeterministicRenderMode` configuration
- `PixelDiffRunner` test utility
- Baseline image storage (Git LFS or separate artefact store)
- WPT subset test class
- CI integration with artefact upload on failure

### Effort Estimate

| Task | Effort |
|------|--------|
| Deterministic render mode | 8–16 hours |
| Pixel diff mechanism | 4–8 hours |
| WPT subset integration | 8–16 hours |
| Failure classification | 4–6 hours |
| CI integration | 4–8 hours |
| **Total** | **28–54 hours** |

### Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Cross-platform rendering differences (Windows vs. Linux CI) | High | Pin CI to single OS or accept per-platform baselines |
| Font rendering varies by OS/version | High | Bundle test fonts; use bitmap font for critical tests |
| Baseline images inflate repo size | Medium | Use Git LFS or external artefact storage |
| WPT tests expose many failures | High | Start with passing subset; track regression over time |

---

## Phase 6 – Differential Testing

**Status:** Complete

**Goal:** Compare rendering output against a reference browser automatically.

### Prerequisites

- Phase 5 pixel regression infrastructure
- Headless Chromium or equivalent available in CI

### Tasks

- [x] **Set up reference engine rendering**:
  - Use headless Chromium (via Playwright or Puppeteer) to render test HTML
  - Capture screenshot at identical viewport/DPI settings
  - Save as reference baseline

- [x] **Build comparison harness**:
  - Render same HTML in Broiler engine
  - Pixel-diff against Chromium output
  - Configurable tolerance (rendering engines will differ in subtle ways)

- [x] **Optional layout metric comparison**:
  - Extract `getBoundingClientRect()` for key elements from Chromium
  - Compare against Fragment tree geometry from Broiler
  - Flag significant dimensional differences

- [x] **Failure diagnosis**:
  - On pixel diff: dump Broiler's Fragment JSON + DisplayList JSON
  - Classify as layout, paint, or raster difference
  - Generate side-by-side comparison report

### Deliverables

- Differential test harness (Broiler vs. Chromium)
- Failure layer classification
- Comparison report generator
- CI integration (nightly, not per-commit)

### Implementation Details

| Component | File | Description |
|-----------|------|-------------|
| `ChromiumRenderer` | `HtmlRenderer.Image.Tests/ChromiumRenderer.cs` | Renders HTML via Playwright headless Chromium, captures screenshots as SKBitmap, extracts `getBoundingClientRect()` |
| `DifferentialTestConfig` | `HtmlRenderer.Image.Tests/DifferentialTestConfig.cs` | Configuration: 5% pixel threshold, 15 colour tolerance, 2px layout tolerance |
| `DifferentialTestRunner` | `HtmlRenderer.Image.Tests/DifferentialTestRunner.cs` | Orchestrates Broiler vs. Chromium rendering, pixel-diff, failure classification |
| `DifferentialTestReport` | `HtmlRenderer.Image.Tests/DifferentialTestReport.cs` | Side-by-side HTML report with Broiler/Chromium/diff PNGs |
| `DifferentialTests` | `HtmlRenderer.Image.Tests/DifferentialTests.cs` | 10 xUnit tests: 8 structural, 1 layout metric, 1 report generation |
| Nightly CI | `.github/workflows/nightly-differential.yml` | Scheduled at 02:00 UTC, installs Chromium, uploads reports |

### Effort Estimate

| Task | Effort |
|------|--------|
| Reference engine setup | 4–8 hours |
| Comparison harness | 8–12 hours |
| Layout metric extraction | 4–8 hours |
| Failure diagnosis + reporting | 8–12 hours |
| CI integration | 4–6 hours |
| **Total** | **28–46 hours** |

### Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Chromium rendering differs from CSS spec (not always correct) | Low | Use WPT-passing subset; manual triage of differences |
| High noise from expected rendering differences | High | Generous tolerance; focus on structural layout differences |
| CI environment needs Chromium installation | Medium | Use containerised CI; Playwright handles browser management |
| Differential tests are slow | High | Run nightly, not per-commit; parallelise rendering |

---

## Non-Functional Requirements

| Requirement | Details |
|-------------|---------|
| **Deterministic** | All IR dumps (Fragment JSON, DisplayList JSON, pixel output) must produce identical output for identical input |
| **Headless** | All tests run without a display server; Broiler.Cli already supports headless mode |
| **CI-friendly** | No flaky tests; transient network failures handled by retry (already implemented in `HeiseCaptureTests`); pixel thresholds avoid false positives |
| **Layer separation** | Test failures clearly attributed to style, layout, paint, or raster layer |
| **Re-baselineable** | Golden files can be regenerated with a single command when intentional changes are made |

---

## Implementation Priority & Quick Wins

### Immediate (can implement now)

| Item | Phase | Effort | Impact |
|------|-------|--------|--------|
| Layout invariant checker (NaN/Inf/negative) | 2 | 2–3 h | Catches silent corruption in all layout tests |
| Clip balance assertion for DisplayList | 4 | 30 min | Detects unmatched Clip/Restore |
| `DisplayList.ToJson()` convenience wrapper | 4 | 1–2 h | Unblocks paint-level golden tests |
| `Fragment.ToJson()` dump method | 2 | 2–4 h | Unblocks layout-level golden tests |

### Short-term (1–2 sprints)

| Item | Phase | Effort | Impact |
|------|-------|--------|--------|
| 5–10 golden layout tests | 2 | 4–8 h | First snapshot coverage for layout correctness |
| 5–10 golden paint tests | 4 | 4–8 h | First snapshot coverage for paint correctness |
| `ComputedStyle.ToJson()` for debugging | 1 | 1 h | Better diagnostics |

### Medium-term (1–2 months)

| Item | Phase | Effort | Impact |
|------|-------|--------|--------|
| Layout fuzz generator | 3 | 13–19 h | Discovers edge-case layout bugs automatically |
| Deterministic render mode | 5 | 8–16 h | Prerequisite for reliable pixel regression |
| Pixel diff runner | 5 | 4–8 h | End-to-end regression detection |

### Long-term (3+ months)

| Item | Phase | Effort | Impact |
|------|-------|--------|--------|
| WPT reftest subset | 5 | 8–16 h | Industry-standard compliance tracking |
| Differential testing vs. Chromium | 6 | 28–46 h | Reference-based correctness validation |
| Failure layer classification | 5–6 | 4–6 h | Root-cause analysis automation |

---

## Stretch Goal – Engine Correctness Metrics

| Metric | Source | Target |
|--------|--------|--------|
| **Layout invariant violation rate** | Phase 2–3 fuzz runs | < 1% of generated cases |
| **Fuzz failure rate** | Phase 3 fuzz runner | Trending toward 0 over time |
| **Pixel regression count** | Phase 5 CI runs | 0 regressions per release |
| **Differential mismatch count** | Phase 6 nightly runs | Decreasing trend; triaged backlog |
| **Golden test pass rate** | Phases 2 + 4 | 100% of committed golden files pass |
| **WPT reftest pass rate** | Phase 5 | Track percentage; target > 80% for supported features |

These metrics should be tracked in CI and reported per build. A dashboard or
trend chart (e.g., via GitHub Actions summary) would provide visibility into
engine stability over time.

---

## Milestone Timeline

```
Month 1          Month 2          Month 3          Month 4+
─────────────────────────────────────────────────────────────
Phase 0 ✅       Phase 3 ✅       Phase 5 ✅       Phase 6 ✅
Phase 1 ✅       (fuzz gen)       (pixel regr.)    (differential)
Phase 2 ✅                        WPT subset
(golden layout)  Phase 4 ✅
                 (golden paint)
```

---

*This roadmap is a living document. Update it as phases are completed and new
priorities emerge.*
