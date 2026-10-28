[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $Bundle,
    [Parameter(Mandatory = $true)][string] $OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$bundlePath = (Resolve-Path -LiteralPath $Bundle).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'BOSS lifecycle smoke output must be a new directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
$core = [Security.SecurityElement]::Escape((Join-Path $bundlePath 'launcher/Broiler.Launcher.Core.dll'))
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><Reference Include="Broiler.Launcher.Core"><HintPath>$core</HintPath></Reference></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $output 'Lifecycle.csproj') -Encoding utf8NoBOM
'<configuration><packageSources><clear/></packageSources></configuration>' | Set-Content -LiteralPath (Join-Path $output 'NuGet.config')
@'
using System.Diagnostics;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Broiler.Launcher.Models;
using Broiler.Launcher.Installation;
using Broiler.Launcher.Services.Boss;

var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
var options = new BossSettings { ListenUrl = $"http://127.0.0.1:{port}/", StartupTimeoutSeconds = 45 };
var runner = new RecordingRunner();
var resolver = new BossResolver(new EmptyInventory(), Path.Combine(args[0], "launcher"), Path.GetTempPath());
var logDirectory = Path.Combine(args[1], "logs");
BossController Create(IBossResolver? chosenResolver = null) => new(() => options, chosenResolver ?? resolver, BossHealthProbe.CreateDefault(), runner, logDirectory,
    _ => throw new Exception("The lifecycle smoke must not open a browser."));
async Task Send(string token) {
    using var pipe = new NamedPipeClientStream(".", runner.Launch!.PipeName, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    await pipe.ConnectAsync(deadline.Token);
    await pipe.WriteAsync(Encoding.UTF8.GetBytes(token + "\n"), deadline.Token);
    await pipe.FlushAsync(deadline.Token);
}
void Require(bool value, string message) { if (!value) throw new Exception(message); }
try {
    using (var first = Create()) {
        var ready = await first.StartAsync();
        Require(ready.Status == BossStatus.Running && ready.IsOwned, "Owned BOSS did not become ready: " + ready.Message);
        Require((await first.StartAsync()).Status == BossStatus.Running && runner.Starts == 1, "Repeated start spawned a duplicate.");
        await Send(new string('0', 64));
        Require((await first.RefreshAsync()).Status == BossStatus.Running, "An invalid control token stopped the server.");
        Require((await first.StopAsync()).Status == BossStatus.Stopped, "Graceful stop failed.");
        Require(runner.Observer!.HasExited, "The stopped child remained alive.");
        Require(File.ReadAllText(ready.LogPath!).Contains("stopped gracefully"), "The graceful control path was not used.");
        Console.WriteLine("Owned start/reuse, rejected invalid token and graceful shutdown passed.");
    }
    var persistent = Create();
    var detached = await persistent.StartAsync();
    Require(detached.Status == BossStatus.Running, "Second BOSS start failed.");
    persistent.Dispose();
    Require(!runner.Observer!.HasExited, "Default launcher disposal stopped BOSS.");
    using (var next = Create()) {
        Require((await next.StartAsync()).Status == BossStatus.AlreadyRunning && !next.Current.IsOwned, "A new launcher claimed an existing instance.");
        await next.StopAsync();
        Require(!runner.Observer.HasExited, "An unowned instance was stopped.");
    }
    using (var log = new FileStream(detached.LogPath!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
    using (var reader = new StreamReader(log)) Require(reader.ReadToEnd().Contains("BOSS"), "Independent server log is empty.");
    await Send(runner.Launch!.Token);
    using var stopDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
    await runner.Observer.WaitForExitAsync(stopDeadline.Token);
    // Exercise installed-scope discovery using complete real payloads in isolated fixture roots.
    // This validates launch/content-root behavior without granting privileges or touching ProgramData.
    var paths = new OfficeInstallPaths(Path.Combine(args[1], "user"), Path.Combine(args[1], "machine"));
    var packageSource = new BundledOfficePackageSource(args[0], OfficeReleaseVerifier.CreateDefault());
    var inventory = new InstallationInventory(paths, _ => true);
    var installedResolver = new BossResolver(inventory, Path.Combine(args[0], "launcher"), Path.GetTempPath());
    foreach (var scope in new[] { InstallScope.Machine, InstallScope.User }) {
        var installer = new ProductInstaller(paths, new FixtureAccess(args[1]));
        var installed = await installer.InstallAsync(packageSource.Read("boss"), scope);
        Require(installed.Succeeded, "Fixture installation failed: " + installed.Message);
        using var scoped = Create(installedResolver);
        Require((await scoped.StartAsync()).Status == BossStatus.Running, "Installed BOSS did not become ready.");
        Require(runner.Launch!.Resolution.Origin == scope.ToString(), "BOSS resolved the wrong installation scope.");
        Require((await scoped.StopAsync()).Status == BossStatus.Stopped, "Installed BOSS did not stop gracefully.");
    }
    File.WriteAllText(Path.Combine(args[1], "boss-lifecycle-results.json"), JsonSerializer.Serialize(new {
        ownedStart = true, duplicateStartPrevented = true, invalidTokenRejected = true, gracefulStop = true,
        persistsAfterLauncherDispose = true, externalOwnershipProtected = true,
        isolatedUserScopeStart = true, isolatedMachineScopeStart = true, logPath = detached.LogPath
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Persistent log/lifetime and external-instance ownership checks passed.");
} finally {
    // Observer is a handle to the exact child started by this smoke. Never enumerate or kill other servers.
    if (runner.Observer is { } child) { if (!child.HasExited) { child.Kill(true); child.WaitForExit(); } child.Dispose(); }
}
sealed class RecordingRunner : IBossProcessRunner {
    public int Starts;
    public BossLaunch? Launch;
    public Process? Observer;
    public IBossProcess Start(BossLaunch launch) {
        var child = new BossProcessRunner().Start(launch);
        Observer?.Dispose(); Observer = Process.GetProcessById(child.ProcessId); Launch = launch; Starts++;
        return child;
    }
}
sealed class EmptyInventory : IInstallationInventory {
    public InstallationInfo Find(string productId, InstallScope scope) => new(productId, scope, InstallationState.NotInstalled);
}
sealed class FixtureAccess(string root) : IInstallAccess {
    public void Prepare(InstallScope scope, OfficeInstallPaths paths) {
        if (!paths.Root(scope).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Fixture installation escaped its output directory.");
        Directory.CreateDirectory(paths.Root(scope));
    }
}
'@ | Set-Content -LiteralPath (Join-Path $output 'Program.cs') -Encoding utf8NoBOM
& dotnet run --project (Join-Path $output 'Lifecycle.csproj') -- $bundlePath $output
if ($LASTEXITCODE -ne 0) { throw "BOSS lifecycle smoke failed; diagnostics at $output" }
