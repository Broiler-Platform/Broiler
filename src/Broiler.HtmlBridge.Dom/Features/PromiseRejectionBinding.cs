using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The HTML spec's two promise-rejection events, fired at the window: <c>unhandledrejection</c> when
/// a rejected promise reaches the end of a microtask checkpoint with nobody waiting on it, and
/// <c>rejectionhandled</c> when a handler arrives for one already reported that way.
/// </summary>
/// <remarks>
/// <para>
/// The engine's tracker (see the bridge's <c>NotifyRejectedPromises</c>) decides <em>which</em>
/// promises these fire for. This module is only the event: the <c>PromiseRejectionEvent</c> shape —
/// <c>promise</c> and <c>reason</c> alongside the ordinary <c>Event</c> members — and the two ways a
/// page listens for it.
/// </para>
/// <para>
/// <b>Cancelling matters here</b>, which is why <c>unhandledrejection</c> is built cancelable and
/// <c>rejectionhandled</c> is not. A page with its own error channel calls
/// <c>preventDefault()</c> to say it has taken responsibility, and the caller's default action —
/// logging the rejection as a failure — must then not run. <c>preventDefault</c> is a no-op on an
/// event whose <c>cancelable</c> is false, so getting that flag wrong would silently make the
/// suppression impossible.
/// </para>
/// <para>
/// <b>Both registration forms, one event object.</b> The <c>on*</c> handler property is invoked
/// before the <c>addEventListener</c> registrations, matching how <c>window.onload</c> is fired
/// ahead of the window <c>load</c> listeners. The two share the event, so a <c>preventDefault()</c>
/// from either is what the caller sees: the handler's cancellation is written to
/// <c>defaultPrevented</c> before dispatch, and the window dispatch seeds itself from that property
/// rather than starting fresh.
/// </para>
/// </remarks>
internal static class PromiseRejectionBinding
{
    /// <summary>Fired for a rejection nobody is waiting on. Cancelable.</summary>
    internal const string UnhandledRejectionEvent = "unhandledrejection";

    /// <summary>Fired when a handler arrives late for one already reported. Not cancelable.</summary>
    internal const string RejectionHandledEvent = "rejectionhandled";

    /// <summary>
    /// Fires <c>unhandledrejection</c> at the window. Returns <see langword="true"/> when a listener
    /// called <c>preventDefault()</c> — the page has claimed the failure and the caller's default
    /// action should not run.
    /// </summary>
    public static bool DispatchUnhandledRejection(IPromiseRejectionHost host, JSValue promise, JSValue reason) =>
        Dispatch(host, UnhandledRejectionEvent, promise, reason, cancelable: true);

    /// <summary>
    /// Fires <c>rejectionhandled</c> at the window: a rejection already reported as unhandled has
    /// since been claimed, so a page that logged it can retract that.
    /// </summary>
    public static void DispatchRejectionHandled(IPromiseRejectionHost host, JSValue promise, JSValue reason) =>
        Dispatch(host, RejectionHandledEvent, promise, reason, cancelable: false);

    private static bool Dispatch(
        IPromiseRejectionHost host, string type, JSValue promise, JSValue reason, bool cancelable)
    {
        var window = host.WindowObject;
        if (window == null)
            return false;

        var prevented = false;
        var evt = new JSObject();
        evt.FastAddValue((KeyString)"type", new JSString(type), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"promise", promise, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"reason", reason, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"bubbles", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue(
            (KeyString)"cancelable",
            cancelable ? JSBoolean.True : JSBoolean.False,
            JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"isTrusted", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);

        // Only for the on* handler, which runs before the window dispatch installs its own. Guarded
        // on `cancelable` for the same reason the dispatch's is: preventDefault() on a
        // non-cancelable event does nothing.
        evt.FastAddValue(
            (KeyString)"preventDefault",
            new DomFunction(
                (in Arguments _) =>
                {
                    if (cancelable)
                        prevented = true;
                    return JSUndefined.Value;
                },
                "preventDefault",
                0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        if (window[(KeyString)("on" + type)] is { } handler && handler is JSFunction or JSObject)
            DomBridge.InvokeEventListener(handler, evt, "DomBridge." + type);

        // Hand the handler's decision to the dispatch, which seeds its own `prevented` from this
        // property. Without this write a cancellation from the on* form would be dropped the moment
        // the listeners ran.
        evt[(KeyString)"defaultPrevented"] = prevented ? JSBoolean.True : JSBoolean.False;

        return !host.DispatchAtWindow(evt) || prevented;
    }
}
