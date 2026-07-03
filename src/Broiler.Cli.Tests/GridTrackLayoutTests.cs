using System.Collections.Generic;
using System.Linq;
using System.Text;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Verifies the definite-track CSS Grid pass (Broiler.Layout CssBoxGrid) against
/// the embedded <c>data-offset-*</c>/<c>data-expected-*</c> geometry that the WPT
/// check-layout grid tests carry — those values are the browser-correct answer,
/// so matching them reproduces the WPT pass condition without a reference image.
///
/// Mirrors css/css-grid/placement/grid-auto-flow-sparse-001.html (WPT #1206).
/// </summary>
public sealed class GridTrackLayoutTests
{
    private sealed record Item(string Cls, double X, double Y, double W, double H);

    // grid.css class -> grid-column / grid-row declarations used by the test.
    private static readonly Dictionary<string, string> ClassRules = new()
    {
        ["firstRowFirstColumn"] = "grid-column:1;grid-row:1;",
        ["firstRowSecondColumn"] = "grid-column:2;grid-row:1;",
        ["secondRowFirstColumn"] = "grid-column:1;grid-row:2;",
        ["secondRowAutoColumn"] = "grid-column:auto;grid-row:2;",
        ["thirdRowAutoColumn"] = "grid-column:auto;grid-row:3;",
        ["autoRowFirstColumn"] = "grid-column:1;grid-row:auto;",
        ["autoRowSecondColumn"] = "grid-column:2;grid-row:auto;",
        ["autoRowThirdColumn"] = "grid-column:3;grid-row:auto;",
        ["autoRowAutoColumn"] = "grid-column:auto;grid-row:auto;",
        ["firstRowAutoColumn"] = "grid-column:auto;grid-row:1;",
        ["autoRowAutoColumnSpanning2"] = "grid-column:span 2;grid-row:auto;",
        ["autoRowSpanning2AutoColumn"] = "grid-column:auto;grid-row:span 2;",
        ["autoRowSpanning2AutoColumnSpanning3"] = "grid-column:span 3;grid-row:span 2;",
        ["autoRowSpanning3AutoColumnSpanning2"] = "grid-column:span 2;grid-row:span 3;",
    };

    // One entry per <div class="grid ..."> block, in document order, with the
    // expected offset/size the WPT test asserts for each child.
    private static readonly (bool ColumnFlow, Item[] Items)[] Containers =
    {
        (false, new[]
        {
            new Item("firstRowSecondColumn", 50, 0, 100, 50),
            new Item("autoRowAutoColumnSpanning2", 150, 0, 350, 50),
            new Item("autoRowAutoColumn", 0, 50, 50, 100),
            new Item("autoRowAutoColumnSpanning2", 50, 50, 250, 100),
            new Item("autoRowAutoColumnSpanning2", 0, 150, 150, 150),
            new Item("autoRowAutoColumn", 150, 150, 150, 150),
        }),
        (false, new[]
        {
            new Item("autoRowSecondColumn", 50, 0, 100, 50),
            new Item("autoRowAutoColumnSpanning2", 150, 0, 350, 50),
            new Item("autoRowFirstColumn", 0, 50, 50, 100),
            new Item("autoRowThirdColumn", 150, 50, 150, 100),
            new Item("autoRowAutoColumn", 300, 50, 200, 100),
            new Item("autoRowAutoColumn", 0, 150, 50, 150),
            new Item("autoRowSpanning2AutoColumnSpanning3", 50, 150, 450, 350),
            new Item("autoRowAutoColumn", 0, 300, 50, 200),
        }),
        (false, new[]
        {
            new Item("firstRowAutoColumn", 0, 0, 50, 50),
            new Item("secondRowAutoColumn", 0, 50, 50, 100),
            new Item("autoRowSecondColumn", 50, 0, 100, 50),
            new Item("autoRowFirstColumn", 0, 150, 50, 150),
            new Item("autoRowAutoColumn", 50, 150, 100, 150),
        }),
        (false, new[]
        {
            new Item("autoRowFirstColumn", 0, 150, 50, 150),
            new Item("firstRowSecondColumn", 50, 0, 100, 50),
            new Item("secondRowAutoColumn", 0, 50, 50, 100),
            new Item("firstRowAutoColumn", 0, 0, 50, 50),
            new Item("autoRowAutoColumn", 50, 150, 100, 150),
        }),
        (true, new[]
        {
            new Item("secondRowFirstColumn", 0, 50, 50, 100),
            new Item("autoRowSpanning2AutoColumn", 0, 150, 50, 350),
            new Item("autoRowAutoColumn", 50, 0, 100, 50),
            new Item("autoRowSpanning2AutoColumn", 50, 50, 100, 250),
            new Item("autoRowSpanning2AutoColumn", 150, 0, 150, 150),
            new Item("autoRowAutoColumn", 150, 150, 150, 150),
        }),
        (true, new[]
        {
            new Item("secondRowAutoColumn", 0, 50, 50, 100),
            new Item("autoRowSpanning2AutoColumn", 0, 150, 50, 350),
            new Item("firstRowAutoColumn", 50, 0, 100, 50),
            new Item("thirdRowAutoColumn", 50, 150, 100, 150),
            new Item("autoRowAutoColumn", 50, 300, 100, 200),
            new Item("autoRowAutoColumn", 150, 0, 150, 50),
            new Item("autoRowSpanning3AutoColumnSpanning2", 150, 50, 350, 450),
            new Item("autoRowAutoColumn", 300, 0, 200, 50),
        }),
        (true, new[]
        {
            new Item("autoRowFirstColumn", 0, 0, 50, 50),
            new Item("autoRowSecondColumn", 50, 0, 100, 50),
            new Item("secondRowAutoColumn", 0, 50, 50, 100),
            new Item("firstRowAutoColumn", 150, 0, 150, 50),
            new Item("autoRowAutoColumn", 150, 50, 150, 100),
        }),
        (true, new[]
        {
            new Item("firstRowAutoColumn", 150, 0, 150, 50),
            new Item("secondRowFirstColumn", 0, 50, 50, 100),
            new Item("autoRowSecondColumn", 50, 0, 100, 50),
            new Item("autoRowFirstColumn", 0, 0, 50, 50),
            new Item("autoRowAutoColumn", 150, 50, 150, 100),
        }),
    };

    private static string BuildHtml()
    {
        var css = new StringBuilder();
        css.Append(".grid{display:grid;grid-template-columns:50px 100px 150px 200px;grid-template-rows:50px 100px 150px 200px;}");
        css.Append(".colflow{grid-auto-flow:column;}");
        css.Append(".uc{position:relative;}");
        css.Append(".sizedToGridArea{width:100%;height:100%;}");
        foreach (var (cls, rule) in ClassRules)
            css.Append('.').Append(cls).Append('{').Append(rule).Append('}');

        var body = new StringBuilder();
        for (int c = 0; c < Containers.Length; c++)
        {
            var (columnFlow, items) = Containers[c];
            body.Append("<div class=\"uc\"><div class=\"grid").Append(columnFlow ? " colflow" : "").Append("\">");
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                body.Append("<div id=\"c").Append(c).Append('i').Append(i)
                    .Append("\" class=\"sizedToGridArea ").Append(it.Cls).Append("\" ")
                    .Append("data-offset-x=\"").Append(it.X).Append("\" ")
                    .Append("data-offset-y=\"").Append(it.Y).Append("\" ")
                    .Append("data-expected-width=\"").Append(it.W).Append("\" ")
                    .Append("data-expected-height=\"").Append(it.H).Append("\"></div>");
            }
            body.Append("</div></div>");
        }

        return "<!DOCTYPE html><html><head><style>" + css + "</style></head><body style=\"margin:0\">"
            + body + "</body></html>";
    }

    [Fact]
    public void GridAutoFlowSparse_MatchesExpectedGeometry()
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, BuildHtml(), "file:///grid-auto-flow-sparse-001.html");

        var assertions = bridge.EvaluateCheckLayoutAssertions();

        var failures = assertions
            .Where(a => System.Math.Abs(a.Expected - a.Actual) > 0.5)
            .Select(a => $"{a.Element} {a.Property}: expected {a.Expected}, got {a.Actual:0.##}")
            .ToList();

        Assert.True(failures.Count == 0,
            $"{failures.Count}/{assertions.Count} assertions failed:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// Locks in the <c>repeat()</c> track expansion, <c>gap</c> spacing, and
    /// two-line span placement (<c>grid-column: 2 / 4</c>) code paths — used by
    /// real grid tests (e.g. css-anchor-position/position-try-grid-001) but not
    /// exercised by the sparse-placement fixture above.
    /// </summary>
    [Fact]
    public void GridRepeatGapAndLineSpan_MatchesExpectedGeometry()
    {
        // 3 columns of 100px, 2 rows of 50px, 10px gap. Column edges: 0..100,
        // 110..210, 220..320. Row edges: 0..50, 60..110.
        const string html =
            "<!DOCTYPE html><html><head><style>"
            + ".g{display:grid;grid-template-columns:repeat(3,100px);"
            + "grid-template-rows:50px 50px;gap:10px;position:relative;}"
            + ".i{width:100%;height:100%;}"
            + "</style></head><body style=\"margin:0\"><div class=\"g\">"
            + "<div id=\"a\" class=\"i\" style=\"grid-column:1;grid-row:1;\" "
            + "data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"100\" data-expected-height=\"50\"></div>"
            + "<div id=\"b\" class=\"i\" style=\"grid-column:2 / 4;grid-row:2;\" "
            + "data-offset-x=\"110\" data-offset-y=\"60\" data-expected-width=\"210\" data-expected-height=\"50\"></div>"
            + "</div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///grid-repeat-gap.html");

        var byKey = bridge.EvaluateCheckLayoutAssertions()
            .ToDictionary(a => a.Element + "/" + a.Property, a => a);

        var failures = byKey.Values
            .Where(a => System.Math.Abs(a.Expected - a.Actual) > 0.5)
            .Select(a => $"{a.Element} {a.Property}: expected {a.Expected}, got {a.Actual:0.##}")
            .ToList();

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// Runs a check-layout fixture through the Broiler layout engine and asserts
    /// every embedded <c>data-offset-*</c>/<c>data-expected-*</c> geometry matches.
    /// </summary>
    private static void AssertCheckLayout(string html, string url)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, url);

        var assertions = bridge.EvaluateCheckLayoutAssertions();
        var failures = assertions
            .Where(a => System.Math.Abs(a.Expected - a.Actual) > 0.5)
            .Select(a => $"{a.Element} {a.Property}: expected {a.Expected}, got {a.Actual:0.##}")
            .ToList();

        Assert.True(assertions.Count > 0, "no check-layout assertions were evaluated");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{assertions.Count} assertions failed:\n" + string.Join("\n", failures));
    }

    // Wraps a sized grid + its items into a check-layout document. Each item is
    // width:100%/height:100% so it fills its resolved grid area, so the asserted
    // offset/size reflect the track geometry the §11 pass computed.
    private static string GridDoc(string gridStyle, params (string style, double x, double y, double w, double h)[] items)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><style>")
          .Append(".g{display:grid;position:relative;").Append(gridStyle).Append("}")
          .Append(".i{width:100%;height:100%;}")
          .Append("</style></head><body style=\"margin:0\"><div class=\"g\">");
        for (int i = 0; i < items.Length; i++)
        {
            var it = items[i];
            sb.Append("<div id=\"n").Append(i).Append("\" class=\"i\" style=\"").Append(it.style).Append("\" ")
              .Append("data-offset-x=\"").Append(it.x).Append("\" data-offset-y=\"").Append(it.y).Append("\" ")
              .Append("data-expected-width=\"").Append(it.w).Append("\" data-expected-height=\"").Append(it.h).Append("\"></div>");
        }
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    [Fact]
    public void GridFrColumns_SplitFreeSpaceEqually()
    {
        // 300px wide, three equal fr columns -> 100px each; one 50px row.
        string html = GridDoc("width:300px;grid-template-columns:1fr 1fr 1fr;grid-template-rows:50px;",
            ("grid-column:1;grid-row:1;", 0, 0, 100, 50),
            ("grid-column:2;grid-row:1;", 100, 0, 100, 50),
            ("grid-column:3;grid-row:1;", 200, 0, 100, 50));
        AssertCheckLayout(html, "file:///grid-fr-columns.html");
    }

    [Fact]
    public void GridFixedPlusFr_FrTakesRemainder()
    {
        // 400px wide: a 100px fixed column then a 1fr column absorbing the rest.
        string html = GridDoc("width:400px;grid-template-columns:100px 1fr;grid-template-rows:30px;",
            ("grid-column:1;grid-row:1;", 0, 0, 100, 30),
            ("grid-column:2;grid-row:1;", 100, 0, 300, 30));
        AssertCheckLayout(html, "file:///grid-fixed-fr.html");
    }

    [Fact]
    public void GridRepeatFrWithGap_DistributesAfterGaps()
    {
        // repeat(2,1fr) with a 20px gap over 220px: free = 200 -> 100px each.
        string html = GridDoc("width:220px;grid-template-columns:repeat(2,1fr);grid-template-rows:40px;gap:20px;",
            ("grid-column:1;grid-row:1;", 0, 0, 100, 40),
            ("grid-column:2;grid-row:1;", 120, 0, 100, 40));
        AssertCheckLayout(html, "file:///grid-repeat-fr-gap.html");
    }

    [Fact]
    public void GridMinmaxFixedFr_GrowsAboveMinimum()
    {
        // minmax(100px,1fr) twice over 300px: each fr share (150) exceeds the
        // 100px floor, so both grow to 150px.
        string html = GridDoc("width:300px;grid-template-columns:minmax(100px,1fr) minmax(100px,1fr);grid-template-rows:30px;",
            ("grid-column:1;grid-row:1;", 0, 0, 150, 30),
            ("grid-column:2;grid-row:1;", 150, 0, 150, 30));
        AssertCheckLayout(html, "file:///grid-minmax.html");
    }

    [Fact]
    public void GridAutoColumns_SizeToFixedContent()
    {
        // Auto columns size to their item's max-content, driven here by a
        // fixed-size inner block so the result is font-independent.
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".g{display:grid;position:relative;width:500px;grid-template-columns:auto auto;grid-template-rows:40px;}"
            + ".box{height:20px;}"
            + "</style></head><body style=\"margin:0\"><div class=\"g\">"
            + "<div id=\"a\" style=\"grid-column:1;grid-row:1;\" data-offset-x=\"0\" data-offset-y=\"0\" "
            + "data-expected-width=\"80\"><div class=\"box\" style=\"width:80px\"></div></div>"
            + "<div id=\"b\" style=\"grid-column:2;grid-row:1;\" data-offset-x=\"80\" data-offset-y=\"0\" "
            + "data-expected-width=\"120\"><div class=\"box\" style=\"width:120px\"></div></div>"
            + "</div></body></html>";
        AssertCheckLayout(html, "file:///grid-auto-content.html");
    }

    [Fact]
    public void GridFrRows_SplitDefiniteHeight()
    {
        // A definite 200px height with two fr rows -> 100px each.
        string html = GridDoc("width:100px;height:200px;grid-template-columns:100px;grid-template-rows:1fr 1fr;",
            ("grid-column:1;grid-row:1;", 0, 0, 100, 100),
            ("grid-column:1;grid-row:2;", 0, 100, 100, 100));
        AssertCheckLayout(html, "file:///grid-fr-rows.html");
    }
}
