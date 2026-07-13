using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // Fixed-position sizing
    // -----------------------------------------------------------------

    /// <summary>
    /// For elements that have <c>position: fixed</c> from CSS rules, ensures
    /// they have explicit pixel <c>width</c> and <c>height</c> inline styles.
    /// The Broiler renderer supports fixed positioning for top/left placement
    /// but cannot resolve dimensions from opposing inset values (e.g.
    /// <c>top: 0; bottom: 0</c> should give full-height but doesn't).
    /// </summary>
    private void ResolveFixedPositionSizing(int vpW, int vpH) => ResolveFixedPositionSizingInTree(DocumentElement, vpW, vpH);

    private void ResolveFixedPositionSizingInTree(DomElement el, int vpW, int vpH)
    {
        if (!IsText(el))
        {
            // Collect cascaded CSS properties for this element.
            var cssProps = CollectMatchedRuleProperties(el);
            // Merge inline styles (higher priority).
            foreach (var kv in InlineStyle(el))
                cssProps[kv.Key] = kv.Value;

            if (cssProps.TryGetValue("position", out var pos) &&
                string.Equals(pos, "fixed", StringComparison.OrdinalIgnoreCase))
            {
                // Ensure position: fixed is set as inline style.
                InlineStyle(el)["position"] = "fixed";

                // Expand the 'inset' shorthand into top/right/bottom/left.
                if (cssProps.TryGetValue("inset", out var insetVal) &&
                    !string.Equals(insetVal.Trim(), "auto", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = insetVal.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string t = parts[0];
                    string r = parts.Length > 1 ? parts[1] : parts[0];
                    string b = parts.Length > 2 ? parts[2] : parts[0];
                    string l = parts.Length > 3 ? parts[3] : r;
                    if (!InlineStyle(el).ContainsKey("top")) InlineStyle(el)["top"] = t;
                    if (!InlineStyle(el).ContainsKey("right")) InlineStyle(el)["right"] = r;
                    if (!InlineStyle(el).ContainsKey("bottom")) InlineStyle(el)["bottom"] = b;
                    if (!InlineStyle(el).ContainsKey("left")) InlineStyle(el)["left"] = l;
                }

                // Copy top/left/right/bottom/width/height from CSS if not already inline.
                foreach (var prop in new[] { "top", "left", "right", "bottom", "width", "height" })
                {
                    if (!InlineStyle(el).ContainsKey(prop) && cssProps.TryGetValue(prop, out var v))
                    {
                        if (!v.Contains("anchor(", StringComparison.OrdinalIgnoreCase))
                            InlineStyle(el)[prop] = v;
                    }
                }

                // Resolve width from opposing left/right insets when no explicit width.
                if (!InlineStyle(el).ContainsKey("width") || InlineStyle(el)["width"] == "auto")
                {
                    var leftPx = TryParsePx(InlineStyle(el).GetValueOrDefault("left"));
                    var rightPx = TryParsePx(InlineStyle(el).GetValueOrDefault("right"));
                    if (leftPx.HasValue && rightPx.HasValue)
                        InlineStyle(el)["width"] = $"{vpW - leftPx.Value - rightPx.Value}px";
                }

                // Resolve height from opposing top/bottom insets when no explicit height.
                if (!InlineStyle(el).ContainsKey("height") || InlineStyle(el)["height"] == "auto")
                {
                    var topPx = TryParsePx(InlineStyle(el).GetValueOrDefault("top"));
                    var bottomPx = TryParsePx(InlineStyle(el).GetValueOrDefault("bottom"));
                    if (topPx.HasValue && bottomPx.HasValue)
                        InlineStyle(el)["height"] = $"{vpH - topPx.Value - bottomPx.Value}px";
                }
            }
        }

        // Snapshot before recursing: the live child list can be mutated mid-walk
        // (concurrent/lazy DOM edit) and throw, aborting resolution. SnapshotChildren
        // tolerates that — same idiom as the other anchor-resolver tree walks.
        foreach (var child in SnapshotChildren(el))
            ResolveFixedPositionSizingInTree(child, vpW, vpH);
    }
}
