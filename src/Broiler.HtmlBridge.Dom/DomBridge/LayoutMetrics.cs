using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private double GetClientWidthForDomElement(DomElement element, bool isRoot)
    {
        if (isRoot)
            return GetViewportReferenceLength(element, vertical: false);

        var props = GetComputedProps(element);
        var containingBlockWidth = ResolveContainingBlockReferenceLength(element, vertical: false);
        var width = ResolveContentBoxExtent(element, vertical: false);

        return width
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-left"), element, percentageBasis: containingBlockWidth)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-right"), element, percentageBasis: containingBlockWidth);
    }

    private double GetClientHeightForDomElement(DomElement element, bool isRoot)
    {
        if (isRoot)
            return GetViewportReferenceLength(element, vertical: true);

        var props = GetComputedProps(element);
        var containingBlockWidth = ResolveContainingBlockReferenceLength(element, vertical: false);
        var height = ResolveContentBoxExtent(element, vertical: true);

        return height
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-top"), element, percentageBasis: containingBlockWidth)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-bottom"), element, percentageBasis: containingBlockWidth);
    }

    private double GetClientTopForDomElement(DomElement element)
    {
        var props = GetComputedProps(element);
        return ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-top-width"), element);
    }

    private double GetClientLeftForDomElement(DomElement element)
    {
        var props = GetComputedProps(element);
        return ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-left-width"), element);
    }

    private double GetOffsetWidthForDomElement(DomElement element, bool isRoot)
    {
        if (isRoot)
            return GetViewportReferenceLength(element, vertical: false);

        if (ShouldReportZeroOffsetMetrics(element))
            return 0;

        var resolved = ResolvePositionAreaForElement(element);
        if (resolved != null)
            return resolved.Value.width;

        var props = GetComputedProps(element);
        var width = GetBorderBoxWidth(props, element);
        if (width > 0)
            return width;

        return ResolveBorderBoxExtent(element, vertical: false);
    }

    private double GetOffsetHeightForDomElement(DomElement element, bool isRoot)
    {
        if (isRoot)
            return GetViewportReferenceLength(element, vertical: true);

        if (ShouldReportZeroOffsetMetrics(element))
            return 0;

        var resolved = ResolvePositionAreaForElement(element);
        if (resolved != null)
            return resolved.Value.height;

        var props = GetComputedProps(element);
        var height = GetBorderBoxHeight(props, element);
        if (height > 0)
            return height;

        return ResolveBorderBoxExtent(element, vertical: true);
    }

    private static bool ShouldReportZeroOffsetMetrics(DomElement element) =>
        string.Equals(element.TagName, "map", StringComparison.OrdinalIgnoreCase);

    private double ResolveContentBoxExtent(DomElement element, bool vertical)
    {
        if (!_contentExtentInProgress.Add((element, vertical)))
            return 0;

        try
        {
            var props = GetComputedProps(element);
            var percentageBasis = ResolveContainingBlockReferenceLength(element, vertical);
            var specified = ParseCssLengthToPixelsWithViewport(
                props.GetValueOrDefault(vertical ? "height" : "width"),
                element,
                percentageBasis: percentageBasis);
            if (specified > 0)
                return specified;

            var svgLength = ResolveSvgGeometryLength(element, vertical ? "height" : "width", vertical, percentageBasis);
            if (svgLength > 0)
                return svgLength;

            var replacedElementLength = ResolveReplacedElementAttributeExtent(element, vertical);
            if (replacedElementLength > 0)
                return replacedElementLength;

            return EstimateAutoContentExtent(element, vertical, new HashSet<DomElement>());
        }
        finally
        {
            _contentExtentInProgress.Remove((element, vertical));
        }
    }

    private static double ResolveReplacedElementAttributeExtent(DomElement element, bool vertical)
    {
        if (!string.Equals(element.TagName, "img", StringComparison.OrdinalIgnoreCase))
            return 0;

        return ParsePositiveDouble(element.Attributes.GetValueOrDefault(vertical ? "height" : "width"));
    }

    private double ResolveBorderBoxExtent(DomElement element, bool vertical)
    {
        var props = GetComputedProps(element);
        var contentExtent = ResolveContentBoxExtent(element, vertical);
        var startPadding = ParseCssLengthToPixelsWithViewport(
            props.GetValueOrDefault(vertical ? "padding-top" : "padding-left"),
            element);
        var endPadding = ParseCssLengthToPixelsWithViewport(
            props.GetValueOrDefault(vertical ? "padding-bottom" : "padding-right"),
            element);
        var startBorder = ParseCssLengthToPixelsWithViewport(
            props.GetValueOrDefault(vertical ? "border-top-width" : "border-left-width"),
            element);
        var endBorder = ParseCssLengthToPixelsWithViewport(
            props.GetValueOrDefault(vertical ? "border-bottom-width" : "border-right-width"),
            element);
        return contentExtent + startPadding + endPadding + startBorder + endBorder;
    }

    private double EstimateAutoContentExtent(DomElement element, bool vertical, HashSet<DomElement> visited)
    {
        // Auto-size estimation recurses through descendants; guard against any
        // accidental cycles in synthesized DOM trees while deriving extents.
        if (!visited.Add(element))
            return 0;

        var extent = MeasureDirectTextExtent(element, vertical);
        var flowExtent = 0d;

        foreach (var child in element.Children)
        {
            if (child.IsTextNode || child.TagName.StartsWith("#", StringComparison.Ordinal))
                continue;

            if (!HasAssociatedLayoutBox(child))
                continue;

            var childProps = GetComputedProps(child);
            var childPosition = childProps.GetValueOrDefault("position");
            if (string.Equals(childPosition, "absolute", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(childPosition, "fixed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var childExtent = ResolveBorderBoxExtent(child, vertical);
            if (vertical)
            {
                flowExtent += ParseCssLengthToPixelsWithViewport(childProps.GetValueOrDefault("margin-top"), child);
                flowExtent += childExtent;
                flowExtent += ParseCssLengthToPixelsWithViewport(childProps.GetValueOrDefault("margin-bottom"), child);
                extent = Math.Max(extent, flowExtent);
            }
            else
            {
                var childInlineExtent =
                    ParseCssLengthToPixelsWithViewport(childProps.GetValueOrDefault("margin-left"), child) +
                    childExtent +
                    ParseCssLengthToPixelsWithViewport(childProps.GetValueOrDefault("margin-right"), child);
                extent = Math.Max(extent, childInlineExtent);
            }
        }

        visited.Remove(element);
        return extent;
    }

    private double MeasureDirectTextExtent(DomElement element, bool vertical)
    {
        var textFragments = new List<string>();
        if (!string.IsNullOrWhiteSpace(element.TextContent))
            textFragments.Add(element.TextContent);

        foreach (var child in element.Children)
        {
            if (child.IsTextNode && !string.IsNullOrWhiteSpace(child.TextContent))
                textFragments.Add(child.TextContent);
        }

        if (textFragments.Count == 0)
            return 0;

        var fontSize = ResolveFontSizeForElement(element);
        if (vertical)
            return fontSize;

        // Approximate inline text advance with an average glyph width of half the
        // current font size, which is enough for the bridge's coarse box metrics.
        var longestLine = textFragments
            .SelectMany(text => text.Replace("\r", string.Empty).Split('\n'))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .DefaultIfEmpty(string.Empty)
            .Max(line => line.Length);
        return longestLine * fontSize * 0.5;
    }

    private double GetOffsetTopForDomElement(DomElement element)
    {
        var resolved = ResolvePositionAreaForElement(element);
        if (resolved != null)
            return resolved.Value.top;

        var offsetParent = GetOffsetParentForDomElement(element);
        if (offsetParent != null)
            return ComputeOffsetRelativeToAncestor(element, offsetParent, vertical: true);

        var layoutRect = ComputeUnzoomedLayoutRect(element);
        return layoutRect.Top;
    }

    private double GetOffsetLeftForDomElement(DomElement element)
    {
        var resolved = ResolvePositionAreaForElement(element);
        if (resolved != null)
            return resolved.Value.left;

        var offsetParent = GetOffsetParentForDomElement(element);
        if (offsetParent != null)
            return ComputeOffsetRelativeToAncestor(element, offsetParent, vertical: false);

        var layoutRect = ComputeUnzoomedLayoutRect(element);
        return layoutRect.Left;
    }

    private DomElement? GetOffsetParentForDomElement(DomElement element)
    {
        if (element.Parent == null ||
            ReferenceEquals(element, DocumentElement) ||
            string.Equals(element.TagName, "html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(element.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var props = GetComputedProps(element);
        if (string.Equals(props.GetValueOrDefault("position"), "fixed", StringComparison.OrdinalIgnoreCase))
            return null;

        var documentElement = GetOwningDocumentElement(element);
        var fallbackBody = FindBodyElement(documentElement);
        for (var current = element.Parent; current != null; current = current.Parent)
        {
            if (string.Equals(current.TagName, "body", StringComparison.OrdinalIgnoreCase))
                return current;

            if (ReferenceEquals(current, documentElement))
                return fallbackBody ?? documentElement;

            var currentProps = GetComputedProps(current);
            var currentPosition = currentProps.GetValueOrDefault("position");
            if (!string.IsNullOrWhiteSpace(currentPosition) &&
                !string.Equals(currentPosition, "static", StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }
        }

        return fallbackBody ?? documentElement;
    }

    private DomElement? GetScrollParentForDomElement(DomElement element)
    {
        var documentElement = GetOwningDocumentElement(element);
        if (!HasAssociatedLayoutBox(element))
            return null;

        if (ReferenceEquals(element, documentElement) || ReferenceEquals(element, GetDocumentScrollingElement(documentElement)))
            return null;

        if (IsViewportBodyElement(element, documentElement))
            return GetDocumentScrollingElement(documentElement);

        var props = GetComputedProps(element);
        var position = props.GetValueOrDefault("position")?.Trim().ToLowerInvariant();
        if (string.Equals(position, "fixed", StringComparison.OrdinalIgnoreCase))
        {
            var fixedContainingBlock = FindFixedPositionContainingBlock(element, documentElement);
            if (fixedContainingBlock == null)
                return null;

            return FindNearestScrollParent(fixedContainingBlock, documentElement);
        }

        if (string.Equals(position, "absolute", StringComparison.OrdinalIgnoreCase))
        {
            var containingBlock = GetOffsetParentForDomElement(element);
            if (containingBlock == null)
                return GetDocumentScrollingElement(documentElement);

            return FindNearestScrollParent(containingBlock, documentElement);
        }

        return FindNearestScrollParent(element.Parent, documentElement);
    }

    private DomElement GetDocumentScrollingElement(DomElement documentElement) => documentElement;

    private DomElement FindNearestScrollParent(DomElement? start, DomElement documentElement)
    {
        for (var current = start; current != null; current = current.Parent)
        {
            if (ReferenceEquals(current, documentElement))
                return GetDocumentScrollingElement(documentElement);

            if (IsViewportBodyElement(current, documentElement) || !HasAssociatedLayoutBox(current))
                continue;

            if (HasOverflowClipping(GetComputedProps(current)))
                return current;
        }

        return GetDocumentScrollingElement(documentElement);
    }

    private DomElement? FindFixedPositionContainingBlock(DomElement element, DomElement documentElement)
    {
        for (var current = element.Parent; current != null; current = current.Parent)
        {
            if (ReferenceEquals(current, documentElement))
                break;

            if (EstablishesFixedPositionContainingBlock(current))
                return current;
        }

        return null;
    }

    private bool EstablishesFixedPositionContainingBlock(DomElement element)
    {
        var props = GetComputedProps(element);
        var transform = props.GetValueOrDefault("transform");
        if (!string.IsNullOrWhiteSpace(transform) &&
            !string.Equals(transform.Trim(), "none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var contain = props.GetValueOrDefault("contain");
        if (string.IsNullOrWhiteSpace(contain))
            return false;

        var normalized = contain.Trim().ToLowerInvariant();
        return normalized.Contains("paint") ||
               normalized.Contains("layout") ||
               normalized.Contains("strict") ||
               normalized.Contains("content");
    }

    private bool HasAssociatedLayoutBox(DomElement element)
    {
        if (element.IsTextNode)
            return false;

        if (element.TagName.StartsWith("#", StringComparison.Ordinal))
            return false;

        var display = GetComputedProps(element).GetValueOrDefault("display")?.Trim().ToLowerInvariant();
        return !string.Equals(display, "none", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(display, "contents", StringComparison.OrdinalIgnoreCase);
    }

    private double GetScrollWidthForDomElement(DomElement element, bool isRoot)
    {
        if (TryGetSelectListBoxScrollExtent(element, verticalAxis: false, out var selectScrollWidth))
            return selectScrollWidth;

        var props = GetComputedProps(element);
        var ownWidth = GetClientWidthForDomElement(element, isRoot: false);
        var ownZoom = GetUsedZoomForElement(element);
        var maxWidth = ownWidth;
        var elementRect = ComputeRenderedRect(element);
        var borderLeft = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-left-width"), element);
        var trailingPadding = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-right"), element);
        var originLeft = elementRect.Left + borderLeft;

        foreach (var child in EnumerateRenderedDescendants(element))
        {
            var childRect = ComputeRenderedRect(child);
            var widthInContainerSpace = ownZoom > 0.0001 ? (childRect.Width / ownZoom) : childRect.Width;
            var childOffset = ReferenceEquals(GetAssignedSlot(child), element)
                ? ComputeOffsetWithinAncestor(child, element, vertical: false)
                : childRect.Left - originLeft;
            maxWidth = Math.Max(maxWidth, childOffset + widthInContainerSpace + trailingPadding);
        }

        return maxWidth;
    }

    private double GetScrollHeightForDomElement(DomElement element, bool isRoot)
    {
        if (TryGetSelectListBoxScrollExtent(element, verticalAxis: true, out var selectScrollHeight))
            return selectScrollHeight;

        var props = GetComputedProps(element);
        var ownHeight = GetClientHeightForDomElement(element, isRoot: false);
        var ownZoom = GetUsedZoomForElement(element);
        var maxHeight = ownHeight;
        var elementRect = ComputeRenderedRect(element);
        var borderTop = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-top-width"), element);
        var trailingPadding = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-bottom"), element);
        var originTop = elementRect.Top + borderTop;

        foreach (var child in EnumerateRenderedDescendants(element))
        {
            var childRect = ComputeRenderedRect(child);
            var heightInContainerSpace = ownZoom > 0.0001 ? (childRect.Height / ownZoom) : childRect.Height;
            var childOffset = ReferenceEquals(GetAssignedSlot(child), element)
                ? ComputeOffsetWithinAncestor(child, element, vertical: true)
                : childRect.Top - originTop;
            maxHeight = Math.Max(maxHeight, childOffset + heightInContainerSpace + trailingPadding);
        }

        return maxHeight;
    }

    private double ComputeOffsetRelativeToAncestor(DomElement element, DomElement ancestor, bool vertical)
    {
        double offset = 0;
        var current = element;
        while (current.Parent != null && !ReferenceEquals(current.Parent, ancestor))
        {
            offset += ComputeOffsetWithinParentForOffset(current, vertical);
            current = current.Parent;
        }

        if (current.Parent != null && ReferenceEquals(current.Parent, ancestor))
            offset += ComputeOffsetWithinParentForOffset(current, vertical);

        return offset;
    }

    private double ComputeOffsetWithinActualAncestor(DomElement element, DomElement ancestor, bool vertical)
    {
        double offset = 0;
        var current = element;

        while (current.Parent != null && !ReferenceEquals(current.Parent, ancestor))
        {
            offset += ComputeOffsetWithinParent(current, vertical);
            current = current.Parent;
        }

        if (current.Parent != null && ReferenceEquals(current.Parent, ancestor))
            offset += ComputeOffsetWithinParent(current, vertical);

        return offset;
    }

    private double ComputeOffsetWithinParentForOffset(DomElement element, bool vertical)
    {
        var parent = element.Parent;
        if (parent == null)
            return 0;

        var props = GetComputedProps(element);
        var position = props.GetValueOrDefault("position");
        var margin = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault(vertical ? "margin-top" : "margin-left"), element);
        var positional = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault(vertical ? "top" : "left"), element);
        var parentProps = GetComputedProps(parent);
        var parentPadding = ParseCssLengthToPixelsWithViewport(parentProps.GetValueOrDefault(vertical ? "padding-top" : "padding-left"), parent);

        if (string.Equals(position, "absolute", StringComparison.OrdinalIgnoreCase))
            return margin + positional;

        double offset = parentPadding;
        if (vertical)
        {
            foreach (var sibling in parent.Children)
            {
                if (ReferenceEquals(sibling, element))
                    break;
                if (sibling.IsTextNode)
                    continue;
                if (!HasAssociatedLayoutBox(sibling))
                    continue;

                var siblingProps = GetComputedProps(sibling);
                var siblingPosition = siblingProps.GetValueOrDefault("position");
                if (string.Equals(siblingPosition, "absolute", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(siblingPosition, "fixed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!ShouldCollapseTopMarginWithParent(sibling))
                    offset += ParseCssLengthToPixelsWithViewport(siblingProps.GetValueOrDefault("margin-top"), sibling);
                offset += GetBorderBoxHeight(siblingProps, sibling);
                offset += ParseCssLengthToPixelsWithViewport(siblingProps.GetValueOrDefault("margin-bottom"), sibling);
                if (string.Equals(siblingPosition, "relative", StringComparison.OrdinalIgnoreCase))
                    offset += ParseCssLengthToPixelsWithViewport(siblingProps.GetValueOrDefault("top"), sibling);
            }

            if (!ShouldCollapseTopMarginWithParent(element))
                offset += margin;
        }
        else
        {
            offset += margin;
        }

        if (string.Equals(position, "relative", StringComparison.OrdinalIgnoreCase))
            offset += positional;

        return offset;
    }

    private bool ShouldCollapseTopMarginWithParent(DomElement element)
    {
        if (element.Parent == null)
            return false;

        var props = GetComputedProps(element);
        var position = props.GetValueOrDefault("position");
        if (string.Equals(position, "absolute", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(position, "fixed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var sibling in element.Parent.Children)
        {
            if (sibling.IsTextNode)
                continue;
            if (ReferenceEquals(sibling, element))
                break;
            return false;
        }

        var parentProps = GetComputedProps(element.Parent);
        return ParseCssLengthToPixelsWithViewport(parentProps.GetValueOrDefault("border-top-width"), element.Parent) == 0 &&
               ParseCssLengthToPixelsWithViewport(parentProps.GetValueOrDefault("padding-top"), element.Parent) == 0;
    }

    private (double Left, double Top, double Width, double Height) GetBoundingClientRectForDomElement(DomElement element, bool isRoot)
    {
        if (isRoot)
            return (0, 0, GetViewportReferenceLength(element, vertical: false), GetViewportReferenceLength(element, vertical: true));

        return ComputeRenderedRect(element);
    }

    private (double Left, double Top, double Width, double Height) ComputeRenderedRect(DomElement element)
    {
        var layoutRect = ComputeUnzoomedLayoutRect(element);
        var zoom = GetUsedZoomForElement(element);
        var transformScale = GetTransformScale(element);
        return (layoutRect.Left, layoutRect.Top, layoutRect.Width * zoom * transformScale, layoutRect.Height * zoom * transformScale);
    }

    private (double Left, double Top, double Width, double Height) ComputeUnzoomedLayoutRect(DomElement element)
    {
        var props = GetComputedProps(element);
        var containingBlockWidth = ResolveContainingBlockReferenceLength(element, vertical: false);
        var containingBlockHeight = ResolveContainingBlockReferenceLength(element, vertical: true);
        var width = ResolveContentBoxExtent(element, vertical: false);
        var height = ResolveContentBoxExtent(element, vertical: true);
        var marginTop = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("margin-top"), element, percentageBasis: containingBlockWidth);
        var marginLeft = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("margin-left"), element, percentageBasis: containingBlockWidth);
        var top = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("top"), element, percentageBasis: containingBlockHeight);
        var left = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("left"), element, percentageBasis: containingBlockWidth);
        var position = props.GetValueOrDefault("position");
        var isSvgPositionedGeometryElement = IsSvgPositionedGeometryElement(element);

        if (isSvgPositionedGeometryElement)
        {
            top = ResolveSvgGeometryLength(element, "y", vertical: true, containingBlockHeight);
            left = ResolveSvgGeometryLength(element, "x", vertical: false, containingBlockWidth);
            position = "absolute";
            marginTop = 0;
            marginLeft = 0;
        }

        if (element.Parent == null || string.Equals(element.TagName, "html", StringComparison.OrdinalIgnoreCase))
            return (0, 0, width, height);

        if (string.Equals(element.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            var specifiedMarginTop = props.GetValueOrDefault("margin-top");
            var specifiedMarginLeft = props.GetValueOrDefault("margin-left");
            var bodyMarginTop = HasExplicitBodyMargin(specifiedMarginTop) ? marginTop : DefaultBodyMarginPixels;
            var bodyMarginLeft = HasExplicitBodyMargin(specifiedMarginLeft) ? marginLeft : DefaultBodyMarginPixels;
            return (bodyMarginLeft, bodyMarginTop, width, height);
        }

        var parentRect = ComputeUnzoomedLayoutRect(element.Parent);
        var parentProps = GetComputedProps(element.Parent);
        var parentBorderTop = ParseCssLengthToPixelsWithViewport(parentProps.GetValueOrDefault("border-top-width"), element.Parent);
        var parentBorderLeft = ParseCssLengthToPixelsWithViewport(parentProps.GetValueOrDefault("border-left-width"), element.Parent);
        var parentPaddingTop = ParseCssLengthToPixelsWithViewport(parentProps.GetValueOrDefault("padding-top"), element.Parent);
        var parentPaddingLeft = ParseCssLengthToPixelsWithViewport(parentProps.GetValueOrDefault("padding-left"), element.Parent);

        var baseTop = parentRect.Top + parentBorderTop + parentPaddingTop;
        var baseLeft = parentRect.Left + parentBorderLeft + parentPaddingLeft;

        if (!string.Equals(position, "absolute", StringComparison.OrdinalIgnoreCase) && !IsSvgGeometryContainer(element.Parent))
        {
            foreach (var sibling in element.Parent.Children)
            {
                if (ReferenceEquals(sibling, element))
                    break;
                if (sibling.IsTextNode)
                    continue;

                var siblingProps = GetComputedProps(sibling);
                var siblingDisplay = siblingProps.GetValueOrDefault("display");
                if (!string.Equals(siblingDisplay, "contents", StringComparison.OrdinalIgnoreCase) &&
                    !HasAssociatedLayoutBox(sibling))
                {
                    continue;
                }

                var siblingRect = ComputeRenderedRect(sibling);
                var siblingPosition = siblingProps.GetValueOrDefault("position");
                if (string.Equals(siblingPosition, "absolute", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(siblingPosition, "fixed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                baseTop += GetNormalFlowHeightContribution(sibling, siblingRect);
                baseTop += ParseCssLengthToPixelsWithViewport(siblingProps.GetValueOrDefault("margin-top"), sibling);
                baseTop += ParseCssLengthToPixelsWithViewport(siblingProps.GetValueOrDefault("margin-bottom"), sibling);
            }
        }

        var resolvedTop = baseTop + marginTop;
        var resolvedLeft = baseLeft + marginLeft;

        if (string.Equals(position, "absolute", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(position, "relative", StringComparison.OrdinalIgnoreCase))
        {
            resolvedTop += top;
            resolvedLeft += left;
        }

        var (translateX, translateY) = GetTransformTranslate(element);
        resolvedTop += translateY;
        resolvedLeft += translateX;

        return (resolvedLeft, resolvedTop, width, height);
    }

    private double GetNormalFlowHeightContribution(
        DomElement element,
        (double Left, double Top, double Width, double Height) renderedRect)
    {
        var display = GetComputedProps(element).GetValueOrDefault("display");
        if (!string.Equals(display, "contents", StringComparison.OrdinalIgnoreCase))
            return renderedRect.Height;

        var hasRect = false;
        var minTop = 0.0;
        var maxBottom = 0.0;
        CollectDisplayContentsFlowExtents(element, ref hasRect, ref minTop, ref maxBottom);
        return hasRect ? Math.Max(0, maxBottom - minTop) : 0;
    }

    private void CollectDisplayContentsFlowExtents(
        DomElement element,
        ref bool hasRect,
        ref double minTop,
        ref double maxBottom)
    {
        foreach (var child in element.Children)
        {
            if (child.IsTextNode || string.Equals(child.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                continue;

            var childProps = GetComputedProps(child);
            var childPosition = childProps.GetValueOrDefault("position");
            if (string.Equals(childPosition, "absolute", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(childPosition, "fixed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var childDisplay = childProps.GetValueOrDefault("display");
            if (string.Equals(childDisplay, "contents", StringComparison.OrdinalIgnoreCase))
            {
                CollectDisplayContentsFlowExtents(child, ref hasRect, ref minTop, ref maxBottom);
                continue;
            }

            var rect = ComputeRenderedRect(child);
            if (!hasRect)
            {
                minTop = rect.Top;
                maxBottom = rect.Top + rect.Height;
                hasRect = true;
                continue;
            }

            minTop = Math.Min(minTop, rect.Top);
            maxBottom = Math.Max(maxBottom, rect.Top + rect.Height);
        }
    }

    private static bool IsSvgGeometryContainer(DomElement? element) =>
        element != null && IsSvgElement(element);

    private static bool IsSvgPositionedGeometryElement(DomElement element)
    {
        if (!IsSvgElement(element))
            return false;

        if (IsSvgShapeElement(element))
            return true;

        return IsSvgViewportElement(element) && IsSvgGeometryContainer(element.Parent);
    }

    private static bool IsSvgShapeElement(DomElement element)
    {
        var tag = element.TagName;
        return string.Equals(tag, "rect", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "svg:rect", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "image", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "svg:image", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "foreignobject", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "svg:foreignobject", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSvgViewportElement(DomElement element)
    {
        var tag = element.TagName;
        return string.Equals(tag, "svg", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "svg:svg", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSvgElement(DomElement element) =>
        string.Equals(element.NamespaceURI, "http://www.w3.org/2000/svg", StringComparison.OrdinalIgnoreCase) ||
        IsSvgViewportElement(element) ||
        IsSvgShapeElement(element);

    private double ResolveSvgGeometryLength(DomElement element, string attributeName, bool vertical, double percentageBasis)
    {
        if (!IsSvgElement(element) || !element.Attributes.TryGetValue(attributeName, out var rawValue))
            return 0;

        var parsed = ParseCssLengthToPixelsWithViewport(rawValue, element, percentageBasis: percentageBasis);
        if (parsed > 0 || string.Equals(rawValue?.Trim(), "0", StringComparison.Ordinal))
            return parsed;

        if ((string.Equals(attributeName, "width", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(attributeName, "height", StringComparison.OrdinalIgnoreCase)) &&
            element.Attributes.TryGetValue("viewBox", out var viewBox) &&
            !string.IsNullOrWhiteSpace(viewBox))
        {
            var parts = viewBox.Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4 &&
                double.TryParse(parts[vertical ? 3 : 2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var viewBoxLength))
            {
                return viewBoxLength;
            }
        }

        return 0;
    }

    private double GetUsedZoomForElement(DomElement element)
    {
        var props = GetComputedProps(element);
        var specifiedZoom = props.GetValueOrDefault("zoom");
        var parentZoom = element.Parent != null ? GetUsedZoomForElement(element.Parent) : 1.0;
        return ResolveSpecifiedZoom(specifiedZoom, parentZoom);
    }

    private static double ResolveSpecifiedZoom(string? specifiedZoom, double parentZoom)
    {
        if (string.IsNullOrWhiteSpace(specifiedZoom) ||
            specifiedZoom.Equals("inherit", StringComparison.OrdinalIgnoreCase) ||
            specifiedZoom.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return parentZoom;
        }

        if (double.TryParse(specifiedZoom, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var zoom) && zoom > 0)
            return parentZoom * zoom;

        return parentZoom;
    }

    private double GetTransformScale(DomElement element)
    {
        var transform = GetElementTransformValue(element);
        if (string.IsNullOrWhiteSpace(transform))
            return 1;

        var match = System.Text.RegularExpressions.Regex.Match(transform, @"scale\(\s*(?<value>[-+]?[0-9]*\.?[0-9]+)\s*\)", RegexOptions.IgnoreCase);
        if (match.Success &&
            double.TryParse(match.Groups["value"].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double scale))
        {
            return scale;
        }

        return 1;
    }

    private (double X, double Y) GetTransformTranslate(DomElement element)
    {
        var transform = GetElementTransformValue(element);
        if (string.IsNullOrWhiteSpace(transform))
            return (0, 0);

        double translateX = 0;
        double translateY = 0;
        foreach (Match match in System.Text.RegularExpressions.Regex.Matches(
                     transform,
                     @"translate\(\s*(?<x>[-+]?[0-9]*\.?[0-9]+)(?:[,\s]+(?<y>[-+]?[0-9]*\.?[0-9]+))?\s*\)",
                     RegexOptions.IgnoreCase))
        {
            if (double.TryParse(match.Groups["x"].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsedX))
            {
                translateX += parsedX;
            }

            if (match.Groups["y"].Success &&
                double.TryParse(match.Groups["y"].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsedY))
            {
                translateY += parsedY;
            }
        }

        return (translateX, translateY);
    }

    private string? GetElementTransformValue(DomElement element)
    {
        var props = GetComputedProps(element);
        var transform = props.GetValueOrDefault("transform");
        if (!string.IsNullOrWhiteSpace(transform) &&
            !string.Equals(transform.Trim(), "none", StringComparison.OrdinalIgnoreCase))
        {
            return transform;
        }

        return element.Attributes.TryGetValue("transform", out var attributeTransform)
            ? attributeTransform
            : null;
    }

    private static bool IsSvgGroupElement(DomElement element)
    {
        var tag = element.TagName;
        return string.Equals(tag, "g", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "svg:g", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSvgTextContentElement(DomElement element)
    {
        var tag = element.TagName;
        return string.Equals(tag, "text", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "svg:text", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "tspan", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "svg:tspan", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "textpath", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "svg:textpath", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetSvgChildrenUnionRect(
        DomElement element,
        out (double Left, double Top, double Width, double Height) rect)
    {
        var found = false;
        var minLeft = 0d;
        var minTop = 0d;
        var maxRight = 0d;
        var maxBottom = 0d;

        foreach (var child in element.Children)
        {
            if (child.IsTextNode || child.TagName.StartsWith("#", StringComparison.Ordinal))
                continue;

            var childRect = GetHitTestRectForElement(child);
            if (childRect.Width <= 0 || childRect.Height <= 0)
                continue;

            if (!found)
            {
                found = true;
                minLeft = childRect.Left;
                minTop = childRect.Top;
                maxRight = childRect.Left + childRect.Width;
                maxBottom = childRect.Top + childRect.Height;
                continue;
            }

            minLeft = Math.Min(minLeft, childRect.Left);
            minTop = Math.Min(minTop, childRect.Top);
            maxRight = Math.Max(maxRight, childRect.Left + childRect.Width);
            maxBottom = Math.Max(maxBottom, childRect.Top + childRect.Height);
        }

        rect = found
            ? (minLeft, minTop, Math.Max(0, maxRight - minLeft), Math.Max(0, maxBottom - minTop))
            : (0, 0, 0, 0);
        return found;
    }

    private bool TryGetSvgTextHitTestRect(
        DomElement element,
        out (double Left, double Top, double Width, double Height) rect)
    {
        var text = GetDirectTextContent(element);
        if (string.IsNullOrWhiteSpace(text))
        {
            rect = (0, 0, 0, 0);
            return false;
        }

        var fontSize = ResolveFontSizeForElement(element);
        if (fontSize <= 0)
            fontSize = 16;

        var width = text
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .DefaultIfEmpty(string.Empty)
            .Max(line => line.Length) * fontSize * 0.6;
        if (width <= 0)
        {
            rect = (0, 0, 0, 0);
            return false;
        }

        var baselineX = ResolveSvgTextCoordinate(element, "x");
        var baselineY = ResolveSvgTextCoordinate(element, "y");
        if (IsSvgTextPathElement(element) &&
            TryResolveSvgTextPathStart(element, out var pathStart))
        {
            if (!HasOwnSvgCoordinate(element, "x"))
                baselineX = pathStart.X;
            if (!HasOwnSvgCoordinate(element, "y"))
                baselineY = pathStart.Y;
        }

        var viewport = FindNearestSvgViewportAncestor(element);
        if (viewport != null)
        {
            var viewportRect = ComputeRenderedRect(viewport);
            baselineX += viewportRect.Left;
            baselineY += viewportRect.Top;
        }

        rect = (baselineX, baselineY - fontSize, width, fontSize);
        return true;
    }

    private static string GetDirectTextContent(DomElement element)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(element.TextContent))
            sb.Append(element.TextContent);

        foreach (var child in element.Children)
        {
            if (child.IsTextNode && !string.IsNullOrWhiteSpace(child.TextContent))
                sb.Append(child.TextContent);
        }

        return sb.ToString();
    }

    private double ResolveSvgTextCoordinate(DomElement element, string attributeName)
    {
        for (var current = element; current != null; current = current.Parent)
        {
            if (!IsSvgTextContentElement(current))
                continue;

            if (current.Attributes.TryGetValue(attributeName, out var rawValue))
            {
                var percentageBasis = ResolveContainingBlockReferenceLength(
                    current,
                    vertical: string.Equals(attributeName, "y", StringComparison.OrdinalIgnoreCase));
                var resolved = ParseCssLengthToPixelsWithViewport(rawValue, current, percentageBasis: percentageBasis);
                if (resolved > 0 || string.Equals(rawValue?.Trim(), "0", StringComparison.Ordinal))
                    return resolved;

                var scalar = rawValue?
                    .Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();
                if (double.TryParse(
                    scalar,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var numericValue))
                {
                    return numericValue;
                }
            }

            if (IsSvgTextPathElement(current) &&
                TryResolveSvgTextPathStart(current, out var pathStart))
            {
                return string.Equals(attributeName, "y", StringComparison.OrdinalIgnoreCase)
                    ? pathStart.Y
                    : pathStart.X;
            }
        }

        return 0;
    }

    private static bool HasOwnSvgCoordinate(DomElement element, string attributeName) =>
        element.Attributes.TryGetValue(attributeName, out var rawValue) &&
        !string.IsNullOrWhiteSpace(rawValue);

    private bool TryResolveSvgTextPathStart(DomElement element, out (double X, double Y) point)
    {
        point = default;
        if (!element.Attributes.TryGetValue("href", out var href) &&
            !element.Attributes.TryGetValue("xlink:href", out href))
        {
            return false;
        }

        href = href?.Trim();
        if (string.IsNullOrWhiteSpace(href) || !href.StartsWith('#'))
            return false;

        var documentElement = GetOwningDocumentElement(element);
        var referencedPath = documentElement != null
            ? FindInTree(documentElement, candidate => string.Equals(candidate.Id, href[1..], StringComparison.Ordinal))
            : null;
        if (referencedPath == null ||
            !referencedPath.Attributes.TryGetValue("d", out var pathData) ||
            string.IsNullOrWhiteSpace(pathData))
        {
            return false;
        }

        var moveMatch = System.Text.RegularExpressions.Regex.Match(
            pathData,
            @"[Mm]\s*(?<x>[-+]?[0-9]*\.?[0-9]+)(?:[\s,]+(?<y>[-+]?[0-9]*\.?[0-9]+))",
            RegexOptions.CultureInvariant);
        if (!moveMatch.Success ||
            !double.TryParse(moveMatch.Groups["x"].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var x) ||
            !double.TryParse(moveMatch.Groups["y"].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var y))
        {
            return false;
        }

        point = (x, y);
        return true;
    }

    private static bool IsSvgTextPathElement(DomElement element)
    {
        var tag = element.TagName;
        return string.Equals(tag, "textpath", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "svg:textpath", StringComparison.OrdinalIgnoreCase);
    }

    private static DomElement? FindNearestSvgViewportAncestor(DomElement element)
    {
        for (var current = element.Parent; current != null; current = current.Parent)
        {
            if (IsSvgViewportElement(current))
                return current;
        }

        return null;
    }

    private double GetBorderBoxWidth(Dictionary<string, string> props, DomElement? element = null)
    {
        var containingBlockWidth = element != null ? ResolveContainingBlockReferenceLength(element, vertical: false) : (double?)null;
        return ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("width"), element, percentageBasis: containingBlockWidth)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-left"), element, percentageBasis: containingBlockWidth)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-right"), element, percentageBasis: containingBlockWidth)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-left-width"), element)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-right-width"), element);
    }

    private double GetBorderBoxHeight(Dictionary<string, string> props, DomElement? element = null)
    {
        var containingBlockWidth = element != null ? ResolveContainingBlockReferenceLength(element, vertical: false) : (double?)null;
        var containingBlockHeight = element != null ? ResolveContainingBlockReferenceLength(element, vertical: true) : (double?)null;
        return ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("height"), element, percentageBasis: containingBlockHeight)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-top"), element, percentageBasis: containingBlockWidth)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-bottom"), element, percentageBasis: containingBlockWidth)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-top-width"), element)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-bottom-width"), element);
    }

    private void ScrollElementIntoView(
        DomElement element,
        string? block = null,
        string? inline = null,
        string? behavior = null)
    {
        var current = element;
        for (var i = 0; i < MaxScrollContinuationDepth && current != null; i++)
        {
            var scrollContainer = FindScrollContainer(current) ?? GetOwningDocumentElement(current);
            if (scrollContainer == null)
                return;

            if (IsDocumentElement(scrollContainer) && HasFixedPositionInDocument(current, scrollContainer))
            {
                if (HasActiveVisualViewport())
                {
                    ScrollFixedElementIntoVisualViewport(element, scrollContainer, block, inline);
                    current = GetOuterFrameElement(scrollContainer);
                    continue;
                }

                current = GetOuterFrameElement(scrollContainer);
                continue;
            }

            var (horizontalAlignment, verticalAlignment) = ResolvePhysicalScrollIntoViewAlignments(
                scrollContainer,
                block,
                inline);
            var scrollTop = ResolveScrollIntoViewOffset(element, scrollContainer, vertical: true, alignment: verticalAlignment);
            var scrollLeft = ResolveScrollIntoViewOffset(element, scrollContainer, vertical: false, alignment: horizontalAlignment);

            SetElementScrollOffsetsWithBehavior(scrollContainer, scrollLeft, scrollTop, clamp: true, behavior: behavior);

            var next = GetOuterScrollContinuationElement(scrollContainer);
            if (next == null || ReferenceEquals(next, current))
                return;

            current = next;
        }
    }

    private (string Block, string Inline, string? Behavior) GetScrollIntoViewOptions(in Arguments args)
    {
        const string defaultBlock = "start";
        const string defaultInline = "nearest";

        if (args.Length == 0)
            return (defaultBlock, "start-if-needed", null);

        var first = args[0];
        if (first is JSObject options)
        {
            return (
                NormalizeScrollIntoViewAlignment(GetOptionalStringOption(options, "block"), defaultBlock),
                NormalizeScrollIntoViewAlignment(GetOptionalStringOption(options, "inline"), defaultInline),
                GetOptionalScrollBehavior(options));
        }

        if (first.IsBoolean)
        {
            return first.BooleanValue
                ? (defaultBlock, defaultInline, null)
                : ("end", defaultInline, null);
        }

        return (defaultBlock, defaultInline, null);
    }

    private (double? Left, double? Top, string? Behavior) GetScrollArguments(in Arguments args)
    {
        if (args.Length == 0)
            return (null, null, null);

        if (args[0] is JSObject options)
        {
            return (
                GetOptionalScrollCoordinate(options, "left"),
                GetOptionalScrollCoordinate(options, "top"),
                GetOptionalScrollBehavior(options));
        }

        return (args.Length > 0 ? args[0].DoubleValue : null, args.Length > 1 ? args[1].DoubleValue : null, null);
    }

    private static double? GetOptionalScrollCoordinate(JSObject options, string propertyName)
    {
        var value = options[(KeyString)propertyName];
        return value == null || value.IsUndefined || value.IsNull ? null : value.DoubleValue;
    }

    private static string? GetOptionalScrollBehavior(JSObject options)
    {
        var value = options[(KeyString)"behavior"];
        if (value == null || value.IsUndefined || value.IsNull)
            return null;

        var behavior = value.ToString();
        return string.IsNullOrWhiteSpace(behavior) ? null : behavior;
    }

    private static string? GetOptionalStringOption(JSObject options, string propertyName)
    {
        var value = options[(KeyString)propertyName];
        if (value == null || value.IsUndefined || value.IsNull)
            return null;

        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string NormalizeScrollIntoViewAlignment(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "start" or "center" or "end" or "nearest" or "start-if-needed" or "end-if-needed"
            ? normalized
            : fallback;
    }

    private (string Horizontal, string Vertical) ResolvePhysicalScrollIntoViewAlignments(
        DomElement scrollContainer,
        string? block,
        string? inline)
    {
        var props = GetComputedProps(scrollContainer);
        var writingMode = props.GetValueOrDefault("writing-mode")?.Trim().ToLowerInvariant();
        var direction = props.GetValueOrDefault("direction");
        bool isVerticalWritingMode = IsVerticalWritingMode(writingMode);
        bool isRtl = string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase);

        var horizontal = ResolvePhysicalAxisAlignment(
            alignment: isVerticalWritingMode ? block : inline,
            startMapsToPhysicalStart: !isVerticalWritingMode
                ? !isRtl
                : writingMode?.EndsWith("-rl", StringComparison.Ordinal) != true);
        var vertical = ResolvePhysicalAxisAlignment(
            alignment: isVerticalWritingMode ? inline : block,
            startMapsToPhysicalStart: !isVerticalWritingMode || !isRtl);
        return (horizontal, vertical);
    }

    private static string ResolvePhysicalAxisAlignment(
        string? alignment,
        bool startMapsToPhysicalStart)
    {
        var normalized = NormalizeScrollIntoViewAlignment(alignment, "start");
        if (normalized is "center" or "nearest" || startMapsToPhysicalStart)
            return normalized;

        return normalized switch
        {
            "start" => "end",
            "end" => "start",
            "start-if-needed" => "end-if-needed",
            "end-if-needed" => "start-if-needed",
            _ => normalized
        };
    }

    private double GetElementScrollOffset(DomElement element, bool vertical)
    {
        if (!CanProgrammaticallyScroll(element, vertical))
            return 0;

        return TryGetStoredScrollOffset(element, vertical, out var scrollOffset)
            ? scrollOffset
            : 0;
    }

    private void SetElementScrollOffsets(DomElement element, double? left = null, double? top = null, bool relative = false, bool clamp = true)
    {
        var (nextLeft, nextTop) = ResolveElementScrollOffsets(element, left, top, relative, clamp);
        GetElementRuntimeState(element).Scroll.Left.Set(nextLeft);
        GetElementRuntimeState(element).Scroll.Top.Set(nextTop);
    }

    private (double Left, double Top) ResolveElementScrollOffsets(DomElement element, double? left = null, double? top = null, bool relative = false, bool clamp = true)
    {
        var currentLeft = GetElementScrollOffset(element, vertical: false);
        var currentTop = GetElementScrollOffset(element, vertical: true);

        var nextLeft = left.HasValue ? (relative ? currentLeft + left.Value : left.Value) : currentLeft;
        var nextTop = top.HasValue ? (relative ? currentTop + top.Value : top.Value) : currentTop;

        if (!CanProgrammaticallyScroll(element, vertical: false))
            nextLeft = 0;
        if (!CanProgrammaticallyScroll(element, vertical: true))
            nextTop = 0;

        if (clamp)
        {
            var (minLeft, maxLeft, minTop, maxTop) = GetScrollBounds(element);
            nextLeft = Math.Clamp(nextLeft, minLeft, maxLeft);
            nextTop = Math.Clamp(nextTop, minTop, maxTop);
        }

        return (nextLeft, nextTop);
    }

    private void SetElementScrollOffsetsWithBehavior(
        DomElement element,
        double? left = null,
        double? top = null,
        bool relative = false,
        bool clamp = true,
        string? behavior = null)
    {
        var trackVisualViewport = ReferenceEquals(element, DocumentElement);
        var previousVisualPageLeft = trackVisualViewport ? GetVisualViewportPageOffset(vertical: false) : 0;
        var previousVisualPageTop = trackVisualViewport ? GetVisualViewportPageOffset(vertical: true) : 0;
        var previousLeft = GetElementScrollOffset(element, vertical: false);
        var previousTop = GetElementScrollOffset(element, vertical: true);
        var (targetLeft, targetTop) = ResolveElementScrollOffsets(element, left, top, relative, clamp);
        var hadActiveSmoothScroll = _smoothScrollTokens.ContainsKey(element);
        var effectiveBehavior = ResolveScrollBehavior(element, behavior);
        if (hadActiveSmoothScroll && NormalizeScrollBehavior(behavior) != "smooth")
            effectiveBehavior = "instant";
        CancelSmoothScroll(element);

        if (string.Equals(effectiveBehavior, "smooth", StringComparison.OrdinalIgnoreCase))
        {
            var token = ++_frameActionIdCounter;
            _smoothScrollTokens[element] = token;
            QueueFrameAction(() =>
            {
                if (_smoothScrollTokens.TryGetValue(element, out var activeToken) && activeToken == token)
                {
                    var queuedPreviousLeft = GetElementScrollOffset(element, vertical: false);
                    var queuedPreviousTop = GetElementScrollOffset(element, vertical: true);
                    var queuedPreviousVisualPageLeft = trackVisualViewport ? GetVisualViewportPageOffset(vertical: false) : 0;
                    var queuedPreviousVisualPageTop = trackVisualViewport ? GetVisualViewportPageOffset(vertical: true) : 0;
                    GetElementRuntimeState(element).Scroll.Left.Set(targetLeft);
                    GetElementRuntimeState(element).Scroll.Top.Set(targetTop);
                    NotifyVisualViewportScrollIfNeeded(queuedPreviousVisualPageLeft, queuedPreviousVisualPageTop, trackVisualViewport);
                    DispatchScrollEventIfNeeded(element, queuedPreviousLeft, queuedPreviousTop);
                    DispatchScrollEndEventIfNeeded(element, queuedPreviousLeft, queuedPreviousTop);
                    _smoothScrollTokens.Remove(element);
                }
            });

            // Approximate smooth scrolling with a visible intermediate frame before
            // finishing on the next queued frame.
            GetElementRuntimeState(element).Scroll.Left.Set(previousLeft + ((targetLeft - previousLeft) / 2.0));
            GetElementRuntimeState(element).Scroll.Top.Set(previousTop + ((targetTop - previousTop) / 2.0));
            NotifyVisualViewportScrollIfNeeded(previousVisualPageLeft, previousVisualPageTop, trackVisualViewport);
            DispatchScrollEventIfNeeded(element, previousLeft, previousTop);
            return;
        }

        GetElementRuntimeState(element).Scroll.Left.Set(targetLeft);
        GetElementRuntimeState(element).Scroll.Top.Set(targetTop);
        NotifyVisualViewportScrollIfNeeded(previousVisualPageLeft, previousVisualPageTop, trackVisualViewport);
        DispatchScrollEventIfNeeded(element, previousLeft, previousTop);
        DispatchScrollEndEventIfNeeded(element, previousLeft, previousTop);
    }

    private void QueueFrameAction(Action callback)
    {
        _frameActions[++_frameActionIdCounter] = callback;
    }

    private void CancelSmoothScroll(DomElement element)
    {
        _smoothScrollTokens.Remove(element);
    }

    private void DispatchScrollEventIfNeeded(DomElement element, double previousLeft, double previousTop)
    {
        if (AreClose(previousLeft, GetElementScrollOffset(element, vertical: false)) &&
            AreClose(previousTop, GetElementScrollOffset(element, vertical: true)))
            return;

        DispatchElementEvent(element, "scroll");
    }

    private void DispatchScrollEndEventIfNeeded(DomElement element, double previousLeft, double previousTop)
    {
        if (AreClose(previousLeft, GetElementScrollOffset(element, vertical: false)) &&
            AreClose(previousTop, GetElementScrollOffset(element, vertical: true)))
            return;

        DispatchElementEvent(element, "scrollend");
    }

    private void DispatchElementEvent(DomElement element, string eventType)
    {
        var evt = new JSObject();
        evt.FastAddValue((KeyString)"type", new JSString(eventType), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"bubbles", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        DispatchEventOnElement(element, evt);
    }

    private string ResolveScrollBehavior(DomElement element, string? requestedBehavior)
    {
        var normalizedRequested = NormalizeScrollBehavior(requestedBehavior);
        if (normalizedRequested == "instant" || normalizedRequested == "smooth")
            return normalizedRequested;

        var props = GetComputedProps(element);
        return NormalizeScrollBehavior(props.GetValueOrDefault("scroll-behavior")) == "smooth"
            ? "smooth"
            : "instant";
    }

    private static string NormalizeScrollBehavior(string? behavior)
    {
        if (string.IsNullOrWhiteSpace(behavior))
            return "auto";

        var normalized = behavior.Trim().ToLowerInvariant();
        return normalized is "instant" or "smooth" ? normalized : "auto";
    }

    private bool HasActiveVisualViewport() => GetVisualViewportScale() > 1.0001;

    private double GetVisualViewportScale() => _visualViewportScale > 1 ? _visualViewportScale : 1;

    private double GetVisualViewportWidth() => _viewportWidth / GetVisualViewportScale();

    private double GetVisualViewportHeight() => _viewportHeight / GetVisualViewportScale();

    private double GetVisualViewportPageOffset(bool vertical)
    {
        var layoutOffset = GetElementScrollOffset(DocumentElement, vertical);
        return layoutOffset + GetVisualViewportExtraOffset(vertical);
    }

    private void SetVisualViewportScale(double scale)
    {
        _visualViewportScale = double.IsFinite(scale) && scale > 1 ? scale : 1;
        ClampVisualViewportOffsets();
    }

    private void ScrollFixedElementIntoVisualViewport(
        DomElement element,
        DomElement scrollContainer,
        string? block,
        string? inline)
    {
        var targetTop = ResolveScrollIntoViewOffset(
            element,
            scrollContainer,
            vertical: true,
            alignment: block,
            viewportSizeOverride: GetVisualViewportHeight(),
            currentScrollOverride: GetVisualViewportPageOffset(vertical: true),
            offsetOverride: GetElementScrollOffset(scrollContainer, vertical: true) +
                ComputeOffsetWithinAncestor(element, scrollContainer, vertical: true),
            coordinateSpaceIsPhysical: true);
        var targetLeft = ResolveScrollIntoViewOffset(
            element,
            scrollContainer,
            vertical: false,
            alignment: inline,
            viewportSizeOverride: GetVisualViewportWidth(),
            currentScrollOverride: GetVisualViewportPageOffset(vertical: false),
            offsetOverride: GetElementScrollOffset(scrollContainer, vertical: false) +
                ComputeOffsetWithinAncestor(element, scrollContainer, vertical: false),
            coordinateSpaceIsPhysical: true);
        SetVisualViewportPageOffsets(left: targetLeft, top: targetTop);
    }

    private void SetVisualViewportPageOffsets(double? left = null, double? top = null)
    {
        var oldPageLeft = GetVisualViewportPageOffset(vertical: false);
        var oldPageTop = GetVisualViewportPageOffset(vertical: true);
        var layoutLeft = GetElementScrollOffset(DocumentElement, vertical: false);
        var layoutTop = GetElementScrollOffset(DocumentElement, vertical: true);

        if (left.HasValue)
        {
            _visualViewportPageLeftOffset = Math.Clamp(
                left.Value - layoutLeft,
                0,
                GetVisualViewportMaxExtraOffset(vertical: false));
        }

        if (top.HasValue)
        {
            _visualViewportPageTopOffset = Math.Clamp(
                top.Value - layoutTop,
                0,
                GetVisualViewportMaxExtraOffset(vertical: true));
        }

        if (!AreClose(oldPageLeft, GetVisualViewportPageOffset(vertical: false)) ||
            !AreClose(oldPageTop, GetVisualViewportPageOffset(vertical: true)))
        {
            DispatchVisualViewportScrollEvent();
        }
    }

    private void ClampVisualViewportOffsets()
    {
        _visualViewportPageLeftOffset = Math.Clamp(_visualViewportPageLeftOffset, 0, GetVisualViewportMaxExtraOffset(vertical: false));
        _visualViewportPageTopOffset = Math.Clamp(_visualViewportPageTopOffset, 0, GetVisualViewportMaxExtraOffset(vertical: true));
    }

    private double GetVisualViewportExtraOffset(bool vertical) =>
        vertical ? _visualViewportPageTopOffset : _visualViewportPageLeftOffset;

    private double GetVisualViewportMaxExtraOffset(bool vertical)
    {
        if (!HasActiveVisualViewport())
            return 0;

        var layoutSize = vertical ? _viewportHeight : _viewportWidth;
        var visualSize = vertical ? GetVisualViewportHeight() : GetVisualViewportWidth();
        return Math.Max(0, layoutSize - visualSize);
    }

    private void DispatchVisualViewportScrollEvent()
    {
        if (_visualViewportJSObject == null || _visualViewportScrollListeners.Count == 0)
            return;

        var evt = new JSObject();
        evt.FastAddValue((KeyString)"type", new JSString("scroll"), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"target", _visualViewportJSObject, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"currentTarget", _visualViewportJSObject, JSPropertyAttributes.EnumerableConfigurableValue);

        foreach (var listener in _visualViewportScrollListeners.ToList())
        {
            try
            {
                listener.InvokeFunction(new Arguments(listener, evt));
            }
            catch (Exception ex)
            {
                RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.visualViewport", $"Visual viewport listener error: {ex.Message}", ex);
            }
        }
    }

    private static bool AreClose(double left, double right) => Math.Abs(left - right) < 0.0001;

    private void NotifyVisualViewportScrollIfNeeded(double previousPageLeft, double previousPageTop, bool trackVisualViewport)
    {
        if (!trackVisualViewport)
            return;

        if (!AreClose(previousPageLeft, GetVisualViewportPageOffset(vertical: false)) ||
            !AreClose(previousPageTop, GetVisualViewportPageOffset(vertical: true)))
        {
            DispatchVisualViewportScrollEvent();
        }
    }

    private bool CanProgrammaticallyScroll(DomElement element, bool vertical)
    {
        if (IsDocumentElement(element) ||
            IsViewportBodyElement(element, GetOwningDocumentElement(element)))
        {
            return CanProgrammaticallyScrollRoot(element, vertical);
        }

        if (IsSelectListBox(element))
            return CanProgrammaticallyScrollSelectListBox(element, vertical);

        var props = GetComputedProps(element);
        var axisValue = GetOverflowAxisValue(props, vertical);

        return EnablesScrollingBox(axisValue);
    }

    private bool CanProgrammaticallyScrollRoot(DomElement rootElement, bool vertical)
    {
        var documentElement = GetOwningDocumentElement(rootElement);
        var htmlOverflow = GetOverflowAxisValue(GetComputedProps(documentElement), vertical);
        var body = FindBodyElement(documentElement);
        var bodyOverflow = body != null ? GetOverflowAxisValue(GetComputedProps(body), vertical) : null;

        if (DisablesRootScrolling(htmlOverflow) || DisablesRootScrolling(bodyOverflow))
            return false;

        return true;
    }

    private static string? GetOverflowAxisValue(Dictionary<string, string> props, bool vertical)
    {
        var axisValue = props.GetValueOrDefault(vertical ? "overflow-y" : "overflow-x");
        if (string.IsNullOrWhiteSpace(axisValue))
            axisValue = props.GetValueOrDefault("overflow");
        return axisValue;
    }

    private static bool DisablesRootScrolling(string? overflowValue)
    {
        if (string.IsNullOrWhiteSpace(overflowValue))
            return false;

        var normalized = overflowValue.Trim().ToLowerInvariant();
        return normalized.Contains("hidden") || normalized.Contains("clip");
    }

    private static bool EnablesScrollingBox(string? overflowValue)
    {
        if (string.IsNullOrWhiteSpace(overflowValue))
            return false;

        var value = overflowValue.Trim().ToLowerInvariant();
        return value.Contains("hidden") || value.Contains("scroll") || value.Contains("auto") || value.Contains("clip");
    }

    private (double MinLeft, double MaxLeft, double MinTop, double MaxTop) GetScrollBounds(DomElement element)
    {
        var isRoot = IsViewportElementForMetrics(element);
        var maxLeft = Math.Max(0, GetScrollWidthForDomElement(element, isRoot) - GetClientWidthForDomElement(element, isRoot));
        var maxTop = Math.Max(0, GetScrollHeightForDomElement(element, isRoot) - GetClientHeightForDomElement(element, isRoot));

        var props = GetComputedProps(element);
        var writingMode = props.GetValueOrDefault("writing-mode")?.Trim().ToLowerInvariant();
        var direction = props.GetValueOrDefault("direction");
        var isRtl = string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase);
        var isVertical = IsVerticalWritingMode(writingMode);
        var usesNegativeLeft = (isVertical && writingMode?.EndsWith("-rl", StringComparison.Ordinal) == true)
            || (string.Equals(writingMode, "horizontal-tb", StringComparison.OrdinalIgnoreCase) && isRtl);
        var usesNegativeTop = isVertical && isRtl;

        var minLeft = usesNegativeLeft ? -maxLeft : 0;
        var boundedMaxLeft = usesNegativeLeft ? 0 : maxLeft;
        var minTop = usesNegativeTop ? -maxTop : 0;
        var boundedMaxTop = usesNegativeTop ? 0 : maxTop;
        return (minLeft, boundedMaxLeft, minTop, boundedMaxTop);
    }

    private bool CanProgrammaticallyScrollSelectListBox(DomElement element, bool vertical)
    {
        var props = GetComputedProps(element);
        bool verticalWritingMode = IsVerticalWritingMode(props.GetValueOrDefault("writing-mode"));
        bool blockAxisIsVertical = !verticalWritingMode;
        if (vertical != blockAxisIsVertical)
            return false;

        double clientExtent = vertical ? GetClientHeightForDomElement(element, isRoot: false) : GetClientWidthForDomElement(element, isRoot: false);
        double scrollExtent = vertical ? GetScrollHeightForDomElement(element, isRoot: false) : GetScrollWidthForDomElement(element, isRoot: false);
        return scrollExtent > clientExtent + 0.5;
    }

    private bool TryGetSelectListBoxScrollExtent(DomElement element, bool verticalAxis, out double extent)
    {
        if (!IsSelectListBox(element))
        {
            extent = 0;
            return false;
        }

        var props = GetComputedProps(element);
        bool verticalWritingMode = IsVerticalWritingMode(props.GetValueOrDefault("writing-mode"));
        int optionCount = Math.Max(1, CountSelectOptions(element));
        double rowExtent = Math.Max(16, ResolveLineHeightForElement(element));
        double clientInlineExtent = verticalWritingMode
            ? GetClientHeightForDomElement(element, isRoot: false)
            : GetClientWidthForDomElement(element, isRoot: false);
        double clientBlockExtent = verticalWritingMode
            ? GetClientWidthForDomElement(element, isRoot: false)
            : GetClientHeightForDomElement(element, isRoot: false);
        double totalBlockExtent = Math.Max(clientBlockExtent, optionCount * rowExtent);

        extent = verticalAxis
            ? (verticalWritingMode ? clientInlineExtent : totalBlockExtent)
            : (verticalWritingMode ? totalBlockExtent : clientInlineExtent);
        return true;
    }

    private static int CountSelectOptions(DomElement element)
    {
        int count = 0;
        foreach (var child in element.Children.Where(c => !c.IsTextNode))
        {
            if (string.Equals(child.TagName, "option", StringComparison.OrdinalIgnoreCase))
            {
                count++;
                continue;
            }

            count += CountSelectOptions(child);
        }

        return count;
    }

    private static JSObject CreateSvgLengthValue(double numericValue)
    {
        var svgLength = new JSObject();
        svgLength.FastAddValue((KeyString)"value", new JSNumber(numericValue), JSPropertyAttributes.EnumerableConfigurableValue);
        svgLength.FastAddValue((KeyString)"valueInSpecifiedUnits", new JSNumber(numericValue), JSPropertyAttributes.EnumerableConfigurableValue);
        svgLength.FastAddValue((KeyString)"unitType", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        svgLength.FastAddValue((KeyString)"SVG_LENGTHTYPE_UNKNOWN", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        svgLength.FastAddValue((KeyString)"SVG_LENGTHTYPE_NUMBER", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        svgLength.FastAddValue((KeyString)"SVG_LENGTHTYPE_PERCENTAGE", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        svgLength.FastAddValue((KeyString)"SVG_LENGTHTYPE_EMS", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);
        svgLength.FastAddValue((KeyString)"SVG_LENGTHTYPE_EXS", new JSNumber(4), JSPropertyAttributes.EnumerableConfigurableValue);
        svgLength.FastAddValue((KeyString)"SVG_LENGTHTYPE_PX", new JSNumber(5), JSPropertyAttributes.EnumerableConfigurableValue);
        svgLength.FastAddValue((KeyString)"SVG_LENGTHTYPE_CM", new JSNumber(6), JSPropertyAttributes.EnumerableConfigurableValue);
        svgLength.FastAddValue((KeyString)"SVG_LENGTHTYPE_MM", new JSNumber(7), JSPropertyAttributes.EnumerableConfigurableValue);
        svgLength.FastAddValue((KeyString)"SVG_LENGTHTYPE_IN", new JSNumber(8), JSPropertyAttributes.EnumerableConfigurableValue);
        svgLength.FastAddValue((KeyString)"SVG_LENGTHTYPE_PT", new JSNumber(9), JSPropertyAttributes.EnumerableConfigurableValue);
        svgLength.FastAddValue((KeyString)"SVG_LENGTHTYPE_PC", new JSNumber(10), JSPropertyAttributes.EnumerableConfigurableValue);
        return svgLength;
    }

    private static List<DomElement> CollectSelectOptions(DomElement element)
    {
        var options = new List<DomElement>();
        foreach (var child in element.Children.Where(c => !c.IsTextNode))
        {
            if (string.Equals(child.TagName, "option", StringComparison.OrdinalIgnoreCase))
            {
                options.Add(child);
                continue;
            }

            options.AddRange(CollectSelectOptions(child));
        }

        return options;
    }

    private static int GetSelectSelectedIndex(DomElement element)
    {
        var options = CollectSelectOptions(element);
        if (options.Count == 0)
            return -1;

        if (GetElementRuntimeState(element).FormControl.SelectedIndex.TryGet(out var explicitIndex) &&
            explicitIndex is int dirtyIndex)
        {
            return dirtyIndex >= 0 && dirtyIndex < options.Count ? dirtyIndex : -1;
        }

        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            if (option.Attributes.ContainsKey("selected") ||
                (GetElementRuntimeState(option).FormControl.DefaultSelected.TryGet(out var defaultSelected) && defaultSelected is true))
            {
                return index;
            }
        }

        return 0;
    }

    private static void SetSelectSelectedIndex(DomElement element, int index)
    {
        var options = CollectSelectOptions(element);
        if (options.Count == 0)
        {
            GetElementRuntimeState(element).FormControl.SelectedIndex.Set(-1);
            return;
        }

        if (index < 0 || index >= options.Count)
            index = -1;

        GetElementRuntimeState(element).FormControl.SelectedIndex.Set(index);
    }

    private static string GetSelectValue(DomElement element)
    {
        var options = CollectSelectOptions(element);
        var selectedIndex = GetSelectSelectedIndex(element);
        if (selectedIndex < 0 || selectedIndex >= options.Count)
            return string.Empty;

        var option = options[selectedIndex];
        if (GetElementRuntimeState(option).FormControl.Value.TryGet(out var domValue) && domValue is string stringValue)
            return stringValue;

        if (option.Attributes.TryGetValue("value", out var attrValue))
            return attrValue;

        return option.TextContent;
    }

    private static void SetSelectValue(DomElement element, string value)
    {
        var options = CollectSelectOptions(element);
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            var optionValue = option.Attributes.TryGetValue("value", out var attrValue)
                ? attrValue
                : option.TextContent;
            if (string.Equals(optionValue, value, StringComparison.Ordinal))
            {
                GetElementRuntimeState(element).FormControl.SelectedIndex.Set(index);
                return;
            }
        }

        GetElementRuntimeState(element).FormControl.SelectedIndex.Set(-1);
    }

    private IEnumerable<DomElement> EnumerateRenderedDescendants(DomElement element)
    {
        foreach (var child in EnumerateRenderedChildren(element))
        {
            yield return child;

            foreach (var descendant in EnumerateRenderedDescendants(child))
                yield return descendant;
        }
    }

    private IEnumerable<DomElement> EnumerateRenderedChildren(DomElement element)
    {
        if (string.Equals(element.TagName, "slot", StringComparison.OrdinalIgnoreCase))
        {
            var host = GetSlotHost(element);
            if (host == null)
                yield break;

            foreach (var child in host.Children)
            {
                if (!child.IsTextNode && SlotAcceptsNode(element, child))
                    yield return child;
            }

            yield break;
        }

        foreach (var child in element.Children)
        {
            if (!child.IsTextNode)
                yield return child;
        }
    }

    private DomElement? FindScrollContainer(DomElement element)
    {
        var documentElement = GetOwningDocumentElement(element);
        for (var current = GetScrollTraversalParent(element); current != null; current = GetScrollTraversalParent(current))
        {
            if (ReferenceEquals(current, documentElement))
                return documentElement;

            if (IsViewportBodyElement(current, documentElement))
                continue;

            var props = GetComputedProps(current);
            if (HasOverflowClipping(props))
                return current;
        }

        return documentElement;
    }

    private bool HasFixedPositionAncestorBefore(DomElement element, DomElement ancestor)
    {
        for (var current = GetScrollTraversalParent(element);
             current != null && !ReferenceEquals(current, ancestor);
             current = GetScrollTraversalParent(current))
        {
            var props = GetComputedProps(current);
            if (string.Equals(props.GetValueOrDefault("position"), "fixed", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsDocumentElement(DomElement element) =>
        string.Equals(element.TagName, "html", StringComparison.OrdinalIgnoreCase);

    private bool IsViewportElementForMetrics(DomElement element)
    {
        var documentElement = GetOwningDocumentElement(element);
        return IsDocumentElement(element) || IsViewportBodyElement(element, documentElement);
    }

    private static bool IsViewportBodyElement(DomElement element, DomElement documentElement) =>
        string.Equals(element.TagName, "body", StringComparison.OrdinalIgnoreCase) &&
        ReferenceEquals(element.Parent, documentElement);

    private DomElement GetOwningDocumentElement(DomElement element)
    {
        for (var current = element; current != null; current = current.Parent!)
        {
            if (string.Equals(current.TagName, "html", StringComparison.OrdinalIgnoreCase))
                return current;
        }

        return DocumentElement;
    }

    private bool HasFixedPositionInDocument(DomElement element, DomElement documentElement)
    {
        if (IsFixedPositionElement(element))
            return true;

        return HasFixedPositionAncestorBefore(element, documentElement);
    }

    private bool IsFixedPositionElement(DomElement element)
    {
        var props = GetComputedProps(element);
        return string.Equals(props.GetValueOrDefault("position"), "fixed", StringComparison.OrdinalIgnoreCase);
    }

    private static DomElement? GetOuterFrameElement(DomElement documentElement)
    {
        var docRoot = documentElement.Parent;
        return docRoot != null &&
               string.Equals(docRoot.TagName, "#subdoc-root", StringComparison.OrdinalIgnoreCase)
            ? docRoot.Parent
            : null;
    }

    private DomElement? GetOuterScrollContinuationElement(DomElement scrollContainer)
    {
        if (IsDocumentElement(scrollContainer))
            return GetOuterFrameElement(scrollContainer);

        return scrollContainer;
    }

    private double ComputeOffsetWithinAncestor(DomElement element, DomElement ancestor, bool vertical)
    {
        double offset = 0;
        var current = element;

        while (true)
        {
            var parent = GetScrollTraversalParent(current);
            if (parent == null || ReferenceEquals(parent, ancestor))
                break;

            offset += ComputeOffsetWithinScrollTraversalParent(current, parent, vertical);
            offset -= GetElementScrollOffset(parent, vertical);
            current = parent;
        }

        var directParent = GetScrollTraversalParent(current);
        if (directParent != null && ReferenceEquals(directParent, ancestor))
            offset += ComputeOffsetWithinScrollTraversalParent(current, directParent, vertical);

        return offset;
    }

    private double ComputeOffsetWithinScrollTraversalParent(DomElement element, DomElement parent, bool vertical)
    {
        var assignedSlot = GetAssignedSlot(element);
        if (assignedSlot != null && ReferenceEquals(assignedSlot, parent))
        {
            var host = element.Parent;
            if (host == null)
                return 0;

            var slotProps = GetComputedProps(parent);
            var slotPadding = ParseCssLengthToPixelsWithViewport(
                slotProps.GetValueOrDefault(vertical ? "padding-top" : "padding-left"),
                parent);
            return slotPadding + ComputeOffsetWithinActualAncestor(element, host, vertical);
        }

        return ComputeOffsetWithinParent(element, vertical);
    }

    private double ComputeOffsetWithinParent(DomElement element, bool vertical)
    {
        if (element.Parent == null)
            return 0;

        var parentProps = GetComputedProps(element.Parent);
        var elementProps = GetComputedProps(element);
        var position = elementProps.GetValueOrDefault("position");
        double offset = ParseCssLengthToPixelsWithViewport(
            parentProps.GetValueOrDefault(vertical ? "padding-top" : "padding-left"), element.Parent);
        offset += ParseCssLengthToPixelsWithViewport(
            elementProps.GetValueOrDefault(vertical ? "margin-top" : "margin-left"), element);

        if (!string.Equals(position, "absolute", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var sibling in element.Parent.Children)
            {
                if (ReferenceEquals(sibling, element))
                    break;
                if (sibling.IsTextNode)
                    continue;

                var siblingProps = GetComputedProps(sibling);
                var siblingPosition = siblingProps.GetValueOrDefault("position");
                if (string.Equals(siblingPosition, "absolute", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(siblingPosition, "fixed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                offset += ParseCssLengthToPixelsWithViewport(
                    siblingProps.GetValueOrDefault(vertical ? "margin-top" : "margin-left"), sibling);
                offset += ParseCssLengthToPixelsWithViewport(
                    siblingProps.GetValueOrDefault(vertical ? "height" : "width"), sibling);
                offset += ParseCssLengthToPixelsWithViewport(
                    siblingProps.GetValueOrDefault(vertical ? "margin-bottom" : "margin-right"), sibling);
            }
        }

        if (string.Equals(position, "absolute", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(position, "relative", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(position, "fixed", StringComparison.OrdinalIgnoreCase))
        {
            offset += ResolvePositionedInset(element, vertical);
        }

        return offset;
    }

    private double ResolveScrollIntoViewOffset(
        DomElement element,
        DomElement scrollContainer,
        bool vertical,
        string? alignment,
        double? viewportSizeOverride = null,
        double? currentScrollOverride = null,
        double? offsetOverride = null,
        bool coordinateSpaceIsPhysical = false)
    {
        var normalizedAlignment = NormalizeScrollIntoViewAlignment(alignment, "start");
        var offset = offsetOverride ?? ComputeOffsetWithinAncestor(element, scrollContainer, vertical);
        var targetSize = vertical ? GetBorderBoxHeight(GetComputedProps(element), element) : GetBorderBoxWidth(GetComputedProps(element), element);
        var (marginStart, marginStartOwner) = ResolveScrollIntoViewInset(element, vertical ? "scroll-margin-top" : "scroll-margin-left");
        var (marginEnd, marginEndOwner) = ResolveScrollIntoViewInset(element, vertical ? "scroll-margin-bottom" : "scroll-margin-right");
        var (paddingStart, paddingStartOwner) = ResolveScrollIntoViewInset(scrollContainer, vertical ? "scroll-padding-top" : "scroll-padding-left");
        var (paddingEnd, paddingEndOwner) = ResolveScrollIntoViewInset(scrollContainer, vertical ? "scroll-padding-bottom" : "scroll-padding-right");
        marginStart = ConvertInsetToScrollContainerCoordinates(marginStart, marginStartOwner, scrollContainer);
        marginEnd = ConvertInsetToScrollContainerCoordinates(marginEnd, marginEndOwner, scrollContainer);
        paddingStart = ConvertInsetToScrollContainerCoordinates(paddingStart, paddingStartOwner, scrollContainer);
        paddingEnd = ConvertInsetToScrollContainerCoordinates(paddingEnd, paddingEndOwner, scrollContainer);
        var viewportSize = viewportSizeOverride ?? (vertical
            ? GetClientHeightForDomElement(scrollContainer, IsDocumentElement(scrollContainer))
            : GetClientWidthForDomElement(scrollContainer, IsDocumentElement(scrollContainer)));
        var currentScroll = currentScrollOverride ?? GetElementScrollOffset(scrollContainer, vertical);
        var physicalCurrentScroll = coordinateSpaceIsPhysical
            ? currentScroll
            : ConvertScrollCoordinateToPhysicalPosition(scrollContainer, vertical, currentScroll);

        var startTarget = offset - marginStart - paddingStart;
        var endTarget = offset + targetSize + marginEnd + paddingEnd - viewportSize;
        if (normalizedAlignment == "start")
            return ConvertPhysicalScrollPosition(scrollContainer, vertical, startTarget, coordinateSpaceIsPhysical);
        if (normalizedAlignment == "end")
            return ConvertPhysicalScrollPosition(scrollContainer, vertical, endTarget, coordinateSpaceIsPhysical);

        var alignmentViewportSize = Math.Max(0, viewportSize - paddingStart - paddingEnd);
        if (normalizedAlignment == "center")
        {
            var targetCenter = offset + ((targetSize + marginEnd - marginStart) / 2.0);
            return ConvertPhysicalScrollPosition(
                scrollContainer,
                vertical,
                targetCenter - paddingStart - (alignmentViewportSize / 2.0),
                coordinateSpaceIsPhysical);
        }

        var visibleStart = physicalCurrentScroll + paddingStart;
        var visibleEnd = physicalCurrentScroll + viewportSize - paddingEnd;
        var targetStart = offset - marginStart;
        var targetEnd = offset + targetSize + marginEnd;

        if (targetStart >= visibleStart && targetEnd <= visibleEnd)
            return currentScroll;

        if (normalizedAlignment == "start-if-needed")
            return ConvertPhysicalScrollPosition(scrollContainer, vertical, startTarget, coordinateSpaceIsPhysical);
        if (normalizedAlignment == "end-if-needed")
            return ConvertPhysicalScrollPosition(scrollContainer, vertical, endTarget, coordinateSpaceIsPhysical);

        if (targetSize + marginStart + marginEnd > alignmentViewportSize)
        {
            var chosenTarget = Math.Abs(startTarget - physicalCurrentScroll) <= Math.Abs(endTarget - physicalCurrentScroll)
                ? startTarget
                : endTarget;
            return ConvertPhysicalScrollPosition(scrollContainer, vertical, chosenTarget, coordinateSpaceIsPhysical);
        }

        if (targetStart < visibleStart)
            return ConvertPhysicalScrollPosition(scrollContainer, vertical, startTarget, coordinateSpaceIsPhysical);
        if (targetEnd > visibleEnd)
            return ConvertPhysicalScrollPosition(scrollContainer, vertical, endTarget, coordinateSpaceIsPhysical);

        return currentScroll;
    }

    private double ConvertPhysicalScrollPosition(
        DomElement scrollContainer,
        bool vertical,
        double physicalPosition,
        bool coordinateSpaceIsPhysical)
        => coordinateSpaceIsPhysical
            ? physicalPosition
            : ConvertPhysicalPositionToScrollCoordinate(scrollContainer, vertical, physicalPosition);

    private double ConvertScrollCoordinateToPhysicalPosition(DomElement scrollContainer, bool vertical, double coordinate)
    {
        if (!UsesNegativeScrollCoordinates(scrollContainer, vertical))
            return coordinate;

        return coordinate + GetPositiveScrollExtent(scrollContainer, vertical);
    }

    private double ConvertPhysicalPositionToScrollCoordinate(DomElement scrollContainer, bool vertical, double physicalPosition)
    {
        if (!UsesNegativeScrollCoordinates(scrollContainer, vertical))
            return physicalPosition;

        return physicalPosition - GetPositiveScrollExtent(scrollContainer, vertical);
    }

    private bool UsesNegativeScrollCoordinates(DomElement scrollContainer, bool vertical)
    {
        var (minLeft, _, minTop, _) = GetScrollBounds(scrollContainer);
        return vertical ? minTop < 0 : minLeft < 0;
    }

    private double GetPositiveScrollExtent(DomElement scrollContainer, bool vertical)
    {
        var (minLeft, maxLeft, minTop, maxTop) = GetScrollBounds(scrollContainer);
        return vertical
            ? (minTop < 0 ? maxTop - minTop : maxTop)
            : (minLeft < 0 ? maxLeft - minLeft : maxLeft);
    }

    private (double Value, DomElement Owner) ResolveScrollIntoViewInset(DomElement element, string propertyName)
    {
        var props = GetComputedProps(element);
        var value = props.GetValueOrDefault(propertyName);
        if (string.Equals(value, "inherit", StringComparison.OrdinalIgnoreCase) && element.Parent != null)
            return ResolveScrollIntoViewInset(element.Parent, propertyName);

        return (ParseCssLengthToPixelsWithViewport(value, element), element);
    }

    private double ConvertInsetToScrollContainerCoordinates(double inset, DomElement insetOwner, DomElement scrollContainer)
    {
        if (!double.IsFinite(inset) || AreClose(inset, 0))
            return 0;

        var ownerZoom = GetUsedZoomForElement(insetOwner);
        var containerZoom = GetUsedZoomForElement(scrollContainer);
        if (!double.IsFinite(ownerZoom) || ownerZoom <= 0 ||
            !double.IsFinite(containerZoom) || containerZoom <= 0)
        {
            return inset;
        }

        return inset * (ownerZoom / containerZoom);
    }

    private double ResolvePositionedInset(DomElement element, bool vertical)
    {
        if (element.Parent == null)
            return 0;

        var props = GetComputedProps(element);
        var primaryProperty = vertical ? "top" : "left";
        var secondaryProperty = vertical ? "bottom" : "right";
        var value = props.GetValueOrDefault(primaryProperty);
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
        {
            var fallback = props.GetValueOrDefault(secondaryProperty);
            if (string.IsNullOrWhiteSpace(fallback) ||
                string.Equals(fallback, "auto", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var reference = ResolveContainingBlockReferenceLength(element, vertical);
            var borderBoxSize = vertical
                ? GetBorderBoxHeight(props, element)
                : GetBorderBoxWidth(props, element);
            var fallbackPixels = ParseCssLengthToPixelsWithViewport(fallback, element, percentageBasis: reference);
            return Math.Max(0, reference - borderBoxSize - fallbackPixels);
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.EndsWith("%"))
        {
            if (!double.TryParse(normalized[..^1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var percent))
            {
                return 0;
            }

            var parentProps = GetComputedProps(element.Parent);
            var reference = ParseCssLengthToPixelsWithViewport(parentProps.GetValueOrDefault(vertical ? "height" : "width"), element.Parent);
            return reference <= 0 ? 0 : reference * (percent / 100.0);
        }

        return ParseCssLengthToPixelsWithViewport(value, element);
    }

    private double ParseCssLengthToPixelsWithViewport(
        string? value,
        DomElement? referenceElement = null,
        bool forLineHeight = false,
        double? percentageBasis = null,
        bool forFontSize = false)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        return TryEvaluateCssLengthWithViewport(value, referenceElement, forLineHeight, percentageBasis, out var px, forFontSize)
            ? px
            : 0;
    }

    private bool TryEvaluateCssLengthWithViewport(
        string value,
        DomElement? referenceElement,
        bool forLineHeight,
        double? percentageBasis,
        out double result,
        bool forFontSize = false)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        while (normalized.Length >= 2 &&
               normalized[0] == '(' &&
               normalized[^1] == ')' &&
               HasBalancedParens(normalized[1..^1]))
        {
            normalized = normalized[1..^1].Trim();
        }

        if (TryEvaluateMathLengthFunction(normalized, referenceElement, forLineHeight, percentageBasis, out result, forFontSize))
            return true;

        var additiveOperatorIndex = FindTopLevelAdditiveOperator(normalized);
        if (additiveOperatorIndex > 0)
        {
            if (!TryEvaluateCssLengthWithViewport(
                    normalized[..additiveOperatorIndex],
                    referenceElement,
                    forLineHeight,
                    percentageBasis,
                    out var left,
                    forFontSize) ||
                !TryEvaluateCssLengthWithViewport(
                    normalized[(additiveOperatorIndex + 1)..],
                    referenceElement,
                    forLineHeight,
                    percentageBasis,
                    out var right,
                    forFontSize))
            {
                return false;
            }

            result = normalized[additiveOperatorIndex] == '+'
                ? left + right
                : left - right;
            return true;
        }

        var lower = normalized.ToLowerInvariant();
        if (percentageBasis.HasValue &&
            lower.EndsWith("%", StringComparison.Ordinal) &&
            double.TryParse(lower[..^1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var percent))
        {
            result = percentageBasis.Value * (percent / 100.0);
            return true;
        }

        if (referenceElement != null &&
            lower.EndsWith("rem") &&
            double.TryParse(lower[..^3], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var rem))
        {
            result = rem * ResolveFontSizeForLength(referenceElement, rootRelative: true);
            return true;
        }

        if (referenceElement != null &&
            lower.EndsWith("em") &&
            double.TryParse(lower[..^2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var em))
        {
            // For the font-size property itself, em resolves against the parent's
            // font-size (not the element's own), otherwise resolving the element's
            // font-size would recurse into itself.
            double emBasis;
            if (forFontSize)
            {
                var parent = referenceElement.Parent;
                emBasis = parent != null ? ResolveFontSizeForElement(parent) : 16;
            }
            else
            {
                emBasis = ResolveFontSizeForLength(referenceElement, rootRelative: false);
            }

            result = em * emBasis;
            return true;
        }

        if (referenceElement != null &&
            lower.EndsWith("rlh") &&
            double.TryParse(lower[..^3], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var rlh))
        {
            result = rlh * ResolveLineHeightForLength(referenceElement, rootRelative: true);
            return true;
        }

        if (referenceElement != null &&
            lower.EndsWith("lh") &&
            double.TryParse(lower[..^2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lh))
        {
            result = lh * ResolveLineHeightForLength(referenceElement, rootRelative: false, forLineHeight);
            return true;
        }

        var px = ParseCssLengthToPixels(normalized, _viewportWidth, _viewportHeight);
        if (double.IsNaN(px))
            return false;

        result = px;
        return true;
    }

    private bool TryEvaluateMathLengthFunction(
        string value,
        DomElement? referenceElement,
        bool forLineHeight,
        double? percentageBasis,
        out double result,
        bool forFontSize = false)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value) || value[^1] != ')')
            return false;

        static bool StartsWithFunction(string candidate, string functionName)
            => candidate.StartsWith(functionName + "(", StringComparison.OrdinalIgnoreCase);

        if (StartsWithFunction(value, "calc"))
        {
            var content = value[5..^1];
            return HasBalancedParens(content) &&
                   TryEvaluateCssLengthWithViewport(content, referenceElement, forLineHeight, percentageBasis, out result, forFontSize);
        }

        if (!StartsWithFunction(value, "min") && !StartsWithFunction(value, "max"))
            return false;

        var isMax = StartsWithFunction(value, "max");
        var contentValue = value[4..^1];
        if (!HasBalancedParens(contentValue))
            return false;

        var parts = SplitTopLevelArguments(contentValue);
        if (parts.Count == 0)
            return false;

        double? candidate = null;
        foreach (var part in parts)
        {
            if (!TryEvaluateCssLengthWithViewport(part, referenceElement, forLineHeight, percentageBasis, out var parsed, forFontSize))
                return false;

            candidate = candidate.HasValue
                ? (isMax ? Math.Max(candidate.Value, parsed) : Math.Min(candidate.Value, parsed))
                : parsed;
        }

        if (!candidate.HasValue)
            return false;

        result = candidate.Value;
        return true;
    }

    private static int FindTopLevelAdditiveOperator(string expression)
    {
        var depth = 0;
        for (int i = expression.Length - 1; i >= 1; i--)
        {
            switch (expression[i])
            {
                case ')':
                    depth++;
                    break;
                case '(':
                    depth--;
                    break;
                case '+':
                case '-':
                    if (depth != 0)
                        break;

                    var leftIndex = i - 1;
                    while (leftIndex >= 0 && char.IsWhiteSpace(expression[leftIndex]))
                        leftIndex--;

                    var rightIndex = i + 1;
                    while (rightIndex < expression.Length && char.IsWhiteSpace(expression[rightIndex]))
                        rightIndex++;

                    if (leftIndex >= 0 &&
                        rightIndex < expression.Length &&
                        expression[leftIndex] != '(' &&
                        expression[leftIndex] != ',' &&
                        expression[leftIndex] != '+' &&
                        expression[leftIndex] != '-')
                    {
                        return i;
                    }
                    break;
            }
        }

        return -1;
    }

    private static List<string> SplitTopLevelArguments(string value)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (int i = 0; i < value.Length; i++)
        {
            switch (value[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth < 0)
                        return [];
                    break;
                case ',' when depth == 0:
                    parts.Add(value[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }

        if (depth != 0)
            return [];

        parts.Add(value[start..].Trim());
        return parts;
    }

    private double ResolveContainingBlockReferenceLength(DomElement element, bool vertical)
    {
        if (element.Parent == null ||
            string.Equals(element.TagName, "html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(element.TagName, "body", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(element.Parent.TagName, "html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(element.Parent.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            return GetViewportReferenceLength(element, vertical);
        }

        var parentRect = ComputeUnzoomedLayoutRect(element.Parent);
        var reference = vertical ? parentRect.Height : parentRect.Width;
        return reference > 0 ? reference : GetViewportReferenceLength(element, vertical);
    }

    private double GetViewportReferenceLength(DomElement? element, bool vertical)
    {
        if (element != null)
        {
            var documentElement = GetOwningDocumentElement(element);
            var frameElement = GetOuterFrameElement(documentElement);
            if (frameElement != null)
            {
                var frameProps = GetComputedProps(frameElement);
                var frameLength = ParseCssLengthToPixelsWithViewport(
                    frameProps.GetValueOrDefault(vertical ? "height" : "width"),
                    frameElement);
                if (frameLength > 0)
                    return frameLength;

                if (frameElement.Attributes.TryGetValue(vertical ? "height" : "width", out var frameAttribute) &&
                    double.TryParse(frameAttribute, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out frameLength) &&
                    frameLength > 0)
                {
                    return frameLength;
                }
            }
        }

        return vertical ? _viewportHeight : _viewportWidth;
    }

    private static bool HasExplicitBodyMargin(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase);
    }

    private double ResolveLineHeightForLength(DomElement element, bool rootRelative, bool forLineHeight = false)
    {
        var target = rootRelative ? GetRootElement(element) : (forLineHeight ? element.Parent ?? element : element);
        return ResolveLineHeightForElement(target);
    }

    private double ResolveFontSizeForLength(DomElement element, bool rootRelative)
    {
        var target = rootRelative ? GetRootElement(element) : element;
        return ResolveFontSizeForElement(target);
    }

    private DomElement GetRootElement(DomElement element)
    {
        DomElement? htmlElement = null;
        var current = element;
        while (current.Parent != null)
        {
            current = current.Parent;
            if (string.Equals(current.TagName, "html", StringComparison.OrdinalIgnoreCase))
                htmlElement = current;
        }

        return htmlElement ?? current;
    }

    private double ResolveLineHeightForElement(DomElement element)
    {
        var props = GetComputedProps(element);
        var fontSize = ResolveFontSizeForElement(element);
        var lineHeight = props.GetValueOrDefault("line-height");
        if (string.IsNullOrWhiteSpace(lineHeight) ||
            string.Equals(lineHeight, "normal", StringComparison.OrdinalIgnoreCase))
        {
            return fontSize * 1.2;
        }

        var normalized = lineHeight.Trim().ToLowerInvariant();
        if (double.TryParse(normalized, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var multiplier))
        {
            return fontSize * multiplier;
        }

        return ParseCssLengthToPixelsWithViewport(lineHeight, element, forLineHeight: true);
    }

    private double ResolveFontSizeForElement(DomElement element)
    {
        var props = GetComputedProps(element);
        var fontSize = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("font-size"), element, forFontSize: true);
        if (fontSize > 0)
            return fontSize;

        for (var current = element; current != null; current = current.Parent)
        {
            if (!current.Attributes.TryGetValue("font-size", out var attributeValue) ||
                string.IsNullOrWhiteSpace(attributeValue))
            {
                continue;
            }

            var attributeFontSize = ParseCssLengthToPixelsWithViewport(attributeValue, current, forFontSize: true);
            if (attributeFontSize > 0)
                return attributeFontSize;
        }

        return 16;
    }

}
