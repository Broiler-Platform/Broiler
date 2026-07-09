using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;

namespace Broiler.UI.ListView;

public abstract class UiListView : UiElement
{
    private IReadOnlyList<UiListItem> _items = [];
    private string? _selectedItemId;
    private double _verticalOffset;
    private BSize _preferredSize = new(200, 160);

    public event EventHandler<UiListSelectionChangedEventArgs>? SelectionChanged;

    public IReadOnlyList<UiListItem> Items => _items;

    public string? SelectedItemId
    {
        get => _selectedItemId;
        set
        {
            ThrowIfDisposed();
            SelectItem(value);
        }
    }

    public int SelectedIndex => _selectedItemId is null ? -1 : IndexOf(_selectedItemId);

    public double VerticalOffset
    {
        get => _verticalOffset;
        protected set
        {
            double normalized = Normalize(value);
            if (_verticalOffset.Equals(normalized))
                return;

            _verticalOffset = normalized;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred list view size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public void SetItems(IEnumerable<UiListItem> items)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(items);
        UiListItem[] copy = items.ToArray();
        if (copy.Any(static item => string.IsNullOrWhiteSpace(item.Id)))
            throw new ArgumentException("List item IDs must be non-empty.", nameof(items));
        if (copy.Select(static item => item.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("List item IDs must be unique.", nameof(items));

        string? oldSelection = _selectedItemId;
        _items = copy;
        if (_selectedItemId is not null && IndexOf(_selectedItemId) < 0)
            _selectedItemId = null;

        if (!StringComparer.Ordinal.Equals(oldSelection, _selectedItemId))
            SelectionChanged?.Invoke(this, new UiListSelectionChangedEventArgs(oldSelection, _selectedItemId));

        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public bool SelectItem(string? itemId)
    {
        ThrowIfDisposed();
        if (itemId is not null && IndexOf(itemId) < 0)
            return false;
        if (StringComparer.Ordinal.Equals(_selectedItemId, itemId))
            return false;

        string? oldSelection = _selectedItemId;
        _selectedItemId = itemId;
        SelectionChanged?.Invoke(this, new UiListSelectionChangedEventArgs(oldSelection, _selectedItemId));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    public bool SelectIndex(int index)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)Items.Count)
            return false;
        return SelectItem(Items[index].Id);
    }

    protected int IndexOf(string itemId)
    {
        for (int index = 0; index < Items.Count; index++)
        {
            if (StringComparer.Ordinal.Equals(Items[index].Id, itemId))
                return index;
        }

        return -1;
    }

    protected void SetVerticalOffset(double value) => VerticalOffset = value;

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.ListView,
            Items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Bounds,
            CreateSemanticState(),
            CreateVisibleSemanticNodes());

    protected virtual IReadOnlyList<UiSemanticNode> CreateVisibleSemanticNodes() => [];

    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        return state;
    }

    private static double Normalize(double value)
    {
        if (double.IsNaN(value) || double.IsNegativeInfinity(value))
            return 0;
        if (double.IsPositiveInfinity(value))
            return double.MaxValue;
        return Math.Max(0, value);
    }
}
