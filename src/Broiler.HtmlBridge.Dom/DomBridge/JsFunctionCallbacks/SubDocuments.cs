using Broiler.JavaScript.Runtime;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom;

public sealed partial class DomBridge
{

    private JSValue JsSubDocumentsScroll006Core(DomElement containerElement, in Arguments a)
    {
        var (left, top, behavior) = GetScrollArguments(a);
        SetSubWindowScrollOffsets(containerElement, left, top, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsSubDocumentsScrollTo007Core(DomElement containerElement, in Arguments a)
    {
        var (left, top, behavior) = GetScrollArguments(a);
        SetSubWindowScrollOffsets(containerElement, left, top, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsSubDocumentsScrollBy008Core(DomElement containerElement, in Arguments a)
    {
        var (left, top, behavior) = GetScrollArguments(a);
        SetSubWindowScrollOffsets(containerElement, left, top, relative: true, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsSubDocumentsGetComputedStyle009Core(DomBridge? bridgeForSubStyle, in Arguments a)
    {
        if (a.Length == 0)
            return new JSObject();
        var targetObj = a[0] as JSObject;
        var el = targetObj != null ? bridgeForSubStyle.FindDomElementByJSObject(targetObj) : null;
        var pseudoElement = a.Length > 1 ? a[1]?.ToString() : null;
        return bridgeForSubStyle.BuildComputedStyleObject(el, pseudoElement);
    }

}
