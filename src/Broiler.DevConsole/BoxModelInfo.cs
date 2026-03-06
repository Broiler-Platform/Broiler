namespace Broiler.DevConsole;

/// <summary>
/// Represents the box-model dimensions (margin, border, padding, content)
/// for a single CSS box, suitable for display in a box-model visualiser.
/// </summary>
public sealed class BoxModelInfo
{
    /// <summary>Margin widths (top, right, bottom, left).</summary>
    public BoxEdges Margin { get; init; } = new();

    /// <summary>Border widths (top, right, bottom, left).</summary>
    public BoxEdges Border { get; init; } = new();

    /// <summary>Padding widths (top, right, bottom, left).</summary>
    public BoxEdges Padding { get; init; } = new();

    /// <summary>Content area width.</summary>
    public double ContentWidth { get; init; }

    /// <summary>Content area height.</summary>
    public double ContentHeight { get; init; }
}

/// <summary>
/// The four edge values of a box-model component (margin, border, or padding).
/// </summary>
public sealed class BoxEdges
{
    public double Top { get; init; }
    public double Right { get; init; }
    public double Bottom { get; init; }
    public double Left { get; init; }
}
