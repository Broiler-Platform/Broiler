$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $repo 'artifacts/web-w3'
$previousEngines = $env:W3_ENGINES
function Invoke-Checked([string] $Executable, [string[]] $Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE" }
}
Push-Location $PSScriptRoot
try {
    $env:W3_ENGINES = $null
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $workloads = & dotnet workload list
    if (($workloads -join "`n") -notmatch 'wasm-tools\s+10\.0\.114/10\.0\.100') { throw 'Provision the pinned W0 wasm-tools manifest before W3 verification.' }
    & dotnet --info | Out-File (Join-Path $output 'dotnet-info.txt')
    $workloads | Out-File (Join-Path $output 'workloads.txt')
    Invoke-Checked dotnet @('test', (Join-Path $repo 'Broiler.Writer/src/Broiler.Writer.FormatCodes.Tests/Broiler.Writer.FormatCodes.Tests.csproj'), '-c', 'Release', '-m:1', '-p:UseBroilerUiSource=false')
    Invoke-Checked dotnet @('build', (Join-Path $repo 'Broiler.Writer/src/Broiler.Writer.Windows/Broiler.Writer.Windows.csproj'), '-c', 'Release', '-m:1', '-p:UseBroilerUiSource=false')
    Invoke-Checked dotnet @('run', '--project', 'writer-fixtures/WriterFixtures.csproj', '-c', 'Release', '-p:UseBroilerUiSource=false', '--', 'generate', (Join-Path $output 'fixtures'))
    $project = Join-Path $repo 'Broiler.Writer/src/Broiler.Writer.WebAssembly/Broiler.Writer.WebAssembly.csproj'
    Invoke-Checked dotnet @('publish', $project, '-c', 'Release', '-m:1', '-p:UseBroilerUiSource=false', '-p:RunAOTCompilation=false', '-p:RestorePackagesWithLockFile=true', '-p:RestoreLockedMode=true', '-o', (Join-Path $output 'writer'))
    Copy-Item -LiteralPath (Join-Path (Split-Path $project) 'obj/project.assets.json') -Destination (Join-Path $output 'writer-project.assets.json')
    Invoke-Checked npm @('run', 'test:writer')
    Invoke-Checked dotnet @('run', '--project', 'writer-fixtures/WriterFixtures.csproj', '-c', 'Release', '-p:UseBroilerUiSource=false', '--', 'inspect', (Join-Path $output 'browser'), (Join-Path $output 'contents.json'))
    Invoke-Checked node @('writer-fidelity.mjs')
} finally { $env:W3_ENGINES = $previousEngines; Pop-Location }
