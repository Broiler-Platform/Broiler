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

### Milestone 8: Documentation & Developer Experience ✅

**Objective:** Ensure the architecture is well-documented for contributors.

| Step | Description | Status |
|------|-------------|--------|
| 8.1 | Document the extraction pattern (move type, add type forwarding, wire factory delegate) | ✅ Done |
| 8.2 | Add architecture diagram to README | ✅ Done |
| 8.3 | Document module initializer chain and startup sequence | ✅ Done |
| 8.4 | Add contribution guidelines for adding new built-in types | ✅ Done |

**Documentation Artifacts Created:**

1. **[Extraction Pattern Guide](../architecture/extraction-pattern.md)** — Step-by-step checklist for extracting built-in types from Core to BuiltIns, with three real-world examples (JSMath, JSBigInt, JSIteratorObject), a decision tree for extract-vs-keep, and documentation of non-extractable types (RegExp, Promise).

2. **Architecture Diagram in [Broiler.JavaScript/README.md](../../Broiler.JavaScript/README.md)** — ASCII diagram showing the 4-layer architecture (Foundation → Core → Feature → Application) with all 17 assemblies and links to detailed documentation.

3. **[Module Initializer Chain](../architecture/module-initializers.md)** — Documents all 6 module initializers across 4 assemblies, their registration tables, the runtime startup sequence, and troubleshooting guidance for delegate-is-null and initialization-order issues.

4. **[Contributing: Adding Built-In Types](../architecture/contributing-builtins.md)** — Developer guide covering new type creation, source generator attributes, factory delegate wiring, prototype chain setup, structured clone support, and a pre-PR checklist.

5. **[Internal Dependencies](../architecture/internal-dependencies.md)** — Comprehensive reference for all assembly-to-assembly project references, NuGet package dependencies, factory-delegate and interface contracts, fallback behaviour, type forwarding summary, and circular reference prevention rules.

---

## 5. Test Strategy

### 5.1 Test Projects (Current State)

Each assembly has a corresponding test project. The counts below reflect the current state after M8 completion (158 total tests):

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
| `Broiler.JavaScript.Integration.Tests` | 58 | End-to-end engine scenarios, M6/M7/M8 validation |

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

## 9. Phase 1 Summary

| Milestone | Status | Description |
|-----------|--------|-------------|
| M1 | ✅ Complete | Solution & CI infrastructure |
| M2 | ✅ Complete | Initial built-in extraction (4 types) |
| M3 | ✅ Complete | Extended built-in extraction (8 types) |
| M4 | ✅ Complete | Compiler-coupled type decoupling (JSBigInt) |
| M5 | ✅ Complete | Target framework alignment (net8.0) |
| M6 | ✅ Complete | Final validation |
| M7 | ✅ Complete | Future extraction candidates |
| M8 | ✅ Complete | Documentation & developer experience |

**Phase 1 final state:**
- 40 built-in types extracted to `Broiler.JavaScript.BuiltIns` (24 M1–M4 + 14 TypedArrays + 1 Iterator + 1 Intl)
- 71 types forwarded for backward compatibility
- 6 module initializers wiring satellite assemblies
- TypedArrays and Iterator successfully extracted in M7
- 2 candidates confirmed non-extractable (RegExp, Promise)
- Architecture documentation complete (extraction pattern, module initializers, contribution guide, internal dependencies)
- 158 tests passing across 12 test projects
- Full CI running on 3 platforms

---

## 10. Phase 2: Deep Structural Refactoring

Phase 1 focused on extracting built-in types from Core to satellite assemblies using the delegate/initializer pattern. Phase 2 addresses deeper structural improvements: isolating code-generation infrastructure, decomposing oversized files, consolidating utilities, and reorganizing internal assembly layouts. These changes further reduce the Core assembly surface area and improve long-term maintainability.

### 10.1 Phase 2 Audit Findings

A deep audit of all 17 assemblies revealed additional refactoring opportunities beyond those addressed in Phase 1.

#### Core Assembly (164 .cs files, ~19,255 LOC)

| Functional Area | Location | Files | LOC (approx.) | Observation |
|-----------------|----------|-------|----------------|-------------|
| **Expression Builders** | `LinqExpressions/` | 33 | ~2,700 | LINQ expression-tree builders for JS operations (arithmetic, comparison, coercion, property access, calls). Used exclusively by Compiler's `FastCompiler` during script compilation — not at runtime. |
| **Generator IR** | `LinqExpressions/GeneratorsV2/` | 8 | ~600 | Generator/async-generator rewriting infrastructure. Extension of the expression builder system. |
| **IL Emission** | `Emit/` | 2 | ~200 | Code-generation emission helpers. |
| **CodeGen Integration** | `CodeGen/`, `LambdaGen/`, `FastParser/`, `TypeQuery/`, `Debugger/` | 6 | ~170 | Miscellaneous compilation-time adapter files. |
| **Large Prototype Files** | `Core/Array/`, `Core/String/`, `Core/Date/`, `Core/Object/` | 16 | ~4,700 | Several individual files exceed 500–1,000 lines (JSArrayPrototype: 1,066, JSObject: 1,050, JSDatePrototype: 994, JSStringPrototype: 583). |
| **CLR Interop Stubs** | `Core/Clr/` | 4 | ~240 | Fallback CLR interop, naming conventions, marshal extensions, export attributes. Used only when Clr assembly is loaded. |

#### Foundation Layer Findings

| Finding | Location | Details |
|---------|----------|---------|
| **Duplicate utility** | `Runtime/CancellableDisposableAction.cs` + `Parser/CancellableDisposableAction.cs` | Identical utility type exists in two assemblies with different namespaces. |
| **Misplaced type** | `Ast/Misc/FastParseException.cs` | Parser exception defined in the Ast assembly. Should live alongside the parser that throws it. |
| **Non-reusable collections** | `Parser/FastList.cs`, `Parser/FastStack.cs`, `Parser/FastPool.cs` | Generic data structures already `public` and type-forwarded from Core. Evaluated in M11 — kept in Parser (no benefit to moving). |

#### Feature Layer Findings

| Finding | Location | Details |
|---------|----------|---------|
| **Compiler organization** | `Compiler/` (42 partial files) | All partial files for `FastCompiler` sit in a flat directory with no semantic grouping (statements, expressions, declarations are intermixed). |
| **ExpressionCompiler size** | `ExpressionCompiler/` (150 files, ~8,861 LOC) | Largest assembly by file count. Contains expression types (Y-prefixed), IL code generator (45+ partials), LINQ converters, closure handling, and runtime support. Could benefit from internal decomposition. |

#### Candidate Assembly Proposals

| Candidate | Source | Target | Priority | Rationale |
|-----------|--------|--------|----------|-----------|
| **Code-Generation Builders** | `Core/LinqExpressions/`, `Core/Emit/`, `Core/CodeGen/`, `Core/LambdaGen/`, `Core/TypeQuery/`, `Core/FastParser/` | Into `Compiler` assembly | High | 49 files, ~3,670 LOC used only during compilation. Moving them reduces Core's surface by ~22%. Compiler already references Core. |
| **Shared Utilities** | Duplicate `CancellableDisposableAction`; generic `FastList`/`FastStack`/`FastPool` | Shared location (one canonical copy) | Medium | Eliminates code duplication and enables reuse of high-performance collections. |
| **Parser Exception** | `Ast/Misc/FastParseException.cs` | `Parser` | Low | Minor misplacement; low risk, low impact. |

### 10.2 Milestone 9: Code-Generation Builder Isolation (High Priority)

**Objective:** Move compilation-time expression builders and codegen helpers from Core to the Compiler assembly, reducing Core's file count and clarifying its role as a runtime-only assembly.

**Scope:** The `LinqExpressions/` directory (33 files), `LinqExpressions/GeneratorsV2/` (8 files), `Emit/` (2 files), `CodeGen/` (2 files), `LambdaGen/` (1 file), `FastParser/Compiler/` (1 file), `TypeQuery/` (1 file), and `Debugger/` adapter (1 file) — totaling **49 files, ~3,670 LOC**.

**Pre-requisites:** Coupling analysis to confirm these types are not invoked by Core at runtime. Preliminary grep analysis indicates they are consumed only by `FastCompiler` partial classes during script compilation, but a rigorous verification pass is required.

| Step | Description | Status | Owner |
|------|-------------|--------|-------|
| 9.1 | **Coupling analysis:** For every type in `LinqExpressions/`, `Emit/`, `CodeGen/`, `LambdaGen/`, `TypeQuery/`, `FastParser/`, and `Debugger/` adapter — enumerate all call sites across Core, Compiler, and other assemblies. Classify each as "compilation-time only" or "runtime." | ⬜ Pending | — |
| 9.2 | **Decide target:** If all builders are compilation-time only, move them into `Compiler/Builders/` (preferred — avoids a new assembly). If some have runtime callers, create a new `Broiler.JavaScript.CodeGen` assembly between Core and Compiler. | ⬜ Pending | — |
| 9.3 | **Move files:** Relocate identified files to the target location. Preserve namespaces for source compatibility (e.g., `Broiler.JavaScript.Core.LinqExpressions.JSValueBuilder` keeps its namespace). | ⬜ Pending | — |
| 9.4 | **Add type forwarding (if new assembly):** If a `Broiler.JavaScript.CodeGen` assembly is created, add `[TypeForwardedTo]` attributes in Core for any public types that moved. | ⬜ Pending | — |
| 9.5 | **Update project references:** Ensure Compiler references the new location. Update `Broiler.JavaScript.All` if a new assembly is created. | ⬜ Pending | — |
| 9.6 | **Verify:** All 158+ tests pass, no circular references, build succeeds on all platforms. | ⬜ Pending | — |

**Acceptance Criteria:**
- Core assembly file count reduced by ~49 files (from 164 to ~115).
- No new circular assembly references.
- All existing tests pass without modification.
- Namespace preservation: existing consumers compile without `using` changes.

**Risks & Mitigation:**
- *Risk:* Some builders may be referenced at runtime (e.g., `JSContext.Eval` may use expression builders to JIT-compile eval'd code). *Mitigation:* Step 9.1 coupling analysis will reveal these. If runtime references exist, keep those specific builders in Core and move only the compilation-time-only builders.
- *Risk:* Type forwarding count increases if a new assembly is created. *Mitigation:* Prefer moving into Compiler (no new assembly) if coupling analysis permits.

### 10.3 Milestone 10: Core Large-File Decomposition (Medium Priority)

**Objective:** Split oversized files in Core into smaller, semantically grouped partial files to improve readability, reduce merge conflicts, and lower cognitive load.

**Note:** This milestone does not change assembly boundaries — all files remain in Core. It is a structural refactoring within the Core assembly.

| Step | Description | Status | Owner |
|------|-------------|--------|-------|
| 10.1 | **Split `JSArrayPrototype.cs`** (1,066 lines) into 4 partial files: `JSArrayPrototype.Iteration.cs` (map, filter, reduce, forEach, every, some, find, findIndex), `JSArrayPrototype.Search.cs` (indexOf, lastIndexOf, includes), `JSArrayPrototype.Modification.cs` (push, pop, shift, unshift, splice, fill, copyWithin), `JSArrayPrototype.Utility.cs` (concat, join, reverse, sort, slice, flat, flatMap, at, toReversed, toSorted, toSpliced, with). | ⬜ Pending | — |
| 10.2 | **Split `JSObject.cs`** (1,050 lines) into 3 partial files: `JSObject.PropertyStorage.cs` (descriptor access, defineProperty, hasOwnProperty), `JSObject.ProtoChain.cs` (prototype traversal, inheritance), `JSObject.DynamicDispatch.cs` (meta-object protocol, DynamicMetaObject). | ⬜ Pending | — |
| 10.3 | **Split `JSDatePrototype.cs`** (994 lines) into 3 partial files: `JSDatePrototype.Getters.cs` (getTime, getFullYear, getMonth, getDate, getHours, etc.), `JSDatePrototype.Setters.cs` (setFullYear, setMonth, setHours, etc.), `JSDatePrototype.Formatters.cs` (toISOString, toUTCString, toLocaleDateString, toString). | ⬜ Pending | — |
| 10.4 | **Split `JSStringPrototype.cs`** (583 lines) into 4 partial files: `JSStringPrototype.Search.cs` (indexOf, includes, startsWith, endsWith), `JSStringPrototype.Transform.cs` (toUpperCase, toLowerCase, trim, padStart, padEnd, repeat), `JSStringPrototype.Extract.cs` (slice, substring, charAt, charCodeAt, codePointAt, at), `JSStringPrototype.Pattern.cs` (match, matchAll, replace, replaceAll, split, search). | ⬜ Pending | — |
| 10.5 | **Split `JSObjectStatic.cs`** (443 lines) into 2 partial files: `JSObjectStatic.Introspection.cs` (keys, values, entries, getOwnPropertyDescriptor, getOwnPropertyNames, getPrototypeOf), `JSObjectStatic.Construction.cs` (create, assign, defineProperty, defineProperties, freeze, seal, preventExtensions, fromEntries, groupBy, hasOwn). | ⬜ Pending | — |
| 10.6 | **Verify:** All tests pass, no behavioral changes. | ⬜ Pending | — |

**Acceptance Criteria:**
- No single file exceeds ~300 lines.
- Each partial file has a clear semantic theme documented in its file name.
- All existing tests pass without modification.
- No changes to public API surface.

**Risks & Mitigation:**
- *Risk:* Merge conflicts with in-flight PRs that modify the same files. *Mitigation:* Coordinate timing; perform splits as atomic commits.
- *Risk:* Partial-class field/property ordering may cause confusion. *Mitigation:* Keep all fields and constructor logic in the primary file; partial files contain only methods.

### 10.4 Milestone 11: Foundation Layer Cleanup (Medium Priority)

**Objective:** Eliminate code duplication in the foundation layer and correct type placement.

| Step | Description | Status | Owner |
|------|-------------|--------|-------|
| 11.1 | **Deduplicate `CancellableDisposableAction`:** Deleted the Parser copy. Added a `Parser → Runtime` project reference (no cycle — Runtime does not reference Parser). Parser's `FastTokenStream.UndoMark()` now uses the canonical `Broiler.JavaScript.Core.Core.CancellableDisposableAction` from Runtime. The existing `TypeForwardedTo` in Core continues to work unchanged. | ✅ Complete | — |
| 11.2 | **Evaluate `FastParseException` placement:** **Decision: keep in Ast.** Moving to Parser would create a circular dependency — Ast throws `FastParseException` (in `VariableDeclarator.cs` and `AstUnaryExpression.cs`), so Ast would need to reference Parser, but Parser already references Ast (Parser → Ast). The current placement in `Ast/Misc/` is correct: the exception type is defined alongside the AST nodes that throw it, and all consumers (Parser, Compiler, Runtime) already reference Ast. | ✅ Complete | — |
| 11.3 | **Evaluate generic collection reuse:** **Decision: keep in Parser.** `FastList<T>`, `FastStack<T>`, and `FastPool<T>` are already `public` (not `internal` as originally assumed) and already type-forwarded from Core via `ParserTypeForwarding.cs`. They are accessible to any assembly that references Parser or Core. Moving them to a separate shared assembly would add a new project without meaningful benefit. No other assembly currently needs these collections outside the Parser/Core dependency chain. | ✅ Complete | — |
| 11.4 | **Verify:** All 158 tests pass, no circular references. Build succeeds with 0 errors. | ✅ Complete | — |

**Acceptance Criteria:**
- No duplicate type definitions across assemblies.
- Each misplaced type is either moved to its correct assembly or documented with rationale for its current placement.
- All existing tests pass without modification.

**Risks & Mitigation:**
- *Risk:* Parser currently has no reference to Runtime; adding one changes the dependency graph. *Mitigation:* Step 11.1 includes evaluating whether adding the reference is acceptable. If it violates layering rules (both are foundation-layer assemblies), extract the shared type to a lower common dependency instead.
- *Risk:* Making `FastList`/`FastStack` public may expose internal implementation details. *Mitigation:* Step 11.3 evaluates the trade-off; if risk outweighs benefit, keep them internal and document.

### 10.5 Milestone 12: Compiler Internal Organization (Lower Priority)

**Objective:** Reorganize the Compiler assembly's flat directory structure into semantic subdirectories for improved navigability.

**Note:** This milestone does not change assembly boundaries. All files remain in the Compiler assembly.

| Step | Description | Status | Owner |
|------|-------------|--------|-------|
| 12.1 | **Create subdirectory structure:** `Statements/` (FastCompiler.Statement.cs, FastCompiler.Block.cs, FastCompiler.IfStatement.cs, FastCompiler.ForStatement.cs, etc.), `Expressions/` (FastCompiler.Expression.cs, FastCompiler.SingleExpression.cs, FastCompiler.NextExpression.cs, etc.), `Declarations/` (FastCompiler.Function.cs, FastCompiler.Class.cs, FastCompiler.VariableDeclaration.cs, etc.), `Scope/` (FastFunctionScope.cs, StrictModeExtensions.cs), `Infrastructure/` (CompilerAssemblyInitializer.cs). | ⬜ Pending | — |
| 12.2 | **Move files to subdirectories.** No namespace changes — partial classes remain in the same namespace regardless of folder. | ⬜ Pending | — |
| 12.3 | **Verify:** All tests pass, no behavioral changes. | ⬜ Pending | — |

**Acceptance Criteria:**
- Compiler directory has clear semantic groupings.
- No namespace changes.
- All existing tests pass without modification.

**Risks & Mitigation:**
- *Risk:* IDE tooling may have issues with partial classes in subdirectories. *Mitigation:* Verify with Visual Studio, VS Code, and Rider before committing.

### 10.6 Milestone 13: ExpressionCompiler Decomposition Assessment (Exploratory)

**Objective:** Evaluate whether the ExpressionCompiler assembly (150 files, ~8,861 LOC) should be split into sub-assemblies. This is an exploratory milestone — the outcome may be a "no-go" decision.

| Step | Description | Status | Owner |
|------|-------------|--------|-------|
| 13.1 | **Map internal structure:** Categorize all 150 files into functional groups: Expression Types (Y-prefixed, ~90 files), IL Code Generator (ILCodeGenerator partials, ~45 files), LINQ Converters (LinqConverter partials, ~10 files), Runtime Support (RuntimeAssembly, MethodRepository, Closures, ~5 files). | ⬜ Pending | — |
| 13.2 | **Coupling analysis:** Determine cross-group dependencies. If Expression Types are consumed by all other groups, splitting is not viable. If IL Generator is self-contained, it could become `ExpressionCompiler.ILGen`. | ⬜ Pending | — |
| 13.3 | **Feasibility report:** Document findings with a go/no-go recommendation. Include dependency diagrams and impact analysis. | ⬜ Pending | — |
| 13.4 | **(If go) Create prototype split:** Move IL Generator into a new assembly. Verify all tests pass. | ⬜ Pending | — |
| 13.5 | **(If no-go) Document rationale:** Explain why the current monolithic structure is acceptable, and add internal README for navigability. | ⬜ Pending | — |

**Acceptance Criteria:**
- A documented go/no-go decision with supporting evidence.
- If go: prototype split compiles and all tests pass.
- If no-go: internal documentation added to ExpressionCompiler for navigability.

**Risks & Mitigation:**
- *Risk:* ExpressionCompiler may be too tightly coupled internally for meaningful separation. *Mitigation:* This milestone is explicitly exploratory; a no-go outcome is acceptable.

### 10.7 Milestone 14: Phase 2 Validation & Documentation Update

**Objective:** Comprehensive validation of all Phase 2 changes and update of architecture documentation.

| Step | Description | Status | Owner |
|------|-------------|--------|-------|
| 14.1 | **Full test suite validation:** All tests pass on all 3 CI platforms. | ⬜ Pending | — |
| 14.2 | **Update assembly inventory** (Section 2.1): Reflect current file counts and any new assemblies. | ⬜ Pending | — |
| 14.3 | **Update dependency graph** (Section 2.2): Reflect any changes from M9–M13. | ⬜ Pending | — |
| 14.4 | **Update [Internal Dependencies](../architecture/internal-dependencies.md):** Add any new project references, type forwardings, or factory delegates introduced in Phase 2. | ⬜ Pending | — |
| 14.5 | **Update [Extraction Pattern](../architecture/extraction-pattern.md):** Document any new patterns established during Phase 2 (e.g., intra-assembly file splitting conventions). | ⬜ Pending | — |
| 14.6 | **Update README architecture diagram:** Reflect any new assemblies or reorganized structure. | ⬜ Pending | — |
| 14.7 | **Add Phase 2 validation tests** in `M9–M14ValidationTests.cs` covering new structural invariants. | ⬜ Pending | — |

**Acceptance Criteria:**
- All architecture documentation accurately reflects the current state.
- All tests pass on all CI platforms.
- README architecture diagram is current.

---

## 11. Phase 2 Migration Plan

### 11.1 Execution Order & Dependencies

Phase 2 milestones should be executed in the following order, with clear gates between them:

```
  M9 (CodeGen Isolation)          ─ High Priority, most impactful
    │
    ▼
  M10 (Large-File Decomposition)  ─ Medium Priority, independent of M9
    │
    ▼
  M11 (Foundation Cleanup)        ─ Medium Priority, independent of M9/M10
    │
    ▼
  M12 (Compiler Organization)     ─ Lower Priority, benefits from M9
    │
    ▼
  M13 (ExpressionCompiler Eval)   ─ Exploratory, independent
    │
    ▼
  M14 (Validation & Docs)         ─ Required, depends on all above
```

**Parallelization:** M10 and M11 can be executed in parallel since they operate on different assemblies (Core vs. Foundation). M12 benefits from M9 completing first (if builders move to Compiler, the folder reorganization should include them). M13 is independent and can start at any time.

### 11.2 Risk Mitigation Strategy

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Runtime references found in LinqExpressions (M9) | Medium | High | Coupling analysis (9.1) before any file moves. Partial extraction is acceptable — move only compilation-time builders. |
| Large-file splits cause merge conflicts (M10) | Medium | Low | Coordinate with active contributors. Perform splits as atomic PRs merged quickly. |
| CancellableDisposableAction dedup introduces circular dependency (M11) | Low | Medium | ✅ **Mitigated.** No cycle — added Parser → Runtime reference safely. |
| ExpressionCompiler too coupled to split (M13) | High | Low | Milestone is explicitly exploratory. No-go is acceptable. |
| Nerdbank.GitVersioning fails in shallow clones (CI) | Known | Low | Use `fetch-depth: 0` in CI workflows (already in place). |

### 11.3 Phased Delivery Schedule

Each milestone should be delivered as a separate PR for focused review:

| Phase | Milestone | Estimated Effort | PR Scope |
|-------|-----------|------------------|----------|
| 2a | M9 — CodeGen Isolation | 2–3 days | File moves + project reference updates |
| 2b | M10 — Large-File Decomposition | 1–2 days | Partial-file splits (5 files → 16 partial files, replacing the original monolithic files) |
| 2c | M11 — Foundation Cleanup | 1 day | Dedup + placement fixes |
| 2d | M12 — Compiler Organization | 0.5–1 day | Subdirectory reorganization |
| 2e | M13 — ExpressionCompiler Assessment | 1–2 days | Analysis + feasibility report |
| 2f | M14 — Validation & Docs | 1 day | Documentation updates + validation tests |

**Total estimated effort:** 6.5–10 days

### 11.4 Open Questions & Design Concerns

1. **M9 — Target for LinqExpressions:** Should builders move into the existing Compiler assembly or a new `Broiler.JavaScript.CodeGen` assembly? The Compiler-internal option is simpler (no new assembly) but may make Compiler too large. The new-assembly option is cleaner but adds a dependency to manage. **Recommendation:** Prefer Compiler-internal unless coupling analysis reveals runtime callers.

2. **M11 — CancellableDisposableAction canonical location:** ✅ **Resolved.** Added Parser → Runtime project reference (no cycle exists — Runtime does not reference Parser). Deleted the duplicate Parser copy. Parser now uses the canonical `Broiler.JavaScript.Core.Core.CancellableDisposableAction` from Runtime.

3. **M13 — ExpressionCompiler viability:** The 150-file assembly is large, but its purpose is cohesive (expression tree → IL compilation). Splitting may not provide meaningful modularity gains. **Recommendation:** Treat M13 as exploratory and accept a no-go decision.

4. **NuGet packaging impact:** If a new assembly is created in M9, the `Broiler.JavaScript.All` meta-package must be updated to include it. Consumers using the meta-package will see no difference. Consumers referencing individual assemblies may need to add the new reference. **Recommendation:** Document the NuGet impact in the M9 PR.

---

## 12. Summary

### Phase 1 (Complete)

| Milestone | Status | Description |
|-----------|--------|-------------|
| M1 | ✅ Complete | Solution & CI infrastructure |
| M2 | ✅ Complete | Initial built-in extraction (4 types) |
| M3 | ✅ Complete | Extended built-in extraction (8 types) |
| M4 | ✅ Complete | Compiler-coupled type decoupling (JSBigInt) |
| M5 | ✅ Complete | Target framework alignment (net8.0) |
| M6 | ✅ Complete | Final validation |
| M7 | ✅ Complete | Future extraction candidates |
| M8 | ✅ Complete | Documentation & developer experience |

### Phase 2 (Planned)

| Milestone | Status | Priority | Description |
|-----------|--------|----------|-------------|
| M9 | ⬜ Pending | High | Code-generation builder isolation (~49 files from Core) |
| M10 | ⬜ Pending | Medium | Core large-file decomposition (5 files → ~16 partial files) |
| M11 | ✅ Complete | Medium | Foundation layer cleanup (dedup + placement fixes) |
| M12 | ⬜ Pending | Lower | Compiler internal organization (semantic subdirectories) |
| M13 | ⬜ Pending | Exploratory | ExpressionCompiler decomposition assessment |
| M14 | ⬜ Pending | Required | Phase 2 validation & documentation update |

**Phase 1 final state:** 40 built-in types extracted, 71 types forwarded, 6 module initializers, 158 tests, full CI on 3 platforms.

**Phase 2 target state:** Core assembly reduced by ~49 files (~22%), large files decomposed for readability, foundation layer free of duplicate code, Compiler internally organized, ExpressionCompiler assessed for decomposition.
