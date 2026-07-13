# HtmlBridge current component inventory

Status: analysis baseline for the
[HtmlBridge complexity-reduction roadmap](../roadmap/htmlbridge-complexity-reduction-roadmap.md)

Baseline date: 2026-07-13

## Reading this inventory

This document inventories every non-generated HtmlBridge source file, every
separately declared type, and the method families owned by each file. It lists
individual methods for the smaller Core, Rendering and Scripting projects. For
Broiler.HtmlBridge.Dom, 1,104 approximate methods are spread through 63 partial
DomBridge declarations, including 409 numbered JavaScript callbacks. Repeating
all of those generated-style identifiers would hide rather than explain the
architecture, so the Dom section records every file, its method count, its
mission and its proposed owner. The source remains the signature-level manifest.

Counts exclude bin and obj. A method is counted by a declaration-shaped regular
expression; local functions, constructors and expression variants can make the
number differ from a compiler symbol count.

## Project comparison

| Project | Current mission | Shape | Main problem | Recommended end state |
|---|---|---|---|---|
| Broiler.HtmlBridge.Core | Contracts, script extraction/CSP, diagnostics and logging | 9 files, 1,175 lines | A nominal contract project also owns regex parsing, synchronous I/O, crypto/URL policy and global logging | Keep only contracts/value objects; move host policy/loading out |
| Broiler.HtmlBridge.Dom | Browser DOM/CSSOM/Web API binding and runtime | 65 files, 27,436 lines | Almost all behavior is one partial DomBridge with hidden shared state | Small facade plus document services and feature binding modules |
| Broiler.HtmlBridge.Rendering | Geometry adapter, HTML compatibility rewriting and canvas recording | 3 files, 1,003 lines | Three unrelated concerns grouped by historical convenience | Dissolve after routing each concern to its real owner |
| Broiler.HtmlBridge.Scripting | JS execution and interactive orchestration | 5 files, 682 lines | Duplicated execution/lifetime paths and event-loop ownership split with Dom | One execution pipeline over a per-session browser event loop |

## Broiler.HtmlBridge.Core

### DOM runtime contracts

#### IDomBridgeRuntime

Mission: minimum runtime surface ScriptEngine uses to attach a document, drive
browser work and obtain output.

Properties:

- Csp: current content-security policy.
- TaskCheckpointCallback: callback after host tasks for microtask processing.
- Elements: compatibility enumeration of canonical DOM elements.
- CurrentScriptIndex: diagnostic execution position.
- HasPendingTimers: whether timer/RAF work remains.

Methods:

| Method | Mission | Assessment |
|---|---|---|
| Attach(JSContext, string) | Parse/attach HTML to a JS realm | Leaks the concrete JS engine |
| Attach(JSContext, string, string) | Attach with a document URL | Also combines parsing, realm and navigation context |
| FireWindowLoadEvent() | Complete the load lifecycle | Browser event-loop concern |
| FlushTimerStep() | Run one timer/RAF batch | Browser event-loop concern |
| FlushTimers() | Drain timer/RAF work | Browser event-loop concern; must report cap exhaustion |
| SerializeToHtml() | Serialize the live document | Compatibility output seam |
| GetRenderDocument() | Return the canonical document | Preferred typed output seam |

#### IDomBridgeRuntimeFactory

Method:

- Create(): construct a runtime implementation.

Mission: remove ScriptEngine's compile-time dependency on the concrete bridge.
Keep, but pass a host/session options object when the ResourceLoader, clock and
layout view become injected.

#### DomBridgeRuntimeLimits

Member:

- AsyncDrainIterationLimit: safety cap for async draining.

Mission: prevent unbounded timer/microtask execution. Keep as a configured host
limit; return a diagnostic when reached.

### Script data and execution results

#### ScriptExtractionResult

Properties:

- Scripts
- DeferredScripts
- AsyncScripts

Mission: classify discovered script source text. The model is too lossy for
modules, URLs, nonces, integrity and document order. Replace with a sequence of
metadata-rich ScriptDescriptor values. The file name IScriptExtractor.cs is
misleading because it contains no interface.

#### PageContent

Properties:

- Html
- Scripts
- Url
- DeferredScripts

Mission: host-to-engine input DTO. It duplicates ScriptExtractionResult and
cannot express async/module metadata. Consolidate with the descriptor model.

#### ScriptExecutionResult

Properties:

- Success
- Errors

Mission: diagnostic result for detailed execution.

#### ScriptError

Properties:

- Index
- Message
- StackTrace

Mission: stable error projection which avoids exposing engine exception types.
Keep as a value object; add a drain-limit/cancellation category if needed.

### Scheduling and profiling

#### MicroTaskQueue

Members:

- Count
- Enqueue(Action)
- Drain()

Mission: queue host-created microtasks and collect their exceptions. Ownership
should move to BrowserEventLoop; a queue must be per realm/session, not a mutable
ScriptEngine singleton shared by interactive sessions.

#### ScriptProfilingHook

Members:

- Entries
- Measure(string, Action)
- Clear()

Mission: collect script timings. Keep only if a production or benchmark consumer
exists; otherwise move to host diagnostics. Apply it consistently to interactive
and deferred paths.

#### ScriptTimingEntry

Properties:

- Label
- Elapsed
- Succeeded

Mission: immutable timing result. This is an appropriate diagnostics value type.

### ContentSecurityPolicy

Public properties:

- AllowsEval
- StrictDynamic

Public methods:

| Method | Mission |
|---|---|
| Parse(string) | Parse policy directives |
| AllowsInlineScript(nonce, scriptText) | Evaluate an inline script element |
| AllowsInlineEventHandler(handlerText) | Evaluate an event-handler attribute |
| AllowsInlineStyleAttribute() | Evaluate an inline style attribute |
| AllowsInlineStyleElement(nonce, styleText) | Evaluate a style element |
| AffectsStyles() | Report whether style directives constrain the document |
| AllowsExternalScript(scriptUrl, pageUrl, nonce) | Resolve URL/origin and evaluate external script policy |
| FromHtml(string) | Find and parse meta CSP using regex HTML scanning |
| ExtractNonceFromAttributes(string) | Parse a nonce from raw attribute text |

Private method families:

- effective directive-source selection;
- none/nonce/hash matching and hash computation;
- scheme/absolute URL/same-origin matching and URL resolution;
- raw attribute and meta-tag regex extraction.

Comparison: directive parsing/evaluation is security policy; meta discovery is
Broiler.Dom.Html parsing; URL/origin context belongs to the browser host. The
current class combines all three and is mutable.

### ScriptExtractionService

Public methods:

| Method | Mission | Issue |
|---|---|---|
| Extract(html) | Return normal inline/external script sources | Loses metadata |
| ExtractAll(html, pageUrl) | Classify normal/deferred/async sources | Regex HTML parsing and synchronous I/O |
| DecodeDataUri(dataUri) | Decode embedded source | Duplicated resource-loader concern |
| FetchExternalScript(scriptUrl, pageUrl) | Read file/data/http source | Blocking host/network policy in Core |

Generated regex methods:

- AnyScriptPatternRegex
- DataSrcAttrPatternRegex
- AnySrcAttrPatternRegex
- AnySrcAttrWithValuePatternRegex
- DeferAttrPatternRegex
- AsyncAttrPatternRegex
- ModuleScriptPatternRegex
- ModuleTypeModuleTypeAttributeRegex
- WhitespacePatternRegex

Comparison: Broiler.Dom.Html should discover script elements and attributes.
ResourceLoader should resolve/fetch source. BrowserEventLoop should schedule
classic/defer/async/module execution. Module recognition is currently present
but modules are not executed.

### RenderLogger

Types:

- LogCategory: category enum.
- LogLevel: severity enum.
- RenderLogEntry: timestamp/category/level/context/message/exception value with
  ToString().
- RenderLogger: static global log/event store.

RenderLogger methods:

- Clear()
- Log(category, level, context, message, exception)
- LogDebug(category, context, message)
- LogWarning(category, context, message, exception)
- LogError(category, context, message, exception)

Mission: diagnostic logging. Static mutable state couples sessions and tests.
Inject an ILogger-compatible sink through the host/session.

## Broiler.HtmlBridge.Scripting

### IScriptEngine

Properties:

- StrictModeEnabled
- Csp
- Profiler
- MicroTasks

Methods:

| Method | Mission |
|---|---|
| Execute(scripts) | Execute source without a document result |
| Execute(scripts, html) | Execute and serialize a document |
| Execute(scripts, html, url) | Execute with navigation context |
| Execute(scripts, deferredScripts, html, url) | Execute ordered and deferred sources |
| ExecuteDetailed(scripts) | Return structured diagnostics |
| ExecuteInteractive(scripts, deferredScripts, html, url) | Create a step-driven session |

Assessment: the public interface combines options, host scheduling, diagnostics,
document execution and interactive debugging. Preserve it as a v2 adapter, then
introduce smaller execution/session contracts internally.

### ITypedScriptEngine

Method:

- ExecuteToDocument(scripts, deferredScripts, html, url): return canonical
  Broiler.Dom.DomDocument instead of serialized HTML.

Mission: typed renderer hand-off. This should become the primary internal path;
string-returning overloads adapt by serializing once.

### ScriptEngine

Public methods implement all IScriptEngine and ITypedScriptEngine members above.

Private methods:

| Method | Mission | Proposed owner |
|---|---|---|
| ExecuteCore(...) | Shared realm/runtime execution orchestration | Keep as the one engine pipeline |
| DrainAsyncWork(IDomBridgeRuntime) | Interleave bridge tasks and microtasks | BrowserEventLoop |
| PrepareSource(string) | Apply strict-mode option | Script execution options |
| RegisterRuntimeExtensions(JSContext) | Install engine/host extensions | realm composition |
| RegisterWeakRefPolyfill(JSContext) | Install WeakRef approximation | Broiler.JavaScript implementation |
| RegisterFinalizationRegistryPolyfill(JSContext) | Install finalization approximation | Broiler.JavaScript implementation |

JavaScript callback methods:

- JsScriptEngineQueueMicrotask001Core: enqueue a browser-host microtask.
- JsScriptEngineEval002Core: enforce CSP around eval.
- JsScriptEngineWeakRef004Core: polyfilled WeakRef constructor/behavior.
- JsScriptEngineFinalizationRegistry007Core: polyfilled registry behavior.

Comparison: queueMicrotask and CSP-wrapped eval are browser-realm integration;
ECMAScript WeakRef and FinalizationRegistry belong to the JS engine.

### InteractiveSession

Properties:

- HasPendingWork

Methods:

| Method | Mission |
|---|---|
| Step() | Run the next work batch and return serialized HTML |
| StepDocument() | Run the next batch and return the canonical document |
| CurrentHtml() | Serialize current state without stepping |
| CurrentDocument() | Return current canonical state |
| Complete() | Drain all work and serialize |
| Dispose() | Release realm/runtime state |

Assessment: the typed and serialized methods are parallel views of one session,
which is appropriate. The event loop, CSP and JS context must be session-owned
and disposed even when construction or a step fails.

## Broiler.HtmlBridge.Rendering

### SharedLayoutGeometryProvider

Methods:

| Method | Mission | Problem |
|---|---|---|
| GetGeometry(document, viewportWidth, viewportHeight, baseUrl) | Render/layout a document and return element BoxGeometry | Cache omits base URL/version; provider owns an undisposed HtmlContainer |
| TryGetGeometry(document, element, viewportWidth, viewportHeight, out geometry, baseUrl) | Convenience lookup | Catches broad failures and hides the cause |

Disposition: contract/DTOs to Broiler.Layout; headless implementation to
Broiler.HTML.Orchestration or Broiler.HTML.Headless. Make the session disposable
and snapshot-versioned.

### HtmlPostProcessor

Pipeline method:

- Process(html): run every compatibility rewrite in a fixed order.

Transform methods:

- StripScriptTags
- StripCssDataUriBackgrounds
- StripIframeContent
- ReplaceVideoWithPlaceholder
- ReplaceProgressLikeWithPlaceholder
- ReplaceSelectMultipleWithPlaceholder
- StripObjectContent
- StripHiddenTestArtifacts
- StripTables
- StripForms
- RewriteRootSelector
- NeutraliseRedBackgroundImages

Parsing/render-helper methods:

- GetInlineStyleValue
- AppendHorizontalSelectMultipleTracks
- AppendVerticalSelectMultipleTracks
- AppendSelectMultipleChrome
- ResolveProgressLikeValueRatio
- ReadNumericAttribute

Mission: prepare HTML for a renderer which cannot yet represent every feature,
plus several Acid/WPT-specific visual substitutions. These transforms delete or
replace valid content, so they are neither canonical HTML parsing nor DOM
behavior. Replace them with native renderer support; isolate any remaining test
policy under WPT/CLI.

### CanvasDrawCommandType

Mission: enumerate recorded 2D canvas operations: rectangle/path/text drawing and
save/restore.

### CanvasDrawCommand

Mission: mutable property-bag representation for one recorded command, including
coordinates, style, path/text and global-alpha fields.

### CanvasRenderingContext2D

State:

- Width, Height
- FillStyle, StrokeStyle, LineWidth
- Font, TextAlign, GlobalAlpha
- Commands

Methods:

- FillRect
- StrokeRect
- ClearRect
- BeginPath
- MoveTo
- LineTo
- Arc
- ClosePath
- Fill
- Stroke
- FillText
- StrokeText
- Save
- Restore

Nested CanvasState stores the save/restore snapshot.

Mission: JS-facing canvas state plus command recording. No repository reader of
Commands was found in this baseline. Keep the binding internal and bound memory;
promote an immutable display list to Broiler.Graphics only after a real renderer
consumer exists.

## Broiler.HtmlBridge.Dom declared types

### Primary types

| Type | Mission | Proposed shape |
|---|---|---|
| DomBridge | Public runtime/facade plus nearly every browser API implementation | Source-compatible facade over session/services/modules |
| DomBridgeFactory | IDomBridgeRuntimeFactory implementation | Composition entry point |
| FormElementsCollection | JS live collection for form-associated controls | Forms feature binding |
| ComputedStyleEngineScope | Cache/scope around CSS.Dom style engine | DocumentStyleContext |
| BridgeSelectorStateProvider | Adapt pseudo/state queries to selector engine | CSSOM/selector binding |
| CssStyleDeclaration | JS inline declaration facade | CSSOM binding over one canonical inline state |
| CssRuleStyleDeclaration | JS rule declaration facade | CSSOM binding |
| BridgeDomRange | JS Range wrapper over canonical DomRange | Traversal/Range binding |
| KeyframeEntry | Bridge animation keyframe value | typed model in Broiler.CSS |
| CheckLayoutAssertion | Test assertion DTO | WPT/CLI test support |
| AnchorInfo | Resolved anchor geometry and source | Layout internal/read model |
| PositionAreaResolution | position-area query result | Layout |
| PositionAreaRect | position-area rectangle | Layout |
| AxisSelection | position-area axis selection | CSS typed value or Layout internal |
| KeywordAxis | position-area keyword axis | CSS typed value |

### Runtime-state types

| Type | Current mission | Recommended owner |
|---|---|---|
| EventListenerRegistration | listener/capture/once/passive tuple | EventTargetRegistry |
| ElementRuntimeState | aggregate state and access point for almost every feature | delete after concern-specific stores |
| FormControlRuntimeState | value, checked/selection and validation state | Forms binding/service |
| ScrollRuntimeState | scroll offsets and bounds | ScrollController |
| DialogRuntimeState | open/modal/popover/top-layer flags | TopLayerManager |
| ShadowRuntimeState | shadow root/mode/delegates-focus state | Shadow DOM binding, canonical neutral parts in Dom |
| StyleSheetRuntimeState | sheet/rule/owner/disabled state | StyleSheetRepository |
| DocumentRuntimeState | viewport/document-specific browser state | BrowserDocumentSession |
| AnimationRuntimeState | animation instances/timing/play state | Animation service; sampling in Layout |
| DocumentTypeRuntimeState | doctype shadow data | delete in favor of DomDocumentType |
| RuntimeValue<T> | value plus explicit/set-state marker | replace with feature-specific typed state |

ElementRuntimeState is held in a static ConditionalWeakTable keyed by canonical
nodes. Weak keys prevent some leaks but do not make process-global session state
an acceptable ownership model.

## Broiler.HtmlBridge.Dom file and method-family catalog

### Root/facade files

| File | Lines | Approx. methods | Mission | Proposed disposition |
|---|---:|---:|---|---|
| DomBridge.cs | 1,001 | 44 | construct/attach document, realm globals, lifecycle, timers, viewport and shared state | facade + BrowserDocumentSession + BrowserEventLoop |
| DomBridgeFactory.cs | 8 | 1 | create runtime | keep composition root |
| DomBridge.ComputedStyleEngine.cs | 140 | 6 | CSS.Dom scopes, inline source and invalidation | DocumentStyleContext |
| DomBridge.Selectors.cs | 43 | 3 | canonical selector parser/engine adapter | selector binding; neutral work already in CSS |
| DomBridge.Serialization.cs | 947 | 45 | DOM serialization and render compatibility transforms | Dom.Html serializer + non-destructive RenderDocumentProjector |

### Browser behavior and data files

| File | Lines | Approx. methods | Method-family mission | Proposed disposition |
|---|---:|---:|---|---|
| AnimationResolver.cs | 765 | 31 | collect keyframes, parse timing, interpolate/apply snapshots | syntax to CSS; sampling/application to Layout |
| Attributes.cs | 416 | 25 | attribute/property reflection, namespaces and mutation side effects | Node/Element binding; neutral reflection tables where reusable |
| CheckLayoutAssertions.cs | 113 | 6 | evaluate data-expected-* geometry assertions | WPT/CLI test support |
| Css.cs | 783 | 32 | computed style, CSS values, variables, inheritance and style conversion | CSS/CSS.Dom for neutral work; CSSOM adapter remains |
| ElementInterfaces.cs | 433 | 1 | install large Element/HTMLElement interface source | split feature modules |
| ElementRuntimeState.cs | 221 | 6 | all per-element browser runtime stores | concern-specific session stores |
| Events.cs | 313 | 12 | listener registration/dispatch and event propagation | EventTargetRegistry/dispatcher |
| HitTesting.cs | 534 | 26 | elementFromPoint/top-order/visibility geometry | Layout hit-test read model + thin binding |
| HtmlTreeBuilding.cs | 71 | 1 | create bridge/canonical document nodes | Broiler.Dom.Html plus binding factory |
| JsNative.cs | 31 | 8 | native callback wrappers/conversion helpers | common JS binding utilities |
| JsObjects.cs | 894 | 2 | generate/install JS object model and interface source | split feature bindings; external assets |
| LayoutMetrics.cs | 2,269 | 124 | rectangles, offsets, client/scroll dimensions, viewport and zoom | Layout snapshot + GeometryFacade/ScrollController |
| Messaging.cs | 504 | 24 | MessageChannel/MessagePort/postMessage scheduling | Messaging binding + BrowserEventLoop/BrowsingContextManager |
| ShadowDom.cs | 116 | 10 | attach/query shadow roots and composed relations | Shadow binding; neutral algorithms in Dom |
| SharedLayoutGeometry.cs | 85 | 3 | call/cached shared renderer geometry | ILayoutView GeometryFacade |
| StyleSheets.cs | 918 | 15 | stylesheet/rule JS identity, parsing, imports, mutation and loading | StyleSheetRepository + CSSOM binding + ResourceLoader |
| SubDocumentObjects.cs | 262 | 3 | create Window/Document objects for child contexts | BrowsingContextManager and frame binding |
| SubDocuments.cs | 1,390 | 44 | iframe/object document loading, parsing, origins and lifecycle | BrowsingContextManager + ResourceLoader + Dom.Html |
| Traversal.cs | 707 | 23 | TreeWalker/NodeIterator/Range wrappers and conversion | Traversal/Range binding over Broiler.Dom |
| Utilities.cs | 1,373 | 52 | doctype, URLs/MIME/data, tree/clone, forms/tables, CSS declarations, classList, storage, canvas, SVG and exceptions | distribute by consuming feature; delete duplicates |

### Anchor/position/layout files

| File | Lines | Approx. methods | Method-family mission | Proposed disposition |
|---|---:|---:|---|---|
| AnchorCenter.cs | 94 | 2 | center an anchored box | Layout |
| AnchorFunctions.cs | 307 | 8 | evaluate anchor()/anchor-size() values | grammar/typed values to CSS; used values to Layout |
| AnchorRegistry.cs | 408 | 10 | discover anchor names and computed-style inputs | CSS.Dom style input + Layout registry |
| AnchorResolver.cs | 222 | 5 | orchestrate anchor resolution passes | Layout |
| ContainingBlocks.cs | 81 | 3 | determine containing blocks | Layout |
| CssCleanup.cs | 202 | 5 | rewrite/clean CSS after resolved geometry | eliminate via native Layout; interim render pass |
| Dialogs.cs | 349 | 9 | dialog/top-layer positioning interactions | TopLayerManager + Layout |
| FixedPosition.cs | 87 | 2 | fixed-position correction | Layout |
| Helpers.cs | 132 | 6 | geometry/value helper functions | CSS or Layout according to input/output |
| InlineContainingBlocks.cs | 600 | 19 | inline fragments and containing rectangles | Layout fragmentation/read model |
| PositionArea.cs | 752 | 10 | parse/map/apply position-area | typed syntax to CSS; placement to Layout |
| PositionAreaQueries.cs | 111 | 4 | query resolved position-area values | Layout read model |
| PositionTry.cs | 329 | 9 | collect and choose @position-try fallbacks | rules in CSS; fit/placement in Layout |
| ScrollSimulation.cs | 184 | 5 | approximate scroll/overflow effects | Layout + ScrollController |
| StickyPositioning.cs | 167 | 8 | sticky constraints and positions | Layout |
| Visibility.cs | 251 | 8 | visibility/clipping/in-view geometry | Layout |

### JavaScript registration files

| File | Lines | Approx. methods | Mission | Proposed module |
|---|---:|---:|---|---|
| Registration/Animations.cs | 108 | 4 | register animation APIs | Animation |
| Registration/Console.cs | 40 | 1 | register console | Window/Host diagnostics |
| Registration/Document.cs | 185 | 4 | register Document interfaces/globals | Document |
| Registration/Events.cs | 182 | 1 | register Event/EventTarget/observers | Events |
| Registration/Fetch.cs | 691 | 1 | embed/register fetch stack and helpers | Network + ResourceLoader |
| Registration/Polyfills.cs | 312 | 2 | install browser compatibility polyfills | realm assets by owning module |
| Registration/Registration.cs | 56 | 1 | top-level registration orchestration | DocumentBindingFactory |
| Registration/Traversal.cs | 57 | 1 | register traversal/range APIs | Traversal/Range |
| Registration/Window.cs | 197 | 5 | register Window/timers/viewport globals | Window + BrowserEventLoop |
| Registration/XmlHttpRequest.cs | 436 | 1 | embed/register XHR | Network + ResourceLoader |

### JavaScript callback files

These 14 files contain 409 unique numbered callback-core methods. Each callback
converts Arguments/JSValue, calls bridge behavior and constructs a JS-visible
result. Registration and matching callbacks should be one module.

| File | Lines | Approx. methods | Callback mission | Proposed module |
|---|---:|---:|---|---|
| JsFunctionCallbacks/Attributes.cs | 119 | 8 | attribute APIs | Node/Element |
| JsFunctionCallbacks/Callback.cs | 61 | 5 | generic callback invocation | shared binding runtime |
| JsFunctionCallbacks/Common.cs | 154 | 9 | common conversion/error behavior | shared binding runtime |
| JsFunctionCallbacks/Css.cs | 44 | 2 | CSS entry points | CSSOM |
| JsFunctionCallbacks/ElementInterfaces.cs | 868 | 70 | HTMLElement/SVG/form/table/etc. methods | split Element, SVG, Forms and Tables |
| JsFunctionCallbacks/Events.cs | 64 | 5 | events/observers | Events |
| JsFunctionCallbacks/JsObjects.cs | 1,634 | 115 | Node/Document/Element/geometry/object APIs | split Node, Document, Element and Geometry |
| JsFunctionCallbacks/Messaging.cs | 196 | 13 | ports/channels/postMessage | Messaging |
| JsFunctionCallbacks/Registration.cs | 1,516 | 72 | Window/global/browser APIs | split Window, Document, Network and host modules |
| JsFunctionCallbacks/StyleSheets.cs | 220 | 21 | CSSStyleSheet/CSSRule/declaration APIs | CSSOM |
| JsFunctionCallbacks/SubDocumentObjects.cs | 776 | 30 | child Window/Document/frame objects | Frames/Browsing Context |
| JsFunctionCallbacks/SubDocuments.cs | 43 | 4 | frame/subdocument navigation access | Frames/Browsing Context |
| JsFunctionCallbacks/Traversal.cs | 328 | 21 | Range/TreeWalker/NodeIterator | Traversal/Range |
| JsFunctionCallbacks/Utilities.cs | 536 | 48 | classList, storage, canvas, DOM helpers and exceptions | split Storage, Canvas, Node and shared runtime |

## State and algorithm comparison

| Concern | Current bridge representation | Canonical/target representation | Required migration |
|---|---|---|---|
| Document/tree | canonical nodes plus fake #document-family elements | DomDocument/Fragment/Type only | remove sentinels and tag checks |
| innerHTML | canonical children plus ElementRuntimeState.InnerHtml fallback | canonical children | parse/replace once; delete fallback |
| Inline style | style attribute plus InlineStyle dictionary | one canonical declaration source | write-through transition, then delete duplicate |
| Mutations | DomDocument.Mutated plus manual notifications | one document mutation stream | observer hub subscribes once |
| Event listeners | separate element/window/generic stores | EventTargetRegistry | one target-keyed dispatcher |
| Computed style | CSS.Dom engine plus bridge caches/reconciliations | DocumentStyleContext over CSS.Dom | one invalidation/version authority |
| Geometry | renderer provider plus bridge approximations and caches | versioned LayoutSnapshot | one layout per document version/viewport |
| Anchors/animation | CSS parsing and used-value application both in bridge | typed CSS model + Layout application | split syntax from layout |
| Resources | separate script/style/fetch/XHR/frame URL and I/O logic | injected ResourceLoader | one resolution/origin/load path |
| Browsing contexts | subdocument, window and messaging maps | BrowsingContextManager | explicit tree/lifetime |
| Canvas | unconsumed mutable command list | no recording or Graphics display list | decide consumer first |
| Render compatibility | destructive HTML regex pipeline/live mutations | non-destructive projection/native rendering | isolate and retire passes |

## Important dependency topology finding

Broiler.CSS.Dom references the nested Broiler.CSS/Broiler.DOM checkout while
HtmlBridge references the root Broiler.DOM checkout. At this baseline both
submodules point to the same revision and produce the same Broiler.Dom assembly,
but the solution can build both project paths. The same pattern exists around
nested Graphics dependencies in the HTML tree.

This is a structural drift risk, not a reason to merge DOM ownership into the
bridge. Prefer overridable project paths for standalone submodule builds,
top-level path overrides in the main solution, and a CI revision-equality guard.

## Observed current-worktree build state

The current uncommitted namespace edits move DomBridge to
Broiler.HtmlBridge.Dom and introduce a Broiler.HtmlBridge.DomBridge namespace for
runtime-state types.

- Broiler.HtmlBridge.Scripting builds with zero errors using --no-restore
  (warnings remain).
- Broiler.Wpt.Tests has three CS0118 errors because existing callers use
  Broiler.HtmlBridge.DomBridge as a type while it now resolves as a namespace.

The first roadmap action is therefore to restore the source-compatible public
Broiler.HtmlBridge.DomBridge type before architectural extraction. This
inventory did not modify those in-progress source changes.
