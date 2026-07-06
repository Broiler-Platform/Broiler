using System.Linq;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards grid-track-based intrinsic (shrink-to-fit) width (roadmap #1248
/// Workstream A / E tail). A grid with a fixed column template sizes its
/// <c>min-content</c>/<c>max-content</c>/<c>fit-content</c>/<c>float</c> width to
/// the sum of its column tracks (+ gaps + own padding/border), rather than the
/// max-content of its (often empty) inline content — which collapsed the container
/// (WPT css-grid grid-gutters-and-tracks-001's <c>.fit-content</c> grids, the
/// <c>…-margin-border-padding-vertical-rl</c> container, …).
///
/// Cases here are viewport-independent (they either don't use the containing
/// block's available space, or have min-content == max-content). The
/// <c>fit-content</c> case where min-content ≠ max-content (e.g. a <c>minmax()</c>
/// track) depends on the available space, so it is only exact on a real viewport
/// (CI), not this harness. Vertical writing modes are out of scope (the physical
/// width axis must map through the rotation) and keep their existing sizing.
/// </summary>
public sealed class GridIntrinsicWidthTests
{
    private static double ContainerWidth(string style)
    {
        string html =
            "<!DOCTYPE html><html><head></head><body style=\"margin:0\">"
            + $"<div id=\"g\" style=\"{style}\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\"></div>"
            + "</body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///giw.html");
        var m = bridge.EvaluateCheckLayoutAssertions()
            .Where(a => a.Element.Contains("#g")).ToDictionary(a => a.Property, a => a.Actual);
        return m.TryGetValue("width", out var w) ? w : double.NaN;
    }

    private const string Base = "display:grid;position:relative;grid-template-rows:10px;";

    [Fact]
    public void MaxContentGrid_SumsColumnTracks()
    {
        Assert.Equal(200, ContainerWidth(Base + "width:max-content;grid-template-columns:100px 100px;"), 1);
        Assert.Equal(160, ContainerWidth(Base + "width:max-content;grid-template-columns:30px 50px 80px;"), 1);
        // minmax(): max-content uses the max side.
        Assert.Equal(50, ContainerWidth(Base + "width:max-content;grid-template-columns:minmax(10px,50px);"), 1);
    }

    [Fact]
    public void MinContentGrid_UsesMinTrackSides()
    {
        // minmax(): min-content uses the min side.
        Assert.Equal(10, ContainerWidth(Base + "width:min-content;grid-template-columns:minmax(10px,50px);"), 1);
        Assert.Equal(200, ContainerWidth(Base + "width:min-content;grid-template-columns:100px 100px;"), 1);
    }

    [Fact]
    public void FixedTrackShrinkToFit_SizesToTracksPlusGapsAndBorder()
    {
        // fit-content and float with fixed tracks (min-content == max-content).
        Assert.Equal(200, ContainerWidth(Base + "width:fit-content;grid-template-columns:100px 100px;"), 1);
        Assert.Equal(100, ContainerWidth(Base + "float:left;grid-template-columns:60px 40px;"), 1);
        // column-gap adds to the container.
        Assert.Equal(220, ContainerWidth(Base + "width:fit-content;grid-template-columns:100px 100px;column-gap:20px;"), 1);
        // padding + border count once (frame-correct).
        Assert.Equal(220, ContainerWidth(Base + "width:fit-content;grid-template-columns:100px 100px;padding:0 5px;border:5px solid;"), 1);
    }

    [Fact]
    public void NonFixedTracks_DeclineAndKeepPriorBehaviour()
    {
        // auto-fill / fr / auto need the real track pass, so the intrinsic-width
        // shortcut declines and the grid keeps the existing shrink-to-fit path
        // (which, for these empty grids, does NOT sum the tracks). A mixed
        // fixed+auto template must therefore NOT resolve to just its fixed track.
        double autoFill = ContainerWidth(Base + "float:left;grid-template-columns:repeat(auto-fill,100px);");
        Assert.True(autoFill < 150, $"auto-fill grid must decline the fixed-track shortcut (got {autoFill})");
        double mixed = ContainerWidth(Base + "width:max-content;grid-template-columns:100px auto;");
        Assert.True(System.Math.Abs(mixed - 100) > 0.5, $"fixed+auto template must decline the shortcut (got {mixed})");
    }
}
