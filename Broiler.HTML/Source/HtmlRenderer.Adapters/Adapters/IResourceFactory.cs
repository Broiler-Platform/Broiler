using System.Drawing;

namespace TheArtOfDev.HtmlRenderer.Adapters;

/// <summary>
/// Factory interface for creating rendering resource objects (pens, brushes).
/// Breaks the circular dependency between <see cref="RGraphics"/> and the
/// concrete <c>RAdapter</c> class, allowing <see cref="RGraphics"/> to live
/// in the Adapters module.
/// </summary>
public interface IResourceFactory
{
    /// <summary>
    /// Gets a cached pen for the specified colour.
    /// </summary>
    RPen GetPen(Color color);

    /// <summary>
    /// Gets a cached solid brush for the specified colour.
    /// </summary>
    RBrush GetSolidBrush(Color color);

    /// <summary>
    /// Creates a linear gradient brush.
    /// </summary>
    RBrush GetLinearGradientBrush(RectangleF rect, Color color1, Color color2, double angle);
}
