using System.Drawing.Drawing2D;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.Image.Adapters;

internal sealed class PenAdapter(SKPaint paint) : RPen
{
    public SKPaint Paint { get; } = paint;

    public override double Width
    {
        get => Paint.StrokeWidth;
        set => Paint.StrokeWidth = (float)value;
    }

    public override DashStyle DashStyle
    {
        set
        {
            Paint.PathEffect = value switch
            {
                DashStyle.Solid => null,
                DashStyle.Dash => Width < 2
                                        ? SKPathEffect.CreateDash([4f, 4f], 0)
                                        : SKPathEffect.CreateDash([4f * (float)Width, 2f * (float)Width], 0),
                DashStyle.Dot => SKPathEffect.CreateDash([(float)Width, (float)Width], 0),
                DashStyle.DashDot => SKPathEffect.CreateDash([4f * (float)Width, 2f * (float)Width, (float)Width, 2f * (float)Width], 0),
                DashStyle.DashDotDot => SKPathEffect.CreateDash([4f * (float)Width, 2f * (float)Width, (float)Width, 2f * (float)Width, (float)Width, 2f * (float)Width], 0),
                _ => null,
            };
        }
    }
}
