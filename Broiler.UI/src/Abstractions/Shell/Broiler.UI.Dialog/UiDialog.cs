using System;
using System.Threading.Tasks;
using Broiler.Graphics;
using Broiler.Input.Mouse;
using Broiler.UI.Window;

namespace Broiler.UI.Dialog;

public abstract class UiDialog : UiWindow
{
    private TaskCompletionSource<UiDialogResult>? _resultSource;
    private UiDialogResult? _pendingResult;
    private UiDialogResult _completedResult = UiDialogResult.None;
    private UiElement? _restoreFocusElement;
    private UiSession? _presentationSession;
    private BPoint _moveStartPointer;
    private BRect _moveStartPlacement;
    private bool _isMoving;
    private bool _isPresented;
    private bool _isResultCompleted;

    protected UiDialog()
    {
        Closed += HandleClosed;
    }

    public event EventHandler<UiDialogResultEventArgs>? ResultCompleted;

    public UiDialogPresentationMode PresentationMode { get; private set; } = UiDialogPresentationMode.Modeless;

    public bool IsModal => PresentationMode == UiDialogPresentationMode.Modal && IsPresented;

    public bool IsPresented => _isPresented && !IsClosed && !IsDisposed;

    public bool IsResultCompleted => _isResultCompleted;

    public UiDialogResult CompletedResult => _completedResult;

    public Task<UiDialogResult> ResultTask => _resultSource?.Task ?? Task.FromResult(_completedResult);

    public Task<UiDialogResult> ShowModeless(UiWindow owner, BRect placement = default) =>
        Show(owner, placement, UiDialogPresentationMode.Modeless);

    public Task<UiDialogResult> ShowModal(UiWindow owner, BRect placement = default) =>
        Show(owner, placement, UiDialogPresentationMode.Modal);

    public bool Accept(string? value = null) => Complete(UiDialogResult.Accepted(value));

    public bool Reject(string? value = null) => Complete(UiDialogResult.Rejected(value));

    public bool Cancel() => Complete(UiDialogResult.Cancelled);

    public bool Complete(UiDialogResult result)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(result);
        if (_isResultCompleted)
            return false;

        _pendingResult = result;
        bool closed = Close(UiWindowCloseReason.Programmatic);
        if (!closed)
            _pendingResult = null;

        return closed;
    }

    protected override UiSemanticNode GetSemanticNodeCore()
    {
        UiSemanticNode node = base.GetSemanticNodeCore();
        return node with
        {
            Role = UiSemanticRole.Dialog,
            State = IsModal ? node.State | UiSemanticState.Modal : node.State,
        };
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (HandleMoveInput(input))
            return true;

        return base.OnInput(input);
    }

    protected virtual bool HitTestMoveGrip(BPoint position) => false;

    protected override void OnDetached()
    {
        if (!_isResultCompleted)
            FinishPresentation(UiDialogResult.Closed(UiWindowCloseReason.Programmatic));

        base.OnDetached();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isResultCompleted)
            FinishPresentation(UiDialogResult.Closed(UiWindowCloseReason.SessionDisposed));

        base.Dispose(disposing);
    }

    private Task<UiDialogResult> Show(UiWindow owner, BRect placement, UiDialogPresentationMode mode)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.IsClosed)
            throw new InvalidOperationException("Dialogs cannot be presented by a closed owner.");
        if (_isPresented || Parent is not null || Owner is not null || Session is not null)
            throw new InvalidOperationException("A dialog can only be presented once while unattached.");
        if (_isResultCompleted)
            throw new InvalidOperationException("A completed dialog cannot be presented again.");

        _resultSource = new TaskCompletionSource<UiDialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        PresentationMode = mode;
        _isPresented = true;
        _restoreFocusElement = owner.Session?.FocusedElement;
        owner.OpenOwnedWindow(this, placement, UiWindowKind.Dialog);
        _presentationSession = Session;
        if (Session is not null)
        {
            Session.SetFocus(this);
            if (mode == UiDialogPresentationMode.Modal)
                Session.PushModalElement(this);
        }

        return _resultSource.Task;
    }

    private bool HandleMoveInput(UiInputEvent input)
    {
        if (input.Kind == UiInputEventKind.PointerMove)
        {
            if (!_isMoving)
                return false;

            MoveTo(input.Position);
            return true;
        }

        if (input.Kind != UiInputEventKind.PointerButton || input.MouseButton != MouseButton.Left)
            return false;

        if (input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            if (!HitTestMoveGrip(input.Position))
                return false;

            BeginMove(input.Position);
            return true;
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up && _isMoving)
        {
            EndMove();
            return true;
        }

        return false;
    }

    private void BeginMove(BPoint pointer)
    {
        Activate();
        Session?.SetFocus(this);
        Session?.CaptureInput(this);
        _isMoving = true;
        _moveStartPointer = pointer;
        _moveStartPlacement = ResolveCurrentPlacement();
    }

    private void MoveTo(BPoint pointer)
    {
        double dx = pointer.X - _moveStartPointer.X;
        double dy = pointer.Y - _moveStartPointer.Y;
        SetPlacement(CoerceMovePlacement(new BRect(
            _moveStartPlacement.X + dx,
            _moveStartPlacement.Y + dy,
            _moveStartPlacement.Width,
            _moveStartPlacement.Height)));
    }

    private void EndMove()
    {
        _isMoving = false;
        if (Session?.CapturedElement == this)
            Session.ReleaseInputCapture(this);
    }

    private BRect ResolveCurrentPlacement()
    {
        if (!Placement.IsEmpty)
            return Placement;

        if (Owner is not null && !Bounds.IsEmpty)
        {
            return new BRect(
                Bounds.Left - Owner.Bounds.Left,
                Bounds.Top - Owner.Bounds.Top,
                Bounds.Width,
                Bounds.Height);
        }

        return Bounds;
    }

    private BRect CoerceMovePlacement(BRect placement)
    {
        if (Owner is null || Owner.Bounds.IsEmpty || placement.IsEmpty)
            return placement;

        double maxX = Math.Max(0, Owner.Bounds.Width - placement.Width);
        double maxY = Math.Max(0, Owner.Bounds.Height - placement.Height);
        return new BRect(
            Math.Clamp(placement.X, 0, maxX),
            Math.Clamp(placement.Y, 0, maxY),
            placement.Width,
            placement.Height);
    }

    private void HandleClosed(object? sender, UiWindowClosedEventArgs e)
    {
        UiDialogResult result = _pendingResult ?? UiDialogResult.Closed(e.Reason);
        _pendingResult = null;
        FinishPresentation(result);
    }

    private void FinishPresentation(UiDialogResult result)
    {
        if (_isResultCompleted)
            return;

        _isMoving = false;
        _isPresented = false;
        _isResultCompleted = true;
        _completedResult = result;

        UiSession? session = Session ?? _presentationSession;
        if (session is not null && !session.IsDisposed)
        {
            session.PopModalElement(this);
            if (session.CapturedElement is not null && (ReferenceEquals(session.CapturedElement, this) || session.CapturedElement.IsDescendantOf(this)))
                session.ReleaseInputCapture(session.CapturedElement);

            if (_restoreFocusElement is not null && _restoreFocusElement.Session == session && !ReferenceEquals(_restoreFocusElement, this))
                session.SetFocus(_restoreFocusElement);
            else if (ReferenceEquals(session.FocusedElement, this))
                session.SetFocus(null);
        }

        _resultSource?.TrySetResult(result);
        ResultCompleted?.Invoke(this, new UiDialogResultEventArgs(result));
    }
}
