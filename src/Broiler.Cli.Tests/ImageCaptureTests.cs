using System.Net;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// Integration tests for capturing sites as images using HtmlRenderer.Image.
/// Verifies the rendering pipeline produces valid image output from HTML content.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Engine", "Cli")]
public class ImageCaptureTests : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _prefix;
    private readonly string _outputDir;

    public ImageCaptureTests()
    {
        var tempListener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        tempListener.Start();
        var port = ((IPEndPoint)tempListener.LocalEndpoint).Port;
        tempListener.Stop();

        _prefix = $"http://localhost:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(_prefix);
        _outputDir = Path.Combine(Path.GetTempPath(), $"broiler-imgtest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        try { _listener.Stop(); } catch { }
        try { Directory.Delete(_outputDir, true); } catch { }
    }

    [Fact]
    public async Task CaptureAsImage_LocalHtml_ProducesValidPng()
    {
        const string html = "<html><body><h1>Image Capture Test</h1><p>Content here.</p></body></html>";

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

        // Download the HTML content
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var content = await httpClient.GetStringAsync(_prefix);
        await serverTask;

        // Render to PNG image
        var pngPath = Path.Combine(_outputDir, "capture.png");
        HtmlRender.RenderToFile(content, 800, 600, pngPath, SKEncodedImageFormat.Png);

        Assert.True(File.Exists(pngPath), "PNG capture file should exist.");
        var bytes = await File.ReadAllBytesAsync(pngPath);
        Assert.True(bytes.Length > 100, "PNG file should have meaningful content.");
        // Verify PNG magic bytes
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
    }

    [Fact]
    public async Task CaptureAsImage_LocalHtml_ProducesValidJpeg()
    {
        const string html = "<html><body style='background-color:#eee;'><h2>JPEG Test</h2></body></html>";

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

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var content = await httpClient.GetStringAsync(_prefix);
        await serverTask;

        // Render to JPEG
        var jpegPath = Path.Combine(_outputDir, "capture.jpg");
        HtmlRender.RenderToFile(content, 800, 600, jpegPath, SKEncodedImageFormat.Jpeg);

        Assert.True(File.Exists(jpegPath), "JPEG capture file should exist.");
        var bytes = await File.ReadAllBytesAsync(jpegPath);
        Assert.True(bytes.Length > 100, "JPEG file should have meaningful content.");
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }

    [Fact]
    public void CaptureAsImage_StaticHtml_ProducesConsistentOutput()
    {
        var html = "<html><body><p style='color:blue;font-size:18px;'>Consistent rendering test</p></body></html>";

        using var bitmap1 = HtmlRender.RenderToImage(html, 400, 200);
        using var bitmap2 = HtmlRender.RenderToImage(html, 400, 200);

        Assert.True(ImageComparer.AreIdentical(bitmap1, bitmap2),
            "Same HTML content should produce identical images across renders.");
    }

    [Fact]
    public void CaptureAsImage_ComplexHtml_ProducesNonEmptyImage()
    {
        var html = @"
            <html>
            <head><style>
                body { font-family: Arial; margin: 0; padding: 20px; }
                .header { background-color: #333; color: white; padding: 10px; }
                table { border-collapse: collapse; width: 100%; }
                td, th { border: 1px solid #ddd; padding: 8px; }
            </style></head>
            <body>
                <div class='header'><h1>Site Capture</h1></div>
                <table>
                    <tr><th>Name</th><th>Value</th></tr>
                    <tr><td>Alpha</td><td>100</td></tr>
                    <tr><td>Beta</td><td>200</td></tr>
                </table>
            </body>
            </html>";

        var pngBytes = HtmlRender.RenderToPng(html, 800, 600);
        Assert.True(pngBytes.Length > 500, "Complex HTML should produce substantial image data.");
    }
}
