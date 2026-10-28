using Broiler.Launcher.Models;

namespace Broiler.Launcher.Services;

/// <summary>
/// Contract for loading and persisting launcher configuration.
/// </summary>
public interface ILauncherSettingsStore
{
    /// <summary>
    /// Gets the absolute path of the configuration file.
    /// </summary>
    string SettingsFilePath { get; }

    string? LastLoadError => null;

    /// <summary>
    /// Loads settings from disk or returns default configuration if not found.
    /// </summary>
    LauncherSettings Load();

    /// <summary>
    /// Persists settings to disk.
    /// </summary>
    void Save(LauncherSettings settings);
}
