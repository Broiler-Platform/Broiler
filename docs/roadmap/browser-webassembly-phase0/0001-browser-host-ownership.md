# Decision 0001 - Browser Host Ownership and Topology

**Status:** Approved for Phase 1  
**Date:** 2026-07-11

## Context

`Broiler.Graphics` and `Broiler.UI` already expose platform-neutral render and
host seams. Adding browser types to either core before exercising those seams
would create coupling without evidence.

The UI topology ADR also keeps platform host code in samples/hosts or outside UI
runtime assemblies. The assembly name `Broiler.Browser.Windows` is already used
by the desktop Broiler browser application.

## Decision

The first host is
`Broiler.UI/samples/WebAssembly/Broiler.UI.WebAssembly.Demo`.

It owns:

- .NET/JavaScript bootstrap;
- canvas element binding;
- resize, device-pixel ratio, and page lifecycle observation;
- animation-frame scheduling and UI dispatch;
- sample-local browser input/text adapters;
- optional clipboard/cursor/settings/accessibility ports; and
- deterministic teardown.

The first real application host is `src/Broiler.App.WebAssembly`.

`Broiler.Graphics.WebAssembly` is created only after the CPU-presentation
performance/reuse gate. Browser Input implementation assemblies are extracted
only before a second consumer or productized provider claim. No
`Broiler.UI.WebAssembly` runtime assembly is added under `Broiler.UI/src`.

## Consequences

- Phase 1 can proceed without a neutral public API change.
- Browser code cannot reference Windows/Linux backend projects.
- The sample may contain intentionally temporary adapters, but the support
  statement must name them as sample-owned.
- Reusable provider changes land in their canonical component first, then in
  consumers, then in aggregate/nested checkout pointers.
- Full `Broiler.Browser.Windows` engine hosting is not implied by the UI sample.
