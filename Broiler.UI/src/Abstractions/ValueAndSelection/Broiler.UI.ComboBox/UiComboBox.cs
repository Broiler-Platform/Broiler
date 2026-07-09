using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;

namespace Broiler.UI.ComboBox;

public abstract class UiComboBox : UiElement
{
    private IReadOnlyList<UiComboBoxItem> _items = [];
    private int _selectedIndex = -1;
    private bool _isDropDownOpen;
    private bool _isEnabled = true;
    private BSize _preferredSize = new(180, 32);
    private int _maxDropDownItems = 8;

    public event EventHandler<UiComboBoxSelectionChangedEventArgs>? SelectionChanged;

    public IReadOnlyList<UiComboBoxItem> Items => _items;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            ThrowIfDisposed();
            SelectIndex(value);
        }
    }

    public UiComboBoxItem? SelectedItem =>
        (uint)SelectedIndex < (uint)Items.Count ? Items[SelectedIndex] : null;

    public bool IsDropDownOpen
    {
        get => _isDropDownOpen;
        protected set
        {
            if (_isDropDownOpen == value)
                return;

            _isDropDownOpen = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            ThrowIfDisposed();
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            if (!value)
                CloseDropDown();
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
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred combo box size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public int MaxDropDownItems
    {
        get => _maxDropDownItems;
        set
        {
            ThrowIfDisposed();
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Maximum drop-down items must be positive.");
            _maxDropDownItems = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public void SetItems(IEnumerable<UiComboBoxItem> items)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(items);
        UiComboBoxItem[] copy = items.ToArray();
        if (copy.Any(static item => string.IsNullOrWhiteSpace(item.Id)))
            throw new ArgumentException("ComboBox item IDs must be non-empty.", nameof(items));
        if (copy.Select(static item => item.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("ComboBox item IDs must be unique.", nameof(items));

        _items = copy;
        if (_selectedIndex >= copy.Length)
            SelectIndex(copy.Length == 0 ? -1 : copy.Length - 1);
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public bool SelectIndex(int index)
    {
        ThrowIfDisposed();
        if (index < -1 || index >= Items.Count)
            return false;
        if (_selectedIndex == index)
            return false;

        int oldIndex = _selectedIndex;
        _selectedIndex = index;
        SelectionChanged?.Invoke(this, new UiComboBoxSelectionChangedEventArgs(oldIndex, _selectedIndex));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    public bool OpenDropDown()
    {
        ThrowIfDisposed();
        if (!IsEnabled || IsDropDownOpen || Items.Count == 0)
            return false;

        IsDropDownOpen = true;
        return true;
    }

    public bool CloseDropDown()
    {
        ThrowIfDisposed();
        if (!IsDropDownOpen)
            return false;

        IsDropDownOpen = false;
        return true;
    }

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.ComboBox,
            SelectedItem?.Text ?? string.Empty,
            Bounds,
            CreateSemanticState(),
            []);

    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (IsEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        if (IsDropDownOpen)
            state |= UiSemanticState.Expanded;
        return state;
    }
}
