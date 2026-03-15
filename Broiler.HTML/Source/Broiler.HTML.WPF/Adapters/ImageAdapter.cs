using Broiler.HTML.Adapters.Adapters;
using System.Windows.Media.Imaging;

namespace Broiler.HTML.WPF.Adapters;

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