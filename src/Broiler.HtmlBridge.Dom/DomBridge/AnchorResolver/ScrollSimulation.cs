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
            if (ScrollStateFor(el).Top.TryGet(out var st) && st is double stv)
                scrollTop = stv;
            if (ScrollStateFor(el).Left.TryGet(out var sl) && sl is double slv)
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
                    // Hand the scroll offset to the Broiler.Layout engine via data attributes
                    // instead of DOM-shifting the content. The engine's scroll post-pass
                    // (CssBox.RunScrollSimulation) translates the container's content and its
                    // overflow box (or the viewport, for the document scrolling element) clips it —
                    // no wrapper div, no inline position/top/left/visibility writes, and no
                    // fixed-descendant reparenting (OffsetTop/OffsetLeft skip position:fixed at every
                    // depth, CSS2.1 §9.6.1). The document scrolling element (<html>) is included:
                    // with a scrollable root (tall content) documentElement.scrollTop resolves
                    // normally and the engine translation matches. The flag check is dropped in
                    // Phase 4 item-2 step 5 — the handoff is unconditional (a provable no-op on the
                    // native default path, where the flag was already true); the retired baked
                    // DOM-shift wrapper (and its scroll-hidden / anchor-cb markers) is deleted.
                    if (scrollTop != 0)
                        SetAttr(el, "data-broiler-scroll-top",
                            scrollTop.ToString(CultureInfo.InvariantCulture));
                    if (scrollLeft != 0)
                        SetAttr(el, "data-broiler-scroll-left",
                            scrollLeft.ToString(CultureInfo.InvariantCulture));

                    // Recurse into children (nested scroll containers) and skip the DOM-shift.
                    for (int i = 0; i < el.ChildNodes.Count; i++)
                        if (ChildAt(el, i) is DomElement scrolledChild)
                            ApplyScrollSimulationTree(scrolledChild);
                    return;
                }
            }
        }

        // Use index-based loop because the list may grow during iteration
        // (wrapper insertion above).
        for (int i = 0; i < el.ChildNodes.Count; i++)
            if (ChildAt(el, i) is DomElement child)
                ApplyScrollSimulationTree(child);
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
