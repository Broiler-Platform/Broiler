$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue

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

    # Linux package test
    $publishLinux = Join-Path $scratch 'publish-linux'
    $catalogLinux = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'eng/office-products-linux.json') | ConvertFrom-Json
    foreach ($product in $catalogLinux.products) {
        $directory = Join-Path $publishLinux $product.id
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $directory $product.entryPoint), 'fixture, not executable')
        if ($product.id -eq 'boss') {
            New-Item -ItemType Directory -Path (Join-Path $directory 'wwwroot') | Out-Null
            [IO.File]::WriteAllText((Join-Path $directory 'wwwroot/index.html'), '<html>fixture</html>')
        }
    }
    $outputLinux = Join-Path $scratch 'package-linux'
    & (Join-Path $repoRoot 'scripts/package-office.ps1') -Version '0.1.0-demo.99' -PublishRoot $publishLinux -OutputRoot $outputLinux -Commit ('a' * 40) -Rid 'linux-x64'
    & (Join-Path $repoRoot 'scripts/verify-office-assets.ps1') -Directory (Join-Path $outputLinux 'assets')
    $extractedLinux = Join-Path $scratch 'extracted-linux'
    [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $outputLinux 'assets/Broiler-Office-0.1.0-demo.99-linux-x64.zip'), $extractedLinux)
    $bundleLinux = Join-Path $extractedLinux 'Broiler-Office-0.1.0-demo.99-linux-x64'
    if (!(Test-Path -LiteralPath (Join-Path $bundleLinux 'launcher/Broiler.Launcher.Linux'))) { throw 'Linux launcher isolation failed.' }
    if (!(Test-Path -LiteralPath (Join-Path $bundleLinux 'products/writer/Broiler.Writer.Linux'))) { throw 'Linux product isolation failed.' }
    if (!(Test-Path -LiteralPath (Join-Path $bundleLinux 'start-broiler-office.sh'))) { throw 'Linux start script missing.' }
    $manifestLinux = Get-Content -Raw -LiteralPath (Join-Path $bundleLinux 'office-release.json') | ConvertFrom-Json
    if (!$manifestLinux.demo -or $manifestLinux.components.Count -ne 4) { throw 'Linux demo package metadata failed.' }

    $writer = Join-Path $output 'assets/Broiler-writer-0.1.0-demo.99-win-x64.zip'
    [IO.File]::AppendAllText($writer, 'tampered')
    $rejected = $false
    try { & (Join-Path $repoRoot 'scripts/verify-office-assets.ps1') -Directory (Join-Path $output 'assets') } catch { $rejected = $true }
    if (!$rejected) { throw 'Corrupted asset was accepted.' }
    Remove-Item -LiteralPath (Join-Path $publish 'cell/Broiler.Cells.Windows.exe')
    $rejected = $false
    try { & (Join-Path $repoRoot 'scripts/package-office.ps1') -Version '0.1.0-demo.99' -PublishRoot $publish -OutputRoot (Join-Path $scratch 'incomplete') -Commit ('a' * 40) } catch { $rejected = $true }
    if (!$rejected) { throw 'Incomplete product matrix was accepted.' }
    Write-Output 'Office packaging fixtures passed: Windows and Linux isolation, full matrix, checksum rejection, missing-product rejection.'
} finally {
    $resolved = [IO.Path]::GetFullPath($scratch)
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (!$resolved.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or !(Split-Path $resolved -Leaf).StartsWith('broiler-office-package-test-')) { throw 'Refusing cleanup outside the fixture directory.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
