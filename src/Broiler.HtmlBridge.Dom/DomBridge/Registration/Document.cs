using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private void RegisterDocumentBasics(JSContext context, JSObject document)
    {
        // document.documentElement (the <html> element)
        document.FastAddValue(
            (KeyString)"documentElement",
            ToJSObject(DocumentElement),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.scrollingElement (getter — returns document.documentElement
        // in standards mode, or document.body in quirks mode; we always use
        // standards mode so it's always the <html> element).
        document.FastAddProperty(
            (KeyString)"scrollingElement",
            new JSFunction((in Arguments _) => ToJSObject(DocumentElement), "get scrollingElement"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        // document.body (getter — first <body> child of documentElement)
        document.FastAddProperty(
            (KeyString)"body",
            new JSFunction(JsRegistrationGetBody002Core, "get body"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        // document.head (getter — first <head> child of documentElement)
        document.FastAddProperty(
            (KeyString)"head",
            new JSFunction(JsRegistrationGetHead003Core, "get head"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        // document.title (getter / setter)
        document.FastAddProperty(
            (KeyString)"title",
            new JSFunction((in Arguments a) => new JSString(Title), "get title"),
            new JSFunction(JsRegistrationSetTitle005Core, "set title"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
        // document.getElementById(id)
        document.FastAddValue(
            (KeyString)"getElementById",
            new JSFunction(JsRegistrationGetElementById006Core, "getElementById", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.getElementsByTagName(tag)
        document.FastAddValue(
            (KeyString)"getElementsByTagName",
            new JSFunction(JsRegistrationGetElementsByTagName007Core, "getElementsByTagName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.getElementsByClassName(className)
        document.FastAddValue(
            (KeyString)"getElementsByClassName",
            new JSFunction(JsRegistrationGetElementsByClassName008Core, "getElementsByClassName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.querySelector(selector)
        document.FastAddValue(
            (KeyString)"querySelector",
            new JSFunction(JsRegistrationQuerySelector009Core, "querySelector", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.querySelectorAll(selector)
        document.FastAddValue(
            (KeyString)"querySelectorAll",
            new JSFunction(JsRegistrationQuerySelectorAll010Core, "querySelectorAll", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue(
            (KeyString)"elementFromPoint",
            new JSFunction(JsRegistrationElementFromPoint011Core, "elementFromPoint", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue(
            (KeyString)"elementsFromPoint",
            new JSFunction(JsRegistrationElementsFromPoint012Core, "elementsFromPoint", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.getAnimations() — minimal Web Animations API support used by WPT.
        document.FastAddValue(
            (KeyString)"getAnimations",
            new JSFunction((in Arguments _) => BuildAnimationList(null), "getAnimations", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.createElement(tag)
        document.FastAddValue(
            (KeyString)"createElement",
            new JSFunction((in Arguments a) => JsRegistrationCreateElement014Core(context, in a), "createElement", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.createTextNode(text)
        document.FastAddValue(
            (KeyString)"createTextNode",
            new JSFunction(JsRegistrationCreateTextNode015Core, "createTextNode", 1),
             JSPropertyAttributes.EnumerableConfigurableValue);
        // document.createAttribute(name)
        document.FastAddValue(
            (KeyString)"createAttribute",
            new JSFunction((in Arguments a) => JsRegistrationCreateAttribute016Core(context, in a), "createAttribute", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.createDocumentFragment() — basic iframe/fragment support
        document.FastAddValue(
            (KeyString)"createDocumentFragment",
            new JSFunction(JsRegistrationCreateDocumentFragment017Core, "createDocumentFragment", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.createEvent(type) — DOM Events Level 3
        document.FastAddValue(
            (KeyString)"createEvent",
            new JSFunction(JsRegistrationCreateEvent033Core, "createEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
    }

    private void RegisterDocumentWriting(JSObject document)
    {
        // document.write(html) — parse and insert at the current script position
        document.FastAddValue(
            (KeyString)"write",
            new JSFunction(JsRegistrationWrite036Core, "write", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.writeln(html) — same as write, with trailing newline
        var writeFn = (JSFunction)document[(KeyString)"write"];
        document.FastAddValue(
            (KeyString)"writeln",
            new JSFunction((in Arguments a) => JsRegistrationWriteln037Core(writeFn, in a), "writeln", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
    }

    private void RegisterDocumentNodeAndCollectionApis(JSContext context, JSObject document)
    {
        // Node type constants on document
        document.FastAddValue((KeyString)"ELEMENT_NODE", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"TEXT_NODE", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"COMMENT_NODE", new JSNumber(8), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"DOCUMENT_NODE", new JSNumber(9), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"DOCUMENT_TYPE_NODE", new JSNumber(10), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"DOCUMENT_FRAGMENT_NODE", new JSNumber(11), JSPropertyAttributes.EnumerableConfigurableValue);
        // document.nodeType = DOCUMENT_NODE (9)
        document.FastAddProperty(
            (KeyString)"nodeType",
            new JSFunction((in Arguments _) => new JSNumber(9), "get nodeType"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        // document.nodeName = "#document"
        document.FastAddProperty(
            (KeyString)"nodeName",
            new JSFunction((in Arguments _) => new JSString("#document"), "get nodeName"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        // document.firstChild (getter — returns first child of document: DOCTYPE if present, else documentElement)
        document.FastAddProperty(
            (KeyString)"firstChild",
            new JSFunction((in Arguments _) => _documentNode.Children.Count > 0 ? ToJSObject(_documentNode.Children[0]) : JSNull.Value, "get firstChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        // document.lastChild (getter — returns last child of document, typically documentElement)
        document.FastAddProperty(
            (KeyString)"lastChild",
            new JSFunction((in Arguments _) => _documentNode.Children.Count > 0 ? ToJSObject(_documentNode.Children[^1]) : JSNull.Value, "get lastChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        // document.childNodes (getter — returns children of document node: [DOCTYPE, documentElement])
        document.FastAddProperty(
            (KeyString)"childNodes",
            new JSFunction(JsRegistrationGetChildNodes046Core, "get childNodes"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        // document.removeChild(child)
        var docNodeForMutation = _documentNode;
        document.FastAddValue(
            (KeyString)"removeChild",
            new JSFunction((in Arguments a) => JsRegistrationRemoveChild047Core(docNodeForMutation, in a), "removeChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.appendChild(child)
        document.FastAddValue(
            (KeyString)"appendChild",
            new JSFunction((in Arguments a) => JsRegistrationAppendChild048Core(docNodeForMutation, in a), "appendChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.insertBefore(newChild, refChild)
        document.FastAddValue(
            (KeyString)"insertBefore",
            new JSFunction((in Arguments a) => JsRegistrationInsertBefore049Core(docNodeForMutation, in a), "insertBefore", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.forms — collection of all <form> elements with named access
        document.FastAddProperty(
            (KeyString)"forms",
            new JSFunction(JsRegistrationGetForms050Core, "get forms"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        // document.createElementNS(namespace, tagName)
        document.FastAddValue(
            (KeyString)"createElementNS",
            new JSFunction((in Arguments a) => JsRegistrationCreateElementNS051Core(context, in a), "createElementNS", 2),
             JSPropertyAttributes.EnumerableConfigurableValue);
        // document.createAttributeNS(namespace, qualifiedName)
        document.FastAddValue(
            (KeyString)"createAttributeNS",
            new JSFunction((in Arguments a) => JsRegistrationCreateAttributeNS052Core(context, in a), "createAttributeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.images — collection of all <img> elements
        document.FastAddProperty(
            (KeyString)"images",
            new JSFunction(JsRegistrationGetImages053Core, "get images"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        // document.links — collection of all <a> and <area> elements with href
        // Uses tree-order traversal instead of _knownNodes insertion order
        // to correctly reflect dynamically appended elements.
        document.FastAddProperty(
            (KeyString)"links",
            new JSFunction(JsRegistrationGetLinks054Core, "get links"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        // document.styleSheets — collection of stylesheet objects for main document
        document.FastAddProperty(
            (KeyString)"styleSheets",
            new JSFunction(JsRegistrationGetStyleSheets055Core, "get styleSheets"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        // document.open() — for main document
        document.FastAddValue(
            (KeyString)"open",
            new JSFunction((in Arguments _) => document, "open", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.close() — for main document
        document.FastAddValue(
            (KeyString)"close",
            UndefinedFunction("close", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.implementation — DOMImplementation
        var implementation = new JSObject();
        // implementation.hasFeature() — always returns true per spec
        implementation.FastAddValue(
            (KeyString)"hasFeature",
            TrueFunction("hasFeature", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // implementation.createDocumentType(qualifiedName, publicId, systemId)
        implementation.FastAddValue(
            (KeyString)"createDocumentType",
            new JSFunction((in Arguments a) => JsRegistrationCreateDocumentType057Core(context, in a), "createDocumentType", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // implementation.createDocument(namespace, qualifiedName, doctype)
        implementation.FastAddValue(
            (KeyString)"createDocument",
            new JSFunction((in Arguments a) => JsRegistrationCreateDocument058Core(context, in a), "createDocument", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // implementation.createHTMLDocument(title)
        implementation.FastAddValue(
            (KeyString)"createHTMLDocument",
            new JSFunction(JsRegistrationCreateHTMLDocument059Core, "createHTMLDocument", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue(
            (KeyString)"implementation",
            implementation,
            JSPropertyAttributes.EnumerableConfigurableValue);
    }

    private void RegisterDocumentEventTargetAndMetadata(JSObject document)
    {
        // document-level addEventListener / removeEventListener / dispatchEvent
        var docNode = _documentNode;
        var bridgeRef = this;
        document.FastAddValue(
            (KeyString)"addEventListener",
            new JSFunction((in Arguments a) => JsRegistrationAddEventListener060Core(docNode, in a), "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue(
            (KeyString)"removeEventListener",
            new JSFunction((in Arguments a) => JsRegistrationRemoveEventListener061Core(docNode, in a), "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue(
            (KeyString)"dispatchEvent",
            new JSFunction((in Arguments a) => JsRegistrationDispatchEvent062Core(bridgeRef, docNode, in a), "dispatchEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.contentType — returns the MIME type of the document
        document.FastAddProperty(
            (KeyString)"contentType",
            new JSFunction(JsRegistrationGetContentType063Core, "get contentType"),
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
    }

}
