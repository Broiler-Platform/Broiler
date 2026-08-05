# SampleReports — the Broiler Code Phase 0 workspace fixture

This is a stand-in for an arbitrary user workspace, not a Broiler component. It
exists so every later phase can be judged against the same solution, and so the
constructs the workspace model has to survive are present from the start rather
than discovered during Phase 2.

It is deliberately **not** registered in [`eng/solutions.json`](../../../eng/solutions.json):
it fails to compile by design, so adding it to a generated solution closure
would break the repository's build verification.

## Layout

| Path | Why it is here |
| --- | --- |
| `SampleReports.slnx` | Standard solution format with solution folders and solution items |
| `global.json` | Pins the SDK feature band; the worker must honor it instead of substituting the IDE's SDK |
| `NuGet.config` | Restore configuration that changes what the build resolves, so it belongs in the granted snapshot and the input manifest |
| `Directory.Build.props` / `.targets` | Ancestor isolation — see below |
| `src/SampleReports.Core` | Multi-targeting library (`net10.0;netstandard2.0`), embedded resource, linked file, conditional item, wildcard item |
| `src/SampleReports.App` | Console application with a project reference and the intentional errors |
| `shared/BuildStamp.cs` | A source file outside every project cone, reached by a linked `Compile` item |
| `targets/SampleReports.Android` | `net10.0-android36.0` head with a project-local ABI property |
| `targets/SampleReports.Web` | Standalone `Microsoft.NET.Sdk.WebAssembly` head with explicit interpreted and AOT publish profiles |
| `expected-diagnostics.json` | The diagnostics the fixture must produce, by rule ID and span — never by message text |

## Ancestor isolation

The fixture carries its own `Directory.Build.props` and `Directory.Build.targets`
that do not chain up to the repository's. Without them the fixture would
evaluate one way inside the Broiler checkout and another way after the build
worker materialized it into a neutral per-job root, and every measurement taken
against it would be unreproducible. The same reasoning is why the worker
materializes outside the checkout in the first place.

## The intentional errors

`src/SampleReports.App/Reporting/ReportRunner.Broken.cs` calls
`ReportFormatter.Format(report)` while the method is declared in
`src/SampleReports.Core/Reporting/ReportFormatter.cs` as
`Format(Report, FormatOptions)`. Nothing in the calling file is wrong when read
on its own — a syntax-only classifier cannot find this, which is exactly the
capability boundary Phase 0 has to make visible.

`src/SampleReports.App/Reporting/LegacySummary.cs` calls an `[Obsolete]` member
from the same referenced project and is always compiled, so both fixture
variants emit one warning. "Build succeeded" therefore never means "Problems
pane empty".

Setting `FixtureCorrected=true` swaps `ReportRunner.Broken.cs` for
`ReportRunner.Corrected.cs` through a conditional `Compile Remove` item, so the
corrected variant needed by the Phase 4 and Phase 5 artifact gates is reachable
without editing tracked source:

```bash
dotnet build tests/broiler-code-phase0/fixture/src/SampleReports.App/SampleReports.App.csproj -c Release -p:FixtureCorrected=true
```

## Recorded target status

Measured on the Phase 0 desktop baseline (see
[`../baselines`](../baselines/)). These are local build results, not a support
claim.

| Target | Command | Result |
| --- | --- | --- |
| .NET (broken) | `dotnet build src/SampleReports.App/SampleReports.App.csproj -c Release` | Fails with CS7036 + CS0618, as specified |
| .NET (corrected) | same, `-p:FixtureCorrected=true` | Succeeds with CS0618 |
| Android | `dotnet build targets/SampleReports.Android/SampleReports.Android.csproj -c Debug` | Produces a debug-signed APK |
| WebAssembly | `dotnet build targets/SampleReports.Web/SampleReports.Web.csproj -c Release` | Succeeds |

Android needs `ANDROID_HOME`/`ANDROID_SDK_ROOT` and the `android` workload;
WebAssembly publishing needs `wasm-tools`. Publishing the two browser profiles
and installing the APK on an emulator are Phase 5 gates, not Phase 0 ones.
