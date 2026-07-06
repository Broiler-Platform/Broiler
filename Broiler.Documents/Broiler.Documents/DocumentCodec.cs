using System;
using System.IO;
using Broiler.Documents.Model;

namespace Broiler.Documents;

/// <summary>
/// A document-format codec: identifies its format (<see cref="Descriptor"/>),
/// probes a byte prefix, and reads/writes the rich-text document model. Codecs
/// are registered explicitly into a <see cref="DocumentCodecCatalog"/> — there is
/// no hidden global registration (ADR 0003).
/// </summary>
public abstract class DocumentCodec
{
    protected DocumentCodec(DocumentFormatDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    public DocumentFormatDescriptor Descriptor { get; }

    public string Name => Descriptor.Name;

    /// <summary>True when <see cref="Read"/> is implemented for this format.</summary>
    public abstract bool CanRead { get; }

    /// <summary>True when <see cref="Write"/> is implemented for this format.</summary>
    public abstract bool CanWrite { get; }

    /// <summary>Judge whether a byte prefix is this codec's format.</summary>
    public abstract DocumentProbeResult Probe(DocumentProbeRequest request);

    /// <summary>
    /// Read a document into the model. Recoverable problems surface as
    /// diagnostics on the result rather than exceptions; only hard I/O or
    /// limit-exceeded conditions throw (<see cref="DocumentException"/>).
    /// </summary>
    public abstract DocumentReadResult Read(Stream source, DocumentReadOptions? options = null);

    /// <summary>Write a model document to the destination in this format.</summary>
    public abstract DocumentWriteResult Write(
        RichTextDocument document,
        Stream destination,
        DocumentWriteOptions? options = null);
}
