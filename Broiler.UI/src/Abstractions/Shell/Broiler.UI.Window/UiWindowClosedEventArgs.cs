using System;

namespace Broiler.UI.Window;

public sealed class UiWindowClosedEventArgs : EventArgs
{
    public UiWindowClosedEventArgs(UiWindowCloseReason reason)
    {
        Reason = reason;
    }

    public UiWindowCloseReason Reason { get; }
}
