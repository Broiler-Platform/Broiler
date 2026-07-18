using Broiler.JavaScript.BuiltIns.Null;
using System.Text;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.Dom;
using System.Globalization;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    // form.length and form.action moved to the Phase 3 FormBinding feature module
    // (Broiler.HtmlBridge.Dom.Features).

    private JSValue JsElementInterfacesSetData051Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        SetAttr(element, "data", a.Length > 0 ? a[0].ToString() : string.Empty);
        // Invalidate cached sub-document when data changes
        bridge.InvalidateCachedSubDocument(element);
        return JSUndefined.Value;
    }

    private JSValue JsElementInterfacesGetContentDocument054Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        var dataUrl = TryGetAttribute(element, "data", out var d) ? d : string.Empty;
        if (IsCrossOrigin(dataUrl, bridge._pageUrl))
            return JSNull.Value;
        // Check if the resource actually loaded successfully
        if (bridge.IsObjectLoadFailed(element))
            return JSNull.Value;
        return bridge.GetOrCreateSubDocument(element);
    }


    private JSValue JsElementInterfacesGetSVGDocument055Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        var dataUrl = TryGetAttribute(element, "data", out var d) ? d : string.Empty;
        if (IsCrossOrigin(dataUrl, bridge._pageUrl))
            return JSNull.Value;
        return bridge.GetOrCreateSubDocument(element);
    }

    private JSValue JsElementInterfacesCallback062Core(DomBridge? bridge, string? dimName, DomElement element, in Arguments _)
    {
        // First check computed style for this element
        var computed = bridge.BuildComputedStyleObject(element);
        var csVal = computed[(KeyString)dimName];
        if (csVal != null && !csVal.IsNull && !csVal.IsUndefined)
        {
            var cssStr = csVal.ToString();
            if (!string.IsNullOrEmpty(cssStr))
            {
                var px = ParseCssLengthToPixels(cssStr);
                if (!double.IsNaN(px))
                    return new JSNumber(px);
            }
        }

        // Fallback: HTML attribute
        if (TryGetAttribute(element, dimName, out var attrVal) && double.TryParse(attrVal, out var attrNum))
            return new JSNumber(attrNum);
        return new JSNumber(0);
    }

    private JSValue JsElementInterfacesGetScrollTop072Core(DomBridge? bridgeForOffset, DomElement element, in Arguments _)
    {
        if (bridgeForOffset.GetElementScrollOffset(element, vertical: true) is double sv)
            return new JSNumber(sv);
        return new JSNumber(0);
    }


    private JSValue JsElementInterfacesSetScrollTop073Core(DomBridge? bridgeForOffset, DomElement element, in Arguments a)
    {
        if (a.Length > 0)
            bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, top: a[0].DoubleValue);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetScrollLeft074Core(DomBridge? bridgeForOffset, DomElement element, in Arguments _)
    {
        if (bridgeForOffset.GetElementScrollOffset(element, vertical: false) is double sv)
            return new JSNumber(sv);
        return new JSNumber(0);
    }


    private JSValue JsElementInterfacesSetScrollLeft075Core(DomBridge? bridgeForOffset, DomElement element, in Arguments a)
    {
        if (a.Length > 0)
            bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left: a[0].DoubleValue);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetOffsetParent078Core(DomBridge? bridgeForOffset, DomElement? elForOffset, in Arguments _)
    {
        var offsetParent = bridgeForOffset.GetOffsetParentForDomElement(elForOffset);
        return offsetParent != null ? bridgeForOffset.ToJSObject(offsetParent) : JSNull.Value;
    }


    private JSValue JsElementInterfacesGetBoundingClientRect079Core(DomBridge? bridgeForOffset, DomElement? elForOffset, bool isViewportElement, in Arguments _)
    {
        var (Left, Top, Width, Height) = bridgeForOffset.GetBoundingClientRectForDomElement(elForOffset, isViewportElement);
        var rect = new JSObject();

        rect.FastAddValue((KeyString)"x", new JSNumber(Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"y", new JSNumber(Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"top", new JSNumber(Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"left", new JSNumber(Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"right", new JSNumber(Left + Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"bottom", new JSNumber(Top + Height), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"width", new JSNumber(Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"height", new JSNumber(Height), JSPropertyAttributes.EnumerableConfigurableValue);
        
        return rect;
    }


    private JSValue JsElementInterfacesGetClientRects080Core(DomBridge? bridgeForOffset, DomElement? elForOffset, bool isViewportElement, in Arguments a2)
    {
        var (Left, Top, Width, Height) = bridgeForOffset.GetBoundingClientRectForDomElement(elForOffset, isViewportElement);
        var rect = new JSObject();

        rect.FastAddValue((KeyString)"x", new JSNumber(Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"y", new JSNumber(Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"top", new JSNumber(Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"left", new JSNumber(Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"right", new JSNumber(Left + Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"bottom", new JSNumber(Top + Height), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"width", new JSNumber(Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"height", new JSNumber(Height), JSPropertyAttributes.EnumerableConfigurableValue);
        
        return Width > 0 || Height > 0 || isViewportElement ? new JSArray([rect]) : new JSArray();
    }


    private JSValue JsElementInterfacesScrollIntoView081Core(DomBridge? bridgeForOffset, DomElement? elForOffset, in Arguments a)
    {
        var (Block, Inline, Behavior) = bridgeForOffset.GetScrollIntoViewOptions(a);
        bridgeForOffset.ScrollElementIntoView(elForOffset, Block, Inline, Behavior);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesScroll082Core(DomBridge? bridgeForOffset, DomElement element, in Arguments a)
    {
        var (left, top, behavior) = bridgeForOffset.GetScrollArguments(a);
        bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left, top, clamp: false, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesScrollTo083Core(DomBridge? bridgeForOffset, DomElement element, in Arguments a)
    {
        var (left, top, behavior) = bridgeForOffset.GetScrollArguments(a);
        bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left, top, clamp: false, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesScrollBy084Core(DomBridge? bridgeForOffset, DomElement element, in Arguments a)
    {
        var (left, top, behavior) = bridgeForOffset.GetScrollArguments(a);
        bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left, top, relative: true, clamp: false, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesScrollParent085Core(DomBridge? bridgeForOffset, DomElement? elForOffset, in Arguments _)
    {
        var scrollParent = bridgeForOffset.GetScrollParentForDomElement(elForOffset);
        return scrollParent != null ? bridgeForOffset.ToJSObject(scrollParent) : JSNull.Value;
    }


}
