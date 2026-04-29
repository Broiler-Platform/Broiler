using System;
using System.Drawing.Drawing2D;
using Broiler.HTML.Adapters.Adapters;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal sealed class PenAdapter : RPen
{
    private readonly Func<float, DashStyle, SKPaint>? _paintFactory;
    private readonly Action<SKPaint, float, DashStyle>? _paintUpdater;
    private SKPaint? _paint;
    private float _width;
    private DashStyle _dashStyle;

    public PenAdapter(Func<float, DashStyle, SKPaint> paintFactory, Action<SKPaint, float, DashStyle> paintUpdater)
    {
        _paintFactory = paintFactory ?? throw new ArgumentNullException(nameof(paintFactory));
        _paintUpdater = paintUpdater ?? throw new ArgumentNullException(nameof(paintUpdater));
        _width = 1f;
        _dashStyle = DashStyle.Solid;
    }

    public SKPaint Paint => _paint ??= _paintFactory?.Invoke(_width, _dashStyle)
        ?? throw new InvalidOperationException("Pen paint factory was not configured.");

    public BColor? SolidColor { get; init; }

    internal bool HasMaterializedPaint => _paint is not null;

    public bool HasSimpleStroke => SolidColor.HasValue && _dashStyle == DashStyle.Solid;

    public override double Width
    {
        get => _width;
        set
        {
            _width = (float)value;
            if (_paint is not null)
                _paintUpdater?.Invoke(_paint, _width, _dashStyle);
        }
    }

    public override DashStyle DashStyle
    {
        set
        {
            _dashStyle = value;
            if (_paint is not null)
                _paintUpdater?.Invoke(_paint, _width, _dashStyle);
        }
    }
}
