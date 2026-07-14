# Browser WebAssembly Roadmap for Broiler.Graphics and Broiler.UI

**Status:** Active; Phase 4 implemented locally, cross-browser and assistive-technology evidence pending  
**Date:** 2026-07-10  
**Scope:** Architecture, component ownership, delivery phases, validation gates,
and production-readiness criteria for running a Broiler application in a
WebAssembly-enabled browser. This document contains no implementation.

Related documents:

- [Broiler.Graphics roadmap](../../Broiler.Graphics/ROADMAP.md)
- [Broiler.UI component roadmap](broiler-ui-component.md)
- [Broiler.Input component roadmap](broiler-input-component.md)
- [Broiler.Media component roadmap](broiler-media-component.md)
- [Linux Graphics/Input roadmap](linux-graphics-input-roadmap.md)
- [UI graphics-submission ADR](../../Broiler.UI/docs/adr/0003-graphics-submission-boundary.md)
- [UI input/text-service ADR](../../Broiler.UI/docs/adr/0004-input-and-text-service-boundary.md)
- [UI accessibility ADR](../../Broiler.UI/docs/adr/0008-accessibility-semantic-bridge.md)
- [UI directory-topology ADR](../../Broiler.UI/docs/adr/0019-directory-structure-topology.md)

## 1. Executive decision

Do **not** begin by broadly changing either `Broiler.Graphics` or `Broiler.UI`.
The current platform-neutral assemblies already target `net10.0`, and the
essential browser seams already exist:

- `Broiler.Graphics` can record and replay drawing into a managed RGBA bitmap;
- `Broiler.UI` records platform-neutral `BRenderList` frames through `IUiHost`;
- `Broiler.Input` has neutral keyboard, mouse, text, touch, and pen records; and
- .NET browser WebAssembly supplies the JavaScript-interoperation boundary.

The first executable browser proof should therefore be an application/sample
host around the existing cores:

```text
Broiler.UI.WebAssembly.Demo
  -> selected Broiler.UI.*.Standard controls
  -> Broiler.UI / Broiler.UI.Standard
  -> Broiler.Graphics BRenderList + BImageRenderer
  -> one browser-host pixel presentation call
  -> HTML canvas
```

For input and host services:

```text
DOM pointer/keyboard/text/composition events
  -> sample-local browser adapter in the proof
  -> neutral Broiler.Input records / UiInputEvent
  -> UiSession

requestAnimationFrame + ResizeObserver + browser capabilities
  -> application-owned IUiHost and optional UI host ports
```

The component decision is:

| Area | First executable proof | Reusable/production support |
|---|---|---|
| `Broiler.Graphics` core | Build and use unchanged | Change only if a neutral font/resource or codec seam is proven necessary |
| Graphics browser presentation | Keep in the sample initially | Extract to `Broiler.Graphics.WebAssembly` if reused or if direct Canvas replay is required |
| `Broiler.UI` core and Standard controls | Use unchanged for the initial interactive slice | Add only neutral improvements proven by IME, async clipboard, file-handle, touch, or accessibility gates |
| UI browser host | Application/sample owned | Keep application owned unless two real consumers prove a reusable host package is worthwhile |
| `Broiler.Input` | Reuse neutral records; proof adapter may be sample-local | Extract browser event sources into `Broiler.Input.*.WebAssembly` implementations |

This means the minimum viable browser application does not require enhancing
either core. If performance demands direct Canvas command replay, the first
reusable component enhancement belongs to the **Graphics family**, as a new
backend rather than a core rewrite. `Broiler.UI` changes become necessary only
for higher support tiers whose browser semantics cannot be expressed correctly
by the current neutral contracts.

The name `Broiler.Browser.Windows` must not be used for a new backend. It is
already the assembly name of the desktop application in
`src/Broiler.Browser.Windows`. Public project names in this work use
`WebAssembly`; `browser-wasm` is reserved for the .NET runtime identifier.

## 2. Support tiers and success claims

“Targets WebAssembly” is too ambiguous to be a useful completion claim. Work is
divided into explicit tiers.

| Tier | Claim | Required evidence |
|---|---|---|
| T0 - browser-buildable libraries | Selected neutral assemblies compile for `browser-wasm` | Restore/build succeeds with no browser-incompatible dependency in the graph |
| T1 - graphics proof | Managed Broiler drawing is visible in a real browser canvas | Executable browser publish, deterministic frame, resize/DPR tests |
| T2 - interactive UI preview | Standard controls accept pointer, keyboard, and basic text input | Browser UI host, frame scheduler, focus/capture, input replay, interactive demo |
| T3 - application preview | A selected real Broiler application workflow runs in supported browsers | Text/IME, clipboard, accessibility baseline, lifecycle, performance and packaging evidence |
| T4 - production browser support | The published support matrix is complete and evidence-backed | Cross-browser, mobile if claimed, assistive-technology, security, soak, payload and performance gates |

T0 is not T1: a library build does not prove browser execution. T1 is not T2:
pixels on a canvas do not prove input, focus, scheduling, or text services. T2 is
not T4: an interactive demo does not prove accessible or permission-correct
browser behavior.

The first development lanes are desktop Chromium and Firefox. T2 completion adds
the automated WebKit lane; T3 adds real Safari release evidence and one selected
application. Mobile and full T4 parity remain separate approval decisions.

### 2.1 Application scope

There are two materially different “Broiler application” targets:

1. **UI application track:** a Writer-like or purpose-built application using
   Broiler.UI, Graphics, Input, and application/domain libraries. The estimates
   in Phases 0-6A and 7 cover this track.
2. **Full Broiler browser-engine track:** port `Broiler.Browser.Windows`, including
   HTML.Graphics, DOM, `Broiler.JavaScript.All`, HtmlBridge, page loading,
   persistence, and web security policy. This is not merely a UI/Graphics host
   port and receives a separate Phase 6B audit/workstream.

The full engine graph currently includes dynamic-code paths based on
`DynamicMethod`/`System.Reflection.Emit`, filesystem-backed modules/caches and
favorites, and networking that must be reviewed for asynchronous browser use,
CORS, and same-origin policy. None of those problems is solved by targeting
Broiler.UI or Broiler.Graphics successfully. Unless Phase 6B is explicitly
approved and completed, the T3 “application preview” claim means the UI
application track, not an arbitrary-web browser running inside another browser.

## 3. Current repository evidence

### 3.1 Browser-RID builds already pass

With .NET SDK 10.0.301 and the .NET 10 `wasm-tools` workload, the following were
validated on 2026-07-10:

```powershell
dotnet build Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj `
  -c Release -r browser-wasm

dotnet build Broiler.UI/src/Foundation/Broiler.UI/Broiler.UI.csproj `
  -c Release -r browser-wasm

dotnet build Broiler.UI/src/Foundation/Broiler.UI.Standard/Broiler.UI.Standard.csproj `
  -c Release -r browser-wasm
```

All three builds completed with zero warnings and zero errors after restore.
The Graphics build also covered its managed Media/Image dependency chain. This
is strong T0 evidence, but it is not an executable browser publish or runtime
test. It also does not yet prove every selected `.Standard` control or optional
document/codec dependency; Phase 0/1 must build and publish the exact sample
dependency closure.

The core Graphics project is already marked safe, trimmable, and AOT-compatible.
UI and its neutral Input dependencies target plain `net10.0` and contain no
Windows backend reference.

### 3.2 Existing Graphics seams are sufficient for T1

`IBroilerRenderer` already owns surface creation, image resources, render-list
replay, and offscreen rendering. `BRenderCommand` is a closed command hierarchy
covering:

- solid fill and stroke rectangles;
- rounded fill and stroke rectangles;
- text;
- images;
- rectangular clip push/pop; and
- transform push/pop.

`BImageRenderer` replays all current commands into `BImageSurface` and
`BBitmap`. `BBitmap` exposes tightly packed straight-alpha RGBA data and can
return a no-copy `BPixelBuffer` view. This is the correct shape for a
correctness-first browser `ImageData` presentation path.

No new render command, visitor, surface interface, or browser TFM is required
for the first proof.

### 3.3 Existing UI seams are sufficient for basic T2

`IUiHost` already supplies:

- logical viewport size;
- device scale;
- render-list creation;
- invalidation notification; and
- frame presentation.

`UiSession.RenderFrame` measures, arranges, records, validates, and presents a
frame through that host. `UiSession.DispatchInput` already routes pointer input
through hit testing/capture and routes keyboard/text/composition input to the
focused element.

The browser host can implement `IUiDispatcher` with the browser synchronization
context or a queued microtask and can drive `StandardAnimationScheduler` from
`requestAnimationFrame`. No UI timer or native window is needed.

### 3.4 Known gaps are support-tier gaps, not proof blockers

The investigation found the following concrete limitations:

| Gap | Affected tier | Initial treatment |
|---|---|---|
| System font discovery scans desktop filesystem locations | Text-quality part of T2/T3 | Use block fallback for the proof; choose browser Canvas text or a neutral injectable font source before T3 |
| Current clipboard port is synchronous | T3 | Use trusted DOM clipboard events for T2; design an optional neutral async capability only if T3 needs programmatic access |
| `UiFileDialog` and `StandardFileDialog` are path/directory based | Optional T3/T4 feature | Exclude `FileDialog.Standard`; use an application-owned browser picker; never invent a host filesystem path |
| Composition selection fields are not fully projected into `UiInputEvent` | Robust IME in T3 | Preserve the fields through a small neutral UI fix before claiming IME parity |
| Focus changes do not define a full browser text-input lifecycle | Robust IME/mobile keyboard in T3/T4 | Add neutral focus/text-context notifications if the hidden-textarea proof demonstrates the need |
| Semantic data is primarily descriptive, not a complete action bridge | Accessibility in T3/T4 | Mirror a passive tree for the proof; add stable IDs/actions/focus routing before a production claim |
| Touch/pen detail is not preserved through all UI routing | Mobile/pen T4 | Map the primary pointer to mouse for T2; enhance the neutral routed-pointer model before claiming native touch/pen behavior |
| `UiInputEvent` has no pointer-cancel/leave/capture-lost event and drops keyboard repeat/location metadata | T2 cancellation and advanced keyboard behavior | Use a tested synthetic release/outside-move cleanup for T2; extend the neutral routed-event model only when that is insufficient |
| Mobile `beforeinput.inputType` operations cannot be represented as more than inserted text | Mobile text editing in T4 | Use controlled hidden-editor reconciliation or add neutral edit operations before a mobile claim |
| Image bridge contains sync-over-async code; lossy WebP uses Windows WIC | Codec part of T3 | Run browser runtime tests and publish an honest codec matrix |

These gaps must not be used to justify a speculative browser-specific API in a
neutral core. Each core change is gated by a failing browser scenario and must
improve the neutral model for every host.

## 4. Goals

1. Publish a standalone .NET 10 browser WebAssembly application that renders a
   Broiler UI into an HTML canvas.
2. Reuse the existing `BRenderList`, managed rasterizer, UI tree, layout,
   controls, and neutral input records.
3. Keep all DOM, JavaScript, browser permission, origin, canvas-element, and
   browser lifecycle types outside neutral runtime assemblies.
4. Coalesce UI invalidations and render only through `requestAnimationFrame`.
5. Support logical CSS-pixel layout and correct canvas backing dimensions at
   fractional and high device-pixel ratios.
6. Separate keyboard transitions, committed text, and composition data.
7. Preserve browser pointer cancellation and capture semantics so controls do
   not remain pressed or captured after blur or visibility loss.
8. Provide an evidence-backed text, clipboard, file, accessibility, codec, and
   browser support matrix.
9. Measure the CPU-presentation path before creating a direct browser renderer.
10. Extract reusable WebAssembly implementation assemblies only after their
    ownership and second-consumer value are clear.
11. Preserve all desktop Graphics, Input, and UI tests throughout the work.
12. Keep interpreter, trimming, and AOT choices measurable rather than assuming
    AOT is always the best browser deployment.

## 5. Non-goals

- Porting Direct2D, D3D, DXGI, DirectWrite, Win32, X11, EGL, OpenGL, Vulkan, or
  `evdev` into the browser.
- Making `BWindow.Run()` the browser event loop. Browser hosting uses `UiSession`
  and an application-owned frame scheduler directly.
- Translating each Standard control into a native DOM widget.
- Adding Blazor as a required dependency. The first host is a standalone .NET
  WebAssembly Browser App; embedding it in Blazor may be demonstrated later.
- Adding WebGL or WebGPU before Canvas 2D and CPU-presentation measurements show
  a real need.
- Claiming native filesystem access or returning fabricated local paths.
- Claiming full accessibility merely because an ARIA-like hidden DOM tree exists.
- Claiming mobile support from primary-pointer-to-mouse compatibility alone.
- Requiring multithreaded WebAssembly, `SharedArrayBuffer`, cross-origin
  isolation, or a worker in the first release.
- Referencing `Broiler.UI.All` in the first sample. The sample selects controls
  explicitly so unsupported file/font dialog behavior is not implied.
- Adding solution-wide `Debug-WebAssembly`/`Release-WebAssembly` configurations
  before the dedicated project and CI workflow prove they are useful.

## 6. Ownership and component-change matrix

| Responsibility | Initial owner | Long-term owner | Neutral core change permitted? |
|---|---|---|---|
| Render commands and managed CPU replay | Existing `Broiler.Graphics` | Existing `Broiler.Graphics` | Only for a proven neutral font/codec/resource deficiency |
| Bounded image decode and codec behavior | Existing `Broiler.Media.Image.Managed` plus caller policy | Media codecs enforce supplied limits; application chooses browser budgets | Add neutral decode-limit propagation where current codecs cannot enforce pre-allocation limits |
| RGBA-to-canvas presentation | WebAssembly sample host | Optional `Broiler.Graphics.WebAssembly` | No |
| Direct Canvas 2D replay | Not in first proof | `Broiler.Graphics.WebAssembly` or `.Canvas2D` | Only if existing render contracts cannot express a command |
| UI tree, controls, focus, layout, semantics | Existing `Broiler.UI*` | Existing `Broiler.UI*` | Only for neutral behavioral gaps |
| Frame scheduling and resize observation | WebAssembly application host | Application host | No |
| DOM event subscription and DTO validation | WebAssembly sample host | `Broiler.Input.WebAssembly` shared support if extracted | No |
| Keyboard normalization | Sample adapter | `Broiler.Input.Keyboard.WebAssembly` | Small neutral key/modifier additions only if required |
| Pointer normalization | Sample adapter | `Broiler.Input.Mouse.WebAssembly`, `Broiler.Input.Touch.WebAssembly`, and `Broiler.Input.Pen.WebAssembly` | Neutral routed-pointer enhancement only for T4 |
| Text/composition source | Sample hidden-textarea bridge | `Broiler.Input.Text.WebAssembly` | Preserve missing neutral composition data if required |
| Clipboard, cursor, settings, caret | Application-owned optional UI host ports | Application host | Optional async neutral capability after proof |
| DOM accessibility projection | Application host | Application host | Stable semantic identity/action contracts may be added neutrally |
| File open/save | Application service | Application service or neutral async resource-picker capability | Only if cross-platform resource selection becomes UI scope |
| Deployment, service worker, caching, CSP | WebAssembly application | WebAssembly application | No |

### 6.1 Rules for changing a core

A browser finding may change `Broiler.Graphics`, `Broiler.Input`, or `Broiler.UI`
only when all of the following are true:

1. A checked-in browser test demonstrates that the existing neutral contract
   cannot represent the required behavior correctly.
2. The change is platform-neutral and contains no DOM/JS/canvas/browser type.
3. Existing Windows, Linux, CPU, and UI tests continue to pass.
4. The design has a non-browser explanation, such as asynchronous host
   capability, injectable font source, stable semantic action, or unified
   pointer data.
5. An ADR is added when the public contract or ownership boundary changes.

Browser convenience alone is not enough.

## 7. Target project and dependency topology

### 7.1 First proof

```text
Broiler.UI/
  samples/
    WebAssembly/
      Broiler.UI.WebAssembly.Demo/
        Broiler.UI.WebAssembly.Demo.csproj
        Program.cs
        BrowserUiHost.cs
        BrowserInputBridge.cs
        BrowserTextInputBridge.cs
        wwwroot/
          index.html
          broiler.ui.webassembly.js
```

The proof project is allowed under `samples/` by the UI topology rules. Do not
add a platform-specific runtime project under `Broiler.UI/src`; ADR 0019 keeps
platform hosting in samples/hosts or outside UI runtime assemblies.

The sample references only:

- `Broiler.Graphics`;
- `Broiler.Input` and the specific neutral input contracts;
- `Broiler.UI` and `Broiler.UI.Standard`; and
- explicitly selected control abstraction/Standard pairs.

It must not reference Windows or Linux implementation assemblies.

### 7.2 First real application

Use a separate application project:

```text
src/
  Broiler.App.WebAssembly/
```

Do not initially add `browser-wasm` to `src/Broiler.Browser.Windows`. Its current
plain `net10.0` condition selects Linux Graphics/Input providers, and its
assembly name is already `Broiler.Browser.Windows`. A separate composition root
avoids native dependency leakage and naming ambiguity.

### 7.3 Conditional reusable projects

Extract these only after the proof supplies evidence:

```text
Broiler.Graphics/
  Broiler.Graphics.WebAssembly/
  Broiler.Graphics.WebAssembly.Tests/

Broiler.Input/
  Broiler.Input.WebAssembly/
  Broiler.Input.Keyboard.WebAssembly/
  Broiler.Input.Mouse.WebAssembly/
  Broiler.Input.Text.WebAssembly/
  Broiler.Input.Touch.WebAssembly/       # T4 only
  Broiler.Input.Pen.WebAssembly/         # T4 only
```

`Broiler.Input.WebAssembly` would contain shared DOM-event lifetime, clock, and
DTO-validation support, not all device behavior. Per-kind projects follow the
existing Windows/Linux implementation pattern.

A reusable UI WebAssembly runtime assembly is not planned. If two applications
eventually duplicate substantial `IUiHost`, text, clipboard, and semantic bridge
logic, propose a host-integration package in a separate ADR and update topology
tests deliberately. Do not create it preemptively.

Register projects in their owning solutions when they are created:

- the sample in `Broiler.UI/Broiler.UI.slnx` and root `Broiler.slnx`;
- Graphics backend/tests in `Broiler.Graphics/Broiler.Graphics.sln` and root
  `Broiler.slnx`;
- Input implementations/tests in `Broiler.Input/Broiler.Input.slnx` and root
  `Broiler.slnx`; and
- the application in root `Broiler.slnx`.

Do not add placeholder solution entries for conditional projects before their
decision gate fires.

## 8. Browser runtime and JavaScript boundary

Use the .NET 10 standalone WebAssembly Browser App model and
`System.Runtime.InteropServices.JavaScript` (`JSImport`/`JSExport`). Blazor may
host the result later but must not become a Graphics/UI dependency.

The JavaScript boundary should be narrow:

- one initialization call that binds a canvas and host callbacks;
- one resize/DPR observation stream;
- one coalesced presentation call per CPU-rendered frame;
- one batched command submission per frame if direct Canvas replay is selected;
- compact typed input DTOs;
- explicit start/stop for event listeners and observers; and
- asynchronous calls only for genuinely asynchronous browser capabilities.

Rules:

1. Validate every numeric value arriving from JavaScript. Reject non-finite
   sizes, coordinates, deltas, pressure, scale, and timestamps.
2. Preserve JavaScript object/module handles for the host lifetime and dispose
   them deterministically.
3. Do not make one interop call per pixel or, for direct replay, per render
   command. Batch at the frame boundary.
4. Do not call `UiSession.RenderFrame` recursively from DOM callbacks. Input
   invalidates; the scheduled animation frame renders.
5. Event listeners, observers, pointer capture, and pending callbacks must be
   removed during disposal or navigation.
6. Browser APIs remain behind supported-platform annotations in WebAssembly
   implementation/application projects, never neutral libraries.

## 9. Graphics architecture

### 9.1 Stage A - CPU-rendered canvas presentation

This is the correctness-first path:

```text
BRenderList
  -> persistent BImageRenderer
  -> persistent BImageSurface
  -> surface.Bitmap.ToPixelBuffer(copy: false)
  -> one managed-to-JS frame handoff
  -> ImageData / putImageData
  -> canvas
```

Implementation requirements:

- reuse one renderer and surface rather than calling `RenderToImage` each frame,
  because that API returns an extra bitmap copy;
- cache the no-copy pixel view until resize;
- recreate the cached view after `BImageSurface.Resize`, which replaces its
  bitmap;
- set CSS canvas dimensions to logical viewport units;
- set backing dimensions directly from `BImageSurface.Bitmap.Width/Height`, or
  use the identical `Ceiling(logical * scale)` rule;
- suspend presentation while the observed container is zero-sized instead of
  passing zero dimensions to `BImageSurface`;
- reject excessive logical dimensions, DPR, backing-pixel counts, and frame-byte
  sizes before resize/allocation, retaining or safely recreating the last valid
  surface;
- support DPR 1, 1.25, 1.5, 2, browser zoom, and runtime monitor changes;
- cross the JS boundary once per frame;
- retain/release image handles deterministically; and
- create the proof surface explicitly as RGBA, define `{ alpha: false }` versus
  transparent Canvas context behavior, clear-alpha handling, and opaque alpha
  normalization; and
- compare exact checksums against the managed RGBA buffer, using tolerance for
  translucent Canvas readback where premultiply/unpremultiply can differ.

At 1920x1080 DPR 1, an RGBA frame is approximately 7.9 MiB. At 60 frames per
second, the raw transfer rate is approximately 475 MiB/s before extra interop or
`ImageData` copies. At DPR 2, a full backing frame is approximately 31.6 MiB.
For that reason the CPU path is a proof and correctness reference first; its
production suitability is a measured decision. `copy: false` removes one
managed clone only. Managed-to-JS marshaling, `Uint8ClampedArray`/`ImageData`,
and Canvas storage can still copy the full frame and must be measured separately.

### 9.2 Stage B - direct Canvas 2D replay

Create `Broiler.Graphics.WebAssembly` only when at least one of these gates fires:

- a second application or direct Graphics consumer needs the presenter;
- CPU frame transfer misses the Phase 5 performance/memory thresholds; or
- browser-native text is selected and a coherent Canvas renderer becomes the
  simpler ownership model.

The current closed command model maps naturally:

| Broiler command | Canvas 2D operation |
|---|---|
| `FillRect` | `fillRect` |
| `StrokeRect` | `strokeRect` with explicit line width/alignment tests |
| `FillRoundedRect` | `roundRect` path plus `fill` |
| `StrokeRoundedRect` | `roundRect` path plus `stroke` |
| `DrawText` | `fillText` with an agreed top/origin-to-baseline conversion |
| `DrawImage` | `drawImage` with source, destination, and opacity |
| `PushClip` / `PopClip` | Maintain an independent logical clip stack and reconstruct Canvas state as required |
| `PushTransform` / `PopTransform` | Maintain an independent matrix stack and apply the current matrix with `setTransform`/equivalent |

Serialize/batch the frame and replay it in one JavaScript invocation. Keep the
CPU renderer as:

- the deterministic snapshot oracle for the agreed command/transform/text
  subset;
- the `RenderToImage` implementation/fallback;
- a whole-frame fallback when the native replay planner encounters any
  unsupported command/state combination; and
- an artifact generator for backend comparisons.

Do not implement clip and transform pops as two naive uses of Canvas
`save`/`restore`. Broiler validates them as independent stacks and permits an
interleaving such as `PushClip`, `PushTransform`, `PopClip`, draw,
`PopTransform`. A browser replay-state manager must retain the transform active
after that clip pop, restoring a baseline Canvas state and reapplying current
logical transforms/clips (with the transform captured at each clip push) when
necessary. Do not tighten valid Broiler command ordering merely to match the
single Canvas state stack.

Before treating the CPU renderer as a direct-Canvas oracle, decide transform
semantics for rotation, shear, negative scale, rounded rectangles, images, and
transformed clips. The current CPU renderer often transforms rectangle corners
and rasterizes an axis-aligned bounding box, while native Canvas transforms the
geometry. Choose and test one policy:

- emulate the existing CPU bounding-box semantics in the browser backend;
- define native geometric transforms as canonical and neutrally fix the CPU
  renderer; or
- limit initial direct-replay conformance to translation/axis-aligned scaling
  and document unsupported/different transform cases.

Until that decision lands, CPU artifacts are exact oracles only for the agreed
semantic subset, not for arbitrary transformed geometry. A mixed per-command
CPU/Canvas fallback is out of scope unless a real hybrid compositor is designed;
fallback rejects the native plan and replays the entire frame through CPU.

Before Stage B, also choose a synchronous image-resource realization compatible
with `IBroilerRenderer.CreateImage`: for example reusable `ImageData` backed by
an offscreen/resource canvas, or an eagerly uploaded resource canvas. An
unresolved `createImageBitmap` promise must never make a synchronous Render omit
an image silently. A separately gated async image-resource API is the alternative.

Publish a capability matrix for every `BRenderOptions` and
`BSurfaceDescriptor` field. Browser RAF owns synchronization, and the CPU/browser
paths may ignore or reinterpret antialias, VSync, subpixel text, pixel format,
and transparency options; support must be explicit rather than implied.

WebGL/WebGPU is deferred until measured Canvas 2D results fail a documented
requirement.

### 9.3 Text and fonts

The managed fallback currently discovers real fonts through desktop filesystem
paths. Browser-installed fonts are not visible through that mechanism, so the
initial proof will use the built-in block glyphs unless packaged fonts are wired
explicitly.

Choose one T3 strategy after measuring both:

**Route A - browser-native Canvas text (recommended default)**

- load approved web fonts before starting the UI session;
- render through Canvas `fillText`;
- implement `IBTextMetricsProvider` through cached Canvas `measureText` results;
- register the provider before first layout; and
- prohibit late font activation from silently changing layout after interaction.

This can avoid a Graphics-core change, but browser-native text cannot simply be
painted over an already completed CPU framebuffer without breaking z-order,
clips, transforms, and translucent composition. Route A therefore requires
complete direct Canvas replay for every text-bearing frame, or a separately
designed/tested segmented compositor.

The existing text-metrics provider is process-global. Combining Canvas metrics
with a runtime CPU fallback that draws block or packaged glyphs immediately
creates two inconsistent text environments. Before Route A retains a CPU
fallback, require one of:

- every text-bearing runtime frame is fully Canvas-supported;
- CPU and Canvas use the same packaged font/metrics;
- metrics become renderer/session scoped through a neutral Graphics change; or
- CPU is an offline artifact/reference renderer, not a runtime fallback.

**Route B-lite - pinned packaged fonts in the managed renderer**

- package licensed TTF/WOFF font bytes;
- add a neutral font/glyph resolver injected into `BImageRenderer`;
- use the identical resolver for drawing and measurement; and
- support a restricted, documented script/family/style set such as pinned Latin
  regular/bold plus explicit missing-glyph behavior.

Route B-lite is the small Graphics-core enhancement: `TrueTypeFont` can load
bytes but `BImageRenderer` cannot currently receive a caller-supplied font
source. Approve it only if deterministic CPU text is a product requirement.

**Route B-full - managed font catalog and shaping**

- map multiple families, weight and slant;
- implement fallback chains and missing-glyph selection;
- integrate shaping for surrogate pairs, combining sequences, bidi/RTL, and the
  approved complex scripts; and
- make drawing and measurement consume the same shaped glyph runs.

Route B-full is a materially larger Graphics/text initiative, not a small font
injection seam. Estimate and approve it separately after defining the required
script matrix.

For either route, measured advance and drawn output must keep caret, selection,
hit testing, and wrapping within one CSS pixel for the supported script set.
Test only the declared set: Latin for B-lite, and combining characters,
emoji/surrogate pairs, RTL, and representative complex-script/IME input only for
a route that actually implements them. Do not claim shaping support that the
selected route does not provide.

### 9.4 Images and codecs

Run browser runtime tests for PNG/APNG, JPEG, BMP, GIF if currently advertised,
lossless WebP, lossy WebP, malformed input, and oversized input.

Decoded-image limits are primarily owned by `Broiler.Media.Image.Managed`, not
the Canvas presenter. Current paths do not consistently propagate decoded-pixel
or decoded-byte limits into every decoder before allocation, and
`IBroilerRenderer.CreateImage(ReadOnlySpan<byte>)` has no decode-options
parameter. Checking the bitmap after decode is too late. Before T3 choose one:

- enforce `ImageDecodeOptions` limits inside every managed decoder and propagate
  them through Graphics;
- add a neutral bounded Graphics decode overload; or
- require browser applications to use a bounded Media decode and then call
  `CreateImage(BPixelBuffer)`.

Phase 0 records browser-appropriate encoded, dimension, decoded-pixel, and
decoded-byte budgets; these may be lower than desktop defaults.

Two current risks require explicit decisions:

- `MediaImageBridge` synchronously waits on asynchronous codec/catalog APIs,
  which can be fragile on a single-threaded browser runtime; and
- lossy WebP currently reaches a Windows WIC path and must be reported as
  unsupported or replaced for browser use.

If sync-over-async fails or causes unacceptable stalls, add a companion
asynchronous image-resource API or true synchronous in-memory codec entry point.
Do not break `IBroilerRenderer` solely for the browser. Enforce decoded-pixel and
dimension limits inside the owning decode path before allocating the output.

## 10. UI host, scheduling, and input architecture

### 10.1 Browser UI host

The sample-owned `BrowserUiHost` implements at least:

- `IUiHost`;
- `IUiDispatcher` through a separate dispatcher object;
- `IUiClock` through a monotonic browser-compatible clock; and
- optional cursor, system-settings, clipboard, text-input, and accessibility
  ports as their phases land.

The host tracks separate values for:

- canvas CSS size;
- Broiler logical viewport size;
- device-pixel ratio;
- canvas backing size;
- dirty/invalidation state;
- scheduled animation-frame state;
- active animation registrations; and
- disposed/page-hidden state.

### 10.2 Frame scheduling

`Invalidate` sets a dirty flag and schedules at most one animation frame. The
animation-frame callback:

1. clears the scheduled flag;
2. applies pending resize/DPR changes;
3. ticks due animations;
4. calls `UiSession.RenderFrame` once when dirty or animated;
5. records timing/diagnostics; and
6. schedules another frame only when work remains.

Idle applications must not maintain a continuous animation-frame loop. Page
visibility changes pause presentation and cancel/release transient input state.
An `ImmediateUiDispatcher` is not used for browser production because it can
make DOM callbacks reentrant; callbacks are queued onto the UI context.

### 10.3 Pointer input

Use browser Pointer Events as the common source for mouse, touch, and pen. The T2
slice maps mouse and the primary non-mouse contact into the existing mouse route.

Coordinate conversion is:

```text
DOM client coordinate
  -> subtract canvas bounding-client-rectangle origin
  -> scale from CSS rectangle to Broiler logical viewport
  -> InputPoint in client device-independent units
```

Observe and map where the current neutral UI route has a corresponding event:

- pointer move/down/up/cancel;
- button and buttons state;
- wheel X/Y and browser `deltaMode`;
- enter/leave;
- DOM pointer capture/lost capture;
- focus/blur; and
- page visibility/lifecycle cancellation.

Only prevent browser defaults when the application has intentionally handled the
operation. Pointer cancel, lost capture, blur, disposal, or navigation must
release `UiSession` capture and pressed visual states.

The current UI routed-event vocabulary has no explicit pointer-leave,
pointer-cancel, or capture-lost event. T2 therefore uses an explicitly tested
compatibility cleanup while logical capture is still active:

1. dispatch a pointer move to a point outside the viewport so hover clears;
2. dispatch a synthetic left-button release outside the viewport so a pressed or
   dragging control resets without activating; and
3. release any remaining `UiSession` capture.

Run that sequence for pointer cancel, lost DOM capture, blur, page hide, and
disposal, and test every selected press/drag control. Merely releasing session
capture is insufficient because controls can retain private pressed/drag state.
If a selected control cannot be reset safely by this compatibility sequence,
add a neutral cancel/capture-lost routed event before declaring T2 rather than
embedding more control-specific browser knowledge in the host.

T4 touch/pen support must preserve contact identity, pressure, tilt, hover,
buttons, cancellation, and scroll arbitration through the neutral UI route. If
the existing `UiInputEvent` loses those fields, add a neutral unified-pointer or
modality-preserving model before claiming T4.

### 10.4 Keyboard input

Translate `keydown`/`keyup` separately from text. Use DOM `key` for logical
meaning and `code` for physical identity where required. For the initial slice:

- set platform-native numeric key code to zero;
- normalize names expected by current controls, such as `ArrowLeft` to `Left`
  and the space character to `Space`;
- preserve repeat, location, and modifier state in the neutral Input record; and
- test which browser shortcuts remain browser-owned.

The current `UiInputEvent.FromKeyboardKey` retains the name, transition,
modifiers, native code, header, and source, but not repeat/location metadata. T2
can rely on delivery of repeated keydown events. Extend the neutral UI event only
if a checked-in shortcut/navigation scenario requires the discarded metadata.

Before T3 on macOS, add a neutral Meta/Command representation if the current
modifier model cannot express it. Centralize neutral key matching instead of
adding more Windows virtual-key comparisons to Standard controls.

### 10.5 Text input and IME

Use a visually unobtrusive but browser-editable `<textarea>` or `<input>` as the
native text-service endpoint. Position it from `IUiTextInputHost.PublishCaret`
and keep caret/selection synchronized using the information currently published.
For T2, richer state such as direction, multiline, read-only, password, and input
purpose may be supplied only by application knowledge of the selected Edit
control. A generic host cannot infer those values from `UiTextCaretInfo`; robust
T3 synchronization requires the neutral text-context extension described below.

Forward:

- one authoritative committed-edit stream: prefer supported/cancellable
  `beforeinput`, using `input` only as fallback or reconciliation;
- composition start/update/end as composition events; and
- selection/caret changes when the managed editor requires them.

Do not insert characters by converting `keydown` values. De-duplicate browsers
that emit both composition completion and a following input event. Never forward
both `beforeinput` and the resulting `input` as the same committed insertion.

Before robust T3 IME support:

1. Preserve composition selection start/length when converting neutral Input
   records into `UiInputEvent`.
2. Define old-focus/new-focus text-context lifecycle so the previous caret and
   textarea state are cleared immediately.
3. Extend the neutral text context only with information a platform text service
   genuinely needs: input purpose, multiline/read-only/password, direction,
   selection, virtual-keyboard hint, and privacy-gated surrounding text.

Test dead keys, CJK composition, combining marks, emoji, replacement selection,
focus transfer, mobile virtual keyboard if claimed, and password privacy.

Before a T4 mobile text claim, handle `beforeinput.inputType` operations that do
not arrive as useful key events: backward/forward deletion, replacement and
autocorrect, line/paragraph insertion, selection replacement, and native
undo/redo policy. Use controlled hidden-editor diffing or a neutral edit-operation
contract; do not reduce all operations to inserted strings. Focus the native
editable synchronously during the trusted pointer/key activation so mobile
browsers may open the virtual keyboard, then update its geometry from the next
published frame.

## 11. Browser host capabilities

| Capability | Current contract | T2 behavior | T3/T4 decision |
|---|---|---|---|
| Clipboard | Synchronous `IUiClipboardHost` | Handle trusted DOM copy/cut/paste events and their synchronous `clipboardData` | Add optional neutral async capability only for programmatic/delayed operations |
| Cursor | `IUiCursorHost` | Application policy may map semantic cursor to CSS cursor | Standard controls must request cursor changes before the port alone has an effect |
| Caret/text service | `IUiTextInputHost` | Position hidden editable element | Add neutral focus/text-context lifecycle for robust IME if needed |
| System settings | `IUiSystemSettingsHost` | Application policy maps reliable settings into theme/animation choices | Controls/services must consume the settings before implementing the port alone has an effect |
| Drag/drop | Outbound-only `IUiDragDropHost` | Defer, or handle a selected trusted workflow in the app | Inbound routed drag/drop and pathless data require a separate gated contract |
| Accessibility | `IUiAccessibilityHost` | Frame host explicitly captures/mirrors a passive semantic snapshot; no T2 accessibility claim | T3 needs actionable semantics for its selected workflow; T4 needs generalized stable IDs/actions/virtual children |
| File open/save | Path-based dialog abstractions | Excluded; app uses browser picker directly | Optional neutral opaque resource/stream picker; never return a fabricated path |
| Font dialog | Desktop font assumptions | Excluded or limited to packaged font catalog | Add only if the product exposes a controlled browser font catalog |

### 11.1 Clipboard

The T2 bridge handles native `copy`, `cut`, and `paste` events while they are
trusted user actions. It may expose a short-lived synchronous value to the
existing control command. This avoids changing UI merely for a proof.

The bridge owns shortcut sequencing. It must not let `StandardEdit` read stale
clipboard state during Ctrl/Command+V and then also insert the real later paste
payload. The T2 algorithm is:

- leave browser-owned clipboard shortcut keydowns out of ordinary managed
  shortcut dispatch so the browser produces its trusted clipboard event;
- during `copy`/`cut`, establish an event-scoped host whose `SetText` writes
  `clipboardData.setData`, invoke the selected control command, and prevent the
  default only when handled; and
- during `paste`, install the current event payload before invoking Paste (or
  explicitly insert it once), then prevent the default only when handled.

Context-menu and application command paths use the same event/current-payload
rules where the browser permits them. Tests must prove one copy/cut/paste action
produces one managed edit.

If T3 requires toolbar/menu-initiated asynchronous clipboard reads, define an
optional `ValueTask`-based neutral capability alongside the synchronous one.
Guard delayed completion against changed focus, changed selection, disposal,
navigation, and password fields. Permission denial is normal degraded behavior,
not an exception that tears down the session. An async host interface alone is
not sufficient: add async control/command entry points, or let the application
read first and post one guarded edit command through `IUiDispatcher`.

### 11.2 File open/save

`StandardFileDialog` enumerates directories, known folders, and logical drives;
`UiFileDialog` constructs full filesystem paths. Neither models browser
`File`/`Blob`/opaque handles or save completion.

For the first real app, use an application-level service with:

- an `<input type="file">` fallback for broad open-file support;
- download/Blob fallback for save;
- optional File System Access API progressive enhancement;
- streaming reads/writes for large content; and
- explicit cancellation and permission outcomes.

If more than one platform needs pathless resource selection, introduce a neutral
async resource-picker contract returning name, media type, length, stream, and
optional opaque durable handle. Keep the existing desktop path dialog for real
filesystems. Do not make a WASM virtual filesystem path look like a durable host
path.

### 11.3 Accessibility

The frame host must explicitly capture `StandardSemanticSnapshot` after
layout/render and publish or mirror it; `UiSession` does not currently call the
accessibility port automatically. T2 may expose this passive tree for inspection
but makes no accessibility support claim.

T3 requires an actionable bridge for the selected application workflow. The
host may maintain application-scoped identity/action mappings where that is
safe. A generalized T4 bridge requires neutral support for:

- stable semantic IDs across frames;
- roles, names, descriptions, values, range metadata, states, and relationships;
- supported action set and action dispatch back to the owning `UiElement`;
- focusability and deterministic tab order;
- virtualized child identity;
- coalesced updates after layout;
- accurate bounds after resize/zoom; and
- password/private text redaction.

Validate with browser accessibility inspection, keyboard-only operation, and
representative assistive technology. A visually hidden duplicate DOM must not
create two independent focus models or activate an action twice.

### 11.4 Drag and drop

The existing `IUiDragDropHost` starts an outbound drag only. It does not model
browser drag-enter, drag-over, drag-leave, drop, files, or pathless resource
payloads. If the selected application needs inbound dropping, add it as a
separate gated feature with routed target selection, allowed effects,
cancellation, streaming/opaque resource data, permission handling, and teardown.
Do not describe an asynchronous outbound port enhancement as inbound drag/drop
support.

## 12. Delivery roadmap

Effort estimates are engineering time, not elapsed calendar guarantees. They
assume one engineer familiar with the repository and exclude unrelated control
work.

Indicative cumulative ranges are 2-4 engineer-weeks for T1, 4-7 for T2, and
approximately 12-26 for a T3 UI application, depending mainly on direct Canvas
replay, text, and accessibility findings. Phase 6B full browser-engine work is
not included and cannot be estimated responsibly before its audit.

### Phase 0 - Freeze scope, ownership, and baselines

**Status:** Complete on 2026-07-11; see the
[Phase 0 implementation record](browser-webassembly-phase0/implementation-record.md).
**Estimate:** 3-5 days  
**Objective:** Prevent browser convenience code from leaking into neutral cores.

Deliverables:

- approve the executive/component decision in section 1;
- choose T2 as the first target and list any T3 feature required by the first
  application;
- classify every Standard control as supported, degraded, browser-replaced, or
  excluded;
- select the first real application workflow;
- record the initial browser matrix and reference hardware;
- install/restore the pinned .NET 10 `wasm-tools` workload in developer and CI
  environments;
- capture current desktop CPU render artifacts and UI input traces;
- record browser publish size, startup, and empty-app baseline; and
- approve the names and topology in section 7.

Allowed core changes: none.

Exit criteria:

- no unresolved owner exists for canvas, scheduling, resize, input, text,
  clipboard, accessibility, file selection, or deployment;
- no new project collides with the existing `Broiler.Browser.Windows` assembly;
- unsupported controls do not enter the first dependency graph; and
- T0 build commands are reproducible in CI for the foundations and for the exact
  selected T2 control/application dependency closure.

### Phase 1 - Executable browser runtime proof

**Status:** Implementation complete on 2026-07-11; see the
[Phase 1 implementation record](browser-webassembly-phase1/implementation-record.md).
**Estimate:** 3-5 days  
**Objective:** Advance from browser-buildable libraries to real browser execution.

Deliverables:

- scaffold `Broiler.UI.WebAssembly.Demo` as a standalone .NET 10 WebAssembly
  Browser App;
- reference `Broiler.Graphics`, create a deterministic `BRenderList`, and replay
  it through `BImageRenderer`;
- export a checksum and/or a small RGBA frame to JavaScript;
- run Release interpreted, trimmed, and AOT publishes;
- verify that Windows/Linux implementation assemblies and native assets are not
  present;
- capture browser console errors, startup time, compressed payload, and retained
  heap; and
- execute in Chromium and Firefox.

Runtime tests:

- solid/rounded rectangles;
- clips and transforms;
- in-memory image creation;
- PNG and JPEG decode;
- text fallback characterization (not part of the cross-host pixel checksum
  unless both hosts use identical font bytes/provider);
- disposal; and
- malformed render-list rejection.

Allowed core changes: only a minimal platform-neutral compatibility fix backed
by a failing executable test. No public API change.

Exit criteria:

- the published app loads without unhandled exception;
- a deterministic font-independent CPU frame matches the desktop checksum;
- trimming/AOT warnings attributable to selected Broiler assemblies are zero or
  explicitly resolved; and
- the browser dependency closure is platform neutral.

### Phase 2 - CPU canvas presenter and responsive surface

**Status:** Local implementation complete on 2026-07-11; see the
[Phase 2 implementation record](browser-webassembly-phase2/implementation-record.md).
**Estimate:** 1-2 weeks  
**Objective:** Display full Broiler frames correctly in a resizable browser canvas.

Deliverables:

- implement the persistent renderer/surface flow in section 9.1 inside the
  sample host;
- add the JS module, canvas binding, one-call frame transfer, and cleanup;
- add `ResizeObserver` and DPR/zoom handling;
- suspend zero-sized containers and enforce logical/backing/frame-byte allocation
  budgets before surface resize;
- implement transparency, clear color, and backing-size behavior;
- add image handle create/release tests;
- add resize/resource-retention diagnostics; and
- benchmark render replay, interop transfer, `putImageData`, allocations, and
  retained memory separately.

Allowed core changes: none. If the public bitmap view is insufficient, prove why
before proposing a neutral API.

Exit criteria:

- every current render command is visible through the CPU path;
- DPR 1, 1.25, 1.5, and 2 and repeated resize are correct;
- no old bitmap remains retained after resize/GC stabilization;
- zero/excessive resize requests suspend or fail safely without memory
  exhaustion;
- the canvas is visually stable during browser zoom; and
- measurements determine whether CPU presentation is viable for the T2 target.

### Phase 3 - UI host, frame scheduler, and desktop input vertical slice

**Status:** Local implementation complete on 2026-07-11; see the
[Phase 3 implementation record](browser-webassembly-phase3/implementation-record.md).
**Estimate:** 2-3 weeks  
**Objective:** Reach T2 with the existing UI core and selected Standard controls.

Deliverables:

- implement `BrowserUiHost`, queued dispatcher, monotonic clock, and RAF
  invalidation coalescing;
- create a `UiSession` and a tree containing Window, Panel, Label, Button, Edit,
  Slider, ScrollView, ListView, Menu, Tooltip, and ImageView as appropriate;
- translate pointer move/down/up and wheel, observe cancel/capture/focus/blur,
  and apply the tested synthetic cleanup from section 10.3;
- translate key down/up and modifiers with canonical names, retaining
  repeat/location in the neutral Input record while documenting current UI-route
  loss;
- add a minimal hidden editable element that publishes the focused Edit caret and
  forwards committed text, without claiming composition/IME parity;
- implement canvas focus acquisition and keyboard ownership policy;
- release capture/pressed/hover state on pointer cancel, blur, hidden page, and
  dispose through outside-move/release cleanup;
- add application policy for cursor and reduced-motion behavior, rather than
  assuming that implementing the optional ports activates unused behavior; and
- add browser automation for button activation, slider drag, scroll, menu,
  focus, and keyboard navigation.

The sample may contain the first input adapter. Before a second consumer, extract
the proven DTO/event logic into `Broiler.Input.*.WebAssembly` projects.

Allowed core changes: none when the T2 synthetic cancellation and repeated-
keydown paths pass. A neutral cancel event, Meta modifier/key constant, or
repeat/location projection is permitted only when its cross-platform behavior
test fails without it.

Exit criteria:

- invalidations schedule one frame and idle schedules none;
- no DOM input callback recursively renders;
- pointer capture outside the canvas completes or cancels correctly;
- keyboard and pointer focus remain coherent;
- a focused StandardEdit accepts basic committed text;
- selected controls complete their primary workflows; and
- browser automation reports no unhandled console errors or listener leaks.

### Phase 4 - Text, IME, clipboard, and accessibility baseline

**Status:** Local implementation complete on 2026-07-11; see the
[Phase 4 implementation record](browser-webassembly-phase4/implementation-record.md).
**Estimate:** 3-5 weeks  
**Objective:** Make the preview usable as an application rather than a canvas demo.

Deliverables:

- harden the hidden editable text-service bridge for selection and composition;
- publish caret position and synchronize focus/selection/input purpose;
- deliver committed text separately from composition updates;
- implement trusted DOM copy/cut/paste event bridging;
- have the frame host explicitly capture/mirror semantic snapshots;
- implement an application-owned ordered focus list and DOM-semantic action map
  for the selected workflow, or add a neutral focusability/tab-order model when
  generalized traversal is in approved scope;
- test dead keys, representative CJK IME, combining text, emoji, RTL, password
  privacy, focus transfer, and clipboard denial; and
- test keyboard-only operation plus initial screen-reader/browser combinations.

Allowed core changes, each separately gated:

- preserve composition selection data in `UiInputEvent`;
- add neutral old/new focus and text-input-context lifecycle;
- add optional async clipboard capability if event-based clipboard cannot meet
  the approved workflow, together with async consumer/command behavior; and
- add stable/actionable semantic identity and neutral focusability/tab order if
  the selected workflow cannot be mapped safely by the application host.

Exit criteria:

- composition commit occurs exactly once;
- moving focus clears the previous native text context/caret;
- password contents never enter clipboard/semantic diagnostics accidentally;
- denied capabilities degrade predictably;
- the selected UI is operable by keyboard; and
- the selected workflow has actionable semantics and coherent keyboard/DOM
  focus, while generalized accessibility remains a T4 gate; and
- the accessibility support statement matches actual tested behavior.

### Phase 5 - Rendering, font, codec, and performance decision

**Status:** Local implementation complete on 2026-07-11; browser frame-rate/artifact
evidence and cross-browser CI pending. See the
[Phase 5 implementation record](browser-webassembly-phase5/implementation-record.md).
The Phase 2 CPU failure fired the direct-Canvas gate, so the
`Broiler.Graphics.WebAssembly` batched Canvas 2D backend was created.
**Estimate:** 1-2 weeks if CPU presentation passes; 4-7 weeks if direct Canvas
replay is required.  
**Objective:** Select and harden the production rendering path using evidence.

Deliverables:

- decide browser-native text versus packaged managed fonts;
- audit image codecs, pre-allocation decode-limit enforcement, and sync-over-
  async runtime behavior with `Broiler.Media.Image.Managed` as the codec owner;
- run CPU presentation against the performance gates in section 14;
- if gates pass, retain the sample presenter and document its limits;
- if gates fail or reuse is approved, create `Broiler.Graphics.WebAssembly` and
  implement batched direct Canvas 2D replay;
- add a backend command-coverage suite shared with the CPU oracle;
- decide non-axis-aligned transform semantics and test independent clip/transform
  stack interleavings;
- choose synchronous direct-Canvas image-resource realization and publish the
  renderer-options/surface capability matrix;
- compare browser-native artifacts against CPU artifacts with documented
  antialias/text tolerances; and
- verify image/JS resource disposal and device/DOM lifecycle.

Allowed core changes:

- a neutral injectable font/glyph catalog only if packaged managed fonts are
  selected;
- a scoped text-metrics improvement if process-global registration is proven
  unsafe; or
- a companion async image API if executable browser tests demonstrate a
  deadlock/stall problem; or
- neutral Media/Graphics decode-limit propagation so dimensions/decoded bytes
  are rejected before allocation.

Exit criteria:

- one production rendering/text route is approved;
- every render command has native replay or an explicit tested fallback;
- caret/selection/measurement remains within one CSS pixel for supported text;
- image format support is published honestly; and
- the chosen path meets frame, memory, and stability gates.

### Phase 6A - UI application port and optional browser capabilities

**Estimate:** 2-4 weeks for the UI application track, application dependent  
**Objective:** Prove the architecture with a non-demo Broiler workflow.

Deliverables:

- create `src/Broiler.App.WebAssembly`;
- audit and browser-build the application's exact transitive dependency graph;
- move/share application-neutral model and command code without linking desktop
  window/backend sources;
- compose the selected Standard controls, Graphics path, and Input adapters;
- implement application-level file open/save using browser resources if needed;
- add drag/drop, download, URL/network, or persistence only when required by the
  selected workflow;
- test navigation away, reload, suspend/background, permission denial, and
  disposal; and
- record functional differences from Windows/Linux versions.

Allowed core changes:

- a neutral opaque resource/stream picker only if file selection is part of the
  approved UI abstraction scope and at least two hosts need it;
- touch/pen routed-data improvements only if the application claims those input
  modes; and
- no browser-specific types or policies.

Exit criteria:

- the selected end-to-end workflow runs in each T3 browser;
- no Windows/Linux backend is transitively included;
- files/resources survive only according to documented browser semantics;
- teardown leaves no active observers/listeners/timers/resources; and
- unsupported features are visible in a checked-in capability matrix.

### Phase 6B - Optional full Broiler browser-engine workstream

**Estimate:** Separate initiative; estimate only after the dependency audit.  
**Objective:** Determine and, if approved, implement what is required to run the
Broiler web-browser engine inside a web browser.

This phase is not required for a Broiler.UI application and must not be hidden
inside the Phase 6A estimate.

Deliverables:

- browser-RID, trimming, AOT, and runtime audit of `Broiler.HTML.Graphics`,
  `Broiler.DOM`, `Broiler.JavaScript.All`, HtmlBridge Core/DOM/Rendering/
  Scripting, and every application dependency;
- select or build an interpreter-compatible JavaScript execution backend that
  does not require `DynamicMethod`, `System.Reflection.Emit`, runtime assembly
  generation/loading, or filesystem code caches;
- replace synchronous/blocking page and resource I/O with cancellation-aware
  browser-compatible async operations;
- define a same-origin/CORS policy and decide whether unsupported cross-origin
  navigation fails, uses an explicitly deployed server proxy, or remains out of
  scope;
- replace favorites, modules, caches, and local-file behavior with explicit
  browser storage/resource abstractions;
- audit nested browser security boundaries, CSP, origin/storage partitioning,
  cookies/credentials, redirects, downloads, and untrusted script host
  capabilities;
- create a separate engine conformance/performance matrix for HTML, CSS, DOM,
  JavaScript, network, image, and bridge behavior; and
- publish a revised effort estimate and, if approved, an engine-specific
  delivery plan before implementation expands beyond the first vertical slice.

Allowed core changes: none under the authority of this Graphics/UI roadmap.
Changes to JS/HTML/DOM/Bridge engines require their own component decisions,
tests, and roadmap/ADR updates.

Exit criteria for the audit:

- every dynamic-code, filesystem, sync-I/O, native, and browser-security blocker
  has an owner and disposition;
- the selected dependency closure publishes and runs without unsupported dynamic
  code only if the implementation portion is approved;
- a same-origin static page with the approved JavaScript subset renders through
  the browser-hosted Broiler engine; and
- no claim of arbitrary-web compatibility is made without CORS/network/security
  evidence.

### Phase 7 - CI, packaging, hardening, and support declaration

**Estimate:** 2-3 weeks  
**Objective:** Turn the application proof into a maintainable browser preview.

Deliverables:

- add `.github/workflows/browser-wasm-build.yml`;
- restore the pinned workload and publish Release in CI;
- run headless Chromium first, then Firefox and Playwright WebKit during
  hardening;
- perform release evidence on real Safari in addition to WebKit emulation;
- retain desktop Graphics/UI/Input regression suites;
- run ten-minute animation/resize/input soaks and navigation teardown loops;
- report compressed payload, startup, first frame, frame percentiles,
  allocation, and retained memory on every benchmark run;
- choose interpreted versus AOT default from measured startup/payload/runtime
  tradeoffs;
- package `Broiler.Graphics.WebAssembly` and Input implementations only if they
  were extracted;
- document browser, control, codec, input, accessibility, and permission
  matrices; and
- cross-link this roadmap from component/root READMEs when implementation starts.

Allowed core changes: no new public contract. Stabilization fixes only.

Exit criteria:

- all T3 definition-of-done items pass;
- CI proves actual browser execution, not just compilation;
- release artifacts contain no forbidden native backend;
- performance/payload regressions are tracked; and
- the support statement names browsers and degraded capabilities precisely.

## 13. Suggested pull-request sequence

1. Phase 0 decision record, support matrix template, and CI workload bootstrap.
2. WebAssembly executable smoke project with deterministic CPU checksum.
3. Canvas pixel presenter with resize and DPR tests.
4. Browser UI host, queued dispatcher, and RAF scheduler.
5. Pointer/mouse/wheel bridge and capture cancellation.
6. Keyboard bridge and key-name normalization.
7. Interactive Standard-control demo and browser automation.
8. Hidden text-service bridge and committed text.
9. Composition/IME data preservation and focus lifecycle, if proven necessary.
10. Clipboard event bridge; async neutral capability only in a separate PR if
    its gate fails.
11. Passive semantic DOM proof.
12. Stable actions/focus accessibility extension, if required for T3.
13. Font/text strategy proof and decision artifacts.
14. Codec runtime matrix and image hardening.
15. Graphics performance decision.
16. Conditional `Broiler.Graphics.WebAssembly` direct Canvas backend.
17. Conditional extraction of `Broiler.Input.*.WebAssembly` projects.
18. `Broiler.App.WebAssembly` vertical slice.
19. Conditional browser resource picker/drag-drop work.
20. Optional full browser-engine dependency/dynamic-code/I/O/CORS audit; engine
    implementation follows only under its separately approved plan.
21. Cross-browser CI, soak, packaging, and documentation.

Every PR leaves desktop builds usable. Provider changes land in the canonical
component repository before consumers and aggregate submodule-pointer updates.
`Broiler.Graphics` also exists in nested/aggregate checkouts, so do not update a
consumer against a provider revision that has not landed coherently.

## 14. Validation and quality gates

### 14.1 Browser matrix

| Stage | Required browsers |
|---|---|
| T1 | Current Chromium and Firefox desktop |
| T2 | Current Chromium, Firefox, and automated WebKit |
| T3 | Current Chromium/Edge, Firefox, automated WebKit, and real Safari release evidence |
| T4 mobile claim | Chrome Android and Safari iOS on named reference devices, plus any other explicitly supported browser |

“Current” must be pinned to concrete versions in each release evidence record.
Playwright WebKit is useful automation but is not a substitute for real Safari
evidence.

### 14.2 Build and publish modes

Test:

- Debug/interpreted development;
- Release with trimming;
- Release with AOT; and
- the selected shipping mode.

Do not default to AOT merely because Graphics declares AOT compatibility. AOT
can improve CPU-heavy execution but increases download size. Choose it from
measured first-load, repeat-load, frame-time, and payload results.

### 14.3 Functional test layers

1. **Neutral unit/contract tests:** existing Graphics, Input, and UI suites.
2. **Renderer conformance:** identical command lists replayed through CPU and
   browser backends.
3. **Browser runtime tests:** actual canvas pixels/checksums and JS console.
4. **Input replay:** recorded pointer, keyboard, text, composition, cancel, and
   focus traces.
5. **End-to-end UI tests:** control interaction and real application workflows.
6. **Accessibility tests:** semantic snapshots, DOM projection, keyboard, and
   named assistive technologies.
7. **Lifecycle tests:** resize, zoom, hidden page, navigation, reload, dispose,
   and lost pointer capture.
8. **Security/failure tests:** malformed DTOs/images, permission denial,
   capability absence, oversized input, and interrupted async operations.

### 14.4 Performance gates

Capture exact reference hardware/browser versions before applying thresholds.
Initial gates are:

- idle UI schedules zero continuing animation frames;
- CPU proof sustains at least 30 FPS at 1280x720 DPR 1 on reference mid-range
  hardware for the interactive demo;
- the selected production path targets p95 frame time at or below 16.7 ms for a
  representative simple screen and 33.3 ms for the approved complex screen;
- input-to-present p95 is at or below 50 ms for pointer and keyboard interaction;
- resize/zoom does not leave retained frame buffers after stabilization;
- a ten-minute animation/input soak has no monotonic tab-memory growth;
- no avoidable full-frame managed allocation occurs after warm-up;
- managed and JavaScript full-frame allocation/copy counts and bytes are
  measured separately, including marshalling, typed-array/ImageData reuse, and
  retained JS memory after resize;
- zero-sized/hidden containers suspend safely, and excessive size/DPR requests
  fail without discarding the last valid surface or exhausting memory;
- payload size, first interactive time, and first frame are reported for both
  cold and cached load; and
- a compressed payload regression above 10% requires an explicit review note.

If the CPU path misses its 30 FPS/memory gate, direct Canvas replay is required
for T3. If it passes, direct replay remains optional until reuse or higher targets
justify the complexity.

### 14.5 Rendering/text gates

- CPU browser output checksum equals desktop CPU output for deterministic,
  font-independent frames. Text is checksum-compared only when both hosts use
  identical font bytes and the same metrics/raster path.
- Direct Canvas comparisons use the approved transform-semantic subset/policy;
  within that policy, differences are limited to documented browser
  antialiasing, font, geometry, and color-compositing tolerances.
- Clip and transform stacks validate and unwind after failure.
- Image opacity, transparent surfaces, fractional coordinates, and DPR are
  covered.
- Drawn text, measured advance, caret, selection, and hit testing stay within one
  CSS pixel for the supported font/script matrix.
- Font loading completes before first stable layout or triggers one explicit,
  tested relayout.

### 14.6 Baseline commands

```powershell
dotnet build Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj -c Release
dotnet run --project Broiler.Graphics/Broiler.Graphics.Tests/Broiler.Graphics.Tests.csproj -c Release
dotnet test Broiler.UI/Broiler.UI.slnx -c Release
dotnet build Broiler.slnx -c Release

dotnet workload restore `
  Broiler.UI/samples/WebAssembly/Broiler.UI.WebAssembly.Demo/Broiler.UI.WebAssembly.Demo.csproj
dotnet build Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj `
  -c Release -r browser-wasm
dotnet build Broiler.UI/src/Foundation/Broiler.UI/Broiler.UI.csproj `
  -c Release -r browser-wasm
dotnet publish `
  Broiler.UI/samples/WebAssembly/Broiler.UI.WebAssembly.Demo/Broiler.UI.WebAssembly.Demo.csproj `
  -c Release
```

The final workflow adds the browser automation command chosen by the
implementation. A successful `dotnet publish` without executing the output is
not an acceptance result.

## 15. Security, privacy, and permission model

1. Treat every image, file, clipboard payload, drag item, URL, and JavaScript DTO
   as untrusted input.
2. Enforce image dimensions, decoded pixel count, input length, and allocation
   limits before large allocations.
3. Access clipboard and file pickers only from browser-permitted secure/user-
   activation contexts and handle denial/cancellation as normal outcomes.
4. Never log typed text, composition content, password content, clipboard data,
   file contents, or accessibility values by default.
5. Redact private control values from semantic trees and diagnostics.
6. Validate numeric input and bound event queues to prevent memory/CPU abuse.
7. Remove all event listeners, observers, callbacks, JS object handles, and
   captured pointers on disposal/navigation.
8. Define a Content Security Policy compatible with the chosen .NET bootstrap
   and JS modules; do not expand script policy casually.
9. Do not enable threads, workers, or cross-origin isolation in the first release.
   If a later CPU renderer moves to a worker, add a separate threat/deployment
   review for `SharedArrayBuffer`, COOP/COEP, and transferable buffers.
10. Document origin storage/persistence semantics and never imply that browser
    sandbox storage is an ordinary user filesystem.

## 16. Packaging, compatibility, and versioning

- The first proof is source in a sample, not a new public package.
- Extract `Broiler.Graphics.WebAssembly` only after a reuse/performance gate.
- Extract Input implementations only after the event model is proven by the
  sample and keep their dependencies per input kind.
- Do not create a `Broiler.UI.WebAssembly` runtime under `Broiler.UI/src` without
  superseding ADR 0019 and updating topology tests.
- A browser-driven neutral API change follows semantic versioning and must remain
  usable by desktop hosts.
- Publish browser packages with explicit .NET 10/browser support metadata and no
  Windows/Linux native assets.
- Keep application host JS assets versioned with their managed assembly/package
  and fail clearly on version mismatch.
- Record interpreter/AOT expectations, required workload, browser matrix, and
  capability degradation in package/application documentation.
- Land canonical Graphics/Input provider changes first, consumers second, and
  aggregate/nested checkout pointer updates last.

## 17. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Full RGBA transfer dominates frame time/memory | T2 works but T3 is unusable at high DPR | Persistent buffers, one interop call, measure early, direct Canvas decision gate |
| Browser fonts unavailable to managed fallback | Block glyphs, wrong layout, poor text | Decide Canvas text versus injectable packaged fonts before T3 |
| Canvas text metrics differ from rendering | Caret, selection, hit-test drift | One measurement/rendering route, cache metrics, one-pixel conformance gate |
| Async browser services meet synchronous control commands | Reentrancy, stale selection, permission failures | Trusted event path for T2; optional neutral async contracts with dispatcher-safe continuations for T3 |
| Composition commits twice or loses selection | Corrupted international text input | Preserve selection data, hidden native editor, event trace tests, commit de-duplication |
| Blur/cancel leaves pointer capture or pressed state | Stuck controls | Central cancellation on pointercancel, lost capture, blur, hidden page, dispose |
| Windows-shaped key matching leaks into browser | Broken arrows/shortcuts/Command key | Canonical key-name mapping and neutral Meta/key constants where proven |
| Path-based file dialog is forced into browser | Fake paths, lost data, misleading API | Exclude StandardFileDialog; use streams/opaque handles and progressive browser picker |
| Passive semantic DOM is called accessible | False support claim | Stable IDs/actions/focus and named assistive-technology gates before T3/T4 |
| Touch is mapped to mouse indefinitely | Poor scrolling/gesture/mobile behavior | Treat compatibility mapping as T2 only; preserve full pointer data before mobile claim |
| Sync-over-async image bridge stalls browser thread | Deadlock or long UI pause | Runtime codec tests; async companion API only if failure is reproduced |
| Lossy WebP silently reaches Windows WIC | Runtime failure | Publish codec matrix; exclude or replace browser-incompatible path |
| One JS call per command becomes expensive | Direct renderer slower than expected | Batch/serialize one complete frame |
| AOT increases payload more than it helps runtime | Slow cold load | Benchmark interpreted versus AOT and choose per evidence |
| Browser event listeners survive navigation | Leaks and duplicate input | Explicit lifetime object and teardown-loop tests |
| Platform code is added under UI runtime | Architecture regression | Keep first host in sample/app; preserve ADR/topology tests |
| New backend name collides with desktop browser app | Confusing packages/assemblies | Use `WebAssembly`; reserve existing `Broiler.Browser.Windows` name |
| Nested Graphics checkouts drift | Broken aggregate builds | Provider-first landing and coherent submodule pointer updates |

## 18. Required ADRs and decision records

Before Phase 1, record or approve:

1. **Browser host ownership and project topology** - sample/application host
   first, no UI runtime platform assembly, and conditional extraction rules.
2. **Browser rendering/text strategy** - CPU proof, performance gate, Canvas 2D
   fallback/extraction, and font/metrics ownership.
3. **Browser input boundary** - Pointer Events, key normalization, text versus
   key streams, capture/cancellation, and extraction into Input implementations.

Add a new public-contract ADR only if its gate fires:

4. **Asynchronous UI host capabilities** - clipboard/drag/resource operations,
   continuation affinity, cancellation, and stale-owner protection.
5. **Pathless resource selection** - opaque resource identity, streams, save
   completion, persistence, and coexistence with desktop path dialogs.
6. **Actionable semantic bridge** - stable IDs, actions, focus, virtual children,
   privacy, and DOM/native mapping.
7. **Unified pointer/contact routing** - mouse/touch/pen data preservation and
   control behavior if T4 mobile/pen support is approved.

If the proof works with current public APIs, do not add an ADR merely to create
a browser-flavored type name.

## 19. Definition of done

### 19.1 T2 interactive preview

T2 is complete when:

- the WebAssembly sample publishes and executes in Chromium, Firefox, and the
  automated WebKit lane;
- selected UI/Graphics/Input assemblies build for `browser-wasm` with no native
  desktop backend in the closure;
- the existing managed renderer presents every current render command to canvas;
- resize, zoom, fractional DPR, transparency, and disposal pass;
- RAF scheduling coalesces invalidation and performs no idle rendering;
- pointer, wheel, keyboard, basic committed text, focus, and capture work for the
  selected Standard controls;
- cancel/blur/visibility/dispose cannot leave capture or pressed state behind;
- deterministic, font-independent browser CPU output matches the desktop CPU
  oracle; and
- unsupported controls/capabilities are documented.

No Graphics-core or UI-core public change is required to declare T2 if all these
gates pass.

### 19.2 T3 application preview

T3 is complete when, in addition:

- one selected real Broiler application workflow runs from a dedicated
  `Broiler.App.WebAssembly` composition root;
- text input and the declared IME/script matrix are correct;
- clipboard behavior and denial are deterministic;
- keyboard-only use and actionable semantics/focus for the selected application
  workflow pass the declared accessibility baseline;
- the selected rendering/text route meets performance and memory gates;
- image/codec support is runtime-tested and documented;
- application-required file/resource workflows use honest browser semantics;
- Release trimming and the selected interpreted/AOT deployment pass;
- Chromium/Edge, Firefox, automated WebKit, and real Safari evidence exists; and
- navigation/teardown and ten-minute soak tests show no retained-lifetime leak.

This T3 definition covers the UI application track. A full
`Broiler.Browser.Windows`-equivalent engine claim additionally requires Phase 6B
and its separately approved engine definition of done.

### 19.3 T4 production support

T4 additionally requires:

- every advertised control and host capability classified and tested;
- mobile/touch/pen evidence if claimed, including complete `beforeinput`
  edit-operation and trusted virtual-keyboard activation behavior;
- generalized stable actionable accessibility semantics, focus/tab traversal,
  virtual-child support, and named assistive-technology evidence;
- security, privacy, permission, CSP, payload, caching, and failure-injection
  review;
- public packages only for components proven reusable; and
- a versioned browser support and compatibility policy.

## 20. Recommended decisions to approve

1. Approve T2 desktop-browser interaction as the first target.
2. Approve the no-core-change-first rule.
3. Approve `Broiler.UI.WebAssembly.Demo` as the proof location.
4. Approve `Broiler.App.WebAssembly` as the later real application host.
5. Approve the Phase 0-6A and 7 estimates only for the UI application track;
   require a separate Phase 6B audit/plan for the full Broiler browser engine.
6. Approve CPU raster-to-canvas as the correctness path and measurement baseline.
7. Approve conditional extraction to `Broiler.Graphics.WebAssembly`, not the
   already-used `Broiler.Browser.Windows` name.
8. Approve direct Canvas 2D before considering WebGL/WebGPU.
9. Approve sample-local input translation for the spike and extraction into
   per-kind Input WebAssembly assemblies before reuse/productization.
10. Approve browser-native Canvas text as the default T3 investigation route,
   with packaged managed fonts as a separately justified deterministic option.
11. Approve excluding Standard FileDialog/FontDialog and `Broiler.UI.All` from
    the initial browser sample.
12. Approve trusted DOM clipboard events for T2 and an async neutral UI contract
    only if a T3 workflow proves it necessary.
13. Approve an actionable DOM semantic bridge as a T3 gate, not a T2 proof
    blocker.
14. Approve interpreted-versus-AOT selection through benchmarks.
15. Approve no browser thread/worker requirement in the first release.
16. Approve provider-first, consumer-second, aggregate-pointer-last delivery.

## 21. External implementation references

- [.NET 10 JavaScript `JSImport`/`JSExport` interop](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/?view=aspnetcore-10.0)
- [.NET WebAssembly build tools and AOT](https://learn.microsoft.com/en-us/aspnet/core/blazor/webassembly-build-tools-and-aot?view=aspnetcore-10.0)
- [WHATWG Canvas 2D](https://html.spec.whatwg.org/multipage/canvas.html)
- [W3C Pointer Events](https://www.w3.org/TR/pointerevents3/)
- [W3C Clipboard API and events](https://www.w3.org/TR/clipboard-apis/)
- [File System Access specification](https://wicg.github.io/file-system-access/)

These references constrain the browser adapters. They do not become dependencies
of the neutral Graphics, Input, or UI APIs.
