using System.Xml.Linq;

namespace Broiler.Documents.Docx;

internal static class DocxNamespaces
{
    public static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
    public static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    public static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public static readonly XNamespace Wordprocessing = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static readonly XNamespace Xml = "http://www.w3.org/XML/1998/namespace";

    public const string OfficeDocumentRelationship =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";

    public const string HyperlinkRelationship =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink";

    public const string NumberingRelationship =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering";

    public const string DocumentContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    public const string NumberingContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml";

    public const string PackageContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
}
