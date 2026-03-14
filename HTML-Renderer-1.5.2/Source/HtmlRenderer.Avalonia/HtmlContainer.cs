using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Adapters.Entities;
using TheArtOfDev.HtmlRenderer.Core;
using TheArtOfDev.HtmlRenderer.Core.Entities;
using TheArtOfDev.HtmlRenderer.Avalonia.Adapters;
using TheArtOfDev.HtmlRenderer.Avalonia.Utilities;
using SizeF = System.Drawing.SizeF;

namespace TheArtOfDev.HtmlRenderer.Avalonia;

public sealed class HtmlContainer : IDisposable
{
    public HtmlContainer()
    {
        HtmlContainerInt = new HtmlContainerInt(AvaloniaAdapter.Instance, HandlerFactory.Instance);
        HtmlContainerInt.PageSize = new SizeF(99999, 99999);
    }

    public event EventHandler LoadComplete
    {
        add { HtmlContainerInt.LoadComplete += value; }
        remove { HtmlContainerInt.LoadComplete -= value; }
    }

    public event EventHandler<HtmlLinkClickedEventArgs> LinkClicked
    {
        add { HtmlContainerInt.LinkClicked += value; }
        remove { HtmlContainerInt.LinkClicked -= value; }
    }

    public event EventHandler<HtmlRefreshEventArgs> Refresh
    {
        add { HtmlContainerInt.Refresh += value; }
        remove { HtmlContainerInt.Refresh -= value; }
    }

    public event EventHandler<HtmlScrollEventArgs> ScrollChange
    {
        add { HtmlContainerInt.ScrollChange += value; }
        remove { HtmlContainerInt.ScrollChange -= value; }
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

    public CssData CssData => HtmlContainerInt.CssData;

    public bool AvoidAsyncImagesLoading
    {
        get { return HtmlContainerInt.AvoidAsyncImagesLoading; }
        set { HtmlContainerInt.AvoidAsyncImagesLoading = value; }
    }

    public bool AvoidImagesLateLoading
    {
        get { return HtmlContainerInt.AvoidImagesLateLoading; }
        set { HtmlContainerInt.AvoidImagesLateLoading = value; }
    }

    public bool IsSelectionEnabled
    {
        get { return HtmlContainerInt.IsSelectionEnabled; }
        set { HtmlContainerInt.IsSelectionEnabled = value; }
    }

    public bool IsContextMenuEnabled
    {
        get { return HtmlContainerInt.IsContextMenuEnabled; }
        set { HtmlContainerInt.IsContextMenuEnabled = value; }
    }

    public Point ScrollOffset
    {
        get { return Utils.Convert(HtmlContainerInt.ScrollOffset); }
        set { HtmlContainerInt.ScrollOffset = Utils.Convert(value); }
    }

    public Point Location
    {
        get { return Utils.Convert(HtmlContainerInt.Location); }
        set { HtmlContainerInt.Location = Utils.Convert(value); }
    }

    public Size MaxSize
    {
        get { return Utils.Convert(HtmlContainerInt.MaxSize); }
        set { HtmlContainerInt.MaxSize = Utils.Convert(value); }
    }

    public Size ActualSize
    {
        get { return Utils.Convert(HtmlContainerInt.ActualSize); }
        internal set { HtmlContainerInt.ActualSize = Utils.Convert(value); }
    }

    public string SelectedText => HtmlContainerInt.SelectedText;

    public string SelectedHtml => HtmlContainerInt.SelectedHtml;

    public void ClearSelection() => HtmlContainerInt.ClearSelection();

    public void SetHtml(string htmlSource, CssData baseCssData = null) => HtmlContainerInt.SetHtml(htmlSource, baseCssData);
    public void Clear() => HtmlContainerInt.Clear();
    public string GetHtml(HtmlGenerationStyle styleGen = HtmlGenerationStyle.Inline) => HtmlContainerInt.GetHtml(styleGen);
    public string GetAttributeAt(Point location, string attribute) => HtmlContainerInt.GetAttributeAt(Utils.Convert(location), attribute);

    public List<LinkElementData<Rect>> GetLinks()
    {
        var linkElements = new List<LinkElementData<Rect>>();

        foreach (var link in HtmlContainerInt.GetLinks())
            linkElements.Add(new LinkElementData<Rect>(link.Id, link.Href, Utils.Convert(link.Rectangle)));

        return linkElements;
    }

    public string GetLinkAt(Point location) => HtmlContainerInt.GetLinkAt(Utils.Convert(location));

    public Rect? GetElementRectangle(string elementId)
    {
        var r = HtmlContainerInt.GetElementRectangle(elementId);
        return r.HasValue ? Utils.Convert(r.Value) : (Rect?)null;
    }

    public void PerformLayout()
    {
        using var ig = new GraphicsAdapter();
        HtmlContainerInt.PerformLayout(ig);
    }

    public void PerformPaint(DrawingContext g, Rect clip)
    {
        ArgumentNullException.ThrowIfNull(g);

        using var ig = new GraphicsAdapter(g, Utils.Convert(clip));
        HtmlContainerInt.PerformPaint(ig);
    }

    public void HandleMouseDown(Control parent, PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(e);

        HtmlContainerInt.HandleMouseDown(new ControlAdapter(parent), Utils.Convert(e.GetPosition(parent)));
    }

    public void HandleMouseUp(Control parent, PointerReleasedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(e);

        var mouseEvent = new RMouseEvent(e.InitialPressMouseButton == MouseButton.Left);
        HtmlContainerInt.HandleMouseUp(new ControlAdapter(parent), Utils.Convert(e.GetPosition(parent)), mouseEvent);
    }

    public void HandleMouseDoubleClick(Control parent, PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(e);

        HtmlContainerInt.HandleMouseDoubleClick(new ControlAdapter(parent), Utils.Convert(e.GetPosition(parent)));
    }

    public void HandleMouseMove(Control parent, Point mousePos)
    {
        ArgumentNullException.ThrowIfNull(parent);

        HtmlContainerInt.HandleMouseMove(new ControlAdapter(parent), Utils.Convert(mousePos));
    }

    public void HandleMouseLeave(Control parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        HtmlContainerInt.HandleMouseLeave(new ControlAdapter(parent));
    }

    public void HandleKeyDown(Control parent, KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(e);

        HtmlContainerInt.HandleKeyDown(new ControlAdapter(parent), CreateKeyEvent(e));
    }

    public void Dispose() => HtmlContainerInt.Dispose();

    private static RKeyEvent CreateKeyEvent(KeyEventArgs e)
    {
        var control = (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control;
        return new RKeyEvent(control, e.Key == Key.A, e.Key == Key.C);
    }
}
