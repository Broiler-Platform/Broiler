using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal interface IFontCompatFactory
{
    SKFont CreateFont(SKTypeface typeface, float size);

    FontCompatMetrics GetMetrics(SKFont font);
}

internal readonly record struct FontCompatMetrics(double Height, double UnderlineOffset);
