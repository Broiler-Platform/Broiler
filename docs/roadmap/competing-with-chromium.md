# Roadmap: Competing with Chromium

> **Status**: Proposed — top-level competitive strategy for the Broiler stack
> **Scope**: Strategy and multi-horizon plan across every Broiler engine and the
> shell. This document sits *above* the execution roadmaps and points into them;
> it does not replace them.
> **Relationship**: The
> [Chromium-alignment unified roadmap](./chromium-alignment-unified-roadmap.md)
> is the *conformance* execution track (does Broiler behave like Chromium N?).
> This document is the *competitive* track (would anyone choose Broiler over
> Chromium, and on what ground?).
> **Owner**: TBD
> **Last updated**: 2026-06-27

---

## Table of Contents

1. [The honest problem statement](#1-the-honest-problem-statement)
2. [What "compete" means here (and what it does not)](#2-what-compete-means-here-and-what-it-does-not)
3. [The wedge: where managed code can actually win](#3-the-wedge-where-managed-code-can-actually-win)
4. [Current gap, quantified](#4-current-gap-quantified)
5. [Subsystem inventory: what a browser needs that Broiler lacks](#5-subsystem-inventory-what-a-browser-needs-that-broiler-lacks)
6. [Competitive pillars (workstreams)](#6-competitive-pillars-workstreams)
7. [Horizon plan](#7-horizon-plan)
8. [KPIs and how CI measures them](#8-kpis-and-how-ci-measures-them)
9. [Anti-goals and explicit non-competition](#9-anti-goals-and-explicit-non-competition)
10. [Risks and why this could fail](#10-risks-and-why-this-could-fail)
11. [Relationship to existing roadmaps](#11-relationship-to-existing-roadmaps)
12. [Open decisions](#12-open-decisions)

---

## 1. The honest problem statement

Chromium is the product of ~15 years and thousands of engineer-years, ships every
~4 weeks, and defines the de-facto web platform. It has a multi-tier optimizing
JS engine (V8: Ignition + Sparkplug + Maglev + TurboFan), a multi-process
site-isolated security model, a GPU-accelerated compositor, its own network
stack (HTTP/2, HTTP/3, QUIC), a media pipeline, an accessibility tree, and an
extension ecosystem.

Broiler is an experimental **managed-.NET** browser stack, Windows-only today,
with a JavaScript engine that compiles to IL via dynamic methods (not a
multi-tier JIT), a single-process model, and a Direct2D raster path that is still
replacing its Skia lineage.

**A head-on race — "beat Chrome on its own benchmarks across the whole web" — is
not winnable and should not be the plan.** Any roadmap that promises that is
fiction. The only credible competitive strategy is **differentiation plus a
beachhead**: win decisively in a niche where Broiler's architecture is an
*advantage*, reach "good enough for the modern web" on correctness, and expand
outward from a defensible position. This document plans that, not a fantasy.

---

## 2. What "compete" means here (and what it does not)

We define three concrete, escalating definitions of "competing." Each is a
horizon in [Section 7](#7-horizon-plan).

| Level | "Compete" means | Who chooses Broiler, and why |
|---|---|---|
| **L1 — Embeddable engine** | A .NET app can embed Broiler to render and script real-world HTML/CSS/JS *in-process* with no native Chromium dependency (no CEF, no WebView2). | .NET teams who today ship a 150+ MB CEF/WebView2 runtime and cross a native interop boundary. |
| **L2 — Trusted-content browser** | Broiler is a credible browser for *controlled* content — kiosks, internal line-of-business apps, documentation/report viewers, e-readers, PDF/HTML export — where the content set is known and security surface is bounded. | Vendors who want a small, auditable, memory-safe, single-language stack they fully control. |
| **L3 — General-purpose contender** | Broiler renders the long tail of the open web correctly and fast enough that a technical user could use it as a daily secondary browser. | Users and embedders who value a fully managed, inspectable, .NET-native alternative. |

"Compete" does **not** mean: matching Chromium's feature count, passing 100% of
WPT, beating V8 on peak throughput, or shipping on day one to macOS/Linux/Android.
Those are either out of scope or far-horizon (see
[Section 9](#9-anti-goals-and-explicit-non-competition)).

---

## 3. The wedge: where managed code can actually win

Competing means leading with the axes where being a managed-.NET, single-language
stack is a *feature*, not a liability:

1. **In-process .NET embeddability.** Chromium-in-.NET means CEF or WebView2: a
   large native runtime, a process boundary, and a marshalling layer. Broiler can
   be a NuGet reference that renders and scripts in the host's own AppDomain, with
   first-class CLR ↔ JS interop (already present in `Broiler.JavaScript.Clr`).
   *This is the single strongest wedge and the L1 beachhead.*
2. **Memory safety by construction.** The browser attack surface is dominated by
   native memory-safety bugs. A managed engine removes that entire bug class for
   the engine itself (the host must still bound CLR/host capability — Broiler.JS is
   explicitly *not* a sandbox yet; see [Section 5](#5-subsystem-inventory-what-a-browser-needs-that-broiler-lacks)).
3. **Auditability and determinism.** A single-language, readable codebase that a
   team can fully review is a real procurement advantage for regulated/embedded
   buyers — and it aligns with Broiler's existing per-component human-review gates.
4. **Deep .NET developer experience.** Debug across the JS ↔ C# boundary, ship one
   toolchain, one package manager, one profiler. No second-language native build.
5. **Footprint control.** A managed engine without a multi-process compositor is a
   smaller, simpler artifact for embedded/kiosk targets that don't need Chromium's
   full machinery.

Everything in the horizon plan is sequenced to make these wedges real and
measurable before chasing breadth.

---

## 4. Current gap, quantified

Honest baselines, using signals Broiler already produces in CI. These are the
numbers we move.

| Axis | Chromium | Broiler today | Source |
|---|---|---|---|
| **JS throughput (Octane 2.0, overall geomean)** | ~79,900 | **197**, completing 8 of 15 suites (rest crash/hang/throw) | [`tests/octane/results/comparison.md`](../../tests/octane/results/comparison.md), produced by the [Octane workflow](../../.github/workflows/octane-benchmarks.yml) |
| **JS robustness** | Runs all of Octane | 7 suites abort (process crash, hang, or engine error — e.g. `InvalidProgramException`, IL-codegen `KeyNotFoundException`) | same |
| **Layout/CSS correctness (Acid3)** | 100/100 | 100/100 under the capture harness | [alignment roadmap §4](./chromium-alignment-unified-roadmap.md#4-current-state-snapshot) |
| **Web-platform correctness (WPT)** | reference | Several hundred failing tests under targeted triage | [`wpt-failure-triage.md`](./wpt-failure-triage.md), [`wpt-triage-and-diagnostics.md`](./wpt-triage-and-diagnostics.md) |
| **Platform reach** | Win/macOS/Linux/Android/iOS | Windows-only (Direct2D/Win32) | [public-preview roadmap §1](./public-preview-roadmap.md) |
| **Process/security model** | Multi-process, site isolation, sandbox | Single process, no sandbox | [Section 5](#5-subsystem-inventory-what-a-browser-needs-that-broiler-lacks) |
| **Speedometer (responsiveness)** | reference | not yet measured | gap — see KPI work in [Section 8](#8-kpis-and-how-ci-measures-them) |

The JS gap is the headline: **~400×** on Octane, and — more importantly than the
ratio — Broiler does not yet *finish* half the suite. **Robustness before
throughput**: an engine that crashes on real code can't be optimized into a
competitor. The first JS goal is "completes Octane and the WPT JS subset without
aborting," not a score target.

---

## 5. Subsystem inventory: what a browser needs that Broiler lacks

A browser is not one program; it is ~a dozen subsystems. This is the gap map.
"Have" = exists in some form; "Partial" = exists but immature; "Gap" = essentially
absent. Each row points at where the work lives or would live.

| Subsystem | Chromium | Broiler | Status | Notes / where it lives |
|---|---|---|---|---|
| HTML parsing + DOM | Blink | `Broiler.DOM` | Have | [DOM roadmap](./broiler-dom-component.md) |
| CSS parse/cascade/computed | Blink Style | `Broiler.CSS` | Have | [CSS roadmap](./broiler-css-component.md), [next steps](./broiler-css-next-steps.md) |
| Layout (box model, fl/grid) | Blink Layout (LayoutNG) | `Broiler.Layout` | Partial | [Layout roadmap](./broiler-layout-component.md) |
| Raster / paint | Skia | `Broiler.Graphics` (Direct2D) | Partial | [skia-replacement](./skia-replacement-roadmap.md) |
| **GPU compositor / tiling** | cc + GPU | — | **Gap** | No layer tree / accelerated compositing; needed for smooth scroll/animation at scale |
| JS execution | V8 (4 tiers) | `Broiler.JS` (IL via dynamic methods) | Partial | Compiles to IL but no speculative/optimizing tiers; [JS asm refactor](./javascript-engine-assembly-refactor.md) |
| JS robustness | V8 | `Broiler.JS` | Partial | Crashes/hangs on parts of Octane — first thing to fix |
| Web APIs / bindings | Blink bindings | `Broiler.HtmlBridge` | Partial | events, CSSOM, microtasks; [bridge boundaries](../architecture/htmlbridge-engine-boundaries.md) |
| **Networking stack** | own (HTTP/2/3, QUIC, cache) | host `HttpClient` | **Gap** | No browser cache, HTTP/3, connection pooling tuned for web; no service worker |
| **Process model / sandbox** | multi-process, site isolation, OS sandbox | single process | **Gap** | The core security story for untrusted content |
| **Accessibility tree** | full AX | — | **Gap** | Required for real-world/legal use; UIA bridge on Windows |
| Media (audio/video/WebRTC) | full pipeline | — | **Gap** | `<video>`/`<audio>` decode, MSE, WebRTC |
| Images/fonts | full codecs + shaping | `Broiler.Graphics` codecs + text | Partial | [media component](./broiler-media-component.md) |
| Input / events / IME | full | `Broiler.Input` | Partial | [input component](./broiler-input-component.md) |
| Storage (cookies, IDB, CacheStorage) | full | partial | Partial | localStorage exists in JS storage; IDB/cache are gaps |
| **Extensions** | WebExtensions | — | **Gap (likely non-goal)** | Almost certainly out of scope; see [Section 9](#9-anti-goals-and-explicit-non-competition) |
| Devtools / inspection | full DevTools + CDP | `Broiler.DevConsole`/`DevSite` | Partial | A CDP-subset is a differentiator for embedders |
| Updater / multi-platform shell | full | Windows Direct2D shell | Partial | [UI component](./broiler-ui-component.md), [public preview](./public-preview-roadmap.md) |

The four **Gap** rows that gate *credibility* (not just breadth) are: **JS
robustness+performance**, the **GPU compositor**, the **process/sandbox security
model**, and the **accessibility tree**. The horizon plan front-loads JS
robustness and security because they block L1/L2; the compositor and AX are L2/L3.

---

## 6. Competitive pillars (workstreams)

Five pillars. Each has a single owning question, a primary CI signal, and a
pointer to the execution roadmap that carries the detailed work.

| # | Pillar | Owning question | Primary signal | Executes via |
|---|---|---|---|---|
| **P1 — Correctness** | Does Broiler render/script the web like Chromium N? | WPT delta, Acid2/3 | [alignment roadmap](./chromium-alignment-unified-roadmap.md) (W3/W4/W5, G1–G4) |
| **P2 — Performance** | Is it fast enough that no one notices the engine? | Octane, Speedometer, layout/raster frame budgets | this doc §8 + [engines/standards/perf](./engines-standards-and-performance-roadmap.md) |
| **P3 — Security & process model** | Can it safely run *untrusted* content? | sandbox escape surface, fuzzing, site isolation | new track (see H2) |
| **P4 — Platform & breadth** | Where can it run, and how much of the platform exists? | subsystem inventory (§5), OS targets | component roadmaps + [public preview](./public-preview-roadmap.md) |
| **P5 — Embeddability & DX** | Is it the obvious choice for a .NET app needing a web view? | API stability, footprint, interop ergonomics | new track (see H1) |

P1 is *not re-planned here* — it is already owned by the alignment roadmap. This
document's net-new tracks are **P3** (security/process model) and **P5**
(embeddability), plus the **P2 performance KPIs** below, because those are the
competitive axes the existing roadmaps under-serve.

---

## 7. Horizon plan

Three horizons matching the three "compete" levels. Each horizon has a single
**thesis**, a small set of **must-land** items, and a hard **exit gate**. A
horizon is not "done" until its gate is green in CI on `main`.

### Horizon 1 — Embeddable engine (L1) · *make the wedge real*

**Thesis:** A .NET developer can `dotnet add package Broiler`, render and script
real-world HTML/CSS/JS in-process, and not hit a crash on ordinary content.

Must-land:
- **JS robustness:** Broiler completes 100% of Octane suites without aborting
  (crash/hang). Fix the three failure classes already surfaced (IL-codegen
  `KeyNotFoundException` on `eval`-heavy code, `InvalidProgramException`, and the
  null-ref that escapes JS `try/catch`). *Score is secondary; finishing is the gate.*
- **Stable embedding API:** a documented, semver'd `Broiler.HtmlBridge` host API
  (load, script, CLR interop, lifecycle) with the boundary frozen per
  [engine boundaries](../architecture/htmlbridge-engine-boundaries.md).
- **WPT JS subset** (the alignment roadmap's W3/G1 set) runs without process
  aborts; net-positive pass rate.
- **Footprint + cold-start** published for the embedded engine vs. an equivalent
  WebView2/CEF baseline.

**Exit gate H1:** Octane completes 15/15 suites in CI; embedding API documented +
version-frozen; a sample .NET app renders a fixed corpus of 20 real pages without
crashing.

### Horizon 2 — Trusted-content browser (L2) · *bound the security surface*

**Thesis:** Broiler is a defensible choice for *known* content sets (kiosk, LOB,
docs/report/e-reader, HTML→PDF) where a small, auditable, memory-safe stack wins.

Must-land:
- **Security model v1 (P3):** a capability-bounded host (Broiler.JS stops being
  "not a sandbox") — explicit allow-listing of CLR/host surface, plus a documented
  threat model for "trusted content, hostile network." Continuous fuzzing of the
  parser/JS/codecs boundaries (extend the existing fuzz tracks).
- **Accessibility tree v1 (P4):** a UIA bridge on Windows good enough for basic
  screen-reader navigation — a hard requirement for many procurement buyers.
- **Performance to "imperceptible" on bounded content (P2):** Speedometer in CI;
  layout + raster frame budgets enforced; smooth scroll on the preview shell.
- **Networking v1:** a browser-grade cache + HTTP/2; service-worker decision made.
- **Ship the public preview** ([public-preview roadmap](./public-preview-roadmap.md))
  as the L2 reference application.

**Exit gate H2:** documented threat model + capability sandbox shipped; UIA
navigation demonstrated; Speedometer tracked with a per-release budget; preview
app released.

### Horizon 3 — General-purpose contender (L3) · *close the long tail*

**Thesis:** A technical user can use Broiler as a daily *secondary* browser on at
least one platform; the open web's long tail mostly renders correctly and fast.

Must-land (each is large; sequence by the alignment roadmap's per-release deltas):
- **GPU compositor / accelerated compositing** for animation- and scroll-heavy
  pages (the biggest remaining performance gap after JS).
- **JS optimizing tier:** a speculative/optimizing path above the current IL
  compile, targeting within a small multiple of V8 on Octane/Speedometer rather
  than ~400×.
- **Media pipeline** (`<video>`/`<audio>`, MSE) and the remaining Web API breadth.
- **Second OS target** via a new Broiler.Graphics backend (not a third-party
  toolkit — per the standing UI constraint).
- **Process model v2:** site isolation for genuinely untrusted browsing.

**Exit gate H3:** WPT delta vs. Chromium N within the alignment roadmap's budget
on the agreed subsets; Octane within a single-digit multiple of Chromium;
Speedometer within budget; a daily-driver dogfood report from ≥1 maintainer.

---

## 8. KPIs and how CI measures them

Competition is only real if it is measured every release. Each KPI maps to an
existing or to-be-added CI signal. **"No number" is itself a failure** (mirrors
the alignment roadmap's G6).

| KPI | Metric | Signal / workflow | State |
|---|---|---|---|
| JS robustness | # Octane suites completing without abort (target 15/15) | [`octane-benchmarks.yml`](../../.github/workflows/octane-benchmarks.yml) → [`comparison.md`](../../tests/octane/results/comparison.md) | **live** (8/15) |
| JS throughput | Octane overall geomean; ratio vs. Chromium | same | **live** (~400× gap) |
| Responsiveness | Speedometer 2/3 score | new workflow (model on the Octane harness) | **to add** |
| Correctness | WPT pass-rate delta vs. Chromium N | [`wpt-tests.yml`](../../.github/workflows/wpt-tests.yml) | live |
| Visual fidelity | Acid2/Acid3 + WPT-visual pixel diff | WPT + acid harnesses | live |
| Footprint | engine package size; cold-start ms | new bench (H1) | **to add** |
| Frame budget | layout + raster ms on a fixed page set | new bench (H2) | **to add** |
| Security surface | fuzzer-hours, crashes, sandbox-escape findings | extend fuzz tracks (H2) | **to add** |

Near-term concrete action: add a **Speedometer** workflow alongside the Octane
one (same isolate-and-compare shape — Chromium via Playwright, Broiler via the
shell) so responsiveness becomes a tracked number, not a vibe. The Octane harness
added in this repo is the template.

---

## 9. Anti-goals and explicit non-competition

Saying no is how a small project competes at all. Broiler will **not** pursue:

- **Feature-count parity with Chromium.** We chase the content that real target
  users hit, measured by WPT subsets, not a feature checklist.
- **The WebExtensions ecosystem.** Re-implementing Chrome's extension API is a
  multi-year sink with little payoff for the L1/L2 buyer. Reconsider only if a
  concrete embedder demands it.
- **Beating V8 on peak throughput.** The target is "fast enough to be
  imperceptible on real workloads," i.e. a small multiple of V8, not a win.
- **Day-one cross-platform.** Windows/Direct2D first; additional OS targets come
  via Broiler-owned backends at H3, never via a third-party UI toolkit (standing
  constraint from the [public-preview roadmap](./public-preview-roadmap.md)).
- **Origin-trial / not-yet-shipped web features.** Same rule as the alignment
  roadmap: count a feature only once Chromium ships it enabled-by-default.

---

## 10. Risks and why this could fail

| Risk | Why it bites | Mitigation |
|---|---|---|
| **The JS performance gap is structural** | A managed IL path may have a floor far above V8's tiered JIT; ~400× may not close to single-digit | Treat H1/H2 as winnable on *robustness + embeddability*, where perf is "good enough"; make L3's optimizing tier a research bet, not a promise |
| **Security is the whole game for untrusted content** | Without a sandbox, L3 (open web) is unreachable; managed safety covers the engine, not host capability | Scope L1/L2 to *trusted* content explicitly; gate L3 on a real process/sandbox model |
| **Breadth treadmill** | Chromium ships every 4 weeks; chasing all of it exhausts a small team | The alignment roadmap's fixed WPT *subsets* + this doc's beachhead scoping bound the surface deliberately |
| **No beachhead demand** | If no .NET embedder actually wants this, L1 has no customer | Validate L1 with real embedder interviews *before* investing in H2/H3 |
| **Spreading across subsystems** | A dozen half-built subsystems beat by one polished one | Horizon gates forbid starting H2 breadth before H1 robustness is green |

The most likely *good* outcome is **L1 + L2**: a genuinely useful embeddable,
auditable, memory-safe .NET web engine for trusted content. L3 (daily-driver open
web) is a stretch goal that depends on the compositor and an optimizing JS tier
landing. Planning honestly means naming L1/L2 as the win and L3 as the moonshot.

---

## 11. Relationship to existing roadmaps

This document is the **strategy layer**. It does not supersede or duplicate the
execution roadmaps; it sequences and prioritizes them against a competitive goal.

| Existing roadmap | Role under this strategy |
|---|---|
| [Chromium-alignment unified roadmap](./chromium-alignment-unified-roadmap.md) | The **P1 Correctness** engine — per-release conformance deltas. Owns G1–G6. |
| [Public-preview roadmap](./public-preview-roadmap.md) | The **L2 reference application** and the Windows shell. |
| [Engines / standards / performance](./engines-standards-and-performance-roadmap.md) | Source detail for **P2 Performance** work. |
| [DOM](./broiler-dom-component.md) / [CSS](./broiler-css-component.md) / [Layout](./broiler-layout-component.md) / [Media](./broiler-media-component.md) / [Input](./broiler-input-component.md) / [UI](./broiler-ui-component.md) | **P4 Platform & breadth** — the subsystem build-out in §5. |
| [Skia replacement](./skia-replacement-roadmap.md) | Raster foundation under **P2** and the future compositor. |
| [JS engine assembly refactor](./javascript-engine-assembly-refactor.md) | Structural prerequisite for **JS robustness + optimizing tier** (H1/H3). |

Net-new tracks this document introduces (no existing home): **P3 security/process
model**, **P5 embeddability/DX**, and the **Speedometer + footprint + frame-budget
KPIs** in §8.

---

## 12. Open decisions

| # | Question | Why it matters | Proposed default |
|---|---|---|---|
| 1 | Is the primary beachhead **embedding in .NET apps** (L1) or the **standalone preview browser** (L2)? | Determines whether P5 (embeddability) or P4 (shell breadth) leads | **L1 embedding** — it is the strongest, most defensible wedge; the preview is the L2 proof, not the L1 goal |
| 2 | Do we commit to a **GPU compositor** (large) or push raster further first? | Gates H3 performance and animation fidelity | Defer to H3; quantify the raster ceiling on real pages first |
| 3 | Is an **optimizing JS tier** in scope, or do we accept "fast enough"? | Sets whether the 400× gap is a target or accepted | Accept "good enough" through L2; treat the optimizing tier as an H3 research bet |
| 4 | **Service workers / offline** — in or out? | Large surface; matters for some embedders | Out until a concrete embedder requires it |
| 5 | Which **second OS** at H3, and via which Broiler.Graphics backend? | Platform reach vs. effort | Decide at H2 exit, based on embedder demand |

Open questions raised while operating this roadmap are filed as issues labelled
`competitive-strategy` and resolved at the alignment roadmap's annual review
(it is the natural forcing function), at which point they move into this table.
