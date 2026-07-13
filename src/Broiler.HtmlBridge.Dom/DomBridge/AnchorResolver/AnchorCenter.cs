using System.Globalization;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    /// <summary>
    /// Resolves <c>align-self: anchor-center</c> and
    /// <c>justify-self: anchor-center</c> on elements that have
    /// <c>position-anchor</c> but no <c>position-area</c>.
    /// Centers the element on the anchor in the appropriate axis.
    /// </summary>
    private void ResolveAnchorCenter(Broiler.Dom.DomElement element, Dictionary<string, AnchorInfo> anchorRegistry)
    {
        if (!IsText(element))
        {
            var cssProps = GetComputedProps(element);

            string? positionAnchor = cssProps.GetValueOrDefault("position-anchor");
            string? positionArea = cssProps.GetValueOrDefault("position-area");
            string? alignSelf = cssProps.GetValueOrDefault("align-self");
            string? justifySelf = cssProps.GetValueOrDefault("justify-self");

            bool hasAnchorCenter =
                (alignSelf != null && alignSelf.Equals("anchor-center", StringComparison.OrdinalIgnoreCase)) ||
                (justifySelf != null && justifySelf.Equals("anchor-center", StringComparison.OrdinalIgnoreCase));

            if (hasAnchorCenter &&
                !string.IsNullOrWhiteSpace(positionAnchor) &&
                (string.IsNullOrWhiteSpace(positionArea) || positionArea == "none") &&
                ResolveAnchorForElement(positionAnchor, element, anchorRegistry) is { } anchor)
            {
                double elWidth = TryParsePx(cssProps.GetValueOrDefault("width")) ??
                                 TryParsePx(InlineStyle(element).GetValueOrDefault("width")) ?? 0;
                double elHeight = TryParsePx(cssProps.GetValueOrDefault("height")) ??
                                  TryParsePx(InlineStyle(element).GetValueOrDefault("height")) ?? 0;

                // align-self: anchor-center → center vertically on anchor
                if (alignSelf != null &&
                    alignSelf.Equals("anchor-center", StringComparison.OrdinalIgnoreCase))
                {
                    double anchorCenterY = anchor.Top + anchor.Height / 2.0;
                    double top = anchorCenterY - elHeight / 2.0;
                    InlineStyle(element)["top"] = $"{top.ToString(CultureInfo.InvariantCulture)}px";
                }

                // justify-self: anchor-center → center horizontally on anchor
                if (justifySelf != null &&
                    justifySelf.Equals("anchor-center", StringComparison.OrdinalIgnoreCase))
                {
                    double anchorCenterX = anchor.Left + anchor.Width / 2.0;
                    double left = anchorCenterX - elWidth / 2.0;
                    InlineStyle(element)["left"] = $"{left.ToString(CultureInfo.InvariantCulture)}px";
                }

                // Ensure the element has position:absolute.
                if (!cssProps.TryGetValue("position", out var pos) ||
                    pos == "static")
                {
                    InlineStyle(element)["position"] = "absolute";
                }
            }
        }

        // Snapshot children before recursing: element.Children enumerates the
        // live ChildNodes list, so any concurrent mutation of it (e.g. an
        // anchor-driven DOM move on another node sharing this parent) throws
        // "Collection was modified" mid-traversal (or overflows the ToList() copy).
        // SnapshotChildren tolerates both, the same defensive idiom used across
        // InlineContainingBlocks and the deferred moves in ResolveAnchorPositions.
        foreach (var child in SnapshotChildren(element))
            ResolveAnchorCenter(child, anchorRegistry);
    }
    /// <summary>
    /// Computes the offset within a position-area cell based on alignment.
    /// For "start" cells (top/left), the element is aligned to the end
    /// (nearest to the anchor). For "end" cells (bottom/right), aligned
    /// to the start. For "center" cells, centered.
    /// </summary>
    private static double ComputeAlignmentOffset(AxisSelection sel, double cellSize, double elementSize, bool isInlineAxis)
    {
        double slack = cellSize - elementSize;
        if (slack <= 0) return 0;

        return sel switch
        {
            AxisSelection.Start => slack,// "top" or "left" cell: align towards the anchor (end of cell).
            AxisSelection.End => 0,// "bottom" or "right" cell: align towards the anchor (start of cell).
            AxisSelection.Center => slack / 2,// "center" cell: center the element.
            AxisSelection.SpanStart or AxisSelection.SpanEnd or AxisSelection.SpanAll => 0,// Spanning cells: align to start by default.
            _ => 0,
        };
    }
}
