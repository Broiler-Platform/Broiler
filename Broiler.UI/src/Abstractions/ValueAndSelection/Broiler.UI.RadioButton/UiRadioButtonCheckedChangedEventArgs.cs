using System;

namespace Broiler.UI.RadioButton;

public sealed class UiRadioButtonCheckedChangedEventArgs : EventArgs
{
    public UiRadioButtonCheckedChangedEventArgs(bool oldValue, bool newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public bool OldValue { get; }

    public bool NewValue { get; }
}
