using System.Drawing;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal interface ICanvasCompat
{
    void DrawLine(SKCanvas canvas, float x1, float y1, float x2, float y2, SKPaint paint);

    void DrawRectangle(SKCanvas canvas, RectangleF rect, SKPaint paint);

    void DrawPath(SKCanvas canvas, GraphicsPathAdapter path, SKPaint paint);

    void ClipRounded(
        SKCanvas canvas,
        RectangleF rect,
        double cornerNw,
        double cornerNwY,
        double cornerNe,
        double cornerNeY,
        double cornerSe,
        double cornerSeY,
        double cornerSw,
        double cornerSwY);

    SKPaint CreateTexturePaint(BBitmap bitmap, PointF translateTransformLocation);

    void DrawPolygon(SKCanvas canvas, PointF[] points, SKPaint paint);

    void SaveOpacityLayer(SKCanvas canvas, float opacity);

    void SaveBlendLayer(SKCanvas canvas, string blendMode);
}
