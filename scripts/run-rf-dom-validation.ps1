[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [switch]$IncludeVisual,
    [switch]$IncludePerformance,
    [string]$ResultsDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $ResultsDirectory) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $ResultsDirectory = Join-Path $repoRoot "artifacts/rf-dom-validation/$stamp"
}
elseif (-not [IO.Path]::IsPathRooted($ResultsDirectory)) {
    $ResultsDirectory = Join-Path $repoRoot $ResultsDirectory
}

New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

if (-not $NoBuild) {
    & dotnet build (Join-Path $repoRoot 'Broiler.slnx') --configuration $Configuration --nologo -m:1
    if ($LASTEXITCODE -ne 0) {
        throw "RF-DOM solution build failed with exit code $LASTEXITCODE."
    }

    $standaloneProjects = @(
        'Broiler.DOM/Broiler.Dom.Tests/Broiler.Dom.Tests.csproj',
        'Broiler.DOM/Broiler.Dom.Html.Tests/Broiler.Dom.Html.Tests.csproj',
        'src/Broiler.Wpt.Tests/Broiler.Wpt.Tests.csproj'
    )
    if ($IncludeVisual) {
        $standaloneProjects += 'Broiler.Layout/Broiler.Layout.Tests/Broiler.Layout.Tests.csproj'
    }

    foreach ($project in $standaloneProjects) {
        & dotnet build (Join-Path $repoRoot $project) --configuration $Configuration --nologo -m:1
        if ($LASTEXITCODE -ne 0) {
            throw "RF-DOM validation project build failed for $project with exit code $LASTEXITCODE."
        }
    }

    if ($IncludePerformance) {
        & dotnet build (Join-Path $repoRoot 'src/Broiler.Engines.Baseline/Broiler.Engines.Baseline.csproj') --configuration Release --nologo -m:1
        if ($LASTEXITCODE -ne 0) {
            throw "RF-DOM performance harness build failed with exit code $LASTEXITCODE."
        }
    }
}

$groups = @(
    [pscustomobject]@{
        Name = 'dom-kernel'
        Project = 'Broiler.DOM/Broiler.Dom.Tests/Broiler.Dom.Tests.csproj'
        Filter = $null
        MinimumTests = 19
        AllowedFailures = @()
    },
    [pscustomobject]@{
        Name = 'dom-html'
        Project = 'Broiler.DOM/Broiler.Dom.Html.Tests/Broiler.Dom.Html.Tests.csproj'
        Filter = $null
        MinimumTests = 4
        AllowedFailures = @()
    },
    [pscustomobject]@{
        Name = 'dom-boundary'
        Project = 'src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj'
        Filter = 'FullyQualifiedName~HtmlBridgeBoundaryGuardTests|FullyQualifiedName~DomExtractionPhaseZeroTests'
        MinimumTests = 20
        AllowedFailures = @()
    },
    [pscustomobject]@{
        Name = 'bridge-dom-behavior'
        Project = 'src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj'
        Filter = 'FullyQualifiedName~DomEvents|FullyQualifiedName~MutationObserver|FullyQualifiedName~HtmlDomInterfacesTests|FullyQualifiedName~SvgDomAndCrossDocTests'
        MinimumTests = 190
        AllowedFailures = @()
    },
    [pscustomobject]@{
        Name = 'range-display-contents'
        Project = 'src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj'
        Filter = 'FullyQualifiedName=Broiler.Cli.Tests.DomTraversalAndRangeTests.Range_GetBoundingClientRect_Includes_DisplayContents_Descendants'
        MinimumTests = 1
        AllowedFailures = @()
    },
    [pscustomobject]@{
        Name = 'acid3-dom-range'
        Project = 'src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj'
        Filter = 'FullyQualifiedName~Acid3RegressionTests|FullyQualifiedName~Acid3Phase4RangeTests'
        MinimumTests = 27
        AllowedFailures = @(
            'Acid3_Test0_WhiteSpace_LastChild_After_Removal',
            'CssCascade_After_Dom_Mutation_RemoveChild',
            'GetComputedStyle_LastChild_Recomputes_After_RemoveChild'
        )
    },
    [pscustomobject]@{
        Name = 'wpt-dom'
        Project = 'src/Broiler.Wpt.Tests/Broiler.Wpt.Tests.csproj'
        Filter = 'FullyQualifiedName~Wpt_CssomView_Iframe|FullyQualifiedName~Wpt_CssomView_CreateHtmlDocument'
        MinimumTests = 5
        AllowedFailures = @()
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

    if ($results.Count -lt $group.MinimumTests) {
        $hasUnexpectedFailure = $true
        Write-Host ("Test discovery loss in {0}: expected at least {1}, found {2}." -f $group.Name, $group.MinimumTests, $results.Count) -ForegroundColor Red
    }
    if ($skipped -gt 0) {
        $hasUnexpectedFailure = $true
        Write-Host ("Unexpected skipped tests in {0}: {1}." -f $group.Name, $skipped) -ForegroundColor Red
    }
    if ($testExitCode -ne 0 -and $failed.Count -eq 0) {
        $hasUnexpectedFailure = $true
        Write-Host ("Test host failed for {0} with exit {1} but reported no failed test." -f $group.Name, $testExitCode) -ForegroundColor Red
    }
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

$visualStatus = 'Not run'
if ($IncludeVisual) {
    Write-Host "`n=== renderer-visual-gate ==="
    $visualDirectory = Join-Path $ResultsDirectory 'renderer-visual'
    & (Join-Path $repoRoot 'scripts/run-rf-layout-validation.ps1') `
        -Configuration $Configuration `
        -NoBuild `
        -ResultsDirectory $visualDirectory
    if ($LASTEXITCODE -ne 0) {
        $hasUnexpectedFailure = $true
        $visualStatus = 'Regression'
    }
    else {
        $visualStatus = 'Within baseline'
    }
}

$performanceStatus = 'Not run'
$performanceRows = @()
$globalPerformanceExit = $null
if ($IncludePerformance) {
    Write-Host "`n=== dom-performance ==="
    $performanceDirectory = Join-Path $ResultsDirectory 'performance'
    $baselinePath = Join-Path $repoRoot 'tests/m0-baseline/performance/engine-benchmark-baseline.json'
    $savedErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $performanceOutput = & dotnet run --no-build --project (Join-Path $repoRoot 'src/Broiler.Engines.Baseline') --configuration Release -- `
        benchmarks `
        --output-dir $performanceDirectory `
        --baseline $baselinePath `
        --budget-percent 2 2>&1
    $globalPerformanceExit = $LASTEXITCODE
    $ErrorActionPreference = $savedErrorActionPreference
    $performanceOutput | Select-Object -Last 20 | Write-Host

    $currentPath = Join-Path $performanceDirectory 'engine-benchmark-baseline.json'
    if (-not (Test-Path -LiteralPath $currentPath)) {
        $hasUnexpectedFailure = $true
        $performanceStatus = 'No report'
    }
    else {
        $baselineMetrics = (Get-Content -Raw -LiteralPath $baselinePath | ConvertFrom-Json).results
        $currentMetrics = (Get-Content -Raw -LiteralPath $currentPath | ConvertFrom-Json).results
        $domMetricGates = @(
            [pscustomobject]@{ Name = 'bridge.mutation'; Statistic = 'mean'; BudgetPercent = 2.0 },
            # Fresh-context serialization has a single JIT/startup outlier in
            # both the baseline and current sample set; median is the stable
            # representative statistic for this formerly non-gated metric.
            # Its 10% closeout budget is 0.194 ms at the recorded baseline.
            [pscustomobject]@{ Name = 'bridge.serialize'; Statistic = 'median'; BudgetPercent = 10.0 }
        )
        $performanceRegression = $false

        foreach ($metricGate in $domMetricGates) {
            $baselineMetric = $baselineMetrics | Where-Object { $_.name -eq $metricGate.Name }
            $currentMetric = $currentMetrics | Where-Object { $_.name -eq $metricGate.Name }
            $baselineValue = [double]$baselineMetric.($metricGate.Statistic)
            $currentValue = [double]$currentMetric.($metricGate.Statistic)
            $limit = $baselineValue * (1.0 + ([double]$metricGate.BudgetPercent / 100.0))
            $withinBudget = $currentValue -le $limit
            $performanceRegression = $performanceRegression -or -not $withinBudget
            $performanceRows += [pscustomobject]@{
                Metric = "$($metricGate.Name) ($($metricGate.Statistic))"
                Baseline = $baselineValue
                Current = $currentValue
                Unit = [string]$currentMetric.unit
                Gate = if ($withinBudget) { "Within $($metricGate.BudgetPercent)%" } else { 'Regression' }
            }
        }

        $serializedMetric = $currentMetrics | Where-Object { $_.name -eq 'bridge.render-handoff' }
        $typedMetric = $currentMetrics | Where-Object { $_.name -eq 'bridge.typed-render-handoff' }
        $typedWithinBudget = [double]$typedMetric.mean -le ([double]$serializedMetric.mean * 1.02)
        $performanceRegression = $performanceRegression -or -not $typedWithinBudget
        $performanceRows += [pscustomobject]@{
            Metric = 'typed-vs-serialized-handoff'
            Baseline = [double]$serializedMetric.mean
            Current = [double]$typedMetric.mean
            Unit = [string]$typedMetric.unit
            Gate = if ($typedWithinBudget) { 'Typed no more than 2% slower' } else { 'Regression' }
        }

        if ($performanceRegression) {
            $hasUnexpectedFailure = $true
            $performanceStatus = 'Regression'
        }
        else {
            $performanceStatus = 'DOM metrics within budget'
        }
    }
}

$summaryPath = Join-Path $ResultsDirectory 'summary.md'
$lines = @(
    '# RF-DOM validation summary',
    '',
    "- Date: $(Get-Date -Format o)",
    "- Commit: $(& git -C $repoRoot rev-parse HEAD)",
    "- Configuration: $Configuration",
    "- Renderer visual gate: $visualStatus",
    "- DOM performance gate: $performanceStatus",
    "- Full benchmark harness exit: $(if ($null -eq $globalPerformanceExit) { 'Not run' } else { $globalPerformanceExit }) (non-DOM regressions are reported by the harness but do not redefine the DOM-owned gate)",
    '',
    '| Group | Passed | Accepted failures | Skipped | Unexpected |',
    '|---|---:|---:|---:|---:|'
)
foreach ($summary in $summaries) {
    $lines += "| $($summary.Group) | $($summary.Passed) | $($summary.Accepted) | $($summary.Skipped) | $($summary.Unexpected) |"
}

if ($performanceRows.Count -gt 0) {
    $lines += @(
        '',
        '| DOM performance metric | Comparison/baseline | Current | Unit | Gate |',
        '|---|---:|---:|---|---|'
    )
    foreach ($row in $performanceRows) {
        $lines += "| $($row.Metric) | $($row.Baseline.ToString('F3', [Globalization.CultureInfo]::InvariantCulture)) | $($row.Current.ToString('F3', [Globalization.CultureInfo]::InvariantCulture)) | $($row.Unit) | $($row.Gate) |"
    }
}

$lines | Set-Content -LiteralPath $summaryPath -Encoding utf8
Write-Host "`nRF-DOM summary: $summaryPath"
if ($hasUnexpectedFailure) {
    exit 1
}
