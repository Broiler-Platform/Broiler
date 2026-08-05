#requires -Version 5.1
<#
.SYNOPSIS
    (Re)generate the convenience meta-packages Broiler.<Component>.All for the
    large component families (Media, Input, UI).

.DESCRIPTION
    A meta-package carries no assembly (IncludeBuildOutput=false); it only
    declares PackageReferences to every cross-platform, shippable library in
    its component, so a consumer can install one id to get the whole family.

    Membership is derived by scanning the component for .csproj files and
    excluding, by naming convention:
      *.Tests / *.Demo / *.Diagnostic   -> not shippable
      *.Windows / *.Linux / *.Android /
      *.WebAssembly / *.MediaFoundation  -> platform-native (opt-in
                                                  separately, keeps the meta
                                                  cross-platform)
      *.All                              -> the meta itself
    Re-run after adding or removing a library in one of these components.

    Each meta declares where it lives, relative to its component directory,
    because they are not all in the same place: Media and Input keep theirs at
    the component root, while UI's moved under src/Bundles when the component
    was restructured. The script previously assumed the component root for all
    three, so regenerating produced a duplicate at a stale path and left the
    real file untouched.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

# component -> meta description, tags, and where the meta project lives
# (relative to the component directory).
$metas = @{
    'Broiler.Media' = @{
        Desc = 'Meta-package that pulls in the full cross-platform Broiler.Media stack (core, audio, image, and video plus their managed implementations). Platform-native backends (e.g. MediaFoundation) are separate packages.'
        Tags = 'broiler;media;audio;image;video;meta'
        Dir  = 'Broiler.Media.All'
    }
    'Broiler.Input' = @{
        Desc = 'Meta-package that pulls in the cross-platform Broiler.Input device contracts (keyboard, mouse, pen, touch, text, camera, microphone). Platform backends (*.Windows, *.Linux, *.Android) are separate packages.'
        Tags = 'broiler;input;devices;meta'
        Dir  = 'Broiler.Input.All'
    }
    'Broiler.UI' = @{
        Desc = 'Meta-package that pulls in the complete Broiler retained-mode UI toolkit: every control contract and its Standard implementation.'
        Tags = 'broiler;ui;controls;widgets;meta'
        Dir  = 'src\Bundles\Broiler.UI.All'
    }
}

# Android and WebAssembly are excluded for the same reason as Windows and
# Linux: the meta is the cross-platform surface, and a platform backend belongs
# in a package a consumer opts into. Broiler.Input has five *.Android projects
# that this pattern previously missed, so regenerating would have added Android
# translation code to the package every consumer installs. WebAssembly matches
# nothing in these three components today and is listed so it cannot drift in
# the same way.
$excludeSuffix = '\.(Tests|Demo|Diagnostic|Windows|Linux|Android|WebAssembly|MediaFoundation|All)$'

<#
.SYNOPSIS
    Relative path from a directory to a file, both absolute.
.DESCRIPTION
    Windows PowerShell 5.1 has no Path.GetRelativePath, and the previous inline
    computation hard-coded a single '..\' because every meta was one level below
    its component. UI's is three levels below, so the number of steps up has to
    be derived rather than assumed.
#>
function Get-RelativePath {
    param(
        [Parameter(Mandatory)] [string] $FromDirectory,
        [Parameter(Mandatory)] [string] $ToPath
    )

    $fromParts = @($FromDirectory.TrimEnd('\', '/') -split '[\\/]')
    $toParts   = @($ToPath -split '[\\/]')

    $common = 0
    while ($common -lt $fromParts.Count -and $common -lt $toParts.Count -and
           $fromParts[$common] -eq $toParts[$common]) {
        $common++
    }

    $up = @()
    if ($fromParts.Count -gt $common) {
        $up = @('..') * ($fromParts.Count - $common)
    }

    $down = @()
    if ($toParts.Count -gt $common) {
        $down = $toParts[$common..($toParts.Count - 1)]
    }

    return (@($up + $down) -join '\')
}

foreach ($component in $metas.Keys | Sort-Object) {
    $componentDir = Join-Path $repoRoot $component
    $metaId   = "$component.All"
    $metaDir  = Join-Path $componentDir $metas[$component].Dir
    $metaProj = Join-Path $metaDir "$metaId.csproj"

    # Discover member projects.
    $members = Get-ChildItem -Path $componentDir -Recurse -Filter '*.csproj' |
        Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' } |
        Where-Object { [IO.Path]::GetFileNameWithoutExtension($_.Name) -notmatch $excludeSuffix } |
        Sort-Object FullName

    if (-not $members) { Write-Warning "no members found for $metaId"; continue }

    $refs = foreach ($m in $members) {
        $rel = Get-RelativePath -FromDirectory $metaDir -ToPath $m.FullName
        "    <ProjectReference Include=`"$rel`" />"
    }

    $csproj = @"
<Project Sdk="Microsoft.NET.Sdk">

  <!-- GENERATED by scripts/gen-metapackages.ps1 - do not edit by hand. -->

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <PackageId>$metaId</PackageId>
    <Description>$($metas[$component].Desc)</Description>
    <PackageTags>$($metas[$component].Tags)</PackageTags>

    <!-- Meta-package: ship dependencies only, no assembly. -->
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <SuppressDependenciesWhenPacking>false</SuppressDependenciesWhenPacking>
    <IsPackable>true</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <!-- NU5128: a dependency group with no matching lib folder is expected
         for a dependencies-only meta-package. -->
    <NoWarn>`$(NoWarn);NU5128</NoWarn>
  </PropertyGroup>

  <ItemGroup>
$($refs -join "`r`n")
  </ItemGroup>

</Project>
"@

    if (-not (Test-Path $metaDir)) {
        if ($PSCmdlet.ShouldProcess($metaDir, 'create meta dir')) {
            New-Item -ItemType Directory -Path $metaDir | Out-Null
        }
    }
    if ($PSCmdlet.ShouldProcess($metaProj, "write $metaId.csproj ($($members.Count) members)")) {
        Set-Content -Path $metaProj -Value $csproj -Encoding UTF8
    }
    Write-Host "$metaId <- $($members.Count) member projects"
}
