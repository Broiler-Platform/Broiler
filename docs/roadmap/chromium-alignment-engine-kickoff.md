# Chromium Alignment Kickoff for `Broiler.JavaScript`, `Broiler.HTML`, and `Broiler.HtmlBridge`

> **Status**: Active — initiation / planning companion to the unified roadmap  
> **Companion to**: [`docs/roadmap/chromium-alignment-unified-roadmap.md`](./chromium-alignment-unified-roadmap.md)  
> **Scope**: Planning-only kickoff for the JavaScript, HTML, and Bridge engines  
> **Out of scope**: Closing implementation gaps in this document; those land as follow-up issues/PRs

---

## 1. Purpose

This document translates the unified Chromium roadmap into the first concrete
planning pass for the three Broiler engines in scope for this issue:

- `Broiler.JavaScript`
- `Broiler.HTML`
- `Broiler.HtmlBridge`

It records the current evidence already present in the repository, identifies
the codebase and documentation surfaces that still need Chromium-alignment work,
and seeds the follow-up issues needed to move from the current milestone-based
signals to per-release Chromium tracking.

---

## 2. Initial findings

### 2.1 Cross-engine summary

| Area | What exists today | Gap against the unified Chromium roadmap |
|---|---|---|
| Shared conformance reporting | Focused per-engine milestone summaries exist under `tests/m2-conformance/` for JS, HTML/CSS, and Bridge. | There is no Chromium-N lockfile, no shared dashboard job, no per-release snapshot template, and no single report that measures all engines against the same Chromium release. |
| JavaScript | `tests/m2-conformance/test262-es2025/test262-subset-summary.md` publishes a green focused Test262 slice. | The current gate is repo-owned and milestone-specific; it is not yet pinned to the Test262 revision V8 shipped in Chromium N, and it does not yet publish a delta vs Chromium. |
| HTML | `tests/m2-conformance/html-css-targeted/html-css-targeted-summary.md` publishes a green targeted HTML/CSS slice; the unified roadmap also records Acid3 100/100 and Acid2 80.00% in the current snapshot. | The current signal is narrower than the Chromium-aligned WPT subset defined by Phase C0.2 and is not yet tied to Chromium-N WPT pass-rate baselines or visual-reference captures. |
| Bridge | `tests/m2-conformance/bridge-targeted/bridge-targeted-summary.md` publishes a green targeted DOM/CSSOM/microtask slice; `docs/architecture/htmlbridge-engine-boundaries.md` freezes the public seam. | The bridge does not yet publish the Chromium-aligned DOM/Web API subset required by W5, a per-API parity table, or the recurring gap issue flow called for by the unified roadmap. |

### 2.2 Shared blockers before engine-specific work can be measured

The unified roadmap's Phase C0 introduces repository-wide prerequisites that do
not yet exist in the tree and therefore block Chromium alignment for all three
engines:

- `scripts/chromium/fetch-reference.ps1` and `scripts/chromium/fetch-reference.sh`
- `tests/m2-conformance/chromium-reference/chromium-N.lock.json`
- `tests/m2-conformance/wpt-subsets/chromium-aligned.toml`
- `tests/m2-conformance/test262-es2025/chromium-aligned.exclude`
- `docs/roadmap/snapshots/_template.md`
- `.github/workflows/chromium-alignment.yml`

These items should be treated as shared dependencies for every engine-specific
follow-up issue below.

---

## 3. Codebase and documentation areas requiring follow-up changes

### 3.1 Shared / cross-engine

| Surface | Why it must change for Chromium alignment |
|---|---|
| `docs/roadmap/chromium-alignment-unified-roadmap.md` | Remains the source of truth, but now needs companion planning/snapshot documents to drive adoption. |
| `tests/m2-conformance/` | Needs Chromium-pinned lockfiles, subset definitions, and per-release snapshot inputs so all engines are measured against the same baseline. |
| `.github/workflows/` | Needs the unified `chromium-alignment.yml` workflow described in Phase C0.6. |
| `docs/roadmap/snapshots/` | Does not exist yet; Phase C0 requires a template and append-only per-release snapshot files. |
| `bench/reference/` | Phase C0.4 requires a fixed reference-runner write-up and baseline files before W6 can gate regressions consistently. |

### 3.2 `Broiler.JavaScript`

| Surface | Why it must change for Chromium alignment |
|---|---|
| `Broiler.JavaScript/ECMASCRIPT_ROADMAP.md` | Still useful as historical gap inventory, but future issue filing must move from ECMA-edition framing to Chromium/V8 release framing. |
| `tests/m2-conformance/test262-es2025-manifest.json` and `tests/m2-conformance/test262-es2025/` | Need to be re-pinned to the Test262 revision V8 shipped with Chromium N and augmented with the roadmap's `chromium-aligned.exclude` contract. |
| `Broiler.JavaScript/` runtime, built-ins, parser, compiler, and engine projects | Will absorb the implementation fixes surfaced once Chromium-N Test262/WPT deltas are measured. |
| `src/Broiler.Engines.Baseline/` | Needs to participate in the shared dashboard/reporting flow so JS deltas are published per Chromium release. |

### 3.3 `Broiler.HTML`

| Surface | Why it must change for Chromium alignment |
|---|---|
| `docs/roadmap/wpt-failure-triage.md`, `docs/roadmap/acid3-compliance.md`, and `acid/acid2/acid2-compliance-roadmap.md` | These docs already capture useful failure detail, but the active planning anchor must shift to the Chromium-aligned WPT/visual subsets and per-release snapshots. |
| `tests/m2-conformance/html-css-targeted/` | The current targeted slice is a good seed, but it must be expanded/rebased onto the fixed W4 directories in Phase C0.2 and compared with Chromium-N pass rates. |
| `Broiler.HTML/Source/Broiler.HTML.Dom/`, `Broiler.HTML/Source/Broiler.HTML.CSS/`, `Broiler.HTML/Source/Broiler.HTML.Orchestration/`, and `Broiler.HTML/Source/Broiler.HTML.Rendering/` | These are the core parser/style/layout/rendering areas likely to carry W4 fixes once the Chromium-aligned subset starts producing deltas. |
| `Broiler.HTML/Source/Broiler.HTML.Image/` and `Broiler.HTML/Source/Broiler.HTML.Image.Compat/` | The unified roadmap still expects Acid2/3 and visual-reference parity work, so the raster/output path remains part of HTML-engine follow-up planning even when fixes span W4/W7 together. |

### 3.4 `Broiler.HtmlBridge`

| Surface | Why it must change for Chromium alignment |
|---|---|
| `docs/architecture/htmlbridge-engine-boundaries.md` and `docs/architecture/htmlbridge-spec-map.md` | These are the right starting point for W2/W5, but they need to stay aligned with Chromium-scoped API parity and recurring gap filing. |
| `tests/m2-conformance/bridge-targeted/` | The current focused bridge slice must grow into the W5 Chromium-aligned WPT subset (`dom/`, `html/webappapis/`, `fetch/`, `content-security-policy/`). |
| `src/Broiler.HtmlBridge/DomBridge.Events.cs`, `DomBridge.Selectors.cs`, `DomBridge.Messaging.cs`, `ContentSecurityPolicy.cs`, `MicroTaskQueue.cs`, `ScriptEngine.cs`, and `InteractiveSession.cs` | These files map directly to the bridge behaviours the unified roadmap calls out for DOM events, CSSOM, Fetch, CSP, messaging, and microtask ordering. |
| `src/Broiler.Cli.Tests/` bridge-oriented suites | Existing DOM/network/web-messaging coverage will need to be reshaped into the Chromium-aligned subset and referenced from the unified dashboard. |

---

## 4. Proposed follow-up issues / subtasks

The repository cannot open GitHub issues directly from this document, so the
table below serves as the filing seed for the next issue-creation pass.

| Proposed issue | Engine | Workstreams | Suggested labels | Depends on | Exit signal |
|---|---|---|---|---|---|
| **Bootstrap Chromium-N reference inputs and lockfiles** | Shared | W1 | `chromium-alignment`, `engine:javascript`, `engine:html`, `engine:bridge`, `workstream:W1` | None | `fetch-reference` scripts, lockfile format, WPT/Test262 subset files, and snapshot template are committed |
| **Align `Broiler.JavaScript` conformance gates to Chromium N** | `Broiler.JavaScript` | W3 (+ W1) | `chromium-alignment`, `engine:javascript`, `workstream:W3` | Chromium-N reference inputs and lockfiles | JS dashboard line reports Chromium-N Test262 delta, new gaps are filed with test IDs and target release, and the focused milestone manifest is no longer the only source of truth |
| **Align `Broiler.HTML` HTML/CSS gates to Chromium N** | `Broiler.HTML` | W4 (+ W1, W7) | `chromium-alignment`, `engine:html`, `workstream:W4` | Chromium-N reference inputs and lockfiles | HTML dashboard line reports Chromium-N WPT delta plus visual-reference status, and the fixed W4 subset is tracked per release |
| **Align `Broiler.HtmlBridge` DOM/Web API gates to Chromium N** | `Broiler.HtmlBridge` | W5 (+ W1, W2) | `chromium-alignment`, `engine:bridge`, `workstream:W5` | Chromium-N reference inputs and lockfiles | Bridge dashboard line reports Chromium-N WPT DOM/Web API delta and links every non-zero gap to a filed issue |

### 4.1 JavaScript subtasks

- Re-pin the JS gate to the Test262 revision V8 shipped in Chromium N.
- Commit the long-lived `chromium-aligned.exclude` file called for by Phase C0.3.
- Decide whether the language-level WPT subset should be published alongside the
  existing Test262 slice or folded directly into the unified dashboard only.
- Triage the first Chromium-N delta set into behaviour gaps vs intentional
  Broiler extensions (`decimal`, CLR interop, mixed modules).

### 4.2 HTML subtasks

- Convert the current targeted HTML/CSS suite list into the fixed W4 directory
  set in Phase C0.2.
- Publish Chromium reference captures for Acid2, Acid3, and the WPT visual
  subset so HTML layout regressions can be compared release-to-release.
- Keep the current failure-triage docs as deep-dive references, but move active
  issue filing to Chromium release snapshots and labels.
- Record whether current Acid2 80.00% parity is acceptable as the initial
  baseline or should immediately become a Chromium-N gap issue.

### 4.3 Bridge subtasks

- Expand the bridge-targeted suites into the fixed W5 directories for events,
  microtasks, fetch, CSP, and messaging.
- Publish a parity table that maps the frozen bridge surface to the
  Chromium-scoped DOM/Web API subset being measured.
- File bridge issues per failing test path instead of only keeping aggregate
  milestone summaries.
- Keep the W2 boundary docs current as bridge internals are hardened to avoid
  leaking engine-specific types while Chromium parity work lands.

---

## 5. High-level incremental adoption plan

| Window | Goal | Output |
|---|---|---|
| **Step 1 — Shared bootstrap (first PRs after this doc)** | Land the Phase C0 inputs that all engines depend on. | Reference fetcher, lockfiles, subset definitions, snapshot template, and dashboard workflow skeleton |
| **Step 2 — First Chromium-N baseline publication** | Re-run the existing JS/HTML/Bridge focused signals against the shared Chromium baseline and publish the first snapshot. | `chromium-N0.lock.json`, `docs/roadmap/snapshots/chromium-N0.md`, and filed gap issues for each non-zero delta |
| **Step 3 — One engine issue per workstream** | Move active implementation work into separate JS/HTML/Bridge issues seeded from Section 4. | Three engine-specific execution issues linked from the first snapshot |
| **Step 4 — Release-by-release steady state** | Follow the Day 0 / Day 1–7 / Day 8–21 / Day 22–28 loop in the unified roadmap. | One append-only snapshot per Chromium release, with explicit deferrals and gap history |

### Proposed timeline

- **Current issue:** planning-only kickoff, inventory, and issue seeding.
- **Next Chromium cycle:** complete the shared C0 bootstrap and publish the
  first Chromium-N0 baseline.
- **Following 1–2 Chromium cycles:** close the highest-severity JS/HTML/Bridge
  gaps that block G1, G2, and G3.
- **Steady state after bootstrap:** refresh lockfiles and snapshots every
  Chromium stable release (~4 weeks), keeping the engine issues aligned to the
  latest published delta.

---

## 6. Immediate next actions

- File the four issue seeds from Section 4.
- Land the shared C0 bootstrap before widening any engine-specific conformance
  slice.
- Treat the existing focused milestone summaries as bootstrap evidence, not the
  final Chromium-aligned gate.
- Publish the first Chromium snapshot from CI output rather than by hand so the
  per-release loop starts in the shape required by the unified roadmap.
