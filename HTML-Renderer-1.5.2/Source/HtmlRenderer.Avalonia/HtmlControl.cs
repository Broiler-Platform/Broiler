using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TheArtOfDev.HtmlRenderer.Core;
using TheArtOfDev.HtmlRenderer.Core.Entities;

namespace TheArtOfDev.HtmlRenderer.Avalonia;

/// <summary>
/// Avalonia control for rendering static HTML content.
/// </summary>
public class HtmlControl : Control
{
    protected readonly HtmlContainer _htmlContainer;
    protected CssData _baseCssData;

    public static readonly StyledProperty<bool> AvoidImagesLateLoadingProperty =
        AvaloniaProperty.Register<HtmlControl, bool>(nameof(AvoidImagesLateLoading), false);

    public static readonly StyledProperty<bool> AvoidAsyncImagesLoadingProperty =
        AvaloniaProperty.Register<HtmlControl, bool>(nameof(AvoidAsyncImagesLoading), false);

    public static readonly StyledProperty<bool> IsSelectionEnabledProperty =
        AvaloniaProperty.Register<HtmlControl, bool>(nameof(IsSelectionEnabled), true);

    public static readonly StyledProperty<bool> IsContextMenuEnabledProperty =
        AvaloniaProperty.Register<HtmlControl, bool>(nameof(IsContextMenuEnabled), true);

    public static readonly StyledProperty<string> BaseStylesheetProperty =
        AvaloniaProperty.Register<HtmlControl, string>(nameof(BaseStylesheet));

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<HtmlControl, string>(nameof(Text));

    static HtmlControl()
    {
        AffectsRender<HtmlControl>(TextProperty, BaseStylesheetProperty);
        AffectsMeasure<HtmlControl>(TextProperty, BaseStylesheetProperty);
    }

    public HtmlControl()
    {
        _htmlContainer = new HtmlContainer();
        _htmlContainer.LoadComplete += OnLoadComplete;
        _htmlContainer.LinkClicked += OnLinkClicked;
        _htmlContainer.RenderError += OnRenderError;
        _htmlContainer.Refresh += OnRefresh;
        _htmlContainer.StylesheetLoad += OnStylesheetLoad;
        _htmlContainer.ImageLoad += OnImageLoad;
    }

    public event EventHandler LoadComplete;
    public event EventHandler<HtmlLinkClickedEventArgs> LinkClicked;
    public event EventHandler<HtmlRenderErrorEventArgs> RenderError;
    public event EventHandler<HtmlStylesheetLoadEventArgs> StylesheetLoad;
    public event EventHandler<HtmlImageLoadEventArgs> ImageLoad;

    public bool AvoidImagesLateLoading
    {
        get => GetValue(AvoidImagesLateLoadingProperty);
        set => SetValue(AvoidImagesLateLoadingProperty, value);
    }

    public bool AvoidAsyncImagesLoading
    {
        get => GetValue(AvoidAsyncImagesLoadingProperty);
        set => SetValue(AvoidAsyncImagesLoadingProperty, value);
    }

    public bool IsSelectionEnabled
    {
        get => GetValue(IsSelectionEnabledProperty);
        set => SetValue(IsSelectionEnabledProperty, value);
    }

    public bool IsContextMenuEnabled
    {
        get => GetValue(IsContextMenuEnabledProperty);
        set => SetValue(IsContextMenuEnabledProperty, value);
    }

    public string BaseStylesheet
    {
        get => GetValue(BaseStylesheetProperty);
        set => SetValue(BaseStylesheetProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public virtual string SelectedText => _htmlContainer.SelectedText;

    public virtual string SelectedHtml => _htmlContainer.SelectedHtml;

    public virtual string GetHtml() => _htmlContainer?.GetHtml();

    public virtual Rect? GetElementRectangle(string elementId) => _htmlContainer?.GetElementRectangle(elementId);

    public void ClearSelection()
    {
        _htmlContainer?.ClearSelection();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var htmlWidth = HtmlWidth(Bounds.Size);
        var htmlHeight = HtmlHeight(Bounds.Size);

        if (_htmlContainer == null || htmlWidth <= 0 || htmlHeight <= 0)
            return;

        using (context.PushClip(new Rect(0, 0, htmlWidth, htmlHeight)))
        {
            _htmlContainer.Location = new Point(0, 0);
            _htmlContainer.PerformPaint(context, new Rect(0, 0, htmlWidth, htmlHeight));
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == AvoidImagesLateLoadingProperty)
        {
            _htmlContainer.AvoidImagesLateLoading = change.GetNewValue<bool>();
        }
        else if (change.Property == AvoidAsyncImagesLoadingProperty)
        {
            _htmlContainer.AvoidAsyncImagesLoading = change.GetNewValue<bool>();
        }
        else if (change.Property == IsSelectionEnabledProperty)
        {
            _htmlContainer.IsSelectionEnabled = change.GetNewValue<bool>();
        }
        else if (change.Property == IsContextMenuEnabledProperty)
        {
            _htmlContainer.IsContextMenuEnabled = change.GetNewValue<bool>();
        }
        else if (change.Property == BaseStylesheetProperty)
        {
            var baseCssData = HtmlRender.ParseStyleSheet(change.GetNewValue<string>());
            _baseCssData = baseCssData;
            _htmlContainer.SetHtml(Text, baseCssData);
        }
        else if (change.Property == TextProperty)
        {
            _htmlContainer.ScrollOffset = new Point(0, 0);
            _htmlContainer.SetHtml(change.GetNewValue<string>(), _baseCssData);
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _htmlContainer?.HandleMouseMove(this, e.GetPosition(this));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _htmlContainer?.HandleMouseLeave(this);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _htmlContainer?.HandleMouseDown(this, e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _htmlContainer?.HandleMouseUp(this, e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _htmlContainer?.HandleKeyDown(this, e);
    }

    protected virtual double HtmlWidth(Size size) => size.Width;

    protected virtual double HtmlHeight(Size size) => size.Height;

    private void OnLoadComplete(object sender, EventArgs e) => LoadComplete?.Invoke(this, e);
    private void OnLinkClicked(object sender, HtmlLinkClickedEventArgs e) => LinkClicked?.Invoke(this, e);
    private void OnRenderError(object sender, HtmlRenderErrorEventArgs e) => RenderError?.Invoke(this, e);
    private void OnStylesheetLoad(object sender, HtmlStylesheetLoadEventArgs e) => StylesheetLoad?.Invoke(this, e);
    private void OnImageLoad(object sender, HtmlImageLoadEventArgs e) => ImageLoad?.Invoke(this, e);

    private void OnRefresh(object sender, HtmlRefreshEventArgs e)
    {
        if (e.Layout)
            InvalidateMeasure();

        InvalidateVisual();
    }
}
