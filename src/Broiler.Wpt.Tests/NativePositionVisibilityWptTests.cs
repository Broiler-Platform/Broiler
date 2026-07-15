using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// End-to-end proof of the Phase 5 native <c>position-visibility</c> cutover (P5.8d.2b) through the
/// full WPT render pipeline. With the runner lever on, the bridge stops resolving
/// <c>position-visibility</c> (no <c>display:none</c> write) and the Broiler.Layout engine's
/// post-pass (<c>CssBox.ResolvePositionVisibility</c>) hides an anchor-positioned target whose
/// anchor is scrolled out of an <em>intervening</em> clip container.
///
/// The two fixtures differ only in the scroll container's authored <c>position</c> — the exact case
/// the bridge's pre-<c>position:relative</c> ordering distinguishes and the engine reproduces via the
/// <c>data-broiler-anchor-cb</c> marker:
/// - a <b>static</b> scroll container is not the target's CB, so its scrolled-out anchor is an
///   intervening clip → the target is <b>hidden</b>;
/// - an authored <b>position:relative</b> scroll container IS the target's CB → no intervening clip →
///   the target is <b>shown</b>, even though the anchor is scrolled out.
/// Both the baked (lever-off) and native (lever-on) paths agree.
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class NativePositionVisibilityWptTests : IDisposable
{
    private readonly bool _previousLever = WptTestRunner.NativeAnchorPlacement;

    public void Dispose()
    {
        WptTestRunner.NativeAnchorPlacement = _previousLever;
        Program.ResetTestHooks();
    }

    // A 100x100 red target anchored (position-area: bottom right) to a 100x100 anchor at the top of a
    // 300x100 scroll container, scrolled down 100px so the anchor is scrolled out. {0} is the scroll
    // container's position.
    private const string Template = @"<!DOCTYPE html><meta charset=""utf-8"">
<style>
  body {{ margin: 0; }}
  #sc {{ overflow: hidden scroll; width: 300px; height: 100px; {0} }}
  #anchor {{ anchor-name: --a1; width: 100px; height: 100px; background: orange; }}
  #spacer {{ height: 100px; }}
  #target {{ position-anchor: --a1; position-visibility: anchors-visible; position-area: bottom right;
             width: 100px; height: 100px; background: #ff0000; position: absolute; top: 0; left: 0; }}
</style>
<div id=""sc""><div id=""anchor"">anchor</div><div id=""spacer""></div><div id=""target"">target</div></div>
<script>document.getElementById('sc').scrollTop = 100;</script>";

    private static int RedCount(BBitmap bmp, int w, int h)
    {
        int count = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.R > 200 && p.G < 80 && p.B < 80)
                    count++;
            }
        return count;
    }

    private static int RenderRedCount(bool nativeAnchor, string scrollContainerPosition)
    {
        WptTestRunner.NativeAnchorPlacement = nativeAnchor;
        string dir = Path.Combine(Path.GetTempPath(), "broiler-native-posvis-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "mvp.html");
            File.WriteAllText(file, string.Format(Template, scrollContainerPosition));
            var runner = new WptTestRunner(400, 400);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);
            return RedCount(bmp, 400, 400);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void NativeLeverOn_StaticScroller_HidesTarget()
    {
        // Intervening clip → hidden. Baked path hides it too, so both agree on "no red".
        Assert.Equal(0, RenderRedCount(nativeAnchor: true, ""));
        Assert.Equal(0, RenderRedCount(nativeAnchor: false, ""));
    }

    [Fact]
    public void NativeLeverOn_RelativeScroller_ShowsTarget()
    {
        // The scroller IS the target's CB → no intervening clip → shown (red present) under both
        // the native and baked paths.
        Assert.True(RenderRedCount(nativeAnchor: true, "position: relative;") > 0,
            "target should be visible under the native lever with a position:relative scroller.");
        Assert.True(RenderRedCount(nativeAnchor: false, "position: relative;") > 0,
            "target should be visible under the baked path with a position:relative scroller.");
    }
}
