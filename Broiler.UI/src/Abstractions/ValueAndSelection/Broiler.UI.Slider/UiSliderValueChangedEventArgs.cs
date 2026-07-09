using System;

namespace Broiler.UI.Slider;

public sealed class UiSliderValueChangedEventArgs : EventArgs
{
    public UiSliderValueChangedEventArgs(double oldValue, double newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public double OldValue { get; }

    public double NewValue { get; }
}
