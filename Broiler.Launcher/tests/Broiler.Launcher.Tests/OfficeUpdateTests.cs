using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Broiler.Launcher.Installation;
using Broiler.Launcher.Services.Boss;
using Xunit;

namespace Broiler.Launcher.Tests;

public sealed partial class OfficeInstallationTests
{
    [Fact]
    public async Task ConfiguredIntervalAndCheckHistorySurviveOfflineFailures()
    {
        int calls = 0; bool offline = false; var now = DateTimeOffset.UtcNow;
        using var client = new HttpClient(new CatalogHttp(_ => { calls++; if (offline) throw new HttpRequestException("offline fixture"); return JsonResponse(Array.Empty<object>()); }));
        string cache = Path.Combine(_root, "history-cache");
        var catalog = new OfficeReleaseCatalog(client, cache, _verifier, () => now);
        var prefs = new Broiler.Launcher.Models.UpdateSettings { Channel = "stable", CheckIntervalHours = 72 };
        await catalog.CheckAsync(prefs); Assert.Equal(now, catalog.Status.LastAttempt); Assert.Equal(now, catalog.Status.LastSuccess);
        now += TimeSpan.FromHours(2); await catalog.CheckAsync(prefs); Assert.Equal(1, calls);
        prefs.CheckIntervalHours = 1; await catalog.CheckAsync(prefs); Assert.Equal(2, calls);
        var success = catalog.Status.LastSuccess;
        offline = true; now += TimeSpan.FromHours(2); await catalog.CheckAsync(prefs);
        Assert.Equal(now, catalog.Status.LastAttempt); Assert.Equal(success, catalog.Status.LastSuccess); Assert.Contains("offline", catalog.Status.LastError!);
        var restarted = new OfficeReleaseCatalog(client, cache, _verifier, () => now);
        Assert.Equal(catalog.Status, restarted.Status);
    }
    private void BossPackage(string version)
    {
        WritePackage(version, ["Broiler.Office.Server.exe", "wwwroot/index.html"]);
        _release = _release with { Components = [_release.Components[0] with { Id = "boss", EntryPoint = "Broiler.Office.Server.exe" }] };
        Sign();
    }
    private sealed class UpdateBoss(Func<string?> path) : IBossController
    {
        public BossState Current { get; set; } = new(BossStatus.Stopped, "Stopped");
        public event Action<BossState>? StateChanged { add { } remove { } }
        public int Starts, Stops;
        public bool FailNextStart;
        public Task<BossState> StartAsync(CancellationToken token = default)
        {
            Starts++;
            if (FailNextStart) { FailNextStart = false; return Task.FromResult(Current = new(BossStatus.Failed, "Readiness failed")); }
            return Task.FromResult(Current = new(BossStatus.Running, "Ready", IsOwned: true) { ExecutablePath = path() });
        }
        public Task<BossState> StopAsync(CancellationToken token = default) { Stops++; return Task.FromResult(Current = new(BossStatus.Stopped, "Stopped")); }
        public Task<BossState> RefreshAsync(CancellationToken token = default) => Task.FromResult(Current);
        public Task<BossState> OpenBrowserAsync(CancellationToken token = default) => Task.FromResult(Current);
        public void OpenLog() { }
        public void Dispose() { }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedBossRestartsAndFailedReadinessRestoresPreviousVersion(bool fail)
    {
        BossPackage("1.0.0");
        Assert.True((await Installer().InstallAsync(_source.Read("boss"), InstallScope.User)).Succeeded);
        var inventory = new InstallationInventory(_paths);
        string? PathNow() => inventory.Find("boss", InstallScope.User).ExecutablePath;
        var before = inventory.Find("boss", InstallScope.User).Receipt;
        using var boss = new UpdateBoss(PathNow);
        await boss.StartAsync();
        BossPackage("1.1.0");
        boss.FailNextStart = fail;
        var service = new ProductUpdateService(_paths, _verifier, boss, activity: new UpdateActivity(), platformSupported: true);
        var result = await service.ApplyAsync(Assert.Single(service.FindUpdates([Candidate()])));
        Assert.Equal(!fail, result.Succeeded);
        Assert.Equal(fail ? "1.0.0" : "1.1.0", inventory.Find("boss", InstallScope.User).Receipt!.Version);
        Assert.Equal(BossStatus.Running, boss.Current.Status);
        Assert.Equal(PathNow(), boss.Current.ExecutablePath);
        if (fail) Assert.Equal(before, inventory.Find("boss", InstallScope.User).Receipt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExternalOrOtherScopeBossIsNeverStopped(bool owned)
    {
        BossPackage("1.0.0");
        Assert.True((await Installer().InstallAsync(_source.Read("boss"), InstallScope.User)).Succeeded);
        using var boss = new UpdateBoss(() => "C:\\other\\Broiler.Office.Server.exe")
        {
            Current = new(owned ? BossStatus.Running : BossStatus.AlreadyRunning, "Other instance", IsOwned: owned)
                { ExecutablePath = "C:\\other\\Broiler.Office.Server.exe" }
        };
        BossPackage("1.1.0");
        var service = new ProductUpdateService(_paths, _verifier, boss, activity: new UpdateActivity(), platformSupported: true);
        Assert.False((await service.ApplyAsync(Assert.Single(service.FindUpdates([Candidate()])))).Succeeded);
        Assert.Equal(0, boss.Stops);
        Assert.Equal("1.0.0", new InstallationInventory(_paths).Find("boss", InstallScope.User).Receipt!.Version);
    }

    private sealed class ScopeInventory(InstallationInfo user, InstallationInfo machine) : IInstallationInventory
    {
        public InstallationInfo Find(string id, InstallScope scope) => id == "writer" ? scope == InstallScope.User ? user : machine : new(id, scope, InstallationState.NotInstalled);
    }
    private sealed class UpdateBroker : IMachineUpdateBroker
    {
        public ProductUpdate? Requested;
        public Task<InstallResult> UpdateAsync(ProductUpdate update, IProgress<InstallProgress>? progress, CancellationToken token)
        { Requested = update; return Task.FromResult(new InstallResult(false, "UAC denied", true)); }
        public Task<InstallResult> RollbackAsync(InstallReceipt expected) => throw new InvalidOperationException();
    }
    [Fact]
    public async Task BothScopesRemainExplicitAndMachineDenialPreservesUserReceipt()
    {
        Assert.True((await Install()).Succeeded);
        var user = Installed();
        var machine = user with { Scope = InstallScope.Machine, Receipt = user.Receipt! with { Scope = InstallScope.Machine } };
        WritePackage("1.1.0");
        var broker = new UpdateBroker();
        var service = new ProductUpdateService(_paths, _verifier, machine: broker, activity: new UpdateActivity(), platformSupported: true,
            installationInventory: new ScopeInventory(user, machine));
        var plans = service.FindUpdates([Candidate()]);
        Assert.Equal(2, plans.Count);
        var plan = Assert.Single(plans.Where(x => x.Expected.Scope == InstallScope.Machine));
        Assert.True((await service.ApplyAsync(plan)).Canceled);
        Assert.Equal(plan, broker.Requested);
        Assert.Equal(user.Receipt, Installed().Receipt);
        Assert.False(Directory.Exists(_paths.MachineRoot));
    }
    private sealed class UpdateActivity(string? reason = null) : IProductActivityGuard
    {
        public string? BlockingReason(InstallationInfo installation) => reason;
    }
    private ProductUpdateService Updater(string? block = null) => new(_paths, _verifier, activity: new UpdateActivity(block), platformSupported: true);
    private OfficeReleaseCandidate Candidate() => new OfficeReleaseCatalog(new HttpClient(), Path.Combine(_root, "catalog"), _verifier).ReadLocal(_bundle);

    [Fact]
    public async Task UpdateUsesInventoryAndRetainsPreviousPayloadAndScope()
    {
        Assert.True((await Install()).Succeeded);
        var old = Installed();
        WritePackage("1.2.0");
        var service = Updater();
        var plan = Assert.Single(service.FindUpdates([Candidate()]));
        Assert.Equal(old.Receipt, plan.Expected);
        Assert.True((await service.ApplyAsync(plan)).Succeeded);
        Assert.Equal("1.2.0", Installed().Receipt!.Version);
        Assert.Equal("fixture-1.0.0", File.ReadAllText(old.ExecutablePath!));
        Assert.Empty(service.FindUpdates([Candidate()]));
        Assert.Equal(InstallationState.NotInstalled, new InstallationInventory(_paths).Find("writer", InstallScope.Machine).State);
        Assert.True(Installer().Rollback("writer", InstallScope.User, Installed().Receipt).Succeeded);
        Assert.Equal(old.Receipt, Installed().Receipt);
    }

    [Fact]
    public async Task ChangedReceiptAndRollbackCannotClobberNewerActivation()
    {
        Assert.True((await Install()).Succeeded);
        WritePackage("1.1.0");
        var plan = Assert.Single(Updater().FindUpdates([Candidate()]));
        Assert.True((await Install(repair: true)).Succeeded);
        var changed = Installed().Receipt;
        Assert.False((await Updater().ApplyAsync(plan)).Succeeded);
        Assert.False(Installer().Rollback("writer", InstallScope.User, plan.Expected).Succeeded);
        Assert.Equal(changed, Installed().Receipt);
        Assert.False((await Installer().InstallAsync(_source.Read("writer"), InstallScope.User, expected: plan.Expected)).Succeeded);
    }

    [Fact]
    public async Task RunningProductAndChangedManifestDeferUpdate()
    {
        Assert.True((await Install()).Succeeded);
        WritePackage("1.1.0");
        var plan = Assert.Single(Updater().FindUpdates([Candidate()]));
        Assert.Contains("Close", (await Updater("Close the product.").ApplyAsync(plan)).Message);
        Assert.Equal("1.0.0", Installed().Receipt!.Version);
        WritePackage("1.2.0");
        Assert.False((await Updater().ApplyAsync(plan)).Succeeded);
        Assert.Equal("1.0.0", Installed().Receipt!.Version);
    }

    [Fact]
    public async Task CanceledCorruptAndLateRunningUpdatesLeaveActiveRecordUnchanged()
    {
        Assert.True((await Install()).Succeeded);
        var before = Installed().Receipt;
        WritePackage("1.1.0");
        var plan = Assert.Single(Updater().FindUpdates([Candidate()]));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Updater().ApplyAsync(plan, token: canceled.Token));
        Assert.False((await Installer().InstallAsync(_source.Read("writer"), InstallScope.User, expected: before,
            activationGuard: () => "Product started during extraction.")).Succeeded);
        File.AppendAllText(Path.Combine(_bundle, "packages", "writer.zip"), "tamper");
        Assert.False((await Updater().ApplyAsync(plan)).Succeeded);
        Assert.Equal(before, Installed().Receipt);
    }

    [Fact]
    public async Task NewLauncherRequirementIsDiscoverableButCannotInstall()
    {
        Assert.True((await Install()).Succeeded);
        WritePackage("2.0.0");
        _release = _release with { MinimumLauncherVersion = "2.0.0" };
        Sign();
        var candidate = Candidate();
        Assert.False(candidate.Compatible);
        Assert.Throws<InvalidDataException>(() => _source.Read("writer"));
        var plan = Assert.Single(Updater().FindUpdates([candidate]));
        Assert.Contains("launcher", (await Updater().ApplyAsync(plan)).Message);
    }

    private sealed class CatalogHttp(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(send(request));
    }
    private static HttpResponseMessage JsonResponse(object value) => new(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    private object ReleaseEntry(string tag, bool draft = false, bool prerelease = false) => new
    {
        tag_name = tag, draft, prerelease,
        assets = new[] { new { name = "office-release.json" }, new { name = "office-release.signature.json" } }
    };

    [Fact]
    public async Task CatalogPaginatesFiltersAndUsesEtagAndDailyCache()
    {
        WritePackage("1.1.0");
        int requests = 0, conditional = 0;
        var now = DateTimeOffset.UtcNow;
        using var client = new HttpClient(new CatalogHttp(request =>
        {
            requests++;
            string url = request.RequestUri!.AbsoluteUri;
            if (url.Contains("/download/")) return new(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(Path.Combine(_bundle,
                url.EndsWith("signature.json") ? "office-release.signature.json" : "office-release.json"))) };
            if (request.Headers.IfNoneMatch.Count != 0) { conditional++; return new(HttpStatusCode.NotModified); }
            var response = url.Contains("page=2") ? JsonResponse(new[] { ReleaseEntry("office-v1.1.0") })
                : JsonResponse(new[] { ReleaseEntry("BPP-9.0.0"), ReleaseEntry("office-v8.0.0", draft: true), ReleaseEntry("office-v2.0.0-preview.1", prerelease: true) });
            response.Headers.ETag = new("\"fixture\"");
            if (!url.Contains("page=2")) response.Headers.Add("Link", "<https://api.github.com/repos/Broiler-Platform/Broiler/releases?per_page=100&page=2>; rel=\"next\"");
            return response;
        }));
        var catalog = new OfficeReleaseCatalog(client, Path.Combine(_root, "catalog"), _verifier, () => now);
        var first = await catalog.CheckAsync("stable");
        Assert.Equal("1.1.0", Assert.Single(first.Releases).Release.OfficeVersion);
        int afterFirst = requests;
        Assert.Single((await catalog.CheckAsync("stable")).Releases);
        Assert.Equal(afterFirst, requests);
        now += TimeSpan.FromDays(1.1);
        Assert.Single((await catalog.CheckAsync("stable")).Releases);
        Assert.Equal(2, conditional);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task CatalogFailuresBackOffEvenForManualChecks(int status)
    {
        int calls = 0;
        using var client = new HttpClient(new CatalogHttp(_ => { calls++; return new((HttpStatusCode)status); }));
        var catalog = new OfficeReleaseCatalog(client, Path.Combine(_root, "catalog"), _verifier);
        Assert.Empty((await catalog.CheckAsync("stable", true)).Releases);
        Assert.Empty((await catalog.CheckAsync("stable", true)).Releases);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CatalogRejectsUnexpectedPaginationAndTamperedSignatures()
    {
        using var badLink = new HttpClient(new CatalogHttp(_ =>
        {
            var response = JsonResponse(Array.Empty<object>());
            response.Headers.Add("Link", "<https://example.org/releases>; rel=\"next\"");
            return response;
        }));
        Assert.Contains("pagination", (await new OfficeReleaseCatalog(badLink, Path.Combine(_root, "links"), _verifier).CheckAsync("stable")).Message);
        File.AppendAllText(Path.Combine(_bundle, "office-release.json"), " ");
        Assert.Throws<InvalidDataException>(() => Candidate());
    }

    [Fact]
    public async Task InvalidReleaseCannotHideValidReleaseAndOfflineCacheIsReverified()
    {
        WritePackage("1.1.0");
        bool offline = false;
        using var client = new HttpClient(new CatalogHttp(request =>
        {
            if (offline) throw new HttpRequestException("Offline fixture");
            string url = request.RequestUri!.AbsoluteUri;
            if (!url.Contains("/download/")) return JsonResponse(new[] { ReleaseEntry("office-v9.0.0"), ReleaseEntry("office-v1.1.0") });
            // The high-version release deliberately serves another release's signed manifest.
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(Path.Combine(_bundle,
                url.EndsWith("signature.json") ? "office-release.signature.json" : "office-release.json"))) };
        }));
        string cache = Path.Combine(_root, "offline-cache");
        var catalog = new OfficeReleaseCatalog(client, cache, _verifier);
        var result = await catalog.CheckAsync("stable", true);
        Assert.Equal("1.1.0", Assert.Single(result.Releases).Release.OfficeVersion);
        Assert.Contains("rejected", result.Message);
        offline = true;
        Assert.Single((await catalog.CheckAsync("stable", true)).Releases);
        File.AppendAllText(Path.Combine(cache, "office-v1.1.0", "office-release.json"), " ");
        Assert.Empty((await catalog.CheckAsync("stable", true)).Releases);
    }
}
