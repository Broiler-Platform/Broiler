using System.IO;
using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for the root-scroller programmatic-scroll cluster of issue #1439
/// (<c>css/css-scroll-snap/scroll-snap-root-001</c> and <c>-002</c>, both reported at 0.0%
/// pixel match). Four independent defects stacked up to pin those documents at scroll offset
/// 0 — or, once they scrolled, at the wrong offset:
///
/// <list type="number">
/// <item><description><b>Root <c>overflow: hidden</c> refused every programmatic scroll.</b>
/// <c>DisablesRootScrolling</c> treated <c>hidden</c> like <c>clip</c>, but <c>hidden</c> still
/// establishes a scroll container — it only drops the user-interaction affordance, while
/// <c>scrollTop</c>/<c>scrollIntoView</c> keep working (CSS Overflow 3 §3.3). Both WPT tests use
/// the very common reftest idiom <c>:root { overflow: hidden; /* hide scrollbars */ }</c>.</description></item>
/// <item><description><b><c>scrollHeight</c> dropped trailing margins.</b> The scrollable overflow
/// region is built from descendants' <em>margin</em> boxes (CSS Overflow 3 §3.1); unioning border
/// boxes alone truncated the document by the last child's <c>margin-bottom</c> and clamped scrolls
/// to a max that was too small.</description></item>
/// <item><description><b><c>scroll-padding</c>/<c>scroll-margin</c> shorthands resolved to 0.</b>
/// The style engine keeps them as declared instead of expanding to longhands, and
/// <c>scroll-padding</c>'s percentages had no scrollport basis, so <c>scrollIntoView</c> ignored
/// both insets.</description></item>
/// <item><description><b>No scroll snapping.</b> A <c>mandatory</c> snap container must come to
/// rest on a snap position after any scroll (CSS Scroll Snap 1 §2), which is what makes an
/// <c>end</c>-aligned target land at the scrollport's end edge rather than its start.</description></item>
/// </list>
///
/// <para>A fifth defect surfaced once the offsets were right: the <c>scrollIntoView</c> target
/// size was reconstructed from computed-style strings, which silently dropped the
/// <c>thin</c>/<c>medium</c>/<c>thick</c> border-width keywords and shifted every end/center
/// alignment by the border width.</para>
///
/// <para>The two WPT-mirroring fixtures below assert exact geometry, because the whole point of
/// those tests is that a specific edge lands on a specific pixel row.</para>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class ScrollSnapRootPropagationTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    // The WPT reference PNGs for this cluster are rendered at 1024x768, and both fixtures are
    // written in vh units, so the viewport size is load-bearing for the expected pixel rows.
    private const int W = 1024;
    private const int H = 768;

    private static BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-snap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "t.html");
        File.WriteAllText(file, html);
        try
        {
            var runner = new WptTestRunner(W, H);
            return runner.RenderHtmlFileBitmapPublic(file, dir);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static bool IsRed(BColor p) => p.R > 200 && p.G < 80 && p.B < 80;
    // CSS `green` is #008000, so the green channel sits at 128 — not the 255 a `#0f0` fixture
    // would give. The threshold has to stay below that or every green assertion goes vacuous.
    private static bool IsGreen(BColor p) => p.G > 100 && p.R < 80 && p.B < 80;
    private static bool IsBlue(BColor p) => p.B > 150 && p.R < 80 && p.G < 80;
    private static bool IsOrange(BColor p) => p.R > 200 && p.G is > 120 and < 210 && p.B < 100;

    private static int Count(BBitmap bmp, Func<BColor, bool> match)
    {
        int n = 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (match(bmp.GetPixel(x, y)))
                    n++;
        return n;
    }

    /// <summary>First and last row containing a pixel matching <paramref name="match"/>, or (-1, -1).</summary>
    private static (int First, int Last) RowSpan(BBitmap bmp, Func<BColor, bool> match)
    {
        int first = -1, last = -1;
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (!match(bmp.GetPixel(x, y)))
                    continue;
                if (first < 0)
                    first = y;
                last = y;
                break;
            }
        }
        return (first, last);
    }

    // ---------------------------------------------------------------------
    // 1. Root overflow: hidden is still programmatically scrollable
    // ---------------------------------------------------------------------

    [Fact(Timeout = 600000)]
    public void RootOverflowHidden_StillAcceptsProgrammaticScroll()
    {
        const string html =
            "<!DOCTYPE html><html><head><style>" +
            "html{overflow:hidden} body{margin:0}" +
            ".red{height:100vh;background:red} .green{height:100vh;background:green}" +
            "</style></head><body>" +
            "<div class=\"red\"></div><div class=\"green\"></div>" +
            "<script>document.documentElement.scrollTop = 768;</script>" +
            "</body></html>";

        using var bmp = Render(html);
        Assert.True(Count(bmp, IsGreen) > W * H * 0.95,
            "overflow:hidden root did not scroll; hidden must still allow programmatic scrolling.");
        Assert.True(Count(bmp, IsRed) < W * H * 0.02, "red block still visible after scrolling past it.");
    }

    /// <summary><c>overflow: clip</c> genuinely suppresses the scroll container, so it must stay pinned.</summary>
    [Fact(Timeout = 600000)]
    public void RootOverflowClip_RemainsUnscrollable()
    {
        const string html =
            "<!DOCTYPE html><html><head><style>" +
            "html{overflow:clip} body{margin:0}" +
            ".red{height:100vh;background:red} .green{height:100vh;background:green}" +
            "</style></head><body>" +
            "<div class=\"red\"></div><div class=\"green\"></div>" +
            "<script>document.documentElement.scrollTop = 768;</script>" +
            "</body></html>";

        using var bmp = Render(html);
        Assert.True(Count(bmp, IsRed) > W * H * 0.95,
            "overflow:clip root scrolled; clip suppresses the scroll container entirely.");
    }

    // ---------------------------------------------------------------------
    // 2. Trailing bottom margin is scrollable overflow
    // ---------------------------------------------------------------------

    /// <summary>
    /// The last in-flow child's <c>margin-bottom</c> adds 100vh of scrollable area, so scrolling
    /// to the maximum offset moves the green block fully off-screen. Before the fix the document
    /// ended at the green block's border box, capping the scroll one viewport earlier and leaving
    /// green filling the screen.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ScrollableOverflow_IncludesTrailingBottomMargin()
    {
        const string html =
            "<!DOCTYPE html><html><head><style>" +
            "body{margin:0} .red{height:100vh;background:red}" +
            ".green{height:100vh;background:green;margin-bottom:100vh}" +
            "</style></head><body>" +
            "<div class=\"red\"></div><div class=\"green\"></div>" +
            "<script>document.documentElement.scrollTop = 99999;</script>" +
            "</body></html>";

        using var bmp = Render(html);
        Assert.True(Count(bmp, IsGreen) < W * H * 0.02,
            "green still visible at max scroll; the trailing margin was not scrollable overflow.");
        Assert.True(Count(bmp, IsRed) < W * H * 0.02, "red still visible at max scroll.");
    }

    // ---------------------------------------------------------------------
    // 3. scroll-padding / scroll-margin shorthands (WPT scroll-snap-root-001)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Mirrors <c>css/css-scroll-snap/scroll-snap-root-001</c>. <c>#target</c>'s border box starts
    /// at y=1382.4; <c>scroll-margin: 25vh</c> (192) pulls the snap area start to 1190.4 and the
    /// root's <c>scroll-padding: 25%</c> (192 of the 768px scrollport) pushes the snapport start to
    /// 192, so the scroll lands at 998.4 and the blue border-top paints at 1382.4 - 998.4 = 384 —
    /// the vertical centre the test's own message describes.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ScrollIntoView_HonoursScrollPaddingAndScrollMarginShorthands()
    {
        const string html =
            "<!DOCTYPE html><html><head><style>" +
            "html,body{margin:0;padding:0}" +
            ":root{scroll-snap-type:block mandatory;scroll-padding:25%;overflow:hidden}" +
            "#fail{font:bold 2em;background:red;height:120vh;margin-bottom:60vh}" +
            "#target{margin-bottom:120vh;scroll-margin:25vh;scroll-snap-align:start;border-top:solid blue}" +
            "</style></head><body>" +
            "<div id=\"fail\">FAIL</div>" +
            "<div id=\"target\"><div>Test passes if the blue line above is centered in the viewport.</div></div>" +
            "<script>document.getElementById('target').scrollIntoView();</script>" +
            "</body></html>";

        using var bmp = Render(html);
        Assert.Equal(0, Count(bmp, IsRed));

        var (firstBlue, _) = RowSpan(bmp, IsBlue);
        Assert.InRange(firstBlue, 383, 385);
    }

    // ---------------------------------------------------------------------
    // 4. Mandatory snapping + body scroll-padding must not propagate (WPT -002)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Mirrors <c>css/css-scroll-snap/scroll-snap-root-002</c>. <c>#target</c>'s border box is
    /// 1843.2..1868.2 (20px content + a 5px <c>thick</c> bottom border). <c>scroll-snap-align: end</c>
    /// under a <c>mandatory</c> root snap container aligns that end edge with the scrollport's end,
    /// giving 1868.2 - 768 = 1100.2 and painting the orange stripe on the last rows.
    ///
    /// <para>The <c>scroll-padding: 25%</c> on <c>&lt;body&gt;</c> is the trap the WPT test is named
    /// for: only the root element propagates to the viewport. Honouring body's would shorten the
    /// snapport to 576 and push the stripe off-screen.</para>
    /// </summary>
    [Fact(Timeout = 600000)]
    public void MandatorySnap_EndAlignment_LandsTargetAtScrollportEnd()
    {
        const string html =
            "<!DOCTYPE html><html><head><style>" +
            "html,body{margin:0;padding:0}" +
            ":root{scroll-snap-type:block mandatory;overflow:hidden}" +
            "body{scroll-padding:25%}" +
            "#fail{height:120vh;font:bold 2em;background:red}" +
            "#target{margin:120vh 0;scroll-snap-align:end;border-bottom:solid orange thick;height:20px}" +
            "</style></head><body>" +
            "<div id=\"fail\">FAIL</div>" +
            "<div id=\"target\"><div>Test passes if the orange stripe below is exactly at the bottom of the viewport.</div></div>" +
            "<script>document.getElementById('target').scrollIntoView();</script>" +
            "</body></html>";

        using var bmp = Render(html);
        Assert.Equal(0, Count(bmp, IsRed));

        var (firstOrange, lastOrange) = RowSpan(bmp, IsOrange);
        Assert.InRange(firstOrange, 762, 764);
        Assert.Equal(H - 1, lastOrange);
    }

    /// <summary>
    /// The same document without <c>scroll-snap-type</c> must NOT snap: a plain
    /// <c>scrollIntoView()</c> start-aligns, putting the target's top at the scrollport top and
    /// leaving the orange stripe 25px down rather than at the bottom edge. Guards the snap hook
    /// against firing on ordinary scroll containers.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void WithoutScrollSnapType_ScrollIntoViewStartAligns()
    {
        const string html =
            "<!DOCTYPE html><html><head><style>" +
            "html,body{margin:0;padding:0}" +
            ":root{overflow:hidden}" +
            "#target{margin:120vh 0;scroll-snap-align:end;border-bottom:solid orange thick;height:20px}" +
            "</style></head><body>" +
            "<div id=\"fail\" style=\"height:120vh;background:red\">FAIL</div>" +
            "<div id=\"target\"></div>" +
            "<script>document.getElementById('target').scrollIntoView();</script>" +
            "</body></html>";

        using var bmp = Render(html);
        var (firstOrange, _) = RowSpan(bmp, IsOrange);
        Assert.InRange(firstOrange, 19, 21);
    }

    /// <summary><c>proximity</c> leaves the snap decision to the UA, so it must not move the scroll.</summary>
    [Fact(Timeout = 600000)]
    public void ProximityScrollSnapType_DoesNotSnap()
    {
        const string html =
            "<!DOCTYPE html><html><head><style>" +
            "html,body{margin:0;padding:0}" +
            ":root{scroll-snap-type:block proximity;overflow:hidden}" +
            "#target{margin:120vh 0;scroll-snap-align:end;border-bottom:solid orange thick;height:20px}" +
            "</style></head><body>" +
            "<div id=\"fail\" style=\"height:120vh;background:red\">FAIL</div>" +
            "<div id=\"target\"></div>" +
            "<script>document.getElementById('target').scrollIntoView();</script>" +
            "</body></html>";

        using var bmp = Render(html);
        var (firstOrange, _) = RowSpan(bmp, IsOrange);
        Assert.InRange(firstOrange, 19, 21);
    }

    // ---------------------------------------------------------------------
    // 5. Keyword border widths count toward the scrollIntoView target size
    // ---------------------------------------------------------------------

    /// <summary>
    /// <c>scrollIntoView({block:'end'})</c> aligns the target's border-box end with the scrollport
    /// end, so a <c>thick</c> (5px) bottom border must be part of the measured size. Reconstructing
    /// the size from computed-style strings dropped the keyword and left the border off-screen.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ScrollIntoViewEnd_CountsKeywordBorderWidthInTargetSize()
    {
        const string html =
            "<!DOCTYPE html><html><head><style>" +
            "html,body{margin:0;padding:0} :root{overflow:hidden}" +
            "#target{margin:120vh 0;border-bottom:solid orange thick;height:20px}" +
            "</style></head><body>" +
            "<div style=\"height:120vh\"></div>" +
            "<div id=\"target\"></div>" +
            "<script>document.getElementById('target').scrollIntoView({block:'end'});</script>" +
            "</body></html>";

        using var bmp = Render(html);
        var (_, lastOrange) = RowSpan(bmp, IsOrange);
        Assert.Equal(H - 1, lastOrange);
    }
}
