using System;
using System.Runtime.Versioning;
using Broiler.App;
using Broiler.Code.Core.Hosting;
using Broiler.Code.Core.Shell;
using Broiler.Graphics;
using Broiler.Graphics.Windows;
using Broiler.UI;
using Broiler.UI.CodeEditor.Standard;

namespace Broiler.Code.Windows;

/// <summary>
/// The Win32/Direct2D host for Broiler Code.
///
/// It owns the window, the four platform services, and nothing else. Shell and
/// workspace behaviour is Broiler.Code.Core's, and the architecture test
/// rejects a copy of either appearing in this assembly.
/// </summary>
[SupportedOSPlatform("windows7.0")]
internal sealed class CodeWindow : Direct2DWindow
{
    private readonly DesktopInputRouter _input = new("broiler-code-windows");
    private readonly UiThreadDispatcher _dispatcher;
    private WindowsClipboard? _clipboard;
    private WindowsTextInputService? _textInput;
    private StandardCodeEditor? _editor;
    private UiSession? _session;
    private CodeShell? _shell;

    public CodeWindow()
        : base(new BWindowOptions
        {
            Title = "Broiler Code",
            ClientWidth = 1280,
            ClientHeight = 840,
            ClearColor = new BColor(0xFF, 0xFF, 0xFF),
            RenderOptions = new BRenderOptions(Antialias: true, VSync: true, SubpixelText: true),
        })
    {
        // The dispatcher is constructed on the thread that will run the message
        // loop, and wakes it by invalidating. Constructing it anywhere else
        // would capture the wrong thread as the UI thread and every
        // CheckAccess would answer for a thread that never pumps.
        _dispatcher = new UiThreadDispatcher(Invalidate);
    }

    protected override BRenderList? BuildRenderList(BSize clientSize)
    {
        // Queued UI work runs before the frame is built, so a classification
        // that landed since the last frame is visible in this one rather than
        // the next.
        _dispatcher.Drain();
        return _session?.RenderFrame();
    }

    protected override void OnResized(BSize clientSize, double dpiScale) => Invalidate();

    protected override void OnPointerDown(BPointerEventArgs e)
    {
        // Focus follows the click, before the event is dispatched, so the press
        // that moves the caret is handled by the control that now has focus.
        // Without this the editor keeps focus forever once it has it, and the
        // tree never gets a key.
        if (_session is not null && _shell is not null)
        {
            UiElement? target = _shell.ResolveFocusTarget(
                _session.HitTest(new BPoint(e.Position.X, e.Position.Y)));
            if (target is not null)
                SetFocus(target);
        }

        Dispatch(_input.FromPointerButton(e, pressed: true));
    }

    /// <summary>
    /// Moves session focus, and tells the editor whether it has it. The session
    /// decides where keys go; the control only draws the caret, so both have to
    /// be told or the caret and the keystrokes disagree.
    /// </summary>
    private void SetFocus(UiElement target)
    {
        if (_session is null || ReferenceEquals(_session.FocusedElement, target))
            return;

        _session.SetFocus(target);
        if (_editor is not null)
            _editor.HasFocus = ReferenceEquals(target, _editor);
    }

    /// <summary>Gives the editor keyboard focus, for the first frame.</summary>
    public void FocusEditor()
    {
        if (_editor is not null)
            SetFocus(_editor);
    }

    /// <summary>The shell, for focus routing. It owns which pane a hit belongs to.</summary>
    public void AttachShell(CodeShell shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        _shell = shell;
    }

    protected override void OnPointerMove(BPointerEventArgs e) =>
        Dispatch(_input.FromPointerMove(e));

    protected override void OnPointerUp(BPointerEventArgs e) =>
        Dispatch(_input.FromPointerButton(e, pressed: false));

    protected override void OnMouseWheel(BMouseWheelEventArgs e) =>
        Dispatch(_input.FromWheel(e));

    protected override void OnKeyDown(BKeyEventArgs e) =>
        Dispatch(_input.FromKey(e, pressed: true));

    protected override void OnKeyUp(BKeyEventArgs e) =>
        Dispatch(_input.FromKey(e, pressed: false));

    protected override void OnTextInput(BTextInputEventArgs e)
    {
        // While an IME composition is active the composition path owns the
        // text. Letting WM_CHAR through as well would insert every candidate
        // character twice.
        if (_textInput is not null && _composing)
            return;
        Dispatch(_input.FromText(e.Character.ToString()));
    }

    private bool _composing;

    protected override void OnNativeWindowMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        base.OnNativeWindowMessage(hwnd, message, wParam, lParam);
        _textInput?.TryHandleMessage(message, wParam, lParam);
    }

    /// <summary>
    /// Creates the platform services once the window exists. They all need the
    /// HWND, which is not available until then.
    /// </summary>
    public void AttachServices(UiSession session, StandardCodeEditor editor)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(editor);

        _session = session;
        _editor = editor;
        _clipboard = new WindowsClipboard(NativeHandle);
        _textInput = new WindowsTextInputService(NativeHandle);
        _textInput.CompositionChanged += OnCompositionChanged;
    }

    public IUiClipboardHost? Clipboard => _clipboard;

    public UiThreadDispatcher Dispatcher => _dispatcher;

    private void OnCompositionChanged(string? text, bool committed)
    {
        _composing = text is not null && !committed;
        Dispatch(_input.FromComposition(text, committed));

        // The candidate window follows the caret. Done after the editor has
        // seen the composition, so the rectangle reflects where the text now
        // is rather than where it was.
        if (_editor is not null)
            _textInput?.SetCaretRectangle(_editor.GetCaretRect());
    }

    private void Dispatch(UiInputEvent input)
    {
        _session?.DispatchInput(input);

        // Anything the UI queued while handling the event runs before the next
        // frame, on this thread.
        _dispatcher.Drain();
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _textInput is not null)
        {
            _textInput.CompositionChanged -= OnCompositionChanged;
            _textInput.Dispose();
        }

        base.Dispose(disposing);
    }
}
