using Avalonia.Media;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.Avalonia.Adapters;

internal sealed class BrushAdapter(IBrush brush) : RBrush
{
    public IBrush Brush { get; } = brush;

    public override void Dispose()
    { }
}
