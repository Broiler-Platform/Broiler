using Broiler.JavaScript.BuiltIns.Null;
using System.Text;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    private JSValue JsSubDocumentObjectsGetBody003Core(global::Broiler.Dom.DomElement docRoot, in Arguments _)
    {
        var htmlEl = GetDocumentElement(docRoot);
        foreach (var child in ChildElements(htmlEl))
        {
            if (string.Equals(child.TagName, "body", StringComparison.OrdinalIgnoreCase))
                return ToJSObject(child);
        }

        return JSNull.Value;
    }


    private JSValue JsSubDocumentObjectsGetHead004Core(global::Broiler.Dom.DomElement docRoot, in Arguments _)
    {
        var htmlEl = GetDocumentElement(docRoot);
        foreach (var child in ChildElements(htmlEl))
        {
            if (string.Equals(child.TagName, "head", StringComparison.OrdinalIgnoreCase))
                return ToJSObject(child);
        }

        return JSNull.Value;
    }


    private JSValue JsSubDocumentObjectsGetTitle005Core(global::Broiler.Dom.DomElement docRoot, in Arguments _)
    {
        var htmlEl = GetDocumentElement(docRoot);
        var head = ChildElements(htmlEl).FirstOrDefault(c => string.Equals(c.TagName, "head", StringComparison.OrdinalIgnoreCase));
        if (head != null)
        {
            var titleEl = ChildElements(head).FirstOrDefault(c => string.Equals(c.TagName, "title", StringComparison.OrdinalIgnoreCase));
            if (titleEl != null)
            {
                var sb = new StringBuilder();
                CollectTextContent(titleEl, sb);
                return new JSString(sb.ToString());
            }
        }

        return new JSString(string.Empty);
    }


    private JSValue JsSubDocumentObjectsSetTitle006Core(global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        var htmlEl = GetDocumentElement(docRoot);
        var head = ChildElements(htmlEl).FirstOrDefault(c => string.Equals(c.TagName, "head", StringComparison.OrdinalIgnoreCase));
        if (head != null)
        {
            var titleEl = ChildElements(head).FirstOrDefault(c => string.Equals(c.TagName, "title", StringComparison.OrdinalIgnoreCase));
            if (titleEl != null)
                SetElementTextContent(titleEl, a.Length > 0 ? a[0].ToString() : string.Empty);
        }

        return JSUndefined.Value;
    }


    private JSValue JsSubDocumentObjectsGetForms007Core(global::Broiler.Dom.DomElement docRoot, in Arguments _)
    {
        var results = new List<JSValue>();
        CollectByTagName(docRoot, "form", results);
        return new JSArray(results);
    }


    private JSValue JsSubDocumentObjectsGetChildNodes008Core(global::Broiler.Dom.DomElement docRoot, in Arguments _)
    {
        var arr = new JSArray();
        foreach (var child in ChildElements(docRoot))
            arr.Add(ToJSObject(child));
        return arr;
    }


    private JSValue JsSubDocumentObjectsGetElementById014Core(global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        var id = a.Length > 0 ? a[0].ToString() : string.Empty;
        var found = FindInSubTree(docRoot, el => el.Id == id);
        return found != null ? ToJSObject(found) : JSNull.Value;
    }


    private JSValue JsSubDocumentObjectsGetElementsByTagName015Core(global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        var tagName = a.Length > 0 ? a[0].ToString().ToLowerInvariant() : string.Empty;
        var results = new List<JSValue>();
        CollectByTagName(docRoot, tagName, results);
        return new JSArray(results);
    }


    private JSValue JsSubDocumentObjectsCreateElement016Core(global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'createElement': 1 argument required.");
        var tagName = a[0].ToString();
        ValidateElementName(tagName, _jsContext!);
        tagName = AsciiToLower(tagName);
        var el = CreateBridgeElement(tagName);
        GetElementRuntimeState(el).OwnerDocRoot = docRoot;
        _knownNodes.Add(el);
        return ToJSObject(el);
    }


    private JSValue JsSubDocumentObjectsCreateTextNode017Core(global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        var text = a.Length > 0 ? a[0].ToString() : string.Empty;
        var el = CreateBridgeTextNode(text);
        GetElementRuntimeState(el).OwnerDocRoot = docRoot;
        _knownNodes.Add(el);
        return ToJSObject(el);
    }


    private JSValue JsSubDocumentObjectsCreateComment018Core(global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        var data = a.Length > 0 ? a[0].ToString() : string.Empty;
        var el = CreateBridgeCommentNode(data);
        GetElementRuntimeState(el).OwnerDocRoot = docRoot;
        _knownNodes.Add(el);
        return ToJSObject(el);
    }


    private JSValue JsSubDocumentObjectsCreateElementNS019Core(global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
        var localName = a.Length > 1 ? a[1].ToString() : (a.Length > 0 ? a[0].ToString() : "div");
        ValidateQualifiedName(localName, ns, _jsContext!);
        var el = string.IsNullOrEmpty(ns)
            ? CreateBridgeElement(localName)
            : CreateBridgeElementNS(ns, localName);
        GetElementRuntimeState(el).OwnerDocRoot = docRoot;
        _knownNodes.Add(el);
        return ToJSObject(el);
    }


    private JSValue JsSubDocumentObjectsCreateEvent034Core(in Arguments a)
    {
        var evt = new JSObject();
        var legacyCancelBubble = false;
        evt.FastAddValue((KeyString)"type", new JSString(string.Empty), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"bubbles", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"cancelable", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"target", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"currentTarget", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"srcElement", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"eventPhase", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"isTrusted", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"timeStamp", new JSNumber(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"detail", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"view", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"screenX", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"screenY", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"clientX", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"clientY", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"x", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"y", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"ctrlKey", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"altKey", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"shiftKey", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"metaKey", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"key", new JSString(string.Empty), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"location", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"repeat", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"keyCode", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"charCode", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"which", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"button", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"buttons", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"deltaX", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"deltaY", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"deltaZ", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"deltaMode", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"relatedTarget", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsSubDocumentObjectsStopPropagation020(in Arguments __)
        {
            legacyCancelBubble = true;
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"stopPropagation", new JSFunction(JsSubDocumentObjectsStopPropagation020, "stopPropagation", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsSubDocumentObjectsStopImmediatePropagation021(in Arguments __)
        {
            legacyCancelBubble = true;
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"stopImmediatePropagation", new JSFunction(JsSubDocumentObjectsStopImmediatePropagation021, "stopImmediatePropagation", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsSubDocumentObjectsPreventDefault022(in Arguments __)
        {
            evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"preventDefault", new JSFunction(JsSubDocumentObjectsPreventDefault022, "preventDefault", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsSubDocumentObjectsGetCancelBubble023(in Arguments __)
        {
            return legacyCancelBubble ? JSBoolean.True : JSBoolean.False;
        }

        JSValue JsSubDocumentObjectsSetCancelBubble024(in Arguments setArgs)
        {
            if (setArgs.Length > 0 && setArgs[0].BooleanValue)
                legacyCancelBubble = true;
            return JSUndefined.Value;
        }

        evt.FastAddProperty((KeyString)"cancelBubble", new JSFunction(JsSubDocumentObjectsGetCancelBubble023, "get cancelBubble"), new JSFunction(JsSubDocumentObjectsSetCancelBubble024, "set cancelBubble"), JSPropertyAttributes.EnumerableConfigurableProperty);
        JSValue JsSubDocumentObjectsGetReturnValue025(in Arguments __)
        {
            return evt[(KeyString)"defaultPrevented"].BooleanValue ? JSBoolean.False : JSBoolean.True;
        }

        JSValue JsSubDocumentObjectsSetReturnValue026(in Arguments setArgs)
        {
            if (setArgs.Length > 0 && !setArgs[0].BooleanValue)
                evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
            return JSUndefined.Value;
        }

        evt.FastAddProperty((KeyString)"returnValue", new JSFunction(JsSubDocumentObjectsGetReturnValue025, "get returnValue"), new JSFunction(JsSubDocumentObjectsSetReturnValue026, "set returnValue"), JSPropertyAttributes.EnumerableConfigurableProperty);
        JSValue JsSubDocumentObjectsInitEvent027(in Arguments initArgs)
        {
            if (initArgs.Length > 0)
                evt[(KeyString)"type"] = new JSString(initArgs[0].ToString());
            if (initArgs.Length > 1)
                evt[(KeyString)"bubbles"] = initArgs[1].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 2)
                evt[(KeyString)"cancelable"] = initArgs[2].BooleanValue ? JSBoolean.True : JSBoolean.False;
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"initEvent", new JSFunction(JsSubDocumentObjectsInitEvent027, "initEvent", 3), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsSubDocumentObjectsInitUIEvent028(in Arguments initArgs)
        {
            if (initArgs.Length > 0)
                evt[(KeyString)"type"] = new JSString(initArgs[0].ToString());
            if (initArgs.Length > 1)
                evt[(KeyString)"bubbles"] = initArgs[1].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 2)
                evt[(KeyString)"cancelable"] = initArgs[2].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 3)
                evt[(KeyString)"view"] = initArgs[3];
            if (initArgs.Length > 4)
                evt[(KeyString)"detail"] = initArgs[4];
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"initUIEvent", new JSFunction(JsSubDocumentObjectsInitUIEvent028, "initUIEvent", 5), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsSubDocumentObjectsInitCustomEvent029(in Arguments initArgs)
        {
            if (initArgs.Length > 0)
                evt[(KeyString)"type"] = new JSString(initArgs[0].ToString());
            if (initArgs.Length > 1)
                evt[(KeyString)"bubbles"] = initArgs[1].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 2)
                evt[(KeyString)"cancelable"] = initArgs[2].BooleanValue ? JSBoolean.True : JSBoolean.False;
            evt[(KeyString)"detail"] = initArgs.Length > 3 ? initArgs[3] : JSNull.Value;
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"initCustomEvent", new JSFunction(JsSubDocumentObjectsInitCustomEvent029, "initCustomEvent", 4), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsSubDocumentObjectsInitFocusEvent030(in Arguments initArgs)
        {
            if (initArgs.Length > 0)
                evt[(KeyString)"type"] = new JSString(initArgs[0].ToString());
            if (initArgs.Length > 1)
                evt[(KeyString)"bubbles"] = initArgs[1].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 2)
                evt[(KeyString)"cancelable"] = initArgs[2].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 3)
                evt[(KeyString)"view"] = initArgs[3];
            if (initArgs.Length > 4)
                evt[(KeyString)"detail"] = new JSNumber(initArgs[4].DoubleValue);
            if (initArgs.Length > 5)
                evt[(KeyString)"relatedTarget"] = initArgs[5];
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"initFocusEvent", new JSFunction(JsSubDocumentObjectsInitFocusEvent030, "initFocusEvent", 6), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsSubDocumentObjectsInitKeyboardEvent031(in Arguments initArgs)
        {
            if (initArgs.Length > 0)
                evt[(KeyString)"type"] = new JSString(initArgs[0].ToString());
            if (initArgs.Length > 1)
                evt[(KeyString)"bubbles"] = initArgs[1].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 2)
                evt[(KeyString)"cancelable"] = initArgs[2].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 3)
                evt[(KeyString)"view"] = initArgs[3];
            if (initArgs.Length > 4)
                evt[(KeyString)"key"] = new JSString(initArgs[4].ToString());
            if (initArgs.Length > 5)
                evt[(KeyString)"location"] = new JSNumber(initArgs[5].DoubleValue);
            if (initArgs.Length > 6)
                evt[(KeyString)"ctrlKey"] = initArgs[6].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 7)
                evt[(KeyString)"altKey"] = initArgs[7].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 8)
                evt[(KeyString)"shiftKey"] = initArgs[8].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 9)
                evt[(KeyString)"metaKey"] = initArgs[9].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 10)
                evt[(KeyString)"repeat"] = initArgs[10].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 11)
            {
                var keyCode = initArgs[11].DoubleValue;
                evt[(KeyString)"keyCode"] = new JSNumber(keyCode);
                evt[(KeyString)"which"] = new JSNumber(keyCode);
            }

            if (initArgs.Length > 12)
            {
                var charCode = initArgs[12].DoubleValue;
                evt[(KeyString)"charCode"] = new JSNumber(charCode);
                if (charCode != 0)
                    evt[(KeyString)"which"] = new JSNumber(charCode);
            }

            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"initKeyboardEvent", new JSFunction(JsSubDocumentObjectsInitKeyboardEvent031, "initKeyboardEvent", 13), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsSubDocumentObjectsInitMouseEvent032(in Arguments initArgs)
        {
            if (initArgs.Length > 0)
                evt[(KeyString)"type"] = new JSString(initArgs[0].ToString());
            if (initArgs.Length > 1)
                evt[(KeyString)"bubbles"] = initArgs[1].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 2)
                evt[(KeyString)"cancelable"] = initArgs[2].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 3)
                evt[(KeyString)"view"] = initArgs[3];
            if (initArgs.Length > 4)
                evt[(KeyString)"detail"] = new JSNumber(initArgs[4].DoubleValue);
            if (initArgs.Length > 5)
                evt[(KeyString)"screenX"] = new JSNumber(initArgs[5].DoubleValue);
            if (initArgs.Length > 6)
                evt[(KeyString)"screenY"] = new JSNumber(initArgs[6].DoubleValue);
            if (initArgs.Length > 7)
            {
                evt[(KeyString)"clientX"] = new JSNumber(initArgs[7].DoubleValue);
                evt[(KeyString)"x"] = new JSNumber(initArgs[7].DoubleValue);
            }

            if (initArgs.Length > 8)
            {
                evt[(KeyString)"clientY"] = new JSNumber(initArgs[8].DoubleValue);
                evt[(KeyString)"y"] = new JSNumber(initArgs[8].DoubleValue);
            }

            if (initArgs.Length > 9)
                evt[(KeyString)"ctrlKey"] = initArgs[9].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 10)
                evt[(KeyString)"altKey"] = initArgs[10].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 11)
                evt[(KeyString)"shiftKey"] = initArgs[11].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 12)
                evt[(KeyString)"metaKey"] = initArgs[12].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 13)
            {
                var button = initArgs[13].DoubleValue;
                evt[(KeyString)"button"] = new JSNumber(button);
                evt[(KeyString)"buttons"] = new JSNumber(button switch
                {
                    0 => 1,
                    1 => 4,
                    2 => 2,
                    _ => 0
                });
            }

            if (initArgs.Length > 14)
                evt[(KeyString)"relatedTarget"] = initArgs[14];
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"initMouseEvent", new JSFunction(JsSubDocumentObjectsInitMouseEvent032, "initMouseEvent", 15), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsSubDocumentObjectsInitWheelEvent033(in Arguments initArgs)
        {
            if (initArgs.Length > 0)
                evt[(KeyString)"type"] = new JSString(initArgs[0].ToString());
            if (initArgs.Length > 1)
                evt[(KeyString)"bubbles"] = initArgs[1].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 2)
                evt[(KeyString)"cancelable"] = initArgs[2].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 3)
                evt[(KeyString)"view"] = initArgs[3];
            if (initArgs.Length > 4)
                evt[(KeyString)"detail"] = new JSNumber(initArgs[4].DoubleValue);
            if (initArgs.Length > 5)
                evt[(KeyString)"screenX"] = new JSNumber(initArgs[5].DoubleValue);
            if (initArgs.Length > 6)
                evt[(KeyString)"screenY"] = new JSNumber(initArgs[6].DoubleValue);
            if (initArgs.Length > 7)
            {
                evt[(KeyString)"clientX"] = new JSNumber(initArgs[7].DoubleValue);
                evt[(KeyString)"x"] = new JSNumber(initArgs[7].DoubleValue);
            }

            if (initArgs.Length > 8)
            {
                evt[(KeyString)"clientY"] = new JSNumber(initArgs[8].DoubleValue);
                evt[(KeyString)"y"] = new JSNumber(initArgs[8].DoubleValue);
            }

            if (initArgs.Length > 9)
                evt[(KeyString)"button"] = new JSNumber(initArgs[9].DoubleValue);
            if (initArgs.Length > 10)
                evt[(KeyString)"relatedTarget"] = initArgs[10];
            if (initArgs.Length > 11)
            {
                var modifiers = initArgs[11].ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                evt[(KeyString)"ctrlKey"] = Array.Exists(modifiers, m => string.Equals(m, "Control", StringComparison.OrdinalIgnoreCase)) ? JSBoolean.True : JSBoolean.False;
                evt[(KeyString)"altKey"] = Array.Exists(modifiers, m => string.Equals(m, "Alt", StringComparison.OrdinalIgnoreCase)) ? JSBoolean.True : JSBoolean.False;
                evt[(KeyString)"shiftKey"] = Array.Exists(modifiers, m => string.Equals(m, "Shift", StringComparison.OrdinalIgnoreCase)) ? JSBoolean.True : JSBoolean.False;
                evt[(KeyString)"metaKey"] = Array.Exists(modifiers, m => string.Equals(m, "Meta", StringComparison.OrdinalIgnoreCase)) ? JSBoolean.True : JSBoolean.False;
            }

            if (initArgs.Length > 12)
                evt[(KeyString)"deltaX"] = new JSNumber(initArgs[12].DoubleValue);
            if (initArgs.Length > 13)
                evt[(KeyString)"deltaY"] = new JSNumber(initArgs[13].DoubleValue);
            if (initArgs.Length > 14)
                evt[(KeyString)"deltaZ"] = new JSNumber(initArgs[14].DoubleValue);
            if (initArgs.Length > 15)
                evt[(KeyString)"deltaMode"] = new JSNumber(initArgs[15].DoubleValue);
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"initWheelEvent", new JSFunction(JsSubDocumentObjectsInitWheelEvent033, "initWheelEvent", 16), JSPropertyAttributes.EnumerableConfigurableValue);
        return evt;
    }


    private JSValue JsSubDocumentObjectsQuerySelector035Core(global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
        var found = FindInSubTree(docRoot, el => MatchesSelector(el, selector));
        return found != null ? ToJSObject(found) : JSNull.Value;
    }


    private JSValue JsSubDocumentObjectsQuerySelectorAll036Core(global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
        var results = new List<JSValue>();
        CollectMatching(docRoot, el => MatchesSelector(el, selector), results);
        return new JSArray(results);
    }


    private JSValue JsSubDocumentObjectsElementFromPoint037Core(global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        var hit = HitTestDocumentPoint(docRoot, GetCoordinateArgument(a, 0), GetCoordinateArgument(a, 1)).FirstOrDefault();
        return hit != null ? ToJSObject(hit) : JSNull.Value;
    }


    private JSValue JsSubDocumentObjectsElementsFromPoint038Core(global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        var hits = HitTestDocumentPoint(docRoot, GetCoordinateArgument(a, 0), GetCoordinateArgument(a, 1));
        return new JSArray(hits.Select(ToJSObject).ToArray());
    }


    private JSValue JsSubDocumentObjectsOpen039Core(global::Broiler.JavaScript.Runtime.JSObject? doc, global::Broiler.Dom.DomElement docRoot, in Arguments _)
    {
        ClearChildren(docRoot);
        return doc;
    }


    private JSValue JsSubDocumentObjectsWrite040Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var fragment = a[0].ToString();
        // Parse DOCTYPE if present
        var doctype = bridge.ParseDocType(fragment);
        var (parsedDoc, allEls, _) = BuildDocumentTree(fragment);
        if (docRoot.ChildNodes.Count == 0)
        {
            if (doctype != null)
            {
                SetParent(doctype, docRoot);
                docRoot.AppendChild(doctype);
                bridge._knownNodes.Add(doctype);
            }

            // parsedDoc is the <html> element from HtmlTreeBuilder.
            // Add it directly to docRoot (not its children).
            SetParent(parsedDoc, docRoot);
            docRoot.AppendChild(parsedDoc);
            if (!bridge._knownNodes.Contains(parsedDoc))
                bridge._knownNodes.Add(parsedDoc);
            foreach (var el in allEls)
            {
                if (!bridge._knownNodes.Contains(el))
                    bridge._knownNodes.Add(el);
            }
        }
        else
        {
            var bodyEl = bridge.FindInSubTree(docRoot, el => string.Equals(el.TagName, "body", StringComparison.OrdinalIgnoreCase));
            if (bodyEl != null)
            {
                var parsedBody = FindInTree(parsedDoc, el => string.Equals(el.TagName, "body", StringComparison.OrdinalIgnoreCase));
                if (parsedBody != null)
                {
                    foreach (var child in parsedBody.ChildNodes.ToArray())
                    {
                        SetParent(child, bodyEl);
                        bodyEl.AppendChild(child);
                    }
                }
            }

            foreach (var el in allEls)
            {
                if (!bridge._knownNodes.Contains(el))
                    bridge._knownNodes.Add(el);
            }
        }

        return JSUndefined.Value;
    }


    private JSValue JsSubDocumentObjectsGetImages041Core(global::Broiler.Dom.DomElement docRoot, in Arguments _)
    {
        var results = new List<JSValue>();
        CollectByTagName(docRoot, "img", results);
        return new JSArray(results);
    }


    private JSValue JsSubDocumentObjectsGetLinks042Core(global::Broiler.Dom.DomElement docRoot, in Arguments _)
    {
        var results = new List<JSValue>();
        CollectMatching(docRoot, el => (string.Equals(el.TagName, "a", StringComparison.OrdinalIgnoreCase) || string.Equals(el.TagName, "area", StringComparison.OrdinalIgnoreCase)) && HasAttr(el, "href"), results);
        return new JSArray(results);
    }


    private JSValue JsSubDocumentObjectsRemoveChild044Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var childObj = a[0] as JSObject;
        if (childObj == null)
            return JSNull.Value;
        foreach (var child in ChildElements(docRoot).ToList())
        {
            if (bridge._jsObjectCache.TryGetValue(child, out var cached) && cached == childObj)
            {
                var idx = ChildIndexOf(docRoot, child);
                if (idx >= 0)
                {
                    bridge.NotifyNodeIteratorPreRemoval(child);
                    RemoveNthChild(docRoot, idx);
                    SetParent(child, null);
                    bridge.NotifyChildRemoved(docRoot, child, idx);
                }

                return childObj;
            }
        }

        return childObj;
    }


    private JSValue JsSubDocumentObjectsAppendChild045Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var childObj = a[0] as JSObject;
        if (childObj == null)
            return a.Length > 0 ? a[0] : JSNull.Value;
        foreach (var kvp in bridge._jsObjectCache)
        {
            if (kvp.Value == childObj && kvp.Key is Broiler.Dom.DomElement child)
            {
                if (ParentEl(child) != null)
                    child.Remove();
                SetParent(child, docRoot);
                AdoptSubtreeIntoDocument(child, docRoot);
                docRoot.AppendChild(child);
                return childObj;
            }
        }

        return a[0];
    }


    private JSValue JsSubDocumentObjectsAppend046Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var nodes = bridge.BuildChildNodeArgumentNodes(a);
        var insertIndex = docRoot.ChildNodes.Count;
        foreach (var node in nodes)
            bridge.InsertNodeAt(docRoot, node, insertIndex++);
        return JSUndefined.Value;
    }


    private JSValue JsSubDocumentObjectsPrepend047Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.Dom.DomElement docRoot, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var nodes = bridge.BuildChildNodeArgumentNodes(a);
        var insertIndex = 0;
        foreach (var node in nodes)
            bridge.InsertNodeAt(docRoot, node, insertIndex++);
        return JSUndefined.Value;
    }


    private JSValue JsSubDocumentObjectsCreateDocumentType048Core(in Arguments a)
    {
        if (a.Length < 3)
            throw new JSException("Failed to execute 'createDocumentType' on 'DOMImplementation': 3 arguments required.");
        var qualifiedName = a[0].ToString();
        var publicId = a[1].ToString();
        var systemId = a[2].ToString();
        ValidateElementName(qualifiedName, _jsContext!);
        var dt = CreateBridgeElement("#doctype");
        GetElementRuntimeState(dt).DocumentType.Name.Set(qualifiedName);
        GetElementRuntimeState(dt).DocumentType.PublicId.Set(publicId);
        GetElementRuntimeState(dt).DocumentType.SystemId.Set(systemId);
        GetElementRuntimeState(dt).DocumentType.InternalSubset.Set(null);
        _knownNodes.Add(dt);
        return ToJSObject(dt);
    }


    private JSValue JsSubDocumentObjectsCreateDocument049Core(in Arguments a)
    {
        var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
        var qName = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? a[1].ToString() : null;
        var doctypeArg = a.Length > 2 ? a[2] : null;
        if (!string.IsNullOrEmpty(qName))
            ValidateQualifiedName(qName, ns, _jsContext!);
        var subDocRoot = CreateBridgeElement("#subdoc-root");
        GetElementRuntimeState(subDocRoot).Document.HasViewport.Set(false);
        _knownNodes.Add(subDocRoot);
        if (doctypeArg is JSObject dtObj)
        {
            foreach (var kvp in _jsObjectCache)
            {
                if (kvp.Value == dtObj && kvp.Key is Broiler.Dom.DomElement dtEl)
                {
                    SetParent(dtEl, subDocRoot);
                    GetElementRuntimeState(dtEl).OwnerDocRoot = subDocRoot;
                    subDocRoot.AppendChild(dtEl);
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(qName))
        {
            var docEl = string.IsNullOrEmpty(ns)
                ? CreateBridgeElement(qName)
                : CreateBridgeElementNS(ns, qName);
            SetParent(docEl, subDocRoot);
            GetElementRuntimeState(docEl).OwnerDocRoot = subDocRoot;
            subDocRoot.AppendChild(docEl);
            _knownNodes.Add(docEl);
        }

        return BuildSubDocument(subDocRoot);
    }


    private JSValue JsSubDocumentObjectsCreateHTMLDocument050Core(in Arguments a)
    {
        var subTitle = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
        var subDocRoot = CreateBridgeElement("#subdoc-root");
        GetElementRuntimeState(subDocRoot).Document.HasViewport.Set(false);
        _knownNodes.Add(subDocRoot);
        var dt = CreateBridgeElement("#doctype");
        GetElementRuntimeState(dt).DocumentType.Name.Set("html");
        GetElementRuntimeState(dt).DocumentType.PublicId.Set(string.Empty);
        GetElementRuntimeState(dt).DocumentType.SystemId.Set(string.Empty);
        GetElementRuntimeState(dt).DocumentType.InternalSubset.Set(null);
        SetParent(dt, subDocRoot);
        GetElementRuntimeState(dt).OwnerDocRoot = subDocRoot;
        subDocRoot.AppendChild(dt);
        _knownNodes.Add(dt);
        // "http://www.w3.org/1999/xhtml" is the default HTML namespace the funnel applies.
        var subHtml = CreateBridgeElement("html");
        SetParent(subHtml, subDocRoot);
        GetElementRuntimeState(subHtml).OwnerDocRoot = subDocRoot;
        subDocRoot.AppendChild(subHtml);
        _knownNodes.Add(subHtml);
        var subHead = CreateBridgeElement("head");
        SetParent(subHead, subHtml);
        GetElementRuntimeState(subHead).OwnerDocRoot = subDocRoot;
        subHtml.AppendChild(subHead);
        _knownNodes.Add(subHead);
        if (subTitle != null)
        {
            var subTitleEl = CreateBridgeElement("title");
            SetParent(subTitleEl, subHead);
            GetElementRuntimeState(subTitleEl).OwnerDocRoot = subDocRoot;
            subHead.AppendChild(subTitleEl);
            _knownNodes.Add(subTitleEl);
            var subTitleText = CreateBridgeTextNode(subTitle);
            SetParent(subTitleText, subTitleEl);
            GetElementRuntimeState(subTitleText).OwnerDocRoot = subDocRoot;
            subTitleEl.AppendChild(subTitleText);
            _knownNodes.Add(subTitleText);
        }

        var subBody = CreateBridgeElement("body");
        SetParent(subBody, subHtml);
        GetElementRuntimeState(subBody).OwnerDocRoot = subDocRoot;
        subHtml.AppendChild(subBody);
        _knownNodes.Add(subBody);
        return BuildSubDocument(subDocRoot);
    }


    private JSValue JsSubDocumentObjectsCreateTreeWalker051Core(global::Broiler.HtmlBridge.DomBridge? bridge, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'createTreeWalker': 1 argument required.");
        var rootObj = a[0] as JSObject;
        if (rootObj == null)
            throw new JSException("Failed to execute 'createTreeWalker': parameter 1 is not of type 'Node'.");
        var rootEl = bridge.FindDomElementByJSObject(rootObj);
        if (rootEl == null)
            return JSNull.Value;
        var whatToShow = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? unchecked((int)(uint)a[1].DoubleValue) : unchecked((int)0xFFFFFFFF);
        var filterFn = a.Length > 2 && a[2] is JSFunction f ? f : (a.Length > 2 && a[2] is JSObject filterObj ? filterObj[(KeyString)"acceptNode"] as JSFunction : null);
        return bridge.BuildTreeWalker(rootEl, whatToShow, filterFn);
    }


    private JSValue JsSubDocumentObjectsCreateNodeIterator052Core(global::Broiler.HtmlBridge.DomBridge? bridge, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'createNodeIterator': 1 argument required.");
        var rootObj = a[0] as JSObject;
        if (rootObj == null)
            throw new JSException("Failed to execute 'createNodeIterator': parameter 1 is not of type 'Node'.");
        var rootEl = bridge.FindDomElementByJSObject(rootObj);
        if (rootEl == null)
            return JSNull.Value;
        var whatToShow = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? unchecked((int)(uint)a[1].DoubleValue) : unchecked((int)0xFFFFFFFF);
        var filterFn = a.Length > 2 && a[2] is JSFunction f ? f : (a.Length > 2 && a[2] is JSObject filterObj ? filterObj[(KeyString)"acceptNode"] as JSFunction : null);
        return bridge.BuildNodeIterator(rootEl, whatToShow, filterFn);
    }

}
