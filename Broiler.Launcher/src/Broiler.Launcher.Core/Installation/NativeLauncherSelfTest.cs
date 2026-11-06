using System.IO.Compression;
using System.Security.Cryptography;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;

namespace Broiler.Launcher.Installation;

/// <summary>Exercises native serialization and installation in an isolated temporary root.</summary>
internal static class NativeLauncherSelfTest
{
    internal static void RunBundle(string directory)
    {
        string root = Path.Combine(Path.GetTempPath(), "broiler-native-products-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = new OfficeInstallPaths(Path.Combine(root, "user"), Path.Combine(root, "machine"));
            var source = new BundledOfficePackageSource(directory, OfficeReleaseVerifier.CreateDefault());
            var installer = new ProductInstaller(paths, new UserInstallAccess());
            var inventory = new InstallationInventory(paths);
            System.Diagnostics.ProcessStartInfo? start = null;
            var launcher = new AppLauncherService(settingsStore: new LauncherSettingsStore(Path.Combine(root, "settings.json")),
                isWindows: OperatingSystem.IsWindows(), environment: _ => null, currentDirectory: root, inventory: inventory,
                startProcess: info => { start = info; return 1; });
            foreach (var app in launcher.Apps)
            {
                var package = source.Read(app.Id.ToString().ToLowerInvariant());
                // GitHub release assets are flat; the installed bootstrap downloads
                // them into its cache rather than shipping a local packages folder.
                package = package with { ArchivePath = Path.Combine(directory, package.Component.Asset) };
                var result = installer.InstallAsync(package, InstallScope.User).GetAwaiter().GetResult();
                if (!result.Succeeded) throw new InvalidDataException(result.Message);
                var installed = inventory.Find(package.Component.Id, InstallScope.User);
                if (installed.State != InstallationState.Installed || !launcher.LaunchApp(app).Succeeded || start is null ||
                    Path.GetFullPath(start.FileName) != Path.GetFullPath(installed.ExecutablePath!))
                    throw new InvalidDataException("Installed app discovery/launch failed: " + package.Component.Id);
                Console.WriteLine(package.Component.Id + ": isolated native install, receipt, discovery and launch dispatch passed.");
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "broiler-native-selftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var settings = new LauncherSettingsStore(Path.Combine(root, "settings.json"));
            settings.Save(new LauncherSettings());
            _ = settings.Load();
            if (settings.LastLoadError is not null) throw new InvalidDataException(settings.LastLoadError);
            const string version = "0.1.0-demo.1";
            string archive = Path.Combine(root, "Writer.zip");
            bool isWindows = OperatingSystem.IsWindows();
            string entryPoint = isWindows ? "Broiler.Writer.Windows.exe" : "Broiler.Writer.Linux";
            string rid = isWindows ? "win-x64" : "linux-x64";
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(zip.CreateEntry(entryPoint).Open()))
                writer.Write("Test payload only; never executed.");
            var component = new OfficeComponent("writer", version, rid, "Writer.zip", new FileInfo(archive).Length,
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(archive))), "zip", entryPoint, "native", "self-contained");
            var release = new OfficeRelease(1, version, "preview", "office-v" + version, new string('a', 40), version, [component]) { Demo = true };
            byte[] manifest = OfficeJson.SerializeToUtf8Bytes(release);
            _ = new OfficeReleaseVerifier(new Dictionary<string, string>(), version, allowUnsignedDemo: true).Verify(manifest, []);
            _ = OfficeJson.Deserialize<ReleaseSignature>(OfficeJson.SerializeToUtf8Bytes(new ReleaseSignature(1, "fixture", "fixture")));
            _ = OfficeJson.Deserialize<Dictionary<string, string>>(OfficeJson.SerializeToUtf8Bytes(new Dictionary<string, string>()));
            _ = OfficeJson.Deserialize<OfficeReleaseCatalog.CachePage>(OfficeJson.SerializeToUtf8Bytes(new OfficeReleaseCatalog.CachePage(null, "[]", null)));
            _ = OfficeJson.Deserialize<OfficeReleaseCatalog.CheckState>(OfficeJson.SerializeToUtf8Bytes(new OfficeReleaseCatalog.CheckState(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
            var paths = new OfficeInstallPaths(Path.Combine(root, "user"), Path.Combine(root, "machine"));
            var result = new ProductInstaller(paths, new UserInstallAccess()).InstallAsync(new(release, component, archive), InstallScope.User).GetAwaiter().GetResult();
            if (!result.Succeeded) throw new InvalidDataException(result.Message);
            var info = new InstallationInventory(paths).Find("writer", InstallScope.User);
            if (info.State != InstallationState.Installed) throw new InvalidDataException(info.Diagnostic);
            _ = OfficeJson.Deserialize<Dictionary<string, InstallReceipt?>>(OfficeJson.SerializeToUtf8Bytes(new Dictionary<string, InstallReceipt?> { ["writer"] = info.Receipt }));
            Console.WriteLine("Native settings, catalog, manifest, signature, installer and receipt checks passed.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
