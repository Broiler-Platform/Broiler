using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal interface IPathCompat
{
    SKPath CreatePath();
    void Reset(SKPath path);
    void MoveTo(SKPath path, float x, float y);
    void LineTo(SKPath path, float x, float y);
    void ArcTo(SKPath path, float left, float top, float width, float height, float startAngle, float sweepAngle, bool forceMoveTo);
}
