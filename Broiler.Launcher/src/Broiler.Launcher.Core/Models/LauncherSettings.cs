using System.Collections.Generic;

namespace Broiler.Launcher.Models;

/// <summary>
/// Customization settings for an individual application in the Broiler suite.
/// </summary>
public sealed class AppCustomSettings
{
    /// <summary>
    /// Custom path to the application executable or assembly on disk.
    /// When specified and <see cref="UseAutoDetect"/> is false, this path takes precedence.
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Custom command-line arguments to append whenever launching this application.
    /// </summary>
    public string? Arguments { get; set; }

    /// <summary>
    /// Whether to auto-detect the executable path via the standard workspace/PATH probe pipeline.
    /// Default is true.
    /// </summary>
    public bool UseAutoDetect { get; set; } = true;

    public AppCustomSettings Clone() => new()
    {
        ExecutablePath = ExecutablePath,
        Arguments = Arguments,
        UseAutoDetect = UseAutoDetect,
    };
}

/// <summary>
/// Persistent settings configuration for Broiler Launcher.
/// </summary>
public sealed class LauncherSettings
{
    /// <summary>
    /// Per-application settings keyed by application identifier.
    /// </summary>
    public Dictionary<BroilerAppId, AppCustomSettings> Apps { get; set; } = new();

    /// <summary>
    /// Gets the custom settings for the specified app, or creates a default entry if not present.
    /// </summary>
    public AppCustomSettings GetOrCreate(BroilerAppId id)
    {
        if (!Apps.TryGetValue(id, out var settings))
        {
            settings = new AppCustomSettings();
            Apps[id] = settings;
        }

        return settings;
    }

    /// <summary>
    /// Deep-clones the settings.
    /// </summary>
    public LauncherSettings Clone()
    {
        var clone = new LauncherSettings();
        foreach (var (key, value) in Apps)
        {
            clone.Apps[key] = value.Clone();
        }

        return clone;
    }
}
