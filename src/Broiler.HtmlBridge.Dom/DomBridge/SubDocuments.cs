using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.HtmlBridge.Logging;
using Broiler.HtmlBridge.Scripting;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // The nested-browsing-context state — the per-container sub-document/sub-window JS-object identity,
    // location/base-URL caches, object-load-failure and onload-fired marks, the reverse
    // sub-window→container map, the current-window override, and the P4.4b severed content-document
    // maps — is owned by _browsingContexts (P3.16 BrowsingContextManager, declared in DomBridge.cs).
    // The builders / resolvers / onload dispatch below stay bridge-owned and reach it through that owner.

    private DomDocument? GetContentDocument(DomElement containerElement) =>
        _browsingContexts.GetContentDocument(containerElement);

    /// <summary>The frame element a severed sub-document belongs to, or null for the main /
    /// a detached (createDocument) document. Replaces <c>ParentEl(#subdoc-root)</c>.</summary>
    private DomElement? GetFrameForContentDocument(DomNode? docRoot) =>
        docRoot is DomDocument document ? _browsingContexts.GetContainerForDocument(document) : null;

    private void LinkContentDocument(DomElement containerElement, DomDocument document) =>
        _browsingContexts.LinkContentDocument(containerElement, document);

    /// <summary>The content-document resolver handed to the layout view (P4.4b): maps a
    /// nested-browsing-context container to its severed sub-document so the box builder
    /// projects it as a sub-viewport and composes its geometry into the main frame.</summary>
    private DomDocument? ResolveContentDocumentForRender(DomElement containerElement) =>
        GetContentDocument(containerElement);

    private void InvalidateCachedSubDocument(DomElement containerElement)
    {
        // Order preserved from the pre-P3.16 code: release the old content document's element
        // runtime state while the maps still reference it, then unlink and drop the per-container caches.
        if (_browsingContexts.GetContentDocument(containerElement) is { } existingDocument)
        {
            RemoveElementsRecursive(existingDocument);
            _browsingContexts.UnlinkContentDocument(containerElement);
        }

        _browsingContexts.RemoveContainerCaches(containerElement);
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
        if (_browsingContexts.HasOnloadFired(element)) return;

        var tag = element.TagName?.ToLowerInvariant();
        if (tag != "iframe" && tag != "object") return;

        var hasSrcDoc = tag == "iframe" && HasAttr(element, "srcdoc");
        var resourceUrl = hasSrcDoc ? "about:srcdoc" : GetSubResourceUrl(element);
        if (string.IsNullOrWhiteSpace(resourceUrl) && !hasSrcDoc) return;

        // Ensure the sub-document is loaded (this triggers the fetch if needed)
        GetOrCreateSubDocument(element);

        _browsingContexts.MarkOnloadFired(element);

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
        // Snapshot before iterating: FireSubDocumentOnload runs a sub-document's
        // onload handler, whose script can structurally mutate element.Children
        // mid-walk (append/remove nodes). Enumerating the live collection then
        // throws "Collection was modified" (crash signature
        // DomBridge.FireDescendantOnloads). SnapshotChildren also tolerates a
        // concurrent structural race, like the other DomBridge tree walks.
        foreach (var child in SnapshotChildren(element))
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
        if (_browsingContexts.HasObjectLoadFailed(objectElement))
            return true;

        // Check if this is the first access — probe the resource
        var resourceUrl = GetSubResourceUrl(objectElement);
        if (string.IsNullOrWhiteSpace(resourceUrl))
            return false; // No data attribute → empty sub-document, not a failure

        var (_, contentType) = TryFetchSubResource(resourceUrl, GetInheritedSubDocumentBaseUrl(objectElement));
        if (string.Equals(contentType, FetchFailedContentType, StringComparison.Ordinal))
        {
            _browsingContexts.MarkObjectLoadFailed(objectElement);
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
        if (_browsingContexts.TryGetSubDocument(containerElement, out var cached))
            return cached;

        var executeHtmlScripts = false;
        string? htmlToExecute = null;
        DomDocument? docRoot = GetContentDocument(containerElement);
        if (docRoot == null)
        {
            if (string.Equals(containerElement.TagName, "iframe", StringComparison.OrdinalIgnoreCase) &&
                TryGetAttribute(containerElement, "srcdoc", out var srcDoc))
            {
                _browsingContexts.SetLocation(containerElement, "about:srcdoc");
                _browsingContexts.SetBaseUrl(containerElement, GetInheritedSubDocumentBaseUrl(containerElement));
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
                    _browsingContexts.SetLocation(containerElement, resolvedUrl);
                    _browsingContexts.SetBaseUrl(containerElement, resolvedUrl);
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

        var doc = _subDocuments.BuildDocument(docRoot);
        _browsingContexts.SetSubDocument(containerElement, doc);
        if (executeHtmlScripts && !string.IsNullOrEmpty(htmlToExecute))
            ExecuteSubDocumentScripts(containerElement, htmlToExecute);
        return doc;
    }

    private static DomElement? FindBodyElement(DomElement documentElement) =>
        ChildElements(documentElement).FirstOrDefault(c =>
            !IsText(c) &&
            string.Equals(c.TagName, "body", StringComparison.OrdinalIgnoreCase));

    private string GetInheritedSubDocumentBaseUrl(DomElement containerElement)
    {
        var parentFrame = GetFrameForContentDocument(GetOwningDocument(containerElement));
        if (parentFrame != null &&
            _browsingContexts.TryGetBaseUrl(parentFrame, out var parentBaseUrl) &&
            !string.IsNullOrWhiteSpace(parentBaseUrl))
        {
            return parentBaseUrl;
        }

        return _pageUrl;
    }

    private string GetSubDocumentBaseUrl(DomElement containerElement)
    {
        return _browsingContexts.TryGetBaseUrl(containerElement, out var baseUrl) &&
               !string.IsNullOrWhiteSpace(baseUrl)
            ? baseUrl
            : GetInheritedSubDocumentBaseUrl(containerElement);
    }

    private string ResolveSubResourceUrl(string resourceUrl, string? baseUrl = null)
    {
        resourceUrl = NormalizeWptPlaceholderUrl(resourceUrl);
        if (string.IsNullOrWhiteSpace(resourceUrl))
            return string.Empty;

        // Frames adopt the one shared resolver (Phase 7 item 4) — same absolute-stays / relative-resolves /
        // else-empty behaviour that script and CSP already share via UrlResolver.
        var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? _pageUrl : baseUrl;
        return UrlResolver.Resolve(resourceUrl, effectiveBaseUrl)?.AbsoluteUri ?? string.Empty;
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
        if (!string.IsNullOrWhiteSpace(_resources.LocalBasePath))
            candidates.Add(_resources.LocalBasePath);

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

        var extraction = ScriptExtractionService.ExtractAll(html, GetSubDocumentBaseUrl(containerElement));
        if (extraction.Scripts.Count == 0 &&
            extraction.AsyncScripts.Count == 0 &&
            extraction.DeferredScripts.Count == 0)
            return;

        var subWindow = _subWindows.GetOrCreate(containerElement);

        RunWithWindowContext(subWindow, () =>
        {
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

            foreach (var script in extraction.AsyncScripts)
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
        });
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
    private DomDocument BuildEmptySubDocument(DomElement containerElement)
    {
        var document = CreateBrowsingContextDocument();

        var htmlEl = CreateBridgeElement("html");
        document.AppendChild(htmlEl);

        var headEl = CreateBridgeElement("head");
        SetParent(headEl, htmlEl);
        htmlEl.AppendChild(headEl);

        var bodyEl = CreateBridgeElement("body");
        SetParent(bodyEl, htmlEl);
        htmlEl.AppendChild(bodyEl);

        LinkContentDocument(containerElement, document);

        return document;
    }

    /// <summary>
    /// Creates a sub-document with plain text content wrapped in a <c>&lt;pre&gt;</c> element.
    /// Used for <c>text/plain</c> resources.
    /// </summary>
    private DomDocument BuildSubDocumentWithText(string textContent, DomElement containerElement)
    {
        var document = CreateBrowsingContextDocument();

        var htmlEl = CreateBridgeElement("html");
        document.AppendChild(htmlEl);

        var headEl = CreateBridgeElement("head");
        SetParent(headEl, htmlEl);
        htmlEl.AppendChild(headEl);

        var bodyEl = CreateBridgeElement("body");
        SetParent(bodyEl, htmlEl);
        htmlEl.AppendChild(bodyEl);

        // Wrap text content in <pre> element
        var preEl = CreateBridgeElement("pre");
        SetParent(preEl, bodyEl);
        bodyEl.AppendChild(preEl);

        var textNode = CreateBridgeTextNode(textContent);
        SetParent(textNode, preEl);
        preEl.AppendChild(textNode);

        LinkContentDocument(containerElement, document);

        return document;
    }

    /// <summary>
    /// Gets the resource URL for a container element (iframe src or object data).
    /// </summary>
    internal static string GetSubResourceUrl(DomElement containerElement)
    {
        var tag = containerElement.TagName?.ToLowerInvariant();
        if (tag == "iframe")
            return TryGetAttribute(containerElement, "src", out var src) ? src : string.Empty;
        if (tag == "object")
            return TryGetAttribute(containerElement, "data", out var data) ? data : string.Empty;
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
        if (!string.IsNullOrEmpty(_resources.LocalBasePath))
        {
            var localResult = TryReadLocalResource(resourceUrl, extensionMime);
            if (localResult.content != null || localResult.contentType != string.Empty)
                return localResult;
        }

        // Resolve relative URL against page URL. An absolute URL keeps its raw string so the scheme
        // checks below (file:// / http(s)) and WPT host mapping see the exact original prefix; only the
        // relative case goes through the shared resolver (Phase 7 item 4).
        string resolvedUrl;
        if (Uri.TryCreate(resourceUrl, UriKind.Absolute, out _))
        {
            resolvedUrl = resourceUrl;
        }
        else if (UrlResolver.Resolve(resourceUrl, string.IsNullOrWhiteSpace(baseUrl) ? _pageUrl : baseUrl) is { } resolved)
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
            using var response = _resources.GetAsync(resolvedUrl).GetAwaiter().GetResult();
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
    /// The file existence + binary/text read policy lives in the host <see cref="Runtime.ResourceLoader"/>
    /// (Phase 7 item 4); this method only maps the URL to a path and the loader's I/O exceptions to the
    /// empty-document contract.
    /// </summary>
    private (string? content, string contentType) TryReadFileResource(string fileUrl, string extensionMime)
    {
        try
        {
            var path = new Uri(fileUrl).LocalPath;
            return _resources.LoadLocalResource(path, extensionMime);
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
        if (string.IsNullOrEmpty(_resources.LocalBasePath))
            return (null, string.Empty);

        // Strip query string and fragment from the URL to get the filename
        var filename = resourceUrl;
        var qIdx = filename.IndexOf('?');
        if (qIdx >= 0) filename = filename[..qIdx];
        var hIdx = filename.IndexOf('#');
        if (hIdx >= 0) filename = filename[..hIdx];

        // Only handle relative URLs (no scheme)
        if (filename.Contains("://")) return (null, string.Empty);

        var localPath = Path.Combine(_resources.LocalBasePath, filename);

        try
        {
            // The existence + binary/text read policy lives in the host loader (Phase 7 item 4); missing
            // → (null, ""), binary → (null, extensionMime), text → (content, extensionMime).
            var (content, detectedMime) = _resources.LoadLocalResource(localPath, extensionMime);

            // Detect content type from the read text when extension-based detection was generic.
            if (content != null &&
                (string.Equals(detectedMime, "application/octet-stream", StringComparison.OrdinalIgnoreCase)
                 || string.IsNullOrEmpty(detectedMime)))
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
            (trimmed.StartsWith('<') && !trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) &&
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
    private DomDocument BuildSubDocumentFromHtml(string html, DomElement containerElement)
    {
        var document = CreateBrowsingContextDocument();

        var (parsedRoot, allElements, _) = BuildDocumentTree(html);

        // parsedRoot is the <html> element itself (HtmlTreeBuilder returns it directly).
        // Append it as the sub-document's documentElement (a canonical DomDocument child).
        document.AppendChild(parsedRoot);

        LinkContentDocument(containerElement, document);

        return document;
    }

}
