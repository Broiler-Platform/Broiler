#if BROILER_OFFICE_ALL_IN_ONE
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Imaging;
using Broiler.Graphics.Rendering;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Resources;
using Broiler.Graphics.Windowing;
using Broiler.Graphics.Windows;
using Broiler.Hosting.Windows;
using Broiler.Hosting.Windows.Accessibility;
using Broiler.Hosting.Windows.Input;
using Broiler.Input.Keyboard;
using Broiler.Mail.Application;
using Broiler.Mail.Application.ViewModels;
using Broiler.Mail.Application.Views;
using Broiler.UI;
using Broiler.UI.Standard;

namespace Broiler.Launcher.Windows.Office;

/// <summary>
/// Win32/Direct2D host for Broiler Mail inside the all-in-one Broiler Office executable.
/// </summary>
[SupportedOSPlatform("windows7.0")]
internal sealed class OfficeMailWindow : Direct2DWindow, IUiHost, IUiClipboardHost, IUiTextInputHost, IUiWindowHost, IUiImageHost, IOfficeUiThreadTarget
{
    private readonly StandardQueuedUiDispatcher _dispatcher;
    private readonly UiSession _session;
    private readonly MailShellView _shell;
    private readonly MailKeyboardNavigation _keyboard;
    private readonly AppearanceController _appearance;
    private readonly List<OfficeHostWindow> _hostWindows = [];
    private WindowsClipboard? _clipboard;
    private WindowsAutomationBridge? _automationBridge;
    private WindowsInputBridge? _inputBridge;
    private bool _closePending;

#pragma warning disable CS0618
    private readonly StandardLegacyGraphicsInputAdapter _input = new("broiler-mail");
#pragma warning restore CS0618

    public OfficeMailWindow(MailApplication application, bool ownsMessageLoop = true)
        : base(new BWindowOptions
        {
            Title = "Broiler Mail",
            ClientWidth = 1120,
            ClientHeight = 760,
            ClearColor = StandardControlPaint.Theme.SurfaceAlt,
            RenderOptions = new BRenderOptions(Antialias: true, VSync: true, SubpixelText: true),
            OwnsMessageLoop = false,
        })
    {
        ArgumentNullException.ThrowIfNull(application);

        _dispatcher = new StandardQueuedUiDispatcher(() => PostToUiThread(DrainDispatcher));
        _session = new StandardUiSessionBuilder().WithDispatcher(_dispatcher).Build(this);
        MailShellViewModel model = application.CreateViewModel(_dispatcher);
        StandardControlPaint.ApplyTheme(AppearancePolicy.Resolve(model.Settings.Settings.Theme, UiSystemSettings.Default));
        _shell = new MailShellView(model);
        _shell.Attach(_session);
        _keyboard = _shell.CreateKeyboardNavigation(_session);
        _session.SetFocus(_shell.NavigationFocus);
        _appearance = new AppearanceController(_session, model.Settings, null);
        _appearance.Applied += (_, _) =>
        {
            if (NativeHandle != 0)
                WindowsTitleBar.ApplyDarkMode(NativeHandle, _appearance.Current?.IsDark ?? false);
            Invalidate();
        };

        CloseRequested += (_, _) => RequestClose();
        if (ownsMessageLoop)
            Closed += (_, _) => PostQuitMessage(0);
    }

    protected override int RunCore()
    {
        Show();
        DrainDispatcher();
        while (true)
        {
            int result = GetMessage(out MSG message, 0, 0, 0);
            if (result == -1) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (result == 0) return unchecked((int)message.WParam);
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
    }

    private async void RequestClose()
    {
        if (_closePending) return;
        _closePending = true;
        bool saved;
        try { saved = await _shell.PrepareCloseAsync().ConfigureAwait(false); }
        catch { saved = false; }

        PostToUiThread(() =>
        {
            _closePending = false;
            if (saved) Close();
            else { _shell.ShowView("compose"); Invalidate(); }
        });
    }

    protected override void OnCreated()
    {
        base.OnCreated();
        _clipboard = new WindowsClipboard(NativeHandle);
        _automationBridge ??= new WindowsAutomationBridge(RenderNativeHandle, _session, _shell.Window, () => DpiScale);
        _inputBridge ??= new WindowsInputBridge(NativeHandle, RenderNativeHandle, _session, _keyboard.Handle, () => DpiScale, Invalidate);
        WindowsTitleBar.ApplyDarkMode(NativeHandle, _appearance.Current?.IsDark ?? false);
        DrainDispatcher();
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

    BSize IUiHost.ViewportSize => ClientSize;
    double IUiHost.Scale => DpiScale;
    BRenderList IUiHost.CreateRenderList(int capacity) => new(capacity);
    void IUiHost.Invalidate(UiInvalidation invalidation) => Invalidate();
    void IUiHost.Present(BRenderList renderList) { }
    bool IOfficeUiThreadTarget.PostUiAction(Action callback) => PostToUiThread(callback);

    bool IUiClipboardHost.TryGetText(out string text)
    {
        text = string.Empty;
        return _clipboard is not null && _clipboard.TryGetText(out text);
    }

    void IUiClipboardHost.SetText(string text) => _clipboard?.SetText(text);
    void IUiTextInputHost.PublishCaret(UiTextCaretInfo caret) { }
    void IUiTextInputHost.ClearCaret(UiElement owner) { }

    BImageHandle IUiImageHost.CreateImage(ReadOnlySpan<byte> encodedImage)
    {
        if (encodedImage.IsEmpty || Renderer is not { } renderer) return BImageHandle.Invalid;
        try { return renderer.CreateImage(encodedImage); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return BImageHandle.Invalid; }
    }

    BImageHandle IUiImageHost.CreateImage(BPixelBuffer pixels)
    {
        if (Renderer is not { } renderer) return BImageHandle.Invalid;
        try { return renderer.CreateImage(pixels); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return BImageHandle.Invalid; }
    }

    void IUiImageHost.ReleaseImage(BImageHandle image)
    {
        if (image.IsValid) Renderer?.ReleaseImage(image);
    }

    public IUiHostWindow CreateHostWindow(UiHostWindowRequest request)
    {
        var hostWindow = new OfficeHostWindow(
            OfficeHostWindow.ToScreenPlacement(this, DpiScale, request),
            this,
            StandardControlPaint.Theme.SurfaceAlt,
            "Broiler Mail",
            () => _clipboard is not null && _clipboard.TryGetText(out string text) ? text : null,
            text => _clipboard?.SetText(text));
        _hostWindows.Add(hostWindow);
        hostWindow.Closed += (_, _) => _hostWindows.Remove(hostWindow);
        hostWindow.Show();
        return hostWindow;
    }

    protected override void OnResized(BSize clientSize, double dpiScale) => Invalidate();
    protected override void OnPointerDown(BPointerEventArgs e) => Dispatch(_input.FromPointerButton(e));
    protected override void OnPointerUp(BPointerEventArgs e) => Dispatch(_input.FromPointerButton(e));
    protected override void OnPointerMove(BPointerEventArgs e) => Dispatch(_input.FromPointerMove(e));
    protected override void OnMouseWheel(BMouseWheelEventArgs e) => Dispatch(_input.FromMouseWheel(e));
    protected override void OnKeyDown(BKeyEventArgs e) => Dispatch(_input.FromKey(e, KeyboardKeyTransition.Down));
    protected override void OnKeyUp(BKeyEventArgs e) => Dispatch(_input.FromKey(e, KeyboardKeyTransition.Up));
    protected override void OnTextInput(BTextInputEventArgs e) => _inputBridge?.ProcessTextInput(e.Character);

    protected override void OnNativeWindowMessage(nint hwnd, uint message, nint wParam, nint lParam)
    {
        base.OnNativeWindowMessage(hwnd, message, wParam, lParam);
        if (hwnd == NativeHandle)
        {
            WindowsWindowSizing.OnMessage(hwnd, message, lParam, DpiScale);
            _inputBridge?.OnTopLevelMessage(message, wParam, lParam);
        }
    }

    private void Dispatch(UiInputEvent input)
    {
        if (_keyboard.Handle(input) || _session.DispatchInput(input))
            Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _automationBridge?.Dispose(); } catch { }
            _automationBridge = null;
            _inputBridge?.Dispose();
            foreach (var hostWindow in _hostWindows.ToArray())
                hostWindow.Dispose();
            _hostWindows.Clear();
            _shell.Dispose();
            _appearance.Dispose();
            _session.Dispose();
        }
        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint Hwnd;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int PtX;
        public int PtY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);
}
#endif
