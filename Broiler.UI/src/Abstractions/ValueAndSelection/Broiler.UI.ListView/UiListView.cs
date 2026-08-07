using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;

namespace Broiler.UI.ListView;

public abstract class UiListView : UiElement
{
    private IReadOnlyList<UiListItem> _items = [];
    private string? _selectedItemId;
    private readonly List<string> _selectedItemIds = [];
    private UiListSelectionMode _selectionMode = UiListSelectionMode.Single;
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

    /// <summary>
    /// Whether more than one item may be selected. <see cref="UiListSelectionMode.Single"/>
    /// by default, so a list that never asks for multi-selection behaves exactly as
    /// it always has.
    /// </summary>
    /// <remarks>
    /// Narrowing to <see cref="UiListSelectionMode.Single"/> keeps the primary
    /// selection and drops the rest, so the invariant "Single implies at most one
    /// selected item" holds however the mode is changed.
    /// </remarks>
    public UiListSelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            ThrowIfDisposed();
            if (_selectionMode == value)
                return;

            _selectionMode = value;
            if (value == UiListSelectionMode.Single && _selectedItemIds.Count > 1)
                SelectItem(_selectedItemId);
        }
    }

    /// <summary>
    /// Every selected item, in item order. A single-selection list reports its one
    /// selection, or nothing.
    /// </summary>
    public IReadOnlyList<string> SelectedItemIds => _selectedItemIds;

    /// <summary>Whether <paramref name="itemId"/> is currently selected.</summary>
    public bool IsSelected(string itemId) => _selectedItemIds.Contains(itemId, StringComparer.Ordinal);

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
        string[] oldSelections = [.. _selectedItemIds];
        _items = copy;

        // Items that are gone cannot stay selected.
        _selectedItemIds.RemoveAll(id => IndexOf(id) < 0);
        if (_selectedItemId is not null && IndexOf(_selectedItemId) < 0)
            _selectedItemId = _selectedItemIds.Count > 0 ? _selectedItemIds[0] : null;

        if (!StringComparer.Ordinal.Equals(oldSelection, _selectedItemId) ||
            !oldSelections.SequenceEqual(_selectedItemIds, StringComparer.Ordinal))
        {
            SelectionChanged?.Invoke(
                this,
                new UiListSelectionChangedEventArgs(oldSelection, _selectedItemId, oldSelections, [.. _selectedItemIds]));
        }

        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    /// <summary>
    /// Selects exactly <paramref name="itemId"/>, replacing anything else selected,
    /// and makes it the primary. Passing <c>null</c> clears the selection.
    /// </summary>
    public bool SelectItem(string? itemId)
    {
        ThrowIfDisposed();
        if (itemId is not null && IndexOf(itemId) < 0)
            return false;

        return Apply(itemId, itemId is null ? [] : [itemId]);
    }

    /// <summary>
    /// Replaces the selection with <paramref name="itemIds"/>. Unknown ids are
    /// ignored; in <see cref="UiListSelectionMode.Single"/> only the first survives,
    /// so the mode's invariant cannot be broken from outside.
    /// </summary>
    public bool SetSelectedItems(IEnumerable<string> itemIds)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(itemIds);

        List<string> resolved = [];
        foreach (string itemId in itemIds)
        {
            if (IndexOf(itemId) >= 0 && !resolved.Contains(itemId, StringComparer.Ordinal))
                resolved.Add(itemId);
        }

        if (SelectionMode == UiListSelectionMode.Single && resolved.Count > 1)
            resolved.RemoveRange(1, resolved.Count - 1);

        return Apply(resolved.Count > 0 ? resolved[0] : null, resolved);
    }

    /// <summary>
    /// Adds <paramref name="itemId"/> to the selection, or removes it if already
    /// selected — Ctrl-clicking a row. In <see cref="UiListSelectionMode.Single"/>
    /// this is <see cref="SelectItem"/>, since there is nothing to add to.
    /// </summary>
    public bool ToggleItem(string itemId)
    {
        ThrowIfDisposed();
        if (IndexOf(itemId) < 0)
            return false;

        if (SelectionMode == UiListSelectionMode.Single)
            return SelectItem(itemId);

        List<string> next = [.. _selectedItemIds];
        if (!next.Remove(itemId))
            next.Add(itemId);

        // The toggled item becomes the anchor a later range extends from, whether it
        // was just added or just removed.
        return Apply(itemId, next);
    }

    /// <summary>
    /// Selects everything between the primary selection and <paramref name="itemId"/>
    /// inclusive — Shift-clicking a row. The anchor does not move, so shift-clicking
    /// repeatedly grows and shrinks one range rather than chaining them.
    /// </summary>
    public bool SelectRangeTo(string itemId)
    {
        ThrowIfDisposed();
        int target = IndexOf(itemId);
        if (target < 0)
            return false;

        if (SelectionMode == UiListSelectionMode.Single || _selectedItemId is null)
            return SelectItem(itemId);

        int anchor = IndexOf(_selectedItemId);
        if (anchor < 0)
            return SelectItem(itemId);

        List<string> range = [];
        for (int index = Math.Min(anchor, target); index <= Math.Max(anchor, target); index++)
            range.Add(Items[index].Id);

        return Apply(_selectedItemId, range);
    }

    private bool Apply(string? primary, IReadOnlyList<string> selection)
    {
        // Report in item order regardless of the order ids were supplied in, so a
        // consumer can rely on the selection matching the list it sees.
        List<string> ordered =
            [.. Items.Select(static item => item.Id).Where(id => selection.Contains(id, StringComparer.Ordinal))];

        if (StringComparer.Ordinal.Equals(_selectedItemId, primary) &&
            _selectedItemIds.SequenceEqual(ordered, StringComparer.Ordinal))
        {
            return false;
        }

        string? oldSelection = _selectedItemId;
        string[] oldSelections = [.. _selectedItemIds];

        _selectedItemId = primary;
        _selectedItemIds.Clear();
        _selectedItemIds.AddRange(ordered);

        SelectionChanged?.Invoke(
            this,
            new UiListSelectionChangedEventArgs(oldSelection, _selectedItemId, oldSelections, [.. _selectedItemIds]));
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
