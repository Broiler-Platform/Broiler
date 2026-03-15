using System.Drawing;
using System.Linq;
using Broiler.HTML.Core.Core.IR;
using SkiaSharp;
using Xunit;

namespace Broiler.HTML.Image.Tests;

public class TextRenderTest
{
    [Fact]
    public void LtrText_IsNotMarkedRtl()
    {
        string html = "<html><body><h1>Welcome to Broiler</h1></body></html>";

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
        container.PerformPaint(canvas, clip);

        var displayList = container.HtmlContainerInt.LatestDisplayList;
        Assert.NotNull(displayList);

        var textItems = displayList.Items.OfType<DrawTextItem>().ToList();
        Assert.True(textItems.Count > 0, "Expected at least one text item");

        // The combined text must contain the original words in their correct order
        var allText = string.Join("", textItems.Select(t => t.Text));
        Assert.Contains("Welcome", allText);
        Assert.Contains("Broiler", allText);

        // No text item should be marked RTL for a plain LTR page
        foreach (var item in textItems)
        {
            Assert.False(item.IsRtl,
                $"Text '{item.Text}' should not be marked RTL on a LTR page");
        }
    }
}
