using System;
using System.Globalization;
using Broiler.Graphics;

namespace Broiler.UI.Slider;

public abstract class UiSlider : UiElement
{
    private double _minimum;
    private double _maximum = 100;
    private double _value;
    private double _stepFrequency = 1;
    private double _smallChange = 1;
    private double _largeChange = 10;
    private bool _isEnabled = true;
    private bool _isDirectionReversed;
    private UiSliderOrientation _orientation;
    private BSize _preferredSize = new(160, 32);

    public event EventHandler<UiSliderValueChangedEventArgs>? ValueChanged;

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

    public double StepFrequency
    {
        get => _stepFrequency;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value, nameof(value));
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Step frequency must be non-negative.");
            if (_stepFrequency.Equals(value))
                return;

            _stepFrequency = value;
            CoerceCurrentValue();
        }
    }

    public double SmallChange
    {
        get => _smallChange;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value, nameof(value));
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "SmallChange must be non-negative.");
            _smallChange = value;
        }
    }

    public double LargeChange
    {
        get => _largeChange;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value, nameof(value));
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "LargeChange must be non-negative.");
            _largeChange = value;
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            ThrowIfDisposed();
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
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

    public UiSliderOrientation Orientation
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
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred slider size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public void ChangeBySmallStep(int direction) => Value += Math.Sign(direction) * GetEffectiveChange(SmallChange);

    public void ChangeByLargeStep(int direction) => Value += Math.Sign(direction) * GetEffectiveChange(LargeChange);

    protected double NormalizedValue =>
        Maximum.Equals(Minimum) ? 0 : (Value - Minimum) / (Maximum - Minimum);

    protected void SetValueFromNormalized(double normalized)
    {
        if (IsDirectionReversed)
            normalized = 1 - normalized;

        Value = Minimum + ((Maximum - Minimum) * Math.Clamp(normalized, 0, 1));
    }

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Slider,
            Value.ToString(CultureInfo.InvariantCulture),
            Bounds,
            CreateSemanticState(),
            []);

    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (IsEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        return state;
    }

    private void CoerceCurrentValue() => SetValue(_value);

    private void SetValue(double value)
    {
        double coerced = CoerceValue(value);
        if (_value.Equals(coerced))
            return;

        double oldValue = _value;
        _value = coerced;
        ValueChanged?.Invoke(this, new UiSliderValueChangedEventArgs(oldValue, _value));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private double CoerceValue(double value)
    {
        ValidateFinite(value, nameof(value));
        double clamped = Math.Clamp(value, Minimum, Maximum);
        if (StepFrequency > 0)
            clamped = Minimum + (Math.Round((clamped - Minimum) / StepFrequency) * StepFrequency);

        return Math.Clamp(clamped, Minimum, Maximum);
    }

    private double GetEffectiveChange(double requestedChange) =>
        StepFrequency > 0 && requestedChange < StepFrequency ? StepFrequency : requestedChange;

    private static void ValidateFinite(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameterName, "Range values must be finite.");
    }
}
