using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Phase 2 golden layout tests. Each test:
///   1. Renders a small HTML document via <see cref="HtmlContainer"/>.
///   2. Extracts the <see cref="Fragment"/> tree.
///   3. Serialises it via <see cref="FragmentJsonDumper"/>.
///   4. Runs <see cref="LayoutInvariantChecker"/> on the tree.
///   5. Compares the JSON against a committed golden file.
///
/// To re-baseline: delete the golden file and run the test. A new baseline
/// is written automatically and the test is marked as inconclusive.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Rendering")]
[Trait("Engine", "HtmlRenderer")]
public class GoldenLayoutTests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    // ── Test cases ─────────────────────────────────────────────────

    /// <summary>1. Single block element with explicit width/height.</summary>
    [Fact]
    public void SingleBlock_ExplicitSize()
    {
        const string html = "<div style='width:200px;height:100px;'></div>";
        AssertGoldenLayout(html);
    }

    /// <summary>2. Nested blocks with padding and border.</summary>
    [Fact]
    public void NestedBlocks_PaddingBorder()
    {
        const string html =
            @"<div style='width:300px;padding:10px;border:2px solid black;'>
                <div style='width:100px;height:50px;padding:5px;border:1px solid gray;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>3. Two side-by-side left floats.</summary>
    [Fact]
    public void TwoLeftFloats_SideBySide()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='float:left;width:100px;height:50px;'></div>
                <div style='float:left;width:150px;height:50px;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>4. Left float with text wrap-around.</summary>
    [Fact]
    public void LeftFloat_TextWrap()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='float:left;width:80px;height:60px;'></div>
                <span>Text that should wrap around the floated element on the left side.</span>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>5. Float with clear: both.</summary>
    [Fact]
    public void Float_ClearBoth()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='float:left;width:100px;height:50px;'></div>
                <div style='clear:both;width:200px;height:30px;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>6. Percentage width on nested element.</summary>
    [Fact]
    public void PercentageWidth_Nested()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:50%;height:40px;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>7. Inline elements on a single line.</summary>
    [Fact]
    public void InlineElements_SingleLine()
    {
        const string html =
            @"<div style='width:400px;'>
                <span>Hello</span> <span>World</span>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>8. Multiple lines with word wrap.</summary>
    [Fact]
    public void MultipleLines_WordWrap()
    {
        const string html =
            @"<div style='width:100px;'>
                The quick brown fox jumps over the lazy dog.
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>9. Margin collapse between siblings.</summary>
    [Fact]
    public void MarginCollapse_Siblings()
    {
        const string html =
            @"<div style='width:300px;'>
                <div style='margin-bottom:20px;height:30px;'></div>
                <div style='margin-top:15px;height:30px;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    /// <summary>10. Block formatting context containing floats.</summary>
    [Fact]
    public void BFC_ContainingFloats()
    {
        const string html =
            @"<div style='width:400px;overflow:hidden;'>
                <div style='float:left;width:100px;height:80px;'></div>
                <div style='float:right;width:100px;height:60px;'></div>
              </div>";
        AssertGoldenLayout(html);
    }

    // ── Infrastructure ─────────────────────────────────────────────

    private static void AssertGoldenLayout(string html, [CallerMemberName] string testName = "")
    {
        // 1. Render and extract Fragment tree
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);

        // 2. Run invariant checker
        LayoutInvariantChecker.AssertValid(fragment);

        // 3. Serialise to JSON
        var actualJson = FragmentJsonDumper.ToJson(fragment);

        // 4. Compare against golden file
        var goldenPath = Path.Combine(GoldenDir, $"{testName}.json");

        if (!File.Exists(goldenPath))
        {
            // Write new baseline
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.WriteAllText(goldenPath, actualJson);
            // Fail with a clear message — re-run the test to validate against the new baseline.
            // xUnit 2.x does not support Assert.Skip; upgrade to xUnit v3 to use Skip.
            Assert.Fail($"New golden baseline created at {goldenPath}. Re-run to validate.");
        }

        var expectedJson = File.ReadAllText(goldenPath);
        Assert.Equal(expectedJson, actualJson);
    }

    private static Fragment BuildFragmentTree(string html)
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html);

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 500, 500);
        container.PerformLayout(canvas, clip);

        return container.HtmlContainerInt.LatestFragmentTree!;
    }

    private static string GetSourceDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
