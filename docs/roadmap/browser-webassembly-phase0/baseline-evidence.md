# Browser WebAssembly Phase 0 Baseline Evidence

**Status:** Complete  
**Capture date:** 2026-07-11

This record is completed by the Phase 0 validation run. It distinguishes the
official empty .NET WebAssembly runtime baseline from Broiler execution, which
begins in Phase 1.

## Reference environment

| Item | Recorded value |
|---|---|
| Operating system | Windows 11 Enterprise 10.0.26200, build 26200 |
| CPU | AMD Ryzen 7 5800X, 8 cores / 16 logical processors |
| Installed memory | 34,276,364,288 bytes (31.92 GiB) |
| .NET SDK | 10.0.301 |
| .NET runtime | Microsoft.NETCore.App 10.0.9; browser marker 10.0.9 |
| Workload | `wasm-tools` 10.0.109 / manifest 10.0.100; workload-set 10.0.300-manifests.8c7d7c03 |
| Empty-app template | `Microsoft.NET.Runtime.WebAssembly.Templates.net10` 10.0.9 |
| Browser | Codex in-app browser, local loopback run |

## T0 build evidence

| Target | Result |
|---|---|
| `Broiler.Graphics` Release `browser-wasm` | Passed; 0 warnings, 0 errors |
| `Broiler.UI` Release `browser-wasm` | Passed; 0 warnings, 0 errors |
| `Broiler.UI.Standard` Release `browser-wasm` | Passed; 0 warnings, 0 errors |
| Exact selected T2 closure Release `browser-wasm` | Passed; 54 assemblies, 0 warnings, 0 errors |
| Forbidden Windows/Linux dependency scan | Passed; no `.Windows`, `.Linux`, Direct2D, OpenGL, or Vulkan assembly |

## Desktop artifacts and input trace

Committed under
`docs/testing/baselines/browser-webassembly-phase0/`:

| Artifact | Purpose | SHA-256 |
|---|---|---|
| `cpu-render-baseline.png` | Font-independent managed CPU render baseline | `f160ab0b8ef0c55123abae8a34bb6575a3dd74d1697758ab50df05851a3fbf63` |
| `render-list.json` | Normalized command list, including independent clip/transform interleaving | `2aabb79f3d94df876b6d165ad9a20f676bc014ddc40170df8dae2cf508423d5b` |
| `input-trace.json` | Normalized pointer, synthetic cancel cleanup, key, text, composition, and wheel trace | `0edd7a53d65360c2b99b6415549c80f0d1730f5ed80b25da19263eead78053f0` |
| `manifest.json` | Dimensions, raw RGBA hash, and artifact hashes | `6a477c7b1e24ab681a81f97010b18db030191cebe03b8f6238b7cc707c09b1cd` |

The committed artifact verifier must reproduce all hashes on Windows and Linux.
Text is deliberately absent from the exact render checksum.
The manifest records the 320 x 180 raw RGBA SHA-256 as
`0724aed9b9f5f7dab4b52780bb718965359a5da7e0ecd7881b15c6ccaf901394`.

## Official empty WebAssembly baseline

The empty app at `tests/browser-wasm-phase0/Broiler.Wasm.EmptyBaseline` contains
no Broiler reference. It records the toolchain/runtime cost before Phase 1 adds
Broiler assemblies.

| Metric | Recorded value |
|---|---|
| Release publish output bytes | 12,008,184 bytes in 43 files |
| Release `wwwroot` bytes | 11,871,639 bytes in 39 files |
| Precompressed `.br` bytes | 2,031,495 bytes in 13 files |
| Browser ready-marker startup | Ready on 3/3 local runs: 201.500 ms cold, then 115.400 ms and 64.300 ms with cache |
| Browser console errors | None in all three runs |

Startup is the duration from evaluation of the fingerprinted `main.js` module to
the managed `MarkReady` call. It is a local comparative baseline, not a network
load benchmark. Phase 1 records Broiler-specific startup, payload, first frame,
and console evidence under the same serving/browser conditions.

These values are reference-machine observations, not release budgets. The local
server did not send content-encoding headers, so the startup samples exercised
the uncompressed static assets; compressed-byte counts are recorded separately.

## Reproduction

```powershell
dotnet workload list

dotnet build Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj `
  -c Release -r browser-wasm
dotnet build Broiler.UI/src/Foundation/Broiler.UI/Broiler.UI.csproj `
  -c Release -r browser-wasm
dotnet build Broiler.UI/src/Foundation/Broiler.UI.Standard/Broiler.UI.Standard.csproj `
  -c Release -r browser-wasm
dotnet build tests/browser-wasm-phase0/Broiler.BrowserWasm.Phase0/Broiler.BrowserWasm.Phase0.csproj `
  -c Release -r browser-wasm

dotnet run --project tests/browser-wasm-phase0/Broiler.BrowserWasm.Phase0/Broiler.BrowserWasm.Phase0.csproj `
  -c Release -- --verify

dotnet publish tests/browser-wasm-phase0/Broiler.Wasm.EmptyBaseline/Broiler.Wasm.EmptyBaseline.csproj `
  -c Release -o artifacts/browser-wasm-phase0/empty-publish
```
