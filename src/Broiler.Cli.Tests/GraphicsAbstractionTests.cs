using Broiler.Cli;
using Broiler.HTML.Image;
using RectangleF = System.Drawing.RectangleF;

namespace Broiler.Cli.Tests;

public class GraphicsAbstractionTests
{
    [Fact]
    public void RenderToPng_With_BColor_Background_Fills_Empty_Canvas()
    {
        var png = HtmlRender.RenderToPng(string.Empty, 2, 2, new BColor(12, 34, 56, 255));

        using var bitmap = BBitmap.Decode(png);

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal((byte)12, pixel.R);
        Assert.Equal((byte)34, pixel.G);
        Assert.Equal((byte)56, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void RenderToImage_With_BColor_Background_Returns_BBitmap()
    {
        using var bitmap = HtmlRender.RenderToImage(string.Empty, 2, 2, new BColor(12, 34, 56, 255));

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal((byte)12, pixel.R);
        Assert.Equal((byte)34, pixel.G);
        Assert.Equal((byte)56, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void RenderToImage_With_Defaultable_BColor_Background_Returns_BBitmap()
    {
        using var bitmap = HtmlRender.RenderToImage(string.Empty, 2, 2, new BColor(12, 34, 56, 255));

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal((byte)12, pixel.R);
        Assert.Equal((byte)34, pixel.G);
        Assert.Equal((byte)56, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void RenderToFile_With_BImageFormat_Jpeg_Writes_Jpeg_File()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");

        try
        {
            HtmlRender.RenderToFile(string.Empty, 2, 2, outputPath, BImageFormat.Jpeg, backgroundColor: BColor.White);

            var bytes = File.ReadAllBytes(outputPath);
            Assert.True(bytes.Length > 2);
            Assert.Equal((byte)0xFF, bytes[0]);
            Assert.Equal((byte)0xD8, bytes[1]);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Fact]
    public void RenderToFileAutoSized_With_BImageFormat_Png_Writes_Png_File()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");

        try
        {
            HtmlRender.RenderToFileAutoSized("<html><body>test</body></html>", outputPath, maxWidth: 200, maxHeight: 200, format: BImageFormat.Png);

            var bytes = File.ReadAllBytes(outputPath);
            Assert.True(bytes.Length > 8);
            Assert.Equal((byte)0x89, bytes[0]);
            Assert.Equal((byte)0x50, bytes[1]);
            Assert.Equal((byte)0x4E, bytes[2]);
            Assert.Equal((byte)0x47, bytes[3]);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Fact]
    public void RenderToImageAutoSized_With_BColor_Background_Returns_BBitmap()
    {
        const string html = "<html><body style='margin:0'><div style='width:40px;height:30px;background:#123456'></div></body></html>";

        using var bitmap = HtmlRender.RenderToImageAutoSized(html, 200, 200, BColor.White);

        Assert.True(bitmap.Width >= 40);
        Assert.True(bitmap.Height >= 30);
    }

    [Fact]
    public void RenderToImageAtAnchor_With_BColor_Background_Returns_Anchored_BBitmap()
    {
        const string html = """
            <html><body style='margin:0'>
            <div style='height:20px;background:#ffffff'></div>
            <div id='target' style='width:20px;height:20px;background:#123456'></div>
            </body></html>
            """;

        using var bitmap = HtmlRender.RenderToImageAtAnchor(html, "target", 20, 20, BColor.White);

        Assert.NotNull(bitmap);
        var pixel = bitmap!.GetPixel(0, 0);
        Assert.Equal((byte)0x12, pixel.R);
        Assert.Equal((byte)0x34, pixel.G);
        Assert.Equal((byte)0x56, pixel.B);
        Assert.Equal((byte)0xFF, pixel.A);
    }

    [Fact]
    public void RenderToImageAtAnchor_With_Missing_Anchor_Returns_Null()
    {
        using var bitmap = HtmlRender.RenderToImageAtAnchor("<html><body>test</body></html>", "missing", 20, 20, BColor.White);

        Assert.Null(bitmap);
    }

    [Fact]
    public async Task CaptureImageAsync_With_Fragment_Renders_Anchored_Viewport()
    {
        var htmlPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");

        const string html = """
            <html><body style='margin:0'>
            <div style='height:20px;background:#ffffff'></div>
            <div id='target' style='width:20px;height:20px;background:#123456'></div>
            </body></html>
            """;

        try
        {
            await File.WriteAllTextAsync(htmlPath, html);

            var captureService = new CaptureService();
            await captureService.CaptureImageAsync(new ImageCaptureOptions
            {
                Url = new Uri(htmlPath).AbsoluteUri + "#target",
                OutputPath = outputPath,
                Width = 20,
                Height = 20,
            });

            using var bitmap = BBitmap.Decode(outputPath);
            var pixel = bitmap.GetPixel(0, 0);
            Assert.Equal((byte)0x12, pixel.R);
            Assert.Equal((byte)0x34, pixel.G);
            Assert.Equal((byte)0x56, pixel.B);
            Assert.Equal((byte)0xFF, pixel.A);
        }
        finally
        {
            if (File.Exists(htmlPath))
                File.Delete(htmlPath);
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Fact]
    public void HtmlContainer_PerformLayout_Without_Explicit_Skia_Surface_Builds_FragmentTree()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new System.Drawing.SizeF(50, 50);
        container.SetHtml("<html><body style='margin:0'><div id='target' style='width:20px;height:10px'></div></body></html>");

        container.PerformLayout(new RectangleF(0, 0, 50, 50));

        Assert.NotNull(container.LatestFragmentTree);
        Assert.NotNull(container.GetElementRectangle("target"));
    }

    [Fact]
    public void HtmlContainer_PerformPaint_With_BBitmap_Surface_Renders_Content()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new System.Drawing.SizeF(40, 40);
        container.SetHtml("<html><body style='margin:0;background:#123456'></body></html>");

        using var bitmap = new BBitmap(40, 40);
        bitmap.Erase(BColor.White);
        var clip = new RectangleF(0, 0, 40, 40);

        container.PerformLayout(bitmap, clip);
        container.PerformPaint(bitmap, clip);

        var pixel = bitmap.GetPixel(10, 10);
        Assert.Equal((byte)0x12, pixel.R);
        Assert.Equal((byte)0x34, pixel.G);
        Assert.Equal((byte)0x56, pixel.B);
        Assert.Equal((byte)0xFF, pixel.A);
    }

    [Fact]
    public void HtmlContainer_PerformPaint_With_BBitmap_Translation_Renders_Offset_Content()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new System.Drawing.SizeF(20, 20);
        container.SetHtml("""
            <html><body style='margin:0'>
            <div style='height:5px;background:#ffffff'></div>
            <div style='width:20px;height:15px;background:#123456'></div>
            </body></html>
            """);

        using var bitmap = new BBitmap(20, 15);
        bitmap.Erase(BColor.White);
        container.PerformLayout(new RectangleF(0, 0, 20, 20));
        container.PerformPaint(bitmap, new RectangleF(0, 5, 20, 15), new System.Drawing.PointF(0, -5));

        var pixel = bitmap.GetPixel(10, 0);
        Assert.Equal((byte)0x12, pixel.R);
        Assert.Equal((byte)0x34, pixel.G);
        Assert.Equal((byte)0x56, pixel.B);
        Assert.Equal((byte)0xFF, pixel.A);
    }

    [Fact]
    public void PixelDiffRunner_Compare_With_BBitmap_Returns_BackendNeutral_DiffBitmap()
    {
        using var actual = new BBitmap(1, 1);
        using var baseline = new BBitmap(1, 1);
        actual.SetPixel(0, 0, new BColor(255, 0, 0, 255));
        baseline.SetPixel(0, 0, new BColor(0, 255, 0, 255));

        using var result = PixelDiffRunner.Compare(actual, baseline);

        Assert.NotNull(result.DiffBitmap);
        var pixel = result.DiffBitmap!.GetPixel(0, 0);
        Assert.Equal((byte)255, pixel.R);
        Assert.Equal((byte)0, pixel.G);
        Assert.Equal((byte)255, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

}
