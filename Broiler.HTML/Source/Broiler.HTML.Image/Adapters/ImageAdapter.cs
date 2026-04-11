using Broiler.HTML.Adapters.Adapters;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal sealed class ImageAdapter(SKBitmap bitmap, bool hasIntrinsicRatio = true, bool hasIntrinsicWidth = true, bool hasIntrinsicHeight = true) : RImage
{
    public SKBitmap Bitmap { get; } = bitmap;

    public override double Width => Bitmap.Width;
    public override double Height => Bitmap.Height;
    public override bool HasIntrinsicRatio { get; } = hasIntrinsicRatio;
    public override bool HasIntrinsicWidth { get; } = hasIntrinsicWidth;
    public override bool HasIntrinsicHeight { get; } = hasIntrinsicHeight;

    public override void Dispose() => Bitmap.Dispose();
}
