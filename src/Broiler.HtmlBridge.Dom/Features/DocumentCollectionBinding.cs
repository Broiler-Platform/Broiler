using Broiler.Dom;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Array;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The <c>document</c> live-collection accessors — <c>document.forms</c>, <c>document.images</c>,
/// <c>document.links</c>, <c>document.styleSheets</c> — co-located as an HtmlBridge feature module
/// (Phase 3). Each scans the document for the relevant elements and returns a JS array of their
/// wrappers (<c>forms</c> additionally exposes named access by the form's <c>name</c> attribute;
/// <c>styleSheets</c> returns stylesheet objects rather than element wrappers). The document root,
/// element list, wrapper factory, tree-order link collector and stylesheet-object builder are reached
/// through the <see cref="IDocumentCollectionHost"/> contract; attribute reads use the bridge's
/// neutral <c>internal static</c> <c>TryGetAttribute</c> directly. Previously the bridge's
/// <c>JsRegistrationGetForms050Core</c> etc. in the shared JsFunctionCallbacks/Registration.cs
/// grab-bag.
/// </summary>
internal static class DocumentCollectionBinding
{
    public static JSValue GetForms(IDocumentCollectionHost host, in Arguments a)
    {
        var results = new List<JSValue>();
        foreach (var el in host.Elements)
        {
            if (string.Equals(el.TagName, "form", StringComparison.OrdinalIgnoreCase))
                results.Add(host.ToJSObject(el));
        }

        var arr = new JSArray(results);
        // Add named access: forms with a 'name' attribute can be accessed as properties of the
        // collection.
        foreach (var el in host.Elements)
        {
            if (string.Equals(el.TagName, "form", StringComparison.OrdinalIgnoreCase))
            {
                if (DomBridge.TryGetAttribute(el, "name", out var formName) && !string.IsNullOrEmpty(formName))
                    arr.FastAddValue((KeyString)formName, host.ToJSObject(el), JSPropertyAttributes.EnumerableConfigurableValue);
            }
        }

        return arr;
    }

    public static JSValue GetImages(IDocumentCollectionHost host, in Arguments a)
    {
        var results = new List<JSValue>();
        foreach (var el in host.Elements)
        {
            if (string.Equals(el.TagName, "img", StringComparison.OrdinalIgnoreCase))
                results.Add(host.ToJSObject(el));
        }

        return new JSArray(results);
    }

    public static JSValue GetLinks(IDocumentCollectionHost host, in Arguments a)
    {
        var results = new List<JSValue>();
        host.CollectLinksInTreeOrder(host.DocumentElement, results);
        return new JSArray(results);
    }

    public static JSValue GetStyleSheets(IDocumentCollectionHost host, in Arguments a)
    {
        var styleEls = new List<DomElement>();
        foreach (var el in host.Elements)
        {
            if (string.Equals(el.TagName, "style", StringComparison.OrdinalIgnoreCase))
                styleEls.Add(el);
        }

        var arr = new JSArray();
        foreach (var styleEl in styleEls)
            arr.Add(host.BuildStyleSheetObject(styleEl));
        return arr;
    }
}
