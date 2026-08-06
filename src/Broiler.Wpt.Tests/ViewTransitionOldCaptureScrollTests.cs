using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// The old view-transition capture is taken in the snapshot containing block, i.e. with the page
/// scroll subtracted — found while diagnosing issue #1538's `massive-element-*` family.
///
/// Both captures call the same <c>GetBoundingClientRectForDomElement</c>, but at different moments
/// against different layouts, and only one of them has the scroll folded in: the new capture runs on
/// the render projection, where the scroll is already baked into box positions, while the old one
/// runs during script, where it is not. A page that scrolls and *then* starts a transition therefore
/// captured its old geometry unscrolled — measured on the WPT test as <c>old=(8,8,…)</c> against
/// <c>new=(-38986,8,…)</c> — and the group carrying <c>::view-transition-old</c> was placed a whole
/// scroll offset away.
/// </summary>
public class ViewTransitionOldCaptureScrollTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    /// <summary>
    /// A page taller than the viewport with a named 100x100 band at its very end. The transition
    /// paints only the band's group, in green: on screen when the capture was converted to the
    /// snapshot containing block, and scrolled off the bottom when it was not.
    /// <para>
    /// Rendered at the runner's default 1024x768. A smaller viewport does not work: the scroll
    /// metrics resolve against the default size rather than the configured one, so `vh` lengths and
    /// the maximum scroll offset disagree with the canvas. That is a separate defect — see the
    /// note in docs/wpt-rendering-gaps.md — not something this test should paper over.
    /// </para>
    /// </summary>
    private static int GreenPixels(string script, string bandPosition = "", int w = 1024, int h = 768)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-vtscroll-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string html =
                "<!DOCTYPE html><html><head><style>" +
                "body { margin: 0 }" +
                "#band { width: 100px; height: 100px; background: red; view-transition-name: band; " + bandPosition + " }" +
                "::view-transition-group(*) { animation-play-state: paused }" +
                "::view-transition-group(band) { background: green }" +
                "::view-transition-image-pair(*), ::view-transition-old(*), ::view-transition-new(*) { display: none }" +
                "::view-transition-group(root), ::view-transition-old(root), ::view-transition-new(root) { display: none }" +
                "</style></head><body>" +
                "<div style='height:400vh'></div><div id='band'></div>" +
                "<script>" + script + " document.startViewTransition(() => {});</script>" +
                "</body></html>";
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);

            var runner = new WptTestRunner(w, h);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            int green = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (p.G > 100 && p.R < 100 && p.B < 100) green++;
                }

            return green;
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public void TheOldCaptureFollowsAScrollTakenBeforeTheTransition()
    {
        // Scrolled to the end, the band is on screen, so its group must be too.
        int green = GreenPixels("window.scrollTo(0, 1e9);");

        Assert.True(green > 0, "the old capture's group was placed off-screen — the page scroll was not subtracted");
    }

    [Fact]
    public void WithNoScrollTheCaptureIsUnchanged()
    {
        // The conversion must be a no-op at scroll offset 0: the band sits past a 400vh spacer, so
        // it is below the viewport and its group must be too.
        int green = GreenPixels(string.Empty);

        Assert.Equal(0, green);
    }

    [Fact]
    public void AFixedPositionElementIsNotAdjusted()
    {
        // A fixed element does not move with the page, so its document coordinates are already
        // viewport coordinates — subtracting the scroll would push it off by the scroll amount.
        // Pinned at the top-left, it is on screen both before and after scrolling.
        int scrolled = GreenPixels("window.scrollTo(0, 1e9);", "position: fixed; top: 0; left: 0;");
        int unscrolled = GreenPixels(string.Empty, "position: fixed; top: 0; left: 0;");

        Assert.True(unscrolled > 0, "a fixed band should be on screen unscrolled");
        Assert.Equal(unscrolled, scrolled);
    }
}
