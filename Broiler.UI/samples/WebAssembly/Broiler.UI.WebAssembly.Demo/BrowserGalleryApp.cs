using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Broiler.Graphics.WebAssembly;

namespace Broiler.UI.WebAssembly.Demo;

/// <summary>
/// Owns the gallery lifecycle: loads the direct-Canvas 2D replay module, binds the presenter to the
/// page canvas, builds the <see cref="BrowserGalleryDemo"/>, and routes the page's input, resize,
/// and teardown calls to it. Mirrors the role of <c>Program</c>/<c>Win32DemoWindow.Run</c> in the
/// Win32 sample.
/// </summary>
[SupportedOSPlatform("browser")]
internal static class BrowserGalleryApp
{
    private const string CanvasSelector = "#gallery-canvas";

    // The initial logical box before the page reports its measured size via ResizeUi.
    private const double InitialWidth = 1280;
    private const double InitialHeight = 800;

    private static BrowserCanvasRenderer? _renderer;
    private static BrowserGalleryDemo? _demo;

    internal static async Task StartAsync()
    {
        // The managed JSHost.ImportAsync runs its dynamic import() from inside _framework/, so a
        // "./" specifier resolves there and 404s. The replay module publishes to the app root and is
        // fingerprinted in the page import map by its root URL, so import it with "../".
        await BrowserCanvasRenderer.LoadModuleAsync("../broiler.graphics.webassembly.js");

        var renderer = new BrowserCanvasRenderer();
        renderer.Initialize(CanvasSelector);
        _renderer = renderer;

        _demo = new BrowserGalleryDemo(
            renderer,
            reducedMotion: BrowserInterop.PrefersReducedMotion(),
            darkScheme: BrowserInterop.PrefersDarkScheme(),
            InitialWidth,
            InitialHeight,
            dpr: 1);

        _demo.RenderScheduledFrame();
        BrowserInterop.Ready(_demo.ViewportWidth, _demo.ViewportHeight);
    }

    internal static void Resize(double width, double height, double dpr) => Run(demo => demo.Resize(width, height, dpr));

    internal static void RenderFrame() => Run(static demo => demo.RenderScheduledFrame());

    internal static void PointerMove(double x, double y, int buttons, double timestampMilliseconds) =>
        Run(demo => demo.PointerMove(x, y, buttons, timestampMilliseconds));

    internal static void PointerButton(double x, double y, int buttons, int button, bool down, double timestampMilliseconds) =>
        Run(demo => demo.PointerButton(x, y, buttons, button, down, timestampMilliseconds));

    internal static void PointerWheel(double x, double y, int buttons, bool horizontal, double deltaNotches, double timestampMilliseconds) =>
        Run(demo => demo.PointerWheel(x, y, buttons, horizontal, deltaNotches, timestampMilliseconds));

    internal static void KeyboardKey(string keyName, bool down, int modifiers, int nativeKeyCode, bool repeat, int location, double timestampMilliseconds) =>
        Run(demo => demo.KeyboardKey(keyName, down, modifiers, nativeKeyCode, repeat, location, timestampMilliseconds));

    internal static void TextInput(string text, double timestampMilliseconds) =>
        Run(demo => demo.TextInput(text, timestampMilliseconds));

    internal static void TextComposition(string text, int state, int selectionStart, int selectionLength, double timestampMilliseconds) =>
        Run(demo => demo.TextComposition(text, state, selectionStart, selectionLength, timestampMilliseconds));

    internal static string ClipboardEvent(string operation, string text)
    {
        try
        {
            return _demo?.ClipboardEvent(operation, text) ?? string.Empty;
        }
        catch (Exception exception)
        {
            BrowserInterop.Failed(exception.GetType().Name, exception.ToString());
            return string.Empty;
        }
    }

    internal static void CancelPointer(double timestampMilliseconds) => Run(demo => demo.CancelPointer(timestampMilliseconds));

    internal static void Dispose()
    {
        BrowserGalleryDemo? demo = _demo;
        _demo = null;
        demo?.Dispose();
        BrowserCanvasRenderer? renderer = _renderer;
        _renderer = null;
        renderer?.Dispose();
    }

    private static void Run(Action<BrowserGalleryDemo> action)
    {
        try
        {
            if (_demo is BrowserGalleryDemo demo)
                action(demo);
        }
        catch (Exception exception)
        {
            BrowserInterop.Failed(exception.GetType().Name, exception.ToString());
        }
    }
}

/// <summary>Page-to-managed entry points invoked by <c>main.js</c>.</summary>
internal static partial class BrowserExports
{
    [JSExport]
    internal static void ResizeUi(double logicalWidth, double logicalHeight, double dpr) =>
        BrowserGalleryApp.Resize(logicalWidth, logicalHeight, dpr);

    [JSExport]
    internal static void RenderUiFrame() => BrowserGalleryApp.RenderFrame();

    [JSExport]
    internal static void UiPointerMove(double x, double y, int buttons, double timestampMilliseconds) =>
        BrowserGalleryApp.PointerMove(x, y, buttons, timestampMilliseconds);

    [JSExport]
    internal static void UiPointerButton(double x, double y, int buttons, int button, bool down, double timestampMilliseconds) =>
        BrowserGalleryApp.PointerButton(x, y, buttons, button, down, timestampMilliseconds);

    [JSExport]
    internal static void UiPointerWheel(double x, double y, int buttons, bool horizontal, double deltaNotches, double timestampMilliseconds) =>
        BrowserGalleryApp.PointerWheel(x, y, buttons, horizontal, deltaNotches, timestampMilliseconds);

    [JSExport]
    internal static void UiKeyboardKey(string keyName, bool down, int modifiers, int nativeKeyCode, bool repeat, int location, double timestampMilliseconds) =>
        BrowserGalleryApp.KeyboardKey(keyName, down, modifiers, nativeKeyCode, repeat, location, timestampMilliseconds);

    [JSExport]
    internal static void UiTextInput(string text, double timestampMilliseconds) =>
        BrowserGalleryApp.TextInput(text, timestampMilliseconds);

    [JSExport]
    internal static void UiTextComposition(string text, int state, int selectionStart, int selectionLength, double timestampMilliseconds) =>
        BrowserGalleryApp.TextComposition(text, state, selectionStart, selectionLength, timestampMilliseconds);

    [JSExport]
    internal static string UiClipboardEvent(string operation, string text) => BrowserGalleryApp.ClipboardEvent(operation, text);

    [JSExport]
    internal static void UiCancelPointer(double timestampMilliseconds) => BrowserGalleryApp.CancelPointer(timestampMilliseconds);

    [JSExport]
    internal static void Dispose() => BrowserGalleryApp.Dispose();
}
