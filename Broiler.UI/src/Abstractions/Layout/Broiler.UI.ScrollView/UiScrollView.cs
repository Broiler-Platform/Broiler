using System;
using Broiler.Graphics;

namespace Broiler.UI.ScrollView;

public abstract class UiScrollView : UiElement
{
    private BPoint _offset;
    private BSize _extentSize;
    private BSize _viewportSize;
    private BSize _preferredSize = new(160, 120);
    private double _lineScrollAmount = 32;
    private double _pageScrollFraction = 0.85;
    private UiScrollBarVisibility _horizontalScrollBarVisibility = UiScrollBarVisibility.Auto;
    private UiScrollBarVisibility _verticalScrollBarVisibility = UiScrollBarVisibility.Auto;

    public event EventHandler<UiScrollOffsetChangedEventArgs>? OffsetChanged;

    public BPoint Offset => _offset;

    public double HorizontalOffset => _offset.X;

    public double VerticalOffset => _offset.Y;

    public BSize ExtentSize
    {
        get => _extentSize;
        protected set
        {
            if (_extentSize == value)
                return;

            _extentSize = value;
            CoerceOffset();
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public BSize ViewportSize
    {
        get => _viewportSize;
        protected set
        {
            if (_viewportSize == value)
                return;

            _viewportSize = value;
            CoerceOffset();
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred scroll view size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public double LineScrollAmount
    {
        get => _lineScrollAmount;
        set
        {
            ThrowIfDisposed();
            if (value < 0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Line scroll amount must be a finite non-negative value.");
            _lineScrollAmount = value;
        }
    }

    public double PageScrollFraction
    {
        get => _pageScrollFraction;
        set
        {
            ThrowIfDisposed();
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Page scroll fraction must be a finite positive value.");
            _pageScrollFraction = value;
        }
    }

    public UiScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => _horizontalScrollBarVisibility;
        set
        {
            ThrowIfDisposed();
            ValidateScrollBarVisibility(value, nameof(value));
            if (_horizontalScrollBarVisibility == value)
                return;

            _horizontalScrollBarVisibility = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public UiScrollBarVisibility VerticalScrollBarVisibility
    {
        get => _verticalScrollBarVisibility;
        set
        {
            ThrowIfDisposed();
            ValidateScrollBarVisibility(value, nameof(value));
            if (_verticalScrollBarVisibility == value)
                return;

            _verticalScrollBarVisibility = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public bool SetOffset(BPoint offset)
    {
        ThrowIfDisposed();
        BPoint coerced = Coerce(offset);
        if (_offset == coerced)
            return false;

        BPoint oldOffset = _offset;
        _offset = coerced;
        OffsetChanged?.Invoke(this, new UiScrollOffsetChangedEventArgs(oldOffset, _offset));
        Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    public bool ScrollBy(double horizontalDelta, double verticalDelta) =>
        SetOffset(new BPoint(HorizontalOffset + horizontalDelta, VerticalOffset + verticalDelta));

    public bool ScrollToStart() => SetOffset(BPoint.Zero);

    public bool ScrollToEnd() => SetOffset(new BPoint(MaxHorizontalOffset, MaxVerticalOffset));

    protected double MaxHorizontalOffset => Math.Max(0, ExtentSize.Width - ViewportSize.Width);

    protected double MaxVerticalOffset => Math.Max(0, ExtentSize.Height - ViewportSize.Height);

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.ScrollView,
            $"{HorizontalOffset:0.###},{VerticalOffset:0.###}",
            Bounds,
            Visibility == UiVisibility.Visible ? UiSemanticState.Visible | UiSemanticState.Enabled : UiSemanticState.None,
            []);

    protected void SetViewportAndExtent(BSize viewportSize, BSize extentSize)
    {
        ViewportSize = new BSize(Math.Max(0, viewportSize.Width), Math.Max(0, viewportSize.Height));
        ExtentSize = new BSize(Math.Max(0, extentSize.Width), Math.Max(0, extentSize.Height));
    }

    private void CoerceOffset() => SetOffset(_offset);

    private BPoint Coerce(BPoint offset) =>
        new(
            Math.Clamp(Normalize(offset.X), 0, MaxHorizontalOffset),
            Math.Clamp(Normalize(offset.Y), 0, MaxVerticalOffset));

    private static double Normalize(double value)
    {
        if (double.IsNaN(value))
            return 0;
        if (double.IsNegativeInfinity(value))
            return 0;
        if (double.IsPositiveInfinity(value))
            return double.MaxValue;
        return value;
    }

    private static void ValidateScrollBarVisibility(UiScrollBarVisibility value, string parameterName)
    {
        if (value is not UiScrollBarVisibility.Auto and not UiScrollBarVisibility.Visible and not UiScrollBarVisibility.Hidden)
            throw new ArgumentOutOfRangeException(parameterName);
    }
}
