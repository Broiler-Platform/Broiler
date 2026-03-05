using System.Text.RegularExpressions;
using Broiler.App.Rendering;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.Entities;
using TheArtOfDev.HtmlRenderer.Image;
using YantraJS.Core;

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

        // Execute inline scripts using YantraJS
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

        var format = options.ImageFormat == ImageFormat.Jpeg
            ? SKEncodedImageFormat.Jpeg
            : SKEncodedImageFormat.Png;

        // Extract fragment identifier (e.g. "#top") for anchor-based rendering.
        string? fragment = uri.Fragment;
        if (!string.IsNullOrEmpty(fragment) && fragment.StartsWith('#') && fragment.Length > 1)
        {
            string elementId = fragment[1..]; // strip leading '#'
            RenderAtAnchor(html, elementId, options, format);
        }
        else if (options.FullPage)
        {
            using var bitmap = HtmlRender.RenderToImageAutoSized(html, maxWidth: options.Width);
            using var data = bitmap.Encode(format, 90);
            using var stream = File.OpenWrite(options.OutputPath);
            data.SaveTo(stream);
        }
        else
        {
            HtmlRender.RenderToFile(html, options.Width, options.Height, options.OutputPath, format);
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
        SKEncodedImageFormat format)
    {
        // Maximum layout height used for initial full-page measurement.
        const int LayoutMaxHeight = 99999;
        // Bitmap height for the initial layout pass (must be large enough to
        // contain the visible content but does not need to match LayoutMaxHeight).
        const int LayoutBitmapHeight = 2000;

        int w = options.Width, h = options.Height;

        // 1. Layout with a tall viewport so the full page is measured.
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new System.Drawing.SizeF(w, LayoutMaxHeight);
        container.SetHtml(html);

        using var layoutBmp = new SKBitmap(w, LayoutBitmapHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var layoutCanvas = new SKCanvas(layoutBmp);
        layoutCanvas.Clear(SKColors.White);
        container.PerformLayout(layoutCanvas, new System.Drawing.RectangleF(0, 0, w, LayoutMaxHeight));

        // 2. Find the anchor element position.
        var anchorRect = container.GetElementRectangle(elementId);
        float scrollY = anchorRect?.Y ?? 0;

        // 3. Constrain viewport and set Location to scroll position.
        container.Location = new System.Drawing.PointF(0, scrollY);
        container.MaxSize = new System.Drawing.SizeF(w, h);

        // 4. Render with canvas translation to map scroll region to bitmap.
        using var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Save();
        canvas.Translate(0, -scrollY);
        container.PerformPaint(canvas, new System.Drawing.RectangleF(0, scrollY, w, h));
        canvas.Restore();

        using var data = bitmap.Encode(format, 90);
        using var stream = File.OpenWrite(options.OutputPath);
        data.SaveTo(stream);
    }

    /// <summary>
    /// Extracts and executes inline scripts using YantraJS.
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
                return store.TryGetValue(key, out var val) ? (JSValue)new JSString(val) : JSNull.Value;
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
                    a.Length > 0 ? (JSValue)new JSString(a[0]?.ToString() ?? string.Empty) : new JSString(string.Empty),
                    JSPropertyAttributes.EnumerableConfigurableValue);
                return result;
            }, "matchMedia", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        context["window"] = window;
    }
}
