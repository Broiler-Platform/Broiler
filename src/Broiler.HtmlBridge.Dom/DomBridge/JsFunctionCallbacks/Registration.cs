using Broiler.JavaScript.BuiltIns.Null;
using System.Text;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Json;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.HtmlBridge.Logging;
using Broiler.JavaScript.Engine;
using Broiler.Dom;


namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    private JSValue JsRegistrationElementFromPoint011Core(in Arguments a)
    {
        var hit = HitTestDocumentPoint(DocumentElement, GetCoordinateArgument(a, 0), GetCoordinateArgument(a, 1)).FirstOrDefault();
        return hit != null ? ToJSObject(hit) : JSNull.Value;
    }


    private JSValue JsRegistrationElementsFromPoint012Core(in Arguments a)
    {
        var hits = HitTestDocumentPoint(DocumentElement, GetCoordinateArgument(a, 0), GetCoordinateArgument(a, 1));
        return new JSArray([.. hits.Select(ToJSObject)]);
    }


    // MutationObserver observe()/disconnect() callbacks moved to the Phase 3
    // MutationObserverBinding feature module (Broiler.HtmlBridge.Dom.Features).

    // createTreeWalker / createNodeIterator / createComment moved to the Phase 3
    // TraversalBinding feature module (Broiler.HtmlBridge.Dom.Features).

    private JSValue JsRegistrationCreateDocumentType057Core(JSContext context, in Arguments a)
    {
        if (a.Length < 3)
            throw new JSException("Failed to execute 'createDocumentType' on 'DOMImplementation': 3 arguments required.");
        var qualifiedName = a[0].ToString();
        var publicId = a[1].ToString();
        var systemId = a[2].ToString();
        // Doctype names with colons are validated as qualified names (NamespaceError if malformed)
        if (qualifiedName.Contains(':'))
            ValidateQualifiedName(qualifiedName, null, context);
        else
            ValidateElementName(qualifiedName, context);
        var doctype = CreateBridgeDocumentType(qualifiedName, publicId, systemId);
        return ToJSObject(doctype);
    }


    private JSValue JsRegistrationCreateDocument058Core(JSContext context, in Arguments a)
    {
        var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
        var qName = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? a[1].ToString() : null;
        var doctypeArg = a.Length > 2 ? a[2] : null;
        if (!string.IsNullOrEmpty(qName))
            ValidateQualifiedName(qName, ns, context);
        // Phase 4 item 1 (P4.4a): a createDocument root is a canonical DomDocument (was a #subdoc-root
        // sentinel element).
        var docRoot = CreateBrowsingContextDocument();
        // Append doctype if provided — a DocumentType is a legitimate canonical child of a DomDocument.
        if (doctypeArg is JSObject dtObj)
        {
            foreach (var kvp in _jsObjects.Entries)
            {
                if (kvp.Value == dtObj && kvp.Key is DomNode dtNode)
                {
                    docRoot.AppendChild(dtNode);
                    break;
                }
            }
        }

        // Create document element if qualifiedName is provided (appended after the doctype, per DOM).
        if (!string.IsNullOrEmpty(qName))
        {
            var docEl = string.IsNullOrEmpty(ns)
                ? CreateBridgeElement(qName)
                : CreateBridgeElementNS(ns, qName);
            docRoot.AppendChild(docEl);
        }

        return _subDocuments.BuildDocument(docRoot);
    }


    private JSValue JsRegistrationCreateHTMLDocument059Core(in Arguments a)
    {
        var title = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
        // Phase 4 item 1 (P4.4a): a createHTMLDocument root is a canonical DomDocument (was a
        // #subdoc-root sentinel element); doctype + <html> are appended as canonical document children.
        // Phase 4 item 1 (P4.4c): structural nodes are appended under docRoot (a canonical
        // DomDocument), so GetOwningDocument derives their owner from tree position — no OwnerDocRoot.
        var docRoot = CreateBrowsingContextDocument();
        var doctype = CreateBridgeDocumentType("html", string.Empty, string.Empty);
        docRoot.AppendChild(doctype);
        // "http://www.w3.org/1999/xhtml" is the default HTML namespace the funnel applies.
        var htmlEl = CreateBridgeElement("html");
        docRoot.AppendChild(htmlEl);
        var headEl = CreateBridgeElement("head");
        SetParent(headEl, htmlEl);
        htmlEl.AppendChild(headEl);
        // Add <title> element if title argument is provided
        if (title != null)
        {
            var titleEl = CreateBridgeElement("title");
            SetParent(titleEl, headEl);
            headEl.AppendChild(titleEl);
            var titleText = CreateBridgeTextNode(title);
            SetParent(titleText, titleEl);
            titleEl.AppendChild(titleText);
        }

        var bodyEl = CreateBridgeElement("body");
        SetParent(bodyEl, htmlEl);
        htmlEl.AppendChild(bodyEl);
        return _subDocuments.BuildDocument(docRoot);
    }


    private JSValue JsRegistrationAddEventListener060Core(DomNode? docNode, in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        var type = a[0].ToString();
        if (!GetEventListeners(docNode).TryGetValue(type, out var listeners))
        {
            listeners = [];
            GetEventListeners(docNode)[type] = listeners;
        }

        Dom.Features.EventListenerBinding.AddListener(listeners, a[1], a.Length > 2 ? a[2] : JSUndefined.Value);
        return JSUndefined.Value;
    }


    private JSValue JsRegistrationRemoveEventListener061Core(DomNode? docNode, in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        var type = a[0].ToString();
        Dom.Features.EventListenerBinding.RemoveListener(
            GetEventListeners(docNode).TryGetValue(type, out var listeners) ? listeners : null,
            a[1], a.Length > 2 ? a[2] : JSUndefined.Value);
        return JSUndefined.Value;
    }


    private JSValue JsRegistrationDispatchEvent062Core(DomBridge? bridgeRef, DomNode? docNode, in Arguments a)
    {
        if (a.Length == 0)
            return JSBoolean.True;
        if (a[0] is not JSObject evt)
            return JSBoolean.True;
        return bridgeRef.DispatchEventOnElement(docNode, evt);
    }


    private JSValue JsRegistrationGetContentType063Core(in Arguments _)
    {
        if (_pageUrl.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) || _pageUrl.EndsWith(".xht", StringComparison.OrdinalIgnoreCase) || _pageUrl.Contains("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
            return new JSString("application/xhtml+xml");
        return new JSString("text/html");
    }



    private JSValue JsRegistrationAlert076Core(in Arguments a)
    {
        var msg = a.Length > 0 ? a[0].ToString() : string.Empty;
        RenderLogger.LogDebug(LogCategory.JavaScript, "DomBridge.alert", msg);
        return JSUndefined.Value;
    }


    private JSValue JsRegistrationGetComputedStyle121Core(DomBridge? bridgeForStyle, in Arguments a)
    {
        if (a.Length == 0)
            return new JSObject();
        var targetObj = a[0] as JSObject;
        var el = targetObj != null ? bridgeForStyle.FindDomElementByJSObject(targetObj) : null;
        var pseudoElement = a.Length > 1 ? a[1]?.ToString() : null;
        return bridgeForStyle.BuildComputedStyleObject(el, pseudoElement);
    }


    private JSValue JsRegistrationNow122Core(long performanceTimeOrigin, in Arguments _)
    {
        var elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - performanceTimeOrigin;
        return new JSNumber(elapsed);
    }


    private JSValue JsRegistrationScroll133Core(in Arguments a)
    {
        var (left, top, behavior) = GetScrollArguments(a);
        SetElementScrollOffsetsWithBehavior(DocumentElement, left, top, clamp: false, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsRegistrationScrollTo134Core(in Arguments a)
    {
        var (left, top, behavior) = GetScrollArguments(a);
        SetElementScrollOffsetsWithBehavior(DocumentElement, left, top, clamp: false, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsRegistrationScrollBy135Core(in Arguments a)
    {
        var (left, top, behavior) = GetScrollArguments(a);
        SetElementScrollOffsetsWithBehavior(DocumentElement, left, top, relative: true, clamp: false, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsRegistrationAddEventListener136Core(in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        var type = a[0].ToString();
        Dom.Features.EventListenerBinding.AddListener(
            _eventTargets.WindowListenersForAdd(type), a[1], a.Length > 2 ? a[2] : JSUndefined.Value);
        return JSUndefined.Value;
    }


    private JSValue JsRegistrationRemoveEventListener137Core(in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        var type = a[0].ToString();
        Dom.Features.EventListenerBinding.RemoveListener(
            _eventTargets.TryGetWindowListeners(type, out var listeners) ? listeners : null,
            a[1], a.Length > 2 ? a[2] : JSUndefined.Value);
        return JSUndefined.Value;
    }


    private JSValue JsRegistrationDispatchEvent138Core(in Arguments a)
    {
        if (a.Length == 0 || a[0] is not JSObject evt)
            return JSBoolean.True;
        return DispatchWindowEvent(evt);
    }


    private JSValue JsRegistrationSetScale143Core(in Arguments a)
    {
        if (a.Length > 0)
            SetVisualViewportScale(a[0].DoubleValue);
        return JSUndefined.Value;
    }


    private JSValue JsRegistrationAddEventListener146Core(in Arguments a)
    {
        if (a.Length > 1 && a[0].ToString().Equals("scroll", StringComparison.OrdinalIgnoreCase) && a[1] is JSFunction listener)
        {
            _eventTargets.AddVisualViewportScrollListener(listener);
        }

        return JSUndefined.Value;
    }


    private JSValue JsRegistrationRemoveEventListener147Core(in Arguments a)
    {
        if (a.Length > 1 && a[0].ToString().Equals("scroll", StringComparison.OrdinalIgnoreCase) && a[1] is JSFunction listener)
        {
            _eventTargets.RemoveVisualViewportScrollListener(listener);
        }

        return JSUndefined.Value;
    }


    private JSValue JsRegistrationSetCookie149Core(ref string? cookieStore, in Arguments a)
    {
        if (a.Length > 0)
        {
            var val = a[0].ToString();
            // Simplified: just append cookie value (real browsers parse/update)
            if (!string.IsNullOrEmpty(cookieStore))
                cookieStore += "; " + val;
            else
                cookieStore = val;
        }

        return JSUndefined.Value;
    }


    private static JSValue JsRegistrationGetCurrentTime152Core(DomElement element, in Arguments _)
    {
        if (GetElementRuntimeState(element).Animation.CurrentTimeMilliseconds.TryGet(out var value) && value is double currentTimeMs)
        {
            return new JSNumber(currentTimeMs);
        }

        return new JSNumber(0);
    }


    private static JSValue JsRegistrationSetCurrentTime153Core(DomElement element, in Arguments a)
    {
        if (a.Length > 0)
            GetElementRuntimeState(element).Animation.CurrentTimeMilliseconds.Set(a[0].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsRegistrationThen154Core(JSObject? ready, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSFunction fn)
            fn.InvokeFunction(new Arguments(JSUndefined.Value, JSUndefined.Value));
        return ready;
    }

}
