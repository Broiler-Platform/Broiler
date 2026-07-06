using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Broiler.Documents;

/// <summary>
/// An explicitly composed set of document codecs. The catalog selects a codec by
/// file extension, MIME type, or a confidence-ranked signature probe over a
/// bounded content prefix. There is no hidden global registry (ADR 0001/0003):
/// the composing application decides which codecs are present.
/// </summary>
public sealed class DocumentCodecCatalog
{
    private readonly ReadOnlyCollection<DocumentCodec> _codecs;

    public DocumentCodecCatalog(IEnumerable<DocumentCodec> codecs)
    {
        ArgumentNullException.ThrowIfNull(codecs);

        DocumentCodec[] array = codecs.ToArray();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DocumentCodec codec in array)
        {
            if (codec is null)
                throw new ArgumentException("The codec collection contains a null entry.", nameof(codecs));
            if (!names.Add(codec.Descriptor.Name))
                throw new ArgumentException($"Duplicate document codec '{codec.Descriptor.Name}'.", nameof(codecs));
        }

        _codecs = Array.AsReadOnly(array);
    }

    public IReadOnlyList<DocumentCodec> Codecs => _codecs;

    public DocumentCodec? FindByName(string name) =>
        _codecs.FirstOrDefault(codec => string.Equals(codec.Descriptor.Name, name, StringComparison.OrdinalIgnoreCase));

    public DocumentCodec? FindByExtension(string extension) =>
        _codecs.FirstOrDefault(codec => codec.Descriptor.MatchesExtension(extension));

    public DocumentCodec? FindByMimeType(string mimeType) =>
        _codecs.FirstOrDefault(codec => codec.Descriptor.MatchesMimeType(mimeType));

    /// <summary>Select the highest-confidence codec for a stream's leading bytes.</summary>
    public DocumentCodecMatch? Select(
        Stream source,
        DocumentSourceHints? hints = null,
        DocumentLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        DocumentLimits effective = limits ?? DocumentLimits.Default;
        byte[] prefix = ReadPrefix(source, effective.MaxProbeBytes);
        return Select(prefix, hints, effective);
    }

    /// <summary>Select the highest-confidence codec for an in-memory prefix.</summary>
    public DocumentCodecMatch? Select(
        ReadOnlyMemory<byte> prefix,
        DocumentSourceHints? hints = null,
        DocumentLimits? limits = null)
    {
        var request = new DocumentProbeRequest(prefix, hints, limits ?? DocumentLimits.Default);

        DocumentCodecMatch? best = null;
        foreach (DocumentCodec codec in _codecs)
        {
            DocumentProbeResult result = codec.Probe(request);
            if (!result.IsMatch)
                continue;
            if (best is null || result.Confidence > best.Result.Confidence)
                best = new DocumentCodecMatch(codec, result);
        }

        return best;
    }

    private static byte[] ReadPrefix(Stream stream, int maxBytes)
    {
        if (maxBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes));

        long? originalPosition = stream.CanSeek ? stream.Position : null;
        byte[] buffer = new byte[maxBytes];
        int total = 0;

        try
        {
            while (total < maxBytes)
            {
                int read = stream.Read(buffer, total, maxBytes - total);
                if (read == 0)
                    break;
                total += read;
            }
        }
        finally
        {
            if (originalPosition.HasValue)
                stream.Position = originalPosition.Value;
        }

        if (total == buffer.Length)
            return buffer;

        Array.Resize(ref buffer, total);
        return buffer;
    }
}
