using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

/// <summary>
/// Conversion of <see cref="DomElement"/> instances to YantraJS
/// <see cref="JSObject"/> representations, including sub-document
/// construction and tree-search helpers.
/// </summary>
public sealed partial class DomBridge
{
    private const double DefaultBodyMarginPixels = 8;
    private const int MaxScrollContinuationDepth = 16;

    // RF-BRIDGE-1c Phase F (F3b): the JS-object registry is keyed by canonical DomNode so
    // text/comment nodes (which get JS wrappers) can round-trip once they flip to canonical
    // DomText/DomComment. A facade node IS-A DomNode, so this is a behaviour-preserving widen.
    // P2.2: wrapper identity now lives in JsObjectRegistry, the single authority (was the scattered
    // _jsObjectCache/_docRootToDocJSObject fields).
    private readonly Dom.Runtime.JsObjectRegistry _jsObjects = new();
    /// <summary>Counter for tracking top-layer insertion order via showModal().</summary>
    private int _topLayerCounter;

    internal JSObject ToJSObject(DomNode node)
    {
        if (_jsObjects.TryGet(node, out var cached))
            return cached;

        // Phase 4 item 1: a canonical DomDocument is the document root. The main document is in the
        // node-wrapper map above; a sub-document root's wrapper lives in the document-wrapper map
        // (P2.2/P4.4a). Resolve it here so e.g. documentElement.parentNode returns the document
        // object, not a fallthrough character-data wrapper.
        if (node is DomDocument documentNode && _jsObjects.TryGetDocument(documentNode, out var documentWrapper))
            return documentWrapper;

        var obj = new JSObject();
        _jsObjects.Set(node, obj);

        // RF-BRIDGE-1c Phase F (F3c): canonical character-data nodes (DomText/DomComment) are not
        // Broiler.Dom.DomElement, so they receive a minimal Node/CharacterData wrapper instead of the full
        // element surface below. This branch is dead on today's homogeneous facade tree — facade
        // text/comment nodes are Broiler.Dom.DomElement and fall through to the element wrapper, preserving
        // behaviour — and goes live once text/comment construction flips to canonical
        // DomText/DomComment (F3c construction cutover).
        if (node is DomDocumentType docType)
        {
            // Phase 4 item 1: the doctype is a canonical DomDocumentType (was a #doctype sentinel
            // element). It gets the minimal DocumentType surface, not the full element wrapper.
            PopulateDocumentTypeJSObject(obj, docType);
            return obj;
        }

        if (node is DomDocumentFragment fragment)
        {
            // Phase 4 item 1: the fragment is a canonical DomDocumentFragment (was a
            // #document-fragment sentinel element). It gets the DocumentFragment container surface
            // (Node base + ParentNode mixin + child manipulation), not the full element wrapper.
            PopulateDocumentFragmentJSObject(obj, fragment);
            return obj;
        }

        if (node is not DomElement element)
        {
            PopulateCharacterDataJSObject(obj, node);
            return obj;
        }

        var bridge = this;

        obj.FastAddValue((KeyString)"tagName",
            new JSString(string.IsNullOrEmpty(element.NamespaceUri) ||
                string.Equals(element.NamespaceUri, "http://www.w3.org/1999/xhtml", StringComparison.OrdinalIgnoreCase)
                    ? element.TagName.ToUpperInvariant()
                    : element.TagName),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddProperty((KeyString)"id",
            new JSFunction((in a) => element.Id != null ? new JSString(element.Id) : JSNull.Value, "get id"),
            new JSFunction((in a) => JsJsObjectsSetId002Core(bridge, element, in a), "set id"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // className (read/write) — reflects the 'class' content attribute
        obj.FastAddProperty((KeyString)"className",
            new JSFunction((in a) => JsJsObjectsGetClassName003Core(element, in a), "get className"),
            new JSFunction((in a) => JsJsObjectsSetClassName004Core(bridge, element, in a), "set className"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // title (read/write) — synced with attributes["title"]
        obj.FastAddProperty((KeyString)"title",
            new JSFunction((in a) => TryGetAttribute(element, "title", out var t) ? new JSString(t) : new JSString(string.Empty), "get title"),
            new JSFunction((in a) => JsJsObjectsSetTitle006Core(element, in a), "set title"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lang (read/write) — synced with attributes["lang"]
        obj.FastAddProperty((KeyString)"lang",
            new JSFunction((in a) => TryGetAttribute(element, "lang", out var lang) ? new JSString(lang) : new JSString(string.Empty), "get lang"),
            new JSFunction((in a) => JsJsObjectsSetLang008Core(element, in a), "set lang"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // accessKey (read/write) — synced with attributes["accesskey"]
        obj.FastAddProperty((KeyString)"accessKey",
            new JSFunction((in a) => TryGetAttribute(element, "accesskey", out var accessKey) ? new JSString(accessKey) : new JSString(string.Empty), "get accessKey"),
            new JSFunction((in a) => JsJsObjectsSetAccessKey010Core(element, in a), "set accessKey"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // dir (read/write) — synced with attributes["dir"]
        obj.FastAddProperty((KeyString)"dir",
            new JSFunction((in a) => TryGetAttribute(element, "dir", out var dir) ? new JSString(dir) : new JSString(string.Empty), "get dir"),
            new JSFunction((in a) => JsJsObjectsSetDir012Core(bridge, element, in a), "set dir"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // draggable (read/write) — reflected enumerated attribute
        obj.FastAddProperty((KeyString)"draggable",
            new JSFunction((in _) => JsJsObjectsGetDraggable013Core(element, in _), "get draggable"),
            new JSFunction((in a) => JsJsObjectsSetDraggable014Core(element, in a), "set draggable"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // innerHTML (read/write)
        obj.FastAddProperty((KeyString)"innerHTML",
            new JSFunction((in a) => new JSString(SerializeChildrenToHtml(element)), "get innerHTML"),
            new JSFunction((in a) => JsJsObjectsSetInnerHTML016Core(bridge, element, in a), "set innerHTML"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // outerHTML (read/write)
        obj.FastAddProperty((KeyString)"outerHTML",
            new JSFunction((in _) => new JSString(SerializeElementToHtml(element)), "get outerHTML"),
            new JSFunction((in a) => JsJsObjectsSetOuterHTML018Core(bridge, element, in a), "set outerHTML"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"shadowRoot",
            new JSFunction((in _) => JsJsObjectsGetShadowRoot019Core(element, in _), "get shadowRoot"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // textContent (read/write)
        obj.FastAddProperty((KeyString)"textContent",
            new JSFunction((in _) => GetNodeTextValue(element), "get textContent"),
            new JSFunction((in a) => JsJsObjectsSetTextContent021Core(bridge, element, in a), "set textContent"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"innerText",
            new JSFunction((in _) => GetNodeTextValue(element), "get innerText"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"outerText",
            new JSFunction((in _) => GetNodeTextValue(element), "get outerText"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // style object — CSS property access and manipulation.
        // In browsers, `element.style` is a read-only property: assigning a
        // string sets `style.cssText` instead of replacing the object.
        // Phase 4 item 2: after every element.style mutation (per-property set, cssText, setProperty,
        // removeProperty, cssFloat — all route through this onMutation), write the dict through to the
        // canonical style= attribute so element.style and getAttribute("style") observe one state,
        // then invalidate computed style.
        var styleObj = Dom.Features.StyleDeclarationBinding.BuildInlineDeclaration(element, () =>
        {
            bridge.SyncStyleAttributeFromInlineStyle(element);
            bridge.InvalidateStyleScope(element);
        });
        obj.FastAddProperty((KeyString)"style",
            new JSFunction((in a) => styleObj, "get style"),
            new JSFunction((in a) => JsJsObjectsSetStyle025Core(bridge, element, in a), "set style"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // classList — class list manipulation (Phase 3 P3.6: co-located ClassListBinding module)
        obj.FastAddValue((KeyString)"classList",
            Dom.Features.ClassListBinding.Build(element, bridge.InvalidateStyleScope),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // attributes — NamedNodeMap interface
        obj.FastAddProperty((KeyString)"attributes",
            new JSFunction((in _) => _attributes.BuildNamedNodeMap(element, obj), "get attributes"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // setAttribute(name, value)
        var bridgeForSet = this;
        obj.FastAddValue((KeyString)"setAttribute",
            new JSFunction((in a) => JsJsObjectsSetAttribute027Core(bridgeForSet, element, in a), "setAttribute", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getAttribute(name)
        obj.FastAddValue((KeyString)"getAttribute",
            new JSFunction((in a) => JsJsObjectsGetAttribute028Core(element, in a), "getAttribute", 1),
             JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"getAttributeNode",
            new JSFunction((in a) => JsJsObjectsGetAttributeNode029Core(element, obj, in a), "getAttributeNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"getAttributeNodeNS",
            new JSFunction((in a) => JsJsObjectsGetAttributeNodeNS030Core(element, obj, in a), "getAttributeNodeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- DOM tree navigation --

        // parentNode (read-only, dynamic)
        obj.FastAddProperty((KeyString)"parentNode",
            new JSFunction((in a) => element.ParentNode != null ? ToJSObject(element.ParentNode) : JSNull.Value, "get parentNode"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"isConnected",
            new JSFunction((in _) => JsJsObjectsGetIsConnected032Core(element, in _), "get isConnected"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // childNodes (read-only, dynamic)
        obj.FastAddProperty((KeyString)"childNodes",
            new JSFunction((in a) => JsJsObjectsGetChildNodes033Core(element, in a), "get childNodes"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // firstChild (read-only, dynamic)
        obj.FastAddProperty((KeyString)"firstChild",
            new JSFunction((in a) => JsJsObjectsGetFirstChild034Core(element, in a), "get firstChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastChild (read-only, dynamic)
        obj.FastAddProperty((KeyString)"lastChild",
            new JSFunction((in a) => JsJsObjectsGetLastChild035Core(element, in a), "get lastChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextSibling (read-only, dynamic)
        obj.FastAddProperty((KeyString)"nextSibling",
            new JSFunction((in a) => JsJsObjectsGetNextSibling036Core(element, in a), "get nextSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // previousSibling (read-only, dynamic)
        obj.FastAddProperty((KeyString)"previousSibling",
            new JSFunction((in a) => JsJsObjectsGetPreviousSibling037Core(element, in a), "get previousSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeType (read-only)
        obj.FastAddProperty((KeyString)"nodeType",
            new JSFunction((in a) => JsJsObjectsGetNodeType038Core(element, in a), "get nodeType"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeName (read-only)
        obj.FastAddProperty((KeyString)"nodeName",
            new JSFunction((in a) => JsJsObjectsGetNodeName039Core(element, in a), "get nodeName"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // localName (read-only) — null for non-element nodes; local part of tag name for elements
        obj.FastAddProperty((KeyString)"localName",
            new JSFunction((in a) => JsJsObjectsGetLocalName040Core(element, in a), "get localName"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // prefix (read-only) — namespace prefix or null
        obj.FastAddProperty((KeyString)"prefix",
            new JSFunction((in a) => JsJsObjectsGetPrefix041Core(element, in a), "get prefix"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // namespaceURI (read-only) — returns namespace URI for elements created via createElementNS
        obj.FastAddProperty((KeyString)"namespaceURI",
            new JSFunction((in a) => JsJsObjectsGetNamespaceURI042Core(element, in a), "get namespaceURI"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeValue (read/write) — null for elements, text content for text/comment nodes
        obj.FastAddProperty((KeyString)"nodeValue",
            new JSFunction((in a) => JsJsObjectsGetNodeValue043Core(element, in a), "get nodeValue"),
            new JSFunction((in a) => JsJsObjectsSetNodeValue044Core(bridge, element, in a), "set nodeValue"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // data (read/write) — for text nodes and comment nodes (alias for nodeValue/textContent)
        obj.FastAddProperty((KeyString)"data",
            new JSFunction((in a) => Dom.Features.CharacterDataBinding.GetData(element, in a), "get data"),
            new JSFunction((in a) => Dom.Features.CharacterDataBinding.SetData(this, element, in a), "set data"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // length (read-only) — character count for text/comment nodes, child count for elements
        obj.FastAddProperty((KeyString)"length",
            new JSFunction((in a) => Dom.Features.CharacterDataBinding.GetLength(element, in a), "get length"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // splitText(offset) — splits a text node at the given character offset
        if (IsText(element))
        {
            obj.FastAddValue((KeyString)"splitText",
                new JSFunction((in a) => Dom.Features.CharacterDataBinding.SplitText(this, element, in a), "splitText", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // substringData(offset, count) — for text/comment CharacterData nodes
        if (IsText(element) || IsComment(element))
        {
            obj.FastAddValue((KeyString)"substringData",
                new JSFunction((in a) => Dom.Features.CharacterDataBinding.SubstringData(element, in a), "substringData", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue((KeyString)"appendData",
                new JSFunction((in a) => Dom.Features.CharacterDataBinding.AppendData(this, element, in a), "appendData", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue((KeyString)"deleteData",
                new JSFunction((in a) => Dom.Features.CharacterDataBinding.DeleteData(this, element, in a), "deleteData", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue((KeyString)"insertData",
                new JSFunction((in a) => Dom.Features.CharacterDataBinding.InsertData(this, element, in a), "insertData", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue((KeyString)"replaceData",
                new JSFunction((in a) => Dom.Features.CharacterDataBinding.ReplaceData(this, element, in a), "replaceData", 3),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // ownerDocument (read-only) — returns the Document node (nodeType=9)
        obj.FastAddProperty((KeyString)"ownerDocument",
            new JSFunction((in a) => JsJsObjectsGetOwnerDocument057Core(element, in a), "get ownerDocument"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // parentElement (read-only, dynamic) — like parentNode but returns null for non-element parents
        obj.FastAddProperty((KeyString)"parentElement",
            new JSFunction((in a) => JsJsObjectsGetParentElement058Core(element, in a), "get parentElement"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // hasChildNodes()
        obj.FastAddValue((KeyString)"hasChildNodes",
            new JSFunction((in a) => element.ChildNodes.Count > 0 ? JSBoolean.True : JSBoolean.False, "hasChildNodes", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // hasAttribute(name)
        obj.FastAddValue((KeyString)"hasAttribute",
            new JSFunction((in a) => JsJsObjectsHasAttribute060Core(element, in a), "hasAttribute", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // hasAttributes()
        obj.FastAddValue((KeyString)"hasAttributes",
            new JSFunction((in _) => element.Attributes.Count > 0 ? JSBoolean.True : JSBoolean.False, "hasAttributes", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getAttributeNames()
        obj.FastAddValue((KeyString)"getAttributeNames",
            new JSFunction((in _) => new JSArray([.. AttributeNames(element).Select(static name => (JSValue)new JSString(name))]), "getAttributeNames", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeAttribute(name)
        obj.FastAddValue((KeyString)"removeAttribute",
            new JSFunction((in a) => JsJsObjectsRemoveAttribute063Core(bridgeForSet, element, in a), "removeAttribute", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // toggleAttribute(name, force)
        obj.FastAddValue((KeyString)"toggleAttribute",
            new JSFunction((in a) => JsJsObjectsToggleAttribute064Core(element, in a), "toggleAttribute", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"setAttributeNode",
            new JSFunction((in a) => JsJsObjectsSetAttributeNode065Core(element, obj, in a), "setAttributeNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"setAttributeNodeNS",
            new JSFunction((in a) => JsJsObjectsSetAttributeNodeNS066Core(element, obj, in a), "setAttributeNodeNS", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"removeAttributeNode",
            new JSFunction((in a) => JsJsObjectsRemoveAttributeNode067Core(element, obj, in a), "removeAttributeNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"removeAttributeNodeNS",
            new JSFunction((in a) => JsJsObjectsRemoveAttributeNodeNS068Core(element, obj, in a), "removeAttributeNodeNS", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setAttributeNS(namespace, qualifiedName, value)
        obj.FastAddValue((KeyString)"setAttributeNS",
            new JSFunction((in a) => JsJsObjectsSetAttributeNS069Core(bridgeForSet, element, in a), "setAttributeNS", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getAttributeNS(namespace, localName)
        obj.FastAddValue((KeyString)"getAttributeNS",
            new JSFunction((in a) => JsJsObjectsGetAttributeNS070Core(element, in a), "getAttributeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeAttributeNS(namespace, localName)
        obj.FastAddValue((KeyString)"removeAttributeNS",
            new JSFunction((in a) => JsJsObjectsRemoveAttributeNS071Core(bridgeForSet, element, in a), "removeAttributeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // hasAttributeNS(namespace, localName)
        obj.FastAddValue((KeyString)"hasAttributeNS",
            new JSFunction((in a) => JsJsObjectsHasAttributeNS072Core(element, in a), "hasAttributeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // contains(otherNode) — returns true if otherNode is a descendant
        obj.FastAddValue((KeyString)"contains",
            new JSFunction((in a) => JsJsObjectsContains073Core(element, in a), "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // compareDocumentPosition(otherNode)
        obj.FastAddValue((KeyString)"compareDocumentPosition",
            new JSFunction((in a) => JsJsObjectsCompareDocumentPosition074Core(element, in a), "compareDocumentPosition", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // isSameNode(otherNode)
        obj.FastAddValue((KeyString)"isSameNode",
            new JSFunction((in a) => JsJsObjectsIsSameNode075Core(element, in a), "isSameNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // normalize()
        obj.FastAddValue((KeyString)"normalize",
            new JSFunction((in _) => JsJsObjectsNormalize076Core(element, in _), "normalize", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // isEqualNode(otherNode)
        obj.FastAddValue((KeyString)"isEqualNode",
            new JSFunction((in a) => JsJsObjectsIsEqualNode077Core(element, in a), "isEqualNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"getRootNode",
            new JSFunction((in a) => JsJsObjectsGetRootNode078Core(element, in a), "getRootNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // cloneNode(deep)
        obj.FastAddValue((KeyString)"cloneNode",
            new JSFunction((in a) => JsJsObjectsCloneNode079Core(element, in a), "cloneNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // insertBefore(newChild, refChild)
        var bridgeForInsert = this;
        obj.FastAddValue((KeyString)"insertBefore",
            new JSFunction((in a) => JsJsObjectsInsertBefore080Core(bridgeForInsert, element, in a), "insertBefore", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // children (read-only) — element children only (no text nodes)
        obj.FastAddProperty((KeyString)"children",
            new JSFunction((in a) => JsJsObjectsGetChildren081Core(element, in a), "get children"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // childElementCount (read-only)
        obj.FastAddProperty((KeyString)"childElementCount",
            new JSFunction((in a) => new JSNumber(ChildElements(element).Count(c => !IsText(c))), "get childElementCount"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // firstElementChild (read-only)
        obj.FastAddProperty((KeyString)"firstElementChild",
            new JSFunction((in a) => JsJsObjectsGetFirstElementChild083Core(element, in a), "get firstElementChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastElementChild (read-only)
        obj.FastAddProperty((KeyString)"lastElementChild",
            new JSFunction((in a) => JsJsObjectsGetLastElementChild084Core(element, in a), "get lastElementChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextElementSibling (read-only)
        obj.FastAddProperty((KeyString)"nextElementSibling",
            new JSFunction((in a) => JsJsObjectsGetNextElementSibling085Core(element, in a), "get nextElementSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // previousElementSibling (read-only)
        obj.FastAddProperty((KeyString)"previousElementSibling",
            new JSFunction((in a) => JsJsObjectsGetPreviousElementSibling086Core(element, in a), "get previousElementSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // -- DOM manipulation methods --

        obj.FastAddValue((KeyString)"attachShadow",
            new JSFunction((in a) => JsJsObjectsAttachShadow087Core(element, in a), "attachShadow", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // appendChild(child)
        var bridgeForAppend = this;
        obj.FastAddValue((KeyString)"appendChild",
            new JSFunction((in a) => JsJsObjectsAppendChild088Core(bridgeForAppend, element, in a), "appendChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"append",
            new JSFunction((in a) => JsJsObjectsAppend089Core(element, in a), "append", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"prepend",
            new JSFunction((in a) => JsJsObjectsPrepend090Core(element, in a), "prepend", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeChild(child)
        obj.FastAddValue((KeyString)"removeChild",
            new JSFunction((in a) => JsJsObjectsRemoveChild091Core(bridgeForAppend, element, in a), "removeChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // replaceChild(newChild, oldChild)
        obj.FastAddValue((KeyString)"replaceChild",
            new JSFunction((in a) => JsJsObjectsReplaceChild092Core(bridgeForAppend, element, in a), "replaceChild", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // remove() — ChildNode.remove() per DOM Living Standard
        obj.FastAddValue((KeyString)"remove",
            new JSFunction((in a) => JsJsObjectsRemove093Core(element, in a), "remove", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"before",
            new JSFunction((in a) => JsJsObjectsBefore094Core(element, in a), "before", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"after",
            new JSFunction((in a) => JsJsObjectsAfter095Core(element, in a), "after", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"replaceWith",
            new JSFunction((in a) => JsJsObjectsReplaceWith096Core(element, in a), "replaceWith", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- DOM events --

        // addEventListener(type, listener, useCapture)
        obj.FastAddValue((KeyString)"addEventListener",
            new JSFunction((in a) => JsJsObjectsAddEventListener097Core(element, in a), "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeEventListener(type, listener, useCapture)
        obj.FastAddValue((KeyString)"removeEventListener",
            new JSFunction((in a) => JsJsObjectsRemoveEventListener098Core(element, in a), "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // dispatchEvent(event) — DOM Events Level 3 with capture/target/bubble phases
        obj.FastAddValue((KeyString)"dispatchEvent",
            new JSFunction((in a) => JsJsObjectsDispatchEvent099Core(bridge, element, in a), "dispatchEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // element.click() — creates and dispatches a MouseEvent
        // For checkboxes and radio buttons, toggles checked state.
        obj.FastAddValue((KeyString)"click",
            new JSFunction((in _) => JsJsObjectsClick101Core(bridge, element, in _), "click", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // element.focus() — creates and dispatches a FocusEvent-like object
        obj.FastAddValue((KeyString)"focus",
            new JSFunction((in _) => JsJsObjectsFocus102Core(bridge, element, in _), "focus", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // element.blur() — creates and dispatches a FocusEvent-like object
        obj.FastAddValue((KeyString)"blur",
            new JSFunction((in _) => JsJsObjectsBlur103Core(bridge, element, in _), "blur", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // on* inline event handler properties (onclick, onload, etc.)
        foreach (var eventName in InlineEventNames)
        {
            obj.FastAddProperty((KeyString)$"on{eventName}",
                new JSFunction((in _) => JsJsObjectsCallback104Core(element, eventName, in _), $"get on{eventName}"),
                new JSFunction((in a) => JsJsObjectsCallback105Core(element, eventName, in a), $"set on{eventName}"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // Compile on* HTML attributes into inline event handler functions
        CompileInlineEventAttributes(element);

        // -- Form element support --

        // value (read/write) — for input, textarea, select elements
        // The IDL 'value' property is NOT reflected as a content attribute for inputs.
        obj.FastAddProperty((KeyString)"value",
            new JSFunction((in a) => JsJsObjectsGetValue106Core(element, in a), "get value"),
            new JSFunction((in a) => JsJsObjectsSetValue107Core(element, in a), "set value"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // checked (read/write) — for checkbox and radio inputs
        // Uses the typed checked-state slot as the "dirty" IDL state that tracks
        // programmatic changes. setAttribute("checked") only sets the content
        // attribute and does NOT affect this IDL state.
        obj.FastAddProperty((KeyString)"checked",
            new JSFunction((in a) => JsJsObjectsGetChecked108Core(element, in a), "get checked"),
            new JSFunction((in a) => JsJsObjectsSetChecked109Core(element, in a), "set checked"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // type (read/write) — for input/button elements; getter returns lowercase
        obj.FastAddProperty((KeyString)"type",
            new JSFunction((in a) => JsJsObjectsGetType110Core(element, in a), "get type"),
            new JSFunction((in a) => JsJsObjectsSetType111Core(element, in a), "set type"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // name (read/write) — for form elements; syncs with content attribute
        obj.FastAddProperty((KeyString)"name",
            new JSFunction((in a) => JsJsObjectsGetName112Core(element, in a), "get name"),
            new JSFunction((in a) => JsJsObjectsSetName113Core(element, in a), "set name"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // disabled (read/write) — for form controls
        obj.FastAddProperty((KeyString)"disabled",
            new JSFunction((in a) => HasAttr(element, "disabled") ? JSBoolean.True : JSBoolean.False, "get disabled"),
            new JSFunction((in a) => JsJsObjectsSetDisabled115Core(bridge, element, in a), "set disabled"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // hidden (read/write) — global reflected boolean attribute
        obj.FastAddProperty((KeyString)"hidden",
            new JSFunction((in a) => HasAttr(element, "hidden") ? JSBoolean.True : JSBoolean.False, "get hidden"),
            new JSFunction((in a) => JsJsObjectsSetHidden117Core(bridge, element, in a), "set hidden"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // tabIndex (read/write) — global reflected numeric attribute
        obj.FastAddProperty((KeyString)"tabIndex",
            new JSFunction((in _) => JsJsObjectsGetTabIndex118Core(element, in _), "get tabIndex"),
            new JSFunction((in a) => JsJsObjectsSetTabIndex119Core(element, in a), "set tabIndex"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // required (read/write) — form validation
        obj.FastAddProperty((KeyString)"required",
            new JSFunction((in a) => HasAttr(element, "required") ? JSBoolean.True : JSBoolean.False, "get required"),
            new JSFunction((in a) => JsJsObjectsSetRequired121Core(bridge, element, in a), "set required"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // checkValidity() — form validation (Phase 3 P3.9: FormBinding owns the validity check)
        obj.FastAddValue((KeyString)"checkValidity",
            new JSFunction((in a) => _forms.IsElementValid(element) ? JSBoolean.True : JSBoolean.False, "checkValidity", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // reportValidity() — form validation
        obj.FastAddValue((KeyString)"reportValidity",
            new JSFunction((in a) => _forms.IsElementValid(element) ? JSBoolean.True : JSBoolean.False, "reportValidity", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // submit() — for form elements
        obj.FastAddValue((KeyString)"submit",
            new JSFunction((in a) => JsJsObjectsSubmit125Core(element, obj, in a), "submit", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // querySelector on elements
        obj.FastAddValue((KeyString)"querySelector",
            new JSFunction((in a) => JsJsObjectsQuerySelector126Core(bridge, element, in a), "querySelector", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // querySelectorAll on elements
        obj.FastAddValue((KeyString)"querySelectorAll",
            new JSFunction((in a) => JsJsObjectsQuerySelectorAll127Core(bridge, element, in a), "querySelectorAll", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"matches",
            new JSFunction((in a) => JsJsObjectsMatches128Core(element, in a), "matches", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"closest",
            new JSFunction((in a) => JsJsObjectsClosest129Core(bridge, element, in a), "closest", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"insertAdjacentElement",
            new JSFunction((in a) => JsJsObjectsInsertAdjacentElement130Core(element, in a), "insertAdjacentElement", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"insertAdjacentText",
            new JSFunction((in a) => JsJsObjectsInsertAdjacentText131Core(element, in a), "insertAdjacentText", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"insertAdjacentHTML",
            new JSFunction((in a) => JsJsObjectsInsertAdjacentHTML132Core(element, in a), "insertAdjacentHTML", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getElementsByTagName on elements — searches descendants in tree order
        obj.FastAddValue((KeyString)"getElementsByTagName",
            new JSFunction((in a) => JsJsObjectsGetElementsByTagName133Core(bridge, element, in a), "getElementsByTagName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getContext(contextType) — for <canvas> elements
        obj.FastAddValue((KeyString)"getContext",
            new JSFunction((in a) => JsJsObjectsGetContext134Core(element, in a), "getContext", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // contentWindow / contentDocument — for <iframe> elements with full sub-document DOM
        if (string.Equals(element.TagName, "iframe", StringComparison.OrdinalIgnoreCase))
        {
            obj.FastAddProperty((KeyString)"contentDocument",
                new JSFunction((in _) => JsJsObjectsGetContentDocument135Core(element, in _), "get contentDocument"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty((KeyString)"contentWindow",
                new JSFunction((in _) => JsJsObjectsGetContentWindow136Core(element, in _), "get contentWindow"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);

            // getSVGDocument() — returns contentDocument (same as contentDocument for same-origin)
            obj.FastAddValue((KeyString)"getSVGDocument",
                new JSFunction((in _) => JsJsObjectsGetSVGDocument137Core(element, in _), "getSVGDocument", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // src property (read/write) — for iframe elements
            var bridgeForSrc = this;
            obj.FastAddProperty((KeyString)"src",
                new JSFunction((in _) => TryGetAttribute(element, "src", out var s) ? new JSString(s) : new JSString(string.Empty), "get src"),
                new JSFunction((in a) => JsJsObjectsSetSrc139Core(bridgeForSrc, element, in a), "set src"),
                 JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty((KeyString)"srcdoc",
                new JSFunction((in _) => TryGetAttribute(element, "srcdoc", out var s) ? new JSString(s) : new JSString(string.Empty), "get srcdoc"),
                new JSFunction((in a) => JsJsObjectsSetSrcdoc141Core(bridgeForSrc, element, in a), "set srcdoc"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // sandbox attribute access
            obj.FastAddProperty((KeyString)"sandbox",
                new JSFunction((in _) => TryGetAttribute(element, "sandbox", out var sandbox) ? new JSString(sandbox) : new JSString(string.Empty), "get sandbox"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        AddElementSpecificMembers(obj, element);

        // Node type constants (DOM spec: these exist on all Node objects)
        obj.FastAddValue((KeyString)"ELEMENT_NODE", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"ATTRIBUTE_NODE", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"TEXT_NODE", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"CDATA_SECTION_NODE", new JSNumber(4), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"COMMENT_NODE", new JSNumber(8), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"DOCUMENT_NODE", new JSNumber(9), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"DOCUMENT_TYPE_NODE", new JSNumber(10), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"DOCUMENT_FRAGMENT_NODE", new JSNumber(11), JSPropertyAttributes.EnumerableConfigurableValue);

        return obj;
    }

    /// <summary>
    /// RF-BRIDGE-1c Phase F (F3c): builds the minimal Node/CharacterData JS wrapper for a canonical
    /// <c>DomText</c>/<c>DomComment</c> — the members a character-data node actually exposes (no
    /// tagName/style/attributes/querySelector/form/iframe surface). Populated onto the already-cached
    /// <paramref name="obj"/> (the caller registers it in the <c>JsObjectRegistry</c> before calling, so
    /// re-entrant <c>ToJSObject</c> lookups resolve). The node-level <c>*Core</c> helpers are the
    /// same ones the element wrapper uses, now widened to <see cref="DomNode"/>.
    /// Includes the ChildNode mixin (remove/before/after/replaceWith) and EventTarget (added once the
    /// tree-mutation helpers were widened in F3c part 2b). This wrapper is dead code until the F3c
    /// construction flip; it does not yet expose <c>surroundContents</c>-style range members that only
    /// apply to elements.
    /// </summary>
    private void PopulateCharacterDataJSObject(JSObject obj, DomNode node)
    {
        var bridge = this;

        // -- Node identity --
        obj.FastAddProperty((KeyString)"nodeType",
            new JSFunction((in a) => JsJsObjectsGetNodeType038Core(node, in a), "get nodeType"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"nodeName",
            new JSFunction((in a) => JsJsObjectsGetNodeName039Core(node, in a), "get nodeName"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"localName",
            new JSFunction((in a) => JsJsObjectsGetLocalName040Core(node, in a), "get localName"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"prefix",
            new JSFunction((in a) => JsJsObjectsGetPrefix041Core(node, in a), "get prefix"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"namespaceURI",
            new JSFunction((in a) => JsJsObjectsGetNamespaceURI042Core(node, in a), "get namespaceURI"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // -- Character data --
        obj.FastAddProperty((KeyString)"nodeValue",
            new JSFunction((in a) => JsJsObjectsGetNodeValue043Core(node, in a), "get nodeValue"),
            new JSFunction((in a) => JsJsObjectsSetNodeValue044Core(bridge, node, in a), "set nodeValue"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"textContent",
            new JSFunction((in _) => GetNodeTextValue(node), "get textContent"),
            new JSFunction((in a) => JsJsObjectsSetNodeValue044Core(bridge, node, in a), "set textContent"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"data",
            new JSFunction((in a) => Dom.Features.CharacterDataBinding.GetData(node, in a), "get data"),
            new JSFunction((in a) => Dom.Features.CharacterDataBinding.SetData(this, node, in a), "set data"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"length",
            new JSFunction((in a) => Dom.Features.CharacterDataBinding.GetLength(node, in a), "get length"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // splitText is Text-only (not on Comment).
        if (IsText(node))
        {
            obj.FastAddValue((KeyString)"splitText",
                new JSFunction((in a) => Dom.Features.CharacterDataBinding.SplitText(this, node, in a), "splitText", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        obj.FastAddValue((KeyString)"substringData",
            new JSFunction((in a) => Dom.Features.CharacterDataBinding.SubstringData(node, in a), "substringData", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"appendData",
            new JSFunction((in a) => Dom.Features.CharacterDataBinding.AppendData(this, node, in a), "appendData", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"deleteData",
            new JSFunction((in a) => Dom.Features.CharacterDataBinding.DeleteData(this, node, in a), "deleteData", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"insertData",
            new JSFunction((in a) => Dom.Features.CharacterDataBinding.InsertData(this, node, in a), "insertData", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"replaceData",
            new JSFunction((in a) => Dom.Features.CharacterDataBinding.ReplaceData(this, node, in a), "replaceData", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- Tree navigation --
        obj.FastAddProperty((KeyString)"parentNode",
            new JSFunction((in a) => node.ParentNode != null ? ToJSObject(node.ParentNode) : JSNull.Value, "get parentNode"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"parentElement",
            new JSFunction((in a) => JsJsObjectsGetParentElement058Core(node, in a), "get parentElement"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"isConnected",
            new JSFunction((in a) => JsJsObjectsGetIsConnected032Core(node, in a), "get isConnected"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"childNodes",
            new JSFunction((in a) => JsJsObjectsGetChildNodes033Core(node, in a), "get childNodes"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"firstChild",
            new JSFunction((in a) => JsJsObjectsGetFirstChild034Core(node, in a), "get firstChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"lastChild",
            new JSFunction((in a) => JsJsObjectsGetLastChild035Core(node, in a), "get lastChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"nextSibling",
            new JSFunction((in a) => JsJsObjectsGetNextSibling036Core(node, in a), "get nextSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"previousSibling",
            new JSFunction((in a) => JsJsObjectsGetPreviousSibling037Core(node, in a), "get previousSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"ownerDocument",
            new JSFunction((in a) => JsJsObjectsGetOwnerDocument057Core(node, in a), "get ownerDocument"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddValue((KeyString)"hasChildNodes",
            new JSFunction((in a) => node.ChildNodes.Count > 0 ? JSBoolean.True : JSBoolean.False, "hasChildNodes", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- Node methods --
        obj.FastAddValue((KeyString)"cloneNode",
            new JSFunction((in a) => JsJsObjectsCloneNode079Core(node, in a), "cloneNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"contains",
            new JSFunction((in a) => JsJsObjectsContains073Core(node, in a), "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"compareDocumentPosition",
            new JSFunction((in a) => JsJsObjectsCompareDocumentPosition074Core(node, in a), "compareDocumentPosition", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"isSameNode",
            new JSFunction((in a) => JsJsObjectsIsSameNode075Core(node, in a), "isSameNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"isEqualNode",
            new JSFunction((in a) => JsJsObjectsIsEqualNode077Core(node, in a), "isEqualNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"getRootNode",
            new JSFunction((in a) => JsJsObjectsGetRootNode078Core(node, in a), "getRootNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"normalize",
            new JSFunction((in a) => JsJsObjectsNormalize076Core(node, in a), "normalize", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- ChildNode mixin --
        obj.FastAddValue((KeyString)"remove",
            new JSFunction((in a) => JsJsObjectsRemove093Core(node, in a), "remove", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"before",
            new JSFunction((in a) => JsJsObjectsBefore094Core(node, in a), "before", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"after",
            new JSFunction((in a) => JsJsObjectsAfter095Core(node, in a), "after", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"replaceWith",
            new JSFunction((in a) => JsJsObjectsReplaceWith096Core(node, in a), "replaceWith", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- EventTarget --
        obj.FastAddValue((KeyString)"addEventListener",
            new JSFunction((in a) => JsJsObjectsAddEventListener097Core(node, in a), "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"removeEventListener",
            new JSFunction((in a) => JsJsObjectsRemoveEventListener098Core(node, in a), "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"dispatchEvent",
            new JSFunction((in a) => JsJsObjectsDispatchEvent099Core(bridge, node, in a), "dispatchEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Node type constants (exist on all Node objects).
        obj.FastAddValue((KeyString)"ELEMENT_NODE", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"ATTRIBUTE_NODE", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"TEXT_NODE", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"CDATA_SECTION_NODE", new JSNumber(4), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"COMMENT_NODE", new JSNumber(8), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"DOCUMENT_NODE", new JSNumber(9), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"DOCUMENT_TYPE_NODE", new JSNumber(10), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"DOCUMENT_FRAGMENT_NODE", new JSNumber(11), JSPropertyAttributes.EnumerableConfigurableValue);
    }

    /// <summary>
    /// Builds the JS wrapper for a canonical <see cref="DomDocumentType"/> node (Phase 4 item 1).
    /// A DocumentType is a leaf Node with the ChildNode mixin and DocumentType-specific
    /// <c>name</c>/<c>publicId</c>/<c>systemId</c>/<c>internalSubset</c> — it deliberately does NOT get
    /// the element surface (attributes/style/children) it inherited while it was a <c>#doctype</c>
    /// sentinel element, nor the CharacterData mutation methods. The node-generic handlers are the
    /// same ones the character-data wrapper uses.
    /// </summary>
    private void PopulateDocumentTypeJSObject(JSObject obj, DomDocumentType doctype)
    {
        var bridge = this;
        DomNode node = doctype;

        // -- Node identity --
        obj.FastAddProperty((KeyString)"nodeType",
            new JSFunction((in a) => JsJsObjectsGetNodeType038Core(node, in a), "get nodeType"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"nodeName",
            new JSFunction((in a) => JsJsObjectsGetNodeName039Core(node, in a), "get nodeName"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"nodeValue",
            new JSFunction((in _) => JSNull.Value, "get nodeValue"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"textContent",
            new JSFunction((in _) => JSNull.Value, "get textContent"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // -- DocumentType interface --
        obj.FastAddProperty((KeyString)"name",
            new JSFunction((in _) => new JSString(doctype.Name), "get name"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"publicId",
            new JSFunction((in _) => JsJsObjectsGetPublicId055Core(node, in _), "get publicId"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"systemId",
            new JSFunction((in _) => JsJsObjectsGetSystemId056Core(node, in _), "get systemId"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"internalSubset",
            NullFunction("get internalSubset"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // -- Tree navigation --
        obj.FastAddProperty((KeyString)"parentNode",
            new JSFunction((in a) => node.ParentNode != null ? ToJSObject(node.ParentNode) : JSNull.Value, "get parentNode"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"parentElement",
            new JSFunction((in a) => JsJsObjectsGetParentElement058Core(node, in a), "get parentElement"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"previousSibling",
            new JSFunction((in a) => JsJsObjectsGetPreviousSibling037Core(node, in a), "get previousSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"nextSibling",
            new JSFunction((in a) => JsJsObjectsGetNextSibling036Core(node, in a), "get nextSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"ownerDocument",
            new JSFunction((in a) => JsJsObjectsGetOwnerDocument057Core(node, in a), "get ownerDocument"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"isConnected",
            new JSFunction((in a) => JsJsObjectsGetIsConnected032Core(node, in a), "get isConnected"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddValue((KeyString)"hasChildNodes",
            new JSFunction((in a) => JSBoolean.False, "hasChildNodes", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- Node methods --
        obj.FastAddValue((KeyString)"cloneNode",
            new JSFunction((in a) => JsJsObjectsCloneNode079Core(node, in a), "cloneNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"isEqualNode",
            new JSFunction((in a) => JsJsObjectsIsEqualNode077Core(node, in a), "isEqualNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"isSameNode",
            new JSFunction((in a) => JsJsObjectsIsSameNode075Core(node, in a), "isSameNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"contains",
            new JSFunction((in a) => JsJsObjectsContains073Core(node, in a), "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"compareDocumentPosition",
            new JSFunction((in a) => JsJsObjectsCompareDocumentPosition074Core(node, in a), "compareDocumentPosition", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"getRootNode",
            new JSFunction((in a) => JsJsObjectsGetRootNode078Core(node, in a), "getRootNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- ChildNode mixin --
        obj.FastAddValue((KeyString)"remove",
            new JSFunction((in a) => JsJsObjectsRemove093Core(node, in a), "remove", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"before",
            new JSFunction((in a) => JsJsObjectsBefore094Core(node, in a), "before", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"after",
            new JSFunction((in a) => JsJsObjectsAfter095Core(node, in a), "after", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"replaceWith",
            new JSFunction((in a) => JsJsObjectsReplaceWith096Core(node, in a), "replaceWith", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- EventTarget --
        obj.FastAddValue((KeyString)"addEventListener",
            new JSFunction((in a) => JsJsObjectsAddEventListener097Core(node, in a), "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"removeEventListener",
            new JSFunction((in a) => JsJsObjectsRemoveEventListener098Core(node, in a), "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"dispatchEvent",
            new JSFunction((in a) => JsJsObjectsDispatchEvent099Core(bridge, node, in a), "dispatchEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Node type constants (exist on all Node objects).
        obj.FastAddValue((KeyString)"ELEMENT_NODE", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"ATTRIBUTE_NODE", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"TEXT_NODE", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"CDATA_SECTION_NODE", new JSNumber(4), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"COMMENT_NODE", new JSNumber(8), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"DOCUMENT_NODE", new JSNumber(9), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"DOCUMENT_TYPE_NODE", new JSNumber(10), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"DOCUMENT_FRAGMENT_NODE", new JSNumber(11), JSPropertyAttributes.EnumerableConfigurableValue);
    }

    /// <summary>
    /// Builds the JS wrapper for a canonical <see cref="DomDocumentFragment"/> (Phase 4 item 1). A
    /// fragment is a non-element container: it gets the Node base + ParentNode mixin + child-
    /// manipulation surface, but NOT the element-only surface (attributes/style/tagName) it inherited
    /// while it was a <c>#document-fragment</c> sentinel element. Node-generic members reuse the same
    /// handlers the character-data wrapper uses; the container members are focused fragment lambdas
    /// over the neutral tree helpers and the (DomNode-widened) <see cref="InsertNodeAt"/> — a fragment
    /// parent has no style scope, sub-document onload or child-mutation-observer side effects.
    /// </summary>
    private void PopulateDocumentFragmentJSObject(JSObject obj, DomDocumentFragment fragment)
    {
        var bridge = this;
        DomNode node = fragment;

        // -- Node identity --
        obj.FastAddProperty((KeyString)"nodeType",
            new JSFunction((in a) => JsJsObjectsGetNodeType038Core(node, in a), "get nodeType"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"nodeName",
            new JSFunction((in a) => JsJsObjectsGetNodeName039Core(node, in a), "get nodeName"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"nodeValue",
            new JSFunction((in _) => JSNull.Value, "get nodeValue"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"ownerDocument",
            new JSFunction((in a) => JsJsObjectsGetOwnerDocument057Core(node, in a), "get ownerDocument"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // -- Tree navigation --
        obj.FastAddProperty((KeyString)"parentNode",
            new JSFunction((in _) => node.ParentNode != null ? ToJSObject(node.ParentNode) : JSNull.Value, "get parentNode"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"parentElement",
            new JSFunction((in a) => JsJsObjectsGetParentElement058Core(node, in a), "get parentElement"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"childNodes",
            new JSFunction((in a) => JsJsObjectsGetChildNodes033Core(node, in a), "get childNodes"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"firstChild",
            new JSFunction((in a) => JsJsObjectsGetFirstChild034Core(node, in a), "get firstChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"lastChild",
            new JSFunction((in a) => JsJsObjectsGetLastChild035Core(node, in a), "get lastChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"isConnected",
            new JSFunction((in a) => JsJsObjectsGetIsConnected032Core(node, in a), "get isConnected"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddValue((KeyString)"hasChildNodes",
            new JSFunction((in a) => fragment.ChildNodes.Count > 0 ? JSBoolean.True : JSBoolean.False, "hasChildNodes", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- ParentNode mixin (element views) --
        obj.FastAddProperty((KeyString)"children",
            new JSFunction((in _) => new JSArray([.. ChildElements(fragment).Where(c => !IsText(c)).Select(c => (JSValue)ToJSObject(c))]), "get children"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"childElementCount",
            new JSFunction((in _) => new JSNumber(ChildElements(fragment).Count(c => !IsText(c))), "get childElementCount"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"firstElementChild",
            new JSFunction((in _) =>
            {
                var first = ChildElements(fragment).FirstOrDefault(c => !IsText(c));
                return first != null ? ToJSObject(first) : JSNull.Value;
            }, "get firstElementChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"lastElementChild",
            new JSFunction((in _) =>
            {
                var last = ChildElements(fragment).LastOrDefault(c => !IsText(c));
                return last != null ? ToJSObject(last) : JSNull.Value;
            }, "get lastElementChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // -- textContent (get/set) --
        obj.FastAddProperty((KeyString)"textContent",
            new JSFunction((in _) => GetNodeTextValue(node), "get textContent"),
            new JSFunction((in a) =>
            {
                ClearChildren(fragment);
                var value = a.Length > 0 ? a[0].ToString() : string.Empty;
                if (!string.IsNullOrEmpty(value))
                    fragment.AppendChild(CreateBridgeTextNode(value));
                return JSUndefined.Value;
            }, "set textContent"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // -- Child manipulation --
        obj.FastAddValue((KeyString)"appendChild",
            new JSFunction((in a) =>
            {
                if (a.Length == 0 || a[0] is not JSObject childObj)
                    return JSUndefined.Value;
                var childEl = FindDomNodeByJSObject(childObj);
                if (childEl == null)
                    return a[0];
                if (ReferenceEquals(childEl, fragment) || fragment.IsDescendantOf(childEl))
                    ThrowDOMException(_jsContext!, "The new child element contains the parent.", "HierarchyRequestError");
                InsertNodeAt(fragment, childEl, fragment.ChildNodes.Count);
                return a[0];
            }, "appendChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"insertBefore",
            new JSFunction((in a) =>
            {
                if (a.Length == 0 || a[0] is not JSObject newChildObj)
                    return JSUndefined.Value;
                var newEl = FindDomNodeByJSObject(newChildObj);
                if (newEl == null)
                    return a[0];
                if (ReferenceEquals(newEl, fragment) || fragment.IsDescendantOf(newEl))
                    ThrowDOMException(_jsContext!, "The new child element contains the parent.", "HierarchyRequestError");
                if (a.Length < 2 || a[1].IsNull || a[1].IsUndefined)
                {
                    InsertNodeAt(fragment, newEl, fragment.ChildNodes.Count);
                    return a[0];
                }
                if (a[1] is not JSObject refChildObj)
                    return a[0];
                var refEl = FindDomNodeByJSObject(refChildObj);
                if (refEl == null || ReferenceEquals(newEl, refEl))
                    return a[0];
                var idx = ChildIndexOf(fragment, refEl);
                if (idx < 0)
                    throw new JSException("NotFoundError: The node before which the new node is to be inserted is not a child of this node.");
                InsertNodeAt(fragment, newEl, idx);
                return a[0];
            }, "insertBefore", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"removeChild",
            new JSFunction((in a) =>
            {
                if (a.Length == 0 || a[0] is not JSObject childObj)
                    return JSUndefined.Value;
                var childEl = FindDomNodeByJSObject(childObj);
                if (childEl == null)
                    return a[0];
                var idx = ChildIndexOf(fragment, childEl);
                if (idx < 0)
                    return a[0];
                NotifyNodeIteratorPreRemoval(childEl);
                RemoveNthChild(fragment, idx);
                SetParent(childEl, null);
                return a[0];
            }, "removeChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"replaceChild",
            new JSFunction((in a) =>
            {
                if (a.Length < 2 || a[0] is not JSObject newObj || a[1] is not JSObject oldObj)
                    return JSUndefined.Value;
                var newEl = FindDomNodeByJSObject(newObj);
                var oldEl = FindDomNodeByJSObject(oldObj);
                if (newEl == null || oldEl == null)
                    return a[1];
                var idx = ChildIndexOf(fragment, oldEl);
                if (idx < 0)
                    return a[1];
                SetParent(oldEl, null);
                InsertNodeAt(fragment, newEl, Math.Min(idx, fragment.ChildNodes.Count));
                return a[1];
            }, "replaceChild", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"append",
            new JSFunction((in a) =>
            {
                if (a.Length == 0)
                    return JSUndefined.Value;
                var nodes = BuildChildNodeArgumentNodes(a);
                var insertIndex = fragment.ChildNodes.Count;
                foreach (var child in nodes)
                    InsertNodeAt(fragment, child, insertIndex++);
                return JSUndefined.Value;
            }, "append", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"prepend",
            new JSFunction((in a) =>
            {
                if (a.Length == 0)
                    return JSUndefined.Value;
                var nodes = BuildChildNodeArgumentNodes(a);
                var insertIndex = 0;
                foreach (var child in nodes)
                    InsertNodeAt(fragment, child, insertIndex++);
                return JSUndefined.Value;
            }, "prepend", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- Query --
        obj.FastAddValue((KeyString)"querySelector",
            new JSFunction((in a) => FindInDescendants(fragment, a.Length > 0 ? a[0].ToString() : string.Empty, false, bridge), "querySelector", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"querySelectorAll",
            new JSFunction((in a) => FindInDescendants(fragment, a.Length > 0 ? a[0].ToString() : string.Empty, true, bridge), "querySelectorAll", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- Node methods --
        obj.FastAddValue((KeyString)"cloneNode",
            new JSFunction((in a) => JsJsObjectsCloneNode079Core(node, in a), "cloneNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"isEqualNode",
            new JSFunction((in a) => JsJsObjectsIsEqualNode077Core(node, in a), "isEqualNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"isSameNode",
            new JSFunction((in a) => JsJsObjectsIsSameNode075Core(node, in a), "isSameNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"contains",
            new JSFunction((in a) => JsJsObjectsContains073Core(node, in a), "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"compareDocumentPosition",
            new JSFunction((in a) => JsJsObjectsCompareDocumentPosition074Core(node, in a), "compareDocumentPosition", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"getRootNode",
            new JSFunction((in a) => JsJsObjectsGetRootNode078Core(node, in a), "getRootNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"normalize",
            new JSFunction((in a) => JsJsObjectsNormalize076Core(node, in a), "normalize", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- EventTarget --
        obj.FastAddValue((KeyString)"addEventListener",
            new JSFunction((in a) => JsJsObjectsAddEventListener097Core(node, in a), "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"removeEventListener",
            new JSFunction((in a) => JsJsObjectsRemoveEventListener098Core(node, in a), "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"dispatchEvent",
            new JSFunction((in a) => JsJsObjectsDispatchEvent099Core(bridge, node, in a), "dispatchEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Node type constants (exist on all Node objects).
        obj.FastAddValue((KeyString)"ELEMENT_NODE", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"ATTRIBUTE_NODE", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"TEXT_NODE", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"CDATA_SECTION_NODE", new JSNumber(4), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"COMMENT_NODE", new JSNumber(8), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"DOCUMENT_NODE", new JSNumber(9), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"DOCUMENT_TYPE_NODE", new JSNumber(10), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"DOCUMENT_FRAGMENT_NODE", new JSNumber(11), JSPropertyAttributes.EnumerableConfigurableValue);
    }

}
