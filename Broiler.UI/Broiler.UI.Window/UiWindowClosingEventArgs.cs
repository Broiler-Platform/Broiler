using System;

namespace Broiler.UI.Window;

public sealed class UiWindowClosingEventArgs : EventArgs
{
    public UiWindowClosingEventArgs(UiWindowCloseReason reason)
    {
        Reason = reason;
    }

    public UiWindowCloseReason Reason { get; }

    public bool Cancel { get; set; }
}
