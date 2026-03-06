using System;
using System.Drawing;
using System.IO;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.Image.Adapters;

internal sealed class SkiaImageAdapter : RAdapter
{
    private SkiaImageAdapter()
    {
        AddFontFamilyMapping("monospace", "Courier New");
        AddFontFamilyMapping("Helvetica", "Arial");

        // Register system fonts
        var fontManager = SKFontManager.Default;
        foreach (var familyName in fontManager.FontFamilies)
        {
            AddFontFamily(new FontFamilyAdapter(familyName));
        }
    }

    public static SkiaImageAdapter Instance { get; } = new();

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
