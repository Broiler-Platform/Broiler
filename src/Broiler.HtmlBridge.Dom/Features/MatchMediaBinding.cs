using Broiler.CSS.Dom;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.String;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// <c>window.matchMedia(query)</c>, co-located as an HtmlBridge feature module (Phase 3). It
/// evaluates the query against the current viewport via the canonical
/// <see cref="CssStyleEngine.MatchesMediaQuery"/> and returns a <c>MediaQueryList</c>-shaped object
/// (<c>matches</c>/<c>media</c> plus no-op legacy <c>addListener</c>/<c>removeListener</c> stubs).
/// The only bridge coupling is the live viewport, reached through the narrow
/// <see cref="IMatchMediaHost"/> contract. Previously the bridge's
/// <c>JsRegistrationMatchMedia069Core</c> in the shared JsFunctionCallbacks/Registration.cs grab-bag,
/// with its media-query evaluation in the (now removed) <c>DomBridge.EvaluateMediaQuery</c> wrapper.
/// </summary>
internal static class MatchMediaBinding
{
    public static JSValue MatchMedia(IMatchMediaHost host, in Arguments a)
    {
        var query = a.Length > 0 ? a[0].ToString() : string.Empty;
        var matches = !string.IsNullOrEmpty(query)
            && CssStyleEngine.MatchesMediaQuery(query, new CssEnvironment(host.ViewportWidth, host.ViewportHeight));

        var result = new JSObject();
        result.FastAddValue((KeyString)"matches", matches ? JSBoolean.True : JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        result.FastAddValue((KeyString)"media", new JSString(query), JSPropertyAttributes.EnumerableConfigurableValue);
        // addListener / removeListener stubs (the legacy MediaQueryList API) — no-ops.
        result.FastAddValue((KeyString)"addListener", NoOp("addListener"), JSPropertyAttributes.EnumerableConfigurableValue);
        result.FastAddValue((KeyString)"removeListener", NoOp("removeListener"), JSPropertyAttributes.EnumerableConfigurableValue);
        return result;
    }

    private static JSFunction NoOp(string name) => new((in _) => JSUndefined.Value, name, 1);
}
