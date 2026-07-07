using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression guard for WPT css-grid/abspos/grid-positioned-items-within-grid-
/// implicit-track-001 (roadmap #1248 Workstream D). Covers two coupled features:
///
///  1. <b>Leading implicit tracks</b> — an in-flow item placed with a line that
///     resolves *before* the explicit grid (a negative index) generates implicit
///     tracks ahead of it and shifts the explicit grid right/down (CSS Grid §8.3).
///     The magenta <c>.sixRowsAndSixColumns</c> (<c>grid-column:-5/5;
///     grid-row:-5/5</c>) spans two leading + two explicit + two trailing tracks
///     (900×600 at the padding origin).
///  2. <b>Abspos grid-item placement into that grid</b> — an out-of-flow item's
///     <c>grid-column</c>/<c>grid-row</c> resolve against the (implicit-extended)
///     grid, an <c>auto</c> line resolving to the container's padding edge (§9.2),
///     overriding its <c>top/left/width/height:100%</c> fallback.
///
/// In-process check-layout geometry (the browser-correct answer), LTR only — the
/// WPT test's <c>directionRTL</c> variants are follow-up work. Before the fix the
/// negative line collapsed to a single cell and every cyan item filled the whole
/// containing block.
/// </summary>
public sealed class GridAbsposImplicitTrackTests
{
    private const string Style =
        ".grid{display:grid;grid-template-columns:200px 300px;grid-template-rows:150px 250px;"
      + "grid-auto-columns:100px;grid-auto-rows:50px;width:800px;height:600px;border:5px solid black;"
      + "margin:30px;padding:15px;position:relative;}"
      + ".absolute{position:absolute;top:0;left:0;width:100%;height:100%;}"
      + ".six{grid-row:-5/5;grid-column:-5/5;}";

    private sealed record Case(string Col, string Row, int X, int Y, int W, int H);

    private static readonly Case[] Cases =
    [
        new("auto / 1", "auto / 1", 0, 0, 215, 115),
        new("auto / 2", "auto / 2", 0, 0, 415, 265),
        new("3 / auto", "3 / auto", 715, 515, 115, 115),
        new("2 / auto", "2 / auto", 415, 265, 415, 365),
        new("-4 / 1", "-4 / 1", 115, 65, 100, 50),
        new("-4 / 2", "-4 / 2", 115, 65, 300, 200),
        new("3 / 4", "3 / 4", 715, 515, 100, 50),
        new("2 / 4", "2 / 4", 415, 265, 400, 300),
    ];

    [Fact]
    public void AbsposItems_ResolveAgainstImplicitExtendedGrid()
    {
        var body = new System.Text.StringBuilder();
        for (int i = 0; i < Cases.Length; i++)
        {
            var c = Cases[i];
            body.Append("<div class=\"grid\">")
                .Append("<div class=\"six\" data-offset-x=\"15\" data-offset-y=\"15\" data-expected-width=\"900\" data-expected-height=\"600\"></div>")
                .Append($"<div id=\"a{i}\" class=\"absolute\" style=\"grid-column:{c.Col};grid-row:{c.Row};\" ")
                .Append($"data-offset-x=\"{c.X}\" data-offset-y=\"{c.Y}\" data-expected-width=\"{c.W}\" data-expected-height=\"{c.H}\"></div></div>");
        }

        string html = "<!DOCTYPE html><html><head><style>" + Style + "</style></head><body style=\"margin:0\">"
            + body + "</body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///grid-positioned-items-within-grid-implicit-track-001.html");

        var assertions = bridge.EvaluateCheckLayoutAssertions().ToList();
        var failures = assertions
            .Where(a => Math.Abs(a.Expected - a.Actual) > 0.5)
            .Select(a => $"{a.Element} {a.Property}: expected {a.Expected}, got {a.Actual:0.##}")
            .ToList();

        Assert.True(assertions.Count >= 64, $"expected >=64 assertions, got {assertions.Count}");
        Assert.True(failures.Count == 0, $"{failures.Count} failed:\n" + string.Join("\n", failures));
    }
}
