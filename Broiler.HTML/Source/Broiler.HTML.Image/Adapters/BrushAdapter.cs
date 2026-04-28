using Broiler.HTML.Adapters.Adapters;
using SkiaSharp;
using System.Drawing;

namespace Broiler.HTML.Image.Adapters;

internal sealed class BrushAdapter(SKPaint paint, bool dispose) : RBrush
{
    public SKPaint Paint { get; } = paint;

    public BColor? SolidColor { get; init; }

    public BBitmap? TextureBitmap { get; init; }

    public RectangleF? TextureSourceRect { get; init; }

    public PointF? TextureOrigin { get; init; }

    public override void Dispose()
    {
        if (dispose)
            Paint.Dispose();
    }
}
