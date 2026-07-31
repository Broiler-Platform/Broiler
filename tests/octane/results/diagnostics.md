# Broiler Octane failure diagnostics

- Generated: `2026-07-31T20:28:07.637Z`
- Engine: `Broiler.JS (BroilerJS --script-host)`
- Per-suite timeout: 1800s

5 of 15 suites did not complete.

Statuses: **error** — Octane caught the throw and scored the rest;
**crash** — the process died and took the suite with it;
**timeout** — no result within the per-suite timeout.

| Suite | Status | Failing type | Where |
|---|---|---|---|
| [Crypto](#crypto) | crash | `Broiler.JavaScript.Runtime.JSException` | benchmark `Encrypt`, phase `run`, iteration 1 |
| [PdfJS](#pdfjs) | error | `Error` | benchmark `PdfJS`, phase `run`, iteration 1 |
| [CodeLoad](#codeload) | error | `TypeError` | benchmark `CodeLoadJQuery`, phase `run`, iteration 1 |
| [zlib](#zlib) | error | `ReferenceError` | benchmark `zlib`, phase `run`, iteration 1 |
| [Typescript](#typescript) | error | `TypeError` | benchmark `Typescript`, phase `run`, iteration 1 |

## Crypto

- **Status**: crash
- **Failing type**: `Broiler.JavaScript.Runtime.JSException` (.NET exception surfaced through the engine)
- **Where**: benchmark `Encrypt`, phase `run`, iteration 1
- **Files evaluated**: base.js → crypto.js
- **Ran for**: 2.7s
- **Full output**: `tests/octane/logs/broiler/Crypto.log`

```text
Unhandled exception. Broiler.JavaScript.Runtime.JSException: Maximum call stack size exceeded
at EnsureSufficientExecutionStack in /_/Broiler.JS/Broiler.JavaScript.Engine/JSContextExtensions.cs:line 31

    at EnsureSufficientExecutionStack:/_/Broiler.JS/Broiler.JavaScript.Engine/JSContextExtensions.cs:31,1
```

**JavaScript stack** — mapped back to the Octane sources:

```text
    at EnsureSufficientExecutionStack (/_/Broiler.JS/Broiler.JavaScript.Engine/JSContextExtensions.cs:31)
```

Re-run just this suite:

```bash
./scripts/run-octane-benchmarks.sh --engines broiler --skip-build --only Crypto --verbose
```

## PdfJS

- **Status**: error (exit code 0)
- **Failing type**: `Error`
- **Where**: benchmark `PdfJS`, phase `run`, iteration 1
- **Files evaluated**: base.js → pdfjs.js → octane-runner.js
- **Ran for**: 7.4s
- **Full output**: `tests/octane/logs/broiler/PdfJS.log`

```text
Malformed PDF: stream must have data
```

**JavaScript stack** — mapped back to the Octane sources:

```text
    at error (pdfjs.js:906)
    at malformed (pdfjs.js:915)
    at assertWellFormed (pdfjs.js:926)
    at init (pdfjs.js:728)
    at PDFDocument (pdfjs.js:719)
    at wphSetupDoc (pdfjs.js:29618)
    at messageHandlerComObjOnMessage (pdfjs.js:29571)
    at WorkerTransport_postMessage (pdfjs.js:1889)
    at messageHandlerSend (pdfjs.js:29603)
    at WorkerTransport_sendData (pdfjs.js:2016)
    at getPDFLoad (pdfjs.js:1481)
    at getPdfOnreadystatechange (pdfjs.js:458)
    at inline (pdfjs.js:203)
    at getPdf (pdfjs.js:464)
    at getDocument (pdfjs.js:1465)
```

Re-run just this suite:

```bash
./scripts/run-octane-benchmarks.sh --engines broiler --skip-build --only PdfJS --verbose
```

## CodeLoad

- **Status**: error (exit code 0)
- **Failing type**: `TypeError`
- **Where**: benchmark `CodeLoadJQuery`, phase `run`, iteration 1
- **Files evaluated**: base.js → code-load.js → octane-runner.js
- **Ran for**: 4.6s
- **Full output**: `tests/octane/logs/broiler/CodeLoad.log`

```text
Cannot get property userAgent of undefined
```

**JavaScript stack** — mapped back to the Octane sources:

```text
    at InitializeFactories (/_/Broiler.JS/Broiler.JavaScript.Engine/Core/JSValueCoreExtensions.cs:17)
    at inline (vm.js:1)
    at inline (vm.js:1)
    at native (vm.js:1)
    at inline (code-load.js:1549)
    at runJQuery (code-load.js:1541)
    at runCodeLoadJQuery (code-load.js:108)
    at Measure (base.js:307)
    at inline (base.js:325)
    at RunNextBenchmark (base.js:369)
    at RunStep (base.js:150)
    at inline (base.js:173)
    at __octaneRun (octane-runner.js:217)
    at native (octane-runner.js:225)
```

Re-run just this suite:

```bash
./scripts/run-octane-benchmarks.sh --engines broiler --skip-build --only CodeLoad --verbose
```

## zlib

- **Status**: error (exit code 0)
- **Failing type**: `ReferenceError`
- **Where**: benchmark `zlib`, phase `run`, iteration 1
- **Files evaluated**: base.js → zlib.js → zlib-data.js → octane-runner.js
- **Ran for**: 3.9s
- **Full output**: `tests/octane/logs/broiler/zlib.log`

```text
read is not defined
```

**JavaScript stack** — mapped back to the Octane sources:

```text
    at ResolveIdentifierWithoutWithScopes (/_/Broiler.JS/Broiler.JavaScript.Engine/JSContext.cs:1298)
    at native (vm.js:1)
    at InitializeZlibBenchmark (zlib-data.js:65)
    at runZlib (zlib.js:37)
    at Measure (base.js:307)
    at inline (base.js:325)
    at RunNextBenchmark (base.js:369)
    at RunStep (base.js:150)
    at inline (base.js:173)
    at __octaneRun (octane-runner.js:217)
    at native (octane-runner.js:225)
```

Re-run just this suite:

```bash
./scripts/run-octane-benchmarks.sh --engines broiler --skip-build --only zlib --verbose
```

## Typescript

- **Status**: error (exit code 0)
- **Failing type**: `TypeError`
- **Where**: benchmark `Typescript`, phase `run`, iteration 1
- **Files evaluated**: base.js → typescript.js → typescript-input.js → typescript-compiler.js → octane-runner.js
- **Ran for**: 17.4s
- **Full output**: `tests/octane/logs/broiler/Typescript.log`

```text
Cannot get property getScopedTypeNameEx of null
```

**JavaScript stack** — mapped back to the Octane sources:

```text
    at Item (/_/Broiler.JS/Broiler.JavaScript.BuiltIns/Null/JSNull.cs:50)
    at inline (typescript-compiler.js:14316)
    at inline (typescript-compiler.js:14302)
    at inline (typescript-compiler.js:21940)
    at inline (typescript-compiler.js:21933)
    at inline (typescript-compiler.js:14770)
    at inline (typescript-compiler.js:16674)
    at preCollectFuncDeclTypes (typescript-compiler.js:18438)
    at preCollectTypes (typescript-compiler.js:18503)
    at inline (typescript-compiler.js:3474)
    at walkListChildren (typescript-compiler.js:3656)
    at inline (typescript-compiler.js:3481)
    at walkRecordChildren (typescript-compiler.js:3867)
    at walkNamedTypeChildren (typescript-compiler.js:3872)
    at walkClassDeclChildren (typescript-compiler.js:3876)
```

Re-run just this suite:

```bash
./scripts/run-octane-benchmarks.sh --engines broiler --skip-build --only Typescript --verbose
```

---
_Generated by `scripts/run-octane.mjs`. Re-runs of a single suite write to `tests/octane/logs/partial/`._
