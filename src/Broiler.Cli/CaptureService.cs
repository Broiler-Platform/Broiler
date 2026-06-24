using System.Text;
using System.Text.RegularExpressions;
using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.HTML.Core.Entities;

namespace Broiler.Cli;

/// <summary>
/// Supported output formats for captured content.
/// </summary>
public enum OutputFormat
{
    /// <summary>HTML output.</summary>
    Html,

    /// <summary>Plain-text output.</summary>
    Text,
}

/// <summary>
/// Supported image formats for image capture.
/// </summary>
public enum ImageFormat
{
    /// <summary>PNG image format.</summary>
    Png,

    /// <summary>JPEG image format.</summary>
    Jpeg,
}

/// <summary>
/// Options for configuring a website image capture operation.
/// </summary>
public class ImageCaptureOptions
{
    /// <summary>
    /// The URL of the website to capture as an image.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// The output file path for the captured image.
    /// </summary>
    public required string OutputPath { get; init; }

    /// <summary>
    /// The width of the rendered image in pixels. Defaults to 1024.
    /// </summary>
    public int Width { get; init; } = 1024;

    /// <summary>
    /// The height of the rendered image in pixels. Defaults to 768.
    /// </summary>
    public int Height { get; init; } = 768;

    /// <summary>
    /// When <c>true</c>, the renderer automatically sizes the image to
    /// fit the full HTML content instead of clipping to
    /// <see cref="Width"/>×<see cref="Height"/>.
    /// </summary>
    public bool FullPage { get; init; }

    /// <summary>
    /// Navigation timeout in seconds. Defaults to 30.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// When <c>true</c>, the renderer extracts the first link from the
    /// initial HTML page and navigates to it before rendering. This
    /// emulates the Chromium/Playwright behavior for test landing pages
    /// (e.g.&nbsp;Acid2) that require a click to start.
    /// </summary>
    public bool FollowFirstLink { get; init; }

    /// <summary>
    /// Determines the image format from the output file extension.
    /// Returns <see cref="ImageFormat.Jpeg"/> for .jpg/.jpeg files,
    /// otherwise <see cref="ImageFormat.Png"/>.
    /// </summary>
    public ImageFormat ImageFormat
    {
        get
        {
            var ext = Path.GetExtension(OutputPath);
            return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                ? ImageFormat.Jpeg
                : ImageFormat.Png;
        }
    }
}

/// <summary>
/// Options for configuring a website capture operation.
/// </summary>
public class CaptureOptions
{
    /// <summary>
    /// The URL of the website to capture.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// The output file path for the captured content.
    /// </summary>
    public required string OutputPath { get; init; }

    /// <summary>
    /// Whether to capture the full page content or only a summary.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool FullPage { get; init; }

    /// <summary>
    /// Navigation timeout in seconds. Defaults to 30.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// When <c>true</c>, the renderer extracts the first link from the
    /// initial HTML page and navigates to it before capturing. This
    /// emulates the Chromium/Playwright behavior for test landing pages
    /// (e.g.&nbsp;Acid2) that require a click to start.
    /// </summary>
    public bool FollowFirstLink { get; init; }

    /// <summary>
    /// Determines the output format from the output file extension.
    /// Returns <see cref="OutputFormat.Text"/> for .txt files,
    /// otherwise <see cref="OutputFormat.Html"/>.
    /// </summary>
    public OutputFormat OutputFormat
    {
        get
        {
            var ext = Path.GetExtension(OutputPath);
            return ext.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                ? OutputFormat.Text
                : OutputFormat.Html;
        }
    }
}

/// <summary>
/// Service that captures website content using HttpClient,
/// HTML-Renderer for CSS processing, and YantraJS for script execution.
/// </summary>
public class CaptureService
{
    private static readonly Regex ScriptPattern = new(
        @"<script(?![^>]*\ssrc\s*=)[^>]*>(?<content>[\s\S]*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DataScriptPattern = new(
        @"<script[^>]*\ssrc\s*=\s*[""']?(?<uri>data:[^""'\s>]+)[""']?[^>]*>\s*</script>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches all &lt;script&gt; tags (both inline and with src attributes)
    /// in document order. The tag attributes and body content are captured
    /// so the caller can determine the script type.
    /// </summary>
    private static readonly Regex AnyScriptPattern = new(
        @"<script(?<attrs>[^>]*)>(?<content>[\s\S]*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SrcAttrPattern = new(
        @"\ssrc\s*=\s*(?:""(?<uri>data:[^""]+)""|'(?<uri>data:[^']+)'|(?<uri>data:[^\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches any <c>src</c> attribute value (not just <c>data:</c> URIs).
    /// Used to extract external script URLs for HTTP/HTTPS/file loading.
    /// </summary>
    private static readonly Regex AnySrcAttrPattern = new(
        @"\ssrc\s*=\s*(?:""(?<uri>[^""]+)""|'(?<uri>[^']+)'|(?<uri>[^\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches the <c>defer</c> attribute on a script tag (standalone or with a value).
    /// </summary>
    private static readonly Regex DeferAttrPattern = new(
        @"(?:^|\s)defer(?:\s|$|=)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StylePattern = new(
        @"<style[^>]*>(?<content>[\s\S]*?)</style>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.Compiled);


    /// <summary>
    /// Captures website content from the specified URL, processes it using
    /// the local rendering engines (HTML-Renderer and YantraJS), and saves
    /// the result to the output path.
    /// </summary>
    /// <param name="options">Capture configuration options.</param>
    /// <returns>A task that completes when the capture is finished.</returns>
    /// <exception cref="HttpRequestException">Thrown when the URL cannot be fetched.</exception>
    /// <exception cref="IOException">Thrown when the output file cannot be written.</exception>
    public async Task CaptureAsync(CaptureOptions options)
    {
        try
        {
            var outputDir = Path.GetDirectoryName(Path.GetFullPath(options.OutputPath));
            if (outputDir != null && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PathTooLongException)
        {
            throw new IOException($"Cannot create output directory: {ex.Message}", ex);
        }

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        };

        var html = await httpClient.GetStringAsync(new Uri(options.Url));

        // Follow the first link if requested (e.g. Acid2 landing page navigation).
        if (options.FollowFirstLink)
        {
            html = await LinkNavigator.FollowFirstLinkAsync(html, options.Url, httpClient);
        }

        // Process CSS using HTML-Renderer
        ProcessCss(html);

        // Execute inline scripts using Broiler.JavaScript
        ExecuteScripts(html);

        // Save the captured content
        if (options.OutputFormat == OutputFormat.Text)
        {
            var text = Regex.Replace(html, @"<[^>]+>", string.Empty);
            await File.WriteAllTextAsync(options.OutputPath, text);
        }
        else
        {
            await File.WriteAllTextAsync(options.OutputPath, html);
        }
    }

    /// <summary>
    /// Parses CSS blocks from the HTML using HTML-Renderer's core library.
    /// This exercises the HTML-Renderer engine as part of the rendering pipeline;
    /// parsed CSS data can be extended in future to influence output formatting.
    /// </summary>
    private static void ProcessCss(string html)
    {
        foreach (Match match in StylePattern.Matches(html))
        {
            var cssContent = match.Groups["content"].Value.Trim();
            if (!string.IsNullOrEmpty(cssContent))
            {
                // Parse CSS properties using HTML-Renderer's CssBlock
                var properties = new Dictionary<string, string>();
                foreach (var declaration in cssContent.Split(';'))
                {
                    var colonIdx = declaration.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        var prop = declaration[..colonIdx].Trim();
                        var val = declaration[(colonIdx + 1)..].Trim();
                        if (!string.IsNullOrEmpty(prop))
                            properties[prop] = val;
                    }
                }

                if (properties.Count > 0)
                {
                    _ = new CssBlock("style", properties);
                }
            }
        }
    }

    /// <summary>
    /// Captures website content from the specified URL, renders it as an image
    /// using HtmlRenderer.Image, and saves the result to the output path.
    /// </summary>
    /// <param name="options">Image capture configuration options.</param>
    /// <returns>A task that completes when the image capture is finished.</returns>
    /// <exception cref="HttpRequestException">Thrown when the URL cannot be fetched.</exception>
    /// <exception cref="IOException">Thrown when the output file cannot be written.</exception>
    public async Task CaptureImageAsync(ImageCaptureOptions options)
    {
        try
        {
            var outputDir = Path.GetDirectoryName(Path.GetFullPath(options.OutputPath));
            if (outputDir != null && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PathTooLongException)
        {
            throw new IOException($"Cannot create output directory: {ex.Message}", ex);
        }

        string html;
        var uri = new Uri(options.Url);
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        };

        if (uri.IsFile)
        {
            html = await File.ReadAllTextAsync(uri.LocalPath);
        }
        else
        {
            html = await httpClient.GetStringAsync(uri);
        }

        // Follow the first link if requested (e.g. Acid2 landing page navigation).
        if (options.FollowFirstLink)
        {
            html = await LinkNavigator.FollowFirstLinkAsync(html, options.Url, httpClient);
        }

        // Execute inline scripts via DomBridge so JS-generated content
        // is present before rendering.
        html = ExecuteScriptsWithDom(html, options.Url);

        // Apply the shared post-processing pipeline (strip scripts,
        // data-URI backgrounds, iframe/object fallback content, and
        // hidden test artifacts) so that HtmlRenderer produces clean output.
        html = HtmlPostProcessor.Process(html);

        var format = options.ImageFormat == ImageFormat.Jpeg
            ? BImageFormat.Jpeg
            : BImageFormat.Png;

        // Extract fragment identifier (e.g. "#top") for anchor-based rendering.
        string? fragment = uri.Fragment;
        if (!string.IsNullOrEmpty(fragment) && fragment.StartsWith('#') && fragment.Length > 1)
        {
            string elementId = fragment[1..]; // strip leading '#'
            RenderAtAnchor(html, elementId, options, format);
        }
        else if (options.FullPage)
        {
            HtmlRender.RenderToFileAutoSized(html, options.OutputPath,
                maxWidth: options.Width, maxHeight: options.Height,
                format: format, quality: 90);
        }
        else
        {
            HtmlRender.RenderToFile(html, options.Width, options.Height, options.OutputPath, format, baseUrl:uri.ToString());
        }
    }

    /// <summary>
    /// Renders the HTML scrolled to the element with the given <paramref name="elementId"/>,
    /// producing a viewport-sized image of the content visible at that anchor.
    /// This mirrors the approach used by the Acid2 differential test suite:
    /// layout with a tall viewport, locate the anchor via
    /// <see cref="HtmlContainer.GetElementRectangle"/>, then render the
    /// viewport-sized region starting at the anchor's Y position.
    /// </summary>
    private static void RenderAtAnchor(string html, string elementId, ImageCaptureOptions options,
        BImageFormat format)
    {
        using var bitmap = HtmlRender.RenderToImageAtAnchor(
            html,
            elementId,
            options.Width,
            options.Height,
            baseUrl: options.Url);

        if (bitmap is null)
        {
            HtmlRender.RenderToFile(
                html,
                options.Width,
                options.Height,
                options.OutputPath,
                format,
                baseUrl: options.Url);
            return;
        }

        bitmap.Save(options.OutputPath, format, 90);
    }

    /// <summary>
    /// Extracts and executes inline scripts using Broiler.JavaScript.
    /// This exercises the YantraJS engine as part of the rendering pipeline;
    /// script results can be extended in future to influence output content.
    /// </summary>
    private static void ExecuteScripts(string html)
    {
        var scripts = new List<string>();
        foreach (Match match in ScriptPattern.Matches(html))
        {
            var content = match.Groups["content"].Value.Trim();
            if (!string.IsNullOrEmpty(content))
            {
                scripts.Add(content);
            }
        }

        if (scripts.Count > 0)
        {
            using var context = new JSContext();
            RegisterWindowStub(context);

            foreach (var script in scripts)
            {
                try
                {
                    context.Eval(script);
                }
                catch (Exception ex)
                {
                    // Script execution errors are non-fatal for capture
                    RenderLogger.LogError(LogCategory.JavaScript, "CaptureService.ExecuteScripts", $"Script execution error: {ex.Message}", ex);
                }
            }
        }
    }

    /// <summary>
    /// Executes inline, <c>data:</c> URI, and external <c>src</c> scripts using
    /// <see cref="DomBridge"/> so that JavaScript-generated DOM content is included
    /// in the rendered output. Returns the serialised HTML after script execution.
    /// Scripts with <c>defer</c> are executed after all regular scripts;
    /// <c>async</c> scripts execute alongside regular scripts but are not
    /// guaranteed order. All pending timers and rAF callbacks are flushed
    /// before serialisation.
    /// </summary>
    internal static string ExecuteScriptsWithDom(string html, string url, string? localResourceBasePath = null)
    {
        var scripts = new List<string>();
        var deferredScripts = new List<string>();
        var csp = ContentSecurityPolicy.FromHtml(html);
        foreach (Match match in AnyScriptPattern.Matches(html))
        {
            var attrs = match.Groups["attrs"].Value;
            var isDefer = DeferAttrPattern.IsMatch(attrs);
            var nonce = ContentSecurityPolicy.ExtractNonceFromAttributes(attrs);
            string? scriptContent = null;

            var srcMatch = SrcAttrPattern.Match(attrs);
            if (srcMatch.Success)
            {
                if (csp != null && !csp.AllowsExternalScript(srcMatch.Groups["uri"].Value, url, nonce))
                    continue;

                // data: URI script
                var decoded = DecodeDataUri(srcMatch.Groups["uri"].Value);
                if (!string.IsNullOrEmpty(decoded))
                    scriptContent = decoded;
            }
            else
            {
                // Check for any src= attribute (http/https/file/relative)
                var anySrcMatch = AnySrcAttrPattern.Match(attrs);
                if (anySrcMatch.Success)
                {
                    var srcUri = anySrcMatch.Groups["uri"].Value;
                    if (csp != null && !csp.AllowsExternalScript(srcUri, url, nonce))
                        continue;

                    var fetched = FetchExternalScript(srcUri, url);
                    if (!string.IsNullOrEmpty(fetched))
                        scriptContent = fetched;
                }
                else
                {
                    // Inline script
                    var content = match.Groups["content"].Value.Trim();
                    if (!string.IsNullOrEmpty(content) && (csp == null || csp.AllowsInlineScript(nonce, content)))
                        scriptContent = content;
                }
            }

            if (scriptContent == null) continue;

            if (isDefer)
                deferredScripts.Add(scriptContent);
            else
                scripts.Add(scriptContent);
        }

        if (scripts.Count == 0 && deferredScripts.Count == 0)
            return html;

        var microTasks = new MicroTaskQueue();
        using var context = new JSContext();
        RegisterRuntimeExtensions(context, microTasks, csp);
        var bridge = new DomBridge();
        bridge.Csp = csp;
        bridge.TaskCheckpointCallback = () => microTasks.Drain();
        bridge.Attach(context, html, url);

        // Set local base path for sub-resource resolution (e.g. iframe src)
        if (!string.IsNullOrEmpty(localResourceBasePath))
            bridge.SetLocalBasePath(localResourceBasePath);

        // Execute regular and async scripts in document order.
        // Track the corresponding <script> DOM element index so that
        // document.write() can insert content at the correct position.
        var scriptElements = bridge.Elements
            .Select((el, idx) => (el, idx))
            .Where(t => string.Equals(t.el.TagName, "script", StringComparison.OrdinalIgnoreCase))
            .ToList();

        static void DrainAsyncWork(DomBridge bridge, MicroTaskQueue microTasks)
        {
            for (var iteration = 0; iteration < DomBridge.AsyncDrainIterationLimit; iteration++)
            {
                var hadWork = false;

                if (microTasks.Count > 0)
                {
                    microTasks.Drain();
                    hadWork = true;
                }

                if (bridge.HasPendingTimers)
                {
                    bridge.FlushTimerStep();
                    hadWork = true;
                }

                if (!hadWork)
                    break;
            }
        }

        for (int si = 0; si < scripts.Count; si++)
        {
            if (si < scriptElements.Count)
                bridge.CurrentScriptIndex = scriptElements[si].idx;
            try
            {
                context.Eval(scripts[si]);
                DrainAsyncWork(bridge, microTasks);
            }
            catch (Exception ex)
            {
                // Script execution errors are non-fatal for capture
                RenderLogger.LogError(LogCategory.JavaScript, "CaptureService.ExecuteScriptsWithDom", $"Script execution error: {ex.Message}", ex);
            }
        }
        bridge.CurrentScriptIndex = -1;

        // Execute deferred scripts after all regular scripts (simulates end-of-parsing)
        foreach (var script in deferredScripts)
        {
            try
            {
                context.Eval(script);
                DrainAsyncWork(bridge, microTasks);
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, "CaptureService.ExecuteScriptsWithDom", $"Deferred script error: {ex.Message}", ex);
            }
        }

        // Fire body onload event after all scripts have executed
        // (simulates end-of-parsing / window load in browsers).
        // This is critical for test harnesses like Acid3 that bootstrap
        // the test runner via <body onload="update()">.
        bridge.FireWindowLoadEvent();

        // Drain queued microtasks and timer work before capture.
        DrainAsyncWork(bridge, microTasks);
        bridge.ResolveAnimationSnapshots();

        return bridge.SerializeToHtml();
    }

    /// <summary>
    /// Resolves and downloads an external script from an HTTP/HTTPS/file URL.
    /// Relative URLs are resolved against the page <paramref name="pageUrl"/>.
    /// Returns the script text content, or <c>string.Empty</c> on failure.
    /// </summary>
    internal static string FetchExternalScript(string scriptUrl, string pageUrl)
    {
        try
        {
            // Resolve relative URLs against the page URL
            string resolvedUrl;
            if (Uri.TryCreate(scriptUrl, UriKind.Absolute, out _))
            {
                resolvedUrl = scriptUrl;
            }
            else if (Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri) &&
                     Uri.TryCreate(baseUri, scriptUrl, out var resolved))
            {
                resolvedUrl = resolved.AbsoluteUri;
            }
            else
            {
                return string.Empty;
            }

            // Handle file:// URLs — read from local filesystem
            if (resolvedUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(resolvedUrl);
                var path = uri.LocalPath;
                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }

            using var response = SharedHttpClient.GetAsync(resolvedUrl).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                return string.Empty;

            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.JavaScript, "CaptureService.FetchExternalScript",
                $"Failed to fetch external script '{scriptUrl}': {ex.Message}", ex);
            return string.Empty;
        }
    }

    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>
    /// Removes all <c>&lt;script&gt;</c> tags from the HTML.
    /// Delegates to <see cref="HtmlPostProcessor.StripScriptTags"/>.
    /// </summary>
    internal static string StripScriptTags(string html)
    {
        return HtmlPostProcessor.StripScriptTags(html);
    }

    /// <summary>
    /// Strips CSS <c>background</c> declarations that reference
    /// <c>data:</c> URI images.
    /// Delegates to <see cref="HtmlPostProcessor.StripCssDataUriBackgrounds"/>.
    /// </summary>
    internal static string StripCssDataUriBackgrounds(string html)
    {
        return HtmlPostProcessor.StripCssDataUriBackgrounds(html);
    }

    /// <summary>
    /// Replaces the fallback content of every <c>&lt;iframe&gt;</c> element
    /// with an empty body.
    /// Delegates to <see cref="HtmlPostProcessor.StripIframeContent"/>.
    /// </summary>
    internal static string StripIframeContent(string html)
    {
        return HtmlPostProcessor.StripIframeContent(html);
    }

    /// <summary>
    /// Replaces the fallback content of every <c>&lt;object&gt;</c> element
    /// with an empty body.
    /// Delegates to <see cref="HtmlPostProcessor.StripObjectContent"/>.
    /// </summary>
    internal static string StripObjectContent(string html)
    {
        return HtmlPostProcessor.StripObjectContent(html);
    }

    /// <summary>
    /// Strips test-harness elements whose text content should be invisible
    /// according to CSS but that HtmlRenderer renders visibly.
    /// Delegates to <see cref="HtmlPostProcessor.StripHiddenTestArtifacts"/>.
    /// </summary>
    internal static string StripHiddenTestArtifacts(string html)
    {
        return HtmlPostProcessor.StripHiddenTestArtifacts(html);
    }

    /// <summary>
    /// Strips all <c>&lt;table&gt;…&lt;/table&gt;</c> elements.
    /// Intended for Acid3-specific post-processing only — structural tables
    /// in other pages (e.g. Acid2) must be preserved.
    /// Delegates to <see cref="HtmlPostProcessor.StripTables"/>.
    /// </summary>
    internal static string StripTables(string html)
    {
        return HtmlPostProcessor.StripTables(html);
    }

    /// <summary>
    /// Decodes a <c>data:</c> URI to its text content.
    /// Supports <c>data:text/javascript,...</c> (percent-encoded) and
    /// <c>data:text/javascript;base64,...</c> formats.
    /// </summary>
    internal static string DecodeDataUri(string dataUri)
    {
        if (!dataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var rest = dataUri[5..]; // strip "data:"
        var commaIdx = rest.IndexOf(',');
        if (commaIdx < 0)
            return string.Empty;

        var meta = rest[..commaIdx];
        var payload = rest[(commaIdx + 1)..];

        if (meta.Contains("base64", StringComparison.OrdinalIgnoreCase))
        {
            // Percent-decode first (some Acid3 data URIs percent-encode the base64)
            var decoded = Uri.UnescapeDataString(payload);
            // Strip whitespace (RFC 2045 allows folding)
            decoded = WhitespacePattern.Replace(decoded, string.Empty);
            try
            {
                var bytes = Convert.FromBase64String(decoded);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return string.Empty;
            }
        }
        else
        {
            return Uri.UnescapeDataString(payload);
        }
    }

    /// <summary>
    /// Registers minimal <c>window</c> and <c>document</c> global stubs on the
    /// given <see cref="JSContext"/> so that typical page scripts (e.g. those
    /// accessing <c>window.localStorage</c> or <c>window.matchMedia</c>) do
    /// not throw.
    /// </summary>
    internal static void RegisterWindowStub(JSContext context)
    {
        // document stub with documentElement.classList
        var document = new JSObject();

        var docElement = new JSObject();
        var classList = new JSObject();
        var classes = new List<string>();

        classList.FastAddValue(
            (KeyString)"add",
            new JSFunction((in Arguments a) =>
            {
                for (var i = 0; i < a.Length; i++)
                {
                    var cls = a[i]?.ToString() ?? string.Empty;
                    if (!classes.Contains(cls)) classes.Add(cls);
                }
                return JSUndefined.Value;
            }, "add"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        classList.FastAddValue(
            (KeyString)"remove",
            new JSFunction((in Arguments a) =>
            {
                for (var i = 0; i < a.Length; i++)
                    classes.Remove(a[i]?.ToString() ?? string.Empty);
                return JSUndefined.Value;
            }, "remove"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        classList.FastAddValue(
            (KeyString)"contains",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSBoolean.False;
                return classes.Contains(a[0]?.ToString() ?? string.Empty) ? JSBoolean.True : JSBoolean.False;
            }, "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        docElement.FastAddValue(
            (KeyString)"classList",
            classList,
            JSPropertyAttributes.EnumerableConfigurableValue);

        document.FastAddValue(
            (KeyString)"documentElement",
            docElement,
            JSPropertyAttributes.EnumerableConfigurableValue);

        context["document"] = document;

        // window stub with localStorage and matchMedia
        var window = new JSObject();
        window.FastAddValue(
            (KeyString)"document",
            document,
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.localStorage — in-memory stub
        var storage = new JSObject();
        var store = new Dictionary<string, string>();

        storage.FastAddValue(
            (KeyString)"getItem",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length == 0) return JSNull.Value;
                var key = a[0]?.ToString() ?? string.Empty;
                return store.TryGetValue(key, out var val) ? new JSString(val) : JSNull.Value;
            }, "getItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        storage.FastAddValue(
            (KeyString)"setItem",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length >= 2)
                {
                    var key = a[0]?.ToString() ?? string.Empty;
                    var val = a[1]?.ToString() ?? string.Empty;
                    store[key] = val;
                    storage[(KeyString)key] = new JSString(val);
                }
                return JSUndefined.Value;
            }, "setItem", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        storage.FastAddValue(
            (KeyString)"removeItem",
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0)
                {
                    var key = a[0]?.ToString() ?? string.Empty;
                    store.Remove(key);
                    storage.Delete((KeyString)key);
                }
                return JSUndefined.Value;
            }, "removeItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

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

        window.FastAddValue(
            (KeyString)"localStorage",
            storage,
            JSPropertyAttributes.EnumerableConfigurableValue);

        // window.matchMedia(query) — stub returning { matches: false }
        window.FastAddValue(
            (KeyString)"matchMedia",
            new JSFunction((in Arguments a) =>
            {
                var result = new JSObject();
                result.FastAddValue(
                    (KeyString)"matches",
                    JSBoolean.False,
                    JSPropertyAttributes.EnumerableConfigurableValue);
                result.FastAddValue(
                    (KeyString)"media",
                    a.Length > 0 ? new JSString(a[0]?.ToString() ?? string.Empty) : new JSString(string.Empty),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                return result;
            }, "matchMedia", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        context["window"] = window;
    }

    /// <summary>
    /// Registers runtime polyfills (<c>queueMicrotask</c>, <c>WeakRef</c>,
    /// <c>FinalizationRegistry</c>) on the <see cref="JSContext"/> so that
    /// the CLI script-execution environment matches the App's
    /// <see cref="ScriptEngine.RegisterRuntimeExtensions"/> setup.
    /// </summary>
    private static void RegisterRuntimeExtensions(JSContext context, MicroTaskQueue microTasks, ContentSecurityPolicy? csp = null)
    {
        // queueMicrotask(fn) — queue callback for the next microtask checkpoint
        context["queueMicrotask"] = new JSFunction((in Arguments a) =>
        {
            if (a.Length > 0 && a[0] is JSFunction fn)
            {
                microTasks.Enqueue(() =>
                {
                    try { fn.InvokeFunction(new Arguments(JSUndefined.Value)); }
                    catch (Exception ex)
                    {
                        RenderLogger.LogError(LogCategory.JavaScript, "CaptureService.queueMicrotask",
                            $"Callback error: {ex.Message}", ex);
                    }
                });
            }
            return JSUndefined.Value;
        }, "queueMicrotask", 1);

        if (csp != null && !csp.AllowsEval)
        {
            context["eval"] = new JSFunction((in Arguments _) =>
            {
                throw new InvalidOperationException(
                    "Refused to evaluate a string as JavaScript because 'unsafe-eval' is not an allowed source in the Content Security Policy.");
            }, "eval", 1);
        }

        // WeakRef polyfill
        RegisterWeakRefPolyfill(context);

        // FinalizationRegistry polyfill
        RegisterFinalizationRegistryPolyfill(context);
    }

    /// <summary>
    /// Registers a minimal <c>WeakRef</c> constructor backed by
    /// <see cref="WeakReference{T}"/>.
    /// </summary>
    private static void RegisterWeakRefPolyfill(JSContext context)
    {
        try
        {
            var existing = context.Eval("typeof WeakRef");
            if (existing is JSString s && s.ToString() != "undefined")
                return;
        }
        catch { /* not present — install polyfill */ }

        var weakRefCtor = new JSFunction((in Arguments args) =>
        {
            if (args.Length == 0)
                throw new InvalidOperationException("WeakRef requires a target object.");

            var target = args[0];
            var weakRef = new WeakReference<JSValue>(target);

            var instance = new JSObject();
            instance.FastAddValue((KeyString)"deref", new JSFunction((in Arguments _) =>
            {
                return weakRef.TryGetTarget(out var t) ? t : JSUndefined.Value;
            }, "deref", 0), JSPropertyAttributes.EnumerableConfigurableValue);

            return instance;
        }, "WeakRef", 1);

        context["WeakRef"] = weakRefCtor;
    }

    /// <summary>
    /// Registers a minimal <c>FinalizationRegistry</c> constructor.
    /// </summary>
    private static void RegisterFinalizationRegistryPolyfill(JSContext context)
    {
        try
        {
            var existing = context.Eval("typeof FinalizationRegistry");
            if (existing is JSString s && s.ToString() != "undefined")
                return;
        }
        catch { /* not present — install polyfill */ }

        var registryCtor = new JSFunction((in Arguments args) =>
        {
            var instance = new JSObject();

            instance.FastAddValue((KeyString)"register", new JSFunction((in Arguments _) =>
            {
                return JSUndefined.Value;
            }, "register", 3), JSPropertyAttributes.EnumerableConfigurableValue);

            instance.FastAddValue((KeyString)"unregister", new JSFunction((in Arguments _) =>
            {
                return JSBoolean.False;
            }, "unregister", 1), JSPropertyAttributes.EnumerableConfigurableValue);

            return instance;
        }, "FinalizationRegistry", 1);

        context["FinalizationRegistry"] = registryCtor;
    }
}
