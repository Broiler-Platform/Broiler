# Broiler.Input Phase 1 Testing Foundation

**Status:** Implemented  
**Date:** 2026-07-02

Phase 1 uses a package-free test foundation instead of xUnit or another
third-party test framework.

## Projects

| Project | Purpose |
|---|---|
| `Broiler.Input.Testing` | Deterministic fake clock, fake provider/device, bounded queue, reusable contract assertions |
| `Broiler.Input.Contract.Tests` | Console test runner for contract checks and API compatibility |

Both projects are `IsPackable=false`.

## Fake Coverage

The fake provider proves:

- stable descriptor enumeration;
- open, start, stop, close, and dispose lifecycle transitions;
- cancellation before open;
- removal notification and unavailable device transition;
- bounded delivery with observable dropped-item metrics; and
- diagnostics emitted by lifecycle/fault transitions.

## Running

```powershell
dotnet run --project Broiler.Input\Broiler.Input.Contract.Tests\Broiler.Input.Contract.Tests.csproj
```

The runner exits non-zero on the first failed contract.
