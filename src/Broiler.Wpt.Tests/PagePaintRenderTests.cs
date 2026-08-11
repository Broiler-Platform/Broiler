using System;
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

    [Fact]
    public void A_Page_Background_Paints_The_Whole_Sheet()
    {
        using var rendered = RenderPrint(
            "<!DOCTYPE html><style>@page { margin: 0; background: yellow; }</style>" + Text);

        AssertColor(rendered, 200, 150, 255, 255, 0);
        AssertColor(rendered, 399, 299, 255, 255, 0);
    }

    // The control: the same document without the page background renders on white, as every
    // undecorated render does. Nothing about the decorated path may leak into it.
    [Fact]
    public void A_Page_With_No_Paint_Leaves_The_Sheet_White()
    {
        using var rendered = RenderPrint("<!DOCTYPE html><style>@page { margin: 0; }</style>" + Text);

        AssertColor(rendered, 200, 150, 255, 255, 255);
    }

    // `@page` applies to paged media and to nothing else, so a test that is not a print test never
    // paints one. `page-background-image-print` says as much in its own words: its background
    // should print and not show on screen.
    [Fact]
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
    [Fact]
    public void A_Page_Background_Covers_The_Margin_Area()
    {
        using var rendered = RenderPrint(
            "<!DOCTYPE html><style>@page { margin: 50px; background: yellow; }</style>" + Text);

        AssertColor(rendered, 5, 5, 255, 255, 0);
    }

    // The border, unlike the background, sits on the box the margins leave.
    [Fact]
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
    [Fact]
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
    [Fact]
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
    [Fact]
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
        Assert.Equal(decorated, WptPageDecoration.Resolve("<style>" + css + "</style>") is not null);

    [Fact]
    public void Later_Declarations_Win_And_Land_On_The_Box_They_Belong_To()
    {
        var decoration = WptPageDecoration.Resolve(
            "<style>@page { background: red; border: 1px solid; }</style>"
            + "<style>@page { background: green; }</style>");

        Assert.NotNull(decoration);
        Assert.Equal("background:green;", decoration!.BackgroundCss);
        Assert.Equal("border:1px solid;", decoration.BoxCss);
        Assert.True(decoration.HasInsets);
    }

    // `border-radius` changes the shape the border draws, not the space it takes, so it comes along
    // with the border without making the page area worth measuring.
    [Fact]
    public void A_Border_Radius_Alone_Insets_Nothing()
    {
        var decoration = WptPageDecoration.Resolve("<style>@page { border-radius: 4px; }</style>");

        Assert.NotNull(decoration);
        Assert.False(decoration!.HasInsets);
    }

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
