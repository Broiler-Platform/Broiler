# Roadmap: Refactor Documentation and Testcases

> **Scope:** Restructure all project documentation for clarity and
> consistency, and refactor testcases to emphasise rendering-specific
> verification over structural checks.

## Background

The Broiler project has accumulated a significant body of documentation and
tests across several iterations of feature work. While individual documents
and test files are valuable, the overall collection suffers from:

- **Overlapping documents** — three CSS2-related files in `docs/` cover
  similar ground from different angles without clear relationships
  (`css2-differential-resolution.md`, `css2-differential-verification.md`,
  `css2-verification-report.md`).
- **No documentation index** — there is no top-level guide explaining the
  purpose and audience of each document.
- **Inconsistent structure** — some documents live in `docs/` root while
  related roadmaps live in `docs/roadmap/`; ADRs are well-indexed in
  `docs/adr/README.md` but other directories lack an equivalent.
- **Test count/metric mismatches** — different documents cite different test
  totals without explaining the relationship between test suites.
- **Structure-heavy tests** — `Broiler.App.Tests` is primarily unit tests on
  internal pipeline components (DOM, CSS parsing, tree building) with limited
  rendering-output verification.
- **Rendering tests concentrated in one project** — pixel-level and analytics
  tests exist only in `HtmlRenderer.Image.Tests`, creating a gap between
  structural correctness and visual output.

This roadmap defines concrete phases to address these issues.

---

## Current-State Assessment

### Documentation Inventory

| Location | Files | Purpose |
|----------|-------|---------|
| `docs/` root | 6 standalone files | Mixed: testing guides, architecture overviews, CSS2 reports |
| `docs/adr/` | 44 ADRs + README index | Architecture decisions (well-structured) |
| `docs/roadmap/` | 7 roadmaps | Feature/compliance roadmaps (consistent format) |

**Key problems:**

1. `docs/` root files have no index, unclear audience, and significant
   thematic overlap (three CSS2 documents, two architecture documents, three
   testing documents).
2. `docs/adr/` is the only directory with a proper `README.md` index.
3. No cross-references link related documents together.
4. `docs/roadmap/` files follow a consistent format but are not linked from
   a central navigation point.

### Test Suite Inventory

| Project | Test Count | Primary Focus |
|---------|-----------|---------------|
| `Broiler.App.Tests` | ~20 files, ~298 tests | Structural: DOM, CSS parsing, pipeline wiring, rendering output |
| `Broiler.Cli.Tests` | ~14 files, ~240 tests | CLI output validation, capture integration, W3C compliance |
| `HtmlRenderer.Image.Tests` | ~50 files, ~1474 tests | Rendering: CSS2 chapters, pixel regression, analytics, differential |

**Key problems:**

1. `Broiler.App.Tests` verifies internal component behaviour but rarely
   checks rendered output — if the pipeline produces incorrect pixels, these
   tests may still pass.
2. No rendering-specific tests exist between the CLI capture level
   (`Broiler.Cli.Tests`) and the low-level pixel level
   (`HtmlRenderer.Image.Tests`) — there is no mid-level rendering
   verification layer.
3. CSS2 chapter tests in `HtmlRenderer.Image.Tests` are thorough for spec
   compliance but do not cover cross-feature rendering scenarios (e.g.
   floats interacting with positioned elements).
4. Test categories (`[Trait]` attributes) are inconsistently applied,
   making filtered test runs unreliable.

---

## Goals

1. **Documentation clarity** — every document has a clear purpose, audience,
   and position in a navigable hierarchy.
2. **Documentation completeness** — gaps between architecture decisions,
   roadmaps, and testing guides are filled with cross-references.
3. **Rendering-specific tests** — new tests verify visual output at
   meaningful boundaries, not just internal data structures.
4. **Test organisation** — consistent categorisation, naming, and trait
   usage across all three test projects.
5. **Maintainability** — guidelines ensure new documents and tests follow
   the established structure.

---

## Phase 1 — Documentation Index and Hierarchy

**Goal:** Create a navigable documentation structure with clear relationships.

- [ ] Create `docs/README.md` as the top-level documentation index
  - [ ] Categorise documents: *Reference*, *Guides*, *Reports*, *Roadmaps*,
        *Decisions*
  - [ ] Add one-line summaries for each document
  - [ ] Add links to `docs/adr/README.md` and `docs/roadmap/` index
- [ ] Create `docs/roadmap/README.md` as a roadmap index
  - [ ] List all roadmaps with status (Active / Complete / Proposed)
  - [ ] Cross-reference related roadmaps
- [ ] Add cross-reference sections to existing `docs/` root files
  - [ ] Link `css2-differential-resolution.md` →
        `css2-differential-verification.md` → `css2-verification-report.md`
        with clear scope descriptions
  - [ ] Link `testing-*.md` files to each other and to the testing roadmap
  - [ ] Link `architecture-*.md` files to relevant ADRs

**Acceptance criteria:**
- A reader can navigate from any document to all related documents within
  two clicks.
- Every document in `docs/` is listed in `docs/README.md`.

---

## Phase 2 — Documentation Consolidation

**Goal:** Reduce overlap and clarify document boundaries.

- [x] Consolidate CSS2 verification documents
  - [x] Merge `css2-differential-resolution.md` and
        `css2-differential-verification.md` into a single resolution report,
        or clearly delineate scope with introductory paragraphs
  - [x] Ensure `css2-verification-report.md` serves as the high-level
        summary and links to the detailed resolution document
- [x] Consolidate testing documents
  - [x] Ensure `testing-guide.md` is the practical "how to run tests" entry
        point
  - [x] Ensure `testing-current-state.md` is clearly marked as a
        point-in-time audit (add date and version)
  - [x] Ensure `testing-architecture.md` describes the target architecture
        and links to the roadmap for implementation status
  - [x] Ensure `testing-roadmap.md` is the single source of truth for
        planned testing improvements
- [x] Review and update all ADR statuses in `docs/adr/README.md`
  - [x] Mark completed ADRs as `Accepted`
  - [x] Mark superseded ADRs and link to replacements

**Acceptance criteria:**
- No two documents cover the same topic without clearly stated distinct
  scopes.
- Each testing document has a unique, non-overlapping purpose.

---

## Phase 3 — Rendering-Specific Test Design

**Goal:** Introduce tests that verify rendered visual output, not just
internal data structures.

- [x] Define rendering test categories
  - [x] **Pixel regression** — compare rendered output against baseline
        images (extend existing `PixelRegressionTests` patterns)
  - [x] **Layout verification** — assert bounding-box positions and
        dimensions for key elements after layout
  - [x] **Cross-feature interaction** — test combinations (e.g. floats +
        positioning, tables + overflow, inline-block + text-align)
  - [x] **Visual analytics** — measure coverage, blank-area ratio, and
        timing metrics
- [x] Add rendering tests to `Broiler.App.Tests`
  - [x] Create `RenderingOutputTests.cs` — render HTML snippets through the
        full pipeline and verify output properties (dimensions, non-blank,
        key element positions)
  - [x] Create `CrossFeatureRenderingTests.cs` — test interactions between
        CSS features that are currently only tested in isolation
- [x] Extend `HtmlRenderer.Image.Tests` rendering coverage
  - [x] Add cross-chapter CSS2 interaction tests (e.g. Chapter 9 positioning
        + Chapter 10 dimensions + Chapter 11 overflow)
  - [x] Add real-world snippet tests — extract layout patterns from common
        websites and verify rendering
- [x] Establish baseline images for new pixel regression tests
  - [x] Document the baseline-generation process in `testing-guide.md`

**Acceptance criteria:**
- At least 20 new rendering-specific tests exist across the two test
  projects.
- Every new test verifies observable output (pixels, dimensions, positions)
  rather than internal state alone.

---

## Phase 4 — Test Organisation and Trait Standardisation

**Goal:** Make test filtering reliable and test naming consistent.

- [x] Define standard trait categories
  - [x] `Category` — `Unit`, `Rendering`, `Integration`, `Differential`,
        `Compliance`
  - [x] `Feature` — `BoxModel`, `Float`, `Position`, `Table`, `Text`,
        `Color`, `Font`, `Selector`, `Media`, etc.
  - [x] `Engine` — `HtmlRenderer`, `Broiler`, `Cli`
- [x] Apply traits consistently across all test projects
  - [x] Audit existing `[Trait]` usage in all three test projects
  - [x] Add missing traits to existing tests
  - [x] Ensure new tests created in Phase 3 use the standardised traits
- [x] Standardise test naming conventions
  - [x] Pattern: `[Feature]_[Scenario]_[ExpectedResult]`
  - [x] Document the convention in `testing-guide.md`
- [x] Verify filtered test runs work correctly
  - [x] `dotnet test --filter "Category=Rendering"` returns only rendering
        tests
  - [x] `dotnet test --filter "Category=Unit"` returns only unit tests
  - [x] Document filter commands in `testing-guide.md`

**Acceptance criteria:**
- Every test has at least a `Category` trait.
- Filtered runs by `Category` and `Feature` return expected subsets.
- `testing-guide.md` documents all trait values and filter examples.

---

## Phase 5 — Documentation–Test Alignment

**Goal:** Ensure documentation examples are consistent with actual tests
and vice versa.

- [x] Review all code examples in documentation
  - [x] Verify examples in `testing-guide.md` match real test commands
  - [x] Verify architecture descriptions match current code structure
  - [x] Update outdated references (file paths, class names, test counts)
- [x] Add "Related Tests" sections to roadmaps
  - [x] Each roadmap phase references the tests that validate it
  - [x] Each major test file references the documentation that explains its
        purpose
- [x] Update `testing-current-state.md` with current test counts and
  categories
- [x] Ensure all ADRs reference related tests where applicable

**Acceptance criteria:**
- No documentation references a test, class, or file that does not exist.
- Every roadmap phase links to its validation tests.

---

## Phase 6 — Quality Guidelines

**Goal:** Establish conventions to prevent documentation and test chaos
from recurring.

- [ ] Create `docs/CONTRIBUTING-DOCS.md` with documentation guidelines
  - [ ] Template for new roadmap documents
  - [ ] Template for new ADRs (already exists in ADR format)
  - [ ] Rules: every new document must be added to `docs/README.md`
  - [ ] Rules: every document must include a "Related Documents" section
- [ ] Create `docs/CONTRIBUTING-TESTS.md` with test guidelines
  - [ ] Required traits for every new test
  - [ ] Naming convention enforcement
  - [ ] Requirement: rendering-affecting changes must include at least one
        rendering-specific test
  - [ ] Baseline image update process
- [ ] Add a PR checklist item for documentation and test quality
  - [ ] "New/modified docs added to `docs/README.md`"
  - [ ] "Tests include appropriate `[Trait]` attributes"
  - [ ] "Rendering changes include rendering-specific tests"

**Acceptance criteria:**
- Guidelines documents exist and are linked from `docs/README.md`.
- PR template (or checklist) includes documentation and test quality gates.

---

## Milestones and Timeline

| Phase | Description | Estimated Effort |
|-------|-------------|-----------------|
| 1 | Documentation Index and Hierarchy | 1–2 days |
| 2 | Documentation Consolidation | 2–3 days |
| 3 | Rendering-Specific Test Design | 1–2 weeks |
| 4 | Test Organisation and Trait Standardisation | 3–5 days |
| 5 | Documentation–Test Alignment | 2–3 days |
| 6 | Quality Guidelines | 1–2 days |

Phases 1–2 (documentation) and Phases 3–4 (tests) can proceed in parallel.
Phase 5 requires both tracks to be substantially complete. Phase 6 is the
final step.

---

## Relationship to Other Roadmaps

- **[testing-roadmap.md](../testing-roadmap.md)** — the existing
  test-suite evolution plan. This roadmap complements it by addressing
  organisation and rendering focus rather than pipeline-layer coverage.
- **[w3c-html-compliance.md](w3c-html-compliance.md)** — W3C compliance
  work produces new tests; this roadmap ensures those tests follow
  consistent conventions.
- **[css2-verification-report-resolution.md](css2-verification-report-resolution.md)** —
  CSS2 differential testing is a key input to Phase 3's rendering test
  design.
- **[architecture-roadmap.md](../architecture-roadmap.md)** —
  engine separation directly affects which test boundaries are meaningful
  for rendering verification.

---

## Action Items

- [ ] Create tracking issues for each phase (Phase 1–6)
- [ ] Begin Phase 1 (documentation index) immediately — low effort, high
      impact
- [ ] Identify candidate cross-feature rendering scenarios for Phase 3
- [ ] Audit existing trait usage to scope Phase 4 effort

---

## Related Tests

| Test File | Project | Validates Phase |
|-----------|---------|-----------------|
| `RenderingOutputTests.cs` | `Broiler.App.Tests` | Phase 3 — rendering-specific tests |
| `CrossFeatureRenderingTests.cs` | `Broiler.App.Tests` | Phase 3 — cross-feature interaction tests |
| `PixelRegressionTests.cs` | `HtmlRenderer.Image.Tests` | Phase 3 — pixel regression baselines |
| `CrossChapterCss2InteractionTests.cs` | `HtmlRenderer.Image.Tests` | Phase 3 — cross-chapter CSS2 interactions |
| `RealWorldSnippetRenderingTests.cs` | `HtmlRenderer.Image.Tests` | Phase 3 — real-world snippet rendering |
| All test files with `[Trait]` attributes | All projects | Phase 4 — trait standardisation |
