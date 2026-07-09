using System;
using System.Collections.Generic;
using Broiler.Graphics;

namespace Broiler.UI.Window;

public abstract class UiWindow : UiElement
{
    private readonly List<UiWindow> _ownedWindows = [];
    private string _title = string.Empty;
    private UiWindowState _state;
    private BRect _placement = BRect.Empty;
    private UiViewportBinding _viewportBinding;
    private bool _isActive;
    private bool _isClosed;

    public event EventHandler? Activated;

    public event EventHandler? Deactivated;

    public event EventHandler<UiWindowClosingEventArgs>? Closing;

    public event EventHandler<UiWindowClosedEventArgs>? Closed;

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
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public UiWindowKind Kind { get; private set; } = UiWindowKind.TopLevel;

    public UiWindowState State
    {
        get => _state;
        set
        {
            ThrowIfDisposed();
            if (_state == value)
                return;

            _state = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public UiWindow? Owner { get; private set; }

    public IReadOnlyList<UiWindow> OwnedWindows => _ownedWindows;

    public BRect Placement
    {
        get => _placement;
        private set
        {
            if (_placement == value)
                return;

            _placement = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public UiViewportBinding ViewportBinding => _viewportBinding;

    public bool IsActive => _isActive;

    public bool IsClosed => _isClosed;

    public void SetPlacement(BRect placement)
    {
        ThrowIfDisposed();
        Placement = placement;
    }

    public void BindViewport(UiViewportBinding binding)
    {
        ThrowIfDisposed();
        if (_viewportBinding == binding)
            return;

        _viewportBinding = binding;
        Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public void OpenOwnedWindow(UiWindow window) =>
        OpenOwnedWindow(window, BRect.Empty, UiWindowKind.Owned);

    public void OpenOwnedWindow(UiWindow window, BRect placement, UiWindowKind kind = UiWindowKind.Owned)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(window);
        window.ThrowIfDisposed();

        if (window.Owner is not null || window.Parent is not null || window.Session is not null)
            throw new InvalidOperationException("Owned windows must be unattached logical windows.");
        if (ReferenceEquals(window, this) || IsDescendantOf(window))
            throw new InvalidOperationException("A logical window cannot own itself or one of its ancestors.");

        window.Owner = this;
        window.Kind = kind == UiWindowKind.TopLevel ? UiWindowKind.Owned : kind;
        window.Placement = placement;
        try
        {
            AddChild(window);
            _ownedWindows.Add(window);
        }
        catch
        {
            window.Owner = null;
            window.Kind = UiWindowKind.TopLevel;
            window.Placement = BRect.Empty;
            throw;
        }

        window.Activate();
    }

    public void BringToFront()
    {
        ThrowIfDisposed();
        if (Parent is not null)
            Parent.MoveChildToFront(this);
        else
            Session?.BringRootToFront(this);
    }

    public void Activate()
    {
        ThrowIfDisposed();
        if (_isClosed)
            return;

        if (Session is not null)
            DeactivateWindows(Session.Roots, this);

        BringToFront();
        if (_isActive)
            return;

        _isActive = true;
        Activated?.Invoke(this, EventArgs.Empty);
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public void Deactivate()
    {
        ThrowIfDisposed();
        DeactivateInternal();
    }

    public bool Close(UiWindowCloseReason reason = UiWindowCloseReason.Programmatic)
    {
        if (_isClosed)
            return true;

        ThrowIfDisposed();
        var closing = new UiWindowClosingEventArgs(reason);
        Closing?.Invoke(this, closing);
        if (closing.Cancel)
            return false;

        foreach (UiWindow ownedWindow in _ownedWindows.ToArray())
            ownedWindow.Close(UiWindowCloseReason.OwnerClosed);

        _isClosed = true;
        DeactivateInternal();
        Closed?.Invoke(this, new UiWindowClosedEventArgs(reason));
        Dispose();
        return true;
    }

    protected override bool OnInput(UiInputEvent input)
    {
        Activate();
        return base.OnInput(input);
    }

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Window,
            string.IsNullOrWhiteSpace(Title) ? GetType().Name : Title,
            Bounds,
            CreateSemanticState(),
            CreateChildSemanticNodes());

    protected override void OnChildRemoved(UiElement child)
    {
        if (child is UiWindow window && ReferenceEquals(window.Owner, this))
        {
            _ownedWindows.Remove(window);
            window.Owner = null;
            window.Kind = UiWindowKind.TopLevel;
            window.Placement = BRect.Empty;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isClosed)
        {
            _isClosed = true;
            foreach (UiWindow ownedWindow in _ownedWindows.ToArray())
                ownedWindow.Close(UiWindowCloseReason.OwnerClosed);
            DeactivateInternal();
        }

        base.Dispose(disposing);
    }

    private UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (IsActive)
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

    private void DeactivateInternal()
    {
        if (!_isActive)
            return;

        _isActive = false;
        Deactivated?.Invoke(this, EventArgs.Empty);
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private static void DeactivateWindows(IEnumerable<UiElement> elements, UiWindow except)
    {
        foreach (UiElement element in elements)
        {
            if (ReferenceEquals(element, except))
                continue;
            if (element is UiWindow window)
                window.DeactivateInternal();

            DeactivateWindows(element.Children, except);
        }
    }
}
