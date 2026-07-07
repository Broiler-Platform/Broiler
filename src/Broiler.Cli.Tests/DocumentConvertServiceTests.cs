using System.Text;
using Broiler.Cli;
using Broiler.Documents.Docx;

namespace Broiler.Cli.Tests;

public sealed class DocumentConvertServiceTests
{
    [Fact]
    public void Convert_Html_To_Rtf_Uses_Document_Catalog()
    {
        string directory = CreateTempDirectory();
        try
        {
            string input = Path.Combine(directory, "input.html");
            string output = Path.Combine(directory, "output.rtf");
            File.WriteAllText(input, "<p>Hello <strong>HTML</strong></p>", Encoding.UTF8);

            int exitCode = DocumentConvertService.Convert(input, output);

            Assert.Equal(0, exitCode);
            string rtf = File.ReadAllText(output, Encoding.ASCII);
            Assert.StartsWith("{\\rtf1", rtf);
            Assert.Contains("Hello ", rtf);
            Assert.Contains("\\b HTML", rtf);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Convert_Rtf_To_Html_Uses_Document_Catalog()
    {
        string directory = CreateTempDirectory();
        try
        {
            string input = Path.Combine(directory, "input.rtf");
            string output = Path.Combine(directory, "output.html");
            File.WriteAllText(input, "{\\rtf1\\ansi Hello \\b RTF\\b0\\par}", Encoding.ASCII);

            int exitCode = DocumentConvertService.Convert(input, output);

            Assert.Equal(0, exitCode);
            string html = File.ReadAllText(output, Encoding.UTF8);
            Assert.StartsWith("<!DOCTYPE html><html>", html);
            Assert.Contains("Hello ", html);
            Assert.Contains("font-weight: bold", html);
            Assert.Contains("RTF", html);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Convert_Markdown_To_Rtf_Uses_Document_Catalog()
    {
        string directory = CreateTempDirectory();
        try
        {
            string input = Path.Combine(directory, "input.md");
            string output = Path.Combine(directory, "output.rtf");
            File.WriteAllText(input, "Hello **Markdown**", Encoding.UTF8);

            int exitCode = DocumentConvertService.Convert(input, output);

            Assert.Equal(0, exitCode);
            string rtf = File.ReadAllText(output, Encoding.ASCII);
            Assert.StartsWith("{\\rtf1", rtf);
            Assert.Contains("Hello ", rtf);
            Assert.Contains("\\b Markdown", rtf);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Convert_Html_To_Markdown_Uses_Document_Catalog()
    {
        string directory = CreateTempDirectory();
        try
        {
            string input = Path.Combine(directory, "input.html");
            string output = Path.Combine(directory, "output.md");
            File.WriteAllText(input, "<p>Hello <em>Markdown</em></p>", Encoding.UTF8);

            int exitCode = DocumentConvertService.Convert(input, output);

            Assert.Equal(0, exitCode);
            string markdown = File.ReadAllText(output, Encoding.UTF8);
            Assert.Equal("Hello *Markdown*\n", markdown);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Convert_Html_To_Docx_Uses_Document_Catalog()
    {
        string directory = CreateTempDirectory();
        try
        {
            string input = Path.Combine(directory, "input.html");
            string output = Path.Combine(directory, "output.docx");
            File.WriteAllText(input, "<p>Hello <strong>DOCX</strong></p>", Encoding.UTF8);

            int exitCode = DocumentConvertService.Convert(input, output);

            Assert.Equal(0, exitCode);
            Assert.True(File.ReadAllBytes(output).AsSpan(0, 4).SequenceEqual(new byte[] { 0x50, 0x4B, 0x03, 0x04 }));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Convert_Docx_To_Text_Uses_Document_Catalog()
    {
        string directory = CreateTempDirectory();
        try
        {
            string input = Path.Combine(directory, "input.docx");
            string output = Path.Combine(directory, "output.txt");
            File.WriteAllBytes(input, DocxDocumentCodec.WriteToArray(Broiler.Documents.Model.RichTextDocument.FromPlainText("Hello DOCX")));

            int exitCode = DocumentConvertService.Convert(input, output);

            Assert.Equal(0, exitCode);
            Assert.Equal("Hello DOCX", File.ReadAllText(output, Encoding.UTF8));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "broiler-doc-convert-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
