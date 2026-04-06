using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>Diagnostic tests to check form control visibility.</summary>
public class FormControlDiagTests
{
    [Fact]
    public void InputButton_Should_Render_Visible_Box()
    {
        // Very simple: just a submit button
        var html = @"<html><body><input type='submit' value='Search'></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 200);
        
        // Count non-white pixels in center area
        int nonWhite = 0;
        int borderGrey = 0; // #767676 = RGB(118,118,118)
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                nonWhite++;
            if (Math.Abs(px.Red - 118) < 15 && Math.Abs(px.Green - 118) < 15 && Math.Abs(px.Blue - 118) < 15)
                borderGrey++;
        }

        // Save to file for manual inspection
        using (var ms = new System.IO.MemoryStream())
        {
            bmp.Encode(ms, SKEncodedImageFormat.Png, 100);
            System.IO.File.WriteAllBytes("/tmp/form_control_diag_button.png", ms.ToArray());
        }

        Assert.True(nonWhite > 10, 
            $"Input button should render visible pixels. nonWhite={nonWhite}, borderGrey={borderGrey}");
    }

    [Fact]
    public void TextInput_Should_Render_Visible_Box()
    {
        var html = @"<html><body><input type='text' value='Hello'></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 200);
        
        int nonWhite = 0;
        int borderGrey = 0;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                nonWhite++;
            if (Math.Abs(px.Red - 118) < 15 && Math.Abs(px.Green - 118) < 15 && Math.Abs(px.Blue - 118) < 15)
                borderGrey++;
        }

        using (var ms = new System.IO.MemoryStream())
        {
            bmp.Encode(ms, SKEncodedImageFormat.Png, 100);
            System.IO.File.WriteAllBytes("/tmp/form_control_diag_text.png", ms.ToArray());
        }

        Assert.True(nonWhite > 10,
            $"Text input should render visible pixels. nonWhite={nonWhite}, borderGrey={borderGrey}");
    }

    [Fact]
    public void Select_Should_Render_Visible_Box()
    {
        var html = @"<html><body><select><option>One</option><option>Two</option></select></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 200);
        
        int nonWhite = 0;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                nonWhite++;
        }

        using (var ms = new System.IO.MemoryStream())
        {
            bmp.Encode(ms, SKEncodedImageFormat.Png, 100);
            System.IO.File.WriteAllBytes("/tmp/form_control_diag_select.png", ms.ToArray());
        }

        Assert.True(nonWhite > 10,
            $"Select should render visible pixels. nonWhite={nonWhite}");
    }

    [Fact]
    public void Button_Should_Render_Visible_Box()
    {
        var html = @"<html><body><button>Click Me</button></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 200);
        
        int nonWhite = 0;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                nonWhite++;
        }

        using (var ms = new System.IO.MemoryStream())
        {
            bmp.Encode(ms, SKEncodedImageFormat.Png, 100);
            System.IO.File.WriteAllBytes("/tmp/form_control_diag_button_tag.png", ms.ToArray());
        }

        Assert.True(nonWhite > 10,
            $"Button should render visible pixels. nonWhite={nonWhite}");
    }

    [Fact]
    public void Textarea_Should_Render_Visible_Box()
    {
        var html = @"<html><body><textarea>Some text</textarea></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 200);
        
        int nonWhite = 0;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                nonWhite++;
        }

        using (var ms = new System.IO.MemoryStream())
        {
            bmp.Encode(ms, SKEncodedImageFormat.Png, 100);
            System.IO.File.WriteAllBytes("/tmp/form_control_diag_textarea.png", ms.ToArray());
        }

        Assert.True(nonWhite > 10,
            $"Textarea should render visible pixels. nonWhite={nonWhite}");
    }

    [Fact]
    public void AllFormControls_Side_By_Side()
    {
        var html = @"<html><body style='padding:20px'>
            <input type='text' value='Hello'>
            <input type='submit' value='Search'>
            <button>Click</button>
            <select><option>Pick</option></select>
            <br>
            <textarea>Some text</textarea>
            <input type='checkbox'>
            <input type='radio'>
        </body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 400);
        
        int nonWhite = 0;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                nonWhite++;
        }

        using (var ms = new System.IO.MemoryStream())
        {
            bmp.Encode(ms, SKEncodedImageFormat.Png, 100);
            System.IO.File.WriteAllBytes("/tmp/form_control_diag_all.png", ms.ToArray());
        }

        Assert.True(nonWhite > 100,
            $"All form controls should render visible pixels. nonWhite={nonWhite}");
    }
}
