using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.Entities;
using TheArtOfDev.HtmlRenderer.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Acid2-style navigation, where a landing page contains a link
/// that must be followed to reach the actual test content. Validates that
/// html-renderer can detect and programmatically follow the first link,
/// matching the Chromium/Playwright navigation pattern.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Engine", "Cli")]
public class Acid2NavigationTests : IDisposable
{
    private const int RenderWidth = 800;
    private const int RenderHeight = 600;

    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    private readonly string _outputDir;

    public Acid2NavigationTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), $"broiler-acid2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
        {
            try { Directory.Delete(_outputDir, recursive: true); }
            catch { /* cleanup is best-effort */ }
        }
    }

    // ---------------------------------------------------------------
    //  LinkNavigator.ExtractLinks
    // ---------------------------------------------------------------

    [Fact]
    public void ExtractLinks_ReturnsAllLinks()
    {
        var html = ReadLandingHtml();
        var links = LinkNavigator.ExtractLinks(html);

        Assert.True(links.Count >= 2, $"Expected at least 2 links, got {links.Count}");
    }

    [Fact]
    public void ExtractFirstLinkHref_ReturnsFirstLink()
    {
        var html = ReadLandingHtml();
        var href = LinkNavigator.ExtractFirstLinkHref(html);

        Assert.NotNull(href);
        Assert.Equal("test.html", href);
    }

    [Fact]
    public void ExtractFirstLinkHref_ReturnsNull_WhenNoLinks()
    {
        var href = LinkNavigator.ExtractFirstLinkHref("<html><body>No links here.</body></html>");
        Assert.Null(href);
    }

    // ---------------------------------------------------------------
    //  LinkNavigator.ResolveUrl
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("http://example.com/landing.html", "test.html", "http://example.com/test.html")]
    [InlineData("http://example.com/dir/landing.html", "test.html", "http://example.com/dir/test.html")]
    [InlineData("http://example.com/dir/landing.html", "/test.html", "http://example.com/test.html")]
    [InlineData("http://example.com/landing.html", "http://other.com/page.html", "http://other.com/page.html")]
    public void ResolveUrl_ResolvesCorrectly(string baseUrl, string relative, string expected)
    {
        var resolved = LinkNavigator.ResolveUrl(baseUrl, relative);
        Assert.Equal(expected, resolved);
    }

    // ---------------------------------------------------------------
    //  LinkNavigator.FollowFirstLinkAsync (file-based)
    // ---------------------------------------------------------------

    [Fact]
    public async Task FollowFirstLinkAsync_NavigatesToLinkedPage()
    {
        var landingPath = Path.Combine(TestDataDir, "acid2", "landing.html");
        var landingHtml = await File.ReadAllTextAsync(landingPath);
        var baseUrl = new Uri(landingPath).AbsoluteUri;

        using var httpClient = new HttpClient();
        var result = await LinkNavigator.FollowFirstLinkAsync(landingHtml, baseUrl, httpClient);

        // The result should be the test.html content, not the landing page
        Assert.DoesNotContain("Welcome to the Acid2 Test", result);
        Assert.Contains("background-color: yellow", result);
    }

    [Fact]
    public async Task FollowFirstLinkAsync_ReturnsOriginal_WhenNoLinks()
    {
        var html = "<html><body>No links.</body></html>";

        using var httpClient = new HttpClient();
        var result = await LinkNavigator.FollowFirstLinkAsync(html, "http://example.com/", httpClient);

        Assert.Equal(html, result);
    }

    [Fact]
    public async Task FollowFirstLinkAsync_ReturnsOriginal_WhenOnlyAnchorLinks()
    {
        var html = "<html><body><a href=\"#section1\">Jump</a></body></html>";

        using var httpClient = new HttpClient();
        var result = await LinkNavigator.FollowFirstLinkAsync(html, "http://example.com/", httpClient);

        Assert.Equal(html, result);
    }

    // ---------------------------------------------------------------
    //  HtmlContainer.GetLinks
    // ---------------------------------------------------------------

    [Fact]
    public void HtmlContainer_GetLinks_ReturnsLinksFromParsedHtml()
    {
        var html = ReadLandingHtml();

        using var container = new HtmlContainer();
        container.SetHtml(html);
        var links = container.GetLinks();

        Assert.True(links.Count >= 2, $"Expected at least 2 links, got {links.Count}");
        Assert.Equal("test.html", links[0].Href);
    }

    // ---------------------------------------------------------------
    //  Acid2 rendering after link following
    // ---------------------------------------------------------------

    [Fact]
    public void Acid2Test_RendersToValidPng()
    {
        var html = ReadTestHtml();
        var pngPath = Path.Combine(_outputDir, "acid2-test.png");

        HtmlRender.RenderToFile(html, RenderWidth, RenderHeight, pngPath, SKEncodedImageFormat.Png);

        Assert.True(File.Exists(pngPath), "Rendered PNG file should exist.");
        var bytes = File.ReadAllBytes(pngPath);
        Assert.True(bytes.Length > 500, "Rendered PNG should have meaningful content.");
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);
    }

    [Fact]
    public void Acid2Test_ProducesNonBlankRendering()
    {
        var html = ReadTestHtml();

        using var bitmap = HtmlRender.RenderToImage(html, RenderWidth, RenderHeight);

        int nonWhitePixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Red != 255 || pixel.Green != 255 || pixel.Blue != 255)
                    nonWhitePixels++;
            }
        }

        Assert.True(nonWhitePixels > 500,
            $"Expected more than 500 non-white pixels, got {nonWhitePixels}");
    }

    [Fact]
    public void Acid2Landing_RendersDifferentlyFromTest()
    {
        var landingHtml = ReadLandingHtml();
        var testHtml = ReadTestHtml();

        using var landingBitmap = HtmlRender.RenderToImage(landingHtml, RenderWidth, RenderHeight);
        using var testBitmap = HtmlRender.RenderToImage(testHtml, RenderWidth, RenderHeight);

        // Landing and test pages should produce visually different output
        int differentPixels = 0;
        for (int y = 0; y < landingBitmap.Height; y++)
        {
            for (int x = 0; x < landingBitmap.Width; x++)
            {
                var lp = landingBitmap.GetPixel(x, y);
                var tp = testBitmap.GetPixel(x, y);
                if (lp != tp) differentPixels++;
            }
        }

        Assert.True(differentPixels > 100,
            "Landing and test pages should produce different renderings");
    }

    // ---------------------------------------------------------------
    //  End-to-end: CaptureService with FollowFirstLink
    // ---------------------------------------------------------------

    [Fact]
    public async Task CaptureImageAsync_FollowFirstLink_RendersLinkedPage()
    {
        var landingPath = Path.Combine(TestDataDir, "acid2", "landing.html");
        var outputPath = Path.Combine(_outputDir, "followed.png");

        var options = new ImageCaptureOptions
        {
            Url = new Uri(landingPath).AbsoluteUri,
            OutputPath = outputPath,
            Width = RenderWidth,
            Height = RenderHeight,
            FollowFirstLink = true,
        };

        var service = new CaptureService();
        await service.CaptureImageAsync(options);

        Assert.True(File.Exists(outputPath), "Output image should exist.");
        var bytes = File.ReadAllBytes(outputPath);
        Assert.True(bytes.Length > 500, "Output image should have meaningful content.");

        // Verify it rendered the test page (with colored elements), not the landing page
        using var bitmap = SKBitmap.Decode(outputPath);
        int nonWhitePixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Red != 255 || pixel.Green != 255 || pixel.Blue != 255)
                    nonWhitePixels++;
            }
        }

        Assert.True(nonWhitePixels > 500,
            $"Follow-first-link rendering should produce colored content, got {nonWhitePixels} non-white pixels");
    }

    [Fact]
    public async Task CaptureImageAsync_WithoutFollowFirstLink_RendersLandingPage()
    {
        var landingPath = Path.Combine(TestDataDir, "acid2", "landing.html");
        var withFollowPath = Path.Combine(_outputDir, "with-follow.png");
        var withoutFollowPath = Path.Combine(_outputDir, "without-follow.png");

        var service = new CaptureService();

        await service.CaptureImageAsync(new ImageCaptureOptions
        {
            Url = new Uri(landingPath).AbsoluteUri,
            OutputPath = withFollowPath,
            Width = RenderWidth,
            Height = RenderHeight,
            FollowFirstLink = true,
        });

        await service.CaptureImageAsync(new ImageCaptureOptions
        {
            Url = new Uri(landingPath).AbsoluteUri,
            OutputPath = withoutFollowPath,
            Width = RenderWidth,
            Height = RenderHeight,
            FollowFirstLink = false,
        });

        // Both files should exist and differ
        Assert.True(File.Exists(withFollowPath));
        Assert.True(File.Exists(withoutFollowPath));

        var withFollowBytes = File.ReadAllBytes(withFollowPath);
        var withoutFollowBytes = File.ReadAllBytes(withoutFollowPath);

        // The files should be different since they render different pages
        Assert.NotEqual(withFollowBytes, withoutFollowBytes);
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------

    private static string ReadLandingHtml() =>
        File.ReadAllText(Path.Combine(TestDataDir, "acid2", "landing.html"));

    private static string ReadTestHtml() =>
        File.ReadAllText(Path.Combine(TestDataDir, "acid2", "test.html"));
}
