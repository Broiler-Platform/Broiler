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
    /// Phase 5 LayoutSnapshot endgame, blocker (b) — visual-viewport. When on, the bridge stops
    /// depending on the DOM `zoom` bake for the document-root pinch-zoom of the geometry read model:
    /// it sets <c>Broiler.Layout.Engine.NativeAnchorPlacement.VisualViewportScale</c> around the
    /// shared geometry snapshot (so the extracted <c>BoxGeometry</c> is scaled natively — requires
    /// <c>patches/0006-html-visual-viewport-extraction-scale.patch</c>), and folds the same scale
    /// into <see cref="GetUsedZoomForElement"/> as a root-level zoom so <c>offset*</c> divides it
    /// back out and <c>getBoundingClientRect</c> keeps it (CSSOM-View: pinch-zoom is a root zoom in
    /// this model). **On by default now that patch 0006 is applied and pinned** — the extraction scale
    /// and the read-path fold are inverse halves of one balance, and with 0006 at the pinned SHA both
    /// are live, so the native pinch-zoom read model is authoritative. Inert for a non-pinch page
    /// (<c>HasActiveVisualViewport()</c> is false unless the page sets <c>visualViewport.scale &gt; 1</c>),
    /// so the blast radius is pinch-zoomed pages only. The WPT-runner render `zoom` bake path
    /// (<c>ApplyVisualViewportSerializationState</c>, called only from <c>ResolveAnchorPositions</c>) is
    /// a separate path and is untouched.
    /// </summary>
    internal bool NativeVisualViewport { get; set; } = true;

    /// <summary>
    /// Phase 5 native dialog/backdrop track — top-layer paint. When on, the bridge stamps a
    /// benign <c>data-broiler-top-layer</c> order marker on open modal dialogs, open popovers,
    /// and synthesized <c>::backdrop</c>s (<c>Dialogs.cs</c>). The Broiler.Layout
    /// <c>FragmentTreeBuilder</c> projects it to <see cref="Broiler.Layout.IR.Fragment.TopLayerOrder"/>,
    /// and the renderer's native top-layer paint pass (<c>PaintWalker.PaintTopLayer</c>) paints
    /// those boxes above every ordinary stacking context — the correct CSS Position 4 §top-layer
    /// behaviour, replacing the bridge's approximate very-large-z-index emulation. Off by default:
    /// the marker is inert until the renderer's native-top-layer paint patch is applied (the
    /// pinned <c>PaintWalker</c> never reads the projected order), so stamping stays gated to the
    /// native render path (the WPT runner enables it alongside <see cref="NativeAnchorPlacement"/>)
    /// and the default/production serialization is unchanged.
    /// </summary>
    internal bool NativeTopLayer { get; set; }

    /// <summary>
    /// Phase 5 native dialog/backdrop track — native <c>::backdrop</c>. When on, the bridge stops
    /// synthesizing a backdrop <c>&lt;div&gt;</c> in <c>InsertDialogBackdrops</c> and instead
    /// stamps the resolved backdrop background (<c>data-broiler-backdrop</c>) on the top-layer
    /// element; the renderer generates the <c>::backdrop</c> box natively (Broiler.HTML DomParser,
    /// patch 0011, which depends on the 0010 top-layer paint). Off by default and — unlike
    /// <see cref="NativeTopLayer"/> — <em>not</em> auto-enabled by the WPT runner: the synthesized
    /// <c>&lt;div&gt;</c> is the CI fallback until patch 0011 is applied (the pinned renderer would
    /// otherwise drop backdrops entirely on the WPT path). Requires <see cref="NativeTopLayer"/> to
    /// also be on (the native backdrop is painted by the top-layer pass).
    /// </summary>
    internal bool NativeBackdrop { get; set; }

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
        //    Collects scroll containers that need position:relative but defers stamping it
        //    (step 3e) until after the deferred DOM moves, tagging each anchor-induced scroller
        //    with data-broiler-anchor-cb so the engine's native position-visibility pass still
        //    treats it as an intervening clip container.
        var scrollContainersNeedingRelative = new HashSet<DomElement>();
        var deferredDomMoves = new List<(DomElement element, DomElement oldParent, DomElement newParent)>();
        // Native mode (P5.8d) makes this a per-element decision: MVP-subset boxes are
        // left un-baked (their CSS survives for the engine post-pass) while every other
        // box is baked as usual — see ResolvePositionAreaValues.
        ResolvePositionAreaValues(
            DocumentElement, anchorRegistry, scrollContainersNeedingRelative,
            deferredDomMoves);

        // 3a2. align-self/justify-self: anchor-center on elements with position-anchor but no
        //      position-area is centred natively by the engine post-pass
        //      (CssBox.TryApplyAnchorCenter), so the align-self/justify-self + position-anchor
        //      CSS reaches the render un-baked. The redundant bridge `ResolveAnchorCenter` pass
        //      was deleted in Phase 4 item-2 step 3 now that native is the default.

        // 3b. Resolve position-try-fallbacks for elements whose base
        //     style overflows the containing block.
        ResolvePositionTryFallbacks(DocumentElement, anchorRegistry, positionTryRules);

        // 3c. position-visibility (hide anchor-positioned elements whose anchor is not visible or
        //     does not exist) is resolved natively by the Broiler.Layout engine post-pass
        //     (CssBox.ResolvePositionVisibility); the scroll-container marker stamped in step 3e
        //     gives the engine the pre-position:relative CB view the decision needs. The redundant
        //     bridge `ResolvePositionVisibility` display:none pre-bake was deleted in Phase 4
        //     item-2 step 3 now that native is the default.

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
            {
                InlineStyle(sc)["position"] = "relative";
                // Native mode: mark this scroller so the engine's position-visibility pass knows
                // its position:relative is anchor-induced (not authored) and must still count as an
                // intervening clip container — reproducing the bridge's pre-position:relative CB
                // view without which a static-scroller target would be wrongly kept visible.
                if (NativeAnchorPlacement)
                    SetAttr(sc, "data-broiler-anchor-cb", "1");
            }
        }

        // 4. Insert backdrop elements for modal dialogs.
        InsertDialogBackdrops(
            DocumentElement, viewportWidth, viewportHeight,
            anchorRegistry, positionTryRules);

        // 5. Fixed-position sizing from opposing insets (e.g. top:0;bottom:0) is resolved
        //    natively by the Broiler.Layout engine (CSS2.1 §10.3.7, incl. the fixed→viewport
        //    containing block and the `inset` shorthand). The bridge's `ResolveFixedPositionSizing`
        //    pre-bake was proven redundant (P5.8d.2b) and is deleted now that native is the
        //    default (Phase 4 item-2 step 3) — see NativeFixedSizingTests for the engine parity.

        // 6. Elements that establish containing blocks via non-position properties
        //    (contain:layout, transform, will-change:transform) are resolved natively by the
        //    Broiler.Layout engine (CssBox.FindPositionedContainingBlock +
        //    EstablishesNonPositionAbsPosContainingBlock), so no bridge pre-bake is needed. The
        //    redundant `EnsureContainingBlockPositioning` pass — which baked position:relative
        //    onto those establishers for the static renderer — was deleted in Phase 4 item-2
        //    step 3 now that native is the default. See NativeAnchorContainCbWptTests for parity.

        // 7. The stylesheet's anchor-positioning rules (position-area, anchor-name,
        //    position-anchor, anchor()/anchor-size()) are consumed directly by the engine's
        //    native post-pass, so they must reach the renderer un-stripped. The bridge's
        //    `NeutralizeStyleElementsForAnchorRules` pass — which rewrote <style> text to strip
        //    those rules for the static renderer — was deleted in Phase 4 item-2 step 3 now that
        //    native is the default.

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
        ScrollStateFor(DocumentElement).Left.Set(GetVisualViewportPageOffset(vertical: false));
        ScrollStateFor(DocumentElement).Top.Set(GetVisualViewportPageOffset(vertical: true));
    }

    [GeneratedRegex(@"url\(\s*(['""]?)(?<u>[^'""\)]+)\1\s*\)", RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex ExtractContentImageUrlRegex();
}
