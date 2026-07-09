using System;

namespace Broiler.UI.RichEdit;

/// <summary>
/// Raised when the control is submitted (for example Enter while
/// <see cref="UiRichEdit.AcceptsReturn"/> is false).
/// </summary>
public sealed class RichEditSubmittedEventArgs : EventArgs
{
    public RichEditSubmittedEventArgs(string plainText)
    {
        PlainText = plainText;
    }

    public string PlainText { get; }
}
