using System;
using Broiler.Documents.Model;

namespace Broiler.UI.RichEdit;

/// <summary>Raised after the selection changes.</summary>
public sealed class RichEditSelectionChangedEventArgs : EventArgs
{
    public RichEditSelectionChangedEventArgs(RichTextRange selection)
    {
        Selection = selection;
    }

    public RichTextRange Selection { get; }
}
