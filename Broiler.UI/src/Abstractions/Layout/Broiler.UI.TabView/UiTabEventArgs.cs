using System;

namespace Broiler.UI.TabView;

/// <summary>
/// A request to close a tab. The control raises it and does nothing else — a
/// host that ignores it leaves the tab open.
///
/// This is the whole point of the request shape. Save/Discard/Cancel cannot be
/// expressed by a control that has already closed the document, so the control
/// never removes a tab in response to a user gesture; it asks.
/// </summary>
public sealed class UiTabCloseRequestedEventArgs(UiTabItem tab) : EventArgs
{
    public UiTabItem Tab { get; } = tab;

    public string Id => Tab.Id;
}

/// <summary>
/// A reorder about to be applied, raised before the move so a host can persist
/// the new order or refuse it.
/// </summary>
public sealed class UiTabReorderingEventArgs(UiTabItem tab, int fromIndex, int toIndex) : EventArgs
{
    public UiTabItem Tab { get; } = tab;

    public int FromIndex { get; } = fromIndex;

    public int ToIndex { get; } = toIndex;

    /// <summary>Set to true to leave the order unchanged.</summary>
    public bool Cancel { get; set; }
}

public sealed class UiTabChangedEventArgs(UiTabItem tab) : EventArgs
{
    public UiTabItem Tab { get; } = tab;
}
