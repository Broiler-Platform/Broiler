$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $repo 'artifacts/web-w4'
$previousEngines = $env:W4_ENGINES
function Invoke-Checked([string] $Executable, [string[]] $Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE" }
}
Push-Location $PSScriptRoot
try {
    $env:W4_ENGINES = $null
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $workloads = & dotnet workload list
    if (($workloads -join "`n") -notmatch 'wasm-tools\s+10\.0\.114/10\.0\.100') { throw 'Provision the pinned W0 wasm-tools manifest before W4 verification.' }
    & dotnet --info | Out-File (Join-Path $output 'dotnet-info.txt')
    $workloads | Out-File (Join-Path $output 'workloads.txt')
    # Cells uses xUnit v3's executable runner / Microsoft.Testing.Platform.
    Invoke-Checked dotnet @('run', '--project', (Join-Path $repo 'Broiler.Cells/tests/Broiler.Cells.Tests/Broiler.Cells.Tests.csproj'), '-c', 'Release', '-p:UseBroilerUiSource=false')
    $project = Join-Path $repo 'Broiler.Cells/src/Broiler.Cells.WebAssembly/Broiler.Cells.WebAssembly.csproj'
    Invoke-Checked dotnet @('publish', $project, '-c', 'Release', '-m:1', '-p:UseBroilerUiSource=false', '-p:RunAOTCompilation=false', '-o', (Join-Path $output 'cell'))
    Copy-Item -LiteralPath (Join-Path (Split-Path $project) 'obj/project.assets.json') -Destination (Join-Path $output 'cell-project.assets.json')
    Invoke-Checked npm @('run', 'test:cells')
} finally { $env:W4_ENGINES = $previousEngines; Pop-Location }
