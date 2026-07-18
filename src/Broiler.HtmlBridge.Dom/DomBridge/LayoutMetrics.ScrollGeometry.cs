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

/// <summary>
/// Sibling partial peeled out of <c>LayoutMetrics.cs</c> (Phase 3 ratchet, 2026-07-17) to keep it
/// under the 750-line guard: scroll-container and fixed-position ancestor resolution, rendered-descendant
/// enumeration, and the <c>scrollIntoView</c> physical-offset / scroll-coordinate / scroll-inset geometry
/// conversions. Pure partial-class relocation — no signature, accessibility, or logic change.
/// </summary>
public sealed partial class DomBridge
{
    private IEnumerable<DomElement> EnumerateRenderedDescendants(DomElement element)
    {
        foreach (var child in EnumerateRenderedChildren(element))
        {
            yield return child;

            foreach (var descendant in EnumerateRenderedDescendants(child))
                yield return descendant;
        }
    }

    private IEnumerable<DomElement> EnumerateRenderedChildren(DomElement element)
    {
        if (string.Equals(element.TagName, "slot", StringComparison.OrdinalIgnoreCase))
        {
            var host = GetSlotHost(element);
            if (host == null)
                yield break;

            foreach (var child in ChildElements(host))
            {
                if (!IsText(child) && SlotAcceptsNode(element, child))
                    yield return child;
            }

            yield break;
        }

        foreach (var child in ChildElements(element))
        {
            if (!IsText(child))
                yield return child;
        }
    }

    private DomElement? FindScrollContainer(DomElement element)
    {
        var documentElement = GetOwningDocumentElement(element);
        for (var current = GetScrollTraversalParent(element); current != null; current = GetScrollTraversalParent(current))
        {
            if (ReferenceEquals(current, documentElement))
                return documentElement;

            if (IsViewportBodyElement(current, documentElement))
                continue;

            var props = GetComputedProps(current);
            if (HasOverflowClipping(props))
                return current;
        }

        return documentElement;
    }

    private bool HasFixedPositionAncestorBefore(DomElement element, DomElement ancestor)
    {
        for (var current = GetScrollTraversalParent(element);
             current != null && !ReferenceEquals(current, ancestor);
             current = GetScrollTraversalParent(current))
        {
            var props = GetComputedProps(current);
            if (string.Equals(props.GetValueOrDefault("position"), "fixed", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsDocumentElement(DomElement element) =>
        string.Equals(element.TagName, "html", StringComparison.OrdinalIgnoreCase);

    private bool IsViewportElementForMetrics(DomElement element)
    {
        var documentElement = GetOwningDocumentElement(element);
        return IsDocumentElement(element) || IsViewportBodyElement(element, documentElement);
    }

    private static bool IsViewportBodyElement(DomElement element, DomElement documentElement) =>
        string.Equals(element.TagName, "body", StringComparison.OrdinalIgnoreCase) &&
        ReferenceEquals(ParentEl(element), documentElement);

    private DomElement GetOwningDocumentElement(DomElement element)
    {
        for (var current = element; current != null; current = ParentEl(current)!)
        {
            if (string.Equals(current.TagName, "html", StringComparison.OrdinalIgnoreCase))
                return current;
        }

        return DocumentElement;
    }

    private bool HasFixedPositionInDocument(DomElement element, DomElement documentElement)
    {
        if (IsFixedPositionElement(element))
            return true;

        return HasFixedPositionAncestorBefore(element, documentElement);
    }

    private bool IsFixedPositionElement(DomElement element)
    {
        var props = GetComputedProps(element);
        return string.Equals(props.GetValueOrDefault("position"), "fixed", StringComparison.OrdinalIgnoreCase);
    }

    private DomElement? GetOuterFrameElement(DomElement documentElement)
    {
        // A sub-document's documentElement (<html>) now hangs off its canonical DomDocument
        // (the severed content document, P4.4b); recover the owning frame via the reverse map
        // (was ParentEl(ParentEl(<html>)) through the #subdoc-root element).
        return GetFrameForContentDocument(documentElement?.ParentNode);
    }

    private DomElement? GetOuterScrollContinuationElement(DomElement scrollContainer)
    {
        if (IsDocumentElement(scrollContainer))
            return GetOuterFrameElement(scrollContainer);

        return scrollContainer;
    }

    /// <summary>
    /// RF-BRIDGE-1b: the scroll-aware offset of <paramref name="element"/> within
    /// <paramref name="ancestor"/> from the shared snapshot. The snapshot is a *natural*
    /// (unscrolled) layout, so the element-border-to-ancestor-padding delta it yields is
    /// the pre-scroll offset; the JS-set scroll offset of each intermediate scroll
    /// container strictly between <paramref name="element"/> and <paramref name="ancestor"/>
    /// is then subtracted (the ancestor's own scroll is left to the caller). Returns
    /// <c>false</c> — so the caller reports zero — when either box is missing, or zoom is in
    /// play (a zoomed cross-ancestor delta is not reconciled against the baked snapshot).
    /// </summary>
    private bool TrySharedOffsetWithinAncestor(DomElement element, DomElement ancestor, bool vertical, out double offset,
        bool viewportAnchored = false)
    {
        offset = 0;
        if (!UseSharedLayoutGeometry)
            return false;
        // (RF-BRIDGE-1b Track 3.2) The former cross-frame gate is gone: the shared
        // snapshot now composes each subframe's geometry into the main coordinate frame
        // (CssBox.LayoutNestedBrowsingContexts) *and* a position:fixed / root-anchored
        // abspos element inside a subframe resolves against the frame's own sub-viewport
        // (CssBox.IsNestedViewportRoot / FixedPositioningViewport), so a cross-frame
        // element's composed box is correct. When the subframe box is absent from the
        // snapshot (e.g. a cross-origin or non-materialised frame), the
        // TryGetSharedLayoutGeometry lookups below return false and the caller still
        // falls back to the estimator's frame-aware walk.
        // (RF-BRIDGE-1b Track 3.1) The former abspos-in-inline-CB bypass is gone: the
        // layout engine now places an absolutely/fixed-positioned element whose
        // containing block is an inline box at its inset position, so its shared box is
        // correct and the estimator fallback is no longer needed here.
        if (!TryGetSharedLayoutGeometry(element, out var elementBox) ||
            !TryGetSharedLayoutGeometry(ancestor, out var ancestorBox))
            return false;

        // Both edges are in the snapshot's zoom-baked document space; the offset is expressed
        // in the ANCESTOR's coordinate frame, so divide the delta by the ancestor's cumulative
        // used zoom to recover unzoomed CSS pixels. (Dividing by the *element's* own zoom would
        // be wrong when only the element is zoomed: its own zoom scales its size/content, not
        // its flow position within the ancestor.) A no-op when the ancestor is unzoomed. The
        // former zoom gate that deferred zoomed elements to the estimator is gone, so the
        // estimator can be deleted. Intermediate scroll offsets are already in unzoomed CSS
        // pixels (JS scrollTo), so they subtract cleanly after this division.
        var offsetZoom = GetUsedZoomForElement(ancestor);
        var inverseOffsetZoom = offsetZoom > 0.0001 ? 1.0 / offsetZoom : 1.0;
        double natural = (vertical
            ? elementBox.BorderBox.Top - ancestorBox.PaddingBox.Top
            : elementBox.BorderBox.Left - ancestorBox.PaddingBox.Left) * inverseOffsetZoom;

        // (RF-BRIDGE-1b Track 3.3) For a viewport-anchored target — one being scrolled
        // into the *visual* viewport whose containing chain includes a position:fixed
        // ancestor F — only the scroll containers at or below F move the target: the
        // target rides F's own overflow scroll (and any scroller nested inside F), but
        // F itself is pinned to the viewport, so scroll containers *above* F do not
        // shift it. Stop subtracting once the walk climbs past F. (A normal, non-anchored
        // target subtracts every intermediate scroll container up to the ancestor.)
        var fixedAnchor = viewportAnchored ? FindNearestFixedAncestorOrSelf(element) : null;

        for (var current = element; ;)
        {
            var parent = GetScrollTraversalParent(current);
            if (parent == null || ReferenceEquals(parent, ancestor))
                break;
            if (fixedAnchor != null && !IsDomDescendantOrSelf(parent, fixedAnchor))
                break;
            natural -= GetElementScrollOffset(parent, vertical);
            current = parent;
        }

        offset = natural;
        return true;
    }

    /// <summary>
    /// Walks the DOM ancestry from <paramref name="element"/> (inclusive) and returns the
    /// nearest <c>position:fixed</c> box, or null if none. Used to bound the scroll
    /// subtraction for a viewport-anchored scrollIntoView target
    /// (<see cref="TrySharedOffsetWithinAncestor"/>).
    /// </summary>
    private DomElement? FindNearestFixedAncestorOrSelf(DomElement element)
    {
        for (var current = element; current != null; current = ParentEl(current))
        {
            if (IsFixedPositionElement(current))
                return current;
        }
        return null;
    }

    private static bool IsDomDescendantOrSelf(DomElement node, DomElement potentialAncestor)
    {
        for (var current = node; current != null; current = ParentEl(current))
        {
            if (ReferenceEquals(current, potentialAncestor))
                return true;
        }
        return false;
    }

    /// <summary>
    /// RF-BRIDGE-1b Track 3.3: offset of a viewport-anchored (fixed-subtree) target
    /// within <paramref name="ancestor"/> from the shared snapshot, subtracting only the
    /// scroll of containers at or below the target's nearest fixed ancestor
    /// (<see cref="TrySharedOffsetWithinAncestor"/> with <c>viewportAnchored</c>), else
    /// the estimator. Used by the visual-viewport fixed-element scrollIntoView sites so
    /// they no longer call the estimator directly.
    /// </summary>
    private double OffsetWithinAncestorForFixedPreferShared(DomElement element, DomElement ancestor, bool vertical) =>
        TrySharedOffsetWithinAncestor(element, ancestor, vertical, out var shared, viewportAnchored: true)
            ? shared
            : 0;

    /// <summary>
    /// RF-BRIDGE-1b: offset of <paramref name="element"/> within
    /// <paramref name="ancestor"/> from the scroll-aware shared snapshot
    /// (<see cref="TrySharedOffsetWithinAncestor"/>). With the coarse estimators deleted, a
    /// shared-unavailable element (cross-origin / non-materialised frame) reports 0 — real
    /// in-flow sticky/scrollIntoView targets are always present in the snapshot.
    /// </summary>
    private double OffsetWithinAncestorPreferShared(DomElement element, DomElement ancestor, bool vertical) =>
        TrySharedOffsetWithinAncestor(element, ancestor, vertical, out var shared)
            ? shared
            : 0;

    private double ResolveScrollIntoViewOffset(DomElement element, DomElement scrollContainer,
        bool vertical, string? alignment,
        double? viewportSizeOverride = null,
        double? currentScrollOverride = null,
        double? offsetOverride = null,
        bool coordinateSpaceIsPhysical = false)
    {
        var normalizedAlignment = NormalizeScrollIntoViewAlignment(alignment, "start");
        var offset = offsetOverride ?? OffsetWithinAncestorPreferShared(element, scrollContainer, vertical);
        var targetSize = vertical ? GetBorderBoxHeight(GetComputedProps(element), element) : GetBorderBoxWidth(GetComputedProps(element), element);
        var (marginStart, marginStartOwner) = ResolveScrollIntoViewInset(element, vertical ? "scroll-margin-top" : "scroll-margin-left");
        var (marginEnd, marginEndOwner) = ResolveScrollIntoViewInset(element, vertical ? "scroll-margin-bottom" : "scroll-margin-right");
        var (paddingStart, paddingStartOwner) = ResolveScrollIntoViewInset(scrollContainer, vertical ? "scroll-padding-top" : "scroll-padding-left");
        var (paddingEnd, paddingEndOwner) = ResolveScrollIntoViewInset(scrollContainer, vertical ? "scroll-padding-bottom" : "scroll-padding-right");
        marginStart = ConvertInsetToScrollContainerCoordinates(marginStart, marginStartOwner, scrollContainer);
        marginEnd = ConvertInsetToScrollContainerCoordinates(marginEnd, marginEndOwner, scrollContainer);
        paddingStart = ConvertInsetToScrollContainerCoordinates(paddingStart, paddingStartOwner, scrollContainer);
        paddingEnd = ConvertInsetToScrollContainerCoordinates(paddingEnd, paddingEndOwner, scrollContainer);
        var viewportSize = viewportSizeOverride ?? (vertical
            ? GetClientHeightForDomElement(scrollContainer, IsDocumentElement(scrollContainer))
            : GetClientWidthForDomElement(scrollContainer, IsDocumentElement(scrollContainer)));
        var currentScroll = currentScrollOverride ?? GetElementScrollOffset(scrollContainer, vertical);
        var physicalCurrentScroll = coordinateSpaceIsPhysical
            ? currentScroll
            : ConvertScrollCoordinateToPhysicalPosition(scrollContainer, vertical, currentScroll);

        var startTarget = offset - marginStart - paddingStart;
        var endTarget = offset + targetSize + marginEnd + paddingEnd - viewportSize;
        if (normalizedAlignment == "start")
            return ConvertPhysicalScrollPosition(scrollContainer, vertical, startTarget, coordinateSpaceIsPhysical);
        if (normalizedAlignment == "end")
            return ConvertPhysicalScrollPosition(scrollContainer, vertical, endTarget, coordinateSpaceIsPhysical);

        var alignmentViewportSize = Math.Max(0, viewportSize - paddingStart - paddingEnd);
        if (normalizedAlignment == "center")
        {
            var targetCenter = offset + ((targetSize + marginEnd - marginStart) / 2.0);
            return ConvertPhysicalScrollPosition(
                scrollContainer,
                vertical,
                targetCenter - paddingStart - (alignmentViewportSize / 2.0),
                coordinateSpaceIsPhysical);
        }

        var visibleStart = physicalCurrentScroll + paddingStart;
        var visibleEnd = physicalCurrentScroll + viewportSize - paddingEnd;
        var targetStart = offset - marginStart;
        var targetEnd = offset + targetSize + marginEnd;

        if (targetStart >= visibleStart && targetEnd <= visibleEnd)
            return currentScroll;

        if (normalizedAlignment == "start-if-needed")
            return ConvertPhysicalScrollPosition(scrollContainer, vertical, startTarget, coordinateSpaceIsPhysical);
        if (normalizedAlignment == "end-if-needed")
            return ConvertPhysicalScrollPosition(scrollContainer, vertical, endTarget, coordinateSpaceIsPhysical);

        if (targetSize + marginStart + marginEnd > alignmentViewportSize)
        {
            var chosenTarget = Math.Abs(startTarget - physicalCurrentScroll) <= Math.Abs(endTarget - physicalCurrentScroll)
                ? startTarget
                : endTarget;
            return ConvertPhysicalScrollPosition(scrollContainer, vertical, chosenTarget, coordinateSpaceIsPhysical);
        }

        if (targetStart < visibleStart)
            return ConvertPhysicalScrollPosition(scrollContainer, vertical, startTarget, coordinateSpaceIsPhysical);
        if (targetEnd > visibleEnd)
            return ConvertPhysicalScrollPosition(scrollContainer, vertical, endTarget, coordinateSpaceIsPhysical);

        return currentScroll;
    }

    private double ConvertPhysicalScrollPosition(DomElement scrollContainer,
        bool vertical, double physicalPosition, bool coordinateSpaceIsPhysical)
        => coordinateSpaceIsPhysical
            ? physicalPosition
            : ConvertPhysicalPositionToScrollCoordinate(scrollContainer, vertical, physicalPosition);

    private double ConvertScrollCoordinateToPhysicalPosition(DomElement scrollContainer, bool vertical, double coordinate)
    {
        if (!UsesNegativeScrollCoordinates(scrollContainer, vertical))
            return coordinate;

        return coordinate + GetPositiveScrollExtent(scrollContainer, vertical);
    }

    private double ConvertPhysicalPositionToScrollCoordinate(DomElement scrollContainer, bool vertical, double physicalPosition)
    {
        if (!UsesNegativeScrollCoordinates(scrollContainer, vertical))
            return physicalPosition;

        return physicalPosition - GetPositiveScrollExtent(scrollContainer, vertical);
    }

    private bool UsesNegativeScrollCoordinates(DomElement scrollContainer, bool vertical)
    {
        var (minLeft, _, minTop, _) = GetScrollBounds(scrollContainer);
        return vertical ? minTop < 0 : minLeft < 0;
    }

    private double GetPositiveScrollExtent(DomElement scrollContainer, bool vertical)
    {
        var (minLeft, maxLeft, minTop, maxTop) = GetScrollBounds(scrollContainer);
        return vertical
            ? (minTop < 0 ? maxTop - minTop : maxTop)
            : (minLeft < 0 ? maxLeft - minLeft : maxLeft);
    }

    private (double Value, DomElement Owner) ResolveScrollIntoViewInset(DomElement element, string propertyName)
    {
        var props = GetComputedProps(element);
        var value = props.GetValueOrDefault(propertyName);
        if (string.Equals(value, "inherit", StringComparison.OrdinalIgnoreCase) && ParentEl(element) != null)
            return ResolveScrollIntoViewInset(ParentEl(element), propertyName);

        return (ParseCssLengthToPixelsWithViewport(value, element), element);
    }

    private double ConvertInsetToScrollContainerCoordinates(double inset, DomElement insetOwner, DomElement scrollContainer)
    {
        if (!double.IsFinite(inset) || AreClose(inset, 0))
            return 0;

        var ownerZoom = GetUsedZoomForElement(insetOwner);
        var containerZoom = GetUsedZoomForElement(scrollContainer);
        if (!double.IsFinite(ownerZoom) || ownerZoom <= 0 ||
            !double.IsFinite(containerZoom) || containerZoom <= 0)
        {
            return inset;
        }

        return inset * (ownerZoom / containerZoom);
    }
}
