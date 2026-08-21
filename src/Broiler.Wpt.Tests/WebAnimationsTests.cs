using System.IO;
using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression tests for <c>element.animate()</c> (Web Animations) value snapshotting and
/// transform interpolation (<c>DomBridge.ElementAnimate</c> / <c>TryInterpolateTransform</c>).
/// <c>element.animate</c> was previously a no-op stub, so animation-driven values never
/// rendered; and the keyframe interpolator did not handle <c>transform</c> (it stepped
/// discretely). These verify the snapshot value is computed and baked into the render.
/// </summary>
public class WebAnimationsTests : IDisposable
{
    private readonly string _root;

    public WebAnimationsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "broiler-web-anim-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private BBitmap Render(string html, int w = 400, int h = 200)
    {
        var file = Path.Combine(_root, "t-" + Guid.NewGuid().ToString("N")[..6] + ".html");
        File.WriteAllText(file, html);
        var runner = new WptTestRunner(w, h);
        return runner.RenderHtmlFileBitmapPublic(file, _root);
    }

    [Fact(Timeout = 600000)]
    public void Animate_BackgroundColor_BakesInterpolatedColor()
    {
        // black → magenta at 50% (delay -50s of a 100s animation) is rgb(~100,0,~100).
        using var bmp = Render(
            "<!doctype html>\n<style>.box{width:100px;height:100px;background:white}</style>\n" +
            "<div class=box id=t></div>\n<script>\n" +
            "  t.animate([{backgroundColor:'rgb(0,0,0)'},{backgroundColor:'rgb(200,0,200)'}], " +
            "{duration:100000, delay:-50000, fill:'both'});\n</script>");
        var c = bmp.GetPixel(50, 50);
        Assert.True(c.R is > 60 and < 140 && c.G < 40 && c.B is > 60 and < 140,
            $"backgroundColor should interpolate to mid magenta; got {c.R},{c.G},{c.B}.");
    }

    [Fact(Timeout = 600000)]
    public void Animate_TransformTranslate_BakesInterpolatedOffset()
    {
        // translateX(0 → 400px) at 50% = 200px: the 80px green box sits around x≈200, not x≈0.
        using var bmp = Render(
            "<!doctype html>\n<style>.box{width:80px;height:80px;background:green}</style>\n" +
            "<div class=box id=t></div>\n<script>\n" +
            "  t.animate([{transform:'translateX(0px)'},{transform:'translateX(400px)'}], " +
            "{duration:100000, delay:-50000, fill:'both'});\n</script>");

        static bool IsGreen(BColor c) => c.G > 100 && c.R < 100 && c.B < 100;
        Assert.False(IsGreen(bmp.GetPixel(10, 40)), "box must have moved away from x≈0.");
        Assert.True(IsGreen(bmp.GetPixel(230, 40)), "box must render around the interpolated x≈200.");
    }

    [Fact(Timeout = 600000)]
    public void Animate_TransformUniformScale_BakesInterpolatedScale()
    {
        // scale(1 → 3) at 50% = scale(2): a 40px box becomes ~80px, so a pixel at (60,60)
        // (outside the un-scaled 40px box, inside the scaled 80px box) is green.
        using var bmp = Render(
            "<!doctype html>\n<style>.box{width:40px;height:40px;background:green;transform-origin:0 0}</style>\n" +
            "<div class=box id=t></div>\n<script>\n" +
            "  t.animate([{transform:'scale(1)'},{transform:'scale(3)'}], " +
            "{duration:100000, delay:-50000, fill:'both'});\n</script>");
        var c = bmp.GetPixel(60, 60);
        Assert.True(c.G > 100 && c.R < 100 && c.B < 100,
            $"uniform scale(2) should enlarge the box to cover (60,60); got {c.R},{c.G},{c.B}.");
    }

    [Fact(Timeout = 600000)]
    public void Animate_NoOptions_IsInert_AndDoesNotThrow()
    {
        // A malformed/zero-duration animate() must leave the element at its base value.
        using var bmp = Render(
            "<!doctype html>\n<style>.box{width:100px;height:100px;background:green}</style>\n" +
            "<div class=box id=t></div>\n<script>\n" +
            "  t.animate([{transform:'translateX(400px)'}]);\n</script>");
        var c = bmp.GetPixel(50, 50);
        Assert.True(c.G > 100 && c.R < 100 && c.B < 100,
            $"a zero-duration animate() must not move the box; got {c.R},{c.G},{c.B}.");
    }
}
