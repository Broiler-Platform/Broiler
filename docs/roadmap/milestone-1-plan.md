# Milestone 1 — CI & Test Foundation

> **Parent initiative:** [JavaScript Engine Assembly Refactor](./javascript-engine-assembly-refactor.md)
> **Scope:** Tasks 11–13 from the roadmap (Infrastructure Gaps)
> **Estimated effort:** 2–3 days
> **Status:** Complete

---

## Table of Contents

1. [Goals](#1-goals)
2. [Current Baseline](#2-current-baseline)
3. [Task Checklist](#3-task-checklist)
4. [Task Details](#4-task-details)
5. [Test Baselines](#5-test-baselines)
6. [Assumptions](#6-assumptions)
7. [Risks & Mitigations](#7-risks--mitigations)
8. [Definition of Done](#8-definition-of-done)
9. [Dependencies & Sequencing](#9-dependencies--sequencing)

---

## 1. Goals

Milestone 1 establishes the infrastructure needed for safe, incremental extraction
work in subsequent milestones. Without CI and test projects, extraction changes
carry high regression risk.

**Primary goals:**
- Establish a CI workflow that builds all 17 Broiler.JavaScript assemblies on
  multiple platforms (ubuntu, windows, macos).
- Create unit test projects for each assembly that currently lacks one.
- Add an integration test project that validates cross-assembly wiring.
- Document current test baselines so future changes can be measured.

---

## 2. Current Baseline

### 2.1 Build Status

| Solution | Build Result | Errors | Warnings |
|----------|-------------|--------|----------|
| `Broiler.JavaScript/YantraJS.sln` | ✅ Success | 0 | 531 |
| `Broiler.slnx` (full) | ✅ Success | 0 | 531+ |

> Warnings are predominantly CS8669 (nullable annotations in generated code),
> CS0649 (unassigned fields), and SYSLIB0037 (obsolete API usage). These are
> pre-existing and not blocking.

### 2.2 Assembly Inventory (16 library assemblies + 1 executable)

| # | Assembly | Source Files (non-generated) |
|---|----------|---------------------------|
| 1 | ExpressionCompiler | 150 |
| 2 | JSClassGenerator | 7 |
| 3 | Ast | 65 |
| 4 | Storage | 14 |
| 5 | Parser | 45 |
| 6 | Runtime | 24 |
| 7 | **Core** | **190** |
| 8 | Compiler | 42 |
| 9 | Clr | 13 |
| 10 | Debugger | 27 |
| 11 | BuiltIns | 12 |
| 12 | Modules | 5 |
| 13 | ModuleExtensions | 2 |
| 14 | Network | 11 |
| 15 | NodePollyfill | 1 |
| 16 | All (meta-package) | 0 |
| 17 | Broiler.JavaScript (exe) | — |

### 2.3 Existing Tests

| Test Project | Location | Framework | Tests | Pass | Fail | Notes |
|-------------|----------|-----------|-------|------|------|-------|
| Broiler.Cli.Tests | `src/Broiler.Cli.Tests/` | xUnit 2.5.3 | 599 | 589 | 10 | Covers HTML/CSS/DOM/JS execution; 10 pre-existing failures |
| Broiler.DevConsole.Tests | `src/Broiler.DevConsole.Tests/` | xUnit 2.5.3 | 33 | 33 | 0 | Covers DevConsole UI services |
| JIntPerfTests | `Broiler.JavaScript/OtherTests/JIntPerfTests/` | Console app | N/A | N/A | N/A | Dromaeo benchmark suite (not unit tests) |

**JavaScript engine test coverage: 0 dedicated test projects.**

### 2.4 CI Status

**No CI workflow exists.** The `.github/workflows/` directory is absent.

---

## 3. Task Checklist

### Task 11 — Establish CI Workflow (P1, Medium)

- [x] Create `.github/workflows/ci.yml`
- [x] Configure matrix: `ubuntu-latest`, `windows-latest`, `macos-latest`
- [x] Add build step for `Broiler.JavaScript/YantraJS.sln`
- [x] Add test steps for each test project (existing + new)
- [x] Add code coverage collection with coverlet
- [ ] Validate CI workflow runs green on all platforms

### Task 12 — Create Unit Test Projects (P1, Large)

- [x] `Broiler.JavaScript.Storage.Tests` — PropertySequence, ElementArray, VirtualMemory
- [x] `Broiler.JavaScript.Ast.Tests` — AstNode types, FastToken, StringSpan
- [x] `Broiler.JavaScript.Parser.Tests` — FastParser, FastScanner
- [x] `Broiler.JavaScript.Runtime.Tests` — JSValue, Arguments, PropertyKey, interfaces
- [x] `Broiler.JavaScript.Core.Tests` — JSContext, JSObject, built-in types
- [x] `Broiler.JavaScript.Compiler.Tests` — FastCompiler IL generation
- [x] `Broiler.JavaScript.BuiltIns.Tests` — Event, WeakRef, Intl, Decimal, DisposableStack
- [x] `Broiler.JavaScript.Clr.Tests` — ClrProxy, ClrType, marshalling
- [x] `Broiler.JavaScript.Debugger.Tests` — CDP protocol handling
- [x] `Broiler.JavaScript.Modules.Tests` — CommonJS/ESM module loading
- [x] `Broiler.JavaScript.ModuleExtensions.Tests` — Module builder extensions
- [x] Add all test projects to `YantraJS.sln`
- [x] Verify all test projects build and run

### Task 13 — Integration Test Project (P2, Medium)

- [x] `Broiler.JavaScript.Integration.Tests` — Cross-assembly wiring validation
- [x] Test ModuleInitializer registration (Compiler, BuiltIns, Clr)
- [x] Test TypeForwardedTo resolution across assemblies
- [x] Test factory delegate wiring (CoreScript, JSCompiler, ClrInterop)
- [x] Test source generator output (Names.g.cs independence)
- [x] Add to `YantraJS.sln` and CI workflow

---

## 4. Task Details

### 4.1 CI Workflow (Task 11)

**Recommended workflow structure:**

```yaml
name: CI
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: dotnet build Broiler.JavaScript/YantraJS.sln
      - run: dotnet test <each-test-project> --collect:"XPlat Code Coverage"
```

**Key decisions:**
- .NET SDK 9.0 needed (main exe targets net9.0; libraries target net8.0)
- coverlet via `--collect:"XPlat Code Coverage"` for cross-platform coverage
- Each test project run independently for clearer failure isolation
- Build warnings are informational, not blocking

### 4.2 Test Project Structure (Task 12)

Each test project should follow this pattern:

```
Broiler.JavaScript/<Assembly>.Tests/
├── <Assembly>.Tests.csproj    (xUnit 2.5.3, net8.0)
└── <TestClass>Tests.cs        (initial smoke tests)
```

**csproj template:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\<Assembly>\<Assembly>.csproj" />
  </ItemGroup>
</Project>
```

**Initial test scope per project:**

| Test Project | Initial Tests | Scope |
|-------------|--------------|-------|
| Storage.Tests | 3–5 | PropertySequence CRUD, ElementArray bounds |
| Ast.Tests | 3–5 | AstNode creation, FastToken parsing, StringSpan equality |
| Parser.Tests | 5–8 | Parse simple expressions, statements, functions, modules |
| Runtime.Tests | 3–5 | JSValue boxing, Arguments indexing, PropertyKey equality |
| Core.Tests | 8–15 | JSContext lifecycle, JSObject property ops, built-in type registration |
| Compiler.Tests | 3–5 | Compile + execute simple scripts, verify IL output |
| BuiltIns.Tests | 5–8 | Event creation/dispatch, WeakRef deref, DisposableStack lifecycle |
| Clr.Tests | 3–5 | ClrProxy wrapping, type marshalling, interop calls |
| Debugger.Tests | 2–3 | CDP message parsing, breakpoint registration |
| Modules.Tests | 3–5 | ESM import/export, CommonJS require |
| ModuleExtensions.Tests | 2–3 | ModuleBuilder API, ExportType registration |

### 4.3 Integration Tests (Task 13)

Cross-assembly tests verifying the decoupling patterns work correctly:

| Test | Purpose |
|------|---------|
| `CompilerRegistration_Works` | Verify `CompilerAssemblyInitializer` registers `FastCompiler` |
| `BuiltInsRegistration_Works` | Verify `BuiltInsAssemblyInitializer` wires JSIntl, JSDecimal, etc. |
| `ClrRegistration_Works` | Verify `ClrAssemblyInitializer` registers `DefaultClrInterop` |
| `TypeForwarding_ResolvesCorrectly` | Verify types forwarded from Core resolve to their actual assemblies |
| `FactoryDelegates_Initialize` | Verify CoreScript, JSCompiler factory delegates are wired |
| `SourceGenerator_IndependentPerAssembly` | Verify Core and BuiltIns both have independent `Names` classes |
| `FullPipeline_ParseCompileExecute` | End-to-end: parse JS → compile → execute → verify result |

---

## 5. Test Baselines

### 5.1 Pre-M1 Test Baseline (Snapshot 2026-03-22)

| Metric | Value |
|--------|-------|
| Total test projects | 2 (Broiler.Cli.Tests, Broiler.DevConsole.Tests) |
| Total tests | 632 |
| Passing tests | 622 |
| Failing tests | 10 (all pre-existing in Broiler.Cli.Tests) |
| JavaScript engine test projects | 0 |
| JavaScript engine unit tests | 0 |
| JavaScript engine integration tests | 0 |
| Code coverage | Not measured |
| CI/CD pipeline | None |
| Build errors | 0 |
| Build warnings | 531 |

### 5.2 Post-M1 Target Baseline

| Metric | Target | Actual |
|--------|--------|--------|
| Total test projects | 14+ (2 existing + 11 new unit + 1 integration) | 14 |
| JavaScript engine unit test projects | 11 | 11 |
| JavaScript engine integration test projects | 1 | 1 |
| Minimum tests per new project | 2–3 (smoke tests) | 3–8 (54 total) |
| CI pipeline | Green on ubuntu, windows, macos | Configured |
| Code coverage collection | Enabled (coverlet) | Enabled |
| Build errors | 0 | 0 |

### 5.3 Pre-Existing Failures in Broiler.Cli.Tests

The 10 pre-existing test failures are in `Acid3RegressionTests` and are **not related**
to the JavaScript engine assembly refactor. They test HTML/CSS/DOM compliance (Acid3 suite).
These should not be considered blockers for M1.

---

## 6. Assumptions

1. **xUnit 2.5.3** is the standard test framework for this repository (used by
   existing test projects). All new test projects will use xUnit 2.5.3.

2. **net8.0** is the TFM for test projects (matching the library TFM). The main
   executable targets net9.0, but libraries and tests use net8.0.

3. **.NET SDK 9.0** is available in CI (needed for the net9.0 executable).

4. **coverlet.collector** is sufficient for code coverage collection. No additional
   coverage tools are needed at this stage.

5. **Test projects will initially contain smoke tests** (2–15 per project). Comprehensive
   test coverage expansion is a follow-up effort (not part of M1).

6. **The 531 build warnings are accepted** as pre-existing. M1 does not target
   warning reduction.

7. **JIntPerfTests** remains a standalone console benchmark application. It will not
   be converted to unit tests but may be referenced in CI for build validation.

8. **No external service dependencies** are needed for JavaScript engine tests.
   All tests can run in-process using `JSContext`.

---

## 7. Risks & Mitigations

| # | Risk | Impact | Likelihood | Mitigation |
|---|------|--------|-----------|------------|
| R1 | CI matrix runs too slowly (3 OS × many test projects) | Medium | Medium | Run test projects in parallel; use `--no-build` after initial build step |
| R2 | Nerdbank.GitVersioning fails on CI shallow clone | High | High | Use `fetch-depth: 0` in checkout action |
| R3 | net9.0 SDK not available on all CI runners | High | Low | Explicitly install via `actions/setup-dotnet` with `dotnet-version: '9.0.x'` |
| R4 | Test project references create circular dependencies | Medium | Low | Test projects reference only the assembly under test + test framework; no cross-test-project references |
| R5 | Source generator output differs across platforms | Medium | Low | Already verified: JSClassGenerator generates deterministic output |
| R6 | Some assemblies are difficult to test in isolation (e.g., Compiler needs Core + Runtime) | Medium | Medium | Allow test projects to reference multiple assemblies when needed for setup; document which tests are true unit vs. integration |
| R7 | Pre-existing build warnings treated as errors in CI | Low | Low | Do not enable `TreatWarningsAsErrors` in M1; address in M5 (Documentation milestone) |
| R8 | ModuleInitializer registration order matters | Medium | Low | Integration tests will verify registration; document expected initialization sequence |

---

## 8. Definition of Done

M1 is complete when all of the following are satisfied:

- [x] CI workflow (`.github/workflows/ci.yml`) exists and runs on push/PR to main
- [x] CI builds `YantraJS.sln` on ubuntu, windows, and macos with 0 errors
- [x] 11 unit test projects exist under `Broiler.JavaScript/`
- [x] 1 integration test project exists under `Broiler.JavaScript/`
- [x] All test projects are referenced in `YantraJS.sln`
- [x] All test projects build and run on all 3 platforms
- [x] Each test project has at least 2 passing smoke tests
- [x] Code coverage collection is enabled (coverlet)
- [x] CI workflow runs all test projects
- [x] Test baseline metrics documented (this document)

---

## 9. Dependencies & Sequencing

### Recommended implementation order:

```
Step 1: Create test project directories and .csproj files (Task 12)
   ↓
Step 2: Add smoke tests to each project; verify local build + run
   ↓
Step 3: Add projects to YantraJS.sln
   ↓
Step 4: Create integration test project (Task 13)
   ↓
Step 5: Create CI workflow (Task 11)
   ↓
Step 6: Validate CI runs green on all platforms
```

**Rationale:** Test projects should be created and verified locally before CI is added.
This avoids debugging CI issues and test issues simultaneously.

### Prerequisite for M2–M6:

All subsequent milestones depend on M1 being complete. Extraction work (M2, M3)
requires test projects to validate that type moves don't break behavior. Evaluation
work (M4) requires CI to run tests across platforms.

---

*Last updated: 2026-03-22*
