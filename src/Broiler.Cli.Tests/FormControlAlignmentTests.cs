using Broiler.HTML.Image;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for inline-block form control alignment.
/// Verifies that text-align:center and text-align:right properly
/// shift inline-block elements (input, button, select, textarea)
/// along with their content, borders, and backgrounds.
/// CSS 2.1 §9.4.2, §16.2.
/// </summary>
public class FormControlAlignmentTests
{
    /// <summary>
    /// Finds the horizontal extent of non-white pixels in a bitmap.
    /// Returns (leftMost, rightMost) or (-1, -1) if no non-white pixels found.
    /// </summary>
    private static (int left, int right) FindHorizontalExtent(BBitmap bmp)
    {
        int left = -1, right = -1;
        for (int x = 0; x < bmp.Width; x++)
        {
            for (int y = 0; y < bmp.Height; y++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R < 250 || px.G < 250 || px.B < 250)
                {
                    if (left < 0) left = x;
                    right = x;
                    break;
                }
            }
        }
        return (left, right);
    }

    /// <summary>
    /// text-align:center must center an inline-block submit button.
    /// Regression: ApplyCenterAlignment returned early when line.Words was
    /// empty, skipping inline-block rectangles entirely.
    /// </summary>
    [Fact]
    public void CenteredSubmitButton_IsCentered()
    {
        var html = @"<html><body style='margin:0'>
            <div style='text-align:center; width:800px'>
                <input type='submit' value='Go'>
            </div>
        </body></html>";
        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, 800, 40);
        var (left, right) = FindHorizontalExtent(bmp);
        int center = (left + right) / 2;
        Assert.True(left > 200 && right < 600,
            $"Centered submit button should be in the middle third (left={left}, right={right}, center={center})");
    }

    /// <summary>
    /// text-align:center with the &lt;center&gt; tag must center inputs.
    /// </summary>
    [Fact]
    public void CenterTag_CentersTextInput()
    {
        var html = @"<html><body style='margin:0'>
            <center style='width:800px'>
                <input type='text'>
            </center>
        </body></html>";
        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, 800, 40);
        var (left, right) = FindHorizontalExtent(bmp);
        int center = (left + right) / 2;
        // Input is ~173px wide. Centered at 400, left≈313, right≈486.
        Assert.True(left > 200 && right < 600,
            $"Centered text input should be in the middle third (left={left}, right={right}, center={center})");
    }

    /// <summary>
    /// text-align:right must right-align an inline-block button element.
    /// Regression: ApplyRightAlignment had the same early-return bug.
    /// </summary>
    [Fact]
    public void RightAlignedButton_IsOnRight()
    {
        var html = @"<html><body style='margin:0'>
            <div style='text-align:right; width:800px'>
                <button>OK</button>
            </div>
        </body></html>";
        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, 800, 40);
        var (left, right) = FindHorizontalExtent(bmp);
        // Right-aligned: the button should be near x=800
        Assert.True(left > 600 && right >= 790,
            $"Right-aligned button should be near the right edge (left={left}, right={right})");
    }

    /// <summary>
    /// Inline-block controls should NOT span full container width.
    /// </summary>
    [Fact]
    public void InlineBlockInput_WidthIsConstrained()
    {
        var html = @"<html><body style='margin:0'>
            <input type='submit' value='Search'>
        </body></html>";
        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, 800, 40);
        var (left, right) = FindHorizontalExtent(bmp);
        int width = right - left + 1;
        Assert.True(width < 400,
            $"Submit button width ({width}px) should be constrained, not span full container");
    }

    /// <summary>
    /// Submit and button elements should render text content.
    /// </summary>
    [Theory]
    [InlineData("<input type='submit' value='SearchButton'>")]
    [InlineData("<button>ClickMe</button>")]
    public void FormControl_HasVisibleText(string control)
    {
        var html = $"<html><body style='margin:0; padding:0;'>{control}</body></html>";
        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, 400, 40);

        // Count dark pixels (text rendered in black or near-black)
        int darkPixels = 0;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.R < 100 || px.G < 100 || px.B < 100)
                darkPixels++;
        }

        Assert.True(darkPixels > 20,
            $"Form control should have visible text (dark pixels={darkPixels})");
    }

    /// <summary>
    /// Google-like form with centered controls should center properly.
    /// </summary>
    [Fact]
    public void GoogleLikeForm_ControlsAreCentered()
    {
        var html = @"<html><body style='margin:0'>
            <center style='width:800px'>
                <input type='text' name='q'>
            </center>
        </body></html>";
        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, 800, 60);

        var (left, right) = FindHorizontalExtent(bmp);
        int center = (left + right) / 2;
        Assert.True(Math.Abs(center - 400) < 100,
            $"Input should be centered (control center={center}, container center=400)");
    }
}
