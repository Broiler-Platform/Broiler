using System;
using System.Globalization;

namespace Broiler.Launcher.Linux;

/// <summary>
/// Command-line configuration for the Linux launcher host.
/// </summary>
internal sealed record LinuxLauncherOptions(
    bool OpenWindow,
    int Width,
    int Height,
    int DurationMilliseconds,
    bool RunUntilClose,
    bool EnableEvdevInput,
    bool ShowHelp,
    string? TargetApp,
    bool ListOnly)
{
    public static LinuxLauncherOptions Parse(string[] args)
    {
        bool openWindow = OperatingSystem.IsLinux();
        int width = 840;
        int height = 560;
        int durationMs = 3000;
        bool runUntilClose = openWindow;
        bool enableEvdev = true;
        bool showHelp = false;
        string? targetApp = null;
        bool listOnly = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--window":
                case "-w":
                    openWindow = true;
                    runUntilClose = true;
                    break;
                case "--offscreen":
                    openWindow = false;
                    runUntilClose = false;
                    break;
                case "--width" when i + 1 < args.Length && int.TryParse(args[++i], CultureInfo.InvariantCulture, out int w):
                    width = Math.Max(320, w);
                    break;
                case "--height" when i + 1 < args.Length && int.TryParse(args[++i], CultureInfo.InvariantCulture, out int h):
                    height = Math.Max(240, h);
                    break;
                case "--duration-ms" when i + 1 < args.Length && int.TryParse(args[++i], CultureInfo.InvariantCulture, out int d):
                    durationMs = Math.Max(100, d);
                    break;
                case "--close":
                    runUntilClose = true;
                    break;
                case "--no-input":
                    enableEvdev = false;
                    break;
                case "--app" or "-a" or "--launch" when i + 1 < args.Length:
                    targetApp = args[++i];
                    break;
                case "--list" or "-l":
                    listOnly = true;
                    break;
                case "--help" or "-h" or "-?":
                    showHelp = true;
                    break;
            }
        }

        return new LinuxLauncherOptions(
            OpenWindow: openWindow,
            Width: width,
            Height: height,
            DurationMilliseconds: durationMs,
            RunUntilClose: runUntilClose,
            EnableEvdevInput: enableEvdev,
            ShowHelp: showHelp,
            TargetApp: targetApp,
            ListOnly: listOnly);
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Broiler Launcher (Linux)");
        Console.WriteLine();
        Console.WriteLine("Usage: Broiler.Launcher.Linux [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --window, -w          Open interactive X11 window (runs until closed)");
        Console.WriteLine("  --width <px>          Viewport width (default: 840)");
        Console.WriteLine("  --height <px>         Viewport height (default: 560)");
        Console.WriteLine("  --duration-ms <ms>    Offscreen render duration before exit (default: 3000)");
        Console.WriteLine("  --no-input            Disable evdev pointer/keyboard reading");
        Console.WriteLine("  --app, -a <name>      Directly launch an application and exit");
        Console.WriteLine("  --list, -l            List all applications and their binary paths");
        Console.WriteLine("  --help, -h            Show this help text");
    }
}
