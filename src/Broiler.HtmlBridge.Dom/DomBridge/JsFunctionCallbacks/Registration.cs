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

    private JSValue JsRegistrationGetBody002Core(in Arguments a)
    {
        foreach (var child in DocumentElement.Children)
        {
            if (string.Equals(child.TagName, "body", StringComparison.OrdinalIgnoreCase))
                return ToJSObject(child);
        }

        return JSNull.Value;
    }


    private JSValue JsRegistrationGetHead003Core(in Arguments a)
    {
        foreach (var child in DocumentElement.Children)
        {
            if (string.Equals(child.TagName, "head", StringComparison.OrdinalIgnoreCase))
                return ToJSObject(child);
        }

        return JSNull.Value;
    }


    private JSValue JsRegistrationSetTitle005Core(in Arguments a)
    {
        Title = a.Length > 0 ? a[0].ToString() : string.Empty;
        return JSUndefined.Value;
    }


    private JSValue JsRegistrationGetElementById006Core(in Arguments a)
    {
        var id = a.Length > 0 ? a[0].ToString() : string.Empty;
        var found = FindInSubTree(DocumentElement, el => el.Id == id);
        return found != null ? ToJSObject(found) : JSNull.Value;
    }


    private JSValue JsRegistrationGetElementsByTagName007Core(in Arguments a)
    {
        var tag = a.Length > 0 ? a[0].ToString().ToLowerInvariant() : string.Empty;
        var results = new List<JSValue>();
        foreach (var el in Elements)
        {
            if (tag == "*" || el.TagName == tag)
                results.Add(ToJSObject(el));
        }

        return new JSArray(results);
    }


    private JSValue JsRegistrationGetElementsByClassName008Core(in Arguments a)
    {
        var className = a.Length > 0 ? a[0].ToString() : string.Empty;
        var results = new List<JSValue>();
        foreach (var el in Elements)
        {
            var classes = new HashSet<string>((el.ClassName ?? string.Empty).Split(' ').Where(s => s.Length > 0), StringComparer.Ordinal);
            if (classes.Contains(className))
                results.Add(ToJSObject(el));
        }

        return new JSArray(results);
    }


    private JSValue JsRegistrationQuerySelector009Core(in Arguments a)
    {
        var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
        foreach (var el in Elements)
        {
            if (MatchesSelector(el, selector))
                return ToJSObject(el);
        }

        return JSNull.Value;
    }


    private JSValue JsRegistrationQuerySelectorAll010Core(in Arguments a)
    {
        var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
        var results = new List<JSValue>();
        foreach (var el in Elements)
        {
            if (MatchesSelector(el, selector))
                results.Add(ToJSObject(el));
        }

        return new JSArray(results);
    }


    private JSValue JsRegistrationElementFromPoint011Core(in Arguments a)
    {
        var hit = HitTestDocumentPoint(DocumentElement, GetCoordinateArgument(a, 0), GetCoordinateArgument(a, 1)).FirstOrDefault();
        return hit != null ? ToJSObject(hit) : JSNull.Value;
    }


    private JSValue JsRegistrationElementsFromPoint012Core(in Arguments a)
    {
        var hits = HitTestDocumentPoint(DocumentElement, GetCoordinateArgument(a, 0), GetCoordinateArgument(a, 1));
        return new JSArray(hits.Select(ToJSObject).ToArray());
    }


    private JSValue JsRegistrationCreateElement014Core(global::Broiler.JavaScript.Engine.JSContext context, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'createElement': 1 argument required, but only 0 present.");
        var tag = a[0].ToString();
        ValidateElementName(tag, context);
        tag = AsciiToLower(tag);
        var el = new DomElement(_document, tag, null, null, string.Empty);
        _knownNodes.Add(el);
        return ToJSObject(el);
    }


    private JSValue JsRegistrationCreateTextNode015Core(in Arguments a)
    {
        var text = a.Length > 0 ? a[0].ToString() : string.Empty;
        var el = new DomElement(_document, "#text", null, null, string.Empty, isTextNode: true);
        el.TextContent = text;
        _knownNodes.Add(el);
        return ToJSObject(el);
    }


    private JSValue JsRegistrationCreateAttribute016Core(global::Broiler.JavaScript.Engine.JSContext context, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'createAttribute': 1 argument required, but only 0 present.");
        var name = a[0].ToString();
        ValidateElementName(name, context);
        return BuildStandaloneAttrNode(AsciiToLower(name), null);
    }


    private JSValue JsRegistrationCreateDocumentFragment017Core(in Arguments a)
    {
        var fragment = new DomElement(_document, "#document-fragment", null, null, string.Empty);
        _knownNodes.Add(fragment);
        return ToJSObject(fragment);
    }


    private JSValue JsRegistrationCreateEvent033Core(in Arguments a)
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
        JSValue JsRegistrationStopPropagation018(in Arguments _)
        {
            legacyCancelBubble = true;
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"stopPropagation", new JSFunction(JsRegistrationStopPropagation018, "stopPropagation", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsRegistrationStopImmediatePropagation019(in Arguments _)
        {
            legacyCancelBubble = true;
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"stopImmediatePropagation", new JSFunction(JsRegistrationStopImmediatePropagation019, "stopImmediatePropagation", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsRegistrationPreventDefault020(in Arguments _)
        {
            var cancelable = evt[(KeyString)"cancelable"];
            if (cancelable != null && cancelable.BooleanValue)
                evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"preventDefault", new JSFunction(JsRegistrationPreventDefault020, "preventDefault", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsRegistrationGetCancelBubble021(in Arguments _)
        {
            return legacyCancelBubble ? JSBoolean.True : JSBoolean.False;
        }

        JSValue JsRegistrationSetCancelBubble022(in Arguments setArgs)
        {
            if (setArgs.Length > 0 && setArgs[0].BooleanValue)
                legacyCancelBubble = true;
            return JSUndefined.Value;
        }

        evt.FastAddProperty((KeyString)"cancelBubble", new JSFunction(JsRegistrationGetCancelBubble021, "get cancelBubble"), new JSFunction(JsRegistrationSetCancelBubble022, "set cancelBubble"), JSPropertyAttributes.EnumerableConfigurableProperty);
        JSValue JsRegistrationGetReturnValue023(in Arguments _)
        {
            return evt[(KeyString)"defaultPrevented"].BooleanValue ? JSBoolean.False : JSBoolean.True;
        }

        JSValue JsRegistrationSetReturnValue024(in Arguments setArgs)
        {
            var cancelable = evt[(KeyString)"cancelable"];
            if (setArgs.Length > 0 && !setArgs[0].BooleanValue && cancelable != null && cancelable.BooleanValue)
                evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
            return JSUndefined.Value;
        }

        evt.FastAddProperty((KeyString)"returnValue", new JSFunction(JsRegistrationGetReturnValue023, "get returnValue"), new JSFunction(JsRegistrationSetReturnValue024, "set returnValue"), JSPropertyAttributes.EnumerableConfigurableProperty);
        JSValue JsRegistrationInitEvent025(in Arguments initArgs)
        {
            if (initArgs.Length > 0)
                evt[(KeyString)"type"] = new JSString(initArgs[0].ToString());
            if (initArgs.Length > 1)
                evt[(KeyString)"bubbles"] = initArgs[1].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 2)
                evt[(KeyString)"cancelable"] = initArgs[2].BooleanValue ? JSBoolean.True : JSBoolean.False;
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"initEvent", new JSFunction(JsRegistrationInitEvent025, "initEvent", 3), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsRegistrationInitUIEvent026(in Arguments initArgs)
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

        evt.FastAddValue((KeyString)"initUIEvent", new JSFunction(JsRegistrationInitUIEvent026, "initUIEvent", 5), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsRegistrationInitInputEvent027(in Arguments initArgs)
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
        }

        evt.FastAddValue((KeyString)"initInputEvent", new JSFunction(JsRegistrationInitInputEvent027, "initInputEvent", 7), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsRegistrationInitCustomEvent028(in Arguments initArgs)
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

        evt.FastAddValue((KeyString)"initCustomEvent", new JSFunction(JsRegistrationInitCustomEvent028, "initCustomEvent", 4), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsRegistrationInitFocusEvent029(in Arguments initArgs)
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

        evt.FastAddValue((KeyString)"initFocusEvent", new JSFunction(JsRegistrationInitFocusEvent029, "initFocusEvent", 6), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsRegistrationInitKeyboardEvent030(in Arguments initArgs)
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

        evt.FastAddValue((KeyString)"initKeyboardEvent", new JSFunction(JsRegistrationInitKeyboardEvent030, "initKeyboardEvent", 13), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsRegistrationInitMouseEvent031(in Arguments initArgs)
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

        evt.FastAddValue((KeyString)"initMouseEvent", new JSFunction(JsRegistrationInitMouseEvent031, "initMouseEvent", 15), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsRegistrationInitWheelEvent032(in Arguments initArgs)
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

        evt.FastAddValue((KeyString)"initWheelEvent", new JSFunction(JsRegistrationInitWheelEvent032, "initWheelEvent", 16), JSPropertyAttributes.EnumerableConfigurableValue);
        return evt;
    }


    private JSValue JsRegistrationBroilerRegisterMutationObserver034Core(in Arguments a)
    {
        if (a.Length < 2 || a[0] is not JSObject observerObject || a[1] is not JSObject targetObject)
            return JSUndefined.Value;
        var target = FindDomElementByJSObject(targetObject);
        if (target == null)
            return JSUndefined.Value;
        RegisterMutationObserver(observerObject, target, CreateMutationObserverOptions(a.Length > 2 ? a[2] : JSUndefined.Value));
        return JSUndefined.Value;
    }


    private JSValue JsRegistrationBroilerUnregisterMutationObserver035Core(in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSObject observerObject)
            UnregisterMutationObserver(observerObject);
        return JSUndefined.Value;
    }


    private JSValue JsRegistrationWrite036Core(in Arguments a)
    {
        try
        {
            if (a.Length == 0)
                return JSUndefined.Value;
            var fragment = a[0].ToString();
            var builder = new HtmlTreeBuilder();
            var (fragmentRoot, allEls) = builder.BuildFragment(fragment, "body", _document);
            if (fragmentRoot.Children.Count > 0)
            {
                // Find the <body> element in the main tree
                var mainBody = DocumentElement.Children.FirstOrDefault(c => string.Equals(c.TagName, "body", StringComparison.OrdinalIgnoreCase));
                if (mainBody != null)
                {
                    // Find the currently executing <script> element so we can
                    // insert the new nodes right after it (matching real browser
                    // behaviour where document.write() inserts at the parser
                    // insertion point).
                    DomElement? currentScript = null;
                    var documentElements = Elements;
                    if (CurrentScriptIndex >= 0 && CurrentScriptIndex < documentElements.Count)
                    {
                        currentScript = documentElements[CurrentScriptIndex];
                        // Verify it's a <script> in mainBody
                        if (currentScript.Parent != mainBody)
                            currentScript = null;
                    }

                    var writtenChildren = fragmentRoot.Children.ToArray();
                    if (currentScript != null)
                    {
                        var insertIdx = mainBody.Children.IndexOf(currentScript) + 1;
                        for (int ci = 0; ci < writtenChildren.Length; ci++)
                        {
                            writtenChildren[ci].Parent = mainBody;
                            mainBody.Children.Insert(insertIdx + ci, writtenChildren[ci]);
                        }
                    }
                    else
                    {
                        // Fallback: append to end
                        foreach (var child in writtenChildren)
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
                    foreach (var tc in writtenChildren)
                    {
                        contentEls.Add(tc);
                        CollectAllDescendantsFlat(tc, contentEls);
                    }

                    _knownNodes.UnionWith(contentEls);
                }
            }

            return JSUndefined.Value;
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.document.write", $"Error in document.write: {ex.Message}", ex);
            return JSUndefined.Value;
        }
    }


    private JSValue JsRegistrationWriteln037Core(global::Broiler.JavaScript.BuiltIns.Function.JSFunction? writeFn, in Arguments a)
    {
        var text = a.Length > 0 ? a[0].ToString() + "\n" : "\n";
        return writeFn.InvokeFunction(new Arguments(writeFn, new JSString(text)));
    }


    private JSValue JsRegistrationCreateTreeWalker038Core(global::Broiler.HtmlBridge.DomBridge? bridgeForTraversal, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'createTreeWalker': 1 argument required.");
        var rootObj = a[0] as JSObject;
        if (rootObj == null)
            throw new JSException("Failed to execute 'createTreeWalker': parameter 1 is not of type 'Node'.");
        var rootEl = bridgeForTraversal.FindDomElementByJSObject(rootObj);
        if (rootEl == null)
            return JSNull.Value;
        var whatToShow = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? unchecked((int)(uint)a[1].DoubleValue) : unchecked((int)0xFFFFFFFF);
        var filterFn = a.Length > 2 && a[2] is JSFunction f ? f : (a.Length > 2 && a[2] is JSObject filterObj ? filterObj[(KeyString)"acceptNode"] as JSFunction : null);
        return bridgeForTraversal.BuildTreeWalker(rootEl, whatToShow, filterFn);
    }


    private JSValue JsRegistrationCreateNodeIterator039Core(global::Broiler.HtmlBridge.DomBridge? bridgeForTraversal, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'createNodeIterator': 1 argument required.");
        var rootObj = a[0] as JSObject;
        if (rootObj == null)
            throw new JSException("Failed to execute 'createNodeIterator': parameter 1 is not of type 'Node'.");
        var rootEl = bridgeForTraversal.FindDomElementByJSObject(rootObj);
        if (rootEl == null)
            return JSNull.Value;
        var whatToShow = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? unchecked((int)(uint)a[1].DoubleValue) : unchecked((int)0xFFFFFFFF);
        var filterFn = a.Length > 2 && a[2] is JSFunction f ? f : (a.Length > 2 && a[2] is JSObject filterObj ? filterObj[(KeyString)"acceptNode"] as JSFunction : null);
        return bridgeForTraversal.BuildNodeIterator(rootEl, whatToShow, filterFn);
    }


    private JSValue JsRegistrationCreateComment041Core(in Arguments a)
    {
        var data = a.Length > 0 ? a[0].ToString() : string.Empty;
        var el = new DomElement(_document, "#comment", null, null, string.Empty, isTextNode: false);
        el.TextContent = data;
        _knownNodes.Add(el);
        return ToJSObject(el);
    }


    private JSValue JsRegistrationGetChildNodes046Core(in Arguments _)
    {
        var nodes = new List<JSValue>();
        foreach (var child in _documentNode.Children)
            nodes.Add(ToJSObject(child));
        return new JSArray(nodes);
    }


    private JSValue JsRegistrationRemoveChild047Core(global::Broiler.HtmlBridge.DomElement? docNodeForMutation, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var childObj = a[0] as JSObject;
        if (childObj == null)
            return JSNull.Value;
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
    }


    private JSValue JsRegistrationAppendChild048Core(global::Broiler.HtmlBridge.DomElement? docNodeForMutation, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var childObj = a[0] as JSObject;
        if (childObj == null)
            return JSNull.Value;
        var childEl = FindDomElementByJSObject(childObj);
        if (childEl != null)
        {
            if (childEl.Parent != null)
            {
                var oldParent = childEl.Parent;
                var oldIndex = oldParent.Children.IndexOf(childEl);
                if (oldIndex >= 0)
                {
                    NotifyNodeIteratorPreRemoval(childEl);
                    oldParent.Children.RemoveAt(oldIndex);
                    NotifyChildRemoved(oldParent, childEl, oldIndex);
                }
            }

            childEl.Parent = docNodeForMutation;
            docNodeForMutation.Children.Add(childEl);
            NotifyChildAdded(docNodeForMutation, childEl, docNodeForMutation.Children.Count - 1);
        }

        return a[0];
    }


    private JSValue JsRegistrationInsertBefore049Core(global::Broiler.HtmlBridge.DomElement? docNodeForMutation, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var newObj = a[0] as JSObject;
        if (newObj == null)
            return JSNull.Value;
        var newEl = FindDomElementByJSObject(newObj);
        if (newEl == null)
            return a[0];
        if (newEl.Parent != null)
        {
            var oldParent = newEl.Parent;
            var oldIndex = oldParent.Children.IndexOf(newEl);
            if (oldIndex >= 0)
            {
                NotifyNodeIteratorPreRemoval(newEl);
                oldParent.Children.RemoveAt(oldIndex);
                NotifyChildRemoved(oldParent, newEl, oldIndex);
            }
        }

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
                    NotifyChildAdded(docNodeForMutation, newEl, idx);
                    return a[0];
                }
            }
        }

        // If refChild is null or not found, append
        newEl.Parent = docNodeForMutation;
        docNodeForMutation.Children.Add(newEl);
        NotifyChildAdded(docNodeForMutation, newEl, docNodeForMutation.Children.Count - 1);
        return a[0];
    }


    private JSValue JsRegistrationGetForms050Core(in Arguments _)
    {
        var results = new List<JSValue>();
        foreach (var el in Elements)
        {
            if (string.Equals(el.TagName, "form", StringComparison.OrdinalIgnoreCase))
                results.Add(ToJSObject(el));
        }

        var arr = new JSArray(results);
        // Add named access: forms with a 'name' attribute can be
        // accessed as properties of the collection.
        foreach (var el in Elements)
        {
            if (string.Equals(el.TagName, "form", StringComparison.OrdinalIgnoreCase))
            {
                if (el.Attributes.TryGetValue("name", out var formName) && !string.IsNullOrEmpty(formName))
                    arr.FastAddValue((KeyString)formName, ToJSObject(el), JSPropertyAttributes.EnumerableConfigurableValue);
            }
        }

        return arr;
    }


    private JSValue JsRegistrationCreateElementNS051Core(global::Broiler.JavaScript.Engine.JSContext context, in Arguments a)
    {
        var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
        var localName = a.Length > 1 ? a[1].ToString() : (a.Length > 0 ? a[0].ToString() : "div");
        ValidateQualifiedName(localName, ns, context);
        var el = new DomElement(_document, localName, null, null, string.Empty);
        if (!string.IsNullOrEmpty(ns))
            el.NamespaceURI = ns;
        _knownNodes.Add(el);
        return ToJSObject(el);
    }


    private JSValue JsRegistrationCreateAttributeNS052Core(global::Broiler.JavaScript.Engine.JSContext context, in Arguments a)
    {
        var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
        if (a.Length < 2)
            throw new JSException("Failed to execute 'createAttributeNS': 2 arguments required, but fewer present.");
        var qualifiedName = a[1].ToString();
        ValidateQualifiedName(qualifiedName, ns, context);
        return BuildStandaloneAttrNode(qualifiedName, ns);
    }


    private JSValue JsRegistrationGetImages053Core(in Arguments _)
    {
        var results = new List<JSValue>();
        foreach (var el in Elements)
        {
            if (string.Equals(el.TagName, "img", StringComparison.OrdinalIgnoreCase))
                results.Add(ToJSObject(el));
        }

        return new JSArray(results);
    }


    private JSValue JsRegistrationGetLinks054Core(in Arguments _)
    {
        var results = new List<JSValue>();
        CollectLinksInTreeOrder(DocumentElement, results);
        return new JSArray(results);
    }


    private JSValue JsRegistrationGetStyleSheets055Core(in Arguments _)
    {
        var styleEls = new List<DomElement>();
        foreach (var el in Elements)
        {
            if (string.Equals(el.TagName, "style", StringComparison.OrdinalIgnoreCase))
                styleEls.Add(el);
        }

        var arr = new JSArray();
        foreach (var styleEl in styleEls)
            arr.Add(BuildStyleSheetObject(styleEl));
        return arr;
    }


    private JSValue JsRegistrationCreateDocumentType057Core(global::Broiler.JavaScript.Engine.JSContext context, in Arguments a)
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
        var doctype = new DomElement(_document, "#doctype", null, null, string.Empty);
        GetElementRuntimeState(doctype).DocumentType.Name.Set(qualifiedName);
        GetElementRuntimeState(doctype).DocumentType.PublicId.Set(publicId);
        GetElementRuntimeState(doctype).DocumentType.SystemId.Set(systemId);
        GetElementRuntimeState(doctype).DocumentType.InternalSubset.Set(null);
        _knownNodes.Add(doctype);
        return ToJSObject(doctype);
    }


    private JSValue JsRegistrationCreateDocument058Core(global::Broiler.JavaScript.Engine.JSContext context, in Arguments a)
    {
        var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
        var qName = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? a[1].ToString() : null;
        var doctypeArg = a.Length > 2 ? a[2] : null;
        if (!string.IsNullOrEmpty(qName))
            ValidateQualifiedName(qName, ns, context);
        // Build a new document root
        var docRoot = new DomElement(_document, "#subdoc-root", null, null, string.Empty);
        GetElementRuntimeState(docRoot).Document.HasViewport.Set(false);
        _knownNodes.Add(docRoot);
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
            var docEl = new DomElement(_document, qName, null, null, string.Empty);
            if (!string.IsNullOrEmpty(ns))
                docEl.NamespaceURI = ns;
            docEl.Parent = docRoot;
            docEl.OwnerDocRoot = docRoot;
            docRoot.Children.Add(docEl);
            _knownNodes.Add(docEl);
        }

        return BuildSubDocument(docRoot);
    }


    private JSValue JsRegistrationCreateHTMLDocument059Core(in Arguments a)
    {
        var title = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
        // Build a new HTML document root with html/head/body
        var docRoot = new DomElement(_document, "#subdoc-root", null, null, string.Empty);
        GetElementRuntimeState(docRoot).Document.HasViewport.Set(false);
        _knownNodes.Add(docRoot);
        // Add DOCTYPE
        var doctype = new DomElement(_document, "#doctype", null, null, string.Empty);
        GetElementRuntimeState(doctype).DocumentType.Name.Set("html");
        GetElementRuntimeState(doctype).DocumentType.PublicId.Set(string.Empty);
        GetElementRuntimeState(doctype).DocumentType.SystemId.Set(string.Empty);
        GetElementRuntimeState(doctype).DocumentType.InternalSubset.Set(null);
        doctype.Parent = docRoot;
        doctype.OwnerDocRoot = docRoot;
        docRoot.Children.Add(doctype);
        _knownNodes.Add(doctype);
        var htmlEl = new DomElement(_document, "html", null, null, string.Empty);
        htmlEl.NamespaceURI = "http://www.w3.org/1999/xhtml";
        htmlEl.Parent = docRoot;
        htmlEl.OwnerDocRoot = docRoot;
        docRoot.Children.Add(htmlEl);
        _knownNodes.Add(htmlEl);
        var headEl = new DomElement(_document, "head", null, null, string.Empty);
        headEl.Parent = htmlEl;
        headEl.OwnerDocRoot = docRoot;
        htmlEl.Children.Add(headEl);
        _knownNodes.Add(headEl);
        // Add <title> element if title argument is provided
        if (title != null)
        {
            var titleEl = new DomElement(_document, "title", null, null, string.Empty);
            titleEl.Parent = headEl;
            titleEl.OwnerDocRoot = docRoot;
            headEl.Children.Add(titleEl);
            _knownNodes.Add(titleEl);
            var titleText = new DomElement(_document, "#text", null, null, string.Empty, isTextNode: true);
            titleText.TextContent = title;
            titleText.Parent = titleEl;
            titleText.OwnerDocRoot = docRoot;
            titleEl.Children.Add(titleText);
            _knownNodes.Add(titleText);
        }

        var bodyEl = new DomElement(_document, "body", null, null, string.Empty);
        bodyEl.Parent = htmlEl;
        bodyEl.OwnerDocRoot = docRoot;
        htmlEl.Children.Add(bodyEl);
        _knownNodes.Add(bodyEl);
        return BuildSubDocument(docRoot);
    }


    private JSValue JsRegistrationAddEventListener060Core(global::Broiler.HtmlBridge.DomElement? docNode, in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        var type = a[0].ToString();
        var listener = a[1];
        if (!GetEventListeners(docNode).TryGetValue(type, out var listeners))
        {
            listeners = [];
            GetEventListeners(docNode)[type] = listeners;
        }

        var registration = CreateEventListenerRegistration(listener, a.Length > 2 ? a[2] : JSUndefined.Value);
        if (!HasMatchingEventListener(listeners, registration))
            listeners.Add(registration);
        return JSUndefined.Value;
    }


    private JSValue JsRegistrationRemoveEventListener061Core(global::Broiler.HtmlBridge.DomElement? docNode, in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        var type = a[0].ToString();
        var listener = a[1];
        var capture = GetCaptureForRemoval(a.Length > 2 ? a[2] : JSUndefined.Value);
        if (GetEventListeners(docNode).TryGetValue(type, out var listeners))
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
    }


    private JSValue JsRegistrationDispatchEvent062Core(global::Broiler.HtmlBridge.DomBridge? bridgeRef, global::Broiler.HtmlBridge.DomElement? docNode, in Arguments a)
    {
        if (a.Length == 0)
            return JSBoolean.True;
        var evt = a[0] as JSObject;
        if (evt == null)
            return JSBoolean.True;
        return bridgeRef.DispatchEventOnElement(docNode, evt);
    }


    private JSValue JsRegistrationGetContentType063Core(in Arguments _)
    {
        if (_pageUrl.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) || _pageUrl.EndsWith(".xht", StringComparison.OrdinalIgnoreCase) || _pageUrl.Contains("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
            return new JSString("application/xhtml+xml");
        return new JSString("text/html");
    }


    private JSValue JsRegistrationMatchMedia069Core(in Arguments a)
    {
        var query = a.Length > 0 ? a[0].ToString() : string.Empty;
        var matches = !string.IsNullOrEmpty(query) && EvaluateMediaQuery(query, _viewportWidth, _viewportHeight);
        var result = new JSObject();
        result.FastAddValue((KeyString)"matches", matches ? JSBoolean.True : JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        result.FastAddValue((KeyString)"media", new JSString(query), JSPropertyAttributes.EnumerableConfigurableValue);
        // addListener / removeListener stubs
        result.FastAddValue((KeyString)"addListener", UndefinedFunction("addListener", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        result.FastAddValue((KeyString)"removeListener", UndefinedFunction("removeListener", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        return result;
    }


    private JSValue JsRegistrationSetTimeout070Core(in Arguments a)
    {
        var id = ++_timerIdCounter;
        if (a.Length > 0 && a[0] is JSFunction fn)
        {
            _timeoutCallbacks[id] = fn;
        }

        return new JSNumber(id);
    }


    private JSValue JsRegistrationClearTimeout071Core(in Arguments a)
    {
        if (a.Length > 0)
        {
            var id = (int)a[0].DoubleValue;
            _timeoutCallbacks.Remove(id);
            _clearedTimerIds.Add(id);
        }

        return JSUndefined.Value;
    }


    private JSValue JsRegistrationSetInterval072Core(in Arguments a)
    {
        var id = ++_timerIdCounter;
        if (a.Length > 0 && a[0] is JSFunction fn)
        {
            _intervalCallbacks[id] = fn;
        }

        return new JSNumber(id);
    }


    private JSValue JsRegistrationClearInterval073Core(in Arguments a)
    {
        if (a.Length > 0)
        {
            var id = (int)a[0].DoubleValue;
            _intervalCallbacks.Remove(id);
            _clearedTimerIds.Add(id);
        }

        return JSUndefined.Value;
    }


    private JSValue JsRegistrationRequestAnimationFrame074Core(in Arguments a)
    {
        var id = ++_rafIdCounter;
        if (a.Length > 0 && a[0] is JSFunction fn)
        {
            _rafCallbacks[id] = fn;
        }

        return new JSNumber(id);
    }


    private JSValue JsRegistrationCancelAnimationFrame075Core(in Arguments a)
    {
        if (a.Length > 0)
        {
            var id = (int)a[0].DoubleValue;
            _rafCallbacks.Remove(id);
        }

        return JSUndefined.Value;
    }


    private JSValue JsRegistrationAlert076Core(in Arguments a)
    {
        var msg = a.Length > 0 ? a[0].ToString() : string.Empty;
        RenderLogger.LogDebug(LogCategory.JavaScript, "DomBridge.alert", msg);
        return JSUndefined.Value;
    }


    private JSValue JsRegistrationResponse113Core(ResponseInitParser parseResponseInit, ResponseFactory createResponse, in Arguments a)
    {
        var body = a.Length > 0 && !a[0].IsUndefined && !a[0].IsNull ? a[0].ToString() : string.Empty;
        var (status, statusText, url, type, redirected, headers) = parseResponseInit(a.Length > 1 ? a[1] : null);
        return createResponse(body, status, statusText, url, type, redirected, headers);
    }


    private JSValue JsRegistrationJson114Core(ResponseInitParser parseResponseInit, ResponseFactory createResponse, in Arguments a)
    {
        var jsonBody = JSJSON.Stringify(a.Length > 0 ? a[0] : JSNull.Value);
        var (status, statusText, url, type, redirected, headers) = parseResponseInit(a.Length > 1 ? a[1] : null);
        if (!headers.ContainsKey("Content-Type"))
            headers["Content-Type"] = "application/json";
        return createResponse(jsonBody, status, statusText, url, type, redirected, headers);
    }


    private JSValue JsRegistrationRedirect116Core(global::System.Func<string, string> resolveResponseRedirectUrl, ResponseFactory createResponse, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'redirect' on 'Response': 1 argument required.");
        var status = 302;
        if (a.Length > 1 && int.TryParse(a[1].ToString(), out var parsedStatus))
            status = parsedStatus;
        if (status is not (301 or 302 or 303 or 307 or 308))
            throw new JSException("Failed to execute 'redirect' on 'Response': Invalid status code");
        var resolvedUrl = resolveResponseRedirectUrl(a[0].ToString());
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Location"] = resolvedUrl
        };
        return createResponse(string.Empty, status, string.Empty, string.Empty, "basic", false, headers);
    }


    private JSValue JsRegistrationFetch120Core(JsPropertyStringGetter tryGetJsPropertyString, ObjectStringEntriesEnumerator enumerateObjectStringEntries, global::System.Func<JSValue, JSValue> createAbortErrorValue, ResponseFactory createResponse, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'fetch': 1 argument required.");
        var fetchUrl = a[0].ToString();
        if (a[0] is JSObject requestInput)
        {
            fetchUrl = tryGetJsPropertyString(requestInput, "url", "href") ?? fetchUrl;
        }

        JSValue responseObj = new JSObject();
        // Parse options (method, headers, body)
        var method = "GET";
        string? requestBody = null;
        var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        JSValue signalValue = JSUndefined.Value;
        if (a[0] is JSObject requestObject)
        {
            method = (tryGetJsPropertyString(requestObject, "method") ?? method).ToUpperInvariant();
            requestBody = tryGetJsPropertyString(requestObject, "_bodyInit", "body");
            if (requestObject[(KeyString)"signal"] is { } requestSignal && !requestSignal.IsUndefined && !requestSignal.IsNull)
                signalValue = requestSignal;
            if (requestObject[(KeyString)"headers"] is JSObject requestHeadersObject)
            {
                foreach (var (key, value) in enumerateObjectStringEntries(requestHeadersObject))
                    requestHeaders[key] = value;
            }
        }

        if (a.Length > 1 && a[1] is JSObject opts)
        {
            method = (tryGetJsPropertyString(opts, "method") ?? method).ToUpperInvariant();
            requestBody = tryGetJsPropertyString(opts, "body") ?? requestBody;
            if (opts[(KeyString)"signal"] is { } optionsSignal && !optionsSignal.IsUndefined && !optionsSignal.IsNull)
                signalValue = optionsSignal;
            if (opts[(KeyString)"headers"] is JSObject optionsHeadersObject)
            {
                foreach (var (key, value) in enumerateObjectStringEntries(optionsHeadersObject))
                    requestHeaders[key] = value;
            }
        }

        var rejected = false;
        var rejectedValue = JSUndefined.Value;
        if (signalValue is JSObject signalObject && signalObject[(KeyString)"aborted"].BooleanValue)
        {
            rejected = true;
            rejectedValue = createAbortErrorValue(signalValue);
        }

        try
        {
            if (!rejected)
            {
                var request = new HttpRequestMessage(new HttpMethod(method), fetchUrl);
                if (requestBody != null)
                    request.Content = new StringContent(requestBody, Encoding.UTF8, requestHeaders.TryGetValue("Content-Type", out var ct) ? ct : "text/plain");
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

                responseObj = createResponse(body, statusCode, response.ReasonPhrase ?? string.Empty, fetchUrl, "basic", false, allHeaders);
            }
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.fetch", $"Fetch error: {ex.Message}", ex);
            responseObj = createResponse(string.Empty, 0, ex.Message, fetchUrl, "error", false, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        // Return a thenable (Promise-like) that resolves immediately
        var promise = new JSObject();
        JSValue JsRegistrationThen118(in Arguments thenArgs)
        {
            if (!rejected && thenArgs.Length > 0 && thenArgs[0] is JSFunction cb)
            {
                try
                {
                    cb.InvokeFunction(new Arguments(cb, responseObj));
                }
                catch (Exception ex)
                {
                    RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.fetch.then", $"Callback error: {ex.Message}", ex);
                }
            }

            return promise;
        }

        promise.FastAddValue((KeyString)"then", new JSFunction(JsRegistrationThen118, "then", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsRegistrationCatch119(in Arguments catchArgs)
        {
            if (rejected && catchArgs.Length > 0 && catchArgs[0] is JSFunction cb)
            {
                try
                {
                    cb.InvokeFunction(new Arguments(cb, rejectedValue));
                }
                catch (Exception ex)
                {
                    RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.fetch.catch", $"Callback error: {ex.Message}", ex);
                }
            }

            return promise;
        }

        promise.FastAddValue((KeyString)"catch", new JSFunction(JsRegistrationCatch119, "catch", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        return promise;
    }


    private JSValue JsRegistrationGetComputedStyle121Core(global::Broiler.HtmlBridge.DomBridge? bridgeForStyle, in Arguments a)
    {
        if (a.Length == 0)
            return new JSObject();
        var targetObj = a[0] as JSObject;
        var el = targetObj != null ? bridgeForStyle.FindDomElementByJSObject(targetObj) : null;
        var pseudoElement = a.Length > 1 ? a[1]?.ToString() : null;
        return bridgeForStyle.BuildComputedStyleObject(el, pseudoElement);
    }


    private JSValue JsRegistrationNow122Core(global::System.Int64 performanceTimeOrigin, in Arguments _)
    {
        var elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - performanceTimeOrigin;
        return new JSNumber(elapsed);
    }


    private JSValue JsRegistrationSendBeacon124Core(global::Broiler.JavaScript.Runtime.JSObject? window, in Arguments a)
    {
        if (a.Length == 0 || a[0].IsNullOrUndefined)
            return JSBoolean.False;
        try
        {
            // Per sendBeacon semantics, failure to queue because no live fetch entry
            // point is available should return false instead of throwing.
            if (window[(KeyString)"fetch"] is not JSFunction currentFetch)
                return JSBoolean.False;
            var options = new JSObject();
            options[(KeyString)"method"] = new JSString("POST");
            options[(KeyString)"keepalive"] = JSBoolean.True;
            if (a.Length > 1 && !a[1].IsNullOrUndefined)
                options[(KeyString)"body"] = new JSString(a[1].ToString());
            currentFetch.InvokeFunction(new Arguments(currentFetch, a[0], options));
            return JSBoolean.True;
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.navigator.sendBeacon", $"sendBeacon error: {ex.Message}", ex);
            return JSBoolean.False;
        }
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
    }


    private JSValue JsRegistrationRemoveEventListener137Core(in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
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
        if (a.Length > 1 && a[0].ToString().Equals("scroll", StringComparison.OrdinalIgnoreCase) && a[1] is JSFunction listener && !_visualViewportScrollListeners.Contains(listener))
        {
            _visualViewportScrollListeners.Add(listener);
        }

        return JSUndefined.Value;
    }


    private JSValue JsRegistrationRemoveEventListener147Core(in Arguments a)
    {
        if (a.Length > 1 && a[0].ToString().Equals("scroll", StringComparison.OrdinalIgnoreCase) && a[1] is JSFunction listener)
        {
            _visualViewportScrollListeners.Remove(listener);
        }

        return JSUndefined.Value;
    }


    private JSValue JsRegistrationSetCookie149Core(ref global::System.String? cookieStore, in Arguments a)
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


    private JSValue JsRegistrationGetRandomValues150Core(in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
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
    }


    private static JSValue JsRegistrationGetCurrentTime152Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        if (GetElementRuntimeState(element).Animation.CurrentTimeMilliseconds.TryGet(out var value) && value is double currentTimeMs)
        {
            return new JSNumber(currentTimeMs);
        }

        return new JSNumber(0);
    }


    private static JSValue JsRegistrationSetCurrentTime153Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        if (a.Length > 0)
            GetElementRuntimeState(element).Animation.CurrentTimeMilliseconds.Set(a[0].DoubleValue);
        return JSUndefined.Value;
    }


    private static JSValue JsRegistrationThen154Core(global::Broiler.JavaScript.Runtime.JSObject? ready, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSFunction fn)
            fn.InvokeFunction(new Arguments(JSUndefined.Value, JSUndefined.Value));
        return ready;
    }


    private static JSValue JsRegistrationLog156Core(in Arguments a)
    {
        var parts = new List<string>();
        for (var i = 0; i < a.Length; i++)
            parts.Add(a[i]?.ToString() ?? "undefined");
        RenderLogger.LogDebug(LogCategory.JavaScript, "console.log", string.Join(" ", parts));
        return JSUndefined.Value;
    }


    private static JSValue JsRegistrationWarn157Core(in Arguments a)
    {
        var parts = new List<string>();
        for (var i = 0; i < a.Length; i++)
            parts.Add(a[i]?.ToString() ?? "undefined");
        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Warning, "console.warn", string.Join(" ", parts));
        return JSUndefined.Value;
    }


    private static JSValue JsRegistrationError158Core(in Arguments a)
    {
        var parts = new List<string>();
        for (var i = 0; i < a.Length; i++)
            parts.Add(a[i]?.ToString() ?? "undefined");
        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Error, "console.error", string.Join(" ", parts));
        return JSUndefined.Value;
    }


    private static JSValue JsRegistrationInfo159Core(in Arguments a)
    {
        var parts = new List<string>();
        for (var i = 0; i < a.Length; i++)
            parts.Add(a[i]?.ToString() ?? "undefined");
        RenderLogger.LogDebug(LogCategory.JavaScript, "console.info", string.Join(" ", parts));
        return JSUndefined.Value;
    }

}
