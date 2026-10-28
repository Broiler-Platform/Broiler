using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Broiler.Launcher.Installation;

namespace Broiler.Launcher.Models;

/// <summary>
/// Customization settings for an individual application in the Broiler suite.
/// </summary>
public sealed class AppCustomSettings
{
    public InstallScope PreferredInstalledScope { get; set; } = InstallScope.User;
    public bool ShowInstallActions { get; set; } = true;
    public bool ShowUpdateActions { get; set; } = true;
    public bool ShowBossAction { get; set; } = true;
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
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
        PreferredInstalledScope = PreferredInstalledScope,
        ShowInstallActions = ShowInstallActions,
        ShowUpdateActions = ShowUpdateActions,
        ShowBossAction = ShowBossAction,
        Extra = Extra?.ToDictionary(x => x.Key, x => x.Value.Clone()),
    };
}

/// <summary>
/// Persistent settings configuration for Broiler Launcher.
/// </summary>
public sealed class LauncherSettings
{
    public const int CurrentSchemaVersion = 2;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public InstallScope PreferredInstallScope { get; set; } = InstallScope.User;
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    [JsonIgnore] public Dictionary<string, JsonElement> UnknownApps { get; set; } = new();
    public BossSettings Boss { get; set; } = new();
    public UpdateSettings Updates { get; set; } = new();
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
        var clone = new LauncherSettings { SchemaVersion = SchemaVersion, PreferredInstallScope = PreferredInstallScope,
            Extra = Extra?.ToDictionary(x => x.Key, x => x.Value.Clone()), UnknownApps = UnknownApps.ToDictionary(x => x.Key, x => x.Value.Clone()),
            Boss = (Boss ?? new BossSettings()).Clone(), Updates = (Updates ?? new UpdateSettings()).Clone() };
        foreach (var (key, value) in Apps)
        {
            clone.Apps[key] = value.Clone();
        }

        return clone;
    }
}
