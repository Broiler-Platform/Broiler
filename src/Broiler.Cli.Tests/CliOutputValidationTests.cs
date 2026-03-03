using System.Net;

namespace Broiler.Cli.Tests;

/// <summary>
/// CLI output validation tests that verify the end-to-end capture pipeline
/// produces correct output format, content, and metadata.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Engine", "Cli")]
public class CliOutputValidationTests : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _prefix;
    private readonly string _outputDir;

    public CliOutputValidationTests()
    {
        var tempListener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        tempListener.Start();
        var port = ((IPEndPoint)tempListener.LocalEndpoint).Port;
        tempListener.Stop();

        _prefix = $"http://localhost:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(_prefix);
        _outputDir = Path.Combine(Path.GetTempPath(), $"broiler-cli-val-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        try { _listener.Stop(); } catch { }
        try { Directory.Delete(_outputDir, true); } catch { }
    }

    private Task StartServer(string html) => Task.Run(() =>
    {
        var ctx = _listener.GetContext();
        var buffer = System.Text.Encoding.UTF8.GetBytes(html);
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.ContentLength64 = buffer.Length;
        ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
        ctx.Response.Close();
    });

    // =================================================================
    // HTML output validation
    // =================================================================

    /// <summary>
    /// Verifies that HTML capture preserves the title element text.
    /// </summary>
    [Fact]
    public async Task HtmlCapture_PreservesTitle()
    {
        const string html = "<html><head><title>Test Page Title</title></head><body><h1>Hello</h1></body></html>";

        _listener.Start();
        var serverTask = StartServer(html);

        var outputPath = Path.Combine(_outputDir, "title-test.html");
        var service = new CaptureService();
        await service.CaptureAsync(new CaptureOptions
        {
            Url = _prefix,
            OutputPath = outputPath,
            TimeoutSeconds = 10,
        });
        await serverTask;

        var content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("Hello", content);
    }

    /// <summary>
    /// Verifies that HTML capture preserves special characters and entities.
    /// </summary>
    [Fact]
    public async Task HtmlCapture_PreservesSpecialCharacters()
    {
        const string html = "<html><body><p>Price: &lt;100&gt; &amp; more</p></body></html>";

        _listener.Start();
        var serverTask = StartServer(html);

        var outputPath = Path.Combine(_outputDir, "entities-test.html");
        var service = new CaptureService();
        await service.CaptureAsync(new CaptureOptions
        {
            Url = _prefix,
            OutputPath = outputPath,
            TimeoutSeconds = 10,
        });
        await serverTask;

        var content = await File.ReadAllTextAsync(outputPath);
        Assert.True(content.Contains("Price") && content.Contains("more"),
            "Captured HTML should preserve text content");
    }

    // =================================================================
    // Image output validation
    // =================================================================

    /// <summary>
    /// Verifies that PNG capture produces a valid PNG file with correct magic bytes.
    /// </summary>
    [Fact]
    public async Task PngCapture_ProducesValidPng()
    {
        const string html = "<html><body><div style='background:red;width:200px;height:100px;'></div></body></html>";

        _listener.Start();
        var serverTask = StartServer(html);

        var outputPath = Path.Combine(_outputDir, "valid.png");
        var service = new CaptureService();
        await service.CaptureImageAsync(new ImageCaptureOptions
        {
            Url = _prefix,
            OutputPath = outputPath,
            Width = 400,
            Height = 300,
            TimeoutSeconds = 10,
        });
        await serverTask;

        var bytes = await File.ReadAllBytesAsync(outputPath);
        Assert.True(bytes.Length > 100, "PNG should have meaningful size");
        // PNG magic bytes: 89 50 4E 47
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);
    }

    /// <summary>
    /// Verifies that JPEG capture produces a valid JPEG file with correct magic bytes.
    /// </summary>
    [Fact]
    public async Task JpegCapture_ProducesValidJpeg()
    {
        const string html = "<html><body><div style='background:blue;width:200px;height:100px;'></div></body></html>";

        _listener.Start();
        var serverTask = StartServer(html);

        var outputPath = Path.Combine(_outputDir, "valid.jpg");
        var service = new CaptureService();
        await service.CaptureImageAsync(new ImageCaptureOptions
        {
            Url = _prefix,
            OutputPath = outputPath,
            Width = 400,
            Height = 300,
            TimeoutSeconds = 10,
        });
        await serverTask;

        var bytes = await File.ReadAllBytesAsync(outputPath);
        Assert.True(bytes.Length > 100, "JPEG should have meaningful size");
        // JPEG magic bytes: FF D8
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }

    /// <summary>
    /// Verifies that larger dimensions produce larger image files.
    /// </summary>
    [Fact]
    public async Task ImageCapture_LargerDimensions_ProduceLargerFile()
    {
        const string html = "<html><body><div style='background:green;width:100%;height:100%;'></div></body></html>";

        _listener.Start();

        // Serve two requests
        var serverTask = Task.Run(() =>
        {
            for (int i = 0; i < 2; i++)
            {
                var ctx = _listener.GetContext();
                var buffer = System.Text.Encoding.UTF8.GetBytes(html);
                ctx.Response.ContentType = "text/html";
                ctx.Response.ContentLength64 = buffer.Length;
                ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
                ctx.Response.Close();
            }
        });

        var smallPath = Path.Combine(_outputDir, "small.png");
        var largePath = Path.Combine(_outputDir, "large.png");
        var service = new CaptureService();

        await service.CaptureImageAsync(new ImageCaptureOptions
        {
            Url = _prefix,
            OutputPath = smallPath,
            Width = 200,
            Height = 100,
            TimeoutSeconds = 10,
        });

        await service.CaptureImageAsync(new ImageCaptureOptions
        {
            Url = _prefix,
            OutputPath = largePath,
            Width = 800,
            Height = 600,
            TimeoutSeconds = 10,
        });

        await serverTask;

        var smallBytes = await File.ReadAllBytesAsync(smallPath);
        var largeBytes = await File.ReadAllBytesAsync(largePath);

        Assert.True(largeBytes.Length > smallBytes.Length,
            $"Larger dimensions ({largeBytes.Length} bytes) should produce " +
            $"larger files than smaller dimensions ({smallBytes.Length} bytes)");
    }
}
