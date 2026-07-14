using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.HtmlBridge.Logging;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    private JSValue JsJsObjectsSetId002Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        var val = a.Length > 0 ? a[0].ToString() : string.Empty;
        element.Id = val;
        SetAttr(element, "id", val);
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetClassName003Core(DomElement element, in Arguments a)
    {
        // Prefer Attributes['class'] (synced by setAttribute and className setter).
        // Fall back to element.ClassName for elements created with a class in the constructor
        // but not yet synced to Attributes (e.g. parsed HTML elements).
        if (TryGetAttribute(element, "class", out var cls))
            return new JSString(cls);
        return element.ClassName != null ? new JSString(element.ClassName) : new JSString(string.Empty);
    }


    private JSValue JsJsObjectsSetClassName004Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        var val = a.Length > 0 ? a[0].ToString() : string.Empty;
        element.ClassName = val;
        SetAttr(element, "class", val);
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetTitle006Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "title", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetLang008Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "lang", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetAccessKey010Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "accesskey", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetDir012Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        SetAttr(element, "dir", a.Length > 0 ? a[0].ToString() : string.Empty);
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetDraggable013Core(DomElement element, in Arguments _)
    {
        if (TryGetAttribute(element, "draggable", out var draggable))
            return string.Equals(draggable, "true", StringComparison.OrdinalIgnoreCase) ? JSBoolean.True : JSBoolean.False;
        return JSBoolean.False;
    }


    private JSValue JsJsObjectsSetDraggable014Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "draggable", a.Length > 0 && a[0].BooleanValue ? "true" : "false");
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetInnerHTML016Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        bridge.SetElementInnerHtml(element, a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetOuterHTML018Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        bridge.SetElementOuterHtml(element, a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetShadowRoot019Core(DomElement element, in Arguments _)
    {
        var shadowRoot = GetShadowRoot(element);
        if (shadowRoot == null)
            return JSNull.Value;
        var mode = GetElementRuntimeState(element).Shadow.Mode.TryGet(out var rawMode) ? rawMode as string : null;
        return string.Equals(mode, "open", StringComparison.OrdinalIgnoreCase) ? ToJSObject(shadowRoot) : JSNull.Value;
    }


    private JSValue JsJsObjectsSetTextContent021Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        var text = a.Length > 0 ? a[0].ToString() : string.Empty;
        // Setting textContent replaces all children with a single text node per DOM spec.
        bridge.SetElementTextContent(element, text);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetStyle025Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSString s)
        {
            // Setting element.style = "prop: val; ..." parses as cssText
            InlineStyle(element).Clear();
            GetElementRuntimeState(element).JsSetStyleProps.Clear();
            foreach (var kv in ParseStyle(s.ToString(), reportDrops: true))
            {
                InlineStyle(element)[kv.Key] = kv.Value;
                GetElementRuntimeState(element).JsSetStyleProps.Add(kv.Key);
            }

            // Phase 4 item 2: write-through so getAttribute("style") observes the assignment.
            bridge.SyncStyleAttributeFromInlineStyle(element);
            bridge.InvalidateStyleScope(element);
        }

        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetAttribute027Core(DomBridge? bridgeForSet, DomElement element, in Arguments a)
    {
        if (a.Length >= 2)
        {
            var attrName = a[0].ToString();
            var attrVal = a[1].ToString();
            TryGetAttribute(element, attrName, out var previousAttrVal);
            SetAttr(element, attrName, attrVal);
            // Sync special properties
            if (string.Equals(attrName, "id", StringComparison.OrdinalIgnoreCase))
                element.Id = attrVal;
            else if (string.Equals(attrName, "class", StringComparison.OrdinalIgnoreCase))
                element.ClassName = attrVal;
            else if (string.Equals(attrName, "style", StringComparison.OrdinalIgnoreCase))
            {
                InlineStyle(element).Clear();
                foreach (var kv in ParseStyle(attrVal, reportDrops: true))
                    InlineStyle(element)[kv.Key] = kv.Value;
                bridgeForSet.InvalidateStyleScope(element);
            }
            // Compile on* event handler attributes into functions
            else if (attrName.Length > 2 && attrName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
            {
                bridgeForSet.CompileInlineEventAttribute(element, attrName, attrVal);
            }

            if (!string.Equals(attrName, "style", StringComparison.OrdinalIgnoreCase))
                bridgeForSet.InvalidateStyleScope(element);
            if (!string.Equals(previousAttrVal, attrVal, StringComparison.Ordinal))
                bridgeForSet.NotifyAttributeMutationObservers(element, attrName, previousAttrVal);
        }

        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetAttribute028Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var name = a[0].ToString();
        return TryGetAttribute(element, name, out var val) ? new JSString(val) : JSNull.Value;
    }


    private JSValue JsJsObjectsGetAttributeNode029Core(DomElement element, JSObject? obj, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var name = a[0].ToString();
        return TryGetAttribute(element, name, out var val) ? _attributes.BuildAttrNode(name, val, element, obj) : JSNull.Value;
    }


    private JSValue JsJsObjectsGetAttributeNodeNS030Core(DomElement element, JSObject? obj, in Arguments a)
    {
        if (a.Length < 2)
            return JSNull.Value;
        var ns = a[0].IsNull || a[0].IsUndefined ? null : a[0].ToString();
        var localName = a[1].ToString();
        if (!TryGetNsAttribute(element, ns, localName, out var qName, out var val))
            return JSNull.Value;
        return _attributes.BuildAttrNode(qName, val, element, obj);
    }


    private JSValue JsJsObjectsGetIsConnected032Core(DomNode node, in Arguments _)
    {
        var root = GetTreeRoot(node);
        return ReferenceEquals(root, _document) ? JSBoolean.True : JSBoolean.False;
    }

    /// <summary>Whether <paramref name="node"/> is a sub-document root element (only elements can be).</summary>
    private static bool IsSubDocRootNode(DomNode node) =>
        node is DomElement element && IsSubDocRoot(element);

    private JSValue JsJsObjectsGetChildNodes033Core(DomNode node, in Arguments a)
    {
        var children = new List<JSValue>();
        foreach (var child in node.ChildNodes)
        {
            if (!IsSubDocRootNode(child))
                children.Add(ToJSObject(child));
        }

        return new JSArray(children);
    }


    private JSValue JsJsObjectsGetFirstChild034Core(DomNode node, in Arguments a)
    {
        var first = node.ChildNodes.FirstOrDefault(c => !IsSubDocRootNode(c));
        return first != null ? ToJSObject(first) : JSNull.Value;
    }


    private JSValue JsJsObjectsGetLastChild035Core(DomNode node, in Arguments a)
    {
        var last = node.ChildNodes.LastOrDefault(c => !IsSubDocRootNode(c));
        return last != null ? ToJSObject(last) : JSNull.Value;
    }


    private JSValue JsJsObjectsGetNextSibling036Core(DomNode node, in Arguments a)
    {
        var parent = node.ParentNode;
        if (parent == null)
            return JSNull.Value;
        var siblings = parent.ChildNodes;
        var idx = ChildIndexOf(parent, node);
        for (var i = idx + 1; i < siblings.Count; i++)
        {
            if (!IsSubDocRootNode(siblings[i]))
                return ToJSObject(siblings[i]);
        }

        return JSNull.Value;
    }


    private JSValue JsJsObjectsGetPreviousSibling037Core(DomNode node, in Arguments a)
    {
        var parent = node.ParentNode;
        if (parent == null)
            return JSNull.Value;
        var siblings = parent.ChildNodes;
        var idx = ChildIndexOf(parent, node);
        for (var i = idx - 1; i >= 0; i--)
        {
            if (!IsSubDocRootNode(siblings[i]))
                return ToJSObject(siblings[i]);
        }

        return JSNull.Value;
    }


    private JSValue JsJsObjectsGetNodeType038Core(DomNode node, in Arguments a)
    {
        if (IsText(node))
            return new JSNumber(3); // TEXT_NODE
        if (IsComment(node))
            return new JSNumber(8); // COMMENT_NODE
        if (node is DomDocumentType)
            return new JSNumber(10); // DOCUMENT_TYPE_NODE (canonical DomDocumentType)
        if (node is DomDocumentFragment)
            return new JSNumber(11); // DOCUMENT_FRAGMENT_NODE (canonical DomDocumentFragment)
        if (node is DomDocument)
            return new JSNumber(9); // DOCUMENT_NODE (canonical DomDocument — the document root)
        if (node is not DomElement)
            return new JSNumber(1); // canonical non-element char-data already handled above
        return new JSNumber(1); // ELEMENT_NODE
    }


    private JSValue JsJsObjectsGetNodeName039Core(DomNode node, in Arguments a)
    {
        if (IsText(node))
            return new JSString("#text");
        if (IsComment(node))
            return new JSString("#comment");
        if (node is DomDocumentType docType)
            return new JSString(docType.Name); // doctype nodeName is its (already lowercased) name
        if (node is DomDocumentFragment)
            return new JSString("#document-fragment");
        if (node is DomDocument)
            return new JSString("#document"); // canonical DomDocument — the document root
        if (node is not DomElement element)
            return JSNull.Value;

        // Non-HTML namespace elements preserve original case (per DOM spec)
        if (!string.IsNullOrEmpty(element.NamespaceUri) && !string.Equals(element.NamespaceUri, "http://www.w3.org/1999/xhtml", StringComparison.OrdinalIgnoreCase))
            return new JSString(element.TagName);
        return new JSString(element.TagName.ToUpperInvariant());
    }


    private JSValue JsJsObjectsGetLocalName040Core(DomNode node, in Arguments a)
    {
        // localName is null for non-element nodes (text/comment/document).
        if (node is not DomElement element)
            return JSNull.Value;
        if (element.TagName.StartsWith('#'))
            return JSNull.Value; // #comment, #document, etc.
        var name = element.TagName;
        var colonIdx = name.IndexOf(':');
        if (colonIdx >= 0)
            name = name[(colonIdx + 1)..];
        return new JSString(name.ToLowerInvariant());
    }


    private JSValue JsJsObjectsGetPrefix041Core(DomNode node, in Arguments a)
    {
        if (node is not DomElement element)
            return JSNull.Value;
        var colonIdx = element.TagName.IndexOf(':');
        if (colonIdx >= 0)
            return new JSString(element.TagName[..colonIdx]);
        return JSNull.Value;
    }


    private JSValue JsJsObjectsGetNamespaceURI042Core(DomNode node, in Arguments a)
    {
        // namespaceURI is null for non-element nodes (text/comment/document).
        if (node is not DomElement element)
            return JSNull.Value;
        if (element.NamespaceUri != null)
            return new JSString(element.NamespaceUri);
        // Default namespace for HTML elements
        if (!element.TagName.StartsWith('#'))
            return new JSString("http://www.w3.org/1999/xhtml");
        return JSNull.Value;
    }


    private JSValue JsJsObjectsGetNodeValue043Core(DomNode node, in Arguments a)
    {
        if (IsText(node) || IsComment(node))
            return new JSString(BridgeText(node));
        return JSNull.Value;
    }


    private JSValue JsJsObjectsSetNodeValue044Core(DomBridge? bridge, DomNode node, in Arguments a)
    {
        if (IsText(node) || IsComment(node))
            bridge.SetCharacterData(node, a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetData045Core(DomNode node, in Arguments a)
    {
        if (IsText(node) || IsComment(node))
            return new JSString(BridgeText(node));
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetData046Core(DomBridge? bridge, DomNode node, in Arguments a)
    {
        if (IsText(node) || IsComment(node))
            bridge.SetCharacterData(node, a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetLength047Core(DomNode node, in Arguments a)
    {
        if (IsText(node) || IsComment(node))
            return new JSNumber(BridgeText(node).Length);
        return new JSNumber(node.ChildNodes.Count);
    }


    private JSValue JsJsObjectsSplitText048Core(DomNode node, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'splitText' on 'Text': 1 argument required, but only 0 present.");
        var offset = (int)a[0].DoubleValue;
        var text = BridgeText(node);
        if (offset < 0 || offset > text.Length)
            throw new JSException("Failed to execute 'splitText' on 'Text': The offset " + offset + " is larger than the node's length " + text.Length + ".");
        var remainingText = text[offset..];
        SetBridgeText(node, text[..offset]);
        var newNode = CreateBridgeTextNode(remainingText);
        // Insert new node as next sibling
        if (ParentEl(node) != null)
        {
            var idx = ChildIndexOf(ParentEl(node), node);
            SetParent(newNode, ParentEl(node));
            InsertChildAt(ParentEl(node), idx + 1, newNode);
        }

        // Invalidate cached JSObject so length/data properties reflect the update
        _jsObjects.Remove(node);
        return ToJSObject(newNode);
    }


    private JSValue JsJsObjectsSubstringData049Core(DomNode node, in Arguments a)
    {
        var offset = a.Length > 0 ? (int)a[0].DoubleValue : 0;
        var count = a.Length > 1 ? Math.Max(0, (int)a[1].DoubleValue) : 0;
        var text = BridgeText(node);
        if (offset < 0 || offset > text.Length)
            throw new JSException("INDEX_SIZE_ERR");
        var end = (int)Math.Min((long)offset + count, text.Length);
        return new JSString(text[offset..end]);
    }


    private JSValue JsJsObjectsAppendData050Core(DomBridge? bridge, DomNode node, in Arguments a)
    {
        var data = a.Length > 0 ? a[0].ToString() : string.Empty;
        bridge.SetCharacterData(node, BridgeText(node) + data);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsDeleteData051Core(DomBridge? bridge, DomNode node, in Arguments a)
    {
        var offset = a.Length > 0 ? (int)a[0].DoubleValue : 0;
        var count = a.Length > 1 ? Math.Max(0, (int)a[1].DoubleValue) : 0;
        var text = BridgeText(node);
        if (offset < 0 || offset > text.Length)
            throw new JSException("INDEX_SIZE_ERR");
        var end = (int)Math.Min((long)offset + count, text.Length);
        bridge.SetCharacterData(node, text.Remove(offset, end - offset));
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsInsertData052Core(DomBridge? bridge, DomNode node, in Arguments a)
    {
        var offset = a.Length > 0 ? (int)a[0].DoubleValue : 0;
        var data = a.Length > 1 ? a[1].ToString() : string.Empty;
        var text = BridgeText(node);
        if (offset < 0 || offset > text.Length)
            throw new JSException("INDEX_SIZE_ERR");
        bridge.SetCharacterData(node, text.Insert(offset, data));
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsReplaceData053Core(DomBridge? bridge, DomNode node, in Arguments a)
    {
        var offset = a.Length > 0 ? (int)a[0].DoubleValue : 0;
        var count = a.Length > 1 ? Math.Max(0, (int)a[1].DoubleValue) : 0;
        var data = a.Length > 2 ? a[2].ToString() : string.Empty;
        var text = BridgeText(node);
        if (offset < 0 || offset > text.Length)
            throw new JSException("INDEX_SIZE_ERR");
        var end = (int)Math.Min((long)offset + count, text.Length);
        bridge.SetCharacterData(node, text.Remove(offset, end - offset).Insert(offset, data));
        return JSUndefined.Value;
    }


    private static JSValue JsJsObjectsGetPublicId055Core(DomNode node, in Arguments _) =>
        new JSString(node is DomDocumentType dt ? dt.PublicId : string.Empty);


    private static JSValue JsJsObjectsGetSystemId056Core(DomNode node, in Arguments _) =>
        new JSString(node is DomDocumentType dt ? dt.SystemId : string.Empty);


    private JSValue JsJsObjectsGetOwnerDocument057Core(DomNode node, in Arguments a)
    {
        // For elements in sub-documents, return the sub-document JSObject
        if (GetElementRuntimeState(node).OwnerDocRoot != null && _jsObjects.TryGetDocument(GetElementRuntimeState(node).OwnerDocRoot, out var subDoc))
            return subDoc;
        // For main document elements, return the main document JSObject
        return _documentJSObject ?? JSNull.Value;
    }


    private JSValue JsJsObjectsGetParentElement058Core(DomNode node, in Arguments a)
    {
        var parent = ParentEl(node);
        if (parent == null)
            return JSNull.Value;
        if (IsText(parent))
            return JSNull.Value;
        return ToJSObject(parent);
    }


    private JSValue JsJsObjectsHasAttribute060Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSBoolean.False;
        return HasAttr(element, a[0].ToString()) ? JSBoolean.True : JSBoolean.False;
    }


    private JSValue JsJsObjectsRemoveAttribute063Core(DomBridge? bridgeForSet, DomElement element, in Arguments a)
    {
        if (a.Length > 0)
        {
            var attrName = a[0].ToString();
            TryGetAttribute(element, attrName, out var previousAttrVal);
            var removed = RemoveAttr(element, attrName);
            // Sync special properties
            if (string.Equals(attrName, "id", StringComparison.OrdinalIgnoreCase))
                element.Id = null;
            else if (string.Equals(attrName, "class", StringComparison.OrdinalIgnoreCase))
                element.ClassName = null;
            bridgeForSet.InvalidateStyleScope(element);
            if (removed)
                bridgeForSet.NotifyAttributeMutationObservers(element, attrName, previousAttrVal);
        }

        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsToggleAttribute064Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSBoolean.False;
        var attrName = a[0].ToString();
        var hasAttribute = HasAttr(element, attrName);
        var forceSpecified = a.Length > 1 && !a[1].IsUndefined;
        var shouldHaveAttribute = forceSpecified ? a[1].BooleanValue : !hasAttribute;
        if (shouldHaveAttribute)
        {
            if (!hasAttribute)
                _attributes.SetAttributeLikeSetAttribute(element, attrName, string.Empty);
            return JSBoolean.True;
        }

        if (hasAttribute)
            _attributes.RemoveAttributeLikeRemoveAttribute(element, attrName);
        return JSBoolean.False;
    }


    private JSValue JsJsObjectsSetAttributeNode065Core(DomElement element, JSObject? obj, in Arguments a)
    {
        if (a.Length == 0 || a[0] is not JSObject attrObj)
            return JSNull.Value;
        var name = _attributes.GetAttrNodeName(attrObj);
        if (string.IsNullOrEmpty(name))
            return JSNull.Value;
        var old = TryGetAttribute(element, name, out var oldVal) ? _attributes.BuildAttrNode(name, oldVal, element, obj) : JSNull.Value;
        _attributes.SetAttributeLikeSetAttribute(element, name, attrObj[(KeyString)"value"].ToString());
        return old;
    }


    private JSValue JsJsObjectsSetAttributeNodeNS066Core(DomElement element, JSObject? obj, in Arguments a)
    {
        if (a.Length == 0 || a[0] is not JSObject attrObj)
            return JSNull.Value;
        var name = _attributes.GetAttrNodeName(attrObj);
        var localName = _attributes.GetAttrNodeLocalName(attrObj);
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(localName))
            return JSNull.Value;
        var ns = _attributes.GetAttrNodeNamespace(attrObj);
        JSValue old = JSNull.Value;
        if (TryGetNsAttribute(element, ns, localName, out var oldQName, out var oldVal))
            old = _attributes.BuildAttrNode(oldQName, oldVal, element, obj);
        _attributes.SetAttributeLikeSetAttributeNS(element, ns, name, localName, attrObj[(KeyString)"value"].ToString());
        return old;
    }


    private JSValue JsJsObjectsRemoveAttributeNode067Core(DomElement element, JSObject? obj, in Arguments a)
    {
        if (a.Length == 0 || a[0] is not JSObject attrObj)
            return JSNull.Value;
        var name = _attributes.GetAttrNodeName(attrObj);
        if (string.IsNullOrEmpty(name) || !TryGetAttribute(element, name, out var val))
            return JSNull.Value;
        var removed = _attributes.BuildAttrNode(name, val, element, obj);
        _attributes.RemoveAttributeLikeRemoveAttribute(element, name);
        return removed;
    }


    private JSValue JsJsObjectsRemoveAttributeNodeNS068Core(DomElement element, JSObject? obj, in Arguments a)
    {
        if (a.Length == 0 || a[0] is not JSObject attrObj)
            return JSNull.Value;
        var localName = _attributes.GetAttrNodeLocalName(attrObj);
        if (string.IsNullOrEmpty(localName))
            return JSNull.Value;
        var ns = _attributes.GetAttrNodeNamespace(attrObj);
        if (!TryGetNsAttribute(element, ns, localName, out var qName, out var val))
            return JSNull.Value;
        var removed = _attributes.BuildAttrNode(qName, val, element, obj);
        _attributes.RemoveAttributeLikeRemoveAttributeNS(element, ns, localName);
        return removed;
    }


    private JSValue JsJsObjectsSetAttributeNS069Core(DomBridge? bridgeForSet, DomElement element, in Arguments a)
    {
        if (a.Length >= 3)
        {
            var ns = a[0].IsNull || a[0].IsUndefined ? null : a[0].ToString();
            var qName = a[1].ToString();
            var val = a[2].ToString();
            var localName = qName.Contains(':') ? qName[(qName.IndexOf(':') + 1)..] : qName;
            bridgeForSet._attributes.SetAttributeLikeSetAttributeNS(element, ns, qName, localName, val);
        }

        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetAttributeNS070Core(DomElement element, in Arguments a)
    {
        if (a.Length < 2)
            return JSNull.Value;
        var ns = a[0].IsNull || a[0].IsUndefined ? null : a[0].ToString();
        var localName = a[1].ToString();
        var val = element.GetAttributeNS(ns, localName);
        return val is not null ? new JSString(val) : JSNull.Value;
    }


    private JSValue JsJsObjectsRemoveAttributeNS071Core(DomBridge? bridgeForSet, DomElement element, in Arguments a)
    {
        if (a.Length >= 2)
        {
            var ns = a[0].IsNull || a[0].IsUndefined ? null : a[0].ToString();
            var localName = a[1].ToString();
            bridgeForSet._attributes.RemoveAttributeLikeRemoveAttributeNS(element, ns, localName);
        }

        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsHasAttributeNS072Core(DomElement element, in Arguments a)
    {
        if (a.Length < 2)
            return JSBoolean.False;
        var ns = a[0].IsNull || a[0].IsUndefined ? null : a[0].ToString();
        var localName = a[1].ToString();
        return element.GetAttributeNS(ns, localName) is not null ? JSBoolean.True : JSBoolean.False;
    }


    private JSValue JsJsObjectsContains073Core(DomNode node, in Arguments a)
    {
        if (a.Length == 0)
            return JSBoolean.False;
        if (a[0] is not JSObject otherObj)
            return JSBoolean.False;
        var other = FindDomNodeByJSObject(otherObj);
        if (other == null)
            return JSBoolean.False;
        if (ReferenceEquals(node, other))
            return JSBoolean.True;
        return IsDescendant(node, other) ? JSBoolean.True : JSBoolean.False;
    }


    private JSValue JsJsObjectsCompareDocumentPosition074Core(DomNode node, in Arguments a)
    {
        const int documentPositionDisconnected = 0x01;
        const int documentPositionPreceding = 0x02;
        const int documentPositionFollowing = 0x04;
        const int documentPositionContains = 0x08;
        const int documentPositionContainedBy = 0x10;
        if (a.Length == 0 || a[0] is not JSObject otherObj)
            return new JSNumber(0);
        var other = FindDomNodeByJSObject(otherObj);
        if (other == null || ReferenceEquals(node, other))
            return new JSNumber(0);
        if (!ReferenceEquals(GetTreeRoot(node), GetTreeRoot(other)))
            return new JSNumber(documentPositionDisconnected);
        if (IsDescendant(node, other))
            return new JSNumber(documentPositionFollowing | documentPositionContainedBy);
        if (IsDescendant(other, node))
            return new JSNumber(documentPositionPreceding | documentPositionContains);
        return new JSNumber(CompareTreeOrder(node, other) < 0 ? documentPositionFollowing : documentPositionPreceding);
    }


    private JSValue JsJsObjectsIsSameNode075Core(DomNode node, in Arguments a)
    {
        if (a.Length == 0 || a[0] is not JSObject otherObj)
            return JSBoolean.False;
        var other = FindDomNodeByJSObject(otherObj);
        return ReferenceEquals(node, other) ? JSBoolean.True : JSBoolean.False;
    }


    private JSValue JsJsObjectsNormalize076Core(DomNode node, in Arguments _)
    {
        // normalize() on a character-data node is a no-op (it has no text children to merge).
        if (node is DomElement element)
            NormalizeNode(element);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsIsEqualNode077Core(DomNode node, in Arguments a)
    {
        if (a.Length == 0 || a[0] is not JSObject otherObj)
            return JSBoolean.False;
        var other = FindDomNodeByJSObject(otherObj);
        return other != null && NodesAreEqual(node, other) ? JSBoolean.True : JSBoolean.False;
    }


    private JSValue JsJsObjectsGetRootNode078Core(DomNode node, in Arguments a)
    {
        var composed = false;
        if (a.Length > 0 && a[0] is JSObject options)
        {
            var composedValue = options[(KeyString)"composed"];
            composed = composedValue != null && !composedValue.IsUndefined && !composedValue.IsNull && composedValue.BooleanValue;
        }

        if (!composed)
        {
            var shadowRoot = FindContainingShadowRoot(node);
            if (shadowRoot != null)
                return ToJSObject(shadowRoot);
        }

        return ToJSRootNode(GetTreeRoot(node));
    }


    private JSValue JsJsObjectsCloneNode079Core(DomNode node, in Arguments a)
    {
        var deep = a.Length > 0 && a[0].BooleanValue;
        var clone = CloneDomElement(node, deep);
        return ToJSObject(clone);
    }


    private JSValue JsJsObjectsInsertBefore080Core(DomBridge? bridgeForInsert, DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        if (a[0] is not JSObject newChildObj)
            return JSUndefined.Value;
        var newEl = FindDomNodeByJSObject(newChildObj);
        if (newEl == null)
            return a[0];
        // Prevent circular references (HierarchyRequestError per DOM spec)
        if (ReferenceEquals(newEl, element) || IsDescendant(newEl, element))
            ThrowDOMException(_jsContext!, "The new child element contains the parent.", "HierarchyRequestError");
        if (a.Length < 2 || a[1].IsNull || a[1].IsUndefined)
        {
            bridgeForInsert.InsertNodeAt(element, newEl, element.ChildNodes.Count);
            return a[0];
        }

        if (a[1] is not JSObject refChildObj)
            return a[0];
        var refEl = FindDomNodeByJSObject(refChildObj);
        if (refEl == null)
            return a[0];
        if (ReferenceEquals(newEl, refEl))
            return a[0];
        var idx = ChildIndexOf(element, refEl);
        if (idx < 0)
            throw new JSException("NotFoundError: The node before which the new node is to be inserted is not a child of this node.");
        bridgeForInsert.InsertNodeAt(element, newEl, idx);
        return a[0];
    }


    private JSValue JsJsObjectsGetChildren081Core(DomElement element, in Arguments a)
    {
        var result = new List<JSValue>();
        foreach (var child in ChildElements(element))
        {
            if (!IsText(child) && !IsSubDocRoot(child))
                result.Add(ToJSObject(child));
        }

        return new JSArray(result);
    }


    private JSValue JsJsObjectsGetFirstElementChild083Core(DomElement element, in Arguments a)
    {
        var first = ChildElements(element).FirstOrDefault(c => !IsText(c) && !IsSubDocRoot(c));
        return first != null ? ToJSObject(first) : JSNull.Value;
    }


    private JSValue JsJsObjectsGetLastElementChild084Core(DomElement element, in Arguments a)
    {
        var last = ChildElements(element).LastOrDefault(c => !IsText(c) && !IsSubDocRoot(c));
        return last != null ? ToJSObject(last) : JSNull.Value;
    }


    private JSValue JsJsObjectsGetNextElementSibling085Core(DomElement element, in Arguments a)
    {
        if (ParentEl(element) == null)
            return JSNull.Value;
        var siblings = ChildElements(ParentEl(element)).ToList();
        var idx = siblings.IndexOf(element);
        for (var i = idx + 1; i < siblings.Count; i++)
        {
            if (!IsText(siblings[i]) && !IsSubDocRoot(siblings[i]))
                return ToJSObject(siblings[i]);
        }

        return JSNull.Value;
    }


    private JSValue JsJsObjectsGetPreviousElementSibling086Core(DomElement element, in Arguments a)
    {
        if (ParentEl(element) == null)
            return JSNull.Value;
        var siblings = ChildElements(ParentEl(element)).ToList();
        var idx = siblings.IndexOf(element);
        for (var i = idx - 1; i >= 0; i--)
        {
            if (!IsText(siblings[i]) && !IsSubDocRoot(siblings[i]))
                return ToJSObject(siblings[i]);
        }

        return JSNull.Value;
    }


    private JSValue JsJsObjectsAttachShadow087Core(DomElement element, in Arguments a)
    {
        if (GetShadowRoot(element) != null)
            ThrowDOMException(_jsContext!, "Shadow root already attached.", "NotSupportedError");
        var mode = "open";
        if (a.Length > 0 && a[0] is JSObject options)
        {
            var modeValue = options[(KeyString)"mode"];
            if (modeValue != null && !modeValue.IsUndefined && !modeValue.IsNull)
            {
                mode = modeValue.ToString();
            }
        }

        mode = string.Equals(mode, "closed", StringComparison.OrdinalIgnoreCase) ? "closed" : "open";
        var shadowRoot = CreateBridgeElement("#shadow-root");
        SetParent(shadowRoot, element);
        GetElementRuntimeState(shadowRoot).OwnerDocRoot = GetElementRuntimeState(element).OwnerDocRoot;
        GetElementRuntimeState(shadowRoot).Shadow.Host.Set(element);
        GetElementRuntimeState(shadowRoot).Shadow.Mode.Set(mode);
        GetElementRuntimeState(element).Shadow.Root.Set(shadowRoot);
        GetElementRuntimeState(element).Shadow.Mode.Set(mode);
        return ToJSObject(shadowRoot);
    }


    private JSValue JsJsObjectsAppendChild088Core(DomBridge? bridgeForAppend, DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        if (a[0] is not JSObject childObj)
            return JSUndefined.Value;
        // Find the Broiler.Dom.DomElement for this child JSObject
        var childEl = FindDomNodeByJSObject(childObj);
        if (childEl == null)
            return a[0];
        // Prevent circular references (HierarchyRequestError per DOM spec)
        if (ReferenceEquals(childEl, element) || IsDescendant(childEl, element))
            ThrowDOMException(_jsContext!, "The new child element contains the parent.", "HierarchyRequestError");
        bridgeForAppend.InsertNodeAt(element, childEl, element.ChildNodes.Count);
        return a[0];
    }


    private JSValue JsJsObjectsAppend089Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var nodes = BuildChildNodeArgumentNodes(a);
        var insertIndex = element.ChildNodes.Count;
        foreach (var node in nodes)
            InsertNodeAt(element, node, insertIndex++);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsPrepend090Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var nodes = BuildChildNodeArgumentNodes(a);
        var insertIndex = 0;
        foreach (var node in nodes)
            InsertNodeAt(element, node, insertIndex++);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsRemoveChild091Core(DomBridge? bridgeForAppend, DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        if (a[0] is not JSObject childObj)
            return JSUndefined.Value;
        var childEl = FindDomNodeByJSObject(childObj);
        if (childEl == null)
            return a[0];
        var idx = ChildIndexOf(element, childEl);
        if (idx < 0)
            return a[0];
        NotifyNodeIteratorPreRemoval(childEl);
        RemoveNthChild(element, idx);
        SetParent(childEl, null);
        bridgeForAppend.InvalidateStyleScope(element);
        NotifyChildRemoved(element, childEl, idx);
        return a[0];
    }


    private JSValue JsJsObjectsReplaceChild092Core(DomBridge? bridgeForAppend, DomElement element, in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        if (a[0] is not JSObject newChildObj || a[1] is not JSObject oldChildObj)
            return JSUndefined.Value;
        var newEl = FindDomNodeByJSObject(newChildObj);
        var oldEl = FindDomNodeByJSObject(oldChildObj);
        if (newEl == null || oldEl == null)
            return a[1];
        // Prevent circular references (HierarchyRequestError per DOM spec)
        if (ReferenceEquals(newEl, element) || IsDescendant(newEl, element))
            ThrowDOMException(_jsContext!, "The new child element contains the parent.", "HierarchyRequestError");
        var idx = ChildIndexOf(element, oldEl);
        if (idx < 0)
            return a[1];
        var previousSibling = idx > 0 ? ChildAt(element, idx - 1) : null;
        var nextSibling = idx + 1 < element.ChildNodes.Count ? ChildAt(element, idx + 1) : null;
        // If newChild is already in this parent, remove it first and re-find idx
        if (ReferenceEquals(ParentEl(newEl), element))
        {
            RemoveChildFrom(element, newEl);
            idx = ChildIndexOf(element, oldEl);
            if (idx < 0)
                return a[1];
        }
        else
        {
            if (ParentEl(newEl) != null)
            {
                var oldParent = ParentEl(newEl);
                var oldIndex = ChildIndexOf(oldParent, newEl);
                if (oldIndex >= 0)
                {
                    NotifyNodeIteratorPreRemoval(newEl);
                    RemoveNthChild(oldParent, oldIndex);
                    NotifyChildRemoved(oldParent, newEl, oldIndex);
                }
            }
        }

        SetParent(oldEl, null);
        SetParent(newEl, element);
        AdoptSubtreeIntoDocument(newEl, GetElementRuntimeState(element).OwnerDocRoot);
        element.ReplaceChild(newEl, element.ChildNodes[idx]);
        bridgeForAppend.InvalidateStyleScope(element);
        NotifyChildRemoved(element, oldEl, idx, previousSibling, nextSibling);
        NotifyChildAdded(element, newEl, idx);
        return a[1]; // returns the old child
    }


    private JSValue JsJsObjectsRemove093Core(DomNode element, in Arguments a)
    {
        // Capture the parent up front: ParentEl(node) is computed from the canonical
        // ParentNode, and Children.RemoveAt detaches it — so reading ParentEl(element) after
        // the removal would return null (→ NRE in InvalidateStyleScope). Mirrors the
        // working removeChild path, which holds the parent reference independently.
        var parent = ParentEl(element);
        if (parent != null)
        {
            var idx = ChildIndexOf(parent, element);
            if (idx >= 0)
            {
                NotifyNodeIteratorPreRemoval(element);
                RemoveNthChild(parent, idx);
                SetParent(element, null);
                InvalidateStyleScope(parent);
                NotifyChildRemoved(parent, element, idx);
            }
        }

        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsBefore094Core(DomNode element, in Arguments a)
    {
        if (ParentEl(element) == null || a.Length == 0)
            return JSUndefined.Value;
        var nodes = BuildChildNodeArgumentNodes(a);
        var insertIndex = ChildIndexOf(ParentEl(element), element);
        if (insertIndex < 0)
            return JSUndefined.Value;
        foreach (var node in nodes)
            InsertNodeAt(ParentEl(element), node, insertIndex++);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsAfter095Core(DomNode element, in Arguments a)
    {
        if (ParentEl(element) == null || a.Length == 0)
            return JSUndefined.Value;
        var nodes = BuildChildNodeArgumentNodes(a);
        var insertIndex = ChildIndexOf(ParentEl(element), element);
        if (insertIndex < 0)
            return JSUndefined.Value;
        insertIndex++;
        foreach (var node in nodes)
            InsertNodeAt(ParentEl(element), node, insertIndex++);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsReplaceWith096Core(DomNode element, in Arguments a)
    {
        if (ParentEl(element) == null)
            return JSUndefined.Value;
        var parent = ParentEl(element);
        var replacementIndex = ChildIndexOf(parent, element);
        if (replacementIndex < 0)
            return JSUndefined.Value;
        var nodes = BuildChildNodeArgumentNodes(a);
        NotifyNodeIteratorPreRemoval(element);
        RemoveNthChild(parent, replacementIndex);
        SetParent(element, null);
        InvalidateStyleScope(parent);
        NotifyChildRemoved(parent, element, replacementIndex);
        foreach (var node in nodes)
            InsertNodeAt(parent, node, replacementIndex++);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsAddEventListener097Core(DomNode element, in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        var type = a[0].ToString();
        if (!GetEventListeners(element).TryGetValue(type, out var listeners))
        {
            listeners = [];
            GetEventListeners(element)[type] = listeners;
        }

        Dom.Features.EventListenerBinding.AddListener(listeners, a[1], a.Length > 2 ? a[2] : JSUndefined.Value);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsRemoveEventListener098Core(DomNode element, in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        var type = a[0].ToString();
        Dom.Features.EventListenerBinding.RemoveListener(
            GetEventListeners(element).TryGetValue(type, out var listeners) ? listeners : null,
            a[1], a.Length > 2 ? a[2] : JSUndefined.Value);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsDispatchEvent099Core(DomBridge? bridge, DomNode element, in Arguments a)
    {
        if (a.Length == 0)
            return JSBoolean.True;
        if (a[0] is not JSObject evt)
            return JSBoolean.True;
        return bridge.DispatchEventOnElement(element, evt);
    }


    private JSValue JsJsObjectsClick101Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        // Toggle checked state for checkboxes/radio buttons (per HTML spec)
        if (string.Equals(element.TagName, "input", StringComparison.OrdinalIgnoreCase))
        {
            var inputType = TryGetAttribute(element, "type", out var t) ? t.ToLowerInvariant() : "text";
            if (inputType == "checkbox")
            {
                bool wasChecked = GetElementRuntimeState(element).FormControl.Checked.TryGet(out var cv) && cv is true || (!GetElementRuntimeState(element).FormControl.Checked.IsSet && HasAttr(element, "checked"));
                GetElementRuntimeState(element).FormControl.Checked.Set(!wasChecked);
            }
            else if (inputType == "radio")
            {
                GetElementRuntimeState(element).FormControl.Checked.Set(true);
                // Radio mutual exclusion
                if (TryGetAttribute(element, "name", out var radioName) && !string.IsNullOrEmpty(radioName))
                {
                    var scope = element;
                    while (ParentEl(scope) != null)
                        scope = ParentEl(scope);
                    UncheckRadioSiblings(scope, element, radioName);
                }
            }
        }

        var evt = new JSObject();
        evt.FastAddValue((KeyString)"type", new JSString("click"), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"bubbles", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"cancelable", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"target", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"currentTarget", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"eventPhase", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"detail", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"stopPropagation", UndefinedFunction("stopPropagation", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"stopImmediatePropagation", UndefinedFunction("stopImmediatePropagation", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"preventDefault", UndefinedFunction("preventDefault", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        bridge.DispatchEventOnElement(element, evt);
        // Per HTML spec: clicking a submit button triggers form submission
        if (string.Equals(element.TagName, "input", StringComparison.OrdinalIgnoreCase) || string.Equals(element.TagName, "button", StringComparison.OrdinalIgnoreCase))
        {
            var btnType = "text";
            if (TryGetAttribute(element, "type", out var bt))
                btnType = bt.ToLowerInvariant();
            else if (string.Equals(element.TagName, "button", StringComparison.OrdinalIgnoreCase))
                btnType = "submit"; // <button> defaults to type="submit" per HTML spec
            if (btnType == "submit")
            {
                // Walk up the DOM tree to find the parent <form>
                var form = ParentEl(element);
                while (form != null && !string.Equals(form.TagName, "form", StringComparison.OrdinalIgnoreCase))
                    form = ParentEl(form);
                if (form != null)
                {
                    // Dispatch a submit event on the form
                    var submitEvt = new JSObject();
                    submitEvt.FastAddValue((KeyString)"type", new JSString("submit"), JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"bubbles", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"cancelable", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"target", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"currentTarget", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"eventPhase", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
                    JSValue JsJsObjectsPreventDefault100(in Arguments __)
                    {
                        submitEvt[(KeyString)"defaultPrevented"] = JSBoolean.True;
                        return JSUndefined.Value;
                    }

                    submitEvt.FastAddValue((KeyString)"preventDefault", new JSFunction(JsJsObjectsPreventDefault100, "preventDefault", 0), JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"stopPropagation", UndefinedFunction("stopPropagation", 0), JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"stopImmediatePropagation", UndefinedFunction("stopImmediatePropagation", 0), JSPropertyAttributes.EnumerableConfigurableValue);
                    bridge.DispatchEventOnElement(form, submitEvt);
                }
            }
        }

        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsFocus102Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        var evt = new JSObject();
        evt.FastAddValue((KeyString)"type", new JSString("focus"), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"bubbles", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"cancelable", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"target", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"currentTarget", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"srcElement", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"eventPhase", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"isTrusted", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"timeStamp", new JSNumber(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"detail", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"view", _windowJSObject ?? JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"relatedTarget", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        bridge.DispatchEventOnElement(element, evt);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsBlur103Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        var evt = new JSObject();
        evt.FastAddValue((KeyString)"type", new JSString("blur"), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"bubbles", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"cancelable", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"target", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"currentTarget", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"srcElement", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"eventPhase", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"isTrusted", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"timeStamp", new JSNumber(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"detail", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"view", _windowJSObject ?? JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"relatedTarget", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        bridge.DispatchEventOnElement(element, evt);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsCallback104Core(DomElement element, global::System.String? eventName, in Arguments _)
    {
        if (GetInlineEventHandlers(element).TryGetValue(eventName, out var handler))
            return handler;
        return JSNull.Value;
    }


    private JSValue JsJsObjectsCallback105Core(DomElement element, global::System.String? eventName, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSFunction fn)
            GetInlineEventHandlers(element)[eventName] = fn;
        else
            GetInlineEventHandlers(element).Remove(eventName);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetValue106Core(DomElement element, in Arguments a)
    {
        if (string.Equals(element.TagName, "select", StringComparison.OrdinalIgnoreCase))
            return new JSString(_select.GetValue(element));
        if (GetElementRuntimeState(element).FormControl.Value.TryGet(out var domVal) && domVal is string sv)
            return new JSString(sv);
        if (TryGetAttribute(element, "value", out var val))
            return new JSString(val);
        return new JSString(string.Empty);
    }


    private JSValue JsJsObjectsSetValue107Core(DomElement element, in Arguments a)
    {
        var tag = element.TagName.ToLowerInvariant();
        var v = a.Length > 0 ? a[0].ToString() : string.Empty;
        if (tag == "input")
            GetElementRuntimeState(element).FormControl.Value.Set(v); // IDL value, not reflected
        else if (tag == "select")
            _select.SetValue(element, v);
        else
            SetAttr(element, "value", v);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetChecked108Core(DomElement element, in Arguments a)
    {
        // IDL property takes precedence over content attribute
        if (GetElementRuntimeState(element).FormControl.Checked.TryGet(out var v))
            return v is true ? JSBoolean.True : JSBoolean.False;
        return HasAttr(element, "checked") ? JSBoolean.True : JSBoolean.False;
    }


    private JSValue JsJsObjectsSetChecked109Core(DomElement element, in Arguments a)
    {
        bool newVal = a.Length > 0 && a[0].BooleanValue;
        GetElementRuntimeState(element).FormControl.Checked.Set(newVal);
        if (newVal)
        {
            // Radio button mutual exclusion: uncheck others in same group
            if (TryGetAttribute(element, "type", out var tp) && string.Equals(tp, "radio", StringComparison.OrdinalIgnoreCase) && TryGetAttribute(element, "name", out var radioName) && !string.IsNullOrEmpty(radioName))
            {
                // Find the scope for radio group — form parent, or document root if not in a form
                var scope = ParentEl(element);
                while (scope != null && !string.Equals(scope.TagName, "form", StringComparison.OrdinalIgnoreCase))
                    scope = ParentEl(scope);
                if (scope == null)
                {
                    scope = element;
                    while (ParentEl(scope) != null)
                        scope = ParentEl(scope);
                }

                UncheckRadioSiblings(scope, element, radioName);
            }
        }

        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetType110Core(DomElement element, in Arguments a)
    {
        if (TryGetAttribute(element, "type", out var t))
            return new JSString(t.ToLowerInvariant());
        // Default type values per HTML spec
        var tag = element.TagName.ToLowerInvariant();
        if (tag == "button")
            return new JSString("submit");
        return new JSString(string.Empty);
    }


    private JSValue JsJsObjectsSetType111Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "type", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetName112Core(DomElement element, in Arguments a)
    {
        if (TryGetAttribute(element, "name", out var n))
            return new JSString(n);
        return new JSString(string.Empty);
    }


    private JSValue JsJsObjectsSetName113Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "name", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetDisabled115Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0].BooleanValue)
            SetAttr(element, "disabled", "disabled");
        else
            RemoveAttr(element, "disabled");
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetHidden117Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0].BooleanValue)
            SetAttr(element, "hidden", string.Empty);
        else
            RemoveAttr(element, "hidden");
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetTabIndex118Core(DomElement element, in Arguments _)
    {
        if (TryGetAttribute(element, "tabindex", out var rawTabIndex) && int.TryParse(rawTabIndex, out var parsedTabIndex))
        {
            return new JSNumber(parsedTabIndex);
        }

        return new JSNumber(-1);
    }


    private JSValue JsJsObjectsSetTabIndex119Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var tabIndex = (int)Math.Truncate(a[0].DoubleValue);
        SetAttr(element, "tabindex", tabIndex.ToString());
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetRequired121Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0].BooleanValue)
            SetAttr(element, "required", "required");
        else
            RemoveAttr(element, "required");
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSubmit125Core(DomElement element, JSObject? obj, in Arguments a)
    {
        if (string.Equals(element.TagName, "form", StringComparison.OrdinalIgnoreCase))
        {
            // Fire submit event
            var submitEvt = new JSObject();
            submitEvt.FastAddValue((KeyString)"type", new JSString("submit"), JSPropertyAttributes.EnumerableConfigurableValue);
            submitEvt.FastAddValue((KeyString)"target", obj, JSPropertyAttributes.EnumerableConfigurableValue);
            submitEvt.FastAddValue((KeyString)"bubbles", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
            submitEvt.FastAddValue((KeyString)"cancelable", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
            var prevented = false;
            submitEvt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsJsObjectsPreventDefault124(in Arguments _)
            {
                prevented = true;
                submitEvt[(KeyString)"defaultPrevented"] = JSBoolean.True;
                return JSUndefined.Value;
            }

            submitEvt.FastAddValue((KeyString)"preventDefault", new JSFunction(JsJsObjectsPreventDefault124, "preventDefault", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            submitEvt.FastAddValue((KeyString)"stopPropagation", UndefinedFunction("stopPropagation", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            if (GetEventListeners(element).TryGetValue("submit", out var submitListeners))
            {
                foreach (var registration in submitListeners.ToList())
                {
                    InvokeEventListener(registration.Listener, submitEvt, "DomBridge.submit");
                }
            }

            // If preventDefault was called, do not proceed with default action
            if (prevented)
            {
                RenderLogger.LogDebug(LogCategory.JavaScript, "DomBridge.submit", "Default action prevented");
            }
        }

        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsQuerySelector126Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        var sel = a.Length > 0 ? a[0].ToString() : string.Empty;
        return FindInDescendants(element, sel, false, bridge);
    }


    private JSValue JsJsObjectsQuerySelectorAll127Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        var sel = a.Length > 0 ? a[0].ToString() : string.Empty;
        return FindInDescendants(element, sel, true, bridge);
    }


    private JSValue JsJsObjectsMatches128Core(DomElement element, in Arguments a)
    {
        var sel = a.Length > 0 ? a[0].ToString() : string.Empty;
        return MatchesSelector(element, sel, element) ? JSBoolean.True : JSBoolean.False;
    }


    private JSValue JsJsObjectsClosest129Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        var sel = a.Length > 0 ? a[0].ToString() : string.Empty;
        for (DomElement? current = element; current != null && !current.TagName.StartsWith('#'); current = ParentEl(current))
        {
            if (MatchesSelector(current, sel, element))
                return bridge.ToJSObject(current);
        }

        return JSNull.Value;
    }


    private JSValue JsJsObjectsInsertAdjacentElement130Core(DomElement element, in Arguments a)
    {
        if (a.Length < 2)
            return JSNull.Value;
        var position = NormalizeInsertAdjacentPosition(a[0]);
        if (a[1] is not JSObject adjacentObject)
            return JSNull.Value;
        var adjacentElement = FindDomElementByJSObject(adjacentObject);
        if (adjacentElement == null)
            return JSNull.Value;
        var (parent, index) = GetInsertAdjacentTarget(element, position);
        InsertNodeAt(parent, adjacentElement, index);
        return a[1];
    }


    private JSValue JsJsObjectsInsertAdjacentText131Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var position = NormalizeInsertAdjacentPosition(a[0]);
        var text = a.Length > 1 ? a[1].ToString() : string.Empty;
        var (parent, index) = GetInsertAdjacentTarget(element, position);
        var textNode = CreateBridgeTextNode(text);
        InsertNodeAt(parent, textNode, index);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsInsertAdjacentHTML132Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var position = NormalizeInsertAdjacentPosition(a[0]);
        var html = a.Length > 1 ? a[1].ToString() : string.Empty;
        if (string.IsNullOrEmpty(html))
            return JSUndefined.Value;
        DomElement parsingContext;
        switch (position)
        {
            case "beforebegin":
            case "afterend":
                if (ParentEl(element) == null)
                    ThrowDOMException(_jsContext!, "Cannot insert adjacent HTML without a parent node.", "NoModificationAllowedError");
                parsingContext = ParentEl(element)!;
                break;
            default:
                parsingContext = element;
                break;
        }

        var (parent, index) = GetInsertAdjacentTarget(element, position);
        var nodes = BuildAdjacentHtmlNodes(parsingContext, html);
        foreach (var node in nodes)
            InsertNodeAt(parent, node, index++);
        ResetComputedStyleEngines();
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetElementsByTagName133Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        var tagSearch = a.Length > 0 ? a[0].ToString().ToLowerInvariant() : string.Empty;
        var results = new List<JSValue>();
        CollectDescendantsByTag(element, tagSearch, results, bridge);
        return new JSArray(results);
    }


    private JSValue JsJsObjectsGetContext134Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var contextType = a[0].ToString();
        if (!string.Equals(contextType, "2d", StringComparison.OrdinalIgnoreCase))
            return JSNull.Value;
        if (!string.Equals(element.TagName, "canvas", StringComparison.OrdinalIgnoreCase))
            return JSNull.Value;
#if BROILER_CLI
                            return JSNull.Value; // Canvas 2D context not available in CLI mode
#else
        return BuildCanvas2DContext(element);
#endif
    }


    private JSValue JsJsObjectsGetContentDocument135Core(DomElement element, in Arguments _)
    {
        // Cross-origin iframes return null for contentDocument (same-origin policy)
        if (IsCurrentIframeCrossOrigin(element))
            return JSNull.Value;
        // Non-HTML resources get a minimal empty sub-document (no parsed fallback content)
        return GetOrCreateSubDocument(element);
    }


    private JSValue JsJsObjectsGetContentWindow136Core(DomElement element, in Arguments _)
    {
        if (IsCurrentIframeCrossOrigin(element))
            return JSNull.Value;
        return GetOrCreateSubWindow(element);
    }


    private JSValue JsJsObjectsGetSVGDocument137Core(DomElement element, in Arguments _)
    {
        if (IsCurrentIframeCrossOrigin(element))
            return JSNull.Value;
        return GetOrCreateSubDocument(element);
    }


    private JSValue JsJsObjectsSetSrc139Core(DomBridge? bridgeForSrc, DomElement element, in Arguments a)
    {
        SetAttr(element, "src", a.Length > 0 ? a[0].ToString() : string.Empty);
        // Invalidate cached sub-document when src changes
        InvalidateCachedSubDocument(element);
        _onloadFired.Remove(element);
        // Fire onload for the new resource
        bridgeForSrc.FireSubDocumentOnload(element);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetSrcdoc141Core(DomBridge? bridgeForSrc, DomElement element, in Arguments a)
    {
        SetAttr(element, "srcdoc", a.Length > 0 ? a[0].ToString() : string.Empty);
        InvalidateCachedSubDocument(element);
        _onloadFired.Remove(element);
        bridgeForSrc.FireSubDocumentOnload(element);
        return JSUndefined.Value;
    }

}
