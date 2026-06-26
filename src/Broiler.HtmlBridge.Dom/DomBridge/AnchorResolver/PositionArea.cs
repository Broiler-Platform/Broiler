using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

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
    private void ResolvePositionAreaValues(
        DomElement element,
        Dictionary<string, AnchorInfo> anchorRegistry,
        HashSet<DomElement> scrollContainersNeedingRelative,
        List<(DomElement element, DomElement oldParent, DomElement newParent)> deferredDomMoves)
    {
        if (!element.IsTextNode)
        {
            var cssProps = CollectMatchedRuleProperties(element);
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
                    GetElementRuntimeState(element).Layout.Left.Set(finalLeft);
                    GetElementRuntimeState(element).Layout.Top.Set(finalTop);
                    GetElementRuntimeState(element).Layout.Width.Set(borderBoxW);
                    GetElementRuntimeState(element).Layout.Height.Set(borderBoxH);
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
}
