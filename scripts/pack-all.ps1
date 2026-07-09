#requires -Version 5.1
<#
.SYNOPSIS
    Pack every shippable Broiler component into a folder of .nupkg/.snupkg files.

.DESCRIPTION
    Shared by CI (.github/workflows/nuget-packages.yml) and developers. Packs the
    in-tree component solutions plus the three meta-packages. Submodule components
    (DOM/CSS/Graphics/HTML/JS) publish from their own repos once patches 0011-0015
    land and their pointers are bumped; pass -IncludeSubmodules to also pack them
    from a monorepo checkout (requires those patches applied to the working trees).

    Packability itself is decided by convention in Broiler.Packaging.props
    (tests/demos/diagnostics/benchmarks/tools never pack), so this script only
    chooses which solutions/projects to feed to `dotnet pack`.

    Retries once on the transient Windows CS2012 PDB-lock error after shutting the
    build servers down.

.PARAMETER Output
    Directory to write packages to. Created if missing. Default: artifacts/nupkg.

.PARAMETER Configuration
    Build configuration. Default: Release.

.PARAMETER VersionSuffix
    Optional -p:VersionSuffix override (e.g. 'preview.2' or a CI build id). When
    omitted the version comes from Broiler.Packaging.props (0.1.0-preview.1).

.PARAMETER IncludeSubmodules
    Also pack the submodule components (requires patches 0011-0015 applied).
#>
[CmdletBinding()]
param(
    [string] $Output = 'artifacts/nupkg',
    [string] $Configuration = 'Release',
    [string] $VersionSuffix = '',
    [switch] $IncludeSubmodules
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = if ([IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $repoRoot $Output }
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# In-tree component pack targets (solutions + meta-packages).
$targets = @(
    'Broiler.Layout/Broiler.Layout.slnx',
    'Broiler.Documents/Broiler.Documents.slnx',
    'Broiler.Media/Broiler.Media.slnx',
    'Broiler.Input/Broiler.Input.slnx',
    'Broiler.UI/Broiler.UI.slnx',
    'Broiler.Media/Broiler.Media.All/Broiler.Media.All.csproj',
    'Broiler.Input/Broiler.Input.All/Broiler.Input.All.csproj',
    'Broiler.UI/src/Bundles/Broiler.UI.All/Broiler.UI.All.csproj'
)
if ($IncludeSubmodules) {
    $targets += @(
        'Broiler.DOM/Broiler.Dom.slnx',
        'Broiler.CSS/Broiler.CSS.slnx',
        'Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj'
        # Broiler.HTML and Broiler.JS: add their specific pack targets here once
        # their patches land; they publish from their own repos by default.
    )
}

$packArgs = @('-c', $Configuration, '-o', $outDir)
if ($VersionSuffix) { $packArgs += @("-p:VersionSuffix=$VersionSuffix") }

$failed = @()
foreach ($t in $targets) {
    $path = Join-Path $repoRoot $t
    if (-not (Test-Path $path)) { Write-Warning "skip missing target: $t"; continue }
    Write-Host "== pack $t"

    & dotnet pack $path @packArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "pack failed for $t (exit $LASTEXITCODE); shutting build servers and retrying once"
        & dotnet build-server shutdown | Out-Null
        & dotnet pack $path @packArgs
        if ($LASTEXITCODE -ne 0) { $failed += $t }
    }
}

$nupkgs = @(Get-ChildItem "$outDir/*.nupkg" -ErrorAction SilentlyContinue)
$snupkgs = @(Get-ChildItem "$outDir/*.snupkg" -ErrorAction SilentlyContinue)
Write-Host ""
Write-Host "Packed $($nupkgs.Count) .nupkg and $($snupkgs.Count) .snupkg into $outDir"

if ($failed.Count -gt 0) {
    Write-Error "pack failed for: $($failed -join ', ')"
    exit 1
}
