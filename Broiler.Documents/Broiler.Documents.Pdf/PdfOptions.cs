using System;
using System.Threading;
using Broiler.Documents.Pdf.Text;

namespace Broiler.Documents.Pdf;

/// <summary>
/// Whether a read or write produced a usable result, independently of whether it
/// produced diagnostics.
/// </summary>
/// <remarks>
/// Severity and status answer different questions. A document can carry a dozen
/// warnings and still be a complete rendering of its supported subset
/// (<see cref="Success"/>); another can carry one warning that means a page was
/// dropped (<see cref="Partial"/>). Hosts branch on this, never on a diagnostic
/// count.
/// </remarks>
public enum PdfResultStatus
{
    /// <summary>The declared supported subset was processed; the result is usable.</summary>
    Success,

    /// <summary>
    /// A usable result exists, but named content or features were skipped or
    /// remain uncertain. A host must have the caller opt in before replacing an
    /// open document or publishing this output.
    /// </summary>
    Partial,

    /// <summary>No usable result exists and none may be accepted.</summary>
    Rejected,
}

/// <summary>How far a write got before it stopped.</summary>
public enum PdfDestinationState
{
    /// <summary>Nothing was written; the destination is untouched.</summary>
    NotStarted,

    /// <summary>The complete output reached the destination.</summary>
    Committed,

    /// <summary>
    /// Bytes reached a caller-owned stream and then the write stopped. The prefix
    /// is not a valid PDF and the caller owns discarding it.
    /// </summary>
    PartialDestination,
}

/// <summary>Options for reading a PDF.</summary>
public sealed class PdfReadOptions : DocumentReadOptions
{
    public static new PdfReadOptions Default { get; } = new();

    public PdfReadOptions(
        DocumentLimits? limits = null,
        PdfLimits? pdfLimits = null,
        bool mapPageBreaks = false,
        bool includeInvisibleText = true,
        PdfUriPolicy? uriPolicy = null,
        CancellationToken cancellationToken = default)
        : base(limits)
    {
        PdfLimits = pdfLimits ?? PdfLimits.Default;
        MapPageBreaks = mapPageBreaks;
        IncludeInvisibleText = includeInvisibleText;
        UriPolicy = uriPolicy;
        CancellationToken = cancellationToken;
    }

    /// <summary>The PDF-specific budgets, composed with the shared limits.</summary>
    public PdfLimits PdfLimits { get; }

    /// <summary>
    /// When true, a source page boundary becomes a paragraph break in the model.
    /// Off by default: page boundaries are extraction boundaries, and mapping them
    /// would imply a layout fidelity that re-pagination cannot keep.
    /// </summary>
    public bool MapPageBreaks { get; }

    /// <summary>
    /// When true (the default), text drawn in an invisible or clipping-only
    /// rendering mode is extracted and flagged. Setting it false omits that text,
    /// which is <em>not</em> the same as proving it is invisible — this release
    /// makes no visibility claim in either direction.
    /// </summary>
    public bool IncludeInvisibleText { get; }

    /// <summary>
    /// Overrides the codec's composed URI policy for this read. Null uses the
    /// policy from <see cref="PdfCodecServices"/>.
    /// </summary>
    public PdfUriPolicy? UriPolicy { get; }

    /// <summary>Cancellation observed at every documented parsing checkpoint.</summary>
    public CancellationToken CancellationToken { get; }
}

/// <summary>Options for writing a PDF.</summary>
/// <remarks>
/// Nothing here reads the clock, the machine name, the locale, or the set of
/// installed fonts. Two writes of the same document with the same options produce
/// byte-identical output, which is what makes the writer testable and its output
/// diffable.
/// </remarks>
public sealed class PdfWriteOptions : DocumentWriteOptions
{
    public static new PdfWriteOptions Default { get; } = new();

    public PdfWriteOptions(
        PdfPageSetup? pageSetup = null,
        PdfDocumentMetadata? metadata = null,
        PdfLimits? pdfLimits = null,
        bool compressStreams = true,
        PdfFontFamilyKind defaultFamily = PdfFontFamilyKind.SansSerif,
        float defaultFontSize = 12f,
        string? fileIdentifier = null,
        PdfUriPolicy? uriPolicy = null,
        CancellationToken cancellationToken = default)
    {
        if (defaultFontSize is <= 0 or > 1600 || !float.IsFinite(defaultFontSize))
            throw new ArgumentOutOfRangeException(nameof(defaultFontSize));

        PageSetup = pageSetup ?? PdfPageSetup.Letter;
        Metadata = metadata ?? PdfDocumentMetadata.Empty;
        PdfLimits = pdfLimits ?? PdfLimits.Default;
        CompressStreams = compressStreams;
        DefaultFamily = defaultFamily;
        DefaultFontSize = defaultFontSize;
        FileIdentifier = fileIdentifier;
        UriPolicy = uriPolicy;
        CancellationToken = cancellationToken;
    }

    public PdfPageSetup PageSetup { get; }

    /// <summary>
    /// The metadata to emit. It comes from the caller, never from a read result:
    /// having read a document's Info dictionary is not authority to republish it.
    /// </summary>
    public PdfDocumentMetadata Metadata { get; }

    public PdfLimits PdfLimits { get; }

    /// <summary>When true, content streams are Flate-compressed.</summary>
    public bool CompressStreams { get; }

    /// <summary>The logical family used for runs that name no font family.</summary>
    public PdfFontFamilyKind DefaultFamily { get; }

    /// <summary>The point size used for runs that state none.</summary>
    public float DefaultFontSize { get; }

    /// <summary>
    /// The caller-controlled file identifier. When null, the writer derives one
    /// from the document's own content, so output stays deterministic without
    /// reading a clock or a machine identity.
    /// </summary>
    public string? FileIdentifier { get; }

    /// <summary>Overrides the composed URI policy for this write.</summary>
    public PdfUriPolicy? UriPolicy { get; }

    public CancellationToken CancellationToken { get; }
}

/// <summary>Page geometry for the writer, in points.</summary>
public sealed class PdfPageSetup
{
    public PdfPageSetup(
        double width,
        double height,
        double marginLeft = 72,
        double marginRight = 72,
        double marginTop = 72,
        double marginBottom = 72)
    {
        if (!double.IsFinite(width) || width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (!double.IsFinite(height) || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        foreach ((double margin, string name) in new[]
                 {
                     (marginLeft, nameof(marginLeft)),
                     (marginRight, nameof(marginRight)),
                     (marginTop, nameof(marginTop)),
                     (marginBottom, nameof(marginBottom)),
                 })
        {
            if (!double.IsFinite(margin) || margin < 0)
                throw new ArgumentOutOfRangeException(name);
        }

        if (marginLeft + marginRight >= width)
            throw new ArgumentException("The horizontal margins leave no content width.", nameof(marginLeft));
        if (marginTop + marginBottom >= height)
            throw new ArgumentException("The vertical margins leave no content height.", nameof(marginTop));

        Width = width;
        Height = height;
        MarginLeft = marginLeft;
        MarginRight = marginRight;
        MarginTop = marginTop;
        MarginBottom = marginBottom;
    }

    /// <summary>US Letter with one-inch margins.</summary>
    public static PdfPageSetup Letter { get; } = new(612, 792);

    /// <summary>ISO A4 with one-inch margins.</summary>
    public static PdfPageSetup A4 { get; } = new(595.28, 841.89);

    public double Width { get; }

    public double Height { get; }

    public double MarginLeft { get; }

    public double MarginRight { get; }

    public double MarginTop { get; }

    public double MarginBottom { get; }

    public double ContentWidth => Width - MarginLeft - MarginRight;

    public double ContentHeight => Height - MarginTop - MarginBottom;
}
