[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Directory,
    [switch] $TestProducts
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($Directory)
$exe = Join-Path $root 'launcher/Broiler.Launcher.Windows.exe'
$evidence = Join-Path $root 'evidence'
Copy-Item -LiteralPath (Join-Path $root 'release-assets/office-release.json') -Destination (Join-Path (Split-Path $exe) 'office-release.json')
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
foreach ($argument in @('--office-package-info', '--office-self-test')) {
    $name = $argument.TrimStart('-')
    $stdout = Join-Path $evidence "$name.out"
    $stderr = Join-Path $evidence "$name.err"
    $process = Start-Process -FilePath $exe -ArgumentList $argument -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    if ($process.ExitCode -ne 0) { throw "Native $argument failed: $(Get-Content $stderr -Raw)" }
}
$info = Get-Content (Join-Path $evidence 'office-package-info.out') -Raw | ConvertFrom-Json
if (!$info.demo -or !$info.bootstrapDemo -or !$info.nativeAot) { throw 'Expected an explicit NativeAOT online-demo launcher.' }
$lock = Get-Content (Join-Path $root 'selection.lock.json') -Raw | ConvertFrom-Json
if ($info.version.Split('+')[0] -ne $lock.suite) { throw 'Native launcher version differs from the selected demo version.' }
$process = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden -WorkingDirectory (Split-Path $exe)
try {
    Start-Sleep -Seconds 3
    if ($process.HasExited) { throw "Native graphical launcher exited early: $($process.ExitCode)" }
} finally {
    if (!$process.HasExited) { $null = $process.CloseMainWindow(); if (!$process.WaitForExit(3000)) { $process.Kill(); $process.WaitForExit() } }
}
if ($TestProducts) {
    $process = Start-Process -FilePath $exe -ArgumentList @('--office-test-bundle', ('"' + (Join-Path $root 'release-assets') + '"')) -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $evidence 'native-products.out') -RedirectStandardError (Join-Path $evidence 'native-products.err')
    if ($process.ExitCode -ne 0) { throw "Native product installation tests failed: $(Get-Content (Join-Path $evidence 'native-products.err') -Raw)" }
    $lock = Get-Content (Join-Path $root 'selection.lock.json') -Raw | ConvertFrom-Json
    foreach ($component in $lock.components) {
        $folder = Join-Path $root "smoke-products/$($component.id)"
        $product = Join-Path $folder $component.entryPoint
        $process = Start-Process -FilePath $product -PassThru -WindowStyle Hidden -WorkingDirectory $folder
        try {
            Start-Sleep -Seconds 3
            if ($process.HasExited) { throw "$($component.id) exited early: $($process.ExitCode)" }
        } finally {
            if (!$process.HasExited) { $null = $process.CloseMainWindow(); if (!$process.WaitForExit(3000)) { $process.Kill(); $process.WaitForExit() } }
        }
    }
}
'Native CLI, isolated installation and process-start smoke checks passed. Human GUI acceptance remains required.' | Set-Content (Join-Path $evidence 'smoke-result.txt')
