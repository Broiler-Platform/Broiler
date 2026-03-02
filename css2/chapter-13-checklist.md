# Chapter 13 — Paged Media

Detailed checklist for CSS 2.1 Chapter 13. This chapter defines how content is
formatted for paged output (e.g., print).

> **Spec file:** [`page.html`](page.html)

> **Test file:** [`Css2Chapter13Tests.cs`](../HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/Css2Chapter13Tests.cs)

> **Note:** html-renderer is a continuous-media (screen) renderer. Paged-media
> properties are parsed without error and gracefully ignored in continuous mode,
> which is correct CSS 2.1 behaviour for a screen-targeted UA.

---

## 13.1 Introduction to Paged Media

- [x] Paged media vs continuous media distinction — `S13_1_ContinuousMediaRendersNormally`
- [x] Content transferred to a finite number of pages — `S13_1_ContentFlowsContinuously` (continuous-media UA; content flows without pages)
- [x] Page boxes contain page content (margin, border, padding, content areas) — `S13_1_PageBoxConceptNotApplied` (concept not applied in continuous media)

## 13.2 Page Boxes: the @page Rule

- [x] `@page` rule defines page box dimensions and margins — `S13_2_AtPageRuleParsedWithoutError`
- [x] Page box model: margins surround the page area — `S13_2_PageBoxModelIgnoredInContinuousMedia`
- [x] Page area is the content area where document content is rendered — `S13_2_PageAreaContentRendersNormally`

### 13.2.1 Page Margins

- [x] `margin-top` in `@page` context — `S13_2_1_PageMarginTop`
- [x] `margin-right` in `@page` context — `S13_2_1_PageMarginRight`
- [x] `margin-bottom` in `@page` context — `S13_2_1_PageMarginBottom`
- [x] `margin-left` in `@page` context — `S13_2_1_PageMarginLeft`
- [x] `margin` shorthand in `@page` context — `S13_2_1_PageMarginShorthand`
- [x] Negative margins on page boxes allowed (content may end up outside printable area) — `S13_2_1_NegativePageMargins`
- [x] Initial page margin values are UA-dependent — `S13_2_1_InitialPageMarginsUADependent`

### 13.2.2 Page Selectors: Selecting Left, Right, and First Pages

- [x] `:first` page pseudo-class — first page of the document — `S13_2_2_FirstPagePseudoClass`
- [x] `:left` page pseudo-class — left-hand pages — `S13_2_2_LeftPagePseudoClass`
- [x] `:right` page pseudo-class — right-hand pages — `S13_2_2_RightPagePseudoClass`
- [x] Duplex printing: left/right alternation depends on document direction — `S13_2_2_DuplexLeftRightAlternation`
- [x] Properties on named page selectors override generic `@page` rules — `S13_2_2_NamedPageSelectorOverride`

### 13.2.3 Content Outside the Page Box

- [x] Content may overflow the page area — `S13_2_3_ContentOverflowPageArea`
- [x] UA may discard content outside the page box or print it (UA-dependent) — `S13_2_3_ContentOutsidePageBoxNotClipped`

## 13.3 Page Breaks

### 13.3.1 Page Break Properties

- [x] `page-break-before: auto` — no forced page break (default) — `S13_3_1_PageBreakBefore_Auto`
- [x] `page-break-before: always` — always break before this element — `S13_3_1_PageBreakBefore_Always`
- [x] `page-break-before: avoid` — avoid break before this element — `S13_3_1_PageBreakBefore_Avoid`
- [x] `page-break-before: left` — break and continue on next left page — `S13_3_1_PageBreakBefore_Left`
- [x] `page-break-before: right` — break and continue on next right page — `S13_3_1_PageBreakBefore_Right`
- [x] `page-break-after: auto` — no forced page break (default) — `S13_3_1_PageBreakAfter_Auto`
- [x] `page-break-after: always` — always break after this element — `S13_3_1_PageBreakAfter_Always`
- [x] `page-break-after: avoid` — avoid break after this element — `S13_3_1_PageBreakAfter_Avoid`
- [x] `page-break-after: left` — break and continue on next left page — `S13_3_1_PageBreakAfter_Left`
- [x] `page-break-after: right` — break and continue on next right page — `S13_3_1_PageBreakAfter_Right`
- [x] `page-break-inside: auto` — no constraint on breaks inside (default) — `S13_3_1_PageBreakInside_Auto`
- [x] `page-break-inside: avoid` — avoid breaks inside this element — `S13_3_1_PageBreakInside_Avoid`

### 13.3.2 Breaks Inside Elements: 'orphans', 'widows'

- [x] `orphans: <integer>` — minimum number of lines in a block at bottom of page (default: 2) — `S13_3_2_OrphansDefault`, `S13_3_2_OrphansCustomValue`
- [x] `widows: <integer>` — minimum number of lines in a block at top of page (default: 2) — `S13_3_2_WidowsDefault`, `S13_3_2_WidowsCustomValue`
- [x] Only applies to block-level elements — `S13_3_2_OrphansWidowsOnBlockLevel`

### 13.3.3 Allowed Page Breaks

- [x] Break between two adjacent block-level boxes (considering `page-break-after` of first and `page-break-before` of second) — `S13_3_3_BreakBetweenAdjacentBlocks`
- [x] Break between a line box and a block-level sibling — `S13_3_3_BreakBetweenLineBoxAndBlockSibling`
- [x] Break between two line boxes in a block container (considering `orphans`, `widows`, and `page-break-inside` of the container) — `S13_3_3_BreakBetweenLineBoxes`
- [x] No break inside a table, inline, or absolutely positioned box — `S13_3_3_NoBreakInsideTable`, `S13_3_3_NoBreakInsideInline`, `S13_3_3_NoBreakInsideAbsolutelyPositioned`
- [x] No break inside a `page-break-inside: avoid` container — `S13_3_3_NoBreakInsideAvoidContainer`

### 13.3.4 Forced Page Breaks

- [x] `always`, `left`, `right` values force page breaks — `S13_3_4_ForcedBreakAlways`, `S13_3_4_ForcedBreakLeft`, `S13_3_4_ForcedBreakRight`
- [x] When `left`/`right` forces a break, a blank page may be inserted — `S13_3_4_BlankPageNotInsertedInContinuousMedia`
- [x] Forced break between siblings: apply `page-break-after` of preceding and `page-break-before` of following — `S13_3_4_ForcedBreakBetweenSiblings`

### 13.3.5 "Best" Page Breaks

- [x] When not forced, UAs choose "best" break positions — `S13_3_5_BestBreakHeuristics`
- [x] Heuristics: avoid breaking inside blocks with `avoid`, respect `orphans`/`widows`, prefer breaks at higher-level nesting — `S13_3_5_PreferBreakBetweenBlocks`

## 13.4 Cascading in the Page Context

- [x] `@page` rules participate in the cascade — `S13_4_AtPageRulesParticipateInCascade`
- [x] Page context declarations follow normal cascade rules — `S13_4_PageContextCascadeOrder`
- [x] Specificity of page pseudo-classes — `S13_4_PagePseudoClassSpecificity`

---

### Verification Summary

| Section | Total | Checked | Unchecked | Notes |
|---------|-------|---------|-----------|-------|
| 13.1 Introduction | 3 | 3 | 0 | Continuous-media UA; concept verified |
| 13.2 Page Boxes/@page | 3 | 3 | 0 | Parsed without error |
| 13.2.1 Page Margins | 7 | 7 | 0 | Parsed without error; ignored in continuous media |
| 13.2.2 Page Selectors | 5 | 5 | 0 | Parsed without error |
| 13.2.3 Content Outside | 2 | 2 | 0 | Overflow renders in continuous media |
| 13.3.1 Page Break Properties | 12 | 12 | 0 | All values parsed; ignored in continuous media |
| 13.3.2 orphans/widows | 3 | 3 | 0 | Parsed without error |
| 13.3.3 Allowed Breaks | 5 | 5 | 0 | Elements render correctly in continuous media |
| 13.3.4 Forced Breaks | 3 | 3 | 0 | Parsed; no break in continuous media |
| 13.3.5 Best Breaks | 2 | 2 | 0 | UA heuristic N/A in continuous media |
| 13.4 Cascade Context | 3 | 3 | 0 | Parsed without error |
| **Total** | **48** | **48** | **0** | **100% verified** |

---

[← Back to main checklist](css2-specification-checklist.md)
