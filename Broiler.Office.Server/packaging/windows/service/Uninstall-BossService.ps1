<#
.SYNOPSIS
    Removes the BOSS Windows service installed by Install-BossService.ps1.

.DESCRIPTION
    Stops and deletes the service, removes the firewall rule it added, and deletes the install
    directory unless -KeepFiles is given.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Uninstall-BossService.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Uninstall-BossService.ps1 -KeepFiles

.NOTES
    Run from an elevated PowerShell prompt.
#>
[CmdletBinding()]
param(
    [string] $ServiceName = 'BroilerOfficeServer',
    [string] $InstallPath = (Join-Path $env:ProgramFiles 'Broiler\Office Server'),
    [switch] $KeepFiles
)

$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Uninstall-BossService.ps1 must run from an elevated (Administrator) PowerShell prompt.'
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne 'Stopped') {
        Write-Host '==> stopping the service'
        Stop-Service -Name $ServiceName -Force
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
    }

    Write-Host '==> deleting the service registration'
    # Remove-Service exists only on PowerShell 6+; sc.exe works on Windows PowerShell 5.1 too.
    if (Get-Command Remove-Service -ErrorAction SilentlyContinue) {
        Remove-Service -Name $ServiceName
    }
    else {
        & sc.exe delete $ServiceName | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "sc.exe delete failed with exit code $LASTEXITCODE." }
    }
}
else {
    Write-Host "==> service '$ServiceName' is not registered"
}

Get-NetFirewallRule -ErrorAction SilentlyContinue |
    Where-Object DisplayName -like '*Broiler Office Standalone Server*' |
    ForEach-Object {
        Write-Host "==> removing firewall rule '$($_.DisplayName)'"
        Remove-NetFirewallRule -Name $_.Name -ErrorAction SilentlyContinue
    }

if (-not $KeepFiles -and (Test-Path -LiteralPath $InstallPath)) {
    Write-Host "==> removing $InstallPath"
    Remove-Item -LiteralPath $InstallPath -Recurse -Force
}

Write-Host ''
Write-Host 'Removed.'
