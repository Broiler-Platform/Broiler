using System.Runtime.InteropServices;
using System.Net.Http;

namespace Broiler.Launcher.Installation;

public interface IMachineInstallBroker
{
    Task<InstallResult> InstallAsync(string productId, string packageDirectory, IProgress<InstallProgress>? progress, CancellationToken token);
}

public interface IProductInstallationService
{
    Task<InstallAvailability> GetAvailabilityAsync(string productId, InstallScope scope, CancellationToken token = default);
    Task<InstallResult> InstallAsync(string productId, InstallScope scope, IProgress<InstallProgress>? progress = null, CancellationToken token = default);
}

public sealed class ProductInstallationService(BundledOfficePackageSource source, OfficeInstallPaths paths,
    IMachineInstallBroker? machineBroker = null, bool? platformSupported = null) : IProductInstallationService
{
    private static readonly HttpClient DownloadClient = new() { Timeout = TimeSpan.FromMinutes(10) };
    public static Task<VerifiedPackage> PrepareArchiveAsync(VerifiedPackage package, OfficeInstallPaths paths,
        IProgress<InstallProgress>? progress, CancellationToken token) =>
        new OfficePackageDownloader(DownloadClient, Path.Combine(paths.UserRoot, "cache", "downloads")).EnsureArchiveAsync(package, progress, token);
    private bool PlatformSupported => platformSupported ?? (OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64);
    public static ProductInstallationService CreateDefault(IMachineInstallBroker? machineBroker = null) =>
        new(BundledOfficePackageSource.CreateDefault(), OfficeInstallPaths.CreateDefault(), machineBroker);

    public Task<InstallAvailability> GetAvailabilityAsync(string productId, InstallScope scope, CancellationToken token = default) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        var installed = new InstallationInventory(paths).Find(productId, scope);
        if (installed.State == InstallationState.Installed)
            return new InstallAvailability(false, $"Installed for {(scope == InstallScope.User ? "user" : "all users")} ({installed.Receipt!.Version})", installed);
        if (installed.State == InstallationState.Invalid)
            return new InstallAvailability(false, $"Installation needs repair: {installed.Diagnostic}", installed);
        if (!PlatformSupported) return new InstallAvailability(false, "Office installation currently supports Windows x64.", installed);
        if (scope == InstallScope.Machine && machineBroker is null)
            return new InstallAvailability(false, "Machine installation requires the Windows administrator helper.", installed);
        try
        {
            _ = source.Read(productId);
            return new InstallAvailability(true, scope == InstallScope.User ? "Install for user" : "Install for all", installed);
        }
        catch (Exception ex) { return new InstallAvailability(false, ex.Message, installed); }
    }, token);

    public async Task<InstallResult> InstallAsync(string productId, InstallScope scope,
        IProgress<InstallProgress>? progress = null, CancellationToken token = default)
    {
        var availability = await GetAvailabilityAsync(productId, scope, token).ConfigureAwait(false);
        if (!availability.CanInstall) return new(false, availability.Reason);
        if (scope == InstallScope.Machine)
            return await machineBroker!.InstallAsync(productId, source.DirectoryPath, progress, token).ConfigureAwait(false);
        return await Task.Run(async () =>
        {
            // Re-read signed metadata immediately before staging; availability checks confer no trust.
            var package = source.Read(productId);
            package = await PrepareArchiveAsync(package, paths, progress, token).ConfigureAwait(false);
            return await new ProductInstaller(paths, new UserInstallAccess()).InstallAsync(package, scope, progress, token).ConfigureAwait(false);
        }, token).ConfigureAwait(false);
    }
}
