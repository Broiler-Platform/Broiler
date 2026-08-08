using System.Text;
using Broiler.Documents;
using Broiler.Documents.Docx;
using Broiler.Documents.Html;
using Broiler.Documents.Markdown;
using Broiler.Documents.Rtf;

namespace Broiler.Cli;

/// <summary>
/// Headless document-format conversion through the Broiler.Documents codec catalog.
/// Reads a document (selected by signature/extension) and writes plain text
/// (<c>.txt</c>), normalized RTF (<c>.rtf</c>), deterministic HTML
/// (<c>.html</c>), Markdown (<c>.md</c>), or DOCX (<c>.docx</c>). The
/// conversion path has no UI dependency.
/// </summary>
public static class DocumentConvertService
{
    public static int Convert(string inputPath, string outputPath) =>
        Convert(inputPath, outputPath, Console.Out, Console.Error);

    /// <summary>
    /// Converts one document, writing its transcript to the given writers rather than straight
    /// to the console. A batch conversion runs several of these concurrently and replays each
    /// item's output in input order, which it can only do if the output is capturable.
    /// </summary>
    public static int Convert(string inputPath, string outputPath, TextWriter standardOutput, TextWriter standardError)
    {
        if (!File.Exists(inputPath))
        {
            standardError.WriteLine($"Error: input file not found: {inputPath}");
            return 1;
        }

        var catalog = new DocumentCodecCatalog(new DocumentCodec[]
        {
            new RtfDocumentCodec(),
            new DocxDocumentCodec(),
            new HtmlDocumentCodec(),
            new MarkdownDocumentCodec(),
        });
        byte[] input = File.ReadAllBytes(inputPath);
        var hints = new DocumentSourceHints(fileName: inputPath);

        DocumentCodecMatch? match;
        using (var probeStream = new MemoryStream(input, writable: false))
            match = catalog.Select(probeStream, hints);

        if (match is null)
        {
            standardError.WriteLine($"Error: no document codec recognized '{inputPath}'.");
            return 1;
        }

        DocumentReadResult read;
        using (var readStream = new MemoryStream(input, writable: false))
            read = match.Codec.Read(readStream);

        string extension = Path.GetExtension(outputPath).ToLowerInvariant();
        byte[] output;
        switch (extension)
        {
            case ".txt":
                output = Encoding.UTF8.GetBytes(read.Document.PlainText);
                break;
            case ".rtf":
                output = RtfWriter.WriteToArray(read.Document);
                break;
            case ".docx":
                output = DocxWriter.WriteToArray(read.Document);
                break;
            case ".html":
            case ".htm":
                output = HtmlWriter.WriteToArray(read.Document);
                break;
            case ".md":
            case ".markdown":
                output = MarkdownWriter.WriteToArray(read.Document);
                break;
            default:
                standardError.WriteLine($"Error: unsupported output format '{extension}'. Use .txt, .rtf, .docx, .html, or .md.");
                return 1;
        }

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        File.WriteAllBytes(outputPath, output);
        standardOutput.WriteLine(
            $"Converted {inputPath} -> {outputPath} " +
            $"({match.Codec.Name}, {read.Document.ParagraphCount} paragraph(s), " +
            $"{read.Document.PlainText.Length} character(s), {read.Diagnostics.Count} diagnostic(s)).");
        WriteDiagnostics(read.Diagnostics, standardOutput, standardError);
        return 0;
    }

    /// <summary>
    /// Prints every read diagnostic. A conversion that silently drops content is
    /// the hardest kind to debug, so the codes go to the console rather than
    /// being summarized away as a count.
    /// </summary>
    private static void WriteDiagnostics(
        IReadOnlyList<DocumentDiagnostic> diagnostics,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        foreach (DocumentDiagnostic diagnostic in diagnostics)
        {
            TextWriter writer = diagnostic.Severity == DocumentDiagnosticSeverity.Error
                ? standardError
                : standardOutput;
            writer.WriteLine($"  {diagnostic.Severity}: {diagnostic.Code}: {diagnostic.Message}");
        }
    }
}
