using System;
using System.Globalization;

namespace Broiler.UI.Linux.Demo;

internal sealed record LinuxUiDemoOptions(
    bool OpenWindow,
    bool EnableEvdevInput,
    bool RunUntilClose,
    int DurationMilliseconds,
    int Width,
    int Height,
    string? ArtifactDirectory,
    bool IgnoreInputFocus,
    bool ShowHelp)
{
    public static LinuxUiDemoOptions Parse(string[] args)
    {
        bool openWindow = false;
        bool enableEvdevInput = false;
        bool runUntilClose = false;
        bool ignoreInputFocus = false;
        bool showHelp = false;
        int? durationMilliseconds = null;
        int width = 900;
        int height = 600;
        string? artifactDirectory = null;

        foreach (string arg in args)
        {
            if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-h", StringComparison.OrdinalIgnoreCase))
            {
                showHelp = true;
            }
            else if (arg.Equals("--window", StringComparison.OrdinalIgnoreCase))
            {
                openWindow = true;
            }
            else if (arg.Equals("--enable-evdev-input", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--input", StringComparison.OrdinalIgnoreCase))
            {
                enableEvdevInput = true;
            }
            else if (arg.Equals("--interactive", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--until-close", StringComparison.OrdinalIgnoreCase))
            {
                runUntilClose = true;
            }
            else if (arg.Equals("--input-ignore-focus", StringComparison.OrdinalIgnoreCase))
            {
                ignoreInputFocus = true;
            }
            else if (arg.StartsWith("--duration-ms=", StringComparison.OrdinalIgnoreCase))
            {
                string value = arg["--duration-ms=".Length..];
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0)
                    durationMilliseconds = parsed;
            }
            else if (arg.StartsWith("--artifact-dir=", StringComparison.OrdinalIgnoreCase))
            {
                artifactDirectory = arg["--artifact-dir=".Length..];
            }
            else if (arg.StartsWith("--size=", StringComparison.OrdinalIgnoreCase))
            {
                ParseSize(arg["--size=".Length..], ref width, ref height);
            }
            else if (arg.StartsWith("--width=", StringComparison.OrdinalIgnoreCase))
            {
                string value = arg["--width=".Length..];
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0)
                    width = parsed;
            }
            else if (arg.StartsWith("--height=", StringComparison.OrdinalIgnoreCase))
            {
                string value = arg["--height=".Length..];
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0)
                    height = parsed;
            }
        }

        // A windowed run with no explicit smoke duration should behave like a
        // real application window: stay open until the user closes it, presses
        // Escape, or Ctrl+C. Only fall back to a bounded smoke run when the
        // caller asks for one with --duration-ms (or when running offscreen).
        if (openWindow && !runUntilClose && !durationMilliseconds.HasValue)
            runUntilClose = true;

        int defaultDuration = openWindow && enableEvdevInput ? 10_000 : 2_000;
        return new LinuxUiDemoOptions(
            openWindow,
            enableEvdevInput,
            runUntilClose,
            durationMilliseconds ?? defaultDuration,
            Math.Clamp(width, 320, 3840),
            Math.Clamp(height, 240, 2160),
            string.IsNullOrWhiteSpace(artifactDirectory) ? null : artifactDirectory,
            ignoreInputFocus,
            showHelp);
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Broiler.UI Linux Demo");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --window                 Open an X11/OpenGL window on Linux (stays open until closed).");
        Console.WriteLine("  --input                  Enable first-round evdev keyboard/mouse input.");
        Console.WriteLine("  --interactive            Run until Escape, close, or Ctrl+C.");
        Console.WriteLine("  --input-ignore-focus     Keep evdev input active even when the X11 window is not focused.");
        Console.WriteLine("  --duration-ms=<value>    Close the window after this many ms (bounded smoke run).");
        Console.WriteLine("  --size=<width>x<height>  Logical viewport size. Default: 900x600.");
        Console.WriteLine("  --artifact-dir=<path>    Save backend and CPU comparison PNG artifacts.");
    }

    private static void ParseSize(string value, ref int width, ref int height)
    {
        string[] parts = value.Split(['x', 'X'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return;

        if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedWidth) && parsedWidth > 0)
            width = parsedWidth;
        if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedHeight) && parsedHeight > 0)
            height = parsedHeight;
    }
}
