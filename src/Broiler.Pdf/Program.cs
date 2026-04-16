using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Broiler.Pdf;

public class Program
{
    public static int Main(string[] args)
    {
        string? inputPdfPath = null;
        string? outputPath = null;
        bool preserveLayout = false;

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
                case "--preserve-layout":
                    preserveLayout = true;
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
            var resolvedOutputPath = converter.Convert(
                inputPdfPath,
                outputPath,
                new PdfConversionOptions
                {
                    PreserveLayout = preserveLayout,
                });
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
        Console.WriteLine("Usage: Broiler.Pdf --input <PDF> [--output <FILE|DIR>] [--preserve-layout]");
        Console.WriteLine("   or: Broiler.Pdf <PDF> [--output <FILE|DIR>] [--preserve-layout]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --input <PDF>          Input PDF file to convert");
        Console.WriteLine("  --output <FILE|DIR>    Output .docx file path or directory");
        Console.WriteLine("  --preserve-layout      Keep PDF page positioning and font styling via embedded HTML");
        Console.WriteLine("  --help                 Show this help message");
    }
}

public sealed class PdfConversionOptions
{
    public bool PreserveLayout { get; init; }
}

internal sealed class PdfToWordConverter
{
    public string Convert(string inputPdfPath, string? outputPath = null, PdfConversionOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(inputPdfPath))
            throw new InvalidOperationException("An input PDF file path is required.");

        options ??= new PdfConversionOptions();

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
                if (options.PreserveLayout)
                    AppendPagePreservingLayout(mainPart, body, pages[i], i);
                else
                    AppendPage(body, pages[i].Text);

                if (i < pages.Count - 1)
                    body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
            }

            if (options.PreserveLayout)
                AppendPageSizing(body, pages);

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

    private static void AppendPagePreservingLayout(MainDocumentPart mainPart, Body body, Page page, int pageIndex)
    {
        var chunkId = $"AltChunkId{pageIndex + 1}";
        var alternativePart = mainPart.AddAlternativeFormatImportPart(AlternativeFormatImportPartType.Html, chunkId);

        using (var stream = alternativePart.GetStream(FileMode.Create, FileAccess.Write))
        using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
        {
            writer.Write(BuildPositionedHtml(page));
        }

        body.AppendChild(new AltChunk { Id = chunkId });
    }

    private static void AppendPageSizing(Body body, IReadOnlyList<Page> pages)
    {
        if (pages.Count == 0)
            return;

        var maxWidth = pages.Max(page => page.Width);
        var maxHeight = pages.Max(page => page.Height);

        body.AppendChild(
            new SectionProperties(
                new DocumentFormat.OpenXml.Wordprocessing.PageSize
                {
                    Width = ConvertPointsToTwips(maxWidth),
                    Height = ConvertPointsToTwips(maxHeight),
                },
                new PageMargin
                {
                    Top = 0,
                    Bottom = 0,
                    Left = 0,
                    Right = 0,
                    Header = 0,
                    Footer = 0,
                    Gutter = 0,
                }));
    }

    private static UInt32Value ConvertPointsToTwips(double points)
    {
        return (UInt32Value)(uint)Math.Clamp(Math.Round(points * 20d), 1, uint.MaxValue);
    }

    private static string BuildPositionedHtml(Page page)
    {
        var html = new System.Text.StringBuilder();
        html.Append("""<!DOCTYPE html><html><head><meta charset="utf-8"></head><body style="margin:0;padding:0;background:#fff;">""");
        html.Append($"""<div style="position:relative;width:{FormatCssNumber(page.Width)}pt;height:{FormatCssNumber(page.Height)}pt;overflow:hidden;background:#fff;">""");

        foreach (var letter in page.Letters)
        {
            if (string.IsNullOrWhiteSpace(letter.Value))
                continue;

            var top = Math.Max(0, page.Height - letter.BoundingBox.Top);
            var left = Math.Max(0, letter.BoundingBox.Left);
            var fontSize = Math.Max(1, letter.PointSize > 0 ? letter.PointSize : letter.FontSize);
            var fontFamily = NormalizeFontFamily(letter.FontName);
            var styles = $"position:absolute;left:{FormatCssNumber(left)}pt;top:{FormatCssNumber(top)}pt;font-size:{FormatCssNumber(fontSize)}pt;line-height:1;white-space:pre;";

            if (!string.IsNullOrWhiteSpace(fontFamily))
                styles += $"font-family:'{EscapeCssString(fontFamily)}',sans-serif;";

            if (IsBoldFont(letter.FontName))
                styles += "font-weight:bold;";

            if (IsItalicFont(letter.FontName))
                styles += "font-style:italic;";

            html.Append($"""<span style="{styles}">{System.Net.WebUtility.HtmlEncode(letter.Value)}</span>""");
        }

        html.Append("</div></body></html>");
        return html.ToString();
    }

    private static string FormatCssNumber(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string NormalizeFontFamily(string? fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName))
            return string.Empty;

        var normalized = fontName;
        var subsetSeparator = normalized.IndexOf('+');
        if (subsetSeparator >= 0 && subsetSeparator < normalized.Length - 1)
            normalized = normalized[(subsetSeparator + 1)..];

        normalized = normalized.Replace(',', ' ').Trim();

        return normalized switch
        {
            var value when value.Contains("Helvetica", StringComparison.OrdinalIgnoreCase) => "Helvetica",
            var value when value.Contains("Arial", StringComparison.OrdinalIgnoreCase) => "Arial",
            var value when value.Contains("Courier", StringComparison.OrdinalIgnoreCase) => "Courier New",
            var value when value.Contains("Times", StringComparison.OrdinalIgnoreCase) => "Times New Roman",
            _ => normalized,
        };
    }

    private static string EscapeCssString(string value)
    {
        return value.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("'", @"\'", StringComparison.Ordinal);
    }

    private static bool IsBoldFont(string? fontName)
    {
        return !string.IsNullOrWhiteSpace(fontName)
            && fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsItalicFont(string? fontName)
    {
        return !string.IsNullOrWhiteSpace(fontName)
            && (fontName.Contains("Italic", StringComparison.OrdinalIgnoreCase)
                || fontName.Contains("Oblique", StringComparison.OrdinalIgnoreCase));
    }
}
