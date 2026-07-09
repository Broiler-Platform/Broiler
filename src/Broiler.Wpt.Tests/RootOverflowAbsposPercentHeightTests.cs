using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guards for two core-layout fixes that together flip the
/// css-values <c>calc-in-calc</c> / <c>calc-in-max</c> reftests (WPT
/// "biggest problems" issue #1316), both of which render a green abspos box
/// (<c>height: calc(calc(100%))</c> / <c>max(calc(100%))</c>) that must cover
/// a red <c>html</c> background under <c>html { overflow: hidden }</c>.
///
/// <list type="number">
/// <item><b>Root overflow propagation</b> (CSS Overflow 3 §3.3): a clipping
/// <c>overflow</c> on the root <c>&lt;html&gt;</c> element is applied to the
/// viewport, and the element's own used overflow becomes <c>visible</c>.
/// Broiler previously clipped at the html element's own border box, which — for
/// a short document (empty body with only abspos children) — is ~0px tall, so
/// every out-of-flow descendant was clipped away
/// (<c>ComputedStyleBuilder.RootOverflowUsedValue</c>).</item>
/// <item><b>Abspos percentage block-size</b> (CSS 2.1 §10.5/§10.6.4): for an
/// absolutely positioned box the containing block is the padding box of the
/// nearest positioned ancestor, or the initial containing block — which always
/// has a definite height — so a percentage height resolves against that height
/// instead of collapsing to auto against the (auto-height) flow parent
/// (<c>CssBox.PercentHeightContainingBlockHeight</c> +
/// <c>HeightPercentageResolvesToAuto</c>).</item>
/// </list>
/// </summary>
public class RootOverflowAbsposPercentHeightTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private const int W = 800;
    private const int H = 600;

    private static int CountColor(BBitmap bmp, System.Func<byte, byte, byte, bool> match)
    {
        int count = 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (match(p.R, p.G, p.B))
                    count++;
            }
        return count;
    }

    private static bool IsRed(byte r, byte g, byte b) => r > 200 && g < 80 && b < 80;
    private static bool IsGreen(byte r, byte g, byte b) => g > 100 && r < 80 && b < 80;

    private BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-rootovf-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "t.html");
        File.WriteAllText(file, html);
        try
        {
            var runner = new WptTestRunner(W, H);
            return runner.RenderHtmlFileBitmapPublic(file, dir);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    // Fix 1 in isolation: an explicit-height abspos box under html{overflow:hidden}
    // with an empty body. The html box is ~0px tall; before the fix its
    // overflow:hidden clipped the green box away entirely (all red / blank).
    [Fact]
    public void RootOverflowHidden_DoesNotClipAbsposDescendant()
    {
        const string html =
            "<!DOCTYPE html><html><head><style>" +
            "html,body{margin:0;padding:0} html{background:red;overflow:hidden}" +
            "#g{position:absolute;top:0;left:0;width:100%;height:300px;background:green}" +
            "</style></head><body><div id=\"g\"></div></body></html>";

        using var bmp = Render(html);
        int green = CountColor(bmp, IsGreen);
        // Roughly the top half (800x300 = 240000px) must be green.
        Assert.True(green > 200000, $"green={green}; abspos box was clipped by root overflow:hidden.");
    }

    // Fix 2 in isolation: an abspos box with a percentage height and NO positioned
    // ancestor resolves against the initial containing block (viewport), not the
    // auto-height <body>. Before the fix height:100% collapsed to 0 (all red).
    [Fact]
    public void AbsposPercentageHeight_ResolvesAgainstInitialContainingBlock()
    {
        const string html =
            "<!DOCTYPE html><html><head><style>" +
            "html,body{margin:0;padding:0} html{background:red}" +
            "#g{position:absolute;top:0;left:0;width:100%;height:100%;background:green}" +
            "</style></head><body><div id=\"g\"></div></body></html>";

        using var bmp = Render(html);
        int red = CountColor(bmp, IsRed);
        int green = CountColor(bmp, IsGreen);
        Assert.True(green > W * H * 0.98, $"green={green}; abspos height:100% did not fill the viewport.");
        Assert.True(red < W * H * 0.02, $"red={red}; red root background exposed.");
    }

    // Integration: the exact calc-in-calc / calc-in-max shape — nested calc()
    // percentage height on an abspos box over a red root with overflow:hidden.
    // Needs BOTH fixes; the whole viewport must be green.
    [Theory]
    [InlineData("calc(calc(100%))")]
    [InlineData("max(calc(100%))")]
    public void NestedCalcPercentageHeight_AbsposOverRootOverflowHidden_FillsViewport(string height)
    {
        string html =
            "<!DOCTYPE html><html><head><style>" +
            "html,body{margin:0;padding:0} html{background:red;overflow:hidden}" +
            "#outer{position:absolute;top:0;left:0;background:green;width:100%;height:" + height + "}" +
            "</style></head><body><div id=\"outer\"></div></body></html>";

        using var bmp = Render(html);
        int red = CountColor(bmp, IsRed);
        int green = CountColor(bmp, IsGreen);
        Assert.True(green > W * H * 0.98, $"green={green} for height:{height}; expected the viewport filled green.");
        Assert.True(red < W * H * 0.02, $"red={red} for height:{height}; red root background exposed.");
    }
}
