#if BROILER_OFFICE_ALL_IN_ONE
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Broiler.Graphics;
using Broiler.Graphics.Color;
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
using Broiler.UI;
using Broiler.UI.Standard;

namespace Broiler.Launcher.Windows.Office;

/// <summary>
/// Shared native secondary window for broken-out dialogs in Broiler Office on Windows.
/// </summary>
[SupportedOSPlatform("windows7.0")]
internal sealed class OfficeHostWindow : Direct2DWindow, IUiHostWindow, IUiWindowChromeHost,
    IUiClipboardHost, IUiTextInputHost, IUiImageHost
{
    private readonly Func<string?>? _getClipboardText;
    private readonly Action<string>? _setClipboardText;
    private UiSession? _session;
    private WindowsAutomationBridge? _automationBridge;
    private WindowsInputBridge? _inputBridge;

#pragma warning disable CS0618
    private readonly StandardLegacyGraphicsInputAdapter _legacyInput = new("broiler-office-dialog");
#pragma warning restore CS0618

    public OfficeHostWindow(
        UiHostWindowRequest request,
        BWindow? owner,
        BColor clearColor,
        string fallbackTitle,
        Func<string?>? getClipboardText,
        Action<string>? setClipboardText)
        : base(new BWindowOptions
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? fallbackTitle : request.Title,
            ClientWidth = ToClientExtent(request.Placement.Width, 640),
            ClientHeight = ToClientExtent(request.Placement.Height, 420),
            Left = request.Placement.IsEmpty ? null : request.Placement.X,
            Top = request.Placement.IsEmpty ? null : request.Placement.Y,
            ClearColor = clearColor,
            RenderOptions = new BRenderOptions(Antialias: true, VSync: true, SubpixelText: true),
            OwnsMessageLoop = false,
            Chrome = request.Chrome == UiHostWindowChrome.Owner ? BWindowChrome.Owner : BWindowChrome.System,
            Resizable = request.Resizable,
            Owner = request.IsModal ? owner : null,
        })
    {
        _getClipboardText = getClipboardText;
        _setClipboardText = setClipboardText;
        StateChanged += (_, _) => WindowStateChanged?.Invoke(this, EventArgs.Empty);
        CloseRequested += (_, _) => PostToUiThread(Close);
    }

    public static UiHostWindowRequest ToScreenPlacement(BWindow? owner, double scale, UiHostWindowRequest request)
    {
        if (request.Placement.IsEmpty)
            return request;

        nint ownerHandle = owner?.NativeHandle ?? 0;
        if (ownerHandle == 0)
            return request;

        var origin = default(NativePoint);
        if (!ClientToScreen(ownerHandle, ref origin))
            return request;

        double resolvedScale = scale > 0 ? scale : 1;
        BRect placement = request.Placement;
        return request with
        {
            Placement = new BRect(
                (origin.X / resolvedScale) + placement.X,
                (origin.Y / resolvedScale) + placement.Y,
                placement.Width,
                placement.Height),
        };
    }

    protected override void OnCreated()
    {
        base.OnCreated();
        EnsureBridges();
    }

    private void EnsureBridges()
    {
        if (_session is null || _session.IsDisposed || RenderNativeHandle == 0)
            return;
        _automationBridge ??= new WindowsAutomationBridge(
            RenderNativeHandle,
            _session,
            _session.Roots.Count > 0 ? _session.Roots[0] : null!,
            () => DpiScale);
        _inputBridge ??= new WindowsInputBridge(
            NativeHandle,
            RenderNativeHandle,
            _session,
            null,
            () => DpiScale,
            InvalidateIfAlive);
    }

    BSize IUiHost.ViewportSize => ClientSize;
    double IUiHost.Scale => DpiScale;
    BRenderList IUiHost.CreateRenderList(int capacity) => new(capacity);
    void IUiHost.Invalidate(UiInvalidation invalidation) => InvalidateIfAlive();
    void IUiHost.Present(BRenderList renderList) { }

    public void Bind(UiSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        EnsureBridges();
        InvalidateIfAlive();
    }

    public void Activate()
    {
        if (IsDisposed || NativeHandle == 0)
            return;
        _ = SetForegroundWindow(NativeHandle);
        InvalidateIfAlive();
    }

    public event EventHandler? WindowStateChanged;
    UiHostWindowChrome IUiWindowChromeHost.Chrome =>
        Options.Chrome == BWindowChrome.Owner ? UiHostWindowChrome.Owner : UiHostWindowChrome.System;
    bool IUiWindowChromeHost.IsResizable => Options.Resizable;
    UiHostWindowState IUiWindowChromeHost.WindowState => WindowState switch
    {
        BWindowState.Minimized => UiHostWindowState.Minimized,
        BWindowState.Maximized => UiHostWindowState.Maximized,
        _ => UiHostWindowState.Normal,
    };

    void IUiWindowChromeHost.SetWindowState(UiHostWindowState state) => SetWindowState(state switch
    {
        UiHostWindowState.Minimized => BWindowState.Minimized,
        UiHostWindowState.Maximized => BWindowState.Maximized,
        _ => BWindowState.Normal,
    });

    void IUiWindowChromeHost.SetIcon(BPixelBuffer? icon) => SetIcon(icon);
    void IUiWindowChromeHost.RequestClose() => PostToUiThread(Close);
    void IUiWindowChromeHost.BeginMoveDrag() => BeginMoveDrag();
    void IUiWindowChromeHost.BeginResizeDrag(UiWindowEdge edge) => BeginResizeDrag(edge switch
    {
        UiWindowEdge.Left => BWindowEdge.Left,
        UiWindowEdge.Top => BWindowEdge.Top,
        UiWindowEdge.Right => BWindowEdge.Right,
        UiWindowEdge.Bottom => BWindowEdge.Bottom,
        UiWindowEdge.TopLeft => BWindowEdge.TopLeft,
        UiWindowEdge.TopRight => BWindowEdge.TopRight,
        UiWindowEdge.BottomLeft => BWindowEdge.BottomLeft,
        UiWindowEdge.BottomRight => BWindowEdge.BottomRight,
        _ => BWindowEdge.Right,
    });

    bool IUiClipboardHost.TryGetText(out string text)
    {
        text = _getClipboardText?.Invoke() ?? string.Empty;
        return text.Length > 0;
    }

    void IUiClipboardHost.SetText(string text) => _setClipboardText?.Invoke(text ?? string.Empty);
    void IUiTextInputHost.PublishCaret(UiTextCaretInfo caret) { }
    void IUiTextInputHost.ClearCaret(UiElement owner) { }

    BImageHandle IUiImageHost.CreateImage(ReadOnlySpan<byte> encodedImage)
    {
        if (encodedImage.IsEmpty || Renderer is not { } renderer)
            return BImageHandle.Invalid;
        try { return renderer.CreateImage(encodedImage); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return BImageHandle.Invalid; }
    }

    BImageHandle IUiImageHost.CreateImage(BPixelBuffer pixels)
    {
        if (Renderer is not { } renderer)
            return BImageHandle.Invalid;
        try { return renderer.CreateImage(pixels); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return BImageHandle.Invalid; }
    }

    void IUiImageHost.ReleaseImage(BImageHandle image)
    {
        if (image.IsValid)
            Renderer?.ReleaseImage(image);
    }

    protected override BRenderList? BuildRenderList(BSize clientSize) =>
        _session is { IsDisposed: false } session ? session.RenderFrame() : null;
    protected override void OnResized(BSize clientSize, double dpiScale) => InvalidateIfAlive();
    protected override void OnPointerDown(BPointerEventArgs e) => Dispatch(_legacyInput.FromPointerButton(e));
    protected override void OnPointerMove(BPointerEventArgs e) => Dispatch(_legacyInput.FromPointerMove(e));
    protected override void OnPointerUp(BPointerEventArgs e) => Dispatch(_legacyInput.FromPointerButton(e));
    protected override void OnMouseWheel(BMouseWheelEventArgs e) => Dispatch(_legacyInput.FromMouseWheel(e));
    protected override void OnKeyDown(BKeyEventArgs e) => Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Down));
    protected override void OnKeyUp(BKeyEventArgs e) => Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Up));
    protected override void OnTextInput(BTextInputEventArgs e) => Dispatch(_legacyInput.FromText(e));

    protected override void OnNativeWindowMessage(nint hwnd, uint message, nint wParam, nint lParam)
    {
        base.OnNativeWindowMessage(hwnd, message, wParam, lParam);
        if (hwnd == NativeHandle)
        {
            WindowsWindowSizing.OnMessage(hwnd, message, lParam, DpiScale);
            _inputBridge?.OnTopLevelMessage(message, wParam, lParam);
            if ((message == 0x0006 && wParam.ToInt64() != 0) || message == 0x0007)
            {
                if (RenderNativeHandle != 0)
                    _ = SetFocus(RenderNativeHandle);
            }
        }
    }

    protected override void CloseCore()
    {
        try
        {
            if (_session is { IsDisposed: false } session)
                session.SetFocus(null);
        }
        catch (Exception)
        {
        }
        try
        {
            base.CloseCore();
        }
        catch (COMException)
        {
        }
        catch (Exception)
        {
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _automationBridge?.Dispose(); } catch { }
            _automationBridge = null;
            _inputBridge?.Dispose();
            _inputBridge = null;
            try
            {
                if (!IsDisposed)
                    Close();
            }
            catch
            {
            }
        }
        base.Dispose(disposing);
    }

    private void Dispatch(UiInputEvent input)
    {
        if (_session is { IsDisposed: false } session && session.DispatchInput(input))
            InvalidateIfAlive();
    }

    private void InvalidateIfAlive()
    {
        if (!IsDisposed && NativeHandle != 0)
            Invalidate();
    }

    private static int ToClientExtent(double extent, int fallback) =>
        double.IsFinite(extent) && extent > 0 ? Math.Max(120, (int)Math.Ceiling(extent)) : fallback;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint hwnd, ref NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint SetFocus(nint hWnd);
}
#endif
