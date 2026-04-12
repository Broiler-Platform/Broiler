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
        SKColor backgroundColor = default,
        CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null,
        EventHandler<HtmlImageLoadEventArgs> imageLoad = null,
        string baseUrl = null)
    {
        var bgColor = backgroundColor == default ? SKColors.White : backgroundColor;

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

            if (backgroundColor == default)
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

    public static SKBitmap RenderToImageAutoSized(string html, int maxWidth = 0, int maxHeight = 0,
        SKColor backgroundColor = default,
        CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null,
        EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
    {
        if (string.IsNullOrEmpty(html))
            return new SKBitmap(1, 1);

        var bgColor = backgroundColor == default ? SKColors.White : backgroundColor;

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;

        if (stylesheetLoad != null)
            container.StylesheetLoad += stylesheetLoad;
        if (imageLoad != null)
            container.ImageLoad += imageLoad;

        container.SetHtml(html, cssData);

        if (backgroundColor == default)
            bgColor = ResolveCanvasBackground(container, bgColor);

        var minSize = new SizeF(0, 0);
        var maxSize = new SizeF(maxWidth, maxHeight);
        var finalSize = MeasureHtml(container, minSize, maxSize);

        // Ensure minimum dimensions
        int w = Math.Max(1, (int)Math.Ceiling(finalSize.Width));
        int h = Math.Max(1, (int)Math.Ceiling(finalSize.Height));

        // Apply max width limit
        if (maxWidth < 1 && w > 4096)
            w = 4096;

        // Clamp to max height when a viewport height constraint is specified
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

    public static byte[] RenderToPng(string html, int width, int height,
        SKColor backgroundColor = default,
        CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null,
        EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
    {
        using var bitmap = RenderToImage(html, width, height, backgroundColor, cssData, stylesheetLoad, imageLoad);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
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
