using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A layout pass that starts while another one is still running over the same box tree
/// must not fail the outer pass.
/// </summary>
/// <remarks>
/// <para>
/// Layout calls back into the host while it flows — text measurement is the call it makes
/// for every word, and an image load can complete on the same stack — and a host that
/// re-enters <c>PerformLayout</c> from one of those callbacks lands inside an in-flight
/// <c>CreateLineBoxes</c>. That is not hypothetical: it is the reported crash, an
/// <c>ArgumentException</c> ("An item with the same key has already been added") from
/// <c>CssLineBox.AssignRectanglesToBoxes</c>, seen in the browser on pages such as
/// html5test.com and not in a single-shot capture, which never re-enters.
/// </para>
/// <para>
/// The sequence is a consequence of <c>CreateLineBoxes</c> emptying
/// <c>CssBox.LineBoxes</c> when it starts. The outer pass clears the list and begins
/// flowing; the inner pass clears it again — dropping the line the outer pass is still
/// filling — builds its own lines and assigns them to the boxes; the outer pass then
/// resumes and walks what is now the inner pass's list, so the first line it assigns has
/// already been assigned. <c>PerformLayout</c> catches the exception as a layout error, so
/// the failure is silent and the block simply loses the rest of its lines.
/// </para>
/// <para>
/// The tests drive it through <c>MeasureText</c> because that is a real callback made from
/// inside <c>FlowBox</c>, which is where the reported stack sits.
/// </para>
/// </remarks>
public sealed class LineBoxReentrantLayoutTests
{
    private static readonly Uri BaseUrl = new("file:///reentrant.html");

    // The crash itself: nothing is reported, so the outer pass ran to the end.
    [Fact]
    public void A_Layout_Re_Entered_From_A_Host_Callback_Reports_No_Error()
    {
        var environment = new ReentrantLayoutEnvironment { ReenterOn = "beta" };
        var (root, _) = Paragraph(environment);
        environment.Reenter = () => root.PerformLayout(environment);

        root.PerformLayout(environment);

        Assert.True(environment.ReenterFired, "the callback never re-entered, so nothing was tested");
        Assert.Empty(environment.Errors);
    }

    // ...and the pass that finished last is the one the boxes agree with, which is what the
    // paint walker and FragmentTreeBuilder read back out of CssBox.Rectangles.
    [Fact]
    public void The_Boxes_Carry_The_Rectangles_Of_The_Lines_They_Are_On()
    {
        var environment = new ReentrantLayoutEnvironment { ReenterOn = "beta" };
        var (root, paragraph) = Paragraph(environment);
        environment.Reenter = () => root.PerformLayout(environment);

        root.PerformLayout(environment);

        Assert.NotEmpty(paragraph.LineBoxes);

        foreach (var line in paragraph.LineBoxes)
        {
            foreach (var (box, rectangle) in line.Rectangles)
            {
                Assert.True(
                    box.Rectangles.TryGetValue(line, out var assigned),
                    "a box named by a line box did not receive that line's rectangle");
                Assert.Equal(rectangle, assigned);
            }
        }
    }

    // The guard against fixing it by making the pass tolerant of anything: an ordinary
    // layout, with nothing re-entering, still projects every rectangle exactly once.
    [Fact]
    public void An_Ordinary_Layout_Still_Assigns_Every_Rectangle()
    {
        var environment = new ReentrantLayoutEnvironment();
        var (root, paragraph) = Paragraph(environment);

        root.PerformLayout(environment);

        Assert.Empty(environment.Errors);

        var line = Assert.Single(paragraph.LineBoxes);
        Assert.NotEmpty(line.Rectangles);

        foreach (var (box, rectangle) in line.Rectangles)
            Assert.Equal(rectangle, box.Rectangles[line]);
    }

    /// <summary>
    /// A block whose children are three inline boxes with words — the shape the reported
    /// stack was flowing (a paragraph of inline links) and the smallest one that has a
    /// word measured after the flow has begun.
    /// </summary>
    private static (CssBox Root, CssBox Paragraph) Paragraph(ILayoutEnvironment environment)
    {
        var root = new CssBox(null, null, BaseUrl)
        {
            Location = new PointF(0, 0),
            Size = new SizeF(400, 400),
            Display = CssConstants.Block,
            FontSize = "16px",
            LayoutEnvironment = environment,
        };

        var paragraph = new CssBox(root, null, BaseUrl)
        {
            Location = new PointF(0, 0),
            Size = new SizeF(400, 0),
            Display = CssConstants.Block,
            FontSize = "16px",
        };

        foreach (var word in new[] { "alpha", "beta", "gamma" })
        {
            var inline = new CssBox(paragraph, null, BaseUrl)
            {
                Display = CssConstants.Inline,
                FontSize = "16px",
                Text = word.AsMemory(),
            };

            inline.ParseToWords();
        }

        return (root, paragraph);
    }

    /// <summary>
    /// A host that re-enters layout once, from the text measurement of
    /// <see cref="ReenterOn"/> — a callback layout makes from inside <c>FlowBox</c>.
    /// </summary>
    private sealed class ReentrantLayoutEnvironment : ILayoutEnvironment
    {
        public string? ReenterOn { get; init; }

        public Action? Reenter { get; set; }

        public bool ReenterFired { get; private set; }

        public List<Exception> Errors { get; } = [];

        public SizeF MeasureText(Broiler.Graphics.ILayoutFont font, string text)
        {
            if (!ReenterFired && Reenter != null && text == ReenterOn)
            {
                ReenterFired = true;
                Reenter();
            }

            return new((float)(text.Length * 8), (float)font.Height);
        }

        public void ReportLayoutError(string message, Exception? exception = null)
        {
            if (exception != null)
                Errors.Add(exception);
        }

        public Broiler.Graphics.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => new StubFont(size);
        public void MeasureText(Broiler.Graphics.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = text.Length * 8; }
        public double GetWhitespaceWidth(Broiler.Graphics.ILayoutFont font) => 8;
        public ImageIntrinsics GetImageIntrinsics(object imageHandle) => default;
        public Broiler.Graphics.BColor ParseColor(string value) => default;
        public void RequestRefresh(bool relayout) { }
        public SizeF ViewportSize => new(1000, 1000);
        public PointF RootLocation => PointF.Empty;
        public SizeF ActualSize { get; set; }
        public bool AvoidGeometryAntialias => false;
        public SizeF PageSize => new(1000, 1000);
        public int MarginTop => 0;
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
