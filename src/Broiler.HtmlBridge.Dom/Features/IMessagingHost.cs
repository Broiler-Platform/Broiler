using System;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The narrow bridge services the <see cref="MessagingBinding"/> feature module needs (HtmlBridge
/// complexity-reduction roadmap Phase 3, P3.10). Web messaging (<c>window.postMessage</c>,
/// <c>MessageChannel</c>/<c>MessagePort</c>) and the generic <c>EventTarget</c> dispatch it shares
/// with sub-windows are deeply entangled with the document's browsing-context state — the active
/// window override, the sub-window/sub-document caches and the window-context switch — which the
/// Phase 2 work deliberately left in the bridge (a future <c>BrowsingContextManager</c>). Rather than
/// drag that state into the module, the module reaches the few browsing-context operations it needs
/// through these named seams, exposed as explicit interface members on <see cref="DomBridge"/> so the
/// public surface is unchanged.
/// </summary>
internal interface IMessagingHost
{
    /// <summary>The top-level window JS wrapper (<c>null</c> before attach).</summary>
    JSObject? WindowJSObject { get; }

    /// <summary>The active JS execution context (<c>null</c> before attach), used to raise
    /// <c>DataCloneError</c> and to structured-clone message payloads.</summary>
    JSContext? JsContext { get; }

    /// <summary>Resolves the window currently driving script execution (honouring the active
    /// sub-window override), canonicalised to the owning browsing context.</summary>
    JSObject? ResolveCurrentWindow();

    /// <summary>Resolves the window that owns <paramref name="target"/> (a message port or generic
    /// event target), falling back to the current window.</summary>
    JSObject? ResolveOwnerWindow(JSObject target);

    /// <summary>Runs <paramref name="callback"/> with the global window/document/location/parent
    /// bindings temporarily switched to <paramref name="targetWindow"/>'s browsing context.</summary>
    void RunWithWindowContext(JSObject targetWindow, Action callback);

    /// <summary>Queues <paramref name="callback"/> as an internal frame action on the event loop
    /// (message delivery is asynchronous, per the HTML messaging model).</summary>
    void QueueFrameAction(Action callback);

    /// <summary>Dispatches <paramref name="evt"/> at the top-level window (the fast path when a
    /// posted message targets the main window itself).</summary>
    void DispatchWindowEvent(JSObject evt);
}
