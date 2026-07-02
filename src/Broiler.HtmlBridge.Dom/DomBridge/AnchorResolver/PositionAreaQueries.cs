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
        if (GetElementRuntimeState(element).Layout.Left.TryGet(out var rl) && rl is double resolvedLeft &&
            GetElementRuntimeState(element).Layout.Top.TryGet(out var rt) && rt is double resolvedTop &&
            GetElementRuntimeState(element).Layout.Width.TryGet(out var rw) && rw is double resolvedWidth &&
            GetElementRuntimeState(element).Layout.Height.TryGet(out var rh) && rh is double resolvedHeight)
            return (resolvedLeft, resolvedTop, resolvedWidth, resolvedHeight);

        // Resolve on-the-fly from CSS properties and inline styles.
        var cssProps = CollectMatchedRuleProperties(element);
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

        var anchor = ResolveAnchorForElement(positionAnchor, element, anchorRegistry);
        if (anchor is null)
            return null;

        var rect = ComputePositionAreaRect(element, anchor, positionArea);
        if (rect == null) return null;

        // Cache the resolved values.
        GetElementRuntimeState(element).Layout.Left.Set(rect.Value.Left);
        GetElementRuntimeState(element).Layout.Top.Set(rect.Value.Top);
        GetElementRuntimeState(element).Layout.Width.Set(rect.Value.Width);
        GetElementRuntimeState(element).Layout.Height.Set(rect.Value.Height);

        return (rect.Value.Left, rect.Value.Top, rect.Value.Width, rect.Value.Height);
    }
}
