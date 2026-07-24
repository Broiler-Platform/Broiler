# Broiler documentation

This directory contains documentation for the aggregate Broiler repository:
cross-component architecture, integration contracts, and work that must be
coordinated across more than one component. Component-owned design, API,
conformance, and implementation work belongs with that component.

## Authoritative documents

| Document | Purpose |
| --- | --- |
| [Root roadmap](ROADMAP.md) | The unfinished cross-repository work and its exit gates |
| [HtmlBridge architecture](architecture/htmlbridge.md) | Current bridge assemblies, ownership boundaries, and public seams |
| [Browser WebAssembly architecture](architecture/browser-webassembly.md) | Current browser-host, rendering, input, and support decisions |

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
- [Broiler.JS](../Broiler.JS/docs/roadmap.md)
- [Broiler.JS performance](../Broiler.JS/docs/performance.md)
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

Machine-consumed baselines belong beside their harnesses. The browser WebAssembly
closure fixture is under
[`tests/browser-wasm-phase0/baselines`](../tests/browser-wasm-phase0/baselines/).
Chromium reference locks and focused conformance summaries are under
[`tests/m2-conformance`](../tests/m2-conformance/). Generated WPT and visual
comparison output should remain reproducible from scripts and workflows rather
than being maintained as prose delivery journals.
