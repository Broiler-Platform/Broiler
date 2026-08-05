using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;

namespace Broiler.UI.TreeView;

public enum TreeSelectionMode
{
    Single,

    /// <summary>Shift and control extend the selection.</summary>
    Extended,
}

/// <summary>One row of the flattened, expanded tree.</summary>
public readonly record struct TreeRow(TreeNodeId Id, int Depth, bool HasChildren, bool IsExpanded);

public sealed class TreeSelectionChangedEventArgs(IReadOnlyList<TreeNodeId> selection) : EventArgs
{
    public IReadOnlyList<TreeNodeId> Selection { get; } = selection;
}

public sealed class TreeNodeEventArgs(TreeNodeId node) : EventArgs
{
    public TreeNodeId Node { get; } = node;
}

/// <summary>
/// Platform-neutral state and commands for a virtualized tree.
///
/// The control materializes rows for the expanded set, not for the whole tree,
/// and the renderer paints only the visible slice of those. Expanding a node
/// with ten thousand children costs ten thousand row structs and forty painted
/// rows — never ten thousand elements.
/// </summary>
public abstract class UiTreeView : UiElement
{
    private readonly HashSet<TreeNodeId> _expanded = [];
    private readonly List<TreeNodeId> _selection = [];
    private readonly List<TreeRow> _rows = [];
    private ITreeDataSource? _dataSource;
    private TreeSelectionMode _selectionMode = TreeSelectionMode.Single;
    private BSize _preferredSize = new(280, 400);
    private TreeNodeId _focused = TreeNodeId.None;
    private int _firstVisibleRow;
    private bool _rowsValid;

    public event EventHandler<TreeSelectionChangedEventArgs>? SelectionChanged;

    public event EventHandler<TreeNodeEventArgs>? NodeExpanded;

    public event EventHandler<TreeNodeEventArgs>? NodeCollapsed;

    /// <summary>A node was activated — double-click or Enter.</summary>
    public event EventHandler<TreeNodeEventArgs>? NodeActivated;

    public ITreeDataSource? DataSource
    {
        get => _dataSource;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_dataSource, value))
                return;

            if (_dataSource is IObservableTreeDataSource oldObservable)
                oldObservable.DataChanged -= OnDataChanged;
            _dataSource = value;
            if (_dataSource is IObservableTreeDataSource newObservable)
                newObservable.DataChanged += OnDataChanged;

            // Expansion and selection are kept: they are keyed by ID, and a
            // refresh that replaced the node objects should not collapse the
            // tree the user has arranged.
            InvalidateRows();
        }
    }

    public TreeSelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            ThrowIfDisposed();
            if (_selectionMode == value)
                return;
            _selectionMode = value;
            if (value == TreeSelectionMode.Single && _selection.Count > 1)
                SetSelection([_selection[^1]]);
        }
    }

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_preferredSize == value)
                return;
            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    /// <summary>The flattened expanded set. Rebuilt lazily.</summary>
    public IReadOnlyList<TreeRow> Rows
    {
        get
        {
            EnsureRows();
            return _rows;
        }
    }

    public IReadOnlyList<TreeNodeId> Selection => _selection;

    /// <summary>
    /// The single tab stop. Focus never moves to a node that is not in the
    /// expanded set, so it cannot land on a row that has scrolled out of
    /// existence.
    /// </summary>
    public TreeNodeId FocusedNode => _focused;

    public int FirstVisibleRow
    {
        get => _firstVisibleRow;
        set
        {
            ThrowIfDisposed();
            EnsureRows();
            int clamped = Math.Clamp(value, 0, Math.Max(0, _rows.Count - 1));
            if (_firstVisibleRow == clamped)
                return;
            _firstVisibleRow = clamped;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    /// <summary>How many rows the current bounds can show.</summary>
    public int VisibleRowCapacity { get; set; } = 20;

    public bool IsExpanded(TreeNodeId node) => _expanded.Contains(node);

    public bool Expand(TreeNodeId node)
    {
        ThrowIfDisposed();
        if (_dataSource is null || node.IsNone || !_dataSource.CanExpand(node) || !_expanded.Add(node))
            return false;
        InvalidateRows();
        NodeExpanded?.Invoke(this, new TreeNodeEventArgs(node));
        return true;
    }

    public bool Collapse(TreeNodeId node)
    {
        ThrowIfDisposed();
        if (!_expanded.Remove(node))
            return false;

        InvalidateRows();

        // Focus inside a collapsed subtree would be focus on nothing, so it
        // moves to the node the user just collapsed.
        if (!_focused.IsNone && !_rows.Any(row => row.Id == _focused))
            _focused = node;

        NodeCollapsed?.Invoke(this, new TreeNodeEventArgs(node));
        return true;
    }

    public bool ToggleExpansion(TreeNodeId node) =>
        IsExpanded(node) ? Collapse(node) : Expand(node);

    /// <summary>Expands every ancestor so a node becomes visible, then focuses it.</summary>
    public bool RevealNode(TreeNodeId node, IReadOnlyList<TreeNodeId> ancestors)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(ancestors);
        foreach (TreeNodeId ancestor in ancestors)
            Expand(ancestor);

        EnsureRows();
        int index = _rows.FindIndex(row => row.Id == node);
        if (index < 0)
            return false;

        _focused = node;
        SetSelection([node]);
        EnsureRowVisible(index);
        return true;
    }

    public void SetSelection(IReadOnlyList<TreeNodeId> nodes)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(nodes);

        var next = _selectionMode == TreeSelectionMode.Single && nodes.Count > 1
            ? [nodes[^1]]
            : nodes.Distinct().ToList();
        if (next.SequenceEqual(_selection))
            return;

        _selection.Clear();
        _selection.AddRange(next);
        if (_selection.Count > 0)
            _focused = _selection[^1];

        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        SelectionChanged?.Invoke(this, new TreeSelectionChangedEventArgs([.. _selection]));
    }

    /// <summary>Moves focus by row, optionally extending the selection.</summary>
    public bool MoveFocus(int delta, bool extendSelection)
    {
        ThrowIfDisposed();
        EnsureRows();
        if (_rows.Count == 0)
            return false;

        int current = _focused.IsNone ? -1 : _rows.FindIndex(row => row.Id == _focused);
        int target = Math.Clamp(current + delta, 0, _rows.Count - 1);
        if (target == current)
            return false;

        TreeNodeId node = _rows[target].Id;
        _focused = node;
        if (extendSelection && _selectionMode == TreeSelectionMode.Extended)
        {
            var extended = new List<TreeNodeId>(_selection);
            if (!extended.Contains(node))
                extended.Add(node);
            SetSelection(extended);
        }
        else
        {
            SetSelection([node]);
        }

        EnsureRowVisible(target);
        return true;
    }

    public bool MoveFocusToFirst() => MoveFocusTo(0);

    public bool MoveFocusToLast()
    {
        EnsureRows();
        return MoveFocusTo(_rows.Count - 1);
    }

    /// <summary>
    /// Selects the next node whose label starts with <paramref name="prefix"/>,
    /// searching from the focused row and wrapping. Only the expanded set is
    /// searched: type-ahead that opened collapsed folders would move the user
    /// somewhere they cannot see.
    /// </summary>
    public bool TypeAhead(string prefix)
    {
        ThrowIfDisposed();
        if (_dataSource is null || string.IsNullOrEmpty(prefix))
            return false;

        EnsureRows();
        int start = _focused.IsNone ? 0 : _rows.FindIndex(row => row.Id == _focused) + 1;
        for (int offset = 0; offset < _rows.Count; offset++)
        {
            int index = (start + offset) % _rows.Count;
            string label = _dataSource.GetPresentation(_rows[index].Id).Label;
            if (label.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
                return MoveFocusTo(index);
        }

        return false;
    }

    public void ActivateFocused()
    {
        ThrowIfDisposed();
        if (!_focused.IsNone)
            NodeActivated?.Invoke(this, new TreeNodeEventArgs(_focused));
    }

    /// <summary>Rebuilds the expanded set after the data source changed.</summary>
    public void Refresh() => InvalidateRows();

    protected override void Dispose(bool disposing)
    {
        if (disposing && _dataSource is IObservableTreeDataSource observable)
            observable.DataChanged -= OnDataChanged;
        base.Dispose(disposing);
    }

    protected override UiSemanticNode GetSemanticNodeCore()
    {
        EnsureRows();
        var children = new List<UiSemanticNode>();

        // Only the visible slice is described. A screen reader asking for the
        // node list of a 50,000-row tree must not receive 50,000 nodes; it
        // walks the visible window and scrolls like a sighted user does.
        int last = Math.Min(_rows.Count, _firstVisibleRow + VisibleRowCapacity);
        for (int i = _firstVisibleRow; i < last; i++)
        {
            TreeRow row = _rows[i];
            UiSemanticState state = UiSemanticState.Visible | UiSemanticState.Enabled;
            if (_selection.Contains(row.Id))
                state |= UiSemanticState.Selected;
            if (row.IsExpanded)
                state |= UiSemanticState.Expanded;
            if (_focused == row.Id)
                state |= UiSemanticState.Focused;

            children.Add(new UiSemanticNode(
                UiSemanticRole.ListView, DescribeRow(row, i), Bounds, state, []));
        }

        return new UiSemanticNode(
            UiSemanticRole.ListView,
            $"Tree, {_rows.Count} items",
            Bounds,
            Visibility == UiVisibility.Visible
                ? UiSemanticState.Visible | UiSemanticState.Enabled
                : UiSemanticState.None,
            children);
    }

    /// <summary>
    /// What a screen reader announces for a row. Level and position within the
    /// level are stated explicitly, because a tree's structure is not otherwise
    /// conveyed by a flat list of names.
    /// </summary>
    protected virtual string DescribeRow(TreeRow row, int rowIndex)
    {
        if (_dataSource is null)
            return string.Empty;

        TreeNodePresentation presentation = _dataSource.GetPresentation(row.Id);
        (int position, int count) = PositionWithinLevel(rowIndex, row.Depth);
        string decoration = presentation.Decoration switch
        {
            TreeNodeDecoration.Dirty => ", unsaved changes",
            TreeNodeDecoration.Error => ", error",
            TreeNodeDecoration.Warning => ", warning",
            TreeNodeDecoration.Information => ", information",
            _ => string.Empty,
        };
        string expansion = row.HasChildren ? (row.IsExpanded ? ", expanded" : ", collapsed") : string.Empty;

        return $"{presentation.Label}{decoration}{expansion}, level {row.Depth + 1}, " +
            $"{position} of {count}";
    }

    private (int Position, int Count) PositionWithinLevel(int rowIndex, int depth)
    {
        // Counted by walking outwards over siblings at the same depth, which
        // stops at the first shallower row in each direction.
        int position = 1;
        for (int i = rowIndex - 1; i >= 0 && _rows[i].Depth >= depth; i--)
        {
            if (_rows[i].Depth == depth)
                position++;
        }

        int count = position;
        for (int i = rowIndex + 1; i < _rows.Count && _rows[i].Depth >= depth; i++)
        {
            if (_rows[i].Depth == depth)
                count++;
        }

        return (position, count);
    }

    private bool MoveFocusTo(int index)
    {
        EnsureRows();
        if (index < 0 || index >= _rows.Count)
            return false;
        _focused = _rows[index].Id;
        SetSelection([_focused]);
        EnsureRowVisible(index);
        return true;
    }

    private void EnsureRowVisible(int index)
    {
        if (index < _firstVisibleRow)
            FirstVisibleRow = index;
        else if (index >= _firstVisibleRow + VisibleRowCapacity)
            FirstVisibleRow = index - VisibleRowCapacity + 1;
    }

    private void OnDataChanged(object? sender, TreeDataChangedEventArgs e) => InvalidateRows();

    private void InvalidateRows()
    {
        _rowsValid = false;
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange |
            UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private void EnsureRows()
    {
        if (_rowsValid)
            return;

        _rows.Clear();
        if (_dataSource is not null)
            AppendChildren(_dataSource.Root, depth: 0);
        _rowsValid = true;
        _firstVisibleRow = Math.Clamp(_firstVisibleRow, 0, Math.Max(0, _rows.Count - 1));
    }

    private void AppendChildren(TreeNodeId parent, int depth)
    {
        if (_dataSource is null)
            return;

        int count = _dataSource.GetChildCount(parent);
        for (int i = 0; i < count; i++)
        {
            TreeNodeId child = _dataSource.GetChild(parent, i);
            bool canExpand = _dataSource.CanExpand(child);
            bool expanded = canExpand && _expanded.Contains(child);
            _rows.Add(new TreeRow(child, depth, canExpand, expanded));

            // Only expanded subtrees are walked. A collapsed folder is one row
            // whatever it contains, which is what keeps opening a workspace
            // proportional to what is on screen.
            if (expanded)
                AppendChildren(child, depth + 1);
        }
    }
}
