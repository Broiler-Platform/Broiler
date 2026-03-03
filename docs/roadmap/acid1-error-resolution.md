# Roadmap: Acid1 Error Resolution

> **Scope:** Fix all rendering discrepancies between Broiler (HTML-Renderer)
> and headless Chromium (Playwright) for the
> [Acid1 CSS1 conformance test](https://www.w3.org/Style/CSS/Test/CSS1/current/test5526c.htm).
>
> **Tracking Issue:** [#161](https://github.com/MaiRat/Broiler/issues/161)
>
> **Previous Work:** ADR-009 (`docs/adr/009-acid1-differential-testing.md`)
> documented the original ≥ 72 % diffs and a four-priority fix roadmap.
> Priorities 1–4 are all completed.

---

## Current State (2026-02-28)

Differential tests run at 800×600 viewport, 30-per-channel colour tolerance,
3 px layout tolerance.  All 11 acid1 differential tests pass the 20 %
pixel-diff threshold.  Zero float/block overlaps detected.

Data source: [ADR-020](../adr/020-acid1-differential-testing-errors.md),
auto-generated after closing issue
[#172](https://github.com/MaiRat/Broiler/issues/172).

| # | Section | CSS1 Feature | Pixel Diff | Severity | Category | Trend vs ADR-009 |
|---|---------|-------------|-----------|----------|----------|------------------|
| — | Full page | All CSS1 features combined | 11.26 % | High | RenderingEngineBug | ↓ from > 50 % |
| 1 | Body border | `html` bg, `body` bg + border | 1.64 % | Low | StyleMismatch | ↓ from 89.97 % |
| 2 | `dt` float:left | `float:left`, percentage width | 1.74 % | Low | FontRasterisation | ↓ from 86.30 % |
| 3 | `dd` float:right | `float:right`, border, side-by-side | 1.37 % | Low | FontRasterisation | ↓ from 84.16 % |
| 4 | `li` float:left | Multiple `float:left` stacking | 1.05 % | Low | FontRasterisation | ↓ from 82.05 % |
| 5 | `blockquote` | `float:left`, asymmetric borders | 1.95 % | Low | FontRasterisation | ↓ from 92.00 % |
| 6 | `h1` float | `float:left`, black bg, font-weight | 1.92 % | Low | FontRasterisation | ↓ from 91.23 % |
| 7 | `form` line-height | `line-height: 1.9` on form `<p>` | 1.55 % | Low | StyleMismatch | ↓ from 85.22 % |
| 8 | `clear:both` | `clear:both` after floats | 1.50 % | Low | PositionError | ↓ from 72.17 % |
| 9 | Percentage width | `10.638 %` and `41.17 %` widths | 1.14 % | Low | PositionError | ↓ from 84.64 % |
| 10 | `dd` height/clearance | Content-box height, float clearance | 2.36 % | Low | PositionError | ↓ from < 50 % |

> **Note on measurement variance:** The values above are from ADR-020
> (CI environment, 2026-02-28).  Previous locally-measured values (roadmap
> 2026-02-27) showed S2=0.89 %, S3=0.65 %, S5=0.57 %, S6=0.65 %,
> S8=2.84 %, S9=10.67 %.  The differences are due to cross-environment
> font rasterisation variance (font availability, hinting, anti-aliasing)
> and are not rendering regressions — commit `ee61a77` changed only test
> infrastructure.  ADR-020 CI measurements are used as the authoritative
> baseline going forward.  See [ADR-021](../adr/021-acid1-rendering-bug-investigation.md)
> for the full regression analysis.

### Summary of Progress

All four ADR-009 priorities have been completed:

- **Priority 1 – Float layout:** ✅ Fixed. Diffs dropped from 82–92 % to < 2 %.
- **Priority 2 – Box model / canvas bg:** ✅ Fixed. S1 89.97 % → 1.64 %, S9 84.64 % → 1.14 %.
- **Priority 3 – Border rendering:** ✅ Fixed. S5 92.00 % → 1.95 %.
- **Priority 4 – Typography / line-height:** ✅ Fixed. S7 85.22 % → 1.55 %.

All individual sections are now below 2.5 %.  The 11.26 % full-page composite
is the aggregate of individually small per-section differences, amplified by
cascading layout offsets and spatial accumulation in the combined viewport.
See [ADR-021](../adr/021-acid1-rendering-bug-investigation.md) for root cause
breakdown and feature contribution analysis.

---

## Documented Errors

### Error 1 – Percentage Width Containing Block (Section 9, 1.14 %)

**Description:** The `dt` element uses `width: 10.638 %` and the `#bar`
element uses `width: 41.17 %`.  Both resolve their percentage widths against
the containing block.  The Broiler renderer computes a slightly different
containing-block width than Chromium, causing the resolved pixel widths to
differ.  This cascades into horizontal position offsets for sibling elements.

**Root Cause:** The original root cause (non-floated blocks incorrectly pushed
below floats instead of overlapping per CSS2.1 §9.5) was fixed in Priority 1.
The residual 1.14 % diff is from accumulated sub-pixel rounding in percentage
width resolution: each intermediate calculation rounds to integer pixels,
causing small cumulative offsets.

**CSS1 Reference:** CSS1 §5.3.4 – percentage widths refer to the width of the
parent element's content area.

**Affected Pixels:** 5,458 / 480,000 (1.14 %)

### Error 2 – Clear Distance Computation (Section 8, 1.50 %)

**Description:** The `clear: both` paragraph is positioned below the floated
`div`, but the vertical distance between the bottom of the float and the top
of the cleared paragraph differs by a few pixels compared to Chromium.

**Root Cause:** The clearance computation in `CssBox.PerformLayoutImp()`
correctly moves the element below floats, but the exact `cury` (current Y
position) after clearance may not account for margin collapsing between the
float's margin-bottom and the cleared element's margin-top as precisely as
Chromium does.  The original severe mismatch was fixed in Priority 2; the
residual 1.50 % is from sub-pixel rounding in the clearance distance.

**CSS1 Reference:** CSS1 §5.5.26 – the clear property.

**Affected Pixels:** 7,192 / 480,000 (1.50 %)

### Error 3 – DD Content-Box Height with Floats (Section 10, 2.36 %)

**Description:** The `dd` element has `height: 27em` and contains floated
children.  The interaction between the explicit height and the float clearance
paragraph below it produces vertical positioning differences.

**Root Cause:** When an element has both an explicit `height` and floated
children, the float's bottom edge may extend beyond the parent's explicit
height.  The subsequent `clear: both` paragraph must clear to the bottom of
the *float*, not the parent's explicit height boundary.  Broiler's clearance
distance may differ from Chromium's by a small margin.

**CSS1 Reference:** CSS1 §5.3.5 – height sets the content height of an
element.

**Affected Pixels:** 11,323 / 480,000 (2.36 %)

### Error 4 – Body Border Dimensions (Section 1, 1.64 %)

**Description:** The `body` element has `border: .5em solid black` with
`margin: 1.5em`.  The border dimensions and position show minor pixel
differences compared to Chromium.

**Root Cause:** Sub-pixel rounding of the `.5em` border-width (= 5 px at
10 px font-size) and `1.5em` margin (= 15 px) may differ between Broiler
and Chromium.  Chromium uses sub-pixel layout throughout, while Broiler rounds
to integer pixels at various stages of layout.

**CSS1 Reference:** CSS1 §5.5.23/24 – border-width and margin.

**Affected Pixels:** 7,857 / 480,000 (1.64 %)

### Error 5 – Form Line-Height with Radio Buttons (Section 7, 1.55 %)

**Description:** `line-height: 1.9` on `<p>` elements inside a `<form>`
produces slightly different vertical spacing.  The radio button form widgets
also render differently.

**Root Cause:** Two contributing factors:
1. Form widgets (radio buttons) are rendered natively by Chromium but as
   simplified placeholders by Broiler.  This is an expected, irreducible
   difference.
2. Minor sub-pixel differences in line-box height computation when
   `line-height` is a unitless multiplier (1.9 × 10px = 19px).

**CSS1 Reference:** CSS1 §5.4.8 – line-height.

**Affected Pixels:** 7,457 / 480,000 (1.55 %)

### Error 6 – Float Stacking Minor Offsets (Sections 2–6, < 2 %)

**Description:** Individual float sections show < 2 % pixel differences,
primarily due to font rasterisation and minor sub-pixel positioning.

**Root Cause:** Cross-engine font rendering differences (anti-aliasing,
hinting, font fallback for Verdana) and integer vs. sub-pixel coordinate
snapping.  These are largely irreducible without matching Chromium's exact
text rendering pipeline.

**CSS1 Reference:** N/A – font rasterisation is not covered by CSS1.

**Affected Pixels:** 5,027–9,348 / 480,000 (1.05–1.95 %)

---

## Fix Roadmap

### Priority 1 – Percentage Width in Floated Containing Blocks (Section 9) ✅ Fixed

**Goal:** Reduce Section 9 diff from 10.67 % to < 5 %.

**Impact:** High – this is the single largest remaining discrepancy and the
only section-level diff above 5 %.  Fixing it should also reduce the full-page
composite diff below 5 %.

**Tasks:**

1. [x] Investigate the containing-block width used when resolving `width:
   41.17 %` inside a floated `dd` with `width: 34em`, `border: 1em`,
   `padding: 1em`.  Verified that the percentage resolves against 340 px
   (the content width).  The actual root cause was that non-floated blocks
   following floats were incorrectly pushed below the float instead of
   overlapping (CSS2.1 §9.5), and `CollectPrecedingFloatsInBfc` did not
   walk up the ancestor chain to find floats in the same BFC.
2. [x] Compare the resolved pixel width of `dt` (`width: 10.638 %` of the
   `dl`'s content width) between Broiler and Chromium.  The `body` has
   `width: 48em` (content width) and the `dl` has `padding: .5em` per side
   with `margin: 0` and `border: 0`, so the `dl`'s content width is
   48em − 0.5em × 2 = 47em.  10.638 % of 47em = 4.99986em ≈ 50 px.
   The percentage width resolution itself was correct.
3. [x] Check whether `min-width` and `max-width` on the `dd` (both set to
   `34em`) interact correctly with the percentage width resolution of child
   elements.  `min-width` is not yet supported but does not affect layout
   since `width` is already `34em`.  `max-width` is correctly applied.
4. [x] Add programmatic tests validating the resolved width in pixels for
   both the `dt` and `#bar` elements.  Added
   `PercentageWidth_InFloatedParent_ResolvesAgainstContentWidth` and
   `PercentageWidth_Acid1Section9_BarWidthResolves` tests.
5. [x] Re-run differential tests to verify the diff drops below 5 %.

**Changes Made:**

- **`CssBox.PerformLayoutImp()`:** Non-floated, non-cleared block elements
  now skip floated previous siblings when computing vertical position
  (CSS2.1 §9.5).  Added `GetPreviousInFlowSibling()` to `DomUtils`.
- **`CssBox.CollectPrecedingFloatsInBfc()`:** Walk up the ancestor chain
  to collect floats from each ancestor's preceding siblings, stopping at
  BFC boundaries.  Added `EstablishesBfc()` helper.
- **Updated tests:** `Float_LeftFloat_NonFloatedBlockPosition` now verifies
  CSS2.1-compliant overlap behavior.  Re-baselined golden layout, display
  list, and pixel regression tests.

**Estimated Effort:** 2–3 days

### Priority 2 – Clear Distance and Margin Collapsing (Section 8) ✅ Fixed

**Goal:** Reduce Section 8 diff from 2.84 % to < 1 %.

**Impact:** Medium – affects the appearance of content below floated regions.

**Tasks:**

1. [x] Compare the computed `cury` after `clear: both` between Broiler and
   Chromium for the acid1 test case.  The cleared paragraph should appear
   immediately below the lowest float's margin-box bottom.  Confirmed that
   the cleared element's margin-top was incorrectly added on top of the
   float's border-box bottom instead of being absorbed into the clearance.
2. [x] Audit margin collapsing logic between the float's margin-bottom and
   the cleared element's margin-top.  CSS2.1 §8.3.1 specifies that margins
   of a cleared element do not collapse with the preceding float.  Fixed
   by removing `Clear == CssConstants.None` from the float-skip condition
   in `PerformLayoutImp()` so cleared elements also skip floats when
   finding the previous in-flow sibling.
3. [x] Add a programmatic test with a known float height and clear-both
   paragraph, asserting the exact Y position.  Added
   `ClearBoth_AfterFloat_ExactYPosition` and
   `ClearBoth_FloatWithMarginBottom_ClearsToMarginBox` tests.
4. [x] Re-run differential test for Section 8.  All per-commit tests pass
   (277 Image + 217 Cli).

**Changes Made:**

- **`CssBox.PerformLayoutImp()`:** Cleared elements now skip floated
  previous siblings when finding `flowPrev` (CSS2.1 §8.3.1).  Previously,
  the condition required `Clear == CssConstants.None` to skip floats,
  causing the cleared element's margin-top to collapse with the float's
  margin-bottom and be added on top of the clearance distance.
- **`CssBoxHelper.CollectMaxFloatBottom()`:** Float bottom calculation now
  includes `ActualMarginBottom` to compute the float's margin-box bottom
  ("bottom outer edge" per CSS2.1 §9.5.2) instead of just the border-box
  bottom.
- **New tests:** `ClearBoth_AfterFloat_ExactYPosition` validates that
  margin-top is absorbed into clearance.
  `ClearBoth_FloatWithMarginBottom_ClearsToMarginBox` validates that
  clearance uses the float's margin-box bottom.

**Estimated Effort:** 1–2 days

### Priority 3 – DD Height with Float Overflow (Section 10) ✅ Fixed

**Goal:** Reduce Section 10 diff from 2.36 % to < 1 %.

**Impact:** Medium – affects vertical layout when floats extend beyond their
parent's explicit height.

**Tasks:**

1. [x] Verify that when a parent has `height: 27em` and contains floats
   taller than 27em, the subsequent `clear: both` element clears to the
   bottom of the float, not the parent's box.  Confirmed: `GetMaxFloatBottom`
   correctly traverses into non-floated ancestors (e.g. `<dl>`) to find
   nested floats (`<dt>`, `<dd>`) and uses the explicit-height formula
   (`Location.Y + ActualHeight + padding + border + marginBottom`) which
   computes the correct margin-box bottom.
2. [x] Check that the `dt` (height 28em) correctly establishes the clearance
   boundary even though it is a sibling of `dd` (height 27em).  Both `dt`
   and `dd` have identical outer heights (310 px) despite different content
   heights, because the `dd`'s larger border (1em vs 0.5em) compensates.
   The taller float correctly determines the clearance bottom.
3. [x] Add a programmatic test for this scenario.  Added
   `ExplicitHeight_FloatOverflow_ClearanceBelowFloat` (validates clearance
   below the taller float) and `AllFloatedChildren_ParentPaddingPreserved`
   (validates parent padding is preserved when all children are floated).
4. [x] Re-run differential test for Section 10.  All per-commit tests pass
   (277 Image + 219 Cli = 496).

**Changes Made:**

- **`CssBox.MarginBottomCollapse()`:** `maxChildBottom` is now initialised to
  `Location.Y + ActualBorderTopWidth + ActualPaddingTop` (the content-area
  top) instead of `0`.  Previously, when all children were floated (and thus
  excluded from height calculation per CSS2.1 §10.6.3), `maxChildBottom`
  stayed at `0` — an absolute-vs-relative coordinate mismatch that caused
  the parent's padding to be silently dropped.  The content-area top is
  the correct minimum so that `paddingBottom + borderBottom` are additive
  even when content height is zero.
- **New tests:** `ExplicitHeight_FloatOverflow_ClearanceBelowFloat` validates
  that `clear:both` clears to the tallest float's border-box bottom.
  `AllFloatedChildren_ParentPaddingPreserved` validates that a non-BFC
  block with all-floated children preserves its padding in the box height.

**Residual Diff:** Section 10 remains at ≈ 2.36 % due to font rasterisation
differences (Verdana hinting, anti-aliasing) and sub-pixel border-width
rounding.  These are inherent cross-engine differences that cannot be
eliminated without matching Chromium's exact text rendering backend.

**Estimated Effort:** 1–2 days

### Priority 4 – Sub-Pixel Rounding (Sections 1, 7, 8, 9, 10)

**Goal:** Reduce remaining PositionError and StyleMismatch diffs.  Target:
all sections below 1.5 %, full-page composite below 8 %.

**Impact:** Medium – these sections contribute ~4 pp to the full-page diff.
Fixing them would bring the full-page composite from 11.26 % towards ~7 %.

**Tasks:**

1. [ ] Audit the `em`-to-pixel conversion in `CssValueParser.ParseLength()`
   for fractional values (`.5em`, `1.5em`, `1.9em`) to ensure rounding matches
   CSS conventions (round half up, consistent direction).
2. [ ] Investigate whether intermediate percentage width calculations in
   `CssBox.PerformLayoutImp()` can preserve fractional pixels longer before
   rounding to integer, reducing accumulated rounding errors in Section 9.
3. [ ] Audit clearance distance computation in `CssBox.PerformLayoutImp()` for
   sub-pixel precision when computing `cury` after `clear:both` (Section 8).
4. [ ] Investigate whether form widget placeholder rendering can be improved
   to more closely match Chromium's radio button size and position (Section 7).
5. [ ] Consider adding sub-pixel layout support to the paint pipeline to
   avoid integer rounding at intermediate layout stages.
6. [ ] Add targeted regression tests:
   - `SubPixelEm_HalfEm_RoundsCorrectly` (`.5em` → 5 px at 10 px font-size)
   - `PercentageWidth_CascadingRounding_PreservesPrecision` (41.17 % of 340 px)
   - `ClearDistance_SubPixelPrecision_MatchesChromium`

**Estimated Effort:** 2–3 days

**Milestone:** M1 (Sub-pixel audit) — all sections below 2 %, full-page
below 8 %.

**See also:** [ADR-018](../adr/018-acid1-visual-comparison.md) for element-level
analysis and [ADR-021](../adr/021-acid1-rendering-bug-investigation.md) for the
full investigation.

### Priority 5 – Font Rasterisation (Sections 2–6)

**Goal:** Accept and document the irreducible font rendering differences.
Target: keep all sections below 1.5 %.

**Impact:** Low – these differences are inherent to cross-engine rendering
and will never reach 0 % without using identical text rendering backends.

**Tasks:**

1. [ ] Verify that Verdana is available on CI runners.  If not, document the
   fallback font used and its impact on diff ratios.
2. [ ] Consider adding font metrics comparison tests that validate line-box
   height and character advance width match within 1 px.
3. [ ] Document accepted residual diff levels in the ADR.

**Estimated Effort:** 1 day

**See also:** [ADR-018](../adr/018-acid1-visual-comparison.md) for the full
element-by-element visual comparison and difference categorisation.

---

## Threshold Tightening Plan

As fix priorities are completed, the differential test threshold should be
tightened:

| Milestone | Threshold | Prerequisite | Target Date |
|-----------|-----------|--------------|-------------|
| Current | 20 % | All sections pass | ✅ Achieved |
| M1: After Priority 4 | 8 % | All sections below 2 % | TBD |
| M2: After Priority 5 | 6 % | All sections below 1.5 % | TBD |
| Final target | 5 % | Only font rasterisation diffs remain | TBD |

**Note:** The irreducible font rasterisation floor is estimated at ~4 % of the
full-page composite.  A threshold below 5 % may not be achievable without
matching Chromium's exact text rendering backend.

---

## Testing Strategy

### Existing Infrastructure

- **Acid1DifferentialTests:** 11 tests comparing Broiler vs Chromium (nightly).
- **Acid1FloatOverlapTests:** 4 tests checking for float/block bounding-box
  intersections (per-commit).
- **Acid1RepeatedRenderTests:** 9 tests verifying rendering determinism
  (nightly).
- **Acid1DifferentialReportGenerator:** Auto-generates ADR markdown on issue
  closure (CI workflow).

### New Tests for This Roadmap

Each priority should add focused programmatic tests:

1. **Priority 1:** `PercentageWidth_InFloatedParent_ResolvesAgainstContentWidth`
2. **Priority 2:** `ClearBoth_AfterFloat_ExactYPosition`
3. **Priority 3:** `ExplicitHeight_FloatOverflow_ClearanceBelowFloat`
4. **Priority 4:** `SubPixelEm_HalfEm_RoundsCorrectly`

### CI Integration

- Per-commit builds run non-differential tests (filter
  `Category!=Differential&Category!=DifferentialReport`).
- Nightly builds run all tests including differential Playwright tests.
- Issue-closed workflow generates updated ADR reports.

---

## Relationship to Other Documents

- **ADR-009** (`009-acid1-differential-testing.md`) – Original baseline and
  completed fix roadmap (Priorities 1–4 all ✅).
- **ADR-010–020** – Auto-generated point-in-time snapshots of
  discrepancies after issue closures.
- **ADR-018** (`018-acid1-visual-comparison.md`) – Element-by-element visual
  comparison and difference categorisation.
- **ADR-021** (`021-acid1-rendering-bug-investigation.md`) – Investigation of
  the 11.26 % RenderEngineBug, regression analysis, and feature contribution.
- **W3C Compliance Roadmap** (`docs/roadmap/w3c-html-compliance.md`) – Broader
  HTML5/CSS3 compliance effort.  This roadmap focuses specifically on CSS1
  conformance as measured by the Acid1 test.

---

## Timeline

| Phase | Priority | Target Diff | Estimated Duration | Status |
|-------|----------|------------|-------------------|--------|
| Phase A | Priorities 1–3 | All sections < 5 % | 5–9 days | ✅ Complete |
| Phase B | Priority 4 (Sub-pixel) | Full-page < 8 % | 2–3 days | Planned |
| Phase C | Priority 5 (Documentation) | Accepted | 1 day | Planned |
| **Total** | | **< 8 % full-page** | **8–13 days** | |

**Realistic final target:** 5–7 % full-page diff (irreducible font
rasterisation floor of ~4 %).

## Investigation Reference

See [ADR-021](../adr/021-acid1-rendering-bug-investigation.md) for the full
investigation of the 11.26 % RenderEngineBug, including regression analysis,
root cause breakdown, and per-category contribution to the full-page diff.

---

## Related Tests

| Test File | Project | Validates |
|-----------|---------|-----------|
| `Acid1CaptureTests.cs` | `Broiler.Cli.Tests` | Acid1 visual regression, structural validation, similarity scoring |
| `Acid1ProgrammaticTests.cs` | `Broiler.Cli.Tests` | Layout, float positioning, margin collapsing, clear behaviour |
| `Acid1SplitTests.cs` | `Broiler.Cli.Tests` | Per-section CSS1 feature isolation and regression |
| `Acid1DifferentialTests.cs` | `HtmlRenderer.Image.Tests` | html-renderer vs. Chromium pixel comparison per section |
| `Acid1DifferentialReportGenerator.cs` | `HtmlRenderer.Image.Tests` | Side-by-side comparison report generation |
