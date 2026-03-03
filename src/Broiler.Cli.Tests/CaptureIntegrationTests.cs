using System.Net;

namespace Broiler.Cli.Tests;

/// <summary>
/// Integration tests for the CaptureService using a local HTTP server.
/// Tests use the local rendering engines (HTML-Renderer and YantraJS)
/// instead of Playwright/Chromium.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Engine", "Cli")]
public class CaptureIntegrationTests : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _prefix;
    private readonly string _outputDir;

    public CaptureIntegrationTests()
    {
        // Find an available port by binding to port 0
        var tempListener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        tempListener.Start();
        var port = ((IPEndPoint)tempListener.LocalEndpoint).Port;
        tempListener.Stop();

        _prefix = $"http://localhost:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(_prefix);
        _outputDir = Path.Combine(Path.GetTempPath(), $"broiler-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        try { _listener.Stop(); } catch { }
        try { Directory.Delete(_outputDir, true); } catch { }
    }

    [Fact]
    public async Task CaptureAsync_LocalHtmlFile_ProducesOutput()
    {
        const string html = "<html><body><h1>Hello from Broiler</h1></body></html>";

        _listener.Start();
        var serverTask = Task.Run(() =>
        {
            var ctx = _listener.GetContext();
            var buffer = System.Text.Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html";
            ctx.Response.ContentLength64 = buffer.Length;
            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.Close();
        });

        var outputPath = Path.Combine(_outputDir, "capture.html");
        var service = new CaptureService();

        await service.CaptureAsync(new CaptureOptions
        {
            Url = _prefix,
            OutputPath = outputPath,
            TimeoutSeconds = 30,
        });

        await serverTask;

        Assert.True(File.Exists(outputPath), "Captured file should exist.");
        var content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("Hello from Broiler", content);
    }

    [Fact]
    public async Task CaptureAsync_InvalidUrl_ThrowsHttpRequestException()
    {
        var outputPath = Path.Combine(_outputDir, "fail.html");
        var service = new CaptureService();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.CaptureAsync(new CaptureOptions
            {
                Url = "http://localhost:1/nonexistent",
                OutputPath = outputPath,
                TimeoutSeconds = 5,
            }));
    }

    [Fact]
    public async Task CaptureImageAsync_LocalHtml_ProducesImageFile()
    {
        const string html = "<html><body><h1>Image Test</h1><p>Hello from Broiler</p></body></html>";

        _listener.Start();
        var serverTask = Task.Run(() =>
        {
            var ctx = _listener.GetContext();
            var buffer = System.Text.Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html";
            ctx.Response.ContentLength64 = buffer.Length;
            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.Close();
        });

        var outputPath = Path.Combine(_outputDir, "capture.png");
        var service = new CaptureService();

        await service.CaptureImageAsync(new ImageCaptureOptions
        {
            Url = _prefix,
            OutputPath = outputPath,
            Width = 800,
            Height = 600,
            TimeoutSeconds = 10,
        });

        await serverTask;

        Assert.True(File.Exists(outputPath), "Image capture file should exist.");
        var bytes = await File.ReadAllBytesAsync(outputPath);
        Assert.True(bytes.Length > 100, "Image file should have meaningful content.");
        // Verify PNG magic bytes
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
    }

    [Fact]
    public async Task CaptureImageAsync_JpegFormat_ProducesJpegFile()
    {
        const string html = "<html><body><h1>JPEG Test</h1></body></html>";

        _listener.Start();
        var serverTask = Task.Run(() =>
        {
            var ctx = _listener.GetContext();
            var buffer = System.Text.Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html";
            ctx.Response.ContentLength64 = buffer.Length;
            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.Close();
        });

        var outputPath = Path.Combine(_outputDir, "capture.jpg");
        var service = new CaptureService();

        await service.CaptureImageAsync(new ImageCaptureOptions
        {
            Url = _prefix,
            OutputPath = outputPath,
            Width = 800,
            Height = 600,
            TimeoutSeconds = 10,
        });

        await serverTask;

        Assert.True(File.Exists(outputPath), "JPEG capture file should exist.");
        var bytes = await File.ReadAllBytesAsync(outputPath);
        Assert.True(bytes.Length > 100, "JPEG file should have meaningful content.");
        // Verify JPEG magic bytes
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }

    [Fact]
    public async Task CaptureImageAsync_InvalidUrl_ThrowsHttpRequestException()
    {
        var outputPath = Path.Combine(_outputDir, "fail.png");
        var service = new CaptureService();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.CaptureImageAsync(new ImageCaptureOptions
            {
                Url = "http://localhost:1/nonexistent",
                OutputPath = outputPath,
                TimeoutSeconds = 5,
            }));
    }

    /// <summary>
    /// Regression test: scripts accessing window.localStorage and
    /// window.matchMedia (like the heise.de color-scheme script) must
    /// not crash the capture pipeline.
    /// </summary>
    [Fact]
    public async Task CaptureAsync_HeiseColorSchemeScript_DoesNotThrow()
    {
        const string html = @"<html><head></head><body>
            <script>
                var config = JSON.parse(window.localStorage['akwaConfig-v2'] || '{}')
                var scheme = config.colorScheme ? config.colorScheme.scheme : 'auto'
                if (scheme === 'dark' || (scheme === 'auto' && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
                  document.documentElement.classList.add('dark')
                }
            </script>
            <h1>Hello</h1>
        </body></html>";

        _listener.Start();
        var serverTask = Task.Run(() =>
        {
            var ctx = _listener.GetContext();
            var buffer = System.Text.Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html";
            ctx.Response.ContentLength64 = buffer.Length;
            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.Close();
        });

        var outputPath = Path.Combine(_outputDir, "heise-script.html");
        var service = new CaptureService();

        await service.CaptureAsync(new CaptureOptions
        {
            Url = _prefix,
            OutputPath = outputPath,
            TimeoutSeconds = 30,
        });

        await serverTask;

        Assert.True(File.Exists(outputPath), "Captured file should exist.");
        var content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("Hello", content);
    }
}

