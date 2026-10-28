using System;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Launcher.Cli;

namespace Broiler.Launcher.Linux;

/// <summary>
/// Entry point for Broiler Launcher on Linux.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        LinuxLauncherOptions options = LinuxLauncherOptions.Parse(args);
        if (options.ShowHelp)
        {
            LinuxLauncherOptions.PrintHelp();
            return 0;
        }

        if (LauncherCliHandler.TryHandleCli(args, out int exitCode))
            return exitCode;

        if (options.OpenWindow && !OperatingSystem.IsLinux())
        {
            Console.WriteLine("X11/OpenGL window is only supported on Linux; running offscreen.");
            options = options with { OpenWindow = false, EnableEvdevInput = false, RunUntilClose = false };
        }

        using CancellationTokenSource shutdown = new();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        UiThreadSynchronizationContext.Run(() => LinuxLauncherRunner.RunAsync(options, shutdown.Token));
        return 0;
    }
}
