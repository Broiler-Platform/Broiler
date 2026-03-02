# Roadmap: Resolve Differences Between html-renderer and Chromium

> **Scope:** Fix all rendering discrepancies between the html-renderer engine
> (Broiler) and headless Chromium (Playwright) documented in
> [`css2-differential-verification.md`](../css2-differential-verification.md).
>
> **Tracking Issue:** [#191](https://github.com/MaiRat/Broiler/issues/191)
>
> **Previous Work:**
> - [ADR-009](../adr/009-acid1-differential-testing.md) — Acid1 differential
>   testing with Chromium.
> - [Acid1 Error Resolution Roadmap](acid1-error-resolution.md) — all four
>   Acid1 priorities completed.
> - [CSS2 Differential Verification](../css2-differential-verification.md) —
>   pixel-by-pixel comparison of 280 CSS2 chapter tests.

---

## Current State (2026-03-01)

Differential verification ran 280 tests at 800×600 viewport, 5 %
pixel-diff threshold, 15-per-channel colour tolerance, 2 px layout
tolerance.

| Metric           | Count |
|------------------|-------|
| Total tests      | 280   |
| Identical (0 %)  | 6     |
| Pass (≤ 5 %)     | 154   |
| Fail (> 5 %)     | 126   |
| Errors           | 0     |

### Per-Chapter Summary

| Chapter | Total | Pass | Fail | Avg Diff  |
|---------|-------|------|------|-----------|
| 9       | 50    | 14   | 36   | 67.73 %   |
| 10      | 135   | 53   | 82   | 58.67 %   |
| 17      | 95    | 87   | 8    | 4.43 %    |

### Severity Distribution

| Severity   | Count | Description                                 |
|------------|-------|---------------------------------------------|
| Identical  | 6     | 0 % pixel diff — engines match exactly      |
| Low        | 148   | < 5 % — font rasterisation / anti-aliasing  |
| Medium     | 5     | 5–10 % — moderate rendering difference      |
| High       | 2     | 10–20 % — significant difference            |
| Critical   | 119   | ≥ 20 % — major layout or rendering bug      |

---

## Root-Cause Analysis

Each failing test was classified into one of the following root causes
based on manual inspection of the rendered outputs and the
[Key Findings](../css2-differential-verification.md#key-findings).

### 1. User-Agent Stylesheet Differences (116 Critical tests)

**Description:** Chromium applies default `body { margin: 8px }` and
background propagation rules per the
[HTML specification §15 (Rendering)](https://html.spec.whatwg.org/multipage/rendering.html#the-page).
html-renderer does not apply `body` margins by default, causing all
block-level coloured boxes to fill different viewport regions.

**Impact:** This single root cause accounts for **92 % of all Critical
failures** (116 of 119). Fixing it would move the majority of Critical
tests into the Pass category without any layout algorithm changes.

**Affected Chapters:** 9 (32 tests), 10 (81 tests), 17 (3 tests).

### 2. Table Layer Background Rendering (3 Critical tests)

**Description:** Three Chapter 17 tests
(`S17_5_1_Layer1_TableBackground`, `S17_5_1_Layer5_RowBackground`,
`S17_5_1_Layer6_CellBackground`) render table-layer backgrounds
(table → row-group → row → cell) differently. html-renderer does not
propagate background colours through the six-layer table painting model
described in CSS2.1 §17.5.1.

**Impact:** 3 Critical tests isolated to table background propagation.

### 3. Float Overlap Issues (6 tests with overlaps)

**Description:** Six tests report float/block overlap warnings, indicating
that html-renderer places floated elements in positions where they
overlap with block-level content. This violates CSS2.1 §9.5 float
placement rules.

**Tests:** `S9_5_1_ContentFlowsAroundFloat`,
`S9_8_ComparisonExample_AllPositioningSchemes`,
`S10_3_5_Golden_FloatShrinkToFit`,
`S10_6_6_InlineBlock_AutoHeightIncludesFloats`,
`S10_6_6_Golden_OverflowHiddenWithFloat`,
`S10_6_7_BFCRoot_AutoHeightIncludesFloats`,
`S10_6_7_BFCRoot_FloatTallerThanContent`,
`S10_6_7_BFCRoot_ContentTallerThanFloat`.

**Impact:** Float placement inaccuracies can cascade into layout errors
for any page that uses floats.

### 4. Table Height Distribution (2 High tests)

**Description:** `S17_5_3_PercentageHeight` (12.54 %) and
`S17_5_3_ExtraHeightDistributed` (18.77 %) show differences in how
html-renderer distributes extra height among table rows per
CSS2.1 §17.5.3.

**Impact:** Tables with explicit or percentage heights render with
noticeably different row heights.

### 5. Medium-Severity Rendering Differences (5 tests)

| Test | Diff | Root Cause |
|------|------|------------|
| S9_7_FloatAdjustsDisplay | 6.72 % | `display` adjustment when `float` is set (§9.7) |
| S10_8_2_VerticalAlign_TableCell | 6.55 % | Vertical alignment in table cells |
| S17_Integration_MixedHtmlCssTable | 6.21 % | Mixed HTML/CSS table rendering |
| S17_Integration_Golden_ComplexTable | 5.78 % | Complex multi-feature table |
| S17_5_3_MinimumRowHeight | 5.01 % | Minimum row height algorithm |

### 6. Font Rasterisation Differences (148 Low tests)

**Description:** Tests with < 5 % differences are caused by cross-engine
font rasterisation and anti-aliasing. These are expected, irreducible
differences between the GDI+-based html-renderer pipeline and
Chromium's Skia-based pipeline.

**Impact:** Acceptable. No action required.

---

## Prioritised Fix Plan

Fixes are ordered by impact (number of tests resolved) and feasibility
(estimated effort and risk).

### Priority 1 — User-Agent Stylesheet Alignment ✅

**Goal:** Apply Chromium-compatible UA defaults to eliminate the dominant
source of Critical failures.

**Scope:** 116 Critical tests → expected to move to Pass or Low.

**Tasks:**

- [x] Add `body { margin: 8px }` to the html-renderer default stylesheet
  (`HtmlRenderer.Core/Core/CssDefaults.cs`, line 28).
- [x] Add UA-stylesheet background propagation rules for `<html>` and
  `<body>` per [CSS Backgrounds §2.11.2](https://www.w3.org/TR/css-backgrounds-3/#special-backgrounds).
  Implemented in `PaintWalker.FindCanvasBackground()` and
  `PaintWalker.EmitCanvasBackground()` — the source fragment is passed as
  `propagatedFrom` to suppress double-painting.
- [x] **Resolved:** Implemented option (b) — `Css2TestSnippets.All()`
  now applies `EnsureBodyWrapper()` which wraps bare HTML snippets in
  `<html><body>…</body></html>` when neither `<body` nor `<html` tags are
  present. Snippets with explicit wrappers are returned unchanged. This
  aligns both engines so the UA default `body { margin: 8px }` applies
  consistently during differential comparison.
- [x] Verify that the default margin does not break existing Broiler
  rendering (run full Acid1 + CSS2 chapter test suites).
  All 1451 existing tests pass; 6 new `Css2TestSnippetsTests` verify
  the wrapping logic.
- [ ] Re-run the differential verification suite and update
  `css2-differential-verification.md` with new results.

**Examples of affected tests (from verification data):**

| Test | Diff | Chapter | Notes |
|------|------|---------|-------|
| S9_2_4_DisplayBlock | 97.92 % | 9 | Block-only, no text, pure margin offset |
| S10_2_Width_Auto_Block | 97.50 % | 10 | Width calculation shifted by 8 px margins |
| S10_5_Height_ExplicitLength | 93.75 % | 10 | Height rendering offset by body margin |
| S9_5_1_FloatLeft_TouchesContainingBlockEdge | 98.96 % | 9 | Float edge position differs by 8 px |

**Estimated Effort:** Small–Medium — fragment wrapper injection or
test-level fix + regression testing.

**Risk:** Low — the UA stylesheet change is already in place; remaining
work is aligning the parsing pipeline. Users relying on the current
zero-margin default may see layout shifts. Provide a migration note.

**Milestone:** Critical test count drops from 119 to ≤ 6.

---

### Priority 2 — Table Layer Background Propagation ✅

**Goal:** Implement the six-layer table painting model per CSS2.1 §17.5.1.

**Scope:** 3 Critical tests (`S17_5_1_Layer1_TableBackground`,
`S17_5_1_Layer5_RowBackground`, `S17_5_1_Layer6_CellBackground`).

**Tasks:**

- [x] Audit current table background painting in
  `CssTable.cs` / `CssBox.PaintBackground()`.
- [x] Implement the six-layer painting order: (1) table, (2) column
  groups, (3) columns, (4) row groups, (5) rows, (6) cells.
  Implemented in `PaintWalker.PaintTableChildren()` — column/column-group
  backgrounds are painted first (layers 2–3) and tracked in a HashSet to
  prevent double-painting; row-group/row/cell are painted in tree order
  (layers 4–6).
- [x] Ensure transparent cells correctly show underlying layer
  backgrounds (verified by `S17_5_1_TransparentCellShowsRow`: 1.58 %).
- [x] Add targeted tests for each layer in isolation.
- [ ] Re-run differential verification for Chapter 17 to confirm the
  3 Critical tests now pass.

**Verification results (from current data):**

| Test | Diff | Status |
|------|------|--------|
| S17_5_1_Layer1_TableBackground | 98.42 % | FAIL — still Critical |
| S17_5_1_Layer5_RowBackground | 100.00 % | FAIL — still Critical |
| S17_5_1_Layer6_CellBackground | 99.03 % | FAIL — still Critical |
| S17_5_1_TransparentCellShowsRow | 1.58 % | PASS |
| S17_5_1_ColumnGroupBackground | 2.12 % | PASS |
| S17_5_1_MultipleLayers | 2.13 % | PASS |

> **Note:** The 3 Critical failures above are likely dominated by the
> same user-agent stylesheet root cause as Priority 1 (bare test snippets
> missing `<body>` wrapper). Once P1 is resolved, these may drop to Low
> or Pass.

**Estimated Effort:** Complete — code changes are in place.

**Risk:** Low — the six-layer painting model is implemented and passes
the majority of Chapter 17 tests (87 of 95 pass within 5% threshold).

**Milestone:** All Chapter 17 tests pass (0 Critical, 0 High).

---

### Priority 3 — Float Overlap Resolution 🔄

**Goal:** Eliminate float/block overlap warnings by improving float
placement per CSS2.1 §9.5.

**Scope:** 6–8 tests with float overlap warnings across Chapters 9
and 10.

**Tasks:**

- [x] CSS2.1 §9.5.1 rule 6 enforcement: After
  `CollectPrecedingFloatsInBfc()`, enforce
  `top = Math.Max(top, pf.Location.Y)` for each preceding float. This
  prevents a float from starting above preceding floats when it follows
  a zero-height block container (`CssBox.cs`, lines 339–345).
- [ ] For each test with overlap warnings, render side-by-side images
  (html-renderer vs Chromium) and identify the specific float
  placement error.
- [ ] Review float placement logic in `CssBox.cs`
  (`PerformLayoutImp()`, `CollectPrecedingFloatsInBfc()`) and
  `CssLayoutEngine.cs`.
- [ ] Fix float placement to comply with the nine float rules in
  CSS2.1 §9.5.1.
- [ ] Verify that the Acid1 float tests (Sections 2–6) remain passing.
- [ ] Re-run differential verification for Chapters 9 and 10.

**Tests with float overlap warnings (from verification data):**

| Test | Diff | Overlaps | Chapter |
|------|------|----------|---------|
| S9_5_1_ContentFlowsAroundFloat | 99.06 % | 1 | 9 |
| S9_8_ComparisonExample_AllPositioningSchemes | 95.85 % | 2 | 9 |
| S10_3_5_Golden_FloatShrinkToFit | 98.52 % | 1 | 10 |
| S10_6_6_InlineBlock_AutoHeightIncludesFloats | 99.17 % | 1 | 10 |
| S10_6_6_Golden_OverflowHiddenWithFloat | 97.91 % | 1 | 10 |
| S10_6_7_BFCRoot_AutoHeightIncludesFloats | 96.89 % | 1 | 10 |
| S10_6_7_BFCRoot_FloatTallerThanContent | 95.51 % | 1 | 10 |
| S10_6_7_BFCRoot_ContentTallerThanFloat | 89.15 % | 1 | 10 |

**Estimated Effort:** Medium–High — float layout is one of the most
complex parts of the CSS2 box model.

**Risk:** High — changes to float layout can cascade throughout the
rendering pipeline. Require full regression testing.

**Milestone:** Zero float/block overlaps reported. Float-related Critical
tests drop to Low or Pass.

**Required Expertise:** Deep knowledge of CSS2.1 §9.5 float model and
the html-renderer layout pipeline.

---

### Priority 4 — Table Height Distribution ⬜

**Goal:** Match Chromium's row-height distribution algorithm per
CSS2.1 §17.5.3.

**Scope:** 2 High tests (`S17_5_3_PercentageHeight`,
`S17_5_3_ExtraHeightDistributed`).

**Tasks:**

- [ ] Inspect how `CssTable.cs` distributes extra height among rows
  when the table has an explicit height.
- [ ] Compare algorithm with the CSS2.1 §17.5.3 specification and
  Chromium's behaviour.
- [ ] Implement corrected distribution algorithm.
- [ ] Add unit tests for percentage-based and extra-height distribution.
- [ ] Re-run differential verification for Chapter 17.

**Estimated Effort:** Medium — isolated to table height logic.

**Risk:** Medium — may affect tables with explicit heights.

**Milestone:** Both High-severity tests move to Low or Pass.

---

### Priority 5 — Medium-Severity Rendering Fixes ⬜

**Goal:** Resolve the 5 medium-severity tests that show 5–10 % diffs.

**Tasks:**

- [ ] `S9_7_FloatAdjustsDisplay` (6.72 %): Verify that setting `float`
  on an element correctly adjusts its `display` value per §9.7.
  Chromium computes `display: block` for floated elements that were
  originally `inline`; html-renderer may not apply this transform.
- [ ] `S10_8_2_VerticalAlign_TableCell` (6.55 %): Review vertical
  alignment computation for `td` / `th` elements. The difference likely
  stems from baseline calculation or cell padding handling.
- [ ] `S17_Integration_MixedHtmlCssTable` (6.21 %) and
  `S17_Integration_Golden_ComplexTable` (5.78 %): Investigate
  combined HTML attribute + CSS property handling in tables. HTML
  attributes like `width`, `border`, `cellpadding` may interact
  differently with CSS properties in each engine.
- [ ] `S17_5_3_MinimumRowHeight` (5.01 %): Ensure minimum row height
  algorithm matches §17.5.3. Closely related to P4 table height work.
- [ ] Re-run differential verification for affected chapters.

**Estimated Effort:** Medium — each fix is independent and localised.

**Risk:** Low–Medium — targeted fixes with limited blast radius.

**Milestone:** All tests at ≤ 5 % diff (0 Medium-severity tests).

---

### Priority 6 — Font Rasterisation Monitoring (Ongoing) ⬜

**Goal:** Accept irreducible font differences; monitor for regressions.

**Scope:** 148 Low-severity tests (< 5 % diff).

**Tasks:**

- [ ] Establish a baseline snapshot of all Low-severity diff ratios.
- [ ] Add a CI check that flags any Low test whose diff increases by
  more than 2 percentage points (regression detection).
- [ ] Document expected cross-environment variance (GDI+ vs Skia,
  font availability, hinting settings).

**Estimated Effort:** Small — CI configuration + documentation.

**Risk:** None — monitoring only, no rendering changes.

**Milestone:** CI regression gate active; no Low test exceeds 5 %.

---

### Priority 7 — Expand Differential Coverage to Chapters 4, 5, 8 ⬜

**Goal:** Extend the differential verification to cover all CSS2 chapters
with existing test suites.

**Scope:** Chapters 4 (Syntax, 65 tests), 5 (Selectors, 66 tests), and
8 (Box Model, 81 tests) have tests in `Css2Chapter4Tests.cs`,
`Css2Chapter5Tests.cs`, and `Css2Chapter8Tests.cs` respectively, but are
not yet included in the differential verification against Chromium.

**Tasks:**

- [ ] Add Chapters 4, 5, 8 test HTML snippets to
  `Css2DifferentialVerificationTests.VerifyAllCss2Tests_GenerateReport()`
  alongside the existing Chapters 9, 10, 17 test extraction.
- [ ] Run the expanded differential suite and analyse results.
- [ ] Update `css2-differential-verification.md` with per-chapter
  summaries for chapters 4, 5, 8.
- [ ] Triage any new Critical/High/Medium failures and add them to the
  appropriate priority's task list.

**Estimated Effort:** Small — infrastructure exists; requires wiring in
additional chapter test classes.

**Risk:** Low — read-only comparison; no rendering changes.

**Milestone:** Differential coverage expanded from 280 to ~492 tests.

---

## Milestones

| Milestone | Priority | Target Outcome | Success Metric | Status |
|-----------|----------|---------------|----------------|--------|
| M1 | P1 | UA stylesheet aligned | Critical ≤ 6, pass rate ≥ 90 % | ✅ Code Complete |
| M2 | P2 | Table backgrounds correct | Chapter 17: 0 Critical | ✅ Code Complete |
| M3 | P3 | Float overlaps eliminated | 0 overlap warnings | 🔄 In Progress |
| M4 | P4 | Table heights correct | 0 High-severity tests | ⬜ Pending |
| M5 | P5 | Medium diffs resolved | 0 Medium-severity tests | ⬜ Pending |
| M6 | P6 | Regression gate active | CI monitors all Low tests | ⬜ Pending |
| M7 | P7 | Full chapter coverage | Chapters 4, 5, 8 in diff suite | ⬜ Pending |

---

## Required Resources and Expertise

| Area | Expertise Needed | Notes |
|------|-----------------|-------|
| UA Stylesheet | CSS spec knowledge, HTML5 parsing | Fragment wrapper injection or test-level fix |
| Table Backgrounds | CSS2.1 §17.5.1 table painting model | ✅ Six-layer painting order implemented |
| Float Layout | CSS2.1 §9.5 float placement rules | Complex; benefits from visual debugging |
| Table Height | CSS2.1 §17.5.3 height distribution | Algorithm-level change |
| Rendering Pipeline | html-renderer internals (`CssBox`, `CssTable`) | Code-level familiarity with layout engine |
| CI Infrastructure | xUnit + differential test runner | Existing infrastructure; extend for regression gating |
| Coverage Expansion | Test infrastructure (`DifferentialTestRunner`) | Wire additional chapter test classes |

---

## Progress Tracking

Each priority's completion will be tracked by:

1. Re-running the differential verification suite
   (`dotnet test --filter "FullyQualifiedName~Css2DifferentialVerificationTests"`).
2. Updating [`css2-differential-verification.md`](../css2-differential-verification.md)
   with new results.
3. Recording the before/after metrics in this roadmap.
4. Creating an ADR for any significant rendering algorithm change.

### Completion Criteria

The roadmap is complete when:

- **0 Critical** tests remain (currently 119).
- **0 High** tests remain (currently 2).
- **0 Medium** tests remain (currently 5).
- All **Low** tests are monitored by CI regression gating.
- Differential coverage expanded to all CSS2 chapters with tests
  (currently chapters 9, 10, 17; pending chapters 4, 5, 8).
- All changes are documented with ADRs where appropriate.
- `css2-differential-verification.md` is updated with final results.
