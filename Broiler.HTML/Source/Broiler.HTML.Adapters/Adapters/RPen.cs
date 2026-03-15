using System.Drawing.Drawing2D;

namespace TheArtOfDev.HtmlRenderer.Adapters;

public abstract class RPen
{
    public abstract double Width { get; set; }
    public abstract DashStyle DashStyle { set; }
}