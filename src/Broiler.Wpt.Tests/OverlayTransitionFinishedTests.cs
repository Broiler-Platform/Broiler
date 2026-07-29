using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for CSS Position 4 §overlay transition completion (WPT
/// <c>css/css-position/overlay/overlay-transition-finished</c>).
///
/// A popover shown with a discrete <c>overlay</c> transition is held out of the top layer while the
/// transition runs — but Broiler never modelled the transition <em>finishing</em>, so it was held
/// out forever and rendered in normal flow beneath the page's fixed red box. The reference is a
/// plain green viewport. <c>ScheduleOverlayTransitionCompletion</c> now schedules the transition's
/// end on the event loop, so a page that drains it (the WPT runner, and this test) sees the popover
/// on top; a render taken without draining timers still sees the transition running, which is what
/// <c>OverlayTransitionRenderTests</c> pins.
/// </summary>
public class OverlayTransitionFinishedTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private const int W = 200;
    private const int H = 200;

    [Fact]
    public void OverlayTransition_Finished_PopoverPaintsOnTopOfViewport()
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-overlayfin-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            const string html = """
<!DOCTYPE html>
<html class="reftest-wait">
<style>
  #transition-in, #red { display: block; border: none; width: 100vw; height: 100vh; }
  #red { position: fixed; background-color: red; }
  #transition-in {
    transition: overlay 0.1s step-end;
    transition-behavior: allow-discrete;
    background-color: green;
  }
</style>
<div id="transition-in" popover></div>
<div id="red"></div>
<script>
  window.takeScreenshot = function () { document.documentElement.classList.remove('reftest-wait'); };
  let t = document.querySelector('#transition-in');
  t.addEventListener('transitionend', () => takeScreenshot());
  t.offsetTop;
  t.showPopover();
  if (getComputedStyle(t).overlay != 'none') {
    // Transition didn't start — the test's own fail condition (renders pink).
    t.style.backgroundColor = 'pink';
    takeScreenshot();
  }
</script>
</html>
""";
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);

            var runner = new WptTestRunner(W, H);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            int green = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (p.G > 100 && p.R < 100) green++;
                }

            // The reference is <body style="background-color:green"> — a plain green viewport. The
            // popover must cover it corner to corner, over the fixed red box and with no white
            // margin (which is what an in-flow, held-out popover left behind).
            Assert.Equal(W * H, green);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
