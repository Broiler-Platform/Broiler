using Broiler.HTML.Image;
using Broiler.HtmlBridge;
using SkiaSharp;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Verifies that HTML form controls are visible in rendered output.
/// Regression tests for:
/// - UA default styles (CssDefaults.cs)
/// - Value text injection (HtmlParser.cs)
/// - Min-width / min-height in inline-block layout (CssLayoutEngine.cs)
/// - Form stripping bug (HtmlPostProcessor should NOT strip form elements)
/// </summary>
public class FormControlRenderTests
{
    private static int CountNonWhitePixels(SKBitmap bmp)
    {
        int count = 0;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                count++;
        }
        return count;
    }

    [Fact]
    public void InputSubmit_Renders_Visible()
    {
        var html = @"<html><body><input type='submit' value='Search'></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        Assert.True(CountNonWhitePixels(bmp) > 10,
            "Submit button should render visible pixels (border + text)");
    }

    [Fact]
    public void InputText_Renders_Visible()
    {
        var html = @"<html><body><input type='text' value='Hello'></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        Assert.True(CountNonWhitePixels(bmp) > 10,
            "Text input should render visible pixels (border + text)");
    }

    [Fact]
    public void Button_Element_Renders_Visible()
    {
        var html = @"<html><body><button>Click Me</button></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        Assert.True(CountNonWhitePixels(bmp) > 10,
            "Button element should render visible pixels");
    }

    [Fact]
    public void Select_Element_Renders_Visible()
    {
        var html = @"<html><body><select><option>One</option></select></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        Assert.True(CountNonWhitePixels(bmp) > 10,
            "Select element should render visible pixels (border)");
    }

    [Fact]
    public void Textarea_Element_Renders_Visible()
    {
        var html = @"<html><body><textarea>Some text</textarea></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        Assert.True(CountNonWhitePixels(bmp) > 10,
            "Textarea element should render visible pixels (border + text)");
    }

    /// <summary>
    /// Regression: HtmlPostProcessor.Process must NOT strip form elements.
    /// Previously, StripHiddenTestArtifacts removed all &lt;form&gt; elements
    /// which made form controls inside &lt;form&gt; tags invisible.
    /// </summary>
    [Fact]
    public void FormWrapped_Controls_Not_Stripped()
    {
        var html = @"<html><body>
            <form action='/search'>
                <input type='text' name='q' value='Hello'>
                <input type='submit' value='Search'>
            </form>
        </body></html>";

        var processed = HtmlPostProcessor.Process(html);

        // The form tag and its contents must be preserved
        Assert.Contains("<input", processed);
        Assert.Contains("Search", processed);
        Assert.Contains("<form", processed);
    }

    /// <summary>
    /// Regression: Controls inside a form tag must render visibly after
    /// HtmlPostProcessor.Process.
    /// </summary>
    [Fact]
    public void FormWrapped_Controls_Render_Visible()
    {
        var html = @"<html><body>
            <form action='/search'>
                <input type='text' name='q' value='Hello'>
                <input type='submit' value='Search'>
            </form>
        </body></html>";

        var processed = HtmlPostProcessor.Process(html);
        using var bmp = HtmlRender.RenderToImage(processed, 600, 100);
        Assert.True(CountNonWhitePixels(bmp) > 50,
            "Form-wrapped controls should render visible pixels after post-processing");
    }
}
