using System;

namespace Broiler.UI.ComboBox;

public sealed class UiComboBoxSelectionChangedEventArgs : EventArgs
{
    public UiComboBoxSelectionChangedEventArgs(int oldIndex, int newIndex)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    public int OldIndex { get; }

    public int NewIndex { get; }
}
