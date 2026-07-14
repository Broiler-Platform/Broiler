using System;
using System.Collections.Generic;
using Broiler.CSS;

namespace Broiler.Layout;

/// <summary>
/// The used geometry of a CSS anchor element (an element carrying an
/// <c>anchor-name</c>) in the layout coordinate space: its border-box rectangle.
/// </summary>
public readonly record struct AnchorRect(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

/// <summary>
/// A registry of laid-out anchor elements keyed by <c>anchor-name</c>, and the
/// entry point for placing an anchor-positioned box against a named anchor. It
/// composes the pure geometry primitives (<see cref="PositionAreaGrid"/>,
/// <see cref="AnchorGeometry"/>) with the name→rect lookup so a single call resolves
/// an anchored box's grid cell or an <c>anchor()</c> / <c>anchor-size()</c> query.
/// </summary>
/// <remarks>
/// The engine-facing anchor-placement facade for HtmlBridge complexity-reduction
/// roadmap Phase 5 work item 3 — the piece the layout engine lacks today (it has no
/// anchor-name→box index). It is pure and additive: it holds no DOM, cascade, or box
/// tree; a caller registers the anchors it has already laid out and then queries.
/// Wiring this into the layout engine's absolute-positioning post-pass (so anchored
/// boxes are placed natively rather than pre-baked by the bridge) is a later
/// increment; this establishes the API that pass will call.
/// </remarks>
public sealed class AnchorRegistry
{
    // Anchor names are custom idents — case-sensitive (ordinal), last registration wins.
    private readonly Dictionary<string, AnchorRect> _anchors = new(StringComparer.Ordinal);

    /// <summary>Number of registered anchors.</summary>
    public int Count => _anchors.Count;

    /// <summary>
    /// Registers (or replaces) the border-box rectangle of the anchor named
    /// <paramref name="anchorName"/>.
    /// </summary>
    public void Register(string anchorName, AnchorRect rect)
    {
        ArgumentNullException.ThrowIfNull(anchorName);
        _anchors[anchorName] = rect;
    }

    /// <summary>Looks up a registered anchor's rectangle.</summary>
    public bool TryGet(string anchorName, out AnchorRect rect) => _anchors.TryGetValue(anchorName, out rect);

    /// <summary>
    /// Resolves the <c>position-area</c> grid cell for an anchored box against the
    /// named anchor and the box's containing-block frame. Returns <c>null</c> when the
    /// anchor is not registered (the caller falls back to normal positioning).
    /// </summary>
    public PositionAreaCell? ResolvePositionAreaCell(
        string anchorName, PositionAreaValue area,
        double cbX, double cbY, double cbWidth, double cbHeight)
    {
        if (!_anchors.TryGetValue(anchorName, out var a))
            return null;
        return PositionAreaGrid.ComputeCell(
            cbX, cbY, cbWidth, cbHeight, a.Left, a.Top, a.Right, a.Bottom, area);
    }

    /// <summary>
    /// Resolves an <c>anchor(&lt;side&gt;)</c> reference against the named anchor.
    /// Returns <c>null</c> when the anchor is not registered.
    /// </summary>
    public double? ResolveAnchorEdge(
        string anchorName, AnchorSide side,
        double scrollAdjX, double scrollAdjY,
        AnchorInsetProperty property, double cbWidth, double cbHeight)
    {
        if (!_anchors.TryGetValue(anchorName, out var a))
            return null;
        return AnchorGeometry.ResolveEdge(
            a.Left, a.Top, a.Right, a.Bottom, side, scrollAdjX, scrollAdjY, property, cbWidth, cbHeight);
    }

    /// <summary>
    /// Resolves an <c>anchor-size(&lt;dimension&gt;)</c> reference against the named
    /// anchor. Returns <c>null</c> when the anchor is not registered.
    /// </summary>
    public double? ResolveAnchorSize(string anchorName, AnchorSizeDimension dimension)
    {
        if (!_anchors.TryGetValue(anchorName, out var a))
            return null;
        return AnchorGeometry.ResolveSize(dimension, a.Width, a.Height);
    }
}
