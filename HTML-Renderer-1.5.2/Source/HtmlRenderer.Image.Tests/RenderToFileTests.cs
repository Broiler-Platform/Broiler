namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Tests for rendering HTML to files.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Rendering")]
[Trait("Engine", "HtmlRenderer")]
public class RenderToFileTests(RenderingFixture fixture)
{
    [Fact]
    public void RenderToFile_Png_CreatesValidFile()
    {
        Assert.True(File.Exists(fixture.PngFilePath));
        var bytes = File.ReadAllBytes(fixture.PngFilePath);
        Assert.True(bytes.Length > 0);
        // Verify PNG signature
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
    }

    [Fact]
    public void RenderToFile_Jpeg_CreatesValidFile()
    {
        Assert.True(File.Exists(fixture.JpegFilePath));
        var bytes = File.ReadAllBytes(fixture.JpegFilePath);
        Assert.True(bytes.Length > 0);
        // Verify JPEG signature
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }
}
