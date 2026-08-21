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

    [Fact(Timeout = 600000)]
    public void A_Document_With_No_Page_Rule_Keeps_The_Default_Box_And_No_Margins()
    {
        var box = WptPageBox.Resolve("<!DOCTYPE html><html><body></body></html>", DefaultBox);

        Assert.Equal(DefaultBox, box.BoxSize);
        Assert.Equal(DefaultBox, box.AreaSize);
    }

    // The shape css/CSS2/pagination is built on: a 5in by 3in sheet with half-inch margins is a
    // four-by-two-inch page area. Getting this wrong is what cut those tests at 768px.
    [Fact(Timeout = 600000)]
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

    [Fact(Timeout = 600000)]
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

    [Fact(Timeout = 600000)]
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

    // A caller that knows its pages one by one asks for a name explicitly, and an empty name says
    // "take no named rule at all" — which is what a paged render wants, because guessing one name
    // for the whole document puts one page's margins on every page.
    // page-name-unnamed-trailing-001 is the case: it uses exactly one *name*, but its flow starts
    // unnamed, so the guess gave it a 260px page area and a fourth page.
    [Fact(Timeout = 600000)]
    public void An_Explicit_Page_Name_Replaces_The_Document_Wide_Guess()
    {
        const string html = "<!DOCTYPE html><html><head><style>"
            + "@page { size: 200px 300px; margin: 0; } @page landscape { margin: 20px; }"
            + "</style></head><body><div style=\"page: landscape\"></div></body></html>";

        // The guess: one name used, so it is taken for the whole document.
        Assert.Equal(20, WptPageBox.Resolve(html, DefaultBox).MarginTop, 3);

        // Asked for by name: the same rule, but because the caller said so.
        Assert.Equal(20, WptPageBox.Resolve(html, DefaultBox, pageName: "landscape").MarginTop, 3);

        // Asked for no name at all: the unconditional rule alone.
        Assert.Equal(0, WptPageBox.Resolve(html, DefaultBox, pageName: string.Empty).MarginTop, 3);
    }

    // A name the document declares no rule for leaves the unconditional one standing.
    [Fact(Timeout = 600000)]
    public void An_Unknown_Page_Name_Falls_Back_To_The_Unconditional_Rule()
    {
        const string html = "<!DOCTYPE html><html><head><style>"
            + "@page { size: 200px 300px; margin: 5px; } @page landscape { margin: 20px; }"
            + "</style></head><body><div style=\"page: landscape\"></div></body></html>";

        Assert.Equal(5, WptPageBox.Resolve(html, DefaultBox, pageName: "nosuchpage").MarginTop, 3);
    }

    // ---- page one's own box ----------------------------------------------

    // ...except when the caller is asking for page one specifically, which only a paged render
    // does. `:first` describes exactly one page, so a surface that prints that page can carry it.
    // page-rule-specificity-print-portrait-ref states its whole geometry that way: one page, sized
    // by `@page :first { size: portrait }` alone.
    [Fact(Timeout = 600000)]
    public void First_Page_Reads_The_First_Page_Rule()
    {
        const string html = "<!DOCTYPE html><html><head><style>"
            + "@page :first { size: 400px; }"
            + "</style></head><body></body></html>";

        Assert.Equal(DefaultBox, WptPageBox.Resolve(html, DefaultBox).BoxSize);
        Assert.Equal(
            new SizeF(400, 400),
            WptPageBox.Resolve(html, DefaultBox, firstPage: true).BoxSize);
    }

    // `:first` is the most specific of the three, so it layers over the unconditional rule and over
    // the used named one — page-rule-specificity-002-print states `@page :first { size: portrait }`
    // beside `@page { size: landscape }` and expects the first page portrait.
    [Fact(Timeout = 600000)]
    public void The_First_Page_Rule_Layers_Over_The_Unconditional_One()
    {
        const string html = "<!DOCTYPE html><html><head><style>"
            + "@page { size: 5in 3in; margin: 10px; } @page :first { size: 3in 5in; }"
            + "</style></head><body></body></html>";

        var box = WptPageBox.Resolve(html, DefaultBox, firstPage: true);

        Assert.Equal(new SizeF(288, 480), box.BoxSize);
        Assert.Equal(10, box.MarginTop, 3);   // still the unconditional rule's
    }

    // The other pseudo-classes describe pages this model still cannot single out, so `:first` does
    // not open the door to them.
    [Theory]
    [InlineData("@page :left { size: 400px; }")]
    [InlineData("@page :right { size: 400px; }")]
    [InlineData("@page :blank { size: 400px; }")]
    public void Only_First_Is_Read_For_Page_One(string rule)
    {
        var html = $"<!DOCTYPE html><html><head><style>{rule}</style></head><body></body></html>";

        Assert.Equal(DefaultBox, WptPageBox.Resolve(html, DefaultBox, firstPage: true).BoxSize);
    }

    // ---- the named page the document actually uses ----

    // The runner renders one sheet and that sheet is page one, so it takes the box of the page the
    // flow starts on. page-name-table-001-print is the pair: a table on `page: square`, a
    // `@page square` sizing the sheet 5in, and a reference that spells the same page as the
    // unconditional rule. Reading only the unconditional rule scored it 0.0 %.
    [Fact(Timeout = 600000)]
    public void The_One_Named_Page_The_Document_Uses_Is_Applied()
    {
        const string html = "<!DOCTYPE html><html><head><style>"
            + "@page { size: 100px; } @page square { size: 5in; }"
            + "</style></head><body><table style=\"page: square;\"></table></body></html>";

        Assert.Equal(new SizeF(480, 480), WptPageBox.Resolve(html, DefaultBox).BoxSize);
    }

    // The name can come from a style rule rather than a style attribute.
    [Fact(Timeout = 600000)]
    public void A_Page_Name_From_A_Style_Rule_Counts_As_Used()
    {
        const string html = "<!DOCTYPE html><html><head><style>"
            + "table { page: square; } @page square { size: 5in; }"
            + "</style></head><body><table></table></body></html>";

        Assert.Equal(new SizeF(480, 480), WptPageBox.Resolve(html, DefaultBox).BoxSize);
    }

    // Two names need a per-page box, which one surface cannot carry, so neither is taken.
    // page-margin-auto-print names six pages and must stay exactly as it was.
    [Fact(Timeout = 600000)]
    public void Two_Named_Pages_Leave_The_Unconditional_Box_Alone()
    {
        const string html = "<!DOCTYPE html><html><head><style>"
            + "@page { size: 100px; } @page aaa { size: 5in; } @page bbb { size: 7in; }"
            + "</style></head><body>"
            + "<div style=\"page: aaa;\"></div><div style=\"page: bbb;\"></div>"
            + "</body></html>";

        Assert.Equal(new SizeF(100, 100), WptPageBox.Resolve(html, DefaultBox).BoxSize);
    }

    // `auto` is the initial value and names no page.
    [Fact(Timeout = 600000)]
    public void Page_Auto_Names_No_Page()
    {
        const string html = "<!DOCTYPE html><html><head><style>"
            + "@page { size: 100px; } @page square { size: 5in; }"
            + "</style></head><body><div style=\"page: auto;\"></div></body></html>";

        Assert.Equal(new SizeF(100, 100), WptPageBox.Resolve(html, DefaultBox).BoxSize);
    }

    // A named rule the document never puts content on is still ignored — which is what keeps this
    // narrower than the earlier attempt that read every selectored rule.
    [Fact(Timeout = 600000)]
    public void A_Named_Page_Nothing_Uses_Is_Ignored()
    {
        const string html = "<!DOCTYPE html><html><head><style>"
            + "@page { size: 100px; } @page square { size: 5in; }"
            + "</style></head><body><div></div></body></html>";

        Assert.Equal(new SizeF(100, 100), WptPageBox.Resolve(html, DefaultBox).BoxSize);
    }

    // The named rule layers over the unconditional one, whichever order they are written in.
    [Theory]
    [InlineData("@page { size: 100px; margin: 4px; } @page square { margin: 9px; }")]
    [InlineData("@page square { margin: 9px; } @page { size: 100px; margin: 4px; }")]
    public void The_Named_Rule_Layers_Over_The_Unconditional_One(string rules)
    {
        var html = "<!DOCTYPE html><html><head><style>" + rules
            + "</style></head><body><div style=\"page: square;\"></div></body></html>";

        var box = WptPageBox.Resolve(html, DefaultBox);

        Assert.Equal(new SizeF(100, 100), box.BoxSize);
        Assert.Equal(9, box.MarginTop, 3);
    }

    // A pseudo-class describes particular pages of a flow this model does not paginate, so it is
    // never read — even when the document uses exactly one named page beside it.
    [Fact(Timeout = 600000)]
    public void A_Pseudo_Class_Selector_Is_Never_Read()
    {
        const string html = "<!DOCTYPE html><html><head><style>"
            + "@page { size: 100px; } @page :first { size: 5in; } @page square:first { size: 7in; }"
            + "</style></head><body><div style=\"page: square;\"></div></body></html>";

        Assert.Equal(new SizeF(100, 100), WptPageBox.Resolve(html, DefaultBox).BoxSize);
    }

    // ...but the unconditional rule in the same sheet still applies.
    [Fact(Timeout = 600000)]
    public void The_Unconditional_Rule_Applies_Alongside_Selectored_Ones()
    {
        const string html = "<!DOCTYPE html><html><head><style>"
            + "@page { size: 400px; margin: 20px; } @page :first { margin: 0; }"
            + "</style></head><body></body></html>";

        var box = WptPageBox.Resolve(html, DefaultBox);

        Assert.Equal(new SizeF(400, 400), box.BoxSize);
        Assert.Equal(20, box.MarginTop, 3);
    }

    [Fact(Timeout = 600000)]
    public void Later_Page_Rules_Win()
    {
        const string html = "<!DOCTYPE html><html><head>"
            + "<style>@page { size: 400px; }</style><style>@page { size: 200px; }</style>"
            + "</head><body></body></html>";

        Assert.Equal(new SizeF(200, 200), WptPageBox.Resolve(html, DefaultBox).BoxSize);
    }

    // Margins can exceed the sheet in a malformed document; the area must still be usable rather
    // than zero or negative, because it is divided by to get a page count.
    [Fact(Timeout = 600000)]
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

    [Fact(Timeout = 600000)]
    public void A_Font_Size_On_The_Page_Is_What_Em_Resolves_Against()
    {
        var box = WptPageBox.Resolve(Page("font-size: 10px; size: 32em 28em;"), DefaultBox);

        Assert.Equal(320, box.BoxSize.Width, 1);
        Assert.Equal(280, box.BoxSize.Height, 1);
    }

    // `width` and `height` size the page *area* the way they size any other box's content, so the
    // sheet is that plus its margins. margin-boxes/dimensions-011 writes the same page both ways:
    // `width: 20em; height: 16em; margin: 6em` against `size: 32em 28em; margin: 0`.
    [Fact(Timeout = 600000)]
    public void Width_And_Height_Size_The_Page_Area_And_The_Margins_Are_Added()
    {
        var box = WptPageBox.Resolve(Page("margin: 6em; width: 20em; height: 16em;"), DefaultBox);

        Assert.Equal(new SizeF(512, 448), box.BoxSize);
        Assert.Equal(new SizeF(320, 256), box.AreaSize);
    }

    // ---- flow-relative margins ----

    // css-page-3 §3.2 lets the page box carry the flow-relative margins, and the two tests that
    // state them disagree on purpose about where the writing mode comes from: page-box-008-print
    // puts `vertical-rl` on the root element, page-box-009-print puts it on the `@page` rule. Both
    // expect the same 16/32/48/80 ring on a 400x800 page from 2/8/6/20 %, which is each percentage
    // taken against the dimension its physical side runs along.
    [Theory]
    [InlineData(":root { writing-mode: vertical-rl; }", "")]
    [InlineData("", "writing-mode: vertical-rl;")]
    public void Logical_Margins_Resolve_Through_The_Pages_Writing_Mode(string rootCss, string pageCss)
    {
        var html = "<!DOCTYPE html><html><head><style>" + rootCss + "@page { " + pageCss
            + "size: 400px 800px;"
            + "margin-inline-start: 2%; margin-block-start: 8%;"
            + "margin-inline-end: 6%; margin-block-end: 20%; }"
            + "</style></head><body></body></html>";

        var box = WptPageBox.Resolve(html, DefaultBox);

        Assert.Equal(16, box.MarginTop, 3);      // inline-start, 2 % of the 800px height
        Assert.Equal(32, box.MarginRight, 3);    // block-start,  8 % of the 400px width
        Assert.Equal(48, box.MarginBottom, 3);   // inline-end,   6 % of the height
        Assert.Equal(80, box.MarginLeft, 3);     // block-end,   20 % of the width
    }

    // The initial axes map block to vertical and inline to horizontal, so a document that states no
    // writing mode reads the flow-relative names as the physical ones they coincide with.
    [Fact(Timeout = 600000)]
    public void Logical_Margins_On_The_Initial_Axes_Are_The_Physical_Ones()
    {
        var box = WptPageBox.Resolve(
            Page("margin-block-start: 1px; margin-block-end: 2px;"
                + "margin-inline-start: 3px; margin-inline-end: 4px;"),
            DefaultBox);

        Assert.Equal(1, box.MarginTop, 3);
        Assert.Equal(2, box.MarginBottom, 3);
        Assert.Equal(3, box.MarginLeft, 3);
        Assert.Equal(4, box.MarginRight, 3);
    }

    // `direction: rtl` reverses the inline axis and leaves the block axis alone.
    [Fact(Timeout = 600000)]
    public void Rtl_Swaps_The_Inline_Sides()
    {
        const string html = "<!DOCTYPE html><html><head><style>"
            + "html { direction: rtl; }"
            + "@page { margin-inline-start: 3px; margin-block-start: 1px; }"
            + "</style></head><body></body></html>";

        var box = WptPageBox.Resolve(html, DefaultBox);

        Assert.Equal(3, box.MarginRight, 3);
        Assert.Equal(1, box.MarginTop, 3);
    }

    // A writing mode further down the tree styles a box inside the page, not the page.
    [Fact(Timeout = 600000)]
    public void Only_The_Root_Elements_Writing_Mode_Turns_The_Page()
    {
        const string html = "<!DOCTYPE html><html><head><style>"
            + "body { writing-mode: vertical-rl; }"
            + "@page { margin-block-start: 7px; }"
            + "</style></head><body></body></html>";

        Assert.Equal(7, WptPageBox.Resolve(html, DefaultBox).MarginTop, 3);
    }

    // A flow-relative longhand is a longhand: it overrides the shorthand it follows, and is
    // overridden by the physical longhand that follows it.
    [Fact(Timeout = 600000)]
    public void Logical_And_Physical_Margins_Cascade_In_Source_Order()
    {
        var box = WptPageBox.Resolve(
            Page("margin: 10px; margin-block-start: 20px; margin-inline-start: 30px;"
                + "margin-left: 40px;"),
            DefaultBox);

        Assert.Equal(20, box.MarginTop, 3);
        Assert.Equal(40, box.MarginLeft, 3);
        Assert.Equal(10, box.MarginRight, 3);
    }

    // ---- `auto` page margins ----------------------------------------------

    // css-page-3 §3.2: with a `size` fixing the box and `width`/`height` fixing the area inside it,
    // the margins absorb the difference — so `auto` centres the page area. page-margin-auto-print
    // states exactly this page: 20em x 7em holding a 12em x 3em area.
    [Fact(Timeout = 600000)]
    public void An_Auto_Margin_Centres_The_Page_Area_In_Its_Box()
    {
        var box = WptPageBox.Resolve(
            Page("size: 20em 7em; width: 12em; height: 3em; margin: auto;"), DefaultBox);

        Assert.Equal(new SizeF(320, 112), box.BoxSize);
        Assert.Equal(new SizeF(192, 48), box.AreaSize);
        Assert.Equal(64, box.MarginLeft, 3);
        Assert.Equal(64, box.MarginRight, 3);
        Assert.Equal(32, box.MarginTop, 3);
        Assert.Equal(32, box.MarginBottom, 3);
    }

    // One `auto` beside a stated margin takes the whole remainder rather than half of it, which is
    // how page-margin-auto-print's `@page bbb { margin-top: 0 }` pushes the area to the top.
    [Theory]
    [InlineData("margin: auto; margin-top: 0;", 0, 64)]
    [InlineData("margin: auto; margin-bottom: 0;", 64, 0)]
    public void A_Single_Auto_Takes_The_Whole_Remainder(string extra, float top, float bottom)
    {
        var box = WptPageBox.Resolve(
            Page("size: 20em 7em; width: 12em; height: 3em;" + extra), DefaultBox);

        Assert.Equal(top, box.MarginTop, 3);
        Assert.Equal(bottom, box.MarginBottom, 3);
    }

    // The remainder is signed: an area larger than its box hangs off every edge rather than
    // clamping to zero. page-margin-auto-negative-print states a 300px page with a 340px area and
    // expects exactly -20 on each side.
    [Fact(Timeout = 600000)]
    public void An_Area_Larger_Than_Its_Box_Gives_Negative_Auto_Margins()
    {
        var box = WptPageBox.Resolve(
            Page("size: 300px; width: 340px; height: 340px; margin: auto;"), DefaultBox);

        Assert.Equal(new SizeF(300, 300), box.BoxSize);
        Assert.Equal(-20, box.MarginLeft, 3);
        Assert.Equal(-20, box.MarginRight, 3);
        Assert.Equal(-20, box.MarginTop, 3);
        Assert.Equal(-20, box.MarginBottom, 3);
        Assert.Equal(new SizeF(340, 340), box.AreaSize);
    }

    // With no area stated there is nothing for an `auto` margin to take, so it is zero — and the
    // page keeps the size it declared rather than collapsing to it.
    [Fact(Timeout = 600000)]
    public void Auto_With_No_Stated_Area_Is_Zero()
    {
        var box = WptPageBox.Resolve(Page("size: 300px; margin: auto;"), DefaultBox);

        Assert.Equal(new SizeF(300, 300), box.BoxSize);
        Assert.Equal(0, box.MarginTop, 3);
        Assert.Equal(0, box.MarginLeft, 3);
    }

    // With no `auto` on the axis the area plus its margins *is* the box, and a declared `size`
    // gives way to it. page-size-013-print states this over-constrained resolution outright, and
    // its reference spells the answer out: `size: 500px; margin: 50px; width: 200px; height: 300px`
    // is the same page as `size: 300px 400px; margin: 50px`.
    [Fact(Timeout = 600000)]
    public void An_Over_Constrained_Axis_Resizes_The_Box_Around_Its_Area()
    {
        var box = WptPageBox.Resolve(
            Page("size: 500px; margin: 50px; width: 200px; height: 300px;"), DefaultBox);

        Assert.Equal(new SizeF(300, 400), box.BoxSize);
        Assert.Equal(new SizeF(200, 300), box.AreaSize);
        Assert.Equal(50, box.MarginLeft, 3);
    }

    // `auto` inside the shorthand used to reject the whole declaration, leaving every margin at
    // zero; it expands like any other component.
    [Fact(Timeout = 600000)]
    public void Auto_Expands_Through_The_Shorthand()
    {
        var box = WptPageBox.Resolve(
            Page("size: 20em 7em; width: 12em; height: 3em; margin: 10px auto;"), DefaultBox);

        Assert.Equal(10, box.MarginTop, 3);
        Assert.Equal(10, box.MarginBottom, 3);
        Assert.Equal(64, box.MarginLeft, 3);
        Assert.Equal(64, box.MarginRight, 3);
    }

    // A declared `size` survives a declared area only when an `auto` margin is there to absorb the
    // difference — which is the one thing that separates page-margin-auto-print (keeps its 20em x
    // 7em sheet) from page-size-013-print (resized to 300x400).
    [Fact(Timeout = 600000)]
    public void A_Declared_Size_Survives_A_Declared_Area_Only_Under_Auto()
    {
        Assert.Equal(
            new SizeF(320, 112),
            WptPageBox.Resolve(
                Page("size: 20em 7em; width: 12em; height: 3em; margin: auto;"), DefaultBox).BoxSize);

        Assert.Equal(
            new SizeF(192, 48),
            WptPageBox.Resolve(
                Page("size: 20em 7em; width: 12em; height: 3em; margin: 0;"), DefaultBox).BoxSize);
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
