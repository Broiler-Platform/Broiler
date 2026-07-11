using System.Globalization;
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
        var cssProps = CollectMatchedRuleProperties(element);

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
        foreach (var kv in InlineStyle(element))
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
                                     InlineStyle(element).GetValueOrDefault("position-anchor");

            // When the target element is fixed-positioned (e.g. top-layer dialog)
            // and the anchor is NOT fixed-positioned, anchor positions must be
            // adjusted by the document scroll offset so the anchor's viewport
            // position is used instead of its document position.
            bool targetIsFixed =
                (cssProps.GetValueOrDefault("position") ?? InlineStyle(element).GetValueOrDefault("position")) == "fixed" ||
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

                    // Scroll-driven positioning (CSS Anchor Positioning § scroll):
                    // an anchored element must track its anchor's *scrolled* position.
                    // When a scroll container is an ancestor of the anchor but NOT of
                    // the target (the target lives outside that scroller, e.g. a fixed
                    // or sibling-of-scroller abspos box), the anchor's edges shift by
                    // that scroller's scroll offset while the target does not — so the
                    // resolved inset must subtract it. Scrollers that contain the target
                    // too move both together and are skipped. (The target-inside-scroller
                    // case is handled separately by ApplyScrollSimulation, which shifts
                    // the target's own subtree.)
                    double nestedX = 0, nestedY = 0;
                    if (!anchorIsFixed && anchor.SourceElement != null)
                        ComputeInterveningScrollOffset(
                            anchor.SourceElement, element, out nestedX, out nestedY);

                    double adjY = anchorIsFixed ? 0 : scrollAdjY + nestedY;
                    double adjX = anchorIsFixed ? 0 : scrollAdjX + nestedX;

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
                    InlineStyle(element)[kv.Key] = resolved;
            }

            // Apply non-anchor CSS properties (e.g. position, margin).
            foreach (var kv in cssProps)
            {
                if (!kv.Value.Contains("anchor(", StringComparison.OrdinalIgnoreCase) &&
                    !kv.Value.Contains("anchor-size(", StringComparison.OrdinalIgnoreCase) &&
                    !InlineStyle(element).ContainsKey(kv.Key) &&
                    IsLayoutProperty(kv.Key))
                {
                    InlineStyle(element)[kv.Key] = kv.Value;
                }
            }

            // Remove 'inset' shorthand.
            InlineStyle(element).Remove("inset");
        }

        // Resolve anchor-size() function calls in both CSS and inline styles.
        if (hasAnchorSizeRef)
        {
            ResolveAnchorSizeFunctions(element, cssProps, anchorRegistry);
        }

        // Snapshot the child list: resolving anchor functions on a descendant can
        // mutate the live DOM (e.g. anchor-driven style/structure changes under
        // content-visibility), and iterating the live collection while it changes
        // throws "Collection was modified" (WPT content-visibility-anchor-positioning)
        // or overflows the ToList() copy. SnapshotChildren tolerates both.
        foreach (var child in SnapshotChildren(element))
            ResolveAnchorFunctions(child, anchorRegistry);
    }
    /// <summary>
    /// Accumulates the scroll offset of scroll containers that lie between the
    /// anchor and the target — i.e. that are ancestors of <paramref name="anchorEl"/>
    /// but do not also contain <paramref name="targetEl"/>. Such a scroller moves the
    /// anchor (and its edges) but not the target, so an anchored element positioned
    /// against it must subtract this offset to stay pinned to the anchor's scrolled
    /// position. The walk stops at the first scroller that also contains the target
    /// (that scroller scrolls both, or is the target's containing block). The offset
    /// is scaled to match <c>ApplyScrollSimulation</c> under an active visual viewport.
    /// </summary>
    private void ComputeInterveningScrollOffset(
        DomElement anchorEl, DomElement targetEl, out double offX, out double offY)
    {
        offX = 0;
        offY = 0;
        var scale = GetScrollSimulationScaleFactor();

        // A position:sticky element stays pinned to its nearest scroll
        // container's edge instead of translating with that scroller's scroll.
        // When the anchor (or a box between it and its scroller) is sticky, the
        // anchor's scrolled position does NOT shift by the full scroll offset,
        // so a target outside the scroller must not subtract it — doing so drives
        // the anchored box off-screen (css-anchor-position anchor-scroll-to-sticky-004).
        bool stickyToNextScroller = IsSticky(GetComputedProps(anchorEl));

        for (var el = ParentEl(anchorEl); el != null; el = ParentEl(el))
        {
            var props = GetComputedProps(el);
            if (!HasOverflowClipping(props))
            {
                // A sticky box below the next scroller pins the anchor to it;
                // remember that until we reach the scroller itself.
                if (IsSticky(props))
                    stickyToNextScroller = true;
                continue;
            }

            // The target lives inside this scroller too → they scroll together
            // (or this scroller is the target's containing block); no separation.
            if (IsDescendantOrSelf(targetEl, el))
                break;

            // Skip scrollers the anchor is sticky-pinned to: the anchor resists
            // this scroller's scroll, so its edges don't move by that offset.
            if (!stickyToNextScroller)
            {
                if (GetElementRuntimeState(el).Scroll.Left.TryGet(out var sl) && sl is double slv)
                    offX += slv * scale;
                if (GetElementRuntimeState(el).Scroll.Top.TryGet(out var st) && st is double stv)
                    offY += stv * scale;
            }

            // Past this scroller, sticky pinning resets: a sticky box higher up
            // pins to the next scroll container, not this one.
            stickyToNextScroller = false;
        }
    }
    /// <summary>
    /// True when the computed <c>position</c> in <paramref name="props"/> is
    /// <c>sticky</c>.
    /// </summary>
    private static bool IsSticky(Dictionary<string, string> props) =>
        string.Equals(props.GetValueOrDefault("position"), "sticky", StringComparison.OrdinalIgnoreCase);
    /// <summary>
    /// True when <paramref name="node"/> is <paramref name="ancestor"/> or a
    /// descendant of it.
    /// </summary>
    private static bool IsDescendantOrSelf(DomElement node, DomElement ancestor)
    {
        for (var cur = node; cur != null; cur = ParentEl(cur))
            if (cur == ancestor)
                return true;
        return false;
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
                                 InlineStyle(element).GetValueOrDefault("position-anchor");

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
                InlineStyle(element)[kv.Key] = ResolveValue(kv.Value);
            }
        }

        // Resolve in existing inline styles.
        var inlineKeys = new List<string>(InlineStyle(element).Keys);
        foreach (var key in inlineKeys)
        {
            if (InlineStyle(element)[key].Contains("anchor-size(", StringComparison.OrdinalIgnoreCase))
            {
                InlineStyle(element)[key] = ResolveValue(InlineStyle(element)[key]);
            }
        }
    }
}
