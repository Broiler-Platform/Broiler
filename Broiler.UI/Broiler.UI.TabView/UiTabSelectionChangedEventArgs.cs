using System;

namespace Broiler.UI.TabView;

public sealed class UiTabSelectionChangedEventArgs : EventArgs
{
    public UiTabSelectionChangedEventArgs(int oldIndex, int newIndex)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    public int OldIndex { get; }

    public int NewIndex { get; }
}
