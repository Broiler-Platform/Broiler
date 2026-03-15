using Broiler.HTML.Adapters.Adapters;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal sealed class ImageAdapter(SKBitmap bitmap) : RImage
{
    public SKBitmap Bitmap { get; } = bitmap;

    public override double Width => Bitmap.Width;
    public override double Height => Bitmap.Height;

    public override void Dispose() => Bitmap.Dispose();
}
