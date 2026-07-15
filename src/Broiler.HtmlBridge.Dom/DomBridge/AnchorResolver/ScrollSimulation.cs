using System.Globalization;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // Scroll simulation
    // -----------------------------------------------------------------

    /// <summary>
    /// Simulates scroll positions set via JavaScript (<c>element.scrollTop</c>,
    /// <c>element.scrollLeft</c>) by shifting children of scroll containers
    /// with negative margins.  Combined with <c>overflow: hidden</c>, this
    /// produces the same visual output as a real browser scroll.
    /// </summary>
    private void ApplyScrollSimulation(DomElement root) => ApplyScrollSimulationTree(root);

    private void ApplyScrollSimulationTree(DomElement el)
    {
        if (!IsText(el))
        {
            double scrollTop = 0;
            double scrollLeft = 0;
            if (GetElementRuntimeState(el).Scroll.Top.TryGet(out var st) && st is double stv)
                scrollTop = stv;
            if (GetElementRuntimeState(el).Scroll.Left.TryGet(out var sl) && sl is double slv)
                scrollLeft = slv;

            var scrollScale = GetScrollSimulationScaleFactor();
            if (!AreClose(scrollScale, 1))
            {
                scrollTop *= scrollScale;
                scrollLeft *= scrollScale;
            }

            if (scrollTop != 0 || scrollLeft != 0)
            {
                // Only apply to elements that clip overflow, or to the
                // document scrolling element (<html>) which is implicitly
                // clipped by the viewport.
                var props = GetComputedProps(el);
                bool clips = HasOverflowClipping(props);
                bool isDocScrollingElement =
                    string.Equals(el.TagName, "html", StringComparison.OrdinalIgnoreCase);

                if ((clips || isDocScrollingElement) && el.ChildNodes.Count > 0)
                {
                    // Wrap all children in a positioned div that shifts content
                    // upward / leftward.  Using position:relative + top/left
                    // ensures the shifted content is clipped correctly by the
                    // container's overflow:hidden at all edges (including top),
                    // avoiding the rendering artefact where negative-margin
                    // spacers can leak above the container's top edge.
                    var wrapper = CreateBridgeElement("div");
                    InlineStyle(wrapper)["position"] = "relative";
                    // Native mode: this scroll-offset wrapper is a rendering hack, not an authored
                    // containing block, so mark it (like an anchor-induced-relative scroller) — the
                    // engine's position-visibility pass must resolve a target's CB to the real
                    // authored scroll container, not this wrapper (else an authored position:relative
                    // scroller's target is wrongly hidden — position-visibility-anchors-visible-with-position).
                    if (NativeAnchorPlacement)
                        SetAttr(wrapper, "data-broiler-anchor-cb", "1");
                    if (scrollTop != 0)
                        InlineStyle(wrapper)["top"] =
                            $"{(-scrollTop).ToString(CultureInfo.InvariantCulture)}px";
                    if (scrollLeft != 0)
                        InlineStyle(wrapper)["left"] =
                            $"{(-scrollLeft).ToString(CultureInfo.InvariantCulture)}px";

                    var originalChildren = SnapshotChildren(el);
                    ClearChildren(el);
                    el.AppendChild(wrapper);
                    foreach (var child in originalChildren)
                    {
                        SetParent(child, wrapper);
                        wrapper.AppendChild(child);
                    }

                    // For the document scrolling element, extract ALL
                    // fixed-positioned descendants from the wrapper and
                    // re-parent them as direct children of <html>.  This
                    // prevents the Broiler renderer from incorrectly
                    // shifting viewport-relative elements by the scroll
                    // offset applied to the wrapper.
                    if (isDocScrollingElement)
                    {
                        var fixedDescendants = new List<DomElement>();
                        CollectFixedDescendants(wrapper, fixedDescendants);
                        foreach (var fixedEl in fixedDescendants)
                        {
                            fixedEl.Remove();
                            SetParent(fixedEl, el);
                            el.AppendChild(fixedEl);
                        }
                    }

                    // Hide normal-flow children that are entirely above the
                    // scroll position.  This prevents coloured content from
                    // leaking above the container's top edge (Broiler's
                    // renderer clips overflow at the bottom but may not
                    // fully clip at the top for position:relative offsets).
                    // Skip this for the document scrolling element (<html>)
                    // because its children are structural (<head>, <body>)
                    // and must not be hidden — the viewport clips naturally.
                    if (scrollTop > 0 && !isDocScrollingElement)
                    {
                        double childOffset = 0;
                        foreach (var child in SnapshotChildren(wrapper))
                        {
                            if (IsText(child)) continue;
                            var cp = GetComputedProps(child);
                            var childPos = cp.GetValueOrDefault("position");
                            if (childPos == "absolute" || childPos == "fixed")
                                continue;
                            double childH =
                                TryParsePx(cp.GetValueOrDefault("height")) ?? 0;
                            double childMT =
                                TryParsePx(cp.GetValueOrDefault("margin-top")) ?? 0;
                            childOffset += childMT;
                            if (childOffset + childH <= scrollTop)
                            {
                                InlineStyle(child)["visibility"] = "hidden";
                                // Native mode: this visibility:hidden is a scroll-clip hack, not an
                                // authored value — mark it so the engine's position-visibility pass
                                // does not mistake a scrolled-out anchor for an authored
                                // visibility:hidden anchor (which would hide the target even when its
                                // CB is the scroller — position-visibility-anchors-visible-with-position).
                                if (NativeAnchorPlacement)
                                    SetAttr(child, "data-broiler-scroll-hidden", "1");
                                childOffset += childH;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }
        }

        // Use index-based loop because the list may grow during iteration
        // (wrapper insertion above).
        for (int i = 0; i < el.ChildNodes.Count; i++)
            if (ChildAt(el, i) is DomElement child)
                ApplyScrollSimulationTree(child);
    }

    /// <summary>
    /// Recursively collects all descendants with <c>position: fixed</c>
    /// (including generated backdrop divs) from the given subtree.
    /// </summary>
    private void CollectFixedDescendants(DomElement parent, List<DomElement> results)
    {
        foreach (var child in SnapshotChildren(parent))
        {
            if (IsText(child)) continue;
            var cp = GetComputedProps(child);
            var pos = cp.GetValueOrDefault("position");
            if (pos == "fixed")
            {
                results.Add(child);
            }
            else
            {
                // Recurse into non-fixed elements to find fixed descendants.
                CollectFixedDescendants(child, results);
            }
        }
    }
    
    private double GetScrollSimulationScaleFactor() => HasActiveVisualViewport() ? GetVisualViewportScale() : 1;

    private static bool HasOverflowClipping(Dictionary<string, string> props)
    {
        if (props.TryGetValue("overflow", out var ov))
        {
            var val = ov.Trim().ToLowerInvariant();
            if (val.Contains("hidden") || val.Contains("scroll") || val.Contains("auto") || val.Contains("clip"))
                return true;
        }
        if (props.TryGetValue("overflow-x", out var ovx))
        {
            var val = ovx.Trim().ToLowerInvariant();
            if (val.Contains("hidden") || val.Contains("scroll") || val.Contains("auto") || val.Contains("clip"))
                return true;
        }
        if (props.TryGetValue("overflow-y", out var ovy))
        {
            var val = ovy.Trim().ToLowerInvariant();
            if (val.Contains("hidden") || val.Contains("scroll") || val.Contains("auto") || val.Contains("clip"))
                return true;
        }
        return false;
    }
}
