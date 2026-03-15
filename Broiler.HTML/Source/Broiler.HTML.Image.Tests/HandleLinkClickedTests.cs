using System.Drawing;
using Broiler.HTML.Adapters;
using Broiler.HTML.Orchestration.Core;
using Broiler.HTML.Image.Adapters;
using Broiler.HTML.Dom.Core.Dom;

namespace Broiler.HTML.Image.Tests;

public class HandleLinkClickedTests
{
    private static HtmlContainerInt CreateContainer()
    {
        var container = new HtmlContainerInt(SkiaImageAdapter.Instance, HandlerFactory.Instance);
        container.PageSize = new SizeF(99999, 99999);
        return container;
    }

    [Fact]
    public void EmptyAnchorHash_FiresScrollChangeToOrigin()
    {
        var container = CreateContainer();
        container.SetHtml("<html><body><a href=\"#\">Top</a><p id=\"content\">Hello</p></body></html>");

        double? scrollX = null, scrollY = null;
        container.ScrollChange += (_, args) => { scrollX = args.X; scrollY = args.Y; };

        // Create a CssBox with href="#" to simulate the anchor click
        var tag = new HtmlTag("a", false, new Dictionary<string, string> { { "href", "#" } });
        var box = new CssBox(null, tag);

        container.HandleLinkClicked(this, PointF.Empty, box);

        Assert.NotNull(scrollX);
        Assert.NotNull(scrollY);
        Assert.Equal(0.0, scrollX.Value);
        Assert.Equal(0.0, scrollY.Value);
    }

    [Fact]
    public void NamedAnchor_FiresScrollChangeToElement()
    {
        var container = CreateContainer();
        container.SetHtml("<html><body><a href=\"#target\">Go</a><div id=\"target\">Target</div></body></html>");

        using var bmp = new SkiaSharp.SKBitmap(800, 600);
        using var canvas = new SkiaSharp.SKCanvas(bmp);
        container.PerformLayout(new GraphicsAdapter(canvas, new RectangleF(0, 0, 800, 600)));

        double? scrollX = null, scrollY = null;
        container.ScrollChange += (_, args) => { scrollX = args.X; scrollY = args.Y; };

        var tag = new HtmlTag("a", false, new Dictionary<string, string> { { "href", "#target" } });
        var box = new CssBox(null, tag);

        container.HandleLinkClicked(this, PointF.Empty, box);

        // The element exists, so scroll should be triggered to its location
        Assert.NotNull(scrollX);
        Assert.NotNull(scrollY);
    }

    [Fact]
    public void EmptyHref_DoesNotFireScrollChange()
    {
        var container = CreateContainer();
        container.SetHtml("<html><body><a href=\"\">Empty</a></body></html>");

        bool scrollFired = false;
        container.ScrollChange += (_, _) => scrollFired = true;

        var tag = new HtmlTag("a", false, new Dictionary<string, string> { { "href", "" } });
        var box = new CssBox(null, tag);

        container.HandleLinkClicked(this, PointF.Empty, box);

        Assert.False(scrollFired);
    }

    [Fact]
    public void LinkClicked_CanBeInterceptedAndHandled()
    {
        var container = CreateContainer();
        container.SetHtml("<html><body><a href=\"#\">Top</a></body></html>");

        bool intercepted = false;
        container.LinkClicked += (_, args) =>
        {
            intercepted = true;
            args.Handled = true;
        };

        bool scrollFired = false;
        container.ScrollChange += (_, _) => scrollFired = true;

        var tag = new HtmlTag("a", false, new Dictionary<string, string> { { "href", "#" } });
        var box = new CssBox(null, tag);

        container.HandleLinkClicked(this, PointF.Empty, box);

        Assert.True(intercepted);
        Assert.False(scrollFired); // Should not scroll because event was handled
    }

    [Fact]
    public void LinkClicked_FragmentHref_ResolvesAgainstBaseUrl()
    {
        var container = CreateContainer();
        container.BaseUrl = "https://example.com/acid2.html";
        container.SetHtml("<html><body><a href=\"#top\">Top</a></body></html>");

        string? receivedLink = null;
        container.LinkClicked += (_, args) =>
        {
            receivedLink = args.Link;
            args.Handled = true;
        };

        var tag = new HtmlTag("a", false, new Dictionary<string, string> { { "href", "#top" } });
        var box = new CssBox(null, tag);

        container.HandleLinkClicked(this, PointF.Empty, box);

        Assert.Equal("https://example.com/acid2.html#top", receivedLink);
    }
}
