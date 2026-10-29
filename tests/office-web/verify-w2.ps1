[CmdletBinding()]
param([switch] $LocalPackages, [string] $HostingVersion, [string] $GraphicsVersion)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $PSScriptRoot
try {
    & ./publish-w2.ps1 @PSBoundParameters
    & ./publish.ps1
    & dotnet test (Join-Path $repo 'Broiler.Launcher/tests/Broiler.Launcher.Tests/Broiler.Launcher.Tests.csproj') -c Release -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Launcher tests failed.' }
    & dotnet build (Join-Path $repo 'Broiler.Launcher/src/Broiler.Launcher.Windows/Broiler.Launcher.Windows.csproj') -c Release -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Windows launcher build failed.' }
    & npm run test:launcher
    if ($LASTEXITCODE -ne 0) { throw 'Launcher browser integration failed.' }
    & npm test
    if ($LASTEXITCODE -ne 0) { throw 'Editor browser regression failed.' }
} finally { Pop-Location }
