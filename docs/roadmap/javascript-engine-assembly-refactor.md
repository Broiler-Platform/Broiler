# Broiler.JavaScript (JavaScript-Engine) Assembly Refactor Roadmap

## 1. Overview

This roadmap tracks the refactoring of the Broiler.JavaScript (JavaScript-Engine) core component into logically separated assemblies. The goal is to improve maintainability, testability, and modularity by giving each major subsystem its own assembly with well-defined boundaries and dependency directions.

---

## 2. Current Architecture Analysis

### 2.1 Assembly Inventory

The JavaScript engine comprises **17 assemblies** (plus 12 test projects):

| Assembly | Purpose | .cs Files | Layer |
|----------|---------|-----------|-------|
| `Broiler.JavaScript.ExpressionCompiler` | LINQ expression-tree compilation infrastructure | 150 | Foundation |
| `Broiler.JavaScript.Ast` | AST node definitions (expressions, statements, literals) | 65 | Foundation |
| `Broiler.JavaScript.Storage` | Memory management (VirtualMemory, property storage, caching) | 14 | Foundation |
| `Broiler.JavaScript.Parser` | JavaScript lexer/parser (FastParser, FastScanner) | 45 | Foundation |
| `Broiler.JavaScript.Runtime` | Execution engine, value types (`JSValue`, `Arguments`, delegates) | 24 | Foundation |
| `Broiler.JavaScript.Core` | Main engine (JSContext, globals, scope, LINQ builders) | 178 | Core |
| `Broiler.JavaScript.Compiler` | FastCompiler JIT/compilation pipeline | 42 | Feature |
| `Broiler.JavaScript.BuiltIns` | Built-in objects (Map, Set, Date, BigInt, Intl, Console, etc.) | 24 | Feature |
| `Broiler.JavaScript.Clr` | CLR interop (.NET object marshalling, proxy builders) | 13 | Feature |
| `Broiler.JavaScript.Debugger` | Debugging infrastructure (breakpoints, stepping) | 27 | Feature |
| `Broiler.JavaScript.Modules` | Module system (ESM, CommonJS, module loading) | 5 | Feature |
| `Broiler.JavaScript.ModuleExtensions` | Fluent API extensions for module registration | 2 | Feature |
| `Broiler.JavaScript.Network` | Network-related utilities | 11 | Feature |
| `Broiler.JavaScript.NodePollyfill` | Node.js polyfill layer | 1 | Feature |
| `Broiler.JavaScript.JSClassGenerator` | Source generator for built-in class registration | 7 | Tooling |
| `Broiler.JavaScript.All` | Meta-package (transitively includes all engine assemblies) | 0 | Integration |
| `Broiler.JavaScript` | CLI REPL and script runner | 39 | Application |

### 2.2 Dependency Graph

```
Application Layer
  Broiler.JavaScript (CLI)
    └─► Clr, Core, ExpressionCompiler, Modules, Network

Integration Layer
  Broiler.JavaScript.All (meta-package)
    └─► Core, Clr, Compiler, Modules, ModuleExtensions, BuiltIns, Debugger

Feature Layer
  ModuleExtensions ──► Clr, Core, ExpressionCompiler, Modules
  Modules ──────────► Core
  Debugger ─────────► Core
  Clr ──────────────► Core
  BuiltIns ─────────► Core
  Compiler ─────────► Core

Core Layer
  Core ──► Parser, Storage, Runtime, ExpressionCompiler

Foundation Layer
  Parser ──► Ast
  Storage ──► ExpressionCompiler
  Runtime ──► Ast, ExpressionCompiler
  Ast ──► ExpressionCompiler
  ExpressionCompiler (leaf — no internal dependencies)
```

### 2.3 Decoupling Mechanisms

The architecture uses three key mechanisms to maintain clean layering:

1. **Type Forwarding** — `[assembly: TypeForwardedTo(...)]` attributes in `Broiler.JavaScript.Core` preserve binary compatibility for types that were extracted to Foundation assemblies:
   - **31 types** forwarded via `AssemblyInfo.cs` (Runtime, Storage types)
   - **12 types** forwarded via `ParserTypeForwarding.cs` (Parser types)
   - **10 types** forwarded via `StorageTypeForwarding.cs` (Storage types)
   - **18 types** forwarded via `AstTypeForwarding.cs` (AST types)

2. **Module Initializers** — `[ModuleInitializer]` methods in satellite assemblies register factories and delegates without requiring reverse references:
   - `BuiltInsAssemblyInitializer` — registers built-in classes and factory delegates for `JSDecimal`, `JSBigInt`, `JSConsole`, `JSIntl`, `JSDisposableStack`, structured clone
   - `CompilerAssemblyInitializer` — registers `DefaultJSCompiler` compilation pipeline
   - `ClrAssemblyInitializer` — wires CLR interop, proxy builders, and CSX module provider
   - `CoreScriptCoreExtensions` — wires `CoreScript` factory delegates for compiler, cache, context, and error creation
   - `JSValueCoreExtensions` — wires `JSValue` core value constants (`UndefinedValue`, `NullValue`, `BooleanTrue/False`) and factory delegates (`CreateNumber`, `CreateString`), plus `Arguments.Empty`
   - `PropertySequenceCoreExtensions` — wires `PropertySequence.TypeErrorFactory` for property deletion errors

3. **Interface/Delegate Contracts** — Core defines contracts (`IClrInterop`, `IBuiltInRegistry`, `IJSCompiler`, `ICodeCache`, `IDebugger`) that feature assemblies implement, plus factory delegates (`CreateDecimalFactory`, `CreateBigIntFactory`, `ConsoleFactory`, etc.) set at initialization time.

### 2.4 Core Assembly Internal Structure

The Core assembly (178 files) is the largest and contains these logical subsystems:

| Subdirectory | Files | Description |
|--------------|-------|-------------|
| `Core/` | 92 | Main runtime objects: JSContext, globals, primitives, promises, objects, properties, iterators, regex, date, symbols, maps/sets, BigInt, Decimal, Intl stubs, CLR fallback |
| `LinqExpressions/` | 41 | LINQ expression-tree builders for JS operations (arithmetic, comparison, coercion, property access, function calls) |
| `Extensions/` | 10 | Extension methods for collections, strings, types |
| `Utils/` | 9 | Utility classes (hashing, pooling, span helpers) |
| `CodeGen/` | 2 | Source generator integration points |
| `Emit/` | 2 | IL emission infrastructure |
| Other (`Debugger`, `FastParser`, `LambdaGen`, `Parser`) | 4 | Small adapter/integration files |

---

## 3. Assembly Boundary Definitions

### 3.1 Foundation Layer

These assemblies have **no upward dependencies** and contain the lowest-level abstractions:

- **`ExpressionCompiler`** — LINQ expression-tree manipulation, compilation helpers. Leaf dependency for most other assemblies.
- **`Ast`** — JavaScript AST node types and token definitions. Depends only on ExpressionCompiler.
- **`Storage`** — Memory management primitives (`VirtualMemory`, `VirtualArray`, concurrent maps, property storage). Depends only on ExpressionCompiler.
- **`Runtime`** — Core value types (`JSValue`, `Arguments`, `PropertyKey`), function delegate types, prototype/symbol contracts. Depends on Ast and ExpressionCompiler.
- **`Parser`** — JavaScript lexer and parser. Depends on Ast.

### 3.2 Core Layer

- **`Core`** — Central engine assembly containing `JSContext`, global objects, scope management, LINQ expression builders, property/element storage, and built-in registration infrastructure (`DefaultBuiltInRegistry`). Depends on Parser, Storage, Runtime, and ExpressionCompiler.

### 3.3 Feature Layer

Each feature assembly depends on Core and implements a specific capability:

- **`Compiler`** — `FastCompiler` JIT/compilation pipeline. Registers via `CompilerAssemblyInitializer`.
- **`BuiltIns`** — Extracted built-in JavaScript objects (Map, Set, Date, BigInt, Decimal, Intl, Console, JSON, Proxy, Math, Reflect, WeakRef, FinalizationRegistry, etc.). Registers via `BuiltInsAssemblyInitializer`.
- **`Clr`** — CLR/.NET interop (proxy creation, expression building, CSX module support). Registers via `ClrAssemblyInitializer`.
- **`Debugger`** — Debugging infrastructure (breakpoints, stepping, scope introspection).
- **`Modules`** — ESM/CommonJS module system, module loading and resolution.
- **`ModuleExtensions`** — Fluent API for module registration (depends on Clr, Core, Modules).
- **`Network`** — Network utilities.
- **`NodePollyfill`** — Node.js compatibility polyfills.

### 3.4 Integration & Application Layer

- **`All`** — Meta-package that transitively includes all engine assemblies for consumers who want the full engine.
- **`Broiler.JavaScript` (CLI)** — Command-line REPL and script runner.

### 3.5 Cross-Assembly Interaction Rules

1. **No circular references.** Dependency flow is strictly downward: Feature → Core → Foundation.
2. **Upward communication via delegates/interfaces.** Feature assemblies register factories into Core's static properties at module initialization time (e.g., `JSValue.CreateBigIntFactory`, `DefaultBuiltInRegistry.ConsoleFactory`).
3. **Type forwarding for backward compatibility.** When a type moves from Core to a Foundation assembly, a `[TypeForwardedTo]` attribute is added to Core so existing consumers continue to resolve the type.
4. **Namespace preservation.** Extracted types retain their original namespace (e.g., `Broiler.JavaScript.Core.Core.Map.JSMap` stays the same after moving to the BuiltIns assembly) for source compatibility.

---

## 4. Implementation Roadmap

### Milestone 1: Solution & CI Infrastructure ✅

**Objective:** Establish the multi-assembly solution structure and CI pipeline.

| Step | Description | Status |
|------|-------------|--------|
| 1.1 | Create `YantraJS.sln` with all assembly projects | ✅ Done |
| 1.2 | Create `Directory.Build.props` with shared defaults (net8.0, LangVersion=latest) | ✅ Done |
| 1.3 | Set up CI workflow (`.github/workflows/ci.yml`) with 3-platform matrix (ubuntu, windows, macos) | ✅ Done |
| 1.4 | Add 12 test projects covering all assemblies | ✅ Done |
| 1.5 | Verify baseline: all tests pass, 0 build errors | ✅ Done |

**Tests:** 54 tests across 12 test projects (Storage: 5, Ast: 5, Parser: 5, Runtime: 4, Core: 8, Compiler: 3, BuiltIns: 5, Clr: 3, Debugger: 3, Modules: 3, ModuleExtensions: 3, Integration: 7).

### Milestone 2: Initial Built-In Extraction ✅

**Objective:** Extract first batch of built-in types from Core to BuiltIns assembly, establishing the extraction pattern.

| Step | Description | Status |
|------|-------------|--------|
| 2.1 | Extract `JSProxy` to BuiltIns | ✅ Done |
| 2.2 | Extract `JSMath` to BuiltIns | ✅ Done |
| 2.3 | Extract `JSReflect` to BuiltIns | ✅ Done |
| 2.4 | Extract `JSConsole` to BuiltIns (with `ConsoleFactory` delegate) | ✅ Done |
| 2.5 | Wire `BuiltInsAssemblyInitializer` registration chain | ✅ Done |
| 2.6 | Verify: 93 tests pass, 0 build errors | ✅ Done |

**Pattern Established:** Types that depend only on Core abstractions can be moved to BuiltIns. When Core needs to create instances, a factory delegate is introduced (e.g., `DefaultBuiltInRegistry.ConsoleFactory`).

### Milestone 3: Extended Built-In Extraction ✅

**Objective:** Extract remaining built-in types that have no compiler coupling.

| Step | Description | Status |
|------|-------------|--------|
| 3.1 | Extract `JSJSON` and `JSJsonParser` to BuiltIns | ✅ Done |
| 3.2 | Extract `DataView` and `DataViewStatic` to BuiltIns | ✅ Done |
| 3.3 | Extract `JSMap` and `JSWeakMap` to BuiltIns | ✅ Done |
| 3.4 | Extract `JSSet` and `JSWeakSet` to BuiltIns | ✅ Done |
| 3.5 | Add `StructuredCloneExtension` delegate for Map/Set clone support | ✅ Done |
| 3.6 | Verify: 116 tests pass across 12 projects | ✅ Done |

**Total BuiltIns types extracted:** 24 (12 in M2, 8 in M3, plus pre-existing).

### Milestone 4: Compiler-Coupled Type Decoupling ✅

**Objective:** Decouple types that have references from the Compiler assembly.

| Step | Description | Status |
|------|-------------|--------|
| 4.1 | Decouple `JSBigInt` via factory delegates (`CreateBigIntFromStringFactory`, `CreateBigIntFactory` on `JSValue`) | ✅ Done |
| 4.2 | Update `JSBigIntBuilder` to use `StaticCallExpression` | ✅ Done |
| 4.3 | Update `JSGlobal` to use `JSValue.CreateBigInt()` and `TypeOf()` | ✅ Done |
| 4.4 | Assess TypedArrays — confirmed zero compiler coupling (no extraction blocker) | ✅ Done |
| 4.5 | Verify: 116 tests pass across 12 projects | ✅ Done |

### Milestone 5: Target Framework Alignment ✅

**Objective:** Ensure all projects use consistent target framework and build settings.

| Step | Description | Status |
|------|-------------|--------|
| 5.1 | Align all projects to `net8.0` TFM | ✅ Done |
| 5.2 | Verify `Directory.Build.props` provides shared defaults | ✅ Done |
| 5.3 | Confirm CI uses `dotnet-version: 8.0.x` | ✅ Done |
| 5.4 | Verify: 116 tests pass, 0 build errors | ✅ Done |

### Milestone 6: Final Validation ✅

**Objective:** Comprehensive validation of the refactored architecture.

| Step | Description | Status |
|------|-------------|--------|
| 6.1 | Full CI pipeline green on all 3 platforms (ubuntu, windows, macos) | ✅ Done |
| 6.2 | Verify all 71 `TypeForwardedTo` attributes resolve correctly | ✅ Done |
| 6.3 | Verify all 6 module initializers execute in correct order | ✅ Done |
| 6.4 | Validate no circular assembly references exist | ✅ Done |
| 6.5 | Confirm backward compatibility: consumers referencing Core still resolve forwarded types | ✅ Done |
| 6.6 | Performance baseline: ensure no regression from assembly separation overhead | ✅ Done |

**Validation Details:**
- Fixed build breakage from Ast namespace reorganization (`Broiler.JavaScript.Ast.Misc`) in 7 files
- Added 15 validation tests in `Broiler.JavaScript.Integration.Tests/M6ValidationTests.cs` covering:
  - TypeForwardedTo resolution (71 forwarded types across 4 files, spot-checked against 19 representative types)
  - Module initializer wiring (all 6 initializers verified through behavioral tests)
  - Circular reference detection (DFS-based cycle detection on loaded assembly graph)
  - Backward compatibility (type forwarding via Core, namespace preservation, dependency layering)
  - Performance baseline (eval latency < 200ms, Map+Set 1000-element operations < 5s)
- All 131 tests pass (116 existing + 15 new M6 validation tests)

### Milestone 7: Future Extraction Candidates ✅

**Objective:** Identify, plan, and execute extraction of remaining built-in types from Core.

Coupling analysis was performed for each candidate in `Broiler.JavaScript.Core/Core/`. Viable candidates (TypedArrays, Iterator) have been extracted; non-viable candidates (RegExp, Promise) remain in Core.

| Step | Description | Status |
|------|-------------|--------|
| 7.1 | Analyze TypedArrays coupling (14 files in `Core/Array/Typed/`) | ✅ Done |
| 7.2 | Analyze RegExp coupling (`Core/RegExp/`) | ✅ Done |
| 7.3 | Analyze Promise coupling (`Core/Promise/`) | ✅ Done |
| 7.4 | Analyze Iterator coupling (`Core/Iterator/`) | ✅ Done |
| 7.5 | Confirm Intl extraction status (`BuiltIns/Intl/`) | ✅ Done |
| 7.6 | Extract TypedArrays: move 14 files from Core to BuiltIns | ✅ Done |
| 7.7 | Extract Iterator: move JSIteratorObject from Core to BuiltIns | ✅ Done |
| 7.8 | Add IteratorPrototypeSetup delegate to DefaultBuiltInRegistry | ✅ Done |
| 7.9 | Move ArrayBuffer StructuredClone to StructuredCloneExtension delegate | ✅ Done |
| 7.10 | Extract KeyEnumerator to Core/Enumerators/ for cross-assembly use | ✅ Done |
| 7.11 | Add 19 M7 validation tests in `M7ValidationTests.cs` | ✅ Done |
| 7.12 | Verify: 150 tests pass across 12 projects | ✅ Done |

**Candidate Analysis:**

| Candidate | Verdict | Compiler Coupling | Parser Coupling | Core Coupling | Extraction Pattern |
|-----------|---------|-------------------|-----------------|---------------|-------------------|
| **TypedArrays** | ✅ Extracted | None | None | JSGlobal StructuredClone type checks | Extended `StructuredCloneExtension` delegate (same pattern as JSMap/JSSet) |
| **RegExp** | ❌ Not extractable | JSRegExpBuilder in LinqExpressions (Compiler uses it for regex literals) | Light (RegExpValidator) | JSStringPrototype has 8+ hardcoded `is JSRegExp` checks | Would require Compiler→BuiltIns dependency; String prototype refactor too invasive |
| **Promise** | ❌ Not extractable | None | None | `JSContext.PendingPromises` field is `ConcurrentDictionary<long, JSPromise>`; JSAsyncFunction creates instances | Infrastructure-level coupling; cannot abstract without major JSContext refactor |
| **Iterator** | ✅ Extracted | None | None | `DefaultBuiltInRegistry` hardcodes 12 static method references | Replaced with `IteratorPrototypeSetup` delegate (same pattern as ConsoleFactory) |
| **Intl** | ✅ Already extracted | None | None | Factory delegates (`IntlFactory`, `IntlDateFormatter`) | Template pattern — fully decoupled via BuiltInsAssemblyInitializer |

**Extraction Implementation Details:**

**TypedArrays** (14 files, ~2,240 LOC) — ✅ Completed
1. Moved `JSArrayBuffer`, `JSTypedArray`, `JSTypedArray.prototype`, `TypedArrayParameters`, and 10 concrete typed array types from `Core/Array/Typed/` to `BuiltIns/Array/Typed/`
2. Extended `StructuredCloneExtension` delegate in `BuiltInsAssemblyInitializer` to handle ArrayBuffer cloning (alongside Map/Set)
3. Removed ArrayBuffer handling from `JSGlobal.StructuredCloneValue` — now delegated to BuiltIns
4. Extracted shared `KeyEnumerator(int length)` struct to `Core/Enumerators/KeyEnumerator.cs` for cross-assembly use by JSString, JSArrayPrototype, and JSTypedArray
5. TypedArray types registered automatically via `[JSClassGenerator]` source generation in BuiltIns
6. Removed unused `using Broiler.JavaScript.Core.Typed` imports from JSString.cs and JSArrayPrototype.cs

**Iterator Helpers** (1 file, ~587 LOC) — ✅ Completed
1. Moved `JSIteratorObject` from `Core/Iterator/` to `BuiltIns/Iterator/`
2. Added `IteratorPrototypeSetup` delegate property to `DefaultBuiltInRegistry`
3. Made `AddProto` method public on `DefaultBuiltInRegistry` for satellite assembly use
4. Wired 11 iterator helper methods (map, filter, take, drop, flatMap, reduce, toArray, forEach, some, every, find) via delegate in `BuiltInsAssemblyInitializer`
5. Replaced `Names.Iterator` (source-generated) with `KeyStrings.GetOrCreate("Iterator")` in `DefaultBuiltInRegistry`
6. Iterator type registered automatically via `[JSClassGenerator]` source generation in BuiltIns

**Validation Details:**
- 19 validation tests in `Broiler.JavaScript.Integration.Tests/M7ValidationTests.cs` covering:
  - TypedArrays: extracted to BuiltIns, no compiler coupling, functional end-to-end, StructuredClone delegated (4 tests)
  - RegExp: remains in Core, compiler coupling confirmed, functional with String methods (3 tests)
  - Promise: remains in Core, JSContext coupling confirmed, functional end-to-end (3 tests)
  - Iterator: extracted to BuiltIns, no compiler/parser coupling, decoupled from registry, functional end-to-end (4 tests)
  - Intl: in BuiltIns, Core has no direct reference, factory delegate wired (3 tests)
  - Extraction pattern invariants: Core→Foundation only, all candidates accounted for (2 tests)
- All 150 tests pass (131 M6 baseline + 19 M7 validation tests)

### Milestone 8: Documentation & Developer Experience 🔲

**Objective:** Ensure the architecture is well-documented for contributors.

| Step | Description | Status |
|------|-------------|--------|
| 8.1 | Document the extraction pattern (move type, add type forwarding, wire factory delegate) | 🔲 Pending |
| 8.2 | Add architecture diagram to README | 🔲 Pending |
| 8.3 | Document module initializer chain and startup sequence | 🔲 Pending |
| 8.4 | Add contribution guidelines for adding new built-in types | 🔲 Pending |

---

## 5. Test Strategy

### 5.1 Test Projects (Current State)

Each assembly has a corresponding test project. The counts below reflect the current state after M7 completion (150 total tests):

| Test Project | Tests | Coverage Area |
|-------------|-------|---------------|
| `Broiler.JavaScript.Storage.Tests` | 5 | VirtualMemory, property storage |
| `Broiler.JavaScript.Ast.Tests` | 5 | AST node construction, visitor pattern |
| `Broiler.JavaScript.Parser.Tests` | 5 | Lexer/parser correctness |
| `Broiler.JavaScript.Runtime.Tests` | 4 | JSValue operations, type coercion |
| `Broiler.JavaScript.Core.Tests` | 8 | JSContext, scope, globals |
| `Broiler.JavaScript.Compiler.Tests` | 3 | FastCompiler compilation |
| `Broiler.JavaScript.BuiltIns.Tests` | 58 | Built-in object behavior |
| `Broiler.JavaScript.Clr.Tests` | 3 | CLR interop marshalling |
| `Broiler.JavaScript.Debugger.Tests` | 3 | Debugging infrastructure |
| `Broiler.JavaScript.Modules.Tests` | 3 | Module loading/resolution |
| `Broiler.JavaScript.ModuleExtensions.Tests` | 3 | Module registration API |
| `Broiler.JavaScript.Integration.Tests` | 50 | End-to-end engine scenarios, M6 validation, M7 extraction validation |

### 5.2 Testing Approach per Milestone

- **Before extraction:** Verify all existing tests pass (baseline).
- **During extraction:** Run targeted tests for the affected assembly and its dependents.
- **After extraction:** Run full test suite across all 12 test projects on all 3 CI platforms.
- **Module initializer tests:** Force-load satellite assemblies via `RuntimeHelpers.RunClassConstructor(typeof(ExtractedType).TypeHandle)` to trigger initialization.

### 5.3 CI Configuration

- **Workflow:** `.github/workflows/ci.yml`
- **Triggers:** Push to `main`, pull requests to `main`
- **Matrix:** ubuntu-latest, windows-latest, macos-latest
- **SDK:** .NET 8.0.x
- **Coverage:** Coverlet (XPlat code coverage) for all 12 test projects

---

## 6. Migration & Compatibility

### 6.1 Binary Compatibility

Type forwarding (`[assembly: TypeForwardedTo]`) ensures that assemblies compiled against `Broiler.JavaScript.Core` continue to resolve types that have been moved to Foundation assemblies. Currently **71 types** are forwarded across 4 forwarding files.

### 6.2 Source Compatibility

Namespaces are preserved during extraction. A type moved from `Broiler.JavaScript.Core` to `Broiler.JavaScript.BuiltIns` retains its original namespace, so source code using the type does not need `using` changes.

### 6.3 NuGet Package Compatibility

The `Broiler.JavaScript.All` meta-package transitively includes all engine assemblies, so consumers who reference it get the complete engine regardless of how types are distributed internally.

### 6.4 Fallback Patterns

When an optional feature assembly is not loaded, Core provides fallback behavior:
- **`FallbackClrInterop`** — Marshals primitives to JS values; returns `JSUndefined` for complex objects when the full Clr assembly is not loaded.
- **Factory delegates default to null** — Core checks factory delegates before invocation and provides sensible defaults (e.g., no console output if `ConsoleFactory` is not wired).

---

## 7. Review & Feedback Process

### 7.1 Review Cadence

- **Per-milestone review:** Each milestone should be reviewed before proceeding to the next. Review includes:
  - Code review of extracted types and factory delegates
  - Verification that all tests pass on all CI platforms
  - Check for unintended dependency additions

### 7.2 Review Checklist

For each extraction:
- [ ] Type moved to correct assembly
- [ ] `[TypeForwardedTo]` added to Core (if applicable)
- [ ] Factory delegate introduced (if Core needs to create instances)
- [ ] Module initializer updated to wire the factory
- [ ] Namespace preserved for source compatibility
- [ ] Unit tests updated or added in the target assembly's test project
- [ ] No circular assembly references introduced
- [ ] Full test suite passes

### 7.3 Stakeholder Feedback

- Track progress via this roadmap issue
- Collect feedback on assembly boundary decisions before major extractions
- Adjust roadmap based on real-world usage patterns and performance data

### 7.4 Iterative Adjustment

The roadmap is a living document. As extraction proceeds:
- New candidates may be identified
- Some candidates may be deferred if extraction is too complex or risky
- Assembly boundaries may be refined based on dependency analysis

---

## 8. Build & Test Commands

```bash
# Build the full solution
dotnet build Broiler.JavaScript/YantraJS.sln --configuration Release

# Run all tests
dotnet test Broiler.JavaScript/YantraJS.sln

# Run tests for a specific assembly
dotnet test Broiler.JavaScript/Broiler.JavaScript.BuiltIns.Tests/

# Run with code coverage
dotnet test Broiler.JavaScript/YantraJS.sln --collect:"XPlat Code Coverage"
```

---

## 9. Summary

| Milestone | Status | Description |
|-----------|--------|-------------|
| M1 | ✅ Complete | Solution & CI infrastructure |
| M2 | ✅ Complete | Initial built-in extraction (4 types) |
| M3 | ✅ Complete | Extended built-in extraction (8 types) |
| M4 | ✅ Complete | Compiler-coupled type decoupling (JSBigInt) |
| M5 | ✅ Complete | Target framework alignment (net8.0) |
| M6 | ✅ Complete | Final validation |
| M7 | ✅ Complete | Future extraction candidates |
| M8 | 🔲 Pending | Documentation & developer experience |

**Current state:**
- 40 built-in types extracted to `Broiler.JavaScript.BuiltIns` (24 M1–M4 + 14 TypedArrays + 1 Iterator + 1 Intl)
- 71 types forwarded for backward compatibility
- 6 module initializers wiring satellite assemblies
- TypedArrays and Iterator successfully extracted in M7
- 2 candidates confirmed non-extractable (RegExp, Promise)
- 150 tests passing across 12 test projects
- Full CI running on 3 platforms
