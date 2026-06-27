using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression tests for abspos block-axis self-alignment (<c>align-self</c>) in
/// horizontal writing mode, modelled on WPT
/// <c>css/css-align/abspos/align-self-htb-ltr-htb.html</c>.
///
/// Two layout defects were fixed together (see <c>CssBox</c>
/// <c>GetAbsoluteContainingBlockPaddingBox</c> + the abspos self-alignment
/// apply step):
///   1. The containing block's height was unresolved when the abspos child
///      aligned on the block axis (heights resolve bottom-up), so the IMCB
///      height was 0 and the box never moved.
///   2. A non-stretch <c>align-self</c> never shrank the box from the stretched
///      inset height to its content height.
/// </summary>
public class AbsposBlockAxisAlignTests : IDisposable
{
    private readonly string _tempDir;

    public AbsposBlockAxisAlignTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-abspos-diag-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // Vertical span of green pixels along the box's centre column.
    private static (int top, int bottom) GreenBand(BBitmap bmp, int x)
    {
        int top = -1, bottom = -1;
        for (int y = 0; y < 200; y++)
        {
            var p = bmp.GetPixel(x, y);
            if (p.G > 100 && p.R < 100 && p.B < 100)
            {
                if (top < 0) top = y;
                bottom = y;
            }
        }
        return (top, bottom);
    }

    [Theory]
    // align-self value, expected offset-y (from container content box top=24),
    // expected border-box height. Mirrors the data-* assertions in the WPT test.
    [InlineData("start", 0, 20)]
    [InlineData("center", 10, 20)]
    [InlineData("end", 20, 20)]
    [InlineData("stretch", 0, 40)]
    public void Abspos_BlockAxis_AlignSelf_PositionsAndSizesBox(string align, int offsetY, int height)
    {
        // container content box: body margin 0 + container margin 20 + border 4
        // → top/left = 24, content 40×40. item is 20px tall (20×20 ::before),
        // full width (inline axis stretches as justify-self is unset).
        var html = $@"<!DOCTYPE html>
<style>
  body {{ margin: 0; }}
  .container {{ position: relative; display: inline-block; margin: 20px;
               border: solid 4px; width: 40px; height: 40px; }}
  .item {{ position: absolute; inset: 0; background: green; align-self: {align}; }}
  .item::before {{ width: 20px; height: 20px; content: ''; display: block; }}
</style>
<div class='container'><div class='item'></div></div>";
        var file = Path.Combine(_tempDir, "abspos-align.html");
        File.WriteAllText(file, html);

        var runner = new WptTestRunner(320, 240);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, _tempDir);

        var (top, bottom) = GreenBand(bmp, x: 34); // a column inside the box
        int expectedTop = 24 + offsetY;
        int expectedBottom = expectedTop + height;

        Assert.True(top >= 0, $"align-self:{align}: box not painted (no green found).");
        // ±2px tolerance for inclusive pixel scan / antialiasing.
        Assert.True(System.Math.Abs(top - expectedTop) <= 2,
            $"align-self:{align}: green top={top}, expected ~{expectedTop}.");
        Assert.True(System.Math.Abs(bottom - expectedBottom) <= 2,
            $"align-self:{align}: green bottom={bottom}, expected ~{expectedBottom} (height {height}).");
    }
}
