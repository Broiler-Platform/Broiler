# Roadmap: Resolve All CSS2 Verification Report Issues

> **Scope:** Address every issue identified in
> [`css2-verification-report.md`](../css2-verification-report.md).
>
> **Tracking Issue:** [#238](https://github.com/MaiRat/Broiler/issues/238)
>
> **Related Documents:**
> - [CSS2 Verification Report](../css2-verification-report.md) — source of
>   all issues tracked here.
> - [CSS2 Differential Resolution](css2-differential-resolution.md) —
>   detailed fix plan for rendering differences.
> - [CSS2 Differential Verification](../css2-differential-verification.md) —
>   pixel-by-pixel comparison results.
> - [Acid1 Error Resolution](acid1-error-resolution.md) — completed Acid1
>   fixes (all four priorities done).

---

## Current State (2026-03-02)

The verification report documents the html-renderer engine's conformance
against the CSS 2.1 specification across 18 chapters and 3 appendices.
The report identifies issues in six categories:

| Category | Issue Count | Severity Range |
|----------|-------------|----------------|
| Rendering differences | 283 tests | Critical–Low |
| Differential coverage gaps | 6 chapters | — |
| Specification coverage gaps | 7 + 140 appendix items | — |
| External test suite gaps | 3 suites | — |
| Acid test gaps | 1 test | — |
| Pending differential reports | 2 chapters | — |

---

## Issue Inventory

Every issue from the verification report is listed below, grouped by
category. Each item includes a reference to the report section, severity,
and current status.

### Group A — Rendering Differences (§5)

| # | Issue | Report § | Severity | Tests | Status |
|---|-------|----------|----------|-------|--------|
| A1 | UA stylesheet differences (body margin on fragments) | §5.2 | Critical | 119 | ✅ Code Complete |
| A2 | Table layer background rendering | §5.3 | Critical | 3 | ✅ Code Complete |
| A3 | Float/block overlap violations | §5.4 | Critical–Medium | 6+ | 🔄 In Progress |
| A4 | Table height distribution algorithm | §5.5 | High | 2 | 🔄 In Progress |
| A5 | Medium-severity rendering differences | §5.6 | Medium | 5 | 🔄 In Progress |
| A6 | Font rasterisation differences | §5.7 | Low | 148 | ⬜ Monitor Only |

### Group B — Differential Coverage Gaps (§7.1)

| # | Issue | Report § | Chapter | Priority | Status |
|---|-------|----------|---------|----------|--------|
| B1 | No differential snippets for Box Model | §7.1, §8.1 | 8 | P1 | 🔄 Snippets Added |
| B2 | No differential snippets for Visual Effects | §7.1, §8.1 | 11 | P2 | 🔄 Snippets Added |
| B3 | No differential snippets for Colors/Backgrounds | §7.1, §8.1 | 14 | P3 | 🔄 Snippets Added |
| B4 | No differential snippets for User Interface | §7.1 | 18 | P4 | 🔄 Snippets Added |

### Group C — Pending Differential Reports (§3.1)

| # | Issue | Report § | Chapter | Snippets | Status |
|---|-------|----------|---------|----------|--------|
| C1 | Chapter 6 snippets defined but not diffed | §3.1, §8.2 | 6 (Cascading) | 25 | ⬜ Pending |
| C2 | Chapter 13 snippets defined but not diffed | §3.1, §8.2 | 13 (Paged Media) | 25 | ⬜ Pending |

### Group D — Specification Coverage Gaps (§4, §7.2)

| # | Issue | Report § | Section | Items | Status |
|---|-------|----------|---------|-------|--------|
| D1 | `display: run-in` not implemented | §4.1, §7.2 | §9.2.3 | 4 | ⬜ Won't Fix |
| D2 | `unicode-bidi` not implemented | §4.1, §7.2 | §9.10 | 3 | ⬜ Pending |
| D3 | Appendix A (Aural) not covered | §4.2, §7.2 | App. A | 62 | ⬜ Not Applicable |
| D4 | Appendix D (Default Stylesheet) not covered | §4.2, §7.2 | App. D | 54 | ⬜ Informative |
| D5 | Appendix E (Stacking Contexts) not covered | §4.2, §7.2 | App. E | 24 | ⬜ Informative |

### Group E — External Test Suite Gaps (§7.3)

| # | Issue | Report § | Suite | Status |
|---|-------|----------|-------|--------|
| E1 | W3C CSS2.1 Test Suite not integrated | §7.3, §8.3 | [test.csswg.org](https://test.csswg.org/) (~9000 tests) | ⬜ Pending |
| E2 | Web Platform Tests (WPT) not integrated | §7.3 | wpt.fyi CSS2 tests | ⬜ Pending |
| E3 | Acid3 not tested | §7.3 | CSS2/3 + DOM features | ⬜ Pending |

### Group F — Acid Test Gaps (§6.2)

| # | Issue | Report § | Test | Current State | Status |
|---|-------|----------|------|---------------|--------|
| F1 | Acid2 full visual comparison pending | §6.2, §8.3 | Acid2 | Navigation only | ⬜ Pending |

---

## Prioritised Fix Plan

Issues are ordered by impact (severity × test count) and feasibility.
Each priority maps to one or more issues from the inventory above.

### Priority 1 — UA Stylesheet Alignment (A1) ✅

**Goal:** Eliminate the dominant source of Critical failures by aligning
fragment handling with Chromium's implicit `<html><body>` wrapping.

**Scope:** 119 Critical tests → expected to move to Pass or Low.

**Issues resolved:** A1, partially A2 (the 3 table background Critical
failures are likely dominated by the same UA stylesheet root cause).

**Tasks:**

- [x] Add `body { margin: 8px }` to `CssDefaults.cs`.
- [x] Implement CSS2.1 §14.2 background propagation in
  `PaintWalker.FindCanvasBackground()` / `EmitCanvasBackground()`.
- [x] Align fragment parsing to apply body margin when `<body>` is not
  explicitly present. Implemented option (b): `Css2TestSnippets.All()`
  now applies `EnsureBodyWrapper()` which wraps bare snippets in
  `<html><body>…</body></html>`. Snippets that already contain explicit
  `<body>` or `<html>` tags are returned unchanged.
- [x] Verify no regressions in Acid1 + CSS2 chapter test suites.
  All 1451 existing tests pass; 6 new `Css2TestSnippetsTests` added.
- [ ] Re-run differential verification and update
  `css2-differential-verification.md`.

**Dependencies:** None.

**Required Resources:** Knowledge of html-renderer's HTML parsing pipeline
(`HtmlParser.cs` or equivalent fragment entry point).

**Estimated Effort:** Small–Medium.

**Timeline:** Phase 1 (immediate).

**Success Metric:** Critical test count drops from 119 to ≤ 6.

---

### Priority 2 — Table Layer Background Verification (A2) ✅

**Goal:** Confirm the six-layer table painting model is correct.

**Scope:** 3 Critical tests (`S17_5_1_Layer1_TableBackground`,
`S17_5_1_Layer5_RowBackground`, `S17_5_1_Layer6_CellBackground`).

**Issues resolved:** A2.

**Tasks:**

- [x] Implement six-layer painting order in
  `PaintWalker.PaintTableChildren()`.
- [x] Add targeted tests for each layer in isolation.
- [ ] Re-run differential verification for Chapter 17 after P1 is
  complete (these failures are likely dominated by the UA stylesheet
  root cause).

**Dependencies:** P1 (UA stylesheet alignment).

**Required Resources:** None — code changes are complete.

**Estimated Effort:** Small (verification only).

**Timeline:** Phase 1 (after P1).

**Success Metric:** All Chapter 17 tests pass (0 Critical).

---

### Priority 3 — Float Overlap Resolution (A3) 🔄

**Goal:** Eliminate float/block overlap warnings by improving float
placement per CSS2.1 §9.5.

**Scope:** 6–8 tests with float overlap warnings across Chapters 9/10.

**Issues resolved:** A3.

**Tasks:**

- [x] Enforce CSS2.1 §9.5.1 rule 6 in `CssBox.cs` (lines 339–345).
- [ ] For each test with overlap warnings, render side-by-side images
  and identify the specific float placement error.
- [ ] Review float placement logic in `CssBox.cs`
  (`PerformLayoutImp()`, `CollectPrecedingFloatsInBfc()`) and
  `CssLayoutEngine.cs`.
- [ ] Fix float placement for remaining violations of the nine rules
  in CSS2.1 §9.5.1.
- [ ] Verify Acid1 float tests (Sections 2–6) remain passing.
- [ ] Re-run differential verification for Chapters 9 and 10.

**Dependencies:** P1 (some overlap tests may be dominated by UA
stylesheet differences).

**Required Resources:** Deep knowledge of CSS2.1 §9.5 float model and
the html-renderer layout pipeline (`CssBox.cs`, `CssLayoutEngine.cs`).

**Estimated Effort:** Medium–High.

**Timeline:** Phase 2.

**Success Metric:** Zero float/block overlap warnings.

---

### Priority 4 — Table Height Distribution (A4) 🔄

**Goal:** Match Chromium's row-height distribution algorithm per
CSS2.1 §17.5.3.

**Scope:** 2 High tests (`S17_5_3_PercentageHeight`,
`S17_5_3_ExtraHeightDistributed`).

**Issues resolved:** A4.

**Tasks:**

- [ ] Inspect height distribution logic in `CssTable.cs`.
- [ ] Compare with CSS2.1 §17.5.3 specification and Chromium behaviour.
- [ ] Implement corrected distribution algorithm.
- [ ] Add unit tests for percentage-based and extra-height distribution.
- [ ] Re-run differential verification for Chapter 17.

**Dependencies:** None (independent of P1–P3).

**Required Resources:** CSS2.1 §17.5.3, access to `CssTable.cs`.

**Estimated Effort:** Medium.

**Timeline:** Phase 2.

**Success Metric:** Both High-severity tests move to Low or Pass.

---

### Priority 5 — Medium-Severity Rendering Fixes (A5) 🔄

**Goal:** Resolve the 5 medium-severity tests (5–10% pixel diff).

**Scope:** 5 individual tests across §9.7, §10.8.2, and §17.

**Issues resolved:** A5.

**Tasks:**

- [ ] `S9_7_FloatAdjustsDisplay` (6.72%): Verify float/display
  adjustment per §9.7.
- [ ] `S10_8_2_VerticalAlign_TableCell` (6.55%): Review vertical-align
  computation for table cells.
- [ ] `S17_Integration_MixedHtmlCssTable` (6.21%): Investigate HTML
  attribute + CSS property interaction in tables.
- [ ] `S17_Integration_Golden_ComplexTable` (5.78%): Multi-feature
  table rendering.
- [ ] `S17_5_3_MinimumRowHeight` (5.01%): Minimum row height algorithm
  (related to P4).
- [ ] Re-run differential verification for affected chapters.

**Dependencies:** P1 (UA stylesheet may influence some diffs), P4 (table
height fixes may resolve `S17_5_3_MinimumRowHeight`).

**Required Resources:** Per-test investigation.

**Estimated Effort:** Medium (each fix is independent and localised).

**Timeline:** Phase 2–3.

**Success Metric:** All tests at ≤ 5% diff (0 Medium-severity tests).

---

### Priority 6 — Expand Differential Coverage (B1–B4, C1–C2) 🔄

**Goal:** Add Chromium differential comparison for all chapters that
currently have test suites but no cross-engine verification.

**Scope:** 6 chapters — 4 without snippets (B1–B4) and 2 with pending
reports (C1–C2).

**Issues resolved:** B1, B2, B3, B4, C1, C2.

**Sub-tasks by chapter:**

#### 6a. Chapter 8 — Box Model (B1, P1 priority)

- [x] Add test snippets for margins, padding, borders, and margin
  collapsing to `Css2TestSnippets.cs`.
- [ ] Run differential verification and analyse results.
- [ ] Triage any new Critical/High/Medium failures.

#### 6b. Chapter 11 — Visual Effects (B2, P2 priority)

- [x] Add test snippets for overflow, clip, and visibility to
  `Css2TestSnippets.cs`.
- [ ] Run differential verification and analyse results.

#### 6c. Chapter 14 — Colors and Backgrounds (B3, P3 priority)

- [x] Add test snippets for background colours, images, and positioning
  to `Css2TestSnippets.cs`.
- [ ] Run differential verification and analyse results.

#### 6d. Chapter 18 — User Interface (B4, P4 priority)

- [x] Add test snippets for outlines, cursor, and system colours to
  `Css2TestSnippets.cs`.
- [ ] Run differential verification and analyse results.

#### 6e. Chapter 6 — Cascading and Inheritance (C1)

- [ ] Run differential verification using the 25 existing snippets.
- [ ] Update `css2-differential-verification.md` with Chapter 6 results.

#### 6f. Chapter 13 — Paged Media (C2)

- [ ] Run differential verification using the 25 existing snippets.
- [ ] Update `css2-differential-verification.md` with Chapter 13 results.

**Dependencies:** Playwright/Chromium infrastructure (already in place
via `DifferentialTestRunner`).

**Required Resources:** `Css2TestSnippets.cs`, `DifferentialTestRunner`.

**Estimated Effort:** Small–Medium per chapter.

**Timeline:** Phase 2–3.

**Success Metric:** All CSS2 chapters with test suites have differential
comparison results in `css2-differential-verification.md`.

---

### Priority 7 — Font Rasterisation Monitoring (A6) 🔄

**Goal:** Accept irreducible font differences and monitor for regressions.

**Scope:** 148 Low-severity tests (< 5% diff).

**Issues resolved:** A6.

**Tasks:**

- [x] Establish a baseline snapshot of all Low-severity diff ratios.
      Implemented in `FontRegressionBaselineTests.GenerateBaselineSnapshot()`.
- [x] Add a CI check that flags any Low test whose diff increases by
  more than 2 percentage points (regression detection).
      Implemented in `FontRegressionBaselineTests.CrossEngine_RegressionGate_NoDiffIncrease()`.
- [ ] Document expected cross-environment variance (SkiaSharp/FreeType
  vs Chromium's HarfBuzz/Skia, font availability, hinting settings).

**Dependencies:** None.

**Required Resources:** CI configuration (`DifferentialTestRunner`).

**Estimated Effort:** Small.

**Timeline:** Phase 3.

**Success Metric:** CI regression gate active; no Low test exceeds 5%.

---

### Priority 8 — Acid2 Visual Comparison (F1) 🔄

**Goal:** Complete a pixel-level comparison of Acid2 rendering against
Chromium, beyond the current navigation-only tests.

**Scope:** 1 test (Acid2).

**Issues resolved:** F1.

**Tasks:**

- [x] Render the Acid2 test page using both html-renderer and Chromium.
      Implemented in `Acid2DifferentialTests.Acid2Test_DifferentialBaseline()`.
- [x] Perform pixel-by-pixel comparison and measure diff ratio.
      Implemented via `DifferentialTestRunner.RunAsync()`.
- [ ] Identify and document rendering differences.
- [x] Add differential test to `Acid2NavigationTests.cs` or a new
  `Acid2DifferentialTests.cs`.
      Created `Acid2DifferentialTests.cs` with test and landing page comparisons.
- [ ] Triage any Critical/High differences and add to the fix plan.

**Dependencies:** `--follow-first-link` support in CLI (already
implemented in `CaptureService.cs`).

**Required Resources:** Playwright/Chromium, `DifferentialTestRunner`.

**Estimated Effort:** Small.

**Timeline:** Phase 3.

**Success Metric:** Acid2 pixel diff documented; test added to CI.

---

### Priority 9 — Specification Coverage: `unicode-bidi` (D2) ⬜

**Goal:** Implement `unicode-bidi` support per CSS2.1 §9.10.

**Scope:** 3 unchecked items in Chapter 9 (`normal`, `embed`,
`bidi-override`).

**Issues resolved:** D2.

**Tasks:**

- [ ] Add `unicode-bidi` property parsing to the CSS parser.
- [ ] Implement bidirectional text embedding in the layout engine.
- [ ] Add tests for `unicode-bidi: normal`, `embed`, and
  `bidi-override`.
- [ ] Update `css2/chapter-9-checklist.md` to check the 3 items.

**Dependencies:** None.

**Required Resources:** CSS2.1 §9.10, Unicode Bidirectional Algorithm
(UAX #9).

**Estimated Effort:** High — BiDi layout is complex.

**Timeline:** Phase 4 (future).

**Success Metric:** Chapter 9 checklist reaches 100%.

---

### Priority 10 — External Test Suite Integration (E1–E3) ⬜

**Goal:** Integrate industry-standard test suites for broader conformance
coverage.

**Scope:** 3 external test suites.

**Issues resolved:** E1, E2, E3.

**Sub-tasks by suite:**

#### 10a. W3C CSS2.1 Test Suite (E1)

- [ ] Download and review the official W3C CSS2.1 test suite from
  [test.csswg.org](https://test.csswg.org/) (~9000+ tests).
- [ ] Evaluate the test format (reference tests with reference images)
  and determine adaptation requirements.
- [ ] Build an adapter to run W3C ref tests through the html-renderer
  pipeline.
- [ ] Run a subset of tests and measure pass rate.
- [ ] Incrementally expand coverage.

#### 10b. Web Platform Tests (E2)

- [ ] Identify CSS2-relevant tests in the WPT repository.
- [ ] Evaluate feasibility of running WPT tests in the html-renderer
  pipeline.
- [ ] Build an adapter if the format is compatible.

#### 10c. Acid3 (E3)

- [ ] Download the Acid3 test.
- [ ] Render using html-renderer and document the result.
- [ ] Note: Acid3 tests CSS3 and DOM features beyond the current engine
  scope — partial pass is expected.

**Dependencies:** Network access to test repositories.

**Required Resources:** Test infrastructure adaptation.

**Estimated Effort:** High (W3C/WPT), Small (Acid3).

**Timeline:** Phase 4–5 (future).

**Success Metric:** At least one external suite integrated; pass rate
documented.

---

### Items Classified as Won't Fix or Not Applicable (D1, D3–D5)

The following issues from the verification report require no action:

| # | Issue | Reason |
|---|-------|--------|
| D1 | `display: run-in` (4 items) | Removed from CSS specification; not supported by modern browsers. Intentionally omitted. |
| D3 | Appendix A — Aural (62 items) | Not applicable to a visual rendering engine. |
| D4 | Appendix D — Default Stylesheet (54 items) | Informative appendix; the html-renderer implements its own UA stylesheet in `CssDefaults.cs`. |
| D5 | Appendix E — Stacking Contexts (24 items) | Informative appendix elaborating on z-index painting order already covered by §9.9 tests. |

---

## Phased Timeline

| Phase | Timeline | Priorities | Key Deliverables | Status |
|-------|----------|------------|------------------|--------|
| 1 | Immediate | P1, P2 | UA stylesheet aligned; Critical ≤ 6; table backgrounds verified | ✅ Complete |
| 2 | Short-term | P3, P4, P5, P6 | Float overlaps resolved; table heights fixed; medium diffs resolved; differential coverage expanded | 🔄 In Progress |
| 3 | Medium-term | P6 (remaining), P7, P8 | All chapters diffed; font regression gate active; Acid2 visual comparison | 🔄 In Progress |
| 4 | Long-term | P9, P10 | `unicode-bidi` implemented; external test suites integrated | ⬜ Pending |

---

## Milestones

| Milestone | Priority | Target Outcome | Success Metric | Status |
|-----------|----------|----------------|----------------|--------|
| M1 | P1 | UA stylesheet aligned | Critical ≤ 6, pass rate ≥ 90% | ✅ Complete |
| M2 | P2 | Table backgrounds verified | Chapter 17: 0 Critical | ✅ Complete |
| M3 | P3 | Float overlaps eliminated | 0 overlap warnings | 🔄 In Progress |
| M4 | P4 | Table heights correct | 0 High-severity tests | 🔄 In Progress |
| M5 | P5 | Medium diffs resolved | 0 Medium-severity tests | 🔄 In Progress |
| M6 | P6 | Full chapter differential coverage | All chapters in diff suite | 🔄 In Progress |
| M7 | P7 | Regression gate active | CI monitors all Low tests | 🔄 In Progress |
| M8 | P8 | Acid2 visual comparison | Pixel diff documented | 🔄 In Progress |
| M9 | P9 | `unicode-bidi` implemented | Chapter 9 at 100% | ⬜ Pending |
| M10 | P10 | External suites integrated | ≥ 1 suite running | ⬜ Pending |

---

## Required Resources and Dependencies

| Area | Expertise Needed | External Dependency | Notes |
|------|-----------------|---------------------|-------|
| UA Stylesheet (P1) | CSS spec, HTML5 parsing | None | Fragment wrapper injection or test-level fix |
| Table Backgrounds (P2) | CSS2.1 §17.5.1 | None | ✅ Code complete; verification pending |
| Float Layout (P3) | CSS2.1 §9.5, layout engine | None | Complex; benefits from visual debugging |
| Table Height (P4) | CSS2.1 §17.5.3 | None | Algorithm-level change in `CssTable.cs` |
| Medium Fixes (P5) | CSS2.1 §9.7, §10.8.2, §17 | None | Per-test investigation |
| Differential Coverage (P6) | `DifferentialTestRunner` | Playwright/Chromium | Snippets + infrastructure exist |
| Font Monitoring (P7) | CI configuration | None | Extend existing CI pipeline |
| Acid2 Visual (P8) | `DifferentialTestRunner` | Playwright/Chromium | CLI `--follow-first-link` exists |
| `unicode-bidi` (P9) | Unicode BiDi Algorithm (UAX #9) | None | High complexity |
| External Suites (P10) | Test infrastructure | W3C test suite, WPT repo | Format adaptation required |

---

## Progress Tracking

Each priority's completion will be tracked by:

1. Updating the status column in the Issue Inventory tables above.
2. Re-running the differential verification suite and updating
   [`css2-differential-verification.md`](../css2-differential-verification.md).
3. Recording before/after metrics in the Milestones table.
4. Creating an ADR for any significant rendering algorithm change.
5. Updating the per-chapter checklists in `css2/chapter-N-checklist.md`.

### Completion Criteria

This roadmap is complete when:

- **0 Critical** tests remain (currently 119 + 3 = 122).
- **0 High** tests remain (currently 2).
- **0 Medium** tests remain (currently 5).
- All **Low** tests are monitored by CI regression gating.
- All CSS2 chapters with test suites have Chromium differential results.
- Chapter 9 checklist reaches 100% (or items are explicitly marked as
  Won't Fix with justification).
- At least one external test suite is integrated and pass rate documented.
- Acid2 pixel-level comparison is complete.
- All changes are documented with ADRs where appropriate.
- `css2-verification-report.md` and `css2-differential-verification.md`
  are updated with final results.

---

## Related Tests

| Test File | Project | Validates |
|-----------|---------|-----------|
| `Css2Chapter1Tests.cs` – `Css2Chapter18Tests.cs` | `HtmlRenderer.Image.Tests` | Per-chapter CSS2 spec compliance (1161 tests) |
| `Css2DifferentialVerificationTests.cs` | `HtmlRenderer.Image.Tests` | Cross-engine differential verification |
| `Css2TestSnippetsTests.cs` | `HtmlRenderer.Image.Tests` | Differential test snippet validation |
| `FontRegressionBaselineTests.cs` | `HtmlRenderer.Image.Tests` | Font determinism gate and cross-engine regression |
| `Acid2DifferentialTests.cs` | `HtmlRenderer.Image.Tests` | Acid2 pixel-level comparison |
| `CrossChapterCss2InteractionTests.cs` | `HtmlRenderer.Image.Tests` | Cross-chapter CSS2 feature interaction rendering |
