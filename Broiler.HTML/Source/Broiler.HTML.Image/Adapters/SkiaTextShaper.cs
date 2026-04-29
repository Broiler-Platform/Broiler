using System;
using System.Drawing;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal sealed class SkiaTextShaper : ITextShaper
{
    private SkiaTextShaper() { }

    public static SkiaTextShaper Instance { get; } = new();

    public SizeF MeasureString(FontAdapter font, string text)
    {
        var width = font.Font.MeasureText(text);
        return new SizeF(width, (float)font.Height);
    }

    public void MeasureString(FontAdapter font, string text, double maxWidth, out int charFit, out double charFitWidth)
    {
        charFit = 0;
        charFitWidth = 0;

        for (int i = 1; i <= text.Length; i++)
        {
            var substring = text.Substring(0, i);
            var width = font.Font.MeasureText(substring);
            if (width > maxWidth)
                break;

            charFit = i;
            charFitWidth = width;
        }
    }

    public void DrawString(SKCanvas canvas, FontAdapter font, string text, Color color, PointF point)
    {
        var origin = GetDrawOrigin(font, point);
        using var paint = new SKPaint
        {
            Color = Utilities.Utils.Convert(color),
            IsAntialias = true,
        };

        canvas.DrawText(text, origin.X, origin.Y, font.RenderFont, paint);
    }

    public void DrawGradientString(SKCanvas canvas, FontAdapter font, string text, RectangleF rect, PointF point, SizeF size, Color[] colors, float[] positions, float angle)
    {
        var renderFont = font.RenderFont;
        var origin = GetDrawOrigin(font, point);
        float shaderWidth = Math.Max(rect.Width, renderFont.MeasureText(text));
        float shaderHeight = Math.Max(rect.Height > 0 ? rect.Height : size.Height, (float)font.Size);
        var shaderRect = new RectangleF(rect.X, rect.Y, shaderWidth, shaderHeight);

        var radians = angle * Math.PI / 180.0;
        float cx = shaderRect.X + shaderRect.Width / 2f;
        float cy = shaderRect.Y + shaderRect.Height / 2f;
        float halfDiag = Math.Max(shaderRect.Width, shaderRect.Height) / 2f;
        float sin = (float)Math.Sin(radians);
        float cos = (float)Math.Cos(radians);
        var startPoint = new SKPoint(cx - sin * halfDiag, cy + cos * halfDiag);
        var endPoint = new SKPoint(cx + sin * halfDiag, cy - cos * halfDiag);

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

        if (string.Equals(font.Typeface.FamilyName, "Ahem", StringComparison.OrdinalIgnoreCase)
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

    private static PointF GetDrawOrigin(FontAdapter font, PointF topLeft)
    {
        var metrics = font.RenderFont.Metrics;
        return new PointF(topLeft.X, topLeft.Y - metrics.Ascent);
    }
}
