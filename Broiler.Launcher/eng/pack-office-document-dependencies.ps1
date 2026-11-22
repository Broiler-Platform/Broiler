[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$feed = Join-Path $repositoryRoot 'artifacts/local-feed'
$version = '0.1.0-preview.52'

$projects = @(
    'Broiler.Documents/src/Broiler.Documents.Model/Broiler.Documents.Model.csproj'
    'Broiler.Documents/src/Broiler.Documents/Broiler.Documents.csproj'
    'Broiler.Documents/src/Broiler.Documents.Pagination/Broiler.Documents.Pagination.csproj'
    'Broiler.Documents.Codecs/src/Broiler.Documents.Csv/Broiler.Documents.Csv.csproj'
    'Broiler.Documents.Codecs/src/Broiler.Documents.Html/Broiler.Documents.Html.csproj'
    'Broiler.Documents.Codecs/src/Broiler.Documents.Ods/Broiler.Documents.Ods.csproj'
    'Broiler.Documents.Codecs/src/Broiler.Documents.Pdf/Broiler.Documents.Pdf.csproj'
    'Broiler.Documents.Codecs/src/Broiler.Documents.Xlsx/Broiler.Documents.Xlsx.csproj'
)

foreach ($project in $projects) {
    dotnet pack (Join-Path $repositoryRoot $project) -c Release -o $feed "-p:Version=$version"
    if ($LASTEXITCODE -ne 0) {
        throw "Packing $project failed (exit code $LASTEXITCODE)."
    }
}
