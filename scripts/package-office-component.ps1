[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('launcher', 'boss')][string] $Product,
    [Parameter(Mandatory = $true)][string] $Version,
    [Parameter(Mandatory = $true)][string] $OutputRoot,
    [ValidateSet('win-x64', 'linux-x64', 'both')][string] $Rid = 'both',
    [string] $WriterWebLock,
    [switch] $Demo,
    [switch] $RequireClean
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $output) { throw 'Component release output must be new.' }
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$') { throw 'A semantic component version is required.' }
if ($Demo -and $Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+-demo\.[1-9][0-9]*$') { throw 'Demo components require a demo.N version.' }
if ($RequireClean -and @(& git -C $repoRoot status --porcelain).Count) { throw 'Component release requires committed sources.' }
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
$assets = Join-Path $output 'assets'
New-Item -ItemType Directory -Path $assets | Out-Null

$targetRids = if ($Rid -eq 'both') { @('win-x64', 'linux-x64') } else { @($Rid) }
$web = $null
if ($Product -eq 'boss') {
    if (!$WriterWebLock) { throw 'BOSS releases require a reviewed Writer web lock; source publishing is not a fallback.' }
    $web = Join-Path $output 'writer-web'
    & python (Join-Path $PSScriptRoot 'office_artifacts.py') fetch-web $WriterWebLock --cache (Join-Path $output 'cache') --output $web
    if ($LASTEXITCODE -ne 0) { throw 'Pinned Writer web artifact validation failed.' }
}

foreach ($targetRid in $targetRids) {
    $catalogPath = if ($targetRid -eq 'linux-x64') { Join-Path $repoRoot 'eng/office-products-linux.json' } else { Join-Path $repoRoot 'eng/office-products.json' }
    $catalog = Get-Content -Raw -LiteralPath $catalogPath | ConvertFrom-Json
    $item = @($catalog.products | Where-Object id -eq $Product)[0]
    $publish = Join-Path $output "publish-$targetRid"
    New-Item -ItemType Directory -Path $publish | Out-Null
    $configuration = if ($targetRid -eq 'win-x64') { 'Release-Windows' } else { 'Release-Linux' }
    $publishArguments = @('publish', (Join-Path $repoRoot $item.project), '-c', $configuration, '-r', $targetRid, '--self-contained', 'true',
        '--output', $publish, "-p:Version=$Version", "-p:BroilerOfficeDemo=$($Demo.ToString().ToLowerInvariant())", '-p:PublishAot=false', '-p:PublishSingleFile=false', '-p:PublishReadyToRun=true')
    if ($Product -eq 'boss') {
        $publishArguments += "-p:WriterClientArtifactDirectory=$web"
    }
    & dotnet @publishArguments
    if ($LASTEXITCODE -ne 0) { throw "Component publish failed for $targetRid." }
    if ($Product -eq 'boss') {
        $platform = if ($targetRid -eq 'win-x64') { 'windows' } else { 'linux' }
        & (Join-Path $PSScriptRoot 'package-boss.ps1') -PackageDirectory $publish -Platform $platform
        if ($targetRid -eq 'win-x64') {
            @'
@echo off
"%~dp0Broiler.Office.Server.exe" --urls http://127.0.0.1:5555 %*
'@ | Set-Content -LiteralPath (Join-Path $publish 'Start-BOSS.cmd') -Encoding ascii
        }
        if ($targetRid -eq 'linux-x64') {
            $shContent = "#!/bin/sh`nDIR=`"`$(cd `"`$(dirname `"`$0`")`" && pwd)`"`n`"`$DIR/Broiler.Office.Server`" --urls http://127.0.0.1:5555 `"`$@`"`n"
            [System.IO.File]::WriteAllText((Join-Path $publish 'start-boss.sh'), $shContent, [System.Text.UTF8Encoding]::new($false))
        }
        if (($targetRid -eq 'win-x64' -and $IsWindows) -or ($targetRid -eq 'linux-x64' -and $IsLinux)) {
            & (Join-Path $PSScriptRoot 'smoke-test-boss.ps1') -PackageDirectory $publish -Port 15556
        }
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $publish 'LICENSE')
    $archive = Join-Path $assets "Broiler-$Product-$Version-$targetRid.zip"
    [IO.Compression.ZipFile]::CreateFromDirectory($publish, $archive, [IO.Compression.CompressionLevel]::Optimal, $false)
}

$arguments = @((Join-Path $repoRoot 'eng/write-component-manifest.py'), '--directory', $assets, '--product', $Product, '--version', $Version,
    '--repository', 'Broiler-Platform/Broiler', '--tag', "$Product-v$Version", '--commit', $commit)
if ($Demo) { $arguments += '--demo' }
if ($Product -eq 'boss') { $arguments += @('--dependency-lock', $WriterWebLock) }
& python @arguments
if ($LASTEXITCODE -ne 0) { throw 'Component artifact metadata failed.' }
Write-Output "Reusable component release assets: $assets"
