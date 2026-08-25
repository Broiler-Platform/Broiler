using System;
using System.Collections.Generic;
using Broiler.Documents.Model;
using Broiler.Documents.Pdf.Structure;

namespace Broiler.Documents.Pdf;

/// <summary>
/// The outcome of reading a PDF: the extracted document plus what the reader
/// learned about the file.
/// </summary>
/// <remarks>
/// The status is the load-bearing field. <see cref="PdfResultStatus.Rejected"/>
/// means the <see cref="DocumentReadResult.Document"/> is a placeholder that no
/// host may present, and a rejected read never replaces an open document or
/// produces an output file.
/// </remarks>
public sealed class PdfReadResult : DocumentReadResult
{
    public PdfReadResult(
        RichTextDocument document,
        PdfResultStatus status,
        PdfDocumentMetadata metadata,
        PdfVersion declaredVersion,
        int pageCount,
        IReadOnlyList<PdfExtensionDeclaration> extensions,
        IEnumerable<DocumentDiagnostic>? diagnostics = null)
        : base(document, diagnostics)
    {
        Status = status;
        Metadata = metadata ?? PdfDocumentMetadata.Empty;
        DeclaredVersion = declaredVersion;
        PageCount = pageCount;
        Extensions = extensions ?? Array.Empty<PdfExtensionDeclaration>();
    }

    public PdfResultStatus Status { get; }

    /// <summary>The normalized metadata allowlist; never raw Info or XMP data.</summary>
    public PdfDocumentMetadata Metadata { get; }

    /// <summary>
    /// The version the file effectively declares, after the Catalog override. A
    /// 2.x value records what the file claims, not what this codec implements.
    /// </summary>
    public PdfVersion DeclaredVersion { get; }

    public int PageCount { get; }

    /// <summary>
    /// Developer extensions the Catalog declared. This is inventory for
    /// diagnostics; no declaration here ever enabled a feature.
    /// </summary>
    public IReadOnlyList<PdfExtensionDeclaration> Extensions { get; }

    /// <summary>True when a host may present the document to a user.</summary>
    public bool IsUsable => Status != PdfResultStatus.Rejected;
}

/// <summary>The outcome of writing a PDF.</summary>
public sealed class PdfWriteResult : DocumentWriteResult
{
    public PdfWriteResult(
        long bytesWritten,
        PdfResultStatus status,
        PdfDestinationState destinationState,
        int pageCount,
        IEnumerable<DocumentDiagnostic>? diagnostics = null)
        : base(bytesWritten, diagnostics)
    {
        Status = status;
        DestinationState = destinationState;
        PageCount = pageCount;
    }

    public PdfResultStatus Status { get; }

    /// <summary>
    /// How far the destination got. <see cref="PdfResultStatus.Success"/> requires
    /// <see cref="PdfDestinationState.Committed"/>; a rejection paired with
    /// <see cref="PdfDestinationState.PartialDestination"/> tells a caller-owned
    /// stream that an unusable prefix needs discarding.
    /// </summary>
    public PdfDestinationState DestinationState { get; }

    public int PageCount { get; }
}
