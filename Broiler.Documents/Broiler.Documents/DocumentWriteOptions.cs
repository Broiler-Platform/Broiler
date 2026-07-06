namespace Broiler.Documents;

/// <summary>
/// Knobs for writing a document. Format-neutral at this level; format-specific
/// options derive from these.
/// </summary>
public sealed class DocumentWriteOptions
{
    public static DocumentWriteOptions Default { get; } = new();

    public DocumentWriteOptions(bool asciiOnly = true)
    {
        AsciiOnly = asciiOnly;
    }

    /// <summary>
    /// When true, non-ASCII characters are escaped into the format's portable
    /// representation (for RTF, <c>\uN</c> with an ASCII fallback char) rather
    /// than emitted as raw bytes.
    /// </summary>
    public bool AsciiOnly { get; }
}
