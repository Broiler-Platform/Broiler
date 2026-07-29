<#
.SYNOPSIS
    Writes the BUILD-INFO.txt that ships at the root of a Broiler Preview Package.

.DESCRIPTION
    A preview package is a moving target — it is built from a branch on demand, not from a release
    tag — so the first question anyone asks about one is "which build is this?". This drops a plain
    text file at the package root answering that, plus what the package contains, what the target
    machine needs, and how to start each app.

    Used by .github/workflows/broiler-preview-package.yml.

.PARAMETER PackageDirectory
    Package root to write BUILD-INFO.txt into (…/packages/BPP-Linux-self-contained).

.PARAMETER Platform
    linux or windows.

.PARAMETER Variant
    self-contained or framework-dependent.

.PARAMETER Branch
    Branch the package was built from.

.PARAMETER Configuration
    MSBuild configuration used (Release-Linux / Release-Windows).

.EXAMPLE
    ./scripts/write-bpp-build-info.ps1 -PackageDirectory /tmp/packages/BPP-Linux-self-contained `
        -Platform linux -Variant self-contained -Branch main -Configuration Release-Linux
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $PackageDirectory,
    [Parameter(Mandatory = $true)][ValidateSet('linux', 'windows')][string] $Platform,
    [Parameter(Mandatory = $true)][ValidateSet('self-contained', 'framework-dependent')][string] $Variant,
    [string] $Branch = '(unknown)',
    [string] $Configuration = '(unknown)'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PackageDirectory)) {
    throw "Package directory not found: $PackageDirectory"
}

$packageName = Split-Path -Leaf $PackageDirectory
$commit = (git -C (Split-Path -Parent $PSScriptRoot) rev-parse HEAD 2>$null)
if (-not $commit) { $commit = '(unknown)' }
$built = [DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm:ss') + ' UTC'
$run = if ($env:GITHUB_RUN_NUMBER) { "GitHub Actions run #$env:GITHUB_RUN_NUMBER" } else { 'local build' }

$runtimeNote = if ($Variant -eq 'self-contained') {
    'self-contained — the .NET runtime is included, nothing to install'
}
else {
    'framework-dependent — requires the ASP.NET Core 10 runtime on the target machine'
}

$rid = if ($Platform -eq 'windows') { 'win-x64' } else { 'linux-x64' }
$exeSuffix = if ($Platform -eq 'windows') { '.exe' } else { '' }
$bossLauncher = if ($Platform -eq 'windows') { 'BOSS\Start-BOSS.cmd' } else { './BOSS/run-boss.sh' }
$bossServer = if ($Platform -eq 'windows') { 'BOSS\Broiler.Office.Server.exe' } else { './BOSS/Broiler.Office.Server' }

$lines = @(
    'Broiler Preview Package'
    '======================='
    ''
    "Package        : $packageName"
    "Target         : $rid"
    "Deployment     : $runtimeNote"
    "Branch         : $Branch"
    "Commit         : $commit"
    "Configuration  : $Configuration"
    "Built          : $built ($run)"
    ''
    'This is a preview build from a branch, not a supported release. Expect rough edges.'
    ''
    'Contents'
    '--------'
    ''
    ('  {0,-24}{1}' -f "Broiler.Browser$exeSuffix", 'The Broiler web browser.')
    ('  {0,-24}{1}' -f "Broiler.Writer$exeSuffix", 'The Broiler word processor (desktop).')
    ('  {0,-24}{1}' -f 'BOSS/', 'Broiler Office Standalone Server — serves the Broiler Writer')
    ('  {0,-24}{1}' -f '', 'web app (WebAssembly) over HTTP from its own Kestrel host.')
    ''
    'Starting the Office server (BOSS)'
    '---------------------------------'
    ''
    "  $bossLauncher                        listens on http://0.0.0.0:5555"
    "  $bossServer --urls http://0.0.0.0:5555"
    ''
    '  Then open http://localhost:5555/ — health probe at /healthz, server details at /api/info.'
    ''
    '  To run it as a system service (systemd, SysV init, or a Windows service), see'
    '  BOSS/service/. Full documentation: BOSS/README.md.'
)

if ($Variant -eq 'framework-dependent') {
    $lines += @(
        ''
        'Runtime requirement'
        '-------------------'
        ''
        '  Install the ASP.NET Core 10 runtime before running BOSS:'
        '  https://dotnet.microsoft.com/download/dotnet/10.0  (ASP.NET Core Runtime, not just the'
        '  .NET Runtime). The self-contained package needs no installation.'
    )
}

$lines += @(
    ''
    'Source, issues, documentation: https://github.com/Broiler-Platform/Broiler'
)

$destination = Join-Path $PackageDirectory 'BUILD-INFO.txt'
# CRLF on Windows so Notepad renders it; LF elsewhere.
$newline = if ($Platform -eq 'windows') { "`r`n" } else { "`n" }
[System.IO.File]::WriteAllText($destination, ($lines -join $newline) + $newline)

Write-Host "==> wrote $destination"
