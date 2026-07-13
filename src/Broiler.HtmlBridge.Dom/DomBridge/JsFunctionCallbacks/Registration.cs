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

    private JSValue JsRegistrationGetBody002Core(in Arguments a)
    {
        foreach (var child in ChildElements(DocumentElement))
        {
            if (string.Equals(child.TagName, "body", StringComparison.OrdinalIgnoreCase))
                return ToJSObject(child);
        }

        return JSNull.Value;
    }


    private JSValue JsRegistrationGetHead003Core(in Arguments a)
    {
        foreach (var child in ChildElements(DocumentElement))
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
        return new JSArray([.. hits.Select(ToJSObject)]);
    }


    private JSValue JsRegistrationCreateElement014Core(JSContext context, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'createElement': 1 argument required, but only 0 present.");
        var tag = a[0].ToString();
        ValidateElementName(tag, context);
        tag = AsciiToLower(tag);
        var el = CreateBridgeElement(tag);
        return ToJSObject(el);
    }


    private JSValue JsRegistrationCreateTextNode015Core(in Arguments a)
    {
        var text = a.Length > 0 ? a[0].ToString() : string.Empty;
        var el = CreateBridgeTextNode(text);
        return ToJSObject(el);
    }


    private JSValue JsRegistrationCreateAttribute016Core(JSContext context, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'createAttribute': 1 argument required, but only 0 present.");
        var name = a[0].ToString();
        ValidateElementName(name, context);
        return _attributes.BuildStandaloneAttrNode(AsciiToLower(name), null);
    }


    private JSValue JsRegistrationCreateDocumentFragment017Core(in Arguments a)
    {
        var fragment = CreateBridgeDocumentFragment();
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


    // MutationObserver observe()/disconnect() callbacks moved to the Phase 3
    // MutationObserverBinding feature module (Broiler.HtmlBridge.Dom.Features).

    private JSValue JsRegistrationWrite036Core(in Arguments a)
    {
        try
        {
            if (a.Length == 0)
                return JSUndefined.Value;
            var fragment = a[0].ToString();
            var (fragmentRoot, allEls) = BuildFragmentTree(fragment, "body");
            if (fragmentRoot.ChildNodes.Count > 0)
            {
                // Find the <body> element in the main tree
                var mainBody = ChildElements(DocumentElement).FirstOrDefault(c => string.Equals(c.TagName, "body", StringComparison.OrdinalIgnoreCase));
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
                        if (ParentEl(currentScript) != mainBody)
                            currentScript = null;
                    }

                    var writtenChildren = fragmentRoot.ChildNodes.ToArray();
                    if (currentScript != null)
                    {
                        var insertIdx = ChildIndexOf(mainBody, currentScript) + 1;
                        for (int ci = 0; ci < writtenChildren.Length; ci++)
                        {
                            SetParent(writtenChildren[ci], mainBody);
                            InsertChildAt(mainBody, insertIdx + ci, writtenChildren[ci]);
                        }
                    }
                    else
                    {
                        // Fallback: append to end
                        foreach (var child in writtenChildren)
                        {
                            SetParent(child, mainBody);
                            mainBody.AppendChild(child);
                        }
                    }
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


    private JSValue JsRegistrationWriteln037Core(JSFunction? writeFn, in Arguments a)
    {
        var text = a.Length > 0 ? a[0].ToString() + "\n" : "\n";
        return writeFn.InvokeFunction(new Arguments(writeFn, new JSString(text)));
    }


    // createTreeWalker / createNodeIterator / createComment moved to the Phase 3
    // TraversalBinding feature module (Broiler.HtmlBridge.Dom.Features).

    private JSValue JsRegistrationGetChildNodes046Core(in Arguments _)
    {
        var nodes = new List<JSValue>();
        foreach (var child in ChildElements(_documentNode))
            nodes.Add(ToJSObject(child));
        return new JSArray(nodes);
    }


    private JSValue JsRegistrationRemoveChild047Core(DomElement? docNodeForMutation, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        if (a[0] is not JSObject childObj)
            return JSNull.Value;
        var childEl = FindDomNodeByJSObject(childObj);
        if (childEl != null)
        {
            var idx = ChildIndexOf(docNodeForMutation, childEl);
            if (idx >= 0)
            {
                NotifyNodeIteratorPreRemoval(childEl);
                RemoveNthChild(docNodeForMutation, idx);
                SetParent(childEl, null);
                NotifyChildRemoved(docNodeForMutation, childEl, idx);
            }
        }

        return a[0];
    }


    private JSValue JsRegistrationAppendChild048Core(DomElement? docNodeForMutation, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        if (a[0] is not JSObject childObj)
            return JSNull.Value;
        var childEl = FindDomNodeByJSObject(childObj);
        if (childEl != null)
        {
            if (ParentEl(childEl) != null)
            {
                var oldParent = ParentEl(childEl);
                var oldIndex = ChildIndexOf(oldParent, childEl);
                if (oldIndex >= 0)
                {
                    NotifyNodeIteratorPreRemoval(childEl);
                    RemoveNthChild(oldParent, oldIndex);
                    NotifyChildRemoved(oldParent, childEl, oldIndex);
                }
            }

            SetParent(childEl, docNodeForMutation);
            docNodeForMutation.AppendChild(childEl);
            NotifyChildAdded(docNodeForMutation, childEl, docNodeForMutation.ChildNodes.Count - 1);
        }

        return a[0];
    }


    private JSValue JsRegistrationInsertBefore049Core(DomElement? docNodeForMutation, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        if (a[0] is not JSObject newObj)
            return JSNull.Value;
        var newEl = FindDomNodeByJSObject(newObj);
        if (newEl == null)
            return a[0];
        if (ParentEl(newEl) != null)
        {
            var oldParent = ParentEl(newEl);
            var oldIndex = ChildIndexOf(oldParent, newEl);
            if (oldIndex >= 0)
            {
                NotifyNodeIteratorPreRemoval(newEl);
                RemoveNthChild(oldParent, oldIndex);
                NotifyChildRemoved(oldParent, newEl, oldIndex);
            }
        }

        if (a.Length > 1 && a[1] is JSObject refObj && !a[1].IsNull)
        {
            var refEl = FindDomNodeByJSObject(refObj);
            if (refEl != null)
            {
                var idx = ChildIndexOf(docNodeForMutation, refEl);
                if (idx >= 0)
                {
                    SetParent(newEl, docNodeForMutation);
                    InsertChildAt(docNodeForMutation, idx, newEl);
                    NotifyChildAdded(docNodeForMutation, newEl, idx);
                    return a[0];
                }
            }
        }

        // If refChild is null or not found, append
        SetParent(newEl, docNodeForMutation);
        docNodeForMutation.AppendChild(newEl);
        NotifyChildAdded(docNodeForMutation, newEl, docNodeForMutation.ChildNodes.Count - 1);
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
                if (TryGetAttribute(el, "name", out var formName) && !string.IsNullOrEmpty(formName))
                    arr.FastAddValue((KeyString)formName, ToJSObject(el), JSPropertyAttributes.EnumerableConfigurableValue);
            }
        }

        return arr;
    }


    private JSValue JsRegistrationCreateElementNS051Core(JSContext context, in Arguments a)
    {
        var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
        var localName = a.Length > 1 ? a[1].ToString() : (a.Length > 0 ? a[0].ToString() : "div");
        ValidateQualifiedName(localName, ns, context);
        var el = string.IsNullOrEmpty(ns)
            ? CreateBridgeElement(localName)
            : CreateBridgeElementNS(ns, localName);
        return ToJSObject(el);
    }


    private JSValue JsRegistrationCreateAttributeNS052Core(JSContext context, in Arguments a)
    {
        var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
        if (a.Length < 2)
            throw new JSException("Failed to execute 'createAttributeNS': 2 arguments required, but fewer present.");
        var qualifiedName = a[1].ToString();
        ValidateQualifiedName(qualifiedName, ns, context);
        return _attributes.BuildStandaloneAttrNode(qualifiedName, ns);
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
                    GetElementRuntimeState(dtNode).OwnerDocRoot = docRoot;
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
            GetElementRuntimeState(docEl).OwnerDocRoot = docRoot;
        }

        return BuildSubDocument(docRoot);
    }


    private JSValue JsRegistrationCreateHTMLDocument059Core(in Arguments a)
    {
        var title = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
        // Phase 4 item 1 (P4.4a): a createHTMLDocument root is a canonical DomDocument (was a
        // #subdoc-root sentinel element); doctype + <html> are appended as canonical document children.
        var docRoot = CreateBrowsingContextDocument();
        var doctype = CreateBridgeDocumentType("html", string.Empty, string.Empty);
        docRoot.AppendChild(doctype);
        GetElementRuntimeState(doctype).OwnerDocRoot = docRoot;
        // "http://www.w3.org/1999/xhtml" is the default HTML namespace the funnel applies.
        var htmlEl = CreateBridgeElement("html");
        docRoot.AppendChild(htmlEl);
        GetElementRuntimeState(htmlEl).OwnerDocRoot = docRoot;
        var headEl = CreateBridgeElement("head");
        SetParent(headEl, htmlEl);
        GetElementRuntimeState(headEl).OwnerDocRoot = docRoot;
        htmlEl.AppendChild(headEl);
        // Add <title> element if title argument is provided
        if (title != null)
        {
            var titleEl = CreateBridgeElement("title");
            SetParent(titleEl, headEl);
            GetElementRuntimeState(titleEl).OwnerDocRoot = docRoot;
            headEl.AppendChild(titleEl);
            var titleText = CreateBridgeTextNode(title);
            SetParent(titleText, titleEl);
            GetElementRuntimeState(titleText).OwnerDocRoot = docRoot;
            titleEl.AppendChild(titleText);
        }

        var bodyEl = CreateBridgeElement("body");
        SetParent(bodyEl, htmlEl);
        GetElementRuntimeState(bodyEl).OwnerDocRoot = docRoot;
        htmlEl.AppendChild(bodyEl);
        return BuildSubDocument(docRoot);
    }


    private JSValue JsRegistrationAddEventListener060Core(DomElement? docNode, in Arguments a)
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


    private JSValue JsRegistrationRemoveEventListener061Core(DomElement? docNode, in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        var type = a[0].ToString();
        Dom.Features.EventListenerBinding.RemoveListener(
            GetEventListeners(docNode).TryGetValue(type, out var listeners) ? listeners : null,
            a[1], a.Length > 2 ? a[2] : JSUndefined.Value);
        return JSUndefined.Value;
    }


    private JSValue JsRegistrationDispatchEvent062Core(DomBridge? bridgeRef, DomElement? docNode, in Arguments a)
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


    private JSValue JsRegistrationSetTimeout070Core(in Arguments a) =>
        new JSNumber(_eventLoop.SetTimeout(a.Length > 0 ? a[0] as JSFunction : null));


    private JSValue JsRegistrationClearTimeout071Core(in Arguments a)
    {
        if (a.Length > 0)
            _eventLoop.ClearTimeout((int)a[0].DoubleValue);

        return JSUndefined.Value;
    }


    private JSValue JsRegistrationSetInterval072Core(in Arguments a) =>
        new JSNumber(_eventLoop.SetInterval(a.Length > 0 ? a[0] as JSFunction : null));


    private JSValue JsRegistrationClearInterval073Core(in Arguments a)
    {
        if (a.Length > 0)
            _eventLoop.ClearInterval((int)a[0].DoubleValue);

        return JSUndefined.Value;
    }


    private JSValue JsRegistrationRequestAnimationFrame074Core(in Arguments a) =>
        new JSNumber(_eventLoop.RequestAnimationFrame(a.Length > 0 ? a[0] as JSFunction : null));


    private JSValue JsRegistrationCancelAnimationFrame075Core(in Arguments a)
    {
        if (a.Length > 0)
            _eventLoop.CancelAnimationFrame((int)a[0].DoubleValue);

        return JSUndefined.Value;
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


    private JSValue JsRegistrationSendBeacon124Core(JSObject? window, in Arguments a)
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
