# Roadmap: Unified Browser Compliance for Broiler Engines

> **Status**: Active — the only supported roadmap of record for `Broiler.JavaScript`, `Broiler.HTML`, and `Broiler.HtmlBridge`  
> **Scope**: Keep Broiler aligned with the latest stable **major Chromium** and **major Firefox** releases through a single cross-engine plan  
> **Current reference browsers (2026-05)**: Chromium **148** and Firefox **150**

---

## 1. Decision

This document replaces the previous engine/browser-compliance roadmaps with one
unified plan. Going forward, **all Broiler engine compliance work must track
against this roadmap**.

The deprecated roadmaps are retained only as historical context:

- [`engines-standards-and-performance-roadmap.md`](./engines-standards-and-performance-roadmap.md)
- [`acid3-compliance.md`](./acid3-compliance.md)
- [`acid-test-triage.md`](./acid-test-triage.md)
- [`wpt-failure-triage.md`](./wpt-failure-triage.md)
- [`google-search-compliance.md`](./google-search-compliance.md)
- [`javascript-engine-assembly-refactor.md`](./javascript-engine-assembly-refactor.md)
- [`skia-replacement-roadmap.md`](./skia-replacement-roadmap.md)
- [`../../Broiler.JavaScript/ECMASCRIPT_ROADMAP.md`](../../Broiler.JavaScript/ECMASCRIPT_ROADMAP.md)

Supporting operational documents remain active:

- [`engines-m0-baseline.md`](./engines-m0-baseline.md)
- [`../architecture/htmlbridge-engine-boundaries.md`](../architecture/htmlbridge-engine-boundaries.md)
- [`../architecture/htmlbridge-spec-map.md`](../architecture/htmlbridge-spec-map.md)

---

## 2. What we learned from the deprecated plans

| Source | Key points preserved here | Known gap that this roadmap closes |
|---|---|---|
| `engines-standards-and-performance-roadmap.md` | Cross-engine sequencing, milestones, dashboard gating | It still depended on multiple active sub-roadmaps instead of one plan of record |
| `ECMASCRIPT_ROADMAP.md` | JS language-version tracking and Test262 discipline | It tracked ECMAScript only, not browser-facing host/API parity |
| `wpt-failure-triage.md` | WPT bucketization and targeted failure reduction | It did not tie WPT work to Chromium/Firefox release targets |
| `acid3-compliance.md` + `acid-test-triage.md` | Visual parity and acid regression workflows | Acid coverage is useful but too narrow to define browser compliance by itself |
| `google-search-compliance.md` | Real-page smoke validation and JS/CSS/rendering gap notes | A single-site plan is not broad enough for release-to-release browser alignment |
| `javascript-engine-assembly-refactor.md` | JS modularity and ownership boundaries | Architecture work was not tied directly to browser release readiness |
| `skia-replacement-roadmap.md` | Renderer backend migration risks and validation needs | Backend migration needs to serve browser parity goals, not run as a separate roadmap |

### Current cross-engine gaps

1. **No single release target** for the latest Chromium and Firefox majors.
2. **No unified compatibility scoreboard** that combines JS, DOM/HTML, CSS,
   rendering, and real-page smoke checks.
3. **Too many area-specific trackers** marked active at the same time.
4. **Insufficient Firefox-specific parity review** relative to Chromium-heavy
   tooling and history.
5. **Architecture and backend work** is not consistently prioritized by impact
   on browser compatibility outcomes.

---

## 3. Goal

Broiler must maintain a rolling compliance program that keeps the three engines
usable against the **latest stable major Chromium and Firefox releases**:

- **`Broiler.JavaScript`** — ECMAScript language behavior and browser-facing JS
  host expectations
- **`Broiler.HtmlBridge`** — DOM, events, fetch/resource loading, timers,
  structured clone, and browser API glue
- **`Broiler.HTML`** — HTML parsing, DOM/render tree behavior, CSS/layout, paint,
  and pixel fidelity

Success means Broiler can detect browser-version deltas quickly, prioritize the
highest-impact gaps, and ship compatibility updates on a repeatable cadence.

---

## 4. Compatibility policy

### 4.1 Release targets

- Track the latest **stable major Chromium** and **stable major Firefox**
  releases as the primary compatibility targets.
- Refresh the recorded target versions at least **once per month** and after any
  major browser release that affects Broiler’s failing signals.
- Maintain at most **one major-version lag** as a temporary exception; any
  larger gap is a roadmap regression that must be called out explicitly.

### 4.2 Sources of truth

- **Language conformance:** Test262 subsets and engine unit tests
- **Platform conformance:** WPT targeted slices and Broiler CLI/browser tests
- **Visual fidelity:** acid tests plus representative page captures
- **Real-page smoke:** Google Search plus a maintained small-site matrix
- **Operational baselines:** [`engines-m0-baseline.md`](./engines-m0-baseline.md)

### 4.3 Supported comparison browsers

- Chromium is the primary render/script comparison source for existing acid and
  Playwright-based flows.
- Firefox must be added anywhere a workflow currently uses Chromium alone when
  the result influences roadmap priority or release-readiness decisions.

---

## 5. Unified workstreams

### W1 — Browser release intake

- Record the current stable major Chromium and Firefox versions.
- Capture notable release changes that affect JS, DOM, CSS, graphics, or API
  compatibility.
- Re-run targeted comparison suites when the reference major changes.

### W2 — Shared compatibility dashboard

- Publish one dashboard view that reports:
  - JS/Test262 status
  - targeted WPT status
  - acid visual status
  - real-page smoke status
  - top Chromium-vs-Firefox-vs-Broiler deltas
- Keep historical notes in supporting docs, but use this roadmap as the only
  prioritization source.

### W3 — JavaScript engine compliance

- Continue ECMAScript version work, but prioritize features that block current
  Chromium/Firefox behavior first.
- Keep Test262 subsets aligned with the latest major-browser-exposed language
  surface.
- Track browser-relevant built-ins and APIs that real sites expect, including
  encoding, URL, abort, performance, observer, and structured-clone-adjacent
  surfaces where Broiler owns the behavior.

### W4 — DOM, HTML, and bridge compliance

- Prioritize the bridge and DOM features that unblock targeted WPT and real-page
  behavior.
- Keep event dispatch, timers, parser/script coordination, fetch/resource
  loading, iframe/subframe behavior, and shadow-DOM-adjacent work aligned with
  current browser behavior.
- Treat Firefox/Chromium divergence review as part of triage, not as an
  afterthought.

### W5 — CSS, layout, and rendering compliance

- Continue the WPT and acid follow-up work, but rank tasks by impact on current
  Chromium/Firefox parity rather than by document ownership.
- Use the graphics backend and rendering internals roadmap items only when they
  measurably improve browser compatibility, stability, or performance.
- Expand representative rendering checks beyond acid-only coverage.

### W6 — Real-page interoperability

- Keep `google-search-compliance.md` history as input only; move active work
  here.
- Maintain a small smoke matrix that includes:
  - search landing page
  - JS-heavy document page
  - CSS/layout stress page
  - forms/input page
- For each page, compare Broiler against both Chromium and Firefox when feasible.

---

## 6. Delivery phases

### Phase A — Consolidate and baseline

- Deprecate prior engine/browser-compliance roadmaps
- Publish this unified roadmap
- Update top-level documentation to point only to this roadmap
- Point active dashboard/baseline docs at this roadmap

### Phase B — Establish release tracking discipline

- Add recorded Chromium/Firefox target majors to the active dashboard/reporting
- Define the smallest maintained compatibility scorecard for every PR and every
  browser-major refresh
- Review missing Firefox comparison coverage and file the follow-up work

### Phase C — Close highest-impact engine gaps

- Land the highest-value JS host/runtime gaps affecting current target browsers
- Land the highest-value DOM/bridge gaps affecting targeted WPT and smoke pages
- Land the highest-value CSS/layout/rendering gaps affecting Chromium/Firefox
  parity

### Phase D — Sustain evergreen compliance

- Repeat the browser-major intake cadence
- Keep the compatibility scorecard current
- Retire obsolete sub-trackers instead of reviving them as independent roadmaps

---

## 7. Exit criteria and operating cadence

### Every PR touching engine behavior should preserve or improve

- targeted Test262 results
- targeted WPT results
- acid regression results
- real-page smoke results
- published baseline/performance signals

### Monthly browser-review cadence

1. Record latest stable Chromium and Firefox majors.
2. Compare them against Broiler’s current dashboard.
3. Re-rank the backlog by user-visible impact.
4. Update this roadmap if priorities change.
5. Do **not** spin up a new standalone roadmap; extend this one instead.

---

## 8. Deprecation rule

If a roadmap concerns browser or engine compliance for `Broiler.JavaScript`,
`Broiler.HTML`, or `Broiler.HtmlBridge`, it must either:

- live inside this roadmap, or
- be clearly marked as a historical/supporting document that defers to this
  roadmap.

No alternative active roadmap should be created for the Broiler engines without
first updating this document.
