using System;
using Broiler.Graphics;

namespace Broiler.UI.Splitter;

/// <summary>A reusable normalized splitter grip; hosts apply <see cref="Value"/> to pane layout.</summary>
public abstract class UiSplitter : UiElement
{
    private bool _isEnabled = true;
    private UiSplitterOrientation _orientation;
    private double _value = 0.5;
    private double _minimum = 0.1;
    private double _maximum = 0.9;
    private double _smallChange = 0.02;
    private double _largeChange = 0.1;
    private double _dragExtent = 400;
    private BSize _preferredSize = new(8, 8);

    public event EventHandler<UiSplitterValueChangedEventArgs>? ValueChanged;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value, UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public UiSplitterOrientation Orientation
    {
        get => _orientation;
        set => SetField(ref _orientation, value, UiInvalidationKind.Measure | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public double Value
    {
        get => _value;
        set
        {
            ThrowIfDisposed();
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            double next = Math.Clamp(value, Minimum, Maximum);
            if (_value.Equals(next))
                return;
            double old = _value;
            _value = next;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            ValueChanged?.Invoke(this, new UiSplitterValueChangedEventArgs(old, next));
        }
    }

    public double Minimum
    {
        get => _minimum;
        set
        {
            ThrowIfDisposed();
            if (!double.IsFinite(value) || value < 0 || value > Maximum)
                throw new ArgumentOutOfRangeException(nameof(value));
            _minimum = value;
            Value = _value;
        }
    }

    public double Maximum
    {
        get => _maximum;
        set
        {
            ThrowIfDisposed();
            if (!double.IsFinite(value) || value < Minimum || value > 1)
                throw new ArgumentOutOfRangeException(nameof(value));
            _maximum = value;
            Value = _value;
        }
    }

    public double SmallChange
    {
        get => _smallChange;
        set => _smallChange = Positive(value, nameof(value));
    }

    public double LargeChange
    {
        get => _largeChange;
        set => _largeChange = Positive(value, nameof(value));
    }

    /// <summary>Available drag distance in layout units, supplied by the composing host.</summary>
    public double DragExtent
    {
        get => _dragExtent;
        set => _dragExtent = Positive(value, nameof(value));
    }

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    protected void AdjustValue(double delta) => Value += delta;

    protected override UiSemanticNode GetSemanticNodeCore()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (IsEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        string axis = Orientation == UiSplitterOrientation.Horizontal ? "horizontal" : "vertical";
        return new UiSemanticNode(
            UiSemanticRole.Splitter,
            $"Resize panes, {axis}, {Value:P0}",
            Bounds,
            state,
            []);
    }

    private void SetField<T>(ref T field, T value, UiInvalidationKind invalidation)
        where T : struct
    {
        ThrowIfDisposed();
        if (field.Equals(value))
            return;
        field = value;
        Invalidate(invalidation);
    }

    private static double Positive(double value, string parameter) =>
        value > 0 && double.IsFinite(value)
            ? value
            : throw new ArgumentOutOfRangeException(parameter);
}
