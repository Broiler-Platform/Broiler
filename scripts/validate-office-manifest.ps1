[CmdletBinding()]
param([Parameter(Mandatory = $true)][string] $Bundle)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$bundlePath = (Resolve-Path -LiteralPath $Bundle).Path
$core = Join-Path $bundlePath 'launcher/Broiler.Launcher.Core.dll'
$scratch = Join-Path ([IO.Path]::GetTempPath()) ("broiler-office-contract-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $scratch | Out-Null
$escapedCore = [Security.SecurityElement]::Escape($core)
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup><Reference Include="Broiler.Launcher.Core"><HintPath>$escapedCore</HintPath></Reference></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $scratch 'Contract.csproj') -Encoding utf8NoBOM
'<configuration><packageSources><clear/></packageSources></configuration>' | Set-Content -LiteralPath (Join-Path $scratch 'NuGet.config')
@'
using Broiler.Launcher.Installation;
if (!OfficeBuild.IsDemo) throw new Exception("Expected the demo build boundary.");
var source = new BundledOfficePackageSource(args[0], OfficeReleaseVerifier.CreateDefault());
foreach (var id in OfficeCatalog.ProductIds) {
    var package = source.Read(id);
    if (!package.Release.Demo) throw new Exception("Missing demo marker.");
    Console.WriteLine($"Packaged launcher validated component: {id} {package.Component.Version}");
}
'@ | Set-Content -LiteralPath (Join-Path $scratch 'Program.cs') -Encoding utf8NoBOM
& dotnet run --project (Join-Path $scratch 'Contract.csproj') -- $bundlePath
if ($LASTEXITCODE -ne 0) { throw "Packaged launcher manifest validation failed; diagnostics at $scratch" }
