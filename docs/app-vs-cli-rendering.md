# Why the browser app renders pages worse than the CLI

An investigation into the reported symptom: the same page captured with
`Broiler.Cli` and opened in the desktop browser app renders very differently —
in the app, the words inside a line drift apart (`7-Zip` becomes `7- Zip`,
`64-bit x64` becomes `64- bit x64`), line spacing looks airy, and the glyphs
look like a different typeface. Reported on Windows 11.

**It is not a Windows problem and not a DPI problem.** The whole difference is
one hop: the CLI *paints* the layout display list, and the app *translates* it
into a `Broiler.Graphics` render list first. That translation throws away the
font object layout measured with and re-derives a font from two strings — one
of which is a point count that is then read as pixels. Everything downstream
(DirectWrite on Windows, the CPU rasterizer on Linux) then draws glyphs whose
advances have nothing to do with the advances layout assigned, so every word
lands at a position computed for a different font.

Reproduced headlessly on Linux — see [Reproduction](#reproduction) — so it can
be fixed and verified without a Windows machine.

## The two pipelines

Both hosts build the *same* `HtmlContainer`, with the same adapter, the same UA
style sheet and the same options, and both call `PerformLayout`. Layout,
cascade, line breaking and box geometry are therefore identical — which is why
the two screenshots break lines in exactly the same places. They diverge only
after the display list exists:

| | `Broiler.Cli` | browser app (`Broiler.Browser.*`) |
| --- | --- | --- |
| entry | `CaptureService` → `HtmlRender.RenderToImageCore` | `BrowserApp.BrowserViewport.BuildHtmlRenderList` |
| after layout | `container.PerformPaint(bitmap, clip)` | `container.CreateDisplayList()` |
| display-list consumer | `RGraphicsRasterBackend.Replay` | `HtmlGraphicsRenderListBuilder.Build` |
| text drawn by | the same `RGraphics`/`RFont` that measured it | the platform text stack, from a rebuilt `BFontStyle` |
| output | pixels in a `BBitmap` | a `BRenderList` replayed by Direct2D / OpenGL / Vulkan |

`src/Broiler.Cli/CaptureService.cs` → `Broiler.HTML/Source/Broiler.HTML.Image/HtmlRender.cs:299-301`
(`PerformLayout` then `PerformPaint` on one bitmap), versus
`src/Broiler.Browser.Core/BrowserApp.cs:1487-1494`
(`CreateDisplayList` then `HtmlGraphicsRenderListBuilder.Build`).

The browser app is the *only* consumer of `HtmlGraphicsRenderListBuilder`; no
other head in the repository takes this path.

### Why the CLI is right by construction

`RGraphicsRasterBackend.RenderDrawText`
(`Broiler.HTML/Source/Broiler.HTML.Orchestration/IR/RGraphicsRasterBackend.cs:617-624`):

```csharp
if (item.FontHandle is RFont font)
{
    var origin = item.Origin;
    …
    g.DrawString(item.Text, font, item.Color, origin, size, item.IsRtl);
}
```

`item.FontHandle` is the very `RFont` instance layout measured every word with —
`PaintWalker` copies it onto the item (`PaintWalker.Text.cs:90`,
`FontHandle = inline.FontHandle`). Measure and draw therefore run through one
font object, one shaper and one advance formula, so a drawn word is exactly as
wide as the box layout reserved for it. The CLI cannot drift.

### Why the app is wrong

`HtmlGraphicsRenderListBuilder.DrawText`
(`Broiler.HTML/Source/Broiler.HTML.Graphics/HtmlGraphicsRenderList.cs:188-208`):

```csharp
var font = new BFontStyle(
    string.IsNullOrWhiteSpace(item.FontFamily) ? "Segoe UI" : item.FontFamily,
    item.FontSize,
    ToFontWeight(item.FontWeight));
…
list.DrawText(new BTextRun(item.Text, font, ToColor(item.Color, opacity)),
              new BPoint(item.Origin.X, item.Origin.Y));
```

`item.FontHandle` is never read. The run is described to the renderer as
*(family string, size, weight)* and re-resolved, re-selected and re-shaped by
whatever text stack the backend has. Three independent errors follow.

## Defect 1 — `DrawTextItem.FontSize` is a point count, drawn as pixels

`PaintWalker.ParseFontSize`
(`Broiler.HTML/Source/Broiler.HTML.Orchestration/IR/PaintWalker.Text.cs:194-226`)
re-parses the *computed style string* and strips the unit:

```csharp
if (numeric.EndsWith("pt", …)) numeric = numeric[..^2];
else if (numeric.EndsWith("px", …)) numeric = numeric[..^2];
else if (numeric.EndsWith("em", …)) numeric = numeric[..^2];
return double.TryParse(numeric, …) ? result : fallback;
```

The number it returns is documented as points (`return 12; // default: matches
CssConstants.FontSize (12pt)`, and the named sizes map to pt), which is correct
for the pipeline: the style layer normalises font sizes to points
(`CssBoxProperties.GetEmHeight() => ActualFont.Size * CssMetrics.PtToPx`,
`CssMetrics.PtToPx = 96/72`).

`BFontStyle`'s second parameter is `double SizeInPixels`
(`Broiler.Graphics/Broiler.Graphics/Text/BFontStyle.cs:28-32`), and both
Direct2D and the CPU renderer honour it as pixels. **Every point-valued size is
therefore drawn at exactly 0.75× the size layout measured** — and because the
unit is stripped rather than converted, a `px` or `em` string produces a
number that is not in any consistent unit at all.

Measured on `acid/acid1/acid1.html`, which sets `font: 10px/1 Verdana, sans-serif`:

| | value |
| --- | --- |
| layout's font (`DrawTextItem.FontHandle.Size`) | `7.5` pt = 10 CSS px |
| `DrawTextItem.FontSize` for the same run | `7.5` |
| what the app hands the renderer (`BFontStyle.SizeInPixels`) | `7.5` |
| what it should hand the renderer | `10` |

Same page, other runs whose computed string is still `10px`, report
`FontSize = 10` while the real font is `7.5pt` — the same field carries points
for some runs and pixels for others.

The sibling helper in `PaintWalker.Background.cs:319-322` states the points
contract explicitly and *does* convert, so the correct idiom already exists a
file away:

```csharp
// ParseFontSize returns values in CSS points (matching CssConstants.FontSize = 12pt).
// Convert pt -> px so that em-based positions match browser rendering (12pt = 16px).
fontSize = (float)(ParseFontSize(style.FontSize) * (96.0 / 72.0));
```

## Defect 2 — the CSS `font-family` list is passed as a single family name

`DrawTextItem.FontFamily` is the declared value, unchanged — a list such as
`"Verdana, sans-serif"` (acid1) or `"Verdana, Arial, Helvetica"` (7-zip.org).
The measuring side splits and resolves it:
`FontsHandler.EnumerateFamilyCandidates`
(`Broiler.Graphics/Broiler.Graphics/Text/FontsHandler.cs:154`) splits on `,`,
strips quotes, and `TryResolveAvailableFamily` (line 132) returns the first
candidate actually installed.

The app's translator passes the whole string through as a family name. On
Windows it reaches
`DirectWriteText.ResolveFontFamily`
(`Broiler.Graphics/Broiler.Graphics.Windows/DirectWriteTextMetricsProvider.cs:144-158`),
which maps the three generic keywords and otherwise returns the string
unchanged:

```csharp
return trimmed.ToLowerInvariant() switch
{
    "sans-serif" => "Segoe UI",
    "serif" => "Times New Roman",
    "monospace" or "monospaced" => "Consolas",
    _ => trimmed,
};
```

`IDWriteFactory::CreateTextFormat("Verdana, Arial, Helvetica", …)` matches no
installed family; DirectWrite substitutes its own default. So on Windows the
app draws essentially every page in one substituted face, at 0.75× size, at
positions computed for Verdana at full size.

On Linux the family never mattered in the first place: the OpenGL and Vulkan
renderers hand the whole render list to `BImageRenderer`
(`LinuxOpenGlRenderer.cs:77`, `LinuxVulkanRenderer.cs:8`), whose `DrawText`
rasterizes with `FallbackSystemFont.Shared` — *one* host sans-serif face for
every run, family ignored (`BImageRenderer.cs:238-253`). Android is a third
text stack again: `AndroidCanvasRenderer.DrawText` sets
`_paint.TextSize = run.Font.SizeInPixels`, resolves its own typeface, and puts
the baseline at `Origin.Y + SizeInPixels * 0.8`
(`src/Broiler.App.Android/AndroidCanvasRenderer.cs:187-198`).

So the same render list is drawn by three different text engines with three
different family-resolution rules and three different baseline conventions —
and none of them is the engine that measured the line.

## Defect 3 — the origin convention differs

`DrawTextItem.Origin` is the inline box's top-left. The raster backend fixes
the convention: it treats `Origin.Y` as the top of the em box and puts the
baseline one ascender below it, using the *layout* font's ascender and scale.
Direct2D instead receives the origin as the top-left of a DirectWrite layout
rectangle, so the baseline lands wherever DirectWrite's own line metrics for
the substituted face put it. This is the vertical half of the same problem, and
it is why the app's line spacing looks airy relative to its glyphs.

## Everything else the translation drops

The word-spacing is the loudest symptom, but the render-list translation is
lossy in other ways the CLI is not. Comparing the two switches
(`RGraphicsRasterBackend.Replay` lines 70-153 against
`HtmlGraphicsRenderListBuilder.Build` lines 51-136):

| display item / feature | CLI raster backend | app render-list builder |
| --- | --- | --- |
| `DrawSvgEllipseItem` | rendered | **silently dropped** |
| `DrawSvgPolygonItem` | rendered | **silently dropped** |
| `DrawSvgPolylineItem` | rendered | **silently dropped** |
| `FilterItem` / `RestoreFilterItem` | real filter layer | **silently dropped** |
| `BlendModeItem` / `RestoreBlendModeItem` | real blend layer | **no-op** |
| `OpacityItem` group | `SaveOpacityLayer` (group composite) | alpha folded into each primitive's colour |
| `ClipItem` with rounded corners / polygon | `PushClipRounded` / `PushClipPolygon` | axis-aligned rectangle clip only |
| `DrawTiledGradientItem` | real gradient | **flat fill of the first stop** |
| `DrawLineItem`, diagonal | rendered | **dropped** (only horizontal/vertical are drawn) |
| `DrawBorderItem` dashed / dotted / groove / ridge / inset / outset | per style | painted solid (`double` is the only special case) |
| border radius | honoured | ignored — sides are axis-aligned `FillRect`s |
| text: `IsRtl`, `GlyphRotationDeg`, `GradientStops` (`background-clip: text`) | honoured | dropped |
| text: italic / oblique | carried by `FontHandle` | not carried by `DrawTextItem` at all, and never set on `BFontStyle` |

`BRenderList`'s vocabulary is the deeper constraint: its whole command set is
`FillRect`, `StrokeRect`, `FillRoundedRect`, `StrokeRoundedRect`, `DrawText`,
`DrawImage`, clip and transform (`Broiler.Graphics/Broiler.Graphics/RenderList/BRenderCommand.cs`).
There is no gradient, no path, no layer and — decisively — no positioned glyph
run: `BTextRun` is a *string* plus a `BFontStyle`, so any backend consuming it
must re-resolve and re-shape the text. Defects 1-3 are symptoms of that.

## What is *not* the cause

- **DPI scaling.** Everything from `BrowserApp` down to the render list is in
  device-independent pixels; `Direct2DWindow.ClientSize` returns DIPs
  (`Direct2DWindow.cs:98`), the surface is created with
  `new BSurfaceDescriptor(RenderDipSize, DpiScale, …)` (`Direct2DWindow.cs:376-378`),
  and the scale is applied exactly once, by `ID2D1DeviceContext::SetDpi(96 * DpiScale)`
  (`Direct2DSurface.cs:237,280`). That is why the app screenshot is uniformly
  larger than the CLI's 1024×768 capture; it cannot open a gap *inside* a line.
- **Layout, cascade or line breaking.** Identical in both — the same container,
  the same options, and the reported screenshots break lines in the same places.
- **Fonts installed on the machine.** Both hosts resolve families through the
  same process-wide `FontsHandler`; the app's *layout* picks the same faces the
  CLI does. Only the app's *drawing* re-resolves, and gets it wrong.
- **The 7-Zip font-size defects** documented in
  [seven-zip-font-sizing-investigation.md](seven-zip-font-sizing-investigation.md).
  Those were about the CLI's own sizes and are fixed; this is downstream of them.

## Reproduction

The Linux OpenGL and Vulkan heads replay the render list through
`BImageRenderer`, so a `BImageRenderer` replay *is* the Linux app path, and it
shows the same defect as the Windows screenshots. A ~120-line console program
is enough to see both pipelines side by side:

1. Reference `Broiler.HTML.Image`, `Broiler.HTML.Graphics`, `Broiler.Graphics`
   and `Broiler.Media.Image.Managed`, and register the codecs at startup
   (`BImageCodecs.Use(new MediaCodecCatalog(ManagedImageCodecs.CreateCodecs()))`).
2. **CLI path:** `HtmlRender.RenderToImage(html, 1024, 768, BColor.White, baseUrl: …)`,
   then `Save`.
3. **App path:** build an `HtmlContainer` the way `BrowserViewport` does
   (`AvoidAsyncImagesLoading`/`AvoidImagesLateLoading` true, `Location` origin,
   `MaxSize` the viewport), `PerformLayout`, then
   `HtmlGraphicsRenderListBuilder.Build(new BImageRenderer(), container.CreateDisplayList(), clip)`
   and `renderer.RenderToImage(list, new BSurfaceDescriptor(new BSize(1024, 768), 1.0), frame)`.
4. Walk `displayList.Items` and, for each `DrawTextItem`, compare `item.Bounds.Width`
   (what layout reserved) with `BTextMeasurer.MeasureAdvance(item.Text, rebuiltBFontStyle)`
   (what the app will draw). Print `item.FontSize`, `item.FontFamily` and
   `((ILayoutFont)item.FontHandle).Size` beside them.

Results on a bare Linux container:

| page | total advance layout reserved | total advance the app draws | ratio |
| --- | --- | --- | --- |
| `acid/acid1/acid1.html` | 2134 px | 1910 px | 0.895 |
| `https://www.7-zip.org/` (CSS inlined) | 10779 px | 9168 px | 0.851 |

The 7-Zip render reproduces the reported screenshot exactly, `7- Zip`,
`64- bit x64`, `2026- 06- 25` and all — `7-` and `Zip` are separate
`DrawTextItem`s (break opportunity after the hyphen), so the deficit between
the reserved and the drawn advance opens up between them.

Note that "fix the units, keep everything else" does **not** fix it — measured
on the same container, converting `FontSize` pt→px and taking the first family
name leaves the mean per-run error at 0.15 rather than improving it, because on
Linux the drawing side ignores the family and uses one host face regardless.
The unit bug is real and provable from the code, but on its own it only changes
which direction the drift goes.

## Remediation

Ranked smallest-first. The first two are worth doing regardless; the third is
what actually makes the app match the CLI.

1. **Carry the resolved font, not the strings** — `Broiler.HTML`,
   `HtmlGraphicsRenderList.cs`. `DrawTextItem.FontHandle` already carries the
   `RFont` layout measured with. At minimum, read its `Size`
   (points → `* CssMetrics.PtToPx` for `SizeInPixels`) instead of
   `item.FontSize`, and split `item.FontFamily` to its first installed family
   rather than passing the list. Also map slant once `DrawTextItem` carries it.
   This removes the systematic 0.75× error and the "every page in one
   substituted face" behaviour on Windows.
2. **Fix `PaintWalker.ParseFontSize`** — `Broiler.HTML`,
   `PaintWalker.Text.cs`. Stripping `px`/`em` and returning the bare number as
   points is wrong for any style string that is not already in `pt`; convert
   like `PaintWalker.Background.cs` does, or drop the field in favour of
   `FontHandle`.
3. **Give `BRenderList` a positioned glyph run** — `Broiler.Graphics`
   (`BRenderCommand`, `IBroilerRenderer`, each backend). A `DrawGlyphRun`
   command carrying the typeface identity, the size in px and per-glyph ids
   and offsets — produced by the shaper that already measured the line — is the
   only construction under which the app cannot drift, and it is how the CLI
   gets it right today. Direct2D has `DrawGlyphRun`; `BImageRenderer` already
   fills TrueType contours. Until this exists, steps 1-2 reduce the error but
   cannot eliminate it, because two shapers will never agree exactly.
4. **Close the dropped-item gaps** in the table above — SVG ellipse/polygon/
   polyline, filter and blend layers, rounded and polygon clips, real
   gradients, diagonal lines, border styles and radius. Each needs a matching
   `BRenderList` primitive first, which is the same project as step 3.

Both `HtmlGraphicsRenderList.cs` and `PaintWalker.Text.cs` are in the
`Broiler.HTML` submodule, so per `CLAUDE.md` a fix there ships as a patch under
`patches/` unless the submodule push is authorised.

## Open questions that need a Windows machine

- Which face DirectWrite actually substitutes for `"Verdana, Arial, Helvetica"`;
  the ratio above is measured against the Linux fallback face, and the Windows
  drift will differ in magnitude (though not in kind).
- Whether `D2D1_DRAW_TEXT_OPTIONS.NONE` with `DWRITE_MEASURING_MODE.NATURAL`
  and an unbounded layout rect places the baseline where Defect 3 predicts.
- Whether ClearType/subpixel positioning adds a further per-run rounding on top
  of the systematic error.
