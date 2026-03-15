using DashStyle = System.Drawing.Drawing2D.DashStyle;
using System.Windows.Media;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.WPF.Adapters;

internal sealed class PenAdapter(Brush brush) : RPen
{
    private double _width;
    private System.Windows.Media.DashStyle _dashStyle = DashStyles.Solid;

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
                DashStyle.Solid => DashStyles.Solid,
                DashStyle.Dash => DashStyles.Dash,
                DashStyle.Dot => DashStyles.Dot,
                DashStyle.DashDot => DashStyles.DashDot,
                DashStyle.DashDotDot => DashStyles.DashDotDot,
                _ => DashStyles.Solid,
            };
        }
    }

    public Pen CreatePen()
    {
        var pen = new Pen(brush, _width) { DashStyle = _dashStyle };
        return pen;
    }
}