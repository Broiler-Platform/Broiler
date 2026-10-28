$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$scratch = Join-Path ([IO.Path]::GetTempPath()) ("broiler-office-package-test-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $scratch | Out-Null
try {
    $publish = Join-Path $scratch 'publish'
    $catalog = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'eng/office-products.json') | ConvertFrom-Json
    foreach ($product in $catalog.products) {
        $directory = Join-Path $publish $product.id
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $directory $product.entryPoint), 'fixture, not executable')
        if ($product.id -eq 'boss') {
            New-Item -ItemType Directory -Path (Join-Path $directory 'wwwroot') | Out-Null
            [IO.File]::WriteAllText((Join-Path $directory 'wwwroot/index.html'), '<html>fixture</html>')
        }
    }
    $output = Join-Path $scratch 'package'
    & (Join-Path $repoRoot 'scripts/package-office.ps1') -Version '0.1.0-demo.99' -PublishRoot $publish -OutputRoot $output -Commit ('a' * 40)
    & (Join-Path $repoRoot 'scripts/verify-office-assets.ps1') -Directory (Join-Path $output 'assets')
    $extracted = Join-Path $scratch 'extracted'
    [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $output 'assets/Broiler-Office-0.1.0-demo.99-win-x64.zip'), $extracted)
    $bundle = Join-Path $extracted 'Broiler-Office-0.1.0-demo.99-win-x64'
    if (!(Test-Path -LiteralPath (Join-Path $bundle 'launcher/Broiler.Launcher.Windows.exe'))) { throw 'Launcher isolation failed.' }
    if (!(Test-Path -LiteralPath (Join-Path $bundle 'products/writer/Broiler.Writer.Windows.exe'))) { throw 'Product isolation failed.' }
    $manifest = Get-Content -Raw -LiteralPath (Join-Path $bundle 'office-release.json') | ConvertFrom-Json
    if (!$manifest.demo -or $manifest.components.Count -ne 6) { throw 'Demo package metadata failed.' }
    $writer = Join-Path $output 'assets/Broiler-writer-0.1.0-demo.99-win-x64.zip'
    [IO.File]::AppendAllText($writer, 'tampered')
    $rejected = $false
    try { & (Join-Path $repoRoot 'scripts/verify-office-assets.ps1') -Directory (Join-Path $output 'assets') } catch { $rejected = $true }
    if (!$rejected) { throw 'Corrupted asset was accepted.' }
    Remove-Item -LiteralPath (Join-Path $publish 'cell/Broiler.Cells.Windows.exe')
    $rejected = $false
    try { & (Join-Path $repoRoot 'scripts/package-office.ps1') -Version '0.1.0-demo.99' -PublishRoot $publish -OutputRoot (Join-Path $scratch 'incomplete') -Commit ('a' * 40) } catch { $rejected = $true }
    if (!$rejected) { throw 'Incomplete product matrix was accepted.' }
    Write-Output 'Office packaging fixtures passed: isolation, full matrix, checksum rejection, missing-product rejection.'
} finally {
    $resolved = [IO.Path]::GetFullPath($scratch)
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (!$resolved.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or !(Split-Path $resolved -Leaf).StartsWith('broiler-office-package-test-')) { throw 'Refusing cleanup outside the fixture directory.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
