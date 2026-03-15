using System;
using System.IO;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core;
using TheArtOfDev.HtmlRenderer.Core.Entities;
using TheArtOfDev.HtmlRenderer.Image.Adapters;
using System.Drawing;

namespace TheArtOfDev.HtmlRenderer.Image;

public static class HtmlRender
{
    public static SKBitmap RenderToImage(string html, int width, int height,
        SKColor backgroundColor = default,
        CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null,
        EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);

        var bgColor = backgroundColor == default ? SKColors.White : backgroundColor;
        canvas.Clear(bgColor);

        if (!string.IsNullOrEmpty(html))
            RenderHtml(canvas, html, new PointF(0, 0), new SizeF(width, height), cssData, stylesheetLoad, imageLoad);

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

        var minSize = new SizeF(0, 0);
        var maxSize = new SizeF(maxWidth, maxHeight);
        var finalSize = MeasureHtml(container, minSize, maxSize);

        // Ensure minimum dimensions
        int w = Math.Max(1, (int)Math.Ceiling(finalSize.Width));
        int h = Math.Max(1, (int)Math.Ceiling(finalSize.Height));

        // Apply max width limit
        if (maxWidth < 1 && w > 4096)
            w = 4096;

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

    public static byte[] RenderToJpeg(string html, int width, int height,
        int quality = 90,
        SKColor backgroundColor = default,
        CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null,
        EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
    {
        using var bitmap = RenderToImage(html, width, height, backgroundColor, cssData, stylesheetLoad, imageLoad);
        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data.ToArray();
    }

    public static void RenderToFile(string html, int width, int height, string filePath,
        SKEncodedImageFormat format = SKEncodedImageFormat.Png,
        int quality = 90,
        SKColor backgroundColor = default,
        CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null,
        EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        using var bitmap = RenderToImage(html, width, height, backgroundColor, cssData, stylesheetLoad, imageLoad);
        using var data = bitmap.Encode(format, quality);
        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);
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

    private static SizeF RenderHtml(SKCanvas canvas, string html, PointF location, SizeF maxSize,
        CssData cssData,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad,
        EventHandler<HtmlImageLoadEventArgs> imageLoad)
    {
        SizeF actualSize = new(0, 0);

        if (string.IsNullOrEmpty(html))
            return actualSize;

        using var container = new HtmlContainer();
        container.Location = location;
        container.MaxSize = maxSize;
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;

        if (stylesheetLoad != null)
            container.StylesheetLoad += stylesheetLoad;
        if (imageLoad != null)
            container.ImageLoad += imageLoad;

        container.SetHtml(html, cssData);

        var clip = new RectangleF(location.X, location.Y, maxSize.Width, maxSize.Height);
        container.PerformLayout(canvas, clip);
        container.PerformPaint(canvas, clip);

        actualSize = container.ActualSize;

        return actualSize;
    }
}
