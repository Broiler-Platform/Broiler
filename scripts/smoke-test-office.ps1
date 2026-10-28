[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $Archive,
    [Parameter(Mandatory = $true)][string] $OutputDirectory,
    [switch] $SkipDesktopStartup
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Smoke extraction directory must be new.' }
[IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path -LiteralPath $Archive).Path, [IO.Path]::GetFullPath($OutputDirectory))
$roots = @(Get-ChildItem -LiteralPath $OutputDirectory -Directory)
if ($roots.Count -ne 1) { throw 'Expected a single Office bundle root.' }
$bundle = $roots[0].FullName
$manifest = Get-Content -Raw -LiteralPath (Join-Path $bundle 'office-release.json') | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or !$manifest.demo -or $manifest.channel -ne 'preview') { throw 'Invalid Office demo manifest.' }
$expected = @('boss', 'browser', 'cell', 'launcher', 'plate', 'writer')
if (@($manifest.components).Count -ne 6 -or (@($manifest.components.id | Sort-Object) -join ',') -ne ($expected -join ',')) { throw 'Incomplete or duplicate Office product matrix.' }
& (Join-Path $PSScriptRoot 'validate-office-manifest.ps1') -Bundle $bundle
if ($IsWindows) {
    $infoStart = [Diagnostics.ProcessStartInfo]::new((Join-Path $bundle 'launcher/Broiler.Launcher.Windows.exe'))
    $infoStart.UseShellExecute = $false
    $infoStart.RedirectStandardOutput = $true
    $infoStart.RedirectStandardError = $true
    $infoStart.CreateNoWindow = $true
    $infoStart.ArgumentList.Add('--office-package-info')
    $infoProcess = [Diagnostics.Process]::Start($infoStart)
    try {
        if (!$infoProcess.WaitForExit(15000)) { $infoProcess.Kill($true); throw 'Launcher build identity check timed out.' }
        $info = $infoProcess.StandardOutput.ReadToEnd() | ConvertFrom-Json
        $launcherComponent = @($manifest.components | Where-Object id -eq 'launcher')[0]
        if ($infoProcess.ExitCode -ne 0 -or !$info.demo -or ($info.version -split '\+')[0] -ne $launcherComponent.version) { throw 'Launcher is not the pinned demo component build.' }
    } finally { $infoProcess.Dispose() }
    & (Join-Path $PSScriptRoot 'smoke-test-office-settings.ps1') -LauncherDirectory (Join-Path $bundle 'launcher') -OutputDirectory (Join-Path $OutputDirectory 'settings-render')
}
$results = @()
foreach ($component in $manifest.components) {
    $zip = Join-Path $bundle "packages/$($component.asset)"
    if ((Get-Item -LiteralPath $zip).Length -ne $component.size -or (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $component.sha256) { throw "Component archive integrity failed: $($component.id)" }
    $payload = if ($component.id -eq 'launcher') { Join-Path $bundle 'launcher' } else { Join-Path $bundle "products/$($component.id)" }
    if (!(Test-Path -LiteralPath (Join-Path $payload $component.entryPoint))) { throw "Portable component missing: $($component.id)" }
    # Validate the archive bytes independently of the portable copy.
    $extracted = Join-Path $OutputDirectory "component-$($component.id)"
    [IO.Compression.ZipFile]::ExtractToDirectory($zip, $extracted)
    if (!(Test-Path -LiteralPath (Join-Path $extracted $component.entryPoint))) { throw 'Component ZIP has the wrong root layout.' }
    if ($component.id -eq 'boss') {
        & (Join-Path $PSScriptRoot 'smoke-test-boss.ps1') -PackageDirectory $extracted -Port 15555
        & (Join-Path $PSScriptRoot 'smoke-test-boss-launcher.ps1') -Bundle $bundle -OutputDirectory (Join-Path $OutputDirectory 'boss-lifecycle')
        & (Join-Path $PSScriptRoot 'smoke-test-office-update.ps1') -Bundle $bundle -OutputDirectory (Join-Path $OutputDirectory 'update-lifecycle')
        $results += @{ id = 'boss'; status = 'health-info-shell-runtime-and-routing-passed' }
    }
    elseif (!$SkipDesktopStartup) {
        if (!$IsWindows) { throw 'Desktop startup checks require Windows.' }
        $process = Start-Process -FilePath (Join-Path $extracted $component.entryPoint) -WorkingDirectory ([IO.Path]::GetTempPath()) -WindowStyle Hidden -PassThru
        try {
            # MainWindowHandle excludes hidden windows. Hidden startup checks detect early
            # process failure; they cannot certify rendering or dismiss preview notices.
            if ($process.WaitForExit(4000)) { throw "Desktop component exited during startup: $($component.id), code $($process.ExitCode)" }
            $results += @{ id = $component.id; status = 'hidden-process-startup-passed'; manualInteractionRequired = $true }
        } finally {
            if (!$process.HasExited) { $null = $process.CloseMainWindow(); if (!$process.WaitForExit(3000)) { $process.Kill($true); $process.WaitForExit() } }
            $process.Dispose()
        }
    }
    else { $results += @{ id = $component.id; status = 'layout-and-hash-only'; manualInteractionRequired = $true } }
}
$results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'smoke-results.json') -Encoding utf8NoBOM
Write-Output "Office extracted-archive smoke checks passed: $bundle"
