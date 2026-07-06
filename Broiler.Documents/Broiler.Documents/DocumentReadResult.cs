using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Broiler.Documents.Model;

namespace Broiler.Documents;

/// <summary>
/// The outcome of reading a document: a best-effort <see cref="RichTextDocument"/>
/// plus any diagnostics. Reads do not throw on malformed-but-recoverable input
/// (ADR 0003/0004); unsupported or skipped constructs surface as diagnostics.
/// </summary>
public sealed class DocumentReadResult
{
    public DocumentReadResult(RichTextDocument document, IEnumerable<DocumentDiagnostic>? diagnostics = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Diagnostics = diagnostics is null
            ? EmptyDiagnostics
            : Array.AsReadOnly(diagnostics.ToArray());
    }

    private static readonly ReadOnlyCollection<DocumentDiagnostic> EmptyDiagnostics =
        Array.AsReadOnly(Array.Empty<DocumentDiagnostic>());

    public RichTextDocument Document { get; }

    public IReadOnlyList<DocumentDiagnostic> Diagnostics { get; }

    public bool HasErrors => Diagnostics.Any(static d => d.Severity == DocumentDiagnosticSeverity.Error);
}
