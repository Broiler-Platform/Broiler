using Broiler.Launcher.Installation;

namespace Broiler.Launcher.Models;

public sealed class UpdateSettings
{
    public int CheckIntervalHours { get; set; } = 24;
    [System.Text.Json.Serialization.JsonExtensionData] public Dictionary<string, System.Text.Json.JsonElement>? Extra { get; set; }
    public bool CheckOnStartup { get; set; } = true;
    public string Channel { get; set; } = OfficeBuild.IsDemo ? "preview" : "stable";
    public UpdateSettings Clone() { var copy = (UpdateSettings)MemberwiseClone(); copy.Extra = Extra?.ToDictionary(x => x.Key, x => x.Value.Clone()); return copy; }
}
