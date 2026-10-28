using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Net;
using System.Net.Http;
using Broiler.Launcher.Installation;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Xunit;

namespace Broiler.Launcher.Tests;

public sealed partial class OfficeInstallationTests : IDisposable
{
    private static readonly Lazy<string> PrivateKey = new(() => { using var rsa = RSA.Create(3072); return rsa.ExportPkcs8PrivateKeyPem(); });
    private readonly string _root = Path.Combine(Path.GetTempPath(), "broiler-install-test-" + Guid.NewGuid().ToString("N"));
    private readonly RSA _rsa = RSA.Create();
    private readonly OfficeInstallPaths _paths;
    private readonly BundledOfficePackageSource _source;
    private readonly OfficeReleaseVerifier _verifier;
    private readonly string _bundle;
    private OfficeRelease _release = null!;
    public OfficeInstallationTests()
    {
        _rsa.ImportFromPem(PrivateKey.Value);
        _bundle = Path.Combine(_root, "bundle");
        Directory.CreateDirectory(Path.Combine(_bundle, "packages"));
        _paths = new(Path.Combine(_root, "user"), Path.Combine(_root, "machine"));
        _verifier = new(new Dictionary<string, string> { ["fixture"] = _rsa.ExportSubjectPublicKeyInfoPem() }, "1.0.0");
        _source = new(_bundle, _verifier);
        WritePackage();
    }
    public void Dispose() { _rsa.Dispose(); Directory.Delete(_root, true); }

    private void WritePackage(string version = "1.0.0", string[]? names = null, bool symlink = false)
    {
        string archive = Path.Combine(_bundle, "packages", "writer.zip");
        File.Delete(archive);
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            foreach (string name in names ?? ["Broiler.Writer.Windows.exe", "library.dll"])
            {
                var entry = zip.CreateEntry(name);
                if (symlink) entry.ExternalAttributes = 0xA1FF << 16;
                using var writer = new StreamWriter(entry.Open());
                writer.Write("fixture-" + version);
            }
        }
        var component = new OfficeComponent("writer", version, "win-x64", "writer.zip", new FileInfo(archive).Length,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(archive))), "zip", "Broiler.Writer.Windows.exe", "native", "self-contained");
        _release = new(1, version, "stable", "office-v" + version, new string('a', 40), "0.1.0-preview.1", [component]);
        Sign();
    }

    private void Sign()
    {
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(_release, OfficeJson.Options);
        File.WriteAllBytes(Path.Combine(_bundle, "office-release.json"), data);
        byte[] signature = _rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        File.WriteAllText(Path.Combine(_bundle, "office-release.signature.json"),
            JsonSerializer.Serialize(new ReleaseSignature(1, "fixture", Convert.ToBase64String(signature)), OfficeJson.Options));
    }
    private ProductInstaller Installer(IInstallAccess? access = null) => new(_paths, access ?? new UserInstallAccess());
    private InstallationInfo Installed() => new InstallationInventory(_paths).Find("writer", InstallScope.User);
    private async Task<InstallResult> Install(bool repair = false, IProgress<InstallProgress>? progress = null, CancellationToken token = default) =>
        await Installer().InstallAsync(_source.Read("writer"), InstallScope.User, progress, token, repair);

    [Fact]
    public async Task InstallsCompleteClosureAndReceiptWithoutChangingSource()
    {
        Assert.True((await Install()).Succeeded);
        var installed = Installed();
        Assert.Equal(InstallationState.Installed, installed.State);
        Assert.Equal("1.0.0", installed.Receipt!.Version);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(installed.ExecutablePath)!, "library.dll")));
        Assert.True(File.Exists(Path.Combine(_bundle, "packages", "writer.zip")));
        Assert.Empty(Directory.GetDirectories(Path.Combine(_paths.UserRoot, "staging")));
        Assert.Equal(InstallationState.NotInstalled, new InstallationInventory(_paths).Find("writer", InstallScope.Machine).State);
    }

    [Fact]
    public async Task RejectsAlreadyInstalledAndPreservesActiveFilesDuringRepairAndRollback()
    {
        Assert.True((await Install()).Succeeded);
        var old = Installed();
        Assert.False((await Install()).Succeeded);
        WritePackage("1.1.0");
        Assert.True((await Install(repair: true)).Succeeded);
        Assert.Equal("1.1.0", Installed().Receipt!.Version);
        Assert.Equal("fixture-1.0.0", File.ReadAllText(old.ExecutablePath!));
        Assert.True(Installer().Rollback("writer", InstallScope.User).Succeeded);
        Assert.Equal(old.ExecutablePath, Installed().ExecutablePath);
    }

    [Fact]
    public async Task RefusesDowngradeOnExplicitRepair()
    {
        Assert.True((await Install()).Succeeded);
        WritePackage("0.9.0");
        Assert.False((await Install(repair: true)).Succeeded);
        Assert.Equal("1.0.0", Installed().Receipt!.Version);
    }

    [Fact]
    public async Task HashMismatchCannotChangePriorInstallation()
    {
        Assert.True((await Install()).Succeeded);
        var old = Installed();
        WritePackage("1.1.0");
        var package = _source.Read("writer");
        byte[] data = File.ReadAllBytes(package.ArchivePath);
        data[^1] ^= 1;
        File.WriteAllBytes(package.ArchivePath, data);
        var result = await Installer().InstallAsync(package, InstallScope.User, repair: true);
        Assert.False(result.Succeeded);
        Assert.Contains("hash", result.Message);
        Assert.Equal(old.Receipt, Installed().Receipt);
    }

    [Theory]
    [InlineData("../escaped.exe")]
    [InlineData("/absolute.exe")]
    [InlineData("C:/drive.exe")]
    [InlineData("sub\\windows.exe")]
    [InlineData("file:stream")]
    [InlineData("CON.txt")]
    [InlineData("trailing.")]
    [InlineData(".office-receipt.json")]
    public async Task UnsafeArchivePathsCannotActivate(string name)
    {
        WritePackage(names: ["Broiler.Writer.Windows.exe", name]);
        var result = await Install();
        Assert.False(result.Succeeded);
        Assert.Equal(InstallationState.NotInstalled, Installed().State);
        Assert.False(File.Exists(Path.Combine(_root, "escaped.exe")));
    }

    [Fact]
    public async Task RejectsCaseCollidingArchiveNames()
    {
        WritePackage(names: ["Broiler.Writer.Windows.exe", "A.dll", "a.dll"]);
        Assert.False((await Install()).Succeeded);
        Assert.Equal(InstallationState.NotInstalled, Installed().State);
    }

    [Fact]
    public async Task RejectsArchiveSymlinks()
    {
        WritePackage(symlink: true);
        Assert.False((await Install()).Succeeded);
        Assert.Equal(InstallationState.NotInstalled, Installed().State);
    }

    [Fact]
    public async Task CancellationDuringExtractionPreservesPriorActivation()
    {
        Assert.True((await Install()).Succeeded);
        var old = Installed();
        WritePackage("1.1.0");
        using var cancellation = new CancellationTokenSource();
        var result = await Install(true, new Reporter(p => { if (p.Stage == "Extracting product") cancellation.Cancel(); }), cancellation.Token);
        Assert.True(result.Canceled);
        Assert.Equal(old.Receipt, Installed().Receipt);
        Assert.Empty(Directory.GetDirectories(Path.Combine(_paths.UserRoot, "staging")));
    }

    [Fact]
    public async Task CancellationAtCommitBoundaryDoesNotProduceHalfActivation()
    {
        using var cancellation = new CancellationTokenSource();
        var result = await Install(progress: new Reporter(p => { if (!p.CanCancel) cancellation.Cancel(); }), token: cancellation.Token);
        Assert.True(result.Succeeded);
        Assert.Equal(InstallationState.Installed, Installed().State);
    }

    [Fact]
    public async Task ConcurrentOperationIsRejectedByPerProductFileLock()
    {
        Directory.CreateDirectory(Path.Combine(_paths.UserRoot, "locks"));
        using var locked = new FileStream(Path.Combine(_paths.UserRoot, "locks", "writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Assert.False((await Install()).Succeeded);
        Assert.Equal(InstallationState.NotInstalled, Installed().State);
    }

    [Fact]
    public async Task StaleOrMismatchedReceiptIsNotReportedAsInstalled()
    {
        Assert.True((await Install()).Succeeded);
        File.Delete(Installed().ExecutablePath!);
        Assert.Equal(InstallationState.Invalid, Installed().State);
        Assert.False((await Install()).Succeeded);
        Assert.True((await Install(repair: true)).Succeeded);
        Assert.Equal(InstallationState.Installed, Installed().State);
    }

    [Fact]
    public async Task UnregisterPreservesDocumentsSettingsAndVersionFiles()
    {
        Assert.True((await Install()).Succeeded);
        string executable = Installed().ExecutablePath!;
        string settings = Path.Combine(_paths.UserRoot, "settings.json");
        File.WriteAllText(settings, "keep");
        Assert.True(Installer().Unregister("writer", InstallScope.User).Succeeded);
        Assert.Equal(InstallationState.NotInstalled, Installed().State);
        Assert.True(File.Exists(executable));
        Assert.Equal("keep", File.ReadAllText(settings));
    }

    [Fact]
    public async Task OrdinaryInstallerCannotWriteMachineScope()
    {
        var result = await Installer().InstallAsync(_source.Read("writer"), InstallScope.Machine);
        Assert.False(result.Succeeded);
        Assert.False(Directory.Exists(_paths.MachineRoot));
    }

    [Fact]
    public void MachineAccessCannotAcceptAnArbitraryDestinationRoot()
    {
        Assert.Throws<UnauthorizedAccessException>(() => new WindowsMachineInstallAccess().Prepare(InstallScope.Machine, _paths));
        Assert.False(Directory.Exists(_paths.MachineRoot));
    }

    [Fact]
    public async Task ActivationWriteFailureLeavesPriorRecordIntact()
    {
        Assert.True((await Install()).Succeeded);
        var old = Installed();
        WritePackage("1.1.0");
        using var recordLock = new FileStream(_paths.Record("writer", InstallScope.User), FileMode.Open, FileAccess.Read, FileShare.Read);
        var result = await Install(repair: true);
        Assert.False(result.Succeeded);
        Assert.Equal(old.Receipt, Installed().Receipt);
    }

    [Fact]
    public void TamperedManifestAndUntrustedSignatureAreRejected()
    {
        byte[] manifest = File.ReadAllBytes(Path.Combine(_bundle, "office-release.json"));
        byte[] signature = File.ReadAllBytes(Path.Combine(_bundle, "office-release.signature.json"));
        manifest[^1] ^= 1;
        Assert.Throws<InvalidDataException>(() => _verifier.Verify(manifest, signature));
        Assert.Throws<InvalidDataException>(() => new OfficeReleaseVerifier(new Dictionary<string, string>(), "1.0.0").Verify(manifest, signature));
    }

    [Fact]
    public void EmptyTrustStoreDisablesPackageSource()
    {
        Assert.NotNull(OfficeReleaseVerifier.CreateDefault());
        var empty = new OfficeReleaseVerifier(new Dictionary<string, string>(), "1.0.0");
        Assert.False(empty.HasTrustedKeys);
        Assert.Throws<InvalidDataException>(() => new BundledOfficePackageSource(_bundle, empty).Read("writer"));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("duplicate")]
    [InlineData("minimum")]
    [InlineData("rid")]
    [InlineData("entry")]
    public void SignedButInvalidMetadataIsRejected(string change)
    {
        _release = change switch
        {
            "schema" => _release with { SchemaVersion = 99 },
            "duplicate" => _release with { Components = [_release.Components[0], _release.Components[0]] },
            "minimum" => _release with { MinimumLauncherVersion = "2.0.0" },
            "rid" => _release with { Components = [_release.Components[0] with { Rid = "linux-x64" }] },
            _ => _release with { Components = [_release.Components[0] with { EntryPoint = "../cmd.exe" }] },
        };
        Sign();
        Assert.Throws<InvalidDataException>(() => _source.Read("writer"));
        Assert.False(Directory.Exists(_paths.UserRoot));
    }

    [Fact]
    public void ManifestCanIncludeLauncherWithoutOfferingSelfInstallation()
    {
        _release = _release with { Components = [_release.Components[0], _release.Components[0] with
            { Id = "launcher", EntryPoint = "Broiler.Launcher.Windows.exe", Asset = "launcher.zip" }] };
        Sign();
        Assert.Equal("writer", _source.Read("writer").Component.Id);
        Assert.Throws<InvalidDataException>(() => _source.Read("launcher"));
    }

    private byte[] DemoManifest()
    {
        _release = _release with { OfficeVersion = "1.0.0-demo.1", Tag = "office-v1.0.0-demo.1", Channel = "preview", Demo = true };
        return JsonSerializer.SerializeToUtf8Bytes(_release, OfficeJson.Options);
    }

    [Fact]
    public void DefaultVerifierBuildBoundaryMatchesRequestedBuildMode()
    {
#if EXPECT_OFFICE_DEMO
        Assert.True(OfficeBuild.IsDemo);
#else
        Assert.False(OfficeBuild.IsDemo);
#endif
        Assert.Equal(OfficeReleaseVerifier.CreateDefault().HasTrustedKeys || OfficeBuild.IsDemo,
            OfficeReleaseVerifier.CreateDefault().CanReadPackages);
    }

    [Fact]
    public void ProductionVerifierRejectsUnsignedDemoWhileExplicitDemoVerifierAcceptsIt()
    {
        byte[] manifest = DemoManifest();
        var keys = new Dictionary<string, string>();
        Assert.Throws<InvalidDataException>(() => new OfficeReleaseVerifier(keys, "1.0.0").Verify(manifest, []));
        Assert.True(new OfficeReleaseVerifier(keys, "1.0.0", allowUnsignedDemo: true).Verify(manifest, []).Demo);
        Assert.Throws<InvalidDataException>(() => new OfficeReleaseVerifier(keys, "1.0.0", true).Verify(manifest, [1]));
    }

    [Theory]
    [InlineData(false, "preview", "1.0.0-demo.1")]
    [InlineData(true, "stable", "1.0.0-demo.1")]
    [InlineData(true, "preview", "1.0.0")]
    public void DemoVerificationCannotAcceptUnmarkedOrStableReleases(bool demo, string channel, string version)
    {
        _release = _release with { Demo = demo, Channel = channel, OfficeVersion = version, Tag = "office-v" + version };
        var verifier = new OfficeReleaseVerifier(new Dictionary<string, string>(), "1.0.0", true);
        Assert.Throws<InvalidDataException>(() => verifier.Verify(JsonSerializer.SerializeToUtf8Bytes(_release, OfficeJson.Options), []));
    }

    [Fact]
    public async Task DemoCanInstallBundledBytesAndMarksItsReceiptWithoutSigningKeys()
    {
        File.WriteAllBytes(Path.Combine(_bundle, "office-release.json"), DemoManifest());
        File.Delete(Path.Combine(_bundle, "office-release.signature.json"));
        var source = new BundledOfficePackageSource(_bundle, new OfficeReleaseVerifier(new Dictionary<string, string>(), "1.0.0", true));
        var package = source.Read("writer");
        Assert.True((await Installer().InstallAsync(package, InstallScope.User)).Succeeded);
        Assert.True(Installed().Receipt!.Demo);
        File.Delete(package.ArchivePath);
        Assert.Throws<InvalidDataException>(() => source.Read("writer"));
        using var client = new HttpClient(new DownloadHandler([]));
        await Assert.ThrowsAsync<InvalidDataException>(() => new OfficePackageDownloader(client, Path.Combine(_root, "demo-cache")).EnsureArchiveAsync(package));
    }

    [Fact]
    public void IsolatedPortableOfficeLayoutIsDiscoveredFromLauncherFolder()
    {
        string launcher = Path.Combine(_bundle, "launcher"), product = Path.Combine(_bundle, "products", "writer");
        Directory.CreateDirectory(launcher);
        Directory.CreateDirectory(product);
        File.WriteAllText(Path.Combine(product, "Broiler.Writer.Windows.exe"), "fixture");
        var app = BroilerAppInfo.CreateDefaultSuite().Single(a => a.Id == BroilerAppId.Writer);
        var service = new AppLauncherService([app], new MemoryStore(), isWindows: true, environment: _ => null,
            currentDirectory: launcher, inventory: new EmptyInstallationInventory());
        Assert.Equal("Ready (Portable)", service.ResolveApp(app, launcher).StatusText);
    }

    [Theory]
    [InlineData("1.9.0", "1.10.0")]
    [InlineData("1.0.0-preview.9", "1.0.0-preview.10")]
    [InlineData("1.0.0-rc.1", "1.0.0")]
    public void VersionsUseSemanticPrecedence(string a, string b) => Assert.True(OfficeVersion.Parse(a).CompareTo(OfficeVersion.Parse(b)) < 0);

    [Theory]
    [InlineData("1.0.0\n")]
    [InlineData("1.0.0-01")]
    [InlineData("01.0.0")]
    public void InvalidSemanticVersionsAreRejected(string value) => Assert.Throws<InvalidDataException>(() => OfficeVersion.Parse(value));

    [Fact]
    public async Task DiscoveryPrefersManagedUserInstallButRetainsExplicitCustomOverride()
    {
        Assert.True((await Install()).Succeeded);
        var app = BroilerAppInfo.CreateDefaultSuite().Single(a => a.Id == BroilerAppId.Writer);
        var service = new AppLauncherService([app], new MemoryStore(), inventory: new InstallationInventory(_paths));
        Assert.Equal(InstallScope.User, service.AutoDetect(app, _bundle).InstallScope);
        string custom = Path.Combine(_root, "custom.exe");
        File.WriteAllText(custom, "custom");
        service.Settings.Apps[app.Id] = new() { ExecutablePath = custom, UseAutoDetect = false };
        Assert.Equal(custom, service.ResolveApp(app).ResolvedPath);
        Assert.Equal(InstallationState.Installed, Installed().State);
    }

    [Fact]
    public async Task AvailabilityUsesInventoryAndBrokerAndDoesNotOverwriteInstalledScope()
    {
        var service = new ProductInstallationService(_source, _paths, platformSupported: true);
        Assert.True((await service.GetAvailabilityAsync("writer", InstallScope.User)).CanInstall);
        Assert.False((await service.GetAvailabilityAsync("writer", InstallScope.Machine)).CanInstall);
        Assert.True((await service.InstallAsync("writer", InstallScope.User)).Succeeded);
        var availability = await service.GetAvailabilityAsync("writer", InstallScope.User);
        Assert.False(availability.CanInstall);
        Assert.Equal("1.0.0", availability.Installation.Receipt!.Version);
    }

    [Fact]
    public async Task MachineApprovalDenialDoesNotWriteAnyMachineFiles()
    {
        var broker = new DeniedBroker();
        var service = new ProductInstallationService(_source, _paths, broker, platformSupported: true);
        Assert.True((await service.GetAvailabilityAsync("writer", InstallScope.Machine)).CanInstall);
        Assert.True((await service.InstallAsync("writer", InstallScope.Machine)).Canceled);
        Assert.Equal("writer", broker.RequestedProduct);
        Assert.False(Directory.Exists(_paths.MachineRoot));
    }

    [Fact]
    public async Task MissingArchiveDownloadsOnlySignedReleaseAssetAndVerifiesBytes()
    {
        var package = _source.Read("writer");
        byte[] bytes = File.ReadAllBytes(package.ArchivePath);
        File.Delete(package.ArchivePath);
        var handler = new DownloadHandler(bytes);
        using var client = new HttpClient(handler);
        var downloader = new OfficePackageDownloader(client, Path.Combine(_root, "download-cache"));
        var downloaded = await downloader.EnsureArchiveAsync(package);
        Assert.Equal("https://github.com/Broiler-Platform/Broiler/releases/download/office-v1.0.0/writer.zip", handler.RequestedUri?.AbsoluteUri);
        Assert.True((await Installer().InstallAsync(downloaded, InstallScope.User)).Succeeded);
        await downloader.EnsureArchiveAsync(package);
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task CorruptDownloadCannotEnterCacheOrInventory()
    {
        var package = _source.Read("writer");
        byte[] bytes = File.ReadAllBytes(package.ArchivePath);
        File.Delete(package.ArchivePath);
        bytes[0] ^= 1;
        using var client = new HttpClient(new DownloadHandler(bytes));
        string cache = Path.Combine(_root, "download-cache");
        await Assert.ThrowsAsync<InvalidDataException>(() => new OfficePackageDownloader(client, cache).EnsureArchiveAsync(package));
        Assert.Empty(Directory.GetFiles(cache));
        Assert.Equal(InstallationState.NotInstalled, Installed().State);
    }

    [Fact]
    public async Task BothScopesRemainVisibleAndUserScopeWinsDiscovery()
    {
        var inventory = new InstallationInventory(_paths, _ => true); // Permission policy is tested separately from scope selection.
        var machine = new ProductInstaller(_paths, new TemporaryMachineAccess());
        Assert.True((await machine.InstallAsync(_source.Read("writer"), InstallScope.Machine)).Succeeded);
        var app = BroilerAppInfo.CreateDefaultSuite().Single(a => a.Id == BroilerAppId.Writer);
        var service = new AppLauncherService([app], new MemoryStore(), isWindows: true, inventory: inventory);
        Assert.Equal(InstallScope.Machine, service.AutoDetect(app, _bundle).InstallScope);
        Assert.True((await Install()).Succeeded);
        Assert.Equal(InstallScope.User, service.AutoDetect(app, _bundle).InstallScope);
        Assert.All(inventory.Enumerate("writer"), info => Assert.Equal(InstallationState.Installed, info.State));
        var linux = new AppLauncherService([app], new MemoryStore(), isWindows: false, environment: _ => null, currentDirectory: _bundle, inventory: inventory);
        Assert.Null(linux.AutoDetect(app, _bundle).InstallScope);
    }

    [Fact]
    public async Task ScopeSpoofingInReceiptIsReportedAsInvalid()
    {
        Assert.True((await Install()).Succeeded);
        string record = _paths.Record("writer", InstallScope.User);
        var active = JsonSerializer.Deserialize<ActiveInstallation>(File.ReadAllText(record), OfficeJson.Options)!;
        File.WriteAllText(record, JsonSerializer.Serialize(active with { Current = active.Current with { Scope = InstallScope.Machine } }, OfficeJson.Options));
        Assert.Equal(InstallationState.Invalid, Installed().State);
    }

    private sealed class TemporaryMachineAccess : IInstallAccess
    {
        public void Prepare(InstallScope scope, OfficeInstallPaths paths)
        {
            Assert.Equal(InstallScope.Machine, scope);
            Assert.Contains("broiler-install-test-", paths.MachineRoot);
            Directory.CreateDirectory(paths.MachineRoot);
        }
    }

    private sealed class DeniedBroker : IMachineInstallBroker
    {
        public string? RequestedProduct;
        public Task<InstallResult> InstallAsync(string productId, string packageDirectory, IProgress<InstallProgress>? progress, CancellationToken token)
        {
            RequestedProduct = productId;
            return Task.FromResult(new InstallResult(false, "Administrator approval canceled.", true));
        }
    }

    private sealed class DownloadHandler(byte[] bytes) : HttpMessageHandler
    {
        public Uri? RequestedUri;
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            RequestedUri = request.RequestUri;
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        }
    }

    private sealed class Reporter(Action<InstallProgress> report) : IProgress<InstallProgress>
    {
        public void Report(InstallProgress value) => report(value);
    }
    private sealed class MemoryStore : ILauncherSettingsStore
    {
        public string SettingsFilePath => "memory";
        public LauncherSettings Load() => new();
        public void Save(LauncherSettings settings) { }
    }
}
