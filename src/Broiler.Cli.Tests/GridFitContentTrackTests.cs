using System.Linq;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards CSS Grid <c>fit-content(L)</c> track sizing (roadmap #1248 Workstream E)
/// and, underneath it, the max-content contribution of sequential atomic
/// inline-level boxes.
///
/// Each grid item holds two <c>display:inline-block; width:40px</c> spans, so its
/// min-content is 40 (one span per line) and its max-content is 80 (both on one
/// line). Track sizes therefore resolve deterministically, with no font metrics:
///  • <c>min-content</c> → 40, <c>max-content</c> → 80;
///  • <c>fit-content(60px)</c> → max(40, min(60, 80)) = 60 (limit binds);
///  • <c>fit-content(30px)</c> → max(40, min(30, 80)) = 40 (min-content floor wins).
///
/// The max-content = 80 result depends on the fix in
/// <see cref="Broiler.HtmlBridge"/>'s layout core (CssBoxHelper.GetMinMaxSumWords):
/// an inline-block is inline-level and stays on the max-content line, so two of them
/// sum rather than taking the widest (which gave 40 and collapsed the tracks).
/// </summary>
public sealed class GridFitContentTrackTests
{
    [Fact]
    public void FitContentAndIntrinsicTracks_SizeToItemContentContributions()
    {
        const string style =
            ".g{display:grid;width:500px;grid-template-rows:40px;"
            + "grid-template-columns:min-content max-content fit-content(60px) fit-content(30px);}"
            + ".c span{display:inline-block;width:40px;height:20px;}";

        (int x, int w)[] cols = { (0, 40), (40, 80), (120, 60), (180, 40) };
        var body = new System.Text.StringBuilder();
        for (int i = 0; i < cols.Length; i++)
            body.Append($"<div class=\"c\" style=\"grid-column:{i + 1};grid-row:1;\" ")
                .Append($"data-offset-x=\"{cols[i].x}\" data-offset-y=\"0\" data-expected-width=\"{cols[i].w}\">")
                .Append("<span></span><span></span></div>");

        string html = "<!DOCTYPE html><html><head><style>" + style + "</style></head>"
            + "<body style=\"margin:0\"><div class=\"g\">" + body + "</div></body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///grid-fit-content.html");

        var failures = bridge.EvaluateCheckLayoutAssertions()
            .Where(a => System.Math.Abs(a.Expected - a.Actual) > 0.5)
            .Select(a => $"{a.Element} {a.Property}: expected {a.Expected}, got {a.Actual:0.##}")
            .ToList();

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
