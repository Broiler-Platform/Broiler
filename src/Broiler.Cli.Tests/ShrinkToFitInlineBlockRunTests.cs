using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the max-content contribution of a run of atomic inline-level boxes in the
/// shrink-to-fit path (<c>CssBox.ComputeShrinkToFitWidth</c>), the companion to the
/// grid/table intrinsic path (<c>CssBoxHelper.GetMinMaxSumWords</c>).
///
/// Per CSS Sizing 3 §5 the max-content width of a container is its widest line laid
/// out with no wrapping. Inline-level children sit side by side and their widths add
/// — the code already accumulated a run of adjacent floats, but treated an
/// inline-block (and inline-table/-flex/-grid) like a block, resetting the line, so
/// two 40px inline-blocks in a shrink-to-fit float/inline-block shrank to 40 instead
/// of 80. Block-level children still start their own line (widest wins → 40).
///
/// Font-independent (all boxes are fixed-size); in-process check-layout geometry.
/// </summary>
public sealed class ShrinkToFitInlineBlockRunTests
{
    [Fact]
    public void ShrinkToFit_SumsInlineBlockRun_ButBlocksResetTheLine()
    {
        const string style =
            ".ib{display:inline-block;width:40px;height:20px;}"
            + ".f{float:left;clear:both;}"
            + ".shrink{display:inline-block;}"
            + ".blockkids > div{display:block;width:40px;height:20px;}";
        const string ibs = "<span class=\"ib\"></span><span class=\"ib\"></span>";

        string html = "<!DOCTYPE html><html><head><style>" + style + "</style></head>"
            + "<body style=\"margin:0\">"
            // shrink-to-fit float of two inline-blocks -> 40 + 40 = 80
            + $"<div class=\"f\" data-expected-width=\"80\">{ibs}</div>"
            // shrink-to-fit inline-block wrapper of two inline-blocks -> 80
            + $"<div class=\"shrink\" data-expected-width=\"80\">{ibs}</div>"
            // block-level children each start their own line -> widest = 40
            + "<div class=\"f blockkids\" style=\"clear:both\" data-expected-width=\"40\">"
            + "<div></div><div></div></div>"
            + "</body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///shrink-to-fit-inline-block-run.html");

        var failures = bridge.EvaluateCheckLayoutAssertions()
            .Where(a => Math.Abs(a.Expected - a.Actual) > 0.5)
            .Select(a => $"{a.Element} {a.Property}: expected {a.Expected}, got {a.Actual:0.##}")
            .ToList();

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
