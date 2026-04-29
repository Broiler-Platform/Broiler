using Broiler.HTML.Adapters.Adapters;
using SkiaSharp;
using System.Drawing;
using System;

namespace Broiler.HTML.Image.Adapters;

internal sealed class BrushAdapter : RBrush
{
    private readonly Func<SKPaint>? _paintFactory;
    private readonly bool _dispose;
    private SKPaint? _paint;

    public BrushAdapter(SKPaint paint, bool dispose)
    {
        _paint = paint ?? throw new ArgumentNullException(nameof(paint));
        _dispose = dispose;
    }

    public BrushAdapter(Func<SKPaint> paintFactory, bool dispose)
    {
        _paintFactory = paintFactory ?? throw new ArgumentNullException(nameof(paintFactory));
        _dispose = dispose;
    }

    public SKPaint Paint => _paint ??= _paintFactory?.Invoke()
        ?? throw new InvalidOperationException("Brush paint factory was not configured.");

    public BColor? SolidColor { get; init; }

    public BBitmap? TextureBitmap { get; init; }

    public RectangleF? TextureSourceRect { get; init; }

    public PointF? TextureOrigin { get; init; }

    internal bool HasMaterializedPaint => _paint is not null;

    public override void Dispose()
    {
        if (_dispose)
            _paint?.Dispose();
    }
}
