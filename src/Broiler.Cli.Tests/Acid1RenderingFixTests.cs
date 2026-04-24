using System.Drawing;
using Broiler.HTML.Image;
using SkiaSharp;

namespace Broiler.Cli.Tests;

public class Acid1RenderingFixTests
{
    // The layout itself preserves the five-line footer wrap at 405px; the
    // full-page bitmap additionally includes the trailing 15px body margin.
    private const float MinExpectedLayoutHeight = 405f;
    private const float MinExpectedFooterHeight = 65f;

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
        Assert.True(container.ActualSize.Height >= MinExpectedLayoutHeight,
            $"Expected Acid1 full-page layout height to preserve the footer wrap (got {container.ActualSize.Height}).");
        Assert.True(footerRect.Value.Height >= MinExpectedFooterHeight,
            $"Expected Acid1 footer text to wrap to at least five lines (got {footerRect.Value.Height}px).");
    }

    [Fact]
    public void Acid1_FullPage_AutoSized_Render_Includes_Trailing_Body_Margin()
    {
        var htmlPath = Path.Combine(RepoRoot, "acid", "acid1", "acid1.html");
        Assert.True(File.Exists(htmlPath), $"Acid1 fixture not found at {htmlPath}");

        var html = File.ReadAllText(htmlPath);

        using var bitmap = HtmlRender.RenderToImageAutoSized(html, maxWidth: 1024, maxHeight: 768);

        Assert.Equal(520, bitmap.Width);
        Assert.Equal(420, bitmap.Height);
    }

    [Fact]
    public void Inline_Form_With_Block_Paragraphs_Lays_Out_Radio_Controls()
    {
        var htmlPath = Path.Combine(RepoRoot, "acid", "acid1", "acid1.html");
        Assert.True(File.Exists(htmlPath), $"Acid1 fixture not found at {htmlPath}");

        var html = File.ReadAllText(htmlPath)
            .Replace(
                "<form action=\"https://www.w3.org/Style/CSS/Test/CSS1/current/\" method=\"get\">",
                "<form id=\"form\" action=\"https://www.w3.org/Style/CSS/Test/CSS1/current/\" method=\"get\">")
            .Replace(
                "<input type=\"radio\" name=\"foo\" value=\"off\">",
                "<input id=\"r1\" type=\"radio\" name=\"foo\" value=\"off\">")
            .Replace(
                "<input type=\"radio\" name=\"foo2\" value=\"on\">",
                "<input id=\"r2\" type=\"radio\" name=\"foo2\" value=\"on\">");

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html);
        container.MaxSize = new SizeF(520, 0);

        using var bitmap = new SKBitmap(1, 1);
        using var canvas = new SKCanvas(bitmap);
        container.PerformLayout(canvas, new RectangleF(0, 0, 520, 99999));

        var formRect = container.GetElementRectangle("form");
        var radio1Rect = container.GetElementRectangle("r1");
        var radio2Rect = container.GetElementRectangle("r2");

        Assert.True(formRect.HasValue && formRect.Value.Height >= 38,
            $"Expected the inline form to expand around its block paragraphs (got {formRect}).");
        Assert.True(radio1Rect.HasValue && radio1Rect.Value.Width == 13 && radio1Rect.Value.Height == 13,
            $"Expected the first radio to keep Chromium-like 13×13 bounds (got {radio1Rect}).");
        Assert.True(radio2Rect.HasValue && radio2Rect.Value.Width == 13 && radio2Rect.Value.Height == 13,
            $"Expected the second radio to keep Chromium-like 13×13 bounds (got {radio2Rect}).");
        Assert.True(radio2Rect.Value.Y > radio1Rect.Value.Y,
            $"Expected the second radio to appear below the first one (got r1={radio1Rect}, r2={radio2Rect}).");
    }
}
