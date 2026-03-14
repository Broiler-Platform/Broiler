using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TheArtOfDev.HtmlRenderer.Core;
using TheArtOfDev.HtmlRenderer.Core.Entities;
using TheArtOfDev.HtmlRenderer.Avalonia.Adapters;
using TheArtOfDev.HtmlRenderer.Avalonia.Utilities;

namespace TheArtOfDev.HtmlRenderer.Avalonia;

public static class HtmlRender
{
    public static void AddFontFamily(global::Avalonia.Media.FontFamily fontFamily)
    {
        ArgumentNullException.ThrowIfNull(fontFamily);

        AvaloniaAdapter.Instance.AddFontFamily(new FontFamilyAdapter(fontFamily));
    }

    public static void AddFontFamilyMapping(string fromFamily, string toFamily)
    {
        ArgumentException.ThrowIfNullOrEmpty(fromFamily);
        ArgumentException.ThrowIfNullOrEmpty(toFamily);

        AvaloniaAdapter.Instance.AddFontFamilyMapping(fromFamily, toFamily);
    }

    public static CssData ParseStyleSheet(string stylesheet, bool combineWithDefault = true) =>
        CssDataParser.Parse(AvaloniaAdapter.Instance, stylesheet, combineWithDefault ? AvaloniaAdapter.Instance.DefaultCssData : null);

    public static Size Measure(string html, double maxWidth = 0, CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null, EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
    {
        Size actualSize = default;
        if (string.IsNullOrEmpty(html))
            return actualSize;

        using var container = new HtmlContainer();
        container.MaxSize = new Size(maxWidth, 0);
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;

        if (stylesheetLoad != null)
            container.StylesheetLoad += stylesheetLoad;
        if (imageLoad != null)
            container.ImageLoad += imageLoad;

        container.SetHtml(html, cssData);
        container.PerformLayout();

        actualSize = container.ActualSize;
        return actualSize;
    }

    public static Size Render(DrawingContext g, string html, double left = 0, double top = 0, double maxWidth = 0, CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null, EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
    {
        ArgumentNullException.ThrowIfNull(g);
        return RenderClip(g, html, new Point(left, top), new Size(maxWidth, 0), cssData, stylesheetLoad, imageLoad);
    }

    public static Size Render(DrawingContext g, string html, Point location, Size maxSize, CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null, EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
    {
        ArgumentNullException.ThrowIfNull(g);
        return RenderClip(g, html, location, maxSize, cssData, stylesheetLoad, imageLoad);
    }

    public static RenderTargetBitmap RenderToImage(string html, Size size, CssData cssData = null,
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null, EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
    {
        var renderTarget = new RenderTargetBitmap(new PixelSize((int)size.Width, (int)size.Height), new Vector(96, 96));

        if (string.IsNullOrEmpty(html))
            return renderTarget;

        using (var g = renderTarget.CreateDrawingContext())
        {
            RenderHtml(g, html, new Point(), size, cssData, stylesheetLoad, imageLoad);
        }

        return renderTarget;
    }

    private static Size MeasureHtmlByRestrictions(HtmlContainer htmlContainer, Size minSize, Size maxSize)
    {
        using var mg = new GraphicsAdapter();
        var sizeInt = HtmlRendererUtils.MeasureHtmlByRestrictions(mg, htmlContainer.HtmlContainerInt, Utils.Convert(minSize), Utils.Convert(maxSize));

        if (maxSize.Width < 1 && sizeInt.Width > 4096)
            sizeInt.Width = 4096;

        return Utils.ConvertRound(sizeInt);
    }

    private static Size RenderClip(DrawingContext g, string html, Point location, Size maxSize, CssData cssData, EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad, EventHandler<HtmlImageLoadEventArgs> imageLoad)
    {
        Size actualSize;
        if (maxSize.Height > 0)
        {
            using (g.PushClip(new Rect(location, maxSize)))
            {
                actualSize = RenderHtml(g, html, location, maxSize, cssData, stylesheetLoad, imageLoad);
            }
        }
        else
        {
            actualSize = RenderHtml(g, html, location, maxSize, cssData, stylesheetLoad, imageLoad);
        }

        return actualSize;
    }

    private static Size RenderHtml(DrawingContext g, string html, Point location, Size maxSize, CssData cssData, EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad, EventHandler<HtmlImageLoadEventArgs> imageLoad)
    {
        Size actualSize = default;

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
        container.PerformLayout();
        container.PerformPaint(g, new Rect(0, 0, double.MaxValue, double.MaxValue));

        actualSize = container.ActualSize;

        return actualSize;
    }
}
