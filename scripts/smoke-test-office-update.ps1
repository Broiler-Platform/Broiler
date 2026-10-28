[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $Bundle,
    [string] $PreviousBundle,
    [Parameter(Mandatory = $true)][string] $OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$bundlePath = (Resolve-Path -LiteralPath $Bundle).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Update smoke output must be a new directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
if ($PreviousBundle) {
    $previousPath = (Resolve-Path -LiteralPath $PreviousBundle).Path
} else {
    # CI contract fixture: real current archives with an older metadata version.
    # Supply PreviousBundle to test two genuinely different published builds.
    $previousPath = Join-Path $output 'previous-contract-fixture'
    New-Item -ItemType Directory -Path (Join-Path $previousPath 'packages') | Out-Null
    $fixture = Get-Content -Raw -LiteralPath (Join-Path $bundlePath 'office-release.json') | ConvertFrom-Json
    $fixture.officeVersion = '0.0.0-demo.1'
    $fixture.tag = 'office-v0.0.0-demo.1'
    foreach ($component in $fixture.components) {
        $component.version = '0.0.0-demo.1'
        if ($component.id -in @('writer', 'boss')) {
            Copy-Item -LiteralPath (Join-Path $bundlePath "packages/$($component.asset)") -Destination (Join-Path $previousPath 'packages')
        }
    }
    $fixture | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $previousPath 'office-release.json') -Encoding utf8NoBOM
}
$core = [Security.SecurityElement]::Escape((Join-Path $bundlePath 'launcher/Broiler.Launcher.Core.dll'))
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><Reference Include="Broiler.Launcher.Core"><HintPath>$core</HintPath></Reference></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $output 'Update.csproj') -Encoding utf8NoBOM
'<configuration><packageSources><clear/></packageSources></configuration>' | Set-Content -LiteralPath (Join-Path $output 'NuGet.config')
@'
using System.Net;
using System.Net.Sockets;
using Broiler.Launcher.Installation;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services.Boss;

string next = args[0], previous = args[1], root = args[2];
void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
var paths = new OfficeInstallPaths(Path.Combine(root, "user"), Path.Combine(root, "machine"));
var verifier = OfficeReleaseVerifier.CreateDefault();
var source = new BundledOfficePackageSource(previous, verifier);
var installer = new ProductInstaller(paths, new UserInstallAccess());
foreach (string id in new[] { "writer", "boss" })
    Require((await installer.InstallAsync(source.Read(id), InstallScope.User)).Succeeded, "Previous install failed: " + id);
var inventory = new InstallationInventory(paths);
var oldWriter = inventory.Find("writer", InstallScope.User);
var oldBoss = inventory.Find("boss", InstallScope.User);
using var reservation = new TcpListener(IPAddress.Loopback, 0);
reservation.Start();
int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
reservation.Stop();
var settings = new BossSettings { ListenUrl = $"http://127.0.0.1:{port}/", KeepRunningOnLauncherExit = false,
    AdditionalArguments = "--Logging:LogLevel:Default Warning" };
using var boss = new BossController(() => settings, new BossResolver(inventory, root, root), BossHealthProbe.CreateDefault(), new BossProcessRunner(), Path.Combine(root, "logs"));
Require((await boss.StartAsync()).IsOwned, "Previous BOSS failed to start.");
try
{
    using var http = new HttpClient();
    var candidate = new OfficeReleaseCatalog(http, Path.Combine(root, "cache"), verifier).ReadLocal(next);
    var updates = new ProductUpdateService(paths, verifier, boss);
    var plans = updates.FindUpdates([candidate]);
    Require(plans.Count == 2, "Expected writer and BOSS upgrades.");
    foreach (var plan in plans)
    {
        var result = await updates.ApplyAsync(plan);
        Require(result.Succeeded, result.Message);
        var installed = inventory.Find(plan.Component.Id, InstallScope.User);
        Require(installed.Receipt!.Version == plan.Component.Version, "Wrong activated version.");
        Require(File.Exists(plan.Component.Id == "boss" ? oldBoss.ExecutablePath : oldWriter.ExecutablePath), "Previous payload lost.");
        Console.WriteLine($"Updated {plan.Component.Id}: {plan.Expected.Version} -> {installed.Receipt.Version}");
    }
    Require(boss.Current.IsOwned && boss.Current.ExecutablePath == inventory.Find("boss", InstallScope.User).ExecutablePath, "BOSS did not restart from updated installation.");
    Require(updates.FindUpdates([candidate]).Count == 0, "Update remained eligible after activation.");
    Require(installer.Rollback("writer", InstallScope.User, inventory.Find("writer", InstallScope.User).Receipt).Succeeded, "Writer rollback failed.");
    Require(inventory.Find("writer", InstallScope.User).Receipt == oldWriter.Receipt, "Rollback did not restore exact previous receipt.");
    Require(!Directory.Exists(paths.MachineRoot), "User update touched machine scope.");
    Console.WriteLine("Office update smoke passed: real archives, retained payloads, owned BOSS restart/readiness, exact receipt rollback, isolated user scope.");
}
finally { await boss.StopAsync(); }
'@ | Set-Content -LiteralPath (Join-Path $output 'Program.cs') -Encoding utf8NoBOM
& dotnet run --project (Join-Path $output 'Update.csproj') -- $bundlePath $previousPath $output
if ($LASTEXITCODE -ne 0) { throw "Office update smoke failed; diagnostics at $output" }
[ordered]@{
    passed = $true
    bundle = $bundlePath
    previousBundle = $previousPath
    previousIsContractFixture = [string]::IsNullOrEmpty($PreviousBundle)
    writerUpgrade = $true
    bossUpgradeAndReadiness = $true
    retainedPreviousPayloads = $true
    exactReceiptRollback = $true
    machineScopeUnchanged = $true
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'office-update-results.json') -Encoding utf8NoBOM
