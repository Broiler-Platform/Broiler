using System.Runtime.Versioning;
using Broiler.Graphics;
using Broiler.Graphics.Windows;
using Broiler.Input.Keyboard;
using Broiler.UI;
using Broiler.UI.Standard;

namespace Broiler.App.Graphics;

[SupportedOSPlatform("windows7.0")]
internal sealed class BrowserWindow : Direct2DWindow
{
    private readonly BrowserUiHost _host;
    private readonly BrowserApp _app;

#pragma warning disable CS0618
    private readonly StandardLegacyGraphicsInputAdapter _legacyInput = new("broiler-browser");
#pragma warning restore CS0618

    public BrowserWindow(string? initialUrl)
        : base(new BWindowOptions
        {
            Title = "Broiler Browser",
            ClientWidth = 1100,
            ClientHeight = 800,
            ClearColor = BrowserPalette.Canvas,
            RenderOptions = new BRenderOptions(Antialias: true, VSync: true, SubpixelText: true),
        })
    {
        _host = new BrowserUiHost(
            () => ClientSize,
            () => DpiScale,
            Invalidate,
            static _ => { },
            PostToUiThread);
        _app = new BrowserApp(_host, () => Renderer, initialUrl, SetAnimationActive);
    }

    protected override BRenderList? BuildRenderList(BSize clientSize) => _app.RenderFrame();

    protected override BFrameContext CreateFrameContext(long frameIndex) =>
        new(_app.ResolveClearColor(), frameIndex, Options.RenderOptions);

    protected override void OnResized(BSize clientSize, double dpiScale) => _app.Invalidate();

    protected override void OnGraphicsResourcesReleasing() => _app.ReleaseGraphicsResources();

    protected override void OnAnimationTick() => _app.StepAnimation();

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

    private void Dispatch(UiInputEvent input) => _app.Dispatch(input);

    private void SetAnimationActive(bool active)
    {
        if (active)
            StartAnimationTimer(16);
        else
            StopAnimationTimer();
    }

}
