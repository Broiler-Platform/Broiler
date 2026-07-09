using System;
using System.Collections.Generic;

namespace Broiler.UI.Menu;

public sealed class UiMenuItemInvokedEventArgs : EventArgs
{
    public UiMenuItemInvokedEventArgs(UiMenuItem item, IReadOnlyList<int> path)
    {
        Item = item;
        Path = path;
    }

    public UiMenuItem Item { get; }

    public IReadOnlyList<int> Path { get; }
}
