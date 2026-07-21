using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private void RegisterContentRenderingPolyfills(JSContext context, JSObject document)
    {
        // Google Search Compliance content-rendering / fidelity polyfills — Image, IntersectionObserver,
        // ResizeObserver, TextEncoder/TextDecoder, URL/URLSearchParams and AbortController — are a versioned
        // embedded .js asset (Phase 3 work item 6, externalized from inline C# string literals) evaluated
        // once here. See Polyfills/content-rendering-polyfills.js.
        context.Eval(PolyfillAssets.ContentRendering);

        // document.cookie — get/set stub (in-memory, non-persistent). Host-driven (not pure JS), so it stays
        // here rather than in the JS asset. Order-independent of the pure-JS polyfills above.
        var cookieStore = "";
        document.FastAddProperty(
            (KeyString)"cookie",
            new JSFunction((in _) => new JSString(cookieStore), "get cookie"),
            new JSFunction((in a) => Dom.Features.WindowDocumentMiscBinding.SetCookie(ref cookieStore, in a), "set cookie"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
    }

    private void RegisterSecurityAndConstructorPolyfills(JSContext context, JSObject window)
    {
        // window.crypto — the getRandomValues/randomUUID subset (Phase 3: co-located CryptoBinding module)
        var cryptoObj = Dom.Features.CryptoBinding.Build();
        window.FastAddValue((KeyString)"crypto", cryptoObj, JSPropertyAttributes.EnumerableConfigurableValue);
        context["crypto"] = cryptoObj;

        // DOMException constructor
        RegisterDOMException(context);

        // Node constructor with type constants
        RegisterNodeConstructor(context);

        // SVGLength interface constants
        RegisterSVGLength(context);
    }

}
