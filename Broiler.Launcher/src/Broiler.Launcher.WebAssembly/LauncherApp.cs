using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.WebAssembly;
using Broiler.Hosting;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Launcher.Browser;
using Broiler.UI;
using Broiler.UI.Standard;
using Broiler.UI.Window;
using Broiler.UI.Window.Standard;

namespace Broiler.Launcher.WebAssembly;

[SupportedOSPlatform("browser")]
internal sealed class LauncherUiHost(BrowserCanvasRenderer renderer) : IUiHost
{
    public BSize ViewportSize { get; set; } = new(1000, 600);
    public double Scale { get; set; } = 1;
    public BColor Background { get; set; } = BColor.FromArgb(255, 244, 247, 251);
    public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
    public void Invalidate(UiInvalidation invalidation) => BrowserInterop.Invalidate();
    public void Present(BRenderList renderList) => renderer.PresentFrame(renderList,
        Math.Max(1, (int)Math.Ceiling(ViewportSize.Width * Scale)), Math.Max(1, (int)Math.Ceiling(ViewportSize.Height * Scale)),
        Scale, ViewportSize.Width, ViewportSize.Height, Background);
}
internal sealed class LauncherDispatcher(FrameDispatcher frames) : IUiDispatcher
{
    public bool CheckAccess() => true;
    public void Post(Action callback) => frames.Post(callback);
}
[SupportedOSPlatform("browser")]
internal sealed class Preferences : IBrowserPreferences
{
    public string? Read(string key) => BrowserInterop.ReadPreference(key);
    public void Write(string key, string value) => BrowserInterop.WritePreference(key, value);
}
[SupportedOSPlatform("browser")]
internal static class LauncherApp
{
    private static readonly InputDeviceId Pointer = InputDeviceId.FromOpaqueValue("browser-primary-pointer");
    private static readonly InputDeviceId Keyboard = InputDeviceId.FromOpaqueValue("browser-keyboard");
    private static FrameDispatcher? _frames;
    private static BrowserCanvasRenderer? _renderer;
    private static LauncherUiHost? _host;
    private static UiSession? _session;
    private static StandardWindow? _root;
    private static WebLauncherView? _view;
    private static BrowserLauncher? _launcher;
    private static long _sequence;
    internal static async Task StartAsync()
    {
        await BrowserCanvasRenderer.LoadModuleAsync("../broiler.graphics.webassembly.js");
        _renderer = new(); _renderer.Initialize("#launcher-canvas");
        _frames = new(BrowserInterop.Invalidate); _host = new(_renderer);
        var deployment = BrowserDeploymentInfo.Read(BrowserInterop.Metadata());
        _launcher = new(uri => BrowserInterop.Navigate(uri.AbsoluteUri, _launcher!.OpenInNewTab), new Preferences(), deployment);
        _session = new StandardUiSessionBuilder().WithDispatcher(new LauncherDispatcher(_frames)).Build(_host);
        _view = new(_launcher, deployment, () => { _launcher.SetTheme(_launcher.Theme == "dark" ? "light" : "dark"); ApplyTheme(); });
        _root = new() { Title = "Broiler Office", Chrome = UiWindowChrome.None, BorderThickness = 0 };
        _root.AddChild(_view); _session.AddRoot(_root);
        ApplyTheme(); Frame();
        BrowserInterop.Ready(deployment.Approved ? $"{deployment.Channel} · Office {deployment.CatalogVersion} · Writer {deployment.WriterVersion} · Cell {deployment.CellVersion}" : "Local preview");
    }
    private static void ApplyTheme()
    {
        bool dark = _launcher!.Theme == "dark";
        _host!.Background = dark ? BColor.FromArgb(255, 15, 23, 36) : BColor.FromArgb(255, 244, 247, 251);
        _root!.Background = _host.Background;
        StandardThemeController.Apply(_session!, dark ? StandardThemeTokens.Dark : StandardThemeTokens.Light);
        _view!.SetTheme(dark); BrowserInterop.ApplyTheme(dark ? "dark" : "light");
    }
    internal static void Frame() { _frames?.Drain(); _session?.RenderFrame(); }
    internal static void Resize(double width, double height, double dpr)
    {
        if (_host is null) return;
        _host.ViewportSize = new(Math.Clamp(width, 280, 10000), Math.Clamp(height, 1, 10000));
        _host.Scale = Math.Clamp(dpr, 0.5, 4);
        _root?.Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
    }
    private static InputEventHeader Header(InputDeviceId device, double time) => new(device,
        new InputTimestamp((long)Math.Max(0, time * 1000), 1_000_000, "browser-performance"), ++_sequence);
    internal static void PointerEvent(string kind, double x, double y, int buttons, int button, double time) => _frames?.Post(() => {
        var point = InputPoint.ClientDeviceIndependentPixels(x, y);
        if (kind == "move") _session?.DispatchInput(UiInputEvent.FromMouseMove(new MouseMoveEvent(Header(Pointer, time), point, (MouseButtons)buttons)));
        else _session?.DispatchInput(UiInputEvent.FromMouseButton(new MouseButtonEvent(Header(Pointer, time), point, (MouseButtons)buttons,
            button switch { 0 => MouseButton.Left, 1 => MouseButton.Middle, 2 => MouseButton.Right, _ => MouseButton.None },
            kind == "down" ? MouseButtonTransition.Down : MouseButtonTransition.Up)));
    });
    internal static void CancelPointer() => _frames?.Post(() => {
        if (_session?.CapturedElement is UiElement captured) _session.ReleaseInputCapture(captured);
    });
    internal static void Key(string name, bool down, int modifiers, bool repeat, double time) => _frames?.Post(() => {
        if (down && !repeat && (modifiers & 10) != 0 && name is "1" or "2" or "3" or "4") {
            _launcher?.Launch(name switch { "1" => "browser", "2" => "writer", "3" => "cell", _ => "plate" }); return;
        }
        string canonical = name == " " ? "Space" : name.StartsWith("Arrow", StringComparison.Ordinal) ? name[5..] : name;
        _session?.DispatchInput(UiInputEvent.FromKeyboardKey(new KeyboardKeyEvent(Header(Keyboard, time), KeyboardKey.FromName(canonical),
            down ? KeyboardKeyTransition.Down : KeyboardKeyTransition.Up, (KeyboardModifierState)modifiers, 0, 0, repeat ? 2 : 1, false, repeat, KeyboardKeyLocation.Standard)));
    });
    internal static void Launch(string id) => _launcher?.Launch(id);
    internal static void SetTheme(bool dark) { _launcher?.SetTheme(dark ? "dark" : "light"); ApplyTheme(); }
    internal static void SetNewTab(bool enabled) { _launcher?.SetOpenInNewTab(enabled); _view?.SetTheme(_launcher?.Theme == "dark"); }
    internal static void Dispose() { _session?.Dispose(); _frames?.Dispose(); _renderer?.Dispose(); _session = null; _frames = null; _renderer = null; }
}
[SupportedOSPlatform("browser")]
public static partial class LauncherExports
{
    [JSExport] public static void Render() => LauncherApp.Frame();
    [JSExport] public static void Resize(double width, double height, double dpr) => LauncherApp.Resize(width, height, dpr);
    [JSExport] public static void Pointer(string kind, double x, double y, int buttons, int button, double time) => LauncherApp.PointerEvent(kind, x, y, buttons, button, time);
    [JSExport] public static void CancelPointer() => LauncherApp.CancelPointer();
    [JSExport] public static void Key(string name, bool down, int modifiers, bool repeat, double time) => LauncherApp.Key(name, down, modifiers, repeat, time);
    [JSExport] public static void Launch(string id) => LauncherApp.Launch(id);
    [JSExport] public static void SetTheme(bool dark) => LauncherApp.SetTheme(dark);
    [JSExport] public static void SetNewTab(bool enabled) => LauncherApp.SetNewTab(enabled);
    [JSExport] public static void Dispose() => LauncherApp.Dispose();
}

