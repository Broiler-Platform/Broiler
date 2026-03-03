namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Tests for rendering HTML to JPEG format.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Rendering")]
[Trait("Engine", "HtmlRenderer")]
public class RenderToJpegTests(RenderingFixture fixture)
{
    [Fact]
    public void RenderToJpeg_ReturnsValidJpegWithCorrectMagicBytes()
    {
        Assert.NotNull(fixture.JpegBytes);
        Assert.True(fixture.JpegBytes.Length > 0);
        // JPEG magic bytes: FF D8 FF
        Assert.Equal(0xFF, fixture.JpegBytes[0]);
        Assert.Equal(0xD8, fixture.JpegBytes[1]);
        Assert.Equal(0xFF, fixture.JpegBytes[2]);
    }

    [Fact]
    public void RenderToJpeg_QualityAffectsFileSize() => Assert.True(fixture.JpegHighQuality.Length > fixture.JpegLowQuality.Length,
            $"High quality ({fixture.JpegHighQuality.Length}) should be larger than low quality ({fixture.JpegLowQuality.Length})");
}
