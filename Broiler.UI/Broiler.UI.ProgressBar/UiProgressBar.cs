using System;
using System.Globalization;
using Broiler.Graphics;

namespace Broiler.UI.ProgressBar;

public abstract class UiProgressBar : UiElement
{
    private double _minimum;
    private double _maximum = 100;
    private double _value;
    private bool _isIndeterminate;
    private bool _isReducedMotion;
    private bool _isDirectionReversed;
    private UiProgressBarOrientation _orientation;
    private BSize _preferredSize = new(160, 16);

    public event EventHandler<UiProgressBarValueChangedEventArgs>? ValueChanged;

    public double Minimum
    {
        get => _minimum;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value, nameof(value));
            if (value > Maximum)
                throw new ArgumentOutOfRangeException(nameof(value), "Minimum cannot exceed Maximum.");
            if (_minimum.Equals(value))
                return;

            _minimum = value;
            CoerceCurrentValue();
        }
    }

    public double Maximum
    {
        get => _maximum;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value, nameof(value));
            if (value < Minimum)
                throw new ArgumentOutOfRangeException(nameof(value), "Maximum cannot be less than Minimum.");
            if (_maximum.Equals(value))
                return;

            _maximum = value;
            CoerceCurrentValue();
        }
    }

    public double Value
    {
        get => _value;
        set
        {
            ThrowIfDisposed();
            SetValue(value);
        }
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set
        {
            ThrowIfDisposed();
            if (_isIndeterminate == value)
                return;

            _isIndeterminate = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public bool IsReducedMotion
    {
        get => _isReducedMotion;
        set
        {
            ThrowIfDisposed();
            if (_isReducedMotion == value)
                return;

            _isReducedMotion = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public bool IsDirectionReversed
    {
        get => _isDirectionReversed;
        set
        {
            ThrowIfDisposed();
            if (_isDirectionReversed == value)
                return;

            _isDirectionReversed = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public UiProgressBarOrientation Orientation
    {
        get => _orientation;
        set
        {
            ThrowIfDisposed();
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_orientation == value)
                return;

            _orientation = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred progress bar size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    protected double NormalizedValue =>
        Maximum.Equals(Minimum) ? 0 : (Value - Minimum) / (Maximum - Minimum);

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.ProgressBar,
            IsIndeterminate ? "Indeterminate" : Value.ToString(CultureInfo.InvariantCulture),
            Bounds,
            CreateSemanticState(),
            []);

    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        state |= UiSemanticState.Enabled;
        if (IsIndeterminate)
            state |= UiSemanticState.Indeterminate;
        return state;
    }

    private void CoerceCurrentValue() => SetValue(_value);

    private void SetValue(double value)
    {
        ValidateFinite(value, nameof(value));
        double coerced = Math.Clamp(value, Minimum, Maximum);
        if (_value.Equals(coerced))
            return;

        double oldValue = _value;
        _value = coerced;
        ValueChanged?.Invoke(this, new UiProgressBarValueChangedEventArgs(oldValue, _value));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameterName, "Range values must be finite.");
    }
}
