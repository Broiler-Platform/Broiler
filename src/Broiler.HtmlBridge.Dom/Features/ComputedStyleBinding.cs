using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// <c>window.getComputedStyle(element, pseudoElement?)</c> — the CSSOM entry point that resolves an
/// element's used-value style declaration — co-located as an HtmlBridge feature module (Phase 3). It
/// resolves the argument's JS wrapper to its canonical element and returns the computed-style object,
/// reached through the narrow <see cref="IComputedStyleHost"/> contract. Previously the bridge's
/// <c>JsRegistrationGetComputedStyle121Core</c> in the shared JsFunctionCallbacks/Registration.cs
/// grab-bag.
/// </summary>
internal static class ComputedStyleBinding
{
    public static JSValue GetComputedStyle(IComputedStyleHost host, in Arguments a)
    {
        if (a.Length == 0)
            return new JSObject();
        var targetObj = a[0] as JSObject;
        var el = targetObj != null ? host.FindDomElementByJSObject(targetObj) : null;
        var pseudoElement = a.Length > 1 ? a[1]?.ToString() : null;
        return host.BuildComputedStyleObject(el, pseudoElement);
    }
}
