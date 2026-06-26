using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // Containing block positioning
    // -----------------------------------------------------------------

    /// <summary>
    /// For elements that establish containing blocks via CSS properties that
    /// Broiler's renderer does not understand (e.g. <c>contain:layout</c>,
    /// <c>transform</c>), adds <c>position:relative</c> to their inline
    /// styles so the renderer treats them as containing blocks for absolutely
    /// positioned descendants.
    /// </summary>
    private void EnsureContainingBlockPositioning(DomElement root)
    {
        EnsureContainingBlockPositioningTree(root);
    }
    private void EnsureContainingBlockPositioningTree(DomElement el)
    {
        if (!el.IsTextNode && !string.Equals(el.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
        {
            var props = CollectMatchedRuleProperties(el);
            foreach (var kv in el.Style)
                props[kv.Key] = kv.Value;

            // If the element already has explicit positioning, no change needed.
            bool alreadyPositioned = props.TryGetValue("position", out var pos) &&
                (pos == "relative" || pos == "absolute" || pos == "fixed" || pos == "sticky");

            if (!alreadyPositioned && EstablishesContainingBlock(props))
            {
                el.Style["position"] = "relative";
            }
        }

        foreach (var child in el.Children)
            EnsureContainingBlockPositioningTree(child);
    }
    /// <summary>
    /// Determines whether an element with the given CSS properties
    /// establishes a containing block for absolutely positioned descendants.
    /// Per CSS spec, this includes:
    /// <list type="bullet">
    ///   <item>position: relative/absolute/fixed/sticky</item>
    ///   <item>transform (any non-none value)</item>
    ///   <item>contain: layout/paint/strict/content</item>
    ///   <item>will-change: transform</item>
    /// </list>
    /// </summary>
    private static bool EstablishesContainingBlock(Dictionary<string, string> props)
    {
        if (props.TryGetValue("position", out var pos) &&
            (pos == "relative" || pos == "absolute" || pos == "fixed" || pos == "sticky"))
            return true;

        if (props.TryGetValue("transform", out var transform) &&
            !string.Equals(transform, "none", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(transform))
            return true;

        if (props.TryGetValue("contain", out var contain) &&
            !string.IsNullOrWhiteSpace(contain))
        {
            var containLower = contain.ToLowerInvariant();
            if (containLower.Contains("layout") || containLower.Contains("paint") ||
                containLower.Contains("strict") || containLower.Contains("content"))
                return true;
        }

        if (props.TryGetValue("will-change", out var willChange) &&
            willChange.Contains("transform", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
