# Roadmap: Advancing Broiler Engines to Full Standards Compliance and Performance

> **Status**: **M2 implementation complete** — W3, W4, and W5 are published/verified; the unified dashboard now carries the focused Phase 2 JS, HTML/CSS, and Bridge signals of record
> **Tracking issue**: [#1064 — Implement Engines Standards and Performance Roadmap](https://github.com/MaiRat/Broiler/issues/1064)
> **Scope**: Cross-engine — covers `Broiler.JavaScript`, `Broiler.HTML`, and `Broiler.HtmlBridge`

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [In-Scope Engines and Boundaries](#2-in-scope-engines-and-boundaries)
3. [Current State Analysis](#3-current-state-analysis)
4. [Guiding Principles](#4-guiding-principles)
5. [Cross-Engine Workstreams](#5-cross-engine-workstreams)
6. [Phases and Milestones](#6-phases-and-milestones)
7. [Per-Engine Task Breakdown](#7-per-engine-task-breakdown)
8. [Measurable Goals and Exit Criteria](#8-measurable-goals-and-exit-criteria)
9. [Interoperability, Testing, and Benchmarking](#9-interoperability-testing-and-benchmarking)
10. [Bottlenecks and Major Challenges](#10-bottlenecks-and-major-challenges)
11. [Estimated Timelines and Resourcing](#11-estimated-timelines-and-resourcing)
12. [Governance, Tracking, and Review Cadence](#12-governance-tracking-and-review-cadence)
13. [Execution Backlog and Proposed Sub-Issues](#13-execution-backlog-and-proposed-sub-issues)
14. [Implementation Notes and Key Decisions](#14-implementation-notes-and-key-decisions)
15. [Related Roadmaps](#15-related-roadmaps)
16. [Open Questions](#16-open-questions)

---

## 1. Executive Summary

Broiler is composed of three tightly coupled engines:

- **`Broiler.JavaScript`** — the YantraJS-derived ECMAScript engine that executes
  page scripts and host bindings.
- **`Broiler.HTML`** — the managed HTML/CSS rendering engine (parsing, layout,
  cascade, painting) and its image/graphics backend.
- **`Broiler.HtmlBridge`** — the DOM ↔ JavaScript bridge that exposes the HTML
  engine's tree, style, events, and resource pipeline to the JavaScript
  runtime, and orchestrates rendering stages.

This document is the **meta-roadmap** that sequences the work required to bring
all three engines to **full web-platform standards compliance** (ECMA-262,
WHATWG HTML/DOM, WHATWG Fetch, CSS WG modules, WPT-measured behavior) and to a
**measurable, sustained performance baseline** suitable for everyday browsing
workloads.

It does **not** restate the deep, per-area roadmaps that already exist in the
repository. Instead, it links them, fills gaps they do not cover (notably
HtmlBridge), and provides the cross-cutting milestones, exit criteria, and
performance targets that bind them together. See
[Section 15](#15-related-roadmaps) for the existing roadmaps this plan
incorporates by reference.

The recommended strategy is **specification-first, seam-driven, and
measurement-gated**:

- every milestone is anchored to an external specification or test suite (WPT,
  Test262, Acid3) rather than to internal feature lists;
- engine boundaries (`Broiler.JavaScript` ↔ `Broiler.HtmlBridge` ↔
  `Broiler.HTML`) are hardened before behavior is changed, so compliance work
  on one engine does not regress another;
- no milestone is considered "done" until both **conformance** (test pass rate)
  and **performance** (benchmark threshold) gates are met simultaneously.

---

## 2. In-Scope Engines and Boundaries

| Engine | Primary location | Standards surface | Performance surface |
|---|---|---|---|
| `Broiler.JavaScript` | `Broiler.JavaScript/` (YantraJS fork) | ECMA-262 (ES2025+), Test262 | Script throughput, startup time, GC pressure, cold/warm parse |
| `Broiler.HTML` | `Broiler.HTML/Source/Broiler.HTML.*` | WHATWG HTML parsing, DOM, CSS 2.1 + selected CSS3 modules, WPT | Layout time, paint time, raster throughput, memory footprint |
| `Broiler.HtmlBridge` | `src/Broiler.HtmlBridge/` | WHATWG DOM/UI Events, HTML script execution model, CSP, Fetch (subset) | Bridge call overhead, microtask latency, image pipeline cost |

**Out of scope for this roadmap:**

- the WPF shell (`Broiler.HTML.WPF`, `Broiler.App`) — covered by app-level
  product planning;
- the PDF parser (`Broiler.Pdf`) — covered by
  [`broiler-pdf-native-parser.md`](./broiler-pdf-native-parser.md);
- the LogAnalyzer tooling — covered by
  [`log-analyzer-enhancements.md`](./log-analyzer-enhancements.md).

---

## 3. Current State Analysis

### 3.1 `Broiler.JavaScript`

- Language baseline: broad ES2024 coverage with ES2025 support tracked in
  detail in [`Broiler.JavaScript/ECMASCRIPT_ROADMAP.md`](../../Broiler.JavaScript/ECMASCRIPT_ROADMAP.md).
- Architecture is split across parser, AST, compiler (Linq/expression
  compilers), runtime, builtins, modules, and storage projects, each with
  dedicated test projects.
- Conformance signal today: focused unit/integration tests under
  `Broiler.JavaScript.*.Tests` plus the PR-gated ES2025 Test262 subset in
  `tests/m2-conformance/test262-es2025/`.
- Performance signal today: limited — there is no cross-engine benchmark
  harness comparable to the JS micro/macro suites used by mainstream engines.

### 3.2 `Broiler.HTML`

- Layered into `Broiler.HTML.Core`, `.Dom`, `.CSS`, `.Adapters`, `.Image`,
  `.Image.Compat`, `.Orchestration`, `.Primitives`, `.Rendering`, `.Utils`, and
  `.WPF`.
- Standards signal today: Acid3 score of **100/100** under the current
  capture/script harness (see
  [`acid3-compliance.md`](./acid3-compliance.md)); ongoing visual-fidelity
  triage tracked there and in [`acid-test-triage.md`](./acid-test-triage.md);
  WPT triage tracked in [`wpt-failure-triage.md`](./wpt-failure-triage.md);
  the focused M2 HTML/CSS conformance signal is published in
  [`../../tests/m2-conformance/html-css-targeted/html-css-targeted-summary.md`](../../tests/m2-conformance/html-css-targeted/html-css-targeted-summary.md).
- The graphics backend is mid-migration from SkiaSharp to a Broiler-owned
  raster path; see [`skia-replacement-roadmap.md`](./skia-replacement-roadmap.md).
- Performance signal today: pixel-diff and capture timings exist for
  individual pages; there is no continuously tracked layout/paint benchmark.

### 3.3 `Broiler.HtmlBridge`

- Exposes the DOM, CSS, events, traversal, anchor/animation resolvers,
  selectors, and stylesheet plumbing to the JS engine via a partial
  `DomBridge` class split (`DomBridge.*.cs`).
- Hosts the script execution lifecycle (`ScriptEngine`,
  `IScriptEngine`, `ScriptExecutionResult`, `MicroTaskQueue`,
  `ScriptProfilingHook`) and the rendering-stage orchestration
  (`RenderingStages`, `HtmlPostProcessor`, `ImagePipeline`).
- Standards signal today: the focused M2 Bridge DOM/Web compliance signal is
  published in
  [`../../tests/m2-conformance/bridge-targeted/bridge-targeted-summary.md`](../../tests/m2-conformance/bridge-targeted/bridge-targeted-summary.md),
  covering the roadmap-targeted DOM events, CSSOM, and microtask ordering
  slices alongside the broader Acid3/WPT coverage.
- Performance signal today: the `ScriptProfilingHook` provides per-script
  timing, but there is no aggregate bridge-overhead benchmark.

### 3.4 Cross-Cutting Observations

- The three engines share **no common conformance dashboard** today; pass
  rates live inside per-area docs.
- Performance work is currently **reactive** (driven by visible regressions in
  capture/diff workflows) rather than gated by benchmark budgets.
- Several existing roadmaps already define detailed plans inside each engine;
  what is missing is a **portfolio-level sequence** that prevents the engines
  from drifting out of step.

---

## 4. Guiding Principles

1. **Spec before code.** Every behavior change cites a specification section or
   a WPT/Test262 test ID.
2. **Seams before behavior.** Cross-engine refactors land first as
   non-behavior-changing seams (see the existing graphics-backend cutover
   pattern in `docs/architecture/graphics-backend-cutover.md`).
3. **Parallel backends during risky work.** Where feasible, new
   implementations run alongside the legacy path, gated by configuration and
   measured for parity before the cutover.
4. **Measurement-gated milestones.** A milestone is complete only when its
   conformance target *and* its performance budget are both met, and both are
   automatically tracked in CI.
5. **No silent regressions.** New compliance fixes must not regress
   performance benchmarks beyond an agreed band, and vice versa.
6. **Scope discipline.** Each milestone has an explicit "non-goals" list to
   keep deliverables small enough to land within the milestone window.

---

## 5. Cross-Engine Workstreams

The roadmap is organized into seven workstreams that span all three engines.
Phases (Section 6) draw work from these streams in parallel.

| # | Workstream | Owners (engines) | Outcome |
|---|---|---|---|
| W1 | **Conformance harness unification** | JS + HTML + Bridge | Single CI dashboard reporting Test262, WPT, and Acid3 deltas per PR |
| W2 | **Engine-boundary hardening** | Bridge + JS + HTML | Stable, typed seams between JS ↔ Bridge ↔ HTML; no leaking internals |
| W3 | **JavaScript language compliance** | JS | ES2025 ratified features complete; ES2026 staged |
| W4 | **HTML/CSS rendering compliance** | HTML | Targeted WPT/CSS module pass-rate climb; Acid-test deep parity |
| W5 | **DOM / Web API compliance** | Bridge (+ JS, HTML) | Standards-correct DOM, Events, CSSOM, Fetch (subset), CSP, microtasks |
| W6 | **Performance baseline and budgets** | All | Continuous benchmark suite with per-engine budgets enforced in CI |
| W7 | **Graphics backend completion** | HTML | Finish Skia replacement per existing roadmap; remove legacy seams |

---

## 6. Phases and Milestones

Phases are sequential at the *exit-criteria* level, but most workstreams run
in parallel within a phase. Milestone codes (`M0`–`M5`) are used in
[Section 8](#8-measurable-goals-and-exit-criteria) for the goal table.

### Phase 0 — Baseline and Instrumentation (Milestone **M0**) — **Completed 2026-05-05**

Establishes the measurement floor that every later milestone is graded
against. No user-visible behavior changes.

- Stand up a unified conformance dashboard (W1) that runs Test262 (subset),
  WPT (relevant suites), and the Acid3 capture test on every PR.
- Stand up a baseline benchmark harness (W6) covering: JS micro/startup, HTML
  parse, CSS cascade, layout, paint/raster, and end-to-end page capture.
- Publish the current pass rates and benchmark numbers as the **baseline of
  record**.
- Document the engine boundaries currently in use by `Broiler.HtmlBridge` and
  identify all leaky abstractions (W2).

**Completion record:** The M0 baseline of record, unified PR dashboard, and
current HtmlBridge boundary/leak map are now published in
[`engines-m0-baseline.md`](./engines-m0-baseline.md),
[`../architecture/htmlbridge-engine-boundaries.md`](../architecture/htmlbridge-engine-boundaries.md),
and [`.github/workflows/engines-m0-dashboard.yml`](../../.github/workflows/engines-m0-dashboard.yml).

### Phase 1 — Boundary Hardening and Spec Mapping (Milestone **M1**) — **Completed 2026-05-05**

Prepares the engines for compliance-driven change without altering behavior.

- Lock the `IScriptEngine`/`DomBridge` surface and remove engine-internal
  types from the public bridge surface (W2).
- Map every existing `Broiler.HtmlBridge` API to its WHATWG/W3C spec section
  and flag non-compliant or missing behavior.
- Migrate any remaining public Skia-typed surfaces per
  [`skia-replacement-roadmap.md`](./skia-replacement-roadmap.md) (W7).
- Land a per-PR perf-regression gate on the budgets defined in Phase 0 (W6).

**Completion record:** The frozen HtmlBridge public seam, the
`Broiler.HtmlBridge` API/spec map, the remaining public Skia-surface cleanup,
and the representative per-PR benchmark gate are now published in
[`../architecture/htmlbridge-engine-boundaries.md`](../architecture/htmlbridge-engine-boundaries.md),
[`../architecture/htmlbridge-spec-map.md`](../architecture/htmlbridge-spec-map.md),
[`skia-replacement-roadmap.md`](./skia-replacement-roadmap.md),
and [`.github/workflows/engines-m0-dashboard.yml`](../../.github/workflows/engines-m0-dashboard.yml).

### Phase 2 — Targeted Compliance Push (Milestone **M2**)

The first phase that meaningfully moves pass rates.

- Close the ratified **ES2025** gap in `Broiler.JavaScript` (W3) — Iterator
  Helpers, `Promise.try`, Set methods, RegExp `/v` flag completion, etc., per
  the [ECMAScript roadmap](../../Broiler.JavaScript/ECMASCRIPT_ROADMAP.md).
- Land the highest-impact WPT/Acid items from
  [`wpt-failure-triage.md`](./wpt-failure-triage.md) and
  [`acid-test-triage.md`](./acid-test-triage.md) (W4).
- Bring DOM Events, CSSOM `getComputedStyle`, and microtask scheduling in the
  bridge to spec for the cases exercised by WPT today (W5).
- Eliminate fallback-only Skia paths still on the hot raster path (W7).

**Completion record:** The W3 ES2025 subset, the W4 HTML/CSS targeted suite,
and the W5 Bridge DOM/Web targeted suite are now all published and wired into
the unified PR dashboard via
[`tests/m2-conformance/test262-es2025/`](../../tests/m2-conformance/test262-es2025/),
[`../../tests/m2-conformance/html-css-targeted/html-css-targeted-summary.md`](../../tests/m2-conformance/html-css-targeted/html-css-targeted-summary.md),
[`../../tests/m2-conformance/bridge-targeted/bridge-targeted-summary.md`](../../tests/m2-conformance/bridge-targeted/bridge-targeted-summary.md),
and [`.github/workflows/engines-m0-dashboard.yml`](../../.github/workflows/engines-m0-dashboard.yml).

### Phase 3 — Web API Surface Expansion (Milestone **M3**)

Broadens what real-world pages can rely on.

- Add the **Fetch** API subset (request/response, basic CORS posture) and
  align `XMLHttpRequest` semantics with the Fetch model (W5).
- Complete **structured clone**, `MessageChannel`/`postMessage` between
  documents, and `queueMicrotask` semantics (W5).
- Land a compliant **CSS Selectors Level 4** matcher in the cascade engine
  and align `:is()`/`:where()`/`:has()` specificity rules (W4).
- Stage **ES2026** proposals (Decorators, AsyncContext, etc.) behind a
  feature flag in `Broiler.JavaScript` (W3).

### Phase 4 — Performance Hardening (Milestone **M4**)

Performance is treated as a first-class deliverable, not a side effect.

- Hit the per-engine performance budgets in
  [Section 8](#8-measurable-goals-and-exit-criteria) on the reference
  hardware profile.
- Land the JavaScript engine assembly refactor described in
  [`javascript-engine-assembly-refactor.md`](./javascript-engine-assembly-refactor.md)
  to reduce cold-start cost and assembly footprint.
- Optimize the HtmlBridge hot paths identified by `ScriptProfilingHook`
  aggregates (selector lookup, attribute mutation, style invalidation).
- Complete the Broiler-owned raster fast paths (text, gradients, blends)
  introduced by the Skia replacement work and remove the parallel SkiaSharp
  backend from default builds (W7).

### Phase 5 — Steady-State Compliance and Continuous Improvement (Milestone **M5**)

Transitions the project from "catch-up" to "stay-current".

- Subscribe to the **TC39** stage-4 cadence and the **WHATWG**
  living-standard change feed; every ratified change becomes an issue within
  one release cycle.
- Adopt a published **support matrix** ("what Broiler implements") with
  per-feature WPT coverage links.
- Treat any pass-rate or benchmark regression as a release-blocking bug.

---

## 7. Per-Engine Task Breakdown

Each task is an actionable engineering item; full sub-task lists live in the
referenced roadmaps where they already exist.

### 7.1 `Broiler.JavaScript`

- **Compliance**
  - Close the ES2025 gap (W3, M2). Source of truth:
    [`ECMASCRIPT_ROADMAP.md`](../../Broiler.JavaScript/ECMASCRIPT_ROADMAP.md).
  - Wire **Test262** into CI with a published pass-rate trend (W1, M0–M1).
  - Resolve the limitations enumerated in the ECMAScript roadmap's "Known
    Limitations" section (W3, M2–M3).
  - Stage ES2026 proposals behind feature flags (W3, M3).
- **Performance**
  - Execute the assembly refactor in
    [`javascript-engine-assembly-refactor.md`](./javascript-engine-assembly-refactor.md)
    (W6, M4).
  - Add a JS benchmark suite (parser throughput, function-call overhead,
    object property access, GC stress, JSON parse) tied to per-PR budgets
    (W6, M0–M1).
  - Profile and reduce allocation churn in the Linq/expression compiler hot
    paths (W6, M4).

### 7.2 `Broiler.HTML`

- **Compliance**
  - Continue executing
    [`acid3-compliance.md`](./acid3-compliance.md) and
    [`acid-test-triage.md`](./acid-test-triage.md) deep-visual TODOs (W4,
    M2–M3).
  - Expand WPT coverage per
    [`wpt-failure-triage.md`](./wpt-failure-triage.md) and report a single
    aggregate pass rate (W1, M0–M2).
  - Implement Selectors L4 specificity and modern pseudo-classes in the
    cascade (W4, M3).
  - Track Google Search rendering parity per
    [`google-search-compliance.md`](./google-search-compliance.md) as a
    real-page sanity gate (W4, M2–M4).
- **Performance**
  - Add layout, paint, and end-to-end capture benchmarks to the unified
    harness (W6, M0).
  - Finish [`skia-replacement-roadmap.md`](./skia-replacement-roadmap.md)
    through the cutover phase and remove the SkiaSharp default backend
    (W7, M2–M4).
  - Reduce style-recalc cost via incremental invalidation aligned with the
    bridge's mutation events (W6, M4).

### 7.3 `Broiler.HtmlBridge`

- **Compliance**
  - Produce a **bridge surface ↔ spec** mapping document covering every
    `DomBridge.*.cs` partial (W2, M1).
  - Bring `MicroTaskQueue` and script-evaluation order in line with the
    HTML "perform a microtask checkpoint" algorithm (W5, M2).
  - Implement the WHATWG **Fetch** subset and align `XMLHttpRequest` with it
    (W5, M3).
  - Tighten `ContentSecurityPolicy.cs` to the CSP3 algorithm for the
    directives currently honored, and document gaps explicitly (W5, M2–M3).
  - Audit `DomBridge.Events.cs` for spec-correct event dispatch
    (capture/target/bubble, `composedPath`, `passive` listeners) (W5, M2).
  - Audit `DomBridge.Selectors.cs` against Selectors L4 in lock-step with the
    HTML cascade work (W5, M3).
- **Performance**
  - Add bridge-overhead micro-benchmarks (DOM call round-trip, attribute
    set/get, classList mutation, computed-style read) (W6, M0).
  - Cache style/computed-value lookups in the bridge using the HTML engine's
    invalidation signals (W6, M4).
  - Promote `ScriptProfilingHook` aggregates into a per-PR perf report
    (W6, M1).

---

## 8. Measurable Goals and Exit Criteria

Every milestone has both a **conformance** and a **performance** gate. All
numbers are *targets relative to the M0 baseline of record*; absolute values
are filled in at Phase 0 close-out and tracked in the unified dashboard.

| Milestone | JavaScript (Test262) | HTML/CSS (WPT subset) | DOM/Web (WPT subset) | Acid3 capture | Performance gate |
|---|---|---|---|---|---|
| **M0** Baseline ✅ | published in [`engines-m0-baseline.md`](./engines-m0-baseline.md) | published in [`engines-m0-baseline.md`](./engines-m0-baseline.md) | published in [`engines-m0-baseline.md`](./engines-m0-baseline.md) | 100/100 maintained and documented | benchmark baselines published |
| **M1** Boundary | no regression vs. M0 | no regression vs. M0 | no regression vs. M0 | 100/100 | per-PR regression gate live (≤ 2 % slowdown band) |
| **M2** Targeted | **+15 pp** over M0 (ES2025 closed) | **+10 pp** over M0 | **+10 pp** over M0 | 100/100 + new visual-fidelity TODOs closed | no benchmark > 5 % slower than M0 |
| **M3** Web APIs | **+5 pp** over M2 | **+10 pp** over M2 | **+15 pp** over M2 (Fetch, microtasks, structured clone) | 100/100 | no benchmark > 2 % slower than M0 |
| **M4** Perf | no regression vs. M3 | no regression vs. M3 | no regression vs. M3 | 100/100 | **JS startup −30 %**, **HTML parse −20 %**, **layout −20 %**, **end-to-end capture −25 %** vs. M0 |
| **M5** Steady-state | tracks TC39 stage-4 within one release | tracks WHATWG within one release | tracks WHATWG within one release | 100/100 | budgets re-baselined annually; no rolling 90-day regressions |

**Exit criteria common to every milestone:**

- the unified dashboard shows green for both gates for **two consecutive
  releases**;
- all new public APIs introduced in the milestone have spec citations and
  test coverage;
- the milestone's "non-goals" list has not silently grown.

---

## 9. Interoperability, Testing, and Benchmarking

### 9.1 Conformance Suites

- **Test262** for `Broiler.JavaScript` — run a curated subset on PR, full run
  nightly. Pass-rate delta is a PR comment.
- **Web Platform Tests (WPT)** — run an HTML/DOM/CSSOM/Fetch slice tied to
  the engine areas under change; full slice nightly. Triaged via
  [`wpt-failure-triage.md`](./wpt-failure-triage.md).
- **Acid3** — keep the existing 100/100 capture as a release-blocking
  regression gate; deepen visual-fidelity coverage per
  [`acid3-compliance.md`](./acid3-compliance.md).
- **Real-page parity** — keep the Google Search capture parity check
  (`google-search-compliance.md`) as a smoke gate for end-to-end behavior.

### 9.2 Benchmark Harness

A single harness produces results for all three engines so cross-engine
trade-offs are visible:

- **JS**: micro (function call, property access, regex, JSON), startup
  (cold/warm parse + first execution), GC pressure under allocation churn.
- **HTML**: parse throughput (bytes/s), CSS cascade time, layout time on a
  fixture set, paint time, end-to-end capture time.
- **Bridge**: DOM call round-trip latency, mutation throughput, microtask
  drain latency, image-pipeline throughput.

Benchmarks publish JSON results to a stable schema so the dashboard can chart
trends without per-suite custom code.

### 9.3 Reference Hardware Profile

Performance budgets are evaluated against a **single declared reference
profile** (CPU class, RAM, OS, .NET version) so numbers are comparable across
PRs. The profile is documented alongside the M0 baseline.

### 9.4 Interoperability Considerations

- Engine boundary changes that affect bridge consumers (W2) must ship with a
  versioned `IScriptEngine`/`DomBridge` surface and a compatibility note.
- The graphics backend cutover (W7) must preserve the public `BBitmap`/
  `BColor` surface as already established by the Skia replacement work; new
  compliance work must not reintroduce backend-specific types in public
  APIs.
- Fetch / CSP / module-loader behavior must be co-developed across the JS
  engine, the bridge, and the HTML resource loader to avoid divergent
  policies.

---

## 10. Bottlenecks and Major Challenges

1. **No unified pass-rate signal today.** Until W1 lands, every compliance
   claim has to be re-derived per area. This is the single largest blocker
   to coordinated milestone gating and is the first deliverable of M0.
2. **Bridge is the hidden critical path.** Most JS-visible bugs ultimately
   route through `DomBridge.*.cs`; large compliance jumps in either JS or
   HTML will surface bridge gaps that have no dedicated test suite today.
3. **Graphics-backend migration overlaps performance work.** The Skia
   replacement is mid-flight; until W7 finishes, paint/raster benchmarks will
   move for backend reasons rather than algorithmic ones, complicating budget
   enforcement.
4. **Test262 and WPT runtime cost.** Full runs are expensive in CI; the
   roadmap relies on curated PR subsets plus nightly fulls. Choosing the
   right subsets is itself an ongoing task.
5. **ES2025 / ES2026 churn.** The JavaScript spec continues to evolve; M5
   institutionalizes the response, but mid-roadmap proposals may force
   re-prioritization.
6. **Resource concentration.** Several roadmaps (Skia replacement, ECMAScript
   compliance, Acid3 deep-visual, WPT triage) compete for the same
   contributor pool; phases are sequenced to keep no more than two heavy
   workstreams active at once.

---

## 11. Estimated Timelines and Resourcing

Estimates are **planning bands**, not commitments, and assume the
contributor concentration described in Section 10. Calendar lengths assume
parallel execution of the workstreams listed for each phase.

| Phase / Milestone | Calendar band | Engineering effort (person-months) | Primary workstreams |
|---|---|---|---|
| Phase 0 / **M0** | 1–2 months | 3–4 pm | W1, W6 |
| Phase 1 / **M1** | 2–3 months | 5–7 pm | W2, W6, W7 (continuation) |
| Phase 2 / **M2** | 3–5 months | 10–14 pm | W3, W4, W5, W7 |
| Phase 3 / **M3** | 4–6 months | 12–16 pm | W3, W4, W5 |
| Phase 4 / **M4** | 3–5 months | 8–12 pm | W6, W7 (closeout), W3 (perf) |
| Phase 5 / **M5** | continuous | ~1 pm / month sustaining | All |

**Resource roles required across the roadmap:**

- 1 JS-engine engineer (parser/compiler/runtime depth)
- 1 layout/CSS engineer
- 1 graphics/raster engineer (shared with W7 closeout)
- 1 bridge / DOM-API engineer
- 0.5 test-infrastructure engineer (W1, W6 harness)
- 0.5 release/perf engineer (CI gates, dashboards)

Roles may be combined when contributors have overlap, but **W1 and W6 must
have a single accountable owner each** for the duration of the roadmap.

---

## 12. Governance, Tracking, and Review Cadence

- **Issue labels.** Each workstream has a label (`engine:js`, `engine:html`,
  `engine:bridge`, `area:perf`, `area:conformance`); milestones map to GitHub
  milestones.
- **Per-release review.** At every release, the dashboard snapshot is
  attached to the release notes; pass-rate or benchmark regressions block
  the release.
- **Quarterly roadmap review.** Phases, budgets, and the non-goals lists are
  re-validated quarterly. Re-prioritization is recorded in this document's
  changelog.
- **Specification change feed.** TC39 stage-4 promotions and WHATWG
  living-standard updates are reviewed monthly; new items become tracked
  issues against the appropriate engine.

---

## 13. Execution Backlog and Proposed Sub-Issues

The umbrella tracker for execution is [issue #1064](https://github.com/MaiRat/Broiler/issues/1064).
The items below are the **ready-to-file sub-issue queue** for the major
deliverables in this roadmap. Existing area-specific roadmap documents remain
the source of truth for detailed task lists; these issue seeds define the
cross-engine milestone framing, labels, and dependencies needed to execute the
plan.

| Proposed sub-issue | Workstreams | Milestone window | Suggested labels | Depends on | Deliverable / exit signal |
|---|---|---|---|---|---|
| **Stand up the unified conformance dashboard** | W1 | M0 | `engine:js`, `engine:html`, `engine:bridge`, `area:conformance` | #1064 | Publish PR/nightly reporting for Test262, WPT, and Acid3 deltas from one place |
| **Establish baseline benchmark harness and budgets** | W6 | M0 | `engine:js`, `engine:html`, `engine:bridge`, `area:perf` | #1064 | Capture the M0 baseline of record for JS, HTML, bridge, and end-to-end capture metrics |
| **Document and harden JS ↔ Bridge ↔ HTML boundaries** | W2 | M1 | `engine:js`, `engine:html`, `engine:bridge`, `area:conformance` | dashboard + benchmark baselines | Produce a versioned bridge-surface/spec map and remove leaking engine-internal types from public seams |
| **Close the ES2025 compliance gap in `Broiler.JavaScript`** | W3 | M2 | `engine:js`, `area:conformance` | dashboard, boundary map | Land the ratified ES2025 features tracked in `ECMASCRIPT_ROADMAP.md` with Test262 trend reporting |
| **Execute the targeted HTML/CSS compliance push** | W4 | M2–M3 | `engine:html`, `area:conformance` | dashboard, boundary map | Retire the highest-value Acid/WPT failures and publish a single HTML/CSS pass-rate signal in [`tests/m2-conformance/html-css-targeted/`](../../tests/m2-conformance/html-css-targeted/) |
| **Bring `Broiler.HtmlBridge` DOM/Web APIs to current roadmap targets** | W5 | M2–M3 | `engine:bridge`, `area:conformance` | boundary map, dashboard | Close the bridge-level gaps for events, CSSOM, microtasks, CSP, Fetch subset, and structured clone |
| **Finish the graphics backend cutover and remove legacy default paths** | W7 | M1–M4 | `engine:html`, `area:perf` | dashboard, benchmark baselines | Complete the Skia-replacement roadmap through default-build cutover without reintroducing backend-specific public APIs |
| **Hit roadmap performance budgets and wire per-PR regression gates** | W6 | M1–M4 | `engine:js`, `engine:html`, `engine:bridge`, `area:perf` | baseline benchmark harness, boundary hardening | Enforce the roadmap budget bands in CI and close the M4 optimization goals |
| **Adopt steady-state standards tracking and support-matrix governance** | W1–W6 | M5 | `engine:js`, `engine:html`, `engine:bridge`, `area:conformance`, `area:perf` | M0–M4 complete | Convert the roadmap from catch-up execution to release-by-release sustaining work |

### 13.1 Recommended filing order

File the sub-issues in the sequence below so baseline work lands before
behavioral or performance-sensitive changes:

1. Unified conformance dashboard (W1 / M0)
2. Baseline benchmark harness and budgets (W6 / M0)
3. Engine-boundary hardening + bridge surface/spec map (W2 / M1)
4. Graphics backend completion continuation (W7 / M1–M4)
5. JavaScript ES2025 compliance push (W3 / M2)
6. HTML/CSS compliance push (W4 / M2–M3)
7. HtmlBridge DOM/Web API compliance push (W5 / M2–M3)
8. Performance hardening and CI regression gates (W6 / M1–M4)
9. Steady-state governance/support matrix follow-up (M5)

### 13.2 Milestone-to-sub-issue checklist

- [x] **M0** — baseline/dashboard deliverables published in [`engines-m0-baseline.md`](./engines-m0-baseline.md) and [`.github/workflows/engines-m0-dashboard.yml`](../../.github/workflows/engines-m0-dashboard.yml)
- [x] **M1** — file and start the W2/W7 continuation issues; document the frozen public seams
- [x] **M2** — targeted compliance push published across the focused JS, HTML/CSS, and Bridge signals, with the umbrella kickoff tracker [#1072](https://github.com/MaiRat/Broiler/issues/1072) now backed by the Phase 2 dashboard slices of record
  - [x] **W3 / JS** — [Close the ES2025 compliance gap in `Broiler.JavaScript`](#close-the-es2025-compliance-gap-in-broilerjavascript)
  - [x] **W4 / HTML** — [Execute the targeted HTML/CSS compliance push](#execute-the-targeted-htmlcss-compliance-push)
  - [x] **W5 / Bridge** — [Bring `Broiler.HtmlBridge` DOM/Web APIs to current roadmap targets](#bring-broilerhtmlbridge-domweb-apis-to-current-roadmap-targets)
- [ ] **M3** — expand W4/W5/W3 follow-ups for Fetch, structured clone, Selectors L4, and staged ES2026 work
- [ ] **M4** — file optimization/gating follow-ups tied to the published benchmark budget deltas
- [ ] **M5** — file sustaining-governance issue(s) for standards feed intake and support-matrix publication

---

## 14. Implementation Notes and Key Decisions

These notes are the in-repo companion to issue
[#1064](https://github.com/MaiRat/Broiler/issues/1064). Keep them short,
dated, and decision-oriented so contributors can see why the next set of
sub-issues was filed or re-prioritized.

### 14.1 Implementation notes

- **2026-05-06** — W5 close-out: published the focused Bridge Milestone 2
  signal in
  [`tests/m2-conformance/bridge-targeted/bridge-targeted-summary.md`](../../tests/m2-conformance/bridge-targeted/bridge-targeted-summary.md),
  wired [`.github/workflows/engines-m0-dashboard.yml`](../../.github/workflows/engines-m0-dashboard.yml)
  to run and summarize that DOM/event/CSSOM/microtask suite on every PR, and
  re-verified the full W5 slice locally at **167 / 167 passed** across the
  focused event, edge-case dispatch, CSSOM, async, and microtask-ordering
  coverage.
- **2026-05-06** — W4 close-out: published the focused HTML/CSS Milestone 2
  signal in
  [`tests/m2-conformance/html-css-targeted/html-css-targeted-summary.md`](../../tests/m2-conformance/html-css-targeted/html-css-targeted-summary.md),
  wired [`.github/workflows/engines-m0-dashboard.yml`](../../.github/workflows/engines-m0-dashboard.yml)
  to run and summarize that combined suite on every PR, and re-verified the
  full W4 slice locally at **70 / 70 passed** across the WPT-derived CSS
  suites, the focused Acid3/selector regressions, and the Google Search parity
  gate.
- **2026-05-06** — W3 close-out: tightened the `Broiler.JavaScript` ES2025
  surface by fixing iterator-helper callback counters, `Promise.try` receiver
  validation, Set-method set-like handling, and the remaining `RegExp.escape`
  coercion/whitespace gaps; extended the in-repo Test262 harness to load
  harness includes and enforce minimum pass rates; and published the focused
  ES2025 subset baseline under `tests/m2-conformance/test262-es2025/` with a
  PR gate in [`.github/workflows/engines-m0-dashboard.yml`](../../.github/workflows/engines-m0-dashboard.yml).
- **2026-05-05** — M2 targeted-compliance kickoff: filed the umbrella tracker as
  [#1072](https://github.com/MaiRat/Broiler/issues/1072) to cover the
  milestone-wide standards/performance push spanning JS ES2025 compliance,
  targeted HTML/Acid/WPT work, bridge DOM/CSSOM/microtask alignment, and the
  remaining hot-path graphics backend cleanup. The milestone checklist now
  treats the umbrella issue as started while keeping the W3/W4/W5 per-engine
  follow-ups explicitly queued in the recommended filing order.
- **2026-05-05** — M1 boundary/spec-map kickoff: published
  `docs/architecture/htmlbridge-engine-boundaries.md` as the frozen
  `htmlbridge-public-surface/v1` contract, added
  `docs/architecture/htmlbridge-spec-map.md` to map the public
  `Broiler.HtmlBridge` API to WHATWG/W3C anchors with explicit partial and
  non-standard flags, and wired the benchmark harness plus PR workflow to fail
  on mean slowdowns above the M1 ≤ 2% budget versus the committed M0 JSON
  baseline for the current representative per-engine gate metrics
  (`js.startup`, `html.raster`, `bridge.mutation`).
- **2026-04-30** — Execution kickoff: converted this document from a pure draft
  into an execution tracker by adding a ready-to-file sub-issue backlog, a
  milestone filing order, and a milestone checklist aligned to `#1064`.
- **2026-04-30** — JavaScript roadmap continuation: repaired the focused
  `Broiler.JavaScript.BuiltIns.Tests` project after namespace drift and added
  ES2025 coverage for Iterator helpers, `Promise.try`, `RegExp.escape`, and the
  remaining Set methods. `dotnet test Broiler.JavaScript/Broiler.JavaScript.BuiltIns.Tests/Broiler.JavaScript.BuiltIns.Tests.csproj`
  now passes with 68/68 tests green.
- **2026-04-30** — HtmlBridge event-audit continuation: added support for
  `addEventListener` option objects (`capture`, `once`, `passive`) and
  `Event.composedPath()` across bridge-dispatched DOM events, then covered the
  behavior in focused DOM-event regressions. `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~DomEvents`
  now passes with 43 tests green and 2 existing skips.
- **2026-04-30** — HtmlBridge microtask continuation: moved the CLI capture
  path off inline `queueMicrotask` execution, added per-task microtask
  checkpoints to bridge timer flushing, and aligned `ScriptEngine` /
  `InteractiveSession` script sequencing with that checkpoint model. Focused
  regression coverage for same-script, cross-script, and timer-task ordering now
  passes via `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~QueueMicrotask|FullyQualifiedName~Microtasks_Between"`.
- **2026-04-30** — HtmlBridge Fetch continuation: added bridge-level
  `Headers`, `Request`, and `Response` constructors, taught `fetch()` to accept
  `Request` instances and constructor-backed headers, and kept `XMLHttpRequest`
  aligned by routing it through the same response/header primitives. Focused
  bridge network coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~NetworkAndHttpTests`.
- **2026-04-30** — HtmlBridge CSP continuation: expanded
  `ContentSecurityPolicy` beyond eval-only parsing to honor the currently wired
  script directives (`default-src`, `script-src`, `script-src-elem`) for
  inline-script, external-script, nonce, hash, `self`, and eval decisions,
  applied that policy during CLI/App script extraction and runtime eval
  registration, and documented the still-unimplemented CSP3 gaps inline in the
  bridge policy model. Focused regression coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~ContentSecurityPolicyTests`.
- **2026-04-30** — HTML/HtmlBridge selectors continuation: upgraded CSS
  specificity calculation to honor Selectors L4 function semantics for
  `:is()`, `:where()`, `:has()`, `:not()`, and `:nth-child(... of selector)`,
  and fixed style-rule extraction so comma-separated selector groups are not
  split inside functional pseudo-class arguments. Focused regressions now pass
  via `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~SelectorsLevel4SpecificityTests`.
- **2026-04-30** — HTML/HtmlBridge DOM continuation: wired
  `HTMLTableRowElement.insertCell()` / `deleteCell()` and completed
  `HTMLSelectElement.selectedIndex` assignment plus selection-backed `value`
  reads/writes so the previously skipped Acid3 table/select regressions now run
  green. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~Acid3HtmlElementRegressionTests`.
- **2026-04-30** — HTML/HtmlBridge SVG continuation: exposed
  `SVG_LENGTHTYPE_*` constants on bridge-created `SVGLength` instance objects so
  `SVGAnimatedLength.baseVal` / `animVal` match the existing global
  `SVGLength` registration closely enough for the skipped Acid3 constant check
  to run green. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~Acid3SvgAndParsingRegressionTests`.
- **2026-04-30** — HTML/HtmlBridge DOM collection continuation: taught
  document/element `getElementsByTagName('*')` paths to treat `'*'` as the
  wildcard element selector instead of an exact tag-name match, which clears the
  remaining skipped Acid3 numeric-coercion regression around live collection
  lengths. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~Acid3SpecialRegressionTests`.
- **2026-04-30** — HTML engine cascade continuation: started preserving
  computed specificity and stylesheet source order for per-bucket CSS block
  application in the image/rendering pipeline, which clears the skipped Acid3
  debug regression where a later low-specificity selector incorrectly beat an
  earlier higher-specificity one without `!important`. Focused coverage now
  passes via `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~Acid3CascadeDebugTests`
  and `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~CssImportantCascadeTests`.
- **2026-04-30** — HtmlBridge DOM lifecycle continuation: taught bridge node
  insertion paths to adopt moved subtrees into the destination document by
  propagating the destination `ownerDocument` root across cross-document
  `appendChild`, `insertBefore`, `replaceChild`, and document-level append
  operations. The previously skipped Acid3 cross-document lifecycle regressions
  now run green via `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~Acid3_Test26|FullyQualifiedName~Acid3_Test27"`
  and the focused edge-case suite remains green via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~DomEventsEdgeCaseTests`.
- **2026-05-04** — Google Search parity continuation: unskipped the focused
  logo-colour regression by relaxing the blue-pixel threshold to match Google
  blue (`#4285F4`), then re-enabled the footer-region parity check after
  teaching absolute-position boxes with right/bottom-only insets to re-anchor
  themselves after auto-sized layout against the viewport/positioned containing
  block. The simplified `position:absolute; bottom:0` footer now renders in the
  bottom viewport band, and the focused real-page sanity gate is fully green
  via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~GoogleSearchComplianceTests`.
- **2026-05-04** — HtmlBridge hit-testing continuation: seeded internal
  computed-style resolution with key HTML user-agent `display` defaults and
  stopped normal-flow geometry from counting `display:none` siblings, which
  fixes the focused document hit-testing regressions where hidden metadata and
  script nodes were displacing visible targets. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~Document_ElementFromPoint_Uses_Hit_Test_Order_And_Skips_PointerEvents_None|FullyQualifiedName~Document_ElementsFromPoint_Returns_Target_Then_Ancestors_And_Viewport_Bounds"`.
- **2026-05-04** — HtmlBridge DOM Events continuation: taught bridge-created
  `MouseEvents` to expose `initMouseEvent()` alongside the existing
  `initEvent()` / `initUIEvent()` helpers, including the standard mouse
  coordinates, modifier keys, `button`, `detail`, `view`, and `relatedTarget`
  fields. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CreateEvent_MouseEvents_Has_InitMouseEvent|FullyQualifiedName~PhaseF_Test30_DispatchEvent_AddRemoveListener"`.
- **2026-05-04** — HtmlBridge DOM Events continuation: completed the next
  bridge-created `MouseEvents` surface slice by seeding default mouse fields and
  exposing the common `x` / `y` aliases plus `buttons` state alongside
  `clientX`, `clientY`, `button`, and `relatedTarget`. Focused coverage now
  passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CreateEvent_MouseEvents_Has_Alias_Properties_And_Default_Button_State|FullyQualifiedName~CreateEvent_MouseEvents_Has_InitMouseEvent|FullyQualifiedName~PhaseF_Test30_DispatchEvent_AddRemoveListener"`.
- **2026-05-04** — HtmlBridge DOM Events continuation: added deprecated
  `initFocusEvent()` support on bridge-created events in both document and
  sub-document factories, including `view`, UIEvent `detail`, and
  `relatedTarget` initialization for focused compatibility coverage. Focused
  coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CreateEvent_FocusEvents_Has_InitFocusEvent|FullyQualifiedName~DomEvents"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~DomEventsEdgeCaseTests`.
- **2026-05-04** — HtmlBridge DOM Events continuation: added deprecated
  `initKeyboardEvent()` support on bridge-created events in both document and
  sub-document factories, including default keyboard fields plus compatibility
  initialization for `key`, `location`, modifier keys, `keyCode`, `charCode`,
  and `which`. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CreateEvent_KeyboardEvents_Has_InitKeyboardEvent|FullyQualifiedName~DomEvents"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~DomEventsEdgeCaseTests`.
- **2026-05-04** — HtmlBridge DOM Events continuation: added deprecated
  `initWheelEvent()` support on bridge-created events in both document and
  sub-document factories, including default wheel delta fields, mouse-position
  aliases, and compatibility parsing for the legacy `modifiersList` argument to
  seed modifier state. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CreateEvent_WheelEvents_Has_InitWheelEvent|FullyQualifiedName~DomEvents"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~DomEventsEdgeCaseTests`.
- **2026-05-04** — HtmlBridge DOM Events continuation: added deprecated
  `initCustomEvent()` support on bridge-created events in both document and
  sub-document factories so `document.createEvent('CustomEvent')` can seed
  `type`, bubbling flags, cancelability, and payload `detail` compatibly with
  older DOM event call sites. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CreateEvent_CustomEvent_Has_InitCustomEvent|FullyQualifiedName~SubDoc_CreateEvent_CustomEvent_Has_InitCustomEvent|FullyQualifiedName~DomEvents|FullyQualifiedName~SvgDomAndCrossDocTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~DomEventsEdgeCaseTests`.
- **2026-05-04** — HtmlBridge DOM Events continuation: exposed the
  read-only-style `isTrusted` surface on bridge-created events in both document
  and sub-document factories, defaulting script-created events to `false` for
  compatibility with standard `document.createEvent(...)` behavior. Focused
  coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CreateEvent_Event_Has_IsTrusted_False|FullyQualifiedName~SubDoc_CreateEvent_Event_Has_IsTrusted_False|FullyQualifiedName~DomEvents|FullyQualifiedName~SvgDomAndCrossDocTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~DomEventsEdgeCaseTests`.
- **2026-05-04** — HtmlBridge DOM Events continuation: exposed `timeStamp` on
  bridge-created events in both document and sub-document factories, seeding
  script-created events with a Unix-millisecond timestamp so
  `document.createEvent(...)` surfaces a numeric creation time compatibly with
  standard DOM event behavior. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CreateEvent_Event_Has_TimeStamp|FullyQualifiedName~SubDoc_CreateEvent_Event_Has_TimeStamp|FullyQualifiedName~DomEvents|FullyQualifiedName~SvgDomAndCrossDocTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~DomEventsEdgeCaseTests`.
- **2026-05-04** — HtmlBridge DOM Events continuation: exposed legacy
  `srcElement`, `cancelBubble`, and `returnValue` compatibility surfaces on
  bridge-created events in both document and sub-document factories, and wired
  dispatch-time alias behavior so legacy event code can observe targets, stop
  bubbling, and cancel default handling through older DOM event entry points.
  Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CreateEvent_Event_Has_Legacy_Alias_Properties|FullyQualifiedName~SubDoc_CreateEvent_Event_Has_Legacy_Alias_Properties|FullyQualifiedName~Legacy_Event_Aliases_Track_Dispatch_State|FullyQualifiedName~DomEvents|FullyQualifiedName~SvgDomAndCrossDocTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~DomEventsEdgeCaseTests`.
- **2026-05-04** — HtmlBridge DOM Events continuation: exposed the
  `KeyboardEvent.repeat` surface on bridge-created keyboard events in both
  document and sub-document factories, wiring `initKeyboardEvent(...)` to seed
  the repeat flag from the legacy argument list while preserving existing
  `keyCode`/`charCode` compatibility parsing. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CreateEvent_KeyboardEvents_Has_Repeat_Property|FullyQualifiedName~SubDoc_CreateEvent_KeyboardEvents_Has_Repeat_Property|FullyQualifiedName~DomEvents|FullyQualifiedName~SvgDomAndCrossDocTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~DomEventsEdgeCaseTests`.
- **2026-05-04** — HtmlBridge DOM Events continuation: added modern
  `Event` and `CustomEvent` constructors on the bridge window surface, routing
  them through the existing `document.createEvent(...)` factories so constructor
  created events inherit the newer compatibility fields like `isTrusted`,
  `timeStamp`, `srcElement`, and payload `detail` in both main-document and
  sub-document contexts. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~Event_Constructor_Seeds_Init_Dictionary|FullyQualifiedName~CustomEvent_Constructor_Reuses_CreateEvent_Surface|FullyQualifiedName~SubDoc_Event_Constructor_Uses_SubWindow_Surface|FullyQualifiedName~SubDoc_CustomEvent_Constructor_Uses_SubWindow_Surface|FullyQualifiedName~DomEvents|FullyQualifiedName~SvgDomAndCrossDocTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~DomEventsEdgeCaseTests`.
- **2026-05-04** — HtmlBridge DOM Events continuation: added modern typed
  `MouseEvent`, `FocusEvent`, `KeyboardEvent`, and `WheelEvent` constructors on
  the bridge window surface and sub-document windows, routing them through the
  existing `document.createEvent(...)` factories so constructor-created events
  inherit the same compatibility fields and aliases as the legacy init-method
  path. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~MouseEvent_Constructor|FullyQualifiedName~FocusEvent_Constructor|FullyQualifiedName~KeyboardEvent_Constructor|FullyQualifiedName~WheelEvent_Constructor|FullyQualifiedName~DomEvents|FullyQualifiedName~SvgDomAndCrossDocTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~DomEventsEdgeCaseTests`.
- **2026-05-04** — HtmlBridge DOM Events continuation: added the modern
  `UIEvent` constructor on the bridge window surface and sub-document windows,
  routing it through the existing `document.createEvent('UIEvents')` factory so
  constructor-created UI events inherit the bridge event compatibility fields
  like `timeStamp` while reusing the established `initUIEvent(...)` plumbing.
  Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~UIEvent_Constructor|FullyQualifiedName~DomEvents|FullyQualifiedName~SvgDomAndCrossDocTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter FullyQualifiedName~DomEventsEdgeCaseTests`.
- **2026-05-04** — HtmlBridge DOM Events continuation: added `element.focus()`
  and `element.blur()` bridge methods, dispatching non-bubbling focus/blur
  events through the existing event propagation path so target listeners and
  inline handlers observe spec-aligned `focus` / `blur` delivery without a
  separate dispatch path. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~Element_Focus|FullyQualifiedName~Element_Blur|FullyQualifiedName~DomEvents"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~Focus_And_Blur_Do_Not_Bubble|FullyQualifiedName~DomEventsEdgeCaseTests"`.
- **2026-05-04** — HtmlBridge CSSOM continuation: added
  `CSSStyleDeclaration.length`, `item(index)`, and `getPropertyPriority(name)`
  on bridge style objects, and taught `style.setProperty(name, value,
  priority)` plus `getPropertyValue(...)`/property reads to round-trip
  `!important` priorities in a CSSOM-aligned way for inline styles exercised by
  the current bridge tests. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~Style_Length_And_Item|FullyQualifiedName~Style_GetPropertyPriority|FullyQualifiedName~Style_SetProperty_Priority"`.
- **2026-05-04** — HtmlBridge computed-style CSSOM continuation: added
  `getComputedStyle(...).length`, `item(index)`, and
  `getPropertyPriority(name)` on computed style objects, while normalizing
  exposed computed values so `getPropertyValue(...)` and direct property reads
  do not leak authored `!important` suffixes. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~GetComputedStyle_Length_And_Item|FullyQualifiedName~GetComputedStyle_Priority_Is_Empty_And_Values_Are_Normalized"`.
- **2026-05-04** — HtmlBridge inline CSSOM continuation: taught
  `CSSStyleDeclaration.getPropertyValue(...)` and direct property reads to
  resolve longhands from authored shorthands (for example `margin-left` from
  `margin`, or `border-left-width` from `border`) without inflating
  `style.length`, `item(index)`, or `cssText` with synthesized declarations.
  Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~Style_GetPropertyValue_Expands_Inline_Margin_Shorthand|FullyQualifiedName~Style_SetProperty_Shorthand_Resolves_Longhands_Without_Changing_Enumeration|FullyQualifiedName~Style_CssText_Setter_Resolves_Border_Longhands_Without_Duplicating_Declarations"`.
- **2026-05-04** — HtmlBridge stylesheet CSSOM continuation: taught
  `CSSRule.style` objects to expose the same core `CSSStyleDeclaration`
  surface as inline styles, including `cssText`, `setProperty(...)`,
  `getPropertyValue(...)`, `removeProperty(...)`, `getPropertyPriority(...)`,
  `length`, and `item(index)`, while keeping camelCase and kebab-case property
  access working for stylesheet rules. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CssRule_Style|FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests"`.
- **2026-05-04** — HtmlBridge stylesheet CSSOM continuation: added
  backreferences for `CSSRule.parentStyleSheet`, `CSSRule.parentRule`, and
  `CSSStyleDeclaration.parentRule`, wiring stylesheet rule declarations back to
  their owning `CSSRule` while keeping inline and computed style objects
  correctly exposed as `null` parent-rule surfaces. Focused coverage now passes
  via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CssRule_Style|FullyQualifiedName~ParentRule|FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`.
- **2026-05-04** — HtmlBridge stylesheet CSSOM continuation: exposed
  `@media` rules as `CSSMediaRule` objects in `document.styleSheets[...].cssRules`,
  including `type === 4`, `media`, nested `cssRules`, and `cssText`, while
  preserving parent-rule/style-sheet backreferences through nested rules and
  keeping nested stylesheet-rule mutations reflected in the enclosing media
  rule's serialized `cssText`. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CssMediaRule|FullyQualifiedName~CssRule_Style|FullyQualifiedName~ParentRule|FullyQualifiedName~CssRenderingTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`.
- **2026-05-04** — HtmlBridge stylesheet CSSOM continuation: exposed
  `@import` rules as `CSSImportRule` objects in `document.styleSheets[...].cssRules`,
  including `type === 3`, `href`, `media`, and `cssText`, and taught the
  stylesheet rule-string collector to retain top-level semicolon at-rules so
  import entries participate in mixed rule ordering alongside media, font-face,
  and style rules. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CssImportRule|FullyQualifiedName~CssMediaRule|FullyQualifiedName~CssRule_Style|FullyQualifiedName~ParentRule|FullyQualifiedName~CssRenderingTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`.
- **2026-05-04** — HtmlBridge stylesheet CSSOM continuation: exposed
  `@keyframes` rules as `CSSKeyframesRule` objects in `document.styleSheets[...].cssRules`,
  including `type === 7`, `name`, nested `cssRules`, and `cssText`, while also
  surfacing nested keyframe blocks as `CSSKeyframeRule`-like objects with
  `type === 8`, `keyText`, `style`, and parent-rule/style-sheet backreferences
  so keyframe mutations rebuild the enclosing rule's serialized `cssText`.
  Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CssKeyframesRule|FullyQualifiedName~CssImportRule|FullyQualifiedName~CssMediaRule|FullyQualifiedName~CssRule_Style|FullyQualifiedName~ParentRule|FullyQualifiedName~CssRenderingTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`.
- **2026-05-04** — HtmlBridge stylesheet CSSOM continuation: exposed
  `@supports` rules as `CSSSupportsRule` objects in
  `document.styleSheets[...].cssRules`, including `type === 11`,
  `conditionText`, nested `cssRules`, and `cssText`, while reusing nested
  rule parent-rule/style-sheet backreferences so mutations inside the supports
  block rebuild the enclosing rule's serialized `cssText`. Focused coverage now
  passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CssSupportsRule|FullyQualifiedName~CssKeyframesRule|FullyQualifiedName~CssImportRule|FullyQualifiedName~CssMediaRule|FullyQualifiedName~CssRule_Style|FullyQualifiedName~ParentRule|FullyQualifiedName~CssRenderingTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`.
- **2026-05-04** — HtmlBridge stylesheet CSSOM continuation: exposed
  `@layer` entries as `CSSLayerRule`-like objects in
  `document.styleSheets[...].cssRules`, covering both block
  (`@layer name { ... }`) and statement (`@layer name;`) forms with
  `type === 12`, `name`, nested `cssRules`, and `cssText`, while preserving
  nested rule parent-rule/style-sheet backreferences so mutations inside layer
  blocks rebuild the enclosing rule's serialized `cssText`. Focused coverage
  now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CssLayerRule|FullyQualifiedName~CssSupportsRule|FullyQualifiedName~CssKeyframesRule|FullyQualifiedName~CssImportRule|FullyQualifiedName~CssMediaRule|FullyQualifiedName~CssRule_Style|FullyQualifiedName~ParentRule|FullyQualifiedName~CssRenderingTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`.
- **2026-05-04** — HtmlBridge stylesheet CSSOM continuation: exposed
  `@namespace` entries as `CSSNamespaceRule`-like objects in
  `document.styleSheets[...].cssRules`, including `type === 9`,
  `namespaceURI`, optional `prefix`, and `cssText`, while supporting both
  quoted-string and `url(...)` declaration forms and keeping namespace entries
  in mixed top-level rule ordering with import, layer, supports, and style
  rules. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CssNamespaceRule|FullyQualifiedName~CssLayerRule|FullyQualifiedName~CssSupportsRule|FullyQualifiedName~CssKeyframesRule|FullyQualifiedName~CssImportRule|FullyQualifiedName~CssMediaRule|FullyQualifiedName~CssRule_Style|FullyQualifiedName~ParentRule|FullyQualifiedName~CssRenderingTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`.
- **2026-05-04** — HtmlBridge stylesheet CSSOM continuation: exposed
  `@page` entries as `CSSPageRule`-like objects in
  `document.styleSheets[...].cssRules`, including `type === 6`,
  `selectorText`, `style`, and `cssText`, while preserving top-level rule
  ordering with import, namespace, layer, and style rules and allowing page
  declaration mutations through the bridged `CSSStyleDeclaration` to rebuild the
  enclosing rule's serialized `cssText`. Focused coverage now passes via
`dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CssPageRule|FullyQualifiedName~CssNamespaceRule|FullyQualifiedName~CssLayerRule|FullyQualifiedName~CssSupportsRule|FullyQualifiedName~CssKeyframesRule|FullyQualifiedName~CssImportRule|FullyQualifiedName~CssMediaRule|FullyQualifiedName~CssRule_Style|FullyQualifiedName~ParentRule|FullyQualifiedName~CssRenderingTests"`
and
`dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`.
- **2026-05-04** — HtmlBridge stylesheet CSSOM continuation: added
  `CSSRuleList.item()`, `insertRule()`, and `deleteRule()` support for nested
  `cssRules` collections exposed by grouping rules such as `CSSMediaRule`,
  `CSSKeyframesRule`, `CSSSupportsRule`, and `CSSLayerRule`, while also
  exposing `item()` on the top-level stylesheet `cssRules` object so nested
  rule insertion and removal rebuild parent grouping-rule `cssText` in place.
  Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CssMediaRule_CssRules_Item_InsertRule_And_DeleteRule_Update_Nested_List_And_CssText|FullyQualifiedName~CssKeyframesRule_CssRules_InsertRule_And_DeleteRule_Update_Keyframes_And_CssText|FullyQualifiedName~CssMediaRule|FullyQualifiedName~CssKeyframesRule|FullyQualifiedName~CssSupportsRule|FullyQualifiedName~CssLayerRule|FullyQualifiedName~CssPropertyRule|FullyQualifiedName~CssFontFaceRule|FullyQualifiedName~CssCharsetRule|FullyQualifiedName~CssPageRule|FullyQualifiedName~CssNamespaceRule|FullyQualifiedName~CssImportRule|FullyQualifiedName~CssRule_Style|FullyQualifiedName~ParentRule|FullyQualifiedName~CssRenderingTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~WptCssVariablesTests|FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`.
- **2026-05-05** — HtmlBridge stylesheet CSSOM continuation: tightened
  top-level `CSSStyleSheet.insertRule()` / `deleteRule()` live-list behavior so
  `document.styleSheets[...].cssRules` updates in place, clears stale numeric
  indices after deletions, and respects later owner `<style>` text
  replacements instead of re-inserting stale programmatic rules across
  rebuilds. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~InsertRule_On_StyleSheet|FullyQualifiedName~StyleSheet_InsertRule_Does_Not_Reappear_After_Owner_TextContent_Is_Replaced|FullyQualifiedName~DeleteRule_On_StyleSheet|FullyQualifiedName~CssRenderingTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~WptCssVariablesTests|FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`.
- **2026-05-05** — HtmlBridge stylesheet CSSOM continuation: exposed
  `@counter-style` entries as `CSSCounterStyleRule`-like objects in
  `document.styleSheets[...].cssRules`, including `type === 10`, `name`, core
  descriptor surfaces such as `system`, `symbols`, `suffix`, and `fallback`,
  plus serialized `cssText`, while preserving mixed top-level rule ordering
  with charset, import, property, font-face, and style rules. Focused coverage
  now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CssCounterStyleRule|FullyQualifiedName~CssPropertyRule|FullyQualifiedName~CssFontFaceRule|FullyQualifiedName~CssCharsetRule|FullyQualifiedName~CssPageRule|FullyQualifiedName~CssNamespaceRule|FullyQualifiedName~CssLayerRule|FullyQualifiedName~CssSupportsRule|FullyQualifiedName~CssKeyframesRule|FullyQualifiedName~CssImportRule|FullyQualifiedName~CssMediaRule|FullyQualifiedName~CssRule_Style|FullyQualifiedName~ParentRule|FullyQualifiedName~CssRenderingTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~WptCssVariablesTests|FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`.
- **2026-05-04** — HtmlBridge stylesheet CSSOM continuation: exposed
  `@font-face` entries as `CSSFontFaceRule`-like objects in
  `document.styleSheets[...].cssRules`, including `type === 5`, bridged
  `style`, and serialized `cssText`, while keeping `parentStyleSheet`,
  `parentRule`, and `style.parentRule` wired through the generic stylesheet
  CSSOM surfaces so font-face declaration mutations rebuild the enclosing rule
  text and font-face entries remain in mixed ordering with charset, import,
  page, and style rules. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CssFontFaceRule|FullyQualifiedName~CssCharsetRule|FullyQualifiedName~CssPageRule|FullyQualifiedName~CssNamespaceRule|FullyQualifiedName~CssLayerRule|FullyQualifiedName~CssSupportsRule|FullyQualifiedName~CssKeyframesRule|FullyQualifiedName~CssImportRule|FullyQualifiedName~CssMediaRule|FullyQualifiedName~CssRule_Style|FullyQualifiedName~ParentRule|FullyQualifiedName~CssRenderingTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`.
- **2026-05-04** — HtmlBridge stylesheet CSSOM continuation: exposed
  `@property` entries as `CSSPropertyRule`-like objects in
  `document.styleSheets[...].cssRules`, including `type === 25`, `name`,
  `syntax`, `inherits`, `initialValue`, and serialized `cssText`, while
  preserving mixed top-level rule ordering with charset, import, font-face, and
  style rules. Focused coverage now passes via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CssPropertyRule|FullyQualifiedName~CssFontFaceRule|FullyQualifiedName~CssCharsetRule|FullyQualifiedName~CssPageRule|FullyQualifiedName~CssNamespaceRule|FullyQualifiedName~CssLayerRule|FullyQualifiedName~CssSupportsRule|FullyQualifiedName~CssKeyframesRule|FullyQualifiedName~CssImportRule|FullyQualifiedName~CssMediaRule|FullyQualifiedName~CssRule_Style|FullyQualifiedName~ParentRule|FullyQualifiedName~CssRenderingTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~WptCssVariablesTests|FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`.
- **2026-05-04** — HtmlBridge stylesheet CSSOM continuation: exposed
  `@charset` entries as `CSSCharsetRule`-like objects in
  `document.styleSheets[...].cssRules`, including `type === 2`, `encoding`, and
  `cssText`, while preserving mixed top-level rule ordering with charset,
  import, page, namespace, layer, and style rules. Focused coverage now passes
  via
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~CssCharsetRule|FullyQualifiedName~CssPageRule|FullyQualifiedName~CssNamespaceRule|FullyQualifiedName~CssLayerRule|FullyQualifiedName~CssSupportsRule|FullyQualifiedName~CssKeyframesRule|FullyQualifiedName~CssImportRule|FullyQualifiedName~CssMediaRule|FullyQualifiedName~CssRule_Style|FullyQualifiedName~ParentRule|FullyQualifiedName~CssRenderingTests"`
  and
  `dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --filter "FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~CssRenderingTests|FullyQualifiedName~RenderingPipelineTests"`.
- **2026-04-30** — Baseline verification before roadmap changes:
  `dotnet build Broiler.slnx` succeeded, while `dotnet test Broiler.slnx`
  surfaced pre-existing failures in `src/Broiler.LogAnalyzer.Tests/` and
  `src/Broiler.Cli.Tests/`. Follow-up roadmap work should therefore use
  targeted validation for touched areas until the broader baseline is cleaned up.

### 14.2 Key decisions

- **Decision:** Keep `#1064` as the umbrella issue and file execution work as
  workstream-scoped sub-issues rather than creating one issue per paragraph in
  this document.
  **Why:** The roadmap is already organized around workstreams and milestone
  gates, so this preserves traceability without fragmenting the backlog.
- **Decision:** Treat existing area-specific roadmap documents as the detailed
  task source of truth, and use this document only for cross-engine sequencing,
  gating, and prioritization.
  **Why:** It avoids duplicating lower-level TODO lists that already exist in
  `ECMASCRIPT_ROADMAP.md`, the Skia roadmap, WPT triage, Acid3 tracking, and
  related docs.
- **Decision:** Baseline infrastructure work (W1/W6) must be filed and started
  before the M2 compliance pushes are treated as milestone work.
  **Why:** The roadmap's stated exit criteria require published pass-rate and
  benchmark signals before later milestones can be objectively graded.

---

## 15. Related Roadmaps

This roadmap composes and sequences the following existing documents. They
remain the authoritative source for area-level task lists; this roadmap owns
their **inter-engine ordering and gating**.

- [`docs/roadmap/acid3-compliance.md`](./acid3-compliance.md) — Acid3 visual
  parity work in `Broiler.HTML`.
- [`docs/roadmap/acid-test-triage.md`](./acid-test-triage.md) — broader
  acid-test triage queue.
- [`docs/roadmap/wpt-failure-triage.md`](./wpt-failure-triage.md) — Web
  Platform Tests triage.
- [`docs/roadmap/google-search-compliance.md`](./google-search-compliance.md)
  — real-page parity smoke check.
- [`docs/roadmap/skia-replacement-roadmap.md`](./skia-replacement-roadmap.md)
  — graphics backend cutover (W7).
- [`docs/roadmap/javascript-engine-assembly-refactor.md`](./javascript-engine-assembly-refactor.md)
  — JS engine assembly/footprint refactor (W6 / M4).
- [`docs/roadmap/log-analyzer-enhancements.md`](./log-analyzer-enhancements.md)
  — diagnostic tooling supporting the conformance dashboard (W1).
- [`docs/roadmap/broiler-pdf-native-parser.md`](./broiler-pdf-native-parser.md)
  — adjacent (out of scope here, listed for completeness).
- [`Broiler.JavaScript/ECMASCRIPT_ROADMAP.md`](../../Broiler.JavaScript/ECMASCRIPT_ROADMAP.md)
  — language-level ES2025/ES2026 plan (W3).
- [`docs/architecture/graphics-backend-cutover.md`](../architecture/graphics-backend-cutover.md)
  — architectural pattern referenced by W2 (seam-driven migration).

---

## 16. Open Questions

1. Which **Test262** subset (categories, feature flags) constitutes the
   per-PR gate? Full nightly is settled; the PR subset is not.
2. Which **WPT** suites form the per-PR slice for HTML, DOM, and Fetch? The
   slice must be small enough to run on PR but representative enough that a
   green slice predicts a green nightly within an agreed margin.
3. What is the **reference hardware profile** for performance budgets — a
   self-hosted runner, a GitHub-hosted runner with normalization, or both?
4. Does the `Broiler.HtmlBridge` Fetch implementation share code with the
   existing `Broiler.JavaScript.Network` project, or does it own its own
   implementation? This decision shapes Phase 3.
5. How are **ES2026** stage-3 proposals exposed during Phase 3 — a single
   master flag, per-proposal flags, or build-time only?
6. What is the policy for **breaking bridge surface changes** required by W2?
   Versioned `IScriptEngine` interface vs. additive-only evolution.
7. Where does the unified conformance/perf dashboard live — in-repo static
   site (extending `Broiler.DevSite`) or an external service?

---

*Changelog*

- **2026-04-30** — Initial draft created in response to the cross-engine
  roadmap tracking issue.
- **2026-04-30** — Added the execution backlog, milestone filing order, and
  implementation-notes log for issue `#1064`.
