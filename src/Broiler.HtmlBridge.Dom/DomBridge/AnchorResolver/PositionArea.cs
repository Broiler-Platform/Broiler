using System.Globalization;
using Broiler.CSS;
using Broiler.Dom;
using Broiler.Layout;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // position-area resolution
    // -----------------------------------------------------------------

    /// <summary>
    /// Resolves <c>position-area</c> values on elements that have
    /// <c>position-anchor</c>.  Computes the 3×3 grid from the anchor
    /// element's position and the containing block, then selects the
    /// region specified by position-area and sets explicit inline styles.
    /// </summary>
    private void ResolvePositionAreaValues(DomElement element, Dictionary<string, AnchorInfo> anchorRegistry,
        HashSet<DomElement> scrollContainersNeedingRelative,
        List<(DomElement element, DomElement oldParent, DomElement newParent)> deferredDomMoves)
    {
        if (!IsText(element))
        {
            var cssProps = CollectMatchedRuleProperties(element);
            foreach (var kv in InlineStyle(element))
                cssProps[kv.Key] = kv.Value;

            string? positionArea = cssProps.GetValueOrDefault("position-area");
            string? positionAnchor = cssProps.GetValueOrDefault("position-anchor");

            if (!string.IsNullOrWhiteSpace(positionArea) &&
                positionArea != "none" &&
                !string.IsNullOrWhiteSpace(positionAnchor) &&
                ResolveAnchorForElement(positionAnchor, element, anchorRegistry) is { } anchor)
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
                        InlineStyle(element)["position"] = "absolute";
                    }
                    else
                    {
                        InlineStyle(element)["position"] = "fixed";
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
                        var insetParts = rawInset.Split([' ', '\t'],
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
                        var mp = marginShorthand.Split([' ', '\t'],
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
                        var pp = padShorthand.Split([' ', '\t'],
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

                    // Resolve the element's box within the cell — the IMCB
                    // (inset-modified containing block), the used width/height
                    // (percentages against the cell, explicit lengths clamped to it,
                    // else fill the IMCB) and the alignment-based position — via the
                    // canonical Broiler.Layout model (Phase 5 item 3). The bridge keeps
                    // the CSS parsing above and the box-sizing / percentage-box
                    // branches below.
                    string? rawW = cssProps.GetValueOrDefault("width");
                    string? rawH = cssProps.GetValueOrDefault("height");
                    var box = PositionAreaGrid.ResolveElementBox(
                        rect.Value,
                        insetTop, insetRight, insetBottom, insetLeft,
                        TryParsePx(rawW), TryParsePercent(rawW),
                        TryParsePx(rawH), TryParsePercent(rawH),
                        PositionAreaValue.Parse(positionArea));

                    double imcbLeft = box.ImcbLeft, imcbTop = box.ImcbTop;
                    double imcbW = box.ImcbWidth, imcbH = box.ImcbHeight;
                    double resolvedW = box.Width, resolvedH = box.Height;
                    double finalLeft = box.Left, finalTop = box.Top;

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
                                    InlineStyle(blockAncestor)["position"] = "relative";
                            }

                            // Defer the move to avoid collection modification
                            // during tree traversal.
                            if (blockAncestor != null && ParentEl(element) != blockAncestor
                                && ParentEl(element) != null)
                            {
                                deferredDomMoves.Add((element, ParentEl(element), blockAncestor));
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
                            var borderParts = borderShort.Split([' ', '\t'],
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

                        // Content = IMCB minus margin/border/padding on each axis
                        // (canonical Broiler.Layout used-value math, Phase 5 item 3).
                        var (contentW, contentH) = PositionAreaGrid.ContentSizeFillingImcb(
                            imcbW, imcbH,
                            new PositionAreaEdges(marginTop2, marginRight2, marginBottom2, marginLeft2),
                            new PositionAreaEdges(borderN, borderE, borderS, borderW),
                            new PositionAreaEdges(padTop, padRight, padBottom, padLeft));

                        finalLeft = imcbLeft;
                        finalTop = imcbTop;

                        // Set resolved pixel values for margins and padding
                        // to override the percentage values from CSS.
                        InlineStyle(element)["margin-top"] = $"{marginTop2.ToString(CultureInfo.InvariantCulture)}px";
                        InlineStyle(element)["margin-right"] = $"{marginRight2.ToString(CultureInfo.InvariantCulture)}px";
                        InlineStyle(element)["margin-bottom"] = $"{marginBottom2.ToString(CultureInfo.InvariantCulture)}px";
                        InlineStyle(element)["margin-left"] = $"{marginLeft2.ToString(CultureInfo.InvariantCulture)}px";
                        InlineStyle(element)["padding-top"] = $"{padTop.ToString(CultureInfo.InvariantCulture)}px";
                        InlineStyle(element)["padding-right"] = $"{padRight.ToString(CultureInfo.InvariantCulture)}px";
                        InlineStyle(element)["padding-bottom"] = $"{padBottom.ToString(CultureInfo.InvariantCulture)}px";
                        InlineStyle(element)["padding-left"] = $"{padLeft.ToString(CultureInfo.InvariantCulture)}px";
                        InlineStyle(element).Remove("margin");
                        InlineStyle(element).Remove("padding");
                        InlineStyle(element).Remove("inset");

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

                        // border-box → content-box: subtract border + padding per axis
                        // (canonical Broiler.Layout used-value math, Phase 5 item 3).
                        (resolvedW, resolvedH) = PositionAreaGrid.BorderBoxToContentSize(
                            resolvedW, resolvedH,
                            new PositionAreaEdges(bdrT, bdrR, bdrB, bdrL),
                            new PositionAreaEdges(padTop, padRight, padBottom, padLeft));

                        // Override border-width with explicit pixel values so
                        // the renderer doesn't use its own keyword mapping
                        // (which maps medium→2px instead of the spec's 3px).
                        // Always set the value (even 0px) to ensure the
                        // renderer doesn't fall back to its keyword defaults.
                        InlineStyle(element)["border-top-width"] = $"{bdrT.ToString(CultureInfo.InvariantCulture)}px";
                        InlineStyle(element)["border-right-width"] = $"{bdrR.ToString(CultureInfo.InvariantCulture)}px";
                        InlineStyle(element)["border-bottom-width"] = $"{bdrB.ToString(CultureInfo.InvariantCulture)}px";
                        InlineStyle(element)["border-left-width"] = $"{bdrL.ToString(CultureInfo.InvariantCulture)}px";
                    }

                    InlineStyle(element)["left"] = $"{finalLeft.ToString(CultureInfo.InvariantCulture)}px";
                    InlineStyle(element)["top"] = $"{finalTop.ToString(CultureInfo.InvariantCulture)}px";
                    InlineStyle(element)["width"] = $"{resolvedW.ToString(CultureInfo.InvariantCulture)}px";
                    InlineStyle(element)["height"] = $"{resolvedH.ToString(CultureInfo.InvariantCulture)}px";

                    // Record the scroll container for deferred position:relative.
                    if (scrollContainer != null)
                        scrollContainersNeedingRelative.Add(scrollContainer);

                    // Store resolved offsets for JS offset property queries.
                    // Use border-box dimensions (matching offsetWidth/offsetHeight).
                    SetPositionAreaResolution(element, finalLeft, finalTop, borderBoxW, borderBoxH);
                }
            }
        }

        // Snapshot before the recursive descent: element.Children enumerates the
        // live ChildNodes list, and resolving a child can re-enter anchor
        // resolution through a lazy offset/box query (ComputeElementBox →
        // ResolvePositionAreaForElement) or surface a DOM move on a node sharing
        // this parent, mutating the list mid-walk and throwing "Collection was
        // modified" — which aborts position-area resolution for the whole
        // document (WPT issue #1147, signature
        // DomBridge.ResolvePositionAreaValues). Same defensive snapshot idiom
        // (SnapshotChildren) as BuildAnchorRegistry / ResolveAnchorCenter /
        // InlineContainingBlocks.
        foreach (var child in SnapshotChildren(element))
            ResolvePositionAreaValues(child, anchorRegistry, scrollContainersNeedingRelative,
                deferredDomMoves);
    }

    /// <summary>
    /// Computes the grid cell for a given <c>position-area</c> value using
    /// the 3×3 grid defined by the anchor element and containing block.
    /// When <paramref name="scrollContainer"/> is provided, the grid is
    /// computed relative to that container (the anchor's scroll container)
    /// rather than the target element's normal containing block.
    /// </summary>
    private PositionAreaCell? ComputePositionAreaRect(DomElement element, AnchorInfo anchor, string positionArea,
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
            // The scroll container is the CB for the target — either it already
            // establishes a CB, or ResolvePositionAreaValues will apply
            // position:relative to it via scrollContainersNeedingRelative — so
            // keying the grid off the scrollable extent is what the target sees.
            // Otherwise a target with position-area at the anchor's own edge
            // (e.g. "bottom" on an anchor at the scrollport bottom) collapses to
            // a zero-height cell — the "bottom" row spans gridBottom..anchorBottom
            // which are both the scrollport bottom (WPT #1177,
            // position-visibility-remove-anchors-visible).
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

                // ComputeElementBox measures the anchor relative to its nearest
                // CB-establishing ancestor and stops there. When that ancestor is
                // a non-body wrapper (e.g. a transformed div), the returned coords
                // are wrapper-relative, not document-absolute as assumed above.
                // Shift the anchor into the document frame by adding the wrapper's
                // own position (css-anchor-position transform-005: a transformed
                // anchor wrapper below a <p> otherwise placed block-end one text
                // line too high, exposing the red containing block).
                var anchorWrapper = anchor.SourceElement != null
                    ? FindContainingBlockElement(anchor.SourceElement)
                    : null;
                if (anchorWrapper != null)
                {
                    var wrapperBox = ComputeElementBox(anchorWrapper);
                    if (wrapperBox != null)
                    {
                        anchorLeft += wrapperBox.Left;
                        anchorRight += wrapperBox.Left;
                        anchorTop += wrapperBox.Top;
                        anchorBottom += wrapperBox.Top;
                    }
                }
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

        // The 3×3 grid geometry (edges from the CB∪anchor union, cell selection per
        // block/inline span) is the canonical Broiler.Layout.PositionAreaGrid model
        // (Phase 5 item 3). This method keeps the DOM-dependent resolution of the CB
        // frame and the anchor's edges above; the neutral grid math is delegated.
        return PositionAreaGrid.ComputeCell(
            cbOffsetX, cbOffsetY, cbWidth, cbHeight,
            anchorLeft, anchorTop, anchorRight, anchorBottom,
            PositionAreaValue.Parse(positionArea));
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
        foreach (var child in SnapshotChildren(scrollContainer))
        {
            if (IsText(child)) continue;
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
    /// container.  In-flow block children stack vertically, so the scrollable
    /// extent is the <em>sum</em> of their heights and margins (not the tallest
    /// single child, which is what the inline/horizontal axis uses).  Absolutely
    /// and fixed positioned children are out of flow and do not contribute to
    /// the block-axis scroll extent.  The result is clamped to at least the
    /// container's own height (scrollHeight ≥ clientHeight).
    /// </summary>
    private double FindScrollContentHeight(DomElement scrollContainer, double containerHeight)
    {
        double stackedHeight = 0;
        foreach (var child in SnapshotChildren(scrollContainer))
        {
            if (IsText(child)) continue;
            var childProps = GetComputedProps(child);
            var pos = childProps.GetValueOrDefault("position");
            if (pos == "absolute" || pos == "fixed")
                continue;
            double childH = TryParsePx(childProps.GetValueOrDefault("height")) ?? 0;
            double mt = TryParsePx(childProps.GetValueOrDefault("margin-top")) ?? 0;
            double mb = TryParsePx(childProps.GetValueOrDefault("margin-bottom")) ?? 0;
            stackedHeight += childH + mt + mb;
        }
        return Math.Max(containerHeight, stackedHeight);
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
        var parent = ParentEl(el);
        while (parent != null)
        {
            if (!IsText(parent))
            {
                var props = GetComputedProps(parent);
                if (HasOverflowClipping(props))
                    return parent;
            }
            parent = ParentEl(parent);
        }
        return null;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="el"/> is a descendant of
    /// <paramref name="potentialAncestor"/> in the DOM tree.
    /// </summary>
    private static bool IsDescendantOfElement(DomElement el, DomElement potentialAncestor)
    {
        var current = ParentEl(el);
        while (current != null)
        {
            if (ReferenceEquals(current, potentialAncestor)) return true;
            current = ParentEl(current);
        }
        return false;
    }
}
