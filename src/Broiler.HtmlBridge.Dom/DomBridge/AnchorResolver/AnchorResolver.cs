using System.Globalization;
using System.Text.RegularExpressions;
using Broiler.Dom;

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
    /// Native anchor-placement mode (HtmlBridge complexity-reduction roadmap Phase 5
    /// item 3, P5.8d). Default off. When on, the bridge stops pre-baking
    /// <c>position-area</c> into inline pixel styles <em>for the MVP subset</em> —
    /// a box with <c>position-area</c>, an explicit dashed-ident <c>position-anchor</c>,
    /// a non-inline containing block, no intervening scroll container, and no
    /// <c>position-try</c>/<c>anchor()</c> — leaving that box's
    /// <c>position-area</c>/<c>anchor-name</c>/<c>position-anchor</c> CSS intact so the
    /// Broiler.Layout engine's anchor-placement post-pass (gated by
    /// <c>Broiler.Layout.Engine.NativeAnchorPlacement.Enabled</c>) places it during the
    /// final render. Boxes outside the MVP subset stay on the bridge path: they are
    /// baked as usual and marked <c>position-area: none</c> inline so the engine
    /// post-pass skips them. When off, every box is baked exactly as today.
    /// Internal: driven by the WPT runner lever and tests; off in production until the
    /// cutover is complete.
    /// </summary>
    internal bool NativeAnchorPlacement { get; set; }

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

        // 2. Resolve anchor() function values on elements. Native mode passes the
        //    parsed @position-try rules so the anchor()-inset MVP gate can hand off a
        //    position-try box only when the engine has its fallback rules (via the
        //    NativeAnchorPlacement.PositionTryRules channel).
        ResolveAnchorFunctions(DocumentElement, anchorRegistry, positionTryRules);

        // 3. Resolve position-area values on anchored elements.
        //    Collects scroll containers that need position:relative but
        //    defers adding it until after position-visibility resolution,
        //    so IsAnchorVisibleForTarget is not affected by the new CB.
        var scrollContainersNeedingRelative = new HashSet<DomElement>();
        var deferredDomMoves = new List<(DomElement element, DomElement oldParent, DomElement newParent)>();
        // Native mode (P5.8d) makes this a per-element decision: MVP-subset boxes are
        // left un-baked (their CSS survives for the engine post-pass) while every other
        // box is baked as usual — see ResolvePositionAreaValues.
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
        //    misinterpret them. Native mode (P5.8d) keeps position-area/anchor-name/
        //    position-anchor so the engine's post-pass can consume them.
        if (!NativeAnchorPlacement)
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
        var match = ExtractContentImageUrlRegex().Match(content);
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

    [GeneratedRegex(@"url\(\s*(['""]?)(?<u>[^'""\)]+)\1\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex ExtractContentImageUrlRegex();
}
