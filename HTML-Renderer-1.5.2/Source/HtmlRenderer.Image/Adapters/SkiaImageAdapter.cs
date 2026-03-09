using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.Image.Adapters;

internal sealed class SkiaImageAdapter : RAdapter
{
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
        return Array.Find(candidates, c => systemFonts.Contains(c));
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
    public string? LoadFontFromFile(string path, string? mapFromName = null)
    {
        var typeface = SKTypeface.FromFile(path);
        if (typeface == null)
            return null;

        var familyName = typeface.FamilyName;
        AddFontFamily(new FontFamilyAdapter(familyName));

        if (!string.IsNullOrEmpty(mapFromName))
            AddFontFamilyMapping(mapFromName!, familyName);

        // Do not dispose the typeface — SkiaSharp's font manager retains
        // a reference so that subsequent SKTypeface.FromFamilyName lookups
        // can resolve the loaded family.  Disposing would invalidate it.
        return familyName;
    }

    protected override Color GetColorInt(string colorName)
    {
        if (SKColor.TryParse(colorName, out var color))
            return Utilities.Utils.Convert(color);

        // Fallback: try common color names
        return colorName.ToLowerInvariant() switch
        {
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
            _ => Color.FromArgb(255, 0, 0, 0), // default to black
        };
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
        var bitmap = SKBitmap.Decode(memoryStream);
        return bitmap != null ? new ImageAdapter(bitmap) : null;
    }

    protected override RFont CreateFontInt(string family, double size, FontStyle style)
    {
        var skStyle = ConvertFontStyle(style);
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
