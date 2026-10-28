using System.Diagnostics;
using Broiler.Launcher.Services.Boss;

namespace Broiler.Launcher.Installation;

public sealed record ProductUpdate(OfficeReleaseCandidate Candidate, OfficeComponent Component, InstallReceipt Expected);
public interface IUpdateService
{
    IReadOnlyList<ProductUpdate> FindUpdates(IEnumerable<OfficeReleaseCandidate> releases);
    Task<InstallResult> ApplyAsync(ProductUpdate update, IProgress<InstallProgress>? progress = null, CancellationToken token = default);
}
public interface IMachineUpdateBroker
{
    Task<InstallResult> UpdateAsync(ProductUpdate update, IProgress<InstallProgress>? progress, CancellationToken token);
    Task<InstallResult> RollbackAsync(InstallReceipt expected);
}
public interface IProductActivityGuard { string? BlockingReason(InstallationInfo installation); }

public sealed class ProductActivityGuard : IProductActivityGuard
{
    public string? BlockingReason(InstallationInfo installation)
    {
        string directory = Path.GetDirectoryName(installation.ExecutablePath!)! + Path.DirectorySeparatorChar;
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    string name = process.ProcessName;
                    // Framework-dependent products run as dotnet. Do not inspect unrelated processes.
                    string productName = Path.GetFileNameWithoutExtension(installation.Receipt!.EntryPoint);
                    if (!name.Equals(productName, StringComparison.OrdinalIgnoreCase) &&
                        !(installation.Receipt.Invocation == "dotnet" && name.Equals("dotnet", StringComparison.OrdinalIgnoreCase))) continue;
                    string? path = process.MainModule?.FileName;
                    if (path is not null && path.StartsWith(directory, StringComparison.OrdinalIgnoreCase)) return "Close the installed product before updating, then try again.";
                    if (installation.Receipt!.Invocation == "dotnet" && name.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                        return "A dotnet process is running. Close framework-dependent products before updating.";
                }
                catch (InvalidOperationException) { } // Process exited during inspection.
                catch (System.ComponentModel.Win32Exception) { return "A product process could not be inspected. Close it or ask its administrator to update it."; }
            }
        }
        return null;
    }
}

public sealed class ProductUpdateService(OfficeInstallPaths paths, OfficeReleaseVerifier verifier,
    IBossController? boss = null, IMachineUpdateBroker? machine = null, IProductActivityGuard? activity = null,
    bool? platformSupported = null, IInstallationInventory? installationInventory = null) : IUpdateService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public IReadOnlyList<ProductUpdate> FindUpdates(IEnumerable<OfficeReleaseCandidate> releases)
    {
        var candidates = releases.ToArray();
        var result = new List<ProductUpdate>();
        var inventory = installationInventory ?? new InstallationInventory(paths);
        foreach (string id in OfficeCatalog.ProductIds)
        foreach (InstallScope scope in Enum.GetValues<InstallScope>())
        {
            var installed = inventory.Find(id, scope);
            if (installed.State != InstallationState.Installed || installed.Receipt is not { } receipt) continue;
            var latest = candidates.SelectMany(c => c.Release.Components.Where(x => x.Id == id && x.Rid == receipt.Rid &&
                    OfficeVersion.Parse(x.Version).CompareTo(OfficeVersion.Parse(receipt.Version)) > 0).Select(x => new ProductUpdate(c, x, receipt)))
                .OrderByDescending(x => OfficeVersion.Parse(x.Component.Version)).FirstOrDefault();
            if (latest is not null) result.Add(latest);
        }
        return result;
    }

    public async Task<InstallResult> ApplyAsync(ProductUpdate update, IProgress<InstallProgress>? progress = null, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        bool restart = false;
        bool activated = false;
        try
        {
            if (!(platformSupported ?? (OperatingSystem.IsWindows() && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64)))
                return new(false, "Office updates currently support Windows x64.");
            if (!update.Candidate.Compatible) return new(false, "Update the launcher from the verified Office bundle first.");
            if (update.Expected.Scope == InstallScope.Machine && machine is null) return new(false, "Machine updates require the Windows administrator helper.");
            // Re-read metadata now; an earlier catalog result is not an authorization to install different bytes.
            var package = new BundledOfficePackageSource(update.Candidate.PackageDirectory, verifier).Read(update.Component.Id);
            if (package.Component != update.Component || package.Release.Tag != update.Candidate.Release.Tag)
                return new(false, "Release metadata changed. Check for updates again.");
            package = await ProductInstallationService.PrepareArchiveAsync(package, paths, progress, token).ConfigureAwait(false);
            var inventory = installationInventory ?? new InstallationInventory(paths);
            var installed = inventory.Find(update.Component.Id, update.Expected.Scope);
            if (installed.Receipt != update.Expected) return new(false, "Installation changed. Check for updates again.");
            if (update.Component.Id == "boss" && boss is not null)
            {
                var state = await boss.RefreshAsync(token).ConfigureAwait(false);
                if (state.Status is BossStatus.Starting or BossStatus.Stopping or BossStatus.PortConflict ||
                    (state.Status is BossStatus.Running or BossStatus.AlreadyRunning && !state.IsOwned))
                    return new(false, "BOSS is externally managed or busy. Stop it through its administrator tools before updating.");
                if (state.IsOwned && !string.Equals(state.ExecutablePath, installed.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                    return new(false, "The running BOSS belongs to another installation. Stop it before updating this scope.");
                restart = state.IsOwned;
                if (restart && (await boss.StopAsync(token).ConfigureAwait(false)).IsOwned)
                    return new(false, "BOSS could not be stopped; update deferred.");
            }
            string? blocked = (activity ?? new ProductActivityGuard()).BlockingReason(installed);
            if (blocked is not null) return new(false, blocked);
            InstallResult result = update.Expected.Scope == InstallScope.Machine
                ? await machine!.UpdateAsync(update, progress, token).ConfigureAwait(false)
                : await new ProductInstaller(paths, new UserInstallAccess()).InstallAsync(package, update.Expected.Scope, progress, token, expected: update.Expected,
                    activationGuard: () => (activity ?? new ProductActivityGuard()).BlockingReason(installed)).ConfigureAwait(false);
            activated = result.Succeeded;
            if (restart && activated)
            {
                BossState started;
                try { started = await boss!.StartAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (Exception ex) { started = new(BossStatus.Failed, ex.Message); }
                string? newPath = inventory.Find("boss", update.Expected.Scope).ExecutablePath;
                if (started.Status != BossStatus.Running || !started.IsOwned ||
                    !string.Equals(started.ExecutablePath, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    try { await boss!.StopAsync(CancellationToken.None).ConfigureAwait(false); }
                    catch { return new(false, "BOSS readiness failed and shutdown failed. Stop the owned process before manual rollback."); }
                    var current = inventory.Find("boss", update.Expected.Scope).Receipt;
                    if (current is null || current.Version != update.Component.Version || current.Sha256 != update.Component.Sha256)
                        return new(false, "BOSS readiness failed; installation changed, so automatic rollback was deferred.");
                    var rollback = update.Expected.Scope == InstallScope.Machine
                        ? await machine!.RollbackAsync(current).ConfigureAwait(false)
                        : new ProductInstaller(paths, new UserInstallAccess()).Rollback("boss", update.Expected.Scope, current);
                    if (!rollback.Succeeded) return new(false, "BOSS readiness failed. " + rollback.Message);
                    var restored = await boss!.StartAsync(CancellationToken.None).ConfigureAwait(false);
                    return new(false, "BOSS readiness failed; previous version restored. " + restored.Message);
                }
            }
            return result;
        }
        catch (OperationCanceledException) { return new(false, "Update canceled; installed payloads were retained.", true); }
        catch (Exception ex) { return new(false, "Update failed: " + ex.Message); }
        finally
        {
            if (restart && !activated && boss is not null)
            {
                try { await boss.StartAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
            }
            _gate.Release();
        }
    }
}
