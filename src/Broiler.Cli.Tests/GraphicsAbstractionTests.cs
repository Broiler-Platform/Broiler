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
}
