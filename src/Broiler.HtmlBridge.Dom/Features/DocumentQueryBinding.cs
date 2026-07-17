using System.Linq;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Null;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The <c>document</c> element-query methods — <c>getElementById</c>, <c>getElementsByTagName</c>,
/// <c>getElementsByClassName</c>, <c>querySelector</c>, <c>querySelectorAll</c> — co-located as an
/// HtmlBridge feature module (Phase 3). Each searches the document tree and returns the matching
/// element's JS wrapper (or a live-array of wrappers). The document root, element list and wrapper
/// factory are reached through the narrow <see cref="IDocumentQueryHost"/> contract; sub-tree search
/// (<c>FindInSubTree</c>) and selector matching (<c>MatchesSelector</c>) are the bridge's neutral
/// <c>internal static</c> helpers, called directly. Previously the bridge's
/// <c>JsRegistrationGetElementById006Core</c> etc. in the shared JsFunctionCallbacks/Registration.cs
/// grab-bag. Hit-testing (<c>elementFromPoint</c>/<c>elementsFromPoint</c>), the structural
/// accessors (<c>body</c>/<c>head</c>/<c>title</c>) and the live collections
/// (<c>forms</c>/<c>images</c>/<c>links</c>/<c>styleSheets</c>) are separate concerns, not part of
/// this slice.
/// </summary>
internal static class DocumentQueryBinding
{
    public static JSValue GetElementById(IDocumentQueryHost host, in Arguments a)
    {
        var id = a.Length > 0 ? a[0].ToString() : string.Empty;
        var found = DomBridge.FindInSubTree(host.DocumentElement, el => el.Id == id);
        return found != null ? host.ToJSObject(found) : JSNull.Value;
    }

    public static JSValue GetElementsByTagName(IDocumentQueryHost host, in Arguments a)
    {
        var tag = a.Length > 0 ? a[0].ToString().ToLowerInvariant() : string.Empty;
        var results = new List<JSValue>();
        foreach (var el in host.Elements)
        {
            if (tag == "*" || el.TagName == tag)
                results.Add(host.ToJSObject(el));
        }

        return new JSArray(results);
    }

    public static JSValue GetElementsByClassName(IDocumentQueryHost host, in Arguments a)
    {
        var className = a.Length > 0 ? a[0].ToString() : string.Empty;
        var results = new List<JSValue>();
        foreach (var el in host.Elements)
        {
            var classes = new HashSet<string>((el.ClassName ?? string.Empty).Split(' ').Where(s => s.Length > 0), StringComparer.Ordinal);
            if (classes.Contains(className))
                results.Add(host.ToJSObject(el));
        }

        return new JSArray(results);
    }

    public static JSValue QuerySelector(IDocumentQueryHost host, in Arguments a)
    {
        var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
        foreach (var el in host.Elements)
        {
            if (DomBridge.MatchesSelector(el, selector))
                return host.ToJSObject(el);
        }

        return JSNull.Value;
    }

    public static JSValue QuerySelectorAll(IDocumentQueryHost host, in Arguments a)
    {
        var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
        var results = new List<JSValue>();
        foreach (var el in host.Elements)
        {
            if (DomBridge.MatchesSelector(el, selector))
                results.Add(host.ToJSObject(el));
        }

        return new JSArray(results);
    }
}
