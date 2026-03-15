using System;
using System.Collections.Generic;
using SkiaSharp;
using System.Drawing;
using Broiler.HTML.Adapters;
using Broiler.HTML.Orchestration.Core;
using Broiler.HTML.Core.Core.Entities;
using Broiler.HTML.Core.Core.IR;
using Broiler.HTML.Core.Core;
using Broiler.HTML.Image.Adapters;

namespace Broiler.HTML.Image;

public sealed class HtmlContainer : IDisposable
{
    public HtmlContainer()
    {
        HtmlContainerInt = new HtmlContainerInt(SkiaImageAdapter.Instance, HandlerFactory.Instance);
        HtmlContainerInt.SetMargins(0);
        HtmlContainerInt.PageSize = new SizeF(99999, 99999);
    }

    public event EventHandler LoadComplete
    {
        add { HtmlContainerInt.LoadComplete += value; }
        remove { HtmlContainerInt.LoadComplete -= value; }
    }

    public event EventHandler<HtmlRenderErrorEventArgs> RenderError
    {
        add { HtmlContainerInt.RenderError += value; }
        remove { HtmlContainerInt.RenderError -= value; }
    }

    public event EventHandler<HtmlStylesheetLoadEventArgs> StylesheetLoad
    {
        add { HtmlContainerInt.StylesheetLoad += value; }
        remove { HtmlContainerInt.StylesheetLoad -= value; }
    }

    public event EventHandler<HtmlImageLoadEventArgs> ImageLoad
    {
        add { HtmlContainerInt.ImageLoad += value; }
        remove { HtmlContainerInt.ImageLoad -= value; }
    }

    internal HtmlContainerInt HtmlContainerInt { get; }

    /// <summary>
    /// The most recent <see cref="Fragment"/> tree built after layout.
    /// Available after <see cref="PerformLayout"/> has been called.
    /// </summary>
    public Fragment? LatestFragmentTree => HtmlContainerInt.LatestFragmentTree;

    public CssData CssData => HtmlContainerInt.CssData;

    public bool AvoidAsyncImagesLoading
    {
        get => HtmlContainerInt.AvoidAsyncImagesLoading;
        set => HtmlContainerInt.AvoidAsyncImagesLoading = value;
    }

    public bool AvoidImagesLateLoading
    {
        get => HtmlContainerInt.AvoidImagesLateLoading;
        set => HtmlContainerInt.AvoidImagesLateLoading = value;
    }

    public SizeF MaxSize
    {
        get => HtmlContainerInt.MaxSize;
        set => HtmlContainerInt.MaxSize = value;
    }

    public SizeF ActualSize
    {
        get => HtmlContainerInt.ActualSize;
        internal set => HtmlContainerInt.ActualSize = value;
    }

    public PointF Location
    {
        get => HtmlContainerInt.Location;
        set => HtmlContainerInt.Location = value;
    }

    public void SetHtml(string htmlSource, CssData baseCssData = null) => HtmlContainerInt.SetHtml(htmlSource, baseCssData);

    public void PerformLayout(SKCanvas canvas, RectangleF clip)
    {
        using var g = new GraphicsAdapter(canvas, clip);
        HtmlContainerInt.PerformLayout(g);
    }

    public void PerformPaint(SKCanvas canvas, RectangleF clip)
    {
        using var g = new GraphicsAdapter(canvas, clip);
        HtmlContainerInt.PerformPaint(g);
    }

    /// <summary>
    /// Returns the bounding rectangle of the element with the specified <paramref name="elementId"/>,
    /// or <c>null</c> if no such element exists.  Useful for scrolling to an anchor target
    /// (e.g.&nbsp;<c>#top</c>).
    /// Requires <see cref="SetHtml"/> and <see cref="PerformLayout"/> to have been called first.
    /// </summary>
    public RectangleF? GetElementRectangle(string elementId) => HtmlContainerInt.GetElementRectangle(elementId);

    /// <summary>
    /// Returns all links found in the parsed HTML document.
    /// Requires <see cref="SetHtml"/> to have been called first.
    /// Each link includes its <c>id</c>, <c>href</c>, and bounding rectangle.
    /// </summary>
    public List<LinkElementData<RectangleF>> GetLinks() => HtmlContainerInt.GetLinks();

    public void Dispose() => HtmlContainerInt.Dispose();
}
