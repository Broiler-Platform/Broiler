using System.Collections.Generic;
using Broiler.Launcher.Models;

namespace Broiler.Launcher.Services;

/// <summary>
/// Service contract for discovering and starting applications in the Broiler Platform suite.
/// </summary>
public interface IAppLauncherService
{
    /// <summary>
    /// Gets all applications managed by the launcher.
    /// </summary>
    IReadOnlyList<BroilerAppInfo> Apps { get; }

    /// <summary>
    /// Gets the current launcher settings and customization rules.
    /// </summary>
    LauncherSettings Settings { get; }

    string? SettingsLoadError => null;

    /// <summary>
    /// Updates and persists the launcher settings.
    /// </summary>
    void UpdateSettings(LauncherSettings settings);

    /// <summary>
    /// Performs auto-detection for the given application across workspace trees and PATH,
    /// ignoring any custom path overrides.
    /// </summary>
    AppResolution AutoDetect(BroilerAppInfo app, string? baseDirectory = null);

    /// <summary>
    /// Resolves the filesystem location and launch method for the given application,
    /// applying custom settings or falling back to auto-detection.
    /// </summary>
    AppResolution ResolveApp(BroilerAppInfo app, string? baseDirectory = null);

    /// <summary>
    /// Launches the given application with optional command-line arguments.
    /// Custom arguments configured in settings are automatically prepended.
    /// </summary>
    LaunchResult LaunchAppWithArguments(BroilerAppInfo app, IReadOnlyList<string> arguments, string? baseDirectory = null) =>
        LaunchApp(app, LaunchArguments.Format(arguments), baseDirectory);

    LaunchResult LaunchApp(BroilerAppInfo app, string? arguments = null, string? baseDirectory = null);
}
