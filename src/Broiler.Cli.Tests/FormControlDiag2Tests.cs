using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class FormControlDiag2Tests
{
    private readonly ITestOutputHelper _out;
    public FormControlDiag2Tests(ITestOutputHelper output) { _out = output; }

    [Fact]
    public void DiagnoseGoogleLikeForm()
    {
        // Simplified Google-like page
        var html = @"<html><body style='margin:0'>
            <div style='text-align:center; padding-top:100px'>
                <form action='/search'>
                    <input type='text' name='q' style='width:400px'>
                    <br>
                    <input type='submit' value='Google Search'>
                    <input type='submit' value=""I'm Feeling Lucky"">
                </form>
            </div>
        </body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 300);
        bmp.Encode(new SKFileWStream("/tmp/diag_google_like.png"), SKEncodedImageFormat.Png, 100);

        _out.WriteLine($"Image: {bmp.Width}x{bmp.Height}");
        for (int y = 90; y < 180; y += 2)
        {
            int left = -1, right = -1, count = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                {
                    count++;
                    if (left < 0) left = x;
                    right = x;
                }
            }
            if (count > 0)
                _out.WriteLine($"y={y}: count={count}, left={left}, right={right}, center={(left+right)/2}");
        }
    }

    [Fact]
    public void DiagnoseInputTypeHidden()
    {
        // Hidden inputs should not render
        var html = @"<html><body style='margin:0;padding:0'>
            <form action='/search'>
                <input type='hidden' name='hl' value='en'>
                <input type='text' name='q' value='test'>
            </form>
        </body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        bmp.Encode(new SKFileWStream("/tmp/diag_hidden.png"), SKEncodedImageFormat.Png, 100);

        _out.WriteLine($"Image: {bmp.Width}x{bmp.Height}");
        for (int y = 0; y < bmp.Height; y += 2)
        {
            int left = -1, right = -1, count = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                {
                    count++;
                    if (left < 0) left = x;
                    right = x;
                }
            }
            if (count > 0)
                _out.WriteLine($"y={y}: count={count}, left={left}, right={right}");
        }
    }

    [Fact]
    public void DiagnoseCheckboxRadio()
    {
        var html = @"<html><body>
            <input type='checkbox'> Check me
            <br>
            <input type='radio' name='r' value='1'> Option 1
        </body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        bmp.Encode(new SKFileWStream("/tmp/diag_checkbox.png"), SKEncodedImageFormat.Png, 100);

        int nonWhite = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250) nonWhite++;
            }
        _out.WriteLine($"Image: {bmp.Width}x{bmp.Height}, nonWhite: {nonWhite}");
    }
}
