using Broiler.HTML.Image;
using SkiaSharp;

namespace Broiler.Cli.Tests;

public class GraphicsAbstractionTests
{
    [Fact]
    public void RenderToPng_With_BColor_Background_Fills_Empty_Canvas()
    {
        var png = HtmlRender.RenderToPng(string.Empty, 2, 2, new BColor(12, 34, 56, 255));

        using var bitmap = SKBitmap.Decode(png);
        Assert.NotNull(bitmap);

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal((byte)12, pixel.Red);
        Assert.Equal((byte)34, pixel.Green);
        Assert.Equal((byte)56, pixel.Blue);
        Assert.Equal((byte)255, pixel.Alpha);
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
    public void RenderToImage_With_SKColor_Background_Still_Returns_SKBitmap()
    {
        using var bitmap = HtmlRender.RenderToImage(string.Empty, 2, 2, new SKColor(12, 34, 56, 255));

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal((byte)12, pixel.Red);
        Assert.Equal((byte)34, pixel.Green);
        Assert.Equal((byte)56, pixel.Blue);
        Assert.Equal((byte)255, pixel.Alpha);
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

    [Fact]
    public void PixelDiffResult_DiffImage_Compatibility_Shims_From_DiffBitmap()
    {
        using var actual = new BBitmap(1, 1);
        using var baseline = new BBitmap(1, 1);
        actual.SetPixel(0, 0, new BColor(255, 0, 0, 255));
        baseline.SetPixel(0, 0, new BColor(0, 255, 0, 255));

        using var result = PixelDiffRunner.Compare(actual, baseline);
        using var diffImage = result.DiffImage;

        Assert.NotNull(diffImage);
        var pixel = diffImage!.GetPixel(0, 0);
        Assert.Equal((byte)255, pixel.Red);
        Assert.Equal((byte)0, pixel.Green);
        Assert.Equal((byte)255, pixel.Blue);
        Assert.Equal((byte)255, pixel.Alpha);
    }
}
