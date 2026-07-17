using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.BuiltIns.Number;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The window timer / animation-frame scheduling API — <c>setTimeout</c>/<c>clearTimeout</c>,
/// <c>setInterval</c>/<c>clearInterval</c>, <c>requestAnimationFrame</c>/<c>cancelAnimationFrame</c>
/// — co-located as an HtmlBridge feature module (Phase 3). Each entry point is a thin adapter that
/// unwraps the JS arguments and delegates to the P2.4 <see cref="BrowserEventLoop"/> task-queue
/// owner. It holds no state of its own, so it takes the owner as a parameter rather than through a
/// host contract. Previously the bridge's
/// <c>JsRegistrationSetTimeout070Core</c>..<c>CancelAnimationFrame075Core</c> in the shared
/// JsFunctionCallbacks/Registration.cs grab-bag.
/// </summary>
internal static class TimerBinding
{
    public static JSValue SetTimeout(BrowserEventLoop loop, in Arguments a) =>
        new JSNumber(loop.SetTimeout(a.Length > 0 ? a[0] as JSFunction : null));

    public static JSValue ClearTimeout(BrowserEventLoop loop, in Arguments a)
    {
        if (a.Length > 0)
            loop.ClearTimeout((int)a[0].DoubleValue);

        return JSUndefined.Value;
    }

    public static JSValue SetInterval(BrowserEventLoop loop, in Arguments a) =>
        new JSNumber(loop.SetInterval(a.Length > 0 ? a[0] as JSFunction : null));

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
}
