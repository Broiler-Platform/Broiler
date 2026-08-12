# Broiler documentation

This directory contains documentation for the aggregate Broiler repository:
cross-component architecture, integration contracts, and work that must be
coordinated across more than one component. Component-owned design, API,
conformance, and implementation work belongs with that component.

## Authoritative documents

| Document | Purpose |
| --- | --- |
| [Root roadmap](ROADMAP.md) | The unfinished cross-repository work and its exit gates |
| [Broiler Code roadmap](broiler-code-roadmap.md) | The phased plan for a shared C#/.NET IDE, multi-project editing, diagnostics, and .NET/Android/WebAssembly builds |
| [Performance and benchmark roadmap](../Broiler.JS/docs/roadmap/Roadmap.md) | **Moved into the `Broiler.JS` submodule on 2026-08-07** — it was `docs/performance-roadmap.md` with its detail in `docs/performance/`, and it is now [`Broiler.JS/docs/roadmap/`](../Broiler.JS/docs/roadmap/) in full. JavaScript execution speed: the phased plan, the Octane and probe metrics it is judged on, and the evidence each phase still owes. It spans the main-repo `tests/octane` harness and the engine, and the harness half is reached by name rather than by link — a submodule cannot link to its parent. It moved because every *item* in it changes engine source |
| [WPT rendering gaps](wpt-rendering-gaps.md) | The worst-scoring WPT pixel mismatches, the capability each one is missing, and its owning component |
| [Real-world website render tests](real-world-render-tests.md) | The public-site visual corpus, its live and recorded-DOM comparison lanes, metrics, CI workflow, and reproducible local command |
| [Broiler Code architecture](architecture/broiler-code.md) | Source of truth for Broiler Code's capability boundary, workspace and diagnostic model, trust boundary, and build-worker contract |
| [Broiler Code budgets and support matrix](architecture/broiler-code-budgets.md) | The frozen performance budgets, the traces they are measured against, the recorded baselines, and what is still unmeasured |
| [SDK-project mutation matrix](architecture/broiler-code-project-mutations.md) | Which project constructs Broiler Code may edit, read losslessly, or only evaluate |
| [HtmlBridge architecture](architecture/htmlbridge.md) | Current bridge assemblies, ownership boundaries, and public seams |
| [Browser WebAssembly architecture](architecture/browser-webassembly.md) | Current browser-host, rendering, input, and support decisions |
| [Android application architecture](architecture/android.md) | Proposed Android host topology, platform baseline, and ownership boundaries |
| [Multithreading analysis and roadmap](architecture/multithreading.md) | Where concurrency can and cannot speed up each component, and the order the work has to happen in |

Completed migration plans, delivery logs, and investigation journals remain
available in Git history. They are not the current backlog; durable decisions
and unresolved outcomes have been consolidated here or moved to the owning
component.

## Component ownership

The component roadmaps are the source of truth for component-local work:

- [Broiler.CSS](../Broiler.CSS/docs/roadmap.md)
- [Broiler.DOM](../Broiler.DOM/docs/roadmap.md)
- [Broiler.Documents](../Broiler.Documents/docs/roadmap.md)
- [Broiler.Graphics](../Broiler.Graphics/docs/roadmap.md)
- [Broiler.HTML](../Broiler.HTML/docs/roadmap.md)
- [Broiler.Input](../Broiler.Input/docs/roadmap.md)
- [Broiler.JS](../Broiler.JS/docs/roadmap/) — all three of its plans, indexed; the
  component roadmap is [`component.md`](../Broiler.JS/docs/roadmap/Component.md) and the
  performance campaign is
  [`performance.md`](../Broiler.JS/docs/roadmap/Roadmap.md)
- [Broiler.JS performance acceptance](../Broiler.JS/docs/roadmap/Measurement.md) — the gate
  every performance claim in this repository passes, engine or harness
- [Broiler.Regex](../Broiler.JS/Broiler.Regex/docs/roadmap.md)
- [Broiler.Layout](../Broiler.Layout/docs/roadmap.md)
- [Broiler.Media](../Broiler.Media/docs/roadmap.md)
- [Broiler.UI](../Broiler.UI/docs/roadmap.md)

The component READMEs describe supported surfaces and standalone validation
commands. A root document should link to those component records instead of
copying their implementation checklists.

## Documentation rules

- Keep a roadmap item only while an outcome remains open. Remove completed
  implementation histories once any durable decision has been folded into an
  architecture or support document.
- Give every roadmap item an owner, current evidence, next action, and objective
  exit gate. A checked historical task is not release or conformance evidence.
- Put API and dependency rules beside the component that enforces them.
- Put generated test reports and comparison images under `tests/` or the ignored
  `artifacts/` directory, not under `docs/`.
- Record current support honestly. Local smoke evidence does not imply
  cross-browser, accessibility, hardware, security, or production support.

## Test evidence

Machine-consumed baselines belong beside their harnesses. Chromium reference
locks and focused conformance summaries are under
[`tests/m2-conformance`](../tests/m2-conformance/); durable WPT expected
failures are under [`tests/wpt-baseline`](../tests/wpt-baseline/). Generated WPT
and visual comparison output should remain reproducible from scripts and
workflows rather than being maintained as prose delivery journals — and should
not be committed at all, which is why the April 2026 WPT result snapshots and
the browser WebAssembly phase-0 baselines were retired. A committed baseline
that no harness compares against is a prose journal in a binary format.
