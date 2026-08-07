using System;
using System.Collections.Generic;

namespace Broiler.UI.ListView;

public sealed class UiListSelectionChangedEventArgs : EventArgs
{
    public UiListSelectionChangedEventArgs(string? oldItemId, string? newItemId)
        : this(oldItemId, newItemId, oldItemId is null ? [] : [oldItemId], newItemId is null ? [] : [newItemId])
    {
    }

    public UiListSelectionChangedEventArgs(
        string? oldItemId,
        string? newItemId,
        IReadOnlyList<string> oldItemIds,
        IReadOnlyList<string> newItemIds)
    {
        OldItemId = oldItemId;
        NewItemId = newItemId;
        OldItemIds = oldItemIds;
        NewItemIds = newItemIds;
    }

    /// <summary>The previous primary selection — the anchor a range extends from.</summary>
    public string? OldItemId { get; }

    /// <summary>The current primary selection.</summary>
    public string? NewItemId { get; }

    /// <summary>
    /// Everything that was selected before the change, in item order. Single-selection
    /// lists report at most one, so a handler written for them keeps working.
    /// </summary>
    public IReadOnlyList<string> OldItemIds { get; }

    /// <summary>Everything selected after the change, in item order.</summary>
    public IReadOnlyList<string> NewItemIds { get; }
}
