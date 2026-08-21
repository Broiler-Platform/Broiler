using System;
using System.IO;
using Broiler.HTML.Image;
using Xunit;

namespace Broiler.Wpt.Tests;

/// <summary>
/// The runner's paged render: a WPT <c>-print</c> test laid out on pages of the document's own
/// <c>@page</c> box, cut from the flow and stacked into one bitmap.
/// </summary>
/// <remarks>
/// <para>
/// The model is that the engine paints one continuous surface and a page is a band of it — so the
/// document is laid out once, on a surface several page areas tall, with the container's page size
/// set to one page area. That separation is the whole mechanism: the page area is what <c>vh</c>
/// and the fragmentation boundaries resolve against, the surface is what gets rasterised.
/// </para>
/// <para>
/// Behind <see cref="WptTestRunner.PagedPrint"/>, off by default — see that property for what the
/// paged run currently scores and why. These tests set it for their own duration.
/// </para>
/// </remarks>
[Collection("PagedPrint")]
public sealed class PagedPrintRenderTests : IDisposable
{
    private readonly bool _previousPagedPrint = WptTestRunner.PagedPrint;

    public PagedPrintRenderTests() => WptTestRunner.PagedPrint = true;

    public void Dispose()
    {
        WptTestRunner.PagedPrint = _previousPagedPrint;
        Program.ResetTestHooks();
    }

    // A 200x100 sheet with 10px margins: a 180x80 page area, and blocks small enough that two of
    // them share a page unless something breaks between them.
    private const string PageStyle =
        "<!DOCTYPE html><meta charset=\"utf-8\"><style>"
        + "@page { size: 200px 100px; margin: 10px; }"
        + "html,body { margin: 0; padding: 0; }"
        + "div { height: 30px; background: #000; }"
        + "</style>";

    [Fact(Timeout = 600000)]
    public void A_Single_Page_Document_Renders_One_Page_Box()
    {
        using var rendered = RenderPrint(PageStyle + "<div></div>");

        Assert.Equal(200, rendered.Width);
        Assert.Equal(100, rendered.Height);
    }

    // A forced break puts the second block on page two, and the output grows by one page box —
    // the page count is the render's own, not a fixed number of bands.
    [Fact(Timeout = 600000)]
    public void A_Forced_Break_Adds_A_Page()
    {
        using var rendered = RenderPrint(
            PageStyle + "<div></div><div style=\"break-before: page\"></div>");

        Assert.Equal(200, rendered.Height);
        Assert.True(IsInk(rendered, 100, 20), "page 1 should carry the first block");
        Assert.True(IsInk(rendered, 100, 120), "page 2 should carry the second block");
    }

    // The same document with a change of page name instead of a forced break: CSS Paged Media 3
    // §3.4 makes the two mean the same thing, which is how WPT's page-name references are written.
    [Fact(Timeout = 600000)]
    public void A_Change_Of_Page_Name_Adds_The_Same_Page()
    {
        using var rendered = RenderPrint(
            PageStyle + "<div style=\"page: a\"></div><div style=\"page: b\"></div>");

        Assert.Equal(200, rendered.Height);
        Assert.True(IsInk(rendered, 100, 120), "page 2 should carry the second block");
    }

    [Fact(Timeout = 600000)]
    public void The_Same_Page_Name_On_Both_Blocks_Adds_No_Page()
    {
        using var rendered = RenderPrint(
            PageStyle + "<div style=\"page: a\"></div><div style=\"page: a\"></div>");

        Assert.Equal(100, rendered.Height);
    }

    // A named page may be a different size, and then the flow on it divides against *that* area.
    // Here the named block is 250px tall on a 300px page of its own, so it is one page; laid out
    // against the unconditional 100px area — which is what one layout for the whole document can
    // only ever do — it would be three, and the sheet would come out 1000px instead of 400px.
    [Fact(Timeout = 600000)]
    public void A_Named_Page_Divides_Its_Flow_Against_Its_Own_Area()
    {
        using var rendered = RenderPrint(
            "<!DOCTYPE html><meta charset=\"utf-8\"><style>"
            + "@page { size: 200px 100px; margin: 0; }"
            + "@page tall { size: 200px 300px; }"
            + "html,body { margin: 0; padding: 0; }"
            + "</style>"
            + "<div style=\"height:80px; background:#000\"></div>"
            + "<div style=\"page:tall; height:250px; background:#000\"></div>");

        Assert.Equal(200, rendered.Width);
        Assert.Equal(400, rendered.Height);
        Assert.True(IsInk(rendered, 100, 340), "the named page should carry the whole block");
    }

    // And the page after a named one goes back to the unconditional rule. A run of pages is bounded
    // by the name changes on either side of it, so an unnamed run following a named one is its own
    // run and not a continuation — `page-name-unnamed-trailing-001` is built on exactly that.
    [Fact(Timeout = 600000)]
    public void An_Unnamed_Page_After_A_Named_One_Returns_To_The_Unconditional_Box()
    {
        using var rendered = RenderPrint(
            InsetStyle
            + "<div style=\"height:60px; background:#000\"></div>"
            + "<div style=\"page:mid; height:40px; background:#000\"></div>"
            + "<div style=\"height:60px; background:#000\"></div>");

        Assert.Equal(300, rendered.Height);
        Assert.True(IsInk(rendered, 5, 5), "page 1 has no margin, so its content starts at the corner");
        Assert.False(IsInk(rendered, 5, 105), "page 2 is inset by @page mid's margin");
        Assert.True(IsInk(rendered, 25, 125), "page 2's content starts inside that margin");
        Assert.True(IsInk(rendered, 5, 205), "page 3 is back on the unconditional box");
    }

    // The name belongs to the innermost box that starts the page, not the outermost. A plain
    // wrapper around named content does not claim the page for itself — the same rule the flow
    // applies when it decides where to break (CssBox.StartPageName).
    [Fact(Timeout = 600000)]
    public void A_Wrapper_Does_Not_Claim_The_Page_Its_Child_Names()
    {
        using var rendered = RenderPrint(
            InsetStyle
            + "<div style=\"height:60px; background:#000\"></div>"
            + "<div><div style=\"page:mid; height:40px; background:#000\"></div></div>"
            + "<div style=\"height:60px; background:#000\"></div>");

        Assert.Equal(300, rendered.Height);
        Assert.False(IsInk(rendered, 5, 105), "page 2 is inset by @page mid's margin");
        Assert.True(IsInk(rendered, 25, 125), "page 2's content starts inside that margin");
    }

    // A float declares no break of its own, but it does not get to step over one either: a
    // `break-after: page` on the box before it ends the page the float would otherwise sit on.
    [Fact(Timeout = 600000)]
    public void A_Float_Follows_A_Forced_Break_Onto_The_Next_Page()
    {
        using var rendered = RenderPrint(
            "<!DOCTYPE html><meta charset=\"utf-8\"><style>"
            + "@page { size: 200px 100px; margin: 0; }"
            + "html,body { margin: 0; padding: 0; }"
            + "</style>"
            + "<div style=\"display:flow-root\">"
            + "<div style=\"break-after:page; height:20px\"></div>"
            + "<div style=\"float:left; width:40px; height:40px; background:#000\"></div>"
            + "</div>");

        Assert.Equal(200, rendered.Height);
        Assert.False(IsInk(rendered, 20, 40), "the float should not stay on the page the break ended");
        Assert.True(IsInk(rendered, 20, 120), "the float belongs after the break");
    }

    // CSS Paged Media 3: the page area is the fixed-positioning containing block, so a fixed box
    // appears once on every page rather than once in the document. WPT's `css-page/fixedpos-*`
    // say it in the text they render: "This should repeat on every page."
    [Fact(Timeout = 600000)]
    public void A_Fixed_Box_Repeats_On_Every_Page()
    {
        using var rendered = RenderPrint(
            PlainPageStyle
            + "<div style=\"position:fixed; bottom:0; left:0; width:50px; height:20px;"
            + " background:#000\"></div>"
            + "<div style=\"height:250px\"></div>");

        Assert.Equal(300, rendered.Height);
        Assert.True(IsInk(rendered, 10, 90), "page 1 carries the fixed box");
        Assert.True(IsInk(rendered, 10, 190), "page 2 carries it too");
        Assert.True(IsInk(rendered, 10, 290), "and so does page 3");
    }

    // And it is anchored by its bottom *margin edge*, so its own height comes off the page area's
    // bottom — including when that height is the content's and is only known once the box has been
    // laid out. Placing it before then anchors it by its top edge instead, which in a paged render
    // drops it onto the page after the one it belongs to.
    [Fact(Timeout = 600000)]
    public void A_Bottom_Anchored_Fixed_Box_Sized_By_Its_Content_Sits_On_The_Page_Area()
    {
        using var rendered = RenderPrint(
            PlainPageStyle
            + "<div style=\"position:fixed; bottom:0; left:0; width:50px; background:#000\">"
            + "<div style=\"height:20px\"></div></div>"
            + "<div style=\"height:250px\"></div>");

        Assert.True(IsInk(rendered, 10, 90), "the box ends at the bottom of page 1's area");
        Assert.False(IsInk(rendered, 10, 70), "and does not start further up than its own height");
        Assert.True(IsInk(rendered, 10, 190), "page 2 gets the same box in the same place");
    }

    // A box anchored by `bottom` whose height is its content's is placed twice: once before its
    // children are laid out and again once they have been. The first placement must not put it
    // below the containing block's bottom edge, because the running maximum that decides how tall
    // the document is keeps the overshoot after the box has been corrected — and in a paged render
    // a document 36px too tall is a whole extra blank page.
    [Fact(Timeout = 600000)]
    public void A_Bottom_Anchored_Box_Sized_By_Its_Content_Adds_No_Page()
    {
        // The masked 36x36 square and the two full-page blocks are `fixedpos-009-print`'s own
        // reference, cut down: it is two pages of `height: 100vh` and came out three.
        const string Pencil =
            ".pencil { background-color: #000;"
            + " mask-image: url(data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIyNCIgaGVpZ2h0PSIyNCI+PHBhdGggZD0iTTMgMTcuMjVWMjFoMy43NUwxNy44MSA5Ljk0bC0zLjc1LTMuNzVMMyAxNy4yNXpNMjAuNzEgNy4wNGEuOTk2Ljk5NiAwIDAgMCAwLTEuNDFsLTIuMzQtMi4zNGEuOTk2Ljk5NiAwIDAgMC0xLjQxIDBsLTEuODMgMS44MyAzLjc1IDMuNzUgMS44My0xLjgzeiIvPjxwYXRoIGQ9Ik0wIDBoMjR2MjRIMHoiIGZpbGw9Im5vbmUiLz48L3N2Zz4=);"
            + " mask-repeat: no-repeat; width: 36px; height: 36px; }";

        using var rendered = RenderPrint(
            PlainPageStyle
            + "<style>.p { position: relative; height: 100vh } " + Pencil + "</style>"
            + "<div class=\"p\"><div style=\"position:absolute; bottom:0; right:0\">"
            + "<div class=\"pencil\"></div></div>A</div>"
            + "<div class=\"p\"><div style=\"position:absolute; bottom:0; right:0\">"
            + "<div class=\"pencil\"></div></div>B</div>");

        Assert.Equal(200, rendered.Height);
    }

    // A 200x100 sheet with no page margin, for the tests that read a position off the page area.
    private const string PlainPageStyle =
        "<!DOCTYPE html><meta charset=\"utf-8\"><style>"
        + "@page { size: 200px 100px; margin: 0; }"
        + "html,body { margin: 0; padding: 0; }"
        + "</style>";

    // A 200x100 sheet whose `mid` page carries a 20px margin and whose unconditional page carries
    // none, so which box a page took is readable from where its content starts.
    private const string InsetStyle =
        "<!DOCTYPE html><meta charset=\"utf-8\"><style>"
        + "@page { size: 200px 100px; margin: 0; }"
        + "@page mid { margin: 20px; }"
        + "html,body { margin: 0; padding: 0; }"
        + "</style>";

    // Nothing declares a break here: the content is simply taller than one page area, and the
    // bands are cut from a continuous surface, so it continues on the next page by itself. That is
    // the whole of automatic fragmentation in this model.
    [Fact(Timeout = 600000)]
    public void Content_Taller_Than_The_Page_Area_Continues_On_The_Next_Page()
    {
        using var rendered = RenderPrint(
            PageStyle + "<div style=\"height:60px\"></div><div style=\"height:60px\"></div>");

        Assert.Equal(200, rendered.Height);
        Assert.True(IsInk(rendered, 100, 130), "page 2 should carry the remainder of the flow");
    }

    // Content is placed at the page's margin origin, not at the sheet's corner: the top-left 10px
    // of every page box is margin, and the page area starts inside it.
    [Fact(Timeout = 600000)]
    public void Content_Is_Placed_Inside_The_Page_Margin()
    {
        using var rendered = RenderPrint(PageStyle + "<div></div>");

        Assert.False(IsInk(rendered, 5, 5), "the page margin should be blank");
        Assert.True(IsInk(rendered, 100, 20), "the page area should carry the block");
    }

    // Off by default, and then a print test renders on the ordinary viewport like any other — the
    // lever is what decides, so this is the control that says so.
    [Fact(Timeout = 600000)]
    public void With_The_Lever_Off_A_Print_Test_Renders_On_The_Viewport()
    {
        WptTestRunner.PagedPrint = false;

        using var rendered = RenderPrint(PageStyle + "<div></div>");

        Assert.Equal(400, rendered.Width);
        Assert.Equal(300, rendered.Height);
    }

    // ---- CSS Paged Media 3 §5: margin boxes ----

    // A 300x200 sheet with 50px margins: a 200x100 page area, and a 200x50 top edge strip.
    private const string MarginBoxPage =
        "<!DOCTYPE html><meta charset=\"utf-8\"><style>"
        + "@page { size: 300px 200px; margin: 50px; @SLOTS }"
        + "html,body { margin: 0; padding: 0; }"
        + "</style><div style=\"height:10px\"></div>";

    private static BBitmap RenderMarginBoxes(string slots) =>
        RenderPrint(MarginBoxPage.Replace("@SLOTS", slots));

    [Fact(Timeout = 600000)]
    public void A_Margin_Box_Paints_In_Its_Own_Slot()
    {
        using var rendered = RenderMarginBoxes("@top-left { content: \"\"; background: #000; }");

        Assert.True(IsInk(rendered, 60, 25), "the top-left box should cover the start of the top strip");
        Assert.False(IsInk(rendered, 150, 120), "the page area should be untouched");
    }

    [Theory]
    [InlineData("@top-left-corner { content: \"\"; background:#000; }", 25, 25)]
    [InlineData("@top-right-corner { content: \"\"; background:#000; }", 275, 25)]
    [InlineData("@bottom-left-corner { content: \"\"; background:#000; }", 25, 175)]
    [InlineData("@bottom-right-corner { content: \"\"; background:#000; }", 275, 175)]
    [InlineData("@left-middle { content: \"\"; background:#000; }", 25, 100)]
    [InlineData("@right-middle { content: \"\"; background:#000; }", 275, 100)]
    [InlineData("@bottom-center { content: \"\"; background:#000; }", 150, 175)]
    public void Each_Slot_Is_Where_The_Page_Margin_Puts_It(string slot, int x, int y)
    {
        using var rendered = RenderMarginBoxes(slot);

        Assert.True(IsInk(rendered, x, y), $"expected the box at ({x},{y})");
    }

    // A box on its own fills its edge; two of them share it. Both are the same rule — CSS Paged
    // Media 3 §5.3.2 gives the unused length to the boxes that state none.
    [Fact(Timeout = 600000)]
    public void Two_Auto_Boxes_Share_Their_Edge()
    {
        using var rendered = RenderMarginBoxes(
            "@top-left { content: \"\"; background:#000; } @top-right { content: \"\"; background:#00f; }");

        Assert.True(IsInk(rendered, 60, 25), "the left box should hold the start of the strip");
        Assert.Equal(255, rendered.GetPixel(240, 25).B);
    }

    [Fact(Timeout = 600000)]
    public void A_Stated_Width_Is_Kept_And_The_Rest_Goes_To_The_Auto_Box()
    {
        using var rendered = RenderMarginBoxes(
            "@top-left { content: \"\"; background:#000; width: 40px; }"
            + "@top-right { content: \"\"; background:#00f; }");

        Assert.True(IsInk(rendered, 60, 25), "the stated 40px should start the strip");
        Assert.False(IsInk(rendered, 130, 25), "and end at 40px");
        Assert.Equal(255, rendered.GetPixel(130, 25).B);
    }

    // `content: none`, `content: normal` and no content at all generate no box; an empty string
    // generates one. margin-boxes/content-001 is built on the difference.
    [Theory]
    [InlineData("content: none;", false)]
    [InlineData("content: normal;", false)]
    [InlineData("background: #000;", false)]
    [InlineData("content: \"\";", true)]
    public void A_Box_Exists_Only_When_Its_Content_Does(string declarations, bool painted)
    {
        using var rendered = RenderMarginBoxes($"@top-left {{ background:#000; {declarations} }}");

        Assert.Equal(painted, IsInk(rendered, 60, 25));
    }

    // A running header: the same margin box drawn on every page, which is what page margin boxes
    // are for. The overlay is built per page rather than once, and this is what says so.
    [Fact(Timeout = 600000)]
    public void A_Margin_Box_Is_Drawn_On_Every_Page()
    {
        const string html =
            "<!DOCTYPE html><meta charset=\"utf-8\"><style>"
            + "@page { size: 300px 200px; margin: 50px;"
            + "  @top-center { content: \"header\"; font: 20px monospace; } }"
            + "html,body { margin: 0; padding: 0; }"
            + "div { height: 60px; }"
            + "</style><div></div><div style=\"break-before:page\"></div>";

        using var rendered = RenderPrint(html);

        Assert.Equal(400, rendered.Height);
        Assert.True(TopStripInk(rendered, page: 0) > 0, "page 1 should carry the header");
        Assert.True(TopStripInk(rendered, page: 1) > 0, "page 2 should carry the header");
    }

    // The page counters are reset per page on the overlay's root, so `counter(page)` has the right
    // value to read — but nothing reads it yet: the engine's `content` support takes a single
    // quoted string and renders anything else as the text it is written as, `counter(page)` and
    // `"a" "b"` alike.
    //
    // Which is why this is *not* worked around here. WPT's own references state the same content
    // through a `::before` rule, so both sides of every comparison render the same literal text and
    // agree; evaluating the counters on the test side alone would separate them and lose the eight
    // margin-box tests that use one. The fix is generated content in the engine, and it fixes both
    // sides at once.
    [Fact(Timeout = 600000)]
    public void A_Page_Counter_Renders_As_Written_Until_The_Engine_Evaluates_Content()
    {
        const string html =
            "<!DOCTYPE html><meta charset=\"utf-8\"><style>"
            + "@page { size: 300px 200px; margin: 50px;"
            + "  @top-center { content: counter(page); font: 20px monospace; } }"
            + "html,body { margin: 0; padding: 0; }"
            + "div { height: 60px; }"
            + "</style><div></div><div style=\"break-before:page\"></div>";

        using var rendered = RenderPrint(html);

        Assert.Equal(TopStripInk(rendered, page: 0), TopStripInk(rendered, page: 1));
        Assert.True(TopStripInk(rendered, page: 0) > 0);
    }

    /// <summary>Ink in one page's top margin strip, which is where a running header goes.</summary>
    private static int TopStripInk(BBitmap bitmap, int page)
    {
        int ink = 0;
        for (int y = page * 200; y < page * 200 + 50; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (IsInk(bitmap, x, y))
                    ink++;

        return ink;
    }

    [Theory]
    [InlineData("block-page-break-1-print.html", true)]
    [InlineData("multicol-height-002-print.xht", true)]
    [InlineData("scope-implicit-004-print.xhtml", true)]
    [InlineData("block-page-break-print-ref.html", false)]
    [InlineData("printing-basics.html", false)]
    [InlineData("reprint.html", false)]
    public void A_Print_Test_Is_Recognised_By_The_Stem_Of_Its_Name(string name, bool expected) =>
        Assert.Equal(expected, WptTestRunner.IsPrintTestPath(name));

    private static bool IsInk(BBitmap bitmap, int x, int y)
    {
        var pixel = bitmap.GetPixel(x, y);
        return pixel.R < 100 && pixel.G < 100 && pixel.B < 100;
    }

    /// <summary>Renders <paramref name="html"/> as a file whose name marks it a print test.</summary>
    private static BBitmap RenderPrint(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-paged-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "paged-print.html");
            File.WriteAllText(file, html);
            return new WptTestRunner(400, 300).RenderHtmlFileBitmapPublic(file, dir);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
