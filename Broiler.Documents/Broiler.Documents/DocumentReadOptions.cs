using System;

namespace Broiler.Documents;

/// <summary>
/// Knobs for reading a document. Format-neutral at this level; format-specific
/// options derive from these (for example <c>PdfReadOptions</c>). Defaults are
/// safe: embedded binary objects are <b>not</b> decoded (ADR 0004).
/// </summary>
/// <remarks>
/// The type is open for derivation so a codec can carry its own immutable option
/// object through the shared <see cref="DocumentCodec.Read"/> signature. Only
/// behavior genuinely shared by several codecs belongs here; a codec validates
/// the concrete option type it was handed rather than downcasting opportunistically
/// (PDF roadmap §6.1).
/// </remarks>
public class DocumentReadOptions
{
    /// <summary>Windows-1252, the RTF default when no <c>\ansicpg</c> is present.</summary>
    public const int Windows1252CodePage = 1252;

    public static DocumentReadOptions Default { get; } = new();

    public DocumentReadOptions(
        DocumentLimits? limits = null,
        int defaultCodePage = Windows1252CodePage,
        bool decodeEmbeddedObjects = false)
    {
        if (defaultCodePage <= 0)
            throw new ArgumentOutOfRangeException(nameof(defaultCodePage));

        Limits = limits ?? DocumentLimits.Default;
        DefaultCodePage = defaultCodePage;
        DecodeEmbeddedObjects = decodeEmbeddedObjects;
    }

    public DocumentLimits Limits { get; }

    /// <summary>Fallback code page for <c>\'hh</c> bytes when the document declares none.</summary>
    public int DefaultCodePage { get; }

    /// <summary>
    /// When true, a codec may decode embedded images through a delegated image
    /// codec (still limit-bounded). Off by default; embedded OLE objects are
    /// never instantiated regardless (ADR 0004).
    /// </summary>
    public bool DecodeEmbeddedObjects { get; }
}
