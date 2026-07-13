using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    private JSValue JsEventsStopPropagation001Core(ref bool legacyCancelBubble, ref bool stopped, in Arguments _)
    {
        stopped = true;
        legacyCancelBubble = true;
        return JSUndefined.Value;
    }


    private JSValue JsEventsStopImmediatePropagation002Core(ref bool immediateStopped, ref bool legacyCancelBubble, ref bool stopped, in Arguments _)
    {
        stopped = true;
        immediateStopped = true;
        legacyCancelBubble = true;
        return JSUndefined.Value;
    }


    private JSValue JsEventsPreventDefault003Core(bool currentListenerPassive, JSObject evt, ref bool prevented, in Arguments _)
    {
        var cancelable = evt[(KeyString)"cancelable"];
        if (!currentListenerPassive && cancelable != null && cancelable.BooleanValue)
        {
            prevented = true;
            evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
        }

        return JSUndefined.Value;
    }


    private JSValue JsEventsSetCancelBubble005Core(ref bool legacyCancelBubble, ref bool stopped, in Arguments setArgs)
    {
        if (setArgs.Length > 0 && setArgs[0].BooleanValue)
        {
            legacyCancelBubble = true;
            stopped = true;
        }

        return JSUndefined.Value;
    }


    private JSValue JsEventsSetReturnValue007Core(bool currentListenerPassive, JSObject evt, ref bool prevented, in Arguments setArgs)
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
