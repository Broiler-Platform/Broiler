using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Function;

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
    }

}
