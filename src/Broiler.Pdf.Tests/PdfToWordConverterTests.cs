using System.Text;
using System.IO.Compression;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Broiler.Pdf.Tests;

public class PdfToWordConverterTests
{
    [Fact]
    public void Convert_Creates_Docx_With_Extracted_Text()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var pdfPath = Path.Combine(tempDirectory, "sample.pdf");
            File.WriteAllText(pdfPath, CreateMinimalPdf("Hello PDF"), Encoding.ASCII);

            var converter = new PdfToWordConverter();
            var outputPath = converter.Convert(pdfPath);

            Assert.Equal(Path.Combine(tempDirectory, "sample.docx"), outputPath);
            Assert.True(File.Exists(outputPath));
            Assert.Contains("Hello PDF", ReadWordText(outputPath));
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void ResolveOutputPath_Uses_Existing_Output_Directory()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var pdfPath = Path.Combine(tempDirectory, "sample.pdf");
            var outputDirectory = Path.Combine(tempDirectory, "out");
            Directory.CreateDirectory(outputDirectory);

            var outputPath = PdfToWordConverter.ResolveOutputPath(pdfPath, outputDirectory);

            Assert.Equal(Path.Combine(outputDirectory, "sample.docx"), outputPath);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void Program_Main_Creates_Default_Output_File()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var pdfPath = Path.Combine(tempDirectory, "cli.pdf");
            File.WriteAllText(pdfPath, CreateMinimalPdf("CLI Conversion"), Encoding.ASCII);

            var exitCode = Program.Main(["--input", pdfPath]);

            var outputPath = Path.Combine(tempDirectory, "cli.docx");
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));
            Assert.Contains("CLI Conversion", ReadWordText(outputPath));
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void Program_Main_Missing_File_Returns_Error()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var missingPdfPath = Path.Combine(tempDirectory, "missing.pdf");

            var exitCode = Program.Main(["--input", missingPdfPath]);

            Assert.Equal(1, exitCode);
            Assert.False(File.Exists(Path.Combine(tempDirectory, "missing.docx")));
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void Convert_WithPreserveLayout_Embeds_Positioned_Html()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var pdfPath = Path.Combine(tempDirectory, "layout.pdf");
            File.WriteAllText(
                pdfPath,
                CreateMinimalPdf(
                    ("Top Left", 72, 720, 18),
                    ("Bottom Right", 300, 120, 12)),
                Encoding.ASCII);

            var converter = new PdfToWordConverter();
            var outputPath = converter.Convert(
                pdfPath,
                options: new PdfConversionOptions
                {
                    PreserveLayout = true,
                });

            Assert.True(File.Exists(outputPath));

            var documentXml = ReadArchiveEntry(outputPath, "word/document.xml");
            var html = ReadAlternativeChunk(outputPath);
            Assert.Contains("altChunk", documentXml, StringComparison.Ordinal);
            Assert.Contains("position:absolute", html, StringComparison.Ordinal);
            Assert.Contains(">T<", html, StringComparison.Ordinal);
            Assert.Contains(">B<", html, StringComparison.Ordinal);
            Assert.Contains("width:612pt", html, StringComparison.Ordinal);
            Assert.Contains("height:792pt", html, StringComparison.Ordinal);
            Assert.Contains("left:72", html, StringComparison.Ordinal);
            Assert.Contains("left:300", html, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void Program_Main_WithPreserveLayout_Creates_AltChunk_Output()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var pdfPath = Path.Combine(tempDirectory, "cli-layout.pdf");
            File.WriteAllText(pdfPath, CreateMinimalPdf("CLI Layout"), Encoding.ASCII);

            var exitCode = Program.Main(["--input", pdfPath, "--preserve-layout"]);

            var outputPath = Path.Combine(tempDirectory, "cli-layout.docx");
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));
            Assert.Contains("position:absolute", ReadAlternativeChunk(outputPath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "broiler-pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string ReadWordText(string outputPath)
    {
        using var document = WordprocessingDocument.Open(outputPath, false);
        return string.Join(
            "\n",
            document.MainDocumentPart!.Document.Body!
                .Descendants<Text>()
                .Select(text => text.Text));
    }

    private static string ReadAlternativeChunk(string outputPath)
    {
        using var archive = ZipFile.OpenRead(outputPath);
        var entry = archive.Entries.FirstOrDefault(entry => entry.FullName.StartsWith("word/afchunk", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(entry);
        using var stream = entry!.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string ReadArchiveEntry(string outputPath, string entryName)
    {
        using var archive = ZipFile.OpenRead(outputPath);
        var entry = archive.GetEntry(entryName);
        Assert.NotNull(entry);
        using var stream = entry!.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string CreateMinimalPdf(string text)
    {
        return CreateMinimalPdf((text, 72, 720, 18));
    }

    private static string CreateMinimalPdf(params (string Text, int X, int Y, int FontSize)[] fragments)
    {
        var pageContent = string.Join(
            "\n",
            fragments.Select(fragment =>
            {
                var escapedText = fragment.Text
                    .Replace(@"\", @"\\", StringComparison.Ordinal)
                    .Replace("(", @"\(", StringComparison.Ordinal)
                    .Replace(")", @"\)", StringComparison.Ordinal);

                return $"BT\n/F1 {fragment.FontSize} Tf\n{fragment.X} {fragment.Y} Td\n({escapedText}) Tj\nET";
            })) + "\n";

        var objects = new[]
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Count 1 /Kids [3 0 R] >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n",
            $"4 0 obj\n<< /Length {Encoding.ASCII.GetByteCount(pageContent)} >>\nstream\n{pageContent}endstream\nendobj\n",
            "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n",
        };

        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");

        var offsets = new List<int> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(obj);
        }

        var startXref = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n");
        builder.Append($"0 {objects.Length + 1}\n");
        builder.Append("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            builder.Append(offset.ToString("D10"));
            builder.Append(" 00000 n \n");
        }

        builder.Append("trailer\n");
        builder.Append($"<< /Size {objects.Length + 1} /Root 1 0 R >>\n");
        builder.Append("startxref\n");
        builder.Append(startXref);
        builder.Append("\n%%EOF\n");
        return builder.ToString();
    }
}
