using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression tests for the inline-background paint-order fix (CSS2.1 Appendix E).
///
/// <para>The glyphs of every inline box on a line are emitted in one pass from the
/// containing block's line boxes (<c>PaintWalker.EmitText</c>); a
/// <c>display:inline</c> child fragment carries only its own background/border.
/// Painted in the normal child phase (Step 5) those backgrounds landed <em>on top
/// of</em> the already-emitted text — so a coloured <c>&lt;span&gt;</c> with a
/// background rendered as a solid background rectangle with its text completely
/// hidden. The fix emits inline-box backgrounds/borders before the block's text
/// (<c>EmitInlineLevelBoxDecorations</c>) and suppresses re-emission in Step 5.</para>
/// </summary>
public class InlineBackgroundPaintOrderTests : IDisposable
{
    private readonly string _tempDir;

    public InlineBackgroundPaintOrderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-inline-bg-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // Renders the body and counts pixels matching the text colour (blue) and the
    // background colour (red), scanning the whole bitmap.
    private (int textPixels, int bgPixels) CountColors(string body)
    {
        var html = $@"<!DOCTYPE html>
<style>
  body {{ margin: 0; font-family: monospace; font-size: 48px; }}
  .s {{ color: blue; background: red; }}
</style>
<body>{body}</body>";
        var file = Path.Combine(_tempDir, "t-" + Guid.NewGuid().ToString("N")[..6] + ".html");
        File.WriteAllText(file, html);

        var runner = new WptTestRunner(400, 200);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, _tempDir);

        int text = 0, bg = 0;
        for (int y = 0; y < 200; y++)
        for (int x = 0; x < 400; x++)
        {
            var p = bmp.GetPixel(x, y);
            // Blue text: B dominant, R/G low.
            if (p.B > 150 && p.R < 100 && p.G < 100) text++;
            // Red background: R dominant, G/B low.
            else if (p.R > 150 && p.G < 100 && p.B < 100) bg++;
        }
        return (text, bg);
    }

    [Fact]
    public void InlineSpanBackground_PaintsBehindText_TextRemainsVisible()
    {
        var (textPixels, bgPixels) = CountColors("<span class=\"s\">WWWWWW</span>");

        // The background must still be visible around the glyphs.
        Assert.True(bgPixels > 0, "inline span background (red) should be painted");

        // The key regression: the text colour (blue) must show through. Before the
        // fix the inline background painted over the glyphs, leaving ZERO blue
        // pixels (a solid red rectangle).
        Assert.True(textPixels > 0,
            $"inline span text (blue) is hidden behind its own background — " +
            $"text pixels={textPixels}, bg pixels={bgPixels}; the inline-background " +
            $"paint-order regressed.");
    }
}
