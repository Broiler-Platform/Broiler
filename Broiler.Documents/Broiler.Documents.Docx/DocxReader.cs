using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Broiler.Documents.Model;
using Broiler.Graphics;

namespace Broiler.Documents.Docx;

internal static class DocxReader
{
    public static DocumentReadResult Read(byte[] bytes, DocumentReadOptions options)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(options);

        var diagnostics = new List<DocumentDiagnostic>();
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            string documentPart = FindMainDocumentPart(archive, options.Limits, diagnostics) ?? "word/document.xml";
            ZipArchiveEntry? documentEntry = FindEntry(archive, documentPart);
            if (documentEntry is null)
            {
                diagnostics.Add(DocumentDiagnostic.Error(
                    "docx.package.document",
                    "DOCX package did not contain a main word/document.xml part."));
                return new DocumentReadResult(RichTextDocument.Empty, diagnostics);
            }

            XDocument? documentXml = LoadEntryXml(documentEntry, options.Limits, diagnostics, "docx.document.xml");
            if (documentXml is null)
                return new DocumentReadResult(RichTextDocument.Empty, diagnostics);

            DocxRelationships documentRelationships = ReadRelationships(
                archive,
                RelationshipsPartPath(documentPart),
                BasePartDirectory(documentPart),
                options.Limits,
                diagnostics);
            DocxNumbering numbering = DocxNumbering.Load(
                archive,
                documentRelationships,
                BasePartDirectory(documentPart),
                options.Limits,
                diagnostics);

            RichTextDocument document = ReadDocumentXml(
                documentXml,
                documentRelationships,
                numbering,
                options.Limits,
                diagnostics);
            return new DocumentReadResult(document, diagnostics);
        }
        catch (InvalidDataException ex)
        {
            diagnostics.Add(DocumentDiagnostic.Error(
                "docx.package.zip",
                "DOCX ZIP package could not be opened: " + ex.GetType().Name + "."));
            return new DocumentReadResult(RichTextDocument.Empty, diagnostics);
        }
        catch (XmlException ex)
        {
            diagnostics.Add(DocumentDiagnostic.Error(
                "docx.xml",
                "DOCX XML could not be parsed: " + ex.GetType().Name + "."));
            return new DocumentReadResult(RichTextDocument.Empty, diagnostics);
        }
    }

    private static RichTextDocument ReadDocumentXml(
        XDocument documentXml,
        DocxRelationships relationships,
        DocxNumbering numbering,
        DocumentLimits limits,
        List<DocumentDiagnostic> diagnostics)
    {
        XElement? body = documentXml.Root?.Element(DocxNamespaces.Wordprocessing + "body");
        if (body is null)
        {
            diagnostics.Add(DocumentDiagnostic.Error(
                "docx.document.body",
                "DOCX document.xml did not contain a WordprocessingML body."));
            return RichTextDocument.Empty;
        }

        var builder = new DocxDocumentBuilder(limits, diagnostics);
        foreach (XElement paragraph in body.Elements(DocxNamespaces.Wordprocessing + "p"))
            ReadParagraph(paragraph, relationships, numbering, builder);

        return builder.Build();
    }

    private static void ReadParagraph(
        XElement paragraph,
        DocxRelationships relationships,
        DocxNumbering numbering,
        DocxDocumentBuilder builder)
    {
        ParagraphStyle paragraphStyle = ReadParagraphStyle(paragraph.Element(DocxNamespaces.Wordprocessing + "pPr"), numbering);
        builder.StartParagraph(paragraphStyle);
        ReadParagraphChildren(paragraph.Elements(), relationships, builder, InlineStyle.Default);
        builder.FinishParagraph();
    }

    private static void ReadParagraphChildren(
        IEnumerable<XElement> elements,
        DocxRelationships relationships,
        DocxDocumentBuilder builder,
        InlineStyle style)
    {
        foreach (XElement element in elements)
        {
            if (element.Name == DocxNamespaces.Wordprocessing + "pPr")
                continue;

            if (element.Name == DocxNamespaces.Wordprocessing + "r")
            {
                ReadRun(element, relationships, builder, style);
                continue;
            }

            if (element.Name == DocxNamespaces.Wordprocessing + "hyperlink")
            {
                InlineStyle hyperlinkStyle = ApplyHyperlinkStyle(element, relationships, style, builder);
                ReadParagraphChildren(element.Elements(), relationships, builder, hyperlinkStyle);
                continue;
            }

            if (element.Name == DocxNamespaces.Wordprocessing + "del")
            {
                builder.AddDiagnosticOnce("docx.revision.delete", "Deleted revision content was skipped.");
                continue;
            }

            if (element.Name == DocxNamespaces.Wordprocessing + "sdt")
            {
                XElement? content = element.Element(DocxNamespaces.Wordprocessing + "sdtContent");
                if (content is not null)
                    ReadParagraphChildren(content.Elements(), relationships, builder, style);
                continue;
            }

            if (element.HasElements)
                ReadParagraphChildren(element.Elements(), relationships, builder, style);
        }
    }

    private static void ReadRun(
        XElement run,
        DocxRelationships relationships,
        DocxDocumentBuilder builder,
        InlineStyle inherited)
    {
        InlineStyle style = ReadRunStyle(run.Element(DocxNamespaces.Wordprocessing + "rPr"), inherited);
        foreach (XElement child in run.Elements())
        {
            if (child.Name == DocxNamespaces.Wordprocessing + "rPr")
                continue;

            if (child.Name == DocxNamespaces.Wordprocessing + "t")
            {
                builder.AppendText(child.Value, style);
                continue;
            }

            if (child.Name == DocxNamespaces.Wordprocessing + "tab")
            {
                builder.AppendText("\t", style);
                continue;
            }

            if (child.Name == DocxNamespaces.Wordprocessing + "br" ||
                child.Name == DocxNamespaces.Wordprocessing + "cr")
            {
                builder.AppendText(((char)0x2028).ToString(), style);
                continue;
            }

            if (child.Name == DocxNamespaces.Wordprocessing + "noBreakHyphen")
            {
                builder.AppendText("\u2011", style);
                continue;
            }

            if (child.Name == DocxNamespaces.Wordprocessing + "softHyphen")
            {
                builder.AppendText("\u00AD", style);
                continue;
            }

            if (child.Name == DocxNamespaces.Wordprocessing + "delText")
            {
                builder.AddDiagnosticOnce("docx.revision.delete", "Deleted revision content was skipped.");
                continue;
            }

            if (child.Name == DocxNamespaces.Wordprocessing + "drawing" ||
                child.Name == DocxNamespaces.Wordprocessing + "object" ||
                child.Name == DocxNamespaces.Wordprocessing + "pict")
            {
                builder.AddDiagnosticOnce("docx.skip.embedded", "Embedded DOCX drawing or object content was skipped.");
            }
        }
    }

    private static ParagraphStyle ReadParagraphStyle(XElement? pPr, DocxNumbering numbering)
    {
        ParagraphStyle style = ParagraphStyle.Default;
        if (pPr is null)
            return style;

        XElement? jc = pPr.Element(DocxNamespaces.Wordprocessing + "jc");
        string? alignment = WordValue(jc);
        style = alignment switch
        {
            "center" => style with { Alignment = TextAlignment.Center },
            "right" or "end" => style with { Alignment = TextAlignment.Right },
            _ => style,
        };

        XElement? spacing = pPr.Element(DocxNamespaces.Wordprocessing + "spacing");
        if (spacing is not null)
        {
            if (TryReadTwips(spacing.Attribute(DocxNamespaces.Wordprocessing + "before"), out float before))
                style = style with { SpacingBefore = before };
            if (TryReadTwips(spacing.Attribute(DocxNamespaces.Wordprocessing + "after"), out float after))
                style = style with { SpacingAfter = after };

            string? lineRule = (string?)spacing.Attribute(DocxNamespaces.Wordprocessing + "lineRule");
            if ((lineRule is null || lineRule == "auto") &&
                TryReadInt(spacing.Attribute(DocxNamespaces.Wordprocessing + "line"), out int line) &&
                line > 0)
            {
                style = style with { LineSpacing = line / 240f };
            }
        }

        XElement? ind = pPr.Element(DocxNamespaces.Wordprocessing + "ind");
        if (ind is not null && TryReadInt(ind.Attribute(DocxNamespaces.Wordprocessing + "left"), out int leftTwips))
            style = style with { IndentLevel = Math.Max(style.IndentLevel, (int)Math.Round(leftTwips / 360f)) };

        XElement? numPr = pPr.Element(DocxNamespaces.Wordprocessing + "numPr");
        if (numPr is not null)
        {
            int indent = style.IndentLevel;
            if (TryReadInt(numPr.Element(DocxNamespaces.Wordprocessing + "ilvl")?.Attribute(DocxNamespaces.Wordprocessing + "val"), out int ilvl))
                indent = Math.Max(indent, ilvl + 1);

            ListKind kind = ListKind.Bullet;
            if (TryReadInt(numPr.Element(DocxNamespaces.Wordprocessing + "numId")?.Attribute(DocxNamespaces.Wordprocessing + "val"), out int numId))
                kind = numbering.KindFor(numId);

            style = style with { ListKind = kind, IndentLevel = Math.Max(1, indent) };
        }

        return style;
    }

    private static InlineStyle ReadRunStyle(XElement? rPr, InlineStyle style)
    {
        if (rPr is null)
            return style;

        style = ApplyOnOff(rPr, "b", style, static (s, v) => s with { Bold = v });
        style = ApplyOnOff(rPr, "i", style, static (s, v) => s with { Italic = v });
        style = ApplyOnOff(rPr, "strike", style, static (s, v) => s with { Strikethrough = v });
        style = ApplyOnOff(rPr, "dstrike", style, static (s, v) => s with { Strikethrough = v });

        XElement? underline = rPr.Element(DocxNamespaces.Wordprocessing + "u");
        if (underline is not null)
        {
            string? value = WordValue(underline);
            style = style with { Underline = !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) };
        }

        XElement? fonts = rPr.Element(DocxNamespaces.Wordprocessing + "rFonts");
        string? fontFamily =
            (string?)fonts?.Attribute(DocxNamespaces.Wordprocessing + "ascii") ??
            (string?)fonts?.Attribute(DocxNamespaces.Wordprocessing + "hAnsi") ??
            (string?)fonts?.Attribute(DocxNamespaces.Wordprocessing + "cs") ??
            (string?)fonts?.Attribute(DocxNamespaces.Wordprocessing + "eastAsia");
        if (!string.IsNullOrWhiteSpace(fontFamily))
            style = style with { FontFamily = fontFamily };

        XElement? size = rPr.Element(DocxNamespaces.Wordprocessing + "sz");
        if (TryReadInt(size?.Attribute(DocxNamespaces.Wordprocessing + "val"), out int halfPoints) && halfPoints > 0)
            style = style with { FontSize = halfPoints / 2f };

        XElement? color = rPr.Element(DocxNamespaces.Wordprocessing + "color");
        string? colorValue = WordValue(color);
        if (TryParseHexColor(colorValue, out BColor foreground))
            style = style with { Foreground = foreground };

        XElement? shade = rPr.Element(DocxNamespaces.Wordprocessing + "shd");
        string? fill = (string?)shade?.Attribute(DocxNamespaces.Wordprocessing + "fill");
        if (TryParseHexColor(fill, out BColor shadeColor))
            style = style with { Background = shadeColor };

        XElement? highlight = rPr.Element(DocxNamespaces.Wordprocessing + "highlight");
        string? highlightValue = WordValue(highlight);
        if (TryParseHighlight(highlightValue, out BColor highlightColor))
            style = style with { Background = highlightColor };

        return style;
    }

    private static InlineStyle ApplyHyperlinkStyle(
        XElement hyperlink,
        DocxRelationships relationships,
        InlineStyle style,
        DocxDocumentBuilder builder)
    {
        string? id = (string?)hyperlink.Attribute(DocxNamespaces.Relationships + "id");
        string? href = null;
        if (!string.IsNullOrWhiteSpace(id) &&
            relationships.TryGet(id, out DocxRelationship? relationship) &&
            relationship is not null)
        {
            href = relationship.TargetModeExternal ? relationship.Target : null;
        }

        string? anchor = (string?)hyperlink.Attribute(DocxNamespaces.Wordprocessing + "anchor");
        if (string.IsNullOrWhiteSpace(href) && !string.IsNullOrWhiteSpace(anchor))
            href = "#" + anchor;

        if (string.IsNullOrWhiteSpace(href))
            return style;

        if (!IsAllowedLink(href))
        {
            builder.AddDiagnosticOnce("docx.link", "A hyperlink with a disallowed scheme was dropped.");
            return style;
        }

        return style with { LinkHref = href };
    }

    private static bool IsAllowedLink(string href)
    {
        if (href.StartsWith("#", StringComparison.Ordinal))
            return true;
        if (!Uri.TryCreate(href, UriKind.Absolute, out Uri? uri))
            return false;

        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase);
    }

    private static InlineStyle ApplyOnOff(
        XElement parent,
        string localName,
        InlineStyle style,
        Func<InlineStyle, bool, InlineStyle> apply)
    {
        XElement? element = parent.Element(DocxNamespaces.Wordprocessing + localName);
        return element is null ? style : apply(style, ReadOnOff(element));
    }

    private static bool ReadOnOff(XElement element)
    {
        string? value = WordValue(element);
        return value is null ||
            !(value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
              value.Equals("0", StringComparison.OrdinalIgnoreCase) ||
              value.Equals("off", StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindMainDocumentPart(
        ZipArchive archive,
        DocumentLimits limits,
        List<DocumentDiagnostic> diagnostics)
    {
        DocxRelationships packageRelationships = ReadRelationships(
            archive,
            "_rels/.rels",
            string.Empty,
            limits,
            diagnostics);
        foreach (DocxRelationship relationship in packageRelationships.All)
        {
            if (relationship.Type.Equals(DocxNamespaces.OfficeDocumentRelationship, StringComparison.Ordinal))
                return relationship.Target;
        }

        return null;
    }

    private static DocxRelationships ReadRelationships(
        ZipArchive archive,
        string path,
        string baseDirectory,
        DocumentLimits limits,
        List<DocumentDiagnostic> diagnostics)
    {
        ZipArchiveEntry? entry = FindEntry(archive, path);
        if (entry is null)
            return DocxRelationships.Empty;

        XDocument? rels = LoadEntryXml(entry, limits, diagnostics, "docx.relationships");
        if (rels?.Root is null)
            return DocxRelationships.Empty;

        var relationships = new List<DocxRelationship>();
        foreach (XElement element in rels.Root.Elements(DocxNamespaces.PackageRelationships + "Relationship"))
        {
            string? id = (string?)element.Attribute("Id");
            string? type = (string?)element.Attribute("Type");
            string? target = (string?)element.Attribute("Target");
            if (string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(type) ||
                string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            bool external = string.Equals((string?)element.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase);
            string resolved = external ? target : NormalizePackagePath(baseDirectory, target);
            relationships.Add(new DocxRelationship(id, type, resolved, external));
        }

        return new DocxRelationships(relationships);
    }

    private static XDocument? LoadEntryXml(
        ZipArchiveEntry entry,
        DocumentLimits limits,
        List<DocumentDiagnostic> diagnostics,
        string diagnosticCode)
    {
        if (entry.Length > limits.MaxBinBytes)
        {
            diagnostics.Add(DocumentDiagnostic.Error(
                diagnosticCode + ".limit",
                "A DOCX XML part exceeded MaxBinBytes and was skipped."));
            return null;
        }

        try
        {
            using Stream stream = entry.Open();
            return XDocument.Load(stream, LoadOptions.None);
        }
        catch (Exception ex) when (ex is XmlException or InvalidDataException)
        {
            diagnostics.Add(DocumentDiagnostic.Error(
                diagnosticCode,
                "A DOCX XML part could not be parsed: " + ex.GetType().Name + "."));
            return null;
        }
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string path)
    {
        string normalized = path.TrimStart('/').Replace('\\', '/');
        return archive.Entries.FirstOrDefault(entry =>
            entry.FullName.Replace('\\', '/').Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string RelationshipsPartPath(string partPath)
    {
        string normalized = partPath.TrimStart('/').Replace('\\', '/');
        int slash = normalized.LastIndexOf('/');
        if (slash < 0)
            return "_rels/" + normalized + ".rels";

        return normalized[..slash] + "/_rels/" + normalized[(slash + 1)..] + ".rels";
    }

    private static string BasePartDirectory(string partPath)
    {
        string normalized = partPath.TrimStart('/').Replace('\\', '/');
        int slash = normalized.LastIndexOf('/');
        return slash < 0 ? string.Empty : normalized[..slash];
    }

    private static string NormalizePackagePath(string baseDirectory, string target)
    {
        target = target.Replace('\\', '/');
        if (target.StartsWith("/", StringComparison.Ordinal))
            return target.TrimStart('/');

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(baseDirectory))
            parts.AddRange(baseDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries));

        foreach (string part in target.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
                continue;
            if (part == "..")
            {
                if (parts.Count > 0)
                    parts.RemoveAt(parts.Count - 1);
                continue;
            }

            parts.Add(part);
        }

        return string.Join("/", parts);
    }

    private static string? WordValue(XElement? element) =>
        (string?)element?.Attribute(DocxNamespaces.Wordprocessing + "val");

    private static bool TryReadInt(XAttribute? attribute, out int value) =>
        int.TryParse((string?)attribute, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryReadTwips(XAttribute? attribute, out float points)
    {
        points = 0f;
        if (!TryReadInt(attribute, out int twips))
            return false;

        points = twips / 20f;
        return true;
    }

    private static bool TryParseHexColor(string? value, out BColor color)
    {
        color = BColor.Empty;
        if (string.IsNullOrWhiteSpace(value) ||
            value.Equals("auto", StringComparison.OrdinalIgnoreCase) ||
            value.Length != 6)
        {
            return false;
        }

        if (!int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            return false;

        color = BColor.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        return true;
    }

    private static bool TryParseHighlight(string? value, out BColor color)
    {
        color = value?.ToLowerInvariant() switch
        {
            "black" => BColor.Black,
            "blue" => BColor.Blue,
            "cyan" => BColor.FromArgb(0, 255, 255),
            "green" => BColor.Green,
            "magenta" => BColor.FromArgb(255, 0, 255),
            "red" => BColor.Red,
            "yellow" => BColor.FromArgb(255, 255, 0),
            "white" => BColor.White,
            "darkblue" => BColor.FromArgb(0, 0, 128),
            "darkcyan" => BColor.FromArgb(0, 128, 128),
            "darkgreen" => BColor.FromArgb(0, 100, 0),
            "darkmagenta" => BColor.FromArgb(128, 0, 128),
            "darkred" => BColor.FromArgb(128, 0, 0),
            "darkyellow" => BColor.FromArgb(128, 128, 0),
            "darkgray" => BColor.FromArgb(128, 128, 128),
            "lightgray" => BColor.FromArgb(211, 211, 211),
            _ => BColor.Empty,
        };

        return !color.IsEmpty;
    }

    private sealed class DocxDocumentBuilder
    {
        private readonly DocumentLimits _limits;
        private readonly List<DocumentDiagnostic> _diagnostics;
        private readonly List<RichTextParagraph> _paragraphs = [];
        private readonly List<Segment> _segments = [];
        private readonly HashSet<string> _diagnosticOnce = new(StringComparer.Ordinal);
        private ParagraphStyle _paragraphStyle = ParagraphStyle.Default;

        public DocxDocumentBuilder(DocumentLimits limits, List<DocumentDiagnostic> diagnostics)
        {
            _limits = limits;
            _diagnostics = diagnostics;
        }

        public void StartParagraph(ParagraphStyle style)
        {
            _segments.Clear();
            _paragraphStyle = style;
        }

        public void AppendText(string text, InlineStyle style)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (text.Length > _limits.MaxRunLength)
            {
                text = text[.._limits.MaxRunLength];
                AddDiagnosticOnce("docx.limit.run", "A DOCX text run exceeded MaxRunLength and was truncated.");
            }

            if (_segments.Count > 0 && _segments[^1].Style.Equals(style))
            {
                Segment previous = _segments[^1];
                _segments[^1] = new Segment(previous.Text + text, style);
                return;
            }

            _segments.Add(new Segment(text, style));
        }

        public void FinishParagraph()
        {
            if (_paragraphs.Count >= _limits.MaxParagraphCount)
            {
                AddDiagnosticOnce("docx.limit.paragraphs", "DOCX input exceeded MaxParagraphCount; remaining paragraphs were dropped.");
                _segments.Clear();
                return;
            }

            RichTextParagraph paragraph = RichTextParagraph.Empty.WithParagraphStyle(_paragraphStyle);
            int offset = 0;
            foreach (Segment segment in _segments)
            {
                paragraph = paragraph.InsertText(offset, segment.Text, segment.Style);
                offset += segment.Text.Length;
            }

            _paragraphs.Add(paragraph);
            _segments.Clear();
            _paragraphStyle = ParagraphStyle.Default;
        }

        public RichTextDocument Build() =>
            _paragraphs.Count == 0
                ? RichTextDocument.Empty
                : RichTextDocument.FromParagraphs(_paragraphs);

        public void AddDiagnosticOnce(string code, string message)
        {
            if (_diagnosticOnce.Add(code))
                _diagnostics.Add(DocumentDiagnostic.Warning(code, message));
        }

        private readonly record struct Segment(string Text, InlineStyle Style);
    }

    private sealed class DocxRelationships
    {
        private readonly Dictionary<string, DocxRelationship> _byId;

        public DocxRelationships(IEnumerable<DocxRelationship> relationships)
        {
            All = relationships.ToArray();
            _byId = All.ToDictionary(relationship => relationship.Id, StringComparer.Ordinal);
        }

        public static DocxRelationships Empty { get; } = new(Array.Empty<DocxRelationship>());

        public IReadOnlyList<DocxRelationship> All { get; }

        public bool TryGet(string id, out DocxRelationship? relationship) =>
            _byId.TryGetValue(id, out relationship);
    }

    private sealed record DocxRelationship(
        string Id,
        string Type,
        string Target,
        bool TargetModeExternal);

    private sealed class DocxNumbering
    {
        private readonly Dictionary<int, ListKind> _numKinds;

        private DocxNumbering(Dictionary<int, ListKind> numKinds) => _numKinds = numKinds;

        public static DocxNumbering Empty { get; } = new(new Dictionary<int, ListKind>());

        public static DocxNumbering Load(
            ZipArchive archive,
            DocxRelationships documentRelationships,
            string documentBaseDirectory,
            DocumentLimits limits,
            List<DocumentDiagnostic> diagnostics)
        {
            string? numberingPath = documentRelationships.All
                .FirstOrDefault(relationship => relationship.Type.Equals(DocxNamespaces.NumberingRelationship, StringComparison.Ordinal))
                ?.Target;
            numberingPath ??= NormalizePackagePath(documentBaseDirectory, "numbering.xml");

            ZipArchiveEntry? entry = FindEntry(archive, numberingPath);
            if (entry is null)
                return Empty;

            XDocument? xml = LoadEntryXml(entry, limits, diagnostics, "docx.numbering");
            if (xml?.Root is null)
                return Empty;

            var abstractKinds = new Dictionary<int, ListKind>();
            foreach (XElement abstractNum in xml.Root.Elements(DocxNamespaces.Wordprocessing + "abstractNum"))
            {
                if (!TryReadInt(abstractNum.Attribute(DocxNamespaces.Wordprocessing + "abstractNumId"), out int abstractId))
                    continue;

                XElement? level = abstractNum.Elements(DocxNamespaces.Wordprocessing + "lvl").FirstOrDefault();
                string? format = WordValue(level?.Element(DocxNamespaces.Wordprocessing + "numFmt"));
                abstractKinds[abstractId] = format is "decimal" or "decimalZero" or "upperRoman" or "lowerRoman" or "upperLetter" or "lowerLetter"
                    ? ListKind.Numbered
                    : ListKind.Bullet;
            }

            var numKinds = new Dictionary<int, ListKind>();
            foreach (XElement num in xml.Root.Elements(DocxNamespaces.Wordprocessing + "num"))
            {
                if (!TryReadInt(num.Attribute(DocxNamespaces.Wordprocessing + "numId"), out int numId))
                    continue;
                if (!TryReadInt(num.Element(DocxNamespaces.Wordprocessing + "abstractNumId")?.Attribute(DocxNamespaces.Wordprocessing + "val"), out int abstractId))
                    continue;
                if (abstractKinds.TryGetValue(abstractId, out ListKind kind))
                    numKinds[numId] = kind;
            }

            return new DocxNumbering(numKinds);
        }

        public ListKind KindFor(int numId) =>
            _numKinds.TryGetValue(numId, out ListKind kind) ? kind : ListKind.Bullet;
    }
}
