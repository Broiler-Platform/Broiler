# Browser WebAssembly Phase 5 Implementation Record

**Status:** Local implementation complete; browser frame-rate/artifact evidence and
cross-browser CI pending
**Date:** 2026-07-11
**Roadmap:** [Browser WebAssembly Roadmap](../browser-webassembly-graphics-ui-roadmap.md), Phase 5

## 1. Outcome

Phase 2 measured the CPU raster-to-`ImageData` presenter at roughly 195–209 ms per
frame at 1280×720 DPR 1 and recorded that it fails the roadmap's 30 FPS gate. Per
section 9.2 that fires the gate requiring a batched **direct Canvas 2D** backend.
Phase 5 creates it:

```text
BRenderList
  -> CanvasFramePlanner (neutral; owns clip/transform stacks, bounding-box baking)
  -> one batched op stream in backing pixels
  -> one presentFrame interop call per frame
  -> broiler.graphics.webassembly.js (Canvas 2D replay)
  -> canvas
```

The backend is the new component **`Broiler.Graphics.WebAssembly`** in the
`Broiler.Graphics` submodule, plus `Broiler.Graphics.WebAssembly.Tests`. No
`Broiler.Graphics` core or public API was changed: the CPU renderer stays the
pixel-exact oracle, and the transform policy mirrors it rather than replacing it.

The four Phase 5 decisions and their records:

- **Rendering/text route:** browser-native Canvas text (Route A). See
  [font-and-text-route.md](font-and-text-route.md).
- **Transform semantics:** axis-aligned bounding-box emulation. See
  [transform-semantics.md](transform-semantics.md).
- **Image resource realization + codecs:** synchronous reusable-`ImageData` resource
  canvases; bounded `Broiler.Media` decode owns limits; honest format matrix. See
  [codec-and-image-matrix.md](codec-and-image-matrix.md).
- **Renderer-options/surface capability matrix:** every `BRenderOptions` and
  `BSurfaceDescriptor` field is classified. See
  [renderer-capability-matrix.md](renderer-capability-matrix.md).

## 2. The backend

`Broiler.Graphics.WebAssembly` separates a platform-neutral planner from a
browser-gated presenter:

| Type | Platform | Role |
|---|---|---|
| `CanvasFramePlanner` | neutral | `BRenderList` → batched op stream; owns clip/transform stacks; bounding-box baking; lazy clip emission |
| `CanvasTransformPolicy` | neutral | bounding-box `ToDeviceAabb` / `AverageScale`, mirroring `BImageRenderer` exactly |
| `CanvasReplayOp` / `CanvasFrame` | neutral | op-code contract and frame view |
| `BrowserCanvasRenderer` | `browser` | image resources, one interop present per frame, whole-frame CPU fallback |
| `CanvasInterop` | `browser` | narrow `JSImport` boundary |
| `broiler.graphics.webassembly.js` | browser | Canvas 2D replay module |

The planner resolves each drawing command to an **absolute device rectangle** plus
the **current intersected clip**, so the browser side never mirrors Broiler's
independent clip/transform stacks. The interleaving
`PushClip, PushTransform, PopClip, draw, PopTransform` — where a transform must
survive a clip pop — is handled entirely managed-side. The replay module keeps a
single base `save()` and reconstructs the rectangular clip from it, never using
naive per-pop `save`/`restore`.

The browser interop compiles for every target (the JS interop types resolve from
the shared framework and are gated with `[SupportedOSPlatform("browser")]`), so the
library sits in the default solution while its DOM/Canvas code only runs on the
`browser-wasm` runtime.

## 3. Conformance evidence (headless)

`Broiler.Graphics.WebAssembly.Tests` is a 23-test console suite that runs off the
browser:

- **Op-level planner tests** — device-rect baking under translation/scale/DPR, lazy
  clip emission (one `SetClip` shared, `ClearClip` on pop), nested-clip intersection,
  the interleaving above (transform survives clip pop), thickness/radius scaling,
  text baseline + string table, image source/dest/opacity, all-ten-kinds coverage
  without fallback, unbalanced-list rejection, and cross-frame buffer reuse.
- **Transform-policy tests** — identity/translation/axis-scale exactness and rotation
  collapsing to the transformed bounding box.
- **CPU-oracle pixel conformance** — a managed `Canvas2DReferenceRasterizer` replays
  the planned stream with the same pixel-coverage and clip rules as the CPU
  `BCanvas`; a scene of solid fills, rectangular clips, transforms, and a nearest
  image blit is byte-identical to `BImageRenderer` output at DPR 1 and DPR 2, plus a
  dedicated clip/transform interleaving scene.

This validates the planner's device geometry, clip intersection, op ordering, and
image mapping on the coordinate-exact subset. Rounded/stroke antialiasing,
translucent compositing, and browser-native text differ within documented
tolerances and are the **browser-runtime** gate (section 5), not a headless one.

## 4. Sample integration

`Broiler.UI.WebAssembly.Demo` gains a Phase 5 section that constructs a
`BrowserCanvasRenderer`, loads the replay module, and presents a deterministic scene
exercising every command kind through the direct-Canvas path. The canonical replay
module lives in the backend's `wwwroot`; the sample vendors it via a build target
and scopes static-asset fingerprinting to `main.js` so the module publishes at a
stable URL that `JSHost.ImportAsync` can load. Trimmed publish confirmed the module
lands as `broiler.graphics.webassembly.js` with no trimming warnings.

This is a rendering-path proof, not UI integration; wiring the direct-Canvas path
under a real `UiSession`/application is Phase 6A.

## 5. Remaining gates

The following require an actual browser and are pending, consistent with every
prior phase:

- frame-time, input-to-present, memory, and ten-minute soak gates (section 14.4) on
  reference hardware — the reason the backend exists;
- CPU-vs-Canvas artifact comparison within documented antialias/text tolerances;
- caret/selection/measurement within one CSS pixel for the Route A text set; and
- committed Chromium/Firefox (and automated WebKit) CI runs of the Phase 5 smoke
  test, plus AOT publish evidence.

Until those land, Phase 5 selects and hardens the production rendering path with
headless conformance and build/publish evidence, and states the browser-measured
gates as open.

## 6. Records

- [Machine-readable boundary](phase5-boundary.json)
- [Font and text route decision](font-and-text-route.md)
- [Transform-semantics decision](transform-semantics.md)
- [Codec audit and image-format matrix](codec-and-image-matrix.md)
- [Renderer-options / surface capability matrix](renderer-capability-matrix.md)
- Backend: `Broiler.Graphics/Broiler.Graphics.WebAssembly`
- Tests: `Broiler.Graphics/Broiler.Graphics.WebAssembly.Tests`
- [Cross-browser smoke test](../../../tests/browser-wasm-phase5/smoke.mjs)
