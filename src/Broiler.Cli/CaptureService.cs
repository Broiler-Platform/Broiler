using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Broiler.HTML.Image;
using Broiler.HtmlBridge.Core.Diagnostics;
using Broiler.Layout.Net;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;
#if BROILER_JS_REJECTION_TRACKING
using Broiler.JavaScript.BuiltIns.Promise;
#endif
using Broiler.CSS;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Logging;
using Broiler.HtmlBridge.Scripting;
using Broiler.HtmlBridge.Internal.Scripting;
using Broiler.Media.Image;
using Broiler.HtmlBridge.Dom;

namespace Broiler.Cli;

// Disambiguate the unqualified `Regex` type: the Broiler.JS engine now exposes a top-level
// `Broiler.Regex` namespace which, from this `Broiler.*` namespace, otherwise shadows
// System.Text.RegularExpressions.Regex by simple-name lookup (CS0118). The alias must sit
// inside the Broiler.Cli namespace scope so it is resolved before the enclosing `Broiler`
// namespace's `Regex` member. This file uses only the .NET Regex.
using Regex = System.Text.RegularExpressions.Regex;

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
    /// <summary>
    /// Matches all &lt;script&gt; tags (both inline and with src attributes)
    /// in document order. The tag attributes and body content are captured
    /// so the caller can determine the script type.
    /// </summary>
    private static readonly Regex AnyScriptPattern = new(
        @"<script(?<attrs>[^>]*)>(?<content>[\s\S]*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches <c>&lt;noscript …&gt;…&lt;/noscript&gt;</c> blocks, so the scripts inside one can be
    /// skipped.
    /// </summary>
    private static readonly Regex NoscriptBlockPattern = new(
        @"<noscript[^>]*>[\s\S]*?</noscript>",
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
    /// An attribute value read straight out of the source text, decoded the way the tokenizer
    /// would have decoded it (WHATWG §13.2.5, the attribute-value character-reference states).
    /// </summary>
    /// <remarks>
    /// This loop matches attributes with a regex over the raw markup, which skips the tokenizer
    /// and therefore its decoding step — so a <c>src</c> was requested exactly as spelled. An
    /// ampersand in a query string is spelled <c>&amp;amp;</c> in conforming HTML, so the request
    /// went out with the entity intact and the server answered about a query it did not have:
    /// <c>www.mediawiki.org</c>'s ResourceLoader bootstrap
    /// (<c>load.php?lang=en&amp;amp;modules=startup&amp;amp;…</c>) came back 404, the script was
    /// dropped for want of content, and with it every module the page loads through
    /// <c>mw.loader</c> — the whole skin's JavaScript, from one unresolved entity.
    /// </remarks>
    private static string DecodeAttributeValue(string value) =>
        value.IndexOf('&') < 0 ? value : System.Net.WebUtility.HtmlDecode(value);

    /// <summary>
    /// Matches the <c>defer</c> attribute on a script tag (standalone or with a value).
    /// </summary>
    private static readonly Regex DeferAttrPattern = new(
        @"(?:^|\s)defer(?:\s|$|=)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches <c>type="module"</c> on a script tag (Phase 7 tail: capture runs modules through the engine
    /// module path when available, instead of dropping them as classic-script syntax errors).
    /// </summary>
    private static readonly Regex TypeModuleAttrPattern = new(
        @"\stype\s*=\s*(?:""\s*module\s*""|'\s*module\s*'|module(?:\s|$))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Captures the value of a script tag's <c>type</c> attribute, so a data block — JSON-LD,
    /// framework state, speculation rules, an import map, a client-side template — can be told
    /// from a script and skipped rather than compiled. See <see cref="ScriptMimeType"/>.
    /// </summary>
    private static readonly Regex TypeAttrPattern = new(
        @"\stype\s*=\s*(?:""(?<type>[^""]*)""|'(?<type>[^']*)'|(?<type>[^\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StylePattern = new(
        @"<style[^>]*>(?<content>[\s\S]*?)</style>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);


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

        using var httpClient = CreateHttpClient(options.TimeoutSeconds);

        var html = await FetchDocumentAsync(httpClient, new Uri(options.Url));

        // Follow the first link if requested (e.g. Acid2 landing page navigation).
        if (options.FollowFirstLink)
        {
            html = await LinkNavigator.FollowFirstLinkAsync(html, options.Url, httpClient);
        }

        // Process CSS using HTML-Renderer
        ProcessCss(html);

        // Execute inline scripts using Broiler.JavaScript
        ExecuteScripts(html, options.Url);

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
    /// The client both capture entry points fetch the document (and any followed link) on.
    /// </summary>
    /// <remarks>
    /// It carries a <c>User-Agent</c>, which <see cref="HttpClient"/> otherwise omits entirely. A
    /// server is free to refuse an unidentified request outright rather than serve it differently,
    /// and Wikimedia's User-Agent policy does exactly that: <c>https://www.mediawiki.org/wiki/MediaWiki</c>
    /// answered every capture <c>403 Forbidden</c> — before a redirect, before any content
    /// negotiation — for want of this one header. See <see cref="BroilerUserAgent"/>.
    /// </remarks>
    private static HttpClient CreateHttpClient(int timeoutSeconds) =>
        BroilerUserAgent.Apply(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
        });

    /// <summary>
    /// Fetches the top-level document, recording it when a diagnostic run is listening. The page
    /// itself is the one resource nothing below the CLI ever sees — the bridge is handed HTML, not a
    /// URL — so it can only be archived from here.
    /// </summary>
    private static async Task<string> FetchDocumentAsync(HttpClient httpClient, Uri uri)
    {
        var attempt = ResourceTrace.Begin(ResourceTraceKind.Document, uri.AbsoluteUri);
        try
        {
            var html = await httpClient.GetStringAsync(uri);
            attempt.Completed(html);
            return html;
        }
        catch (Exception ex)
        {
            attempt.Failed(ex);
            throw;
        }
    }

    /// <summary>
    /// Parses CSS declarations from the HTML using the shared CSS kernel.
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
                _ = new CssParser().ParseDeclarations(cssContent);
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
        using var httpClient = CreateHttpClient(options.TimeoutSeconds);

        if (uri.IsFile)
        {
            var attempt = ResourceTrace.Begin(ResourceTraceKind.Document, uri.AbsoluteUri);
            try
            {
                html = await File.ReadAllTextAsync(uri.LocalPath);
                attempt.Completed(html);
            }
            catch (Exception ex)
            {
                attempt.Failed(ex);
                throw;
            }
        }
        else
        {
            html = await FetchDocumentAsync(httpClient, uri);
        }

        // Follow the first link if requested (e.g. Acid2 landing page navigation).
        if (options.FollowFirstLink)
        {
            html = await LinkNavigator.FollowFirstLinkAsync(html, options.Url, httpClient);
        }

        // Execute inline scripts via DomBridge so JS-generated content
        // is present before rendering.
        html = ExecuteScriptsWithDom(html, options.Url);

        // Apply the production render-preparation pipeline (strip already-executed scripts, empty
        // iframe fallback, box replaced video/progress/meter/select-multiple, :root->html) so that
        // HtmlRenderer produces clean output. This is the browsing profile: it deliberately does NOT
        // apply the Acid/WPT test-harness artifact cleanup (which strips valid content like <map>).
        html = HtmlPostProcessor.ProcessForBrowsing(html);

        var format = options.ImageFormat == ImageFormat.Jpeg
            ? ImageEncodeFormat.Jpeg
            : ImageEncodeFormat.Png;

        // Extract fragment identifier (e.g. "#top") for anchor-based rendering.
        string? fragment = uri.Fragment;
        if (!string.IsNullOrEmpty(fragment) && fragment.StartsWith('#') && fragment.Length > 1)
        {
            string elementId = fragment[1..]; // strip leading '#'
            RenderAtAnchor(html, elementId, options, format);
        }
        else if (options.FullPage)
        {
            HtmlRender.RenderToFileAutoSizedWithStyleSet(html, options.OutputPath,
                maxWidth: options.Width, maxHeight: options.Height,
                format: format, quality: 90);
        }
        else
        {
            HtmlRender.RenderToFileWithStyleSet(
                html, options.Width, options.Height, options.OutputPath, format, baseUrl: uri.ToString());
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
        ImageEncodeFormat format)
    {
        using var bitmap = HtmlRender.RenderToImageAtAnchorWithStyleSet(
            html,
            elementId,
            options.Width,
            options.Height,
            baseUrl: options.Url);

        if (bitmap is null)
        {
            HtmlRender.RenderToFileWithStyleSet(
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
    /// The source spans covered by <c>&lt;noscript&gt;</c> blocks.
    /// </summary>
    /// <remarks>
    /// A <c>noscript</c> body is what a browser shows when it is <em>not</em> running scripts, so
    /// with scripting enabled it is inert and a <c>&lt;script&gt;</c> written inside one must never
    /// execute. The HTML parser gets this right on its own — it takes a <c>noscript</c> body as raw
    /// text, so no script element ever reaches the DOM — but the two hosts below do not go through
    /// it: they extract scripts with their own regex pass over the source, and a regex for
    /// <c>&lt;script&gt;</c> matches one nested inside a <c>noscript</c> just as happily. Hence the
    /// explicit skip. A host moved onto the tokenizer-backed <c>HtmlScriptScanner</c> — which
    /// <c>ScriptExtractionService</c> already uses — would not need it.
    /// </remarks>
    private static List<(int Start, int End)> NoscriptSpans(string html)
    {
        var spans = new List<(int Start, int End)>();
        foreach (Match match in NoscriptBlockPattern.Matches(html))
            spans.Add((match.Index, match.Index + match.Length));
        return spans;
    }

    /// <summary>
    /// Whether <paramref name="index"/> falls inside one of <paramref name="spans"/>.
    /// </summary>
    private static bool IsInsideNoscript(List<(int Start, int End)> spans, int index)
    {
        foreach (var span in spans)
        {
            if (index >= span.Start && index < span.End)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts and executes inline scripts using Broiler.JavaScript.
    /// This exercises the YantraJS engine as part of the rendering pipeline;
    /// script results can be extended in future to influence output content.
    /// </summary>
    private static void ExecuteScripts(string html, string url)
    {
        var scripts = new List<string>();
        var noscriptSpans = NoscriptSpans(html);
        foreach (Match match in AnyScriptPattern.Matches(html))
        {
            if (IsInsideNoscript(noscriptSpans, match.Index))
                continue;

            var attrs = match.Groups["attrs"].Value;

            // This path evaluates only what the element itself carries, so a script with a `src`
            // has nothing here to run.
            if (AnySrcAttrPattern.IsMatch(attrs))
                continue;

            // A `<script>` whose type is neither a JavaScript MIME essence nor `module` is a data
            // block the browser never executes — the DOM path below has skipped those since
            // ScriptMimeType was introduced, and this one had not, so a capture written as HTML
            // reported a compile error for content that was never JavaScript. The first
            // `<script>` on `about.google` is a JSON-LD block, and it failed as
            // "Unexpected token Colon: : at 1, 12".
            var typeMatch = TypeAttrPattern.Match(attrs);
            if (!ScriptMimeType.IsExecutable(typeMatch.Success ? typeMatch.Groups["type"].Value : null))
                continue;

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

            for (var i = 0; i < scripts.Count; i++)
            {
                // The same labels the DOM path uses, so an error and its archived source name the
                // script identically whichever capture mode produced them.
                var label = ScriptLabel.Inline(i);
                if (ResourceTrace.IsActive)
                    RecordExecutedScript(url, label, scripts[i]);

                try
                {
                    context.Eval(scripts[i], label);
                }
                catch (Exception ex)
                {
                    // Script execution errors are non-fatal for capture
                    RenderLogger.LogError(LogCategory.JavaScript, "CaptureService.ExecuteScripts", $"Script {label} failed: {ex.Message}", ex);
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
    /// <summary>
    /// Starts the document's speculative preload scan (multithreading roadmap item #17) and returns
    /// the prefetcher its worker fills, or <c>null</c> when no scan ran.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This host walks the script tags itself and fetched each external one <em>inline as it
    /// reached it</em> — the serial round trips item #2 exists to remove, in a call site item #2
    /// never reached, because the split it built lives in <c>ScriptExtractionService.ExtractAll</c>
    /// and capture does its own extraction. The scan is what supplies the URL set early enough to
    /// fix that: the whole set from one pass of the source, on a worker, before the loop below has
    /// looked at the first tag.
    /// </para>
    /// <para>
    /// <b>The prefetch key is derived exactly as the consume site derives it</b> — the raw
    /// attribute value resolved against the page URL by the same
    /// <see cref="UrlResolver"/> call <c>FetchExternalScript</c> makes. The scan also computes a
    /// resolution of its own, against the document's <c>&lt;base href&gt;</c>, which is the more
    /// correct answer and the wrong one to use here: a prefetch stored under a key the consumer
    /// never asks for is not a saved round trip, it is a second one. Whether script <c>src</c>
    /// should honour <c>&lt;base&gt;</c> is a separate question about the consume site.
    /// </para>
    /// <para>
    /// Nothing is lost if the worker is slower than this thread: <c>Consume</c> starts the fetch
    /// itself when the scan has not reached that URL yet, and the two collapse onto one request.
    /// </para>
    /// </remarks>
    private static SubResourcePrefetcher? StartScriptPrefetch(string html, string url, ContentSecurityPolicy? csp)
    {
        var prefetcher = new SubResourcePrefetcher(
            resolved => ScriptExtractionService.FetchExternalScript(resolved, url));

        var scan = SpeculativePreloadScan.Start(
            html,
            url,
            csp,
            result => prefetcher.Prefetch(
                result.RawUrls(PreloadKind.Script)
                    .Select(raw => UrlResolver.Resolve(raw, url)?.AbsoluteUri)
                    .OfType<string>()));

        return scan is null ? null : prefetcher;
    }

    internal static string ExecuteScriptsWithDom(string html, string url, string? localResourceBasePath = null)
    {
        var scripts = new List<string>();
        var deferredScripts = new List<string>();
        var moduleRoots = new List<string>();
        var csp = ContentSecurityPolicy.FromHtml(html);
        var scriptPrefetcher = StartScriptPrefetch(html, url, csp);
        var noscriptSpans = NoscriptSpans(html);
        foreach (Match match in AnyScriptPattern.Matches(html))
        {
            if (IsInsideNoscript(noscriptSpans, match.Index))
                continue;

            var attrs = match.Groups["attrs"].Value;

            // A `<script>` whose type is neither a JavaScript MIME essence nor `module` is a data
            // block the browser never executes. Compiling one produced a syntax error about
            // content that was never JavaScript — a `text/template` opening with a bullet reported
            // as "Unexpected token Empty:  at 1, 1".
            var typeMatch = TypeAttrPattern.Match(attrs);
            if (!ScriptMimeType.IsExecutable(typeMatch.Success ? typeMatch.Groups["type"].Value : null))
                continue;

            var isDefer = DeferAttrPattern.IsMatch(attrs);
            var isModule = TypeModuleAttrPattern.IsMatch(attrs);
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
                    var srcUri = DecodeAttributeValue(anySrcMatch.Groups["uri"].Value);
                    if (csp != null && !csp.AllowsExternalScript(srcUri, url, nonce))
                        continue;

                    // Consume side of item #2's split. Identical resolution and identical result;
                    // the difference is that the preload scan has had this request in flight since
                    // before the loop started, instead of it beginning here, one script at a time.
                    var fetched = ScriptExtractionService.FetchExternalScript(srcUri, url, scriptPrefetcher);
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

            // <script type="module"> is deferred by definition; route it to the engine module path
            // (run after the classic deferred scripts) rather than the classic buckets.
            if (isModule)
                moduleRoots.Add(scriptContent);
            else if (isDefer)
                deferredScripts.Add(scriptContent);
            else
                scripts.Add(scriptContent);
        }

        // Archive the program text of everything that is about to run, under the labels the error log
        // names it by, so "Script inline-7 failed" points at a file. External sources were already
        // recorded as fetched; the sink stores identical bytes once, so the second entry costs a
        // manifest row and yields the label→URL mapping. Off by default; see ResourceTrace.
        if (ResourceTrace.IsActive)
        {
            for (var i = 0; i < scripts.Count; i++)
                RecordExecutedScript(url, ScriptLabel.Inline(i), scripts[i]);
            for (var i = 0; i < deferredScripts.Count; i++)
                RecordExecutedScript(url, ScriptLabel.Deferred(i), deferredScripts[i]);
            for (var i = 0; i < moduleRoots.Count; i++)
                RecordExecutedScript(url, ScriptLabel.Module(i.ToString(CultureInfo.InvariantCulture)), moduleRoots[i]);
        }

        // No scripts to run: normally the raw HTML passes through untouched, but
        // a CSP style-src policy that blocks inline styles still has to be applied
        // to the rendered output, so fall through to build and re-serialize the DOM.
        if (scripts.Count == 0 && deferredScripts.Count == 0 && moduleRoots.Count == 0 &&
            (csp == null || !csp.AffectsStyles()))
            return html;

        // Run modules through the engine's own module machinery when it binds imports (as ScriptEngine
        // does). Capture had no module support before — a type="module" script was eval'd as a classic
        // script and threw — so this is additive; when the engine can't bind imports the context stays a
        // plain JSContext and the modules are skipped (unchanged net effect: they did not execute).
        var useEngineModules = moduleRoots.Count > 0 && EngineModuleSupport.Available;
        var microTasks = new MicroTaskQueue();
        // See MicroTaskSynchronizationContext: without this the engine resumes await continuations
        // on the thread pool, concurrently with this thread's serialize/render.
        using var microTaskContext = MicroTaskSynchronizationContext.Install(microTasks);
        using JSContext context = useEngineModules ? new BridgeModuleContext(csp, url) : new JSContext();
        RegisterRuntimeExtensions(context, microTasks, csp);
        // Multithreading roadmap item #16, and the earliest point it can start: the last of these
        // sources was unknown until the external fetches above returned, so there is no version of
        // this that precedes the context. What it overlaps with is everything between here and the
        // eval loop — the parse and DOM build in Attach — and then, once the loop is running, the
        // execution of each script overlaps the compile of the ones behind it. Ordering is
        // untouched: the loop below still evaluates the same sources in the same order on this
        // thread, and finds the compile already done instead of doing it inline. Disposed (joined)
        // before the context, so no compile outlives the cache it writes into.
        // Each unit carries the label the eval below will pass, because the location is part of the
        // code-cache key: compiling under one shared location while evaluating under per-script ones
        // would file every entry where the eval does not look, and the overlap would be lost with no
        // symptom other than being slower.
        using var compileAhead = ScriptCompileAhead.Start(
            context,
            [
                .. scripts.Select((s, i) => new ScriptCompileUnit(s, ScriptLabel.Inline(i))),
                .. deferredScripts.Select((s, i) => new ScriptCompileUnit(s, ScriptLabel.Deferred(i))),
            ]);
        // Disposed with this call, and declared after the context so it is torn down first: the
        // bridge owns a per-document session — the headless layout view and its HtmlContainer
        // (hence the whole box tree and every sub-resource its image load handlers hold), timer
        // and animation queues, listener stores, observers and message ports. Only the serialized
        // HTML string outlives this scope, so there is nothing to keep the session alive for.
        using var bridge = new DomBridge();
        bridge.Csp = csp;
        bridge.TaskCheckpointCallback = () => microTasks.Drain();
        // Attach enforces the CSP style-src family on the parsed DOM itself (it runs
        // when bridge.Csp is set), so blocked inline style attributes / <style> elements
        // neither apply nor render — no separate ApplyStyleContentSecurityPolicy call needed.
        bridge.Attach(context, html, url);

        // Set local base path for sub-resource resolution (e.g. iframe src)
        if (!string.IsNullOrEmpty(localResourceBasePath))
            bridge.SetLocalBasePath(localResourceBasePath);

        // Execute regular and async scripts in document order.
        // Track the corresponding <script> DOM element index so that document.currentScript names
        // the running script and document.write() inserts at its position. Only the elements each
        // bucket actually runs are counted: pairing the buckets against every <script> in the tree
        // attributed the n-th executed script to the n-th element, which on any document carrying a
        // data block before a script — a JSON-LD block, an import map — is a different element
        // (ScriptElementMap).
        var scriptElements = ScriptElementMap.Classic(bridge.Elements);
        var deferredScriptElements = ScriptElementMap.Deferred(bridge.Elements);

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

                if (bridge.HasPendingTimersDueBy(DomBridge.AsyncDrainVirtualTimeBudgetMs))
                {
                    bridge.FlushTimerStep();
                    hadWork = true;
                }

                if (!hadWork)
                    return; // settled — nothing left that this capture's window covers
            }

            // Phase 8 item 3: the iteration budget is exhausted while work is *still due now*. The
            // virtual-time horizon above already retires the ordinary case — a page holding an
            // interval, whose next tick is simply later — so reaching here means work that keeps
            // regenerating at the current instant and never lets the clock move: a callback
            // rescheduling itself with no delay, or a microtask queueing another. That is worth a
            // warning; an interval is not, and used to get one.
            RenderLogger.LogWarning(LogCategory.JavaScript, "CaptureService.DrainAsyncWork",
                $"Async work still due after {DomBridge.AsyncDrainIterationLimit} drain iterations; " +
                $"stopping with pending microtasks={microTasks.Count}. " +
                "A callback is rescheduling itself with no delay, so the virtual clock cannot advance.");
        }

        for (int si = 0; si < scripts.Count; si++)
        {
            bridge.CurrentScriptIndex = si < scriptElements.Count ? scriptElements[si] : -1;
            var label = ScriptLabel.Inline(si);
            try
            {
                context.Eval(scripts[si], label);
                // Event-loop ordering (EL-3): only a microtask checkpoint between synchronous scripts; timers
                // are deferred to the post-load drain below, so they fire (in deadline order) after all script
                // execution rather than eagerly between scripts.
                microTasks.Drain();
            }
            catch (Exception ex)
            {
                // Script execution errors are non-fatal for capture. The label names which script:
                // this used to report only "Script execution error", which on a document with a dozen
                // scripts said nothing about which one had stopped running.
                RenderLogger.LogError(LogCategory.JavaScript, "CaptureService.ExecuteScriptsWithDom", $"Script {label} failed: {ex.Message}", ex);
            }
        }
        bridge.CurrentScriptIndex = -1;

        // Execute deferred scripts after all regular scripts (simulates end-of-parsing)
        for (var di = 0; di < deferredScripts.Count; di++)
        {
            // A deferred script is as much the running script as a non-deferred one; this bucket
            // never set the index, so document.currentScript was null and document.write appended
            // to <body> for the whole of it.
            bridge.CurrentScriptIndex = di < deferredScriptElements.Count ? deferredScriptElements[di] : -1;
            var label = ScriptLabel.Deferred(di);
            try
            {
                context.Eval(deferredScripts[di], label);
                microTasks.Drain(); // microtask checkpoint only; timers deferred to the post-load drain (EL-3)
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, "CaptureService.ExecuteScriptsWithDom", $"Script {label} failed: {ex.Message}", ex);
            }
        }
        bridge.CurrentScriptIndex = -1;

        // Execute ES modules (deferred by definition) last, through the engine module machinery on the
        // BridgeModuleContext the DOM is attached to. Only when the engine binds imports (see the context
        // setup above); otherwise moduleRoots is left unrun. The page URL is the module base for resolving
        // relative imports; the key is made unique per root within this capture.
        if (context is BridgeModuleContext moduleContext)
        {
            for (var mi = 0; mi < moduleRoots.Count; mi++)
            {
                try
                {
                    moduleContext.RunScriptAsync(moduleRoots[mi], url, uniqueModuleID: $"{url}#capture-module-{mi}")
                        .GetAwaiter().GetResult();
                    microTasks.Drain(); // microtask checkpoint only; timers deferred to the post-load drain (EL-3)
                }
                catch (Exception ex)
                {
                    RenderLogger.LogError(LogCategory.JavaScript, "CaptureService.ExecuteScriptsWithDom", $"Module script error: {ex.Message}", ex);
                }
            }
        }

        // Fire body onload event after all scripts have executed
        // (simulates end-of-parsing / window load in browsers).
        // This is critical for test harnesses like Acid3 that bootstrap
        // the test runner via <body onload="update()">.
        bridge.FireWindowLoadEvent();

        // Drain queued microtasks and timer work before capture.
        DrainAsyncWork(bridge, microTasks);
        ReportUnhandledRejections();
        bridge.ResolveAnimationSnapshots();

        var serialized = bridge.SerializeToHtml();
        // The DOM as the scripts left it. Diffed against the fetched document — archived beside it —
        // this is precisely what the page's JavaScript did or failed to do, which no other artefact
        // of the run shows.
        ResourceTrace.RecordBody(ResourceTraceKind.Document, url, serialized, DiagnosticSession.AfterScriptsLabel);
        return serialized;
    }

    /// <summary>
    /// Logs the promises that were rejected with nothing to handle them — a browser's
    /// <c>Uncaught (in promise)</c>.
    /// </summary>
    /// <remarks>
    /// Called after the async drain, not before: until the microtask checkpoint has run to
    /// quiescence a rejection may still be claimed, and reporting earlier would call a handled
    /// rejection unhandled. Empty unless a diagnostic run turned the engine's tracker on, so an
    /// ordinary capture pays a single lock and an empty list.
    /// </remarks>
    private static void ReportUnhandledRejections()
    {
#if BROILER_JS_REJECTION_TRACKING
        foreach (var rejection in JSPromiseRejectionTracker.TakePending())
        {
            RenderLogger.Log(
                LogCategory.JavaScript,
                LogLevel.Error,
                "CaptureService.UnhandledRejection",
                $"Unhandled promise rejection: {rejection.Describe()}");
        }
#endif
    }

    /// <summary>
    /// Archives one script as executed. <c>Url</c> carries the page and the label so an entry is
    /// self-describing in the manifest even when the body is shared with an external fetch.
    /// </summary>
    private static void RecordExecutedScript(string pageUrl, string label, string source) =>
        ResourceTrace.RecordBody(ResourceTraceKind.ExecutedScript, $"{pageUrl}#{label}", source, label);

    /// <summary>
    /// Resolves and downloads an external script from an HTTP/HTTPS/file URL.
    /// Relative URLs are resolved against the page <paramref name="pageUrl"/>.
    /// Returns the script text content, or <c>string.Empty</c> on failure.
    /// </summary>
    /// <remarks>
    /// Phase 7 item 4: delegates to the single <see cref="ScriptExtractionService.FetchExternalScript"/>
    /// implementation rather than duplicating the resolve + file/http switch (and a second
    /// <c>HttpClient</c>) here; the empty-on-failure contract this method's callers/tests expect is
    /// preserved by mapping the service's <c>null</c> to <see cref="string.Empty"/>.
    /// </remarks>
    internal static string FetchExternalScript(string scriptUrl, string pageUrl) =>
        ScriptExtractionService.FetchExternalScript(scriptUrl, pageUrl) ?? string.Empty;

    /// <summary>
    /// Removes all <c>&lt;script&gt;</c> tags from the HTML.
    /// Delegates to <see cref="HtmlPostProcessor.StripScriptTags"/>.
    /// </summary>
    internal static string StripScriptTags(string html)
    {
        return HtmlPostProcessor.StripScriptTags(html);
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
    /// Decodes a <c>data:</c> URI to its text content.
    /// Supports <c>data:text/javascript,...</c> (percent-encoded) and
    /// <c>data:text/javascript;base64,...</c> formats.
    /// </summary>
    /// <remarks>
    /// Phase 7 item 4: delegates to the single <see cref="ScriptExtractionService.DecodeDataUri"/>
    /// implementation rather than keeping a byte-identical copy here.
    /// </remarks>
    internal static string DecodeDataUri(string dataUri) => ScriptExtractionService.DecodeDataUri(dataUri);

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
