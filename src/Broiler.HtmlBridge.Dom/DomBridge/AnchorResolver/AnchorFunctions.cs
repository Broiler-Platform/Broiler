using System.Globalization;
using System.Linq;
using Broiler.CSS;
using Broiler.Dom;
using Broiler.Layout;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // anchor() resolution
    // -----------------------------------------------------------------

    // The anchor()/anchor-size() grammar (token matching + typed extraction) is the
    // canonical Broiler.CSS.AnchorFunction model (Phase 5 item 4). These callbacks
    // keep only the used-value geometry; AnchorFunction.Rewrite/RewriteSize supply
    // the parsed AnchorFunctionRef/AnchorSizeFunctionRef.
    private void ResolveAnchorFunctions(DomElement element, Dictionary<string, AnchorInfo> anchorRegistry)
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

        // Native mode (P5.8d.2b anchor()-insets expansion): for the MVP subset, skip
        // baking the anchor() insets entirely so the box's `left/right/top/bottom:
        // anchor(...)` CSS survives to the render and the Broiler.Layout engine's placement
        // post-pass resolves it natively (see CssBox.TryApplyAnchorInsetPlacement). Every
        // other anchor() box is baked below.
        if (hasAnchorRef && NativeAnchorPlacement &&
            IsMvpNativeAnchorInsetBox(element, cssProps, anchorRegistry))
            hasAnchorRef = false;

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
                var resolved = AnchorFunction.Rewrite(kv.Value, r =>
                {
                    var anchorName = string.IsNullOrEmpty(r.Name)
                        ? (implicitAnchor ?? string.Empty)
                        : r.Name!;
                    var fallback = r.Fallback;

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

                    // Edge coordinate math (anchor edge − scroll adjustment, plus the
                    // right/bottom opposite-edge flip) is the canonical
                    // Broiler.Layout.AnchorGeometry model (Phase 5 item 3).
                    double value = AnchorGeometry.ResolveEdge(
                        anchor.Left, anchor.Top, anchor.Right, anchor.Bottom,
                        r.Side, adjX, adjY, MapAnchorInsetProperty(propName), cbW, cbH);

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
        // Native mode (P5.8d.2b anchor-size() expansion): skip baking for the MVP subset so
        // the box's `width/height: anchor-size(...)` survives to the engine's sizing pass
        // (CssBox.TryApplyNativeAnchorSizing).
        if (hasAnchorSizeRef &&
            !(NativeAnchorPlacement && IsMvpNativeAnchorSizeBox(element, cssProps, anchorRegistry)))
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
    /// Whether an element's <c>anchor()</c> insets are the MVP subset the engine's native
    /// placement post-pass reproduces (P5.8d.2b), so the bridge can hand them off instead of
    /// pre-baking (see <see cref="CssBox"/>'s <c>TryApplyAnchorInsetPlacement</c>). Requires:
    /// every <c>anchor()</c> reference is in a physical inset (<c>left</c>/<c>right</c>/
    /// <c>top</c>/<c>bottom</c>) and names a registered, accessible anchor; no
    /// <c>anchor-size()</c>; at most one inset per axis (opposing-inset sizing needs a re-flow
    /// the reposition-only pass can't do); the box is not fixed/modal and has no intervening
    /// scroll offset (the engine uses no scroll adjustment); and no <c>position-try</c>.
    /// </summary>
    private bool IsMvpNativeAnchorInsetBox(
        DomElement element, Dictionary<string, string> cssProps,
        Dictionary<string, AnchorInfo> anchorRegistry)
    {
        // Merge inline styles over matched-rule props (inline wins), matching what the
        // engine cascade projects onto the box.
        var merged = new Dictionary<string, string>(cssProps, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in InlineStyle(element))
            merged[kv.Key] = kv.Value;

        // No position-try (out of the MVP subset).
        if (merged.ContainsKey("position-try-fallbacks") || merged.ContainsKey("position-try"))
            return false;

        // Fixed / modal-dialog targets get a document-scroll adjustment the engine MVP
        // does not apply — keep them baked.
        var position = merged.GetValueOrDefault("position");
        if (position == "fixed")
            return false;
        if (GetElementRuntimeState(element).Dialog.Modal.TryGet(out var modal) && modal is true)
            return false;

        // Every anchor()/anchor-size() must be an anchor() in a physical inset; any
        // anchor-size(), or an anchor() outside left/right/top/bottom, stays baked.
        bool anyAnchorInset = false;
        foreach (var (key, value) in merged)
        {
            if (value.Contains("anchor-size(", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!value.Contains("anchor(", StringComparison.OrdinalIgnoreCase))
                continue;
            if (key is not ("left" or "right" or "top" or "bottom"))
                return false;
            anyAnchorInset = true;
        }
        if (!anyAnchorInset)
            return false;

        // Reposition-only: opposing insets on an axis would size the box (needs a re-flow).
        if (HasInset(merged, "left") && HasInset(merged, "right"))
            return false;
        if (HasInset(merged, "top") && HasInset(merged, "bottom"))
            return false;

        // Every referenced anchor must be registered, accessible, and moved by no scroll
        // relative to the target (the engine resolves with a zero scroll adjustment).
        string? implicitAnchor = merged.GetValueOrDefault("position-anchor");
        foreach (var key in new[] { "left", "right", "top", "bottom" })
        {
            if (!merged.TryGetValue(key, out var value) ||
                !value.Contains("anchor(", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!AnchorFunction.TryGetFirst(value, out var reference))
                return false;
            var name = string.IsNullOrEmpty(reference.Name)
                ? (implicitAnchor ?? string.Empty)
                : reference.Name!;
            if (string.IsNullOrEmpty(name) || name == "auto")
                return false;
            if (!anchorRegistry.TryGetValue(name, out var anchor) ||
                !IsAnchorAccessible(anchor.SourceElement, element))
                return false;
            // A non-fixed anchor separated from the target by a scroll container shifts by
            // that scroller's offset (the bridge subtracts it); the engine MVP does not.
            if (anchor.SourceElement != null)
            {
                bool anchorIsFixed =
                    GetComputedProps(anchor.SourceElement).GetValueOrDefault("position") == "fixed";
                if (!anchorIsFixed)
                {
                    ComputeInterveningScrollOffset(anchor.SourceElement, element, out var sx, out var sy);
                    if (sx != 0 || sy != 0)
                        return false;
                }
            }
        }

        return true;
    }

    /// <summary>Whether a physical inset in <paramref name="props"/> is present and not
    /// <c>auto</c>.</summary>
    private static bool HasInset(Dictionary<string, string> props, string name) =>
        props.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v) && v.Trim() != "auto";

    /// <summary>
    /// Whether an element's <c>anchor-size()</c> sizing is the MVP subset the engine's native
    /// sizing pass reproduces (P5.8d.2b), so the bridge can hand it off instead of pre-baking
    /// (see <see cref="CssBox"/>'s <c>TryApplyNativeAnchorSizing</c>). Requires: an absolutely
    /// positioned, <b>childless</b> box (the engine only sizes childless boxes without a
    /// re-flow); <c>anchor-size()</c> only in <c>width</c>/<c>height</c>, each naming a
    /// registered accessible anchor; no <c>anchor()</c> inset (the combined case stays baked);
    /// no <c>position-area</c>; no right/bottom inset (the engine grows the box from its
    /// laid-out origin); no CSS <c>zoom</c>; not a modal dialog; and no <c>position-try</c>.
    /// </summary>
    private bool IsMvpNativeAnchorSizeBox(
        DomElement element, Dictionary<string, string> cssProps,
        Dictionary<string, AnchorInfo> anchorRegistry)
    {
        var merged = new Dictionary<string, string>(cssProps, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in InlineStyle(element))
            merged[kv.Key] = kv.Value;

        // Absolutely positioned only (fixed gets a scroll adjustment the engine MVP omits).
        if (merged.GetValueOrDefault("position") != "absolute")
            return false;
        if (merged.ContainsKey("position-try-fallbacks") || merged.ContainsKey("position-try"))
            return false;
        // position-area boxes are placed+sized by the position-area path, not here.
        var area = merged.GetValueOrDefault("position-area");
        if (!string.IsNullOrWhiteSpace(area) && area != "none")
            return false;
        if (GetElementRuntimeState(element).Dialog.Modal.TryGet(out var modal) && modal is true)
            return false;
        // The engine sizes already-laid-out (already-zoomed) box geometry; a CSS zoom scale
        // would double-count, so keep zoomed boxes baked.
        if (Math.Abs(GetUsedZoomForElement(element) - 1.0) > 0.0001)
            return false;
        // Childless: the engine only resizes a box with no in-flow children/words.
        if (ChildElements(element).Any())
            return false;
        if (!string.IsNullOrWhiteSpace(GetElementTextContent(element)))
            return false;
        // The engine grows the box from its laid-out left/top origin, so a right/bottom inset
        // (which the baked path resolves via a re-layout) stays baked.
        if (HasInset(merged, "right") || HasInset(merged, "bottom"))
            return false;

        // anchor-size() only in width/height; any anchor() inset means the combined case,
        // which stays baked (the anchor()-inset gate already excludes anchor-size boxes).
        bool anySize = false;
        foreach (var (key, value) in merged)
        {
            if (value.Contains("anchor(", StringComparison.OrdinalIgnoreCase) &&
                !value.Contains("anchor-size(", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!value.Contains("anchor-size(", StringComparison.OrdinalIgnoreCase))
                continue;
            if (key is not ("width" or "height"))
                return false;
            anySize = true;
        }
        if (!anySize)
            return false;

        // Every referenced anchor must be registered and accessible.
        string? implicitAnchor = merged.GetValueOrDefault("position-anchor");
        foreach (var key in new[] { "width", "height" })
        {
            if (!merged.TryGetValue(key, out var value) ||
                !value.Contains("anchor-size(", StringComparison.OrdinalIgnoreCase))
                continue;
            bool ok = true;
            AnchorFunction.RewriteSize(value, r =>
            {
                var name = string.IsNullOrEmpty(r.Name)
                    ? (implicitAnchor ?? string.Empty)
                    : r.Name!;
                if (string.IsNullOrEmpty(name) || name == "auto" ||
                    !anchorRegistry.TryGetValue(name, out var anchor) ||
                    !IsAnchorAccessible(anchor.SourceElement, element))
                    ok = false;
                return "0px";
            });
            if (!ok)
                return false;
        }

        return true;
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
    private void ComputeInterveningScrollOffset(DomElement anchorEl, DomElement targetEl, out double offX, out double offY)
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
    /// Maps the CSS inset property an <c>anchor()</c> resolves into to the
    /// <see cref="AnchorInsetProperty"/> the Layout edge resolver flips against
    /// (only right/bottom differ; everything else uses the raw edge).
    /// </summary>
    private static AnchorInsetProperty MapAnchorInsetProperty(string property) => property switch
    {
        "right" => AnchorInsetProperty.Right,
        "bottom" => AnchorInsetProperty.Bottom,
        "left" => AnchorInsetProperty.Left,
        "top" => AnchorInsetProperty.Top,
        _ => AnchorInsetProperty.Other,
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
            return AnchorFunction.RewriteSize(value, r =>
            {
                var anchorName = string.IsNullOrEmpty(r.Name)
                    ? (implicitAnchor ?? string.Empty)
                    : r.Name!;

                if (!anchorRegistry.TryGetValue(anchorName, out var anchor))
                    return "0px";

                double result = AnchorGeometry.ResolveSize(r.Dimension, anchor.Width, anchor.Height);

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
