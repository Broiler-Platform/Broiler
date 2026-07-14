[CmdletBinding()]
param(
    [string] $SolutionPath = (Join-Path $PSScriptRoot '..\Broiler.slnx')
)

$ErrorActionPreference = 'Stop'

$solutionFile = Get-Item -LiteralPath $SolutionPath
$repositoryRoot = $solutionFile.Directory.FullName

# These are recursive checkouts of repositories that already have a canonical, shallower
# checkout in this repository. Including both copies would create duplicate assembly identities.
$duplicateCheckoutPrefixes = @(
    'Broiler.CSS/Broiler.DOM/',
    'Broiler.HTML/Broiler.Graphics/',
    'Broiler.JS/Broiler.Regex/Broiler.Unicode/'
)

$projectExtensions = @('.csproj', '.fsproj', '.vbproj')
$filesystemProjects = Get-ChildItem -LiteralPath $repositoryRoot -Recurse -File |
    Where-Object {
        $_.Extension -in $projectExtensions -and
        $_.FullName -notmatch '[\\/](bin|obj|\.git)[\\/]'
    } |
    ForEach-Object {
        $_.FullName.Substring($repositoryRoot.Length).TrimStart('\', '/').Replace('\', '/')
    } |
    Where-Object {
        $path = $_
        -not ($duplicateCheckoutPrefixes | Where-Object { $path.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) })
    } |
    Sort-Object -Unique

[xml] $solution = Get-Content -LiteralPath $solutionFile.FullName -Raw
$solutionProjects = $solution.SelectNodes('//Project[@Path]') |
    ForEach-Object { $_.Path.Replace('\', '/') } |
    Sort-Object -Unique

$comparison = Compare-Object -ReferenceObject $solutionProjects -DifferenceObject $filesystemProjects
$missing = @($comparison | Where-Object SideIndicator -eq '=>' | ForEach-Object InputObject)
$stale = @($comparison | Where-Object SideIndicator -eq '<=' | ForEach-Object InputObject)

if ($missing.Count -gt 0) {
    Write-Error "Projects missing from $($solutionFile.Name):`n  $($missing -join "`n  ")" -ErrorAction Continue
}

if ($stale.Count -gt 0) {
    Write-Error "Stale projects in $($solutionFile.Name):`n  $($stale -join "`n  ")" -ErrorAction Continue
}

if ($missing.Count -gt 0 -or $stale.Count -gt 0) {
    exit 1
}

Write-Host "$($solutionFile.Name) contains all $($filesystemProjects.Count) canonical projects."
