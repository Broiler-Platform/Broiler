using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TheArtOfDev.HtmlRenderer.Core.Entities;

namespace TheArtOfDev.HtmlRenderer.Avalonia;

/// <summary>
/// Avalonia control for rendering HTML content with scrollbar support.
/// </summary>
public class HtmlPanel : HtmlControl
{
    private readonly ScrollBar _verticalScrollBar;
    private readonly ScrollBar _horizontalScrollBar;

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<HtmlPanel, IBrush?>(nameof(Background), Brushes.White);

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    static HtmlPanel()
    {
        AffectsRender<HtmlPanel>(TextProperty, BackgroundProperty);
        AffectsMeasure<HtmlPanel>(TextProperty);
    }

    public HtmlPanel()
    {
        _verticalScrollBar = new ScrollBar
        {
            Orientation = Orientation.Vertical,
            Width = 18,
        };
        _verticalScrollBar.Scroll += OnScrollBarScroll;
        VisualChildren.Add(_verticalScrollBar);
        LogicalChildren.Add(_verticalScrollBar);

        _horizontalScrollBar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Height = 18,
        };
        _horizontalScrollBar.Scroll += OnScrollBarScroll;
        VisualChildren.Add(_horizontalScrollBar);
        LogicalChildren.Add(_horizontalScrollBar);

        _htmlContainer.ScrollChange += OnScrollChange;

        Focusable = true;
    }

    public virtual void ScrollToElement(string elementId)
    {
        ArgumentException.ThrowIfNullOrEmpty(elementId);

        if (_htmlContainer == null)
            return;

        var rect = _htmlContainer.GetElementRectangle(elementId);
        if (!rect.HasValue)
            return;

        ScrollToPoint(rect.Value.X, rect.Value.Y);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_htmlContainer == null)
            return availableSize;

        _htmlContainer.MaxSize = new Size(HtmlWidth(availableSize), 0);
        _htmlContainer.PerformLayout();
        var size = _htmlContainer.ActualSize;

        // Manage scrollbar visibility
        bool relayout = false;
        var htmlWidth = HtmlWidth(availableSize);
        var htmlHeight = HtmlHeight(availableSize);

        if ((!_verticalScrollBar.IsVisible && size.Height > htmlHeight) ||
            (_verticalScrollBar.IsVisible && size.Height <= htmlHeight))
        {
            _verticalScrollBar.IsVisible = !_verticalScrollBar.IsVisible;
            relayout = true;
        }

        if ((!_horizontalScrollBar.IsVisible && size.Width > htmlWidth) ||
            (_horizontalScrollBar.IsVisible && size.Width <= htmlWidth))
        {
            _horizontalScrollBar.IsVisible = !_horizontalScrollBar.IsVisible;
            relayout = true;
        }

        if (relayout)
        {
            _htmlContainer.MaxSize = new Size(HtmlWidth(availableSize), 0);
            _htmlContainer.PerformLayout();
        }

        if (double.IsPositiveInfinity(availableSize.Width) || double.IsPositiveInfinity(availableSize.Height))
            availableSize = size;

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var scrollHeight = HtmlHeight(finalSize);
        scrollHeight = scrollHeight > 1 ? scrollHeight : 1;
        var scrollWidth = HtmlWidth(finalSize);
        scrollWidth = scrollWidth > 1 ? scrollWidth : 1;

        _verticalScrollBar.Arrange(new Rect(
            Math.Max(finalSize.Width - _verticalScrollBar.Width, 0), 0,
            _verticalScrollBar.Width, scrollHeight));

        _horizontalScrollBar.Arrange(new Rect(
            0, Math.Max(finalSize.Height - _horizontalScrollBar.Height, 0),
            scrollWidth, _horizontalScrollBar.Height));

        if (_htmlContainer != null)
        {
            if (_verticalScrollBar.IsVisible)
            {
                _verticalScrollBar.ViewportSize = HtmlHeight(finalSize);
                _verticalScrollBar.SmallChange = 25;
                _verticalScrollBar.LargeChange = _verticalScrollBar.ViewportSize * .9;
                _verticalScrollBar.Maximum = _htmlContainer.ActualSize.Height - _verticalScrollBar.ViewportSize;
            }

            if (_horizontalScrollBar.IsVisible)
            {
                _horizontalScrollBar.ViewportSize = HtmlWidth(finalSize);
                _horizontalScrollBar.SmallChange = 25;
                _horizontalScrollBar.LargeChange = _horizontalScrollBar.ViewportSize * .9;
                _horizontalScrollBar.Maximum = _htmlContainer.ActualSize.Width - _horizontalScrollBar.ViewportSize;
            }

            UpdateScrollOffsets();
        }

        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var htmlWidth = HtmlWidth(Bounds.Size);
        var htmlHeight = HtmlHeight(Bounds.Size);

        if (Background != null)
            context.DrawRectangle(Background, null, new Rect(Bounds.Size));

        if (_htmlContainer == null || htmlWidth <= 0 || htmlHeight <= 0)
            return;

        using (context.PushClip(new Rect(0, 0, htmlWidth, htmlHeight)))
        {
            _htmlContainer.Location = new Point(0, 0);
            _htmlContainer.PerformPaint(context, new Rect(0, 0, htmlWidth, htmlHeight));
        }

        // Render rectangle in right bottom corner where both scrolls meet
        if (_horizontalScrollBar.IsVisible && _verticalScrollBar.IsVisible)
        {
            context.DrawRectangle(
                Brushes.LightGray, null,
                new Rect(HtmlWidth(Bounds.Size), HtmlHeight(Bounds.Size),
                    _verticalScrollBar.Width, _horizontalScrollBar.Height));
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (!_verticalScrollBar.IsVisible)
            return;

        _verticalScrollBar.Value -= e.Delta.Y * 50;
        UpdateScrollOffsets();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_verticalScrollBar.IsVisible)
        {
            switch (e.Key)
            {
                case Key.Up:
                    _verticalScrollBar.Value -= _verticalScrollBar.SmallChange;
                    UpdateScrollOffsets();
                    e.Handled = true;
                    break;
                case Key.Down:
                    _verticalScrollBar.Value += _verticalScrollBar.SmallChange;
                    UpdateScrollOffsets();
                    e.Handled = true;
                    break;
                case Key.PageUp:
                    _verticalScrollBar.Value -= _verticalScrollBar.LargeChange;
                    UpdateScrollOffsets();
                    e.Handled = true;
                    break;
                case Key.PageDown:
                    _verticalScrollBar.Value += _verticalScrollBar.LargeChange;
                    UpdateScrollOffsets();
                    e.Handled = true;
                    break;
                case Key.Home:
                    _verticalScrollBar.Value = 0;
                    UpdateScrollOffsets();
                    e.Handled = true;
                    break;
                case Key.End:
                    _verticalScrollBar.Value = _verticalScrollBar.Maximum;
                    UpdateScrollOffsets();
                    e.Handled = true;
                    break;
            }
        }

        if (_horizontalScrollBar.IsVisible)
        {
            switch (e.Key)
            {
                case Key.Left:
                    _horizontalScrollBar.Value -= _horizontalScrollBar.SmallChange;
                    UpdateScrollOffsets();
                    e.Handled = true;
                    break;
                case Key.Right:
                    _horizontalScrollBar.Value += _horizontalScrollBar.SmallChange;
                    UpdateScrollOffsets();
                    e.Handled = true;
                    break;
            }
        }
    }

    protected override double HtmlWidth(Size size)
    {
        var width = base.HtmlWidth(size) - (_verticalScrollBar.IsVisible ? _verticalScrollBar.Width : 0);
        return width > 1 ? width : 1;
    }

    protected override double HtmlHeight(Size size)
    {
        var height = base.HtmlHeight(size) - (_horizontalScrollBar.IsVisible ? _horizontalScrollBar.Height : 0);
        return height > 1 ? height : 1;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            _horizontalScrollBar.Value = 0;
            _verticalScrollBar.Value = 0;
        }
    }

    private void OnScrollChange(object sender, HtmlScrollEventArgs e) => ScrollToPoint(e.X, e.Y);

    private void ScrollToPoint(double x, double y)
    {
        _horizontalScrollBar.Value = x;
        _verticalScrollBar.Value = y;
        UpdateScrollOffsets();
    }

    private void OnScrollBarScroll(object sender, ScrollEventArgs e) => UpdateScrollOffsets();

    private void UpdateScrollOffsets()
    {
        var newScrollOffset = new Point(-_horizontalScrollBar.Value, -_verticalScrollBar.Value);
        if (newScrollOffset.Equals(_htmlContainer.ScrollOffset))
            return;

        _htmlContainer.ScrollOffset = newScrollOffset;
        InvalidateVisual();
    }
}
