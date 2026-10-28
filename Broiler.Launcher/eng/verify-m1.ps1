[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $PackageFeed
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$launcherRoot = Split-Path $PSScriptRoot -Parent
$feedPath = (Resolve-Path -LiteralPath $PackageFeed).Path
$scratch = Join-Path ([IO.Path]::GetTempPath()) ("broiler-launcher-m1-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $scratch | Out-Null
$escapedFeed = [Security.SecurityElement]::Escape($feedPath)
$configPath = Join-Path $scratch 'NuGet.config'
@"
<configuration>
  <packageSources>
    <clear />
    <add key="candidate" value="$escapedFeed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <disabledPackageSources><clear /></disabledPackageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="candidate"><package pattern="Broiler.UI*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath $configPath -Encoding utf8
$solution = Join-Path $launcherRoot 'Broiler.Windows.Launcher.slnx'
& dotnet restore $solution -p:Configuration=Release-Windows --configfile $configPath --packages (Join-Path $scratch 'packages') --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "Candidate restore failed; diagnostics at $scratch" }
& dotnet build $solution -c Release-Windows --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Candidate Windows build failed.' }
& dotnet test (Join-Path $launcherRoot 'tests/Broiler.Launcher.Tests') -c Release-Windows --no-build --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Candidate launcher tests failed.' }
Write-Output "M1 package consumer verified with an isolated cache at $scratch"
