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

    [Fact]
    public void A_Single_Page_Document_Renders_One_Page_Box()
    {
        using var rendered = RenderPrint(PageStyle + "<div></div>");

        Assert.Equal(200, rendered.Width);
        Assert.Equal(100, rendered.Height);
    }

    // A forced break puts the second block on page two, and the output grows by one page box —
    // the page count is the render's own, not a fixed number of bands.
    [Fact]
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
    [Fact]
    public void A_Change_Of_Page_Name_Adds_The_Same_Page()
    {
        using var rendered = RenderPrint(
            PageStyle + "<div style=\"page: a\"></div><div style=\"page: b\"></div>");

        Assert.Equal(200, rendered.Height);
        Assert.True(IsInk(rendered, 100, 120), "page 2 should carry the second block");
    }

    [Fact]
    public void The_Same_Page_Name_On_Both_Blocks_Adds_No_Page()
    {
        using var rendered = RenderPrint(
            PageStyle + "<div style=\"page: a\"></div><div style=\"page: a\"></div>");

        Assert.Equal(100, rendered.Height);
    }

    // Nothing declares a break here: the content is simply taller than one page area, and the
    // bands are cut from a continuous surface, so it continues on the next page by itself. That is
    // the whole of automatic fragmentation in this model.
    [Fact]
    public void Content_Taller_Than_The_Page_Area_Continues_On_The_Next_Page()
    {
        using var rendered = RenderPrint(
            PageStyle + "<div style=\"height:60px\"></div><div style=\"height:60px\"></div>");

        Assert.Equal(200, rendered.Height);
        Assert.True(IsInk(rendered, 100, 130), "page 2 should carry the remainder of the flow");
    }

    // Content is placed at the page's margin origin, not at the sheet's corner: the top-left 10px
    // of every page box is margin, and the page area starts inside it.
    [Fact]
    public void Content_Is_Placed_Inside_The_Page_Margin()
    {
        using var rendered = RenderPrint(PageStyle + "<div></div>");

        Assert.False(IsInk(rendered, 5, 5), "the page margin should be blank");
        Assert.True(IsInk(rendered, 100, 20), "the page area should carry the block");
    }

    // Off by default, and then a print test renders on the ordinary viewport like any other — the
    // lever is what decides, so this is the control that says so.
    [Fact]
    public void With_The_Lever_Off_A_Print_Test_Renders_On_The_Viewport()
    {
        WptTestRunner.PagedPrint = false;

        using var rendered = RenderPrint(PageStyle + "<div></div>");

        Assert.Equal(400, rendered.Width);
        Assert.Equal(300, rendered.Height);
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
