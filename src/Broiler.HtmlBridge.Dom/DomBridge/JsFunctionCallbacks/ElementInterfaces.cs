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

    private JSValue JsElementInterfacesGetLength025Core(DomElement element, in Arguments _)
    {
        var controls = CollectFormControls(element);
        return new JSNumber(controls.Count);
    }


    private JSValue JsElementInterfacesSetAction027Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "action", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesAdd037Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;

        if (a[0] is not JSObject optObj)
            return JSUndefined.Value;

        var optEl = FindDomElementByJSObject(optObj);
        if (optEl == null)
            return JSUndefined.Value;

        DomElement? refEl = null;
        if (a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined)
        {
            if (a[1] is JSObject refObj)
                refEl = FindDomElementByJSObject(refObj);
        }

        optEl.Remove();
        SetParent(optEl, element);
        if (refEl != null)
        {
            var idx = ChildIndexOf(element, refEl);
            if (idx >= 0)
                InsertChildAt(element, idx, optEl);
            else
                element.AppendChild(optEl);
        }
        else
            element.AppendChild(optEl);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetOptions039Core(DomElement element, in Arguments _)
    {
        var opts = new List<JSValue>();
        foreach (var c in ChildElements(element))
            if (string.Equals(c.TagName, "option", StringComparison.OrdinalIgnoreCase))
                opts.Add(ToJSObject(c));
        var arr = new JSArray(opts);
        JSValue JsElementInterfacesGetLength038(in Arguments __)
        {
            return new JSNumber(opts.Count);
        }

        arr.FastAddProperty((KeyString)"length", new JSFunction(JsElementInterfacesGetLength038, "get length"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        return arr;
    }


    private JSValue JsElementInterfacesSetSelectedIndex041Core(DomElement element, in Arguments a)
    {
        var index = a.Length == 0 ? -1 : (int)Math.Truncate(a[0].DoubleValue);
        SetSelectSelectedIndex(element, index);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetSize042Core(DomElement element, in Arguments _)
    {
        if (TryGetAttribute(element, "size", out var rawSize) && int.TryParse(rawSize, out var parsedSize) && parsedSize > 0)
        {
            return new JSNumber(parsedSize);
        }

        return new JSNumber(0);
    }


    private JSValue JsElementInterfacesSetSize043Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var size = (int)Math.Truncate(a[0].DoubleValue);
        if (size > 0)
            SetAttr(element, "size", size.ToString());
        else
            RemoveAttr(element, "size");
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetDefaultSelected045Core(DomElement element, in Arguments a)
    {
        GetElementRuntimeState(element).FormControl.DefaultSelected.Set(a.Length > 0 && a[0].BooleanValue);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetHtmlFor047Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "for", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetHttpEquiv049Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "http-equiv", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetData050Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        if (!TryGetAttribute(element, "data", out var d))
            return new JSString(string.Empty);
        // Resolve relative URI against base URL
        if (Uri.TryCreate(bridge._pageUrl, UriKind.Absolute, out var baseUri) && Uri.TryCreate(baseUri, d, out var resolved))
            return new JSString(resolved.AbsoluteUri);
        return new JSString(d);
    }


    private JSValue JsElementInterfacesSetData051Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        SetAttr(element, "data", a.Length > 0 ? a[0].ToString() : string.Empty);
        // Invalidate cached sub-document when data changes
        bridge.InvalidateCachedSubDocument(element);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetType053Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "type", a.Length > 0 ? a[0].ToString() : string.Empty);
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


    private JSValue JsElementInterfacesGetHref056Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        if (!TryGetAttribute(element, "href", out var h))
            return new JSString(string.Empty);
        if (Uri.TryCreate(bridge._pageUrl, UriKind.Absolute, out var baseUri) && Uri.TryCreate(baseUri, h, out var resolved))
            return new JSString(resolved.AbsoluteUri);
        return new JSString(h);
    }


    private JSValue JsElementInterfacesSetHref057Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "href", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesCallback059Core(string? captured, DomElement element, in Arguments a)
    {
        SetAttr(element, captured, a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetHref060Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        if (!TryGetAttribute(element, "href", out var h))
            return new JSString(string.Empty);
        if (Uri.TryCreate(bridge._pageUrl, UriKind.Absolute, out var baseUri) && Uri.TryCreate(baseUri, h, out var resolved))
            return new JSString(resolved.AbsoluteUri);
        return new JSString(h);
    }


    private JSValue JsElementInterfacesSetHref061Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "href", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
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


    private JSValue JsElementInterfacesCallback063Core(string? dimName, DomElement element, in Arguments a)
    {
        SetAttr(element, dimName, a.Length > 0 ? a[0].ToString() : "0");
        return JSUndefined.Value;
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


    private JSValue JsElementInterfacesCallback086Core(string? attrName, DomElement element, in Arguments _)
    {
        var animLength = new JSObject();
        var valueStr = TryGetAttribute(element, attrName, out var v) ? v : "0";
        double.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var numVal);
        var baseVal = CreateSvgLengthValue(numVal);
        var animVal = CreateSvgLengthValue(numVal);
        animLength.FastAddValue((KeyString)"baseVal", baseVal, JSPropertyAttributes.EnumerableConfigurableValue);
        animLength.FastAddValue((KeyString)"animVal", animVal, JSPropertyAttributes.EnumerableConfigurableValue);
        return animLength;
    }


    private JSValue JsElementInterfacesGetViewBox087Core(DomElement element, in Arguments _)
    {
        var animRect = new JSObject();
        var baseRect = new JSObject();
        double vbX = 0, vbY = 0, vbW = 0, vbH = 0;
        if (TryGetAttribute(element, "viewBox", out var vb) && !string.IsNullOrWhiteSpace(vb))
        {
            var parts = vb.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
            {
                double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out vbX);
                double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out vbY);
                double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out vbW);
                double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out vbH);
            }
        }

        baseRect.FastAddValue((KeyString)"x", new JSNumber(vbX), JSPropertyAttributes.EnumerableConfigurableValue);
        baseRect.FastAddValue((KeyString)"y", new JSNumber(vbY), JSPropertyAttributes.EnumerableConfigurableValue);
        baseRect.FastAddValue((KeyString)"width", new JSNumber(vbW), JSPropertyAttributes.EnumerableConfigurableValue);
        baseRect.FastAddValue((KeyString)"height", new JSNumber(vbH), JSPropertyAttributes.EnumerableConfigurableValue);
        animRect.FastAddValue((KeyString)"baseVal", baseRect, JSPropertyAttributes.EnumerableConfigurableValue);
        animRect.FastAddValue((KeyString)"animVal", baseRect, JSPropertyAttributes.EnumerableConfigurableValue);
        return animRect;
    }


    private JSValue JsElementInterfacesGetNumberOfChars088Core(DomElement element, in Arguments _)
    {
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        return new JSNumber(sb.Length);
    }


    private JSValue JsElementInterfacesGetComputedTextLength089Core(DomElement element, in Arguments _)
    {
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        // Stub: estimate using font-size * character count * 0.6 average advance ratio
        double fontSize = 16;
        if (TryGetAttribute(element, "font-size", out var fs))
        {
            var fsClean = fs.Replace("px", "").Replace("pt", "").Trim();
            double.TryParse(fsClean, NumberStyles.Any, CultureInfo.InvariantCulture, out fontSize);
        }

        return new JSNumber(sb.Length * fontSize * 0.6);
    }


    private JSValue JsElementInterfacesGetSubStringLength090Core(DomElement element, in Arguments a)
    {
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        var charnum = a.Length > 0 ? (int)a[0].DoubleValue : 0;
        var nchars = a.Length > 1 ? (int)a[1].DoubleValue : 0;
        if (charnum < 0 || charnum >= sb.Length)
            throw new JSException("INDEX_SIZE_ERR");
        if (nchars == 0)
            return new JSNumber(0);
        double fontSize = 16;
        if (TryGetAttribute(element, "font-size", out var fs))
        {
            var fsClean = fs.Replace("px", "").Replace("pt", "").Trim();
            double.TryParse(fsClean, NumberStyles.Any, CultureInfo.InvariantCulture, out fontSize);
        }

        return new JSNumber(nchars * fontSize * 0.6);
    }


    private JSValue JsElementInterfacesGetStartPositionOfChar091Core(DomElement element, in Arguments a)
    {
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        var charnum = a.Length > 0 ? (int)a[0].DoubleValue : 0;
        if (charnum < 0 || charnum >= sb.Length)
            throw new JSException("INDEX_SIZE_ERR");
        double fontSize = 16;
        if (TryGetAttribute(element, "font-size", out var fs))
        {
            var fsClean = fs.Replace("px", "").Replace("pt", "").Trim();
            double.TryParse(fsClean, NumberStyles.Any, CultureInfo.InvariantCulture, out fontSize);
        }

        var pt = new JSObject();
        pt.FastAddValue((KeyString)"x", new JSNumber(charnum * fontSize * 0.6), JSPropertyAttributes.EnumerableConfigurableValue);
        pt.FastAddValue((KeyString)"y", new JSNumber(fontSize), JSPropertyAttributes.EnumerableConfigurableValue);
        return pt;
    }


    private JSValue JsElementInterfacesGetEndPositionOfChar092Core(DomElement element, in Arguments a)
    {
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        var charnum = a.Length > 0 ? (int)a[0].DoubleValue : 0;
        if (charnum < 0 || charnum >= sb.Length)
            throw new JSException("INDEX_SIZE_ERR");
        double fontSize = 16;
        if (TryGetAttribute(element, "font-size", out var fs))
        {
            var fsClean = fs.Replace("px", "").Replace("pt", "").Trim();
            double.TryParse(fsClean, NumberStyles.Any, CultureInfo.InvariantCulture, out fontSize);
        }

        var pt = new JSObject();
        pt.FastAddValue((KeyString)"x", new JSNumber((charnum + 1) * fontSize * 0.6), JSPropertyAttributes.EnumerableConfigurableValue);
        pt.FastAddValue((KeyString)"y", new JSNumber(fontSize), JSPropertyAttributes.EnumerableConfigurableValue);
        return pt;
    }


    private JSValue JsElementInterfacesGetRotationOfChar093Core(DomElement element, in Arguments a)
    {
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        var charnum = a.Length > 0 ? (int)a[0].DoubleValue : 0;
        if (charnum < 0 || charnum >= sb.Length)
            throw new JSException("INDEX_SIZE_ERR");
        // Default rotation is 0 degrees (horizontal text)
        return new JSNumber(0);
    }


    private JSValue JsElementInterfacesSetCurrentTime095Core(ref double currentTime, in Arguments a)
    {
        if (a.Length > 0)
            currentTime = a[0].DoubleValue;
        return JSUndefined.Value;
    }

}
