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

        // HTMLElement global content-attribute reflectors (id, className, title, lang, accessKey, dir,
        // draggable) — Phase 3 P3.54: extracted into the co-located GlobalAttributeBinding feature module.
        // The selector-affecting three (id/className/dir) invalidate the style scope on write through the
        // one-member IGlobalAttributeHost contract (DomBridge.GlobalAttributeHost.cs).
        Dom.Features.GlobalAttributeBinding.Install(this, obj, element);

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
        var styleObj = Dom.Features.StyleDeclarationBinding.BuildInlineDeclaration(bridge, element, () =>
        {
            bridge.SyncStyleAttributeFromInlineStyle(element);
            bridge.InvalidateStyleScope(element);
        }, onPositionAreaInvalidate: bridge.ClearPositionAreaResolution);
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
            new JSFunction((in a) => _attributes.SetAttribute(element, in a), "setAttribute", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getAttribute(name)
        obj.FastAddValue((KeyString)"getAttribute",
            new JSFunction((in a) => _attributes.GetAttribute(element, in a), "getAttribute", 1),
             JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"getAttributeNode",
            new JSFunction((in a) => _attributes.GetAttributeNode(element, obj, in a), "getAttributeNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"getAttributeNodeNS",
            new JSFunction((in a) => _attributes.GetAttributeNodeNS(element, obj, in a), "getAttributeNodeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- DOM tree navigation --

        // parentNode (read-only, dynamic)
        obj.FastAddProperty((KeyString)"parentNode",
            new JSFunction((in a) => element.ParentNode != null ? ToJSObject(element.ParentNode) : JSNull.Value, "get parentNode"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"isConnected",
            new JSFunction((in _) => Dom.Features.NodeAccessorsBinding.GetIsConnected(this, element, in _), "get isConnected"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // childNodes (read-only, dynamic)
        obj.FastAddProperty((KeyString)"childNodes",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetChildNodes(this, element, in a), "get childNodes"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // firstChild (read-only, dynamic)
        obj.FastAddProperty((KeyString)"firstChild",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetFirstChild(this, element, in a), "get firstChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastChild (read-only, dynamic)
        obj.FastAddProperty((KeyString)"lastChild",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetLastChild(this, element, in a), "get lastChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextSibling (read-only, dynamic)
        obj.FastAddProperty((KeyString)"nextSibling",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNextSibling(this, element, in a), "get nextSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // previousSibling (read-only, dynamic)
        obj.FastAddProperty((KeyString)"previousSibling",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetPreviousSibling(this, element, in a), "get previousSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeType (read-only)
        obj.FastAddProperty((KeyString)"nodeType",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNodeType(element, in a), "get nodeType"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeName (read-only)
        obj.FastAddProperty((KeyString)"nodeName",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNodeName(element, in a), "get nodeName"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // localName (read-only) — null for non-element nodes; local part of tag name for elements
        obj.FastAddProperty((KeyString)"localName",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetLocalName(element, in a), "get localName"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // prefix (read-only) — namespace prefix or null
        obj.FastAddProperty((KeyString)"prefix",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetPrefix(element, in a), "get prefix"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // namespaceURI (read-only) — returns namespace URI for elements created via createElementNS
        obj.FastAddProperty((KeyString)"namespaceURI",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNamespaceURI(element, in a), "get namespaceURI"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeValue (read/write) — null for elements, text content for text/comment nodes
        obj.FastAddProperty((KeyString)"nodeValue",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNodeValue(element, in a), "get nodeValue"),
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.SetNodeValue(this, element, in a), "set nodeValue"),
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
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetOwnerDocument(this, element, in a), "get ownerDocument"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // parentElement (read-only, dynamic) — like parentNode but returns null for non-element parents
        obj.FastAddProperty((KeyString)"parentElement",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetParentElement(this, element, in a), "get parentElement"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // hasChildNodes()
        obj.FastAddValue((KeyString)"hasChildNodes",
            new JSFunction((in a) => element.ChildNodes.Count > 0 ? JSBoolean.True : JSBoolean.False, "hasChildNodes", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // hasAttribute(name)
        obj.FastAddValue((KeyString)"hasAttribute",
            new JSFunction((in a) => _attributes.HasAttribute(element, in a), "hasAttribute", 1),
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
            new JSFunction((in a) => _attributes.RemoveAttribute(element, in a), "removeAttribute", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // toggleAttribute(name, force)
        obj.FastAddValue((KeyString)"toggleAttribute",
            new JSFunction((in a) => _attributes.ToggleAttribute(element, in a), "toggleAttribute", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"setAttributeNode",
            new JSFunction((in a) => _attributes.SetAttributeNode(element, obj, in a), "setAttributeNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"setAttributeNodeNS",
            new JSFunction((in a) => _attributes.SetAttributeNodeNS(element, obj, in a), "setAttributeNodeNS", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"removeAttributeNode",
            new JSFunction((in a) => _attributes.RemoveAttributeNode(element, obj, in a), "removeAttributeNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"removeAttributeNodeNS",
            new JSFunction((in a) => _attributes.RemoveAttributeNodeNS(element, obj, in a), "removeAttributeNodeNS", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setAttributeNS(namespace, qualifiedName, value)
        obj.FastAddValue((KeyString)"setAttributeNS",
            new JSFunction((in a) => _attributes.SetAttributeNS(element, in a), "setAttributeNS", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getAttributeNS(namespace, localName)
        obj.FastAddValue((KeyString)"getAttributeNS",
            new JSFunction((in a) => _attributes.GetAttributeNS(element, in a), "getAttributeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeAttributeNS(namespace, localName)
        obj.FastAddValue((KeyString)"removeAttributeNS",
            new JSFunction((in a) => _attributes.RemoveAttributeNS(element, in a), "removeAttributeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // hasAttributeNS(namespace, localName)
        obj.FastAddValue((KeyString)"hasAttributeNS",
            new JSFunction((in a) => _attributes.HasAttributeNS(element, in a), "hasAttributeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // contains(otherNode) — returns true if otherNode is a descendant
        obj.FastAddValue((KeyString)"contains",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.Contains(this, element, in a), "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // compareDocumentPosition(otherNode)
        obj.FastAddValue((KeyString)"compareDocumentPosition",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.CompareDocumentPosition(this, element, in a), "compareDocumentPosition", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // isSameNode(otherNode)
        obj.FastAddValue((KeyString)"isSameNode",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.IsSameNode(this, element, in a), "isSameNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // normalize()
        obj.FastAddValue((KeyString)"normalize",
            new JSFunction((in _) => Dom.Features.NodeRelationshipsBinding.Normalize(this, element, in _), "normalize", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // isEqualNode(otherNode)
        obj.FastAddValue((KeyString)"isEqualNode",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.IsEqualNode(this, element, in a), "isEqualNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"getRootNode",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.GetRootNode(this, element, in a), "getRootNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // cloneNode(deep)
        obj.FastAddValue((KeyString)"cloneNode",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.CloneNode(this, element, in a), "cloneNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // insertBefore(newChild, refChild)
        var bridgeForInsert = this;
        obj.FastAddValue((KeyString)"insertBefore",
            new JSFunction((in a) => JsJsObjectsInsertBefore080Core(bridgeForInsert, element, in a), "insertBefore", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // children (read-only) — element children only (no text nodes)
        obj.FastAddProperty((KeyString)"children",
            new JSFunction((in a) => Dom.Features.ElementTraversalBinding.GetChildren(this, element, in a), "get children"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // childElementCount (read-only)
        obj.FastAddProperty((KeyString)"childElementCount",
            new JSFunction((in a) => new JSNumber(ChildElements(element).Count(c => !IsText(c))), "get childElementCount"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // firstElementChild (read-only)
        obj.FastAddProperty((KeyString)"firstElementChild",
            new JSFunction((in a) => Dom.Features.ElementTraversalBinding.GetFirstElementChild(this, element, in a), "get firstElementChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastElementChild (read-only)
        obj.FastAddProperty((KeyString)"lastElementChild",
            new JSFunction((in a) => Dom.Features.ElementTraversalBinding.GetLastElementChild(this, element, in a), "get lastElementChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextElementSibling (read-only)
        obj.FastAddProperty((KeyString)"nextElementSibling",
            new JSFunction((in a) => Dom.Features.ElementTraversalBinding.GetNextElementSibling(this, element, in a), "get nextElementSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // previousElementSibling (read-only)
        obj.FastAddProperty((KeyString)"previousElementSibling",
            new JSFunction((in a) => Dom.Features.ElementTraversalBinding.GetPreviousElementSibling(this, element, in a), "get previousElementSibling"),
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
            new JSFunction((in a) => Dom.Features.ChildNodeBinding.Remove(this, element, in a), "remove", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"before",
            new JSFunction((in a) => Dom.Features.ChildNodeBinding.Before(this, element, in a), "before", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"after",
            new JSFunction((in a) => Dom.Features.ChildNodeBinding.After(this, element, in a), "after", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"replaceWith",
            new JSFunction((in a) => Dom.Features.ChildNodeBinding.ReplaceWith(this, element, in a), "replaceWith", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- DOM events --

        // addEventListener(type, listener, useCapture)
        obj.FastAddValue((KeyString)"addEventListener",
            new JSFunction((in a) => Dom.Features.EventTargetBinding.AddEventListener(this, element, in a), "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeEventListener(type, listener, useCapture)
        obj.FastAddValue((KeyString)"removeEventListener",
            new JSFunction((in a) => Dom.Features.EventTargetBinding.RemoveEventListener(this, element, in a), "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // dispatchEvent(event) — DOM Events Level 3 with capture/target/bubble phases
        obj.FastAddValue((KeyString)"dispatchEvent",
            new JSFunction((in a) => Dom.Features.EventTargetBinding.DispatchEvent(this, element, in a), "dispatchEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // element.click() — creates and dispatches a MouseEvent
        // For checkboxes and radio buttons, toggles checked state.
        obj.FastAddValue((KeyString)"click",
            new JSFunction((in _) => Dom.Features.EventTargetBinding.Click(this, element, in _), "click", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // element.focus() — creates and dispatches a FocusEvent-like object
        obj.FastAddValue((KeyString)"focus",
            new JSFunction((in _) => Dom.Features.EventTargetBinding.Focus(this, element, in _), "focus", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // element.blur() — creates and dispatches a FocusEvent-like object
        obj.FastAddValue((KeyString)"blur",
            new JSFunction((in _) => Dom.Features.EventTargetBinding.Blur(this, element, in _), "blur", 0),
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
            new JSFunction((in a) => Dom.Features.SelectorsBinding.QuerySelector(this, element, in a), "querySelector", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // querySelectorAll on elements
        obj.FastAddValue((KeyString)"querySelectorAll",
            new JSFunction((in a) => Dom.Features.SelectorsBinding.QuerySelectorAll(this, element, in a), "querySelectorAll", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"matches",
            new JSFunction((in a) => Dom.Features.SelectorsBinding.Matches(this, element, in a), "matches", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"closest",
            new JSFunction((in a) => Dom.Features.SelectorsBinding.Closest(this, element, in a), "closest", 1),
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
            new JSFunction((in a) => Dom.Features.SelectorsBinding.GetElementsByTagName(this, element, in a), "getElementsByTagName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getContext(contextType) — for <canvas> elements
        obj.FastAddValue((KeyString)"getContext",
            new JSFunction((in a) => JsJsObjectsGetContext134Core(element, in a), "getContext", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // <iframe> browsing-context accessors (contentDocument/contentWindow/getSVGDocument, src/srcdoc
        // read/write, sandbox reflection) — Phase 3 P3.55: extracted into the co-located IframeElementBinding
        // feature module, sibling of the P3.52 <object> ObjectElementBinding. Reaches the frames machinery
        // through the IIframeElementHost contract (DomBridge.IframeElementHost.cs).
        Dom.Features.IframeElementBinding.Install(this, obj, element);

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

}
