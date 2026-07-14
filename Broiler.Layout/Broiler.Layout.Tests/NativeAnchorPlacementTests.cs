using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// Covers the P5.8c native anchor-placement post-pass (flag-gated, default off):
/// the box-tree anchor registry build, the position-area inset resolution, and the
/// end-to-end reposition of a MVP <c>position-area</c> box against a registered
/// anchor. The pass composes the already-tested P5.4–P5.8a primitives; these tests
/// pin the engine glue (registry walk, containing-block gathering, offset apply).
/// </summary>
public sealed class NativeAnchorPlacementTests
{
    private static readonly Uri BaseUrl = new("file:///anchor-pass.html");

    private static CssBox Box(CssBox parent, PointF location, SizeF size)
    {
        var b = new CssBox(parent, null, BaseUrl) { Location = location, Size = size };
        // Root boxes get a minimal layout environment so the containing-block resolution
        // (which reads Actual border widths → the box's em/font) works on these synthetic
        // trees; children inherit it. Sizes are irrelevant — the test boxes carry no text.
        if (parent == null)
            b.LayoutEnvironment = new FakeLayoutEnvironment();
        return b;
    }

    [Fact]
    public void BuildAnchorRegistry_CollectsNamedAnchorsWithBorderBox()
    {
        var root = Box(null, new PointF(0, 0), new SizeF(300, 300));
        var a = Box(root, new PointF(40, 50), new SizeF(80, 40));
        a.AnchorName = "--a";
        var plain = Box(root, new PointF(0, 0), new SizeF(10, 10)); // no anchor-name
        _ = plain;
        var nested = Box(a, new PointF(200, 200), new SizeF(20, 20));
        nested.AnchorName = "--b";

        var registry = CssBox.BuildAnchorRegistry(root);

        Assert.Equal(2, registry.Count);
        Assert.True(registry.TryGet("--a", out var ra));
        Assert.Equal(new AnchorRect(40, 50, 80, 40), ra);
        Assert.True(registry.TryGet("--b", out var rb));
        Assert.Equal(new AnchorRect(200, 200, 20, 20), rb);
    }

    [Theory]
    [InlineData("10px", 100.0, 10.0)]
    [InlineData("25%", 200.0, 50.0)]
    [InlineData("auto", 100.0, 0.0)]
    [InlineData("", 100.0, 0.0)]
    [InlineData("1em", 100.0, 0.0)]  // non-px/percent → 0 (MVP parity with the bridge)
    public void ResolveInset_PxOrPercentAgainstBasis(string value, double basis, double expected)
    {
        Assert.Equal(expected, CssBox.ResolveInset(value, basis));
    }

    [Fact]
    public void Pass_RepositionsPositionAreaBox_AgainstAnchor_WhenFlagOn()
    {
        // Containing block = a relative wrapper at the origin, 200×200.
        var root = Box(null, new PointF(0, 0), new SizeF(1000, 1000));
        var cb = Box(root, new PointF(0, 0), new SizeF(200, 200));
        cb.Position = "relative";
        cb.Display = "block"; // a real (non-inline) containing block

        // Anchor: 20×20 border box at (40,40) → right/bottom = 60.
        var anchor = Box(cb, new PointF(40, 40), new SizeF(20, 20));
        anchor.AnchorName = "--a";

        // Target: absolutely positioned, position-area "bottom right", explicit 30×30
        // (definite content size), at origin.
        var target = Box(cb, new PointF(0, 0), new SizeF(30, 30));
        target.Position = "absolute";
        target.PositionArea = "bottom right";
        target.PositionAnchor = "--a";
        target.Width = "30px";
        target.Height = "30px";

        // "bottom right" cell = [anchorRight..gridRight] × [anchorBottom..gridBottom]
        //   = [60..200] × [60..200]; End alignment puts the box at the cell start → (60,60).
        try
        {
            NativeAnchorPlacement.Enabled = true;
            CssBox.RunNativeAnchorPlacement(root);
        }
        finally
        {
            NativeAnchorPlacement.Enabled = false;
        }

        Assert.Equal(60, target.Location.X, 3);
        Assert.Equal(60, target.Location.Y, 3);
        // Explicit content size, no padding/border → border box stays 30×30.
        Assert.Equal(30, target.Size.Width, 3);
        Assert.Equal(30, target.Size.Height, 3);
    }

    [Fact]
    public void Pass_FillsCell_WhenAutoWidthChildlessBox()
    {
        var (root, cb) = FillCellFixture(out var target);
        // Auto width/height (default) + childless + content-box → fills the cell.
        try
        {
            NativeAnchorPlacement.Enabled = true;
            CssBox.RunNativeAnchorPlacement(root);
        }
        finally { NativeAnchorPlacement.Enabled = false; }
        _ = cb;

        // Cell = [60..200] × [60..200] = 140×140; no insets → fills 140×140 at (60,60).
        Assert.Equal(60, target.Location.X, 3);
        Assert.Equal(60, target.Location.Y, 3);
        Assert.Equal(140, target.Size.Width, 3);
        Assert.Equal(140, target.Size.Height, 3);
    }

    [Fact]
    public void Pass_ResolvesPercentSize_AgainstCell()
    {
        var (root, _) = FillCellFixture(out var target);
        target.Width = "25%";   // 25% of cellW 140 = 35
        target.Height = "50%";  // 50% of cellH 140 = 70
        try
        {
            NativeAnchorPlacement.Enabled = true;
            CssBox.RunNativeAnchorPlacement(root);
        }
        finally { NativeAnchorPlacement.Enabled = false; }

        Assert.Equal(35, target.Size.Width, 3);
        Assert.Equal(70, target.Size.Height, 3);
    }

    [Fact]
    public void Pass_AddsPaddingAndBorder_ToContentSize_ForBorderBox()
    {
        var (root, _) = FillCellFixture(out var target);
        target.Width = "20px";
        target.Height = "20px";
        target.PaddingLeft = "5px";
        target.PaddingRight = "5px";
        try
        {
            NativeAnchorPlacement.Enabled = true;
            CssBox.RunNativeAnchorPlacement(root);
        }
        finally { NativeAnchorPlacement.Enabled = false; }

        // content-box 20 + padding 5+5 → border-box width 30; height unaffected (20).
        Assert.Equal(30, target.Size.Width, 3);
        Assert.Equal(20, target.Size.Height, 3);
    }

    [Fact]
    public void Pass_KeepsLaidOutSize_WhenBoxHasChildren()
    {
        var (root, _) = FillCellFixture(out var target);
        // A childful box cannot be resized without re-flow → reposition-only.
        _ = Box(target, new PointF(0, 0), new SizeF(10, 10));
        try
        {
            NativeAnchorPlacement.Enabled = true;
            CssBox.RunNativeAnchorPlacement(root);
        }
        finally { NativeAnchorPlacement.Enabled = false; }

        Assert.Equal(60, target.Location.X, 3);
        Assert.Equal(60, target.Location.Y, 3);
        // Size preserved (auto width would otherwise fill the cell).
        Assert.Equal(30, target.Size.Width, 3);
        Assert.Equal(30, target.Size.Height, 3);
    }

    [Fact]
    public void Pass_BorderBox_TreatsResolvedSizeAsBorderBox()
    {
        var (root, _) = FillCellFixture(out var target);
        // With box-sizing: border-box the authored width IS the border box, so padding
        // and border are NOT added on top (content-box would give 50 + 10+10 = 70).
        target.BoxSizing = "border-box";
        target.Width = "50px";
        target.Height = "50px";
        target.PaddingLeft = "10px";
        target.PaddingRight = "10px";
        try
        {
            NativeAnchorPlacement.Enabled = true;
            CssBox.RunNativeAnchorPlacement(root);
        }
        finally { NativeAnchorPlacement.Enabled = false; }

        Assert.Equal(50, target.Size.Width, 3);   // border box = authored 50px
        Assert.Equal(50, target.Size.Height, 3);
    }

    [Fact]
    public void Pass_BorderBox_FillsCell_AsBorderBox()
    {
        var (root, _) = FillCellFixture(out var target);
        // Auto width + border-box → the border box fills the cell (140), padding/border
        // eat into the content box rather than extending beyond the cell.
        target.BoxSizing = "border-box";
        target.PaddingLeft = "10px";
        target.PaddingRight = "10px";
        try
        {
            NativeAnchorPlacement.Enabled = true;
            CssBox.RunNativeAnchorPlacement(root);
        }
        finally { NativeAnchorPlacement.Enabled = false; }

        Assert.Equal(140, target.Size.Width, 3);
        Assert.Equal(140, target.Size.Height, 3);
    }

    [Fact]
    public void Pass_PercentMargin_StretchesMarginBoxToImcb()
    {
        var (root, _) = FillCellFixture(out var target);
        // Auto width (stretch) + percentage margin-left → the margin box fills the cell
        // (IMCB = cell 140), so the border box shrinks by the resolved margin and shifts.
        target.MarginLeft = "10%"; // 10% of cell 140 = 14
        try
        {
            NativeAnchorPlacement.Enabled = true;
            CssBox.RunNativeAnchorPlacement(root);
        }
        finally { NativeAnchorPlacement.Enabled = false; }

        Assert.Equal(126, target.Size.Width, 3);   // 140 - 14
        Assert.Equal(140, target.Size.Height, 3);
        Assert.Equal(74, target.Location.X, 3);     // imcbLeft 60 + margin 14
        Assert.Equal(60, target.Location.Y, 3);
        Assert.Equal("14px", target.MarginLeft);    // resolved px written back
    }

    [Fact]
    public void Pass_PercentPadding_ResolvedAgainstCell_FillsImcb()
    {
        var (root, _) = FillCellFixture(out var target);
        // Auto width + percentage padding → the border box still fills the IMCB (140);
        // padding eats the content box, and is written back resolved against the cell.
        target.PaddingLeft = "10%"; target.PaddingRight = "10%"; // 14 each of cell 140
        try
        {
            NativeAnchorPlacement.Enabled = true;
            CssBox.RunNativeAnchorPlacement(root);
        }
        finally { NativeAnchorPlacement.Enabled = false; }

        Assert.Equal(140, target.Size.Width, 3);   // content 112 + padding 14+14
        Assert.Equal(140, target.Size.Height, 3);
        Assert.Equal(60, target.Location.X, 3);
        Assert.Equal("14px", target.PaddingLeft);   // % resolved against cell width, not own width
        Assert.Equal("14px", target.PaddingRight);
    }

    [Fact]
    public void Pass_PercentBoxProps_ResolveAgainstCellHeight_ForVerticalContainingBlock()
    {
        // A non-square cell in a vertical-writing-mode CB: percentage margins/padding must
        // resolve against the cell's INLINE size, which is the cell HEIGHT here (CSS
        // Writing Modes §7.4) — not the width the bridge always used.
        var root = Box(null, new PointF(0, 0), new SizeF(1000, 1000));
        var cb = Box(root, new PointF(0, 0), new SizeF(200, 100));
        cb.Position = "relative";
        cb.Display = "block";
        cb.WritingMode = "vertical-rl";
        var anchor = Box(cb, new PointF(40, 40), new SizeF(20, 20));
        anchor.AnchorName = "--a";
        var target = Box(cb, new PointF(0, 0), new SizeF(30, 30));
        target.Position = "absolute";
        target.Display = "block";
        target.PositionArea = "bottom right"; // cell = (60,60,140,40): cellW 140, cellH 40
        target.PositionAnchor = "--a";
        target.MarginLeft = "10%"; // 10% of cell HEIGHT 40 = 4 (NOT 14 = 10% of width)

        try
        {
            NativeAnchorPlacement.Enabled = true;
            CssBox.RunNativeAnchorPlacement(root);
        }
        finally { NativeAnchorPlacement.Enabled = false; }

        // border box width = imcb 140 - margin 4 = 136; positioned at imcbLeft 60 + margin 4.
        Assert.Equal(136, target.Size.Width, 3);
        Assert.Equal(40, target.Size.Height, 3);
        Assert.Equal(64, target.Location.X, 3);
        Assert.Equal("4px", target.MarginLeft);
    }

    [Fact]
    public void Pass_SharedAnchorName_BindsWithinOwnContainingBlockScope()
    {
        // Two sibling relative containers each declare anchor-name --a and hold a
        // "bottom right" target. With scope-aware resolution each target must bind to the
        // anchor in ITS OWN container — not the global last-registered --a.
        var root = Box(null, new PointF(0, 0), new SizeF(1000, 1000));

        CssBox MakeContainer(float x, out CssBox target)
        {
            var cb = Box(root, new PointF(x, 0), new SizeF(200, 200));
            cb.Position = "relative";
            cb.Display = "block";
            var anchor = Box(cb, new PointF(x + 40, 40), new SizeF(20, 20));
            anchor.AnchorName = "--a";
            target = Box(cb, new PointF(0, 0), new SizeF(30, 30));
            target.Position = "absolute";
            target.Display = "block";
            target.PositionArea = "bottom right";
            target.PositionAnchor = "--a";
            target.Width = "30px";
            target.Height = "30px";
            return cb;
        }

        MakeContainer(0, out var targetA);
        MakeContainer(300, out var targetB);

        try
        {
            NativeAnchorPlacement.Enabled = true;
            CssBox.RunNativeAnchorPlacement(root);
        }
        finally { NativeAnchorPlacement.Enabled = false; }

        // targetA binds anchorA (right/bottom = 60) in container A → (60,60).
        Assert.Equal(60, targetA.Location.X, 3);
        Assert.Equal(60, targetA.Location.Y, 3);
        // targetB binds anchorB (at x=340 → right 360, bottom 60) in container B → (360,60).
        // A flat last-wins registry would bind BOTH to anchorB, collapsing targetA's cell.
        Assert.Equal(360, targetB.Location.X, 3);
        Assert.Equal(60, targetB.Location.Y, 3);
    }

    // ------------------------------------------------------------------
    // anchor() inset placement (P5.8d.2b anchor()-insets expansion)
    // ------------------------------------------------------------------

    // CB 200×200 at origin; anchor --a is a 20×20 box at (40,40) → right/bottom 60,
    // left/top 40. Returns a childless 30×30 abspos target at the origin for the caller
    // to give anchor() insets.
    private static (CssBox root, CssBox cb) AnchorInsetFixture(out CssBox target)
    {
        var root = Box(null, new PointF(0, 0), new SizeF(1000, 1000));
        var cb = Box(root, new PointF(0, 0), new SizeF(200, 200));
        cb.Position = "relative";
        cb.Display = "block";
        var anchor = Box(cb, new PointF(40, 40), new SizeF(20, 20));
        anchor.AnchorName = "--a";
        target = Box(cb, new PointF(0, 0), new SizeF(30, 30));
        target.Position = "absolute";
        return (root, cb);
    }

    private static void RunPass(CssBox root)
    {
        try
        {
            NativeAnchorPlacement.Enabled = true;
            CssBox.RunNativeAnchorPlacement(root);
        }
        finally { NativeAnchorPlacement.Enabled = false; }
    }

    [Fact]
    public void AnchorInset_LeftTop_PlacesMarginEdgeAtAnchorEdges()
    {
        var (root, _) = AnchorInsetFixture(out var target);
        target.Left = "anchor(--a right)";  // box left edge = anchor right = 60
        target.Top = "anchor(--a bottom)";  // box top edge  = anchor bottom = 60
        RunPass(root);
        Assert.Equal(60, target.Location.X, 3);
        Assert.Equal(60, target.Location.Y, 3);
        Assert.Equal(30, target.Size.Width, 3);  // reposition-only: size kept
        Assert.Equal(30, target.Size.Height, 3);
    }

    [Fact]
    public void AnchorInset_RightBottom_PlacesFarMarginEdgeAtAnchorEdges()
    {
        var (root, _) = AnchorInsetFixture(out var target);
        // right: anchor(--a left) → box RIGHT edge lands on the anchor's left edge (40),
        // so with a 30-wide box the left edge is at 10. Likewise bottom → box bottom at 40.
        target.Right = "anchor(--a left)";
        target.Bottom = "anchor(--a top)";
        RunPass(root);
        Assert.Equal(10, target.Location.X, 3);   // 40 - 30
        Assert.Equal(10, target.Location.Y, 3);
    }

    [Fact]
    public void AnchorInset_MixedAnchorAndPlainLength_ResolvesEach()
    {
        var (root, _) = AnchorInsetFixture(out var target);
        target.Left = "anchor(--a right)";  // 60
        target.Top = "15px";                // plain length → box top edge at 15
        RunPass(root);
        Assert.Equal(60, target.Location.X, 3);
        Assert.Equal(15, target.Location.Y, 3);
    }

    [Fact]
    public void AnchorInset_ImplicitAnchor_UsesPositionAnchor()
    {
        var (root, _) = AnchorInsetFixture(out var target);
        target.PositionAnchor = "--a";
        target.Left = "anchor(right)";   // no name in the function → position-anchor --a
        target.Top = "anchor(bottom)";
        RunPass(root);
        Assert.Equal(60, target.Location.X, 3);
        Assert.Equal(60, target.Location.Y, 3);
    }

    [Fact]
    public void AnchorInset_UnregisteredAnchor_LeavesBoxPut()
    {
        var (root, _) = AnchorInsetFixture(out var target);
        target.Left = "anchor(--missing right)";
        target.Top = "anchor(--missing bottom)";
        RunPass(root);
        // Fallback 0px → box left/top margin edge at the CB origin (0,0), not the anchor.
        Assert.Equal(0, target.Location.X, 3);
        Assert.Equal(0, target.Location.Y, 3);
    }

    // ------------------------------------------------------------------
    // anchor-size() sizing (P5.8d.2b anchor-size() expansion)
    // ------------------------------------------------------------------

    [Fact]
    public void AnchorSize_WidthAndHeight_SizeBoxToAnchorDimensions()
    {
        var (root, _) = AnchorInsetFixture(out var target);  // anchor --a is 20×20
        target.Width = "anchor-size(--a width)";
        target.Height = "anchor-size(--a height)";
        RunPass(root);
        Assert.Equal(20, target.Size.Width, 3);
        Assert.Equal(20, target.Size.Height, 3);
    }

    [Fact]
    public void AnchorSize_OnlyWidth_KeepsLaidOutHeight()
    {
        var (root, _) = AnchorInsetFixture(out var target);  // laid-out 30×30
        target.Width = "anchor-size(--a width)"; // 20
        RunPass(root);
        Assert.Equal(20, target.Size.Width, 3);
        Assert.Equal(30, target.Size.Height, 3);  // height untouched
    }

    [Fact]
    public void AnchorSize_ContentBox_AddsPaddingAndBorder()
    {
        var (root, _) = AnchorInsetFixture(out var target);
        target.Width = "anchor-size(--a width)"; // content 20
        target.PaddingLeft = "5px";
        target.PaddingRight = "5px";
        RunPass(root);
        Assert.Equal(30, target.Size.Width, 3);   // 20 + 5 + 5 border box
    }

    [Fact]
    public void AnchorSize_BorderBox_UsesResolvedAsBorderBox()
    {
        var (root, _) = AnchorInsetFixture(out var target);
        target.Width = "anchor-size(--a width)"; // 20
        target.BoxSizing = "border-box";
        target.PaddingLeft = "5px";
        target.PaddingRight = "5px";
        RunPass(root);
        Assert.Equal(20, target.Size.Width, 3);   // border box = resolved 20 (padding not added)
    }

    [Fact]
    public void AnchorSize_ImplicitAnchor_UsesPositionAnchor()
    {
        var (root, _) = AnchorInsetFixture(out var target);
        target.PositionAnchor = "--a";
        target.Width = "anchor-size(width)";   // no name → position-anchor --a
        RunPass(root);
        Assert.Equal(20, target.Size.Width, 3);
    }

    [Fact]
    public void AnchorSize_ChildfulBox_KeepsLaidOutSize()
    {
        var (root, _) = AnchorInsetFixture(out var target);
        target.Width = "anchor-size(--a width)";
        _ = Box(target, new PointF(0, 0), new SizeF(10, 10));  // has a child → excluded
        RunPass(root);
        Assert.Equal(30, target.Size.Width, 3);  // unchanged (reposition/resize needs a re-flow)
    }

    // Shared fixture: CB 200×200 at origin, anchor --a (20×20 at (40,40)), and a
    // childless absolutely-positioned "bottom right" target (30×30 at origin).
    private static (CssBox root, CssBox cb) FillCellFixture(out CssBox target)
    {
        var root = Box(null, new PointF(0, 0), new SizeF(1000, 1000));
        var cb = Box(root, new PointF(0, 0), new SizeF(200, 200));
        cb.Position = "relative";
        cb.Display = "block"; // a real (non-inline) containing block
        var anchor = Box(cb, new PointF(40, 40), new SizeF(20, 20));
        anchor.AnchorName = "--a";
        target = Box(cb, new PointF(0, 0), new SizeF(30, 30));
        target.Position = "absolute";
        target.PositionArea = "bottom right";
        target.PositionAnchor = "--a";
        return (root, cb);
    }

    [Theory]
    [InlineData("30px", 30.0, null)]
    [InlineData("50%", null, 50.0)]
    [InlineData("42", 42.0, null)]
    [InlineData("auto", null, null)]
    [InlineData("", null, null)]
    [InlineData("1em", null, null)]
    public void ParseSizeComponent_MatchesBridgeSemantics(string value, double? px, double? pct)
    {
        var (ep, p) = CssBox.ParseSizeComponent(value);
        Assert.Equal(px, ep);
        Assert.Equal(pct, p);
    }

    [Fact]
    public void Pass_LeavesBoxUnmoved_WhenAnchorNotRegistered()
    {
        var root = Box(null, new PointF(0, 0), new SizeF(1000, 1000));
        var cb = Box(root, new PointF(0, 0), new SizeF(200, 200));
        cb.Position = "relative";
        cb.Display = "block"; // a real (non-inline) containing block
        var target = Box(cb, new PointF(7, 9), new SizeF(30, 30));
        target.Position = "absolute";
        target.PositionArea = "bottom right";
        target.PositionAnchor = "--missing";

        try
        {
            NativeAnchorPlacement.Enabled = true;
            CssBox.RunNativeAnchorPlacement(root);
        }
        finally
        {
            NativeAnchorPlacement.Enabled = false;
        }

        Assert.Equal(7, target.Location.X, 3);
        Assert.Equal(9, target.Location.Y, 3);
    }

    // Minimal layout environment for the synthetic box trees: resolves a fixed-size
    // font (so em-based Actual border/padding widths compute) and returns benign
    // defaults for everything else. The test boxes carry no text or replaced content,
    // so the measurement/image/colour members are never exercised.
    private sealed class FakeLayoutEnvironment : Broiler.Layout.ILayoutEnvironment
    {
        private static readonly Broiler.Graphics.ILayoutFont TheFont = new FakeFont();
        public Broiler.Graphics.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => TheFont;
        public SizeF MeasureText(Broiler.Graphics.ILayoutFont font, string text) => SizeF.Empty;
        public void MeasureText(Broiler.Graphics.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = 0; charFitWidth = 0; }
        public double GetWhitespaceWidth(Broiler.Graphics.ILayoutFont font) => 0;
        public Broiler.Layout.ImageIntrinsics GetImageIntrinsics(object imageHandle) => default;
        public Broiler.Graphics.BColor ParseColor(string value) => default;
        public void RequestRefresh(bool relayout) { }
        public SizeF ViewportSize => new(1000, 1000);
        public PointF RootLocation => PointF.Empty;
        public SizeF ActualSize { get; set; }
        public bool AvoidGeometryAntialias => false;
        public SizeF PageSize => new(1000, 1000);
        public int MarginTop => 0;
        public void ReportLayoutError(string message, Exception? exception = null) { }
        public bool AvoidAsyncImagesLoading => true;
        public bool AvoidImagesLateLoading => true;
        public Broiler.Layout.ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete) => null!;
        public string FormatListMarker(int number, string style) => string.Empty;
    }

    private sealed class FakeFont : Broiler.Graphics.ILayoutFont
    {
        public double Size => 16;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
