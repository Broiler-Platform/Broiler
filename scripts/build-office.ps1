[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+-demo\.[1-9][0-9]*$')][string] $Version,
    [Parameter(Mandatory = $true)][string] $OutputRoot,
    [switch] $RequireClean
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$IsWindows) { throw 'The Office demo currently targets Windows x64.' }
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $output) { throw 'Office build output must be a new directory.' }
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve source commit.' }
$dirty = @(& git -C $repoRoot status --porcelain).Count -ne 0
if ($RequireClean -and $dirty) { throw 'Release builds require a clean committed source tree.' }
New-Item -ItemType Directory -Path $output | Out-Null
$publish = Join-Path $output 'publish'
$catalog = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'eng/office-products.json') | ConvertFrom-Json
# BOSS invokes the WASM client through MSBuild, not ProjectReference. Restore it
# separately without a Windows RID, and force a fresh content-hashed client stage.
& dotnet restore (Join-Path $repoRoot 'Broiler.Writer/src/Broiler.Writer.WebAssembly/Broiler.Writer.WebAssembly.csproj') -p:Configuration=Release-Windows
if ($LASTEXITCODE -ne 0) { throw 'Writer WebAssembly restore failed.' }
foreach ($product in $catalog.products) {
    Write-Output "Publishing Office component: $($product.id)"
    $destination = Join-Path $publish $product.id
    & dotnet publish (Join-Path $repoRoot $product.project) -c Release-Windows -r win-x64 --self-contained true --output $destination `
        -p:Version=$Version -p:BroilerOfficeDemo=true -p:UseAppHost=true -p:PublishAot=false -p:PublishSingleFile=false `
        -p:PublishReadyToRun=true -p:ForceWriterClientPublish=true
    if ($LASTEXITCODE -ne 0) { throw "Office publish failed: $($product.id)" }
    if ($product.id -eq 'boss') {
        & (Join-Path $PSScriptRoot 'package-boss.ps1') -PackageDirectory $destination -Platform windows
        @'
@echo off
"%~dp0Broiler.Office.Server.exe" --urls http://127.0.0.1:5555 %*
'@ | Set-Content -LiteralPath (Join-Path $destination 'Start-BOSS.cmd') -Encoding ascii
    }
}
& (Join-Path $PSScriptRoot 'package-office.ps1') -Version $Version -PublishRoot $publish -OutputRoot (Join-Path $output 'package') -Commit $commit -WorkspaceDirty:$dirty
if ($LASTEXITCODE -ne 0) { throw 'Office packaging failed.' }
Write-Output "Office assets: $(Join-Path $output 'package/assets')"
