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

        // Anchor: 20×20 border box at (40,40) → right/bottom = 60.
        var anchor = Box(cb, new PointF(40, 40), new SizeF(20, 20));
        anchor.AnchorName = "--a";

        // Target: absolutely positioned, position-area "bottom right", 30×30, at origin.
        var target = Box(cb, new PointF(0, 0), new SizeF(30, 30));
        target.Position = "absolute";
        target.PositionArea = "bottom right";
        target.PositionAnchor = "--a";

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
        // Size is unchanged (reposition-only MVP).
        Assert.Equal(30, target.Size.Width, 3);
        Assert.Equal(30, target.Size.Height, 3);
    }

    [Fact]
    public void Pass_LeavesBoxUnmoved_WhenAnchorNotRegistered()
    {
        var root = Box(null, new PointF(0, 0), new SizeF(1000, 1000));
        var cb = Box(root, new PointF(0, 0), new SizeF(200, 200));
        cb.Position = "relative";
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
}
