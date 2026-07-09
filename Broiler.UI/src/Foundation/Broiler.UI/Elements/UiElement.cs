using System;
using System.Collections.Generic;
using Broiler.Graphics;

namespace Broiler.UI;

public abstract class UiElement : IDisposable
{
    private readonly List<UiElement> _children = [];
    private UiVisibility _visibility = UiVisibility.Visible;
    private bool _isDisposed;

    public UiElement? Parent { get; private set; }

    public UiSession? Session { get; private set; }

    public IReadOnlyList<UiElement> Children => _children;

    public BSize DesiredSize { get; private set; }

    public BRect Bounds { get; private set; }

    public UiVisibility Visibility
    {
        get => _visibility;
        set
        {
            ThrowIfDisposed();
            if (_visibility == value)
                return;

            _visibility = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public bool IsAttached => Session is not null;

    public bool IsDisposed => _isDisposed;

    public void AddChild(UiElement child) => InsertChild(_children.Count, child);

    public void InsertChild(int index, UiElement child)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(child);
        child.ThrowIfDisposed();

        if ((uint)index > (uint)_children.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (ReferenceEquals(child, this) || IsDescendantOf(child))
            throw new InvalidOperationException("Adding the element would create a cycle.");
        if (child.Parent is not null || (child.Session is not null && child.Session != Session))
            throw new InvalidOperationException("A UI element can belong to only one tree.");

        _children.Insert(index, child);
        child.Parent = this;
        if (Session is not null)
            child.AttachToSession(Session);

        OnChildAdded(child);
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public bool MoveChildToFront(UiElement child) => MoveChild(child, _children.Count - 1);

    public bool MoveChildToBack(UiElement child) => MoveChild(child, 0);

    public bool MoveChild(UiElement child, int newIndex)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(child);
        if ((uint)newIndex >= (uint)_children.Count)
            throw new ArgumentOutOfRangeException(nameof(newIndex));

        int oldIndex = _children.IndexOf(child);
        if (oldIndex < 0)
            return false;
        if (oldIndex == newIndex)
            return false;

        _children.RemoveAt(oldIndex);
        _children.Insert(newIndex, child);
        Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    public bool RemoveChild(UiElement child)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(child);

        int index = _children.IndexOf(child);
        if (index < 0)
            return false;

        _children.RemoveAt(index);
        if (child.Session is not null)
            child.DetachFromSession();

        child.Parent = null;
        OnChildRemoved(child);
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    public BSize Measure(BSize availableSize)
    {
        ThrowIfDisposed();
        DesiredSize = Visibility == UiVisibility.Collapsed ? BSize.Empty : MeasureCore(availableSize);
        return DesiredSize;
    }

    public void Arrange(BRect finalRect)
    {
        ThrowIfDisposed();
        Bounds = Visibility == UiVisibility.Collapsed ? BRect.Empty : finalRect;
        ArrangeCore(Bounds);
    }

    public void Render(UiRenderContext context)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(context);
        if (Visibility != UiVisibility.Visible)
            return;

        RenderCore(context);
    }

    public bool DispatchInput(UiInputEvent input)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(input);
        if (Visibility != UiVisibility.Visible)
            return false;

        return OnInput(input);
    }

    public void Invalidate(UiInvalidationKind kind)
    {
        if (kind == UiInvalidationKind.None || _isDisposed)
            return;

        Session?.Invalidate(this, kind);
    }

    public UiSemanticNode GetSemanticNode() => GetSemanticNodeCore();

    public void Dispose()
    {
        if (_isDisposed)
            return;

        Dispose(disposing: true);
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    protected virtual BSize MeasureCore(BSize availableSize)
    {
        foreach (UiElement child in _children)
            child.Measure(availableSize);
        return BSize.Empty;
    }

    protected virtual void ArrangeCore(BRect finalRect)
    {
        foreach (UiElement child in _children)
            child.Arrange(finalRect);
    }

    protected virtual void RenderCore(UiRenderContext context)
    {
        foreach (UiElement child in _children)
            child.Render(context);
    }

    protected virtual bool OnInput(UiInputEvent input)
    {
        return false;
    }

    protected virtual UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Generic,
            GetType().Name,
            Bounds,
            Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None,
            CreateChildSemanticNodes());

    protected virtual void OnAttached()
    {
    }

    protected virtual void OnDetached()
    {
    }

    protected virtual void OnChildAdded(UiElement child)
    {
    }

    protected virtual void OnChildRemoved(UiElement child)
    {
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (UiElement child in _children.ToArray())
                child.Dispose();
            _children.Clear();

            if (Parent is not null)
                Parent.RemoveChild(this);
            else
                Session?.RemoveRoot(this);
        }
    }

    internal void AttachToSession(UiSession session)
    {
        if (Session is not null)
        {
            if (Session == session)
                return;
            throw new InvalidOperationException("A UI element can attach to only one session.");
        }

        Session = session;
        OnAttached();
        foreach (UiElement child in _children)
            child.AttachToSession(session);
    }

    internal void DetachFromSession()
    {
        if (Session is null)
            return;

        foreach (UiElement child in _children)
            child.DetachFromSession();
        OnDetached();
        Session = null;
    }

    public bool IsDescendantOf(UiElement possibleAncestor)
    {
        ArgumentNullException.ThrowIfNull(possibleAncestor);
        UiElement? current = Parent;
        while (current is not null)
        {
            if (ReferenceEquals(current, possibleAncestor))
                return true;
            current = current.Parent;
        }

        return false;
    }

    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);

    private IReadOnlyList<UiSemanticNode> CreateChildSemanticNodes()
    {
        if (_children.Count == 0)
            return [];

        var nodes = new List<UiSemanticNode>(_children.Count);
        foreach (UiElement child in _children)
        {
            if (child.Visibility != UiVisibility.Collapsed)
                nodes.Add(child.GetSemanticNode());
        }

        return nodes;
    }
}
