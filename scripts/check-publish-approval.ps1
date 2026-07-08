#requires -Version 5.1
<#
.SYNOPSIS
    Governance gate for PUBLIC NuGet publishing. Exits non-zero unless every
    gated component's human-review record carries an APPROVED status.

.DESCRIPTION
    Per the repo policy (README "Human review status"), a component is release-
    facing only when its HUMAN_REVIEW.md names a reviewer/commit/decision and the
    decision is an approval. Each record starts with a blockquote status line, e.g.

        > **Status: APPROVED FOR FIRST PREVIEW.**
        > **Status: PENDING HUMAN REVIEW - ...**

    This gate reads that status line and classifies APPROVED (incl. "approved with
    conditions") as a pass; PENDING / NOT APPROVED / SUMMARY-ONLY as a block. The
    CI workflow runs it before pushing to nuget.org. GitHub Packages (internal
    preview feed) does not require it. As of writing Broiler.DOM and Broiler.JS are
    PENDING, so this correctly blocks the public feed.

    Override for emergencies only: BROILER_PUBLISH_FORCE=1 (logged).
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

# Component review records that gate the packages published from this repo.
$records = @(
    'Broiler.DOM/HUMAN_REVIEW.md',
    'Broiler.CSS/HUMAN_REVIEW.md',
    'Broiler.Layout/HUMAN_REVIEW.md',
    'Broiler.Graphics/HUMAN_REVIEW.md',
    'Broiler.HTML/HUMAN_REVIEW.md',
    'Broiler.JS/HUMAN_REVIEW.md'
)

function Get-ReviewStatus {
    param([string] $Path)
    foreach ($line in (Get-Content -Path $Path)) {
        if ($line -imatch 'Status:\s*(.+)') {
            $s = $matches[1] -replace '\*+', '' -replace '>', ''
            $s = $s.Trim()
            if ($s -imatch 'not approved' -or $s -imatch 'pending' -or $s -imatch 'summary only') {
                return @{ Approved = $false; Text = $s }
            }
            if ($s -imatch 'approved') { return @{ Approved = $true; Text = $s } }
            return @{ Approved = $false; Text = $s }
        }
    }
    return @{ Approved = $false; Text = '(no Status line found)' }
}

$results = foreach ($rel in $records) {
    $path = Join-Path $repoRoot $rel
    if (-not (Test-Path $path)) { [pscustomobject]@{ Record = $rel; Approved = $false; Status = '(missing)' }; continue }
    $st = Get-ReviewStatus $path
    [pscustomobject]@{ Record = $rel; Approved = $st.Approved; Status = $st.Text }
}

Write-Host "Publish-approval gate ($($records.Count) component records):"
foreach ($r in $results) {
    $mark = if ($r.Approved) { 'PASS ' } else { 'BLOCK' }
    Write-Host ("  [{0}] {1,-32} {2}" -f $mark, $r.Record, $r.Status)
}

$blocked = @($results | Where-Object { -not $_.Approved })
if ($blocked.Count -eq 0) {
    Write-Host "`nPASS - all gated components are approved for release."
    exit 0
}

if ($env:BROILER_PUBLISH_FORCE -eq '1') {
    Write-Warning "`nBROILER_PUBLISH_FORCE=1 - bypassing the gate despite $($blocked.Count) blocked record(s)."
    exit 0
}

Write-Error "`nPublic NuGet publish is blocked: $($blocked.Count) component(s) not approved. Publish to the GitHub Packages preview feed instead, or sign off the records."
exit 1
