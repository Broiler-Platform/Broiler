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
        ResolvePositionAreaValues(
            DocumentElement, anchorRegistry, scrollContainersNeedingRelative);

        // 3b. Resolve position-try-fallbacks for elements whose base
        //     style overflows the containing block.
        ResolvePositionTryFallbacks(DocumentElement, anchorRegistry, positionTryRules);

        // 3c. Resolve position-visibility: hide anchor-positioned elements
        //     whose anchor is not visible or does not exist.
        ResolvePositionVisibility(DocumentElement, anchorRegistry);

        // 3d. Now apply deferred position:relative to scroll containers
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

        // 8. Apply scroll simulation: shift content in scroll containers
        //    where JavaScript set scrollTop/scrollLeft to match Chromium output.
        ApplyScrollSimulation(DocumentElement);
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

            if (scrollTop != 0 || scrollLeft != 0)
            {
                // Only apply to elements that clip overflow.
                var props = GetComputedProps(el);
                bool clips = HasOverflowClipping(props);

                if (clips && el.Children.Count > 0)
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

                    // Hide normal-flow children that are entirely above the
                    // scroll position.  This prevents coloured content from
                    // leaking above the container's top edge (Broiler's
                    // renderer clips overflow at the bottom but may not
                    // fully clip at the top for position:relative offsets).
                    if (scrollTop > 0)
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
        double Top, double Left, double Width, double Height)
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
                    registry[anchorName] = box;
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
            double top = TryParsePx(props.GetValueOrDefault("top")) ?? 0;
            double left = TryParsePx(props.GetValueOrDefault("left")) ?? 0;
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
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sel, _, decls) in CssRules)
        {
            if (MatchesSelector(element, sel))
                foreach (var kv in decls)
                    props[kv.Key] = kv.Value;
        }
        foreach (var kv in element.Style)
            props[kv.Key] = kv.Value;
        return props;
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
            ParseMarginShorthand(sibProps, ref sibMT, ref sibMT, ref sibMR);

            totalHeight += sibHeight + sibMT + sibMB;
        }
        return totalHeight;
    }

    // -----------------------------------------------------------------
    // anchor() resolution
    // -----------------------------------------------------------------

    private static readonly Regex AnchorFunctionPattern = new(
        @"anchor\(\s*(?:(?<name>--[a-zA-Z0-9_-]+)\s+)?(?<edge>top|right|bottom|left|start|end|center)\s*\)",
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

            foreach (var kv in cssProps)
            {
                var propName = kv.Key.ToLowerInvariant();
                var resolved = AnchorFunctionPattern.Replace(kv.Value, m =>
                {
                    var anchorName = m.Groups["name"].Value;
                    if (string.IsNullOrEmpty(anchorName))
                        anchorName = implicitAnchor ?? string.Empty;
                    var edge = m.Groups["edge"].Value.ToLowerInvariant();

                    if (!anchorRegistry.TryGetValue(anchorName, out var anchor))
                        return "0px";

                    // Compute the raw edge position (from CB origin).
                    double rawValue = edge switch
                    {
                        "top" => anchor.Top,
                        "right" => anchor.Right,
                        "bottom" => anchor.Bottom,
                        "left" => anchor.Left,
                        "center" => (anchor.Top + anchor.Bottom) / 2,
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

            // Resolve any anchor() references in the try properties.
            double tryLeft = 0, tryTop = 0, tryWidth = baseWidth, tryHeight = baseHeight;

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

            // Handle "inset: auto" which resets all inset properties.
            if (merged.TryGetValue("inset", out var insetVal) &&
                insetVal.Trim() == "auto")
            {
                // The try rule explicitly resets insets; recalculate.
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
    // Dialog backdrop insertion
    // -----------------------------------------------------------------

    private static void InsertDialogBackdrops(DomElement root, int vpW, int vpH)
    {
        var modals = new List<(DomElement dialog, DomElement parent)>();
        FindModalDialogs(root, modals);

        foreach (var (dialog, parent) in modals)
        {
            // Insert a backdrop div BEFORE the dialog.
            // Use 'position: fixed' with explicit pixel viewport dimensions
            // because the Broiler renderer cannot resolve opposing insets.
            // Use pre-composited rgb(229,229,229) instead of rgba(0,0,0,0.1)
            // because the renderer's alpha compositing gives incorrect results.
            var backdrop = new DomElement(
                "div", null, null, string.Empty,
                style: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["position"] = "fixed",
                    ["top"] = "0",
                    ["left"] = "0",
                    ["width"] = $"{vpW}px",
                    ["height"] = $"{vpH}px",
                    // Pre-composited ::backdrop color: the CSS spec default for
                    // dialog::backdrop is rgba(0,0,0,0.1).  Alpha-blending
                    // 10% black over white: 255*(1-0.1) + 0*0.1 = 229.5 ≈ 229.
                    // We use the pre-composited value because the Broiler
                    // renderer's alpha compositing produces incorrect results.
                    ["background-color"] = "rgb(229, 229, 229)",
                });
            backdrop.Parent = parent;

            int idx = parent.Children.IndexOf(dialog);
            if (idx >= 0)
                parent.Children.Insert(idx, backdrop);

            // Ensure the dialog has UA default styles.
            if (!dialog.Style.ContainsKey("display"))
                dialog.Style["display"] = "block";
            if (!dialog.Style.ContainsKey("border"))
            {
                dialog.Style["border-width"] = "1px";
                dialog.Style["border-style"] = "solid";
                dialog.Style["border-color"] = "black";
            }
            if (!dialog.Style.ContainsKey("padding"))
                dialog.Style["padding"] = "1em";
            if (!dialog.Style.ContainsKey("background") &&
                !dialog.Style.ContainsKey("background-color"))
                dialog.Style["background-color"] = "white";
        }
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
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
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
                    registry[anchorName] = box;
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
                double w = TryParsePx(parentProps.GetValueOrDefault("width")) ?? _viewportWidth;
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
                double h = TryParsePx(parentProps.GetValueOrDefault("height")) ?? _viewportHeight;
                h -= TryParsePx(parentProps.GetValueOrDefault("padding-top")) ?? 0;
                h -= TryParsePx(parentProps.GetValueOrDefault("padding-bottom")) ?? 0;
                return h;
            }
            parent = parent.Parent;
        }
        return _viewportHeight;
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
        HashSet<DomElement> scrollContainersNeedingRelative)
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
                var scrollContainer = anchorEl != null
                    ? FindNearestScrollContainer(anchorEl)
                    : null;

                // Compute the grid cell using the anchor's scroll container
                // (or the viewport) as the containing block.
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

                    element.Style["left"] = $"{rect.Value.Left.ToString(CultureInfo.InvariantCulture)}px";
                    element.Style["top"] = $"{rect.Value.Top.ToString(CultureInfo.InvariantCulture)}px";

                    // Preserve the element's own explicit dimensions when set.
                    // The position-area cell defines the maximum available area,
                    // not the element's intrinsic size.
                    double resolvedW = rect.Value.Width;
                    double resolvedH = rect.Value.Height;

                    double? explicitW = TryParsePx(cssProps.GetValueOrDefault("width"));
                    double? explicitH = TryParsePx(cssProps.GetValueOrDefault("height"));

                    if (explicitW.HasValue && explicitW.Value > 0)
                        resolvedW = Math.Min(explicitW.Value, rect.Value.Width);
                    if (explicitH.HasValue && explicitH.Value > 0)
                        resolvedH = Math.Min(explicitH.Value, rect.Value.Height);

                    element.Style["width"] = $"{resolvedW.ToString(CultureInfo.InvariantCulture)}px";
                    element.Style["height"] = $"{resolvedH.ToString(CultureInfo.InvariantCulture)}px";

                    // Record the scroll container for deferred position:relative.
                    if (scrollContainer != null)
                        scrollContainersNeedingRelative.Add(scrollContainer);

                    // Store resolved offsets for JS offset property queries.
                    element.DomProperties["_resolvedLeft"] = rect.Value.Left;
                    element.DomProperties["_resolvedTop"] = rect.Value.Top;
                    element.DomProperties["_resolvedWidth"] = resolvedW;
                    element.DomProperties["_resolvedHeight"] = resolvedH;
                }
            }
        }

        foreach (var child in element.Children)
            ResolvePositionAreaValues(child, anchorRegistry, scrollContainersNeedingRelative);
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

        if (scrollContainer != null)
        {
            // Use the scroll container's own dimensions as the CB.
            var scProps = GetComputedProps(scrollContainer);
            cbWidth = TryParsePx(scProps.GetValueOrDefault("width")) ?? _viewportWidth;
            cbHeight = TryParsePx(scProps.GetValueOrDefault("height")) ?? _viewportHeight;

            // Compute anchor position relative to the scroll container.
            var anchorRelPos = ComputeAnchorRelativeToContainer(anchor, scrollContainer);
            anchorLeft = anchorRelPos.Left;
            anchorRight = anchorRelPos.Left + anchor.Width;
            anchorTop = anchorRelPos.Top;
            anchorBottom = anchorRelPos.Top + anchor.Height;
        }
        else
        {
            cbWidth = FindContainingBlockWidth(element);
            cbHeight = FindContainingBlockHeight(element);
            anchorLeft = anchor.Left;
            anchorRight = anchor.Right;
            anchorTop = anchor.Top;
            anchorBottom = anchor.Bottom;
        }

        // Grid column edges: CB-left, anchor-left, anchor-right, max(CB-right, anchor-right)
        double gridLeft = 0; // CB left (in CB coordinates)
        double gridRight = Math.Max(cbWidth, anchorRight);

        // Grid row edges: when the anchor extends above the CB (anchorTop < 0),
        // the grid top is clamped to the anchor top, not the CB top (which is 0).
        double gridTop = Math.Min(0, anchorTop);
        double gridBottom = Math.Max(cbHeight, anchorBottom);

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
    /// Computes the anchor's position relative to the specified container by
    /// subtracting the container's document-coordinate position from the
    /// anchor's document-coordinate position.
    /// </summary>
    private (double Left, double Top) ComputeAnchorRelativeToContainer(
        AnchorInfo anchor, DomElement container)
    {
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
