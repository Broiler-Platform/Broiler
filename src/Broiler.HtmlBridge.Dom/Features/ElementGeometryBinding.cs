using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The HTML/CSSOM element box-model and scrolling interface, co-located as an HtmlBridge feature module
/// (Phase 3): the box metrics (<c>clientTop</c>/<c>clientLeft</c>/<c>clientWidth</c>/<c>clientHeight</c>,
/// <c>offsetWidth</c>/<c>offsetHeight</c>, <c>scrollWidth</c>/<c>scrollHeight</c>, <c>offsetTop</c>/
/// <c>offsetLeft</c>, <c>offsetParent</c>, <c>getBoundingClientRect</c>/<c>getClientRects</c>) and the
/// imperative scrolling API (<c>scrollTop</c>/<c>scrollLeft</c> get/set, <c>scroll</c>/<c>scrollTo</c>/
/// <c>scrollBy</c>, <c>scrollIntoView</c>, <c>scrollParent</c>). Every value here reads the live layout, so
/// the module depends on the bridge through the deliberately wide <see cref="IElementGeometryHost"/>
/// contract (the Phase 3 "wide-explicit-host" template) rather than a one-member seam — the point is that
/// the exact geometry surface is now named instead of the callbacks reaching into arbitrary bridge
/// internals. Was the bridge's box-model block in <c>DomBridge/ElementInterfaces.cs</c> and the
/// <c>JsElementInterfacesGetScrollTop072Core</c>..<c>ScrollParent085Core</c> callbacks.
/// </summary>
internal static class ElementGeometryBinding
{
    /// <summary>
    /// Installs the box-model metrics and scrolling members on <paramref name="obj"/> for
    /// <paramref name="element"/>, reading all geometry through <paramref name="host"/>.
    /// </summary>
    public static void Install(IElementGeometryHost host, JSObject obj, DomElement element)
    {
        // -- TODO-G4 / TODO-G19: Box model properties for all elements --
        // clientWidth/clientHeight, offsetWidth/offsetHeight, scrollWidth/scrollHeight,
        // scrollTop/scrollLeft, and getBoundingClientRect()
        var isViewportElement = host.IsViewportElementForMetrics(element);

        obj.FastAddProperty((KeyString)"clientTop",
            new JSFunction((in _) => new JSNumber(host.GetClientTopForDomElement(element)), "get clientTop"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"clientLeft",
            new JSFunction((in _) => new JSNumber(host.GetClientLeftForDomElement(element)), "get clientLeft"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"clientWidth",
            new JSFunction((in _) => new JSNumber(host.GetClientWidthForDomElement(element, isViewportElement)), "get clientWidth"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"clientHeight",
            new JSFunction((in _) => new JSNumber(host.GetClientHeightForDomElement(element, isViewportElement)), "get clientHeight"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"offsetWidth",
            new JSFunction((in _) => new JSNumber(host.GetOffsetWidthForDomElement(element, isViewportElement)), "get offsetWidth"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"offsetHeight",
            new JSFunction((in _) => new JSNumber(host.GetOffsetHeightForDomElement(element, isViewportElement)), "get offsetHeight"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"scrollWidth",
            new JSFunction((in _) => new JSNumber(host.GetScrollWidthForDomElement(element, isViewportElement)), "get scrollWidth"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"scrollHeight",
            new JSFunction((in _) => new JSNumber(host.GetScrollHeightForDomElement(element, isViewportElement)), "get scrollHeight"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"scrollTop",
            new JSFunction((in _) => GetScrollTop(host, element), "get scrollTop"),
            new JSFunction((in a) => SetScrollTop(host, element, in a), "set scrollTop"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"scrollLeft",
            new JSFunction((in _) => GetScrollLeft(host, element), "get scrollLeft"),
            new JSFunction((in a) => SetScrollLeft(host, element, in a), "set scrollLeft"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"offsetTop",
            new JSFunction((in _) => new JSNumber(host.GetOffsetTopForDomElement(element)), "get offsetTop"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"offsetLeft",
            new JSFunction((in _) => new JSNumber(host.GetOffsetLeftForDomElement(element)), "get offsetLeft"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"offsetParent",
            new JSFunction((in _) => GetOffsetParent(host, element), "get offsetParent"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // getBoundingClientRect() — returns DOMRect-like object
        obj.FastAddValue((KeyString)"getBoundingClientRect",
            new JSFunction((in _) => GetBoundingClientRect(host, element, isViewportElement), "getBoundingClientRect", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getClientRects() — returns array with one DOMRect for root elements
        obj.FastAddValue((KeyString)"getClientRects",
            new JSFunction((in a2) => GetClientRects(host, element, isViewportElement), "getClientRects", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"scrollIntoView",
            new JSFunction((in a) => ScrollIntoView(host, element, in a), "scrollIntoView", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"scroll",
            new JSFunction((in a) => Scroll(host, element, in a), "scroll", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"scrollTo",
            new JSFunction((in a) => Scroll(host, element, in a), "scrollTo", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"scrollBy",
            new JSFunction((in a) => ScrollBy(host, element, in a), "scrollBy", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"scrollParent",
            new JSFunction((in _) => GetScrollParent(host, element), "scrollParent", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
    }

    private static JSValue GetScrollTop(IElementGeometryHost host, DomElement element)
    {
        if (host.GetElementScrollOffset(element, vertical: true) is double sv)
            return new JSNumber(sv);
        return new JSNumber(0);
    }

    private static JSValue SetScrollTop(IElementGeometryHost host, DomElement element, in Arguments a)
    {
        if (a.Length > 0)
            host.SetElementScrollOffsetsWithBehavior(element, top: a[0].DoubleValue);
        return JSUndefined.Value;
    }

    private static JSValue GetScrollLeft(IElementGeometryHost host, DomElement element)
    {
        if (host.GetElementScrollOffset(element, vertical: false) is double sv)
            return new JSNumber(sv);
        return new JSNumber(0);
    }

    private static JSValue SetScrollLeft(IElementGeometryHost host, DomElement element, in Arguments a)
    {
        if (a.Length > 0)
            host.SetElementScrollOffsetsWithBehavior(element, left: a[0].DoubleValue);
        return JSUndefined.Value;
    }

    private static JSValue GetOffsetParent(IElementGeometryHost host, DomElement element)
    {
        var offsetParent = host.GetOffsetParentForDomElement(element);
        return offsetParent != null ? host.ToJSObject(offsetParent) : JSNull.Value;
    }

    private static JSValue GetBoundingClientRect(IElementGeometryHost host, DomElement element, bool isViewportElement)
    {
        var (Left, Top, Width, Height) = host.GetBoundingClientRectForDomElement(element, isViewportElement);
        return BuildRect(Left, Top, Width, Height);
    }

    private static JSValue GetClientRects(IElementGeometryHost host, DomElement element, bool isViewportElement)
    {
        var (Left, Top, Width, Height) = host.GetBoundingClientRectForDomElement(element, isViewportElement);
        var rect = BuildRect(Left, Top, Width, Height);
        return Width > 0 || Height > 0 || isViewportElement ? new JSArray([rect]) : new JSArray();
    }

    // Builds the DOMRect-like object (x/y/top/left/right/bottom/width/height) shared by
    // getBoundingClientRect() and getClientRects().
    private static JSObject BuildRect(double left, double top, double width, double height)
    {
        var rect = new JSObject();
        rect.FastAddValue((KeyString)"x", new JSNumber(left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"y", new JSNumber(top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"top", new JSNumber(top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"left", new JSNumber(left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"right", new JSNumber(left + width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"bottom", new JSNumber(top + height), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"width", new JSNumber(width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"height", new JSNumber(height), JSPropertyAttributes.EnumerableConfigurableValue);
        return rect;
    }

    private static JSValue ScrollIntoView(IElementGeometryHost host, DomElement element, in Arguments a)
    {
        var (Block, Inline, Behavior) = host.GetScrollIntoViewOptions(a);
        host.ScrollElementIntoView(element, Block, Inline, Behavior);
        return JSUndefined.Value;
    }

    // scroll() / scrollTo() — absolute scroll to (left, top).
    private static JSValue Scroll(IElementGeometryHost host, DomElement element, in Arguments a)
    {
        var (left, top, behavior) = host.GetScrollArguments(a);
        host.SetElementScrollOffsetsWithBehavior(element, left, top, clamp: false, behavior: behavior);
        return JSUndefined.Value;
    }

    // scrollBy() — relative scroll.
    private static JSValue ScrollBy(IElementGeometryHost host, DomElement element, in Arguments a)
    {
        var (left, top, behavior) = host.GetScrollArguments(a);
        host.SetElementScrollOffsetsWithBehavior(element, left, top, relative: true, clamp: false, behavior: behavior);
        return JSUndefined.Value;
    }

    private static JSValue GetScrollParent(IElementGeometryHost host, DomElement element)
    {
        var scrollParent = host.GetScrollParentForDomElement(element);
        return scrollParent != null ? host.ToJSObject(scrollParent) : JSNull.Value;
    }
}
