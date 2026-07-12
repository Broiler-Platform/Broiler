# Formatting Codes Phase 1 benchmark

This runner applies the grammar-version 1 projector to the shared deterministic
Phase 0 fixture manifest. It records full-rebuild time, thread-local allocation,
projected size, token count, diagnostics, and the host background-projection
recommendation.

Run from the repository root:

```powershell
dotnet run --project tests/formatting-codes-phase1/Broiler.FormattingCodes.Phase1.Benchmarks/Broiler.FormattingCodes.Phase1.Benchmarks.csproj -c Release -- --iterations 5
```

Timings are machine-specific. The frozen conservative policy is based on source
shape as well as projector time: a host should leave the UI path above 100,000
source UTF-16 characters or 10,000 combined paragraph/run structural units.
The synchronous projector remains thread-agnostic and cancellation-aware.
