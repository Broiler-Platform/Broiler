using System.Text;
using Broiler.Documents;
using Broiler.Documents.Rtf;

namespace Broiler.Cli;

/// <summary>
/// Headless document-format conversion through the Broiler.Documents codec catalog.
/// Reads a document (currently RTF, selected by signature/extension) and writes
/// plain text (<c>.txt</c>) or normalized RTF (<c>.rtf</c>). The conversion path has
/// no UI dependency.
/// </summary>
public static class DocumentConvertService
{
    public static int Convert(string inputPath, string outputPath)
    {
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: input file not found: {inputPath}");
            return 1;
        }

        var catalog = new DocumentCodecCatalog(new DocumentCodec[] { new RtfDocumentCodec() });
        byte[] input = File.ReadAllBytes(inputPath);
        var hints = new DocumentSourceHints(fileName: inputPath);

        DocumentCodecMatch? match;
        using (var probeStream = new MemoryStream(input, writable: false))
            match = catalog.Select(probeStream, hints);

        if (match is null)
        {
            Console.Error.WriteLine($"Error: no document codec recognized '{inputPath}'.");
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
            default:
                Console.Error.WriteLine($"Error: unsupported output format '{extension}'. Use .txt or .rtf.");
                return 1;
        }

        File.WriteAllBytes(outputPath, output);
        Console.WriteLine(
            $"Converted {inputPath} -> {outputPath} " +
            $"({match.Codec.Name}, {read.Document.ParagraphCount} paragraph(s), {read.Diagnostics.Count} diagnostic(s)).");
        return 0;
    }
}
