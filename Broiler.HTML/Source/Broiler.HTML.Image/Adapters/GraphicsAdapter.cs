using System;
using System.Drawing;
using Broiler.HTML.Adapters.Adapters;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal sealed class GraphicsAdapter(SKCanvas canvas, RectangleF initialClip, bool dispose = false) : RGraphics(SkiaImageAdapter.Instance, initialClip)
{
    public override void PopClip()
    {
        canvas.Restore();
        _clipStack.Pop();
    }

    public override void PushClip(RectangleF rect)
    {
        _clipStack.Push(rect);
        canvas.Save();
        canvas.ClipRect(Utilities.Utils.Convert(rect));
    }

    public override void PushClipExclude(RectangleF rect)
    {
        _clipStack.Push(_clipStack.Peek());
        canvas.Save();
        canvas.ClipRect(Utilities.Utils.Convert(rect), SKClipOperation.Difference);
    }

    public override object SetAntiAliasSmoothingMode() =>
        // SkiaSharp uses antialiasing by default in paint objects
        null;

    public override void ReturnPreviousSmoothingMode(object prevMode)
    {
        // No-op for SkiaSharp
    }

    public override SizeF MeasureString(string str, RFont font)
    {
        var fontAdapter = (FontAdapter)font;
        var skFont = fontAdapter.Font;
        var width = skFont.MeasureText(str);
        return new SizeF(width, (float)font.Height);
    }

    public override void MeasureString(string str, RFont font, double maxWidth, out int charFit, out double charFitWidth)
    {
        charFit = 0;
        charFitWidth = 0;

        var fontAdapter = (FontAdapter)font;
        var skFont = fontAdapter.Font;

        // Measure character by character to find how many fit
        for (int i = 1; i <= str.Length; i++)
        {
            var substr = str.Substring(0, i);
            var w = skFont.MeasureText(substr);
            if (w > maxWidth)
                break;
            charFit = i;
            charFitWidth = w;
        }
    }

    public override void DrawString(string str, RFont font, Color color, PointF point, SizeF size, bool rtl)
    {
        var fontAdapter = (FontAdapter)font;
        using var paint = new SKPaint();
        paint.Color = Utilities.Utils.Convert(color);
        paint.IsAntialias = true;

        // Use the CSS px-sized render font for correct glyph dimensions.
        // Baseline positioning uses the render font's own metrics so the
        // top of the text aligns with point.Y.
        var renderFont = fontAdapter.RenderFont;
        var metrics = renderFont.Metrics;
        float y = (float)point.Y - metrics.Ascent;
        float x = (float)point.X;

        canvas.DrawText(str, x, y, renderFont, paint);
    }

    public override RBrush GetTextureBrush(RImage image, RectangleF dstRect, PointF translateTransformLocation)
    {
        var imgAdapter = (ImageAdapter)image;
        var paint = new SKPaint();
        var shader = SKShader.CreateBitmap(
            imgAdapter.Bitmap,
            SKShaderTileMode.Repeat,
            SKShaderTileMode.Repeat,
            SKMatrix.CreateTranslation((float)translateTransformLocation.X, (float)translateTransformLocation.Y));
        paint.Shader = shader;
        return new BrushAdapter(paint, true);
    }

    public override RGraphicsPath GetGraphicsPath() => new GraphicsPathAdapter();

    public override void DrawLine(RPen pen, double x1, double y1, double x2, double y2) => canvas.DrawLine((float)x1, (float)y1, (float)x2, (float)y2, ((PenAdapter)pen).Paint);

    public override void DrawRectangle(RPen pen, double x, double y, double width, double height) => canvas.DrawRect(SKRect.Create((float)x, (float)y, (float)width, (float)height), ((PenAdapter)pen).Paint);

    public override void DrawRectangle(RBrush brush, double x, double y, double width, double height) => canvas.DrawRect(SKRect.Create((float)x, (float)y, (float)width, (float)height), ((BrushAdapter)brush).Paint);

    public override void DrawImage(RImage image, RectangleF destRect, RectangleF srcRect)
    {
        var imgAdapter = (ImageAdapter)image;
        canvas.DrawBitmap(imgAdapter.Bitmap, Utilities.Utils.Convert(srcRect), Utilities.Utils.Convert(destRect));
    }

    public override void DrawImage(RImage image, RectangleF destRect)
    {
        var imgAdapter = (ImageAdapter)image;
        canvas.DrawBitmap(imgAdapter.Bitmap, Utilities.Utils.Convert(destRect));
    }

    public override void DrawPath(RPen pen, RGraphicsPath path) => canvas.DrawPath(((GraphicsPathAdapter)path).Path, ((PenAdapter)pen).Paint);

    public override void DrawPath(RBrush brush, RGraphicsPath path) => canvas.DrawPath(((GraphicsPathAdapter)path).Path, ((BrushAdapter)brush).Paint);

    public override void DrawPolygon(RBrush brush, PointF[] points)
    {
        if (points == null || points.Length == 0)
            return;

        using var path = new SKPath();
        path.MoveTo(Utilities.Utils.Convert(points[0]));

        for (int i = 1; i < points.Length; i++)
            path.LineTo(Utilities.Utils.Convert(points[i]));

        path.Close();
        canvas.DrawPath(path, ((BrushAdapter)brush).Paint);
    }

    public override void SaveOpacityLayer(float opacity)
    {
        // SkiaSharp SaveLayer uses only the alpha channel of the paint's
        // color to modulate the layer during compositing; RGB values are
        // irrelevant when no shader/color-filter is applied.
        byte alpha = (byte)Math.Clamp((int)(opacity * 255), 0, 255);
        using var paint = new SKPaint { Color = new SKColor(0, 0, 0, alpha) };
        canvas.SaveLayer(paint);
    }

    public override void RestoreOpacityLayer()
    {
        canvas.Restore();
    }

    public override void SaveBlendLayer(string blendMode)
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
            _ => SKBlendMode.SrcOver, // "normal"
        };

        using var paint = new SKPaint { BlendMode = skBlendMode };
        canvas.SaveLayer(paint);
    }

    public override void RestoreBlendLayer()
    {
        canvas.Restore();
    }

    public override void Dispose()
    {
        if (dispose)
            canvas.Dispose();
    }
}
