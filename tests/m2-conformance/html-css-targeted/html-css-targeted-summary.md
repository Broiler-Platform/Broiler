# HTML/CSS targeted conformance

This file is a focused HTML/CSS conformance signal. It keeps the targeted gate
small and reproducible while broader standards and test-infrastructure work is
tracked in the
[root roadmap](../../../docs/ROADMAP.md#standards-and-test-infrastructure).

## Current published result

| Metric | Value |
|---|---:|
| Total tests | 70 |
| Executed | 70 |
| Passed | 70 |
| Failed | 0 |
| Skipped / not executed | 0 |
| Pass rate | 100.0% |

## Included suites

- `Broiler.Cli.Tests.WptCssVariablesTests`
- `Broiler.Cli.Tests.WptFontAndSelectorTests`
- `Broiler.Cli.Tests.GoogleSearchComplianceTests`
- `Broiler.Cli.Tests.Acid3HtmlElementRegressionTests`
- `Broiler.Cli.Tests.Acid3SpecialRegressionTests`
- `Broiler.Cli.Tests.Acid3CssSelectorRegressionTests`
- `Broiler.Cli.Tests.SelectorsLevel4SpecificityTests`

These suites cover WPT-derived CSS/custom-property and selector slices,
focused Acid3 regressions, and a Google Search real-page parity sanity gate.
Open engine-specific work belongs in the
[CSS](../../../Broiler.CSS/docs/roadmap.md),
[HTML](../../../Broiler.HTML/docs/roadmap.md), or
[layout](../../../Broiler.Layout/docs/roadmap.md) roadmap.

## Reproducing the signal

```bash
dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --configuration Release --filter "FullyQualifiedName~Broiler.Cli.Tests.WptCssVariablesTests|FullyQualifiedName~Broiler.Cli.Tests.WptFontAndSelectorTests|FullyQualifiedName~GoogleSearchComplianceTests|FullyQualifiedName~Acid3HtmlElementRegressionTests|FullyQualifiedName~Acid3SpecialRegressionTests|FullyQualifiedName~Acid3CssSelectorRegressionTests|FullyQualifiedName~SelectorsLevel4SpecificityTests"
```
