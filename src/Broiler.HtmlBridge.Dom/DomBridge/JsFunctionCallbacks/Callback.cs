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

    private JSValue JsCallbackStopPropagation001Core(ref global::System.Boolean legacyCancelBubble, in Arguments _)
    {
        legacyCancelBubble = true;
        return JSUndefined.Value;
    }


    private JSValue JsCallbackStopImmediatePropagation002Core(ref global::System.Boolean immediateStopped, ref global::System.Boolean legacyCancelBubble, in Arguments _)
    {
        immediateStopped = true;
        legacyCancelBubble = true;
        return JSUndefined.Value;
    }


    private JSValue JsCallbackPreventDefault003Core(global::System.Boolean currentListenerPassive, global::Broiler.JavaScript.Runtime.JSObject evt, ref global::System.Boolean prevented, in Arguments _)
    {
        var cancelable = evt[(KeyString)"cancelable"];
        if (!currentListenerPassive && cancelable != null && cancelable.BooleanValue)
        {
            prevented = true;
            evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
        }

        return JSUndefined.Value;
    }


    private JSValue JsCallbackSetCancelBubble005Core(ref global::System.Boolean legacyCancelBubble, in Arguments setArgs)
    {
        if (setArgs.Length > 0 && setArgs[0].BooleanValue)
        {
            legacyCancelBubble = true;
        }

        return JSUndefined.Value;
    }


    private JSValue JsCallbackSetReturnValue007Core(global::System.Boolean currentListenerPassive, global::Broiler.JavaScript.Runtime.JSObject evt, ref global::System.Boolean prevented, in Arguments setArgs)
    {
        var cancelable = evt[(KeyString)"cancelable"];
        if (setArgs.Length > 0 && !setArgs[0].BooleanValue && !currentListenerPassive && cancelable != null && cancelable.BooleanValue)
        {
            prevented = true;
            evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
        }

        return JSUndefined.Value;
    }

}
