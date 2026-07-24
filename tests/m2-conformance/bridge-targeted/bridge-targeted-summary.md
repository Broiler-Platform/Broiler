# Bridge DOM/Web targeted conformance

This file is a focused `Broiler.HtmlBridge` conformance signal. It keeps the
DOM/event/CSSOM/microtask gate small and reproducible. Current ownership and
remaining integration work are documented in the
[HtmlBridge architecture](../../../docs/architecture/htmlbridge.md) and
[root roadmap](../../../docs/ROADMAP.md#htmlbridge-runtime).

## Current published result

| Metric | Value |
|---|---:|
| Total tests | 167 |
| Executed | 167 |
| Passed | 167 |
| Failed | 0 |
| Skipped / not executed | 0 |
| Pass rate | 100.0% |

## Included suites

- `Broiler.Cli.Tests.DomEventsTests`
- `Broiler.Cli.Tests.DomEventsEdgeCaseTests`
- `Broiler.Cli.Tests.SelectorsAndCssomTests`
- `Broiler.Cli.Tests.TimerAndAsyncTests`
- `Broiler.Cli.Tests.ScriptEngineExecuteTests.ScriptEngine_Execute_Runs_Microtasks_Between_Sequential_Scripts`
- `Broiler.Cli.Tests.ScriptEngineExecuteTests.ScriptEngine_Execute_Runs_Microtasks_Between_Deferred_Scripts`
- `Broiler.Cli.Tests.ScriptEngineExecuteTests.InteractiveSession_Step_Runs_Microtasks_Between_Timer_Tasks`

These suites cover:

- DOM event dispatch (`capture` / target / bubble, `composedPath()`, passive
  listeners, legacy aliases, modern constructors, and edge-case dispatch)
- CSSOM and `getComputedStyle()` compliance slices exercised by
  `SelectorsAndCssomTests`
- microtask checkpoint ordering through `queueMicrotask()`, bridge timer
  flushing, and sequential/deferred script execution

## Reproducing the signal

```bash
dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --configuration Release --filter "FullyQualifiedName~DomEventsTests|FullyQualifiedName~DomEventsEdgeCaseTests|FullyQualifiedName~SelectorsAndCssomTests|FullyQualifiedName~TimerAndAsyncTests|FullyQualifiedName~ScriptEngine_Execute_Runs_Microtasks_Between_Sequential_Scripts|FullyQualifiedName~ScriptEngine_Execute_Runs_Microtasks_Between_Deferred_Scripts|FullyQualifiedName~InteractiveSession_Step_Runs_Microtasks_Between_Timer_Tasks"
```
