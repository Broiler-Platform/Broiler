# Roadmap to First Public Preview

> **Status**: Proposed — planning document for the first public preview of Broiler
> **Scope**: (A) WPT CI coverage, (B) Broiler.App WPF → Broiler.Graphics migration,
> (C) Layout-component extraction to purify HtmlBridge
> **Owner**: TBD
> **Last updated**: 2026-06-27

---

## Table of Contents

1. [Goal & Definition of "First Public Preview"](#1-goal--definition-of-first-public-preview)
2. [Current State Snapshot](#2-current-state-snapshot)
3. [The Three Workstreams & Why This Order](#3-the-three-workstreams--why-this-order)
4. [Phase Plan](#4-phase-plan)
   - [Phase 0 — CI Safety Net (foundation)](#phase-0--ci-safety-net-foundation)
   - [Phase 1 — WPT Coverage Completeness (Workstream A)](#phase-1--wpt-coverage-completeness-workstream-a)
   - [Phase 2 — Broiler.Graphics UI Migration (Workstream B)](#phase-2--broilergraphics-ui-migration-workstream-b)
   - [Phase 3 — Public Preview Hardening & Release](#phase-3--public-preview-hardening--release)
   - [Track C — Layout Component Extraction (parallel, non-blocking)](#track-c--layout-component-extraction-parallel-non-blocking)
5. [Dependency & Sequencing Diagram](#5-dependency--sequencing-diagram)
6. [Exit Criteria for the Preview](#6-exit-criteria-for-the-preview)
7. [Risks & Mitigations](#7-risks--mitigations)
8. [Open Decisions](#8-open-decisions)

---

## 1. Goal & Definition of "First Public Preview"

The first public preview is a **runnable Windows browser shipped on the
Broiler.Graphics (Direct2D/Win32) UI** — not WPF — that an external user can
download, launch, type a URL, and browse with confidence backed by a visible CI
quality signal.

**A preview is explicitly *not*:** full standards compliance, cross-platform
(macOS/Linux), or 100% WPT pass rate. It *is*: a stable, self-hostable app on the
Broiler-owned graphics stack, with an honest, automated statement of what renders
correctly.

### Firm constraint: no external UI dependency

The preview UI **must be built entirely on Broiler-owned code** — specifically
`Broiler.Graphics` (Win32 + Direct2D) and its `Broiler.HTML.Graphics` host. **No
external UI framework may be introduced**, including:

- **No Avalonia.** The `docs/roadmap/avalonia-ui-support.md` direction is **not**
  the preview path and is out of scope here. Cross-platform, if pursued later,
  must be achieved by extending Broiler.Graphics' own backend set, not by adopting
  a third-party toolkit.
- **No WPF in the shipped path.** WPF (`Broiler.HTML.WPF`, `<UseWPF>`) is the thing
  we are migrating *away from*. It may remain temporarily as a build-able fallback
  during migration but must not be part of the released preview artifact.
- **No other external windowing/UI toolkit** (WinUI, MAUI, Win2D, etc.). The
  windowing, input, and drawing surface all come from Broiler.Graphics.

The only acceptable external dependency under the UI is the OS/native layer that
Broiler.Graphics itself wraps (Win32, Direct2D) — that is Broiler's own backend,
not an external UI framework.

**Other framing assumptions** (see [Open Decisions](#8-open-decisions)):
- Preview target is **Windows-only / Direct2D** for v0 (matches
  `Broiler.Graphics.Windows` and the existing Win32 demo). Cross-platform is
  deferred and, when pursued, stays inside Broiler.Graphics per the constraint above.
- Layout extraction (Workstream C) is **architectural debt reduction, not a
  launch blocker** — see the pro/contra in [Track C](#track-c--layout-component-extraction-parallel-non-blocking).

---

## 2. Current State Snapshot

### A. WPT test coverage in CI
- **Runner**: `src/Broiler.Wpt/WptTestRunner.cs` discovers *all* HTML files under
  the WPT checkout (no hardcoded allowlist) and compares rendered output to
  Chromium-generated reference PNGs (`PixelDiffRunner`).
- **Critical gap**: testharness.js assertion tests are injected as **no-op stubs**
  (`WptTestRunner.cs:195-278`). They execute but assert nothing and pass/fail
  only on pixel comparison. → **`dom/`, `html/`, `js/` and other non-reftest
  segments are not actually validated.** Today only **CSS reftests** are meaningfully tested.
- **CI trigger**: `.github/workflows/wpt-tests.yml` is **`workflow_dispatch` only**
  — no nightly, no PR/push run, not a merge gate.
- **Latest run** (per `docs/roadmap/wpt-failure-triage.md`): 24,920 tests →
  9.1% pass, 8.0% fail, **82.9% skipped** (mostly missing reference images).
- Two suites explicitly deferred (`Program.cs:31-35`): `css/css-view-transitions`,
  `css/filter-effects`.
- **README references `.github/workflows/build.yml` — which does not exist.** There
  is no push/PR build-and-test CI today (only the manual WPT job and the
  `engines-m0-dashboard.yml` job).
- Acid tests: fixtures live in `acid/{acid1,acid2,acid3}`; Acid1 has a unit-test
  path (`Broiler.Cli.Tests --filter Acid1`); Acid2/3 tracked in
  `docs/roadmap/acid2-*`, `acid3-compliance.md`. Not wired into CI as a signal.

### B. Broiler.App (WPF) → Broiler.Graphics
- `src/Broiler.App` is **WPF**: `net10.0-windows`, `<UseWPF>true</UseWPF>`, renders
  via `HtmlPanel` (from `Broiler.HTML.WPF`), animates via `DispatcherTimer`, input
  via WPF routed events (`MainWindow.xaml.cs`).
- **Broiler.Graphics already provides the target stack**: `IBroilerRenderer` /
  `IBroilerSurface` (platform-neutral render-list replay), `BWindow` →
  `Direct2DWindow` (Win32 message loop), `Direct2DRenderer` (Direct2D 1.1),
  plus `BEditControl` / `BButtonControl`.
- **A working PoC exists**: `Broiler.HTML.Graphics.Win32.Demo` —
  `RenderedUrlWindow : Direct2DWindow` takes a URL, calls
  `HtmlContainer.SetHtml()` → `PerformLayout()` → `CreateRenderList()` →
  `Direct2DRenderer.Render()`. **~80% of the host plumbing is already proven.**
- **Gaps to a real browser**: navigation chrome (address bar, back/fwd/refresh,
  favorites), Win32→DOM input-event translation, scrolling, text-input/focus,
  the dev console, and an animation-tick loop on `WM_TIMER` instead of `DispatcherTimer`.

### C. Layout component / purifying HtmlBridge
- HtmlBridge is 5 projects: `Core`, `Dom` (DOM↔JS bridge), `Rendering`,
  `Scripting`, and the `HtmlBridge` facade.
- Layout logic exists in **two** places:
  - `Broiler.HTML/Source/Broiler.HTML.Dom/CssLayoutEngine.cs` — the **real**
    renderer layout engine (this is what the Graphics app paints from).
  - `src/Broiler.HtmlBridge.Rendering/CssBoxModel.cs` (~1000 LOC: block/flex/grid)
    + `CssTextProperties.cs` — a **parallel** box model used only to answer
    `getComputedStyle()` / `offsetLeft/Top/Width/Height` queries from JS.
- The in-progress **CSS component** effort (`broiler-css-component.md`,
  `broiler-css-next-steps.md`) explicitly **excludes layout** — it extracts
  parsing/cascade/computed-styles only. So no other effort owns this.
- **"Purifying" = extract a `Broiler.HtmlBridge.Layout` assembly**: move
  `CssBoxModel` + `CssTextProperties` + element offset-state out of
  `Rendering`/`Dom`, behind `IDomElement` / `ICssPropertyResolver` interfaces,
  leaving HtmlBridge as DOM↔JS bridging + scripting + paint/composite only.

---

## 3. The Three Workstreams & Why This Order

| Order | Workstream | Why here |
|------|-----------|----------|
| **1st** | **A — WPT/CI safety net** | You cannot ship a preview "with confidence" without an automated, honest signal of what works. CI must exist *before* the large UI migration so rendering/JS regressions are caught while B is in flight. The expensive part (testharness support) also unlocks the segments that are currently invisible. |
| **2nd** | **B — Graphics UI migration** | The headline deliverable of the preview. A PoC already proves feasibility, so risk is "productionizing chrome + input," not "does it render." Runs against the A safety net. |
| **Parallel / Deferred** | **C — Layout extraction** | Genuinely independent: the Graphics app paints from the renderer's `CssLayoutEngine`, **not** HtmlBridge's `CssBoxModel`, so C neither blocks nor is blocked by B. It is debt reduction ("eventually"). Recommended to land core slices alongside B but **not a launch gate.** |

**Net critical path to preview: A → B → release.** C floats alongside.

---

## 4. Phase Plan

### Phase 0 — CI Safety Net (foundation)
*Goal: a real, automatic build/test signal exists before the migration starts.*
**Status: implemented — pending first green CI run to validate on hosted runners.**

| # | Task | Status | Delivered | Exit |
|---|------|--------|-----------|------|
| 0.1 | Create `build.yml` | ✅ Done | `.github/workflows/build.yml` — push(main)/PR/manual; `windows-latest`; `submodules: recursive`; .NET 8+10 SDKs; restore → build Release → test → upload TRX. Fixes the dangling README reference. | Green check on every PR |
| 0.2 | Schedule WPT | ✅ Done | `.github/workflows/wpt-tests.yml` — added `on: schedule` (weekly Sun 03:00 UTC) + retained `workflow_dispatch`; per-run summary + `wpt-results.json` artifact. | Scheduled run visible |
| 0.3 | Segment matrix | ✅ Done | Scheduled job is a `matrix` over `segment` (css/CSS2, css-flexbox, css-text, selectors, dom, html/syntax); each segment caches its WPT checkout + references and uploads `wpt-results-<id>`. `fail-fast: false` so one slow/failing segment can't sink the rest. | Per-segment results |
| 0.4 | Baseline gate | ◑ Wired, baselines pending | `scripts/check-wpt-regression.sh` compares `summary.passed/failed` against `tests/wpt-baseline/<id>.json`; **fails only on regression**, warns-and-passes when no baseline exists. Baselines are committed per segment after the first run (see `tests/wpt-baseline/README.md`). | Regression alarms work |

**Cost note (0.2/0.3):** reference-image generation (Chromium/Playwright) is
expensive, so the matrix runs **weekly**, not nightly, and caches both the WPT
checkout and generated references (keyed by `CACHE_EPOCH` + segment — bump
`CACHE_EPOCH` to refresh). Cadence can tighten to nightly once references are
cached and the gated set is small enough to fit the 360-min budget per leg.

**Remaining to fully close Phase 0:** (a) one successful run of each workflow on
hosted runners to confirm the full-solution Windows build and the per-segment WPT
legs are green; (b) commit the first `tests/wpt-baseline/<id>.json` snapshots to
arm the regression gate.

> **Note on submodules**: both workflows check out with `submodules: recursive`
> — without nested submodules the Broiler.JS Regex-alias bridge build breaks
> (see memory `broiler-js-nested-submodules`).

---

### Phase 1 — WPT Coverage Completeness (Workstream A)
*Goal: "the CI WPT suite covers all segments that need testing."*

The phrase "all segments" today is blocked by two things: (1) missing reference
images make 83% skip, and (2) the runner can't score testharness.js tests, so
whole categories silently no-op. Both must be addressed.

| # | Task | Detail | Exit |
|---|------|--------|------|
| 1.1 | **testharness.js result reporting** | Replace the no-op stubs (`WptTestRunner.cs:195-278`) with a real harness shim that collects `test()`/`assert_*` results and emits PASS/FAIL per subtest. This is the single highest-value item — it makes `dom/`, `html/`, `js/`, `domparsing/`, `css/cssom*` (script-asserted) tests actually count. | testharness tests report real pass/fail |
| 1.2 | Reference-image backlog | Generate Chromium refs for the top skipped buckets (`css-flexbox` 1038, `css-ui` 1272, `css-break` 478, `css-transforms` 451 …) via the existing Playwright path in `run-wpt-tests.sh`. Cache refs as a CI artifact to avoid regenerating each run. | Skip rate < 20% on covered segments |
| 1.3 | Segment coverage checklist | Enumerate WPT top-level dirs and mark each: **gated / tracked / deferred**. Minimum *gated* set for preview: `css/CSS2`, `css/css-flexbox`, `css/css-text`, `css/selectors`, `dom`, `html` (parsing). Deferred (documented): `css-view-transitions`, `filter-effects`, `webgl`, `2dcontext`, workers/service-workers, fetch/network. | Checklist in repo, CI matrix matches |
| 1.4 | Acid tests (nice-to-have) | Wire Acid1 (already unit-tested) + Acid2/Acid3 fixtures (`acid/`) into CI as **non-gating** screenshot+diff jobs publishing a score. Track in `acid2-*`/`acid3-compliance.md`. | Acid score visible, not blocking |
| 1.5 | Coverage dashboard | Extend the WPT triage markdown (`wpt-triage-summary.md`) into a per-segment pass-rate table committed each nightly, so "what works" is the preview's public honesty signal. | Dashboard auto-updates |

**Phase 1 exit**: every segment in the gated set runs real assertions (not stubs)
with references present, results are per-segment, regressions alarm, and the
deferred list is explicit and documented.

---

### Phase 2 — Broiler.Graphics UI Migration (Workstream B)
*Goal: Broiler.App runs on Broiler.Graphics (Win32/Direct2D), at feature parity
with the WPF shell's browsing essentials.*

Strategy: **promote the `Broiler.HTML.Graphics.Win32.Demo` PoC into a real app
project** (`Broiler.Browser.Windows`) rather than mutating the WPF app in place.
Keep the WPF app buildable until parity is reached, then switch the shipped artifact.

**Status: core shell implemented and running (2.1–2.4, 2.6 done; 2.5 partial; 2.7
not started; 2.8 partial).** New project `src/Broiler.Browser.Windows` (assembly
`Broiler.Browser.Windows`) builds and launches a Direct2D browser that renders
URLs/local files, navigates, scrolls, and steps script-driven animations — no WPF.

| # | Task | Status | Delivered / Notes | Exit |
|---|------|--------|-------------------|------|
| 2.1 | Scaffold `Broiler.Browser.Windows` | ✅ Done | `BrowserWindow : Direct2DWindow` hosts `Broiler.HTML.Graphics.HtmlContainer`. Reuses the platform-neutral `RenderingPipeline.cs` / `PageLoader.cs` / `IPageLoader.cs` / `FavoritesManager.cs` from `Broiler.App` via linked `<Compile>` (no source duplication, WPF app untouched). `Program.cs` is the Win32 entry point. | Window opens, renders a URL ✅ |
| 2.2 | Navigation chrome | ✅ Done | Toolbar (Back/Forward/Refresh, address `BEditControl`, ☆ favorite toggle, Go) + a favorites bar of `BButtonControl`s rebuilt from `FavoritesManager` (shares the same `favorites.json` as the WPF app). History stack with fragment-anchor resolution. | Can navigate by typing a URL ✅ |
| 2.3 | Input translation | ✅ Done | **Extended `Broiler.Graphics`**: added `BPointerEventArgs`/`BMouseWheelEventArgs`/`BKeyEventArgs`/`BTextInputEventArgs` + virtual `OnPointer*`/`OnMouseWheel`/`OnKey*`/`OnTextInput` hooks on `BWindow`, wired in `Direct2DWindow`'s render-host window-proc (`WM_MOUSE*`/`WM_KEY*`/`WM_CHAR`/`WM_MOUSEWHEEL`, with screen→client + DIP conversion, mouse-leave tracking, focus-on-click). App maps these to `HtmlContainer.HandleMouse*`; link clicks drive navigation via the `LinkClicked` event. | Links clickable ✅ (hover re-render not wired) |
| 2.4 | Scrolling & viewport | ✅ Done | Scroll-offset state via `HtmlContainer.ScrollOffset`; mouse-wheel + keyboard (arrows/PageUp-Down/Home/End) scrolling, clamped to content height (`ActualSize`). Layout cached; only the render list rebuilds on scroll. Re-layout on `OnResized` (verified repaint after resize). No visible scrollbar yet. | Long pages scroll smoothly ✅ |
| 2.5 | Text input & focus | ◑ Partial | Address bar (native `BEditControl`) has full text entry + focus. In-page form-field text entry (`OnTextInput` → DOM) is **not** wired yet. | Address bar editable; form fields TODO |
| 2.6 | Animation loop | ✅ Done | **Extended `Broiler.Graphics`**: `StartAnimationTimer`/`StopAnimationTimer` + `OnAnimationTick` (backed by `SetTimer`/`WM_TIMER`/`KillTimer` in `Direct2DWindow`). App steps `InteractiveSession` one batch/tick and re-renders. Verified with a `setInterval` counter running to completion. | Script animations run ✅ |
| 2.7 | Dev console port | ☐ Not started | `DevConsolePanel` is still WPF-only; needs a Graphics panel/overlay over the platform-neutral `Broiler.DevConsole` backend. | Console usable in new app |
| 2.8 | Parity checklist & cutover | ◑ Partial | Both shells build in the solution; WPF kept as fallback. README/default not yet flipped (Phase 3 §3.4). | Graphics app is the default |

> **Rendering note (important):** the Graphics host renders reliably from
> **serialized HTML** (`HtmlContainer.SetHtml`). The typed-document handoff
> (`SetDocument(InteractiveSession.CurrentDocument())`, used by the WPF shell) lays
> out **empty** in `Broiler.HTML.Graphics` — so the app uses `InteractiveSession`'s
> serialized variants (`CurrentHtml()`, `Step()`) for both the initial paint and the
> animation loop. Making the typed-document path render in the Graphics host is a
> follow-up (it would avoid re-parsing HTML each animation frame).
>
> **Assembly-resolution note:** the HTML stack pulls in two physically distinct
> builds of same-identity assemblies (e.g. `Broiler.Dom` is checked out under both
> `Broiler.DOM` and the CSS submodule). MSBuild dedups them and drops the runtime
> entry from `deps.json`, so the host won't probe for them even though the DLLs are
> in the output. `Program.Main` installs an `AssemblyLoadContext.Resolving` fallback
> that loads such assemblies from the app directory. The demo PoC dodges this only
> because it never touches `Broiler.Dom`.
>
> **Sub-module note:** the input/timer additions to `Broiler.Graphics`
> (`BWindow`, `Direct2DWindow`, new `BInputEvents.cs`) were applied to **both**
> checkouts — top-level `Broiler.Graphics/` and the nested `Broiler.HTML/Broiler.Graphics/`
> (the copy the HTML build graph actually compiles against).

**Phase 2 exit**: `dotnet run` launches the Graphics browser; a user can browse a
representative set of real sites (navigation, scroll, click, form input, JS) with
no WPF dependency in the shipped path. *(Remaining for full exit: in-page form
text input (2.5), dev-console port (2.7), README/default cutover (2.8), and ideally
making the typed-document render path work to drop per-frame HTML re-parsing.)*

---

### Phase 3 — Public Preview Hardening & Release
*Goal: shippable artifact.*

| # | Task | Detail |
|---|------|--------|
| 3.1 | Packaging | Self-contained Windows build (single-folder or installer); app icon, version stamp. The first preview consumes Phase 0/1 of the [application installation and update roadmap](application-installation-and-update-roadmap.md): product/version contracts and a reviewed, signed portable artifact. Native installers, profile self-installation, automatic update, and rollback remain follow-up phases unless maintainers explicitly promote them to preview blockers. |
| 3.2 | Crash/error UX | Graceful page-load errors, render-exception guard (don't hard-crash on a bad page). |
| 3.3 | Smoke suite | A small set of "must render" real-world pages run in CI against the Graphics app (the README already aspires to a heise.de capture — wire it to the new app). |
| 3.4 | Docs refresh | ✅ Root and component READMEs now disclose preview instability, AI assistance, foundation provenance, licensing, and human-review status. Add final artifact install/run instructions and the WPT coverage dashboard link when those exist. |
| 3.5 | License and provenance audit | Preserve Apache-2.0 files for Broiler work, BSD-3-Clause conditions for inherited HTML Renderer material, Yantra JS attribution, Unicode data terms, and independent-project disclaimers in release artifacts. |
| 3.6 | Human review sign-off | A real developer reviews each release-facing component at the exact candidate commit, records evidence/findings/scope, and signs its `HUMAN_REVIEW.md`. `PENDING` is release-blocking. |
| 3.7 | Release | Tag, GitHub release with the packaged artifact + known-limitations list (deferred WPT segments, Windows-only). |

---

### Track C — Layout Component Extraction (parallel, non-blocking)
*Goal: purify HtmlBridge by extracting a dedicated `Broiler.HtmlBridge.Layout`
assembly. Runs alongside A/B; recommended before preview but not a gate.*

> **Audit 2026-06-27:** This track remains open. The completed `Broiler.Layout`
> extraction moved the renderer engine, not the parallel HtmlBridge box model.
> `CssBoxModel.cs` and `CssTextProperties.cs` still live in
> `Broiler.HtmlBridge.Rendering`; see
> [`refactor-gap.md`](refactor-gap.md), RF-BRIDGE-1.

| # | Slice | Detail |
|---|------|--------|
| C.1 | Define boundary | New `src/Broiler.HtmlBridge.Layout`. Introduce `IDomElement` + `ICssPropertyResolver` so layout no longer hard-references the bridge's concrete `DomElement` (`CssBoxModel.cs:124`, `:211`). |
| C.2 | Move box model | Relocate `CssBoxModel.cs` (block/flex/grid) + `CssTextProperties.cs` from `HtmlBridge.Rendering` into `HtmlBridge.Layout`. |
| C.3 | Move offset state | Move `LayoutRuntimeState` (`ElementRuntimeState.cs:42`) into Layout; `HtmlBridge.Dom` keeps only a reference, reads via an `ILayoutEngine`. |
| C.4 | Re-point consumers | `getComputedStyle`/offset queries and `TypeForwarding.cs` re-exports route through the new assembly. Paint/composite (`RenderingStages`), `HtmlPostProcessor`, `ImagePipeline` stay in `Rendering`. |
| C.5 | Verify | Existing `Broiler.App.Tests`/bridge tests green; `getComputedStyle` parity unchanged. |

> Why safe to defer: the shipped Graphics app paints from the renderer's
> `CssLayoutEngine`, not from HtmlBridge's `CssBoxModel`. C only cleans the
> JS-facing computed-layout path; it does not change pixels.

#### Should we do the Layout extraction at all? — Pro / Contra

*(This decision is currently open — captured here to decide deliberately rather
than by default. The question is not "is a clean layout boundary nice" — it
obviously is — but "is extracting `Broiler.HtmlBridge.Layout` worth doing now,
and is the current two-engine split even the right end state?")*

**Pro — reasons to do the extraction**

- **Honest module boundaries.** HtmlBridge's charter is DOM↔JS bridging +
  scripting. ~2,400 LOC of block/flex/grid layout (`CssBoxModel`,
  `CssTextProperties`) living there is mission creep; a `Layout` assembly makes
  the dependency graph state the truth.
- **Testability in isolation.** A standalone layout assembly behind `IDomElement`
  / `ICssPropertyResolver` can be unit-tested with synthetic inputs, without
  spinning up the JS engine or the full bridge.
- **Enables later convergence.** Today there are **two** box models (renderer's
  `CssLayoutEngine` and the bridge's `CssBoxModel`). A clean Layout boundary is
  the precondition for *eventually* collapsing them into one — the duplication is
  a latent correctness risk (`getComputedStyle` can disagree with painted pixels).
- **Smaller blast radius for the CSS component work.** The in-progress CSS
  extraction (phases 4–7) touches cascade/computed-styles; a separated layout
  module gives that work a stable seam to call instead of reaching into bridge
  internals.
- **Reduces churn during/after the UI migration.** Once the Graphics app is the
  product, you'll want layout reuse to be a clean library call, not buried in the
  bridge.

**Contra — reasons to defer or not do it**

- **Zero user-visible value for the preview.** It changes no pixels and no
  behaviour; it is pure internal hygiene. Every hour spent here is an hour not
  spent on the migration (B) or real test coverage (A), which *do* gate the preview.
- **Refactor risk on hot code.** `getComputedStyle`/offset queries are widely
  depended upon by scripts; an interface-extraction touching `CssBoxModel:124/211`
  and `ElementRuntimeState:42` risks subtle regressions right when the migration
  needs the bridge to be stable.
- **It may be solving the wrong problem.** The deeper issue is the **two-engine
  duplication**, not the *location* of the second engine. Extracting the bridge's
  `CssBoxModel` into its own assembly could **entrench** the duplicate by giving it
  a tidy home — when the better long-term move might be to delete it and have the
  bridge query the renderer's `CssLayoutEngine` for layout metrics. Extraction and
  unification are different projects; doing the first can quietly justify never
  doing the second.
- **CSS component work is mid-flight.** Phases 5–7 (`broiler-css-next-steps.md`)
  are actively moving cascade/computed-style code. Re-homing layout against
  interfaces *now* means rebasing against a moving target; doing it after phase 7
  settles is cheaper.
- **Interface design is hard to get right early.** `IDomElement` /
  `ICssPropertyResolver` drawn before the CSS component lands may need to be
  redrawn afterward — paying the abstraction tax twice.

**Recommendation.** **Defer past the preview; do not gate on it.** First decide
the *end state* — extract-and-keep-two-engines vs. unify-on-`CssLayoutEngine` —
before moving code, because that choice changes whether C.2/C.3 are even the
right slices. A cheap, reversible first step that pays off under either end state:
do **C.1 only** (introduce `IDomElement` / `ICssPropertyResolver` seams *in place*,
no code move) opportunistically during Phase 2 slack. Hold C.2–C.5 until after the
CSS component reaches phase 7, then revisit with the unification question answered.

---

## 5. Dependency & Sequencing Diagram

```
Phase 0 (CI safety net) ──┬─► Phase 1 (WPT completeness, Workstream A)
                          │
                          └─► Phase 2 (Graphics UI, Workstream B) ─► Phase 3 (Release)
                                   ▲
                                   │ (caught by A's nightly regressions)
                                   │
Track C (Layout extraction) ───────┘  runs in parallel, lands before/after preview, not a gate
```

- **0 must precede everything** (no signal = blind migration).
- **1 and 2 can overlap** once 0 lands; 1 protects 2.
- **3 needs 2 complete and 1's gated set green.**
- **C floats**; ideally C.1–C.3 land during Phase 2's slack.

---

## 6. Exit Criteria for the Preview

1. `build.yml` green on every PR; nightly WPT runs per-segment with regression gating.
2. Gated WPT segments run **real assertions** (testharness scored, not stubbed),
   references present, skip rate < 20% on those segments.
3. Broiler.App runs on **Broiler.Graphics (Direct2D)** with no WPF in the shipped
   path: navigate, scroll, click, hover, form input, JS, CSS animation all work.
4. Packaged Windows artifact + refreshed README + published WPT coverage dashboard
   (the public honesty signal) + documented known-limitations list.
5. Apache-2.0 and third-party license/provenance notices are present in the source and
   packaged artifact; all component `HUMAN_REVIEW.md` files name the release-candidate
   commit and contain an attributable human approval.
6. *(Nice-to-have)* Acid1 passing; Acid2/Acid3 scores published, non-gating.

---

## 7. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| testharness shim (1.1) is larger than expected (event loop, async tests, `done()`) | Slips Phase 1 | Land it incrementally: sync tests first, then async/promise; ship per-segment as ready. It's the highest-value item — start it first in Phase 1. |
| Reference generation cost/time (1.2) blows the 360-min CI cap | Nightly times out | Segment matrix (0.3) + cache references as artifacts; regenerate only changed segments. |
| Input/focus/scroll in Win32 (2.3–2.5) is fiddly vs free-in-WPF | Slips Phase 2 | The PoC already handles the render path; budget these three as the real work. Parity checklist (2.8) catches gaps early. |
| WPF retired before Graphics reaches parity | Regression for users | Keep WPF buildable as fallback until 2.8 passes; cut over only then. |
| Layout extraction (C) changes `getComputedStyle` output | JS-visible regression | Interface-only refactor; gate on bridge tests + computed-style parity (C.5). Defer if risky. |
| Windows-only preview disappoints cross-platform expectations | Perception | State Windows-only in known-limitations; point to the separate Avalonia roadmap. |

---

## 8. Open Decisions

- **No external UI dependency — DECIDED (closed).** The preview UI is built only
  on Broiler.Graphics; **no Avalonia, no WPF in the shipped path, no other
  external toolkit**. See [the firm constraint in §1](#firm-constraint-no-external-ui-dependency).
  Cross-platform, if pursued, extends Broiler.Graphics' backends — it does not
  reopen this decision.

Still open:

1. **WPF app fate**: Retire vs freeze-as-fallback after cutover (proposed:
   freeze during migration, retire post-preview — it must not ship).
2. **Layout extraction (Track C)** — *open, leaning defer.* Should we extract
   `Broiler.HtmlBridge.Layout` at all, and if so when? See the full
   [pro/contra and recommendation in Track C](#should-we-do-the-layout-extraction-at-all--pro--contra).
   Proposed: not a launch gate; decide the end state (extract-and-keep-two-engines
   vs. unify on `CssLayoutEngine`) before moving code; optionally do the in-place
   seam (C.1) during Phase 2 slack.
3. **Gated WPT segment set**: confirm the minimum list in 1.3 (and the Phase 0
   matrix segments) is the right bar for "all segments that need testing," or
   expand it.
4. **WPT matrix cadence**: weekly now (reference-generation cost). Tighten to
   nightly once references are cached and the gated set fits the time budget?
