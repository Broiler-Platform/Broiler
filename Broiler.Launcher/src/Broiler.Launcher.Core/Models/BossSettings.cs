namespace Broiler.Launcher.Models;

public sealed class BossSettings
{
    public string? AdditionalArguments { get; set; }
    [System.Text.Json.Serialization.JsonExtensionData] public Dictionary<string, System.Text.Json.JsonElement>? Extra { get; set; }
    public bool UseAutoDetect { get; set; } = true;
    public string? ExecutablePath { get; set; }
    public string ListenUrl { get; set; } = "http://127.0.0.1:5555/";
    public int StartupTimeoutSeconds { get; set; } = 30;
    public bool OpenBrowserOnStart { get; set; }
    public bool KeepRunningOnLauncherExit { get; set; } = true;
    public BossSettings Clone() { var copy = (BossSettings)MemberwiseClone(); copy.Extra = Extra?.ToDictionary(x => x.Key, x => x.Value.Clone()); return copy; }

    public IReadOnlyList<string> ArgumentTokens()
    {
        var tokens = Services.LaunchArguments.Parse(AdditionalArguments);
        for (int i = 0; i < tokens.Count; i += 2)
            if (i + 1 >= tokens.Count || !System.Text.RegularExpressions.Regex.IsMatch(tokens[i], @"^--Logging:LogLevel:[A-Za-z0-9_.]+\z") ||
                tokens[i + 1] is not ("Trace" or "Debug" or "Information" or "Warning" or "Error" or "Critical" or "None"))
                throw new ArgumentException("BOSS arguments accept logging level pairs only, for example --Logging:LogLevel:Default Warning. Configure the listener with its URL field.");
        return tokens;
    }

    public Uri Validate()
    {
        _ = ArgumentTokens();
        if (!Uri.TryCreate(ListenUrl, UriKind.Absolute, out var uri) || uri.Scheme != "http" || !uri.IsLoopback ||
            uri.Port < 1 || uri.AbsolutePath != "/" || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            StartupTimeoutSeconds is < 1 or > 120)
            throw new ArgumentException("BOSS requires a loopback HTTP root URL and a startup timeout between 1 and 120 seconds.");
        return uri;
    }
}
