using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Tests for rendering HTML to SKBitmap images with various HTML samples.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Rendering")]
[Trait("Engine", "HtmlRenderer")]
public class RenderToImageTests(RenderingFixture fixture)
{
    [Fact]
    public void RenderToImage_SimpleDiv_CreatesCorrectSize()
    {
        Assert.NotNull(fixture.SimpleDiv);
        Assert.Equal(400, fixture.SimpleDiv.Width);
        Assert.Equal(300, fixture.SimpleDiv.Height);
    }

    [Fact]
    public void RenderToImage_EmptyHtmlWithDefaultBackground_IsWhite()
    {
        var pixel = fixture.EmptyWhiteBackground.GetPixel(0, 0);
        Assert.Equal(255, pixel.Red);
        Assert.Equal(255, pixel.Green);
        Assert.Equal(255, pixel.Blue);
    }

    [Fact]
    public void RenderToImage_EmptyHtmlWithCustomBackground_IsApplied()
    {
        var pixel = fixture.EmptyRedBackground.GetPixel(0, 0);
        Assert.Equal(255, pixel.Red);
        Assert.Equal(0, pixel.Green);
        Assert.Equal(0, pixel.Blue);
    }

    [Fact]
    public void RenderToImage_ColoredDivAndNestedElements_RenderCorrectly()
    {
        Assert.NotNull(fixture.ColoredDiv);
        // Check that some pixels in the blue div area are non-white
        bool hasNonWhite = false;
        for (int y = 0; y < 20 && !hasNonWhite; y++)
            for (int x = 0; x < 20 && !hasNonWhite; x++)
            {
                var p = fixture.ColoredDiv.GetPixel(x, y);
                if (p.Red != 255 || p.Green != 255 || p.Blue != 255)
                    hasNonWhite = true;
            }
        Assert.True(hasNonWhite, "Expected non-white pixels from blue background div");
    }

    [Fact]
    public void RenderToImage_TableAndCssStyles_DoNotThrow()
    {
        Assert.NotNull(fixture.TableAndStyles);
        Assert.Equal(400, fixture.TableAndStyles.Width);
    }

    [Fact]
    public void RenderToImage_LargeDocument_DoesNotThrow()
    {
        Assert.NotNull(fixture.LargeDocument);
        Assert.Equal(800, fixture.LargeDocument.Width);
    }

    [Fact]
    public void RenderToImageAutoSized_CreatesReasonableSize()
    {
        Assert.NotNull(fixture.AutoSized);
        Assert.True(fixture.AutoSized.Width > 0);
        Assert.True(fixture.AutoSized.Height > 0);
        Assert.True(fixture.AutoSized.Width <= 4096);
    }

    [Fact]
    public void RenderToImageAutoSized_EmptyHtml_ReturnsMinimalBitmap()
    {
        using var bitmap = HtmlRender.RenderToImageAutoSized("", maxWidth: 100);
        Assert.True(bitmap.Width >= 1);
        Assert.True(bitmap.Height >= 1);
    }

    [Fact]
    public void RenderToImageAutoSized_NullHtml_ReturnsMinimalBitmap()
    {
        using var bitmap = HtmlRender.RenderToImageAutoSized(null, maxWidth: 100);
        Assert.True(bitmap.Width >= 1);
        Assert.True(bitmap.Height >= 1);
    }
}
