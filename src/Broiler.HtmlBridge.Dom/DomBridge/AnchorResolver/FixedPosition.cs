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
    private void ResolveFixedPositionSizing(int vpW, int vpH)
    {
        ResolveFixedPositionSizingInTree(DocumentElement, vpW, vpH);
    }
    private void ResolveFixedPositionSizingInTree(DomElement el, int vpW, int vpH)
    {
        if (!el.IsTextNode)
        {
            // Collect cascaded CSS properties for this element.
            var cssProps = CollectMatchedRuleProperties(el);
            // Merge inline styles (higher priority).
            foreach (var kv in el.Style)
                cssProps[kv.Key] = kv.Value;

            if (cssProps.TryGetValue("position", out var pos) &&
                string.Equals(pos, "fixed", StringComparison.OrdinalIgnoreCase))
            {
                // Ensure position: fixed is set as inline style.
                el.Style["position"] = "fixed";

                // Expand the 'inset' shorthand into top/right/bottom/left.
                if (cssProps.TryGetValue("inset", out var insetVal) &&
                    !string.Equals(insetVal.Trim(), "auto", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = insetVal.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string t = parts[0];
                    string r = parts.Length > 1 ? parts[1] : parts[0];
                    string b = parts.Length > 2 ? parts[2] : parts[0];
                    string l = parts.Length > 3 ? parts[3] : r;
                    if (!el.Style.ContainsKey("top")) el.Style["top"] = t;
                    if (!el.Style.ContainsKey("right")) el.Style["right"] = r;
                    if (!el.Style.ContainsKey("bottom")) el.Style["bottom"] = b;
                    if (!el.Style.ContainsKey("left")) el.Style["left"] = l;
                }

                // Copy top/left/right/bottom/width/height from CSS if not already inline.
                foreach (var prop in new[] { "top", "left", "right", "bottom", "width", "height" })
                {
                    if (!el.Style.ContainsKey(prop) && cssProps.TryGetValue(prop, out var v))
                    {
                        if (!v.Contains("anchor(", StringComparison.OrdinalIgnoreCase))
                            el.Style[prop] = v;
                    }
                }

                // Resolve width from opposing left/right insets when no explicit width.
                if (!el.Style.ContainsKey("width") || el.Style["width"] == "auto")
                {
                    var leftPx = TryParsePx(el.Style.GetValueOrDefault("left"));
                    var rightPx = TryParsePx(el.Style.GetValueOrDefault("right"));
                    if (leftPx.HasValue && rightPx.HasValue)
                        el.Style["width"] = $"{vpW - leftPx.Value - rightPx.Value}px";
                }

                // Resolve height from opposing top/bottom insets when no explicit height.
                if (!el.Style.ContainsKey("height") || el.Style["height"] == "auto")
                {
                    var topPx = TryParsePx(el.Style.GetValueOrDefault("top"));
                    var bottomPx = TryParsePx(el.Style.GetValueOrDefault("bottom"));
                    if (topPx.HasValue && bottomPx.HasValue)
                        el.Style["height"] = $"{vpH - topPx.Value - bottomPx.Value}px";
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
