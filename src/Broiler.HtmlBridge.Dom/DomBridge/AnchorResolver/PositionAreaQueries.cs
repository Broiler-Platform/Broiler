using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // position-area resolution for JS offset queries
    // -----------------------------------------------------------------

    /// <summary>
    /// Resolves position-area for a specific element during JS execution,
    /// returning the computed rect as (left, top, width, height).
    /// Called lazily when offsetLeft/offsetTop/etc. are queried.
    /// </summary>
    internal (double left, double top, double width, double height)?
        ResolvePositionAreaForElement(DomElement element)
    {
        // Check for pre-resolved values first.
        if (element.DomProperties.TryGetValue("_resolvedLeft", out var rl) && rl is double resolvedLeft &&
            element.DomProperties.TryGetValue("_resolvedTop", out var rt) && rt is double resolvedTop &&
            element.DomProperties.TryGetValue("_resolvedWidth", out var rw) && rw is double resolvedWidth &&
            element.DomProperties.TryGetValue("_resolvedHeight", out var rh) && rh is double resolvedHeight)
            return (resolvedLeft, resolvedTop, resolvedWidth, resolvedHeight);

        // Resolve on-the-fly from CSS properties and inline styles.
        var cssProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (selector, _, declarations) in CssRules)
        {
            if (MatchesSelector(element, selector))
                foreach (var kv in declarations)
                    cssProps[kv.Key] = kv.Value;
        }
        foreach (var kv in element.Style)
            cssProps[kv.Key] = kv.Value;

        string? positionArea = cssProps.GetValueOrDefault("position-area");
        string? positionAnchor = cssProps.GetValueOrDefault("position-anchor");

        if (string.IsNullOrWhiteSpace(positionArea) || positionArea == "none" ||
            string.IsNullOrWhiteSpace(positionAnchor))
            return null;

        // Build anchor registry on-the-fly.
        var anchorRegistry = new Dictionary<string, AnchorInfo>(StringComparer.Ordinal);
        BuildAnchorRegistry(anchorRegistry);
        BuildInlineAnchorRegistry(anchorRegistry);

        if (!anchorRegistry.TryGetValue(positionAnchor, out var anchor))
            return null;

        var rect = ComputePositionAreaRect(element, anchor, positionArea);
        if (rect == null) return null;

        // Cache the resolved values.
        element.DomProperties["_resolvedLeft"] = rect.Value.Left;
        element.DomProperties["_resolvedTop"] = rect.Value.Top;
        element.DomProperties["_resolvedWidth"] = rect.Value.Width;
        element.DomProperties["_resolvedHeight"] = rect.Value.Height;

        return (rect.Value.Left, rect.Value.Top, rect.Value.Width, rect.Value.Height);
    }
}
