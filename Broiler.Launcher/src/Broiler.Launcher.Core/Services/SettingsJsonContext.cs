using System.Text.Json.Serialization;
using Broiler.Launcher.Models;
namespace Broiler.Launcher.Services;
[JsonSerializable(typeof(LauncherSettings))]
internal partial class SettingsJsonContext : JsonSerializerContext { }
