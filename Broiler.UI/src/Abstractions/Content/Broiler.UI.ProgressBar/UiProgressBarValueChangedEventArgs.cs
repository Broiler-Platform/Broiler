using System;

namespace Broiler.UI.ProgressBar;

public sealed class UiProgressBarValueChangedEventArgs : EventArgs
{
    public UiProgressBarValueChangedEventArgs(double oldValue, double newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public double OldValue { get; }

    public double NewValue { get; }
}
