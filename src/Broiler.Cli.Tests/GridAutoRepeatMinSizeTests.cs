using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression guard for WPT css-grid/grid-definition/grid-auto-repeat-min-size-001
/// (roadmap #1248 Workstream C). A shrink-to-fit (float / intrinsic-keyword) grid
/// with <c>grid: repeat(auto-fill, 50px) / repeat(auto-fill, 100px)</c> and
/// <c>min-width:300px; min-height:200px</c> must resolve to 300×200 (3×4 tracks)
/// with the last-cell item (<c>grid-column:-2; grid-row:-2</c>) at (200, 150).
///
/// Covers two independent fixes made together:
///  1. <b>min-height/max-height clamp on a float's explicit height</b> — a
///     <c>float</c> grid with <c>height:100; min-height:200</c> kept 100 (the
///     explicit-height override ran after the §10.7 clamp and never re-applied it).
///  2. <b>intrinsic-sizing width keywords</b> — <c>width:min-content</c>/
///     <c>max-content</c>/<c>fit-content</c> fell through to the stretched
///     container width (1024) instead of shrink-to-fit + min-width.
///
/// In-process check-layout geometry (the browser-correct answer), no pixel
/// reference.
///
/// The <c>box-sizing:border-box</c> variants (g9/g11/g12) close two further root
/// causes (roadmap #1248 Workstream C, "the 3 border-box variants"):
///  3. <b>auto-fill count under a definite min-size uses ceil, not floor</b> — the
///     repeat count that fills a definite <em>size</em> is the largest that does not
///     overflow (floor), but the count that fills a definite <em>min-size</em> (an
///     indefinite used size) is the smallest that reaches it (ceil, §7.2.3.2). With
///     <c>box-sizing:border-box</c> the content min-height is 200−20=180, so the row
///     count is ⌈180/50⌉=4 (floor gave 3). A definite height (g10) still floors.
///  4. <b>intrinsic-sizing height keyword under border-box</b> — <c>height:
///     min-content</c>/<c>max-content</c> was reinterpreted as a specified border-box
///     length, dropping the border (offsetHeight 200 vs 220); such a keyword is the
///     content height already computed, so it is left in place.
/// </summary>
public sealed class GridAutoRepeatMinSizeTests
{
    private const string Style =
        ".grid{position:relative;display:grid;grid:repeat(auto-fill,50px)/repeat(auto-fill,100px);min-width:300px;min-height:200px;float:left;}"
        + ".border{border:10px solid;}"
        + ".borderBox{box-sizing:border-box;}"
        + ".item{grid-column:-2;grid-row:-2;}";

    private sealed record Case(string Id, string Cls, string Style, int W, int H, int Ix, int Iy);

    // All twelve variants of grid-auto-repeat-min-size-001, resolved to their
    // browser-correct geometry. g10 (border-box + a definite explicit height) keeps
    // the definite-size floor count (2 cols / 3 rows), so its item lands at (100,100)
    // in a 300×200 box — every other case fills the min-size and lands at (200,150).
    private static readonly Case[] Cases =
    [
        new("g1", "grid", "", 300, 200, 200, 150),
        new("g2", "grid", "width:200px;height:100px;", 300, 200, 200, 150),
        new("g3", "grid", "width:min-content;height:min-content;", 300, 200, 200, 150),
        new("g4", "grid", "width:max-content;height:max-content;", 300, 200, 200, 150),
        new("g5", "grid border", "", 320, 220, 200, 150),
        new("g6", "grid border", "width:200px;height:100px;", 320, 220, 200, 150),
        new("g7", "grid border", "width:min-content;height:min-content;", 320, 220, 200, 150),
        new("g8", "grid border", "width:max-content;height:max-content;", 320, 220, 200, 150),
        new("g9", "grid border borderBox", "", 320, 220, 200, 150),
        new("g10", "grid border borderBox", "width:200px;height:100px;", 300, 200, 100, 100),
        new("g11", "grid border borderBox", "width:min-content;height:min-content;", 320, 220, 200, 150),
        new("g12", "grid border borderBox", "width:max-content;height:max-content;", 320, 220, 200, 150),
    ];

    [Fact]
    public void ShrinkToFitAutoFillGrid_HonoursMinSizeAndIntrinsicWidthKeywords()
    {
        var body = new System.Text.StringBuilder();
        foreach (var c in Cases)
            body.Append($"<div id=\"{c.Id}\" class=\"{c.Cls}\" style=\"{c.Style}\" ")
                .Append($"data-expected-width=\"{c.W}\" data-expected-height=\"{c.H}\">")
                .Append($"<div id=\"i{c.Id}\" class=\"item\" data-offset-x=\"{c.Ix}\" data-offset-y=\"{c.Iy}\" ")
                .Append("data-expected-width=\"100\" data-expected-height=\"50\"></div></div>");

        string html = "<!DOCTYPE html><html><head><style>" + Style + "</style></head><body style=\"margin:0\">"
            + body + "</body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///grid-auto-repeat-min-size-001.html");

        var failures = bridge.EvaluateCheckLayoutAssertions()
            .Where(a => Math.Abs(a.Expected - a.Actual) > 0.5)
            .Select(a => $"{a.Element} {a.Property}: expected {a.Expected}, got {a.Actual:0.##}")
            .ToList();

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
