using Broiler.Cli;
using Broiler.HTML.Image;
using Broiler.HTML.Image.Adapters;
using SkiaSharp;
using System.Drawing;
using System.Drawing.Drawing2D;
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

    [Theory]
    [InlineData("#123456", 0x12, 0x34, 0x56, 0xFF)]
    [InlineData("#12345678", 0x12, 0x34, 0x56, 0x78)]
    [InlineData("#abc", 0xAA, 0xBB, 0xCC, 0xFF)]
    [InlineData("#abcd", 0xAA, 0xBB, 0xCC, 0xDD)]
    public void SkiaImageAdapter_GetColor_Parses_Hex_Colors_Without_Skia_Color_Parser(
        string color,
        int expectedRed,
        int expectedGreen,
        int expectedBlue,
        int expectedAlpha)
    {
        var parsed = SkiaImageAdapter.Instance.GetColor(color);

        Assert.Equal(expectedRed, parsed.R);
        Assert.Equal(expectedGreen, parsed.G);
        Assert.Equal(expectedBlue, parsed.B);
        Assert.Equal(expectedAlpha, parsed.A);
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
    public void BBitmap_Copy_And_EncodeDecode_Roundtrip_Preserve_Pixels()
    {
        using var source = new BBitmap(2, 2);
        source.SetPixel(0, 0, new BColor(255, 0, 0, 255));
        source.SetPixel(1, 0, new BColor(0, 255, 0, 255));
        source.SetPixel(0, 1, new BColor(0, 0, 255, 255));
        source.SetPixel(1, 1, new BColor(255, 255, 0, 128));

        using var copy = source.Copy();
        using var roundTripped = BBitmap.Decode(copy.Encode(BImageFormat.Png));

        Assert.Equal(source.GetPixel(0, 0), roundTripped.GetPixel(0, 0));
        Assert.Equal(source.GetPixel(1, 0), roundTripped.GetPixel(1, 0));
        Assert.Equal(source.GetPixel(0, 1), roundTripped.GetPixel(0, 1));
        Assert.Equal(source.GetPixel(1, 1), roundTripped.GetPixel(1, 1));
    }

    [Fact]
    public void BBitmap_EncodeDecode_Jpeg_Roundtrip_Preserves_Size_Without_Skia_Codec()
    {
        using var source = new BBitmap(2, 2);
        source.Clear(new BColor(220, 30, 40, 255));

        using var roundTripped = BBitmap.Decode(source.Encode(BImageFormat.Jpeg, 100));

        Assert.Equal(source.Width, roundTripped.Width);
        Assert.Equal(source.Height, roundTripped.Height);

        var pixel = roundTripped.GetPixel(0, 0);
        Assert.Equal((byte)255, pixel.A);
        Assert.InRange(pixel.R, (byte)150, byte.MaxValue);
        Assert.InRange(pixel.G, byte.MinValue, (byte)120);
        Assert.InRange(pixel.B, byte.MinValue, (byte)120);
    }

    [Fact]
    public void Raster_Stream_Image_Load_Uses_BackendNeutral_Bitmap_Without_Materializing_Skia_Compat_Bitmap()
    {
        using var source = new BBitmap(2, 2);
        source.SetPixel(0, 0, new BColor(255, 0, 0, 255));
        source.SetPixel(1, 0, new BColor(0, 255, 0, 255));
        source.SetPixel(0, 1, new BColor(0, 0, 255, 255));
        source.SetPixel(1, 1, new BColor(255, 255, 0, 255));

        using var loaded = Assert.IsType<ImageAdapter>(
            SkiaImageAdapter.Instance.ImageFromStream(new MemoryStream(source.Encode(BImageFormat.Png))));
        using var target = new BBitmap(4, 4);
        using var graphics = Assert.IsType<GraphicsAdapter>(target.OpenGraphics(new RectangleF(0, 0, 4, 4)));

        Assert.False(loaded.Bitmap.HasMaterializedCompatBitmap);
        Assert.False(target.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);

        graphics.DrawImage(loaded, new RectangleF(0, 0, 4, 4));

        Assert.False(loaded.Bitmap.HasMaterializedCompatBitmap);
        Assert.False(target.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);
        Assert.Equal(new BColor(255, 0, 0, 255), target.GetPixel(0, 0));
        Assert.Equal(new BColor(255, 0, 0, 255), target.GetPixel(1, 1));
        Assert.Equal(new BColor(255, 255, 0, 255), target.GetPixel(3, 3));
    }

    [Fact]
    public void Invalid_Raster_Stream_Image_Load_Returns_Null()
    {
        using var stream = new MemoryStream([0x01, 0x02, 0x03, 0x04]);

        Assert.Null(SkiaImageAdapter.Instance.ImageFromStream(stream));
    }

    [Fact]
    public void BBitmap_OpenGraphics_Syncs_SkiaOverride_Drawing_Back_Into_Primary_Pixel_Buffer()
    {
        using var _ = BGraphicsBackend.OverrideForCurrentThread(BGraphicsBackend.SkiaFallbackId);
        using var bitmap = new BBitmap(4, 4);
        using (var graphics = bitmap.OpenGraphics(new RectangleF(0, 0, 4, 4)))
        {
            using var brush = graphics.GetSolidBrush(Color.FromArgb(255, 255, 0, 0));
            graphics.DrawRectangle(brush, 0, 0, 4, 4);
        }

        Assert.Equal(new BColor(255, 0, 0, 255), bitmap.GetPixel(1, 1));
        Assert.Equal(new BColor(255, 0, 0, 255), bitmap.GetPixel(3, 3));
    }

    [Fact]
    public void RasterCapable_Solid_Brush_And_Pen_Drawing_Do_Not_Materialize_Skia_Paint()
    {
        using var bitmap = new BBitmap(6, 6);
        var graphics = Assert.IsType<GraphicsAdapter>(bitmap.OpenGraphics(new RectangleF(0, 0, 6, 6)));
        using var brush = Assert.IsType<BrushAdapter>(graphics.GetSolidBrush(Color.FromArgb(255, 123, 45, 67)));
        var pen = Assert.IsType<PenAdapter>(graphics.GetPen(Color.FromArgb(255, 17, 33, 197)));

        Assert.False(bitmap.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);
        Assert.False(brush.HasMaterializedPaint);
        Assert.False(pen.HasMaterializedPaint);

        graphics.DrawRectangle(brush, 0, 0, 6, 6);
        graphics.DrawLine(pen, 0, 0, 5, 5);

        Assert.False(bitmap.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);
        Assert.False(brush.HasMaterializedPaint);
        Assert.False(pen.HasMaterializedPaint);

        graphics.Dispose();

        Assert.Equal(0, bitmap.CompatSyncInvocationCount);
        Assert.False(bitmap.HasMaterializedCompatBitmap);
    }

    [Fact]
    public void RasterCapable_Texture_Brush_Drawing_Does_Not_Materialize_Skia_Paint()
    {
        using var bitmap = new BBitmap(4, 4);
        using var texture = new BBitmap(1, 1);
        texture.SetPixel(0, 0, new BColor(0, 255, 0, 255));

        using var graphics = Assert.IsType<GraphicsAdapter>(bitmap.OpenGraphics(new RectangleF(0, 0, 4, 4)));
        using var image = new ImageAdapter(texture.Copy());
        using var brush = Assert.IsType<BrushAdapter>(graphics.GetTextureBrush(
            image,
            new RectangleF(0, 0, 1, 1),
            new PointF(0, 0)));

        Assert.False(bitmap.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);
        Assert.False(brush.HasMaterializedPaint);

        graphics.DrawRectangle(brush, 0, 0, 4, 4);

        Assert.False(bitmap.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);
        Assert.False(brush.HasMaterializedPaint);
        Assert.Equal(new BColor(0, 255, 0, 255), bitmap.GetPixel(2, 2));
    }

    [Fact]
    public void NonRaster_Fallback_Drawing_Materializes_Skia_Paint_On_Demand()
    {
        using var bitmap = new BBitmap(6, 6);
        var graphics = Assert.IsType<GraphicsAdapter>(bitmap.OpenGraphics(new RectangleF(0, 0, 6, 6)));
        var pen = Assert.IsType<PenAdapter>(graphics.GetPen(Color.FromArgb(255, 211, 19, 173)));
        pen.DashStyle = DashStyle.Dash;

        Assert.False(bitmap.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);
        Assert.False(pen.HasMaterializedPaint);

        graphics.DrawLine(pen, 0, 3, 5, 3);

        Assert.True(bitmap.HasMaterializedCompatBitmap);
        Assert.True(graphics.HasMaterializedCanvas);
        Assert.True(pen.HasMaterializedPaint);

        graphics.Dispose();

        Assert.Equal(1, bitmap.CompatSyncInvocationCount);
    }

    [Fact]
    public void Linear_Gradient_Brush_Creation_Defers_Skia_Paint_Until_Draw()
    {
        using var bitmap = new BBitmap(6, 6);
        using var graphics = Assert.IsType<GraphicsAdapter>(bitmap.OpenGraphics(new RectangleF(0, 0, 6, 6)));
        using var brush = Assert.IsType<BrushAdapter>(graphics.GetLinearGradientBrush(
            new RectangleF(0, 0, 6, 6),
            Color.Red,
            Color.Blue,
            90));

        Assert.False(bitmap.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);
        Assert.False(brush.HasMaterializedPaint);
    }

    [Fact]
    public void Linear_Gradient_Brush_Fallback_Drawing_Materializes_Skia_Paint_On_Demand()
    {
        using var bitmap = new BBitmap(6, 6);
        using var graphics = Assert.IsType<GraphicsAdapter>(bitmap.OpenGraphics(new RectangleF(0, 0, 6, 6)));
        using var brush = Assert.IsType<BrushAdapter>(graphics.GetLinearGradientBrush(
            new RectangleF(0, 0, 6, 6),
            Color.Red,
            Color.Blue,
            90));

        Assert.False(bitmap.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);
        Assert.False(brush.HasMaterializedPaint);

        graphics.DrawRectangle(brush, 0, 0, 6, 6);

        Assert.True(bitmap.HasMaterializedCompatBitmap);
        Assert.True(graphics.HasMaterializedCanvas);
        Assert.True(brush.HasMaterializedPaint);
    }

    [Fact]
    public void FontAdapter_Defers_Skia_Font_Creation_Until_Text_Uses_It()
    {
        var font = new FontAdapter(SKTypeface.Default, 12, FontStyle.Regular);
        using var bitmap = new BBitmap(32, 32);
        using var graphics = bitmap.OpenGraphics(new RectangleF(0, 0, 32, 32));

        Assert.False(font.HasMaterializedLayoutFont);
        Assert.False(font.HasMaterializedRenderFont);

        var size = graphics.MeasureString("abc", font);

        Assert.True(size.Width > 0);
        Assert.True(font.HasMaterializedLayoutFont);
        Assert.False(font.HasMaterializedRenderFont);

        graphics.DrawString("abc", font, Color.Black, new PointF(0, 0), size, rtl: false);

        Assert.True(font.HasMaterializedRenderFont);
    }

    [Fact]
    public void Broiler_Text_Draw_And_Measurement_Do_Not_Materialize_Skia_For_Loaded_Fonts()
    {
        var alias = $"RasterText_{Guid.NewGuid():N}";
        var fontPath = Path.Combine(GetRepoRoot(), "tests", "wpt", "fonts", "Ahem.ttf");
        var family = SkiaImageAdapter.Instance.LoadFontFromFile(fontPath, alias);
        Assert.Equal(alias, family);

        var font = Assert.IsType<FontAdapter>(SkiaImageAdapter.Instance.GetFont(alias, 12, FontStyle.Regular));
        using var bitmap = new BBitmap(48, 24);
        bitmap.Clear(BColor.White);
        using var graphics = Assert.IsType<GraphicsAdapter>(bitmap.OpenGraphics(new RectangleF(0, 0, 48, 24)));

        var size = graphics.MeasureString("XX", font);

        Assert.False(bitmap.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);
        Assert.False(SkiaImageAdapter.Instance.HasMaterializedLoadedTypeface(alias));

        graphics.DrawString("XX", font, Color.Black, new PointF(4, 4), size, rtl: false);

        Assert.False(bitmap.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);
        Assert.False(SkiaImageAdapter.Instance.HasMaterializedLoadedTypeface(alias));
        Assert.Equal(new BColor(0, 0, 0, 255), bitmap.GetPixel(4, 4));
    }

    [Fact]
    public void Broiler_Render_Font_Reuses_The_Registered_Font_Size_For_Loaded_Fonts()
    {
        var alias = $"RasterRenderSize_{Guid.NewGuid():N}";
        var fontPath = Path.Combine(GetRepoRoot(), "tests", "wpt", "fonts", "Ahem.ttf");
        var family = SkiaImageAdapter.Instance.LoadFontFromFile(fontPath, alias);
        Assert.Equal(alias, family);

        var font = Assert.IsType<FontAdapter>(SkiaImageAdapter.Instance.GetFont(alias, 12, FontStyle.Regular));

        Assert.True(font.TryGetBroilerLayoutFont(out var layoutFont));
        Assert.True(font.TryGetBroilerRenderFont(out var renderFont));
        Assert.Equal(12f, layoutFont.Size);
        Assert.Equal(12f, renderFont.Size);
        Assert.False(SkiaImageAdapter.Instance.HasMaterializedLoadedTypeface(alias));
    }

    [Fact]
    public void GraphicsAdapter_Text_Operations_Delegate_Through_Text_Shaper_Seam()
    {
        using var surface = SKSurface.Create(new SKImageInfo(32, 32));
        var textShaper = new RecordingTextShaper();
        using var graphics = new GraphicsAdapter(
            () => surface.Canvas,
            new RectangleF(0, 0, 32, 32),
            textShaper: textShaper);
        var font = new FontAdapter(SKTypeface.Default, 12, FontStyle.Regular);

        var measureSize = graphics.MeasureString("measure", font);
        graphics.MeasureString("limit", font, 12, out var charFit, out var charFitWidth);
        graphics.DrawString("draw", font, Color.Black, new PointF(1, 2), new SizeF(3, 4), rtl: false);
        graphics.DrawGradientString(
            "gradient",
            font,
            new RectangleF(0, 0, 8, 9),
            new PointF(5, 6),
            new SizeF(7, 8),
            rtl: false,
            colors: [Color.Red, Color.Blue],
            positions: [0f, 1f],
            angle: 45f);

        Assert.Equal(new SizeF(7, 11), measureSize);
        Assert.Equal(3, charFit);
        Assert.Equal(9, charFitWidth);
        Assert.Equal(
            ["MeasureString", "MeasureStringMaxWidth", "DrawString", "DrawGradientString"],
            textShaper.Calls);
        Assert.False(font.HasMaterializedLayoutFont);
        Assert.False(font.HasMaterializedRenderFont);
    }

    [Fact]
    public void GraphicsAdapter_NonText_Fallback_Operations_Delegate_Through_Canvas_Compat_Seam()
    {
        using var surface = SKSurface.Create(new SKImageInfo(32, 32));
        var canvasCompat = new RecordingCanvasCompat();
        using var graphics = new GraphicsAdapter(
            () => surface.Canvas,
            new RectangleF(0, 0, 32, 32),
            canvasCompat: canvasCompat);
        using var textureBitmap = new BBitmap(2, 1);
        using var image = new ImageAdapter(textureBitmap.Copy());
        using var textureBrush = Assert.IsType<BrushAdapter>(graphics.GetTextureBrush(
            image,
            new RectangleF(0, 0, 2, 1),
            new PointF(3, 4)));
        using var solidBrush = graphics.GetSolidBrush(Color.Red);
        var pen = Assert.IsType<PenAdapter>(graphics.GetPen(Color.Blue));
        using var path = Assert.IsType<GraphicsPathAdapter>(graphics.GetGraphicsPath());

        Assert.False(textureBrush.HasMaterializedPaint);

        _ = textureBrush.Paint;
        graphics.DrawLine(pen, 1, 2, 3, 4);
        graphics.DrawRectangle(pen, 1, 2, 3, 4);
        graphics.DrawRectangle(solidBrush, 5, 6, 7, 8);
        path.Start(1, 1);
        path.LineTo(4, 1);
        path.LineTo(4, 4);
        graphics.DrawPath(pen, path);
        graphics.DrawPath(solidBrush, path);
        graphics.PushClipRounded(new RectangleF(1, 2, 10, 11), 1, 2, 3, 4, 5, 6, 7, 8);
        graphics.DrawPolygon(solidBrush, [new PointF(1, 1), new PointF(5, 1), new PointF(3, 4)]);
        graphics.SaveOpacityLayer(0.5f);
        graphics.SaveBlendLayer("screen");

        Assert.True(textureBrush.HasMaterializedPaint);
        Assert.Equal(
            ["CreateTexturePaint", "DrawLine", "DrawRectangle", "DrawRectangle", "DrawPath", "DrawPath", "ClipRounded", "DrawPolygon", "SaveOpacityLayer", "SaveBlendLayer"],
            canvasCompat.Calls);
    }

    [Fact]
    public void BBitmap_Compatibility_Surface_Operations_Delegate_Through_Bitmap_Compat_Seam()
    {
        using var compatSurface = new RecordingBitmapCompatSurface(4, 4);
        using var bitmap = new BBitmap(4, 4, new byte[4 * 4 * 4], compatSurface);

        bitmap.SetPixel(1, 1, new BColor(10, 20, 30, 255));
        bitmap.Clear(new BColor(40, 50, 60, 255));

        using var canvas = bitmap.OpenCanvas();
        using var bitmapCopy = bitmap.ToSkBitmapCopy();
        using var compatBitmap = bitmap.AsSkBitmap();
        using var recorder = new SKPictureRecorder();
        recorder.BeginRecording(new SKRect(0, 0, 4, 4)).DrawRect(new SKRect(0, 0, 4, 4), new SKPaint { Color = SKColors.Red });
        using var picture = recorder.EndRecording();

        bitmap.DrawPictureToFit(picture);

        Assert.Equal(1, bitmap.CompatSyncInvocationCount);
        Assert.Equal(
            ["SetPixel", "Clear", "OpenCanvas", "ToBitmapCopy", "AsBitmap", "OpenCanvas", "SyncToPrimaryBuffer"],
            compatSurface.Calls);
    }

    [Fact]
    public void Aliased_Font_File_Load_Defers_Skia_Typeface_Creation_Until_Font_Request()
    {
        var alias = $"LazyProbeSans_{Guid.NewGuid():N}";
        var fontPath = Path.Combine(GetRepoRoot(), "acid", "fonts", "DejaVuSans.ttf");

        var family = SkiaImageAdapter.Instance.LoadFontFromFile(fontPath, alias);

        Assert.Equal(alias, family);
        Assert.True(SkiaImageAdapter.Instance.HasDeferredLoadedTypefacePath(alias));
        Assert.False(SkiaImageAdapter.Instance.HasMaterializedLoadedTypeface(alias));

        var font = Assert.IsType<FontAdapter>(SkiaImageAdapter.Instance.GetFont(alias, 12, FontStyle.Regular));

        Assert.False(SkiaImageAdapter.Instance.HasMaterializedLoadedTypeface(alias));
        Assert.False(font.HasMaterializedLayoutFont);
        Assert.False(font.HasMaterializedRenderFont);

        using var bitmap = new BBitmap(32, 32);
        using var graphics = bitmap.OpenGraphics(new RectangleF(0, 0, 32, 32));
        var size = graphics.MeasureString("abc", font);

        Assert.True(size.Width > 0);
        Assert.True(SkiaImageAdapter.Instance.HasMaterializedLoadedTypeface(alias));
        Assert.True(font.HasMaterializedLayoutFont);
    }

    [Fact]
    public void SkiaImageAdapter_Font_Operations_Delegate_Through_Typeface_Resolver_Seam()
    {
        var resolver = new RecordingFontTypefaceResolver();
        var adapter = new SkiaImageAdapter(resolver, ["SystemUi"]);

        Assert.True(adapter.IsFontExists("SystemUi"));

        var family = adapter.LoadFontFromFile("/tmp/fonts/SeamSans.ttf", "SeamSans");

        Assert.Equal("SeamSans", family);
        Assert.True(adapter.HasDeferredLoadedTypefacePath("SeamSans"));
        Assert.False(adapter.HasMaterializedLoadedTypeface("SeamSans"));

        var font = Assert.IsType<FontAdapter>(adapter.GetFont("SeamSans", 12, FontStyle.Bold | FontStyle.Italic));

        Assert.False(adapter.HasMaterializedLoadedTypeface("SeamSans"));
        Assert.False(font.HasMaterializedLayoutFont);
        Assert.False(font.HasMaterializedRenderFont);

        using var bitmap = new BBitmap(32, 32);
        using var graphics = bitmap.OpenGraphics(new RectangleF(0, 0, 32, 32));
        var size = graphics.MeasureString("abc", font);

        Assert.True(size.Width > 0);
        Assert.True(adapter.HasMaterializedLoadedTypeface("SeamSans"));
        Assert.Equal(
            [
                "RegisterFontFile",
                "HasDeferredLoadedTypefacePath",
                "HasMaterializedLoadedTypeface",
                "HasMaterializedLoadedTypeface",
                "ResolveTypeface",
                "HasMaterializedLoadedTypeface",
            ],
            resolver.Calls);
    }

    [Fact]
    public void RasterCapable_Path_Drawing_Does_Not_Materialize_Skia_Path()
    {
        using var bitmap = new BBitmap(7, 7);
        bitmap.Clear(BColor.White);
        using var graphics = Assert.IsType<GraphicsAdapter>(bitmap.OpenGraphics(new RectangleF(0, 0, 7, 7)));
        using var brush = Assert.IsType<BrushAdapter>(graphics.GetSolidBrush(Color.Blue));
        using var path = Assert.IsType<GraphicsPathAdapter>(graphics.GetGraphicsPath());
        path.Start(1, 5);
        path.LineTo(3, 1);
        path.LineTo(5, 5);

        Assert.False(path.HasMaterializedPath);
        Assert.False(bitmap.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);

        graphics.DrawPath(brush, path);

        Assert.False(path.HasMaterializedPath);
        Assert.False(bitmap.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);
    }

    [Fact]
    public void NonRaster_Fallback_Path_Drawing_Materializes_Skia_Path_On_Demand()
    {
        using var bitmap = new BBitmap(7, 7);
        using var graphics = Assert.IsType<GraphicsAdapter>(bitmap.OpenGraphics(new RectangleF(0, 0, 7, 7)));
        var pen = Assert.IsType<PenAdapter>(graphics.GetPen(Color.Black));
        using var path = Assert.IsType<GraphicsPathAdapter>(graphics.GetGraphicsPath());
        pen.DashStyle = DashStyle.Dash;
        path.Start(1, 1);
        path.LineTo(5, 5);

        Assert.False(path.HasMaterializedPath);
        Assert.False(bitmap.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);

        graphics.DrawPath(pen, path);

        Assert.True(path.HasMaterializedPath);
        Assert.True(bitmap.HasMaterializedCompatBitmap);
        Assert.True(graphics.HasMaterializedCanvas);
    }

    [Fact]
    public void RasterCapable_OpenGraphics_With_Translation_And_Clip_Does_Not_Materialize_Skia_Compatibility_Surface()
    {
        using var bitmap = new BBitmap(6, 6);
        bitmap.Clear(BColor.White);
        using var graphics = Assert.IsType<GraphicsAdapter>(bitmap.OpenGraphics(new RectangleF(0, 0, 6, 6), new PointF(1, 1)));
        using var brush = Assert.IsType<BrushAdapter>(graphics.GetSolidBrush(Color.FromArgb(255, 200, 10, 20)));

        graphics.PushClip(new RectangleF(1, 1, 3, 3));

        Assert.False(bitmap.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);

        graphics.DrawRectangle(brush, 0, 0, 5, 5);
        graphics.PopClip();

        Assert.False(bitmap.HasMaterializedCompatBitmap);
        Assert.False(graphics.HasMaterializedCanvas);
        Assert.Equal(new BColor(200, 10, 20, 255), bitmap.GetPixel(2, 2));
        Assert.Equal(BColor.White, bitmap.GetPixel(0, 0));
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
    public void BCanvas_SaveBlendLayer_Screen_Composites_Into_Base_Bitmap()
    {
        using var bitmap = new BBitmap(1, 1);
        using var canvas = bitmap.OpenRasterCanvas();
        canvas.Clear(new BColor(128, 128, 128, 255));
        canvas.SaveBlendLayer("screen");

        canvas.FillRect(new RectangleF(0, 0, 1, 1), new BColor(255, 0, 0, 255));
        canvas.RestoreBlendLayer();

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal((byte)255, pixel.R);
        Assert.InRange(pixel.G, (byte)127, (byte)128);
        Assert.InRange(pixel.B, (byte)127, (byte)128);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void BCanvas_SaveBlendLayer_Overlay_Composites_Into_Base_Bitmap()
    {
        using var bitmap = new BBitmap(1, 1);
        using var canvas = bitmap.OpenRasterCanvas();
        canvas.Clear(new BColor(128, 128, 128, 255));
        canvas.SaveBlendLayer("overlay");

        canvas.FillRect(new RectangleF(0, 0, 1, 1), new BColor(255, 0, 0, 255));
        canvas.RestoreBlendLayer();

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal((byte)255, pixel.R);
        Assert.InRange(pixel.G, (byte)0, (byte)1);
        Assert.InRange(pixel.B, (byte)0, (byte)1);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void BCanvas_SaveBlendLayer_Difference_Composites_Into_Base_Bitmap()
    {
        using var bitmap = new BBitmap(1, 1);
        using var canvas = bitmap.OpenRasterCanvas();
        canvas.Clear(new BColor(128, 128, 128, 255));
        canvas.SaveBlendLayer("difference");

        canvas.FillRect(new RectangleF(0, 0, 1, 1), new BColor(255, 0, 0, 255));
        canvas.RestoreBlendLayer();

        var pixel = bitmap.GetPixel(0, 0);
        Assert.InRange(pixel.R, (byte)127, (byte)128);
        Assert.InRange(pixel.G, (byte)127, (byte)128);
        Assert.InRange(pixel.B, (byte)127, (byte)128);
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
    public void BBitmap_OpenGraphics_Routes_Hinted_Overlay_Blend_Layer_Through_Backend_Neutral_Primitives()
    {
        using var bitmap = new BBitmap(4, 4);
        bitmap.Clear(new BColor(128, 128, 128, 255));
        using var graphics = Assert.IsType<GraphicsAdapter>(bitmap.OpenGraphics(new RectangleF(0, 0, 4, 4)));
        using var brush = graphics.GetSolidBrush(Color.FromArgb(255, 255, 0, 0));

        graphics.HintNextLayerCanUseRaster(true);
        graphics.SaveBlendLayer("overlay");
        graphics.DrawRectangle(brush, 0, 0, 4, 4);
        graphics.RestoreBlendLayer();

        var pixel = bitmap.GetPixel(1, 1);
        Assert.Equal(new BColor(255, 1, 1, 255), pixel);
        Assert.False(graphics.HasMaterializedCanvas);
        Assert.False(bitmap.HasMaterializedCompatBitmap);
    }

    [Fact]
    public void BBitmap_OpenGraphics_Routes_Hinted_Difference_Blend_Layer_Through_Backend_Neutral_Primitives()
    {
        using var bitmap = new BBitmap(4, 4);
        bitmap.Clear(new BColor(128, 128, 128, 255));
        using var graphics = Assert.IsType<GraphicsAdapter>(bitmap.OpenGraphics(new RectangleF(0, 0, 4, 4)));
        using var brush = graphics.GetSolidBrush(Color.FromArgb(255, 255, 0, 0));

        graphics.HintNextLayerCanUseRaster(true);
        graphics.SaveBlendLayer("difference");
        graphics.DrawRectangle(brush, 0, 0, 4, 4);
        graphics.RestoreBlendLayer();

        var pixel = bitmap.GetPixel(1, 1);
        Assert.InRange(pixel.R, (byte)127, (byte)128);
        Assert.InRange(pixel.G, (byte)127, (byte)128);
        Assert.InRange(pixel.B, (byte)127, (byte)128);
        Assert.Equal((byte)255, pixel.A);
        Assert.False(graphics.HasMaterializedCanvas);
        Assert.False(bitmap.HasMaterializedCompatBitmap);
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
        using var _ = BGraphicsBackend.OverrideForCurrentThread(BGraphicsBackend.BroilerRasterId);
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

    private static string GetRepoRoot() => Path.GetFullPath(Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".."));

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
    public void PixelDiffRunner_Compare_Matches_NonText_Screen_Blend_Fixture()
    {
        const string html = """
            <html><body style='margin:0;background:#808080'>
            <div style='width:4px;height:4px;margin-left:1px;margin-top:1px;background:#ff0000;mix-blend-mode:screen'></div>
            </body></html>
            """;

        using var rendered = HtmlRender.RenderToImage(html, 6, 6, BColor.White);
        using var expected = new BBitmap(6, 6);
        expected.Clear(new BColor(128, 128, 128, 255));
        FillRect(expected, 1, 1, 4, 4, new BColor(255, 128, 128, 255));

        using var diff = PixelDiffRunner.Compare(rendered, expected);

        Assert.True(diff.IsMatch);
        Assert.Equal(0, diff.DiffPixelCount);
        Assert.Equal(0d, diff.DiffRatio);
        Assert.Null(diff.DiffBitmap);
    }

    [Fact]
    public void PixelDiffRunner_Compare_Matches_NonText_Overlay_Blend_Fixture()
    {
        const string html = """
            <html><body style='margin:0;background:#808080'>
            <div style='width:4px;height:4px;margin-left:1px;margin-top:1px;background:#ff0000;mix-blend-mode:overlay'></div>
            </body></html>
            """;

        using var rendered = HtmlRender.RenderToImage(html, 6, 6, BColor.White);
        using var expected = new BBitmap(6, 6);
        expected.Clear(new BColor(128, 128, 128, 255));
        FillRect(expected, 1, 1, 4, 4, new BColor(255, 1, 1, 255));

        using var diff = PixelDiffRunner.Compare(rendered, expected);

        Assert.True(diff.IsMatch);
        Assert.Equal(0, diff.DiffPixelCount);
        Assert.Equal(0d, diff.DiffRatio);
        Assert.Null(diff.DiffBitmap);
    }

    [Fact]
    public void PixelDiffRunner_Compare_Matches_NonText_Difference_Blend_Fixture()
    {
        const string html = """
            <html><body style='margin:0;background:#808080'>
            <div style='width:4px;height:4px;margin-left:1px;margin-top:1px;background:#ff0000;mix-blend-mode:difference'></div>
            </body></html>
            """;

        using var rendered = HtmlRender.RenderToImage(html, 6, 6, BColor.White);
        using var expected = new BBitmap(6, 6);
        expected.Clear(new BColor(128, 128, 128, 255));
        FillRect(expected, 1, 1, 4, 4, new BColor(127, 128, 128, 255));

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

    private sealed class RecordingTextShaper : ITextShaper
    {
        public List<string> Calls { get; } = [];

        public SizeF MeasureString(FontAdapter font, string text)
        {
            Calls.Add("MeasureString");
            return new SizeF(7, 11);
        }

        public void MeasureString(FontAdapter font, string text, double maxWidth, out int charFit, out double charFitWidth)
        {
            Calls.Add("MeasureStringMaxWidth");
            charFit = 3;
            charFitWidth = 9;
        }

        public bool TryDrawString(BCanvas canvas, FontAdapter font, string text, Color color, PointF point)
        {
            Calls.Add("TryDrawString");
            return false;
        }

        public bool TryDrawGradientString(BCanvas canvas, FontAdapter font, string text, RectangleF rect, PointF point, SizeF size, Color[] colors, float[] positions, float angle)
        {
            Calls.Add("TryDrawGradientString");
            return false;
        }

        public void DrawString(SKCanvas canvas, FontAdapter font, string text, Color color, PointF point)
        {
            Calls.Add("DrawString");
        }

        public void DrawGradientString(SKCanvas canvas, FontAdapter font, string text, RectangleF rect, PointF point, SizeF size, Color[] colors, float[] positions, float angle)
        {
            Calls.Add("DrawGradientString");
        }
    }

    private sealed class RecordingFontTypefaceResolver : IFontTypefaceResolver
    {
        private readonly HashSet<string> _deferredFamilies = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _materializedFamilies = new(StringComparer.OrdinalIgnoreCase);

        internal List<string> Calls { get; } = [];

        public string RegisterFontFile(string path, string alias = null)
        {
            Calls.Add("RegisterFontFile");
            var family = alias ?? "RegisteredTypeface";
            _deferredFamilies.Add(family);
            _materializedFamilies.Remove(family);
            return family;
        }

        public bool HasDeferredLoadedTypefacePath(string family)
        {
            Calls.Add("HasDeferredLoadedTypefacePath");
            return _deferredFamilies.Contains(family);
        }

        public bool HasMaterializedLoadedTypeface(string family)
        {
            Calls.Add("HasMaterializedLoadedTypeface");
            return _materializedFamilies.Contains(family);
        }

        public SKTypeface ResolveTypeface(string family, FontStyle style)
        {
            Calls.Add("ResolveTypeface");
            _deferredFamilies.Add(family);
            _materializedFamilies.Add(family);
            return SKTypeface.Default;
        }
    }

    private sealed class RecordingCanvasCompat : ICanvasCompat
    {
        public List<string> Calls { get; } = [];

        public void DrawLine(SKCanvas canvas, float x1, float y1, float x2, float y2, SKPaint paint)
        {
            Calls.Add("DrawLine");
        }

        public void DrawRectangle(SKCanvas canvas, RectangleF rect, SKPaint paint)
        {
            Calls.Add("DrawRectangle");
        }

        public void DrawPath(SKCanvas canvas, GraphicsPathAdapter path, SKPaint paint)
        {
            Calls.Add("DrawPath");
        }

        public void ClipRounded(
            SKCanvas canvas,
            RectangleF rect,
            double cornerNw,
            double cornerNwY,
            double cornerNe,
            double cornerNeY,
            double cornerSe,
            double cornerSeY,
            double cornerSw,
            double cornerSwY)
        {
            Calls.Add("ClipRounded");
        }

        public SKPaint CreateTexturePaint(BBitmap bitmap, PointF translateTransformLocation)
        {
            Calls.Add("CreateTexturePaint");
            return new SKPaint();
        }

        public void DrawPolygon(SKCanvas canvas, PointF[] points, SKPaint paint)
        {
            Calls.Add("DrawPolygon");
        }

        public void SaveOpacityLayer(SKCanvas canvas, float opacity)
        {
            Calls.Add("SaveOpacityLayer");
        }

        public void SaveBlendLayer(SKCanvas canvas, string blendMode)
        {
            Calls.Add("SaveBlendLayer");
        }
    }

    private sealed class RecordingBitmapCompatSurface(int width, int height) : IBitmapCompatSurface
    {
        private readonly SKBitmap _bitmap = new(width, height);

        public List<string> Calls { get; } = [];

        public bool IsMaterialized => true;

        public void SetPixel(int x, int y, BColor color)
        {
            Calls.Add("SetPixel");
            _bitmap.SetPixel(x, y, color.ToSkColor());
        }

        public void Clear(BColor color)
        {
            Calls.Add("Clear");
            _bitmap.Erase(color.ToSkColor());
        }

        public SKBitmap AsBitmap()
        {
            Calls.Add("AsBitmap");
            return _bitmap;
        }

        public SKBitmap ToBitmapCopy()
        {
            Calls.Add("ToBitmapCopy");
            return _bitmap.Copy();
        }

        public SKCanvas OpenCanvas()
        {
            Calls.Add("OpenCanvas");
            return new SKCanvas(_bitmap);
        }

        public void SyncToPrimaryBuffer()
        {
            Calls.Add("SyncToPrimaryBuffer");
        }

        public void Dispose() => _bitmap.Dispose();
    }

}
