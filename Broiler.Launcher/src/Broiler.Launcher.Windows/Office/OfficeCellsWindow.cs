#if BROILER_OFFICE_ALL_IN_ONE
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Broiler.Cells.Core;
using Broiler.Cells.UI;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Imaging;
using Broiler.Graphics.Rendering;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Resources;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Graphics.Windows;
using Broiler.Hosting.Windows;
using Broiler.Hosting.Windows.Accessibility;
using Broiler.Hosting.Windows.Input;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI;
using Broiler.UI.Standard;
using Broiler.UI.Tooltip.Standard;
using Broiler.UI.Window;
using Broiler.UI.Window.Standard;

namespace Broiler.Launcher.Windows.Office;

/// <summary>
/// Win32/Direct2D host for Broiler Cells inside the all-in-one Broiler Office executable.
/// </summary>
[SupportedOSPlatform("windows7.0")]
internal sealed class OfficeCellsWindow : Direct2DWindow, IUiHost, IUiCursorHost, IUiImageHost, IUiWindowHost, IUiClipboardHost, IOfficeUiThreadTarget
{
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint LoadCursorW(nint hInstance, nint lpCursorName);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint SetCursor(nint hCursor);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern short GetKeyState(int virtualKey);

    private nint _activeCursorHandle;
    private readonly List<OfficeHostWindow> _dialogWindows = [];
    private WindowsClipboard? _clipboard;
    private const int TooltipTickMilliseconds = 33;
    private readonly StandardTooltip _tooltip;
    private readonly StandardTooltipController _tooltips;
    private bool _isTicking;
    private readonly UiSession _session;
    private readonly CellsView _view;
    private readonly StandardWindow _rootWindow;
    private readonly StandardQueuedUiDispatcher _dispatcher;
    private WindowsInputBridge? _inputBridge;
    private WindowsAutomationBridge? _automation;

#pragma warning disable CS0618
    private readonly StandardLegacyGraphicsInputAdapter _input = new("broiler-cells");
#pragma warning restore CS0618

    public OfficeCellsWindow(bool ownsMessageLoop = true) : base(new BWindowOptions
    {
        Title = "Broiler Cells",
        ClientWidth = 1000,
        ClientHeight = 600,
        ClearColor = CellsView.Canvas,
        RenderOptions = new BRenderOptions(Antialias: true, VSync: true, SubpixelText: true),
        OwnsMessageLoop = ownsMessageLoop,
    })
    {
        if (!ownsMessageLoop)
            CloseRequested += (_, _) => Close();

        _dispatcher = new StandardQueuedUiDispatcher(() => PostToUiThread(DrainDispatcher));
        _session = new StandardUiSessionBuilder().WithDispatcher(_dispatcher).Build(this);
        _view = new CellsView(new SheetModel(initialValue: (row, column) => (row, column) switch
        {
            (0, 0) => "Item",
            (0, 1) => "Quantity",
            (1, 0) => "Paper",
            (1, 1) => "12",
            (2, 0) => "Pens",
            (2, 1) => "4",
            (4, 0) => "Long text stays inside this cell",
            (9, 9) => "Last cell",
            _ => string.Empty,
        }), printHost: new WindowsPrintHost());

        _rootWindow = new StandardWindow
        {
            Chrome = UiWindowChrome.None,
            BorderThickness = 0,
            Background = CellsView.Canvas,
        };
        _tooltip = new StandardTooltip
        {
            Font = new BFontStyle("Segoe UI", 12),
            Background = CellsPalette.MenuPopup,
            Foreground = CellsPalette.Title,
            BorderColor = CellsPalette.WindowBorder,
        };
        _tooltips = new StandardTooltipController(_tooltip);

        _rootWindow.AddChild(_view);
        _view.OwnerWindow = _rootWindow;
        _view.ViewportProvider = () => ClientSize;
        _view.ExitRequested += (_, _) =>
        {
            if (!PostToUiThread(Close))
                Close();
        };
        _view.TitleChanged += (_, newTitle) => SetTitle(newTitle);

        _session.AddRoot(_rootWindow);
        _rootWindow.OpenOwnedWindow(_tooltip, new BRect(0, 0, 1, 1));
        _tooltip.Deactivate();

        StandardThemeController.Apply(_session, CellsView.Theme);
        _view.CellControl.SelectCell(new CellPosition(1, 1));
    }

    private void SyncTicking()
    {
        bool wanted = _tooltips.IsWaiting;
        if (wanted == _isTicking)
            return;

        _isTicking = wanted;
        if (wanted)
            StartAnimationTimer(TooltipTickMilliseconds);
        else
            StopAnimationTimer();
    }

    protected override void OnAnimationTick()
    {
        if (_tooltips.Tick())
            Invalidate();
        SyncTicking();
    }

    BSize IUiHost.ViewportSize => ClientSize;
    double IUiHost.Scale => DpiScale;
    BRenderList IUiHost.CreateRenderList(int capacity) => new(capacity);
    void IUiHost.Invalidate(UiInvalidation invalidation) => Invalidate();
    void IUiHost.Present(BRenderList renderList) { }
    bool IOfficeUiThreadTarget.PostUiAction(Action callback) => PostToUiThread(callback);

    public IUiHostWindow CreateHostWindow(UiHostWindowRequest request)
    {
        var window = new OfficeHostWindow(
            OfficeHostWindow.ToScreenPlacement(this, DpiScale, request),
            this,
            CellsView.Canvas,
            "Broiler Cells",
            () => _clipboard is not null && _clipboard.TryGetText(out string text) ? text : null,
            text => _clipboard?.SetText(text));
        _dialogWindows.Add(window);
        window.Closed += (_, _) => _dialogWindows.Remove(window);
        window.Show();
        return window;
    }

    bool IUiClipboardHost.TryGetText(out string text)
    {
        text = string.Empty;
        return _clipboard is not null && _clipboard.TryGetText(out text);
    }

    void IUiClipboardHost.SetText(string text) => _clipboard?.SetText(text);

    BImageHandle IUiImageHost.CreateImage(ReadOnlySpan<byte> encodedImage)
    {
        try { return Renderer?.CreateImage(encodedImage) ?? BImageHandle.Invalid; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return BImageHandle.Invalid; }
    }

    BImageHandle IUiImageHost.CreateImage(BPixelBuffer pixels)
    {
        try { return Renderer?.CreateImage(pixels) ?? BImageHandle.Invalid; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return BImageHandle.Invalid; }
    }

    void IUiImageHost.ReleaseImage(BImageHandle image)
    {
        if (image.IsValid)
            Renderer?.ReleaseImage(image);
    }

    protected override void OnCreated()
    {
        _clipboard = new WindowsClipboard(NativeHandle);
        _inputBridge = new WindowsInputBridge(NativeHandle, RenderNativeHandle, _session,
            scaleProvider: () => DpiScale, invalidateAction: Invalidate);
        _automation = new WindowsAutomationBridge(RenderNativeHandle, _session, _rootWindow, () => DpiScale);
    }

    protected override BRenderList? BuildRenderList(BSize clientSize)
    {
        if (_session.IsDisposed)
            return null;
        DrainDispatcher();
        return _session.RenderFrame();
    }

    private void DrainDispatcher()
    {
        try { _dispatcher.Drain(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    protected override void OnResized(BSize clientSize, double dpiScale) => Invalidate();
    protected override void OnPointerDown(BPointerEventArgs e) => DispatchPointerButton(e);
    protected override void OnPointerUp(BPointerEventArgs e) => DispatchPointerButton(e);
    protected override void OnPointerMove(BPointerEventArgs e) => Dispatch(_input.FromPointerMove(e));
    protected override void OnMouseWheel(BMouseWheelEventArgs e) => Dispatch(_input.FromMouseWheel(e));
    protected override void OnTextInput(BTextInputEventArgs e) => Dispatch(_input.FromText(e));
    protected override void OnKeyDown(BKeyEventArgs e) => Dispatch(_input.FromKey(e, KeyboardKeyTransition.Down));
    protected override void OnKeyUp(BKeyEventArgs e) => Dispatch(_input.FromKey(e, KeyboardKeyTransition.Up));

    private void DispatchPointerButton(BPointerEventArgs e)
    {
        var input = _input.FromPointerButton(e);
        InputModifiers modifiers = InputModifiers.None;
        if (GetKeyState(0x11) < 0) modifiers |= InputModifiers.Control;
        if (GetKeyState(0x10) < 0) modifiers |= InputModifiers.Shift;
        if (GetKeyState(0x12) < 0) modifiers |= InputModifiers.Alt;

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
        if (_session.IsDisposed)
            return;

        bool handled = _session.DispatchInput(input);
        bool changed = input.Kind == UiInputEventKind.PointerMove
            ? _tooltips.PointerMoved(input.Position)
            : _tooltips.Dismiss();

        SyncTicking();
        if ((handled || changed) && !IsDisposed && NativeHandle != 0)
            Invalidate();
    }

    public void SetCursor(UiCursorShape shape)
    {
        nint cursor = shape switch
        {
            UiCursorShape.ResizeHorizontal => LoadCursorW(0, 32644),
            UiCursorShape.ResizeVertical => LoadCursorW(0, 32645),
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
        if (message == 0x0020 && _activeCursorHandle != 0)
            SetCursor(_activeCursorHandle);

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
            foreach (var window in _dialogWindows.ToArray())
                window.Dispose();
            _dialogWindows.Clear();
            _automation?.Dispose();
            _inputBridge?.Dispose();
            _session.Dispose();
        }
        base.Dispose(disposing);
    }
}
#endif
