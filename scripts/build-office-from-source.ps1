[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+-demo\.[1-9][0-9]*$')][string] $Version,
    [Parameter(Mandatory = $true)][string] $OutputRoot,
    [ValidateSet('win-x64', 'linux-x64')][string] $Rid = $(if ($IsWindows) { 'win-x64' } else { 'linux-x64' }),
    [switch] $RequireClean
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $output) { throw 'Office build output must be a new directory.' }
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve source commit.' }
$dirty = @(& git -C $repoRoot status --porcelain).Count -ne 0
if ($RequireClean -and $dirty) { throw 'Release builds require a clean committed source tree.' }
New-Item -ItemType Directory -Path $output | Out-Null
$publish = Join-Path $output 'publish'
$catalogFile = if ($Rid -eq 'linux-x64') { Join-Path $repoRoot 'eng/office-products-linux.json' } else { Join-Path $repoRoot 'eng/office-products.json' }
$catalog = Get-Content -Raw -LiteralPath $catalogFile | ConvertFrom-Json
$configuration = if ($Rid -eq 'win-x64') { 'Release-Windows' } else { 'Release-Linux' }
# BOSS invokes the WASM clients through MSBuild, not ProjectReference. Restore them
# separately without a host RID, and force a fresh content-hashed client stage.
foreach ($wasmClient in @(
    'Broiler.Launcher/src/Broiler.Launcher.WebAssembly/Broiler.Launcher.WebAssembly.csproj',
    'Broiler.Writer/src/Broiler.Writer.WebAssembly/Broiler.Writer.WebAssembly.csproj',
    'Broiler.Cells/src/Broiler.Cells.WebAssembly/Broiler.Cells.WebAssembly.csproj',
    'Broiler.Plate/src/Broiler.Plate.WebAssembly/Broiler.Plate.WebAssembly.csproj'
)) {
    & dotnet restore (Join-Path $repoRoot $wasmClient) -p:Configuration=$configuration
    if ($LASTEXITCODE -ne 0) { throw "WebAssembly client restore failed: $wasmClient" }
}
foreach ($product in $catalog.products) {
    Write-Output "Publishing Office component: $($product.id) ($Rid)"
    $destination = Join-Path $publish $product.id
    & dotnet publish (Join-Path $repoRoot $product.project) -c $configuration -r $Rid --self-contained true --output $destination `
        -p:Version=$Version -p:BroilerOfficeDemo=true -p:UseAppHost=true -p:PublishAot=false -p:PublishSingleFile=false `
        -p:PublishReadyToRun=true -p:ForceOfficeClientsPublish=true
    if ($LASTEXITCODE -ne 0) { throw "Office publish failed: $($product.id)" }
    if ($product.id -eq 'boss') {
        & (Join-Path $PSScriptRoot 'package-boss.ps1') -PackageDirectory $destination -Platform (if ($Rid -eq 'win-x64') { 'windows' } else { 'linux' })
        if ($Rid -eq 'win-x64') {
            @'
@echo off
"%~dp0Broiler.Office.Server.exe" --urls http://127.0.0.1:5555 %*
'@ | Set-Content -LiteralPath (Join-Path $destination 'Start-BOSS.cmd') -Encoding ascii
        }
    }
}
& (Join-Path $PSScriptRoot 'package-office.ps1') -Version $Version -PublishRoot $publish -OutputRoot (Join-Path $output 'package') -Commit $commit -Rid $Rid -WorkspaceDirty:$dirty
if ($LASTEXITCODE -ne 0) { throw 'Office packaging failed.' }
Write-Output "Office assets: $(Join-Path $output 'package/assets')"
