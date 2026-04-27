using System;
using System.IO;
using SkiaSharp;
using System.Drawing;
using Broiler.HTML.Core.Core.Entities;
using Broiler.HTML.Core.Core;
using Broiler.HTML.Orchestration.Core;
using Broiler.HTML.Image.Adapters;

namespace Broiler.HTML.Image;

public static class HtmlRender
{
    public static SKBitmap RenderToImage(string html, int width, int height,
        BColor backgroundColor,
        CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null,
        EventHandler<HtmlImageLoadEventArgs> imageLoad = null,
        string baseUrl = null) =>
        RenderToImageCore(html, width, height, backgroundColor, cssData, stylesheetLoad, imageLoad, baseUrl);

    public static SKBitmap RenderToImage(string html, int width, int height,
        SKColor backgroundColor = default,
        CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null,
        EventHandler<HtmlImageLoadEventArgs> imageLoad = null,
        string baseUrl = null) =>
        RenderToImageCore(html, width, height, backgroundColor == default ? null : backgroundColor.ToBColor(), cssData, stylesheetLoad, imageLoad, baseUrl);

    public static SKBitmap RenderToImageAutoSized(string html, int maxWidth, int maxHeight,
        BColor backgroundColor,
        CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null,
        EventHandler<HtmlImageLoadEventArgs> imageLoad = null) =>
        RenderToImageAutoSizedCore(html, maxWidth, maxHeight, backgroundColor, cssData, stylesheetLoad, imageLoad);

    public static SKBitmap RenderToImageAutoSized(string html, int maxWidth = 0, int maxHeight = 0,
        SKColor backgroundColor = default,
        CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null,
        EventHandler<HtmlImageLoadEventArgs> imageLoad = null) =>
        RenderToImageAutoSizedCore(html, maxWidth, maxHeight, backgroundColor == default ? null : backgroundColor.ToBColor(), cssData, stylesheetLoad, imageLoad);

    public static byte[] RenderToPng(string html, int width, int height,
        BColor backgroundColor,
        CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null,
        EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
        => RenderToPngCore(html, width, height, backgroundColor, cssData, stylesheetLoad, imageLoad);

    public static byte[] RenderToPng(string html, int width, int height,
        SKColor backgroundColor = default,
        CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null,
        EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
        => RenderToPngCore(html, width, height, backgroundColor == default ? null : backgroundColor.ToBColor(), cssData, stylesheetLoad, imageLoad);

    public static void RenderToFile(string html, int width, int height, string filePath,
        BImageFormat format,
        int quality = 90,
        BColor backgroundColor = default,
        CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null,
        EventHandler<HtmlImageLoadEventArgs> imageLoad = null, string baseUrl = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        using var bitmap = backgroundColor == default
            ? RenderToImage(html, width, height, cssData: cssData, stylesheetLoad: stylesheetLoad, imageLoad: imageLoad, baseUrl: baseUrl)
            : RenderToImage(html, width, height, backgroundColor, cssData, stylesheetLoad, imageLoad, baseUrl);
        using var data = bitmap.Encode(format.ToSkEncodedImageFormat(), quality);
        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);
    }

    public static void RenderToFile(string html, int width, int height, string filePath,
        SKEncodedImageFormat format = SKEncodedImageFormat.Png,
        int quality = 90,
        SKColor backgroundColor = default,
        CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null,
        EventHandler<HtmlImageLoadEventArgs> imageLoad = null, string baseUrl = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        using var bitmap = RenderToImage(html, width, height, backgroundColor, cssData, stylesheetLoad, imageLoad, baseUrl:baseUrl);
        using var data = bitmap.Encode(format, quality);
        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);
    }

    public static void RenderToFileAutoSized(string html, string filePath,
        int maxWidth = 0,
        int maxHeight = 0,
        BImageFormat format = BImageFormat.Png,
        int quality = 90,
        BColor backgroundColor = default,
        CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null,
        EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        using var bitmap = backgroundColor == default
            ? RenderToImageAutoSized(html, maxWidth, maxHeight, cssData: cssData, stylesheetLoad: stylesheetLoad, imageLoad: imageLoad)
            : RenderToImageAutoSized(html, maxWidth, maxHeight, backgroundColor, cssData, stylesheetLoad, imageLoad);
        using var data = bitmap.Encode(format.ToSkEncodedImageFormat(), quality);
        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);
    }

    /// <summary>
    /// Loads a TrueType/OpenType font from a file and registers it with
    /// the rendering adapter so that CSS <c>font-family</c> references
    /// using <paramref name="cssName"/> resolve to it.
    /// </summary>
    /// <param name="path">Absolute path to a .ttf or .otf font file.</param>
    /// <param name="cssName">
    /// Optional CSS family name alias.  When provided, the font will be
    /// accessible under this name in addition to its own family name
    /// (e.g. pass <c>"Ahem"</c> for the WPT Ahem test font).
    /// </param>
    /// <returns>
    /// The font's own family name, or <c>null</c> if loading failed.
    /// </returns>
    public static string LoadFontFromFile(string path, string cssName = null)
        => SkiaImageAdapter.Instance.LoadFontFromFile(path, cssName);

    private static SKBitmap RenderToImageCore(string html, int width, int height,
        BColor? backgroundColor,
        CssData cssData,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad,
        EventHandler<HtmlImageLoadEventArgs> imageLoad,
        string baseUrl)
    {
        var bgColor = backgroundColor?.ToSkColor() ?? SKColors.White;

        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);

        if (!string.IsNullOrEmpty(html))
        {
            using var container = new HtmlContainer();
            container.Location = new PointF(0, 0);
            container.MaxSize = new SizeF(width, height);
            container.AvoidAsyncImagesLoading = true;
            container.AvoidImagesLateLoading = true;

            if (stylesheetLoad != null)
                container.StylesheetLoad += stylesheetLoad;
            if (imageLoad != null)
                container.ImageLoad += imageLoad;

            container.SetHtml(html, cssData, baseUrl);

            if (backgroundColor is null)
                bgColor = ResolveCanvasBackground(container, bgColor);

            canvas.Clear(bgColor);

            var clip = new RectangleF(0, 0, width, height);
            container.PerformLayout(canvas, clip);
            container.PerformPaint(canvas, clip);
        }
        else
        {
            canvas.Clear(bgColor);
        }

        return bitmap;
    }

    private static SKBitmap RenderToImageAutoSizedCore(string html, int maxWidth, int maxHeight,
        BColor? backgroundColor,
        CssData cssData,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad,
        EventHandler<HtmlImageLoadEventArgs> imageLoad)
    {
        if (string.IsNullOrEmpty(html))
            return new SKBitmap(1, 1);

        var bgColor = backgroundColor?.ToSkColor() ?? SKColors.White;

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;

        if (stylesheetLoad != null)
            container.StylesheetLoad += stylesheetLoad;
        if (imageLoad != null)
            container.ImageLoad += imageLoad;

        container.SetHtml(html, cssData);

        if (backgroundColor is null)
            bgColor = ResolveCanvasBackground(container, bgColor);

        var minSize = new SizeF(0, 0);
        var maxSize = new SizeF(maxWidth, maxHeight);
        var finalSize = MeasureHtml(container, minSize, maxSize);

        int w = Math.Max(1, (int)Math.Ceiling(finalSize.Width));
        int h = Math.Max(1, (int)Math.Ceiling(finalSize.Height));

        if (maxWidth < 1 && w > 4096)
            w = 4096;

        if (maxHeight > 0 && h > maxHeight)
            h = maxHeight;

        container.MaxSize = new SizeF(w, h);

        var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(bgColor);

        var clip = new RectangleF(0, 0, w, h);
        container.PerformPaint(canvas, clip);

        return bitmap;
    }

    private static byte[] RenderToPngCore(string html, int width, int height,
        BColor? backgroundColor,
        CssData cssData,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad,
        EventHandler<HtmlImageLoadEventArgs> imageLoad)
    {
        using var bitmap = RenderToImageCore(html, width, height, backgroundColor, cssData, stylesheetLoad, imageLoad, null);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static SizeF MeasureHtml(HtmlContainer container, SizeF minSize, SizeF maxSize)
    {
        // Create a small temporary surface for measurement
        using var measureBitmap = new SKBitmap(1, 1);
        using var measureCanvas = new SKCanvas(measureBitmap);
        var clip = new RectangleF(0, 0, 99999, 99999);

        using var g = new GraphicsAdapter(measureCanvas, clip);
        return HtmlRendererUtils.MeasureHtmlByRestrictions(g, container.HtmlContainerInt, minSize, maxSize);
    }

    /// <summary>
    /// Reads the root element's computed background color from the container
    /// and converts it to an <see cref="SKColor"/>.  Returns the supplied
    /// <paramref name="fallback"/> when the root has no explicit background.
    /// </summary>
    private static SKColor ResolveCanvasBackground(HtmlContainer container, SKColor fallback)
    {
        var rootBg = container.GetRootBackgroundColor();
        if (!rootBg.IsEmpty && rootBg.A > 0)
            return new SKColor(rootBg.R, rootBg.G, rootBg.B, rootBg.A);

        return fallback;
    }
}
