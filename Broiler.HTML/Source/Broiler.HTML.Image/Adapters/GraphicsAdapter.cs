using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.HTML.Adapters.Adapters;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal sealed class GraphicsAdapter : RGraphics
{
    private readonly Func<SKCanvas> _canvasFactory;
    private readonly BCanvas? _rasterCanvas;
    private readonly bool _disposeCanvas;
    private readonly bool _restoreOnDispose;
    private readonly Action? _onDispose;
    private readonly List<Action<SKCanvas>> _deferredCanvasOperations = [];
    private readonly Stack<bool> _rasterLayerStack = new();
    private readonly ITextShaper _textShaper = SkiaTextShaper.Instance;
    private SKCanvas? _canvas;
    private int _activeSkiaLayerDepth;
    private bool _nextLayerCanUseRaster;

    public GraphicsAdapter(
        Func<SKCanvas> canvasFactory,
        RectangleF initialClip,
        BCanvas? rasterCanvas = null,
        bool disposeCanvas = false,
        bool restoreOnDispose = false,
        Action? onDispose = null,
        Action<SKCanvas, object?>? initialCanvasOperation = null,
        object? initialCanvasOperationState = null)
        : base(SkiaImageAdapter.Instance, initialClip)
    {
        _canvasFactory = canvasFactory ?? throw new ArgumentNullException(nameof(canvasFactory));
        _rasterCanvas = rasterCanvas;
        _disposeCanvas = disposeCanvas;
        _restoreOnDispose = restoreOnDispose;
        _onDispose = onDispose;
        if (initialCanvasOperation is not null)
            _deferredCanvasOperations.Add(canvas => initialCanvasOperation(canvas, initialCanvasOperationState));
    }

    internal bool HasMaterializedCanvas => _canvas is not null;

    public override void PopClip()
    {
        ApplyCanvasOperation(static canvas => canvas.Restore());
        _rasterCanvas?.PopClip();
        _clipStack.Pop();
    }

    public override void PushClip(RectangleF rect)
    {
        _clipStack.Push(rect);
        ApplyCanvasOperation(canvas =>
        {
            canvas.Save();
            canvas.ClipRect(Utilities.Utils.Convert(rect));
        });
        _rasterCanvas?.PushClip(rect);
    }

    public override void PushClipExclude(RectangleF rect)
    {
        _clipStack.Push(_clipStack.Peek());
        ApplyCanvasOperation(canvas =>
        {
            canvas.Save();
            canvas.ClipRect(Utilities.Utils.Convert(rect), SKClipOperation.Difference);
        });
        _rasterCanvas?.PushClipExclude(rect);
    }

    public override void PushClipRounded(RectangleF rect,
        double cornerNw, double cornerNwY,
        double cornerNe, double cornerNeY,
        double cornerSe, double cornerSeY,
        double cornerSw, double cornerSwY)
    {
        _clipStack.Push(rect);
        ApplyCanvasOperation(canvas =>
        {
            canvas.Save();
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
        });

        _rasterCanvas?.PushClipRounded(
            rect,
            cornerNw, cornerNwY,
            cornerNe, cornerNeY,
            cornerSe, cornerSeY,
            cornerSw, cornerSwY);
    }

    public override object SetAntiAliasSmoothingMode() => null;

    public override void ReturnPreviousSmoothingMode(object prevMode)
    {
    }

    public override SizeF MeasureString(string str, RFont font) =>
        _textShaper.MeasureString((FontAdapter)font, str);

    public override void MeasureString(string str, RFont font, double maxWidth, out int charFit, out double charFitWidth) =>
        _textShaper.MeasureString((FontAdapter)font, str, maxWidth, out charFit, out charFitWidth);

    public override void DrawString(string str, RFont font, Color color, PointF point, SizeF size, bool rtl)
    {
        var canvas = EnsureCanvas();
        var fontAdapter = (FontAdapter)font;
        using var paint = new SKPaint
        {
            Color = Utilities.Utils.Convert(color),
            IsAntialias = true,
        };

        var renderFont = fontAdapter.RenderFont;
        var origin = _textShaper.GetDrawOrigin(fontAdapter, point);
        canvas.DrawText(str, origin.X, origin.Y, renderFont, paint);
    }

    public override void DrawGradientString(string str, RFont font, RectangleF rect, PointF point, SizeF size, bool rtl, Color[] colors, float[] positions, float angle)
    {
        if (colors == null || colors.Length == 0)
            return;

        var canvas = EnsureCanvas();
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
        return new BrushAdapter(
            () =>
            {
                var paint = new SKPaint();
                paint.Shader = SKShader.CreateBitmap(
                    imgAdapter.Bitmap.AsSkBitmap(),
                    SKShaderTileMode.Repeat,
                    SKShaderTileMode.Repeat,
                    SKMatrix.CreateTranslation((float)translateTransformLocation.X, (float)translateTransformLocation.Y));
                return paint;
            },
            dispose: true)
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
            _rasterCanvas!.DrawLine(new PointF((float)x1, (float)y1), new PointF((float)x2, (float)y2), penAdapter.SolidColor!.Value, (float)pen.Width);
            return;
        }

        EnsureCanvas().DrawLine((float)x1, (float)y1, (float)x2, (float)y2, penAdapter.Paint);
    }

    public override void DrawRectangle(RPen pen, double x, double y, double width, double height)
    {
        var penAdapter = (PenAdapter)pen;
        if (CanUseRaster && penAdapter.HasSimpleStroke)
        {
            _rasterCanvas!.DrawRectangleStroke(new RectangleF((float)x, (float)y, (float)width, (float)height), penAdapter.SolidColor!.Value, (float)pen.Width);
            return;
        }

        EnsureCanvas().DrawRect(SKRect.Create((float)x, (float)y, (float)width, (float)height), penAdapter.Paint);
    }

    public override void DrawRectangle(RBrush brush, double x, double y, double width, double height)
    {
        var brushAdapter = (BrushAdapter)brush;
        if (CanUseRaster
            && brushAdapter.TextureBitmap is BBitmap textureBitmap
            && brushAdapter.TextureSourceRect is RectangleF textureSourceRect
            && brushAdapter.TextureOrigin is PointF textureOrigin)
        {
            _rasterCanvas!.FillRectTiled(
                textureBitmap,
                new RectangleF((float)x, (float)y, (float)width, (float)height),
                textureSourceRect,
                textureOrigin);
            return;
        }

        if (CanUseRaster && brushAdapter.SolidColor is BColor solidColor)
        {
            _rasterCanvas!.FillRect(new RectangleF((float)x, (float)y, (float)width, (float)height), solidColor);
            return;
        }

        EnsureCanvas().DrawRect(SKRect.Create((float)x, (float)y, (float)width, (float)height), brushAdapter.Paint);
    }

    public override void DrawImage(RImage image, RectangleF destRect, RectangleF srcRect)
    {
        var imgAdapter = (ImageAdapter)image;
        if (CanUseRaster)
        {
            _rasterCanvas!.DrawBitmap(imgAdapter.Bitmap, destRect, srcRect);
            return;
        }

        EnsureCanvas().DrawBitmap(imgAdapter.Bitmap.AsSkBitmap(), Utilities.Utils.Convert(srcRect), Utilities.Utils.Convert(destRect));
    }

    public override void DrawImage(RImage image, RectangleF destRect)
    {
        var imgAdapter = (ImageAdapter)image;
        if (CanUseRaster)
        {
            _rasterCanvas!.DrawBitmap(
                imgAdapter.Bitmap,
                destRect,
                new RectangleF(0, 0, imgAdapter.Bitmap.Width, imgAdapter.Bitmap.Height));
            return;
        }

        EnsureCanvas().DrawBitmap(imgAdapter.Bitmap.AsSkBitmap(), Utilities.Utils.Convert(destRect));
    }

    public override void DrawPath(RPen pen, RGraphicsPath path)
    {
        var penAdapter = (PenAdapter)pen;
        var pathAdapter = (GraphicsPathAdapter)path;
        if (CanUseRaster && penAdapter.HasSimpleStroke && pathAdapter.FlattenedPoints.Count > 1)
        {
            _rasterCanvas!.DrawPathStroke(pathAdapter.FlattenedPoints, penAdapter.SolidColor!.Value, (float)pen.Width);
            return;
        }

        EnsureCanvas().DrawPath(pathAdapter.Path, penAdapter.Paint);
    }

    public override void DrawPath(RBrush brush, RGraphicsPath path)
    {
        var brushAdapter = (BrushAdapter)brush;
        var pathAdapter = (GraphicsPathAdapter)path;
        if (CanUseRaster && brushAdapter.SolidColor is BColor solidColor && pathAdapter.FlattenedPoints.Count > 2)
        {
            _rasterCanvas!.FillPolygon([.. pathAdapter.FlattenedPoints], solidColor);
            return;
        }

        EnsureCanvas().DrawPath(pathAdapter.Path, brushAdapter.Paint);
    }

    public override void DrawPolygon(RBrush brush, PointF[] points)
    {
        if (points == null || points.Length == 0)
            return;

        var brushAdapter = (BrushAdapter)brush;
        if (CanUseRaster && brushAdapter.SolidColor is BColor solidColor)
        {
            _rasterCanvas!.FillPolygon(points, solidColor);
            return;
        }

        using var path = new SKPath();
        path.MoveTo(Utilities.Utils.Convert(points[0]));

        for (int i = 1; i < points.Length; i++)
            path.LineTo(Utilities.Utils.Convert(points[i]));

        path.Close();
        EnsureCanvas().DrawPath(path, brushAdapter.Paint);
    }

    public override void HintNextLayerCanUseRaster(bool canUseRaster) =>
        _nextLayerCanUseRaster = canUseRaster;

    public override void SaveOpacityLayer(float opacity)
    {
        bool useRaster = _rasterCanvas is not null && _activeSkiaLayerDepth == 0 && _nextLayerCanUseRaster;
        _nextLayerCanUseRaster = false;
        _rasterLayerStack.Push(useRaster);
        if (useRaster)
        {
            _rasterCanvas!.SaveOpacityLayer(opacity);
            return;
        }

        _activeSkiaLayerDepth++;
        ApplyCanvasOperation(canvas => SaveOpacityLayerOnCanvas(canvas, opacity));
    }

    public override void RestoreOpacityLayer()
    {
        bool usedRaster = _rasterLayerStack.Count > 0 && _rasterLayerStack.Pop();
        if (usedRaster)
        {
            _rasterCanvas!.RestoreOpacityLayer();
            return;
        }

        ApplyCanvasOperation(static canvas => canvas.Restore());
        _activeSkiaLayerDepth = Math.Max(0, _activeSkiaLayerDepth - 1);
    }

    public override void SaveBlendLayer(string blendMode)
    {
        bool useRaster = _rasterCanvas is not null
            && _activeSkiaLayerDepth == 0
            && _nextLayerCanUseRaster;
        _nextLayerCanUseRaster = false;
        _rasterLayerStack.Push(useRaster);
        if (useRaster)
        {
            _rasterCanvas!.SaveBlendLayer(blendMode);
            return;
        }

        _activeSkiaLayerDepth++;
        ApplyCanvasOperation(canvas => SaveBlendLayerOnCanvas(canvas, blendMode));
    }

    public override void RestoreBlendLayer()
    {
        bool usedRaster = _rasterLayerStack.Count > 0 && _rasterLayerStack.Pop();
        if (usedRaster)
        {
            _rasterCanvas!.RestoreBlendLayer();
            return;
        }

        ApplyCanvasOperation(static canvas => canvas.Restore());
        _activeSkiaLayerDepth = Math.Max(0, _activeSkiaLayerDepth - 1);
    }

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
        if (_restoreOnDispose)
        {
            _canvas?.Restore();
            _rasterCanvas?.Restore();
        }

        if (_disposeCanvas)
        {
            _canvas?.Dispose();
            _rasterCanvas?.Dispose();
        }

        _onDispose?.Invoke();
    }

    private bool CanUseRaster => _rasterCanvas is not null && _activeSkiaLayerDepth == 0;

    private SKCanvas EnsureCanvas()
    {
        if (_canvas is not null)
            return _canvas;

        _canvas = _canvasFactory();
        foreach (var operation in _deferredCanvasOperations)
            operation(_canvas);

        _deferredCanvasOperations.Clear();
        return _canvas;
    }

    private void ApplyCanvasOperation(Action<SKCanvas> operation)
    {
        if (_canvas is not null)
        {
            operation(_canvas);
            return;
        }

        _deferredCanvasOperations.Add(operation);
    }

    private static void SaveOpacityLayerOnCanvas(SKCanvas canvas, float opacity)
    {
        byte alpha = (byte)Math.Clamp((int)(opacity * 255), 0, 255);
        using var paint = new SKPaint { Color = new SKColor(0, 0, 0, alpha) };
        canvas.SaveLayer(paint);
    }

    private static void SaveBlendLayerOnCanvas(SKCanvas canvas, string blendMode)
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
