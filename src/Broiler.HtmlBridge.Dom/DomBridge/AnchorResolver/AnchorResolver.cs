using System.Globalization;

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
        // -1. CSS Content 3 element replacement: when the root element's
        //     `content` computes to a replaced value (an image), the root is
        //     replaced by that image and its descendants generate no boxes —
        //     so, per CSS Backgrounds 3 §body-background, the body background
        //     no longer propagates to the canvas.  Reproduce this by replacing
        //     the document body with a single <img> at the origin.
        ReplaceRootWithReplacedContent();

        // 0. Apply UA default position:fixed to modal dialogs before anchor
        //    resolution, since browsers treat top-layer elements as fixed.
        ApplyDialogUAPositioning(DocumentElement);
        ApplyPopoverUAPositioning(DocumentElement);

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
            RemoveChildFrom(oldParent, el);
            newParent.AppendChild(el);
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
                InlineStyle(sc)["position"] = "relative";
        }

        // 4. Insert backdrop elements for modal dialogs.
        InsertDialogBackdrops(
            DocumentElement, viewportWidth, viewportHeight,
            anchorRegistry, positionTryRules);

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

        // 7b. Resolve position:sticky offsets into position:relative so the
        //     static renderer pins sticky boxes to their scroll container /
        //     containing-block edges.  Runs before scroll simulation so the
        //     rewritten (relative) boxes flow through the normal path.
        ResolveStickyPositioning(DocumentElement);

        // 8. Apply scroll simulation: shift content in scroll containers
        //    where JavaScript set scrollTop/scrollLeft to match Chromium output.
        ApplyScrollSimulation(DocumentElement);

        // Drop any shared-layout-geometry snapshot built for anchor-box geometry
        // (TryGetAnchorLayoutBox) so later live geometry queries lay out fresh —
        // the resolution above mutated the DOM. No-op when the shared path is off.
        ClearSharedGeometrySnapshot();
    }

    /// <summary>
    /// CSS Content 3 §"content" element replacement: when the root element's
    /// computed <c>content</c> is a replaced value (an image <c>url()</c>), the
    /// root element is replaced by that image; its normal children generate no
    /// boxes.  Because the root then has no in-flow body descendant contributing
    /// a background, the body background does not propagate to the canvas
    /// (CSS Backgrounds 3 §body-background).  Broiler does not model non-pseudo
    /// element replacement in layout, so reproduce the visual result here: strip
    /// the root/body backgrounds that would otherwise reach the canvas and
    /// replace the body's contents with a single <c>&lt;img&gt;</c> of the
    /// replaced image, positioned at the initial containing block origin.
    /// </summary>
    private void ReplaceRootWithReplacedContent()
    {
        var html = FindFirstElementByTagName(DocumentElement, "html");
        if (html == null)
            return;

        var content = GetComputedProps(html).GetValueOrDefault("content")?.Trim();
        if (string.IsNullOrEmpty(content))
            return;

        var url = ExtractContentImageUrl(content);
        if (url == null)
            return;

        var body = FindFirstElementByTagName(html, "body");
        if (body == null)
            return;

        // The replaced root paints only the image; neither the root nor the
        // (box-less) body background reaches the canvas.
        InlineStyle(html)["background"] = "none";
        InlineStyle(html)["background-color"] = "transparent";
        InlineStyle(body)["margin"] = "0";
        InlineStyle(body)["padding"] = "0";
        InlineStyle(body)["background"] = "none";
        InlineStyle(body)["background-color"] = "transparent";

        ClearChildren(body);

        var img = CreateBridgeElement(
            "img",
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["src"] = url,
            });
        SetParent(img, body);
        body.AppendChild(img);
    }

    /// <summary>
    /// Extracts the target of a CSS <c>url(...)</c> value (used by the root
    /// element-replacement path).  Returns <c>null</c> for non-<c>url()</c>
    /// content (strings, counters, <c>normal</c>/<c>none</c>).
    /// </summary>
    private static string? ExtractContentImageUrl(string content)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            content, @"url\(\s*(['""]?)(?<u>[^'""\)]+)\1\s*\)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["u"].Value.Trim() : null;
    }

    private void ApplyVisualViewportSerializationState()
    {
        if (!HasActiveVisualViewport())
            return;

        var scale = GetVisualViewportScale();
        if (!double.IsFinite(scale) || scale <= 1.0001)
            return;

        var combinedZoom = GetUsedZoomForElement(DocumentElement) * scale;
        InlineStyle(DocumentElement)["zoom"] = combinedZoom.ToString("0.###", CultureInfo.InvariantCulture);
        GetElementRuntimeState(DocumentElement).Scroll.Left.Set(GetVisualViewportPageOffset(vertical: false));
        GetElementRuntimeState(DocumentElement).Scroll.Top.Set(GetVisualViewportPageOffset(vertical: true));
    }
}
