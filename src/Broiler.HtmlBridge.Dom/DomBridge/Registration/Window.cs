using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Null;
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
        window.FastAddValue((KeyString)"document", document, JSPropertyAttributes.EnumerableConfigurableValue);

        // window.localStorage — in-memory stub backed by a plain JSObject
        window.FastAddValue((KeyString)"localStorage", Dom.Features.WebStorageBinding.BuildLocalStorage(), JSPropertyAttributes.EnumerableConfigurableValue);

        // window.matchMedia(query) — evaluates basic media queries
        window.FastAddValue((KeyString)"matchMedia", new DomFunction((in a) => Dom.Features.MatchMediaBinding.MatchMedia(this, in a), "matchMedia", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        // window.location — the URL components here, and the navigation surface (`href`, assign,
        // replace, reload, toString) from LocationBinding. The components alone made
        // `location.replace(url)` a TypeError ("undefined is not a function") which aborts the rest
        // of the calling function, so they are built together: see LocationBinding for what the
        // four do in a capture, and why they do not navigate.
        var location = new JSObject();
        location.FastAddValue((KeyString)"protocol", new JSString(_pageProtocol), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"host", new JSString(_pageHost), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"hostname", new JSString(_pageHostName), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"port", new JSString(_pagePort), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"pathname", new JSString(_pagePathName), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"search", new JSString(_pageSearch), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"hash", new JSString(_pageHash), JSPropertyAttributes.EnumerableConfigurableValue);
        location.FastAddValue((KeyString)"origin", new JSString(_pageOrigin), JSPropertyAttributes.EnumerableConfigurableValue);
        Dom.Features.LocationBinding.AddNavigationSurface(location, _pageUrl);

        window.FastAddValue((KeyString)"location", location, JSPropertyAttributes.EnumerableConfigurableValue);

        // document.location is the *same* Location as window.location (HTML §3.1.5: the getter
        // returns this document's relevant global object's Location), so it is the one object
        // registered on both rather than a copy — pages compare the two, and `document.location`
        // is the spelling half of them reach for. Undefined here, it did not read as a missing
        // property but threw "Cannot get property protocol of undefined" out of the first script
        // to ask an origin of it, which aborts the whole <script>. That is the same One-Google-bar
        // bundle `top` above died in — `dF=function(){var a=document.location;return
        // a.protocol+"//"+a.host}` is how it builds its own origin — so it is the next thing that
        // failed once `top` resolved.
        document.FastAddValue((KeyString)"location", location, JSPropertyAttributes.EnumerableConfigurableValue);

        // window timers / animation frames — thin adapters over the P2.4 BrowserEventLoop, co-located
        // in the TimerBinding feature module (Phase 3).
        window.FastAddValue((KeyString)"setTimeout", new DomFunction((in a) => Dom.Features.TimerBinding.SetTimeout(_eventLoop, in a), "setTimeout", 2), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"clearTimeout", new DomFunction((in a) => Dom.Features.TimerBinding.ClearTimeout(_eventLoop, in a), "clearTimeout", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"setInterval", new DomFunction((in a) => Dom.Features.TimerBinding.SetInterval(_eventLoop, in a), "setInterval", 2), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"clearInterval", new DomFunction((in a) => Dom.Features.TimerBinding.ClearInterval(_eventLoop, in a), "clearInterval", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"requestAnimationFrame", new DomFunction((in a) => Dom.Features.TimerBinding.RequestAnimationFrame(_eventLoop, in a), "requestAnimationFrame", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"cancelAnimationFrame", new DomFunction((in a) => Dom.Features.TimerBinding.CancelAnimationFrame(_eventLoop, in a), "cancelAnimationFrame", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        // window.alert(msg) — logs to debug output
        window.FastAddValue((KeyString)"alert", new DomFunction(Dom.Features.WindowDocumentMiscBinding.Alert, "alert", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        // btoa / atob — the WindowOrWorkerGlobalScope base64 pair (HTML §8.3), co-located in the
        // Base64Binding feature module. The window IS the global object, so registering here is
        // what makes the unqualified `atob(…)` a page writes resolve as well.
        window.FastAddValue((KeyString)"btoa", new DomFunction((in a) => Dom.Features.Base64Binding.Btoa(_jsContext!, in a), "btoa", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"atob", new DomFunction((in a) => Dom.Features.Base64Binding.Atob(_jsContext!, in a), "atob", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        // console object (shared between window.console and global console)
        var console = Dom.Features.ConsoleBinding.Build();
        window.FastAddValue((KeyString)"console", console, JSPropertyAttributes.EnumerableConfigurableValue);

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
        window.FastAddValue((KeyString)"parent", globalThis, JSPropertyAttributes.EnumerableConfigurableValue);
        context["parent"] = globalThis;

        // window.self — refers to this window
        window.FastAddValue((KeyString)"self", window, JSPropertyAttributes.EnumerableConfigurableValue);

        // window.top — the topmost browsing context. This document is the top-level one, so
        // `top`, `parent` and `self` are all this window; a sub-document's window instead gets
        // a `top` pointing back here (SubWindowBinding). It was the one member of that trio
        // never registered, and because `window` IS the global object that made the unqualified
        // `top` a ReferenceError rather than merely an undefined property — which aborts the
        // whole <script>, not just the statement that read it. Framing checks spell it
        // unqualified (`if (top != self)`), so this is the first thing a page's boilerplate
        // touches: google.com's One-Google-bar bundle died on it, taking with it every listener
        // the rest of that script would have registered.
        window.FastAddValue((KeyString)"top", globalThis, JSPropertyAttributes.EnumerableConfigurableValue);
        context["top"] = globalThis;

        // document.defaultView — returns the window object
        document.FastAddValue((KeyString)"defaultView", window, JSPropertyAttributes.EnumerableConfigurableValue);
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
        performanceObj.FastAddValue((KeyString)"timeOrigin", new JSNumber(performanceTimeOrigin), JSPropertyAttributes.EnumerableConfigurableValue);
        performanceObj.FastAddValue((KeyString)"now", new DomFunction((in _) => Dom.Features.WindowDocumentMiscBinding.PerformanceNow(performanceTimeOrigin, in _), "now", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        // performance.getEntriesByType() — stub returning empty array
        performanceObj.FastAddValue((KeyString)"getEntriesByType", new DomFunction((in _) => new JSArray(), "getEntriesByType", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        // performance.mark() / performance.measure() — no-op stubs
        performanceObj.FastAddValue((KeyString)"mark", UndefinedFunction("mark", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        performanceObj.FastAddValue((KeyString)"measure", UndefinedFunction("measure", 3), JSPropertyAttributes.EnumerableConfigurableValue);

        window.FastAddValue((KeyString)"performance", performanceObj, JSPropertyAttributes.EnumerableConfigurableValue);
        context["performance"] = performanceObj;
    }

    /// <summary>
    /// <c>window.history</c> (HTML §7.2.3). A capture never leaves the page it was given, so the
    /// session is one entry long and the traversal methods do nothing; what matters is that the
    /// object and its members <em>exist</em>.
    /// </summary>
    /// <remarks>
    /// Absent, it did not read as a missing feature — reading through it threw "Cannot get
    /// property replaceState of undefined", which aborts the function that asked and, with it,
    /// whatever that function was in the middle of setting up. It is boilerplate in every router
    /// and analytics bundle, so the abort lands early: on <c>www.mediawiki.org</c> it took out
    /// <c>skins.vector.js</c>, and the skin then never applied the preferences that decide which
    /// of its two appearance panels is shown — leaving both in the page and the article a
    /// panel's height too far down.
    /// </remarks>
    private void RegisterHistoryObject(JSContext context, JSObject window)
    {
        var history = new JSObject();

        history.FastAddValue((KeyString)"length", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        history.FastAddValue((KeyString)"state", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        history.FastAddValue((KeyString)"scrollRestoration", new JSString("auto"), JSPropertyAttributes.EnumerableConfigurableValue);

        // pushState/replaceState record the state the page hands them, because a page that writes
        // one commonly reads it straight back; neither changes the document's URL, which a capture
        // has no way to honour.
        history.FastAddValue((KeyString)"pushState", new DomFunction((in a) => StoreHistoryState(history, in a), "pushState", 3), JSPropertyAttributes.EnumerableConfigurableValue);
        history.FastAddValue((KeyString)"replaceState", new DomFunction((in a) => StoreHistoryState(history, in a), "replaceState", 3), JSPropertyAttributes.EnumerableConfigurableValue);

        history.FastAddValue((KeyString)"back", UndefinedFunction("back", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        history.FastAddValue((KeyString)"forward", UndefinedFunction("forward", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        history.FastAddValue((KeyString)"go", UndefinedFunction("go", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        window.FastAddValue((KeyString)"history", history, JSPropertyAttributes.EnumerableConfigurableValue);
        context["history"] = history;
    }

    private static JSValue StoreHistoryState(JSObject history, in Arguments arguments)
    {
        history.FastAddValue(
            (KeyString)"state",
            arguments.Length > 0 ? arguments[0] : JSNull.Value,
            JSPropertyAttributes.EnumerableConfigurableValue);

        return JSUndefined.Value;
    }

    /// <summary>
    /// <c>PerformanceObserver</c> (Performance Timeline §2) and <c>requestIdleCallback</c>
    /// (Background Tasks §2), as the shapes a page needs them to have rather than as working
    /// instrumentation: a headless capture produces no performance entries to deliver, and its
    /// event loop has no idle period to wait for.
    /// </summary>
    /// <remarks>
    /// A missing constructor is worse than an inert one here. Telemetry bundles construct one at
    /// module scope, so <c>new PerformanceObserver(…)</c> threw a ReferenceError that rejected the
    /// promise the module was resolving, and every module waiting on that one stayed unresolved —
    /// which is how a page can lose behaviour that has nothing to do with performance timing.
    /// <c>requestIdleCallback</c> runs its callback on a timer instead of dropping it, because
    /// what pages defer to idle is often the work that produces visible content.
    /// </remarks>
    private void RegisterObservationStubs(JSContext context, JSObject window)
    {
        if (window[(KeyString)"PerformanceObserver"] is not JSUndefined)
            return;

        var observerPrototype = new JSObject();
        observerPrototype.FastAddValue((KeyString)"observe", UndefinedFunction("observe", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        observerPrototype.FastAddValue((KeyString)"disconnect", UndefinedFunction("disconnect", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        observerPrototype.FastAddValue((KeyString)"takeRecords", new DomFunction((in _) => new JSArray(), "takeRecords", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        var performanceObserver = new JSFunction((in _) =>
        {
            var instance = new JSObject();
            instance.FastAddValue((KeyString)"observe", UndefinedFunction("observe", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            instance.FastAddValue((KeyString)"disconnect", UndefinedFunction("disconnect", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            instance.FastAddValue((KeyString)"takeRecords", new DomFunction((in _) => new JSArray(), "takeRecords", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            return instance;
        }, "PerformanceObserver", 1);

        performanceObserver.FastAddValue((KeyString)"prototype", observerPrototype, JSPropertyAttributes.ConfigurableValue);

        // Feature detection reads this before observing, and an observer that claims to support
        // nothing is the honest answer for a capture that reports no entries.
        performanceObserver.FastAddValue((KeyString)"supportedEntryTypes", new JSArray(), JSPropertyAttributes.EnumerableConfigurableValue);

        window.FastAddValue((KeyString)"PerformanceObserver", performanceObserver, JSPropertyAttributes.EnumerableConfigurableValue);
        context["PerformanceObserver"] = performanceObserver;

        if (window[(KeyString)"requestIdleCallback"] is JSUndefined)
        {
            var requestIdle = new DomFunction((in a) => Dom.Features.TimerBinding.SetTimeout(_eventLoop, in a), "requestIdleCallback", 1);
            window.FastAddValue((KeyString)"requestIdleCallback", requestIdle, JSPropertyAttributes.EnumerableConfigurableValue);
            context["requestIdleCallback"] = requestIdle;

            var cancelIdle = new DomFunction((in a) => Dom.Features.TimerBinding.ClearTimeout(_eventLoop, in a), "cancelIdleCallback", 1);
            window.FastAddValue((KeyString)"cancelIdleCallback", cancelIdle, JSPropertyAttributes.EnumerableConfigurableValue);
            context["cancelIdleCallback"] = cancelIdle;
        }
    }

    private void RegisterNavigatorObject(JSContext context, JSObject window)
    {
        // TODO-G3: navigator object with sendBeacon, userAgent, language, etc.
        var navigatorObj = new JSObject();
        // The same string the network sees, rather than a second copy of it: a page that compares
        // what it was told with what its own fetches report is entitled to one answer.
        navigatorObj.FastAddValue((KeyString)"userAgent", new JSString(Layout.Net.BroilerUserAgent.Value), JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue((KeyString)"language", new JSString("en-US"), JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue((KeyString)"languages", new JSArray([new JSString("en-US"), new JSString("en")]), JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue((KeyString)"cookieEnabled", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue((KeyString)"onLine", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue((KeyString)"platform", new JSString("Win32"), JSPropertyAttributes.EnumerableConfigurableValue);
        navigatorObj.FastAddValue((KeyString)"vendor", new JSString(""), JSPropertyAttributes.EnumerableConfigurableValue);

        // sendBeacon(url, data) — queues a fire-and-forget POST via fetch semantics
        navigatorObj.FastAddValue((KeyString)"sendBeacon", new DomFunction((in a) => Dom.Features.BeaconBinding.Send(window, in a), "sendBeacon", 2), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"navigator", navigatorObj, JSPropertyAttributes.EnumerableConfigurableValue);

        context["navigator"] = navigatorObj;
        context["postMessage"] = window[(KeyString)"postMessage"];
    }

    private void RegisterViewportObjects(JSContext context, JSObject window)
    {
        // TODO-G4: window.innerWidth / innerHeight
        var vpWidth = _viewportWidth;
        var vpHeight = _viewportHeight;

        window.FastAddProperty((KeyString)"innerWidth", new DomFunction((in _) => new JSNumber(vpWidth), "get innerWidth"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty((KeyString)"innerHeight", new DomFunction((in _) => new JSNumber(vpHeight), "get innerHeight"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty((KeyString)"outerWidth", new DomFunction((in _) => new JSNumber(vpWidth), "get outerWidth"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty((KeyString)"outerHeight", new DomFunction((in _) => new JSNumber(vpHeight), "get outerHeight"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty((KeyString)"scrollX", new DomFunction((in _) => new JSNumber(GetElementScrollOffset(DocumentElement, vertical: false)), "get scrollX"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty((KeyString)"scrollY", new DomFunction((in _) => new JSNumber(GetElementScrollOffset(DocumentElement, vertical: true)), "get scrollY"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty((KeyString)"pageXOffset", new DomFunction((in _) => new JSNumber(GetElementScrollOffset(DocumentElement, vertical: false)), "get pageXOffset"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        window.FastAddProperty((KeyString)"pageYOffset", new DomFunction((in _) => new JSNumber(GetElementScrollOffset(DocumentElement, vertical: true)), "get pageYOffset"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // window scroll / scrollTo / scrollBy, co-located in the WindowScrollBinding feature module (Phase 3).
        window.FastAddValue((KeyString)"scroll", new DomFunction((in a) => Dom.Features.WindowScrollBinding.Scroll(this, in a), "scroll", 2), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"scrollTo", new DomFunction((in a) => Dom.Features.WindowScrollBinding.ScrollTo(this, in a), "scrollTo", 2), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"scrollBy", new DomFunction((in a) => Dom.Features.WindowScrollBinding.ScrollBy(this, in a), "scrollBy", 2), JSPropertyAttributes.EnumerableConfigurableValue);
        // window addEventListener / removeEventListener / dispatchEvent, co-located in the
        // WindowEventTargetBinding feature module (Phase 3). These reach the global object — so
        // the idiomatic unqualified `addEventListener("load", …)` registers a window listener,
        // as it does in a browser — through MirrorWindowMembersOntoGlobal, which shares the
        // identical function objects so the two spellings address one listener store.
        window.FastAddValue((KeyString)"addEventListener", new DomFunction((in a) => Dom.Features.WindowEventTargetBinding.AddEventListener(this, in a), "addEventListener", 3), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"removeEventListener", new DomFunction((in a) => Dom.Features.WindowEventTargetBinding.RemoveEventListener(this, in a), "removeEventListener", 3), JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"dispatchEvent", new DomFunction((in a) => Dom.Features.WindowEventTargetBinding.DispatchEvent(this, in a), "dispatchEvent", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        _messaging.RegisterWindowMessaging(window);

        // `frames` is the one member registered twice with *different* shapes: a live getter on the
        // window and, historically, a static snapshot on the global for the unqualified spelling.
        // Now that the window IS the global the second write would simply overwrite the first,
        // freezing `frames` to whatever existed before any <iframe> was scripted. The accessor is
        // the correct one for both spellings, so it is the only registration.
        window.FastAddProperty((KeyString)"frames", new DomFunction((in _) => BuildWindowFramesArray(), "get frames"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

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
        visualViewport.FastAddProperty((KeyString)"width", new DomFunction((in _) => new JSNumber(GetVisualViewportWidth()), "get width"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        visualViewport.FastAddProperty((KeyString)"height", new DomFunction((in _) => new JSNumber(GetVisualViewportHeight()), "get height"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        visualViewport.FastAddProperty((KeyString)"scale", new DomFunction((in _) => new JSNumber(GetVisualViewportScale()), "get scale"), new DomFunction((in a) => Dom.Features.WindowDocumentMiscBinding.SetVisualViewportScale(this, in a), "set scale"), JSPropertyAttributes.EnumerableConfigurableProperty);
        visualViewport.FastAddProperty((KeyString)"pageLeft", new DomFunction((in _) => new JSNumber(GetVisualViewportPageOffset(vertical: false)), "get pageLeft"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        visualViewport.FastAddProperty((KeyString)"pageTop", new DomFunction((in _) => new JSNumber(GetVisualViewportPageOffset(vertical: true)), "get pageTop"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // visualViewport addEventListener / removeEventListener (scroll), co-located in the
        // VisualViewportEventTargetBinding feature module (Phase 3).
        visualViewport.FastAddValue((KeyString)"addEventListener", new DomFunction((in a) => Dom.Features.VisualViewportEventTargetBinding.AddEventListener(this, in a), "addEventListener", 2), JSPropertyAttributes.EnumerableConfigurableValue);
        visualViewport.FastAddValue((KeyString)"removeEventListener", new DomFunction((in a) => Dom.Features.VisualViewportEventTargetBinding.RemoveEventListener(this, in a), "removeEventListener", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        window.FastAddValue((KeyString)"visualViewport", visualViewport, JSPropertyAttributes.EnumerableConfigurableValue);
        context["visualViewport"] = visualViewport;
    }

}
