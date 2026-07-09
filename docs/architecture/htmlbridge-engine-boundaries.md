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

### Versioned DOM adapter policy

`Broiler.HtmlBridge.DomElement` and `HtmlTreeBuilder` are the explicit
`htmlbridge-dom-adapter/v1` compatibility boundary. Their version and removal
boundary are published as constants and locked by architecture tests. They remain
supported for `htmlbridge-public-surface/v1` consumers and may be removed only in
`htmlbridge-public-surface/v2` or later.

This adapter is not parallel ownership. `DomElement` delegates tree and attribute
state to the canonical node model, and `HtmlTreeBuilder` delegates parsing to
`Broiler.Dom.Html`. The adapter must not acquire independent runtime state,
standards algorithms, parsing, or serialization. New in-repo code should consume
canonical nodes and parser contracts directly.

HTML tokenization, document/fragment tree construction, and deterministic
serialization are owned by `Broiler.Dom.Html`. The bridge keeps only a
versioned compatibility materializer for its `DomElement` facade, while the
renderer consumes the same shared parser contract. `innerHTML`,
`document.write`, and subdocument parsing therefore no longer carry separate
tree-builder implementations.

The optional `ITypedScriptEngine` extension and renderer
`SetDocument(DomDocument, ...)` entry points form the Phase 5 typed hand-off.
The WPF interactive path uses this route exclusively; RF-DOM-1 removed its unused
serialized-mode switch and alternate payload. The frozen
`IScriptEngine.Execute(...)` string methods and `SetHtml(...)` renderer methods
remain direct public-v1 compatibility APIs, but no longer form an alternate
application pipeline.

TreeWalker and NodeIterator state and traversal are now canonical
`Broiler.Dom` algorithms. The bridge only converts JavaScript filters and node
wrappers. Canonical `DomRange` provides engine-neutral boundary and mutation
semantics. Client-rectangle calculation deliberately remains bridge-owned because
it consumes computed style and renderer geometry; its `display: contents`
descendant behavior is covered by a dedicated regression gate. Content operations
and JavaScript wrappers likewise remain outside the dependency-free kernel.

Mutation-observer semantics are **not** yet fully canonical. The bridge still
owns `MutationObserver` registration, observer-option matching, and mutation
record filtering (`DomBridge/Registration/Events.cs` and the surrounding
`__broilerRegisterMutationObserver` plumbing); it adapts filtered records into
JavaScript callback objects. Several `DomRange` content operations are likewise
still implemented in bridge rather than the kernel. Promoting the neutral parts
of these paths — option matching/record filtering into `Broiler.DOM` and range
content operations into canonical `DomRange` — is scoped as Phase 4 of the
[DOM/CSS promotion roadmap](../roadmap/htmlbridge-dom-css-promotion-roadmap.md),
not something already delivered here.

## DOM/CSS promotion — Phase 0 baseline (2026-07-09)

Phase 0 of the
[HtmlBridge DOM/CSS promotion roadmap](../roadmap/htmlbridge-dom-css-promotion-roadmap.md)
locks the boundary before any promotion PR slice moves code out of the bridge.
The roadmap's later phases move neutral CSS/DOM algorithms into `Broiler.CSS`,
`Broiler.CSS.Dom`, and `Broiler.DOM`. This section is the frozen inventory of
what stays in the bridge and why, so those moves cannot silently re-package
compatibility code as canonical engine code.

### Allowed HtmlBridge compatibility seams

Each seam is an intentional bridge-owned shim. Its removal boundary is
`htmlbridge-public-surface/v2`; do not delete or promote it earlier. Adding a new
seam to this list must be a deliberate, reviewed decision.

| Seam | Kind | Ownership rationale | Promotion disposition |
|---|---|---|---|
| `Broiler.HtmlBridge.DomElement` | Versioned DOM adapter (`htmlbridge-dom-adapter/v1`) | Source-compatibility facade over `Broiler.Dom.DomElement`; delegates tree/attribute state to the canonical node | Non-candidate — remove at v2, do not promote |
| `Broiler.HtmlBridge.HtmlTreeBuilder` | Versioned DOM adapter (`htmlbridge-dom-adapter/v1`) | Compatibility materializer over `Broiler.Dom.Html.HtmlDocumentParser` | Non-candidate — remove at v2, do not promote |
| `DomBridge.CssRules` | Obsolete CSSOM compatibility view | Historical `(selector, specificity, declarations)` tuple projection; not a cascade store or render input | Remove at v2; consumers move to shared `Broiler.CSS` stylesheet/style-engine APIs |
| `DomBridge.CalculateSpecificity(string)` | Static selector-specificity helper | Thin delegation to `Broiler.CSS.CssSelectorParser.CalculateSpecificity` | Remove at v2; public replacement is the CSS parser API |
| CSSOM JavaScript wrappers (`DomBridge/StyleSheets.cs`) | Live JS object identity | Build `CSSStyleSheet`/`CSSRule` JS objects with parent-rule/sheet wiring and mutation events | Keep in bridge; Phase 3 thins them over a neutral CSS rule projection |
| `ElementRuntimeState` | Bridge runtime state | JS identity, listeners, mutation-observer options, form/scroll/layout cache, dialog/shadow/animation state | Non-candidate — stays bridge-owned |
| Host / resource loading (external stylesheet + subresource fetch) | Host integration | Fetching and base-path resolution are host concerns, not DOM/CSS | Keep in bridge; only host-supplied *text* assembly may move to CSS.Dom (Phase 2 open question) |

The `MutationObserver` registration/option-matching/record-filtering paths and
several `DomRange` content operations are also still bridge-owned; see the
"Versioned DOM adapter policy" note above for their Phase 4 disposition.

### Caller catalog (pre-v2 surface audit)

Callers of the four named public seams, as of the Phase 0 baseline. This is the
migration checklist that must reach zero non-test in-repo callers before the
seam can be removed at v2.

| Seam | In-repo production callers | Test callers |
|---|---|---|
| `DomElement` | Bridge-wide (`Broiler.HtmlBridge.Dom`); public entry points are `DomBridge.DocumentElement` / `DomBridge.Elements` | DOM/CSS/WPT compatibility tests via those entry points |
| `HtmlTreeBuilder` | Bridge-internal only (`DomBridge.cs`, `DomBridge/SubDocuments.cs`, `DomBridge/JsFunctionCallbacks/*`) | `DomExtractionPhaseZeroTests` |
| `DomBridge.CssRules` | **None** outside the bridge (already `[Obsolete]`) | `CssExtractionPhaseTwoTests`, `SelectorsLevel4SpecificityTests` |
| `DomBridge.CalculateSpecificity` | **None** — no in-repo callers; tests call `Broiler.CSS.CssSelectorParser.CalculateSpecificity` directly | (specificity behavior covered against the CSS parser, not the bridge shim) |

### Phase 0 guard tests

The baseline is enforced from the main-repo test project so the guards run in
main-repo CI without depending on submodule test runs:

- `Broiler.Cli.Tests.HtmlBridgePromotionPhaseZeroTests` — freezes the seam
  versions/shape above and guards that `Broiler.Dom` does not reference or expose
  JavaScript-engine types and that `Broiler.CSS.Dom` does not reference
  `Broiler.HtmlBridge.*`.

The canonical components additionally self-guard inside their own submodules
(`Broiler.Dom.Tests.DomArchitectureTests` forbids all non-framework dependencies;
`Broiler.CSS.Dom.Tests.CssDomArchitectureTests` pins CSS.Dom's project references
to `Broiler.CSS` + `Broiler.Dom` only).

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
