using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.HTML.Adapters.Adapters;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal sealed class GraphicsAdapter(
    SKCanvas canvas,
    RectangleF initialClip,
    BCanvas? rasterCanvas = null,
    bool dispose = false,
    bool restoreOnDispose = false,
    Action? onDispose = null) : RGraphics(SkiaImageAdapter.Instance, initialClip)
{
    private readonly Stack<bool> _rasterLayerStack = new();
    private readonly ITextShaper _textShaper = SkiaTextShaper.Instance;
    private int _activeSkiaLayerDepth;
    private bool _nextLayerCanUseRaster;

    public override void PopClip()
    {
        canvas.Restore();
        rasterCanvas?.PopClip();
        _clipStack.Pop();
    }

    public override void PushClip(RectangleF rect)
    {
        _clipStack.Push(rect);
        canvas.Save();
        canvas.ClipRect(Utilities.Utils.Convert(rect));
        rasterCanvas?.PushClip(rect);
    }

    public override void PushClipExclude(RectangleF rect)
    {
        _clipStack.Push(_clipStack.Peek());
        canvas.Save();
        canvas.ClipRect(Utilities.Utils.Convert(rect), SKClipOperation.Difference);
        rasterCanvas?.PushClipExclude(rect);
    }

    public override void PushClipRounded(RectangleF rect,
        double cornerNw, double cornerNwY,
        double cornerNe, double cornerNeY,
        double cornerSe, double cornerSeY,
        double cornerSw, double cornerSwY)
    {
        _clipStack.Push(rect);
        canvas.Save();
        if ((cornerNw <= 0 && cornerNwY <= 0)
            && (cornerNe <= 0 && cornerNeY <= 0)
            && (cornerSe <= 0 && cornerSeY <= 0)
            && (cornerSw <= 0 && cornerSwY <= 0))
        {
            canvas.ClipRect(Utilities.Utils.Convert(rect));
        }
        else
        {
            var skRect = Utilities.Utils.Convert(rect);
            // SKRoundRect radii: [topLeft.x, topLeft.y, topRight.x, topRight.y,
            //                     bottomRight.x, bottomRight.y, bottomLeft.x, bottomLeft.y]
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

        rasterCanvas?.PushClipRounded(
            rect,
            cornerNw, cornerNwY,
            cornerNe, cornerNeY,
            cornerSe, cornerSeY,
            cornerSw, cornerSwY);
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
        return _textShaper.MeasureString((FontAdapter)font, str);
    }

    public override void MeasureString(string str, RFont font, double maxWidth, out int charFit, out double charFitWidth)
        => _textShaper.MeasureString((FontAdapter)font, str, maxWidth, out charFit, out charFitWidth);

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
        var origin = _textShaper.GetDrawOrigin(fontAdapter, point);

        canvas.DrawText(str, origin.X, origin.Y, renderFont, paint);
    }

    public override void DrawGradientString(string str, RFont font, RectangleF rect, PointF point, SizeF size, bool rtl, Color[] colors, float[] positions, float angle)
    {
        if (colors == null || colors.Length == 0)
            return;

        var fontAdapter = (FontAdapter)font;
        var renderFont = fontAdapter.RenderFont;
        var origin = _textShaper.GetDrawOrigin(fontAdapter, point);
        float shaderWidth = Math.Max(rect.Width, _textShaper.MeasureRenderedText(fontAdapter, str));
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
            canvas.DrawText(str, origin.X, origin.Y, renderFont, maskPaint);
        }

        using var shader = SKShader.CreateLinearGradient(startPoint, endPoint, skColors, positions, SKShaderTileMode.Clamp);
        using var gradientPaint = new SKPaint
        {
            Shader = shader,
            BlendMode = SKBlendMode.SrcIn,
            IsAntialias = false,
        };

        if (string.Equals(fontAdapter.Typeface.FamilyName, "Ahem", StringComparison.OrdinalIgnoreCase)
            && !str.Contains(' '))
        {
            gradientPaint.BlendMode = SKBlendMode.SrcOver;
            canvas.DrawRect(Utilities.Utils.Convert(shaderRect), gradientPaint);
            canvas.Restore();
            return;
        }

        canvas.DrawRect(Utilities.Utils.Convert(shaderRect), gradientPaint);
        canvas.Restore();
    }

    public override RBrush GetTextureBrush(RImage image, RectangleF dstRect, PointF translateTransformLocation)
    {
        var imgAdapter = (ImageAdapter)image;
        var paint = new SKPaint();
        var shader = SKShader.CreateBitmap(
            imgAdapter.Bitmap.AsSkBitmap(),
            SKShaderTileMode.Repeat,
            SKShaderTileMode.Repeat,
            SKMatrix.CreateTranslation((float)translateTransformLocation.X, (float)translateTransformLocation.Y));
        paint.Shader = shader;
        return new BrushAdapter(paint, true)
        {
            TextureBitmap = imgAdapter.Bitmap,
            TextureSourceRect = dstRect,
            TextureOrigin = translateTransformLocation,
        };
    }

    public override RGraphicsPath GetGraphicsPath() => new GraphicsPathAdapter();

    public override void DrawLine(RPen pen, double x1, double y1, double x2, double y2)
    {
        var penAdapter = (PenAdapter)pen;
        if (CanUseRaster && penAdapter.HasSimpleStroke)
        {
            rasterCanvas!.DrawLine(new PointF((float)x1, (float)y1), new PointF((float)x2, (float)y2), penAdapter.SolidColor!.Value, (float)pen.Width);
            return;
        }

        canvas.DrawLine((float)x1, (float)y1, (float)x2, (float)y2, penAdapter.Paint);
    }

    public override void DrawRectangle(RPen pen, double x, double y, double width, double height)
    {
        var penAdapter = (PenAdapter)pen;
        if (CanUseRaster && penAdapter.HasSimpleStroke)
        {
            rasterCanvas!.DrawRectangleStroke(new RectangleF((float)x, (float)y, (float)width, (float)height), penAdapter.SolidColor!.Value, (float)pen.Width);
            return;
        }

        canvas.DrawRect(SKRect.Create((float)x, (float)y, (float)width, (float)height), penAdapter.Paint);
    }

    public override void DrawRectangle(RBrush brush, double x, double y, double width, double height)
    {
        var brushAdapter = (BrushAdapter)brush;
        if (CanUseRaster
            && brushAdapter.TextureBitmap is BBitmap textureBitmap
            && brushAdapter.TextureSourceRect is RectangleF textureSourceRect
            && brushAdapter.TextureOrigin is PointF textureOrigin)
        {
            rasterCanvas!.FillRectTiled(
                textureBitmap,
                new RectangleF((float)x, (float)y, (float)width, (float)height),
                textureSourceRect,
                textureOrigin);
            return;
        }

        if (CanUseRaster && brushAdapter.SolidColor is BColor solidColor)
        {
            rasterCanvas!.FillRect(new RectangleF((float)x, (float)y, (float)width, (float)height), solidColor);
            return;
        }

        canvas.DrawRect(SKRect.Create((float)x, (float)y, (float)width, (float)height), brushAdapter.Paint);
    }

    public override void DrawImage(RImage image, RectangleF destRect, RectangleF srcRect)
    {
        var imgAdapter = (ImageAdapter)image;
        if (CanUseRaster)
        {
            rasterCanvas!.DrawBitmap(imgAdapter.Bitmap, destRect, srcRect);
            return;
        }

        canvas.DrawBitmap(imgAdapter.Bitmap.AsSkBitmap(), Utilities.Utils.Convert(srcRect), Utilities.Utils.Convert(destRect));
    }

    public override void DrawImage(RImage image, RectangleF destRect)
    {
        var imgAdapter = (ImageAdapter)image;
        if (CanUseRaster)
        {
            rasterCanvas!.DrawBitmap(
                imgAdapter.Bitmap,
                destRect,
                new RectangleF(0, 0, imgAdapter.Bitmap.Width, imgAdapter.Bitmap.Height));
            return;
        }

        canvas.DrawBitmap(imgAdapter.Bitmap.AsSkBitmap(), Utilities.Utils.Convert(destRect));
    }

    public override void DrawPath(RPen pen, RGraphicsPath path)
    {
        var penAdapter = (PenAdapter)pen;
        var pathAdapter = (GraphicsPathAdapter)path;
        if (CanUseRaster && penAdapter.HasSimpleStroke && pathAdapter.FlattenedPoints.Count > 1)
        {
            rasterCanvas!.DrawPathStroke(pathAdapter.FlattenedPoints, penAdapter.SolidColor!.Value, (float)pen.Width);
            return;
        }

        canvas.DrawPath(pathAdapter.Path, penAdapter.Paint);
    }

    public override void DrawPath(RBrush brush, RGraphicsPath path)
    {
        var brushAdapter = (BrushAdapter)brush;
        var pathAdapter = (GraphicsPathAdapter)path;
        if (CanUseRaster && brushAdapter.SolidColor is BColor solidColor && pathAdapter.FlattenedPoints.Count > 2)
        {
            rasterCanvas!.FillPolygon([.. pathAdapter.FlattenedPoints], solidColor);
            return;
        }

        canvas.DrawPath(pathAdapter.Path, brushAdapter.Paint);
    }

    public override void DrawPolygon(RBrush brush, PointF[] points)
    {
        if (points == null || points.Length == 0)
            return;

        var brushAdapter = (BrushAdapter)brush;
        if (CanUseRaster && brushAdapter.SolidColor is BColor solidColor)
        {
            rasterCanvas!.FillPolygon(points, solidColor);
            return;
        }

        using var path = new SKPath();
        path.MoveTo(Utilities.Utils.Convert(points[0]));

        for (int i = 1; i < points.Length; i++)
            path.LineTo(Utilities.Utils.Convert(points[i]));

        path.Close();
        canvas.DrawPath(path, brushAdapter.Paint);
    }

    public override void HintNextLayerCanUseRaster(bool canUseRaster) =>
        _nextLayerCanUseRaster = canUseRaster;

    public override void SaveOpacityLayer(float opacity)
    {
        bool useRaster = rasterCanvas is not null && _activeSkiaLayerDepth == 0 && _nextLayerCanUseRaster;
        _nextLayerCanUseRaster = false;
        _rasterLayerStack.Push(useRaster);
        if (useRaster)
        {
            rasterCanvas!.SaveOpacityLayer(opacity);
            return;
        }

        _activeSkiaLayerDepth++;
        // SkiaSharp SaveLayer uses only the alpha channel of the paint's
        // color to modulate the layer during compositing; RGB values are
        // irrelevant when no shader/color-filter is applied.
        byte alpha = (byte)Math.Clamp((int)(opacity * 255), 0, 255);
        using var paint = new SKPaint { Color = new SKColor(0, 0, 0, alpha) };
        canvas.SaveLayer(paint);
    }

    public override void RestoreOpacityLayer()
    {
        bool usedRaster = _rasterLayerStack.Count > 0 && _rasterLayerStack.Pop();
        if (usedRaster)
        {
            rasterCanvas!.RestoreOpacityLayer();
            return;
        }

        canvas.Restore();
        _activeSkiaLayerDepth = Math.Max(0, _activeSkiaLayerDepth - 1);
    }

    public override void SaveBlendLayer(string blendMode)
    {
        bool useRaster = rasterCanvas is not null
            && _activeSkiaLayerDepth == 0
            && _nextLayerCanUseRaster;
        _nextLayerCanUseRaster = false;
        _rasterLayerStack.Push(useRaster);
        if (useRaster)
        {
            rasterCanvas!.SaveBlendLayer(blendMode);
            return;
        }

        _activeSkiaLayerDepth++;
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
        bool usedRaster = _rasterLayerStack.Count > 0 && _rasterLayerStack.Pop();
        if (usedRaster)
        {
            rasterCanvas!.RestoreBlendLayer();
            return;
        }

        canvas.Restore();
        _activeSkiaLayerDepth = Math.Max(0, _activeSkiaLayerDepth - 1);
    }

    /// <summary>
    /// Creates an off-screen gradient bitmap tile for tiled gradient rendering.
    /// </summary>
    public override RImage? CreateLinearGradientTile(int width, int height, Color[] colors, float[] positions, float angle)
    {
        if (width <= 0 || height <= 0 || colors == null || colors.Length == 0)
            return null;

        var bitmap = new BBitmap(width, height);
        using var tileCanvas = bitmap.OpenRasterCanvas();
        var gradientColors = new BColor[colors.Length];
        for (int i = 0; i < colors.Length; i++)
            gradientColors[i] = new BColor(colors[i].R, colors[i].G, colors[i].B, colors[i].A);

        tileCanvas.FillLinearGradientRect(new RectangleF(0, 0, width, height), gradientColors, positions, angle);

        return new ImageAdapter(bitmap);
    }

    public override void Dispose()
    {
        if (restoreOnDispose)
        {
            canvas.Restore();
            rasterCanvas?.Restore();
        }

        if (dispose)
        {
            canvas.Dispose();
            rasterCanvas?.Dispose();
        }

        onDispose?.Invoke();
    }

    // Display-list replay can hint when a compositing group contains only raster-capable
    // non-text items. Those groups can stay on BCanvas end-to-end; any unhinted layer
    // continues through Skia so mixed text/complex-brush groups keep their existing behavior.
    private bool CanUseRaster => rasterCanvas is not null && _activeSkiaLayerDepth == 0;
}
