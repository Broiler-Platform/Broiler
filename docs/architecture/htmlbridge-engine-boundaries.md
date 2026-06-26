# HtmlBridge engine boundaries (M1 frozen surface)

`Broiler.HtmlBridge` is the seam between the JavaScript engine, the bridge-owned
DOM model, and the HTML renderer. M1 freezes the currently supported public
boundary so follow-on compliance work can tighten behavior without accidentally
expanding the cross-engine contract.

## Boundary version

- **Boundary version:** `htmlbridge-public-surface/v1`
- **Status:** frozen for M1 follow-on work
- **Change policy:** additive or behavioral changes inside this boundary require
  spec citations, targeted tests, and an explicit roadmap note; any breaking
  surface change requires a boundary-version bump.

## Frozen public seam

### `IScriptEngine` contract (`htmlbridge-public-surface/v1`)

`IScriptEngine` is the preferred consumer-facing bridge contract. Its public
surface is intentionally free of concrete `Broiler.JavaScript.*` and
`DomElement` types.

| Surface | Purpose | Primary spec anchor | Notes |
|---|---|---|---|
| `Execute(...)` overloads | Execute inline and deferred scripts against a document and return serialized HTML | HTML Living Standard — scripting, script processing model, window load/event-loop integration | `string` HTML hand-off remains a compatibility choice, not a final typed boundary |
| `ExecuteDetailed(...)` | Broiler diagnostic wrapper around script execution | No direct WHATWG/W3C API | Non-standard diagnostic surface |
| `ExecuteInteractive(...)` | Interactive timer-batch stepping for capture/dev workflows | HTML timers + animation frame processing | Broiler-specific debugging surface layered on top of browser concepts |
| `StrictModeEnabled` | Prepend `"use strict"` to executed sources | ECMA-262 strict mode | Broiler execution option, not a web-platform API |
| `Csp` | Apply bridge CSP decisions | CSP Level 3 (`script-src`, `script-src-elem`, `default-src`) | Bridge-owned policy object |
| `Profiler` | Collect per-script timings | No direct WHATWG/W3C API | Broiler-specific diagnostics used by perf gating |
| `MicroTasks` | Drain queued microtasks/promises | HTML event loop / microtask checkpoint | Bridge-owned helper rather than a browser API |

### `DomBridge` stable orchestration surface (`htmlbridge-public-surface/v1`)

These members are frozen for existing in-repo orchestration and capture flows
without exposing concrete JavaScript-engine types.

| Surface | Purpose | Primary spec anchor | Notes |
|---|---|---|---|
| `AsyncDrainIterationLimit` | Safety cap for timer/microtask draining | HTML event loop processing model | Broiler guard rail |
| `TaskCheckpointCallback` | Hook used to run a microtask checkpoint after queued tasks | HTML "perform a microtask checkpoint" | Bridge-owned integration seam |
| `Title` | Current document title mirror | HTML document title | Read-only outside bridge mutation paths |
| `SetViewportSize(...)` | Seed viewport-dependent layout / geometry | CSSOM View | Bridge utility surface |
| `SetLocalBasePath(...)` | Resolve local subresources for tooling | No direct WHATWG/W3C API | Broiler tooling helper |
| `FlushTimers()` / `FlushTimerStep()` / `HasPendingTimers` | Drive queued timers / RAF batches | HTML timers / animation frame callbacks | Used by CLI/WPT harnesses |
| `FireWindowLoadEvent()` | Dispatch post-parse load lifecycle | HTML load event processing | End-of-execution bridge hook |
| `SerializeToHtml()` | Return post-execution DOM as HTML | DOM Parsing/Serialization concepts | Compatibility hand-off to the renderer |
| `ResolveAnchorPositions(...)` | Apply anchor-position snapshots before serialization/rendering | CSS Anchor Positioning draft | Convenience pipeline hook |
| `ResolveAnimationSnapshots()` | Resolve animation-driven state before serialization/rendering | Web Animations / CSS Animations concepts | Convenience pipeline hook |
| `CalculateSpecificity(...)` | Shared selector-specificity helper | Selectors Level 4 | Static helper used by bridge/render paths |
| `CssRules` | Expose parsed stylesheet rule cache to in-repo consumers | CSSOM | Bridge-owned representation, not browser-native CSSRule objects |

## Frozen compatibility-only leak surface

The following public `DomBridge` members still expose engine-internal types.
They are **not** part of `htmlbridge-public-surface/v1`; they are compatibility
members that are now frozen so M2+ work cannot silently add new leaks.

| Member | Leak type | Why it remains | Follow-up |
|---|---|---|---|
| `Attach(JSContext, string)` | concrete `JSContext` | Current registration path still binds directly to YantraJS globals | Replace with a bridge-owned context abstraction in a later boundary-version bump |
| `Attach(JSContext, string, string)` | concrete `JSContext` | Same as above, plus URL seeding | Same follow-up as above |
| `RegisterNamedElementGlobals(JSContext)` | concrete `JSContext` | Legacy HTML named-access wiring still edits the engine global object directly | Fold into the future abstracted attach/runtime surface |
| `DocumentElement` | compatibility `DomElement` facade over `Broiler.Dom.DomElement` | Existing test/WPT helpers still use the legacy surface | Migrate callers to canonical nodes or typed snapshots before removing the facade |
| `Elements` | tree-derived `IReadOnlyList<DomElement>` | Existing capture/execution helpers still enumerate nodes through the compatibility view | Replace remaining callers with canonical queries or typed snapshots |

As of 2026-06-24, `DomBridge` owns a canonical `Broiler.Dom.DomDocument`, and
the legacy `DomElement` type derives from the canonical node type. Child and
attribute mutations route through canonical mutation APIs. JavaScript listeners,
observer options, and IDL runtime state remain bridge-owned. The legacy public
type remains only as a source-compatibility facade.

HTML tokenization, document/fragment tree construction, and deterministic
serialization are owned by `Broiler.Dom.Html`. The bridge keeps only a
compatibility materializer for its temporary `DomElement` facade, while the
renderer consumes the same shared parser contract. `innerHTML`,
`document.write`, and subdocument parsing therefore no longer carry separate
tree-builder implementations.

The optional `ITypedScriptEngine` extension and renderer
`SetDocument(DomDocument, ...)` entry points form the Phase 5 typed hand-off.
The WPF interactive path uses this route by default. The frozen
`IScriptEngine.Execute(...)` string methods and `SetHtml(...)` renderer methods
remain compatibility surfaces, selectable through
`RenderingPipeline.HandoffMode.SerializedHtml`.

TreeWalker and NodeIterator state and traversal are now canonical
`Broiler.Dom` algorithms. The bridge only converts JavaScript filters and node
wrappers. Canonical `DomRange` provides engine-neutral boundary and mutation
semantics; bridge-only range geometry and content operations remain outside the
kernel.

## PR dashboard surfaces tied to this boundary

- JavaScript conformance baseline:
  `src/Broiler.Engines.Baseline` `test262`
- WPT-relevant suites:
  `Broiler.Cli.Tests.WptCssVariablesTests`,
  `Broiler.Cli.Tests.WptFontAndSelectorTests`
- Acid3 regression:
  `src/Broiler.Cli.Tests/Acid3RegressionTests.cs`
- Benchmark regression gate:
  `Broiler.Engines.Baseline benchmarks --baseline ... --budget-percent 2`
  (blocking today on `js.startup`, `html.raster`, and `bridge.mutation`)

## Related documents

- [HtmlBridge API/spec map](./htmlbridge-spec-map.md)
- [Engines M0 baseline](../roadmap/engines-m0-baseline.md)
- [Cross-engine roadmap](../roadmap/engines-standards-and-performance-roadmap.md)
