using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Broiler.Graphics;
using Broiler.Graphics.Windows;
using Broiler.Input.Keyboard;
using Broiler.UI;
using Broiler.UI.Standard;

namespace Broiler.Writer;

[SupportedOSPlatform("windows7.0")]
internal sealed class WriterWindow : Direct2DWindow
{
    private readonly WriterUiHost _host;
    private readonly WriterApp _app;

#pragma warning disable CS0618
    private readonly StandardLegacyGraphicsInputAdapter _legacyInput = new("broiler-writer");
#pragma warning restore CS0618

    public WriterWindow()
        : base(new BWindowOptions
        {
            Title = "Broiler Writer",
            ClientWidth = 1120,
            ClientHeight = 780,
            ClearColor = WriterPalette.Canvas,
            RenderOptions = new BRenderOptions(Antialias: true, VSync: true, SubpixelText: true),
        })
    {
        _host = new WriterUiHost(
            () => ClientSize,
            () => DpiScale,
            Invalidate,
            static _ => { });
        _app = new WriterApp(_host, CloseNativeWindow);
    }

    protected override BRenderList? BuildRenderList(BSize clientSize) => _app.RenderFrame();

    protected override void OnResized(BSize clientSize, double dpiScale) => _app.Invalidate();

    protected override void OnPointerDown(BPointerEventArgs e) =>
        Dispatch(_legacyInput.FromPointerButton(e));

    protected override void OnPointerMove(BPointerEventArgs e) =>
        Dispatch(_legacyInput.FromPointerMove(e));

    protected override void OnPointerUp(BPointerEventArgs e) =>
        Dispatch(_legacyInput.FromPointerButton(e));

    protected override void OnMouseWheel(BMouseWheelEventArgs e) =>
        Dispatch(_legacyInput.FromMouseWheel(e));

    protected override void OnKeyDown(BKeyEventArgs e) =>
        Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Down));

    protected override void OnKeyUp(BKeyEventArgs e) =>
        Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Up));

    protected override void OnTextInput(BTextInputEventArgs e) =>
        Dispatch(_legacyInput.FromText(e));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _app.Dispose();

        base.Dispose(disposing);
    }

    private void CloseNativeWindow()
    {
        if (!PostToUiThread(CloseNativeWindowNow))
            CloseNativeWindowNow();
    }

    private void CloseNativeWindowNow()
    {
        if (NativeHandle != IntPtr.Zero)
            _ = DestroyWindow(NativeHandle);
    }

    private void Dispatch(UiInputEvent input) => _app.Dispatch(input);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hwnd);
}
