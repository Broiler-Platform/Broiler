using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Live-geometry (Phase 5 endgame) regression coverage for a box positioned by
/// <c>anchor()</c> physical insets, read via <c>offsetLeft/Top</c> and
/// <c>getBoundingClientRect</c> during script — before the render bake
/// (<c>ResolveAnchorFunctions</c>) runs. The non-native renderer cannot resolve
/// <c>anchor()</c> live, so it lays such a box out at the static origin
/// (<c>offsetLeft/Top = 0</c>); <see cref="DomBridge.ResolveAnchorInsetForElement"/>
/// resolves it lazily via the canonical <c>AnchorGeometry.ResolveEdge</c>, matching the
/// bake, so the CSSOM geometry APIs agree.
/// </summary>
public sealed class AnchorInsetLiveGeometryTests
{
    // #a anchor at (100,100) 50×70 → right edge 150, bottom edge 170.
    private const string Html = @"<!DOCTYPE html><html><head><style>
  #cb { position: relative; width: 400px; height: 400px; }
  #a  { position: absolute; left: 100px; top: 100px; width: 50px; height: 70px; anchor-name: --a; }
  #t  { position: absolute; position-anchor: --a; width: 30px; height: 20px;
        left: anchor(--a right); top: anchor(--a bottom); }
  #r  { position: absolute; position-anchor: --a; width: 30px; height: 20px;
        right: anchor(--a left); bottom: anchor(--a top); }
</style></head><body>
  <div id='cb'><div id='a'></div><div id='t'></div><div id='r'></div></div>
</body></html>";

    private static double Read(JSContext c, string expr) =>
        double.Parse(c.Eval(expr).ToString()!, System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void StartInsets_LiveOffsetsAndRect_ResolveToAnchorEdge()
    {
        using var context = new JSContext();
        new DomBridge().Attach(context, Html, "file:///anchor-inset-live.html");

        // left: anchor(--a right) → 150; top: anchor(--a bottom) → 170.
        Assert.Equal(150, Read(context, "document.getElementById('t').offsetLeft"), 3);
        Assert.Equal(170, Read(context, "document.getElementById('t').offsetTop"), 3);
        Assert.Equal(30, Read(context, "document.getElementById('t').offsetWidth"), 3);
        Assert.Equal(20, Read(context, "document.getElementById('t').offsetHeight"), 3);

        // getBoundingClientRect agrees with the offset getters (size + offsetParent invariant).
        Assert.Equal(Read(context, "document.getElementById('t').offsetWidth"),
            Read(context, "document.getElementById('t').getBoundingClientRect().width"), 3);
        double expectedLeft = Read(context, "document.getElementById('cb').getBoundingClientRect().left")
            + Read(context, "document.getElementById('cb').clientLeft")
            + Read(context, "document.getElementById('t').offsetLeft");
        Assert.Equal(expectedLeft, Read(context, "document.getElementById('t').getBoundingClientRect().left"), 3);
    }

    [Fact]
    public void EndInsets_ResolveAgainstContainingBlockMinusBox()
    {
        using var context = new JSContext();
        new DomBridge().Attach(context, Html, "file:///anchor-inset-live.html");

        // #r: right: anchor(--a left) → the box's right edge sits at the anchor's left (100),
        // so offsetLeft = 100 − 30 (box width) = 70; bottom: anchor(--a top) → 100 − 20 = 80.
        Assert.Equal(70, Read(context, "document.getElementById('r').offsetLeft"), 3);
        Assert.Equal(80, Read(context, "document.getElementById('r').offsetTop"), 3);
    }

    // #a anchor 50×70; #s sized by anchor-size(); #p adds padding+border in content-box.
    private const string SizeHtml = @"<!DOCTYPE html><html><head><style>
  #cb { position: relative; width: 400px; height: 400px; }
  #a  { position: absolute; left: 100px; top: 100px; width: 50px; height: 70px; anchor-name: --a; }
  #s  { position: absolute; position-anchor: --a; left: 0; top: 0;
        width: anchor-size(--a width); height: anchor-size(--a height); }
  #p  { position: absolute; position-anchor: --a; left: 0; top: 0;
        width: anchor-size(--a width); height: anchor-size(--a height);
        padding: 10px; border: 5px solid; }
</style></head><body>
  <div id='cb'><div id='a'></div><div id='s'></div><div id='p'></div></div>
</body></html>";

    [Fact]
    public void AnchorSize_LiveDimensions_ResolveToAnchorSize()
    {
        using var context = new JSContext();
        new DomBridge().Attach(context, SizeHtml, "file:///anchor-size-live.html");

        // #s: content-box, no padding/border → offset size == the anchor's 50×70.
        Assert.Equal(50, Read(context, "document.getElementById('s').offsetWidth"), 3);
        Assert.Equal(70, Read(context, "document.getElementById('s').offsetHeight"), 3);
        // getBoundingClientRect agrees.
        Assert.Equal(50, Read(context, "document.getElementById('s').getBoundingClientRect().width"), 3);
        Assert.Equal(70, Read(context, "document.getElementById('s').getBoundingClientRect().height"), 3);
    }

    [Fact]
    public void AnchorSize_ContentBox_AddsPaddingAndBorderToBorderBox()
    {
        using var context = new JSContext();
        new DomBridge().Attach(context, SizeHtml, "file:///anchor-size-live.html");

        // #p: content-box 50×70 + padding 10 each side (20) + border 5 each side (10) = 80×100.
        Assert.Equal(80, Read(context, "document.getElementById('p').offsetWidth"), 3);
        Assert.Equal(100, Read(context, "document.getElementById('p').offsetHeight"), 3);
    }
}
