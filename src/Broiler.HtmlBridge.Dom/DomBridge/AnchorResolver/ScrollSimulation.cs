using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

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
    private void ApplyScrollSimulation(DomElement root)
    {
        ApplyScrollSimulationTree(root);
    }
    private void ApplyScrollSimulationTree(DomElement el)
    {
        if (!el.IsTextNode)
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

                if ((clips || isDocScrollingElement) && el.Children.Count > 0)
                {
                    // Wrap all children in a positioned div that shifts content
                    // upward / leftward.  Using position:relative + top/left
                    // ensures the shifted content is clipped correctly by the
                    // container's overflow:hidden at all edges (including top),
                    // avoiding the rendering artefact where negative-margin
                    // spacers can leak above the container's top edge.
                    var wrapper = new DomElement(_document, "div", null, null, "");
                    wrapper.Style["position"] = "relative";
                    if (scrollTop != 0)
                        wrapper.Style["top"] =
                            $"{(-scrollTop).ToString(CultureInfo.InvariantCulture)}px";
                    if (scrollLeft != 0)
                        wrapper.Style["left"] =
                            $"{(-scrollLeft).ToString(CultureInfo.InvariantCulture)}px";

                    var originalChildren = SnapshotChildren(el);
                    el.Children.Clear();
                    el.Children.Add(wrapper);
                    foreach (var child in originalChildren)
                    {
                        child.Parent = wrapper;
                        wrapper.Children.Add(child);
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
                            fixedEl.Parent?.Children.Remove(fixedEl);
                            fixedEl.Parent = el;
                            el.Children.Add(fixedEl);
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
                            if (child.IsTextNode) continue;
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
                                child.Style["visibility"] = "hidden";
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
        for (int i = 0; i < el.Children.Count; i++)
            ApplyScrollSimulationTree(el.Children[i]);
    }
    /// <summary>
    /// Recursively collects all descendants with <c>position: fixed</c>
    /// (including generated backdrop divs) from the given subtree.
    /// </summary>
    private void CollectFixedDescendants(DomElement parent, List<DomElement> results)
    {
        foreach (var child in SnapshotChildren(parent))
        {
            if (child.IsTextNode) continue;
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
    private double GetScrollSimulationScaleFactor() =>
        HasActiveVisualViewport() ? GetVisualViewportScale() : 1;
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
