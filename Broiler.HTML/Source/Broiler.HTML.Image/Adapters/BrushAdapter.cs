using Broiler.HTML.Adapters.Adapters;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal sealed class BrushAdapter(SKPaint paint, bool dispose) : RBrush
{
    public SKPaint Paint { get; } = paint;

    public BColor? SolidColor { get; init; }

    public override void Dispose()
    {
        if (dispose)
            Paint.Dispose();
    }
}
