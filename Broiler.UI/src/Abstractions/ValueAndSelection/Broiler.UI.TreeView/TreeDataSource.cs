using System;

namespace Broiler.UI.TreeView;

/// <summary>
/// A node's identity, supplied by the data source and stable across a refresh.
///
/// Expansion, selection, and scroll are keyed by this rather than by index or by
/// object reference. A Solution Explorer rebuilds its node objects whenever the
/// workspace changes; keying on references would collapse the tree every time
/// the user saved a file.
/// </summary>
public readonly record struct TreeNodeId(string Value)
{
    public static TreeNodeId None => new(string.Empty);

    public bool IsNone => string.IsNullOrEmpty(Value);

    public override string ToString() => Value;
}

/// <summary>Severity or dirty state shown as a decoration, never by colour alone.</summary>
public enum TreeNodeDecoration
{
    None = 0,
    Dirty,
    Information,
    Warning,
    Error,
}

/// <summary>What the view needs to draw one row.</summary>
public sealed record TreeNodePresentation(
    TreeNodeId Id,
    string Label,
    string? SecondaryLabel = null,
    string? IconKey = null,
    TreeNodeDecoration Decoration = TreeNodeDecoration.None);

/// <summary>
/// The tree's content, supplied lazily.
///
/// Every member is designed so a large tree never has to be materialized:
/// <see cref="GetChildCount"/> answers without enumerating, and
/// <see cref="CanExpand"/> answers without expanding. A Solution Explorer over a
/// directory with fifty thousand files must be able to say "this folder can be
/// opened" without listing it, and a contract that required a materialized tree
/// would make that impossible.
/// </summary>
public interface ITreeDataSource
{
    /// <summary>The root, which the view does not display.</summary>
    TreeNodeId Root { get; }

    /// <summary>Children of a node, without enumerating them.</summary>
    int GetChildCount(TreeNodeId node);

    TreeNodeId GetChild(TreeNodeId node, int index);

    /// <summary>
    /// Whether the node shows an expander. Answered without expanding, so a
    /// folder can offer one before anything has listed it.
    /// </summary>
    bool CanExpand(TreeNodeId node);

    TreeNodePresentation GetPresentation(TreeNodeId node);
}

/// <summary>Raised when the data source's content changed underneath the view.</summary>
public sealed class TreeDataChangedEventArgs(TreeNodeId node) : EventArgs
{
    /// <summary>The subtree that changed, or None for the whole tree.</summary>
    public TreeNodeId Node { get; } = node;
}

/// <summary>A data source that can tell the view when it has changed.</summary>
public interface IObservableTreeDataSource : ITreeDataSource
{
    event EventHandler<TreeDataChangedEventArgs>? DataChanged;
}
