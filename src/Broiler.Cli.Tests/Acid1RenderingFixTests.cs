using System.Drawing;
using Broiler.HTML.Image;
using SkiaSharp;

namespace Broiler.Cli.Tests;

public class Acid1RenderingFixTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void Acid1_FullPage_Layout_Preserves_Footer_Wrap_Height()
    {
        var htmlPath = Path.Combine(RepoRoot, "acid", "acid1", "acid1.html");
        Assert.True(File.Exists(htmlPath), $"Acid1 fixture not found at {htmlPath}");

        var html = File.ReadAllText(htmlPath).Replace(
            "<p style=\"color: black; font-size: 1em; line-height: 1.3em; clear: both\">",
            "<p id=\"footer\" style=\"color: black; font-size: 1em; line-height: 1.3em; clear: both\">");

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html);
        container.MaxSize = new SizeF(520, 0);

        using var bitmap = new SKBitmap(1, 1);
        using var canvas = new SKCanvas(bitmap);
        container.PerformLayout(canvas, new RectangleF(0, 0, 520, 99999));

        var footerRect = container.GetElementRectangle("footer");
        Assert.True(footerRect.HasValue, "Expected to resolve the Acid1 footer paragraph.");
        Assert.True(container.ActualSize.Height >= 405,
            $"Expected Acid1 full-page layout height to preserve the footer wrap (got {container.ActualSize.Height}).");
        Assert.True(footerRect.Value.Height >= 65,
            $"Expected Acid1 footer text to wrap to at least five lines (got {footerRect.Value.Height}px).");
    }
}
