param(
    [Parameter(Mandatory = $true, Position = 0)]
    [int]$Milestone,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AdditionalArgs
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptDir)
$projectPath = Join-Path $repoRoot 'src/Broiler.Engines.Baseline/Broiler.Engines.Baseline.csproj'

$arguments = @(
    'run',
    '--project', $projectPath,
    '--configuration', 'Release',
    '--',
    'chromium-reference',
    '--milestone', $Milestone
) + $AdditionalArgs

dotnet @arguments
