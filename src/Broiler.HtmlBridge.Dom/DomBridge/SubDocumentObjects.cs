using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private JSObject BuildSubDocument(DomElement docRoot)
    {
        var doc = new JSObject();
        _docRootToDocJSObject[docRoot] = doc;
        // Map docRoot → doc JSObject so that ToJSObject(docRoot) returns the doc
        // object. This ensures strict equality checks like 'range.startContainer === doc' work.
        _jsObjectCache[docRoot] = doc;
        var bridge = this;

        DomElement GetDocumentElement() =>
            docRoot.Children.FirstOrDefault(c => !c.IsTextNode && !c.TagName.StartsWith("#"))
            ?? docRoot;

        doc.FastAddProperty(
            (KeyString)"documentElement",
            new JSFunction((in Arguments _) => ToJSObject(DomBridge.GetDocumentElement(docRoot)), "get documentElement"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        doc.FastAddProperty(
            (KeyString)"scrollingElement",
            new JSFunction((in Arguments _) => ToJSObject(DomBridge.GetDocumentElement(docRoot)), "get scrollingElement"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // body
        doc.FastAddProperty(
            (KeyString)"body",
            new JSFunction((in Arguments _) => JsSubDocumentObjectsGetBody003Core(docRoot, in _), "get body"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // head
        doc.FastAddProperty(
            (KeyString)"head",
            new JSFunction((in Arguments _) => JsSubDocumentObjectsGetHead004Core(docRoot, in _), "get head"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // title (dynamic getter from <title> element in <head>)
        doc.FastAddProperty(
            (KeyString)"title",
            new JSFunction((in Arguments _) => JsSubDocumentObjectsGetTitle005Core(docRoot, in _), "get title"),
            new JSFunction((in Arguments a) => JsSubDocumentObjectsSetTitle006Core(docRoot, in a), "set title"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // forms (dynamic collection of <form> elements)
        doc.FastAddProperty(
            (KeyString)"forms",
            new JSFunction((in Arguments _) => JsSubDocumentObjectsGetForms007Core(docRoot, in _), "get forms"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // childNodes
        doc.FastAddProperty(
            (KeyString)"childNodes",
            new JSFunction((in Arguments _) => JsSubDocumentObjectsGetChildNodes008Core(docRoot, in _), "get childNodes"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // firstChild
        doc.FastAddProperty(
            (KeyString)"firstChild",
            new JSFunction((in Arguments _) => docRoot.Children.Count > 0 ? ToJSObject(docRoot.Children[0]) : JSNull.Value, "get firstChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastChild
        doc.FastAddProperty(
            (KeyString)"lastChild",
            new JSFunction((in Arguments _) => docRoot.Children.Count > 0 ? ToJSObject(docRoot.Children[^1]) : JSNull.Value, "get lastChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // hasChildNodes()
        doc.FastAddValue(
            (KeyString)"hasChildNodes",
            new JSFunction((in Arguments _) => docRoot.Children.Count > 0 ? JSBoolean.True : JSBoolean.False,
                "hasChildNodes", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // nodeType = DOCUMENT_NODE (9)
        doc.FastAddProperty(
            (KeyString)"nodeType",
            new JSFunction((in Arguments _) => new JSNumber(9), "get nodeType"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeName = "#document"
        doc.FastAddProperty(
            (KeyString)"nodeName",
            new JSFunction((in Arguments _) => new JSString("#document"), "get nodeName"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // localName = null for document
        doc.FastAddProperty(
            (KeyString)"localName",
            NullFunction("get localName"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // getElementById(id)
        doc.FastAddValue(
            (KeyString)"getElementById",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsGetElementById014Core(docRoot, in a), "getElementById", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getElementsByTagName(tag)
        doc.FastAddValue(
            (KeyString)"getElementsByTagName",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsGetElementsByTagName015Core(docRoot, in a), "getElementsByTagName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createElement(tag)
        doc.FastAddValue(
            (KeyString)"createElement",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsCreateElement016Core(docRoot, in a), "createElement", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createTextNode(text)
        doc.FastAddValue(
            (KeyString)"createTextNode",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsCreateTextNode017Core(docRoot, in a), "createTextNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createComment(data)
        doc.FastAddValue(
            (KeyString)"createComment",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsCreateComment018Core(docRoot, in a), "createComment", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createElementNS(ns, localName)
        doc.FastAddValue(
            (KeyString)"createElementNS",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsCreateElementNS019Core(docRoot, in a), "createElementNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createEvent(type)
        doc.FastAddValue(
            (KeyString)"createEvent",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsCreateEvent034Core(in a), "createEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // querySelector / querySelectorAll
        doc.FastAddValue(
            (KeyString)"querySelector",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsQuerySelector035Core(docRoot, in a), "querySelector", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        doc.FastAddValue(
            (KeyString)"querySelectorAll",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsQuerySelectorAll036Core(docRoot, in a), "querySelectorAll", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        doc.FastAddValue(
            (KeyString)"elementFromPoint",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsElementFromPoint037Core(docRoot, in a), "elementFromPoint", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        doc.FastAddValue(
            (KeyString)"elementsFromPoint",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsElementsFromPoint038Core(docRoot, in a), "elementsFromPoint", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.open()
        doc.FastAddValue(
            (KeyString)"open",
            new JSFunction((in Arguments _) => JsSubDocumentObjectsOpen039Core(doc, docRoot, in _), "open", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.close()
        doc.FastAddValue(
            (KeyString)"close",
            UndefinedFunction("close", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.write(html)
        doc.FastAddValue(
            (KeyString)"write",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsWrite040Core(bridge, docRoot, in a), "write", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.images
        doc.FastAddProperty(
            (KeyString)"images",
            new JSFunction((in Arguments _) => JsSubDocumentObjectsGetImages041Core(docRoot, in _), "get images"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.links
        doc.FastAddProperty(
            (KeyString)"links",
            new JSFunction((in Arguments _) => JsSubDocumentObjectsGetLinks042Core(docRoot, in _), "get links"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.styleSheets
        doc.FastAddProperty(
            (KeyString)"styleSheets",
            new JSFunction((in Arguments _) => bridge.BuildStyleSheetsCollection(docRoot), "get styleSheets"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // removeChild on document
        doc.FastAddValue(
            (KeyString)"removeChild",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsRemoveChild044Core(bridge, docRoot, in a), "removeChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // appendChild on document
        doc.FastAddValue(
            (KeyString)"appendChild",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsAppendChild045Core(bridge, docRoot, in a), "appendChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        doc.FastAddValue(
            (KeyString)"append",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsAppend046Core(bridge, docRoot, in a), "append", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        doc.FastAddValue(
            (KeyString)"prepend",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsPrepend047Core(bridge, docRoot, in a), "prepend", 0),
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
        subImpl.FastAddValue(
            (KeyString)"hasFeature",
            TrueFunction("hasFeature", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        subImpl.FastAddValue(
            (KeyString)"createDocumentType",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsCreateDocumentType048Core(in a), "createDocumentType", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        subImpl.FastAddValue(
            (KeyString)"createDocument",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsCreateDocument049Core(in a), "createDocument", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        subImpl.FastAddValue(
            (KeyString)"createHTMLDocument",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsCreateHTMLDocument050Core(in a), "createHTMLDocument", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        doc.FastAddValue(
            (KeyString)"implementation",
            subImpl,
            JSPropertyAttributes.EnumerableConfigurableValue);

        // defaultView — return the main window object so getComputedStyle is accessible
        if (_windowJSObject != null)
        {
            doc.FastAddValue(
                (KeyString)"defaultView",
                _windowJSObject,
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // createTreeWalker(root, whatToShow, filter)
        doc.FastAddValue(
            (KeyString)"createTreeWalker",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsCreateTreeWalker051Core(bridge, in a), "createTreeWalker", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createNodeIterator(root, whatToShow, filter)
        doc.FastAddValue(
            (KeyString)"createNodeIterator",
            new JSFunction((in Arguments a) => JsSubDocumentObjectsCreateNodeIterator052Core(bridge, in a), "createNodeIterator", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createRange()
        doc.FastAddValue(
            (KeyString)"createRange",
            new JSFunction((in Arguments a) => bridge.BuildRange(docRoot), "createRange", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return doc;
    }

    /// <summary>Finds the first element in a sub-tree matching a predicate.</summary>
    private DomElement? FindInSubTree(DomElement root, Func<DomElement, bool> predicate)
    {
        foreach (var child in root.Children)
        {
            if (!child.IsTextNode && !child.TagName.StartsWith("#") && predicate(child))
                return child;
            var found = FindInSubTree(child, predicate);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>Finds the first element in a tree matching a predicate (includes the root).</summary>
    private static DomElement? FindInTree(DomElement root, Func<DomElement, bool> predicate)
    {
        if (predicate(root)) return root;
        foreach (var child in root.Children)
        {
            var found = FindInTree(child, predicate);
            if (found != null) return found;
        }
        return null;
    }

}
