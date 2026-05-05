# Broiler.HtmlBridge Split Evaluation

> **Status**: Draft for team review  
> **Tracking issue**: Evaluate and Refactor Broiler.HtmlBridge for Potential Split

---

## 1. Summary

`Broiler.HtmlBridge` has grown into a broad integration layer rather than a
single-purpose bridge. The current project mixes:

- HTML ingestion (`HtmlTreeBuilder`, `ScriptExtractor`)
- DOM ↔ JavaScript binding (`DomBridge.*.cs`)
- CSS/CSSOM logic (`DomBridge.Css.cs`, `DomBridge.StyleSheets.cs`)
- script execution/runtime policy (`ScriptEngine`, `MicroTaskQueue`,
  `ContentSecurityPolicy`)
- rendering-adjacent adaptation (`HtmlPostProcessor`, `RenderingStages`,
  `ImagePipeline`, anchor/animation resolvers)

### Decision

A **meaningful split is justified**, but **not as an immediate assembly split**.
The current `DomBridge` partials still share too much mutable state for a safe
hard split without churn. The recommended direction is a **seam-first internal
refactor**:

1. keep `DomBridge` and `ScriptEngine` as the public compatibility façade;
2. extract focused internal services/modules behind that façade; then
3. re-evaluate whether separate projects/assemblies are worthwhile once
   dependencies are one-way instead of cyclic.

This gives maintainability and testability gains now without destabilizing the
existing bridge surface.

---

## 2. Current Responsibility Audit

### 2.1 Project-level responsibility groups

| Area | Main files | Current role |
|---|---|---|
| HTML ingest | `HtmlTreeBuilder.cs`, `ScriptExtractor.cs`, `PageContent.cs` | Build a DOM tree from HTML and extract executable script content |
| DOM/JS host | `DomBridge.cs`, `DomBridge.Registration.cs`, `DomBridge.JsObjects.cs`, `DomBridge.Events.cs`, `DomBridge.Traversal.cs`, `DomBridge.Selectors.cs`, `DomBridge.Serialization.cs` | Expose `document`, `window`, elements, events, traversal, timers, and serialization to YantraJS |
| CSS/CSSOM | `DomBridge.Css.cs`, `DomBridge.StyleSheets.cs`, `DomBridge.Utilities.cs`, `CssTextProperties.cs`, `CssBoxModel.cs` | Cascade, computed style, CSSOM objects, stylesheet mutation, box-model helpers |
| Script/runtime orchestration | `ScriptEngine.cs`, `IScriptEngine.cs`, `InteractiveSession.cs`, `MicroTaskQueue.cs`, `ContentSecurityPolicy.cs`, `ScriptProfilingHook.cs`, `RenderLogger.cs` | Create JS contexts, attach the bridge, enforce CSP, drain microtasks/timers, support interactive sessions |
| Rendering adaptation | `RenderingStages.cs`, `HtmlPostProcessor.cs`, `ImagePipeline.cs`, `DomBridge.AnchorResolver.cs`, `DomBridge.AnimationResolver.cs` | Convert post-script DOM state into rendering-friendly output and support rendering-time behaviors |

### 2.2 Largest hotspots

The maintainability concern is real, not hypothetical:

| File | Approx. size | Why it matters |
|---|---:|---|
| `DomBridge.JsObjects.cs` | 8,227 lines | Element/object projection, sub-documents, property wiring, and DOM API behavior are concentrated in one file |
| `DomBridge.AnchorResolver.cs` | 3,522 lines | Anchor and viewport behavior are mixed into the same bridge state model |
| `DomBridge.Css.cs` | 3,380 lines | Cascade, computed style, invalidation, media queries, and CSS value handling live together |
| `DomBridge.Registration.cs` | 2,911 lines | Global registration (`document`, `window`, constructors, XHR) is a major bridge subsystem by itself |
| `DomBridge.Traversal.cs` | 1,995 lines | Tree-walking and range/iterator behavior form another large domain |

### 2.3 Dependencies and coupling

`Broiler.HtmlBridge` currently depends on both engine sides:

- **`Broiler.HTML.Dom`** for shared DOM/tokenizer/serialization primitives
- **`Broiler.HTML` image/rendering projects** for rendering-adjacent helpers
- **`Broiler.JavaScript.All`** for `JSContext`, `JSObject`, built-ins, and
  runtime interop

The strongest internal coupling is inside `DomBridge` itself. Its partial files
share bridge-owned state such as:

- DOM element storage and document roots
- JS object caches
- timer, animation-frame, and event-listener queues
- visual viewport and location state
- stylesheet caches and style invalidation state

That shared mutable state means the current partial split is organizational, not
architectural.

---

## 3. What Can Be Decoupled Today

### 3.1 Low-risk seams already present

These pieces are already close to standalone and can move first:

- `HtmlTreeBuilder`
- `ScriptExtractor`
- `ContentSecurityPolicy`
- `MicroTaskQueue`
- `ScriptProfilingHook`
- `InteractiveSession`

`ScriptEngine` also already provides a useful façade via `IScriptEngine`, which
helps preserve backward compatibility while internals move.

### 3.2 Seams that need extraction before any hard split

The following concerns are currently intertwined and should be separated behind
internal APIs before considering new assemblies:

1. **DOM state ownership**
   - element lists
   - root-document/sub-document bookkeeping
   - JS object identity/cache management

2. **CSS engine responsibilities**
   - selector matching and invalidation
   - inline/computed style resolution
   - stylesheet/CSSOM object creation

3. **window/document host registration**
   - global object wiring
   - DOM constructors/factories
   - fetch/XHR/event constructor exposure

4. **rendering adaptation**
   - serialization transforms
   - post-processing for renderer compatibility
   - anchor/animation/view-viewport behavior that exists only for capture/render paths

---

## 4. Recommended Target Shape

The best next step is **modular decomposition inside the existing project**.

| Proposed module | Responsibility | Keep public surface? |
|---|---|---|
| `HtmlBridge.DomModel` | HTML ingest, DOM state ownership, traversal/serialization primitives | Internal behind `DomBridge` |
| `HtmlBridge.DomHost` | `document`/`window` registration, JS object projection, event wiring | Keep `DomBridge` façade public |
| `HtmlBridge.Css` | selector matching, cascade, computed styles, CSSOM/stylesheets | Internal behind current DOM APIs |
| `HtmlBridge.Runtime` | script execution lifecycle, microtasks, CSP, profiling, interactive stepping | Keep `IScriptEngine`/`ScriptEngine` public |
| `HtmlBridge.Rendering` | post-processing, image pipeline, anchor/animation helpers used by render/capture flows | Mostly internal |

These can start as folders/namespaces or internal classes. A separate assembly
for each area is **not** required up front.

### Recommended non-goal for the first refactor

Do **not** start by splitting `DomBridge` into multiple public types that each
expose partial browser APIs. That would increase integration complexity for
callers while the implementation still depends on shared state.

---

## 5. Refactor Roadmap

### Milestone 1 — Make ownership explicit

- add a bridge-boundary document mapping each `DomBridge.*.cs` file to a single
  subsystem owner
- group files into `Dom`, `Css`, `Runtime`, and `Rendering` folders/namespaces
- keep all existing public types and method signatures unchanged

**Exit criteria**

- no behavior changes
- existing callers still construct `DomBridge` and `ScriptEngine` exactly as today

### Milestone 2 — Extract internal services

- extract bridge-private services for:
  - DOM state/object identity
  - stylesheet/CSSOM management
  - timer/task scheduling
  - event-listener storage/dispatch support
- make `DomBridge` delegate to those services instead of owning every concern directly

**Exit criteria**

- `DomBridge` becomes a coordinator instead of the direct implementation site
- focused tests can target at least some services without booting the full bridge

### Milestone 3 — Separate CSS from DOM host logic

- move CSS cascade/computed-style/CSSOM logic behind a dedicated internal API
- remove direct cross-calls where DOM object projection manipulates CSS internals
- keep `element.style`, `getComputedStyle`, and `document.styleSheets` behavior unchanged

**Exit criteria**

- CSS changes flow through a narrow bridge API instead of shared mutable fields
- DOM host code can be reasoned about without reading CSS implementation files

### Milestone 4 — Isolate rendering-specific helpers

- move `HtmlPostProcessor`, render-only serialization transforms, anchor resolver,
  animation resolver, and image-pipeline helpers behind an explicit rendering adapter
- keep runtime DOM behavior separate from capture/render compatibility code

**Exit criteria**

- non-rendering bridge work no longer needs to touch render-specific helpers
- render-path changes can be tested without broad DOM API regressions

### Milestone 5 — Re-evaluate assembly/project split

Only after Milestones 1-4, decide whether to create additional projects such as:

- `Broiler.HtmlBridge.Runtime`
- `Broiler.HtmlBridge.Css`
- `Broiler.HtmlBridge.Rendering`

**Go/no-go rule:** do not split assemblies until dependencies are primarily
one-way and the compatibility façade can stay additive-only.

---

## 6. Backward Compatibility and Migration Notes

- Preserve `DomBridge` as the integration façade used by current callers.
- Preserve `IScriptEngine`/`ScriptEngine` signatures.
- Prefer internal extraction and delegation over call-site rewrites.
- Keep `InternalsVisibleTo` consumers working until replacement test seams exist.
- Delay namespace or project moves for types that are still consumed broadly by
  `Broiler.Cli`, `Broiler.Wpt`, and existing bridge tests.

---

## 7. Testing Implications

The current tests mostly validate the bridge end-to-end, which is useful for
regression safety but not ideal for modular refactors. The roadmap should add
targeted validation incrementally while keeping the current behavior suites as
the compatibility net.

Suggested validation layers:

1. existing end-to-end bridge suites remain the compatibility gate;
2. new internal tests focus on extracted services once those services exist;
3. rendering-adapter tests stay separate from DOM/CSS host tests where possible.

---

## 8. Final Recommendation

`Broiler.HtmlBridge` **should not remain exactly as-is**, because the current
partial-class structure hides several distinct subsystems inside one stateful
type and one project. However, the right first move is **not** a hard split
into new public modules or assemblies.

The justified plan is:

- **yes** to a split in responsibilities;
- **no** to an immediate assembly breakup;
- **yes** to a staged, backward-compatible refactor that first extracts
  internal seams and only later considers project boundaries.
