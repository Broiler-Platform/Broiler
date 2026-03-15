using System;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Adapters;
using System.Drawing;

namespace TheArtOfDev.HtmlRenderer.Image.Adapters;

internal sealed class GraphicsPathAdapter : RGraphicsPath
{
    private PointF _lastPoint;

    public SKPath Path { get; } = new();

    public override void Start(double x, double y)
    {
        _lastPoint = new PointF((float)x, (float)y);
        Path.MoveTo((float)x, (float)y);
    }

    public override void LineTo(double x, double y)
    {
        Path.LineTo((float)x, (float)y);
        _lastPoint = new PointF((float)x, (float)y);
    }

    public override void ArcTo(double x, double y, double size, Corner corner)
    {
        float left = (float)(Math.Min(x, _lastPoint.X) - (corner == Corner.TopRight || corner == Corner.BottomRight ? size : 0));
        float top = (float)(Math.Min(y, _lastPoint.Y) - (corner == Corner.BottomLeft || corner == Corner.BottomRight ? size : 0));
        var rect = SKRect.Create(left, top, (float)size * 2, (float)size * 2);
        Path.ArcTo(rect, GetStartAngle(corner), 90, false);
        _lastPoint = new PointF((float)x, (float)y);
    }

    public override void Dispose() => Path.Dispose();

    private static float GetStartAngle(Corner corner)
    {
        return corner switch
        {
            Corner.TopLeft => 180,
            Corner.TopRight => 270,
            Corner.BottomLeft => 90,
            Corner.BottomRight => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(corner)),
        };
    }
}
