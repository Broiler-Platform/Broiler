using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Code.Workspaces;
using Broiler.Code.Workspaces.Model;
using Broiler.UI.TreeView;

namespace Broiler.Code.Core.Shell;

/// <summary>
/// Presents a workspace to <see cref="UiTreeView"/>.
///
/// It is a data source, not a control: the tree owns virtualization, keyboard
/// navigation, and semantics, and this decides only what the nodes are. That
/// split is why the tree could be tested against fifty thousand rows without a
/// workspace, and why this can be tested without a UI.
///
/// Node IDs are derived from workspace identity rather than from paths, so a
/// rename keeps a node's expansion and selection.
/// </summary>
public sealed class SolutionExplorerSource : IObservableTreeDataSource, IDisposable
{
    private readonly CodeWorkspace _workspace;
    private readonly Dictionary<string, Node> _nodes = [];
    private bool _valid;
    private bool _disposed;

    public SolutionExplorerSource(CodeWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _workspace.ItemChanged += OnItemChanged;
        _workspace.DocumentOpened += OnDocumentChanged;
        _workspace.DocumentClosed += OnDocumentClosed;
    }

    public event EventHandler<TreeDataChangedEventArgs>? DataChanged;

    public TreeNodeId Root => new("root");

    /// <summary>
    /// The review state to badge a file row with, when the host composed the
    /// review workspace.
    ///
    /// A delegate rather than a reference to the controller, so this stays
    /// testable with no review store and so the explorer keeps working
    /// unchanged on a host that does not compose one. It is called once per
    /// visible row per repaint, so the implementation behind it must be a
    /// lookup — see <see cref="Review.ReviewController.StateFor"/>.
    /// </summary>
    public Func<string, Broiler.Code.Review.ReviewState>? ReviewStateOf { get; set; }

    /// <summary>The workspace item a node stands for, or None for a grouping node.</summary>
    public WorkspaceItemId ItemFor(TreeNodeId node)
    {
        EnsureBuilt();
        return _nodes.TryGetValue(node.Value, out Node? found) ? found.Item : WorkspaceItemId.None;
    }

    /// <summary>The ancestors of a node, outermost first, for RevealNode.</summary>
    public IReadOnlyList<TreeNodeId> AncestorsOf(TreeNodeId node)
    {
        EnsureBuilt();
        var chain = new List<TreeNodeId>();
        string? current = _nodes.TryGetValue(node.Value, out Node? found) ? found.Parent : null;
        while (current is not null && current != Root.Value)
        {
            chain.Add(new TreeNodeId(current));
            current = _nodes.TryGetValue(current, out Node? parent) ? parent.Parent : null;
        }

        chain.Reverse();
        return chain;
    }

    /// <summary>Finds the node standing for a workspace item.</summary>
    public TreeNodeId NodeFor(WorkspaceItemId item)
    {
        EnsureBuilt();
        foreach (Node node in _nodes.Values)
        {
            if (node.Item == item)
                return new TreeNodeId(node.Key);
        }

        return TreeNodeId.None;
    }

    public int GetChildCount(TreeNodeId node)
    {
        EnsureBuilt();
        return _nodes.TryGetValue(node.Value, out Node? found) ? found.Children.Count : 0;
    }

    public TreeNodeId GetChild(TreeNodeId node, int index)
    {
        EnsureBuilt();
        return new TreeNodeId(_nodes[node.Value].Children[index]);
    }

    public bool CanExpand(TreeNodeId node)
    {
        EnsureBuilt();
        return _nodes.TryGetValue(node.Value, out Node? found) && found.Children.Count > 0;
    }

    public TreeNodePresentation GetPresentation(TreeNodeId node)
    {
        EnsureBuilt();
        if (!_nodes.TryGetValue(node.Value, out Node? found))
            return new TreeNodePresentation(node, node.Value);

        // A dirty decoration comes from the open document, so the explorer and
        // the tab strip agree about what has unsaved changes.
        TreeNodeDecoration decoration = TreeNodeDecoration.None;
        if (!found.Item.IsNone && _workspace.FindOpenDocument(found.Item) is { IsDirty: true })
            decoration = TreeNodeDecoration.Dirty;

        string? secondary = found.Secondary;

        // Unsaved work outranks a review badge on the same row. There is one
        // decoration per row, and a file being edited right now is the more
        // urgent of the two things to say about it — its review state is about
        // to change anyway.
        if (ReviewStateOf is { } review && found.IconKey == "source" &&
            !found.Item.IsNone && _workspace.FindItem(found.Item) is { } item)
        {
            Broiler.Code.Review.ReviewState state = review(item.RelativePath);
            if (state.IsAttested || state.OpenNotes > 0)
            {
                secondary = found.Secondary is { Length: > 0 } existing
                    ? $"{existing} · {state.ToDisplayString()}"
                    : state.ToDisplayString();

                if (decoration == TreeNodeDecoration.None)
                    decoration = Review.ReviewPaneSource.DecorationFor(state);
            }
        }

        return new TreeNodePresentation(node, found.Label, secondary, found.IconKey, decoration);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _workspace.ItemChanged -= OnItemChanged;
        _workspace.DocumentOpened -= OnDocumentChanged;
        _workspace.DocumentClosed -= OnDocumentClosed;
    }

    /// <summary>Rebuilds from the workspace and tells the view.</summary>
    public void Refresh()
    {
        _valid = false;
        DataChanged?.Invoke(this, new TreeDataChangedEventArgs(TreeNodeId.None));
    }

    private void OnItemChanged(WorkspaceItem item) => Refresh();

    private void OnDocumentChanged(SourceDocument document) =>
        DataChanged?.Invoke(this, new TreeDataChangedEventArgs(NodeFor(document.Id)));

    private void OnDocumentClosed(WorkspaceItemId id) =>
        DataChanged?.Invoke(this, new TreeDataChangedEventArgs(NodeFor(id)));

    private void EnsureBuilt()
    {
        if (_valid)
            return;

        _nodes.Clear();
        var root = new Node("root", null, "Workspace", null, "workspace", WorkspaceItemId.None);
        _nodes["root"] = root;

        foreach (CodeSolution solution in _workspace.Solutions)
        {
            string solutionKey = $"solution:{solution.Id}";
            var solutionNode = new Node(
                solutionKey, "root", solution.Name, $"{solution.Projects.Count} projects",
                "solution", solution.Id);
            _nodes[solutionKey] = solutionNode;
            root.Children.Add(solutionKey);

            foreach (WorkspaceItemId projectId in solution.Projects)
            {
                CodeProject? project = _workspace.Projects.FirstOrDefault(p => p.Id == projectId);
                if (project is null)
                    continue;

                string projectKey = $"project:{project.Id}";

                // A project the declared model cannot fully edit says so here,
                // rather than surprising the user when an edit is refused.
                var projectNode = new Node(
                    projectKey,
                    solutionKey,
                    project.Name,
                    project.HasUnsupportedConstructs
                        ? "read-only: unsupported constructs"
                        : string.Join(", ", project.TargetFrameworks),
                    "project",
                    project.Id);
                _nodes[projectKey] = projectNode;
                solutionNode.Children.Add(projectKey);

                foreach (WorkspaceItemId itemId in project.Items)
                {
                    WorkspaceItem? item = _workspace.FindItem(itemId);
                    if (item is null)
                        continue;

                    string itemKey = $"item:{item.Id}";
                    _nodes[itemKey] = new Node(
                        itemKey,
                        projectKey,
                        item.Name,

                        // A linked file shows where it really lives; a user
                        // otherwise has no way to tell it is not in the project
                        // directory, and editing it affects every project that
                        // links it.
                        item.IsLinked ? $"linked from {item.RelativePath}" : null,
                        "source",
                        item.Id);
                    projectNode.Children.Add(itemKey);
                }
            }
        }

        _valid = true;
    }

    private sealed record Node(
        string Key,
        string? Parent,
        string Label,
        string? Secondary,
        string IconKey,
        WorkspaceItemId Item)
    {
        public List<string> Children { get; } = [];
    }
}
