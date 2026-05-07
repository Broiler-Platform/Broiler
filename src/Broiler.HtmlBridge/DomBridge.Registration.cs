using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
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

/// <summary>
/// JavaScript bridge registration — wires up the <c>document</c>,
/// <c>window</c>, <c>console</c>, and <c>XMLHttpRequest</c> globals
/// on the YantraJS <see cref="JSContext"/>.
/// </summary>
public sealed partial class DomBridge
{
    // ------------------------------------------------------------------
    //  JavaScript bridge
    // ------------------------------------------------------------------


    /// <summary>
    /// The element backing <c>document.documentElement</c> (the &lt;html&gt; element).
    /// </summary>
    public DomElement DocumentElement { get; } = new("html", null, null, string.Empty);

    private void RegisterDocument(JSContext context)
    {
        _jsContext = context;
        var document = new JSObject();

        // Map the document JSObject to _documentNode so that ToJSObject(_documentNode) returns
        // the same object as the 'document' variable visible in JS. This ensures
        // strict equality checks like 'range.commonAncestorContainer === document' work.
        _jsObjectCache[_documentNode] = document;

        // document.documentElement (the <html> element)
        document.FastAddValue(
            (KeyString)"documentElement",
            ToJSObject(DocumentElement),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.scrollingElement (getter — returns document.documentElement
        // in standards mode, or document.body in quirks mode; we always use
        // standards mode so it's always the <html> element).
        document.FastAddProperty(
            (KeyString)"scrollingElement",
            new JSFunction((in Arguments _) => ToJSObject(DocumentElement), "get scrollingElement"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.body (getter — first <body> child of documentElement)
        document.FastAddProperty(
            (KeyString)"body",
            new JSFunction((in Arguments a) =>
            {
                foreach (var child in DocumentElement.Children)
                {
                    if (string.Equals(child.TagName, "body", StringComparison.OrdinalIgnoreCase))
                        return ToJSObject(child);
                }
                return JSNull.Value;
            }, "get body"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.head (getter — first <head> child of documentElement)
        document.FastAddProperty(
            (KeyString)"head",
            new JSFunction((in Arguments a) =>
            {
                foreach (var child in DocumentElement.Children)
                {
                    if (string.Equals(child.TagName, "head", StringComparison.OrdinalIgnoreCase))
                        return ToJSObject(child);
                }
                return JSNull.Value;
            }, "get head"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.title (getter / setter)
        document.FastAddProperty(
            (KeyString)"title",
            new JSFunction((in Arguments a) => new JSString(Title), "get title"),
            new JSFunction((in Arguments a) =>
            {
                Title = a.Length > 0 ? a[0].ToString() : string.Empty;
                return JSUndefined.Value;
            }, "set title"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.getElementById(id)
        document.FastAddValue(
            (KeyString)"getElementById",
            new JSFunction((in Arguments a) =>
            {
                var id = a.Length > 0 ? a[0].ToString() : string.Empty;
                foreach (var el in _elements)
                {
                    if (el.Id == id)
                        return ToJSObject(el);
                }
                return JSNull.Value;
            }, "getElementById", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.getElementsByTagName(tag)
        document.FastAddValue(
            (KeyString)"getElementsByTagName",
            new JSFunction((in Arguments a) =>
            {
                var tag = a.Length > 0 ? a[0].ToString().ToLowerInvariant() : string.Empty;
                var results = new List<JSValue>();
                foreach (var el in _elements)
                {
                    if (tag == "*" || el.TagName == tag)
                        results.Add(ToJSObject(el));
                }
                return new JSArray(results);
            }, "getElementsByTagName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.getElementsByClassName(className)
        document.FastAddValue(
            (KeyString)"getElementsByClassName",
            new JSFunction((in Arguments a) =>
            {
                var className = a.Length > 0 ? a[0].ToString() : string.Empty;
                var results = new List<JSValue>();
                foreach (var el in _elements)
                {
                    var classes = new HashSet<string>(
                        (el.ClassName ?? string.Empty).Split(' ').Where(s => s.Length > 0),
                        StringComparer.Ordinal);
                    if (classes.Contains(className))
                        results.Add(ToJSObject(el));
                }
                return new JSArray(results);
            }, "getElementsByClassName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.querySelector(selector)
        document.FastAddValue(
            (KeyString)"querySelector",
            new JSFunction((in Arguments a) =>
            {
                var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
                foreach (var el in _elements)
                {
                    if (MatchesSelector(el, selector))
                        return ToJSObject(el);
                }
                return JSNull.Value;
            }, "querySelector", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.querySelectorAll(selector)
        document.FastAddValue(
            (KeyString)"querySelectorAll",
            new JSFunction((in Arguments a) =>
            {
                var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
                var results = new List<JSValue>();
                foreach (var el in _elements)
                {
                    if (MatchesSelector(el, selector))
                        results.Add(ToJSObject(el));
                }
                return new JSArray(results);
            }, "querySelectorAll", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        document.FastAddValue(
            (KeyString)"elementFromPoint",
            new JSFunction((in Arguments a) =>
            {
                var hit = HitTestDocumentPoint(DocumentElement, GetCoordinateArgument(a, 0), GetCoordinateArgument(a, 1))
                    .FirstOrDefault();
                return hit != null ? ToJSObject(hit) : JSNull.Value;
            }, "elementFromPoint", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        document.FastAddValue(
            (KeyString)"elementsFromPoint",
            new JSFunction((in Arguments a) =>
            {
                var hits = HitTestDocumentPoint(DocumentElement, GetCoordinateArgument(a, 0), GetCoordinateArgument(a, 1));
                return new JSArray(hits.Select(ToJSObject).ToArray());
            }, "elementsFromPoint", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.getAnimations() — minimal Web Animations API support used by WPT.
        document.FastAddValue(
            (KeyString)"getAnimations",
            new JSFunction((in Arguments _) => BuildAnimationList(null), "getAnimations", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createElement(tag)
        document.FastAddValue(
            (KeyString)"createElement",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    throw new JSException("Failed to execute 'createElement': 1 argument required, but only 0 present.");
                var tag = a[0].ToString();
                ValidateElementName(tag, context);
                tag = AsciiToLower(tag);
                var el = new DomElement(tag, null, null, string.Empty);
                _elements.Add(el);
                return ToJSObject(el);
            }, "createElement", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createTextNode(text)
        document.FastAddValue(
            (KeyString)"createTextNode",
            new JSFunction((in Arguments a) =>
            {
                var text = a.Length > 0 ? a[0].ToString() : string.Empty;
                var el = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                el.TextContent = text;
                _elements.Add(el);
                return ToJSObject(el);
            }, "createTextNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createDocumentFragment() — basic iframe/fragment support
        document.FastAddValue(
            (KeyString)"createDocumentFragment",
            new JSFunction((in Arguments a) =>
            {
                var fragment = new DomElement("#document-fragment", null, null, string.Empty);
                _elements.Add(fragment);
                return ToJSObject(fragment);
            }, "createDocumentFragment", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createEvent(type) — DOM Events Level 3
        document.FastAddValue(
            (KeyString)"createEvent",
            new JSFunction((in Arguments a) =>
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
                evt.FastAddValue((KeyString)"stopPropagation",
                    new JSFunction((in Arguments _) =>
                    {
                        legacyCancelBubble = true;
                        return JSUndefined.Value;
                    }, "stopPropagation", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"stopImmediatePropagation",
                    new JSFunction((in Arguments _) =>
                    {
                        legacyCancelBubble = true;
                        return JSUndefined.Value;
                    }, "stopImmediatePropagation", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"preventDefault",
                    new JSFunction((in Arguments _) =>
                    {
                        var cancelable = evt[(KeyString)"cancelable"];
                        if (cancelable != null && cancelable.BooleanValue)
                            evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
                        return JSUndefined.Value;
                    }, "preventDefault", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddProperty(
                    (KeyString)"cancelBubble",
                    new JSFunction((in Arguments _) => legacyCancelBubble ? JSBoolean.True : JSBoolean.False, "get cancelBubble"),
                    new JSFunction((in Arguments setArgs) =>
                    {
                        if (setArgs.Length > 0 && setArgs[0].BooleanValue)
                            legacyCancelBubble = true;
                        return JSUndefined.Value;
                    }, "set cancelBubble"),
                    JSPropertyAttributes.EnumerableConfigurableProperty);
                evt.FastAddProperty(
                    (KeyString)"returnValue",
                    new JSFunction((in Arguments _) => evt[(KeyString)"defaultPrevented"].BooleanValue ? JSBoolean.False : JSBoolean.True, "get returnValue"),
                    new JSFunction((in Arguments setArgs) =>
                    {
                        var cancelable = evt[(KeyString)"cancelable"];
                        if (setArgs.Length > 0 &&
                            !setArgs[0].BooleanValue &&
                            cancelable != null &&
                            cancelable.BooleanValue)
                            evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
                        return JSUndefined.Value;
                    }, "set returnValue"),
                    JSPropertyAttributes.EnumerableConfigurableProperty);
                evt.FastAddValue((KeyString)"initEvent",
                    new JSFunction((in Arguments initArgs) =>
                    {
                        if (initArgs.Length > 0)
                            evt[(KeyString)"type"] = new JSString(initArgs[0].ToString());
                        if (initArgs.Length > 1)
                            evt[(KeyString)"bubbles"] = initArgs[1].BooleanValue ? JSBoolean.True : JSBoolean.False;
                        if (initArgs.Length > 2)
                            evt[(KeyString)"cancelable"] = initArgs[2].BooleanValue ? JSBoolean.True : JSBoolean.False;
                        return JSUndefined.Value;
                    }, "initEvent", 3),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"initUIEvent",
                    new JSFunction((in Arguments initArgs) =>
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
                    }, "initUIEvent", 5),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"initInputEvent",
                    new JSFunction((in Arguments initArgs) =>
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
                            evt[(KeyString)"data"] = initArgs[4];
                        if (initArgs.Length > 5)
                            evt[(KeyString)"inputType"] = new JSString(initArgs[5].ToString());
                        if (initArgs.Length > 6)
                            evt[(KeyString)"isComposing"] = initArgs[6].BooleanValue ? JSBoolean.True : JSBoolean.False;
                        return JSUndefined.Value;
                    }, "initInputEvent", 7),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"initCustomEvent",
                    new JSFunction((in Arguments initArgs) =>
                    {
                        if (initArgs.Length > 0)
                            evt[(KeyString)"type"] = new JSString(initArgs[0].ToString());
                        if (initArgs.Length > 1)
                            evt[(KeyString)"bubbles"] = initArgs[1].BooleanValue ? JSBoolean.True : JSBoolean.False;
                        if (initArgs.Length > 2)
                            evt[(KeyString)"cancelable"] = initArgs[2].BooleanValue ? JSBoolean.True : JSBoolean.False;
                        evt[(KeyString)"detail"] = initArgs.Length > 3
                            ? initArgs[3]
                            : JSNull.Value;
                        return JSUndefined.Value;
                    }, "initCustomEvent", 4),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"initFocusEvent",
                    new JSFunction((in Arguments initArgs) =>
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
                    }, "initFocusEvent", 6),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"initKeyboardEvent",
                    new JSFunction((in Arguments initArgs) =>
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
                    }, "initKeyboardEvent", 13),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"initMouseEvent",
                    new JSFunction((in Arguments initArgs) =>
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
                    }, "initMouseEvent", 15),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"initWheelEvent",
                    new JSFunction((in Arguments initArgs) =>
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
                            var modifiers = initArgs[11].ToString()
                                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            evt[(KeyString)"ctrlKey"] = Array.Exists(modifiers, m => string.Equals(m, "Control", StringComparison.OrdinalIgnoreCase))
                                ? JSBoolean.True
                                : JSBoolean.False;
                            evt[(KeyString)"altKey"] = Array.Exists(modifiers, m => string.Equals(m, "Alt", StringComparison.OrdinalIgnoreCase))
                                ? JSBoolean.True
                                : JSBoolean.False;
                            evt[(KeyString)"shiftKey"] = Array.Exists(modifiers, m => string.Equals(m, "Shift", StringComparison.OrdinalIgnoreCase))
                                ? JSBoolean.True
                                : JSBoolean.False;
                            evt[(KeyString)"metaKey"] = Array.Exists(modifiers, m => string.Equals(m, "Meta", StringComparison.OrdinalIgnoreCase))
                                ? JSBoolean.True
                                : JSBoolean.False;
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
                    }, "initWheelEvent", 16),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                return evt;
            }, "createEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Event / typed event constructors — DOM Level 4
        context.Eval(@"
                function Event(type, options) {
                    options = options || {};
                    var evt = document.createEvent('Event');
                    evt.initEvent(type, options.bubbles === true, options.cancelable === true);
                    return evt;
                }

                function CustomEvent(type, options) {
                    options = options || {};
                    var evt = document.createEvent('CustomEvent');
                    evt.initCustomEvent(
                        type,
                        options.bubbles === true,
                        options.cancelable === true,
                        options.detail !== undefined ? options.detail : null);
                    return evt;
                }

                function MouseEvent(type, options) {
                    options = options || {};
                    var evt = document.createEvent('MouseEvents');
                    evt.initMouseEvent(
                        type,
                        options.bubbles === true,
                        options.cancelable === true,
                        options.view !== undefined ? options.view : null,
                        options.detail !== undefined ? options.detail : 0,
                        options.screenX !== undefined ? options.screenX : 0,
                        options.screenY !== undefined ? options.screenY : 0,
                        options.clientX !== undefined ? options.clientX : 0,
                        options.clientY !== undefined ? options.clientY : 0,
                        options.ctrlKey === true,
                        options.altKey === true,
                        options.shiftKey === true,
                        options.metaKey === true,
                        options.button !== undefined ? options.button : 0,
                        options.relatedTarget !== undefined ? options.relatedTarget : null);
                    return evt;
                }

                function FocusEvent(type, options) {
                    options = options || {};
                    var evt = document.createEvent('FocusEvents');
                    evt.initFocusEvent(
                        type,
                        options.bubbles === true,
                        options.cancelable === true,
                        options.view !== undefined ? options.view : null,
                        options.detail !== undefined ? options.detail : 0,
                        options.relatedTarget !== undefined ? options.relatedTarget : null);
                    return evt;
                }

                function KeyboardEvent(type, options) {
                    options = options || {};
                    var evt = document.createEvent('KeyboardEvents');
                    evt.initKeyboardEvent(
                        type,
                        options.bubbles === true,
                        options.cancelable === true,
                        options.view !== undefined ? options.view : null,
                        options.key !== undefined ? options.key : '',
                        options.location !== undefined ? options.location : 0,
                        options.ctrlKey === true,
                        options.altKey === true,
                        options.shiftKey === true,
                        options.metaKey === true,
                        options.repeat === true,
                        options.keyCode !== undefined ? options.keyCode : 0,
                        options.charCode !== undefined ? options.charCode : 0);
                    return evt;
                }

                function WheelEvent(type, options) {
                    options = options || {};
                    var evt = document.createEvent('WheelEvents');
                    var modifiers = [];
                    if (options.ctrlKey === true) modifiers.push('Control');
                    if (options.altKey === true) modifiers.push('Alt');
                    if (options.shiftKey === true) modifiers.push('Shift');
                    if (options.metaKey === true) modifiers.push('Meta');
                    evt.initWheelEvent(
                        type,
                        options.bubbles === true,
                        options.cancelable === true,
                        options.view !== undefined ? options.view : null,
                        options.detail !== undefined ? options.detail : 0,
                        options.screenX !== undefined ? options.screenX : 0,
                        options.screenY !== undefined ? options.screenY : 0,
                        options.clientX !== undefined ? options.clientX : 0,
                        options.clientY !== undefined ? options.clientY : 0,
                        options.button !== undefined ? options.button : 0,
                        options.relatedTarget !== undefined ? options.relatedTarget : null,
                        modifiers.join(' '),
                        options.deltaX !== undefined ? options.deltaX : 0,
                        options.deltaY !== undefined ? options.deltaY : 0,
                        options.deltaZ !== undefined ? options.deltaZ : 0,
                        options.deltaMode !== undefined ? options.deltaMode : 0);
                    return evt;
                }

                function UIEvent(type, options) {
                    options = options || {};
                    var evt = document.createEvent('UIEvents');
                    evt.initUIEvent(
                        type,
                        options.bubbles === true,
                        options.cancelable === true,
                        options.view !== undefined ? options.view : null,
                        options.detail !== undefined ? options.detail : 0);
                    return evt;
                }

                function InputEvent(type, options) {
                    options = options || {};
                    var evt = document.createEvent('InputEvent');
                    evt.initInputEvent(
                        type,
                        options.bubbles === true,
                        options.cancelable === true,
                        options.view !== undefined ? options.view : null,
                        options.data !== undefined ? options.data : null,
                        options.inputType !== undefined ? options.inputType : '',
                        options.isComposing === true);
                    return evt;
                }
            ");

        // MutationObserver — DOM Level 4
        var mutationObservers = _mutationObservers;
        context.Eval(@"
                function MutationObserver(callback) {
                    this._callback = callback;
                    this._targets = [];
                    this._records = [];
                }
                MutationObserver.prototype.observe = function(target, options) {
                    this._targets.push({ target: target, options: options || {} });
                };
                MutationObserver.prototype.disconnect = function() {
                    this._targets = [];
                    this._records = [];
                };
                MutationObserver.prototype.takeRecords = function() {
                    var r = this._records.slice();
                    this._records = [];
                    return r;
                };
                MutationObserver.prototype._notify = function(records) {
                    if (records && records.length > 0) {
                        for (var i = 0; i < records.length; i++) {
                            this._records.push(records[i]);
                        }
                        var pending = this._records.slice();
                        this._records = [];
                        try { this._callback(pending, this); } catch(e) {}
                    }
                };
            ");

        // document.write(html) — parse and insert at the current script position
        document.FastAddValue(
            (KeyString)"write",
            new JSFunction((in Arguments a) =>
            {
                try
                {
                    if (a.Length == 0) return JSUndefined.Value;
                    var fragment = a[0].ToString();
                    var builder = new HtmlTreeBuilder();
                    var (docEl, allEls, _) = builder.Build($"<html><body>{fragment}</body></html>");
                    var bodyEl = docEl.Children.FirstOrDefault(c =>
                        string.Equals(c.TagName, "body", StringComparison.OrdinalIgnoreCase));
                    if (bodyEl != null)
                    {
                        // Find the <body> element in the main tree
                        var mainBody = DocumentElement.Children.FirstOrDefault(c =>
                            string.Equals(c.TagName, "body", StringComparison.OrdinalIgnoreCase));
                        if (mainBody != null)
                        {
                            // Find the currently executing <script> element so we can
                            // insert the new nodes right after it (matching real browser
                            // behaviour where document.write() inserts at the parser
                            // insertion point).
                            DomElement? currentScript = null;
                            if (CurrentScriptIndex >= 0 && CurrentScriptIndex < _elements.Count)
                            {
                                currentScript = _elements[CurrentScriptIndex];
                                // Verify it's a <script> in mainBody
                                if (currentScript.Parent != mainBody)
                                    currentScript = null;
                            }

                            if (currentScript != null)
                            {
                                var insertIdx = mainBody.Children.IndexOf(currentScript) + 1;
                                var children = new List<DomElement>(bodyEl.Children);
                                for (int ci = 0; ci < children.Count; ci++)
                                {
                                    children[ci].Parent = mainBody;
                                    mainBody.Children.Insert(insertIdx + ci, children[ci]);
                                }
                            }
                            else
                            {
                                // Fallback: append to end
                                foreach (var child in bodyEl.Children)
                                {
                                    child.Parent = mainBody;
                                    mainBody.Children.Add(child);
                                }
                            }
                            // Register the content elements (excluding wrapper html/body
                            // from the fragment parse) in document order so that
                            // getElementsByTagName, document.links, etc. return
                            // elements in the correct order relative to the rest of
                            // the document.
                            var contentEls = new List<DomElement>();
                            var topChildren = new List<DomElement>(bodyEl.Children);
                            foreach (var tc in topChildren)
                            {
                                contentEls.Add(tc);
                                CollectAllDescendantsFlat(tc, contentEls);
                            }
                            if (CurrentScriptIndex >= 0 && CurrentScriptIndex < _elements.Count)
                                _elements.InsertRange(CurrentScriptIndex + 1, contentEls);
                            else
                                _elements.AddRange(contentEls);
                        }
                    }
                    return JSUndefined.Value;
                }
                catch (Exception ex)
                {
                    RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.document.write",
                        $"Error in document.write: {ex.Message}", ex);
                    return JSUndefined.Value;
                }
            }, "write", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.writeln(html) — same as write, with trailing newline
        var writeFn = (JSFunction)document[(KeyString)"write"];
        document.FastAddValue(
            (KeyString)"writeln",
            new JSFunction((in Arguments a) =>
            {
                var text = a.Length > 0 ? a[0].ToString() + "\n" : "\n";
                return writeFn.InvokeFunction(new Arguments(writeFn, new JSString(text)));
            }, "writeln", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- Phase 2: NodeFilter, TreeWalker, NodeIterator, Range --

        // NodeFilter constants
        var nodeFilter = new JSObject();
        nodeFilter.FastAddValue((KeyString)"FILTER_ACCEPT", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"FILTER_REJECT", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"FILTER_SKIP", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ALL", new JSNumber(0xFFFFFFFF), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ELEMENT", new JSNumber(0x1), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ATTRIBUTE", new JSNumber(0x2), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_TEXT", new JSNumber(0x4), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_CDATA_SECTION", new JSNumber(0x8), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ENTITY_REFERENCE", new JSNumber(0x10), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ENTITY", new JSNumber(0x20), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_PROCESSING_INSTRUCTION", new JSNumber(0x40), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_COMMENT", new JSNumber(0x80), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_DOCUMENT", new JSNumber(0x100), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_DOCUMENT_TYPE", new JSNumber(0x200), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_DOCUMENT_FRAGMENT", new JSNumber(0x400), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_NOTATION", new JSNumber(0x800), JSPropertyAttributes.EnumerableConfigurableValue);
        context["NodeFilter"] = nodeFilter;

        // document.createTreeWalker(root, whatToShow, filter)
        var bridgeForTraversal = this;
        document.FastAddValue(
            (KeyString)"createTreeWalker",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    throw new JSException("Failed to execute 'createTreeWalker': 1 argument required.");
                var rootObj = a[0] as JSObject;
                if (rootObj == null)
                    throw new JSException("Failed to execute 'createTreeWalker': parameter 1 is not of type 'Node'.");
                var rootEl = bridgeForTraversal.FindDomElementByJSObject(rootObj);
                if (rootEl == null) return JSNull.Value;

                var whatToShow = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? unchecked((int)(uint)a[1].DoubleValue) : unchecked((int)0xFFFFFFFF);
                var filterFn = a.Length > 2 && a[2] is JSFunction f ? f : (a.Length > 2 && a[2] is JSObject filterObj ? filterObj[(KeyString)"acceptNode"] as JSFunction : null);

                return bridgeForTraversal.BuildTreeWalker(rootEl, whatToShow, filterFn);
            }, "createTreeWalker", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createNodeIterator(root, whatToShow, filter)
        document.FastAddValue(
            (KeyString)"createNodeIterator",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    throw new JSException("Failed to execute 'createNodeIterator': 1 argument required.");
                var rootObj = a[0] as JSObject;
                if (rootObj == null)
                    throw new JSException("Failed to execute 'createNodeIterator': parameter 1 is not of type 'Node'.");
                var rootEl = bridgeForTraversal.FindDomElementByJSObject(rootObj);
                if (rootEl == null) return JSNull.Value;

                var whatToShow = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? unchecked((int)(uint)a[1].DoubleValue) : unchecked((int)0xFFFFFFFF);
                var filterFn = a.Length > 2 && a[2] is JSFunction f ? f : (a.Length > 2 && a[2] is JSObject filterObj ? filterObj[(KeyString)"acceptNode"] as JSFunction : null);

                return bridgeForTraversal.BuildNodeIterator(rootEl, whatToShow, filterFn);
            }, "createNodeIterator", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createRange()
        document.FastAddValue(
            (KeyString)"createRange",
            new JSFunction((in Arguments a) => bridgeForTraversal.BuildRange(), "createRange", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createComment(data)
        document.FastAddValue(
            (KeyString)"createComment",
            new JSFunction((in Arguments a) =>
            {
                var data = a.Length > 0 ? a[0].ToString() : string.Empty;
                var el = new DomElement("#comment", null, null, string.Empty, isTextNode: false);
                el.TextContent = data;
                _elements.Add(el);
                return ToJSObject(el);
            }, "createComment", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Node type constants on document
        document.FastAddValue((KeyString)"ELEMENT_NODE", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"TEXT_NODE", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"COMMENT_NODE", new JSNumber(8), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"DOCUMENT_NODE", new JSNumber(9), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"DOCUMENT_TYPE_NODE", new JSNumber(10), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"DOCUMENT_FRAGMENT_NODE", new JSNumber(11), JSPropertyAttributes.EnumerableConfigurableValue);

        // document.nodeType = DOCUMENT_NODE (9)
        document.FastAddProperty(
            (KeyString)"nodeType",
            new JSFunction((in Arguments _) => new JSNumber(9), "get nodeType"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.nodeName = "#document"
        document.FastAddProperty(
            (KeyString)"nodeName",
            new JSFunction((in Arguments _) => new JSString("#document"), "get nodeName"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.firstChild (getter — returns first child of document: DOCTYPE if present, else documentElement)
        document.FastAddProperty(
            (KeyString)"firstChild",
            new JSFunction((in Arguments _) =>
            {
                return _documentNode.Children.Count > 0
                    ? ToJSObject(_documentNode.Children[0])
                    : JSNull.Value;
            }, "get firstChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.lastChild (getter — returns last child of document, typically documentElement)
        document.FastAddProperty(
            (KeyString)"lastChild",
            new JSFunction((in Arguments _) =>
            {
                return _documentNode.Children.Count > 0
                    ? ToJSObject(_documentNode.Children[^1])
                    : JSNull.Value;
            }, "get lastChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.childNodes (getter — returns children of document node: [DOCTYPE, documentElement])
        document.FastAddProperty(
            (KeyString)"childNodes",
            new JSFunction((in Arguments _) =>
            {
                var nodes = new List<JSValue>();
                foreach (var child in _documentNode.Children)
                    nodes.Add(ToJSObject(child));
                return new JSArray(nodes);
            }, "get childNodes"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.removeChild(child)
        var docNodeForMutation = _documentNode;
        document.FastAddValue(
            (KeyString)"removeChild",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var childObj = a[0] as JSObject;
                if (childObj == null) return JSNull.Value;
                var childEl = FindDomElementByJSObject(childObj);
                if (childEl != null)
                {
                    var idx = docNodeForMutation.Children.IndexOf(childEl);
                    if (idx >= 0)
                    {
                        NotifyNodeIteratorPreRemoval(childEl);
                        docNodeForMutation.Children.RemoveAt(idx);
                        childEl.Parent = null;
                        NotifyChildRemoved(docNodeForMutation, childEl, idx);
                    }
                }
                return a[0];
            }, "removeChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.appendChild(child)
        document.FastAddValue(
            (KeyString)"appendChild",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var childObj = a[0] as JSObject;
                if (childObj == null) return JSNull.Value;
                var childEl = FindDomElementByJSObject(childObj);
                if (childEl != null)
                {
                    childEl.Parent?.Children.Remove(childEl);
                    childEl.Parent = docNodeForMutation;
                    docNodeForMutation.Children.Add(childEl);
                }
                return a[0];
            }, "appendChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.insertBefore(newChild, refChild)
        document.FastAddValue(
            (KeyString)"insertBefore",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var newObj = a[0] as JSObject;
                if (newObj == null) return JSNull.Value;
                var newEl = FindDomElementByJSObject(newObj);
                if (newEl == null) return a[0];

                newEl.Parent?.Children.Remove(newEl);
                if (a.Length > 1 && a[1] is JSObject refObj && !a[1].IsNull)
                {
                    var refEl = FindDomElementByJSObject(refObj);
                    if (refEl != null)
                    {
                        var idx = docNodeForMutation.Children.IndexOf(refEl);
                        if (idx >= 0)
                        {
                            newEl.Parent = docNodeForMutation;
                            docNodeForMutation.Children.Insert(idx, newEl);
                            return a[0];
                        }
                    }
                }
                // If refChild is null or not found, append
                newEl.Parent = docNodeForMutation;
                docNodeForMutation.Children.Add(newEl);
                return a[0];
            }, "insertBefore", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.forms — collection of all <form> elements with named access
        document.FastAddProperty(
            (KeyString)"forms",
            new JSFunction((in Arguments _) =>
            {
                var results = new List<JSValue>();
                foreach (var el in _elements)
                {
                    if (string.Equals(el.TagName, "form", StringComparison.OrdinalIgnoreCase))
                        results.Add(ToJSObject(el));
                }
                var arr = new JSArray(results);
                // Add named access: forms with a 'name' attribute can be
                // accessed as properties of the collection.
                foreach (var el in _elements)
                {
                    if (string.Equals(el.TagName, "form", StringComparison.OrdinalIgnoreCase))
                    {
                        if (el.Attributes.TryGetValue("name", out var formName) && !string.IsNullOrEmpty(formName))
                            arr.FastAddValue((KeyString)formName, ToJSObject(el), JSPropertyAttributes.EnumerableConfigurableValue);
                    }
                }
                return arr;
            }, "get forms"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.createElementNS(namespace, tagName)
        document.FastAddValue(
            (KeyString)"createElementNS",
            new JSFunction((in Arguments a) =>
            {
                var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
                var localName = a.Length > 1 ? a[1].ToString() : (a.Length > 0 ? a[0].ToString() : "div");
                ValidateQualifiedName(localName, ns, context);
                var el = new DomElement(localName, null, null, string.Empty);
                if (!string.IsNullOrEmpty(ns))
                    el.NamespaceURI = ns;
                _elements.Add(el);
                return ToJSObject(el);
            }, "createElementNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.images — collection of all <img> elements
        document.FastAddProperty(
            (KeyString)"images",
            new JSFunction((in Arguments _) =>
            {
                var results = new List<JSValue>();
                foreach (var el in _elements)
                {
                    if (string.Equals(el.TagName, "img", StringComparison.OrdinalIgnoreCase))
                        results.Add(ToJSObject(el));
                }
                return new JSArray(results);
            }, "get images"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.links — collection of all <a> and <area> elements with href
        // Uses tree-order traversal instead of _elements insertion order
        // to correctly reflect dynamically appended elements.
        document.FastAddProperty(
            (KeyString)"links",
            new JSFunction((in Arguments _) =>
            {
                var results = new List<JSValue>();
                CollectLinksInTreeOrder(DocumentElement, results);
                return new JSArray(results);
            }, "get links"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.styleSheets — collection of stylesheet objects for main document
        document.FastAddProperty(
            (KeyString)"styleSheets",
            new JSFunction((in Arguments _) =>
            {
                var styleEls = new List<DomElement>();
                foreach (var el in _elements)
                {
                    if (string.Equals(el.TagName, "style", StringComparison.OrdinalIgnoreCase))
                        styleEls.Add(el);
                }
                var arr = new JSArray();
                foreach (var styleEl in styleEls)
                    arr.Add(BuildStyleSheetObject(styleEl));
                return arr;
            }, "get styleSheets"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.open() — for main document
        document.FastAddValue(
            (KeyString)"open",
            new JSFunction((in Arguments _) =>
            {
                // Main document open is a no-op in our implementation
                return document;
            }, "open", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.close() — for main document
        document.FastAddValue(
            (KeyString)"close",
            new JSFunction((in Arguments _) => JSUndefined.Value, "close", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.implementation — DOMImplementation
        var implementation = new JSObject();

        // implementation.hasFeature() — always returns true per spec
        implementation.FastAddValue(
            (KeyString)"hasFeature",
            new JSFunction((in Arguments _) => JSBoolean.True, "hasFeature", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // implementation.createDocumentType(qualifiedName, publicId, systemId)
        implementation.FastAddValue(
            (KeyString)"createDocumentType",
            new JSFunction((in Arguments a) =>
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
                var doctype = new DomElement("#doctype", null, null, string.Empty);
                doctype.DomProperties["name"] = qualifiedName;
                doctype.DomProperties["publicId"] = publicId;
                doctype.DomProperties["systemId"] = systemId;
                doctype.DomProperties["internalSubset"] = null;
                _elements.Add(doctype);
                return ToJSObject(doctype);
            }, "createDocumentType", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // implementation.createDocument(namespace, qualifiedName, doctype)
        implementation.FastAddValue(
            (KeyString)"createDocument",
            new JSFunction((in Arguments a) =>
            {
                var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
                var qName = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? a[1].ToString() : null;
                var doctypeArg = a.Length > 2 ? a[2] : null;

                if (!string.IsNullOrEmpty(qName))
                    ValidateQualifiedName(qName, ns, context);

                // Build a new document root
                var docRoot = new DomElement("#subdoc-root", null, null, string.Empty);
                docRoot.DomProperties["_hasViewport"] = false;
                _elements.Add(docRoot);

                // Append doctype if provided
                if (doctypeArg is JSObject dtObj)
                {
                    // Find the DomElement for the doctype JSObject
                    foreach (var kvp in _jsObjectCache)
                    {
                        if (kvp.Value == dtObj)
                        {
                            var dtEl = kvp.Key;
                            dtEl.Parent = docRoot;
                            dtEl.OwnerDocRoot = docRoot;
                            docRoot.Children.Add(dtEl);
                            break;
                        }
                    }
                }

                // Create document element if qualifiedName is provided
                if (!string.IsNullOrEmpty(qName))
                {
                    var docEl = new DomElement(qName, null, null, string.Empty);
                    if (!string.IsNullOrEmpty(ns))
                        docEl.NamespaceURI = ns;
                    docEl.Parent = docRoot;
                    docEl.OwnerDocRoot = docRoot;
                    docRoot.Children.Add(docEl);
                    _elements.Add(docEl);
                }

                return BuildSubDocument(docRoot);
            }, "createDocument", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // implementation.createHTMLDocument(title)
        implementation.FastAddValue(
            (KeyString)"createHTMLDocument",
            new JSFunction((in Arguments a) =>
            {
                var title = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;

                // Build a new HTML document root with html/head/body
                var docRoot = new DomElement("#subdoc-root", null, null, string.Empty);
                docRoot.DomProperties["_hasViewport"] = false;
                _elements.Add(docRoot);

                // Add DOCTYPE
                var doctype = new DomElement("#doctype", null, null, string.Empty);
                doctype.DomProperties["name"] = "html";
                doctype.DomProperties["publicId"] = string.Empty;
                doctype.DomProperties["systemId"] = string.Empty;
                doctype.DomProperties["internalSubset"] = null;
                doctype.Parent = docRoot;
                doctype.OwnerDocRoot = docRoot;
                docRoot.Children.Add(doctype);
                _elements.Add(doctype);

                var htmlEl = new DomElement("html", null, null, string.Empty);
                htmlEl.NamespaceURI = "http://www.w3.org/1999/xhtml";
                htmlEl.Parent = docRoot;
                htmlEl.OwnerDocRoot = docRoot;
                docRoot.Children.Add(htmlEl);
                _elements.Add(htmlEl);

                var headEl = new DomElement("head", null, null, string.Empty);
                headEl.Parent = htmlEl;
                headEl.OwnerDocRoot = docRoot;
                htmlEl.Children.Add(headEl);
                _elements.Add(headEl);

                // Add <title> element if title argument is provided
                if (title != null)
                {
                    var titleEl = new DomElement("title", null, null, string.Empty);
                    titleEl.Parent = headEl;
                    titleEl.OwnerDocRoot = docRoot;
                    headEl.Children.Add(titleEl);
                    _elements.Add(titleEl);

                    var titleText = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                    titleText.TextContent = title;
                    titleText.Parent = titleEl;
                    titleText.OwnerDocRoot = docRoot;
                    titleEl.Children.Add(titleText);
                    _elements.Add(titleText);
                }

                var bodyEl = new DomElement("body", null, null, string.Empty);
                bodyEl.Parent = htmlEl;
                bodyEl.OwnerDocRoot = docRoot;
                htmlEl.Children.Add(bodyEl);
                _elements.Add(bodyEl);

                return BuildSubDocument(docRoot);
            }, "createHTMLDocument", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        document.FastAddValue(
            (KeyString)"implementation",
            implementation,
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document-level addEventListener / removeEventListener / dispatchEvent
        var docNode = _documentNode;
        var bridgeRef = this;
        document.FastAddValue(
            (KeyString)"addEventListener",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSUndefined.Value;
                var type = a[0].ToString();
                var listener = a[1];
                if (!docNode.EventListeners.TryGetValue(type, out var listeners))
                {
                    listeners = [];
                    docNode.EventListeners[type] = listeners;
                }
                var registration = CreateEventListenerRegistration(listener, a.Length > 2 ? a[2] : JSUndefined.Value);
                if (!HasMatchingEventListener(listeners, registration))
                    listeners.Add(registration);
                return JSUndefined.Value;
            }, "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        document.FastAddValue(
            (KeyString)"removeEventListener",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSUndefined.Value;
                var type = a[0].ToString();
                var listener = a[1];
                var capture = GetCaptureForRemoval(a.Length > 2 ? a[2] : JSUndefined.Value);
                if (docNode.EventListeners.TryGetValue(type, out var listeners))
                {
                    for (int i = listeners.Count - 1; i >= 0; i--)
                    {
                        if (listeners[i].Listener == listener && listeners[i].Capture == capture)
                        {
                            listeners.RemoveAt(i);
                            break;
                        }
                    }
                }
                return JSUndefined.Value;
            }, "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        document.FastAddValue(
            (KeyString)"dispatchEvent",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSBoolean.True;
                var evt = a[0] as JSObject;
                if (evt == null) return JSBoolean.True;
                return bridgeRef.DispatchEventOnElement(docNode, evt);
            }, "dispatchEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.contentType — returns the MIME type of the document
        document.FastAddProperty(
            (KeyString)"contentType",
            new JSFunction((in Arguments _) =>
            {
                if (_pageUrl.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) ||
                    _pageUrl.EndsWith(".xht", StringComparison.OrdinalIgnoreCase) ||
                    _pageUrl.Contains("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
                    return new JSString("application/xhtml+xml");
                return new JSString("text/html");
            }, "get contentType"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.URL — returns the document URL
        document.FastAddProperty(
            (KeyString)"URL",
            new JSFunction((in Arguments _) => new JSString(_pageUrl), "get URL"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.documentURI — same as document.URL
        document.FastAddProperty(
            (KeyString)"documentURI",
            new JSFunction((in Arguments _) => new JSString(_pageUrl), "get documentURI"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.compatMode — "CSS1Compat" for standards mode, "BackCompat" for quirks
        document.FastAddProperty(
            (KeyString)"compatMode",
            new JSFunction((in Arguments _) => new JSString("CSS1Compat"), "get compatMode"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.characterSet — always UTF-8
        document.FastAddProperty(
            (KeyString)"characterSet",
            new JSFunction((in Arguments _) => new JSString("UTF-8"), "get characterSet"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.inputEncoding — alias for characterSet
        document.FastAddProperty(
            (KeyString)"inputEncoding",
            new JSFunction((in Arguments _) => new JSString("UTF-8"), "get inputEncoding"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        _documentJSObject = document;
        context["document"] = document;

        // window global
        var window = new JSObject();
        _windowJSObject = window;
        window.FastAddValue(
            (KeyString)"document",
            document,
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.localStorage — in-memory stub backed by a plain JSObject
        window.FastAddValue(
            (KeyString)"localStorage",
            BuildLocalStorageObject(),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.matchMedia(query) — evaluates basic media queries
        window.FastAddValue(
            (KeyString)"matchMedia",
            new JSFunction((in Arguments a) =>
            {
                var query = a.Length > 0 ? a[0].ToString() : string.Empty;
                var matches = !string.IsNullOrEmpty(query) && EvaluateMediaQuery(query, _viewportWidth, _viewportHeight);
                var result = new JSObject();
                result.FastAddValue(
                    (KeyString)"matches",
                    matches ? JSBoolean.True : JSBoolean.False,
                    JSPropertyAttributes.EnumerableConfigurableValue);
                result.FastAddValue(
                    (KeyString)"media",
                    new JSString(query),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                // addListener / removeListener stubs
                result.FastAddValue(
                    (KeyString)"addListener",
                    new JSFunction((in Arguments _) => JSUndefined.Value, "addListener", 1),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                result.FastAddValue(
                    (KeyString)"removeListener",
                    new JSFunction((in Arguments _) => JSUndefined.Value, "removeListener", 1),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                return result;
            }, "matchMedia", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.location (read-only)
        var location = new JSObject();
        location.FastAddValue((KeyString)"href", new JSString(_pageUrl), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"protocol", new JSString(_pageProtocol), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"host", new JSString(_pageHost), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"hostname", new JSString(_pageHostName), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"pathname", new JSString(_pagePathName), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"search", new JSString(_pageSearch), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"hash", new JSString(_pageHash), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"origin", new JSString(_pageOrigin), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"location",
            location,
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.setTimeout(fn, delay) — queues callback for deferred execution
        window.FastAddValue(
            (KeyString)"setTimeout",
            new JSFunction((in Arguments a) =>
            {
                var id = ++_timerIdCounter;
                if (a.Length > 0 && a[0] is JSFunction fn)
                {
                    _timeoutCallbacks[id] = fn;
                }
                return new JSNumber(id);
            }, "setTimeout", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.clearTimeout(id) — removes queued callback
        window.FastAddValue(
            (KeyString)"clearTimeout",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var id = (int)a[0].DoubleValue;
                    _timeoutCallbacks.Remove(id);
                    _clearedTimerIds.Add(id);
                }
                return JSUndefined.Value;
            }, "clearTimeout", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.setInterval(fn, delay) — queues repeating callback
        window.FastAddValue(
            (KeyString)"setInterval",
            new JSFunction((in Arguments a) =>
            {
                var id = ++_timerIdCounter;
                if (a.Length > 0 && a[0] is JSFunction fn)
                {
                    _intervalCallbacks[id] = fn;
                }
                return new JSNumber(id);
            }, "setInterval", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.clearInterval(id) — removes interval callback
        window.FastAddValue(
            (KeyString)"clearInterval",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var id = (int)a[0].DoubleValue;
                    _intervalCallbacks.Remove(id);
                    _clearedTimerIds.Add(id);
                }
                return JSUndefined.Value;
            }, "clearInterval", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.requestAnimationFrame(fn) — queues callback for pre-render execution
        window.FastAddValue(
            (KeyString)"requestAnimationFrame",
            new JSFunction((in Arguments a) =>
            {
                var id = ++_rafIdCounter;
                if (a.Length > 0 && a[0] is JSFunction fn)
                {
                    _rafCallbacks[id] = fn;
                }
                return new JSNumber(id);
            }, "requestAnimationFrame", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.cancelAnimationFrame(id) — removes queued rAF callback
        window.FastAddValue(
            (KeyString)"cancelAnimationFrame",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var id = (int)a[0].DoubleValue;
                    _rafCallbacks.Remove(id);
                }
                return JSUndefined.Value;
            }, "cancelAnimationFrame", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.alert(msg) — logs to debug output
        window.FastAddValue(
            (KeyString)"alert",
            new JSFunction((in Arguments a) =>
            {
                var msg = a.Length > 0 ? a[0].ToString() : string.Empty;
                RenderLogger.LogDebug(LogCategory.JavaScript, "DomBridge.alert", msg);
                return JSUndefined.Value;
            }, "alert", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // console object (shared between window.console and global console)
        var console = BuildConsoleObject();
        window.FastAddValue(
            (KeyString)"console",
            console,
            JSPropertyAttributes.EnumerableConfigurableValue);

        static IEnumerable<(string Key, string Value)> EnumerateObjectStringEntries(JSObject obj)
        {
            foreach (var (key, value) in obj.Entries)
            {
                if (string.IsNullOrEmpty(key) || key[0] == '_' || value is JSFunction || value.IsUndefined || value.IsNull)
                    continue;

                yield return (key, value.ToString());
            }
        }

        static string? TryGetJsPropertyString(JSObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                var value = obj[(KeyString)name];
                if (value != null && !value.IsUndefined && !value.IsNull)
                    return value.ToString();
            }

            return null;
        }

        static JSObject CreateThenable(Func<JSValue> resolver)
        {
            var thenable = new JSObject();
            thenable.FastAddValue((KeyString)"then", new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && a[0] is JSFunction cb)
                    cb.InvokeFunction(new Arguments(cb, resolver()));

                return thenable;
            }, "then", 1), JSPropertyAttributes.EnumerableConfigurableValue);

            return thenable;
        }

        static JSObject CreateHeadersObject(JSValue? initValue = null)
        {
            var headersObject = new JSObject();
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var originalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void SyncHeader(string name)
            {
                if (!values.TryGetValue(name, out var currentValue))
                    currentValue = string.Empty;

                var originalName = originalNames.TryGetValue(name, out var storedName) ? storedName : name;
                headersObject[(KeyString)originalName] = new JSString(currentValue);
                headersObject[(KeyString)name.ToLowerInvariant()] = new JSString(currentValue);
            }

            void SetHeader(string name, string value)
            {
                values[name] = value;
                originalNames[name] = name;
                SyncHeader(name);
            }

            void AppendHeader(string name, string value)
            {
                if (values.TryGetValue(name, out var existing) && !string.IsNullOrEmpty(existing))
                    values[name] = $"{existing}, {value}";
                else
                    values[name] = value;

                originalNames[name] = name;
                SyncHeader(name);
            }

            if (initValue is JSObject initObject)
            {
                foreach (var (key, value) in EnumerateObjectStringEntries(initObject))
                    AppendHeader(key, value);
            }

            headersObject.FastAddValue((KeyString)"get", new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    return JSNull.Value;

                var name = a[0].ToString();
                return values.TryGetValue(name, out var currentValue)
                    ? new JSString(currentValue)
                    : JSNull.Value;
            }, "get", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            headersObject.FastAddValue((KeyString)"has", new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    return JSBoolean.False;

                return values.ContainsKey(a[0].ToString()) ? JSBoolean.True : JSBoolean.False;
            }, "has", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            headersObject.FastAddValue((KeyString)"set", new JSFunction((in Arguments a) =>
            {
                if (a.Length >= 2)
                    SetHeader(a[0].ToString(), a[1].ToString());

                return JSUndefined.Value;
            }, "set", 2), JSPropertyAttributes.EnumerableConfigurableValue);
            headersObject.FastAddValue((KeyString)"append", new JSFunction((in Arguments a) =>
            {
                if (a.Length >= 2)
                    AppendHeader(a[0].ToString(), a[1].ToString());

                return JSUndefined.Value;
            }, "append", 2), JSPropertyAttributes.EnumerableConfigurableValue);
            headersObject.FastAddValue((KeyString)"delete", new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var name = a[0].ToString();
                    values.Remove(name);
                    originalNames.Remove(name);
                    headersObject[(KeyString)name] = JSUndefined.Value;
                    headersObject[(KeyString)name.ToLowerInvariant()] = JSUndefined.Value;
                }

                return JSUndefined.Value;
            }, "delete", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            headersObject.FastAddValue((KeyString)"forEach", new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && a[0] is JSFunction cb)
                {
                    foreach (var header in values)
                    {
                        var name = originalNames.TryGetValue(header.Key, out var originalName)
                            ? originalName
                            : header.Key;
                        cb.InvokeFunction(new Arguments(cb, new JSString(header.Value), new JSString(name), headersObject));
                    }
                }

                return JSUndefined.Value;
            }, "forEach", 1), JSPropertyAttributes.EnumerableConfigurableValue);

            return headersObject;
        }

        static JSValue ParseJsonText(string jsonText)
            => JSJSON.Parse(new Arguments(JSUndefined.Value, new JSString(jsonText)));

        static string DecodeFormComponent(string value)
            => Uri.UnescapeDataString(value.Replace("+", " "));

        static bool IsFormComponentUnescapedByte(byte value)
            => (value >= (byte)'a' && value <= (byte)'z')
               || (value >= (byte)'A' && value <= (byte)'Z')
               || (value >= (byte)'0' && value <= (byte)'9')
               || value is (byte)'*' or (byte)'-' or (byte)'.' or (byte)'_';

        static string EncodeFormComponent(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var builder = new StringBuilder(bytes.Length);
            foreach (var current in bytes)
            {
                if (current == (byte)' ')
                {
                    builder.Append('+');
                }
                else if (IsFormComponentUnescapedByte(current))
                {
                    builder.Append((char)current);
                }
                else
                {
                    builder.Append('%');
                    builder.Append(current.ToString("X2"));
                }
            }

            return builder.ToString();
        }

        static JSObject CreateFormDataObject(JSValue? initValue = null)
        {
            var formDataObject = new JSObject();
            var entries = new List<KeyValuePair<string, string>>();

            void AppendEntry(string name, string value)
                => entries.Add(new KeyValuePair<string, string>(name, value));

            void SetEntry(string name, string value)
            {
                var firstIndex = -1;
                for (var i = 0; i < entries.Count; i++)
                {
                    if (!string.Equals(entries[i].Key, name, StringComparison.Ordinal))
                        continue;

                    if (firstIndex < 0)
                    {
                        firstIndex = i;
                        entries[i] = new KeyValuePair<string, string>(name, value);
                    }
                    else
                    {
                        entries.RemoveAt(i);
                        i--;
                    }
                }

                if (firstIndex < 0)
                    entries.Add(new KeyValuePair<string, string>(name, value));
            }

            if (initValue != null && !initValue.IsUndefined && !initValue.IsNull)
            {
                if (initValue is JSObject initObject)
                {
                    foreach (var (key, value) in EnumerateObjectStringEntries(initObject))
                        AppendEntry(key, value);
                }
                else
                {
                    var initText = initValue.ToString();
                    if (!string.IsNullOrEmpty(initText))
                    {
                        foreach (var segment in initText.Split('&', StringSplitOptions.RemoveEmptyEntries))
                        {
                            var separatorIndex = segment.IndexOf('=');
                            var rawName = separatorIndex >= 0 ? segment[..separatorIndex] : segment;
                            var rawValue = separatorIndex >= 0 ? segment[(separatorIndex + 1)..] : string.Empty;
                            AppendEntry(DecodeFormComponent(rawName), DecodeFormComponent(rawValue));
                        }
                    }
                }
            }

            formDataObject.FastAddValue((KeyString)"append", new JSFunction((in Arguments a) =>
            {
                if (a.Length >= 2)
                    AppendEntry(a[0].ToString(), a[1].ToString());

                return JSUndefined.Value;
            }, "append", 2), JSPropertyAttributes.EnumerableConfigurableValue);
            formDataObject.FastAddValue((KeyString)"delete", new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var name = a[0].ToString();
                    entries.RemoveAll(entry => string.Equals(entry.Key, name, StringComparison.Ordinal));
                }

                return JSUndefined.Value;
            }, "delete", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            formDataObject.FastAddValue((KeyString)"forEach", new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && a[0] is JSFunction cb)
                {
                    foreach (var entry in entries)
                        cb.InvokeFunction(new Arguments(cb, new JSString(entry.Value), new JSString(entry.Key), formDataObject));
                }

                return JSUndefined.Value;
            }, "forEach", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            formDataObject.FastAddValue((KeyString)"get", new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    return JSNull.Value;

                var name = a[0].ToString();
                foreach (var entry in entries)
                {
                    if (string.Equals(entry.Key, name, StringComparison.Ordinal))
                        return new JSString(entry.Value);
                }

                return JSNull.Value;
            }, "get", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            formDataObject.FastAddValue((KeyString)"getAll", new JSFunction((in Arguments a) =>
            {
                var result = new JSArray();
                if (a.Length == 0)
                    return result;

                var name = a[0].ToString();
                foreach (var entry in entries)
                {
                    if (string.Equals(entry.Key, name, StringComparison.Ordinal))
                        result.Add(new JSString(entry.Value));
                }

                return result;
            }, "getAll", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            formDataObject.FastAddValue((KeyString)"has", new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    return JSBoolean.False;

                var name = a[0].ToString();
                return entries.Any(entry => string.Equals(entry.Key, name, StringComparison.Ordinal))
                    ? JSBoolean.True
                    : JSBoolean.False;
            }, "has", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            formDataObject.FastAddValue((KeyString)"set", new JSFunction((in Arguments a) =>
            {
                if (a.Length >= 2)
                    SetEntry(a[0].ToString(), a[1].ToString());

                return JSUndefined.Value;
            }, "set", 2), JSPropertyAttributes.EnumerableConfigurableValue);
            formDataObject.FastAddValue((KeyString)"toString", new JSFunction((in Arguments _) =>
                new JSString(string.Join("&", entries.Select(static entry => $"{EncodeFormComponent(entry.Key)}={EncodeFormComponent(entry.Value)}"))),
                "toString", 0), JSPropertyAttributes.EnumerableConfigurableValue);

            return formDataObject;
        }

        static JSValue CreateBlobBody(string bodyText, JSObject headersObject)
        {
            var contentType = TryGetJsPropertyString(headersObject, "content-type", "Content-Type") ?? string.Empty;
            var blobObject = new JSObject();
            blobObject[(KeyString)"size"] = new JSNumber(Encoding.UTF8.GetByteCount(bodyText));
            blobObject[(KeyString)"type"] = new JSString(contentType);
            blobObject.FastAddValue((KeyString)"text", new JSFunction((in Arguments _) =>
            {
                return CreateThenable(() => new JSString(bodyText));
            }, "text", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            blobObject.FastAddValue((KeyString)"arrayBuffer", new JSFunction((in Arguments _) =>
            {
                return CreateThenable(() => new JSArrayBuffer(Encoding.UTF8.GetBytes(bodyText)));
            }, "arrayBuffer", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            return blobObject;
        }

        JSObject CreateRequestObject(JSValue inputValue, JSValue? initValue = null)
        {
            string url;
            string method;
            string? body;
            JSObject headersObject;
            JSValue signalValue = JSUndefined.Value;

            if (inputValue is JSObject inputObject && !string.IsNullOrEmpty(TryGetJsPropertyString(inputObject, "url", "href")))
            {
                url = TryGetJsPropertyString(inputObject, "url", "href") ?? string.Empty;
                method = (TryGetJsPropertyString(inputObject, "method") ?? "GET").ToUpperInvariant();
                body = TryGetJsPropertyString(inputObject, "_bodyInit", "body");
                headersObject = inputObject[(KeyString)"headers"] is JSObject inputHeaders
                    ? CreateHeadersObject(inputHeaders)
                    : CreateHeadersObject();
                signalValue = inputObject[(KeyString)"signal"] ?? JSUndefined.Value;
            }
            else
            {
                url = inputValue.ToString();
                method = "GET";
                body = null;
                headersObject = CreateHeadersObject();
            }

            if (initValue is JSObject initObject)
            {
                method = (TryGetJsPropertyString(initObject, "method") ?? method).ToUpperInvariant();
                if (TryGetJsPropertyString(initObject, "body") is string initBody)
                    body = initBody;
                if (initObject[(KeyString)"headers"] is JSObject initHeaders)
                    headersObject = CreateHeadersObject(initHeaders);
                if (initObject[(KeyString)"signal"] is { } initSignal && !initSignal.IsUndefined && !initSignal.IsNull)
                    signalValue = initSignal;
            }

            var requestObject = new JSObject();
            requestObject[(KeyString)"url"] = new JSString(url);
            requestObject[(KeyString)"method"] = new JSString(method);
            requestObject[(KeyString)"headers"] = headersObject;
            requestObject[(KeyString)"bodyUsed"] = JSBoolean.False;
            requestObject[(KeyString)"_bodyInit"] = body == null ? JSNull.Value : new JSString(body);
            requestObject[(KeyString)"signal"] = signalValue;
            requestObject.FastAddValue((KeyString)"clone", new JSFunction((in Arguments _) =>
            {
                if (requestObject[(KeyString)"bodyUsed"].BooleanValue)
                    throw new JSException("Failed to execute 'clone' on 'Request': body is already used.");

                return CreateRequestObject(requestObject);
            }, "clone", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            requestObject.FastAddValue((KeyString)"text", new JSFunction((in Arguments _) =>
            {
                if (requestObject[(KeyString)"bodyUsed"].BooleanValue)
                    throw new JSException("Failed to execute body reader on 'Request': body is already used.");

                return CreateThenable(() =>
                {
                    requestObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                    return body == null ? new JSString(string.Empty) : new JSString(body);
                });
            }, "text", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            requestObject.FastAddValue((KeyString)"json", new JSFunction((in Arguments _) =>
            {
                if (requestObject[(KeyString)"bodyUsed"].BooleanValue)
                    throw new JSException("Failed to execute body reader on 'Request': body is already used.");

                return CreateThenable(() =>
                {
                    requestObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                    return ParseJsonText(body ?? string.Empty);
                });
            }, "json", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            requestObject.FastAddValue((KeyString)"arrayBuffer", new JSFunction((in Arguments _) =>
            {
                if (requestObject[(KeyString)"bodyUsed"].BooleanValue)
                    throw new JSException("Failed to execute body reader on 'Request': body is already used.");

                return CreateThenable(() =>
                {
                    requestObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                    return new JSArrayBuffer(Encoding.UTF8.GetBytes(body ?? string.Empty));
                });
            }, "arrayBuffer", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            requestObject.FastAddValue((KeyString)"blob", new JSFunction((in Arguments _) =>
            {
                if (requestObject[(KeyString)"bodyUsed"].BooleanValue)
                    throw new JSException("Failed to execute body reader on 'Request': body is already used.");

                return CreateThenable(() =>
                {
                    requestObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                    return CreateBlobBody(body ?? string.Empty, headersObject);
                });
            }, "blob", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            requestObject.FastAddValue((KeyString)"formData", new JSFunction((in Arguments _) =>
            {
                if (requestObject[(KeyString)"bodyUsed"].BooleanValue)
                    throw new JSException("Failed to execute body reader on 'Request': body is already used.");

                return CreateThenable(() =>
                {
                    requestObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                    return CreateFormDataObject(new JSString(body ?? string.Empty));
                });
            }, "formData", 0), JSPropertyAttributes.EnumerableConfigurableValue);

            return requestObject;
        }

        JSValue CreateResponse(string body, int statusCode, string statusText, string responseUrl, string type, bool redirected, Dictionary<string, string> headers)
        {
            var responseHeaders = new JSObject();
            foreach (var header in headers)
                responseHeaders[(KeyString)header.Key] = new JSString(header.Value);

            var headersObject = CreateHeadersObject(responseHeaders);
            var responseObject = new JSObject();
            responseObject[(KeyString)"ok"] = statusCode >= 200 && statusCode < 300 ? JSBoolean.True : JSBoolean.False;
            responseObject[(KeyString)"status"] = new JSNumber(statusCode);
            responseObject[(KeyString)"statusText"] = new JSString(statusText);
            responseObject[(KeyString)"url"] = new JSString(responseUrl);
            responseObject[(KeyString)"redirected"] = redirected ? JSBoolean.True : JSBoolean.False;
            responseObject[(KeyString)"type"] = new JSString(type);
            responseObject[(KeyString)"bodyUsed"] = JSBoolean.False;
            responseObject[(KeyString)"headers"] = headersObject;
            responseObject[(KeyString)"_bodyText"] = new JSString(body);
            responseObject.FastAddValue((KeyString)"text", new JSFunction((in Arguments _) =>
            {
                if (responseObject[(KeyString)"bodyUsed"].BooleanValue)
                    throw new JSException("Failed to execute body reader on 'Response': body is already used.");

                return CreateThenable(() =>
                {
                    responseObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                    return new JSString(body);
                });
            }, "text", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            responseObject.FastAddValue((KeyString)"json", new JSFunction((in Arguments _) =>
            {
                if (responseObject[(KeyString)"bodyUsed"].BooleanValue)
                    throw new JSException("Failed to execute body reader on 'Response': body is already used.");

                return CreateThenable(() =>
                {
                    responseObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                    return ParseJsonText(body);
                });
            }, "json", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            responseObject.FastAddValue((KeyString)"arrayBuffer", new JSFunction((in Arguments _) =>
            {
                if (responseObject[(KeyString)"bodyUsed"].BooleanValue)
                    throw new JSException("Failed to execute body reader on 'Response': body is already used.");

                return CreateThenable(() =>
                {
                    responseObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                    return new JSArrayBuffer(Encoding.UTF8.GetBytes(body));
                });
            }, "arrayBuffer", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            responseObject.FastAddValue((KeyString)"blob", new JSFunction((in Arguments _) =>
            {
                if (responseObject[(KeyString)"bodyUsed"].BooleanValue)
                    throw new JSException("Failed to execute body reader on 'Response': body is already used.");

                return CreateThenable(() =>
                {
                    responseObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                    return CreateBlobBody(body, headersObject);
                });
            }, "blob", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            responseObject.FastAddValue((KeyString)"formData", new JSFunction((in Arguments _) =>
            {
                if (responseObject[(KeyString)"bodyUsed"].BooleanValue)
                    throw new JSException("Failed to execute body reader on 'Response': body is already used.");

                return CreateThenable(() =>
                {
                    responseObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                    return CreateFormDataObject(new JSString(body));
                });
            }, "formData", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            responseObject.FastAddValue((KeyString)"clone", new JSFunction((in Arguments _) =>
            {
                if (responseObject[(KeyString)"bodyUsed"].BooleanValue)
                    throw new JSException("Failed to execute 'clone' on 'Response': body is already used.");

                return CreateResponse(body, statusCode, statusText, responseUrl, type, redirected, new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase));
            }, "clone", 0), JSPropertyAttributes.EnumerableConfigurableValue);

            return responseObject;
        }

        static JSValue CreateAbortErrorValue(JSValue signalValue)
        {
            if (signalValue is JSObject signalObject)
            {
                var reason = signalObject[(KeyString)"reason"];
                if (reason != null && !reason.IsUndefined && !reason.IsNull)
                    return reason;
            }

            var error = new JSObject();
            error[(KeyString)"name"] = new JSString("AbortError");
            error[(KeyString)"message"] = new JSString("The operation was aborted.");
            return error;
        }

        var formDataCtor = new JSFunction((in Arguments a) => CreateFormDataObject(a.Length > 0 ? a[0] : null), "FormData", 1);
        var headersCtor = new JSFunction((in Arguments a) => CreateHeadersObject(a.Length > 0 ? a[0] : null), "Headers", 1);
        var requestCtor = new JSFunction((in Arguments a) => CreateRequestObject(a.Length > 0 ? a[0] : JSUndefined.Value, a.Length > 1 ? a[1] : null), "Request", 2);
        var responseCtor = new JSFunction((in Arguments a) =>
        {
            var body = a.Length > 0 && !a[0].IsUndefined && !a[0].IsNull ? a[0].ToString() : string.Empty;
            var status = 200;
            var statusText = string.Empty;
            var url = string.Empty;
            var type = "basic";
            var redirected = false;
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (a.Length > 1 && a[1] is JSObject initObject)
            {
                if (TryGetJsPropertyString(initObject, "status") is string statusValue && int.TryParse(statusValue, out var parsedStatus))
                    status = parsedStatus;
                statusText = TryGetJsPropertyString(initObject, "statusText") ?? string.Empty;
                url = TryGetJsPropertyString(initObject, "url") ?? string.Empty;
                type = TryGetJsPropertyString(initObject, "type") ?? "basic";
                redirected = string.Equals(TryGetJsPropertyString(initObject, "redirected"), "true", StringComparison.OrdinalIgnoreCase);

                if (initObject[(KeyString)"headers"] is JSObject initHeaders)
                {
                    foreach (var (key, value) in EnumerateObjectStringEntries(initHeaders))
                        headers[key] = value;
                }
            }

            return CreateResponse(body, status, statusText, url, type, redirected, headers);
        }, "Response", 2);
        var messageChannelCtor = new JSFunction((in Arguments _) => CreateMessageChannel(), "MessageChannel", 0);
        window.FastAddValue((KeyString)"FormData", formDataCtor, JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"Headers", headersCtor, JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"Request", requestCtor, JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"Response", responseCtor, JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"MessageChannel", messageChannelCtor, JSPropertyAttributes.EnumerableConfigurableValue);
        context["FormData"] = formDataCtor;
        context["Headers"] = headersCtor;
        context["Request"] = requestCtor;
        context["Response"] = responseCtor;
        context["MessageChannel"] = messageChannelCtor;

        // fetch(url, options) — polyfill backed by HttpClient with headers, method support
        var fetchFn = new JSFunction((in Arguments a) =>
        {
            if (a.Length == 0)
                throw new JSException("Failed to execute 'fetch': 1 argument required.");

            var fetchUrl = a[0].ToString();
            if (a[0] is JSObject requestInput)
            {
                fetchUrl = TryGetJsPropertyString(requestInput, "url", "href") ?? fetchUrl;
            }

            JSValue responseObj = new JSObject();

            // Parse options (method, headers, body)
            var method = "GET";
            string? requestBody = null;
            var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            JSValue signalValue = JSUndefined.Value;

            if (a[0] is JSObject requestObject)
            {
                method = (TryGetJsPropertyString(requestObject, "method") ?? method).ToUpperInvariant();
                requestBody = TryGetJsPropertyString(requestObject, "_bodyInit", "body");
                if (requestObject[(KeyString)"signal"] is { } requestSignal && !requestSignal.IsUndefined && !requestSignal.IsNull)
                    signalValue = requestSignal;
                if (requestObject[(KeyString)"headers"] is JSObject requestHeadersObject)
                {
                    foreach (var (key, value) in EnumerateObjectStringEntries(requestHeadersObject))
                        requestHeaders[key] = value;
                }
            }

            if (a.Length > 1 && a[1] is JSObject opts)
            {
                method = (TryGetJsPropertyString(opts, "method") ?? method).ToUpperInvariant();
                requestBody = TryGetJsPropertyString(opts, "body") ?? requestBody;
                if (opts[(KeyString)"signal"] is { } optionsSignal && !optionsSignal.IsUndefined && !optionsSignal.IsNull)
                    signalValue = optionsSignal;
                if (opts[(KeyString)"headers"] is JSObject optionsHeadersObject)
                {
                    foreach (var (key, value) in EnumerateObjectStringEntries(optionsHeadersObject))
                        requestHeaders[key] = value;
                }
            }

            var rejected = false;
            var rejectedValue = JSUndefined.Value;

            if (signalValue is JSObject signalObject && signalObject[(KeyString)"aborted"].BooleanValue)
            {
                rejected = true;
                rejectedValue = CreateAbortErrorValue(signalValue);
            }

            try
            {
                if (!rejected)
                {
                    var request = new HttpRequestMessage(new HttpMethod(method), fetchUrl);
                    if (requestBody != null)
                        request.Content = new StringContent(requestBody, Encoding.UTF8,
                            requestHeaders.TryGetValue("Content-Type", out var ct) ? ct : "text/plain");
                    foreach (var kv in requestHeaders)
                    {
                        if (!string.Equals(kv.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                            request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                    }

                    var response = SharedHttpClient.SendAsync(request).GetAwaiter().GetResult();
                    var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var statusCode = (int)response.StatusCode;

                    var allHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var h in response.Headers)
                        allHeaders[h.Key] = string.Join(", ", h.Value);
                    if (response.Content.Headers != null)
                    {
                        foreach (var h in response.Content.Headers)
                            allHeaders[h.Key] = string.Join(", ", h.Value);
                    }
                    responseObj = CreateResponse(
                        body,
                        statusCode,
                        response.ReasonPhrase ?? string.Empty,
                        fetchUrl,
                        "basic",
                        false,
                        allHeaders);
                }
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.fetch", $"Fetch error: {ex.Message}", ex);
                responseObj = CreateResponse(
                    string.Empty,
                    0,
                    ex.Message,
                    fetchUrl,
                    "error",
                    false,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            }

            // Return a thenable (Promise-like) that resolves immediately
            var promise = new JSObject();
            promise.FastAddValue((KeyString)"then", new JSFunction((in Arguments thenArgs) =>
            {
                if (!rejected && thenArgs.Length > 0 && thenArgs[0] is JSFunction cb)
                {
                    try { cb.InvokeFunction(new Arguments(cb, responseObj)); }
                    catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.fetch.then", $"Callback error: {ex.Message}", ex); }
                }
                return promise;
            }, "then", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            promise.FastAddValue((KeyString)"catch", new JSFunction((in Arguments catchArgs) =>
            {
                if (rejected && catchArgs.Length > 0 && catchArgs[0] is JSFunction cb)
                {
                    try { cb.InvokeFunction(new Arguments(cb, rejectedValue)); }
                    catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.fetch.catch", $"Callback error: {ex.Message}", ex); }
                }
                return promise;
            }, "catch", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            return promise;
        }, "fetch", 1);

        window.FastAddValue((KeyString)"fetch", fetchFn, JSPropertyAttributes.EnumerableConfigurableValue);

        // window.getComputedStyle(element, pseudoElement)
        var bridgeForStyle = this;
        window.FastAddValue(
            (KeyString)"getComputedStyle",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return new JSObject();
                var targetObj = a[0] as JSObject;
                var el = targetObj != null ? bridgeForStyle.FindDomElementByJSObject(targetObj) : null;
                var pseudoElement = a.Length > 1 ? a[1]?.ToString() : null;
                return bridgeForStyle.BuildComputedStyleObject(el, pseudoElement);
            }, "getComputedStyle", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // XMLHttpRequest — basic polyfill backed by HttpClient
        RegisterXMLHttpRequest(context);

        context["window"] = window;
        window.FastAddValue((KeyString)"Event", context["Event"], JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"CustomEvent", context["CustomEvent"], JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"MouseEvent", context["MouseEvent"], JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"FocusEvent", context["FocusEvent"], JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"KeyboardEvent", context["KeyboardEvent"], JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"WheelEvent", context["WheelEvent"], JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"UIEvent", context["UIEvent"], JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"InputEvent", context["InputEvent"], JSPropertyAttributes.EnumerableConfigurableValue);

        // window.parent — uses the JSContext global scope so that parent.X()
        // resolves user-defined globals (e.g. parent.notify() from sub-documents).
        var globalThis = context.Eval("this");
        window.FastAddValue(
            (KeyString)"parent",
            globalThis,
            JSPropertyAttributes.EnumerableConfigurableValue);
        context["parent"] = globalThis;

        // window.self — refers to this window
        window.FastAddValue(
            (KeyString)"self",
            window,
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.defaultView — returns the window object
        document.FastAddValue(
            (KeyString)"defaultView",
            window,
            JSPropertyAttributes.EnumerableConfigurableValue);

        context["console"] = console;
        context["fetch"] = fetchFn;

        // Expose timer functions as globals (matching window.* counterparts)
        context["setTimeout"] = window[(KeyString)"setTimeout"];
        context["clearTimeout"] = window[(KeyString)"clearTimeout"];
        context["setInterval"] = window[(KeyString)"setInterval"];
        context["clearInterval"] = window[(KeyString)"clearInterval"];
        context["requestAnimationFrame"] = window[(KeyString)"requestAnimationFrame"];
        context["cancelAnimationFrame"] = window[(KeyString)"cancelAnimationFrame"];

        // ---------------------------------------------------------------
        //  Google Search Compliance: Phase 1 (P0) — Critical polyfills
        // ---------------------------------------------------------------

        // TODO-G2: performance object with performance.now() and timeOrigin
        var performanceTimeOrigin = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var performanceObj = new JSObject();
        performanceObj.FastAddValue(
            (KeyString)"timeOrigin",
            new JSNumber(performanceTimeOrigin),
            JSPropertyAttributes.EnumerableConfigurableValue);
        performanceObj.FastAddValue(
            (KeyString)"now",
            new JSFunction((in Arguments _) =>
            {
                var elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - performanceTimeOrigin;
                return new JSNumber(elapsed);
            }, "now", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // performance.getEntriesByType() — stub returning empty array
        performanceObj.FastAddValue(
            (KeyString)"getEntriesByType",
            new JSFunction((in Arguments _) => new JSArray(), "getEntriesByType", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // performance.mark() / performance.measure() — no-op stubs
        performanceObj.FastAddValue(
            (KeyString)"mark",
            new JSFunction((in Arguments _) => JSUndefined.Value, "mark", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        performanceObj.FastAddValue(
            (KeyString)"measure",
            new JSFunction((in Arguments _) => JSUndefined.Value, "measure", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"performance",
            performanceObj,
            JSPropertyAttributes.EnumerableConfigurableValue);
        context["performance"] = performanceObj;

        // TODO-G3: navigator object with sendBeacon, userAgent, language, etc.
        var navigatorObj = new JSObject();
        navigatorObj.FastAddValue(
            (KeyString)"userAgent",
            new JSString("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Broiler/1.0"),
            JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue(
            (KeyString)"language",
            new JSString("en-US"),
            JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue(
            (KeyString)"languages",
            new JSArray(new JSValue[] { new JSString("en-US"), new JSString("en") }),
            JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue(
            (KeyString)"cookieEnabled",
            JSBoolean.True,
            JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue(
            (KeyString)"onLine",
            JSBoolean.True,
            JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue(
            (KeyString)"platform",
            new JSString("Win32"),
            JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue(
            (KeyString)"vendor",
            new JSString(""),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // sendBeacon(url, data) — no-op, returns true
        navigatorObj.FastAddValue(
            (KeyString)"sendBeacon",
            new JSFunction((in Arguments _) => JSBoolean.True, "sendBeacon", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"navigator",
            navigatorObj,
            JSPropertyAttributes.EnumerableConfigurableValue);
        context["navigator"] = navigatorObj;
        context["postMessage"] = window[(KeyString)"postMessage"];

        // TODO-G4: window.innerWidth / innerHeight
        var vpWidth = _viewportWidth;
        var vpHeight = _viewportHeight;
        window.FastAddProperty(
            (KeyString)"innerWidth",
            new JSFunction((in Arguments _) => new JSNumber(vpWidth), "get innerWidth"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty(
            (KeyString)"innerHeight",
            new JSFunction((in Arguments _) => new JSNumber(vpHeight), "get innerHeight"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty(
            (KeyString)"outerWidth",
            new JSFunction((in Arguments _) => new JSNumber(vpWidth), "get outerWidth"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty(
            (KeyString)"outerHeight",
            new JSFunction((in Arguments _) => new JSNumber(vpHeight), "get outerHeight"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty(
            (KeyString)"scrollX",
            new JSFunction((in Arguments _) => new JSNumber(GetElementScrollOffset(DocumentElement, vertical: false)), "get scrollX"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty(
            (KeyString)"scrollY",
            new JSFunction((in Arguments _) => new JSNumber(GetElementScrollOffset(DocumentElement, vertical: true)), "get scrollY"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty(
            (KeyString)"pageXOffset",
            new JSFunction((in Arguments _) => new JSNumber(GetElementScrollOffset(DocumentElement, vertical: false)), "get pageXOffset"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty(
            (KeyString)"pageYOffset",
            new JSFunction((in Arguments _) => new JSNumber(GetElementScrollOffset(DocumentElement, vertical: true)), "get pageYOffset"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddValue(
            (KeyString)"scroll",
            new JSFunction((in Arguments a) =>
            {
                var (left, top, behavior) = GetScrollArguments(a);
                SetElementScrollOffsetsWithBehavior(DocumentElement, left, top, clamp: false, behavior: behavior);
                return JSUndefined.Value;
            }, "scroll", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"scrollTo",
            new JSFunction((in Arguments a) =>
            {
                var (left, top, behavior) = GetScrollArguments(a);
                SetElementScrollOffsetsWithBehavior(DocumentElement, left, top, clamp: false, behavior: behavior);
                return JSUndefined.Value;
            }, "scrollTo", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"scrollBy",
            new JSFunction((in Arguments a) =>
            {
                var (left, top, behavior) = GetScrollArguments(a);
                SetElementScrollOffsetsWithBehavior(DocumentElement, left, top, relative: true, clamp: false, behavior: behavior);
                return JSUndefined.Value;
            }, "scrollBy", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"addEventListener",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSUndefined.Value;
                var type = a[0].ToString();
                var listener = a[1];
                if (!_windowEventListeners.TryGetValue(type, out var listeners))
                {
                    listeners = [];
                    _windowEventListeners[type] = listeners;
                }
                var registration = CreateEventListenerRegistration(listener, a.Length > 2 ? a[2] : JSUndefined.Value);
                if (!HasMatchingEventListener(listeners, registration))
                    listeners.Add(registration);
                return JSUndefined.Value;
            }, "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"removeEventListener",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSUndefined.Value;
                var type = a[0].ToString();
                var listener = a[1];
                var capture = GetCaptureForRemoval(a.Length > 2 ? a[2] : JSUndefined.Value);
                if (_windowEventListeners.TryGetValue(type, out var listeners))
                {
                    for (int i = listeners.Count - 1; i >= 0; i--)
                    {
                        if (listeners[i].Listener == listener && listeners[i].Capture == capture)
                        {
                            listeners.RemoveAt(i);
                            break;
                        }
                    }
                }
                return JSUndefined.Value;
            }, "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"dispatchEvent",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0 || a[0] is not JSObject evt)
                    return JSBoolean.True;
                return DispatchWindowEvent(evt);
            }, "dispatchEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        RegisterWindowMessaging(window);
        window.FastAddProperty(
            (KeyString)"frames",
            new JSFunction((in Arguments _) => BuildWindowFramesArray(), "get frames"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        context["frames"] = BuildWindowFramesArray();
        // window.screen — basic stub for screen dimensions
        var screenObj = new JSObject();
        screenObj.FastAddValue((KeyString)"width", new JSNumber(vpWidth), JSPropertyAttributes.EnumerableConfigurableValue);
        screenObj.FastAddValue((KeyString)"height", new JSNumber(vpHeight), JSPropertyAttributes.EnumerableConfigurableValue);
        screenObj.FastAddValue((KeyString)"availWidth", new JSNumber(vpWidth), JSPropertyAttributes.EnumerableConfigurableValue);
        screenObj.FastAddValue((KeyString)"availHeight", new JSNumber(vpHeight), JSPropertyAttributes.EnumerableConfigurableValue);
        screenObj.FastAddValue((KeyString)"colorDepth", new JSNumber(24), JSPropertyAttributes.EnumerableConfigurableValue);
        screenObj.FastAddValue((KeyString)"pixelDepth", new JSNumber(24), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"screen", screenObj, JSPropertyAttributes.EnumerableConfigurableValue);
        context["screen"] = screenObj;

        var visualViewport = new JSObject();
        _visualViewportJSObject = visualViewport;
        visualViewport.FastAddProperty(
            (KeyString)"width",
            new JSFunction((in Arguments _) => new JSNumber(GetVisualViewportWidth()), "get width"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        visualViewport.FastAddProperty(
            (KeyString)"height",
            new JSFunction((in Arguments _) => new JSNumber(GetVisualViewportHeight()), "get height"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        visualViewport.FastAddProperty(
            (KeyString)"scale",
            new JSFunction((in Arguments _) => new JSNumber(GetVisualViewportScale()), "get scale"),
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                    SetVisualViewportScale(a[0].DoubleValue);
                return JSUndefined.Value;
            }, "set scale"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
        visualViewport.FastAddProperty(
            (KeyString)"pageLeft",
            new JSFunction((in Arguments _) => new JSNumber(GetVisualViewportPageOffset(vertical: false)), "get pageLeft"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        visualViewport.FastAddProperty(
            (KeyString)"pageTop",
            new JSFunction((in Arguments _) => new JSNumber(GetVisualViewportPageOffset(vertical: true)), "get pageTop"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        visualViewport.FastAddValue(
            (KeyString)"addEventListener",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 1 &&
                    a[0].ToString().Equals("scroll", StringComparison.OrdinalIgnoreCase) &&
                    a[1] is JSFunction listener &&
                    !_visualViewportScrollListeners.Contains(listener))
                {
                    _visualViewportScrollListeners.Add(listener);
                }
                return JSUndefined.Value;
            }, "addEventListener", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        visualViewport.FastAddValue(
            (KeyString)"removeEventListener",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 1 &&
                    a[0].ToString().Equals("scroll", StringComparison.OrdinalIgnoreCase) &&
                    a[1] is JSFunction listener)
                {
                    _visualViewportScrollListeners.Remove(listener);
                }
                return JSUndefined.Value;
            }, "removeEventListener", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"visualViewport", visualViewport, JSPropertyAttributes.EnumerableConfigurableValue);
        context["visualViewport"] = visualViewport;

        // ---------------------------------------------------------------
        //  Google Search Compliance: Phase 2 (P1) — Content rendering
        // ---------------------------------------------------------------

        // TODO-G6: Image() constructor — returns stub object with src property
        context.Eval(@"
                function Image(width, height) {
                    this.src = '';
                    this.width = width || 0;
                    this.height = height || 0;
                    this.alt = '';
                    this.complete = false;
                    this.naturalWidth = 0;
                    this.naturalHeight = 0;
                    this.onload = null;
                    this.onerror = null;
                    this.addEventListener = function() {};
                    this.removeEventListener = function() {};
                }
            ");

        // TODO-G7: document.cookie — get/set stub (in-memory, non-persistent)
        var cookieStore = "";
        document.FastAddProperty(
            (KeyString)"cookie",
            new JSFunction((in Arguments _) => new JSString(cookieStore), "get cookie"),
            new JSFunction((in Arguments a) =>
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
            }, "set cookie"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // ---------------------------------------------------------------
        //  Google Search Compliance: Phase 3 (P2) — Fidelity polyfills
        // ---------------------------------------------------------------

        // TODO-G10: IntersectionObserver — stub that immediately invokes callback
        context.Eval(@"
                function IntersectionObserver(callback, options) {
                    this._callback = callback;
                    this._targets = [];
                }
                IntersectionObserver.prototype.observe = function(target) {
                    this._targets.push(target);
                    // Immediately report as intersecting
                    var entry = {
                        target: target,
                        isIntersecting: true,
                        intersectionRatio: 1.0,
                        boundingClientRect: { top: 0, left: 0, bottom: 0, right: 0, width: 0, height: 0 },
                        intersectionRect: { top: 0, left: 0, bottom: 0, right: 0, width: 0, height: 0 },
                        rootBounds: null,
                        time: 0
                    };
                    try { this._callback([entry], this); } catch(e) {}
                };
                IntersectionObserver.prototype.unobserve = function(target) {
                    this._targets = this._targets.filter(function(t) { return t !== target; });
                };
                IntersectionObserver.prototype.disconnect = function() {
                    this._targets = [];
                };
                IntersectionObserver.prototype.takeRecords = function() {
                    return [];
                };
            ");

        // TODO-G11: ResizeObserver — no-op stub
        context.Eval(@"
                function ResizeObserver(callback) {
                    this._callback = callback;
                }
                ResizeObserver.prototype.observe = function() {};
                ResizeObserver.prototype.unobserve = function() {};
                ResizeObserver.prototype.disconnect = function() {};
            ");

        // TODO-G13: TextEncoder / TextDecoder — basic UTF-8 stubs
        context.Eval(@"
                function TextEncoder() {
                    this.encoding = 'utf-8';
                }
                TextEncoder.prototype.encode = function(str) {
                    str = str || '';
                    var arr = [];
                    for (var i = 0; i < str.length; i++) {
                        var c = str.charCodeAt(i);
                        if (c < 0x80) {
                            arr.push(c);
                        } else if (c < 0x800) {
                            arr.push(0xC0 | (c >> 6));
                            arr.push(0x80 | (c & 0x3F));
                        } else if (c >= 0xD800 && c <= 0xDBFF && i + 1 < str.length) {
                            var next = str.charCodeAt(i + 1);
                            if (next >= 0xDC00 && next <= 0xDFFF) {
                                var cp = ((c - 0xD800) << 10) + (next - 0xDC00) + 0x10000;
                                arr.push(0xF0 | (cp >> 18));
                                arr.push(0x80 | ((cp >> 12) & 0x3F));
                                arr.push(0x80 | ((cp >> 6) & 0x3F));
                                arr.push(0x80 | (cp & 0x3F));
                                i++;
                            } else {
                                arr.push(0xEF); arr.push(0xBF); arr.push(0xBD);
                            }
                        } else {
                            arr.push(0xE0 | (c >> 12));
                            arr.push(0x80 | ((c >> 6) & 0x3F));
                            arr.push(0x80 | (c & 0x3F));
                        }
                    }
                    return new Uint8Array(arr);
                };
                TextEncoder.prototype.encodeInto = function(str, dest) {
                    var encoded = this.encode(str);
                    var len = Math.min(encoded.length, dest.length);
                    for (var i = 0; i < len; i++) dest[i] = encoded[i];
                    return { read: str.length, written: len };
                };

                function TextDecoder(encoding) {
                    this.encoding = (encoding || 'utf-8').toLowerCase();
                    this.fatal = false;
                    this.ignoreBOM = false;
                }
                TextDecoder.prototype.decode = function(input) {
                    if (!input || input.length === 0) return '';
                    var bytes = input instanceof Uint8Array ? input : new Uint8Array(input);
                    var result = '';
                    var len = bytes.length;
                    for (var i = 0; i < len; ) {
                        var b = bytes[i];
                        if (b < 0x80) {
                            result += String.fromCharCode(b);
                            i++;
                        } else if ((b & 0xE0) === 0xC0 && i + 1 < len) {
                            result += String.fromCharCode(((b & 0x1F) << 6) | (bytes[i+1] & 0x3F));
                            i += 2;
                        } else if ((b & 0xF0) === 0xE0 && i + 2 < len) {
                            result += String.fromCharCode(((b & 0x0F) << 12) | ((bytes[i+1] & 0x3F) << 6) | (bytes[i+2] & 0x3F));
                            i += 3;
                        } else if ((b & 0xF8) === 0xF0 && i + 3 < len) {
                            var cp = ((b & 0x07) << 18) | ((bytes[i+1] & 0x3F) << 12) | ((bytes[i+2] & 0x3F) << 6) | (bytes[i+3] & 0x3F);
                            cp -= 0x10000;
                            result += String.fromCharCode(0xD800 + (cp >> 10), 0xDC00 + (cp & 0x3FF));
                            i += 4;
                        } else {
                            result += '\uFFFD';
                            i++;
                        }
                    }
                    return result;
                };
            ");

        // TODO-G14: URL / URLSearchParams polyfills
        context.Eval(@"
                function URLSearchParams(init) {
                    this._params = [];
                    if (typeof init === 'string') {
                        var s = init.charAt(0) === '?' ? init.substring(1) : init;
                        var pairs = s.split('&');
                        for (var i = 0; i < pairs.length; i++) {
                            var kv = pairs[i].split('=');
                            if (kv[0]) this._params.push([decodeURIComponent(kv[0]), decodeURIComponent(kv[1] || '')]);
                        }
                    } else if (init && typeof init === 'object') {
                        var keys = Object.keys(init);
                        for (var j = 0; j < keys.length; j++) {
                            this._params.push([keys[j], String(init[keys[j]])]);
                        }
                    }
                }
                URLSearchParams.prototype.get = function(name) {
                    for (var i = 0; i < this._params.length; i++) {
                        if (this._params[i][0] === name) return this._params[i][1];
                    }
                    return null;
                };
                URLSearchParams.prototype.getAll = function(name) {
                    var r = [];
                    for (var i = 0; i < this._params.length; i++) {
                        if (this._params[i][0] === name) r.push(this._params[i][1]);
                    }
                    return r;
                };
                URLSearchParams.prototype.has = function(name) { return this.get(name) !== null; };
                URLSearchParams.prototype.set = function(name, value) {
                    var found = false;
                    for (var i = 0; i < this._params.length; i++) {
                        if (this._params[i][0] === name) {
                            if (!found) { this._params[i][1] = String(value); found = true; }
                            else { this._params.splice(i, 1); i--; }
                        }
                    }
                    if (!found) this._params.push([name, String(value)]);
                };
                URLSearchParams.prototype.append = function(name, value) {
                    this._params.push([name, String(value)]);
                };
                URLSearchParams.prototype['delete'] = function(name) {
                    this._params = this._params.filter(function(p) { return p[0] !== name; });
                };
                URLSearchParams.prototype.toString = function() {
                    return this._params.map(function(p) {
                        return encodeURIComponent(p[0]) + '=' + encodeURIComponent(p[1]);
                    }).join('&');
                };
                URLSearchParams.prototype.forEach = function(cb) {
                    for (var i = 0; i < this._params.length; i++) cb(this._params[i][1], this._params[i][0], this);
                };
            ");

        context.Eval(@"
                function URL(url, base) {
                    if (base) {
                        if (url.indexOf('://') === -1 && url.charAt(0) !== '/') {
                            var baseNoQuery = base.split('?')[0].split('#')[0];
                            var lastSlash = baseNoQuery.lastIndexOf('/');
                            url = baseNoQuery.substring(0, lastSlash + 1) + url;
                        } else if (url.charAt(0) === '/') {
                            var m = base.match(/^([a-zA-Z][a-zA-Z0-9+\-.]*:\/\/[^\/]+)/);
                            url = (m ? m[1] : '') + url;
                        }
                    }
                    var match = url.match(/^([a-zA-Z][a-zA-Z0-9+\-.]*):\/\/([^\/:]+)(:\d+)?(\/[^?#]*)?(\?[^#]*)?(#.*)?$/);
                    if (match) {
                        this.protocol = match[1] + ':';
                        this.hostname = match[2];
                        this.port = match[3] ? match[3].substring(1) : '';
                        this.host = this.hostname + (this.port ? ':' + this.port : '');
                        this.pathname = match[4] || '/';
                        this.search = match[5] || '';
                        this.hash = match[6] || '';
                        this.origin = this.protocol + '//' + this.host;
                        this.href = url;
                    } else {
                        this.href = url;
                        this.protocol = ''; this.hostname = ''; this.port = '';
                        this.host = ''; this.pathname = url; this.search = '';
                        this.hash = ''; this.origin = '';
                    }
                    this.searchParams = new URLSearchParams(this.search);
                }
                URL.prototype.toString = function() { return this.href; };
                URL.prototype.toJSON = function() { return this.href; };
            ");

        // ---------------------------------------------------------------
        //  Google Search Compliance: Phase 4 (P3) — Polish and edge cases
        // ---------------------------------------------------------------

        // TODO-G16: AbortController / AbortSignal — basic stubs
        context.Eval(@"
                function AbortController() {
                    this.signal = {
                        aborted: false,
                        reason: undefined,
                        onabort: null,
                        _listeners: [],
                        addEventListener: function(type, listener) {
                            if (type !== 'abort' || typeof listener !== 'function') return;
                            if (this._listeners.indexOf(listener) === -1) this._listeners.push(listener);
                        },
                        removeEventListener: function(type, listener) {
                            if (type !== 'abort') return;
                            var index = this._listeners.indexOf(listener);
                            if (index !== -1) this._listeners.splice(index, 1);
                        },
                        throwIfAborted: function() {
                            if (this.aborted) throw (this.reason !== undefined ? this.reason : new DOMException('The operation was aborted.', 'AbortError'));
                        }
                    };
                }
                AbortController.prototype.abort = function(reason) {
                    if (this.signal.aborted) return;
                    this.signal.aborted = true;
                    this.signal.reason = reason !== undefined ? reason : new DOMException('The operation was aborted.', 'AbortError');
                    var event = { type: 'abort', target: this.signal, currentTarget: this.signal };
                    if (typeof this.signal.onabort === 'function') {
                        try { this.signal.onabort(event); } catch(e) {}
                    }
                    var listeners = this.signal._listeners.slice();
                    for (var i = 0; i < listeners.length; i++) {
                        try { listeners[i].call(this.signal, event); } catch(e) {}
                    }
                };
            ");

        // TODO-G18: window.crypto.getRandomValues() — cryptographically secure
        var cryptoObj = new JSObject();
        cryptoObj.FastAddValue(
            (KeyString)"getRandomValues",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSUndefined.Value;
                var arr = a[0];
                if (arr is JSObject arrObj)
                {
                    var lengthProp = arrObj[(KeyString)"length"];
                    if (lengthProp != null && !lengthProp.IsUndefined && !lengthProp.IsNull)
                    {
                        var len = (int)lengthProp.DoubleValue;
                        var buffer = new byte[len];
                        System.Security.Cryptography.RandomNumberGenerator.Fill(buffer);
                        for (var i = 0; i < len; i++)
                            arrObj[(KeyString)i.ToString()] = new JSNumber(buffer[i]);
                    }
                }
                return arr;
            }, "getRandomValues", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        cryptoObj.FastAddValue(
            (KeyString)"randomUUID",
            new JSFunction((in Arguments _) => new JSString(Guid.NewGuid().ToString()), "randomUUID", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"crypto",
            cryptoObj,
            JSPropertyAttributes.EnumerableConfigurableValue);
        context["crypto"] = cryptoObj;

        // DOMException constructor
        RegisterDOMException(context);

        // Node constructor with type constants
        RegisterNodeConstructor(context);

        // SVGLength interface constants
        RegisterSVGLength(context);
    }

    /// <summary>
    /// Registers a basic <c>XMLHttpRequest</c> constructor on the context.
    /// Supports <c>open</c>, <c>send</c>, <c>setRequestHeader</c>,
    /// <c>onreadystatechange</c>, <c>readyState</c>, <c>status</c>, and <c>responseText</c>.
    /// </summary>
    private static void RegisterXMLHttpRequest(JSContext context) => context.Eval(@"
                function XMLHttpRequest() {
                    this.readyState = 0;
                    this.status = 0;
                    this.statusText = '';
                    this.response = null;
                    this.responseText = '';
                    this.responseType = '';
                    this.responseURL = '';
                    this.responseXML = null;
                    this.onreadystatechange = null;
                    this.onload = null;
                    this.onerror = null;
                    this.onabort = null;
                    this.onprogress = null;
                    this.onloadstart = null;
                    this.onloadend = null;
                    this.ontimeout = null;
                    this.withCredentials = false;
                    this.timeout = 0;
                    this._method = 'GET';
                    this._url = '';
                    this._async = true;
                    this._headers = {};
                    this._responseHeaders = {};
                    this._mimeOverride = null;
                    this._aborted = false;
                    this.UNSENT = 0;
                    this.OPENED = 1;
                    this.HEADERS_RECEIVED = 2;
                    this.LOADING = 3;
                    this.DONE = 4;
                }
                XMLHttpRequest.UNSENT = 0;
                XMLHttpRequest.OPENED = 1;
                XMLHttpRequest.HEADERS_RECEIVED = 2;
                XMLHttpRequest.LOADING = 3;
                XMLHttpRequest.DONE = 4;
                XMLHttpRequest.prototype.open = function(method, url, isAsync) {
                    this._method = method;
                    this._url = url;
                    this._async = isAsync !== false;
                    this.readyState = 1;
                    this.status = 0;
                    this.statusText = '';
                    this.response = null;
                    this.responseText = '';
                    this.responseXML = null;
                    this.responseURL = '';
                    this._responseHeaders = {};
                    this._aborted = false;
                    if (typeof this.onreadystatechange === 'function') {
                        this.onreadystatechange();
                    }
                };
                XMLHttpRequest.prototype.setRequestHeader = function(name, value) {
                    this._headers[name] = value;
                };
                XMLHttpRequest.prototype.getResponseHeader = function(name) {
                    if (!name) return null;
                    var lower = name.toLowerCase();
                    for (var key in this._responseHeaders) {
                        if (key.toLowerCase() === lower) return this._responseHeaders[key];
                    }
                    return null;
                };
                XMLHttpRequest.prototype.getAllResponseHeaders = function() {
                    var result = '';
                    for (var key in this._responseHeaders) {
                        result += key.toLowerCase() + ': ' + this._responseHeaders[key] + '\r\n';
                    }
                    return result;
                };
                XMLHttpRequest.prototype.overrideMimeType = function(mime) {
                    this._mimeOverride = mime;
                };
                XMLHttpRequest.prototype.abort = function() {
                    this._aborted = true;
                     this.readyState = 0;
                     this.status = 0;
                     this.statusText = '';
                     this.response = null;
                     this.responseText = '';
                     this.responseXML = null;
                     if (typeof this.onabort === 'function') {
                         this.onabort();
                     }
                     if (typeof this.onloadend === 'function') {
                         this.onloadend();
                    }
                };
                XMLHttpRequest.prototype.send = function(body) {
                    var self = this;
                    if (self._aborted) return;
                    try {
                        var opts = { method: self._method };
                        if (body && self._method !== 'GET' && self._method !== 'HEAD') {
                            opts.body = '' + body;
                        }
                        var hasHeaders = false;
                        for (var k in self._headers) { hasHeaders = true; break; }
                        if (hasHeaders) {
                            opts.headers = self._headers;
                        }
                        if (typeof self.onloadstart === 'function') {
                            self.onloadstart();
                        }
                        fetch(self._url, opts).then(function(response) {
                            if (self._aborted) return;
                            self.status = response.status;
                            self.statusText = response.statusText;
                            self.responseURL = response.url || self._url;
                            self.readyState = 2;
                            if (response.headers && typeof response.headers.forEach === 'function') {
                                response.headers.forEach(function(value, name) {
                                    self._responseHeaders[name] = value;
                                });
                            }
                            if (typeof self.onreadystatechange === 'function') {
                                self.onreadystatechange();
                            }
                            var bodyPromise;
                            if (self.responseType === 'arraybuffer' &&
                                response &&
                                typeof response.arrayBuffer === 'function') {
                                bodyPromise = response.arrayBuffer();
                            } else if (self.responseType === 'blob' &&
                                response &&
                                typeof response.blob === 'function') {
                                bodyPromise = response.blob();
                            } else if (self.responseType === 'json' &&
                                response &&
                                typeof response.json === 'function') {
                                bodyPromise = response.json();
                            } else {
                                bodyPromise = response.text();
                            }
                            bodyPromise.then(function(bodyValue) {
                                if (self._aborted) return;
                                if (self.responseType === '' || self.responseType === 'text') {
                                    // Per XHR semantics, the default/text response types expose
                                    // the same textual payload via both response and responseText.
                                    self.response = bodyValue;
                                    self.responseText = '' + bodyValue;
                                    self.responseXML = null;
                                } else if (self.responseType === 'document') {
                                    var responseDocument = document.implementation.createHTMLDocument('');
                                    responseDocument.body.innerHTML = '' + bodyValue;
                                    self.response = responseDocument;
                                    self.responseXML = responseDocument;
                                    self.responseText = '';
                                } else {
                                    self.response = bodyValue;
                                    self.responseText = '';
                                    self.responseXML = null;
                                }
                                 self.readyState = 3;
                                 if (typeof self.onprogress === 'function') {
                                     self.onprogress();
                                 }
                                self.readyState = 4;
                                if (typeof self.onreadystatechange === 'function') {
                                    self.onreadystatechange();
                                }
                                if (typeof self.onload === 'function') {
                                    self.onload();
                                }
                                if (typeof self.onloadend === 'function') {
                                    self.onloadend();
                                }
                            });
                        });
                    } catch(e) {
                        self.readyState = 4;
                        self.status = 0;
                        if (typeof self.onreadystatechange === 'function') {
                            self.onreadystatechange();
                        }
                        if (typeof self.onerror === 'function') {
                            self.onerror();
                        }
                        if (typeof self.onloadend === 'function') {
                            self.onloadend();
                        }
                    }
                };
            ");

    private JSArray BuildAnimationList(DomElement? target)
    {
        var animations = new List<JSValue>();
        foreach (var element in _elements)
        {
            if (element.IsTextNode || string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                continue;
            if (target != null && !ReferenceEquals(element, target))
                continue;

            if (!TryGetAnimationProperties(element, out var animationShorthand, out var animationDelay))
                continue;

            EnsureAnimationCurrentTime(element, animationShorthand, animationDelay);
            animations.Add(BuildAnimationObject(element));
        }

        return new JSArray(animations);
    }

    private bool TryGetAnimationProperties(
        DomElement element,
        out string? animationShorthand,
        out string? animationDelay)
    {
        animationShorthand = null;
        animationDelay = null;

        if (element.Style.TryGetValue("animation", out animationShorthand))
        {
            element.Style.TryGetValue("animation-delay", out animationDelay);
            return true;
        }

        var stylesheetProps = CollectStylesheetAnimationProperties(element);
        if (stylesheetProps == null)
            return false;

        var hasAnimation = stylesheetProps.TryGetValue("animation", out animationShorthand);
        stylesheetProps.TryGetValue("animation-delay", out animationDelay);
        return hasAnimation || stylesheetProps.ContainsKey("animation-name");
    }

    private void EnsureAnimationCurrentTime(
        DomElement element,
        string? animationShorthand,
        string? animationDelay)
    {
        if (element.DomProperties.ContainsKey("_animationCurrentTimeMs"))
            return;

        double delaySec = 0;
        if (!string.IsNullOrWhiteSpace(animationDelay) &&
            TryParseCssTime(animationDelay, out var delayOverride))
        {
            delaySec = delayOverride;
        }
        else if (!string.IsNullOrWhiteSpace(animationShorthand))
        {
            var durations = new List<double>();
            foreach (var part in TokenizeAnimationShorthand(animationShorthand))
            {
                if (TryParseCssTime(part, out var seconds))
                    durations.Add(seconds);
            }

            if (durations.Count >= 2)
                delaySec = durations[1];
        }

        var currentTimeMs = delaySec > 0 ? (delaySec * 1000.0) + 1.0 : Math.Abs(delaySec) * 1000.0;
        element.DomProperties["_animationCurrentTimeMs"] = currentTimeMs;
    }

    private static JSObject BuildAnimationObject(DomElement element)
    {
        var animation = new JSObject();
        animation.FastAddProperty(
            (KeyString)"currentTime",
            new JSFunction((in Arguments _) =>
            {
                if (element.DomProperties.TryGetValue("_animationCurrentTimeMs", out var value) &&
                    value is double currentTimeMs)
                {
                    return new JSNumber(currentTimeMs);
                }

                return new JSNumber(0);
            }, "get currentTime"),
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                    element.DomProperties["_animationCurrentTimeMs"] = a[0].DoubleValue;
                return JSUndefined.Value;
            }, "set currentTime"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        var ready = new JSObject();
        ready.FastAddValue(
            (KeyString)"then",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && a[0] is JSFunction fn)
                    fn.InvokeFunction(new Arguments(JSUndefined.Value, JSUndefined.Value));
                return ready;
            }, "then", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        ready.FastAddValue(
            (KeyString)"catch",
            new JSFunction((in Arguments _) => ready, "catch", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        animation.FastAddValue((KeyString)"ready", ready, JSPropertyAttributes.EnumerableConfigurableValue);
        return animation;
    }

    /// <summary>
    /// Builds a <c>console</c> object exposing <c>log</c>, <c>warn</c>,
    /// <c>error</c>, and <c>info</c>.
    /// </summary>
    private static JSObject BuildConsoleObject()
    {
        var console = new JSObject();

        console.FastAddValue(
            (KeyString)"log",
            new JSFunction((in Arguments a) =>
            {
                var parts = new List<string>();
                for (var i = 0; i < a.Length; i++)
                    parts.Add(a[i]?.ToString() ?? "undefined");
                RenderLogger.LogDebug(LogCategory.JavaScript, "console.log", string.Join(" ", parts));
                return JSUndefined.Value;
            }, "log"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        console.FastAddValue(
            (KeyString)"warn",
            new JSFunction((in Arguments a) =>
            {
                var parts = new List<string>();
                for (var i = 0; i < a.Length; i++)
                    parts.Add(a[i]?.ToString() ?? "undefined");
                RenderLogger.Log(LogCategory.JavaScript, LogLevel.Warning, "console.warn", string.Join(" ", parts));
                return JSUndefined.Value;
            }, "warn"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        console.FastAddValue(
            (KeyString)"error",
            new JSFunction((in Arguments a) =>
            {
                var parts = new List<string>();
                for (var i = 0; i < a.Length; i++)
                    parts.Add(a[i]?.ToString() ?? "undefined");
                RenderLogger.Log(LogCategory.JavaScript, LogLevel.Error, "console.error", string.Join(" ", parts));
                return JSUndefined.Value;
            }, "error"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        console.FastAddValue(
            (KeyString)"info",
            new JSFunction((in Arguments a) =>
            {
                var parts = new List<string>();
                for (var i = 0; i < a.Length; i++)
                    parts.Add(a[i]?.ToString() ?? "undefined");
                RenderLogger.LogDebug(LogCategory.JavaScript, "console.info", string.Join(" ", parts));
                return JSUndefined.Value;
            }, "info"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return console;
    }

}
