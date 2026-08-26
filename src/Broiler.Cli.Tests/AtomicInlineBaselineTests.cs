namespace Broiler.Cli.Tests;

/// <summary>
/// Where an <em>atomic</em> inline-level box sits vertically on its line — an
/// <c>inline-block</c> with no in-flow line box of its own, or one that clips its overflow, both
/// of which CSS2.1 §10.8.1 gives a baseline at their bottom margin edge. An inline
/// <c>&lt;svg&gt;</c> is the second kind: the parser lays it out as a replaced
/// <c>inline-block</c> and gives it <c>overflow: hidden</c>.
/// </summary>
/// <remarks>
/// <para>
/// They were left wherever the flow put them, which is the top of the line, so two of different
/// heights came out with their tops flush instead of their bottoms. Only <c>&lt;img&gt;</c> was
/// aligned; the inline-block half of <c>CssLineBox.SetBaseLine</c> returned early for the initial
/// <c>vertical-align: baseline</c>, on the premise — written into the comment beside it — that an
/// inline-block's flow position is already on the baseline.
/// </para>
/// <para>
/// Every expectation is Chromium's measured answer to the same markup at the same viewport width,
/// from one probe run against both engines. Each row is absolutely positioned at the origin so
/// these measure the vertical alignment within a line and not where the line itself falls.
/// </para>
/// <para>
/// The shared bottom is taken from the atomic inlines on the line rather than from the line's
/// strut baseline, which is the answer the spec gives. That is a deliberate limit, not an
/// oversight: this engine computes the strut baseline without the half-leading
/// <c>line-height</c> contributes, so a line whose strut is taller than its atomic inlines has a
/// baseline well below its text, and aligning to it would move boxes further from a browser than
/// leaving them alone does.
/// <see cref="AnAtomicInlineIsNotPushedDownByATallStrut"/> pins that limit so it is a decision on
/// the record rather than a surprise.
/// </para>
/// </remarks>
public class AtomicInlineBaselineTests
{
    /// <summary>
    /// Lays <paramref name="children"/> out on one line inside an absolutely positioned row and
    /// reports each child's client rect as <c>left,top,width,height</c>, joined with <c>;</c>.
    /// </summary>
    private static string RowRects(string children, string childCss = "display:inline-block")
    {
        var html = $@"<!doctype html>
<html><head><title>Test</title><style>
.s {{ {childCss} }}
#row {{ position: absolute; left: 0; top: 0; }}
</style></head>
<body style=""margin:0"">
<div id=""row"">{children}</div>
<div id=""result""></div>
<script>
var out = [], kids = document.getElementById('row').children;
for (var i = 0; i < kids.length; i++) {{
    var b = kids[i].getBoundingClientRect();
    out.push([b.left, b.top, b.width, b.height].join(','));
}}
document.getElementById('result').textContent = out.join(';');
</script>
</body>
</html>";
        return CaptureService.ExecuteScriptsWithDom(html, "https://example.com");
    }

    private static void AssertRow(string expected, string children, string childCss = "display:inline-block")
        => Assert.Contains($">{expected}<", RowRects(children, childCss));

    /// <summary>
    /// The case this was found through: two inline <c>&lt;svg&gt;</c> roots on one line. The
    /// shorter stands on the taller's bottom rather than starting beside it at the line top.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TwoInlineSvgRootsStandOnASharedBaseline()
        => AssertRow("0,42,180,98;180,0,180,140",
            @"<svg width=""180"" height=""98""></svg><svg width=""180"" height=""140""></svg>",
            childCss: "");

    /// <summary>An empty <c>inline-block</c> behaves identically — the defect was never about SVG.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TwoEmptyInlineBlocksStandOnASharedBaseline()
        => AssertRow("0,42,180,98;180,0,180,140",
            @"<span class=""s"" style=""width:180px;height:98px""></span>"
            + @"<span class=""s"" style=""width:180px;height:140px""></span>");

    /// <summary>Equal heights need no movement, so nothing moves.</summary>
    [Fact(Timeout = 600000)]
    public void EqualHeightsAreUnchanged()
        => AssertRow("0,0,60,40;60,0,60,40",
            @"<span class=""s"" style=""width:60px;height:40px""></span>"
            + @"<span class=""s"" style=""width:60px;height:40px""></span>");

    /// <summary>Every atomic inline on the line aligns to the tallest one, not just the first.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void EveryAtomicInlineAlignsToTheTallestOnTheLine()
        => AssertRow("0,60,60,30;60,0,60,90;120,40,60,50",
            @"<span class=""s"" style=""width:60px;height:30px""></span>"
            + @"<span class=""s"" style=""width:60px;height:90px""></span>"
            + @"<span class=""s"" style=""width:60px;height:50px""></span>");

    /// <summary>
    /// <c>vertical-align</c> other than <c>baseline</c> keeps the path it always had:
    /// <c>top</c> aligns to the line box, so the short box stays at the top.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnExplicitVerticalAlignStillWins()
        => AssertRow("0,0,60,40;60,0,60,120",
            @"<span class=""s"" style=""width:60px;height:40px;vertical-align:top""></span>"
            + @"<span class=""s"" style=""width:60px;height:120px""></span>");

    /// <summary>
    /// It is the <em>border</em> box that is measured and moved, so a bordered box is placed by
    /// its outer edge — 120 − 50 rather than 120 − 40.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ABorderCountsTowardTheAlignedBox()
        => AssertRow("0,70,70,50;70,0,60,120",
            @"<span class=""s"" style=""width:60px;height:40px;border:5px solid""></span>"
            + @"<span class=""s"" style=""width:60px;height:120px""></span>");

    /// <summary>
    /// It is the <em>margin</em> box that stands on the baseline, so a bottom margin lifts the box
    /// by that much: 120 − (40 + 20).
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ABottomMarginCountsTowardTheAlignedBox()
        => AssertRow("0,60,60,40;60,0,60,120",
            @"<span class=""s"" style=""width:60px;height:40px;margin-bottom:20px""></span>"
            + @"<span class=""s"" style=""width:60px;height:120px""></span>");

    /// <summary>
    /// A line whose strut is taller than every atomic inline on it leaves them where they are.
    /// Chromium puts the box's bottom on the text baseline — here <c>0,36,...</c> for the first —
    /// so this pins a known limit rather than a browser's answer; see the remarks on the class.
    /// The point of pinning it is that the box is not pushed *below* the line's text either, which
    /// is what aligning to this engine's strut baseline would do.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnAtomicInlineIsNotPushedDownByATallStrut()
        => AssertRow("0,0,400,100",
            @"<span class=""s"" style=""width:400px;height:100px""></span>",
            childCss: "display:inline-block");
}
