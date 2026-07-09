using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;

namespace Broiler.UI.Toolbar;

public abstract class UiToolbar : UiElement
{
    private readonly HashSet<UiElement> _separatorBefore = [];
    private string _title = string.Empty;
    private UiToolbarOrientation _orientation;
    private bool _isEnabled = true;
    private double _spacing = 6;
    private double _padding = 6;
    private BSize _preferredSize = new(0, 42);

    public string Title
    {
        get => _title;
        set
        {
            ThrowIfDisposed();
            value ??= string.Empty;
            if (StringComparer.Ordinal.Equals(_title, value))
                return;

            _title = value;
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    public UiToolbarOrientation Orientation
    {
        get => _orientation;
        set
        {
            ThrowIfDisposed();
            if (_orientation == value)
                return;

            _orientation = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
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
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public double Spacing
    {
        get => _spacing;
        set
        {
            ThrowIfDisposed();
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Toolbar spacing must be non-negative.");
            if (_spacing.Equals(value))
                return;

            _spacing = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public double Padding
    {
        get => _padding;
        set
        {
            ThrowIfDisposed();
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Toolbar padding must be non-negative.");
            if (_padding.Equals(value))
                return;

            _padding = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred toolbar size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public void SetSeparatorBefore(UiElement child, bool hasSeparator)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(child);
        if (!Children.Contains(child))
            throw new InvalidOperationException("Toolbar separator metadata can only be assigned to a child of this toolbar.");

        bool changed = hasSeparator ? _separatorBefore.Add(child) : _separatorBefore.Remove(child);
        if (changed)
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public bool GetSeparatorBefore(UiElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return _separatorBefore.Contains(child);
    }

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Toolbar,
            string.IsNullOrWhiteSpace(Title) ? GetType().Name : Title,
            Bounds,
            CreateSemanticState(),
            CreateChildSemanticNodes());

    protected override void OnChildRemoved(UiElement child)
    {
        _separatorBefore.Remove(child);
    }

    private UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (IsEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        return state;
    }

    private IReadOnlyList<UiSemanticNode> CreateChildSemanticNodes()
    {
        if (Children.Count == 0)
            return [];

        var nodes = new List<UiSemanticNode>(Children.Count);
        foreach (UiElement child in Children)
        {
            if (child.Visibility != UiVisibility.Collapsed)
                nodes.Add(child.GetSemanticNode());
        }

        return nodes;
    }
}
