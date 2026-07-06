using Broiler.Graphics;
using Broiler.Graphics.Windows;
using Broiler.UI;
using Broiler.UI.Label;
using Broiler.UI.Label.Standard;
using Broiler.UI.Panel;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window.Standard;
using System.Runtime.Versioning;

namespace Broiler.App.Graphics;

[SupportedOSPlatform("windows7.0")]
internal sealed class Phase3UiPreviewWindow : Direct2DWindow
{
    private readonly PreviewUiHost _host;
    private readonly UiSession _session;
#pragma warning disable CS0618
    private readonly StandardLegacyGraphicsInputAdapter _legacyInput = new();
#pragma warning restore CS0618

    public Phase3UiPreviewWindow()
        : base(new BWindowOptions
        {
            Title = "Broiler.UI Phase 3",
            ClientWidth = 640,
            ClientHeight = 360,
            ClearColor = BColor.White,
            RenderOptions = new BRenderOptions(Antialias: true, VSync: true, SubpixelText: true),
        })
    {
        _host = new PreviewUiHost(this);
        _session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(_host);
        _session.AddRoot(CreatePreviewTree());
    }

    protected override BRenderList? BuildRenderList(BSize clientSize)
    {
        _host.Update(clientSize, DpiScale);
        return _session.RenderFrame();
    }

    protected override void OnResized(BSize clientSize, double dpiScale)
    {
        _host.Update(clientSize, dpiScale);
        Invalidate();
    }

    protected override void OnPointerDown(BPointerEventArgs e) =>
        Dispatch(_legacyInput.FromPointerButton(e));

    protected override void OnPointerMove(BPointerEventArgs e) =>
        Dispatch(_legacyInput.FromPointerMove(e));

    protected override void OnPointerUp(BPointerEventArgs e) =>
        Dispatch(_legacyInput.FromPointerButton(e));

    protected override void OnMouseWheel(BMouseWheelEventArgs e) =>
        Dispatch(_legacyInput.FromMouseWheel(e));

    protected override void OnKeyDown(BKeyEventArgs e) =>
        Dispatch(_legacyInput.FromKey(e));

    protected override void OnTextInput(BTextInputEventArgs e) =>
        Dispatch(_legacyInput.FromText(e));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _session.Dispose();

        base.Dispose(disposing);
    }

    private void Dispatch(UiInputEvent input)
    {
        if (_session.DispatchInput(input))
            Invalidate();
    }

    private static StandardWindow CreatePreviewTree()
    {
        var window = new StandardWindow { Title = "Broiler.UI Phase 3" };
        var root = new StandardPanel
        {
            LayoutMode = UiPanelLayoutMode.Stack,
            Spacing = 8,
            Background = BColor.FromArgb(248, 250, 252),
        };
        root.AddChild(new StandardLabel
        {
            Text = "&Window, Panel, and Label",
            Foreground = BColor.FromArgb(24, 54, 82),
            Font = BFontStyle.Default with { SizeInPixels = 24, Weight = BFontWeight.SemiBold },
        });
        root.AddChild(new StandardLabel
        {
            Text = "This preview is composed by the Windows app host, rendered through Broiler.Graphics, and routed through the UI session.",
            Wrapping = UiTextWrapping.Wrap,
            Foreground = BColor.FromArgb(54, 65, 75),
        });
        root.AddChild(new StandardLabel
        {
            Text = "Resize the native window to exercise viewport relayout and DPI-aware binding.",
            Wrapping = UiTextWrapping.Wrap,
            Foreground = BColor.FromArgb(74, 85, 95),
        });
        window.AddChild(root);
        window.OpenOwnedWindow(
            new StandardWindow
            {
                Title = "Managed child",
                Background = BColor.FromArgb(255, 255, 255),
            },
            new BRect(360, 170, 220, 96));
        ((StandardWindow)window.OwnedWindows[0]).AddChild(new StandardLabel
        {
            Text = "Logical subwindow, no HWND",
            Wrapping = UiTextWrapping.Wrap,
            Foreground = BColor.FromArgb(24, 54, 82),
        });
        return window;
    }

    private sealed class PreviewUiHost : IUiHost
    {
        private readonly Phase3UiPreviewWindow _window;

        public PreviewUiHost(Phase3UiPreviewWindow window)
        {
            _window = window;
        }

        public BSize ViewportSize { get; private set; } = new(640, 360);

        public double Scale { get; private set; } = 1;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation) => _window.Invalidate();

        public void Present(BRenderList renderList)
        {
        }

        public void Update(BSize viewportSize, double scale)
        {
            ViewportSize = viewportSize;
            Scale = scale;
        }
    }
}
