<#
.SYNOPSIS
    Starts a packaged BOSS and verifies it actually serves the Broiler Writer.

.DESCRIPTION
    Launches the published server on a loopback port and checks the things that have historically
    broken a BOSS package:

      * /healthz answers                     — the host starts at all
      * /api/info reports server "BOSS"      — the API routes are mapped
      * / returns the transformed index.html — the vendored Writer bundle is present and is the
                                               WebAssembly SDK's rewritten shell, not the raw
                                               placeholder that cannot boot
      * the fingerprinted runtime module is served as JavaScript from _framework/
      * an unknown path falls back to the shell — client-side routes survive a refresh

    The server is deliberately started from a *different* working directory, because a service
    manager does the same: it proves the content root resolves to the executable's own folder rather
    than the caller's cwd (otherwise every page 404s while /healthz keeps answering OK).

    Used by .github/workflows/broiler-preview-package.yml after each publish.

.PARAMETER BossDirectory
    Directory holding the published Broiler.Office.Server and its wwwroot.

.PARAMETER Port
    Loopback port to listen on. Default 5555.

.PARAMETER TimeoutSeconds
    How long to wait for the server to start serving. Default 120 — a self-contained first start on
    a cold CI runner is not instant.

.EXAMPLE
    ./scripts/smoke-test-boss.ps1 -BossDirectory /tmp/packages/BPP-Linux-self-contained/BOSS
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $BossDirectory,
    [int] $Port = 5555,
    [int] $TimeoutSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$BossDirectory = (Resolve-Path -LiteralPath $BossDirectory).Path
$executableName = if ($IsWindows) { 'Broiler.Office.Server.exe' } else { 'Broiler.Office.Server' }
$executable = Join-Path $BossDirectory $executableName
if (-not (Test-Path -LiteralPath $executable)) {
    throw "Server executable not found: $executable"
}

$baseUrl = "http://127.0.0.1:$Port"
$logDirectory = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [System.IO.Path]::GetTempPath() }
$stdout = Join-Path $logDirectory "boss-smoke-$Port.out.log"
$stderr = Join-Path $logDirectory "boss-smoke-$Port.err.log"

# Not $BossDirectory: a service manager starts the server from an unrelated directory, and this is
# the cheapest way to catch a content root that silently follows the caller's cwd.
$foreignWorkingDirectory = [System.IO.Path]::GetTempPath()

function Invoke-BossRequest {
    param([string] $Path)
    # -NoProxy: a proxy configured on the machine must not swallow a loopback request.
    Invoke-WebRequest -Uri "$baseUrl$Path" -NoProxy -TimeoutSec 30 -SkipHttpErrorCheck -UseBasicParsing
}

Write-Host "==> starting BOSS from $BossDirectory on $baseUrl"
$server = Start-Process -FilePath $executable `
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

    $index = Invoke-BossRequest '/'
    if ($index.StatusCode -ne 200) {
        $failures.Add("/ returned HTTP $($index.StatusCode) — the vendored Writer bundle is missing or unreadable.")
    }
    elseif ($index.Content -notmatch '<script type="importmap">') {
        $failures.Add('/ served an index.html without an import map — the raw placeholder shell was packaged instead of the transformed one, and the Writer cannot boot.')
    }
    else {
        Write-Host "    /                   OK ($($index.Content.Length) bytes, import map present)"
    }

    # Follow the import map to the fingerprinted runtime entry point and make sure it is really
    # served — a wwwroot that lost _framework/ still returns a perfectly good index.html.
    $runtimeMatch = [regex]::Match($index.Content, '_framework/dotnet\.[a-z0-9]+\.js')
    if (-not $runtimeMatch.Success) {
        $failures.Add('The import map references no fingerprinted _framework/dotnet.<hash>.js module.')
    }
    else {
        $runtime = Invoke-BossRequest "/$($runtimeMatch.Value)"
        if ($runtime.StatusCode -ne 200) {
            $failures.Add("$($runtimeMatch.Value) returned HTTP $($runtime.StatusCode) — the mono-wasm runtime is not being served.")
        }
        else {
            Write-Host "    $($runtimeMatch.Value)  OK"
        }
    }

    # MapFallbackToFile: an in-app route must return the shell, not a 404.
    $fallback = Invoke-BossRequest '/documents/untitled'
    if ($fallback.StatusCode -ne 200) {
        $failures.Add("A client-routed path returned HTTP $($fallback.StatusCode); the SPA fallback is not working.")
    }
    else {
        Write-Host '    client-side route   OK (falls back to the app shell)'
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
