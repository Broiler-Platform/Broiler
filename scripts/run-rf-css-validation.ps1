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
    $ResultsDirectory = Join-Path $repoRoot "artifacts/rf-css-validation/$stamp"
}
elseif (-not [IO.Path]::IsPathRooted($ResultsDirectory)) {
    $ResultsDirectory = Join-Path $repoRoot $ResultsDirectory
}

New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

if (-not $NoBuild) {
    & dotnet build (Join-Path $repoRoot 'Broiler.slnx') --configuration $Configuration --nologo -m:1
    if ($LASTEXITCODE -ne 0) {
        throw "RF-CSS validation build failed with exit code $LASTEXITCODE."
    }

    # Some validation projects are intentionally kept outside the main solution
    # graph. Build every test assembly that this script can execute so --no-build
    # never reuses binaries from the retired Broiler.HTML.CSS assembly graph.
    $validationProjects = @(
        'Broiler.CSS/Broiler.CSS.Tests/Broiler.CSS.Tests.csproj',
        'Broiler.CSS/Broiler.CSS.Dom.Tests/Broiler.CSS.Dom.Tests.csproj',
        'src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj'
    )
    if ($IncludeVisual) {
        $validationProjects += 'src/Broiler.Wpt.Tests/Broiler.Wpt.Tests.csproj'
    }

    foreach ($project in $validationProjects) {
        & dotnet build (Join-Path $repoRoot $project) --configuration $Configuration --nologo -m:1
        if ($LASTEXITCODE -ne 0) {
            throw "RF-CSS validation project build failed for $project with exit code $LASTEXITCODE."
        }
    }

    if ($IncludePerformance) {
        & dotnet build (Join-Path $repoRoot 'src/Broiler.Engines.Baseline/Broiler.Engines.Baseline.csproj') --configuration Release --nologo -m:1
        if ($LASTEXITCODE -ne 0) {
            throw "RF-CSS performance harness build failed with exit code $LASTEXITCODE."
        }
    }
}

$groups = @(
    [pscustomobject]@{
        Name = 'css-kernel'
        Project = 'Broiler.CSS/Broiler.CSS.Tests/Broiler.CSS.Tests.csproj'
        Filter = $null
        AllowedFailures = @()
    },
    [pscustomobject]@{
        Name = 'css-dom'
        Project = 'Broiler.CSS/Broiler.CSS.Dom.Tests/Broiler.CSS.Dom.Tests.csproj'
        Filter = $null
        AllowedFailures = @()
    },
    [pscustomobject]@{
        Name = 'css-extraction'
        Project = 'src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj'
        Filter = 'FullyQualifiedName~CssExtraction'
        AllowedFailures = @()
    },
    [pscustomobject]@{
        Name = 'bridge-mutation'
        Project = 'src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj'
        Filter = 'FullyQualifiedName~DynamicStyle|FullyQualifiedName~SvgDomDynamicContent|FullyQualifiedName~SelectorsLevel4Specificity|FullyQualifiedName~StyleSheet_InsertRule_Is_Observed_By_GetComputedStyle'
        AllowedFailures = @()
    },
    [pscustomobject]@{
        Name = 'cli-css'
        Project = 'src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj'
        Filter = 'FullyQualifiedName~SelectorsAndCssom|FullyQualifiedName~CssRendering|FullyQualifiedName~CssImportantCascade|FullyQualifiedName~WptCssVariables|FullyQualifiedName~CssSelectorsPolish|FullyQualifiedName~Phase5SharedCascade'
        AllowedFailures = @(
            'Has_NthChild_Invalidation_Tracks_Removals',
            'Root_Matches_DocumentElement_Only',
            'Has_GeneralSibling_NestedNthChild_Invalidation_Tracks_Removals',
            'Lang_Matches_XmlLang_Ancestor',
            'Has_IsAndWhereWrappedSelectors_Invalidation_Tracks_Removals'
        )
    }
)

if ($IncludeVisual) {
    $groups += [pscustomobject]@{
        Name = 'acid3-css-layout'
        Project = 'src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj'
        Filter = 'FullyQualifiedName~Acid3CssComplianceTests|FullyQualifiedName~Acid3CascadeDebugTests|FullyQualifiedName~Acid3BorderLayoutTests|FullyQualifiedName~Acid3BarPositionTest'
        AllowedFailures = @(
            'Without_Important_Higher_Specificity_Red_Wins',
            'Border_Shorthand_Expands_Color_To_Individual_Sides'
        )
    }
    $groups += [pscustomobject]@{
        Name = 'wpt-anchor'
        Project = 'src/Broiler.Wpt.Tests/Broiler.Wpt.Tests.csproj'
        Filter = 'FullyQualifiedName~Anchor|FullyQualifiedName~PositionVisibility|FullyQualifiedName~Backdrop'
        AllowedFailures = @(
            'Wpt_PositionAreaAnchorPartiallyOutside_MatchesReference',
            'Wpt_AnchorPositionTopLayer001_MatchesReference',
            'Wpt_AnchorPositionTopLayer002_MatchesReference',
            'Wpt_AnchorPositionTopLayer003_MatchesReference',
            'Wpt_AnchorPositionTopLayer004_MatchesReference',
            'Wpt_AnchorPositionTopLayer005_MatchesReference',
            'Wpt_AnchorPositionTopLayer006_MatchesReference',
            'Wpt_PositionVisibilityRemoveAnchorsVisible_MatchesReference',
            'Wpt_PositionVisibilityAnchorsVisibleWithPosition_MatchesReference'
        )
    }
}

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
        Failed = $failed.Count
        Skipped = $skipped
        Unexpected = $unexpected.Count
        Trx = $trxPath
    })
}

$performanceStatus = 'Not run'
if ($IncludePerformance) {
    $performanceDir = Join-Path $ResultsDirectory 'performance'
    # Keep compilation outside the timed process. A compile immediately before
    # sampling made the raster gate noisy enough to produce false regressions.
    & dotnet run --no-build --project (Join-Path $repoRoot 'src/Broiler.Engines.Baseline') --configuration Release -- benchmarks --output-dir $performanceDir
    if ($LASTEXITCODE -ne 0) {
        $hasUnexpectedFailure = $true
        $performanceStatus = 'Regression'
    }
    else {
        $performanceStatus = 'Within budget'
    }
}

$summaryPath = Join-Path $ResultsDirectory 'summary.md'
$lines = @(
    '# RF-CSS validation summary',
    '',
    "- Date: $(Get-Date -Format o)",
    "- Commit: $(& git -C $repoRoot rev-parse HEAD)",
    "- Configuration: $Configuration",
    "- Visual gates: $IncludeVisual",
    "- Performance gate: $IncludePerformance",
    "- Performance result: $performanceStatus",
    '',
    '| Group | Passed | Allowed failures | Skipped | Unexpected |',
    '|---|---:|---:|---:|---:|'
)
foreach ($summary in $summaries) {
    $lines += "| $($summary.Group) | $($summary.Passed) | $($summary.Failed - $summary.Unexpected) | $($summary.Skipped) | $($summary.Unexpected) |"
}
$lines | Set-Content -LiteralPath $summaryPath -Encoding utf8

Write-Host "`nRF-CSS summary: $summaryPath"
if ($hasUnexpectedFailure) {
    exit 1
}
