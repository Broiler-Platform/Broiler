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
        GetElementRuntimeState(DocumentElement).Scroll.Left.Set(GetVisualViewportPageOffset(vertical: false));
        GetElementRuntimeState(DocumentElement).Scroll.Top.Set(GetVisualViewportPageOffset(vertical: true));
    }
}
