using System;
using System.Threading;
using System.Threading.Tasks;

namespace Broiler.UI.WebAssembly.Demo;

internal static class Program
{
    public static async Task Main()
    {
        try
        {
            await BrowserGalleryApp.StartAsync();
        }
        catch (Exception exception)
        {
            Environment.ExitCode = 1;
            BrowserInterop.Failed(exception.GetType().Name, exception.ToString());
        }

        // Keep the managed runtime resident so the page can drive input, resize, and render frames.
        await Task.Delay(Timeout.InfiniteTimeSpan);
    }
}
