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
- Text and font loading still route through the existing Skia-backed
  compatibility seams during the fallback window; external SVG image
  rasterization now uses the Broiler-owned `BSvgRasterizer`.

## Diagnostics

CLI capture artifacts and WPT results already record:

- backend `id`
- backend `displayName`
- backend `label`

This metadata should be used during stabilization to confirm whether a result
came from the default Broiler raster path or the explicit Skia fallback.

## Stabilization suite and rollback gates

The M5 stabilization suite now runs representative cases for:

- Acid fixtures (`acid2.html`)
- WPT subsets (`css-anchor-position/position-visibility-anchors-visible.html`)
- CLI screenshot capture
- SVG sample pages
- text-heavy regression pages

Rollback gating for the default backend currently uses:

- pixel parity within the default deterministic diff threshold
  (`PixelDiffThreshold = 0.001`, `ColorTolerance = 5`)
- aggregate Broiler runtime across the curated suite no worse than
  `max(4x Skia, Skia + 400 ms)`

If either gate fails, the explicit `skia` mode remains the rollback path while
the failing fixtures are triaged.
