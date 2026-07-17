using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.HtmlBridge.Logging;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The residual thin window/document singletons (Phase 3) — the last callbacks peeled out of the
/// JsFunctionCallbacks/Registration.cs grab-bag so that file carries no loose JS callback. These are
/// genuinely independent one-offs (each ≤10 lines) that do not individually warrant their own feature
/// module, so they are collected here — <b>not</b> a god-object grab-bag: there is no shared mutable
/// state, and the two that touch the bridge do so only through the narrow
/// <see cref="IWindowDocumentMiscHost"/> contract; the rest are stateless (or take a by-ref store):
/// <list type="bullet">
///   <item><c>window.alert</c> — logs to debug output (headless; no UI dialog).</item>
///   <item><c>performance.now</c> — milliseconds since the performance time origin.</item>
///   <item><c>window.visualViewport.scale</c> setter.</item>
///   <item><c>document.contentType</c> getter (XHTML vs HTML by page URL).</item>
///   <item><c>document.cookie</c> setter (simplified append; the getter lives elsewhere).</item>
/// </list>
/// Previously the bridge's <c>JsRegistrationAlert076Core</c>/<c>Now122Core</c>/<c>SetScale143Core</c>/
/// <c>GetContentType063Core</c>/<c>SetCookie149Core</c>.
/// </summary>
internal static class WindowDocumentMiscBinding
{
    public static JSValue Alert(in Arguments a)
    {
        var msg = a.Length > 0 ? a[0].ToString() : string.Empty;
        RenderLogger.LogDebug(LogCategory.JavaScript, "DomBridge.alert", msg);
        return JSUndefined.Value;
    }

    public static JSValue PerformanceNow(long performanceTimeOrigin, in Arguments a)
        => new JSNumber(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - performanceTimeOrigin);

    public static JSValue SetVisualViewportScale(IWindowDocumentMiscHost host, in Arguments a)
    {
        if (a.Length > 0)
            host.SetVisualViewportScale(a[0].DoubleValue);
        return JSUndefined.Value;
    }

    public static JSValue GetContentType(IWindowDocumentMiscHost host, in Arguments a)
    {
        var url = host.PageUrl;
        if (url.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase)
            || url.EndsWith(".xht", StringComparison.OrdinalIgnoreCase)
            || url.Contains("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
            return new JSString("application/xhtml+xml");
        return new JSString("text/html");
    }

    public static JSValue SetCookie(ref string? cookieStore, in Arguments a)
    {
        if (a.Length > 0)
        {
            var val = a[0].ToString();
            // Simplified: just append the cookie value (real browsers parse/update).
            if (!string.IsNullOrEmpty(cookieStore))
                cookieStore += "; " + val;
            else
                cookieStore = val;
        }

        return JSUndefined.Value;
    }
}
