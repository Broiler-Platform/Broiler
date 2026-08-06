using System;
using System.Threading;
using System.Threading.Tasks;

namespace Broiler.Writer;

/// <summary>Linux entry point for Broiler Writer.</summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Composition root: without a codec catalog the renderer cannot decode
        // the images a document embeds, and the editor would draw every picture
        // as an empty outline.
        Broiler.Graphics.BImageCodecs.Use(
            new Broiler.Media.MediaCodecCatalog(Broiler.Media.Image.Managed.ManagedImageCodecs.CreateCodecs()));

        LinuxWriterOptions options = LinuxWriterOptions.Parse(args);
        if (options.ShowHelp)
        {
            LinuxWriterOptions.PrintHelp();
            return 0;
        }

        if (options.OpenWindow && !OperatingSystem.IsLinux())
        {
            Console.WriteLine("Windowed Broiler Writer uses the Linux X11/OpenGL backend; running offscreen on this OS.");
            options = options with { OpenWindow = false, EnableEvdevInput = false, RunUntilClose = false };
        }

        using CancellationTokenSource shutdown = new();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        try
        {
            return await LinuxWriterRunner.RunAsync(options, shutdown.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
