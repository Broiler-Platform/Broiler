using System;

namespace Broiler.UI.ListView;

public sealed class UiListSelectionChangedEventArgs : EventArgs
{
    public UiListSelectionChangedEventArgs(string? oldItemId, string? newItemId)
    {
        OldItemId = oldItemId;
        NewItemId = newItemId;
    }

    public string? OldItemId { get; }

    public string? NewItemId { get; }
}
