namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
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

    // All anchors, keyed by anchor-name, in document order. The name→single
    // <see cref="AnchorInfo"/> registry keeps only the last element for a name;
    // this keeps every element so a query can bind to the anchor in its own
    // scope when several elements share an anchor-name (see
    // <see cref="ResolveAnchorForElement"/>). Rebuilt by BuildAnchorRegistry.
    private Dictionary<string, List<AnchorInfo>>? _anchorCandidates;

    private void BuildAnchorRegistry(Dictionary<string, AnchorInfo> registry)
    {
        var candidates = new Dictionary<string, List<AnchorInfo>>(StringComparer.Ordinal);
        foreach (var el in Elements)
        {
            if (el.IsTextNode)
                continue;

            var declarations = CollectMatchedRuleProperties(el);
            if (!declarations.TryGetValue("anchor-name", out var anchorName))
                continue;

            var box = ComputeElementBox(el);
            if (box != null)
            {
                var info = box with { SourceElement = el };
                registry[anchorName] = info;
                if (!candidates.TryGetValue(anchorName, out var list))
                    candidates[anchorName] = list = [];
                list.Add(info);
            }
        }
        _anchorCandidates = candidates;
    }
    /// <summary>
    /// Resolves the anchor a positioned element binds to for a given
    /// <c>anchor-name</c>. When several elements share that name, CSS binds the
    /// query element to the acceptable anchor <em>in its scope</em> rather than a
    /// single global one; here that is approximated by the candidate inside the
    /// query element's own containing block. Only diverges from the global
    /// name→single registry when duplicates exist <em>and</em> an in-CB candidate
    /// is found, so unique-name lookups (the overwhelming majority) are unchanged.
    /// </summary>
    private AnchorInfo? ResolveAnchorForElement(
        string name, DomElement queryEl, Dictionary<string, AnchorInfo> registry)
    {
        if (_anchorCandidates != null &&
            _anchorCandidates.TryGetValue(name, out var list) && list.Count > 1)
        {
            var cb = FindContainingBlockElement(queryEl);
            if (cb != null)
            {
                AnchorInfo? scoped = null;
                foreach (var cand in list) // document order
                    if (cand.SourceElement != null &&
                        IsDescendantOfElement(cand.SourceElement, cb))
                        scoped = cand; // keep the last in-CB candidate
                if (scoped != null)
                    return scoped;
            }
        }
        return registry.TryGetValue(name, out var global) ? global : (AnchorInfo?)null;
    }
    private AnchorInfo? ComputeElementBox(DomElement element)
    {
        var props = GetComputedProps(element);

        string? position = props.GetValueOrDefault("position");
        bool isPositioned = position == "absolute" || position == "fixed";

        // An absolutely/fixed-positioned anchor whose containing block is an
        // *inline* element (e.g. a `position:relative` <span>) cannot be placed by
        // Broiler's renderer inside that inline box (see PromoteAbsPosFromInlineCBs)
        // — its real layout lands at the inline-flow position, ignoring the box's own
        // left/top insets. The shared-geometry path would therefore register the
        // anchor at the wrong rect (css-anchor-position position-area-inline-container:
        // an abspos anchor at left:100/top:25 in a <span> came back at the end of the
        // preceding text, collapsing all four position-area cells). The CSS-inset
        // estimator below resolves this case exactly from the explicit insets, so
        // bypass the shared path for it.
        bool absPosInInlineCB = isPositioned
            && FindContainingBlockElement(element) is { } inlineCbAncestor
            && IsInlineContainingBlock(inlineCbAncestor);

        // Prefer the renderer's real layout for the anchor rect when the shared
        // geometry path is active (RF-BRIDGE-1b). The CSS-property estimator below
        // cannot model inline flow — an inline anchor after an inline-block, or with
        // real font metrics, is mis-sized/mis-placed (see AnchorScroll* / the
        // css-anchor-position anchor-scroll cluster). Real layout gets the inline
        // border box exactly; convert its document coords to the anchor's
        // containing-block-relative frame (the estimator's convention) so downstream
        // anchor() resolution is unchanged. Falls through to the estimator whenever
        // the geometry is unavailable (flag off, detached, no box) — so this is a
        // no-op unless UseSharedLayoutGeometry is enabled.
        if (!absPosInInlineCB && TryGetAnchorLayoutBox(element, out var layoutBox))
            return layoutBox;

        double width = TryParsePx(props.GetValueOrDefault("width")) ?? 0;
        double height = TryParsePx(props.GetValueOrDefault("height")) ?? 0;

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
    /// Tries to source the anchor's box from the renderer's real layout
    /// (RF-BRIDGE-1b), returning it in the same containing-block-relative frame the
    /// CSS-property estimator uses. This is the accurate path for inline anchors
    /// (inline flow + font metrics), which the estimator cannot model. Returns
    /// <c>false</c> — so the caller falls back to the estimator — whenever the shared
    /// geometry path is disabled (<see cref="UseSharedLayoutGeometry"/>, the default)
    /// or the element produced no usable box, keeping this a no-op until the parity
    /// gate enables real-layout geometry.
    /// </summary>
    private bool TryGetAnchorLayoutBox(DomElement element, out AnchorInfo box)
    {
        box = default!;
        if (!UseSharedLayoutGeometry)
            return false;
        if (!TryGetSharedLayoutGeometry(element, out var geometry))
            return false;

        var border = geometry.BorderBox;
        if (border.Width <= 0 && border.Height <= 0)
            return false;

        // The estimator expresses the box relative to the anchor's nearest
        // containing-block-establishing ancestor's border-box origin (its ancestor
        // walk stops there and folds in that ancestor's border+padding). Match that
        // frame by subtracting the CB's document-space border-box origin.
        double originX = 0, originY = 0;
        var cb = FindGeometryContainingBlockAncestor(element);
        if (cb != null && TryGetSharedLayoutGeometry(cb, out var cbGeometry))
        {
            originX = cbGeometry.BorderBox.Left;
            originY = cbGeometry.BorderBox.Top;
        }

        box = new AnchorInfo(
            border.Top - originY, border.Left - originX, border.Width, border.Height);
        return true;
    }
    /// <summary>
    /// Walks up from <paramref name="element"/> to the nearest ancestor that
    /// establishes a containing block (the frame the estimator measures against).
    /// </summary>
    private DomElement? FindGeometryContainingBlockAncestor(DomElement element)
    {
        for (var ancestor = element.Parent; ancestor != null; ancestor = ancestor.Parent)
            if (EstablishesContainingBlock(GetComputedProps(ancestor)))
                return ancestor;
        return null;
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

            foreach (var kv in CollectMatchedRuleProperties(element))
                props[kv.Key] = kv.Value;
            foreach (var kv in element.Style)
                props[kv.Key] = kv.Value;

            ExpandCssShorthands(props);
            CSS.Dom.CssStyleEngine.ResolveLengthAttrFunctions(props, element);
            ResolveExplicitInheritedValues(props, element);
            ApplyInheritedProperties(props, element);

            // Expand the inset shorthand → top, right, bottom, left so that
            // downstream code (ComputeElementBox, TryApplyFallback, etc.) can
            // read the individual inset properties directly.
            if (props.TryGetValue("inset", out var insetVal2))
            {
                var parts = insetVal2.Split([' ', '\t'],
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
            _computedPropsInProgress.TryRemove(element, out _);
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

            if (CSS.Dom.CssComputedDefaults.InitialValues.TryGetValue(key, out var initialValue))
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
        // Snapshot the sibling list before walking it: element.Parent.Children
        // enumerates the live ChildNodes list, so any mutation of it during the
        // walk (e.g. an anchor-driven DOM move on a node sharing this parent, or
        // a lazy offset query that re-enters anchor resolution) throws
        // "Collection was modified" mid-traversal and aborts the registry build
        // for the whole document (WPT issue #1131, signature
        // DomBridge.BuildAnchorRegistry). Same defensive snapshot (SnapshotChildren)
        // idiom as ResolveAnchorCenter and InlineContainingBlocks.
        foreach (var sibling in SnapshotChildren(element.Parent))
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
}
