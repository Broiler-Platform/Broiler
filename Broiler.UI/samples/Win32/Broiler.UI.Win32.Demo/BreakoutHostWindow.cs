using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Broiler.Graphics;
using Broiler.Graphics.Windows;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.UI.Standard;

namespace Broiler.UI.Win32.Demo;

/// <summary>
/// A real second native window that hosts a broken-out Broiler.UI subwindow. It is a
/// <see cref="Direct2DWindow"/> that does not own the thread message loop (it is serviced by the
/// main window's loop) and exposes the neutral <see cref="IUiHostWindow"/> contract so a
/// <see cref="UiSession"/> can render into it and receive its input.
/// </summary>
[SupportedOSPlatform("windows7.0")]
internal sealed class BreakoutHostWindow : Direct2DWindow, IUiHostWindow, IUiClipboardHost, IUiTextInputHost
{
    private UiSession? _session;
    private string _clipboard = string.Empty;
    private UiTextCaretInfo? _caret;

#pragma warning disable CS0618
    private readonly StandardLegacyGraphicsInputAdapter _legacyInput = new("broiler-ui-breakout-window");
#pragma warning restore CS0618

    public BreakoutHostWindow(UiHostWindowRequest request)
        : base(new BWindowOptions
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Broiler.UI" : request.Title,
            ClientWidth = ToClientExtent(request.Placement.Width, 480),
            ClientHeight = ToClientExtent(request.Placement.Height, 320),
            ClearColor = StandardControlPaint.Theme.SurfaceAlt,
            RenderOptions = new BRenderOptions(Antialias: true, VSync: true, SubpixelText: true),
            OwnsMessageLoop = false,
        })
    {
    }

    // IUiHost
    BSize IUiHost.ViewportSize => ClientSize;

    double IUiHost.Scale => DpiScale;

    BRenderList IUiHost.CreateRenderList(int capacity) => new(capacity);

    void IUiHost.Invalidate(UiInvalidation invalidation) => Invalidate();

    void IUiHost.Present(BRenderList renderList)
    {
        // The base Direct2DWindow renders the list returned from BuildRenderList; nothing else to do.
    }

    // IUiHostWindow
    public void Bind(UiSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Invalidate();
    }

    public void Activate()
    {
        if (NativeHandle != IntPtr.Zero)
            SetForegroundWindow(NativeHandle);
        Invalidate();
    }

    void IUiHostWindow.SetTitle(string title) => SetTitle(title);

    // IUiClipboardHost / IUiTextInputHost mirror the main window's DemoUiHost so hosted controls
    // keep clipboard and caret behavior.
    public bool TryGetText(out string text)
    {
        text = _clipboard;
        return true;
    }

    public void SetText(string text) => _clipboard = text ?? string.Empty;

    public void PublishCaret(UiTextCaretInfo caret) => _caret = caret;

    public void ClearCaret(UiElement owner)
    {
        if (_caret?.Owner == owner)
            _caret = null;
    }

    protected override BRenderList? BuildRenderList(BSize clientSize) => _session?.RenderFrame();

    protected override void OnPointerDown(BPointerEventArgs e) => Dispatch(_legacyInput.FromPointerButton(e));

    protected override void OnPointerMove(BPointerEventArgs e) => Dispatch(_legacyInput.FromPointerMove(e));

    protected override void OnPointerUp(BPointerEventArgs e) => Dispatch(_legacyInput.FromPointerButton(e));

    protected override void OnMouseWheel(BMouseWheelEventArgs e) => Dispatch(_legacyInput.FromMouseWheel(e));

    protected override void OnKeyDown(BKeyEventArgs e) => Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Down));

    protected override void OnKeyUp(BKeyEventArgs e) => Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Up));

    protected override void OnTextInput(BTextInputEventArgs e) => Dispatch(_legacyInput.FromText(e));

    protected override void Dispose(bool disposing)
    {
        // Destroy the native window when the framework disposes this host window.
        if (disposing && !IsDisposed)
            Close();

        base.Dispose(disposing);
    }

    private void Dispatch(UiInputEvent input)
    {
        if (_session is not null && !_session.IsDisposed && _session.DispatchInput(input))
            Invalidate();
    }

    private static int ToClientExtent(double requested, int fallback) =>
        requested > 1 ? (int)Math.Round(requested) : fallback;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
}
