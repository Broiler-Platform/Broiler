$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $PSScriptRoot
try {
    $output = Join-Path $repo 'artifacts/web-w0'
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    & dotnet --info | Out-File (Join-Path $output 'dotnet-info.txt')
    $workloads = & dotnet workload list
    $workloads | Out-File (Join-Path $output 'workloads.txt')
    if (($workloads -join "`n") -notmatch 'wasm-tools\s+10\.0\.114/10\.0\.100') {
        throw 'Baseline requires wasm-tools manifest 10.0.114/10.0.100. Provision that workload before publishing.'
    }
    foreach ($product in @(
        @{ Name = 'writer'; Project = 'Broiler.Writer/src/Broiler.Writer.WebAssembly/Broiler.Writer.WebAssembly.csproj' },
        @{ Name = 'cell'; Project = 'Broiler.Cells/src/Broiler.Cells.WebAssembly/Broiler.Cells.WebAssembly.csproj' }
    )) {
        & dotnet publish (Join-Path $repo $product.Project) -c Release -m:1 -p:RunAOTCompilation=false -p:UseBroilerUiSource=false -o (Join-Path $output $product.Name)
        if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($product.Name)" }
        $assets = Join-Path (Split-Path (Join-Path $repo $product.Project)) 'obj/project.assets.json'
        Copy-Item -LiteralPath $assets -Destination (Join-Path $output "$($product.Name)-project.assets.json")
    }
} finally { Pop-Location }
