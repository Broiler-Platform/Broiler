<#
.SYNOPSIS
    Publishes the Phase 0 payload probes and records what composing the Roslyn
    semantic service costs a browser host to download.

.DESCRIPTION
    Publishes a control probe (portable classification only) and a Roslyn probe
    with identical trimmed-interpreted settings, then reports the size of each
    published _framework directory and the difference between them.

    The Roslyn probe's trim warnings are not reviewed. This measures how large
    the composition is, which is the question that gates the Android and browser
    decision; it does not claim the trimmed composition works. See the
    unmeasured modes in the Roslyn harness report.

.EXAMPLE
    pwsh tests/broiler-code-phase0/payload-probes/measure-payloads.ps1 -OutputPath baseline.json
#>
[CmdletBinding()]
param(
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$probeRoot = $PSScriptRoot

function Measure-Probe {
    param([string]$Name)

    $project = Join-Path $probeRoot "$Name/$Name.csproj"

    # An explicit output directory. The SDK's default publish path for this
    # profile has moved between releases, and a probe that silently measures
    # the wrong directory is worse than one that fails.
    $publishDir = Join-Path $probeRoot "$Name/bin/publish"
    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }

    Write-Host "Publishing $Name ..."
    & dotnet publish $project -c Release -r browser-wasm --self-contained true -o $publishDir --nologo -v:q | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing $Name failed with exit code $LASTEXITCODE."
    }

    $framework = Join-Path $publishDir 'wwwroot/_framework'
    if (-not (Test-Path $framework)) {
        throw "No _framework directory was produced for $Name at $framework."
    }

    # Only the files a browser actually downloads on first load. Compressed
    # siblings are counted separately because that is what crosses the network.
    $all = Get-ChildItem $framework -File -Recurse
    $compressed = $all | Where-Object { $_.Extension -in '.br', '.gz' }
    $raw = $all | Where-Object { $_.Extension -notin '.br', '.gz' }
    $brotli = $all | Where-Object { $_.Extension -eq '.br' }

    # Satellite resource assemblies are localized diagnostic message text.
    # Diagnostic identity never depends on message text, so they are separable
    # payload — and on the recorded baseline they are the majority of it, which
    # is only visible if they are counted apart from the code.
    $satelliteRaw = $raw | Where-Object { $_.Name -like '*.resources.*' }
    $satelliteBrotli = $brotli | Where-Object { $_.Name -like '*.resources.*' }

    [pscustomobject]@{
        probe                 = $Name
        fileCount             = $raw.Count
        rawBytes              = [int64]($raw | Measure-Object Length -Sum).Sum
        brotliBytes           = [int64]($brotli | Measure-Object Length -Sum).Sum
        compressedFiles       = $compressed.Count
        satelliteFileCount    = $satelliteRaw.Count
        satelliteRawBytes     = [int64](($satelliteRaw | Measure-Object Length -Sum).Sum)
        satelliteBrotliBytes  = [int64](($satelliteBrotli | Measure-Object Length -Sum).Sum)
        largestAssemblies     = @(
            $raw | Where-Object { $_.Extension -eq '.wasm' -or $_.Extension -eq '.dll' } |
                Sort-Object Length -Descending | Select-Object -First 8 |
                ForEach-Object { [pscustomobject]@{ name = $_.Name; bytes = [int64]$_.Length } }
        )
    }
}

$baseline = Measure-Probe -Name 'Probe.Wasm.Baseline'
$roslyn = Measure-Probe -Name 'Probe.Wasm.Roslyn'

$report = [pscustomobject]@{
    schemaVersion = 1
    recordedOn    = @{
        framework       = (& dotnet --version)
        operatingSystem = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        architecture    = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    }
    profile       = 'browser-wasm, self-contained, PublishTrimmed=true, RunAOTCompilation=false, InvariantGlobalization=true'
    caveat        = 'Size only. Roslyn trim warnings are unreviewed, so this does not claim the trimmed composition runs.'
    probes        = @($baseline, $roslyn)
    delta         = [pscustomobject]@{
        rawBytes    = $roslyn.rawBytes - $baseline.rawBytes
        brotliBytes = $roslyn.brotliBytes - $baseline.brotliBytes
    }
    # What the composition costs once localized message resources are excluded.
    # Reported separately rather than instead: dropping them is a decision with
    # a user-visible consequence, so both numbers have to be on the table.
    deltaWithoutSatellites = [pscustomobject]@{
        rawBytes    = ($roslyn.rawBytes - $roslyn.satelliteRawBytes) -
                      ($baseline.rawBytes - $baseline.satelliteRawBytes)
        brotliBytes = ($roslyn.brotliBytes - $roslyn.satelliteBrotliBytes) -
                      ($baseline.brotliBytes - $baseline.satelliteBrotliBytes)
    }
}

$json = $report | ConvertTo-Json -Depth 6
if ($OutputPath) {
    $json | Out-File -FilePath $OutputPath -Encoding utf8
    Write-Host "Wrote $OutputPath"
}

$json
