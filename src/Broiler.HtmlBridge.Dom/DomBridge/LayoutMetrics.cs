using System.Text;
using System.Text.RegularExpressions;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.HtmlBridge.Logging;
using Broiler.Dom;
using System.Globalization;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // RF-BRIDGE-1b: tracks whether a shared-geometry read pass is active. The pass reads
    // one static post-script layout snapshot (_sharedGeometrySnapshot, built up front by
    // WithLayoutGeometryCache); nested WithLayoutGeometryCache calls share the outermost
    // pass's snapshot, and only the owner builds and tears it down — so live JS geometry
    // queries (where the DOM may mutate between calls) each lay out fresh. The old coarse
    // estimators recursed up/down/across the tree and fanned out exponentially on deep
    // css-align / css-anchor-position trees (WPT #1113); they are gone now — the shared
    // renderer layout is the sole geometry source.
    private bool _layoutGeometryPassActive;

    // Per-pass memos for the document-wide anchor inputs the live anchor-geometry resolvers
    // (position-area / anchor()-inset / anchor-size() / position-try) all need: the anchor
    // registry (a full Elements walk + per-anchor box computation) and the parsed @position-try
    // rules (a tree walk + CSS parse). A single getBoundingClientRect over an anchor box invokes
    // several resolvers (and each offset getter re-invokes them, plus the offsetParent recursion),
    // so building these once per read pass — the DOM/styles are static within a WithLayoutGeometryCache
    // pass — avoids the O(anchors) rebuild fan-out. Null outside a pass (each build is fresh, matching
    // the render bake); cleared with the geometry snapshot when the owning pass ends.
    private Dictionary<string, AnchorInfo>? _passAnchorRegistry;
    private Dictionary<string, Dictionary<string, string>>? _passPositionTryRules;

    /// <summary>The anchor registry (stylesheet + inline anchors), built once per read pass.</summary>
    private Dictionary<string, AnchorInfo> GetAnchorRegistryForPass()
    {
        if (_layoutGeometryPassActive && _passAnchorRegistry is { } cached)
            return cached;
        var registry = new Dictionary<string, AnchorInfo>(StringComparer.Ordinal);
        BuildAnchorRegistry(registry);
        BuildInlineAnchorRegistry(registry);
        if (_layoutGeometryPassActive)
            _passAnchorRegistry = registry;
        return registry;
    }

    /// <summary>The parsed <c>@position-try</c> rules, parsed once per read pass.</summary>
    private Dictionary<string, Dictionary<string, string>> GetPositionTryRulesForPass()
    {
        if (_layoutGeometryPassActive && _passPositionTryRules is { } cached)
            return cached;
        var rules = ParsePositionTryRules();
        if (_layoutGeometryPassActive)
            _passPositionTryRules = rules;
        return rules;
    }

    // Test seam retained so the equivalence tests compile. The shared snapshot is the
    // single geometry source now, so toggling this no longer selects an alternate path.
    internal static bool LayoutGeometryCacheEnabled = true;

    /// <summary>
    /// Runs <paramref name="evaluate"/> with the shared-geometry snapshot installed for the
    /// pass, then tears it down. Only sound for a read pass over a static layout snapshot
    /// (no DOM or computed-style mutation mid-pass); nested calls share the outermost pass's
    /// snapshot and only the owner builds/clears it.
    /// </summary>
    private T WithLayoutGeometryCache<T>(Func<T> evaluate)
    {
        if (!LayoutGeometryCacheEnabled)
            return evaluate();

        var owner = !_layoutGeometryPassActive;
        if (owner)
        {
            _layoutGeometryPassActive = true;
            // RF-BRIDGE-1b: build the shared-geometry snapshot up front, before the read
            // pass enumerates the element tree. BuildSharedGeometrySnapshot calls
            // GetRenderDocument, which mutates the document (reflects style into
            // attributes); doing that lazily mid-traversal modified a Children collection
            // that an enclosing foreach was still enumerating (InvalidOperationException).
            // Building once here also bounds it to one layout per pass. Built
            // unconditionally so a stale snapshot left by a non-pass shared query (e.g. the
            // anchor resolver) is overwritten fresh for this pass.
            _sharedGeometrySnapshot = BuildSharedGeometrySnapshot();
        }

        try
        {
            return evaluate();
        }
        finally
        {
            if (owner)
            {
                // RF-BRIDGE-1b: the shared-geometry snapshot is scoped to the same pass.
                ClearSharedGeometrySnapshot();
                _passAnchorRegistry = null;
                _passPositionTryRules = null;
                _layoutGeometryPassActive = false;
            }
        }
    }

    private double GetClientWidthForDomElement(DomElement element, bool isRoot) =>
        WithLayoutGeometryCache(() =>
        {
            if (isRoot)
                return GetViewportReferenceLength(element, vertical: false);

            // RF-BRIDGE-1b: clientWidth is the padding-box width (content + padding),
            // reported in the element's own unzoomed CSS pixels (the shared snapshot is
            // in zoom-baked space, so divide out the element's own used zoom). An element
            // with no shared box (detached / display:none) reports zero.
            if (TryGetSharedLayoutGeometry(element, out var shared))
                return UnzoomSharedExtent(shared.PaddingBox.Width, element);

            return 0;
        });

    private double GetClientHeightForDomElement(DomElement element, bool isRoot) =>
        WithLayoutGeometryCache(() =>
        {
            if (isRoot)
                return GetViewportReferenceLength(element, vertical: true);

            // RF-BRIDGE-1b: clientHeight is the padding-box height (content + padding),
            // reported in the element's own unzoomed CSS pixels (see GetClientWidth). An
            // element with no shared box (detached / display:none) reports zero.
            if (TryGetSharedLayoutGeometry(element, out var shared))
                return UnzoomSharedExtent(shared.PaddingBox.Height, element);

            return 0;
        });

    private double GetClientTopForDomElement(DomElement element)
    {
        var props = GetComputedProps(element);
        return ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-top-width"), element);
    }

    private double GetClientLeftForDomElement(DomElement element)
    {
        var props = GetComputedProps(element);
        return ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-left-width"), element);
    }

    private double GetOffsetWidthForDomElement(DomElement element, bool isRoot) =>
        WithLayoutGeometryCache(() =>
        {
            if (isRoot)
                return GetViewportReferenceLength(element, vertical: false);

            if (ShouldReportZeroOffsetMetrics(element))
                return 0;

            // A live position-area resolution takes precedence over the shared
            // renderer snapshot, matching offset-left/top below: the non-native
            // renderer lays a position-area box out before its grid-cell placement
            // is baked, so its shared border box is 0 — the resolution is the box's
            // actual used size (css-anchor-position position-area-anchor-partially-outside).
            var resolved = ResolvePositionAreaForElement(element);
            if (resolved != null)
                return resolved.Value.width;

            // A live anchor-size() width resolves the box's used size the same way —
            // the pre-bake snapshot sizes an unresolved anchor-size() box to 0.
            if (ResolveAnchorSizeForElement(element) is { width: { } anchorWidth })
                return anchorWidth;

            // A position-try fallback sizes the box when it applies.
            if (ResolvePositionTryForElement(element) is { width: { } tryWidth })
                return tryWidth;

            // An opposing-inset auto-width anchor box is sized by the two resolved insets.
            if (ResolveAnchorInsetForElement(element) is { width: { } insetWidth })
                return insetWidth;

            if (TryGetSharedLayoutGeometry(element, out var shared))
                return UnzoomSharedExtent(shared.BorderBox.Width, element);

            return 0;
        });

    private double GetOffsetHeightForDomElement(DomElement element, bool isRoot) =>
        WithLayoutGeometryCache(() =>
        {
            if (isRoot)
                return GetViewportReferenceLength(element, vertical: true);

            if (ShouldReportZeroOffsetMetrics(element))
                return 0;

            // See GetOffsetWidthForDomElement: a live position-area resolution wins
            // over the shared renderer snapshot (which is 0 pre-bake), matching
            // offset-left/top.
            var resolved = ResolvePositionAreaForElement(element);
            if (resolved != null)
                return resolved.Value.height;

            if (ResolveAnchorSizeForElement(element) is { height: { } anchorHeight })
                return anchorHeight;

            if (ResolvePositionTryForElement(element) is { height: { } tryHeight })
                return tryHeight;

            if (ResolveAnchorInsetForElement(element) is { height: { } insetHeight })
                return insetHeight;

            if (TryGetSharedLayoutGeometry(element, out var shared))
                return UnzoomSharedExtent(shared.BorderBox.Height, element);

            return 0;
        });

    private static bool ShouldReportZeroOffsetMetrics(DomElement element) =>
        string.Equals(element.TagName, "map", StringComparison.OrdinalIgnoreCase);

    private double GetOffsetTopForDomElement(DomElement element) =>
        WithLayoutGeometryCache(() =>
        {
            var resolved = ResolvePositionAreaForElement(element);
            if (resolved != null)
                return resolved.Value.top;

            // A position-try fallback (when the base overflows) overrides the base placement.
            if (ResolvePositionTryForElement(element) is { top: { } tryTop })
                return tryTop;

            // A live anchor() vertical inset resolves the box's top the same way, before the
            // render bake — the pre-bake snapshot lays an anchor()-positioned box at the origin.
            if (ResolveAnchorInsetForElement(element) is { top: { } insetTop })
                return insetTop;

            var offsetParent = GetOffsetParentForDomElement(element);
            if (TryGetSharedOffset(element, offsetParent, vertical: true, out var sharedTop))
                return sharedTop;

            return 0;
        });

    private double GetOffsetLeftForDomElement(DomElement element) =>
        WithLayoutGeometryCache(() =>
        {
            var resolved = ResolvePositionAreaForElement(element);
            if (resolved != null)
                return resolved.Value.left;

            if (ResolvePositionTryForElement(element) is { left: { } tryLeft })
                return tryLeft;

            if (ResolveAnchorInsetForElement(element) is { left: { } insetLeft })
                return insetLeft;

            var offsetParent = GetOffsetParentForDomElement(element);
            if (TryGetSharedOffset(element, offsetParent, vertical: false, out var sharedLeft))
                return sharedLeft;

            return 0;
        });

    /// <summary>
    /// RF-BRIDGE-1b: derive <c>offsetTop</c>/<c>offsetLeft</c> straight from the renderer's
    /// real box geometry when the shared snapshot is active. <c>offsetTop/Left</c> is the
    /// element's border-box edge relative to the offset parent's padding edge (HTML
    /// <c>offsetParent.clientLeft/Top</c> already excludes that border), or the initial
    /// containing block when there is no offset parent. This reflects layout effects the
    /// per-property estimator cannot model — e.g. abspos static-position alignment
    /// (<c>align-self</c>/<c>justify-self</c>) in vertical writing modes. Returns
    /// <c>false</c> (fall back to the estimator) when either box is missing from the snapshot.
    /// </summary>
    private bool TryGetSharedOffset(DomElement element, DomElement offsetParent, bool vertical, out double offset)
    {
        offset = 0;
        if (!UseSharedLayoutGeometry || !TryGetSharedLayoutGeometry(element, out var elementGeometry))
            return false;

        double elementEdge = vertical ? elementGeometry.BorderBox.Top : elementGeometry.BorderBox.Left;
        double parentEdge = 0;
        if (offsetParent != null)
        {
            if (!TryGetSharedLayoutGeometry(offsetParent, out var parentGeometry))
                return false;
            parentEdge = vertical ? parentGeometry.PaddingBox.Top : parentGeometry.PaddingBox.Left;
        }

        // The snapshot delta is in zoom-baked document space (the element's own edge and
        // the offset parent's padding edge are both baked). offsetTop/Left is reported in
        // the element's own unzoomed CSS pixels — excluding its own zoom — so divide the
        // delta by the element's cumulative used zoom (a no-op when unzoomed). This is the
        // zoom-chain-aware reconciliation: both edges are in the same baked space, so a
        // single division by the element's own zoom recovers the offset-parent-relative
        // value for the common, middle-ancestor-zoom, and own-zoom cases alike.
        var zoom = GetUsedZoomForElement(element);
        offset = zoom > 0.0001 ? (elementEdge - parentEdge) / zoom : (elementEdge - parentEdge);
        return true;
    }

    private DomElement? GetOffsetParentForDomElement(DomElement element)
    {
        if (ParentEl(element) == null ||
            ReferenceEquals(element, DocumentElement) ||
            string.Equals(element.TagName, "html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(element.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var props = GetComputedProps(element);
        if (string.Equals(props.GetValueOrDefault("position"), "fixed", StringComparison.OrdinalIgnoreCase))
            return null;

        var documentElement = GetOwningDocumentElement(element);
        var fallbackBody = FindBodyElement(documentElement);
        for (var current = ParentEl(element); current != null; current = ParentEl(current))
        {
            if (string.Equals(current.TagName, "body", StringComparison.OrdinalIgnoreCase))
                return current;

            if (ReferenceEquals(current, documentElement))
                return fallbackBody ?? documentElement;

            var currentProps = GetComputedProps(current);
            var currentPosition = currentProps.GetValueOrDefault("position");
            if (!string.IsNullOrWhiteSpace(currentPosition) &&
                !string.Equals(currentPosition, "static", StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }
        }

        return fallbackBody ?? documentElement;
    }

    private DomElement? GetScrollParentForDomElement(DomElement element)
    {
        var documentElement = GetOwningDocumentElement(element);
        if (!HasAssociatedLayoutBox(element))
            return null;

        if (ReferenceEquals(element, documentElement) || ReferenceEquals(element, GetDocumentScrollingElement(documentElement)))
            return null;

        if (IsViewportBodyElement(element, documentElement))
            return GetDocumentScrollingElement(documentElement);

        var props = GetComputedProps(element);
        var position = props.GetValueOrDefault("position")?.Trim().ToLowerInvariant();
        if (string.Equals(position, "fixed", StringComparison.OrdinalIgnoreCase))
        {
            var fixedContainingBlock = FindFixedPositionContainingBlock(element, documentElement);
            if (fixedContainingBlock == null)
                return null;

            return FindNearestScrollParent(fixedContainingBlock, documentElement);
        }

        if (string.Equals(position, "absolute", StringComparison.OrdinalIgnoreCase))
        {
            var containingBlock = GetOffsetParentForDomElement(element);
            if (containingBlock == null)
                return GetDocumentScrollingElement(documentElement);

            return FindNearestScrollParent(containingBlock, documentElement);
        }

        return FindNearestScrollParent(ParentEl(element), documentElement);
    }

    private DomElement GetDocumentScrollingElement(DomElement documentElement) => documentElement;

    private DomElement FindNearestScrollParent(DomElement? start, DomElement documentElement)
    {
        for (var current = start; current != null; current = ParentEl(current))
        {
            if (ReferenceEquals(current, documentElement))
                return GetDocumentScrollingElement(documentElement);

            if (IsViewportBodyElement(current, documentElement) || !HasAssociatedLayoutBox(current))
                continue;

            if (HasOverflowClipping(GetComputedProps(current)))
                return current;
        }

        return GetDocumentScrollingElement(documentElement);
    }

    private DomElement? FindFixedPositionContainingBlock(DomElement element, DomElement documentElement)
    {
        for (var current = ParentEl(element); current != null; current = ParentEl(current))
        {
            if (ReferenceEquals(current, documentElement))
                break;

            if (EstablishesFixedPositionContainingBlock(current))
                return current;
        }

        return null;
    }

    private bool EstablishesFixedPositionContainingBlock(DomElement element)
    {
        var props = GetComputedProps(element);
        var transform = props.GetValueOrDefault("transform");
        if (!string.IsNullOrWhiteSpace(transform) &&
            !string.Equals(transform.Trim(), "none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var contain = props.GetValueOrDefault("contain");
        if (string.IsNullOrWhiteSpace(contain))
            return false;

        var normalized = contain.Trim().ToLowerInvariant();
        return normalized.Contains("paint") ||
               normalized.Contains("layout") ||
               normalized.Contains("strict") ||
               normalized.Contains("content");
    }

    private bool HasAssociatedLayoutBox(DomElement element)
    {
        if (IsText(element))
            return false;

        if (element.TagName.StartsWith('#'))
            return false;

        var display = GetComputedProps(element).GetValueOrDefault("display")?.Trim().ToLowerInvariant();
        return !string.Equals(display, "none", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(display, "contents", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// RF-BRIDGE-1b: computes an element's scrollable overflow extent
    /// (<c>scrollWidth</c>/<c>scrollHeight</c>) from the shared renderer-layout
    /// snapshot — the union of the element's own padding-box (client) extent and the
    /// end edges of its rendered descendants' border boxes, expressed in the element's
    /// padding-box coordinate space and including the element's end padding. This
    /// mirrors the coarse estimator's element-descendant semantics but reads real box
    /// geometry from the provider instead of re-deriving it. Returns <c>false</c> when
    /// the element has no shared box (detached / <c>display:none</c>) so the caller
    /// falls back to the estimator.
    ///
    /// <para>The snapshot is in zoom-baked space, so the raw extent is divided by the
    /// element's own used zoom (<see cref="UnzoomSharedExtent"/>) to report the
    /// element's own unzoomed CSS pixels; a zoomed descendant keeps its larger baked
    /// extent, which correctly counts as scrollable overflow.</para>
    /// </summary>
    private bool TryGetSharedScrollExtent(DomElement element, bool vertical, out double extent)
    {
        extent = 0;
        if (!TryGetSharedLayoutGeometry(element, out var box))
            return false;

        var paddingBox = box.PaddingBox;
        var contentBox = box.ContentBox;
        var origin = vertical ? paddingBox.Top : paddingBox.Left;
        // The scrolling area is at least the padding-box (client) extent, and the
        // scrollable overflow region includes the container's end padding.
        var max = vertical ? (double)paddingBox.Height : paddingBox.Width;
        var endPadding = vertical
            ? Math.Max(0, paddingBox.Bottom - contentBox.Bottom)
            : Math.Max(0, paddingBox.Right - contentBox.Right);

        foreach (var descendant in EnumerateRenderedDescendants(element))
        {
            if (!TryGetSharedLayoutGeometry(descendant, out var childBox))
                continue;
            var childEnd = vertical ? childBox.BorderBox.Bottom : childBox.BorderBox.Right;
            max = Math.Max(max, (childEnd - origin) + endPadding);
        }

        // The snapshot is in zoom-baked space; report the extent in the element's own
        // unzoomed CSS pixels. Only the element's own used zoom is divided out, so a
        // zoomed descendant's larger baked extent still counts as overflow.
        extent = UnzoomSharedExtent(max, element);
        return true;
    }

    /// <summary>
    /// RF-BRIDGE-1b: converts a shared-snapshot extent (in the renderer's zoom-baked
    /// document space) into <paramref name="element"/>'s own unzoomed CSS pixels by
    /// dividing out its cumulative used zoom. A no-op for unzoomed elements
    /// (used zoom == 1). Reads used zoom from the live document, which the geometry
    /// snapshot pass restores after building (see the render-doc/live-doc separation),
    /// so the true zoom is readable here.
    /// </summary>
    private double UnzoomSharedExtent(double extent, DomElement element)
    {
        var zoom = GetUsedZoomForElement(element);
        return zoom > 0.0001 ? extent / zoom : extent;
    }

    /// <summary>
    /// RF-BRIDGE-1b: <paramref name="element"/>'s border-box extent along the axis from
    /// the shared renderer layout, in the element's own unzoomed CSS pixels. Returns
    /// <c>false</c> (caller falls back to the estimator) when the shared path is off or the
    /// element has no box.
    /// </summary>
    private bool TrySharedBorderBoxExtent(DomElement element, bool vertical, out double extent)
    {
        extent = 0;
        if (!UseSharedLayoutGeometry || !TryGetSharedLayoutGeometry(element, out var box))
            return false;
        extent = UnzoomSharedExtent(vertical ? box.BorderBox.Height : box.BorderBox.Width, element);
        return true;
    }

    /// <summary>
    /// RF-BRIDGE-1b: <paramref name="element"/>'s content-box extent along the axis from
    /// the shared renderer layout (the renderer's <c>ClientRectangle</c> is the content
    /// box), in the element's own unzoomed CSS pixels. Returns <c>false</c> (caller falls
    /// back to the estimator) when the shared path is off or the element has no box.
    /// </summary>
    private bool TrySharedContentBoxExtent(DomElement element, bool vertical, out double extent)
    {
        extent = 0;
        if (!UseSharedLayoutGeometry || !TryGetSharedLayoutGeometry(element, out var box))
            return false;
        extent = UnzoomSharedExtent(vertical ? box.ContentBox.Height : box.ContentBox.Width, element);
        return true;
    }

    private double GetScrollWidthForDomElement(DomElement element, bool isRoot) =>
        WithLayoutGeometryCache(() =>
        {
            if (TryGetSelectListBoxScrollExtent(element, verticalAxis: false, out var selectScrollWidth))
                return selectScrollWidth;

            // RF-BRIDGE-1b: scroll overflow comes from the shared renderer layout — the
            // union of the element's own box and its rendered descendants, including the
            // root/viewport scrolling area, computed by TryGetSharedScrollExtent. An
            // element with no shared box (detached / display:none) reports zero.
            if (TryGetSharedScrollExtent(element, vertical: false, out var sharedScrollWidth))
                return sharedScrollWidth;

            return 0;
        });

    private double GetScrollHeightForDomElement(DomElement element, bool isRoot) =>
        WithLayoutGeometryCache(() =>
        {
            if (TryGetSelectListBoxScrollExtent(element, verticalAxis: true, out var selectScrollHeight))
                return selectScrollHeight;

            // RF-BRIDGE-1b: scroll overflow comes from the shared renderer layout (see
            // GetScrollWidth). An element with no shared box reports zero.
            if (TryGetSharedScrollExtent(element, vertical: true, out var sharedScrollHeight))
                return sharedScrollHeight;

            return 0;
        });

    private (double Left, double Top, double Width, double Height) GetBoundingClientRectForDomElement(DomElement element, bool isRoot) =>
        WithLayoutGeometryCache(() =>
        {
            if (isRoot)
                return (0, 0, GetViewportReferenceLength(element, vertical: false), GetViewportReferenceLength(element, vertical: true));

            return ComputeRenderedRect(element);
        });

    private (double Left, double Top, double Width, double Height) ComputeRenderedRect(DomElement element)
    {
        var (Left, Top, Width, Height) = ComputeUnzoomedLayoutRect(element);
        var zoom = GetUsedZoomForElement(element);
        var transformScale = GetTransformScale(element);
        return (Left, Top, Width * zoom * transformScale, Height * zoom * transformScale);
    }

    private (double Left, double Top, double Width, double Height) ComputeUnzoomedLayoutRect(DomElement element)
    {
        // A live anchor resolution (position-area grid placement, an anchor() physical inset,
        // or anchor-size() sizing) wins over the shared renderer snapshot — the non-native
        // renderer lays such a box out at 0×0 / the static origin before the render bake runs, so
        // getBoundingClientRect would disagree with the (already-corrected) offset getters for a box
        // read during script (css-anchor-position position-area-anchor-partially-outside and the
        // anchor()/anchor-size() tests read geometry live). POSITION: when the box is anchor-*placed*
        // (position-area or anchor() inset) it is composed from the standard offsetParent invariant
        // (border-box edge = offsetParent border-box origin + its border + the element's offset),
        // reusing the offset getters; an only-anchor-*sized* box keeps the snapshot position (it is
        // placed normally). SIZE: the position-area resolution's used box, or the snapshot border box
        // with each anchor-size() axis overridden by its resolved dimension. Zoom/transform stay
        // approximate on this live path, as elsewhere.
        var areaResolution = ResolvePositionAreaForElement(element);
        (double? left, double? top, double? width, double? height)? insetResolution = null;
        (double? width, double? height)? sizeResolution = null;
        (double? left, double? top, double? width, double? height)? tryResolution = null;
        if (areaResolution is null)
        {
            tryResolution = ResolvePositionTryForElement(element);
            insetResolution = ResolveAnchorInsetForElement(element);
            sizeResolution = ResolveAnchorSizeForElement(element);
        }
        bool anchorPlaced = areaResolution is not null
            || (tryResolution is { } tr && (tr.left is not null || tr.top is not null))
            || (insetResolution is { } ins && (ins.left is not null || ins.top is not null));
        bool anchorSized = (sizeResolution is { } sz && (sz.width is not null || sz.height is not null))
            || (insetResolution is { } isz && (isz.width is not null || isz.height is not null))
            || (tryResolution is { } tsz && (tsz.width is not null || tsz.height is not null));
        if (anchorPlaced || anchorSized)
        {
            bool haveSnapshot = TryGetSharedLayoutGeometry(element, out var snapBox);
            var z = GetUsedZoomForElement(element);
            var iz = z > 0.0001 ? 1.0 / z : 1.0;

            double width, height;
            if (areaResolution is { } ar)
            {
                width = ar.width;
                height = ar.height;
            }
            else
            {
                width = haveSnapshot ? snapBox.BorderBox.Width * iz : 0;
                height = haveSnapshot ? snapBox.BorderBox.Height * iz : 0;
                // anchor-size(), opposing-inset auto, then position-try fallback sizing override the snapshot.
                if (sizeResolution?.width is { } aw) width = aw;
                if (sizeResolution?.height is { } ah) height = ah;
                if (insetResolution?.width is { } iw) width = iw;
                if (insetResolution?.height is { } ih) height = ih;
                if (tryResolution?.width is { } tw) width = tw;
                if (tryResolution?.height is { } th) height = th;
            }

            double left, top;
            if (anchorPlaced)
            {
                left = GetOffsetLeftForDomElement(element);
                top = GetOffsetTopForDomElement(element);
                if (GetOffsetParentForDomElement(element) is { } offsetParent)
                {
                    var parentRect = ComputeUnzoomedLayoutRect(offsetParent);
                    left += parentRect.Left + GetClientLeftForDomElement(offsetParent);
                    top += parentRect.Top + GetClientTopForDomElement(offsetParent);
                }
            }
            else
            {
                // Only anchor-*sized* — the box is placed normally, so its snapshot origin is right.
                left = haveSnapshot ? snapBox.BorderBox.Left : 0;
                top = haveSnapshot ? snapBox.BorderBox.Top : 0;
            }

            return (left, top, width, height);
        }

        // RF-BRIDGE-1b: the element's rect comes from the renderer's real layout (border
        // box, document coords), which powers getBoundingClientRect and offset top/left.
        // The snapshot is in zoom-baked space and ComputeRenderedRect re-applies zoom to
        // the SIZE, so the size is divided back to the element's own unzoomed CSS pixels
        // here; the position stays in the rendered document coordinate space, which is
        // what getBoundingClientRect wants (a no-op for unzoomed elements). An element with
        // no shared box (detached / display:none) reports a zero rect.
        if (TryGetSharedLayoutGeometry(element, out var sharedGeometry))
        {
            var zoom = GetUsedZoomForElement(element);
            var inverseZoom = zoom > 0.0001 ? 1.0 / zoom : 1.0;
            return (
                (double)sharedGeometry.BorderBox.Left,
                (double)sharedGeometry.BorderBox.Top,
                sharedGeometry.BorderBox.Width * inverseZoom,
                sharedGeometry.BorderBox.Height * inverseZoom);
        }

        return (0, 0, 0, 0);
    }

}
