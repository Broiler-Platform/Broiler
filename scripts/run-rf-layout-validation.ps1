[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [string]$ResultsDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $ResultsDirectory) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $ResultsDirectory = Join-Path $repoRoot "artifacts/rf-layout-validation/$stamp"
}
elseif (-not [IO.Path]::IsPathRooted($ResultsDirectory)) {
    $ResultsDirectory = Join-Path $repoRoot $ResultsDirectory
}

New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

if (-not $NoBuild) {
    & dotnet build (Join-Path $repoRoot 'Broiler.slnx') --configuration $Configuration --nologo -m:1
    if ($LASTEXITCODE -ne 0) {
        throw "RF-LAYOUT solution build failed with exit code $LASTEXITCODE."
    }

    # The layout test project intentionally sits beside the extracted component
    # and is not part of the root solution graph.
    & dotnet build (Join-Path $repoRoot 'Broiler.Layout/Broiler.Layout.Tests/Broiler.Layout.Tests.csproj') --configuration $Configuration --nologo -m:1
    if ($LASTEXITCODE -ne 0) {
        throw "RF-LAYOUT test build failed with exit code $LASTEXITCODE."
    }
}

$groups = @(
    [pscustomobject]@{
        Name = 'layout-kernel'
        Project = 'Broiler.Layout/Broiler.Layout.Tests/Broiler.Layout.Tests.csproj'
        Filter = $null
        AllowedFailures = @()
    },
    [pscustomobject]@{
        Name = 'diagnostic-snapshot'
        Project = 'src/Broiler.DevConsole.Tests/Broiler.DevConsole.Tests.csproj'
        Filter = 'FullyQualifiedName~ConsoleServiceTests.BuildBoxTree|FullyQualifiedName~ConsoleServiceTests.GetComputedStyles|FullyQualifiedName~ConsoleServiceTests.GetBoxModel'
        AllowedFailures = @()
    },
    [pscustomobject]@{
        Name = 'acid2-layout'
        Project = 'src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj'
        Filter = 'FullyQualifiedName~Acid2ImageComparisonTests'
        AllowedFailures = @()
    },
    [pscustomobject]@{
        Name = 'acid3-layout'
        Project = 'src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj'
        Filter = 'FullyQualifiedName~Acid3CssComplianceTests|FullyQualifiedName~Acid3CascadeDebugTests|FullyQualifiedName~Acid3BorderLayoutTests|FullyQualifiedName~Acid3BarPositionTest'
        AllowedFailures = @(
            'Without_Important_Higher_Specificity_Red_Wins',
            'Border_Shorthand_Expands_Color_To_Individual_Sides'
        )
    }
)

$summaries = [Collections.Generic.List[object]]::new()
$hasUnexpectedFailure = $false

foreach ($group in $groups) {
    Write-Host "`n=== $($group.Name) ==="
    $trxName = "$($group.Name).trx"
    $args = @(
        'test', (Join-Path $repoRoot $group.Project),
        '--configuration', $Configuration,
        '--no-build', '--no-restore', '--nologo',
        '--results-directory', $ResultsDirectory,
        '--logger', "trx;LogFileName=$trxName"
    )
    if ($group.Filter) {
        $args += @('--filter', $group.Filter)
    }

    & dotnet @args
    $testExitCode = $LASTEXITCODE
    $trxPath = Join-Path $ResultsDirectory $trxName
    if (-not (Test-Path -LiteralPath $trxPath)) {
        Write-Host "$($group.Name) produced no TRX result (exit $testExitCode)." -ForegroundColor Red
        $hasUnexpectedFailure = $true
        continue
    }

    [xml]$trx = Get-Content -Raw -LiteralPath $trxPath
    $results = @($trx.TestRun.Results.UnitTestResult)
    $failed = @($results | Where-Object { $_.outcome -eq 'Failed' })
    $unexpected = @($failed | Where-Object {
        $failureName = [string]$_.testName
        -not ($group.AllowedFailures | Where-Object {
            $failureName -eq $_ -or $failureName.EndsWith(".$_")
        })
    })
    $passed = @($results | Where-Object { $_.outcome -eq 'Passed' }).Count
    $skipped = @($results | Where-Object { $_.outcome -eq 'NotExecuted' }).Count

    if ($unexpected.Count -gt 0) {
        $hasUnexpectedFailure = $true
        Write-Host ("Unexpected failures in {0}: {1}" -f $group.Name, (($unexpected.testName | Sort-Object) -join ', ')) -ForegroundColor Red
    }

    $summaries.Add([pscustomobject]@{
        Group = $group.Name
        Passed = $passed
        Accepted = $failed.Count - $unexpected.Count
        Skipped = $skipped
        Unexpected = $unexpected.Count
    })
}

Write-Host "`n=== wpt-layout-curated ==="
$wptRoot = Join-Path $repoRoot 'tests/wpt'
$wptJsonPath = Join-Path $ResultsDirectory 'wpt-results.json'
$wptMarkdownPath = Join-Path $ResultsDirectory 'wpt-summary.md'
$savedErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$wptOutput = & dotnet run --no-build --project (Join-Path $repoRoot 'src/Broiler.Wpt/Broiler.Wpt.csproj') --configuration $Configuration -- `
    --wpt-dir $wptRoot `
    --reference-dir (Join-Path $wptRoot 'references') `
    --json-output $wptJsonPath `
    --markdown-output $wptMarkdownPath 2>&1
$wptExitCode = $LASTEXITCODE
$ErrorActionPreference = $savedErrorActionPreference
$wptOutput | Select-Object -Last 25 | Write-Host

if (-not (Test-Path -LiteralPath $wptJsonPath)) {
    Write-Host "WPT runner produced no JSON result (exit $wptExitCode)." -ForegroundColor Red
    $hasUnexpectedFailure = $true
}
else {
    $baselinePath = Join-Path $repoRoot 'tests/wpt-baseline/rf-layout-curated.json'
    $baseline = Get-Content -Raw -LiteralPath $baselinePath | ConvertFrom-Json
    $wptReport = Get-Content -Raw -LiteralPath $wptJsonPath | ConvertFrom-Json
    $wptResults = @($wptReport.results)
    $observedPaths = @($wptResults | ForEach-Object { [string]$_.relativeTestPath })
    $allowedFailures = @($baseline.allowedFailures)
    $allowedSkips = @($baseline.allowedSkips)

    $wptFailures = @($wptResults | Where-Object { -not $_.passed -and -not $_.skipped })
    $wptSkips = @($wptResults | Where-Object { $_.skipped })
    $unexpectedFailures = @($wptFailures | Where-Object { $_.relativeTestPath -notin $allowedFailures })
    $unexpectedSkips = @($wptSkips | Where-Object { $_.relativeTestPath -notin $allowedSkips })
    $missingBaselineTests = @($allowedFailures + $allowedSkips | Where-Object { $_ -notin $observedPaths })
    $discoveryLoss = $wptResults.Count -lt [int]$baseline.expectedTotal

    if ($unexpectedFailures.Count -gt 0 -or $unexpectedSkips.Count -gt 0 -or $missingBaselineTests.Count -gt 0 -or $discoveryLoss) {
        $hasUnexpectedFailure = $true
        if ($unexpectedFailures.Count -gt 0) {
            Write-Host ("Unexpected WPT failures: {0}" -f (($unexpectedFailures.relativeTestPath | Sort-Object) -join ', ')) -ForegroundColor Red
        }
        if ($unexpectedSkips.Count -gt 0) {
            Write-Host ("Unexpected WPT skips: {0}" -f (($unexpectedSkips.relativeTestPath | Sort-Object) -join ', ')) -ForegroundColor Red
        }
        if ($missingBaselineTests.Count -gt 0) {
            Write-Host ("Baseline WPTs were not discovered: {0}" -f (($missingBaselineTests | Sort-Object) -join ', ')) -ForegroundColor Red
        }
        if ($discoveryLoss) {
            Write-Host "WPT discovery fell from $($baseline.expectedTotal) to $($wptResults.Count) tests." -ForegroundColor Red
        }
    }

    $summaries.Add([pscustomobject]@{
        Group = 'wpt-layout-curated'
        Passed = @($wptResults | Where-Object { $_.passed }).Count
        Accepted = $wptFailures.Count - $unexpectedFailures.Count
        Skipped = $wptSkips.Count
        Unexpected = $unexpectedFailures.Count + $unexpectedSkips.Count + $missingBaselineTests.Count + [int]$discoveryLoss
    })
}

$summaryPath = Join-Path $ResultsDirectory 'summary.md'
$lines = @(
    '# RF-LAYOUT validation summary',
    '',
    "- Date: $(Get-Date -Format o)",
    "- Commit: $(& git -C $repoRoot rev-parse HEAD)",
    "- Configuration: $Configuration",
    '- WPT subset: committed in-tree `tests/wpt` corpus with `tests/wpt-baseline/rf-layout-curated.json`',
    '',
    '| Group | Passed | Accepted failures | Skipped | Unexpected |',
    '|---|---:|---:|---:|---:|'
)
foreach ($summary in $summaries) {
    $lines += "| $($summary.Group) | $($summary.Passed) | $($summary.Accepted) | $($summary.Skipped) | $($summary.Unexpected) |"
}
$lines | Set-Content -LiteralPath $summaryPath -Encoding utf8

Write-Host "`nRF-LAYOUT summary: $summaryPath"
if ($hasUnexpectedFailure) {
    exit 1
}
