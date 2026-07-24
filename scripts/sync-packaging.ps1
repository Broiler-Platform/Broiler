#requires -Version 5.1
<#
.SYNOPSIS
    Vendor the canonical Broiler packaging props + icon into every component
    and wire each component's Directory.Build.props to import the local copy.

.DESCRIPTION
    Implements the vendoring design tracked by the release and distribution
    work in docs/ROADMAP.md. Each component is self-contained: it carries its
    own eng/Broiler.Packaging.props and eng/icon.png
    so it builds and packs standalone -- important because the in-tree components
    are slated to become submodules.

    The script is idempotent: re-running it refreshes the vendored files and
    leaves an already-wired Directory.Build.props untouched.

    Submodule components (DOM, CSS, Graphics, HTML, JS) are NOT vendored by
    default: editing them is a submodule change subject to the push-or-patch
    workflow in CLAUDE.md. Pass -IncludeSubmodules to stage those working-tree
    changes for patch generation.

.PARAMETER IncludeSubmodules
    Also vendor into the git-submodule components. Off by default.

.PARAMETER WhatIf
    Show what would change without writing.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch] $IncludeSubmodules
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$canonicalProps = Join-Path $repoRoot 'eng\Broiler.Packaging.props'
$canonicalIcon  = Join-Path $repoRoot 'eng\icon.png'

foreach ($f in @($canonicalProps, $canonicalIcon)) {
    if (-not (Test-Path $f)) { throw "Canonical file missing: $f" }
}

# In-tree component library roots (Writer/Browser apps and tooling excluded).
$inTree = @(
    'Broiler.Layout',
    'Broiler.Documents',
    'Broiler.Media',
    'Broiler.Input',
    'Broiler.UI'
)

# Submodule component roots (push-or-patch workflow; opt in via -IncludeSubmodules).
$submodules = @(
    'Broiler.DOM',
    'Broiler.CSS',
    'Broiler.Graphics',
    'Broiler.HTML',
    'Broiler.JS'
)

$components = $inTree
if ($IncludeSubmodules) { $components += $submodules }

$importLine = '<Import Project="$(MSBuildThisFileDirectory)eng\Broiler.Packaging.props" />'

function Set-DirectoryBuildProps {
    param([string] $ComponentDir)

    $dbp = Join-Path $ComponentDir 'Directory.Build.props'

    if (-not (Test-Path $dbp)) {
        # No existing props: create a self-contained one that chains to a
        # parent Directory.Build.props when present (in-tree build) and always
        # imports the vendored packaging props. GetPathOfFileAbove returns ''
        # for a standalone/submodule build, so that import is simply skipped.
        $content = @"
<Project>

  <!-- Chain to a parent Directory.Build.props when one exists (in-tree build).
       Skipped for standalone / submodule builds. The path is captured in a
       property first so the inner quotes don't collide with the Condition. -->
  <PropertyGroup>
    <BroilerParentBuildProps>`$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '`$(MSBuildThisFileDirectory)../'))</BroilerParentBuildProps>
  </PropertyGroup>
  <Import Project="`$(BroilerParentBuildProps)" Condition="'`$(BroilerParentBuildProps)' != ''" />

  <!-- Broiler NuGet packaging metadata (vendored; do not edit by hand). -->
  $importLine

</Project>
"@
        if ($PSCmdlet.ShouldProcess($dbp, 'create Directory.Build.props')) {
            Set-Content -Path $dbp -Value $content -Encoding UTF8
        }
        Write-Host "  created  Directory.Build.props"
        return
    }

    $text = Get-Content -Path $dbp -Raw
    if ($text -match 'eng[\\/]Broiler\.Packaging\.props') {
        Write-Host "  ok       Directory.Build.props already imports packaging props"
        return
    }

    # Inject the import as the first child of the root <Project> element.
    $injected = [regex]::Replace(
        $text,
        '(<Project[^>]*>)',
        "`$1`r`n`r`n  <!-- Broiler NuGet packaging metadata (vendored; do not edit by hand). -->`r`n  $importLine`r`n",
        1)

    if ($injected -eq $text) {
        Write-Warning "  could not locate <Project> in $dbp; import NOT added"
        return
    }
    if ($PSCmdlet.ShouldProcess($dbp, 'inject packaging import')) {
        Set-Content -Path $dbp -Value $injected -Encoding UTF8
    }
    Write-Host "  patched  Directory.Build.props (import injected)"
}

foreach ($name in $components) {
    $dir = Join-Path $repoRoot $name
    if (-not (Test-Path $dir)) { Write-Warning "skip missing component: $name"; continue }

    Write-Host "== $name"
    $engDir = Join-Path $dir 'eng'
    if (-not (Test-Path $engDir)) {
        if ($PSCmdlet.ShouldProcess($engDir, 'create eng dir')) {
            New-Item -ItemType Directory -Path $engDir | Out-Null
        }
    }

    if ($PSCmdlet.ShouldProcess("$engDir\Broiler.Packaging.props", 'vendor props')) {
        Copy-Item $canonicalProps (Join-Path $engDir 'Broiler.Packaging.props') -Force
    }
    if ($PSCmdlet.ShouldProcess("$engDir\icon.png", 'vendor icon')) {
        Copy-Item $canonicalIcon (Join-Path $engDir 'icon.png') -Force
    }
    Write-Host "  vendored eng\Broiler.Packaging.props + eng\icon.png"

    Set-DirectoryBuildProps -ComponentDir $dir
}

Write-Host ""
Write-Host "Done. Packability is by naming convention (tests/demos/diagnostics excluded)."
