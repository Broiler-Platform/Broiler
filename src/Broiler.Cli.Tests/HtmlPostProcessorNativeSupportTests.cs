using Broiler.HtmlBridge;
using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 6 (P6.3) native-behaviour migration evidence for <see cref="HtmlPostProcessor"/>. These pin the
/// renderer behaviour that justifies dropping the <c>:root</c>→<c>html</c> rewrite from the production
/// <c>ProcessForBrowsing</c> profile (the renderer supports <c>:root</c> natively) and document that
/// <c>&lt;script&gt;</c> content is natively non-rendered (so <c>StripScriptTags</c> is not needed for
/// bleed-through — it is kept only as a protective normaliser before the other regex passes). The
/// string-level tests pin that production no longer rewrites <c>:root</c> while the test-harness profile
/// still does.
/// </summary>
public sealed class HtmlPostProcessorNativeSupportTests
{
    private static int CountPixels(BBitmap b, Func<byte, byte, byte, bool> match)
    {
        int n = 0;
        for (int y = 0; y < b.Height; y++)
        for (int x = 0; x < b.Width; x++)
        {
            var p = b.GetPixel(x, y);
            if (match(p.R, p.G, p.B))
                n++;
        }
        return n;
    }

    [Fact]
    public void Renderer_Supports_Root_Pseudo_Class_Natively()
    {
        // No HtmlPostProcessor / no :root->html rewrite: a :root background must paint natively.
        const string html =
            "<!DOCTYPE html><html><head><style>:root { background: rgb(0,128,192); }</style></head>" +
            "<body><p>x</p></body></html>";
        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, 80, 80);
        var p = bmp.GetPixel(0, 0);
        Assert.Equal((0, 128, 192), (p.R, p.G, p.B));
    }

    [Fact]
    public void Renderer_Does_Not_Render_Script_Content()
    {
        // HTML-like markup inside a <script> body must NOT bleed through as rendered content.
        const string withScript =
            "<!DOCTYPE html><html><body>" +
            "<script>var x='<div style=\"background:rgb(0,200,0);width:60px;height:60px\"></div>';</script>" +
            "</body></html>";
        using var b1 = HtmlRender.RenderToImageWithStyleSet(withScript, 100, 100);
        Assert.Equal(0, CountPixels(b1, (r, g, b) => r < 80 && g > 150 && b < 80));

        // Control: the same green div outside a script renders (guards the detector isn't trivially zero).
        const string real =
            "<!DOCTYPE html><html><body>" +
            "<div style=\"background:rgb(0,200,0);width:60px;height:60px\"></div></body></html>";
        using var b2 = HtmlRender.RenderToImageWithStyleSet(real, 100, 100);
        Assert.True(CountPixels(b2, (r, g, b) => r < 80 && g > 150 && b < 80) > 1000);
    }

    [Fact]
    public void ProcessForBrowsing_Does_Not_Rewrite_Root_Selector()
    {
        const string html = "<style>:root{color:red}</style>";
        var browsing = HtmlPostProcessor.ProcessForBrowsing(html);
        Assert.Contains(":root", browsing, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_TestHarness_Still_Rewrites_Root_Selector()
    {
        const string html = "<style>:root{color:red}</style>";
        var harness = HtmlPostProcessor.Process(html);
        Assert.DoesNotContain(":root", harness, StringComparison.Ordinal);
        Assert.Contains("html{color:red}", harness, StringComparison.Ordinal);
    }
}
