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
            new JSFunction((in Arguments a) =>
                element.Id != null ? new JSString(element.Id) : JSNull.Value,
                "get id"),
            new JSFunction((in Arguments a) =>
            {
                var val = a.Length > 0 ? a[0].ToString() : string.Empty;
                element.Id = val;
                element.Attributes["id"] = val;
                bridge.InvalidateStyleScope(element);
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
                return element.ClassName != null ? new JSString(element.ClassName) : new JSString(string.Empty);
            }, "get className"),
            new JSFunction((in Arguments a) =>
            {
                var val = a.Length > 0 ? a[0].ToString() : string.Empty;
                element.ClassName = val;
                element.Attributes["class"] = val;
                bridge.InvalidateStyleScope(element);
                return JSUndefined.Value;
            }, "set className"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // title (read/write) — synced with attributes["title"]
        obj.FastAddProperty(
            (KeyString)"title",
            new JSFunction((in Arguments a) =>
                element.Attributes.TryGetValue("title", out var t)
                    ? new JSString(t)
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

        // style object — CSS property access and manipulation.
        // In browsers, `element.style` is a read-only property: assigning a
        // string sets `style.cssText` instead of replacing the object.
        var styleObj = BuildStyleObject(element);
        obj.FastAddProperty(
            (KeyString)"style",
            new JSFunction((in Arguments a) => styleObj, "get style"),
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && a[0] is JSString s)
                {
                    // Setting element.style = "prop: val; ..." parses as cssText
                    element.Style.Clear();
                    element.JsSetStyleProps.Clear();
                    foreach (var kv in ParseStyle(s.ToString()))
                    {
                        element.Style[kv.Key] = kv.Value;
                        element.JsSetStyleProps.Add(kv.Key);
                    }
                }
                return JSUndefined.Value;
            }, "set style"),
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

                    if (!string.Equals(attrName, "style", StringComparison.OrdinalIgnoreCase))
                        bridgeForSet.InvalidateStyleScope(element);
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
                    ? new JSString(val)
                    : JSNull.Value;
            }, "getAttribute", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- DOM tree navigation --

        // parentNode (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"parentNode",
            new JSFunction((in Arguments a) =>
                element.Parent != null ? ToJSObject(element.Parent) : JSNull.Value,
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
                element.Children.Count > 0 ? ToJSObject(element.Children[0]) : JSNull.Value,
                "get firstChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastChild (read-only, dynamic)
        obj.FastAddProperty(
            (KeyString)"lastChild",
            new JSFunction((in Arguments a) =>
                element.Children.Count > 0 ? ToJSObject(element.Children[^1]) : JSNull.Value,
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
                    ? ToJSObject(siblings[idx + 1])
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
                    ? ToJSObject(siblings[idx - 1])
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
                // Non-HTML namespace elements preserve original case (per DOM spec)
                if (!string.IsNullOrEmpty(element.NamespaceURI) &&
                    !string.Equals(element.NamespaceURI, "http://www.w3.org/1999/xhtml", StringComparison.OrdinalIgnoreCase))
                    return new JSString(element.TagName);
                return new JSString(element.TagName.ToUpperInvariant());
            }, "get nodeName"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // localName (read-only) — null for non-element nodes; local part of tag name for elements
        obj.FastAddProperty(
            (KeyString)"localName",
            new JSFunction((in Arguments a) =>
            {
                if (element.IsTextNode) return JSNull.Value;
                if (element.TagName.StartsWith("#")) return JSNull.Value; // #comment, #document, etc.
                var name = element.TagName;
                var colonIdx = name.IndexOf(':');
                if (colonIdx >= 0)
                    name = name[(colonIdx + 1)..];
                return new JSString(name.ToLowerInvariant());
            }, "get localName"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // prefix (read-only) — namespace prefix or null
        obj.FastAddProperty(
            (KeyString)"prefix",
            new JSFunction((in Arguments a) =>
            {
                var colonIdx = element.TagName.IndexOf(':');
                if (colonIdx >= 0)
                    return new JSString(element.TagName[..colonIdx]);
                return JSNull.Value;
            }, "get prefix"),
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
                    return element.TextContent != null ? new JSString(element.TextContent) : JSNull.Value;
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
                    return element.TextContent != null ? new JSString(element.TextContent) : new JSString(string.Empty);
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

        // ownerDocument (read-only) — returns the Document node (nodeType=9)
        obj.FastAddProperty(
            (KeyString)"ownerDocument",
            new JSFunction((in Arguments a) =>
            {
                // For elements in sub-documents, return the sub-document JSObject
                if (element.OwnerDocRoot != null &&
                    _docRootToDocJSObject.TryGetValue(element.OwnerDocRoot, out var subDoc))
                    return subDoc;
                // For main document elements, return the main document JSObject
                return _documentJSObject ?? JSNull.Value;
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
                return ToJSObject(element.Parent);
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

                    bridgeForSet.InvalidateStyleScope(element);
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
                    bridgeForSet.InvalidateStyleScope(element);
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
                    return new JSString(val);
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
                        bridgeForSet.InvalidateStyleScope(element);
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
        var bridgeForInsert = this;
        obj.FastAddValue(
            (KeyString)"insertBefore",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSUndefined.Value;
                var newChildObj = a[0] as JSObject;
                if (newChildObj == null) return JSUndefined.Value;
                var newEl = FindDomElementByJSObject(newChildObj);
                if (newEl == null) return a[0];

                // Prevent circular references (HierarchyRequestError per DOM spec)
                if (ReferenceEquals(newEl, element) || IsDescendant(newEl, element))
                    ThrowDOMException(_jsContext!, "The new child element contains the parent.", "HierarchyRequestError");
                if (a.Length < 2 || a[1].IsNull || a[1].IsUndefined)
                {
                    newEl.Parent?.Children.Remove(newEl);
                    newEl.Parent = element;
                    element.Children.Add(newEl);
                    bridgeForInsert.InvalidateStyleScope(element);

                    // Fire onload for iframe/object children after DOM insertion
                    var ntag = newEl.TagName?.ToLowerInvariant();
                    if (ntag == "iframe" || ntag == "object")
                        bridgeForInsert.FireSubDocumentOnload(newEl);
                    else
                        bridgeForInsert.FireDescendantOnloads(newEl);

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
                bridgeForInsert.InvalidateStyleScope(element);

                // Fire onload for iframe/object children after DOM insertion
                var newTag = newEl.TagName?.ToLowerInvariant();
                if (newTag == "iframe" || newTag == "object")
                    bridgeForInsert.FireSubDocumentOnload(newEl);
                else
                    bridgeForInsert.FireDescendantOnloads(newEl);

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
                return first != null ? ToJSObject(first) : JSNull.Value;
            }, "get firstElementChild"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastElementChild (read-only)
        obj.FastAddProperty(
            (KeyString)"lastElementChild",
            new JSFunction((in Arguments a) =>
            {
                var last = element.Children.LastOrDefault(c => !c.IsTextNode);
                return last != null ? ToJSObject(last) : JSNull.Value;
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
                    if (!siblings[i].IsTextNode) return ToJSObject(siblings[i]);
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
                    if (!siblings[i].IsTextNode) return ToJSObject(siblings[i]);
                }
                return JSNull.Value;
            }, "get previousElementSibling"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // -- DOM manipulation methods --

        // appendChild(child)
        var bridgeForAppend = this;
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

                // Prevent circular references (HierarchyRequestError per DOM spec)
                if (ReferenceEquals(childEl, element) || IsDescendant(childEl, element))
                    ThrowDOMException(_jsContext!, "The new child element contains the parent.", "HierarchyRequestError");
                childEl.Parent?.Children.Remove(childEl);
                childEl.Parent = element;
                element.Children.Add(childEl);
                bridgeForAppend.InvalidateStyleScope(element);

                // Fire onload for iframe/object children after DOM insertion
                var childTag = childEl.TagName?.ToLowerInvariant();
                if (childTag == "iframe" || childTag == "object")
                    bridgeForAppend.FireSubDocumentOnload(childEl);
                else
                    bridgeForAppend.FireDescendantOnloads(childEl);

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
                if (childEl == null) return a[0];
                var idx = element.Children.IndexOf(childEl);
                if (idx < 0) return a[0];
                NotifyNodeIteratorPreRemoval(childEl);
                element.Children.RemoveAt(idx);
                childEl.Parent = null;
                bridgeForAppend.InvalidateStyleScope(element);
                NotifyChildRemoved(element, childEl, idx);
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

                // Prevent circular references (HierarchyRequestError per DOM spec)
                if (ReferenceEquals(newEl, element) || IsDescendant(newEl, element))
                    ThrowDOMException(_jsContext!, "The new child element contains the parent.", "HierarchyRequestError");

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
                bridgeForAppend.InvalidateStyleScope(element);
                return a[1]; // returns the old child
            }, "replaceChild", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // remove() — ChildNode.remove() per DOM Living Standard
        obj.FastAddValue(
            (KeyString)"remove",
            new JSFunction((in Arguments a) =>
            {
                if (element.Parent != null)
                {
                    var idx = element.Parent.Children.IndexOf(element);
                    if (idx >= 0)
                    {
                        NotifyNodeIteratorPreRemoval(element);
                        element.Parent.Children.RemoveAt(idx);
                        var parent = element.Parent;
                        element.Parent = null;
                        InvalidateStyleScope(parent);
                        NotifyChildRemoved(parent, element, idx);
                    }
                }
                return JSUndefined.Value;
            }, "remove", 0),
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
        // For checkboxes and radio buttons, toggles checked state.
        obj.FastAddValue(
            (KeyString)"click",
            new JSFunction((in Arguments _) =>
            {
                // Toggle checked state for checkboxes/radio buttons (per HTML spec)
                if (string.Equals(element.TagName, "input", StringComparison.OrdinalIgnoreCase))
                {
                    var inputType = element.Attributes.TryGetValue("type", out var t) ? t.ToLowerInvariant() : "text";
                    if (inputType == "checkbox")
                    {
                        bool wasChecked = element.DomProperties.TryGetValue("checked", out var cv) && cv is true
                            || (!element.DomProperties.ContainsKey("checked") && element.Attributes.ContainsKey("checked"));
                        element.DomProperties["checked"] = !wasChecked;
                    }
                    else if (inputType == "radio")
                    {
                        element.DomProperties["checked"] = true;
                        // Radio mutual exclusion
                        if (element.Attributes.TryGetValue("name", out var radioName) && !string.IsNullOrEmpty(radioName))
                        {
                            var scope = element;
                            while (scope.Parent != null) scope = scope.Parent;
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

                // Per HTML spec: clicking a submit button triggers form submission
                if (string.Equals(element.TagName, "input", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(element.TagName, "button", StringComparison.OrdinalIgnoreCase))
                {
                    var btnType = "text";
                    if (element.Attributes.TryGetValue("type", out var bt))
                        btnType = bt.ToLowerInvariant();
                    else if (string.Equals(element.TagName, "button", StringComparison.OrdinalIgnoreCase))
                        btnType = "submit"; // <button> defaults to type="submit" per HTML spec

                    if (btnType == "submit")
                    {
                        // Walk up the DOM tree to find the parent <form>
                        var form = element.Parent;
                        while (form != null && !string.Equals(form.TagName, "form", StringComparison.OrdinalIgnoreCase))
                            form = form.Parent;
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
                            submitEvt.FastAddValue((KeyString)"preventDefault",
                                new JSFunction((in Arguments __) =>
                                {
                                    submitEvt[(KeyString)"defaultPrevented"] = JSBoolean.True;
                                    return JSUndefined.Value;
                                }, "preventDefault", 0),
                                JSPropertyAttributes.EnumerableConfigurableValue);
                            submitEvt.FastAddValue((KeyString)"stopPropagation",
                                new JSFunction((in Arguments __) => JSUndefined.Value, "stopPropagation", 0),
                                JSPropertyAttributes.EnumerableConfigurableValue);
                            submitEvt.FastAddValue((KeyString)"stopImmediatePropagation",
                                new JSFunction((in Arguments __) => JSUndefined.Value, "stopImmediatePropagation", 0),
                                JSPropertyAttributes.EnumerableConfigurableValue);
                            bridge.DispatchEventOnElement(form, submitEvt);
                        }
                    }
                }

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
        // Uses DomProperties["checked"] as the "dirty" IDL state that tracks
        // programmatic changes. setAttribute("checked") only sets the content
        // attribute and does NOT affect this IDL state.
        obj.FastAddProperty(
            (KeyString)"checked",
            new JSFunction((in Arguments a) =>
            {
                // IDL property takes precedence over content attribute
                if (element.DomProperties.TryGetValue("checked", out var v))
                    return v is true ? JSBoolean.True : JSBoolean.False;
                return element.Attributes.ContainsKey("checked") ? JSBoolean.True : JSBoolean.False;
            }, "get checked"),
            new JSFunction((in Arguments a) =>
            {
                bool newVal = a.Length > 0 && a[0].BooleanValue;
                element.DomProperties["checked"] = newVal;
                if (newVal)
                {
                    // Radio button mutual exclusion: uncheck others in same group
                    if (element.Attributes.TryGetValue("type", out var tp) &&
                        string.Equals(tp, "radio", StringComparison.OrdinalIgnoreCase) &&
                        element.Attributes.TryGetValue("name", out var radioName) &&
                        !string.IsNullOrEmpty(radioName))
                    {
                        // Find the scope for radio group — form parent, or document root if not in a form
                        var scope = element.Parent;
                        while (scope != null && !string.Equals(scope.TagName, "form", StringComparison.OrdinalIgnoreCase))
                            scope = scope.Parent;
                        if (scope == null)
                        {
                            scope = element;
                            while (scope.Parent != null) scope = scope.Parent;
                        }
                        UncheckRadioSiblings(scope, element, radioName);
                    }
                }
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
                    bridge.InvalidateStyleScope(element);
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
                    bridge.InvalidateStyleScope(element);
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
                if (!string.Equals(contextType, "2d", StringComparison.OrdinalIgnoreCase))
                    return JSNull.Value;
                if (!string.Equals(element.TagName, "canvas", StringComparison.OrdinalIgnoreCase))
                    return JSNull.Value;
#if BROILER_CLI
                return JSNull.Value; // Canvas 2D context not available in CLI mode
#else
                return BuildCanvas2DContext(element);
#endif
            }, "getContext", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // contentWindow / contentDocument — for <iframe> elements with full sub-document DOM
        if (string.Equals(element.TagName, "iframe", StringComparison.OrdinalIgnoreCase))
        {
            var iframeSrcValue = element.Attributes.TryGetValue("src", out var srcVal) ? srcVal : string.Empty;
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
                    return GetOrCreateSubWindow(element);
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
            var bridgeForSrc = this;
            obj.FastAddProperty(
                (KeyString)"src",
                new JSFunction((in Arguments _) =>
                {
                    return element.Attributes.TryGetValue("src", out var s)
                        ? new JSString(s)
                        : new JSString(string.Empty);
                }, "get src"),
                new JSFunction((in Arguments a) =>
                {
                    element.Attributes["src"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                    // Invalidate cached sub-document when src changes
                    InvalidateCachedSubDocument(element);
                    _onloadFired.Remove(element);
                    // Fire onload for the new resource
                    bridgeForSrc.FireSubDocumentOnload(element);
                    return JSUndefined.Value;
                }, "set src"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // sandbox attribute access
            obj.FastAddProperty(
                (KeyString)"sandbox",
                new JSFunction((in Arguments _) =>
                {
                    return element.Attributes.TryGetValue("sandbox", out var sandbox)
                        ? new JSString(sandbox)
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
                    var cap = element.Children.Find(c => string.Equals(c.TagName, "caption", StringComparison.OrdinalIgnoreCase));
                    return cap != null ? ToJSObject(cap) : JSNull.Value;
                }, "get caption"),
                new JSFunction((in Arguments a) => JSUndefined.Value, "set caption"), // setting to same value is no-op
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // tHead (get/set) — returns first <thead> child or null
            obj.FastAddProperty(
                (KeyString)"tHead",
                new JSFunction((in Arguments _) =>
                {
                    var th = element.Children.Find(c => string.Equals(c.TagName, "thead", StringComparison.OrdinalIgnoreCase));
                    return th != null ? ToJSObject(th) : JSNull.Value;
                }, "get tHead"),
                new JSFunction((in Arguments a) => JSUndefined.Value, "set tHead"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // tFoot (get/set) — returns first <tfoot> child or null
            obj.FastAddProperty(
                (KeyString)"tFoot",
                new JSFunction((in Arguments _) =>
                {
                    var tf = element.Children.Find(c => string.Equals(c.TagName, "tfoot", StringComparison.OrdinalIgnoreCase));
                    return tf != null ? ToJSObject(tf) : JSNull.Value;
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
                        if (string.Equals(c.TagName, "tbody", StringComparison.OrdinalIgnoreCase))
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
                    var cap = element.Children.Find(c => string.Equals(c.TagName, "caption", StringComparison.OrdinalIgnoreCase));
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
                    var th = element.Children.Find(c => string.Equals(c.TagName, "thead", StringComparison.OrdinalIgnoreCase));
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
                    var tf = element.Children.Find(c => string.Equals(c.TagName, "tfoot", StringComparison.OrdinalIgnoreCase));
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
                    var cap = element.Children.Find(c => string.Equals(c.TagName, "caption", StringComparison.OrdinalIgnoreCase));
                    if (cap != null) { cap.Parent = null; element.Children.Remove(cap); }
                    return JSUndefined.Value;
                }, "deleteCaption", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // deleteTHead()
            obj.FastAddValue(
                (KeyString)"deleteTHead",
                new JSFunction((in Arguments _) =>
                {
                    var th = element.Children.Find(c => string.Equals(c.TagName, "thead", StringComparison.OrdinalIgnoreCase));
                    if (th != null) { th.Parent = null; element.Children.Remove(th); }
                    return JSUndefined.Value;
                }, "deleteTHead", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // deleteTFoot()
            obj.FastAddValue(
                (KeyString)"deleteTFoot",
                new JSFunction((in Arguments _) =>
                {
                    var tf = element.Children.Find(c => string.Equals(c.TagName, "tfoot", StringComparison.OrdinalIgnoreCase));
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
                        if (string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase))
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
                        string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase)).ToList();
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
                    if (tableEl != null && (string.Equals(tableEl.TagName, "thead", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(tableEl.TagName, "tbody", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(tableEl.TagName, "tfoot", StringComparison.OrdinalIgnoreCase)))
                        tableEl = tableEl.Parent;
                    if (tableEl == null || !string.Equals(tableEl.TagName, "table", StringComparison.OrdinalIgnoreCase))
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
                        if (string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase)) idx++;
                    }
                    return new JSNumber(-1);
                }, "get sectionRowIndex"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // cells — returns collection of <td>/<th> children
            obj.FastAddProperty(
                (KeyString)"cells",
                new JSFunction((in Arguments _) =>
                {
                    var cells = new List<JSValue>();
                    foreach (var c in element.Children)
                        if (string.Equals(c.TagName, "td", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(c.TagName, "th", StringComparison.OrdinalIgnoreCase))
                            cells.Add(ToJSObject(c));
                    var arr = new JSArray(cells);
                    arr.FastAddProperty(
                        (KeyString)"length",
                        new JSFunction((in Arguments __) => new JSNumber(cells.Count), "get length"),
                        null, JSPropertyAttributes.EnumerableConfigurableProperty);
                    return arr;
                }, "get cells"),
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
                        ? new JSString(act)
                        : new JSString(string.Empty),
                    "get action"),
                new JSFunction((in Arguments a) =>
                {
                    element.Attributes["action"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                    return JSUndefined.Value;
                }, "set action"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        if (tag == "details")
        {
            obj.FastAddProperty(
                (KeyString)"open",
                new JSFunction((in Arguments _) =>
                    element.Attributes.ContainsKey("open") ? JSBoolean.True : JSBoolean.False,
                    "get open"),
                new JSFunction((in Arguments a) =>
                {
                    if (a.Length > 0 && a[0].BooleanValue)
                        element.Attributes["open"] = "";
                    else
                        element.Attributes.Remove("open");

                    bridge.InvalidateStyleScope(element);
                    return JSUndefined.Value;
                }, "set open"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // HTMLDialogElement interface
        if (tag == "dialog")
        {
            // showModal() — sets the 'open' attribute so the dialog is visible.
            obj.FastAddValue(
                (KeyString)"showModal",
                new JSFunction((in Arguments _) =>
                {
                    element.Attributes["open"] = "";
                    element.DomProperties["_modal"] = true;
                    element.DomProperties["_topLayerOrder"] = ++bridge._topLayerCounter;
                    bridge.InvalidateStyleScope(element);
                    return JSUndefined.Value;
                }, "showModal", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // show() — opens non-modally.
            obj.FastAddValue(
                (KeyString)"show",
                new JSFunction((in Arguments _) =>
                {
                    element.Attributes["open"] = "";
                    bridge.InvalidateStyleScope(element);
                    return JSUndefined.Value;
                }, "show", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // close(returnValue) — removes the 'open' attribute.
            obj.FastAddValue(
                (KeyString)"close",
                new JSFunction((in Arguments a) =>
                {
                    element.Attributes.Remove("open");
                    element.DomProperties.Remove("_modal");
                    if (a.Length > 0)
                        element.DomProperties["_returnValue"] = a[0].ToString();
                    bridge.InvalidateStyleScope(element);
                    return JSUndefined.Value;
                }, "close", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // open (getter/setter)
            obj.FastAddProperty(
                (KeyString)"open",
                new JSFunction((in Arguments _) =>
                    element.Attributes.ContainsKey("open") ? JSBoolean.True : JSBoolean.False,
                    "get open"),
                new JSFunction((in Arguments a) =>
                {
                    if (a.Length > 0 && a[0].BooleanValue)
                        element.Attributes["open"] = "";
                    else
                        element.Attributes.Remove("open");
                    bridge.InvalidateStyleScope(element);
                    return JSUndefined.Value;
                }, "set open"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // returnValue (getter/setter)
            obj.FastAddProperty(
                (KeyString)"returnValue",
                new JSFunction((in Arguments _) =>
                    element.DomProperties.TryGetValue("_returnValue", out var rv) && rv is string s
                        ? new JSString(s) : new JSString(string.Empty),
                    "get returnValue"),
                new JSFunction((in Arguments a) =>
                {
                    element.DomProperties["_returnValue"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                    return JSUndefined.Value;
                }, "set returnValue"),
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
                        if (string.Equals(c.TagName, "option", StringComparison.OrdinalIgnoreCase))
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
                        if (string.Equals(c.TagName, "option", StringComparison.OrdinalIgnoreCase))
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

            obj.FastAddProperty(
                (KeyString)"size",
                new JSFunction((in Arguments _) =>
                {
                    if (element.Attributes.TryGetValue("size", out var rawSize) &&
                        int.TryParse(rawSize, out var parsedSize) &&
                        parsedSize > 0)
                    {
                        return new JSNumber(parsedSize);
                    }

                    return new JSNumber(0);
                }, "get size"),
                new JSFunction((in Arguments a) =>
                {
                    if (a.Length == 0)
                        return JSUndefined.Value;

                    var size = (int)Math.Truncate(a[0].DoubleValue);
                    if (size > 0)
                        element.Attributes["size"] = size.ToString();
                    else
                        element.Attributes.Remove("size");
                    return JSUndefined.Value;
                }, "set size"),
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
                        ? new JSString(f) : new JSString(string.Empty),
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
                        ? new JSString(he) : new JSString(string.Empty),
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
                    bridge.InvalidateCachedSubDocument(element);
                    return JSUndefined.Value;
                }, "set data"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // type property (MIME type of the resource)
            obj.FastAddProperty(
                (KeyString)"type",
                new JSFunction((in Arguments _) =>
                    element.Attributes.TryGetValue("type", out var t)
                        ? new JSString(t) : new JSString(string.Empty),
                    "get type"),
                new JSFunction((in Arguments a) =>
                {
                    element.Attributes["type"] = a.Length > 0 ? a[0].ToString() : string.Empty;
                    return JSUndefined.Value;
                }, "set type"),
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // contentDocument for <object> element (with same-origin check)
            // Returns null when the resource fails to load (HTTP 404, file not found, etc.)
            // which signals that the fallback content (child nodes) should be visible.
            obj.FastAddProperty(
                (KeyString)"contentDocument",
                new JSFunction((in Arguments _) =>
                {
                    var dataUrl = element.Attributes.TryGetValue("data", out var d) ? d : string.Empty;
                    if (IsCrossOrigin(dataUrl, bridge._pageUrl)) return JSNull.Value;
                    // Check if the resource actually loaded successfully
                    if (bridge.IsObjectLoadFailed(element)) return JSNull.Value;
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
                        element.Attributes.TryGetValue(captured, out var v) ? new JSString(v) : new JSString(string.Empty),
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

        // HTMLImageElement — height/width return computed CSS value or HTML attribute
        if (tag == "img")
        {
            foreach (var dim in new[] { "height", "width" })
            {
                var dimName = dim;
                obj.FastAddProperty(
                    (KeyString)dimName,
                    new JSFunction((in Arguments _) =>
                    {
                        // First check computed style for this element
                        var computed = bridge.BuildComputedStyleObject(element);
                        var csVal = computed[(KeyString)dimName];
                        if (csVal != null && !csVal.IsNull && !csVal.IsUndefined)
                        {
                            var cssStr = csVal.ToString();
                            if (!string.IsNullOrEmpty(cssStr))
                            {
                                var px = ParseCssLengthToPixels(cssStr);
                                if (!double.IsNaN(px)) return new JSNumber(px);
                            }
                        }
                        // Fallback: HTML attribute
                        if (element.Attributes.TryGetValue(dimName, out var attrVal) &&
                            double.TryParse(attrVal, out var attrNum))
                            return new JSNumber(attrNum);
                        return new JSNumber(0);
                    }, "get " + dimName),
                    new JSFunction((in Arguments a) =>
                    {
                        element.Attributes[dimName] = a.Length > 0 ? a[0].ToString() : "0";
                        return JSUndefined.Value;
                    }, "set " + dimName),
                    JSPropertyAttributes.EnumerableConfigurableProperty);
            }
        }

        // -- TODO-G4 / TODO-G19: Box model properties for all elements --
        // clientWidth/clientHeight, offsetWidth/offsetHeight, scrollWidth/scrollHeight,
        // scrollTop/scrollLeft, and getBoundingClientRect()
        {
            var isViewportElement = IsViewportElementForMetrics(element);
            var bridgeForOffset = this;
            var elForOffset = element;

            obj.FastAddProperty(
                (KeyString)"clientTop",
                new JSFunction((in Arguments _) => new JSNumber(bridgeForOffset.GetClientTopForDomElement(elForOffset)), "get clientTop"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
            obj.FastAddProperty(
                (KeyString)"clientLeft",
                new JSFunction((in Arguments _) => new JSNumber(bridgeForOffset.GetClientLeftForDomElement(elForOffset)), "get clientLeft"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
            obj.FastAddProperty(
                (KeyString)"clientWidth",
                new JSFunction((in Arguments _) => new JSNumber(bridgeForOffset.GetClientWidthForDomElement(elForOffset, isViewportElement)), "get clientWidth"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
            obj.FastAddProperty(
                (KeyString)"clientHeight",
                new JSFunction((in Arguments _) => new JSNumber(bridgeForOffset.GetClientHeightForDomElement(elForOffset, isViewportElement)), "get clientHeight"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
            obj.FastAddProperty(
                (KeyString)"offsetWidth",
                new JSFunction((in Arguments _) => new JSNumber(bridgeForOffset.GetOffsetWidthForDomElement(elForOffset, isViewportElement)), "get offsetWidth"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
            obj.FastAddProperty(
                (KeyString)"offsetHeight",
                new JSFunction((in Arguments _) => new JSNumber(bridgeForOffset.GetOffsetHeightForDomElement(elForOffset, isViewportElement)), "get offsetHeight"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
            obj.FastAddProperty(
                (KeyString)"scrollWidth",
                new JSFunction((in Arguments _) => new JSNumber(bridgeForOffset.GetScrollWidthForDomElement(elForOffset, isViewportElement)), "get scrollWidth"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
            obj.FastAddProperty(
                (KeyString)"scrollHeight",
                new JSFunction((in Arguments _) => new JSNumber(bridgeForOffset.GetScrollHeightForDomElement(elForOffset, isViewportElement)), "get scrollHeight"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
            obj.FastAddProperty(
                (KeyString)"scrollTop",
                new JSFunction((in Arguments _) =>
                {
                    if (bridgeForOffset.GetElementScrollOffset(element, vertical: true) is double sv)
                        return new JSNumber(sv);
                    return new JSNumber(0);
                }, "get scrollTop"),
                new JSFunction((in Arguments a) =>
                {
                    if (a.Length > 0)
                        bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, top: a[0].DoubleValue);
                    return JSUndefined.Value;
                }, "set scrollTop"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
            obj.FastAddProperty(
                (KeyString)"scrollLeft",
                new JSFunction((in Arguments _) =>
                {
                    if (bridgeForOffset.GetElementScrollOffset(element, vertical: false) is double sv)
                        return new JSNumber(sv);
                    return new JSNumber(0);
                }, "get scrollLeft"),
                new JSFunction((in Arguments a) =>
                {
                    if (a.Length > 0)
                        bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left: a[0].DoubleValue);
                    return JSUndefined.Value;
                }, "set scrollLeft"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
            obj.FastAddProperty(
                (KeyString)"offsetTop",
                new JSFunction((in Arguments _) =>
                {
                    return new JSNumber(bridgeForOffset.GetOffsetTopForDomElement(elForOffset));
                }, "get offsetTop"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
            obj.FastAddProperty(
                (KeyString)"offsetLeft",
                new JSFunction((in Arguments _) =>
                {
                    return new JSNumber(bridgeForOffset.GetOffsetLeftForDomElement(elForOffset));
                }, "get offsetLeft"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);
            obj.FastAddProperty(
                (KeyString)"offsetParent",
                new JSFunction((in Arguments _) =>
                {
                    var offsetParent = bridgeForOffset.GetOffsetParentForDomElement(elForOffset);
                    return offsetParent != null ? bridgeForOffset.ToJSObject(offsetParent) : JSNull.Value;
                }, "get offsetParent"),
                null,
                JSPropertyAttributes.EnumerableConfigurableProperty);

            // getBoundingClientRect() — returns DOMRect-like object
            obj.FastAddValue(
                (KeyString)"getBoundingClientRect",
                new JSFunction((in Arguments _) =>
                {
                    var rectData = bridgeForOffset.GetBoundingClientRectForDomElement(elForOffset, isViewportElement);
                    var rect = new JSObject();
                    rect.FastAddValue((KeyString)"x", new JSNumber(rectData.Left), JSPropertyAttributes.EnumerableConfigurableValue);
                    rect.FastAddValue((KeyString)"y", new JSNumber(rectData.Top), JSPropertyAttributes.EnumerableConfigurableValue);
                    rect.FastAddValue((KeyString)"top", new JSNumber(rectData.Top), JSPropertyAttributes.EnumerableConfigurableValue);
                    rect.FastAddValue((KeyString)"left", new JSNumber(rectData.Left), JSPropertyAttributes.EnumerableConfigurableValue);
                    rect.FastAddValue((KeyString)"right", new JSNumber(rectData.Left + rectData.Width), JSPropertyAttributes.EnumerableConfigurableValue);
                    rect.FastAddValue((KeyString)"bottom", new JSNumber(rectData.Top + rectData.Height), JSPropertyAttributes.EnumerableConfigurableValue);
                    rect.FastAddValue((KeyString)"width", new JSNumber(rectData.Width), JSPropertyAttributes.EnumerableConfigurableValue);
                    rect.FastAddValue((KeyString)"height", new JSNumber(rectData.Height), JSPropertyAttributes.EnumerableConfigurableValue);
                    return rect;
                }, "getBoundingClientRect", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            // getClientRects() — returns array with one DOMRect for root elements
            obj.FastAddValue(
                (KeyString)"getClientRects",
                new JSFunction((in Arguments a2) =>
                {
                    var rectData = bridgeForOffset.GetBoundingClientRectForDomElement(elForOffset, isViewportElement);
                    var rect = new JSObject();
                    rect.FastAddValue((KeyString)"x", new JSNumber(rectData.Left), JSPropertyAttributes.EnumerableConfigurableValue);
                    rect.FastAddValue((KeyString)"y", new JSNumber(rectData.Top), JSPropertyAttributes.EnumerableConfigurableValue);
                    rect.FastAddValue((KeyString)"top", new JSNumber(rectData.Top), JSPropertyAttributes.EnumerableConfigurableValue);
                    rect.FastAddValue((KeyString)"left", new JSNumber(rectData.Left), JSPropertyAttributes.EnumerableConfigurableValue);
                    rect.FastAddValue((KeyString)"right", new JSNumber(rectData.Left + rectData.Width), JSPropertyAttributes.EnumerableConfigurableValue);
                    rect.FastAddValue((KeyString)"bottom", new JSNumber(rectData.Top + rectData.Height), JSPropertyAttributes.EnumerableConfigurableValue);
                    rect.FastAddValue((KeyString)"width", new JSNumber(rectData.Width), JSPropertyAttributes.EnumerableConfigurableValue);
                    rect.FastAddValue((KeyString)"height", new JSNumber(rectData.Height), JSPropertyAttributes.EnumerableConfigurableValue);
                    return rectData.Width > 0 || rectData.Height > 0 || isViewportElement
                        ? new JSArray(new JSValue[] { rect })
                        : new JSArray();
                }, "getClientRects", 0),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue(
                (KeyString)"scrollIntoView",
                new JSFunction((in Arguments a) =>
                {
                    var options = bridgeForOffset.GetScrollIntoViewOptions(a);
                    bridgeForOffset.ScrollElementIntoView(elForOffset, options.Block, options.Inline, options.Behavior);
                    return JSUndefined.Value;
                }, "scrollIntoView", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);
            obj.FastAddValue(
                (KeyString)"scroll",
                new JSFunction((in Arguments a) =>
                {
                    var (left, top, behavior) = bridgeForOffset.GetScrollArguments(a);
                    bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left, top, clamp: false, behavior: behavior);
                    return JSUndefined.Value;
                }, "scroll", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);
            obj.FastAddValue(
                (KeyString)"scrollTo",
                new JSFunction((in Arguments a) =>
                {
                    var (left, top, behavior) = bridgeForOffset.GetScrollArguments(a);
                    bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left, top, clamp: false, behavior: behavior);
                    return JSUndefined.Value;
                }, "scrollTo", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);
            obj.FastAddValue(
                (KeyString)"scrollBy",
                new JSFunction((in Arguments a) =>
                {
                    var (left, top, behavior) = bridgeForOffset.GetScrollArguments(a);
                    bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left, top, relative: true, clamp: false, behavior: behavior);
                    return JSUndefined.Value;
                }, "scrollBy", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);
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

                // getComputedTextLength() — returns estimated total advance width
                obj.FastAddValue(
                    (KeyString)"getComputedTextLength",
                    new JSFunction((in Arguments _) =>
                    {
                        var sb = new StringBuilder();
                        CollectTextContent(element, sb);
                        // Stub: estimate using font-size * character count * 0.6 average advance ratio
                        double fontSize = 16;
                        if (element.Attributes.TryGetValue("font-size", out var fs))
                        {
                            var fsClean = fs.Replace("px", "").Replace("pt", "").Trim();
                            double.TryParse(fsClean, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out fontSize);
                        }
                        return new JSNumber(sb.Length * fontSize * 0.6);
                    }, "getComputedTextLength", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);

                // getSubStringLength(charnum, nchars) — returns advance width of substring
                obj.FastAddValue(
                    (KeyString)"getSubStringLength",
                    new JSFunction((in Arguments a) =>
                    {
                        var sb = new StringBuilder();
                        CollectTextContent(element, sb);
                        var charnum = a.Length > 0 ? (int)a[0].DoubleValue : 0;
                        var nchars = a.Length > 1 ? (int)a[1].DoubleValue : 0;
                        if (charnum < 0 || charnum >= sb.Length)
                            throw new JSException("INDEX_SIZE_ERR");
                        if (nchars == 0) return new JSNumber(0);
                        double fontSize = 16;
                        if (element.Attributes.TryGetValue("font-size", out var fs))
                        {
                            var fsClean = fs.Replace("px", "").Replace("pt", "").Trim();
                            double.TryParse(fsClean, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out fontSize);
                        }
                        return new JSNumber(nchars * fontSize * 0.6);
                    }, "getSubStringLength", 2),
                    JSPropertyAttributes.EnumerableConfigurableValue);

                // getStartPositionOfChar(charnum) — returns SVGPoint {x, y}
                obj.FastAddValue(
                    (KeyString)"getStartPositionOfChar",
                    new JSFunction((in Arguments a) =>
                    {
                        var sb = new StringBuilder();
                        CollectTextContent(element, sb);
                        var charnum = a.Length > 0 ? (int)a[0].DoubleValue : 0;
                        if (charnum < 0 || charnum >= sb.Length)
                            throw new JSException("INDEX_SIZE_ERR");
                        double fontSize = 16;
                        if (element.Attributes.TryGetValue("font-size", out var fs))
                        {
                            var fsClean = fs.Replace("px", "").Replace("pt", "").Trim();
                            double.TryParse(fsClean, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out fontSize);
                        }
                        var pt = new JSObject();
                        pt.FastAddValue((KeyString)"x", new JSNumber(charnum * fontSize * 0.6), JSPropertyAttributes.EnumerableConfigurableValue);
                        pt.FastAddValue((KeyString)"y", new JSNumber(fontSize), JSPropertyAttributes.EnumerableConfigurableValue);
                        return pt;
                    }, "getStartPositionOfChar", 1),
                    JSPropertyAttributes.EnumerableConfigurableValue);

                // getEndPositionOfChar(charnum) — returns SVGPoint {x, y}
                obj.FastAddValue(
                    (KeyString)"getEndPositionOfChar",
                    new JSFunction((in Arguments a) =>
                    {
                        var sb = new StringBuilder();
                        CollectTextContent(element, sb);
                        var charnum = a.Length > 0 ? (int)a[0].DoubleValue : 0;
                        if (charnum < 0 || charnum >= sb.Length)
                            throw new JSException("INDEX_SIZE_ERR");
                        double fontSize = 16;
                        if (element.Attributes.TryGetValue("font-size", out var fs))
                        {
                            var fsClean = fs.Replace("px", "").Replace("pt", "").Trim();
                            double.TryParse(fsClean, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out fontSize);
                        }
                        var pt = new JSObject();
                        pt.FastAddValue((KeyString)"x", new JSNumber((charnum + 1) * fontSize * 0.6), JSPropertyAttributes.EnumerableConfigurableValue);
                        pt.FastAddValue((KeyString)"y", new JSNumber(fontSize), JSPropertyAttributes.EnumerableConfigurableValue);
                        return pt;
                    }, "getEndPositionOfChar", 1),
                    JSPropertyAttributes.EnumerableConfigurableValue);

                // getRotationOfChar(charnum) — returns rotation angle in degrees
                obj.FastAddValue(
                    (KeyString)"getRotationOfChar",
                    new JSFunction((in Arguments a) =>
                    {
                        var sb = new StringBuilder();
                        CollectTextContent(element, sb);
                        var charnum = a.Length > 0 ? (int)a[0].DoubleValue : 0;
                        if (charnum < 0 || charnum >= sb.Length)
                            throw new JSException("INDEX_SIZE_ERR");
                        // Default rotation is 0 degrees (horizontal text)
                        return new JSNumber(0);
                    }, "getRotationOfChar", 1),
                    JSPropertyAttributes.EnumerableConfigurableValue);
            }

            // SVGSVGElement methods (getCurrentTime, setCurrentTime)
            if (tag == "svg" || tag == "svg:svg")
            {
                double currentTime = 0;

                obj.FastAddValue(
                    (KeyString)"getCurrentTime",
                    new JSFunction((in Arguments _) => new JSNumber(currentTime), "getCurrentTime", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);

                obj.FastAddValue(
                    (KeyString)"setCurrentTime",
                    new JSFunction((in Arguments a) =>
                    {
                        if (a.Length > 0)
                            currentTime = a[0].DoubleValue;
                        return JSUndefined.Value;
                    }, "setCurrentTime", 1),
                    JSPropertyAttributes.EnumerableConfigurableValue);
            }

            // SMIL animation element methods (beginElement, endElement, getStartTime)
            if (tag == "set" || tag == "svg:set" ||
                tag == "animate" || tag == "svg:animate" ||
                tag == "animatetransform" || tag == "svg:animatetransform" ||
                tag == "animatemotion" || tag == "svg:animatemotion")
            {
                obj.FastAddValue(
                    (KeyString)"beginElement",
                    new JSFunction((in Arguments _) => JSUndefined.Value, "beginElement", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);

                obj.FastAddValue(
                    (KeyString)"endElement",
                    new JSFunction((in Arguments _) => JSUndefined.Value, "endElement", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);

                obj.FastAddValue(
                    (KeyString)"getStartTime",
                    new JSFunction((in Arguments _) => new JSNumber(0), "getStartTime", 0),
                    JSPropertyAttributes.EnumerableConfigurableValue);
            }
        }

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

    private double GetClientWidthForDomElement(DomElement element, bool isRoot)
    {
        if (isRoot)
            return GetViewportReferenceLength(element, vertical: false);

        var props = GetComputedProps(element);
        var containingBlockWidth = ResolveContainingBlockReferenceLength(element, vertical: false);
        return ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("width"), element, percentageBasis: containingBlockWidth)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-left"), element, percentageBasis: containingBlockWidth)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-right"), element, percentageBasis: containingBlockWidth);
    }

    private double GetClientHeightForDomElement(DomElement element, bool isRoot)
    {
        if (isRoot)
            return GetViewportReferenceLength(element, vertical: true);

        var props = GetComputedProps(element);
        var containingBlockWidth = ResolveContainingBlockReferenceLength(element, vertical: false);
        var containingBlockHeight = ResolveContainingBlockReferenceLength(element, vertical: true);
        return ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("height"), element, percentageBasis: containingBlockHeight)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-top"), element, percentageBasis: containingBlockWidth)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-bottom"), element, percentageBasis: containingBlockWidth);
    }

    private double GetClientTopForDomElement(DomElement element)
    {
        var props = GetComputedProps(element);
        return ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-top-width"), element);
    }

    private double GetClientLeftForDomElement(DomElement element)
    {
        var props = GetComputedProps(element);
        return ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-left-width"), element);
    }

    private double GetOffsetWidthForDomElement(DomElement element, bool isRoot)
    {
        if (isRoot)
            return GetViewportReferenceLength(element, vertical: false);

        var resolved = ResolvePositionAreaForElement(element);
        if (resolved != null)
            return resolved.Value.width;

        var props = GetComputedProps(element);
        var width = GetBorderBoxWidth(props, element);
        if (width > 0)
            return width;

        return 0;
    }

    private double GetOffsetHeightForDomElement(DomElement element, bool isRoot)
    {
        if (isRoot)
            return GetViewportReferenceLength(element, vertical: true);

        var resolved = ResolvePositionAreaForElement(element);
        if (resolved != null)
            return resolved.Value.height;

        var props = GetComputedProps(element);
        var height = GetBorderBoxHeight(props, element);
        if (height > 0)
            return height;

        return 0;
    }

    private double GetOffsetTopForDomElement(DomElement element)
    {
        var resolved = ResolvePositionAreaForElement(element);
        if (resolved != null)
            return resolved.Value.top;

        var offsetParent = GetOffsetParentForDomElement(element);
        if (offsetParent != null)
            return ComputeOffsetRelativeToAncestor(element, offsetParent, vertical: true);

        var layoutRect = ComputeUnzoomedLayoutRect(element);
        return layoutRect.Top;
    }

    private double GetOffsetLeftForDomElement(DomElement element)
    {
        var resolved = ResolvePositionAreaForElement(element);
        if (resolved != null)
            return resolved.Value.left;

        var offsetParent = GetOffsetParentForDomElement(element);
        if (offsetParent != null)
            return ComputeOffsetRelativeToAncestor(element, offsetParent, vertical: false);

        var layoutRect = ComputeUnzoomedLayoutRect(element);
        return layoutRect.Left;
    }

    private DomElement? GetOffsetParentForDomElement(DomElement element)
    {
        if (element.Parent == null ||
            ReferenceEquals(element, DocumentElement) ||
            string.Equals(element.TagName, "html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(element.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var props = GetComputedProps(element);
        if (string.Equals(props.GetValueOrDefault("position"), "fixed", StringComparison.OrdinalIgnoreCase))
            return null;

        var documentElement = GetOwningDocumentElement(element);
        var fallbackBody = FindBodyElement(documentElement);
        for (var current = element.Parent; current != null; current = current.Parent)
        {
            if (string.Equals(current.TagName, "body", StringComparison.OrdinalIgnoreCase))
                return current;

            if (ReferenceEquals(current, documentElement))
                return fallbackBody ?? documentElement;

            var currentProps = GetComputedProps(current);
            var currentPosition = currentProps.GetValueOrDefault("position");
            if (!string.IsNullOrWhiteSpace(currentPosition) &&
                !string.Equals(currentPosition, "static", StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }
        }

        return fallbackBody ?? documentElement;
    }

    private double GetScrollWidthForDomElement(DomElement element, bool isRoot)
    {
        if (TryGetSelectListBoxScrollExtent(element, verticalAxis: false, out var selectScrollWidth))
            return selectScrollWidth;

        var props = GetComputedProps(element);
        var ownWidth = GetClientWidthForDomElement(element, isRoot: false);
        var ownZoom = GetUsedZoomForElement(element);
        var trailingPadding = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-right"), element);
        var maxWidth = ownWidth;
        foreach (var child in EnumerateRenderedDescendants(element))
        {
            var childRect = ComputeRenderedRect(child);
            var widthInContainerSpace = ownZoom > 0.0001 ? (childRect.Width / ownZoom) : childRect.Width;
            var childOffset = ComputeOffsetWithinAncestor(child, element, vertical: false);
            maxWidth = Math.Max(maxWidth, childOffset + widthInContainerSpace + trailingPadding);
        }

        return maxWidth;
    }

    private double GetScrollHeightForDomElement(DomElement element, bool isRoot)
    {
        if (TryGetSelectListBoxScrollExtent(element, verticalAxis: true, out var selectScrollHeight))
            return selectScrollHeight;

        var props = GetComputedProps(element);
        var ownHeight = GetClientHeightForDomElement(element, isRoot: false);
        var ownZoom = GetUsedZoomForElement(element);
        var trailingPadding = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-bottom"), element);
        var maxHeight = ownHeight;
        foreach (var child in EnumerateRenderedDescendants(element))
        {
            var childRect = ComputeRenderedRect(child);
            var heightInContainerSpace = ownZoom > 0.0001 ? (childRect.Height / ownZoom) : childRect.Height;
            var childOffset = ComputeOffsetWithinAncestor(child, element, vertical: true);
            maxHeight = Math.Max(maxHeight, childOffset + heightInContainerSpace + trailingPadding);
        }

        return maxHeight;
    }

    private double ComputeOffsetRelativeToAncestor(DomElement element, DomElement ancestor, bool vertical)
    {
        double offset = 0;
        var current = element;
        while (current.Parent != null && !ReferenceEquals(current.Parent, ancestor))
        {
            offset += ComputeOffsetWithinParentForOffset(current, vertical);
            current = current.Parent;
        }

        if (current.Parent != null && ReferenceEquals(current.Parent, ancestor))
            offset += ComputeOffsetWithinParentForOffset(current, vertical);

        return offset;
    }

    private double ComputeOffsetWithinParentForOffset(DomElement element, bool vertical)
    {
        if (element.Parent == null)
            return 0;

        var props = GetComputedProps(element);
        var position = props.GetValueOrDefault("position");
        var margin = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault(vertical ? "margin-top" : "margin-left"), element);
        var positional = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault(vertical ? "top" : "left"), element);

        if (string.Equals(position, "absolute", StringComparison.OrdinalIgnoreCase))
            return margin + positional;

        double offset = 0;
        if (vertical)
        {
            foreach (var sibling in element.Parent.Children)
            {
                if (ReferenceEquals(sibling, element))
                    break;
                if (sibling.IsTextNode)
                    continue;

                var siblingProps = GetComputedProps(sibling);
                var siblingPosition = siblingProps.GetValueOrDefault("position");
                if (string.Equals(siblingPosition, "absolute", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(siblingPosition, "fixed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!ShouldCollapseTopMarginWithParent(sibling))
                    offset += ParseCssLengthToPixelsWithViewport(siblingProps.GetValueOrDefault("margin-top"), sibling);
                offset += GetBorderBoxHeight(siblingProps, sibling);
                offset += ParseCssLengthToPixelsWithViewport(siblingProps.GetValueOrDefault("margin-bottom"), sibling);
                if (string.Equals(siblingPosition, "relative", StringComparison.OrdinalIgnoreCase))
                    offset += ParseCssLengthToPixelsWithViewport(siblingProps.GetValueOrDefault("top"), sibling);
            }

            if (!ShouldCollapseTopMarginWithParent(element))
                offset += margin;
        }
        else
        {
            offset += margin;
        }

        if (string.Equals(position, "relative", StringComparison.OrdinalIgnoreCase))
            offset += positional;

        return offset;
    }

    private bool ShouldCollapseTopMarginWithParent(DomElement element)
    {
        if (element.Parent == null)
            return false;

        var props = GetComputedProps(element);
        var position = props.GetValueOrDefault("position");
        if (string.Equals(position, "absolute", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(position, "fixed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var sibling in element.Parent.Children)
        {
            if (sibling.IsTextNode)
                continue;
            if (ReferenceEquals(sibling, element))
                break;
            return false;
        }

        var parentProps = GetComputedProps(element.Parent);
        return ParseCssLengthToPixelsWithViewport(parentProps.GetValueOrDefault("border-top-width"), element.Parent) == 0 &&
               ParseCssLengthToPixelsWithViewport(parentProps.GetValueOrDefault("padding-top"), element.Parent) == 0;
    }

    private (double Left, double Top, double Width, double Height) GetBoundingClientRectForDomElement(DomElement element, bool isRoot)
    {
        if (isRoot)
            return (0, 0, GetViewportReferenceLength(element, vertical: false), GetViewportReferenceLength(element, vertical: true));

        return ComputeRenderedRect(element);
    }

    private (double Left, double Top, double Width, double Height) ComputeRenderedRect(DomElement element)
    {
        var layoutRect = ComputeUnzoomedLayoutRect(element);
        var zoom = GetUsedZoomForElement(element);
        var transformScale = GetTransformScale(element);
        return (layoutRect.Left, layoutRect.Top, layoutRect.Width * zoom * transformScale, layoutRect.Height * zoom * transformScale);
    }

    private (double Left, double Top, double Width, double Height) ComputeUnzoomedLayoutRect(DomElement element)
    {
        var props = GetComputedProps(element);
        var containingBlockWidth = ResolveContainingBlockReferenceLength(element, vertical: false);
        var containingBlockHeight = ResolveContainingBlockReferenceLength(element, vertical: true);
        var width = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("width"), element, percentageBasis: containingBlockWidth);
        var height = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("height"), element, percentageBasis: containingBlockHeight);
        var marginTop = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("margin-top"), element, percentageBasis: containingBlockWidth);
        var marginLeft = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("margin-left"), element, percentageBasis: containingBlockWidth);
        var top = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("top"), element, percentageBasis: containingBlockHeight);
        var left = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("left"), element, percentageBasis: containingBlockWidth);
        var position = props.GetValueOrDefault("position");

        if (element.Parent == null || string.Equals(element.TagName, "html", StringComparison.OrdinalIgnoreCase))
            return (0, 0, width, height);

        if (string.Equals(element.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            var specifiedMarginTop = props.GetValueOrDefault("margin-top");
            var specifiedMarginLeft = props.GetValueOrDefault("margin-left");
            var bodyMarginTop = HasExplicitBodyMargin(specifiedMarginTop) ? marginTop : DefaultBodyMarginPixels;
            var bodyMarginLeft = HasExplicitBodyMargin(specifiedMarginLeft) ? marginLeft : DefaultBodyMarginPixels;
            return (bodyMarginLeft, bodyMarginTop, width, height);
        }

        var parentRect = ComputeUnzoomedLayoutRect(element.Parent);
        var parentProps = GetComputedProps(element.Parent);
        var parentBorderTop = ParseCssLengthToPixelsWithViewport(parentProps.GetValueOrDefault("border-top-width"), element.Parent);
        var parentBorderLeft = ParseCssLengthToPixelsWithViewport(parentProps.GetValueOrDefault("border-left-width"), element.Parent);
        var parentPaddingTop = ParseCssLengthToPixelsWithViewport(parentProps.GetValueOrDefault("padding-top"), element.Parent);
        var parentPaddingLeft = ParseCssLengthToPixelsWithViewport(parentProps.GetValueOrDefault("padding-left"), element.Parent);

        var baseTop = parentRect.Top + parentBorderTop + parentPaddingTop;
        var baseLeft = parentRect.Left + parentBorderLeft + parentPaddingLeft;

        if (!string.Equals(position, "absolute", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var sibling in element.Parent.Children)
            {
                if (ReferenceEquals(sibling, element))
                    break;
                if (sibling.IsTextNode)
                    continue;

                var siblingRect = ComputeRenderedRect(sibling);
                var siblingProps = GetComputedProps(sibling);
                var siblingPosition = siblingProps.GetValueOrDefault("position");
                if (string.Equals(siblingPosition, "absolute", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(siblingPosition, "fixed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                baseTop += siblingRect.Height;
                baseTop += ParseCssLengthToPixelsWithViewport(siblingProps.GetValueOrDefault("margin-top"), sibling);
                baseTop += ParseCssLengthToPixelsWithViewport(siblingProps.GetValueOrDefault("margin-bottom"), sibling);
            }
        }

        var resolvedTop = baseTop + marginTop;
        var resolvedLeft = baseLeft + marginLeft;

        if (string.Equals(position, "absolute", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(position, "relative", StringComparison.OrdinalIgnoreCase))
        {
            resolvedTop += top;
            resolvedLeft += left;
        }

        return (resolvedLeft, resolvedTop, width, height);
    }

    private double GetUsedZoomForElement(DomElement element)
    {
        var props = GetComputedProps(element);
        var specifiedZoom = props.GetValueOrDefault("zoom");
        var parentZoom = element.Parent != null ? GetUsedZoomForElement(element.Parent) : 1.0;
        return ResolveSpecifiedZoom(specifiedZoom, parentZoom);
    }

    private static double ResolveSpecifiedZoom(string? specifiedZoom, double parentZoom)
    {
        if (string.IsNullOrWhiteSpace(specifiedZoom) ||
            specifiedZoom.Equals("inherit", StringComparison.OrdinalIgnoreCase) ||
            specifiedZoom.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return parentZoom;
        }

        if (double.TryParse(specifiedZoom, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var zoom) && zoom > 0)
            return parentZoom * zoom;

        return parentZoom;
    }

    private double GetTransformScale(DomElement element)
    {
        var props = GetComputedProps(element);
        var transform = props.GetValueOrDefault("transform");
        if (string.IsNullOrWhiteSpace(transform))
            return 1;

        var match = Regex.Match(transform, @"scale\(\s*(?<value>[-+]?[0-9]*\.?[0-9]+)\s*\)", RegexOptions.IgnoreCase);
        if (match.Success &&
            double.TryParse(match.Groups["value"].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double scale))
        {
            return scale;
        }

        return 1;
    }

    private double GetBorderBoxWidth(Dictionary<string, string> props, DomElement? element = null)
    {
        var containingBlockWidth = element != null ? ResolveContainingBlockReferenceLength(element, vertical: false) : (double?)null;
        return ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("width"), element, percentageBasis: containingBlockWidth)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-left"), element, percentageBasis: containingBlockWidth)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-right"), element, percentageBasis: containingBlockWidth)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-left-width"), element)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-right-width"), element);
    }

    private double GetBorderBoxHeight(Dictionary<string, string> props, DomElement? element = null)
    {
        var containingBlockWidth = element != null ? ResolveContainingBlockReferenceLength(element, vertical: false) : (double?)null;
        var containingBlockHeight = element != null ? ResolveContainingBlockReferenceLength(element, vertical: true) : (double?)null;
        return ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("height"), element, percentageBasis: containingBlockHeight)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-top"), element, percentageBasis: containingBlockWidth)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("padding-bottom"), element, percentageBasis: containingBlockWidth)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-top-width"), element)
             + ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("border-bottom-width"), element);
    }

    private void ScrollElementIntoView(
        DomElement element,
        string? block = null,
        string? inline = null,
        string? behavior = null)
    {
        var current = element;
        for (var i = 0; i < MaxScrollContinuationDepth && current != null; i++)
        {
            var scrollContainer = FindScrollContainer(current) ?? GetOwningDocumentElement(current);
            if (scrollContainer == null)
                return;

            if (IsDocumentElement(scrollContainer) && HasFixedPositionInDocument(current, scrollContainer))
            {
                if (HasActiveVisualViewport())
                {
                    ScrollFixedElementIntoVisualViewport(element, scrollContainer, block, inline);
                    current = GetOuterFrameElement(scrollContainer);
                    continue;
                }

                current = GetOuterFrameElement(scrollContainer);
                continue;
            }

            var scrollTop = ResolveScrollIntoViewOffset(element, scrollContainer, vertical: true, alignment: block);
            var scrollLeft = ResolveScrollIntoViewOffset(element, scrollContainer, vertical: false, alignment: inline);

            SetElementScrollOffsetsWithBehavior(scrollContainer, scrollLeft, scrollTop, clamp: true, behavior: behavior);

            var next = GetOuterScrollContinuationElement(scrollContainer);
            if (next == null || ReferenceEquals(next, current))
                return;

            current = next;
        }
    }

    private (string Block, string Inline, string? Behavior) GetScrollIntoViewOptions(in Arguments args)
    {
        const string defaultBlock = "start";
        const string defaultInline = "start";

        if (args.Length == 0)
            return (defaultBlock, defaultInline, null);

        var first = args[0];
        if (first is JSObject options)
        {
            return (
                NormalizeScrollIntoViewAlignment(GetOptionalStringOption(options, "block"), defaultBlock),
                NormalizeScrollIntoViewAlignment(GetOptionalStringOption(options, "inline"), defaultInline),
                GetOptionalScrollBehavior(options));
        }

        if (first.IsBoolean)
        {
            return first.BooleanValue
                ? (defaultBlock, defaultInline, null)
                : ("end", defaultInline, null);
        }

        return (defaultBlock, defaultInline, null);
    }

    private (double? Left, double? Top, string? Behavior) GetScrollArguments(in Arguments args)
    {
        if (args.Length == 0)
            return (null, null, null);

        if (args[0] is JSObject options)
        {
            return (
                GetOptionalScrollCoordinate(options, "left"),
                GetOptionalScrollCoordinate(options, "top"),
                GetOptionalScrollBehavior(options));
        }

        return (args.Length > 0 ? args[0].DoubleValue : null, args.Length > 1 ? args[1].DoubleValue : null, null);
    }

    private static double? GetOptionalScrollCoordinate(JSObject options, string propertyName)
    {
        var value = options[(KeyString)propertyName];
        return value == null || value.IsUndefined || value.IsNull ? null : value.DoubleValue;
    }

    private static string? GetOptionalScrollBehavior(JSObject options)
    {
        var value = options[(KeyString)"behavior"];
        if (value == null || value.IsUndefined || value.IsNull)
            return null;

        var behavior = value.ToString();
        return string.IsNullOrWhiteSpace(behavior) ? null : behavior;
    }

    private static string? GetOptionalStringOption(JSObject options, string propertyName)
    {
        var value = options[(KeyString)propertyName];
        if (value == null || value.IsUndefined || value.IsNull)
            return null;

        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string NormalizeScrollIntoViewAlignment(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "start" or "center" or "end" or "nearest"
            ? normalized
            : fallback;
    }

    private double GetElementScrollOffset(DomElement element, bool vertical)
    {
        if (!CanProgrammaticallyScroll(element, vertical))
            return 0;

        var propertyName = vertical ? "_scrollTop" : "_scrollLeft";
        return element.DomProperties.TryGetValue(propertyName, out var offset) && offset is double scrollOffset
            ? scrollOffset
            : 0;
    }

    private void SetElementScrollOffsets(DomElement element, double? left = null, double? top = null, bool relative = false, bool clamp = true)
    {
        var (nextLeft, nextTop) = ResolveElementScrollOffsets(element, left, top, relative, clamp);
        element.DomProperties["_scrollLeft"] = nextLeft;
        element.DomProperties["_scrollTop"] = nextTop;
    }

    private (double Left, double Top) ResolveElementScrollOffsets(DomElement element, double? left = null, double? top = null, bool relative = false, bool clamp = true)
    {
        var currentLeft = GetElementScrollOffset(element, vertical: false);
        var currentTop = GetElementScrollOffset(element, vertical: true);

        var nextLeft = left.HasValue ? (relative ? currentLeft + left.Value : left.Value) : currentLeft;
        var nextTop = top.HasValue ? (relative ? currentTop + top.Value : top.Value) : currentTop;

        if (!CanProgrammaticallyScroll(element, vertical: false))
            nextLeft = 0;
        if (!CanProgrammaticallyScroll(element, vertical: true))
            nextTop = 0;

        if (clamp)
        {
            var (minLeft, maxLeft, minTop, maxTop) = GetScrollBounds(element);
            nextLeft = Math.Clamp(nextLeft, minLeft, maxLeft);
            nextTop = Math.Clamp(nextTop, minTop, maxTop);
        }

        return (nextLeft, nextTop);
    }

    private void SetElementScrollOffsetsWithBehavior(
        DomElement element,
        double? left = null,
        double? top = null,
        bool relative = false,
        bool clamp = true,
        string? behavior = null)
    {
        var trackVisualViewport = ReferenceEquals(element, DocumentElement);
        var previousVisualPageLeft = trackVisualViewport ? GetVisualViewportPageOffset(vertical: false) : 0;
        var previousVisualPageTop = trackVisualViewport ? GetVisualViewportPageOffset(vertical: true) : 0;
        var (targetLeft, targetTop) = ResolveElementScrollOffsets(element, left, top, relative, clamp);
        var effectiveBehavior = ResolveScrollBehavior(element, behavior);
        CancelSmoothScroll(element);

        if (string.Equals(effectiveBehavior, "smooth", StringComparison.OrdinalIgnoreCase))
        {
            var currentLeft = GetElementScrollOffset(element, vertical: false);
            var currentTop = GetElementScrollOffset(element, vertical: true);
            // Approximate smooth scrolling with a visible intermediate frame before
            // finishing on the next queued frame.
            element.DomProperties["_scrollLeft"] = currentLeft + ((targetLeft - currentLeft) / 2.0);
            element.DomProperties["_scrollTop"] = currentTop + ((targetTop - currentTop) / 2.0);
            NotifyVisualViewportScrollIfNeeded(previousVisualPageLeft, previousVisualPageTop, trackVisualViewport);

            var token = ++_frameActionIdCounter;
            _smoothScrollTokens[element] = token;
            QueueFrameAction(() =>
            {
                if (_smoothScrollTokens.TryGetValue(element, out var activeToken) && activeToken == token)
                {
                    var queuedPreviousVisualPageLeft = trackVisualViewport ? GetVisualViewportPageOffset(vertical: false) : 0;
                    var queuedPreviousVisualPageTop = trackVisualViewport ? GetVisualViewportPageOffset(vertical: true) : 0;
                    element.DomProperties["_scrollLeft"] = targetLeft;
                    element.DomProperties["_scrollTop"] = targetTop;
                    NotifyVisualViewportScrollIfNeeded(queuedPreviousVisualPageLeft, queuedPreviousVisualPageTop, trackVisualViewport);
                    _smoothScrollTokens.Remove(element);
                }
            });
            return;
        }

        element.DomProperties["_scrollLeft"] = targetLeft;
        element.DomProperties["_scrollTop"] = targetTop;
        NotifyVisualViewportScrollIfNeeded(previousVisualPageLeft, previousVisualPageTop, trackVisualViewport);
    }

    private void QueueFrameAction(Action callback)
    {
        _frameActions[++_frameActionIdCounter] = callback;
    }

    private void CancelSmoothScroll(DomElement element)
    {
        _smoothScrollTokens.Remove(element);
    }

    private string ResolveScrollBehavior(DomElement element, string? requestedBehavior)
    {
        var normalizedRequested = NormalizeScrollBehavior(requestedBehavior);
        if (normalizedRequested == "instant" || normalizedRequested == "smooth")
            return normalizedRequested;

        var props = GetComputedProps(element);
        return NormalizeScrollBehavior(props.GetValueOrDefault("scroll-behavior")) == "smooth"
            ? "smooth"
            : "instant";
    }

    private static string NormalizeScrollBehavior(string? behavior)
    {
        if (string.IsNullOrWhiteSpace(behavior))
            return "auto";

        var normalized = behavior.Trim().ToLowerInvariant();
        return normalized is "instant" or "smooth" ? normalized : "auto";
    }

    private bool HasActiveVisualViewport() => GetVisualViewportScale() > 1.0001;

    private double GetVisualViewportScale() => _visualViewportScale > 1 ? _visualViewportScale : 1;

    private double GetVisualViewportWidth() => _viewportWidth / GetVisualViewportScale();

    private double GetVisualViewportHeight() => _viewportHeight / GetVisualViewportScale();

    private double GetVisualViewportPageOffset(bool vertical)
    {
        var layoutOffset = GetElementScrollOffset(DocumentElement, vertical);
        return layoutOffset + GetVisualViewportExtraOffset(vertical);
    }

    private void SetVisualViewportScale(double scale)
    {
        _visualViewportScale = double.IsFinite(scale) && scale > 1 ? scale : 1;
        ClampVisualViewportOffsets();
    }

    private void ScrollFixedElementIntoVisualViewport(
        DomElement element,
        DomElement scrollContainer,
        string? block,
        string? inline)
    {
        var targetTop = ResolveScrollIntoViewOffset(
            element,
            scrollContainer,
            vertical: true,
            alignment: block,
            viewportSizeOverride: GetVisualViewportHeight(),
            currentScrollOverride: GetVisualViewportPageOffset(vertical: true),
            offsetOverride: GetElementScrollOffset(scrollContainer, vertical: true) +
                ComputeOffsetWithinAncestor(element, scrollContainer, vertical: true));
        var targetLeft = ResolveScrollIntoViewOffset(
            element,
            scrollContainer,
            vertical: false,
            alignment: inline,
            viewportSizeOverride: GetVisualViewportWidth(),
            currentScrollOverride: GetVisualViewportPageOffset(vertical: false),
            offsetOverride: GetElementScrollOffset(scrollContainer, vertical: false) +
                ComputeOffsetWithinAncestor(element, scrollContainer, vertical: false));
        SetVisualViewportPageOffsets(left: targetLeft, top: targetTop);
    }

    private void SetVisualViewportPageOffsets(double? left = null, double? top = null)
    {
        var oldPageLeft = GetVisualViewportPageOffset(vertical: false);
        var oldPageTop = GetVisualViewportPageOffset(vertical: true);
        var layoutLeft = GetElementScrollOffset(DocumentElement, vertical: false);
        var layoutTop = GetElementScrollOffset(DocumentElement, vertical: true);

        if (left.HasValue)
        {
            _visualViewportPageLeftOffset = Math.Clamp(
                left.Value - layoutLeft,
                0,
                GetVisualViewportMaxExtraOffset(vertical: false));
        }

        if (top.HasValue)
        {
            _visualViewportPageTopOffset = Math.Clamp(
                top.Value - layoutTop,
                0,
                GetVisualViewportMaxExtraOffset(vertical: true));
        }

        if (!AreClose(oldPageLeft, GetVisualViewportPageOffset(vertical: false)) ||
            !AreClose(oldPageTop, GetVisualViewportPageOffset(vertical: true)))
        {
            DispatchVisualViewportScrollEvent();
        }
    }

    private void ClampVisualViewportOffsets()
    {
        _visualViewportPageLeftOffset = Math.Clamp(_visualViewportPageLeftOffset, 0, GetVisualViewportMaxExtraOffset(vertical: false));
        _visualViewportPageTopOffset = Math.Clamp(_visualViewportPageTopOffset, 0, GetVisualViewportMaxExtraOffset(vertical: true));
    }

    private double GetVisualViewportExtraOffset(bool vertical) =>
        vertical ? _visualViewportPageTopOffset : _visualViewportPageLeftOffset;

    private double GetVisualViewportMaxExtraOffset(bool vertical)
    {
        if (!HasActiveVisualViewport())
            return 0;

        var layoutSize = vertical ? _viewportHeight : _viewportWidth;
        var visualSize = vertical ? GetVisualViewportHeight() : GetVisualViewportWidth();
        return Math.Max(0, layoutSize - visualSize);
    }

    private void DispatchVisualViewportScrollEvent()
    {
        if (_visualViewportJSObject == null || _visualViewportScrollListeners.Count == 0)
            return;

        var evt = new JSObject();
        evt.FastAddValue((KeyString)"type", new JSString("scroll"), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"target", _visualViewportJSObject, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"currentTarget", _visualViewportJSObject, JSPropertyAttributes.EnumerableConfigurableValue);

        foreach (var listener in _visualViewportScrollListeners.ToList())
        {
            try
            {
                listener.InvokeFunction(new Arguments(listener, evt));
            }
            catch (Exception ex)
            {
                RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.visualViewport", $"Visual viewport listener error: {ex.Message}", ex);
            }
        }
    }

    private static bool AreClose(double left, double right) => Math.Abs(left - right) < 0.0001;

    private void NotifyVisualViewportScrollIfNeeded(double previousPageLeft, double previousPageTop, bool trackVisualViewport)
    {
        if (!trackVisualViewport)
            return;

        if (!AreClose(previousPageLeft, GetVisualViewportPageOffset(vertical: false)) ||
            !AreClose(previousPageTop, GetVisualViewportPageOffset(vertical: true)))
        {
            DispatchVisualViewportScrollEvent();
        }
    }

    private bool CanProgrammaticallyScroll(DomElement element, bool vertical)
    {
        if (IsDocumentElement(element) ||
            IsViewportBodyElement(element, GetOwningDocumentElement(element)))
        {
            return CanProgrammaticallyScrollRoot(element, vertical);
        }

        if (IsSelectListBox(element))
            return CanProgrammaticallyScrollSelectListBox(element, vertical);

        var props = GetComputedProps(element);
        var axisValue = GetOverflowAxisValue(props, vertical);

        return EnablesScrollingBox(axisValue);
    }

    private bool CanProgrammaticallyScrollRoot(DomElement rootElement, bool vertical)
    {
        var documentElement = GetOwningDocumentElement(rootElement);
        var htmlOverflow = GetOverflowAxisValue(GetComputedProps(documentElement), vertical);
        var body = FindBodyElement(documentElement);
        var bodyOverflow = body != null ? GetOverflowAxisValue(GetComputedProps(body), vertical) : null;

        if (DisablesRootScrolling(htmlOverflow) || DisablesRootScrolling(bodyOverflow))
            return false;

        return true;
    }

    private static string? GetOverflowAxisValue(Dictionary<string, string> props, bool vertical)
    {
        var axisValue = props.GetValueOrDefault(vertical ? "overflow-y" : "overflow-x");
        if (string.IsNullOrWhiteSpace(axisValue))
            axisValue = props.GetValueOrDefault("overflow");
        return axisValue;
    }

    private static bool DisablesRootScrolling(string? overflowValue)
    {
        if (string.IsNullOrWhiteSpace(overflowValue))
            return false;

        var normalized = overflowValue.Trim().ToLowerInvariant();
        return normalized.Contains("hidden") || normalized.Contains("clip");
    }

    private static bool EnablesScrollingBox(string? overflowValue)
    {
        if (string.IsNullOrWhiteSpace(overflowValue))
            return false;

        var value = overflowValue.Trim().ToLowerInvariant();
        return value.Contains("hidden") || value.Contains("scroll") || value.Contains("auto") || value.Contains("clip");
    }

    private (double MinLeft, double MaxLeft, double MinTop, double MaxTop) GetScrollBounds(DomElement element)
    {
        var isRoot = IsViewportElementForMetrics(element);
        var maxLeft = Math.Max(0, GetScrollWidthForDomElement(element, isRoot) - GetClientWidthForDomElement(element, isRoot));
        var maxTop = Math.Max(0, GetScrollHeightForDomElement(element, isRoot) - GetClientHeightForDomElement(element, isRoot));

        var props = GetComputedProps(element);
        var writingMode = props.GetValueOrDefault("writing-mode")?.Trim().ToLowerInvariant();
        var direction = props.GetValueOrDefault("direction");
        var isRtl = string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase);
        var isVertical = IsVerticalWritingMode(writingMode);
        var usesNegativeLeft = (isVertical && writingMode?.EndsWith("-rl", StringComparison.Ordinal) == true)
            || (string.Equals(writingMode, "horizontal-tb", StringComparison.OrdinalIgnoreCase) && isRtl);
        var usesNegativeTop = isVertical && isRtl;

        var minLeft = usesNegativeLeft ? -maxLeft : 0;
        var boundedMaxLeft = usesNegativeLeft ? 0 : maxLeft;
        var minTop = usesNegativeTop ? -maxTop : 0;
        var boundedMaxTop = usesNegativeTop ? 0 : maxTop;
        return (minLeft, boundedMaxLeft, minTop, boundedMaxTop);
    }

    private bool CanProgrammaticallyScrollSelectListBox(DomElement element, bool vertical)
    {
        var props = GetComputedProps(element);
        bool verticalWritingMode = IsVerticalWritingMode(props.GetValueOrDefault("writing-mode"));
        bool blockAxisIsVertical = !verticalWritingMode;
        if (vertical != blockAxisIsVertical)
            return false;

        double clientExtent = vertical ? GetClientHeightForDomElement(element, isRoot: false) : GetClientWidthForDomElement(element, isRoot: false);
        double scrollExtent = vertical ? GetScrollHeightForDomElement(element, isRoot: false) : GetScrollWidthForDomElement(element, isRoot: false);
        return scrollExtent > clientExtent + 0.5;
    }

    private bool TryGetSelectListBoxScrollExtent(DomElement element, bool verticalAxis, out double extent)
    {
        if (!IsSelectListBox(element))
        {
            extent = 0;
            return false;
        }

        var props = GetComputedProps(element);
        bool verticalWritingMode = IsVerticalWritingMode(props.GetValueOrDefault("writing-mode"));
        int optionCount = Math.Max(1, CountSelectOptions(element));
        double rowExtent = Math.Max(16, ResolveLineHeightForElement(element));
        double clientInlineExtent = verticalWritingMode
            ? GetClientHeightForDomElement(element, isRoot: false)
            : GetClientWidthForDomElement(element, isRoot: false);
        double clientBlockExtent = verticalWritingMode
            ? GetClientWidthForDomElement(element, isRoot: false)
            : GetClientHeightForDomElement(element, isRoot: false);
        double totalBlockExtent = Math.Max(clientBlockExtent, optionCount * rowExtent);

        extent = verticalAxis
            ? (verticalWritingMode ? clientInlineExtent : totalBlockExtent)
            : (verticalWritingMode ? totalBlockExtent : clientInlineExtent);
        return true;
    }

    private static int CountSelectOptions(DomElement element)
    {
        int count = 0;
        foreach (var child in element.Children.Where(c => !c.IsTextNode))
        {
            if (string.Equals(child.TagName, "option", StringComparison.OrdinalIgnoreCase))
            {
                count++;
                continue;
            }

            count += CountSelectOptions(child);
        }

        return count;
    }

    private static IEnumerable<DomElement> EnumerateRenderedDescendants(DomElement element)
    {
        foreach (var child in element.Children)
        {
            if (child.IsTextNode)
                continue;

            yield return child;

            foreach (var descendant in EnumerateRenderedDescendants(child))
                yield return descendant;
        }
    }

    private DomElement? FindScrollContainer(DomElement element)
    {
        var documentElement = GetOwningDocumentElement(element);
        for (var current = element.Parent; current != null; current = current.Parent)
        {
            if (ReferenceEquals(current, documentElement))
                return documentElement;

            if (IsViewportBodyElement(current, documentElement))
                continue;

            var props = GetComputedProps(current);
            if (HasOverflowClipping(props))
                return current;
        }

        return documentElement;
    }

    private bool HasFixedPositionAncestorBefore(DomElement element, DomElement ancestor)
    {
        for (var current = element.Parent; current != null && !ReferenceEquals(current, ancestor); current = current.Parent)
        {
            var props = GetComputedProps(current);
            if (string.Equals(props.GetValueOrDefault("position"), "fixed", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsDocumentElement(DomElement element) =>
        string.Equals(element.TagName, "html", StringComparison.OrdinalIgnoreCase);

    private bool IsViewportElementForMetrics(DomElement element)
    {
        var documentElement = GetOwningDocumentElement(element);
        return IsDocumentElement(element) || IsViewportBodyElement(element, documentElement);
    }

    private static bool IsViewportBodyElement(DomElement element, DomElement documentElement) =>
        string.Equals(element.TagName, "body", StringComparison.OrdinalIgnoreCase) &&
        ReferenceEquals(element.Parent, documentElement);

    private DomElement GetOwningDocumentElement(DomElement element)
    {
        for (var current = element; current != null; current = current.Parent!)
        {
            if (string.Equals(current.TagName, "html", StringComparison.OrdinalIgnoreCase))
                return current;
        }

        return DocumentElement;
    }

    private bool HasFixedPositionInDocument(DomElement element, DomElement documentElement)
    {
        if (IsFixedPositionElement(element))
            return true;

        return HasFixedPositionAncestorBefore(element, documentElement);
    }

    private bool IsFixedPositionElement(DomElement element)
    {
        var props = GetComputedProps(element);
        return string.Equals(props.GetValueOrDefault("position"), "fixed", StringComparison.OrdinalIgnoreCase);
    }

    private static DomElement? GetOuterFrameElement(DomElement documentElement)
    {
        var docRoot = documentElement.Parent;
        return docRoot != null &&
               string.Equals(docRoot.TagName, "#subdoc-root", StringComparison.OrdinalIgnoreCase)
            ? docRoot.Parent
            : null;
    }

    private DomElement? GetOuterScrollContinuationElement(DomElement scrollContainer)
    {
        if (IsDocumentElement(scrollContainer))
            return GetOuterFrameElement(scrollContainer);

        return scrollContainer;
    }

    private double ComputeOffsetWithinAncestor(DomElement element, DomElement ancestor, bool vertical)
    {
        double offset = 0;
        var current = element;

        while (current.Parent != null && !ReferenceEquals(current.Parent, ancestor))
        {
            offset += ComputeOffsetWithinParent(current, vertical);
            offset -= GetElementScrollOffset(current.Parent, vertical);
            current = current.Parent;
        }

        if (current.Parent != null && ReferenceEquals(current.Parent, ancestor))
            offset += ComputeOffsetWithinParent(current, vertical);

        return offset;
    }

    private double ComputeOffsetWithinParent(DomElement element, bool vertical)
    {
        if (element.Parent == null)
            return 0;

        var parentProps = GetComputedProps(element.Parent);
        var elementProps = GetComputedProps(element);
        var position = elementProps.GetValueOrDefault("position");
        double offset = ParseCssLengthToPixelsWithViewport(
            parentProps.GetValueOrDefault(vertical ? "padding-top" : "padding-left"), element.Parent);
        offset += ParseCssLengthToPixelsWithViewport(
            elementProps.GetValueOrDefault(vertical ? "margin-top" : "margin-left"), element);

        if (!string.Equals(position, "absolute", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var sibling in element.Parent.Children)
            {
                if (ReferenceEquals(sibling, element))
                    break;
                if (sibling.IsTextNode)
                    continue;

                var siblingProps = GetComputedProps(sibling);
                var siblingPosition = siblingProps.GetValueOrDefault("position");
                if (string.Equals(siblingPosition, "absolute", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(siblingPosition, "fixed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                offset += ParseCssLengthToPixelsWithViewport(
                    siblingProps.GetValueOrDefault(vertical ? "margin-top" : "margin-left"), sibling);
                offset += ParseCssLengthToPixelsWithViewport(
                    siblingProps.GetValueOrDefault(vertical ? "height" : "width"), sibling);
                offset += ParseCssLengthToPixelsWithViewport(
                    siblingProps.GetValueOrDefault(vertical ? "margin-bottom" : "margin-right"), sibling);
            }
        }

        if (string.Equals(position, "absolute", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(position, "relative", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(position, "fixed", StringComparison.OrdinalIgnoreCase))
        {
            offset += ResolvePositionedInset(element, vertical);
        }

        return offset;
    }

    private double ResolveScrollIntoViewOffset(
        DomElement element,
        DomElement scrollContainer,
        bool vertical,
        string? alignment,
        double? viewportSizeOverride = null,
        double? currentScrollOverride = null,
        double? offsetOverride = null)
    {
        var normalizedAlignment = NormalizeScrollIntoViewAlignment(alignment, "start");
        var offset = offsetOverride ?? ComputeOffsetWithinAncestor(element, scrollContainer, vertical);
        var targetSize = vertical ? GetBorderBoxHeight(GetComputedProps(element), element) : GetBorderBoxWidth(GetComputedProps(element), element);
        var (marginStart, marginStartOwner) = ResolveScrollIntoViewInset(element, vertical ? "scroll-margin-top" : "scroll-margin-left");
        var (marginEnd, marginEndOwner) = ResolveScrollIntoViewInset(element, vertical ? "scroll-margin-bottom" : "scroll-margin-right");
        var (paddingStart, _) = ResolveScrollIntoViewInset(scrollContainer, vertical ? "scroll-padding-top" : "scroll-padding-left");
        var (paddingEnd, _) = ResolveScrollIntoViewInset(scrollContainer, vertical ? "scroll-padding-bottom" : "scroll-padding-right");
        marginStart = ConvertInsetToScrollContainerCoordinates(marginStart, marginStartOwner, scrollContainer);
        marginEnd = ConvertInsetToScrollContainerCoordinates(marginEnd, marginEndOwner, scrollContainer);
        var viewportSize = viewportSizeOverride ?? (vertical
            ? GetClientHeightForDomElement(scrollContainer, IsDocumentElement(scrollContainer))
            : GetClientWidthForDomElement(scrollContainer, IsDocumentElement(scrollContainer)));
        var currentScroll = currentScrollOverride ?? GetElementScrollOffset(scrollContainer, vertical);

        var startTarget = offset - marginStart - paddingStart;
        var endTarget = offset + targetSize + marginEnd + paddingEnd - viewportSize;
        if (normalizedAlignment == "start")
            return startTarget;
        if (normalizedAlignment == "end")
            return endTarget;

        var alignmentViewportSize = Math.Max(0, viewportSize - paddingStart - paddingEnd);
        if (normalizedAlignment == "center")
        {
            var targetCenter = offset + ((targetSize + marginEnd - marginStart) / 2.0);
            return targetCenter - paddingStart - (alignmentViewportSize / 2.0);
        }

        var visibleStart = currentScroll + paddingStart;
        var visibleEnd = currentScroll + viewportSize - paddingEnd;
        var targetStart = offset - marginStart;
        var targetEnd = offset + targetSize + marginEnd;

        if (targetStart >= visibleStart && targetEnd <= visibleEnd)
            return currentScroll;

        if (targetSize + marginStart + marginEnd > alignmentViewportSize)
            return Math.Abs(startTarget - currentScroll) <= Math.Abs(endTarget - currentScroll) ? startTarget : endTarget;

        if (targetStart < visibleStart)
            return startTarget;
        if (targetEnd > visibleEnd)
            return endTarget;

        return currentScroll;
    }

    private (double Value, DomElement Owner) ResolveScrollIntoViewInset(DomElement element, string propertyName)
    {
        var props = GetComputedProps(element);
        var value = props.GetValueOrDefault(propertyName);
        if (string.Equals(value, "inherit", StringComparison.OrdinalIgnoreCase) && element.Parent != null)
            return ResolveScrollIntoViewInset(element.Parent, propertyName);

        return (ParseCssLengthToPixelsWithViewport(value, element), element);
    }

    private double ConvertInsetToScrollContainerCoordinates(double inset, DomElement insetOwner, DomElement scrollContainer)
    {
        if (!double.IsFinite(inset) || AreClose(inset, 0))
            return 0;

        var ownerZoom = GetUsedZoomForElement(insetOwner);
        var containerZoom = GetUsedZoomForElement(scrollContainer);
        if (!double.IsFinite(ownerZoom) || ownerZoom <= 0 ||
            !double.IsFinite(containerZoom) || containerZoom <= 0)
        {
            return inset;
        }

        return inset * (ownerZoom / containerZoom);
    }

    private double ResolvePositionedInset(DomElement element, bool vertical)
    {
        if (element.Parent == null)
            return 0;

        var props = GetComputedProps(element);
        var primaryProperty = vertical ? "top" : "left";
        var secondaryProperty = vertical ? "bottom" : "right";
        var value = props.GetValueOrDefault(primaryProperty);
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
        {
            var fallback = props.GetValueOrDefault(secondaryProperty);
            if (string.IsNullOrWhiteSpace(fallback) ||
                string.Equals(fallback, "auto", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var reference = ResolveContainingBlockReferenceLength(element, vertical);
            var borderBoxSize = vertical
                ? GetBorderBoxHeight(props, element)
                : GetBorderBoxWidth(props, element);
            var fallbackPixels = ParseCssLengthToPixelsWithViewport(fallback, element, percentageBasis: reference);
            return Math.Max(0, reference - borderBoxSize - fallbackPixels);
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.EndsWith("%"))
        {
            if (!double.TryParse(normalized[..^1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var percent))
            {
                return 0;
            }

            var parentProps = GetComputedProps(element.Parent);
            var reference = ParseCssLengthToPixelsWithViewport(parentProps.GetValueOrDefault(vertical ? "height" : "width"), element.Parent);
            return reference <= 0 ? 0 : reference * (percent / 100.0);
        }

        return ParseCssLengthToPixelsWithViewport(value, element);
    }

    private double ParseCssLengthToPixelsWithViewport(
        string? value,
        DomElement? referenceElement = null,
        bool forLineHeight = false,
        double? percentageBasis = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        return TryEvaluateCssLengthWithViewport(value, referenceElement, forLineHeight, percentageBasis, out var px)
            ? px
            : 0;
    }

    private bool TryEvaluateCssLengthWithViewport(
        string value,
        DomElement? referenceElement,
        bool forLineHeight,
        double? percentageBasis,
        out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        while (normalized.Length >= 2 &&
               normalized[0] == '(' &&
               normalized[^1] == ')' &&
               HasBalancedParens(normalized[1..^1]))
        {
            normalized = normalized[1..^1].Trim();
        }

        if (TryEvaluateMathLengthFunction(normalized, referenceElement, forLineHeight, percentageBasis, out result))
            return true;

        var additiveOperatorIndex = FindTopLevelAdditiveOperator(normalized);
        if (additiveOperatorIndex > 0)
        {
            if (!TryEvaluateCssLengthWithViewport(
                    normalized[..additiveOperatorIndex],
                    referenceElement,
                    forLineHeight,
                    percentageBasis,
                    out var left) ||
                !TryEvaluateCssLengthWithViewport(
                    normalized[(additiveOperatorIndex + 1)..],
                    referenceElement,
                    forLineHeight,
                    percentageBasis,
                    out var right))
            {
                return false;
            }

            result = normalized[additiveOperatorIndex] == '+'
                ? left + right
                : left - right;
            return true;
        }

        var lower = normalized.ToLowerInvariant();
        if (percentageBasis.HasValue &&
            lower.EndsWith("%", StringComparison.Ordinal) &&
            double.TryParse(lower[..^1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var percent))
        {
            result = percentageBasis.Value * (percent / 100.0);
            return true;
        }

        if (referenceElement != null &&
            lower.EndsWith("rem") &&
            double.TryParse(lower[..^3], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var rem))
        {
            result = rem * ResolveFontSizeForLength(referenceElement, rootRelative: true);
            return true;
        }

        if (referenceElement != null &&
            lower.EndsWith("em") &&
            double.TryParse(lower[..^2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var em))
        {
            result = em * ResolveFontSizeForLength(referenceElement, rootRelative: false);
            return true;
        }

        if (referenceElement != null &&
            lower.EndsWith("rlh") &&
            double.TryParse(lower[..^3], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var rlh))
        {
            result = rlh * ResolveLineHeightForLength(referenceElement, rootRelative: true);
            return true;
        }

        if (referenceElement != null &&
            lower.EndsWith("lh") &&
            double.TryParse(lower[..^2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lh))
        {
            result = lh * ResolveLineHeightForLength(referenceElement, rootRelative: false, forLineHeight);
            return true;
        }

        var px = ParseCssLengthToPixels(normalized, _viewportWidth, _viewportHeight);
        if (double.IsNaN(px))
            return false;

        result = px;
        return true;
    }

    private bool TryEvaluateMathLengthFunction(
        string value,
        DomElement? referenceElement,
        bool forLineHeight,
        double? percentageBasis,
        out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value) || value[^1] != ')')
            return false;

        static bool StartsWithFunction(string candidate, string functionName)
            => candidate.StartsWith(functionName + "(", StringComparison.OrdinalIgnoreCase);

        if (StartsWithFunction(value, "calc"))
        {
            var content = value[5..^1];
            return HasBalancedParens(content) &&
                   TryEvaluateCssLengthWithViewport(content, referenceElement, forLineHeight, percentageBasis, out result);
        }

        if (!StartsWithFunction(value, "min") && !StartsWithFunction(value, "max"))
            return false;

        var isMax = StartsWithFunction(value, "max");
        var contentValue = value[4..^1];
        if (!HasBalancedParens(contentValue))
            return false;

        var parts = SplitTopLevelArguments(contentValue);
        if (parts.Count == 0)
            return false;

        double? candidate = null;
        foreach (var part in parts)
        {
            if (!TryEvaluateCssLengthWithViewport(part, referenceElement, forLineHeight, percentageBasis, out var parsed))
                return false;

            candidate = candidate.HasValue
                ? (isMax ? Math.Max(candidate.Value, parsed) : Math.Min(candidate.Value, parsed))
                : parsed;
        }

        if (!candidate.HasValue)
            return false;

        result = candidate.Value;
        return true;
    }

    private static int FindTopLevelAdditiveOperator(string expression)
    {
        var depth = 0;
        for (int i = expression.Length - 1; i >= 1; i--)
        {
            switch (expression[i])
            {
                case ')':
                    depth++;
                    break;
                case '(':
                    depth--;
                    break;
                case '+':
                case '-':
                    if (depth != 0)
                        break;

                    var leftIndex = i - 1;
                    while (leftIndex >= 0 && char.IsWhiteSpace(expression[leftIndex]))
                        leftIndex--;

                    var rightIndex = i + 1;
                    while (rightIndex < expression.Length && char.IsWhiteSpace(expression[rightIndex]))
                        rightIndex++;

                    if (leftIndex >= 0 &&
                        rightIndex < expression.Length &&
                        expression[leftIndex] != '(' &&
                        expression[leftIndex] != ',' &&
                        expression[leftIndex] != '+' &&
                        expression[leftIndex] != '-')
                    {
                        return i;
                    }
                    break;
            }
        }

        return -1;
    }

    private static List<string> SplitTopLevelArguments(string value)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (int i = 0; i < value.Length; i++)
        {
            switch (value[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth < 0)
                        return [];
                    break;
                case ',' when depth == 0:
                    parts.Add(value[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }

        if (depth != 0)
            return [];

        parts.Add(value[start..].Trim());
        return parts;
    }

    private double ResolveContainingBlockReferenceLength(DomElement element, bool vertical)
    {
        if (element.Parent == null ||
            string.Equals(element.TagName, "html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(element.TagName, "body", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(element.Parent.TagName, "html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(element.Parent.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            return GetViewportReferenceLength(element, vertical);
        }

        var parentRect = ComputeUnzoomedLayoutRect(element.Parent);
        var reference = vertical ? parentRect.Height : parentRect.Width;
        return reference > 0 ? reference : GetViewportReferenceLength(element, vertical);
    }

    private double GetViewportReferenceLength(DomElement? element, bool vertical)
    {
        if (element != null)
        {
            var documentElement = GetOwningDocumentElement(element);
            var frameElement = GetOuterFrameElement(documentElement);
            if (frameElement != null)
            {
                var frameProps = GetComputedProps(frameElement);
                var frameLength = ParseCssLengthToPixelsWithViewport(
                    frameProps.GetValueOrDefault(vertical ? "height" : "width"),
                    frameElement);
                if (frameLength > 0)
                    return frameLength;

                if (frameElement.Attributes.TryGetValue(vertical ? "height" : "width", out var frameAttribute) &&
                    double.TryParse(frameAttribute, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out frameLength) &&
                    frameLength > 0)
                {
                    return frameLength;
                }
            }
        }

        return vertical ? _viewportHeight : _viewportWidth;
    }

    private static bool HasExplicitBodyMargin(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase);
    }

    private double ResolveLineHeightForLength(DomElement element, bool rootRelative, bool forLineHeight = false)
    {
        var target = rootRelative ? GetRootElement(element) : (forLineHeight ? element.Parent ?? element : element);
        return ResolveLineHeightForElement(target);
    }

    private double ResolveFontSizeForLength(DomElement element, bool rootRelative)
    {
        var target = rootRelative ? GetRootElement(element) : element;
        return ResolveFontSizeForElement(target);
    }

    private DomElement GetRootElement(DomElement element)
    {
        DomElement? htmlElement = null;
        var current = element;
        while (current.Parent != null)
        {
            current = current.Parent;
            if (string.Equals(current.TagName, "html", StringComparison.OrdinalIgnoreCase))
                htmlElement = current;
        }

        return htmlElement ?? current;
    }

    private double ResolveLineHeightForElement(DomElement element)
    {
        var props = GetComputedProps(element);
        var fontSize = ResolveFontSizeForElement(element);
        var lineHeight = props.GetValueOrDefault("line-height");
        if (string.IsNullOrWhiteSpace(lineHeight) ||
            string.Equals(lineHeight, "normal", StringComparison.OrdinalIgnoreCase))
        {
            return fontSize * 1.2;
        }

        var normalized = lineHeight.Trim().ToLowerInvariant();
        if (double.TryParse(normalized, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var multiplier))
        {
            return fontSize * multiplier;
        }

        return ParseCssLengthToPixelsWithViewport(lineHeight, element, forLineHeight: true);
    }

    private double ResolveFontSizeForElement(DomElement element)
    {
        var props = GetComputedProps(element);
        var fontSize = ParseCssLengthToPixelsWithViewport(props.GetValueOrDefault("font-size"), element);
        return fontSize > 0 ? fontSize : 16;
    }

    // ── Phase 6: Sub-document cache ──────────────────────────────────────────
    private readonly Dictionary<DomElement, JSObject> _subDocumentCache = [];
    private readonly Dictionary<DomElement, JSObject> _subWindowCache = [];
    private readonly Dictionary<DomElement, string> _subDocumentLocationCache = [];
    private readonly Dictionary<DomElement, string> _subDocumentBaseUrlCache = [];
    private readonly HashSet<DomElement> _objectLoadFailures = [];
    private readonly HashSet<DomElement> _onloadFired = [];

    private void InvalidateCachedSubDocument(DomElement containerElement)
    {
        _subDocumentCache.Remove(containerElement);
        _subWindowCache.Remove(containerElement);
        _subDocumentLocationCache.Remove(containerElement);
        _subDocumentBaseUrlCache.Remove(containerElement);
    }

    /// <summary>
    /// Fires the onload event handler on an iframe or object element after its
    /// sub-document has been loaded. The handler is only fired once per element.
    /// Handles both property-based handlers (element.onload = function) and
    /// attribute-based handlers (setAttribute("onload", code)).
    /// </summary>
    private void FireSubDocumentOnload(DomElement element)
    {
        if (_jsContext == null) return;
        if (_onloadFired.Contains(element)) return;

        var tag = element.TagName?.ToLowerInvariant();
        if (tag != "iframe" && tag != "object") return;

        var hasSrcDoc = tag == "iframe" && element.Attributes.ContainsKey("srcdoc");
        var resourceUrl = hasSrcDoc ? "about:srcdoc" : GetSubResourceUrl(element);
        if (string.IsNullOrWhiteSpace(resourceUrl) && !hasSrcDoc) return;

        // Ensure the sub-document is loaded (this triggers the fetch if needed)
        GetOrCreateSubDocument(element);

        _onloadFired.Add(element);

        // Fire the onload handler
        try
        {
            var evt = new JSObject();
            evt.FastAddValue((KeyString)"type", new JSString("load"), JSPropertyAttributes.EnumerableConfigurableValue);
            evt.FastAddValue((KeyString)"bubbles", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
            DispatchEventOnElement(element, evt);
        }
        catch (Exception ex)
        {
            RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.FireSubDocumentOnload",
                $"onload handler error for <{tag}>: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Recursively fires onload for all iframe/object descendants of an element.
    /// Called when a subtree containing iframes/objects is added to the document.
    /// </summary>
    private void FireDescendantOnloads(DomElement element)
    {
        foreach (var child in element.Children)
        {
            var childTag = child.TagName?.ToLowerInvariant();
            if (childTag == "iframe" || childTag == "object")
            {
                FireSubDocumentOnload(child);
            }
            FireDescendantOnloads(child);
        }
    }

    /// <summary>
    /// Returns <c>true</c> if the resource for the given <c>&lt;object&gt;</c> element
    /// failed to load (HTTP 404, file not found, etc.), meaning fallback content
    /// should be visible and contentDocument should return null.
    /// </summary>
    private bool IsObjectLoadFailed(DomElement objectElement)
    {
        if (_objectLoadFailures.Contains(objectElement))
            return true;

        // Check if this is the first access — probe the resource
        var resourceUrl = GetSubResourceUrl(objectElement);
        if (string.IsNullOrWhiteSpace(resourceUrl))
            return false; // No data attribute → empty sub-document, not a failure

        var (_, contentType) = TryFetchSubResource(resourceUrl, GetInheritedSubDocumentBaseUrl(objectElement));
        if (string.Equals(contentType, FetchFailedContentType, StringComparison.Ordinal))
        {
            _objectLoadFailures.Add(objectElement);
            return true;
        }

        return false;
    }

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

        var executeHtmlScripts = false;
        string? htmlToExecute = null;
        var docRoot = containerElement.Children.FirstOrDefault(c =>
            string.Equals(c.TagName, "#subdoc-root", StringComparison.OrdinalIgnoreCase));
        if (docRoot == null)
        {
            if (string.Equals(containerElement.TagName, "iframe", StringComparison.OrdinalIgnoreCase) &&
                containerElement.Attributes.TryGetValue("srcdoc", out var srcDoc))
            {
                _subDocumentLocationCache[containerElement] = "about:srcdoc";
                _subDocumentBaseUrlCache[containerElement] = GetInheritedSubDocumentBaseUrl(containerElement);
                docRoot = BuildSubDocumentFromHtml(srcDoc, containerElement);
                htmlToExecute = srcDoc;
                executeHtmlScripts = true;
            }
            else
            {
                // Determine the resource URL for this container
                var resourceUrl = GetSubResourceUrl(containerElement);
                var resolvedUrl = ResolveSubResourceUrl(resourceUrl, GetInheritedSubDocumentBaseUrl(containerElement));
                if (!string.IsNullOrWhiteSpace(resolvedUrl))
                {
                    _subDocumentLocationCache[containerElement] = resolvedUrl;
                    _subDocumentBaseUrlCache[containerElement] = resolvedUrl;
                }

                var (fetchedContent, contentType) = TryFetchSubResource(resourceUrl, GetInheritedSubDocumentBaseUrl(containerElement));

                if (!string.IsNullOrEmpty(fetchedContent) &&
                    IsXmlContentType(contentType))
                {
                    // XML/SVG/XHTML content → parse with XML parser
                    docRoot = BuildSubDocumentFromXml(fetchedContent, contentType, containerElement);
                }
                else if (!string.IsNullOrEmpty(fetchedContent) &&
                    (contentType.Contains("html", StringComparison.OrdinalIgnoreCase) ||
                     string.IsNullOrEmpty(contentType)))
                {
                    // HTML content → parse with HTML parser
                    docRoot = BuildSubDocumentFromHtml(fetchedContent, containerElement);
                    htmlToExecute = fetchedContent;
                    executeHtmlScripts = true;
                }
                else if (!string.IsNullOrEmpty(fetchedContent) &&
                         contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                {
                    // text/plain (or other text/* types) → document with pre-formatted text
                    docRoot = BuildSubDocumentWithText(fetchedContent, containerElement);
                }
                else
                {
                    // Default: create an empty sub-document structure
                    // (binary resources like image/png, fetch failures, about:blank, etc.)
                    docRoot = BuildEmptySubDocument(containerElement);
                }
            }
        }

        var doc = BuildSubDocument(docRoot);
        _subDocumentCache[containerElement] = doc;
        if (executeHtmlScripts && !string.IsNullOrEmpty(htmlToExecute))
            ExecuteSubDocumentScripts(containerElement, htmlToExecute);
        return doc;
    }

    private JSObject GetOrCreateSubWindow(DomElement containerElement)
    {
        if (_subWindowCache.TryGetValue(containerElement, out var cached))
            return cached;

        var subDocument = GetOrCreateSubDocument(containerElement);
        var subWindow = new JSObject();
        _subWindowCache[containerElement] = subWindow;

        subWindow.FastAddProperty(
            (KeyString)"document",
            new JSFunction((in Arguments _) => GetOrCreateSubDocument(containerElement), "get document"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        var locationHref = GetSubWindowLocationHref(containerElement);
        var iframeLocation = new JSObject();
        iframeLocation.FastAddValue(
            (KeyString)"href",
            new JSString(locationHref),
            JSPropertyAttributes.EnumerableConfigurableValue);
        if (Uri.TryCreate(locationHref, UriKind.Absolute, out var locationUri))
        {
            iframeLocation.FastAddValue((KeyString)"protocol", new JSString(locationUri.Scheme + ":"), JSPropertyAttributes.EnumerableConfigurableValue);
            iframeLocation.FastAddValue((KeyString)"host", new JSString(locationUri.IsDefaultPort ? locationUri.Host : $"{locationUri.Host}:{locationUri.Port}"), JSPropertyAttributes.EnumerableConfigurableValue);
            iframeLocation.FastAddValue((KeyString)"hostname", new JSString(locationUri.Host), JSPropertyAttributes.EnumerableConfigurableValue);
            iframeLocation.FastAddValue((KeyString)"pathname", new JSString(locationUri.AbsolutePath), JSPropertyAttributes.EnumerableConfigurableValue);
            iframeLocation.FastAddValue((KeyString)"search", new JSString(locationUri.Query), JSPropertyAttributes.EnumerableConfigurableValue);
            iframeLocation.FastAddValue((KeyString)"hash", new JSString(locationUri.Fragment), JSPropertyAttributes.EnumerableConfigurableValue);
            iframeLocation.FastAddValue((KeyString)"origin", new JSString($"{locationUri.Scheme}://{(locationUri.IsDefaultPort ? locationUri.Host : $"{locationUri.Host}:{locationUri.Port}")}"), JSPropertyAttributes.EnumerableConfigurableValue);
        }
        else
        {
            iframeLocation.FastAddValue((KeyString)"search", new JSString(string.Empty), JSPropertyAttributes.EnumerableConfigurableValue);
            iframeLocation.FastAddValue((KeyString)"hash", new JSString(string.Empty), JSPropertyAttributes.EnumerableConfigurableValue);
        }
        subWindow.FastAddValue(
            (KeyString)"location",
            iframeLocation,
            JSPropertyAttributes.EnumerableConfigurableValue);

        subWindow.FastAddProperty(
            (KeyString)"scrollX",
            new JSFunction((in Arguments _) => new JSNumber(GetSubWindowScrollOffset(containerElement, vertical: false)), "get scrollX"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        subWindow.FastAddProperty(
            (KeyString)"scrollY",
            new JSFunction((in Arguments _) => new JSNumber(GetSubWindowScrollOffset(containerElement, vertical: true)), "get scrollY"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        subWindow.FastAddProperty(
            (KeyString)"pageXOffset",
            new JSFunction((in Arguments _) => new JSNumber(GetSubWindowScrollOffset(containerElement, vertical: false)), "get pageXOffset"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);
        subWindow.FastAddProperty(
            (KeyString)"pageYOffset",
            new JSFunction((in Arguments _) => new JSNumber(GetSubWindowScrollOffset(containerElement, vertical: true)), "get pageYOffset"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        subWindow.FastAddValue(
            (KeyString)"scroll",
            new JSFunction((in Arguments a) =>
            {
                var (left, top, behavior) = GetScrollArguments(a);
                SetSubWindowScrollOffsets(containerElement, left, top, behavior: behavior);
                return JSUndefined.Value;
            }, "scroll", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        subWindow.FastAddValue(
            (KeyString)"scrollTo",
            new JSFunction((in Arguments a) =>
            {
                var (left, top, behavior) = GetScrollArguments(a);
                SetSubWindowScrollOffsets(containerElement, left, top, behavior: behavior);
                return JSUndefined.Value;
            }, "scrollTo", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        subWindow.FastAddValue(
            (KeyString)"scrollBy",
            new JSFunction((in Arguments a) =>
            {
                var (left, top, behavior) = GetScrollArguments(a);
                SetSubWindowScrollOffsets(containerElement, left, top, relative: true, behavior: behavior);
                return JSUndefined.Value;
            }, "scrollBy", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        subWindow.FastAddValue(
            (KeyString)"self",
            subWindow,
            JSPropertyAttributes.EnumerableConfigurableValue);
        subWindow.FastAddValue(
            (KeyString)"window",
            subWindow,
            JSPropertyAttributes.EnumerableConfigurableValue);
        var parentWindow = GetParentWindowForSubDocument(containerElement);
        if (parentWindow != null)
        {
            subWindow.FastAddValue(
                (KeyString)"parent",
                parentWindow,
                JSPropertyAttributes.EnumerableConfigurableValue);
        }
        subWindow.FastAddValue(
            (KeyString)"top",
            _windowJSObject ?? subWindow,
            JSPropertyAttributes.EnumerableConfigurableValue);

        subDocument.FastAddValue(
            (KeyString)"defaultView",
            subWindow,
            JSPropertyAttributes.EnumerableConfigurableValue);

        return subWindow;
    }

    private string GetSubWindowLocationHref(DomElement containerElement)
    {
        if (_subDocumentLocationCache.TryGetValue(containerElement, out var cachedLocation) &&
            !string.IsNullOrWhiteSpace(cachedLocation))
        {
            return cachedLocation;
        }

        if (string.Equals(containerElement.TagName, "iframe", StringComparison.OrdinalIgnoreCase) &&
            containerElement.Attributes.ContainsKey("srcdoc"))
            return "about:srcdoc";

        var resolvedUrl = ResolveSubResourceUrl(GetSubResourceUrl(containerElement), GetInheritedSubDocumentBaseUrl(containerElement));
        return !string.IsNullOrWhiteSpace(resolvedUrl) ? resolvedUrl : "about:blank";
    }

    private double GetSubWindowScrollOffset(DomElement containerElement, bool vertical)
    {
        var scrollingElement = GetSubDocumentScrollingElement(containerElement);
        return scrollingElement == null ? 0 : GetElementScrollOffset(scrollingElement, vertical);
    }

    private void SetSubWindowScrollOffsets(DomElement containerElement, double? left = null, double? top = null, bool relative = false, string? behavior = null)
    {
        var scrollingElement = GetSubDocumentScrollingElement(containerElement);
        if (scrollingElement == null)
            return;

        SetElementScrollOffsetsWithBehavior(scrollingElement, left, top, relative: relative, clamp: false, behavior: behavior);
    }

    private static DomElement? GetSubDocumentScrollingElement(DomElement containerElement)
    {
        var docRoot = containerElement.Children.FirstOrDefault(c =>
            string.Equals(c.TagName, "#subdoc-root", StringComparison.OrdinalIgnoreCase));
        if (docRoot == null)
            return null;

        return docRoot.Children.FirstOrDefault(c => !c.IsTextNode && !c.TagName.StartsWith("#"));
    }

    private static DomElement? FindBodyElement(DomElement documentElement) =>
        documentElement.Children.FirstOrDefault(c =>
            !c.IsTextNode &&
            string.Equals(c.TagName, "body", StringComparison.OrdinalIgnoreCase));

    private JSObject? GetParentWindowForSubDocument(DomElement containerElement)
    {
        var ownerDocRoot = containerElement.OwnerDocRoot;
        if (ownerDocRoot?.Parent != null && !ownerDocRoot.Parent.TagName.StartsWith("#", StringComparison.Ordinal))
            return GetOrCreateSubWindow(ownerDocRoot.Parent);

        return _windowJSObject;
    }

    private string GetInheritedSubDocumentBaseUrl(DomElement containerElement)
    {
        var ownerDocRoot = containerElement.OwnerDocRoot;
        if (ownerDocRoot?.Parent != null &&
            !ownerDocRoot.Parent.TagName.StartsWith("#", StringComparison.Ordinal) &&
            _subDocumentBaseUrlCache.TryGetValue(ownerDocRoot.Parent, out var parentBaseUrl) &&
            !string.IsNullOrWhiteSpace(parentBaseUrl))
        {
            return parentBaseUrl;
        }

        return _pageUrl;
    }

    private string GetSubDocumentBaseUrl(DomElement containerElement)
    {
        return _subDocumentBaseUrlCache.TryGetValue(containerElement, out var baseUrl) &&
               !string.IsNullOrWhiteSpace(baseUrl)
            ? baseUrl
            : GetInheritedSubDocumentBaseUrl(containerElement);
    }

    private string ResolveSubResourceUrl(string resourceUrl, string? baseUrl = null)
    {
        resourceUrl = NormalizeWptPlaceholderUrl(resourceUrl);
        if (string.IsNullOrWhiteSpace(resourceUrl))
            return string.Empty;

        if (Uri.TryCreate(resourceUrl, UriKind.Absolute, out var absoluteUri))
            return absoluteUri.AbsoluteUri;

        var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? _pageUrl : baseUrl;
        return Uri.TryCreate(effectiveBaseUrl, UriKind.Absolute, out var baseUri) &&
               Uri.TryCreate(baseUri, resourceUrl, out var resolved)
            ? resolved.AbsoluteUri
            : string.Empty;
    }

    private bool TryGetWptRootDirectory(out string wptRoot)
    {
        static string? FindWptRoot(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            DirectoryInfo? current;
            if (File.Exists(path))
                current = new FileInfo(path).Directory;
            else if (Directory.Exists(path))
                current = new DirectoryInfo(path);
            else
                current = new FileInfo(path).Directory;

            while (current != null)
            {
                if (string.Equals(current.Name, "wpt", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(current.Parent?.Name, "tests", StringComparison.OrdinalIgnoreCase))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return null;
        }

        wptRoot = string.Empty;

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(_localBasePath))
            candidates.Add(_localBasePath);

        if (Uri.TryCreate(_pageUrl, UriKind.Absolute, out var pageUri) &&
            string.Equals(pageUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(pageUri.LocalPath);
        }

        foreach (var candidate in candidates)
        {
            var root = FindWptRoot(candidate);
            if (!string.IsNullOrWhiteSpace(root))
            {
                wptRoot = root;
                return true;
            }
        }

        return false;
    }

    private string? TryMapLocalWptHttpResource(string absoluteUrl)
    {
        if (!TryGetWptRootDirectory(out var wptRoot) ||
            !Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var resourceUri) ||
            !(string.Equals(resourceUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
              string.Equals(resourceUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (!string.Equals(resourceUri.Host, "web-platform.test", StringComparison.OrdinalIgnoreCase) &&
            !resourceUri.Host.EndsWith(".web-platform.test", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relativePath = resourceUri.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        var localPath = Path.Combine(wptRoot, relativePath);
        return File.Exists(localPath) ? localPath : null;
    }

    private void ExecuteSubDocumentScripts(DomElement containerElement, string html)
    {
        if (_jsContext == null || string.IsNullOrWhiteSpace(html))
            return;

        var extraction = new ScriptExtractor().ExtractAll(html, GetSubDocumentBaseUrl(containerElement));
        if (extraction.Scripts.Count == 0 && extraction.DeferredScripts.Count == 0)
            return;

        var subDocument = GetOrCreateSubDocument(containerElement);
        var subWindow = GetOrCreateSubWindow(containerElement);
        var location = subWindow[(KeyString)"location"];

        JSValue? previousWindow = null;
        JSValue? previousDocument = null;
        JSValue? previousLocation = null;
        JSValue? previousParent = null;
        JSValue? previousSelf = null;
        JSValue? previousTop = null;

        try
        {
            previousWindow = _jsContext.Eval("typeof window === 'undefined' ? undefined : window");
            previousDocument = _jsContext.Eval("typeof document === 'undefined' ? undefined : document");
            previousLocation = _jsContext.Eval("typeof location === 'undefined' ? undefined : location");
            previousParent = _jsContext.Eval("typeof parent === 'undefined' ? undefined : parent");
            previousSelf = _jsContext.Eval("typeof self === 'undefined' ? undefined : self");
            previousTop = _jsContext.Eval("typeof top === 'undefined' ? undefined : top");

            _jsContext["window"] = subWindow;
            _jsContext["document"] = subDocument;
            _jsContext["location"] = location;
            _jsContext["parent"] = GetParentWindowForSubDocument(containerElement) ?? JSUndefined.Value;
            _jsContext["self"] = subWindow;
            _jsContext["top"] = _windowJSObject ?? subWindow;

            foreach (var script in extraction.Scripts)
            {
                try
                {
                    _jsContext.Eval(script);
                }
                catch (Exception ex)
                {
                    RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.ExecuteSubDocumentScripts",
                        $"Sub-document script error: {ex.Message}", ex);
                }
            }

            foreach (var script in extraction.DeferredScripts)
            {
                try
                {
                    _jsContext.Eval(script);
                }
                catch (Exception ex)
                {
                    RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.ExecuteSubDocumentScripts",
                        $"Sub-document deferred script error: {ex.Message}", ex);
                }
            }
        }
        finally
        {
            _jsContext["window"] = previousWindow ?? JSUndefined.Value;
            _jsContext["document"] = previousDocument ?? JSUndefined.Value;
            _jsContext["location"] = previousLocation ?? JSUndefined.Value;
            _jsContext["parent"] = previousParent ?? JSUndefined.Value;
            _jsContext["self"] = previousSelf ?? JSUndefined.Value;
            _jsContext["top"] = previousTop ?? JSUndefined.Value;
        }
    }

    /// <summary>
    /// Returns true if the content type indicates XML-family content
    /// (application/xml, text/xml, image/svg+xml, application/xhtml+xml).
    /// </summary>
    private static bool IsXmlContentType(string contentType) =>
        string.Equals(contentType, "application/xml", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(contentType, "text/xml", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(contentType, "image/svg+xml", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(contentType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a minimal empty sub-document structure (html > head + body).
    /// </summary>
    private DomElement BuildEmptySubDocument(DomElement containerElement)
    {
        var docRoot = new DomElement("#subdoc-root", null, null, string.Empty);
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

        return docRoot;
    }

    /// <summary>
    /// Creates a sub-document with plain text content wrapped in a <c>&lt;pre&gt;</c> element.
    /// Used for <c>text/plain</c> resources.
    /// </summary>
    private DomElement BuildSubDocumentWithText(string textContent, DomElement containerElement)
    {
        var docRoot = new DomElement("#subdoc-root", null, null, string.Empty);
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

        // Wrap text content in <pre> element
        var preEl = new DomElement("pre", null, null, string.Empty);
        preEl.Parent = bodyEl;
        bodyEl.Children.Add(preEl);

        var textNode = new DomElement("#text", null, null, string.Empty, isTextNode: true);
        textNode.TextContent = textContent;
        textNode.Parent = preEl;
        preEl.Children.Add(textNode);

        containerElement.Children.Insert(0, docRoot);
        _elements.Add(docRoot);
        _elements.Add(htmlEl);
        _elements.Add(headEl);
        _elements.Add(bodyEl);
        _elements.Add(preEl);
        _elements.Add(textNode);

        return docRoot;
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
    /// Attempts to fetch a sub-resource URL and return its content along with the
    /// detected content type. Returns <c>(null, contentType)</c> for non-HTML resources,
    /// about:blank, empty URLs, or when the fetch fails.
    /// Supports <c>data:</c> URIs, <c>file://</c> URLs, and <c>http(s)://</c> URLs.
    /// </summary>
    private (string? content, string contentType) TryFetchSubResource(string resourceUrl, string? baseUrl = null)
    {
        resourceUrl = NormalizeWptPlaceholderUrl(resourceUrl);
        if (string.IsNullOrWhiteSpace(resourceUrl))
            return (null, string.Empty);

        // about:blank gets an empty document (default behavior)
        if (string.Equals(resourceUrl, "about:blank", StringComparison.OrdinalIgnoreCase))
            return (null, "text/html");

        // Handle data: URIs — decode and return content directly
        if (resourceUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var (mimeType, body) = DecodeDataUriParts(resourceUrl);
            if (string.Equals(mimeType, "text/html", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mimeType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(mimeType))
                return (!string.IsNullOrEmpty(body) ? body : null, mimeType);
            // Non-HTML data URIs: return body with detected MIME type
            return (!string.IsNullOrEmpty(body) ? body : null, mimeType);
        }

        // Detect content type from extension for non-HTML resources
        var extensionMime = GetMimeTypeForExtension(resourceUrl);

        // Try local base path first (before URL resolution and HTTP fetch)
        if (!string.IsNullOrEmpty(_localBasePath))
        {
            var localResult = TryReadLocalResource(resourceUrl, extensionMime);
            if (localResult.content != null || localResult.contentType != string.Empty)
                return localResult;
        }

        // Resolve relative URL against page URL
        string resolvedUrl;
        if (Uri.TryCreate(resourceUrl, UriKind.Absolute, out _))
        {
            resolvedUrl = resourceUrl;
        }
        else if (Uri.TryCreate(string.IsNullOrWhiteSpace(baseUrl) ? _pageUrl : baseUrl, UriKind.Absolute, out var baseUri) &&
                 Uri.TryCreate(baseUri, resourceUrl, out var resolved))
        {
            resolvedUrl = resolved.AbsoluteUri;
        }
        else
        {
            return (null, extensionMime);
        }

        // Handle file:// URLs — read directly from local filesystem
        if (resolvedUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadFileResource(resolvedUrl, extensionMime);
        }

        if (TryMapLocalWptHttpResource(resolvedUrl) is { } localWptPath)
        {
            return TryReadFileResource(new Uri(localWptPath).AbsoluteUri, extensionMime);
        }

        // Only fetch HTTP/HTTPS URLs
        if (!resolvedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !resolvedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return (null, extensionMime);

        try
        {
            using var response = SharedHttpClient.GetAsync(resolvedUrl).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                return (null, FetchFailedContentType);

            var contentType = response.Content.Headers.ContentType?.MediaType ?? extensionMime;
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return (content, contentType);
        }
        catch
        {
            return (null, FetchFailedContentType);
        }
    }

    /// <summary>Sentinel content type indicating a network/file fetch failure (404, connection refused, etc.).</summary>
    private const string FetchFailedContentType = "__fetch_failed__";

    /// <summary>
    /// Reads a file:// URL from the local filesystem and returns its content with detected MIME type.
    /// </summary>
    private static (string? content, string contentType) TryReadFileResource(string fileUrl, string extensionMime)
    {
        try
        {
            var uri = new Uri(fileUrl);
            var path = uri.LocalPath;
            if (!System.IO.File.Exists(path))
                return (null, string.Empty); // File not found → empty document (not a fetch failure)

            // For binary content types (images, fonts, etc.) return null content with MIME type
            if (extensionMime.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                extensionMime.StartsWith("font/", StringComparison.OrdinalIgnoreCase) ||
                extensionMime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
                extensionMime.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extensionMime, "application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return (null, extensionMime);
            }

            var content = System.IO.File.ReadAllText(path);
            return (content, extensionMime);
        }
        catch
        {
            return (null, string.Empty);
        }
    }

    /// <summary>
    /// Attempts to read a resource from the local base path directory.
    /// Strips query strings from the filename. Detects content type from content
    /// when extension-based detection returns a generic type (e.g. for XHTML files
    /// served without a recognized extension).
    /// </summary>
    private (string? content, string contentType) TryReadLocalResource(string resourceUrl, string extensionMime)
    {
        if (string.IsNullOrEmpty(_localBasePath))
            return (null, string.Empty);

        // Strip query string and fragment from the URL to get the filename
        var filename = resourceUrl;
        var qIdx = filename.IndexOf('?');
        if (qIdx >= 0) filename = filename.Substring(0, qIdx);
        var hIdx = filename.IndexOf('#');
        if (hIdx >= 0) filename = filename.Substring(0, hIdx);

        // Only handle relative URLs (no scheme)
        if (filename.Contains("://")) return (null, string.Empty);

        var localPath = System.IO.Path.Combine(_localBasePath, filename);
        if (!System.IO.File.Exists(localPath))
            return (null, string.Empty);

        // For binary content types (images, fonts, etc.) return null content with MIME type
        if (extensionMime.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
            extensionMime.StartsWith("font/", StringComparison.OrdinalIgnoreCase) ||
            extensionMime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
            extensionMime.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extensionMime, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return (null, extensionMime);
        }

        try
        {
            var content = System.IO.File.ReadAllText(localPath);

            // Detect content type from content when extension is generic
            var detectedMime = extensionMime;
            if (string.Equals(detectedMime, "application/octet-stream", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(detectedMime))
            {
                detectedMime = DetectContentTypeFromContent(content, filename);
            }

            return (content, detectedMime);
        }
        catch
        {
            return (null, string.Empty);
        }
    }

    /// <summary>
    /// Detects the MIME type of text content based on its initial bytes/structure.
    /// Used for files without a recognized extension (e.g. xhtml.1, xhtml.2).
    /// </summary>
    private static string DetectContentTypeFromContent(string content, string filename)
    {
        if (string.IsNullOrEmpty(content))
            return "text/plain";

        var trimmed = content.TrimStart();

        // SVG detection
        if (trimmed.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
            (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && trimmed.Contains("<svg", StringComparison.OrdinalIgnoreCase)))
            return "image/svg+xml";

        // XHTML detection (has xmlns on root html element)
        if (trimmed.Contains("xmlns=\"http://www.w3.org/1999/xhtml", StringComparison.OrdinalIgnoreCase))
            return "application/xhtml+xml";

        // Generic XML detection
        if (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
            (trimmed.StartsWith("<") && !trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) &&
             !trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase)))
            return "application/xml";

        // HTML detection
        if (trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            return "text/html";

        return "text/plain";
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

        // parsedRoot is the <html> element itself (HtmlTreeBuilder returns it directly).
        // Move it under #subdoc-root as the documentElement.
        parsedRoot.Parent = docRoot;
        docRoot.Children.Add(parsedRoot);

        containerElement.Children.Insert(0, docRoot);
        _elements.Add(docRoot);
        _elements.Add(parsedRoot);
        // Add structural children (head, body) that HtmlTreeBuilder does not include in allElements
        foreach (var child in parsedRoot.Children)
            AddElementsRecursive(child);
        // Add non-structural elements from the builder (skip any already registered)
        foreach (var el in allElements)
        {
            if (!_elements.Contains(el))
                _elements.Add(el);
        }

        return docRoot;
    }

    /// <summary>
    /// Recursively adds a DomElement and all its descendants to the element tracking list.
    /// Used by <see cref="BuildSubDocumentFromHtml"/> to register structural elements
    /// (html, head, body) that <see cref="HtmlTreeBuilder"/> does not include in its allElements list.
    /// </summary>
    private void AddElementsRecursive(DomElement element)
    {
        if (!_elements.Contains(element))
            _elements.Add(element);
        foreach (var child in element.Children)
            AddElementsRecursive(child);
    }

    /// <summary>
    /// Builds a sub-document tree from XML/SVG/XHTML content using an XML parser.
    /// For XHTML with valid namespace, also executes embedded scripts.
    /// XML well-formedness errors result in an empty document.
    /// </summary>
    private DomElement BuildSubDocumentFromXml(string xmlContent, string contentType, DomElement containerElement)
    {
        var docRoot = new DomElement("#subdoc-root", null, null, string.Empty);
        docRoot.Parent = containerElement;

        try
        {
            // Strip XML processing instructions before parsing (XDocument doesn't need them)
            var cleanXml = xmlContent;
            while (cleanXml.TrimStart().StartsWith("<?xml-stylesheet", StringComparison.OrdinalIgnoreCase))
            {
                var piEnd = cleanXml.IndexOf("?>", StringComparison.Ordinal);
                if (piEnd >= 0) cleanXml = cleanXml.Substring(piEnd + 2).TrimStart();
                else break;
            }

            var xdoc = System.Xml.Linq.XDocument.Parse(cleanXml);
            if (xdoc.Root == null)
            {
                containerElement.Children.Insert(0, docRoot);
                _elements.Add(docRoot);
                return docRoot;
            }

            // Check XHTML namespace validity
            var rootNs = xdoc.Root.Name.NamespaceName;
            var isXhtml = string.Equals(contentType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase);
            var hasCorrectXhtmlNs = string.Equals(rootNs, "http://www.w3.org/1999/xhtml", StringComparison.Ordinal);

            if (isXhtml && !hasCorrectXhtmlNs)
            {
                // Wrong XHTML namespace — create empty doc, don't execute scripts
                var emptyDoc = BuildEmptySubDocument(containerElement);
                // Remove the docRoot we already created since BuildEmptySubDocument creates its own
                return emptyDoc;
            }

            // Build DOM tree from XML
            var rootEl = BuildDomElementFromXElement(xdoc.Root);
            rootEl.Parent = docRoot;
            docRoot.Children.Add(rootEl);

            containerElement.Children.Insert(0, docRoot);
            _elements.Add(docRoot);
            _elements.Add(rootEl);

            // Execute scripts in XHTML documents with correct namespace
            if (isXhtml && hasCorrectXhtmlNs)
            {
                ExecuteSubDocumentScripts(docRoot);
            }
        }
        catch (System.Xml.XmlException)
        {
            // XML well-formedness error — return empty document, don't execute scripts
            containerElement.Children.Insert(0, docRoot);
            _elements.Add(docRoot);
        }

        return docRoot;
    }

    /// <summary>
    /// Recursively builds a DomElement tree from an XElement.
    /// </summary>
    private DomElement BuildDomElementFromXElement(System.Xml.Linq.XElement xe)
    {
        var tagName = xe.Name.LocalName.ToLowerInvariant();
        var el = new DomElement(tagName, null, null, string.Empty);

        foreach (var attr in xe.Attributes())
        {
            if (!attr.IsNamespaceDeclaration)
                el.Attributes[attr.Name.LocalName] = attr.Value;
        }

        foreach (var child in xe.Nodes())
        {
            if (child is System.Xml.Linq.XElement childXe)
            {
                var childEl = BuildDomElementFromXElement(childXe);
                childEl.Parent = el;
                el.Children.Add(childEl);
                _elements.Add(childEl);
            }
            else if (child is System.Xml.Linq.XText childText)
            {
                var textNode = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                textNode.TextContent = childText.Value;
                textNode.Parent = el;
                el.Children.Add(textNode);
                _elements.Add(textNode);
            }
        }

        return el;
    }

    /// <summary>
    /// Finds and executes script elements within a sub-document tree.
    /// Scripts call parent.notify() etc. in the main JS context.
    /// </summary>
    private void ExecuteSubDocumentScripts(DomElement docRoot)
    {
        if (_jsContext == null) return;

        var scripts = new List<string>();
        CollectScriptContent(docRoot, scripts);

        foreach (var scriptCode in scripts)
        {
            try
            {
                _jsContext.Eval(scriptCode);
            }
            catch (Exception ex)
            {
                RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.ExecuteSubDocumentScripts",
                    $"Sub-document script error: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Recursively collects text content from script elements.
    /// </summary>
    private static void CollectScriptContent(DomElement element, List<string> scripts)
    {
        if (string.Equals(element.TagName, "script", StringComparison.OrdinalIgnoreCase))
        {
            var text = GetTextContentRecursive(element);
            if (!string.IsNullOrWhiteSpace(text))
                scripts.Add(text);
            return;
        }

        foreach (var child in element.Children)
            CollectScriptContent(child, scripts);
    }

    /// <summary>
    /// Gets the concatenated text content of an element and all its descendants.
    /// </summary>
    private static string GetTextContentRecursive(DomElement element)
    {
        if (element.IsTextNode)
            return element.TextContent ?? string.Empty;

        var sb = new StringBuilder();
        foreach (var child in element.Children)
            sb.Append(GetTextContentRecursive(child));
        return sb.ToString();
    }

    /// <summary>
    /// Builds a full document JSObject for a sub-document tree rooted at the given element.
    /// </summary>
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
            new JSFunction((in Arguments _) => ToJSObject(GetDocumentElement()), "get documentElement"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        doc.FastAddProperty(
            (KeyString)"scrollingElement",
            new JSFunction((in Arguments _) => ToJSObject(GetDocumentElement()), "get scrollingElement"),
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

        // title (dynamic getter from <title> element in <head>)
        doc.FastAddProperty(
            (KeyString)"title",
            new JSFunction((in Arguments _) =>
            {
                var htmlEl = GetDocumentElement();
                var head = htmlEl.Children
                    .FirstOrDefault(c => string.Equals(c.TagName, "head", StringComparison.OrdinalIgnoreCase));
                if (head != null)
                {
                    var titleEl = head.Children
                        .FirstOrDefault(c => string.Equals(c.TagName, "title", StringComparison.OrdinalIgnoreCase));
                    if (titleEl != null)
                    {
                        // Two paths: (1) textContent setter clears children and stores text directly
                        // in TextContent, (2) text set via child text nodes (e.g. createTextNode + appendChild).
                        if (titleEl.TextContent != null && titleEl.Children.Count == 0)
                            return new JSString(titleEl.TextContent);
                        var sb = new StringBuilder();
                        CollectTextContent(titleEl, sb);
                        return new JSString(sb.ToString());
                    }
                }
                return new JSString(string.Empty);
            }, "get title"),
            new JSFunction((in Arguments a) =>
            {
                var htmlEl = GetDocumentElement();
                var head = htmlEl.Children
                    .FirstOrDefault(c => string.Equals(c.TagName, "head", StringComparison.OrdinalIgnoreCase));
                if (head != null)
                {
                    var titleEl = head.Children
                        .FirstOrDefault(c => string.Equals(c.TagName, "title", StringComparison.OrdinalIgnoreCase));
                    if (titleEl != null)
                    {
                        titleEl.TextContent = a.Length > 0 ? a[0].ToString() : string.Empty;
                        titleEl.Children.Clear();
                    }
                }
                return JSUndefined.Value;
            }, "set title"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // forms (dynamic collection of <form> elements)
        doc.FastAddProperty(
            (KeyString)"forms",
            new JSFunction((in Arguments _) =>
            {
                var results = new List<JSValue>();
                CollectByTagName(docRoot, "form", results);
                return new JSArray(results);
            }, "get forms"),
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
                    ? ToJSObject(docRoot.Children[0])
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
                    ? ToJSObject(docRoot.Children[^1])
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
                return found != null ? ToJSObject(found) : JSNull.Value;
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
                var tagName = a[0].ToString();
                ValidateElementName(tagName, _jsContext!);
                tagName = AsciiToLower(tagName);
                var el = new DomElement(tagName, null, null, string.Empty);
                el.OwnerDocRoot = docRoot;
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
                el.OwnerDocRoot = docRoot;
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
                el.OwnerDocRoot = docRoot;
                _elements.Add(el);
                return ToJSObject(el);
            }, "createComment", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createElementNS(ns, localName)
        doc.FastAddValue(
            (KeyString)"createElementNS",
            new JSFunction((in Arguments a) =>
            {
                var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
                var localName = a.Length > 1 ? a[1].ToString() : (a.Length > 0 ? a[0].ToString() : "div");
                ValidateQualifiedName(localName, ns, _jsContext!);
                var el = new DomElement(localName, null, null, string.Empty);
                if (!string.IsNullOrEmpty(ns))
                    el.NamespaceURI = ns;
                el.OwnerDocRoot = docRoot;
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
                return found != null ? ToJSObject(found) : JSNull.Value;
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

        doc.FastAddValue(
            (KeyString)"elementFromPoint",
            new JSFunction((in Arguments a) =>
            {
                var hit = HitTestDocumentPoint(docRoot, GetCoordinateArgument(a, 0), GetCoordinateArgument(a, 1))
                    .FirstOrDefault();
                return hit != null ? ToJSObject(hit) : JSNull.Value;
            }, "elementFromPoint", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        doc.FastAddValue(
            (KeyString)"elementsFromPoint",
            new JSFunction((in Arguments a) =>
            {
                var hits = HitTestDocumentPoint(docRoot, GetCoordinateArgument(a, 0), GetCoordinateArgument(a, 1));
                return new JSArray(hits.Select(ToJSObject).ToArray());
            }, "elementsFromPoint", 2),
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
                    (string.Equals(el.TagName, "a", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(el.TagName, "area", StringComparison.OrdinalIgnoreCase)) &&
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
                        var idx = docRoot.Children.IndexOf(child);
                        if (idx >= 0)
                        {
                            bridge.NotifyNodeIteratorPreRemoval(child);
                            docRoot.Children.RemoveAt(idx);
                            child.Parent = null;
                            bridge.NotifyChildRemoved(docRoot, child, idx);
                        }
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
                subDocRoot.DomProperties["_hasViewport"] = false;
                _elements.Add(subDocRoot);

                if (doctypeArg is JSObject dtObj)
                {
                    foreach (var kvp in _jsObjectCache)
                    {
                        if (kvp.Value == dtObj)
                        {
                            var dtEl = kvp.Key;
                            dtEl.Parent = subDocRoot;
                            dtEl.OwnerDocRoot = subDocRoot;
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
                    docEl.OwnerDocRoot = subDocRoot;
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
                subDocRoot.DomProperties["_hasViewport"] = false;
                _elements.Add(subDocRoot);

                var dt = new DomElement("#doctype", null, null, string.Empty);
                dt.DomProperties["name"] = "html";
                dt.DomProperties["publicId"] = string.Empty;
                dt.DomProperties["systemId"] = string.Empty;
                dt.DomProperties["internalSubset"] = null;
                dt.Parent = subDocRoot;
                dt.OwnerDocRoot = subDocRoot;
                subDocRoot.Children.Add(dt);
                _elements.Add(dt);

                var subHtml = new DomElement("html", null, null, string.Empty);
                subHtml.NamespaceURI = "http://www.w3.org/1999/xhtml";
                subHtml.Parent = subDocRoot;
                subHtml.OwnerDocRoot = subDocRoot;
                subDocRoot.Children.Add(subHtml);
                _elements.Add(subHtml);

                var subHead = new DomElement("head", null, null, string.Empty);
                subHead.Parent = subHtml;
                subHead.OwnerDocRoot = subDocRoot;
                subHtml.Children.Add(subHead);
                _elements.Add(subHead);

                if (subTitle != null)
                {
                    var subTitleEl = new DomElement("title", null, null, string.Empty);
                    subTitleEl.Parent = subHead;
                    subTitleEl.OwnerDocRoot = subDocRoot;
                    subHead.Children.Add(subTitleEl);
                    _elements.Add(subTitleEl);

                    var subTitleText = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                    subTitleText.TextContent = subTitle;
                    subTitleText.Parent = subTitleEl;
                    subTitleText.OwnerDocRoot = subDocRoot;
                    subTitleEl.Children.Add(subTitleText);
                    _elements.Add(subTitleText);
                }

                var subBody = new DomElement("body", null, null, string.Empty);
                subBody.Parent = subHtml;
                subBody.OwnerDocRoot = subDocRoot;
                subHtml.Children.Add(subBody);
                _elements.Add(subBody);

                return BuildSubDocument(subDocRoot);
            }, "createHTMLDocument", 1),
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
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    throw new JSException("Failed to execute 'createTreeWalker': 1 argument required.");
                var rootObj = a[0] as JSObject;
                if (rootObj == null)
                    throw new JSException("Failed to execute 'createTreeWalker': parameter 1 is not of type 'Node'.");
                var rootEl = bridge.FindDomElementByJSObject(rootObj);
                if (rootEl == null) return JSNull.Value;

                var whatToShow = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? unchecked((int)(uint)a[1].DoubleValue) : unchecked((int)0xFFFFFFFF);
                var filterFn = a.Length > 2 && a[2] is JSFunction f ? f : (a.Length > 2 && a[2] is JSObject filterObj ? filterObj[(KeyString)"acceptNode"] as JSFunction : null);

                return bridge.BuildTreeWalker(rootEl, whatToShow, filterFn);
            }, "createTreeWalker", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // createNodeIterator(root, whatToShow, filter)
        doc.FastAddValue(
            (KeyString)"createNodeIterator",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0)
                    throw new JSException("Failed to execute 'createNodeIterator': 1 argument required.");
                var rootObj = a[0] as JSObject;
                if (rootObj == null)
                    throw new JSException("Failed to execute 'createNodeIterator': parameter 1 is not of type 'Node'.");
                var rootEl = bridge.FindDomElementByJSObject(rootObj);
                if (rootEl == null) return JSNull.Value;

                var whatToShow = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? unchecked((int)(uint)a[1].DoubleValue) : unchecked((int)0xFFFFFFFF);
                var filterFn = a.Length > 2 && a[2] is JSFunction f ? f : (a.Length > 2 && a[2] is JSObject filterObj ? filterObj[(KeyString)"acceptNode"] as JSFunction : null);

                return bridge.BuildNodeIterator(rootEl, whatToShow, filterFn);
            }, "createNodeIterator", 3),
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

    private static double GetCoordinateArgument(in Arguments args, int index) =>
        args.Length > index && !args[index].IsNull && !args[index].IsUndefined
            ? args[index].DoubleValue
            : double.NaN;

    private IReadOnlyList<DomElement> HitTestDocumentPoint(DomElement docRoot, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
            return Array.Empty<DomElement>();

        var documentElement = IsDocumentElement(docRoot)
            ? docRoot
            : docRoot.Children.FirstOrDefault(c => !c.IsTextNode && !c.TagName.StartsWith("#"));
        if (documentElement == null)
            return Array.Empty<DomElement>();

        if (!DocumentHasViewport(documentElement))
            return Array.Empty<DomElement>();

        var viewportWidth = GetViewportReferenceLength(documentElement, vertical: false);
        var viewportHeight = GetViewportReferenceLength(documentElement, vertical: true);
        if (viewportWidth <= 0 || viewportHeight <= 0 || x < 0 || y < 0 || x >= viewportWidth || y >= viewportHeight)
            return Array.Empty<DomElement>();

        var hits = new List<DomElement>();
        CollectHitTestMatches(documentElement, x, y, hits);
        return hits;
    }

    private void CollectHitTestMatches(DomElement element, double x, double y, List<DomElement> hits)
    {
        for (var i = element.Children.Count - 1; i >= 0; i--)
        {
            var child = element.Children[i];
            if (!child.IsTextNode && !child.TagName.StartsWith("#", StringComparison.Ordinal))
                CollectHitTestMatches(child, x, y, hits);
        }

        if (IsElementHitTestCandidate(element, x, y))
            hits.Add(element);
    }

    private bool IsElementHitTestCandidate(DomElement element, double x, double y)
    {
        if (!IsElementRenderedForHitTesting(element))
            return false;

        var props = GetComputedProps(element);
        if (string.Equals(props.GetValueOrDefault("pointer-events"), "none", StringComparison.OrdinalIgnoreCase))
            return false;

        var rect = GetHitTestRectForElement(element);
        if (rect.Width <= 0 || rect.Height <= 0)
            return false;

        return x >= rect.Left && x < rect.Left + rect.Width &&
               y >= rect.Top && y < rect.Top + rect.Height;
    }

    private (double Left, double Top, double Width, double Height) GetHitTestRectForElement(DomElement element)
    {
        if (IsDocumentElement(element))
            return GetBoundingClientRectForDomElement(element, isRoot: true);

        var rect = GetBoundingClientRectForDomElement(element, isRoot: false);
        var documentElement = GetOwningDocumentElement(element);
        if (!IsViewportBodyElement(element, documentElement))
            return rect;

        return (
            rect.Left,
            rect.Top,
            Math.Max(rect.Width, GetScrollWidthForDomElement(element, isRoot: false)),
            Math.Max(rect.Height, GetScrollHeightForDomElement(element, isRoot: false)));
    }

    private bool IsElementRenderedForHitTesting(DomElement element)
    {
        for (var current = element; current != null; current = current.Parent)
        {
            if (current.IsTextNode)
                return false;

            if (current.TagName.StartsWith("#", StringComparison.Ordinal))
                continue;

            var props = GetComputedProps(current);
            var display = props.GetValueOrDefault("display");
            if (string.Equals(display, "none", StringComparison.OrdinalIgnoreCase))
                return false;

            var visibility = props.GetValueOrDefault("visibility");
            if (string.Equals(visibility, "hidden", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(visibility, "collapse", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool DocumentHasViewport(DomElement documentElement)
    {
        var docRoot = documentElement.OwnerDocRoot;
        if (docRoot == null)
            return true;

        return !docRoot.DomProperties.TryGetValue("_hasViewport", out var value) ||
               value is not bool hasViewport ||
               hasViewport;
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

    /// <summary>
    /// Collects all <c>&lt;a&gt;</c> and <c>&lt;area&gt;</c> elements with an
    /// <c>href</c> attribute in document tree order.
    /// </summary>
    private void CollectLinksInTreeOrder(DomElement root, List<JSValue> results)
    {
        foreach (var child in root.Children)
        {
            if (!child.IsTextNode &&
                (string.Equals(child.TagName, "a", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(child.TagName, "area", StringComparison.OrdinalIgnoreCase)) &&
                child.Attributes.ContainsKey("href"))
            {
                results.Add(ToJSObject(child));
            }
            CollectLinksInTreeOrder(child, results);
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
                InvalidateStyleScope(element);
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
                InvalidateStyleScope(element);
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
