[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Directory,
    [switch] $TestProducts
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($Directory)
$isWindows = [System.OperatingSystem]::IsWindows()
$launcherExeName = if ($isWindows) { 'Broiler.Launcher.Windows.exe' } else { 'Broiler.Launcher.Linux' }
$exe = Join-Path $root "launcher/$launcherExeName"
if (-not (Test-Path $exe)) {
    $found = Get-ChildItem (Join-Path $root 'launcher') -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -like 'Broiler.Launcher*' } | Select-Object -First 1
    if ($found) { $exe = $found.FullName }
}
if (-not $isWindows -and (Test-Path $exe)) {
    & chmod +x $exe 2>$null
}
$evidence = Join-Path $root 'evidence'
Copy-Item -LiteralPath (Join-Path $root 'release-assets/office-release.json') -Destination (Join-Path (Split-Path $exe) 'office-release.json')
New-Item -ItemType Directory -Force -Path $evidence | Out-Null

function Start-SmokeProcess {
    param(
        [Parameter(Mandatory)][string] $FilePath,
        [string[]] $ArgumentList = @(),
        [string] $WorkingDirectory,
        [string] $RedirectStandardOutput,
        [string] $RedirectStandardError,
        [switch] $Wait,
        [switch] $PassThru
    )
    $params = @{ FilePath = $FilePath }
    if ($ArgumentList.Count -gt 0) { $params['ArgumentList'] = $ArgumentList }
    if ($WorkingDirectory) { $params['WorkingDirectory'] = $WorkingDirectory }
    if ($RedirectStandardOutput) { $params['RedirectStandardOutput'] = $RedirectStandardOutput }
    if ($RedirectStandardError) { $params['RedirectStandardError'] = $RedirectStandardError }
    if ($Wait) { $params['Wait'] = $true }
    if ($PassThru) { $params['PassThru'] = $true }
    if ([System.OperatingSystem]::IsWindows()) { $params['WindowStyle'] = 'Hidden' }
    Start-Process @params
}

foreach ($argument in @('--office-package-info', '--office-self-test')) {
    $name = $argument.TrimStart('-')
    $stdout = Join-Path $evidence "$name.out"
    $stderr = Join-Path $evidence "$name.err"
    $process = Start-SmokeProcess -FilePath $exe -ArgumentList @($argument) -Wait -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    if ($process.ExitCode -ne 0) { throw "Native $argument failed: $(Get-Content $stderr -Raw)" }
}
$info = Get-Content (Join-Path $evidence 'office-package-info.out') -Raw | ConvertFrom-Json
if (!$info.demo -or !$info.bootstrapDemo -or !$info.nativeAot) { throw 'Expected an explicit NativeAOT online-demo launcher.' }
$lock = Get-Content (Join-Path $root 'selection.lock.json') -Raw | ConvertFrom-Json
if ($info.version.Split('+')[0] -ne $lock.suite) { throw 'Native launcher version differs from the selected demo version.' }
$process = Start-SmokeProcess -FilePath $exe -PassThru -WorkingDirectory (Split-Path $exe)
try {
    Start-Sleep -Seconds 3
    if ($process.HasExited) { throw "Native graphical launcher exited early: $($process.ExitCode)" }
} finally {
    if (!$process.HasExited) { $null = $process.CloseMainWindow(); if (!$process.WaitForExit(3000)) { $process.Kill(); $process.WaitForExit() } }
}
if ($TestProducts) {
    $process = Start-SmokeProcess -FilePath $exe -ArgumentList @('--office-test-bundle', (Join-Path $root 'release-assets')) -Wait -PassThru -RedirectStandardOutput (Join-Path $evidence 'native-products.out') -RedirectStandardError (Join-Path $evidence 'native-products.err')
    if ($process.ExitCode -ne 0) { throw "Native product installation tests failed: $(Get-Content (Join-Path $evidence 'native-products.err') -Raw)" }
    $lock = Get-Content (Join-Path $root 'selection.lock.json') -Raw | ConvertFrom-Json
    foreach ($component in $lock.components) {
        $folder = Join-Path $root "smoke-products/$($component.id)"
        $product = Join-Path $folder $component.entryPoint
        if (-not $isWindows -and (Test-Path $product)) {
            & chmod +x $product 2>$null
        }
        $process = Start-SmokeProcess -FilePath $product -PassThru -WorkingDirectory $folder
        try {
            Start-Sleep -Seconds 3
            if ($process.HasExited) { throw "$($component.id) exited early: $($process.ExitCode)" }
        } finally {
            if (!$process.HasExited) { $null = $process.CloseMainWindow(); if (!$process.WaitForExit(3000)) { $process.Kill(); $process.WaitForExit() } }
        }
    }
}
'Native CLI, isolated installation and process-start smoke checks passed. Human GUI acceptance remains required.' | Set-Content (Join-Path $evidence 'smoke-result.txt')
