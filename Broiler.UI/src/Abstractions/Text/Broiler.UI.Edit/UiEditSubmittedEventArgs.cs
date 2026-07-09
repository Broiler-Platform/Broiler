using System;

namespace Broiler.UI.Edit;

public sealed class UiEditSubmittedEventArgs : EventArgs
{
    public UiEditSubmittedEventArgs(string text)
    {
        Text = text;
    }

    public string Text { get; }
}
