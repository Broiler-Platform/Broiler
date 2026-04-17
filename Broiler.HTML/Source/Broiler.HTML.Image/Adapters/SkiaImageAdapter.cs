using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Broiler.HTML.Adapters;
using Broiler.HTML.Adapters.Adapters;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal sealed class SkiaImageAdapter : RAdapter
{
    /// <summary>
    /// Typefaces loaded from font files via <see cref="LoadFontFromFile"/>,
    /// keyed by CSS family name (case-insensitive).  SkiaSharp's system
    /// <see cref="SKFontManager"/> does not expose fonts loaded with
    /// <see cref="SKTypeface.FromFile"/>; this dictionary allows
    /// <see cref="CreateFontInt"/> to resolve them by name.
    /// </summary>
    private readonly Dictionary<string, SKTypeface> _loadedTypefaces
        = new(StringComparer.OrdinalIgnoreCase);

    private SkiaImageAdapter()
    {
        // Register system fonts first so we can probe availability below.
        var fontManager = SKFontManager.Default;
        var systemFonts = new HashSet<string>(fontManager.FontFamilies, StringComparer.OrdinalIgnoreCase);
        foreach (var familyName in systemFonts)
        {
            AddFontFamily(new FontFamilyAdapter(familyName));
        }

        // CSS 2.1 §15.3 generic font family mappings.
        // SkiaSharp does not resolve CSS generic family names; map them to the
        // first available system font from a prioritised fallback list.
        MapGenericFamily("sans-serif", systemFonts, "Arial", "Helvetica", "Liberation Sans", "DejaVu Sans");
        MapGenericFamily("serif", systemFonts, "Times New Roman", "Liberation Serif", "DejaVu Serif");
        MapGenericFamily("monospace", systemFonts, "Courier New", "Liberation Mono", "DejaVu Sans Mono");
        MapGenericFamily("cursive", systemFonts, "Comic Sans MS", "URW Chancery L");
        MapGenericFamily("fantasy", systemFonts, "Impact");

        // Common alias: web content often uses "Helvetica" expecting Arial-like metrics.
        if (!systemFonts.Contains("Helvetica"))
        {
            var arialLike = FirstAvailable(systemFonts, "Arial", "Liberation Sans", "DejaVu Sans");
            if (arialLike != null)
                AddFontFamilyMapping("Helvetica", arialLike);
        }
    }

    /// <summary>
    /// Maps a CSS generic font family name to the first available system font.
    /// </summary>
    private void MapGenericFamily(string genericName, HashSet<string> systemFonts, params string[] candidates)
    {
        var resolved = FirstAvailable(systemFonts, candidates);
        if (resolved != null)
            AddFontFamilyMapping(genericName, resolved);
    }

    private static string? FirstAvailable(HashSet<string> systemFonts, params string[] candidates)
    {
        return Array.Find(candidates, systemFonts.Contains);
    }

    public static SkiaImageAdapter Instance { get; } = new();

    /// <summary>
    /// Loads a TrueType/OpenType font from a file path and registers it as
    /// an available font family.  Optionally maps a CSS name to the loaded
    /// family (e.g. mapping "sans-serif" to a bundled reference font for
    /// deterministic test comparison).
    /// </summary>
    /// <param name="path">Absolute path to a .ttf or .otf font file.</param>
    /// <param name="mapFromName">
    /// When non-null, adds a font-family mapping from this name to the
    /// loaded font's family name (e.g. <c>"sans-serif"</c>).
    /// </param>
    /// <returns>The family name of the loaded font, or <c>null</c> on failure.</returns>
    public override string LoadFontFromFile(string path, string mapFromName = null)
    {
        var typeface = SKTypeface.FromFile(path);
        if (typeface == null)
            return null;

        var familyName = typeface.FamilyName;
        AddFontFamily(new FontFamilyAdapter(familyName));

        // Cache the typeface so CreateFontInt can use it directly.
        // SKFontManager.Default cannot find typefaces loaded from files,
        // so we maintain our own lookup dictionary.
        _loadedTypefaces[familyName] = typeface;

        if (!string.IsNullOrEmpty(mapFromName))
        {
            AddFontFamilyMapping(mapFromName!, familyName);
            // Also register under the alias name so that CSS font-family
            // lookups with the alias (e.g. "Ahem") resolve to this typeface
            // even before the mapping is applied.
            _loadedTypefaces[mapFromName!] = typeface;
        }

        // Do not dispose the typeface — SkiaSharp's font manager retains
        // a reference so that subsequent SKTypeface.FromFamilyName lookups
        // can resolve the loaded family.  Disposing would invalidate it.
        return familyName;
    }

    protected override Color GetColorInt(string colorName)
    {
        if (SKColor.TryParse(colorName, out var color))
            return Utilities.Utils.Convert(color);

        // Fallback: try common color names (CSS 2.1 basic + CSS Color Level 3 extended)
        return colorName.ToLowerInvariant() switch
        {
            // CSS 2.1 §4.3.6 basic color keywords
            "white" => Color.FromArgb(255, 255, 255, 255),
            "black" => Color.FromArgb(255, 0, 0, 0),
            "red" => Color.FromArgb(255, 255, 0, 0),
            "green" => Color.FromArgb(255, 0, 128, 0),
            "blue" => Color.FromArgb(255, 0, 0, 255),
            "yellow" => Color.FromArgb(255, 255, 255, 0),
            "orange" => Color.FromArgb(255, 255, 165, 0),
            "purple" => Color.FromArgb(255, 128, 0, 128),
            "gray" or "grey" => Color.FromArgb(255, 128, 128, 128),
            "silver" => Color.FromArgb(255, 192, 192, 192),
            "maroon" => Color.FromArgb(255, 128, 0, 0),
            "olive" => Color.FromArgb(255, 128, 128, 0),
            "lime" => Color.FromArgb(255, 0, 255, 0),
            "aqua" or "cyan" => Color.FromArgb(255, 0, 255, 255),
            "teal" => Color.FromArgb(255, 0, 128, 128),
            "navy" => Color.FromArgb(255, 0, 0, 128),
            "fuchsia" or "magenta" => Color.FromArgb(255, 255, 0, 255),
            "transparent" => Color.FromArgb(0, 255, 255, 255),

            // CSS Color Level 3 extended color keywords (used by WPT tests)
            "lightgray" or "lightgrey" => Color.FromArgb(255, 211, 211, 211),
            "darkgray" or "darkgrey" => Color.FromArgb(255, 169, 169, 169),
            "dimgray" or "dimgrey" => Color.FromArgb(255, 105, 105, 105),
            "lightslategray" or "lightslategrey" => Color.FromArgb(255, 119, 136, 153),
            "slategray" or "slategrey" => Color.FromArgb(255, 112, 128, 144),
            "darkslategray" or "darkslategrey" => Color.FromArgb(255, 47, 79, 79),
            "gainsboro" => Color.FromArgb(255, 220, 220, 220),
            "whitesmoke" => Color.FromArgb(255, 245, 245, 245),
            "aliceblue" => Color.FromArgb(255, 240, 248, 255),
            "ghostwhite" => Color.FromArgb(255, 248, 248, 255),
            "snow" => Color.FromArgb(255, 255, 250, 250),
            "seashell" => Color.FromArgb(255, 255, 245, 238),
            "floralwhite" => Color.FromArgb(255, 255, 250, 240),
            "linen" => Color.FromArgb(255, 250, 240, 230),
            "antiquewhite" => Color.FromArgb(255, 250, 235, 215),
            "oldlace" => Color.FromArgb(255, 253, 245, 230),
            "papayawhip" => Color.FromArgb(255, 255, 239, 213),
            "blanchedalmond" => Color.FromArgb(255, 255, 235, 205),
            "bisque" => Color.FromArgb(255, 255, 228, 196),
            "peachpuff" => Color.FromArgb(255, 255, 218, 185),
            "navajowhite" => Color.FromArgb(255, 255, 222, 173),
            "moccasin" => Color.FromArgb(255, 255, 228, 181),
            "cornsilk" => Color.FromArgb(255, 255, 248, 220),
            "ivory" => Color.FromArgb(255, 255, 255, 240),
            "lemonchiffon" => Color.FromArgb(255, 255, 250, 205),
            "lightyellow" => Color.FromArgb(255, 255, 255, 224),
            "lightgoldenrodyellow" => Color.FromArgb(255, 250, 250, 210),
            "beige" => Color.FromArgb(255, 245, 245, 220),
            "wheat" => Color.FromArgb(255, 245, 222, 179),
            "sandybrown" => Color.FromArgb(255, 244, 164, 96),
            "goldenrod" => Color.FromArgb(255, 218, 165, 32),
            "darkgoldenrod" => Color.FromArgb(255, 184, 134, 11),
            "gold" => Color.FromArgb(255, 255, 215, 0),
            "khaki" => Color.FromArgb(255, 240, 230, 140),
            "darkkhaki" => Color.FromArgb(255, 189, 183, 107),
            "tan" => Color.FromArgb(255, 210, 180, 140),
            "burlywood" => Color.FromArgb(255, 222, 184, 135),
            "peru" => Color.FromArgb(255, 205, 133, 63),
            "chocolate" => Color.FromArgb(255, 210, 105, 30),
            "sienna" => Color.FromArgb(255, 160, 82, 45),
            "saddlebrown" => Color.FromArgb(255, 139, 69, 19),
            "brown" => Color.FromArgb(255, 165, 42, 42),
            "firebrick" => Color.FromArgb(255, 178, 34, 34),
            "darkred" => Color.FromArgb(255, 139, 0, 0),
            "indianred" => Color.FromArgb(255, 205, 92, 92),
            "rosybrown" => Color.FromArgb(255, 188, 143, 143),
            "lightcoral" => Color.FromArgb(255, 240, 128, 128),
            "salmon" => Color.FromArgb(255, 250, 128, 114),
            "darksalmon" => Color.FromArgb(255, 233, 150, 122),
            "lightsalmon" => Color.FromArgb(255, 255, 160, 122),
            "coral" => Color.FromArgb(255, 255, 127, 80),
            "tomato" => Color.FromArgb(255, 255, 99, 71),
            "orangered" => Color.FromArgb(255, 255, 69, 0),
            "darkorange" => Color.FromArgb(255, 255, 140, 0),
            "crimson" => Color.FromArgb(255, 220, 20, 60),
            "deeppink" => Color.FromArgb(255, 255, 20, 147),
            "hotpink" => Color.FromArgb(255, 255, 105, 180),
            "lightpink" => Color.FromArgb(255, 255, 182, 193),
            "pink" => Color.FromArgb(255, 255, 192, 203),
            "palevioletred" => Color.FromArgb(255, 219, 112, 147),
            "mediumvioletred" => Color.FromArgb(255, 199, 21, 133),
            "orchid" => Color.FromArgb(255, 218, 112, 214),
            "plum" => Color.FromArgb(255, 221, 160, 221),
            "violet" => Color.FromArgb(255, 238, 130, 238),
            "mediumpurple" => Color.FromArgb(255, 147, 112, 219),
            "darkorchid" => Color.FromArgb(255, 153, 50, 204),
            "darkviolet" => Color.FromArgb(255, 148, 0, 211),
            "darkmagenta" => Color.FromArgb(255, 139, 0, 139),
            "blueviolet" => Color.FromArgb(255, 138, 43, 226),
            "indigo" => Color.FromArgb(255, 75, 0, 130),
            "rebeccapurple" => Color.FromArgb(255, 102, 51, 153),
            "slateblue" => Color.FromArgb(255, 106, 90, 205),
            "darkslateblue" => Color.FromArgb(255, 72, 61, 139),
            "mediumslateblue" => Color.FromArgb(255, 123, 104, 238),
            "lavender" => Color.FromArgb(255, 230, 230, 250),
            "thistle" => Color.FromArgb(255, 216, 191, 216),
            "mistyrose" => Color.FromArgb(255, 255, 228, 225),
            "lavenderblush" => Color.FromArgb(255, 255, 240, 245),
            "honeydew" => Color.FromArgb(255, 240, 255, 240),
            "mintcream" => Color.FromArgb(255, 245, 255, 250),
            "azure" => Color.FromArgb(255, 240, 255, 255),
            "lightsteelblue" => Color.FromArgb(255, 176, 196, 222),
            "powderblue" => Color.FromArgb(255, 176, 224, 230),
            "lightblue" => Color.FromArgb(255, 173, 216, 230),
            "skyblue" => Color.FromArgb(255, 135, 206, 235),
            "lightskyblue" => Color.FromArgb(255, 135, 206, 250),
            "deepskyblue" => Color.FromArgb(255, 0, 191, 255),
            "dodgerblue" => Color.FromArgb(255, 30, 144, 255),
            "cornflowerblue" => Color.FromArgb(255, 100, 149, 237),
            "steelblue" => Color.FromArgb(255, 70, 130, 180),
            "royalblue" => Color.FromArgb(255, 65, 105, 225),
            "mediumblue" => Color.FromArgb(255, 0, 0, 205),
            "darkblue" => Color.FromArgb(255, 0, 0, 139),
            "midnightblue" => Color.FromArgb(255, 25, 25, 112),
            "cadetblue" => Color.FromArgb(255, 95, 158, 160),
            "paleturquoise" => Color.FromArgb(255, 175, 238, 238),
            "turquoise" => Color.FromArgb(255, 64, 224, 208),
            "mediumturquoise" => Color.FromArgb(255, 72, 209, 204),
            "darkturquoise" => Color.FromArgb(255, 0, 206, 209),
            "lightcyan" => Color.FromArgb(255, 224, 255, 255),
            "mediumaquamarine" => Color.FromArgb(255, 102, 205, 170),
            "aquamarine" => Color.FromArgb(255, 127, 255, 212),
            "darkseagreen" => Color.FromArgb(255, 143, 188, 143),
            "mediumseagreen" => Color.FromArgb(255, 60, 179, 113),
            "seagreen" => Color.FromArgb(255, 46, 139, 87),
            "darkcyan" => Color.FromArgb(255, 0, 139, 139),
            "lightseagreen" => Color.FromArgb(255, 32, 178, 170),
            "lightgreen" => Color.FromArgb(255, 144, 238, 144),
            "palegreen" => Color.FromArgb(255, 152, 251, 152),
            "springgreen" => Color.FromArgb(255, 0, 255, 127),
            "mediumspringgreen" => Color.FromArgb(255, 0, 250, 154),
            "lawngreen" => Color.FromArgb(255, 124, 252, 0),
            "chartreuse" => Color.FromArgb(255, 127, 255, 0),
            "greenyellow" => Color.FromArgb(255, 173, 255, 47),
            "yellowgreen" => Color.FromArgb(255, 154, 205, 50),
            "limegreen" => Color.FromArgb(255, 50, 205, 50),
            "forestgreen" => Color.FromArgb(255, 34, 139, 34),
            "darkgreen" => Color.FromArgb(255, 0, 100, 0),
            "olivedrab" => Color.FromArgb(255, 107, 142, 35),
            "darkolivegreen" => Color.FromArgb(255, 85, 107, 47),

            _ => ResolveExtendedColorName(colorName),
        };
    }

    /// <summary>
    /// Fallback for CSS named colors not in the primary switch. Uses
    /// <see cref="Color.FromName"/> which recognises .NET known colors
    /// (case-insensitive). Returns black if the name is unrecognised.
    /// </summary>
    private static Color ResolveExtendedColorName(string colorName)
    {
        var c = Color.FromName(colorName);
        if (c.IsKnownColor)
            return Color.FromArgb(c.A, c.R, c.G, c.B);
        return Color.FromArgb(255, 0, 0, 0);
    }

    protected override RPen CreatePen(Color color)
    {
        var paint = new SKPaint
        {
            Color = Utilities.Utils.Convert(color),
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeWidth = 1
        };
        return new PenAdapter(paint);
    }

    protected override RBrush CreateSolidBrush(Color color)
    {
        var paint = new SKPaint
        {
            Color = Utilities.Utils.Convert(color),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        return new BrushAdapter(paint, false);
    }

    protected override RBrush CreateLinearGradientBrush(RectangleF rect, Color color1, Color color2, double angle)
    {
        var radians = angle * Math.PI / 180.0;
        var cos = (float)Math.Cos(radians);
        var sin = (float)Math.Sin(radians);
        var cx = (float)(rect.X + rect.Width / 2);
        var cy = (float)(rect.Y + rect.Height / 2);
        var halfDiag = (float)Math.Max(rect.Width, rect.Height) / 2;

        var startPoint = new SKPoint(cx - cos * halfDiag, cy - sin * halfDiag);
        var endPoint = new SKPoint(cx + cos * halfDiag, cy + sin * halfDiag);

        var shader = SKShader.CreateLinearGradient(
            startPoint,
            endPoint,
            new[] { Utilities.Utils.Convert(color1), Utilities.Utils.Convert(color2) },
            null,
            SKShaderTileMode.Clamp);

        var paint = new SKPaint
        {
            Shader = shader,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        return new BrushAdapter(paint, true);
    }

    protected override RImage ConvertImageInt(object image) => image != null ? new ImageAdapter((SKBitmap)image) : null;

    protected override RImage ImageFromStreamInt(Stream memoryStream)
    {
        // Read the stream into a byte array so we can inspect the content
        // before attempting a bitmap decode.  SKBitmap.Decode silently
        // returns null for formats it cannot handle (e.g. SVG).
        byte[] data;
        if (memoryStream is MemoryStream ms)
        {
            data = ms.ToArray();
        }
        else if (memoryStream.CanSeek)
        {
            data = new byte[memoryStream.Length - memoryStream.Position];
            _ = memoryStream.Read(data, 0, data.Length);
        }
        else
        {
            using var copy = new MemoryStream();
            memoryStream.CopyTo(copy);
            data = copy.ToArray();
        }

        if (IsSvgData(data))
        {
            return RasterizeSvg(data);
        }

        var bitmap = SKBitmap.Decode(data);
        return bitmap != null ? new ImageAdapter(bitmap) : null;
    }

    /// <summary>
    /// Rasterizes SVG data to an <see cref="SKBitmap"/> using Svg.Skia.
    /// Parses width/height from the SVG root element to determine output
    /// dimensions.  Per the HTML spec, when the SVG does not specify both
    /// explicit width AND height the intrinsic size is 300×150 (the default
    /// replaced element size).  This matches browser behaviour (Chromium).
    /// </summary>
    private static RImage RasterizeSvg(byte[] data)
    {
        var svgContent = System.Text.Encoding.UTF8.GetString(data);

        // Parse the SVG root element's width, height and viewBox attributes.
        var (svgWidth, svgHeight, vbRatio) = ParseSvgIntrinsicDimensions(svgContent);
        bool preserveAspectRatioNone = HasPreserveAspectRatioNone(svgContent);

        bool parsedIntrinsicWidth = svgWidth > 0;
        bool parsedIntrinsicHeight = svgHeight > 0;
        int width, height;
        // Chrome's SVG sizing for <img> elements: only when BOTH explicit
        // width and height attributes are present does the SVG have true
        // intrinsic dimensions and an intrinsic aspect ratio.  When either
        // dimension is missing Chrome falls back to the 300×150 default
        // object size, regardless of viewBox or partial attributes.
        bool hasBothDimensions = parsedIntrinsicWidth && parsedIntrinsicHeight;
        if (hasBothDimensions)
        {
            width = (int)Math.Ceiling(svgWidth);
            height = (int)Math.Ceiling(svgHeight);
        }
        else
        {
            width = 300;
            height = 150;
        }

        bool suppressPartialIntrinsicDimensions =
            preserveAspectRatioNone && vbRatio > 0 && !hasBothDimensions;

        SKBitmap? bitmap;
        if (suppressPartialIntrinsicDimensions)
        {
            const int fallbackRenderScale = 4;
            int renderWidth = width * fallbackRenderScale;
            int renderHeight = height * fallbackRenderScale;
            bitmap = RenderSvgToBitmap(svgContent, renderWidth, renderHeight);
            if (bitmap != null && !IsBitmapFullyTransparent(bitmap))
            {
                bitmap = NormalizeSvgContentBounds(bitmap, renderWidth, renderHeight);
            }
            else
            {
                bitmap?.Dispose();
                var svgForRender = EnsureSvgViewport(svgContent, svgWidth, svgHeight, width, height);
                bitmap = RenderSvgToBitmap(svgForRender, renderWidth, renderHeight);
            }
        }
        else
        {
            // SVGs that use percentage-based dimensions internally (e.g.
            // width="100%" on child elements) need explicit viewport dimensions
            // on the root <svg> element for percentage resolution.  Inject the
            // computed width/height before parsing when the root element is
            // missing one or both attributes.
            var svgForRender = EnsureSvgViewport(svgContent, svgWidth, svgHeight, width, height);
            bitmap = RenderSvgToBitmap(svgForRender, width, height);
        }

        if (bitmap == null)
            return null;

        if (TryParseSolidViewportFill(svgContent, out var solidFill))
            bitmap.Erase(solidFill);

        // Only SVGs with both explicit width and height have intrinsic
        // dimensions.  A viewBox exposes an intrinsic ratio only when the
        // root element preserves aspect ratio; preserveAspectRatio="none"
        // allows non-uniform scaling and therefore does not contribute an
        // intrinsic ratio for CSS background-size calculations.
        bool hasIntrinsicRatio = hasBothDimensions || (vbRatio > 0 && !preserveAspectRatioNone);
        double intrinsicRatio = hasBothDimensions
            ? svgWidth / svgHeight
            : (preserveAspectRatioNone ? 0 : vbRatio);
        return new ImageAdapter(bitmap,
            hasIntrinsicRatio: hasIntrinsicRatio,
            hasIntrinsicWidth: parsedIntrinsicWidth && !suppressPartialIntrinsicDimensions,
            hasIntrinsicHeight: parsedIntrinsicHeight && !suppressPartialIntrinsicDimensions,
            intrinsicAspectRatio: intrinsicRatio > 0 ? intrinsicRatio : null,
            intrinsicWidth: parsedIntrinsicWidth && !suppressPartialIntrinsicDimensions ? svgWidth : 0,
            intrinsicHeight: parsedIntrinsicHeight && !suppressPartialIntrinsicDimensions ? svgHeight : 0);
    }

    private static SKBitmap? RenderSvgToBitmap(string svgContent, int width, int height)
    {
        using var svg = new Svg.Skia.SKSvg();
        svg.FromSvg(svgContent);

        if (svg.Picture == null)
            return null;

        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var cullRect = svg.Picture.CullRect;
        if (cullRect.Width > 0 && cullRect.Height > 0
            && ((int)Math.Ceiling(cullRect.Width) != width
                || (int)Math.Ceiling(cullRect.Height) != height))
        {
            float scaleX = width / cullRect.Width;
            float scaleY = height / cullRect.Height;
            canvas.Scale(scaleX, scaleY);
        }

        canvas.DrawPicture(svg.Picture);
        return bitmap;
    }

    private static SKBitmap NormalizeSvgContentBounds(SKBitmap bitmap, int width, int height)
    {
        int minX = bitmap.Width;
        int minY = bitmap.Height;
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha == 0)
                    continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY
            || (minX == 0 && minY == 0 && maxX == bitmap.Width - 1 && maxY == bitmap.Height - 1))
            return bitmap;

        int croppedWidth = maxX - minX + 1;
        int croppedHeight = maxY - minY + 1;
        var nonEmptyCols = new List<int>(croppedWidth);
        for (int x = minX; x <= maxX; x++)
        {
            bool hasOpaquePixel = false;
            for (int y = minY; y <= maxY; y++)
            {
                if (bitmap.GetPixel(x, y).Alpha != 0)
                {
                    hasOpaquePixel = true;
                    break;
                }
            }

            if (hasOpaquePixel)
                nonEmptyCols.Add(x);
        }

        var nonEmptyRows = new List<int>(croppedHeight);
        for (int y = minY; y <= maxY; y++)
        {
            bool hasOpaquePixel = false;
            for (int x = minX; x <= maxX; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha != 0)
                {
                    hasOpaquePixel = true;
                    break;
                }
            }

            if (hasOpaquePixel)
                nonEmptyRows.Add(y);
        }

        if (nonEmptyCols.Count == 0 || nonEmptyRows.Count == 0)
            return bitmap;

        var condensed = new SKBitmap(nonEmptyCols.Count, nonEmptyRows.Count, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (int destY = 0; destY < nonEmptyRows.Count; destY++)
        {
            int srcY = nonEmptyRows[destY];
            for (int destX = 0; destX < nonEmptyCols.Count; destX++)
            {
                condensed.SetPixel(destX, destY, bitmap.GetPixel(nonEmptyCols[destX], srcY));
            }
        }

        var normalized = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(normalized);
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint
        {
            FilterQuality = SKFilterQuality.None,
            IsAntialias = false,
        };
        canvas.DrawBitmap(condensed, new SKRect(0, 0, width, height), paint);
        condensed.Dispose();
        bitmap.Dispose();
        return normalized;
    }

    /// <summary>
    /// Ensures the SVG root element has explicit width and height attributes
    /// so that percentage-based child dimensions (e.g. width="100%") can
    /// resolve correctly.  Returns the original content unchanged when both
    /// attributes are already present.
    /// </summary>
    private static string EnsureSvgViewport(
        string svgContent,
        double parsedWidth, double parsedHeight,
        int targetWidth, int targetHeight)
    {
        // Both intrinsic dimensions are present – nothing to inject.
        if (parsedWidth > 0 && parsedHeight > 0)
            return svgContent;

        int svgIdx = svgContent.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
        if (svgIdx < 0)
            return svgContent;

        int tagEnd = svgContent.IndexOf('>', svgIdx);
        if (tagEnd < 0)
            return svgContent;

        var tag = svgContent.Substring(svgIdx, tagEnd - svgIdx + 1);
        var updatedTag = tag;
        if (parsedWidth <= 0)
        {
            // Replace non-intrinsic/percentage width (or missing width) with
            // an explicit viewport width so Svg.Skia can resolve percentages.
            // Use the raster target size rather than raw viewBox dimensions:
            // extreme viewBox values (used by WPT SVG sizing tests) are
            // coordinate-space metadata, not the CSS viewport size that
            // percentage children should resolve against.
            int effectiveWidth = targetWidth;
            updatedTag = System.Text.RegularExpressions.Regex.Replace(
                updatedTag,
                @"\swidth\s*=\s*[""'][^""']*[""']",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            updatedTag = updatedTag.Insert(updatedTag.Length - 1, $" width=\"{effectiveWidth}\"");
        }

        if (parsedHeight <= 0)
        {
            // Replace non-intrinsic/percentage height (or missing height)
            // with an explicit viewport height.  See width handling above.
            int effectiveHeight = targetHeight;
            updatedTag = System.Text.RegularExpressions.Regex.Replace(
                updatedTag,
                @"\sheight\s*=\s*[""'][^""']*[""']",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            updatedTag = updatedTag.Insert(updatedTag.Length - 1, $" height=\"{effectiveHeight}\"");
        }

        return svgContent.Substring(0, svgIdx) + updatedTag + svgContent[(tagEnd + 1)..];
    }

    /// <summary>
    /// Parses the SVG root element to extract intrinsic width, height and
    /// viewBox aspect ratio.  Returns (width, height, viewBoxRatio) where
    /// values ≤ 0 indicate the attribute was absent or non-numeric.
    /// </summary>
    private static (double width, double height, double viewBoxRatio) ParseSvgIntrinsicDimensions(string svg)
    {
        double w = -1, h = -1, ratio = -1;

        // Find the <svg ...> opening tag.
        int svgIdx = svg.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
        if (svgIdx < 0) return (w, h, ratio);
        int tagEnd = svg.IndexOf('>', svgIdx);
        if (tagEnd < 0) return (w, h, ratio);
        var tag = svg.Substring(svgIdx, tagEnd - svgIdx + 1);

        // Parse width attribute (only absolute units / plain numbers).
        w = ParseSvgLengthAttribute(tag, "width");
        h = ParseSvgLengthAttribute(tag, "height");

        // Parse viewBox for aspect ratio.
        var vbMatch = System.Text.RegularExpressions.Regex.Match(
            tag, @"viewBox\s*=\s*[""']([^""']+)[""']",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (vbMatch.Success)
        {
            var parts = vbMatch.Groups[1].Value.Split(
                new[] { ' ', ',', '\t', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4
                && double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double vbW)
                && double.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double vbH)
                && vbW > 0 && vbH > 0)
            {
                ratio = vbW / vbH;
            }
        }

        return (w, h, ratio);
    }

    private static bool HasPreserveAspectRatioNone(string svg)
    {
        int svgIdx = svg.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
        if (svgIdx < 0) return false;
        int tagEnd = svg.IndexOf('>', svgIdx);
        if (tagEnd < 0) return false;
        var tag = svg.Substring(svgIdx, tagEnd - svgIdx + 1);

        var m = System.Text.RegularExpressions.Regex.Match(
            tag,
            @"(?<!\w)preserveAspectRatio\s*=\s*[""']([^""']+)[""']",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) return false;

        return m.Groups[1].Value.Trim().StartsWith("none", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts a numeric length value from an SVG attribute.  Returns the
    /// value in pixels for plain numbers and "px" units; returns -1 for
    /// percentage values or absent attributes.
    /// </summary>
    private static double ParseSvgLengthAttribute(string tag, string name)
    {
        // Match name="value" but avoid matching longer attribute names
        // (e.g. "width" should not match "stroke-width").
        var m = System.Text.RegularExpressions.Regex.Match(
            tag, @"(?<!\w)" + name + @"\s*=\s*[""']([^""']+)[""']",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) return -1;

        var val = m.Groups[1].Value.Trim();
        // Ignore percentage values – they are not intrinsic dimensions.
        if (val.EndsWith('%')) return -1;
        // Strip "px" suffix.
        if (val.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            val = val[..^2];

        return double.TryParse(val, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double v) && v > 0 ? v : -1;
    }

    private static bool IsBitmapFullyTransparent(SKBitmap bitmap)
    {
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha != 0)
                    return false;
            }
        }

        return true;
    }

    private static bool TryParseSolidViewportFill(string svgContent, out SKColor color)
    {
        color = SKColors.Transparent;

        var rectMatches = System.Text.RegularExpressions.Regex.Matches(
            svgContent,
            @"<rect\b[^>]*>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (rectMatches.Count != 1)
            return false;

        var rectTag = rectMatches[0].Value;
        bool fillsViewport =
            System.Text.RegularExpressions.Regex.IsMatch(rectTag, @"\bwidth\s*=\s*[""']100%[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            && System.Text.RegularExpressions.Regex.IsMatch(rectTag, @"\bheight\s*=\s*[""']100%[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!fillsViewport)
            return false;

        var fillMatch = System.Text.RegularExpressions.Regex.Match(
            rectTag,
            @"\bfill\s*=\s*[""']([^""']+)[""']",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!fillMatch.Success)
            return false;

        var fillValue = fillMatch.Groups[1].Value.Trim();
        try
        {
            var parsed = System.Drawing.ColorTranslator.FromHtml(fillValue);
            color = new SKColor(parsed.R, parsed.G, parsed.B, parsed.A);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether the given byte array contains SVG image data by
    /// looking for an XML declaration (&lt;?xml) or an &lt;svg root element
    /// within the first 1 KB of content (after skipping leading whitespace
    /// and any UTF-8 BOM).
    /// </summary>
    private static bool IsSvgData(byte[] data)
    {
        if (data == null || data.Length < 4)
            return false;

        // Skip UTF-8 BOM if present
        int offset = 0;
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            offset = 3;

        // Skip leading whitespace
        while (offset < data.Length && (data[offset] == ' ' || data[offset] == '\t' ||
               data[offset] == '\r' || data[offset] == '\n'))
            offset++;

        if (offset >= data.Length)
            return false;

        // Scan the first 1 KB for SVG markers
        int scanLength = Math.Min(data.Length, offset + 1024);
        var header = System.Text.Encoding.UTF8.GetString(data, offset, scanLength - offset);

        // Accept SVG content even when the file starts with comments or
        // DOCTYPE declarations before the root <svg> tag.
        return header.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    protected override RFont CreateFontInt(string family, double size, FontStyle style)
    {
        var skStyle = ConvertFontStyle(style);
        // Prefer typefaces loaded from files over the system font manager,
        // because SKFontManager.Default cannot resolve fonts that were
        // loaded with SKTypeface.FromFile (they are not registered with
        // the native OS font manager).
        if (_loadedTypefaces.TryGetValue(family, out var loaded))
            return new FontAdapter(loaded, size, style);
        var typeface = SKTypeface.FromFamilyName(family, skStyle) ?? SKTypeface.Default;
        return new FontAdapter(typeface, size, style);
    }

    protected override RFont CreateFontInt(RFontFamily family, double size, FontStyle style) => CreateFontInt(family.Name, size, style);

    private static SKFontStyle ConvertFontStyle(FontStyle style)
    {
        var weight = (style & FontStyle.Bold) != 0 ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = (style & FontStyle.Italic) != 0 ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        return new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);
    }
}
