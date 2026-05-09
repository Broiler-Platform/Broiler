# Unified Roadmap: Aligning Broiler Engines with Major Chromium Releases

> **Status**: **Active — single source of truth** for Broiler engine roadmap planning  
> **Supersedes**: All prior Broiler roadmaps (see [Section 11](#11-superseded-roadmaps))  
> **Scope**: Cross-engine — `Broiler.JavaScript`, `Broiler.HTML`, `Broiler.HtmlBridge`, and the
> graphics backend that supports them  
> **Owners**: Engines Working Group (JS, HTML, Bridge, Graphics) and Standards Steward  
> **Review cadence**: Re-published every Chromium major release (~4 weeks); see
> [Section 9](#9-governance-and-periodic-review)

---

## Table of Contents

1. [Why a Unified Roadmap](#1-why-a-unified-roadmap)
2. [Guiding Principle: Chromium as the Reference Implementation](#2-guiding-principle-chromium-as-the-reference-implementation)
3. [In-Scope Engines and Chromium Surface](#3-in-scope-engines-and-chromium-surface)
4. [Current State Snapshot](#4-current-state-snapshot)
5. [Chromium-Aligned Workstreams](#5-chromium-aligned-workstreams)
6. [Release Cadence and Phases](#6-release-cadence-and-phases)
7. [Compliance Gates and Exit Criteria](#7-compliance-gates-and-exit-criteria)
8. [Compliance-Gap Tracking](#8-compliance-gap-tracking)
9. [Governance and Periodic Review](#9-governance-and-periodic-review)
10. [Stakeholder Review](#10-stakeholder-review)
11. [Superseded Roadmaps](#11-superseded-roadmaps)
12. [Open Questions](#12-open-questions)

---

## 1. Why a Unified Roadmap

Broiler has historically maintained four parallel roadmaps — one per engine area
(`ECMASCRIPT_ROADMAP.md`), one per visual-fidelity test (`acid2-compliance-roadmap.md`),
one cross-engine portfolio plan (`engines-standards-and-performance-roadmap.md`),
and one for the graphics backend cutover (`skia-replacement-roadmap.md`). Each
was authored against a *different external reference*: ECMA-262 editions, the
1998 Acid2 test, an internal phase model, and SkiaSharp parity respectively.

The result was predictable: the engines drifted out of step. JavaScript work
shipped against ES2025 while DOM APIs lagged behind the corresponding HTML Living
Standard snapshot; CSS modules advanced at the rate of WPT triage rather than
the cadence of new Chromium releases; and the graphics backend was measured
against a frozen Skia baseline rather than against Chromium's current raster
behaviour.

This roadmap **replaces all four prior roadmaps** with a single plan whose
external reference is the **current Chromium stable release**. Everything that
ships in a Chromium milestone — language features, DOM APIs, CSS modules,
network behaviour, security primitives, raster output — is treated as a single,
versioned compliance target that all Broiler engines move toward together.

The four prior roadmaps remain in the repository for historical context but are
**marked deprecated** and link back here. See [Section 11](#11-superseded-roadmaps).

---

## 2. Guiding Principle: Chromium as the Reference Implementation

Chromium is the *de facto* baseline for the modern web platform: it ships every
~4 weeks, drives WHATWG/W3C/TC39 implementation reports, and is the reference
that web content is authored against. Aligning Broiler to Chromium directly
gives us four properties no spec-only target can:

- **A versioned, observable baseline.** "Chromium 126" is a single, reproducible
  artefact; "ES2025" is a moving set of stage-4 proposals. Anchoring to a
  Chromium milestone makes "are we current?" a yes/no question.
- **Tested behaviour, not just specified behaviour.** Chromium's WPT pass-rate
  per release is published; we can compute a *delta* per engine per release.
- **Predictable cadence.** Chromium's 4-week train gives us a natural review
  rhythm and forces small, frequent updates rather than rare large ones.
- **A single compliance vocabulary.** Every Broiler engine talks about the same
  "Chromium N" target instead of three different specification snapshots.

**The principle, stated operationally:**

> For every Chromium stable release **N**, Broiler publishes a compliance
> snapshot describing, per engine, the delta against Chromium N's behaviour on
> the agreed conformance suites (Test262, WPT, Acid2/3, and the
> Chromium-aligned raster reference). No Broiler engine is considered "current"
> until its delta against the latest Chromium stable is within the budget set
> in [Section 7](#7-compliance-gates-and-exit-criteria).

This does **not** mean Broiler reimplements Chromium internals. It means the
*observable behaviour* of Broiler's engines is measured and gated against
Chromium's, release by release.

---

## 3. In-Scope Engines and Chromium Surface

| Broiler engine | Chromium subsystem we align to | Primary conformance signal |
|---|---|---|
| `Broiler.JavaScript` | V8 (language semantics only — not internals) | Test262 ES2025+, plus the language-level subset of WPT executed under Chromium N |
| `Broiler.HTML` (parser, DOM tree, CSS, layout) | Blink: HTML parser, Style, Layout | WPT html/, css/, dom/ suites, scored against Chromium N's published pass rates |
| `Broiler.HtmlBridge` | Blink bindings + Web Platform APIs (Events, CSSOM, Fetch subset, CSP, microtasks) | WPT dom/, html/webappapis/, fetch/, content-security-policy/ |
| Graphics backend (managed raster path) | Blink/Skia raster output | Pixel-diff against Chromium N reference captures for Acid2, Acid3, and the WPT visual subset |

**Out of scope** (covered by separate, area-specific docs that are *not*
roadmaps and remain authoritative for their topic):

- Application shell (`Broiler.HTML.WPF`, `Broiler.App`) — product planning.
- PDF parser (`Broiler.Pdf`) — see `docs/roadmap/broiler-pdf-native-parser.md`.
- Tooling (`Broiler.Cli`, LogAnalyzer) — see
  `docs/roadmap/log-analyzer-enhancements.md`.

These area docs are **not** deprecated; only the four engine roadmaps listed
in [Section 11](#11-superseded-roadmaps) are replaced by this document.

---

## 4. Current State Snapshot

This section is refreshed at every periodic review (see
[Section 9](#9-governance-and-periodic-review)). At the time this roadmap was
adopted:

- **`Broiler.JavaScript`** — broad ES2024 coverage, ES2025 work in progress
  per the deprecated `ECMASCRIPT_ROADMAP.md`. Test262 results published per PR
  on the ES2025 subset under `tests/m2-conformance/test262-es2025/`.
- **`Broiler.HTML`** — Acid3 score 100/100 under the current capture/script
  harness; Acid2 content-area match 80.00%; targeted WPT triage in progress
  per `docs/roadmap/wpt-failure-triage.md`.
- **`Broiler.HtmlBridge`** — focused M2 DOM/CSSOM/microtask coverage published
  in `tests/m2-conformance/bridge-targeted/bridge-targeted-summary.md`.
- **Graphics backend** — mid-migration from SkiaSharp to a Broiler-owned raster
  path (workstream W7 below).

A unified per-engine delta against the **latest Chromium stable** does not yet
exist; building it is the first deliverable of this roadmap (see Phase C0
below).

---

## 5. Chromium-Aligned Workstreams

Workstream codes carry over from the prior cross-engine roadmap so existing
sub-issues continue to map cleanly. The *anchor* of each workstream is
re-pointed at Chromium.

| # | Workstream | Anchored against | Deliverable per Chromium release |
|---|---|---|---|
| **W1** | Unified conformance dashboard | Test262, WPT, Acid2/3 under Chromium N | Single CI report per PR with per-engine delta vs. Chromium N |
| **W2** | Engine-boundary hardening | Blink's bindings layer (as an architectural reference) | Stable typed seams JS ↔ Bridge ↔ HTML; no behaviour change leaks across seams |
| **W3** | JavaScript language compliance | V8's shipped feature set in Chromium N (TC39 stage-4 features actually enabled by default) | Close all Test262 deltas in language scope vs. Chromium N |
| **W4** | HTML/CSS rendering compliance | Blink's WPT pass rates for html/, css/, dom/ in Chromium N | Net-positive per-suite pass-rate delta each release; zero regressions |
| **W5** | DOM / Web API compliance | Blink's bindings under Chromium N for Events, CSSOM, Fetch (subset), CSP, microtasks | Per-API parity table refreshed per release |
| **W6** | Performance budgets | Blink/V8 capture timings on the same reference pages | Per-engine perf budget enforced in CI; no silent regressions |
| **W7** | Graphics backend completion | Blink raster output for the WPT visual subset and Acid2/3 | Pixel-diff parity ≥ agreed threshold against Chromium N captures |

Each workstream produces, per Chromium release, a **single line in the
unified compliance dashboard**.

---

## 6. Release Cadence and Phases

Phases are no longer numbered against an internal milestone clock. They are
keyed to Chromium stable releases and named **C-N** where N is the Chromium
major version we are tracking.

### Phase C0 — Bootstrapping the Chromium baseline (one-time)

Before any per-release tracking can begin, we must establish the baseline:

1. Pick a starting Chromium stable release **N0** at the time of adoption.
2. Capture Chromium N0's observable behaviour on the agreed suites (Test262
   subset, WPT subsets enumerated in W4/W5, Acid2/3 captures, and a small set
   of raster reference captures for W7).
3. Run the same suites against `main` and publish the **initial delta** per
   workstream.
4. Convert each non-zero delta into a tracked compliance gap (see
   [Section 8](#8-compliance-gap-tracking)).

Phase C0 is complete when the unified dashboard renders at least one full
per-engine delta against Chromium N0 in CI.

### Phase C-N — Per-release tracking (recurring, every ~4 weeks)

For each subsequent Chromium stable release **N**:

1. **Day 0 (Chromium N ships).** Refresh the reference captures and WPT/Test262
   expectations for Chromium N. This is mechanical and lives in
   `tests/m2-conformance/` and the W7 reference set.
2. **Day 0 – Day 7.** Recompute deltas per engine. Open or update sub-issues
   for any new gaps surfaced by Chromium N.
3. **Day 7 – Day 21.** Engineering work to close in-budget gaps (see
   [Section 7](#7-compliance-gates-and-exit-criteria)).
4. **Day 21 – Day 28.** Publish the **Chromium N compliance snapshot** in
   `docs/roadmap/snapshots/chromium-N.md` (template added in Phase C0) and
   update [Section 4](#4-current-state-snapshot) of this roadmap.

Snapshots are append-only: every release produces a new file, none are edited
after publication. This gives a permanent, auditable history of Broiler's
alignment over time.

### Phase C-N+ — Forward-looking work (continuous)

Work that *anticipates* the next Chromium release (origin-trial features,
intent-to-ship items, TC39 stage-3 proposals) is allowed but is **not on the
critical path**. It is staged behind feature flags and only counted toward
compliance once it ships in a Chromium stable release.

---

## 7. Compliance Gates and Exit Criteria

Every workstream has the same shape of gate, parameterised per engine. A
release is "in budget" when **all** of the following hold against Chromium N:

| Gate | Metric | Target (default) |
|---|---|---|
| **G1 — JS language** | Test262 pass-rate delta vs. Chromium N on the agreed subset | ≤ 0.5% gap, no new failures |
| **G2 — HTML/CSS** | WPT pass-rate delta vs. Chromium N on `html/`, `css/`, `dom/` subsets | Net-positive vs. previous release; no regressions |
| **G3 — DOM/Web API** | WPT pass-rate delta on `dom/`, `html/webappapis/`, `fetch/`, `content-security-policy/` subsets | Net-positive vs. previous release; no regressions |
| **G4 — Visual fidelity** | Pixel-diff vs. Chromium N captures for Acid2, Acid3, WPT visual subset | ≥ current published thresholds; no regressions |
| **G5 — Performance** | Per-engine benchmark suite | Within ±5% of previous release on each tracked metric |
| **G6 — No silent failure** | CI pipeline | Dashboard publishes per release; missing data is itself a failure |

Per-release exemptions are allowed (Chromium occasionally ships behaviour
changes that we deliberately defer) but **must** be recorded in the snapshot
file with a rationale and a target release for closure.

---

## 8. Compliance-Gap Tracking

Each non-zero delta surfaced by the dashboard becomes a tracked gap. Gaps are
filed as GitHub issues with the label `chromium-alignment` and reference:

- the Chromium release that surfaced the gap (e.g. `chromium:N`);
- the engine and workstream (`engine:javascript`, `workstream:W3`);
- the specific test ID(s) (Test262 id, WPT path, or capture name);
- a target Chromium release by which the gap should close, or an explicit
  "deferred" decision with rationale.

Gaps that close are not deleted; they are marked closed and remain searchable
under the `chromium-alignment` label, giving a per-test history of when the
alignment landed. Long-standing gaps surface in the next periodic review
(Section 9) and become roadmap-level decisions.

---

## 9. Governance and Periodic Review

| Cadence | Activity | Owner |
|---|---|---|
| Every Chromium stable release (~4 weeks) | Publish the Chromium-N compliance snapshot; refresh [Section 4](#4-current-state-snapshot) | Standards Steward |
| Quarterly | Engines Working Group review of trends across the last ~3 snapshots; re-balance workstream priorities | Engines Working Group |
| Annually | Re-evaluate this roadmap end-to-end; adjust scope, gates, and budgets; confirm Chromium remains the right anchor | All stakeholders ([Section 10](#10-stakeholder-review)) |
| Ad-hoc | Chromium ships a behaviour change that breaks an in-budget gate | Standards Steward calls an out-of-cycle review |

The annual review is the *only* mechanism by which this roadmap is amended.
Per-release snapshots are append-only and do not edit the roadmap text outside
[Section 4](#4-current-state-snapshot).

---

## 10. Stakeholder Review

This roadmap is reviewed and signed off by the following groups before each
annual revision. Per-release snapshots do not require sign-off — they are
mechanical outputs of CI.

- **Engines Working Group** — JS, HTML, Bridge, and Graphics maintainers.
- **Standards Steward** — owns conformance suites and Chromium delta tooling.
- **Performance Steward** — owns the W6 benchmark suite and budgets.
- **Release Management** — confirms the Chromium cadence remains compatible
  with Broiler's own release cadence.
- **Documentation owner** — keeps superseded roadmaps' deprecation notices
  pointing at this document.

The initial adoption of this roadmap (the change that introduces it and
deprecates the four prior roadmaps) is itself the first stakeholder review.

---

## 11. Superseded Roadmaps

The following roadmaps are **deprecated** as of the adoption of this document.
They remain in the repository for historical reference and continue to be
linked from related issues, but they are **not** the source of truth for
engine planning. New planning, sub-issues, and gates live in this document and
its per-release snapshots.

| Deprecated roadmap | Topic | Replacement section in this roadmap |
|---|---|---|
| [`Broiler.JavaScript/ECMASCRIPT_ROADMAP.md`](../../Broiler.JavaScript/ECMASCRIPT_ROADMAP.md) | ECMAScript edition tracking | W3 (Section 5) + G1 (Section 7) |
| [`acid/acid2/acid2-compliance-roadmap.md`](../../acid/acid2/acid2-compliance-roadmap.md) | Acid2 visual fidelity | W4 + W7 (Section 5) + G4 (Section 7) |
| [`docs/roadmap/engines-standards-and-performance-roadmap.md`](./engines-standards-and-performance-roadmap.md) | Cross-engine portfolio plan | All of Sections 5–7 |
| [`docs/roadmap/skia-replacement-roadmap.md`](./skia-replacement-roadmap.md) | Graphics backend cutover | W7 (Section 5) + G4 (Section 7) |

Each of those documents now carries a deprecation notice at the top pointing
back to this file.

Area docs that are **not** roadmaps (`acid3-compliance.md`,
`acid-test-triage.md`, `wpt-failure-triage.md`, `engines-m0-baseline.md`,
`google-search-compliance.md`, `javascript-engine-assembly-refactor.md`,
`broiler-pdf-native-parser.md`, `log-analyzer-enhancements.md`) remain the
authoritative reference for their respective topics and are **not**
deprecated.

---

## 12. Open Questions

These are tracked as items for the first stakeholder review after adoption:

1. **Subset selection.** Exactly which WPT directories under `html/`, `css/`,
   `dom/`, `fetch/`, and `content-security-policy/` form the gate sets in
   W4/W5? The first Chromium N0 snapshot will propose an initial cut.
2. **Performance reference hardware.** W6 budgets need a fixed hardware
   profile; choosing it is a Phase C0 deliverable.
3. **Origin-trial features.** Should any Chromium origin-trial features be
   counted toward "current"? Default in this roadmap is **no**; revisit at
   the first annual review.
4. **Snapshot retention.** Per-release snapshots accumulate; agree on whether
   any compaction is acceptable beyond ~24 months.
