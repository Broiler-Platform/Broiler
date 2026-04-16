using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Broiler.Cli.Tests;

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
    public async Task Program_Main_ConvertPdf_Creates_Default_Output_File()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var pdfPath = Path.Combine(tempDirectory, "cli.pdf");
            File.WriteAllText(pdfPath, CreateMinimalPdf("CLI Conversion"), Encoding.ASCII);

            var exitCode = await Program.Main(["--convert-pdf", pdfPath]);

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
    public async Task Program_Main_ConvertPdf_Missing_File_Returns_Error()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var missingPdfPath = Path.Combine(tempDirectory, "missing.pdf");

            var exitCode = await Program.Main(["--convert-pdf", missingPdfPath]);

            Assert.Equal(1, exitCode);
            Assert.False(File.Exists(Path.Combine(tempDirectory, "missing.docx")));
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

    private static string CreateMinimalPdf(string text)
    {
        var escapedText = text
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("(", @"\(", StringComparison.Ordinal)
            .Replace(")", @"\)", StringComparison.Ordinal);

        var objects = new[]
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Count 1 /Kids [3 0 R] >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n",
            $"4 0 obj\n<< /Length {Encoding.ASCII.GetByteCount($"BT\n/F1 18 Tf\n72 720 Td\n({escapedText}) Tj\nET\n")} >>\nstream\nBT\n/F1 18 Tf\n72 720 Td\n({escapedText}) Tj\nET\nendstream\nendobj\n",
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
