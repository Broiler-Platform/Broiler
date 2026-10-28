using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Linq;
using System.Collections.Generic;
using Broiler.Launcher.Installation;
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
        Converters = { new JsonStringEnumConverter<BroilerAppId>(), new ScopeConverter() },
    };

    private readonly string _settingsFilePath;
    private sealed class ScopeConverter : JsonConverter<InstallScope>
    {
        public override InstallScope Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && Enum.TryParse<InstallScope>(reader.GetString(), true, out var scope)) return scope;
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int value)) return (InstallScope)value;
            using var ignored = JsonDocument.ParseValue(ref reader);
            return (InstallScope)(-1);
        }
        public override void Write(Utf8JsonWriter writer, InstallScope value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
    }

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
            var root = JsonNode.Parse(json) as JsonObject;
            var unknown = new Dictionary<string, JsonElement>();
            string? appsKey = root?.Select(x => x.Key).FirstOrDefault(x => x.Equals("Apps", StringComparison.OrdinalIgnoreCase));
            if (appsKey is not null && root![appsKey] is JsonObject apps)
                foreach (string key in apps.Select(x => x.Key).ToArray())
                    if (!Enum.TryParse<BroilerAppId>(key, true, out var id) || !Enum.IsDefined(id))
                    {
                        unknown[key] = JsonSerializer.SerializeToElement(apps[key]);
                        apps.Remove(key);
                    }
            var loaded = JsonSerializer.Deserialize<LauncherSettings>(root?.ToJsonString() ?? json, JsonOptions);
            loaded ??= new LauncherSettings();
            loaded.UnknownApps = unknown;
            loaded.Boss ??= new BossSettings();
            loaded.Updates ??= new UpdateSettings();
            loaded.Apps ??= new();
            foreach (var key in new System.Collections.Generic.List<BroilerAppId>(loaded.Apps.Keys))
                loaded.Apps[key] ??= new AppCustomSettings();
            LastLoadError = LauncherSettingsValidation.Normalize(loaded);
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
        LauncherSettingsValidation.Validate(settings);

        try
        {
            string? directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var root = JsonSerializer.SerializeToNode(settings, JsonOptions)!.AsObject();
            foreach (var (key, value) in settings.UnknownApps)
                root["Apps"]!.AsObject()[key] = JsonNode.Parse(value.GetRawText());
            string json = root.ToJsonString(JsonOptions);
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
