using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
public class ImagePipelineTests
{
    [Fact]
    public void ImageDecoder_DetectFormat_Png() => Assert.Equal(ImageFormat.Png, ImageDecoder.DetectFormat("image.png"));

    [Fact]
    public void ImageDecoder_DetectFormat_Jpeg() => Assert.Equal(ImageFormat.Jpeg, ImageDecoder.DetectFormat("photo.jpg"));

    [Fact]
    public void ImageDecoder_DetectFormat_Svg() => Assert.Equal(ImageFormat.Svg, ImageDecoder.DetectFormat("icon.svg"));

    [Fact]
    public void ImageDecoder_DetectFormat_DataUri() => Assert.Equal(ImageFormat.Png, ImageDecoder.DetectFormat("data:image/png;base64,abc"));

    [Fact]
    public void ImageDecoder_DetectFormatFromBytes_Png()
    {
        var data = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0 };
        Assert.Equal(ImageFormat.Png, ImageDecoder.DetectFormatFromBytes(data));
    }

    [Fact]
    public void ImageDecoder_DetectFormatFromBytes_Jpeg()
    {
        var data = new byte[] { 0xFF, 0xD8, 0xFF, 0, 0, 0, 0, 0 };
        Assert.Equal(ImageFormat.Jpeg, ImageDecoder.DetectFormatFromBytes(data));
    }

    [Fact]
    public void ImageDecoder_CreatePlaceholder_CorrectDimensions()
    {
        var img = ImageDecoder.CreatePlaceholder(100, 50, ImageFormat.Png);
        Assert.Equal(100, img.Width);
        Assert.Equal(50, img.Height);
        Assert.Equal(ImageFormat.Png, img.Format);
        Assert.Equal(100 * 50 * 4, img.PixelData.Length);
    }

    [Fact]
    public void DecodedImage_StoresProperties()
    {
        var data = new byte[4];
        var img = new DecodedImage(10, 20, ImageFormat.Gif, data, "test.gif");
        Assert.Equal(10, img.Width);
        Assert.Equal(20, img.Height);
        Assert.Equal(ImageFormat.Gif, img.Format);
        Assert.Equal("test.gif", img.Source);
    }

    [Fact]
    public void SvgParser_Parse_SimpleRect()
    {
        var svg = "<svg><rect x=\"10\" y=\"20\" width=\"100\" height=\"50\"/></svg>";
        var result = SvgParser.Parse(svg);
        Assert.Equal("svg", result.TagName);
        Assert.True(result.Children.Count > 0);
    }

    [Fact]
    public void SvgRenderer_Render_Rect_ProducesCommand()
    {
        var rect = new SvgElement { TagName = "rect" };
        rect.Attributes["x"] = "10";
        rect.Attributes["y"] = "20";
        rect.Attributes["width"] = "100";
        rect.Attributes["height"] = "50";
        var root = new SvgElement { TagName = "svg" };
        root.Children.Add(rect);

        var commands = SvgRenderer.Render(root);
        Assert.True(commands.Count > 0);
        Assert.Contains(commands, c => c.Type == SvgDrawCommandType.Rectangle);
    }

    [Fact]
    public void CanvasRenderingContext2D_FillRect_DoesNotThrow()
    {
        var ctx = new CanvasRenderingContext2D(300, 150);
        ctx.FillRect(10, 20, 100, 50);
    }

    [Fact]
    public void CanvasRenderingContext2D_DefaultProperties()
    {
        var ctx = new CanvasRenderingContext2D(300, 150);
        Assert.Equal(300, ctx.Width);
        Assert.Equal(150, ctx.Height);
        Assert.Equal("#000000", ctx.FillStyle);
        Assert.Equal("#000000", ctx.StrokeStyle);
        Assert.Equal(1.0f, ctx.LineWidth);
        Assert.Equal(1.0f, ctx.GlobalAlpha);
    }

    [Fact]
    public void CanvasRenderingContext2D_PathOperations_DoNotThrow()
    {
        var ctx = new CanvasRenderingContext2D(300, 150);
        ctx.BeginPath();
        ctx.MoveTo(10, 10);
        ctx.LineTo(100, 100);
        ctx.ClosePath();
        ctx.Stroke();
    }

    [Fact]
    public void CanvasRenderingContext2D_SaveRestore_RestoresState()
    {
        var ctx = new CanvasRenderingContext2D(300, 150);
        ctx.FillStyle = "red";
        ctx.Save();
        ctx.FillStyle = "blue";
        Assert.Equal("blue", ctx.FillStyle);
        ctx.Restore();
        Assert.Equal("red", ctx.FillStyle);
    }
}
