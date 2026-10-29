[CmdletBinding()]
param([switch] $LocalPackages, [string] $HostingVersion, [string] $GraphicsVersion)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $repo 'artifacts/web-w2'
function Invoke-Checked([string] $Executable, [string[]] $Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE" }
}
Push-Location $PSScriptRoot
try {
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $properties = @()
    if ($LocalPackages) {
        $version = '0.1.0-w2check.' + [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
        $HostingVersion = $GraphicsVersion = $version
        $feed = Join-Path $output ('feed-' + $version)
        foreach ($project in @(
            'Broiler.Graphics/src/Broiler.Graphics/Broiler.Graphics.csproj',
            'Broiler.Graphics/src/Broiler.Graphics.WebAssembly/Broiler.Graphics.WebAssembly.csproj',
            'Broiler.Hosting/src/Broiler.Hosting.Contracts/Broiler.Hosting.Contracts.csproj',
            'Broiler.Hosting/src/Broiler.Hosting.WebAssembly/Broiler.Hosting.WebAssembly.csproj'
        )) { Invoke-Checked dotnet @('pack', (Join-Path $repo $project), '-c', 'Release', '-m:1', "-p:Version=$version", "-p:PackageVersion=$version", '-o', $feed) }
        $escapedFeed = [Security.SecurityElement]::Escape($feed)
        $config = Join-Path $feed 'NuGet.config'
        @"
<configuration><packageSources><clear/><add key="local-w2" value="$escapedFeed"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources><packageSourceMapping>
<packageSource key="local-w2"><package pattern="Broiler.Graphics"/><package pattern="Broiler.Graphics.WebAssembly"/><package pattern="Broiler.Hosting.Contracts"/><package pattern="Broiler.Hosting.WebAssembly"/></packageSource>
<packageSource key="nuget.org"><package pattern="*"/></packageSource></packageSourceMapping></configuration>
"@ | Set-Content -LiteralPath $config -Encoding utf8
        $properties += "-p:RestoreConfigFile=$config", "-p:RestorePackagesPath=$(Join-Path $output 'nuget-cache')"
    } else {
        if (!$HostingVersion -or !$GraphicsVersion) { throw 'Supply released -HostingVersion and -GraphicsVersion, or explicitly use -LocalPackages for pre-release testing.' }
        foreach ($package in @(
            @{ Id='broiler.hosting.contracts'; Version=$HostingVersion },
            @{ Id='broiler.hosting.webassembly'; Version=$HostingVersion },
            @{ Id='broiler.graphics'; Version=$GraphicsVersion },
            @{ Id='broiler.graphics.webassembly'; Version=$GraphicsVersion }
        )) {
            $available = Invoke-RestMethod ('https://api.nuget.org/v3-flatcontainer/' + $package.Id + '/index.json')
            if ($package.Version -notin $available.versions) { throw "Package version is not published: $($package.Id) $($package.Version)" }
        }
    }
    $properties += "-p:BroilerWebHostingVersion=$HostingVersion", "-p:BroilerWebGraphicsVersion=$GraphicsVersion"
    $project = Join-Path $repo 'Broiler.Launcher/src/Broiler.Launcher.WebAssembly/Broiler.Launcher.WebAssembly.csproj'
    Invoke-Checked dotnet (@('publish', $project, '-c', 'Release', '-m:1', '-p:RunAOTCompilation=false', '-o', (Join-Path $output 'launcher')) + $properties)
    $webRoot = Join-Path $output 'launcher/wwwroot'
    $manifestPath = Join-Path $webRoot 'broiler.graphics.replay.json'
    if (!(Test-Path -LiteralPath $manifestPath) -or !(Test-Path -LiteralPath (Join-Path $webRoot 'broiler.hosting.webassembly.js'))) {
        throw 'The selected packages do not supply the W1 browser assets. Publish and select the W1 package generation first.'
    }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $digest = (Get-FileHash -LiteralPath (Join-Path $webRoot 'broiler.graphics.webassembly.js') -Algorithm SHA256).Hash
    if ($manifest.protocol -ne 'broiler.canvas/1' -or $manifest.packageVersion -ne $GraphicsVersion -or $manifest.sha256 -ne $digest) {
        throw 'Published renderer/replay manifest does not match the selected Graphics package.'
    }
    Copy-Item -LiteralPath (Join-Path (Split-Path $project) 'obj/project.assets.json') -Destination (Join-Path $output 'launcher-project.assets.json')
    @{ localVerification=[bool]$LocalPackages; hostingVersion=$HostingVersion; graphicsVersion=$GraphicsVersion; publishedAt=[DateTimeOffset]::UtcNow.ToString('o') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build-info.json') -Encoding utf8
} finally { Pop-Location }
