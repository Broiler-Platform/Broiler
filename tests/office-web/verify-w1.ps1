$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Invoke-Checked([string] $Executable, [string[]] $Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE" }
}
Push-Location $PSScriptRoot
try {
    $inventory = & dotnet workload list
    if (($inventory -join "`n") -notmatch 'wasm-tools\s+10\.0\.114/10\.0\.100') { throw 'Provision the W0 pinned wasm-tools manifest before running W1.' }
    Invoke-Checked dotnet @('test', (Join-Path $repo 'Broiler.Hosting/tests/Broiler.Hosting.WebAssembly.Tests/Broiler.Hosting.WebAssembly.Tests.csproj'), '-c', 'Release', '-m:1')
    Invoke-Checked dotnet @('test', (Join-Path $repo 'Broiler.Launcher/tests/Broiler.Launcher.Tests/Broiler.Launcher.Tests.csproj'), '-c', 'Release', '-m:1')
    Invoke-Checked dotnet @('run', '--project', (Join-Path $repo 'Broiler.Graphics/src/tests/Broiler.Graphics.WebAssembly.Tests/Broiler.Graphics.WebAssembly.Tests.csproj'), '-c', 'Release')
    $packages = Join-Path $repo ('artifacts/web-w1/package-probe-' + [guid]::NewGuid().ToString('N'))
    foreach ($project in @(
        'Broiler.Graphics/src/Broiler.Graphics.WebAssembly/Broiler.Graphics.WebAssembly.csproj',
        'Broiler.Hosting/src/Broiler.Hosting.Contracts/Broiler.Hosting.Contracts.csproj',
        'Broiler.Hosting/src/Broiler.Hosting.WebAssembly/Broiler.Hosting.WebAssembly.csproj'
    )) { Invoke-Checked dotnet @('pack', (Join-Path $repo $project), '-c', 'Release', '-m:1', '-o', $packages) }
    $graphicsPackage = (Get-ChildItem $packages -Filter 'Broiler.Graphics.WebAssembly.*.nupkg').FullName
    $hostingPackage = (Get-ChildItem $packages -Filter 'Broiler.Hosting.WebAssembly.*.nupkg').FullName
    Invoke-Checked python @('verify-packages.py', $graphicsPackage, $hostingPackage)
    Invoke-Checked dotnet @('publish', 'contract-sample/ContractSample.csproj', '-c', 'Release', '-m:1', '-p:RunAOTCompilation=false', '-o', (Join-Path $repo 'artifacts/web-w1/sample'))
    Invoke-Checked npm @('run', 'test:contracts')
} finally { Pop-Location }
