using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using YantraJS.Core;

namespace Broiler.App.Rendering;

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
    public DomElement DocumentElement { get; } = new("html", null, null, string.Empty);

    private void RegisterDocument(JSContext context)
    {
        _jsContext = context;
        var document = new JSObject();

        // document.documentElement (the <html> element)
        document.FastAddValue(
            (KeyString)"documentElement",
            ToJSObject(DocumentElement),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.body (getter — first <body> child of documentElement)
        document.FastAddProperty(
            (KeyString)"body",
            new JSFunction((in Arguments a) =>
            {
                foreach (var child in DocumentElement.Children)
                {
                    if (string.Equals(child.TagName, "body", StringComparison.OrdinalIgnoreCase))
                        return ToJSObject(child);
                }
                return JSNull.Value;
            }, "get body"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.head (getter — first <head> child of documentElement)
        document.FastAddProperty(
            (KeyString)"head",
            new JSFunction((in Arguments a) =>
            {
                foreach (var child in DocumentElement.Children)
                {
                    if (string.Equals(child.TagName, "head", StringComparison.OrdinalIgnoreCase))
                        return ToJSObject(child);
                }
                return JSNull.Value;
            }, "get head"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.title (getter / setter)
        document.FastAddProperty(
            (KeyString)"title",
            new JSFunction((in Arguments a) => new JSString(Title), "get title"),
            new JSFunction((in Arguments a) =>
            {
                Title = a.Length > 0 ? a[0].ToString() : string.Empty;
                return JSUndefined.Value;
            }, "set title"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.getElementById(id)
        document.FastAddValue(
            (KeyString)"getElementById",
            new JSFunction((in Arguments a) =>
            {
                var id = a.Length > 0 ? a[0].ToString() : string.Empty;
                foreach (var el in _elements)
                {
                    if (el.Id == id)
                        return ToJSObject(el);
                }
                return JSNull.Value;
            }, "getElementById", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.getElementsByTagName(tag)
        document.FastAddValue(
            (KeyString)"getElementsByTagName",
            new JSFunction((in Arguments a) =>
            {
                var tag = a.Length > 0 ? a[0].ToString().ToLowerInvariant() : string.Empty;
                var results = new List<JSValue>();
                foreach (var el in _elements)
                {
                    if (el.TagName == tag)
                        results.Add(ToJSObject(el));
                }
                return new JSArray(results);
            }, "getElementsByTagName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.getElementsByClassName(className)
        document.FastAddValue(
            (KeyString)"getElementsByClassName",
            new JSFunction((in Arguments a) =>
            {
                var className = a.Length > 0 ? a[0].ToString() : string.Empty;
                var results = new List<JSValue>();
                foreach (var el in _elements)
                {
                    var classes = new System.Collections.Generic.HashSet<string>(
                        (el.ClassName ?? string.Empty).Split(' ').Where(s => s.Length > 0),
                        System.StringComparer.Ordinal);
                    if (classes.Contains(className))
                        results.Add(ToJSObject(el));
                }
                return new JSArray(results);
            }, "getElementsByClassName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.querySelector(selector)
        document.FastAddValue(
            (KeyString)"querySelector",
            new JSFunction((in Arguments a) =>
            {
                var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
                foreach (var el in _elements)
                {
                    if (MatchesSelector(el, selector))
                        return ToJSObject(el);
                }
                return JSNull.Value;
            }, "querySelector", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.querySelectorAll(selector)
        document.FastAddValue(
            (KeyString)"querySelectorAll",
            new JSFunction((in Arguments a) =>
            {
                var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
                var results = new List<JSValue>();
                foreach (var el in _elements)
                {
                    if (MatchesSelector(el, selector))
                        results.Add(ToJSObject(el));
                }
                return new JSArray(results);
            }, "querySelectorAll", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createElement(tag)
        document.FastAddValue(
            (KeyString)"createElement",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    throw new JSException("Failed to execute 'createElement': 1 argument required, but only 0 present.");
                var tag = a[0].ToString().ToLowerInvariant();
                ValidateElementName(tag, context);
                var el = new DomElement(tag, null, null, string.Empty);
                _elements.Add(el);
                return ToJSObject(el);
            }, "createElement", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createTextNode(text)
        document.FastAddValue(
            (KeyString)"createTextNode",
            new JSFunction((in Arguments a) =>
            {
                var text = a.Length > 0 ? a[0].ToString() : string.Empty;
                var el = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                el.TextContent = text;
                _elements.Add(el);
                return ToJSObject(el);
            }, "createTextNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createDocumentFragment() — basic iframe/fragment support
        document.FastAddValue(
            (KeyString)"createDocumentFragment",
            new JSFunction((in Arguments a) =>
            {
                var fragment = new DomElement("#document-fragment", null, null, string.Empty);
                _elements.Add(fragment);
                return ToJSObject(fragment);
            }, "createDocumentFragment", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createEvent(type) — DOM Events Level 3
        document.FastAddValue(
            (KeyString)"createEvent",
            new JSFunction((in Arguments a) =>
            {
                var evt = new JSObject();
                evt.FastAddValue((KeyString)"type", new JSString(string.Empty), JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"bubbles", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"cancelable", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"target", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"currentTarget", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"eventPhase", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"detail", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"view", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"stopPropagation",
                    new JSFunction((in Arguments _) => JSUndefined.Value, "stopPropagation", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"stopImmediatePropagation",
                    new JSFunction((in Arguments _) => JSUndefined.Value, "stopImmediatePropagation", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"preventDefault",
                    new JSFunction((in Arguments _) => JSUndefined.Value, "preventDefault", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"initEvent",
                    new JSFunction((in Arguments initArgs) =>
                    {
                        if (initArgs.Length > 0)
                            evt[(KeyString)"type"] = new JSString(initArgs[0].ToString());
                        if (initArgs.Length > 1)
                            evt[(KeyString)"bubbles"] = initArgs[1].BooleanValue ? JSBoolean.True : JSBoolean.False;
                        if (initArgs.Length > 2)
                            evt[(KeyString)"cancelable"] = initArgs[2].BooleanValue ? JSBoolean.True : JSBoolean.False;
                        return JSUndefined.Value;
                    }, "initEvent", 3),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"initUIEvent",
                    new JSFunction((in Arguments initArgs) =>
                    {
                        if (initArgs.Length > 0)
                            evt[(KeyString)"type"] = new JSString(initArgs[0].ToString());
                        if (initArgs.Length > 1)
                            evt[(KeyString)"bubbles"] = initArgs[1].BooleanValue ? JSBoolean.True : JSBoolean.False;
                        if (initArgs.Length > 2)
                            evt[(KeyString)"cancelable"] = initArgs[2].BooleanValue ? JSBoolean.True : JSBoolean.False;
                        if (initArgs.Length > 3)
                            evt[(KeyString)"view"] = initArgs[3];
                        if (initArgs.Length > 4)
                            evt[(KeyString)"detail"] = initArgs[4];
                        return JSUndefined.Value;
                    }, "initUIEvent", 5),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                return evt;
            }, "createEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // CustomEvent constructor — DOM Level 4
        context.Eval(@"
                function CustomEvent(type, options) {
                    options = options || {};
                    this.type = type;
                    this.detail = options.detail !== undefined ? options.detail : null;
                    this.bubbles = options.bubbles === true;
                    this.cancelable = options.cancelable === true;
                    this.defaultPrevented = false;
                    this.target = null;
                    this.currentTarget = null;
                    this.eventPhase = 0;
                    this.stopPropagation = function() {};
                    this.preventDefault = function() { this.defaultPrevented = true; };
                    this.initCustomEvent = function(type, bubbles, cancelable, detail) {
                        this.type = type;
                        this.bubbles = bubbles === true;
                        this.cancelable = cancelable === true;
                        this.detail = detail !== undefined ? detail : null;
                    };
                }
            ");

        // MutationObserver — DOM Level 4
        var mutationObservers = _mutationObservers;
        context.Eval(@"
                function MutationObserver(callback) {
                    this._callback = callback;
                    this._targets = [];
                }
                MutationObserver.prototype.observe = function(target, options) {
                    this._targets.push({ target: target, options: options || {} });
                };
                MutationObserver.prototype.disconnect = function() {
                    this._targets = [];
                };
                MutationObserver.prototype.takeRecords = function() {
                    return [];
                };
            ");

        // document.write(html) — parse and append to body
        document.FastAddValue(
            (KeyString)"write",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSUndefined.Value;
                var fragment = a[0].ToString();
                var builder = new HtmlTreeBuilder();
                var (docEl, allEls, _) = builder.Build($"<html><body>{fragment}</body></html>");
                var bodyEl = docEl.Children.FirstOrDefault(c =>
                    string.Equals(c.TagName, "body", StringComparison.OrdinalIgnoreCase));
                if (bodyEl != null)
                {
                    // Find the <body> element in the main tree
                    var mainBody = DocumentElement.Children.FirstOrDefault(c =>
                        string.Equals(c.TagName, "body", StringComparison.OrdinalIgnoreCase));
                    if (mainBody != null)
                    {
                        foreach (var child in bodyEl.Children)
                        {
                            child.Parent = mainBody;
                            mainBody.Children.Add(child);
                            _elements.Add(child);
                        }
                    }
                }
                return JSUndefined.Value;
            }, "write", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.writeln(html) — same as write, with trailing newline
        var writeFn = (JSFunction)document[(KeyString)"write"];
        document.FastAddValue(
            (KeyString)"writeln",
            new JSFunction((in Arguments a) =>
            {
                var text = a.Length > 0 ? a[0].ToString() + "\n" : "\n";
                return writeFn.InvokeFunction(new Arguments(writeFn, new JSString(text)));
            }, "writeln", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- Phase 2: NodeFilter, TreeWalker, NodeIterator, Range --

        // NodeFilter constants
        var nodeFilter = new JSObject();
        nodeFilter.FastAddValue((KeyString)"FILTER_ACCEPT", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"FILTER_REJECT", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"FILTER_SKIP", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ALL", new JSNumber(0xFFFFFFFF), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ELEMENT", new JSNumber(0x1), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ATTRIBUTE", new JSNumber(0x2), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_TEXT", new JSNumber(0x4), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_CDATA_SECTION", new JSNumber(0x8), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ENTITY_REFERENCE", new JSNumber(0x10), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ENTITY", new JSNumber(0x20), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_PROCESSING_INSTRUCTION", new JSNumber(0x40), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_COMMENT", new JSNumber(0x80), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_DOCUMENT", new JSNumber(0x100), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_DOCUMENT_TYPE", new JSNumber(0x200), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_DOCUMENT_FRAGMENT", new JSNumber(0x400), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_NOTATION", new JSNumber(0x800), JSPropertyAttributes.EnumerableConfigurableValue);
        context["NodeFilter"] = nodeFilter;

        // document.createTreeWalker(root, whatToShow, filter)
        var bridgeForTraversal = this;
        document.FastAddValue(
            (KeyString)"createTreeWalker",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    throw new JSException("Failed to execute 'createTreeWalker': 1 argument required.");
                var rootObj = a[0] as JSObject;
                if (rootObj == null)
                    throw new JSException("Failed to execute 'createTreeWalker': parameter 1 is not of type 'Node'.");
                var rootEl = bridgeForTraversal.FindDomElementByJSObject(rootObj);
                if (rootEl == null) return JSNull.Value;

                var whatToShow = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? (int)a[1].DoubleValue : unchecked((int)0xFFFFFFFF);
                var filterFn = a.Length > 2 && a[2] is JSFunction f ? f : (a.Length > 2 && a[2] is JSObject filterObj ? filterObj[(KeyString)"acceptNode"] as JSFunction : null);

                return bridgeForTraversal.BuildTreeWalker(rootEl, whatToShow, filterFn);
            }, "createTreeWalker", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createNodeIterator(root, whatToShow, filter)
        document.FastAddValue(
            (KeyString)"createNodeIterator",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    throw new JSException("Failed to execute 'createNodeIterator': 1 argument required.");
                var rootObj = a[0] as JSObject;
                if (rootObj == null)
                    throw new JSException("Failed to execute 'createNodeIterator': parameter 1 is not of type 'Node'.");
                var rootEl = bridgeForTraversal.FindDomElementByJSObject(rootObj);
                if (rootEl == null) return JSNull.Value;

                var whatToShow = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? (int)a[1].DoubleValue : unchecked((int)0xFFFFFFFF);
                var filterFn = a.Length > 2 && a[2] is JSFunction f ? f : (a.Length > 2 && a[2] is JSObject filterObj ? filterObj[(KeyString)"acceptNode"] as JSFunction : null);

                return bridgeForTraversal.BuildNodeIterator(rootEl, whatToShow, filterFn);
            }, "createNodeIterator", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createRange()
        document.FastAddValue(
            (KeyString)"createRange",
            new JSFunction((in Arguments a) => bridgeForTraversal.BuildRange(), "createRange", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createComment(data)
        document.FastAddValue(
            (KeyString)"createComment",
            new JSFunction((in Arguments a) =>
            {
                var data = a.Length > 0 ? a[0].ToString() : string.Empty;
                var el = new DomElement("#comment", null, null, string.Empty, isTextNode: false);
                el.TextContent = data;
                _elements.Add(el);
                return ToJSObject(el);
            }, "createComment", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Node type constants on document
        document.FastAddValue((KeyString)"ELEMENT_NODE", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"TEXT_NODE", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"COMMENT_NODE", new JSNumber(8), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"DOCUMENT_NODE", new JSNumber(9), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"DOCUMENT_TYPE_NODE", new JSNumber(10), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"DOCUMENT_FRAGMENT_NODE", new JSNumber(11), JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createElementNS(namespace, tagName)
        document.FastAddValue(
            (KeyString)"createElementNS",
            new JSFunction((in Arguments a) =>
            {
                var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
                var localName = a.Length > 1 ? a[1].ToString() : (a.Length > 0 ? a[0].ToString() : "div");
                ValidateQualifiedName(localName, ns, context);
                var el = new DomElement(localName, null, null, string.Empty);
                if (!string.IsNullOrEmpty(ns))
                    el.NamespaceURI = ns;
                _elements.Add(el);
                return ToJSObject(el);
            }, "createElementNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.images — collection of all <img> elements
        document.FastAddProperty(
            (KeyString)"images",
            new JSFunction((in Arguments _) =>
            {
                var results = new List<JSValue>();
                foreach (var el in _elements)
                {
                    if (string.Equals(el.TagName, "img", StringComparison.OrdinalIgnoreCase))
                        results.Add(ToJSObject(el));
                }
                return new JSArray(results);
            }, "get images"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.links — collection of all <a> and <area> elements with href
        document.FastAddProperty(
            (KeyString)"links",
            new JSFunction((in Arguments _) =>
            {
                var results = new List<JSValue>();
                foreach (var el in _elements)
                {
                    if ((string.Equals(el.TagName, "a", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(el.TagName, "area", StringComparison.OrdinalIgnoreCase)) &&
                        el.Attributes.ContainsKey("href"))
                        results.Add(ToJSObject(el));
                }
                return new JSArray(results);
            }, "get links"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.styleSheets — collection of stylesheet objects for main document
        document.FastAddProperty(
            (KeyString)"styleSheets",
            new JSFunction((in Arguments _) =>
            {
                var styleEls = new List<DomElement>();
                foreach (var el in _elements)
                {
                    if (string.Equals(el.TagName, "style", StringComparison.OrdinalIgnoreCase))
                        styleEls.Add(el);
                }
                var arr = new JSArray();
                foreach (var styleEl in styleEls)
                    arr.Add(BuildStyleSheetObject(styleEl));
                return arr;
            }, "get styleSheets"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.open() — for main document
        document.FastAddValue(
            (KeyString)"open",
            new JSFunction((in Arguments _) =>
            {
                // Main document open is a no-op in our implementation
                return document;
            }, "open", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.close() — for main document
        document.FastAddValue(
            (KeyString)"close",
            new JSFunction((in Arguments _) => JSUndefined.Value, "close", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.implementation — DOMImplementation
        var implementation = new JSObject();

        // implementation.hasFeature() — always returns true per spec
        implementation.FastAddValue(
            (KeyString)"hasFeature",
            new JSFunction((in Arguments _) => JSBoolean.True, "hasFeature", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // implementation.createDocumentType(qualifiedName, publicId, systemId)
        implementation.FastAddValue(
            (KeyString)"createDocumentType",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 3)
                    throw new JSException("Failed to execute 'createDocumentType' on 'DOMImplementation': 3 arguments required.");
                var qualifiedName = a[0].ToString();
                var publicId = a[1].ToString();
                var systemId = a[2].ToString();
                ValidateElementName(qualifiedName, context);
                var doctype = new DomElement("#doctype", null, null, string.Empty);
                doctype.DomProperties["name"] = qualifiedName;
                doctype.DomProperties["publicId"] = publicId;
                doctype.DomProperties["systemId"] = systemId;
                doctype.DomProperties["internalSubset"] = null;
                _elements.Add(doctype);
                return ToJSObject(doctype);
            }, "createDocumentType", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // implementation.createDocument(namespace, qualifiedName, doctype)
        implementation.FastAddValue(
            (KeyString)"createDocument",
            new JSFunction((in Arguments a) =>
            {
                var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
                var qName = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? a[1].ToString() : null;
                var doctypeArg = a.Length > 2 ? a[2] : null;

                if (!string.IsNullOrEmpty(qName))
                    ValidateQualifiedName(qName, ns, context);

                // Build a new document root
                var docRoot = new DomElement("#subdoc-root", null, null, string.Empty);
                _elements.Add(docRoot);

                // Append doctype if provided
                if (doctypeArg is JSObject dtObj)
                {
                    // Find the DomElement for the doctype JSObject
                    foreach (var kvp in _jsObjectCache)
                    {
                        if (kvp.Value == dtObj)
                        {
                            var dtEl = kvp.Key;
                            dtEl.Parent = docRoot;
                            docRoot.Children.Add(dtEl);
                            break;
                        }
                    }
                }

                // Create document element if qualifiedName is provided
                if (!string.IsNullOrEmpty(qName))
                {
                    var docEl = new DomElement(qName, null, null, string.Empty);
                    if (!string.IsNullOrEmpty(ns))
                        docEl.NamespaceURI = ns;
                    docEl.Parent = docRoot;
                    docRoot.Children.Add(docEl);
                    _elements.Add(docEl);
                }

                return BuildSubDocument(docRoot);
            }, "createDocument", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // implementation.createHTMLDocument(title)
        implementation.FastAddValue(
            (KeyString)"createHTMLDocument",
            new JSFunction((in Arguments a) =>
            {
                var title = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;

                // Build a new HTML document root with html/head/body
                var docRoot = new DomElement("#subdoc-root", null, null, string.Empty);
                _elements.Add(docRoot);

                // Add DOCTYPE
                var doctype = new DomElement("#doctype", null, null, string.Empty);
                doctype.DomProperties["name"] = "html";
                doctype.DomProperties["publicId"] = string.Empty;
                doctype.DomProperties["systemId"] = string.Empty;
                doctype.DomProperties["internalSubset"] = null;
                doctype.Parent = docRoot;
                docRoot.Children.Add(doctype);
                _elements.Add(doctype);

                var htmlEl = new DomElement("html", null, null, string.Empty);
                htmlEl.NamespaceURI = "http://www.w3.org/1999/xhtml";
                htmlEl.Parent = docRoot;
                docRoot.Children.Add(htmlEl);
                _elements.Add(htmlEl);

                var headEl = new DomElement("head", null, null, string.Empty);
                headEl.Parent = htmlEl;
                htmlEl.Children.Add(headEl);
                _elements.Add(headEl);

                // Add <title> element if title argument is provided
                if (title != null)
                {
                    var titleEl = new DomElement("title", null, null, string.Empty);
                    titleEl.Parent = headEl;
                    headEl.Children.Add(titleEl);
                    _elements.Add(titleEl);

                    var titleText = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                    titleText.TextContent = title;
                    titleText.Parent = titleEl;
                    titleEl.Children.Add(titleText);
                    _elements.Add(titleText);
                }

                var bodyEl = new DomElement("body", null, null, string.Empty);
                bodyEl.Parent = htmlEl;
                htmlEl.Children.Add(bodyEl);
                _elements.Add(bodyEl);

                return BuildSubDocument(docRoot);
            }, "createHTMLDocument", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        document.FastAddValue(
            (KeyString)"implementation",
            implementation,
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document-level addEventListener / removeEventListener / dispatchEvent
        var docNode = _documentNode;
        var bridgeRef = this;
        document.FastAddValue(
            (KeyString)"addEventListener",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSUndefined.Value;
                var type = a[0].ToString();
                var listener = a[1];
                var capture = a.Length > 2 && a[2].BooleanValue;
                if (!docNode.EventListeners.TryGetValue(type, out var listeners))
                {
                    listeners = [];
                    docNode.EventListeners[type] = listeners;
                }
                listeners.Add((listener, capture));
                return JSUndefined.Value;
            }, "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        document.FastAddValue(
            (KeyString)"removeEventListener",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSUndefined.Value;
                var type = a[0].ToString();
                var listener = a[1];
                var capture = a.Length > 2 && a[2].BooleanValue;
                if (docNode.EventListeners.TryGetValue(type, out var listeners))
                {
                    for (int i = listeners.Count - 1; i >= 0; i--)
                    {
                        if (listeners[i].Listener == listener && listeners[i].Capture == capture)
                        {
                            listeners.RemoveAt(i);
                            break;
                        }
                    }
                }
                return JSUndefined.Value;
            }, "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        document.FastAddValue(
            (KeyString)"dispatchEvent",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSBoolean.True;
                var evt = a[0] as JSObject;
                if (evt == null) return JSBoolean.True;
                return bridgeRef.DispatchEventOnElement(docNode, evt);
            }, "dispatchEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.contentType — returns the MIME type of the document
        document.FastAddProperty(
            (KeyString)"contentType",
            new JSFunction((in Arguments _) =>
            {
                if (_pageUrl.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) ||
                    _pageUrl.EndsWith(".xht", StringComparison.OrdinalIgnoreCase) ||
                    _pageUrl.Contains("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
                    return new JSString("application/xhtml+xml");
                return new JSString("text/html");
            }, "get contentType"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.URL — returns the document URL
        document.FastAddProperty(
            (KeyString)"URL",
            new JSFunction((in Arguments _) => new JSString(_pageUrl), "get URL"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.documentURI — same as document.URL
        document.FastAddProperty(
            (KeyString)"documentURI",
            new JSFunction((in Arguments _) => new JSString(_pageUrl), "get documentURI"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.compatMode — "CSS1Compat" for standards mode, "BackCompat" for quirks
        document.FastAddProperty(
            (KeyString)"compatMode",
            new JSFunction((in Arguments _) => new JSString("CSS1Compat"), "get compatMode"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.characterSet — always UTF-8
        document.FastAddProperty(
            (KeyString)"characterSet",
            new JSFunction((in Arguments _) => new JSString("UTF-8"), "get characterSet"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.inputEncoding — alias for characterSet
        document.FastAddProperty(
            (KeyString)"inputEncoding",
            new JSFunction((in Arguments _) => new JSString("UTF-8"), "get inputEncoding"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        _documentJSObject = document;
        context["document"] = document;

        // window global
        var window = new JSObject();
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
            new JSFunction((in Arguments a) =>
            {
                var query = a.Length > 0 ? a[0].ToString() : string.Empty;
                var matches = !string.IsNullOrEmpty(query) && EvaluateMediaQuery(query);
                var result = new JSObject();
                result.FastAddValue(
                    (KeyString)"matches",
                    matches ? JSBoolean.True : JSBoolean.False,
                    JSPropertyAttributes.EnumerableConfigurableValue);
                result.FastAddValue(
                    (KeyString)"media",
                    new JSString(query),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                // addListener / removeListener stubs
                result.FastAddValue(
                    (KeyString)"addListener",
                    new JSFunction((in Arguments _) => JSUndefined.Value, "addListener", 1),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                result.FastAddValue(
                    (KeyString)"removeListener",
                    new JSFunction((in Arguments _) => JSUndefined.Value, "removeListener", 1),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                return result;
            }, "matchMedia", 1),
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
            new JSFunction((in Arguments a) =>
            {
                var id = ++_timerIdCounter;
                if (a.Length > 0 && a[0] is JSFunction fn)
                {
                    _timeoutCallbacks[id] = fn;
                }
                return new JSNumber(id);
            }, "setTimeout", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.clearTimeout(id) — removes queued callback
        window.FastAddValue(
            (KeyString)"clearTimeout",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var id = (int)a[0].DoubleValue;
                    _timeoutCallbacks.Remove(id);
                    _clearedTimerIds.Add(id);
                }
                return JSUndefined.Value;
            }, "clearTimeout", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.setInterval(fn, delay) — queues repeating callback
        window.FastAddValue(
            (KeyString)"setInterval",
            new JSFunction((in Arguments a) =>
            {
                var id = ++_timerIdCounter;
                if (a.Length > 0 && a[0] is JSFunction fn)
                {
                    _intervalCallbacks[id] = fn;
                }
                return new JSNumber(id);
            }, "setInterval", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.clearInterval(id) — removes interval callback
        window.FastAddValue(
            (KeyString)"clearInterval",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var id = (int)a[0].DoubleValue;
                    _intervalCallbacks.Remove(id);
                    _clearedTimerIds.Add(id);
                }
                return JSUndefined.Value;
            }, "clearInterval", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.requestAnimationFrame(fn) — queues callback for pre-render execution
        window.FastAddValue(
            (KeyString)"requestAnimationFrame",
            new JSFunction((in Arguments a) =>
            {
                var id = ++_rafIdCounter;
                if (a.Length > 0 && a[0] is JSFunction fn)
                {
                    _rafCallbacks[id] = fn;
                }
                return new JSNumber(id);
            }, "requestAnimationFrame", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.cancelAnimationFrame(id) — removes queued rAF callback
        window.FastAddValue(
            (KeyString)"cancelAnimationFrame",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var id = (int)a[0].DoubleValue;
                    _rafCallbacks.Remove(id);
                }
                return JSUndefined.Value;
            }, "cancelAnimationFrame", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.alert(msg) — logs to debug output
        window.FastAddValue(
            (KeyString)"alert",
            new JSFunction((in Arguments a) =>
            {
                var msg = a.Length > 0 ? a[0].ToString() : string.Empty;
                RenderLogger.LogDebug(LogCategory.JavaScript, "DomBridge.alert", msg);
                return JSUndefined.Value;
            }, "alert", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // console object (shared between window.console and global console)
        var console = BuildConsoleObject();
        window.FastAddValue(
            (KeyString)"console",
            console,
            JSPropertyAttributes.EnumerableConfigurableValue);

        // fetch(url, options) — polyfill backed by HttpClient with headers, method support
        var fetchFn = new JSFunction((in Arguments a) =>
        {
            if (a.Length == 0)
                throw new JSException("Failed to execute 'fetch': 1 argument required.");

            var fetchUrl = a[0].ToString();
            var responseObj = new JSObject();

            // Parse options (method, headers, body)
            var method = "GET";
            string? requestBody = null;
            var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (a.Length > 1 && a[1] is JSObject opts)
            {
                var mVal = opts[(KeyString)"method"];
                if (mVal is JSString mStr)
                    method = mStr.ToString().ToUpperInvariant();
                var bVal = opts[(KeyString)"body"];
                if (bVal is JSString bStr)
                    requestBody = bStr.ToString();
                var hVal = opts[(KeyString)"headers"];
                if (hVal is JSObject hObj)
                {
                    // Enumerate known request header names.
                    // Note: YantraJS does not support Object.keys/for-in on all object
                    // types reliably, so we probe a fixed set of common HTTP headers.
                    // Custom headers outside this list will not be forwarded.
                    var commonHeaders = new[] { "Content-Type", "Accept", "Authorization",
                        "X-Requested-With", "Cache-Control", "Pragma", "If-Modified-Since",
                        "If-None-Match", "Range" };
                    foreach (var name in commonHeaders)
                    {
                        var v = hObj[(KeyString)name];
                        if (v is JSString sv)
                            requestHeaders[name] = sv.ToString();
                    }
                }
            }

            try
            {
                var request = new HttpRequestMessage(new HttpMethod(method), fetchUrl);
                if (requestBody != null)
                    request.Content = new StringContent(requestBody, System.Text.Encoding.UTF8,
                        requestHeaders.TryGetValue("Content-Type", out var ct) ? ct : "text/plain");
                foreach (var kv in requestHeaders)
                {
                    if (!string.Equals(kv.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                        request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }

                var response = SharedHttpClient.SendAsync(request).GetAwaiter().GetResult();
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var statusCode = (int)response.StatusCode;

                responseObj.FastAddValue((KeyString)"ok", response.IsSuccessStatusCode ? JSBoolean.True : JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                responseObj.FastAddValue((KeyString)"status", new JSNumber(statusCode), JSPropertyAttributes.EnumerableConfigurableValue);
                responseObj.FastAddValue((KeyString)"statusText", new JSString(response.ReasonPhrase ?? string.Empty), JSPropertyAttributes.EnumerableConfigurableValue);
                responseObj.FastAddValue((KeyString)"url", new JSString(fetchUrl), JSPropertyAttributes.EnumerableConfigurableValue);
                responseObj.FastAddValue((KeyString)"redirected", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                responseObj.FastAddValue((KeyString)"type", new JSString("basic"), JSPropertyAttributes.EnumerableConfigurableValue);
                responseObj.FastAddValue((KeyString)"bodyUsed", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);

                // response.headers — Headers-like object with get(), has(), forEach()
                var allHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in response.Headers)
                    allHeaders[h.Key] = string.Join(", ", h.Value);
                if (response.Content.Headers != null)
                {
                    foreach (var h in response.Content.Headers)
                        allHeaders[h.Key] = string.Join(", ", h.Value);
                }

                var headersObj = new JSObject();
                headersObj.FastAddValue((KeyString)"get", new JSFunction((in Arguments hArgs) =>
                {
                    if (hArgs.Length == 0) return JSNull.Value;
                    var name = hArgs[0].ToString();
                    return allHeaders.TryGetValue(name, out var val) ? (JSValue)new JSString(val) : JSNull.Value;
                }, "get", 1), JSPropertyAttributes.EnumerableConfigurableValue);
                headersObj.FastAddValue((KeyString)"has", new JSFunction((in Arguments hArgs) =>
                {
                    if (hArgs.Length == 0) return JSBoolean.False;
                    return allHeaders.ContainsKey(hArgs[0].ToString()) ? JSBoolean.True : JSBoolean.False;
                }, "has", 1), JSPropertyAttributes.EnumerableConfigurableValue);
                headersObj.FastAddValue((KeyString)"forEach", new JSFunction((in Arguments hArgs) =>
                {
                    if (hArgs.Length > 0 && hArgs[0] is JSFunction cb)
                    {
                        foreach (var kv in allHeaders)
                        {
                            try { cb.InvokeFunction(new Arguments(cb, new JSString(kv.Value), new JSString(kv.Key))); }
                            catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.fetch.headers.forEach", $"Callback error: {ex.Message}", ex); }
                        }
                    }
                    return JSUndefined.Value;
                }, "forEach", 1), JSPropertyAttributes.EnumerableConfigurableValue);
                responseObj.FastAddValue((KeyString)"headers", headersObj, JSPropertyAttributes.EnumerableConfigurableValue);

                // response.text() — returns a thenable with the body text
                responseObj.FastAddValue((KeyString)"text", new JSFunction((in Arguments _) =>
                {
                    var thenable = new JSObject();
                    thenable.FastAddValue((KeyString)"then", new JSFunction((in Arguments thenArgs) =>
                    {
                        if (thenArgs.Length > 0 && thenArgs[0] is JSFunction cb)
                        {
                            try { cb.InvokeFunction(new Arguments(cb, new JSString(body))); }
                            catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.fetch.text", $"Callback error: {ex.Message}", ex); }
                        }
                        return thenable;
                    }, "then", 1), JSPropertyAttributes.EnumerableConfigurableValue);
                    return thenable;
                }, "text", 0), JSPropertyAttributes.EnumerableConfigurableValue);

                // response.json() — returns a thenable with parsed JSON
                responseObj.FastAddValue((KeyString)"json", new JSFunction((in Arguments jsonArgs) =>
                {
                    var thenable = new JSObject();
                    thenable.FastAddValue((KeyString)"then", new JSFunction((in Arguments thenArgs) =>
                    {
                        if (thenArgs.Length > 0 && thenArgs[0] is JSFunction cb)
                        {
                            try
                            {
                                var escaped = body
                                    .Replace("\\", "\\\\")
                                    .Replace("\"", "\\\"")
                                    .Replace("\n", "\\n")
                                    .Replace("\r", "\\r")
                                    .Replace("\t", "\\t")
                                    .Replace("\b", "\\b")
                                    .Replace("\f", "\\f");
                                var parsed = context.Eval($"JSON.parse(\"{escaped}\")");
                                cb.InvokeFunction(new Arguments(cb, parsed));
                            }
                            catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.fetch.json", $"JSON parse error: {ex.Message}", ex); }
                        }
                        return thenable;
                    }, "then", 1), JSPropertyAttributes.EnumerableConfigurableValue);
                    return thenable;
                }, "json", 0), JSPropertyAttributes.EnumerableConfigurableValue);

                // response.arrayBuffer() — returns a thenable with empty array buffer stub
                responseObj.FastAddValue((KeyString)"arrayBuffer", new JSFunction((in Arguments _) =>
                {
                    var thenable = new JSObject();
                    thenable.FastAddValue((KeyString)"then", new JSFunction((in Arguments thenArgs) =>
                    {
                        if (thenArgs.Length > 0 && thenArgs[0] is JSFunction cb)
                        {
                            try { cb.InvokeFunction(new Arguments(cb, new JSObject())); }
                            catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.fetch.arrayBuffer", $"Callback error: {ex.Message}", ex); }
                        }
                        return thenable;
                    }, "then", 1), JSPropertyAttributes.EnumerableConfigurableValue);
                    return thenable;
                }, "arrayBuffer", 0), JSPropertyAttributes.EnumerableConfigurableValue);

                // response.clone() — returns a shallow copy
                responseObj.FastAddValue((KeyString)"clone", new JSFunction((in Arguments _) => responseObj, "clone", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.fetch", $"Fetch error: {ex.Message}", ex);
                responseObj.FastAddValue((KeyString)"ok", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                responseObj.FastAddValue((KeyString)"status", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
                responseObj.FastAddValue((KeyString)"statusText", new JSString(ex.Message), JSPropertyAttributes.EnumerableConfigurableValue);
                responseObj.FastAddValue((KeyString)"url", new JSString(fetchUrl), JSPropertyAttributes.EnumerableConfigurableValue);
                var emptyHeaders = new JSObject();
                emptyHeaders.FastAddValue((KeyString)"get", new JSFunction((in Arguments _) => JSNull.Value, "get", 1), JSPropertyAttributes.EnumerableConfigurableValue);
                emptyHeaders.FastAddValue((KeyString)"has", new JSFunction((in Arguments _) => JSBoolean.False, "has", 1), JSPropertyAttributes.EnumerableConfigurableValue);
                emptyHeaders.FastAddValue((KeyString)"forEach", new JSFunction((in Arguments _) => JSUndefined.Value, "forEach", 1), JSPropertyAttributes.EnumerableConfigurableValue);
                responseObj.FastAddValue((KeyString)"headers", emptyHeaders, JSPropertyAttributes.EnumerableConfigurableValue);
            }

            // Return a thenable (Promise-like) that resolves immediately
            var promise = new JSObject();
            promise.FastAddValue((KeyString)"then", new JSFunction((in Arguments thenArgs) =>
            {
                if (thenArgs.Length > 0 && thenArgs[0] is JSFunction cb)
                {
                    try { cb.InvokeFunction(new Arguments(cb, responseObj)); }
                    catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.fetch.then", $"Callback error: {ex.Message}", ex); }
                }
                return promise;
            }, "then", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            promise.FastAddValue((KeyString)"catch", new JSFunction((in Arguments catchArgs) =>
            {
                // catch is a no-op for successful fetches
                return promise;
            }, "catch", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            return promise;
        }, "fetch", 1);

        window.FastAddValue((KeyString)"fetch", fetchFn, JSPropertyAttributes.EnumerableConfigurableValue);

        // window.getComputedStyle(element, pseudoElement)
        var bridgeForStyle = this;
        window.FastAddValue(
            (KeyString)"getComputedStyle",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return new JSObject();
                var targetObj = a[0] as JSObject;
                var el = targetObj != null ? bridgeForStyle.FindDomElementByJSObject(targetObj) : null;
                return bridgeForStyle.BuildComputedStyleObject(el);
            }, "getComputedStyle", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // XMLHttpRequest — basic polyfill backed by HttpClient
        RegisterXMLHttpRequest(context);

        context["window"] = window;

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

        // DOMException constructor
        RegisterDOMException(context);

        // Node constructor with type constants
        RegisterNodeConstructor(context);
    }

    /// <summary>
    /// Registers a basic <c>XMLHttpRequest</c> constructor on the context.
    /// Supports <c>open</c>, <c>send</c>, <c>setRequestHeader</c>,
    /// <c>onreadystatechange</c>, <c>readyState</c>, <c>status</c>, and <c>responseText</c>.
    /// </summary>
    private static void RegisterXMLHttpRequest(JSContext context) => context.Eval(@"
                function XMLHttpRequest() {
                    this.readyState = 0;
                    this.status = 0;
                    this.statusText = '';
                    this.responseText = '';
                    this.responseType = '';
                    this.responseURL = '';
                    this.responseXML = null;
                    this.onreadystatechange = null;
                    this.onload = null;
                    this.onerror = null;
                    this.onabort = null;
                    this.onprogress = null;
                    this.onloadstart = null;
                    this.onloadend = null;
                    this.ontimeout = null;
                    this.withCredentials = false;
                    this.timeout = 0;
                    this._method = 'GET';
                    this._url = '';
                    this._async = true;
                    this._headers = {};
                    this._responseHeaders = {};
                    this._mimeOverride = null;
                    this._aborted = false;
                    this.UNSENT = 0;
                    this.OPENED = 1;
                    this.HEADERS_RECEIVED = 2;
                    this.LOADING = 3;
                    this.DONE = 4;
                }
                XMLHttpRequest.UNSENT = 0;
                XMLHttpRequest.OPENED = 1;
                XMLHttpRequest.HEADERS_RECEIVED = 2;
                XMLHttpRequest.LOADING = 3;
                XMLHttpRequest.DONE = 4;
                XMLHttpRequest.prototype.open = function(method, url, isAsync) {
                    this._method = method;
                    this._url = url;
                    this._async = isAsync !== false;
                    this.readyState = 1;
                    this.status = 0;
                    this.statusText = '';
                    this.responseText = '';
                    this.responseURL = '';
                    this._responseHeaders = {};
                    this._aborted = false;
                    if (typeof this.onreadystatechange === 'function') {
                        this.onreadystatechange();
                    }
                };
                XMLHttpRequest.prototype.setRequestHeader = function(name, value) {
                    this._headers[name] = value;
                };
                XMLHttpRequest.prototype.getResponseHeader = function(name) {
                    if (!name) return null;
                    var lower = name.toLowerCase();
                    for (var key in this._responseHeaders) {
                        if (key.toLowerCase() === lower) return this._responseHeaders[key];
                    }
                    return null;
                };
                XMLHttpRequest.prototype.getAllResponseHeaders = function() {
                    var result = '';
                    for (var key in this._responseHeaders) {
                        result += key.toLowerCase() + ': ' + this._responseHeaders[key] + '\r\n';
                    }
                    return result;
                };
                XMLHttpRequest.prototype.overrideMimeType = function(mime) {
                    this._mimeOverride = mime;
                };
                XMLHttpRequest.prototype.abort = function() {
                    this._aborted = true;
                    this.readyState = 0;
                    this.status = 0;
                    this.statusText = '';
                    this.responseText = '';
                    if (typeof this.onabort === 'function') {
                        this.onabort();
                    }
                    if (typeof this.onloadend === 'function') {
                        this.onloadend();
                    }
                };
                XMLHttpRequest.prototype.send = function(body) {
                    var self = this;
                    if (self._aborted) return;
                    try {
                        var opts = { method: self._method };
                        if (body && self._method !== 'GET' && self._method !== 'HEAD') {
                            opts.body = '' + body;
                        }
                        var hasHeaders = false;
                        for (var k in self._headers) { hasHeaders = true; break; }
                        if (hasHeaders) {
                            opts.headers = self._headers;
                        }
                        if (typeof self.onloadstart === 'function') {
                            self.onloadstart();
                        }
                        fetch(self._url, opts).then(function(response) {
                            if (self._aborted) return;
                            self.status = response.status;
                            self.statusText = response.statusText;
                            self.responseURL = response.url || self._url;
                            self.readyState = 2;
                            if (response.headers && typeof response.headers.forEach === 'function') {
                                response.headers.forEach(function(value, name) {
                                    self._responseHeaders[name] = value;
                                });
                            }
                            if (typeof self.onreadystatechange === 'function') {
                                self.onreadystatechange();
                            }
                            response.text().then(function(text) {
                                if (self._aborted) return;
                                self.responseText = text;
                                self.readyState = 3;
                                if (typeof self.onprogress === 'function') {
                                    self.onprogress();
                                }
                                self.readyState = 4;
                                if (typeof self.onreadystatechange === 'function') {
                                    self.onreadystatechange();
                                }
                                if (typeof self.onload === 'function') {
                                    self.onload();
                                }
                                if (typeof self.onloadend === 'function') {
                                    self.onloadend();
                                }
                            });
                        });
                    } catch(e) {
                        self.readyState = 4;
                        self.status = 0;
                        if (typeof self.onreadystatechange === 'function') {
                            self.onreadystatechange();
                        }
                        if (typeof self.onerror === 'function') {
                            self.onerror();
                        }
                        if (typeof self.onloadend === 'function') {
                            self.onloadend();
                        }
                    }
                };
            ");

    /// <summary>
    /// Builds a <c>console</c> object exposing <c>log</c>, <c>warn</c>,
    /// <c>error</c>, and <c>info</c>.
    /// </summary>
    private static JSObject BuildConsoleObject()
    {
        var console = new JSObject();

        console.FastAddValue(
            (KeyString)"log",
            new JSFunction((in Arguments a) =>
            {
                var parts = new List<string>();
                for (var i = 0; i < a.Length; i++)
                    parts.Add(a[i]?.ToString() ?? "undefined");
                RenderLogger.LogDebug(LogCategory.JavaScript, "console.log", string.Join(" ", parts));
                return JSUndefined.Value;
            }, "log"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        console.FastAddValue(
            (KeyString)"warn",
            new JSFunction((in Arguments a) =>
            {
                var parts = new List<string>();
                for (var i = 0; i < a.Length; i++)
                    parts.Add(a[i]?.ToString() ?? "undefined");
                RenderLogger.Log(LogCategory.JavaScript, LogLevel.Warning, "console.warn", string.Join(" ", parts));
                return JSUndefined.Value;
            }, "warn"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        console.FastAddValue(
            (KeyString)"error",
            new JSFunction((in Arguments a) =>
            {
                var parts = new List<string>();
                for (var i = 0; i < a.Length; i++)
                    parts.Add(a[i]?.ToString() ?? "undefined");
                RenderLogger.Log(LogCategory.JavaScript, LogLevel.Error, "console.error", string.Join(" ", parts));
                return JSUndefined.Value;
            }, "error"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        console.FastAddValue(
            (KeyString)"info",
            new JSFunction((in Arguments a) =>
            {
                var parts = new List<string>();
                for (var i = 0; i < a.Length; i++)
                    parts.Add(a[i]?.ToString() ?? "undefined");
                RenderLogger.LogDebug(LogCategory.JavaScript, "console.info", string.Join(" ", parts));
                return JSUndefined.Value;
            }, "info"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return console;
    }

}
