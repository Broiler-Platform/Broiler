using System;
using System.Drawing;
using System.Linq;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;
using TheArtOfDev.HtmlRenderer.Image.Adapters;
using Xunit;

namespace HtmlRenderer.Tests;

public class TextRenderTest
{
    [Fact]
    public void WelcomeToBroiler_TextNotReversed()
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

        // Collect all text from display list
        var allText = string.Join("", textItems.Select(t => t.Text));
        Console.WriteLine($"All text from display list: '{allText}'");
        
        foreach (var item in textItems)
        {
            Console.WriteLine($"  Text: '{item.Text}' IsRtl={item.IsRtl}");
            // Check that no word is reversed
            Assert.DoesNotContain("emocleW", item.Text);
            Assert.DoesNotContain("reliorB", item.Text);
        }

        // The full text should contain "Welcome" and "Broiler" (not reversed)
        Assert.Contains("Welcome", allText);
        Assert.Contains("Broiler", allText);
    }
}
