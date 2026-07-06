using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.UI.Panel;

public abstract class UiPanel : UiElement
{
    private readonly Dictionary<UiElement, UiDock> _docks = [];
    private UiPanelLayoutMode _layoutMode;
    private UiStackOrientation _stackOrientation;
    private double _spacing;

    public UiPanelLayoutMode LayoutMode
    {
        get => _layoutMode;
        set
        {
            ThrowIfDisposed();
            if (_layoutMode == value)
                return;

            _layoutMode = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public UiStackOrientation StackOrientation
    {
        get => _stackOrientation;
        set
        {
            ThrowIfDisposed();
            if (_stackOrientation == value)
                return;

            _stackOrientation = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public double Spacing
    {
        get => _spacing;
        set
        {
            ThrowIfDisposed();
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Panel spacing must be non-negative.");
            if (_spacing.Equals(value))
                return;

            _spacing = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public void SetDock(UiElement child, UiDock dock)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(child);
        if (!Children.Contains(child))
            throw new InvalidOperationException("Dock metadata can only be assigned to a child of this panel.");

        _docks[child] = dock;
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
    }

    public UiDock GetDock(UiElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return _docks.TryGetValue(child, out UiDock dock) ? dock : UiDock.Fill;
    }

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Panel,
            GetType().Name,
            Bounds,
            Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None,
            CreateChildSemanticNodes());

    protected override void OnChildRemoved(UiElement child)
    {
        _docks.Remove(child);
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
