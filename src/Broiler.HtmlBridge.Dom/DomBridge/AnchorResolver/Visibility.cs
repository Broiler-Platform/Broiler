namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // position-visibility resolution
    // -----------------------------------------------------------------

    /// <summary>
    /// Implements the <c>position-visibility</c> CSS property for anchor-positioned
    /// elements.  Hides elements when their anchor is not visible (scrolled out,
    /// CSS <c>visibility: hidden</c>) or does not exist.
    /// </summary>
    private void ResolvePositionVisibility(
        Broiler.Dom.DomElement root,
        Dictionary<string, AnchorInfo> anchorRegistry)
    {
        ResolvePositionVisibilityTree(root, anchorRegistry);
    }
    private void ResolvePositionVisibilityTree(
        Broiler.Dom.DomElement el,
        Dictionary<string, AnchorInfo> anchorRegistry)
    {
        if (!IsText(el))
        {
            var props = GetComputedProps(el);
            string? posVis = props.GetValueOrDefault("position-visibility");
            string? posAnchor = props.GetValueOrDefault("position-anchor");

            // Position-area targets whose anchor is scrolled out of view must be
            // hidden — the position-visibility-initial reftest asserts this even
            // when position-visibility is unset (the reference paints nothing for
            // a target whose position-area anchor is scrolled off). Only apply
            // this default when the element uses both position-anchor AND
            // position-area, so raw anchor()-driven abspos targets (e.g. the
            // AnchorScrollTracking guards) keep their current always-visible
            // behaviour — Broiler's IsAnchorVisibleForTarget doesn't yet handle
            // sticky pinning or abspos anchors inside scrollers, and forcing
            // the check on them drops the target off-screen (WPT #1177).
            if (string.IsNullOrWhiteSpace(posVis) &&
                !string.IsNullOrWhiteSpace(posAnchor) &&
                !string.IsNullOrWhiteSpace(props.GetValueOrDefault("position-area")))
                posVis = "anchors-visible";

            if (!string.IsNullOrWhiteSpace(posVis) &&
                !posVis.Equals("always", StringComparison.OrdinalIgnoreCase))
            {
                if (posVis.Equals("anchors-visible", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(posAnchor))
                    {
                        var anchorEl = FindElementByAnchorName(posAnchor);
                        if (anchorEl != null && !IsAnchorVisibleForTarget(anchorEl, el))
                        {
                            InlineStyle(el)["display"] = "none";
                        }
                    }
                }
                else if (posVis.Equals("anchors-valid", StringComparison.OrdinalIgnoreCase))
                {
                    bool hasValidAnchor = false;

                    // Check explicit position-anchor reference.
                    if (!string.IsNullOrWhiteSpace(posAnchor) &&
                        anchorRegistry.ContainsKey(posAnchor))
                        hasValidAnchor = true;

                    // Check anchor() function references in CSS values.
                    if (!hasValidAnchor)
                    {
                        foreach (var kv in props)
                        {
                            if (kv.Value.Contains("anchor(", StringComparison.OrdinalIgnoreCase))
                            {
                                var m = AnchorFunctionPattern.Match(kv.Value);
                                if (m.Success)
                                {
                                    string name = m.Groups["name"].Value;
                                    if (anchorRegistry.ContainsKey(name))
                                    { hasValidAnchor = true; break; }
                                }
                            }
                        }
                    }

                    if (!hasValidAnchor)
                        InlineStyle(el)["display"] = "none";
                }
            }
        }

        // Snapshot before recursing: the live child list can be mutated mid-walk
        // (concurrent/lazy DOM edit) and throw, aborting resolution. SnapshotChildren
        // tolerates that — same idiom as the other anchor-resolver tree walks.
        foreach (var child in SnapshotChildren(el))
            ResolvePositionVisibilityTree(child, anchorRegistry);
    }
    /// <summary>
    /// Finds the <see cref="Broiler.Dom.DomElement"/> that has the given
    /// <c>anchor-name</c> (from CSS rules or inline styles).
    /// </summary>
    private Broiler.Dom.DomElement? FindElementByAnchorName(string anchorName)
    {
        foreach (var el in Elements)
        {
            if (IsText(el)) continue;
            // Check inline styles first.
            if (InlineStyle(el).TryGetValue("anchor-name", out var n) &&
                string.Equals(n.Trim(), anchorName, StringComparison.Ordinal))
                return el;
        }

        // Fall back to the shared cascade.
        foreach (var el in Elements)
        {
            if (IsText(el)) continue;
            var declarations = CollectMatchedRuleProperties(el);
            if (declarations.TryGetValue("anchor-name", out var name) &&
                string.Equals(name.Trim(), anchorName, StringComparison.Ordinal))
                return el;
        }
        return null;
    }
    /// <summary>
    /// Checks whether the anchor element is "visible" for the purposes of
    /// <c>position-visibility: anchors-visible</c>.  An anchor is not visible
    /// if it has <c>visibility: hidden</c> (or inherits it), or if it has been
    /// scrolled out of view in an ancestor scroll container.  However, when
    /// the scroll container is the containing block for the target element,
    /// the anchor is considered visible (per spec § position-visibility).
    /// </summary>
    private bool IsAnchorVisibleForTarget(Broiler.Dom.DomElement anchor, Broiler.Dom.DomElement target)
    {
        // Check CSS visibility on the anchor and its ancestors.
        if (HasInheritedVisibilityHidden(anchor))
            return false;

        // Find the containing block element for the target.
        var targetCB = FindContainingBlockElement(target);

        // Walk from the anchor upward looking for scroll containers.
        var el = ParentEl(anchor);
        while (el != null)
        {
            var props = GetComputedProps(el);
            bool isScrollContainer = HasOverflowClipping(props);

            if (isScrollContainer)
            {
                // When the scroll container IS the containing block for the
                // target, there are no intervening clips → anchor is visible.
                if (el == targetCB)
                    return true;

                // Check if the anchor is scrolled out of this container.
                if (GetElementRuntimeState(el).Scroll.Top.TryGet(out var st) &&
                    st is double scrollTop && scrollTop > 0)
                {
                    double containerH =
                        TryParsePx(props.GetValueOrDefault("height")) ?? 0;
                    double anchorOffset =
                        ComputeNaturalOffsetInContainer(anchor, el);
                    double anchorH =
                        TryParsePx(GetComputedProps(anchor)
                            .GetValueOrDefault("height")) ?? 0;

                    // Anchor is NOT visible if entirely above or below the
                    // visible region.
                    if (anchorOffset + anchorH <= scrollTop ||
                        anchorOffset >= scrollTop + containerH)
                        return false;
                }
            }

            el = ParentEl(el);
        }

        return true;
    }
    /// <summary>
    /// Checks whether the element or any ancestor has <c>visibility: hidden</c>.
    /// </summary>
    private bool HasInheritedVisibilityHidden(Broiler.Dom.DomElement el)
    {
        var current = el;
        while (current != null)
        {
            var props = GetComputedProps(current);
            if (props.TryGetValue("visibility", out var v) &&
                v.Equals("hidden", StringComparison.OrdinalIgnoreCase))
                return true;
            current = ParentEl(current);
        }
        return false;
    }
    /// <summary>
    /// Computes the vertical offset of <paramref name="el"/> relative to the
    /// <paramref name="container"/> by summing heights of preceding siblings
    /// and ancestor margins/padding up to the container.
    /// </summary>
    private double ComputeNaturalOffsetInContainer(Broiler.Dom.DomElement el, Broiler.Dom.DomElement container)
    {
        double offset = 0;
        var current = el;
        while (current != null && current != container)
        {
            offset += ComputePrecedingSiblingHeights(current);
            var props = GetComputedProps(current);
            offset += TryParsePx(props.GetValueOrDefault("margin-top")) ?? 0;
            current = ParentEl(current);
            if (current != null && current != container)
            {
                var cProps = GetComputedProps(current);
                offset += TryParsePx(cProps.GetValueOrDefault("padding-top")) ?? 0;
                offset += TryParsePx(cProps.GetValueOrDefault("border-top-width")) ?? 0;
            }
        }
        return offset;
    }
    /// <summary>
    /// Finds the nearest positioned ancestor that serves as the containing
    /// block for an absolutely positioned element.
    /// </summary>
    private Broiler.Dom.DomElement? FindContainingBlockElement(Broiler.Dom.DomElement el)
    {
        var parent = ParentEl(el);
        while (parent != null)
        {
            var pProps = GetComputedProps(parent);
            if (EstablishesContainingBlock(pProps))
                return parent;
            parent = ParentEl(parent);
        }
        return null;
    }
    /// <summary>
    /// Finds the containing block for the anchor referenced by the target element.
    /// The anchor's CB is typically the same as the target's CB when both are
    /// inside the same positioned ancestor.
    /// </summary>
    private Broiler.Dom.DomElement? FindAnchorContainingBlock(Broiler.Dom.DomElement target, Broiler.Dom.DomElement targetCB)
    {
        // Find the anchor element by looking at the target's position-anchor.
        var cssProps = GetComputedProps(target);
        string? posAnchor = cssProps.GetValueOrDefault("position-anchor");
        if (string.IsNullOrWhiteSpace(posAnchor)) return null;

        var anchorEl = FindElementByAnchorName(posAnchor);
        if (anchorEl == null) return null;

        return FindContainingBlockElement(anchorEl);
    }
}
