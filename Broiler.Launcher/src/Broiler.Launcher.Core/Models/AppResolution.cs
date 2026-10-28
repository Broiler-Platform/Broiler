namespace Broiler.Launcher.Models;

/// <summary>
/// Result of probing the filesystem or configuration for a specific application binary.
/// </summary>
public sealed record AppResolution(
    bool IsAvailable,
    string? ResolvedPath,
    bool LaunchWithDotnet,
    string StatusText,
    bool IsCustom = false)
{
    public static AppResolution Available(string path, bool launchWithDotnet = false, string statusText = "Ready", bool isCustom = false) =>
        new(IsAvailable: true, ResolvedPath: path, LaunchWithDotnet: launchWithDotnet, StatusText: statusText, IsCustom: isCustom);

    public static AppResolution NotFound(string statusText = "Not Found") =>
        new(IsAvailable: false, ResolvedPath: null, LaunchWithDotnet: false, StatusText: statusText, IsCustom: false);
}
