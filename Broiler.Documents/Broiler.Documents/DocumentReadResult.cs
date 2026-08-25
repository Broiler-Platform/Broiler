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
/// <remarks>
/// Open for derivation so a codec can return the same result through the shared
/// <see cref="DocumentCodec.Read"/> signature while adding format-specific detail
/// (<c>PdfReadResult</c> adds a success/partial/rejected status, page count, and
/// normalized metadata). Format-specific state never moves into this base until a
/// second non-PDF consumer exists (PDF roadmap §3 containment rule).
/// </remarks>
public class DocumentReadResult
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
