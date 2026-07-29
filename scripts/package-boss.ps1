<#
.SYNOPSIS
    Lays the BOSS packaging assets around a published Broiler.Office.Server.

.DESCRIPTION
    `dotnet publish` produces the server, appsettings.json and the vendored Writer wwwroot. This
    script adds everything a user needs around it — the README, the foreground launcher, and the
    service unit files / install scripts for the target platform — turning the publish output into
    the BOSS/ folder that ships inside a Broiler Preview Package (BPP).

    Assets come from Broiler.Office.Server/packaging: common/ everywhere, then linux/ or windows/.
    Only the platform's own half is copied, so a user never has to work out which files apply.

    Used by .github/workflows/broiler-preview-package.yml; run it by hand to reproduce a package.

.PARAMETER BossDirectory
    The publish output directory to lay the assets into (…/BPP-<os>-<variant>/BOSS).

.PARAMETER Platform
    linux or windows — selects which half of the packaging tree ships.

.EXAMPLE
    ./scripts/package-boss.ps1 -BossDirectory /tmp/packages/BPP-Linux-self-contained/BOSS -Platform linux
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $BossDirectory,
    [Parameter(Mandatory = $true)][ValidateSet('linux', 'windows')][string] $Platform
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$packagingRoot = Join-Path $repoRoot 'Broiler.Office.Server/packaging'

if (-not (Test-Path -LiteralPath $BossDirectory)) {
    throw "Publish output not found: $BossDirectory"
}
if (-not (Test-Path -LiteralPath $packagingRoot)) {
    throw "Packaging assets not found: $packagingRoot"
}

$executableName = if ($Platform -eq 'windows') { 'Broiler.Office.Server.exe' } else { 'Broiler.Office.Server' }
$executable = Join-Path $BossDirectory $executableName
if (-not (Test-Path -LiteralPath $executable)) {
    throw "Expected server executable was not produced: $executable"
}

# The whole point of the vendored-publish hosting model: without wwwroot the server starts, answers
# /healthz, and 404s every page. Fail here rather than shipping that.
$indexHtml = Join-Path $BossDirectory 'wwwroot/index.html'
if (-not (Test-Path -LiteralPath $indexHtml)) {
    throw "The Writer bundle is missing from the publish output: $indexHtml"
}

# The WebAssembly SDK rewrites index.html with an import map pointing at the fingerprinted runtime.
# The raw placeholder from the project's wwwroot has neither, and an app built from it cannot boot —
# which is exactly what a hosted (non-vendored) publish would have produced.
if ((Get-Content -LiteralPath $indexHtml -Raw) -notmatch '<script type="importmap">') {
    throw "The vendored index.html has no import map — the Writer bundle was not published/transformed correctly: $indexHtml"
}

Write-Host "==> laying BOSS packaging assets into $BossDirectory ($Platform)"

Copy-Item -LiteralPath (Join-Path $packagingRoot 'common/README.md') -Destination $BossDirectory -Force

if ($Platform -eq 'linux') {
    Copy-Item -LiteralPath (Join-Path $packagingRoot 'linux/run-boss.sh') -Destination $BossDirectory -Force
    Copy-Item -LiteralPath (Join-Path $packagingRoot 'linux/service') -Destination $BossDirectory -Recurse -Force

    # tar preserves the mode, so set it here rather than asking users to chmod after extracting.
    if ($IsLinux -or $IsMacOS) {
        chmod 0755 $executable (Join-Path $BossDirectory 'run-boss.sh')
        chmod 0755 (Join-Path $BossDirectory 'service/install-service.sh') `
                   (Join-Path $BossDirectory 'service/uninstall-service.sh') `
                   (Join-Path $BossDirectory 'service/sysv/broiler-office-server')
    }
}
else {
    Copy-Item -LiteralPath (Join-Path $packagingRoot 'windows/Start-BOSS.cmd') -Destination $BossDirectory -Force
    Copy-Item -LiteralPath (Join-Path $packagingRoot 'windows/service') -Destination $BossDirectory -Recurse -Force
}

$launcher = if ($Platform -eq 'windows') { 'Start-BOSS.cmd' } else { 'run-boss.sh' }
foreach ($asset in @($executableName, 'appsettings.json', 'README.md', $launcher)) {
    if (-not (Test-Path -LiteralPath (Join-Path $BossDirectory $asset))) {
        throw "Packaging did not produce the expected asset: $asset"
    }
    Write-Host "    $asset"
}

Get-ChildItem -LiteralPath (Join-Path $BossDirectory 'service') -Recurse -File |
    ForEach-Object { Write-Host "    service/$([IO.Path]::GetRelativePath((Join-Path $BossDirectory 'service'), $_.FullName) -replace '\\', '/')" }

$bundleSize = (Get-ChildItem -LiteralPath (Join-Path $BossDirectory 'wwwroot') -Recurse -File |
    Measure-Object -Property Length -Sum).Sum
Write-Host ("    wwwroot/ ({0:N1} MB Writer bundle)" -f ($bundleSize / 1MB))
