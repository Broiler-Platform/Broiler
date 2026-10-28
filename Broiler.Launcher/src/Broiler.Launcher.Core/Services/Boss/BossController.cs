using System.Diagnostics;
using System.Security.Cryptography;
using Broiler.Launcher.Installation;
using Broiler.Launcher.Models;

namespace Broiler.Launcher.Services.Boss;

public enum BossStatus { Stopped, Starting, Running, AlreadyRunning, Missing, PortConflict, Failed, Stopping }
public sealed record BossState(BossStatus Status, string Message, Uri? Address = null, bool IsOwned = false, string? LogPath = null, int? ProcessId = null)
{
    public string? ExecutablePath { get; init; }
}
public interface IBossController : IDisposable
{
    BossState Current { get; }
    event Action<BossState>? StateChanged;
    Task<BossState> StartAsync(CancellationToken token = default);
    Task<BossState> RefreshAsync(CancellationToken token = default);
    Task<BossState> StopAsync(CancellationToken token = default);
    Task<BossState> OpenBrowserAsync(CancellationToken token = default);
    void OpenLog();
}

public sealed class BossController(Func<BossSettings> settings, IBossResolver resolver, IBossHealthProbe probe,
    IBossProcessRunner runner, string logDirectory, Action<Uri>? openBrowser = null) : IBossController
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private IBossProcess? _owned;
    private string? _instanceId;
    private Uri? _ownedAddress;
    private string? _ownedExecutable;
    private string? _lastLog;
    private bool _disposed;
    private BossState _state = new(BossStatus.Stopped, "BOSS has not been started.");
    public BossState Current => Volatile.Read(ref _state);
    public event Action<BossState>? StateChanged;
    public static BossController CreateDefault(IAppLauncherService service) => new(
        () => (service.Settings.Boss ?? new BossSettings()).Clone(), BossResolver.CreateDefault(), BossHealthProbe.CreateDefault(),
        new BossProcessRunner(), Path.Combine(OfficeInstallPaths.CreateDefault().UserRoot, "boss"));

    private BossState Set(BossStatus status, string message, Uri? uri = null, bool owned = false, string? log = null)
    {
        var state = new BossState(status, message, uri, owned, log, owned ? _owned?.ProcessId : null) { ExecutablePath = owned ? _ownedExecutable : null };
        Volatile.Write(ref _state, state);
        StateChanged?.Invoke(state);
        return state;
    }
    private void Log(string message)
    {
        if (_lastLog is null) return;
        try
        {
            using var stream = new FileStream(_lastLog, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream);
            writer.WriteLine($"{DateTimeOffset.UtcNow:O} Launcher: {message}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    private void ReleaseOwned()
    {
        _owned?.Dispose();
        _owned = null;
        _instanceId = null;
        _ownedAddress = null;
        _ownedExecutable = null;
    }
    private async Task StopOwnedAsync(CancellationToken token)
    {
        if (_owned is null) return;
        if (!_owned.HasExited)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                await _owned.RequestStopAsync(deadline.Token).ConfigureAwait(false);
                await _owned.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
                Log("BOSS stopped gracefully.");
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException)
            {
                token.ThrowIfCancellationRequested();
                Log("Graceful shutdown did not complete; terminating only this launcher's child.");
                _owned.Kill();
                using var killDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await _owned.WaitForExitAsync(killDeadline.Token).ConfigureAwait(false);
            }
        }
        ReleaseOwned();
    }
    private async Task<FileStream> AcquireStartLockAsync(int port, CancellationToken token)
    {
        OfficeInstallPaths.RejectLinks(logDirectory);
        Directory.CreateDirectory(logDirectory);
        string path = Path.Combine(logDirectory, $"start-{port}.lock");
        OfficeInstallPaths.RejectLinks(path);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try { return new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { await Task.Delay(100, token).ConfigureAwait(false); }
        }
    }
    public async Task<BossState> StartAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        bool spawned = false;
        Uri? uri = null;
        try
        {
            var options = settings().Clone();
            uri = options.Validate();
            if (_owned?.HasExited == true) ReleaseOwned();
            if (_owned is not null && _ownedAddress != uri)
                return Set(BossStatus.Running, "BOSS is still running at its original address; stop it before changing the listener.", _ownedAddress, true, _lastLog);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(options.StartupTimeoutSeconds));
            Set(BossStatus.Starting, "Checking BOSS…", uri);
            using var startLock = await AcquireStartLockAsync(uri.Port, deadline.Token).ConfigureAwait(false);
            var existing = await probe.ProbeAsync(uri, deadline.Token).ConfigureAwait(false);
            if (_owned?.HasExited == true) ReleaseOwned();
            if (existing.Kind == BossProbeKind.Healthy)
            {
                bool owned = _owned is not null && existing.InstanceId == _instanceId;
                return Ready(owned ? BossStatus.Running : BossStatus.AlreadyRunning, owned ? "BOSS is running." : "BOSS is already running; reusing the existing instance.", uri, owned, options.OpenBrowserOnStart);
            }
            if (existing.Kind == BossProbeKind.Conflict)
                return Set(BossStatus.PortConflict, $"BOSS port conflict: {existing.Error}", uri, _owned is not null, _owned is not null ? _lastLog : null);
            if (_owned is null)
            {
                var resolution = resolver.Resolve(options);
                if (!resolution.Available) return Set(BossStatus.Missing, resolution.Error ?? "BOSS is missing.", uri);
                _instanceId = Guid.NewGuid().ToString("N");
                _lastLog = Path.Combine(logDirectory, $"boss-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{_instanceId}.log");
                using (var log = new FileStream(_lastLog, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite)) { }
                Log($"Starting {resolution.Path} ({resolution.Origin}) at {uri}.");
                _owned = runner.Start(new(resolution, uri, _instanceId, "broiler-boss-" + _instanceId,
                    Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), _lastLog) { AdditionalArguments = options.ArgumentTokens() });
                _ownedAddress = uri;
                _ownedExecutable = resolution.Path;
                spawned = true;
            }
            Set(BossStatus.Starting, "Starting BOSS; waiting for the web client…", uri, true, _lastLog);
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                var health = await probe.ProbeAsync(uri, deadline.Token).ConfigureAwait(false);
                if (health.Kind == BossProbeKind.Healthy && health.InstanceId == _instanceId && !_owned.HasExited)
                {
                    return Ready(BossStatus.Running, "BOSS is ready. Open it in your browser.", uri, true, options.OpenBrowserOnStart);
                }
                if (health.Kind == BossProbeKind.Healthy)
                {
                    await StopOwnedAsync(CancellationToken.None).ConfigureAwait(false);
                    if (health.InstanceId is null || (await probe.ProbeAsync(uri, deadline.Token).ConfigureAwait(false)).Kind != BossProbeKind.Healthy)
                        throw new IOException("BOSS ownership identity did not match. Publish the matching server build with launcher lifecycle support.");
                    return Ready(BossStatus.AlreadyRunning, "Another BOSS instance became ready; reusing it.", uri, false, options.OpenBrowserOnStart);
                }
                if (_owned.HasExited) throw new IOException($"BOSS exited during startup (code {_owned.ExitCode}). Check required runtimes and the log.");
                if (health.Kind == BossProbeKind.Conflict) throw new IOException($"BOSS readiness failed: {health.Error}");
                await Task.Delay(100, deadline.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log(ex.Message);
            if (spawned) { try { await StopOwnedAsync(CancellationToken.None).ConfigureAwait(false); } catch (Exception cleanup) { Log(cleanup.Message); } }
            return Set(BossStatus.Failed, ex is OperationCanceledException ? "BOSS start canceled or timed out. See the log." : $"BOSS start failed: {ex.Message}", uri, _owned is not null, _lastLog);
        }
        finally { _gate.Release(); }
    }
    public async Task<BossState> RefreshAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var options = settings().Clone();
            var uri = options.Validate();
            if (_owned is not null && !_owned.HasExited && _ownedAddress != uri)
                return Set(BossStatus.Running, "BOSS is running at its original address; stop it before applying the new listener setting.", _ownedAddress, true, _lastLog);
            var health = await probe.ProbeAsync(uri, token).ConfigureAwait(false);
            if (_owned?.HasExited == true) { Log($"Process exited with code {_owned.ExitCode}."); ReleaseOwned(); }
            if (health.Kind == BossProbeKind.Healthy)
            {
                bool owned = _owned is not null && _instanceId == health.InstanceId;
                return Set(owned ? BossStatus.Running : BossStatus.AlreadyRunning, owned ? "BOSS is running." : "Existing BOSS instance detected (not managed by this launcher).", uri, owned, owned ? _lastLog : null);
            }
            if (health.Kind == BossProbeKind.Conflict) return Set(BossStatus.PortConflict, health.Error ?? "BOSS port is occupied.", uri, _owned is not null, _lastLog);
            var resolution = resolver.Resolve(options);
            return Set(resolution.Available ? BossStatus.Stopped : BossStatus.Missing,
                resolution.Available ? "BOSS is stopped." : resolution.Error ?? "BOSS is missing.", uri, _owned is not null, _lastLog);
        }
        catch (Exception ex) { return Set(BossStatus.Failed, ex.Message, log: _lastLog); }
        finally { _gate.Release(); }
    }
    public async Task<BossState> StopAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_owned is null) return Set(Current.Status, "This launcher does not own BOSS. Manage an external service through its administrator tools.", Current.Address);
            Set(BossStatus.Stopping, "Stopping this launcher's BOSS instance…", Current.Address, true, _lastLog);
            await StopOwnedAsync(token).ConfigureAwait(false);
            return Set(BossStatus.Stopped, "BOSS stopped.", Current.Address, log: _lastLog);
        }
        catch (Exception ex) { return Set(BossStatus.Failed, $"BOSS stop failed: {ex.Message}", Current.Address, _owned is not null, _lastLog); }
        finally { _gate.Release(); }
    }
    private void OpenBrowser(Uri uri) => (openBrowser ?? (value => { using var process = Process.Start(new ProcessStartInfo(value.AbsoluteUri) { UseShellExecute = true }); }))(uri);
    private BossState Ready(BossStatus status, string message, Uri uri, bool owned, bool open)
    {
        if (open)
        {
            try { OpenBrowser(uri); }
            catch (Exception ex) { message += $" Browser could not be opened: {ex.Message}"; }
        }
        return Set(status, message, uri, owned, owned ? _lastLog : null);
    }
    public async Task<BossState> OpenBrowserAsync(CancellationToken token = default)
    {
        var state = await RefreshAsync(token).ConfigureAwait(false);
        if (state.Status is not (BossStatus.Running or BossStatus.AlreadyRunning)) return state;
        try { OpenBrowser(state.Address!); return state; }
        catch (Exception ex) { return Set(state.Status, $"BOSS is ready, but the browser could not be opened: {ex.Message}", state.Address, state.IsOwned, state.LogPath); }
    }
    public void OpenLog()
    {
        if (Current.LogPath is not string path || !File.Exists(path)) return;
        using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _gate.Wait();
        try
        {
            if (!settings().KeepRunningOnLauncherExit) StopOwnedAsync(CancellationToken.None).GetAwaiter().GetResult();
            else ReleaseOwned(); // The child's independent file log/control server outlive this handle.
        }
        finally { _gate.Release(); _lifetime.Dispose(); }
    }
}
