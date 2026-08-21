using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// WPT issue #1491, problem 1: navigation-timing/dom-interactive-media-document.html
/// (<c>&lt;iframe src="../media/A4.webm"&gt;</c>) crashed the whole page render with
/// <c>DomDocument.CreateElement — 'n&#x5;4&#x353;:' is not a valid qualified name</c>.
///
/// The frame's resource was read with <c>File.ReadAllText</c> and handed to the HTML
/// parser whatever it was, so the tokeniser minted that tag name out of WebM bytes.
/// Navigating a frame to media, to an image, or to plain text does not parse the
/// resource as markup: the UA synthesises a document that presents it (HTML
/// §"read-media" / §"read-image" / §"read-text").
/// </summary>
public class EmbeddedMediaDocumentTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("broiler-embedded-doc").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteFile(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string PageWithFrame(string resourceName) => $@"<!DOCTYPE html>
<body style=""margin:0"">
  <iframe src=""{resourceName}"" style=""width:200px;height:200px;border:0"" frameborder=""0""></iframe>
</body>";

    private BBitmap RenderPageWithFrame(string resourceName) =>
        HtmlRender.RenderToImageWithStyleSet(
            PageWithFrame(resourceName), 200, 200,
            baseUrl: new Uri(Path.Combine(_dir, "page.html")).AbsoluteUri);

    /// <summary>
    /// The bytes that produced the crash: the 4118-byte offset of WPT's
    /// <c>media/A4.webm</c>, where <c>&lt;N&#x5;4</c> starts a tag name the HTML
    /// tokeniser accepts and that ends in a colon.
    /// </summary>
    private static byte[] MediaBytesThatTokeniseAsATag() =>
        [0x1A, 0x45, 0xDF, 0xA3, .. "<N4"u8, 0xEB, 0xCD, 0x93, (byte)':', 0x2F, 0xF8, 0x41, 0x98];

    [Fact(Timeout = 600000)]
    public void Frame_To_A_Media_Resource_Does_Not_Parse_Its_Bytes_As_Markup()
    {
        WriteFile("A4.webm", MediaBytesThatTokeniseAsATag());

        // The crash was a throw out of the render, so completing at all is the assertion.
        using var bitmap = RenderPageWithFrame("A4.webm");

        // A media document paints its own black canvas rather than the host page's white.
        var pixel = bitmap.GetPixel(100, 100);
        Assert.True(pixel.R <= 40 && pixel.G <= 40 && pixel.B <= 40,
            $"Expected the media document's black canvas but got ({pixel.R},{pixel.G},{pixel.B}).");
    }

    [Fact(Timeout = 600000)]
    public void Frame_To_An_Opaque_Resource_Paints_Nothing()
    {
        // A PDF has no synthesised document here: the frame stays empty rather
        // than showing the file's bytes as text.
        WriteFile("doc.pdf", [.. "%PDF-1.7\n<pdf::obj junk"u8, 0x00, 0x01, 0x02]);

        using var bitmap = RenderPageWithFrame("doc.pdf");

        var pixel = bitmap.GetPixel(100, 100);
        Assert.True(pixel.R >= 245 && pixel.G >= 245 && pixel.B >= 245,
            $"Expected an empty frame but got ({pixel.R},{pixel.G},{pixel.B}).");
    }

    [Fact(Timeout = 600000)]
    public void Frame_To_An_Extensionless_Binary_Resource_Does_Not_Parse_Its_Bytes_As_Markup()
    {
        // Nothing in the name says binary, so the bytes decide: a NUL byte and an
        // invalid UTF-8 sequence both keep the resource out of the HTML parser.
        WriteFile("resource", [.. "<x::y"u8, 0x00, 0xEB, 0xCD, 0x93]);

        using var bitmap = RenderPageWithFrame("resource");

        var pixel = bitmap.GetPixel(100, 100);
        Assert.True(pixel.R >= 245 && pixel.G >= 245 && pixel.B >= 245,
            $"Expected an empty frame but got ({pixel.R},{pixel.G},{pixel.B}).");
    }

    [Fact(Timeout = 600000)]
    public void Frame_To_An_Extensionless_Text_Resource_Is_Still_Parsed_As_Markup()
    {
        // The common case the sniff must not break: an extensionless HTML resource
        // (WPT serves plenty) is still the frame's document.
        WriteFile("resource", "<body style='margin:0;background:red'>"u8.ToArray());

        using var bitmap = RenderPageWithFrame("resource");

        var pixel = bitmap.GetPixel(100, 100);
        Assert.True(pixel.R >= 200 && pixel.G <= 40 && pixel.B <= 40,
            $"Expected the frame's red document to paint but got ({pixel.R},{pixel.G},{pixel.B}).");
    }

    [Fact(Timeout = 600000)]
    public void Frame_To_A_Plain_Text_Resource_Presents_The_Text()
    {
        WriteFile("notes.txt", "hello <not-a-tag>"u8.ToArray());

        using var bitmap = RenderPageWithFrame("notes.txt");

        // The text document paints glyphs on the default white canvas: some pixel
        // in the first text line must be dark.
        bool painted = false;
        for (int y = 0; y < 40 && !painted; y++)
            for (int x = 0; x < 200 && !painted; x++)
            {
                var p = bitmap.GetPixel(x, y);
                painted = p.R < 128 && p.G < 128 && p.B < 128;
            }

        Assert.True(painted, "Expected the plain-text document's text to paint in the frame.");
    }
}
