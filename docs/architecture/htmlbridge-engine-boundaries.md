# HtmlBridge engine boundaries and current abstraction leaks

`Broiler.HtmlBridge` is the seam between the JavaScript engine, the bridge-owned
DOM model, and the HTML renderer. M0 records the boundary **as it exists today**
so later milestones can tighten it without losing sight of the current coupling.

## Current boundary map

### JavaScript → Bridge

- `IScriptEngine` is the intended bridge-facing abstraction for script
  execution, including DOM-aware execution, deferred scripts, CSP, microtasks,
  and interactive stepping.
- `DomBridge.Attach(JSContext, html[, url])` registers `document` and
  `window.location` on a concrete `JSContext`.
- `DomBridge.RegisterNamedElementGlobals`, timer flushing, and viewport helpers
  expose the bridge-owned DOM state back into JavaScript execution.

### Bridge → HTML

- `DomBridge` parses HTML into `DomElement` instances and serializes the updated
  DOM back into HTML strings after script execution.
- `HtmlContainer` / `HtmlRender` then consume that post-bridge HTML for layout,
  paint, and raster output.
- `CaptureService.ExecuteScriptsWithDom` is the main end-to-end composition
  point used by CLI capture and Acid3 execution.

### PR dashboard surfaces

- JavaScript conformance baseline: `src/Broiler.Engines.Baseline` `test262`
  command.
- WPT-relevant suites: the existing `Broiler.Cli.Tests` WPT-derived test
  classes (`WptCssVariablesTests`, `WptCompositingTests`,
  `WptFontAndSelectorTests`).
- Acid3 regression: `src/Broiler.Cli.Tests/Acid3RegressionTests.cs`.

## Current abstraction leaks

1. **Concrete JavaScript engine types cross the public bridge seam.**
   `DomBridge` is public, but its main attachment path requires concrete
   `JSContext`, `JSObject`, `JSFunction`, and `JSValue` types rather than a
   bridge-owned abstraction.
2. **Bridge internals leak HTML-engine internal types.**
   `DomBridge.Elements` exposes `DomElement`, and multiple bridge partials work
   directly against `Broiler.HTML.Core.Core.Entities` types instead of a stable
   bridge DTO surface.
3. **CLI capture still orchestrates bridge-specific async details itself.**
   `CaptureService.ExecuteScriptsWithDom` knows about `MicroTaskQueue`,
   `DomBridge.AsyncDrainIterationLimit`, script extraction order, and DOM
   serialization rather than delegating the whole script+DOM lifecycle to a
   single bridge abstraction.
4. **The JS ↔ Bridge ↔ HTML hand-off is still string-based.**
   Script execution returns serialized HTML instead of a typed DOM/render tree
   contract, so later pipeline stages re-parse data that the bridge already had
   in memory.

## Why this is the M0 baseline

This document intentionally records the current seams without changing them.
M1 can now measure progress against a fixed boundary map: fewer concrete
JavaScript types at the bridge API, fewer renderer internals exposed from the
bridge, and a typed hand-off that avoids HTML re-serialization as the default
integration path.
