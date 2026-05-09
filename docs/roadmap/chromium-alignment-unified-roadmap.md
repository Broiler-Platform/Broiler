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
12. [Resolved Decisions](#12-resolved-decisions)

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

Phase C0 establishes everything the recurring Phase C-N loop depends on. It is
broken into six concrete deliverables, each with explicit acceptance criteria.
Until **all** of them land on `main`, no per-release Chromium snapshot is
considered authoritative.

Pick a starting Chromium stable release **N0** at the time of adoption. At
roadmap-adoption time, **N0 = the latest Chromium stable on the day the first
C0 PR opens** — the exact number is recorded in
`docs/roadmap/snapshots/chromium-N0.md` once published.

#### C0.1 — Stand up the upstream-Chromium reference fetcher

1. Add `scripts/chromium/fetch-reference.ps1` (and its `.sh` counterpart) that,
   given a Chromium milestone number, downloads:
   - the Chromium release notes JSON from `chromiumdash.appspot.com/fetch_milestones`;
   - the matching V8 version and the WPT revision Chromium pinned for that
     milestone (read from `DEPS` at the corresponding tag);
   - the Test262 revision pinned by V8 at that release.
2. The script writes the resolved versions to
   `tests/m2-conformance/chromium-reference/chromium-N.lock.json`. This file
   is the **single source of truth** for "what does Chromium N mean" for every
   downstream step.
3. **Acceptance:** running the script for at least two adjacent Chromium
   stable versions produces deterministic lockfiles, and CI fails if a
   snapshot PR is opened without the matching lockfile.

#### C0.2 — Pin the WPT subsets that form the W4/W5 gate sets

The W4/W5 gates do **not** run all of WPT — that would be both noisy and
slower than Chromium's own WPT cadence. They run a fixed, version-controlled
subset chosen so that (a) it covers behaviour Broiler actually exercises and
(b) Chromium's own pass-rate on the subset is ≥ 99% (so any failure on our
side is a real Broiler gap, not an upstream flake).

The initial subset is fixed below and lives at
`tests/m2-conformance/wpt-subsets/chromium-aligned.toml`:

| Workstream | WPT directories included | Rationale |
|---|---|---|
| **W4 (HTML/CSS)** | `html/syntax/parsing/`, `html/semantics/forms/`, `html/semantics/scripting-1/`, `dom/nodes/`, `dom/ranges/`, `dom/traversal/`, `css/CSS2/`, `css/css-cascade/`, `css/css-color/`, `css/css-flexbox/`, `css/css-grid/`, `css/css-text/`, `css/css-fonts/`, `css/css-backgrounds/`, `css/css-display/` | Covers parser, DOM tree, cascade, layout primitives, and the CSS modules Broiler renders today (Acid3 surface + WPT visual subset). |
| **W5 (DOM/Web API)** | `dom/events/`, `dom/abort/`, `html/webappapis/timers/`, `html/webappapis/microtask-queuing/`, `html/webappapis/scripting/`, `fetch/api/basic/`, `fetch/api/headers/`, `fetch/api/request/`, `fetch/api/response/`, `content-security-policy/script-src/`, `content-security-policy/style-src/` | Covers the Bridge surface (events, microtasks, fetch subset, CSP) without pulling in service workers, web RTC, or other features Broiler does not implement. |

Adding or removing a directory from this table requires the **annual review**
(Section 9). The list is not changed mid-cycle.

#### C0.3 — Define the Test262 subset for G1

G1 runs the **language scope** of Test262 at the revision V8 pinned for
Chromium N (resolved by C0.1). Excluded by default:
`test/intl402/`, `test/staging/`, and any test marked `[noStrict]` plus
`[onlyStrict]` for the same feature (we run the strict variant only).

The exclude-list is committed at
`tests/m2-conformance/test262-es2025/chromium-aligned.exclude` and, like the
WPT subset, only changes at the annual review.

#### C0.4 — Lock the W6 performance reference hardware

The W6 budgets are meaningless without a fixed reference profile. The
adopted reference is:

- **Reference runner:** `windows-2022` GitHub-hosted runner (4 vCPU, 16 GB RAM,
  Standard_D4s_v3 class). This is what Broiler's CI already provisions, so
  no infra change is required.
- **Warm-up policy:** every benchmark runs 3 untimed warm-up iterations and
  reports the median of the next 7 timed iterations.
- **Numbers are only comparable within the reference profile.** Local
  developer numbers are informational only.

Phase C0 publishes a `bench/reference/README.md` documenting the profile and
the warm-up policy, and `bench/reference/baseline-N0.json` capturing the
first set of numbers so subsequent runs have something to diff against.

#### C0.5 — Author the snapshot template

Add `docs/roadmap/snapshots/_template.md` with the canonical structure every
per-release snapshot follows (one H2 per workstream, a "Deferred items" H2,
and a "Reference lockfile" pointer to the matching `chromium-N.lock.json`).
This guarantees snapshots are diffable across releases.

#### C0.6 — Bootstrap the unified dashboard CI job

Wire a CI workflow `chromium-alignment.yml` that, on every PR and on a
nightly schedule against `main`:

1. Reads the latest `chromium-N.lock.json`.
2. Runs the Test262 subset (C0.3), the WPT subsets (C0.2), the Acid2/Acid3
   captures, and the W7 raster reference captures.
3. Renders a single Markdown report under `artifacts/chromium-alignment/` and
   posts a summary comment on the PR.
4. **Fails the build** if any G1–G6 gate (Section 7) regresses against the
   previous snapshot's numbers committed on `main`.

**Phase C0 is complete** when (a) C0.1–C0.6 have all merged, (b) the first
real `docs/roadmap/snapshots/chromium-N0.md` has been published from CI
output (not hand-written), and (c) every non-zero delta in that snapshot has
a tracked issue under the `chromium-alignment` label.

### Phase C-N — Per-release tracking (recurring, every ~4 weeks)

For each subsequent Chromium stable release **N**, the steps below are
executed in order. Each step has an explicit owner and a concrete artefact;
none is allowed to be skipped — if a step cannot complete, the snapshot
records *why*, but the step is still attempted.

#### Day 0 — Chromium N ships

| # | Step | Owner | Artefact |
|---|---|---|---|
| 1 | Run `scripts/chromium/fetch-reference.ps1 N` to refresh `chromium-N.lock.json` | Standards Steward | Updated lockfile committed via PR |
| 2 | Bump the WPT and Test262 submodule pins to the revisions in the lockfile | Standards Steward | Same PR as step 1 |
| 3 | Re-capture the Acid2, Acid3, and WPT-visual reference PNGs by running them against the new Chromium build, store under `tests/m2-conformance/visual-reference/chromium-N/` | Graphics WG | PR adds the new reference set |
| 4 | Open the snapshot PR from the template (`cp _template.md chromium-N.md`); leave bodies empty for now | Standards Steward | Draft PR |

#### Day 1 – Day 7 — Delta computation and triage

| # | Step | Owner | Artefact |
|---|---|---|---|
| 5 | Trigger the `chromium-alignment.yml` workflow against the Day-0 PR; download the report artefact | Standards Steward | Markdown delta report |
| 6 | For each new failing test, file a `chromium-alignment` issue with: test ID, engine, workstream, the Chromium N it appeared in, and a candidate target Chromium release for closure | Engine maintainer (auto-routed by CODEOWNERS on the test path) | One issue per failing test, linked from the snapshot draft |
| 7 | For each *behaviour change* in Chromium N (read from the Chromium release notes JSON in step 1) that intersects a Broiler engine, file a "tracking" issue even if no test fails today (so we don't get caught by lazy-triggered failures) | Standards Steward | Issues labelled `chromium-alignment`, `behaviour-change` |
| 8 | Run the W6 benchmarks; if any metric is outside the ±5% band against the previous snapshot, file a `perf-regression` issue and link it from the snapshot | Performance Steward | Bench report committed under `bench/reports/chromium-N.json` |

#### Day 8 – Day 21 — Closing in-budget gaps

| # | Step | Owner | Artefact |
|---|---|---|---|
| 9 | Engine maintainers prioritise the issues opened in steps 6–8 by gate severity: G6 > G1 > G3 > G2 > G4 > G5 (i.e. dashboard-availability and JS-language regressions block the snapshot before pixel-fidelity ones) | Engines Working Group | Issues moved into "In progress" on the alignment project board |
| 10 | Each fix lands as a normal PR that references its `chromium-alignment` issue. The PR description **must** include the before/after numbers from the dashboard | PR author | Merged PRs linked from the issue |
| 11 | Issues that cannot land in this cycle are explicitly **deferred**: the issue is moved to the next Chromium release's milestone, and a one-line rationale is added to the snapshot's "Deferred items" section | Engine maintainer | Updated snapshot draft |

#### Day 22 – Day 28 — Snapshot publication

| # | Step | Owner | Artefact |
|---|---|---|---|
| 12 | Re-run the `chromium-alignment.yml` workflow against `main` and copy the rendered Markdown into the snapshot PR | Standards Steward | Filled-in `chromium-N.md` |
| 13 | Update [Section 4](#4-current-state-snapshot) of this roadmap with one paragraph per engine summarising the new state | Standards Steward | Same PR as step 12 |
| 14 | Get sign-off from each engine maintainer on the PR (one approving review per engine: JS, HTML, Bridge, Graphics) | Engine maintainers | Approved PR |
| 15 | Merge. The merged commit is tagged `chromium-N-snapshot` so it is trivially reachable from external links | Standards Steward | Tag pushed |

Snapshots are **append-only**: every release produces a new file, none are
edited after the merge in step 15. This gives a permanent, auditable history
of Broiler's alignment over time. Errata to a published snapshot live in the
*next* snapshot's "Errata" section, not in edits to the historical file.

### Phase C-N+ — Forward-looking work (continuous)

Work that *anticipates* the next Chromium release (intent-to-ship items,
TC39 stage-3 proposals, draft CSS modules) is allowed but is **not on the
critical path**. It is staged behind feature flags whose default is `off`,
so it cannot affect the gate measurements above, and it is only counted
toward compliance once it ships enabled-by-default in a Chromium stable
release. **Chromium origin-trial features are explicitly excluded from the
gate measurements** — origin trials are upstream experiments and
implementing against them risks chasing features that get withdrawn. This
default is reviewed at each annual revision (Section 9).

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

## 12. Resolved Decisions

These were the open questions raised during initial drafting; each has been
answered and folded into the relevant section. They are kept here as a
single-glance reference and as a decision log for future annual reviews.

| # | Question | Decision | Where it lives |
|---|---|---|---|
| 1 | Which WPT directories form the W4/W5 gate sets? | The fixed table in Phase C0.2 (15 dirs for W4, 11 for W5), pinned in `tests/m2-conformance/wpt-subsets/chromium-aligned.toml`. Membership only changes at the annual review. | Section 6, Phase C0.2 |
| 2 | What is the W6 performance reference hardware? | GitHub-hosted `windows-2022` runner (4 vCPU / 16 GB / Standard_D4s_v3), 3 untimed warm-ups + median of 7 timed iterations. Local numbers are informational only. | Section 6, Phase C0.4 |
| 3 | Are Chromium origin-trial features counted toward "current"? | **No.** Origin trials are upstream experiments and may be withdrawn; tracking them risks wasted work. Stage-3 TC39 proposals and intent-to-ship items follow the same rule (staged behind off-by-default flags, counted only once shipped enabled-by-default in Chromium stable). Reviewed annually. | Section 6, Phase C-N+ |
| 4 | What is the snapshot retention policy? | Snapshots are **kept indefinitely** in `docs/roadmap/snapshots/`. They are tiny (~50 KB each), and at one snapshot per ~4 weeks that is well under 1 MB/year of repository growth — not worth compacting. The Day-22 snapshot tag (`chromium-N-snapshot`) is also kept indefinitely. | Section 6, Phase C-N step 15 |
| 5 | What is the priority order between gates when work cannot finish in a cycle? | G6 > G1 > G3 > G2 > G4 > G5 — i.e. dashboard availability and JS-language regressions block before pixel and perf ones. Anything that cannot land is explicitly deferred to the next Chromium release with a one-line rationale in the snapshot. | Section 6, Phase C-N step 9 / step 11 |

Future open questions raised during operation of this roadmap are filed as
issues with the label `chromium-alignment` + `roadmap-question` and resolved
at the next annual review (Section 9), at which point they move into the
table above.
