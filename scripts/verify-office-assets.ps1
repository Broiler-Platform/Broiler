[CmdletBinding()]
param([Parameter(Mandatory = $true)][string] $Directory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path -LiteralPath $Directory).Path
$seen = @{}
foreach ($line in Get-Content -LiteralPath (Join-Path $root 'SHA256SUMS.txt')) {
    if ($line -notmatch '^([a-f0-9]{64})  ([a-zA-Z0-9][a-zA-Z0-9._-]+)$') { throw 'Invalid checksum line.' }
    $hash = $Matches[1]; $name = $Matches[2]
    if ($seen.ContainsKey($name) -or $name -eq 'SHA256SUMS.txt') { throw 'Duplicate/self-referential checksum entry.' }
    $seen[$name] = $true
    if ((Get-FileHash -LiteralPath (Join-Path $root $name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw "Asset checksum mismatch: $name" }
}
$actual = @(Get-ChildItem -LiteralPath $root -File | Where-Object Name -ne 'SHA256SUMS.txt')
if ($actual.Count -ne $seen.Count -or @($actual | Where-Object { !$seen.ContainsKey($_.Name) }).Count) { throw 'The checksum list does not cover the exact asset set.' }
$manifest = Get-Content -Raw -LiteralPath (Join-Path $root 'office-release.json') | ConvertFrom-Json
if (!$manifest.demo -or $manifest.channel -ne 'preview' -or
    (@($manifest.components.id | Sort-Object) -join ',') -ne 'boss,browser,cell,launcher,plate,writer') { throw 'This workflow only publishes complete Office demo prereleases.' }
foreach ($component in $manifest.components) {
    if (!$seen.ContainsKey($component.asset)) { throw 'Component asset is outside the verified release asset set.' }
    $asset = Join-Path $root $component.asset
    if ((Get-Item -LiteralPath $asset).Length -ne $component.size -or (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant() -ne $component.sha256) { throw 'Release manifest and asset bytes disagree.' }
}
Write-Output "Verified $($seen.Count) Office release assets."
