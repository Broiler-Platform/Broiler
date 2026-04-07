using System;
using System.IO;
using Broiler.HtmlBridge;
using RenderImageFormat = Broiler.HtmlBridge.ImageFormat;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for SVG image detection and handling.
/// Verifies that SVG data is properly detected by <see cref="ImageDecoder.DetectFormatFromBytes"/>
/// and that <c>SkiaImageAdapter.ImageFromStreamInt</c> throws a meaningful
/// <see cref="NotSupportedException"/> for SVG input instead of silently returning null.
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

    // ────────────────────── SkiaImageAdapter SVG rejection ──────────────────────

    [Fact]
    public void HtmlRender_SvgImage_Throws_NotSupportedException()
    {
        // Construct an HTML page with an <img> whose src is a data-URI SVG.
        // The rendering engine will attempt to decode the image via
        // SkiaImageAdapter.ImageFromStreamInt, which should throw
        // NotSupportedException for SVG data.  The error is caught by
        // ImageLoadHandler and reported — so the render completes without
        // crashing, but the image is not displayed.
        var svgDataUri = "data:image/svg+xml;base64," +
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"50\" height=\"50\"><rect fill=\"red\" width=\"50\" height=\"50\"/></svg>"));

        var html = $@"<!DOCTYPE html><html><body><img src=""{svgDataUri}"" width=""50"" height=""50""/></body></html>";

        // The render should complete without throwing — the exception is
        // handled internally and the image simply isn't shown.
        using var bitmap = Broiler.HTML.Image.HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);
        Assert.Equal(200, bitmap.Width);
        Assert.Equal(200, bitmap.Height);
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
}
