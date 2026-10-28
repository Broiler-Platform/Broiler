using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Broiler.Launcher.Installation;

public enum InstallScope { User, Machine }
public enum InstallationState { NotInstalled, Installed, Invalid }

public sealed record OfficeComponent(string Id, string Version, string Rid, string Asset, long Size,
    string Sha256, string Archive, string EntryPoint, string Invocation, string PublishMode);
public sealed record OfficeRelease(int SchemaVersion, string OfficeVersion, string Channel, string Tag,
    string Commit, string MinimumLauncherVersion, OfficeComponent[] Components)
{
    public bool Demo { get; init; }
}
public sealed record ReleaseSignature(int SchemaVersion, string KeyId, string Signature);
public sealed record VerifiedPackage(OfficeRelease Release, OfficeComponent Component, string ArchivePath);
public sealed record InstallReceipt(int SchemaVersion, string ProductId, InstallScope Scope, string Version,
    string Rid, string ReleaseTag, string Asset, string Sha256, string EntryPoint, string Invocation,
    string InstallId, DateTimeOffset InstalledAtUtc)
{
    public bool Demo { get; init; }
}
public sealed record ActiveInstallation(InstallReceipt Current, InstallReceipt? Previous);
public sealed record InstallationInfo(string ProductId, InstallScope Scope, InstallationState State,
    InstallReceipt? Receipt = null, string? ExecutablePath = null, string? Diagnostic = null);
public sealed record InstallProgress(string Stage, int? Percent = null, bool CanCancel = true);
public sealed record InstallResult(bool Succeeded, string Message, bool Canceled = false);
public sealed record InstallAvailability(bool CanInstall, string Reason, InstallationInfo Installation);

public static class OfficeJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<InstallScope>(JsonNamingPolicy.CamelCase) },
    };
}

public static class OfficeCatalog
{
    public static readonly string[] ProductIds = ["browser", "writer", "cell", "plate", "boss"];
    public static void ValidateId(string id)
    {
        if (!ProductIds.Contains(id, StringComparer.Ordinal)) throw new InvalidDataException("Unknown Office product.");
    }

    public static string EntryPoint(string id, string invocation)
    {
        if (id != "launcher") ValidateId(id);
        string name = id switch
        {
            "browser" => "Broiler.Browser.Windows", "writer" => "Broiler.Writer.Windows",
            "cell" => "Broiler.Cells.Windows", "plate" => "Broiler.Plate.Windows",
            "launcher" => "Broiler.Launcher.Windows", _ => "Broiler.Office.Server",
        };
        return name + (invocation == "dotnet" ? ".dll" : ".exe");
    }

    public static void ValidateComponent(OfficeComponent item)
    {
        if (item.Id != "launcher") ValidateId(item.Id);
        _ = OfficeVersion.Parse(item.Version);
        if (item.Version.Length > 100 || item.Rid != "win-x64" || item.Archive != "zip" ||
            item.Invocation is not ("native" or "dotnet") || item.EntryPoint != EntryPoint(item.Id, item.Invocation) ||
            item.PublishMode is not ("self-contained" or "framework-dependent" or "native-aot") ||
            item.Size <= 0 || item.Size > 2L * 1024 * 1024 * 1024 ||
            !Regex.IsMatch(item.Sha256, @"^[a-f0-9]{64}\z") ||
            !Regex.IsMatch(item.Asset, @"^[a-zA-Z0-9][a-zA-Z0-9._-]{0,180}\.zip\z"))
            throw new InvalidDataException("Invalid or unsupported Office component metadata (M2 supports Windows x64 ZIPs).");
    }
}

/// <summary>Semantic precedence; build metadata does not affect ordering.</summary>
public sealed class OfficeVersion : IComparable<OfficeVersion>
{
    private readonly string[] _core;
    private readonly string[] _pre;
    private OfficeVersion(string[] core, string[] pre) { _core = core; _pre = pre; }
    public static OfficeVersion Parse(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 100) throw new InvalidDataException("Invalid semantic version.");
        var match = Regex.Match(value, @"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z");
        if (!match.Success) throw new InvalidDataException("Invalid semantic version.");
        var pre = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : [];
        if (pre.Any(p => p.Length > 1 && p.All(char.IsAsciiDigit) && p[0] == '0'))
            throw new InvalidDataException("Numeric prerelease identifiers cannot have leading zeroes.");
        return new([match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value], pre);
    }
    private static int Numeric(string a, string b) => a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
    public int CompareTo(OfficeVersion? other)
    {
        if (other is null) return 1;
        for (int i = 0; i < 3; i++) { int n = Numeric(_core[i], other._core[i]); if (n != 0) return n; }
        if (_pre.Length == 0 || other._pre.Length == 0) return (_pre.Length == 0 ? 1 : 0).CompareTo(other._pre.Length == 0 ? 1 : 0);
        for (int i = 0; i < Math.Min(_pre.Length, other._pre.Length); i++)
        {
            bool a = _pre[i].All(char.IsAsciiDigit), b = other._pre[i].All(char.IsAsciiDigit);
            int n = a && b ? Numeric(_pre[i], other._pre[i]) : a != b ? (a ? -1 : 1) : string.CompareOrdinal(_pre[i], other._pre[i]);
            if (n != 0) return n;
        }
        return _pre.Length.CompareTo(other._pre.Length);
    }
}

/// <summary>Only compiled-in public keys are trusted. No package can supply its own trust root.</summary>
public sealed class OfficeReleaseVerifier(IReadOnlyDictionary<string, string> publicKeys, string launcherVersion, bool allowUnsignedDemo = false)
{
    public bool HasTrustedKeys => publicKeys.Count > 0;
    public bool CanReadPackages => HasTrustedKeys || allowUnsignedDemo;
    public static OfficeReleaseVerifier CreateDefault()
    {
        using var stream = typeof(OfficeReleaseVerifier).Assembly.GetManifestResourceStream("Broiler.Launcher.OfficeReleaseKeys.json")!;
        var keys = JsonSerializer.Deserialize<Dictionary<string, string>>(stream, OfficeJson.Options)!;
        string version = typeof(OfficeReleaseVerifier).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.1.0-preview.1";
        return new(keys, version, OfficeBuild.IsDemo);
    }
    public bool IsCompatible(OfficeRelease release) => OfficeVersion.Parse(release.MinimumLauncherVersion).CompareTo(OfficeVersion.Parse(launcherVersion)) <= 0;
    public OfficeRelease Verify(byte[] manifest, byte[] signatureBytes, bool allowNewerLauncher = false)
    {
        if (manifest.Length > 1024 * 1024 || signatureBytes.Length > 16 * 1024) throw new InvalidDataException("Release metadata exceeds limits.");
        if (allowUnsignedDemo && signatureBytes.Length == 0) return ValidateManifest(manifest, unsigned: true, allowNewerLauncher);
        if (!HasTrustedKeys) throw new InvalidDataException("No trusted Office release key is configured in this launcher.");
        var signature = JsonSerializer.Deserialize<ReleaseSignature>(signatureBytes, OfficeJson.Options) ?? throw new InvalidDataException("Missing release signature.");
        if (signature.SchemaVersion != 1 || !publicKeys.TryGetValue(signature.KeyId, out var pem)) throw new InvalidDataException("Untrusted release signature key.");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        if (rsa.KeySize < 3072 || !rsa.VerifyData(manifest, Convert.FromBase64String(signature.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new InvalidDataException("Office release signature verification failed.");
        return ValidateManifest(manifest, unsigned: false, allowNewerLauncher);
    }
    private OfficeRelease ValidateManifest(byte[] manifest, bool unsigned, bool allowNewerLauncher)
    {
        var release = JsonSerializer.Deserialize<OfficeRelease>(manifest, OfficeJson.Options) ?? throw new InvalidDataException("Missing release manifest.");
        if ((unsigned && !release.Demo) || (release.Demo && (!allowUnsignedDemo || release.Channel != "preview" ||
            !Regex.IsMatch(release.OfficeVersion, @"^[0-9]+\.[0-9]+\.[0-9]+-demo\.[1-9][0-9]*\z"))))
            throw new InvalidDataException("Unsigned packages require a demo launcher and an explicitly marked demo prerelease.");
        _ = OfficeVersion.Parse(release.OfficeVersion);
        if (release.SchemaVersion != 1 || release.Channel is not ("stable" or "preview") ||
            release.Tag != "office-v" + release.OfficeVersion || !Regex.IsMatch(release.Commit, @"^[a-f0-9]{40}\z") ||
            (release.Channel == "stable" && release.OfficeVersion.Split('+')[0].Contains('-')) ||
            (!IsCompatible(release) && !allowNewerLauncher) ||
            release.Components is null || release.Components.Length is < 1 or > 32)
            throw new InvalidDataException("Invalid or incompatible Office release manifest.");
        foreach (var component in release.Components) OfficeCatalog.ValidateComponent(component);
        if (release.Components.GroupBy(c => (c.Id, c.Rid)).Any(g => g.Count() != 1)) throw new InvalidDataException("Duplicate Office component.");
        return release;
    }
}

public static class OfficeBuild
{
#if BROILER_OFFICE_DEMO
    public static bool IsDemo => true;
#else
    public static bool IsDemo => false;
#endif
}
