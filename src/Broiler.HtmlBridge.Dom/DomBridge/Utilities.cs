using Broiler.JavaScript.BuiltIns.Null;
using System.Text;
using System.Text.RegularExpressions;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

/// <summary>
/// Internal helper methods — string conversions, DOM tree utilities,
/// form-control collection, table-row helpers, and JS-object builders
/// for <c>style</c>, <c>classList</c>, <c>localStorage</c>, and
/// <c>canvas.getContext("2d")</c>.
/// </summary>
public sealed partial class DomBridge
{
    private static readonly System.Text.RegularExpressions.Regex ImportantSuffixPattern = new(@"\s*!\s*important\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly HashSet<string> CssStyleDeclarationNonCssNames = new(StringComparer.Ordinal)
    {
        "setProperty", "getPropertyValue", "removeProperty",
        "cssText", "cssFloat", "length", "parentRule",
        "item", "getPropertyPriority",
    };

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
        GetElementRuntimeState(doctype).DocumentType.Name.Set(name);
        GetElementRuntimeState(doctype).DocumentType.PublicId.Set(publicId);
        GetElementRuntimeState(doctype).DocumentType.SystemId.Set(systemId);
        GetElementRuntimeState(doctype).DocumentType.InternalSubset.Set(null);

        return doctype;
    }

    /// <summary>Gets the lowercase name from a DOCTYPE DomElement.</summary>
    private static string GetDocTypeName(DomElement element)
    {
        var dtName = GetElementRuntimeState(element).DocumentType.Name.TryGet(out var n) ? n?.ToString() ?? "html" : "html";
        return dtName.ToLowerInvariant();
    }

    /// <summary>
    /// Searches descendants of an element using a CSS selector.
    /// </summary>
    private static JSValue FindInDescendants(DomElement root, string selector, bool all, DomBridge bridge)
    {
        var results = new List<JSValue>();
        if (selector.IndexOf(":scope", StringComparison.Ordinal) >= 0 &&
            MatchesSelector(root, selector, root))
        {
            results.Add(bridge.ToJSObject(root));
            if (!all)
                return results[0];
        }

        SearchDescendants(root, selector, results, bridge, all, root);
        if (all) return new JSArray(results);
        return results.Count > 0 ? results[0] : JSNull.Value;
    }

    private static void SearchDescendants(DomElement parent, string selector, List<JSValue> results, DomBridge bridge, bool all, DomElement scope)
    {
        foreach (var child in ChildElements(parent))
        {
            if (!IsText(child) && MatchesSelector(child, selector, scope))
            {
                results.Add(bridge.ToJSObject(child));
                if (!all) return;
            }
            SearchDescendants(child, selector, results, bridge, all, scope);
            if (!all && results.Count > 0) return;
        }
    }

    /// <summary>
    /// Recursively collects text content from a node and its descendants.
    /// </summary>
    private static void CollectTextContent(DomElement node, StringBuilder sb)
    {
        if (IsText(node))
        {
            sb.Append(node.TextContent ?? string.Empty);
            return;
        }
        foreach (var child in ChildElements(node))
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

        var ext = Path.GetExtension(path).ToLowerInvariant();
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

        var ext = Path.GetExtension(path).ToLowerInvariant();
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
        targetUrl = NormalizeWptPlaceholderUrl(targetUrl);
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

    private static string NormalizeWptPlaceholderUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        return url
            .Replace("{{hosts[alt][]}}", "www1.web-platform.test", StringComparison.Ordinal)
            .Replace("{{hosts[www][]}}", "www.web-platform.test", StringComparison.Ordinal)
            .Replace("{{hosts[][]}}", "web-platform.test", StringComparison.Ordinal)
            .Replace("{{ports[http][0]}}", "8000", StringComparison.Ordinal)
            .Replace("{{ports[https][0]}}", "8443", StringComparison.Ordinal);
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

    private static bool IsSubDocRoot(DomElement element) =>
        string.Equals(element.TagName, "#subdoc-root", StringComparison.Ordinal);

    /// <summary>
    /// Returns <c>true</c> if <paramref name="candidate"/> is a descendant of
    /// <paramref name="ancestor"/> in the DOM tree.
    /// </summary>
    private static bool IsDescendant(DomElement ancestor, DomElement candidate)
    {
        var current = ParentEl(candidate);
        while (current != null)
        {
            if (ReferenceEquals(current, ancestor)) return true;
            current = ParentEl(current);
        }
        return false;
    }

    /// <summary>
    /// Compares two nodes in document tree order.
    /// Returns -1 when <paramref name="first"/> precedes <paramref name="second"/>,
    /// 1 when it follows, and 0 when no ordering can be determined.
    /// </summary>
    private static int CompareTreeOrder(DomElement first, DomElement second)
    {
        if (ReferenceEquals(first, second))
            return 0;

        var firstAncestors = new List<DomElement>();
        for (var current = first; current != null; current = ParentEl(current))
            firstAncestors.Add(current);
        firstAncestors.Reverse();

        var secondAncestors = new List<DomElement>();
        for (var current = second; current != null; current = ParentEl(current))
            secondAncestors.Add(current);
        secondAncestors.Reverse();

        var divergenceIndex = 0;
        var sharedLength = Math.Min(firstAncestors.Count, secondAncestors.Count);
        while (divergenceIndex < sharedLength &&
               ReferenceEquals(firstAncestors[divergenceIndex], secondAncestors[divergenceIndex]))
        {
            divergenceIndex++;
        }

        if (divergenceIndex == 0 ||
            divergenceIndex >= firstAncestors.Count ||
            divergenceIndex >= secondAncestors.Count)
        {
            return 0;
        }

        var commonAncestor = firstAncestors[divergenceIndex - 1];
        var firstChild = firstAncestors[divergenceIndex];
        var secondChild = secondAncestors[divergenceIndex];
        var firstIndex = ChildIndexOf(commonAncestor, firstChild);
        var secondIndex = ChildIndexOf(commonAncestor, secondChild);

        if (firstIndex == secondIndex)
            return 0;

        return firstIndex < secondIndex ? -1 : 1;
    }

    /// <summary>
    /// Updates the owning document root for <paramref name="node"/> and its
    /// descendants when the subtree is inserted into another document.
    /// Nested sub-document roots remain isolated browsing contexts and are not
    /// re-owned by the outer document.
    /// </summary>
    private static void AdoptSubtreeIntoDocument(DomElement node, DomElement? ownerDocRoot)
    {
        GetElementRuntimeState(node).OwnerDocRoot = ownerDocRoot;

        foreach (var child in ChildElements(node))
        {
            if (IsSubDocRoot(child))
                continue;

            AdoptSubtreeIntoDocument(child, ownerDocRoot);
        }
    }

    /// <summary>
    /// Clones a <see cref="DomElement"/>. When <paramref name="deep"/> is true,
    /// all descendants are recursively cloned.
    /// </summary>
    private DomElement CloneDomElement(DomElement source, bool deep)
    {
        var clone = new DomElement(source.TagName, source.Id, source.ClassName, string.Empty, null, null, IsText(source));
        // RF-BRIDGE-1c Phase F: raw inner-HTML lives in ElementRuntimeState now; copy it across
        // the clone (matching the prior facade behaviour of seeding InnerHtml at construction).
        GetElementRuntimeState(clone).InnerHtml = GetElementRuntimeState(source).InnerHtml;
        // RF-BRIDGE-1c Phase C2: copy attributes straight from the canonical namespace-keyed
        // set so namespaced attributes (namespace, prefix, local name) survive the clone —
        // that fidelity used to depend on the separate NsAttrMap shadow. No-namespace
        // attributes go through SetAttribute (which lowercases, matching the prior snapshot
        // path); a prefixed qualified name can only exist with a namespace, so it never
        // reaches the SetAttribute branch (which would throw on the ':').
        foreach (var attribute in source.Attributes.Values)
        {
            if (attribute.NamespaceUri is null)
                clone.SetAttribute(attribute.QualifiedName, attribute.Value);
            else
                clone.SetAttributeNS(attribute.NamespaceUri, attribute.QualifiedName, attribute.Value);
        }
        // RF-BRIDGE-1c Phase B: inline style lives in ElementRuntimeState now. Copy the
        // source's live style dict (which may hold JS mutations not yet synced to the
        // `style=` attribute), replacing the clone's lazily-seeded attribute values.
        var cloneStyle = InlineStyle(clone);
        cloneStyle.Clear();
        foreach (var kv in InlineStyle(source))
            cloneStyle[kv.Key] = kv.Value;
        clone.TextContent = source.TextContent;
        clone.NamespaceURI = source.NamespaceURI;
        // Copy browser-runtime values (e.g., checked state for inputs).
        GetElementRuntimeState(source).CopyRuntimeValuesTo(GetElementRuntimeState(clone));
        // Carry the memoized position-area resolution too (was ElementRuntimeState.Layout,
        // now the bridge-level PositionAreaResolutions cache — see PositionAreaQueries.cs).
        CopyPositionAreaResolution(source, clone);

        if (deep)
        {
            foreach (var child in ChildElements(source))
            {
                var childClone = CloneDomElement(child, true);
                SetParent(childClone, clone);
                clone.AppendChild(childClone);
                _knownNodes.Add(childClone);
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
        foreach (var child in ChildElements(table))
        {
            if (string.Equals(child.TagName, "thead", StringComparison.OrdinalIgnoreCase))
                foreach (var c in ChildElements(child))
                    if (string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase))
                        rows.Add(c);
        }
        // 2. All tr children that are direct children of table, or children of tbody elements (in tree order)
        foreach (var child in ChildElements(table))
        {
            var ctag = child.TagName.ToLowerInvariant();
            if (ctag == "tr")
                rows.Add(child);
            else if (ctag == "tbody")
                foreach (var c in ChildElements(child))
                    if (string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase))
                        rows.Add(c);
        }
        // 3. All tr children of tfoot elements (in tree order)
        foreach (var child in ChildElements(table))
        {
            if (string.Equals(child.TagName, "tfoot", StringComparison.OrdinalIgnoreCase))
                foreach (var c in ChildElements(child))
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
        bridge._knownNodes.Add(tr);

        var allRows = CollectTableRows(table);
        if (allRows.Count == 0 || index == -1 || index == allRows.Count)
        {
            // Find the last section to append to, or create a tbody
            DomElement? lastSection = null;
            for (int i = table.ChildNodes.Count - 1; i >= 0; i--)
            {
                var ctag = ChildAt(table, i).TagName.ToLowerInvariant();
                if (ctag == "thead" || ctag == "tbody" || ctag == "tfoot")
                {
                    lastSection = ChildAt(table, i);
                    break;
                }
            }
            if (lastSection == null && allRows.Count == 0)
            {
                // No sections and no rows at all: create a new tbody per spec
                var tbody = new DomElement("tbody", null, null, string.Empty);
                bridge._knownNodes.Add(tbody);
                SetParent(tbody, table);
                table.AppendChild(tbody);
                lastSection = tbody;
            }
            if (lastSection != null)
            {
                SetParent(tr, lastSection);
                lastSection.AppendChild(tr);
            }
            else
            {
                SetParent(tr, table);
                table.AppendChild(tr);
            }
        }
        else if (index >= 0 && index < allRows.Count)
        {
            var refRow = allRows[index];
            var parent = ParentEl(refRow) ?? table;
            SetParent(tr, parent);
            var idx = ChildIndexOf(parent, refRow);
            InsertChildAt(parent, idx >= 0 ? idx : parent.ChildNodes.Count, tr);
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
        foreach (var child in ChildElements(parent))
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
        foreach (var child in ChildElements(scope))
        {
            if (!IsText(child) && !ReferenceEquals(child, except))
            {
                if (string.Equals(child.TagName, "input", StringComparison.OrdinalIgnoreCase) &&
                    TryGetAttribute(child, "type", out var st) &&
                    string.Equals(st, "radio", StringComparison.OrdinalIgnoreCase) &&
                    TryGetAttribute(child, "name", out var sn) &&
                    string.Equals(sn, radioName, StringComparison.Ordinal))
                {
                    GetElementRuntimeState(child).FormControl.Checked.Set(false);
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
            new JSFunction((in Arguments _) => JsUtilitiesGetLength002Core(form, in _), "get length"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        return collection;
    }

    /// <summary>
    /// Collects all descendants of <paramref name="root"/> in document order
    /// (depth-first pre-order).
    /// </summary>
    private static void CollectDescendants(DomElement root, List<DomElement> result)
    {
        foreach (var child in ChildElements(root))
        {
            // Skip sub-document roots — they are separate document trees
            // and must not be traversed as part of the parent document.
            if (IsSubDocRoot(child))
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
        foreach (var child in ChildElements(parent))
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
        foreach (var child in ChildElements(root))
        {
            if (tagName == "*" || string.Equals(child.TagName, tagName, StringComparison.OrdinalIgnoreCase))
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
        if (IsText(element)) return 3; // TEXT_NODE
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

    /// <summary>
    /// A JSObject subclass that intercepts property get/set to sync
    /// JavaScript camelCase style property assignments (e.g.
    /// <c>style.animationDelay = '-100s'</c>) with the DOM element's
    /// inline style (<see cref="ElementRuntimeState.Style"/>, reached via
    /// <c>InlineStyle</c>) in CSS kebab-case (<c>animation-delay: -100s</c>).
    /// </summary>
    private sealed class CssStyleDeclaration : JSObject
    {
        private readonly DomElement _element;
        private readonly Action? _onMutation;

        // Names that are JS methods / special properties, not CSS properties.
        private static HashSet<string> NonCssNames => CssStyleDeclarationNonCssNames;

        public CssStyleDeclaration(DomElement element, Action? onMutation = null)
        {
            _element = element;
            _onMutation = onMutation;
        }

        protected override bool SetValue(
            KeyString name, JSValue value, JSValue receiver, bool throwError = true)
        {
            var nameStr = name.ToString();
            if (!NonCssNames.Contains(nameStr))
            {
                var kebab = ToKebabCase(nameStr);
                var val = value?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(val))
                {
                    InlineStyle(_element).Remove(kebab);
                    GetElementRuntimeState(_element).JsSetStyleProps.Remove(kebab);
                }
                else
                {
                    InlineStyle(_element)[kebab] = val;
                    GetElementRuntimeState(_element).JsSetStyleProps.Add(kebab);
                }

                // Invalidate cached position-area resolution when relevant
                // properties change so offset queries recompute.
                if (kebab is "position-area" or "position-anchor")
                    ClearPositionAreaResolution(_element);

                _onMutation?.Invoke();
            }

            return base.SetValue(name, value, receiver, throwError);
        }

        protected override JSValue GetValue(
            KeyString key, JSValue receiver, bool throwError = true)
        {
            // Try normal lookup first (methods, explicit properties, etc.)
            var result = base.GetValue(key, receiver, false);
            if (result != null && !result.IsUndefined)
                return result;

            // Fall back to InlineStyle(element) lookup (kebab-case)
            var nameStr = key.ToString();
            if (!NonCssNames.Contains(nameStr))
            {
                if (TryGetStylePropertyRawValue(_element, nameStr, out var val))
                    return new JSString(StripCssPriority(val));
            }

            return new JSString(string.Empty);
        }
    }

    private sealed class CssRuleStyleDeclaration : JSObject
    {
        private readonly Dictionary<string, string> _style;

        public CssRuleStyleDeclaration(Dictionary<string, string> style) => _style = style;

        protected override bool SetValue(
            KeyString name, JSValue value, JSValue receiver, bool throwError = true)
        {
            var nameStr = name.ToString();
            if (!CssStyleDeclarationNonCssNames.Contains(nameStr))
            {
                var kebab = ToKebabCase(nameStr);
                var val = value?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(val))
                    _style.Remove(kebab);
                else
                    _style[kebab] = val;
            }

            return base.SetValue(name, value, receiver, throwError);
        }

        protected override JSValue GetValue(
            KeyString key, JSValue receiver, bool throwError = true)
        {
            var result = base.GetValue(key, receiver, false);
            if (result != null && !result.IsUndefined)
                return result;

            var nameStr = key.ToString();
            if (!CssStyleDeclarationNonCssNames.Contains(nameStr) &&
                TryGetStylePropertyRawValue(_style, nameStr, out var val))
            {
                return new JSString(StripCssPriority(val));
            }

            return new JSString(string.Empty);
        }
    }

    private static string StripCssPriority(string? value)
        => string.IsNullOrEmpty(value) ? string.Empty : ImportantSuffixPattern.Replace(value, string.Empty).Trim();

    private static string GetCssPriority(string? value)
        => !string.IsNullOrEmpty(value) && ImportantSuffixPattern.IsMatch(value) ? "important" : string.Empty;

    private static string ApplyCssPriority(string value, string priority)
        => string.Equals(priority?.Trim(), "important", StringComparison.OrdinalIgnoreCase)
            ? $"{StripCssPriority(value)} !important".Trim()
            : StripCssPriority(value);

    private static List<string> GetStylePropertyNames(IReadOnlyDictionary<string, string> style)
        => style.Keys.ToList();

    private static List<string> GetStylePropertyNames(DomElement element)
        => GetStylePropertyNames((IReadOnlyDictionary<string, string>)InlineStyle(element));

    private static Dictionary<string, string> BuildDeclaredInlineStyleMap(DomElement element)
    {
        var declared = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (TryGetAttribute(element, "style", out var inlineStyle) &&
            !string.IsNullOrEmpty(inlineStyle))
        {
            foreach (var kv in ParseStyle(inlineStyle))
                declared[kv.Key] = kv.Value;
        }

        foreach (var property in GetElementRuntimeState(element).JsSetStyleProps)
        {
            if (InlineStyle(element).TryGetValue(property, out var value))
                declared[property] = value;
        }

        return declared;
    }

    private static bool TryGetExpandedInlineStyleRawValue(DomElement element, string property, out string value)
    {
        var declared = BuildDeclaredInlineStyleMap(element);
        if (declared.Count == 0)
        {
            value = string.Empty;
            return false;
        }

        ExpandCssShorthands(declared);

        if (declared.TryGetValue(property, out value!))
            return true;

        var camel = ToCamelCaseStatic(property);
        if (camel != property && declared.TryGetValue(camel, out value!))
            return true;

        var kebab = ToKebabCase(property);
        if (kebab != property && declared.TryGetValue(kebab, out value!))
            return true;

        value = string.Empty;
        return false;
    }

    private static bool TryGetStylePropertyRawValue(IReadOnlyDictionary<string, string> style, string property, out string value)
    {
        if (style.TryGetValue(property, out value!))
            return true;

        var camel = ToCamelCaseStatic(property);
        if (camel != property && style.TryGetValue(camel, out value!))
            return true;

        var kebab = ToKebabCase(property);
        if (kebab != property && style.TryGetValue(kebab, out value!))
            return true;

        value = string.Empty;
        return false;
    }

    private static bool TryGetStylePropertyRawValue(DomElement element, string property, out string value)
    {
        if (TryGetStylePropertyRawValue((IReadOnlyDictionary<string, string>)InlineStyle(element), property, out value!))
            return true;

        return TryGetExpandedInlineStyleRawValue(element, property, out value!);
    }

    private static JSObject BuildStyleObject(DomElement element, Action? onMutation = null, JSValue? parentRule = null)
    {
        var style = new CssStyleDeclaration(element, onMutation);

        // style.cssText (getter / setter)
        style.FastAddProperty(
            (KeyString)"cssText",
            new JSFunction((in Arguments a) => JsUtilitiesGetCssText003Core(element, in a), "get cssText"),
            new JSFunction((in Arguments a) => JsUtilitiesSetCssText004Core(element, onMutation, in a), "set cssText"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // style.setProperty(property, value)
        style.FastAddValue(
            (KeyString)"setProperty",
            new JSFunction((in Arguments a) => JsUtilitiesSetProperty005Core(element, onMutation, in a), "setProperty", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // style.getPropertyValue(property) — checks InlineStyle(element) dict first, then
        // tries camelCase conversion for kebab-case input (or vice versa),
        // and also checks JSObject properties (set via el.style.camelCase = value).
        style.FastAddValue(
            (KeyString)"getPropertyValue",
            new JSFunction((in Arguments a) => JsUtilitiesGetPropertyValue006Core(element, in a), "getPropertyValue", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // style.removeProperty(property)
        style.FastAddValue(
            (KeyString)"removeProperty",
            new JSFunction((in Arguments a) => JsUtilitiesRemoveProperty007Core(element, onMutation, in a), "removeProperty", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // style.cssFloat (getter/setter) — maps to CSS "float" property
        style.FastAddProperty(
            (KeyString)"cssFloat",
            new JSFunction((in Arguments a) => JsUtilitiesGetCssFloat008Core(element, in a), "get cssFloat"),
            new JSFunction((in Arguments a) => JsUtilitiesSetCssFloat009Core(element, onMutation, in a), "set cssFloat"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // style.length (read-only)
        style.FastAddProperty(
            (KeyString)"length",
            new JSFunction((in Arguments _) => new JSNumber(GetStylePropertyNames(element).Count), "get length"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // style.item(index)
        style.FastAddValue(
            (KeyString)"item",
            new JSFunction((in Arguments a) => JsUtilitiesItem011Core(element, in a), "item", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // style.getPropertyPriority(property)
        style.FastAddValue(
            (KeyString)"getPropertyPriority",
            new JSFunction((in Arguments a) => JsUtilitiesGetPropertyPriority012Core(element, in a), "getPropertyPriority", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        style.FastAddProperty(
            (KeyString)"parentRule",
            new JSFunction((in Arguments _) => parentRule ?? JSNull.Value, "get parentRule"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        return style;
    }

    private static JSObject BuildStyleObject(Dictionary<string, string> styleMap, JSValue? parentRule = null)
    {
        var style = new CssRuleStyleDeclaration(styleMap);

        style.FastAddProperty(
            (KeyString)"cssText",
            new JSFunction((in Arguments _) => JsUtilitiesGetCssText014Core(styleMap, in _), "get cssText"),
            new JSFunction((in Arguments a) => JsUtilitiesSetCssText015Core(styleMap, in a), "set cssText"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        style.FastAddValue(
            (KeyString)"setProperty",
            new JSFunction((in Arguments a) => JsUtilitiesSetProperty016Core(styleMap, in a), "setProperty", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        style.FastAddValue(
            (KeyString)"getPropertyValue",
            new JSFunction((in Arguments a) => JsUtilitiesGetPropertyValue017Core(styleMap, in a), "getPropertyValue", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        style.FastAddValue(
            (KeyString)"removeProperty",
            new JSFunction((in Arguments a) => JsUtilitiesRemoveProperty018Core(styleMap, in a), "removeProperty", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        style.FastAddProperty(
            (KeyString)"cssFloat",
            new JSFunction((in Arguments _) => JsUtilitiesGetCssFloat019Core(styleMap, in _), "get cssFloat"),
            new JSFunction((in Arguments a) => JsUtilitiesSetCssFloat020Core(styleMap, in a), "set cssFloat"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        style.FastAddProperty(
            (KeyString)"length",
            new JSFunction((in Arguments _) => new JSNumber(GetStylePropertyNames(styleMap).Count), "get length"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        style.FastAddValue(
            (KeyString)"item",
            new JSFunction((in Arguments a) => JsUtilitiesItem022Core(styleMap, in a), "item", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        style.FastAddValue(
            (KeyString)"getPropertyPriority",
            new JSFunction((in Arguments a) => JsUtilitiesGetPropertyPriority023Core(styleMap, in a), "getPropertyPriority", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        style.FastAddProperty(
            (KeyString)"parentRule",
            new JSFunction((in Arguments _) => parentRule ?? JSNull.Value, "get parentRule"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        return style;
    }

    /// <summary>
    /// Builds a <c>classList</c> object exposing <c>add</c>, <c>remove</c>,
    /// <c>toggle</c>, and <c>contains</c>.
    /// </summary>
    private static JSObject BuildClassListObject(DomElement element, Action<DomElement>? onClassChanged = null)
    {
        var classList = new JSObject();

        // classList.contains(className)
        classList.FastAddValue(
            (KeyString)"contains",
            new JSFunction((in Arguments a) => JsUtilitiesContains025Core(element, in a), "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // classList.add(...classNames)
        classList.FastAddValue(
            (KeyString)"add",
            new JSFunction((in Arguments a) => JsUtilitiesAdd026Core(element, onClassChanged, in a), "add"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // classList.remove(...classNames)
        classList.FastAddValue(
            (KeyString)"remove",
            new JSFunction((in Arguments a) => JsUtilitiesRemove027Core(element, onClassChanged, in a), "remove"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // classList.toggle(className[, force])
        classList.FastAddValue(
            (KeyString)"toggle",
            new JSFunction((in Arguments a) => JsUtilitiesToggle028Core(element, onClassChanged, in a), "toggle", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // classList.replace(oldToken, newToken)
        classList.FastAddValue(
            (KeyString)"replace",
            new JSFunction((in Arguments a) => JsUtilitiesReplaceClassToken(element, onClassChanged, in a), "replace", 2),
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
            new JSFunction((in Arguments a) => JsUtilitiesGetItem029Core(store, in a), "getItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // localStorage.setItem(key, value)
        storage.FastAddValue(
            (KeyString)"setItem",
            new JSFunction((in Arguments a) => JsUtilitiesSetItem030Core(storage, store, in a), "setItem", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // localStorage.removeItem(key)
        storage.FastAddValue(
            (KeyString)"removeItem",
            new JSFunction((in Arguments a) => JsUtilitiesRemoveItem031Core(storage, store, in a), "removeItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // localStorage.clear()
        storage.FastAddValue(
            (KeyString)"clear",
            new JSFunction((in Arguments a) => JsUtilitiesClear032Core(storage, store, in a), "clear", 0),
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
        if (TryGetAttribute(canvas, "width", out var w) && int.TryParse(w, out var pw)) width = pw;
        if (TryGetAttribute(canvas, "height", out var h) && int.TryParse(h, out var ph)) height = ph;

        var context2d = new CanvasRenderingContext2D(width, height);

        // fillStyle (get/set)
        ctx.FastAddProperty(
            (KeyString)"fillStyle",
            new JSFunction((in Arguments _) => new JSString(context2d.FillStyle), "get fillStyle"),
            new JSFunction((in Arguments a) => JsUtilitiesSetFillStyle034Core(context2d, in a), "set fillStyle"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // strokeStyle (get/set)
        ctx.FastAddProperty(
            (KeyString)"strokeStyle",
            new JSFunction((in Arguments _) => new JSString(context2d.StrokeStyle), "get strokeStyle"),
            new JSFunction((in Arguments a) => JsUtilitiesSetStrokeStyle036Core(context2d, in a), "set strokeStyle"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lineWidth (get/set)
        ctx.FastAddProperty(
            (KeyString)"lineWidth",
            new JSFunction((in Arguments _) => new JSNumber(context2d.LineWidth), "get lineWidth"),
            new JSFunction((in Arguments a) => JsUtilitiesSetLineWidth038Core(context2d, in a), "set lineWidth"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // font (get/set)
        ctx.FastAddProperty(
            (KeyString)"font",
            new JSFunction((in Arguments _) => new JSString(context2d.Font), "get font"),
            new JSFunction((in Arguments a) => JsUtilitiesSetFont040Core(context2d, in a), "set font"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // globalAlpha (get/set)
        ctx.FastAddProperty(
            (KeyString)"globalAlpha",
            new JSFunction((in Arguments _) => new JSNumber(context2d.GlobalAlpha), "get globalAlpha"),
            new JSFunction((in Arguments a) => JsUtilitiesSetGlobalAlpha042Core(context2d, in a), "set globalAlpha"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // canvas property
        ctx.FastAddProperty(
            (KeyString)"canvas",
            new JSFunction((in Arguments _) => new JSObject(), "get canvas"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // Drawing methods
        ctx.FastAddValue((KeyString)"fillRect", new JSFunction((in Arguments a) => JsUtilitiesFillRect044Core(context2d, in a), "fillRect", 4), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"strokeRect", new JSFunction((in Arguments a) => JsUtilitiesStrokeRect045Core(context2d, in a), "strokeRect", 4), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"clearRect", new JSFunction((in Arguments a) => JsUtilitiesClearRect046Core(context2d, in a), "clearRect", 4), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"beginPath", new JSFunction((in Arguments _) => JsUtilitiesBeginPath047Core(context2d, in _), "beginPath", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"moveTo", new JSFunction((in Arguments a) => JsUtilitiesMoveTo048Core(context2d, in a), "moveTo", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"lineTo", new JSFunction((in Arguments a) => JsUtilitiesLineTo049Core(context2d, in a), "lineTo", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"arc", new JSFunction((in Arguments a) => JsUtilitiesArc050Core(context2d, in a), "arc", 5), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"closePath", new JSFunction((in Arguments _) => JsUtilitiesClosePath051Core(context2d, in _), "closePath", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"fill", new JSFunction((in Arguments _) => JsUtilitiesFill052Core(context2d, in _), "fill", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"stroke", new JSFunction((in Arguments _) => JsUtilitiesStroke053Core(context2d, in _), "stroke", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"fillText", new JSFunction((in Arguments a) => JsUtilitiesFillText054Core(context2d, in a), "fillText", 3), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"strokeText", new JSFunction((in Arguments a) => JsUtilitiesStrokeText055Core(context2d, in a), "strokeText", 3), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"save", new JSFunction((in Arguments _) => JsUtilitiesSave056Core(context2d, in _), "save", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"restore", new JSFunction((in Arguments _) => JsUtilitiesRestore057Core(context2d, in _), "restore", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        // measureText(text) — returns { width: ... }
        ctx.FastAddValue((KeyString)"measureText", new JSFunction((in Arguments a) => JsUtilitiesMeasureText058Core(in a), "measureText", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        return ctx;
    }
#endif

    // ------------------------------------------------------------------
    //  Element name validation
    // ------------------------------------------------------------------

    /// <summary>
    /// Regex for valid XML Name: must start with a Unicode letter or underscore,
    /// followed by Unicode letters, digits, hyphens, underscores, or dots.
    /// Uses Unicode categories per XML 1.0 §2.3 to accept non-ASCII characters
    /// such as U+212A (Kelvin sign).
    /// Colons are NOT allowed (use <see cref="ValidXmlQualifiedNamePattern"/> for qualified names).
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex ValidXmlNamePattern = new(
        @"^[\p{L}_][\p{L}\p{N}_.\-]*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Regex for valid XML QName: either a simple name or prefix:localName
    /// where both prefix and localName are valid XML names (no colons).
    /// Uses Unicode categories per XML 1.0 §2.3.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex ValidXmlQualifiedNamePattern = new(
        @"^[\p{L}_][\p{L}\p{N}_.\-]*(?::[\p{L}_][\p{L}\p{N}_.\-]*)?$",
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
