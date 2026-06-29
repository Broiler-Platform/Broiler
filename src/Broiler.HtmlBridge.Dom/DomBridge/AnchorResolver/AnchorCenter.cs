using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    /// <summary>
    /// Resolves <c>align-self: anchor-center</c> and
    /// <c>justify-self: anchor-center</c> on elements that have
    /// <c>position-anchor</c> but no <c>position-area</c>.
    /// Centers the element on the anchor in the appropriate axis.
    /// </summary>
    private void ResolveAnchorCenter(
        DomElement element,
        Dictionary<string, AnchorInfo> anchorRegistry)
    {
        if (!element.IsTextNode)
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
                anchorRegistry.TryGetValue(positionAnchor, out var anchor))
            {
                double elWidth = TryParsePx(cssProps.GetValueOrDefault("width")) ??
                                 TryParsePx(element.Style.GetValueOrDefault("width")) ?? 0;
                double elHeight = TryParsePx(cssProps.GetValueOrDefault("height")) ??
                                  TryParsePx(element.Style.GetValueOrDefault("height")) ?? 0;

                // align-self: anchor-center → center vertically on anchor
                if (alignSelf != null &&
                    alignSelf.Equals("anchor-center", StringComparison.OrdinalIgnoreCase))
                {
                    double anchorCenterY = anchor.Top + anchor.Height / 2.0;
                    double top = anchorCenterY - elHeight / 2.0;
                    element.Style["top"] = $"{top.ToString(CultureInfo.InvariantCulture)}px";
                }

                // justify-self: anchor-center → center horizontally on anchor
                if (justifySelf != null &&
                    justifySelf.Equals("anchor-center", StringComparison.OrdinalIgnoreCase))
                {
                    double anchorCenterX = anchor.Left + anchor.Width / 2.0;
                    double left = anchorCenterX - elWidth / 2.0;
                    element.Style["left"] = $"{left.ToString(CultureInfo.InvariantCulture)}px";
                }

                // Ensure the element has position:absolute.
                if (!cssProps.TryGetValue("position", out var pos) ||
                    pos == "static")
                {
                    element.Style["position"] = "absolute";
                }
            }
        }

        // Snapshot children before recursing: element.Children enumerates the
        // live ChildNodes list, so any concurrent mutation of it (e.g. an
        // anchor-driven DOM move on another node sharing this parent) throws
        // "Collection was modified" mid-traversal. Iterating a snapshot is the
        // same defensive idiom already used by the .ToList() walks in
        // InlineContainingBlocks and the deferred moves in ResolveAnchorPositions.
        foreach (var child in element.Children.ToList())
            ResolveAnchorCenter(child, anchorRegistry);
    }
    /// <summary>
    /// Computes the offset within a position-area cell based on alignment.
    /// For "start" cells (top/left), the element is aligned to the end
    /// (nearest to the anchor). For "end" cells (bottom/right), aligned
    /// to the start. For "center" cells, centered.
    /// </summary>
    private static double ComputeAlignmentOffset(
        AxisSelection sel, double cellSize, double elementSize, bool isInlineAxis)
    {
        double slack = cellSize - elementSize;
        if (slack <= 0) return 0;

        switch (sel)
        {
            case AxisSelection.Start:
                // "top" or "left" cell: align towards the anchor (end of cell).
                return slack;
            case AxisSelection.End:
                // "bottom" or "right" cell: align towards the anchor (start of cell).
                return 0;
            case AxisSelection.Center:
                // "center" cell: center the element.
                return slack / 2;
            case AxisSelection.SpanStart:
            case AxisSelection.SpanEnd:
            case AxisSelection.SpanAll:
                // Spanning cells: align to start by default.
                return 0;
            default:
                return 0;
        }
    }
}
