using System.Collections.Generic;
using System.Linq;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

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
/// reference. The <c>box-sizing:border-box</c> variants are a separate open
/// border-box + auto-fill-count + min-height subtlety (documented in the
/// workstream) and are not asserted here.
/// </summary>
public sealed class GridAutoRepeatMinSizeTests
{
    private const string Style =
        ".grid{position:relative;display:grid;grid:repeat(auto-fill,50px)/repeat(auto-fill,100px);min-width:300px;min-height:200px;float:left;}"
        + ".border{border:10px solid;}"
        + ".item{grid-column:-2;grid-row:-2;}";

    private sealed record Case(string Id, string Cls, string Style, int W, int H, int Ix, int Iy);

    // The nine variants whose full geometry the two fixes make correct.
    private static readonly Case[] Cases =
    {
        new("g1", "grid", "", 300, 200, 200, 150),
        new("g2", "grid", "width:200px;height:100px;", 300, 200, 200, 150),
        new("g3", "grid", "width:min-content;height:min-content;", 300, 200, 200, 150),
        new("g4", "grid", "width:max-content;height:max-content;", 300, 200, 200, 150),
        new("g5", "grid border", "", 320, 220, 200, 150),
        new("g6", "grid border", "width:200px;height:100px;", 320, 220, 200, 150),
        new("g7", "grid border", "width:min-content;height:min-content;", 320, 220, 200, 150),
        new("g8", "grid border", "width:max-content;height:max-content;", 320, 220, 200, 150),
    };

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
            .Where(a => System.Math.Abs(a.Expected - a.Actual) > 0.5)
            .Select(a => $"{a.Element} {a.Property}: expected {a.Expected}, got {a.Actual:0.##}")
            .ToList();

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
