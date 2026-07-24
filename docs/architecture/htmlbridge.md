# HtmlBridge architecture

- **Boundary:** `htmlbridge-public-surface/v2`
- **Status:** Current aggregate-repository architecture
- **Last reconciled:** 2026-07-24

HtmlBridge connects the Broiler JavaScript engine to the canonical DOM, CSS,
HTML, and layout components. It owns JavaScript-facing browser behavior and host
integration; it does not own parallel implementations of those canonical
engines.

## Assembly shape

The source contains three HtmlBridge code assemblies:

| Assembly | Responsibility |
| --- | --- |
| `Broiler.HtmlBridge.Core` | Shared contracts and value objects, script extraction descriptors, CSP parsing, profiling records, and logging |
| `Broiler.HtmlBridge.Dom` | `DomBridge`, JavaScript DOM/CSSOM/event bindings, browser runtime state, resource and browsing-context policy, serialization projection, and the document event loop |
| `Broiler.HtmlBridge.Scripting` | Public execution interfaces, `ScriptEngine`, interactive sessions, and orchestration of script execution around a bridge instance |

The former `Broiler.HtmlBridge.Rendering` project has no source project in the
current graph. Canvas recording and the remaining render-preparation helpers are
internal to the DOM assembly.

The principal dependency direction is:

```text
Broiler.HtmlBridge.Scripting
  -> Broiler.HtmlBridge.Dom
    -> Broiler.HtmlBridge.Core
```

Core and Dom also depend on the narrow canonical components they adapt. A new
assembly is justified only by a stable deployment or dependency seam, not by a
single web-platform feature.

## Canonical owners and bridge responsibilities

| Concern | Canonical owner | HtmlBridge responsibility |
| --- | --- | --- |
| DOM nodes, mutation, traversal, ranges | `Broiler.Dom` | Preserve JavaScript wrapper identity, convert arguments/results, and dispatch browser-facing behavior |
| HTML parsing and neutral serialization | `Broiler.Dom.Html` / `Broiler.HTML` | Apply host/runtime state and produce the renderer-facing projection |
| CSS syntax, selectors, cascade, computed values | `Broiler.CSS` / `Broiler.CSS.Dom` | Own CSSOM JavaScript objects, live identity, host-fetched stylesheet text, and invalidation orchestration |
| Layout boxes and used geometry | `Broiler.Layout` through `ILayoutView` | Translate layout snapshots into CSSOM View, hit testing, scroll, sticky, and anchor APIs |
| JavaScript language and modules | `Broiler.JS` | Supply browser globals, DOM bindings, module URL resolution, CSP/host policy, and page lifecycle ordering |
| Raster/image/audio/video implementation | `Broiler.Graphics` / `Broiler.Media` / `Broiler.HTML` | Record Canvas API calls and route resources; do not duplicate codecs |
| Networking, origins, frames, timers, messaging | HtmlBridge/host | Enforce host policy and expose browser-shaped JavaScript behavior |

Feature bindings should be co-located by web-platform surface and depend on
narrow host interfaces. Runtime state is instance-scoped to the owning
`DomBridge`; process-global per-element state is forbidden.

## Public v2 seam

- `IScriptEngine` remains the compatibility interface. It aggregates
  `IScriptExecutor`, `IInteractiveScriptEngine`, `IScriptProfiling`, and
  `IScriptEventLoop`, so capability-specific consumers can request a narrow
  surface without breaking existing v2 callers.
- `ITypedScriptEngine` adds typed-document execution.
- `DomBridge` implements `IDomBridgeRuntime`; `DomBridgeFactory` implements
  `IDomBridgeRuntimeFactory`.
- Public changes must remain additive or behavior-preserving within v2 unless a
  separately approved v3 boundary is introduced.
- Internal helpers, generated-style callbacks, and compatibility transforms must
  not become new public contracts accidentally.

The executable surface is guarded by the architecture and compatibility tests
under [`src/Broiler.Cli.Tests`](../../src/Broiler.Cli.Tests/).

## Document and execution flow

1. `ScriptExtractionService` classifies synchronous, deferred, asynchronous, and
   module scripts and applies the host URL/CSP input.
2. `ScriptEngine` creates the JavaScript context and one `DomBridge` for the
   document/session.
3. `RunPageScripts` drives the shared normal, detailed, typed, and interactive
   execution path. ES modules use the Broiler.JS module machinery and
   host-provided resolution context.
4. `DomBridge` mutates a canonical `DomDocument`; JavaScript wrappers retain
   object identity without becoming a second DOM.
5. Rendering builds a non-destructive renderer projection from the canonical
   state. Geometry queries use the layout snapshot described below.
6. Session disposal tears down queued work, layout resources, bridge state, and
   the JavaScript context.

## Layout and geometry

`DomBridge` obtains real box geometry through the neutral
[`ILayoutView`](../../Broiler.Layout/Broiler.Layout/ILayoutView.cs) contract. A
read pass builds at most one `DomElement`-keyed geometry snapshot and reuses it
for `getBoundingClientRect`, `client*`, `offset*`, scrolling, hit testing, and
check-layout assertions. Elements that produce no box return the defined empty
geometry rather than reviving a parallel estimator.

The concrete headless view is registered by an application/test composition
root. The current static `LayoutViewFactory` is a temporary seam; replacing it
with explicit session/composition injection is tracked in
[the root roadmap](../ROADMAP.md#htmlbridge-runtime).

Geometry snapshots enable the layout engine's native CSS `zoom` used-value path.
The direct serialization/capture path still retains a compatibility carry-through
when its downstream renderer has not enabled native zoom. The carry-through can
be deleted only after every renderer consumer participates in the native route.

## Event loop and module execution

`BrowserEventLoop` is the document-scoped owner of timeout, interval,
animation-frame, and internal frame-action queues. Timers share an ID space and
run by virtual deadline with FIFO ordering for equal deadlines. A microtask
checkpoint runs after each task.

`ScriptEngine` defers timer draining until after synchronous script phases and
window load. The remaining standards gap is a single ordered task model that
interleaves deferred/module tasks with timer and rendering tasks instead of
driving them through fixed phase buckets. That work is tracked in
[the root roadmap](../ROADMAP.md#htmlbridge-runtime).

## Dependency and ownership rules

- Canonical components never reference HtmlBridge.
- Core contracts do not acquire renderer, platform, or application dependencies.
- DOM/CSS algorithms move to their canonical components only when they are
  independent of JavaScript identity and host policy.
- Resource fetching remains host/bridge-owned; parsers and style engines consume
  supplied bytes or text.
- Layout answers geometry; the bridge owns the JavaScript/CSSOM View projection.
- A bridge/document session owns and disposes all mutable runtime state.
- WPT-only transforms stay explicit and cannot silently become the production
  semantics.

## Related documentation

- [Root roadmap](../ROADMAP.md)
- [Documentation index](../README.md)
- [Browser WebAssembly architecture](browser-webassembly.md)
