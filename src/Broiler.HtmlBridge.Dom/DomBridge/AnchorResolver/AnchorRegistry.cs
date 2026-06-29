using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // Anchor registry
    // -----------------------------------------------------------------

    private sealed record AnchorInfo(
        double Top, double Left, double Width, double Height,
        DomElement? SourceElement = null)
    {
        public double Right => Left + Width;
        public double Bottom => Top + Height;
    }
    private void BuildAnchorRegistry(Dictionary<string, AnchorInfo> registry)
    {
        foreach (var el in Elements)
        {
            if (el.IsTextNode)
                continue;

            var declarations = CollectMatchedRuleProperties(el);
            if (!declarations.TryGetValue("anchor-name", out var anchorName))
                continue;

            var box = ComputeElementBox(el);
            if (box != null)
                registry[anchorName] = box with { SourceElement = el };
        }
    }
    private AnchorInfo? ComputeElementBox(DomElement element)
    {
        var props = GetComputedProps(element);

        double width = TryParsePx(props.GetValueOrDefault("width")) ?? 0;
        double height = TryParsePx(props.GetValueOrDefault("height")) ?? 0;

        string? position = props.GetValueOrDefault("position");
        bool isPositioned = position == "absolute" || position == "fixed";

        // For absolutely positioned elements, use their explicit insets.
        if (isPositioned)
        {
            double? topPx = TryParsePx(props.GetValueOrDefault("top"));
            double? leftPx = TryParsePx(props.GetValueOrDefault("left"));
            double? rightPx = TryParsePx(props.GetValueOrDefault("right"));
            double? bottomPx = TryParsePx(props.GetValueOrDefault("bottom"));

            double top = topPx ?? 0;
            double left = leftPx ?? 0;

            // When both left and right are specified without explicit width,
            // derive width from the containing block dimensions.
            if (width == 0 && leftPx.HasValue && rightPx.HasValue)
            {
                double cbW = FindContainingBlockWidth(element);
                width = cbW - leftPx.Value - rightPx.Value;
                if (width < 0) width = 0;
            }

            // When both top and bottom are specified without explicit height,
            // derive height from the containing block dimensions.
            if (height == 0 && topPx.HasValue && bottomPx.HasValue)
            {
                double cbH = FindContainingBlockHeight(element);
                height = cbH - topPx.Value - bottomPx.Value;
                if (height < 0) height = 0;
            }

            // When only right/bottom are specified, compute left/top from
            // the containing block dimensions.
            if (leftPx == null && rightPx.HasValue)
            {
                double cbW = FindContainingBlockWidth(element);
                left = cbW - rightPx.Value - width;
            }
            if (topPx == null && bottomPx.HasValue)
            {
                double cbH = FindContainingBlockHeight(element);
                top = cbH - bottomPx.Value - height;
            }

            return new AnchorInfo(top, left, width, height);
        }

        // For normal-flow elements, accumulate offsets from margins, padding,
        // borders, and preceding sibling heights up the ancestor chain.
        double marginLeft = TryParsePx(props.GetValueOrDefault("margin-left")) ?? 0;
        double marginTop = TryParsePx(props.GetValueOrDefault("margin-top")) ?? 0;
        double marginRight = TryParsePx(props.GetValueOrDefault("margin-right")) ?? 0;
        ParseMarginShorthand(props, ref marginLeft, ref marginTop, ref marginRight);

        double accLeft = marginLeft;
        double accTop = marginTop;

        // Add height of preceding siblings (vertical stacking in normal flow).
        accTop += ComputePrecedingSiblingHeights(element);

        // Walk up ancestors to accumulate margins, padding, and borders.
        var ancestor = element.Parent;
        while (ancestor != null)
        {
            var ancProps = GetComputedProps(ancestor);

            // Check if ancestor establishes a CB — if so, stop here.
            if (EstablishesContainingBlock(ancProps))
            {
                accLeft += TryParsePx(ancProps.GetValueOrDefault("padding-left")) ?? 0;
                accTop += TryParsePx(ancProps.GetValueOrDefault("padding-top")) ?? 0;
                accLeft += TryParsePx(ancProps.GetValueOrDefault("border-left-width")) ?? 0;
                accTop += TryParsePx(ancProps.GetValueOrDefault("border-top-width")) ?? 0;
                break;
            }

            // Accumulate ancestor margin + padding + border.
            double ancML = TryParsePx(ancProps.GetValueOrDefault("margin-left")) ?? 0;
            double ancMT = TryParsePx(ancProps.GetValueOrDefault("margin-top")) ?? 0;
            double ancMR = 0;
            ParseMarginShorthand(ancProps, ref ancML, ref ancMT, ref ancMR);

            // Apply UA default body margin (8px) if body has no explicit margin.
            if (string.Equals(ancestor.TagName, "body", StringComparison.OrdinalIgnoreCase) &&
                ancML == 0 && ancMT == 0 &&
                !ancProps.ContainsKey("margin") &&
                !ancProps.ContainsKey("margin-left") &&
                !ancProps.ContainsKey("margin-top"))
            {
                ancML = 8;
                ancMT = 8;
                ancMR = 8;
            }

            accLeft += ancML;
            accTop += ancMT;
            accLeft += TryParsePx(ancProps.GetValueOrDefault("padding-left")) ?? 0;
            accTop += TryParsePx(ancProps.GetValueOrDefault("padding-top")) ?? 0;
            accLeft += TryParsePx(ancProps.GetValueOrDefault("border-left-width")) ?? 0;
            accTop += TryParsePx(ancProps.GetValueOrDefault("border-top-width")) ?? 0;
            accTop += ComputePrecedingSiblingHeights(ancestor);

            ancestor = ancestor.Parent;
        }

        // For block-level elements without explicit width, compute width
        // from the containing block content width minus horizontal margins.
        if (width == 0)
        {
            string? display = props.GetValueOrDefault("display");
            bool isInline = display != null &&
                (display.Contains("inline", StringComparison.OrdinalIgnoreCase) &&
                 !display.Contains("inline-block", StringComparison.OrdinalIgnoreCase));

            if (!isInline)
            {
                double cbWidth = FindContainingBlockWidth(element);
                width = cbWidth - marginLeft - marginRight;
                if (width < 0) width = 0;
            }
        }

        return new AnchorInfo(accTop, accLeft, width, height);
    }
    /// <summary>
    /// Parses the 'margin' shorthand into individual margin values,
    /// only overwriting values that are still at their defaults (0).
    /// </summary>
    private static void ParseMarginShorthand(
        Dictionary<string, string> props,
        ref double marginLeft, ref double marginTop, ref double marginRight)
    {
        if (marginLeft == 0 && marginTop == 0 && marginRight == 0 &&
            props.TryGetValue("margin", out var marginShorthand))
        {
            var parts = marginShorthand.Trim().Split(null as char[], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1)
                marginTop = TryParsePx(parts[0]) ?? 0;
            if (parts.Length >= 2)
            {
                marginRight = TryParsePx(parts[1]) ?? 0;
                marginLeft = TryParsePx(parts[1]) ?? 0;
            }
            else if (parts.Length == 1)
                marginLeft = marginRight = marginTop;
            if (parts.Length >= 4)
                marginLeft = TryParsePx(parts[3]) ?? 0;
        }
    }
    /// <summary>
    /// Gets computed CSS properties for an element (CSS rules + inline styles).
    /// </summary>
    private Dictionary<string, string> GetComputedProps(DomElement element)
    {
        if (_computedPropsCache.TryGetValue(element, out var cached))
            return cached;

        if (_computedPropsInProgress.TryGetValue(element, out var inProgress))
            return inProgress;

        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _computedPropsInProgress[element] = props;
        try
        {
            ApplyUserAgentDisplayDefaults(props, element);

            foreach (var kv in CollectMatchedRuleProperties(element))
                props[kv.Key] = kv.Value;
            foreach (var kv in element.Style)
                props[kv.Key] = kv.Value;

            ExpandCssShorthands(props);
            ResolveLengthAttrFunctions(props, element);
            ResolveExplicitInheritedValues(props, element);
            ApplyInheritedProperties(props, element);

            // Expand the inset shorthand → top, right, bottom, left so that
            // downstream code (ComputeElementBox, TryApplyFallback, etc.) can
            // read the individual inset properties directly.
            if (props.TryGetValue("inset", out var insetVal2))
            {
                var parts = insetVal2.Split(new[] { ' ', '\t' },
                    StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    string iTop = parts[0];
                    string iRight = parts.Length > 1 ? parts[1] : iTop;
                    string iBottom = parts.Length > 2 ? parts[2] : iTop;
                    string iLeft = parts.Length > 3 ? parts[3] : iRight;

                    if (!props.ContainsKey("top")) props["top"] = iTop;
                    if (!props.ContainsKey("right")) props["right"] = iRight;
                    if (!props.ContainsKey("bottom")) props["bottom"] = iBottom;
                    if (!props.ContainsKey("left")) props["left"] = iLeft;
                }
            }

            ApplyApproximateFormControlComputedSizes(props, element);
            ApplyLogicalSizeAliases(props);

            _computedPropsCache[element] = props;
            return props;
        }
        finally
        {
            _computedPropsInProgress.Remove(element);
        }
    }
    private void ResolveExplicitInheritedValues(Dictionary<string, string> props, DomElement element)
    {
        Dictionary<string, string>? parentProps = null;
        foreach (var key in props.Keys.ToList())
        {
            var value = props[key];
            if (!string.Equals(value?.Trim(), "inherit", StringComparison.OrdinalIgnoreCase))
                continue;

            if (element.Parent != null)
            {
                parentProps ??= GetComputedProps(element.Parent);
                if (parentProps.TryGetValue(key, out var parentValue) && !string.IsNullOrWhiteSpace(parentValue))
                {
                    props[key] = parentValue;
                    continue;
                }
            }

            if (CssInitialValues.TryGetValue(key, out var initialValue))
                props[key] = initialValue;
            else
                props.Remove(key);
        }
    }
    /// <summary>
    /// Computes the total height of preceding siblings in normal flow.
    /// </summary>
    private double ComputePrecedingSiblingHeights(DomElement element)
    {
        if (element.Parent == null) return 0;

        double totalHeight = 0;
        // Snapshot the sibling list before walking it: element.Parent.Children
        // enumerates the live ChildNodes list, so any mutation of it during the
        // walk (e.g. an anchor-driven DOM move on a node sharing this parent, or
        // a lazy offset query that re-enters anchor resolution) throws
        // "Collection was modified" mid-traversal and aborts the registry build
        // for the whole document (WPT issue #1131, signature
        // DomBridge.BuildAnchorRegistry). Same defensive idiom as the .ToList()
        // walks in ResolveAnchorCenter and InlineContainingBlocks.
        foreach (var sibling in element.Parent.Children.ToList())
        {
            if (sibling == element) break;
            if (sibling.IsTextNode) continue;

            var sibProps = GetComputedProps(sibling);
            string? sibPos = sibProps.GetValueOrDefault("position");
            if (sibPos == "absolute" || sibPos == "fixed") continue;

            double sibHeight = TryParsePx(sibProps.GetValueOrDefault("height")) ?? 0;
            double sibMT = TryParsePx(sibProps.GetValueOrDefault("margin-top")) ?? 0;
            double sibMB = TryParsePx(sibProps.GetValueOrDefault("margin-bottom")) ?? 0;
            double sibMR = 0;
            ParseMarginShorthand(sibProps, ref sibMT, ref sibMB, ref sibMR);

            totalHeight += sibHeight + sibMT + sibMB;
        }
        return totalHeight;
    }
}
