using System.Drawing;
using System.Drawing.Drawing2D;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal interface IPaintCompatFactory
{
    SKPaint CreateSolidBrushPaint(Color color);

    SKPaint CreateLinearGradientBrushPaint(RectangleF rect, Color color1, Color color2, double angle);

    SKPaint CreatePenPaint(Color color, float strokeWidth, DashStyle dashStyle);

    void UpdatePenPaint(SKPaint paint, float strokeWidth, DashStyle dashStyle);
}
