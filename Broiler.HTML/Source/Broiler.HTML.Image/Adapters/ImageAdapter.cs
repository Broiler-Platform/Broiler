using Broiler.HTML.Adapters.Adapters;
using SkiaSharp;
using System.Drawing;

namespace Broiler.HTML.Image.Adapters;

internal sealed class ImageAdapter(
    SKBitmap bitmap,
    bool hasIntrinsicRatio = true,
    bool hasIntrinsicWidth = true,
    bool hasIntrinsicHeight = true,
    double? intrinsicAspectRatio = null) : RImage
{
    public SKBitmap Bitmap { get; } = bitmap;

    public override double Width => Bitmap.Width;
    public override double Height => Bitmap.Height;
    public override bool HasIntrinsicRatio { get; } = hasIntrinsicRatio;
    public override bool HasIntrinsicWidth { get; } = hasIntrinsicWidth;
    public override bool HasIntrinsicHeight { get; } = hasIntrinsicHeight;
    public override double IntrinsicAspectRatio { get; } =
        intrinsicAspectRatio.HasValue && intrinsicAspectRatio.Value > 0
            ? intrinsicAspectRatio.Value
            : (bitmap.Height > 0 ? (double)bitmap.Width / bitmap.Height : 0);

    public override bool TryGetUniformColor(out Color color)
    {
        if (Bitmap.Width <= 0 || Bitmap.Height <= 0)
        {
            color = Color.Empty;
            return false;
        }

        var first = Bitmap.GetPixel(0, 0);
        for (int y = 0; y < Bitmap.Height; y++)
        {
            for (int x = 0; x < Bitmap.Width; x++)
            {
                if (Bitmap.GetPixel(x, y) != first)
                {
                    color = Color.Empty;
                    return false;
                }
            }
        }

        color = Color.FromArgb(first.Alpha, first.Red, first.Green, first.Blue);
        return true;
    }

    public override void Dispose() => Bitmap.Dispose();
}
