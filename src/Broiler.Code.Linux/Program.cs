using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Broiler.App;
using Broiler.Code.Core.Hosting;

namespace Broiler.Code.Linux;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // --services prints what the head provides and exits. It is how the
        // support claim is checked from a script or a test without opening a
        // window, and it prints substitutes and gaps as loudly as it prints
        // what works.
        if (Array.IndexOf(args, "--services") >= 0)
        {
            // Probed rather than assumed: --services must answer for this
            // machine, and whether a selection can be owned depends on whether
            // there is a display to own it on.
            using LinuxX11Clipboard? clipboard = LinuxX11Clipboard.TryOpen();
            HostServiceReport report = CodeHost.DescribeServices(new LinuxFileDialogs().Helper, clipboard is not null);
            Console.WriteLine(report.Describe());

            IReadOnlyList<string> unavailable = CodeHost.UnavailableCommands(report);
            if (unavailable.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"Disabled on this host: {string.Join(", ", unavailable)}");
            }

            // Exit code reflects substitutes only. An unavailable service is a
            // stated gap; a substitute reported as support is the thing this
            // check exists to catch.
            return report.HasNoSubstitutes ? 0 : 1;
        }

        // evdev is a global device stream, so input is normally read only while
        // the X11 window has focus. --ignore-focus lifts that for a test
        // harness driving the window without a focused display.
        bool ignoreFocus = Array.IndexOf(args, "--ignore-focus") >= 0;
        string? workspacePath = Array.Find(args, argument => !argument.StartsWith('-'));

        using var lifetime = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            lifetime.Cancel();
        };

        try
        {
            return await CodeHost.RunAsync(workspacePath, ignoreFocus, lifetime.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
    }
}
