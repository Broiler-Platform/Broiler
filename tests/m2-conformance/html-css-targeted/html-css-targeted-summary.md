# HTML/CSS targeted conformance

This file is the focused **Milestone 2 / W4** HTML/CSS signal of record for
the engines standards roadmap. It keeps the targeted HTML/CSS compliance gate
small, reproducible, and tied directly to the roadmap work that closed the
highest-value WPT, Acid, selector, and real-page parity slices.

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

These suites cover the W4 deliverables called out by the roadmap:

- the WPT-derived CSS/custom-property and selector slices from
  [`docs/roadmap/wpt-failure-triage.md`](../../../docs/roadmap/wpt-failure-triage.md)
- the focused Acid3 and selector regressions tied to
  [`docs/roadmap/acid3-compliance.md`](../../../docs/roadmap/acid3-compliance.md)
  and [`docs/roadmap/acid-test-triage.md`](../../../docs/roadmap/acid-test-triage.md)
- the real-page Google Search parity sanity gate from
  [`docs/roadmap/google-search-compliance.md`](../../../docs/roadmap/google-search-compliance.md)

## Reproducing the signal

```bash
dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --configuration Release --filter "FullyQualifiedName~Broiler.Cli.Tests.WptCssVariablesTests|FullyQualifiedName~Broiler.Cli.Tests.WptFontAndSelectorTests|FullyQualifiedName~GoogleSearchComplianceTests|FullyQualifiedName~Acid3HtmlElementRegressionTests|FullyQualifiedName~Acid3SpecialRegressionTests|FullyQualifiedName~Acid3CssSelectorRegressionTests|FullyQualifiedName~SelectorsLevel4SpecificityTests"
```
