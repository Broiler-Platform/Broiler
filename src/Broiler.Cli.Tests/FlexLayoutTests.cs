using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for CSS flex/grid layout fallback.
/// Broiler doesn't implement true flex/grid layout; instead it routes
/// all flex children through the inline-block (FlowInlineBlock) path.
/// These tests verify that:
/// 1. Block-level children of flex containers are NOT wrapped in
///    anonymous block boxes (they remain individual flex items).
/// 2. Flex children with display:block flow side-by-side (row direction).
/// 3. Submit buttons inside flex containers render correctly.
/// </summary>
public class FlexLayoutTests
{
    /// <summary>
    /// Two display:block divs inside a flex container should flow
    /// side-by-side (like flex-direction:row), not stack vertically.
    /// Regression: CorrectBlockInsideInline was wrapping them in a
    /// single anonymous block, causing them to stack.
    /// </summary>
    [Fact]
    public void FlexChildren_DisplayBlock_SideBySide()
    {
        var html = "<html><body style='margin:0'>" +
            "<div style='display:flex; width:600px'>" +
            "<div style='background:red; padding:10px'><span>Short</span></div>" +
            "<div style='background:blue; padding:10px; color:white'><span>Longer</span></div>" +
            "</div></body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 100);

        int firstRed = -1, firstBlue = -1;
        bool bothOnSameRow = false;
        for (int y = 0; y < bmp.Height; y++)
        {
            bool hasRed = false, hasBlue = false;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50) hasRed = true;
                if (px.Red < 50 && px.Green < 50 && px.Blue > 200) hasBlue = true;
            }
            if (hasRed && firstRed < 0) firstRed = y;
            if (hasBlue && firstBlue < 0) firstBlue = y;
            if (hasRed && hasBlue) bothOnSameRow = true;
        }

        Assert.True(bothOnSameRow || Math.Abs(firstRed - firstBlue) < 5,
            $"Flex children should be side-by-side. Red starts at y={firstRed}, blue at y={firstBlue}");
    }

    /// <summary>
    /// Submit buttons inside a flex container should flow side-by-side.
    /// </summary>
    [Fact]
    public void FlexChildren_SubmitButtons_SideBySide()
    {
        var html = "<html><body style='margin:0'>" +
            "<div style='display:flex; width:600px'>" +
            "<input type='submit' value='One' style='background:red; padding:10px'>" +
            "<input type='submit' value='Two' style='background:blue; color:white; padding:10px'>" +
            "</div></body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 100);

        int firstRed = -1, firstBlue = -1;
        for (int y = 0; y < bmp.Height; y++)
        {
            bool hasRed = false, hasBlue = false;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50) hasRed = true;
                if (px.Red < 50 && px.Green < 50 && px.Blue > 200) hasBlue = true;
            }
            if (hasRed && firstRed < 0) firstRed = y;
            if (hasBlue && firstBlue < 0) firstBlue = y;
        }

        Assert.True(Math.Abs(firstRed - firstBlue) < 5,
            $"Submit buttons should be side-by-side. Red at y={firstRed}, blue at y={firstBlue}");
    }

    /// <summary>
    /// Flex children with display:block should use shrink-to-fit sizing,
    /// not expand to the full container width.
    /// </summary>
    [Fact]
    public void FlexChild_DisplayBlock_NotFullWidth()
    {
        var html = "<html><body style='margin:0'>" +
            "<div style='display:flex; width:600px'>" +
            "<div style='display:block; background:#f0f0f0; padding:5px'>Short</div>" +
            "</div></body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 60);

        // Find the gray button extent
        int maxRight = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 && px.Green < 250 && px.Blue < 250)
                    maxRight = Math.Max(maxRight, x);
            }
        }

        // The content "Short" (~30px) plus padding should be << 600px
        Assert.True(maxRight < 300,
            $"Flex child should shrink-to-fit, not expand to 600px. maxRight={maxRight}");
    }

    /// <summary>
    /// Grid children with display:block should also use content sizing.
    /// </summary>
    [Fact]
    public void GridChild_UsesContentSizing()
    {
        var html = "<html><body style='margin:0'>" +
            "<div style='display:grid; width:600px'>" +
            "<div style='display:block; background:#f0f0f0; padding:5px'>Grid item</div>" +
            "</div></body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 60);

        int maxRight = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 && px.Green < 250 && px.Blue < 250)
                    maxRight = Math.Max(maxRight, x);
            }
        }

        Assert.True(maxRight < 300,
            $"Grid child should use content sizing, not full 600px. maxRight={maxRight}");
    }
}
