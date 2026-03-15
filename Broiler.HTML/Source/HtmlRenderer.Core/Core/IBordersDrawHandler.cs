using System.Drawing;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Core.Dom;

namespace TheArtOfDev.HtmlRenderer.Core;

/// <summary>
/// Read-only view of the border-related CSS properties that drawing handlers require.
/// Implemented by <c>CssBoxProperties</c> to decouple handlers from the DOM tree.
/// </summary>
/// <remarks>
/// See ADR-007, section "Circular Dependencies Remaining (Future Work)", item 4.
/// </remarks>
internal interface IBorderRenderData
{
    string BorderTopStyle { get; }
    string BorderRightStyle { get; }
    string BorderBottomStyle { get; }
    string BorderLeftStyle { get; }

    double ActualBorderTopWidth { get; }
    double ActualBorderRightWidth { get; }
    double ActualBorderBottomWidth { get; }
    double ActualBorderLeftWidth { get; }

    Color ActualBorderTopColor { get; }
    Color ActualBorderRightColor { get; }
    Color ActualBorderBottomColor { get; }
    Color ActualBorderLeftColor { get; }

    double ActualCornerNw { get; }
    double ActualCornerNe { get; }
    double ActualCornerSe { get; }
    double ActualCornerSw { get; }

    bool IsRounded { get; }

    /// <summary>
    /// Whether geometry anti-aliasing should be avoided for this box's rendering.
    /// </summary>
    bool AvoidGeometryAntialias { get; }
}

/// <summary>
/// Interface for border drawing handlers used by CssBox.
/// Breaks the direct static dependency between <c>CssBox</c> and the concrete
/// <c>BordersDrawHandler</c> class.
/// </summary>
internal interface IBordersDrawHandler
{
    /// <summary>
    /// Draws all visible borders for a box within the given rectangle.
    /// </summary>
    void DrawBoxBorders(RGraphics g, IBorderRenderData box, RectangleF rect, bool isFirst, bool isLast);

    /// <summary>
    /// Draws a single border side using the specified brush.
    /// </summary>
    void DrawBorder(Border border, RGraphics g, IBorderRenderData box, RBrush brush, RectangleF rectangle);
}
