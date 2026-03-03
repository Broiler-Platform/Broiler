using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Phase 4 golden DisplayList (paint-level) tests. Each test:
///   1. Renders a small HTML document via <see cref="HtmlContainer"/>.
///   2. Extracts the <see cref="DisplayList"/> after paint.
///   3. Runs <see cref="PaintInvariantCheckerHelper"/> on the list.
///   4. Serialises it via <see cref="DisplayListJsonDumper"/>.
///   5. Compares the JSON against a committed golden file.
///
/// To re-baseline: delete the golden file and run the test. A new baseline
/// is written automatically and the test is marked as failed (re-run to validate).
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Rendering")]
[Trait("Engine", "HtmlRenderer")]
public class GoldenDisplayListTests
{
    private const int RenderSize = 500;

    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenDisplayList");

    // ── Test cases ─────────────────────────────────────────────────

    /// <summary>1. Single coloured div → FillRectItem only.</summary>
    [Fact]
    public void SingleColoredDiv_FillRect()
    {
        const string html = "<div style='width:100px;height:50px;background:red;'></div>";
        var dl = BuildDisplayList(html);

        Assert.Contains(dl.Items, i => i is FillRectItem);

        AssertGoldenDisplayList(dl);
    }

    /// <summary>2. Div with border → FillRectItem + DrawBorderItem.</summary>
    [Fact]
    public void DivWithBorder_FillRectAndDrawBorder()
    {
        const string html = "<div style='width:100px;height:50px;background:blue;border:2px solid black;'></div>";
        var dl = BuildDisplayList(html);

        Assert.Contains(dl.Items, i => i is FillRectItem);
        Assert.Contains(dl.Items, i => i is DrawBorderItem);

        AssertGoldenDisplayList(dl);
    }

    /// <summary>3. Text paragraph → DrawTextItem entries.</summary>
    [Fact]
    public void TextParagraph_DrawTextItems()
    {
        const string html = "<p style='width:200px;color:black;'>Hello World</p>";
        var dl = BuildDisplayList(html);

        Assert.Contains(dl.Items, i => i is DrawTextItem);

        AssertGoldenDisplayList(dl);
    }

    /// <summary>4. Nested div with overflow:hidden → ClipItem / RestoreItem pair.</summary>
    [Fact]
    public void OverflowHidden_ClipAndRestore()
    {
        const string html =
            @"<div style='width:100px;height:50px;overflow:hidden;'>
                <div style='width:200px;height:100px;background:green;'></div>
              </div>";
        var dl = BuildDisplayList(html);

        Assert.Contains(dl.Items, i => i is ClipItem);
        Assert.Contains(dl.Items, i => i is RestoreItem);

        AssertGoldenDisplayList(dl);
    }

    /// <summary>5. Underlined text → DrawLineItem for text-decoration.</summary>
    [Fact]
    public void UnderlinedText_DrawLineItem()
    {
        const string html = "<span style='text-decoration:underline;'>Underlined</span>";
        var dl = BuildDisplayList(html);

        Assert.Contains(dl.Items, i => i is DrawTextItem);

        AssertGoldenDisplayList(dl);
    }

    /// <summary>6. Float with background → correct paint-order position.</summary>
    [Fact]
    public void FloatWithBackground_PaintOrder()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='float:left;width:80px;height:60px;background:orange;'></div>
                <span>Text around the float.</span>
              </div>";
        var dl = BuildDisplayList(html);

        Assert.Contains(dl.Items, i => i is FillRectItem);
        Assert.Contains(dl.Items, i => i is DrawTextItem);

        AssertGoldenDisplayList(dl);
    }

    /// <summary>7. Border with radius → DrawBorderItem with corner radii.</summary>
    [Fact]
    public void BorderWithRadius_CornerRadii()
    {
        const string html =
            "<div style='width:100px;height:100px;border:3px solid red;border-radius:10px;background:yellow;'></div>";
        var dl = BuildDisplayList(html);

        Assert.Contains(dl.Items, i => i is DrawBorderItem);

        AssertGoldenDisplayList(dl);
    }

    /// <summary>8. Multiple nested blocks produce deterministic output.</summary>
    [Fact]
    public void NestedBlocks_DeterministicOutput()
    {
        const string html =
            @"<div style='width:200px;padding:5px;background:#eee;'>
                <div style='width:100px;height:30px;background:red;'></div>
                <div style='width:100px;height:30px;background:blue;'></div>
              </div>";
        var dl = BuildDisplayList(html);

        var fillRects = dl.Items.OfType<FillRectItem>().ToList();
        Assert.True(fillRects.Count >= 3, "Expected at least 3 FillRectItems (parent + 2 children)");

        AssertGoldenDisplayList(dl);
    }

    /// <summary>9. Div with both padding and border → correct bounds.</summary>
    [Fact]
    public void PaddingAndBorder_CorrectBounds()
    {
        const string html =
            "<div style='width:150px;height:80px;padding:10px;border:2px solid gray;background:lightblue;'></div>";
        var dl = BuildDisplayList(html);

        Assert.Contains(dl.Items, i => i is FillRectItem);
        Assert.Contains(dl.Items, i => i is DrawBorderItem);

        AssertGoldenDisplayList(dl);
    }

    /// <summary>10. Text with colour and font-size metadata.</summary>
    [Fact]
    public void TextWithColorAndFontSize()
    {
        const string html =
            "<div style='width:200px;color:navy;font-size:18px;'>Styled text</div>";
        var dl = BuildDisplayList(html);

        var textItem = dl.Items.OfType<DrawTextItem>().FirstOrDefault();
        Assert.NotNull(textItem);
        Assert.False(string.IsNullOrEmpty(textItem.Text));

        AssertGoldenDisplayList(dl);
    }

    // ── Infrastructure ─────────────────────────────────────────────

    private static void AssertGoldenDisplayList(DisplayList displayList, [CallerMemberName] string testName = "")
    {
        // 1. Run paint invariant checker
        PaintInvariantCheckerHelper.AssertValid(displayList);

        // 2. Serialise to JSON
        var actualJson = displayList.ToJson();

        // 3. Compare against golden file
        var goldenPath = Path.Combine(GoldenDir, $"{testName}.json");

        if (!File.Exists(goldenPath))
        {
            // Write new baseline
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.WriteAllText(goldenPath, actualJson);
            Assert.Fail($"New golden baseline created at {goldenPath}. Re-run to validate.");
        }

        var expectedJson = File.ReadAllText(goldenPath);
        Assert.Equal(expectedJson, actualJson);
    }

    private static DisplayList BuildDisplayList(string html)
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html);

        using var bitmap = new SKBitmap(RenderSize, RenderSize);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, RenderSize, RenderSize);
        container.PerformLayout(canvas, clip);
        container.PerformPaint(canvas, clip);

        return container.HtmlContainerInt.LatestDisplayList!;
    }

    private static string GetSourceDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
