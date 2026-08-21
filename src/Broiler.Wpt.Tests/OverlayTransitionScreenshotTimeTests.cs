using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for when a discrete <c>overlay</c> entry transition stops holding a popover out
/// of the top layer (issue #1538 problem 21 — WPT
/// <c>css/css-position/overlay/overlay-transition-finished</c>).
///
/// The CSSOM answer and the painted answer are taken at different instants. That test reads
/// <c>getComputedStyle(el).overlay</c> right after <c>showPopover()</c> and paints itself pink
/// unless it sees <c>none</c>, then screenshots from <c>transitionend</c> — by which point the
/// popover must be in the top layer. Holding it out for both instants, as
/// <c>PopoverHeldOutByOverlayTransitionIn</c> alone did, makes the test unwinnable.
/// </summary>
public class OverlayTransitionScreenshotTimeTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private static (int Red, int Green, int Pink) Render(string html, int w = 200, int h = 200)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-overlay-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);

            var runner = new WptTestRunner(w, h);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            int red = 0, green = 0, pink = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (p.R > 200 && p.G < 100 && p.B < 100) red++;
                    else if (p.G > 100 && p.R < 100) green++;
                    else if (p.R > 200 && p.G > 150 && p.B > 150) pink++;
                }

            return (red, green, pink);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    // overlay-transition-finished, reduced: a popover whose `overlay` transitions in over 0.1s, a
    // plain fixed red div beneath it, and a screenshot gated on `transitionend`. The popover's own
    // fail-path (pink when the transition did not start) is kept, because it is what pins the
    // script-time half.
    private const string Markup = """
<!DOCTYPE html>
<html class="{{WAIT}}">
<style>
  html, body { margin: 0 }
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
  let el = document.querySelector("#transition-in");
  {{LISTENER}}
  el.offsetTop;
  el.showPopover();
  if (getComputedStyle(el).overlay != "none") {
    el.style.backgroundColor = "pink";
  }
</script>
""";

    private static string Case(string wait, string listener) =>
        Markup.Replace("{{WAIT}}", wait).Replace("{{LISTENER}}", listener);

    [Fact(Timeout = 600000)]
    public void APageWaitingOnTransitionEnd_RendersThePopoverInTheTopLayer()
    {
        var (red, green, pink) = Render(Case(
            "reftest-wait",
            "el.addEventListener('transitionend', () => " +
            "document.documentElement.classList.remove('reftest-wait'));"));

        // The screenshot belongs after the transition, so the popover covers the fixed red div.
        Assert.Equal(0, red);
        Assert.Equal(0, pink);
        Assert.Equal(200 * 200, green);
    }

    [Fact(Timeout = 600000)]
    public void WithNoTransitionEndGate_ThePopoverStaysOutOfTheTopLayer()
    {
        // The same page without the gate is `overlay-transition-in-rendering`'s shape: the screenshot
        // is taken at once, while the transition still holds `overlay: none`, so the popover paints
        // beneath the fixed red div.
        var (red, green, pink) = Render(Case(string.Empty, string.Empty));

        Assert.Equal(0, pink);
        Assert.Equal(200 * 200, red);
        Assert.Equal(0, green);
    }

    [Fact(Timeout = 600000)]
    public void ScriptStillObservesTheTransitionAsRunning()
    {
        // The paint-time move must not reach getComputedStyle: a page that gates on `transitionend`
        // still has to read `overlay: none` synchronously after showPopover(), or the real test
        // paints its pink fail colour before the screenshot is ever taken. No pink in either case
        // above is that assertion; this one states it as its own fact by making pink the only thing
        // that could cover the canvas.
        var (_, _, pink) = Render(Case(
            "reftest-wait",
            "el.addEventListener('transitionend', () => {});"));

        Assert.Equal(0, pink);
    }

    [Fact(Timeout = 600000)]
    public void AListenerOnAnAncestorAlsoCounts()
    {
        // `transitionend` bubbles, so a listener on the document is as good a gate as one on the
        // popover itself.
        var (red, green, _) = Render(Case(
            "reftest-wait",
            "document.addEventListener('transitionend', () => " +
            "document.documentElement.classList.remove('reftest-wait'));"));

        Assert.Equal(0, red);
        Assert.Equal(200 * 200, green);
    }
}
