using Broiler.CSS;
using Broiler.Layout;

namespace Broiler.Layout.Tests;

/// <summary>
/// Guards <see cref="AnchorRegistry"/>, the engine-facing named-anchor placement
/// facade (Phase 5 item 3 groundwork). Verifies name→rect registration/lookup and
/// that the composed placement queries agree with the underlying
/// <see cref="PositionAreaGrid"/> / <see cref="AnchorGeometry"/> primitives, plus the
/// unknown-anchor null contract.
/// </summary>
public sealed class AnchorRegistryTests
{
    private static AnchorRegistry WithAnchor(string name = "--a") =>
        new AnchorRegistry().Also(r => r.Register(name, new AnchorRect(40, 50, 80, 40)));

    [Fact]
    public void RegisterAndLookup()
    {
        var r = new AnchorRegistry();
        Assert.Equal(0, r.Count);
        r.Register("--a", new AnchorRect(1, 2, 3, 4));
        Assert.Equal(1, r.Count);
        Assert.True(r.TryGet("--a", out var rect));
        Assert.Equal(new AnchorRect(1, 2, 3, 4), rect);
        Assert.False(r.TryGet("--b", out _));
    }

    [Fact]
    public void Register_LastWins_AndNamesAreCaseSensitive()
    {
        var r = new AnchorRegistry();
        r.Register("--a", new AnchorRect(0, 0, 1, 1));
        r.Register("--a", new AnchorRect(9, 9, 9, 9));
        r.Register("--A", new AnchorRect(2, 2, 2, 2));
        Assert.Equal(2, r.Count);
        r.TryGet("--a", out var a);
        Assert.Equal(new AnchorRect(9, 9, 9, 9), a);
    }

    [Fact]
    public void AnchorRect_ExposesRightAndBottom()
    {
        var rect = new AnchorRect(40, 50, 80, 40);
        Assert.Equal(120, rect.Right);
        Assert.Equal(90, rect.Bottom);
    }

    [Fact]
    public void ResolvePositionAreaCell_MatchesPrimitive()
    {
        var r = WithAnchor();
        var area = new PositionAreaValue(PositionAreaSpan.End, PositionAreaSpan.End);
        var viaRegistry = r.ResolvePositionAreaCell("--a", area, 0, 0, 200, 200);
        var direct = PositionAreaGrid.ComputeCell(0, 0, 200, 200, 40, 50, 120, 90, area);
        Assert.Equal(direct, viaRegistry);
    }

    [Fact]
    public void ResolveAnchorEdge_MatchesPrimitive()
    {
        var r = WithAnchor();
        var viaRegistry = r.ResolveAnchorEdge("--a", AnchorSide.Right, 3, 0, AnchorInsetProperty.Right, 200, 300);
        var direct = AnchorGeometry.ResolveEdge(40, 50, 120, 90, AnchorSide.Right, 3, 0, AnchorInsetProperty.Right, 200, 300);
        Assert.Equal(direct, viaRegistry);
    }

    [Fact]
    public void ResolveAnchorSize_MatchesPrimitive()
    {
        var r = WithAnchor();
        Assert.Equal(80, r.ResolveAnchorSize("--a", AnchorSizeDimension.Width));
        Assert.Equal(40, r.ResolveAnchorSize("--a", AnchorSizeDimension.Height));
    }

    [Fact]
    public void UnknownAnchor_ReturnsNull()
    {
        var r = WithAnchor();
        Assert.Null(r.ResolvePositionAreaCell("--missing",
            new PositionAreaValue(PositionAreaSpan.Start, PositionAreaSpan.Start), 0, 0, 100, 100));
        Assert.Null(r.ResolveAnchorEdge("--missing", AnchorSide.Left, 0, 0, AnchorInsetProperty.Left, 100, 100));
        Assert.Null(r.ResolveAnchorSize("--missing", AnchorSizeDimension.Width));
    }
}

internal static class TestFluent
{
    public static T Also<T>(this T value, System.Action<T> action)
    {
        action(value);
        return value;
    }
}
