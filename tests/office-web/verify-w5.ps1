$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $repo 'artifacts/web-w5'
$previousEngines = $env:W5_ENGINES
function Invoke-Checked([string] $Executable, [string[]] $Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE" }
}
Push-Location $PSScriptRoot
try {
    $env:W5_ENGINES = $null
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $workloads = & dotnet workload list
    if (($workloads -join "`n") -notmatch 'wasm-tools\s+10\.0\.114/10\.0\.100') { throw 'Provision the pinned W0 wasm-tools manifest before W5 verification.' }
    & dotnet --info | Out-File (Join-Path $output 'dotnet-info.txt')
    $workloads | Out-File (Join-Path $output 'workloads.txt')
    $writer = Join-Path $repo 'Broiler.Writer/src/Broiler.Writer.WebAssembly'
    $cells = Join-Path $repo 'Broiler.Cells/src/Broiler.Cells.WebAssembly'
    if ((Get-FileHash (Join-Path $writer 'wwwroot/broiler.recovery.js')).Hash -ne (Get-FileHash (Join-Path $cells 'wwwroot/broiler.recovery.js')).Hash) { throw 'Vendored recovery modules differ.' }
    Invoke-Checked dotnet @('test', (Join-Path $repo 'Broiler.Writer/src/Broiler.Writer.FormatCodes.Tests/Broiler.Writer.FormatCodes.Tests.csproj'), '-c', 'Release', '-m:1', '-p:UseBroilerUiSource=false')
    Invoke-Checked dotnet @('run', '--project', (Join-Path $repo 'Broiler.Cells/tests/Broiler.Cells.Tests/Broiler.Cells.Tests.csproj'), '-c', 'Release', '-p:UseBroilerUiSource=false')
    foreach ($item in @(@{ Path=$writer; Name='writer'; Project='Broiler.Writer.WebAssembly.csproj' }, @{ Path=$cells; Name='cell'; Project='Broiler.Cells.WebAssembly.csproj' })) {
        Invoke-Checked dotnet @('publish', (Join-Path $item.Path $item.Project), '-c', 'Release', '-m:1', '-p:UseBroilerUiSource=false', '-p:RunAOTCompilation=false', '-p:RestoreLockedMode=true', '-o', (Join-Path $output $item.Name))
        Copy-Item -LiteralPath (Join-Path $item.Path 'obj/project.assets.json') -Destination (Join-Path $output ($item.Name + '-project.assets.json'))
    }
    Invoke-Checked npm @('run', 'test:recovery')
    Invoke-Checked dotnet @('run', '--project', 'writer-fixtures/WriterFixtures.csproj', '-c', 'Release', '-p:UseBroilerUiSource=false', '--', 'inspect-recovery', (Join-Path $output 'browser'), (Join-Path $output 'writer-fidelity.json'))
    Invoke-Checked node @('recovery-fidelity.mjs')
} finally { $env:W5_ENGINES = $previousEngines; Pop-Location }
