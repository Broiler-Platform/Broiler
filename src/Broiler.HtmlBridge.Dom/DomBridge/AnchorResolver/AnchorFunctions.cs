using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // anchor() resolution
    // -----------------------------------------------------------------

    private static readonly System.Text.RegularExpressions.Regex AnchorFunctionPattern = new(
        @"anchor\(\s*(?:(?<name>--[a-zA-Z0-9_-]+)\s+)?(?<edge>top|right|bottom|left|start|end|center)\s*(?:,\s*(?<fallback>[^)]+?))?\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex AnchorSizeFunctionPattern = new(
        @"anchor-size\(\s*(?:(?<name>--[a-zA-Z0-9_-]+)\s+)?(?<dim>width|height|block|inline|self-block|self-inline)\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private void ResolveAnchorFunctions(
        DomElement element,
        Dictionary<string, AnchorInfo> anchorRegistry)
    {
        var cssProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (selector, _, declarations) in CssRules)
        {
            if (MatchesSelector(element, selector))
                foreach (var kv in declarations)
                    cssProps[kv.Key] = kv.Value;
        }

        bool hasAnchorRef = false;
        bool hasAnchorSizeRef = false;
        foreach (var kv in cssProps)
        {
            if (kv.Value.Contains("anchor(", StringComparison.OrdinalIgnoreCase))
                hasAnchorRef = true;
            if (kv.Value.Contains("anchor-size(", StringComparison.OrdinalIgnoreCase))
                hasAnchorSizeRef = true;
        }
        // Also check inline styles for anchor-size()
        foreach (var kv in element.Style)
        {
            if (kv.Value.Contains("anchor-size(", StringComparison.OrdinalIgnoreCase))
                hasAnchorSizeRef = true;
        }

        if (hasAnchorRef)
        {
            // Need CB dimensions for resolving anchor positions in right/bottom contexts.
            double cbW = FindContainingBlockWidth(element);
            double cbH = FindContainingBlockHeight(element);

            // Get the implicit anchor name from position-anchor.
            string? implicitAnchor = cssProps.GetValueOrDefault("position-anchor") ??
                                     element.Style.GetValueOrDefault("position-anchor");

            // When the target element is fixed-positioned (e.g. top-layer dialog)
            // and the anchor is NOT fixed-positioned, anchor positions must be
            // adjusted by the document scroll offset so the anchor's viewport
            // position is used instead of its document position.
            bool targetIsFixed =
                (cssProps.GetValueOrDefault("position") ?? element.Style.GetValueOrDefault("position")) == "fixed" ||
                (GetElementRuntimeState(element).Dialog.Modal.TryGet(out var tModal) && tModal is true);
            double scrollAdjY = 0, scrollAdjX = 0;
            if (targetIsFixed)
            {
                var docEl = DocumentElement;
                if (GetElementRuntimeState(docEl).Scroll.Top.TryGet(out var stv) && stv is double scrollTop)
                    scrollAdjY = scrollTop;
                if (GetElementRuntimeState(docEl).Scroll.Left.TryGet(out var slv) && slv is double scrollLeft)
                    scrollAdjX = scrollLeft;
            }

            foreach (var kv in cssProps)
            {
                var propName = kv.Key.ToLowerInvariant();
                var resolved = AnchorFunctionPattern.Replace(kv.Value, m =>
                {
                    var anchorName = m.Groups["name"].Value;
                    if (string.IsNullOrEmpty(anchorName))
                        anchorName = implicitAnchor ?? string.Empty;
                    var edge = m.Groups["edge"].Value.ToLowerInvariant();
                    var fallback = m.Groups["fallback"].Success
                        ? m.Groups["fallback"].Value.Trim()
                        : null;

                    if (!anchorRegistry.TryGetValue(anchorName, out var anchor) ||
                        !IsAnchorAccessible(anchor.SourceElement, element))
                    {
                        // Anchor not found or not accessible — use fallback or 0px.
                        return fallback ?? "0px";
                    }

                    // Compute the raw edge position (from CB origin).
                    // When the target is fixed and the anchor is not fixed,
                    // adjust for document scroll to get viewport position.
                    // Use only the CSS computed position to determine if the
                    // anchor is fixed — modal dialogs with position:absolute
                    // are still shifted by scroll simulation and need adjustment.
                    bool anchorIsFixed = anchor.SourceElement != null &&
                        GetComputedProps(anchor.SourceElement).GetValueOrDefault("position") == "fixed";
                    double adjY = anchorIsFixed ? 0 : scrollAdjY;
                    double adjX = anchorIsFixed ? 0 : scrollAdjX;

                    double rawValue = edge switch
                    {
                        "top" => anchor.Top - adjY,
                        "right" => anchor.Right - adjX,
                        "bottom" => anchor.Bottom - adjY,
                        "left" => anchor.Left - adjX,
                        "center" => (anchor.Top + anchor.Bottom) / 2 - adjY,
                        _ => 0,
                    };

                    // For right/bottom inset properties, anchor() returns
                    // the distance from the CB's opposite edge.
                    double value = propName switch
                    {
                        "right" => cbW - rawValue,
                        "bottom" => cbH - rawValue,
                        _ => rawValue,
                    };

                    return $"{value.ToString(CultureInfo.InvariantCulture)}px";
                });

                if (resolved != kv.Value)
                    element.Style[kv.Key] = resolved;
            }

            // Apply non-anchor CSS properties (e.g. position, margin).
            foreach (var kv in cssProps)
            {
                if (!kv.Value.Contains("anchor(", StringComparison.OrdinalIgnoreCase) &&
                    !kv.Value.Contains("anchor-size(", StringComparison.OrdinalIgnoreCase) &&
                    !element.Style.ContainsKey(kv.Key) &&
                    IsLayoutProperty(kv.Key))
                {
                    element.Style[kv.Key] = kv.Value;
                }
            }

            // Remove 'inset' shorthand.
            element.Style.Remove("inset");
        }

        // Resolve anchor-size() function calls in both CSS and inline styles.
        if (hasAnchorSizeRef)
        {
            ResolveAnchorSizeFunctions(element, cssProps, anchorRegistry);
        }

        foreach (var child in element.Children)
            ResolveAnchorFunctions(child, anchorRegistry);
    }
    private static bool IsLayoutProperty(string prop) => prop switch
    {
        "position" or "top" or "right" or "bottom" or "left"
            or "margin" or "margin-top" or "margin-right"
            or "margin-bottom" or "margin-left"
            or "width" or "height" => true,
        _ => false,
    };
    /// <summary>
    /// Resolves <c>anchor-size()</c> function calls in CSS properties and inline
    /// styles, replacing them with computed pixel values from the anchor element's
    /// dimensions.
    /// </summary>
    private static void ResolveAnchorSizeFunctions(
        DomElement element,
        Dictionary<string, string> cssProps,
        Dictionary<string, AnchorInfo> anchorRegistry)
    {
        // Get implicit anchor name from position-anchor.
        string? implicitAnchor = cssProps.GetValueOrDefault("position-anchor") ??
                                 element.Style.GetValueOrDefault("position-anchor");

        string ResolveValue(string value)
        {
            return AnchorSizeFunctionPattern.Replace(value, m =>
            {
                var anchorName = m.Groups["name"].Value;
                if (string.IsNullOrEmpty(anchorName))
                    anchorName = implicitAnchor ?? string.Empty;
                var dim = m.Groups["dim"].Value.ToLowerInvariant();

                if (!anchorRegistry.TryGetValue(anchorName, out var anchor))
                    return "0px";

                double result = dim switch
                {
                    "width" or "inline" or "self-inline" => anchor.Width,
                    "height" or "block" or "self-block" => anchor.Height,
                    _ => 0,
                };

                return $"{result.ToString(CultureInfo.InvariantCulture)}px";
            });
        }

        // Resolve in CSS properties and apply as inline styles.
        foreach (var kv in cssProps)
        {
            if (kv.Value.Contains("anchor-size(", StringComparison.OrdinalIgnoreCase))
            {
                element.Style[kv.Key] = ResolveValue(kv.Value);
            }
        }

        // Resolve in existing inline styles.
        var inlineKeys = new List<string>(element.Style.Keys);
        foreach (var key in inlineKeys)
        {
            if (element.Style[key].Contains("anchor-size(", StringComparison.OrdinalIgnoreCase))
            {
                element.Style[key] = ResolveValue(element.Style[key]);
            }
        }
    }
}
