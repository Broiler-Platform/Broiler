using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Fragmentation 3 §3 forced page breaks — <c>break-before</c>/<c>break-after</c> and their
/// legacy <c>page-break-*</c> spellings — placing a box at the top of the next page.
/// </summary>
/// <remarks>
/// <para>
/// The mechanism is exercised directly here rather than through a paged render, because it is
/// governed by the layout environment's page size and nothing else: a page size is in force, so a
/// break boundary exists and a forced break moves the box to it. That is the whole contract, and
/// it is what a paged host — a printer, a PDF export, the WPT print reftests — builds on.
/// </para>
/// <para>
/// <b>Not yet reachable from the WPT runner, deliberately.</b> Forced breaks cannot be switched on
/// for the print reftests until <em>automatic</em> fragmentation exists alongside them. The corpus
/// pairs a test that relies on content overflowing a page with a reference that states the same
/// layout using <c>break-before: page</c> — <c>css-break/grid/monolithic-overflow-print</c> is
/// exactly that — so honouring the forced break while the overflow still runs on makes the two
/// sides disagree where they used to agree. Measured: enabling it moved css/css-page 137 → 143 and
/// css/css-break 423 → 417, a net zero bought by desynchronising six pairs.
/// </para>
/// </remarks>
public sealed class ForcedPageBreakTests
{
    private static readonly Uri BaseUrl = new("file:///page-break.html");

    private const double PageHeight = 200;
    private const double ChildHeight = 50;

    // The property and its legacy alias both reach the box, in both directions.
    [Theory]
    [InlineData("break-before", "page")]
    [InlineData("page-break-before", "page")]
    [InlineData("break-before", "always")]
    [InlineData("break-before", "left")]
    [InlineData("break-before", "right")]
    [InlineData("break-before", "recto")]
    [InlineData("break-before", "verso")]
    public void A_Forced_BreakBefore_Moves_The_Box_To_The_Next_Page(string property, string value)
    {
        var (root, second) = TreeWithTwoChildren();
        CssUtils.SetPropertyValue(second, property, value);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(PageHeight, second.Location.Y, 3);
    }

    // break-after on the *previous* sibling forces the same break.
    [Theory]
    [InlineData("break-after")]
    [InlineData("page-break-after")]
    public void A_Forced_BreakAfter_Moves_The_Following_Box(string property)
    {
        var (root, second) = TreeWithTwoChildren();
        CssUtils.SetPropertyValue(root.Boxes[0], property, "page");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(PageHeight, second.Location.Y, 3);
    }

    // The control: with no break declared the second child follows the first, so a change that
    // simply pushed every box to a page boundary would not pass this.
    [Fact]
    public void Without_A_Forced_Break_The_Box_Follows_Its_Sibling()
    {
        var (root, second) = TreeWithTwoChildren();
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(ChildHeight, second.Location.Y, 3);
    }

    [Theory]
    [InlineData(CssConstants.Auto)]
    [InlineData("avoid")]
    [InlineData("column")]
    public void A_Non_Forcing_Break_Value_Moves_Nothing(string value)
    {
        var (root, second) = TreeWithTwoChildren();
        second.BreakBefore = value;
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(ChildHeight, second.Location.Y, 3);
    }

    // A box already at the top of a page satisfies the break where it stands. Moving it would
    // insert an entirely blank page, which is the one way this can be wrong and still look right
    // on a test whose content happens to start at a boundary.
    [Fact]
    public void A_Box_Already_At_A_Page_Boundary_Does_Not_Move()
    {
        var (root, second) = TreeWithTwoChildren(firstHeight: PageHeight);
        second.BreakBefore = "page";
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(PageHeight, second.Location.Y, 3);
    }

    // Unpaginated is the default, and the whole reason this can be applied unconditionally: with
    // no page size there is no boundary, so a forced break is inert rather than wrong.
    [Fact]
    public void Without_A_Page_Size_A_Forced_Break_Is_Inert()
    {
        var (root, second) = TreeWithTwoChildren(pageHeight: 99999);
        second.BreakBefore = "page";
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(ChildHeight, second.Location.Y, 3);
    }

    // The moved box takes its subtree with it — a break that moved the box and left its content
    // behind would show as text on the wrong page.
    [Fact]
    public void The_Moved_Box_Carries_Its_Descendants()
    {
        var (root, second) = TreeWithTwoChildren();
        var grandchild = new CssBox(second, null, BaseUrl)
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 20),
            Display = CssConstants.Block,
            FontSize = "16px",
        };
        grandchild.Height = "20px";

        second.BreakBefore = "page";
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(second.Location.Y, grandchild.Location.Y, 3);
    }

    /// <summary>A root of two stacked block children; the second is the one under test.</summary>
    private static (CssBox Root, CssBox Second) TreeWithTwoChildren(
        double firstHeight = ChildHeight, double pageHeight = PageHeight)
    {
        var root = new CssBox(null, null, BaseUrl)
        {
            Location = new PointF(0, 0),
            Size = new SizeF(400, 1000),
            Display = CssConstants.Block,
            FontSize = "16px",
            LayoutEnvironment = new StubLayoutEnvironment(pageHeight),
        };

        CssBox Child(double height)
        {
            var box = new CssBox(root, null, BaseUrl)
            {
                Location = new PointF(0, 0),
                Size = new SizeF(400, (float)height),
                Display = CssConstants.Block,
                FontSize = "16px",
            };
            box.Height = height.ToString(System.Globalization.CultureInfo.InvariantCulture) + "px";
            return box;
        }

        Child(firstHeight);
        return (root, Child(ChildHeight));
    }

    private sealed class StubLayoutEnvironment(double pageHeight) : ILayoutEnvironment
    {
        public Broiler.Graphics.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => new StubFont(size);
        public SizeF MeasureText(Broiler.Graphics.ILayoutFont font, string text) => SizeF.Empty;
        public void MeasureText(Broiler.Graphics.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = 0; charFitWidth = 0; }
        public double GetWhitespaceWidth(Broiler.Graphics.ILayoutFont font) => 0;
        public ImageIntrinsics GetImageIntrinsics(object imageHandle) => default;
        public Broiler.Graphics.BColor ParseColor(string value) => default;
        public void RequestRefresh(bool relayout) { }
        public SizeF ViewportSize => new(400, 1000);
        public PointF RootLocation => PointF.Empty;
        public SizeF ActualSize { get; set; }
        public bool AvoidGeometryAntialias => false;
        public SizeF PageSize => new(400, (float)pageHeight);
        public int MarginTop => 0;
        public void ReportLayoutError(string message, Exception? exception = null) { }
        public bool AvoidAsyncImagesLoading => true;
        public bool AvoidImagesLateLoading => true;
        public ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete) => null!;
        public string FormatListMarker(int number, string style) => string.Empty;
    }

    private sealed class StubFont(double size) : Broiler.Graphics.ILayoutFont
    {
        public double Size { get; } = size;
        public double Height => size;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
