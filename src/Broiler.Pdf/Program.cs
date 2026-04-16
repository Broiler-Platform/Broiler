using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace Broiler.Pdf;

public class Program
{
    public static int Main(string[] args)
    {
        string? inputPdfPath = null;
        string? outputPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input" when i + 1 < args.Length:
                    inputPdfPath = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "--input":
                case "--output":
                    Console.Error.WriteLine($"Error: '{args[i]}' requires a value.");
                    PrintUsage();
                    return 1;
                case "--help":
                    PrintUsage();
                    return 0;
                default:
                    if (inputPdfPath is null && !args[i].StartsWith('-'))
                    {
                        inputPdfPath = args[i];
                        break;
                    }

                    Console.Error.WriteLine($"Error: Unrecognized argument '{args[i]}'.");
                    PrintUsage();
                    return 1;
            }
        }

        if (inputPdfPath is null)
        {
            Console.Error.WriteLine("Error: An input PDF file is required.");
            PrintUsage();
            return 1;
        }

        try
        {
            var converter = new PdfToWordConverter();
            var resolvedOutputPath = converter.Convert(inputPdfPath, outputPath);
            Console.WriteLine($"Word document saved to {resolvedOutputPath}");
            return 0;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"PDF conversion failed: {ex.Message}");
            return 1;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"File I/O error: {ex.Message}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"PDF conversion failed: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: Broiler.Pdf --input <PDF> [--output <FILE|DIR>]");
        Console.WriteLine("   or: Broiler.Pdf <PDF> [--output <FILE|DIR>]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --input <PDF>          Input PDF file to convert");
        Console.WriteLine("  --output <FILE|DIR>    Output .docx file path or directory");
        Console.WriteLine("  --help                 Show this help message");
    }
}

internal sealed class PdfToWordConverter
{
    public string Convert(string inputPdfPath, string? outputPath = null)
    {
        if (string.IsNullOrWhiteSpace(inputPdfPath))
            throw new InvalidOperationException("An input PDF file path is required.");

        var fullInputPath = Path.GetFullPath(inputPdfPath);
        if (!File.Exists(fullInputPath))
            throw new FileNotFoundException($"Input PDF file '{fullInputPath}' was not found.", fullInputPath);

        if (!string.Equals(Path.GetExtension(fullInputPath), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The input file must use the .pdf extension.");

        var resolvedOutputPath = ResolveOutputPath(fullInputPath, outputPath);
        var outputDirectory = Path.GetDirectoryName(resolvedOutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        try
        {
            using var pdfDocument = PdfDocument.Open(fullInputPath);
            using var wordDocument = WordprocessingDocument.Create(resolvedOutputPath, WordprocessingDocumentType.Document);

            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            var body = mainPart.Document.Body!;
            var pages = pdfDocument.GetPages().ToList();
            for (int i = 0; i < pages.Count; i++)
            {
                AppendPage(body, pages[i].Text);

                if (i < pages.Count - 1)
                    body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
            }

            mainPart.Document.Save();
            return resolvedOutputPath;
        }
        catch (Exception ex) when (ex is not FileNotFoundException and not InvalidOperationException)
        {
            if (ex is IOException or UnauthorizedAccessException)
                throw new IOException($"Could not write Word document '{resolvedOutputPath}': {ex.Message}", ex);

            throw new InvalidOperationException($"Could not convert PDF '{fullInputPath}' to a Word document: {ex.Message}", ex);
        }
    }

    internal static string ResolveOutputPath(string inputPdfPath, string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return Path.ChangeExtension(inputPdfPath, ".docx");

        var fullOutputPath = Path.GetFullPath(outputPath);
        if (LooksLikeDirectory(outputPath, fullOutputPath))
        {
            return Path.Combine(
                fullOutputPath,
                Path.GetFileNameWithoutExtension(inputPdfPath) + ".docx");
        }

        var extension = Path.GetExtension(fullOutputPath);
        if (string.IsNullOrEmpty(extension))
            return fullOutputPath + ".docx";

        if (!string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The output path must be a .docx file or a directory.");

        return fullOutputPath;
    }

    private static bool LooksLikeDirectory(string originalOutputPath, string fullOutputPath)
    {
        if (Directory.Exists(fullOutputPath))
            return true;

        return originalOutputPath.EndsWith(Path.DirectorySeparatorChar)
            || originalOutputPath.EndsWith(Path.AltDirectorySeparatorChar);
    }

    private static void AppendPage(Body body, string pageText)
    {
        var lines = pageText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        if (lines.Length == 0)
        {
            body.AppendChild(new Paragraph());
            return;
        }

        foreach (var line in lines)
        {
            body.AppendChild(
                new Paragraph(
                    new Run(
                        new Text(line)
                        {
                            Space = SpaceProcessingModeValues.Preserve,
                        })));
        }
    }
}
