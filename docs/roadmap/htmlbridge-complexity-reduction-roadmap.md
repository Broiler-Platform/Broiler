# HtmlBridge complexity-reduction roadmap

Status: proposed

Baseline date: 2026-07-13

Scope: Broiler.HtmlBridge.Core, Broiler.HtmlBridge.Dom,
Broiler.HtmlBridge.Rendering, Broiler.HtmlBridge.Scripting, and the canonical
components that should absorb engine-neutral behavior.

Companion inventory:
[HtmlBridge current component inventory](../architecture/htmlbridge-current-component-inventory.md).

## Executive decision

Do not solve the remaining complexity by moving the whole bridge into
Broiler.Dom, Broiler.CSS, or Broiler.HTML.

The previous promotion work has already established a canonical DOM and a shared
CSS engine. The next constraint is the shape of the browser adapter itself:
Broiler.HtmlBridge.Dom is a 27,436-line partial god object which combines JavaScript
binding, browser-runtime state, host resource loading, CSSOM, event-loop behavior,
layout queries, rendering workarounds, and test compatibility. Most of those
responsibilities really are bridge responsibilities, but they should not be one
class.

The recommended end state is:

1. Keep a small, source-compatible DomBridge facade.
2. Split browser behavior into document-scoped services and feature binding
   modules inside the bridge before considering more assemblies.
3. Promote only engine-neutral algorithms and data models to Broiler.Dom,
   Broiler.Dom.Html, Broiler.CSS, Broiler.CSS.Dom, Broiler.Layout, Broiler.HTML,
   Broiler.JavaScript, or Broiler.Graphics.
4. Replace the three unrelated responsibilities in
   Broiler.HtmlBridge.Rendering, then remove that project.
5. Make resource loading, time, and layout explicit injected host services.

This sequence reduces coupling without turning the canonical DOM/CSS libraries
into a browser host or creating dozens of tiny assemblies.

## Baseline and why this work is now necessary

The measurements below describe the current working tree, excluding bin and obj.
Method counts are approximate declaration counts, so they are useful for sizing
and trend checks rather than public-API accounting.

| Project | Source files | Physical lines | Non-blank lines | Approx. methods |
|---|---:|---:|---:|---:|
| Broiler.HtmlBridge.Core | 9 | 1,175 | 987 | 50 |
| Broiler.HtmlBridge.Dom | 65 | 27,436 | 23,641 | 1,104 |
| Broiler.HtmlBridge.Rendering | 3 | 1,003 | 908 | 35 |
| Broiler.HtmlBridge.Scripting | 5 | 682 | 594 | 22 |
| **Total** | **82** | **30,296** | **26,130** | **about 1,211** |

The dominant class is DomBridge:

- It is reopened by 63 partial declarations.
- Fourteen callback files contain 409 distinct numbered Js...Core callbacks.
- Forty-one of the 65 Dom source files directly know about the JavaScript
  engine; 25 know about CSS/computed style; 11 know about resources or network
  loading; 12 parse or serialize HTML; and 8 calculate layout geometry.
- InlineStyle is touched from 24 files, GetElementRuntimeState from 25,
  GetComputedProps from 16, ToJSObject from 16, and CreateBridgeElement from 13.
  These are hidden shared-state APIs, not feature-local dependencies.

Largest individual files:

| File | Lines | Main reason for size |
|---|---:|---|
| DomBridge/LayoutMetrics.cs | 2,269 | CSSOM View, layout approximation, scrolling, rectangles, zoom |
| JsFunctionCallbacks/JsObjects.cs | 1,634 | numbered callbacks for several unrelated interfaces |
| JsFunctionCallbacks/Registration.cs | 1,516 | callback plumbing and generic dispatch |
| DomBridge/SubDocuments.cs | 1,390 | frames, documents, parsing, origin, lifecycle and resource loading |
| DomBridge/Utilities.cs | 1,373 | unrelated DOM, URL, MIME, storage, form, canvas and SVG helpers |
| DomBridge/DomBridge.cs | 1,001 | construction, attach, lifecycle, timers and global orchestration |
| DomBridge.Serialization.cs | 947 | serialization plus render-oriented transforms |
| DomBridge/StyleSheets.cs | 918 | CSSOM identity, mutation, parsing and resource loading |

The current project graph also makes a low-level binding project pull the full
image rendering stack:

    Scripting -> Dom -> Rendering -> HTML.Image
                                      -> HTML.Orchestration
                                      -> HTML.Core -> Layout

Dom currently needs Rendering primarily for geometry and compatibility helpers.
That dependency direction should be replaced by a small layout/read-model
contract.

## Complexity model

The plan treats four kinds of complexity separately. Moving a file only helps
the first kind; it does not automatically help the other three.

| Kind | Current symptom | Correct response |
|---|---|---|
| Ownership | Neutral algorithms live in a browser adapter | Promote the algorithm and its neutral tests |
| Cohesion | One partial class owns unrelated browser APIs | Extract document services and feature modules |
| Dependency | Dom reaches through Rendering to HTML.Image | Invert through narrow layout and host contracts |
| State | Canonical DOM state is shadowed by bridge dictionaries | Establish one authority and one invalidation stream |

## Target architecture

```mermaid
flowchart TD
    Host["Scripting / browser host"] --> Facade["DomBridge compatibility facade"]
    Facade --> Session["BrowserDocumentSession"]
    Session --> Bindings["Feature binding modules"]
    Session --> Loop["BrowserEventLoop"]
    Session --> Events["EventTargetRegistry / MutationObserverHub"]
    Session --> Styles["DocumentStyleContext / StyleSheetRepository"]
    Session --> Resources["IResourceLoader + origin/CSP policy"]
    Session --> Geometry["ILayoutView / GeometryFacade"]
    Bindings --> JS["Broiler.JavaScript runtime contracts"]
    Bindings --> DOM["Broiler.Dom + Broiler.Dom.Html"]
    Styles --> CSS["Broiler.CSS + Broiler.CSS.Dom"]
    Geometry --> Layout["Broiler.Layout read model"]
    LayoutHost["Broiler.HTML headless layout adapter"] -. implements .-> Geometry
    LayoutHost --> HTML["Broiler.HTML orchestration/rendering"]
```

Dependency rules:

- Broiler.Dom and Broiler.CSS must never reference HtmlBridge, a JavaScript
  engine, networking, or renderer policy.
- The bridge may depend on canonical DOM/CSS and public Layout contracts.
- The bridge must not depend on Broiler.HTML.Image.
- Broiler.HTML may implement a bridge-consumed layout interface, but the
  interface and DTOs must live below the HTML renderer.
- A browser host composes implementations. A feature callback must not construct
  HttpClient, parse a file path, or create a renderer directly.

## Ownership comparison and proposed destinations

### Canonical components

| Destination | Move here | Keep out |
|---|---|---|
| Broiler.Dom | Tree-neutral range operations, traversal, mutation records and option matching, node equality/normalization, neutral shadow-tree algorithms | JS wrappers, event-loop scheduling, URL/origin policy, computed style, geometry |
| Broiler.Dom.Html | HTML document/fragment parsing, deterministic serialization, canonical doctype/parser metadata, script-element discovery as metadata | Fetching scripts, CSP decisions, execution order, render compatibility rewrites |
| Broiler.CSS | CSS syntax and typed value models: anchor grammar, position-area/position-try values, keyframes/timing functions, CSS time and length expressions | Live CSSOM object identity, stylesheet fetching, DOM cascade, used layout values |
| Broiler.CSS.Dom | Selector matching, cascade, computed style, style scopes, tree-aware invalidation | JavaScript CSSOM wrappers, network loading, used geometry |
| Broiler.Layout | Anchor placement, position-try selection, sticky/fixed containing blocks, overflow/scroll geometry, zoomed used values, hit testing, animation sampling/application | JS conversion, document loading, renderer-specific compatibility transforms |
| Broiler.HTML | DOM-to-box projection and headless layout-session implementation; HTML rendering orchestration | Canonical DOM APIs, JS bindings, CSP and fetch policy |
| Broiler.JavaScript | ECMAScript WeakRef and FinalizationRegistry support and reusable engine-level primitives | Browser APIs such as Window, fetch, DOM events, queueMicrotask host integration |
| Broiler.Graphics | Immutable canvas display-list primitives only if commands are actually replayed | JS Canvas bindings and an unused mutable command recorder |

### Bridge and host components

| Destination | Move or keep here | Reason |
|---|---|---|
| HtmlBridge facade | Existing public construction/attach/flush/serialize entry points | Source compatibility and a single composition root |
| HtmlBridge feature bindings | JS registration, conversion, callback dispatch, CSSOM/DOM object identity | These translate browser IDL behavior into this JS engine |
| HtmlBridge document services | listeners, timers, observers, browsing contexts, top layer, JS identity, style session | Browser-runtime behavior is legitimate bridge ownership |
| Host/security layer | immutable CSP policy, URL/origin decisions, injected resource loader | Host policy should not contaminate DOM or CSS |
| WPT/CLI test support | check-layout assertions, Acid-specific transforms, path mapping and test-only shims | Test policy must not run on arbitrary production pages |

## Proposed bridge decomposition

Start inside Broiler.HtmlBridge.Dom. Assembly boundaries should follow stable
dependency boundaries later; they should not be used as the first refactoring
tool.

### Document-scoped services

| Service | Mission | Replaces or contains |
|---|---|---|
| BrowserDocumentSession | Own document, URL/origin, viewport, lifecycle and disposal | DomBridge's mutable document-wide fields |
| JsObjectRegistry | Preserve one JS wrapper identity per canonical node/object | scattered ToJSObject/CreateBridgeElement caches |
| DocumentBindingFactory | Build bindings and their narrow dependencies | generic callback registration in the facade |
| BrowserEventLoop | Own tasks, timers, intervals, RAF, microtask checkpoints and thread affinity | timer lists and drain loops in DomBridge/ScriptEngine |
| EventTargetRegistry | One listener store and dispatch path for node/window/generic targets | the current three listener stores |
| MutationObserverHub | Subscribe once to DomDocument.Mutated, filter records, queue delivery | manual notifications and registration-specific state |
| DocumentStyleContext | Own style scopes, engines, caches and invalidation | global computed-property and scope helpers |
| StyleSheetRepository | Own sheet text/rules/import state; use an injected loader | mixed CSSOM identity, parsing and fetch code |
| BrowsingContextManager | Own parent/child windows, frames, origins, ports and lifecycle | SubDocuments, SubDocumentObjects and Messaging overlap |
| GeometryFacade | Translate Layout read-model values to CSSOM View values | LayoutMetrics and SharedLayoutGeometry glue |
| ScrollController | Own scroll offsets and scrolling API behavior | ScrollRuntimeState plus geometry approximations |
| TopLayerManager | Own dialog/popover order and modal/top-layer state | dialog flags spread across anchor/runtime files |
| RenderDocumentProjector | Produce a non-destructive renderer input snapshot | live-DOM mutations in serialization/render preparation |

Every service is instance-scoped to BrowserDocumentSession. No runtime state may
remain in a process-global static ConditionalWeakTable.

### Feature binding modules

Registration and callbacks for one web-platform feature must be co-located:

- Window and lifecycle
- Document
- Node and attributes
- Element and geometry
- Traversal and Range
- Events and MutationObserver
- CSSOM and computed style
- SVG
- Forms and tables
- Dialog and popover
- Frames and browsing contexts
- Fetch and XMLHttpRequest
- Messaging
- Canvas

Each module exposes one Install(JsRealmContext) entry point and receives only the
services it uses. Replace numbered names such as JsElement123Core with semantic
names while moving them. A temporary compatibility registration table may map
old callback names to new handlers.

## Detailed delivery roadmap

### Phase 0 - stabilize the boundary and freeze a baseline

Goal: begin from a buildable public surface and prevent refactoring from being
confused with an API break.

Work:

1. Finish or revert the in-progress namespace move in the current working tree.
   At this baseline, Broiler.HtmlBridge.Scripting builds, but
   Broiler.Wpt.Tests has three CS0118 failures because
   Broiler.HtmlBridge.DomBridge is now interpreted as a namespace where callers
   expect the public DomBridge type.
2. Preserve the public full name Broiler.HtmlBridge.DomBridge as the v2
   compatibility facade. Use an internal namespace such as
   Broiler.HtmlBridge.WebApi or Broiler.HtmlBridge.Dom.Runtime; do not use
   Broiler.HtmlBridge.DomBridge as a namespace. Type forwarding cannot preserve
   a namespace rename when the full type name itself changes.
3. Capture the public API surface for Core, Dom, Rendering and Scripting.
4. Record deterministic WPT/Acid/pixel baselines and the bridge.mutation
   benchmark.
5. Add architecture tests for the dependency rules in this document.

Exit criteria:

- Broiler.slnx builds with zero errors.
- Existing v2 public names compile from a small consumer fixture.
- No canonical project references a bridge or JavaScript assembly.
- Baseline test and benchmark artifacts are committed or linked.

Suggested PRs:

- P0.1 namespace/public-surface repair.
- P0.2 API snapshot and architecture guards.
- P0.3 reproducible behavioral/performance baseline.

The recorded Phase 0 baseline (committed/linked artifacts, reproducible commands and
observed status) lives in [Phase 0 baseline](htmlbridge-phase0-baseline.md).

### Phase 1 - repair the project graph

Status: **completed** 2026-07-13 (branch `htmlbridge-phase1-project-graph`). All five
work items landed: (1) dropped the dead Rendering→Core reference; (2) inverted
Dom→HTML.Image behind a new `Broiler.Layout.ILayoutView`, with the renderer-backed
implementation relocated to the new `Broiler.HTML.Headless` submodule project and injected
into `DomBridge` via a `[ModuleInitializer]`-registered factory; (3) made the layout view
disposable, document-scoped and `(document,version,viewport,baseUrl)`-keyed; (4) collapsed
the duplicate `Broiler.Dom`/`Broiler.Graphics` nodes via overridable `$(BroilerDomPath)`/
`$(BroilerGraphicsPath)` MSBuild props plus a `scripts/check-submodule-sha-drift.sh` CI
guard; (5) narrowed the bridge Dom/Scripting projects off `Broiler.JavaScript.All`. All four
exit criteria are locked by guard tests in `HtmlBridgeArchitectureGuardTests`. The static
`DomBridge.LayoutViewFactory` seam is an intentional temporary compromise that Phase 2's
`BrowserDocumentSession` replaces with constructor injection.

Goal: make later extraction possible without dragging duplicate or high-level
projects through every test.

Work:

1. Remove the unused Rendering-to-Core reference if the API audit still shows no
   call.
2. Replace the Dom-to-HTML.Image geometry dependency with a small ILayoutView
   contract and immutable geometry DTOs. Put the contract with Broiler.Layout or
   in a dependency-neutral bridge abstraction; put the current implementation in
   Broiler.HTML.Orchestration or a small Broiler.HTML.Headless project.
3. Make SharedLayoutGeometryProvider disposable, document-scoped and
   version-aware. Its cache key must include document version, viewport and base
   URL. Do not swallow all renderer exceptions.
4. Unify duplicate root and nested Broiler.Dom/Broiler.Graphics project paths.
   Add overridable MSBuild paths for submodule-local builds, top-level overrides
   for the main solution, and a CI guard which fails if duplicate submodule SHAs
   drift.
5. Replace broad Broiler.JavaScript.All references with the smallest stable
   engine/runtime projects possible.

Exit criteria:

- Broiler.HtmlBridge.Dom no longer references Broiler.HTML.Image.
- One Broiler.Dom assembly project node and one Graphics implementation are
  present in a solution build.
- Geometry tests pass through ILayoutView.
- Dependency tests lock the new graph.

### Phase 2 - establish document services and a single state authority

Status: **P2.1 completed** 2026-07-13 (branch `htmlbridge-phase2-p2-1-lifetime-disposal`).
`DomBridge` is now `IDisposable` with a deterministic, idempotent `Dispose()` that releases
every per-session resource (layout view, timer/animation queues, listener stores, mutation
observers, ranges/iterators, message ports, JS wrapper caches; it drops — never disposes — the
borrowed `JSContext`). A shared `ClearRuntimeSessionState()` reset is called by both `Dispose()`
and `ParseHtml`, so **re-attaching now leaves no timers/listeners/observers from the prior
document** (previously they leaked — nothing cleared those maps on re-parse). The post-dispose
document/timer entry points fail fast with `ObjectDisposedException`. A minimal
`DomBridgeDisposalRegistry` (namespace `Broiler.HtmlBridge.Dom.Runtime`) is the single
lifetime/composition seam that P2.2+ grows into `BrowserDocumentSession`. Characterization +
disposal + guard tests live in `Broiler.Cli.Tests/DomBridgeSessionLifetimeTests.cs`; the public-API
snapshot baseline was regenerated (only the `Broiler.HtmlBridge.Dom` DomBridge type line changed —
Core is untouched, so `IDomBridgeRuntime` stays source-compatible and is **not** `IDisposable`).

**P2.2 completed** 2026-07-13 (same branch). JS wrapper identity now has a single authority:
`JsObjectRegistry` (namespace `Broiler.HtmlBridge.Dom.Runtime`) owns the per-node wrapper map and
the sub-document-root document-wrapper map (both reference-keyed) behind a narrow surface
(`TryGet`/`Set`/`Remove`/`Entries`/`TryGetNode`/`SetDocument`/`TryGetDocument`/`Clear`), replacing
the scattered `_jsObjectCache` and `_docRootToDocJSObject` fields at ~20 sites across
`JsObjects`/`JsFunctionCallbacks`/`Registration`/`SubDocuments*`/`ShadowDom`/`Utilities`. Behavior
is preserved; re-parse now also releases stale sub-document wrappers via one `Clear()` (observably
equivalent — the dropped keys are detached roots no lookup can reach again). No public-API change
(the registry is internal). Tests: `Broiler.Cli.Tests/JsObjectRegistryTests.cs` (registry unit
tests + wrapper-identity characterization through the bridge). The wrapper *construction* in
`ToJSObject` stays in the bridge (it needs `this` for hundreds of callbacks); only the identity
store moved. The per-document JS singletons (`_documentJSObject`/`_windowJSObject`/
`_visualViewportJSObject`) are intentionally left as fields — they are single globals, not node
identity — for a later pass.

**P2.3 completed** 2026-07-13 (same branch). Computed-style machinery now has a single authority:
`DocumentStyleContext` (namespace `Broiler.HtmlBridge.Dom.Runtime`) owns the per-document-root
`CssStyleEngine` scopes (and the `ComputedStyleEngineScope` type), the `GetComputedProps` memo (cache
+ re-entrancy in-progress map), and the style-invalidation batch state — replacing the five scattered
bridge fields (`_computedStyleEngines`, `_computedPropsCache`, `_computedPropsInProgress`,
`_styleInvalidationBatchDepth`, `_pendingStyleInvalidationRoots`). There is now one invalidation
route, `DocumentStyleContext.InvalidateComputedStyle()`, which clears the memo *and* the engines'
cascade/computed caches together (they must invalidate as one because `GetComputedProps` reads inline
style from the live ElementRuntimeState map, invisible to the engine's own DOM-mutation subscription).
The bridge keeps the algorithms that need the DOM/loading (engine construction via
`GetSyncedScopedEngine`, `<style>`/`<link>` collection, the recursive scope walk) and calls into the
context for storage; no back-reference. Behavior-preserving; no public-API change (internal). Tests:
`Broiler.Cli.Tests/DocumentStyleContextTests.cs` (memo/engine-scope/batch unit tests + a
class-change → `getComputedStyle` invalidation characterization through the bridge). Net −53 lines in
the bridge partials.

**P2.4 completed** 2026-07-13 (same branch). The document's task queues now have a single owner:
`BrowserEventLoop` (namespace `Broiler.HtmlBridge.Dom.Runtime`) owns the `setTimeout`/`setInterval`
callback maps, the `requestAnimationFrame` map, the internal frame-action queue, their id counters
and the cleared-timer set — plus the drain itself (`DrainStep`/`DrainAll`). It replaces the eight
scattered bridge fields and the ~90-line `FlushTimerStep` body. Registration
(`setTimeout`/`clearTimeout`/`setInterval`/`clearInterval`/`requestAnimationFrame`/
`cancelAnimationFrame`) and `QueueFrameAction` delegate to it; `DomBridge.FlushTimers`/
`FlushTimerStep`/`HasPendingTimers` are now thin wrappers (still guarded by `ThrowIfDisposed`, and the
per-task `TaskCheckpointCallback` is passed into the drain). The incidental reuse of the frame-action
counter to mint smooth-scroll tokens is gone — smooth-scroll tokens get their own bridge-local
counter (observably equivalent: the two were independent namespaces). Behavior-preserving; no
public-API change (the loop is internal, and the public timer methods keep their signatures). Tests:
`Broiler.Cli.Tests/BrowserEventLoopTests.cs` (registration/cancellation/drain/checkpoint/error-isolation
unit tests + a timer-flush characterization through the bridge; existing
`ScriptEngineExecuteTests.DomBridge_FlushTimerStep_*` still pass). Net −147 lines in the bridge
partials. The loop is also the seam for the still-pending single-owner thread-affinity model (Phase 2
item 5); today it preserves the existing defensive concurrent collections.

**P2.5 completed** 2026-07-13 (same branch). Listeners and observers now have single owners, both in
namespace `Broiler.HtmlBridge.Dom.Runtime`:

- `EventTargetRegistry` owns the per-node `addEventListener` listeners, the window listeners, the
  generic JS-target (message-port / sub-window) listeners, the target→owner-window map, and the
  visual-viewport `scroll` listeners — replacing four scattered bridge fields plus the node-listener
  store that used to live on the process-global `ElementRuntimeState`. Node listeners now use an
  **instance-scoped `ConditionalWeakTable`**, keeping the same weak GC semantics while removing them
  from the static table (partial progress on Phase-2 item 4). `ElementRuntimeState.EventListeners` is
  deleted; only inline `on*` handlers remain node-runtime state there. The dispatch algorithms stay in
  the bridge (`FireListeners` became an instance method) and read/write listeners through the registry.
- `MutationObserverHub` owns the registered observer list — `Register` (with `observe()` replace
  semantics), `Unregister` (`disconnect`), `Count`, `Snapshot`, `Clear`. Registration
  (`Common.cs`), the three delivery loops (`Traversal.cs`) and teardown route through it; the bridge
  still builds and delivers the JS mutation records.

Behavior-preserving; no public-API change (both are internal; only private helpers changed static→
instance). Tests: `Broiler.Cli.Tests/EventTargetRegistryTests.cs` +
`Broiler.Cli.Tests/MutationObserverHubTests.cs` (unit tests + element/window listener characterization;
the existing `DomEventsEdgeCaseTests`, messaging and MutationObserver suites still pass). Full-suite
regression check vs the P2.4 baseline: zero regressions.

**P2.6 completed** 2026-07-13 (same branch) — **Phase 2 complete.** Two owners, both in
`Broiler.HtmlBridge.Dom.Runtime`:

- `MessagePortRegistry` owns the `MessageChannel`/`MessagePort` state — entangled peers, closed and
  started marks, and the per-port queue of pending messages — replacing the four scattered port maps.
  The messaging callbacks still build/dispatch the JS `MessageEvent`s; they read/mutate port state
  through it (`Link`/`TryGetPeer`/`HasPeer`/`IsClosed`/`Close`/`IsStarted`/`Start`/`Enqueue`/
  `TakeQueued`/`Clear`).
- `ResourceLoader` owns the host resource I/O — a process-shared `HttpClient` (kept static inside the
  loader so many documents don't each open a socket pool) and the optional local base path — replacing
  the static `SharedHttpClient` that feature callbacks reached into directly. This is the "no feature
  callback constructs an `HttpClient`" seam Phase 7 builds on (CSP, unified fetch/XHR/frame routing,
  cancellation are still to come). `FetchExternalStylesheet` went static→instance.

Behavior-preserving; no public-API change (both internal). Tests:
`Broiler.Cli.Tests/MessagePortRegistryTests.cs` + `Broiler.Cli.Tests/ResourceLoaderTests.cs` (unit
tests + existing messaging/network suites pass). Full-suite regression check vs the P2.5 baseline:
every candidate fails identically in isolation on both sides → zero regressions.

**Deferred within "browsing-context state" (a follow-up, not blocking Phase 3):** the sub-window and
sub-document content caches in `SubDocuments.cs` (`_subWindowCache`/`_subWindowContainers`,
`_subDocumentCache`, `_subDocumentLocationCache`, `_subDocumentBaseUrlCache`, `_objectLoadFailures`,
`_onloadFired`) and `_currentWindowOverride` are not yet consolidated into a `BrowsingContextManager`
— they are largely internal to `SubDocuments.cs` and intertwined with sub-document resolution. P2.6
took the cross-file cohesive slice (ports) and the resource-loader seam.

## Phase 2 outcome

All six sub-PRs landed on branch `htmlbridge-phase2-p2-1-lifetime-disposal`: P2.1 disposal/lifetime,
P2.2 `JsObjectRegistry`, P2.3 `DocumentStyleContext`, P2.4 `BrowserEventLoop`, P2.5
`EventTargetRegistry`+`MutationObserverHub`, P2.6 `MessagePortRegistry`+`ResourceLoader`. Hidden
bridge state now has explicit single owners (all internal, in `Broiler.HtmlBridge.Dom.Runtime`); node
event listeners were de-globalized off the process-static `ElementRuntimeState` onto an instance
`ConditionalWeakTable`. Not fully met and carried forward: two simultaneous sessions are still not
isolated (blocked at the Broiler.JS engine's shared globals — a JS-engine concern, not the bridge),
and the remaining process-static `ElementRuntimeState`/`PositionAreaResolutions` tables plus the
sub-document caches above are still to be de-globalized/consolidated.

Two findings recorded for later phases:

- **The "two *simultaneous* sessions are isolated" exit criterion is blocked below the bridge.**
  Two live `JSContext` instances currently share global state at the Broiler.JS engine layer (the
  last-created context's globals win), so simultaneous-session isolation cannot be delivered by a
  bridge-only change. The supported model today is one active session per thread; the bridge
  guarantees *sequential* re-attach isolation. Full simultaneous isolation needs JS-engine work
  (out of this roadmap's scope).
- **De-globalizing the process-static per-element runtime tables** (`ElementRuntimeStates`,
  `PositionAreaResolutions`) is deferred: it is a 155-call-site / 24-file cascade through the
  project's ~284 static helpers, and the tables are weak + node-keyed (they GC with the session's
  nodes, so they do not leak or cross sessions today). Its own later PR under item 4.

Goal: make hidden state dependencies explicit while preserving behavior.

Work:

1. Introduce BrowserDocumentSession and move construction/disposal into it.
2. Extract JsObjectRegistry, DocumentStyleContext, BrowserEventLoop,
   EventTargetRegistry, MutationObserverHub and an injected IResourceLoader.
3. Split ElementRuntimeState by concern:
   listener, form control, scroll, dialog/top layer, shadow root, stylesheet,
   document, animation and doctype state.
4. Remove process-global runtime state. Attach/reparse/dispose must release every
   timer, listener, observer, browsing context, layout snapshot and JS wrapper.
5. Define a single-owner event-loop/threading model. Concurrent collections are
   not a substitute for document thread affinity.
6. Route all computed-style cache clears through DocumentStyleContext and all
   mutations through DomDocument.Mutated.

Exit criteria:

- Two simultaneous sessions cannot see each other's nodes, listeners, timers,
  styles or storage.
- Re-attaching a DomBridge leaves no state from the prior document.
- There is one mutation stream, one event dispatcher, one style invalidation
  route and one resource loader.
- DomBridge's fields are primarily facade/session references, not feature state.

Suggested PR order:

- P2.1 session lifetime and disposal characterization. **(done — see Status above)**
- P2.2 JS identity registry. **(done — see Status above)**
- P2.3 style context and invalidation. **(done — see Status above)**
- P2.4 event loop. **(done — see Status above)**
- P2.5 listeners/observers. **(done — see Status above)**
- P2.6 resource loader and browsing-context state. **(done — see Status above; Phase 2 complete)**

### Phase 3 - replace the partial god object with feature modules

Status: **P3.1 completed** 2026-07-13 (branch `htmlbridge-phase3-traversal-module`). The DOM
traversal / Range vertical slice is the first co-located feature binding module:
`TraversalBinding` (namespace `Broiler.HtmlBridge.Dom.Features`) now owns `TreeWalker`,
`NodeIterator`, `Range`, the `NodeFilter` machinery and `document.createComment` — its registration
(`RegisterDocumentApis`), every handler (renamed from the numbered `JsTraversal…020…039Core` to
semantic `Range*`/`Create*` names) and the traversal-scoped state (the weak active-range and
active-node-iterator registries) live together in one file. The module depends only on the narrow
`ITraversalHost` contract (JS-wrapper identity, node lookup, boundary/geometry helpers still in the
bridge pending Phase 5, and the range-scoped node-construction seams) which `DomBridge` implements
via **explicit interface members** in `DomBridge.TraversalHost.cs` — so no handler reaches an
arbitrary bridge private field and the public surface is unchanged. `DomBridge`'s traversal
partials are now thin: the old `JsFunctionCallbacks/Traversal.cs` is deleted; `Traversal.cs` keeps
only the mutation-observer notification machinery and range client-rect geometry plus three
one-line `Build*` delegators; `Registration/Traversal.cs` is a single delegating call; the
`_activeRanges`/`_activeNodeIterators` fields moved off the bridge. Neutral static DOM-tree helpers
the module shares (`IsText`/`IsComment`/`ParentEl`/`ChildAt`/`ChildIndexOf`/`ChildElements`/
`GetNodeType`/`GetDocumentOrderNodes`/`CollectTextContent`/`IsDescendant`/`FindCommonAncestor`/
`GetNodesInRange`/`ThrowDOMException`) were widened `private static`→`internal static` in place
(no behaviour/API change; Phase 4 promotes them to Broiler.Dom). Behaviour-preserving; no
public-API change (both the module and the contract are internal). Tests:
`Broiler.Cli.Tests/TraversalBindingModuleTests.cs` (co-location/host-contract/state-moved guards +
Range/TreeWalker/createComment characterizations). Regression check vs the P2.6 baseline: the
existing traversal, mutation-observer, events and messaging suites pass unchanged; the pre-existing
environmental/known failures (`Range_GetBoundingClientRect_Includes_DisplayContents_Descendants`
headless-geometry, the six Acid3 pixel/cascade/border/NodeIterator-pre-removal tests, and the two
`:root`/`:lang` selector tests) fail identically on both sides → zero regressions.

Status: **P3.2 completed** 2026-07-13 (same branch). The **MutationObserver** feature (the
Events-and-MutationObserver pair's observer half) is the second co-located module:
`MutationObserverBinding` (namespace `Broiler.HtmlBridge.Dom.Features`) now **owns** the P2.5
`MutationObserverHub` state authority and co-locates the whole feature — the JS `MutationObserver`
polyfill + its `__broilerRegister/UnregisterMutationObserver` host functions, the
`observe()`/`disconnect()` callbacks (was `JsRegistrationBroiler…034/035Core`), the option parsing
(`CreateMutationObserverOptions`/`GetMutationObserverOption`, moved out of `Common.cs`), and the
childList/attribute/characterData record delivery (`Deliver…`, moved out of `Traversal.cs`). It
depends only on the narrow `IMutationObserverHost` contract (`ToJSObject` + `FindDomNodeByJSObject`),
which `DomBridge` implements via explicit interface members in `DomBridge.MutationObserverHost.cs`.
The bridge keeps three same-name `Notify…MutationObservers` delegators so the ~7 mutation-path call
sites in `Traversal.cs`/`Attributes.cs`/`JsObjects.cs` are untouched; `RegisterDocumentEventsAnd
MutationObservers` now registers only the typed `Event` constructors and delegates the observer
install; lifetime reset calls `_mutations.Clear()`. This also finished the P3.1 `Traversal.cs`
cleanup (the mutation-observer machinery it had temporarily retained is gone). Behaviour-preserving;
no public-API change (module + contract internal). Tests:
`Broiler.Cli.Tests/MutationObserverBindingModuleTests.cs` (co-location/host-contract/hub-ownership
guards + childList/attribute-oldValue/disconnect characterizations). Regression check: the
MutationObserver, DomEvents, Attributes, Traversal, Messaging and architecture-guard suites pass
unchanged; same pre-existing/known failures as above → zero regressions.

Status: **P3.3 completed** 2026-07-13 (same branch). The **event dispatch engine** — the highest-
coupling core of the Events feature — is the third co-located module: `EventDispatchBinding`
(namespace `Broiler.HtmlBridge.Dom.Features`) owns the capture → target → bubble propagation
algorithm (`DispatchEventOnElement`), the per-element listener firing (`FireListeners`, which had no
external callers), the event object's propagation-control methods (`stopPropagation`/
`stopImmediatePropagation`/`preventDefault`/`cancelBubble`/`returnValue`, renamed from the numbered
`JsEvents…001…007Core`) and `composedPath()`. It reads what it dispatches through the narrow
`IEventDispatchHost` contract (`ToJSObject`, `DocumentNode`, `DocumentJSObject`, `WindowJSObject`,
`GetEventListeners`, `GetInlineEventHandlers`), implemented by explicit interface members in
`DomBridge.EventDispatchHost.cs`. **Deliberately kept in the bridge** (different concerns, not
dispatch): the `addEventListener`/`removeEventListener` *registration* helpers
(`CreateEventListenerRegistration`/`GetCaptureForRemoval`/`HasMatchingEventListener`) that the four
registration sites use, inline-handler *compilation* (`CompileInlineEventAttribute(s)`), form
validity checks, and the shared `InvokeEventListener` (widened to `internal static` — also used by
the window/submit/messaging firing paths, which the module calls as `DomBridge.InvokeEventListener`).
The bridge keeps a same-name `DispatchEventOnElement` delegator so the ~five caller files
(`JsObjects`/`Registration`/`LayoutMetrics`/`SubDocuments`/`DomBridge.cs`) are untouched; the emptied
`JsFunctionCallbacks/Events.cs` was deleted. Behaviour-preserving; no public-API change (module +
contract internal). Tests: `Broiler.Cli.Tests/EventDispatchBindingModuleTests.cs` (co-location/
host-contract guards + capture/target/bubble ordering, stopPropagation and preventDefault
characterizations). Regression check: DomEvents (81), DomEventsEdgeCase (33), Acid3RegressionTests
(26), Attributes, MutationObserver, Messaging and architecture-guard suites pass unchanged → zero
regressions.

Status: **P3.4 completed** 2026-07-13 (same branch) — the Events feature's listener half, completing
Events alongside P3.3's dispatch half. The `addEventListener`/`removeEventListener` *registration
semantics* (option parsing for capture/once/passive, the DOM duplicate-registration check, and
match-by-listener-and-capture removal) are now one co-located helper, `EventListenerBinding`
(namespace `Broiler.HtmlBridge.Dom.Features`), exposing two storage-agnostic operations —
`AddListener(list, listener, options)` and `RemoveListener(list?, listener, options)` — plus the four
former bridge helpers (`CreateEventListenerRegistration`/`GetCaptureForRemoval`/
`HasMatchingEventListener`/`GetBooleanOption`) now internal to it. It is stateless with **no host
contract**: each of the four target callbacks (element in `JsObjects`, document + window in
`Registration`, message-port in `Messaging`) resolves its own listener list from the P2.5
`EventTargetRegistry` and calls the shared operations, replacing the identical ~15-line add/remove
block that had been copied across those four feature files. Behaviour-preserving; no public-API
change (the helper is internal). Tests: `Broiler.Cli.Tests/EventListenerBindingModuleTests.cs`
(co-location guard + dedup / capture-scoped-removal / once characterizations). Regression check:
DomEvents (81), DomEventsEdgeCase (33), Messaging (15), Attributes and the event/architecture-guard
suites pass unchanged → zero regressions.

Status: **P3.5 completed** 2026-07-13 (same branch). The **HTML table DOM interfaces** are the fifth
co-located module: `TableBinding` (namespace `Broiler.HtmlBridge.Dom.Features`) owns the whole
`HTMLTableElement` interface (`caption`/`tHead`/`tFoot`/`tBodies`/`rows` plus `createCaption`/
`createTHead`/`createTFoot`/`deleteCaption`/`deleteTHead`/`deleteTFoot`/`insertRow`/`deleteRow`),
`HTMLTableSectionElement` (`rows`/`insertRow`) and `HTMLTableRowElement` (`rowIndex`/
`sectionRowIndex`/`cells`/`insertCell`/`deleteCell`) — ~20 callbacks (renamed from the numbered
`JsElementInterfaces…001…023Core`) plus `BuildTableRows` and the `insertRow` placement algorithm,
moved out of `JsFunctionCallbacks/ElementInterfaces.cs` and `Utilities.cs`. The table registration in
`AddElementSpecificMembers` collapsed to a single `_tables.Install(obj, element, tag)` call. Table
DOM is pure canonical-tree manipulation, so the `ITableHost` contract is just two seams (`ToJSObject`
+ `CreateElement`, the construction funnel), implemented via explicit interface members in
`DomBridge.TableHost.cs`; everything structural uses the neutral static `DomBridge` tree helpers
(`SetParent`/`InsertChildAt`/`RemoveChildFrom`/`IsTableCellElement`/`UndefinedFunction` widened
`private`→`internal static`). `CollectTableRows` stayed a bridge `internal static` helper because hit
testing also uses it. Behaviour-preserving; no public-API change (module + contract internal).
Tests: `Broiler.Cli.Tests/TableBindingModuleTests.cs` (co-location/host-contract guards + insertRow/
insertCell/rows-spec-order/createTHead-idempotence/deleteRow characterizations). Regression check:
HtmlDomInterface (49), FormControlRender, Acid3RegressionTests (26) and the architecture-guard suites
pass unchanged; the one pre-existing environmental failure
(`FormControlRenderTests.SelectListBox_SizingAndScrolling_Follow_WritingMode`, a `<select>` layout
test) fails identically on both sides → zero regressions.

Status: **P3.6 completed** 2026-07-13 (same branch). The **`Element.classList` / `DOMTokenList`**
API is the sixth co-located module: `ClassListBinding` (namespace `Broiler.HtmlBridge.Dom.Features`)
owns `Build(element, onClassChanged)` plus the `contains`/`add`/`remove`/`toggle`/`replace`
operations (renamed from the bridge's `BuildClassListObject` + the scattered `JsUtilities…025…Core`
callbacks). It is the cleanest slice so far — pure logic over the canonical `Broiler.Dom.DomTokenList`
with an injected `Action<DomElement>` style-invalidation callback, so it is an **internal static
class with no host contract at all**. The registration site (`JsObjects.cs`) calls
`ClassListBinding.Build(element, bridge.InvalidateStyleScope)`. Behaviour-preserving; no public-API
change. Tests: `Broiler.Cli.Tests/ClassListBindingModuleTests.cs` (co-location guard +
add/remove/contains, toggle-with/without-force, replace characterizations). Regression check:
SelectorsAndCssom (only the two known-baseline `:root`/`:lang` fails, unchanged) and the
architecture-guard suites pass → zero regressions.

Status: **P3.7 completed** 2026-07-13 (same branch) — the first *runtime-state-coupled* feature
extracted, establishing the narrow-named-accessor pattern for the entangled remainder. The **dialog
/ popover / details JS API** is the seventh co-located module: `DialogBinding` (namespace
`Broiler.HtmlBridge.Dom.Features`) owns `HTMLDialogElement` (`showModal`/`show`/`close`/`open`/
`returnValue`), the popover API (`showPopover`/`hidePopover` on any element with the global
`popover` attribute) and `HTMLDetailsElement.open` — 8 callbacks (renamed from the numbered
`JsElementInterfaces…029…036Core`; the identical details/dialog `open` setters deduplicated) plus
the three registration blocks in `AddElementSpecificMembers`, now one
`_dialogs.Install(obj, element, tag, hasPopover)` call. Its runtime state
(`ElementRuntimeState.Dialog.{Modal,PopoverOpen,TopLayerOrder}`, `FormControl.ReturnValue`, the
top-layer counter) is reached through the narrow `IDialogHost` contract as **named primitives**
(`SetOpenAttribute`/`HasOpenAttribute`/`InvalidateStyleScope`/`AssignNextTopLayerOrder`/
`SetDialogModal`/`SetPopoverOpen`/`Get`/`SetReturnValue`/`PopoverKeepsOverlayOnHide`), implemented
via explicit interface members in `DomBridge.DialogHost.cs` — the module never touches the
runtime-state object, and these accessors are the single seam a future `TopLayerManager` re-homes.
The backdrop/top-layer **rendering** stays in the bridge's anchor resolver. Behaviour-preserving; no
public-API change (module + contract internal). Tests:
`Broiler.Cli.Tests/DialogBindingModuleTests.cs` (co-location/host-contract guards +
showModal/close/returnValue, dialog.open-setter, details.open characterizations). Regression check:
Dialog, Popover, Overlay, Backdrop, HtmlDomInterface (49), Acid3RegressionTests (26) and the
architecture-guard suites pass unchanged → zero regressions (the renderer reads the same runtime
state the module now writes).

Status: **P3.8 completed** 2026-07-13 (same branch) — the second runtime-state-coupled feature, via
the P3.7 named-accessor pattern. The **HTMLSelectElement / HTMLOptionElement** interface is the
eighth co-located module: `SelectBinding` (namespace `Broiler.HtmlBridge.Dom.Features`) owns
`select.add`/`options`/`selectedIndex`/`size` plus the option-collection, selected-index and value
algorithms (`CollectSelectOptions`/`GetSelectedIndex`/`SetSelectedIndex`/`GetValue`/`SetValue`,
**relocated out of `LayoutMetrics.cs`** where they lived but were never used by layout) and
`option.defaultSelected` — 6 callbacks (renamed from the numbered `JsElementInterfaces…037…045Core`).
The select + option registration blocks in `AddElementSpecificMembers` collapsed to one
`_select.Install(obj, element, tag)` call; the shared `value` form-control handler in `JsObjects.cs`
keeps its input/textarea branches and delegates only its select branch to `_select.GetValue`/
`SetValue`. The per-element form-control state (the select's dirty selected index, an option's IDL
value and default-selected flag on `ElementRuntimeState.FormControl`) is reached through the narrow
`ISelectHost` contract as named primitives (`TryGetSelectedIndex`/`SetSelectedIndex`/
`TryGetOptionValue`/`Get`/`SetOptionDefaultSelected`) plus `ToJSObject`/`FindDomElementByJSObject`,
implemented via explicit interface members in `DomBridge.SelectHost.cs`; the module never touches the
runtime-state object. Neutral attribute helpers `HasAttr`/`TryGetAttribute`/`SetAttr`/`RemoveAttr`/
`GetElementTextContent` were widened `private`→`internal static`. Behaviour-preserving; no public-API
change (module + contract internal). Tests: `Broiler.Cli.Tests/SelectBindingModuleTests.cs`
(co-location/host-contract guards + options/default-selected-index, selectedIndex-setter,
value-setter, add/size characterizations). Regression check: HtmlDomInterface (49), FormControlRender
and the architecture-guard suites pass unchanged; the one pre-existing environmental failure
(`FormControlRenderTests.SelectListBox_SizingAndScrolling_Follow_WritingMode`, a `<select>` layout
test) fails identically on both sides → zero regressions.

Status: **P3.9 completed** 2026-07-13 (same branch). The **HTMLFormElement** interface is the ninth
co-located module: `FormBinding` (namespace `Broiler.HtmlBridge.Dom.Features`) owns `form.elements`
(an `HTMLFormControlsCollection` with numeric **and named** access), `form.length`, `form.action`,
and the constraint-validation checks (`checkValidity`/`reportValidity`, whose logic moved out of
`Events.cs` — completing that file's de-form-ing). The bridge's `FormElementsCollection` (a JSObject
subclass with a named-lookup override) moved into the module as a nested type; its sole bridge
coupling — the `DomBridge` back-reference it carried only to wrap a control as a JS object — is
replaced by the narrow `IFormHost` contract (`ToJSObject`), implemented via one explicit interface
member in `DomBridge.FormHost.cs`. Everything else (control collection, validity) is pure
tree/attribute work over the already-`internal static` `DomBridge.CollectFormControls`/`HasAttr`/
`TryGetAttribute`/`ChildElements`/`IsText`, so no new widening was needed. The form registration block
in `AddElementSpecificMembers` collapsed to one `_forms.Install(obj, element, tag)` call; the
`checkValidity`/`reportValidity` registration on form-associated elements in `JsObjects.cs` now calls
`_forms.IsElementValid(element)`. Behaviour-preserving; no public-API change (module + contract
internal). Tests: `Broiler.Cli.Tests/FormBindingModuleTests.cs` (co-location/host-contract guards +
elements indexed/named/length, action get/set, checkValidity characterizations). Regression check:
HtmlDomInterface (49), FormControlRender and the architecture-guard suites pass unchanged; the one
pre-existing environmental `<select>` layout failure reproduces identically → zero regressions.

Status: **P3.10 completed** 2026-07-13 (same branch) — the first *browsing-context-coupled* feature.
The **web-messaging** feature is the tenth co-located module: `MessagingBinding` (namespace
`Broiler.HtmlBridge.Dom.Features`) owns `window.postMessage`, `MessageChannel`/`MessagePort` (creation,
`postMessage`, `start`/`close`/`onmessage`, the per-port pending-message queue), the structured-clone
+ transfer-list handling and `MessageEvent` construction — and it **owns** the P2.6
`MessagePortRegistry` state authority (entangled peers, closed/started marks, queued messages), the way
P3.2 took over the P2.5 hub. It also owns the generic `EventTarget` dispatch
(`addEventListener`/`removeEventListener`/`dispatchEvent` with the propagation-control methods) that is
installed on message ports **and** sub-windows — the two non-node event targets — co-located here (its
listeners already come from the shared `EventTargetRegistry`) pending a future dedicated generic-
EventTarget/Window module; sub-window installation goes through the module's public
`InstallEventTargetApi`. All the callbacks were renamed from the numbered `JsMessaging…001…017Core` to
semantic names. The module holds a reference to the shared `EventTargetRegistry` (generic-target
listeners + owner-window map, which it does **not** own) and reaches the document's browsing-context
operations — current/owner-window resolution, the window-context switch, top-window dispatch and
frame-action queueing — through the narrow `IMessagingHost` contract, implemented via explicit
interface members in `DomBridge.MessagingHost.cs`. The window-resolution / window-context-switch
cluster itself (`ResolveCurrentWindow`/`ResolveOwnerWindow`/`GetCanonicalWindow`/`RunWithWindowContext`/
`GetWindowDocument`/`GetWindowParent`) is genuine browsing-context infrastructure entangled with the
sub-window/sub-document caches Phase 2 deferred, so it was **relocated (not moved into the module)**
into a new bridge partial `DomBridge.WindowContext.cs` — bridge-owned pending a future
`BrowsingContextManager`, and still called directly by `SubDocuments.cs`. The three external call sites
(`Registration/Window.cs` window messaging, `Registration/Fetch.cs` `MessageChannel` constructor,
`SubDocuments.cs` sub-window EventTarget install) now go through the module; lifetime reset calls
`_messaging.ClearPorts()`. The old `DomBridge/Messaging.cs` + `JsFunctionCallbacks/Messaging.cs` were
deleted. Behaviour-preserving; no public-API change (module + contract internal). Tests:
`Broiler.Cli.Tests/MessagingBindingModuleTests.cs` (co-location / host-contract / registry-ownership
guards + MessageChannel port round-trip, queue-until-onmessage, and async window-postMessage
characterizations). Regression check vs the P3.9 baseline: WebMessaging (existing), MessagePortRegistry,
DomEvents (81), DomEventsEdgeCase, MutationObserver, EventDispatch, EventListener, Attributes and the
architecture-guard suites all pass unchanged (164 tests); the pre-existing environmental iframe/sub-
document HTTP failures (`HttpSubResourceTests.Iframe_*`, `ScriptEngineExecuteTests.…Iframe_Scroll_State
_In_SrcDoc`) fail identically on both sides → zero regressions.

Status: **P3.11 completed** 2026-07-13 (same branch) — the networking feature. **`fetch` /
`XMLHttpRequest`** is the eleventh co-located module: `FetchBinding` (namespace
`Broiler.HtmlBridge.Dom.Features`, split into `FetchBinding.cs` / `FetchBinding.Callbacks.cs` /
`FetchBinding.Xhr.cs` to stay under the 750-line/file guideline) owns the whole `fetch` polyfill and
its `Headers`/`Request`/`Response`/`FormData`/`Blob`/`AbortController` helper objects, the `Response`
static factories (`new Response`/`Response.json`/`Response.redirect`) and the `XMLHttpRequest` polyfill
— i.e. `RegisterFetchAndHttpApis` (now `Install`), the four `JsRegistration…113/114/116/120Core`
callbacks (moved out of the shared 1516-line `JsFunctionCallbacks/Registration.cs`), the four fetch
delegate types (moved out of `JsFunctionCallbacks/Common.cs`) and `RegisterXMLHttpRequest`. Host I/O
goes through the injected **P2.6 `ResourceLoader`** — the module holds a reference to it (passed in
`new FetchBinding(this, _resources)`), so no feature callback constructs an `HttpClient` (the seam
Phase 7 builds on). The **only** remaining bridge coupling — the page URL used to resolve a relative
`Response.redirect` target — is the narrow `IFetchHost.PageUrl`, implemented via one explicit interface
member in `DomBridge.FetchHost.cs`. **Two non-networking registrations that historically lived inside
`RegisterFetchAndHttpApis` were relocated** to the window-globals site (`Registration/Registration.cs`):
`MessageChannel` (messaging — delegates to `_messaging.CreateMessageChannel()`) and `getComputedStyle`
(CSSOM — still calls the bridge's `JsRegistrationGetComputedStyle121Core`). The caller now does
`var fetchFn = _fetch.Install(context, window)`; the old `Registration/Fetch.cs` and
`Registration/XmlHttpRequest.cs` were deleted. Behaviour-preserving; no public-API change (module +
contract internal). Tests: `Broiler.Cli.Tests/FetchBindingModuleTests.cs` (co-location / host-contract /
ResourceLoader-ownership guards + Response/Response.json, Headers/FormData, XHR-installed and
relocated-MessageChannel/getComputedStyle characterizations, all network-free). Regression check vs the
P3.10 baseline: the network/computed-style/messaging/selector suites pass unchanged (286 tests); the
pre-existing environmental failures (the 8 `HttpClientMigrationTests` assembly-reflection checks, the 3
`HttpSubResourceTests.Iframe_*`, the 2 `NetworkAndHttpTests.Fetch_*Body_Readers` that need real HTTP,
and the 2 `SelectorsAndCssomTests` `:root`/`:lang`) fail identically on both sides → zero regressions.

Status: **P3.12 completed** 2026-07-13 (same branch) — the DOM-attributes feature. **Node/attributes**
is the twelfth co-located module: `AttributesBinding` (namespace `Broiler.HtmlBridge.Dom.Features`) owns
the attribute object model — the `element.attributes` `NamedNodeMap` (`BuildNamedNodeMap` + the eight
`getNamedItem`/`setNamedItem`/`removeNamedItem`/`item`/NS callbacks, renamed from the numbered
`JsAttributes…002…009Core`) and the `Attr` node construction (`BuildAttrNode`/`BuildStandaloneAttrNode`/
`BuildAttrNodeCore`/`TryGetAttachedAttrNamespace`/`GetAttrNode{Name,LocalName,Namespace}`) — **and the
attribute write path** (`SetAttributeLikeSetAttribute`/`…NS` + `RemoveAttributeLikeRemoveAttribute`/`…NS`),
which applies the change to the canonical attribute set and then coordinates the cross-cutting side
effects through the narrow `IAttributesHost` contract: `ApplyStyleAttribute` (re-parse the `style`
attribute into inline style + invalidate), `CompileInlineEventAttribute` (an `on*` handler),
`InvalidateStyleScope`, and `NotifyAttributeMutationObservers`. Those seams are implemented via explicit
interface members in `DomBridge.AttributesHost.cs`, so the public surface is unchanged. The element's own
`getAttribute`/`setAttribute`/… methods stay registered among the other element members in the bridge but
now **delegate their write and Attr-node construction to `_attributes`** (the module both consumes the
write hub and provides Attr-node construction back to those element callbacks + `document.createAttribute`).
The low-level, engine-neutral attribute scans (`TryGetAttribute`/`SetAttr`/`RemoveAttr`/`AttributeNames`/
`GetAttr`/`TryGetNsAttribute`) stay shared `internal static` helpers on `DomBridge` — used by many other
modules — and are called qualified (`GetAttr`/`AttributeNames`/`TryGetNsAttribute` widened
`private`→`internal static`); Phase 4 promotes them to Broiler.Dom. The document-query collectors
(`CollectByTagName`/`CollectLinksInTreeOrder`/…) and `AttributeSnapshot`/`RestoreAttributes` stay in the
bridge (not attributes-feature). The old `JsFunctionCallbacks/Attributes.cs` was deleted. Behaviour-
preserving; no public-API change (module + contract internal). Tests:
`Broiler.Cli.Tests/AttributesBindingModuleTests.cs` (co-location / host-contract guards + set/get/remove/
hasAttribute round-trip, NamedNodeMap + Attr node, style-attribute→inline-style, and attribute
MutationObserver characterizations). Regression check vs the P3.11 baseline: the attribute,
MutationObserver, HtmlDomInterface and namespace suites pass unchanged (140 tests, 0 failures); the
pre-existing environmental failures (the three `ScriptEngineExecuteTests` zoom/iframe serialization tests
and the two `SelectorsAndCssomTests` `:root`/`:lang`) fail identically on both sides → zero regressions.

Status: **P3.13 completed** 2026-07-14 (branch `htmlbridge-phase3-subdocument-module`) — the first
**browsing-context feature** slice, and the one **Phase 4 (P4.4b) unblocked.** The nested-browsing-context
**`document` object surface** is the thirteenth co-located module: `SubDocumentBinding` (namespace
`Broiler.HtmlBridge.Dom.Features`, split into `SubDocumentBinding.cs` / `.Nodes.cs` / `.Implementation.cs`
/ `.Events.cs` to stay under the 750-line/file guideline) owns `BuildDocument` (was `BuildSubDocument`)
and every `document`-object callback it wires — documentElement/body/head/title/forms/childNodes,
getElementById/getElementsByTagName/querySelector(All)/elementFromPoint(s), createElement/TextNode/
Comment/ElementNS, the legacy `createEvent` + `initEvent`/`initMouseEvent`/… mutator family,
open/write, images/links/styleSheets, appendChild/removeChild/append/prepend, `document.implementation`
(createDocumentType/createDocument/createHTMLDocument) and createTreeWalker/NodeIterator/Range — the
~35 numbered `JsSubDocumentObjects…003…052Core` callbacks renamed to semantic names and moved out of the
755-line `JsFunctionCallbacks/SubDocumentObjects.cs` (deleted) plus `BuildSubDocument` out of
`SubDocumentObjects.cs` (which now retains only the shared `FindInSubTree`/`FindInTree` tree-search
helpers, widened `private`→`internal static` because the **main** document's getElementById
(`Registration.cs`) and `LayoutMetrics` also call them).

**Why this slice, and why now:** P4.4b severed the `#subdoc-root` sentinel, so a sub-document root is a
canonical `DomNode`/`DomDocument` and the whole surface operates cleanly over a `DomNode docRoot`. The
sub-document *document* object is the largest self-contained JS surface of the frames feature; the
browsing-context **infrastructure** (the `_subDocumentCache`/`_subWindowCache`/content-document maps, the
`GetOrCreateSubDocument`/`GetOrCreateSubWindow` builders, resource loading, onload, and the sub-*window*
object with its scroll/getComputedStyle callbacks) stays bridge-owned pending a future
`BrowsingContextManager` — exactly as P3.10 left `WindowContext.cs`. The two bridge entry points that
build a document (`GetOrCreateSubDocument` and the **main** document's
`createDocument`/`createHTMLDocument` in `Registration.cs`) now call `_subDocuments.BuildDocument(docRoot)`.

Because a document surface is essentially the whole DOM re-projected onto a root node, it genuinely needs
many bridge services, so the `ISubDocumentHost` contract is **wider than the small feature contracts**
(JS-wrapper identity + reverse lookup, the node-construction funnels, `SetOwnerDocRoot`, and the shared
sub-surface builders — Range/TreeWalker/NodeIterator/styleSheets/hit-testing/collect-by-tag/matching);
every seam is explicit via `DomBridge.SubDocumentHost.cs` (explicit interface members), so no callback
reaches an arbitrary bridge private field, and the assembly's neutral static `DomBridge` tree/selector
helpers (`ChildElements`/`ChildAt`/`GetDocumentElement`/`CollectTextContent`/`MatchesSelector`/`SetParent`/
`ValidateElementName`/`AdoptSubtreeIntoDocument`/`BuildDocumentTree`/… widened `private`→`internal static`
in place) are called directly and are **not** part of the contract. The `Entries`-scan node lookups in
appendChild/createDocument became the equivalent `FindDomNodeByJSObject`. Behaviour-preserving; no
public-API change (module + contract internal). Tests:
`Broiler.Cli.Tests/SubDocumentBindingModuleTests.cs` (co-location / host-contract guards +
createHTMLDocument structure/lookup, createDocument nodeType/doctype, createEvent+initEvent,
append/remove, and the **regime-A `<iframe srcdoc>` `contentDocument`** surface — the P4.4b path — all
network-free through `document.implementation` + srcdoc). Regression check vs the P3.12/P4-merged
baseline: the SubDocument / Iframe / Frame / Doctype / DocumentFragment / DocumentSentinel /
HtmlDomInterface / DomTraversal / serialization / messaging / shadow-DOM suites pass unchanged; every
observed failure (the headless `Range_GetBoundingClientRect`, the real-HTTP `HttpSubResourceTests.Iframe_*`,
the zoom/srcdoc `ScriptEngineExecuteTests.DomBridge_SerializeToHtml_*`, and the standing pixel/graphics/
font/`:lang`/CssEscape environmental set) reproduces identically with the change stashed and rebuilt →
zero regressions (verified in isolation, since the full Cli.Tests run is non-deterministic under parallel
early-abort).

Status: **P3.14 completed** 2026-07-14 (branch `htmlbridge-phase3-subdocument-module`) — the **CSSOM style
declaration** slice. The JS `CSSStyleDeclaration` object in all three flavours is the fourteenth co-located
module: `StyleDeclarationBinding` (namespace `Broiler.HtmlBridge.Dom.Features`, split
`StyleDeclarationBinding.cs` / `.Callbacks.cs`) owns the writable `element.style` (`BuildInlineDeclaration`,
was `BuildStyleObject(element,…)`), the writable rule declaration (`BuildRuleDeclaration`, was
`BuildStyleObject(styleMap,…)` — the 6 `rule.style` sites in `StyleSheets.cs`), and the read-only
`getComputedStyle` result (`BuildComputedDeclaration`, was the object half of `BuildComputedStyleObject`) —
each exposing cssText/setProperty/getPropertyValue/removeProperty/cssFloat/length/item/getPropertyPriority/
parentRule plus camelCase↔kebab bracket access. It owns the `CssStyleDeclaration`/`CssRuleStyleDeclaration`
`JSObject` subclasses, the ~20 numbered callbacks (`JsUtilities…003…023Core` → semantic `Inline*`/`Rule*`;
`JsCss…001/003Core` → `Computed*`; the deleted `JsFunctionCallbacks/Css.cs`), and the declaration-only
helpers (`GetStylePropertyNames`, `TryGetStylePropertyRawValue`, `TryGetExpandedInlineStyleRawValue`,
`BuildDeclaredInlineStyleMap`, `CssStyleDeclarationNonCssNames`).

**Like `ClassListBinding` (P3.6) it is an internal *static* class with no host contract** — pure CSSOM-IDL
logic over an inline-style dictionary and the canonical `CssPropertyNames`/`CssPriority` helpers. The map
*production* and the invalidation side effects stay in the bridge: `element.style`'s caller passes the
`onMutation` (P4.7 write-through + `InvalidateStyleScope`), the bridge's thin `BuildComputedStyleObject`
wrapper passes the engine-cascaded computed map, and the module reaches the shared inline-style store /
"set-via-JS" bookkeeping through neutral static `DomBridge` helpers (`InlineStyle`, `ParseStyle`,
`IsAcceptableInlineValue`, `ExpandCssShorthands`, `ClearPositionAreaResolution` widened
`private`→`internal static`; plus four new named `Mark`/`Unmark`/`Clear`/`InlineStylePropsSetByJs`
bookkeeping seams so the module never touches the runtime-state `JsSetStyleProps` set directly). No
public-API change. Tests: `Broiler.Cli.Tests/StyleDeclarationBindingModuleTests.cs` (co-location/static
guard + camelCase/kebab/cssText one-state, removeProperty/length/item, cssFloat→float, getComputedStyle
read consistency, and stylesheet `rule.style` mutation). Regression check vs the P3.13 baseline: the CSSOM
/ style-declaration / stylesheets / selectors / anchor / position-area / serialization / animation suites
pass unchanged; the only failures (the `:lang` selector, the three zoom/srcdoc
`ScriptEngineExecuteTests.DomBridge_SerializeToHtml_*`, and the `HttpClientMigrationTests` reflection guard)
are the standing environmental set, confirmed identical at baseline in isolation → zero regressions.

Still to come — each entangled with layout or rendering; the P3.7–P3.14 named-accessor / relocated-infra /
shared-write-hub / wide-explicit-host / no-host-static pattern is the template for any residual coupling:
the CSSOM **stylesheet** objects (CSSStyleSheet/CSSRule in `StyleSheets.cs`, the sibling of P3.14's
declaration), Element/geometry, Window/Document, SVG, the **rest** of Frames/browsing-contexts (the
`BrowsingContextManager` consolidating the sub-window / content-document caches, the sub-window object and
`WindowContext.cs`), Canvas (better done with Phase 6, which dissolves
`Broiler.HtmlBridge.Rendering.CanvasCommandRecorder`), and the DomBridge 500-800-line facade target.

Goal: make each browser API understandable and testable without loading the
entire DomBridge implementation.

Work:

1. Pick one vertical slice with moderate coupling, such as Traversal/Range.
2. Move registration and callbacks together into its feature module.
3. Give the module explicit dependencies and semantic callback names.
4. Repeat for Events, CSSOM, Element, Window/Document, Forms, Frames/Network,
   Messaging and Canvas.
5. Break Utilities.cs apart only when a consumer module is extracted; every
   helper gets a clear owner or is deleted.
6. Externalize embedded polyfill JavaScript as versioned assets after module
   ownership is stable.
7. Add a guard forbidding new DomBridge partial declarations.

Exit criteria:

- A feature's registration, handlers and tests are discoverable together.
- No callback accesses arbitrary DomBridge private fields.
- DomBridge is a composition/compatibility facade, targeted at 500-800 lines and
  one primary class file.
- No production source file exceeds 750 lines without a documented exemption.

### Phase 4 - eliminate parallel DOM state

Status: **P4.11 completed** 2026-07-14 (branch `htmlbridge-phase4-remove-subdocroot-guards`) — **work item 1,
final cleanup: delete the now-inert `#subdoc-root` tag-name special cases.** P4.4b severed the materialized
nested browsing context from the `_document` tree (a sub-document is a canonical `Broiler.Dom.DomDocument`
referenced through a container↔document map, never an in-tree `#subdoc-root` sentinel child) and left the
element with **zero creation sites**, so every remaining `IsSubDocRoot` guard and `"#subdoc-root"` TagName
check across the bridge became dead code — provably unreachable, never firing. This removes them all: the
`IsSubDocRoot(DomElement)` (`Utilities.cs`) and `IsSubDocRootNode(DomNode)` (`JsFunctionCallbacks/JsObjects.cs`)
helpers plus their ~15 call sites — the node child/sibling navigation (`childNodes`/`firstChild`/`lastChild`/
`nextSibling`/`previousSibling`), the element navigation (`children`/`childElementCount`/`firstElementChild`/
`lastElementChild`/`nextElementSibling`/`previousElementSibling`), the fragment `children` view, `CollectDescendants`
and `AdoptSubtreeIntoDocument`; the `CollectWindowFrames` recursion skip (`DomBridge.cs`); the serialization
`GetKind` `DocumentRoot` arm and the `GetChildren` sub-document skip (`DomBridge.Serialization.cs`, which
collapse to "always element" / "all children" now that a severed sub-document can never appear in `ChildNodes`);
the `CollectStyleElementsInTree` and `InvalidateStyleScopeRecursive` `#subdoc` recursion guards (`Css.cs`); the
`ToJSRootNode` `#subdoc-root` branch (`ShadowDom.cs`, whose fall-through `ToJSObject` already resolves a
canonical `DomDocument` root to its document wrapper via the P4.6 branch); and the document-level
`surroundContents` sentinel guard (`TraversalBinding.Range.cs`, whose `#document`/`#subdoc-root` element check
is dead now that the document root is a canonical `DomDocument` — the canonical `SurroundContents` enforces the
single-document-element hierarchy rule directly). The generic `#`-prefix stop in `GetDocumentRootFor` stays —
it is still live for `#shadow-root` (the one remaining `#`-prefixed bridge element); only its stale doc comment
was corrected. Behaviour-preserving (every removed guard was unreachable); no public-API change (all helpers
were private). Tests: `Broiler.Cli.Tests/SubdocRootGuardRemovalTests.cs` (reflection guards that
`IsSubDocRoot`/`IsSubDocRootNode` are gone and must not return, + node/element child-and-sibling navigation,
`getRootNode()`, and iframe-host navigation/serialization/style-collection characterizations pinning that the
severed sub-document neither appears in the main tree nor leaks its `<style>` into the parent cascade).
Regression check vs the P4.10/merged baseline: the SubDocument / Range / Serialization / ShadowDom / sentinel-
migration / KnownNodes / BrowsingContextRoot / HtmlDomInterface / DomTraversal / Acid3 / DomEvents / Attributes
suites reproduce an **identical** failure set with the change stashed (the standing headless
`Range_GetBoundingClientRect`, the real-HTTP `HttpSubResourceTests.Iframe_*`, the zoom/srcdoc
`ScriptEngineExecuteTests.DomBridge_SerializeToHtml_*`, and the flaky Acid3 score/border/cascade/NodeIterator
set) → zero regressions.

**This closes the item-1 sentinel work: every `#document`/`#document-fragment`/`#doctype`/`#subdoc-root`
fake-tag element is gone (P4.2/P4.3/P4.4a/P4.4b/P4.6) and the residual dead tag-name guards they left behind
are now removed.** The remaining Phase 4 residue is unchanged and still blocked/gated as recorded below:
item 2's full inline-style dict elimination (P4.7 shipped the script-observable write-through slice; the
~200-site dict rewrite is deferred), item 4/5's submodule-push-gated promotions (`IsEqualNode` P4.9,
`CommonAncestorWith` P4.10) and the `GetNodesInRange` / `DomRange`-stringifier / `Normalize` / `CloneDomElement`
swaps blocked by regime-A layout coupling or side-effect coupling, and P4.4c (`OwnerDocRoot`) which still needs
regime-A content-node adoption.

Status: **P4.10 prepared as a submodule patch** 2026-07-14 (same branch) — **work items 4/5: promote the
nearest-common-ancestor tree query to canonical `Broiler.Dom.DomNode.CommonAncestorWith`.** The bridge's
`FindCommonAncestor(a, b)` (`Traversal.cs`) is a neutral tree walk — the deepest inclusive ancestor of
two nodes, or `null` for disjoint trees — that belongs in canonical `Broiler.Dom`. Canonical had only a
*private* range-scoped `FindCommonAncestor` (two boundary points, throws on disjoint) and the public
`DomRange.CommonAncestorContainer`; neither is the null-tolerant node-level query the bridge needs, so
this is a promotion (a public `DomNode.CommonAncestorWith(other) : DomNode?` addition), verified
equivalent by delegating the bridge helper to it and running the range / `compareDocumentPosition`
suites green (71 tests; only the standing headless `Range_GetBoundingClientRect` geometry failure, which
reproduces on the baseline).

Same submodule-scope outcome as P4.9: the `Broiler.DOM` push 403s, so it ships as
`patches/0002-add-domnode-commonancestorwith.patch` (indexed in `patches/README.md`), the pointer is
**unbumped**, and the bridge keeps `FindCommonAncestor` as the active fallback; the follow-up after the
patch lands is to replace the helper body with `a.CommonAncestorWith(b)` (the four call sites already
null-check). No main-repo behaviour change.

**With P4.9 (`IsEqualNode`) and P4.10 (`CommonAncestorWith`), the clean, quirk-free neutral-algorithm
promotions are exhausted.** The other promotion candidates are *not* clean: `GetNodesInRange`
(`Traversal.cs`) walks via `GetDocumentOrderNodes`, which excludes `#subdoc-root` subtrees — the same
regime-A layout coupling that blocks P4.4b — so a canonical (exclusion-free) version would not be
behaviour-equivalent; and the range stringifier (`TraversalBinding.Range.cs`) has non-spec quirks
(e.g. it omits end-container text) that need spec-correctness work before promotion. Both are recorded
as blocked rather than merely push-gated.

Status: **P4.9 prepared as a submodule patch** 2026-07-14 (same branch) — **work items 4/5: promote node
equality (`Node.isEqualNode`) to canonical `Broiler.Dom.DomNode.IsEqualNode`.** The bridge's
`NodesAreEqual` / `CanonicalAttributesAreEqual` copies (`SubDocuments.cs`) duplicate a neutral DOM tree
algorithm that belongs in canonical `Broiler.Dom` (the agent audit confirmed canonical had *no*
equality operation, so this is a promotion — a submodule *addition* — not an in-repo reuse). The
canonical `DomNode.IsEqualNode` was written and its equivalence to the bridge copy verified by
delegating the bridge's `isEqualNode` binding to it and running the equality suites green (the bridge's
element-text comparison via `BridgeText` is a no-op on the canonical tree — an element's `NodeValue` is
null — so the spec algorithm is behaviour-equivalent).

**The `Broiler.DOM` submodule push returned 403** (its `MaiRat/` remote is outside this session's
GitHub scope — the documented egress caveat), so per `CLAUDE.md` this ships as
`patches/0001-add-domnode-isequalnode.patch` (with a new `patches/README.md` index and apply
instructions) and the **submodule pointer is left unbumped**. The bridge keeps its `NodesAreEqual`
implementation as the **active fallback** so CI (which clones the submodule by pointer) still compiles;
once a maintainer applies the patch and bumps the `Broiler.DOM` gitlink, the follow-up is to delete the
bridge copy and delegate the binding to `node.IsEqualNode(other)`. Behaviour is pinned by
`Broiler.Cli.Tests/IsEqualNodePromotionTests.cs` (equal/unequal element trees, attribute-order
irrelevance, text-node data equality) — green today against the bridge copy and required to stay green
after the canonical delegation. No main-repo behaviour change in this commit.

The other item-4/5 residue stays as recorded under P4.8: the remaining Broiler.Dom promotions
(`GetNodesInRange`, a `DomRange` stringifier) are the same submodule-push-gated shape; the `Normalize` /
`CloneDomElement` swaps stay blocked by the side-effect / runtime-state coupling; `GetDocumentOrderNodes`
stays blocked by the P4.4b regime-A layout coupling.

Status: **P4.8 completed** 2026-07-13 (same branch) — **work item 5, first slice: reuse canonical
`IsDescendantOf`; delete the bridge `IsDescendant` copy.** The bridge's
`IsDescendant(ancestor, candidate)` static helper (an ancestor-walk in `Utilities.cs`) exactly
duplicated canonical `Broiler.Dom.DomNode.IsDescendantOf(ancestor)`. All 13 call sites — `contains`,
`compareDocumentPosition`, the `appendChild`/`insertBefore`/`replaceChild` circular-reference guards
(element + fragment), `InsertNodeAt`, `GetNodesInRange`, the range boundary comparison
(`IsPositionAfter`) and `TraversalBinding` range extraction — now call the canonical instance method
(`candidate.IsDescendantOf(ancestor)`), and the bridge copy is deleted. Behaviour-preserving: the two
algorithms are identical for non-null args, and every call site passes a **non-null ancestor** (all
lookup-derived ancestors are null-guarded before the call), so canonical's `ArgumentNullException` on
a null ancestor — the one semantic difference from the bridge's lenient `return false` — is
unreachable. No public-API change (both were internal). Regression check vs the P4.7 baseline: the
traversal / range / HtmlDomInterface / fragment-and-doctype-sentinel / cross-document / DOM-edge-case /
Acid3-range suites pass (185 tests); the only failure (the headless `Range_GetBoundingClientRect`
display-contents geometry test) reproduces identically on the baseline → zero regressions.

Item 4 was found **already substantially complete** during this pass: the MutationObserver
option-matching fully delegates to canonical `DomMutationObserverFilter.Matches` (P3.2), and the Range
*content* operations (extract/clone/delete/insert/surround) delegate to canonical `DomRange` (P3.1).
Its residue is either intentional bridge leniency (cross-tree boundary comparison returns `0` instead
of throwing `WrongDocument`) or **Broiler.Dom submodule promotions** (a public `GetNodesInRange`, a
`DomRange` stringifier, a canonical `IsEqualNode`) that need the submodule push/patch workflow. The
remaining item-5 swaps (`Normalize`, `CloneDomElement`) stay **blocked** by the side-effect coupling
the P4.1 note describes — the bridge versions fire MutationObserver / NodeIterator / live-range /
computed-style side effects on an explicit bridge mutation path that canonical operations (which
publish only to `DomDocument.Mutated`, a stream the bridge's observers do not subscribe to) would
silently drop; `CloneDomElement` additionally copies bridge runtime state (inline style, form-control
state, position-area memo) canonical `CloneNode` knows nothing about, so it is gated on the item-2
inline-style/runtime-state convergence. `GetDocumentOrderNodes` stays blocked by the P4.4b regime-A
`#subdoc-root` layout coupling (its walk excludes `#subdoc-root` subtrees; canonical
`InclusiveDescendants` does not).

Status: **P4.7 completed** 2026-07-13 (same branch) — **work item 2, the inline-style single authority
(script-observable slice).** The bridge's kebab-case inline-style dict (`ElementRuntimeState.Style`,
reached via `InlineStyle(element)`) was authoritative but only synced back to the canonical `style=`
attribute at *serialization*, so after `element.style.color='red'` a script reading
`getAttribute("style")` saw the stale author string (`setAttribute("style",…)` already kept both in
sync; the CSSOM path did not). This closes that divergence with a **narrow write-through**: a single
shared serializer, `SyncStyleAttributeFromInlineStyle` (extracted from `ReflectRenderState` so mid-run
and final serialization use the *identical* CSSOM form — shorthand-first, `"; "`-joined, attribute
removed when empty), now runs after every `element.style` mutation as well as at serialization. It is
wired at exactly two seams: the style-object `onMutation` lambda (covers per-property set, `cssText`,
`setProperty`, `removeProperty`, `cssFloat`) and `JsJsObjectsSetStyle025Core` (`element.style = "…"`).

**Why write-through, not full elimination:** the dict has ~200 call sites (138 in the anchor resolver
alone, doing tight per-property geometry read-modify-write); parse-on-read/serialize-on-write against
the attribute would be a ~200-site rewrite with real hot-loop cost. Write-through satisfies the exit
criterion's script-observable contract — `element.style`, `getAttribute("style")`, `getComputedStyle`
and serialization now observe one state — while leaving the dict as the internal working store. It uses
the node-model `SetAttr`/`RemoveAttr` (not the JS `setAttribute` binding), so there is no reparse loop,
and touches **zero** anchor-resolver sites (those write the dict directly and legitimately do *not*
leak resolved geometry into `getAttribute` mid-resolution). The invalidation half of the exit criterion
was already met (every CSSOM mutation routes through `InvalidateStyleScope`).

**Behavioral note (spec-correct):** after a CSSOM mutation `getAttribute("style")` returns the
*serialized* declaration rather than the raw author string — matching real browsers. An un-mutated
element still returns its exact author string (no seeding/normalization on read). No public-API change.
Tests: `Broiler.Cli.Tests/InlineStyleWriteThroughTests.cs` (a `MARK=[…]` wrapper isolates the *live*
`getAttribute` value from the end-of-run serialization: camelCase/setProperty/cssText/whole-assign
reflect live, removeProperty empties, un-mutated returns raw, getComputedStyle + serialization
preserved). Regression check vs the P4.6 baseline: the InlineStyle / CSSOM / CssStyleDeclaration /
Selectors-CSSOM / attribute / computed-style / serializer / dialog / popover / position-area/try /
sticky suites pass (250+ tests); the only failures (the `:lang` selector and the
`ScriptEngineExecuteTests` zoom-serialization test) reproduce identically on the baseline → zero
regressions. The full parallel-state elimination of the dict remains available as later work if a
single-authority (no dict) model is wanted.

Status: **P4.6 completed** 2026-07-13 (same branch) — **work item 1, the final sentinel: `#document`.**
The `#document` wrapper element (`_documentNode`, a fake-tag `DomElement` that sat between the
canonical `_document` and `<html>`) is deleted; the JS `document` object now maps directly to the
canonical `Broiler.Dom.DomDocument`, and `<html>`/doctype are its direct children. This is the *last*
`#document`-family sentinel (doctype P4.2, fragment P4.3, subdoc-root-regime-B P4.4a; regime-A
`#subdoc-root` remains blocked below the bridge per P4.4b).

**No layout blocker (unlike `#subdoc-root`).** `Broiler.Layout` has zero `#document` references; the
renderer's `DomDocument→CssBox` builder (`Broiler.HTML .../HtmlParser.cs`) already *special-cased and
discarded* the sentinel (a zero-width box that would collapse layout), so with the sentinel gone the
bridge's tree flows through the renderer's normal `<html>`-rooted path and produces an identical box
tree — the workaround becomes dead code. Precedent: P4.4a already proved a canonical `DomDocument`
root works (sub-documents), including pre-insert validity and the JS-document-over-`DomDocument`
wrapper.

Change surface (well-bounded, ~16 files, +66/−58): constructor + `ParseHtml` retarget to `_document`
(the doctype-then-`<html>` append order already satisfies canonical `DomDocument` validity —
one documentElement, doctype-first — and `ParseHtml` now clears `_document` first so re-parse stays
valid); `_jsObjects.Set(_document, document)` remaps the JS wrapper; `ITraversalHost`/
`IEventDispatchHost.DocumentNode` and the event-dispatch propagation `path` widen `DomElement`→
`DomNode`; the child-mutation notify chain (`NotifyChildAdded`/`NotifyChildRemoved`/
`NotifyMutationObservers`/`TraversalBinding.NotifyNodeRemoved`/`DeliverChildListMutation`) widens to
`DomNode` so `document.appendChild`/`removeChild` MutationObserver delivery is preserved; the generic
`nodeType`/`nodeName` sites gain a `DomDocument` branch (canonical `NodeType` is already Document/9).

**Two semantic fixes the migration required** (both because the document parent is now a non-element
`DomDocument`, which the element-only `ParentEl` nulls): (1) `GetTreeRoot` now walks to the *absolute*
root so `getRootNode()`/`isConnected`/`compareDocumentPosition` return the document, not `<html>`; and
(2) the four `parentNode` getters (element/char-data/doctype/fragment wrappers) read the raw
`ParentNode` instead of `ParentEl`, so `document.documentElement.parentNode` and `doctype.parentNode`
resolve to the document (a `parentNode`-vs-`parentElement` correctness fix — `parentElement` correctly
stays element-only and is now null for the documentElement). `ToJSObject` also gained a `DomDocument`
branch resolving a sub-document root to its document wrapper. **Behavioral note:** `document.appendChild`
of a second element / text node now throws `HierarchyRequestError` (canonical validity), which is
spec-correct — the sentinel previously permitted it silently.

Behaviour-preserving on every normal path; no public-API change (the widened interfaces are internal).
Tests: `Broiler.Cli.Tests/DocumentSentinelMigrationTests.cs` (nodeType/nodeName, documentElement/
head/body, firstChild=doctype, getElementById/querySelector, `getRootNode()`/`isConnected`,
serialization round-trip). Regression check vs the P4.5 baseline: the DOM / events / mutation-observer
/ shadow-DOM / traversal-range / HtmlDomInterface / sentinel-migration / sub-document / messaging /
serializer / namespace / public-API-snapshot / architecture-guard suites pass (300+ tests); every
observed failure (the `:lang`/CssEscape/CssExtraction structural guards, the `<select>` writing-mode
layout test, the `ScriptEngineExecuteTests` iframe-scroll/zoom serialization tests, the headless
`Range_GetBoundingClientRect` and iframe-HTTP tests) reproduces identically with the change stashed →
zero regressions.

Status: **P4.5 completed** 2026-07-13 (branch `claude/htmlbridge-phase-4-a4w8vp`) — **work item 3, the
parallel `InnerHtml` string.** `ElementRuntimeState.InnerHtml` (the bridge-side raw-text mirror for
`<style>`/`<script>`/`<textarea>`, the historical `innerHTML`-getter fallback, and the
serialization round-trip value) is **deleted**. The `innerHTML` getter already served canonical
children (`SerializeChildrenToHtml`), and `SetElementInnerHtml` already parses/replaces into canonical
children — so the string was pure shadow state. All nine accesses were removed: the two write sites in
`SetElementInnerHtml` (`SubDocuments.cs`) and `CloneDomElement` (`Utilities.cs`, a clone now carries
content only via its deep-cloned DomText children — shallow clone correctly drops it, per DOM); the
progress/meter placeholder reset (`Serialization.cs`); the `<style>` source fallback in
`GetStyleElementSourceText` (`Css.cs`); the `textContent` fallback for a childless element
(`Common.cs`, now returns `""` per DOM); the serializer's `GetRawInnerHtml` adapter (now `_ => null`,
matching the canonical default); and the anchor-cleanup `!hadTextChild` InnerHtml branch
(`AnchorResolver/CssCleanup.cs`), which neutralized a source that no longer exists.

**The load-bearing invariant — proven, not assumed:** raw-text element content is *always* a canonical
`DomText` child (initial parse via `HtmlDocumentParser`, and the `innerHTML` setter's fragment parse,
both emit one), even after the full anchor/render resolve pipeline (`NeutralizeStyleElementsForAnchorRules`
rewrites the text node **in place** via `SetBridgeText`, never migrating content to a string). The
childless-`<style>`-with-content state that `AnimationResolver`/`PositionTry`/`CssCleanup` carried
defensive InnerHtml-fallback handling for is **unreachable through the public API** — the former
`AnimationInnerHtmlStyleTests` had to fabricate it by reflection. Those two tests were rewritten to
exercise the same `@keyframes` / stylesheet-`animation` collection through a real DomText-backed
`<style>` (their production shape); the stale "CSS can live in InnerHtml with no DomText child"
comments across those three resolvers were corrected. Behaviour-preserving; no public-API change (the
field was `private`). Tests: `Broiler.Cli.Tests/InnerHtmlParallelStateRemovalTests.cs` (invariant
probe: a `<style>` keeps its DomText child through `ResolveAnchorPositions`; plus getter/setter/
textContent/clone/serialization/cascade characterizations). Regression check vs the P4.4a baseline: see
below.

**Finding recorded for P4.4c (eliminate `OwnerDocRoot`): it is effectively gated on P4.4b, contrary to
this roadmap's earlier "independent of this blocker" note.** A full audit found `OwnerDocRoot`'s
`ownerDocument`-getter read and its `_documentWrappers` key have **no canonical substitute** for
regime-A (`#subdoc-root`) iframe nodes and shadow roots: those nodes are not children of a canonical
`DomDocument`, so canonical `node.OwnerDocument` returns the *main* document for them. The property
cannot be removed until regime-A roots become canonical documents (the same P4.4b layout blocker). The
only safe isolated changes here are preparatory convergence (trimming the redundant regime-B
`OwnerDocRoot` writes that duplicate `AppendChild`'s `AdoptNode`, and adopting detached
sub-document-created nodes so canonical `OwnerDocument` matches) — non-terminal, so deferred.

Status: **P4.4a completed** 2026-07-13 (same branch) — work item 1, third sentinel (`#subdoc-root`),
**stage a of a multi-stage remodel**. `#subdoc-root` cannot become a canonical `DomDocument` in place
because the iframe/object/frame roots (regime A) are live *tree children* of their container, which a
`DomDocument` forbids; the full remodel severs that link and follows a container↔document reference.
P4.4a does the safe, self-contained first stage: the **detached** `createDocument`/`createHTMLDocument`
roots (regime B) — which are already parentless — become real canonical `DomDocument`s
(`CreateBrowsingContextDocument`), and their doctype/documentElement are appended as true canonical
document children (a `DomDocumentType` is finally a legitimate child of a `DomDocument`). Regime A
(iframe) stays on the `#subdoc-root` element path until P4.4b.

Foundation (behavior-preserving `DomElement`→`DomNode` widenings so a `DomDocument` root flows through
the shared sub-document infrastructure): `ElementRuntimeState.OwnerDocRoot`, the `_documentWrappers`
map + `SetDocument`/`TryGetDocument`, `AdoptSubtreeIntoDocument`, `BuildSubDocument` + its 24
sub-document callbacks, and the shared descendant helpers `GetDocumentElement`/`CollectByTagName`/
`FindInSubTree`/`CollectMatching`/`CollectStyleElements`/`HitTestDocumentPoint`/`BuildStyleSheetsCollection`/
`BuildRange`. `GetDocumentElement` became honestly nullable (an empty `DomDocument` has no
documentElement, per DOM — was the `#subdoc-root` self-fallback), with null-guards added to the six
callers.

**Two regressions found and fixed during the stage** (both from the doctype/document now being a
canonical non-element the surrounding element-typed code didn't expect): (1) `createDocument` with an
empty qualifiedName produced an empty `DomDocument`, so `doc.documentElement` returned null and the
facade getter crashed on `ToJSObject(null)` — fixed by returning `null` per DOM; (2) the `Range`
`setStart/EndBefore/After` boundary setters used `ParentEl` (`ParentNode as DomElement`, which nulls a
`DomDocument` parent) and so threw `InvalidNodeTypeError` when a node's parent was a regime-B document
root — fixed to use the raw `ParentNode` (a Document is a valid boundary container). Both are guarded
by pre-existing tests (`DomImplementationTests.Implementation_CreateDocument_Without_QualifiedName`,
`Acid3Phase4RangeTests.Test8_MovingBoundaryPoints`).

Behaviour-preserving; no public-API change. Tests:
`Broiler.Cli.Tests/BrowsingContextRootMigrationTests.cs` (createDocument/createHTMLDocument report
nodeType 9 with a canonical documentElement + doctype child; element creation/lookup/query work).
Regression check vs the P4.3 baseline: a 1073-test wide sweep has the change-side failure set as a
strict subset of baseline (no new failures); the Acid+Range set-diff's one apparent delta passes in
isolation (known parallel-load render flakiness) → zero regressions.

Status: **P4.4b completed** 2026-07-14 (branch `htmlbridge-phase4-p44b-iframe-sever`) — **the regime-A
iframe/object/frame sever, delivered as the cross-layer fix the earlier blocker called for.** The
materialized sub-document of an `<iframe>`/`<object>`/`<frame>` is no longer an in-tree `#subdoc-root`
child of the container; the four `BuildSubDocument*` paths now mint a canonical `Broiler.Dom.DomDocument`
(via the P4.4a `CreateBrowsingContextDocument` funnel) referenced through container↔document maps
(`_contentDocuments`/`_documentContainers`, with `GetContentDocument`/`GetFrameForContentDocument`/
`LinkContentDocument`). The tree-child lookups were rewired to the forward map (`GetOrCreateSubDocument`,
`InvalidateCachedSubDocument`, `GetSubDocumentScrollingElement`, `TrySerializeCurrentSrcDoc`) and the
three `ParentEl(#subdoc-root)` reverse lookups to the reverse map (`GetOuterFrameElement`,
`GetParentWindowForSubDocument`/`GetInheritedSubDocumentBaseUrl`, and `GetViewportForDocRoot` — the last
because `GetDocumentRootFor` now returns the sub-document `<html>` instead of the `#subdoc-root` element).

**The cross-layer half that unblocked it** (the reason the prior bridge-only attempt was reverted): the
renderer now lays out the *referenced* content document instead of a `#subdoc-root` subtree.
`Broiler.Layout` gained a neutral `CssBox.IsNestedBrowsingContextRoot` flag; `LayoutNestedBrowsingContexts`
keys off that flag rather than `SourceElement.TagName == "#subdoc-root"`. A `Func<DomElement,DomDocument?>`
content-document resolver is threaded from the bridge through `ILayoutView.GetGeometry` →
`HeadlessLayoutView` → `HtmlContainer`/`HtmlContainerInt.ContentDocumentResolver` →
`DomParser.GenerateCssTree` → `HtmlParser.ParseDocument`/`AppendCanonicalNode`, which — for an element the
resolver maps to a content document — synthesises the sub-viewport box (flag set, no `SourceElement`) and
projects the referenced document's tree into it. So a subframe element still reports real
`getBoundingClientRect` composed into the main frame, from a document that is no longer in `_document`.
`HeadlessLayoutView` bypasses its `(document,version,viewport,baseUrl)` snapshot cache when a resolver is
present, because a severed sub-document has its own `DomDocument.Version` invisible to the main document's.

`Broiler.Layout` and `Broiler.HTML` are the parent-repo / submodule this touched; the cascade is
unaffected because `SharedRendererCascade.BuildEngine` uses the document only for a null-check (rules come
from the globally-collected `styleSet` + each element's own inline/ancestor chain), and
`CascadeParseStyles`/`CascadeApplyStyles` walk `box.Boxes`, so a separate content `DomDocument` cascades
identically. `OwnerDocRoot` was left exactly as before (regime-A parsed nodes still unadopted) to preserve
behaviour — the reverse-map lookups tolerate the resulting nulls, giving parity.

Behaviour-preserving; no public-API change to the bridge (the maps/resolver are internal; the widened
`ILayoutView`/`HtmlContainer` members are renderer-side). Tests:
`Broiler.Cli.Tests/SubDocumentSeverMigrationTests.cs` (sentinel absent from the serialized tree,
`contentDocument` DOM intact, subframe geometry composed, `srcdoc` serialization round-trip, `srcdoc`
reassignment rebuild). Regression check vs the merge-base: the **four oracle tests the prior attempt
regressed now pass** (`Todo28_Iframe_ZeroSize_No_Visual_Box`, the two script-assigned-iframe-position
`ScrollIntoView` tests, `SubframeElement_GetBoundingClientRect_Is_Composed_Into_Main_Frame`); the
sub-document/iframe/frames/messaging (138), HtmlDomInterface/serialization/geometry/anchor (134), and
Acid3/DOM/traversal/shadow/migration (506) suites show **zero regressions** — every failure
(HttpSubResource real-HTTP iframe, zoom/iframe-srcdoc serialization, Acid3 score/border/cascade/NodeIterator
pixel, the standing `Range_GetBoundingClientRect` headless-geometry) reproduces identically with all
changes stashed.

**Residual follow-up (not blocking):** the `#subdoc-root` *element* is fully eliminated (zero creation
sites), but a handful of now-inert defensive `#subdoc-root` tag guards remain (`Utilities.IsSubDocRoot`
and its two exclusion call sites, the `Css.cs` style-scope/collect guards, `DomBridge.CollectWindowFrames`,
and the `DomBridge.Serialization` `GetKind`/skip arms). They can never fire (no such element is ever
built) and are left as a trivial dead-code removal so this change stays a focused, behaviour-preserving
sever. **P4.4c** (eliminate `OwnerDocRoot`) is now unblocked (regime-A roots are canonical
`DomDocument`s); the remaining `#document` sentinel work is independent.

Status: **P4.3 completed** 2026-07-13 (same branch) — work item 1, second of four sentinels. The
`#document-fragment` sentinel element is replaced by the canonical `Broiler.Dom.DomDocumentFragment`.
Unlike the doctype leaf, a fragment is a *container*, so it gets a dedicated
`PopulateDocumentFragmentJSObject` wrapper in `ToJSObject` (Node base + ParentNode mixin +
child-manipulation: childNodes/children/first-last-child/-ElementChild, appendChild/insertBefore/
removeChild/replaceChild/append/prepend, querySelector(-All), textContent, cloneNode) — deliberately
NOT the element surface (attributes/style/tagName) it inherited as a sentinel element, and (preserving
today's behaviour) NOT `getElementById`. Node-generic members reuse the existing DomNode handlers;
the container members are focused fragment lambdas over the neutral tree helpers plus two shared
helpers widened `DomElement`→`DomNode`: `InsertNodeAt` (guarding the element-only style-scope
invalidation / child-added notification with `is DomElement`; a fragment parent has neither) and
`FindInDescendants`/`SearchDescendants` (read-only descendant walk; scope is null for a fragment
root). Fragment construction funnels through a new `CreateBridgeDocumentFragment()` at all three sites
(`createDocumentFragment`, Range clone/extract result, the internal HTML fragment-parse container);
the parse-container carriers (`BuildFragmentTree` return, `TryBuildInnerHtmlFragmentContainer` out
param, the `parsedContainer` local) became `DomDocumentFragment`. `CloneDomElement`, `NodesAreEqual`,
`GetNodeType`×2, `GetNodeName`, the serialization `GetKind` and the `append`-family child-spread
(`BuildChildNodeArgumentNodes`) all gained a `DomDocumentFragment` branch and shed their
`#document-fragment` TagName checks (the child-spread was the "silently stops matching" hazard the
audit flagged). `appendChild(fragment)` now also unpacks natively (via the DomNode-widened
`InsertNodeAt`), matching `append(fragment)`.

**Regression fixed in this slice (introduced by P4.2, missed because that PR's wide-sweep log was
tail-truncated to 8 of 10 failures):** `document.implementation.createDocument(ns, qname, doctype)`
resolved the passed doctype with `_jsObjects.Entries … kvp.Key is DomElement`, which silently skipped
the now-non-element `DomDocumentType` — so the doctype's `OwnerDocRoot` was never set and
`doctype.ownerDocument` wrongly returned the main document. Fixed by matching `is DomNode` in both
`createDocument` paths (main + sub-document) and, for the same class of hazard, in the sub-document
`appendChild` node-lookup. Guarded by the pre-existing
`DomEdgeCasePhase4Tests.CreateDocument_XHTML_With_DocType_Sets_OwnerDocument` (confirmed pass@P4.1 →
fail@P4.2 → pass now).

Behaviour-preserving; no public-API change. Tests:
`Broiler.Cli.Tests/DocumentFragmentSentinelMigrationTests.cs` (create, append-unpack, query/children,
cloneNode, Range extractContents). Regression check vs the P4.1 baseline (predating both P4.2 and
P4.3): a 1005-test sweep has the change-side failure set as a strict subset of baseline (one flaky
`:root` test even flipped to passing) with **no new failures**; the 21 apparent Acid deltas all pass
in isolation (known parallel-load render flakiness — the Acid count varied 7/8/28 across runs) → zero
regressions.

Status: **P4.2 completed** 2026-07-13 (same branch) — work item 1, first of four sentinels. The
`#doctype` sentinel element is replaced by the canonical `Broiler.Dom.DomDocumentType`. The doctype
was a fake-tag `DomElement` (null namespace, `TagName == "#doctype"`) whose name/publicId/systemId
lived in a parallel `ElementRuntimeState.DocumentType` state class (`DocumentTypeRuntimeState`); it
now IS a canonical DocumentType node that carries those natively — so `DocumentTypeRuntimeState` and
its `CopyRuntimeValuesTo` copy are deleted. Construction funnels through a new
`CreateBridgeDocumentType(name, publicId, systemId)` over the existing `DomDocument.CreateDocumentType`
factory (name lowercased once at construction to preserve the old read-time `GetDocTypeName`
lowercasing; the always-`null` `internalSubset` is dropped — canonical has no such field and the JS
getter already returned `null`). All five creation sites (main-parse `ParseDocType`, `document.write`,
`createDocumentType` × main+sub, `createHTMLDocument` × main+sub) use the funnel. The reads move to the
canonical node: a dedicated `PopulateDocumentTypeJSObject` wrapper gives the doctype the correct
minimal DocumentType surface (Node base + ChildNode mixin + EventTarget + name/publicId/systemId,
**not** the element surface it used to inherit); `GetNodeType`/`GetNodeName`, the serialization adapter
(`GetKind`/`GetName`), `NodesAreEqual` (isEqualNode) and `CloneDomElement` all gained a
`DomDocumentType` branch and shed their `#doctype` TagName special-cases. The canonical
`HtmlSerializer`/`HtmlDocumentParser` already speak `DomDocumentType`, so no submodule change was
needed. **One regression found and fixed during the slice:** the sub-document `childNodes` handler
used `ChildElements` (element-only), which silently dropped the now-non-element doctype — switched to
raw `ChildNodes` (matching `firstChild` and DOM `childNodes` semantics). Behaviour-preserving; no
public-API change. Tests: `Broiler.Cli.Tests/DoctypeSentinelMigrationTests.cs` (parsed doctype,
`createDocumentType`, `createHTMLDocument`, `cloneNode`, serialization). Regression check vs the P4.1
baseline: the DOM / sub-document / cross-document / HTML-DOM-interface / serializer / namespace /
traversal / lifetime / guard suites pass unchanged (595 in the wide sweep); the Acid pixel/layout
suite fails the same **count** (8) on baseline and change with the differing test passing in isolation
(known container flakiness), plus the standing headless-geometry failure → zero regressions.

The remaining three sentinels — `#document-fragment` → `DomDocumentFragment`, `#subdoc-root` →
browsing-context root, and `#document` → `DomDocument` — are separate sub-slices (P4.3+). They are
harder than doctype: a fragment/subdoc-root/document is a **container** whose JS surface (appendChild,
querySelector, children) the element wrapper currently supplies, and a canonical `DomDocumentType` may
only be a child of a canonical `DomDocument` — so once the document/subdoc roots also become canonical,
the doctype's `SetParent`/`AppendChild` will run under canonical pre-insert validity (today it is
allowed because the parent is still an element).

Status: **P4.1 completed** 2026-07-13 (branch `htmlbridge-phase4-remove-knownnodes`) — work item 6.
The `_knownNodes` parallel node set is deleted. It was a process-instance
`HashSet<DomNode>` populated on **every** node-construction path (`createElement`, `cloneNode`,
`createComment`/`createTextNode`, `createDocumentFragment`, doctype/subdoc-root creation, HTML/XHTML
parse, `document.write`, `innerHTML`/`outerHTML` replace, `insertAdjacent*`, `attachShadow`, the
XML-fallback builders) — ~70 write sites across 10 files — but had **no behaviour-affecting reader
left**: its last real consumer (`document.links`/collection ordering) was already replaced by
tree-order traversal, and its only surviving lookup was a redundant `if (!Contains) Add` guard on a
`HashSet` (idempotent by definition). It was pure parallel state shadowing canonical tree membership,
so the canonical Broiler.Dom tree is now the single authority. Removed with it: the now-dead
`AddElementsRecursive` register-subtree helper (its `RemoveElementsRecursive` counterpart stays — it
still evicts `_jsObjects`/`_styleSheetCache` on sub-document teardown) and the orphaned
`CollectAllDescendantsFlat` document.write helper. `ParseHtml`/`Dispose` no longer `Clear()` a set
that is gone. Behaviour-preserving; no public-API change (`_knownNodes` was `private`). Tests:
`Broiler.Cli.Tests/KnownNodesRemovalTests.cs` (guard: the field is gone + it must not be
reintroduced; characterizations: element create/insert, `cloneNode`/comment/fragment construction,
and `innerHTML` parse-replace all still round-trip through the canonical tree). Regression check vs
the P3.12 baseline: the DOM / traversal / namespace / HTML-DOM-interface / serializer / cross-document
(SVG) / attributes / MutationObserver / lifetime / public-API-snapshot / architecture-guard / Acid3
suites pass unchanged; the pre-existing environmental headless-geometry failure
(`DomTraversalAndRangeTests.Range_GetBoundingClientRect_Includes_DisplayContents_Descendants`) fails
identically with the change stashed → zero regressions.

This is the first Phase 4 slice and the safest opener: it removes a genuine parallel authority (not a
convenience wrapper) and declutters the `SubDocuments`/`Registration` node-construction paths that the
still-pending Phase 3 Frames/browsing-context and Window/Document feature modules must extract — every
construction site lost its `_knownNodes.Add(...)` bookkeeping line. The heavier parallel-state items
(1 sentinel `#document`-family elements, 2 inline-style single authority, 3 parallel `InnerHtml`
string) remain; item 5 "delete bridge copies" is a **separate judgement**, not a blanket sweep: the
Phase-3-widened neutral shims (`IsText`/`ParentEl`/`ChildAt`/attribute scans, etc.) read the canonical
tree rather than holding state, so they are wrappers to consolidate opportunistically, and the bridge's
`Normalize`/`isEqualNode`/`cloneNode` reimplementations are **not** straight swaps for the canonical
ones — they fire the bridge's MutationObserver / NodeIterator side effects the canonical algorithms do
not.

Goal: make Broiler.Dom and Broiler.Dom.Html the only authorities for document
tree/content state.

Work:

1. Replace sentinel elements named #document, #document-fragment, #doctype and
   #subdoc-root with DomDocument, DomDocumentFragment, DomDocumentType and
   explicit browsing-context roots. Remove tag-name special cases.
2. Make the canonical style attribute or a canonical declaration object the one
   inline-style authority. A JS style mutation and getAttribute must observe the
   same state and trigger the same invalidation.
3. Remove the parallel InnerHtml string. innerHTML becomes parse/replace of
   canonical children; serialization always reads canonical nodes.
4. Promote neutral mutation-option matching and Range content algorithms to
   Broiler.Dom where they are still duplicated.
5. Reuse canonical Normalize, equality, clone, tree-order and traversal
   operations; delete bridge copies.
6. Remove tree-derived lists such as _knownNodes if they have no independent
   lifecycle role.

Exit criteria:

- One tree, one attribute/declaration value, one innerHTML representation and
  one mutation source exist.
- No #document-family fake tag checks remain.
- DOM conformance, Range, Selection, serialization and shadow-tree tests pass.

### Phase 5 - move used-value behavior into Layout

Goal: turn LayoutMetrics and AnchorResolver into a thin API adapter over a
single layout snapshot.

Broiler.Layout needs a richer public read model:

- border/content/padding geometry and client rectangles;
- fragmented rectangles and display: contents descendants;
- scroll overflow, scroll bounds and scroll offsets;
- offset parent and containing blocks;
- hit-test/topmost order;
- viewport, zoom and used-value metadata;
- anchor/position-try resolution;
- animation sample time and applied used values.

Work:

1. Define LayoutSnapshot/ILayoutView with document-version and viewport identity.
2. Implement all geometry APIs against one snapshot per document version.
3. Move anchor placement, position-area, position-try, sticky/fixed containing
   blocks, overflow simulation and hit testing to Layout.
4. Move neutral anchor/keyframe/timing syntax models to Broiler.CSS first; Layout
   consumes those models and applies them to boxes.
5. Keep only CSSOM View unit conversion, Web IDL defaults and JS object
   construction in the bridge.
6. Delete fallback geometry approximations after parity is proven.

Exit criteria:

- No per-element renderer/layout construction.
- One layout pass services all geometry queries until document/viewport
  invalidation.
- LayoutMetrics is a small binding/facade, not a layout engine.
- Anchor, sticky/fixed, scrolling, hit-testing and animation tests exercise
  Layout directly plus thin bridge contract tests.

### Phase 6 - remove Broiler.HtmlBridge.Rendering

Goal: dissolve a project which currently groups three unrelated concepts.

Disposition:

| Current type | Interim action | End state |
|---|---|---|
| SharedLayoutGeometryProvider | Put behind ILayoutView and make lifetime/cache correct | implementation in HTML.Orchestration/HTML.Headless |
| HtmlPostProcessor | Convert to ordered, non-destructive render-preparation passes | native HTML/Layout behavior; remaining Acid/WPT shims in test support |
| CanvasRenderingContext2D / CanvasDrawCommand | Internalize in Canvas binding and cap/remove unused command storage | real immutable Broiler.Graphics display list if a renderer consumes it |

HtmlPostProcessor must not be moved wholesale into Broiler.Dom.Html: it strips or
replaces valid content and contains renderer/test policy. The migration is to
replace each workaround with native HTML/Layout behavior, not to rename it.

Exit criteria:

- Rendering project has no consumers and is deleted.
- Render preparation never mutates the live script-visible DOM.
- Production browsing does not apply Acid/WPT-specific transforms.
- Canvas commands are either rendered and bounded or are not recorded.

### Phase 7 - isolate loading, security and browsing-context policy

Goal: separate deterministic document algorithms from host I/O and policy.

Work:

1. Split ContentSecurityPolicy into immutable directive parsing/evaluation,
   document meta discovery, and URL/origin context.
2. Replace regex HTML discovery in CSP and ScriptExtractionService with
   Broiler.Dom.Html parser output.
3. Make script extraction return metadata-rich descriptors: source kind, URL,
   nonce, async/defer/module flags and document order.
4. Route scripts, stylesheets, fetch, XHR and frames through one injected
   ResourceLoader with explicit file/data/http policy and cancellation.
5. Keep CSP checks in the host/browser layer; DOM and CSS receive already
   authorized content.
6. Move module-script support and script ordering into the browser event loop;
   do not silently skip a recognized module.

Exit criteria:

- No direct HttpClient/file/data-URI switch remains in feature callbacks.
- Unit tests use a deterministic in-memory loader.
- One URL resolution/origin implementation is shared by script, CSS, fetch, XHR
  and frames.
- CSP tests distinguish parse, discovery, policy and load/execution decisions.

### Phase 8 - simplify Core and Scripting, then reconsider assemblies

Goal: leave small contracts whose names match their responsibility.

Work:

1. Split IScriptEngine execution, interactive-session, profiling and
   microtask/event-loop capabilities. Preserve the old interface as an adapter
   until a deliberate public-surface v3.
2. Give every InteractiveSession a private event-loop/context lifetime. Ensure
   failed construction disposes it.
3. Make async-drain-limit exhaustion an explicit diagnostic/result, not a silent
   stop.
4. Apply profiling consistently or move it to host diagnostics if there are no
   real consumers.
5. Rename IScriptExtractor.cs to match ScriptExtractionResult, or restore a
   meaningful interface.
6. Decide final assemblies from dependency and deployment needs:
   likely Core, WebApi bindings and Scripting/Host. Avoid assembly-per-feature.

Exit criteria:

- Core contains contracts/value objects, not regex parsers, networking and
  mutable global logging together.
- ScriptEngine has one execution pipeline shared by normal, detailed, typed and
  interactive entry points.
- A public v3 is proposed only for changes which cannot be adapted behind v2.

## Priority and sequencing

The critical path is:

    Phase 0 -> Phase 1 -> Phase 2 -> Phase 3 -> Phase 4 -> Phase 5 -> Phase 6
                                           \-> Phase 7 -> Phase 8

Phase 7 can start after the ResourceLoader seam in Phase 2. CSS typed-value and
Layout read-model work can run in parallel with feature-module extraction after
Phase 1. Removing Rendering waits for both geometry inversion and replacement of
its compatibility passes.

Recommended first six implementation PRs:

1. Repair the namespace/public facade and restore Broiler.slnx.
2. Add dependency guards and deduplicate project paths.
3. Introduce ILayoutView; remove the Dom-to-HTML.Image path.
4. Introduce BrowserDocumentSession plus deterministic disposal tests.
5. Extract DocumentStyleContext and JsObjectRegistry.
6. Convert Traversal/Range into the first co-located feature binding module.

These establish the pattern without beginning with the riskiest areas
(LayoutMetrics, frames, events or network).

## Validation matrix

Every phase must choose the smallest relevant rows; milestone releases run all
rows.

| Boundary | Required validation |
|---|---|
| Public surface | API snapshot, compatibility consumer, no unplanned v2 changes |
| Core/Scripting | execution, deferred/async/interactive, microtask/timer, CSP and profiling tests |
| DOM | DOM/Range/Selection/traversal/mutation/shadow/serialization tests |
| CSS | parser, selector, cascade, computed-style, CSSOM live-mutation tests |
| Layout | geometry, scroll, fixed/sticky, hit-test, anchor, animation and zoom tests |
| Renderer | Acid, pixel/reference and headless-layout parity |
| Browser behavior | WPT subsets for the touched feature, then full committed-baseline comparison |
| Performance | bridge.mutation no worse than baseline +2%; layout snapshot built at most once per document version/viewport |
| Lifetime | repeat attach/dispose and parallel-session leak/isolation tests |
| Architecture | no canonical-to-bridge references, no HTML.Image dependency, no direct callback networking, no new partial files |

## Quantitative completion criteria

The program is complete when all of the following are true:

- Broiler.HtmlBridge.Dom is below 18,000 non-generated lines as a guardrail,
  with complexity moved to correct reusable engines or deleted rather than merely
  hidden.
- The public DomBridge facade is at most 800 lines and has no feature-private
  state.
- No new DomBridge partial files exist; no feature source file exceeds 750 lines
  without a reviewed exception.
- Broiler.HtmlBridge.Rendering is deleted.
- Broiler.HtmlBridge.Dom has no reference to Broiler.HTML.Image.
- The solution builds one canonical Dom and one canonical Graphics project path.
- There is one authority each for DOM content, inline style, events, mutations,
  resources, computed-style invalidation and layout snapshots.
- Fake #document-family elements and process-global session runtime state are
  gone.
- DOM equality/normalization, CSS parsing/timing, anchor placement and geometry
  each have one implementation in their canonical owner.
- Public v2 compatibility remains intact unless a separately approved v3
  boundary explicitly changes it.

## Risks and controls

| Risk | Control |
|---|---|
| A move changes behavior while appearing mechanical | Characterization test before each extraction; one concern per PR |
| Canonical projects absorb browser-specific policy | Dependency guards plus the ownership table above |
| New services merely recreate the god object | Narrow constructor dependencies; no service locator or DomBridge back-reference |
| Layout extraction causes repeated rendering | Versioned snapshot contract and benchmark assertion |
| State split creates two authorities | State-authority checklist and temporary write-through adapter with a deletion issue |
| Public namespace churn breaks consumers | Preserve Broiler.HtmlBridge.DomBridge facade through v2 |
| WPT-only hacks leak into production | Explicit RenderPreparationPass classification and production/test pipelines |
| More projects increase build complexity | Extract classes first; create assemblies only at stable dependency seams |

## Decisions that should be made explicitly

These are architecture decisions, not blockers to starting Phases 0-2:

1. Whether the layout contract lives in Broiler.Layout or a tiny
   dependency-neutral Broiler.HTML.Headless.Abstractions assembly. Prefer
   Broiler.Layout if the DTOs contain only used-value/read-model concepts.
2. Whether host/security remains under HtmlBridge or becomes Broiler.Web.Security.
   Create a new project only if there is a second non-bridge consumer.
3. Whether Canvas is intended to render. If not, remove command recording; if
   yes, define the Graphics display-list consumer before promoting its model.
4. Whether public-surface v3 is desired. None of the internal decomposition
   requires it.

## Relationship to existing roadmaps

This roadmap follows, rather than replaces, the completed neutral DOM/CSS
promotion work:

- [Phase 0 baseline record](htmlbridge-phase0-baseline.md)
- [DOM/CSS promotion roadmap](htmlbridge-dom-css-promotion-roadmap.md)
- [Remaining work roadmap](htmlbridge-remaining-work-roadmap.md)
- [Promotion backlog](htmlbridge-promotion-backlog-roadmap.md)
- [Out-of-scope routing](htmlbridge-out-of-scope-routing-roadmap.md)
- [Engine boundary](../architecture/htmlbridge-engine-boundaries.md)

Those documents answer which standards algorithms could leave the bridge. This
document answers how to reduce the complexity of the bridge responsibilities
which correctly remain.
