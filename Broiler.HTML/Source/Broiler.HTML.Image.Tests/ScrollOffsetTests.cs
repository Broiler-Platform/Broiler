using System.Drawing;
using System.Linq;
using Broiler.HTML.Core.Core.IR;
using SkiaSharp;
using Xunit;

namespace Broiler.HTML.Image.Tests;

public class ScrollOffsetTests
{
    /// <summary>
    /// When ScrollOffset is set, display list items should be shifted by the offset
    /// so that content moves within the viewport when scrolling.
    /// </summary>
    [Fact]
    public void ScrollOffset_Shifts_DisplayList_Items()
    {
        string html = "<html><body><p>Hello World</p></body></html>";

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html);
        container.MaxSize = new SizeF(800, 600);

        using var bitmap = new SKBitmap(800, 600);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 800, 600);
        container.PerformLayout(canvas, clip);

        // Render without scroll to get baseline positions
        container.PerformPaint(canvas, clip);
        var noScrollList = container.HtmlContainerInt.LatestDisplayList;
        Assert.NotNull(noScrollList);
        var noScrollTexts = noScrollList.Items.OfType<DrawTextItem>().ToList();
        Assert.True(noScrollTexts.Count > 0, "Expected text items without scroll");
        float baselineY = noScrollTexts[0].Origin.Y;

        // Now apply scroll offset (scroll down 50px → offset = (0, -50))
        container.HtmlContainerInt.ScrollOffset = new PointF(0, -50);
        canvas.Clear(SKColors.White);
        container.PerformPaint(canvas, clip);

        var scrolledList = container.HtmlContainerInt.LatestDisplayList;
        Assert.NotNull(scrolledList);
        var scrolledTexts = scrolledList.Items.OfType<DrawTextItem>().ToList();
        Assert.True(scrolledTexts.Count > 0, "Expected text items with scroll");

        // Text should be shifted up by 50px (the scroll offset)
        float scrolledY = scrolledTexts[0].Origin.Y;
        Assert.Equal(baselineY - 50, scrolledY, 1);
    }

    /// <summary>
    /// When ScrollOffset is zero, display list items should not be modified.
    /// </summary>
    [Fact]
    public void ZeroScrollOffset_Does_Not_Shift_Items()
    {
        string html = "<html><body><p>Test</p></body></html>";

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html);
        container.MaxSize = new SizeF(800, 600);

        using var bitmap = new SKBitmap(800, 600);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 800, 600);
        container.PerformLayout(canvas, clip);

        // Render twice with zero scroll to verify consistency
        container.PerformPaint(canvas, clip);
        var firstRender = container.HtmlContainerInt.LatestDisplayList;
        var firstTexts = firstRender.Items.OfType<DrawTextItem>().ToList();

        canvas.Clear(SKColors.White);
        container.HtmlContainerInt.ScrollOffset = new PointF(0, 0);
        container.PerformPaint(canvas, clip);
        var secondRender = container.HtmlContainerInt.LatestDisplayList;
        var secondTexts = secondRender.Items.OfType<DrawTextItem>().ToList();

        Assert.Equal(firstTexts.Count, secondTexts.Count);
        for (int i = 0; i < firstTexts.Count; i++)
        {
            Assert.Equal(firstTexts[i].Origin.Y, secondTexts[i].Origin.Y, 1);
        }
    }
}
