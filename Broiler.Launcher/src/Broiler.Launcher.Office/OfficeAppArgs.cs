using System;
using System.Globalization;
using System.IO;

namespace Broiler.Launcher.Office;

/// <summary>
/// Parses common command-line arguments passed to built-in Broiler Office applications.
/// </summary>
internal sealed record OfficeAppOptions(
    bool ShowHelp,
    bool OpenWindow,
    bool EnableEvdevInput,
    bool RunUntilClose,
    bool IgnoreInputFocus,
    int Width,
    int Height,
    double DurationMilliseconds,
    string? InitialPath,
    string? DataDirectory,
    bool SmokeTest)
{
    public static OfficeAppOptions Parse(string[] args, int defaultWidth = 1120, int defaultHeight = 760)
    {
        bool showHelp = false;
        bool openWindow = true;
        bool enableEvdev = true;
        bool runUntilClose = true;
        bool ignoreInputFocus = false;
        bool smokeTest = false;
        int width = defaultWidth;
        int height = defaultHeight;
        double durationMs = 2000;
        string? initialPath = null;
        string? dataDirectory = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--help" or "-h" or "-?":
                    showHelp = true;
                    break;
                case "--offscreen":
                    openWindow = false;
                    enableEvdev = false;
                    runUntilClose = false;
                    break;
                case "--window":
                    openWindow = true;
                    break;
                case "--no-evdev":
                    enableEvdev = false;
                    break;
                case "--ignore-focus":
                    ignoreInputFocus = true;
                    break;
                case "--smoke-test":
                    smokeTest = true;
                    break;
                case "--width" when i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) && w > 0:
                    width = w;
                    i++;
                    break;
                case "--height" when i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) && h > 0:
                    height = h;
                    i++;
                    break;
                case "--duration" when i + 1 < args.Length && double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && d > 0:
                    durationMs = d;
                    runUntilClose = false;
                    i++;
                    break;
                case "--data-directory" when i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]):
                    dataDirectory = Path.GetFullPath(args[++i]);
                    break;
                default:
                    if (!arg.StartsWith('-') && initialPath is null)
                        initialPath = arg;
                    break;
            }
        }

        return new OfficeAppOptions(
            ShowHelp: showHelp,
            OpenWindow: openWindow,
            EnableEvdevInput: enableEvdev,
            RunUntilClose: runUntilClose,
            IgnoreInputFocus: ignoreInputFocus,
            Width: width,
            Height: height,
            DurationMilliseconds: durationMs,
            InitialPath: initialPath,
            DataDirectory: dataDirectory,
            SmokeTest: smokeTest);
    }
}
