using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Broiler.Launcher.Installation;

public interface IInstallAccess
{
    void Prepare(InstallScope scope, OfficeInstallPaths paths);
    void ValidateDestination(string path) { }
    void CreateStaging(string path) => Directory.CreateDirectory(path);
    void PrepareActivation(string payload) { }
}

public sealed class UserInstallAccess : IInstallAccess
{
    public void Prepare(InstallScope scope, OfficeInstallPaths paths)
    {
        if (scope != InstallScope.User) throw new UnauthorizedAccessException("Machine installation requires the administrator helper.");
        OfficeInstallPaths.RejectLinks(paths.Root(scope));
        Directory.CreateDirectory(paths.Root(scope));
    }
}

/// <summary>All writes are confined to a validated scope root; activation never overwrites a live payload.</summary>
public sealed class ProductInstaller(OfficeInstallPaths paths, IInstallAccess access)
{
    private const long MaxExpandedBytes = 4L * 1024 * 1024 * 1024;
    private const int MaxEntries = 100_000;

    public async Task<InstallResult> InstallAsync(VerifiedPackage package, InstallScope scope,
        IProgress<InstallProgress>? progress = null, CancellationToken cancellationToken = default, bool repair = false,
        InstallReceipt? expected = null, Func<string?>? activationGuard = null)
    {
        string? staging = null;
        try
        {
            var item = package.Component;
            OfficeCatalog.ValidateId(item.Id); // Self-update is a separate bootstrap operation, not an M2 product install.
            OfficeCatalog.ValidateComponent(item);
            cancellationToken.ThrowIfCancellationRequested();
            access.Prepare(scope, paths);
            string root = paths.Root(scope);
            OfficeInstallPaths.RejectLinks(root);
            using var operationLock = AcquireLock(root, item.Id);
            var inventory = new InstallationInventory(paths);
            var installed = inventory.Find(item.Id, scope);
            string? incompatible = OfficeCompatibility.BlockingReason(package.Release, item.Id, scope, inventory);
            if (incompatible is not null) throw new InvalidOperationException(incompatible);
            if (expected is not null && (installed.State != InstallationState.Installed || installed.Receipt != expected ||
                expected.Rid != item.Rid || OfficeVersion.Parse(item.Version).CompareTo(OfficeVersion.Parse(expected.Version)) <= 0))
                throw new InvalidOperationException("The installation changed or this is not a newer compatible update. Check for updates again.");
            if (!repair && expected is null && installed.State != InstallationState.NotInstalled)
                throw new InvalidOperationException(installed.State == InstallationState.Installed
                    ? "This product is already installed in the selected scope. Use repair explicitly."
                    : "The installation record needs repair before installation can continue.");
            if (installed.Receipt is { } current && OfficeVersion.Parse(item.Version).CompareTo(OfficeVersion.Parse(current.Version)) < 0)
                throw new InvalidOperationException("Installing an older version is not allowed.");

            staging = Path.Combine(root, "staging", Guid.NewGuid().ToString("N"));
            OfficeInstallPaths.RejectLinks(staging);
            access.ValidateDestination(staging);
            access.CreateStaging(staging);
            string snapshot = Path.Combine(staging, "payload.zip");
            progress?.Report(new("Verifying package", 0));
            OfficeInstallPaths.RejectLinks(package.ArchivePath);
            await using (var source = new FileStream(package.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            await using (var target = new FileStream(snapshot, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, true))
            {
                if (source.Length != item.Size) throw new InvalidDataException("Package size does not match the signed manifest.");
                await CopyLimitedAsync(source, target, item.Size, cancellationToken);
                target.Position = 0;
                string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(target, cancellationToken));
                if (hash != item.Sha256) throw new InvalidDataException("Package hash does not match the signed manifest.");
            }
            string payload = Path.Combine(staging, "product");
            Directory.CreateDirectory(payload);
            await ExtractAsync(snapshot, payload, progress, cancellationToken);
            if (!File.Exists(Path.Combine(payload, item.EntryPoint)) ||
                (item.Invocation == "dotnet" && !File.Exists(Path.Combine(payload, Path.ChangeExtension(item.EntryPoint, ".runtimeconfig.json")))))
                throw new InvalidDataException("The product entry point or .NET runtime configuration is missing.");
            if (item.Id == "boss" && !File.Exists(Path.Combine(payload, "wwwroot", "index.html")))
                throw new InvalidDataException("The BOSS web bundle is missing.");
            var receipt = new InstallReceipt(1, item.Id, scope, item.Version, item.Rid, package.Release.Tag,
                item.Asset, item.Sha256, item.EntryPoint, item.Invocation, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow) { Demo = package.Release.Demo };
            await File.WriteAllTextAsync(Path.Combine(payload, ".office-receipt.json"), JsonSerializer.Serialize(receipt, OfficeJson.Options), cancellationToken);
            access.PrepareActivation(payload);
            string destination = paths.VersionDirectory(receipt);
            OfficeInstallPaths.RejectLinks(destination);
            access.ValidateDestination(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            cancellationToken.ThrowIfCancellationRequested();
            // Extractions remain independent. Serialize only the final activation
            // across products so stable cohort checks cannot race another commit.
            using var scopeActivation = AcquireLock(root, "suite-activation");
            incompatible = OfficeCompatibility.BlockingReason(package.Release, item.Id, scope, inventory);
            if (incompatible is not null) throw new InvalidOperationException(incompatible);
            string? activationBlocked = activationGuard?.Invoke();
            if (activationBlocked is not null) throw new InvalidOperationException(activationBlocked);
            progress?.Report(new("Activating installation", 100, false));
            // From here commit is deliberately non-cancellable. An interrupted commit leaves an
            // unreferenced complete version, never a partially updated active product.
            Directory.Move(payload, destination);
            WriteActive(paths.Record(item.Id, scope), new(receipt, installed.Receipt));
            return new(true, $"Installed {item.Id} {item.Version} for {(scope == InstallScope.User ? "user" : "all users")}.");
        }
        catch (OperationCanceledException) { return new(false, "Installation canceled; the previous installation is unchanged.", true); }
        catch (Exception ex) { return new(false, $"Installation failed: {ex.Message}"); }
        finally
        {
            // Only delete our own random stage, after checking every existing ancestor again.
            if (staging is not null)
            {
                try { OfficeInstallPaths.RejectLinks(staging); Directory.Delete(staging, true); }
                catch (IOException) { }
                catch (InvalidDataException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static FileStream AcquireLock(string root, string id)
    {
        string path = Path.Combine(root, "locks", id + ".lock");
        OfficeInstallPaths.RejectLinks(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    internal static void WriteActive(string record, ActiveInstallation active)
    {
        OfficeInstallPaths.RejectLinks(record);
        Directory.CreateDirectory(Path.GetDirectoryName(record)!);
        string temporary = record + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, active, OfficeJson.Options);
                stream.Flush(true);
            }
            File.Move(temporary, record, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public InstallResult Rollback(string id, InstallScope scope, InstallReceipt? expected = null, IReadOnlyDictionary<string, InstallReceipt?>? expectedPeers = null)
    {
        try
        {
            OfficeCatalog.ValidateId(id);
            access.Prepare(scope, paths);
            using var operationLock = AcquireLock(paths.Root(scope), id);
            using var scopeActivation = AcquireLock(paths.Root(scope), "suite-activation");
            var inventory = new InstallationInventory(paths);
            var active = inventory.ReadActive(id, scope);
            string? peersChanged = expectedPeers is null ? null : OfficeCompatibility.PeersChanged(expectedPeers, scope, inventory);
            if (peersChanged is not null) throw new InvalidOperationException(peersChanged);
            if (expected is not null && active.Current != expected) throw new InvalidOperationException("Installation changed; rollback was deferred.");
            if (active.Previous is null) throw new InvalidOperationException("No previous version is registered.");
            inventory.ValidateReceipt(active.Previous, id, scope);
            WriteActive(paths.Record(id, scope), new(active.Previous, active.Current));
            return new(true, "Previous installation activated.");
        }
        catch (Exception ex) { return new(false, $"Rollback failed: {ex.Message}"); }
    }

    public InstallResult Unregister(string id, InstallScope scope)
    {
        try
        {
            OfficeCatalog.ValidateId(id);
            access.Prepare(scope, paths);
            using var operationLock = AcquireLock(paths.Root(scope), id);
            string path = paths.Record(id, scope);
            using var scopeActivation = AcquireLock(paths.Root(scope), "suite-activation");
            OfficeInstallPaths.RejectLinks(path);
            File.Delete(path);
            return new(true, "Installation unregistered. Version files and user data were retained for recovery.");
        }
        catch (Exception ex) { return new(false, $"Removal failed: {ex.Message}"); }
    }

    private static async Task ExtractAsync(string archivePath, string destination, IProgress<InstallProgress>? progress, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > MaxEntries) throw new InvalidDataException("Too many archive entries.");
        long expanded = 0;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int index = 0;
        int lastPercent = -1;
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            string name = entry.FullName;
            int type = (entry.ExternalAttributes >> 16) & 0xF000;
            bool directory = name.EndsWith('/');
            string trimmed = directory ? name[..^1] : name;
            string[] parts = trimmed.Split('/');
            if (type is not (0 or 0x8000 or 0x4000) || (type == 0x4000 && !directory) || (type == 0x8000 && directory) || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0 ||
                string.IsNullOrEmpty(trimmed) || name.Contains('\\') || parts.Any(p => string.IsNullOrEmpty(p) || p is "." or ".." ||
                    p.EndsWith('.') || p.EndsWith(' ') || p.IndexOfAny([':', '*', '?', '"', '<', '>', '|', '\0']) >= 0 ||
                    System.Text.RegularExpressions.Regex.IsMatch(p, @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) ||
                parts.Any(p => p.StartsWith(".office-", StringComparison.OrdinalIgnoreCase)) || !names.Add(trimmed))
                throw new InvalidDataException("Unsafe or duplicate archive entry.");
            expanded = checked(expanded + entry.Length);
            if (expanded > MaxExpandedBytes) throw new InvalidDataException("Expanded archive exceeds limits.");
            string target = Path.GetFullPath(Path.Combine(destination, trimmed.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(Path.GetFullPath(destination) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Archive path escapes the product directory.");
            OfficeInstallPaths.RejectLinks(target);
            if (directory) Directory.CreateDirectory(target);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var source = entry.Open();
                await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                await CopyLimitedAsync(source, output, entry.Length, token);
            }
            int percent = ++index * 100 / Math.Max(1, archive.Entries.Count);
            if (percent != lastPercent) { progress?.Report(new("Extracting product", percent)); lastPercent = percent; }
        }
    }

    private static async Task CopyLimitedAsync(Stream source, Stream target, long expected, CancellationToken token)
    {
        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, token)) > 0)
        {
            total = checked(total + read);
            if (total > expected) throw new InvalidDataException("Package stream exceeded its declared length.");
            await target.WriteAsync(buffer.AsMemory(0, read), token);
        }
        if (total != expected) throw new InvalidDataException("Truncated package stream.");
    }
}
