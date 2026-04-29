using System;
using System.Drawing;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal sealed class SkiaCanvasCompat : ICanvasCompat
{
    public static ICanvasCompat Instance { get; } = new SkiaCanvasCompat();

    public void DrawLine(SKCanvas canvas, float x1, float y1, float x2, float y2, SKPaint paint) =>
        canvas.DrawLine(x1, y1, x2, y2, paint);

    public void DrawRectangle(SKCanvas canvas, RectangleF rect, SKPaint paint) =>
        canvas.DrawRect(Utilities.Utils.Convert(rect), paint);

    public void DrawPath(SKCanvas canvas, GraphicsPathAdapter path, SKPaint paint) =>
        canvas.DrawPath(path.Path, paint);

    public void ClipRounded(
        SKCanvas canvas,
        RectangleF rect,
        double cornerNw,
        double cornerNwY,
        double cornerNe,
        double cornerNeY,
        double cornerSe,
        double cornerSeY,
        double cornerSw,
        double cornerSwY)
    {
        if ((cornerNw <= 0 && cornerNwY <= 0)
            && (cornerNe <= 0 && cornerNeY <= 0)
            && (cornerSe <= 0 && cornerSeY <= 0)
            && (cornerSw <= 0 && cornerSwY <= 0))
        {
            canvas.ClipRect(Utilities.Utils.Convert(rect));
            return;
        }

        var skRect = Utilities.Utils.Convert(rect);
        var radii = new[]
        {
            new SKPoint((float)cornerNw, (float)cornerNwY),
            new SKPoint((float)cornerNe, (float)cornerNeY),
            new SKPoint((float)cornerSe, (float)cornerSeY),
            new SKPoint((float)cornerSw, (float)cornerSwY),
        };
        var rrect = new SKRoundRect();
        rrect.SetRectRadii(skRect, radii);
        canvas.ClipRoundRect(rrect);
    }

    public SKPaint CreateTexturePaint(BBitmap bitmap, PointF translateTransformLocation)
    {
        var paint = new SKPaint();
        paint.Shader = SKShader.CreateBitmap(
            bitmap.AsSkBitmap(),
            SKShaderTileMode.Repeat,
            SKShaderTileMode.Repeat,
            SKMatrix.CreateTranslation((float)translateTransformLocation.X, (float)translateTransformLocation.Y));
        return paint;
    }

    public void DrawPolygon(SKCanvas canvas, PointF[] points, SKPaint paint)
    {
        using var path = new SKPath();
        path.MoveTo(Utilities.Utils.Convert(points[0]));

        for (int i = 1; i < points.Length; i++)
            path.LineTo(Utilities.Utils.Convert(points[i]));

        path.Close();
        canvas.DrawPath(path, paint);
    }

    public void SaveOpacityLayer(SKCanvas canvas, float opacity)
    {
        byte alpha = (byte)Math.Clamp((int)(opacity * 255), 0, 255);
        using var paint = new SKPaint { Color = new SKColor(0, 0, 0, alpha) };
        canvas.SaveLayer(paint);
    }

    public void SaveBlendLayer(SKCanvas canvas, string blendMode)
    {
        var skBlendMode = blendMode?.ToLowerInvariant() switch
        {
            "multiply" => SKBlendMode.Multiply,
            "screen" => SKBlendMode.Screen,
            "overlay" => SKBlendMode.Overlay,
            "darken" => SKBlendMode.Darken,
            "lighten" => SKBlendMode.Lighten,
            "color-dodge" => SKBlendMode.ColorDodge,
            "color-burn" => SKBlendMode.ColorBurn,
            "hard-light" => SKBlendMode.HardLight,
            "soft-light" => SKBlendMode.SoftLight,
            "difference" => SKBlendMode.Difference,
            "exclusion" => SKBlendMode.Exclusion,
            "hue" => SKBlendMode.Hue,
            "saturation" => SKBlendMode.Saturation,
            "color" => SKBlendMode.Color,
            "luminosity" => SKBlendMode.Luminosity,
            "plus-lighter" => SKBlendMode.Plus,
            _ => SKBlendMode.SrcOver,
        };

        using var paint = new SKPaint { BlendMode = skBlendMode };
        canvas.SaveLayer(paint);
    }
}
