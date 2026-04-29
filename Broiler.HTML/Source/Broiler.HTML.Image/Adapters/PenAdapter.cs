using System;
using System.Drawing.Drawing2D;
using Broiler.HTML.Adapters.Adapters;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal sealed class PenAdapter : RPen
{
    private readonly Func<float, DashStyle, SKPaint>? _paintFactory;
    private SKPaint? _paint;
    private float _width;
    private DashStyle _dashStyle;

    public PenAdapter(SKPaint paint)
    {
        _paint = paint ?? throw new ArgumentNullException(nameof(paint));
        _width = paint.StrokeWidth;
        _dashStyle = paint.PathEffect is null ? DashStyle.Solid : DashStyle.Custom;
    }

    public PenAdapter(Func<float, DashStyle, SKPaint> paintFactory)
    {
        _paintFactory = paintFactory ?? throw new ArgumentNullException(nameof(paintFactory));
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
                _paint.StrokeWidth = _width;
        }
    }

    public override DashStyle DashStyle
    {
        set
        {
            _dashStyle = value;
            if (_paint is not null)
                _paint.PathEffect = CreatePathEffect(value, _width);
        }
    }

    private static SKPathEffect? CreatePathEffect(DashStyle value, float width) => value switch
    {
        DashStyle.Solid => null,
        DashStyle.Dash => width < 2f
            ? SKPathEffect.CreateDash([4f, 4f], 0)
            : SKPathEffect.CreateDash([4f * width, 2f * width], 0),
        DashStyle.Dot => SKPathEffect.CreateDash([width, width], 0),
        DashStyle.DashDot => SKPathEffect.CreateDash([4f * width, 2f * width, width, 2f * width], 0),
        DashStyle.DashDotDot => SKPathEffect.CreateDash([4f * width, 2f * width, width, 2f * width, width, 2f * width], 0),
        _ => null,
    };
}
