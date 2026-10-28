using Broiler.Launcher.Installation;
using Broiler.Launcher.Services;

namespace Broiler.Launcher.Models;

public static class LauncherSettingsValidation
{
    public static string? Normalize(LauncherSettings settings)
    {
        var warnings = new List<string>();
        settings.Apps ??= new();
        settings.Boss ??= new();
        settings.Updates ??= new();
        if (settings.SchemaVersion > LauncherSettings.CurrentSchemaVersion) warnings.Add("Settings belong to a newer launcher and cannot be overwritten by this version.");
        else settings.SchemaVersion = LauncherSettings.CurrentSchemaVersion;
        if (!Enum.IsDefined(settings.PreferredInstallScope)) { settings.PreferredInstallScope = InstallScope.User; warnings.Add("Unknown install scope reset to user."); }
        foreach (var id in settings.Apps.Keys.ToArray())
        {
            settings.Apps[id] ??= new();
            if (!Enum.IsDefined(settings.Apps[id].PreferredInstalledScope)) { settings.Apps[id].PreferredInstalledScope = InstallScope.User; warnings.Add($"Unknown {id} scope reset to user."); }
        }
        if (settings.Updates.Channel is not ("stable" or "preview")) { settings.Updates.Channel = new UpdateSettings().Channel; warnings.Add("Unknown update channel reset to its default."); }
        if (settings.Updates.CheckIntervalHours is < 1 or > 168) { settings.Updates.CheckIntervalHours = 24; warnings.Add("Update interval reset to 24 hours."); }
        try { settings.Boss.Validate(); }
        catch { settings.Boss.ListenUrl = "http://127.0.0.1:5555/"; settings.Boss.StartupTimeoutSeconds = 30; settings.Boss.AdditionalArguments = null; warnings.Add("Invalid BOSS listener, timeout or arguments reset to safe defaults."); }
        return warnings.Count == 0 ? null : string.Join(" ", warnings);
    }

    public static void Validate(LauncherSettings settings)
    {
        if (settings.SchemaVersion > LauncherSettings.CurrentSchemaVersion) throw new InvalidOperationException("These settings were created by a newer launcher. Use that launcher to edit them.");
        if (!Enum.IsDefined(settings.PreferredInstallScope)) throw new ArgumentException("Select an installation scope.");
        if (settings.Updates.Channel is not ("stable" or "preview") || settings.Updates.CheckIntervalHours is < 1 or > 168)
            throw new ArgumentException("Updates require stable/preview and an interval from 1 to 168 hours.");
        settings.Boss.Validate();
        foreach (var app in settings.Apps.Values)
        {
            if (!Enum.IsDefined(app.PreferredInstalledScope)) throw new ArgumentException("Select a preferred installed scope.");
            _ = LaunchArguments.Parse(app.Arguments);
        }
    }
}
