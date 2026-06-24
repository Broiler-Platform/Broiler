using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.Array.Typed;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Json;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    private JSValue JsSubDocumentsScroll006Core(global::Broiler.HtmlBridge.DomElement containerElement, in Arguments a)
    {
        var (left, top, behavior) = GetScrollArguments(a);
        SetSubWindowScrollOffsets(containerElement, left, top, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsSubDocumentsScrollTo007Core(global::Broiler.HtmlBridge.DomElement containerElement, in Arguments a)
    {
        var (left, top, behavior) = GetScrollArguments(a);
        SetSubWindowScrollOffsets(containerElement, left, top, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsSubDocumentsScrollBy008Core(global::Broiler.HtmlBridge.DomElement containerElement, in Arguments a)
    {
        var (left, top, behavior) = GetScrollArguments(a);
        SetSubWindowScrollOffsets(containerElement, left, top, relative: true, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsSubDocumentsGetComputedStyle009Core(global::Broiler.HtmlBridge.DomBridge? bridgeForSubStyle, in Arguments a)
    {
        if (a.Length == 0)
            return new JSObject();
        var targetObj = a[0] as JSObject;
        var el = targetObj != null ? bridgeForSubStyle.FindDomElementByJSObject(targetObj) : null;
        var pseudoElement = a.Length > 1 ? a[1]?.ToString() : null;
        return bridgeForSubStyle.BuildComputedStyleObject(el, pseudoElement);
    }

}
