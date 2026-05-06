using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

/// <summary>
/// Resolves CSS anchor positioning — for elements that use <c>anchor()</c>
/// functions, computes the anchored position from the target anchor element's
/// known CSS position and dimensions and writes the resolved pixel values as
/// inline styles.  Also inserts a backdrop element for modal <c>&lt;dialog&gt;</c>
/// elements.  This allows the static Broiler renderer to produce the correct
/// visual output for tests that rely on CSS anchor positioning (e.g. WPT
/// <c>anchor-position-top-layer-007.html</c>).
/// </summary>
public sealed partial class DomBridge
{
    /// <summary>
    /// Resolves <c>anchor()</c> function values and inserts <c>::backdrop</c>
    /// placeholder elements for modal dialogs.  Must be called after script
    /// execution and before serialization.
    /// </summary>
    /// <param name="viewportWidth">Viewport width in pixels (default 1024).</param>
    /// <param name="viewportHeight">Viewport height in pixels (default 768).</param>
    public void ResolveAnchorPositions(int viewportWidth = 1024, int viewportHeight = 768)
    {
        // 0. Apply UA default position:fixed to modal dialogs before anchor
        //    resolution, since browsers treat top-layer elements as fixed.
        ApplyDialogUAPositioning(DocumentElement);

        // 1. Build an anchor registry from CSS rules with anchor-name.
        var anchorRegistry = new Dictionary<string, AnchorInfo>(StringComparer.Ordinal);
        BuildAnchorRegistry(anchorRegistry);

        // Also register anchors from inline styles (e.g. set via JS).
        BuildInlineAnchorRegistry(anchorRegistry);

        // 1b. Parse @position-try at-rules from stylesheets.
        var positionTryRules = ParsePositionTryRules();

        // 2. Resolve anchor() function values on elements.
        ResolveAnchorFunctions(DocumentElement, anchorRegistry);

        // 3. Resolve position-area values on anchored elements.
        //    Collects scroll containers that need position:relative but
        //    defers adding it until after position-visibility resolution,
        //    so IsAnchorVisibleForTarget is not affected by the new CB.
        var scrollContainersNeedingRelative = new HashSet<DomElement>();
        var deferredDomMoves = new List<(DomElement element, DomElement oldParent, DomElement newParent)>();
        ResolvePositionAreaValues(
            DocumentElement, anchorRegistry, scrollContainersNeedingRelative,
            deferredDomMoves);

        // 3a2. Resolve align-self/justify-self: anchor-center on elements
        //      that have position-anchor but no position-area.
        ResolveAnchorCenter(DocumentElement, anchorRegistry);

        // 3b. Resolve position-try-fallbacks for elements whose base
        //     style overflows the containing block.
        ResolvePositionTryFallbacks(DocumentElement, anchorRegistry, positionTryRules);

        // 3c. Resolve position-visibility: hide anchor-positioned elements
        //     whose anchor is not visible or does not exist.
        ResolvePositionVisibility(DocumentElement, anchorRegistry);

        // 3d. Apply deferred DOM moves (inline CB → block ancestor promotion).
        //     Must be done after all position-area resolution is complete
        //     to avoid collection modification during traversal.
        foreach (var (el, oldParent, newParent) in deferredDomMoves)
        {
            oldParent.Children.Remove(el);
            newParent.Children.Add(el);
        }

        // 3d2. Promote any remaining absolutely positioned children of
        //      inline CBs to the block-level ancestor.  This handles
        //      non-position-area elements (like anchor elements) that
        //      the Broiler renderer can't place inside inline boxes.
        PromoteAbsPosFromInlineCBs(DocumentElement);

        // 3e. Now apply deferred position:relative to scroll containers
        //     used as containing blocks by position-area.
        foreach (var sc in scrollContainersNeedingRelative)
        {
            var scProps = GetComputedProps(sc);
            bool alreadyPositioned =
                scProps.TryGetValue("position", out var scPos) &&
                (scPos == "relative" || scPos == "absolute" ||
                 scPos == "fixed" || scPos == "sticky");
            if (!alreadyPositioned)
                sc.Style["position"] = "relative";
        }

        // 4. Insert backdrop elements for modal dialogs.
        InsertDialogBackdrops(DocumentElement, viewportWidth, viewportHeight);

        // 5. Ensure fixed-position elements from CSS have explicit pixel
        //    dimensions (the Broiler renderer does not resolve width/height
        //    from opposing inset values).
        ResolveFixedPositionSizing(viewportWidth, viewportHeight);

        // 6. Ensure elements that establish containing blocks via non-position
        //    properties (contain:layout, transform) get position:relative so the
        //    Broiler renderer treats them as containing blocks for abspos children.
        EnsureContainingBlockPositioning(DocumentElement);

        // 7. Strip CSS rules with unsupported properties (anchor(), inset,
        //    anchor-name) from the stylesheet so the renderer doesn't
        //    misinterpret them.
        NeutralizeStyleElementsForAnchorRules(DocumentElement);

        // 7a. Persist active visual-viewport pinch-zoom state into the DOM so
        //     the static renderer can reproduce zoomed fixed-position pages.
        ApplyVisualViewportSerializationState();

        // 8. Apply scroll simulation: shift content in scroll containers
        //    where JavaScript set scrollTop/scrollLeft to match Chromium output.
        ApplyScrollSimulation(DocumentElement);
    }

    private void ApplyVisualViewportSerializationState()
    {
        if (!HasActiveVisualViewport())
            return;

        var scale = GetVisualViewportScale();
        if (!double.IsFinite(scale) || scale <= 1.0001)
            return;

        var combinedZoom = GetUsedZoomForElement(DocumentElement) * scale;
        DocumentElement.Style["zoom"] = combinedZoom.ToString("0.###", CultureInfo.InvariantCulture);
        DocumentElement.DomProperties["_scrollLeft"] = GetVisualViewportPageOffset(vertical: false);
        DocumentElement.DomProperties["_scrollTop"] = GetVisualViewportPageOffset(vertical: true);
    }

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
            var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (sel, _, decls) in CssRules)
            {
                if (MatchesSelector(el, sel))
                    foreach (var kv in decls)
                        props[kv.Key] = kv.Value;
            }
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
            var cssProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (selector, _, declarations) in CssRules)
            {
                if (MatchesSelector(el, selector))
                    foreach (var kv in declarations)
                        cssProps[kv.Key] = kv.Value;
            }
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

        foreach (var child in el.Children)
            ResolveFixedPositionSizingInTree(child, vpW, vpH);
    }

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
            if (el.DomProperties.TryGetValue("_scrollTop", out var st) && st is double stv)
                scrollTop = stv;
            if (el.DomProperties.TryGetValue("_scrollLeft", out var sl) && sl is double slv)
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
                    var wrapper = new DomElement("div", null, null, "")
                    {
                        Parent = el,
                    };
                    wrapper.Style["position"] = "relative";
                    if (scrollTop != 0)
                        wrapper.Style["top"] =
                            $"{(-scrollTop).ToString(CultureInfo.InvariantCulture)}px";
                    if (scrollLeft != 0)
                        wrapper.Style["left"] =
                            $"{(-scrollLeft).ToString(CultureInfo.InvariantCulture)}px";

                    var originalChildren = el.Children.ToList();
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
                        foreach (var child in wrapper.Children)
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
        foreach (var child in parent.Children)
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

    // -----------------------------------------------------------------
    // position-visibility resolution
    // -----------------------------------------------------------------

    /// <summary>
    /// Implements the <c>position-visibility</c> CSS property for anchor-positioned
    /// elements.  Hides elements when their anchor is not visible (scrolled out,
    /// CSS <c>visibility: hidden</c>) or does not exist.
    /// </summary>
    private void ResolvePositionVisibility(
        DomElement root,
        Dictionary<string, AnchorInfo> anchorRegistry)
    {
        ResolvePositionVisibilityTree(root, anchorRegistry);
    }

    private void ResolvePositionVisibilityTree(
        DomElement el,
        Dictionary<string, AnchorInfo> anchorRegistry)
    {
        if (!el.IsTextNode)
        {
            var props = GetComputedProps(el);
            string? posVis = props.GetValueOrDefault("position-visibility");
            string? posAnchor = props.GetValueOrDefault("position-anchor");

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
                            el.Style["display"] = "none";
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
                        el.Style["display"] = "none";
                }
            }
        }

        foreach (var child in el.Children)
            ResolvePositionVisibilityTree(child, anchorRegistry);
    }

    /// <summary>
    /// Finds the <see cref="DomElement"/> that has the given
    /// <c>anchor-name</c> (from CSS rules or inline styles).
    /// </summary>
    private DomElement? FindElementByAnchorName(string anchorName)
    {
        foreach (var el in _elements)
        {
            if (el.IsTextNode) continue;
            // Check inline styles first.
            if (el.Style.TryGetValue("anchor-name", out var n) &&
                string.Equals(n.Trim(), anchorName, StringComparison.Ordinal))
                return el;
        }

        // Fall back to CSS rules.
        foreach (var el in _elements)
        {
            if (el.IsTextNode) continue;
            foreach (var (sel, _, decls) in CssRules)
            {
                if (MatchesSelector(el, sel) &&
                    decls.TryGetValue("anchor-name", out var name) &&
                    string.Equals(name.Trim(), anchorName, StringComparison.Ordinal))
                    return el;
            }
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
    private bool IsAnchorVisibleForTarget(DomElement anchor, DomElement target)
    {
        // Check CSS visibility on the anchor and its ancestors.
        if (HasInheritedVisibilityHidden(anchor))
            return false;

        // Find the containing block element for the target.
        var targetCB = FindContainingBlockElement(target);

        // Walk from the anchor upward looking for scroll containers.
        var el = anchor.Parent;
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
                if (el.DomProperties.TryGetValue("_scrollTop", out var st) &&
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

            el = el.Parent;
        }

        return true;
    }

    /// <summary>
    /// Checks whether the element or any ancestor has <c>visibility: hidden</c>.
    /// </summary>
    private bool HasInheritedVisibilityHidden(DomElement el)
    {
        var current = el;
        while (current != null)
        {
            var props = GetComputedProps(current);
            if (props.TryGetValue("visibility", out var v) &&
                v.Equals("hidden", StringComparison.OrdinalIgnoreCase))
                return true;
            current = current.Parent;
        }
        return false;
    }

    /// <summary>
    /// Computes the vertical offset of <paramref name="el"/> relative to the
    /// <paramref name="container"/> by summing heights of preceding siblings
    /// and ancestor margins/padding up to the container.
    /// </summary>
    private double ComputeNaturalOffsetInContainer(DomElement el, DomElement container)
    {
        double offset = 0;
        var current = el;
        while (current != null && current != container)
        {
            offset += ComputePrecedingSiblingHeights(current);
            var props = GetComputedProps(current);
            offset += TryParsePx(props.GetValueOrDefault("margin-top")) ?? 0;
            current = current.Parent;
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
    private DomElement? FindContainingBlockElement(DomElement el)
    {
        var parent = el.Parent;
        while (parent != null)
        {
            var pProps = GetComputedProps(parent);
            if (EstablishesContainingBlock(pProps))
                return parent;
            parent = parent.Parent;
        }
        return null;
    }

    /// <summary>
    /// Finds the containing block for the anchor referenced by the target element.
    /// The anchor's CB is typically the same as the target's CB when both are
    /// inside the same positioned ancestor.
    /// </summary>
    private DomElement? FindAnchorContainingBlock(DomElement target, DomElement targetCB)
    {
        // Find the anchor element by looking at the target's position-anchor.
        var cssProps = GetComputedProps(target);
        string? posAnchor = cssProps.GetValueOrDefault("position-anchor");
        if (string.IsNullOrWhiteSpace(posAnchor)) return null;

        var anchorEl = FindElementByAnchorName(posAnchor);
        if (anchorEl == null) return null;

        return FindContainingBlockElement(anchorEl);
    }

    // -----------------------------------------------------------------
    // Strip unsupported CSS rules from <style> elements
    // -----------------------------------------------------------------

    /// <summary>
    /// Rewrites <c>&lt;style&gt;</c> element text content to remove rules
    /// that contain <c>anchor()</c>, <c>anchor-name</c>, or <c>inset</c>
    /// properties.  This prevents the renderer from applying unsupported CSS
    /// that would conflict with the resolved inline styles.
    /// </summary>
    private static void NeutralizeStyleElementsForAnchorRules(DomElement root)
    {
        if (string.Equals(root.TagName, "style", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var child in root.Children)
            {
                if (child.IsTextNode && !string.IsNullOrEmpty(child.TextContent))
                    child.TextContent = RemoveUnsupportedCssRules(child.TextContent);
            }
        }

        foreach (var child in root.Children)
            NeutralizeStyleElementsForAnchorRules(child);
    }

    private static readonly Regex CssRuleBlockPattern = new(
        @"(?<selector>[^{}@]+)\{(?<body>[^}]*)\}",
        RegexOptions.Compiled);

    /// <summary>
    /// Matches <c>@position-try</c> at-rules (with their full block).
    /// </summary>
    private static readonly Regex PositionTryAtRulePattern = new(
        @"@position-try\s+[^{]+\{[^}]*\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Property names that are anchor-positioning-specific and should be
    /// stripped from CSS rule bodies after the DomBridge has resolved them
    /// into inline styles.  Properties whose values contain <c>anchor(</c>
    /// or <c>anchor-size(</c> are also stripped (matched separately).
    /// </summary>
    private static readonly string[] UnsupportedPropertyNames =
    {
        "anchor-name",
        "position-area",
        "position-anchor",
        "position-try-fallbacks",
        "position-try",
        "position-visibility",
    };

    private static string RemoveUnsupportedCssRules(string css)
    {
        // 1. Remove @position-try at-rules entirely.
        css = PositionTryAtRulePattern.Replace(css, string.Empty);

        // 2. Within each rule block, strip only the unsupported individual
        //    properties while keeping all other declarations intact.
        css = CssRuleBlockPattern.Replace(css, m =>
        {
            var body = m.Groups["body"].Value;

            // Quick check: if no unsupported properties exist, return as-is.
            bool hasUnsupported = false;
            foreach (var propName in UnsupportedPropertyNames)
            {
                if (body.Contains(propName, StringComparison.OrdinalIgnoreCase))
                { hasUnsupported = true; break; }
            }
            if (!hasUnsupported &&
                !body.Contains("anchor(", StringComparison.OrdinalIgnoreCase) &&
                !body.Contains("anchor-size(", StringComparison.OrdinalIgnoreCase))
            {
                return m.Value;
            }

            // If the rule contained position-area or position-anchor, the
            // DomBridge has resolved the element's position to explicit
            // inline pixel values.  Strip layout/sizing properties from
            // the CSS rule so they don't conflict with the inline values.
            bool hasPositionResolution =
                body.Contains("position-area", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("position-anchor", StringComparison.OrdinalIgnoreCase);

            // Strip unsupported properties from the body.
            var cleanedBody = StripUnsupportedProperties(body, hasPositionResolution);

            // If the rule body is now empty, remove the entire rule.
            if (string.IsNullOrWhiteSpace(cleanedBody))
                return string.Empty;

            return m.Groups["selector"].Value + "{" + cleanedBody + "}";
        });

        return css;
    }

    /// <summary>
    /// Layout properties that should also be stripped when the DomBridge has
    /// resolved position-area or position-anchor to inline pixel values.
    /// These would otherwise conflict with the DomBridge-computed values.
    /// </summary>
    private static readonly HashSet<string> PositionResolvedProperties = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "position", "width", "height", "top", "left", "right", "bottom",
        "inset", "inset-block", "inset-inline",
        "inset-block-start", "inset-block-end",
        "inset-inline-start", "inset-inline-end",
        "align-self", "justify-self",
        "grid-column", "grid-row", "grid-area",
        "grid-column-start", "grid-column-end",
        "grid-row-start", "grid-row-end",
    };

    /// <summary>
    /// Removes individual CSS declarations that use anchor-positioning
    /// properties from a rule body string, keeping all other declarations.
    /// When <paramref name="stripPositionProps"/> is true, also strips
    /// layout properties that would conflict with DomBridge-resolved values.
    /// </summary>
    private static string StripUnsupportedProperties(string body, bool stripPositionProps)
    {
        var sb = new System.Text.StringBuilder();
        // Split on ';' to get individual declarations.
        var declarations = body.Split(';');
        foreach (var decl in declarations)
        {
            var trimmed = decl.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Check if this declaration uses an unsupported property name.
            var colonIdx = trimmed.IndexOf(':');
            if (colonIdx < 0)
            {
                sb.Append(trimmed).Append(';');
                continue;
            }

            var propName = trimmed[..colonIdx].Trim();
            var propValue = trimmed[(colonIdx + 1)..].Trim();

            bool isUnsupported = false;
            foreach (var unsupported in UnsupportedPropertyNames)
            {
                if (propName.Equals(unsupported, StringComparison.OrdinalIgnoreCase))
                { isUnsupported = true; break; }
            }

            // Also strip declarations whose values contain anchor() or
            // anchor-size() function calls.
            if (!isUnsupported &&
                (propValue.Contains("anchor(", StringComparison.OrdinalIgnoreCase) ||
                 propValue.Contains("anchor-size(", StringComparison.OrdinalIgnoreCase)))
            {
                isUnsupported = true;
            }

            // Strip layout/sizing properties from position-area resolved rules.
            if (!isUnsupported && stripPositionProps &&
                PositionResolvedProperties.Contains(propName))
            {
                isUnsupported = true;
            }

            if (!isUnsupported)
                sb.Append(' ').Append(trimmed).Append(';');
        }

        return sb.ToString();
    }

    // -----------------------------------------------------------------
    // Anchor registry
    // -----------------------------------------------------------------

    private sealed record AnchorInfo(
        double Top, double Left, double Width, double Height,
        DomElement? SourceElement = null)
    {
        public double Right => Left + Width;
        public double Bottom => Top + Height;
    }

    private void BuildAnchorRegistry(Dictionary<string, AnchorInfo> registry)
    {
        foreach (var (selector, _, declarations) in CssRules)
        {
            if (!declarations.TryGetValue("anchor-name", out var anchorName))
                continue;

            foreach (var el in _elements)
            {
                if (!MatchesSelector(el, selector))
                    continue;

                var box = ComputeElementBox(el);
                if (box != null)
                    registry[anchorName] = box with { SourceElement = el };
            }
        }
    }

    private AnchorInfo? ComputeElementBox(DomElement element)
    {
        var props = GetComputedProps(element);

        double width = TryParsePx(props.GetValueOrDefault("width")) ?? 0;
        double height = TryParsePx(props.GetValueOrDefault("height")) ?? 0;

        string? position = props.GetValueOrDefault("position");
        bool isPositioned = position == "absolute" || position == "fixed";

        // For absolutely positioned elements, use their explicit insets.
        if (isPositioned)
        {
            double? topPx = TryParsePx(props.GetValueOrDefault("top"));
            double? leftPx = TryParsePx(props.GetValueOrDefault("left"));
            double? rightPx = TryParsePx(props.GetValueOrDefault("right"));
            double? bottomPx = TryParsePx(props.GetValueOrDefault("bottom"));

            double top = topPx ?? 0;
            double left = leftPx ?? 0;

            // When both left and right are specified without explicit width,
            // derive width from the containing block dimensions.
            if (width == 0 && leftPx.HasValue && rightPx.HasValue)
            {
                double cbW = FindContainingBlockWidth(element);
                width = cbW - leftPx.Value - rightPx.Value;
                if (width < 0) width = 0;
            }

            // When both top and bottom are specified without explicit height,
            // derive height from the containing block dimensions.
            if (height == 0 && topPx.HasValue && bottomPx.HasValue)
            {
                double cbH = FindContainingBlockHeight(element);
                height = cbH - topPx.Value - bottomPx.Value;
                if (height < 0) height = 0;
            }

            // When only right/bottom are specified, compute left/top from
            // the containing block dimensions.
            if (leftPx == null && rightPx.HasValue)
            {
                double cbW = FindContainingBlockWidth(element);
                left = cbW - rightPx.Value - width;
            }
            if (topPx == null && bottomPx.HasValue)
            {
                double cbH = FindContainingBlockHeight(element);
                top = cbH - bottomPx.Value - height;
            }

            return new AnchorInfo(top, left, width, height);
        }

        // For normal-flow elements, accumulate offsets from margins, padding,
        // borders, and preceding sibling heights up the ancestor chain.
        double marginLeft = TryParsePx(props.GetValueOrDefault("margin-left")) ?? 0;
        double marginTop = TryParsePx(props.GetValueOrDefault("margin-top")) ?? 0;
        double marginRight = TryParsePx(props.GetValueOrDefault("margin-right")) ?? 0;
        ParseMarginShorthand(props, ref marginLeft, ref marginTop, ref marginRight);

        double accLeft = marginLeft;
        double accTop = marginTop;

        // Add height of preceding siblings (vertical stacking in normal flow).
        accTop += ComputePrecedingSiblingHeights(element);

        // Walk up ancestors to accumulate margins, padding, and borders.
        var ancestor = element.Parent;
        while (ancestor != null)
        {
            var ancProps = GetComputedProps(ancestor);

            // Check if ancestor establishes a CB — if so, stop here.
            if (EstablishesContainingBlock(ancProps))
            {
                accLeft += TryParsePx(ancProps.GetValueOrDefault("padding-left")) ?? 0;
                accTop += TryParsePx(ancProps.GetValueOrDefault("padding-top")) ?? 0;
                accLeft += TryParsePx(ancProps.GetValueOrDefault("border-left-width")) ?? 0;
                accTop += TryParsePx(ancProps.GetValueOrDefault("border-top-width")) ?? 0;
                break;
            }

            // Accumulate ancestor margin + padding + border.
            double ancML = TryParsePx(ancProps.GetValueOrDefault("margin-left")) ?? 0;
            double ancMT = TryParsePx(ancProps.GetValueOrDefault("margin-top")) ?? 0;
            double ancMR = 0;
            ParseMarginShorthand(ancProps, ref ancML, ref ancMT, ref ancMR);

            // Apply UA default body margin (8px) if body has no explicit margin.
            if (string.Equals(ancestor.TagName, "body", StringComparison.OrdinalIgnoreCase) &&
                ancML == 0 && ancMT == 0 &&
                !ancProps.ContainsKey("margin") &&
                !ancProps.ContainsKey("margin-left") &&
                !ancProps.ContainsKey("margin-top"))
            {
                ancML = 8;
                ancMT = 8;
                ancMR = 8;
            }

            accLeft += ancML;
            accTop += ancMT;
            accLeft += TryParsePx(ancProps.GetValueOrDefault("padding-left")) ?? 0;
            accTop += TryParsePx(ancProps.GetValueOrDefault("padding-top")) ?? 0;
            accLeft += TryParsePx(ancProps.GetValueOrDefault("border-left-width")) ?? 0;
            accTop += TryParsePx(ancProps.GetValueOrDefault("border-top-width")) ?? 0;
            accTop += ComputePrecedingSiblingHeights(ancestor);

            ancestor = ancestor.Parent;
        }

        // For block-level elements without explicit width, compute width
        // from the containing block content width minus horizontal margins.
        if (width == 0)
        {
            string? display = props.GetValueOrDefault("display");
            bool isInline = display != null &&
                (display.Contains("inline", StringComparison.OrdinalIgnoreCase) &&
                 !display.Contains("inline-block", StringComparison.OrdinalIgnoreCase));

            if (!isInline)
            {
                double cbWidth = FindContainingBlockWidth(element);
                width = cbWidth - marginLeft - marginRight;
                if (width < 0) width = 0;
            }
        }

        return new AnchorInfo(accTop, accLeft, width, height);
    }

    /// <summary>
    /// Parses the 'margin' shorthand into individual margin values,
    /// only overwriting values that are still at their defaults (0).
    /// </summary>
    private static void ParseMarginShorthand(
        Dictionary<string, string> props,
        ref double marginLeft, ref double marginTop, ref double marginRight)
    {
        if (marginLeft == 0 && marginTop == 0 && marginRight == 0 &&
            props.TryGetValue("margin", out var marginShorthand))
        {
            var parts = marginShorthand.Trim().Split(null as char[], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1)
                marginTop = TryParsePx(parts[0]) ?? 0;
            if (parts.Length >= 2)
            {
                marginRight = TryParsePx(parts[1]) ?? 0;
                marginLeft = TryParsePx(parts[1]) ?? 0;
            }
            else if (parts.Length == 1)
                marginLeft = marginRight = marginTop;
            if (parts.Length >= 4)
                marginLeft = TryParsePx(parts[3]) ?? 0;
        }
    }

    /// <summary>
    /// Gets computed CSS properties for an element (CSS rules + inline styles).
    /// </summary>
    private Dictionary<string, string> GetComputedProps(DomElement element)
    {
        if (_computedPropsCache.TryGetValue(element, out var cached))
            return cached;

        if (_computedPropsInProgress.TryGetValue(element, out var inProgress))
            return inProgress;

        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _computedPropsInProgress[element] = props;
        try
        {
            ApplyUserAgentDisplayDefaults(props, element);

            foreach (var (sel, _, decls) in CssRules)
            {
                if (MatchesSelector(element, sel))
                    foreach (var kv in decls)
                        props[kv.Key] = kv.Value;
            }
            foreach (var kv in element.Style)
                props[kv.Key] = kv.Value;

            ExpandCssShorthands(props);
            ResolveLengthAttrFunctions(props, element);
            ResolveExplicitInheritedValues(props, element);
            ApplyInheritedProperties(props, element);

            // Expand the inset shorthand → top, right, bottom, left so that
            // downstream code (ComputeElementBox, TryApplyFallback, etc.) can
            // read the individual inset properties directly.
            if (props.TryGetValue("inset", out var insetVal2))
            {
                var parts = insetVal2.Split(new[] { ' ', '\t' },
                    StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    string iTop = parts[0];
                    string iRight = parts.Length > 1 ? parts[1] : iTop;
                    string iBottom = parts.Length > 2 ? parts[2] : iTop;
                    string iLeft = parts.Length > 3 ? parts[3] : iRight;

                    if (!props.ContainsKey("top")) props["top"] = iTop;
                    if (!props.ContainsKey("right")) props["right"] = iRight;
                    if (!props.ContainsKey("bottom")) props["bottom"] = iBottom;
                    if (!props.ContainsKey("left")) props["left"] = iLeft;
                }
            }

            ApplyApproximateFormControlComputedSizes(props, element);
            ApplyLogicalSizeAliases(props);

            _computedPropsCache[element] = props;
            return props;
        }
        finally
        {
            _computedPropsInProgress.Remove(element);
        }
    }

    private void ResolveExplicitInheritedValues(Dictionary<string, string> props, DomElement element)
    {
        Dictionary<string, string>? parentProps = null;
        foreach (var key in props.Keys.ToList())
        {
            var value = props[key];
            if (!string.Equals(value?.Trim(), "inherit", StringComparison.OrdinalIgnoreCase))
                continue;

            if (element.Parent != null)
            {
                parentProps ??= GetComputedProps(element.Parent);
                if (parentProps.TryGetValue(key, out var parentValue) && !string.IsNullOrWhiteSpace(parentValue))
                {
                    props[key] = parentValue;
                    continue;
                }
            }

            if (CssInitialValues.TryGetValue(key, out var initialValue))
                props[key] = initialValue;
            else
                props.Remove(key);
        }
    }

    /// <summary>
    /// Computes the total height of preceding siblings in normal flow.
    /// </summary>
    private double ComputePrecedingSiblingHeights(DomElement element)
    {
        if (element.Parent == null) return 0;

        double totalHeight = 0;
        foreach (var sibling in element.Parent.Children)
        {
            if (sibling == element) break;
            if (sibling.IsTextNode) continue;

            var sibProps = GetComputedProps(sibling);
            string? sibPos = sibProps.GetValueOrDefault("position");
            if (sibPos == "absolute" || sibPos == "fixed") continue;

            double sibHeight = TryParsePx(sibProps.GetValueOrDefault("height")) ?? 0;
            double sibMT = TryParsePx(sibProps.GetValueOrDefault("margin-top")) ?? 0;
            double sibMB = TryParsePx(sibProps.GetValueOrDefault("margin-bottom")) ?? 0;
            double sibMR = 0;
            ParseMarginShorthand(sibProps, ref sibMT, ref sibMB, ref sibMR);

            totalHeight += sibHeight + sibMT + sibMB;
        }
        return totalHeight;
    }

    // -----------------------------------------------------------------
    // anchor() resolution
    // -----------------------------------------------------------------

    private static readonly Regex AnchorFunctionPattern = new(
        @"anchor\(\s*(?:(?<name>--[a-zA-Z0-9_-]+)\s+)?(?<edge>top|right|bottom|left|start|end|center)\s*(?:,\s*(?<fallback>[^)]+?))?\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AnchorSizeFunctionPattern = new(
        @"anchor-size\(\s*(?:(?<name>--[a-zA-Z0-9_-]+)\s+)?(?<dim>width|height|block|inline|self-block|self-inline)\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private void ResolveAnchorFunctions(
        DomElement element,
        Dictionary<string, AnchorInfo> anchorRegistry)
    {
        var cssProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (selector, _, declarations) in CssRules)
        {
            if (MatchesSelector(element, selector))
                foreach (var kv in declarations)
                    cssProps[kv.Key] = kv.Value;
        }

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
        foreach (var kv in element.Style)
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
                                     element.Style.GetValueOrDefault("position-anchor");

            // When the target element is fixed-positioned (e.g. top-layer dialog)
            // and the anchor is NOT fixed-positioned, anchor positions must be
            // adjusted by the document scroll offset so the anchor's viewport
            // position is used instead of its document position.
            bool targetIsFixed =
                (cssProps.GetValueOrDefault("position") ?? element.Style.GetValueOrDefault("position")) == "fixed" ||
                (element.DomProperties.TryGetValue("_modal", out var tModal) && tModal is true);
            double scrollAdjY = 0, scrollAdjX = 0;
            if (targetIsFixed)
            {
                var docEl = DocumentElement;
                if (docEl.DomProperties.TryGetValue("_scrollTop", out var stv) && stv is double scrollTop)
                    scrollAdjY = scrollTop;
                if (docEl.DomProperties.TryGetValue("_scrollLeft", out var slv) && slv is double scrollLeft)
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
                    double adjY = anchorIsFixed ? 0 : scrollAdjY;
                    double adjX = anchorIsFixed ? 0 : scrollAdjX;

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
                    element.Style[kv.Key] = resolved;
            }

            // Apply non-anchor CSS properties (e.g. position, margin).
            foreach (var kv in cssProps)
            {
                if (!kv.Value.Contains("anchor(", StringComparison.OrdinalIgnoreCase) &&
                    !kv.Value.Contains("anchor-size(", StringComparison.OrdinalIgnoreCase) &&
                    !element.Style.ContainsKey(kv.Key) &&
                    IsLayoutProperty(kv.Key))
                {
                    element.Style[kv.Key] = kv.Value;
                }
            }

            // Remove 'inset' shorthand.
            element.Style.Remove("inset");
        }

        // Resolve anchor-size() function calls in both CSS and inline styles.
        if (hasAnchorSizeRef)
        {
            ResolveAnchorSizeFunctions(element, cssProps, anchorRegistry);
        }

        foreach (var child in element.Children)
            ResolveAnchorFunctions(child, anchorRegistry);
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
                                 element.Style.GetValueOrDefault("position-anchor");

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
                element.Style[kv.Key] = ResolveValue(kv.Value);
            }
        }

        // Resolve in existing inline styles.
        var inlineKeys = new List<string>(element.Style.Keys);
        foreach (var key in inlineKeys)
        {
            if (element.Style[key].Contains("anchor-size(", StringComparison.OrdinalIgnoreCase))
            {
                element.Style[key] = ResolveValue(element.Style[key]);
            }
        }
    }

    // -----------------------------------------------------------------
    // @position-try parsing and fallback resolution
    // -----------------------------------------------------------------

    private static readonly Regex PositionTryParsePattern = new(
        @"@position-try\s+(?<name>--[a-zA-Z0-9_-]+)\s*\{(?<body>[^}]*)\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses all <c>@position-try</c> at-rules from <c>&lt;style&gt;</c>
    /// elements, returning a dictionary mapping rule name to its property
    /// declarations.
    /// </summary>
    private Dictionary<string, Dictionary<string, string>> ParsePositionTryRules()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        CollectPositionTryRulesFromTree(DocumentElement, result);
        return result;
    }

    private void CollectPositionTryRulesFromTree(
        DomElement el,
        Dictionary<string, Dictionary<string, string>> result)
    {
        if (string.Equals(el.TagName, "style", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var child in el.Children)
            {
                if (child.IsTextNode && !string.IsNullOrEmpty(child.TextContent))
                {
                    foreach (Match m in PositionTryParsePattern.Matches(child.TextContent))
                    {
                        var name = m.Groups["name"].Value;
                        var body = m.Groups["body"].Value;
                        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var decl in body.Split(';'))
                        {
                            var trimmed = decl.Trim();
                            if (string.IsNullOrEmpty(trimmed)) continue;
                            var colonIdx = trimmed.IndexOf(':');
                            if (colonIdx < 0) continue;
                            var propName = trimmed[..colonIdx].Trim();
                            var propValue = trimmed[(colonIdx + 1)..].Trim();
                            props[propName] = propValue;
                        }
                        result[name] = props;
                    }
                }
            }
        }

        foreach (var child in el.Children)
            CollectPositionTryRulesFromTree(child, result);
    }

    /// <summary>
    /// For elements with <c>position-try-fallbacks</c>, checks whether
    /// the base style overflows the containing block and applies the first
    /// non-overflowing fallback from the <c>@position-try</c> rules.
    /// </summary>
    private void ResolvePositionTryFallbacks(
        DomElement root,
        Dictionary<string, AnchorInfo> anchorRegistry,
        Dictionary<string, Dictionary<string, string>> positionTryRules)
    {
        ResolvePositionTryFallbacksTree(root, anchorRegistry, positionTryRules);
    }

    private void ResolvePositionTryFallbacksTree(
        DomElement element,
        Dictionary<string, AnchorInfo> anchorRegistry,
        Dictionary<string, Dictionary<string, string>> positionTryRules)
    {
        if (!element.IsTextNode &&
            !string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
        {
            // Collect all CSS + inline properties to find position-try-fallbacks.
            var cssProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (selector, _, declarations) in CssRules)
            {
                if (MatchesSelector(element, selector))
                    foreach (var kv in declarations)
                        cssProps[kv.Key] = kv.Value;
            }
            foreach (var kv in element.Style)
                cssProps[kv.Key] = kv.Value;

            string? fallbacks = cssProps.GetValueOrDefault("position-try-fallbacks") ??
                                cssProps.GetValueOrDefault("position-try");

            if (!string.IsNullOrWhiteSpace(fallbacks) && positionTryRules.Count > 0)
            {
                TryApplyFallback(element, cssProps, anchorRegistry, positionTryRules, fallbacks!);
            }
        }

        foreach (var child in element.Children)
            ResolvePositionTryFallbacksTree(child, anchorRegistry, positionTryRules);
    }

    private void TryApplyFallback(
        DomElement element,
        Dictionary<string, string> baseProps,
        Dictionary<string, AnchorInfo> anchorRegistry,
        Dictionary<string, Dictionary<string, string>> positionTryRules,
        string fallbackList)
    {
        // Get the containing block dimensions.
        double cbWidth = FindContainingBlockWidth(element);
        double cbHeight = FindContainingBlockHeight(element);

        // Check if the base style overflows the IMCB.
        double baseLeft = TryParsePx(element.Style.GetValueOrDefault("left")) ?? 0;
        double baseTop = TryParsePx(element.Style.GetValueOrDefault("top")) ?? 0;
        double baseRight = TryParsePx(element.Style.GetValueOrDefault("right")) ??
                           TryParsePx(baseProps.GetValueOrDefault("right")) ?? 0;
        double baseBottom = TryParsePx(element.Style.GetValueOrDefault("bottom")) ??
                            TryParsePx(baseProps.GetValueOrDefault("bottom")) ?? 0;
        double baseWidth = TryParsePx(element.Style.GetValueOrDefault("width")) ??
                           TryParsePx(baseProps.GetValueOrDefault("width")) ?? 0;
        double baseHeight = TryParsePx(element.Style.GetValueOrDefault("height")) ??
                            TryParsePx(baseProps.GetValueOrDefault("height")) ?? 0;

        // Compute IMCB (inset-modified containing block) dimensions.
        double imcbWidth = cbWidth - baseLeft - baseRight;
        double imcbHeight = cbHeight - baseTop - baseBottom;

        // Estimate content width for min-content/max-content.
        string? widthVal = baseProps.GetValueOrDefault("width");
        bool hasAutoWidth = widthVal == "min-content" || widthVal == "max-content" ||
                            widthVal == "auto" || widthVal == "fit-content";
        if (hasAutoWidth && baseWidth == 0)
        {
            // Estimate from child element widths.
            baseWidth = EstimateMinContentWidth(element);
        }

        bool baseOverflows = baseLeft < 0 || baseTop < 0 ||
                             baseLeft + baseWidth > cbWidth ||
                             baseTop + baseHeight > cbHeight ||
                             (imcbWidth < baseWidth && imcbWidth >= 0) ||
                             (imcbHeight < baseHeight && imcbHeight >= 0);

        if (!baseOverflows)
            return; // Base style fits; no fallback needed.

        // Parse the fallback list (comma-separated @position-try names).
        var names = fallbackList.Split(',').Select(n => n.Trim()).ToArray();

        // Get implicit anchor name from position-anchor.
        string? implicitAnchor = baseProps.GetValueOrDefault("position-anchor");

        foreach (var name in names)
        {
            if (!positionTryRules.TryGetValue(name, out var tryProps))
                continue;

            // Compute the element position/size with this fallback applied.
            // Start with the base properties, then overlay the try properties.
            var merged = new Dictionary<string, string>(baseProps, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in tryProps)
                merged[kv.Key] = kv.Value;

            // Handle "inset: auto" — reset all inset properties so the
            // base style's insets don't leak through to the fallback.
            if (merged.TryGetValue("inset", out var insetVal) &&
                insetVal.Trim() == "auto")
            {
                if (!tryProps.ContainsKey("left"))
                    merged["left"] = "auto";
                if (!tryProps.ContainsKey("right"))
                    merged["right"] = "auto";
                if (!tryProps.ContainsKey("top"))
                    merged["top"] = "auto";
                if (!tryProps.ContainsKey("bottom"))
                    merged["bottom"] = "auto";
            }

            // Resolve explicit width/height first so that subsequent
            // left-from-right and top-from-bottom calculations use
            // the correct element dimensions.
            double tryWidth = baseWidth, tryHeight = baseHeight;

            if (merged.TryGetValue("width", out var wv))
            {
                var w = TryParsePx(wv);
                if (w.HasValue) tryWidth = w.Value;
            }
            if (merged.TryGetValue("height", out var hv))
            {
                var h = TryParsePx(hv);
                if (h.HasValue) tryHeight = h.Value;
            }

            // Resolve any anchor() references in the try properties.
            double tryLeft = 0, tryTop = 0;

            if (merged.TryGetValue("left", out var lv) && lv != "auto")
            {
                var resolvedL = AnchorFunctionPattern.Replace(lv, m =>
                    ResolveAnchorEdge(m, anchorRegistry, "left", cbWidth, cbHeight, implicitAnchor));
                tryLeft = TryParsePx(resolvedL) ?? 0;
            }

            if (merged.TryGetValue("right", out var rv) && rv != "auto")
            {
                var resolvedR = AnchorFunctionPattern.Replace(rv, m =>
                    ResolveAnchorEdge(m, anchorRegistry, "right", cbWidth, cbHeight, implicitAnchor));
                var rightPx = TryParsePx(resolvedR);
                if (rightPx.HasValue)
                {
                    if (!merged.TryGetValue("left", out var leftV) ||
                        leftV == "auto" || string.IsNullOrEmpty(leftV))
                    {
                        // No left specified; compute left from right + width.
                        tryLeft = cbWidth - rightPx.Value - tryWidth;
                    }
                    else
                    {
                        // Both left and right specified; compute width.
                        tryWidth = cbWidth - tryLeft - rightPx.Value;
                    }
                }
            }

            if (merged.TryGetValue("top", out var tv) && tv != "auto")
            {
                var resolvedT = AnchorFunctionPattern.Replace(tv, m =>
                    ResolveAnchorEdge(m, anchorRegistry, "top", cbWidth, cbHeight, implicitAnchor));
                tryTop = TryParsePx(resolvedT) ?? 0;
            }

            if (merged.TryGetValue("bottom", out var bv) && bv != "auto")
            {
                var resolvedB = AnchorFunctionPattern.Replace(bv, m =>
                    ResolveAnchorEdge(m, anchorRegistry, "bottom", cbWidth, cbHeight, implicitAnchor));
                var bottomPx = TryParsePx(resolvedB);
                if (bottomPx.HasValue)
                {
                    if (!merged.TryGetValue("top", out var topV) ||
                        topV == "auto" || string.IsNullOrEmpty(topV))
                    {
                        tryTop = cbHeight - bottomPx.Value - tryHeight;
                    }
                    else
                    {
                        tryHeight = cbHeight - tryTop - bottomPx.Value;
                    }
                }
            }

            bool fits = tryLeft >= 0 && tryTop >= 0 &&
                        tryLeft + tryWidth <= cbWidth &&
                        tryTop + tryHeight <= cbHeight;

            if (fits)
            {
                // Apply the fallback: set resolved values as inline styles.
                element.Style["left"] = $"{tryLeft.ToString(CultureInfo.InvariantCulture)}px";
                element.Style["top"] = $"{tryTop.ToString(CultureInfo.InvariantCulture)}px";
                element.Style["width"] = $"{tryWidth.ToString(CultureInfo.InvariantCulture)}px";
                element.Style["height"] = $"{tryHeight.ToString(CultureInfo.InvariantCulture)}px";
                element.Style.Remove("right");
                element.Style.Remove("bottom");
                element.Style.Remove("inset");
                return;
            }
        }
    }

    /// <summary>
    /// Estimates the min-content width of an element by examining its
    /// children's explicit widths. This is a heuristic for elements
    /// with <c>width: min-content</c>.
    /// </summary>
    private double EstimateMinContentWidth(DomElement element)
    {
        double maxWidth = 0;
        foreach (var child in element.Children)
        {
            if (child.IsTextNode) continue;
            var childProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (sel, _, decls) in CssRules)
            {
                if (MatchesSelector(child, sel))
                    foreach (var kv in decls)
                        childProps[kv.Key] = kv.Value;
            }
            foreach (var kv in child.Style)
                childProps[kv.Key] = kv.Value;

            double childWidth = TryParsePx(childProps.GetValueOrDefault("width")) ?? 0;
            if (childWidth > maxWidth)
                maxWidth = childWidth;
        }
        return maxWidth;
    }

    private static string ResolveAnchorEdge(
        Match m, Dictionary<string, AnchorInfo> registry,
        string contextProp, double cbWidth, double cbHeight,
        string? implicitAnchor = null)
    {
        var anchorName = m.Groups["name"].Value;
        if (string.IsNullOrEmpty(anchorName))
            anchorName = implicitAnchor ?? string.Empty;
        var edge = m.Groups["edge"].Value.ToLowerInvariant();

        if (!registry.TryGetValue(anchorName, out var anchor))
            return "0px";

        double rawValue = edge switch
        {
            "top" => anchor.Top,
            "right" => anchor.Right,
            "bottom" => anchor.Bottom,
            "left" => anchor.Left,
            "center" => (anchor.Top + anchor.Bottom) / 2,
            _ => 0,
        };

        // For right/bottom properties, return distance from the opposite CB edge.
        double value = contextProp switch
        {
            "right" => cbWidth - rawValue,
            "bottom" => cbHeight - rawValue,
            _ => rawValue,
        };

        return $"{value.ToString(CultureInfo.InvariantCulture)}px";
    }

    // -----------------------------------------------------------------
    // Dialog UA default positioning
    // -----------------------------------------------------------------

    /// <summary>
    /// Applies the UA default <c>position: fixed</c> to modal dialog elements
    /// that don't already have an explicit position, matching browser behaviour
    /// where top-layer elements are always treated as fixed-positioned.
    /// Must be called <em>before</em> anchor resolution so that anchor()
    /// function values are resolved with the correct positioning context.
    /// </summary>
    private void ApplyDialogUAPositioning(DomElement root)
    {
        foreach (var el in _elements)
        {
            if (!string.Equals(el.TagName, "dialog", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!el.Attributes.ContainsKey("open"))
                continue;
            if (!(el.DomProperties.TryGetValue("_modal", out var m) && m is true))
                continue;

            // Check if position is already set (inline or CSS).
            // position:absolute dialogs keep their author position so that
            // scroll simulation can shift them, matching Chromium behaviour.
            var props = GetComputedProps(el);
            if (props.TryGetValue("position", out var pos) &&
                (pos == "fixed" || pos == "absolute"))
                continue;

            // Set position:fixed as UA default for modal dialogs that have
            // no explicit position, matching Chromium's top-layer behaviour.
            el.Style["position"] = "fixed";
        }
    }

    // -----------------------------------------------------------------
    // Dialog backdrop insertion
    // -----------------------------------------------------------------

    private void InsertDialogBackdrops(DomElement root, int vpW, int vpH)
    {
        var modals = new List<(DomElement dialog, DomElement parent)>();
        FindModalDialogs(root, modals);

        foreach (var (dialog, parent) in modals)
        {
            // Collect ::backdrop CSS properties for this dialog element.
            // Look for selectors ending with "::backdrop" that would match
            // the dialog (e.g. "dialog::backdrop", "#target::backdrop").
            var backdropBg = GetBackdropBackground(dialog);

            // Insert a backdrop div BEFORE the dialog.
            // Use 'position: fixed' with explicit pixel viewport dimensions
            // because the Broiler renderer cannot resolve opposing insets.
            var backdropStyle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["position"] = "fixed",
                ["top"] = "0",
                ["left"] = "0",
                ["width"] = $"{vpW}px",
                ["height"] = $"{vpH}px",
                ["background-color"] = backdropBg,
            };

            var backdrop = new DomElement(
                "div", null, null, string.Empty,
                style: backdropStyle);
            backdrop.Parent = parent;

            int idx = parent.Children.IndexOf(dialog);
            if (idx >= 0)
                parent.Children.Insert(idx, backdrop);

            // Ensure the dialog has UA default styles.
            // Check both inline styles and CSS rules before applying defaults.
            var dialogProps = GetComputedProps(dialog);
            if (!dialog.Style.ContainsKey("display"))
                dialog.Style["display"] = "block";
            if (!dialog.Style.ContainsKey("border") &&
                !dialogProps.ContainsKey("border") &&
                !dialogProps.ContainsKey("border-width"))
            {
                dialog.Style["border-width"] = "1px";
                dialog.Style["border-style"] = "solid";
                dialog.Style["border-color"] = "black";
            }
            if (!dialog.Style.ContainsKey("padding") &&
                !dialogProps.ContainsKey("padding"))
                dialog.Style["padding"] = "1em";
            if (!dialog.Style.ContainsKey("background") &&
                !dialog.Style.ContainsKey("background-color") &&
                !dialogProps.ContainsKey("background") &&
                !dialogProps.ContainsKey("background-color"))
                dialog.Style["background-color"] = "white";
        }
    }

    /// <summary>
    /// Determines the background color for a dialog's <c>::backdrop</c>
    /// pseudo-element by checking CSS rules for <c>::backdrop</c> selectors
    /// that match the given dialog element.
    /// </summary>
    private string GetBackdropBackground(DomElement dialog)
    {
        // Default backdrop color: pre-composited rgba(0,0,0,0.1) over white.
        // Alpha-blending: 255*(1-0.1) + 0*0.1 = 229.5 ≈ 229.
        const string defaultBg = "rgb(229, 229, 229)";

        foreach (var (selector, _, declarations) in CssRules)
        {
            // Check for selectors ending in ::backdrop
            if (!selector.Contains("::backdrop", StringComparison.OrdinalIgnoreCase))
                continue;

            // Extract the base selector before ::backdrop
            var parts = selector.Split("::backdrop", StringSplitOptions.None);
            if (parts.Length < 2) continue;

            string baseSelector = parts[0].Trim();

            // Check if the base selector matches this dialog
            bool matches = false;
            if (string.IsNullOrEmpty(baseSelector) ||
                string.Equals(baseSelector, "dialog", StringComparison.OrdinalIgnoreCase))
            {
                // Bare "::backdrop" or "dialog::backdrop" matches all dialogs
                matches = string.Equals(dialog.TagName, "dialog", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // Try matching specific selectors like "#target::backdrop"
                matches = MatchesSelector(dialog, baseSelector);
            }

            if (matches)
            {
                // Check for background or background-color in the declarations
                if (declarations.TryGetValue("background", out var bg))
                {
                    if (string.Equals(bg.Trim(), "transparent", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(bg.Trim(), "none", StringComparison.OrdinalIgnoreCase))
                        return "transparent";
                    return bg;
                }
                if (declarations.TryGetValue("background-color", out var bgColor))
                {
                    if (string.Equals(bgColor.Trim(), "transparent", StringComparison.OrdinalIgnoreCase))
                        return "transparent";
                    return bgColor;
                }
            }
        }

        return defaultBg;
    }

    /// <summary>
    /// Checks whether an anchor element is accessible from a target element,
    /// according to CSS Anchor Positioning top-layer visibility rules.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Non-top-layer elements cannot anchor to top-layer elements.</item>
    /// <item>A top-layer element can only anchor to top-layer elements that
    /// were added to the top layer <em>before</em> it (lower order).</item>
    /// <item>Non-top-layer anchors are always accessible.</item>
    /// </list>
    /// </remarks>
    private static bool IsAnchorAccessible(DomElement? anchorElement, DomElement targetElement)
    {
        if (anchorElement == null) return true;

        bool anchorIsTopLayer =
            anchorElement.DomProperties.TryGetValue("_modal", out var am) && am is true;
        bool targetIsTopLayer =
            targetElement.DomProperties.TryGetValue("_modal", out var tm) && tm is true;

        if (!anchorIsTopLayer)
            return true; // Non-top-layer anchors are accessible from anywhere.

        if (!targetIsTopLayer)
            return false; // Non-top-layer target cannot see top-layer anchor.

        // Both are in top layer — anchor must have been added BEFORE the target.
        int anchorOrder = anchorElement.DomProperties.TryGetValue("_topLayerOrder", out var ao) && ao is int aoi ? aoi : 0;
        int targetOrder = targetElement.DomProperties.TryGetValue("_topLayerOrder", out var to) && to is int toi ? toi : 0;

        return anchorOrder < targetOrder;
    }

    private static void FindModalDialogs(DomElement element, List<(DomElement, DomElement)> results)
    {
        if (string.Equals(element.TagName, "dialog", StringComparison.OrdinalIgnoreCase) &&
            element.Attributes.ContainsKey("open") &&
            element.DomProperties.TryGetValue("_modal", out var isModal) &&
            isModal is bool modal && modal &&
            element.Parent != null)
        {
            results.Add((element, element.Parent));
        }

        foreach (var child in element.Children)
            FindModalDialogs(child, results);
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static double? TryParsePx(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value!.Trim();
        if (v.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            v = v[..^2];
        // Don't parse pure numbers without px suffix if they contain '%'
        if (v.Contains('%')) return null;
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }

    /// <summary>
    /// Tries to parse a CSS percentage value (e.g. "50%") and returns
    /// the numeric value (e.g. 50.0).
    /// </summary>
    private static double? TryParsePercent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value!.Trim();
        if (!v.EndsWith('%')) return null;
        v = v[..^1];
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }

    /// <summary>
    /// Resolves a CSS value that may be a percentage or a pixel length.
    /// Percentages are resolved against <paramref name="reference"/>.
    /// Returns 0 for values that cannot be parsed.
    /// </summary>
    private static double ResolvePctOrPx(string value, double reference)
    {
        var pct = TryParsePercent(value);
        if (pct.HasValue)
            return reference * pct.Value / 100.0;
        return TryParsePx(value) ?? 0;
    }

    /// <summary>
    /// Returns true if the value contains a CSS percentage token.
    /// </summary>
    private static bool HasPercent(string? value)
    {
        return value != null && value.Contains('%');
    }

    /// <summary>
    /// Resolves a CSS border-width value from cascaded properties.
    /// Checks the individual property (e.g. "border-left-width") first,
    /// then falls back to the "border" shorthand.  The CSS keywords
    /// "thin", "medium", and "thick" are mapped to 1, 3, and 4 px
    /// respectively to match <see cref="CssValueParser.GetActualBorderWidth"/>.
    /// </summary>
    private static double ResolveBorderWidth(
        Dictionary<string, string> cssProps,
        string sideProperty,
        string shorthandProperty)
    {
        if (cssProps.TryGetValue(sideProperty, out var sideVal) && sideVal != null)
            return ResolveBorderKeywordOrPx(sideVal);

        // Try the border-width shorthand (1-4 values: top [right [bottom [left]]])
        if (cssProps.TryGetValue("border-width", out var bwVal) && bwVal != null)
        {
            var parts = bwVal.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int idx = sideProperty switch
            {
                "border-top-width" => 0,
                "border-right-width" => parts.Length > 1 ? 1 : 0,
                "border-bottom-width" => parts.Length > 2 ? 2 : 0,
                "border-left-width" => parts.Length > 3 ? 3 : (parts.Length > 1 ? 1 : 0),
                _ => 0
            };
            return ResolveBorderKeywordOrPx(parts[idx]);
        }

        // Fall back to the border shorthand (e.g. "solid")
        if (cssProps.TryGetValue(shorthandProperty, out var shortVal) && shortVal != null)
        {
            // If the shorthand contains an explicit width, use it; otherwise
            // the shorthand implies "medium" (3px).
            foreach (var part in shortVal.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var px = TryParsePx(part);
                if (px.HasValue)
                    return px.Value;
                if (part.Equals("thin", StringComparison.OrdinalIgnoreCase))
                    return 1;
                if (part.Equals("thick", StringComparison.OrdinalIgnoreCase))
                    return 4;
            }
            // "border: solid" (style only, no width) → medium = 3px
            bool hasStyle = false;
            foreach (var part in shortVal.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Equals("solid", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("dotted", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("dashed", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("double", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("groove", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("ridge", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("inset", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("outset", StringComparison.OrdinalIgnoreCase))
                {
                    hasStyle = true;
                    break;
                }
            }
            if (hasStyle)
                return 3; // medium
        }

        return 0;
    }

    /// <summary>Converts a CSS border-width keyword or pixel value to a number.</summary>
    private static double ResolveBorderKeywordOrPx(string value)
    {
        if (value.Equals("thin", StringComparison.OrdinalIgnoreCase)) return 1;
        if (value.Equals("medium", StringComparison.OrdinalIgnoreCase)) return 3;
        if (value.Equals("thick", StringComparison.OrdinalIgnoreCase)) return 4;
        return TryParsePx(value) ?? 0;
    }

    /// <summary>
    /// Resolves <c>align-self: anchor-center</c> and
    /// <c>justify-self: anchor-center</c> on elements that have
    /// <c>position-anchor</c> but no <c>position-area</c>.
    /// Centers the element on the anchor in the appropriate axis.
    /// </summary>
    private void ResolveAnchorCenter(
        DomElement element,
        Dictionary<string, AnchorInfo> anchorRegistry)
    {
        if (!element.IsTextNode)
        {
            var cssProps = GetComputedProps(element);

            string? positionAnchor = cssProps.GetValueOrDefault("position-anchor");
            string? positionArea = cssProps.GetValueOrDefault("position-area");
            string? alignSelf = cssProps.GetValueOrDefault("align-self");
            string? justifySelf = cssProps.GetValueOrDefault("justify-self");

            bool hasAnchorCenter =
                (alignSelf != null && alignSelf.Equals("anchor-center", StringComparison.OrdinalIgnoreCase)) ||
                (justifySelf != null && justifySelf.Equals("anchor-center", StringComparison.OrdinalIgnoreCase));

            if (hasAnchorCenter &&
                !string.IsNullOrWhiteSpace(positionAnchor) &&
                (string.IsNullOrWhiteSpace(positionArea) || positionArea == "none") &&
                anchorRegistry.TryGetValue(positionAnchor, out var anchor))
            {
                double elWidth = TryParsePx(cssProps.GetValueOrDefault("width")) ??
                                 TryParsePx(element.Style.GetValueOrDefault("width")) ?? 0;
                double elHeight = TryParsePx(cssProps.GetValueOrDefault("height")) ??
                                  TryParsePx(element.Style.GetValueOrDefault("height")) ?? 0;

                // align-self: anchor-center → center vertically on anchor
                if (alignSelf != null &&
                    alignSelf.Equals("anchor-center", StringComparison.OrdinalIgnoreCase))
                {
                    double anchorCenterY = anchor.Top + anchor.Height / 2.0;
                    double top = anchorCenterY - elHeight / 2.0;
                    element.Style["top"] = $"{top.ToString(CultureInfo.InvariantCulture)}px";
                }

                // justify-self: anchor-center → center horizontally on anchor
                if (justifySelf != null &&
                    justifySelf.Equals("anchor-center", StringComparison.OrdinalIgnoreCase))
                {
                    double anchorCenterX = anchor.Left + anchor.Width / 2.0;
                    double left = anchorCenterX - elWidth / 2.0;
                    element.Style["left"] = $"{left.ToString(CultureInfo.InvariantCulture)}px";
                }

                // Ensure the element has position:absolute.
                if (!cssProps.TryGetValue("position", out var pos) ||
                    pos == "static")
                {
                    element.Style["position"] = "absolute";
                }
            }
        }

        foreach (var child in element.Children)
            ResolveAnchorCenter(child, anchorRegistry);
    }

    /// <summary>
    /// Computes the offset within a position-area cell based on alignment.
    /// For "start" cells (top/left), the element is aligned to the end
    /// (nearest to the anchor). For "end" cells (bottom/right), aligned
    /// to the start. For "center" cells, centered.
    /// </summary>
    private static double ComputeAlignmentOffset(
        AxisSelection sel, double cellSize, double elementSize, bool isInlineAxis)
    {
        double slack = cellSize - elementSize;
        if (slack <= 0) return 0;

        switch (sel)
        {
            case AxisSelection.Start:
                // "top" or "left" cell: align towards the anchor (end of cell).
                return slack;
            case AxisSelection.End:
                // "bottom" or "right" cell: align towards the anchor (start of cell).
                return 0;
            case AxisSelection.Center:
                // "center" cell: center the element.
                return slack / 2;
            case AxisSelection.SpanStart:
            case AxisSelection.SpanEnd:
            case AxisSelection.SpanAll:
                // Spanning cells: align to start by default.
                return 0;
            default:
                return 0;
        }
    }

    // -----------------------------------------------------------------
    // Inline anchor registry (anchors set via JS style manipulation)
    // -----------------------------------------------------------------

    private void BuildInlineAnchorRegistry(Dictionary<string, AnchorInfo> registry)
    {
        foreach (var el in _elements)
        {
            if (el.Style.TryGetValue("anchor-name", out var anchorName) &&
                !string.IsNullOrWhiteSpace(anchorName))
            {
                var box = ComputeElementBoxWithContainer(el);
                if (box != null && !registry.ContainsKey(anchorName))
                    registry[anchorName] = box with { SourceElement = el };
            }
        }
    }

    /// <summary>
    /// Computes an element's box position relative to its positioned
    /// containing block, resolving <c>right</c> to <c>left</c> when needed.
    /// </summary>
    private AnchorInfo? ComputeElementBoxWithContainer(DomElement element)
    {
        // Delegate to the main ComputeElementBox which already handles
        // both positioned and normal-flow elements with ancestor offset
        // accumulation and block-width computation.
        return ComputeElementBox(element);
    }

    /// <summary>
    /// Finds the width of the nearest positioned ancestor (containing block)
    /// for an absolutely positioned element.
    /// </summary>
    private double FindContainingBlockWidth(DomElement element)
    {
        var parent = element.Parent;
        while (parent != null)
        {
            var parentProps = GetComputedProps(parent);

            if (EstablishesContainingBlock(parentProps))
            {
                double? explicitW = TryParsePx(parentProps.GetValueOrDefault("width"));
                if (explicitW == null)
                {
                    // Inline elements (e.g. <span>) with position:relative
                    // may not have explicit width. Estimate from content.
                    explicitW = EstimateInlineContentWidth(parent);
                }
                double w = explicitW ?? _viewportWidth;
                // Subtract padding from the CB width to get content width.
                w -= TryParsePx(parentProps.GetValueOrDefault("padding-left")) ?? 0;
                w -= TryParsePx(parentProps.GetValueOrDefault("padding-right")) ?? 0;
                return w;
            }
            parent = parent.Parent;
        }
        // No positioned ancestor found; use viewport width minus default body
        // margin (8px each side) as the effective content width for block layout.
        return _viewportWidth - 16;
    }

    /// <summary>
    /// Finds the height of the nearest positioned ancestor (containing block)
    /// for an absolutely positioned element.
    /// </summary>
    private double FindContainingBlockHeight(DomElement element)
    {
        var parent = element.Parent;
        while (parent != null)
        {
            var parentProps = GetComputedProps(parent);

            if (EstablishesContainingBlock(parentProps))
            {
                double? explicitH = TryParsePx(parentProps.GetValueOrDefault("height"));
                if (explicitH == null)
                {
                    // Inline elements may not have explicit height. Estimate from content.
                    explicitH = EstimateInlineContentHeight(parent);
                }
                double h = explicitH ?? _viewportHeight;
                h -= TryParsePx(parentProps.GetValueOrDefault("padding-top")) ?? 0;
                h -= TryParsePx(parentProps.GetValueOrDefault("padding-bottom")) ?? 0;
                return h;
            }
            parent = parent.Parent;
        }
        return _viewportHeight;
    }

    /// <summary>
    /// Returns the computed left margin of the <c>&lt;body&gt;</c> element
    /// (defaults to 8px per CSS 2 § UA stylesheet).
    /// </summary>
    private double FindBodyMarginLeft()
    {
        var body = FindBodyElement();
        if (body != null)
        {
            var props = GetComputedProps(body);
            return TryParsePx(props.GetValueOrDefault("margin-left")) ?? 8;
        }
        return 8;
    }

    /// <summary>
    /// Returns the computed top margin of the <c>&lt;body&gt;</c> element
    /// (defaults to 8px per CSS 2 § UA stylesheet).
    /// </summary>
    private double FindBodyMarginTop()
    {
        var body = FindBodyElement();
        if (body != null)
        {
            var props = GetComputedProps(body);
            return TryParsePx(props.GetValueOrDefault("margin-top")) ?? 8;
        }
        return 8;
    }

    private DomElement? FindBodyElement()
    {
        foreach (var el in _elements)
        {
            if (!el.IsTextNode &&
                string.Equals(el.TagName, "body", StringComparison.OrdinalIgnoreCase))
                return el;
        }
        return null;
    }

    /// <summary>
    /// Estimates the content width of an inline element (e.g. a positioned
    /// <c>&lt;span&gt;</c>) by examining its text content and child element widths.
    /// Returns <c>null</c> if the element is not an inline element.
    /// </summary>
    private double? EstimateInlineContentWidth(DomElement element)
    {
        string? display = GetComputedProps(element).GetValueOrDefault("display");
        bool isInline = IsInlineElement(element.TagName, display);
        if (!isInline) return null;

        double totalWidth = 0;
        var elProps = GetComputedProps(element);
        double fontSize = TryParsePx(elProps.GetValueOrDefault("font-size")) ?? 16;

        // Check parent font-size as well (inline elements inherit).
        if (element.Parent != null)
        {
            var parentProps = GetComputedProps(element.Parent);
            double parentFs = TryParsePx(parentProps.GetValueOrDefault("font-size")) ?? 16;
            if (parentFs > 0) fontSize = parentFs;
        }

        foreach (var child in element.Children)
        {
            if (child.IsTextNode)
            {
                // Estimate text width from font-size (Ahem font: 1ch = font-size).
                int charCount = (child.TextContent ?? "").Length;
                totalWidth += charCount * fontSize;
            }
            else
            {
                var childProps = GetComputedProps(child);
                // Skip absolutely positioned children (they don't contribute to flow width).
                string? childPos = childProps.GetValueOrDefault("position");
                if (childPos == "absolute" || childPos == "fixed") continue;

                double childW = TryParsePx(childProps.GetValueOrDefault("width")) ?? 0;
                totalWidth += childW;
            }
        }
        return totalWidth > 0 ? totalWidth : null;
    }

    /// <summary>
    /// Estimates the content height of an inline element by using its
    /// line-height or font-size.
    /// Returns <c>null</c> if the element is not an inline element.
    /// </summary>
    private double? EstimateInlineContentHeight(DomElement element)
    {
        string? display = GetComputedProps(element).GetValueOrDefault("display");
        bool isInline = IsInlineElement(element.TagName, display);
        if (!isInline) return null;

        var props = GetComputedProps(element);
        double fontSize = TryParsePx(props.GetValueOrDefault("font-size")) ?? 16;

        // Check parent for font-size and line-height (inline elements inherit).
        if (element.Parent != null)
        {
            var parentProps = GetComputedProps(element.Parent);
            double parentFs = TryParsePx(parentProps.GetValueOrDefault("font-size")) ?? 16;
            if (parentFs > 0) fontSize = parentFs;

            string? lhVal = parentProps.GetValueOrDefault("line-height");
            if (lhVal != null)
            {
                var lhTrimmed = lhVal.Trim();
                // Check for explicit px value first (e.g. "100px").
                if (lhTrimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                {
                    double? lhPx = TryParsePx(lhTrimmed);
                    if (lhPx.HasValue) return lhPx.Value;
                }
                // Unitless values are line-height multipliers (e.g. "1", "1.5").
                if (double.TryParse(lhTrimmed,
                        NumberStyles.Float, CultureInfo.InvariantCulture, out var lhMul))
                    return parentFs * lhMul;
            }
        }

        return fontSize;
    }

    /// <summary>
    /// Determines if an element is an inline element based on its tag name
    /// and display property.
    /// </summary>
    private static bool IsInlineElement(string tagName, string? display)
    {
        if (display != null)
        {
            var d = display.Trim().ToLowerInvariant();
            // inline-block establishes a containing block for abspos children
            // and is treated as block-level for layout purposes, so it is
            // NOT considered inline here.
            if (d == "inline") return true;
            if (d == "block" || d == "flex" || d == "grid" || d == "table" ||
                d == "list-item" || d == "flow-root" || d == "inline-block" ||
                d == "inline-flex" || d == "inline-grid")
                return false;
        }
        // Default inline elements.
        var tag = tagName.ToLowerInvariant();
        return tag is "span" or "a" or "strong" or "em" or "b" or "i" or
               "code" or "small" or "big" or "sub" or "sup" or "abbr" or
               "cite" or "q" or "mark" or "label" or "time";
    }

    /// <summary>
    /// Checks whether the given element is an inline-level element that
    /// establishes a containing block (e.g. <c>&lt;span&gt;</c> with
    /// <c>position: relative</c>).  Broiler's renderer cannot correctly
    /// place absolutely positioned children inside such elements.
    /// </summary>
    private bool IsInlineContainingBlock(DomElement element)
    {
        var props = GetComputedProps(element);
        string? display = props.GetValueOrDefault("display");
        if (!IsInlineElement(element.TagName, display))
            return false;
        return EstablishesContainingBlock(props);
    }

    /// <summary>
    /// Promotes absolutely positioned children out of inline containing
    /// blocks to the nearest block-level ancestor.  Adjusts their
    /// coordinates to be relative to the block ancestor instead of the
    /// inline element.  This is needed because Broiler's renderer does
    /// not support positioning absolutely positioned elements inside
    /// inline boxes (like <c>&lt;span&gt;</c>).
    /// </summary>
    private void PromoteAbsPosFromInlineCBs(DomElement root)
    {
        // Collect all promotions first (to avoid mutating during traversal).
        var promotions = new List<(DomElement child, DomElement inlineCB, DomElement blockAncestor,
            double offX, double offY)>();
        CollectInlineCBPromotions(root, promotions);

        foreach (var (child, inlineCB, blockAncestor, offX, offY) in promotions)
        {
            // Adjust the child's left/top styles.
            // Read from computed CSS (rules + inline) to get the correct
            // current values, then write to inline style (which overrides).
            var childCss = GetComputedProps(child);
            double curLeft = TryParsePx(childCss.GetValueOrDefault("left")) ?? 0;
            double curTop = TryParsePx(childCss.GetValueOrDefault("top")) ?? 0;
            double curWidth = TryParsePx(childCss.GetValueOrDefault("width")) ?? 0;
            double curHeight = TryParsePx(childCss.GetValueOrDefault("height")) ?? 0;
            child.Style["position"] = childCss.GetValueOrDefault("position") ?? "absolute";
            // Ensure inline elements (like <span>) are treated as block-level
            // after absolute positioning, so the renderer paints backgrounds.
            string? childDisplay = childCss.GetValueOrDefault("display");
            if (IsInlineElement(child.TagName, childDisplay))
                child.Style["display"] = "block";
            child.Style["left"] = $"{(curLeft + offX).ToString(CultureInfo.InvariantCulture)}px";
            child.Style["top"] = $"{(curTop + offY).ToString(CultureInfo.InvariantCulture)}px";
            // Ensure width and height are preserved as inline styles.
            if (curWidth > 0)
                child.Style["width"] = $"{curWidth.ToString(CultureInfo.InvariantCulture)}px";
            if (curHeight > 0)
                child.Style["height"] = $"{curHeight.ToString(CultureInfo.InvariantCulture)}px";
            // Preserve background-color if specified in CSS rules.
            string? bg = childCss.GetValueOrDefault("background-color")
                      ?? childCss.GetValueOrDefault("background");
            if (!string.IsNullOrWhiteSpace(bg) && bg != "transparent" && bg != "initial")
                child.Style["background-color"] = bg;

            // Move from inline CB to block ancestor.
            inlineCB.Children.Remove(child);
            blockAncestor.Children.Add(child);

            // Ensure the block ancestor has position:relative.
            var blockProps = GetComputedProps(blockAncestor);
            string? blockPos = blockProps.GetValueOrDefault("position");
            if (blockPos == null || blockPos == "static")
                blockAncestor.Style["position"] = "relative";
        }
    }

    private void CollectInlineCBPromotions(
        DomElement element,
        List<(DomElement child, DomElement inlineCB, DomElement blockAncestor,
            double offX, double offY)> promotions)
    {
        if (!element.IsTextNode && IsInlineContainingBlock(element))
        {
            var (offX, offY, blockAncestor) = ComputeInlineCBOffset(element);
            if (blockAncestor != null)
            {
                // Collect absolutely positioned children.
                foreach (var child in element.Children.ToList())
                {
                    if (child.IsTextNode) continue;
                    var childProps = GetComputedProps(child);
                    string? childPos = childProps.GetValueOrDefault("position");
                    if (childPos == "absolute" || childPos == "fixed")
                    {
                        promotions.Add((child, element, blockAncestor, offX, offY));
                    }
                }
            }
        }

        foreach (var child in element.Children.ToList())
            CollectInlineCBPromotions(child, promotions);
    }

    /// <summary>
    /// Computes the offset from an inline containing block to the nearest
    /// block-level ancestor.  This offset is used to adjust absolute
    /// coordinates when promoting position-area elements out of an inline CB.
    /// Returns (offsetX, offsetY, blockAncestor).
    /// </summary>
    private (double offsetX, double offsetY, DomElement? blockAncestor)
        ComputeInlineCBOffset(DomElement inlineCB)
    {
        // Walk up from the inline CB to find the nearest block-level ancestor.
        // The inline CB's position within its parent block is determined by:
        // - Preceding text content (widths of chars)
        // - Preceding inline siblings
        // - Line breaks
        // We estimate this from the layout context.
        var parent = inlineCB.Parent;
        DomElement? blockAncestor = null;

        // Find nearest block-level ancestor.
        while (parent != null)
        {
            if (!parent.IsTextNode)
            {
                var parentProps = GetComputedProps(parent);
                string? parentDisplay = parentProps.GetValueOrDefault("display");
                if (!IsInlineElement(parent.TagName, parentDisplay))
                {
                    blockAncestor = parent;
                    break;
                }
            }
            parent = parent.Parent;
        }

        if (blockAncestor == null) return (0, 0, null);

        // Compute the inline CB's position within the block ancestor.
        // This accounts for preceding siblings (line breaks, text) and
        // the text/inline content before the inline CB in the same line.
        double offsetX = 0, offsetY = 0;

        // Walk from the block ancestor down to the inline CB, accumulating
        // offset from preceding siblings' dimensions and the inline CB's
        // own position within its parent.
        offsetX += EstimateInlineOffsetX(inlineCB, blockAncestor);
        offsetY += EstimateInlineOffsetY(inlineCB, blockAncestor);

        return (offsetX, offsetY, blockAncestor);
    }

    /// <summary>
    /// Estimates the horizontal position of an inline element within
    /// its nearest block ancestor, accounting for preceding text and
    /// inline content.
    /// </summary>
    private double EstimateInlineOffsetX(DomElement inlineEl, DomElement blockAncestor)
    {
        double offset = 0;
        var parent = inlineEl.Parent;
        while (parent != null && parent != blockAncestor)
        {
            // Accumulate horizontal position from parent's preceding content
            offset += EstimatePrecedingInlineWidth(inlineEl, parent);
            inlineEl = parent;
            parent = parent.Parent;
        }
        // Final level: position within the block ancestor
        if (parent == blockAncestor)
            offset += EstimatePrecedingInlineWidth(inlineEl, blockAncestor);
        return offset;
    }

    /// <summary>
    /// Estimates the vertical position of an inline element within
    /// its nearest block ancestor, accounting for preceding block
    /// siblings, line breaks (<c>&lt;br&gt;</c>), and text nodes that
    /// contain only line breaks.
    /// </summary>
    private double EstimateInlineOffsetY(DomElement inlineEl, DomElement blockAncestor)
    {
        double offset = 0;
        var parent = inlineEl.Parent;
        while (parent != null && parent != blockAncestor)
        {
            offset += EstimatePrecedingBlockHeight(inlineEl, parent);
            inlineEl = parent;
            parent = parent.Parent;
        }
        if (parent == blockAncestor)
            offset += EstimatePrecedingBlockHeight(inlineEl, blockAncestor);
        return offset;
    }

    /// <summary>
    /// Estimates the total height of preceding siblings in block/inline context,
    /// handling <c>&lt;br&gt;</c> elements (which contribute the parent's
    /// line-height) and text nodes with line breaks.
    /// </summary>
    private double EstimatePrecedingBlockHeight(DomElement element, DomElement parent)
    {
        double totalHeight = 0;
        var parentProps = GetComputedProps(parent);
        double fontSize = TryParsePx(parentProps.GetValueOrDefault("font-size")) ?? 16;
        double lineHeight = ResolveLineHeight(parentProps, fontSize);

        foreach (var sibling in parent.Children)
        {
            if (sibling == element) break;
            if (sibling.IsTextNode)
            {
                // Count line breaks in text content.
                var text = sibling.TextContent ?? "";
                int lineBreaks = text.Count(c => c == '\n');
                // Don't count text node line breaks as they're usually
                // just whitespace in the HTML source.
                continue;
            }

            var sibProps = GetComputedProps(sibling);
            string? sibPos = sibProps.GetValueOrDefault("position");
            if (sibPos == "absolute" || sibPos == "fixed") continue;

            if (sibling.TagName.Equals("br", StringComparison.OrdinalIgnoreCase))
            {
                // <br> creates a line break with the parent's line-height.
                totalHeight += lineHeight;
                continue;
            }

            double sibHeight = TryParsePx(sibProps.GetValueOrDefault("height")) ?? 0;
            double sibMT = TryParsePx(sibProps.GetValueOrDefault("margin-top")) ?? 0;
            double sibMB = TryParsePx(sibProps.GetValueOrDefault("margin-bottom")) ?? 0;
            double sibMR = 0;
            ParseMarginShorthand(sibProps, ref sibMT, ref sibMB, ref sibMR);

            totalHeight += sibHeight + sibMT + sibMB;
        }
        return totalHeight;
    }

    /// <summary>
    /// Resolves the computed line-height from CSS properties.
    /// Handles unitless values (multipliers of font-size), pixel values,
    /// and the "normal" keyword (defaults to 1.2 × font-size).
    /// </summary>
    private static double ResolveLineHeight(Dictionary<string, string> props, double fontSize)
    {
        string? lh = props.GetValueOrDefault("line-height");
        if (string.IsNullOrWhiteSpace(lh) || lh == "normal")
            return fontSize * 1.2;

        var v = lh!.Trim();

        // Explicit pixel value.
        if (v.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            double? px = TryParsePx(v);
            if (px.HasValue) return px.Value;
        }

        // Unitless: a multiplier of font-size.
        if (double.TryParse(v, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var multiplier))
            return fontSize * multiplier;

        return fontSize * 1.2;
    }

    /// <summary>
    /// Estimates the total width of inline content preceding <paramref name="element"/>
    /// within <paramref name="parent"/>.  This includes text nodes and inline elements.
    /// </summary>
    private double EstimatePrecedingInlineWidth(DomElement element, DomElement parent)
    {
        double width = 0;
        var props = GetComputedProps(parent);
        double fontSize = TryParsePx(props.GetValueOrDefault("font-size")) ?? 16;

        foreach (var sibling in parent.Children)
        {
            if (sibling == element) break;

            if (sibling.IsTextNode)
            {
                // Decode HTML entities (e.g. &nbsp; → \u00A0) before counting.
                var text = System.Net.WebUtility.HtmlDecode(sibling.TextContent ?? "");
                int charCount = 0;
                foreach (char c in text)
                {
                    if (c == '\n' || c == '\r') continue;
                    if (c == '\u00A0' || !char.IsWhiteSpace(c)) // &nbsp; or visible
                        charCount++;
                }
                width += charCount * fontSize;
            }
            else
            {
                var sibProps = GetComputedProps(sibling);
                string? sibPos = sibProps.GetValueOrDefault("position");
                if (sibPos == "absolute" || sibPos == "fixed") continue;

                // Check for explicit width
                double? sibW = TryParsePx(sibProps.GetValueOrDefault("width"));
                if (sibW.HasValue)
                    width += sibW.Value;
                else if (sibling.TagName.Equals("br", StringComparison.OrdinalIgnoreCase))
                    width = 0; // Line break resets horizontal position
            }
        }
        return width;
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

    // -----------------------------------------------------------------
    // position-area resolution
    // -----------------------------------------------------------------

    /// <summary>
    /// Resolves <c>position-area</c> values on elements that have
    /// <c>position-anchor</c>.  Computes the 3×3 grid from the anchor
    /// element's position and the containing block, then selects the
    /// region specified by position-area and sets explicit inline styles.
    /// </summary>
    private void ResolvePositionAreaValues(
        DomElement element,
        Dictionary<string, AnchorInfo> anchorRegistry,
        HashSet<DomElement> scrollContainersNeedingRelative,
        List<(DomElement element, DomElement oldParent, DomElement newParent)> deferredDomMoves)
    {
        if (!element.IsTextNode)
        {
            var cssProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (selector, _, declarations) in CssRules)
            {
                if (MatchesSelector(element, selector))
                    foreach (var kv in declarations)
                        cssProps[kv.Key] = kv.Value;
            }
            foreach (var kv in element.Style)
                cssProps[kv.Key] = kv.Value;

            string? positionArea = cssProps.GetValueOrDefault("position-area");
            string? positionAnchor = cssProps.GetValueOrDefault("position-anchor");

            if (!string.IsNullOrWhiteSpace(positionArea) &&
                positionArea != "none" &&
                !string.IsNullOrWhiteSpace(positionAnchor) &&
                anchorRegistry.TryGetValue(positionAnchor, out var anchor))
            {
                // Find the anchor element to determine its scroll container.
                var anchorEl = FindElementByAnchorName(positionAnchor);
                var rawScrollContainer = anchorEl != null
                    ? FindNearestScrollContainer(anchorEl)
                    : null;

                // Only use the scroll container as the CB when the
                // positioned element is actually inside that scroll
                // container.  When the element is outside (e.g. the
                // anchor is in a scrollable sibling), the grid must
                // be computed against the element's own CB.
                var scrollContainer = rawScrollContainer != null &&
                    IsDescendantOfElement(element, rawScrollContainer)
                        ? rawScrollContainer
                        : null;

                // Compute the grid cell using the anchor's scroll container
                // (or the element's own CB) as the containing block.
                var rect = ComputePositionAreaRect(
                    element, anchor, positionArea, scrollContainer);
                if (rect != null)
                {
                    // Preserve position:fixed when the element already has it;
                    // otherwise default to position:absolute.
                    if (!cssProps.TryGetValue("position", out var origPos) ||
                        !origPos.Equals("fixed", StringComparison.OrdinalIgnoreCase))
                    {
                        element.Style["position"] = "absolute";
                    }
                    else
                    {
                        element.Style["position"] = "fixed";
                    }

                    double cellW = rect.Value.Width;
                    double cellH = rect.Value.Height;

                    // Resolve any percentage insets within the cell.
                    // CSS spec: top/bottom % resolve against CB height,
                    // left/right % resolve against CB width.  For position-area
                    // the CB is the position-area cell.
                    double insetTop = 0, insetRight = 0, insetBottom = 0, insetLeft = 0;
                    string? rawInset = cssProps.GetValueOrDefault("inset");
                    if (rawInset != null)
                    {
                        var insetParts = rawInset.Split(new[] { ' ', '\t' },
                            StringSplitOptions.RemoveEmptyEntries);
                        if (insetParts.Length > 0)
                        {
                            insetTop = ResolvePctOrPx(insetParts[0], cellH);
                            insetRight = ResolvePctOrPx(
                                insetParts.Length > 1 ? insetParts[1] : insetParts[0], cellW);
                            insetBottom = ResolvePctOrPx(
                                insetParts.Length > 2 ? insetParts[2] : insetParts[0], cellH);
                            insetLeft = ResolvePctOrPx(
                                insetParts.Length > 3 ? insetParts[3]
                                    : (insetParts.Length > 1 ? insetParts[1] : insetParts[0]), cellW);
                        }
                    }
                    else
                    {
                        // Check individual inset properties from CSS
                        string? rawTop2 = cssProps.GetValueOrDefault("top");
                        string? rawRight2 = cssProps.GetValueOrDefault("right");
                        string? rawBottom2 = cssProps.GetValueOrDefault("bottom");
                        string? rawLeft2 = cssProps.GetValueOrDefault("left");
                        if (rawTop2 != null && rawTop2 != "auto")
                            insetTop = ResolvePctOrPx(rawTop2, cellH);
                        if (rawRight2 != null && rawRight2 != "auto")
                            insetRight = ResolvePctOrPx(rawRight2, cellW);
                        if (rawBottom2 != null && rawBottom2 != "auto")
                            insetBottom = ResolvePctOrPx(rawBottom2, cellH);
                        if (rawLeft2 != null && rawLeft2 != "auto")
                            insetLeft = ResolvePctOrPx(rawLeft2, cellW);
                    }

                    // The IMCB (Inset-Modified Containing Block) is the cell
                    // after applying insets.
                    double imcbLeft = rect.Value.Left + insetLeft;
                    double imcbTop = rect.Value.Top + insetTop;
                    double imcbW = cellW - insetLeft - insetRight;
                    double imcbH = cellH - insetTop - insetBottom;
                    if (imcbW < 0) imcbW = 0;
                    if (imcbH < 0) imcbH = 0;

                    // Resolve percentage margins against the cell width
                    // (CSS spec: margin % always resolves against inline dimension).
                    double marginTop2 = ResolvePctOrPx(
                        cssProps.GetValueOrDefault("margin-top") ?? "0", cellW);
                    double marginRight2 = ResolvePctOrPx(
                        cssProps.GetValueOrDefault("margin-right") ?? "0", cellW);
                    double marginBottom2 = ResolvePctOrPx(
                        cssProps.GetValueOrDefault("margin-bottom") ?? "0", cellW);
                    double marginLeft2 = ResolvePctOrPx(
                        cssProps.GetValueOrDefault("margin-left") ?? "0", cellW);

                    // Resolve percentage margins from the 'margin' shorthand
                    string? marginShorthand = cssProps.GetValueOrDefault("margin");
                    if (marginShorthand != null)
                    {
                        var mp = marginShorthand.Split(new[] { ' ', '\t' },
                            StringSplitOptions.RemoveEmptyEntries);
                        if (mp.Length > 0)
                        {
                            if (!cssProps.ContainsKey("margin-top"))
                                marginTop2 = ResolvePctOrPx(mp[0], cellW);
                            if (!cssProps.ContainsKey("margin-right"))
                                marginRight2 = ResolvePctOrPx(
                                    mp.Length > 1 ? mp[1] : mp[0], cellW);
                            if (!cssProps.ContainsKey("margin-bottom"))
                                marginBottom2 = ResolvePctOrPx(
                                    mp.Length > 2 ? mp[2] : mp[0], cellW);
                            if (!cssProps.ContainsKey("margin-left"))
                                marginLeft2 = ResolvePctOrPx(
                                    mp.Length > 3 ? mp[3]
                                        : (mp.Length > 1 ? mp[1] : mp[0]), cellW);
                        }
                    }

                    // Resolve percentage padding against the cell width.
                    double padTop = 0, padRight = 0, padBottom = 0, padLeft = 0;
                    string? padShorthand = cssProps.GetValueOrDefault("padding");
                    if (padShorthand != null)
                    {
                        var pp = padShorthand.Split(new[] { ' ', '\t' },
                            StringSplitOptions.RemoveEmptyEntries);
                        if (pp.Length > 0)
                        {
                            padTop = ResolvePctOrPx(pp[0], cellW);
                            padRight = ResolvePctOrPx(
                                pp.Length > 1 ? pp[1] : pp[0], cellW);
                            padBottom = ResolvePctOrPx(
                                pp.Length > 2 ? pp[2] : pp[0], cellW);
                            padLeft = ResolvePctOrPx(
                                pp.Length > 3 ? pp[3]
                                    : (pp.Length > 1 ? pp[1] : pp[0]), cellW);
                        }
                    }
                    if (cssProps.TryGetValue("padding-top", out var pt))
                        padTop = ResolvePctOrPx(pt, cellW);
                    if (cssProps.TryGetValue("padding-right", out var pr))
                        padRight = ResolvePctOrPx(pr, cellW);
                    if (cssProps.TryGetValue("padding-bottom", out var pb))
                        padBottom = ResolvePctOrPx(pb, cellW);
                    if (cssProps.TryGetValue("padding-left", out var pl))
                        padLeft = ResolvePctOrPx(pl, cellW);

                    // Resolve element dimensions.  Percentage values are
                    // resolved against the position-area cell dimensions.
                    // Explicit pixel values are used directly.
                    double resolvedW = imcbW;
                    double resolvedH = imcbH;

                    string? rawW = cssProps.GetValueOrDefault("width");
                    string? rawH = cssProps.GetValueOrDefault("height");

                    double? explicitW = TryParsePx(rawW);
                    double? explicitH = TryParsePx(rawH);
                    double? pctW = TryParsePercent(rawW);
                    double? pctH = TryParsePercent(rawH);

                    if (pctW.HasValue)
                        resolvedW = cellW * pctW.Value / 100.0;
                    else if (explicitW.HasValue && explicitW.Value > 0)
                        resolvedW = Math.Min(explicitW.Value, cellW);

                    if (pctH.HasValue)
                        resolvedH = cellH * pctH.Value / 100.0;
                    else if (explicitH.HasValue && explicitH.Value > 0)
                        resolvedH = Math.Min(explicitH.Value, cellH);

                    // Compute alignment-based offset within the cell.
                    // Parse the position-area to determine alignment.
                    ParsePositionArea(positionArea, out var blockAlign, out var inlineAlign);

                    double offsetX = ComputeAlignmentOffset(
                        inlineAlign, cellW, resolvedW, isInlineAxis: true);
                    double offsetY = ComputeAlignmentOffset(
                        blockAlign, cellH, resolvedH, isInlineAxis: false);

                    double finalLeft = rect.Value.Left + offsetX;
                    double finalTop = rect.Value.Top + offsetY;

                    // Broiler's renderer cannot place absolutely positioned
                    // children inside inline elements (e.g. <span> with
                    // position:relative).  When the containing block is an
                    // inline element, promote the coordinates to the nearest
                    // block-level ancestor and ensure it has position:relative.
                    if (scrollContainer == null)
                    {
                        var inlineCB = FindContainingBlockElement(element);
                        if (inlineCB != null && IsInlineContainingBlock(inlineCB))
                        {
                            var (inlineOffX, inlineOffY, blockAncestor) =
                                ComputeInlineCBOffset(inlineCB);
                            finalLeft += inlineOffX;
                            finalTop += inlineOffY;

                            // Ensure the block ancestor has position:relative
                            // so the renderer can place the abs-pos element.
                            if (blockAncestor != null)
                            {
                                var blockProps = GetComputedProps(blockAncestor);
                                string? blockPos = blockProps.GetValueOrDefault("position");
                                if (blockPos == null || blockPos == "static")
                                    blockAncestor.Style["position"] = "relative";
                            }

                            // Defer the move to avoid collection modification
                            // during tree traversal.
                            if (blockAncestor != null && element.Parent != blockAncestor
                                && element.Parent != null)
                            {
                                deferredDomMoves.Add((element, element.Parent, blockAncestor));
                            }
                        }
                    }

                    // Determine whether to use cell-boundary positioning
                    // (left/top/right/bottom to define the IMCB) so the
                    // renderer's box model naturally handles margins, borders,
                    // and padding.  Use this when the element stretches to
                    // fill the cell (place-self: stretch, the default for
                    // position-area) and has percentage-based box properties.
                    bool hasPercentBoxProps =
                        HasPercent(cssProps.GetValueOrDefault("margin")) ||
                        HasPercent(cssProps.GetValueOrDefault("margin-top")) ||
                        HasPercent(cssProps.GetValueOrDefault("margin-right")) ||
                        HasPercent(cssProps.GetValueOrDefault("margin-bottom")) ||
                        HasPercent(cssProps.GetValueOrDefault("margin-left")) ||
                        HasPercent(cssProps.GetValueOrDefault("padding")) ||
                        HasPercent(cssProps.GetValueOrDefault("padding-top")) ||
                        HasPercent(cssProps.GetValueOrDefault("padding-right")) ||
                        HasPercent(cssProps.GetValueOrDefault("padding-bottom")) ||
                        HasPercent(cssProps.GetValueOrDefault("padding-left")) ||
                        HasPercent(rawInset) ||
                        HasPercent(cssProps.GetValueOrDefault("top")) ||
                        HasPercent(cssProps.GetValueOrDefault("left")) ||
                        HasPercent(cssProps.GetValueOrDefault("right")) ||
                        HasPercent(cssProps.GetValueOrDefault("bottom"));

                    if (hasPercentBoxProps)
                    {
                        // Resolve all percentages explicitly and use
                        // left/top + computed content width/height.
                        // Content width = IMCB width - margins - borders - padding
                        double borderW = TryParsePx(cssProps.GetValueOrDefault("border-left-width")) ?? 0;
                        double borderE = TryParsePx(cssProps.GetValueOrDefault("border-right-width")) ?? 0;
                        double borderN = TryParsePx(cssProps.GetValueOrDefault("border-top-width")) ?? 0;
                        double borderS = TryParsePx(cssProps.GetValueOrDefault("border-bottom-width")) ?? 0;

                        // Parse border shorthand if individual widths not set
                        string? borderShort = cssProps.GetValueOrDefault("border");
                        if (borderShort != null)
                        {
                            var borderParts = borderShort.Split(new[] { ' ', '\t' },
                                StringSplitOptions.RemoveEmptyEntries);
                            foreach (var bp in borderParts)
                            {
                                var bw = TryParsePx(bp);
                                if (bw.HasValue)
                                {
                                    if (borderW == 0) borderW = bw.Value;
                                    if (borderE == 0) borderE = bw.Value;
                                    if (borderN == 0) borderN = bw.Value;
                                    if (borderS == 0) borderS = bw.Value;
                                    break;
                                }
                            }
                        }

                        double contentW = imcbW - marginLeft2 - marginRight2
                            - borderW - borderE - padLeft - padRight;
                        double contentH = imcbH - marginTop2 - marginBottom2
                            - borderN - borderS - padTop - padBottom;
                        if (contentW < 0) contentW = 0;
                        if (contentH < 0) contentH = 0;

                        finalLeft = imcbLeft;
                        finalTop = imcbTop;

                        // Set resolved pixel values for margins and padding
                        // to override the percentage values from CSS.
                        element.Style["margin-top"] = $"{marginTop2.ToString(CultureInfo.InvariantCulture)}px";
                        element.Style["margin-right"] = $"{marginRight2.ToString(CultureInfo.InvariantCulture)}px";
                        element.Style["margin-bottom"] = $"{marginBottom2.ToString(CultureInfo.InvariantCulture)}px";
                        element.Style["margin-left"] = $"{marginLeft2.ToString(CultureInfo.InvariantCulture)}px";
                        element.Style["padding-top"] = $"{padTop.ToString(CultureInfo.InvariantCulture)}px";
                        element.Style["padding-right"] = $"{padRight.ToString(CultureInfo.InvariantCulture)}px";
                        element.Style["padding-bottom"] = $"{padBottom.ToString(CultureInfo.InvariantCulture)}px";
                        element.Style["padding-left"] = $"{padLeft.ToString(CultureInfo.InvariantCulture)}px";
                        element.Style.Remove("margin");
                        element.Style.Remove("padding");
                        element.Style.Remove("inset");

                        resolvedW = contentW;
                        resolvedH = contentH;
                    }

                    // When box-sizing is border-box, the resolved width/height
                    // represent the total (border-box) dimensions.  The renderer
                    // treats the CSS 'width'/'height' properties as content-box
                    // dimensions, so we need to subtract borders and padding to
                    // get the correct content width/height.  We also set explicit
                    // pixel border-width values in the inline style so the
                    // renderer uses CSS-spec values (medium=3px) rather than its
                    // own default (medium=2px).
                    double borderBoxW = resolvedW;
                    double borderBoxH = resolvedH;
                    string? boxSizing = cssProps.GetValueOrDefault("box-sizing");
                    if (boxSizing != null &&
                        boxSizing.Equals("border-box", StringComparison.OrdinalIgnoreCase) &&
                        !hasPercentBoxProps)
                    {
                        double bdrL = ResolveBorderWidth(cssProps, "border-left-width", "border");
                        double bdrR = ResolveBorderWidth(cssProps, "border-right-width", "border");
                        double bdrT = ResolveBorderWidth(cssProps, "border-top-width", "border");
                        double bdrB = ResolveBorderWidth(cssProps, "border-bottom-width", "border");

                        resolvedW -= bdrL + bdrR + padLeft + padRight;
                        resolvedH -= bdrT + bdrB + padTop + padBottom;
                        if (resolvedW < 0) resolvedW = 0;
                        if (resolvedH < 0) resolvedH = 0;

                        // Override border-width with explicit pixel values so
                        // the renderer doesn't use its own keyword mapping
                        // (which maps medium→2px instead of the spec's 3px).
                        // Always set the value (even 0px) to ensure the
                        // renderer doesn't fall back to its keyword defaults.
                        element.Style["border-top-width"] = $"{bdrT.ToString(CultureInfo.InvariantCulture)}px";
                        element.Style["border-right-width"] = $"{bdrR.ToString(CultureInfo.InvariantCulture)}px";
                        element.Style["border-bottom-width"] = $"{bdrB.ToString(CultureInfo.InvariantCulture)}px";
                        element.Style["border-left-width"] = $"{bdrL.ToString(CultureInfo.InvariantCulture)}px";
                    }

                    element.Style["left"] = $"{finalLeft.ToString(CultureInfo.InvariantCulture)}px";
                    element.Style["top"] = $"{finalTop.ToString(CultureInfo.InvariantCulture)}px";
                    element.Style["width"] = $"{resolvedW.ToString(CultureInfo.InvariantCulture)}px";
                    element.Style["height"] = $"{resolvedH.ToString(CultureInfo.InvariantCulture)}px";

                    // Record the scroll container for deferred position:relative.
                    if (scrollContainer != null)
                        scrollContainersNeedingRelative.Add(scrollContainer);

                    // Store resolved offsets for JS offset property queries.
                    // Use border-box dimensions (matching offsetWidth/offsetHeight).
                    element.DomProperties["_resolvedLeft"] = finalLeft;
                    element.DomProperties["_resolvedTop"] = finalTop;
                    element.DomProperties["_resolvedWidth"] = borderBoxW;
                    element.DomProperties["_resolvedHeight"] = borderBoxH;
                }
            }
        }

        foreach (var child in element.Children)
            ResolvePositionAreaValues(child, anchorRegistry, scrollContainersNeedingRelative,
                deferredDomMoves);
    }

    private readonly record struct PositionAreaRect(
        double Left, double Top, double Width, double Height);

    /// <summary>
    /// Computes the rectangle for a given <c>position-area</c> value using
    /// the 3×3 grid defined by the anchor element and containing block.
    /// When <paramref name="scrollContainer"/> is provided, the grid is
    /// computed relative to that container (the anchor's scroll container)
    /// rather than the target element's normal containing block.
    /// </summary>
    private PositionAreaRect? ComputePositionAreaRect(
        DomElement element, AnchorInfo anchor, string positionArea,
        DomElement? scrollContainer = null)
    {
        double cbWidth, cbHeight;
        double anchorLeft, anchorRight, anchorTop, anchorBottom;
        double cbOffsetX = 0, cbOffsetY = 0;

        if (scrollContainer != null)
        {
            // Use the scroll container's own dimensions as the CB.
            var scProps = GetComputedProps(scrollContainer);
            cbWidth = TryParsePx(scProps.GetValueOrDefault("width")) ?? _viewportWidth;
            cbHeight = TryParsePx(scProps.GetValueOrDefault("height")) ?? _viewportHeight;

            // For the position-area grid, use the scroll content dimensions
            // (the actual scrollable area) rather than the scroll port dimensions.
            // The grid extends to cover the full scrollable content.
            double scrollContentWidth = FindScrollContentWidth(scrollContainer, cbWidth);
            double scrollContentHeight = FindScrollContentHeight(scrollContainer, cbHeight);

            // Compute anchor position relative to the scroll container.
            var anchorRelPos = ComputeAnchorRelativeToContainer(anchor, scrollContainer);
            anchorLeft = anchorRelPos.Left;
            anchorRight = anchorRelPos.Left + anchor.Width;
            anchorTop = anchorRelPos.Top;
            anchorBottom = anchorRelPos.Top + anchor.Height;

            // Use scroll content dimensions for the grid edges.
            cbWidth = scrollContentWidth;
            cbHeight = scrollContentHeight;
        }
        else
        {
            cbWidth = FindContainingBlockWidth(element);
            cbHeight = FindContainingBlockHeight(element);
            anchorLeft = anchor.Left;
            anchorRight = anchor.Right;
            anchorTop = anchor.Top;
            anchorBottom = anchor.Bottom;

            // Determine the containing block for coordinate system.
            var cbEl = FindContainingBlockElement(element);
            if (cbEl == null)
            {
                // No positioned ancestor → initial CB = body content area.
                // Anchor coordinates from ComputeElementBox are document-absolute,
                // and the grid origin must account for body margin.
                cbOffsetX = FindBodyMarginLeft();
                cbOffsetY = FindBodyMarginTop();
            }
            else
            {
                // A positioned ancestor was found. The anchor's CSS top/left
                // values from ComputeElementBox are relative to the anchor's
                // own CB. If the anchor's CB is the same as the target's CB,
                // coordinates are already in the right frame — grid origin is 0.
                // Otherwise, we need to map the anchor coordinates.
                var anchorCBEl = FindAnchorContainingBlock(element, cbEl);
                if (anchorCBEl == cbEl)
                {
                    // Same CB → anchor coords are CB-relative → grid at origin.
                    cbOffsetX = 0;
                    cbOffsetY = 0;
                }
                else
                {
                    // Different CBs → use document coordinates.
                    var box = ComputeElementBox(cbEl);
                    cbOffsetX = box?.Left ?? 0;
                    cbOffsetY = box?.Top ?? 0;
                }
            }
        }

        // Grid column edges: extend to include both the CB and the anchor.
        double gridLeft = Math.Min(cbOffsetX, anchorLeft);
        double gridRight = Math.Max(cbOffsetX + cbWidth, anchorRight);

        // Grid row edges.
        double gridTop = Math.Min(cbOffsetY, anchorTop);
        double gridBottom = Math.Max(cbOffsetY + cbHeight, anchorBottom);

        // Parse the position-area value into block and inline axis selections.
        ParsePositionArea(positionArea, out var blockSel, out var inlineSel);

        // Compute column range.
        double colStart, colEnd;
        switch (inlineSel)
        {
            case AxisSelection.Start:
                colStart = gridLeft; colEnd = anchorLeft; break;
            case AxisSelection.Center:
                colStart = anchorLeft; colEnd = anchorRight; break;
            case AxisSelection.End:
                colStart = anchorRight; colEnd = gridRight; break;
            case AxisSelection.SpanStart:
                colStart = gridLeft; colEnd = anchorRight; break;
            case AxisSelection.SpanEnd:
                colStart = anchorLeft; colEnd = gridRight; break;
            case AxisSelection.SpanAll:
                colStart = gridLeft; colEnd = gridRight; break;
            default:
                colStart = gridLeft; colEnd = gridRight; break;
        }

        // Compute row range.
        double rowStart, rowEnd;
        switch (blockSel)
        {
            case AxisSelection.Start:
                rowStart = gridTop; rowEnd = anchorTop; break;
            case AxisSelection.Center:
                rowStart = anchorTop; rowEnd = anchorBottom; break;
            case AxisSelection.End:
                rowStart = anchorBottom; rowEnd = gridBottom; break;
            case AxisSelection.SpanStart:
                rowStart = gridTop; rowEnd = anchorBottom; break;
            case AxisSelection.SpanEnd:
                rowStart = anchorTop; rowEnd = gridBottom; break;
            case AxisSelection.SpanAll:
                rowStart = gridTop; rowEnd = gridBottom; break;
            default:
                rowStart = gridTop; rowEnd = gridBottom; break;
        }

        double width = Math.Max(0, colEnd - colStart);
        double height = Math.Max(0, rowEnd - rowStart);

        return new PositionAreaRect(colStart, rowStart, width, height);
    }

    /// <summary>
    /// Computes the total width of the scrollable content inside a scroll
    /// container by examining its children's widths and margins.
    /// Falls back to the container's own width if no explicit child widths
    /// are found.
    /// </summary>
    private double FindScrollContentWidth(DomElement scrollContainer, double containerWidth)
    {
        double maxWidth = containerWidth;
        foreach (var child in scrollContainer.Children)
        {
            if (child.IsTextNode) continue;
            var childProps = GetComputedProps(child);
            double? childW = TryParsePx(childProps.GetValueOrDefault("width"));
            if (childW.HasValue)
            {
                double ml = TryParsePx(childProps.GetValueOrDefault("margin-left")) ?? 0;
                double mr = TryParsePx(childProps.GetValueOrDefault("margin-right")) ?? 0;
                double totalW = childW.Value + ml + mr;
                if (totalW > maxWidth) maxWidth = totalW;
            }
        }
        return maxWidth;
    }

    /// <summary>
    /// Computes the total height of the scrollable content inside a scroll
    /// container by examining its children's heights and margins.
    /// Falls back to the container's own height if no explicit child heights
    /// are found.
    /// </summary>
    private double FindScrollContentHeight(DomElement scrollContainer, double containerHeight)
    {
        double maxHeight = containerHeight;
        foreach (var child in scrollContainer.Children)
        {
            if (child.IsTextNode) continue;
            var childProps = GetComputedProps(child);
            double? childH = TryParsePx(childProps.GetValueOrDefault("height"));
            if (childH.HasValue)
            {
                double mt = TryParsePx(childProps.GetValueOrDefault("margin-top")) ?? 0;
                double mb = TryParsePx(childProps.GetValueOrDefault("margin-bottom")) ?? 0;
                double totalH = childH.Value + mt + mb;
                if (totalH > maxHeight) maxHeight = totalH;
            }
        }
        return maxHeight;
    }

    /// <summary>
    /// Computes the anchor's position relative to the specified container.
    /// When the anchor's containing block IS the container (e.g. the
    /// scroll container itself has position:relative), the anchor's
    /// coordinates from ComputeElementBox are already container-relative.
    /// Otherwise, both are in document coordinates and we subtract.
    /// </summary>
    private (double Left, double Top) ComputeAnchorRelativeToContainer(
        AnchorInfo anchor, DomElement container)
    {
        // Check if the container establishes a CB. If it does, the anchor's
        // ComputeElementBox walk will have stopped at the container, and
        // the returned coordinates are already container-relative.
        var containerProps = GetComputedProps(container);
        if (EstablishesContainingBlock(containerProps))
            return (anchor.Left, anchor.Top);

        var containerBox = ComputeElementBox(container);
        if (containerBox == null)
            return (anchor.Left, anchor.Top);
        return (anchor.Left - containerBox.Left, anchor.Top - containerBox.Top);
    }

    /// <summary>
    /// Finds the nearest ancestor of <paramref name="el"/> that is a scroll
    /// container (has <c>overflow: hidden/scroll/auto/clip</c>).
    /// </summary>
    private DomElement? FindNearestScrollContainer(DomElement el)
    {
        var parent = el.Parent;
        while (parent != null)
        {
            if (!parent.IsTextNode)
            {
                var props = GetComputedProps(parent);
                if (HasOverflowClipping(props))
                    return parent;
            }
            parent = parent.Parent;
        }
        return null;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="el"/> is a descendant of
    /// <paramref name="potentialAncestor"/> in the DOM tree.
    /// </summary>
    private static bool IsDescendantOfElement(DomElement el, DomElement potentialAncestor)
    {
        var current = el.Parent;
        while (current != null)
        {
            if (ReferenceEquals(current, potentialAncestor)) return true;
            current = current.Parent;
        }
        return false;
    }

    private enum AxisSelection { Start, Center, End, SpanStart, SpanEnd, SpanAll }

    /// <summary>
    /// Parses a position-area value into block and inline axis selections.
    /// Block keywords: top, bottom, span-top, span-bottom.
    /// Inline keywords: left, right, span-left, span-right.
    /// Ambiguous: center, span-all (assigned to whichever axis needs it).
    /// </summary>
    private static void ParsePositionArea(
        string value, out AxisSelection blockSel, out AxisSelection inlineSel)
    {
        blockSel = AxisSelection.SpanAll;
        inlineSel = AxisSelection.SpanAll;

        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        if (parts.Length == 1)
        {
            var sel = MapKeyword(parts[0]);
            var axis = ClassifyKeyword(parts[0]);
            if (axis == KeywordAxis.Block)
                blockSel = sel;
            else if (axis == KeywordAxis.Inline)
                inlineSel = sel;
            else // ambiguous single keyword
            {
                blockSel = sel;
                inlineSel = sel;
            }
            return;
        }

        // Two keywords: disambiguate axes.
        var sel1 = MapKeyword(parts[0]);
        var sel2 = MapKeyword(parts[1]);
        var axis1 = ClassifyKeyword(parts[0]);
        var axis2 = ClassifyKeyword(parts[1]);

        if (axis1 == KeywordAxis.Block && axis2 == KeywordAxis.Inline)
        { blockSel = sel1; inlineSel = sel2; }
        else if (axis1 == KeywordAxis.Inline && axis2 == KeywordAxis.Block)
        { inlineSel = sel1; blockSel = sel2; }
        else if (axis1 == KeywordAxis.Block && axis2 != KeywordAxis.Block)
        { blockSel = sel1; inlineSel = sel2; }
        else if (axis1 == KeywordAxis.Inline && axis2 != KeywordAxis.Inline)
        { inlineSel = sel1; blockSel = sel2; }
        else if (axis2 == KeywordAxis.Block)
        { inlineSel = sel1; blockSel = sel2; }
        else if (axis2 == KeywordAxis.Inline)
        { blockSel = sel1; inlineSel = sel2; }
        else
        { blockSel = sel1; inlineSel = sel2; } // both ambiguous → first=block, second=inline
    }

    private enum KeywordAxis { Block, Inline, Ambiguous }

    private static KeywordAxis ClassifyKeyword(string kw) => kw.Trim().ToLowerInvariant() switch
    {
        "top" or "bottom" or "span-top" or "span-bottom" or "block-start" or "block-end" => KeywordAxis.Block,
        "left" or "right" or "span-left" or "span-right" or "inline-start" or "inline-end" => KeywordAxis.Inline,
        _ => KeywordAxis.Ambiguous,
    };

    private static AxisSelection MapKeyword(string kw) => kw.Trim().ToLowerInvariant() switch
    {
        "top" or "left" or "start" or "block-start" or "inline-start" => AxisSelection.Start,
        "center" => AxisSelection.Center,
        "bottom" or "right" or "end" or "block-end" or "inline-end" => AxisSelection.End,
        "span-top" or "span-left" or "span-start" => AxisSelection.SpanStart,
        "span-bottom" or "span-right" or "span-end" => AxisSelection.SpanEnd,
        "span-all" or "all" => AxisSelection.SpanAll,
        _ => AxisSelection.SpanAll,
    };

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
        if (element.DomProperties.TryGetValue("_resolvedLeft", out var rl) && rl is double resolvedLeft &&
            element.DomProperties.TryGetValue("_resolvedTop", out var rt) && rt is double resolvedTop &&
            element.DomProperties.TryGetValue("_resolvedWidth", out var rw) && rw is double resolvedWidth &&
            element.DomProperties.TryGetValue("_resolvedHeight", out var rh) && rh is double resolvedHeight)
            return (resolvedLeft, resolvedTop, resolvedWidth, resolvedHeight);

        // Resolve on-the-fly from CSS properties and inline styles.
        var cssProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (selector, _, declarations) in CssRules)
        {
            if (MatchesSelector(element, selector))
                foreach (var kv in declarations)
                    cssProps[kv.Key] = kv.Value;
        }
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

        if (!anchorRegistry.TryGetValue(positionAnchor, out var anchor))
            return null;

        var rect = ComputePositionAreaRect(element, anchor, positionArea);
        if (rect == null) return null;

        // Cache the resolved values.
        element.DomProperties["_resolvedLeft"] = rect.Value.Left;
        element.DomProperties["_resolvedTop"] = rect.Value.Top;
        element.DomProperties["_resolvedWidth"] = rect.Value.Width;
        element.DomProperties["_resolvedHeight"] = rect.Value.Height;

        return (rect.Value.Left, rect.Value.Top, rect.Value.Width, rect.Value.Height);
    }
}
