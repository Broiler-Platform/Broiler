# Browser WebAssembly Phase 2 Implementation Record

**Status:** Local implementation complete; Chromium/Firefox CI evidence pending  
**Date:** 2026-07-11  
**Roadmap:** [Browser WebAssembly Roadmap](../browser-webassembly-graphics-ui-roadmap.md), Phase 2

## 1. Outcome

The sample now implements the roadmap's persistent Stage A CPU presenter:

```text
persistent BImageRenderer
  -> persistent BImageSurface
  -> cached BBitmap.ToPixelBuffer(copy: false)
  -> one byte-array interop call per accepted frame
  -> persistent CanvasRenderingContext2D + reusable ImageData
  -> putImageData
```

No neutral Graphics, Media, Input, or UI source/API was changed. The existing
public bitmap view is sufficient for a no-copy managed frame view.

## 2. Managed presenter

`BrowserCpuPresenter` owns and deterministically disposes:

- one `BImageRenderer`;
- one resizable `BImageSurface`;
- one cached no-copy `BPixelBuffer` view, recreated only after resize;
- one persistent in-memory checker image handle;
- logical size, DPR, surface generation, frame, resize, reject, and suspend
  counters; and
- weak references to replaced bitmaps for post-GC retention checks.

The responsive render list contains all ten current command kinds, including
text, images, rounded geometry, and independently interleaved clip/transform
stacks. The Phase 1 font-independent scene remains the exact checksum oracle.

## 3. Resize and allocation policy

The application validates values before `BImageSurface.Resize`:

| Budget | Limit |
|---|---:|
| Logical width or height | 4,096 CSS pixels |
| DPR | `(0, 4]` |
| Backing pixels | 16,777,216 |
| RGBA frame bytes | 67,108,864 (64 MiB) |

Backing size uses `Ceiling(logical * DPR)`. A zero width or height suspends
without resizing. An invalid or excessive request is rejected while retaining
the last valid surface. DPR 1, 1.25, 1.5, and 2 produced the exact expected
backing dimensions.

`ResizeObserver` and `window.resize` share one requestAnimationFrame-coalesced
observation stream. The canvas CSS size is logical pixels and its backing size
comes directly from the managed surface. Observer/listener/RAF state and the
managed presenter are cleaned up on `pagehide`.

## 4. Transparency and resource behavior

The main presenter is explicitly opaque:

- the surface is RGBA with `EnableTransparency: false`;
- the clear color is opaque;
- every presented managed pixel is checked for alpha 255; and
- Canvas 2D is acquired with `{ alpha: false }`.

A separate managed runtime check creates a transparent surface and proves that
a transparent clear preserves zero alpha. Phase 1 continues to exercise image
opacity 0.85. The persistent benchmark uses image opacity 1 because the current
CPU canvas implements opacity with a full-surface temporary bitmap; retaining
that command in every benchmark frame would add an avoidable frame-sized
allocation and obscure the persistent presenter measurement.

## 5. Performance decision

The presenter removes avoidable full-frame managed allocations after warm-up:
the local AOT path allocated about 3,088 managed bytes per steady frame, reused
JavaScript `ImageData`, and retained zero replaced bitmaps after repeated resize
and forced-GC stabilization.

However, CPU replay scales with backing pixels and fails the roadmap's 30 FPS
gate at 1280 x 720 DPR 1. Local full-AOT replay was approximately 195-209 ms per
frame, before the comparatively small 1.5-2.6 ms frame interop. The CPU path is
therefore approved only as the deterministic correctness oracle and low-size
fallback, not as the T3 production renderer.

This fires the roadmap's performance gate for a batched direct-Canvas backend.
Per the roadmap, `Broiler.Graphics.WebAssembly` belongs to the later Phase 5
rendering decision/workstream. Phase 2 does not create it prematurely and does
not require a Graphics-core rewrite.

## 6. Automation and remaining gate

The Phase 2 workflow publishes trimmed and full-AOT variants, rejects desktop
Broiler assets, and runs Chromium and Firefox through:

- DPR 1/1.25/1.5/2;
- zero-size suspension and last-valid-surface retention;
- oversize rejection;
- repeated resize plus a stabilization frame with old-bitmap retention checks;
- same-size warm frames with reusable ImageData;
- 1280 x 720 performance evidence; and
- deterministic pagehide/dispose cleanup.

The local approved browser surface passed the functional checks and all publish
modes. The status remains "CI evidence pending" until the committed workflow
records separate Chromium and Firefox runs.

## 7. Records

- [Phase 2 measurements](baseline-evidence.md)
- [Machine-readable decision](phase2-boundary.json)
- [Sample instructions](../../../Broiler.UI/samples/WebAssembly/Broiler.UI.WebAssembly.Demo/README.md)
- [Cross-browser smoke test](../../../tests/browser-wasm-phase2/smoke.mjs)
