using System.Text.Json.Serialization;
namespace Broiler.Launcher.Installation;
[JsonSerializable(typeof(OfficeRelease))]
[JsonSerializable(typeof(ReleaseSignature))]
[JsonSerializable(typeof(InstallReceipt))]
[JsonSerializable(typeof(ActiveInstallation))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, InstallReceipt>))]
[JsonSerializable(typeof(OfficeReleaseCatalog.CachePage))]
[JsonSerializable(typeof(OfficeReleaseCatalog.CheckState))]
internal partial class OfficeJsonContext : JsonSerializerContext { }
