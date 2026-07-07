using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Broiler.Documents.Model;
using Broiler.Graphics;

namespace Broiler.Documents.Docx;

/// <summary>Serializes the rich-text document model to a minimal DOCX package.</summary>
public static class DocxWriter
{
    private static readonly DateTimeOffset ZipTimestamp =
        new(new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    public static DocumentWriteResult Write(
        RichTextDocument document,
        Stream destination,
        DocumentWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        _ = options;

        var context = new DocxWriteContext(document.Paragraphs.Any(static p => p.Style.ListKind != ListKind.None));
        XDocument documentXml = BuildDocumentXml(document, context);

        using var package = new MemoryStream();
        using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddXmlEntry(archive, "[Content_Types].xml", BuildContentTypes(context.HasNumbering));
            AddXmlEntry(archive, "_rels/.rels", BuildPackageRelationships());
            AddXmlEntry(archive, "word/document.xml", documentXml);
            if (context.HasDocumentRelationships)
                AddXmlEntry(archive, "word/_rels/document.xml.rels", BuildDocumentRelationships(context));
            if (context.HasNumbering)
                AddXmlEntry(archive, "word/numbering.xml", BuildNumbering());
        }

        byte[] bytes = package.ToArray();
        destination.Write(bytes, 0, bytes.Length);
        return new DocumentWriteResult(bytes.Length, context.Diagnostics);
    }

    public static byte[] WriteToArray(RichTextDocument document, DocumentWriteOptions? options = null)
    {
        using var stream = new MemoryStream();
        Write(document, stream, options);
        return stream.ToArray();
    }

    private static XDocument BuildDocumentXml(RichTextDocument document, DocxWriteContext context)
    {
        var body = new XElement(DocxNamespaces.Wordprocessing + "body");
        foreach (RichTextParagraph paragraph in document.Paragraphs)
            body.Add(BuildParagraph(paragraph, context));

        body.Add(new XElement(DocxNamespaces.Wordprocessing + "sectPr"));

        return new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement(
                DocxNamespaces.Wordprocessing + "document",
                new XAttribute(XNamespace.Xmlns + "w", DocxNamespaces.Wordprocessing.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "r", DocxNamespaces.Relationships.NamespaceName),
                body));
    }

    private static XElement BuildParagraph(RichTextParagraph paragraph, DocxWriteContext context)
    {
        var element = new XElement(DocxNamespaces.Wordprocessing + "p");
        XElement? properties = BuildParagraphProperties(paragraph.Style);
        if (properties is not null)
            element.Add(properties);

        int offset = 0;
        foreach (StyleRun run in paragraph.Runs)
        {
            string text = paragraph.Text.Substring(offset, run.Length);
            offset += run.Length;
            AddRun(element, text, run.Style, context);
        }

        return element;
    }

    private static XElement? BuildParagraphProperties(ParagraphStyle style)
    {
        var properties = new XElement(DocxNamespaces.Wordprocessing + "pPr");

        if (style.ListKind != ListKind.None)
        {
            int level = Math.Clamp(style.IndentLevel <= 0 ? 0 : style.IndentLevel - 1, 0, 8);
            int numId = style.ListKind == ListKind.Numbered ? 2 : 1;
            properties.Add(new XElement(
                DocxNamespaces.Wordprocessing + "numPr",
                new XElement(DocxNamespaces.Wordprocessing + "ilvl", WordAttribute("val", level.ToString(CultureInfo.InvariantCulture))),
                new XElement(DocxNamespaces.Wordprocessing + "numId", WordAttribute("val", numId.ToString(CultureInfo.InvariantCulture)))));
        }

        if (style.Alignment == TextAlignment.Center)
            properties.Add(new XElement(DocxNamespaces.Wordprocessing + "jc", WordAttribute("val", "center")));
        else if (style.Alignment == TextAlignment.Right)
            properties.Add(new XElement(DocxNamespaces.Wordprocessing + "jc", WordAttribute("val", "right")));

        if (Math.Abs(style.LineSpacing - 1f) > 0.001f ||
            Math.Abs(style.SpacingBefore) > 0.001f ||
            Math.Abs(style.SpacingAfter) > 0.001f)
        {
            var spacing = new XElement(DocxNamespaces.Wordprocessing + "spacing");
            if (Math.Abs(style.SpacingBefore) > 0.001f)
                spacing.Add(WordAttribute("before", Twips(style.SpacingBefore).ToString(CultureInfo.InvariantCulture)));
            if (Math.Abs(style.SpacingAfter) > 0.001f)
                spacing.Add(WordAttribute("after", Twips(style.SpacingAfter).ToString(CultureInfo.InvariantCulture)));
            if (Math.Abs(style.LineSpacing - 1f) > 0.001f)
            {
                spacing.Add(WordAttribute("line", Math.Max(1, (int)Math.Round(style.LineSpacing * 240f)).ToString(CultureInfo.InvariantCulture)));
                spacing.Add(WordAttribute("lineRule", "auto"));
            }

            properties.Add(spacing);
        }

        if (style.IndentLevel > 0)
        {
            properties.Add(new XElement(
                DocxNamespaces.Wordprocessing + "ind",
                WordAttribute("left", (style.IndentLevel * 360).ToString(CultureInfo.InvariantCulture))));
        }

        return properties.HasElements ? properties : null;
    }

    private static void AddRun(XElement parent, string text, InlineStyle style, DocxWriteContext context)
    {
        if (!string.IsNullOrEmpty(style.LinkHref))
        {
            XElement? hyperlink = BuildHyperlink(text, style, context);
            if (hyperlink is not null)
            {
                parent.Add(hyperlink);
                return;
            }
        }

        parent.Add(BuildRun(text, style, context));
    }

    private static XElement? BuildHyperlink(string text, InlineStyle style, DocxWriteContext context)
    {
        string href = style.LinkHref ?? string.Empty;
        if (href.StartsWith("#", StringComparison.Ordinal) && href.Length > 1)
        {
            return new XElement(
                DocxNamespaces.Wordprocessing + "hyperlink",
                WordAttribute("anchor", href[1..]),
                BuildRun(text, style with { LinkHref = null }, context));
        }

        if (!IsExternalLink(href))
        {
            context.AddDiagnosticOnce("docx.link", "A hyperlink with a disallowed or relative target was written as plain text.");
            return null;
        }

        string relationshipId = context.GetHyperlinkRelationshipId(href);
        return new XElement(
            DocxNamespaces.Wordprocessing + "hyperlink",
            new XAttribute(DocxNamespaces.Relationships + "id", relationshipId),
            new XAttribute(DocxNamespaces.Wordprocessing + "history", "1"),
            BuildRun(text, style with { LinkHref = null }, context));
    }

    private static XElement BuildRun(string text, InlineStyle style, DocxWriteContext context)
    {
        var run = new XElement(DocxNamespaces.Wordprocessing + "r");
        XElement? properties = BuildRunProperties(style, context);
        if (properties is not null)
            run.Add(properties);
        AddTextContent(run, text);
        return run;
    }

    private static XElement? BuildRunProperties(InlineStyle style, DocxWriteContext context)
    {
        var properties = new XElement(DocxNamespaces.Wordprocessing + "rPr");
        if (style.Bold)
            properties.Add(new XElement(DocxNamespaces.Wordprocessing + "b"));
        if (style.Italic)
            properties.Add(new XElement(DocxNamespaces.Wordprocessing + "i"));
        if (style.Underline)
            properties.Add(new XElement(DocxNamespaces.Wordprocessing + "u", WordAttribute("val", "single")));
        if (style.Strikethrough)
            properties.Add(new XElement(DocxNamespaces.Wordprocessing + "strike"));

        if (!string.IsNullOrWhiteSpace(style.FontFamily))
        {
            properties.Add(new XElement(
                DocxNamespaces.Wordprocessing + "rFonts",
                WordAttribute("ascii", style.FontFamily),
                WordAttribute("hAnsi", style.FontFamily),
                WordAttribute("cs", style.FontFamily),
                WordAttribute("eastAsia", style.FontFamily)));
        }

        if (style.FontSize.HasValue)
        {
            int halfPoints = Math.Max(1, (int)Math.Round(style.FontSize.Value * 2f));
            properties.Add(new XElement(
                DocxNamespaces.Wordprocessing + "sz",
                WordAttribute("val", halfPoints.ToString(CultureInfo.InvariantCulture))));
        }

        if (!style.Foreground.IsEmpty)
        {
            properties.Add(new XElement(
                DocxNamespaces.Wordprocessing + "color",
                WordAttribute("val", FormatColor(style.Foreground, context))));
        }

        if (!style.Background.IsEmpty)
        {
            properties.Add(new XElement(
                DocxNamespaces.Wordprocessing + "shd",
                WordAttribute("val", "clear"),
                WordAttribute("color", "auto"),
                WordAttribute("fill", FormatColor(style.Background, context))));
        }

        return properties.HasElements ? properties : null;
    }

    private static void AddTextContent(XElement run, string text)
    {
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\t' && text[i] != (char)0x2028)
                continue;

            if (i > start)
                run.Add(TextElement(text[start..i]));

            run.Add(text[i] == '\t'
                ? new XElement(DocxNamespaces.Wordprocessing + "tab")
                : new XElement(DocxNamespaces.Wordprocessing + "br"));
            start = i + 1;
        }

        if (start < text.Length)
            run.Add(TextElement(text[start..]));
    }

    private static XElement TextElement(string value) =>
        new(
            DocxNamespaces.Wordprocessing + "t",
            new XAttribute(DocxNamespaces.Xml + "space", "preserve"),
            value);

    private static XDocument BuildContentTypes(bool includeNumbering)
    {
        var types = new XElement(
            DocxNamespaces.ContentTypes + "Types",
            new XAttribute(XNamespace.Xmlns + "ct", DocxNamespaces.ContentTypes.NamespaceName),
            new XElement(
                DocxNamespaces.ContentTypes + "Default",
                new XAttribute("Extension", "rels"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
            new XElement(
                DocxNamespaces.ContentTypes + "Default",
                new XAttribute("Extension", "xml"),
                new XAttribute("ContentType", "application/xml")),
            new XElement(
                DocxNamespaces.ContentTypes + "Override",
                new XAttribute("PartName", "/word/document.xml"),
                new XAttribute("ContentType", DocxNamespaces.DocumentContentType)));

        if (includeNumbering)
        {
            types.Add(new XElement(
                DocxNamespaces.ContentTypes + "Override",
                new XAttribute("PartName", "/word/numbering.xml"),
                new XAttribute("ContentType", DocxNamespaces.NumberingContentType)));
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), types);
    }

    private static XDocument BuildPackageRelationships() =>
        new(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement(
                DocxNamespaces.PackageRelationships + "Relationships",
                new XAttribute(XNamespace.Xmlns + "rel", DocxNamespaces.PackageRelationships.NamespaceName),
                new XElement(
                    DocxNamespaces.PackageRelationships + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", DocxNamespaces.OfficeDocumentRelationship),
                    new XAttribute("Target", "word/document.xml"))));

    private static XDocument BuildDocumentRelationships(DocxWriteContext context)
    {
        var root = new XElement(
            DocxNamespaces.PackageRelationships + "Relationships",
            new XAttribute(XNamespace.Xmlns + "rel", DocxNamespaces.PackageRelationships.NamespaceName));

        if (context.HasNumbering)
        {
            root.Add(new XElement(
                DocxNamespaces.PackageRelationships + "Relationship",
                new XAttribute("Id", context.NumberingRelationshipId),
                new XAttribute("Type", DocxNamespaces.NumberingRelationship),
                new XAttribute("Target", "numbering.xml")));
        }

        foreach (KeyValuePair<string, string> relationship in context.HyperlinkRelationships.OrderBy(pair => pair.Value, StringComparer.Ordinal))
        {
            root.Add(new XElement(
                DocxNamespaces.PackageRelationships + "Relationship",
                new XAttribute("Id", relationship.Value),
                new XAttribute("Type", DocxNamespaces.HyperlinkRelationship),
                new XAttribute("Target", relationship.Key),
                new XAttribute("TargetMode", "External")));
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
    }

    private static XDocument BuildNumbering()
    {
        var root = new XElement(
            DocxNamespaces.Wordprocessing + "numbering",
            new XAttribute(XNamespace.Xmlns + "w", DocxNamespaces.Wordprocessing.NamespaceName));

        root.Add(BuildAbstractNumber(1, "bullet", "\u2022"));
        root.Add(BuildAbstractNumber(2, "decimal", "%1."));
        root.Add(new XElement(
            DocxNamespaces.Wordprocessing + "num",
            WordAttribute("numId", "1"),
            new XElement(DocxNamespaces.Wordprocessing + "abstractNumId", WordAttribute("val", "1"))));
        root.Add(new XElement(
            DocxNamespaces.Wordprocessing + "num",
            WordAttribute("numId", "2"),
            new XElement(DocxNamespaces.Wordprocessing + "abstractNumId", WordAttribute("val", "2"))));

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
    }

    private static XElement BuildAbstractNumber(int id, string format, string levelText)
    {
        var abstractNum = new XElement(
            DocxNamespaces.Wordprocessing + "abstractNum",
            WordAttribute("abstractNumId", id.ToString(CultureInfo.InvariantCulture)));

        for (int level = 0; level < 9; level++)
        {
            abstractNum.Add(new XElement(
                DocxNamespaces.Wordprocessing + "lvl",
                WordAttribute("ilvl", level.ToString(CultureInfo.InvariantCulture)),
                new XElement(DocxNamespaces.Wordprocessing + "start", WordAttribute("val", "1")),
                new XElement(DocxNamespaces.Wordprocessing + "numFmt", WordAttribute("val", format)),
                new XElement(DocxNamespaces.Wordprocessing + "lvlText", WordAttribute("val", levelText)),
                new XElement(
                    DocxNamespaces.Wordprocessing + "pPr",
                    new XElement(
                        DocxNamespaces.Wordprocessing + "ind",
                        WordAttribute("left", ((level + 1) * 360).ToString(CultureInfo.InvariantCulture)),
                        WordAttribute("hanging", "360")))));
        }

        return abstractNum;
    }

    private static void AddXmlEntry(ZipArchive archive, string path, XDocument document)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        entry.LastWriteTime = ZipTimestamp;
        using Stream stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            NewLineHandling = NewLineHandling.None,
        });
        document.Save(writer);
    }

    private static bool IsExternalLink(string href)
    {
        if (!Uri.TryCreate(href, UriKind.Absolute, out Uri? uri))
            return false;

        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase);
    }

    private static XAttribute WordAttribute(string name, string value) =>
        new(DocxNamespaces.Wordprocessing + name, value);

    private static int Twips(float points) => (int)Math.Round(points * 20f);

    private static string FormatColor(BColor color, DocxWriteContext context)
    {
        if (color.A != 255)
            context.AddDiagnosticOnce("docx.color.alpha", "DOCX colors do not preserve alpha; RGB channels were written.");

        return string.Create(CultureInfo.InvariantCulture, $"{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    private sealed class DocxWriteContext
    {
        private readonly Dictionary<string, string> _hyperlinks = new(StringComparer.Ordinal);
        private readonly List<DocumentDiagnostic> _diagnostics = [];
        private readonly HashSet<string> _diagnosticOnce = new(StringComparer.Ordinal);
        private int _nextRelationshipId;

        public DocxWriteContext(bool hasNumbering)
        {
            HasNumbering = hasNumbering;
            _nextRelationshipId = hasNumbering ? 2 : 1;
        }

        public bool HasNumbering { get; }

        public bool HasDocumentRelationships => HasNumbering || _hyperlinks.Count > 0;

        public string NumberingRelationshipId => "rId1";

        public IReadOnlyDictionary<string, string> HyperlinkRelationships => _hyperlinks;

        public IReadOnlyList<DocumentDiagnostic> Diagnostics => _diagnostics;

        public string GetHyperlinkRelationshipId(string href)
        {
            if (_hyperlinks.TryGetValue(href, out string? existing))
                return existing;

            string id = "rId" + _nextRelationshipId.ToString(CultureInfo.InvariantCulture);
            _nextRelationshipId++;
            _hyperlinks[href] = id;
            return id;
        }

        public void AddDiagnosticOnce(string code, string message)
        {
            if (_diagnosticOnce.Add(code))
                _diagnostics.Add(DocumentDiagnostic.Warning(code, message));
        }
    }
}
