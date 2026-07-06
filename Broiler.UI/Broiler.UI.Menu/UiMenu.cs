using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;

namespace Broiler.UI.Menu;

public abstract class UiMenu : UiElement
{
    private IReadOnlyList<UiMenuItem> _items = [];
    private IReadOnlyList<int> _selectedPath = [];
    private bool _isOpen;
    private UiMenuPresentationMode _presentationMode;
    private int _maxDepth = 4;
    private BSize _preferredSize = new(320, 28);

    public event EventHandler<UiMenuItemInvokedEventArgs>? ItemInvoked;

    public IReadOnlyList<UiMenuItem> Items => _items;

    public IReadOnlyList<int> SelectedPath => _selectedPath;

    public bool IsOpen
    {
        get => _isOpen;
        protected set
        {
            if (_isOpen == value)
                return;

            _isOpen = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public UiMenuPresentationMode PresentationMode
    {
        get => _presentationMode;
        set
        {
            ThrowIfDisposed();
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_presentationMode == value)
                return;

            _presentationMode = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public int MaxDepth
    {
        get => _maxDepth;
        set
        {
            ThrowIfDisposed();
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Maximum menu depth must be positive.");
            _maxDepth = value;
            if (_selectedPath.Count > _maxDepth)
                SetSelectedPath(_selectedPath.Take(_maxDepth));
        }
    }

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred menu size must be non-negative.");
            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public void SetItems(IEnumerable<UiMenuItem> items)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToArray();
        _selectedPath = [];
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public bool Open()
    {
        ThrowIfDisposed();
        if (Items.Count == 0)
            return false;
        if (SelectedPath.Count == 0)
            SetSelectedPath([0]);
        IsOpen = true;
        return true;
    }

    public bool Close()
    {
        ThrowIfDisposed();
        if (!IsOpen)
            return false;
        IsOpen = false;
        return true;
    }

    public bool SetSelectedPath(IEnumerable<int> path)
    {
        ThrowIfDisposed();
        int[] copy = path.Take(MaxDepth).ToArray();
        if (copy.Length == 0 || GetItem(copy) is null)
            return false;
        if (_selectedPath.SequenceEqual(copy))
            return false;

        _selectedPath = copy;
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    public UiMenuItem? GetItem(IReadOnlyList<int> path)
    {
        IReadOnlyList<UiMenuItem> current = Items;
        UiMenuItem? item = null;
        for (int depth = 0; depth < path.Count; depth++)
        {
            int index = path[depth];
            if ((uint)index >= (uint)current.Count)
                return null;

            item = current[index];
            current = item.Children.ToArray();
        }

        return item;
    }

    protected bool InvokeSelected()
    {
        UiMenuItem? item = GetItem(SelectedPath);
        if (item is null || item.IsSeparator || !item.IsEnabled || item.Children.Count > 0)
            return false;

        if (item.IsCheckable)
            item.IsChecked = !item.IsChecked;
        ItemInvoked?.Invoke(this, new UiMenuItemInvokedEventArgs(item, SelectedPath.ToArray()));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Menu,
            PresentationMode.ToString(),
            Bounds,
            CreateSemanticState(),
            []);

    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible | UiSemanticState.Enabled : UiSemanticState.None;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        if (IsOpen)
            state |= UiSemanticState.Expanded;
        return state;
    }
}
