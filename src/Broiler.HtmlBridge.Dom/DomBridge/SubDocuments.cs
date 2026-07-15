using System.Text;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.HtmlBridge.Logging;
using Broiler.Dom;
using System.Xml.Linq;

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

    private JSObject GetOrCreateSubWindow(DomElement containerElement)
    {
        if (_browsingContexts.TryGetSubWindow(containerElement, out var cached))
            return cached;

        var subDocument = GetOrCreateSubDocument(containerElement);
        var subWindow = new JSObject();
        _browsingContexts.SetSubWindow(containerElement, subWindow);
        _eventTargets.SetOwnerWindow(subWindow, subWindow);
        _messaging.InstallEventTargetApi(subWindow, "DomBridge.subWindow.dispatchEvent");
        _messaging.RegisterWindowMessaging(subWindow);

        subWindow.FastAddProperty((KeyString)"document",
            new JSFunction((in _) => GetOrCreateSubDocument(containerElement), "get document"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        var locationHref = GetSubWindowLocationHref(containerElement);
        var iframeLocation = new JSObject();
        iframeLocation.FastAddValue((KeyString)"href",
            new JSString(locationHref), JSPropertyAttributes.EnumerableConfigurableValue);
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
        subWindow.FastAddValue((KeyString)"location", iframeLocation, JSPropertyAttributes.EnumerableConfigurableValue);

        subWindow.FastAddProperty((KeyString)"scrollX", new JSFunction((in _) => new JSNumber(GetSubWindowScrollOffset(containerElement, vertical: false)), "get scrollX"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        subWindow.FastAddProperty((KeyString)"scrollY", new JSFunction((in _) => new JSNumber(GetSubWindowScrollOffset(containerElement, vertical: true)), "get scrollY"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        subWindow.FastAddProperty((KeyString)"pageXOffset", new JSFunction((in _) => new JSNumber(GetSubWindowScrollOffset(containerElement, vertical: false)), "get pageXOffset"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        subWindow.FastAddProperty((KeyString)"pageYOffset", new JSFunction((in _) => new JSNumber(GetSubWindowScrollOffset(containerElement, vertical: true)), "get pageYOffset"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        subWindow.FastAddValue((KeyString)"scroll", new JSFunction((in a) => JsSubDocumentsScroll006Core(containerElement, in a), "scroll", 2), JSPropertyAttributes.EnumerableConfigurableValue);
        subWindow.FastAddValue((KeyString)"scrollTo", new JSFunction((in a) => JsSubDocumentsScrollTo007Core(containerElement, in a), "scrollTo", 2), JSPropertyAttributes.EnumerableConfigurableValue);
        subWindow.FastAddValue((KeyString)"scrollBy", new JSFunction((in a) => JsSubDocumentsScrollBy008Core(containerElement, in a), "scrollBy", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        subWindow.FastAddValue((KeyString)"self", subWindow, JSPropertyAttributes.EnumerableConfigurableValue);
        subWindow.FastAddValue((KeyString)"window", subWindow, JSPropertyAttributes.EnumerableConfigurableValue);

        if (_jsContext?["Event"] is { } eventCtor)
            subWindow.FastAddValue((KeyString)"Event", eventCtor, JSPropertyAttributes.EnumerableConfigurableValue);

        if (_jsContext?["CustomEvent"] is { } customEventCtor)
            subWindow.FastAddValue((KeyString)"CustomEvent", customEventCtor, JSPropertyAttributes.EnumerableConfigurableValue);

        if (_jsContext?["MouseEvent"] is { } mouseEventCtor)
            subWindow.FastAddValue((KeyString)"MouseEvent", mouseEventCtor, JSPropertyAttributes.EnumerableConfigurableValue);

        if (_jsContext?["FocusEvent"] is { } focusEventCtor)
            subWindow.FastAddValue((KeyString)"FocusEvent", focusEventCtor, JSPropertyAttributes.EnumerableConfigurableValue);

        if (_jsContext?["KeyboardEvent"] is { } keyboardEventCtor)
            subWindow.FastAddValue((KeyString)"KeyboardEvent", keyboardEventCtor, JSPropertyAttributes.EnumerableConfigurableValue);

        if (_jsContext?["WheelEvent"] is { } wheelEventCtor)
            subWindow.FastAddValue((KeyString)"WheelEvent", wheelEventCtor, JSPropertyAttributes.EnumerableConfigurableValue);

        if (_jsContext?["UIEvent"] is { } uiEventCtor)
            subWindow.FastAddValue((KeyString)"UIEvent", uiEventCtor, JSPropertyAttributes.EnumerableConfigurableValue);

        if (_jsContext?["MessageChannel"] is { } messageChannelCtor)
            subWindow.FastAddValue((KeyString)"MessageChannel", messageChannelCtor, JSPropertyAttributes.EnumerableConfigurableValue);

        var parentWindow = GetParentWindowForSubDocument(containerElement);
        if (parentWindow != null)
        {
            subWindow.FastAddValue((KeyString)"parent", parentWindow, JSPropertyAttributes.EnumerableConfigurableValue);
        }

        subWindow.FastAddValue((KeyString)"top", _windowJSObject ?? subWindow, JSPropertyAttributes.EnumerableConfigurableValue);

        subDocument.FastAddValue((KeyString)"defaultView", subWindow, JSPropertyAttributes.EnumerableConfigurableValue);

        // window.getComputedStyle — sub-window needs its own copy so that
        // doc.defaultView.getComputedStyle(node, "") resolves CSS rules from
        // the sub-document's <style> elements rather than the main document.
        var bridgeForSubStyle = this;
        subWindow.FastAddValue((KeyString)"getComputedStyle", new JSFunction((in a) => JsSubDocumentsGetComputedStyle009Core(bridgeForSubStyle, in a), "getComputedStyle", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return subWindow;
    }

    private string GetSubWindowLocationHref(DomElement containerElement)
    {
        if (_browsingContexts.TryGetLocation(containerElement, out var cachedLocation) &&
            !string.IsNullOrWhiteSpace(cachedLocation))
        {
            return cachedLocation;
        }

        if (string.Equals(containerElement.TagName, "iframe", StringComparison.OrdinalIgnoreCase) &&
            HasAttr(containerElement, "srcdoc"))
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

    private DomElement? GetSubDocumentScrollingElement(DomElement containerElement)
    {
        var document = GetContentDocument(containerElement);
        return document == null ? null : GetDocumentElement(document);
    }

    private static DomElement? FindBodyElement(DomElement documentElement) =>
        ChildElements(documentElement).FirstOrDefault(c =>
            !IsText(c) &&
            string.Equals(c.TagName, "body", StringComparison.OrdinalIgnoreCase));

    private JSObject? GetParentWindowForSubDocument(DomElement containerElement)
    {
        // The container's owning document is a severed sub-document DomDocument when the container is
        // itself nested in another frame; recover that frame via the reverse map (P4.4c: the owning
        // document comes from the canonical tree, was OwnerDocRoot / ParentEl(#subdoc-root)).
        var parentFrame = GetFrameForContentDocument(GetOwningDocument(containerElement));
        if (parentFrame != null)
            return GetOrCreateSubWindow(parentFrame);

        return _windowJSObject;
    }

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

        var subWindow = GetOrCreateSubWindow(containerElement);

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
    private static string GetSubResourceUrl(DomElement containerElement)
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
    /// </summary>
    private static (string? content, string contentType) TryReadFileResource(string fileUrl, string extensionMime)
    {
        try
        {
            var uri = new Uri(fileUrl);
            var path = uri.LocalPath;
            if (!File.Exists(path))
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

            var content = File.ReadAllText(path);
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
        if (!File.Exists(localPath))
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
            var content = File.ReadAllText(localPath);

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

    // RF-BRIDGE-1c Phase F (F3c part 2d): unregister the whole node subtree from the bridge's
    // per-node caches when a sub-document root is torn down (raw ChildNodes so canonical
    // text/comment nodes are released too). The former AddElementsRecursive counterpart is gone —
    // node membership is now read from the canonical tree, so there is nothing to register on build.
    private void RemoveElementsRecursive(DomNode node)
    {
        _jsObjects.Remove(node);

        if (node is DomElement element)
            _styleSheetCache.Remove(element);

        foreach (var child in node.ChildNodes)
            RemoveElementsRecursive(child);
    }

    private void NormalizeNode(DomElement node)
    {
        for (var index = 0; index < node.ChildNodes.Count;)
        {
            var child = ChildAt(node, index);
            if (!IsText(child))
            {
                // Recurse into element children (a comment has no text children to merge).
                if (child is DomElement childElement)
                    NormalizeNode(childElement);
                index++;
                continue;
            }

            var mergedText = BridgeText(child);
            var nextIndex = index + 1;
            while (nextIndex < node.ChildNodes.Count && IsText(ChildAt(node, nextIndex)))
            {
                mergedText += BridgeText(ChildAt(node, nextIndex));
                RemoveChildAt(node, nextIndex);
            }

            if (mergedText.Length == 0)
            {
                RemoveChildAt(node, index);
                continue;
            }

            SetCharacterData(child, mergedText);
            index++;
        }
    }

    private void RemoveChildAt(DomElement parent, int index)
    {
        if (index < 0 || index >= parent.ChildNodes.Count)
            return;

        var child = ChildAt(parent, index);
        NotifyNodeIteratorPreRemoval(child);
        RemoveNthChild(parent, index);
        SetParent(child, null);
        InvalidateStyleScope(parent);
        NotifyChildRemoved(parent, child, index);
    }

    // Phase 4 items 4/5 (P4.9 follow-up): the bridge's NodesAreEqual / CanonicalAttributesAreEqual
    // copies were deleted after their promotion to canonical Broiler.Dom.DomNode.IsEqualNode landed
    // in the pinned submodule (patches/0001, applied by the maintainer). The isEqualNode binding now
    // delegates to node.IsEqualNode(other); behaviour is pinned by IsEqualNodePromotionTests. The
    // canonical algorithm drops the bridge copy's element-level BridgeText comparison, which was a
    // no-op on the canonical tree (an element's NodeValue is null) — so it is behaviour-equivalent.

    private string NormalizeInsertAdjacentPosition(JSValue? value)
    {
        var position = value?.ToString().Trim().ToLowerInvariant() ?? string.Empty;
        if (position is "beforebegin" or "afterbegin" or "beforeend" or "afterend")
            return position;

        ThrowDOMException(_jsContext!, $"'{position}' is not a valid insertion position.", "SyntaxError");
        return string.Empty;
    }

    private (DomElement Parent, int Index) GetInsertAdjacentTarget(DomElement element, string position)
    {
        switch (position)
        {
            case "beforebegin":
                if (ParentEl(element) == null)
                    ThrowDOMException(_jsContext!, "Cannot insert adjacent content without a parent node.", "NoModificationAllowedError");
                return (ParentEl(element)!, ChildIndexOf(ParentEl(element)!, element));
            case "afterbegin":
                return (element, 0);
            case "beforeend":
                return (element, element.ChildNodes.Count);
            case "afterend":
                if (ParentEl(element) == null)
                    ThrowDOMException(_jsContext!, "Cannot insert adjacent content without a parent node.", "NoModificationAllowedError");
                return (ParentEl(element)!, ChildIndexOf(ParentEl(element)!, element) + 1);
            default:
                ThrowDOMException(_jsContext!, $"'{position}' is not a valid insertion position.", "SyntaxError");
                return (element, element.ChildNodes.Count);
        }
    }

    // Phase 4 item 1: parent widened DomElement -> DomNode so a canonical DomDocumentFragment can be
    // an insertion parent (fragment.appendChild/append/...). The style-scope invalidation and
    // child-added mutation notification are element-only concerns, guarded accordingly; the onload
    // firing below already guards on `node is DomElement`. Behaviour for element parents is identical.
    private void InsertNodeAt(DomNode parent, DomNode node, int index)
    {
        if (ReferenceEquals(node, parent) || parent.IsDescendantOf(node))
            ThrowDOMException(_jsContext!, "The new child element contains the parent.", "HierarchyRequestError");

        if (index < 0)
            index = 0;
        if (index > parent.ChildNodes.Count)
            index = parent.ChildNodes.Count;

        if (ParentEl(node) != null)
        {
            var oldParent = ParentEl(node);
            var oldIndex = ChildIndexOf(oldParent, node);
            if (oldIndex >= 0)
            {
                if (ReferenceEquals(oldParent, parent) && oldIndex < index)
                    index--;

                NotifyNodeIteratorPreRemoval(node);
                RemoveNthChild(oldParent, oldIndex);
                NotifyChildRemoved(oldParent, node, oldIndex);
            }
        }

        SetParent(node, parent);
        InsertChildAt(parent, index, node);
        if (parent is DomElement parentElement)
        {
            InvalidateStyleScope(parentElement);
            NotifyChildAdded(parentElement, node, index);
        }

        // RF-BRIDGE-1c Phase F (F3c part 2b): only elements carry a TagName / fire onloads; a
        // canonical char-data node inserts with no sub-document side effects.
        if (node is DomElement insertedElement)
        {
            var insertedTag = insertedElement.TagName?.ToLowerInvariant();
            if (insertedTag == "iframe" || insertedTag == "object")
                FireSubDocumentOnload(insertedElement);
            else
                FireDescendantOnloads(insertedElement);
        }
    }

    private List<DomNode> BuildAdjacentHtmlNodes(DomElement contextElement, string html)
    {
        var nodes = new List<DomNode>();
        if (string.IsNullOrEmpty(html))
            return nodes;

        if (!TryBuildInnerHtmlFragmentContainer(contextElement, html, out var fragmentContainer))
            return nodes;

        // RF-BRIDGE-1c Phase F (F3c part 2d): move ALL children (raw ChildNodes) so text/comment
        // nodes in the parsed fragment survive.
        foreach (var child in fragmentContainer.ChildNodes.ToArray())
        {
            RemoveChildFrom(fragmentContainer, child);
            SetParent(child, null);
            nodes.Add(child);
        }

        return nodes;
    }

    private List<DomNode> BuildChildNodeArgumentNodes(in Arguments arguments)
    {
        // RF-BRIDGE-1c Phase F (F3c part 2b): returns canonical DomNode — an argument may be a
        // text node (resolved via FindDomNodeByJSObject) and string arguments mint text nodes.
        var nodes = new List<DomNode>();
        for (var i = 0; i < arguments.Length; i++)
        {
            var value = arguments[i];
            if (value is JSObject candidateObject)
            {
                var candidateNode = FindDomNodeByJSObject(candidateObject);
                if (candidateNode != null)
                {
                    // Phase 4 item 1: a canonical DomDocumentFragment argument inserts its children
                    // (per DOM), not the fragment itself. (Was a "#document-fragment" TagName check on
                    // the former sentinel element — a non-element fragment no longer matches that.)
                    if (candidateNode is DomDocumentFragment candidateFragment)
                    {
                        foreach (var fragmentChild in candidateFragment.ChildNodes.ToArray())
                            nodes.Add(fragmentChild);
                        continue;
                    }

                    nodes.Add(candidateNode);
                    continue;
                }
            }

            var textNode = CreateBridgeTextNode(value.ToString());
            nodes.Add(textNode);
        }

        return nodes;
    }

    private void SetElementInnerHtml(DomElement element, string html)
    {
        html ??= string.Empty;

        foreach (var child in element.ChildNodes.ToArray())
            RemoveElementsRecursive(child);

        ClearChildren(element);

        if (!string.IsNullOrEmpty(html) &&
            TryBuildInnerHtmlFragmentContainer(element, html, out var fragmentContainer))
        {
            // RF-BRIDGE-1c Phase F (F3c part 2d): move ALL children so parsed text/comment survive.
            foreach (var child in fragmentContainer.ChildNodes.ToArray())
            {
                SetParent(child, element);
                element.AppendChild(child);
            }
        }

        ResetComputedStyleEngines();
        InvalidateStyleScope(element);
    }

    private void SetElementOuterHtml(DomElement element, string html)
    {
        html ??= string.Empty;

        var parent = ParentEl(element);
        if (parent == null)
            return;

        var index = ChildIndexOf(parent, element);
        if (index < 0)
            return;

        var previousSibling = index > 0 ? ChildAt(parent, index - 1) : null;
        var nextSibling = index + 1 < parent.ChildNodes.Count ? ChildAt(parent, index + 1) : null;

        DomDocumentFragment? parsedContainer = null;
        if (!string.IsNullOrEmpty(html))
        {
            var parsingContext = parent.TagName.StartsWith('#')
                ? CreateBridgeElement("body")
                : parent;
            if (TryBuildInnerHtmlFragmentContainer(parsingContext, html, out var fragmentContainer))
                parsedContainer = fragmentContainer;
        }

        NotifyNodeIteratorPreRemoval(element);
        RemoveNthChild(parent, index);
        SetParent(element, null);
        NotifyChildRemoved(parent, element, index, previousSibling, nextSibling);

        if (parsedContainer != null)
        {
            var insertIndex = index;
            foreach (var child in parsedContainer.ChildNodes.ToArray())
            {
                SetParent(child, parent);
                InsertChildAt(parent, insertIndex, child);
                NotifyChildAdded(parent, child, insertIndex);
                insertIndex++;
            }
        }

        ResetComputedStyleEngines();
        InvalidateStyleScope(parent);
    }

    private bool TryBuildInnerHtmlFragmentContainer(DomElement contextElement, string html, out DomDocumentFragment container)
    {
        container = null!;

        var contextTag = contextElement.TagName.ToLowerInvariant();
        if (IsVoidHtmlElementTag(contextTag))
            return false;

        var (fragment, _) = BuildFragmentTree(html, contextTag);
        container = fragment;
        return true;
    }

    private static DomElement? FindFirstElementByTag(DomElement root, string tag)
    {
        foreach (var child in ChildElements(root))
        {
            if (!IsText(child) && string.Equals(child.TagName, tag, StringComparison.OrdinalIgnoreCase))
                return child;

            var match = FindFirstElementByTag(child, tag);
            if (match != null)
                return match;
        }

        return null;
    }

    private static bool IsVoidHtmlElementTag(string tag) => tag is
        "area" or "base" or "br" or "col" or "embed" or "hr" or "img" or
        "input" or "link" or "meta" or "param" or "source" or "track" or "wbr";

    /// <summary>
    /// Builds a sub-document tree from XML/SVG/XHTML content using an XML parser.
    /// For XHTML with valid namespace, also executes embedded scripts.
    /// XML well-formedness errors result in an empty document.
    /// </summary>
    private DomDocument BuildSubDocumentFromXml(string xmlContent, string contentType, DomElement containerElement)
    {
        var document = CreateBrowsingContextDocument();

        try
        {
            // Strip XML processing instructions before parsing (XDocument doesn't need them)
            var cleanXml = xmlContent;
            while (cleanXml.TrimStart().StartsWith("<?xml-stylesheet", StringComparison.OrdinalIgnoreCase))
            {
                var piEnd = cleanXml.IndexOf("?>", StringComparison.Ordinal);
                if (piEnd >= 0) cleanXml = cleanXml[(piEnd + 2)..].TrimStart();
                else break;
            }

            var xdoc = XDocument.Parse(cleanXml);
            if (xdoc.Root == null)
            {
                LinkContentDocument(containerElement, document);
                return document;
            }

            // Check XHTML namespace validity
            var rootNs = xdoc.Root.Name.NamespaceName;
            var isXhtml = string.Equals(contentType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase);
            var hasCorrectXhtmlNs = string.Equals(rootNs, "http://www.w3.org/1999/xhtml", StringComparison.Ordinal);

            if (isXhtml && !hasCorrectXhtmlNs)
            {
                // Wrong XHTML namespace — create empty doc, don't execute scripts. It links
                // the container to its own document.
                return BuildEmptySubDocument(containerElement);
            }

            // Build DOM tree from XML
            var rootEl = BuildDomElementFromXElement(xdoc.Root);
            document.AppendChild(rootEl);

            LinkContentDocument(containerElement, document);

            // Execute scripts in XHTML documents with correct namespace
            if (isXhtml && hasCorrectXhtmlNs)
            {
                ExecuteSubDocumentScripts(rootEl);
            }
        }
        catch (System.Xml.XmlException)
        {
            // XML well-formedness error — return empty document, don't execute scripts
            LinkContentDocument(containerElement, document);
        }

        return document;
    }

    /// <summary>
    /// Recursively builds a Broiler.Dom.DomElement tree from an XElement.
    /// </summary>
    private DomElement BuildDomElementFromXElement(XElement xe)
    {
        var tagName = xe.Name.LocalName.ToLowerInvariant();
        var el = CreateBridgeElement(tagName);

        foreach (var attr in xe.Attributes())
        {
            if (!attr.IsNamespaceDeclaration)
                SetAttr(el, attr.Name.LocalName, attr.Value);
        }

        foreach (var child in xe.Nodes())
        {
            if (child is XElement childXe)
            {
                var childEl = BuildDomElementFromXElement(childXe);
                SetParent(childEl, el);
                el.AppendChild(childEl);
            }
            else if (child is XText childText)
            {
                var textNode = CreateBridgeTextNode(childText.Value);
                SetParent(textNode, el);
                el.AppendChild(textNode);
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

        foreach (var child in ChildElements(element))
            CollectScriptContent(child, scripts);
    }

    /// <summary>
    /// Gets the concatenated text content of an element and all its descendants.
    /// </summary>
    private static string GetTextContentRecursive(DomElement element)
    {
        // RF-BRIDGE-1c Phase F (F3c part 2d): aggregate descendant text over raw ChildNodes.
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        return sb.ToString();
    }

    /// <summary>
    /// Builds a full document JSObject for a sub-document tree rooted at the given element.
    /// </summary>
}
