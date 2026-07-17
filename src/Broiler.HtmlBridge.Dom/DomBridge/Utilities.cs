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
using Broiler.Dom;
using Broiler.CSS;

namespace Broiler.HtmlBridge;

/// <summary>
/// Internal helper methods — string conversions, DOM tree utilities,
/// form-control collection, table-row helpers, and JS-object builders
/// for <c>style</c>, <c>classList</c>, <c>localStorage</c>, and
/// <c>canvas.getContext("2d")</c>.
/// </summary>
public sealed partial class DomBridge
{
    /// <summary>
    /// Parses a DOCTYPE declaration and creates the canonical <see cref="DomDocumentType"/> node.
    /// </summary>
    private DomDocumentType? ParseDocType(string html)
    {
        var match = DocTypePattern.Match(html);
        if (!match.Success) return null;

        var name = match.Groups[1].Value;
        var publicId = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;
        var systemId = match.Groups[3].Success ? match.Groups[3].Value : string.Empty;

        return CreateBridgeDocumentType(name, publicId, systemId);
    }

    /// <summary>
    /// Searches descendants of an element using a CSS selector.
    /// </summary>
    // Phase 4 item 1: root widened DomElement -> DomNode so querySelector/querySelectorAll work over a
    // canonical DomDocumentFragment. A fragment cannot itself match a selector, so the :scope-root
    // self-match is guarded to element roots and the descendant scope is null for a fragment root.
    private static JSValue FindInDescendants(DomNode root, string selector, bool all, DomBridge bridge)
    {
        var results = new List<JSValue>();
        var scope = root as DomElement;
        if (scope is not null && selector.Contains(":scope") &&
            MatchesSelector(scope, selector, scope))
        {
            results.Add(bridge.ToJSObject(scope));
            if (!all)
                return results[0];
        }

        SearchDescendants(root, selector, results, bridge, all, scope);
        if (all) return new JSArray(results);
        return results.Count > 0 ? results[0] : JSNull.Value;
    }

    private static void SearchDescendants(DomNode parent, string selector, List<JSValue> results, DomBridge bridge, bool all, DomElement? scope)
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
    internal static void CollectTextContent(DomNode node, StringBuilder sb)
    {
        if (IsText(node))
        {
            sb.Append(BridgeText(node));
            return;
        }
        // RF-BRIDGE-1c Phase F (F3c part 2c): walk raw ChildNodes so direct text children are
        // aggregated (comment children contribute nothing — not IsText, no element children).
        // Behaviour-preserving on today's homogeneous tree where every child is an element.
        foreach (var child in node.ChildNodes)
            CollectTextContent(child, sb);
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
            decoded = System.Text.RegularExpressions.Regex.Replace(decoded, @"\s", string.Empty);
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
    private DomElement? FindDomElementByJSObject(JSObject jsObj) => FindDomNodeByJSObject(jsObj) as DomElement;

    /// <summary>
    /// Finds the canonical <see cref="DomNode"/> corresponding to a given
    /// <see cref="JSObject"/> by reverse-scanning the JS-object cache. Unlike
    /// <see cref="FindDomElementByJSObject"/> this also resolves text/comment nodes
    /// (RF-BRIDGE-1c Phase F — needed once ranges/selection carry canonical char-data nodes).
    /// </summary>
    private DomNode? FindDomNodeByJSObject(JSObject jsObj) =>
        _jsObjects.TryGetNode(jsObj, out var node) ? node : null;

    // Phase 4 item 5: the bridge's IsDescendant(ancestor, candidate) copy is deleted; call sites use
    // the canonical Broiler.Dom.DomNode.IsDescendantOf(ancestor) instance method (identical ancestor
    // walk; every bridge call site passes a non-null ancestor, so canonical's null-ancestor throw is
    // unreachable).

    /// <summary>
    /// Compares two nodes in document tree order.
    /// Returns -1 when <paramref name="first"/> precedes <paramref name="second"/>,
    /// 1 when it follows, and 0 when no ordering can be determined.
    /// </summary>
    internal static int CompareTreeOrder(DomNode first, DomNode second)
    {
        if (ReferenceEquals(first, second))
            return 0;

        var firstAncestors = new List<DomNode>();
        for (DomNode? current = first; current != null; current = current.ParentNode)
            firstAncestors.Add(current);
        firstAncestors.Reverse();

        var secondAncestors = new List<DomNode>();
        for (DomNode? current = second; current != null; current = current.ParentNode)
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
    /// The canonical <see cref="DomDocument"/> that owns <paramref name="node"/> (Phase 4 item 1,
    /// P4.4c). For a connected node this is the absolute tree root: after P4.4b a (sub-)document root
    /// is a canonical <see cref="DomDocument"/>, so ownership is derived from tree position — no
    /// parallel <c>OwnerDocRoot</c> field. A detached node falls back to the canonical owner-document
    /// set at construction/adoption (a sub-document's <c>createElement</c> node was adopted into its
    /// content document; every other node was minted from the main <c>_document</c>).
    /// </summary>
    internal static DomDocument GetOwningDocument(DomNode node)
    {
        DomNode root = node;
        while (root.ParentNode is { } parent)
            root = parent;
        return root as DomDocument ?? node.OwnerDocument;
    }

    /// <summary>
    /// Clones a <see cref="DomElement"/>. When <paramref name="deep"/> is true,
    /// all descendants are recursively cloned.
    /// </summary>
    private DomNode CloneDomElement(DomNode source, bool deep)
    {
        // RF-BRIDGE-1c Phase F (F3c): canonical character-data nodes clone via the document
        // factories (DomText/DomComment ctors are internal). Dead on today's homogeneous facade
        // tree — facade text/comment nodes are Broiler.Dom.DomElement and take the element path below; live
        // once text/comment construction flips to canonical DomText/DomComment.
        if (source is DomCharacterData sourceCharData)
        {
            return IsComment(source)
                ? _document.CreateComment(sourceCharData.Data)
                : _document.CreateTextNode(sourceCharData.Data);
        }

        // Phase 4 item 1: a canonical DomDocumentType clones its immutable name/publicId/systemId.
        if (source is DomDocumentType sourceDocType)
            return _document.CreateDocumentType(sourceDocType.Name, sourceDocType.PublicId, sourceDocType.SystemId);

        // Phase 4 item 1: a canonical DomDocumentFragment clones to an empty fragment; a deep clone
        // copies its children (a shallow clone is empty, per DOM).
        if (source is DomDocumentFragment sourceFragment)
        {
            var fragmentClone = CreateBridgeDocumentFragment();
            if (deep)
                foreach (var child in sourceFragment.ChildNodes.ToArray())
                    fragmentClone.AppendChild(CloneDomElement(child, true));
            return fragmentClone;
        }

        var element = (DomElement)source;
        // Preserve the source element's exact namespace (folds the old post-construction
        // `clone.NamespaceURI = element.NamespaceURI` — see below); SVG/foreign clones keep
        // their namespace rather than defaulting to HTML.
        var clone = CreateBridgeElementNS(element.NamespaceUri, element.TagName, element.Id, element.ClassName);
        // RF-BRIDGE-1c Phase C2: copy attributes straight from the canonical namespace-keyed
        // set so namespaced attributes (namespace, prefix, local name) survive the clone —
        // that fidelity used to depend on the separate NsAttrMap shadow. No-namespace
        // attributes go through SetAttribute (which lowercases, matching the prior snapshot
        // path); a prefixed qualified name can only exist with a namespace, so it never
        // reaches the SetAttribute branch (which would throw on the ':').
        foreach (var attribute in element.Attributes.Values)
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
        foreach (var kv in InlineStyle(element))
            cloneStyle[kv.Key] = kv.Value;
        // RF-BRIDGE-1c Phase F (F3c part 2d): element text lives in child DomText nodes, cloned by
        // the deep-clone loop below — no element-store TextContent scalar to copy.
        // (The namespace was carried at construction via CreateBridgeElementNS above.)
        // Copy browser-runtime values (e.g., checked state for inputs).
        GetElementRuntimeState(element).CopyRuntimeValuesTo(GetElementRuntimeState(clone));
        // Carry the memoized position-area resolution too (was ElementRuntimeState.Layout,
        // now the bridge-level PositionAreaResolutions cache — see PositionAreaQueries.cs).
        CopyPositionAreaResolution(element, clone);

        if (deep)
        {
            // RF-BRIDGE-1c Phase F (F3c): iterate raw ChildNodes (not ChildElements) so text/
            // comment children clone too once they flip to canonical DomText/DomComment. On the
            // homogeneous facade tree every child is already an element, so this is a no-op widen.
            foreach (var child in element.ChildNodes)
            {
                var childClone = CloneDomElement(child, true);
                SetParent(childClone, clone);
                clone.AppendChild(childClone);
            }
        }
        return clone;
    }

    /// <summary>
    /// Collects all &lt;tr&gt; elements in a table in HTMLTableElement.rows spec order:
    /// 1. thead rows, 2. tbody rows + direct tr children (in tree order), 3. tfoot rows. Neutral
    /// tree helper shared by the <c>TableBinding</c> feature module and hit testing.
    /// </summary>
    internal static List<DomElement> CollectTableRows(DomElement table)
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
        // 2. Direct tr children of the table, or tr children of tbody elements (in tree order)
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
    internal static void UncheckRadioSiblings(DomElement scope, DomElement except, string radioName)
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

    // form.elements collection (indexed + named access) moved to the Phase 3 FormBinding feature
    // module (Broiler.HtmlBridge.Dom.Features).


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
    /// Returns the node type constant for a <see cref="DomElement"/>.
    /// </summary>
    internal static int GetNodeType(DomNode node)
    {
        if (IsText(node)) return 3; // TEXT_NODE
        if (IsComment(node)) return 8;
        if (node is DomDocumentType) return 10; // DOCUMENT_TYPE_NODE (canonical)
        if (node is DomDocumentFragment) return 11; // DOCUMENT_FRAGMENT_NODE (canonical)
        if (node is DomDocument) return 9; // DOCUMENT_NODE (canonical DomDocument — document root)
        if (node is not DomElement) return 1;
        return 1; // ELEMENT_NODE
    }

    // classList / DOMTokenList moved to the Phase 3 ClassListBinding feature module
    // (Broiler.HtmlBridge.Dom.Features).
    // style / CSSStyleDeclaration (element.style, rule.style, getComputedStyle result) moved to the
    // Phase 3 (P3.14) StyleDeclarationBinding feature module (Broiler.HtmlBridge.Dom.Features).

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
        storage.FastAddValue((KeyString)"getItem",
            new JSFunction((in a) => JsUtilitiesGetItem029Core(store, in a), "getItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // localStorage.setItem(key, value)
        storage.FastAddValue((KeyString)"setItem",
            new JSFunction((in a) => JsUtilitiesSetItem030Core(storage, store, in a), "setItem", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // localStorage.removeItem(key)
        storage.FastAddValue((KeyString)"removeItem",
            new JSFunction((in a) => JsUtilitiesRemoveItem031Core(storage, store, in a), "removeItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // localStorage.clear()
        storage.FastAddValue((KeyString)"clear",
            new JSFunction((in a) => JsUtilitiesClear032Core(storage, store, in a), "clear", 0),
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
        ctx.FastAddProperty((KeyString)"fillStyle",
            new JSFunction((in _) => new JSString(context2d.FillStyle), "get fillStyle"),
            new JSFunction((in a) => JsUtilitiesSetFillStyle034Core(context2d, in a), "set fillStyle"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // strokeStyle (get/set)
        ctx.FastAddProperty((KeyString)"strokeStyle",
            new JSFunction((in _) => new JSString(context2d.StrokeStyle), "get strokeStyle"),
            new JSFunction((in a) => JsUtilitiesSetStrokeStyle036Core(context2d, in a), "set strokeStyle"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lineWidth (get/set)
        ctx.FastAddProperty((KeyString)"lineWidth",
            new JSFunction((in _) => new JSNumber(context2d.LineWidth), "get lineWidth"),
            new JSFunction((in a) => JsUtilitiesSetLineWidth038Core(context2d, in a), "set lineWidth"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // font (get/set)
        ctx.FastAddProperty((KeyString)"font",
            new JSFunction((in _) => new JSString(context2d.Font), "get font"),
            new JSFunction((in a) => JsUtilitiesSetFont040Core(context2d, in a), "set font"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // globalAlpha (get/set)
        ctx.FastAddProperty((KeyString)"globalAlpha",
            new JSFunction((in _) => new JSNumber(context2d.GlobalAlpha), "get globalAlpha"),
            new JSFunction((in a) => JsUtilitiesSetGlobalAlpha042Core(context2d, in a), "set globalAlpha"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // canvas property
        ctx.FastAddProperty((KeyString)"canvas",
            new JSFunction((in _) => new JSObject(), "get canvas"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // Drawing methods
        ctx.FastAddValue((KeyString)"fillRect", new JSFunction((in a) => JsUtilitiesFillRect044Core(context2d, in a), "fillRect", 4), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"strokeRect", new JSFunction((in a) => JsUtilitiesStrokeRect045Core(context2d, in a), "strokeRect", 4), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"clearRect", new JSFunction((in a) => JsUtilitiesClearRect046Core(context2d, in a), "clearRect", 4), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"beginPath", new JSFunction((in _) => JsUtilitiesBeginPath047Core(context2d, in _), "beginPath", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"moveTo", new JSFunction((in a) => JsUtilitiesMoveTo048Core(context2d, in a), "moveTo", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"lineTo", new JSFunction((in a) => JsUtilitiesLineTo049Core(context2d, in a), "lineTo", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"arc", new JSFunction((in a) => JsUtilitiesArc050Core(context2d, in a), "arc", 5), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"closePath", new JSFunction((in _) => JsUtilitiesClosePath051Core(context2d, in _), "closePath", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"fill", new JSFunction((in _) => JsUtilitiesFill052Core(context2d, in _), "fill", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"stroke", new JSFunction((in _) => JsUtilitiesStroke053Core(context2d, in _), "stroke", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"fillText", new JSFunction((in a) => JsUtilitiesFillText054Core(context2d, in a), "fillText", 3), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"strokeText", new JSFunction((in a) => JsUtilitiesStrokeText055Core(context2d, in a), "strokeText", 3), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"save", new JSFunction((in _) => JsUtilitiesSave056Core(context2d, in _), "save", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"restore", new JSFunction((in _) => JsUtilitiesRestore057Core(context2d, in _), "restore", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        // measureText(text) — returns { width: ... }
        ctx.FastAddValue((KeyString)"measureText", new JSFunction((in a) => JsUtilitiesMeasureText058Core(in a), "measureText", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        return ctx;
    }
#endif

}
