using System;
using System.IO;
using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using RenderImageFormat = Broiler.HtmlBridge.ImageFormat;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for SVG image detection and rendering.
/// Verifies that SVG data is properly detected by <see cref="ImageDecoder.DetectFormatFromBytes"/>
/// and that <c>SkiaImageAdapter.ImageFromStreamInt</c> rasterizes SVG input to a bitmap
/// via Svg.Skia instead of silently returning null.
/// </summary>
public class SvgImageRenderingTests
{
    // ────────────────────── SVG byte-level detection ──────────────────────

    [Fact]
    public void DetectFormatFromBytes_Returns_Svg_For_Xml_Declaration_With_SvgTag()
    {
        var svg = System.Text.Encoding.UTF8.GetBytes(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
        Assert.Equal(RenderImageFormat.Svg, ImageDecoder.DetectFormatFromBytes(svg));
    }

    [Fact]
    public void DetectFormatFromBytes_Returns_Svg_For_Direct_SvgTag()
    {
        var svg = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"100\"><rect fill=\"red\"/></svg>");
        Assert.Equal(RenderImageFormat.Svg, ImageDecoder.DetectFormatFromBytes(svg));
    }

    [Fact]
    public void DetectFormatFromBytes_Returns_Svg_With_Leading_Whitespace()
    {
        var svg = System.Text.Encoding.UTF8.GetBytes(
            "  \n\t<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
        Assert.Equal(RenderImageFormat.Svg, ImageDecoder.DetectFormatFromBytes(svg));
    }

    [Fact]
    public void DetectFormatFromBytes_Returns_Svg_With_Utf8_Bom()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var svgBytes = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
        var data = new byte[bom.Length + svgBytes.Length];
        Buffer.BlockCopy(bom, 0, data, 0, bom.Length);
        Buffer.BlockCopy(svgBytes, 0, data, bom.Length, svgBytes.Length);

        Assert.Equal(RenderImageFormat.Svg, ImageDecoder.DetectFormatFromBytes(data));
    }

    [Fact]
    public void DetectFormatFromBytes_Returns_Svg_With_Comment_And_Doctype_Preamble()
    {
        var svg = System.Text.Encoding.UTF8.GetBytes(
            "<!-- generated --><!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.1//EN\" " +
            "\"http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd\">" +
            "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");

        Assert.Equal(RenderImageFormat.Svg, ImageDecoder.DetectFormatFromBytes(svg));
    }

    [Fact]
    public void DetectFormatFromBytes_Returns_Unknown_For_NonSvg_Xml()
    {
        var xml = System.Text.Encoding.UTF8.GetBytes(
            "<?xml version=\"1.0\"?><root><item/></root>");
        Assert.Equal(RenderImageFormat.Unknown, ImageDecoder.DetectFormatFromBytes(xml));
    }

    [Fact]
    public void DetectFormatFromBytes_Returns_Png_Not_Svg_For_Png_Bytes()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Assert.Equal(RenderImageFormat.Png, ImageDecoder.DetectFormatFromBytes(png));
    }

    [Fact]
    public void DetectFormat_Returns_Svg_For_DotSvg_Extension()
    {
        Assert.Equal(RenderImageFormat.Svg, ImageDecoder.DetectFormat("https://example.com/logo.svg"));
    }

    [Fact]
    public void DetectFormat_Returns_Svg_For_DataUri_SvgXml()
    {
        Assert.Equal(RenderImageFormat.Svg, ImageDecoder.DetectFormat("data:image/svg+xml;base64,PHN2Zw=="));
    }

    // ────────────────────── SVG rasterization via Svg.Skia ──────────────────────

    [Fact]
    public void HtmlRender_SvgImage_Is_Rendered_As_Bitmap()
    {
        // A red 50×50 SVG as a data-URI.  The rendering engine should
        // rasterize it via Svg.Skia and display it in the output bitmap.
        var svgDataUri = "data:image/svg+xml;base64," +
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"50\" height=\"50\">" +
                "<rect fill=\"red\" width=\"50\" height=\"50\"/></svg>"));

        var html = $@"<!DOCTYPE html><html><body style=""margin:0;padding:0"">" +
                   $@"<img src=""{svgDataUri}"" width=""50"" height=""50""/></body></html>";

        using var bitmap = Broiler.HTML.Image.HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);
        Assert.Equal(200, bitmap.Width);
        Assert.Equal(200, bitmap.Height);

        // The top-left area (where the 50×50 red SVG should be) must
        // contain red pixels — proving the SVG was rasterized.
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > 200, $"Expected red channel > 200, got {pixel.Red}");
        Assert.True(pixel.Green < 50, $"Expected green channel < 50, got {pixel.Green}");
        Assert.True(pixel.Blue < 50, $"Expected blue channel < 50, got {pixel.Blue}");
    }

    [Fact]
    public void HtmlRender_SvgImage_Blue_Circle_Rendered()
    {
        var svgDataUri = "data:image/svg+xml;base64," +
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"100\">" +
                "<circle cx=\"50\" cy=\"50\" r=\"50\" fill=\"blue\"/></svg>"));

        var html = $@"<!DOCTYPE html><html><body style=""margin:0;padding:0"">" +
                   $@"<img src=""{svgDataUri}"" width=""100"" height=""100""/></body></html>";

        using var bitmap = Broiler.HTML.Image.HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);

        // Center of the circle (50,50) should be blue.
        var pixel = bitmap.GetPixel(50, 50);
        Assert.True(pixel.Blue > 200, $"Expected blue channel > 200, got {pixel.Blue}");
        Assert.True(pixel.Red < 50, $"Expected red channel < 50, got {pixel.Red}");
    }

    [Fact]
    public void HtmlRender_SvgImage_With_ViewBox_Rendered()
    {
        // SVG with viewBox and no explicit width/height — should use viewBox dimensions.
        var svgDataUri = "data:image/svg+xml;base64," +
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 80 60\">" +
                "<rect fill=\"green\" width=\"80\" height=\"60\"/></svg>"));

        var html = $@"<!DOCTYPE html><html><body style=""margin:0;padding:0"">" +
                   $@"<img src=""{svgDataUri}"" width=""80"" height=""60""/></body></html>";

        using var bitmap = Broiler.HTML.Image.HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);

        // The green rect should be visible at the top-left.
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > 100, $"Expected green channel > 100, got {pixel.Green}");
    }

    [Fact]
    public void HtmlRender_EmptySvg_Does_Not_Crash()
    {
        var svgDataUri = "data:image/svg+xml;base64," +
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"));

        var html = $@"<!DOCTYPE html><html><body><img src=""{svgDataUri}""/></body></html>";

        using var bitmap = Broiler.HTML.Image.HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);
        Assert.Equal(200, bitmap.Width);
    }

    [Fact]
    public void BSvgRasterizer_RasterizeToBitmap_Returns_BackendNeutral_Bitmap()
    {
        var svgBytes = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"30\">" +
            "<rect fill=\"red\" width=\"40\" height=\"30\"/></svg>");

        using var bitmap = BSvgRasterizer.RasterizeToBitmap(svgBytes);

        Assert.NotNull(bitmap);
        Assert.Equal(40, bitmap.Width);
        Assert.Equal(30, bitmap.Height);

        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.R > 200, $"Expected red channel > 200, got {pixel.R}");
        Assert.True(pixel.G < 50, $"Expected green channel < 50, got {pixel.G}");
        Assert.True(pixel.B < 50, $"Expected blue channel < 50, got {pixel.B}");
    }

    [Fact]
    public void BSvgRasterizer_IsSvgData_Accepts_Commented_Svg_Preamble()
    {
        var svgBytes = System.Text.Encoding.UTF8.GetBytes(
            "<!-- generated --><svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"30\"></svg>");

        Assert.True(BSvgRasterizer.IsSvgData(svgBytes));
    }

    [Fact]
    public void BSvgRasterizer_IsSvgData_Rejects_Comment_That_Mentions_Svg_But_Has_NonSvg_Root()
    {
        var xmlBytes = System.Text.Encoding.UTF8.GetBytes(
            "<!-- mentions <svg> but is not svg --><root></root>");

        Assert.False(BSvgRasterizer.IsSvgData(xmlBytes));
        Assert.Equal(RenderImageFormat.Unknown, ImageDecoder.DetectFormatFromBytes(xmlBytes));
    }

    [Fact]
    public void HtmlRender_NonSvg_Image_Still_Works()
    {
        // A minimal 1×1 red PNG encoded as base64 data URI.
        // This ensures that the SVG detection does not break normal image loading.
        var pngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==";
        var html = $@"<!DOCTYPE html><html><body><img src=""data:image/png;base64,{pngBase64}"" width=""1"" height=""1""/></body></html>";

        using var bitmap = Broiler.HTML.Image.HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);
        Assert.Equal(200, bitmap.Width);
    }

    [Fact]
    public void HtmlRender_SvgImage_ComplexSvg_With_Path()
    {
        // A simple SVG with a path element (triangle) - tests path rendering.
        var svgDataUri = "data:image/svg+xml;base64," +
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"100\">" +
                "<path d=\"M10,90 L50,10 L90,90 Z\" fill=\"orange\" stroke=\"black\" stroke-width=\"2\"/>" +
                "</svg>"));

        var html = $@"<!DOCTYPE html><html><body style=""margin:0;padding:0"">" +
                   $@"<img src=""{svgDataUri}"" width=""100"" height=""100""/></body></html>";

        using var bitmap = Broiler.HTML.Image.HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);
        Assert.Equal(200, bitmap.Width);

        // The triangle is in the top-left 100×100 area.
        // At (50, 50) we should have the orange fill.
        var pixel = bitmap.GetPixel(50, 50);
        Assert.True(pixel.Red > 150, $"Expected red channel > 150 (orange), got {pixel.Red}");
        Assert.True(pixel.Green > 100, $"Expected green channel > 100 (orange), got {pixel.Green}");
    }

    [Fact]
    public void HtmlRender_SvgImage_With_Text()
    {
        // SVG with text element — verifies text rendering doesn't crash.
        var svgDataUri = "data:image/svg+xml;base64," +
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"50\">" +
                "<rect fill=\"white\" width=\"200\" height=\"50\"/>" +
                "<text x=\"10\" y=\"30\" font-size=\"20\" fill=\"black\">Hello SVG</text>" +
                "</svg>"));

        var html = $@"<!DOCTYPE html><html><body style=""margin:0;padding:0"">" +
                   $@"<img src=""{svgDataUri}"" width=""200"" height=""50""/></body></html>";

        using var bitmap = Broiler.HTML.Image.HtmlRender.RenderToImage(html, 300, 200);
        Assert.NotNull(bitmap);
        Assert.Equal(300, bitmap.Width);
    }

    [Fact]
    public void HtmlRender_MultipleSvgImages()
    {
        // Two different SVG images side by side.
        var redSvg = "data:image/svg+xml;base64," +
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"50\" height=\"50\">" +
                "<rect fill=\"red\" width=\"50\" height=\"50\"/></svg>"));
        var blueSvg = "data:image/svg+xml;base64," +
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"50\" height=\"50\">" +
                "<rect fill=\"blue\" width=\"50\" height=\"50\"/></svg>"));

        var html = $@"<!DOCTYPE html><html><body style=""margin:0;padding:0"">" +
                   $@"<img src=""{redSvg}"" width=""50"" height=""50""/>" +
                   $@"<img src=""{blueSvg}"" width=""50"" height=""50""/></body></html>";

        using var bitmap = Broiler.HTML.Image.HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);

        // First image area should be red
        var redPixel = bitmap.GetPixel(10, 10);
        Assert.True(redPixel.Red > 200, $"Expected red pixel, got R={redPixel.Red}");

        // Second image area should be blue (starts at x≈50)
        var bluePixel = bitmap.GetPixel(60, 10);
        Assert.True(bluePixel.Blue > 200, $"Expected blue pixel, got B={bluePixel.Blue}");
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"8px\" viewBox=\"0 0 2147483647 1\" preserveAspectRatio=\"none\"><rect y=\"0\" width=\"100%\" height=\"100%\" fill=\"lime\"/></svg>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" height=\"8px\" viewBox=\"0 0 1 2147483647\" preserveAspectRatio=\"none\"><rect y=\"0\" width=\"100%\" height=\"100%\" fill=\"lime\"/></svg>")]
    public void HtmlRender_BackgroundSvgContain_With_PreserveAspectRatioNone_Fills_BackgroundArea(string svgMarkup)
    {
        var svgDataUri = "data:image/svg+xml;base64," +
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svgMarkup));

        var html = $@"<!DOCTYPE html>
<html>
<body style=""margin:8px"">
  <div style=""background-image:url('{svgDataUri}');background-repeat:no-repeat;background-size:contain;border:1px solid black;width:768px;height:256px""></div>
</body>
</html>";

        using var bitmap = Broiler.HTML.Image.HtmlRender.RenderToImage(html, 1024, 768);

        var interiorPixel = bitmap.GetPixel(100, 100);
        Assert.True(interiorPixel.Green > 200,
            $"Expected contain background interior to be green, got R={interiorPixel.Red} G={interiorPixel.Green} B={interiorPixel.Blue}");
        Assert.True(interiorPixel.Red < 50, $"Expected red channel < 50, got {interiorPixel.Red}");
        Assert.True(interiorPixel.Blue < 50, $"Expected blue channel < 50, got {interiorPixel.Blue}");
    }
}
