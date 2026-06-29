using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Reproduces WPT css/css-align/abspos/align-self-static-position-001.html
/// (block-container family) to drive the abspos *static-position* self-alignment
/// model in CssBox. When an abspos box has AUTO insets on an axis, align-self /
/// justify-self aligns it within its static-position rectangle rather than the
/// inset-modified containing block.
/// </summary>
public class AbsposStaticPositionAlignTests : IDisposable
{
    private readonly string _tempDir;

    public AbsposStaticPositionAlignTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-abspos-static-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // Bounding box of purple (R&B high, G low) pixels, or null if none.
    private static (int left, int top, int right, int bottom)? PurpleBox(BBitmap bmp)
    {
        int l = int.MaxValue, t = int.MaxValue, r = -1, b = -1;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var p = bmp.GetPixel(x, y);
            if (p.R > 100 && p.B > 100 && p.G < 90)
            {
                if (x < l) l = x;
                if (x > r) r = x;
                if (y < t) t = y;
                if (y > b) b = y;
            }
        }
        return r < 0 ? null : (l, t, r, b);
    }

    private BBitmap Render(string inlineClass, string blockClass, string alignClass)
    {
        // body margin 0; container pushed to (80,80) so negative static offsets
        // stay on-screen. container content origin = (81,81); .block content
        // origin (static position) = (86,86); .block content width = 75.
        var html = $@"<!DOCTYPE html>
<style>
  body {{ margin: 0; }}
  .block {{ width: 75px; height: 50px; border: 5px dotted blue; }}
  .container {{ border: 1px solid; position: relative; width: 100px; height: 100px;
                display: inline-block; margin-top: 80px; margin-left: 80px; }}
  .abs {{ width: 50px; height: 50px; position: absolute; background: purple; }}
  .static-positioned-inline {{ left: auto; right: auto; }}
  .static-positioned-block {{ top: auto; bottom: auto; }}
  .positioned-inline {{ left: 0; right: 0; }}
  .positioned-block {{ top: 0; bottom: 0; }}
  .center {{ justify-self: center; align-self: center; }}
  .end {{ justify-self: end; align-self: end; }}
  .start {{ justify-self: start; align-self: start; }}
</style>
<div class='container'><div class='block'>
  <div class='abs {inlineClass} {blockClass} {alignClass}'></div>
</div></div>";
        var file = Path.Combine(_tempDir, $"static-{inlineClass}-{blockClass}-{alignClass}.html");
        File.WriteAllText(file, html);
        var runner = new WptTestRunner(320, 240);
        return runner.RenderHtmlFileBitmapPublic(file, _tempDir);
    }

    // inline class, block class, align class, expected box top-left (page px).
    //
    // Layout: container content origin (81,81); .block content origin — the abs
    // box's static position — (86,86); .block content width 75; .abs is 50×50.
    //   • A POSITIONED axis (inset:0) → inset-modified CB = container content
    //     [81,181], free 50 → start 81 / center 106 / end 131.
    //   • The STATIC INLINE axis → static-position rect = parent content
    //     [86,161], free 25 → start 86 / center 98.5 / end 111.
    //   • The STATIC BLOCK axis → zero-size rect at the static position 86
    //     (free = −50) → start 86 / center 61 / end 36.
    // These mirror the per-axis offsets of WPT align-self-static-position-001.
    [Theory]
    [InlineData("positioned-inline", "static-positioned-block", "start", 81, 86)]
    [InlineData("positioned-inline", "static-positioned-block", "center", 106, 61)]
    [InlineData("positioned-inline", "static-positioned-block", "end", 131, 36)]
    [InlineData("static-positioned-inline", "static-positioned-block", "start", 86, 86)]
    [InlineData("static-positioned-inline", "static-positioned-block", "center", 98, 61)]
    [InlineData("static-positioned-inline", "static-positioned-block", "end", 111, 36)]
    [InlineData("static-positioned-inline", "positioned-block", "start", 86, 81)]
    [InlineData("static-positioned-inline", "positioned-block", "center", 98, 106)]
    [InlineData("static-positioned-inline", "positioned-block", "end", 111, 131)]
    public void AbsposStaticPosition_AlignsWithinStaticPositionRect(
        string inlineClass, string blockClass, string alignClass, int expLeft, int expTop)
    {
        using var bmp = Render(inlineClass, blockClass, alignClass);
        var box = PurpleBox(bmp);

        Assert.True(box is not null,
            $"[{inlineClass}|{blockClass}|{alignClass}]: purple box not painted.");
        var (left, top, right, bottom) = box!.Value;

        // ±2px for the inclusive pixel scan / antialiasing.
        Assert.True(System.Math.Abs(left - expLeft) <= 2,
            $"[{inlineClass}|{blockClass}|{alignClass}]: left={left}, expected ~{expLeft}.");
        Assert.True(System.Math.Abs(top - expTop) <= 2,
            $"[{inlineClass}|{blockClass}|{alignClass}]: top={top}, expected ~{expTop}.");
        // The box must keep its full 50×50 size (a prior bug shrank it by the
        // block-axis offset when the static alignment moved it up).
        Assert.True(System.Math.Abs((right - left) - 49) <= 2,
            $"[{inlineClass}|{blockClass}|{alignClass}]: width={right - left + 1}, expected ~50.");
        Assert.True(System.Math.Abs((bottom - top) - 49) <= 2,
            $"[{inlineClass}|{blockClass}|{alignClass}]: height={bottom - top + 1}, expected ~50.");
    }
}
