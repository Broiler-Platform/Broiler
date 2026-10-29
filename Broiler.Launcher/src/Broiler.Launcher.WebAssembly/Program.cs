using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Broiler.Launcher.WebAssembly;

[SupportedOSPlatform("browser")]
internal static class Program
{
    private static async Task Main()
    {
        try { await LauncherApp.StartAsync(); }
        catch (Exception error) { BrowserInterop.Failed(error.Message); }
        await Task.Delay(Timeout.Infinite);
    }
}
[SupportedOSPlatform("browser")]
internal static partial class BrowserInterop
{
    [JSImport("launcher.invalidate", "main.js")] internal static partial void Invalidate();
    [JSImport("launcher.ready", "main.js")] internal static partial void Ready(string summary);
    [JSImport("launcher.failed", "main.js")] internal static partial void Failed(string message);
    [JSImport("launcher.navigate", "main.js")] internal static partial void Navigate(string url, bool newTab);
    [JSImport("launcher.readPreference", "main.js")] internal static partial string ReadPreference(string key);
    [JSImport("launcher.writePreference", "main.js")] internal static partial void WritePreference(string key, string value);
    [JSImport("launcher.applyTheme", "main.js")] internal static partial void ApplyTheme(string theme);
    [JSImport("launcher.metadata", "main.js")] internal static partial string Metadata();
}
