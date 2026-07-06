using System;
using System.Threading.Tasks;
using Broiler.Graphics;
using Broiler.UI.Window;

namespace Broiler.UI.Dialog;

public abstract class UiDialog : UiWindow
{
    private TaskCompletionSource<UiDialogResult>? _resultSource;
    private UiDialogResult? _pendingResult;
    private UiDialogResult _completedResult = UiDialogResult.None;
    private UiElement? _restoreFocusElement;
    private UiSession? _presentationSession;
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
