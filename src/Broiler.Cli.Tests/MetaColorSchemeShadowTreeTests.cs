using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// A <c>&lt;meta name="color-scheme"&gt;</c> inside a shadow tree must NOT affect the document's
/// color scheme — WPT
/// <c>html/semantics/document-metadata/the-meta-element/color-scheme/meta-color-scheme-single-value-in-shadow-tree</c>.
/// The meta color-scheme metadata is a document-tree concept; a meta in a shadow tree is scoped
/// away and the root stays the light UA default.
/// </summary>
public class MetaColorSchemeShadowTreeTests
{
    private static (int R, int G, int B) RenderCanvasCenter(string script)
    {
        const string html = "<!DOCTYPE html><html><head></head><body></body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        context.Eval(script);
        var serialized = bridge.SerializeToHtml();
        using var bmp = HtmlRender.RenderToImageWithStyleSet(serialized, 100, 100);
        var c = bmp.GetPixel(50, 50);
        return (c.R, c.G, c.B);
    }

    [Fact(Timeout = 600000)]
    public void MetaColorScheme_InShadowTree_DoesNotDarkenCanvas()
    {
        // Attach a shadow root and insert <meta name=color-scheme content=dark> into it. Per spec
        // this is scoped to the shadow tree and does not set the document's color scheme, so the
        // canvas stays the light UA default (white) rather than the dark UA canvas rgb(18,18,18).
        var (r, g, b) = RenderCanvasCenter("""
            var host = document.createElement('div');
            document.body.appendChild(host);
            var root = host.attachShadow({ mode: 'open' });
            var meta = document.createElement('meta');
            meta.setAttribute('name', 'color-scheme');
            meta.setAttribute('content', 'dark');
            root.appendChild(meta);
        """);
        Assert.True((r, g, b) == (255, 255, 255), $"expected light canvas, was {r},{g},{b}");
    }

    [Fact(Timeout = 600000)]
    public void MetaColorScheme_InDocumentHead_DarkensCanvas()
    {
        // Control: the same meta in the document head DOES set a dark color scheme.
        var (r, g, b) = RenderCanvasCenter("""
            var meta = document.createElement('meta');
            meta.setAttribute('name', 'color-scheme');
            meta.setAttribute('content', 'dark');
            document.head.appendChild(meta);
        """);
        Assert.True((r, g, b) == (18, 18, 18), $"expected dark canvas, was {r},{g},{b}");
    }
}
