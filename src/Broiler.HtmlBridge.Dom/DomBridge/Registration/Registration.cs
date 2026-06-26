using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.Array.Typed;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Json;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
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
    public DomElement DocumentElement { get; }

    private void RegisterDocument(JSContext context)
    {
        _jsContext = context;
        var document = new JSObject();

        // Map the document JSObject to _documentNode so that ToJSObject(_documentNode) returns
        // the same object as the 'document' variable visible in JS. This ensures
        // strict equality checks like 'range.commonAncestorContainer === document' work.
        _jsObjectCache[_documentNode] = document;

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
        var fetchFn = RegisterFetchAndHttpApis(context, window);
        RegisterWindowGlobals(context, document, window, console, fetchFn);
        RegisterPerformanceObject(context, window);
        RegisterNavigatorObject(context, window);
        RegisterViewportObjects(context, window);
        RegisterContentRenderingPolyfills(context, document);
        RegisterSecurityAndConstructorPolyfills(context, window);
    }

}
