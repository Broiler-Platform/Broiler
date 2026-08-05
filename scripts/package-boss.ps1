<#
.SYNOPSIS
    Lays the BOSS packaging assets around a published Broiler.Office.Server.

.DESCRIPTION
    `dotnet publish` produces the server, appsettings.json and the vendored Writer wwwroot. This
    script adds everything a user needs around it — the README, the foreground launcher, and the
    service unit files / install scripts for the target platform — turning the publish output into
    a complete Broiler Preview Package (BPP).

    The server publishes into the package root rather than a BOSS/ subfolder, so these assets land
    in the same flat directory as Broiler.Browser and Broiler.Writer.

    Assets come from Broiler.Office.Server/packaging: common/ everywhere, then linux/ or windows/.
    Only the platform's own half is copied, so a user never has to work out which files apply.

    Used by .github/workflows/broiler-preview-package.yml; run it by hand to reproduce a package.

.PARAMETER PackageDirectory
    The package root to lay the assets into (…/BPP-<os>-<variant>), which is also the directory
    `dotnet publish` wrote the server into.

.PARAMETER Platform
    linux or windows — selects which half of the packaging tree ships.

.EXAMPLE
    ./scripts/package-boss.ps1 -PackageDirectory /tmp/packages/BPP-Linux-self-contained -Platform linux
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $PackageDirectory,
    [Parameter(Mandatory = $true)][ValidateSet('linux', 'windows')][string] $Platform
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$packagingRoot = Join-Path $repoRoot 'Broiler.Office.Server/packaging'

if (-not (Test-Path -LiteralPath $PackageDirectory)) {
    throw "Publish output not found: $PackageDirectory"
}
if (-not (Test-Path -LiteralPath $packagingRoot)) {
    throw "Packaging assets not found: $packagingRoot"
}

$executableName = if ($Platform -eq 'windows') { 'Broiler.Office.Server.exe' } else { 'Broiler.Office.Server' }
$executable = Join-Path $PackageDirectory $executableName
if (-not (Test-Path -LiteralPath $executable)) {
    throw "Expected server executable was not produced: $executable"
}

# The whole point of the vendored-publish hosting model: without wwwroot the server starts, answers
# /healthz, and 404s every page. Fail here rather than shipping that.
$indexHtml = Join-Path $PackageDirectory 'wwwroot/index.html'
if (-not (Test-Path -LiteralPath $indexHtml)) {
    throw "The Writer bundle is missing from the publish output: $indexHtml"
}

# The WebAssembly SDK rewrites index.html with an import map pointing at the fingerprinted runtime.
# The raw placeholder from the project's wwwroot has neither, and an app built from it cannot boot —
# which is exactly what a hosted (non-vendored) publish would have produced.
if ((Get-Content -LiteralPath $indexHtml -Raw) -notmatch '<script type="importmap">') {
    throw "The vendored index.html has no import map — the Writer bundle was not published/transformed correctly: $indexHtml"
}

Write-Host "==> laying BOSS packaging assets into $PackageDirectory ($Platform)"

Copy-Item -LiteralPath (Join-Path $packagingRoot 'common/README.md') -Destination $PackageDirectory -Force

if ($Platform -eq 'linux') {
    Copy-Item -LiteralPath (Join-Path $packagingRoot 'linux/run-boss.sh') -Destination $PackageDirectory -Force
    Copy-Item -LiteralPath (Join-Path $packagingRoot 'linux/service') -Destination $PackageDirectory -Recurse -Force

    # tar preserves the mode, so set it here rather than asking users to chmod after extracting.
    if ($IsLinux -or $IsMacOS) {
        chmod 0755 $executable (Join-Path $PackageDirectory 'run-boss.sh')
        chmod 0755 (Join-Path $PackageDirectory 'service/install-service.sh') `
                   (Join-Path $PackageDirectory 'service/uninstall-service.sh') `
                   (Join-Path $PackageDirectory 'service/sysv/broiler-office-server')
    }
}
else {
    Copy-Item -LiteralPath (Join-Path $packagingRoot 'windows/Start-BOSS.cmd') -Destination $PackageDirectory -Force
    Copy-Item -LiteralPath (Join-Path $packagingRoot 'windows/service') -Destination $PackageDirectory -Recurse -Force
}

$launcher = if ($Platform -eq 'windows') { 'Start-BOSS.cmd' } else { 'run-boss.sh' }
foreach ($asset in @($executableName, 'appsettings.json', 'README.md', $launcher)) {
    if (-not (Test-Path -LiteralPath (Join-Path $PackageDirectory $asset))) {
        throw "Packaging did not produce the expected asset: $asset"
    }
    Write-Host "    $asset"
}

Get-ChildItem -LiteralPath (Join-Path $PackageDirectory 'service') -Recurse -File |
    ForEach-Object { Write-Host "    service/$([IO.Path]::GetRelativePath((Join-Path $PackageDirectory 'service'), $_.FullName) -replace '\\', '/')" }

$bundleSize = (Get-ChildItem -LiteralPath (Join-Path $PackageDirectory 'wwwroot') -Recurse -File |
    Measure-Object -Property Length -Sum).Sum
Write-Host ("    wwwroot/ ({0:N1} MB Writer bundle)" -f ($bundleSize / 1MB))
