using DashStyle = System.Drawing.Drawing2D.DashStyle;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.Avalonia.Adapters;

internal sealed class PenAdapter(IBrush brush) : RPen
{
    private double _width;
    private IDashStyle _dashStyle;

    public override double Width
    {
        get { return _width; }
        set { _width = value; }
    }

    public override DashStyle DashStyle
    {
        set
        {
            _dashStyle = value switch
            {
                System.Drawing.Drawing2D.DashStyle.Dash => global::Avalonia.Media.DashStyle.Dash,
                System.Drawing.Drawing2D.DashStyle.Dot => global::Avalonia.Media.DashStyle.Dot,
                System.Drawing.Drawing2D.DashStyle.DashDot => global::Avalonia.Media.DashStyle.DashDot,
                System.Drawing.Drawing2D.DashStyle.DashDotDot => global::Avalonia.Media.DashStyle.DashDotDot,
                _ => null,
            };
        }
    }

    public IPen CreatePen()
    {
        var pen = new Pen(brush, _width);
        if (_dashStyle != null)
            pen.DashStyle = _dashStyle;
        return pen.ToImmutable();
    }
}
