using System;
using System.Drawing;
using System.IO;
using Broiler.HTML.Image;
using Xunit;

namespace Broiler.Wpt.Tests;

/// <summary>
/// The paint an <c>@page</c> rule puts on the sheet — CSS Paged Media 3 §7's page background, and
/// the border and padding between the page's margin and its page area.
/// </summary>
/// <remarks>
/// <para>
/// Unpaginated, which is how a <c>-print</c> reftest renders by default
/// (<see cref="WptTestRunner.PagedPrint"/> is off): the surface stays the runner's viewport and
/// stands in for one sheet. The page's own paint goes underneath it either way, and that is what
/// these cover — <c>css-page/page-box-001-print</c> and its neighbours state a page background and
/// match a reference that states the same colour on <c>body</c>, so a page that paints nothing
/// matches them by 0.0 %.
/// </para>
/// <para>
/// The pairing with <see cref="PagedPrintRenderTests"/> is deliberate: that file covers cutting the
/// flow into pages, this one covers what is painted under it, and the two are independent — the
/// page paints whether or not the flow is paginated.
/// </para>
/// </remarks>
public sealed class PagePaintRenderTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private const string Text = "text";

    [Fact(Timeout = 600000)]
    public void A_Page_Background_Paints_The_Whole_Sheet()
    {
        using var rendered = RenderPrint(
            "<!DOCTYPE html><style>@page { margin: 0; background: yellow; }</style>" + Text);

        AssertColor(rendered, 200, 150, 255, 255, 0);
        AssertColor(rendered, 399, 299, 255, 255, 0);
    }

    // The control: the same document without the page background renders on white, as every
    // undecorated render does. Nothing about the decorated path may leak into it.
    [Fact(Timeout = 600000)]
    public void A_Page_With_No_Paint_Leaves_The_Sheet_White()
    {
        using var rendered = RenderPrint("<!DOCTYPE html><style>@page { margin: 0; }</style>" + Text);

        AssertColor(rendered, 200, 150, 255, 255, 255);
    }

    // `@page` applies to paged media and to nothing else, so a test that is not a print test never
    // paints one. `page-background-image-print` says as much in its own words: its background
    // should print and not show on screen.
    [Fact(Timeout = 600000)]
    public void A_Page_Background_Does_Not_Paint_On_Screen()
    {
        using var rendered = Render(
            "screen-test.html",
            "<!DOCTYPE html><style>@page { margin: 0; background: yellow; }</style>" + Text);

        AssertColor(rendered, 200, 150, 255, 255, 255);
    }

    // CSS Paged Media 3 §7.2 paints the page background over the whole page box, margin area
    // included — `page-background-004-print` states a page with a 50px margin and matches a
    // reference that is solid yellow corner to corner.
    [Fact(Timeout = 600000)]
    public void A_Page_Background_Covers_The_Margin_Area()
    {
        using var rendered = RenderPrint(
            "<!DOCTYPE html><style>@page { margin: 50px; background: yellow; }</style>" + Text);

        AssertColor(rendered, 5, 5, 255, 255, 0);
    }

    // The border, unlike the background, sits on the box the margins leave.
    [Fact(Timeout = 600000)]
    public void A_Page_Border_Is_Drawn_Inside_The_Page_Margin()
    {
        using var rendered = RenderPrint(
            "<!DOCTYPE html><style>@page { margin: 20px; border: 10px solid black; }</style>" + Text);

        AssertColor(rendered, 200, 25, 0, 0, 0);
        AssertColor(rendered, 200, 5, 255, 255, 255);
    }

    // A page that states a border and padding moves its page area in by them, which is how
    // `page-box-011-print` lines up with a reference that insets its content with a `body` border
    // and padding of its own.
    [Fact(Timeout = 600000)]
    public void A_Page_Border_And_Padding_Inset_The_Flow()
    {
        using var withInsets = RenderPrint(
            "<!DOCTYPE html><style>@page { margin: 0; border: 10px solid black; padding: 10px; }"
            + "html,body { margin: 0; } div { height: 10px; background: #00f; }</style><div></div>");

        Assert.Equal(255, withInsets.GetPixel(200, 25).B);
        Assert.Equal(255, withInsets.GetPixel(200, 15).R);
    }

    // css-page-3 §5.1: `visibility` applies in the page context. `page-visibility-hidden-001-print`
    // hides a red page border and matches a reference whose border is `solid transparent` — so the
    // border keeps its space and paints nothing.
    [Fact(Timeout = 600000)]
    public void A_Hidden_Page_Paints_Neither_Its_Background_Nor_Its_Border()
    {
        using var rendered = RenderPrint(
            "<!DOCTYPE html><style>@page { visibility: hidden; margin: 0;"
            + " border: 10px solid black; background: yellow; }</style>" + Text);

        AssertColor(rendered, 200, 5, 255, 255, 255);
        AssertColor(rendered, 200, 150, 255, 255, 255);
    }

    // A root element that generates no box generates no page, so nothing of the sheet is painted.
    // `root-element-display-none-print` states a hotpink page with a red border and matches an
    // empty document.
    [Fact(Timeout = 600000)]
    public void A_Display_None_Root_Leaves_The_Sheet_Blank()
    {
        using var rendered = RenderPrint(
            "<!DOCTYPE html><style>@page { margin: 0; border: 10px solid red; background: hotpink; }"
            + "html { display: none; }</style>FAIL");

        AssertColor(rendered, 200, 150, 255, 255, 255);
        AssertColor(rendered, 200, 5, 255, 255, 255);
    }

    // ---- what the rule contributes ----

    [Theory]
    [InlineData("@page { size: 200px; margin: 10px; }", false)]
    [InlineData("@page { visibility: hidden; }", false)]
    [InlineData("@page { background: yellow; }", true)]
    [InlineData("@page { background-image: url(x.png); }", true)]
    [InlineData("@page { border: 1px solid; }", true)]
    [InlineData("@page { padding: 1px; }", true)]
    [InlineData("@page :first { background: yellow; }", false)]
    public void A_Page_Is_Decorated_Only_When_It_Paints(string css, bool decorated) =>
        Assert.Equal(decorated, Decoration("<style>" + css + "</style>") is not null);

    [Fact(Timeout = 600000)]
    public void Later_Declarations_Win_And_Land_On_The_Box_They_Belong_To()
    {
        var decoration = Decoration(
            "<style>@page { background: red; border: 1px solid; }</style>"
            + "<style>@page { background: green; }</style>");

        Assert.NotNull(decoration);
        Assert.Equal("background:green;", decoration!.BackgroundCss);
        Assert.Equal("border:1px solid;", decoration.BoxCss);
        Assert.True(decoration.HasInsets);
    }

    // `border-radius` changes the shape the border draws, not the space it takes, so it comes along
    // with the border without making the page area worth measuring.
    [Fact(Timeout = 600000)]
    public void A_Border_Radius_Alone_Insets_Nothing()
    {
        var decoration = Decoration("<style>@page { border-radius: 4px; }</style>");

        Assert.NotNull(decoration);
        Assert.False(decoration!.HasInsets);
    }

    // The probe the decoration renders on is horizontal-tb and its containing block is not the
    // page, so neither the axis mapping nor the percentage basis of a flow-relative padding can be
    // left to it. page-box-008-print and -009-print both expect a 16/32/48/80 ring on a 400x800
    // page from 2/8/6/20 %, and state the writing mode in different places.
    [Theory]
    [InlineData(":root { writing-mode: vertical-rl; }", "")]
    [InlineData("", "writing-mode: vertical-rl;")]
    public void Logical_Padding_Becomes_The_Physical_Longhand_It_Means(string rootCss, string pageCss)
    {
        var decoration = Decoration(
            "<style>" + rootCss + "@page { " + pageCss + "size: 400px 800px;"
            + "padding-inline-start: 2%; padding-block-start: 8%;"
            + "padding-inline-end: 6%; padding-block-end: 20%; }</style>");

        Assert.NotNull(decoration);
        Assert.Equal(
            "padding-top:16px;padding-right:32px;padding-bottom:48px;padding-left:80px;",
            decoration!.BoxCss);
        Assert.True(decoration.HasInsets);
    }

    // A percentage is taken against the page box, not the box the page's margins leave: the ring
    // above is 2 % of 800 even though the border box is only 736 tall.
    [Fact(Timeout = 600000)]
    public void Logical_Padding_Percentages_Resolve_Against_The_Whole_Page_Box()
    {
        var decoration = Decoration(
            "<style>@page { size: 400px 800px; margin: 100px; padding-block-start: 10%; }</style>");

        Assert.Equal("padding-top:80px;", decoration!.BoxCss);
    }

    // Physical and flow-relative padding are both longhands, so the later one wins wherever they
    // name the same side.
    [Fact(Timeout = 600000)]
    public void Logical_Padding_Cascades_With_The_Physical_Longhand()
    {
        var decoration = Decoration(
            "<style>@page { padding-top: 1px; padding-block-start: 2px; }</style>");

        Assert.Equal("padding-top:2px;", decoration!.BoxCss);
    }

    /// <summary>
    /// The decoration a document declares, resolved against its own page box the way
    /// <c>WptTestRunner</c> resolves it — the box is what a flow-relative padding's percentage
    /// needs.
    /// </summary>
    private static WptPageDecoration? Decoration(string html) =>
        WptPageDecoration.Resolve(html, WptPageBox.Resolve(html, new SizeF(800, 600)));

    private static void AssertColor(BBitmap bitmap, int x, int y, int r, int g, int b)
    {
        var pixel = bitmap.GetPixel(x, y);
        Assert.Equal((r, g, b), (pixel.R, pixel.G, pixel.B));
    }

    /// <summary>Renders <paramref name="html"/> as a file whose name marks it a print test.</summary>
    private static BBitmap RenderPrint(string html) => Render("page-paint-print.html", html);

    private static BBitmap Render(string name, string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-page-paint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, name);
            File.WriteAllText(file, html);
            return new WptTestRunner(400, 300).RenderHtmlFileBitmapPublic(file, dir);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
