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

/// <summary>
/// Conversion of <see cref="DomElement"/> instances to YantraJS
/// <see cref="JSObject"/> representations, including sub-document
/// construction and tree-search helpers.
/// </summary>
public sealed partial class DomBridge
{
    private const double DefaultBodyMarginPixels = 8;
    private const int MaxScrollContinuationDepth = 16;

    private readonly Dictionary<DomElement, JSObject> _jsObjectCache = [];
    /// <summary>Counter for tracking top-layer insertion order via showModal().</summary>
    private int _topLayerCounter;

    internal JSObject ToJSObject(DomElement element)
    {
        if (_jsObjectCache.TryGetValue(element, out var cached))
            return cached;

        var obj = new JSObject();
        var bridge = this;
        _jsObjectCache[element] = obj;

        obj.FastAddValue(
            (KeyString)"tagName",
            new JSString(
                string.IsNullOrEmpty(element.NamespaceURI) ||
                string.Equals(element.NamespaceURI, "http://www.w3.org/1999/xhtml", StringComparison.OrdinalIgnoreCase)
                    ? element.TagName.ToUpperInvariant()
                    : element.TagName),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddProperty(
            (KeyString)"id",
            new JSFunction((in Arguments a) => element.Id != null ? new JSString(element.Id) : JSNull.Value,
                "get id"),
            new JSFunction((in Arguments a) => JsJsObjectsSetId002Core(bridge, element, in a), "set id"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // className (read/write) — reflects the 'class' content attribute
        obj.FastAddProperty(
            (KeyString)"className",
            new JSFunction((in Arguments a) => JsJsObjectsGetClassName003Core(element, in a), "get className"),
            new JSFunction((in Arguments a) => JsJsObjectsSetClassName004Core(bridge, element, in a), "set className"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // title (read/write) — synced with attributes["title"]
        obj.FastAddProperty(
            (KeyString)"title",
            new JSFunction((in Arguments a) => element.Attributes.TryGetValue("title", out var t) ? new JSString(t) : new JSString(string.Empty),
                "get title"),
            new JSFunction((in Arguments a) => JsJsObjectsSetTitle006Core(element, in a), "set title"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lang (read/write) — synced with attributes["lang"]
        obj.FastAddProperty(
            (KeyString)"lang",
            new JSFunction((in Arguments a) => element.Attributes.TryGetValue("lang", out var lang) ? new JSString(lang) : new JSString(string.Empty),
                "get lang"),
            new JSFunction((in Arguments a) => JsJsObjectsSetLang008Core(element, in a), "set lang"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // accessKey (read/write) — synced with attributes["accesskey"]
        obj.FastAddProperty(
            (KeyString)"accessKey",
            new JSFunction((in Arguments a) => element.Attributes.TryGetValue("accesskey", out var accessKey) ? new JSString(accessKey) : new JSString(string.Empty),
                "get accessKey"),
            new JSFunction((in Arguments a) => JsJsObjectsSetAccessKey010Core(element, in a), "set accessKey"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // dir (read/write) — synced with attributes["dir"]
        obj.FastAddProperty(
            (KeyString)"dir",
            new JSFunction((in Arguments a) => element.Attributes.TryGetValue("dir", out var dir) ? new JSString(dir) : new JSString(string.Empty),
                "get dir"),
            new JSFunction((in Arguments a) => JsJsObjectsSetDir012Core(bridge, element, in a), "set dir"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // draggable (read/write) — reflected enumerated attribute
        obj.FastAddProperty(
            (KeyString)"draggable",
            new JSFunction((in Arguments _) => JsJsObjectsGetDraggable013Core(element, in _), "get draggable"),
            new JSFunction((in Arguments a) => JsJsObjectsSetDraggable014Core(element, in a), "set draggable"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // innerHTML (read/write)
        obj.FastAddProperty(
            (KeyString)"innerHTML",
            new JSFunction((in Arguments a) => new JSString(element.InnerHtml), "get innerHTML"),
            new JSFunction((in Arguments a) => JsJsObjectsSetInnerHTML016Core(bridge, element, in a), "set innerHTML"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // outerHTML (read/write)
        obj.FastAddProperty(
            (KeyString)"outerHTML",
            new JSFunction((in Arguments _) => new JSString(SerializeElementToHtml(element)), "get outerHTML"),
            new JSFunction((in Arguments a) => JsJsObjectsSetOuterHTML018Core(bridge, element, in a), "set outerHTML"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty(
            (KeyString)"shadowRoot",
            new JSFunction((in Arguments _) => JsJsObjectsGetShadowRoot019Core(element, in _), "get shadowRoot"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        JSValue GetNodeTextValue()
        {
            // For text nodes, return the direct text content
            if (element.IsTextNode)
                return element.TextContent != null ? new JSString(element.TextContent) : new JSString(string.Empty);
            // For element nodes with direct TextContent set (e.g., via JS setter)
            if (element.TextContent != null && element.Children.Count == 0)
                return new JSString(element.TextContent);
            // For element nodes, recursively collect text from descendants
            if (element.Children.Count > 0)
            {
                var sb = new StringBuilder();
                CollectTextContent(element, sb);
                return new JSString(sb.ToString());
            }
            // Fallback to InnerHtml if no children and no TextContent
            return new JSString(element.InnerHtml);
        }

        // textContent (read/write)
        obj.FastAddProperty(
            (KeyString)"textContent",
            new JSFunction((in Arguments _) => this.GetNodeTextValue(element), "get textContent"),
            new JSFunction((in Arguments a) => JsJsObjectsSetTextContent021Core(bridge, element, in a), "set textContent"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty(
            (KeyString)"innerText",
            new JSFunction((in Arguments _) => this.GetNodeTextValue(element), "get innerText"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty(
            (KeyString)"outerText",
            new JSFunction((in Arguments _) => this.GetNodeTextValue(element), "get outerText"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // style object — CSS property access and manipulation.
        // In browsers, `element.style` is a read-only property: assigning a
        // string sets `style.cssText` instead of replacing the object.
        var styleObj = BuildStyleObject(element, () => bridge.InvalidateStyleScope(element));
        obj.FastAddProperty(
            (KeyString)"style",
            new JSFunction((in Arguments a) => styleObj, "get style"),
            new JSFunction((in Arguments a) => JsJsObjectsSetStyle025Core(bridge, element, in a), "set style"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // classList — class list manipulation
        obj.FastAddValue(
            (KeyString)"classList",
            BuildClassListObject(element, bridge.InvalidateStyleScope),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // attributes — NamedNodeMap interface
        obj.FastAddProperty(
            (KeyString)"attributes",
            new JSFunction((in Arguments _) => BuildNamedNodeMapObject(element, obj),
                "get attributes"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // setAttribute(name, value)
        var bridgeForSet = this;
        obj.FastAddValue(
            (KeyString)"setAttribute",
            new JSFunction((in Arguments a) => JsJsObjectsSetAttribute027Core(bridgeForSet, element, in a), "setAttribute", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getAttribute(name)
        obj.FastAddValue(
            (KeyString)"getAttribute",
            new JSFunction((in Arguments a) => JsJsObjectsGetAttribute028Core(element, in a), "getAttribute", 1),
             JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"getAttributeNode",
            new JSFunction((in Arguments a) => JsJsObjectsGetAttributeNode029Core(element, obj, in a), "getAttributeNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"getAttributeNodeNS",
            new JSFunction((in Arguments a) => JsJsObjectsGetAttributeNodeNS030Core(element, obj, in a), "getAttributeNodeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- DOM tree navigation --

        // parentNode (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"parentNode",
            new JSFunction((in Arguments a) => element.Parent != null ? ToJSObject(element.Parent) : JSNull.Value,
                "get parentNode"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty(
            (KeyString)"isConnected",
            new JSFunction((in Arguments _) => JsJsObjectsGetIsConnected032Core(element, in _), "get isConnected"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // childNodes (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"childNodes",
            new JSFunction((in Arguments a) => JsJsObjectsGetChildNodes033Core(element, in a), "get childNodes"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // firstChild (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"firstChild",
            new JSFunction((in Arguments a) => JsJsObjectsGetFirstChild034Core(element, in a), "get firstChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastChild (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"lastChild",
            new JSFunction((in Arguments a) => JsJsObjectsGetLastChild035Core(element, in a), "get lastChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextSibling (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"nextSibling",
            new JSFunction((in Arguments a) => JsJsObjectsGetNextSibling036Core(element, in a), "get nextSibling"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // previousSibling (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"previousSibling",
            new JSFunction((in Arguments a) => JsJsObjectsGetPreviousSibling037Core(element, in a), "get previousSibling"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeType (read-only)
        obj.FastAddProperty(
            (KeyString)"nodeType",
            new JSFunction((in Arguments a) => JsJsObjectsGetNodeType038Core(element, in a), "get nodeType"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeName (read-only)
        obj.FastAddProperty(
            (KeyString)"nodeName",
            new JSFunction((in Arguments a) => JsJsObjectsGetNodeName039Core(element, in a), "get nodeName"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // localName (read-only) — null for non-element nodes; local part of tag name for elements
        obj.FastAddProperty(
            (KeyString)"localName",
            new JSFunction((in Arguments a) => JsJsObjectsGetLocalName040Core(element, in a), "get localName"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // prefix (read-only) — namespace prefix or null
        obj.FastAddProperty(
            (KeyString)"prefix",
            new JSFunction((in Arguments a) => JsJsObjectsGetPrefix041Core(element, in a), "get prefix"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // namespaceURI (read-only) — returns namespace URI for elements created via createElementNS
        obj.FastAddProperty(
            (KeyString)"namespaceURI",
            new JSFunction((in Arguments a) => JsJsObjectsGetNamespaceURI042Core(element, in a), "get namespaceURI"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeValue (read/write) — null for elements, text content for text/comment nodes
        obj.FastAddProperty(
            (KeyString)"nodeValue",
            new JSFunction((in Arguments a) => JsJsObjectsGetNodeValue043Core(element, in a), "get nodeValue"),
            new JSFunction((in Arguments a) => JsJsObjectsSetNodeValue044Core(bridge, element, in a), "set nodeValue"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // data (read/write) — for text nodes and comment nodes (alias for nodeValue/textContent)
        obj.FastAddProperty(
            (KeyString)"data",
            new JSFunction((in Arguments a) => JsJsObjectsGetData045Core(element, in a), "get data"),
            new JSFunction((in Arguments a) => JsJsObjectsSetData046Core(bridge, element, in a), "set data"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // length (read-only) — character count for text/comment nodes, child count for elements
        obj.FastAddProperty(
            (KeyString)"length",
            new JSFunction((in Arguments a) => JsJsObjectsGetLength047Core(element, in a), "get length"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // splitText(offset) — splits a text node at the given character offset
        if (element.IsTextNode)
        {
            obj.FastAddValue(
                (KeyString)"splitText",
                new JSFunction((in Arguments a) => JsJsObjectsSplitText048Core(element, in a), "splitText", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // substringData(offset, count) — for text/comment CharacterData nodes
        if (element.IsTextNode || string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
        {
            obj.FastAddValue(
                (KeyString)"substringData",
                new JSFunction((in Arguments a) => JsJsObjectsSubstringData049Core(element, in a), "substringData", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue(
                (KeyString)"appendData",
                new JSFunction((in Arguments a) => JsJsObjectsAppendData050Core(bridge, element, in a), "appendData", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue(
                (KeyString)"deleteData",
                new JSFunction((in Arguments a) => JsJsObjectsDeleteData051Core(bridge, element, in a), "deleteData", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue(
                (KeyString)"insertData",
                new JSFunction((in Arguments a) => JsJsObjectsInsertData052Core(bridge, element, in a), "insertData", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue(
                (KeyString)"replaceData",
                new JSFunction((in Arguments a) => JsJsObjectsReplaceData053Core(bridge, element, in a), "replaceData", 3),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // DOCTYPE-specific properties
        if (string.Equals(element.TagName, "#doctype", StringComparison.OrdinalIgnoreCase))
        {
            obj.FastAddProperty(
                (KeyString)"name",
                new JSFunction((in Arguments _) => new JSString(GetDocTypeName(element)), "get name"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty(
                (KeyString)"publicId",
                new JSFunction((in Arguments _) => JsJsObjectsGetPublicId055Core(element, in _), "get publicId"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty(
                (KeyString)"systemId",
                new JSFunction((in Arguments _) => JsJsObjectsGetSystemId056Core(element, in _), "get systemId"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty(
                (KeyString)"internalSubset",
                NullFunction("get internalSubset"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // ownerDocument (read-only) — returns the Document node (nodeType=9)
        obj.FastAddProperty(
            (KeyString)"ownerDocument",
            new JSFunction((in Arguments a) => JsJsObjectsGetOwnerDocument057Core(element, in a), "get ownerDocument"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // parentElement (read-only, dynamic) — like parentNode but returns null for non-element parents
        obj.FastAddProperty(
            (KeyString)"parentElement",
            new JSFunction((in Arguments a) => JsJsObjectsGetParentElement058Core(element, in a), "get parentElement"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // hasChildNodes()
        obj.FastAddValue(
            (KeyString)"hasChildNodes",
            new JSFunction((in Arguments a) => element.Children.Count > 0 ? JSBoolean.True : JSBoolean.False,
                "hasChildNodes", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // hasAttribute(name)
        obj.FastAddValue(
            (KeyString)"hasAttribute",
            new JSFunction((in Arguments a) => JsJsObjectsHasAttribute060Core(element, in a), "hasAttribute", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // hasAttributes()
        obj.FastAddValue(
            (KeyString)"hasAttributes",
            new JSFunction((in Arguments _) => element.Attributes.Count > 0 ? JSBoolean.True : JSBoolean.False,
                "hasAttributes", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getAttributeNames()
        obj.FastAddValue(
            (KeyString)"getAttributeNames",
            new JSFunction((in Arguments _) => new JSArray(element.Attributes.Keys.Select(static name => (JSValue)new JSString(name)).ToArray()),
                "getAttributeNames", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeAttribute(name)
        obj.FastAddValue(
            (KeyString)"removeAttribute",
            new JSFunction((in Arguments a) => JsJsObjectsRemoveAttribute063Core(bridgeForSet, element, in a), "removeAttribute", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // toggleAttribute(name, force)
        obj.FastAddValue(
            (KeyString)"toggleAttribute",
            new JSFunction((in Arguments a) => JsJsObjectsToggleAttribute064Core(element, in a), "toggleAttribute", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"setAttributeNode",
            new JSFunction((in Arguments a) => JsJsObjectsSetAttributeNode065Core(element, obj, in a), "setAttributeNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"setAttributeNodeNS",
            new JSFunction((in Arguments a) => JsJsObjectsSetAttributeNodeNS066Core(element, obj, in a), "setAttributeNodeNS", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"removeAttributeNode",
            new JSFunction((in Arguments a) => JsJsObjectsRemoveAttributeNode067Core(element, obj, in a), "removeAttributeNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"removeAttributeNodeNS",
            new JSFunction((in Arguments a) => JsJsObjectsRemoveAttributeNodeNS068Core(element, obj, in a), "removeAttributeNodeNS", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setAttributeNS(namespace, qualifiedName, value)
        obj.FastAddValue(
            (KeyString)"setAttributeNS",
            new JSFunction((in Arguments a) => JsJsObjectsSetAttributeNS069Core(bridgeForSet, element, in a), "setAttributeNS", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getAttributeNS(namespace, localName)
        obj.FastAddValue(
            (KeyString)"getAttributeNS",
            new JSFunction((in Arguments a) => JsJsObjectsGetAttributeNS070Core(element, in a), "getAttributeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeAttributeNS(namespace, localName)
        obj.FastAddValue(
            (KeyString)"removeAttributeNS",
            new JSFunction((in Arguments a) => JsJsObjectsRemoveAttributeNS071Core(bridgeForSet, element, in a), "removeAttributeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // hasAttributeNS(namespace, localName)
        obj.FastAddValue(
            (KeyString)"hasAttributeNS",
            new JSFunction((in Arguments a) => JsJsObjectsHasAttributeNS072Core(element, in a), "hasAttributeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // contains(otherNode) — returns true if otherNode is a descendant
        obj.FastAddValue(
            (KeyString)"contains",
            new JSFunction((in Arguments a) => JsJsObjectsContains073Core(element, in a), "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // compareDocumentPosition(otherNode)
        obj.FastAddValue(
            (KeyString)"compareDocumentPosition",
            new JSFunction((in Arguments a) => JsJsObjectsCompareDocumentPosition074Core(element, in a), "compareDocumentPosition", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // isSameNode(otherNode)
        obj.FastAddValue(
            (KeyString)"isSameNode",
            new JSFunction((in Arguments a) => JsJsObjectsIsSameNode075Core(element, in a), "isSameNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // normalize()
        obj.FastAddValue(
            (KeyString)"normalize",
            new JSFunction((in Arguments _) => JsJsObjectsNormalize076Core(element, in _), "normalize", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // isEqualNode(otherNode)
        obj.FastAddValue(
            (KeyString)"isEqualNode",
            new JSFunction((in Arguments a) => JsJsObjectsIsEqualNode077Core(element, in a), "isEqualNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"getRootNode",
            new JSFunction((in Arguments a) => JsJsObjectsGetRootNode078Core(element, in a), "getRootNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // cloneNode(deep)
        obj.FastAddValue(
            (KeyString)"cloneNode",
            new JSFunction((in Arguments a) => JsJsObjectsCloneNode079Core(element, in a), "cloneNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // insertBefore(newChild, refChild)
        var bridgeForInsert = this;
        obj.FastAddValue(
            (KeyString)"insertBefore",
            new JSFunction((in Arguments a) => JsJsObjectsInsertBefore080Core(bridgeForInsert, element, in a), "insertBefore", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // children (read-only) — element children only (no text nodes, no #subdoc-root)
        obj.FastAddProperty(
            (KeyString)"children",
            new JSFunction((in Arguments a) => JsJsObjectsGetChildren081Core(element, in a), "get children"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // childElementCount (read-only)
        obj.FastAddProperty(
            (KeyString)"childElementCount",
            new JSFunction((in Arguments a) => new JSNumber(element.Children.Count(c => !c.IsTextNode && !IsSubDocRoot(c))),
                "get childElementCount"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // firstElementChild (read-only)
        obj.FastAddProperty(
            (KeyString)"firstElementChild",
            new JSFunction((in Arguments a) => JsJsObjectsGetFirstElementChild083Core(element, in a), "get firstElementChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastElementChild (read-only)
        obj.FastAddProperty(
            (KeyString)"lastElementChild",
            new JSFunction((in Arguments a) => JsJsObjectsGetLastElementChild084Core(element, in a), "get lastElementChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextElementSibling (read-only)
        obj.FastAddProperty(
            (KeyString)"nextElementSibling",
            new JSFunction((in Arguments a) => JsJsObjectsGetNextElementSibling085Core(element, in a), "get nextElementSibling"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // previousElementSibling (read-only)
        obj.FastAddProperty(
            (KeyString)"previousElementSibling",
            new JSFunction((in Arguments a) => JsJsObjectsGetPreviousElementSibling086Core(element, in a), "get previousElementSibling"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // -- DOM manipulation methods --

        obj.FastAddValue(
            (KeyString)"attachShadow",
            new JSFunction((in Arguments a) => JsJsObjectsAttachShadow087Core(element, in a), "attachShadow", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // appendChild(child)
        var bridgeForAppend = this;
        obj.FastAddValue(
            (KeyString)"appendChild",
            new JSFunction((in Arguments a) => JsJsObjectsAppendChild088Core(bridgeForAppend, element, in a), "appendChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"append",
            new JSFunction((in Arguments a) => JsJsObjectsAppend089Core(element, in a), "append", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"prepend",
            new JSFunction((in Arguments a) => JsJsObjectsPrepend090Core(element, in a), "prepend", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeChild(child)
        obj.FastAddValue(
            (KeyString)"removeChild",
            new JSFunction((in Arguments a) => JsJsObjectsRemoveChild091Core(bridgeForAppend, element, in a), "removeChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // replaceChild(newChild, oldChild)
        obj.FastAddValue(
            (KeyString)"replaceChild",
            new JSFunction((in Arguments a) => JsJsObjectsReplaceChild092Core(bridgeForAppend, element, in a), "replaceChild", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // remove() — ChildNode.remove() per DOM Living Standard
        obj.FastAddValue(
            (KeyString)"remove",
            new JSFunction((in Arguments a) => JsJsObjectsRemove093Core(element, in a), "remove", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"before",
            new JSFunction((in Arguments a) => JsJsObjectsBefore094Core(element, in a), "before", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"after",
            new JSFunction((in Arguments a) => JsJsObjectsAfter095Core(element, in a), "after", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"replaceWith",
            new JSFunction((in Arguments a) => JsJsObjectsReplaceWith096Core(element, in a), "replaceWith", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- DOM events --

        // addEventListener(type, listener, useCapture)
        obj.FastAddValue(
            (KeyString)"addEventListener",
            new JSFunction((in Arguments a) => JsJsObjectsAddEventListener097Core(element, in a), "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeEventListener(type, listener, useCapture)
        obj.FastAddValue(
            (KeyString)"removeEventListener",
            new JSFunction((in Arguments a) => JsJsObjectsRemoveEventListener098Core(element, in a), "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // dispatchEvent(event) — DOM Events Level 3 with capture/target/bubble phases
        obj.FastAddValue(
            (KeyString)"dispatchEvent",
            new JSFunction((in Arguments a) => JsJsObjectsDispatchEvent099Core(bridge, element, in a), "dispatchEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // element.click() — creates and dispatches a MouseEvent
        // For checkboxes and radio buttons, toggles checked state.
        obj.FastAddValue(
            (KeyString)"click",
            new JSFunction((in Arguments _) => JsJsObjectsClick101Core(bridge, element, in _), "click", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // element.focus() — creates and dispatches a FocusEvent-like object
        obj.FastAddValue(
            (KeyString)"focus",
            new JSFunction((in Arguments _) => JsJsObjectsFocus102Core(bridge, element, in _), "focus", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // element.blur() — creates and dispatches a FocusEvent-like object
        obj.FastAddValue(
            (KeyString)"blur",
            new JSFunction((in Arguments _) => JsJsObjectsBlur103Core(bridge, element, in _), "blur", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // on* inline event handler properties (onclick, onload, etc.)
        foreach (var eventName in InlineEventNames)
        {
            obj.FastAddProperty(
                (KeyString)$"on{eventName}",
                new JSFunction((in Arguments _) => JsJsObjectsCallback104Core(element, eventName, in _), $"get on{eventName}"),
                new JSFunction((in Arguments a) => JsJsObjectsCallback105Core(element, eventName, in a), $"set on{eventName}"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // Compile on* HTML attributes into inline event handler functions
        CompileInlineEventAttributes(element);

        // -- Form element support --

        // value (read/write) — for input, textarea, select elements
        // The IDL 'value' property is NOT reflected as a content attribute for inputs.
        obj.FastAddProperty(
            (KeyString)"value",
            new JSFunction((in Arguments a) => JsJsObjectsGetValue106Core(element, in a), "get value"),
            new JSFunction((in Arguments a) => JsJsObjectsSetValue107Core(element, in a), "set value"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // checked (read/write) — for checkbox and radio inputs
        // Uses DomProperties["checked"] as the "dirty" IDL state that tracks
        // programmatic changes. setAttribute("checked") only sets the content
        // attribute and does NOT affect this IDL state.
        obj.FastAddProperty(
            (KeyString)"checked",
            new JSFunction((in Arguments a) => JsJsObjectsGetChecked108Core(element, in a), "get checked"),
            new JSFunction((in Arguments a) => JsJsObjectsSetChecked109Core(element, in a), "set checked"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // type (read/write) — for input/button elements; getter returns lowercase
        obj.FastAddProperty(
            (KeyString)"type",
            new JSFunction((in Arguments a) => JsJsObjectsGetType110Core(element, in a), "get type"),
            new JSFunction((in Arguments a) => JsJsObjectsSetType111Core(element, in a), "set type"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // name (read/write) — for form elements; syncs with content attribute
        // Skip for DOCTYPE nodes which have their own name property (doctype name)
        if (!string.Equals(element.TagName, "#doctype", StringComparison.OrdinalIgnoreCase))
        {
            obj.FastAddProperty(
                (KeyString)"name",
                new JSFunction((in Arguments a) => JsJsObjectsGetName112Core(element, in a), "get name"),
                new JSFunction((in Arguments a) => JsJsObjectsSetName113Core(element, in a), "set name"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // disabled (read/write) — for form controls
        obj.FastAddProperty(
            (KeyString)"disabled",
            new JSFunction((in Arguments a) => element.Attributes.ContainsKey("disabled") ? JSBoolean.True : JSBoolean.False,
                "get disabled"),
            new JSFunction((in Arguments a) => JsJsObjectsSetDisabled115Core(bridge, element, in a), "set disabled"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // hidden (read/write) — global reflected boolean attribute
        obj.FastAddProperty(
            (KeyString)"hidden",
            new JSFunction((in Arguments a) => element.Attributes.ContainsKey("hidden") ? JSBoolean.True : JSBoolean.False,
                "get hidden"),
            new JSFunction((in Arguments a) => JsJsObjectsSetHidden117Core(bridge, element, in a), "set hidden"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // tabIndex (read/write) — global reflected numeric attribute
        obj.FastAddProperty(
            (KeyString)"tabIndex",
            new JSFunction((in Arguments _) => JsJsObjectsGetTabIndex118Core(element, in _), "get tabIndex"),
            new JSFunction((in Arguments a) => JsJsObjectsSetTabIndex119Core(element, in a), "set tabIndex"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // required (read/write) — form validation
        obj.FastAddProperty(
            (KeyString)"required",
            new JSFunction((in Arguments a) => element.Attributes.ContainsKey("required") ? JSBoolean.True : JSBoolean.False,
                "get required"),
            new JSFunction((in Arguments a) => JsJsObjectsSetRequired121Core(bridge, element, in a), "set required"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // checkValidity() — form validation
        obj.FastAddValue(
            (KeyString)"checkValidity",
            new JSFunction((in Arguments a) => CheckElementValidity(element) ? JSBoolean.True : JSBoolean.False, "checkValidity", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // reportValidity() — form validation
        obj.FastAddValue(
            (KeyString)"reportValidity",
            new JSFunction((in Arguments a) => CheckElementValidity(element) ? JSBoolean.True : JSBoolean.False, "reportValidity", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // submit() — for form elements
        obj.FastAddValue(
            (KeyString)"submit",
            new JSFunction((in Arguments a) => JsJsObjectsSubmit125Core(element, obj, in a), "submit", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // querySelector on elements
        obj.FastAddValue(
            (KeyString)"querySelector",
            new JSFunction((in Arguments a) => JsJsObjectsQuerySelector126Core(bridge, element, in a), "querySelector", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // querySelectorAll on elements
        obj.FastAddValue(
            (KeyString)"querySelectorAll",
            new JSFunction((in Arguments a) => JsJsObjectsQuerySelectorAll127Core(bridge, element, in a), "querySelectorAll", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"matches",
            new JSFunction((in Arguments a) => JsJsObjectsMatches128Core(element, in a), "matches", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"closest",
            new JSFunction((in Arguments a) => JsJsObjectsClosest129Core(bridge, element, in a), "closest", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"insertAdjacentElement",
            new JSFunction((in Arguments a) => JsJsObjectsInsertAdjacentElement130Core(element, in a), "insertAdjacentElement", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"insertAdjacentText",
            new JSFunction((in Arguments a) => JsJsObjectsInsertAdjacentText131Core(element, in a), "insertAdjacentText", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"insertAdjacentHTML",
            new JSFunction((in Arguments a) => JsJsObjectsInsertAdjacentHTML132Core(element, in a), "insertAdjacentHTML", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getElementsByTagName on elements — searches descendants in tree order
        obj.FastAddValue(
            (KeyString)"getElementsByTagName",
            new JSFunction((in Arguments a) => JsJsObjectsGetElementsByTagName133Core(bridge, element, in a), "getElementsByTagName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getContext(contextType) — for <canvas> elements
        obj.FastAddValue(
            (KeyString)"getContext",
            new JSFunction((in Arguments a) => JsJsObjectsGetContext134Core(element, in a), "getContext", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // contentWindow / contentDocument — for <iframe> elements with full sub-document DOM
        if (string.Equals(element.TagName, "iframe", StringComparison.OrdinalIgnoreCase))
        {
            bool IsCurrentIframeCrossOrigin()
            {
                if (element.Attributes.ContainsKey("srcdoc"))
                    return false;

                var iframeSrcValue = element.Attributes.TryGetValue("src", out var srcVal) ? srcVal : string.Empty;
                return IsCrossOrigin(iframeSrcValue, _pageUrl);
            }

            obj.FastAddProperty(
                (KeyString)"contentDocument",
                new JSFunction((in Arguments _) => JsJsObjectsGetContentDocument135Core(element, in _), "get contentDocument"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty(
                (KeyString)"contentWindow",
                new JSFunction((in Arguments _) => JsJsObjectsGetContentWindow136Core(element, in _), "get contentWindow"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // getSVGDocument() — returns contentDocument (same as contentDocument for same-origin)
            obj.FastAddValue(
                (KeyString)"getSVGDocument",
                new JSFunction((in Arguments _) => JsJsObjectsGetSVGDocument137Core(element, in _), "getSVGDocument", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // src property (read/write) — for iframe elements
            var bridgeForSrc = this;
            obj.FastAddProperty(
                (KeyString)"src",
                new JSFunction((in Arguments _) => element.Attributes.TryGetValue("src", out var s) ? new JSString(s) : new JSString(string.Empty), "get src"),
                new JSFunction((in Arguments a) => JsJsObjectsSetSrc139Core(bridgeForSrc, element, in a), "set src"),
                 JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty(
                (KeyString)"srcdoc",
                new JSFunction((in Arguments _) => element.Attributes.TryGetValue("srcdoc", out var s) ? new JSString(s) : new JSString(string.Empty), "get srcdoc"),
                new JSFunction((in Arguments a) => JsJsObjectsSetSrcdoc141Core(bridgeForSrc, element, in a), "set srcdoc"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // sandbox attribute access
            obj.FastAddProperty(
                (KeyString)"sandbox",
                new JSFunction((in Arguments _) => element.Attributes.TryGetValue("sandbox", out var sandbox) ? new JSString(sandbox) : new JSString(string.Empty), "get sandbox"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
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

}
