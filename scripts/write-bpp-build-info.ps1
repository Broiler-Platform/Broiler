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
    linux, windows, or android.

.PARAMETER Variant
    self-contained or framework-dependent for the desktop packages; app-bundle for Android,
    which has no such split — an .aab always carries its own runtime.

.PARAMETER Branch
    Branch the package was built from.

.PARAMETER Configuration
    MSBuild configuration used (Release-Linux / Release-Windows / Release).

.PARAMETER SigningCertificateSha256
    SHA-256 fingerprint of the certificate the Android bundles are signed with, as
    scripts/sign-android-app-bundles.ps1 reports it. Quoted in BUILD-INFO.txt so the package says
    which key signed it. Android only.

.EXAMPLE
    ./scripts/write-bpp-build-info.ps1 -PackageDirectory /tmp/packages/BPP-Linux-self-contained `
        -Platform linux -Variant self-contained -Branch main -Configuration Release-Linux

.EXAMPLE
    ./scripts/write-bpp-build-info.ps1 -PackageDirectory /tmp/packages/BPP-Android `
        -Platform android -Variant app-bundle -Branch main -Configuration Release
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $PackageDirectory,
    [Parameter(Mandatory = $true)][ValidateSet('linux', 'windows', 'android')][string] $Platform,
    [Parameter(Mandatory = $true)][ValidateSet('self-contained', 'framework-dependent', 'app-bundle')][string] $Variant,
    [string] $Branch = '(unknown)',
    [string] $Configuration = '(unknown)',
    [string] $SigningCertificateSha256 = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PackageDirectory)) {
    throw "Package directory not found: $PackageDirectory"
}

# app-bundle is the only Android variant and is meaningless for the desktop packages, so the
# two sets of switches cannot be crossed.
$isAndroid = $Platform -eq 'android'
if ($isAndroid -ne ($Variant -eq 'app-bundle')) {
    throw "Variant '$Variant' does not apply to platform '$Platform'."
}

$packageName = Split-Path -Leaf $PackageDirectory
$commit = (git -C (Split-Path -Parent $PSScriptRoot) rev-parse HEAD 2>$null)
if (-not $commit) { $commit = '(unknown)' }
$built = [DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm:ss') + ' UTC'
$run = if ($env:GITHUB_RUN_NUMBER) { "GitHub Actions run #$env:GITHUB_RUN_NUMBER" } else { 'local build' }

$runtimeNote = switch ($Variant) {
    'self-contained' { 'self-contained — the .NET runtime is included, nothing to install' }
    'framework-dependent' { 'framework-dependent — requires the ASP.NET Core 10 runtime on the target machine' }
    'app-bundle' { 'app bundle — the .NET runtime ships inside the .aab, nothing to install' }
}

$rid = switch ($Platform) {
    'windows' { 'win-x64' }
    'linux' { 'linux-x64' }
    'android' { 'android-arm64, android-x64' }
}

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
)

if ($isAndroid) {
    $lines += @(
        'Contents'
        '--------'
        ''
        ('  {0,-24}{1}' -f 'Broiler.Browser.aab', 'The Broiler web browser — org.broiler.browser.')
        ('  {0,-24}{1}' -f 'Broiler.Writer.aab', 'The Broiler word processor — org.broiler.writer.')
        ''
        '  Both bundles carry the arm64-v8a and x86_64 ABIs, compile against API 36, and declare a'
        '  minimum of API 24.'
        ''
        'Signing'
        '-------'
        ''
        '  Both bundles are signed with the Broiler release key. The key itself is kept outside'
        '  the repository — the build reads it from encrypted repository secrets — and the'
        '  workflow verifies each signature against it before packaging.'
        ''
        '  Check a bundle before you install or upload it:'
        ''
        '    jarsigner -verify Broiler.Browser.aab'
        '    keytool -printcert -jarfile Broiler.Browser.aab'
    )

    if (-not [string]::IsNullOrWhiteSpace($SigningCertificateSha256)) {
        $lines += @(
            ''
            '  The signing certificate has this SHA-256 fingerprint:'
            ''
            "    $SigningCertificateSha256"
        )
    }

    $lines += @(
        ''
        'Installing an .aab'
        '------------------'
        ''
        '  An app bundle is not installable as it stands. Use Google bundletool to build the APK'
        '  set for a device and install that:'
        ''
        '    bundletool build-apks --bundle=Broiler.Browser.aab --output=Broiler.Browser.apks'
        '    bundletool install-apks --apks=Broiler.Browser.apks'
        ''
        '  bundletool signs the APKs it generates with its own debug key unless --ks says'
        '  otherwise; that is a local install detail and leaves the bundle signature untouched.'
        ''
        '  bundletool releases: https://github.com/google/bundletool/releases'
        ''
        '  Android support is a preview: the applications are verified on an API 36 emulator, and'
        '  physical-device validation is still outstanding. See docs/architecture/android.md.'
    )
}
else {
    $exeSuffix = if ($Platform -eq 'windows') { '.exe' } else { '' }
    $bossLauncher = if ($Platform -eq 'windows') { 'BOSS\Start-BOSS.cmd' } else { './BOSS/run-boss.sh' }
    $bossServer = if ($Platform -eq 'windows') { 'BOSS\Broiler.Office.Server.exe' } else { './BOSS/Broiler.Office.Server' }

    $lines += @(
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
