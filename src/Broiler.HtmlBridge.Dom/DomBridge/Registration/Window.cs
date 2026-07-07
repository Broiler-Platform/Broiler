using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private JSObject RegisterWindowBasics(JSObject document, JSObject window)
    {
        window.FastAddValue(
            (KeyString)"document",
            document,
            JSPropertyAttributes.EnumerableConfigurableValue);
        // window.localStorage — in-memory stub backed by a plain JSObject
        window.FastAddValue(
            (KeyString)"localStorage",
            BuildLocalStorageObject(),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // window.matchMedia(query) — evaluates basic media queries
        window.FastAddValue(
            (KeyString)"matchMedia",
            new JSFunction(JsRegistrationMatchMedia069Core, "matchMedia", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // window.location (read-only)
        var location = new JSObject();
        location.FastAddValue((KeyString)"href", new JSString(_pageUrl), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"protocol", new JSString(_pageProtocol), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"host", new JSString(_pageHost), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"hostname", new JSString(_pageHostName), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"pathname", new JSString(_pagePathName), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"search", new JSString(_pageSearch), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"hash", new JSString(_pageHash), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"origin", new JSString(_pageOrigin), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"location",
            location,
            JSPropertyAttributes.EnumerableConfigurableValue);
        // window.setTimeout(fn, delay) — queues callback for deferred execution
        window.FastAddValue(
            (KeyString)"setTimeout",
            new JSFunction(JsRegistrationSetTimeout070Core, "setTimeout", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // window.clearTimeout(id) — removes queued callback
        window.FastAddValue(
            (KeyString)"clearTimeout",
            new JSFunction(JsRegistrationClearTimeout071Core, "clearTimeout", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // window.setInterval(fn, delay) — queues repeating callback
        window.FastAddValue(
            (KeyString)"setInterval",
            new JSFunction(JsRegistrationSetInterval072Core, "setInterval", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // window.clearInterval(id) — removes interval callback
        window.FastAddValue(
            (KeyString)"clearInterval",
            new JSFunction(JsRegistrationClearInterval073Core, "clearInterval", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // window.requestAnimationFrame(fn) — queues callback for pre-render execution
        window.FastAddValue(
            (KeyString)"requestAnimationFrame",
            new JSFunction(JsRegistrationRequestAnimationFrame074Core, "requestAnimationFrame", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // window.cancelAnimationFrame(id) — removes queued rAF callback
        window.FastAddValue(
            (KeyString)"cancelAnimationFrame",
            new JSFunction(JsRegistrationCancelAnimationFrame075Core, "cancelAnimationFrame", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // window.alert(msg) — logs to debug output
        window.FastAddValue(
            (KeyString)"alert",
            new JSFunction(JsRegistrationAlert076Core, "alert", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // console object (shared between window.console and global console)
        var console = BuildConsoleObject();
        window.FastAddValue(
            (KeyString)"console",
            console,
            JSPropertyAttributes.EnumerableConfigurableValue);
        return console;
    }

    private void RegisterWindowGlobals(JSContext context, JSObject document, JSObject window, JSObject console, JSFunction fetchFn)
    {
        context["window"] = window;
        window.FastAddValue((KeyString)"Event", context["Event"], JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"CustomEvent", context["CustomEvent"], JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"MouseEvent", context["MouseEvent"], JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"FocusEvent", context["FocusEvent"], JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"KeyboardEvent", context["KeyboardEvent"], JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"WheelEvent", context["WheelEvent"], JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"UIEvent", context["UIEvent"], JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"InputEvent", context["InputEvent"], JSPropertyAttributes.EnumerableConfigurableValue);
        // window.parent — uses the JSContext global scope so that parent.X()
        // resolves user-defined globals (e.g. parent.notify() from sub-documents).
        var globalThis = context.Eval("this");
        window.FastAddValue(
            (KeyString)"parent",
            globalThis,
            JSPropertyAttributes.EnumerableConfigurableValue);
        context["parent"] = globalThis;
        // window.self — refers to this window
        window.FastAddValue(
            (KeyString)"self",
            window,
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.defaultView — returns the window object
        document.FastAddValue(
            (KeyString)"defaultView",
            window,
            JSPropertyAttributes.EnumerableConfigurableValue);
        context["console"] = console;
        context["fetch"] = fetchFn;
        // Expose timer functions as globals (matching window.* counterparts)
        context["setTimeout"] = window[(KeyString)"setTimeout"];
        context["clearTimeout"] = window[(KeyString)"clearTimeout"];
        context["setInterval"] = window[(KeyString)"setInterval"];
        context["clearInterval"] = window[(KeyString)"clearInterval"];
        context["requestAnimationFrame"] = window[(KeyString)"requestAnimationFrame"];
        context["cancelAnimationFrame"] = window[(KeyString)"cancelAnimationFrame"];
    }

    private void RegisterPerformanceObject(JSContext context, JSObject window)
    {
        // ---------------------------------------------------------------
        //  Google Search Compliance: Phase 1 (P0) — Critical polyfills
        // ---------------------------------------------------------------

        // TODO-G2: performance object with performance.now() and timeOrigin
        var performanceTimeOrigin = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var performanceObj = new JSObject();
        performanceObj.FastAddValue(
            (KeyString)"timeOrigin",
            new JSNumber(performanceTimeOrigin),
            JSPropertyAttributes.EnumerableConfigurableValue);
        performanceObj.FastAddValue(
            (KeyString)"now",
            new JSFunction((in Arguments _) => JsRegistrationNow122Core(performanceTimeOrigin, in _), "now", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // performance.getEntriesByType() — stub returning empty array
        performanceObj.FastAddValue(
            (KeyString)"getEntriesByType",
            new JSFunction((in Arguments _) => new JSArray(), "getEntriesByType", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // performance.mark() / performance.measure() — no-op stubs
        performanceObj.FastAddValue(
            (KeyString)"mark",
            UndefinedFunction("mark", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        performanceObj.FastAddValue(
            (KeyString)"measure",
            UndefinedFunction("measure", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"performance",
            performanceObj,
            JSPropertyAttributes.EnumerableConfigurableValue);
        context["performance"] = performanceObj;
    }

    private void RegisterNavigatorObject(JSContext context, JSObject window)
    {
        // TODO-G3: navigator object with sendBeacon, userAgent, language, etc.
        var navigatorObj = new JSObject();
        navigatorObj.FastAddValue(
            (KeyString)"userAgent",
            new JSString("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Broiler/1.0"),
            JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue(
            (KeyString)"language",
            new JSString("en-US"),
            JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue(
            (KeyString)"languages",
            new JSArray([new JSString("en-US"), new JSString("en")]),
            JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue(
            (KeyString)"cookieEnabled",
            JSBoolean.True,
            JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue(
            (KeyString)"onLine",
            JSBoolean.True,
            JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue(
            (KeyString)"platform",
            new JSString("Win32"),
            JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue(
            (KeyString)"vendor",
            new JSString(""),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // sendBeacon(url, data) — queues a fire-and-forget POST via fetch semantics
        navigatorObj.FastAddValue(
            (KeyString)"sendBeacon",
            new JSFunction((in Arguments a) => JsRegistrationSendBeacon124Core(window, in a), "sendBeacon", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"navigator",
            navigatorObj,
            JSPropertyAttributes.EnumerableConfigurableValue);
        context["navigator"] = navigatorObj;
        context["postMessage"] = window[(KeyString)"postMessage"];
    }

    private void RegisterViewportObjects(JSContext context, JSObject window)
    {
        // TODO-G4: window.innerWidth / innerHeight
        var vpWidth = _viewportWidth;
        var vpHeight = _viewportHeight;
        window.FastAddProperty(
            (KeyString)"innerWidth",
            new JSFunction((in Arguments _) => new JSNumber(vpWidth), "get innerWidth"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty(
            (KeyString)"innerHeight",
            new JSFunction((in Arguments _) => new JSNumber(vpHeight), "get innerHeight"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty(
            (KeyString)"outerWidth",
            new JSFunction((in Arguments _) => new JSNumber(vpWidth), "get outerWidth"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty(
            (KeyString)"outerHeight",
            new JSFunction((in Arguments _) => new JSNumber(vpHeight), "get outerHeight"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty(
            (KeyString)"scrollX",
            new JSFunction((in Arguments _) => new JSNumber(GetElementScrollOffset(DocumentElement, vertical: false)), "get scrollX"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty(
            (KeyString)"scrollY",
            new JSFunction((in Arguments _) => new JSNumber(GetElementScrollOffset(DocumentElement, vertical: true)), "get scrollY"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty(
            (KeyString)"pageXOffset",
            new JSFunction((in Arguments _) => new JSNumber(GetElementScrollOffset(DocumentElement, vertical: false)), "get pageXOffset"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty(
            (KeyString)"pageYOffset",
            new JSFunction((in Arguments _) => new JSNumber(GetElementScrollOffset(DocumentElement, vertical: true)), "get pageYOffset"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddValue(
            (KeyString)"scroll",
            new JSFunction(JsRegistrationScroll133Core, "scroll", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"scrollTo",
            new JSFunction(JsRegistrationScrollTo134Core, "scrollTo", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"scrollBy",
            new JSFunction(JsRegistrationScrollBy135Core, "scrollBy", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"addEventListener",
            new JSFunction(JsRegistrationAddEventListener136Core, "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"removeEventListener",
            new JSFunction(JsRegistrationRemoveEventListener137Core, "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue(
            (KeyString)"dispatchEvent",
            new JSFunction(JsRegistrationDispatchEvent138Core, "dispatchEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        RegisterWindowMessaging(window);
        window.FastAddProperty(
            (KeyString)"frames",
            new JSFunction((in Arguments _) => BuildWindowFramesArray(), "get frames"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        context["frames"] = BuildWindowFramesArray();
        // window.screen — basic stub for screen dimensions
        var screenObj = new JSObject();
        screenObj.FastAddValue((KeyString)"width", new JSNumber(vpWidth), JSPropertyAttributes.EnumerableConfigurableValue);
        screenObj.FastAddValue((KeyString)"height", new JSNumber(vpHeight), JSPropertyAttributes.EnumerableConfigurableValue);
        screenObj.FastAddValue((KeyString)"availWidth", new JSNumber(vpWidth), JSPropertyAttributes.EnumerableConfigurableValue);
        screenObj.FastAddValue((KeyString)"availHeight", new JSNumber(vpHeight), JSPropertyAttributes.EnumerableConfigurableValue);
        screenObj.FastAddValue((KeyString)"colorDepth", new JSNumber(24), JSPropertyAttributes.EnumerableConfigurableValue);
        screenObj.FastAddValue((KeyString)"pixelDepth", new JSNumber(24), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"screen", screenObj, JSPropertyAttributes.EnumerableConfigurableValue);
        context["screen"] = screenObj;
        var visualViewport = new JSObject();
        _visualViewportJSObject = visualViewport;
        visualViewport.FastAddProperty(
            (KeyString)"width",
            new JSFunction((in Arguments _) => new JSNumber(GetVisualViewportWidth()), "get width"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        visualViewport.FastAddProperty(
            (KeyString)"height",
            new JSFunction((in Arguments _) => new JSNumber(GetVisualViewportHeight()), "get height"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        visualViewport.FastAddProperty(
            (KeyString)"scale",
            new JSFunction((in Arguments _) => new JSNumber(GetVisualViewportScale()), "get scale"),
            new JSFunction(JsRegistrationSetScale143Core, "set scale"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
        visualViewport.FastAddProperty(
            (KeyString)"pageLeft",
            new JSFunction((in Arguments _) => new JSNumber(GetVisualViewportPageOffset(vertical: false)), "get pageLeft"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        visualViewport.FastAddProperty(
            (KeyString)"pageTop",
            new JSFunction((in Arguments _) => new JSNumber(GetVisualViewportPageOffset(vertical: true)), "get pageTop"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        visualViewport.FastAddValue(
            (KeyString)"addEventListener",
            new JSFunction(JsRegistrationAddEventListener146Core, "addEventListener", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        visualViewport.FastAddValue(
            (KeyString)"removeEventListener",
            new JSFunction(JsRegistrationRemoveEventListener147Core, "removeEventListener", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"visualViewport", visualViewport, JSPropertyAttributes.EnumerableConfigurableValue);
        context["visualViewport"] = visualViewport;
    }

}
