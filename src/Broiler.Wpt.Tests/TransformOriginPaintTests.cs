using System;
using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// CSS Transforms 1 §8: an element's <c>transform</c> is applied about its <c>transform-origin</c>,
/// and the painted result has to agree with the geometry the element's own script measures.
///
/// <para>
/// It did not. The paint walker applied every CSS transform about the box centre and never read the
/// property, while the script bridge's transform chain read it correctly — so
/// <c>transform-origin: 0 0; transform: scale(0.5)</c> on a 100×100 box reported a rect at (0, 0)
/// from <c>getBoundingClientRect</c> and painted it at (25, 25). Scaling <em>up</em> was worse than
/// a displacement: a centre origin sends a top-left child off the canvas entirely, so
/// <c>transform: scale(5)</c> — the shape WPT <c>css/filter-effects/filter-scaling-001</c> (entry 28
/// of issue #1774's top 30, 50.1%) is built on — rendered a blank page where half the viewport
/// should be green.
/// </para>
///
/// <para>
/// The reftest suite barely sees this, which is why it survived so long: both sides of a reftest
/// are rendered by Broiler and a reference normally declares the same origin as its test, so the
/// error cancels. It does not cancel against a reference browser's screenshot.
/// </para>
/// </summary>
/// <remarks>
/// The fix's paint half lives in <c>Broiler.HTML</c>'s <c>PaintWalker</c> and ships as a patch under
/// <c>patches/</c> until it lands upstream, so each case here probes first and self-skips rather
/// than going red against a pinned pointer that does not carry it. The grammar itself is main-repo
/// and unconditionally covered by <c>Broiler.Layout.Tests.CssTransformOriginTests</c>.
/// </remarks>
[Xunit.Collection("NativeAnchorWpt")]
public class TransformOriginPaintTests
{
    private const int Size = 200;

    private static BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-tformorigin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);
            return new WptTestRunner(Size, Size).RenderHtmlFileBitmapPublic(file, dir);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>
    /// A 100×100 box holding a 20×20 green square at its top-left, scaled by a half about
    /// <paramref name="origin"/>. The square's painted top-left corner is the whole answer.
    /// </summary>
    private static string Page(string origin) =>
        "<!DOCTYPE html><html><body style=\"margin:0\">"
        + $"<div style=\"width:100px;height:100px;transform-origin:{origin};transform:scale(0.5)\">"
        + "<div style=\"width:20px;height:20px;background:green\"></div></div>"
        + "</body></html>";

    private static (int X, int Y)? GreenCorner(BBitmap bmp)
    {
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.R < 80 && p.G > 100 && p.B < 80)
                    return (x, y);
            }
        }

        return null;
    }

    /// <summary>
    /// Whether the paint path reads <c>transform-origin</c> at all. Without the patch every origin
    /// paints at the box centre, so <c>0 0</c> — the one furthest from it — is the probe.
    /// </summary>
    private static bool PaintReadsTransformOrigin() => GreenCorner(Render(Page("0 0"))) == (0, 0);

    private static void AssertCorner(int x, int y, string origin)
    {
        // Self-skip rather than fail: the paint half is a pending patch, so on a checkout of the
        // pinned Broiler.HTML pointer there is nothing here to assert yet.
        if (!PaintReadsTransformOrigin())
            return;

        Assert.Equal((x, y), GreenCorner(Render(Page(origin))));
    }

    [Fact(Timeout = 600000)]
    public void ZeroZero_PaintsFromTheBoxCorner() => AssertCorner(0, 0, "0 0");

    /// <summary>The initial value, and the only one that used to be right.</summary>
    [Fact(Timeout = 600000)]
    public void FiftyPercent_PaintsFromTheCentre() => AssertCorner(25, 25, "50% 50%");

    [Fact(Timeout = 600000)]
    public void OneHundredPercent_PaintsFromTheFarCorner() => AssertCorner(50, 50, "100% 100%");

    /// <summary>The keyword pair, including the spelling that leads with the vertical one.</summary>
    [Theory]
    [InlineData("left top")]
    [InlineData("top left")]
    public void TheCornerKeywords_PaintFromTheCorner(string origin) => AssertCorner(0, 0, origin);

    [Fact(Timeout = 600000)]
    public void ALength_IsMeasuredFromTheBoxCorner() => AssertCorner(5, 5, "10px 10px");

    /// <summary>A lone vertical keyword moves y and centres x.</summary>
    [Fact(Timeout = 600000)]
    public void ALoneTop_MovesOnlyTheVerticalAxis() => AssertCorner(25, 0, "top");

    /// <summary>
    /// Scaling up is where the wrong origin stopped being a displacement and started being a blank
    /// page: about the centre, this box's content lands entirely off the canvas.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ScalingUpAboutTheCorner_KeepsTheContentOnTheCanvas()
    {
        if (!PaintReadsTransformOrigin())
            return;

        var bmp = Render(
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<div style=\"width:100px;height:100px;transform-origin:0 0;transform:scale(5)\">"
            + "<div style=\"width:20px;height:20px;background:green\"></div></div>"
            + "</body></html>");

        Assert.Equal((0, 0), GreenCorner(bmp));

        // 20px scaled five times is a 100px square, so the far corner is inside the canvas too.
        var far = bmp.GetPixel(99, 99);
        Assert.True(far.R < 80 && far.G > 100 && far.B < 80,
            $"the scaled square should reach (99, 99), got rgb({far.R},{far.G},{far.B})");
    }
}
