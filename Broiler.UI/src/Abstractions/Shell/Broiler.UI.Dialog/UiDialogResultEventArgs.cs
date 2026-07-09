using System;

namespace Broiler.UI.Dialog;

public sealed class UiDialogResultEventArgs : EventArgs
{
    public UiDialogResultEventArgs(UiDialogResult result)
    {
        Result = result;
    }

    public UiDialogResult Result { get; }
}
