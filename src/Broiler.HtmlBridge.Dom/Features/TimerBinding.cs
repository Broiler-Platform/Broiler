using System;
using System.Diagnostics;
using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.BuiltIns.Number;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The window timer / animation-frame scheduling API — <c>setTimeout</c>/<c>clearTimeout</c>,
/// <c>setInterval</c>/<c>clearInterval</c>, <c>requestAnimationFrame</c>/<c>cancelAnimationFrame</c>
/// and <c>requestIdleCallback</c>/<c>cancelIdleCallback</c> — co-located as an HtmlBridge feature
/// module (Phase 3). Each entry point is a thin adapter that
/// unwraps the JS arguments and delegates to the P2.4 <see cref="BrowserEventLoop"/> task-queue
/// owner. It holds no state of its own, so it takes the owner as a parameter rather than through a
/// host contract. Previously the bridge's
/// <c>JsRegistrationSetTimeout070Core</c>..<c>CancelAnimationFrame075Core</c> in the shared
/// JsFunctionCallbacks/Registration.cs grab-bag.
/// </summary>
internal static class TimerBinding
{
    public static JSValue SetTimeout(BrowserEventLoop loop, in Arguments a) =>
        new JSNumber(loop.SetTimeout(a.Length > 0 ? a[0] as JSFunction : null, ReadDelayMs(a)));

    // The delay argument (a[1]) in ms; absent / NaN / negative are treated as 0 (the event loop clamps too).
    private static double ReadDelayMs(in Arguments a) => a.Length > 1 ? a[1].DoubleValue : 0;

    public static JSValue ClearTimeout(BrowserEventLoop loop, in Arguments a)
    {
        if (a.Length > 0)
            loop.ClearTimeout((int)a[0].DoubleValue);

        return JSUndefined.Value;
    }

    public static JSValue SetInterval(BrowserEventLoop loop, in Arguments a) =>
        new JSNumber(loop.SetInterval(a.Length > 0 ? a[0] as JSFunction : null, ReadDelayMs(a)));

    public static JSValue ClearInterval(BrowserEventLoop loop, in Arguments a)
    {
        if (a.Length > 0)
            loop.ClearInterval((int)a[0].DoubleValue);

        return JSUndefined.Value;
    }

    public static JSValue RequestAnimationFrame(BrowserEventLoop loop, in Arguments a) =>
        new JSNumber(loop.RequestAnimationFrame(a.Length > 0 ? a[0] as JSFunction : null));

    public static JSValue CancelAnimationFrame(BrowserEventLoop loop, in Arguments a)
    {
        if (a.Length > 0)
            loop.CancelAnimationFrame((int)a[0].DoubleValue);

        return JSUndefined.Value;
    }

    /// <summary>
    /// The budget an idle callback is given, in milliseconds — Background Tasks §2.2 caps
    /// <c>timeRemaining()</c> at 50 ms, and that cap is what a page draining a queue actually
    /// measures itself against.
    /// </summary>
    private const double IdleBudgetMs = 50;

    /// <summary>
    /// <c>requestIdleCallback(callback, { timeout })</c> — Background Tasks §2.3, queued as an
    /// ordinary task because a headless drain has no idle period to wait for. What makes it work is
    /// the <c>IdleDeadline</c> the callback is handed: it used to be a bare alias of
    /// <c>setTimeout</c>, so the callback got the timer's zero arguments and the first thing every
    /// caller does with the parameter — <c>deadline.timeRemaining()</c> — threw "Cannot get property
    /// timeRemaining of undefined". That is not a peripheral API: MediaWiki's ResourceLoader
    /// evaluates its module implementations inside one (<c>asyncEvalTask</c> loops until
    /// <c>timeRemaining() &lt;= 0</c>), and its storage store walks localStorage in another.
    /// </summary>
    /// <remarks>
    /// The second argument is an options dictionary, not a delay, so routing it through
    /// <see cref="ReadDelayMs"/> read <c>NaN</c> off the object and scheduled at 0 regardless of what
    /// the page asked for; <c>timeout</c> is read out of it properly here.
    /// </remarks>
    public static JSValue RequestIdleCallback(BrowserEventLoop loop, in Arguments a)
    {
        if (a.Length == 0 || a[0] as JSFunction is not { } callback)
            return new JSNumber(loop.SetTimeout(null));

        // A timeout means "run by then at the latest". There is no idle period here for the callback
        // to have been run in earlier, so a callback that carries one is always running because that
        // deadline arrived — which is what didTimeout reports.
        var timeoutMs = ReadIdleTimeoutMs(a);
        var didTimeout = timeoutMs > 0;

        // The deadline is minted when the callback runs, not when it is registered: the budget is the
        // time this invocation has used, so a re-registered callback gets a fresh one each time.
        var withDeadline = new DomFunction((in _) =>
        {
            callback.InvokeFunction(new Arguments(JSUndefined.Value, CreateIdleDeadline(didTimeout)));
            return JSUndefined.Value;
        }, "requestIdleCallback callback", 0);

        // Scheduling it on the timer queue is what keeps the handle cancellable: the id comes from the
        // same space clearTimeout/cancelIdleCallback act on.
        return new JSNumber(loop.SetTimeout(withDeadline, timeoutMs));
    }

    /// <summary>
    /// <c>cancelIdleCallback</c> — the handle came from the timer id space, so this is
    /// <see cref="ClearTimeout"/> under the name Background Tasks gives it.
    /// </summary>
    public static JSValue CancelIdleCallback(BrowserEventLoop loop, in Arguments a) => ClearTimeout(loop, in a);

    // The `timeout` member of the options dictionary (a[1]), in ms. Absent, non-numeric or
    // non-positive means the page asked for no deadline at all.
    private static double ReadIdleTimeoutMs(in Arguments a)
    {
        if (a.Length < 2 || a[1] is not JSObject options)
            return 0;

        var timeout = options[(KeyString)"timeout"];
        if (timeout is null || timeout.IsNull || timeout.IsUndefined)
            return 0;

        var ms = timeout.DoubleValue;
        return double.IsNaN(ms) || ms <= 0 ? 0 : ms;
    }

    /// <summary>
    /// An <c>IdleDeadline</c> (Background Tasks §2.2) over the wall clock, measured from the moment
    /// the callback is entered. Real elapsed time rather than the event loop's virtual clock,
    /// because the budget is what bounds the work the callback does inside a single synchronous
    /// call, which the virtual clock knows nothing about — and because a budget that never runs down
    /// is a page's `while (deadline.timeRemaining() > n)` loop that never yields.
    /// </summary>
    private static JSObject CreateIdleDeadline(bool didTimeout)
    {
        var enteredAt = Stopwatch.GetTimestamp();

        var deadline = new JSObject();
        deadline.FastAddValue((KeyString)"didTimeout",
            didTimeout ? JSBoolean.True : JSBoolean.False,
            JSPropertyAttributes.EnumerableConfigurableValue);
        deadline.FastAddValue((KeyString)"timeRemaining",
            new DomFunction((in _) => new JSNumber(
                Math.Max(0, IdleBudgetMs - Stopwatch.GetElapsedTime(enteredAt).TotalMilliseconds)),
                "timeRemaining", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        return deadline;
    }
}
