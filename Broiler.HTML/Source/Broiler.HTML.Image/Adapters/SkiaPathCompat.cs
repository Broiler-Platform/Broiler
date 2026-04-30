using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal sealed class SkiaPathCompat : IPathCompat
{
    public static IPathCompat Instance { get; } = new SkiaPathCompat();

    public SKPath CreatePath() => new();

    public void Reset(SKPath path) => path.Reset();

    public void MoveTo(SKPath path, float x, float y) => path.MoveTo(x, y);

    public void LineTo(SKPath path, float x, float y) => path.LineTo(x, y);

    public void ArcTo(SKPath path, float left, float top, float width, float height, float startAngle, float sweepAngle, bool forceMoveTo) =>
        path.ArcTo(SKRect.Create(left, top, width, height), startAngle, sweepAngle, forceMoveTo);
}
