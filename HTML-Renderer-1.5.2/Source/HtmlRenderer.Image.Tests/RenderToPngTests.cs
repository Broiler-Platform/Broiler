using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Tests for rendering HTML to PNG format.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Rendering")]
[Trait("Engine", "HtmlRenderer")]
public class RenderToPngTests(RenderingFixture fixture)
{
    [Fact]
    public void RenderToPng_SimpleHtml_ReturnsValidPngWithCorrectMagicBytes()
    {
        Assert.NotNull(fixture.PngBytes);
        Assert.True(fixture.PngBytes.Length > 100);
        // PNG magic bytes: 137 80 78 71 13 10 26 10
        Assert.Equal(0x89, fixture.PngBytes[0]);
        Assert.Equal(0x50, fixture.PngBytes[1]);
        Assert.Equal(0x4E, fixture.PngBytes[2]);
        Assert.Equal(0x47, fixture.PngBytes[3]);
    }

    [Fact]
    public void RenderToPng_StyledHtml_ReturnsValidPng()
    {
        Assert.NotNull(fixture.PngStyledBytes);
        Assert.True(fixture.PngStyledBytes.Length > 100);
    }

    [Fact]
    public void RenderToPng_EmptyHtml_ReturnsValidPng()
    {
        var emptyBytes = HtmlRender.RenderToPng("", 100, 100);
        Assert.NotNull(emptyBytes);
        Assert.True(emptyBytes.Length > 0);
    }
}
