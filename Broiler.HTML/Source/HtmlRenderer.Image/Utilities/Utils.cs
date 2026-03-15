using System.Drawing;
using SkiaSharp;

namespace TheArtOfDev.HtmlRenderer.Image.Utilities;

internal static class Utils
{
    public static PointF Convert(SKPoint p) => new(p.X, p.Y);
    public static SKPoint Convert(PointF p) => new((float)p.X, (float)p.Y);
    public static SKPoint[] Convert(PointF[] points)
    {
        var result = new SKPoint[points.Length];
        for (int i = 0; i < points.Length; i++)
            result[i] = Convert(points[i]);
        return result;
    }
    public static SizeF Convert(SKSize s) => new(s.Width, s.Height);
    public static SKSize Convert(SizeF s) => new((float)s.Width, (float)s.Height);
    public static RectangleF Convert(SKRect r) => new(r.Left, r.Top, r.Width, r.Height);
    public static SKRect Convert(RectangleF r) => SKRect.Create((float)r.X, (float)r.Y, (float)r.Width, (float)r.Height);
    public static Color Convert(SKColor c) => Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue);
    public static SKColor Convert(Color c) => new(c.R, c.G, c.B, c.A);
}
