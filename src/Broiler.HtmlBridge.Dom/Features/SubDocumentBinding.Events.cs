using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// <see cref="SubDocumentBinding"/> — the legacy <c>document.createEvent(type)</c> factory and the
/// <c>initEvent</c>/<c>initUIEvent</c>/<c>initMouseEvent</c>/… mutator family it installs on the
/// returned event object. Self-contained: it builds a plain JS event object with no bridge coupling.
/// </summary>
internal sealed partial class SubDocumentBinding
{
    private static JSValue CreateEvent(in Arguments a)
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
        JSValue StopPropagation(in Arguments __)
        {
            legacyCancelBubble = true;
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"stopPropagation", new JSFunction(StopPropagation, "stopPropagation", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue StopImmediatePropagation(in Arguments __)
        {
            legacyCancelBubble = true;
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"stopImmediatePropagation", new JSFunction(StopImmediatePropagation, "stopImmediatePropagation", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue PreventDefault(in Arguments __)
        {
            evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"preventDefault", new JSFunction(PreventDefault, "preventDefault", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue GetCancelBubble(in Arguments __)
        {
            return legacyCancelBubble ? JSBoolean.True : JSBoolean.False;
        }

        JSValue SetCancelBubble(in Arguments setArgs)
        {
            if (setArgs.Length > 0 && setArgs[0].BooleanValue)
                legacyCancelBubble = true;
            return JSUndefined.Value;
        }

        evt.FastAddProperty((KeyString)"cancelBubble", new JSFunction(GetCancelBubble, "get cancelBubble"), new JSFunction(SetCancelBubble, "set cancelBubble"), JSPropertyAttributes.EnumerableConfigurableProperty);
        JSValue GetReturnValue(in Arguments __)
        {
            return evt[(KeyString)"defaultPrevented"].BooleanValue ? JSBoolean.False : JSBoolean.True;
        }

        JSValue SetReturnValue(in Arguments setArgs)
        {
            if (setArgs.Length > 0 && !setArgs[0].BooleanValue)
                evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
            return JSUndefined.Value;
        }

        evt.FastAddProperty((KeyString)"returnValue", new JSFunction(GetReturnValue, "get returnValue"), new JSFunction(SetReturnValue, "set returnValue"), JSPropertyAttributes.EnumerableConfigurableProperty);
        JSValue InitEvent(in Arguments initArgs)
        {
            if (initArgs.Length > 0)
                evt[(KeyString)"type"] = new JSString(initArgs[0].ToString());
            if (initArgs.Length > 1)
                evt[(KeyString)"bubbles"] = initArgs[1].BooleanValue ? JSBoolean.True : JSBoolean.False;
            if (initArgs.Length > 2)
                evt[(KeyString)"cancelable"] = initArgs[2].BooleanValue ? JSBoolean.True : JSBoolean.False;
            return JSUndefined.Value;
        }

        evt.FastAddValue((KeyString)"initEvent", new JSFunction(InitEvent, "initEvent", 3), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue InitUIEvent(in Arguments initArgs)
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

        evt.FastAddValue((KeyString)"initUIEvent", new JSFunction(InitUIEvent, "initUIEvent", 5), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue InitCustomEvent(in Arguments initArgs)
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

        evt.FastAddValue((KeyString)"initCustomEvent", new JSFunction(InitCustomEvent, "initCustomEvent", 4), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue InitFocusEvent(in Arguments initArgs)
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

        evt.FastAddValue((KeyString)"initFocusEvent", new JSFunction(InitFocusEvent, "initFocusEvent", 6), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue InitKeyboardEvent(in Arguments initArgs)
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

        evt.FastAddValue((KeyString)"initKeyboardEvent", new JSFunction(InitKeyboardEvent, "initKeyboardEvent", 13), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue InitMouseEvent(in Arguments initArgs)
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

        evt.FastAddValue((KeyString)"initMouseEvent", new JSFunction(InitMouseEvent, "initMouseEvent", 15), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue InitWheelEvent(in Arguments initArgs)
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

        evt.FastAddValue((KeyString)"initWheelEvent", new JSFunction(InitWheelEvent, "initWheelEvent", 16), JSPropertyAttributes.EnumerableConfigurableValue);
        return evt;
    }
}
