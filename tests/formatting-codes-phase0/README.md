# Formatting Codes Phase 0 fixtures

This directory contains deterministic, generated-in-memory fixtures for the
public Formatting Codes roadmap. Large text blobs are deliberately not checked
in. [`fixtures.json`](fixtures.json) is the reviewable manifest and the console
runner validates every generated document against its expected character,
paragraph, and normalized-run counts.

The Phase 0 runner measures three model-only reference operations:

- constructing the immutable `RichTextDocument` fixture;
- materializing its `PlainText`; and
- scanning every paragraph and normalized style run.

These are regression context and fixture validation, not a performance claim
for the Phase 1 projector. Projector timings and interaction budgets begin in
Phase 1.

Run from the repository root:

```powershell
dotnet run --project tests/formatting-codes-phase0/Broiler.FormattingCodes.Phase0.Benchmarks/Broiler.FormattingCodes.Phase0.Benchmarks.csproj -c Release -- --iterations 3
```

The runner writes a JSON report to standard output. Timing and allocation
figures are machine-specific; fixture sizes and counts must be identical on all
supported platforms.
