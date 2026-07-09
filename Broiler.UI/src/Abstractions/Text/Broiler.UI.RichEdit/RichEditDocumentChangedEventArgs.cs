using System;
using Broiler.Documents.Model;

namespace Broiler.UI.RichEdit;

/// <summary>Raised after the document content changes.</summary>
public sealed class RichEditDocumentChangedEventArgs : EventArgs
{
    public RichEditDocumentChangedEventArgs(RichTextDocument document)
    {
        Document = document;
    }

    public RichTextDocument Document { get; }
}
