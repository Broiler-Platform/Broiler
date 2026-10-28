using System.Collections.Generic;
using Broiler.Graphics.Color;

namespace Broiler.Launcher.Models;

/// <summary>
/// Metadata and discovery rules for an application within the Broiler suite.
/// </summary>
public sealed record BroilerAppInfo(
    BroilerAppId Id,
    string Title,
    string ShortName,
    string Tagline,
    string Description,
    string Category,
    string BadgeText,
    BColor AccentColor,
    string WindowsExeName,
    string WindowsDllName,
    string LinuxBinaryName,
    string LinuxDllName,
    IReadOnlyList<string> RelativeProbePaths)
{
    public AppPlatformInfo? WindowsPlatform { get; init; }
    public AppPlatformInfo? LinuxPlatform { get; init; }
    public bool SupportsLinux { get; init; } = true;

    /// <summary>Explicit descriptors override legacy metadata for external catalogs.</summary>
    public AppPlatformInfo? GetPlatform(bool windows)
    {
        if (windows && WindowsPlatform is not null) return WindowsPlatform;
        if (!windows && !SupportsLinux) return null;
        if (!windows && LinuxPlatform is not null) return LinuxPlatform;
        var probes = new List<string>();
        foreach (var path in RelativeProbePaths)
        {
            string normalized = path.Replace('\\', '/');
            if (windows ? normalized.Contains(".Linux/") : normalized.Contains(".Windows/")) continue;
            probes.Add(path);
        }
        return new AppPlatformInfo(windows ? WindowsExeName : LinuxBinaryName,
            windows ? WindowsDllName : LinuxDllName, probes);
    }

    /// <summary>
    /// Gets the standard suite of Broiler desktop applications.
    /// </summary>
    public static IReadOnlyList<BroilerAppInfo> CreateDefaultSuite() =>
    [
        new BroilerAppInfo(
            Id: BroilerAppId.Browser,
            Title: "Broiler Browser",
            ShortName: "Browser",
            Tagline: "Web browser engine",
            Description: "Web browser powered by the native Broiler HTML, CSS, DOM, and JavaScript runtime.",
            Category: "Web",
            BadgeText: "WEB",
            AccentColor: BColor.FromArgb(255, 30, 136, 229), // Blue
            WindowsExeName: "Broiler.Browser.Windows.exe",
            WindowsDllName: "Broiler.Browser.Windows.dll",
            LinuxBinaryName: "Broiler.Browser.Linux",
            LinuxDllName: "Broiler.Browser.Linux.dll",
            RelativeProbePaths:
            [
                "Broiler.Browser/src/Broiler.Browser.Windows/bin/Release/net10.0-windows/Broiler.Browser.Windows.exe",
                "Broiler.Browser/src/Broiler.Browser.Windows/bin/Debug/net10.0-windows/Broiler.Browser.Windows.exe",
                "Broiler.Browser/src/Broiler.Browser.Windows/bin/Release-Windows/net10.0-windows/win-x64/Broiler.Browser.Windows.exe",
                "Broiler.Browser/src/Broiler.Browser.Linux/bin/Release/net10.0/Broiler.Browser.Linux",
                "Broiler.Browser/src/Broiler.Browser.Linux/bin/Debug/net10.0/Broiler.Browser.Linux",
            ]),

        new BroilerAppInfo(
            Id: BroilerAppId.Writer,
            Title: "Broiler Writer",
            ShortName: "Writer",
            Tagline: "Word processor & documents",
            Description: "Word processor with rich text editing, pagination, format codecs, and export capabilities.",
            Category: "Office",
            BadgeText: "DOC",
            AccentColor: BColor.FromArgb(255, 63, 81, 181), // Indigo
            WindowsExeName: "Broiler.Writer.Windows.exe",
            WindowsDllName: "Broiler.Writer.Windows.dll",
            LinuxBinaryName: "Broiler.Writer.Linux",
            LinuxDllName: "Broiler.Writer.Linux.dll",
            RelativeProbePaths:
            [
                "Broiler.Writer/src/Broiler.Writer.Windows/bin/Release/net10.0-windows/Broiler.Writer.Windows.exe",
                "Broiler.Writer/src/Broiler.Writer.Windows/bin/Debug/net10.0-windows/Broiler.Writer.Windows.exe",
                "Broiler.Writer/src/Broiler.Writer.Windows/bin/Release-Windows/net10.0-windows/win-x64/Broiler.Writer.Windows.exe",
                "Broiler.Writer/src/Broiler.Writer.Linux/bin/Release/net10.0/Broiler.Writer.Linux",
                "Broiler.Writer/src/Broiler.Writer.Linux/bin/Debug/net10.0/Broiler.Writer.Linux",
            ]),

        new BroilerAppInfo(
            Id: BroilerAppId.Cell,
            Title: "Broiler Cell",
            ShortName: "Cell",
            Tagline: "Spreadsheet & tabular data",
            Description: "Grid-based spreadsheet editor for tabular data, rapid calculation, and CSV workflow.",
            Category: "Data",
            BadgeText: "SHEET",
            AccentColor: BColor.FromArgb(255, 46, 125, 50), // Green
            WindowsExeName: "Broiler.Cells.Windows.exe",
            WindowsDllName: "Broiler.Cells.Windows.dll",
            LinuxBinaryName: "", // no Linux head
            LinuxDllName: "",
            RelativeProbePaths:
            [
                "Broiler.Cells/src/Broiler.Cells.Windows/bin/Debug/net10.0-windows/Broiler.Cells.Windows.exe",
                "Broiler.Cells/src/Broiler.Cells.Windows/bin/Release/net10.0-windows/Broiler.Cells.Windows.exe",
                "Broiler.Cells/src/Broiler.Cells.Windows/bin/Debug/net10.0-windows/Broiler.Cells.Windows.dll",
                "Broiler.Cells/src/Broiler.Cells.Windows/bin/Release/net10.0-windows/Broiler.Cells.Windows.dll",
            ]) { SupportsLinux = false },

        new BroilerAppInfo(
            Id: BroilerAppId.Plate,
            Title: "Broiler Plate",
            ShortName: "Plate",
            Tagline: "Document & image viewer",
            Description: "Fast, hardware-accelerated viewer for PDF, Markdown, and rich graphic formats.",
            Category: "Viewer",
            BadgeText: "VIEW",
            AccentColor: BColor.FromArgb(255, 230, 81, 0), // Deep Orange
            WindowsExeName: "Broiler.Plate.Windows.exe",
            WindowsDllName: "Broiler.Plate.Windows.dll",
            LinuxBinaryName: "", // no Linux head
            LinuxDllName: "",
            RelativeProbePaths:
            [
                "Broiler.Plate/src/Broiler.Plate.Windows/bin/Release/net10.0-windows/Broiler.Plate.Windows.exe",
                "Broiler.Plate/src/Broiler.Plate.Windows/bin/Debug/net10.0-windows/Broiler.Plate.Windows.exe",
                "Broiler.Plate/src/Broiler.Plate.Windows/bin/Release-Windows/net10.0-windows/win-x64/native/Broiler.Plate.Windows.exe",
                "Broiler.Plate/src/Broiler.Plate.Windows/bin/Release/net10.0-windows/Broiler.Plate.Windows.dll",
                "Broiler.Plate/src/Broiler.Plate.Windows/bin/Debug/net10.0-windows/Broiler.Plate.Windows.dll",
            ]) { SupportsLinux = false },
    ];
}

public sealed record AppPlatformInfo(string BinaryName, string DllName, IReadOnlyList<string> ProbePaths);
