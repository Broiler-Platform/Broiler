using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Broiler.Launcher.Cli;
using Broiler.Native.Windows;

namespace Broiler.Launcher.Windows;

[SupportedOSPlatform("windows7.0")]
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (WindowsMachineInstallBroker.TryHandle(args, out int machineExitCode)) return machineExitCode;
        _ = WindowNative.SetProcessDpiAwarenessContext(new nint(-4)); // PER_MONITOR_AWARE_V2

#if BROILER_OFFICE_ALL_IN_ONE
        Broiler.Launcher.Windows.Office.OfficeAllInOneBootstrap.Register();
#endif

        // CLI support for directly launching apps or listing apps
        if (args.Length > 0 && LauncherCliHandler.TryHandleCli(args, out int exitCode))
        {
            return exitCode;
        }

        try
        {
#if BROILER_OFFICE_ALL_IN_ONE
            return Broiler.Launcher.Windows.Office.OfficeWindowCoordinator.Run(
                static () => new LauncherWindow(ownsMessageLoop: false));
#else
            using var window = new LauncherWindow();
            return window.Run();
#endif
        }
        catch (Exception error)
        {
            _ = WindowNative.MessageBox(0, error.ToString(), "Broiler Launcher", 0x10);
            return 1;
        }
    }
}
