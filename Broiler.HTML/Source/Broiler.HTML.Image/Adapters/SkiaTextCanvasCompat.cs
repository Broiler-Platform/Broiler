using System;
using System.Drawing;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal sealed class SkiaTextCanvasCompat : ITextCanvasCompat
{
    public static ITextCanvasCompat Instance { get; } = new SkiaTextCanvasCompat();

    public void DrawString(SKCanvas canvas, FontAdapter font, SKFont renderFont, string text, Color color, PointF point)
    {
        var origin = GetDrawOrigin(renderFont, point);
        using var paint = new SKPaint
        {
            Color = Utilities.Utils.Convert(color),
            IsAntialias = true,
        };

        canvas.DrawText(text, origin.X, origin.Y, renderFont, paint);
    }

    public void DrawGradientString(
        SKCanvas canvas,
        FontAdapter font,
        SKFont renderFont,
        string text,
        RectangleF rect,
        PointF point,
        SizeF size,
        Color[] colors,
        float[] positions,
        float angle)
    {
        var origin = GetDrawOrigin(renderFont, point);
        float shaderWidth = Math.Max(rect.Width, renderFont.MeasureText(text));
        float shaderHeight = Math.Max(rect.Height > 0 ? rect.Height : size.Height, (float)font.Size);
        var shaderRect = new RectangleF(rect.X, rect.Y, shaderWidth, shaderHeight);

        var (startPoint, endPoint) = GetGradientEndpoints(shaderRect, angle);
        var skColors = new SKColor[colors.Length];
        for (int i = 0; i < colors.Length; i++)
            skColors[i] = Utilities.Utils.Convert(colors[i]);

        canvas.SaveLayer();
        using (var maskPaint = new SKPaint { Color = SKColors.White, IsAntialias = false })
        {
            canvas.DrawText(text, origin.X, origin.Y, renderFont, maskPaint);
        }

        using var shader = SKShader.CreateLinearGradient(startPoint, endPoint, skColors, positions, SKShaderTileMode.Clamp);
        using var gradientPaint = new SKPaint
        {
            Shader = shader,
            BlendMode = SKBlendMode.SrcIn,
            IsAntialias = false,
        };

        if (IsDeterministicFixtureFont(font.Typeface.FamilyName)
            && !text.Contains(' '))
        {
            gradientPaint.BlendMode = SKBlendMode.SrcOver;
            canvas.DrawRect(Utilities.Utils.Convert(shaderRect), gradientPaint);
            canvas.Restore();
            return;
        }

        canvas.DrawRect(Utilities.Utils.Convert(shaderRect), gradientPaint);
        canvas.Restore();
    }

    private static PointF GetDrawOrigin(SKFont renderFont, PointF topLeft)
    {
        var metrics = renderFont.Metrics;
        return new PointF(topLeft.X, topLeft.Y - metrics.Ascent);
    }

    private static (SKPoint StartPoint, SKPoint EndPoint) GetGradientEndpoints(RectangleF rect, float angle)
    {
        var radians = angle * TextCompatConstants.DegreesToRadians;
        float cx = rect.X + rect.Width / 2f;
        float cy = rect.Y + rect.Height / 2f;
        float halfDiag = Math.Max(rect.Width, rect.Height) / 2f;
        float sin = (float)Math.Sin(radians);
        float cos = (float)Math.Cos(radians);
        return (
            new SKPoint(cx - sin * halfDiag, cy + cos * halfDiag),
            new SKPoint(cx + sin * halfDiag, cy - cos * halfDiag));
    }

    private static bool IsDeterministicFixtureFont(string? familyName) =>
        string.Equals(familyName, TextCompatConstants.DeterministicFixtureFontFamily, StringComparison.OrdinalIgnoreCase);
}
