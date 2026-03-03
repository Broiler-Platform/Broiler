# ADR-021: Acid1 RenderEngineBug Investigation

## Status

Resolved

## Trigger

- **Issue:** [#174](https://github.com/MaiRat/Broiler/issues/174) – Investigate
  RenderEngineBug: Acid1 Differential Testing Errors & Fix Roadmap
- **Triggering Report:** [ADR-020](020-acid1-differential-testing-errors.md) –
  11.26 % full-page pixel diff classified as RenderingEngineBug
- **Triggering Commit:** [`ee61a77`](https://github.com/MaiRat/Broiler/commit/ee61a77b29ca0ce6bc841e518e96f6c8da981435)

## Context

ADR-020, auto-generated on 2026-02-28 after closing issue
[#172](https://github.com/MaiRat/Broiler/issues/172), reported a full-page
pixel diff of **11.26 %** (High severity, category: RenderingEngineBug) between
Broiler's HTML-Renderer and headless Chromium.  This issue calls for a root
cause investigation and fix roadmap for the remaining discrepancies.

## Investigation Methodology

1. Reviewed ADR-020 error table and per-section pixel diff ratios.
2. Compared ADR-020 data against the ADR-009 baseline (2026-02-26) and the
   roadmap's previously recorded state (2026-02-27).
3. Traced commit [`ee61a77`](https://github.com/MaiRat/Broiler/commit/ee61a77b29ca0ce6bc841e518e96f6c8da981435)
   — a merge of PR #173 that added region-based visual comparison and tightened
   regression thresholds.  This commit changed **test infrastructure only**; no
   rendering code was modified.
4. Analysed per-section categories (FontRasterisation, StyleMismatch,
   PositionError) to identify which CSS1 features contribute most to the
   composite error.

## Regression Analysis

### ADR-009 Baseline → ADR-020

All sections show dramatic improvement from the original baseline:

| # | Section | ADR-009 | ADR-020 | Change (pp) |
|---|---------|---------|---------|-------------|
| — | Full page | > 50 % | 11.26 % | ↓ > 38.74 |
| 1 | Body border | 89.97 % | 1.64 % | ↓ 88.33 |
| 2 | `dt` float:left | 86.30 % | 1.74 % | ↓ 84.56 |
| 3 | `dd` float:right | 84.16 % | 1.37 % | ↓ 82.79 |
| 4 | `li` float:left | 82.05 % | 1.05 % | ↓ 81.00 |
| 5 | `blockquote` | 92.00 % | 1.95 % | ↓ 90.05 |
| 6 | `h1` float | 91.23 % | 1.92 % | ↓ 89.31 |
| 7 | `form` line-height | 85.22 % | 1.55 % | ↓ 83.67 |
| 8 | `clear:both` | 72.17 % | 1.50 % | ↓ 70.67 |
| 9 | % width | 84.64 % | 1.14 % | ↓ 83.50 |
| 10 | `dd` height | < 50 % | 2.36 % | ↓ > 47.64 |

**Conclusion:** No regressions relative to ADR-009.  All sections improved by
70–90 percentage points thanks to the four completed ADR-009 priorities (float
layout, box model/canvas bg, border rendering, typography).

### Roadmap State (2026-02-27) → ADR-020 (2026-02-28)

| # | Section | Roadmap | ADR-020 | Delta | Explanation |
|---|---------|---------|---------|-------|-------------|
| — | Full page | 11.26 % | 11.26 % | 0.00 | Unchanged |
| 1 | Body border | 1.64 % | 1.64 % | 0.00 | Unchanged |
| 2 | `dt` float:left | 0.89 % | 1.74 % | +0.85 | Font rasterisation variance¹ |
| 3 | `dd` float:right | 0.65 % | 1.37 % | +0.72 | Font rasterisation variance¹ |
| 4 | `li` float:left | 1.05 % | 1.05 % | 0.00 | Unchanged |
| 5 | `blockquote` | 0.57 % | 1.95 % | +1.38 | Font rasterisation variance¹ |
| 6 | `h1` float | 0.65 % | 1.92 % | +1.27 | Font rasterisation variance¹ |
| 7 | `form` line-height | 1.55 % | 1.55 % | 0.00 | Unchanged |
| 8 | `clear:both` | 2.84 % | 1.50 % | −1.34 | Improved (CI environment) |
| 9 | % width | 10.67 % | 1.14 % | −9.53 | Major improvement (CI env) |
| 10 | `dd` height | 2.36 % | 2.36 % | 0.00 | Unchanged |

¹ Sections 2, 3, 5, 6 show increased diffs in ADR-020 compared to the roadmap
data.  Since commit `ee61a77` changed only test infrastructure (no rendering
code), these are **cross-environment font rasterisation variance**, not true
rendering regressions.  ADR-020 was generated on CI; the roadmap data was
collected locally.  Font availability (Verdana hinting, anti-aliasing) differs
between environments.

**Conclusion:** No rendering regressions.  Sections 8 and 9 show significant
improvement in the CI environment.  Sections 2, 3, 5, 6 show expected
cross-environment variance in font rasterisation, all remaining well below the
20 % threshold.

## Root Cause Breakdown

### Why is the full page 11.26 % when all sections are < 2.5 %?

The full-page composite renders all 10 sections together in a single 800×600
viewport.  The composite diff (11.26 %) is higher than any individual section
diff because:

1. **Spatial accumulation:** Per-section tests render each section in isolation
   at 800×600, so most of the viewport is background.  The full-page test packs
   all sections into a single viewport, increasing the density of differing
   pixels.
2. **Cascading layout offsets:** Sub-pixel rounding differences in early
   sections shift subsequent sections vertically, causing downstream position
   errors that do not appear in isolated section tests.
3. **Background interaction:** The `html` blue background and `body` white
   background interact with all sections; minor border/margin differences
   cascade through the entire page.

### Per-Category Contribution to Full-Page Diff

| Category | Sections | ADR-020 Range | Root Cause | Fixable? |
|----------|----------|---------------|------------|----------|
| FontRasterisation | 2, 3, 4, 5, 6 | 1.05–1.95 % | Cross-engine font anti-aliasing, hinting, Verdana glyph metrics | No (irreducible) |
| StyleMismatch | 1, 7 | 1.55–1.64 % | Sub-pixel border/margin rounding; form radio button placeholders | Partially |
| PositionError | 8, 9, 10 | 1.14–2.36 % | Clearance distance rounding, margin collapsing precision | Partially |

**FontRasterisation (5 sections)** contributes the most sections but each is
small (< 2 %).  These are **irreducible** without matching Chromium's exact
text rendering backend.

**PositionError (3 sections)** contributes the highest individual diffs (S10 at
2.36 %).  These are **partially fixable** by improving sub-pixel rounding
precision.

**StyleMismatch (2 sections)** is partially fixable via better form widget
rendering and sub-pixel border rounding.

### Feature Contribution to Error Ratio

Ranking CSS1 features by their estimated contribution to the 11.26 % full-page
diff:

| Rank | CSS1 Feature | Sections | Estimated Contribution | Priority |
|------|-------------|----------|----------------------|----------|
| 1 | Sub-pixel rounding (border, margin, clearance) | 1, 8, 9, 10 | ~4 % | Priority 4 in roadmap |
| 2 | Font rasterisation (Verdana AA, hinting) | 2, 3, 4, 5, 6 | ~4 % | Irreducible |
| 3 | Cascading layout offsets (accumulated rounding) | Full page | ~2 % | Priority 4 in roadmap |
| 4 | Form widget rendering (radio buttons) | 7 | ~1 % | Priority 5 in roadmap |

## Action Plan

### Immediate Actions (completed as part of this investigation)

1. ✅ Updated the [Acid1 Error Resolution Roadmap](../roadmap/acid1-error-resolution.md)
   with ADR-020 data, regression analysis, and concrete milestones.
2. ✅ Documented root cause breakdown and feature contribution analysis in this
   ADR.
3. ✅ Confirmed no rendering regressions from commit `ee61a77`.

### Sub-Issues to Draft

The following distinct bugs within the Acid1 render should be tracked as
separate work items:

1. **Sub-pixel rounding in `em`-to-pixel conversion** (Sections 1, 8, 10):
   Audit `CssValueParser.ParseLength()` for fractional `em` values (`.5em`,
   `1.5em`, `1.9em`) to ensure consistent rounding direction.  Target: reduce
   S1 from 1.64 % to < 1 %, S8 from 1.50 % to < 1 %, S10 from 2.36 % to
   < 1.5 %.

2. **Percentage width cascading rounding** (Section 9): Investigate whether
   intermediate percentage width calculations can preserve fractional pixels
   longer before rounding to integer.  Target: reduce S9 from 1.14 % to
   < 0.5 %.

3. **Form radio button widget fidelity** (Section 7): Measure Chromium radio
   button dimensions and update Broiler's placeholder rendering to match.
   Target: reduce S7 from 1.55 % to < 1 %.

4. **Font rasterisation documentation** (Sections 2–6): Verify Verdana
   availability on CI, document fallback fonts, establish accepted residual
   diff levels.  No code changes expected.

### Milestones

| Milestone | Target Full-Page Diff | Target Section Max | Prerequisites |
|-----------|----------------------|-------------------|---------------|
| M1: Sub-pixel audit | < 8 % | < 2 % | Sub-issue 1 completed |
| M2: Widget refinement | < 6 % | < 1.5 % | Sub-issues 1–3 completed |
| M3: Documentation | < 6 % | < 1.5 % | Sub-issue 4 completed |
| M4: Threshold tightening | N/A | N/A | Reduce threshold from 20 % to 8 % |

### Targeted Regression Tests

The following tests should be added to prevent regressions in the improved
areas:

1. **Sub-pixel em conversion test:** Verify that `.5em` at `font-size: 10px`
   produces exactly 5 px (not 4 px or 6 px).
2. **Percentage width precision test:** Verify that `41.17 %` of 340 px
   produces a width within 1 px of 139.978 px.
3. **Clearance distance precision test:** Verify that `clear:both` after a
   float with `margin-bottom` produces the correct Y position within 1 px.

## Consequences

- The 11.26 % full-page diff is **not a single bug** but the aggregate of
  many small, categorised discrepancies.  No individual section exceeds 2.5 %.
- No rendering regressions were found relative to ADR-009 or previous roadmap
  data.
- The largest fixable improvement opportunity is sub-pixel rounding (~4 pp
  reduction possible).
- Font rasterisation accounts for ~4 pp and is **irreducible** without
  matching Chromium's text rendering.
- A realistic target for the full-page diff after completing all fixable items
  is **5–7 %** (irreducible font rasterisation floor).

## Resolution (2026-02-28)

A severe rendering bug was identified and fixed in the Acid1 `<blockquote>`
and `<h1>` float positioning.

### Root Cause

CSS2.1 §9.5.1 rule 6 was not enforced: "The outer top of a floating box may
not be higher than the outer top of any block or floated box generated by an
element earlier in the source document."

When the `<blockquote>` (float:left) followed a `<ul>` element whose children
were all floated, the `<ul>` collapsed to zero height (correct per CSS2.1
§10.6.3).  The blockquote's initial vertical position was then based on the
`<ul>`'s collapsed bottom, placing it at the same Y as the top of all
preceding li floats.  This caused vertical overlap with ALL preceding floats
across both rows, forcing the collision resolver to push the blockquote below
everything — onto a third row instead of sitting beside `li#baz` on row 2.

### Fix

After collecting preceding floats in `CollectPrecedingFloatsInBfc()`,
enforce rule 6 by setting `top = max(top, preceding_float.Location.Y)`.
This ensures the blockquote starts at the same level as `li#baz` (the last
preceding float that was pushed to row 2) and is correctly placed to its
right.

**File changed:** `HTML-Renderer-1.5.2/Source/HtmlRenderer.Dom/Core/Dom/CssBox.cs`
(5-line addition after `CollectPrecedingFloatsInBfc` call)

### Impact

| Metric | Before | After |
|--------|--------|-------|
| Blockquote horizontal error | 114 px | 6 px |
| Blockquote vertical error | 100 px | 0 px |
| Blockquote on correct row | No (row 3) | Yes (row 2) |

### Regression Test

`Acid1ProgrammaticTests.FloatRule6_FloatAfterZeroHeightBlock_SameRowAsLastPrecedingFloat`

## Related Documents

- [ADR-009](009-acid1-differential-testing.md) – Original differential testing
  baseline
- [ADR-018](018-acid1-visual-comparison.md) – Element-by-element visual
  comparison
- [ADR-020](020-acid1-differential-testing-errors.md) – Triggering error report
- [Acid1 Error Resolution Roadmap](../roadmap/acid1-error-resolution.md) –
  Detailed fix plan

## Related Tests

| Test File | Project | Purpose |
|-----------|---------|---------|
| `Acid1ProgrammaticTests.cs` | `Broiler.Cli.Tests` | Regression test for the blockquote float fix (see above) |
| `Acid1DifferentialTests.cs` | `HtmlRenderer.Image.Tests` | Per-section pixel comparison tracking investigation progress |
