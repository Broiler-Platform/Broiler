using Broiler.Cli;
using Broiler.HTML.Image;
using Broiler.HTML.Image.Adapters;
using System.Drawing;
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
    public void HtmlContainer_PerformPaint_With_BBitmap_Translation_Helper_Renders_Offset_Content()
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

    [Fact]
    public void BBitmap_ResizeNearest_Scales_Source_Pixels_Without_Skia_Surface_Access()
    {
        using var source = new BBitmap(2, 1);
        source.SetPixel(0, 0, new BColor(255, 0, 0, 255));
        source.SetPixel(1, 0, new BColor(0, 0, 255, 255));

        using var resized = source.ResizeNearest(4, 1);

        Assert.Equal((byte)255, resized.GetPixel(0, 0).R);
        Assert.Equal((byte)255, resized.GetPixel(1, 0).R);
        Assert.Equal((byte)255, resized.GetPixel(2, 0).B);
        Assert.Equal((byte)255, resized.GetPixel(3, 0).B);
    }

    [Fact]
    public void BCanvas_FillRect_Respects_Translation_And_Clip()
    {
        using var bitmap = new BBitmap(6, 6);
        using var canvas = bitmap.OpenRasterCanvas();
        canvas.Clear(BColor.White);
        canvas.PushClip(new RectangleF(2, 2, 3, 3));
        canvas.Translate(1, 1);

        canvas.FillRect(new RectangleF(1, 1, 3, 3), new BColor(255, 0, 0, 255));

        Assert.Equal(new BColor(255, 0, 0, 255), bitmap.GetPixel(2, 2));
        Assert.Equal(new BColor(255, 0, 0, 255), bitmap.GetPixel(4, 4));
        Assert.Equal(BColor.White, bitmap.GetPixel(1, 1));
        Assert.Equal(BColor.White, bitmap.GetPixel(5, 5));
    }

    [Fact]
    public void BCanvas_PushClipExclude_Skips_Excluded_Pixels()
    {
        using var bitmap = new BBitmap(5, 5);
        using var canvas = bitmap.OpenRasterCanvas();
        canvas.Clear(BColor.White);
        canvas.PushClip(new RectangleF(0, 0, 5, 5));
        canvas.PushClipExclude(new RectangleF(2, 2, 2, 2));

        canvas.FillRect(new RectangleF(0, 0, 5, 5), new BColor(255, 0, 0, 255));

        Assert.Equal(new BColor(255, 0, 0, 255), bitmap.GetPixel(1, 1));
        Assert.Equal(BColor.White, bitmap.GetPixel(2, 2));
        Assert.Equal(BColor.White, bitmap.GetPixel(3, 3));
    }

    [Fact]
    public void BCanvas_DrawLine_Renders_Stroke_Without_Skia_Canvas()
    {
        using var bitmap = new BBitmap(6, 6);
        using var canvas = bitmap.OpenRasterCanvas();
        canvas.Clear(BColor.White);

        canvas.DrawLine(new System.Drawing.PointF(1, 3.5f), new System.Drawing.PointF(4, 3.5f), new BColor(0, 0, 0, 255));

        Assert.Equal(new BColor(0, 0, 0, 255), bitmap.GetPixel(1, 3));
        Assert.Equal(new BColor(0, 0, 0, 255), bitmap.GetPixel(4, 3));
        Assert.Equal(BColor.White, bitmap.GetPixel(0, 1));
    }

    [Fact]
    public void BCanvas_SaveOpacityLayer_Composites_With_Opacity()
    {
        using var bitmap = new BBitmap(1, 1);
        using var canvas = bitmap.OpenRasterCanvas();
        canvas.Clear(BColor.White);
        canvas.SaveOpacityLayer(0.5f);

        canvas.FillRect(new RectangleF(0, 0, 1, 1), new BColor(255, 0, 0, 255));
        canvas.RestoreOpacityLayer();

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal((byte)255, pixel.R);
        Assert.InRange(pixel.G, (byte)127, (byte)128);
        Assert.InRange(pixel.B, (byte)127, (byte)128);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void BCanvas_SaveBlendLayer_Multiply_Composites_Into_Base_Bitmap()
    {
        using var bitmap = new BBitmap(1, 1);
        using var canvas = bitmap.OpenRasterCanvas();
        canvas.Clear(new BColor(128, 128, 128, 255));
        canvas.SaveBlendLayer("multiply");

        canvas.FillRect(new RectangleF(0, 0, 1, 1), new BColor(255, 0, 0, 255));
        canvas.RestoreBlendLayer();

        var pixel = bitmap.GetPixel(0, 0);
        Assert.InRange(pixel.R, (byte)127, (byte)128);
        Assert.Equal((byte)0, pixel.G);
        Assert.Equal((byte)0, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void BBitmap_OpenGraphics_Routes_Hinted_Opacity_Layer_Through_Backend_Neutral_Primitives()
    {
        using var bitmap = new BBitmap(4, 4);
        bitmap.Clear(BColor.White);
        using var graphics = bitmap.OpenGraphics(new RectangleF(0, 0, 4, 4));
        using var brush = graphics.GetSolidBrush(Color.FromArgb(255, 255, 0, 0));

        graphics.HintNextLayerCanUseRaster(true);
        graphics.SaveOpacityLayer(0.5f);
        graphics.DrawRectangle(brush, 0, 0, 4, 4);
        graphics.RestoreOpacityLayer();

        var pixel = bitmap.GetPixel(1, 1);
        Assert.Equal((byte)255, pixel.R);
        Assert.InRange(pixel.G, (byte)127, (byte)128);
        Assert.InRange(pixel.B, (byte)127, (byte)128);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void BBitmap_OpenGraphics_Routes_Solid_Fills_And_Strokes_Through_Backend_Neutral_Primitives()
    {
        using var bitmap = new BBitmap(6, 6);
        bitmap.Clear(BColor.White);
        using var graphics = bitmap.OpenGraphics(new RectangleF(0, 0, 6, 6));
        using var brush = graphics.GetSolidBrush(Color.FromArgb(255, 255, 0, 0));
        var pen = graphics.GetPen(Color.FromArgb(255, 0, 0, 255));
        pen.Width = 1;

        graphics.DrawRectangle(brush, 1, 1, 3, 3);
        graphics.DrawLine(pen, 0, 5, 5, 5);

        Assert.Equal(new BColor(255, 0, 0, 255), bitmap.GetPixel(2, 2));
        Assert.Equal(new BColor(0, 0, 255, 255), bitmap.GetPixel(3, 5));
        Assert.Equal(BColor.White, bitmap.GetPixel(0, 0));
    }

    [Fact]
    public void HtmlContainer_PerformPaint_With_BBitmap_Surface_Renders_Solid_Border_And_Fill()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(8, 8);
        container.SetHtml("""
            <html><body style='margin:0'>
            <div style='width:4px;height:4px;border:2px solid #ff0000;background:#0000ff'></div>
            </body></html>
            """);

        using var bitmap = new BBitmap(8, 8);
        bitmap.Clear(BColor.White);
        var clip = new RectangleF(0, 0, 8, 8);

        container.PerformLayout(bitmap, clip);
        container.PerformPaint(bitmap, clip);

        Assert.Equal(new BColor(255, 0, 0, 255), bitmap.GetPixel(1, 1));
        Assert.Equal(new BColor(0, 0, 255, 255), bitmap.GetPixel(3, 3));
        Assert.Equal(new BColor(255, 0, 0, 255), bitmap.GetPixel(7, 7));
    }

    [Fact]
    public void BBitmap_OpenGraphics_Routes_Image_Blit_Through_Backend_Neutral_Primitives()
    {
        using var source = new BBitmap(2, 1);
        source.SetPixel(0, 0, new BColor(255, 0, 0, 255));
        source.SetPixel(1, 0, new BColor(0, 0, 255, 255));

        using var dest = new BBitmap(4, 2);
        dest.Clear(BColor.White);
        using var graphics = dest.OpenGraphics(new RectangleF(0, 0, 4, 2));
        using var image = new ImageAdapter(source.Copy());

        graphics.DrawImage(image, new RectangleF(0, 0, 4, 2));

        Assert.Equal(new BColor(255, 0, 0, 255), dest.GetPixel(0, 0));
        Assert.Equal(new BColor(255, 0, 0, 255), dest.GetPixel(1, 1));
        Assert.Equal(new BColor(0, 0, 255, 255), dest.GetPixel(2, 0));
        Assert.Equal(new BColor(0, 0, 255, 255), dest.GetPixel(3, 1));
    }

    [Fact]
    public void HtmlContainer_PerformPaint_With_BBitmap_Surface_Renders_Background_Image()
    {
        using var source = new BBitmap(2, 1);
        source.SetPixel(0, 0, new BColor(255, 0, 0, 255));
        source.SetPixel(1, 0, new BColor(0, 0, 255, 255));
        string pngBase64 = Convert.ToBase64String(source.Encode(BImageFormat.Png, 100));

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(4, 2);
        container.SetHtml($"""
            <html><body style='margin:0'>
            <div style="width:4px;height:2px;background-image:url(data:image/png;base64,{pngBase64});background-repeat:no-repeat;background-size:4px 2px"></div>
            </body></html>
            """);

        using var bitmap = new BBitmap(4, 2);
        bitmap.Clear(BColor.White);
        var clip = new RectangleF(0, 0, 4, 2);

        container.PerformLayout(bitmap, clip);
        container.PerformPaint(bitmap, clip);

        Assert.Equal(new BColor(255, 0, 0, 255), bitmap.GetPixel(0, 0));
        Assert.Equal(new BColor(255, 0, 0, 255), bitmap.GetPixel(1, 1));
        Assert.Equal(new BColor(0, 0, 255, 255), bitmap.GetPixel(2, 0));
        Assert.Equal(new BColor(0, 0, 255, 255), bitmap.GetPixel(3, 1));
    }

    [Fact]
    public void BBitmap_OpenGraphics_Routes_Texture_Brush_Rectangles_Through_Backend_Neutral_Primitives()
    {
        using var source = new BBitmap(2, 1);
        source.SetPixel(0, 0, new BColor(255, 0, 0, 255));
        source.SetPixel(1, 0, new BColor(0, 0, 255, 255));

        using var dest = new BBitmap(4, 2);
        dest.Clear(BColor.White);
        using var graphics = dest.OpenGraphics(new RectangleF(0, 0, 4, 2));
        using var image = new ImageAdapter(source.Copy());
        using var brush = graphics.GetTextureBrush(image, new RectangleF(0, 0, 2, 1), new PointF(0, 0));

        graphics.DrawRectangle(brush, 0, 0, 4, 2);

        Assert.Equal(new BColor(255, 0, 0, 255), dest.GetPixel(0, 0));
        Assert.Equal(new BColor(0, 0, 255, 255), dest.GetPixel(1, 0));
        Assert.Equal(new BColor(255, 0, 0, 255), dest.GetPixel(2, 1));
        Assert.Equal(new BColor(0, 0, 255, 255), dest.GetPixel(3, 1));
    }

    [Fact]
    public void HtmlContainer_PerformPaint_With_BBitmap_Surface_Renders_Repeating_Background_Image()
    {
        using var source = new BBitmap(2, 1);
        source.SetPixel(0, 0, new BColor(255, 0, 0, 255));
        source.SetPixel(1, 0, new BColor(0, 0, 255, 255));
        string pngBase64 = Convert.ToBase64String(source.Encode(BImageFormat.Png, 100));

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(4, 2);
        container.SetHtml($"""
            <html><body style='margin:0'>
            <div style="width:4px;height:2px;background-image:url(data:image/png;base64,{pngBase64});background-repeat:repeat"></div>
            </body></html>
            """);

        using var bitmap = new BBitmap(4, 2);
        bitmap.Clear(BColor.White);
        var clip = new RectangleF(0, 0, 4, 2);

        container.PerformLayout(bitmap, clip);
        container.PerformPaint(bitmap, clip);

        Assert.Equal(new BColor(255, 0, 0, 255), bitmap.GetPixel(0, 0));
        Assert.Equal(new BColor(0, 0, 255, 255), bitmap.GetPixel(1, 0));
        Assert.Equal(new BColor(255, 0, 0, 255), bitmap.GetPixel(2, 1));
        Assert.Equal(new BColor(0, 0, 255, 255), bitmap.GetPixel(3, 1));
    }

    [Fact]
    public void HtmlContainer_PerformPaint_With_BBitmap_Surface_Renders_Repeating_Gradient_Background()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(4, 2);
        container.SetHtml("""
            <html><body style='margin:0'>
            <div style='width:4px;height:2px;background-image:linear-gradient(#00ff00, #00ff00);background-size:2px 1px;background-repeat:repeat'></div>
            </body></html>
            """);

        using var bitmap = new BBitmap(4, 2);
        bitmap.Clear(BColor.White);
        var clip = new RectangleF(0, 0, 4, 2);

        container.PerformLayout(bitmap, clip);
        container.PerformPaint(bitmap, clip);

        Assert.Equal(new BColor(0, 255, 0, 255), bitmap.GetPixel(0, 0));
        Assert.Equal(new BColor(0, 255, 0, 255), bitmap.GetPixel(3, 1));
    }

    [Fact]
    public void BBitmap_OpenGraphics_Creates_Linear_Gradient_Tile_Through_Backend_Neutral_Primitives()
    {
        using var bitmap = new BBitmap(1, 1);
        using var graphics = bitmap.OpenGraphics(new RectangleF(0, 0, 1, 1));
        using var tile = graphics.CreateLinearGradientTile(4, 1, [Color.Red, Color.Blue], [0f, 1f], 90f);

        var image = Assert.IsType<ImageAdapter>(tile);
        var left = image.Bitmap.GetPixel(0, 0);
        var middle = image.Bitmap.GetPixel(1, 0);
        var right = image.Bitmap.GetPixel(3, 0);

        Assert.True(left.R > left.B);
        Assert.True(middle.R > 0 && middle.B > 0);
        Assert.True(right.B > right.R);
        Assert.Equal((byte)255, left.A);
        Assert.Equal((byte)255, right.A);
    }

    [Fact]
    public void HtmlContainer_PerformPaint_With_BBitmap_Surface_Renders_NonUniform_Gradient_Background()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(4, 4);
        container.SetHtml("""
            <html><body style='margin:0'>
            <div style='width:4px;height:4px;background-image:linear-gradient(#ff0000, #0000ff);background-repeat:no-repeat'></div>
            </body></html>
            """);

        using var bitmap = new BBitmap(4, 4);
        bitmap.Clear(BColor.White);
        var clip = new RectangleF(0, 0, 4, 4);

        container.PerformLayout(bitmap, clip);
        container.PerformPaint(bitmap, clip);

        var top = bitmap.GetPixel(2, 0);
        var middle = bitmap.GetPixel(2, 1);
        var bottom = bitmap.GetPixel(2, 3);

        Assert.True(top.R > top.B);
        Assert.True(middle.R > 0 && middle.B > 0);
        Assert.True(bottom.B > bottom.R);
        Assert.Equal((byte)255, top.A);
        Assert.Equal((byte)255, bottom.A);
    }

    [Fact]
    public void HtmlContainer_PerformPaint_With_BBitmap_Surface_Renders_Inline_Svg_Ellipse_As_Shape_Not_Rectangle()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(20, 20);
        container.SetHtml("""
            <html><body style='margin:0'>
            <svg xmlns='http://www.w3.org/2000/svg' width='20' height='20' style='display:block'>
              <ellipse cx='10' cy='10' rx='8' ry='5' fill='#0000ff'/>
            </svg>
            </body></html>
            """);

        using var bitmap = new BBitmap(20, 20);
        bitmap.Clear(BColor.White);
        var clip = new RectangleF(0, 0, 20, 20);

        container.PerformLayout(bitmap, clip);
        container.PerformPaint(bitmap, clip);

        Assert.Equal(new BColor(0, 0, 255, 255), bitmap.GetPixel(10, 10));
        Assert.Equal(BColor.White, bitmap.GetPixel(2, 5));
    }

    [Fact]
    public void BBitmap_OpenGraphics_Routes_Simple_Path_Strokes_Through_Backend_Neutral_Primitives()
    {
        using var bitmap = new BBitmap(7, 7);
        bitmap.Clear(BColor.White);
        using var graphics = bitmap.OpenGraphics(new RectangleF(0, 0, 7, 7));
        var pen = graphics.GetPen(Color.Black);
        pen.Width = 1;
        using var path = graphics.GetGraphicsPath();
        path.Start(1, 4);
        path.ArcTo(4, 1, 3, Broiler.HTML.Adapters.Adapters.RGraphicsPath.Corner.TopLeft);
        path.LineTo(5, 1);

        graphics.DrawPath(pen, path);

        Assert.Equal(new BColor(0, 0, 0, 255), bitmap.GetPixel(1, 3));
        Assert.Contains(
            new[]
            {
                bitmap.GetPixel(2, 1),
                bitmap.GetPixel(2, 2),
                bitmap.GetPixel(3, 1),
                bitmap.GetPixel(3, 2),
            },
            pixel => pixel == new BColor(0, 0, 0, 255));
        Assert.Equal(new BColor(0, 0, 0, 255), bitmap.GetPixel(4, 1));
    }

    [Fact]
    public void BBitmap_OpenGraphics_Routes_Solid_Path_Fills_Through_Backend_Neutral_Primitives()
    {
        using var bitmap = new BBitmap(7, 7);
        bitmap.Clear(BColor.White);
        using var graphics = bitmap.OpenGraphics(new RectangleF(0, 0, 7, 7));
        using var brush = graphics.GetSolidBrush(Color.Blue);
        using var path = graphics.GetGraphicsPath();
        path.Start(1, 5);
        path.LineTo(3, 1);
        path.LineTo(5, 5);

        graphics.DrawPath(brush, path);

        Assert.Equal(new BColor(0, 0, 255, 255), bitmap.GetPixel(3, 3));
        Assert.Equal(BColor.White, bitmap.GetPixel(0, 0));
        Assert.Equal(BColor.White, bitmap.GetPixel(6, 6));
    }

    [Fact]
    public void PixelDiffRunner_Compare_Matches_LayoutDominant_NonText_Fixture()
    {
        const string html = """
            <html><body style='margin:0;background:#ffffff'>
            <div style='width:12px;height:3px;background:#ff0000'></div>
            <div style='height:2px'></div>
            <div style='width:8px;height:4px;margin-left:2px;background:#0000ff'></div>
            </body></html>
            """;

        using var rendered = HtmlRender.RenderToImage(html, 12, 12, BColor.White);
        using var expected = new BBitmap(12, 12);
        expected.Clear(BColor.White);
        FillRect(expected, 0, 0, 12, 3, new BColor(255, 0, 0, 255));
        FillRect(expected, 2, 5, 8, 4, new BColor(0, 0, 255, 255));

        using var diff = PixelDiffRunner.Compare(rendered, expected);

        Assert.True(diff.IsMatch);
        Assert.Equal(0, diff.DiffPixelCount);
        Assert.Equal(0d, diff.DiffRatio);
        Assert.Null(diff.DiffBitmap);
    }

    [Fact]
    public void PixelDiffRunner_Compare_Matches_ShapeHeavy_NonText_Fixture()
    {
        const string html = """
            <html><body style='margin:0;background:#ffffff'>
            <div style='width:8px;height:8px;border:2px solid #ff0000;background:#0000ff'></div>
            </body></html>
            """;

        using var rendered = HtmlRender.RenderToImage(html, 12, 12, BColor.White);
        using var expected = new BBitmap(12, 12);
        expected.Clear(BColor.White);
        FillRect(expected, 0, 0, 12, 12, new BColor(255, 0, 0, 255));
        FillRect(expected, 2, 2, 8, 8, new BColor(0, 0, 255, 255));

        using var diff = PixelDiffRunner.Compare(rendered, expected);

        Assert.True(diff.IsMatch);
        Assert.Equal(0, diff.DiffPixelCount);
        Assert.Equal(0d, diff.DiffRatio);
        Assert.Null(diff.DiffBitmap);
    }

    [Fact]
    public void PixelDiffRunner_Compare_Matches_Clipped_NonText_Opacity_Fixture()
    {
        const string html = """
            <html><body style='margin:0;background:#ffffff'>
            <div style='width:4px;height:4px;margin-left:1px;margin-top:1px;overflow:hidden;opacity:0.5'>
              <div style='width:6px;height:6px;background:#ff0000'></div>
            </div>
            </body></html>
            """;

        using var rendered = HtmlRender.RenderToImage(html, 8, 8, BColor.White);
        using var expected = new BBitmap(8, 8);
        expected.Clear(BColor.White);
        FillRect(expected, 1, 1, 4, 4, new BColor(255, 128, 128, 255));

        using var diff = PixelDiffRunner.Compare(rendered, expected);

        Assert.True(diff.IsMatch);
        Assert.Equal(0, diff.DiffPixelCount);
        Assert.Equal(0d, diff.DiffRatio);
        Assert.Null(diff.DiffBitmap);
    }

    [Fact]
    public void PixelDiffRunner_Compare_Matches_NonText_Opacity_Fixture()
    {
        const string html = """
            <html><body style='margin:0;background:#ffffff'>
            <div style='width:4px;height:4px;margin-left:1px;margin-top:1px;background:#ff0000;opacity:0.5'></div>
            </body></html>
            """;

        using var rendered = HtmlRender.RenderToImage(html, 6, 6, BColor.White);
        using var expected = new BBitmap(6, 6);
        expected.Clear(BColor.White);
        FillRect(expected, 1, 1, 4, 4, new BColor(255, 128, 128, 255));

        using var diff = PixelDiffRunner.Compare(rendered, expected);

        Assert.True(diff.IsMatch);
        Assert.Equal(0, diff.DiffPixelCount);
        Assert.Equal(0d, diff.DiffRatio);
        Assert.Null(diff.DiffBitmap);
    }

    [Fact]
    public void PixelDiffRunner_Compare_Matches_NonText_Multiply_Blend_Fixture()
    {
        const string html = """
            <html><body style='margin:0;background:#808080'>
            <div style='width:4px;height:4px;margin-left:1px;margin-top:1px;background:#ff0000;mix-blend-mode:multiply'></div>
            </body></html>
            """;

        using var rendered = HtmlRender.RenderToImage(html, 6, 6, BColor.White);
        using var expected = new BBitmap(6, 6);
        expected.Clear(new BColor(128, 128, 128, 255));
        FillRect(expected, 1, 1, 4, 4, new BColor(128, 0, 0, 255));

        using var diff = PixelDiffRunner.Compare(rendered, expected);

        Assert.True(diff.IsMatch);
        Assert.Equal(0, diff.DiffPixelCount);
        Assert.Equal(0d, diff.DiffRatio);
        Assert.Null(diff.DiffBitmap);
    }

    [Fact]
    public void HtmlContainer_PerformPaint_With_BBitmap_Surface_Renders_Rounded_Border_Path()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(10, 10);
        container.SetHtml("""
            <html><body style='margin:0'>
            <div style='width:6px;height:6px;border:2px solid #ff0000;border-radius:4px;background:#0000ff'></div>
            </body></html>
            """);

        using var bitmap = new BBitmap(10, 10);
        bitmap.Clear(BColor.White);
        var clip = new RectangleF(0, 0, 10, 10);

        container.PerformLayout(bitmap, clip);
        container.PerformPaint(bitmap, clip);

        Assert.Equal(BColor.White, bitmap.GetPixel(0, 0));
        Assert.Equal(new BColor(255, 0, 0, 255), bitmap.GetPixel(4, 1));
        Assert.Equal(new BColor(0, 0, 255, 255), bitmap.GetPixel(5, 5));
    }

    private static void FillRect(BBitmap bitmap, int x, int y, int width, int height, BColor color)
    {
        using var canvas = bitmap.OpenRasterCanvas();
        canvas.FillRect(new RectangleF(x, y, width, height), color);
    }

}
