using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Broiler.Documents;

/// <summary>
/// The outcome of writing a document: the number of bytes written to the
/// destination plus any diagnostics (styles or constructs that could not be
/// represented in the target format).
/// </summary>
public sealed class DocumentWriteResult
{
    public DocumentWriteResult(long bytesWritten, IEnumerable<DocumentDiagnostic>? diagnostics = null)
    {
        if (bytesWritten < 0)
            throw new ArgumentOutOfRangeException(nameof(bytesWritten));

        BytesWritten = bytesWritten;
        Diagnostics = diagnostics is null
            ? EmptyDiagnostics
            : Array.AsReadOnly(diagnostics.ToArray());
    }

    private static readonly ReadOnlyCollection<DocumentDiagnostic> EmptyDiagnostics =
        Array.AsReadOnly(Array.Empty<DocumentDiagnostic>());

    public long BytesWritten { get; }

    public IReadOnlyList<DocumentDiagnostic> Diagnostics { get; }
}
