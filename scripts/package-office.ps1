[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+-demo\.[1-9][0-9]*$')][string] $Version,
    [Parameter(Mandatory = $true)][string] $PublishRoot,
    [Parameter(Mandatory = $true)][string] $OutputRoot,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-f0-9]{40}$')][string] $Commit,
    [switch] $WorkspaceDirty
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent
$catalog = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'eng/office-products.json') | ConvertFrom-Json
if ($catalog.schemaVersion -ne 1 -or $catalog.rid -ne 'win-x64' -or
    (@($catalog.products.id | Sort-Object) -join ',') -ne 'boss,browser,cell,launcher,plate,writer') { throw 'Invalid Office product catalog.' }
$publish = (Resolve-Path -LiteralPath $PublishRoot).Path
$output = [IO.Path]::GetFullPath($OutputRoot)
if ($output.StartsWith($publish + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Package output cannot be inside a published payload tree.' }
if (Test-Path -LiteralPath $output) { throw 'Office package output must be a new directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
$assets = Join-Path $output 'assets'
$bundle = Join-Path $output "bundle/Broiler-Office-$Version-win-x64"
foreach ($directory in @($assets, $bundle, (Join-Path $bundle 'packages'), (Join-Path $bundle 'products'), (Join-Path $bundle 'licenses'))) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}
$components = @()
$sbomComponents = @{}
foreach ($product in $catalog.products) {
    $source = Join-Path $publish $product.id
    if (!(Test-Path -LiteralPath (Join-Path $source $product.entryPoint))) { throw "Missing publish entry point: $($product.id)" }
    if (@(Get-ChildItem -LiteralPath $source -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Published payloads must not contain links.' }
    if ($product.id -eq 'boss' -and !(Test-Path -LiteralPath (Join-Path $source 'wwwroot/index.html'))) { throw 'BOSS web assets are missing.' }
    $asset = "Broiler-$($product.id)-$Version-win-x64.zip"
    $archive = Join-Path $assets $asset
    [IO.Compression.ZipFile]::CreateFromDirectory($source, $archive, [IO.Compression.CompressionLevel]::Optimal, $false)
    if ((Get-Item -LiteralPath $archive).Length -gt 2GB) { throw 'Component exceeds installer archive limit.' }
    Copy-Item -LiteralPath $archive -Destination (Join-Path $bundle 'packages')
    $destination = if ($product.id -eq 'launcher') { Join-Path $bundle 'launcher' } else { Join-Path $bundle "products/$($product.id)" }
    Copy-Item -LiteralPath $source -Destination $destination -Recurse
    $components += [ordered]@{
        id = $product.id; version = $Version; rid = 'win-x64'; asset = $asset
        size = (Get-Item -LiteralPath $archive).Length
        sha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
        archive = 'zip'; entryPoint = $product.entryPoint; invocation = 'native'; publishMode = 'self-contained'
    }
    foreach ($deps in Get-ChildItem -LiteralPath $source -Filter '*.deps.json') {
        $libraries = (Get-Content -Raw -LiteralPath $deps.FullName | ConvertFrom-Json).libraries
        foreach ($library in $libraries.PSObject.Properties) {
            if ($library.Value.type -ne 'package') { continue }
            $separator = $library.Name.LastIndexOf('/')
            $name = $library.Name.Substring(0, $separator)
            $dependencyVersion = $library.Name.Substring($separator + 1)
            $sbomComponents[$library.Name] = [ordered]@{ type = 'library'; name = $name; version = $dependencyVersion; purl = "pkg:nuget/$name@$dependencyVersion" }
        }
    }
    $componentRoot = ($product.project -split '/')[0]
    foreach ($licenseName in @('LICENSE', 'LICENSE.md', 'LICENSE.txt')) {
        $license = Join-Path $repoRoot "$componentRoot/$licenseName"
        if (Test-Path -LiteralPath $license) { Copy-Item -LiteralPath $license -Destination (Join-Path $bundle "licenses/$($product.id)-$licenseName"); break }
    }
}
$release = [ordered]@{
    schemaVersion = 1; officeVersion = $Version; channel = 'preview'; tag = "office-v$Version"
    commit = $Commit; minimumLauncherVersion = '0.1.0-demo.1'; demo = $true; components = @($components)
}
$release | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $bundle 'office-release.json') -Encoding utf8NoBOM
$build = [ordered]@{ version = $Version; commit = $Commit; rid = 'win-x64'; demo = $true; workspaceDirty = [bool]$WorkspaceDirty; builtAtUtc = [DateTime]::UtcNow.ToString('O'); submodules = @(& git -C $repoRoot submodule status --recursive) }
$build | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $bundle 'BUILD-INFO.json') -Encoding utf8NoBOM
$bom = [ordered]@{ bomFormat = 'CycloneDX'; specVersion = '1.6'; version = 1; serialNumber = "urn:uuid:$([guid]::NewGuid())"; components = @($sbomComponents.GetEnumerator() | Sort-Object Name | ForEach-Object Value) }
$bom | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $bundle 'SBOM.cdx.json') -Encoding utf8NoBOM
Copy-Item -LiteralPath (Join-Path $repoRoot 'Broiler.Launcher/packaging/README-demo.md') -Destination (Join-Path $bundle 'README.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $bundle 'licenses/LICENSE')
@'
@echo off
start "Broiler Office Demo" "%~dp0launcher\Broiler.Launcher.Windows.exe"
'@ | Set-Content -LiteralPath (Join-Path $bundle 'Start-Broiler-Office.cmd') -Encoding ascii
foreach ($name in @('office-release.json', 'BUILD-INFO.json', 'SBOM.cdx.json', 'README.md')) { Copy-Item -LiteralPath (Join-Path $bundle $name) -Destination $assets }
$bundleArchive = Join-Path $assets "Broiler-Office-$Version-win-x64.zip"
[IO.Compression.ZipFile]::CreateFromDirectory($bundle, $bundleArchive, [IO.Compression.CompressionLevel]::Optimal, $true)
$checksums = @(Get-ChildItem -LiteralPath $assets -File | Sort-Object Name | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name })
$checksums | Set-Content -LiteralPath (Join-Path $assets 'SHA256SUMS.txt') -Encoding ascii
Write-Output $bundleArchive
