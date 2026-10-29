using Broiler.Documents;
using Broiler.Documents.Model;
using Broiler.Documents.Html;
using Broiler.Documents.Rtf;
using Broiler.Documents.Docx;
using Broiler.Documents.Odt;
using Broiler.Documents.Markdown;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using Broiler.Documents.Resources;

var codecs = new Dictionary<string, DocumentCodec> { ["rtf"] = new RtfDocumentCodec(), ["docx"] = new DocxDocumentCodec(), ["odt"] = new OdtDocumentCodec(), ["html"] = new HtmlDocumentCodec(), ["md"] = new MarkdownDocumentCodec() };
var readOptions = new DocumentReadOptions(resourcePolicy: DocumentResourcePolicy.AllowOwnDocuments);
if (args[0] == "generate") {
    string directory = Path.GetFullPath(args[1]); Directory.CreateDirectory(directory);
    const string source = """
        <!doctype html><html><body><p>Round trip ÄΩ</p><p><b>Bold</b> <i>Italic</i> <u>Underline</u></p><p><a href="https://example.invalid/document">Link</a></p><p>Second paragraph.</p></body></html>
        """;
    var read = new HtmlDocumentCodec().Read(new MemoryStream(Encoding.UTF8.GetBytes(source)), readOptions);
    var fixtures = new List<object>();
    foreach (var (extension, codec) in codecs) {
        string path = Path.Combine(directory, "basic." + extension);
        using (var destination = File.Create(path)) codec.Write(read.Document, destination, new DocumentWriteOptions(resources: read.Resources));
        var result = codec.Read(File.OpenRead(path), readOptions);
        fixtures.Add(new { format = extension, expected = Describe(result) });
    }
    File.WriteAllText(Path.Combine(directory, "fixtures.json"), JsonSerializer.Serialize(fixtures, new JsonSerializerOptions { WriteIndented = true }));
    File.WriteAllText(Path.Combine(directory, "unsupported.html"), "<html><body><p>Supported text</p><table><tr><td>Table cell</td></tr></table><video>Unsupported video</video><svg><text>Vector</text></svg></body></html>");
    File.WriteAllText(Path.Combine(directory, "image.html"), "<html><body><p>Embedded image</p><p><img alt='pixel' width='8' height='4' src='data:image/png;base64," + Convert.ToBase64String(Png()) + "'></p></body></html>");
    File.WriteAllText(Path.Combine(directory, "font.html"), "<html><body><p style='font-family:NonexistentTestTypeface;font-size:18px'>Fallback font text</p></body></html>");
    var resources = new DocumentConversionContextBuilder(DocumentResourcePolicy.AllowOwnDocuments);
    var image = resources.AdmitImage(new InlineImage(Png(), "image/png", 8, 4, altText: "pixel", name: "pixel"), DocumentResourceProvenance.CallerSupplied, DocumentResourceDisposition.Embedded);
    var imageDocument = RichTextDocument.FromParagraphs(new[] { RichTextParagraph.Plain("Embedded image"), RichTextParagraph.Create(InlineImage.Placeholder.ToString(), new InlineStyle { Image = image }) });
    using (var imageFile = File.Create(Path.Combine(directory, "image.docx"))) DocxWriter.Write(imageDocument, imageFile, new DocumentWriteOptions(resources: resources.Build()));
    using (var sourceZip = new ZipArchive(File.OpenRead(Path.Combine(directory, "image.docx")), ZipArchiveMode.Read))
    using (var badZip = new ZipArchive(File.Create(Path.Combine(directory, "bad-image.docx")), ZipArchiveMode.Create)) {
        foreach (var entry in sourceZip.Entries) {
            using var sourceEntry = entry.Open(); using var memory = new MemoryStream(); sourceEntry.CopyTo(memory);
            byte[] data = memory.ToArray(); if (entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) data[41] = 0;
            using var target = badZip.CreateEntry(entry.FullName).Open(); target.Write(data);
        }
    }
    File.WriteAllText(Path.Combine(directory, "external-image.html"), "<html><body><p>External image</p><img src='https://unrequested.example/pixel.png'></body></html>");
    File.WriteAllText(Path.Combine(directory, "long.md"), string.Join("\n\n", Enumerable.Range(0, 1000).Select(index => "Paragraph " + index + ": " + new string('x', 80))));
    File.WriteAllText(Path.Combine(directory, "too-much-text.md"), new string('x', 250001));
    File.WriteAllText(Path.Combine(directory, "broken.docx"), "not a ZIP archive");
    using (var zip = new ZipArchive(File.Create(Path.Combine(directory, "expanded.docx")), ZipArchiveMode.Create)) {
        using var stream = zip.CreateEntry("word/document.xml", CompressionLevel.SmallestSize).Open(); stream.Write(new byte[33 * 1024 * 1024]);
    }
    Console.WriteLine("Generated " + directory);
} else if (args[0] == "inspect-recovery") {
    var results = new List<object>();
    foreach (string path in Directory.EnumerateFiles(args[1], "*-writer-recovered.json")) {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        using var stream = new MemoryStream(Convert.FromBase64String(json.RootElement.GetProperty("payload").GetString()!));
        results.Add(new { path, content = Describe(codecs["rtf"].Read(stream, readOptions)) });
    }
    File.WriteAllText(args[2], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
} else if (args[0] == "inspect") {
    var results = new List<object>();
    foreach (string path in Directory.EnumerateFiles(args[1], "*", SearchOption.AllDirectories).Where(path => codecs.ContainsKey(Path.GetExtension(path)[1..].ToLowerInvariant()))) {
        var codec = codecs[Path.GetExtension(path)[1..].ToLowerInvariant()];
        using var source = File.OpenRead(path); results.Add(new { path, content = Describe(codec.Read(source, readOptions)) });
    }
    File.WriteAllText(args[2], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
}

static object Describe(DocumentReadResult result) {
    var styles = result.Document.Paragraphs.SelectMany(paragraph => paragraph.Runs).Select(run => run.Style).ToArray();
    return new { text = result.Document.PlainText, paragraphs = result.Document.ParagraphCount,
        bold = styles.Any(style => style.Bold), italic = styles.Any(style => style.Italic), underline = styles.Any(style => style.Underline),
        links = styles.Select(style => style.LinkHref).Where(link => link is not null).Distinct().ToArray(), images = styles.Count(style => style.Image is not null),
        fonts = styles.Select(style => style.FontFamily).Where(font => font is not null).Distinct().ToArray(),
        status = result.Status.ToString(), notes = result.Diagnostics.Select(note => new { note.Code, note.Message, severity = note.Severity.ToString() }).ToArray() };
}

static byte[] Png() {
    using var png = new MemoryStream(); png.Write(new byte[] { 137,80,78,71,13,10,26,10 });
    byte[] header = new byte[13]; System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, 8); System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), 4); header[8] = 8; header[9] = 6;
    Chunk("IHDR", header);
    using var compressed = new MemoryStream();
    using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true)) {
        for (int y = 0; y < 4; y++) { zlib.WriteByte(0); for (int x = 0; x < 8; x++) zlib.Write(new byte[] { 255,0,0,255 }); }
    }
    Chunk("IDAT", compressed.ToArray()); Chunk("IEND", Array.Empty<byte>()); return png.ToArray();
    void Chunk(string type, byte[] payload) {
        byte[] length = new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, payload.Length); png.Write(length);
        byte[] bytes = Encoding.ASCII.GetBytes(type).Concat(payload).ToArray(); png.Write(bytes);
        uint crc = uint.MaxValue; foreach (byte value in bytes) { crc ^= value; for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1; }
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(length, ~crc); png.Write(length);
    }
}
