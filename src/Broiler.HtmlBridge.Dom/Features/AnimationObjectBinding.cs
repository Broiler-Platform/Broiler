using Broiler.Dom;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.BuiltIns.Number;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The Web Animations <c>Animation</c> object surface built by <c>BuildAnimationObject</c> — its
/// <c>currentTime</c> get/set and its <c>ready</c>-promise <c>then</c> — co-located as an HtmlBridge
/// feature module (Phase 3). <c>currentTime</c> reads/writes the element's animation timeline on the
/// element's runtime state (via the bridge's neutral <c>internal static</c> <c>GetElementRuntimeState</c>);
/// <c>then</c> is a synchronous promise shim that invokes its callback immediately and returns the
/// <c>ready</c> object (touching no bridge state). These are pure static callbacks (the animation object
/// is built in a static context), so the module has no host contract. Previously the bridge's
/// <c>JsRegistrationGetCurrentTime152Core</c>/<c>SetCurrentTime153Core</c>/<c>Then154Core</c> in the
/// shared JsFunctionCallbacks/Registration.cs grab-bag.
/// </summary>
internal static class AnimationObjectBinding
{
    public static JSValue GetCurrentTime(DomElement element, in Arguments a)
    {
        if (DomBridge.GetElementRuntimeState(element).Animation.CurrentTimeMilliseconds.TryGet(out var value) && value is double currentTimeMs)
            return new JSNumber(currentTimeMs);

        return new JSNumber(0);
    }

    public static JSValue SetCurrentTime(DomElement element, in Arguments a)
    {
        if (a.Length > 0)
            DomBridge.GetElementRuntimeState(element).Animation.CurrentTimeMilliseconds.Set(a[0].DoubleValue);
        return JSUndefined.Value;
    }

    // ready.then(cb): the layout is static, so the animation is already "ready" — run the callback
    // synchronously and return the ready object for chaining.
    public static JSValue Then(JSObject? ready, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSFunction fn)
            fn.InvokeFunction(new Arguments(JSUndefined.Value, JSUndefined.Value));
        return ready;
    }
}
