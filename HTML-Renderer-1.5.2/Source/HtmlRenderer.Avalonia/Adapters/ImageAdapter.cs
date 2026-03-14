using Avalonia.Media.Imaging;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.Avalonia.Adapters;

internal sealed class ImageAdapter(Bitmap image) : RImage
{
    public Bitmap Image { get; } = image;

    public override double Width => Image.Size.Width;

    public override double Height => Image.Size.Height;

    public override void Dispose()
    {
        Image.Dispose();
    }
}
