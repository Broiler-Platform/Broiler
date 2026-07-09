using System;

namespace Broiler.UI.Button;

public sealed class UiButtonClickEventArgs : EventArgs
{
    public UiButtonClickEventArgs(UiButtonActivationReason reason)
    {
        Reason = reason;
    }

    public UiButtonActivationReason Reason { get; }
}
