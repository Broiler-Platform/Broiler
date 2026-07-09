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

    public IReadOnlyList<UiTabItem> Tabs => _tabs;

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
        _tabs.Add(item);
        if (content is not null)
            AddChild(content);
        if (_selectedIndex < 0)
            _selectedIndex = 0;

        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return item;
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
            UiSemanticState state = UiSemanticState.Visible | UiSemanticState.Enabled;
            if (index == SelectedIndex)
                state |= UiSemanticState.Selected;
            nodes.Add(new UiSemanticNode(UiSemanticRole.Generic, _tabs[index].Header, Bounds, state, []));
        }

        return nodes;
    }
}
