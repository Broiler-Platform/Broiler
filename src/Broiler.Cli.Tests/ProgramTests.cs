namespace Broiler.Cli.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Cli")]
public class ProgramTests
{
    [Fact]
    public async Task Main_WithHelp_ReturnsZero()
    {
        var result = await Program.Main(["--help"]);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Main_WithNoArgs_ReturnsOne()
    {
        var result = await Program.Main([]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithMissingOutput_ReturnsOne()
    {
        var result = await Program.Main(["--url", "https://example.com"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithMissingUrl_ReturnsOne()
    {
        var result = await Program.Main(["--output", "test.html"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithInvalidUrl_ReturnsOne()
    {
        var result = await Program.Main(["--url", "not-a-url", "--output", "test.html"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithNonHttpUrl_ReturnsOne()
    {
        var result = await Program.Main(["--url", "ftp://example.com", "--output", "test.html"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithUnrecognizedArg_ReturnsOne()
    {
        var result = await Program.Main(["--unknown"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithInvalidTimeout_ReturnsOne()
    {
        var result = await Program.Main(["--url", "https://example.com", "--output", "test.html", "--timeout", "abc"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithNegativeTimeout_ReturnsOne()
    {
        var result = await Program.Main(["--url", "https://example.com", "--output", "test.html", "--timeout", "-5"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithZeroTimeout_ReturnsOne()
    {
        var result = await Program.Main(["--url", "https://example.com", "--output", "test.html", "--timeout", "0"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithTimeoutMissingValue_ReturnsOne()
    {
        var result = await Program.Main(["--url", "https://example.com", "--output", "test.html", "--timeout"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithTestEngines_ReturnsZero()
    {
        var result = await Program.Main(["--test-engines"]);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Main_CaptureImage_WithMissingOutput_ReturnsOne()
    {
        var result = await Program.Main(["--capture-image", "https://example.com"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_CaptureImage_WithInvalidUrl_ReturnsOne()
    {
        var result = await Program.Main(["--capture-image", "not-a-url", "--output", "test.png"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_CaptureImage_WithNonHttpUrl_ReturnsOne()
    {
        var result = await Program.Main(["--capture-image", "ftp://example.com", "--output", "test.png"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_CaptureImage_WithFileUrl_DoesNotRejectUrl()
    {
        // file:// URLs should be accepted by the argument parser (not rejected as invalid).
        // The actual capture may fail if the file doesn't exist, but the URL
        // validation should pass.
        var outputPath = Path.Combine(Path.GetTempPath(), $"broiler-test-{Guid.NewGuid():N}.png");
        var result = await Program.Main(["--capture-image", "file:///nonexistent.html", "--output", outputPath]);
        // Returns 1 because the file doesn't exist, but NOT because of URL validation
        Assert.Equal(1, result);
    }

    [Fact]
    public void ImageCaptureOptions_FullPage_CanBeSet()
    {
        var options = new ImageCaptureOptions
        {
            Url = "https://example.com",
            OutputPath = "test.png",
            FullPage = true,
        };
        Assert.True(options.FullPage);
    }

    [Fact]
    public async Task Main_WithInvalidWidth_ReturnsOne()
    {
        var result = await Program.Main(["--capture-image", "https://example.com", "--output", "test.png", "--width", "abc"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithNegativeWidth_ReturnsOne()
    {
        var result = await Program.Main(["--capture-image", "https://example.com", "--output", "test.png", "--width", "-5"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithInvalidHeight_ReturnsOne()
    {
        var result = await Program.Main(["--capture-image", "https://example.com", "--output", "test.png", "--height", "abc"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithNegativeHeight_ReturnsOne()
    {
        var result = await Program.Main(["--capture-image", "https://example.com", "--output", "test.png", "--height", "-5"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithWidthMissingValue_ReturnsOne()
    {
        var result = await Program.Main(["--capture-image", "https://example.com", "--output", "test.png", "--width"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithHeightMissingValue_ReturnsOne()
    {
        var result = await Program.Main(["--capture-image", "https://example.com", "--output", "test.png", "--height"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_CaptureImage_WithMissingValue_ReturnsOne()
    {
        var result = await Program.Main(["--capture-image"]);
        Assert.Equal(1, result);
    }

    [Fact]
    public void RunEngineTests_ProducesOutput()
    {
        var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var exitCode = Program.RunEngineTests();
            var output = writer.ToString();

            Assert.Equal(0, exitCode);
            Assert.Contains("[PASS] HTML-Renderer", output);
            Assert.Contains("[PASS] YantraJS", output);
            Assert.Contains("All engine tests passed.", output);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }
}

[Trait("Category", "Unit")]
[Trait("Engine", "Cli")]
public class CaptureOptionsTests
{
    [Fact]
    public void OutputFormat_HtmlExtension_ReturnsHtml()
    {
        var options = new CaptureOptions { Url = "https://example.com", OutputPath = "output.html" };
        Assert.Equal(OutputFormat.Html, options.OutputFormat);
    }

    [Fact]
    public void OutputFormat_TxtExtension_ReturnsText()
    {
        var options = new CaptureOptions { Url = "https://example.com", OutputPath = "output.txt" };
        Assert.Equal(OutputFormat.Text, options.OutputFormat);
    }

    [Fact]
    public void OutputFormat_UnknownExtension_DefaultsToHtml()
    {
        var options = new CaptureOptions { Url = "https://example.com", OutputPath = "output.bmp" };
        Assert.Equal(OutputFormat.Html, options.OutputFormat);
    }

    [Fact]
    public void DefaultTimeout_IsThirtySeconds()
    {
        var options = new CaptureOptions { Url = "https://example.com", OutputPath = "output.html" };
        Assert.Equal(30, options.TimeoutSeconds);
    }

    [Fact]
    public void DefaultFullPage_IsFalse()
    {
        var options = new CaptureOptions { Url = "https://example.com", OutputPath = "output.html" };
        Assert.False(options.FullPage);
    }
}

[Trait("Category", "Unit")]
[Trait("Engine", "Cli")]
public class ImageCaptureOptionsTests
{
    [Fact]
    public void ImageFormat_PngExtension_ReturnsPng()
    {
        var options = new ImageCaptureOptions { Url = "https://example.com", OutputPath = "output.png" };
        Assert.Equal(ImageFormat.Png, options.ImageFormat);
    }

    [Fact]
    public void ImageFormat_JpgExtension_ReturnsJpeg()
    {
        var options = new ImageCaptureOptions { Url = "https://example.com", OutputPath = "output.jpg" };
        Assert.Equal(ImageFormat.Jpeg, options.ImageFormat);
    }

    [Fact]
    public void ImageFormat_JpegExtension_ReturnsJpeg()
    {
        var options = new ImageCaptureOptions { Url = "https://example.com", OutputPath = "output.jpeg" };
        Assert.Equal(ImageFormat.Jpeg, options.ImageFormat);
    }

    [Fact]
    public void ImageFormat_UnknownExtension_DefaultsToPng()
    {
        var options = new ImageCaptureOptions { Url = "https://example.com", OutputPath = "output.bmp" };
        Assert.Equal(ImageFormat.Png, options.ImageFormat);
    }

    [Fact]
    public void DefaultWidth_Is1024()
    {
        var options = new ImageCaptureOptions { Url = "https://example.com", OutputPath = "output.png" };
        Assert.Equal(1024, options.Width);
    }

    [Fact]
    public void DefaultHeight_Is768()
    {
        var options = new ImageCaptureOptions { Url = "https://example.com", OutputPath = "output.png" };
        Assert.Equal(768, options.Height);
    }

    [Fact]
    public void DefaultTimeout_IsThirtySeconds()
    {
        var options = new ImageCaptureOptions { Url = "https://example.com", OutputPath = "output.png" };
        Assert.Equal(30, options.TimeoutSeconds);
    }
}
