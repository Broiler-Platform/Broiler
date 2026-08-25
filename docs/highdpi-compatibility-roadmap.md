# HighDPI compatibility roadmap

- **Status:** Active
- **Scope:** Missing, incomplete, or incorrect high-DPI (fractional/per-monitor
  display scaling) behavior across the graphics surfaces, windowing layers, and
  UI/host shells
- **Last reconciled:** 2026-08-24
- **Evidence basis:** Source audit of the DPI/scale paths in `Broiler.Graphics`,
  `Broiler.UI`, and the per-platform application shells under `src/`

This document consolidates high-DPI gaps found across the whole rendering stack,
not only one component. The implementation owner may be `Broiler.Graphics`
(per-platform surfaces), a per-platform application shell under `src/`, or
`Broiler.UI`. It is a coordination roadmap, not a replacement for the owning
component documents; where this document and source disagree, the current source
revision takes priority.

Out of scope: raster quality/anti-aliasing tuning, glyph hinting, and any
performance-only work. In scope only when the observable result is wrong or
missing at a non-1.0 display scale.

## Architecture baseline (what already works)

The stack has a single, consistent DPI contract and most surfaces honor it. New
work should extend this contract, not invent a parallel one.

- **`DpiScale` on `BSurfaceDescriptor`** is the one scale channel. Every surface
  works in logical device-independent units (DIP / CSS px) and converts to
  physical pixels as `pixels = ceil(DIP * DpiScale)`, guarded by a shared
  `NormalizeDpiScale` against non-finite or non-positive values.
- **Windows Direct2D surfaces** apply scale through `ID2D1DeviceContext::SetDpi`
  and size their bitmaps from `DIP * scale`
  (`Broiler.Graphics/Broiler.Graphics.Windows/Direct2DSurface.cs`,
  `Direct2DOffscreenSurface.cs`).
- **Android** resolves `DisplayMetrics.Density`, re-resolves it on every surface
  change, feeds it to both input hit-testing and the renderer, and derives
  logical size as physical / density
  (`src/Broiler.App.Android/AndroidBroilerView.cs`,
  `Broiler.Graphics/Broiler.Graphics.Android/AndroidSurfaceGeometry.cs`).
- **WebAssembly** reads `window.devicePixelRatio` and re-reads it through the
  `ResizeObserver`, so browser zoom and monitor moves are picked up
  (`src/Broiler.Writer.WebAssembly/wwwroot/main.js`,
  `Broiler.Graphics/Broiler.Graphics.WebAssembly/CanvasFramePlanner.cs`).
- **The Windows Browser app** declares PerMonitorV2 in `app.manifest` and also
  sets it at runtime (`src/Broiler.Browser.Windows/`).
- **The UI framework** is scale-agnostic by design: it lays out in logical units
  and receives scale via `IUiHost.Scale` / `UiViewportBinding`, wired to the
  window's live `DpiScale`
  (`Broiler.UI/src/Foundation/Broiler.UI/Host/IUiHost.cs`).

## Closure rules

An item closes only when:

1. the process/window actually reports a non-1.0 scale on a scaled display (not a
   pinned 1.0);
2. content renders at the display's native resolution (crisp, not DWM/compositor
   bitmap-stretched) and at the correct physical size;
3. a scale *change* at runtime (cross-monitor drag, zoom, density change) is
   reflected without restart; and
4. the owning component/shell document is reconciled with the change.

## Open items

### HDPI-1 — Broiler Code for Windows runs DPI-unaware (High)

**Owner:** `src/Broiler.Code.Windows`

`Program.cs` declares neither an `app.manifest` nor a runtime
`SetProcessDpiAwarenessContext` call, yet `CodeWindow : Direct2DWindow` sizes in
DIPs and trusts `GetDpiForWindow`. With no awareness declared, Windows reports 96
DPI, `DpiScale` pins at 1.0, and the DWM bitmap-stretches the whole window on any
scaled display — the exact failure the Browser manifest already documents. This
is the only shipping app with no DPI story at all.

**Next actions:**

1. Add `SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2)` as the first
   statement of `Main`, matching `src/Broiler.Writer.Windows/Program.cs`; or
2. Preferably, give the project the same PerMonitorV2 `app.manifest` as
   `src/Broiler.Browser.Windows` and reference it via `<ApplicationManifest>`.
3. Verify on a 150%/200% monitor that `window.DpiScale` reports the real scale
   and text is rendered, not resampled.

### HDPI-2 — Linux X11 window never sources the display scale (High)

**Owner:** `Broiler.Graphics.Linux.OpenGL` + `src/Broiler.Browser.Linux`

`LinuxBrowserRunner.cs` builds the surface with `BSurfaceDescriptor.Default(...)`,
whose scale is hardcoded to `1.0` (`BRenderOptions.cs`).
`LinuxOpenGlX11WindowSurface` only echoes the descriptor's `DpiScale` and
preserves it across `ConfigureNotify`; it never reads `Xft.dpi`, RandR
per-monitor scale, or `GDK_SCALE` / `QT_SCALE_FACTOR`. There is also no
`--scale` / `--dpi` CLI option (`LinuxBrowserOptions.cs`). On a HiDPI Linux
display the window therefore renders everything at half physical size. The raster
pipeline downstream is already scale-correct (`LinuxOpenGlNativeReplay.cs` honors
the descriptor) — only the sourcing is missing.

**Next actions:**

1. At startup, resolve an initial scale from the X server (`Xft.dpi` resource,
   then RandR monitor geometry) with `GDK_SCALE` / `QT_SCALE_FACTOR` env
   overrides, and seed the descriptor with it.
2. Expose an explicit `--scale` override in `LinuxBrowserOptions` as an escape
   hatch and for headless reproduction.
3. (Stretch) React to RandR scale changes at runtime the way `ConfigureNotify`
   reacts to size changes.

### HDPI-3 — WM_DPICHANGED re-renders but never resizes the window (Medium)

**Owner:** `Broiler.Graphics.Windows`

`Direct2DWindow.cs` handles `WM_DPICHANGED` by calling `ResizeSurfaceAndNotify()`
but discards `lParam`, the OS-suggested window rectangle; `SetWindowPos` never
appears in the file. The demo window
(`Broiler.Graphics.Windows.Demo/Direct2DDemoWindow.cs`) has the same shape. Per
Microsoft's PerMonitorV2 contract the app must `SetWindowPos` to the suggested
rect. Without it, dragging the window across monitors of different DPI leaves the
frame at its old pixel size while content reflows into a smaller logical box —
self-consistent and crisp, but the wrong physical size.

**Next actions:**

1. In the `WM_DPICHANGED` handler, read the `RECT*` from `lParam` and
   `SetWindowPos` to it (position and size) before/with the surface resize.
2. Apply the same fix to the demo window so the reference implementation is
   correct.

## Minor / informational

- **HDPI-4 (Low) — manifest vs. runtime awareness inconsistency.** Writer, the
  two `Broiler.UI` Win32 samples, and the graphics demo rely on the runtime
  `SetProcessDpiAwarenessContext(-4)` call rather than a manifest. This is
  functionally correct because it is the first statement in `Main`, but a
  manifest is the more robust path (applied before any CLR/window init). Only
  Browser currently ships one. Consider standardizing on the manifest for all
  windowed Windows apps.
- **HDPI-5 (Low) — initial window sizing uses system, not target-monitor, DPI.**
  `Direct2DWindow` pre-sizes from `GetDeviceDpi(IntPtr.Zero)` (primary/system
  DPI) before the HWND exists, then self-corrects on the first
  `WM_DPICHANGED` / `WM_SIZE`. A window opened on a non-primary monitor of a
  different scale flashes at the wrong size for one frame.
- **HDPI-6 (Informational) — CLI headless capture is always 1x.**
  `src/Broiler.Cli` has no device-scale override; captures rasterize at 1x CSS
  px. Correct for conformance testing, but it cannot emit 2x "retina" captures.
  Add a `--scale` / device-pixel-ratio option only if such captures become a
  requirement.

## Priority order

1. **HDPI-1** — user-visible blur in a shipping app; one-line/one-file fix.
2. **HDPI-2** — affects every HiDPI Linux user of Browser (and any future Linux
   Writer/Code shell built on the same runner).
3. **HDPI-3** — cross-monitor resize correctness polish.
4. HDPI-4 / HDPI-5 / HDPI-6 — hardening and consistency, no user-visible defect
   at a fixed scale.
