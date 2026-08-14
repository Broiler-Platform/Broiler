using Broiler.HtmlBridge.Logging;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;
#if BROILER_JS_REJECTION_TRACKING
using Broiler.JavaScript.BuiltIns.Promise;
#endif

namespace Broiler.HtmlBridge;

/// <summary>
/// A rejected promise nobody is waiting on, reported the way a browser reports it: as the
/// <c>unhandledrejection</c> event at the window, with the console line as its default action rather
/// than as the only thing that happens.
/// </summary>
/// <remarks>
/// <para>
/// This is what the rejection tracking was for. Collecting these was the previous step, and it fed
/// one consumer — the CLI's diagnostic log — so a rejection was something only a diagnostic run
/// could observe, and only after the fact. A page could not see its own failures. Routing them
/// through the event gives a page the error channel it expects, and gives the log the behaviour a
/// console has: a page that handles its rejections is not also told it failed to.
/// </para>
/// <para>
/// <b>The split across the submodule boundary.</b> Deciding <em>which</em> promises are unhandled is
/// engine work, and lives in <c>Broiler.JS</c>'s <c>JSPromiseRejectionTracker</c> — a patch under
/// <c>patches/</c> until a maintainer applies it, so the drain here is behind
/// <c>BROILER_JS_REJECTION_TRACKING</c>. Everything else — the event, both listener forms,
/// cancellation, the default action — is this repository's and is compiled and tested either way.
/// <see cref="NotifyUnhandledRejection"/> is the seam: the drain calls it with what the tracker
/// found, and a test calls it directly.
/// </para>
/// </remarks>
public sealed partial class DomBridge : Dom.Features.IPromiseRejectionHost
{
    JSObject? Dom.Features.IPromiseRejectionHost.WindowObject => _windowJSObject;

    bool Dom.Features.IPromiseRejectionHost.DispatchAtWindow(JSObject evt) =>
        DispatchWindowEvent(evt).BooleanValue;

    /// <summary>
    /// Turns on the engine's rejection tracking, from <c>Attach</c>. Unconditional, because
    /// <c>unhandledrejection</c> is a platform event and a page may not have its failures reported
    /// only when a diagnostic run happens to be watching. Compiled out without the engine patch.
    /// </summary>
    private static void EnablePromiseRejectionTracking()
    {
#if BROILER_JS_REJECTION_TRACKING
        JSPromiseRejectionTracker.Enabled = true;
#endif
    }

    /// <summary>
    /// Fires <c>unhandledrejection</c> for every promise the engine is still holding as unclaimed,
    /// and <c>rejectionhandled</c> for every one since claimed. Call after a microtask checkpoint has
    /// drained — before it, a rejection about to be handled a microtask later still looks unhandled.
    /// </summary>
    /// <remarks>
    /// Without the engine patch there is nothing to drain and this returns immediately, which is why
    /// a host may call it unconditionally.
    /// </remarks>
    public void NotifyRejectedPromises()
    {
        ThrowIfDisposed();
#if BROILER_JS_REJECTION_PROMISE_IDENTITY
        foreach (var rejection in JSPromiseRejectionTracker.TakeNotifications())
            NotifyUnhandledRejection(rejection.Promise, rejection.Reason);

        foreach (var rejection in JSPromiseRejectionTracker.TakeReclaimed())
            NotifyRejectionHandled(rejection.Promise, rejection.Reason);
#elif BROILER_JS_REJECTION_TRACKING
        // This engine reports *that* a promise was rejected unhandled, not *which* promise it was,
        // so the event's `promise` is undefined and `rejectionhandled` cannot fire at all — both
        // need the identity the notification API carries. `reason`, which is the only description of
        // the failure there is, is unaffected, and so is cancelling.
        foreach (var rejection in JSPromiseRejectionTracker.TakePending())
            NotifyUnhandledRejection(null, rejection.Reason);
#endif
    }

    /// <summary>
    /// Reports one unhandled rejection: fires the event, and logs the failure unless a listener
    /// called <c>preventDefault()</c>. Returns <see langword="true"/> when it was logged.
    /// </summary>
    /// <remarks>
    /// The log line is the event's <em>default action</em>, in the spec's sense — what happens when
    /// nothing claims it. Its wording is unchanged from when the CLI produced it directly, because
    /// the diagnostics digest groups failures by message and a rewording would split one failure
    /// into two across a version boundary.
    /// </remarks>
    /// <remarks>
    /// Internal, not public, because its parameters are engine types: the bridge's public surface is
    /// guarded against leaking <c>Broiler.JS</c> types to embedders, and a host has no promise to
    /// hand in anyway — it calls <see cref="NotifyRejectedPromises"/> and the engine supplies these.
    /// </remarks>
    internal bool NotifyUnhandledRejection(JSValue? promise, JSValue? reason)
    {
        ThrowIfDisposed();

        var promiseValue = promise ?? JSUndefined.Value;
        var reasonValue = reason ?? JSUndefined.Value;

        if (Dom.Features.PromiseRejectionBinding.DispatchUnhandledRejection(this, promiseValue, reasonValue))
            return false;

        RenderLogger.Log(
            LogCategory.JavaScript,
            LogLevel.Error,
            "DomBridge.UnhandledRejection",
            $"Unhandled promise rejection: {DescribeRejectionReason(reasonValue)}");
        return true;
    }

    /// <summary>
    /// Fires <c>rejectionhandled</c>: a rejection already reported as unhandled has since been
    /// claimed. There is no default action — the event exists so a page that logged the failure can
    /// retract it, and the log cannot retract a line it has already written.
    /// </summary>
    internal void NotifyRejectionHandled(JSValue? promise, JSValue? reason)
    {
        ThrowIfDisposed();
        Dom.Features.PromiseRejectionBinding.DispatchRejectionHandled(
            this, promise ?? JSUndefined.Value, reason ?? JSUndefined.Value);
    }

    /// <summary>
    /// The reason as a console would render it: an <c>Error</c>'s <c>name: message</c> when it has
    /// one, otherwise the value's own string form. "[object Object]" would say nothing about what
    /// failed, and the reason is the only description of the failure there is.
    /// </summary>
    internal static string DescribeRejectionReason(JSValue reason)
    {
        try
        {
            if (reason is JSObject error &&
                error[(KeyString)"message"] is { } message &&
                !message.IsUndefined)
            {
                var name = error[(KeyString)"name"];
                return name == null || name.IsUndefined ? message.ToString() : name + ": " + message;
            }

            return reason?.ToString() ?? "undefined";
        }
        catch (Exception)
        {
            // Describing a rejection must never replace it: the reason can be a hostile object whose
            // toString throws, and this runs while reporting a failure that already happened.
            return "<rejection reason could not be described>";
        }
    }
}
