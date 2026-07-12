# Phase 5: Renderer-Options and Surface Capability Matrix

**Date:** 2026-07-11
**Roadmap:** section 9.2, deliverable "publish the renderer-options/surface capability
matrix." The roadmap notes that browser RAF owns synchronization and that the CPU and
Canvas paths may ignore or reinterpret several options; support must be explicit rather
than implied.

Backend: `Broiler.Graphics.WebAssembly` (direct Canvas 2D). "CPU oracle" is
`BImageRenderer`.

## `BRenderOptions`

| Field | Direct Canvas 2D backend | CPU oracle |
|---|---|---|
| `Antialias` | **Reinterpreted.** Canvas antialiases fills/paths by default; the backend does not toggle it. Rect fills on integer device coordinates are effectively hard-edged. Not honored as a switch. | Ignored; the CPU rasterizer is hard-edged for rects, coverage-antialiased for glyph contours only. |
| `VSync` | **Ignored.** Presentation cadence is owned by the application's `requestAnimationFrame` scheduler, not this option. | Ignored (offscreen). |
| `SubpixelText` | **Ignored.** Route A text is Canvas `fillText`; subpixel positioning/antialiasing is the browser's, not controllable here. | Ignored (block/packaged glyphs). |

## `BSurfaceDescriptor`

Note: the direct-Canvas backend presents to a live canvas and does not take a
`BSurfaceDescriptor`; these apply to the **CPU fallback surface** and to how the
presenter is configured. Backing size is derived from `Ceiling(css * dpiScale)`.

| Field | Behavior |
|---|---|
| `Size` | Logical CSS size; the presenter sets canvas CSS width/height from it. |
| `DpiScale` | Baked into the planner's pixel-scale transform; canvas backing size is `Ceiling(css * dpiScale)`. Validated `(0, 4]` by the sample host budgets. |
| `PixelFormat` | The Canvas backing store is browser-defined RGBA; `Rgba8` is the natural match. `Bgra8` is not requested on this path. The CPU fallback surface uses `Rgba8`. |
| `EnableTransparency` | The presenter acquires the 2D context with `{ alpha: false }` and clears to an opaque color, i.e. **opaque**. A transparent canvas context is a separate, unproven configuration and is not claimed. |

## Frame options

| Aspect | Behavior |
|---|---|
| Clear color | Applied as an opaque full-backing fill before replay. |
| Clip stack | Rectangular, axis-aligned, intersected across the stack (matches the CPU `all-clips-must-contain` rule). Reconstructed from a single base `save()`; not naive per-pop `save`/`restore`. |
| Transform stack | Baked into axis-aligned device rectangles managed-side (see the transform-semantics decision); the Canvas transform stays identity. |
| Fallback | On a planner `RequiresCpuFallback` signal (defensive; no current command triggers it), the whole frame is presented via the CPU raster path + `putImageData`. |

Any capability marked ignored/reinterpreted/opaque here must not be described as
supported. Alternate configurations (transparent context, `Bgra8`, honored antialias
toggles) require their own tested evidence before they are claimed.
