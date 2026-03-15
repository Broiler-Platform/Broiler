using Broiler.HTML.Dom.Core.Dom;
using System.Collections.Generic;

namespace Broiler.DevConsole;

/// <summary>
/// A read-only snapshot of a <see cref="CssBox"/> node for display in
/// the DOM Inspector tree view.  Each node captures the tag name, id,
/// display value, and a reference to the underlying box for style lookup.
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

    /// <summary>
    /// Reference to the underlying <see cref="CssBox"/> so that callers
    /// can extract computed styles and box-model dimensions on demand.
    /// </summary>
    internal CssBox? Box { get; init; }

    /// <summary>Child nodes.</summary>
    public List<BoxTreeNode> Children { get; } = [];
}
