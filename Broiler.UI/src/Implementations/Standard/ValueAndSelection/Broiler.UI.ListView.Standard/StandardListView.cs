using System;
using System.Collections.Generic;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.ListView.Standard;

public sealed class StandardListView : UiListView, IStandardThemedControl
{
    private BRect _contentBounds = BRect.Empty;
    private BRect _scrollbarTrackBounds = BRect.Empty;
    private double _scrollbarThickness = 12;
    private double _minimumScrollbarThumbLength = 18;
    private bool _showScrollbar = true;
    private bool _isDraggingScrollbar;
    private double _dragPointerOffsetWithinThumb;

    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.Surface;
        Foreground = theme.Text;
        SelectedBackground = theme.AccentSoft;
        FocusRing = theme.FocusRing;
        BorderColor = theme.Border;
        ScrollbarTrack = theme.SurfaceDisabled;
        ScrollbarThumb = theme.BorderStrong;
    }

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor SelectedBackground { get; set; } = StandardControlPaint.AccentSoft;

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor ScrollbarTrack { get; set; } = StandardControlPaint.SurfaceDisabled;

    public BColor ScrollbarThumb { get; set; } = StandardControlPaint.BorderStrong;

    public BFontStyle Font { get; set; } = BFontStyle.Default;

    public double ItemHeight { get; set; } = 26;

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    public double WheelScrollItems { get; set; } = 3;

    public double ScrollbarThickness
    {
        get => _scrollbarThickness;
        set
        {
            ThrowIfDisposed();
            ValidateNonNegativeFinite(value, nameof(value));
            if (_scrollbarThickness == value)
                return;

            _scrollbarThickness = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public double MinimumScrollbarThumbLength
    {
        get => _minimumScrollbarThumbLength;
        set
        {
            ThrowIfDisposed();
            ValidateNonNegativeFinite(value, nameof(value));
            if (_minimumScrollbarThumbLength == value)
                return;

            _minimumScrollbarThumbLength = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public bool ShowScrollbar
    {
        get => _showScrollbar;
        set
        {
            ThrowIfDisposed();
            if (_showScrollbar == value)
                return;

            _showScrollbar = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public BRect ContentBounds => _contentBounds;

    public bool HasVerticalScrollbar { get; private set; }

    public int FirstVisibleIndex { get; private set; }

    public int VisibleItemCount { get; private set; }

    protected override BSize MeasureCore(BSize availableSize) =>
        new(
            ClampDesired(PreferredSize.Width, availableSize.Width),
            ClampDesired(PreferredSize.Height, availableSize.Height));

    protected override void ArrangeCore(BRect finalRect)
    {
        HasVerticalScrollbar = ShouldShowVerticalScrollbar(finalRect.Height);
        double thickness = HasVerticalScrollbar ? Math.Min(ScrollbarThickness, Math.Max(0, finalRect.Width)) : 0;
        _contentBounds = new BRect(finalRect.Left, finalRect.Top, Math.Max(0, finalRect.Width - thickness), finalRect.Height);
        _scrollbarTrackBounds = HasVerticalScrollbar
            ? new BRect(_contentBounds.Right, finalRect.Top, thickness, finalRect.Height)
            : BRect.Empty;

        CoerceOffset();
        UpdateVisibleRange();
    }

    protected override void RenderCore(UiRenderContext context)
    {
        UpdateVisibleRange();
        StandardControlPaint.FillRounded(context.RenderList, Bounds, Background, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, BorderColor, CornerRadius, 1);
        context.RenderList.PushClip(_contentBounds);

        for (int index = FirstVisibleIndex; index < Math.Min(Items.Count, FirstVisibleIndex + VisibleItemCount); index++)
        {
            BRect row = GetItemBounds(index);
            UiListItem item = Items[index];
            if (StringComparer.Ordinal.Equals(item.Id, SelectedItemId))
                context.RenderList.FillRect(StandardControlPaint.Inset(row, 2), SelectedBackground);

            context.RenderList.DrawText(new BTextRun(item.Text, Font, Foreground), new BPoint(row.Left + 6, row.Top + Math.Max(0, (row.Height - BTextMeasurer.GetLineHeight(Font)) / 2)));
        }

        context.RenderList.PopClip();
        RenderScrollbar(context);

        if (Session?.FocusedElement == this)
            StandardControlPaint.StrokeRounded(context.RenderList, StandardControlPaint.Inset(Bounds, 2), FocusRing, Math.Max(0, CornerRadius - 2), 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        return input.Kind switch
        {
            UiInputEventKind.PointerMove => HandlePointerMove(input),
            UiInputEventKind.PointerButton => HandlePointerButton(input),
            UiInputEventKind.PointerWheel => HandleWheel(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            _ => false,
        };
    }

    protected override IReadOnlyList<UiSemanticNode> CreateVisibleSemanticNodes()
    {
        UpdateVisibleRange();
        var nodes = new List<UiSemanticNode>(VisibleItemCount);
        for (int index = FirstVisibleIndex; index < Math.Min(Items.Count, FirstVisibleIndex + VisibleItemCount); index++)
        {
            UiListItem item = Items[index];
            UiSemanticState state = UiSemanticState.Visible | UiSemanticState.Enabled;
            if (StringComparer.Ordinal.Equals(item.Id, SelectedItemId))
                state |= UiSemanticState.Selected;
            nodes.Add(new UiSemanticNode(UiSemanticRole.Generic, item.Text, GetItemBounds(index), state, []));
        }

        return nodes;
    }

    public void ScrollIntoView(string itemId)
    {
        int index = IndexOf(itemId);
        if (index < 0)
            return;

        double top = index * ItemHeight;
        double bottom = top + ItemHeight;
        if (top < VerticalOffset)
            SetVerticalOffset(top);
        else if (bottom > VerticalOffset + ViewportHeight)
            SetVerticalOffset(bottom - ViewportHeight);

        CoerceOffset();
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left)
            return false;

        if (input.MouseButtonTransition == MouseButtonTransition.Down)
            return HandlePointerDown(input.Position);

        if (input.MouseButtonTransition == MouseButtonTransition.Up && _isDraggingScrollbar)
        {
            EndScrollbarDrag();
            return true;
        }

        return false;
    }

    private bool HandlePointerDown(BPoint position)
    {
        Session?.SetFocus(this);
        if (HasVerticalScrollbar && TryHandleScrollbarPointerDown(position))
            return true;

        if (!_contentBounds.Contains(position))
            return true;

        int index = (int)Math.Floor((VerticalOffset + position.Y - _contentBounds.Top) / Math.Max(1, ItemHeight));
        if ((uint)index < (uint)Items.Count)
        {
            string itemId = Items[index].Id;
            SelectItem(itemId);
            ScrollIntoView(itemId);
        }

        return true;
    }

    private bool TryHandleScrollbarPointerDown(BPoint position)
    {
        if (_scrollbarTrackBounds.IsEmpty || !_scrollbarTrackBounds.Contains(position))
            return false;

        BRect thumb = GetScrollbarThumbBounds();
        if (thumb.Contains(position))
        {
            BeginScrollbarDrag(position, thumb);
            return true;
        }

        double pageDelta = ViewportHeight * 0.85;
        if (position.Y < thumb.Top)
            pageDelta = -pageDelta;

        SetVerticalOffset(VerticalOffset + pageDelta);
        CoerceOffset();
        return true;
    }

    private void BeginScrollbarDrag(BPoint position, BRect thumb)
    {
        _isDraggingScrollbar = true;
        _dragPointerOffsetWithinThumb = position.Y - thumb.Top;
        Session?.CaptureInput(this);
    }

    private bool HandlePointerMove(UiInputEvent input)
    {
        if (!_isDraggingScrollbar)
            return false;

        DragScrollbar(input.Position);
        return true;
    }

    private void DragScrollbar(BPoint position)
    {
        BRect thumb = GetScrollbarThumbBounds();
        double scrollableTrack = Math.Max(0, _scrollbarTrackBounds.Height - thumb.Height);
        if (scrollableTrack <= 0)
            return;

        double thumbTop = position.Y - _dragPointerOffsetWithinThumb;
        double normalized = (thumbTop - _scrollbarTrackBounds.Top) / scrollableTrack;
        SetVerticalOffset(Math.Clamp(normalized, 0, 1) * MaxVerticalOffset);
        CoerceOffset();
    }

    private void EndScrollbarDrag()
    {
        _isDraggingScrollbar = false;
        _dragPointerOffsetWithinThumb = 0;
        Session?.ReleaseInputCapture(this);
    }

    private bool HandleWheel(UiInputEvent input)
    {
        if (input.WheelAxis != MouseWheelAxis.Vertical)
            return false;

        SetVerticalOffset(VerticalOffset - input.WheelDeltaNotches * ItemHeight * WheelScrollItems);
        CoerceOffset();
        return true;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        int index = SelectedIndex >= 0 ? SelectedIndex : 0;
        if (IsKey(input, BVirtualKey.Down, "Down"))
            return SelectAndReveal(Math.Min(Items.Count - 1, index + 1));
        if (IsKey(input, BVirtualKey.Up, "Up"))
            return SelectAndReveal(Math.Max(0, index - 1));
        if (IsKey(input, BVirtualKey.Home, "Home"))
            return SelectAndReveal(0);
        if (IsKey(input, BVirtualKey.End, "End"))
            return SelectAndReveal(Items.Count - 1);
        if (IsKey(input, BVirtualKey.PageDown, "PageDown"))
            return SelectAndReveal(Math.Min(Items.Count - 1, index + Math.Max(1, VisibleItemCount - 1)));
        if (IsKey(input, BVirtualKey.PageUp, "PageUp"))
            return SelectAndReveal(Math.Max(0, index - Math.Max(1, VisibleItemCount - 1)));

        return false;
    }

    private bool SelectAndReveal(int index)
    {
        if ((uint)index >= (uint)Items.Count)
            return false;

        Session?.SetFocus(this);
        string itemId = Items[index].Id;
        SelectItem(itemId);
        ScrollIntoView(itemId);
        return true;
    }

    private void CoerceOffset()
    {
        if (VerticalOffset > MaxVerticalOffset)
            SetVerticalOffset(MaxVerticalOffset);
    }

    private void UpdateVisibleRange()
    {
        double itemHeight = Math.Max(1, ItemHeight);
        FirstVisibleIndex = Math.Clamp((int)Math.Floor(VerticalOffset / itemHeight), 0, Math.Max(0, Items.Count));
        VisibleItemCount = Math.Min(Math.Max(0, Items.Count - FirstVisibleIndex), Math.Max(0, (int)Math.Ceiling(ViewportHeight / itemHeight) + 1));
    }

    private BRect GetItemBounds(int index) =>
        new(_contentBounds.Left, _contentBounds.Top + index * ItemHeight - VerticalOffset, _contentBounds.Width, ItemHeight);

    private void RenderScrollbar(UiRenderContext context)
    {
        if (!HasVerticalScrollbar)
            return;

        StandardControlPaint.FillRounded(context.RenderList, _scrollbarTrackBounds, ScrollbarTrack, StandardControlPaint.PillRadius);
        StandardControlPaint.FillRounded(context.RenderList, GetScrollbarThumbBounds(), ScrollbarThumb, StandardControlPaint.PillRadius);
    }

    private BRect GetScrollbarThumbBounds()
    {
        if (_scrollbarTrackBounds.IsEmpty)
            return BRect.Empty;

        double rawThumbHeight = _scrollbarTrackBounds.Height * (ViewportHeight / Math.Max(ViewportHeight, ExtentHeight));
        double minThumbHeight = Math.Min(Math.Max(0, MinimumScrollbarThumbLength), _scrollbarTrackBounds.Height);
        double thumbHeight = Math.Clamp(rawThumbHeight, minThumbHeight, _scrollbarTrackBounds.Height);
        double scrollableTrack = Math.Max(0, _scrollbarTrackBounds.Height - thumbHeight);
        double thumbTop = _scrollbarTrackBounds.Top + (MaxVerticalOffset <= 0 ? 0 : scrollableTrack * (VerticalOffset / MaxVerticalOffset));
        return new BRect(_scrollbarTrackBounds.Left, thumbTop, _scrollbarTrackBounds.Width, thumbHeight);
    }

    private bool ShouldShowVerticalScrollbar(double viewportHeight) =>
        ShowScrollbar && ScrollbarThickness > 0 && viewportHeight > 0 && ExtentHeight > viewportHeight;

    private double ExtentHeight => Items.Count * Math.Max(1, ItemHeight);

    private double ViewportHeight => _contentBounds.IsEmpty ? Bounds.Height : _contentBounds.Height;

    private double MaxVerticalOffset => Math.Max(0, ExtentHeight - ViewportHeight);

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));

    private static void ValidateNonNegativeFinite(double value, string parameterName)
    {
        if (value < 0 || double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameterName, "Scrollbar metrics must be finite non-negative values.");
    }
}
