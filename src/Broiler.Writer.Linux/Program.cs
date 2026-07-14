using System;
using System.Threading;
using System.Threading.Tasks;

namespace Broiler.Writer;

/// <summary>Linux entry point for Broiler Writer.</summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
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
