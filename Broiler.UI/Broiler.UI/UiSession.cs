using System;
using System.Collections.Generic;
using Broiler.Graphics;

namespace Broiler.UI;

public sealed class UiSession : IDisposable
{
    private readonly List<UiElement> _roots = [];
    private readonly List<UiInvalidation> _invalidations = [];
    private readonly List<UiElement> _modalElements = [];
    private bool _isDisposed;

    public UiSession(IUiHost host, IUiDispatcher dispatcher, IUiClock clock, UiFactorySet? factories = null)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Factories = factories ?? new UiFactorySet([]);
    }

    public IUiHost Host { get; }

    public IUiDispatcher Dispatcher { get; }

    public IUiClock Clock { get; }

    public UiFactorySet Factories { get; }

    public IReadOnlyList<UiElement> Roots => _roots;

    public IReadOnlyList<UiInvalidation> Invalidations => _invalidations;

    public UiElement? FocusedElement { get; private set; }

    public UiElement? CapturedElement { get; private set; }

    public IReadOnlyList<UiElement> ModalElements => _modalElements;

    public UiElement? ModalElement => _modalElements.Count == 0 ? null : _modalElements[^1];

    public bool IsDisposed => _isDisposed;

    public void AddRoot(UiElement root)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(root);
        if (root.Parent is not null)
            throw new InvalidOperationException("Root elements cannot already have a parent.");
        if (root.Session is not null)
            throw new InvalidOperationException("Root elements cannot already be attached to a session.");

        _roots.Add(root);
        root.AttachToSession(this);
        Invalidate(root, UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public bool RemoveRoot(UiElement root)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(root);

        if (!_roots.Remove(root))
            return false;

        if (FocusedElement is not null && (ReferenceEquals(FocusedElement, root) || FocusedElement.IsDescendantOf(root)))
            FocusedElement = null;
        if (CapturedElement is not null && (ReferenceEquals(CapturedElement, root) || CapturedElement.IsDescendantOf(root)))
            CapturedElement = null;
        RemoveModalElements(root);

        root.DetachFromSession();
        Invalidate(root, UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    public bool BringRootToFront(UiElement root) => MoveRoot(root, _roots.Count - 1);

    public bool SendRootToBack(UiElement root) => MoveRoot(root, 0);

    public bool MoveRoot(UiElement root, int newIndex)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(root);
        if ((uint)newIndex >= (uint)_roots.Count)
            throw new ArgumentOutOfRangeException(nameof(newIndex));

        int oldIndex = _roots.IndexOf(root);
        if (oldIndex < 0)
            return false;
        if (oldIndex == newIndex)
            return false;

        _roots.RemoveAt(oldIndex);
        _roots.Insert(newIndex, root);
        Invalidate(root, UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    public void SetFocus(UiElement? element)
    {
        ThrowIfDisposed();
        if (element is not null && element.Session != this)
            throw new InvalidOperationException("Focused elements must belong to this session.");

        FocusedElement = element;
        if (element is not null)
            Invalidate(element, UiInvalidationKind.Semantic | UiInvalidationKind.Render);
    }

    public void CaptureInput(UiElement element)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(element);
        if (element.Session != this)
            throw new InvalidOperationException("Captured elements must belong to this session.");

        CapturedElement = element;
    }

    public void ReleaseInputCapture(UiElement element)
    {
        ThrowIfDisposed();
        if (ReferenceEquals(CapturedElement, element))
            CapturedElement = null;
    }

    public void PushModalElement(UiElement element)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(element);
        if (element.Session != this)
            throw new InvalidOperationException("Modal elements must belong to this session.");
        if (_modalElements.Contains(element))
            throw new InvalidOperationException("The modal element is already in the session modal stack.");

        _modalElements.Add(element);
    }

    public void PopModalElement(UiElement element)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(element);
        int index = _modalElements.LastIndexOf(element);
        if (index >= 0)
            _modalElements.RemoveAt(index);
    }

    public void Invalidate(UiElement element, UiInvalidationKind kind)
    {
        if (_isDisposed || kind == UiInvalidationKind.None)
            return;

        var invalidation = new UiInvalidation(element, kind);
        _invalidations.Add(invalidation);
        Host.Invalidate(invalidation);
    }

    public BRenderList RenderFrame()
    {
        ThrowIfDisposed();
        int initialInvalidationCount = _invalidations.Count;
        BRenderList renderList = Host.CreateRenderList();
        var context = new UiRenderContext(renderList, this, Host);

        foreach (UiElement root in _roots)
        {
            root.Measure(Host.ViewportSize);
            root.Arrange(new BRect(0, 0, Host.ViewportSize.Width, Host.ViewportSize.Height));
            root.Render(context);
        }
        context.FlushDeferred();

        renderList.Validate();
        Host.Present(renderList);
        if (initialInvalidationCount > 0)
            _invalidations.RemoveRange(0, Math.Min(initialInvalidationCount, _invalidations.Count));
        return renderList;
    }

    public bool DispatchInput(UiInputEvent input)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(input);

        UiElement? target = ResolveDispatchTarget(input);
        if (target is null)
            return false;

        UiElement? current = target;
        while (current is not null)
        {
            if (current.DispatchInput(input))
                return true;

            current = current.Parent;
        }

        return false;
    }

    private UiElement? ResolveDispatchTarget(UiInputEvent input)
    {
        UiElement? modal = ModalElement;
        if (modal is null)
            return CapturedElement ?? ResolveInputTarget(input);

        if (CapturedElement is not null && IsWithinSubtree(CapturedElement, modal))
            return CapturedElement;

        if (input.Kind is UiInputEventKind.KeyboardKey or UiInputEventKind.TextInput or UiInputEventKind.TextComposition)
            return FocusedElement is not null && IsWithinSubtree(FocusedElement, modal) ? FocusedElement : modal;

        UiElement? hit = HitTest(input.Position);
        return hit is not null && IsWithinSubtree(hit, modal) ? hit : modal;
    }

    private UiElement? ResolveInputTarget(UiInputEvent input) =>
        input.Kind is UiInputEventKind.KeyboardKey or UiInputEventKind.TextInput or UiInputEventKind.TextComposition
            ? FocusedElement ?? HitTest(input.Position)
            : HitTest(input.Position) ?? FocusedElement;

    public UiElement? HitTest(BPoint point)
    {
        ThrowIfDisposed();
        for (int index = _roots.Count - 1; index >= 0; index--)
        {
            UiElement root = _roots[index];
            if (root.Visibility == UiVisibility.Visible && root.Bounds.Contains(point))
                return HitTest(root, point) ?? root;
        }

        return null;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        foreach (UiElement root in _roots.ToArray())
            root.Dispose();
        _roots.Clear();
        _invalidations.Clear();
        FocusedElement = null;
        CapturedElement = null;
        _modalElements.Clear();
        _isDisposed = true;
    }

    private static UiElement? HitTest(UiElement element, BPoint point)
    {
        for (int index = element.Children.Count - 1; index >= 0; index--)
        {
            UiElement child = element.Children[index];
            if (child.Visibility == UiVisibility.Visible && child.Bounds.Contains(point))
                return HitTest(child, point) ?? child;
        }

        return null;
    }

    private void RemoveModalElements(UiElement root)
    {
        for (int index = _modalElements.Count - 1; index >= 0; index--)
        {
            UiElement modal = _modalElements[index];
            if (ReferenceEquals(modal, root) || modal.IsDescendantOf(root))
                _modalElements.RemoveAt(index);
        }
    }

    private static bool IsWithinSubtree(UiElement element, UiElement root) =>
        ReferenceEquals(element, root) || element.IsDescendantOf(root);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);
}
