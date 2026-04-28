# Graphics backend cutover and fallback

Broiler now defaults its `BBitmap` rendering path to the Broiler-owned raster
pipeline (`broiler`) while keeping an explicit Skia fallback mode (`skia`) for
the stabilization window.

## Backend selection

The active backend is selected in this order:

1. an internal per-thread override used by tests and controlled tooling;
2. the `BROILER_GRAPHICS_BACKEND` environment variable;
3. the default Broiler raster mode.

Supported values:

- `broiler` — default Broiler raster pipeline
- `skia` — explicit SkiaSharp fallback path

## What changes between modes

- `broiler` enables the `BCanvas`-backed raster pipeline on `BBitmap` surfaces.
- `skia` keeps the legacy Skia-only painting path on `BBitmap` surfaces.
- Text, font loading, and SVG compatibility still route through the existing
  Skia-backed compatibility seams during the fallback window.

## Diagnostics

CLI capture artifacts and WPT results already record:

- backend `id`
- backend `displayName`
- backend `label`

This metadata should be used during stabilization to confirm whether a result
came from the default Broiler raster path or the explicit Skia fallback.
