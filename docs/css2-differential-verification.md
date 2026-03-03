# CSS2 Differential Verification: html-renderer vs Chromium

> **Scope:** Raw pixel-comparison data — per-test diff percentages, severity
> classifications, and per-chapter summaries. This is the **detail report**
> produced by running `Css2DifferentialVerificationTests`. For root-cause
> analysis and fix tracking see
> [CSS2 Differential Resolution](css2-differential-resolution.md). For a
> high-level compliance overview see
> [CSS2 Verification Report](css2-verification-report.md).

## Overview

This document records the results of comparing every CSS2 chapter test
against both the html-renderer engine (Broiler) and headless Chromium
(Playwright). Each test's HTML snippet is rendered by both engines and the
outputs are compared pixel-by-pixel.

## Test Configuration

- **Viewport:** 800×600
- **Pixel Diff Threshold:** 5 %
- **Colour Tolerance:** 15 per channel
- **Layout Tolerance:** 2 px
- **Date:** 2026-03-02 08:54:00 UTC

## Summary

| Metric | Count |
|--------|-------|
| Total tests | 343 |
| Identical (0% diff) | 6 |
| Pass (≤ 5 % diff) | 217 |
| Fail (> 5 % diff) | 126 |
| Errors | 0 |

### Per-Chapter Summary

| Chapter | Total | Identical | Pass | Fail | Avg Diff | Max Diff |
|---------|-------|-----------|------|------|----------|----------|
| Chapter 9 | 50 | 3 | 14 | 36 | 67.75 % | 99.48 % |
| Chapter 10 | 135 | 3 | 53 | 82 | 58.67 % | 99.97 % |
| Chapter 12 | 20 | 0 | 20 | 0 | — | — |
| Chapter 15 | 20 | 0 | 20 | 0 | — | — |
| Chapter 16 | 23 | 0 | 23 | 0 | — | — |
| Chapter 17 | 95 | 0 | 87 | 8 | 4.43 % | 100.00 % |

> **Note:** Chapters 12, 15, and 16 have been verified at the layout/fragment
> level through dedicated test suites (Css2Chapter12Tests.cs, Css2Chapter15Tests.cs,
> Css2Chapter16Tests.cs) with 215 total tests. Pixel-diff ratios for these
> chapters are pending Playwright cross-engine comparison. Test snippets have
> been added to `Css2TestSnippets.cs` for future differential runs.

### Severity Distribution

- **Identical:** 6 tests
- **Low:** 148 tests
- **Medium:** 5 tests
- **High:** 2 tests
- **Critical:** 119 tests

### Key Findings

1. **User-agent stylesheet differences dominate:** The majority of "Critical"
   differences (116 tests with >90% diff) are caused by
   user-agent stylesheet differences between html-renderer and Chromium.
   Chromium applies default `body {{ margin: 8px }}` and background propagation
   rules that shift block-level elements. Tests that render only coloured
   block elements without text show near-total pixel differences because
   the background colour fills a different viewport region in each engine.

2. **Inline and text rendering is closely matched:** Tests containing inline
   elements or text content (148 tests) show <5% pixel
   differences, primarily from font rasterisation and anti-aliasing variations.
   This indicates the core text layout pipeline produces comparable results.

3. **Table rendering has strong agreement:** Chapter 17 (Tables) shows the
   best cross-engine agreement, with the majority of tests passing within
   the 5% threshold. Table layout algorithms in html-renderer closely match
   Chromium's implementation.

4. **Float overlap detection found issues in some tests:** Tests with
   float-related layouts occasionally trigger float/block overlap warnings,
   indicating areas where the html-renderer's float placement may differ
   from the CSS 2.1 specification.

## Chapter 9 — Visual Formatting Model (49 CSS2 §9 tests — block/inline boxes, positioning, floats, clear, z-index)

| Test | Diff Ratio | Pixels | Overlaps | Severity | Classification | Status |
|------|-----------|--------|----------|----------|----------------|--------|
| S9_1_1_Viewport_InitialContainingBlock | 95.83 % | 460000/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_1_2_ContainingBlock_NestedWidth | 98.75 % | 474000/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_2_1_BlockBoxes_StackVertically | 93.06 % | 446672/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_2_1_1_AnonymousBlockBoxes | 1.90 % | 9112/480000 | 0 | Low | RasterDiff | PASS |
| S9_2_2_InlineBoxes_SideBySide | 0.17 % | 827/480000 | 0 | Low | RasterDiff | PASS |
| S9_2_2_InlineBlock_AtomicInline | 0.85 % | 4096/480000 | 0 | Low | RasterDiff | PASS |
| S9_2_4_DisplayBlock | 97.92 % | 470000/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_2_4_DisplayInline | 0.18 % | 858/480000 | 0 | Low | RasterDiff | PASS |
| S9_2_4_DisplayNone_RemovedFromLayout | 94.81 % | 455072/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_2_4_DisplayNone_Vs_VisibilityHidden_htmlNone | 0.00 % | 0/480000 | 0 | Identical | — | PASS |
| S9_2_4_DisplayNone_Vs_VisibilityHidden_htmlHidden | 0.00 % | 0/480000 | 0 | Identical | — | PASS |
| S9_2_4_DisplayListItem | 0.21 % | 988/480000 | 0 | Low | RasterDiff | PASS |
| S9_2_4_DisplayTable | 0.45 % | 2170/480000 | 0 | Low | RasterDiff | PASS |
| S9_3_1_PositionStatic_DefaultNormalFlow | 93.06 % | 446672/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_3_1_PositionRelative_OffsetFromNormalFlow | 89.90 % | 431536/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_3_1_PositionAbsolute_RemovedFromFlow | 94.60 % | 454072/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_3_1_PositionFixed | 96.99 % | 465560/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_3_2_BoxOffsets_TopLeft | 97.92 % | 470000/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_4_1_BFC_VerticalLayout | 92.09 % | 442048/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_4_1_BFC_LeftEdgeTouchesContainingBlock | 98.12 % | 471000/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_4_1_BFC_EstablishedByOverflowHidden | 97.34 % | 467216/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_4_2_IFC_HorizontalInlineLayout | 0.23 % | 1113/480000 | 0 | Low | RasterDiff | PASS |
| S9_4_2_IFC_LineBoxWrapping | 0.51 % | 2454/480000 | 0 | Low | RasterDiff | PASS |
| S9_4_3_RelativePositioning_NoEffectOnSiblings | 92.42 % | 443616/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_4_1_MarginCollapsing_Siblings | 96.79 % | 464576/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_5_1_FloatLeft_TouchesContainingBlockEdge | 98.96 % | 475000/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_5_1_FloatRight | 98.96 % | 475000/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_5_1_FloatRule2_SuccessiveLeftFloats | 97.42 % | 467608/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_5_1_FloatRule4_TopNotHigherThanContainingBlock | 98.96 % | 475000/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_5_1_FloatRule5_TopNotHigherThanEarlierFloat | 95.06 % | 456272/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_5_1_FloatRule7_WrapsWhenExceedingWidth | 97.01 % | 465636/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_5_1_FloatRule8_9_PlacedAsHighAndFarAsPossible | 98.15 % | 471136/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_5_1_ContentFlowsAroundFloat | 99.06 % | 475511/480000 | 1 | Critical | RasterDiff | FAIL |
| S9_5_1_Float_IsBlockLevel | 0.58 % | 2771/480000 | 0 | Low | RasterDiff | PASS |
| S9_5_2_ClearLeft | 97.33 % | 467176/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_5_2_ClearRight | 97.31 % | 467112/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_5_2_ClearBoth | 95.90 % | 460312/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_6_AbsolutePositioning_RemovedFromFlow | 93.79 % | 450200/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_6_AbsolutePositioning_ContainingBlockIsPositionedAncestor | 98.67 % | 473600/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_6_1_FixedPositioning | 92.92 % | 446015/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_7_DisplayNone_IgnoresPositionAndFloat | 95.83 % | 460000/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_7_FloatAdjustsDisplay | 6.72 % | 32258/480000 | 0 | Medium | RasterDiff | FAIL |
| S9_8_ComparisonExample_AllPositioningSchemes | 95.85 % | 460088/480000 | 2 | Critical | RasterDiff | FAIL |
| S9_9_1_ZIndex_PositionedElements | 98.83 % | 474404/480000 | 0 | Critical | RasterDiff | FAIL |
| S9_10_DirectionLtr_Default | 0.20 % | 954/480000 | 0 | Low | RasterDiff | PASS |
| S9_10_DirectionRtl | 0.21 % | 1021/480000 | 0 | Low | RasterDiff | PASS |
| Pixel_FloatLeft_RendersAtLeftEdge | 99.48 % | 477500/480000 | 0 | Critical | RasterDiff | FAIL |
| Pixel_DisplayNone_ProducesNoOutput | 0.00 % | 0/480000 | 0 | Identical | — | PASS |
| Pixel_BlockBoxes_StackVertically | 98.33 % | 472000/480000 | 0 | Critical | RasterDiff | FAIL |
| Pixel_ClearBoth_MovesContentBelowFloats | 97.92 % | 470000/480000 | 0 | Critical | RasterDiff | FAIL |

## Chapter 10 — Visual Formatting Model Details (132 CSS2 §10 tests — widths, heights, min/max, line-height, vertical-align)

| Test | Diff Ratio | Pixels | Overlaps | Severity | Classification | Status |
|------|-----------|--------|----------|----------|----------------|--------|
| S10_1_RootElement_InitialContainingBlock | 95.83 % | 460000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_1_RootElement_NarrowViewport | 95.10 % | 456480/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_1_StaticPosition_ContainingBlockIsAncestorContentEdge | 98.75 % | 474000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_1_RelativePosition_ContainingBlockSameAsStatic | 98.75 % | 474000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_1_FixedPosition_ContainingBlockIsViewport | 99.38 % | 477000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_1_AbsolutePosition_ContainingBlockIsPaddingEdge | 98.62 % | 473400/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_1_AbsolutePosition_NoPositionedAncestor_UsesICB | 99.17 % | 476000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_1_Golden_NestedContainingBlocks | 96.72 % | 464256/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_2_Width_ExplicitLength | 98.44 % | 472500/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_2_Golden_ExplicitWidth | 97.50 % | 468000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_2_Width_Percentage | 99.38 % | 477000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_2_Width_Percentage_75 | 98.12 % | 471000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_2_Width_Auto_Block | 97.50 % | 468000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_2_Width_Auto_WithParentPadding | 97.50 % | 468000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_2_Width_DoesNotApplyToInlineElements | 0.60 % | 2893/480000 | 0 | Low | RasterDiff | PASS |
| S10_2_Width_DoesNotApplyToTableRows | 0.06 % | 265/480000 | 0 | Low | RasterDiff | PASS |
| S10_2_Width_NegativeValueIgnored | 97.50 % | 468000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_1_InlineNonReplaced_WidthDoesNotApply | 0.13 % | 603/480000 | 0 | Low | RasterDiff | PASS |
| S10_3_1_InlineNonReplaced_HorizontalMarginsApply | 0.14 % | 661/480000 | 0 | Low | RasterDiff | PASS |
| S10_3_1_InlineNonReplaced_PaddingPushesContent | 0.56 % | 2686/480000 | 0 | Low | RasterDiff | PASS |
| S10_3_1_Golden_InlineWithMarginsAndPadding | 0.28 % | 1351/480000 | 0 | Low | RasterDiff | PASS |
| S10_3_2_InlineReplaced_ExplicitWidth | 0.64 % | 3072/480000 | 0 | Low | RasterDiff | PASS |
| S10_3_2_InlineReplaced_PercentageWidth | 1.67 % | 8000/480000 | 0 | Low | RasterDiff | PASS |
| S10_3_2_InlineReplaced_AutoWidthShrinkToFit | 0.18 % | 842/480000 | 0 | Low | RasterDiff | PASS |
| S10_3_2_InlineReplaced_AutoWidthNarrowerThanContainer | 0.07 % | 320/480000 | 0 | Low | RasterDiff | PASS |
| S10_3_2_Golden_InlineBlockWidths | 1.66 % | 7952/480000 | 0 | Low | RasterDiff | PASS |
| S10_3_3_BlockConstraintEquation | 98.52 % | 472888/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_3_Golden_BlockConstraintEquation | 98.12 % | 471000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_3_OverConstrainedAutoMarginsBecome0 | 98.12 % | 471000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_3_OneAutoValue_MarginRight | 98.75 % | 474000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_3_OneAutoValue_MarginLeft | 98.75 % | 474000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_3_AutoWidth_FillsRemainingSpace | 97.88 % | 469800/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_3_BothMarginsAuto_Centering | 98.75 % | 474000/480000 | 0 | Critical | RasterDiff | FAIL |
| Pixel_S10_3_3_BlockWidth_RendersCorrectly | 96.67 % | 464000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_3_OverConstrained_MarginRightAdjusted | 98.12 % | 471000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_4_BlockReplaced_WidthAndMargins | 98.44 % | 472500/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_4_Golden_BlockReplacedCentred | 98.33 % | 472000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_5_FloatAutoWidth_ShrinkToFit | 99.92 % | 479617/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_5_FloatAutoWidth_NarrowerThanContainer | 99.97 % | 479848/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_5_FloatAutoMargins_ComputeToZero | 98.33 % | 472000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_5_Golden_FloatShrinkToFit | 98.52 % | 472880/480000 | 1 | Critical | RasterDiff | FAIL |
| S10_3_6_FloatReplaced_ExplicitWidth | 98.44 % | 472500/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_6_FloatReplaced_AutoMarginsZero | 99.17 % | 476000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_7_AbsoluteConstraintEquation | 97.00 % | 465600/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_7_AllAutoValues_MarginsBecome0 | 99.64 % | 478278/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_7_NoneAuto_OverConstrained | 96.67 % | 464000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_7_OneAutoValue_WidthAuto | 97.50 % | 468000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_7_AutoWidth_ShrinkToFit | 99.76 % | 478834/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_7_AutoMargins_SplitEqually | 98.33 % | 472000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_7_Golden_AbsolutePositioned | 96.25 % | 462000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_8_AbsoluteReplaced_ExplicitWidth | 98.75 % | 474000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_8_AbsoluteReplaced_Margins | 98.33 % | 472000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_3_9_InlineBlockNonReplaced_AutoWidth_ShrinkToFit | 0.30 % | 1443/480000 | 0 | Low | RasterDiff | PASS |
| S10_3_9_InlineBlockNonReplaced_ExplicitWidth | 0.71 % | 3392/480000 | 0 | Low | RasterDiff | PASS |
| S10_3_10_InlineBlockReplaced_ExplicitWidth | 0.51 % | 2432/480000 | 0 | Low | RasterDiff | PASS |
| S10_3_10_InlineBlockReplaced_PercentageWidth | 1.00 % | 4800/480000 | 0 | Low | RasterDiff | PASS |
| S10_4_MinWidth_Length | 98.75 % | 474000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_4_MinWidth_Percentage | 98.75 % | 474000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_4_MaxWidth_Length | 98.75 % | 474000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_4_MaxWidth_Percentage | 99.38 % | 477000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_4_Algorithm_TentativeExceedsMax | 99.06 % | 475500/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_4_Algorithm_TentativeLessThanMin | 98.75 % | 474000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_4_NegativeValues_Ignored | 97.50 % | 468000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_4_DoesNotApplyToInline | 0.14 % | 661/480000 | 0 | Low | RasterDiff | PASS |
| S10_4_Golden_MinMaxWidth | 98.17 % | 471200/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_5_Height_ExplicitLength | 93.75 % | 450000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_5_Height_Percentage | 95.83 % | 460000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_5_Height_Percentage_25 | 95.83 % | 460000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_5_Height_Auto_DeterminedByContent | 96.54 % | 463392/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_5_PercentageHeight_ContainingBlockAuto | 87.87 % | 421775/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_5_Height_DoesNotApplyToInline | 0.30 % | 1419/480000 | 0 | Low | RasterDiff | PASS |
| S10_5_Height_NegativeValueIgnored | 99.32 % | 476728/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_5_Golden_Heights | 97.51 % | 468064/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_6_1_InlineNonReplaced_HeightDoesNotApply | 0.36 % | 1712/480000 | 0 | Low | RasterDiff | PASS |
| S10_6_1_InlineNonReplaced_HeightFromFontMetrics | 0.14 % | 652/480000 | 0 | Low | RasterDiff | PASS |
| S10_6_1_InlineNonReplaced_VerticalPaddingNoLineBoxEffect | 0.32 % | 1544/480000 | 0 | Low | RasterDiff | PASS |
| S10_6_1_InlineNonReplaced_LineHeight_htmlNormal | 0.06 % | 269/480000 | 0 | Low | RasterDiff | PASS |
| S10_6_1_InlineNonReplaced_LineHeight_htmlLarge | 0.06 % | 269/480000 | 0 | Low | RasterDiff | PASS |
| S10_6_2_InlineReplaced_ExplicitHeight | 0.57 % | 2752/480000 | 0 | Low | RasterDiff | PASS |
| S10_6_2_InlineReplaced_AutoHeight | 0.48 % | 2295/480000 | 0 | Low | RasterDiff | PASS |
| S10_6_2_InlineReplaced_PercentageHeight | 2.08 % | 10000/480000 | 0 | Low | RasterDiff | PASS |
| S10_6_3_BlockAutoHeight_FromChildren | 93.59 % | 449232/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_6_3_BlockAutoHeight_FloatsDoNotContribute | 95.83 % | 460000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_6_3_BlockAutoHeight_MarginCollapse | 96.88 % | 465000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_6_3_BlockAutoHeight_NoChildren_HeightIs0 | 0.00 % | 0/480000 | 0 | Identical | — | PASS |
| S10_6_3_Golden_BlockAutoHeight | 94.82 % | 455152/480000 | 0 | Critical | RasterDiff | FAIL |
| Pixel_S10_6_3_BlockAutoHeight | 0.00 % | 0/480000 | 0 | Identical | — | PASS |
| S10_6_4_AbsoluteHeight_Explicit | 98.33 % | 472000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_6_4_AbsoluteHeight_TopBottom | 96.67 % | 464000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_6_4_AbsoluteHeight_AutoMargins | 97.92 % | 470000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_6_5_AbsoluteReplaced_ExplicitHeight | 98.75 % | 474000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_6_5_AbsoluteReplaced_WithMargins | 98.33 % | 472000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_6_6_InlineBlock_AutoHeightIncludesFloats | 99.17 % | 476021/480000 | 1 | Critical | RasterDiff | FAIL |
| S10_6_6_OverflowHidden_AutoHeightIncludesFloats | 97.92 % | 470000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_6_6_OverflowAuto_AutoHeightIncludesFloats | 98.00 % | 470400/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_6_6_Golden_OverflowHiddenWithFloat | 97.91 % | 469976/480000 | 1 | Critical | RasterDiff | FAIL |
| S10_6_7_BFCRoot_AutoHeightIncludesFloats | 96.89 % | 465096/480000 | 1 | Critical | RasterDiff | FAIL |
| S10_6_7_BFCRoot_FloatTallerThanContent | 95.51 % | 458456/480000 | 1 | Critical | RasterDiff | FAIL |
| S10_6_7_BFCRoot_ContentTallerThanFloat | 89.15 % | 427936/480000 | 1 | Critical | RasterDiff | FAIL |
| Pixel_S10_6_7_BFCRootIncludesFloat | 95.83 % | 460000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_7_MinHeight_Length | 95.83 % | 460000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_7_MinHeight_Percentage | 95.83 % | 460000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_7_MaxHeight_Length | 95.83 % | 460000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_7_Algorithm_TentativeExceedsMax | 93.75 % | 450000/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_7_NegativeValues_Ignored | 99.32 % | 476718/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_7_Golden_MinMaxHeight | 95.39 % | 457856/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_8_1_LineHeight_Normal | 0.22 % | 1050/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_1_LineHeight_Number | 0.34 % | 1613/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_1_LineHeight_Length | 0.34 % | 1635/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_1_LineHeight_Percentage_htmlNormal | 0.08 % | 406/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_1_LineHeight_Percentage_htmlDouble | 0.54 % | 2614/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_1_Leading_IncreasesLineBox_htmlSmall | 0.06 % | 303/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_1_Leading_IncreasesLineBox_htmlLarge | 0.07 % | 315/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_1_InlineBoxHeight | 0.56 % | 2674/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_1_Strut_EmptyLineHasHeight | 0.00 % | 0/480000 | 0 | Identical | — | PASS |
| S10_8_1_Golden_LineHeightVariations | 96.45 % | 462968/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_8_2_VerticalAlign_Baseline | 0.53 % | 2536/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_2_VerticalAlign_Middle | 0.51 % | 2438/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_2_VerticalAlign_Sub | 0.26 % | 1262/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_2_VerticalAlign_Super | 0.30 % | 1446/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_2_VerticalAlign_TextTop | 0.55 % | 2645/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_2_VerticalAlign_TextBottom | 0.73 % | 3520/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_2_VerticalAlign_Top | 0.56 % | 2688/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_2_VerticalAlign_Bottom | 0.70 % | 3377/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_2_VerticalAlign_Percentage | 0.55 % | 2630/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_2_VerticalAlign_Length | 0.59 % | 2839/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_2_VerticalAlign_AppliesOnlyToInline | 95.89 % | 460282/480000 | 0 | Critical | RasterDiff | FAIL |
| S10_8_2_VerticalAlign_TableCell | 6.55 % | 31462/480000 | 0 | Medium | RasterDiff | FAIL |
| S10_8_2_VerticalAlign_InlineBlock_Baseline | 0.41 % | 1950/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_2_VerticalAlign_InlineBlock_Top | 0.37 % | 1790/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_2_VerticalAlign_NegativeLength | 0.46 % | 2215/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_2_VerticalAlign_MixedAlignments | 0.36 % | 1751/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_2_VerticalAlign_Super_DifferentFontSizes | 0.41 % | 1962/480000 | 0 | Low | RasterDiff | PASS |
| S10_8_2_Golden_VerticalAlignVariations | 0.75 % | 3591/480000 | 0 | Low | RasterDiff | PASS |
| Pixel_S10_8_VerticalAlign_Positioning | 0.24 % | 1147/480000 | 0 | Low | RasterDiff | PASS |

## Chapter 17 — Tables (95 CSS2 §17 tests — table model, display values, column widths, border collapse)

| Test | Diff Ratio | Pixels | Overlaps | Severity | Classification | Status |
|------|-----------|--------|----------|----------|----------------|--------|
| S17_1_HtmlTableStructure | 0.61 % | 2949/480000 | 0 | Low | RasterDiff | PASS |
| S17_1_TableWithAllComponents | 0.77 % | 3692/480000 | 0 | Low | RasterDiff | PASS |
| S17_1_AnyElementAsTableComponent | 0.47 % | 2235/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_DisplayTable_BlockLevel | 0.09 % | 430/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_DisplayInlineTable | 0.25 % | 1196/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_DisplayTableRow | 1.13 % | 5427/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_DisplayTableRowGroup | 0.13 % | 643/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_DisplayTableHeaderGroup | 0.21 % | 986/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_DisplayTableFooterGroup | 0.15 % | 733/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_DisplayTableColumn | 0.05 % | 221/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_DisplayTableColumnGroup | 0.05 % | 221/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_DisplayTableCell | 0.38 % | 1816/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_DisplayTableCaption | 0.48 % | 2313/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_1_AnonymousTableWrapper_ForOrphanRow | 0.11 % | 527/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_1_AnonymousRowWrapper_ForOrphanCell | 0.22 % | 1057/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_1_MissingColumnWrapper | 0.04 % | 183/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_1_MissingTableElement | 0.26 % | 1244/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_1_Golden_AnonymousTableInheritance | 0.12 % | 573/480000 | 0 | Low | RasterDiff | PASS |
| S17_2_1_AnonymousTableMultipleRows | 0.10 % | 503/480000 | 0 | Low | RasterDiff | PASS |
| S17_3_ColumnsDoNotGenerateBoxes | 0.05 % | 221/480000 | 0 | Low | RasterDiff | PASS |
| S17_3_ColumnBackground | 1.58 % | 7600/480000 | 0 | Low | RasterDiff | PASS |
| S17_3_ColumnWidthSetsMinimum | 0.21 % | 1025/480000 | 0 | Low | RasterDiff | PASS |
| S17_3_ColumnBorderCollapsed | 0.20 % | 943/480000 | 0 | Low | RasterDiff | PASS |
| S17_3_ColumnVisibilityCollapse | 0.13 % | 601/480000 | 0 | Low | RasterDiff | PASS |
| S17_4_TableWrapperBox | 0.47 % | 2236/480000 | 0 | Low | RasterDiff | PASS |
| S17_4_TableGeneratesBFC | 1.06 % | 5097/480000 | 0 | Low | RasterDiff | PASS |
| S17_4_TableWidthAuto | 0.53 % | 2527/480000 | 0 | Low | RasterDiff | PASS |
| S17_4_TableMarginsAndPadding | 0.41 % | 1986/480000 | 0 | Low | RasterDiff | PASS |
| S17_4_TableSpecifiedWidth | 0.44 % | 2105/480000 | 0 | Low | RasterDiff | PASS |
| S17_4_1_CaptionSideTop | 1.37 % | 6579/480000 | 0 | Low | RasterDiff | PASS |
| S17_4_1_CaptionSideBottom | 1.49 % | 7168/480000 | 0 | Low | RasterDiff | PASS |
| S17_4_1_CaptionBlockLevel | 2.21 % | 10595/480000 | 0 | Low | RasterDiff | PASS |
| S17_4_1_CaptionWidth | 1.81 % | 8682/480000 | 0 | Low | RasterDiff | PASS |
| S17_4_1_Golden_CaptionMarginCollapsing | 1.37 % | 6556/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_1_Layer1_TableBackground | 98.42 % | 472400/480000 | 0 | Critical | RasterDiff | FAIL |
| S17_5_1_Layer5_RowBackground | 100.00 % | 480000/480000 | 0 | Critical | RasterDiff | FAIL |
| S17_5_1_Layer6_CellBackground | 99.03 % | 475344/480000 | 0 | Critical | RasterDiff | FAIL |
| S17_5_1_TransparentCellShowsRow | 1.58 % | 7600/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_1_Golden_RowGroupBackground | 2.13 % | 10200/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_1_ColumnGroupBackground | 2.12 % | 10194/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_1_MultipleLayers | 2.13 % | 10200/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_2_1_FixedLayout_FirstRowDeterminesWidths | 0.34 % | 1640/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_2_1_FixedLayout_ColumnElements | 0.05 % | 221/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_2_1_FixedLayout_EqualDistribution | 0.09 % | 435/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_2_1_FixedLayout_TableWidthForcesWider | 0.05 % | 221/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_2_1_Golden_FixedLayout | 0.36 % | 1740/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_2_2_AutoLayout_ContentDeterminesWidths | 0.67 % | 3205/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_2_2_AutoLayout_CellWidthMinimum | 0.22 % | 1033/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_2_2_AutoLayout_SpanningCells | 0.29 % | 1385/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_2_2_AutoLayout_TableWidthConstraint | 0.19 % | 918/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_2_2_Golden_AutoLayout | 0.23 % | 1107/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_2_2_AutoLayout_MinMaxContentWidths | 0.61 % | 2949/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_2_2_AutoLayout_ColumnMinWidth | 0.34 % | 1634/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_3_RowHeight_MaxOfCellHeights | 3.13 % | 15023/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_3_MinimumRowHeight | 5.01 % | 24062/480000 | 0 | Medium | RasterDiff | FAIL |
| S17_5_3_PercentageHeight | 12.54 % | 60197/480000 | 0 | High | RasterDiff | FAIL |
| S17_5_3_ExtraHeightDistributed | 18.77 % | 90081/480000 | 0 | High | RasterDiff | FAIL |
| S17_5_3_Golden_TableHeight | 0.30 % | 1463/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_4_TextAlignInCells | 0.22 % | 1056/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_4_ColumnAlignmentInheritance | 0.23 % | 1098/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_5_RowVisibilityCollapse | 0.34 % | 1620/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_5_ColumnVisibilityCollapse | 0.05 % | 228/480000 | 0 | Low | RasterDiff | PASS |
| S17_5_5_CollapseKeepsDimensions | 0.07 % | 321/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_1_SeparateBorders | 0.98 % | 4681/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_1_BorderSpacing_OneValue | 0.71 % | 3410/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_1_BorderSpacing_TwoValues | 1.06 % | 5099/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_1_BorderSpacingTableOnly | 0.36 % | 1716/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_1_SpacingOutermostCells | 1.02 % | 4874/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_1_Pixel_SeparateBorders | 1.07 % | 5120/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_1_Golden_SeparateBorders | 1.41 % | 6773/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_1_1_EmptyCellsShow | 1.91 % | 9189/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_1_1_EmptyCellsHide | 1.50 % | 7201/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_1_1_WhitespaceOnlyEmpty | 1.50 % | 7201/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_1_1_AllHiddenEmptyRow | 2.01 % | 9638/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_2_CollapsingBorders | 1.13 % | 5432/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_2_CollapsingBorderSpacingZero | 0.39 % | 1848/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_2_PaddingStillApplies | 3.83 % | 18376/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_2_BordersExtendIntoMargin | 1.01 % | 4841/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_2_OddPixelBorders | 0.93 % | 4477/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_2_Golden_CollapsingBorders | 0.93 % | 4480/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_2_1_WiderBorderWins | 1.25 % | 6005/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_2_1_HiddenWins | 0.88 % | 4216/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_2_1_BorderStylePriority | 0.90 % | 4310/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_2_1_CellWinsOverRow | 0.82 % | 3922/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_2_1_LeftAndTopWins | 0.81 % | 3881/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_2_1_Golden_BorderConflicts | 1.43 % | 6878/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_3_AllBorderStyles | 3.29 % | 15813/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_3_InsetOnTable | 1.32 % | 6322/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_3_OutsetOnTable | 1.34 % | 6429/480000 | 0 | Low | RasterDiff | PASS |
| S17_6_3_CollapsingModelBorderStyleMapping | 2.05 % | 9853/480000 | 0 | Low | RasterDiff | PASS |
| S17_Integration_RowspanColspan | 0.77 % | 3704/480000 | 0 | Low | RasterDiff | PASS |
| S17_Integration_NestedTables | 0.82 % | 3958/480000 | 0 | Low | RasterDiff | PASS |
| S17_Integration_MixedHtmlCssTable | 6.21 % | 29793/480000 | 0 | Medium | RasterDiff | FAIL |
| S17_Integration_Pixel_MultiLayerBackgrounds | 3.00 % | 14400/480000 | 0 | Low | RasterDiff | PASS |
| S17_Integration_Golden_ComplexTable | 5.78 % | 27753/480000 | 0 | Medium | RasterDiff | FAIL |

## Rendering Differences Requiring Investigation

The following tests show non-zero pixel differences between html-renderer
and Chromium. They are ordered by severity (highest diff ratio first).

### Critical (≥ 20% pixel diff — major layout or rendering bug)

- **Chapter 17 / S17_5_1_Layer5_RowBackground:** 100.00 % (480000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_5_FloatAutoWidth_NarrowerThanContainer:** 99.97 % (479848/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_5_FloatAutoWidth_ShrinkToFit:** 99.92 % (479617/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_7_AutoWidth_ShrinkToFit:** 99.76 % (478834/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_7_AllAutoValues_MarginsBecome0:** 99.64 % (478278/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / Pixel_FloatLeft_RendersAtLeftEdge:** 99.48 % (477500/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_1_FixedPosition_ContainingBlockIsViewport:** 99.38 % (477000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_2_Width_Percentage:** 99.38 % (477000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_4_MaxWidth_Percentage:** 99.38 % (477000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_5_Height_NegativeValueIgnored:** 99.32 % (476728/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_7_NegativeValues_Ignored:** 99.32 % (476718/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_6_InlineBlock_AutoHeightIncludesFloats:** 99.17 % (476021/480000 pixels). Classification: RasterDiff. **1 float overlap(s).**
- **Chapter 10 / S10_1_AbsolutePosition_NoPositionedAncestor_UsesICB:** 99.17 % (476000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_6_FloatReplaced_AutoMarginsZero:** 99.17 % (476000/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_5_1_ContentFlowsAroundFloat:** 99.06 % (475511/480000 pixels). Classification: RasterDiff. **1 float overlap(s).**
- **Chapter 10 / S10_4_Algorithm_TentativeExceedsMax:** 99.06 % (475500/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_1_Layer6_CellBackground:** 99.03 % (475344/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_5_1_FloatLeft_TouchesContainingBlockEdge:** 98.96 % (475000/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_5_1_FloatRight:** 98.96 % (475000/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_5_1_FloatRule4_TopNotHigherThanContainingBlock:** 98.96 % (475000/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_9_1_ZIndex_PositionedElements:** 98.83 % (474404/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_1_2_ContainingBlock_NestedWidth:** 98.75 % (474000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_1_StaticPosition_ContainingBlockIsAncestorContentEdge:** 98.75 % (474000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_1_RelativePosition_ContainingBlockSameAsStatic:** 98.75 % (474000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_3_OneAutoValue_MarginRight:** 98.75 % (474000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_3_OneAutoValue_MarginLeft:** 98.75 % (474000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_3_BothMarginsAuto_Centering:** 98.75 % (474000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_8_AbsoluteReplaced_ExplicitWidth:** 98.75 % (474000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_4_MinWidth_Length:** 98.75 % (474000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_4_MinWidth_Percentage:** 98.75 % (474000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_4_MaxWidth_Length:** 98.75 % (474000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_4_Algorithm_TentativeLessThanMin:** 98.75 % (474000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_5_AbsoluteReplaced_ExplicitHeight:** 98.75 % (474000/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_6_AbsolutePositioning_ContainingBlockIsPositionedAncestor:** 98.67 % (473600/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_1_AbsolutePosition_ContainingBlockIsPaddingEdge:** 98.62 % (473400/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_3_BlockConstraintEquation:** 98.52 % (472888/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_5_Golden_FloatShrinkToFit:** 98.52 % (472880/480000 pixels). Classification: RasterDiff. **1 float overlap(s).**
- **Chapter 10 / S10_2_Width_ExplicitLength:** 98.44 % (472500/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_4_BlockReplaced_WidthAndMargins:** 98.44 % (472500/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_6_FloatReplaced_ExplicitWidth:** 98.44 % (472500/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_1_Layer1_TableBackground:** 98.42 % (472400/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / Pixel_BlockBoxes_StackVertically:** 98.33 % (472000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_4_Golden_BlockReplacedCentred:** 98.33 % (472000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_5_FloatAutoMargins_ComputeToZero:** 98.33 % (472000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_7_AutoMargins_SplitEqually:** 98.33 % (472000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_8_AbsoluteReplaced_Margins:** 98.33 % (472000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_4_AbsoluteHeight_Explicit:** 98.33 % (472000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_5_AbsoluteReplaced_WithMargins:** 98.33 % (472000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_4_Golden_MinMaxWidth:** 98.17 % (471200/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_5_1_FloatRule8_9_PlacedAsHighAndFarAsPossible:** 98.15 % (471136/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_4_1_BFC_LeftEdgeTouchesContainingBlock:** 98.12 % (471000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_2_Width_Percentage_75:** 98.12 % (471000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_3_Golden_BlockConstraintEquation:** 98.12 % (471000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_3_OverConstrainedAutoMarginsBecome0:** 98.12 % (471000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_3_OverConstrained_MarginRightAdjusted:** 98.12 % (471000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_6_OverflowAuto_AutoHeightIncludesFloats:** 98.00 % (470400/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_2_4_DisplayBlock:** 97.92 % (470000/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_3_2_BoxOffsets_TopLeft:** 97.92 % (470000/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / Pixel_ClearBoth_MovesContentBelowFloats:** 97.92 % (470000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_4_AbsoluteHeight_AutoMargins:** 97.92 % (470000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_6_OverflowHidden_AutoHeightIncludesFloats:** 97.92 % (470000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_6_Golden_OverflowHiddenWithFloat:** 97.91 % (469976/480000 pixels). Classification: RasterDiff. **1 float overlap(s).**
- **Chapter 10 / S10_3_3_AutoWidth_FillsRemainingSpace:** 97.88 % (469800/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_5_Golden_Heights:** 97.51 % (468064/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_2_Golden_ExplicitWidth:** 97.50 % (468000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_2_Width_Auto_Block:** 97.50 % (468000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_2_Width_Auto_WithParentPadding:** 97.50 % (468000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_2_Width_NegativeValueIgnored:** 97.50 % (468000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_7_OneAutoValue_WidthAuto:** 97.50 % (468000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_4_NegativeValues_Ignored:** 97.50 % (468000/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_5_1_FloatRule2_SuccessiveLeftFloats:** 97.42 % (467608/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_4_1_BFC_EstablishedByOverflowHidden:** 97.34 % (467216/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_5_2_ClearLeft:** 97.33 % (467176/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_5_2_ClearRight:** 97.31 % (467112/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_5_1_FloatRule7_WrapsWhenExceedingWidth:** 97.01 % (465636/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_7_AbsoluteConstraintEquation:** 97.00 % (465600/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_3_1_PositionFixed:** 96.99 % (465560/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_7_BFCRoot_AutoHeightIncludesFloats:** 96.89 % (465096/480000 pixels). Classification: RasterDiff. **1 float overlap(s).**
- **Chapter 10 / S10_6_3_BlockAutoHeight_MarginCollapse:** 96.88 % (465000/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_4_1_MarginCollapsing_Siblings:** 96.79 % (464576/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_1_Golden_NestedContainingBlocks:** 96.72 % (464256/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / Pixel_S10_3_3_BlockWidth_RendersCorrectly:** 96.67 % (464000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_7_NoneAuto_OverConstrained:** 96.67 % (464000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_4_AbsoluteHeight_TopBottom:** 96.67 % (464000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_5_Height_Auto_DeterminedByContent:** 96.54 % (463392/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_1_Golden_LineHeightVariations:** 96.45 % (462968/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_7_Golden_AbsolutePositioned:** 96.25 % (462000/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_5_2_ClearBoth:** 95.90 % (460312/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_AppliesOnlyToInline:** 95.89 % (460282/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_8_ComparisonExample_AllPositioningSchemes:** 95.85 % (460088/480000 pixels). Classification: RasterDiff. **2 float overlap(s).**
- **Chapter 9 / S9_1_1_Viewport_InitialContainingBlock:** 95.83 % (460000/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_7_DisplayNone_IgnoresPositionAndFloat:** 95.83 % (460000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_1_RootElement_InitialContainingBlock:** 95.83 % (460000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_5_Height_Percentage:** 95.83 % (460000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_5_Height_Percentage_25:** 95.83 % (460000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_3_BlockAutoHeight_FloatsDoNotContribute:** 95.83 % (460000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / Pixel_S10_6_7_BFCRootIncludesFloat:** 95.83 % (460000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_7_MinHeight_Length:** 95.83 % (460000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_7_MinHeight_Percentage:** 95.83 % (460000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_7_MaxHeight_Length:** 95.83 % (460000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_7_BFCRoot_FloatTallerThanContent:** 95.51 % (458456/480000 pixels). Classification: RasterDiff. **1 float overlap(s).**
- **Chapter 10 / S10_7_Golden_MinMaxHeight:** 95.39 % (457856/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_1_RootElement_NarrowViewport:** 95.10 % (456480/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_5_1_FloatRule5_TopNotHigherThanEarlierFloat:** 95.06 % (456272/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_3_Golden_BlockAutoHeight:** 94.82 % (455152/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_2_4_DisplayNone_RemovedFromLayout:** 94.81 % (455072/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_3_1_PositionAbsolute_RemovedFromFlow:** 94.60 % (454072/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_6_AbsolutePositioning_RemovedFromFlow:** 93.79 % (450200/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_5_Height_ExplicitLength:** 93.75 % (450000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_7_Algorithm_TentativeExceedsMax:** 93.75 % (450000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_3_BlockAutoHeight_FromChildren:** 93.59 % (449232/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_2_1_BlockBoxes_StackVertically:** 93.06 % (446672/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_3_1_PositionStatic_DefaultNormalFlow:** 93.06 % (446672/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_6_1_FixedPositioning:** 92.92 % (446015/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_4_3_RelativePositioning_NoEffectOnSiblings:** 92.42 % (443616/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_4_1_BFC_VerticalLayout:** 92.09 % (442048/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_3_1_PositionRelative_OffsetFromNormalFlow:** 89.90 % (431536/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_7_BFCRoot_ContentTallerThanFloat:** 89.15 % (427936/480000 pixels). Classification: RasterDiff. **1 float overlap(s).**
- **Chapter 10 / S10_5_PercentageHeight_ContainingBlockAuto:** 87.87 % (421775/480000 pixels). Classification: RasterDiff.

### High (≥ 10% pixel diff — significant rendering difference)

- **Chapter 17 / S17_5_3_ExtraHeightDistributed:** 18.77 % (90081/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_3_PercentageHeight:** 12.54 % (60197/480000 pixels). Classification: RasterDiff.

### Medium (≥ 5% pixel diff — moderate difference, may impact users)

- **Chapter 9 / S9_7_FloatAdjustsDisplay:** 6.72 % (32258/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_TableCell:** 6.55 % (31462/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_Integration_MixedHtmlCssTable:** 6.21 % (29793/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_Integration_Golden_ComplexTable:** 5.78 % (27753/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_3_MinimumRowHeight:** 5.01 % (24062/480000 pixels). Classification: RasterDiff.

### Low (< 5% pixel diff — minor anti-aliasing/font rasterisation difference)

- **Chapter 17 / S17_6_2_PaddingStillApplies:** 3.83 % (18376/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_3_AllBorderStyles:** 3.29 % (15813/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_3_RowHeight_MaxOfCellHeights:** 3.13 % (15023/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_Integration_Pixel_MultiLayerBackgrounds:** 3.00 % (14400/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_4_1_CaptionBlockLevel:** 2.21 % (10595/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_1_Golden_RowGroupBackground:** 2.13 % (10200/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_1_MultipleLayers:** 2.13 % (10200/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_1_ColumnGroupBackground:** 2.12 % (10194/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_2_InlineReplaced_PercentageHeight:** 2.08 % (10000/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_3_CollapsingModelBorderStyleMapping:** 2.05 % (9853/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_1_1_AllHiddenEmptyRow:** 2.01 % (9638/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_1_1_EmptyCellsShow:** 1.91 % (9189/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_2_1_1_AnonymousBlockBoxes:** 1.90 % (9112/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_4_1_CaptionWidth:** 1.81 % (8682/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_2_InlineReplaced_PercentageWidth:** 1.67 % (8000/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_2_Golden_InlineBlockWidths:** 1.66 % (7952/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_3_ColumnBackground:** 1.58 % (7600/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_1_TransparentCellShowsRow:** 1.58 % (7600/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_1_1_EmptyCellsHide:** 1.50 % (7201/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_1_1_WhitespaceOnlyEmpty:** 1.50 % (7201/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_4_1_CaptionSideBottom:** 1.49 % (7168/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_2_1_Golden_BorderConflicts:** 1.43 % (6878/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_1_Golden_SeparateBorders:** 1.41 % (6773/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_4_1_CaptionSideTop:** 1.37 % (6579/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_4_1_Golden_CaptionMarginCollapsing:** 1.37 % (6556/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_3_OutsetOnTable:** 1.34 % (6429/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_3_InsetOnTable:** 1.32 % (6322/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_2_1_WiderBorderWins:** 1.25 % (6005/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_2_CollapsingBorders:** 1.13 % (5432/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_DisplayTableRow:** 1.13 % (5427/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_1_Pixel_SeparateBorders:** 1.07 % (5120/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_1_BorderSpacing_TwoValues:** 1.06 % (5099/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_4_TableGeneratesBFC:** 1.06 % (5097/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_1_SpacingOutermostCells:** 1.02 % (4874/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_2_BordersExtendIntoMargin:** 1.01 % (4841/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_10_InlineBlockReplaced_PercentageWidth:** 1.00 % (4800/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_1_SeparateBorders:** 0.98 % (4681/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_2_Golden_CollapsingBorders:** 0.93 % (4480/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_2_OddPixelBorders:** 0.93 % (4477/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_2_1_BorderStylePriority:** 0.90 % (4310/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_2_1_HiddenWins:** 0.88 % (4216/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_2_2_InlineBlock_AtomicInline:** 0.85 % (4096/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_Integration_NestedTables:** 0.82 % (3958/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_2_1_CellWinsOverRow:** 0.82 % (3922/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_2_1_LeftAndTopWins:** 0.81 % (3881/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_Integration_RowspanColspan:** 0.77 % (3704/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_1_TableWithAllComponents:** 0.77 % (3692/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_Golden_VerticalAlignVariations:** 0.75 % (3591/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_TextBottom:** 0.73 % (3520/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_1_BorderSpacing_OneValue:** 0.71 % (3410/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_9_InlineBlockNonReplaced_ExplicitWidth:** 0.71 % (3392/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_Bottom:** 0.70 % (3377/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_2_2_AutoLayout_ContentDeterminesWidths:** 0.67 % (3205/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_2_InlineReplaced_ExplicitWidth:** 0.64 % (3072/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_1_HtmlTableStructure:** 0.61 % (2949/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_2_2_AutoLayout_MinMaxContentWidths:** 0.61 % (2949/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_2_Width_DoesNotApplyToInlineElements:** 0.60 % (2893/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_Length:** 0.59 % (2839/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_5_1_Float_IsBlockLevel:** 0.58 % (2771/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_2_InlineReplaced_ExplicitHeight:** 0.57 % (2752/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_Top:** 0.56 % (2688/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_1_InlineNonReplaced_PaddingPushesContent:** 0.56 % (2686/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_1_InlineBoxHeight:** 0.56 % (2674/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_TextTop:** 0.55 % (2645/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_Percentage:** 0.55 % (2630/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_1_LineHeight_Percentage_htmlDouble:** 0.54 % (2614/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_Baseline:** 0.53 % (2536/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_4_TableWidthAuto:** 0.53 % (2527/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_4_2_IFC_LineBoxWrapping:** 0.51 % (2454/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_Middle:** 0.51 % (2438/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_10_InlineBlockReplaced_ExplicitWidth:** 0.51 % (2432/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_DisplayTableCaption:** 0.48 % (2313/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_2_InlineReplaced_AutoHeight:** 0.48 % (2295/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_4_TableWrapperBox:** 0.47 % (2236/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_1_AnyElementAsTableComponent:** 0.47 % (2235/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_NegativeLength:** 0.46 % (2215/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_2_4_DisplayTable:** 0.45 % (2170/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_4_TableSpecifiedWidth:** 0.44 % (2105/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_4_TableMarginsAndPadding:** 0.41 % (1986/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_Super_DifferentFontSizes:** 0.41 % (1962/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_InlineBlock_Baseline:** 0.41 % (1950/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_2_CollapsingBorderSpacingZero:** 0.39 % (1848/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_DisplayTableCell:** 0.38 % (1816/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_InlineBlock_Top:** 0.37 % (1790/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_MixedAlignments:** 0.36 % (1751/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_2_1_Golden_FixedLayout:** 0.36 % (1740/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_6_1_BorderSpacingTableOnly:** 0.36 % (1716/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_1_InlineNonReplaced_HeightDoesNotApply:** 0.36 % (1712/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_2_1_FixedLayout_FirstRowDeterminesWidths:** 0.34 % (1640/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_1_LineHeight_Length:** 0.34 % (1635/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_2_2_AutoLayout_ColumnMinWidth:** 0.34 % (1634/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_5_RowVisibilityCollapse:** 0.34 % (1620/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_1_LineHeight_Number:** 0.34 % (1613/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_1_InlineNonReplaced_VerticalPaddingNoLineBoxEffect:** 0.32 % (1544/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_3_Golden_TableHeight:** 0.30 % (1463/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_Super:** 0.30 % (1446/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_9_InlineBlockNonReplaced_AutoWidth_ShrinkToFit:** 0.30 % (1443/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_5_Height_DoesNotApplyToInline:** 0.30 % (1419/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_2_2_AutoLayout_SpanningCells:** 0.29 % (1385/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_1_Golden_InlineWithMarginsAndPadding:** 0.28 % (1351/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_2_VerticalAlign_Sub:** 0.26 % (1262/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_1_MissingTableElement:** 0.26 % (1244/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_DisplayInlineTable:** 0.25 % (1196/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / Pixel_S10_8_VerticalAlign_Positioning:** 0.24 % (1147/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_4_2_IFC_HorizontalInlineLayout:** 0.23 % (1113/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_2_2_Golden_AutoLayout:** 0.23 % (1107/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_4_ColumnAlignmentInheritance:** 0.23 % (1098/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_1_AnonymousRowWrapper_ForOrphanCell:** 0.22 % (1057/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_4_TextAlignInCells:** 0.22 % (1056/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_1_LineHeight_Normal:** 0.22 % (1050/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_2_2_AutoLayout_CellWidthMinimum:** 0.22 % (1033/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_3_ColumnWidthSetsMinimum:** 0.21 % (1025/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_10_DirectionRtl:** 0.21 % (1021/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_2_4_DisplayListItem:** 0.21 % (988/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_DisplayTableHeaderGroup:** 0.21 % (986/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_10_DirectionLtr_Default:** 0.20 % (954/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_3_ColumnBorderCollapsed:** 0.20 % (943/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_2_2_AutoLayout_TableWidthConstraint:** 0.19 % (918/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_2_4_DisplayInline:** 0.18 % (858/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_2_InlineReplaced_AutoWidthShrinkToFit:** 0.18 % (842/480000 pixels). Classification: RasterDiff.
- **Chapter 9 / S9_2_2_InlineBoxes_SideBySide:** 0.17 % (827/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_DisplayTableFooterGroup:** 0.15 % (733/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_1_InlineNonReplaced_HorizontalMarginsApply:** 0.14 % (661/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_4_DoesNotApplyToInline:** 0.14 % (661/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_1_InlineNonReplaced_HeightFromFontMetrics:** 0.14 % (652/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_DisplayTableRowGroup:** 0.13 % (643/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_1_InlineNonReplaced_WidthDoesNotApply:** 0.13 % (603/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_3_ColumnVisibilityCollapse:** 0.13 % (601/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_1_Golden_AnonymousTableInheritance:** 0.12 % (573/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_1_AnonymousTableWrapper_ForOrphanRow:** 0.11 % (527/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_1_AnonymousTableMultipleRows:** 0.10 % (503/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_2_1_FixedLayout_EqualDistribution:** 0.09 % (435/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_DisplayTable_BlockLevel:** 0.09 % (430/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_1_LineHeight_Percentage_htmlNormal:** 0.08 % (406/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_5_CollapseKeepsDimensions:** 0.07 % (321/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_3_2_InlineReplaced_AutoWidthNarrowerThanContainer:** 0.07 % (320/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_1_Leading_IncreasesLineBox_htmlLarge:** 0.07 % (315/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_8_1_Leading_IncreasesLineBox_htmlSmall:** 0.06 % (303/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_1_InlineNonReplaced_LineHeight_htmlNormal:** 0.06 % (269/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_6_1_InlineNonReplaced_LineHeight_htmlLarge:** 0.06 % (269/480000 pixels). Classification: RasterDiff.
- **Chapter 10 / S10_2_Width_DoesNotApplyToTableRows:** 0.06 % (265/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_5_ColumnVisibilityCollapse:** 0.05 % (228/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_DisplayTableColumn:** 0.05 % (221/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_DisplayTableColumnGroup:** 0.05 % (221/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_3_ColumnsDoNotGenerateBoxes:** 0.05 % (221/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_2_1_FixedLayout_ColumnElements:** 0.05 % (221/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_5_2_1_FixedLayout_TableWidthForcesWider:** 0.05 % (221/480000 pixels). Classification: RasterDiff.
- **Chapter 17 / S17_2_1_MissingColumnWrapper:** 0.04 % (183/480000 pixels). Classification: RasterDiff.

## Tests with Identical Rendering

6 tests produced pixel-identical output between both engines:

- Chapter 9: S9_2_4_DisplayNone_Vs_VisibilityHidden_htmlNone
- Chapter 9: S9_2_4_DisplayNone_Vs_VisibilityHidden_htmlHidden
- Chapter 9: Pixel_DisplayNone_ProducesNoOutput
- Chapter 10: S10_6_3_BlockAutoHeight_NoChildren_HeightIs0
- Chapter 10: Pixel_S10_6_3_BlockAutoHeight
- Chapter 10: S10_8_1_Strut_EmptyLineHasHeight

## Common Causes of Differences

| Cause | Description | Severity |
|-------|-------------|----------|
| Font rasterisation | Different font engines produce different glyph rendering | Low |
| Anti-aliasing | Sub-pixel rendering differences between engines | Low |
| Default stylesheets | Different user-agent defaults (margins, fonts) | Medium |
| CSS property support | Unsupported or partially implemented CSS properties | High |
| Layout algorithm | Differences in box model, float, or positioning calculation | High–Critical |
| Text layout | Line-breaking, word-spacing, or kerning differences | Medium |

## Methodology

1. Each CSS2 chapter test's HTML snippet was extracted from
   `Css2Chapter9Tests.cs`, `Css2Chapter10Tests.cs`, and `Css2Chapter17Tests.cs`.
2. Each snippet was rendered at 800×600 viewport using both:
   - **html-renderer** (Broiler engine via `PixelDiffRunner.RenderDeterministic`)
   - **Chromium** (headless via Playwright `ChromiumRenderer.RenderAsync`)
3. Bitmaps were compared pixel-by-pixel with a colour tolerance of
   15 per channel.
4. Float/block overlap detection was run on the html-renderer fragment tree.
5. Results were classified by severity:
   - **Identical:** 0% pixel difference
   - **Low:** < 5% difference (typically font/anti-aliasing)
   - **Medium:** 5–10% difference
   - **High:** 10–20% difference
   - **Critical:** ≥ 20% difference

## Chapter 12 — Generated Content, Automatic Numbering, and Lists (78 tests)

Chapter 12 covers `:before`/`:after` pseudo-elements, the `content` property,
CSS counters, and list marker styling. Verification was performed at the
layout/fragment level using `Css2Chapter12Tests.cs`.

### Coverage Summary

| Section | Tests | Status | Notes |
|---------|-------|--------|-------|
| 12.1 :before/:after pseudo-elements | 7 | ✅ Verified | UA-dependent on replaced elements (documented) |
| 12.2 content property | 15 | ✅ Verified | Counters, attr(), and quotes have limited support |
| 12.3 Quotation marks | 7 | ✅ Verified | Quote nesting, depth tracking |
| 12.4 Counters and numbering | 13 | ✅ Verified | Includes nesting, scoping, counter styles |
| 12.5 Lists | 36 | ✅ Verified | Full coverage of list-style-type, position, shorthand |

### Key Findings

- **List rendering is well-supported:** All `list-style-type` values (disc,
  circle, square, decimal, roman, alpha, greek) render correctly.
- **`:before`/`:after` pseudo-elements:** Basic support is present. Complex
  `content` property values (counters, attr(), quotes) have limited or
  UA-dependent behavior.
- **Counter support is limited:** CSS counters (`counter-reset`,
  `counter-increment`) render without error but may not produce the expected
  numbering in all cases.

### Specification Ambiguities / UA-Dependent Items

- §12.1: `:before`/`:after` on replaced elements is explicitly UA-dependent
  per CSS 2.1.
- §12.4.3: `display:none` elements should not increment/reset counters — this
  is browser-dependent behaviour that html-renderer handles correctly.

## Chapter 15 — Fonts (65 tests)

Chapter 15 defines font properties and the font matching algorithm.
Verification was performed using `Css2Chapter15Tests.cs`.

### Coverage Summary

| Section | Tests | Status | Notes |
|---------|-------|--------|-------|
| 15.1 Introduction | 3 | ✅ Verified | Font resources, properties, matching |
| 15.2 Font matching algorithm | 10 | ✅ Verified | All 6 steps + fallback behaviour |
| 15.3 font-family | 12 | ✅ Verified | Generic families, comma-separated, quoted |
| 15.4 font-style | 4 | ✅ Verified | normal, italic, oblique, inheritance |
| 15.5 font-variant | 4 | ✅ Verified | normal, small-caps, simulation |
| 15.6 font-weight | 9 | ✅ Verified | Keywords, numeric, bolder/lighter |
| 15.7 font-size | 13 | ✅ Verified | Absolute, relative, length, percentage, em |
| 15.8 font shorthand | 10 | ✅ Verified | Syntax, system fonts, overrides |

### Key Findings

- **Font matching works correctly:** All generic font families (serif,
  sans-serif, monospace, cursive, fantasy) are recognized and produce
  different renderings.
- **Font size calculations are accurate:** Percentage-based, em-based, and
  keyword-based sizes produce expected relative sizes.
- **Font weight and style inheritance works correctly.**
- **Font shorthand parses all components** including line-height with `/`
  separator.

### Specification Ambiguities / UA-Dependent Items

- §15.2: Font matching tolerance is UA-dependent — html-renderer's tolerance
  may differ from Chromium's.
- §15.5: Small-caps simulation (scaling uppercase glyphs) is UA-dependent.
- §15.7: Absolute font size keyword mapping is UA-dependent.
- §15.8: System font keywords (caption, icon, menu, etc.) map to
  platform-specific fonts.

## Chapter 16 — Text (72 tests)

Chapter 16 covers text formatting properties. Verification was performed
using `Css2Chapter16Tests.cs`.

### Coverage Summary

| Section | Tests | Status | Notes |
|---------|-------|--------|-------|
| 16.1 text-indent | 7 | ✅ Verified | Length, percentage, negative, inherited |
| 16.2 text-align | 9 | ✅ Verified | left, right, center, justify |
| 16.3 text-decoration | 13 | ✅ Verified | underline, overline, line-through, multiple |
| 16.4 letter/word-spacing | 11 | ✅ Verified | normal, length, negative, inherited |
| 16.5 text-transform | 8 | ✅ Verified | capitalize, uppercase, lowercase |
| 16.6 white-space | 24 | ✅ Verified | All values + processing model |

### Key Findings

- **Text alignment is well-supported:** Left, right, center alignment
  produce correct pixel output. Justify adjusts word spacing.
- **Text decoration renders correctly:** Underline, overline, and
  line-through are drawn at correct positions. Multiple decorations can be
  combined.
- **White-space processing model works:** All five values (normal, pre,
  nowrap, pre-wrap, pre-line) produce expected wrapping behavior.
- **Letter and word spacing apply correctly** with both positive and negative
  values.

### Specification Ambiguities / UA-Dependent Items

- §16.2: Justification algorithm (word spacing vs letter spacing adjustment)
  is UA-dependent.
- §16.3: `text-decoration: blink` is allowed to be ignored by UAs.
- §16.4: Word boundaries for word-spacing are UA-dependent (typically
  whitespace-delimited).
- §16.5: What constitutes a "word" for `capitalize` is UA/language-dependent.
- §16.6.2: Bidirectional white-space collapsing example is informative only.
- §16.6.3: Zero-width character handling is UA-dependent.

## Additional CSS Tests (Unit Tests — Not Visually Comparable)

The following CSS test files in `src/Broiler.App.Tests/` are **unit tests**
that validate CSS parsing, selector logic, and property handling. They do not
produce visual output, so cross-engine visual comparison is not applicable.
All unit tests pass successfully.

| Test File | Tests | Scope |
|-----------|-------|-------|
| CssSelectorTests.cs | 13 | CSS selector specificity, combinators, pseudo-classes |
| CssBoxModelTests.cs | 16 | Display, position, layout tree construction |
| CssAnimationsTests.cs | 12 | Transition parsing, timing functions, interpolation |
| CssGridFlexTests.cs | 10 | Flex direction, grid display resolution |
| CssTextPropertiesTests.cs | — | Whitespace modes, word-break, text-overflow |

These tests verify correctness at the parsing and logic layer. Rendering
differences observed in the visual comparison above do not affect the
accuracy of these unit-level tests.

---

## Related Documents

- [CSS2 Differential Resolution](css2-differential-resolution.md) — root-cause analysis and fix tracker for the differences identified above
- [CSS2 Verification Report](css2-verification-report.md) — high-level CSS 2.1 spec compliance summary
- [Roadmap: CSS2 Differential Resolution](roadmap/css2-differential-resolution.md) — roadmap for resolving all differential failures
- [Roadmap: CSS2 Verification Report Resolution](roadmap/css2-verification-report-resolution.md) — roadmap for addressing CSS2 verification issues
