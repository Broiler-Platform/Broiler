using Broiler.Launcher.Models;
using Broiler.Launcher.Services.Boss;
using Xunit;

namespace Broiler.Launcher.Tests;

public sealed class BossControllerTests : IDisposable
{
    [Fact]
    public async Task ValidatedLoggingArgumentsReachTheOwnedProcessAsTokens()
    {
        _settings.AdditionalArguments = "--Logging:LogLevel:Default Warning";
        using var controller = Controller();
        Assert.True((await controller.StartAsync()).IsOwned);
        Assert.Equal(new[] { "--Logging:LogLevel:Default", "Warning" }, _runner.Launch!.AdditionalArguments);
    }
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boss-controller-" + Guid.NewGuid().ToString("N"));
    private readonly FakeRunner _runner = new();
    private readonly FakeResolver _resolver = new();
    private readonly FakeProbe _probe;
    private readonly BossSettings _settings = new() { StartupTimeoutSeconds = 1 };
    private int _browserOpens;
    public BossControllerTests() { Directory.CreateDirectory(_root); _probe = new(_runner); }
    public void Dispose() => Directory.Delete(_root, true);
    private BossController Controller() => new(() => _settings, _resolver, _probe, _runner, _root, _ => _browserOpens++);

    [Fact]
    public async Task ConcurrentAndRepeatedStartsSpawnOnlyOneProcess()
    {
        using var controller = Controller();
        var states = await Task.WhenAll(controller.StartAsync(), controller.StartAsync());
        Assert.All(states, state => Assert.Equal(BossStatus.Running, state.Status));
        Assert.True(controller.Current.IsOwned);
        Assert.Equal(1, _runner.Starts);
        Assert.Equal(BossStatus.Running, (await controller.StartAsync()).Status);
        Assert.Equal(0, _browserOpens);
        await controller.OpenBrowserAsync();
        Assert.Equal(1, _browserOpens);
        Assert.NotNull(controller.Current.LogPath);
    }

    [Fact]
    public async Task ExistingServiceIsReusedEvenWithoutLocalBinaryAndCannotBeStopped()
    {
        _probe.External = new(BossProbeKind.Healthy);
        _resolver.Available = false;
        using var controller = Controller();
        Assert.Equal(BossStatus.AlreadyRunning, (await controller.StartAsync()).Status);
        Assert.False(controller.Current.IsOwned);
        Assert.Equal(0, _runner.Starts);
        Assert.Equal(0, _resolver.Calls);
        Assert.Contains("does not own", (await controller.StopAsync()).Message);
        await controller.OpenBrowserAsync();
        Assert.Equal(1, _browserOpens);
    }

    [Fact]
    public async Task WrongListenerNeverStartsAnotherProcessOrOpensBrowser()
    {
        _probe.External = new(BossProbeKind.Conflict, Error: "Different application.");
        using var controller = Controller();
        Assert.Equal(BossStatus.PortConflict, (await controller.StartAsync()).Status);
        await controller.OpenBrowserAsync();
        Assert.Equal(0, _browserOpens);
        Assert.Equal(0, _runner.Starts);
    }

    [Fact]
    public async Task MissingBossOffersExplicitInstallation()
    {
        _resolver.Available = false;
        using var controller = Controller();
        Assert.Equal(BossStatus.Missing, (await controller.StartAsync()).Status);
        Assert.Contains("Install", controller.Current.Message);
        Assert.Equal(0, _runner.Starts);
    }

    [Fact]
    public async Task StartupTimeoutCleansOnlyOwnedChildAndPreservesLog()
    {
        _probe.NeverReady = true;
        using var controller = Controller();
        Assert.Equal(BossStatus.Failed, (await controller.StartAsync()).Status);
        Assert.True(_runner.Child!.HasExited);
        Assert.Equal(1, _runner.Child.StopRequests);
        Assert.False(controller.Current.IsOwned);
        Assert.True(File.Exists(controller.Current.LogPath));
    }

    [Fact]
    public async Task EarlyExitReportsExitCode()
    {
        _runner.ExitImmediately = true;
        using var controller = Controller();
        Assert.Equal(BossStatus.Failed, (await controller.StartAsync()).Status);
        Assert.Contains("code 7", controller.Current.Message);
        Assert.False(controller.Current.IsOwned);
    }

    [Fact]
    public async Task OlderOwnedServerWithoutLifecycleIdentityCannotClaimSuccessfulReuse()
    {
        _probe.HideOwnedIdentity = true;
        using var controller = Controller();
        Assert.Equal(BossStatus.Failed, (await controller.StartAsync()).Status);
        Assert.True(_runner.Child!.HasExited);
        Assert.False(controller.Current.IsOwned);
    }

    [Fact]
    public async Task GracefulStopUsesControlChannelWithoutKilling()
    {
        using var controller = Controller();
        await controller.StartAsync();
        Assert.Equal(BossStatus.Stopped, (await controller.StopAsync()).Status);
        Assert.Equal(1, _runner.Child!.StopRequests);
        Assert.Equal(0, _runner.Child.Kills);
    }

    [Fact]
    public async Task FailedGracefulStopHasOwnedProcessFallback()
    {
        using var controller = Controller();
        await controller.StartAsync();
        _runner.Child!.FailStop = true;
        Assert.Equal(BossStatus.Stopped, (await controller.StopAsync()).Status);
        Assert.Equal(1, _runner.Child.Kills);
    }

    [Fact]
    public async Task DefaultDisposalLeavesServerRunningAndNewLauncherDoesNotClaimIt()
    {
        var controller = Controller();
        await controller.StartAsync();
        controller.Dispose();
        Assert.False(_runner.Child!.HasExited);
        Assert.Equal(0, _runner.Child.StopRequests);
        using var next = Controller();
        Assert.Equal(BossStatus.AlreadyRunning, (await next.StartAsync()).Status);
        Assert.False(next.Current.IsOwned);
        Assert.Equal(1, _runner.Starts);
    }

    [Fact]
    public async Task OptInStopOnLauncherExitStopsOnlyOwnedInstance()
    {
        _settings.KeepRunningOnLauncherExit = false;
        var controller = Controller();
        await controller.StartAsync();
        controller.Dispose();
        Assert.True(_runner.Child!.HasExited);
        Assert.Equal(1, _runner.Child.StopRequests);
    }

    [Fact]
    public async Task BrowserFailureDoesNotStopAHealthyServer()
    {
        _settings.OpenBrowserOnStart = true;
        using var controller = new BossController(() => _settings, _resolver, _probe, _runner, _root, _ => throw new IOException("No browser association."));
        var result = await controller.StartAsync();
        Assert.Equal(BossStatus.Running, result.Status);
        Assert.Contains("Browser could not", result.Message);
        Assert.False(_runner.Child!.HasExited);
    }

    [Fact]
    public async Task ListenerChangesDoNotOrphanTheCurrentlyOwnedServer()
    {
        using var controller = Controller();
        await controller.StartAsync();
        _settings.ListenUrl = "http://127.0.0.1:7777/";
        Assert.Equal(5555, (await controller.StartAsync()).Address!.Port);
        Assert.Equal(5555, (await controller.RefreshAsync()).Address!.Port);
        Assert.Equal(1, _runner.Starts);
        Assert.Equal(BossStatus.Stopped, (await controller.StopAsync()).Status);
    }

    [Fact]
    public async Task SeparateControllersShareStartupLockAndReuseEndpoint()
    {
        using var first = Controller();
        using var second = Controller();
        var states = await Task.WhenAll(first.StartAsync(), second.StartAsync());
        Assert.Contains(states, state => state.IsOwned);
        Assert.Contains(states, state => state.Status == BossStatus.AlreadyRunning);
        Assert.Equal(1, _runner.Starts);
    }

    [Theory]
    [InlineData("http://0.0.0.0:5555/")]
    [InlineData("https://127.0.0.1:5555/")]
    [InlineData("http://127.0.0.1:5555/path")]
    public async Task InvalidAddressFailsBeforeLaunching(string url)
    {
        _settings.ListenUrl = url;
        using var controller = Controller();
        Assert.Equal(BossStatus.Failed, (await controller.StartAsync()).Status);
        Assert.Equal(0, _runner.Starts);
    }

    private sealed class FakeResolver : IBossResolver
    {
        public bool Available = true;
        public int Calls;
        public BossResolution Resolve(BossSettings settings) { Calls++; return new(Available, "C:/fixture/Broiler.Office.Server.exe", false, "Fixture", Available ? null : "BOSS missing. Install for user/all."); }
    }
    private sealed class FakeRunner : IBossProcessRunner
    {
        public int Starts;
        public bool ExitImmediately;
        public FakeProcess? Child;
        public BossLaunch? Launch;
        public IBossProcess Start(BossLaunch launch) { Starts++; Launch = launch; return Child = new() { HasExited = ExitImmediately }; }
    }
    private sealed class FakeProbe(FakeRunner runner) : IBossHealthProbe
    {
        public BossProbeResult? External;
        public bool NeverReady;
        public bool HideOwnedIdentity;
        public Task<BossProbeResult> ProbeAsync(Uri address, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(External ?? (!NeverReady && runner.Child is { HasExited: false }
                ? new(BossProbeKind.Healthy, HideOwnedIdentity ? null : runner.Launch!.InstanceId) : new BossProbeResult(BossProbeKind.Absent)));
        }
    }
    private sealed class FakeProcess : IBossProcess
    {
        public int ProcessId => 123;
        public bool HasExited { get; set; }
        public int ExitCode => 7;
        public int StopRequests, Kills;
        public bool FailStop;
        public Task RequestStopAsync(CancellationToken token)
        {
            StopRequests++;
            if (FailStop) throw new TimeoutException();
            HasExited = true;
            return Task.CompletedTask;
        }
        public Task WaitForExitAsync(CancellationToken token) => HasExited ? Task.CompletedTask : Task.Delay(Timeout.Infinite, token);
        public void Kill() { Kills++; HasExited = true; }
        public void Dispose() { }
    }
}
