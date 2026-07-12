using System;

namespace Broiler.UI.Splitter;

public sealed class UiSplitterValueChangedEventArgs : EventArgs
{
    public UiSplitterValueChangedEventArgs(double oldValue, double newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public double OldValue { get; }

    public double NewValue { get; }
}
