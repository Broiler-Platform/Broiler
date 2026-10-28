using System.Text.Json;
using System.Text.RegularExpressions;

namespace Broiler.Launcher.Installation;

public sealed class OfficeInstallPaths(string userRoot, string machineRoot)
{
    public string UserRoot { get; } = Path.GetFullPath(userRoot);
    public string MachineRoot { get; } = Path.GetFullPath(machineRoot);
    public static OfficeInstallPaths CreateDefault()
    {
        string user = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string machine = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!Path.IsPathFullyQualified(user) || !Path.IsPathFullyQualified(machine))
            throw new InvalidOperationException("The operating system installation directories are unavailable.");
        return new(Path.Combine(user, "Broiler", "Office"), Path.Combine(machine, "Broiler", "Office"));
    }
    public string Root(InstallScope scope) => scope switch
    {
        InstallScope.User => UserRoot, InstallScope.Machine => MachineRoot,
        _ => throw new InvalidDataException("Invalid installation scope."),
    };
    public string Record(string id, InstallScope scope)
    {
        OfficeCatalog.ValidateId(id);
        return Path.Combine(Root(scope), "inventory", id + ".json");
    }
    public string VersionDirectory(InstallReceipt receipt)
    {
        OfficeCatalog.ValidateId(receipt.ProductId);
        _ = OfficeVersion.Parse(receipt.Version);
        // Version metadata cannot become drive syntax, streams, traversal, or a reserved path segment.
        if (!Regex.IsMatch(receipt.InstallId, @"^[a-f0-9]{32}\z")) throw new InvalidDataException("Invalid installation ID.");
        return Path.Combine(Root(receipt.Scope), "products", receipt.ProductId, "versions", receipt.Version, receipt.InstallId);
    }

    public static void RejectLinks(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Links and reparse points are not allowed in Office installation paths.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    public static byte[] ReadBounded(string path, int limit)
    {
        RejectLinks(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > limit) throw new InvalidDataException("Office metadata exceeds its size limit.");
        byte[] bytes = new byte[checked((int)file.Length)];
        file.ReadExactly(bytes);
        return bytes;
    }
}

public interface IInstallationInventory
{
    InstallationInfo Find(string productId, InstallScope scope);
    IReadOnlyList<InstallationInfo> Enumerate(string productId) => [Find(productId, InstallScope.User), Find(productId, InstallScope.Machine)];
}

public sealed class InstallationInventory(OfficeInstallPaths paths, Func<string, bool>? machinePathTrust = null) : IInstallationInventory
{
    public IReadOnlyList<InstallationInfo> Enumerate(string productId) => [Find(productId, InstallScope.User), Find(productId, InstallScope.Machine)];
    private bool IsMachinePathTrusted(string path) => machinePathTrust?.Invoke(path) ??
        (OperatingSystem.IsWindows() && WindowsInstallSecurity.IsProtected(path));
    public InstallationInfo Find(string productId, InstallScope scope)
    {
        try
        {
            string recordPath = paths.Record(productId, scope);
            OfficeInstallPaths.RejectLinks(recordPath);
            if (!File.Exists(recordPath)) return new(productId, scope, InstallationState.NotInstalled);
            if (scope == InstallScope.Machine && !IsMachinePathTrusted(recordPath))
                throw new InvalidDataException("The machine installation record is not protected from ordinary user writes.");
            var active = ReadActive(productId, scope);
            string executable = ValidateReceipt(active.Current, productId, scope);
            return new(productId, scope, InstallationState.Installed, active.Current, executable);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NullReferenceException)
        {
            return new(productId, scope, InstallationState.Invalid, Diagnostic: ex.Message);
        }
    }

    internal ActiveInstallation ReadActive(string id, InstallScope scope) =>
        JsonSerializer.Deserialize<ActiveInstallation>(OfficeInstallPaths.ReadBounded(paths.Record(id, scope), 64 * 1024), OfficeJson.Options)
        ?? throw new InvalidDataException("Missing active Office receipt.");

    internal string ValidateReceipt(InstallReceipt receipt, string id, InstallScope scope)
    {
        if (receipt.SchemaVersion != 1 || receipt.ProductId != id || receipt.Scope != scope || receipt.InstalledAtUtc == default ||
            receipt.ReleaseTag.Length > 128 || !receipt.ReleaseTag.StartsWith("office-v", StringComparison.Ordinal))
            throw new InvalidDataException("Invalid installed Office receipt.");
        OfficeCatalog.ValidateComponent(new(id, receipt.Version, receipt.Rid, receipt.Asset, 1,
            receipt.Sha256, "zip", receipt.EntryPoint, receipt.Invocation, "self-contained"));
        string versionDir = paths.VersionDirectory(receipt);
        OfficeInstallPaths.RejectLinks(versionDir);
        var saved = JsonSerializer.Deserialize<InstallReceipt>(OfficeInstallPaths.ReadBounded(Path.Combine(versionDir, ".office-receipt.json"), 64 * 1024), OfficeJson.Options);
        if (saved != receipt) throw new InvalidDataException("The active record and version receipt disagree.");
        string executable = Path.Combine(versionDir, receipt.EntryPoint);
        OfficeInstallPaths.RejectLinks(executable);
        if (!File.Exists(executable)) throw new InvalidDataException("The installed Office entry point is missing.");
        if (scope == InstallScope.Machine && (!IsMachinePathTrusted(executable) || !IsMachinePathTrusted(Path.Combine(versionDir, ".office-receipt.json"))))
            throw new InvalidDataException("The machine product files are not protected from ordinary user writes.");
        if (id == "boss" && !File.Exists(Path.Combine(versionDir, "wwwroot", "index.html")))
            throw new InvalidDataException("The BOSS web assets are missing.");
        return executable;
    }
}

/// <summary>A bundled ZIP source. Network download/selection is owned by later release/update milestones.</summary>
public sealed class BundledOfficePackageSource(string directory, OfficeReleaseVerifier verifier)
{
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public bool HasTrustedKeys => verifier.HasTrustedKeys;
    public static BundledOfficePackageSource CreateDefault()
    {
        string directory = AppContext.BaseDirectory;
        if (!File.Exists(Path.Combine(directory, "office-release.json")) &&
            Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)).Equals("launcher", StringComparison.OrdinalIgnoreCase))
            directory = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(directory))!;
        return new(directory, OfficeReleaseVerifier.CreateDefault());
    }
    public VerifiedPackage Read(string productId)
    {
        OfficeCatalog.ValidateId(productId);
        if (!verifier.CanReadPackages) throw new InvalidDataException("No trusted Office release key is configured in this launcher.");
        byte[] manifest = OfficeInstallPaths.ReadBounded(Path.Combine(DirectoryPath, "office-release.json"), 1024 * 1024);
        string signaturePath = Path.Combine(DirectoryPath, "office-release.signature.json");
        byte[] signature = File.Exists(signaturePath) ? OfficeInstallPaths.ReadBounded(signaturePath, 16 * 1024) : [];
        var release = verifier.Verify(manifest, signature);
        var component = release.Components.SingleOrDefault(c => c.Id == productId)
            ?? throw new InvalidDataException("This Office package does not contain the selected product.");
        string archive = Path.Combine(DirectoryPath, "packages", component.Asset);
        OfficeInstallPaths.RejectLinks(archive);
        if (release.Demo && !File.Exists(archive)) throw new InvalidDataException("Demo installation requires the bundled local archive.");
        return new(release, component, archive);
    }
}
