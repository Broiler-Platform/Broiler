using System;
using System.Runtime.InteropServices;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Rendering;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Windowing;
using Broiler.Graphics.Windows;
using Broiler.Hosting.Windows;
using Broiler.Hosting.Windows.Accessibility;
using Broiler.Hosting.Windows.Input;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Launcher.UI;
using Broiler.Launcher.Installation;
using Broiler.UI;
using Broiler.UI.Standard;
using Broiler.UI.Window;
using Broiler.UI.Window.Standard;

namespace Broiler.Launcher.Windows;

/// <summary>
/// Direct2D native window host for Broiler Launcher on Windows.
/// Dispatches graphics, pointer, and keyboard input events from Direct2D into the Broiler.UI session.
/// </summary>
internal sealed class LauncherWindow : Direct2DWindow, IUiHost, IUiCursorHost
{
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint LoadCursorW(nint hInstance, nint lpCursorName);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint SetCursor(nint hCursor);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern short GetKeyState(int virtualKey);

    private nint _activeCursorHandle;
    private readonly UiSession _session;
    private readonly LauncherShellView _shell;
    private readonly StandardWindow _rootWindow;
    private readonly StandardQueuedUiDispatcher _dispatcher;
    private WindowsInputBridge? _inputBridge;
    private WindowsAutomationBridge? _automation;

#pragma warning disable CS0618
    private readonly StandardLegacyGraphicsInputAdapter _input = new("broiler-launcher");
#pragma warning restore CS0618

    public LauncherWindow() : base(new BWindowOptions
    {
        Title = OfficeBuild.IsDemo ? "Broiler Office Demo (unsigned)" : "Broiler Launcher",
        ClientWidth = 840,
        ClientHeight = 560,
        MinClientWidth = 640,
        MinClientHeight = 480,
        ClearColor = LauncherView.CanvasColor,
        RenderOptions = new BRenderOptions(Antialias: true, VSync: true, SubpixelText: true),
    })
    {
        _dispatcher = new StandardQueuedUiDispatcher(() => PostToUiThread(DrainDispatcher));
        _session = new StandardUiSessionBuilder().WithDispatcher(_dispatcher).Build(this);

        _rootWindow = new StandardWindow
        {
            Chrome = UiWindowChrome.None,
            BorderThickness = 0,
            Background = LauncherView.CanvasColor,
        };

        _shell = new LauncherShellView(ownerWindow: _rootWindow,
            installationService: ProductInstallationService.CreateDefault(new WindowsMachineInstallBroker()), machineUpdateBroker: new WindowsMachineInstallBroker());
        _rootWindow.AddChild(_shell);
        _shell.LauncherView.ExitRequested += (_, _) => Close();

        _session.AddRoot(_rootWindow);
        StandardThemeController.Apply(_session, LauncherView.Theme);
    }

    BSize IUiHost.ViewportSize => ClientSize;
    double IUiHost.Scale => DpiScale;
    BRenderList IUiHost.CreateRenderList(int capacity) => new(capacity);
    void IUiHost.Invalidate(UiInvalidation invalidation) => Invalidate();
    void IUiHost.Present(BRenderList renderList) { }

    protected override void OnCreated()
    {
        _inputBridge = new WindowsInputBridge(
            NativeHandle,
            RenderNativeHandle,
            _session,
            scaleProvider: () => DpiScale,
            invalidateAction: Invalidate);

        _automation = new WindowsAutomationBridge(RenderNativeHandle, _session, _rootWindow, () => DpiScale);
    }

    protected override BRenderList? BuildRenderList(BSize clientSize)
    {
        DrainDispatcher();
        return _session.RenderFrame();
    }

    private void DrainDispatcher()
    {
        try
        {
            _dispatcher.Drain();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    protected override void OnResized(BSize clientSize, double dpiScale) => Invalidate();

    protected override void OnPointerDown(BPointerEventArgs e) => DispatchPointerButton(e);

    protected override void OnPointerUp(BPointerEventArgs e) => DispatchPointerButton(e);

    protected override void OnPointerMove(BPointerEventArgs e)
    {
        Dispatch(_input.FromPointerMove(e));
        Invalidate();
    }

    protected override void OnMouseWheel(BMouseWheelEventArgs e) => Dispatch(_input.FromMouseWheel(e));

    protected override void OnTextInput(BTextInputEventArgs e) => Dispatch(_input.FromText(e));

    protected override void OnKeyDown(BKeyEventArgs e) => Dispatch(_input.FromKey(e, KeyboardKeyTransition.Down));

    protected override void OnKeyUp(BKeyEventArgs e) => Dispatch(_input.FromKey(e, KeyboardKeyTransition.Up));

    private void DispatchPointerButton(BPointerEventArgs e)
    {
        var input = _input.FromPointerButton(e);
        InputModifiers modifiers = InputModifiers.None;
        if (GetKeyState(0x11) < 0)
            modifiers |= InputModifiers.Control;
        if (GetKeyState(0x10) < 0)
            modifiers |= InputModifiers.Shift;
        if (GetKeyState(0x12) < 0)
            modifiers |= InputModifiers.Alt;

        MouseButtons buttons = MouseButtons.None;
        if (e.Buttons.HasFlag(BMouseButtons.Left)) buttons |= MouseButtons.Left;
        if (e.Buttons.HasFlag(BMouseButtons.Right)) buttons |= MouseButtons.Right;
        if (e.Buttons.HasFlag(BMouseButtons.Middle)) buttons |= MouseButtons.Middle;

        Dispatch(UiInputEvent.FromMouseButton(new MouseButtonEvent(
            input.Header,
            InputPoint.ClientDeviceIndependentPixels(e.Position.X, e.Position.Y),
            buttons,
            input.MouseButton,
            input.MouseButtonTransition!.Value,
            InputEventSource.Synthetic,
            modifiers)));
    }

    private void Dispatch(UiInputEvent input)
    {
        if (_session.DispatchInput(input))
            Invalidate();
    }

    public void SetCursor(UiCursorShape shape)
    {
        nint cursor = shape switch
        {
            UiCursorShape.Hand => LoadCursorW(0, 32649),
            UiCursorShape.Text => LoadCursorW(0, 32513),
            UiCursorShape.Wait => LoadCursorW(0, 32514),
            _ => LoadCursorW(0, 32512),
        };
        _activeCursorHandle = cursor;
        SetCursor(cursor);
    }

    protected override void OnNativeWindowMessage(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == 0x0020 /* WM_SETCURSOR */ && _activeCursorHandle != 0)
        {
            SetCursor(_activeCursorHandle);
        }

        base.OnNativeWindowMessage(hwnd, message, wParam, lParam);
        if (hwnd == NativeHandle)
        {
            WindowsWindowSizing.OnMessage(hwnd, message, lParam, DpiScale);
            _inputBridge?.OnTopLevelMessage(message, wParam, lParam);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _automation?.Dispose();
            _inputBridge?.Dispose();
            _session.Dispose();
        }
        base.Dispose(disposing);
    }
}
