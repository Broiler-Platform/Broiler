[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('launcher', 'boss')][string] $Product,
    [Parameter(Mandatory = $true)][string] $Version,
    [Parameter(Mandatory = $true)][string] $OutputRoot,
    [string] $WriterWebLock,
    [switch] $Demo,
    [switch] $RequireClean
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $output) { throw 'Component release output must be new.' }
if (!$IsWindows) { throw 'Office-owned components currently target Windows x64.' }
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$') { throw 'A semantic component version is required.' }
if ($Demo -and $Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+-demo\.[1-9][0-9]*$') { throw 'Demo components require a demo.N version.' }
if ($RequireClean -and @(& git -C $repoRoot status --porcelain).Count) { throw 'Component release requires committed sources.' }
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
$catalog = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'eng/office-products.json') | ConvertFrom-Json
$item = @($catalog.products | Where-Object id -eq $Product)[0]
$publish = Join-Path $output 'publish'
New-Item -ItemType Directory -Path $output | Out-Null
$publishArguments = @('publish', (Join-Path $repoRoot $item.project), '-c', 'Release-Windows', '-r', 'win-x64', '--self-contained', 'true',
    '--output', $publish, "-p:Version=$Version", "-p:BroilerOfficeDemo=$($Demo.ToString().ToLowerInvariant())", '-p:PublishAot=false', '-p:PublishSingleFile=false', '-p:PublishReadyToRun=true')
if ($Product -eq 'boss') {
    if (!$WriterWebLock) { throw 'BOSS releases require a reviewed Writer web lock; source publishing is not a fallback.' }
    $web = Join-Path $output 'writer-web'
    & python (Join-Path $PSScriptRoot 'office_artifacts.py') fetch-web $WriterWebLock --cache (Join-Path $output 'cache') --output $web
    if ($LASTEXITCODE -ne 0) { throw 'Pinned Writer web artifact validation failed.' }
    $publishArguments += "-p:WriterClientArtifactDirectory=$web"
}
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw 'Component publish failed.' }
if ($Product -eq 'boss') {
    & (Join-Path $PSScriptRoot 'package-boss.ps1') -PackageDirectory $publish -Platform windows
    @'
@echo off
"%~dp0Broiler.Office.Server.exe" --urls http://127.0.0.1:5555 %*
'@ | Set-Content -LiteralPath (Join-Path $publish 'Start-BOSS.cmd') -Encoding ascii
    & (Join-Path $PSScriptRoot 'smoke-test-boss.ps1') -PackageDirectory $publish -Port 15556
}
$assets = Join-Path $output 'assets'
New-Item -ItemType Directory -Path $assets | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $publish 'LICENSE')
$archive = Join-Path $assets "Broiler-$Product-$Version-win-x64.zip"
[IO.Compression.ZipFile]::CreateFromDirectory($publish, $archive, [IO.Compression.CompressionLevel]::Optimal, $false)
$arguments = @((Join-Path $repoRoot 'eng/write-component-manifest.py'), '--directory', $assets, '--product', $Product, '--version', $Version,
    '--repository', 'Broiler-Platform/Broiler', '--tag', "$Product-v$Version", '--commit', $commit)
if ($Demo) { $arguments += '--demo' }
if ($Product -eq 'boss') { $arguments += @('--dependency-lock', $WriterWebLock) }
& python @arguments
if ($LASTEXITCODE -ne 0) { throw 'Component artifact metadata failed.' }
Write-Output "Reusable component release assets: $assets"
