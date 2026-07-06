using Broiler.Graphics;
using Broiler.Graphics.Windows;
using Broiler.Input.Keyboard;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Label;
using Broiler.UI.Label.Standard;
using Broiler.UI.Panel;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Toolbar.Standard;
using Broiler.UI.Window.Standard;
using System.Runtime.Versioning;

namespace Broiler.App.Graphics;

[SupportedOSPlatform("windows7.0")]
internal sealed class Phase4ToolbarPreviewWindow : Direct2DWindow
{
    private readonly PreviewUiHost _host;
    private readonly UiSession _session;
    private readonly StandardEdit _address;
    private readonly StandardLabel _status;
    private readonly List<string> _history = [];
    private int _historyIndex = -1;
#pragma warning disable CS0618
    private readonly StandardLegacyGraphicsInputAdapter _legacyInput = new();
#pragma warning restore CS0618

    public Phase4ToolbarPreviewWindow()
        : base(new BWindowOptions
        {
            Title = "Broiler.UI Phase 4 Toolbar",
            ClientWidth = 900,
            ClientHeight = 220,
            ClearColor = BColor.White,
            RenderOptions = new BRenderOptions(Antialias: true, VSync: true, SubpixelText: true),
        })
    {
        _host = new PreviewUiHost(this);
        _session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(_host);
        StandardWindow window = CreateToolbarTree(out _address, out _status);
        _session.AddRoot(window);
        _session.SetFocus(_address);
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
        Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Down));

    protected override void OnKeyUp(BKeyEventArgs e) =>
        Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Up));

    protected override void OnTextInput(BTextInputEventArgs e) =>
        Dispatch(_legacyInput.FromText(e));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _session.Dispose();

        base.Dispose(disposing);
    }

    private StandardWindow CreateToolbarTree(out StandardEdit address, out StandardLabel status)
    {
        var window = new StandardWindow { Title = "Toolbar proof", Background = BColor.FromArgb(0xFF, 0xF8, 0xFA, 0xFC) };
        var root = new StandardPanel { LayoutMode = UiPanelLayoutMode.Stack, Spacing = 10, Background = BColor.FromArgb(0xFF, 0xF8, 0xFA, 0xFC) };
        var toolbar = new StandardToolbar
        {
            Title = "Navigation toolbar",
            Spacing = 8,
            Padding = 6,
            PreferredSize = new BSize(760, 44),
        };
        var back = new StandardButton { Text = "<", PreferredSize = new BSize(40, 32), Command = new StandardCommand("back", NavigateBack, CanNavigateBack) };
        var forward = new StandardButton { Text = ">", PreferredSize = new BSize(40, 32), Command = new StandardCommand("forward", NavigateForward, CanNavigateForward) };
        var refresh = new StandardButton { Text = "Refresh", PreferredSize = new BSize(84, 32), Command = new StandardCommand("refresh", RefreshCurrent, () => _historyIndex >= 0) };
        var addressBox = new StandardEdit { PlaceholderText = "Type a URL and press Enter", PreferredSize = new BSize(520, 32) };
        var go = new StandardButton { Text = "Go", PreferredSize = new BSize(56, 32), IsDefault = true };
        status = new StandardLabel { Text = "Ready", Wrapping = UiTextWrapping.Wrap, Foreground = BColor.FromArgb(0xFF, 0x36, 0x41, 0x4D) };
        toolbar.AddChild(back);
        toolbar.AddChild(forward);
        toolbar.AddChild(refresh);
        toolbar.AddChild(addressBox);
        toolbar.AddChild(go);
        toolbar.SetSeparatorBefore(addressBox, true);
        root.AddChild(new StandardLabel
        {
            Text = "Broiler.UI toolbar proof",
            Font = BFontStyle.Default with { SizeInPixels = 22, Weight = BFontWeight.SemiBold },
            Foreground = BColor.FromArgb(0xFF, 0x18, 0x36, 0x52),
        });
        root.AddChild(toolbar);
        root.AddChild(status);
        window.AddChild(root);

        addressBox.Submitted += (_, _) => Navigate(addressBox.Text);
        go.Clicked += (_, _) => Navigate(addressBox.Text);
        address = addressBox;
        return window;
    }

    private void Navigate(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        url = url.Trim();
        if (_historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);

        _history.Add(url);
        _historyIndex = _history.Count - 1;
        _address.Text = url;
        _address.SetSelection(_address.Text.Length, 0);
        _status.Text = "Loaded " + url;
        Invalidate();
    }

    private void NavigateBack()
    {
        if (!CanNavigateBack())
            return;

        _historyIndex--;
        ApplyHistorySelection("Back");
    }

    private void NavigateForward()
    {
        if (!CanNavigateForward())
            return;

        _historyIndex++;
        ApplyHistorySelection("Forward");
    }

    private void RefreshCurrent()
    {
        if (_historyIndex < 0)
            return;

        ApplyHistorySelection("Refreshed");
    }

    private bool CanNavigateBack() => _historyIndex > 0;

    private bool CanNavigateForward() => _historyIndex >= 0 && _historyIndex < _history.Count - 1;

    private void ApplyHistorySelection(string verb)
    {
        string url = _history[_historyIndex];
        _address.Text = url;
        _address.SetSelection(url.Length, 0);
        _status.Text = verb + " " + url;
        Invalidate();
    }

    private void Dispatch(UiInputEvent input)
    {
        if (_session.DispatchInput(input))
            Invalidate();
    }

    private sealed class PreviewUiHost : IUiHost, IUiClipboardHost
    {
        private readonly Phase4ToolbarPreviewWindow _window;
        private string _clipboard = string.Empty;

        public PreviewUiHost(Phase4ToolbarPreviewWindow window)
        {
            _window = window;
        }

        public BSize ViewportSize { get; private set; } = new(900, 220);

        public double Scale { get; private set; } = 1;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation) => _window.Invalidate();

        public void Present(BRenderList renderList)
        {
        }

        public bool TryGetText(out string text)
        {
            text = _clipboard;
            return true;
        }

        public void SetText(string text) => _clipboard = text;

        public void Update(BSize viewportSize, double scale)
        {
            ViewportSize = viewportSize;
            Scale = scale;
        }
    }
}
