using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

/// <summary>
/// Diagnostic tests to understand the three reported issues:
/// 1. Controls span from left to right border
/// 2. Buttons contain no text 
/// 3. Controls are not clickable
/// </summary>
public class FormControlDiagnosticTests
{
    private readonly ITestOutputHelper _out;
    public FormControlDiagnosticTests(ITestOutputHelper output) => _out = output;

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

    /// <summary>
    /// Check if inline-block controls are properly sized (not spanning full width).
    /// A submit button should be much narrower than the container width.
    /// </summary>
    [Fact]
    public void InputSubmit_Width_Is_Constrained()
    {
        var html = @"<html><body style='margin:0'>
            <input type='submit' value='Search'>
        </body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 60);
        
        // Count columns that have non-white pixels
        int leftMostNonWhite = -1;
        int rightMostNonWhite = -1;
        for (int x = 0; x < bmp.Width; x++)
        {
            for (int y = 0; y < bmp.Height; y++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                {
                    if (leftMostNonWhite < 0) leftMostNonWhite = x;
                    rightMostNonWhite = x;
                    break;
                }
            }
        }
        
        int controlWidth = rightMostNonWhite - leftMostNonWhite + 1;
        _out.WriteLine($"Submit button: left={leftMostNonWhite}, right={rightMostNonWhite}, width={controlWidth} (container=800)");
        
        // The button should NOT span the full 800px. With "Search" text + padding,
        // it should be much less than 400px wide.
        Assert.True(controlWidth < 400, 
            $"Submit button width ({controlWidth}px) should be constrained, not span full container");
    }

    /// <summary>
    /// Check if a text input is properly sized using its min-width (173px).
    /// </summary>
    [Fact]
    public void InputText_Width_Is_Constrained()
    {
        var html = @"<html><body style='margin:0'>
            <input type='text'>
        </body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 60);
        
        int leftMostNonWhite = -1;
        int rightMostNonWhite = -1;
        for (int x = 0; x < bmp.Width; x++)
        {
            for (int y = 0; y < bmp.Height; y++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                {
                    if (leftMostNonWhite < 0) leftMostNonWhite = x;
                    rightMostNonWhite = x;
                    break;
                }
            }
        }
        
        int controlWidth = rightMostNonWhite - leftMostNonWhite + 1;
        _out.WriteLine($"Text input: left={leftMostNonWhite}, right={rightMostNonWhite}, width={controlWidth} (container=800)");
        
        // The input should use ~173px (min-width), not 800px
        Assert.True(controlWidth < 400,
            $"Text input width ({controlWidth}px) should be ~173px, not span full container");
    }

    /// <summary>
    /// Input type=submit should render its "value" text.
    /// Count dark pixels in the text area to verify text rendering.
    /// </summary>
    [Fact]
    public void InputSubmit_Has_Text_Content()
    {
        var html = @"<html><body style='margin:0; padding:0;'>
            <input type='submit' value='SearchButton'>
        </body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 40);
        
        // Count dark pixels that could be text (< 100 in any channel)
        int darkPixels = 0;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.Red < 100 || px.Green < 100 || px.Blue < 100)
                darkPixels++;
        }
        
        _out.WriteLine($"Submit button dark pixels (text): {darkPixels}");
        Assert.True(darkPixels > 20,
            $"Submit button should have visible text (dark pixels={darkPixels})");
    }

    /// <summary>
    /// Button element should render its child text content.
    /// </summary>
    [Fact]
    public void ButtonElement_Has_Text_Content()
    {
        var html = @"<html><body style='margin:0; padding:0;'>
            <button>ClickMe</button>
        </body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 40);
        
        int darkPixels = 0;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.Red < 100 || px.Green < 100 || px.Blue < 100)
                darkPixels++;
        }
        
        _out.WriteLine($"Button element dark pixels (text): {darkPixels}");
        Assert.True(darkPixels > 20,
            $"Button element should have visible text (dark pixels={darkPixels})");
    }

    /// <summary>
    /// Google-like form with centered controls.
    /// </summary>
    [Fact]
    public void CenteredForm_Controls_Are_Centered()
    {
        var html = @"<html><body style='margin:0'>
            <center>
                <input type='text' name='q'>
                <br>
                <input type='submit' value='Search'>
            </center>
        </body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 100);
        
        // Find the horizontal extent of the text input (first row of controls)
        int inputLeft = -1, inputRight = -1;
        for (int x = 0; x < bmp.Width; x++)
        {
            bool hasPixel = false;
            for (int y = 0; y < 30; y++) // Check top region only
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                {
                    hasPixel = true;
                    break;
                }
            }
            if (hasPixel)
            {
                if (inputLeft < 0) inputLeft = x;
                inputRight = x;
            }
        }
        
        if (inputLeft >= 0)
        {
            int inputCenter = (inputLeft + inputRight) / 2;
            int containerCenter = 400; // 800/2
            _out.WriteLine($"Centered input: left={inputLeft}, right={inputRight}, center={inputCenter} (container center=400)");
            
            // The center of the input should be near the center of the container
            Assert.True(Math.Abs(inputCenter - containerCenter) < 100,
                $"Input should be centered (control center={inputCenter}, container center={containerCenter})");
        }
        else
        {
            Assert.Fail("No visible pixels found for input");
        }
    }
}
