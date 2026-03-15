using System.Windows.Media;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.WPF.Adapters;

internal sealed class BrushAdapter(Brush brush) : RBrush
{
    public Brush Brush { get; } = brush;

    public override void Dispose()
    { }
}