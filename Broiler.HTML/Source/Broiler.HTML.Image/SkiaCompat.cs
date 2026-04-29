using SkiaSharp;

namespace Broiler.HTML.Image;

internal static class SkiaCompat
{
    public static SKColor ToSkColor(this BColor color) => new(color.R, color.G, color.B, color.A);

    public static BColor ToBColor(this SKColor color) => new(color.Red, color.Green, color.Blue, color.Alpha);
}
