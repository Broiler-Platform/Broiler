using System.IO.Compression;
using System.Text;

namespace Broiler.Documents.Docx.Tests;

/// <summary>
/// Builds minimal DOCX packages around a hand-written <c>w:body</c> so reader
/// tests can state the exact WordprocessingML they exercise. Only the parts the
/// reader needs are written: the content types, the package relationship that
/// points at the main document, and the parts a test asks for.
/// </summary>
internal static class DocxTestPackage
{
    public const string BodyNamespaceDeclarations =
        "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
        "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" " +
        "xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"";

    /// <summary>Wraps <paramref name="bodyXml"/> in a document part and zips a package around it.</summary>
    public static byte[] FromBody(string bodyXml, IReadOnlyDictionary<string, string>? extraParts = null)
    {
        string documentXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<w:document " + BodyNamespaceDeclarations + ">" +
            "<w:body>" + bodyXml + "</w:body>" +
            "</w:document>";

        var parts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["[Content_Types].xml"] =
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Override PartName=\"/word/document.xml\" ContentType=\"" +
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
                "</Types>",
            ["_rels/.rels"] =
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" " +
                "Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" " +
                "Target=\"word/document.xml\"/>" +
                "</Relationships>",
            ["word/document.xml"] = documentXml,
        };

        if (extraParts is not null)
        {
            foreach (KeyValuePair<string, string> part in extraParts)
                parts[part.Key] = part.Value;
        }

        return Zip(parts);
    }

    /// <summary>Reads a package built by <see cref="FromBody"/> through the public codec.</summary>
    public static DocumentReadResult ReadBody(string bodyXml, DocumentReadOptions? options = null)
    {
        using var stream = new MemoryStream(FromBody(bodyXml), writable: false);
        return new DocxDocumentCodec().Read(stream, options);
    }

    /// <summary>A <c>w:p</c> holding a single run of <paramref name="text"/>.</summary>
    public static string Paragraph(string text) =>
        "<w:p><w:r><w:t xml:space=\"preserve\">" + Escape(text) + "</w:t></w:r></w:p>";

    /// <summary>A table whose rows are given as arrays of cell contents.</summary>
    public static string Table(params string[][] rows)
    {
        var builder = new StringBuilder("<w:tbl><w:tblPr><w:tblW w:w=\"9000\" w:type=\"dxa\"/></w:tblPr><w:tblGrid/>");
        foreach (string[] row in rows)
        {
            builder.Append("<w:tr><w:trPr/>");
            foreach (string cell in row)
                builder.Append("<w:tc><w:tcPr/>").Append(cell).Append("</w:tc>");
            builder.Append("</w:tr>");
        }

        return builder.Append("</w:tbl>").ToString();
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static byte[] Zip(IReadOnlyDictionary<string, string> parts)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (KeyValuePair<string, string> part in parts)
            {
                ZipArchiveEntry entry = archive.CreateEntry(part.Key, CompressionLevel.NoCompression);
                using Stream stream = entry.Open();
                byte[] bytes = Encoding.UTF8.GetBytes(part.Value);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        return buffer.ToArray();
    }
}
