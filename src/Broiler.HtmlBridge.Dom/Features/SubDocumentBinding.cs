using System.Text;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The nested-browsing-context <c>document</c> object feature binding (HtmlBridge complexity-reduction
/// roadmap Phase 3, P3.13) — the JS <c>document</c> surface built over a sub-document root node
/// (an <c>&lt;iframe&gt;</c>/<c>&lt;object&gt;</c>/<c>&lt;frame&gt;</c> content document, a
/// <c>createDocument</c>/<c>createHTMLDocument</c> result, or the <c>DOMImplementation</c> factories on
/// the main document): documentElement/body/head/title/forms/childNodes, getElementById/
/// getElementsByTagName/querySelector(All)/elementFromPoint(s), createElement/TextNode/Comment/
/// ElementNS/Event, open/write, images/links/styleSheets, appendChild/removeChild/append/prepend,
/// <c>document.implementation</c> and createRange/TreeWalker/NodeIterator.
/// <para>
/// This slice is what P4.4b unblocked: after the <c>#subdoc-root</c> sentinel was severed, a
/// sub-document root is a canonical <see cref="Broiler.Dom.DomNode"/>/<see cref="Broiler.Dom.DomDocument"/>,
/// so the whole surface operates cleanly over a <c>DomNode docRoot</c>. The browsing-context
/// infrastructure (the sub-document/-window caches, the content-document maps, resource loading, onload
/// and the sub-<em>window</em> object) stays bridge-owned pending a future <c>BrowsingContextManager</c>;
/// the module reaches the bridge only through the explicit <see cref="ISubDocumentHost"/> contract and
/// the assembly's neutral static <c>DomBridge</c> tree/selector helpers.
/// </para>
/// </summary>
internal sealed partial class SubDocumentBinding(ISubDocumentHost host)
{
    private readonly ISubDocumentHost _host = host;

    /// <summary>
    /// Builds the JS <c>document</c> object for the sub-document rooted at <paramref name="docRoot"/> and
    /// registers it as that root's wrapper identity. Was <c>DomBridge.BuildSubDocument</c>.
    /// </summary>
    internal JSObject BuildDocument(DomNode docRoot)
    {
        var doc = new JSObject();
        _host.RegisterDocumentWrapper(docRoot, doc);

        doc.FastAddProperty((KeyString)"documentElement",
            new DomFunction((in _) => DomBridge.GetDocumentElement(docRoot) is { } de ? _host.ToJSObject(de) : JSNull.Value, "get documentElement"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        doc.FastAddProperty((KeyString)"scrollingElement",
            new DomFunction((in _) => DomBridge.GetDocumentElement(docRoot) is { } se ? _host.ToJSObject(se) : JSNull.Value, "get scrollingElement"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // body
        doc.FastAddProperty((KeyString)"body",
            new DomFunction((in _) => GetBody(docRoot), "get body"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // head
        doc.FastAddProperty((KeyString)"head",
            new DomFunction((in _) => GetHead(docRoot), "get head"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // title (dynamic getter from <title> element in <head>)
        doc.FastAddProperty((KeyString)"title",
            new DomFunction((in _) => GetTitle(docRoot), "get title"),
            new DomFunction((in a) => SetTitle(docRoot, in a), "set title"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // forms (dynamic collection of <form> elements)
        doc.FastAddProperty((KeyString)"forms",
            new DomFunction((in _) => GetForms(docRoot), "get forms"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // childNodes
        doc.FastAddProperty((KeyString)"childNodes",
            new DomFunction((in _) => GetChildNodes(docRoot), "get childNodes"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // firstChild
        doc.FastAddProperty((KeyString)"firstChild",
            new DomFunction((in _) => docRoot.ChildNodes.Count > 0 ? _host.ToJSObject(DomBridge.ChildAt(docRoot, 0)) : JSNull.Value, "get firstChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastChild
        doc.FastAddProperty((KeyString)"lastChild",
            new DomFunction((in _) => docRoot.ChildNodes.Count > 0 ? _host.ToJSObject(DomBridge.ChildAt(docRoot, ^1)) : JSNull.Value, "get lastChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // hasChildNodes()
        doc.FastAddValue((KeyString)"hasChildNodes",
            new DomFunction((in _) => docRoot.ChildNodes.Count > 0 ? JSBoolean.True : JSBoolean.False, "hasChildNodes", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // nodeType = DOCUMENT_NODE (9)
        doc.FastAddProperty((KeyString)"nodeType",
            new DomFunction((in _) => new JSNumber(9), "get nodeType"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeName = "#document"
        doc.FastAddProperty((KeyString)"nodeName",
            new DomFunction((in _) => new JSString("#document"), "get nodeName"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // localName = null for document
        doc.FastAddProperty((KeyString)"localName",
            DomBridge.NullFunction("get localName"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // getElementById(id)
        doc.FastAddValue((KeyString)"getElementById",
            new DomFunction((in a) => GetElementById(docRoot, in a), "getElementById", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getElementsByTagName(tag)
        doc.FastAddValue((KeyString)"getElementsByTagName",
            new DomFunction((in a) => GetElementsByTagName(docRoot, in a), "getElementsByTagName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getElementsByClassName(names) / getElementsByName(name) — the two collection lookups a
        // frame's document was missing while the main document had them. A script in a frame is a
        // script like any other: absent, these read as undefined rather than as missing methods, so
        // calling one threw and took the frame's whole <script> with it.
        doc.FastAddValue((KeyString)"getElementsByClassName",
            new DomFunction((in a) => GetElementsByClassName(docRoot, in a), "getElementsByClassName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        doc.FastAddValue((KeyString)"getElementsByName",
            new DomFunction((in a) => GetElementsByName(docRoot, in a), "getElementsByName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createElement(tag)
        doc.FastAddValue((KeyString)"createElement",
            new DomFunction((in a) => CreateElement(docRoot, in a), "createElement", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createTextNode(text)
        doc.FastAddValue((KeyString)"createTextNode",
            new DomFunction((in a) => CreateTextNode(docRoot, in a), "createTextNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createComment(data)
        doc.FastAddValue((KeyString)"createComment",
            new DomFunction((in a) => CreateComment(docRoot, in a), "createComment", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createElementNS(ns, localName)
        doc.FastAddValue((KeyString)"createElementNS",
            new DomFunction((in a) => CreateElementNS(docRoot, in a), "createElementNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createEvent(type)
        doc.FastAddValue((KeyString)"createEvent",
            new DomFunction((in a) => CreateEvent(in a), "createEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // querySelector / querySelectorAll
        doc.FastAddValue((KeyString)"querySelector",
            new DomFunction((in a) => QuerySelector(docRoot, in a), "querySelector", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        doc.FastAddValue((KeyString)"querySelectorAll",
            new DomFunction((in a) => QuerySelectorAll(docRoot, in a), "querySelectorAll", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        doc.FastAddValue((KeyString)"elementFromPoint",
            new DomFunction((in a) => ElementFromPoint(docRoot, in a), "elementFromPoint", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        doc.FastAddValue((KeyString)"elementsFromPoint",
            new DomFunction((in a) => ElementsFromPoint(docRoot, in a), "elementsFromPoint", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.open()
        doc.FastAddValue((KeyString)"open",
            new DomFunction((in _) => Open(doc, docRoot), "open", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.close()
        doc.FastAddValue((KeyString)"close",
            DomBridge.UndefinedFunction("close", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.write(html)
        doc.FastAddValue((KeyString)"write",
            new DomFunction((in a) => Write(docRoot, in a), "write", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.images
        doc.FastAddProperty((KeyString)"images",
            new DomFunction((in _) => GetImages(docRoot), "get images"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.links
        doc.FastAddProperty((KeyString)"links",
            new DomFunction((in _) => GetLinks(docRoot), "get links"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.scripts
        doc.FastAddProperty((KeyString)"scripts",
            new DomFunction((in _) => GetScripts(docRoot), "get scripts"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.styleSheets
        doc.FastAddProperty((KeyString)"styleSheets",
            new DomFunction((in _) => _host.BuildStyleSheetsCollection(docRoot), "get styleSheets"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // removeChild on document
        doc.FastAddValue((KeyString)"removeChild",
            new DomFunction((in a) => RemoveChild(docRoot, in a), "removeChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // appendChild on document
        doc.FastAddValue((KeyString)"appendChild",
            new DomFunction((in a) => AppendChild(docRoot, in a), "appendChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        doc.FastAddValue((KeyString)"append",
            new DomFunction((in a) => Append(docRoot, in a), "append", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        doc.FastAddValue((KeyString)"prepend",
            new DomFunction((in a) => Prepend(docRoot, in a), "prepend", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Node type constants
        doc.FastAddValue((KeyString)"ELEMENT_NODE", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        doc.FastAddValue((KeyString)"TEXT_NODE", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);
        doc.FastAddValue((KeyString)"COMMENT_NODE", new JSNumber(8), JSPropertyAttributes.EnumerableConfigurableValue);
        doc.FastAddValue((KeyString)"DOCUMENT_NODE", new JSNumber(9), JSPropertyAttributes.EnumerableConfigurableValue);
        doc.FastAddValue((KeyString)"DOCUMENT_TYPE_NODE", new JSNumber(10), JSPropertyAttributes.EnumerableConfigurableValue);
        doc.FastAddValue((KeyString)"DOCUMENT_FRAGMENT_NODE", new JSNumber(11), JSPropertyAttributes.EnumerableConfigurableValue);

        // document.implementation on sub-documents
        var subImpl = new JSObject();
        subImpl.FastAddValue((KeyString)"hasFeature",
            DomBridge.TrueFunction("hasFeature", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        subImpl.FastAddValue((KeyString)"createDocumentType",
            new DomFunction((in a) => CreateDocumentType(in a), "createDocumentType", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        subImpl.FastAddValue((KeyString)"createDocument",
            new DomFunction((in a) => CreateDocument(in a), "createDocument", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        subImpl.FastAddValue((KeyString)"createHTMLDocument",
            new DomFunction((in a) => CreateHTMLDocument(in a), "createHTMLDocument", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        doc.FastAddValue((KeyString)"implementation",
            subImpl, JSPropertyAttributes.EnumerableConfigurableValue);

        // defaultView — return the main window object so getComputedStyle is accessible
        if (_host.WindowJSObject != null)
        {
            doc.FastAddValue((KeyString)"defaultView",
                _host.WindowJSObject, JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // createTreeWalker(root, whatToShow, filter)
        doc.FastAddValue((KeyString)"createTreeWalker",
            new DomFunction((in a) => CreateTreeWalker(in a), "createTreeWalker", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createNodeIterator(root, whatToShow, filter)
        doc.FastAddValue((KeyString)"createNodeIterator",
            new DomFunction((in a) => CreateNodeIterator(in a), "createNodeIterator", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // startViewTransition() — CSS View Transitions, scoped to this nested browsing context.
        // Absent here, a page driving a transition inside its <iframe> through contentDocument hit a
        // TypeError that aborted the rest of its script, so the main frame's own transition never ran
        // either (WPT css-view-transitions/iframe-and-main-frame-transition-*).
        doc.FastAddValue((KeyString)"startViewTransition",
            new DomFunction((in a) => _host.StartViewTransition(docRoot, in a), "startViewTransition", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createRange()
        doc.FastAddValue((KeyString)"createRange",
            new DomFunction((in _) => _host.BuildRange(docRoot), "createRange", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return doc;
    }

    // -------- read-only document getters --------

    private JSValue GetBody(DomNode docRoot)
    {
        var htmlEl = DomBridge.GetDocumentElement(docRoot);
        if (htmlEl == null)
            return JSNull.Value;
        foreach (var child in DomBridge.ChildElements(htmlEl))
        {
            if (string.Equals(child.TagName, "body", StringComparison.OrdinalIgnoreCase))
                return _host.ToJSObject(child);
        }

        return JSNull.Value;
    }

    private JSValue GetHead(DomNode docRoot)
    {
        var htmlEl = DomBridge.GetDocumentElement(docRoot);
        if (htmlEl == null)
            return JSNull.Value;
        foreach (var child in DomBridge.ChildElements(htmlEl))
        {
            if (string.Equals(child.TagName, "head", StringComparison.OrdinalIgnoreCase))
                return _host.ToJSObject(child);
        }

        return JSNull.Value;
    }

    private JSValue GetTitle(DomNode docRoot)
    {
        var htmlEl = DomBridge.GetDocumentElement(docRoot);
        if (htmlEl == null)
            return new JSString(string.Empty);
        var head = DomBridge.ChildElements(htmlEl).FirstOrDefault(c => string.Equals(c.TagName, "head", StringComparison.OrdinalIgnoreCase));
        if (head != null)
        {
            var titleEl = DomBridge.ChildElements(head).FirstOrDefault(c => string.Equals(c.TagName, "title", StringComparison.OrdinalIgnoreCase));
            if (titleEl != null)
            {
                var sb = new StringBuilder();
                DomBridge.CollectTextContent(titleEl, sb);
                return new JSString(sb.ToString());
            }
        }

        return new JSString(string.Empty);
    }

    private JSValue SetTitle(DomNode docRoot, in Arguments a)
    {
        var htmlEl = DomBridge.GetDocumentElement(docRoot);
        if (htmlEl == null)
            return JSUndefined.Value;
        var head = DomBridge.ChildElements(htmlEl).FirstOrDefault(c => string.Equals(c.TagName, "head", StringComparison.OrdinalIgnoreCase));
        if (head != null)
        {
            var titleEl = DomBridge.ChildElements(head).FirstOrDefault(c => string.Equals(c.TagName, "title", StringComparison.OrdinalIgnoreCase));
            if (titleEl != null)
                _host.SetElementTextContent(titleEl, a.Length > 0 ? a[0].ToString() : string.Empty);
        }

        return JSUndefined.Value;
    }

    private JSValue GetForms(DomNode docRoot)
    {
        var results = new List<JSValue>();
        _host.CollectByTagName(docRoot, "form", results);
        return new JSArray(results);
    }

    private JSValue GetChildNodes(DomNode docRoot)
    {
        // childNodes includes ALL node types (per DOM), notably the canonical DomDocumentType — which
        // is no longer a DomElement after Phase 4 item 1, so ChildElements would wrongly drop it. This
        // matches the sub-document's firstChild (raw first child) and the document childNodes semantics.
        var arr = new JSArray();
        foreach (var child in docRoot.ChildNodes)
            arr.Add(_host.ToJSObject(child));
        return arr;
    }

    private JSValue GetElementById(DomNode docRoot, in Arguments a)
    {
        var id = a.Length > 0 ? a[0].ToString() : string.Empty;
        var found = DomBridge.FindInSubTree(docRoot, el => el.Id == id);
        return found != null ? _host.ToJSObject(found) : JSNull.Value;
    }

    private JSValue GetElementsByTagName(DomNode docRoot, in Arguments a)
    {
        var tagName = a.Length > 0 ? a[0].ToString().ToLowerInvariant() : string.Empty;
        var results = new List<JSValue>();
        _host.CollectByTagName(docRoot, tagName, results);
        return new JSArray(results);
    }

    /// <summary>
    /// <c>getElementsByClassName</c> on a frame's document. It reuses
    /// <see cref="ClassNameSet"/>, the rule the main document and the element-scoped search already
    /// share, so all three surfaces answer a class query the same way.
    /// </summary>
    private JSValue GetElementsByClassName(DomNode docRoot, in Arguments a)
    {
        var wanted = ClassNameSet.Parse(a.Length > 0 ? a[0].ToString() : string.Empty);
        var results = new List<JSValue>();
        if (wanted.Length > 0)
            _host.CollectMatching(docRoot, el => ClassNameSet.Matches(el, wanted), results);

        return new JSArray(results);
    }

    /// <summary>
    /// <c>getElementsByName</c> on a frame's document — the elements whose <c>name</c> attribute is
    /// identical to the argument, matching the main document's implementation (HTML §3.1.5).
    /// </summary>
    private JSValue GetElementsByName(DomNode docRoot, in Arguments a)
    {
        var name = a.Length > 0 ? a[0].ToString() : string.Empty;
        var results = new List<JSValue>();
        _host.CollectMatching(
            docRoot,
            el => DomBridge.TryGetAttribute(el, "name", out var value) && string.Equals(value, name, StringComparison.Ordinal),
            results);

        return new JSArray(results);
    }

    private JSValue QuerySelector(DomNode docRoot, in Arguments a)
    {
        var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
        var found = DomBridge.FindInSubTree(docRoot, el => _host.MatchesSelector(el, selector));
        return found != null ? _host.ToJSObject(found) : JSNull.Value;
    }

    private JSValue QuerySelectorAll(DomNode docRoot, in Arguments a)
    {
        var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
        var results = new List<JSValue>();
        _host.CollectMatching(docRoot, el => _host.MatchesSelector(el, selector), results);
        return new JSArray(results);
    }

    private JSValue ElementFromPoint(DomNode docRoot, in Arguments a)
    {
        var hit = _host.HitTestDocumentPoint(docRoot, DomBridge.GetCoordinateArgument(a, 0), DomBridge.GetCoordinateArgument(a, 1)).FirstOrDefault();
        return hit != null ? _host.ToJSObject(hit) : JSNull.Value;
    }

    private JSValue ElementsFromPoint(DomNode docRoot, in Arguments a)
    {
        var hits = _host.HitTestDocumentPoint(docRoot, DomBridge.GetCoordinateArgument(a, 0), DomBridge.GetCoordinateArgument(a, 1));
        return new JSArray(hits.Select(_host.ToJSObject).ToArray());
    }

    private JSValue GetImages(DomNode docRoot)
    {
        var results = new List<JSValue>();
        _host.CollectByTagName(docRoot, "img", results);
        return new JSArray(results);
    }

    private JSValue GetScripts(DomNode docRoot)
    {
        var results = new List<JSValue>();
        _host.CollectByTagName(docRoot, "script", results);
        return new JSArray(results);
    }

    private JSValue GetLinks(DomNode docRoot)
    {
        var results = new List<JSValue>();
        _host.CollectMatching(docRoot, el => (string.Equals(el.TagName, "a", StringComparison.OrdinalIgnoreCase) || string.Equals(el.TagName, "area", StringComparison.OrdinalIgnoreCase)) && DomBridge.HasAttr(el, "href"), results);
        return new JSArray(results);
    }
}
