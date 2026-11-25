<#
.SYNOPSIS
    Starts a packaged BOSS and verifies it serves the Broiler Office WebAssembly suite.

.DESCRIPTION
    Launches the published server on a loopback port and checks the things that have historically
    broken a BOSS package:

      * /healthz answers                     — the host starts at all
      * /api/info reports server "BOSS"      — the API routes are mapped and report Office apps
      * /office-web.json returns approved    — local same-origin catalog for the Office Launcher
      * /, /writer/, /cell/, /plate/ return  — all four vendored Office bundles are present and are
        transformed index.html shells          the WebAssembly SDK's rewritten shells, not placeholders
      * every fingerprinted runtime module in each app's import map is served as HTTP 200
      * /writer, /cell, /plate redirect to trailing-slash mount paths
      * unknown paths fall back to the corresponding app shell — client-side routes survive refresh

    The server is deliberately started from a *different* working directory, because a service
    manager does the same: it proves the content root resolves to the executable's own folder rather
    than the caller's cwd (otherwise every page 404s while /healthz keeps answering OK).

    Used by .github/workflows/broiler-preview-package.yml after each publish.

.PARAMETER PackageDirectory
    Directory holding the published Broiler.Office.Server and its wwwroot — the package root, since
    the server publishes into it directly rather than into a BOSS/ subfolder.

.PARAMETER Port
    Loopback port to listen on. Default 5555.

.PARAMETER TimeoutSeconds
    How long to wait for the server to start serving. Default 120 — a self-contained first start on
    a cold CI runner is not instant.

.EXAMPLE
    ./scripts/smoke-test-boss.ps1 -PackageDirectory /tmp/packages/BPP-Linux-self-contained
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $PackageDirectory,
    [int] $Port = 5555,
    [int] $TimeoutSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PackageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$executableName = if ($IsWindows) { 'Broiler.Office.Server.exe' } else { 'Broiler.Office.Server' }
$executable = Join-Path $PackageDirectory $executableName
if (-not (Test-Path -LiteralPath $executable)) {
    throw "Server executable not found: $executable"
}

$baseUrl = "http://127.0.0.1:$Port"
$logDirectory = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [System.IO.Path]::GetTempPath() }
$stdout = Join-Path $logDirectory "boss-smoke-$Port.out.log"
$stderr = Join-Path $logDirectory "boss-smoke-$Port.err.log"

# Not $PackageDirectory: a service manager starts the server from an unrelated directory, and this is
# the cheapest way to catch a content root that silently follows the caller's cwd.
$foreignWorkingDirectory = [System.IO.Path]::GetTempPath()

function Invoke-BossRequest {
    param([string] $Path)
    # -NoProxy: a proxy configured on the machine must not swallow a loopback request.
    Invoke-WebRequest -Uri "$baseUrl$Path" -NoProxy -TimeoutSec 30 -SkipHttpErrorCheck -UseBasicParsing
}

Write-Host "==> starting BOSS from $PackageDirectory on $baseUrl"
$bossStartOptions = @{}
if ($IsWindows) { $bossStartOptions.WindowStyle = 'Hidden' }
$server = Start-Process @bossStartOptions -FilePath $executable `
    -ArgumentList '--urls', $baseUrl `
    -WorkingDirectory $foreignWorkingDirectory `
    -RedirectStandardOutput $stdout `
    -RedirectStandardError $stderr `
    -PassThru

$failures = [System.Collections.Generic.List[string]]::new()
try {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $ready = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($server.HasExited) {
            throw "The server exited with code $($server.ExitCode) before it started serving."
        }

        try {
            $health = Invoke-BossRequest '/healthz'
            if ($health.StatusCode -eq 200) { $ready = $true; break }
        }
        catch {
            # Connection refused while Kestrel is still binding — keep waiting.
        }

        Start-Sleep -Milliseconds 500
    }

    if (-not $ready) {
        throw "The server did not answer $baseUrl/healthz within ${TimeoutSeconds}s."
    }
    Write-Host '    /healthz            OK'

    $info = (Invoke-BossRequest '/api/info').Content | ConvertFrom-Json
    if ($info.server -ne 'BOSS') {
        $failures.Add("/api/info reported server '$($info.server)', expected 'BOSS'.")
    }
    else {
        Write-Host "    /api/info           OK (v$($info.version), apps: $($info.apps.name -join ', '))"
    }

    $catalogResponse = Invoke-BossRequest '/office-web.json'
    if ($catalogResponse.StatusCode -ne 200) {
        $failures.Add("/office-web.json returned HTTP $($catalogResponse.StatusCode).")
    }
    else {
        $catalog = $catalogResponse.Content | ConvertFrom-Json
        if ($catalog.status -ne 'approved' -or @($catalog.products).Count -lt 3) {
            $failures.Add('/office-web.json did not report an approved local Broiler Office catalog.')
        }
        else {
            Write-Host "    /office-web.json    OK (status=$($catalog.status), products=$($catalog.products.id -join ', '))"
        }
    }

    $apps = @(
        @{ Name = 'Office'; BasePath = '/';        Fallback = '/documents/untitled';        ExpectedLink = 'href="/writer/"' }
        @{ Name = 'Writer'; BasePath = '/writer/'; Fallback = '/writer/documents/untitled'; ExpectedLink = 'href="/"' }
        @{ Name = 'Cell';   BasePath = '/cell/';   Fallback = '/cell/sheet/1';              ExpectedLink = 'href="/"' }
        @{ Name = 'Plate';  BasePath = '/plate/';  Fallback = '/plate/view/1';              ExpectedLink = 'href="/"' }
    )

    foreach ($app in $apps) {
        $basePath = $app.BasePath
        $index = Invoke-BossRequest $basePath
        if ($index.StatusCode -ne 200) {
            $failures.Add("$basePath returned HTTP $($index.StatusCode) — the vendored $($app.Name) bundle is missing or unreadable.")
            continue
        }
        if ($index.Content -notmatch '<script type="importmap">') {
            $failures.Add("$basePath served an index.html without an import map — the raw placeholder shell was packaged instead of the transformed one.")
            continue
        }
        if ($index.Content -notmatch [regex]::Escape($app.ExpectedLink)) {
            $failures.Add("$basePath did not contain expected local Office link ($($app.ExpectedLink)).")
        }
        Write-Host "    $($basePath.PadRight(20))OK ($($index.Content.Length) bytes, import map present)"

        # Follow the import map and fetch every module the app boots from — a wwwroot that lost
        # _framework/ still returns a perfectly good index.html.
        $importMapMatch = [regex]::Match($index.Content, '(?s)<script type="importmap">(?<json>.*?)</script>')
        if (-not $importMapMatch.Success) {
            $failures.Add("The import map in $basePath index.html could not be read.")
        }
        else {
            $imports = $importMapMatch.Groups['json'].Value | ConvertFrom-Json
            $modules = @()
            if ($imports.PSObject.Properties.Name -contains 'imports') {
                $modules = @($imports.imports.PSObject.Properties | ForEach-Object { $_.Value })
            }

            if ($modules.Count -eq 0) {
                $failures.Add("The import map in $basePath lists no modules — the $($app.Name) bundle cannot boot from this shell.")
            }

            foreach ($module in $modules) {
                $modulePath = $basePath + ($module -replace '^\./', '')
                $response = Invoke-BossRequest $modulePath
                if ($response.StatusCode -ne 200) {
                    $failures.Add("$modulePath returned HTTP $($response.StatusCode) — a module the $($app.Name) app boots from is not being served.")
                }
                else {
                    Write-Host "    $modulePath  OK"
                }
            }
        }

        $fallback = Invoke-BossRequest $app.Fallback
        if ($fallback.StatusCode -ne 200) {
            $failures.Add("Client-routed path $($app.Fallback) returned HTTP $($fallback.StatusCode); the SPA fallback is not working.")
        }
        else {
            Write-Host "    $($app.Fallback)  OK (falls back to the $($app.Name) shell)"
        }
    }
}
finally {
    if (-not $server.HasExited) {
        Write-Host '==> stopping BOSS'
        # CloseMainWindow does nothing for a console app; Kill is the portable stop, and the server
        # holds no state that needs a graceful shutdown.
        $server.Kill()
        $server.WaitForExit(30000) | Out-Null
    }

    foreach ($log in @($stdout, $stderr)) {
        if ((Test-Path -LiteralPath $log) -and (Get-Item -LiteralPath $log).Length -gt 0) {
            Write-Host "---- $(Split-Path -Leaf $log) ----"
            Get-Content -LiteralPath $log -Tail 40 | ForEach-Object { Write-Host "    $_" }
        }
    }
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) { Write-Host "::error::BOSS smoke test: $failure" }
    throw "BOSS smoke test failed with $($failures.Count) problem(s)."
}

Write-Host '==> BOSS smoke test passed.'
