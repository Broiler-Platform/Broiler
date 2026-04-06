using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

/// <summary>
/// Diagnostic tests to understand current form control rendering.
/// </summary>
public class FormControlDiagnosticTests
{
    private readonly ITestOutputHelper _out;
    public FormControlDiagnosticTests(ITestOutputHelper output) { _out = output; }

    private void AnalyzeRow(SKBitmap bmp, int y, string label)
    {
        int leftMostNonWhite = -1;
        int rightMostNonWhite = -1;
        int nonWhite = 0;
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
            {
                nonWhite++;
                if (leftMostNonWhite < 0) leftMostNonWhite = x;
                rightMostNonWhite = x;
            }
        }
        _out.WriteLine($"{label} y={y}: nonWhite={nonWhite}, left={leftMostNonWhite}, right={rightMostNonWhite}");
    }

    [Fact]
    public void DiagnoseInputWidthAndAlignment()
    {
        // Centered form with input and button
        var html = @"<html><body style='text-align:center'>
            <form action='/search' style='text-align:center'>
                <input type='text' name='q' value='query'>
                <input type='submit' value='Search'>
            </form>
        </body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 100);
        bmp.Encode(new SKFileWStream("/tmp/diag_form_centered.png"), SKEncodedImageFormat.Png, 100);

        _out.WriteLine($"Image: {bmp.Width}x{bmp.Height}");
        for (int y = 0; y < bmp.Height; y += 2)
        {
            AnalyzeRow(bmp, y, "centered");
        }
    }

    [Fact]
    public void DiagnoseButtonElementText()
    {
        // Button element with text
        var html = @"<html><body>
            <button>Click Me</button>
        </body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        bmp.Encode(new SKFileWStream("/tmp/diag_button_text.png"), SKEncodedImageFormat.Png, 100);

        _out.WriteLine($"Image: {bmp.Width}x{bmp.Height}");

        // Check if there's any text-like content (dark pixels)
        int darkPixels = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 100 && px.Green < 100 && px.Blue < 100)
                    darkPixels++;
            }
        _out.WriteLine($"Dark pixels (text-like): {darkPixels}");

        for (int y = 0; y < bmp.Height; y += 2)
        {
            AnalyzeRow(bmp, y, "button");
        }
    }

    [Fact]
    public void DiagnoseSimpleInputAlignment()
    {
        // Just a plain input, no form wrapping
        var html = @"<html><body>
            <input type='text' value='Hello'>
            <input type='submit' value='Submit'>
        </body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 100);
        bmp.Encode(new SKFileWStream("/tmp/diag_simple_input.png"), SKEncodedImageFormat.Png, 100);

        _out.WriteLine($"Image: {bmp.Width}x{bmp.Height}");
        for (int y = 0; y < bmp.Height; y += 2)
        {
            AnalyzeRow(bmp, y, "simple");
        }
    }

    [Fact]
    public void DiagnoseFormBlock()
    {
        // Check if form element is actually display:block and stretching full width
        var html = @"<html><body style='margin:0; padding:0'>
            <form style='background:red'>
                <input type='text' value='query'>
            </form>
        </body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 100);
        bmp.Encode(new SKFileWStream("/tmp/diag_form_block.png"), SKEncodedImageFormat.Png, 100);

        _out.WriteLine($"Image: {bmp.Width}x{bmp.Height}");
        // Count red pixels to see form width
        int redPixels = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50)
                    redPixels++;
            }
        _out.WriteLine($"Red pixels (form background): {redPixels}");
        for (int y = 0; y < bmp.Height; y += 2)
        {
            AnalyzeRow(bmp, y, "formblock");
        }
    }
}
