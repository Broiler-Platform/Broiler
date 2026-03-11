using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using YantraJS.Core;

namespace Broiler.App.Rendering;

/// <summary>
/// Conversion of <see cref="DomElement"/> instances to YantraJS
/// <see cref="JSObject"/> representations, including sub-document
/// construction and tree-search helpers.
/// </summary>
public sealed partial class DomBridge
{
    private readonly Dictionary<DomElement, JSObject> _jsObjectCache = [];

    internal JSObject ToJSObject(DomElement element)
    {
        if (_jsObjectCache.TryGetValue(element, out var cached))
            return cached;

        var obj = new JSObject();
        _jsObjectCache[element] = obj;

        obj.FastAddValue(
            (KeyString)"tagName",
            new JSString(element.TagName.ToUpperInvariant()),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddProperty(
            (KeyString)"id",
            new JSFunction((in Arguments a) =>
                element.Id != null ? (JSValue)new JSString(element.Id) : JSNull.Value,
                "get id"),
            new JSFunction((in Arguments a) =>
            {
                var val = a.Length > 0 ? a[0].ToString() : string.Empty;
                element.Id = val;
                element.Attributes["id"] = val;
                return JSUndefined.Value;
            }, "set id"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // className (read/write) — reflects the 'class' content attribute
        obj.FastAddProperty(
            (KeyString)"className",
            new JSFunction((in Arguments a) =>
            {
                // Prefer Attributes['class'] (synced by setAttribute and className setter).
                // Fall back to element.ClassName for elements created with a class in the constructor
                // but not yet synced to Attributes (e.g. parsed HTML elements).
                if (element.Attributes.TryGetValue("class", out var cls))
                    return new JSString(cls);
                return element.ClassName != null ? (JSValue)new JSString(element.ClassName) : new JSString(string.Empty);
            }, "get className"),
            new JSFunction((in Arguments a) =>
            {
                var val = a.Length > 0 ? a[0].ToString() : string.Empty;
                element.ClassName = val;
                element.Attributes["class"] = val;
                return JSUndefined.Value;
            }, "set className"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // title (read/write) — synced with attributes["title"]
        obj.FastAddProperty(
            (KeyString)"title",
            new JSFunction((in Arguments a) =>
                element.Attributes.TryGetValue("title", out var t)
                    ? (JSValue)new JSString(t)
                    : new JSString(string.Empty),
                "get title"),
            new JSFunction((in Arguments a) =>
            {
                element.Attributes["title"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                return JSUndefined.Value;
            }, "set title"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // innerHTML (read/write)
        obj.FastAddProperty(
            (KeyString)"innerHTML",
            new JSFunction((in Arguments a) => new JSString(element.InnerHtml), "get innerHTML"),
            new JSFunction((in Arguments a) =>
            {
                element.InnerHtml = a.Length > 0 ? a[0].ToString() : string.Empty;
                return JSUndefined.Value;
            }, "set innerHTML"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // textContent (read/write)
        obj.FastAddProperty(
            (KeyString)"textContent",
            new JSFunction((in Arguments a) =>
            {
                // For text nodes, return the direct text content
                if (element.IsTextNode)
                    return element.TextContent != null ? (JSValue)new JSString(element.TextContent) : new JSString(string.Empty);
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
            }, "get textContent"),
            new JSFunction((in Arguments a) =>
            {
                var text = a.Length > 0 ? a[0].ToString() : string.Empty;
                element.TextContent = text;
                // Setting textContent clears all children per DOM spec
                element.Children.Clear();
                return JSUndefined.Value;
            }, "set textContent"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // style object — CSS property access and manipulation
        obj.FastAddValue(
            (KeyString)"style",
            BuildStyleObject(element),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // classList — class list manipulation
        obj.FastAddValue(
            (KeyString)"classList",
            BuildClassListObject(element),
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
            new JSFunction((in Arguments a) =>
            {
                if (a.Length >= 2)
                {
                    var attrName = a[0].ToString();
                    var attrVal = a[1].ToString();
                    element.Attributes[attrName] = attrVal;
                    // Sync special properties
                    if (string.Equals(attrName, "id", StringComparison.OrdinalIgnoreCase))
                        element.Id = attrVal;
                    else if (string.Equals(attrName, "class", StringComparison.OrdinalIgnoreCase))
                        element.ClassName = attrVal;
                    else if (string.Equals(attrName, "style", StringComparison.OrdinalIgnoreCase))
                    {
                        element.Style.Clear();
                        foreach (var kv in ParseStyle(attrVal))
                            element.Style[kv.Key] = kv.Value;
                    }
                    // Compile on* event handler attributes into functions
                    else if (attrName.Length > 2 && attrName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                    {
                        bridgeForSet.CompileInlineEventAttribute(element, attrName, attrVal);
                    }
                }
                return JSUndefined.Value;
            }, "setAttribute", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getAttribute(name)
        obj.FastAddValue(
            (KeyString)"getAttribute",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var name = a[0].ToString();
                return element.Attributes.TryGetValue(name, out var val)
                    ? (JSValue)new JSString(val)
                    : JSNull.Value;
            }, "getAttribute", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- DOM tree navigation --

        // parentNode (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"parentNode",
            new JSFunction((in Arguments a) =>
                element.Parent != null ? (JSValue)ToJSObject(element.Parent) : JSNull.Value,
                "get parentNode"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // childNodes (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"childNodes",
            new JSFunction((in Arguments a) =>
            {
                var children = new List<JSValue>();
                foreach (var child in element.Children)
                    children.Add(ToJSObject(child));
                return new JSArray(children);
            }, "get childNodes"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // firstChild (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"firstChild",
            new JSFunction((in Arguments a) =>
                element.Children.Count > 0 ? (JSValue)ToJSObject(element.Children[0]) : JSNull.Value,
                "get firstChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastChild (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"lastChild",
            new JSFunction((in Arguments a) =>
                element.Children.Count > 0 ? (JSValue)ToJSObject(element.Children[^1]) : JSNull.Value,
                "get lastChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextSibling (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"nextSibling",
            new JSFunction((in Arguments a) =>
            {
                if (element.Parent == null) return JSNull.Value;
                var siblings = element.Parent.Children;
                var idx = siblings.IndexOf(element);
                return idx >= 0 && idx + 1 < siblings.Count
                    ? (JSValue)ToJSObject(siblings[idx + 1])
                    : JSNull.Value;
            }, "get nextSibling"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // previousSibling (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"previousSibling",
            new JSFunction((in Arguments a) =>
            {
                if (element.Parent == null) return JSNull.Value;
                var siblings = element.Parent.Children;
                var idx = siblings.IndexOf(element);
                return idx > 0
                    ? (JSValue)ToJSObject(siblings[idx - 1])
                    : JSNull.Value;
            }, "get previousSibling"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeType (read-only)
        obj.FastAddProperty(
            (KeyString)"nodeType",
            new JSFunction((in Arguments a) =>
            {
                if (element.IsTextNode) return new JSNumber(3); // TEXT_NODE
                if (string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase)) return new JSNumber(8); // COMMENT_NODE
                if (string.Equals(element.TagName, "#document", StringComparison.OrdinalIgnoreCase)) return new JSNumber(9); // DOCUMENT_NODE
                if (string.Equals(element.TagName, "#document-fragment", StringComparison.OrdinalIgnoreCase)) return new JSNumber(11); // DOCUMENT_FRAGMENT_NODE
                if (string.Equals(element.TagName, "#doctype", StringComparison.OrdinalIgnoreCase)) return new JSNumber(10); // DOCUMENT_TYPE_NODE
                return new JSNumber(1); // ELEMENT_NODE
            }, "get nodeType"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeName (read-only)
        obj.FastAddProperty(
            (KeyString)"nodeName",
            new JSFunction((in Arguments a) =>
            {
                if (element.IsTextNode) return new JSString("#text");
                if (string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase)) return new JSString("#comment");
                if (string.Equals(element.TagName, "#document", StringComparison.OrdinalIgnoreCase)) return new JSString("#document");
                if (string.Equals(element.TagName, "#document-fragment", StringComparison.OrdinalIgnoreCase)) return new JSString("#document-fragment");
                if (string.Equals(element.TagName, "#doctype", StringComparison.OrdinalIgnoreCase))
                {
                    return new JSString(GetDocTypeName(element));
                }
                return new JSString(element.TagName.ToUpperInvariant());
            }, "get nodeName"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // localName (read-only) — null for non-element nodes; tag name (lowercase) for elements
        obj.FastAddProperty(
            (KeyString)"localName",
            new JSFunction((in Arguments a) =>
            {
                if (element.IsTextNode) return JSNull.Value;
                if (element.TagName.StartsWith("#")) return JSNull.Value; // #comment, #document, etc.
                return new JSString(element.TagName.ToLowerInvariant());
            }, "get localName"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // namespaceURI (read-only) — returns namespace URI for elements created via createElementNS
        obj.FastAddProperty(
            (KeyString)"namespaceURI",
            new JSFunction((in Arguments a) =>
            {
                if (element.NamespaceURI != null)
                    return new JSString(element.NamespaceURI);
                // Default namespace for HTML elements
                if (!element.IsTextNode && !element.TagName.StartsWith("#"))
                    return new JSString("http://www.w3.org/1999/xhtml");
                return JSNull.Value;
            }, "get namespaceURI"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeValue (read/write) — null for elements, text content for text/comment nodes
        obj.FastAddProperty(
            (KeyString)"nodeValue",
            new JSFunction((in Arguments a) =>
            {
                if (element.IsTextNode || string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                    return element.TextContent != null ? (JSValue)new JSString(element.TextContent) : JSNull.Value;
                return JSNull.Value;
            }, "get nodeValue"),
            new JSFunction((in Arguments a) =>
            {
                if (element.IsTextNode || string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                    element.TextContent = a.Length > 0 ? a[0].ToString() : string.Empty;
                return JSUndefined.Value;
            }, "set nodeValue"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // data (read/write) — for text nodes and comment nodes (alias for nodeValue/textContent)
        obj.FastAddProperty(
            (KeyString)"data",
            new JSFunction((in Arguments a) =>
            {
                if (element.IsTextNode || string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                    return element.TextContent != null ? (JSValue)new JSString(element.TextContent) : new JSString(string.Empty);
                return JSUndefined.Value;
            }, "get data"),
            new JSFunction((in Arguments a) =>
            {
                if (element.IsTextNode || string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                    element.TextContent = a.Length > 0 ? a[0].ToString() : string.Empty;
                return JSUndefined.Value;
            }, "set data"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // length (read-only) — character count for text/comment nodes, child count for elements
        obj.FastAddProperty(
            (KeyString)"length",
            new JSFunction((in Arguments a) =>
            {
                if (element.IsTextNode || string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                    return new JSNumber((element.TextContent ?? string.Empty).Length);
                return new JSNumber(element.Children.Count);
            }, "get length"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // splitText(offset) — splits a text node at the given character offset
        if (element.IsTextNode)
        {
            obj.FastAddValue(
                (KeyString)"splitText",
                new JSFunction((in Arguments a) =>
                {
                    if (a.Length == 0) throw new JSException("Failed to execute 'splitText' on 'Text': 1 argument required, but only 0 present.");
                    var offset = (int)a[0].DoubleValue;
                    var text = element.TextContent ?? string.Empty;
                    if (offset < 0 || offset > text.Length)
                        throw new JSException("Failed to execute 'splitText' on 'Text': The offset " + offset + " is larger than the node's length " + text.Length + ".");

                    var remainingText = text.Substring(offset);
                    element.TextContent = text.Substring(0, offset);

                    var newNode = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                    newNode.TextContent = remainingText;
                    _elements.Add(newNode);

                    // Insert new node as next sibling
                    if (element.Parent != null)
                    {
                        var idx = element.Parent.Children.IndexOf(element);
                        newNode.Parent = element.Parent;
                        element.Parent.Children.Insert(idx + 1, newNode);
                    }

                    // Invalidate cached JSObject so length/data properties reflect the update
                    _jsObjectCache.Remove(element);

                    return ToJSObject(newNode);
                }, "splitText", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // substringData(offset, count) — for text/comment CharacterData nodes
        if (element.IsTextNode || string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
        {
            obj.FastAddValue(
                (KeyString)"substringData",
                new JSFunction((in Arguments a) =>
                {
                    var offset = a.Length > 0 ? (int)a[0].DoubleValue : 0;
                    var count = a.Length > 1 ? Math.Max(0, (int)a[1].DoubleValue) : 0;
                    var text = element.TextContent ?? string.Empty;
                    if (offset < 0 || offset > text.Length) throw new JSException("INDEX_SIZE_ERR");
                    var end = (int)Math.Min((long)offset + count, text.Length);
                    return new JSString(text.Substring(offset, end - offset));
                }, "substringData", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue(
                (KeyString)"appendData",
                new JSFunction((in Arguments a) =>
                {
                    var data = a.Length > 0 ? a[0].ToString() : string.Empty;
                    element.TextContent = (element.TextContent ?? string.Empty) + data;
                    return JSUndefined.Value;
                }, "appendData", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue(
                (KeyString)"deleteData",
                new JSFunction((in Arguments a) =>
                {
                    var offset = a.Length > 0 ? (int)a[0].DoubleValue : 0;
                    var count = a.Length > 1 ? Math.Max(0, (int)a[1].DoubleValue) : 0;
                    var text = element.TextContent ?? string.Empty;
                    if (offset < 0 || offset > text.Length) throw new JSException("INDEX_SIZE_ERR");
                    var end = (int)Math.Min((long)offset + count, text.Length);
                    element.TextContent = text.Remove(offset, end - offset);
                    return JSUndefined.Value;
                }, "deleteData", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue(
                (KeyString)"insertData",
                new JSFunction((in Arguments a) =>
                {
                    var offset = a.Length > 0 ? (int)a[0].DoubleValue : 0;
                    var data = a.Length > 1 ? a[1].ToString() : string.Empty;
                    var text = element.TextContent ?? string.Empty;
                    if (offset < 0 || offset > text.Length) throw new JSException("INDEX_SIZE_ERR");
                    element.TextContent = text.Insert(offset, data);
                    return JSUndefined.Value;
                }, "insertData", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue(
                (KeyString)"replaceData",
                new JSFunction((in Arguments a) =>
                {
                    var offset = a.Length > 0 ? (int)a[0].DoubleValue : 0;
                    var count = a.Length > 1 ? Math.Max(0, (int)a[1].DoubleValue) : 0;
                    var data = a.Length > 2 ? a[2].ToString() : string.Empty;
                    var text = element.TextContent ?? string.Empty;
                    if (offset < 0 || offset > text.Length) throw new JSException("INDEX_SIZE_ERR");
                    var end = (int)Math.Min((long)offset + count, text.Length);
                    element.TextContent = text.Remove(offset, end - offset).Insert(offset, data);
                    return JSUndefined.Value;
                }, "replaceData", 3),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // DOCTYPE-specific properties
        if (string.Equals(element.TagName, "#doctype", StringComparison.OrdinalIgnoreCase))
        {
            obj.FastAddProperty(
                (KeyString)"name",
                new JSFunction((in Arguments _) =>
                {
                    return new JSString(GetDocTypeName(element));
                }, "get name"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty(
                (KeyString)"publicId",
                new JSFunction((in Arguments _) =>
                {
                    var pubId = element.DomProperties.TryGetValue("publicId", out var p) ? p?.ToString() ?? string.Empty : string.Empty;
                    return new JSString(pubId);
                }, "get publicId"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty(
                (KeyString)"systemId",
                new JSFunction((in Arguments _) =>
                {
                    var sysId = element.DomProperties.TryGetValue("systemId", out var s) ? s?.ToString() ?? string.Empty : string.Empty;
                    return new JSString(sysId);
                }, "get systemId"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty(
                (KeyString)"internalSubset",
                new JSFunction((in Arguments _) => JSNull.Value, "get internalSubset"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // ownerDocument (read-only) — returns the document element wrapper
        obj.FastAddProperty(
            (KeyString)"ownerDocument",
            new JSFunction((in Arguments a) =>
            {
                // The documentElement itself has ownerDocument = null per spec,
                // but for simplicity we return the document element's JSObject for all nodes
                return ToJSObject(DocumentElement);
            }, "get ownerDocument"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // parentElement (read-only, dynamic) — like parentNode but returns null for non-element parents
        obj.FastAddProperty(
            (KeyString)"parentElement",
            new JSFunction((in Arguments a) =>
            {
                if (element.Parent == null) return JSNull.Value;
                if (element.Parent.IsTextNode) return JSNull.Value;
                return (JSValue)ToJSObject(element.Parent);
            }, "get parentElement"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // hasChildNodes()
        obj.FastAddValue(
            (KeyString)"hasChildNodes",
            new JSFunction((in Arguments a) =>
                element.Children.Count > 0 ? JSBoolean.True : JSBoolean.False,
                "hasChildNodes", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // hasAttribute(name)
        obj.FastAddValue(
            (KeyString)"hasAttribute",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSBoolean.False;
                return element.Attributes.ContainsKey(a[0].ToString()) ? JSBoolean.True : JSBoolean.False;
            }, "hasAttribute", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeAttribute(name)
        obj.FastAddValue(
            (KeyString)"removeAttribute",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var attrName = a[0].ToString();
                    element.Attributes.Remove(attrName);
                    // Sync special properties
                    if (string.Equals(attrName, "id", StringComparison.OrdinalIgnoreCase))
                        element.Id = null;
                    else if (string.Equals(attrName, "class", StringComparison.OrdinalIgnoreCase))
                        element.ClassName = null;
                }
                return JSUndefined.Value;
            }, "removeAttribute", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setAttributeNS(namespace, qualifiedName, value)
        obj.FastAddValue(
            (KeyString)"setAttributeNS",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length >= 3)
                {
                    var ns = a[0].IsNull || a[0].IsUndefined ? null : a[0].ToString();
                    var qName = a[1].ToString();
                    var val = a[2].ToString();
                    var localName = qName.Contains(':') ? qName[(qName.IndexOf(':') + 1)..] : qName;
                    element.Attributes[qName] = val;
                    element.NsAttrMap[(ns, localName)] = qName;
                }
                return JSUndefined.Value;
            }, "setAttributeNS", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getAttributeNS(namespace, localName)
        obj.FastAddValue(
            (KeyString)"getAttributeNS",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSNull.Value;
                var ns = a[0].IsNull || a[0].IsUndefined ? null : a[0].ToString();
                var localName = a[1].ToString();
                if (element.NsAttrMap.TryGetValue((ns, localName), out var qName) &&
                    element.Attributes.TryGetValue(qName, out var val))
                    return (JSValue)new JSString(val);
                return JSNull.Value;
            }, "getAttributeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeAttributeNS(namespace, localName)
        obj.FastAddValue(
            (KeyString)"removeAttributeNS",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length >= 2)
                {
                    var ns = a[0].IsNull || a[0].IsUndefined ? null : a[0].ToString();
                    var localName = a[1].ToString();
                    if (element.NsAttrMap.TryGetValue((ns, localName), out var qName))
                    {
                        element.Attributes.Remove(qName);
                        element.NsAttrMap.Remove((ns, localName));
                    }
                }
                return JSUndefined.Value;
            }, "removeAttributeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // hasAttributeNS(namespace, localName)
        obj.FastAddValue(
            (KeyString)"hasAttributeNS",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSBoolean.False;
                var ns = a[0].IsNull || a[0].IsUndefined ? null : a[0].ToString();
                var localName = a[1].ToString();
                return element.NsAttrMap.ContainsKey((ns, localName)) ? JSBoolean.True : JSBoolean.False;
            }, "hasAttributeNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // contains(otherNode) — returns true if otherNode is a descendant
        obj.FastAddValue(
            (KeyString)"contains",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSBoolean.False;
                var otherObj = a[0] as JSObject;
                if (otherObj == null) return JSBoolean.False;
                var otherEl = FindDomElementByJSObject(otherObj);
                if (otherEl == null) return JSBoolean.False;
                if (ReferenceEquals(element, otherEl)) return JSBoolean.True;
                return IsDescendant(element, otherEl) ? JSBoolean.True : JSBoolean.False;
            }, "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // cloneNode(deep)
        obj.FastAddValue(
            (KeyString)"cloneNode",
            new JSFunction((in Arguments a) =>
            {
                var deep = a.Length > 0 && a[0].BooleanValue;
                var clone = CloneDomElement(element, deep);
                _elements.Add(clone);
                return ToJSObject(clone);
            }, "cloneNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // insertBefore(newChild, refChild)
        obj.FastAddValue(
            (KeyString)"insertBefore",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSUndefined.Value;
                var newChildObj = a[0] as JSObject;
                if (newChildObj == null) return JSUndefined.Value;
                var newEl = FindDomElementByJSObject(newChildObj);
                if (newEl == null) return a[0];

                // If refChild is null/undefined, act like appendChild
                if (a.Length < 2 || a[1].IsNull || a[1].IsUndefined)
                {
                    newEl.Parent?.Children.Remove(newEl);
                    newEl.Parent = element;
                    element.Children.Add(newEl);
                    return a[0];
                }

                var refChildObj = a[1] as JSObject;
                if (refChildObj == null) return a[0];
                var refEl = FindDomElementByJSObject(refChildObj);
                if (refEl == null) return a[0];

                var idx = element.Children.IndexOf(refEl);
                if (idx < 0) throw new JSException("NotFoundError: The node before which the new node is to be inserted is not a child of this node.");

                newEl.Parent?.Children.Remove(newEl);
                newEl.Parent = element;
                // Re-find index: removing newEl from its old parent may have shifted
                // indices if newEl was a sibling of refEl within this same parent.
                idx = element.Children.IndexOf(refEl);
                element.Children.Insert(idx, newEl);
                return a[0];
            }, "insertBefore", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // children (read-only) — element children only (no text nodes)
        obj.FastAddProperty(
            (KeyString)"children",
            new JSFunction((in Arguments a) =>
            {
                var result = new List<JSValue>();
                foreach (var child in element.Children)
                {
                    if (!child.IsTextNode)
                        result.Add(ToJSObject(child));
                }
                return new JSArray(result);
            }, "get children"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // childElementCount (read-only)
        obj.FastAddProperty(
            (KeyString)"childElementCount",
            new JSFunction((in Arguments a) =>
                new JSNumber(element.Children.Count(c => !c.IsTextNode)),
                "get childElementCount"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // firstElementChild (read-only)
        obj.FastAddProperty(
            (KeyString)"firstElementChild",
            new JSFunction((in Arguments a) =>
            {
                var first = element.Children.FirstOrDefault(c => !c.IsTextNode);
                return first != null ? (JSValue)ToJSObject(first) : JSNull.Value;
            }, "get firstElementChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastElementChild (read-only)
        obj.FastAddProperty(
            (KeyString)"lastElementChild",
            new JSFunction((in Arguments a) =>
            {
                var last = element.Children.LastOrDefault(c => !c.IsTextNode);
                return last != null ? (JSValue)ToJSObject(last) : JSNull.Value;
            }, "get lastElementChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextElementSibling (read-only)
        obj.FastAddProperty(
            (KeyString)"nextElementSibling",
            new JSFunction((in Arguments a) =>
            {
                if (element.Parent == null) return JSNull.Value;
                var siblings = element.Parent.Children;
                var idx = siblings.IndexOf(element);
                for (var i = idx + 1; i < siblings.Count; i++)
                {
                    if (!siblings[i].IsTextNode) return (JSValue)ToJSObject(siblings[i]);
                }
                return JSNull.Value;
            }, "get nextElementSibling"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // previousElementSibling (read-only)
        obj.FastAddProperty(
            (KeyString)"previousElementSibling",
            new JSFunction((in Arguments a) =>
            {
                if (element.Parent == null) return JSNull.Value;
                var siblings = element.Parent.Children;
                var idx = siblings.IndexOf(element);
                for (var i = idx - 1; i >= 0; i--)
                {
                    if (!siblings[i].IsTextNode) return (JSValue)ToJSObject(siblings[i]);
                }
                return JSNull.Value;
            }, "get previousElementSibling"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // -- DOM manipulation methods --

        // appendChild(child)
        obj.FastAddValue(
            (KeyString)"appendChild",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSUndefined.Value;
                var childObj = a[0] as JSObject;
                if (childObj == null) return JSUndefined.Value;

                // Find the DomElement for this child JSObject
                var childEl = FindDomElementByJSObject(childObj);
                if (childEl == null) return a[0];

                // Remove from old parent if any
                childEl.Parent?.Children.Remove(childEl);
                childEl.Parent = element;
                element.Children.Add(childEl);
                return a[0];
            }, "appendChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeChild(child)
        obj.FastAddValue(
            (KeyString)"removeChild",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSUndefined.Value;
                var childObj = a[0] as JSObject;
                if (childObj == null) return JSUndefined.Value;

                var childEl = FindDomElementByJSObject(childObj);
                if (childEl == null || !element.Children.Remove(childEl))
                    return a[0];
                childEl.Parent = null;
                return a[0];
            }, "removeChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // replaceChild(newChild, oldChild)
        obj.FastAddValue(
            (KeyString)"replaceChild",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSUndefined.Value;
                var newChildObj = a[0] as JSObject;
                var oldChildObj = a[1] as JSObject;
                if (newChildObj == null || oldChildObj == null) return JSUndefined.Value;

                var newEl = FindDomElementByJSObject(newChildObj);
                var oldEl = FindDomElementByJSObject(oldChildObj);
                if (newEl == null || oldEl == null) return a[1];

                var idx = element.Children.IndexOf(oldEl);
                if (idx < 0) return a[1];

                // If newChild is already in this parent, remove it first and re-find idx
                if (ReferenceEquals(newEl.Parent, element))
                {
                    element.Children.Remove(newEl);
                    idx = element.Children.IndexOf(oldEl);
                    if (idx < 0) return a[1];
                }
                else
                {
                    newEl.Parent?.Children.Remove(newEl);
                }

                oldEl.Parent = null;
                newEl.Parent = element;
                element.Children[idx] = newEl;
                return a[1]; // returns the old child
            }, "replaceChild", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- DOM events --

        // addEventListener(type, listener, useCapture)
        obj.FastAddValue(
            (KeyString)"addEventListener",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSUndefined.Value;
                var type = a[0].ToString();
                var listener = a[1];
                var capture = a.Length > 2 && a[2].BooleanValue;
                if (!element.EventListeners.TryGetValue(type, out var listeners))
                {
                    listeners = [];
                    element.EventListeners[type] = listeners;
                }
                listeners.Add((listener, capture));
                return JSUndefined.Value;
            }, "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeEventListener(type, listener, useCapture)
        obj.FastAddValue(
            (KeyString)"removeEventListener",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 2) return JSUndefined.Value;
                var type = a[0].ToString();
                var listener = a[1];
                var capture = a.Length > 2 && a[2].BooleanValue;
                if (element.EventListeners.TryGetValue(type, out var listeners))
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

        // dispatchEvent(event) — DOM Events Level 3 with capture/target/bubble phases
        var bridge = this;
        obj.FastAddValue(
            (KeyString)"dispatchEvent",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSBoolean.True;
                var evt = a[0] as JSObject;
                if (evt == null) return JSBoolean.True;

                return bridge.DispatchEventOnElement(element, evt);
            }, "dispatchEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // element.click() — creates and dispatches a MouseEvent
        obj.FastAddValue(
            (KeyString)"click",
            new JSFunction((in Arguments _) =>
            {
                var evt = new JSObject();
                evt.FastAddValue((KeyString)"type", new JSString("click"), JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"bubbles", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"cancelable", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"target", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"currentTarget", JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"eventPhase", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"detail", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"stopPropagation",
                    new JSFunction((in Arguments __) => JSUndefined.Value, "stopPropagation", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"stopImmediatePropagation",
                    new JSFunction((in Arguments __) => JSUndefined.Value, "stopImmediatePropagation", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"preventDefault",
                    new JSFunction((in Arguments __) => JSUndefined.Value, "preventDefault", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                bridge.DispatchEventOnElement(element, evt);
                return JSUndefined.Value;
            }, "click", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // on* inline event handler properties (onclick, onload, etc.)
        foreach (var eventName in InlineEventNames)
        {
            obj.FastAddProperty(
                (KeyString)$"on{eventName}",
                new JSFunction((in Arguments _) =>
                {
                    if (element.InlineEventHandlers.TryGetValue(eventName, out var handler))
                        return handler;
                    return JSNull.Value;
                }, $"get on{eventName}"),
                new JSFunction((in Arguments a) =>
                {
                    if (a.Length > 0 && a[0] is JSFunction fn)
                        element.InlineEventHandlers[eventName] = fn;
                    else
                        element.InlineEventHandlers.Remove(eventName);
                    return JSUndefined.Value;
                }, $"set on{eventName}"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // Compile on* HTML attributes into inline event handler functions
        CompileInlineEventAttributes(element);

        // -- Form element support --

        // value (read/write) — for input, textarea, select elements
        // The IDL 'value' property is NOT reflected as a content attribute for inputs.
        obj.FastAddProperty(
            (KeyString)"value",
            new JSFunction((in Arguments a) =>
            {
                if (element.DomProperties.TryGetValue("_value", out var domVal) && domVal is string sv)
                    return new JSString(sv);
                if (element.Attributes.TryGetValue("value", out var val))
                    return new JSString(val);
                return new JSString(string.Empty);
            }, "get value"),
            new JSFunction((in Arguments a) =>
            {
                var tag = element.TagName.ToLowerInvariant();
                var v = a.Length > 0 ? a[0].ToString() : string.Empty;
                if (tag == "input")
                    element.DomProperties["_value"] = v; // IDL value, not reflected
                else
                    element.Attributes["value"] = v;
                return JSUndefined.Value;
            }, "set value"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // checked (read/write) — for checkbox and radio inputs
        obj.FastAddProperty(
            (KeyString)"checked",
            new JSFunction((in Arguments a) =>
                element.Attributes.ContainsKey("checked") ? JSBoolean.True : JSBoolean.False,
                "get checked"),
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && a[0].BooleanValue)
                {
                    // Radio button mutual exclusion: uncheck others in same group
                    if (element.Attributes.TryGetValue("type", out var tp) &&
                        string.Equals(tp, "radio", System.StringComparison.OrdinalIgnoreCase) &&
                        element.Attributes.TryGetValue("name", out var radioName) &&
                        !string.IsNullOrEmpty(radioName))
                    {
                        // Find the form parent (if any)
                        var formParent = element.Parent;
                        while (formParent != null && !string.Equals(formParent.TagName, "form", System.StringComparison.OrdinalIgnoreCase))
                            formParent = formParent.Parent;
                        if (formParent != null)
                        {
                            // Only iterate form controls, not all elements
                            foreach (var sibling in CollectFormControls(formParent))
                            {
                                if (ReferenceEquals(sibling, element)) continue;
                                if (!string.Equals(sibling.TagName, "input", System.StringComparison.OrdinalIgnoreCase)) continue;
                                if (!sibling.Attributes.TryGetValue("type", out var st) ||
                                    !string.Equals(st, "radio", System.StringComparison.OrdinalIgnoreCase)) continue;
                                if (!sibling.Attributes.TryGetValue("name", out var sn) ||
                                    !string.Equals(sn, radioName, System.StringComparison.Ordinal)) continue;
                                sibling.Attributes.Remove("checked");
                            }
                        }
                    }
                    element.Attributes["checked"] = "checked";
                }
                else
                    element.Attributes.Remove("checked");
                return JSUndefined.Value;
            }, "set checked"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // type (read/write) — for input/button elements; getter returns lowercase
        obj.FastAddProperty(
            (KeyString)"type",
            new JSFunction((in Arguments a) =>
            {
                if (element.Attributes.TryGetValue("type", out var t))
                    return new JSString(t.ToLowerInvariant());
                // Default type values per HTML spec
                var tag = element.TagName.ToLowerInvariant();
                if (tag == "button") return new JSString("submit");
                return new JSString(string.Empty);
            }, "get type"),
            new JSFunction((in Arguments a) =>
            {
                element.Attributes["type"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                return JSUndefined.Value;
            }, "set type"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // name (read/write) — for form elements; syncs with content attribute
        // Skip for DOCTYPE nodes which have their own name property (doctype name)
        if (!string.Equals(element.TagName, "#doctype", StringComparison.OrdinalIgnoreCase))
        {
            obj.FastAddProperty(
                (KeyString)"name",
                new JSFunction((in Arguments a) =>
                {
                    if (element.Attributes.TryGetValue("name", out var n))
                        return new JSString(n);
                    return new JSString(string.Empty);
                }, "get name"),
                new JSFunction((in Arguments a) =>
                {
                    element.Attributes["name"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                    return JSUndefined.Value;
                }, "set name"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // disabled (read/write) — for form controls
        obj.FastAddProperty(
            (KeyString)"disabled",
            new JSFunction((in Arguments a) =>
                element.Attributes.ContainsKey("disabled") ? JSBoolean.True : JSBoolean.False,
                "get disabled"),
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && a[0].BooleanValue)
                    element.Attributes["disabled"] = "disabled";
                else
                    element.Attributes.Remove("disabled");
                return JSUndefined.Value;
            }, "set disabled"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // required (read/write) — form validation
        obj.FastAddProperty(
            (KeyString)"required",
            new JSFunction((in Arguments a) =>
                element.Attributes.ContainsKey("required") ? JSBoolean.True : JSBoolean.False,
                "get required"),
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && a[0].BooleanValue)
                    element.Attributes["required"] = "required";
                else
                    element.Attributes.Remove("required");
                return JSUndefined.Value;
            }, "set required"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // checkValidity() — form validation
        obj.FastAddValue(
            (KeyString)"checkValidity",
            new JSFunction((in Arguments a) =>
            {
                return CheckElementValidity(element) ? JSBoolean.True : JSBoolean.False;
            }, "checkValidity", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // reportValidity() — form validation
        obj.FastAddValue(
            (KeyString)"reportValidity",
            new JSFunction((in Arguments a) =>
            {
                return CheckElementValidity(element) ? JSBoolean.True : JSBoolean.False;
            }, "reportValidity", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // submit() — for form elements
        obj.FastAddValue(
            (KeyString)"submit",
            new JSFunction((in Arguments a) =>
            {
                if (string.Equals(element.TagName, "form", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Fire submit event
                    var submitEvt = new JSObject();
                    submitEvt.FastAddValue((KeyString)"type", new JSString("submit"), JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"target", obj, JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"bubbles", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"cancelable", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
                    var prevented = false;
                    submitEvt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"preventDefault", new JSFunction((in Arguments _) =>
                    {
                        prevented = true;
                        submitEvt[(KeyString)"defaultPrevented"] = JSBoolean.True;
                        return JSUndefined.Value;
                    }, "preventDefault", 0), JSPropertyAttributes.EnumerableConfigurableValue);
                    submitEvt.FastAddValue((KeyString)"stopPropagation", new JSFunction((in Arguments _) => JSUndefined.Value, "stopPropagation", 0), JSPropertyAttributes.EnumerableConfigurableValue);

                    if (element.EventListeners.TryGetValue("submit", out var submitListeners))
                    {
                        foreach (var (listener, _) in submitListeners.ToList())
                        {
                            if (listener is JSFunction fn)
                            {
                                try { fn.InvokeFunction(new Arguments(fn, submitEvt)); }
                                catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.submit", $"Submit listener error: {ex.Message}", ex); }
                            }
                        }
                    }

                    // If preventDefault was called, do not proceed with default action
                    if (prevented)
                    {
                        RenderLogger.LogDebug(LogCategory.JavaScript, "DomBridge.submit", "Default action prevented");
                    }
                }
                return JSUndefined.Value;
            }, "submit", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // querySelector on elements
        obj.FastAddValue(
            (KeyString)"querySelector",
            new JSFunction((in Arguments a) =>
            {
                var sel = a.Length > 0 ? a[0].ToString() : string.Empty;
                return FindInDescendants(element, sel, false, bridge);
            }, "querySelector", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // querySelectorAll on elements
        obj.FastAddValue(
            (KeyString)"querySelectorAll",
            new JSFunction((in Arguments a) =>
            {
                var sel = a.Length > 0 ? a[0].ToString() : string.Empty;
                return FindInDescendants(element, sel, true, bridge);
            }, "querySelectorAll", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getElementsByTagName on elements — searches descendants in tree order
        obj.FastAddValue(
            (KeyString)"getElementsByTagName",
            new JSFunction((in Arguments a) =>
            {
                var tagSearch = a.Length > 0 ? a[0].ToString().ToLowerInvariant() : string.Empty;
                var results = new List<JSValue>();
                CollectDescendantsByTag(element, tagSearch, results, bridge);
                return new JSArray(results);
            }, "getElementsByTagName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getContext(contextType) — for <canvas> elements
        obj.FastAddValue(
            (KeyString)"getContext",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var contextType = a[0].ToString();
                if (!string.Equals(contextType, "2d", System.StringComparison.OrdinalIgnoreCase))
                    return JSNull.Value;
                if (!string.Equals(element.TagName, "canvas", System.StringComparison.OrdinalIgnoreCase))
                    return JSNull.Value;
#if BROILER_CLI
                return JSNull.Value; // Canvas 2D context not available in CLI mode
#else
                return BuildCanvas2DContext(element);
#endif
            }, "getContext", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // contentWindow / contentDocument — for <iframe> elements with full sub-document DOM
        if (string.Equals(element.TagName, "iframe", System.StringComparison.OrdinalIgnoreCase))
        {
            // Determine if iframe src points to a non-HTML resource (Content-Type check)
            var iframeSrcValue = element.Attributes.TryGetValue("src", out var srcVal) ? srcVal : string.Empty;
            var isNonHtmlResource = IsNonHtmlResource(iframeSrcValue);

            // Same-origin check: if iframe src is cross-origin relative to the page, contentDocument returns null
            var isCrossOrigin = IsCrossOrigin(iframeSrcValue, _pageUrl);

            obj.FastAddProperty(
                (KeyString)"contentDocument",
                new JSFunction((in Arguments _) =>
                {
                    // Cross-origin iframes return null for contentDocument (same-origin policy)
                    if (isCrossOrigin) return JSNull.Value;
                    // Non-HTML resources get a minimal empty sub-document (no parsed fallback content)
                    return GetOrCreateSubDocument(element);
                }, "get contentDocument"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            obj.FastAddProperty(
                (KeyString)"contentWindow",
                new JSFunction((in Arguments _) =>
                {
                    if (isCrossOrigin) return JSNull.Value;
                    var iframeWindow = new JSObject();
                    iframeWindow.FastAddProperty(
                        (KeyString)"document",
                        new JSFunction((in Arguments __) => GetOrCreateSubDocument(element), "get document"),
                        null,
                        JSPropertyAttributes.EnumerableConfigurableProperty);
                    var iframeLocation = new JSObject();
                    iframeLocation.FastAddValue((KeyString)"href",
                        new JSString(!string.IsNullOrEmpty(iframeSrcValue) ? iframeSrcValue : "about:blank"),
                        JSPropertyAttributes.EnumerableConfigurableValue);
                    iframeWindow.FastAddValue((KeyString)"location", iframeLocation, JSPropertyAttributes.EnumerableConfigurableValue);
                    return iframeWindow;
                }, "get contentWindow"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // getSVGDocument() — returns contentDocument (same as contentDocument for same-origin)
            obj.FastAddValue(
                (KeyString)"getSVGDocument",
                new JSFunction((in Arguments _) =>
                {
                    if (isCrossOrigin) return JSNull.Value;
                    return GetOrCreateSubDocument(element);
                }, "getSVGDocument", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // src property (read/write) — for iframe elements
            obj.FastAddProperty(
                (KeyString)"src",
                new JSFunction((in Arguments _) =>
                {
                    return element.Attributes.TryGetValue("src", out var s)
                        ? (JSValue)new JSString(s)
                        : new JSString(string.Empty);
                }, "get src"),
                new JSFunction((in Arguments a) =>
                {
                    element.Attributes["src"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                    // Invalidate cached sub-document when src changes
                    _subDocumentCache.Remove(element);
                    return JSUndefined.Value;
                }, "set src"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // sandbox attribute access
            obj.FastAddProperty(
                (KeyString)"sandbox",
                new JSFunction((in Arguments _) =>
                {
                    return element.Attributes.TryGetValue("sandbox", out var sandbox)
                        ? (JSValue)new JSString(sandbox)
                        : new JSString(string.Empty);
                }, "get sandbox"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // -- Phase 5: HTML DOM Interfaces --

        var tag = element.TagName.ToLowerInvariant();

        // HTMLTableElement interface
        if (tag == "table")
        {
            // caption (get/set) — returns first <caption> child or null
            obj.FastAddProperty(
                (KeyString)"caption",
                new JSFunction((in Arguments _) =>
                {
                    var cap = element.Children.Find(c => string.Equals(c.TagName, "caption", System.StringComparison.OrdinalIgnoreCase));
                    return cap != null ? (JSValue)ToJSObject(cap) : JSNull.Value;
                }, "get caption"),
                new JSFunction((in Arguments a) => JSUndefined.Value, "set caption"), // setting to same value is no-op
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // tHead (get/set) — returns first <thead> child or null
            obj.FastAddProperty(
                (KeyString)"tHead",
                new JSFunction((in Arguments _) =>
                {
                    var th = element.Children.Find(c => string.Equals(c.TagName, "thead", System.StringComparison.OrdinalIgnoreCase));
                    return th != null ? (JSValue)ToJSObject(th) : JSNull.Value;
                }, "get tHead"),
                new JSFunction((in Arguments a) => JSUndefined.Value, "set tHead"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // tFoot (get/set) — returns first <tfoot> child or null
            obj.FastAddProperty(
                (KeyString)"tFoot",
                new JSFunction((in Arguments _) =>
                {
                    var tf = element.Children.Find(c => string.Equals(c.TagName, "tfoot", System.StringComparison.OrdinalIgnoreCase));
                    return tf != null ? (JSValue)ToJSObject(tf) : JSNull.Value;
                }, "get tFoot"),
                new JSFunction((in Arguments a) => JSUndefined.Value, "set tFoot"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // tBodies (read-only) — returns collection of <tbody> children
            obj.FastAddProperty(
                (KeyString)"tBodies",
                new JSFunction((in Arguments _) =>
                {
                    var bodies = new List<JSValue>();
                    foreach (var c in element.Children)
                        if (string.Equals(c.TagName, "tbody", System.StringComparison.OrdinalIgnoreCase))
                            bodies.Add(ToJSObject(c));
                    var arr = new JSArray(bodies);
                    arr.FastAddProperty(
                        (KeyString)"length",
                        new JSFunction((in Arguments __) => new JSNumber(bodies.Count), "get length"),
                        null, JSPropertyAttributes.EnumerableConfigurableProperty);
                    return arr;
                }, "get tBodies"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // rows (read-only) — returns all <tr> elements in spec order:
            // 1. thead rows, 2. tbody rows + direct tr children (in tree order), 3. tfoot rows
            obj.FastAddProperty(
                (KeyString)"rows",
                new JSFunction((in Arguments _) => BuildTableRows(element), "get rows"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // createCaption() — returns existing or creates new <caption>
            obj.FastAddValue(
                (KeyString)"createCaption",
                new JSFunction((in Arguments _) =>
                {
                    var cap = element.Children.Find(c => string.Equals(c.TagName, "caption", System.StringComparison.OrdinalIgnoreCase));
                    if (cap != null) return ToJSObject(cap);
                    cap = new DomElement("caption", null, null, string.Empty);
                    bridge._elements.Add(cap);
                    cap.Parent = element;
                    element.Children.Insert(0, cap);
                    return ToJSObject(cap);
                }, "createCaption", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // createTHead() — returns existing or creates new <thead>
            obj.FastAddValue(
                (KeyString)"createTHead",
                new JSFunction((in Arguments _) =>
                {
                    var th = element.Children.Find(c => string.Equals(c.TagName, "thead", System.StringComparison.OrdinalIgnoreCase));
                    if (th != null) return ToJSObject(th);
                    th = new DomElement("thead", null, null, string.Empty);
                    bridge._elements.Add(th);
                    th.Parent = element;
                    element.Children.Add(th);
                    return ToJSObject(th);
                }, "createTHead", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // createTFoot() — returns existing or creates new <tfoot>
            obj.FastAddValue(
                (KeyString)"createTFoot",
                new JSFunction((in Arguments _) =>
                {
                    var tf = element.Children.Find(c => string.Equals(c.TagName, "tfoot", System.StringComparison.OrdinalIgnoreCase));
                    if (tf != null) return ToJSObject(tf);
                    tf = new DomElement("tfoot", null, null, string.Empty);
                    bridge._elements.Add(tf);
                    tf.Parent = element;
                    element.Children.Add(tf);
                    return ToJSObject(tf);
                }, "createTFoot", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // deleteCaption()
            obj.FastAddValue(
                (KeyString)"deleteCaption",
                new JSFunction((in Arguments _) =>
                {
                    var cap = element.Children.Find(c => string.Equals(c.TagName, "caption", System.StringComparison.OrdinalIgnoreCase));
                    if (cap != null) { cap.Parent = null; element.Children.Remove(cap); }
                    return JSUndefined.Value;
                }, "deleteCaption", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // deleteTHead()
            obj.FastAddValue(
                (KeyString)"deleteTHead",
                new JSFunction((in Arguments _) =>
                {
                    var th = element.Children.Find(c => string.Equals(c.TagName, "thead", System.StringComparison.OrdinalIgnoreCase));
                    if (th != null) { th.Parent = null; element.Children.Remove(th); }
                    return JSUndefined.Value;
                }, "deleteTHead", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // deleteTFoot()
            obj.FastAddValue(
                (KeyString)"deleteTFoot",
                new JSFunction((in Arguments _) =>
                {
                    var tf = element.Children.Find(c => string.Equals(c.TagName, "tfoot", System.StringComparison.OrdinalIgnoreCase));
                    if (tf != null) { tf.Parent = null; element.Children.Remove(tf); }
                    return JSUndefined.Value;
                }, "deleteTFoot", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // insertRow(index) — inserts a <tr> into the table
            obj.FastAddValue(
                (KeyString)"insertRow",
                new JSFunction((in Arguments a) =>
                {
                    var index = a.Length > 0 ? (int)a[0].DoubleValue : -1;
                    return InsertTableRow(element, index, bridge);
                }, "insertRow", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // deleteRow(index)
            obj.FastAddValue(
                (KeyString)"deleteRow",
                new JSFunction((in Arguments a) =>
                {
                    if (a.Length == 0) return JSUndefined.Value;
                    var index = (int)a[0].DoubleValue;
                    var rows = CollectTableRows(element);
                    if (index < 0) index = rows.Count + index;
                    if (index >= 0 && index < rows.Count)
                    {
                        var row = rows[index];
                        row.Parent?.Children.Remove(row);
                        row.Parent = null;
                    }
                    return JSUndefined.Value;
                }, "deleteRow", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // HTMLTableSectionElement (thead, tbody, tfoot) — rows and insertRow
        if (tag == "thead" || tag == "tbody" || tag == "tfoot")
        {
            // rows — returns <tr> children of this section
            obj.FastAddProperty(
                (KeyString)"rows",
                new JSFunction((in Arguments _) =>
                {
                    var rows = new List<JSValue>();
                    foreach (var c in element.Children)
                        if (string.Equals(c.TagName, "tr", System.StringComparison.OrdinalIgnoreCase))
                            rows.Add(ToJSObject(c));
                    var arr = new JSArray(rows);
                    arr.FastAddProperty(
                        (KeyString)"length",
                        new JSFunction((in Arguments __) => new JSNumber(rows.Count), "get length"),
                        null, JSPropertyAttributes.EnumerableConfigurableProperty);
                    return arr;
                }, "get rows"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // insertRow(index)
            obj.FastAddValue(
                (KeyString)"insertRow",
                new JSFunction((in Arguments a) =>
                {
                    var index = a.Length > 0 ? (int)a[0].DoubleValue : -1;
                    var tr = new DomElement("tr", null, null, string.Empty);
                    bridge._elements.Add(tr);
                    tr.Parent = element;
                    var trRows = element.Children.Where(c =>
                        string.Equals(c.TagName, "tr", System.StringComparison.OrdinalIgnoreCase)).ToList();
                    if (index < 0 || index >= trRows.Count)
                        element.Children.Add(tr);
                    else
                    {
                        var refRow = trRows[index];
                        var idx = element.Children.IndexOf(refRow);
                        element.Children.Insert(idx, tr);
                    }
                    return ToJSObject(tr);
                }, "insertRow", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // HTMLTableRowElement (tr) — rowIndex, sectionRowIndex, cells, insertCell, deleteCell
        if (tag == "tr")
        {
            // rowIndex — position in the table's rows collection
            obj.FastAddProperty(
                (KeyString)"rowIndex",
                new JSFunction((in Arguments _) =>
                {
                    // Find parent table
                    var tableEl = element.Parent;
                    if (tableEl != null && (string.Equals(tableEl.TagName, "thead", System.StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(tableEl.TagName, "tbody", System.StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(tableEl.TagName, "tfoot", System.StringComparison.OrdinalIgnoreCase)))
                        tableEl = tableEl.Parent;
                    if (tableEl == null || !string.Equals(tableEl.TagName, "table", System.StringComparison.OrdinalIgnoreCase))
                        return new JSNumber(-1);
                    var rows = CollectTableRows(tableEl);
                    return new JSNumber(rows.IndexOf(element));
                }, "get rowIndex"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // sectionRowIndex — position within the parent section
            obj.FastAddProperty(
                (KeyString)"sectionRowIndex",
                new JSFunction((in Arguments _) =>
                {
                    var section = element.Parent;
                    if (section == null) return new JSNumber(-1);
                    var idx = 0;
                    foreach (var c in section.Children)
                    {
                        if (ReferenceEquals(c, element)) return new JSNumber(idx);
                        if (string.Equals(c.TagName, "tr", System.StringComparison.OrdinalIgnoreCase)) idx++;
                    }
                    return new JSNumber(-1);
                }, "get sectionRowIndex"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLFormElement interface
        if (tag == "form")
        {
            // elements — returns collection of form controls with named access
            obj.FastAddProperty(
                (KeyString)"elements",
                new JSFunction((in Arguments _) => BuildFormElementsCollection(element, bridge), "get elements"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // length — alias for elements.length
            obj.FastAddProperty(
                (KeyString)"length",
                new JSFunction((in Arguments _) =>
                {
                    var controls = CollectFormControls(element);
                    return new JSNumber(controls.Count);
                }, "get length"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // action (read/write)
            obj.FastAddProperty(
                (KeyString)"action",
                new JSFunction((in Arguments _) =>
                    element.Attributes.TryGetValue("action", out var act)
                        ? (JSValue)new JSString(act)
                        : new JSString(string.Empty),
                    "get action"),
                new JSFunction((in Arguments a) =>
                {
                    element.Attributes["action"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                    return JSUndefined.Value;
                }, "set action"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLSelectElement interface
        if (tag == "select")
        {
            // add(option, refOption)
            obj.FastAddValue(
                (KeyString)"add",
                new JSFunction((in Arguments a) =>
                {
                    if (a.Length == 0) return JSUndefined.Value;
                    var optObj = a[0] as JSObject;
                    if (optObj == null) return JSUndefined.Value;
                    var optEl = FindDomElementByJSObject(optObj);
                    if (optEl == null) return JSUndefined.Value;

                    DomElement? refEl = null;
                    if (a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined)
                    {
                        var refObj = a[1] as JSObject;
                        if (refObj != null) refEl = FindDomElementByJSObject(refObj);
                    }

                    optEl.Parent?.Children.Remove(optEl);
                    optEl.Parent = element;
                    if (refEl != null)
                    {
                        var idx = element.Children.IndexOf(refEl);
                        if (idx >= 0) element.Children.Insert(idx, optEl);
                        else element.Children.Add(optEl);
                    }
                    else
                        element.Children.Add(optEl);
                    return JSUndefined.Value;
                }, "add", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // options — returns collection of <option> children
            obj.FastAddProperty(
                (KeyString)"options",
                new JSFunction((in Arguments _) =>
                {
                    var opts = new List<JSValue>();
                    foreach (var c in element.Children)
                        if (string.Equals(c.TagName, "option", System.StringComparison.OrdinalIgnoreCase))
                            opts.Add(ToJSObject(c));
                    var arr = new JSArray(opts);
                    arr.FastAddProperty(
                        (KeyString)"length",
                        new JSFunction((in Arguments __) => new JSNumber(opts.Count), "get length"),
                        null, JSPropertyAttributes.EnumerableConfigurableProperty);
                    return arr;
                }, "get options"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // selectedIndex — index of the selected option
            obj.FastAddProperty(
                (KeyString)"selectedIndex",
                new JSFunction((in Arguments _) =>
                {
                    var idx = 0;
                    foreach (var c in element.Children)
                    {
                        if (string.Equals(c.TagName, "option", System.StringComparison.OrdinalIgnoreCase))
                        {
                            if (c.Attributes.ContainsKey("selected") ||
                                (c.DomProperties.TryGetValue("_defaultSelected", out var ds) && ds is true))
                                return new JSNumber(idx);
                            idx++;
                        }
                    }
                    return new JSNumber(0); // default: first option is selected
                }, "get selectedIndex"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLOptionElement interface
        if (tag == "option")
        {
            // defaultSelected (read/write)
            obj.FastAddProperty(
                (KeyString)"defaultSelected",
                new JSFunction((in Arguments _) =>
                    element.DomProperties.TryGetValue("_defaultSelected", out var ds) && ds is true
                        ? JSBoolean.True : JSBoolean.False,
                    "get defaultSelected"),
                new JSFunction((in Arguments a) =>
                {
                    element.DomProperties["_defaultSelected"] = a.Length > 0 && a[0].BooleanValue;
                    return JSUndefined.Value;
                }, "set defaultSelected"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLLabelElement — htmlFor property (maps to 'for' content attribute)
        if (tag == "label")
        {
            obj.FastAddProperty(
                (KeyString)"htmlFor",
                new JSFunction((in Arguments _) =>
                    element.Attributes.TryGetValue("for", out var f)
                        ? (JSValue)new JSString(f) : new JSString(string.Empty),
                    "get htmlFor"),
                new JSFunction((in Arguments a) =>
                {
                    element.Attributes["for"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                    return JSUndefined.Value;
                }, "set htmlFor"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLMetaElement — httpEquiv property (maps to 'http-equiv' content attribute)
        if (tag == "meta")
        {
            obj.FastAddProperty(
                (KeyString)"httpEquiv",
                new JSFunction((in Arguments _) =>
                    element.Attributes.TryGetValue("http-equiv", out var he)
                        ? (JSValue)new JSString(he) : new JSString(string.Empty),
                    "get httpEquiv"),
                new JSFunction((in Arguments a) =>
                {
                    element.Attributes["http-equiv"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                    return JSUndefined.Value;
                }, "set httpEquiv"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLObjectElement — data property with URI resolution + contentDocument + getSVGDocument + type
        if (tag == "object")
        {
            obj.FastAddProperty(
                (KeyString)"data",
                new JSFunction((in Arguments _) =>
                {
                    if (!element.Attributes.TryGetValue("data", out var d))
                        return new JSString(string.Empty);
                    // Resolve relative URI against base URL
                    if (Uri.TryCreate(bridge._pageUrl, UriKind.Absolute, out var baseUri) &&
                        Uri.TryCreate(baseUri, d, out var resolved))
                        return new JSString(resolved.AbsoluteUri);
                    return new JSString(d);
                }, "get data"),
                new JSFunction((in Arguments a) =>
                {
                    element.Attributes["data"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                    // Invalidate cached sub-document when data changes
                    bridge._subDocumentCache.Remove(element);
                    return JSUndefined.Value;
                }, "set data"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // type property (MIME type of the resource)
            obj.FastAddProperty(
                (KeyString)"type",
                new JSFunction((in Arguments _) =>
                    element.Attributes.TryGetValue("type", out var t)
                        ? (JSValue)new JSString(t) : new JSString(string.Empty),
                    "get type"),
                new JSFunction((in Arguments a) =>
                {
                    element.Attributes["type"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                    return JSUndefined.Value;
                }, "set type"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // contentDocument for <object> element (with same-origin check)
            obj.FastAddProperty(
                (KeyString)"contentDocument",
                new JSFunction((in Arguments _) =>
                {
                    var dataUrl = element.Attributes.TryGetValue("data", out var d) ? d : string.Empty;
                    if (IsCrossOrigin(dataUrl, bridge._pageUrl)) return JSNull.Value;
                    return bridge.GetOrCreateSubDocument(element);
                }, "get contentDocument"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // getSVGDocument() for <object> element
            obj.FastAddValue(
                (KeyString)"getSVGDocument",
                new JSFunction((in Arguments _) =>
                {
                    var dataUrl = element.Attributes.TryGetValue("data", out var d) ? d : string.Empty;
                    if (IsCrossOrigin(dataUrl, bridge._pageUrl)) return JSNull.Value;
                    return bridge.GetOrCreateSubDocument(element);
                }, "getSVGDocument", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // HTMLAnchorElement — href property with URI resolution
        if (tag == "a")
        {
            obj.FastAddProperty(
                (KeyString)"href",
                new JSFunction((in Arguments _) =>
                {
                    if (!element.Attributes.TryGetValue("href", out var h))
                        return new JSString(string.Empty);
                    if (Uri.TryCreate(bridge._pageUrl, UriKind.Absolute, out var baseUri) &&
                        Uri.TryCreate(baseUri, h, out var resolved))
                        return new JSString(resolved.AbsoluteUri);
                    return new JSString(h);
                }, "get href"),
                new JSFunction((in Arguments a) =>
                {
                    element.Attributes["href"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                    return JSUndefined.Value;
                }, "set href"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // -- Phase 7: HTMLAreaElement properties --
        if (tag == "area")
        {
            // shape, coords, alt, target — simple reflected attributes
            foreach (var attrName in new[] { "shape", "coords", "alt", "target" })
            {
                var captured = attrName; // capture for closure
                obj.FastAddProperty(
                    (KeyString)captured,
                    new JSFunction((in Arguments _) =>
                        element.Attributes.TryGetValue(captured, out var v) ? (JSValue)new JSString(v) : new JSString(string.Empty),
                        "get " + captured),
                    new JSFunction((in Arguments a) =>
                    {
                        element.Attributes[captured] = a.Length > 0 ? a[0].ToString() : string.Empty;
                        return JSUndefined.Value;
                    }, "set " + captured),
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }

            // href — with URI resolution like <a>
            obj.FastAddProperty(
                (KeyString)"href",
                new JSFunction((in Arguments _) =>
                {
                    if (!element.Attributes.TryGetValue("href", out var h))
                        return new JSString(string.Empty);
                    if (Uri.TryCreate(bridge._pageUrl, UriKind.Absolute, out var baseUri) &&
                        Uri.TryCreate(baseUri, h, out var resolved))
                        return new JSString(resolved.AbsoluteUri);
                    return new JSString(h);
                }, "get href"),
                new JSFunction((in Arguments a) =>
                {
                    element.Attributes["href"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                    return JSUndefined.Value;
                }, "set href"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // -- Phase 6: SVG DOM interfaces --

        // SVG element properties — provide SVGAnimatedLength stubs for dimensional attributes
        if (element.NamespaceURI == "http://www.w3.org/2000/svg" ||
            tag == "svg" || tag == "rect" || tag == "circle" || tag == "ellipse" ||
            tag == "line" || tag == "polyline" || tag == "polygon" || tag == "path" ||
            tag == "text" || tag == "g" || tag == "use" || tag == "image" ||
            tag == "svg:svg" || tag == "svg:rect" || tag == "svg:text" || tag == "svg:g")
        {
            // For SVG dimensional attributes, provide SVGAnimatedLength objects with baseVal/animVal
            foreach (var dimAttr in new[] { "width", "height", "x", "y", "cx", "cy", "r", "rx", "ry" })
            {
                var attrName = dimAttr; // capture for closure
                obj.FastAddProperty(
                    (KeyString)attrName,
                    new JSFunction((in Arguments _) =>
                    {
                        var animLength = new JSObject();
                        var valueStr = element.Attributes.TryGetValue(attrName, out var v) ? v : "0";
                        double.TryParse(valueStr, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var numVal);

                        var baseVal = new JSObject();
                        baseVal.FastAddValue((KeyString)"value", new JSNumber(numVal), JSPropertyAttributes.EnumerableConfigurableValue);
                        baseVal.FastAddValue((KeyString)"valueInSpecifiedUnits", new JSNumber(numVal), JSPropertyAttributes.EnumerableConfigurableValue);
                        baseVal.FastAddValue((KeyString)"unitType", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue); // SVG_LENGTHTYPE_NUMBER

                        var animVal = new JSObject();
                        animVal.FastAddValue((KeyString)"value", new JSNumber(numVal), JSPropertyAttributes.EnumerableConfigurableValue);
                        animVal.FastAddValue((KeyString)"valueInSpecifiedUnits", new JSNumber(numVal), JSPropertyAttributes.EnumerableConfigurableValue);
                        animVal.FastAddValue((KeyString)"unitType", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);

                        animLength.FastAddValue((KeyString)"baseVal", baseVal, JSPropertyAttributes.EnumerableConfigurableValue);
                        animLength.FastAddValue((KeyString)"animVal", animVal, JSPropertyAttributes.EnumerableConfigurableValue);
                        return animLength;
                    }, $"get {attrName}"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }

            // SVG viewBox attribute — returns SVGAnimatedRect with baseVal {x,y,width,height}
            if (tag == "svg" || tag == "svg:svg")
            {
                obj.FastAddProperty(
                    (KeyString)"viewBox",
                    new JSFunction((in Arguments _) =>
                    {
                        var animRect = new JSObject();
                        var baseRect = new JSObject();
                        double vbX = 0, vbY = 0, vbW = 0, vbH = 0;
                        if (element.Attributes.TryGetValue("viewBox", out var vb) && !string.IsNullOrWhiteSpace(vb))
                        {
                            var parts = vb.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 4)
                            {
                                double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out vbX);
                                double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out vbY);
                                double.TryParse(parts[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out vbW);
                                double.TryParse(parts[3], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out vbH);
                            }
                        }
                        baseRect.FastAddValue((KeyString)"x", new JSNumber(vbX), JSPropertyAttributes.EnumerableConfigurableValue);
                        baseRect.FastAddValue((KeyString)"y", new JSNumber(vbY), JSPropertyAttributes.EnumerableConfigurableValue);
                        baseRect.FastAddValue((KeyString)"width", new JSNumber(vbW), JSPropertyAttributes.EnumerableConfigurableValue);
                        baseRect.FastAddValue((KeyString)"height", new JSNumber(vbH), JSPropertyAttributes.EnumerableConfigurableValue);
                        animRect.FastAddValue((KeyString)"baseVal", baseRect, JSPropertyAttributes.EnumerableConfigurableValue);
                        animRect.FastAddValue((KeyString)"animVal", baseRect, JSPropertyAttributes.EnumerableConfigurableValue);
                        return animRect;
                    }, "get viewBox"),
                    null,
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }

            // SVGTextContentElement methods
            if (tag == "text" || tag == "svg:text" || tag == "tspan" || tag == "svg:tspan" ||
                tag == "textpath" || tag == "svg:textpath")
            {
                obj.FastAddValue(
                    (KeyString)"getNumberOfChars",
                    new JSFunction((in Arguments _) =>
                    {
                        var sb = new StringBuilder();
                        CollectTextContent(element, sb);
                        return new JSNumber(sb.Length);
                    }, "getNumberOfChars", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
            }
        }

        return obj;
    }

    // ── Phase 6: Sub-document cache ──────────────────────────────────────────
    private readonly Dictionary<DomElement, JSObject> _subDocumentCache = [];

    /// <summary>
    /// Gets or creates a full sub-document JSObject for iframe/object elements.
    /// The sub-document has its own DOM tree, createElement, getElementById, etc.
    /// For same-origin HTTP/HTTPS resources, attempts to fetch and parse the content.
    /// Non-HTML resources (by extension or Content-Type) get a minimal empty document.
    /// </summary>
    internal JSObject GetOrCreateSubDocument(DomElement containerElement)
    {
        if (_subDocumentCache.TryGetValue(containerElement, out var cached))
            return cached;

        var docRoot = containerElement.Children.FirstOrDefault(c =>
            string.Equals(c.TagName, "#subdoc-root", StringComparison.OrdinalIgnoreCase));
        if (docRoot == null)
        {
            // Determine the resource URL for this container
            var resourceUrl = GetSubResourceUrl(containerElement);
            var fetchedHtml = TryFetchSubResource(resourceUrl);

            if (!string.IsNullOrEmpty(fetchedHtml))
            {
                // Parse the fetched HTML into a sub-document tree
                docRoot = BuildSubDocumentFromHtml(fetchedHtml, containerElement);
            }
            else
            {
                // Default: create an empty sub-document structure
                docRoot = new DomElement("#subdoc-root", null, null, string.Empty);
                docRoot.Parent = containerElement;

                var htmlEl = new DomElement("html", null, null, string.Empty);
                htmlEl.Parent = docRoot;
                docRoot.Children.Add(htmlEl);

                var headEl = new DomElement("head", null, null, string.Empty);
                headEl.Parent = htmlEl;
                htmlEl.Children.Add(headEl);

                var bodyEl = new DomElement("body", null, null, string.Empty);
                bodyEl.Parent = htmlEl;
                htmlEl.Children.Add(bodyEl);

                containerElement.Children.Insert(0, docRoot);
                _elements.Add(docRoot);
                _elements.Add(htmlEl);
                _elements.Add(headEl);
                _elements.Add(bodyEl);
            }
        }

        var doc = BuildSubDocument(docRoot);
        _subDocumentCache[containerElement] = doc;
        return doc;
    }

    /// <summary>
    /// Gets the resource URL for a container element (iframe src or object data).
    /// </summary>
    private static string GetSubResourceUrl(DomElement containerElement)
    {
        var tag = containerElement.TagName?.ToLowerInvariant();
        if (tag == "iframe")
            return containerElement.Attributes.TryGetValue("src", out var src) ? src : string.Empty;
        if (tag == "object")
            return containerElement.Attributes.TryGetValue("data", out var data) ? data : string.Empty;
        return string.Empty;
    }

    /// <summary>
    /// Attempts to fetch a sub-resource URL and return its HTML content.
    /// Returns <c>null</c> for non-HTML resources, about:blank, empty URLs,
    /// or when the fetch fails.  Supports <c>data:</c> URIs with HTML content.
    /// </summary>
    private string? TryFetchSubResource(string resourceUrl)
    {
        if (string.IsNullOrWhiteSpace(resourceUrl))
            return null;

        // about:blank gets an empty document (default behavior)
        if (string.Equals(resourceUrl, "about:blank", StringComparison.OrdinalIgnoreCase))
            return null;

        // Handle data: URIs — decode and return HTML content directly
        if (resourceUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var (mimeType, body) = DecodeDataUriParts(resourceUrl);
            // Only return as HTML if the MIME type is text/html or application/xhtml+xml
            if (string.Equals(mimeType, "text/html", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mimeType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(mimeType))
                return !string.IsNullOrEmpty(body) ? body : null;
            return null;
        }

        // Non-HTML resources by extension get an empty document
        if (IsNonHtmlResource(resourceUrl))
            return null;

        // Resolve relative URL against page URL
        string resolvedUrl;
        if (Uri.TryCreate(resourceUrl, UriKind.Absolute, out _))
        {
            resolvedUrl = resourceUrl;
        }
        else if (Uri.TryCreate(_pageUrl, UriKind.Absolute, out var baseUri) &&
                 Uri.TryCreate(baseUri, resourceUrl, out var resolved))
        {
            resolvedUrl = resolved.AbsoluteUri;
        }
        else
        {
            return null;
        }

        // Only fetch HTTP/HTTPS URLs
        if (!resolvedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !resolvedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            using var response = SharedHttpClient.GetAsync(resolvedUrl).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                return null;

            // Check Content-Type header for non-HTML content
            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!string.IsNullOrEmpty(contentType) &&
                !contentType.Contains("html", StringComparison.OrdinalIgnoreCase) &&
                !contentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
            {
                // Non-HTML content type (image/png, text/plain, etc.) → empty document
                return null;
            }

            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Builds a sub-document tree from fetched HTML content.
    /// </summary>
    private DomElement BuildSubDocumentFromHtml(string html, DomElement containerElement)
    {
        var docRoot = new DomElement("#subdoc-root", null, null, string.Empty);
        docRoot.Parent = containerElement;

        var builder = new HtmlTreeBuilder();
        var (parsedRoot, allElements, _) = builder.Build(html);

        // Move parsed children under the sub-document root
        foreach (var child in parsedRoot.Children)
        {
            child.Parent = docRoot;
            docRoot.Children.Add(child);
        }

        containerElement.Children.Insert(0, docRoot);
        _elements.Add(docRoot);
        _elements.AddRange(allElements);

        return docRoot;
    }

    /// <summary>
    /// Builds a full document JSObject for a sub-document tree rooted at the given element.
    /// </summary>
    private JSObject BuildSubDocument(DomElement docRoot)
    {
        var doc = new JSObject();
        var bridge = this;

        DomElement GetDocumentElement() =>
            docRoot.Children.FirstOrDefault(c => !c.IsTextNode && !c.TagName.StartsWith("#"))
            ?? docRoot;

        doc.FastAddProperty(
            (KeyString)"documentElement",
            new JSFunction((in Arguments _) => ToJSObject(GetDocumentElement()), "get documentElement"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // body
        doc.FastAddProperty(
            (KeyString)"body",
            new JSFunction((in Arguments _) =>
            {
                var htmlEl = GetDocumentElement();
                foreach (var child in htmlEl.Children)
                {
                    if (string.Equals(child.TagName, "body", StringComparison.OrdinalIgnoreCase))
                        return ToJSObject(child);
                }
                return JSNull.Value;
            }, "get body"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // head
        doc.FastAddProperty(
            (KeyString)"head",
            new JSFunction((in Arguments _) =>
            {
                var htmlEl = GetDocumentElement();
                foreach (var child in htmlEl.Children)
                {
                    if (string.Equals(child.TagName, "head", StringComparison.OrdinalIgnoreCase))
                        return ToJSObject(child);
                }
                return JSNull.Value;
            }, "get head"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // childNodes
        doc.FastAddProperty(
            (KeyString)"childNodes",
            new JSFunction((in Arguments _) =>
            {
                var arr = new JSArray();
                foreach (var child in docRoot.Children)
                    arr.Add(ToJSObject(child));
                return arr;
            }, "get childNodes"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // firstChild
        doc.FastAddProperty(
            (KeyString)"firstChild",
            new JSFunction((in Arguments _) =>
            {
                return docRoot.Children.Count > 0
                    ? (JSValue)ToJSObject(docRoot.Children[0])
                    : JSNull.Value;
            }, "get firstChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastChild
        doc.FastAddProperty(
            (KeyString)"lastChild",
            new JSFunction((in Arguments _) =>
            {
                return docRoot.Children.Count > 0
                    ? (JSValue)ToJSObject(docRoot.Children[^1])
                    : JSNull.Value;
            }, "get lastChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // hasChildNodes()
        doc.FastAddValue(
            (KeyString)"hasChildNodes",
            new JSFunction((in Arguments _) =>
                docRoot.Children.Count > 0 ? JSBoolean.True : JSBoolean.False,
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
            new JSFunction((in Arguments _) => JSNull.Value, "get localName"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // getElementById(id)
        doc.FastAddValue(
            (KeyString)"getElementById",
            new JSFunction((in Arguments a) =>
            {
                var id = a.Length > 0 ? a[0].ToString() : string.Empty;
                var found = FindInSubTree(docRoot, el => el.Id == id);
                return found != null ? (JSValue)ToJSObject(found) : JSNull.Value;
            }, "getElementById", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getElementsByTagName(tag)
        doc.FastAddValue(
            (KeyString)"getElementsByTagName",
            new JSFunction((in Arguments a) =>
            {
                var tagName = a.Length > 0 ? a[0].ToString().ToLowerInvariant() : string.Empty;
                var results = new List<JSValue>();
                CollectByTagName(docRoot, tagName, results);
                return new JSArray(results);
            }, "getElementsByTagName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createElement(tag)
        doc.FastAddValue(
            (KeyString)"createElement",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    throw new JSException("Failed to execute 'createElement': 1 argument required.");
                var tagName = a[0].ToString().ToLowerInvariant();
                var el = new DomElement(tagName, null, null, string.Empty);
                _elements.Add(el);
                return ToJSObject(el);
            }, "createElement", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createTextNode(text)
        doc.FastAddValue(
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

        // createComment(data)
        doc.FastAddValue(
            (KeyString)"createComment",
            new JSFunction((in Arguments a) =>
            {
                var data = a.Length > 0 ? a[0].ToString() : string.Empty;
                var el = new DomElement("#comment", null, null, string.Empty);
                el.TextContent = data;
                _elements.Add(el);
                return ToJSObject(el);
            }, "createComment", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createElementNS(ns, localName)
        doc.FastAddValue(
            (KeyString)"createElementNS",
            new JSFunction((in Arguments a) =>
            {
                var ns = a.Length > 0 ? a[0].ToString() : null;
                var localName = a.Length > 1 ? a[1].ToString() : (a.Length > 0 ? a[0].ToString() : "div");
                var el = new DomElement(localName, null, null, string.Empty);
                if (!string.IsNullOrEmpty(ns))
                    el.NamespaceURI = ns;
                _elements.Add(el);
                return ToJSObject(el);
            }, "createElementNS", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createEvent(type)
        doc.FastAddValue(
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
                    new JSFunction((in Arguments __) => JSUndefined.Value, "stopPropagation", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"stopImmediatePropagation",
                    new JSFunction((in Arguments __) => JSUndefined.Value, "stopImmediatePropagation", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                evt.FastAddValue((KeyString)"preventDefault",
                    new JSFunction((in Arguments __) => JSUndefined.Value, "preventDefault", 0),
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

        // querySelector / querySelectorAll
        doc.FastAddValue(
            (KeyString)"querySelector",
            new JSFunction((in Arguments a) =>
            {
                var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
                var found = FindInSubTree(docRoot, el => MatchesSelector(el, selector));
                return found != null ? (JSValue)ToJSObject(found) : JSNull.Value;
            }, "querySelector", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        doc.FastAddValue(
            (KeyString)"querySelectorAll",
            new JSFunction((in Arguments a) =>
            {
                var selector = a.Length > 0 ? a[0].ToString() : string.Empty;
                var results = new List<JSValue>();
                CollectMatching(docRoot, el => MatchesSelector(el, selector), results);
                return new JSArray(results);
            }, "querySelectorAll", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.open()
        doc.FastAddValue(
            (KeyString)"open",
            new JSFunction((in Arguments _) =>
            {
                docRoot.Children.Clear();
                return doc;
            }, "open", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.close()
        doc.FastAddValue(
            (KeyString)"close",
            new JSFunction((in Arguments _) => JSUndefined.Value, "close", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.write(html)
        doc.FastAddValue(
            (KeyString)"write",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSUndefined.Value;
                var fragment = a[0].ToString();

                // Parse DOCTYPE if present
                var doctype = bridge.ParseDocType(fragment);

                var treeBuilder = new HtmlTreeBuilder();
                var (parsedDoc, allEls, _) = treeBuilder.Build(fragment);

                if (docRoot.Children.Count == 0)
                {
                    if (doctype != null)
                    {
                        doctype.Parent = docRoot;
                        docRoot.Children.Add(doctype);
                        bridge._elements.Add(doctype);
                    }

                    // parsedDoc is the <html> element from HtmlTreeBuilder.
                    // Add it directly to docRoot (not its children).
                    parsedDoc.Parent = docRoot;
                    docRoot.Children.Add(parsedDoc);
                    if (!bridge._elements.Contains(parsedDoc))
                        bridge._elements.Add(parsedDoc);
                    foreach (var el in allEls)
                    {
                        if (!bridge._elements.Contains(el))
                            bridge._elements.Add(el);
                    }
                }
                else
                {
                    var bodyEl = bridge.FindInSubTree(docRoot, el =>
                        string.Equals(el.TagName, "body", StringComparison.OrdinalIgnoreCase));
                    if (bodyEl != null)
                    {
                        var parsedBody = FindInTree(parsedDoc, el =>
                            string.Equals(el.TagName, "body", StringComparison.OrdinalIgnoreCase));
                        if (parsedBody != null)
                        {
                            foreach (var child in parsedBody.Children)
                            {
                                child.Parent = bodyEl;
                                bodyEl.Children.Add(child);
                            }
                        }
                    }
                    foreach (var el in allEls)
                    {
                        if (!bridge._elements.Contains(el))
                            bridge._elements.Add(el);
                    }
                }
                return JSUndefined.Value;
            }, "write", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // document.images
        doc.FastAddProperty(
            (KeyString)"images",
            new JSFunction((in Arguments _) =>
            {
                var results = new List<JSValue>();
                CollectByTagName(docRoot, "img", results);
                return new JSArray(results);
            }, "get images"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // document.links
        doc.FastAddProperty(
            (KeyString)"links",
            new JSFunction((in Arguments _) =>
            {
                var results = new List<JSValue>();
                CollectMatching(docRoot, el =>
                    string.Equals(el.TagName, "a", StringComparison.OrdinalIgnoreCase) &&
                    el.Attributes.ContainsKey("href"), results);
                return new JSArray(results);
            }, "get links"),
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
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var childObj = a[0] as JSObject;
                if (childObj == null) return JSNull.Value;
                foreach (var child in docRoot.Children.ToList())
                {
                    if (bridge._jsObjectCache.TryGetValue(child, out var cached) && cached == childObj)
                    {
                        docRoot.Children.Remove(child);
                        child.Parent = null;
                        return childObj;
                    }
                }
                return childObj;
            }, "removeChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // appendChild on document
        doc.FastAddValue(
            (KeyString)"appendChild",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var childObj = a[0] as JSObject;
                if (childObj == null) return a.Length > 0 ? a[0] : JSNull.Value;
                foreach (var kvp in bridge._jsObjectCache)
                {
                    if (kvp.Value == childObj)
                    {
                        var child = kvp.Key;
                        if (child.Parent != null)
                            child.Parent.Children.Remove(child);
                        child.Parent = docRoot;
                        docRoot.Children.Add(child);
                        return childObj;
                    }
                }
                return a[0];
            }, "appendChild", 1),
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
            new JSFunction((in Arguments _) => JSBoolean.True, "hasFeature", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        subImpl.FastAddValue(
            (KeyString)"createDocumentType",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length < 3)
                    throw new JSException("Failed to execute 'createDocumentType' on 'DOMImplementation': 3 arguments required.");
                var qualifiedName = a[0].ToString();
                var publicId = a[1].ToString();
                var systemId = a[2].ToString();
                ValidateElementName(qualifiedName, _jsContext!);
                var dt = new DomElement("#doctype", null, null, string.Empty);
                dt.DomProperties["name"] = qualifiedName;
                dt.DomProperties["publicId"] = publicId;
                dt.DomProperties["systemId"] = systemId;
                dt.DomProperties["internalSubset"] = null;
                _elements.Add(dt);
                return ToJSObject(dt);
            }, "createDocumentType", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        subImpl.FastAddValue(
            (KeyString)"createDocument",
            new JSFunction((in Arguments a) =>
            {
                var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
                var qName = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? a[1].ToString() : null;
                var doctypeArg = a.Length > 2 ? a[2] : null;

                if (!string.IsNullOrEmpty(qName))
                    ValidateQualifiedName(qName, ns, _jsContext!);

                var subDocRoot = new DomElement("#subdoc-root", null, null, string.Empty);
                _elements.Add(subDocRoot);

                if (doctypeArg is JSObject dtObj)
                {
                    foreach (var kvp in _jsObjectCache)
                    {
                        if (kvp.Value == dtObj)
                        {
                            var dtEl = kvp.Key;
                            dtEl.Parent = subDocRoot;
                            subDocRoot.Children.Add(dtEl);
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(qName))
                {
                    var docEl = new DomElement(qName, null, null, string.Empty);
                    if (!string.IsNullOrEmpty(ns))
                        docEl.NamespaceURI = ns;
                    docEl.Parent = subDocRoot;
                    subDocRoot.Children.Add(docEl);
                    _elements.Add(docEl);
                }

                return BuildSubDocument(subDocRoot);
            }, "createDocument", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        subImpl.FastAddValue(
            (KeyString)"createHTMLDocument",
            new JSFunction((in Arguments a) =>
            {
                var subTitle = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;

                var subDocRoot = new DomElement("#subdoc-root", null, null, string.Empty);
                _elements.Add(subDocRoot);

                var dt = new DomElement("#doctype", null, null, string.Empty);
                dt.DomProperties["name"] = "html";
                dt.DomProperties["publicId"] = string.Empty;
                dt.DomProperties["systemId"] = string.Empty;
                dt.DomProperties["internalSubset"] = null;
                dt.Parent = subDocRoot;
                subDocRoot.Children.Add(dt);
                _elements.Add(dt);

                var subHtml = new DomElement("html", null, null, string.Empty);
                subHtml.NamespaceURI = "http://www.w3.org/1999/xhtml";
                subHtml.Parent = subDocRoot;
                subDocRoot.Children.Add(subHtml);
                _elements.Add(subHtml);

                var subHead = new DomElement("head", null, null, string.Empty);
                subHead.Parent = subHtml;
                subHtml.Children.Add(subHead);
                _elements.Add(subHead);

                if (subTitle != null)
                {
                    var subTitleEl = new DomElement("title", null, null, string.Empty);
                    subTitleEl.Parent = subHead;
                    subHead.Children.Add(subTitleEl);
                    _elements.Add(subTitleEl);

                    var subTitleText = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                    subTitleText.TextContent = subTitle;
                    subTitleText.Parent = subTitleEl;
                    subTitleEl.Children.Add(subTitleText);
                    _elements.Add(subTitleText);
                }

                var subBody = new DomElement("body", null, null, string.Empty);
                subBody.Parent = subHtml;
                subHtml.Children.Add(subBody);
                _elements.Add(subBody);

                return BuildSubDocument(subDocRoot);
            }, "createHTMLDocument", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        doc.FastAddValue(
            (KeyString)"implementation",
            subImpl,
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

    /// <summary>Collects all elements matching a tag name in a sub-tree.</summary>
    private void CollectByTagName(DomElement root, string tag, List<JSValue> results)
    {
        foreach (var child in root.Children)
        {
            if (!child.IsTextNode && string.Equals(child.TagName, tag, StringComparison.OrdinalIgnoreCase))
                results.Add(ToJSObject(child));
            CollectByTagName(child, tag, results);
        }
    }

    /// <summary>Collects all elements matching a predicate in a sub-tree.</summary>
    private void CollectMatching(DomElement root, Func<DomElement, bool> predicate, List<JSValue> results)
    {
        foreach (var child in root.Children)
        {
            if (!child.IsTextNode && predicate(child))
                results.Add(ToJSObject(child));
            CollectMatching(child, predicate, results);
        }
    }

    /// <summary>Collects all DomElement nodes in a sub-tree for tracking.</summary>
    private static void CollectSubDocElements(DomElement root, List<DomElement> list)
    {
        list.Add(root);
        foreach (var child in root.Children)
            CollectSubDocElements(child, list);
    }

    /// <summary>
    /// Builds a styleSheets collection JSObject for a sub-document.
    /// </summary>

    // -- Phase 7: NamedNodeMap and Attr node helpers --

    /// <summary>
    /// Builds a NamedNodeMap-like JSObject for element.attributes, with
    /// getNamedItem, setNamedItem, removeNamedItem, item, and length.
    /// </summary>
    private JSObject BuildNamedNodeMapObject(DomElement element, JSObject ownerObj)
    {
        var map = new JSObject();

        // length — number of attributes
        map.FastAddProperty(
            (KeyString)"length",
            new JSFunction((in Arguments _) => new JSNumber(element.Attributes.Count), "get length"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // getNamedItem(name) — returns Attr node or null
        map.FastAddValue(
            (KeyString)"getNamedItem",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var name = a[0].ToString();
                if (!element.Attributes.TryGetValue(name, out var val))
                    return JSNull.Value;
                return BuildAttrNode(name, val, element, ownerObj);
            }, "getNamedItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setNamedItem(attr) — adds/replaces attribute from Attr node, returns old Attr or null
        map.FastAddValue(
            (KeyString)"setNamedItem",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var attrObj = a[0] as JSObject;
                if (attrObj == null) return JSNull.Value;
                var name = attrObj[(KeyString)"name"].ToString();
                var value = attrObj[(KeyString)"value"].ToString();
                JSValue old = JSNull.Value;
                if (element.Attributes.TryGetValue(name, out var oldVal))
                    old = BuildAttrNode(name, oldVal, element, ownerObj);
                element.Attributes[name] = value;
                // Sync special properties
                if (string.Equals(name, "id", StringComparison.OrdinalIgnoreCase))
                    element.Id = value;
                else if (string.Equals(name, "class", StringComparison.OrdinalIgnoreCase))
                    element.ClassName = value;
                return old;
            }, "setNamedItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeNamedItem(name) — removes and returns the Attr node
        map.FastAddValue(
            (KeyString)"removeNamedItem",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var name = a[0].ToString();
                if (!element.Attributes.TryGetValue(name, out var val))
                    return JSNull.Value;
                element.Attributes.Remove(name);
                // Sync special properties
                if (string.Equals(name, "id", StringComparison.OrdinalIgnoreCase))
                    element.Id = null;
                else if (string.Equals(name, "class", StringComparison.OrdinalIgnoreCase))
                    element.ClassName = null;
                return BuildAttrNode(name, val, element, ownerObj);
            }, "removeNamedItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // item(index) — returns Attr node at position
        map.FastAddValue(
            (KeyString)"item",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var idx = (int)a[0].DoubleValue;
                var keys = element.Attributes.Keys.ToList();
                if (idx < 0 || idx >= keys.Count) return JSNull.Value;
                var name = keys[idx];
                return BuildAttrNode(name, element.Attributes[name], element, ownerObj);
            }, "item", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Numeric index access — expose each attribute by index
        var attrKeys = element.Attributes.Keys.ToList();
        for (var i = 0; i < attrKeys.Count; i++)
        {
            var idx = i;
            map.FastAddProperty(
                (KeyString)idx.ToString(),
                new JSFunction((in Arguments _) =>
                {
                    var keys = element.Attributes.Keys.ToList();
                    if (idx >= keys.Count) return JSUndefined.Value;
                    var n = keys[idx];
                    return BuildAttrNode(n, element.Attributes[n], element, ownerObj);
                }, "get " + idx),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        return map;
    }

    /// <summary>
    /// Builds an Attr-like JSObject with name, value, specified, ownerElement, nodeType, nodeName.
    /// </summary>
    private static JSObject BuildAttrNode(string name, string value, DomElement element, JSObject ownerObj)
    {
        var attr = new JSObject();
        attr.FastAddValue((KeyString)"name", new JSString(name), JSPropertyAttributes.EnumerableConfigurableValue);
        attr.FastAddValue((KeyString)"value", new JSString(value), JSPropertyAttributes.EnumerableConfigurableValue);
        attr.FastAddValue((KeyString)"specified", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
        attr.FastAddValue((KeyString)"ownerElement", ownerObj, JSPropertyAttributes.EnumerableConfigurableValue);
        attr.FastAddValue((KeyString)"nodeType", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        attr.FastAddValue((KeyString)"nodeName", new JSString(name), JSPropertyAttributes.EnumerableConfigurableValue);
        return attr;
    }
}
