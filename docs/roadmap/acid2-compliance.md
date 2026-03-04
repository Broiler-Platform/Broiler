# Roadmap: Acid2 Compliance in html-renderer

> **Scope:** Achieve full visual compliance with the
> [Acid2 CSS conformance test](https://www.webstandards.org/files/acid2/test.html)
> in the html-renderer (Broiler) rendering engine.
>
> **Tracking Issue:** [#303](https://github.com/MaiRat/Broiler/issues/303)
>
> **Related Documents:**
> - [Acid1 Error Resolution](acid1-error-resolution.md) — completed Acid1 fixes.
> - [CSS2 Verification Report Resolution](css2-verification-report-resolution.md)
>   — full CSS2 compliance roadmap (Acid2 is Priority 8).
> - [CSS2 Differential Resolution](css2-differential-resolution.md) — rendering
>   difference fixes.

---

## Current State (2026-03-03)

The Acid2 test page (`acid/acid2/acid2.html`) was rendered by both the
html-renderer engine and headless Chromium (Playwright, v145).  The two
renderings were compared at 1024×768, 30-per-channel colour tolerance.

| Metric | Value |
|--------|-------|
| Viewport | 1024 × 768 |
| Total pixels | 786 432 |
| Different pixels | 57 513 |
| **Pixel diff ratio** | **7.31 %** |
| Diff threshold (current) | 30 % |

### Colour Distribution Comparison

| Colour | Broiler | Chromium | Delta | Notes |
|--------|---------|----------|-------|-------|
| White | 95.26 % | 97.19 % | −1.93 pp | Broiler shows extra non-white artefacts |
| Yellow | 0.48 % | 1.76 % | −1.28 pp | Smiley face largely absent in Broiler |
| Black | 3.23 % | 0.85 % | +2.38 pp | Oversized/misplaced black elements |
| Red | 0.04 % | 0.00 % | +0.04 pp | "ERROR" text and red artefacts visible |
| Blue | 0.15 % | 0.00 % | +0.15 pp | Link text colour bleeding |
| Other | 0.84 % | 0.20 % | +0.64 pp | Anti-aliasing and mixed-colour artefacts |

### Visual Summary

- **Chromium reference:** Classic yellow smiley face on white background —
  all 14 lines of the face render correctly.
- **Broiler rendering:** The intro section renders, but the face is
  heavily broken — scalp, eyes, nose, smile, and chin elements are
  mis-positioned, mis-sized, or missing.  The "ERROR" fallback text from
  the nested `<object>` elements is visible.  The table-based bottom line
  is misplaced.

Reference images:
- `acid/acid2/acid2-reference.png` — Chromium rendering (scrolled to `#top`).
- `acid/acid2/acid2-diff.png` — Pixel-level diff (red = different).

### Hotspot Regions (8×8 grid, most-different first)

| Region | Grid Position | Diff % | Likely Cause |
|--------|---------------|--------|--------------|
| Face centre | (1,2) | 69.1 % | Eyes/forehead CSS (float, fixed bg, objects) |
| Face top | (1,1) | 68.8 % | Scalp fixed positioning, min/max-height |
| Lower face | (2,5) | 60.7 % | Smile/chin margin collapsing, clear |
| Lower left | (1,5) | 49.6 % | Table-row display, anonymous cells |
| Mid face | (1,3) | 35.6 % | Nose float, auto margins |
| Upper left | (0,1) | 30.4 % | Intro border/z-index overlap |

---

## Discrepancy Inventory

Each CSS feature exercised by Acid2 is listed below with its current
compliance status, the relevant CSS2.1 section, and severity.

### Category 1 — Fixed Positioning & Min/Max Dimensions (§9.6.1, §10.4, §10.7)

**Acid2 lines:** Scalp (line 26), containing block for face.

| # | Feature | CSS2.1 § | Expected Behaviour | Observed Behaviour | Severity |
|---|---------|----------|--------------------|--------------------|----------|
| 1.1 | `position: fixed` top/left | §9.6.1 | Scalp bar fixed at 9em/11em | Positioned but dimensions wrong | High |
| 1.2 | `max-width` clamping | §10.4 | Width clamped to 4em | Overly wide bar | High |
| 1.3 | `min-height` overrides `max-height` | §10.7 | Height = 1em (min-height wins) | Height incorrect | High |
| 1.4 | `overflow: hidden` on viewport | §11.1.1 | No scrollbars | Handled correctly | ✅ OK |

**Root Cause:** `min-height`/`max-height` precedence rule (§10.7: min-height
wins when min > max) is not fully implemented.  `max-width` on
percentage-specified fixed elements also resolves incorrectly.

### Category 2 — Float Layout & Shrink-to-Fit (§9.5, §10.3.5)

**Acid2 lines:** Second line (attribute selectors + float), nose (float with
auto margins).

| # | Feature | CSS2.1 § | Expected | Observed | Severity |
|---|---------|----------|----------|----------|----------|
| 2.1 | `float: right` inside abs-pos | §9.5.1 | 48×12 yellow bar | Mispositioned | High |
| 2.2 | Shrink-to-fit abs-pos containing block | §10.3.5 | Wraps around float | Width incorrect | High |
| 2.3 | `float: left` with negative margins | §9.5.1 | Nose centred at 2em margin | Mis-positioned | Medium |
| 2.4 | `float: inherit` | §6.2 | Smile inherits `float: right` | Not inherited | Critical |

**Root Cause:** Float placement rules 1–9 (§9.5.1) have known gaps,
particularly rule 6 (no overlap with block-level boxes).  The
`CollectPrecedingFloatsInBfc()` fix from Acid1 resolved basic cases but
Acid2's nested float inheritance and abs-pos interaction remain broken.

### Category 3 — Selectors & Specificity (§5–6)

**Acid2 lines:** Attribute selectors (line 34–35), class combinators (line 42),
adjacent sibling combinator (line 30), parser tests (lines 94–102).

| # | Feature | CSS2.1 § | Expected | Observed | Severity |
|---|---------|----------|----------|----------|----------|
| 3.1 | `[class~=one]` attribute selector | §5.8.1 | Matches `.first.one` element | Likely works | Low |
| 3.2 | Adjacent sibling `p + table + p` | §5.7 | Third `<p>` hidden under table | May be visible | Medium |
| 3.3 | `* html .parser` selector | §5.7 | Should NOT match (no element above `html`) | Should be OK | Low |
| 3.4 | CSS parser error recovery (lines 94–102) | §4.1.7, §4.2 | Invalid declarations ignored | Partially handled | Medium |

### Category 4 — Generated Content & Pseudo-Elements (§12)

**Acid2 lines:** Nose `:before`/`:after` (lines 67–68), smile decorations.

| # | Feature | CSS2.1 § | Expected | Observed | Severity |
|---|---------|----------|----------|----------|----------|
| 4.1 | `::before` with `content: ''` | §12.1 | Empty generated box with borders | Partially works | Medium |
| 4.2 | `::after` with border triangle | §12.1 | Triangle-shaped nose element | Missing or malformed | High |
| 4.3 | `:hover` pseudo-class interaction | §5.11.3 | Colour change on hover | Not applicable (static render) | — |
| 4.4 | `border-color: inherit` on pseudo | §6.2 | Inherits from parent on hover | N/A for static | — |

### Category 5 — Paint Order & Stacking (Appendix E, §9.9)

**Acid2 lines:** Eyes section (lines 52–58) — three layers with specific
paint order: block (#eyes-c) → float (#eyes-b) → inline (#eyes-a).

| # | Feature | CSS2.1 § | Expected | Observed | Severity |
|---|---------|----------|----------|----------|----------|
| 5.1 | Block paints below float | App. E | Yellow block under float | Incorrect order | Critical |
| 5.2 | Float paints below inline | App. E | Float under inline content | Incorrect order | Critical |
| 5.3 | `background: fixed` on float | §14.2.1 | Fixed background tiling | Not rendered | High |
| 5.4 | Nested `<object>` fallback | HTML4 | Inner object renders (PNG) | "ERROR" text visible | Critical |

**Root Cause:** The paint order for the three-layer test (block/float/inline)
requires strict Appendix E compliance.  The html-renderer's `PaintWalker` →
`DisplayList` → `RGraphicsRasterBackend` pipeline may not separate float and
inline painting phases correctly.  The `<object>` fallback chain also fails,
showing the "ERROR" text instead of the intended PNG.

### Category 6 — Table Rendering (§17)

**Acid2 lines:** Bottom line of face (lines 105–115), anonymous table cells,
image height test.

| # | Feature | CSS2.1 § | Expected | Observed | Severity |
|---|---------|----------|----------|----------|----------|
| 6.1 | `display: table` on `<ul>` | §17.2 | Table-rendered list items | Mispositioned | High |
| 6.2 | `display: table-cell` on `<li>` | §17.2.1 | Cells side by side | Stacked vertically | Critical |
| 6.3 | Anonymous table cell wrapping | §17.2.1 | Non-cell children wrapped | Not wrapped | High |
| 6.4 | Row height stretching | §17.5.3 | 0.5em cell stretched to 1em | Incorrect height | Medium |
| 6.5 | `border-spacing: 0` | §17.6.1 | No gaps between cells | Extra spacing | Low |

**Root Cause:** The table layout algorithm in `CssTable.cs` handles basic
tables but does not generate anonymous table objects (§17.2.1) for
`display: table-cell` without an enclosing `display: table-row`.

### Category 7 — Margin Collapsing & Clearance (§8.3.1, §9.5.2)

**Acid2 lines:** Empty div (line 71), smile clearance (line 73).

| # | Feature | CSS2.1 § | Expected | Observed | Severity |
|---|---------|----------|----------|----------|----------|
| 7.1 | Empty block margin collapsing | §8.3.1:7 | Own margins collapse (height 0) | Likely works | Low |
| 7.2 | `clear: both` with neg clearance | §9.5.2 | Smile positioned correctly | Mispositioned | Critical |
| 7.3 | Percentage height → auto | §10.5 | `height: 10%` becomes auto when CB has auto height | May not resolve | Medium |

**Root Cause:** Negative clearance computation (§8.3.1 + §9.5.2) is a known
gap.  The margin collapsing algorithm does not handle the interaction
between `clear`, negative margins, and preceding floats correctly.

### Category 8 — Background & Data URI Images (§14, §5)

**Acid2 lines:** Forehead (line 38), eyes (lines 56–57), chin (line 85).

| # | Feature | CSS2.1 § | Expected | Observed | Severity |
|---|---------|----------|----------|----------|----------|
| 8.1 | `data:image/png` in `background` | §14.2.1 | 1×1 yellow pixel background | Rendered as red (fallback) | High |
| 8.2 | `background: fixed` with offset | §14.2.1 | Fixed tiled background | Not tiled correctly | High |
| 8.3 | Preferred stylesheet (`<link rel>`) | §6.4.1 | `.picture { background: none }` applied | Background may show red | Critical |
| 8.4 | Data URI in `<object>` `data` attr | HTML4 | PNG image decoded inline | Fallback to ERROR text | Critical |

**Root Cause:** The `<link rel="appendix stylesheet">` tag (line 118 of
acid2.html) applies a preferred stylesheet that overrides `.picture`
background from red to none.  If the html-renderer ignores `rel` attributes
on `<link>`, the red background shows through, causing major visual errors.

### Category 9 — HTML Parsing & Error Recovery (§4)

**Acid2 lines:** HTML comment test (line 136), `<table>` closing `<p>`
(line 128).

| # | Feature | CSS2.1 § | Expected | Observed | Severity |
|---|---------|----------|----------|----------|----------|
| 9.1 | `<table>` closes `<p>` per HTML4 DTD | HTML4 | Two separate `<p>` elements | Likely correct | Low |
| 9.2 | Comment parsing (`<!-- →ERROR<!- -->`) | HTML4 | "ERROR" text inside comment | Likely correct | Low |
| 9.3 | CSS parser error recovery | §4.1.7 | Invalid properties ignored | Partially correct | Medium |

### Category 10 — Overflow, Width, & Line-Height (§10, §11)

**Acid2 lines:** Forehead (line 39), chin (lines 85–86).

| # | Feature | CSS2.1 § | Expected | Observed | Severity |
|---|---------|----------|----------|----------|----------|
| 10.1 | Child wider than parent (`overflow`) | §11.1.1 | Content clips to parent | May overflow | Medium |
| 10.2 | `line-height: 1em` with `font: 2px/4px` | §10.8.1 | Precise line box height | Font metrics differ | Low |
| 10.3 | `display: inline` on block child | §9.2.4 | Inline formatting | Likely works | Low |

---

## Remediation Plan

Issues are organized into milestones ordered by impact, dependency, and
implementation complexity.  Each milestone maps to one or more discrepancy
categories above.

### Milestone 1 — Preferred Stylesheet & Object Fallback (Quick Wins)

**Target:** Remove the two most visible artefacts — the red `.picture`
background and the "ERROR" fallback text.

**Estimated Effort:** Small (1–2 days).

| Task | Category | Deliverable |
|------|----------|-------------|
| T1.1 | 8.3 | Support `<link rel="... stylesheet">` with non-standard `rel` values containing "stylesheet" |
| T1.2 | 8.4, 5.4 | Implement `<object>` fallback chain: try `data` attr, fall back to nested content |
| T1.3 | 8.1 | Verify `data:image/png` base64 decoding in CSS `background` property |

**Success Metric:** No red background on `.picture`; no "ERROR" text visible.

**Tests:**
- `Acid2DifferentialTests.Acid2Test_DifferentialBaseline()` — pixel diff should
  decrease significantly.
- New unit test: preferred stylesheet resolution.
- New unit test: `<object>` fallback rendering.

### Milestone 2 — Paint Order Compliance (Appendix E)

**Target:** Correct the three-layer paint order for the eyes section.

**Estimated Effort:** Medium (2–3 days).

| Task | Category | Deliverable |
|------|----------|-------------|
| T2.1 | 5.1, 5.2 | Implement Appendix E paint order: blocks → floats → inline content |
| T2.2 | 5.3 | Support `background-attachment: fixed` on floats |
| T2.3 | 5.1 | Add paint-order integration test with three-layer test case |

**Success Metric:** Eyes section renders as solid yellow rectangle with
black borders and white eye holes.

**Dependencies:** T1.2 (object fallback) must be complete for eyes to render
the PNG instead of ERROR.

### Milestone 3 — Table Display & Anonymous Cell Generation (§17.2.1)

**Target:** Fix the bottom line of the face.

**Estimated Effort:** Medium (2–3 days).

| Task | Category | Deliverable |
|------|----------|-------------|
| T3.1 | 6.1, 6.2 | Handle `display: table` / `display: table-cell` on non-table elements |
| T3.2 | 6.3 | Generate anonymous table cells for non-cell children of table rows |
| T3.3 | 6.4 | Implement row height stretching for cells with `height < row-height` |

**Success Metric:** Four black squares in a horizontal row at the bottom
of the face.

### Milestone 4 — Fixed Positioning & Min/Max Dimension Precedence (§10.7)

**Target:** Fix the scalp line dimensions.

**Estimated Effort:** Small (1–2 days).

| Task | Category | Deliverable |
|------|----------|-------------|
| T4.1 | 1.2 | Enforce `max-width` on fixed-position elements with percentage widths |
| T4.2 | 1.3 | Implement §10.7 rule: `min-height` overrides `max-height` when min > max |
| T4.3 | 1.1 | Verify fixed position coordinate resolution (em units) |

**Success Metric:** Scalp renders as a thin black bar with yellow bottom
border, 4em wide × 1em high.

### Milestone 5 — Margin Collapsing & Negative Clearance (§8.3.1, §9.5.2)

**Target:** Fix smile/chin vertical positioning.

**Estimated Effort:** High (3–5 days).

| Task | Category | Deliverable |
|------|----------|-------------|
| T5.1 | 7.2 | Implement negative clearance computation per §9.5.2 |
| T5.2 | 7.1 | Verify empty-block margin collapsing (§8.3.1:7) |
| T5.3 | 7.3 | Resolve percentage height to auto when containing block has auto height |

**Success Metric:** Smile and chin correctly positioned below the nose.

**Dependencies:** Float layout (Milestone 6) affects clearance calculations.

### Milestone 6 — Float Inheritance & Nested Float Layout (§6.2, §9.5.1)

**Target:** Fix nose and smile sub-elements.

**Estimated Effort:** High (3–5 days).

| Task | Category | Deliverable |
|------|----------|-------------|
| T6.1 | 2.4 | Support `float: inherit` value |
| T6.2 | 2.1, 2.2 | Fix shrink-to-fit width for abs-pos blocks containing floats |
| T6.3 | 2.3 | Correct negative margin interaction with floats |

**Success Metric:** Nose renders as black-bordered rectangle with yellow
interior; smile renders correctly below.

### Milestone 7 — Generated Content Refinement (§12)

**Target:** Fix nose/smile decorative borders via `::before`/`::after`.

**Estimated Effort:** Medium (2–3 days).

| Task | Category | Deliverable |
|------|----------|-------------|
| T7.1 | 4.1, 4.2 | Ensure `::before`/`::after` with `content: ''` generates boxes with border/padding |
| T7.2 | 4.1 | Support `display: block` on generated content |
| T7.3 | 4.2 | Verify border-triangle technique (`border-width` with transparent sides) |

**Success Metric:** Nose triangles and smile borders render correctly.

### Milestone 8 — CSS Parser Hardening & Selector Edge Cases (§4, §5)

**Target:** Pass all parser and selector tests embedded in Acid2.

**Estimated Effort:** Small (1–2 days).

| Task | Category | Deliverable |
|------|----------|-------------|
| T8.1 | 3.2 | Verify `p + table + p` adjacent sibling matching |
| T8.2 | 3.4 | CSS parser ignores invalid property values (e.g., `width: 200`, `border: ... ! error`) |
| T8.3 | 9.3 | Escaped identifiers and backslash handling in CSS |

**Success Metric:** Parser test line (line 13 of face) shows as yellow
bar with black side borders, no red/maroon.

### Milestone 9 — Background Attachment: Fixed & Overflow Clipping

**Target:** Final polish for forehead and chin elements.

**Estimated Effort:** Medium (2 days).

| Task | Category | Deliverable |
|------|----------|-------------|
| T9.1 | 8.2, 5.3 | `background-attachment: fixed` with pixel offset |
| T9.2 | 10.1 | `overflow` clipping when child is wider than parent |
| T9.3 | 10.2 | Fine-tune `line-height` / `font` shorthand parsing for fractional px |

**Success Metric:** Forehead and chin render as yellow bars with correct
black side borders.

### Milestone 10 — Full Acid2 Pass & CI Integration

**Target:** Pixel diff ≤ 5 % (font rasterisation tolerance); CI gate.

**Estimated Effort:** Small (1 day).

| Task | Category | Deliverable |
|------|----------|-------------|
| T10.1 | All | Lower `DiffThreshold` in `Acid2DifferentialTests` from 0.30 to 0.05 |
| T10.2 | All | Add Acid2 full-page test to CI pipeline |
| T10.3 | All | Update `acid2-reference.png` with final Chromium baseline |
| T10.4 | All | Document remaining font-rasterisation differences as accepted |

**Success Metric:** `Acid2DifferentialTests.Acid2Test_DifferentialBaseline()`
passes at ≤ 5 % threshold in CI.

---

## Phased Timeline

| Phase | Timeline | Milestones | Key Deliverables | Status |
|-------|----------|------------|------------------|--------|
| 1 | Immediate | M1 | Stylesheet resolution, object fallback, data URI | ✅ Complete |
| 2 | Short-term | M2, M3, M4 | Paint order, table display, min/max dims | ✅ Complete |
| 3 | Medium-term | M5, M6, M7 | Margin collapsing, float inheritance, gen content | ⬜ Pending |
| 4 | Long-term | M8, M9, M10 | Parser hardening, bg:fixed, CI gate at ≤ 5 % | ⬜ Pending |

---

## CSS2.1 Section Coverage Matrix

The following table maps every CSS2.1 section exercised by Acid2 to the
corresponding milestone and current compliance status.

| CSS2.1 § | Feature | Acid2 Line | Milestone | Status |
|----------|---------|------------|-----------|--------|
| §4.1.7 | CSS parser error recovery | 94–102 | M8 | ⬜ Partial |
| §5.7 | Adjacent sibling combinator (`+`) | 30–31 | M8 | ⬜ Untested |
| §5.8.1 | Attribute selectors | 34–35 | M8 | ✅ Likely OK |
| §6.2 | `float: inherit` | 81 | M6 | ⬜ Not Implemented |
| §8.3.1 | Margin collapsing (empty blocks) | 71 | M5 | ⬜ Partial |
| §8.5 | Border shorthand / transparency | 77, 80 | M7 | ⬜ Partial |
| §9.5.1 | Float positioning rules | 34, 61 | M6 | ⬜ Partial |
| §9.5.2 | Clear with negative clearance | 73 | M5 | ⬜ Not Implemented |
| §9.6.1 | Fixed positioning | 26 | M4 | ⬜ Partial |
| §9.9 | Stacking contexts / z-index | 14, 52–58 | M2 | ⬜ Partial |
| §10.3.5 | Shrink-to-fit width | 34 | M6 | ⬜ Partial |
| §10.4 | `max-width` | 26 | M4 | ⬜ Partial |
| §10.5 | Percentage height → auto | 71 | M5 | ⬜ Untested |
| §10.7 | `min-height` overrides `max-height` | 26 | M4 | ⬜ Not Implemented |
| §10.8.1 | `line-height` computation | 85–86 | M9 | ⬜ Partial |
| §11.1.1 | `overflow: hidden` | 10, 38–39 | M9 | ✅ OK |
| §12.1 | `::before` / `::after` content | 67–68 | M7 | ⬜ Partial |
| §14.2.1 | `background-attachment: fixed` | 56–57, 85 | M9 | ⬜ Not Implemented |
| §17.2 | `display: table` on non-table elements | 105–110 | M3 | ⬜ Partial |
| §17.2.1 | Anonymous table cell generation | 108, 110 | M3 | ⬜ Not Implemented |
| §17.5.3 | Row height distribution | 109 | M3 | ⬜ Partial |
| App. E | Paint order (block/float/inline) | 52–58 | M2 | ⬜ Not Implemented |

---

## Required Resources and Dependencies

| Milestone | Expertise Needed | External Dependency | Notes |
|-----------|-----------------|---------------------|-------|
| M1 | HTML parsing, CSS cascade | None | Quick wins with high visual impact |
| M2 | Paint architecture (`PaintWalker`) | None | Requires `DisplayList` phase changes |
| M3 | Table layout (`CssTable.cs`) | None | Anonymous object generation |
| M4 | Box model (`CssBox.cs`) | None | Dimension constraint resolution |
| M5 | Layout engine (margin algorithm) | None | Complex; high regression risk |
| M6 | Float layout engine | None | Extends Acid1 float fixes |
| M7 | Generated content (`CssBox`) | None | Pseudo-element box generation |
| M8 | CSS parser (`CssParser.cs`) | None | Error recovery edge cases |
| M9 | Background rendering | None | Fixed-attachment implementation |
| M10 | CI/CD | Playwright/Chromium | Threshold adjustment and gating |

---

## Related Tests

| Test File | Project | Validates |
|-----------|---------|-----------|
| `Acid2DifferentialTests.cs` | `HtmlRenderer.Image.Tests` | Acid2 pixel-level comparison |
| `Acid2NavigationTests.cs` | `Broiler.Cli.Tests` | Link following and rendering |
| `Css2Chapter8Tests.cs` | `HtmlRenderer.Image.Tests` | Box model (margins, borders) |
| `Css2Chapter9Tests.cs` | `HtmlRenderer.Image.Tests` | Visual formatting (floats, positioning) |
| `Css2Chapter10Tests.cs` | `HtmlRenderer.Image.Tests` | Dimension details (min/max) |
| `Css2Chapter11Tests.cs` | `HtmlRenderer.Image.Tests` | Visual effects (overflow, clip) |
| `Css2Chapter12Tests.cs` | `HtmlRenderer.Image.Tests` | Generated content |
| `Css2Chapter17Tests.cs` | `HtmlRenderer.Image.Tests` | Tables |

---

## Progress Tracking

Each milestone's completion will be tracked by:

1. Updating the status column in the Remediation Plan tables above.
2. Re-running `Acid2DifferentialTests` and recording the pixel diff.
3. Recording before/after diff ratios in the Milestones table.
4. Creating an ADR for significant rendering algorithm changes.
5. Updating `acid/acid2/acid2-reference.png` when the Chromium baseline
   changes.

### Completion Criteria

This roadmap is complete when:

- **Pixel diff ≤ 5 %** between html-renderer and Chromium for the full
  Acid2 test page (font rasterisation tolerance).
- All 10 milestones are marked ✅ Complete.
- `Acid2DifferentialTests.Acid2Test_DifferentialBaseline()` passes at
  the 5 % threshold in CI.
- All rendering algorithm changes are documented with ADRs.
- Remaining differences are documented and categorised as accepted
  (font rasterisation) or deferred (with justification).
