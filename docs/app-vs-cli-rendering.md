# Why the browser app renders pages worse than the CLI

An investigation into the reported symptom: the same page captured with
`Broiler.Cli` and opened in the desktop browser app renders very differently —
in the app, the words inside a line drift apart (`7-Zip` becomes `7- Zip`,
`64-bit x64` becomes `64- bit x64`), line spacing looks airy, and the glyphs
look like a different typeface. Reported on Windows 11.

**The text symptoms are neither Windows-specific nor caused by DPI.** The whole
difference is one hop: the CLI *paints* the layout display list, and the app
*translates* it into a `Broiler.Graphics` render list first. That translation
throws away the font object layout measured with and re-derives a font from two
strings — one of which is a point count that is then read as pixels. Everything
downstream (DirectWrite on Windows, the CPU rasterizer on Linux, Android's
`Paint` on Android) then draws glyphs whose advances have nothing to do with
the advances layout assigned, so every word lands at a position computed for a
different font.

There *is* a Windows-specific defect on top, but it is a separate one: the app
ships with a manifest declaring it DPI-unaware, so the window is bitmap-stretched.
That explains the size and the softness, not the gaps. See
[A separate Windows defect](#a-separate-windows-defect-the-app-ships-dpi-unaware).

Reproduced headlessly on Linux — see [Reproduction](#reproduction) — so the text
half can be fixed and verified without a Windows machine.

| observed symptom | cause |
| --- | --- |
| words pushed apart, gap ∝ word length | Defect 1, then 2 and the shaper residual |
| `7-Zip` → `7- Zip` | Defect 1 (two `BoxWord`s at independent origins) |
| airy vertical spacing | Defect 1 (small glyphs in a correct line box), then Defect 3 |
| wrong-looking typeface | Defect 2 |
| whole render up-scaled, and soft | the DPI manifest — benign geometry, resampled pixels |
| square corners, flat gradients, missing SVG paths, solid dashed borders | the dropped-item table |
| identical line breaking in both | *not a defect* — shared layout, and the proof the split is in paint only |

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
whatever text stack the backend has. Four independent errors follow.

### Why the damage shows up *between* words

`FragmentTreeBuilder` emits **one fragment per `BoxWord`**, each with its own
`X` and its own `FontHandle` (`FragmentTreeBuilder.cs:431-444`), and
`PaintWalker` turns each into its own `DrawTextItem` at
`Origin = new PointF(inline.X, inline.Y)` (`PaintWalker.Text.cs:81-100`). So
every word is *positioned* by layout and only its glyphs are re-shaped. If the
drawing font is narrower than the measuring font, the shortfall accumulates
inside each word and appears as blank space before the next one — a gap
proportional to word length, with line breaking untouched. `7-Zip` is two break
segments (`7-` and `Zip`) at two independently assigned origins, which is why
it visibly splits into `7- Zip`.

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
Direct2D and the CPU renderer honour it as pixels — so a point count is drawn
as that many pixels.

Measured on `acid/acid1/acid1.html`, which sets `font: 10px/1 Verdana, sans-serif`:

| | value |
| --- | --- |
| layout's font (`DrawTextItem.FontHandle.Size`) | `7.5` pt = 10 CSS px |
| `DrawTextItem.FontSize` for the same run | `7.5` |
| what the app hands the renderer (`BFontStyle.SizeInPixels`) | `7.5` |
| what it should hand the renderer | `10` |

**The error is not a uniform 0.75×**, because the unit is stripped rather than
converted and the switch has no arm for several keywords. The style layer
normalises `%` and `em` font sizes to a `"…pt"` string
(`CssBoxProperties.cs:1612` and `:1640` → `CssLength.ConvertEmToPoints`, which
formats `$"{…}pt"`, `CssLength.cs:114`), so those land on the ordinary 0.75×;
the rest scatter:

| computed `font-size` | measured em | drawn em | ratio |
| --- | --- | --- | --- |
| `medium` / absent (UA default) | 16 px | 12 | 0.750 |
| `Npt`, `N%`, `Nem` (all normalised to `pt`) | N·4/3 px | N | 0.750 |
| `Npx` | N px | N | **1.000 — accidentally correct** |
| `1.5rem` (strips `em` → `"1.5r"` → parse fails → 12) | 24 px | 12 | 0.500 |
| `0.5in`, `cm`, `mm`, `pc`, `ch`, `ex`, `vw`, `vh` (→ 12) | 48 px | 12 | 0.250 |
| `smaller` / `larger` (no switch arm → 12) | parent ± 2 pt | 12 | arbitrary |
| anything under CSS `zoom` (`ActualFont` uses `ComputedFontSizePoints * EffectiveZoom`, `CssBoxProperties.cs:2436`; `ParseFontSize` reads the unzoomed string) | zoomed | unzoomed | wrong on a second axis |

That scatter is itself diagnostic: on one page some runs are correct and others
are three-quarters or half size, which is why the damage looks like erratic
per-word spacing rather than a uniformly smaller page.

On 7-zip.org specifically, `BODY { font-size: 80% }` becomes the computed
string `"9.6pt"` — measured em 12.8 px, drawn em 9.6 px, exactly 0.750×.

### The distinction already exists one layer down

`FontAdapter` — the `RFont` the display list carries — already models exactly
this difference and even says so
(`Broiler.HTML/Source/Broiler.HTML.Image/Adapters/FontAdapter.cs:25-29`):

```csharp
/// <summary>Layout font (pt-based) – used for metrics and text measurement.</summary>
public object Font => _font ??= _fontCompatFactory.CreateFont(Typeface, (float)size);

/// <summary>Render font (CSS px-based) – used for drawing glyphs at correct size.</summary>
public object RenderFont => _renderFont ??= _fontCompatFactory.CreateFont(Typeface, (float)(size * PtToCssPx));
```

The pixel-sized render font the app needs is right there on the object the app
throws away.

**Watch out for one misleading comment while fixing this:**
`ILayoutFont.Size` is documented as "The font size, in CSS pixels"
(`Broiler.Graphics/Broiler.Graphics/Text/ILayoutFont.cs`), which is wrong —
`FontAdapter.Size => size` is the pt value, and `CssBoxProperties.GetEmHeight()`
multiplies it by `PtToPx` to get pixels. The doc comment should be corrected in
the same change.

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
rectangle (`ToTextLayoutRect`, `Direct2DRenderer.cs:468-478`, with no
`SetTextAlignment`/`SetLineSpacing` call anywhere in the file), so the baseline
lands wherever DirectWrite's own line metrics for the *substituted* face at the
*undersized* em put it — displaced twice. This is the vertical half of the same
problem, and it is why the app's line spacing looks airy relative to its glyphs.

## Defect 4 — slant is dropped and weight is quantized differently

`DrawTextItem` has **no slant field at all** (`DisplayList.cs:76-100`), and the
three-argument `BFontStyle` leaves `Slant = BFontSlant.Normal`. Layout measures
with the italic face (`CssBoxProperties.cs:2425-2426`), so italic text is
measured slanted and drawn upright.

Weight diverges too, in the opposite direction. Layout collapses weight to a
single bold bit at `>= 600` (`CssBoxProperties.IsBoldWeight`, `:2893-2899`),
while the translator maps onto DirectWrite's continuous axis
(`ToFontWeight` → Black/Bold/SemiBold/Medium/Light,
`HtmlGraphicsRenderList.cs:375-395`). A `font-weight: 500` run is therefore
measured in Regular and drawn in Medium; a `600` run is measured in Bold and
drawn in SemiBold. `IsRtl` and `GlyphRotationDeg` are dropped as well, both of
which the raster path honours (`RGraphicsRasterBackend.cs:646-656`).

## The residual: two shapers that would still disagree

Even with size, family and slant all correct, the two sides would not agree
exactly. The measuring shaper sums raw `hmtx` advances —
`ttf.GetAdvanceWidth(ttf.GetGlyphIndex(cp)) * scale`, with **no kerning and no
ligatures** unless `ComplexTextShaper.RequiresShaping` is true
(`TrueTypeText.cs:424-441`) — and `RFont.FontFeatures`
(`font-feature-settings`) has no representation in `BFontStyle` at all.
DirectWrite draws with `DWRITE_MEASURING_MODE.NATURAL`
(`Direct2DRenderer.cs:310-320`), i.e. it shapes the run itself with
`kern`/`liga`/`clig`. Latin kerning is predominantly negative, so drawn runs
stay systematically a little short of the measured advance. This is the part
that only a positioned glyph run can remove.

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
must re-resolve and re-shape the text. Defects 1-4 are symptoms of that.

Note also that the switch has **no `default:` arm**, so an unhandled item type
is dropped in silence, and no test anywhere in the repository references
`HtmlGraphicsRenderListBuilder`, `CreateDisplayList` or `DrawTextItem` — the
only non-production callers are the render-stage benchmarks. This path has
never had a regression test.

### The boundary itself is sound; HTML text was routed across it wrongly

Broiler.UI ADR 0003 sanctions exactly this render-list boundary — controls
submit "render primitives, render lists, geometry, color, **text
measurement**, and resource handles" through `Broiler.Graphics`
(`Broiler.UI/docs/adr/0003-graphics-submission-boundary.md`). UI controls are
self-consistent under it because they measure through `BTextMeasurer`, whose
Windows provider is registered by the Direct2D renderer itself
(`Direct2DRenderer.cs:118`, `DirectWriteTextMetricsProvider.UseIfUnset()`) — so
a control measures and draws with the same DirectWrite stack.

The HTML page path never calls `BTextMeasurer` at all (no reference to it
anywhere in `Broiler.HTML`'s own sources, `Broiler.Layout`, or
`Broiler.Browser.Core`). It arrives at the boundary already measured, by a
different engine, and hands over a *description* of the font rather than the
font. The ADR does not cover that case; the defect is that HTML text was pushed
across a boundary whose measurement contract it does not participate in.

## What is *not* the cause

- **DPI scaling — of the word gaps.** Everything from `BrowserApp` down to the
  render list is in device-independent pixels; `Direct2DWindow.ClientSize`
  returns DIPs (`Direct2DWindow.cs:98`), the surface is created with
  `new BSurfaceDescriptor(RenderDipSize, DpiScale, …)` (`Direct2DWindow.cs:376-378`),
  and the scale is applied exactly once, by `ID2D1DeviceContext::SetDpi(96 * DpiScale)`
  (`Direct2DSurface.cs:237`, from `Direct2DRenderer.cs:199`). It scales geometry
  and the DirectWrite em size by the same factor, so it cannot open a gap
  *inside* a line. (There *is* a separate real DPI defect — see below.)
- **Layout, cascade or line breaking.** Identical in both — the same container,
  the same options, and the reported screenshots break lines in the same places.
- **Fonts installed on the machine.** Both hosts resolve families through the
  same process-wide `FontsHandler`; the app's *layout* picks the same faces the
  CLI does. Only the app's *drawing* re-resolves, and gets it wrong.
- **The 7-Zip font-size defects** documented in
  [seven-zip-font-sizing-investigation.md](seven-zip-font-sizing-investigation.md).
  Those were about the CLI's own sizes and are fixed; this is downstream of them.

## A separate Windows defect: the app ships DPI-*unaware*

This does not cause the word gaps, but it is real, it is in the main repo, and
it explains why the app screenshots are both larger *and* softer than the CLI's.

`src/Broiler.Browser.Windows/Program.cs:32` asks for per-monitor awareness and
discards the answer:

```csharp
_ = SetProcessDpiAwarenessContext(new IntPtr(-4)); // PER_MONITOR_AWARE_V2, best effort.
```

But `src/Broiler.Browser.Windows/app.manifest:56` — live, not inside the
surrounding comment, and wired in by
`Broiler.Browser.Windows.csproj:13` (`<ApplicationManifest>`) — declares the
opposite:

```xml
<dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">false</dpiAware>
```

Manifest-declared awareness is applied by the loader before `Main` runs, and
`SetProcessDpiAwarenessContext` then fails with `ERROR_ACCESS_DENIED` — which
is discarded here, so nothing reports it. **Inferred consequence** (needs a
Windows machine to confirm): the process runs DPI-unaware, `GetDpiForWindow`
returns 96, `DpiScale` is pinned at 1.0, and the DWM bitmap-stretches the whole
window to the system scale factor. That matches a ~1638×1050 screenshot of an
1100×800-DIP window, and it means the app's text is not merely mis-shaped but
also resampled.

Worth fixing separately from the text defects, and worth landing in its own
commit, so the two effects stay distinguishable in a screenshot.

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

Ranked smallest-first. Steps 1-2 remove the gross error; step 4 is what makes
the app match the CLI for good.

1. **Take the size from `FontHandle`, not from the CSS string** —
   `Broiler.HTML`, `HtmlGraphicsRenderList.cs:188`. `DrawTextItem.FontHandle`
   already carries the `RFont` layout measured with, whose `Size` is in points:

   ```csharp
   double sizePx = item.FontHandle is RFont f
       ? f.Size * (96.0 / 72.0)   // RFont.Size is POINTS
       : item.FontSize;           // SVG path: already CSS px, no handle
   ```

   **The conditional is not optional.** The same `DrawText` helper is also the
   sink for `DrawSvgTextItem` (`HtmlGraphicsRenderList.cs:91-103`), which builds
   a synthetic `DrawTextItem` with **no `FontHandle`** and a `FontSize` that is
   already in px user units (`SvgRenderer.Text.cs:110`). A blanket `× 4/3` would
   break every SVG text run.

2. **Carry the resolved family, not the CSS list.** Prefer the family the
   handle already resolved (`FontsHandler.GetCachedFont` computes it at
   `FontsHandler.cs:65-67`; exposing it needs a `Family` accessor on `RFont`, in
   `Broiler.Graphics`). Independently useful stopgap: make
   `DirectWriteText.ResolveFontFamily`
   (`Broiler.Graphics/Broiler.Graphics.Windows/DirectWriteTextMetricsProvider.cs:144-158`)
   split the comma list, strip quotes, and pick the first family present in the
   DWrite system font collection before falling back to the generic map. Assert
   on the render list, not on pixels — the Linux renderer ignores family, so a
   pixel test will not catch a regression here.

3. **Put the used size and the slant *in* the display list**, so no consumer
   re-parses CSS: add `FontSizePx` and a slant field to `DrawTextItem`
   (`Broiler.Layout/Broiler.Layout/IR/DisplayList.cs`, **main repo**) and
   populate them from `inline.FontHandle` at `PaintWalker.Text.cs:86-90`
   (**submodule**, two lines). That is `CLAUDE.md`'s "type in the main repo, call
   in the submodule" idiom and keeps the submodule patch tiny.

   **Regression trap — do not simply change `ParseFontSize`'s return unit.**
   Its other caller, `PaintWalker.Background.cs:321`, already multiplies by
   `96.0/72.0` itself; changing the helper without editing that line in the same
   commit double-converts every em-based background position and *will* move
   CLI and WPT output. Add a new field rather than repurposing `FontSize`, or
   fix both call sites atomically.

4. **Give `BRenderList` a positioned glyph run** — `Broiler.Graphics`
   (`BRenderCommand`, `IBroilerRenderer`, each backend). A command carrying the
   typeface identity, the size in px and per-glyph ids and offsets — produced by
   the shaper that already measured the line — is the only construction under
   which the app cannot drift, and it is how the CLI gets it right today.
   Direct2D has `DrawGlyphRun` (or `IDWriteTextLayout` + `SetCharacterSpacing`);
   `BImageRenderer` already fills TrueType contours. It also lets the app place
   the baseline explicitly, closing Defect 3. Until this exists, steps 1-3
   shrink the error but cannot eliminate it — see
   [the residual](#the-residual-two-shapers-that-would-still-disagree).

5. **Stop the silent drops** — add a `default:` arm to the item switch that
   traces or asserts on an unhandled `DisplayItem` (five types are dropped
   today), then close the gaps in the table above. Lowest priority for this
   bug; highest for preventing the next one.

**Regression coverage.** A test in `src/Broiler.Browser.Core.Tests` (main repo,
runs headless on Linux) that lays a fixture out through `HtmlContainer`, walks
`CreateDisplayList()`, and asserts for every `DrawTextItem` that the resulting
`BFontStyle.SizeInPixels == ((ILayoutFont)item.FontHandle).Size * 96.0/72.0`,
covering `medium`, `Npt`, `N%`, `Nem`, `Nrem`, `Npx`, `smaller` and a `zoom`ed
subtree. It would be the first test to touch this path at all.

**Delivery.** `HtmlGraphicsRenderList.cs` and `PaintWalker.Text.cs` are in the
`Broiler.HTML` submodule; `BFontStyle.cs`, `Direct2DRenderer.cs`,
`DirectWriteTextMetricsProvider.cs` and `BImageRenderer.cs` are in
`Broiler.Graphics`. Per `CLAUDE.md`, attempt the submodule push first and bump
the pointer only if it succeeds. Note a conflict to settle with a maintainer
before relying on the fallback: `CLAUDE.md` says a denied push falls back to a
patch under `patches/`, but `docs/ROADMAP.md` declares that ledger retired and
"not a delivery path" (lines 25, 1116, 1450) and the directory does not exist on
disk. A 403 today means recording a blocker, not recreating `patches/`.

The DPI manifest fix (`src/Broiler.Browser.Windows/app.manifest`) is main-repo
and independent of all of the above.

## Open questions that need a Windows machine

- Which face DirectWrite actually substitutes for `"Verdana, Arial, Helvetica"`;
  the ratio above is measured against the Linux fallback face, and the Windows
  drift will differ in magnitude (though not in kind). This decides how much of
  the residual belongs to Defect 2 rather than to the shaper difference.
- Whether the shipped exe really runs DPI-unaware: read
  `GetAwarenessFromDpiAwarenessContext`/`GetDpiForWindow` at startup, check
  whether launching via `dotnet Broiler.Browser.Windows.dll` (host manifest
  rather than the apphost's) changes it, and confirm the softness disappears
  when `dpiAware=false` is removed.
- Whether `D2D1_DRAW_TEXT_OPTIONS.NONE` with `DWRITE_MEASURING_MODE.NATURAL`
  and an unbounded layout rect places the baseline where Defect 3 predicts, and
  whether an explicit baseline offset is still needed after step 1.
- The residual per-word advance delta after steps 1-2, at the same face and em:
  DirectWrite's `kern`/`liga`/`clig` output against the shaper's raw `hmtx`
  sums. That number sizes the case for the glyph-run primitive.
- Whether Segoe UI Semibold/Light — selected by `ToFontWeight` for 500/600/≤300
  — have materially different advances from the single Bold/Regular face
  `IsBoldWeight` picks for measurement.
- Whether ClearType/subpixel positioning adds a further per-run rounding on top
  of the systematic error.
