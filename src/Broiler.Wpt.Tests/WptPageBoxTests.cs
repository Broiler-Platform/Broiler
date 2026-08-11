using System.Drawing;
using Broiler.Wpt;
using Xunit;

namespace Broiler.Wpt.Tests;

/// <summary>
/// <see cref="WptPageBox"/> — resolving CSS Paged Media 3's <c>@page</c> <c>size</c> and
/// <c>margin</c> from a document, which is what defines where a paged render's pages actually are.
/// </summary>
/// <remarks>
/// <para>
/// The page <em>area</em> — the box less its margins — is the containing block content lays out in,
/// what <c>vw</c>/<c>vh</c> resolve against, and where the fragmentation boundaries fall. Nothing
/// else defines it, which is the finding that motivated this: paginating at the viewport instead is
/// not an approximation of the page area but a different set of boundaries, and it moved the print
/// reftests 252 → 228 when tried.
/// </para>
/// <para>
/// Consumed by <c>WptDocumentRenderer.RenderPaged</c> behind <see cref="WptTestRunner.PagedPrint"/>
/// — see <see cref="PagedPrintRenderTests"/> for the render it defines and for what the paged run
/// currently scores.
/// </para>
/// </remarks>
public sealed class WptPageBoxTests
{
    private static readonly SizeF DefaultBox = new(1024, 768);

    private static string Page(string declarations) =>
        $"<!DOCTYPE html><html><head><style>@page {{ {declarations} }}</style></head><body></body></html>";

    [Fact]
    public void A_Document_With_No_Page_Rule_Keeps_The_Default_Box_And_No_Margins()
    {
        var box = WptPageBox.Resolve("<!DOCTYPE html><html><body></body></html>", DefaultBox);

        Assert.Equal(DefaultBox, box.BoxSize);
        Assert.Equal(DefaultBox, box.AreaSize);
    }

    // The shape css/CSS2/pagination is built on: a 5in by 3in sheet with half-inch margins is a
    // four-by-two-inch page area. Getting this wrong is what cut those tests at 768px.
    [Fact]
    public void Size_And_Margin_Give_The_Page_Area()
    {
        var box = WptPageBox.Resolve(Page("size: 5in 3in; margin: 0.5in;"), DefaultBox);

        Assert.Equal(new SizeF(480, 288), box.BoxSize);
        Assert.Equal(new SizeF(384, 192), box.AreaSize);
    }

    [Theory]
    [InlineData("size: 400px", 400, 400)]              // one length is a square page
    [InlineData("size: 300px 200px", 300, 200)]
    [InlineData("size: 5in 3in", 480, 288)]
    [InlineData("size: 10cm 5cm", 377.95f, 188.976f)]
    [InlineData("size: 100mm 50mm", 377.95f, 188.976f)]
    [InlineData("size: 72pt 36pt", 96, 48)]
    [InlineData("size: A4", 793.7f, 1122.5f)]
    [InlineData("size: letter", 816, 1056)]
    [InlineData("size: legal", 816, 1344)]
    public void Size_Accepts_Lengths_And_Named_Pages(string declaration, float width, float height)
    {
        var box = WptPageBox.Resolve(Page(declaration), DefaultBox);

        Assert.Equal(width, box.BoxSize.Width, 1);
        Assert.Equal(height, box.BoxSize.Height, 1);
    }

    // Orientation rotates a named or default page; a pair of lengths already states its own.
    [Theory]
    [InlineData("size: A4 landscape", 1122.5f, 793.7f)]
    [InlineData("size: landscape", 1024, 768)]
    [InlineData("size: portrait", 768, 1024)]
    [InlineData("size: 300px 200px landscape", 300, 200)]
    public void Size_Honours_Orientation_Keywords(string declaration, float width, float height)
    {
        var box = WptPageBox.Resolve(Page(declaration), DefaultBox);

        Assert.Equal(width, box.BoxSize.Width, 1);
        Assert.Equal(height, box.BoxSize.Height, 1);
    }

    [Fact]
    public void Size_Auto_Keeps_The_Default()
    {
        Assert.Equal(DefaultBox, WptPageBox.Resolve(Page("size: auto"), DefaultBox).BoxSize);
    }

    // The margin shorthand in its four arities, TRBL.
    [Theory]
    [InlineData("margin: 10px", 10, 10, 10, 10)]
    [InlineData("margin: 10px 20px", 10, 20, 10, 20)]
    [InlineData("margin: 10px 20px 30px", 10, 20, 30, 20)]
    [InlineData("margin: 10px 20px 30px 40px", 10, 20, 30, 40)]
    public void Margin_Shorthand_Expands_In_TRBL_Order(
        string declaration, float top, float right, float bottom, float left)
    {
        var box = WptPageBox.Resolve(Page(declaration), DefaultBox);

        Assert.Equal(top, box.MarginTop, 3);
        Assert.Equal(right, box.MarginRight, 3);
        Assert.Equal(bottom, box.MarginBottom, 3);
        Assert.Equal(left, box.MarginLeft, 3);
    }

    [Fact]
    public void Margin_Longhands_Are_Read_And_Override_The_Shorthand()
    {
        var box = WptPageBox.Resolve(Page("margin: 10px; margin-left: 40px;"), DefaultBox);

        Assert.Equal(10, box.MarginTop, 3);
        Assert.Equal(40, box.MarginLeft, 3);
    }

    // A page selector describes particular pages of the flow, and a per-page box size is not
    // something one surface can carry — taking one anyway applies the wrong page's geometry
    // everywhere. css-page/page-name-table-001 is a two-named-page document and was broken by
    // exactly this when an earlier attempt read the selectored rules.
    [Theory]
    [InlineData("@page :first { size: 400px; }")]
    [InlineData("@page square { size: 400px; }")]
    [InlineData("@page :left { size: 400px; }")]
    public void A_Page_Rule_With_A_Selector_Is_Ignored(string rule)
    {
        var html = $"<!DOCTYPE html><html><head><style>{rule}</style></head><body></body></html>";

        Assert.Equal(DefaultBox, WptPageBox.Resolve(html, DefaultBox).BoxSize);
    }

    // ...but the unconditional rule in the same sheet still applies.
    [Fact]
    public void The_Unconditional_Rule_Applies_Alongside_Selectored_Ones()
    {
        const string html = "<!DOCTYPE html><html><head><style>"
            + "@page { size: 400px; margin: 20px; } @page :first { margin: 0; }"
            + "</style></head><body></body></html>";

        var box = WptPageBox.Resolve(html, DefaultBox);

        Assert.Equal(new SizeF(400, 400), box.BoxSize);
        Assert.Equal(20, box.MarginTop, 3);
    }

    [Fact]
    public void Later_Page_Rules_Win()
    {
        const string html = "<!DOCTYPE html><html><head>"
            + "<style>@page { size: 400px; }</style><style>@page { size: 200px; }</style>"
            + "</head><body></body></html>";

        Assert.Equal(new SizeF(200, 200), WptPageBox.Resolve(html, DefaultBox).BoxSize);
    }

    // Margins can exceed the sheet in a malformed document; the area must still be usable rather
    // than zero or negative, because it is divided by to get a page count.
    [Fact]
    public void An_Over_Constrained_Margin_Leaves_A_Usable_Area()
    {
        var box = WptPageBox.Resolve(Page("size: 100px; margin: 200px;"), DefaultBox);

        Assert.True(box.AreaSize.Width >= 1);
        Assert.True(box.AreaSize.Height >= 1);
    }

    // Font-relative lengths, against the page's own font size. margin-boxes/dimensions-011 states
    // its page entirely in em, and its reference states the same page a different way — they only
    // agree if both are resolved.
    [Theory]
    [InlineData("size: 32em 28em", 512, 448)]
    [InlineData("size: 20rem", 320, 320)]
    public void Size_Accepts_Font_Relative_Lengths(string declaration, float width, float height)
    {
        var box = WptPageBox.Resolve(Page(declaration), DefaultBox);

        Assert.Equal(width, box.BoxSize.Width, 1);
        Assert.Equal(height, box.BoxSize.Height, 1);
    }

    [Fact]
    public void A_Font_Size_On_The_Page_Is_What_Em_Resolves_Against()
    {
        var box = WptPageBox.Resolve(Page("font-size: 10px; size: 32em 28em;"), DefaultBox);

        Assert.Equal(320, box.BoxSize.Width, 1);
        Assert.Equal(280, box.BoxSize.Height, 1);
    }

    // `width` and `height` size the page *area* the way they size any other box's content, so the
    // sheet is that plus its margins. margin-boxes/dimensions-011 writes the same page both ways:
    // `width: 20em; height: 16em; margin: 6em` against `size: 32em 28em; margin: 0`.
    [Fact]
    public void Width_And_Height_Size_The_Page_Area_And_The_Margins_Are_Added()
    {
        var box = WptPageBox.Resolve(Page("margin: 6em; width: 20em; height: 16em;"), DefaultBox);

        Assert.Equal(new SizeF(512, 448), box.BoxSize);
        Assert.Equal(new SizeF(320, 256), box.AreaSize);
    }

    [Theory]
    [InlineData("size: nonsense")]
    [InlineData("margin: nonsense")]
    [InlineData("size: -5px")]
    public void An_Unparseable_Value_Leaves_The_Default(string declaration)
    {
        var box = WptPageBox.Resolve(Page(declaration), DefaultBox);

        Assert.Equal(DefaultBox, box.BoxSize);
        Assert.Equal(0, box.MarginTop, 3);
    }
}
