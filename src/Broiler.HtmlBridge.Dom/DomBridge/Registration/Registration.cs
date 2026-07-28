using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.HtmlBridge.Logging;

namespace Broiler.HtmlBridge;

/// <summary>
/// JavaScript bridge registration — wires up the <c>document</c>,
/// <c>window</c>, <c>console</c>, and <c>XMLHttpRequest</c> globals
/// on the YantraJS <see cref="JSContext"/>.
/// </summary>
public sealed partial class DomBridge
{
    // ------------------------------------------------------------------
    //  JavaScript bridge
    // ------------------------------------------------------------------


    /// <summary>
    /// The element backing <c>document.documentElement</c> (the &lt;html&gt; element).
    /// </summary>
    public Broiler.Dom.DomElement DocumentElement { get; }

    private void RegisterDocument(JSContext context)
    {
        _jsContext = context;
        var document = new JSObject();

        // Map the document JSObject to the canonical DomDocument so that ToJSObject(_document)
        // returns the same object as the 'document' variable visible in JS. This ensures
        // strict equality checks like 'range.commonAncestorContainer === document' work.
        _jsObjects.Set(_document, document);

        RegisterDocumentBasics(context, document);
        RegisterDocumentEventsAndMutationObservers(context);
        RegisterDocumentWriting(document);
        RegisterDocumentTraversalApis(context, document);
        RegisterDocumentNodeAndCollectionApis(context, document);
        RegisterDocumentEventTargetAndMetadata(document);

        _documentJSObject = document;
        context["document"] = document;

        var window = new JSObject();
        _windowJSObject = window;

        var console = RegisterWindowBasics(document, window);
        var fetchFn = _fetch.Install(context, window);
        // MessageChannel (messaging) and getComputedStyle (CSSOM) historically lived inside the fetch
        // registration; they are registered here alongside the other window globals now that the fetch
        // networking surface is an isolated feature module.
        var messageChannelCtor = new JSFunction((in _) => _messaging.CreateMessageChannel(), "MessageChannel", 0);
        window.FastAddValue((KeyString)"MessageChannel", messageChannelCtor, JSPropertyAttributes.EnumerableConfigurableValue);
        context["MessageChannel"] = messageChannelCtor;
        // CSSStyleSheet constructor (constructable stylesheets / adoptedStyleSheets — CSSOM).
        var cssStyleSheetCtor = new JSFunction((in a) => CreateConstructedStyleSheet(in a), "CSSStyleSheet", 0);
        window.FastAddValue((KeyString)"CSSStyleSheet", cssStyleSheetCtor, JSPropertyAttributes.EnumerableConfigurableValue);
        context["CSSStyleSheet"] = cssStyleSheetCtor;
        // getComputedStyle (CSSOM), co-located in the ComputedStyleBinding feature module (Phase 3).
        window.FastAddValue(
            (KeyString)"getComputedStyle",
            new JSFunction((in a) => Dom.Features.ComputedStyleBinding.GetComputedStyle(this, in a), "getComputedStyle", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        RegisterWindowGlobals(context, document, window, console, fetchFn);
        RegisterPerformanceObject(context, window);
        RegisterNavigatorObject(context, window);
        RegisterViewportObjects(context, window);
        RegisterContentRenderingPolyfills(context, document);
        RegisterSecurityAndConstructorPolyfills(context, window);
        MirrorWindowMembersOntoGlobal(context, window);
    }

    /// <summary>
    /// Copies every own property of <c>window</c> that the global object does not already have
    /// onto the global, preserving each property's descriptor.
    /// <para>
    /// In a browser <c>window</c> <em>is</em> the global object, so <c>getComputedStyle(el)</c>,
    /// <c>location.href</c>, <c>innerWidth</c> and <c>scrollTo(…)</c> are all valid unqualified.
    /// Here the two are distinct objects, so an unqualified reference to a member that lived only
    /// on <c>window</c> raised a <c>ReferenceError</c> — which does not merely skip that one
    /// statement, it aborts the whole script, taking every later statement and every listener the
    /// script would have registered with it. Unqualified spellings are idiomatic in the WPT
    /// corpus, so this silently emptied entire test pages.
    /// </para>
    /// <para>
    /// The mirror list used to be maintained by hand, one <c>context["x"] = window["x"]</c> at a
    /// time (see the timer globals in RegisterWindowGlobals), and had drifted: <c>localStorage</c>,
    /// <c>matchMedia</c>, <c>location</c>, <c>alert</c>, <c>getComputedStyle</c>, <c>self</c>,
    /// <c>innerWidth</c>/<c>innerHeight</c>, <c>outerWidth</c>/<c>outerHeight</c>,
    /// <c>scrollX</c>/<c>scrollY</c>, <c>pageXOffset</c>/<c>pageYOffset</c> and
    /// <c>scroll</c>/<c>scrollTo</c>/<c>scrollBy</c> were all missing. Sweeping instead of listing
    /// keeps the two in step as window members are added.
    /// </para>
    /// <para>
    /// Runs last, after every Register* pass, so it sees the fully-built window. It copies
    /// descriptors rather than values, so accessor-backed members (<c>innerWidth</c> and friends)
    /// stay live getters rather than freezing to a snapshot, and value members share the identical
    /// object — a listener added through the global <c>addEventListener</c> is therefore removable
    /// through <c>window.removeEventListener</c>. Properties the global already owns are left
    /// alone, so engine builtins and the explicit aliases above always win.
    /// </para>
    /// </summary>
    private static void MirrorWindowMembersOntoGlobal(JSContext context, JSObject window)
    {
        context["__broilerWindowForGlobalMirror"] = window;
        try
        {
            context.Eval(@"
(function() {
  var w = __broilerWindowForGlobalMirror;
  var g = globalThis;
  var names = Object.getOwnPropertyNames(w);
  for (var i = 0; i < names.length; i++) {
    var name = names[i];
    if (name in g) continue;
    var descriptor = Object.getOwnPropertyDescriptor(w, name);
    if (!descriptor) continue;
    // A member that resists definition on the global (a frozen builtin slot, say) is
    // skipped rather than aborting the sweep for every member after it.
    try { Object.defineProperty(g, name, descriptor); } catch (e) {}
  }
})();");
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.MirrorWindowMembersOntoGlobal",
                $"Error mirroring window members onto the global object: {ex.Message}", ex);
        }
        finally
        {
            context.Eval("delete globalThis.__broilerWindowForGlobalMirror;");
        }
    }

}
