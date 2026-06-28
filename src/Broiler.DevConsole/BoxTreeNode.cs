namespace Broiler.DevConsole;

/// <summary>
/// A read-only snapshot of a renderer layout-box node for display in
/// the DOM Inspector tree view.  Each node captures the tag name, id,
/// display value, computed styles, and box-model dimensions.
/// </summary>
public sealed class BoxTreeNode
{
    /// <summary>HTML tag name (e.g. "div", "p") or "anon" for anonymous boxes.</summary>
    public string Tag { get; init; } = "anon";

    /// <summary>The HTML <c>id</c> attribute, if any.</summary>
    public string? Id { get; init; }

    /// <summary>The HTML <c>class</c> attribute, if any.</summary>
    public string? CssClass { get; init; }

    /// <summary>CSS <c>display</c> value.</summary>
    public string Display { get; init; } = string.Empty;

    /// <summary>Zero-based depth in the tree.</summary>
    public int Depth { get; init; }

    /// <summary>Resolved CSS properties captured with this snapshot.</summary>
    public IReadOnlyList<ComputedStyleInfo> ComputedStyles { get; init; } = [];

    /// <summary>Resolved box-model dimensions captured with this snapshot.</summary>
    public BoxModelInfo BoxModel { get; init; } = new();

    /// <summary>Child nodes.</summary>
    public List<BoxTreeNode> Children { get; } = [];
}
