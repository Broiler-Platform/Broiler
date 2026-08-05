using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;

namespace Broiler.UI.TabView;

public abstract class UiTabView : UiElement
{
    private readonly List<UiTabItem> _tabs = [];
    private int _selectedIndex = -1;
    private BSize _preferredSize = new(320, 220);
    private UiTabContentLifetimePolicy _inactiveContentPolicy;

    public event EventHandler<UiTabSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    /// A close affordance was used. The control does not act on it; a host that
    /// ignores this leaves the tab open. See ADR 0024.
    /// </summary>
    public event EventHandler<UiTabCloseRequestedEventArgs>? CloseRequested;

    /// <summary>Raised before a reorder is applied, and cancellable.</summary>
    public event EventHandler<UiTabReorderingEventArgs>? Reordering;

    /// <summary>A tab's header or dirty state changed.</summary>
    public event EventHandler<UiTabChangedEventArgs>? TabChanged;

    public event EventHandler<UiTabChangedEventArgs>? TabRemoved;

    public IReadOnlyList<UiTabItem> Tabs => _tabs;

    /// <summary>
    /// How many tabs fit in the strip. Tabs beyond this are reachable through
    /// the overflow affordance rather than being shrunk below legibility.
    /// </summary>
    public int VisibleTabCapacity { get; set; } = int.MaxValue;

    /// <summary>Tabs that do not fit, in order.</summary>
    public IReadOnlyList<UiTabItem> OverflowTabs =>
        _tabs.Count <= VisibleTabCapacity ? [] : [.. _tabs.Skip(VisibleTabCapacity)];

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            ThrowIfDisposed();
            SelectIndex(value);
        }
    }

    public UiTabItem? SelectedTab =>
        (uint)SelectedIndex < (uint)Tabs.Count ? Tabs[SelectedIndex] : null;

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred tab view size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public UiTabContentLifetimePolicy InactiveContentPolicy
    {
        get => _inactiveContentPolicy;
        set
        {
            ThrowIfDisposed();
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_inactiveContentPolicy == value)
                return;

            _inactiveContentPolicy = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public UiTabItem AddTab(string id, string header, UiElement? content = null)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Tab IDs must be non-empty.", nameof(id));
        if (_tabs.Any(tab => StringComparer.Ordinal.Equals(tab.Id, id)))
            throw new ArgumentException("Tab IDs must be unique.", nameof(id));

        var item = new UiTabItem(id, header ?? string.Empty, content);
        item.Changed += OnTabChanged;
        _tabs.Add(item);
        if (content is not null)
            AddChild(content);
        if (_selectedIndex < 0)
            _selectedIndex = 0;

        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return item;
    }

    public UiTabItem? FindTab(string id) =>
        _tabs.FirstOrDefault(tab => StringComparer.Ordinal.Equals(tab.Id, id));

    /// <summary>
    /// Activates a tab by identity. Returns false when no such tab exists, so a
    /// host can tell "already open, now focused" from "not open".
    /// </summary>
    public bool SelectTab(string id)
    {
        ThrowIfDisposed();
        int index = IndexOf(id);
        if (index < 0)
            return false;

        // Selecting a tab in the overflow brings it into the visible strip: the
        // active tab must always be reachable.
        if (index >= VisibleTabCapacity)
            MoveTab(id, Math.Max(0, VisibleTabCapacity - 1), raiseEvent: false);

        int target = IndexOf(id);
        return _selectedIndex == target || SelectIndex(target);
    }

    /// <summary>
    /// Asks the host to close a tab. The control changes nothing.
    /// </summary>
    public void RequestClose(string id)
    {
        ThrowIfDisposed();
        if (FindTab(id) is { } tab)
            CloseRequested?.Invoke(this, new UiTabCloseRequestedEventArgs(tab));
    }

    /// <summary>
    /// Removes a tab. This is the host acting on a decision it has already
    /// made — the control never calls it in response to a gesture.
    /// </summary>
    public bool RemoveTab(string id)
    {
        ThrowIfDisposed();
        int index = IndexOf(id);
        if (index < 0)
            return false;

        UiTabItem tab = _tabs[index];
        tab.Changed -= OnTabChanged;
        _tabs.RemoveAt(index);
        if (tab.Content is not null)
            RemoveChild(tab.Content);

        // Focus moves deterministically: to the next tab, or to the previous
        // one when the closed tab was last, or nowhere when none remain. It
        // never lands on a removed element.
        if (_tabs.Count == 0)
            _selectedIndex = -1;
        else if (_selectedIndex > index || _selectedIndex >= _tabs.Count)
            _selectedIndex = Math.Clamp(_selectedIndex - 1, 0, _tabs.Count - 1);

        TabRemoved?.Invoke(this, new UiTabChangedEventArgs(tab));
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange |
            UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    /// <summary>Moves a tab, keeping the selection on the same tab.</summary>
    public bool MoveTab(string id, int newIndex) => MoveTab(id, newIndex, raiseEvent: true);

    private bool MoveTab(string id, int newIndex, bool raiseEvent)
    {
        ThrowIfDisposed();
        int index = IndexOf(id);
        if (index < 0)
            return false;

        newIndex = Math.Clamp(newIndex, 0, _tabs.Count - 1);
        if (newIndex == index)
            return false;

        UiTabItem tab = _tabs[index];
        if (raiseEvent)
        {
            var args = new UiTabReorderingEventArgs(tab, index, newIndex);
            Reordering?.Invoke(this, args);
            if (args.Cancel)
                return false;
        }

        UiTabItem? selected = SelectedTab;
        _tabs.RemoveAt(index);
        _tabs.Insert(newIndex, tab);

        // The selection follows the tab, not the slot. Reordering must not
        // silently activate a different document.
        if (selected is not null)
            _selectedIndex = _tabs.IndexOf(selected);

        Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    private int IndexOf(string id)
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            if (StringComparer.Ordinal.Equals(_tabs[i].Id, id))
                return i;
        }

        return -1;
    }

    private void OnTabChanged(UiTabItem tab)
    {
        TabChanged?.Invoke(this, new UiTabChangedEventArgs(tab));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public bool SelectIndex(int index)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)_tabs.Count)
            return false;
        if (_selectedIndex == index)
            return false;

        int oldIndex = _selectedIndex;
        _selectedIndex = index;
        SelectionChanged?.Invoke(this, new UiTabSelectionChangedEventArgs(oldIndex, _selectedIndex));
        Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.TabView,
            SelectedTab?.Header ?? string.Empty,
            Bounds,
            CreateSemanticState(),
            CreateTabSemanticNodes());

    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible | UiSemanticState.Enabled : UiSemanticState.None;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        return state;
    }

    private IReadOnlyList<UiSemanticNode> CreateTabSemanticNodes()
    {
        var nodes = new List<UiSemanticNode>(_tabs.Count);
        for (int index = 0; index < _tabs.Count; index++)
        {
            UiTabItem tab = _tabs[index];
            UiSemanticState state = UiSemanticState.Visible | UiSemanticState.Enabled;
            if (index == SelectedIndex)
                state |= UiSemanticState.Selected;

            // A tab beyond the visible capacity is reachable but not on screen.
            if (index >= VisibleTabCapacity)
                state |= UiSemanticState.Offscreen;

            // Dirty state is spoken, not just drawn. A marker a screen reader
            // cannot see is a marker half the users do not have.
            string name = tab.IsDirty
                ? $"{tab.Header}, unsaved changes, {index + 1} of {_tabs.Count}"
                : $"{tab.Header}, {index + 1} of {_tabs.Count}";
            nodes.Add(new UiSemanticNode(UiSemanticRole.Generic, name, Bounds, state, []));
        }

        return nodes;
    }
}
