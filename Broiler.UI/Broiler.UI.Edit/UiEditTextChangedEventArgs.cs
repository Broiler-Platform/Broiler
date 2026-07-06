using System;

namespace Broiler.UI.Edit;

public sealed class UiEditTextChangedEventArgs : EventArgs
{
    public UiEditTextChangedEventArgs(string oldText, string newText)
    {
        OldText = oldText;
        NewText = newText;
    }

    public string OldText { get; }

    public string NewText { get; }
}
