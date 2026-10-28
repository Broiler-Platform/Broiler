[CmdletBinding()]
param(
    [string] $Version,
    [Parameter(Mandatory = $true)][string] $OutputRoot,
    [switch] $RequireClean,
    [string] $LockFile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'office.lock.json'),
    [string] $CacheDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'broiler-office-artifacts'),
    [switch] $Offline,
    [switch] $FromSource
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent
if ($FromSource) {
    if (!$Version) { throw 'An explicit demo version is required for a development source build.' }
    & (Join-Path $PSScriptRoot 'build-office-from-source.ps1') -Version $Version -OutputRoot $OutputRoot -RequireClean:$RequireClean
    if ($LASTEXITCODE -ne 0) { throw 'Development source build failed.' }
    return
}
$output = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $output) { throw 'Office build output must be a new directory.' }
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve source commit.' }
$dirty = @(& git -C $repoRoot status --porcelain).Count -ne 0
if ($RequireClean -and $dirty) { throw 'Release builds require a clean committed source tree.' }
$locked = Get-Content -Raw -LiteralPath $LockFile | ConvertFrom-Json
if ($Version -and $Version -ne $locked.suite) { throw 'The Office version must match the reviewed lockfile.' }
if ($RequireClean -and $Offline) { throw 'Release builds cannot use an offline rehearsal.' }
$arguments = @((Join-Path $PSScriptRoot 'office_artifacts.py'), 'assemble', (Resolve-Path -LiteralPath $LockFile).Path,
    '--output', (Join-Path $output 'package'), '--cache', [IO.Path]::GetFullPath($CacheDirectory), '--commit', $commit)
if ($Offline) { $arguments += '--offline' }
& python @arguments
if ($LASTEXITCODE -ne 0) { throw 'Artifact-first Office assembly failed. No product is rebuilt as a fallback.' }
Write-Output "Office assets: $(Join-Path $output 'package/assets')"
