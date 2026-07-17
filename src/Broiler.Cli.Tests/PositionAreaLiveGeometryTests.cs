using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Live-geometry (Phase 5) regression coverage for <c>position-area</c> boxes read via
/// <c>offsetLeft/Top/Width/Height</c> during script — the css-anchor-position
/// <c>position-area-anchor-partially-outside</c> testharness path. Two fixes are pinned:
/// (1) <c>offsetWidth/Height</c> now prefer the live <c>position-area</c> resolution over the
/// RF-BRIDGE shared-renderer snapshot (which reports 0 before the grid-cell placement is baked),
/// matching <c>offsetLeft/Top</c>; and (2) the anchor's box is expressed relative to the
/// containing block's <b>padding</b> box, so a bordered CB no longer shifts the grid by its
/// border width. The estimator returns the grid cell, which for the fixture's
/// <c>align-self/justify-self: stretch</c> boxes is the used border box.
/// </summary>
public sealed class PositionAreaLiveGeometryTests
{
    // The WPT fixture: a 400×400 position:relative CB with a 2px border, an anchor at
    // right:-50/top:-50 (100×100) that pokes out the top-right, and a stretch #anchored.
    private const string BorderedCbHtml = @"<!DOCTYPE html><html><head><style>
  #container { position: relative; width: 400px; height: 400px; margin: 100px auto; border: 2px solid; }
  #anchor { position: absolute; right: -50px; top: -50px; width: 100px; height: 100px; anchor-name: --anchor; }
  #anchored { position: absolute; align-self: stretch; justify-self: stretch; position-anchor: --anchor; }
</style></head><body>
  <div id='container'><div id='anchor'></div><div id='anchored'></div></div>
</body></html>";

    private static (int left, int top, int width, int height) LiveOffsets(JSContext c, string area)
    {
        c.Eval($"document.getElementById('anchored').style.positionArea='{area}';");
        int Read(string prop) => (int)double.Parse(
            c.Eval($"document.getElementById('anchored').{prop}").ToString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        return (Read("offsetLeft"), Read("offsetTop"), Read("offsetWidth"), Read("offsetHeight"));
    }

    [Theory]
    // The exact data-offset-*/data-expected-* rows from position-area-anchor-partially-outside.
    [InlineData("span-all", 0, -50, 450, 450)]
    [InlineData("left span-all", 0, -50, 350, 450)]
    [InlineData("span-all center", 350, -50, 100, 450)]
    [InlineData("right span-all", 450, -50, 0, 450)]
    [InlineData("span-top span-all", 0, -50, 450, 100)]
    [InlineData("bottom span-all", 0, 50, 450, 350)]
    public void BorderedCb_PartiallyOutsideAnchor_LiveOffsetsMatchGrid(
        string area, int left, int top, int width, int height)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, BorderedCbHtml, "file:///pa-live.html");

        var (l, t, w, h) = LiveOffsets(context, area);

        Assert.Equal(left, l);
        Assert.Equal(top, t);   // was border-width off before the padding-box frame fix
        Assert.Equal(width, w); // was 0 before the offsetWidth resolution-ordering fix
        Assert.Equal(height, h);
    }

    [Fact]
    public void NonStretchExplicitSize_ReportsUsedBox_NotTheGridCell()
    {
        // A NON-stretch, explicitly-sized position-area box: borderless 300×300 CB, 100×100
        // anchor at (100,100). position-area: bottom right selects the [200..300]×[200..300]
        // cell (100×100); an explicit 40×30 box aligns within it toward the anchor corner.
        // offsetWidth/Height must be the used 40×30, not the 100×100 grid cell — matching the
        // render bake's PositionAreaGrid.ResolveElementBox.
        const string html = @"<!DOCTYPE html><html><head><style>
  #cb { position: relative; width: 300px; height: 300px; }
  #a { position: absolute; left: 100px; top: 100px; width: 100px; height: 100px; anchor-name: --a; }
  #t { position: absolute; position-anchor: --a; position-area: bottom right; width: 40px; height: 30px; }
</style></head><body><div id='cb'><div id='a'></div><div id='t'></div></div></body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///pa-nonstretch.html");

        int Read(string prop) => (int)double.Parse(
            context.Eval($"document.getElementById('t').{prop}").ToString()!,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(40, Read("offsetWidth"));  // used size, not the 100-wide cell
        Assert.Equal(30, Read("offsetHeight")); // used size, not the 100-tall cell
        Assert.Equal(200, Read("offsetLeft"));  // aligned to the anchor-side corner of the cell
        Assert.Equal(200, Read("offsetTop"));
    }

    [Fact]
    public void BorderlessCb_IsUnaffected_ByThePaddingBoxFrame()
    {
        // Control: with no CB border the padding-box and border-box origins coincide, so the
        // frame fix is a no-op — a 100×100 anchor at (100,100) in a borderless 300×300 CB,
        // position-area: bottom right → the bottom-right cell [200..300]×[200..300].
        const string html = @"<!DOCTYPE html><html><head><style>
  #cb { position: relative; width: 300px; height: 300px; }
  #a { position: absolute; left: 100px; top: 100px; width: 100px; height: 100px; anchor-name: --a; }
  #t { position: absolute; align-self: stretch; justify-self: stretch; position-anchor: --a; position-area: bottom right; }
</style></head><body><div id='cb'><div id='a'></div><div id='t'></div></div></body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///pa-borderless.html");

        int L() => (int)double.Parse(context.Eval("document.getElementById('t').offsetLeft").ToString()!, System.Globalization.CultureInfo.InvariantCulture);
        int T() => (int)double.Parse(context.Eval("document.getElementById('t').offsetTop").ToString()!, System.Globalization.CultureInfo.InvariantCulture);
        int W() => (int)double.Parse(context.Eval("document.getElementById('t').offsetWidth").ToString()!, System.Globalization.CultureInfo.InvariantCulture);
        int H() => (int)double.Parse(context.Eval("document.getElementById('t').offsetHeight").ToString()!, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(200, L());
        Assert.Equal(200, T());
        Assert.Equal(100, W());
        Assert.Equal(100, H());
    }

    [Fact]
    public void GetBoundingClientRect_LivePositionArea_AgreesWithOffsetGetters()
    {
        // getBoundingClientRect must report the same live position-area geometry as
        // offsetLeft/Top/Width/Height: before this fix the offset getters preferred the
        // live resolution but getBoundingClientRect read the pre-bake shared snapshot
        // (a 0×0 box), so a scripted page saw inconsistent size and position. Fixture:
        // a non-stretch, explicitly-sized box so the used box (40×30) differs from the
        // 100×100 grid cell — the size the snapshot could never report live.
        const string html = @"<!DOCTYPE html><html><head><style>
  #cb { position: relative; width: 300px; height: 300px; }
  #a { position: absolute; left: 100px; top: 100px; width: 100px; height: 100px; anchor-name: --a; }
  #t { position: absolute; position-anchor: --a; position-area: bottom right; width: 40px; height: 30px; }
</style></head><body><div id='cb'><div id='a'></div><div id='t'></div></div></body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///pa-gbcr.html");

        double D(string expr) => double.Parse(
            context.Eval(expr).ToString()!, System.Globalization.CultureInfo.InvariantCulture);

        // Size: the getBoundingClientRect box matches offsetWidth/Height (the used 40×30),
        // not the pre-bake snapshot's 0.
        Assert.Equal(D("document.getElementById('t').offsetWidth"),
            D("document.getElementById('t').getBoundingClientRect().width"), 3);
        Assert.Equal(D("document.getElementById('t').offsetHeight"),
            D("document.getElementById('t').getBoundingClientRect().height"), 3);

        // Position: the getBoundingClientRect origin equals the standard offsetParent
        // invariant — offsetParent border-box left + its clientLeft (border) + offsetLeft —
        // so the box's document rect is internally consistent with the offset getters.
        double expectedLeft = D("document.getElementById('cb').getBoundingClientRect().left")
            + D("document.getElementById('cb').clientLeft")
            + D("document.getElementById('t').offsetLeft");
        double expectedTop = D("document.getElementById('cb').getBoundingClientRect().top")
            + D("document.getElementById('cb').clientTop")
            + D("document.getElementById('t').offsetTop");
        Assert.Equal(expectedLeft, D("document.getElementById('t').getBoundingClientRect().left"), 3);
        Assert.Equal(expectedTop, D("document.getElementById('t').getBoundingClientRect().top"), 3);
    }
}
