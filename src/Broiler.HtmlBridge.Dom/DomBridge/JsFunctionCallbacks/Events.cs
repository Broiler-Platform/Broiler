using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    private JSValue JsEventsStopPropagation001Core(ref global::System.Boolean legacyCancelBubble, ref global::System.Boolean stopped, in Arguments _)
    {
        stopped = true;
        legacyCancelBubble = true;
        return JSUndefined.Value;
    }


    private JSValue JsEventsStopImmediatePropagation002Core(ref global::System.Boolean immediateStopped, ref global::System.Boolean legacyCancelBubble, ref global::System.Boolean stopped, in Arguments _)
    {
        stopped = true;
        immediateStopped = true;
        legacyCancelBubble = true;
        return JSUndefined.Value;
    }


    private JSValue JsEventsPreventDefault003Core(global::System.Boolean currentListenerPassive, global::Broiler.JavaScript.Runtime.JSObject evt, ref global::System.Boolean prevented, in Arguments _)
    {
        var cancelable = evt[(KeyString)"cancelable"];
        if (!currentListenerPassive && cancelable != null && cancelable.BooleanValue)
        {
            prevented = true;
            evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
        }

        return JSUndefined.Value;
    }


    private JSValue JsEventsSetCancelBubble005Core(ref global::System.Boolean legacyCancelBubble, ref global::System.Boolean stopped, in Arguments setArgs)
    {
        if (setArgs.Length > 0 && setArgs[0].BooleanValue)
        {
            legacyCancelBubble = true;
            stopped = true;
        }

        return JSUndefined.Value;
    }


    private JSValue JsEventsSetReturnValue007Core(global::System.Boolean currentListenerPassive, global::Broiler.JavaScript.Runtime.JSObject evt, ref global::System.Boolean prevented, in Arguments setArgs)
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
