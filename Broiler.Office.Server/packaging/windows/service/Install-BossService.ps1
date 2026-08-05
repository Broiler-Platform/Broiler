<#
.SYNOPSIS
    Installs BOSS — the Broiler Office Standalone Server — as a Windows service.

.DESCRIPTION
    Copies the extracted package folder to -InstallPath, registers a Windows service that starts it
    with the requested listening addresses, and starts the service. The server links
    Microsoft.Extensions.Hosting.WindowsServices, so it answers the service control protocol
    directly — no wrapper (NSSM, srvany) is needed.

    The payload is the whole extracted package: the server shares its runtime files with
    Broiler.Browser.exe and Broiler.Writer.exe, which sit in the same folder, so those two are
    copied to -InstallPath as well. They are inert there — nothing starts them, and the service only
    ever runs Broiler.Office.Server.exe.

    Re-running upgrades the payload in place: the service is stopped, the files are refreshed and
    the service is started again.

.PARAMETER Urls
    Listening addresses passed to the server. Separate several with a semicolon.

.PARAMETER InstallPath
    Where to install the server. Defaults to %ProgramFiles%\Broiler\Office Server.

.PARAMETER ServiceName
    Windows service name. Defaults to BroilerOfficeServer.

.PARAMETER ServiceAccount
    Account the service runs as. Defaults to "NT AUTHORITY\NetworkService"; pass "LocalSystem" for
    a more privileged account, or a domain account together with -ServiceAccountPassword.

.PARAMETER OpenFirewall
    Also add an inbound Windows Firewall rule for the TCP port found in -Urls.

.PARAMETER NoStart
    Register the service but do not start it.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Install-BossService.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Install-BossService.ps1 -Urls http://0.0.0.0:5555 -OpenFirewall

.NOTES
    Run from an elevated PowerShell prompt. Remove with .\Uninstall-BossService.ps1.
#>
[CmdletBinding()]
param(
    [string] $Urls = 'http://0.0.0.0:5555',
    [string] $InstallPath = (Join-Path $env:ProgramFiles 'Broiler\Office Server'),
    [string] $ServiceName = 'BroilerOfficeServer',
    [string] $ServiceDisplayName = 'BOSS — Broiler Office Standalone Server',
    [string] $ServiceAccount = 'NT AUTHORITY\NetworkService',
    [System.Security.SecureString] $ServiceAccountPassword,
    [string] $Environment = 'Production',
    [switch] $OpenFirewall,
    [switch] $NoStart
)

$ErrorActionPreference = 'Stop'

function Assert-Elevated {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Install-BossService.ps1 must run from an elevated (Administrator) PowerShell prompt.'
    }
}

Assert-Elevated

$payloadDir = Split-Path -Parent $PSScriptRoot          # the package root (this script lives in …\service)
$sourceExe = Join-Path $payloadDir 'Broiler.Office.Server.exe'

if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "Broiler.Office.Server.exe was not found in $payloadDir — run this script from the extracted package."
}
if (-not (Test-Path -LiteralPath (Join-Path $payloadDir 'wwwroot'))) {
    throw "$payloadDir\wwwroot is missing — the package is incomplete; the server has nothing to serve."
}

Write-Host 'BOSS install'
Write-Host "  install path    : $InstallPath"
Write-Host "  service name    : $ServiceName"
Write-Host "  service account : $ServiceAccount"
Write-Host "  listening on    : $Urls"
Write-Host ''

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing -and $existing.Status -ne 'Stopped') {
    Write-Host '==> stopping the running service'
    Stop-Service -Name $ServiceName -Force
    $existing.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
}

Write-Host "==> copying the server to $InstallPath"
New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
# The vendored wwwroot is content-hashed: a stale asset from a previous release can be paired with a
# freshly-hashed runtime by a cached index.html, so replace it wholesale rather than merging.
$targetWwwRoot = Join-Path $InstallPath 'wwwroot'
if (Test-Path -LiteralPath $targetWwwRoot) { Remove-Item -LiteralPath $targetWwwRoot -Recurse -Force }
Copy-Item -Path (Join-Path $payloadDir '*') -Destination $InstallPath -Recurse -Force

$targetExe = Join-Path $InstallPath 'Broiler.Office.Server.exe'
# The service binary path is a single command line; quote the executable so a path with spaces
# (the ProgramFiles default has one) is not split into an argument.
$binaryPath = '"{0}" --urls "{1}"' -f $targetExe, $Urls

if ($existing) {
    Write-Host '==> updating the existing service registration'
    # Set-Service cannot change the binary path on Windows PowerShell 5.1; sc.exe can, everywhere.
    & sc.exe config $ServiceName binPath= $binaryPath start= delayed-auto obj= $ServiceAccount | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc.exe config failed with exit code $LASTEXITCODE." }
}
else {
    Write-Host '==> registering the service'
    $newServiceArgs = @{
        Name           = $ServiceName
        BinaryPathName = $binaryPath
        DisplayName    = $ServiceDisplayName
        Description    = 'Serves the Broiler Office web apps (Broiler Writer) over Kestrel.'
        StartupType    = 'Automatic'
    }
    if ($ServiceAccountPassword) {
        $newServiceArgs.Credential = [pscredential]::new($ServiceAccount, $ServiceAccountPassword)
    }
    New-Service @newServiceArgs | Out-Null

    if (-not $ServiceAccountPassword) {
        # Built-in accounts (NetworkService, LocalService) take no password, and New-Service has no
        # parameter for them — set the account after creation.
        & sc.exe config $ServiceName obj= $ServiceAccount | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "sc.exe config (account) failed with exit code $LASTEXITCODE." }
    }

    # Delayed start keeps boot responsive; the WebAssembly bundle is large but only read on demand.
    & sc.exe config $ServiceName start= delayed-auto | Out-Null
}

# Restart on crash: after 5s, then 10s, then every 60s, with the failure count reset daily.
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/60000 | Out-Null

Write-Host '==> setting the environment for the service'
# ASPNETCORE_ENVIRONMENT has to reach the service process; the per-service Environment value is the
# supported way (a machine-level variable would leak into every other process on the box).
$serviceKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
Set-ItemProperty -Path $serviceKey -Name 'Environment' -Type MultiString -Value @(
    "ASPNETCORE_ENVIRONMENT=$Environment"
    'DOTNET_NOLOGO=1'
)

# NetworkService/LocalService need read+execute on the install tree; ProgramFiles grants it by
# inheritance, a custom -InstallPath may not.
& icacls $InstallPath /grant "${ServiceAccount}:(OI)(CI)(RX)" /T /Q | Out-Null

if ($OpenFirewall) {
    $port = ([regex]::Match($Urls, ':(\d+)')).Groups[1].Value
    if ($port) {
        Write-Host "==> opening TCP port $port in Windows Firewall"
        $ruleName = "$ServiceDisplayName (TCP $port)"
        Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue |
            Remove-NetFirewallRule -ErrorAction SilentlyContinue
        New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow `
            -Protocol TCP -LocalPort $port -Profile Any | Out-Null
    }
    else {
        Write-Warning "Could not determine a TCP port from -Urls '$Urls'; no firewall rule was added."
    }
}

if (-not $NoStart) {
    Write-Host '==> starting the service'
    Start-Service -Name $ServiceName
    (Get-Service -Name $ServiceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(60))
    Get-Service -Name $ServiceName | Format-Table -AutoSize Status, Name, DisplayName
}

$probe = ($Urls -split ';')[0] -replace '0\.0\.0\.0', 'localhost' -replace '\[::\]', 'localhost'
Write-Host ''
Write-Host 'Done. Manage it with:'
Write-Host "  Start-Service $ServiceName  /  Stop-Service $ServiceName  /  Get-Service $ServiceName"
Write-Host "  Get-EventLog -LogName Application -Source $ServiceName -Newest 20"
Write-Host ''
Write-Host 'Check it is serving:'
Write-Host "  Invoke-RestMethod $probe/healthz"
