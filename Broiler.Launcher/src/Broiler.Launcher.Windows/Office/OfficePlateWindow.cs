#if BROILER_OFFICE_ALL_IN_ONE
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Rendering;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Windowing;
using Broiler.Graphics.Windows;
using Broiler.Hosting.Windows;
using Broiler.Input.Keyboard;
using Broiler.Launcher.Office;
using Broiler.Plate;
using Broiler.UI;
using Broiler.UI.Standard;

namespace Broiler.Launcher.Windows.Office;

/// <summary>
/// Win32/Direct2D host for Broiler Plate inside the all-in-one Broiler Office executable.
/// </summary>
[SupportedOSPlatform("windows7.0")]
internal sealed class OfficePlateWindow : Direct2DWindow, IOfficeUiThreadTarget
{
    private const int AppIconPixels = 64;
    private const int TooltipTickMilliseconds = 33;

    private readonly PlateWindowUiHost _host;
    private readonly PlateApp _app;
    private WindowsClipboard? _clipboard;

#pragma warning disable CS0618
    private readonly StandardLegacyGraphicsInputAdapter _legacyInput = new("broiler-plate");
#pragma warning restore CS0618

    public OfficePlateWindow(PlateFileFormats formats, string? initialPath = null, bool ownsMessageLoop = true)
        : base(new BWindowOptions
        {
            Title = "Broiler Plate",
            ClientWidth = 1120,
            ClientHeight = 780,
            ClearColor = PlatePalette.Canvas,
            RenderOptions = new BRenderOptions(Antialias: true, VSync: true, SubpixelText: true),
            OwnsMessageLoop = ownsMessageLoop,
        })
    {
        if (!ownsMessageLoop)
            CloseRequested += (_, _) => Close();

        _host = new PlateWindowUiHost(
            () => ClientSize,
            () => DpiScale,
            Invalidate,
            static _ => { },
            () => this,
            ReadClipboardText,
            WriteClipboardText,
            getRenderer: () => Renderer);

        _app = new PlateApp(
            _host,
            CloseNativeWindow,
            formats,
            new WindowsPrintHost(),
            OfficeComposition.GetPlatePdfPrintPermission,
            setTicking: SetTicking,
            scanHost: new WindowsScanHost());

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            if (Directory.Exists(initialPath))
                _app.OpenFolder(initialPath);
            else if (File.Exists(initialPath))
                _app.OpenFile(initialPath);
        }
    }

    protected override void OnCreated()
    {
        _clipboard = new WindowsClipboard(NativeHandle);
        SetIcon(PlateApp.CreateAppIcon(AppIconPixels));
    }

    private string? ReadClipboardText() =>
        _clipboard is not null && _clipboard.TryGetText(out string text) ? text : null;

    private void WriteClipboardText(string text) => _clipboard?.SetText(text);

    private void SetTicking(bool ticking)
    {
        if (ticking)
            StartAnimationTimer(TooltipTickMilliseconds);
        else
            StopAnimationTimer();
    }

    bool IOfficeUiThreadTarget.PostUiAction(Action callback) => PostToUiThread(callback);

    protected override void OnAnimationTick() => _app.AnimationTick();
    protected override BRenderList? BuildRenderList(BSize clientSize) => _app.RenderFrame();
    protected override void OnResized(BSize clientSize, double dpiScale) => _app.Invalidate();
    protected override void OnPointerDown(BPointerEventArgs e) => _app.Dispatch(_legacyInput.FromPointerButton(e));
    protected override void OnPointerMove(BPointerEventArgs e) => _app.Dispatch(_legacyInput.FromPointerMove(e));
    protected override void OnPointerUp(BPointerEventArgs e) => _app.Dispatch(_legacyInput.FromPointerButton(e));
    protected override void OnMouseWheel(BMouseWheelEventArgs e) => _app.Dispatch(_legacyInput.FromMouseWheel(e));
    protected override void OnKeyDown(BKeyEventArgs e) => _app.Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Down));
    protected override void OnKeyUp(BKeyEventArgs e) => _app.Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Up));
    protected override void OnTextInput(BTextInputEventArgs e) => _app.Dispatch(_legacyInput.FromText(e));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _app.Dispose();
            _host.Dispose();
        }
        base.Dispose(disposing);
    }

    private void CloseNativeWindow()
    {
        if (!PostToUiThread(CloseNativeWindowNow))
            CloseNativeWindowNow();
    }

    private void CloseNativeWindowNow()
    {
        if (NativeHandle != 0)
            _ = DestroyWindow(NativeHandle);
    }

    protected override void OnNativeWindowMessage(nint hwnd, uint message, nint wParam, nint lParam)
    {
        base.OnNativeWindowMessage(hwnd, message, wParam, lParam);
        if (hwnd == NativeHandle)
        {
            WindowsWindowSizing.OnMessage(hwnd, message, lParam, DpiScale);
            if ((message == 0x0006 && wParam.ToInt64() != 0) || message == 0x0007)
            {
                if (RenderNativeHandle != 0)
                    _ = SetFocus(RenderNativeHandle);
            }
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetFocus(nint hWnd);

    private sealed class PlateWindowUiHost : PlateUiHost, IUiWindowHost
    {
        private readonly List<OfficeHostWindow> _hostWindows = [];
        private readonly Func<BWindow?> _getOwnerWindow;
        private readonly Func<string?>? _getClipboardText;
        private readonly Action<string>? _setClipboardText;

        public PlateWindowUiHost(
            Func<BSize> getViewportSize,
            Func<double> getScale,
            Action invalidate,
            Action<BRenderList> present,
            Func<BWindow?> getOwnerWindow,
            Func<string?>? getClipboardText,
            Action<string>? setClipboardText,
            Func<IBroilerRenderer?>? getRenderer)
            : base(getViewportSize, getScale, invalidate, present, getClipboardText, setClipboardText, getRenderer: getRenderer)
        {
            _getOwnerWindow = getOwnerWindow;
            _getClipboardText = getClipboardText;
            _setClipboardText = setClipboardText;
        }

        public IUiHostWindow CreateHostWindow(UiHostWindowRequest request)
        {
            BWindow? owner = _getOwnerWindow();
            var window = new OfficeHostWindow(
                OfficeHostWindow.ToScreenPlacement(owner, Scale, request),
                owner,
                PlatePalette.Canvas,
                "Broiler Plate",
                _getClipboardText,
                _setClipboardText);
            _hostWindows.Add(window);
            window.Closed += (_, _) => _hostWindows.Remove(window);
            window.Show();
            return window;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var window in _hostWindows.ToArray())
                    window.Dispose();
                _hostWindows.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
#endif
