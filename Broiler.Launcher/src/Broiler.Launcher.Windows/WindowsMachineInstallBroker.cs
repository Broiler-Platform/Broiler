using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Broiler.Launcher.Installation;

namespace Broiler.Launcher.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsMachineInstallBroker : IMachineInstallBroker, IMachineUpdateBroker
{
    public async Task<InstallResult> UpdateAsync(ProductUpdate update, IProgress<InstallProgress>? progress, CancellationToken token)
    {
        var package = new BundledOfficePackageSource(update.Candidate.PackageDirectory, OfficeReleaseVerifier.CreateDefault()).Read(update.Component.Id);
        package = await ProductInstallationService.PrepareArchiveAsync(package, OfficeInstallPaths.CreateDefault(), progress, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        return await RunUpdateHelper("update", update.Expected, update.Candidate.PackageDirectory, package.ArchivePath, progress).ConfigureAwait(false);
    }

    public Task<InstallResult> RollbackAsync(InstallReceipt expected) => RunUpdateHelper("update-rollback", expected, "", "", null);

    private static async Task<InstallResult> RunUpdateHelper(string operation, InstallReceipt expected, string directory, string archive, IProgress<InstallProgress>? progress)
    {
        try
        {
            // The helper accepts an expected receipt value, never arbitrary destinations or commands.
            string receipt = Convert.ToBase64String(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(expected, OfficeJson.Options));
            var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Broiler.Launcher.Windows.exe"))
                { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            foreach (string arg in new[] { "--office-machine", operation, expected.ProductId, directory, archive, receipt }) info.ArgumentList.Add(arg);
            progress?.Report(new("Awaiting Windows administrator approval", null, false));
            using var process = Process.Start(info);
            if (process is null) return new(false, "Administrator update helper could not start.");
            await process.WaitForExitAsync().ConfigureAwait(false);
            return new(process.ExitCode == 0, process.ExitCode == 0 ? "Machine update completed." : $"Administrator update failed (code {process.ExitCode}); check the installation and running processes.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return new(false, "Administrator approval canceled; installation unchanged.", true); }
        catch (Exception ex) { return new(false, "Administrator update failed: " + ex.Message); }
    }
    public async Task<InstallResult> InstallAsync(string productId, string packageDirectory, IProgress<InstallProgress>? progress, CancellationToken token)
    {
        try
        {
            OfficeCatalog.ValidateId(productId);
            token.ThrowIfCancellationRequested();
            var packageSource = new BundledOfficePackageSource(packageDirectory, OfficeReleaseVerifier.CreateDefault());
            var package = await ProductInstallationService.PrepareArchiveAsync(packageSource.Read(productId), OfficeInstallPaths.CreateDefault(), progress, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            string executable = Path.Combine(AppContext.BaseDirectory, "Broiler.Launcher.Windows.exe");
            if (!File.Exists(executable)) return new(false, "The Windows launcher apphost is required for administrator installation.");
            progress?.Report(new("Awaiting Windows administrator approval", null, false));
            var info = new ProcessStartInfo(executable) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            foreach (var value in new[] { "--office-machine", "install", productId, Path.GetFullPath(packageDirectory), package.ArchivePath }) info.ArgumentList.Add(value);
            using var process = Process.Start(info);
            if (process is null) return new(false, "The administrator installer could not be started.");
            // Do not abandon or kill an elevated transaction after UAC acceptance. The helper
            // performs a bounded local operation; cancellation is allowed before this handoff only.
            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode == 0 ? new(true, $"Installed {productId} for all users.")
                : new(false, $"Administrator installation failed (code {process.ExitCode}). Verify the signed package and machine directory permissions.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return new(false, "Administrator approval was canceled; installation was not changed.", true); }
        catch (OperationCanceledException) { return new(false, "Installation canceled.", true); }
        catch (Exception ex) { return new(false, $"Administrator installation failed: {ex.Message}"); }
    }

    public static bool TryHandle(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 || args[0] != "--office-machine") return false;
        try
        {
            if (RuntimeInformation.ProcessArchitecture != Architecture.X64 || args.Length is < 3 or > 6)
                throw new InvalidDataException("Invalid machine installer request.");
            string operation = args[1], productId = args[2];
            OfficeCatalog.ValidateId(productId);
            if (!WindowsInstallSecurity.IsAdministrator()) throw new UnauthorizedAccessException("Run the machine helper as administrator.");
            var installer = new ProductInstaller(OfficeInstallPaths.CreateDefault(), new WindowsMachineInstallAccess());
            InstallResult result;
            if (operation is "update" or "update-rollback" && args.Length == 6)
            {
                if (args[5].Length > 8192) throw new InvalidDataException("Invalid expected receipt.");
                var expected = System.Text.Json.JsonSerializer.Deserialize<InstallReceipt>(Convert.FromBase64String(args[5]), OfficeJson.Options)
                    ?? throw new InvalidDataException("Missing expected receipt.");
                if (expected.ProductId != productId || expected.Scope != InstallScope.Machine) throw new InvalidDataException("Receipt scope mismatch.");
                var installed = new InstallationInventory(OfficeInstallPaths.CreateDefault()).Find(productId, InstallScope.Machine);
                if (installed.Receipt != expected) throw new InvalidDataException("Installation changed.");
                if (operation == "update-rollback") result = installer.Rollback(productId, InstallScope.Machine, expected);
                else
                {
                    if (!Path.IsPathFullyQualified(args[3]) || !Path.IsPathFullyQualified(args[4])) throw new InvalidDataException("Absolute package paths required.");
                    var package = new BundledOfficePackageSource(args[3], OfficeReleaseVerifier.CreateDefault()).Read(productId) with { ArchivePath = args[4] };
                    string? blocked = new ProductActivityGuard().BlockingReason(installed);
                    if (blocked is not null) throw new InvalidOperationException(blocked);
                    result = installer.InstallAsync(package, InstallScope.Machine, expected: expected,
                        activationGuard: () => new ProductActivityGuard().BlockingReason(installed)).GetAwaiter().GetResult();
                }
            }
            else if (operation is "install" or "repair" && args.Length is 4 or 5 && Path.IsPathFullyQualified(args[3]))
            {
                // Verification occurs again inside the elevated process. User-writable input is
                // copied to protected staging and hashed there before any activation.
                var source = new BundledOfficePackageSource(args[3], OfficeReleaseVerifier.CreateDefault());
                var package = source.Read(productId);
                if (args.Length == 5)
                {
                    if (!Path.IsPathFullyQualified(args[4])) throw new InvalidDataException("Archive path must be absolute.");
                    package = package with { ArchivePath = args[4] };
                }
                if (!File.Exists(package.ArchivePath)) throw new FileNotFoundException("Machine installation requires a complete bundled archive; download the full Office bundle first.");
                result = installer.InstallAsync(package, InstallScope.Machine, repair: operation == "repair").GetAwaiter().GetResult();
            }
            else if (operation == "rollback" && args.Length == 3) result = installer.Rollback(productId, InstallScope.Machine);
            else if (operation == "unregister" && args.Length == 3) result = installer.Unregister(productId, InstallScope.Machine);
            else throw new InvalidDataException("Only install, repair, rollback and unregister are supported by the helper.");
            if (!result.Succeeded) exitCode = 4;
        }
        catch { exitCode = 2; }
        return true;
    }
}
