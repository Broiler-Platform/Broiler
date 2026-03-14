using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Avalonia.Utilities;
using FontStyle = System.Drawing.FontStyle;
using Color = System.Drawing.Color;
using RectangleF = System.Drawing.RectangleF;

namespace TheArtOfDev.HtmlRenderer.Avalonia.Adapters;

internal sealed class AvaloniaAdapter : RAdapter
{
    private AvaloniaAdapter()
    {
        var systemFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in FontManager.Current.SystemFonts)
        {
            try
            {
                AddFontFamily(new FontFamilyAdapter(family));
                systemFonts.Add(family.Name);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HtmlRenderer] AvaloniaAdapter failed to add font family: {ex.Message}");
            }
        }

        // CSS 2.1 §15.3 generic font family mappings.
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

    private static string FirstAvailable(HashSet<string> systemFonts, params string[] candidates)
    {
        return Array.Find(candidates, c => systemFonts.Contains(c));
    }

    public static AvaloniaAdapter Instance { get; } = new();

    protected override Color GetColorInt(string colorName)
    {
        if (global::Avalonia.Media.Color.TryParse(colorName, out var color))
            return Utils.Convert(color);

        return Color.Empty;
    }

    protected override RPen CreatePen(Color color) => new PenAdapter(GetSolidColorBrush(color));

    protected override RBrush CreateSolidBrush(Color color)
    {
        var solidBrush = GetSolidColorBrush(color);
        return new BrushAdapter(solidBrush);
    }

    protected override RBrush CreateLinearGradientBrush(RectangleF rect, Color color1, Color color2, double angle)
    {
        var startColor = angle <= 180 ? Utils.Convert(color1) : Utils.Convert(color2);
        var endColor = angle <= 180 ? Utils.Convert(color2) : Utils.Convert(color1);
        angle = angle <= 180 ? angle : angle - 180;

        double x = angle < 135 ? Math.Max((angle - 45) / 90, 0) : 1;
        double y = angle <= 45 ? Math.Max(0.5 - angle / 90, 0) : angle > 135 ? Math.Abs(1.5 - angle / 90) : 0;

        var brush = new LinearGradientBrush
        {
            StartPoint = new global::Avalonia.RelativePoint(x, y, global::Avalonia.RelativeUnit.Relative),
            EndPoint = new global::Avalonia.RelativePoint(1 - x, 1 - y, global::Avalonia.RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(startColor, 0),
                new GradientStop(endColor, 1)
            }
        };
        return new BrushAdapter(brush.ToImmutable());
    }

    protected override RImage ConvertImageInt(object image) => image != null ? new ImageAdapter((Bitmap)image) : null!;

    protected override RImage ImageFromStreamInt(Stream memoryStream)
    {
        var bitmap = new Bitmap(memoryStream);
        return new ImageAdapter(bitmap);
    }

    protected override RFont CreateFontInt(string family, double size, FontStyle style)
    {
        var fontFamily = new FontFamily(family);
        return new FontAdapter(new Typeface(fontFamily, GetAvaloniaFontStyle(style), GetAvaloniaFontWeight(style)), size);
    }

    protected override RFont CreateFontInt(RFontFamily family, double size, FontStyle style) =>
        new FontAdapter(new Typeface(((FontFamilyAdapter)family).FontFamily, GetAvaloniaFontStyle(style), GetAvaloniaFontWeight(style)), size);

    protected override RContextMenu CreateContextMenuInt() => new ContextMenuAdapter();

    private static IBrush GetSolidColorBrush(Color color)
    {
        if (color == Color.White)
            return Brushes.White;
        if (color == Color.Black)
            return Brushes.Black;
        if (color.A < 1)
            return Brushes.Transparent;
        return new SolidColorBrush(Utils.Convert(color)).ToImmutable();
    }

    private static global::Avalonia.Media.FontStyle GetAvaloniaFontStyle(FontStyle style)
    {
        if ((style & FontStyle.Italic) == FontStyle.Italic)
            return global::Avalonia.Media.FontStyle.Italic;

        return global::Avalonia.Media.FontStyle.Normal;
    }

    private static FontWeight GetAvaloniaFontWeight(FontStyle style)
    {
        if ((style & FontStyle.Bold) == FontStyle.Bold)
            return FontWeight.Bold;

        return FontWeight.Normal;
    }
}
