using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.Core.Core.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Core.Core.Array;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;

namespace Broiler.App.Rendering;

/// <summary>
/// Internal helper methods — string conversions, DOM tree utilities,
/// form-control collection, table-row helpers, and JS-object builders
/// for <c>style</c>, <c>classList</c>, <c>localStorage</c>, and
/// <c>canvas.getContext("2d")</c>.
/// </summary>
public sealed partial class DomBridge
{
    private static string ToCamelCaseStatic(string cssName)
    {
        var sb = new StringBuilder();
        bool upper = false;
        foreach (char c in cssName)
        {
            if (c == '-') { upper = true; continue; }
            sb.Append(upper ? char.ToUpperInvariant(c) : c);
            upper = false;
        }
        return sb.ToString();
    }

    /// <summary>Converts JS camelCase property names to CSS kebab-case.</summary>
    private static string ToKebabCase(string camelName)
    {
        var sb = new StringBuilder();
        foreach (char c in camelName)
        {
            if (char.IsUpper(c))
            {
                sb.Append('-');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Parses a DOCTYPE declaration and creates a DomElement representing the DocumentType node.
    /// </summary>
    private DomElement? ParseDocType(string html)
    {
        var match = DocTypePattern.Match(html);
        if (!match.Success) return null;

        var name = match.Groups[1].Value;
        var publicId = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;
        var systemId = match.Groups[3].Success ? match.Groups[3].Value : string.Empty;

        var doctype = new DomElement("#doctype", null, null, string.Empty);
        doctype.DomProperties["name"] = name;
        doctype.DomProperties["publicId"] = publicId;
        doctype.DomProperties["systemId"] = systemId;
        doctype.DomProperties["internalSubset"] = null;

        return doctype;
    }

    /// <summary>Gets the lowercase name from a DOCTYPE DomElement.</summary>
    private static string GetDocTypeName(DomElement element)
    {
        var dtName = element.DomProperties.TryGetValue("name", out var n) ? n?.ToString() ?? "html" : "html";
        return dtName.ToLowerInvariant();
    }

    /// <summary>
    /// Searches descendants of an element using a CSS selector.
    /// </summary>
    private static JSValue FindInDescendants(DomElement root, string selector, bool all, DomBridge bridge)
    {
        var results = new List<JSValue>();
        SearchDescendants(root, selector, results, bridge, all);
        if (all) return new JSArray(results);
        return results.Count > 0 ? results[0] : JSNull.Value;
    }

    private static void SearchDescendants(DomElement parent, string selector, List<JSValue> results, DomBridge bridge, bool all)
    {
        foreach (var child in parent.Children)
        {
            if (!child.IsTextNode && MatchesSelector(child, selector))
            {
                results.Add(bridge.ToJSObject(child));
                if (!all) return;
            }
            SearchDescendants(child, selector, results, bridge, all);
            if (!all && results.Count > 0) return;
        }
    }

    /// <summary>
    /// Recursively collects text content from a node and its descendants.
    /// </summary>
    private static void CollectTextContent(DomElement node, StringBuilder sb)
    {
        if (node.IsTextNode)
        {
            sb.Append(node.TextContent ?? string.Empty);
            return;
        }
        foreach (var child in node.Children)
            CollectTextContent(child, sb);
    }

    /// <summary>
    /// Returns <c>true</c> if the resource URL points to a non-HTML content type
    /// based on file extension (e.g., .png, .txt, .jpg, .gif, .pdf).
    /// </summary>
    private static bool IsNonHtmlResource(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        // Strip query string and fragment
        var path = url;
        var qIndex = path.IndexOf('?');
        if (qIndex >= 0) path = path.Substring(0, qIndex);
        var hIndex = path.IndexOf('#');
        if (hIndex >= 0) path = path.Substring(0, hIndex);

        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".ico" or ".svg" => true,
            ".txt" or ".text" => true,
            ".pdf" or ".zip" or ".tar" or ".gz" => true,
            ".js" or ".mjs" or ".json" or ".xml" or ".css" => true,
            ".mp3" or ".mp4" or ".wav" or ".ogg" or ".webm" or ".avi" => true,
            ".woff" or ".woff2" or ".ttf" or ".otf" or ".eot" => true,
            _ => false,
        };
    }

    /// <summary>
    /// Returns the MIME type for a given file extension.
    /// </summary>
    internal static string GetMimeTypeForExtension(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "application/octet-stream";
        var path = url;
        var qIndex = path.IndexOf('?');
        if (qIndex >= 0) path = path.Substring(0, qIndex);
        var hIndex = path.IndexOf('#');
        if (hIndex >= 0) path = path.Substring(0, hIndex);

        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" or ".mjs" => "application/javascript",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".txt" or ".text" => "text/plain",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",
            ".bmp" => "image/bmp",
            ".pdf" => "application/pdf",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".otf" => "font/otf",
            ".mp3" => "audio/mpeg",
            ".mp4" => "video/mp4",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".webm" => "video/webm",
            _ => "application/octet-stream",
        };
    }

    /// <summary>
    /// Decodes a <c>data:</c> URI and returns the MIME type and decoded body content.
    /// Supports percent-encoded and base64-encoded payloads, as well as nested data URIs.
    /// </summary>
    private static (string mimeType, string body) DecodeDataUriParts(string dataUri)
    {
        if (!dataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return (string.Empty, string.Empty);

        var rest = dataUri[5..]; // strip "data:"
        var commaIdx = rest.IndexOf(',');
        if (commaIdx < 0)
            return (string.Empty, string.Empty);

        var meta = rest[..commaIdx]; // e.g. "text/html;base64" or "text/html;charset=utf-8"
        var payload = rest[(commaIdx + 1)..];

        // Extract MIME type (before any semicolons)
        var mimeType = meta;
        var semiIdx = meta.IndexOf(';');
        if (semiIdx >= 0)
            mimeType = meta[..semiIdx];
        if (string.IsNullOrEmpty(mimeType))
            mimeType = "text/plain"; // default per RFC 2397

        string body;
        if (meta.Contains("base64", StringComparison.OrdinalIgnoreCase))
        {
            var decoded = Uri.UnescapeDataString(payload);
            // Strip whitespace (RFC 2045 allows folding)
            decoded = Regex.Replace(decoded, @"\s", string.Empty);
            try
            {
                var bytes = Convert.FromBase64String(decoded);
                body = Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
                // Malformed base64 payload — return empty body so the caller
                // falls back to the default empty-document path.
                body = string.Empty;
            }
        }
        else
        {
            body = Uri.UnescapeDataString(payload);
        }

        return (mimeType.Trim(), body);
    }

    /// <summary>
    /// Returns <c>true</c> if the target URL is cross-origin relative to the page URL.
    /// Relative URLs and file:// URLs are treated as same-origin.
    /// </summary>
    private static bool IsCrossOrigin(string targetUrl, string pageUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl)) return false;
        // about:blank inherits the origin of the embedding document (always same-origin)
        if (string.Equals(targetUrl, "about:blank", StringComparison.OrdinalIgnoreCase)) return false;
        // data: URIs inherit the origin of the embedding document (always same-origin)
        if (targetUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return false;
        // Relative URLs are always same-origin
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var targetUri)) return false;
        // file:// URLs are same-origin with each other
        if (string.Equals(targetUri.Scheme, "file", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.IsNullOrWhiteSpace(pageUrl)) return false;
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri)) return false;
        // Same-origin: same scheme + host + port
        return !string.Equals(targetUri.Scheme, pageUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(targetUri.Host, pageUri.Host, StringComparison.OrdinalIgnoreCase) ||
               targetUri.Port != pageUri.Port;
    }

    /// <summary>
    /// Finds the <see cref="DomElement"/> corresponding to a given <see cref="JSObject"/>
    /// by looking up the JS object cache.
    /// </summary>
    private DomElement? FindDomElementByJSObject(JSObject jsObj)
    {
        foreach (var kvp in _jsObjectCache)
        {
            if (ReferenceEquals(kvp.Value, jsObj))
                return kvp.Key;
        }
        return null;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="candidate"/> is a descendant of
    /// <paramref name="ancestor"/> in the DOM tree.
    /// </summary>
    private static bool IsDescendant(DomElement ancestor, DomElement candidate)
    {
        var current = candidate.Parent;
        while (current != null)
        {
            if (ReferenceEquals(current, ancestor)) return true;
            current = current.Parent;
        }
        return false;
    }

    /// <summary>
    /// Clones a <see cref="DomElement"/>. When <paramref name="deep"/> is true,
    /// all descendants are recursively cloned.
    /// </summary>
    private DomElement CloneDomElement(DomElement source, bool deep)
    {
        var attrs = new Dictionary<string, string>(source.Attributes, StringComparer.OrdinalIgnoreCase);
        var style = new Dictionary<string, string>(source.Style, StringComparer.OrdinalIgnoreCase);
        var clone = new DomElement(source.TagName, source.Id, source.ClassName, source.InnerHtml, style, attrs, source.IsTextNode);
        clone.TextContent = source.TextContent;
        clone.NamespaceURI = source.NamespaceURI;
        foreach (var kv in source.NsAttrMap)
            clone.NsAttrMap[kv.Key] = kv.Value;
        // Copy DomProperties (e.g., checked state for inputs)
        foreach (var kv in source.DomProperties)
            clone.DomProperties[kv.Key] = kv.Value;

        if (deep)
        {
            foreach (var child in source.Children)
            {
                var childClone = CloneDomElement(child, true);
                childClone.Parent = clone;
                clone.Children.Add(childClone);
                _elements.Add(childClone);
            }
        }
        return clone;
    }

    /// <summary>
    /// Collects all &lt;tr&gt; elements in a table in HTML spec order:
    /// 1. thead rows, 2. tbody rows + direct tr children (in tree order), 3. tfoot rows.
    /// </summary>
    private static List<DomElement> CollectTableRows(DomElement table)
    {
        var rows = new List<DomElement>();
        // 1. All tr children of thead elements (in tree order)
        foreach (var child in table.Children)
        {
            if (string.Equals(child.TagName, "thead", StringComparison.OrdinalIgnoreCase))
                foreach (var c in child.Children)
                    if (string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase))
                        rows.Add(c);
        }
        // 2. All tr children that are direct children of table, or children of tbody elements (in tree order)
        foreach (var child in table.Children)
        {
            var ctag = child.TagName.ToLowerInvariant();
            if (ctag == "tr")
                rows.Add(child);
            else if (ctag == "tbody")
                foreach (var c in child.Children)
                    if (string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase))
                        rows.Add(c);
        }
        // 3. All tr children of tfoot elements (in tree order)
        foreach (var child in table.Children)
        {
            if (string.Equals(child.TagName, "tfoot", StringComparison.OrdinalIgnoreCase))
                foreach (var c in child.Children)
                    if (string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase))
                        rows.Add(c);
        }
        return rows;
    }

    /// <summary>
    /// Builds a JSArray of table rows for the 'rows' property.
    /// </summary>
    private JSArray BuildTableRows(DomElement table)
    {
        var rows = CollectTableRows(table);
        var jsRows = new List<JSValue>();
        foreach (var r in rows)
            jsRows.Add(ToJSObject(r));
        var arr = new JSArray(jsRows);
        arr.FastAddProperty(
            (KeyString)"length",
            new JSFunction((in Arguments _) => new JSNumber(jsRows.Count), "get length"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        return arr;
    }

    /// <summary>
    /// Inserts a row into a table at the given index, per HTMLTableElement.insertRow() spec.
    /// </summary>
    private JSValue InsertTableRow(DomElement table, int index, DomBridge bridge)
    {
        var tr = new DomElement("tr", null, null, string.Empty);
        bridge._elements.Add(tr);

        var allRows = CollectTableRows(table);
        if (allRows.Count == 0 || index == -1 || index == allRows.Count)
        {
            // Find the last section to append to, or create a tbody
            DomElement? lastSection = null;
            for (int i = table.Children.Count - 1; i >= 0; i--)
            {
                var ctag = table.Children[i].TagName.ToLowerInvariant();
                if (ctag == "thead" || ctag == "tbody" || ctag == "tfoot")
                {
                    lastSection = table.Children[i];
                    break;
                }
            }
            if (lastSection == null && allRows.Count == 0)
            {
                // No sections and no rows at all: create a new tbody per spec
                var tbody = new DomElement("tbody", null, null, string.Empty);
                bridge._elements.Add(tbody);
                tbody.Parent = table;
                table.Children.Add(tbody);
                lastSection = tbody;
            }
            if (lastSection != null)
            {
                tr.Parent = lastSection;
                lastSection.Children.Add(tr);
            }
            else
            {
                tr.Parent = table;
                table.Children.Add(tr);
            }
        }
        else if (index >= 0 && index < allRows.Count)
        {
            var refRow = allRows[index];
            var parent = refRow.Parent ?? table;
            tr.Parent = parent;
            var idx = parent.Children.IndexOf(refRow);
            parent.Children.Insert(idx >= 0 ? idx : parent.Children.Count, tr);
        }
        return ToJSObject(tr);
    }

    /// <summary>
    /// Collects form control elements (input, select, textarea, button) from a form.
    /// </summary>
    internal static List<DomElement> CollectFormControls(DomElement form)
    {
        var controls = new List<DomElement>();
        CollectFormControlsRecursive(form, controls);
        return controls;
    }

    private static void CollectFormControlsRecursive(DomElement parent, List<DomElement> controls)
    {
        foreach (var child in parent.Children)
        {
            var ctag = child.TagName.ToLowerInvariant();
            if (ctag == "input" || ctag == "select" || ctag == "textarea" || ctag == "button")
                controls.Add(child);
            CollectFormControlsRecursive(child, controls);
        }
    }

    /// <summary>
    /// Recursively unchecks all radio inputs with the given name within the scope,
    /// except for the specified element. Used for radio button mutual exclusion.
    /// </summary>
    private static void UncheckRadioSiblings(DomElement scope, DomElement except, string radioName)
    {
        foreach (var child in scope.Children)
        {
            if (!child.IsTextNode && !ReferenceEquals(child, except))
            {
                if (string.Equals(child.TagName, "input", StringComparison.OrdinalIgnoreCase) &&
                    child.Attributes.TryGetValue("type", out var st) &&
                    string.Equals(st, "radio", StringComparison.OrdinalIgnoreCase) &&
                    child.Attributes.TryGetValue("name", out var sn) &&
                    string.Equals(sn, radioName, StringComparison.Ordinal))
                {
                    child.DomProperties["checked"] = false;
                }
                UncheckRadioSiblings(child, except, radioName);
            }
        }
    }

    /// <summary>
    /// Builds a form.elements collection (JSObject with indexed + named access).
    /// </summary>
    private JSValue BuildFormElementsCollection(DomElement form, DomBridge bridge)
    {
        var controls = CollectFormControls(form);

        // Use FormElementsCollection which returns null for missing named properties
        // (per HTMLFormControlsCollection spec behavior)
        var collection = new FormElementsCollection(form, bridge);
        for (int i = 0; i < controls.Count; i++)
            collection.FastAddValue((uint)i, ToJSObject(controls[i]),
                JSPropertyAttributes.EnumerableConfigurableValue);

        collection.FastAddProperty(
            (KeyString)"length",
            new JSFunction((in Arguments _) =>
            {
                var currentControls = CollectFormControls(form);
                return new JSNumber(currentControls.Count);
            }, "get length"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        return collection;
    }

    /// <summary>
    /// Collects all descendants of <paramref name="root"/> in document order
    /// (depth-first pre-order).
    /// </summary>
    private static void CollectDescendants(DomElement root, List<DomElement> result)
    {
        foreach (var child in root.Children)
        {
            // Skip sub-document roots — they are separate document trees
            // and must not be traversed as part of the parent document.
            if (string.Equals(child.TagName, "#subdoc-root", StringComparison.Ordinal))
                continue;
            result.Add(child);
            CollectDescendants(child, result);
        }
    }

    /// <summary>
    /// Collects all descendants of <paramref name="parent"/> into <paramref name="result"/>
    /// in depth-first pre-order (without skipping sub-document roots).
    /// Used by <c>document.write()</c> to register parsed elements.
    /// </summary>
    private static void CollectAllDescendantsFlat(DomElement parent, List<DomElement> result)
    {
        foreach (var child in parent.Children)
        {
            result.Add(child);
            CollectAllDescendantsFlat(child, result);
        }
    }

    /// <summary>
    /// Collects descendant elements matching a tag name in tree order (depth-first).
    /// </summary>
    private static void CollectDescendantsByTag(DomElement root, string tagName, List<JSValue> results, DomBridge bridge)
    {
        foreach (var child in root.Children)
        {
            if (string.Equals(child.TagName, tagName, StringComparison.OrdinalIgnoreCase))
                results.Add(bridge.ToJSObject(child));
            CollectDescendantsByTag(child, tagName, results, bridge);
        }
    }

    /// <summary>
    /// Returns a flat list of all nodes in the subtree rooted at
    /// <paramref name="root"/> in document order (including the root).
    /// </summary>
    private static List<DomElement> GetDocumentOrderNodes(DomElement root)
    {
        var list = new List<DomElement> { root };
        CollectDescendants(root, list);
        return list;
    }

    /// <summary>
    /// Returns the node type constant for a <see cref="DomElement"/>.
    /// </summary>
    private static int GetNodeType(DomElement element)
    {
        if (element.IsTextNode) return 3; // TEXT_NODE
        if (string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase)) return 8;
        if (string.Equals(element.TagName, "#document", StringComparison.OrdinalIgnoreCase)) return 9;
        if (string.Equals(element.TagName, "#document-fragment", StringComparison.OrdinalIgnoreCase)) return 11;
        return 1; // ELEMENT_NODE
    }

    /// <summary>
    /// Builds a computed style object for <c>getComputedStyle()</c>.
    /// Collects CSS rules from &lt;style&gt; elements, matches selectors
    /// against the element, and returns computed values.
    /// </summary>

    private static JSObject BuildStyleObject(DomElement element)
    {
        var style = new JSObject();

        // style.cssText (getter / setter)
        style.FastAddProperty(
            (KeyString)"cssText",
            new JSFunction((in Arguments a) =>
            {
                var parts = element.Style.Select(kv => $"{kv.Key}: {kv.Value}");
                var text = string.Join("; ", parts);
                return new JSString(text.Length > 0 ? text + ";" : text);
            }, "get cssText"),
            new JSFunction((in Arguments a) =>
            {
                element.Style.Clear();
                if (a.Length > 0)
                {
                    foreach (var kv in ParseStyle(a[0].ToString()))
                        element.Style[kv.Key] = kv.Value;
                }
                return JSUndefined.Value;
            }, "set cssText"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // style.setProperty(property, value)
        style.FastAddValue(
            (KeyString)"setProperty",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length >= 2)
                    element.Style[a[0].ToString()] = a[1].ToString();
                return JSUndefined.Value;
            }, "setProperty", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // style.getPropertyValue(property) — checks element.Style dict first, then
        // tries camelCase conversion for kebab-case input (or vice versa),
        // and also checks JSObject properties (set via el.style.camelCase = value).
        style.FastAddValue(
            (KeyString)"getPropertyValue",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var prop = a[0].ToString();
                    if (element.Style.TryGetValue(prop, out var val))
                        return new JSString(val);
                    // Try camelCase version of kebab-case input
                    var camel = ToCamelCaseStatic(prop);
                    if (camel != prop && element.Style.TryGetValue(camel, out val))
                        return new JSString(val);
                    // Try kebab-case version of camelCase input
                    var kebab = ToKebabCase(prop);
                    if (kebab != prop && element.Style.TryGetValue(kebab, out val))
                        return new JSString(val);
                    // Check JSObject properties (set via el.style.propertyName = value)
                    var jsVal = a.This?[(KeyString)camel];
                    if (jsVal != null && !jsVal.IsUndefined && !jsVal.IsNull)
                        return jsVal;
                    jsVal = a.This?[(KeyString)prop];
                    if (jsVal != null && !jsVal.IsUndefined && !jsVal.IsNull)
                        return jsVal;
                }
                return new JSString(string.Empty);
            }, "getPropertyValue", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // style.removeProperty(property)
        style.FastAddValue(
            (KeyString)"removeProperty",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var prop = a[0].ToString();
                    var removed = element.Style.TryGetValue(prop, out var val) ? val : string.Empty;
                    element.Style.Remove(prop);
                    return new JSString(removed);
                }
                return new JSString(string.Empty);
            }, "removeProperty", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // style.cssFloat (getter/setter) — maps to CSS "float" property
        style.FastAddProperty(
            (KeyString)"cssFloat",
            new JSFunction((in Arguments a) =>
            {
                if (element.Style.TryGetValue("float", out var val))
                    return new JSString(val);
                return new JSString(string.Empty);
            }, "get cssFloat"),
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                    element.Style["float"] = a[0].ToString();
                return JSUndefined.Value;
            }, "set cssFloat"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        return style;
    }

    /// <summary>
    /// Builds a <c>classList</c> object exposing <c>add</c>, <c>remove</c>,
    /// <c>toggle</c>, and <c>contains</c>.
    /// </summary>
    private static JSObject BuildClassListObject(DomElement element)
    {
        var classList = new JSObject();

        // classList.contains(className)
        classList.FastAddValue(
            (KeyString)"contains",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSBoolean.False;
                var cls = a[0].ToString();
                var classes = new HashSet<string>(
                    (element.ClassName ?? string.Empty).Split(' ').Where(s => s.Length > 0),
                    StringComparer.Ordinal);
                return classes.Contains(cls) ? JSBoolean.True : JSBoolean.False;
            }, "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // classList.add(...classNames)
        classList.FastAddValue(
            (KeyString)"add",
            new JSFunction((in Arguments a) =>
            {
                var classes = (element.ClassName ?? string.Empty)
                    .Split(' ').Where(s => s.Length > 0).ToList();
                var classSet = new HashSet<string>(classes, StringComparer.Ordinal);
                for (var i = 0; i < a.Length; i++)
                {
                    var cls = a[i].ToString();
                    if (!string.IsNullOrEmpty(cls) && classSet.Add(cls))
                        classes.Add(cls);
                }
                element.ClassName = string.Join(" ", classes);
                return JSUndefined.Value;
            }, "add"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // classList.remove(...classNames)
        classList.FastAddValue(
            (KeyString)"remove",
            new JSFunction((in Arguments a) =>
            {
                var toRemove = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < a.Length; i++)
                    toRemove.Add(a[i].ToString());
                var classes = (element.ClassName ?? string.Empty)
                    .Split(' ').Where(s => s.Length > 0 && !toRemove.Contains(s)).ToList();
                element.ClassName = string.Join(" ", classes);
                return JSUndefined.Value;
            }, "remove"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // classList.toggle(className[, force])
        classList.FastAddValue(
            (KeyString)"toggle",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSBoolean.False;
                var cls = a[0].ToString();
                var classes = (element.ClassName ?? string.Empty)
                    .Split(' ').Where(s => s.Length > 0).ToList();
                var classSet = new HashSet<string>(classes, StringComparer.Ordinal);

                bool shouldAdd = a.Length >= 2 && a[1] is not JSUndefined
                    ? a[1].BooleanValue
                    : !classSet.Contains(cls);

                if (shouldAdd)
                {
                    if (classSet.Add(cls)) classes.Add(cls);
                    element.ClassName = string.Join(" ", classes);
                    return JSBoolean.True;
                }
                else
                {
                    classes.Remove(cls);
                    element.ClassName = string.Join(" ", classes);
                    return JSBoolean.False;
                }
            }, "toggle", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return classList;
    }

    /// <summary>
    /// Builds an in-memory <c>localStorage</c> stub exposing <c>getItem</c>,
    /// <c>setItem</c>, <c>removeItem</c>, and <c>clear</c>.
    /// Bracket-notation access (e.g. <c>localStorage["key"]</c>) naturally
    /// falls through to JSObject property lookup.
    /// </summary>
    private static JSObject BuildLocalStorageObject()
    {
        var storage = new JSObject();
        var store = new Dictionary<string, string>();

        // localStorage.getItem(key)
        storage.FastAddValue(
            (KeyString)"getItem",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var key = a[0].ToString();
                return store.TryGetValue(key, out var val) ? new JSString(val) : JSNull.Value;
            }, "getItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // localStorage.setItem(key, value)
        storage.FastAddValue(
            (KeyString)"setItem",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length >= 2)
                {
                    var key = a[0].ToString();
                    var val = a[1].ToString();
                    store[key] = val;
                    storage[(KeyString)key] = new JSString(val);
                }
                return JSUndefined.Value;
            }, "setItem", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // localStorage.removeItem(key)
        storage.FastAddValue(
            (KeyString)"removeItem",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var key = a[0].ToString();
                    store.Remove(key);
                    storage.Delete((KeyString)key);
                }
                return JSUndefined.Value;
            }, "removeItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // localStorage.clear()
        storage.FastAddValue(
            (KeyString)"clear",
            new JSFunction((in Arguments a) =>
            {
                foreach (var key in store.Keys.ToList())
                    storage.Delete((KeyString)key);
                store.Clear();
                return JSUndefined.Value;
            }, "clear", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return storage;
    }

#if !BROILER_CLI
    /// <summary>
    /// Builds a minimal Canvas 2D rendering context exposing basic drawing
    /// operations as defined in the HTML Canvas 2D Context specification.
    /// Drawing commands are recorded but not rasterised in the current implementation.
    /// </summary>
    private static JSObject BuildCanvas2DContext(DomElement canvas)
    {
        var ctx = new JSObject();
        int width = 300, height = 150;
        if (canvas.Attributes.TryGetValue("width", out var w) && int.TryParse(w, out var pw)) width = pw;
        if (canvas.Attributes.TryGetValue("height", out var h) && int.TryParse(h, out var ph)) height = ph;

        var context2d = new CanvasRenderingContext2D(width, height);

        // fillStyle (get/set)
        ctx.FastAddProperty(
            (KeyString)"fillStyle",
            new JSFunction((in Arguments _) => new JSString(context2d.FillStyle), "get fillStyle"),
            new JSFunction((in Arguments a) => { if (a.Length > 0) context2d.FillStyle = a[0].ToString(); return JSUndefined.Value; }, "set fillStyle"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // strokeStyle (get/set)
        ctx.FastAddProperty(
            (KeyString)"strokeStyle",
            new JSFunction((in Arguments _) => new JSString(context2d.StrokeStyle), "get strokeStyle"),
            new JSFunction((in Arguments a) => { if (a.Length > 0) context2d.StrokeStyle = a[0].ToString(); return JSUndefined.Value; }, "set strokeStyle"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lineWidth (get/set)
        ctx.FastAddProperty(
            (KeyString)"lineWidth",
            new JSFunction((in Arguments _) => new JSNumber(context2d.LineWidth), "get lineWidth"),
            new JSFunction((in Arguments a) => { if (a.Length > 0 && a[0] is JSNumber n) context2d.LineWidth = (float)n.DoubleValue; return JSUndefined.Value; }, "set lineWidth"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // font (get/set)
        ctx.FastAddProperty(
            (KeyString)"font",
            new JSFunction((in Arguments _) => new JSString(context2d.Font), "get font"),
            new JSFunction((in Arguments a) => { if (a.Length > 0) context2d.Font = a[0].ToString(); return JSUndefined.Value; }, "set font"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // globalAlpha (get/set)
        ctx.FastAddProperty(
            (KeyString)"globalAlpha",
            new JSFunction((in Arguments _) => new JSNumber(context2d.GlobalAlpha), "get globalAlpha"),
            new JSFunction((in Arguments a) => { if (a.Length > 0 && a[0] is JSNumber n) context2d.GlobalAlpha = (float)n.DoubleValue; return JSUndefined.Value; }, "set globalAlpha"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // canvas property
        ctx.FastAddProperty(
            (KeyString)"canvas",
            new JSFunction((in Arguments _) => new JSObject(), "get canvas"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // Drawing methods
        ctx.FastAddValue((KeyString)"fillRect", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 4) context2d.FillRect((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue);
            return JSUndefined.Value;
        }, "fillRect", 4), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"strokeRect", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 4) context2d.StrokeRect((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue);
            return JSUndefined.Value;
        }, "strokeRect", 4), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"clearRect", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 4) context2d.ClearRect((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue);
            return JSUndefined.Value;
        }, "clearRect", 4), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"beginPath", new JSFunction((in Arguments _) =>
        { context2d.BeginPath(); return JSUndefined.Value; }, "beginPath", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"moveTo", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 2) context2d.MoveTo((float)a[0].DoubleValue, (float)a[1].DoubleValue);
            return JSUndefined.Value;
        }, "moveTo", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"lineTo", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 2) context2d.LineTo((float)a[0].DoubleValue, (float)a[1].DoubleValue);
            return JSUndefined.Value;
        }, "lineTo", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"arc", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 5) context2d.Arc((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue, (float)a[4].DoubleValue);
            return JSUndefined.Value;
        }, "arc", 5), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"closePath", new JSFunction((in Arguments _) =>
        { context2d.ClosePath(); return JSUndefined.Value; }, "closePath", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"fill", new JSFunction((in Arguments _) =>
        { context2d.Fill(); return JSUndefined.Value; }, "fill", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"stroke", new JSFunction((in Arguments _) =>
        { context2d.Stroke(); return JSUndefined.Value; }, "stroke", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"fillText", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 3) context2d.FillText(a[0].ToString(), (float)a[1].DoubleValue, (float)a[2].DoubleValue);
            return JSUndefined.Value;
        }, "fillText", 3), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"strokeText", new JSFunction((in Arguments a) =>
        {
            if (a.Length >= 3) context2d.StrokeText(a[0].ToString(), (float)a[1].DoubleValue, (float)a[2].DoubleValue);
            return JSUndefined.Value;
        }, "strokeText", 3), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"save", new JSFunction((in Arguments _) =>
        { context2d.Save(); return JSUndefined.Value; }, "save", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"restore", new JSFunction((in Arguments _) =>
        { context2d.Restore(); return JSUndefined.Value; }, "restore", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        // measureText(text) — returns { width: ... }
        ctx.FastAddValue((KeyString)"measureText", new JSFunction((in Arguments a) =>
        {
            var text = a.Length > 0 ? a[0].ToString() : string.Empty;
            var result = new JSObject();
            result.FastAddValue((KeyString)"width", new JSNumber(text.Length * 8.0), JSPropertyAttributes.EnumerableConfigurableValue);
            return result;
        }, "measureText", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        return ctx;
    }
#endif

    // ------------------------------------------------------------------
    //  Element name validation
    // ------------------------------------------------------------------

    /// <summary>
    /// Regex for valid XML Name: must start with a letter or underscore,
    /// followed by letters, digits, hyphens, underscores, or dots.
    /// Colons are NOT allowed (use <see cref="ValidXmlQualifiedNamePattern"/> for qualified names).
    /// </summary>
    private static readonly Regex ValidXmlNamePattern = new(
        @"^[a-zA-Z_][\w.\-]*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Regex for valid XML QName: either a simple name or prefix:localName
    /// where both prefix and localName are valid XML names (no colons).
    /// </summary>
    private static readonly Regex ValidXmlQualifiedNamePattern = new(
        @"^[a-zA-Z_][\w.\-]*(?::[a-zA-Z_][\w.\-]*)?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Throws a proper <c>DOMException</c> with the given name/code via the JS-registered constructor.
    /// Constructs the DOMException object in C# and throws it as a <see cref="JSException"/>
    /// so that JS try/catch blocks can intercept it with full <c>.code</c>, <c>.name</c>,
    /// and <c>.message</c> properties intact.
    /// </summary>
    private static void ThrowDOMException(JSContext context, string message, string name)
    {
        if (context["DOMException"] is JSFunction domExCtor)
        {
            var exObj = domExCtor.CreateInstance(
                new Arguments(domExCtor, new JSString(message), new JSString(name)));
            throw new JSException(exObj);
        }

        // Fallback when DOMException constructor is unavailable
        throw new JSException(new JSString($"DOMException: {message} ({name})"));
    }

    /// <summary>
    /// Validates an element/doctype name per the XML spec.
    /// Throws a DOMException with INVALID_CHARACTER_ERR (code 5) for invalid names.
    /// </summary>
    private static void ValidateElementName(string name, JSContext context)
    {
        if (string.IsNullOrEmpty(name) || name.Contains('\0') || !ValidXmlNamePattern.IsMatch(name))
        {
            ThrowDOMException(context,
                $"Failed to execute 'createElement': The tag name provided ('{name}') is not a valid name.",
                "InvalidCharacterError");
        }
    }

    /// <summary>
    /// Validates a qualified name and namespace per the Namespaces in XML spec.
    /// Throws a DOMException with NAMESPACE_ERR (code 14) for namespace violations.
    /// </summary>
    private static void ValidateQualifiedName(string qualifiedName, string? ns, JSContext context)
    {
        // Check for empty prefix (e.g., ":div") first — this is a NamespaceError
        if (!string.IsNullOrEmpty(qualifiedName) && qualifiedName.StartsWith(':'))
        {
            ThrowDOMException(context,
                $"Failed to execute 'createElementNS': The qualified name provided ('{qualifiedName}') has an empty prefix.",
                "NamespaceError");
        }

        // Check for trailing colon (e.g., "a:") — empty local name is a NamespaceError
        if (!string.IsNullOrEmpty(qualifiedName) && qualifiedName.EndsWith(':'))
        {
            ThrowDOMException(context,
                $"Failed to execute 'createElementNS': The qualified name provided ('{qualifiedName}') has an empty local name.",
                "NamespaceError");
        }

        // Validate the name characters (allows optional single colon for prefix:localName)
        if (string.IsNullOrEmpty(qualifiedName) || !ValidXmlQualifiedNamePattern.IsMatch(qualifiedName))
        {
            ThrowDOMException(context,
                $"Failed to execute 'createElementNS': The qualified name provided ('{qualifiedName}') is not a valid name.",
                "InvalidCharacterError");
        }

        var colonIndex = qualifiedName.IndexOf(':');
        if (colonIndex >= 0)
        {
            // Prefixed name: namespace must not be null
            if (string.IsNullOrEmpty(ns))
            {
                ThrowDOMException(context,
                    $"Failed to execute 'createElementNS': The namespace URI provided is empty for qualified name '{qualifiedName}'.",
                    "NamespaceError");
            }

            var prefix = qualifiedName[..colonIndex];
            // "xml" prefix must be the XML namespace
            if (prefix == "xml" && ns != "http://www.w3.org/XML/1998/namespace")
            {
                ThrowDOMException(context,
                    $"Failed to execute 'createElementNS': The namespace URI for prefix 'xml' is invalid.",
                    "NamespaceError");
            }

            // "xmlns" prefix must be the XMLNS namespace
            if (prefix == "xmlns" && ns != "http://www.w3.org/2000/xmlns/")
            {
                ThrowDOMException(context,
                    $"Failed to execute 'createElementNS': The namespace URI for prefix 'xmlns' is invalid.",
                    "NamespaceError");
            }

            // Non-"xmlns" prefix must not use the XMLNS namespace
            if (prefix != "xmlns" && ns == "http://www.w3.org/2000/xmlns/")
            {
                ThrowDOMException(context,
                    $"Failed to execute 'createElementNS': The XMLNS namespace URI may only be used with prefix 'xmlns'.",
                    "NamespaceError");
            }
        }
    }

    /// <summary>
    /// Registers the <c>DOMException</c> constructor on the JS context.
    /// </summary>
    private static void RegisterDOMException(JSContext context)
    {
        context.Eval(@"
            function DOMException(message, name) {
                this.message = message || '';
                this.name = name || 'Error';
                // Map name to legacy code
                var codeMap = {
                    'IndexSizeError': 1,
                    'DOMStringSizeError': 2,
                    'HierarchyRequestError': 3,
                    'WrongDocumentError': 4,
                    'InvalidCharacterError': 5,
                    'NoDataAllowedError': 6,
                    'NoModificationAllowedError': 7,
                    'NotFoundError': 8,
                    'NotSupportedError': 9,
                    'InUseAttributeError': 10,
                    'InvalidStateError': 11,
                    'SyntaxError': 12,
                    'InvalidModificationError': 13,
                    'NamespaceError': 14,
                    'InvalidAccessError': 15,
                    'TypeMismatchError': 17,
                    'SecurityError': 18,
                    'NetworkError': 19,
                    'AbortError': 20,
                    'URLMismatchError': 21,
                    'QuotaExceededError': 22,
                    'TimeoutError': 23,
                    'InvalidNodeTypeError': 24,
                    'DataCloneError': 25
                };
                this.code = codeMap[this.name] || 0;
            }
            DOMException.INDEX_SIZE_ERR = 1;
            DOMException.DOMSTRING_SIZE_ERR = 2;
            DOMException.HIERARCHY_REQUEST_ERR = 3;
            DOMException.WRONG_DOCUMENT_ERR = 4;
            DOMException.INVALID_CHARACTER_ERR = 5;
            DOMException.NO_DATA_ALLOWED_ERR = 6;
            DOMException.NO_MODIFICATION_ALLOWED_ERR = 7;
            DOMException.NOT_FOUND_ERR = 8;
            DOMException.NOT_SUPPORTED_ERR = 9;
            DOMException.INUSE_ATTRIBUTE_ERR = 10;
            DOMException.INVALID_STATE_ERR = 11;
            DOMException.SYNTAX_ERR = 12;
            DOMException.INVALID_MODIFICATION_ERR = 13;
            DOMException.NAMESPACE_ERR = 14;
            DOMException.INVALID_ACCESS_ERR = 15;
            DOMException.TYPE_MISMATCH_ERR = 17;
            DOMException.SECURITY_ERR = 18;
            DOMException.NETWORK_ERR = 19;
            DOMException.ABORT_ERR = 20;
            DOMException.URL_MISMATCH_ERR = 21;
            DOMException.QUOTA_EXCEEDED_ERR = 22;
            DOMException.TIMEOUT_ERR = 23;
            DOMException.INVALID_NODE_TYPE_ERR = 24;
            DOMException.DATA_CLONE_ERR = 25;
            DOMException.prototype = Object.create(Error.prototype);
            DOMException.prototype.constructor = DOMException;
            DOMException.prototype.INDEX_SIZE_ERR = 1;
            DOMException.prototype.DOMSTRING_SIZE_ERR = 2;
            DOMException.prototype.HIERARCHY_REQUEST_ERR = 3;
            DOMException.prototype.WRONG_DOCUMENT_ERR = 4;
            DOMException.prototype.INVALID_CHARACTER_ERR = 5;
            DOMException.prototype.NO_DATA_ALLOWED_ERR = 6;
            DOMException.prototype.NO_MODIFICATION_ALLOWED_ERR = 7;
            DOMException.prototype.NOT_FOUND_ERR = 8;
            DOMException.prototype.NOT_SUPPORTED_ERR = 9;
            DOMException.prototype.INUSE_ATTRIBUTE_ERR = 10;
            DOMException.prototype.INVALID_STATE_ERR = 11;
            DOMException.prototype.SYNTAX_ERR = 12;
            DOMException.prototype.INVALID_MODIFICATION_ERR = 13;
            DOMException.prototype.NAMESPACE_ERR = 14;
            DOMException.prototype.INVALID_ACCESS_ERR = 15;
            DOMException.prototype.TYPE_MISMATCH_ERR = 17;
            DOMException.prototype.SECURITY_ERR = 18;
            DOMException.prototype.NETWORK_ERR = 19;
            DOMException.prototype.ABORT_ERR = 20;
            DOMException.prototype.URL_MISMATCH_ERR = 21;
            DOMException.prototype.QUOTA_EXCEEDED_ERR = 22;
            DOMException.prototype.TIMEOUT_ERR = 23;
            DOMException.prototype.INVALID_NODE_TYPE_ERR = 24;
            DOMException.prototype.DATA_CLONE_ERR = 25;
        ");
    }

    /// <summary>
    /// Registers the <c>Node</c> constructor with DOM type constants on the JS context.
    /// </summary>
    private static void RegisterNodeConstructor(JSContext context)
    {
        context.Eval(@"
            function Node() {}
            Node.ELEMENT_NODE = 1;
            Node.ATTRIBUTE_NODE = 2;
            Node.TEXT_NODE = 3;
            Node.CDATA_SECTION_NODE = 4;
            Node.ENTITY_REFERENCE_NODE = 5;
            Node.ENTITY_NODE = 6;
            Node.PROCESSING_INSTRUCTION_NODE = 7;
            Node.COMMENT_NODE = 8;
            Node.DOCUMENT_NODE = 9;
            Node.DOCUMENT_TYPE_NODE = 10;
            Node.DOCUMENT_FRAGMENT_NODE = 11;
            Node.NOTATION_NODE = 12;
            Node.prototype.ELEMENT_NODE = 1;
            Node.prototype.ATTRIBUTE_NODE = 2;
            Node.prototype.TEXT_NODE = 3;
            Node.prototype.CDATA_SECTION_NODE = 4;
            Node.prototype.ENTITY_REFERENCE_NODE = 5;
            Node.prototype.ENTITY_NODE = 6;
            Node.prototype.PROCESSING_INSTRUCTION_NODE = 7;
            Node.prototype.COMMENT_NODE = 8;
            Node.prototype.DOCUMENT_NODE = 9;
            Node.prototype.DOCUMENT_TYPE_NODE = 10;
            Node.prototype.DOCUMENT_FRAGMENT_NODE = 11;
            Node.prototype.NOTATION_NODE = 12;
        ");
    }

    private static void RegisterSVGLength(JSContext context)
    {
        context.Eval(@"
            function SVGLength() {}
            SVGLength.SVG_LENGTHTYPE_UNKNOWN = 0;
            SVGLength.SVG_LENGTHTYPE_NUMBER = 1;
            SVGLength.SVG_LENGTHTYPE_PERCENTAGE = 2;
            SVGLength.SVG_LENGTHTYPE_EMS = 3;
            SVGLength.SVG_LENGTHTYPE_EXS = 4;
            SVGLength.SVG_LENGTHTYPE_PX = 5;
            SVGLength.SVG_LENGTHTYPE_CM = 6;
            SVGLength.SVG_LENGTHTYPE_MM = 7;
            SVGLength.SVG_LENGTHTYPE_IN = 8;
            SVGLength.SVG_LENGTHTYPE_PT = 9;
            SVGLength.SVG_LENGTHTYPE_PC = 10;
        ");
    }
}
