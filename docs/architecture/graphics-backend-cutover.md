# Graphics backend cutover and fallback

Broiler now defaults its `BBitmap` rendering path to the Broiler-owned raster
pipeline (`broiler`). The external `BROILER_GRAPHICS_BACKEND=skia` escape hatch
used during the stabilization window has been retired; only the internal
per-thread override used by tests and controlled tooling can still force the
Skia compatibility path while the last package cleanup work remains pending.

## Backend selection

The active backend is selected in this order:

1. an internal per-thread override used by tests and controlled tooling;
2. the default Broiler raster mode.

Supported values for the internal override:

- `broiler` — default Broiler raster pipeline
- `skia` — internal SkiaSharp compatibility path

## What changes between modes

- `broiler` enables the `BCanvas`-backed raster pipeline on `BBitmap` surfaces.
- `skia` keeps the legacy Skia-only painting path on `BBitmap` surfaces.
- `BBitmap` now stores its primary pixels in a Broiler-owned buffer and routes
  the remaining `SKBitmap`/`SKCanvas` compatibility-surface lifecycle through
  the internal `IBitmapCompatSurface` seam.
- `BBitmap.DrawPictureToFit` now also routes compat picture playback/scaling
  through the internal `IBitmapCompatSurface` seam.
- Core image-layer defaults now resolve the remaining compat implementations
  through an internal provider seam so the eventual package split only needs to
  move one centralized Skia registration point instead of many hard-wired
  constructor defaults.
- `BBitmap.OpenGraphics` in Broiler raster mode now defers `SKCanvas`/`SKBitmap`
  creation and only materializes the compatibility surface if a draw actually
  falls back to the internal Skia path, and raster-only graphics disposal now
  skips compat-surface sync work when no fallback canvas was ever materialized.
- `FontAdapter` now also defers `SKFont` creation until text measurement or
  drawing actually needs layout/render font state.
- The remaining `FontAdapter` `SKFont` creation and fallback metric projection
  details now route through the internal `IFontCompatFactory` seam.
- `GraphicsPathAdapter` now also defers `SKPath` creation until a fallback path
  draw actually needs the compatibility path object.
- The remaining `GraphicsPathAdapter` `SKPath` creation, reset, and segment
  application details now route through the internal `IPathCompat` seam.
- Solid, texture, and linear-gradient brush adapters plus pen adapters now
  defer `SKPaint`/`SKShader` materialization until a draw call actually falls
  back to the internal Skia compatibility path.
- The remaining solid-brush, linear-gradient, and pen paint creation plus pen
  style update details now route through the internal `IPaintCompatFactory`
  seam instead of living directly inside `SkiaImageAdapter` and `PenAdapter`.
- The high-level `HtmlContainer` rendering API no longer exposes `SKCanvas`
  overloads; the remaining Skia usage is internal to the image/runtime layer.
- `BBitmap` encode/decode/save now use a backend-neutral codec path instead of
  SkiaSharp image codecs.
- Adapter-level raster image stream loading now also routes through the
  backend-neutral `BBitmap.Decode` path instead of `SKBitmap.Decode`.
- Alias-backed font-file registration now defers `SKTypeface.FromFile` until
  font creation resolves the loaded family, while text shaping plus text/gradient
  draw dispatch now route through the internal `ITextShaper` compatibility seam
  and the remaining non-text `GraphicsAdapter` clip, image, line, rectangle,
  path, rounded-clip, texture-paint, polygon-fill, and layer-save fallback
  details now route through the internal `ICanvasCompat` seam during the
  fallback window, and the remaining
  `SkiaImageAdapter` deferred font-file registration and typeface-resolution
  details now route through the internal `IFontTypefaceResolver` seam, while
  system-font enumeration now comes from the Broiler-owned `BroilerFontRegistry`
  instead of `SKFontManager`, and the remaining `BBitmap`
  compatibility-surface lifecycle now also routes through the internal
  `IBitmapCompatSurface` seam; external SVG image rasterization now uses the
  Broiler-owned `BSvgRasterizer`, and CSS hex color parsing now uses a
  Broiler-owned parser instead of SkiaSharp's color parser.
- In the default `broiler` backend, text draw and gradient-text draw now first
  attempt a Broiler-owned raster text path backed by
  `SixLabors.Fonts`/`SixLabors.ImageSharp.Drawing`, so registered/system fonts
  no longer require an `SKCanvas` materialization just to paint glyphs.
- The remaining Skia fallback text draw and gradient-text draw details now route
  through the internal `ITextCanvasCompat` seam inside `SkiaTextShaper`.
- Deterministic `Ahem` text measurement, including max-width char-fit probing,
  now also stays on the Broiler-owned metrics path.
- The remaining non-`Ahem` Skia-backed text measurement compatibility details
  now route through the internal `ITextMetricsCompat` seam inside
  `SkiaTextShaper`.
- Raster-compatible layer compositing now also keeps `multiply`, `screen`,
  `darken`, `lighten`, `overlay`, `difference`, and `plus-lighter` blend modes
  on the Broiler-owned canvas path instead of forcing those cases onto the
  internal Skia layer fallback.
- General text measurement and the explicit internal `skia` override still keep
  the remaining Skia-backed font metrics compatibility path during the final M5
  cleanup window.

## Diagnostics

CLI capture artifacts and WPT results already record:

- backend `id`
- backend `displayName`
- backend `label`

This metadata should be used during stabilization to confirm whether a result
came from the default Broiler raster path or the internal Skia compatibility
path used by tests/tooling.

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

If either gate fails, the internal `skia` override remains available to tests
and controlled tooling while the failing fixtures are triaged.
