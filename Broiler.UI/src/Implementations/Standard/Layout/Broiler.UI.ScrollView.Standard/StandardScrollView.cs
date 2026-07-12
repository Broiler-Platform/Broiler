using System;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.ScrollView.Standard;

public sealed class StandardScrollView : UiScrollView
{
    private BSize _contentDesiredExtent;
    private BRect _verticalTrackBounds = BRect.Empty;
    private BRect _horizontalTrackBounds = BRect.Empty;
    private BRect _scrollbarCornerBounds = BRect.Empty;
    private double _scrollbarThickness = 12;
    private double _minimumThumbLength = 18;
    private bool _showScrollbars = true;
    private ScrollbarAxis _dragAxis = ScrollbarAxis.None;
    private double _dragPointerOffsetWithinThumb;

    public BColor Background { get; set; } = BColor.Transparent;

    public BColor ScrollbarTrack { get; set; } = BColor.FromArgb(0x33, 0x94, 0xA3, 0xB8);

    public BColor ScrollbarThumb { get; set; } = BColor.FromArgb(0xAA, 0x7D, 0x8D, 0xA3);

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
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public double MinimumThumbLength
    {
        get => _minimumThumbLength;
        set
        {
            ThrowIfDisposed();
            ValidateNonNegativeFinite(value, nameof(value));
            if (_minimumThumbLength == value)
                return;

            _minimumThumbLength = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public bool ShowScrollbars
    {
        get => _showScrollbars;
        set
        {
            ThrowIfDisposed();
            if (_showScrollbars == value)
                return;

            _showScrollbars = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public BRect ContentBounds { get; private set; } = BRect.Empty;

    public bool HasVerticalScrollbar { get; private set; }

    public bool HasHorizontalScrollbar { get; private set; }

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize outerSize = new(
            ClampDesired(PreferredSize.Width, availableSize.Width),
            ClampDesired(PreferredSize.Height, availableSize.Height));

        double extentWidth = 0;
        double extentHeight = 0;
        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
                continue;

            BSize desired = child.Measure(new BSize(double.PositiveInfinity, double.PositiveInfinity));
            extentWidth = Math.Max(extentWidth, desired.Width);
            extentHeight = Math.Max(extentHeight, desired.Height);
        }

        _contentDesiredExtent = new BSize(extentWidth, extentHeight);
        ScrollbarLayout layout = CalculateLayout(new BRect(0, 0, outerSize.Width, outerSize.Height), _contentDesiredExtent);
        SetViewportAndExtent(layout.ContentBounds.Size, layout.ExtentSize);
        return outerSize;
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        ScrollbarLayout layout = CalculateLayout(finalRect, _contentDesiredExtent);
        ContentBounds = layout.ContentBounds;
        HasVerticalScrollbar = layout.HasVerticalScrollbar;
        HasHorizontalScrollbar = layout.HasHorizontalScrollbar;
        _verticalTrackBounds = layout.VerticalTrackBounds;
        _horizontalTrackBounds = layout.HorizontalTrackBounds;
        _scrollbarCornerBounds = layout.ScrollbarCornerBounds;
        SetViewportAndExtent(ContentBounds.Size, layout.ExtentSize);

        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
            {
                child.Arrange(BRect.Empty);
                continue;
            }

            child.Arrange(new BRect(ContentBounds.Left - HorizontalOffset, ContentBounds.Top - VerticalOffset, Math.Max(child.DesiredSize.Width, ContentBounds.Width), Math.Max(child.DesiredSize.Height, ContentBounds.Height)));
        }
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (!Background.IsEmpty && Background.A > 0)
            context.RenderList.FillRect(Bounds, Background);

        context.RenderList.PushClip(ContentBounds);
        foreach (UiElement child in Children)
            child.Render(context);
        context.RenderList.PopClip();

        RenderScrollbars(context);
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

    protected override bool ShouldHitTestChildren(BPoint point) =>
        ContentBounds.IsEmpty ? Bounds.Contains(point) : ContentBounds.Contains(point);

    private bool HandleWheel(UiInputEvent input)
    {
        double delta = -input.WheelDeltaNotches * LineScrollAmount;
        return input.WheelAxis == MouseWheelAxis.Horizontal
            ? ScrollBy(delta, 0)
            : ScrollBy(0, delta);
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.PageDown, "PageDown"))
            return ScrollBy(0, ViewportSize.Height * PageScrollFraction);
        if (IsKey(input, BVirtualKey.PageUp, "PageUp"))
            return ScrollBy(0, -ViewportSize.Height * PageScrollFraction);
        if (IsKey(input, BVirtualKey.Home, "Home"))
            return ScrollToStart();
        if (IsKey(input, BVirtualKey.End, "End"))
            return ScrollToEnd();
        if (IsKey(input, BVirtualKey.Down, "Down"))
            return ScrollBy(0, LineScrollAmount);
        if (IsKey(input, BVirtualKey.Up, "Up"))
            return ScrollBy(0, -LineScrollAmount);
        if (IsKey(input, BVirtualKey.Right, "Right"))
            return ScrollBy(LineScrollAmount, 0);
        if (IsKey(input, BVirtualKey.Left, "Left"))
            return ScrollBy(-LineScrollAmount, 0);

        return false;
    }

    private void RenderScrollbars(UiRenderContext context)
    {
        if (HasVerticalScrollbar)
            RenderScrollbar(context, ScrollbarAxis.Vertical);

        if (HasHorizontalScrollbar)
            RenderScrollbar(context, ScrollbarAxis.Horizontal);

        if (!_scrollbarCornerBounds.IsEmpty)
            context.RenderList.FillRect(_scrollbarCornerBounds, ScrollbarTrack);
    }

    private void RenderScrollbar(UiRenderContext context, ScrollbarAxis axis)
    {
        BRect track = GetTrackBounds(axis);
        BRect thumb = GetThumbBounds(axis);
        StandardControlPaint.FillRounded(context.RenderList, track, ScrollbarTrack, StandardControlPaint.PillRadius);
        StandardControlPaint.FillRounded(context.RenderList, thumb, ScrollbarThumb, StandardControlPaint.PillRadius);
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left)
            return false;

        if (input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            Session?.SetFocus(this);
            return HandlePointerDown(input.Position);
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up && _dragAxis != ScrollbarAxis.None)
        {
            EndScrollbarDrag();
            return true;
        }

        return false;
    }

    private bool HandlePointerDown(BPoint position)
    {
        if (HasVerticalScrollbar && TryHandleScrollbarPointerDown(ScrollbarAxis.Vertical, position))
            return true;

        return HasHorizontalScrollbar && TryHandleScrollbarPointerDown(ScrollbarAxis.Horizontal, position);
    }

    private bool TryHandleScrollbarPointerDown(ScrollbarAxis axis, BPoint position)
    {
        BRect track = GetTrackBounds(axis);
        if (track.IsEmpty || !track.Contains(position))
            return false;

        BRect thumb = GetThumbBounds(axis);
        if (thumb.Contains(position))
        {
            BeginScrollbarDrag(axis, position, thumb);
            return true;
        }

        double positionOnAxis = GetAxisPosition(axis, position);
        double pageDelta = axis == ScrollbarAxis.Vertical
            ? ViewportSize.Height * PageScrollFraction
            : ViewportSize.Width * PageScrollFraction;

        if (positionOnAxis < GetAxisStart(axis, thumb))
            pageDelta = -pageDelta;

        if (axis == ScrollbarAxis.Vertical)
            ScrollBy(0, pageDelta);
        else
            ScrollBy(pageDelta, 0);

        return true;
    }

    private void BeginScrollbarDrag(ScrollbarAxis axis, BPoint position, BRect thumb)
    {
        _dragAxis = axis;
        _dragPointerOffsetWithinThumb = GetAxisPosition(axis, position) - GetAxisStart(axis, thumb);
        Session?.CaptureInput(this);
    }

    private bool HandlePointerMove(UiInputEvent input)
    {
        if (_dragAxis == ScrollbarAxis.None)
            return false;

        DragScrollbar(input.Position);
        return true;
    }

    private void DragScrollbar(BPoint position)
    {
        BRect track = GetTrackBounds(_dragAxis);
        BRect thumb = GetThumbBounds(_dragAxis);
        double scrollableTrack = Math.Max(0, GetAxisLength(_dragAxis, track) - GetAxisLength(_dragAxis, thumb));
        if (scrollableTrack <= 0)
            return;

        double thumbStart = GetAxisPosition(_dragAxis, position) - _dragPointerOffsetWithinThumb;
        double normalized = (thumbStart - GetAxisStart(_dragAxis, track)) / scrollableTrack;
        double maxOffset = _dragAxis == ScrollbarAxis.Vertical ? MaxVerticalOffset : MaxHorizontalOffset;
        double offset = Math.Clamp(normalized, 0, 1) * maxOffset;

        if (_dragAxis == ScrollbarAxis.Vertical)
            SetOffset(new BPoint(HorizontalOffset, offset));
        else
            SetOffset(new BPoint(offset, VerticalOffset));
    }

    private void EndScrollbarDrag()
    {
        _dragAxis = ScrollbarAxis.None;
        _dragPointerOffsetWithinThumb = 0;
        Session?.ReleaseInputCapture(this);
    }

    private ScrollbarLayout CalculateLayout(BRect outerBounds, BSize desiredExtent)
    {
        double thickness = ShowScrollbars ? Math.Min(ScrollbarThickness, Math.Max(0, Math.Min(outerBounds.Width, outerBounds.Height))) : 0;
        bool hasVertical = ResolveInitialVisibility(VerticalScrollBarVisibility, desiredExtent.Height, outerBounds.Height, thickness);
        bool hasHorizontal = ResolveInitialVisibility(HorizontalScrollBarVisibility, desiredExtent.Width, outerBounds.Width, thickness);

        for (int index = 0; index < 3; index++)
        {
            BSize viewport = GetViewportSize(outerBounds.Size, hasVertical, hasHorizontal, thickness);
            bool nextVertical = ResolveVisibility(VerticalScrollBarVisibility, desiredExtent.Height, viewport.Height, outerBounds.Height, thickness);
            bool nextHorizontal = ResolveVisibility(HorizontalScrollBarVisibility, desiredExtent.Width, viewport.Width, outerBounds.Width, thickness);
            if (nextVertical == hasVertical && nextHorizontal == hasHorizontal)
                break;

            hasVertical = nextVertical;
            hasHorizontal = nextHorizontal;
        }

        BSize contentSize = GetViewportSize(outerBounds.Size, hasVertical, hasHorizontal, thickness);
        BRect contentBounds = new(outerBounds.Left, outerBounds.Top, contentSize.Width, contentSize.Height);
        BSize extentSize = new(Math.Max(desiredExtent.Width, contentSize.Width), Math.Max(desiredExtent.Height, contentSize.Height));
        BRect verticalTrack = hasVertical
            ? new BRect(contentBounds.Right, outerBounds.Top, thickness, contentBounds.Height)
            : BRect.Empty;
        BRect horizontalTrack = hasHorizontal
            ? new BRect(outerBounds.Left, contentBounds.Bottom, contentBounds.Width, thickness)
            : BRect.Empty;
        BRect corner = hasVertical && hasHorizontal
            ? new BRect(contentBounds.Right, contentBounds.Bottom, thickness, thickness)
            : BRect.Empty;

        return new ScrollbarLayout(contentBounds, extentSize, hasVertical, hasHorizontal, verticalTrack, horizontalTrack, corner);
    }

    private static BSize GetViewportSize(BSize outerSize, bool hasVertical, bool hasHorizontal, double thickness) =>
        new(
            Math.Max(0, outerSize.Width - (hasVertical ? thickness : 0)),
            Math.Max(0, outerSize.Height - (hasHorizontal ? thickness : 0)));

    private static bool ResolveInitialVisibility(UiScrollBarVisibility visibility, double desiredLength, double outerLength, double thickness) =>
        ResolveVisibility(visibility, desiredLength, outerLength, outerLength, thickness);

    private static bool ResolveVisibility(UiScrollBarVisibility visibility, double desiredLength, double viewportLength, double outerLength, double thickness)
    {
        if (thickness <= 0 || outerLength <= 0 || visibility == UiScrollBarVisibility.Hidden)
            return false;

        return visibility == UiScrollBarVisibility.Visible || desiredLength > viewportLength;
    }

    private BRect GetTrackBounds(ScrollbarAxis axis) =>
        axis == ScrollbarAxis.Vertical ? _verticalTrackBounds : _horizontalTrackBounds;

    private BRect GetThumbBounds(ScrollbarAxis axis)
    {
        BRect track = GetTrackBounds(axis);
        if (track.IsEmpty)
            return BRect.Empty;

        double trackLength = GetAxisLength(axis, track);
        double viewportLength = axis == ScrollbarAxis.Vertical ? ViewportSize.Height : ViewportSize.Width;
        double extentLength = axis == ScrollbarAxis.Vertical ? ExtentSize.Height : ExtentSize.Width;
        double maxOffset = axis == ScrollbarAxis.Vertical ? MaxVerticalOffset : MaxHorizontalOffset;
        double offset = axis == ScrollbarAxis.Vertical ? VerticalOffset : HorizontalOffset;
        double rawThumbLength = trackLength * (viewportLength / Math.Max(viewportLength, extentLength));
        double minThumbLength = Math.Min(Math.Max(0, MinimumThumbLength), trackLength);
        double thumbLength = Math.Clamp(rawThumbLength, minThumbLength, trackLength);
        double scrollableTrack = Math.Max(0, trackLength - thumbLength);
        double thumbStart = GetAxisStart(axis, track) + (maxOffset <= 0 ? 0 : scrollableTrack * (offset / maxOffset));

        return axis == ScrollbarAxis.Vertical
            ? new BRect(track.Left, thumbStart, track.Width, thumbLength)
            : new BRect(thumbStart, track.Top, thumbLength, track.Height);
    }

    private static double GetAxisPosition(ScrollbarAxis axis, BPoint point) =>
        axis == ScrollbarAxis.Vertical ? point.Y : point.X;

    private static double GetAxisStart(ScrollbarAxis axis, BRect rect) =>
        axis == ScrollbarAxis.Vertical ? rect.Top : rect.Left;

    private static double GetAxisLength(ScrollbarAxis axis, BRect rect) =>
        axis == ScrollbarAxis.Vertical ? rect.Height : rect.Width;

    private static void ValidateNonNegativeFinite(double value, string parameterName)
    {
        if (value < 0 || double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameterName, "Scrollbar metrics must be finite non-negative values.");
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));

    private readonly record struct ScrollbarLayout(
        BRect ContentBounds,
        BSize ExtentSize,
        bool HasVerticalScrollbar,
        bool HasHorizontalScrollbar,
        BRect VerticalTrackBounds,
        BRect HorizontalTrackBounds,
        BRect ScrollbarCornerBounds);

    private enum ScrollbarAxis
    {
        None,
        Vertical,
        Horizontal,
    }
}
