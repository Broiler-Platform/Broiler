using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.Image.Adapters;

internal sealed class ImageAdapter(SKBitmap bitmap) : RImage
{
    public SKBitmap Bitmap { get; } = bitmap;

    public override double Width => Bitmap.Width;
    public override double Height => Bitmap.Height;

    public override void Dispose() => Bitmap.Dispose();
}
