using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Broiler.Launcher.Models;

namespace Broiler.Launcher.Services;

/// <summary>
/// Handles JSON persistence of launcher configuration in user AppData or a custom path.
/// </summary>
public sealed class LauncherSettingsStore : ILauncherSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter<BroilerAppId>() },
    };

    private readonly string _settingsFilePath;

    public LauncherSettingsStore(string? customPath = null)
    {
        _settingsFilePath = customPath ?? ResolveDefaultSettingsPath();
    }

    public string SettingsFilePath => _settingsFilePath;

    public string? LastLoadError { get; private set; }

    public LauncherSettings Load()
    {
        LastLoadError = null;
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new LauncherSettings();
            }

            string json = File.ReadAllText(_settingsFilePath);
            var loaded = JsonSerializer.Deserialize<LauncherSettings>(json, JsonOptions);
            loaded ??= new LauncherSettings();
            loaded.Apps ??= new();
            foreach (var key in new System.Collections.Generic.List<BroilerAppId>(loaded.Apps.Keys))
                loaded.Apps[key] ??= new AppCustomSettings();
            return loaded;
        }
        catch (Exception ex)
        {
            LastLoadError = ex.Message;
            Console.Error.WriteLine($"Launcher settings could not be loaded: {ex.Message}");
            // Fallback to default configuration on corrupted or unreadable file
            return new LauncherSettings();
        }
    }

    public void Save(LauncherSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            string? directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(settings, JsonOptions);
            string temporary = _settingsFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json);
                if (File.Exists(_settingsFilePath))
                    File.Replace(temporary, _settingsFilePath, _settingsFilePath + ".bak");
                else
                    File.Move(temporary, _settingsFilePath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save launcher settings to '{_settingsFilePath}': {ex.Message}", ex);
        }
    }

    private static string ResolveDefaultSettingsPath()
    {
        // 1. Portable mode: check next to executable
        string localPath = Path.Combine(AppContext.BaseDirectory, "launcher.settings.json");
        if (File.Exists(localPath))
        {
            return localPath;
        }

        // 2. Standard user configuration directory
        string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(baseDir))
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        string appDir = Path.Combine(baseDir, "Broiler");
        return Path.Combine(appDir, "launcher.settings.json");
    }
}
