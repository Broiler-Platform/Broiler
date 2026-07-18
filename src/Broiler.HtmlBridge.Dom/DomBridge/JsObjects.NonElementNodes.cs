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
/// Sibling partial peeled out of <c>JsObjects.cs</c> (Phase 3 ratchet, 2026-07-17) to keep it under
/// the 750-line guard: the non-element node JS-wrapper populators. Builds the minimal JS surface for
/// canonical character-data nodes (<c>DomText</c>/<c>DomComment</c>), <c>DocumentType</c>, and
/// <c>DocumentFragment</c> — the counterparts to the element wrapper that <see cref="ToJSObject"/>
/// (still in <c>JsObjects.cs</c>) dispatches to for these node kinds. Pure partial-class
/// relocation — no signature, accessibility, or logic change.
/// </summary>
public sealed partial class DomBridge
{
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
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNodeType(node, in a), "get nodeType"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"nodeName",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNodeName(node, in a), "get nodeName"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"localName",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetLocalName(node, in a), "get localName"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"prefix",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetPrefix(node, in a), "get prefix"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"namespaceURI",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNamespaceURI(node, in a), "get namespaceURI"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // -- Character data --
        obj.FastAddProperty((KeyString)"nodeValue",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNodeValue(node, in a), "get nodeValue"),
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.SetNodeValue(this, node, in a), "set nodeValue"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"textContent",
            new JSFunction((in _) => GetNodeTextValue(node), "get textContent"),
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.SetNodeValue(this, node, in a), "set textContent"),
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
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetParentElement(this, node, in a), "get parentElement"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"isConnected",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetIsConnected(this, node, in a), "get isConnected"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"childNodes",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetChildNodes(this, node, in a), "get childNodes"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"firstChild",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetFirstChild(this, node, in a), "get firstChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"lastChild",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetLastChild(this, node, in a), "get lastChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"nextSibling",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNextSibling(this, node, in a), "get nextSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"previousSibling",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetPreviousSibling(this, node, in a), "get previousSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"ownerDocument",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetOwnerDocument(this, node, in a), "get ownerDocument"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddValue((KeyString)"hasChildNodes",
            new JSFunction((in a) => node.ChildNodes.Count > 0 ? JSBoolean.True : JSBoolean.False, "hasChildNodes", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- Node methods --
        obj.FastAddValue((KeyString)"cloneNode",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.CloneNode(this, node, in a), "cloneNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"contains",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.Contains(this, node, in a), "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"compareDocumentPosition",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.CompareDocumentPosition(this, node, in a), "compareDocumentPosition", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"isSameNode",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.IsSameNode(this, node, in a), "isSameNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"isEqualNode",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.IsEqualNode(this, node, in a), "isEqualNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"getRootNode",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.GetRootNode(this, node, in a), "getRootNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"normalize",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.Normalize(this, node, in a), "normalize", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- ChildNode mixin --
        obj.FastAddValue((KeyString)"remove",
            new JSFunction((in a) => Dom.Features.ChildNodeBinding.Remove(this, node, in a), "remove", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"before",
            new JSFunction((in a) => Dom.Features.ChildNodeBinding.Before(this, node, in a), "before", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"after",
            new JSFunction((in a) => Dom.Features.ChildNodeBinding.After(this, node, in a), "after", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"replaceWith",
            new JSFunction((in a) => Dom.Features.ChildNodeBinding.ReplaceWith(this, node, in a), "replaceWith", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- EventTarget --
        obj.FastAddValue((KeyString)"addEventListener",
            new JSFunction((in a) => Dom.Features.EventTargetBinding.AddEventListener(this, node, in a), "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"removeEventListener",
            new JSFunction((in a) => Dom.Features.EventTargetBinding.RemoveEventListener(this, node, in a), "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"dispatchEvent",
            new JSFunction((in a) => Dom.Features.EventTargetBinding.DispatchEvent(this, node, in a), "dispatchEvent", 1),
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
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNodeType(node, in a), "get nodeType"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"nodeName",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNodeName(node, in a), "get nodeName"),
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
            new JSFunction((in _) => Dom.Features.NodeAccessorsBinding.GetPublicId(node, in _), "get publicId"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"systemId",
            new JSFunction((in _) => Dom.Features.NodeAccessorsBinding.GetSystemId(node, in _), "get systemId"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"internalSubset",
            NullFunction("get internalSubset"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // -- Tree navigation --
        obj.FastAddProperty((KeyString)"parentNode",
            new JSFunction((in a) => node.ParentNode != null ? ToJSObject(node.ParentNode) : JSNull.Value, "get parentNode"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"parentElement",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetParentElement(this, node, in a), "get parentElement"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"previousSibling",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetPreviousSibling(this, node, in a), "get previousSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"nextSibling",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNextSibling(this, node, in a), "get nextSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"ownerDocument",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetOwnerDocument(this, node, in a), "get ownerDocument"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"isConnected",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetIsConnected(this, node, in a), "get isConnected"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddValue((KeyString)"hasChildNodes",
            new JSFunction((in a) => JSBoolean.False, "hasChildNodes", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- Node methods --
        obj.FastAddValue((KeyString)"cloneNode",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.CloneNode(this, node, in a), "cloneNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"isEqualNode",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.IsEqualNode(this, node, in a), "isEqualNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"isSameNode",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.IsSameNode(this, node, in a), "isSameNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"contains",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.Contains(this, node, in a), "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"compareDocumentPosition",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.CompareDocumentPosition(this, node, in a), "compareDocumentPosition", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"getRootNode",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.GetRootNode(this, node, in a), "getRootNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- ChildNode mixin --
        obj.FastAddValue((KeyString)"remove",
            new JSFunction((in a) => Dom.Features.ChildNodeBinding.Remove(this, node, in a), "remove", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"before",
            new JSFunction((in a) => Dom.Features.ChildNodeBinding.Before(this, node, in a), "before", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"after",
            new JSFunction((in a) => Dom.Features.ChildNodeBinding.After(this, node, in a), "after", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"replaceWith",
            new JSFunction((in a) => Dom.Features.ChildNodeBinding.ReplaceWith(this, node, in a), "replaceWith", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- EventTarget --
        obj.FastAddValue((KeyString)"addEventListener",
            new JSFunction((in a) => Dom.Features.EventTargetBinding.AddEventListener(this, node, in a), "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"removeEventListener",
            new JSFunction((in a) => Dom.Features.EventTargetBinding.RemoveEventListener(this, node, in a), "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"dispatchEvent",
            new JSFunction((in a) => Dom.Features.EventTargetBinding.DispatchEvent(this, node, in a), "dispatchEvent", 1),
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
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNodeType(node, in a), "get nodeType"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"nodeName",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNodeName(node, in a), "get nodeName"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"nodeValue",
            new JSFunction((in _) => JSNull.Value, "get nodeValue"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"ownerDocument",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetOwnerDocument(this, node, in a), "get ownerDocument"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // -- Tree navigation --
        obj.FastAddProperty((KeyString)"parentNode",
            new JSFunction((in _) => node.ParentNode != null ? ToJSObject(node.ParentNode) : JSNull.Value, "get parentNode"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"parentElement",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetParentElement(this, node, in a), "get parentElement"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"childNodes",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetChildNodes(this, node, in a), "get childNodes"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"firstChild",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetFirstChild(this, node, in a), "get firstChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"lastChild",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetLastChild(this, node, in a), "get lastChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        obj.FastAddProperty((KeyString)"isConnected",
            new JSFunction((in a) => Dom.Features.NodeAccessorsBinding.GetIsConnected(this, node, in a), "get isConnected"),
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
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.CloneNode(this, node, in a), "cloneNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"isEqualNode",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.IsEqualNode(this, node, in a), "isEqualNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"isSameNode",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.IsSameNode(this, node, in a), "isSameNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"contains",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.Contains(this, node, in a), "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"compareDocumentPosition",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.CompareDocumentPosition(this, node, in a), "compareDocumentPosition", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"getRootNode",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.GetRootNode(this, node, in a), "getRootNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"normalize",
            new JSFunction((in a) => Dom.Features.NodeRelationshipsBinding.Normalize(this, node, in a), "normalize", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- EventTarget --
        obj.FastAddValue((KeyString)"addEventListener",
            new JSFunction((in a) => Dom.Features.EventTargetBinding.AddEventListener(this, node, in a), "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"removeEventListener",
            new JSFunction((in a) => Dom.Features.EventTargetBinding.RemoveEventListener(this, node, in a), "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"dispatchEvent",
            new JSFunction((in a) => Dom.Features.EventTargetBinding.DispatchEvent(this, node, in a), "dispatchEvent", 1),
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
