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
        document.FastAddValue((KeyString)"documentElement", ToJSObject(DocumentElement), JSPropertyAttributes.EnumerableConfigurableValue);

        // document.scrollingElement (getter — returns document.documentElement
        // in standards mode, or document.body in quirks mode; we always use
        // standards mode so it's always the <html> element).
        document.FastAddProperty((KeyString)"scrollingElement", new JSFunction((in _) => ToJSObject(DocumentElement), "get scrollingElement"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document structural accessors — body/head/title, co-located in the DocumentStructureBinding
        // feature module (Phase 3).
        document.FastAddProperty((KeyString)"body", new JSFunction((in a) => Dom.Features.DocumentStructureBinding.GetBody(this, in a), "get body"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        document.FastAddProperty((KeyString)"head", new JSFunction((in a) => Dom.Features.DocumentStructureBinding.GetHead(this, in a), "get head"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        document.FastAddProperty((KeyString)"title", new JSFunction((in a) => Dom.Features.DocumentStructureBinding.GetTitle(this, in a), "get title"), new JSFunction((in a) => Dom.Features.DocumentStructureBinding.SetTitle(this, in a), "set title"), JSPropertyAttributes.EnumerableConfigurableProperty);

        // document element-query methods — getElementById/getElementsByTagName/getElementsByClassName/
        // querySelector/querySelectorAll, co-located in the DocumentQueryBinding feature module (Phase 3).
        document.FastAddValue((KeyString)"getElementById", new JSFunction((in a) => Dom.Features.DocumentQueryBinding.GetElementById(this, in a), "getElementById", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"getElementsByTagName", new JSFunction((in a) => Dom.Features.DocumentQueryBinding.GetElementsByTagName(this, in a), "getElementsByTagName", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"getElementsByClassName", new JSFunction((in a) => Dom.Features.DocumentQueryBinding.GetElementsByClassName(this, in a), "getElementsByClassName", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"querySelector", new JSFunction((in a) => Dom.Features.DocumentQueryBinding.QuerySelector(this, in a), "querySelector", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"querySelectorAll", new JSFunction((in a) => Dom.Features.DocumentQueryBinding.QuerySelectorAll(this, in a), "querySelectorAll", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        // document.elementFromPoint / elementsFromPoint (hit-testing), co-located in the HitTestBinding
        // feature module (Phase 3).
        document.FastAddValue((KeyString)"elementFromPoint", new JSFunction((in a) => Dom.Features.HitTestBinding.ElementFromPoint(this, in a), "elementFromPoint", 2), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"elementsFromPoint", new JSFunction((in a) => Dom.Features.HitTestBinding.ElementsFromPoint(this, in a), "elementsFromPoint", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        // document.getAnimations() — minimal Web Animations API support used by WPT.
        document.FastAddValue((KeyString)"getAnimations", new JSFunction((in _) => BuildAnimationList(null), "getAnimations", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        // document node factories — createElement/createTextNode/createAttribute/createDocumentFragment,
        // co-located in the DocumentFactoryBinding feature module (Phase 3).
        document.FastAddValue((KeyString)"createElement", new JSFunction((in a) => Dom.Features.DocumentFactoryBinding.CreateElement(this, context, in a), "createElement", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"createTextNode", new JSFunction((in a) => Dom.Features.DocumentFactoryBinding.CreateTextNode(this, in a), "createTextNode", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"createAttribute", new JSFunction((in a) => Dom.Features.DocumentFactoryBinding.CreateAttribute(this, context, in a), "createAttribute", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"createDocumentFragment", new JSFunction((in a) => Dom.Features.DocumentFactoryBinding.CreateDocumentFragment(this, in a), "createDocumentFragment", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createEvent(type) — DOM Events Level 3 (Phase 3: co-located LegacyEventBinding module)
        document.FastAddValue((KeyString)"createEvent", new JSFunction(Dom.Features.LegacyEventBinding.Create, "createEvent", 1), JSPropertyAttributes.EnumerableConfigurableValue);
    }

    private void RegisterDocumentWriting(JSObject document)
    {
        // document.write(html) — parse and insert at the current script position (Phase 3:
        // co-located DocumentWriteBinding feature module).
        document.FastAddValue((KeyString)"write", new JSFunction((in a) => Dom.Features.DocumentWriteBinding.Write(this, in a), "write", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        // document.writeln(html) — same as write, with trailing newline
        var writeFn = (JSFunction)document[(KeyString)"write"];
        document.FastAddValue((KeyString)"writeln", new JSFunction((in a) => Dom.Features.DocumentWriteBinding.Writeln(writeFn, in a), "writeln", 1), JSPropertyAttributes.EnumerableConfigurableValue);
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
        document.FastAddProperty((KeyString)"nodeType", new JSFunction((in _) => new JSNumber(9), "get nodeType"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.nodeName = "#document"
        document.FastAddProperty((KeyString)"nodeName", new JSFunction((in _) => new JSString("#document"), "get nodeName"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.firstChild (getter — returns first child of document: DOCTYPE if present, else documentElement)
        document.FastAddProperty((KeyString)"firstChild", new JSFunction((in _) => _document.ChildNodes.Count > 0 ? ToJSObject(ChildAt(_document, 0)) : JSNull.Value, "get firstChild"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.lastChild (getter — returns last child of document, typically documentElement)
        document.FastAddProperty((KeyString)"lastChild", new JSFunction((in _) => _document.ChildNodes.Count > 0 ? ToJSObject(ChildAt(_document, ^1)) : JSNull.Value, "get lastChild"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document-node mutation — childNodes/removeChild/appendChild/insertBefore, co-located in the
        // NodeMutationBinding feature module (Phase 3).
        document.FastAddProperty((KeyString)"childNodes", new JSFunction((in a) => Dom.Features.NodeMutationBinding.GetChildNodes(this, in a), "get childNodes"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        document.FastAddValue((KeyString)"removeChild", new JSFunction((in a) => Dom.Features.NodeMutationBinding.RemoveChild(this, in a), "removeChild", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"appendChild", new JSFunction((in a) => Dom.Features.NodeMutationBinding.AppendChild(this, in a), "appendChild", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"insertBefore", new JSFunction((in a) => Dom.Features.NodeMutationBinding.InsertBefore(this, in a), "insertBefore", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        // document.forms — collection of all <form> elements with named access
        document.FastAddProperty((KeyString)"forms", new JSFunction((in a) => Dom.Features.DocumentCollectionBinding.GetForms(this, in a), "get forms"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.createElementNS(namespace, tagName)  — DocumentFactoryBinding (Phase 3)
        document.FastAddValue((KeyString)"createElementNS", new JSFunction((in a) => Dom.Features.DocumentFactoryBinding.CreateElementNS(this, context, in a), "createElementNS", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        // document.createAttributeNS(namespace, qualifiedName)  — DocumentFactoryBinding (Phase 3)
        document.FastAddValue((KeyString)"createAttributeNS", new JSFunction((in a) => Dom.Features.DocumentFactoryBinding.CreateAttributeNS(this, context, in a), "createAttributeNS", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        // document.images — collection of all <img> elements
        document.FastAddProperty((KeyString)"images", new JSFunction((in a) => Dom.Features.DocumentCollectionBinding.GetImages(this, in a), "get images"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.links — collection of all <a> and <area> elements with href
        // Uses tree-order traversal so dynamically appended elements are reflected.
        document.FastAddProperty((KeyString)"links", new JSFunction((in a) => Dom.Features.DocumentCollectionBinding.GetLinks(this, in a), "get links"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.styleSheets — collection of stylesheet objects for main document
        document.FastAddProperty((KeyString)"styleSheets", new JSFunction((in a) => Dom.Features.DocumentCollectionBinding.GetStyleSheets(this, in a), "get styleSheets"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.open() — for main document
        document.FastAddValue((KeyString)"open", new JSFunction((in _) => document, "open", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        // document.close() — for main document
        document.FastAddValue((KeyString)"close", UndefinedFunction("close", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        // document.implementation — DOMImplementation
        var implementation = new JSObject();

        // implementation.hasFeature() — always returns true per spec
        implementation.FastAddValue((KeyString)"hasFeature", TrueFunction("hasFeature", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        // document.implementation factories — createDocumentType/createDocument/createHTMLDocument,
        // co-located in the DocumentLevelFactoryBinding feature module (Phase 3).
        implementation.FastAddValue((KeyString)"createDocumentType", new JSFunction((in a) => Dom.Features.DocumentLevelFactoryBinding.CreateDocumentType(this, context, in a), "createDocumentType", 3), JSPropertyAttributes.EnumerableConfigurableValue);

        // implementation.createDocument(namespace, qualifiedName, doctype)
        implementation.FastAddValue((KeyString)"createDocument", new JSFunction((in a) => Dom.Features.DocumentLevelFactoryBinding.CreateDocument(this, context, in a), "createDocument", 3), JSPropertyAttributes.EnumerableConfigurableValue);

        // implementation.createHTMLDocument(title)
        implementation.FastAddValue((KeyString)"createHTMLDocument", new JSFunction((in a) => Dom.Features.DocumentLevelFactoryBinding.CreateHTMLDocument(this, in a), "createHTMLDocument", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        document.FastAddValue((KeyString)"implementation", implementation, JSPropertyAttributes.EnumerableConfigurableValue);
    }

    private void RegisterDocumentEventTargetAndMetadata(JSObject document)
    {
        // document-level addEventListener / removeEventListener / dispatchEvent, co-located in the
        // DocumentEventTargetBinding feature module (Phase 3).
        document.FastAddValue((KeyString)"addEventListener", new JSFunction((in a) => Dom.Features.DocumentEventTargetBinding.AddEventListener(this, in a), "addEventListener", 3), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"removeEventListener", new JSFunction((in a) => Dom.Features.DocumentEventTargetBinding.RemoveEventListener(this, in a), "removeEventListener", 3), JSPropertyAttributes.EnumerableConfigurableValue);
        document.FastAddValue((KeyString)"dispatchEvent", new JSFunction((in a) => Dom.Features.DocumentEventTargetBinding.DispatchEvent(this, in a), "dispatchEvent", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        // document.contentType — returns the MIME type of the document
        document.FastAddProperty((KeyString)"contentType", new JSFunction(JsRegistrationGetContentType063Core, "get contentType"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.URL — returns the document URL
        document.FastAddProperty((KeyString)"URL", new JSFunction((in _) => new JSString(_pageUrl), "get URL"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.documentURI — same as document.URL
        document.FastAddProperty((KeyString)"documentURI", new JSFunction((in _) => new JSString(_pageUrl), "get documentURI"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.compatMode — "CSS1Compat" for standards mode, "BackCompat" for quirks
        document.FastAddProperty((KeyString)"compatMode", new JSFunction((in _) => new JSString("CSS1Compat"), "get compatMode"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.characterSet — always UTF-8
        document.FastAddProperty((KeyString)"characterSet", new JSFunction((in _) => new JSString("UTF-8"), "get characterSet"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.inputEncoding — alias for characterSet
        document.FastAddProperty((KeyString)"inputEncoding", new JSFunction((in _) => new JSString("UTF-8"), "get inputEncoding"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
    }
}
