using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Broiler.Launcher.Cli;

namespace Broiler.Launcher.Windows;

[SupportedOSPlatform("windows7.0")]
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (WindowsMachineInstallBroker.TryHandle(args, out int machineExitCode)) return machineExitCode;
        _ = SetProcessDpiAwarenessContext(new nint(-4)); // PER_MONITOR_AWARE_V2

        // CLI support for directly launching apps or listing apps
        if (args.Length > 0 && LauncherCliHandler.TryHandleCli(args, out int exitCode))
        {
            return exitCode;
        }

        try
        {
            using var window = new LauncherWindow();
            return window.Run();
        }
        catch (Exception error)
        {
            _ = MessageBox(0, error.ToString(), "Broiler Launcher", 0x10);
            return 1;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(nint context);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(nint owner, string text, string caption, uint type);
}
