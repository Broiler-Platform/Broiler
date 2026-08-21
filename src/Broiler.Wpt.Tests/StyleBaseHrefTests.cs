using System.IO;
using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression tests for render-time <c>&lt;base href&gt;</c> resolution of stylesheet
/// <c>url()</c> references (<c>DomBridge.ApplyBaseHrefToStyleUrls</c>, run from
/// <c>ApplySerializationTransforms</c>). The renderer resolved a relative CSS <c>url()</c>
/// against the document's own directory and never consulted <c>&lt;base&gt;</c>, so a page
/// setting <c>&lt;base href="/images/"&gt;</c> and referencing <c>url(green.png)</c> failed to
/// find the image and painted its fallback colour (the WPT <c>*base-uri*</c> family, e.g.
/// <c>css-values/inline-cache-base-uri-cssom</c>). Rewriting the <c>url()</c> against the base
/// during serialization — keeping a root-relative base root-relative so the <c>wptRoot</c>
/// mapper still resolves it — closes that gap.
/// </summary>
public class StyleBaseHrefTests : IDisposable
{
    private readonly string _root;

    public StyleBaseHrefTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "broiler-base-href-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_root);
        // A solid lime image at the WPT-root-relative /images/green.png.
        var imageDir = Path.Combine(_root, "images");
        Directory.CreateDirectory(imageDir);
        using var img = new BBitmap(8, 8);
        img.Clear(new BColor(0, 255, 0, 255));
        img.Save(Path.Combine(imageDir, "green.png"));
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private BColor RenderCenterPixel(string html)
    {
        var dir = Path.Combine(_root, "css", "t");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "t-" + Guid.NewGuid().ToString("N")[..6] + ".html");
        File.WriteAllText(file, html);
        var runner = new WptTestRunner(200, 200);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, _root);
        return bmp.GetPixel(100, 100);
    }

    private static bool IsLime(BColor c) => c.G > 200 && c.R < 80 && c.B < 80;
    private static bool IsRed(BColor c) => c.R > 200 && c.G < 80 && c.B < 80;

    [Fact(Timeout = 600000)]
    public void RootRelativeBase_ResolvesRelativeCssUrl()
    {
        // <base href="/images/"> makes url(green.png) resolve to /images/green.png (lime),
        // covering the red fallback. Without honouring <base> this stayed red.
        var color = RenderCenterPixel(
            "<!doctype html>\n<base href=\"/images/\">\n" +
            "<style>:root { background-color: red; background-image: url(green.png); }</style>");
        Assert.True(IsLime(color), $"root-relative <base> should resolve url(green.png) to lime; got {color.R},{color.G},{color.B}.");
    }

    [Fact(Timeout = 600000)]
    public void RootRelativeBase_ResolvesUrlInScriptInsertedRule()
    {
        // The css-values/inline-cache-base-uri-cssom scenario: the url() arrives through
        // CSSOM insertRule on a second stylesheet declared after <base>.
        var color = RenderCenterPixel(
            "<!doctype html>\n" +
            "<style>:root { background-color: red; }</style>\n" +
            "<base href=\"/images/\">\n" +
            "<style>:root { background-color: red; }</style>\n" +
            "<script>document.styleSheets[1].insertRule(`:root { background-image: url(green.png) }`);</script>");
        Assert.True(IsLime(color), $"<base> must apply to a CSSOM-inserted url(); got {color.R},{color.G},{color.B}.");
    }

    [Fact(Timeout = 600000)]
    public void AbsoluteRootRelativeUrl_WithoutBase_StillResolves()
    {
        // No <base>: a url(/images/green.png) is already root-relative and must still
        // resolve — the transform is a no-op when no <base href> is present.
        var color = RenderCenterPixel(
            "<!doctype html>\n<style>:root { background-color: red; background-image: url(/images/green.png); }</style>");
        Assert.True(IsLime(color), $"absolute root-relative url() should still resolve without <base>; got {color.R},{color.G},{color.B}.");
    }

    [Fact(Timeout = 600000)]
    public void RelativeUrl_WithoutBase_DoesNotResolveToRoot()
    {
        // No <base>: a relative url(green.png) resolves against the document's own directory
        // (css/t/), where there is no image — so the red fallback shows. This guards against
        // the rewrite firing when there is no <base> to honour.
        var color = RenderCenterPixel(
            "<!doctype html>\n<style>:root { background-color: red; background-image: url(green.png); }</style>");
        Assert.True(IsRed(color), $"without <base>, a relative url() must not resolve to the root image; got {color.R},{color.G},{color.B}.");
    }
}
