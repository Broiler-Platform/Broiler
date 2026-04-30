using System.Drawing;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal interface ITextCanvasCompat
{
    void DrawString(SKCanvas canvas, FontAdapter font, SKFont renderFont, string text, Color color, PointF point);

    void DrawGradientString(
        SKCanvas canvas,
        FontAdapter font,
        SKFont renderFont,
        string text,
        RectangleF rect,
        PointF point,
        SizeF size,
        Color[] colors,
        float[] positions,
        float angle);
}
