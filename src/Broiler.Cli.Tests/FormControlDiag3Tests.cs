using Broiler.HTML.Image;
using Broiler.HtmlBridge;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class FormControlDiag3Tests
{
    private readonly ITestOutputHelper _out;
    public FormControlDiag3Tests(ITestOutputHelper output) { _out = output; }

    [Fact]
    public void InputSubmitAfterPostProcessor_HasText()
    {
        // Simulate Broiler.Cli pipeline: post-process then render
        var html = @"<html><body>
            <form action='/search'>
                <input type='submit' value='Google Search'>
                <input type='submit' value=""I'm Feeling Lucky"">
            </form>
        </body></html>";

        var processed = HtmlPostProcessor.Process(html);
        _out.WriteLine($"Processed HTML:\n{processed}");

        using var bmp = HtmlRender.RenderToImage(processed, 600, 100);
        bmp.Encode(new SKFileWStream("/tmp/diag_submit_text.png"), SKEncodedImageFormat.Png, 100);

        int darkPixels = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 100 && px.Green < 100 && px.Blue < 100)
                    darkPixels++;
            }
        _out.WriteLine($"Dark pixels (text): {darkPixels}");
        Assert.True(darkPixels > 50, $"Submit buttons should have visible text. Dark pixels: {darkPixels}");
    }

    [Fact]
    public void HiddenInput_ShouldNotRender()
    {
        var html = @"<html><body style='margin:0'>
            <input type='hidden' name='hl' value='en'>
        </body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 400, 50);
        bmp.Encode(new SKFileWStream("/tmp/diag_hidden_only.png"), SKEncodedImageFormat.Png, 100);

        int nonWhite = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                    nonWhite++;
            }
        _out.WriteLine($"Non-white pixels: {nonWhite}");
        // Hidden input should render NOTHING
        Assert.Equal(0, nonWhite);
    }

    [Fact]
    public void GoogleSearchLikePage_Layout()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><title>Google</title></head>
<body>
<center>
<br><br><br>
<form action='/search' name='f'>
<input type='hidden' name='hl' value='en'>
<input maxlength='2048' name='q' size='55' title='Google Search' type='text' value=''>
<br>
<input name='btnG' type='submit' value='Google Search'>
<input name='btnI' type='submit' value=""I'm Feeling Lucky"">
</form>
</center>
</body>
</html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 400);
        bmp.Encode(new SKFileWStream("/tmp/diag_google_full.png"), SKEncodedImageFormat.Png, 100);

        _out.WriteLine($"Image: {bmp.Width}x{bmp.Height}");
        for (int y = 0; y < bmp.Height; y += 4)
        {
            int left = -1, right = -1, count = 0, darkCount = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                {
                    count++;
                    if (left < 0) left = x;
                    right = x;
                }
                if (px.Red < 100 && px.Green < 100 && px.Blue < 100)
                    darkCount++;
            }
            if (count > 0)
                _out.WriteLine($"y={y}: visible={count}, left={left}, right={right}, center={(left+right)/2}, dark={darkCount}");
        }
    }
}
