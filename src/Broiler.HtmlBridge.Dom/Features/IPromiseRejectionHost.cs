using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// What <see cref="PromiseRejectionBinding"/> needs of the bridge to fire
/// <c>unhandledrejection</c> and <c>rejectionhandled</c> at the window: the global object the
/// <c>on*</c> handler property lives on, and the window-scoped listener dispatch.
/// </summary>
internal interface IPromiseRejectionHost
{
    /// <summary>
    /// The window — which is also the global object, so <c>window.onunhandledrejection = f</c> and a
    /// bare <c>onunhandledrejection = f</c> are one property. <c>null</c> before <c>Attach</c>, when
    /// there is no page to fire at.
    /// </summary>
    JSObject? WindowObject { get; }

    /// <summary>
    /// Runs the window's <c>addEventListener</c> registrations for <paramref name="evt"/>, and
    /// returns <see langword="false"/> when the default was prevented. Seeded from the event's
    /// existing <c>defaultPrevented</c>, so a cancellation from the <c>on*</c> handler that ran
    /// first survives into the result.
    /// </summary>
    bool DispatchAtWindow(JSObject evt);
}
