using Broiler.HTML.Image;
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
/// 4. flex-direction:column stacks items vertically.
/// 5. justify-content:center centers items horizontally.
/// 6. max-width constrains inline-block/flex item width.
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

    /// <summary>
    /// flex-direction:column should stack children vertically.
    /// Each child should be content-sized (shrink-to-fit width).
    /// </summary>
    [Fact]
    public void FlexDirectionColumn_StacksVertically()
    {
        var html = "<html><body style='margin:0'>" +
            "<div style='display:flex; flex-direction:column; width:600px'>" +
            "<div style='background:red; padding:5px'><span>First</span></div>" +
            "<div style='background:blue; padding:5px; color:white'><span>Second</span></div>" +
            "</div></body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 80);

        int firstRed = -1, lastRed = -1, firstBlue = -1;
        for (int y = 0; y < bmp.Height; y++)
        {
            bool hasRed = false, hasBlue = false;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50) hasRed = true;
                if (px.Red < 50 && px.Green < 50 && px.Blue > 200) hasBlue = true;
            }
            if (hasRed)
            {
                if (firstRed < 0) firstRed = y;
                lastRed = y;
            }
            if (hasBlue && firstBlue < 0) firstBlue = y;
        }

        // Blue should start AFTER red ends (stacked vertically)
        Assert.True(firstBlue > lastRed,
            $"flex-direction:column should stack items. Red ends at y={lastRed}, blue starts at y={firstBlue}");
    }

    /// <summary>
    /// justify-content:center should center flex items horizontally
    /// within the flex container.
    /// </summary>
    [Fact]
    public void JustifyContentCenter_CentersItems()
    {
        var html = "<html><body style='margin:0'>" +
            "<div style='display:flex; justify-content:center; width:600px'>" +
            "<input type='submit' value='OK' style='background:#f0f0f0'>" +
            "</div></body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 40);

        int btnLeft = bmp.Width, btnRight = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red == 0xF0 && px.Green == 0xF0 && px.Blue == 0xF0)
                {
                    if (x < btnLeft) btnLeft = x;
                    if (x > btnRight) btnRight = x;
                }
            }
        }

        int center = (btnLeft + btnRight) / 2;
        // Button should be centered around 300px (half of 600px container)
        Assert.True(Math.Abs(center - 300) < 30,
            $"justify-content:center should center button around 300px (center={center})");
    }

    /// <summary>
    /// max-width should constrain an inline-block or flex item's width.
    /// </summary>
    [Fact]
    public void MaxWidth_CapsFlexItemWidth()
    {
        var html = "<html><body style='margin:0'>" +
            "<div style='display:flex; width:600px'>" +
            "<input type='submit' value='This is a long button label' style='max-width:100px; background:#f0f0f0'>" +
            "</div></body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 40);

        int left = bmp.Width, right = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red == 0xF0 && px.Green == 0xF0 && px.Blue == 0xF0)
                {
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
            }
        }

        int width = right - left + 1;
        Assert.True(width <= 105,
            $"max-width:100px should cap button at ~100px (got {width}px)");
    }

    /// <summary>
    /// Google-like layout: flex container with column direction,
    /// centered buttons should render as stacked, centered, content-sized items.
    /// </summary>
    [Fact]
    public void GoogleLike_FlexColumnCenteredButtons()
    {
        var html = @"<html><body style='margin:0'>
<div style='display:flex; flex-direction:column; align-items:center; justify-content:center; width:600px'>
    <input type='submit' value='Google Suche' style='background:#f8f9fa; padding:0 16px; height:36px; margin:4px'>
    <input type='submit' value='Auf gut Glueck!' style='background:#f8f9fa; padding:0 16px; height:36px; margin:4px'>
</div>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 120);

        // Find the extent of button backgrounds (#F8F9FA)
        int btnLeft = bmp.Width, btnRight = 0;
        int firstBtnY = -1, lastBtnY = -1;
        int gapCount = 0;
        bool inBtn = false;

        for (int y = 0; y < bmp.Height; y++)
        {
            bool hasBtn = false;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red >= 0xF5 && px.Green >= 0xF5 && px.Blue >= 0xF5
                    && (px.Red < 0xFF || px.Green < 0xFF || px.Blue < 0xFF))
                {
                    hasBtn = true;
                    if (x < btnLeft) btnLeft = x;
                    if (x > btnRight) btnRight = x;
                }
            }
            if (hasBtn)
            {
                if (firstBtnY < 0) firstBtnY = y;
                lastBtnY = y;
                if (!inBtn && gapCount > 0) gapCount++; // second button
                inBtn = true;
            }
            else if (inBtn)
            {
                inBtn = false;
                gapCount++;
            }
        }

        int width = btnRight - btnLeft + 1;
        // Buttons should be content-sized (not full 600px)
        Assert.True(width < 400,
            $"Buttons should be content-sized, not full width (width={width})");

        // Buttons should be approximately centered
        int center = (btnLeft + btnRight) / 2;
        Assert.True(Math.Abs(center - 300) < 50,
            $"Buttons should be centered around 300px (center={center})");
    }
}
