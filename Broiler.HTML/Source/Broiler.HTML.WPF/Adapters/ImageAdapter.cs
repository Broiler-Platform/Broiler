using System.Windows.Media.Imaging;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.WPF.Adapters;

internal sealed class ImageAdapter(BitmapImage image) : RImage
{
    public BitmapImage Image { get; } = image;

    public override double Width => Image.PixelWidth;

    public override double Height => Image.PixelHeight;

    public override void Dispose()
    {
        Image.StreamSource?.Dispose();
    }
}