using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Broiler.Graphics.WebAssembly;
using Broiler.Hosting;
using Broiler.Hosting.WebAssembly;
using Broiler.Launcher.Browser;

namespace Broiler.Hosting.ContractSample;

[SupportedOSPlatform("browser")]
internal static partial class Program
{
    private static async Task Main()
    {
        await BrowserCanvasRenderer.LoadModuleAsync("../broiler.graphics.webassembly.js");
        new BrowserCanvasRenderer().Initialize("#surface");
        // Browser composition uses injected navigation/preferences, with no native provider assembly.
        var launcher = new BrowserLauncher(_ => { }, new Preferences());
        if (launcher.Capabilities.CanInstall || launcher.Capabilities.CanStartServer || launcher.Launch("plate"))
            throw new InvalidOperationException("Native capability leaked into browser composition.");
        Ready();
        await Task.Delay(Timeout.Infinite);
    }
    [JSImport("sample.ready", "main.js")] internal static partial void Ready();
    [JSImport("sample.invalidate", "main.js")] internal static partial void Invalidate();
    [JSImport("sample.openDocument", "main.js")] internal static partial Task<string> OpenDocument(string accept, double maximumBytes);
    [JSImport("sample.exportDocument", "main.js")] internal static partial Task<string> ExportDocument(string name, string contentType, string base64);
    private sealed class Preferences : IBrowserPreferences
    {
        public string? Read(string key) => null;
        public void Write(string key, string value) { }
    }
}

[SupportedOSPlatform("browser")]
public static partial class Exports
{
    private static readonly BrowserDocumentPort Documents = new(
        async (options, _) => {
            var fields = (await Program.OpenDocument(options.Accept, options.MaximumBytes)).Split('\0');
            var status = Enum.Parse<DocumentOperationStatus>(fields[0]);
            return status == DocumentOperationStatus.Opened
                ? new(status, new(fields[1], fields[2], Convert.FromBase64String(fields[3])))
                : new(status);
        },
        async (document, _) => new(Enum.Parse<DocumentOperationStatus>(await Program.ExportDocument(document.Name, document.ContentType, Convert.ToBase64String(document.Bytes.Span)))));
    private static readonly FrameDispatcher Dispatcher = new(Program.Invalidate);
    private static readonly TrustedClipboard Clipboard = new();
    private static string _text = "";
    private static int _cancelled, _frames, _commits, _cancelledComposition;
    [JSExport] public static void Text(string text) => Dispatcher.Post(() => _text += text);
    [JSExport] public static void Composition(string phase, string text)
    {
        if (phase == "commit") { _commits++; Text(text); }
        if (phase == "cancel") _cancelledComposition++;
    }
    [JSExport] public static void CancelPointer() => _cancelled++;
    [JSExport] public static void Frame() { Dispatcher.Drain(); _frames++; }
    [JSExport] public static string Snapshot() => $"{_text}|{_cancelled}|{_frames}|{_commits}|{_cancelledComposition}";
    [JSExport] public static string Copy(string kind, string incoming) => Clipboard.DuringEvent(kind == "paste" ? incoming : null, () => {
        if (kind == "paste" && Clipboard.TryGetText(out var text)) Text(text);
        else Clipboard.SetText(_text);
    }) ?? "";
    [JSExport] public static async Task<string> OpenDocument()
    {
        var result = await Documents.OpenAsync(new(".csv", 64));
        return $"{result.Status}|{result.Document?.Name}|{result.Document?.Bytes.Length}";
    }
    [JSExport] public static async Task<string> ExportDocument() => (await Documents.ExportAsync(new("../demo.csv", "text/csv", System.Text.Encoding.UTF8.GetBytes("a,b\n1,2\n")))).Status.ToString();
}
